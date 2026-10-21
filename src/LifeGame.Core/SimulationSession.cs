using System.Diagnostics;

namespace LifeGame.Core;

/// <summary>
/// 再生中の盤面を管理する。演算方式の切り替え、再生速度、世代数、1世代の演算時間の計測を担う。
/// 描画側へは、詰めた形式の盤面(<see cref="GetPackedBoard"/>)だけを渡す。
/// </summary>
public sealed class SimulationSession
{
    // 1フレームあたりに進める世代数の段階
    private static readonly double[] SpeedLevels = [0.125, 0.25, 0.5, 1, 2, 4, 8, 16, 32, 64];

    private readonly IReadOnlyList<Func<int, int, ILifeSimulator>> _factories;
    private readonly ulong[] _packed;
    private bool _packedStale = true;
    private double _stepAccumulator;
    private int _speedIndex = 3;

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

    public int Width { get; }

    public int Height { get; }

    public ILifeSimulator Current { get; private set; }

    public int SimulatorIndex { get; private set; }

    public int SimulatorCount => _factories.Count;

    public long Generation { get; private set; }

    public bool Paused { get; set; }

    public double GenerationsPerFrame => SpeedLevels[_speedIndex];

    /// <summary>1世代の演算時間(ミリ秒)の指数移動平均。</summary>
    public double AverageStepMilliseconds { get; private set; }

    /// <summary>前回 <see cref="GetPackedBoard"/> を呼んでから盤面が変わったか。</summary>
    public bool BoardChanged { get; private set; } = true;

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
        _speedIndex = Math.Clamp(_speedIndex + delta, 0, SpeedLevels.Length - 1);
    }

    public void Randomize(double density, int seed)
    {
        Array.Clear(_packed);
        PackedBoard.Randomize(_packed, Width, Height, density, seed);
        LoadPacked();
    }

    public void Clear()
    {
        Array.Clear(_packed);
        LoadPacked();
    }

    public void SetCell(int x, int y, bool alive)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        Current.SetCell(x, y, alive);
        MarkChanged();
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

    public void Advance(int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            long start = Stopwatch.GetTimestamp();
            Current.Step();
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            AverageStepMilliseconds = AverageStepMilliseconds == 0 ? elapsed : AverageStepMilliseconds * 0.9 + elapsed * 0.1;
            Generation++;
        }
        if (steps > 0)
            MarkChanged();
    }

    /// <summary>描画用に、現在の盤面を詰めた形式で返す。必要なときだけ演算方式から書き出す。</summary>
    public ReadOnlySpan<ulong> GetPackedBoard()
    {
        RefreshPacked();
        BoardChanged = false;
        return _packed;
    }

    private void LoadPacked()
    {
        Current.Load(_packed);
        Generation = 0;
        _stepAccumulator = 0;
        _packedStale = false;
        BoardChanged = true;
    }

    private void MarkChanged()
    {
        _packedStale = true;
        BoardChanged = true;
    }

    private void RefreshPacked()
    {
        if (!_packedStale)
            return;
        Current.CopyTo(_packed);
        _packedStale = false;
    }
}
