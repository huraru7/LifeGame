namespace LifeGame.Core.Simulators;

/// <summary>
/// ulong 1つに64セルを詰めたビットボード。1スレッド・スカラー演算で、端は添字の折り返しで扱う。
/// </summary>
public sealed class BitboardSimulator : ILifeSimulator
{
    private readonly int _wordsPerRow;
    private ulong[] _current;
    private ulong[] _next;

    public BitboardSimulator(int width, int height)
    {
        PackedBoard.ValidateSize(width, height);
        Width = width;
        Height = height;
        _wordsPerRow = PackedBoard.WordsPerRow(width);
        _current = new ulong[PackedBoard.Length(width, height)];
        _next = new ulong[_current.Length];
    }

    public string Name => "Bitboard";

    public int Width { get; }

    public int Height { get; }

    public void Load(ReadOnlySpan<ulong> packed) => packed[.._current.Length].CopyTo(_current);

    public void CopyTo(Span<ulong> packed) => _current.CopyTo(packed);

    public void SetCell(int x, int y, bool alive) => PackedBoard.SetCell(_current, Width, x, y, alive);

    public long CountPopulation() => PackedBoard.CountPopulation(_current);

    public void Step()
    {
        ulong[] cur = _current;
        ulong[] next = _next;
        int words = _wordsPerRow;

        for (int y = 0; y < Height; y++)
        {
            int above = (y == 0 ? Height - 1 : y - 1) * words;
            int middle = y * words;
            int below = (y == Height - 1 ? 0 : y + 1) * words;

            for (int x = 0; x < words; x++)
            {
                int prev = x == 0 ? words - 1 : x - 1;
                int following = x == words - 1 ? 0 : x + 1;
                next[middle + x] = LifeRule.Next(
                    cur[above + prev], cur[above + x], cur[above + following],
                    cur[middle + prev], cur[middle + x], cur[middle + following],
                    cur[below + prev], cur[below + x], cur[below + following]);
            }
        }

        (_current, _next) = (_next, _current);
    }
}
