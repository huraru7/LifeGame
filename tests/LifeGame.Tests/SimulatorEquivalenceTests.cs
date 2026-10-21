using LifeGame.Core;
using LifeGame.Core.Simulators;

namespace LifeGame.Tests;

/// <summary>すべての演算方式が、素朴な実装と毎世代まったく同じ盤面になることを確かめる。</summary>
public class SimulatorEquivalenceTests
{
    public static TheoryData<int, int, int> Cases()
    {
        var data = new TheoryData<int, int, int>();
        // トーラスの方式どうしを比べる(範囲の外に盤面が続く方式は SparseLifeSimulatorTests で確かめる)
        for (int index = 1; index < SimulatorCatalog.All.Count; index++)
        {
            if (!SimulatorCatalog.All[index](64, 64).WrapsAround)
                continue;
            data.Add(index, 64, 64);
            // 1行のワード数がVectorの幅で割り切れない(端数をスカラーで処理する経路)
            data.Add(index, 320, 70);
            // 高さがタイルの行数で割り切れない、横長の盤面
            data.Add(index, 1024, 130);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RandomSoup_MatchesNaiveEveryGeneration(int simulatorIndex, int width, int height)
    {
        var initial = new ulong[PackedBoard.Length(width, height)];
        PackedBoard.Randomize(initial, width, height, 0.35, seed: width * 31 + height);

        var expected = new NaiveSimulator(width, height);
        var actual = SimulatorCatalog.All[simulatorIndex](width, height);
        expected.Load(initial);
        actual.Load(initial);

        // ランダムな配置は数百世代で静物と振動子が増え、タイルの読み飛ばしが起きる状態になる
        for (int generation = 1; generation <= 400; generation++)
        {
            expected.Step();
            actual.Step();
            AssertSameBoard(expected, actual, generation);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EditsBetweenSteps_MatchNaive(int simulatorIndex, int width, int height)
    {
        var initial = new ulong[PackedBoard.Length(width, height)];
        PackedBoard.Randomize(initial, width, height, 0.35, seed: 7);

        var expected = new NaiveSimulator(width, height);
        var actual = SimulatorCatalog.All[simulatorIndex](width, height);
        expected.Load(initial);
        actual.Load(initial);
        var random = new Random(11);

        for (int generation = 1; generation <= 300; generation++)
        {
            // 落ち着いた領域(読み飛ばされているタイル)にも描き込みが入るようにする
            if (generation % 25 == 0)
            {
                for (int i = 0; i < 40; i++)
                {
                    int x = random.Next(width);
                    int y = random.Next(height);
                    bool alive = random.Next(2) == 0;
                    expected.SetCell(x, y, alive);
                    actual.SetCell(x, y, alive);
                }
            }
            expected.Step();
            actual.Step();
            AssertSameBoard(expected, actual, generation);
        }
    }

    [Fact]
    public void TileSkip_SkipsQuietTilesAndStillMatchesNaive()
    {
        // ほぼ空の盤面に、タイルの境目をまたいで進むグライダーと、静物・振動子を置く
        // 各パターンの周囲3×3タイルは計算対象になるので、全体のタイル数(32×8)を十分に多くする
        const int width = 2048, height = 512;
        var initial = new ulong[PackedBoard.Length(width, height)];
        foreach (var (x, y) in new[] { (61, 60), (62, 61), (60, 62), (61, 62), (62, 62), (500, 100), (501, 100), (500, 101), (501, 101), (800, 200), (801, 200), (802, 200) })
            PackedBoard.SetCell(initial, width, x, y, true);

        var expected = new NaiveSimulator(width, height);
        var actual = new TileSkipSimulator(width, height);
        expected.Load(initial);
        actual.Load(initial);

        for (int generation = 1; generation <= 600; generation++)
        {
            expected.Step();
            actual.Step();
            AssertSameBoard(expected, actual, generation);
        }
        Assert.True(actual.ActiveTileRatio < 0.15, $"計算したタイルの割合が多すぎます: {actual.ActiveTileRatio:P0}");
    }

    private static void AssertSameBoard(ILifeSimulator expected, ILifeSimulator actual, int generation)
    {
        var expectedBoard = new ulong[PackedBoard.Length(expected.Width, expected.Height)];
        var actualBoard = new ulong[expectedBoard.Length];
        expected.CopyTo(expectedBoard);
        actual.CopyTo(actualBoard);
        Assert.True(expectedBoard.AsSpan().SequenceEqual(actualBoard),
            $"{actual.Name}: {generation}世代目で素朴な実装と一致しません。");
        Assert.Equal(expected.CountPopulation(), actual.CountPopulation());
    }
}
