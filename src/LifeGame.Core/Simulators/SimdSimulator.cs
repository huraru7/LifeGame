using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LifeGame.Core.Simulators;

/// <summary>
/// ビットボードを Vector&lt;ulong&gt; で複数ワードずつ処理する。parallel が true なら、行のまとまりごとに複数スレッドへ分ける。
/// Vector&lt;ulong&gt; の幅は実行環境に合わせて決まる(AVX2なら4ワード、ARMのNEONなら2ワード)。
/// </summary>
public sealed class SimdSimulator : ILifeSimulator
{
    // 1スレッドに任せる行数。小さすぎるとスレッドの受け渡しの費用が目立つ
    private const int RowsPerBlock = 16;

    private readonly PaddedBitGrid _grid;
    private readonly bool _parallel;
    private readonly int _blockCount;
    // 毎世代のラムダ生成を避けるため、デリゲートを使い回す
    private readonly Action<int> _stepBlock;
    private ulong[] _current;
    private ulong[] _next;

    public SimdSimulator(int width, int height, bool parallel)
    {
        _grid = new PaddedBitGrid(width, height);
        _parallel = parallel;
        _blockCount = (height + RowsPerBlock - 1) / RowsPerBlock;
        _stepBlock = StepBlock;
        _current = new ulong[_grid.BufferLength];
        _next = new ulong[_grid.BufferLength];
    }

    public string Name => _parallel ? "SIMD + Parallel" : "SIMD";

    public int Width => _grid.Width;

    public int Height => _grid.Height;

    public void Load(ReadOnlySpan<ulong> packed) => _grid.Load(packed, _current);

    public void CopyTo(Span<ulong> packed) => _grid.CopyTo(_current, packed);

    public void SetCell(int x, int y, bool alive) => _grid.SetCell(_current, x, y, alive);

    public void Step()
    {
        _grid.FillGhosts(_current);

        if (_parallel)
        {
            Parallel.For(0, _blockCount, _stepBlock);
        }
        else
        {
            for (int block = 0; block < _blockCount; block++)
                StepBlock(block);
        }

        (_current, _next) = (_next, _current);
    }

    private void StepBlock(int block)
    {
        int first = block * RowsPerBlock;
        int last = Math.Min(first + RowsPerBlock, Height);
        for (int y = first; y < last; y++)
            StepRow(y);
    }

    private void StepRow(int y)
    {
        ref ulong cur = ref MemoryMarshal.GetArrayDataReference(_current);
        ref ulong next = ref MemoryMarshal.GetArrayDataReference(_next);
        int stride = _grid.Stride;
        int words = _grid.WordsPerRow;
        int middle = _grid.IndexOf(y, 0);
        int above = middle - stride;
        int below = middle + stride;
        int lanes = Vector<ulong>.Count;

        int x = 0;
        // ゴーストのおかげで、左右隣のワードも連続した位置からまとめて読める
        for (; x + lanes <= words; x += lanes)
        {
            var result = LifeRule.Next(
                Load(ref cur, above + x - 1), Load(ref cur, above + x), Load(ref cur, above + x + 1),
                Load(ref cur, middle + x - 1), Load(ref cur, middle + x), Load(ref cur, middle + x + 1),
                Load(ref cur, below + x - 1), Load(ref cur, below + x), Load(ref cur, below + x + 1));
            Unsafe.WriteUnaligned(ref Unsafe.As<ulong, byte>(ref Unsafe.Add(ref next, middle + x)), result);
        }

        // Vector の幅で割り切れない残りはスカラーで処理する
        for (; x < words; x++)
        {
            Unsafe.Add(ref next, middle + x) = LifeRule.Next(
                Unsafe.Add(ref cur, above + x - 1), Unsafe.Add(ref cur, above + x), Unsafe.Add(ref cur, above + x + 1),
                Unsafe.Add(ref cur, middle + x - 1), Unsafe.Add(ref cur, middle + x), Unsafe.Add(ref cur, middle + x + 1),
                Unsafe.Add(ref cur, below + x - 1), Unsafe.Add(ref cur, below + x), Unsafe.Add(ref cur, below + x + 1));
        }
    }

    // 範囲は呼び出し側のループ条件とゴーストの幅で保証している
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<ulong> Load(ref ulong origin, int index) =>
        Unsafe.ReadUnaligned<Vector<ulong>>(ref Unsafe.As<ulong, byte>(ref Unsafe.Add(ref origin, index)));
}
