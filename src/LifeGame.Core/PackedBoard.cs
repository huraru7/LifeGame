namespace LifeGame.Core;

/// <summary>
/// 盤面の「詰めた形式」: 行優先で、1行を Width/64 個の ulong で表す。
/// ワードのビット i が、そのワードの先頭セルから i 番目(右方向)のセルに対応する。
/// リトルエンディアンでは、このまま uint 列として見ると、テクスチャ1ピクセル(RGBA)に32セルが入る。
/// </summary>
public static class PackedBoard
{
    public const int CellsPerWord = 64;

    public static int WordsPerRow(int width) => width / CellsPerWord;

    public static int Length(int width, int height) => WordsPerRow(width) * height;

    /// <summary>盤面サイズが、全方式で扱える条件を満たすか確かめる。</summary>
    public static void ValidateSize(int width, int height)
    {
        if (width < CellsPerWord || width % CellsPerWord != 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "幅は64の倍数で、64以上にしてください。");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), height, "高さは1以上にしてください。");
    }

    public static bool GetCell(ReadOnlySpan<ulong> packed, int width, int x, int y)
    {
        ulong word = packed[y * WordsPerRow(width) + x / CellsPerWord];
        return ((word >> (x % CellsPerWord)) & 1UL) != 0;
    }

    public static void SetCell(Span<ulong> packed, int width, int x, int y, bool alive)
    {
        ref ulong word = ref packed[y * WordsPerRow(width) + x / CellsPerWord];
        ulong mask = 1UL << (x % CellsPerWord);
        word = alive ? word | mask : word & ~mask;
    }

    /// <summary>立っているビットの数(生存セルの数)を数える。</summary>
    public static long CountPopulation(ReadOnlySpan<ulong> words)
    {
        long population = 0;
        foreach (ulong word in words)
            population += System.Numerics.BitOperations.PopCount(word);
        return population;
    }

    /// <summary>指定した範囲を、生存率 density のランダムな配置で埋める(範囲外は変更しない)。</summary>
    public static void Randomize(Span<ulong> packed, int width, int height, double density, int seed,
        int left = 0, int top = 0, int regionWidth = int.MaxValue, int regionHeight = int.MaxValue)
    {
        var random = new Random(seed);
        int right = Math.Min(width, left + Math.Min(regionWidth, width));
        int bottom = Math.Min(height, top + Math.Min(regionHeight, height));
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                SetCell(packed, width, x, y, random.NextDouble() < density);
            }
        }
    }
}
