using System.Numerics;

namespace LifeGame.Core.Simulators;

/// <summary>
/// 生きたセルのある所だけを 64×64 のタイルで持って計算する、盤面の大きさに上限のない方式。
/// Width × Height は受け渡し(表示)に使う範囲で、その外でも計算は正確に続く(端で折り返さず、壁もない)。
///
/// 前の世代で変化したタイルと、その周りのタイルだけを計算する。周りのタイルが無ければ、
/// 境目に生きたセルがあるときだけ作る。空になったタイルは捨てる。
/// 範囲から horizon マス以上離れた「地平線」の外へ出たものは消す。影響は1世代に1マスしか伝わらないので、
/// 消したことが範囲へ届くまでに最低 horizon 世代かかり、実質的に無限の盤面と同じ結果になる。
/// </summary>
public sealed class SparseLifeSimulator : ILifeSimulator
{
    public const int TileSize = 64;
    public const int DefaultHorizon = 16_384;
    private const int TileShift = 6;
    // これより計算するタイルが少なければ、スレッドに分けずに計算する
    private const int ParallelThreshold = 32;

    private static readonly (int Dx, int Dy)[] NeighborOffsets =
        [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];

    private readonly Dictionary<long, Tile> _tiles = [];
    // 次の世代で計算するきっかけになるタイル(前の世代で変化した、または編集された)。重複や捨てたタイルを含みうる
    private readonly List<Tile> _dirty = [];
    private readonly List<Tile> _candidates = [];
    private readonly Action<int> _computeTile;
    // 地平線(タイル座標)。この内側にだけタイルを作り、端の列・行に入ったタイルは捨てる
    private readonly int _minTileX;
    private readonly int _maxTileX;
    private readonly int _minTileY;
    private readonly int _maxTileY;
    private int _stamp;

    public SparseLifeSimulator(int width, int height, int horizon = DefaultHorizon)
    {
        PackedBoard.ValidateSize(width, height);
        Width = width;
        Height = height;
        _minTileX = (-horizon) >> TileShift;
        _minTileY = (-horizon) >> TileShift;
        _maxTileX = (width + horizon - 1) >> TileShift;
        _maxTileY = (height + horizon - 1) >> TileShift;
        _computeTile = ComputeTile;
    }

    public string Name => "Sparse tiles (unbounded)";

    public int Width { get; }

    public int Height { get; }

    public bool WrapsAround => false;

    /// <summary>今持っているタイルの数(計測・表示用)。</summary>
    public int TileCount => _tiles.Count;

    public void Load(ReadOnlySpan<ulong> packed)
    {
        _tiles.Clear();
        _dirty.Clear();
        int wordsPerRow = PackedBoard.WordsPerRow(Width);
        for (int y = 0; y < Height; y++)
        {
            for (int wx = 0; wx < wordsPerRow; wx++)
            {
                ulong word = packed[y * wordsPerRow + wx];
                if (word == 0)
                    continue;
                // 範囲の左端は64の倍数なので、1ワードがちょうどタイルの1行になる
                var tile = GetOrCreate(wx, y >> TileShift)!;
                tile.Current[y & (TileSize - 1)] = word;
                tile.Population += BitOperations.PopCount(word);
                MarkDirty(tile);
            }
        }
    }

    public void CopyTo(Span<ulong> packed)
    {
        int wordsPerRow = PackedBoard.WordsPerRow(Width);
        packed[..PackedBoard.Length(Width, Height)].Clear();
        foreach (var tile in _tiles.Values)
        {
            if (tile.X < 0 || tile.X >= wordsPerRow || tile.Population == 0)
                continue;
            for (int row = 0; row < TileSize; row++)
            {
                int y = (tile.Y << TileShift) + row;
                if (y >= 0 && y < Height)
                    packed[y * wordsPerRow + tile.X] = tile.Current[row];
            }
        }
    }

    public void SetCell(int x, int y, bool alive)
    {
        var tile = alive ? GetOrCreate(x >> TileShift, y >> TileShift) : Get(x >> TileShift, y >> TileShift);
        if (tile is null)
            return;
        ref ulong word = ref tile.Current[y & (TileSize - 1)];
        ulong mask = 1UL << (x & (TileSize - 1));
        bool wasAlive = (word & mask) != 0;
        if (wasAlive == alive)
            return;
        word = alive ? word | mask : word & ~mask;
        tile.Population += alive ? 1 : -1;
        MarkDirty(tile);
    }

    public long CountPopulation()
    {
        long population = 0;
        foreach (var tile in _tiles.Values)
            population += tile.Population;
        return population;
    }

    public void Step()
    {
        _stamp++;
        _candidates.Clear();

        // 1. 変化したタイルと、その周りを計算の対象にする(周りが無ければ、境目に生きたセルがあるときだけ作る)
        foreach (var tile in _dirty)
        {
            if (tile.Removed)
                continue;
            AddCandidate(tile);
            foreach (var (dx, dy) in NeighborOffsets)
            {
                var neighbor = Get(tile.X + dx, tile.Y + dy);
                if (neighbor is null && TouchesEdge(tile, dx, dy))
                    neighbor = GetOrCreate(tile.X + dx, tile.Y + dy);
                if (neighbor is not null)
                    AddCandidate(neighbor);
            }
        }

        // 2. 計算する。各タイルは今の世代を読むだけで、書くのは自分の Next だけなので、並列にできる
        if (_candidates.Count >= ParallelThreshold)
            Parallel.For(0, _candidates.Count, _computeTile);
        else
            for (int i = 0; i < _candidates.Count; i++)
                ComputeTile(i);

        // 3. 変化したタイルは書き込み先と入れ替え、次の世代のきっかけにする。
        //    空になったタイルと、地平線に届いたタイルは捨てる
        _dirty.Clear();
        foreach (var tile in _candidates)
        {
            if (tile.Changed)
            {
                (tile.Current, tile.Next) = (tile.Next, tile.Current);
                _dirty.Add(tile);
            }
            // 地平線で途中まで消されたものは、すぐ内側に動かない残骸(ブロックなど)を残すことがある。
            // 残骸が溜まると後から来たものとぶつかって増えうるので、地平線のすぐ内側で動かなくなったタイルも消す
            if (IsOnHorizon(tile, 0) || (IsOnHorizon(tile, 1) && !tile.Changed) || (tile.Population == 0 && !tile.Changed))
                Remove(tile);
        }
    }

