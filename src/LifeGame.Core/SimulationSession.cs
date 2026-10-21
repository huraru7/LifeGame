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
    // 1世代あたりの秒数の段階(遅い順)。時間で決めるので、画面のFPSによらず同じ速さになる。
    // 0.05秒より速い側は、1秒あたり60〜1920世代
    private static readonly double[] SpeedLevels =
        [2, 1, 0.8, 0.5, 0.4, 0.25, 0.1, 0.05, 1.0 / 60, 1.0 / 120, 1.0 / 240, 1.0 / 480, 1.0 / 960, 1.0 / 1920];

    // 処理が追いつかずに時間が溜まっても、1回にこれ以上は進めない(溜まった分は捨てる)
    private const int MaxStepsPerTick = 4096;

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

    /// <summary>速度の段階(1世代あたりの秒数、遅い順)。</summary>
    public static IReadOnlyList<double> Speeds => SpeedLevels;

    public int Width { get; }

    public int Height { get; }

    public ILifeSimulator Current { get; private set; }

    public int SimulatorIndex { get; private set; }

    public int SimulatorCount => _factories.Count;

    public long Generation { get; private set; }

    public bool Paused { get; set; }

    /// <summary>SpeedLevels の何番目の速さで再生するか。最初は 0.05秒/世代(1秒に20世代)。</summary>
    public int SpeedIndex { get; set; } = 7;

    public double SecondsPerGeneration => SpeedLevels[SpeedIndex];

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

    /// <summary>
    /// セルを書き換える。トーラスの方式では、範囲の外の座標は反対側へ回り込ませる。
    /// 範囲の外にも盤面が続く方式では、そのまま範囲の外に置く。
    /// </summary>
    public void SetCell(int x, int y, bool alive)
    {
        SetCellCore(x, y, alive);
        MarkEdited();
    }

    /// <summary>パターンの左上を (x, y) に合わせて置く。clip を渡すと、その範囲に入るセルだけを置く。</summary>
    public void Stamp(Pattern pattern, int x, int y, bool alive = true, CellRegion? clip = null)
    {
        foreach (var cell in pattern.Cells)
        {
            long cx = (long)x + cell.X;
            long cy = (long)y + cell.Y;
            if (clip is { } region && !region.Contains(cx, cy))
                continue;
            SetCellCore(cx, cy, alive);
        }
        MarkEdited();
    }

    /// <summary>region の中だけを、生存率 density のランダムな配置にする(外は空にする)。</summary>
    public void RandomizeRegion(double density, int seed, CellRegion region)
    {
        Array.Clear(_packed);
        PackedBoard.Randomize(_packed, Width, Height, density, seed, region.Left, region.Top, region.Width, region.Height);
        LoadPacked(generation: 0);
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

    /// <summary>前のフレームからの経過時間 elapsed の分だけ、再生速度に応じて世代を進める。一時停止中は何もしない。</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (Paused)
            return;

        _stepAccumulator += elapsed.TotalSeconds / SecondsPerGeneration;
        int steps = (int)Math.Min(_stepAccumulator, MaxStepsPerTick);
        _stepAccumulator = steps == MaxStepsPerTick ? 0 : _stepAccumulator - steps;
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

    private void SetCellCore(long x, long y, bool alive)
    {
        if (Current.WrapsAround)
            Current.SetCell(Wrap(x, Width), Wrap(y, Height), alive);
        else
            Current.SetCell((int)x, (int)y, alive);
    }

    private static int Wrap(long value, int size) => (int)((value % size + size) % size);

    private void RefreshStats()
    {
        if (!_statsStale)
            return;
        _statsStale = false;
        _stats.Reset(Generation, Current.CountPopulation());
    }
}
