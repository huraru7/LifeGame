namespace LifeGame.Core.Analysis;

/// <summary>
/// 人口(生存セルの数)の推移から、盤面が「落ち着いた」かを判定する。
/// 人口が周期 p(1〜MaxPeriod)で繰り返す状態が ConfirmGenerations 世代続いたら、落ち着いたとみなす。
/// 盤面そのものではなく人口を見るので、遠くへ飛んでいくグライダー(人口は一定)があっても判定できる。
/// </summary>
public sealed class StabilityDetector
{
    private readonly long[] _recent;
    // _runs[p]: 「p 世代前と人口が同じ」が今まで何世代続いているか
    private readonly int[] _runs;
    private long _count;

    public StabilityDetector(int maxPeriod = 60, int confirmGenerations = 600)
    {
        MaxPeriod = maxPeriod;
        ConfirmGenerations = confirmGenerations;
        _recent = new long[maxPeriod + 1];
        _runs = new int[maxPeriod + 1];
    }

    public int MaxPeriod { get; }

    public int ConfirmGenerations { get; }

    public bool IsStable { get; private set; }

    /// <summary>人口が繰り返す周期。盤面の周期より短いことがある(例: ブリンカーは人口が常に3なので1)。</summary>
    public int PopulationPeriod { get; private set; }

    /// <summary>人口の繰り返しが始まった最初の世代。パターンの寿命として使う。</summary>
    public long StableSince { get; private set; }

    public void Reset(long generation, long population)
    {
        Array.Clear(_runs);
        _count = 0;
        IsStable = false;
        PopulationPeriod = 0;
        StableSince = 0;
        Add(generation, population);
    }

    /// <summary>次の世代の人口を加える。世代は1つずつ連続していること。</summary>
    public void Add(long generation, long population)
    {
        if (IsStable)
            return;

        int periods = (int)Math.Min(MaxPeriod, _count);
        for (int p = 1; p <= periods; p++)
        {
            long previous = _recent[(_count - p) % _recent.Length];
            _runs[p] = previous == population ? _runs[p] + 1 : 0;
        }
        _recent[_count % _recent.Length] = population;
        _count++;

        for (int p = 1; p <= periods; p++)
        {
            if (_runs[p] >= ConfirmGenerations)
            {
                IsStable = true;
                PopulationPeriod = p;
                // 一致が始まった世代の p 世代前から、人口は周期 p で繰り返している
                StableSince = generation - _runs[p] + 1 - p;
                return;
            }
        }
    }
}
