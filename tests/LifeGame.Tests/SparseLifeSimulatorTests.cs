using LifeGame.Core;
using LifeGame.Core.Patterns;
using LifeGame.Core.Simulators;

namespace LifeGame.Tests;

/// <summary>範囲の外にも盤面が続く方式(Sparse tiles)が、無限の盤面と同じ結果になることを確かめる。</summary>
public class SparseLifeSimulatorTests
{
    [Fact]
    public void MatchesNaiveOnLargeTorus_WhileNothingReachesTheEdges()
    {
        // 1024² のトーラスの中央に 192² のランダムな配置。影響は1世代に1マスしか広がらないので、
        // 300世代の間はトーラスの端に届かず、無限の盤面と同じ結果になる
        const int size = 1024;
        var initial = new ulong[PackedBoard.Length(size, size)];
        PackedBoard.Randomize(initial, size, size, 0.35, seed: 5, left: 416, top: 416, regionWidth: 192, regionHeight: 192);

        var expected = new NaiveSimulator(size, size);
        var actual = new SparseLifeSimulator(size, size);
        expected.Load(initial);
        actual.Load(initial);

        var expectedBoard = new ulong[initial.Length];
        var actualBoard = new ulong[initial.Length];
        for (int generation = 1; generation <= 300; generation++)
        {
            expected.Step();
            actual.Step();
            expected.CopyTo(expectedBoard);
            actual.CopyTo(actualBoard);
            Assert.True(expectedBoard.AsSpan().SequenceEqual(actualBoard), $"{generation}世代目で一致しません。");
            Assert.Equal(expected.CountPopulation(), actual.CountPopulation());
        }
    }

    [Fact]
    public void EditsBetweenSteps_MatchNaive()
    {
        const int size = 512;
        var expected = new NaiveSimulator(size, size);
        var actual = new SparseLifeSimulator(size, size);
        var random = new Random(3);

        for (int generation = 1; generation <= 200; generation++)
        {
            if (generation % 20 == 1)
            {
                for (int i = 0; i < 300; i++)
                {
                    int x = 200 + random.Next(112);
                    int y = 200 + random.Next(112);
                    bool alive = random.Next(3) != 0;
                    expected.SetCell(x, y, alive);
                    actual.SetCell(x, y, alive);
                }
            }
            expected.Step();
            actual.Step();
            Assert.Equal(expected.CountPopulation(), actual.CountPopulation());
        }
        var expectedBoard = new ulong[PackedBoard.Length(size, size)];
        var actualBoard = new ulong[expectedBoard.Length];
        expected.CopyTo(expectedBoard);
        actual.CopyTo(actualBoard);
        Assert.True(expectedBoard.AsSpan().SequenceEqual(actualBoard));
    }

    [Fact]
    public void GliderLeavingTheRange_NeverComesBack_AndStillCounts()
    {
        // 128² の範囲から右下へ出ていくグライダー。トーラスなら 512 世代で元の位置へ戻ってくる
        var sim = new SparseLifeSimulator(128, 128);
        Stamp(sim, "グライダー", 100, 100);
        var board = new ulong[PackedBoard.Length(128, 128)];

        for (int generation = 1; generation <= 2048; generation++)
        {
            sim.Step();
            if (generation >= 200)
            {
                sim.CopyTo(board);
                Assert.All(board, word => Assert.Equal(0UL, word));
            }
        }
        Assert.Equal(5, sim.CountPopulation());
        // 飛んでいるグライダーの周りだけを持つので、タイルは少ない
        Assert.True(sim.TileCount <= 4);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void GliderBeyondTheHorizon_IsRemovedWithoutDebris(int rotation, int flip)
    {
        // 向きと地平線にぶつかる位置を変えても、残骸を残さず消える
        var glider = PatternLibrary.Find("グライダー").Pattern;
        for (int i = 0; i < rotation; i++)
            glider = glider.RotateClockwise();
        if (flip == 1)
            glider = glider.FlipHorizontal();

        var sim = new SparseLifeSimulator(128, 128, horizon: 128);
        foreach (var cell in glider.Cells)
            sim.SetCell(60 + rotation * 7 + cell.X, 60 + flip * 13 + cell.Y, true);
        for (int generation = 0; generation < 4 * 400; generation++)
            sim.Step();
        Assert.Equal(0, sim.CountPopulation());
        Assert.Equal(0, sim.TileCount);
    }

    [Fact]
    public void GunFiringForLong_DoesNotPileUpDebrisAtTheHorizon()
    {
        // 地平線が近い状態でグライダー銃を長く撃たせても、人口は銃と飛行中のグライダーの分で頭打ちになる
        var sim = new SparseLifeSimulator(128, 128, horizon: 192);
        foreach (var cell in PatternLibrary.Find("ゴスパーのグライダー銃").Pattern.Cells)
            sim.SetCell(40 + cell.X, 40 + cell.Y, true);
        var populations = new List<long>();
        for (int generation = 1; generation <= 6000; generation++)
        {
            sim.Step();
            if (generation % 30 == 0 && generation > 3000)
                populations.Add(sim.CountPopulation());
        }
        Assert.True(populations.Max() - populations.Min() <= 10, $"人口が {populations.Min()}〜{populations.Max()} で増え続けています。");
    }

    [Fact]
    public void CellsCanBePlacedOutsideTheRange()
    {
        var session = new SimulationSession(128, 128, SimulatorCatalog.All, SimulatorCatalog.DefaultIndex);
        // 範囲の左上の外にブリンカーを置く。トーラスのように反対側へは回り込まない
        session.Stamp(PatternLibrary.Find("ブリンカー").Pattern, -10, -10);
        Assert.Equal(3, session.Stats.Population);
        Assert.All(session.GetPackedBoard().ToArray(), word => Assert.Equal(0UL, word));
        session.Advance(3);
        Assert.Equal(3, session.Stats.Population);
    }

    [Theory]
    [InlineData("R-ペントミノ", 1103, 116)]
    [InlineData("ドングリ", 5206, 633)]
    public void KnownMethuselahs_OnSmallRange_HaveKnownLifespan(string name, long lifespan, long finalPopulation)
    {
        // 表示範囲は 256² しかないが、グライダーは範囲の外へ飛び去るだけで戻ってこないので、無限の盤面と同じ結果になる
        var session = new SimulationSession(256, 256, SimulatorCatalog.All, SimulatorCatalog.DefaultIndex);
        session.Stamp(PatternLibrary.Find(name).Pattern, 128, 128);
        while (!session.Stats.Detector.IsStable && session.Generation < 20_000)
            session.Advance(1);

        Assert.Equal(lifespan, session.Stats.Detector.StableSince);
        Assert.Equal(finalPopulation, session.Stats.Population);
    }

    private static void Stamp(ILifeSimulator sim, string name, int x, int y)
    {
        foreach (var cell in PatternLibrary.Find(name).Pattern.Cells)
            sim.SetCell(x + cell.X, y + cell.Y, true);
    }
}
