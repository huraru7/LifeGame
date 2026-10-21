using LifeGame.Core;
using LifeGame.Core.Patterns;
using LifeGame.Core.Simulators;

namespace LifeGame.Tests;

/// <summary>同梱パターンのRLEが正しいことを、既知の動き(静物・周期・進み方)で確かめる。</summary>
public class PatternBehaviorTests
{
    private const int Size = 256;

    [Theory]
    [InlineData("ブロック")]
    [InlineData("ビーハイブ")]
    [InlineData("ローフ")]
    [InlineData("ボート")]
    public void StillLifes_DoNotChange(string name)
    {
        var (shape, origin) = RunAndCapture(name, 1);
        var original = PatternLibrary.Find(name).Pattern;
        Assert.Equal(original.Cells, shape.Cells);
        Assert.Equal(new Cell(100, 100), origin);
    }

    [Theory]
    [InlineData("ブリンカー", 2)]
    [InlineData("ヒキガエル", 2)]
    [InlineData("ビーコン", 2)]
    [InlineData("パルサー", 3)]
    [InlineData("ペンタデカスロン", 15)]
    public void Oscillators_ReturnAfterPeriod_AndNotBefore(string name, int period)
    {
        var original = Board(name);
        for (int g = 1; g < period; g++)
            Assert.False(Run(original, g).AsSpan().SequenceEqual(original), $"{name} が {g} 世代で元に戻りました。");
        Assert.True(Run(original, period).AsSpan().SequenceEqual(original));
    }

    [Theory]
    [InlineData("グライダー", 4)]
    [InlineData("軽量級宇宙船", 4)]
    [InlineData("中量級宇宙船", 4)]
    [InlineData("重量級宇宙船", 4)]
    public void Spaceships_KeepShapeAndMove(string name, int period)
    {
        var (shape, origin) = RunAndCapture(name, period);
        Assert.Equal(PatternLibrary.Find(name).Pattern.Cells, shape.Cells);
        Assert.NotEqual(new Cell(100, 100), origin);
    }

    [Fact]
    public void GosperGun_EmitsOneGliderEvery30Generations()
    {
        // 銃そのものの人口は周期30で戻るので、30世代ごとの差がグライダー1機分(5セル)になる
        var sim = new SimdSimulator(512, 512, parallel: false);
        sim.Load(Board("ゴスパーのグライダー銃", 512));
        long[] population = new long[211];
        population[0] = sim.CountPopulation();
        for (int g = 1; g <= 210; g++)
        {
            sim.Step();
            population[g] = sim.CountPopulation();
        }
        Assert.Equal(5, population[150] - population[120]);
        Assert.Equal(5, population[210] - population[180]);
    }

    [Fact]
    public void InfiniteGrowth_KeepsGrowing()
    {
        var sim = new SimdSimulator(1024, 1024, parallel: true);
        // ブロックを置きながら進む形なので、増え方はゆっくり(2000世代で約200セル)
        sim.Load(Board("無限成長", 1024));
        for (int g = 0; g < 500; g++)
            sim.Step();
        long at500 = sim.CountPopulation();
        for (int g = 0; g < 2000; g++)
            sim.Step();
        Assert.True(sim.CountPopulation() > at500 + 150);
    }

    private static ulong[] Board(string name, int size = Size)
    {
        var session = new SimulationSession(size, size, SimulatorCatalog.All, initialSimulator: 1);
        session.Stamp(PatternLibrary.Find(name).Pattern, 100, 100);
        return session.GetPackedBoard().ToArray();
    }

    private static ulong[] Run(ulong[] board, int generations)
    {
        var sim = new BitboardSimulator(Size, Size);
        sim.Load(board);
        for (int g = 0; g < generations; g++)
            sim.Step();
        var result = new ulong[board.Length];
        sim.CopyTo(result);
        return result;
    }

    private static (Pattern Shape, Cell Origin) RunAndCapture(string name, int generations)
    {
        var result = Run(Board(name), generations);
        var shape = Pattern.FromBoard(result, Size, Size, name, out var origin);
        Assert.NotNull(shape);
        return (shape!, origin);
    }
}
