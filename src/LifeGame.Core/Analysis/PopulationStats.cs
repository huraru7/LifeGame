namespace LifeGame.Core.Analysis;

/// <summary>人口の現在値・最大値・直近の推移(グラフ用)と、落ち着いたかの判定をまとめて持つ。</summary>
public sealed class PopulationStats
{
    public const int HistoryLength = 512;

    private readonly float[] _history = new float[HistoryLength];
    private int _historyCount;
    private int _historyNext;

    public PopulationStats(int maxPeriod = 60, int confirmGenerations = 600)
    {
        Detector = new StabilityDetector(maxPeriod, confirmGenerations);
    }

    public StabilityDetector Detector { get; }

    public long Population { get; private set; }

    public long MaxPopulation { get; private set; }

    /// <summary>計測を始めた(最後に盤面が編集された)世代。</summary>
    public long StartGeneration { get; private set; }

    public void Reset(long generation, long population)
    {
        _historyCount = 0;
        _historyNext = 0;
        StartGeneration = generation;
        MaxPopulation = 0;
        Detector.Reset(generation, population);
        Push(population);
    }

    public void Record(long generation, long population)
    {
        Detector.Add(generation, population);
        Push(population);
    }

    /// <summary>直近の人口の推移を、古い順に destination へ写し、写した数を返す。</summary>
    public int CopyHistory(Span<float> destination)
    {
        int count = Math.Min(_historyCount, destination.Length);
        int start = (_historyNext - count + HistoryLength) % HistoryLength;
        for (int i = 0; i < count; i++)
            destination[i] = _history[(start + i) % HistoryLength];
        return count;
    }

    private void Push(long population)
    {
        Population = population;
        MaxPopulation = Math.Max(MaxPopulation, population);
        _history[_historyNext] = population;
        _historyNext = (_historyNext + 1) % HistoryLength;
        _historyCount = Math.Min(_historyCount + 1, HistoryLength);
    }
}
