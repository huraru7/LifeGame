using System.Diagnostics;
using LifeGame.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App.Rendering;

/// <summary>
/// 比較用の素朴な描画。CPUで1セル1ピクセル(4バイト)の色に展開してから転送する。
/// 転送量は詰めた形式の32倍になり、縮小表示では点の間引きでチラつく。
/// </summary>
internal sealed class ColorBoardRenderer : IBoardRenderer
{
    private readonly Texture2D _texture;
    private readonly Color[] _pixels;
    private readonly int _width;
    private readonly int _height;

    public ColorBoardRenderer(GraphicsDevice device, int width, int height)
    {
        _width = width;
        _height = height;
        _texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
        _pixels = new Color[width * height];
    }

    public string Name => "Color (CPU unpack)";

    public double LastUploadMilliseconds { get; private set; }

    public void Upload(ReadOnlySpan<ulong> packed)
    {
        long start = Stopwatch.GetTimestamp();
        Color alive = BoardColors.Alive;
        Color dead = BoardColors.Dead;
        for (int i = 0; i < packed.Length; i++)
        {
            ulong word = packed[i];
            int baseIndex = i * PackedBoard.CellsPerWord;
            for (int bit = 0; bit < PackedBoard.CellsPerWord; bit++)
            {
                _pixels[baseIndex + bit] = ((word >> bit) & 1UL) != 0 ? alive : dead;
            }
        }
        _texture.SetData(_pixels);
        LastUploadMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    public void Draw(SpriteBatch spriteBatch, Matrix boardToScreen, Viewport viewport)
    {
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: boardToScreen);
        spriteBatch.Draw(_texture, new Rectangle(0, 0, _width, _height), Color.White);
        spriteBatch.End();
    }

    public void Dispose() => _texture.Dispose();
}
