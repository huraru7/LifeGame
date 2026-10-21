namespace LifeGame.Core.Patterns;

/// <summary>同梱パターンの1つ。Description は、パレットに出す短い説明。</summary>
public sealed record LibraryPattern(string Category, Pattern Pattern, string Description);

/// <summary>
/// アプリに同梱するパターン。RLEは LifeWiki などで知られている標準的なもので、
/// 動き(静物・周期・進む速さ)はテストで確かめている。
/// </summary>
public static class PatternLibrary
{
    public const string StillLifes = "静物";
    public const string Oscillators = "振動子";
    public const string Spaceships = "宇宙船";
    public const string Methuselahs = "長寿・成長";
    public const string Guns = "銃";

    public static IReadOnlyList<string> Categories { get; } = [StillLifes, Oscillators, Spaceships, Methuselahs, Guns];

    public static IReadOnlyList<LibraryPattern> All { get; } =
    [
        Entry(StillLifes, "ブロック", "2o$2o!", "最小の静物"),
        Entry(StillLifes, "ビーハイブ", "b2o$o2bo$b2o!", "蜂の巣の形の静物"),
        Entry(StillLifes, "ローフ", "b2o$o2bo$bobo$2bo!", "パンの形の静物"),
        Entry(StillLifes, "ボート", "2o$obo$bo!", "小さな舟の形の静物"),

        Entry(Oscillators, "ブリンカー", "3o!", "周期2"),
        Entry(Oscillators, "ヒキガエル", "b3o$3o!", "周期2"),
        Entry(Oscillators, "ビーコン", "2o$2o$2b2o$2b2o!", "周期2"),
        Entry(Oscillators, "パルサー", "2b3o3b3o2b2$o4bobo4bo$o4bobo4bo$o4bobo4bo$2b3o3b3o2b2$2b3o3b3o2b$o4bobo4bo$o4bobo4bo$o4bobo4bo2$2b3o3b3o!", "周期3"),
        Entry(Oscillators, "ペンタデカスロン", "2bo4bo2b$2ob4ob2o$2bo4bo!", "周期15"),

        Entry(Spaceships, "グライダー", "bo$2bo$3o!", "4世代で斜めに1マス進む"),
        Entry(Spaceships, "軽量級宇宙船", "bo2bo$o4b$o3bo$4o!", "4世代で横に2マス進む"),
        Entry(Spaceships, "中量級宇宙船", "3bo2b$bo3bo$o5b$o4bo$5o!", "4世代で横に2マス進む"),
        Entry(Spaceships, "重量級宇宙船", "3b2o2b$bo4bo$o6b$o5bo$6o!", "4世代で横に2マス進む"),

        Entry(Methuselahs, "R-ペントミノ", "b2o$2o$bo!", "5セルで1103世代続く"),
        Entry(Methuselahs, "ドングリ", "bo5b$3bo3b$2o2b3o!", "7セルで5206世代続く"),
        Entry(Methuselahs, "ダイハード", "6bob$2o6b$bo3b3o!", "130世代で消える"),
        Entry(Methuselahs, "無限成長", "6bob$4bob2o$4bobo$4bo$2bo$obo!", "10セルで永遠に増え続ける"),

        Entry(Guns, "ゴスパーのグライダー銃", "24bo11b$22bobo11b$12b2o6b2o12b2o$11bo3bo4b2o12b2o$2o8bo5bo3b2o14b$2o8bo3bob2o4bobo11b$10bo5bo7bo11b$11bo3bo20b$12b2o!", "30世代ごとにグライダーを撃つ"),
    ];

    public static LibraryPattern Find(string name) => All.First(p => p.Pattern.Name == name);

    private static LibraryPattern Entry(string category, string name, string rleBody, string description) =>
        new(category, Rle.Parse($"#N {name}\nx = 0, y = 0, rule = B3/S23\n{rleBody}"), description);
}
