using System.Diagnostics;
using BenchmarkDotNet.Running;
using LifeGame.Benchmarks;
using LifeGame.Core;

// 引数なし: 全方式を同じ初期配置から同じ世代数だけ進め、1世代あたりの時間を表にする(数十秒で終わる)
// --bdn: BenchmarkDotNet で厳密に測る(数分かかる)
if (args.Contains("--bdn"))
{
    BenchmarkRunner.Run<StepBenchmarks>(args: args.Where(a => a != "--bdn").ToArray());
    return;
}

const int Generations = 200;
Console.WriteLine($"Vector<ulong>.Count = {System.Numerics.Vector<ulong>.Count}, 論理プロセッサ数 = {Environment.ProcessorCount}");

foreach (var scenario in Scenario.All)
{
    Console.WriteLine();
    Console.WriteLine($"## {scenario.Name}  ({scenario.Width}x{scenario.Height}, {Generations}世代)");
    Console.WriteLine($"{"方式",-22} {"ms/世代",10} {"Naive比",9}");

    var initial = scenario.CreateBoard();
    double naiveMs = 0;
    foreach (var factory in SimulatorCatalog.All)
    {
        var sim = factory(scenario.Width, scenario.Height);
        sim.Load(initial);
        // .NETは最初に最適化の弱いコードで動き、何度も呼ばれてから最適化し直す(段階的コンパイル)。
        // その切り替えと並列化の準備を測定から外すため、最低50世代かつ0.5秒は空回しする
        long warmupStart = Stopwatch.GetTimestamp();
        for (int i = 0; i < 50 || Stopwatch.GetElapsedTime(warmupStart).TotalSeconds < 0.5; i++)
            sim.Step();
        sim.Load(initial);

        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < Generations; i++)
            sim.Step();
        double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds / Generations;
        if (naiveMs == 0)
            naiveMs = ms;
        Console.WriteLine($"{sim.Name,-22} {ms,10:F3} {naiveMs / ms,8:F1}x");
    }
}
