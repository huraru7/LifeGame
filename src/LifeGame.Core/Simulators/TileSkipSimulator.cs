namespace LifeGame.Core.Simulators;

/// <summary>
/// 盤面を「1ワード(64セル)× 64行」のタイルに分け、前の世代で変化したタイルとその周囲だけを計算する。
/// 計算しないタイルは、次の世代も今と同じになる(自分も周囲も変化していないため)。
/// 書き込み先のバッファには2世代前の結果が残っており、変化していないなら今と同じ値なので、写す必要もない。
/// </summary>
public sealed class TileSkipSimulator : ILifeSimulator
{
    public const int TileRows = 64;

    private readonly PaddedBitGrid _grid;
    private readonly int _tilesX;
    private readonly int _tilesY;
    private readonly bool[] _active;
    private readonly bool[] _changed;
    private readonly Action<int> _stepTileRow;
    private ulong[] _current;
    private ulong[] _next;

    public TileSkipSimulator(int width, int height)
    {
        _grid = new PaddedBitGrid(width, height);
        _tilesX = _grid.WordsPerRow;
        _tilesY = (height + TileRows - 1) / TileRows;
        _active = new bool[_tilesX * _tilesY];
        _changed = new bool[_active.Length];
        _stepTileRow = StepTileRow;
        _current = new ulong[_grid.BufferLength];
        _next = new ulong[_grid.BufferLength];
        Array.Fill(_active, true);
    }

    public string Name => "Tile skip + Parallel";

    public int Width => _grid.Width;

    public int Height => _grid.Height;

    /// <summary>直前の世代で計算したタイルの割合(0〜1)。</summary>
    public double ActiveTileRatio { get; private set; } = 1.0;

    public void Load(ReadOnlySpan<ulong> packed)
    {
        // 両方のバッファをそろえ、全タイルを計算対象に戻す
        _grid.Load(packed, _current);
        _grid.Load(packed, _next);
        Array.Fill(_active, true);
    }

    public void CopyTo(Span<ulong> packed) => _grid.CopyTo(_current, packed);

    public void SetCell(int x, int y, bool alive)
    {
        _grid.SetCell(_current, x, y, alive);
        ActivateAround(x / PackedBoard.CellsPerWord, y / TileRows);
    }

    public void Step()
    {
        _grid.FillGhosts(_current);
        Parallel.For(0, _tilesY, _stepTileRow);
        UpdateActiveTiles();
        (_current, _next) = (_next, _current);
    }

    private void StepTileRow(int tileY)
    {
        ulong[] cur = _current;
        ulong[] next = _next;
        int stride = _grid.Stride;
        int firstRow = tileY * TileRows;
        int lastRow = Math.Min(firstRow + TileRows, Height);

        for (int tileX = 0; tileX < _tilesX; tileX++)
        {
            int tile = tileY * _tilesX + tileX;
            if (!_active[tile])
            {
                _changed[tile] = false;
                continue;
            }

            bool changed = false;
            for (int y = firstRow; y < lastRow; y++)
            {
                int i = _grid.IndexOf(y, tileX);
                int above = i - stride;
                int below = i + stride;
                ulong result = LifeRule.Next(
                    cur[above - 1], cur[above], cur[above + 1],
                    cur[i - 1], cur[i], cur[i + 1],
                    cur[below - 1], cur[below], cur[below + 1]);
                changed |= result != cur[i];
                next[i] = result;
            }
            _changed[tile] = changed;
        }
    }

    // 変化したタイルの周囲8タイル(トーラスで折り返す)を、次の世代の計算対象にする
    private void UpdateActiveTiles()
    {
        Array.Clear(_active);
        int activeCount = 0;
        for (int tileY = 0; tileY < _tilesY; tileY++)
        {
            for (int tileX = 0; tileX < _tilesX; tileX++)
            {
                if (_changed[tileY * _tilesX + tileX])
                    ActivateAround(tileX, tileY);
            }
        }
        foreach (bool active in _active)
        {
            if (active)
                activeCount++;
        }
        ActiveTileRatio = (double)activeCount / _active.Length;
    }

    private void ActivateAround(int tileX, int tileY)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            int ty = (tileY + dy + _tilesY) % _tilesY;
            for (int dx = -1; dx <= 1; dx++)
            {
                int tx = (tileX + dx + _tilesX) % _tilesX;
                _active[ty * _tilesX + tx] = true;
            }
        }
    }
}
