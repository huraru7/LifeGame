namespace LifeGame.Core.Challenge;

/// <summary>記録チャレンジの1回分の結果。SeedRle は置いた初期配置(再挑戦・再生に使う)。</summary>
public sealed class ChallengeRecord
{
    public required int Category { get; init; }

    public required int CellCount { get; init; }

    /// <summary>落ち着くまでの世代数(人口の繰り返しが始まった世代)。</summary>
    public required long Lifespan { get; init; }

    public required int PopulationPeriod { get; init; }

    public required long MaxPopulation { get; init; }

    public required long FinalPopulation { get; init; }

    public bool Died => FinalPopulation == 0;

    public required string SeedRle { get; init; }

    public required DateTimeOffset AchievedAt { get; init; }
}
