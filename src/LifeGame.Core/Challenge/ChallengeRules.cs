namespace LifeGame.Core.Challenge;

/// <summary>記録チャレンジの決まりごと。</summary>
public static class ChallengeRules
{
    /// <summary>部門: 置けるセル数の上限。</summary>
    public static IReadOnlyList<int> Categories { get; } = [5, 7, 10, 20];

    /// <summary>セルを置ける範囲(盤面の中央の正方形)の一辺。</summary>
    public const int AreaSize = 16;

    /// <summary>
    /// 盤面の一辺。飛んでいくグライダーは4世代で斜めに1マス進むので、
    /// 4096 なら約16,000世代たつまで盤面を一周して戻ってこない。
    /// </summary>
    public const int BoardSize = 4096;

    /// <summary>これ以上続いた場合は判定を打ち切る(グライダーが一周して戻ってくる前)。</summary>
    public const long MaxGenerations = 15_000;

    public const int MaxPopulationPeriod = 60;

    public const int ConfirmGenerations = 600;

    public static int AreaLeft => BoardSize / 2 - AreaSize / 2;

    public static int AreaTop => BoardSize / 2 - AreaSize / 2;
}
