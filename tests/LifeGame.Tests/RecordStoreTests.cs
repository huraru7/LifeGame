using LifeGame.Core.Challenge;

namespace LifeGame.Tests;

public sealed class RecordStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LifeGameTests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "records.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Submit_RanksByLifespan_KeepsTopTen_AndPersists()
    {
        var store = new RecordStore(FilePath);
        for (int i = 1; i <= 12; i++)
            store.Submit(Record(lifespan: i * 100, seed: $"seed{i}"));

        var ranking = store.GetRanking(7);
        Assert.Equal(RecordStore.KeepPerCategory, ranking.Count);
        Assert.Equal(1200, ranking[0].Lifespan);
        Assert.Equal(300, ranking[^1].Lifespan);

        // 上位に入らない記録は null、入る記録は順位を返す
        Assert.Null(store.Submit(Record(lifespan: 50, seed: "weak")));
        Assert.Equal(2, store.Submit(Record(lifespan: 1150, seed: "strong")));

        var reloaded = new RecordStore(FilePath);
        reloaded.Load();
        Assert.Equal(1150, reloaded.GetRanking(7)[1].Lifespan);
        Assert.Empty(reloaded.GetRanking(5));
    }

    [Fact]
    public void Submit_SameSeedTwice_DoesNotDuplicate()
    {
        var store = new RecordStore(FilePath);
        Assert.Equal(1, store.Submit(Record(lifespan: 500, seed: "same")));
        Assert.Equal(1, store.Submit(Record(lifespan: 500, seed: "same")));
        Assert.Single(store.GetRanking(7));
    }

    [Fact]
    public void Load_BrokenFile_StartsEmptyAndBacksUp()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ not json");

        var store = new RecordStore(FilePath);
        store.Load();

        Assert.NotNull(store.LoadError);
        Assert.Empty(store.GetRanking(7));
        Assert.True(File.Exists(FilePath + ".broken"));
    }

    private static ChallengeRecord Record(long lifespan, string seed) => new()
    {
        Category = 7,
        CellCount = 7,
        Lifespan = lifespan,
        PopulationPeriod = 1,
        MaxPopulation = 100,
        FinalPopulation = 50,
        SeedRle = seed,
        AchievedAt = DateTimeOffset.Now,
    };
}
