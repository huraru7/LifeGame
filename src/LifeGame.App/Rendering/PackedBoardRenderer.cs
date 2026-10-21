using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App.Rendering;

/// <summary>
/// 標準の描画。詰めた形式のまま転送し(1ピクセルに32セル)、GPUで1セル1ピクセルの画像へ展開して
/// ミップマップを作る。表示は <c>BoardDisplay.fx</c> が、拡大時は隙間つきの四角、縮小時は生存密度で描く。
/// </summary>
internal sealed class PackedBoardRenderer : IBoardRenderer
{
    private const int CellsPerTexel = 32;

    private static readonly SamplerState CellSampler = new()
    {
        // 拡大は点で(セルの縁をぼかさない)、縮小はミップマップの段の間も滑らかに。
        // 盤面の外は繰り返して読む(ミップマップも端が反対側とつながる)
        Filter = TextureFilter.MinLinearMagPointMipLinear,
        AddressU = TextureAddressMode.Wrap,
        AddressV = TextureAddressMode.Wrap,
        AddressW = TextureAddressMode.Wrap,
    };

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _spriteBatch;
    private readonly Effect _expandEffect;
    private readonly Effect _displayEffect;
    private readonly Texture2D _packedTexture;
    private readonly RenderTarget2D _cells;
    private readonly uint[] _staging;
    private readonly int _width;
    private readonly int _height;

    public PackedBoardRenderer(GraphicsDevice device, Effect expandEffect, Effect displayEffect, int width, int height)
    {
        _device = device;
        _spriteBatch = new SpriteBatch(device);
        _expandEffect = expandEffect;
        _displayEffect = displayEffect;
        _width = width;
        _height = height;
        _packedTexture = new Texture2D(device, width / CellsPerTexel, height, false, SurfaceFormat.Color);
        _cells = new RenderTarget2D(device, width, height, mipMap: true, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _staging = new uint[width / CellsPerTexel * height];
    }

    public string Name => "標準(GPUで展開)";

    public double LastUploadMilliseconds { get; private set; }

    public void Upload(ReadOnlySpan<ulong> packed)
    {
        long start = Stopwatch.GetTimestamp();
        // リトルエンディアンでは、ulong の下位32ビットが先頭の32セル。そのまま uint 列として見れば、
        // R が先頭8セル、G が次の8セル…という並びのピクセルになる
        MemoryMarshal.Cast<ulong, uint>(packed).CopyTo(_staging);
        _packedTexture.SetData(_staging);
        Expand();
        LastUploadMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    public void Draw(SpriteBatch spriteBatch, Matrix boardToScreen, Viewport viewport, Rectangle visibleArea)
    {
        var projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, -1);
        _displayEffect.Parameters["MatrixTransform"].SetValue(boardToScreen * projection);
        _displayEffect.Parameters["BoardSize"].SetValue(new Vector2(_width, _height));
        _displayEffect.Parameters["AliveColor"].SetValue(BoardColors.Alive.ToVector4());
        _displayEffect.Parameters["DeadColor"].SetValue(BoardColors.Dead.ToVector4());
        _displayEffect.Parameters["GapColor"].SetValue(BoardColors.Gap.ToVector4());

        // 描く先も読む元も同じ盤面座標の範囲にする。範囲が盤面の外にはみ出すと、テクスチャ座標が 0〜1 の外になり、
        // サンプラーの Wrap で盤面が繰り返される
        spriteBatch.Begin(blendState: BlendState.Opaque, samplerState: CellSampler, effect: _displayEffect);
        spriteBatch.Draw(_cells, visibleArea, visibleArea, Color.White);
        spriteBatch.End();
    }

    public void Dispose()
    {
        _packedTexture.Dispose();
        _cells.Dispose();
        _spriteBatch.Dispose();
    }

    // 詰めたテクスチャを1セル1ピクセルの画像へ展開する。描画先を戻すときにミップマップが作られる
    private void Expand()
    {
        var previousTargets = _device.GetRenderTargets();
        _device.SetRenderTarget(_cells);

        _expandEffect.Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(0, _width, _height, 0, 0, -1));
        _expandEffect.Parameters["BoardSize"].SetValue(new Vector2(_width, _height));
        _expandEffect.Parameters["PackedWidth"].SetValue((float)(_width / CellsPerTexel));
        _spriteBatch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp, effect: _expandEffect);
        _spriteBatch.Draw(_packedTexture, new Rectangle(0, 0, _width, _height), Color.White);
        _spriteBatch.End();

        if (previousTargets.Length == 0)
            _device.SetRenderTarget(null);
        else
            _device.SetRenderTargets(previousTargets);
    }
}
