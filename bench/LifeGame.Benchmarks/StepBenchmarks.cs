using BenchmarkDotNet.Attributes;
using LifeGame.Core;

namespace LifeGame.Benchmarks;

/// <summary>
/// 1世代の演算時間を BenchmarkDotNet で測る。
/// 盤面は計測中も進み続ける(ランダムな配置は時間とともに落ち着く)ので、反復ごとに初期配置へ戻している。
/// </summary>
[MemoryDiagnoser]
public class StepBenchmarks
{
    // 反復ごとに盤面を戻すため1反復1呼び出しになる。速い方式でも1反復が十分な長さになるよう、まとめて進める
    private const int StepsPerInvocation = 200;

    private ILifeSimulator _simulator = null!;
    private ulong[] _initial = [];

    [ParamsSource(nameof(Scenarios))]
    public Scenario Scenario { get; set; } = null!;

    [ParamsSource(nameof(SimulatorIndices))]
    public int SimulatorIndex { get; set; }

    public static IEnumerable<Scenario> Scenarios => Scenario.All;

    public static IEnumerable<int> SimulatorIndices => Enumerable.Range(0, SimulatorCatalog.All.Count);

    [GlobalSetup]
    public void Setup()
    {
        _simulator = SimulatorCatalog.All[SimulatorIndex](Scenario.Width, Scenario.Height);
        _initial = Scenario.CreateBoard();
    }

    [IterationSetup]
    public void ResetBoard() => _simulator.Load(_initial);

    [Benchmark(OperationsPerInvoke = StepsPerInvocation)]
    public void Step()
    {
        for (int i = 0; i < StepsPerInvocation; i++)
            _simulator.Step();
    }
}
