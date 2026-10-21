using ImGuiNET;
using LifeGame.App.Board;
using LifeGame.App.Input;
using LifeGame.App.Rendering;
using LifeGame.App.Ui;
using LifeGame.Core;
using LifeGame.Core.Challenge;
using LifeGame.Core.Patterns;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace LifeGame.App.Screens;

/// <summary>
/// 記録チャレンジ。中央の枠に部門の数までセルを置き、落ち着くまで自動で走らせて寿命を競う。
/// 流れ: 置く(Placing) → 走らせる(Running) → 結果(Finished)。
/// </summary>
internal sealed class ChallengeScreen : IScreen
{
    private static readonly TimeSpan RunBudget = TimeSpan.FromMilliseconds(10);
    private static readonly NumVector4 AccentText = new(0.925f, 0.871f, 0.588f, 1f);
    private static readonly NumVector4 WarningText = new(1f, 0.55f, 0.45f, 1f);
    private static readonly string[] CategoryLabels = ChallengeRules.Categories.Select(c => $"{c}セル以下").ToArray();
    private static readonly string[] ExampleNames = ["R-ペントミノ", "ドングリ", "ダイハード"];

    private enum Phase
    {
        Placing,
        Running,
        Finished,
    }

    private readonly AppServices _services;
    private readonly SimulationSession _session;
    private readonly BoardView _view;
    private readonly BoardOverlay _overlay;
    private readonly ObservationPanel _observation = new();
    // 置いたセル(盤面上の座標)
    private readonly HashSet<Cell> _seed = [];
    private Phase _phase = Phase.Placing;
    private int _categoryIndex = 1;
    private ChallengeRun? _run;
    private ChallengeRecord? _result;
    private int? _rank;
    private bool _firstEnter = true;
    private Point _hoverCell;
    private bool _hoverVisible;
    private string? _message;

    public ChallengeScreen(AppServices services)
    {
        _services = services;
        _session = new SimulationSession(ChallengeRules.BoardSize, ChallengeRules.BoardSize, SimulatorCatalog.All, SimulatorCatalog.DefaultIndex)
        {
            Paused = true,
        };
        _view = new BoardView(services, ChallengeRules.BoardSize, ChallengeRules.BoardSize);
        _overlay = new BoardOverlay(services);
    }

    public SimulationSession? Session => _session;

    private static Rectangle Area => new(ChallengeRules.AreaLeft, ChallengeRules.AreaTop, ChallengeRules.AreaSize, ChallengeRules.AreaSize);

    private int Limit => ChallengeRules.Categories[_categoryIndex];

    public void OnEnter()
    {
        if (!_firstEnter)
            return;
        _firstEnter = false;
        FocusArea();
    }

    /// <summary>記録の初期配置を置いた状態で、置く段階からやり直す。</summary>
    public void Retry(ChallengeRecord record)
    {
        int index = ChallengeRules.Categories.ToList().IndexOf(record.Category);
        _categoryIndex = index >= 0 ? index : _categoryIndex;
        PlaceSeed(Rle.Parse(record.SeedRle));
        BackToPlacing();
        FocusArea();
    }

    public void Update(GameTime gameTime, InputState input)
    {
        _view.UpdateCamera(input, (float)gameTime.ElapsedGameTime.TotalSeconds);
        _hoverCell = _view.CellUnderMouse(input);
        _hoverVisible = !input.UiWantsMouse;

        switch (_phase)
        {
            case Phase.Placing:
                HandlePlacing(input);
                break;
            case Phase.Running:
                _run!.Advance(RunBudget);
                if (_run.Status != ChallengeStatus.Running)
                    Finish();
                break;
        }

        DrawPanel();
        if (_services.View.ShowObservation)
            _observation.Draw("challenge", _session, _view);
    }

