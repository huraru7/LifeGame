using System.Diagnostics;
using ImGuiNET;
using LifeGame.App.Board;
using LifeGame.App.Input;
using LifeGame.App.Rendering;
using LifeGame.App.Ui;
using LifeGame.Core;
using LifeGame.Core.Editing;
using LifeGame.Core.Patterns;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace LifeGame.App.Screens;

/// <summary>
/// サンドボックス。セルを描く・消す、パターンをスタンプで置く(回転・反転)、取り消し・やり直し、
/// RLEの保存と読み込みをしながら、自由に走らせて眺める。
/// </summary>
internal sealed class SandboxScreen : IScreen
{
    // 表示する範囲。中央の 2048² が遊ぶ範囲で、その周り1024マスは計算の続く外側を薄く見せる
    private const int BoardSize = 4096;
    private static readonly CellRegion Field = new(1024, 1024, 2048, 2048);
    private const double RandomDensity = 0.3;
    private const string SavedCategory = "保存した配置";
    private static readonly TimeSpan MaxSpeedBudget = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(6);

    private enum Tool
    {
        Draw,
        Erase,
        Stamp,
        // 左ドラッグで表示を動かす(盤面は変えない)
        Move,
    }

    private readonly AppServices _services;
    private readonly SimulationSession _session;
    private readonly BoardView _view;
    private readonly BoardOverlay _overlay;
    private readonly ObservationPanel _observation = new();
    // 1手で盤面まるごと(4096²で2MB)を残すので、手数を抑える
    private readonly UndoHistory _history = new(capacity: 30);
    private Tool _tool = Tool.Stamp;
    private Pattern _stamp = PatternLibrary.Find("グライダー").Pattern;
    // 速度の段階を超えて、1フレームに使える時間いっぱいまで進める
    private bool _maxSpeed;
    private bool _firstEnter = true;

    private bool _stroking;
    private bool _strokeAlive;
    private Point _lastStrokeCell;
    private Point _hoverCell;
    private bool _hoverVisible;

    private string[] _savedFiles = [];
    private string _saveName = "";
    private bool _openSavePopup;
    private string? _message;
    private DateTime _messageUntil;

    public SandboxScreen(AppServices services)
    {
        _services = services;
        _session = new SimulationSession(BoardSize, BoardSize, SimulatorCatalog.All, SimulatorCatalog.DefaultIndex);
        _view = new BoardView(services, BoardSize, BoardSize, Field);
        _overlay = new BoardOverlay(services);
        RefreshSavedFiles();
    }

    public SimulationSession? Session => _session;

    public void OnEnter()
    {
        if (!_firstEnter)
            return;
        _firstEnter = false;

        // 最初はグライダー銃を動かしておき、何が起きるのかがすぐ見えるようにする
        var gun = PatternLibrary.Find("ゴスパーのグライダー銃").Pattern;
        int x = BoardSize / 2 - 60;
        int y = BoardSize / 2 - 40;
        _session.Stamp(gun, x, y);
        _view.Camera.FocusOn(new RectangleF(x - 6, y - 10, 150, 90), _services.Device.Viewport, 0.9f, UiSetup.LeftPanelWidth);
    }

    /// <summary>パターンをスタンプとして持つ(道具もスタンプに切り替える)。</summary>
    public void SetStamp(Pattern pattern)
    {
        if (pattern.Width > Field.Width || pattern.Height > Field.Height)
        {
            ShowMessage($"「{pattern.Name}」は {pattern.Width}×{pattern.Height} で、遊ぶ範囲({Field.Width}×{Field.Height})より大きいため置けません。");
            return;
        }
        _stamp = pattern;
        _tool = Tool.Stamp;
    }

    public void LoadPatternFile(string path)
    {
        try
        {
            var pattern = Rle.Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            if (pattern.CellCount == 0)
            {
                ShowMessage($"「{Path.GetFileName(path)}」には生きたセルがありません。");
                return;
            }
            SetStamp(pattern);
            ShowMessage(pattern.IsStandardRule
                ? $"「{pattern.Name}」({pattern.CellCount:N0}セル)を読み込みました。クリックで置けます。"
                : $"「{pattern.Name}」はルール {pattern.Rule} の配置ですが、このアプリでは B3/S23 で動かします。");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            ShowMessage($"「{Path.GetFileName(path)}」を読み込めませんでした: {e.Message}");
        }
    }

    public void Update(GameTime gameTime, InputState input)
    {
        float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        HandleShortcuts(input);
        _view.UpdateCamera(input, deltaSeconds, leftButtonPans: _tool == Tool.Move);
        HandleBoardMouse(input);

        if (!_session.Paused)
        {
            if (_maxSpeed)
                _session.AdvanceFor(MaxSpeedBudget);
            else
                _session.Tick();
        }

        DrawToolPanel();
        if (_services.View.ShowObservation)
            _observation.Draw("sandbox", _session, _view);
        DrawSavePopup();
    }

