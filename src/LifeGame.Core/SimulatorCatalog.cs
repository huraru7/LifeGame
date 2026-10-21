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
}
