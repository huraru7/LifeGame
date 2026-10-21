using LifeGame.Core;
using LifeGame.Core.Simulators;

namespace LifeGame.Tests;

/// <summary>基準にする素朴な実装そのものが正しいことを、よく知られたパターンで確かめる。</summary>
public class NaiveSimulatorTests
{
    [Fact]
    public void Block_StaysStill()
    {
        var board = Board(64, 16, (5, 5), (6, 5), (5, 6), (6, 6));
        var sim = Run(board, 64, 16, 10);
        Assert.Equal(board, sim);
    }

    [Fact]
    public void Blinker_HasPeriodTwo()
    {
        var horizontal = Board(64, 16, (4, 5), (5, 5), (6, 5));
        var vertical = Board(64, 16, (5, 4), (5, 5), (5, 6));
        Assert.Equal(vertical, Run(horizontal, 64, 16, 1));
        Assert.Equal(horizontal, Run(horizontal, 64, 16, 2));
    }

    [Fact]
    public void Glider_MovesDiagonallyAndWrapsAroundTorus()
    {
        // 右下へ進むグライダー。4世代で (1, 1) 動き、トーラスなら 4×64 世代で元の位置に戻る
        var glider = Board(64, 64, (1, 0), (2, 1), (0, 2), (1, 2), (2, 2));
        var moved = Board(64, 64, (2, 1), (3, 2), (1, 3), (2, 3), (3, 3));
        Assert.Equal(moved, Run(glider, 64, 64, 4));
        Assert.Equal(glider, Run(glider, 64, 64, 256));
    }

    private static ulong[] Board(int width, int height, params (int X, int Y)[] cells)
    {
        var packed = new ulong[PackedBoard.Length(width, height)];
        foreach (var (x, y) in cells)
            PackedBoard.SetCell(packed, width, x, y, true);
        return packed;
    }

    private static ulong[] Run(ulong[] board, int width, int height, int generations)
    {
        var sim = new NaiveSimulator(width, height);
        sim.Load(board);
        for (int i = 0; i < generations; i++)
            sim.Step();
        var result = new ulong[board.Length];
        sim.CopyTo(result);
        return result;
    }
}
