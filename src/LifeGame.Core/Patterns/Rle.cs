using System.Text;

namespace LifeGame.Core.Patterns;

/// <summary>
/// ライフゲームのパターンの標準的な形式 RLE(Run Length Encoded)の読み書き。
/// 例: "x = 3, y = 3, rule = B3/S23" のヘッダーに続けて "bob$2bo$3o!"。
/// 数字は直後の記号の繰り返し回数で、b = 死、o = 生、$ = 改行、! = 終わり。
/// </summary>
public static class Rle
{
    private const int MaxLineLength = 70;

    public static Pattern Parse(string text, string fallbackName = "名前なし")
    {
        string name = fallbackName;
        string rule = Pattern.StandardRule;
        var comment = new StringBuilder();
        var body = new StringBuilder();
        bool headerSeen = false;

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (!headerSeen && line[0] == '#')
            {
                if (line.Length >= 2 && line[1] == 'N')
                    name = line[2..].Trim();
                else if (line.Length >= 2 && line[1] is 'C' or 'c')
                    comment.AppendLine(line[2..].Trim());
                continue;
            }

            if (!headerSeen && line[0] == 'x')
            {
                headerSeen = true;
                rule = ParseRule(line) ?? rule;
                continue;
            }

            body.Append(line);
            if (line.Contains('!'))
                break;
        }

        if (!headerSeen)
            throw new FormatException("RLEのヘッダー行(x = ..., y = ...)が見つかりません。");

        return new Pattern(name, ParseBody(body.ToString()), rule, comment.ToString().Trim());
    }

    public static string Write(Pattern pattern)
    {
        var text = new StringBuilder();
        text.Append("#N ").Append(pattern.Name).Append('\n');
        foreach (string line in pattern.Comment.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            text.Append("#C ").Append(line.Trim()).Append('\n');
        text.Append($"x = {pattern.Width}, y = {pattern.Height}, rule = {pattern.Rule}\n");

        var tokens = new List<string>();
        int y = 0;
        int i = 0;
        var cells = pattern.Cells;
        while (i < cells.Count)
        {
            int rowY = cells[i].Y;
            if (rowY > y)
            {
                tokens.Add(Token(rowY - y, '$'));
                y = rowY;
            }

            int x = 0;
            while (i < cells.Count && cells[i].Y == rowY)
            {
                if (cells[i].X > x)
                    tokens.Add(Token(cells[i].X - x, 'b'));
                // 横に続く生存セルを1つの連続にまとめる
                int start = cells[i].X;
                int length = 1;
                while (i + length < cells.Count && cells[i + length].Y == rowY && cells[i + length].X == start + length)
                    length++;
                tokens.Add(Token(length, 'o'));
                x = start + length;
                i += length;
            }
        }
        tokens.Add("!");

        // 1行が長くなりすぎないよう、記号の単位で折り返す(数字と記号の間では切らない)
        int lineLength = 0;
        foreach (string token in tokens)
        {
            if (lineLength + token.Length > MaxLineLength)
            {
                text.Append('\n');
                lineLength = 0;
            }
            text.Append(token);
            lineLength += token.Length;
        }
        text.Append('\n');
        return text.ToString();
    }

    private static List<Cell> ParseBody(string body)
    {
        var cells = new List<Cell>();
        int x = 0;
        int y = 0;
        int run = 0;

        foreach (char ch in body)
        {
            if (char.IsWhiteSpace(ch))
                continue;
            if (char.IsAsciiDigit(ch))
            {
                run = checked(run * 10 + (ch - '0'));
                continue;
            }

            int count = run == 0 ? 1 : run;
            run = 0;
            switch (ch)
            {
                case 'b':
                case '.':
                    x += count;
                    break;
                case '$':
                    y += count;
                    x = 0;
                    break;
                case '!':
                    return cells;
                default:
                    // 'o' のほか、多状態ルールの 'A' などの文字も生存として扱う
                    if (!char.IsAsciiLetter(ch))
                        throw new FormatException($"RLEに読めない文字があります: '{ch}'");
                    for (int i = 0; i < count; i++)
                        cells.Add(new Cell(x + i, y));
                    x += count;
                    break;
            }
        }
        return cells;
    }

    private static string? ParseRule(string header)
    {
        foreach (string part in header.Split(','))
        {
            int equals = part.IndexOf('=');
            if (equals > 0 && part[..equals].Trim().Equals("rule", StringComparison.OrdinalIgnoreCase))
                return part[(equals + 1)..].Trim();
        }
        return null;
    }

    private static string Token(int count, char symbol) => count == 1 ? symbol.ToString() : $"{count}{symbol}";
}
