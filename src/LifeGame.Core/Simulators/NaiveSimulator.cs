namespace LifeGame.Core.Simulators;

/// <summary>
/// 1セル1バイトの素朴な実装。ほかの方式の正しさを確かめる基準にする。
/// 外周に1マスのゴーストセルを持たせて端の判定をなくし、横3マスの和を行ごとに作って使い回す。
/// </summary>
public sealed class NaiveSimulator : ILifeSimulator
{
    private readonly int _stride;
    private byte[] _current;
    private byte[] _next;
    // 各行の横3マスの和。ゴースト行を含む (Height + 2) 行 × Width 列
    private readonly byte[] _rowSums;

    public NaiveSimulator(int width, int height)
    {
        PackedBoard.ValidateSize(width, height);
        Width = width;
        Height = height;
        _stride = width + 2;
        _current = new byte[_stride * (height + 2)];
        _next = new byte[_current.Length];
        _rowSums = new byte[width * (height + 2)];
    }

    public string Name => "Naive";

    public int Width { get; }

    public int Height { get; }

    public void Load(ReadOnlySpan<ulong> packed)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _current[(y + 1) * _stride + x + 1] = PackedBoard.GetCell(packed, Width, x, y) ? (byte)1 : (byte)0;
            }
        }
    }

    public void CopyTo(Span<ulong> packed)
    {
        packed[..PackedBoard.Length(Width, Height)].Clear();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_current[(y + 1) * _stride + x + 1] != 0)
                    PackedBoard.SetCell(packed, Width, x, y, true);
            }
        }
    }

    public void SetCell(int x, int y, bool alive)
    {
        _current[(y + 1) * _stride + x + 1] = alive ? (byte)1 : (byte)0;
    }

    public void Step()
    {
        FillGhosts();

        byte[] cur = _current;
        byte[] sums = _rowSums;
        int width = Width;
        int stride = _stride;

        for (int y = 0; y < Height + 2; y++)
        {
            int src = y * stride;
            int dst = y * width;
            for (int x = 0; x < width; x++)
            {
                sums[dst + x] = (byte)(cur[src + x] + cur[src + x + 1] + cur[src + x + 2]);
            }
        }

        byte[] next = _next;
        for (int y = 1; y <= Height; y++)
        {
            int above = (y - 1) * width;
            int middle = y * width;
            int below = (y + 1) * width;
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                byte self = cur[row + x + 1];
                // 縦3行分の和から自分を引いたものが近傍の数
                int neighbors = sums[above + x] + sums[middle + x] + sums[below + x] - self;
                next[row + x + 1] = (byte)(neighbors == 3 || (neighbors == 2 && self != 0) ? 1 : 0);
            }
        }

        (_current, _next) = (_next, _current);
    }

    // トーラスになるように、反対側の端をゴーストセルへ写す
    private void FillGhosts()
    {
        byte[] cur = _current;
        for (int y = 1; y <= Height; y++)
        {
            int row = y * _stride;
            cur[row] = cur[row + Width];
            cur[row + Width + 1] = cur[row + 1];
        }
        Array.Copy(cur, Height * _stride, cur, 0, _stride);
        Array.Copy(cur, _stride, cur, (Height + 1) * _stride, _stride);
    }
}
