using LifeGame.Core;
using LifeGame.Core.Analysis;
using LifeGame.Core.Challenge;
using LifeGame.Core.Patterns;

namespace LifeGame.Tests;

/// <summary>「落ち着いた」判定と記録チャレンジの実行が、既知の値を再現することを確かめる。</summary>
public class StabilityTests
{
    [Fact]
    public void ConstantPopulation_IsStableFromFirstGeneration()
    {
        var detector = new StabilityDetector(maxPeriod: 10, confirmGenerations: 50);
        detector.Reset(0, 4);
        for (int g = 1; g <= 50; g++)
            detector.Add(g, 4);

        Assert.True(detector.IsStable);
        Assert.Equal(1, detector.PopulationPeriod);
        Assert.Equal(0, detector.StableSince);
    }

    [Fact]
    public void PeriodicPopulation_ReportsPeriodAndStart()
    {
        // 世代0〜9は不規則、世代10から 3, 5, 7 の繰り返し
        var detector = new StabilityDetector(maxPeriod: 10, confirmGenerations: 30);
        long[] noise = [9, 1, 8, 2, 7, 3, 6, 4, 5, 0];
        long[] cycle = [3, 5, 7];
        detector.Reset(0, noise[0]);
        for (int g = 1; g < 200 && !detector.IsStable; g++)
            detector.Add(g, g < 10 ? noise[g] : cycle[(g - 10) % 3]);

        Assert.True(detector.IsStable);
        Assert.Equal(3, detector.PopulationPeriod);
        Assert.Equal(10, detector.StableSince);
    }

    [Theory]
    [InlineData("R-ペントミノ", 1024, 1103, false)]
    [InlineData("ダイハード", 256, 130, true)]
    [InlineData("ドングリ", ChallengeRules.BoardSize, 5206, false)]
    public void KnownMethuselahs_HaveKnownLifespan(string name, int boardSize, long expectedLifespan, bool dies)
    {
        var session = new SimulationSession(boardSize, boardSize, SimulatorCatalog.All, initialSimulator: 3);
        var seed = PatternLibrary.Find(name).Pattern;
        session.Stamp(seed, boardSize / 2, boardSize / 2);

        while (!session.Stats.Detector.IsStable && session.Generation < 20_000)
            session.Advance(1);

        Assert.True(session.Stats.Detector.IsStable);
        Assert.Equal(expectedLifespan, session.Stats.Detector.StableSince);
        Assert.Equal(dies, session.Stats.Population == 0);
    }

    [Fact]
    public void ChallengeRun_RPentomino_ProducesRecord()
    {
        var session = new SimulationSession(ChallengeRules.BoardSize, ChallengeRules.BoardSize, SimulatorCatalog.All, initialSimulator: 3);
        var run = new ChallengeRun(session, PatternLibrary.Find("R-ペントミノ").Pattern, category: 5);

        while (run.Status == ChallengeStatus.Running)
            run.Advance(TimeSpan.FromMilliseconds(50));

        var record = run.ToRecord();
        Assert.Equal(ChallengeStatus.Stable, run.Status);
        Assert.NotNull(record);
        Assert.Equal(1103, record!.Lifespan);
        Assert.Equal(5, record.CellCount);
        Assert.False(record.Died);
        // R-ペントミノは最終的に116セル(グライダー6機を含む)で落ち着く
        Assert.Equal(116, record.FinalPopulation);
        Assert.Equal(PatternLibrary.Find("R-ペントミノ").Pattern.Cells, Rle.Parse(record.SeedRle).Cells);
    }

    [Fact]
    public void ChallengeRun_RejectsTooManyCells()
    {
        var session = new SimulationSession(256, 256, SimulatorCatalog.All, initialSimulator: 1);
        Assert.Throws<ArgumentException>(() => new ChallengeRun(session, PatternLibrary.Find("ドングリ").Pattern, category: 5));
    }
}
