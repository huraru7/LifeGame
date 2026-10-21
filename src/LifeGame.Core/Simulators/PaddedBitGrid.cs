namespace LifeGame.Core.Simulators;

/// <summary>
/// 外周に1ワード・1行のゴーストを持つビットボードの配置。
/// 各行の左右と上下にゴーストを置くことで、隣のワードを折り返しなしの連続した位置から読める(SIMDで一括で読める)。
/// 内側のセル (行 y, ワード x) は、配列の (y + 1) * Stride + x + 1 に置く。
/// </summary>
internal sealed class PaddedBitGrid
{
    public PaddedBitGrid(int width, int height)
    {
        PackedBoard.ValidateSize(width, height);
        Width = width;
        Height = height;
        WordsPerRow = PackedBoard.WordsPerRow(width);
        Stride = WordsPerRow + 2;
    }

    public int Width { get; }

    public int Height { get; }

    public int WordsPerRow { get; }

    public int Stride { get; }

    public int BufferLength => Stride * (Height + 2);

    public int IndexOf(int y, int wordX) => (y + 1) * Stride + wordX + 1;

    /// <summary>トーラスになるように、反対側の端をゴーストへ写す。</summary>
    public void FillGhosts(ulong[] buffer)
    {
        for (int y = 1; y <= Height; y++)
        {
            int row = y * Stride;
            buffer[row] = buffer[row + WordsPerRow];
            buffer[row + WordsPerRow + 1] = buffer[row + 1];
        }
        Array.Copy(buffer, Height * Stride, buffer, 0, Stride);
        Array.Copy(buffer, Stride, buffer, (Height + 1) * Stride, Stride);
    }

    public void Load(ReadOnlySpan<ulong> packed, ulong[] buffer)
    {
        for (int y = 0; y < Height; y++)
        {
            packed.Slice(y * WordsPerRow, WordsPerRow).CopyTo(buffer.AsSpan(IndexOf(y, 0), WordsPerRow));
        }
    }

    public void CopyTo(ulong[] buffer, Span<ulong> packed)
    {
        for (int y = 0; y < Height; y++)
        {
            buffer.AsSpan(IndexOf(y, 0), WordsPerRow).CopyTo(packed.Slice(y * WordsPerRow, WordsPerRow));
        }
    }

    /// <summary>ゴーストを除いた内側だけの人口を数える。</summary>
    public long CountPopulation(ulong[] buffer)
    {
        long population = 0;
        for (int y = 0; y < Height; y++)
            population += PackedBoard.CountPopulation(buffer.AsSpan(IndexOf(y, 0), WordsPerRow));
        return population;
    }

    public void SetCell(ulong[] buffer, int x, int y, bool alive)
    {
        ref ulong word = ref buffer[IndexOf(y, x / PackedBoard.CellsPerWord)];
        ulong mask = 1UL << (x % PackedBoard.CellsPerWord);
        word = alive ? word | mask : word & ~mask;
    }
}
