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
    ];

    /// <summary><see cref="All"/> と同じ順の名前(メニューに出す。盤面を作らずに名前だけ欲しいとき用)。</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["Naive", "Bitboard", "SIMD", "SIMD + Parallel", "Tile skip + Parallel"];

    /// <summary>アプリで使う方式(SIMD + Parallel)。</summary>
    public const int DefaultIndex = 3;
}