    public void Draw(GameTime gameTime)
    {
        _view.Draw(_session);
        _overlay.Begin(_view.Camera);
        _overlay.DrawRectangleOutline(_view.Camera, Area, BoardColors.Accent * (_phase == Phase.Placing ? 0.9f : 0.35f), 2f);
        if (_phase == Phase.Placing && _hoverVisible && Area.Contains(_hoverCell))
            _overlay.DrawRectangleOutline(_view.Camera, new Rectangle(_hoverCell.X, _hoverCell.Y, 1, 1), BoardColors.Highlight * 0.8f, 1.5f);
        _overlay.End();
    }

    public void Dispose() => _view.Dispose();

    private void HandlePlacing(InputState input)
    {
        if (input.Pressed(Keys.Enter) && CanStart)
        {
            Start();
            return;
        }
        if (input.UiWantsMouse || !Area.Contains(_hoverCell))
            return;

        var cell = new Cell(_hoverCell.X, _hoverCell.Y);
        if (input.LeftPressed)
        {
            if (_seed.Remove(cell))
            {
                _session.SetCell(cell.X, cell.Y, false);
            }
            else if (_seed.Count >= Limit)
            {
                _message = $"この部門では {Limit} セルまでしか置けません。";
            }
            else
            {
                _seed.Add(cell);
                _session.SetCell(cell.X, cell.Y, true);
                _message = null;
            }
        }
        else if (input.RightPressed && _seed.Remove(cell))
        {
            _session.SetCell(cell.X, cell.Y, false);
        }
    }

    private bool CanStart => _seed.Count > 0 && _seed.Count <= Limit;

    private void Start()
    {
        var pattern = new Pattern("初期配置", _seed);
        _run = new ChallengeRun(_session, pattern, Limit, _seed.Min(c => c.X), _seed.Min(c => c.Y));
        _phase = Phase.Running;
        _message = null;
    }

    private void Finish()
    {
        _phase = Phase.Finished;
        _result = _run!.ToRecord();
        _rank = null;
        if (_result is null)
            return;
        try
        {
            _rank = _services.Records.Submit(_result);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _message = $"記録を保存できませんでした: {e.Message}";
        }
    }

    private void BackToPlacing()
    {
        _phase = Phase.Placing;
        _run = null;
        _result = null;
        _rank = null;
        _session.Clear();
        foreach (var cell in _seed)
            _session.SetCell(cell.X, cell.Y, true);
    }

    // パターンを枠の中央に置き直す(置いてあったセルは消す)
    private void PlaceSeed(Pattern pattern)
    {
        _seed.Clear();
        int left = ChallengeRules.AreaLeft + (ChallengeRules.AreaSize - pattern.Width) / 2;
        int top = ChallengeRules.AreaTop + (ChallengeRules.AreaSize - pattern.Height) / 2;
        foreach (var cell in pattern.Cells)
            _seed.Add(new Cell(left + cell.X, top + cell.Y));
    }

    private void FocusArea() =>
        _view.Camera.FocusOn(new RectangleF(Area.X - 6, Area.Y - 6, Area.Width + 12, Area.Height + 12), _services.Device.Viewport, 0.85f, UiSetup.LeftPanelWidth);

    private void DrawPanel()
    {
        float s = UiSetup.Scale;
        ImGui.SetNextWindowPos(new NumVector2(10 * s, 40 * s), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("記録チャレンジ", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushTextWrapPos(320 * s);
            switch (_phase)
            {
                case Phase.Placing:
                    DrawPlacing(s);
                    break;
                case Phase.Running:
                    DrawRunning(s);
                    break;
                case Phase.Finished:
                    DrawFinished(s);
                    break;
            }

            if (_message is not null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, WarningText);
                ImGui.TextUnformatted(_message);
                ImGui.PopStyleColor();
            }

            ImGui.Separator();
            if (ImGui.Button("枠に寄る")) FocusArea();
            ImGui.SameLine();
            if (ImGui.Button("全体を見る")) _view.FitAll(_services.Device.Viewport);
            ImGui.PopTextWrapPos();
        }
        ImGui.End();
    }

