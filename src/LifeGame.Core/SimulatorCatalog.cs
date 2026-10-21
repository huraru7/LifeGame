using LifeGame.Core.Simulators;

namespace LifeGame.Core;

/// <summary>アプリ・テスト・ベンチマークで共通に使う、演算方式の一覧。</summary>
public static class SimulatorCatalog
{
    public static IReadOnlyList<Func<int, int, ILifeSimulator>> All { get; } =
    [
        (w, h) => new NaiveSimulator(w, h),
        (w, h) => new BitboardSimulator(w, h),
        (w, h) => new SimdSimulator(w, h, parallel: false),
        (w, h) => new SimdSimulator(w, h, parallel: true),
        (w, h) => new TileSkipSimulator(w, h),
        (w, h) => new SparseLifeSimulator(w, h),
    ];

    /// <summary><see cref="All"/> と同じ順の名前(メニューに出す。盤面を作らずに名前だけ欲しいとき用)。</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["Naive", "Bitboard", "SIMD", "SIMD + Parallel", "Tile skip + Parallel", "Sparse tiles (unbounded)"];

    /// <summary>アプリで使う方式(Sparse tiles)。範囲の外でも計算が続き、端で折り返さない。</summary>
    public const int DefaultIndex = 5;

    /// <summary>端が反対側とつながる(トーラスの)方式の中で最も速いもの(SIMD + Parallel)。</summary>
    public const int FastestToroidalIndex = 3;
}
