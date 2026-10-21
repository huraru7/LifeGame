namespace LifeGame.Core;

/// <summary>盤面上の長方形の範囲(セル単位)。</summary>
public readonly record struct CellRegion(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public bool Contains(long x, long y) => x >= Left && x < Right && y >= Top && y < Bottom;
}
