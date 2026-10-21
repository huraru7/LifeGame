using LifeGame.Core;

namespace LifeGame.Benchmarks;

/// <summary>計測に使う初期配置。全面のランダムと、ほぼ空の盤面(タイルの読み飛ばしが効く状況)の2通り。</summary>
public sealed record Scenario(string Name, int Width, int Height, bool Sparse)
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("全面ランダム", 2048, 2048, Sparse: false),
        new("中央256x256だけランダム", 2048, 2048, Sparse: true),
    ];

    public override string ToString() => $"{(Sparse ? "Sparse" : "Dense")}{Width}";

    public ulong[] CreateBoard()
    {
        var packed = new ulong[PackedBoard.Length(Width, Height)];
        if (Sparse)
            PackedBoard.Randomize(packed, Width, Height, 0.3, seed: 1, Width / 2 - 128, Height / 2 - 128, 256, 256);
        else
            PackedBoard.Randomize(packed, Width, Height, 0.3, seed: 1);
        return packed;
    }
}
