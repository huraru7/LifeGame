using System.Diagnostics;
using LifeGame.Core.Analysis;
using LifeGame.Core.Challenge;
using LifeGame.Core.Editing;
using LifeGame.Core.Patterns;

namespace LifeGame.Core;

/// <summary>
/// 再生中の盤面を管理する。演算方式の切り替え、再生速度、世代数、1世代の演算時間、人口の統計を担う。
/// 描画側へは、詰めた形式の盤面(<see cref="GetPackedBoard"/>)だけを渡す。
/// </summary>
public sealed class SimulationSession
{
    // 1フレームあたりに進める世代数の段階
    private static readonly double[] SpeedLevels = [0.125, 0.25, 0.5, 1, 2, 4, 8, 16, 32, 64];

    private readonly IReadOnlyList<Func<int, int, ILifeSimulator>> _factories;
    private readonly ulong[] _packed;
    private readonly PopulationStats _stats = new(ChallengeRules.MaxPopulationPeriod, ChallengeRules.ConfirmGenerations);
    private bool _packedStale = true;
    // 編集のたびに人口を数え直すと重いので、次に統計が必要になったときにまとめて数え直す
    private bool _statsStale = true;
    private double _stepAccumulator;

    public SimulationSession(int width, int height, IReadOnlyList<Func<int, int, ILifeSimulator>> factories, int initialSimulator)
    {
        PackedBoard.ValidateSize(width, height);
        Width = width;
        Height = height;
        _factories = factories;
        _packed = new ulong[PackedBoard.Length(width, height)];
        SimulatorIndex = initialSimulator;
        Current = factories[initialSimulator](width, height);
    }

    public static IReadOnlyList<double> Speeds => SpeedLevels;

    public int Width { get; }

    public int Height { get; }

    public ILifeSimulator Current { get; private set; }

    public int SimulatorIndex { get; private set; }

    public int SimulatorCount => _factories.Count;

    public long Generation { get; private set; }

    public bool Paused { get; set; }

    /// <summary>SpeedLevels の何番目の速さで再生するか。</summary>
    public int SpeedIndex { get; set; } = 3;

    public double GenerationsPerFrame => SpeedLevels[SpeedIndex];

    /// <summary>1世代の演算時間(ミリ秒)の指数移動平均。</summary>
    public double AverageStepMilliseconds { get; private set; }

    /// <summary>前回 <see cref="GetPackedBoard"/> を呼んでから盤面が変わったか。</summary>
    public bool BoardChanged { get; private set; } = true;

    /// <summary>人口の統計。最後に編集されてからの推移を持つ。</summary>
    public PopulationStats Stats
    {
        get
        {
            RefreshStats();
            return _stats;
        }
    }

    public void SwitchSimulator(int index)
    {
        if (index == SimulatorIndex || index < 0 || index >= _factories.Count)
            return;

        // 今の盤面を共通の形式で取り出し、新しい方式へ渡すので、世代は途切れない
        RefreshPacked();
        var next = _factories[index](Width, Height);
        next.Load(_packed);
        Current = next;
        SimulatorIndex = index;
        AverageStepMilliseconds = 0;
    }

    public void ChangeSpeed(int delta)
    {
        SpeedIndex = Math.Clamp(SpeedIndex + delta, 0, SpeedLevels.Length - 1);
    }

    public void Randomize(double density, int seed)
    {
        Array.Clear(_packed);
        PackedBoard.Randomize(_packed, Width, Height, density, seed);
        LoadPacked(generation: 0);
    }

    public void Clear()
    {
        Array.Clear(_packed);
        LoadPacked(generation: 0);
    }

    public void SetCell(int x, int y, bool alive)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        Current.SetCell(x, y, alive);
        MarkEdited();
    }

    /// <summary>パターンの左上を (x, y) に合わせて置く。盤面の端からはみ出た分は反対側へ回り込む(トーラス)。</summary>
    public void Stamp(Pattern pattern, int x, int y, bool alive = true)
    {
        foreach (var cell in pattern.Cells)
        {
            int cx = (int)(((long)x + cell.X) % Width + Width) % Width;
            int cy = (int)(((long)y + cell.Y) % Height + Height) % Height;
            Current.SetCell(cx, cy, alive);
        }
        MarkEdited();
    }

    public BoardSnapshot CaptureSnapshot()
    {
        RefreshPacked();
        return new BoardSnapshot((ulong[])_packed.Clone(), Generation);
    }

    public void RestoreSnapshot(BoardSnapshot snapshot)
    {
        snapshot.Cells.CopyTo(_packed, 0);
        LoadPacked(snapshot.Generation);
    }

    /// <summary>再生速度に応じて、このフレームの分だけ世代を進める。一時停止中は何もしない。</summary>
    public void Tick()
    {
        if (Paused)
            return;

        _stepAccumulator += GenerationsPerFrame;
        int steps = (int)_stepAccumulator;
        _stepAccumulator -= steps;
        Advance(steps);
    }

    /// <summary>budget の時間を使い切るまで世代を進め、進めた世代数を返す。</summary>
    public int AdvanceFor(TimeSpan budget)
    {
        long start = Stopwatch.GetTimestamp();
        int steps = 0;
        while (Stopwatch.GetElapsedTime(start) < budget)
        {
            Advance(1);
            steps++;
        }
        return steps;
    }

    public void Advance(int steps)
    {
        if (steps <= 0)
            return;

        RefreshStats();
        for (int i = 0; i < steps; i++)
        {
            long start = Stopwatch.GetTimestamp();
            Current.Step();
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            AverageStepMilliseconds = AverageStepMilliseconds == 0 ? elapsed : AverageStepMilliseconds * 0.9 + elapsed * 0.1;
            Generation++;
            _stats.Record(Generation, Current.CountPopulation());
        }
        _packedStale = true;
        BoardChanged = true;
    }

    /// <summary>描画用に、現在の盤面を詰めた形式で返す。必要なときだけ演算方式から書き出す。</summary>
    public ReadOnlySpan<ulong> GetPackedBoard()
    {
        RefreshPacked();
        BoardChanged = false;
        return _packed;
    }

    private void LoadPacked(long generation)
    {
        Current.Load(_packed);
        Generation = generation;
        _stepAccumulator = 0;
        _packedStale = false;
        BoardChanged = true;
        _statsStale = true;
    }

    private void MarkEdited()
    {
        _packedStale = true;
        BoardChanged = true;
        _statsStale = true;
    }

    private void RefreshPacked()
    {
        if (!_packedStale)
            return;
        Current.CopyTo(_packed);
        _packedStale = false;
    }

    private void RefreshStats()
    {
        if (!_statsStale)
            return;
        _statsStale = false;
        _stats.Reset(Generation, Current.CountPopulation());
    }
}
