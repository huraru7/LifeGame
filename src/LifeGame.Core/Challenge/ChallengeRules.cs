namespace LifeGame.Core.Challenge;

/// <summary>記録チャレンジの決まりごと。</summary>
public static class ChallengeRules
{
    /// <summary>部門: 置けるセル数の上限。</summary>
    public static IReadOnlyList<int> Categories { get; } = [5, 7, 10, 20];

    /// <summary>セルを置ける範囲(盤面の中央の正方形)の一辺。</summary>
    public const int AreaSize = 16;

    /// <summary>
    /// 表示する盤面の一辺。演算は範囲の外にも続く方式(Sparse tiles)なので、
    /// 飛んでいくグライダーは戻ってこない。範囲の外へ出たものも計算と人口に含まれる。
    /// </summary>
    public const int BoardSize = 4096;

    /// <summary>
    /// これ以上続いた場合は判定を打ち切る。グライダーが地平線(範囲から16,384マス先)に届いて消えるのは
    /// 約70,000世代後なので、それより前に打ち切り、消したことが結果に影響しないようにする。
    /// </summary>
    public const long MaxGenerations = 50_000;

    public const int MaxPopulationPeriod = 60;

    public const int ConfirmGenerations = 600;

    public static int AreaLeft => BoardSize / 2 - AreaSize / 2;

    public static int AreaTop => BoardSize / 2 - AreaSize / 2;
}