    private void ComputeTile(int index)
    {
        var tile = _candidates[index];

        // 上下に1行ずつ足した3列(左・中・右のタイル)を用意する。行 r の上は [r]、自分は [r + 1]、下は [r + 2]
        Span<ulong> west = stackalloc ulong[TileSize + 2];
        Span<ulong> center = stackalloc ulong[TileSize + 2];
        Span<ulong> east = stackalloc ulong[TileSize + 2];
        FillColumn(west, tile.X - 1, tile.Y);
        FillColumn(center, tile.X, tile.Y);
        FillColumn(east, tile.X + 1, tile.Y);

        var next = tile.Next;
        var current = tile.Current;
        bool changed = false;
        int lanes = Vector<ulong>.Count;
        // 1タイルの行は連続して並んでいるので、Vector の幅の行数ずつまとめて計算できる
        for (int row = 0; row < TileSize; row += lanes)
        {
            var result = LifeRule.Next(
                new Vector<ulong>(west[row..]), new Vector<ulong>(center[row..]), new Vector<ulong>(east[row..]),
                new Vector<ulong>(west[(row + 1)..]), new Vector<ulong>(center[(row + 1)..]), new Vector<ulong>(east[(row + 1)..]),
                new Vector<ulong>(west[(row + 2)..]), new Vector<ulong>(center[(row + 2)..]), new Vector<ulong>(east[(row + 2)..]));
            result.CopyTo(next.AsSpan(row));
            changed |= !Vector.EqualsAll(result, new Vector<ulong>(current.AsSpan(row)));
        }

        long population = 0;
        if (changed)
        {
            foreach (ulong word in next)
                population += BitOperations.PopCount(word);
        }
        else
        {
            population = tile.Population;
        }
        tile.Changed = changed;
        tile.Population = population;
    }

    // 縦に並んだ3タイル(上・自分・下)から、上下に1行ずつ足した列を作る。無いタイルは死んだセルとして扱う
    private void FillColumn(Span<ulong> column, int tileX, int tileY)
    {
        var above = Get(tileX, tileY - 1);
        var middle = Get(tileX, tileY);
        var below = Get(tileX, tileY + 1);
        column[0] = above?.Current[TileSize - 1] ?? 0;
        if (middle is null)
            column.Slice(1, TileSize).Clear();
        else
            middle.Current.CopyTo(column[1..]);
        column[TileSize + 1] = below?.Current[0] ?? 0;
    }

    // タイルの (dx, dy) 側の境目に、生きたセルがあるか(あれば、その側のタイルにセルが生まれうる)
    private static bool TouchesEdge(Tile tile, int dx, int dy)
    {
        if (tile.Population == 0)
            return false;
        ulong mask = dx switch
        {
            -1 => 1UL,              // 左端のセルはビット0
            1 => 1UL << (TileSize - 1),
            _ => ulong.MaxValue,
        };
        if (dy == -1)
            return (tile.Current[0] & mask) != 0;
        if (dy == 1)
            return (tile.Current[TileSize - 1] & mask) != 0;
        foreach (ulong word in tile.Current)
        {
            if ((word & mask) != 0)
                return true;
        }
        return false;
    }

    // 地平線から inset タイル以内か(0 なら地平線そのもの)
    private bool IsOnHorizon(Tile tile, int inset) =>
        tile.X <= _minTileX + inset || tile.X >= _maxTileX - inset ||
        tile.Y <= _minTileY + inset || tile.Y >= _maxTileY - inset;

    private void AddCandidate(Tile tile)
    {
        if (tile.CandidateStamp == _stamp)
            return;
        tile.CandidateStamp = _stamp;
        _candidates.Add(tile);
    }

    private void MarkDirty(Tile tile)
    {
        tile.Changed = true;
        _dirty.Add(tile);
    }

    private Tile? Get(int tileX, int tileY) => _tiles.GetValueOrDefault(Key(tileX, tileY));

    // 地平線の外には作らない(null を返す)
    private Tile? GetOrCreate(int tileX, int tileY)
    {
        long key = Key(tileX, tileY);
        if (_tiles.TryGetValue(key, out var tile))
            return tile;
        if (tileX < _minTileX || tileX > _maxTileX || tileY < _minTileY || tileY > _maxTileY)
            return null;
        tile = new Tile(tileX, tileY);
        _tiles.Add(key, tile);
        return tile;
    }

    private void Remove(Tile tile)
    {
        tile.Removed = true;
        _tiles.Remove(Key(tile.X, tile.Y));
    }

    private static long Key(int tileX, int tileY) => ((long)tileY << 32) | (uint)tileX;

    private sealed class Tile(int x, int y)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public ulong[] Current = new ulong[TileSize];

        public ulong[] Next = new ulong[TileSize];

        public long Population;

        /// <summary>直前の世代で変化した、または編集された。</summary>
        public bool Changed;

        public bool Removed;

        /// <summary>今の世代の計算対象に入れた印(重複して入れないため)。</summary>
        public int CandidateStamp = -1;
    }
}
