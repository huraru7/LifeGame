using System.Text.Json;

namespace LifeGame.Core.Challenge;

/// <summary>
/// 記録チャレンジの記録を、部門ごとに上位 KeepPerCategory 件まで JSON ファイルに残す。
/// 順位は寿命の長い順。同じなら最大人口の多い順、セル数の少ない順、先に出した順。
/// </summary>
public sealed class RecordStore
{
    public const int KeepPerCategory = 10;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private List<ChallengeRecord> _records = [];

    public RecordStore(string path)
    {
        _path = path;
    }

    /// <summary>読み込みに失敗したときの理由(壊れたファイルは .broken を付けて退避する)。</summary>
    public string? LoadError { get; private set; }

    public void Load()
    {
        LoadError = null;
        if (!File.Exists(_path))
            return;
        try
        {
            _records = JsonSerializer.Deserialize<List<ChallengeRecord>>(File.ReadAllText(_path), JsonOptions) ?? [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadError = e.Message;
            _records = [];
            TryBackupBrokenFile();
        }
    }

    public IReadOnlyList<ChallengeRecord> GetRanking(int category) =>
        Sort(_records.Where(r => r.Category == category)).ToList();

    /// <summary>
    /// 記録を出す。上位に入れば保存して順位(1始まり)を返し、入らなければ null。
    /// 同じ部門に同じ初期配置の記録が既にあれば、追加せずにその順位を返す。
    /// </summary>
    public int? Submit(ChallengeRecord record)
    {
        var ranking = GetRanking(record.Category);
        int existing = ranking.ToList().FindIndex(r => r.SeedRle == record.SeedRle);
        if (existing >= 0)
            return existing + 1;

        var updated = Sort(ranking.Append(record)).Take(KeepPerCategory).ToList();
        int rank = updated.IndexOf(record);
        if (rank < 0)
            return null;

        _records = _records.Where(r => r.Category != record.Category).Concat(updated).ToList();
        Save();
        return rank + 1;
    }

    private static IEnumerable<ChallengeRecord> Sort(IEnumerable<ChallengeRecord> records) =>
        records
            .OrderByDescending(r => r.Lifespan)
            .ThenByDescending(r => r.MaxPopulation)
            .ThenBy(r => r.CellCount)
            .ThenBy(r => r.AchievedAt);

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        // 書き込みの途中で落ちても元のファイルが壊れないよう、別名で書いてから置き換える
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_records, JsonOptions));
        File.Move(temporary, _path, overwrite: true);
    }

    private void TryBackupBrokenFile()
    {
        try
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
        catch (IOException)
        {
            // 退避できなくても、次の保存で上書きされるだけなので続ける
        }
    }
}
