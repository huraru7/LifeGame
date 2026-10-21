namespace LifeGame.Core.Editing;

/// <summary>盤面のまるごとの写しと、そのときの世代数。</summary>
public sealed record BoardSnapshot(ulong[] Cells, long Generation);

/// <summary>
/// 編集の取り消し・やり直し。編集の直前の盤面を丸ごと残す方式(2048²で1手512KB)。
/// 古いものから捨て、最大 capacity 手まで残す。
/// </summary>
public sealed class UndoHistory
{
    private readonly LinkedList<BoardSnapshot> _undo = new();
    private readonly Stack<BoardSnapshot> _redo = new();

    public UndoHistory(int capacity = 50)
    {
        Capacity = capacity;
    }

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>編集の直前に呼ぶ。新しい編集をしたら、やり直しの履歴は消える。</summary>
    public void Record(BoardSnapshot before)
    {
        _undo.AddLast(before);
        if (_undo.Count > Capacity)
            _undo.RemoveFirst();
        _redo.Clear();
    }

    public BoardSnapshot? Undo(BoardSnapshot current)
    {
        if (_undo.Last is not { } last)
            return null;
        _undo.RemoveLast();
        _redo.Push(current);
        return last.Value;
    }

    public BoardSnapshot? Redo(BoardSnapshot current)
    {
        if (!_redo.TryPop(out var next))
            return null;
        _undo.AddLast(current);
        return next;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
