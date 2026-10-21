using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App.Rendering;

/// <summary>
/// 詰めた形式のまま転送し、シェーダでビットを展開して描く。1ピクセル(RGBA)に32セルが入る。
/// 縮小表示では、シェーダが1ピクセルの範囲の生存密度を求めるので、チラつきにくい。
/// </summary>
internal sealed class PackedBoardRenderer : IBoardRenderer
{
    private const int CellsPerTexel = 32;

    private readonly Texture2D _texture;
    private readonly uint[] _staging;
    private readonly Effect _effect;
    private readonly int _width;
    private readonly int _height;

    public PackedBoardRenderer(GraphicsDevice device, Effect effect, int width, int height)
    {
        _width = width;
        _height = height;
        _effect = effect;
        _texture = new Texture2D(device, width / CellsPerTexel, height, false, SurfaceFormat.Color);
        _staging = new uint[width / CellsPerTexel * height];

        _effect.Parameters["BoardSize"].SetValue(new Vector2(width, height));
        _effect.Parameters["PackedWidth"].SetValue((float)(width / CellsPerTexel));
        _effect.Parameters["AliveColor"].SetValue(BoardColors.Alive.ToVector4());
        _effect.Parameters["DeadColor"].SetValue(BoardColors.Dead.ToVector4());
        _effect.Parameters["GridColor"].SetValue(BoardColors.Grid.ToVector4());
    }

    public string Name => "Packed bits (shader unpack)";

    public double LastUploadMilliseconds { get; private set; }

    public void Upload(ReadOnlySpan<ulong> packed)
    {
        long start = Stopwatch.GetTimestamp();
        // リトルエンディアンでは、ulong の下位32ビットが先頭の32セル。そのまま uint 列として見れば、
        // R が先頭8セル、G が次の8セル…という並びのピクセルになる
        MemoryMarshal.Cast<ulong, uint>(packed).CopyTo(_staging);
        _texture.SetData(_staging);
        LastUploadMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    public void Draw(SpriteBatch spriteBatch, Matrix boardToScreen, Viewport viewport)
    {
        var projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, -1);
        _effect.Parameters["MatrixTransform"].SetValue(boardToScreen * projection);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, effect: _effect);
        spriteBatch.Draw(_texture, new Rectangle(0, 0, _width, _height), Color.White);
        spriteBatch.End();
    }

    public void Dispose() => _texture.Dispose();
}