    public void Draw(GameTime gameTime)
    {
        _view.Draw(_session);

        _overlay.Begin(_view.Camera);
        _overlay.DrawRectangleOutline(_view.Camera, new Rectangle(Field.Left, Field.Top, Field.Width, Field.Height), BoardColors.FieldBorder * 0.8f, 2f);
        if (_hoverVisible && _tool != Tool.Move && _view.InField(_hoverCell))
        {
            if (_tool == Tool.Stamp)
            {
                _overlay.DrawPattern(_view.Camera, _stamp, StampOrigin(_hoverCell), BoardColors.Accent * 0.6f);
            }
            else if (_view.Camera.Zoom >= 6f)
            {
                var color = _tool == Tool.Draw ? BoardColors.Highlight : BoardColors.Accent;
                _overlay.DrawRectangleOutline(_view.Camera, new Rectangle(_hoverCell.X, _hoverCell.Y, 1, 1), color * 0.8f, 1.5f);
            }
        }
        _overlay.End();
    }

    public void Dispose() => _view.Dispose();

    private void HandleShortcuts(InputState input)
    {
        if (input.Pressed(Keys.Space))
            _session.Paused = !_session.Paused;
        if (input.Pressed(Keys.N))
            StepOnce();
        if (input.Pressed(Keys.OemPlus) || input.Pressed(Keys.Add))
            ChangeSpeed(+1);
        if (input.Pressed(Keys.OemMinus) || input.Pressed(Keys.Subtract))
            ChangeSpeed(-1);

        if (input.Pressed(Keys.D1))
            _tool = Tool.Draw;
        if (input.Pressed(Keys.D2))
            _tool = Tool.Erase;
        if (input.Pressed(Keys.D3))
            _tool = Tool.Stamp;
        if (input.Pressed(Keys.D4))
            _tool = Tool.Move;
        if (input.Pressed(Keys.Q))
            _stamp = _stamp.RotateClockwise().RotateClockwise().RotateClockwise();
        if (input.Pressed(Keys.E))
            _stamp = _stamp.RotateClockwise();
        if (input.Pressed(Keys.X))
            _stamp = _stamp.FlipHorizontal();

        if (input.Ctrl && input.Pressed(Keys.Z))
        {
            if (input.Shift)
                Redo();
            else
                Undo();
        }
        if (input.Ctrl && input.Pressed(Keys.Y))
            Redo();
        if (input.Pressed(Keys.Home))
            _view.ShowWholeField();
        if (!input.Ctrl && input.Pressed(Keys.R))
            Randomize();
        if (input.Pressed(Keys.Delete))
            Clear();
    }

    private void HandleBoardMouse(InputState input)
    {
        _hoverCell = _view.CellUnderMouse(input);
        _hoverVisible = !input.UiWantsMouse && !_view.IsPanning;

        if (_stroking)
        {
            if (!input.LeftDown)
            {
                _stroking = false;
                return;
            }
            // マウスを速く動かしても線が途切れないよう、前のフレームの位置から線で結ぶ
            PaintLine(_lastStrokeCell, _hoverCell, _strokeAlive);
            _lastStrokeCell = _hoverCell;
            return;
        }

        // 描く・置くは遊ぶ範囲の中から始める
        if (input.UiWantsMouse || _view.IsPanning || !input.LeftPressed || !_view.InField(_hoverCell))
            return;

        switch (_tool)
        {
            case Tool.Stamp:
                RecordUndo();
                var origin = StampOrigin(_hoverCell);
                // 範囲からはみ出した部分は置かない
                _session.Stamp(_stamp, origin.X, origin.Y, clip: Field);
                break;
            case Tool.Draw:
            case Tool.Erase:
                BeginStroke(alive: _tool == Tool.Draw);
                break;
        }
    }

    private void BeginStroke(bool alive)
    {
        RecordUndo();
        _stroking = true;
        _strokeAlive = alive;
        _lastStrokeCell = _hoverCell;
        _session.SetCell(_hoverCell.X, _hoverCell.Y, alive);
    }

