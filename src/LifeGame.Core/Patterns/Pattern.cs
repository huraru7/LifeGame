namespace LifeGame.Core.Patterns;

/// <summary>セルの座標。</summary>
public readonly record struct Cell(int X, int Y);

/// <summary>
/// 生存セルの配置。座標は左上が (0, 0) になるようにそろえて持つ。
/// 回転・反転は新しいパターンを返す(元のパターンは変えない)。
/// </summary>
public sealed class Pattern
{
    public const string StandardRule = "B3/S23";

    public Pattern(string name, IEnumerable<Cell> cells, string rule = StandardRule, string comment = "")
    {
        var list = cells.Distinct().ToList();
        if (list.Count > 0)
        {
            int minX = list.Min(c => c.X);
            int minY = list.Min(c => c.Y);
            list = list
                .Select(c => new Cell(c.X - minX, c.Y - minY))
                .OrderBy(c => c.Y)
                .ThenBy(c => c.X)
                .ToList();
        }

        Name = name;
        Cells = list;
        Rule = rule;
        Comment = comment;
        Width = list.Count == 0 ? 0 : list.Max(c => c.X) + 1;
        Height = list.Count == 0 ? 0 : list[^1].Y + 1;
    }

    public string Name { get; }

    /// <summary>行優先(上の行から、行の中は左から)に並んだ生存セル。</summary>
    public IReadOnlyList<Cell> Cells { get; }

    public string Rule { get; }

    public string Comment { get; }

    public int Width { get; }

    public int Height { get; }

    public int CellCount => Cells.Count;

    /// <summary>このアプリが扱う標準のルール(B3/S23)か。RLEでは "23/3" と書かれることもある。</summary>
    public bool IsStandardRule
    {
        get
        {
            string rule = Rule.Replace(" ", "").ToUpperInvariant();
            return rule is "B3/S23" or "23/3" or "S23/B3";
        }
    }

    public Pattern RotateClockwise() =>
        new(Name, Cells.Select(c => new Cell(Height - 1 - c.Y, c.X)), Rule, Comment);

    public Pattern FlipHorizontal() =>
        new(Name, Cells.Select(c => new Cell(Width - 1 - c.X, c.Y)), Rule, Comment);

    public Pattern WithName(string name) => new(name, Cells, Rule, Comment);

    /// <summary>
    /// 盤面の指定した範囲から、生存セルを取り出してパターンにする。生存セルが無ければ null。
    /// origin には、取り出したパターンの左上が盤面のどこにあったかを返す。
    /// </summary>
    public static Pattern? FromBoard(ReadOnlySpan<ulong> packed, int boardWidth, int boardHeight, string name,
        out Cell origin, int left = 0, int top = 0, int regionWidth = int.MaxValue, int regionHeight = int.MaxValue)
    {
        int right = (int)Math.Min(boardWidth, (long)left + regionWidth);
        int bottom = (int)Math.Min(boardHeight, (long)top + regionHeight);
        int wordsPerRow = PackedBoard.WordsPerRow(boardWidth);
        var cells = new List<Cell>();

        for (int y = top; y < bottom; y++)
        {
            int firstWord = left / PackedBoard.CellsPerWord;
            int lastWord = (right - 1) / PackedBoard.CellsPerWord;
            for (int w = firstWord; w <= lastWord; w++)
            {
                ulong word = packed[y * wordsPerRow + w];
                // 空のワードを飛ばし、立っているビットだけを順に取り出す
                while (word != 0)
                {
                    int bit = System.Numerics.BitOperations.TrailingZeroCount(word);
                    word &= word - 1;
                    int x = w * PackedBoard.CellsPerWord + bit;
                    if (x >= left && x < right)
                        cells.Add(new Cell(x, y));
                }
            }
        }

        if (cells.Count == 0)
        {
            origin = default;
            return null;
        }
        origin = new Cell(cells.Min(c => c.X), cells.Min(c => c.Y));
        return new Pattern(name, cells);
    }
}
