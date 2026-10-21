using System.Diagnostics;
using LifeGame.Core.Patterns;

namespace LifeGame.Core.Challenge;

public enum ChallengeStatus
{
    Running,
    Stable,
    LimitReached,
}

/// <summary>
/// 記録チャレンジの1回分の実行。初期配置を盤面の中央に置き、落ち着くか上限に達するまで進める。
/// 1フレームに使える時間を渡して少しずつ進めるので、画面を止めずに走らせられる。
/// </summary>
public sealed class ChallengeRun
{
    private readonly SimulationSession _session;

    /// <summary>left・top を省くと、置ける範囲の中央に置く(盤面が十分に広いので、置く位置は結果に影響しない)。</summary>
    public ChallengeRun(SimulationSession session, Pattern seed, int category, int? left = null, int? top = null)
    {
        if (seed.CellCount > category)
            throw new ArgumentException($"セル数({seed.CellCount})が部門の上限({category})を超えています。", nameof(seed));

        _session = session;
        Seed = seed;
        Category = category;
        session.Paused = true;
        session.Clear();
        session.Stamp(seed,
            left ?? ChallengeRules.AreaLeft + (ChallengeRules.AreaSize - seed.Width) / 2,
            top ?? ChallengeRules.AreaTop + (ChallengeRules.AreaSize - seed.Height) / 2);
    }

    public Pattern Seed { get; }

    public int Category { get; }

    public ChallengeStatus Status { get; private set; } = ChallengeStatus.Running;

    public long Generation => _session.Generation;

    /// <summary>budget の時間を使い切るか、終わるまで世代を進める。</summary>
    public void Advance(TimeSpan budget)
    {
        long start = Stopwatch.GetTimestamp();
        while (Status == ChallengeStatus.Running && Stopwatch.GetElapsedTime(start) < budget)
        {
            _session.Advance(1);
            if (_session.Stats.Detector.IsStable)
                Status = ChallengeStatus.Stable;
            else if (_session.Generation >= ChallengeRules.MaxGenerations)
                Status = ChallengeStatus.LimitReached;
        }
    }

    /// <summary>落ち着いて終わったときの記録。まだ走っている・上限で打ち切ったときは null。</summary>
    public ChallengeRecord? ToRecord()
    {
        if (Status != ChallengeStatus.Stable)
            return null;
        var stats = _session.Stats;
        return new ChallengeRecord
        {
            Category = Category,
            CellCount = Seed.CellCount,
            Lifespan = stats.Detector.StableSince,
            PopulationPeriod = stats.Detector.PopulationPeriod,
            MaxPopulation = stats.MaxPopulation,
            FinalPopulation = stats.Population,
            SeedRle = Rle.Write(Seed.WithName($"{Category}セル部門の記録")),
            AchievedAt = DateTimeOffset.Now,
        };
    }
}