    // ブレゼンハムの線分アルゴリズムで、from から to までのセルを塗る
    private void PaintLine(Point from, Point to, bool alive)
    {
        int dx = Math.Abs(to.X - from.X), sx = from.X < to.X ? 1 : -1;
        int dy = -Math.Abs(to.Y - from.Y), sy = from.Y < to.Y ? 1 : -1;
        int error = dx + dy;
        int x = from.X, y = from.Y;
        while (true)
        {
            // 範囲の外へ出た部分は描かない
            if (Field.Contains(x, y))
                _session.SetCell(x, y, alive);
            if (x == to.X && y == to.Y)
                break;
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; x += sx; }
            if (doubled <= dx) { error += dx; y += sy; }
        }
    }

    // スタンプはマウスの位置が中心になるように置く
    private Point StampOrigin(Point center) => new(center.X - _stamp.Width / 2, center.Y - _stamp.Height / 2);

    private void DrawToolPanel()
    {
        float s = UiSetup.Scale;
        ImGui.SetNextWindowPos(new NumVector2(10 * s, 40 * s), ImGuiCond.FirstUseEver);
        if (Panels.Begin("サンドボックス", ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (ImGui.Button(_session.Paused ? "再生 (Space)" : "一時停止 (Space)", new NumVector2(140 * s, 0)))
                _session.Paused = !_session.Paused;
            ImGui.SameLine();
            if (ImGui.Button("1世代進める (N)"))
                StepOnce();

            int speeds = SimulationSession.Speeds.Count;
            int level = _maxSpeed ? speeds : _session.SpeedIndex;
            string speedLabel = _maxSpeed ? "最大" : $"{SimulationSession.Speeds[level]:0.###} 世代/フレーム";
            ImGui.SetNextItemWidth(280 * s);
            if (ImGui.SliderInt("##speed", ref level, 0, speeds, speedLabel))
            {
                _maxSpeed = level == speeds;
                if (!_maxSpeed)
                    _session.SpeedIndex = level;
            }

            ImGui.Separator();
            ImGui.TextDisabled("道具");
            if (ImGui.RadioButton("描く (1)", _tool == Tool.Draw)) _tool = Tool.Draw;
            ImGui.SameLine();
            if (ImGui.RadioButton("消す (2)", _tool == Tool.Erase)) _tool = Tool.Erase;
            ImGui.SameLine();
            if (ImGui.RadioButton("スタンプ (3)", _tool == Tool.Stamp)) _tool = Tool.Stamp;
            ImGui.SameLine();
            if (ImGui.RadioButton("移動 (4)", _tool == Tool.Move)) _tool = Tool.Move;

            if (_tool == Tool.Stamp)
            {
                ImGui.TextUnformatted($"スタンプ: {_stamp.Name}");
                ImGui.TextDisabled($"{_stamp.Width}×{_stamp.Height}、{_stamp.CellCount:N0} セル");
                if (ImGui.Button("左に回す (Q)")) _stamp = _stamp.RotateClockwise().RotateClockwise().RotateClockwise();
                ImGui.SameLine();
                if (ImGui.Button("右に回す (E)")) _stamp = _stamp.RotateClockwise();
                ImGui.SameLine();
                if (ImGui.Button("反転 (X)")) _stamp = _stamp.FlipHorizontal();
            }
            else if (_tool == Tool.Move)
            {
                ImGui.TextDisabled("左ドラッグで表示を動かします");
            }
            else
            {
                ImGui.TextDisabled("右ドラッグで表示を動かせます");
            }

            ImGui.Separator();
            ImGui.TextDisabled("パターン");
            DrawPatternList(s);

            ImGui.Separator();
            ImGui.BeginDisabled(!_history.CanUndo);
            if (ImGui.Button("元に戻す (Ctrl+Z)")) Undo();
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(!_history.CanRedo);
            if (ImGui.Button("やり直す (Ctrl+Y)")) Redo();
            ImGui.EndDisabled();

            if (ImGui.Button("ランダム (R)")) Randomize();
            ImGui.SameLine();
            if (ImGui.Button("全消去 (Delete)")) Clear();
            ImGui.SameLine();
            if (ImGui.Button("範囲全体 (Home)")) _view.ShowWholeField();

            if (ImGui.Button("盤面を保存…")) _openSavePopup = true;
            ImGui.SameLine();
            if (ImGui.Button("保存先を開く")) OpenPatternsFolder();
            ImGui.TextDisabled(".rle ファイルはウィンドウにドロップしても読み込めます");

            if (_message is not null && DateTime.Now < _messageUntil)
            {
                // ファイル名などに % が入っても書式として解釈されないよう、TextUnformatted で出す
                ImGui.PushTextWrapPos(300 * s);
                ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(0.925f, 0.871f, 0.588f, 1f));
                ImGui.TextUnformatted(_message);
                ImGui.PopStyleColor();
                ImGui.PopTextWrapPos();
            }
        }
        ImGui.End();
    }

    private void DrawPatternList(float s)
    {
        if (!ImGui.BeginChild("patterns", new NumVector2(300 * s, 180 * s), ImGuiChildFlags.Borders))
        {
            ImGui.EndChild();
            return;
        }

        foreach (string category in PatternLibrary.Categories)
        {
            if (!ImGui.TreeNodeEx(category, ImGuiTreeNodeFlags.DefaultOpen))
                continue;
            foreach (var entry in PatternLibrary.All.Where(p => p.Category == category))
            {
                bool selected = _tool == Tool.Stamp && _stamp.Name == entry.Pattern.Name;
                if (ImGui.Selectable($"{entry.Pattern.Name}##library", selected))
                    SetStamp(entry.Pattern);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(entry.Description);
            }
            ImGui.TreePop();
        }

        if (ImGui.TreeNodeEx(SavedCategory, ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (_savedFiles.Length == 0)
                ImGui.TextDisabled("まだありません");
            foreach (string file in _savedFiles)
            {
                if (ImGui.Selectable($"{Path.GetFileNameWithoutExtension(file)}##saved"))
                    LoadPatternFile(file);
            }
            ImGui.TreePop();
        }
        ImGui.EndChild();
    }

    private void DrawSavePopup()
    {
        const string popupName = "盤面を保存";
        if (_openSavePopup)
        {
            ImGui.OpenPopup(popupName);
            _openSavePopup = false;
        }

        bool open = true;
        if (!ImGui.BeginPopupModal(popupName, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextUnformatted("遊ぶ範囲の中の生きているセルを、RLEファイルとして保存します。");
        ImGui.TextDisabled("保存した配置は、パターンの一覧からスタンプとして置けます。");
        ImGui.SetNextItemWidth(300 * UiSetup.Scale);
        bool submitted = ImGui.InputText("名前", ref _saveName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere(-1);

        if (ImGui.Button("保存") || submitted)
        {
            SaveBoard();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("やめる"))
            ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void SaveBoard()
    {
        string name = string.Concat(_saveName.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (name.Length == 0)
            name = $"盤面-{DateTime.Now:yyyyMMdd-HHmmss}";

        var snapshot = _session.CaptureSnapshot();
        var pattern = Pattern.FromBoard(snapshot.Cells, BoardSize, BoardSize, name, out _, Field.Left, Field.Top, Field.Width, Field.Height);
        if (pattern is null)
        {
            ShowMessage("盤面に生きたセルが無いため、保存しませんでした。");
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.PatternsDirectory);
            string path = Path.Combine(AppPaths.PatternsDirectory, name + ".rle");
            for (int i = 2; File.Exists(path); i++)
                path = Path.Combine(AppPaths.PatternsDirectory, $"{name} ({i}).rle");
            File.WriteAllText(path, Rle.Write(pattern));
            ShowMessage($"「{Path.GetFileName(path)}」に保存しました({pattern.CellCount:N0}セル)。");
            _saveName = "";
            RefreshSavedFiles();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowMessage($"保存できませんでした: {e.Message}");
        }
    }

    private void OpenPatternsFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.PatternsDirectory);
            Process.Start(new ProcessStartInfo { FileName = AppPaths.PatternsDirectory, UseShellExecute = true });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ShowMessage($"フォルダを開けませんでした: {e.Message}");
        }
    }

    private void RefreshSavedFiles()
    {
        _savedFiles = Directory.Exists(AppPaths.PatternsDirectory)
            ? Directory.GetFiles(AppPaths.PatternsDirectory, "*.rle").Order().ToArray()
            : [];
    }

    private void StepOnce()
    {
        _session.Paused = true;
        _session.Advance(1);
    }

    private void ChangeSpeed(int delta)
    {
        if (_maxSpeed)
        {
            if (delta < 0)
                _maxSpeed = false;
            return;
        }
        if (delta > 0 && _session.SpeedIndex == SimulationSession.Speeds.Count - 1)
            _maxSpeed = true;
        else
            _session.ChangeSpeed(delta);
    }

    private void RecordUndo() => _history.Record(_session.CaptureSnapshot());

    private void Undo()
    {
        if (_history.Undo(_session.CaptureSnapshot()) is { } snapshot)
            _session.RestoreSnapshot(snapshot);
    }

    private void Redo()
    {
        if (_history.Redo(_session.CaptureSnapshot()) is { } snapshot)
            _session.RestoreSnapshot(snapshot);
    }

    private void Randomize()
    {
        RecordUndo();
        _session.RandomizeRegion(RandomDensity, Environment.TickCount, Field);
    }

    private void Clear()
    {
        RecordUndo();
        _session.Clear();
    }

    private void ShowMessage(string message)
    {
        _message = message;
        _messageUntil = DateTime.Now + MessageDuration;
    }
}
