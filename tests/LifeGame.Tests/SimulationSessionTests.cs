using LifeGame.Core;
using LifeGame.Core.Simulators;

namespace LifeGame.Tests;

public class SimulationSessionTests
{
    [Fact]
    public void SwitchingSimulatorMidRun_KeepsBoardAndGeneration()
    {
        const int width = 256, height = 128;
        var session = new SimulationSession(width, height, SimulatorCatalog.All, initialSimulator: 0);
        session.Randomize(0.3, seed: 3);

        var reference = new NaiveSimulator(width, height);
        reference.Load(session.GetPackedBoard());

        // 方式を順に切り替えながら進めても、1つの方式で進めたのと同じ結果になる
        for (int round = 0; round < SimulatorCatalog.All.Count * 2; round++)
        {
            session.SwitchSimulator(round % SimulatorCatalog.All.Count);
            session.Advance(20);
            for (int i = 0; i < 20; i++)
                reference.Step();
        }

        var expected = new ulong[PackedBoard.Length(width, height)];
        reference.CopyTo(expected);
        Assert.Equal(SimulatorCatalog.All.Count * 2 * 20, session.Generation);
        Assert.True(session.GetPackedBoard().SequenceEqual(expected));
    }

    [Fact]
    public void CatalogNames_MatchSimulatorNames()
    {
        var names = SimulatorCatalog.All.Select(create => create(64, 64).Name);
        Assert.Equal(SimulatorCatalog.Names, names);
    }

    [Fact]
    public void Stamp_WrapsAroundBoardEdges()
    {
        var session = new SimulationSession(64, 64, SimulatorCatalog.All, initialSimulator: 1);
        session.Stamp(LifeGame.Core.Patterns.PatternLibrary.Find("ブロック").Pattern, 63, 63);
        var board = session.GetPackedBoard();
        Assert.True(PackedBoard.GetCell(board, 64, 63, 63));
        Assert.True(PackedBoard.GetCell(board, 64, 0, 0));
        Assert.Equal(4, session.Stats.Population);
    }

    [Fact]
    public void Tick_AdvancesByAccumulatedSpeed()
    {
        var session = new SimulationSession(64, 64, SimulatorCatalog.All, initialSimulator: 1);
        session.ChangeSpeed(-2); // 1フレームに 0.25 世代

        for (int i = 0; i < 8; i++)
            session.Tick();

        Assert.Equal(2, session.Generation);
    }
}
