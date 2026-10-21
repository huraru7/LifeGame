using LifeGame.Core;
using LifeGame.Core.Editing;

namespace LifeGame.Tests;

public class UndoHistoryTests
{
    [Fact]
    public void UndoRedo_RestoresBoardAndGeneration()
    {
        var session = new SimulationSession(64, 64, SimulatorCatalog.All, initialSimulator: 1);
        var history = new UndoHistory();

        history.Record(session.CaptureSnapshot());
        session.SetCell(5, 5, true);
        session.Advance(3);
        var edited = session.CaptureSnapshot();

        session.RestoreSnapshot(history.Undo(session.CaptureSnapshot())!);
        Assert.Equal(0, session.Generation);
        Assert.Equal(0, session.Current.CountPopulation());

        session.RestoreSnapshot(history.Redo(session.CaptureSnapshot())!);
        Assert.Equal(edited.Generation, session.Generation);
        Assert.True(session.GetPackedBoard().SequenceEqual(edited.Cells));
    }

    [Fact]
    public void Record_DropsOldestBeyondCapacity_AndClearsRedo()
    {
        var history = new UndoHistory(capacity: 3);
        var empty = new BoardSnapshot([], 0);
        for (int i = 1; i <= 5; i++)
            history.Record(new BoardSnapshot([], i));

        Assert.Equal(5, history.Undo(empty)!.Generation);
        Assert.Equal(4, history.Undo(empty)!.Generation);
        Assert.Equal(3, history.Undo(empty)!.Generation);
        Assert.Null(history.Undo(empty));

        Assert.True(history.CanRedo);
        history.Record(new BoardSnapshot([], 9));
        Assert.False(history.CanRedo);
    }
}