    private void DrawPlacing(float s)
    {
        ImGui.TextUnformatted("中央の枠の中に、決められた数までセルを置いて「開始」。盤面が落ち着くまでの世代数(寿命)を競います。");
        ImGui.TextDisabled("左クリックで置く・消す、右クリックで消す");

        ImGui.SetNextItemWidth(180 * s);
        if (ImGui.Combo("部門", ref _categoryIndex, CategoryLabels, CategoryLabels.Length))
            _message = _seed.Count > Limit ? $"置いたセルが {Limit} を超えています。減らしてください。" : null;

        bool over = _seed.Count > Limit;
        ImGui.PushStyleColor(ImGuiCol.Text, over ? WarningText : AccentText);
        ImGui.TextUnformatted($"置いたセル: {_seed.Count} / {Limit}");
        ImGui.PopStyleColor();

        var best = _services.Records.GetRanking(Limit).FirstOrDefault();
        ImGui.TextDisabled(best is null ? "この部門の記録はまだありません" : $"この部門の1位: {best.Lifespan:N0} 世代");

        ImGui.BeginDisabled(!CanStart);
        if (ImGui.Button("開始 (Enter)", new NumVector2(120 * s, 0)))
            Start();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("全部消す"))
        {
            _seed.Clear();
            BackToPlacing();
        }

        ImGui.TextDisabled("有名な配置を置いてみる:");
        foreach (string name in ExampleNames)
        {
            var pattern = PatternLibrary.Find(name).Pattern;
            ImGui.BeginDisabled(pattern.CellCount > Limit);
            if (ImGui.Button($"{name}({pattern.CellCount})"))
            {
                PlaceSeed(pattern);
                BackToPlacing();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private void DrawRunning(float s)
    {
        var stats = _session.Stats;
        ImGui.TextUnformatted($"実行中…  世代 {_session.Generation:N0}");
        float fraction = (float)_session.Generation / ChallengeRules.MaxGenerations;
        ImGui.ProgressBar(fraction, new NumVector2(300 * s, 0), $"上限 {ChallengeRules.MaxGenerations:N0} 世代まで");
        ImGui.TextUnformatted($"人口 {stats.Population:N0}   最大人口 {stats.MaxPopulation:N0}");
        ImGui.TextDisabled($"落ち着いたあと、念のため {ChallengeRules.ConfirmGenerations} 世代見届けてから終わります");
        if (ImGui.Button("中止"))
            BackToPlacing();
    }

    private void DrawFinished(float s)
    {
        if (_result is { } record)
        {
            ImGui.SetWindowFontScale(1.6f);
            ImGui.PushStyleColor(ImGuiCol.Text, AccentText);
            ImGui.TextUnformatted($"寿命 {record.Lifespan:N0} 世代");
            ImGui.PopStyleColor();
            ImGui.SetWindowFontScale(1f);

            ImGui.TextUnformatted(record.Died
                ? "最後はすべてのセルが消えました。"
                : $"落ち着いた形: 人口 {record.FinalPopulation:N0}(人口の周期 {record.PopulationPeriod})");
            ImGui.TextUnformatted($"最大人口 {record.MaxPopulation:N0}   使ったセル {record.CellCount}");
            ImGui.PushStyleColor(ImGuiCol.Text, _rank is null ? ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled] : AccentText);
            ImGui.TextUnformatted(_rank is int rank
                ? $"{Limit}セル部門で {rank} 位に入りました"
                : "上位10件には入りませんでした");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.TextUnformatted($"{ChallengeRules.MaxGenerations:N0} 世代を超えても落ち着かなかったため、記録の対象外です。");
            ImGui.TextDisabled("ずっと増え続ける配置か、盤面を一周したグライダーとぶつかった可能性があります。");
        }

        if (ImGui.Button("同じ配置でもう一度"))
            BackToPlacing();
        ImGui.SameLine();
        if (ImGui.Button("新しい配置"))
        {
            _seed.Clear();
            BackToPlacing();
        }
        if (ImGui.Button("サンドボックスで開く") && _run is not null)
            _services.Navigator.ShowSandbox(_run.Seed.WithName("チャレンジの初期配置"));
        ImGui.SameLine();
        if (ImGui.Button("記録一覧"))
            _services.Navigator.ShowRecords(Limit);
    }
}
