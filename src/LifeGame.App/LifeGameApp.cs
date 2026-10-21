using ImGuiNET;
using LifeGame.App.Input;
using LifeGame.App.Rendering;
using LifeGame.App.Screens;
using LifeGame.App.Ui;
using LifeGame.Core;
using LifeGame.Core.Challenge;
using LifeGame.Core.Patterns;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NumVector2 = System.Numerics.Vector2;

namespace LifeGame.App;

/// <summary>
/// アプリ本体。ウィンドウ、メニュー、画面の切り替え(<see cref="INavigator"/>)を受け持つ。
/// 1フレームの流れ: Update で UI のフレームを始め、今の画面が入力・状態・UIを処理する。
/// Draw で今の画面が盤面を描き、最後に UI を重ねる。
/// </summary>
public sealed class LifeGameApp : Game, INavigator
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly InputState _input = new();
    private ImGuiRenderer _ui = null!;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private AppServices _services = null!;
    private SandboxScreen _sandbox = null!;
    private ChallengeScreen _challenge = null!;
    private RecordsScreen _records = null!;
    private IScreen _current = null!;
    private bool _showHelp;
    private bool _applyingSize;
    private Point _windowedSize = new(1280, 800);

    public LifeGameApp()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _windowedSize.X,
            PreferredBackBufferHeight = _windowedSize.Y,
            SynchronizeWithVerticalRetrace = true,
            // 全画面は画面の解像度を切り替えない(枠のない最大化)ので、切り替えが速く、ぼやけない
            HardwareModeSwitch = false,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        // 再生速度は「1フレームあたりの世代数」で決めるので、フレームの間隔は固定しない
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        Window.Title = "LifeGame";
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    public void ShowSandbox(Pattern? stamp = null)
    {
        SwitchTo(_sandbox);
        if (stamp is not null)
            _sandbox.SetStamp(stamp);
    }

    public void ShowChallenge(ChallengeRecord? retry = null)
    {
        SwitchTo(_challenge);
        if (retry is not null)
            _challenge.Retry(retry);
    }

    public void ShowRecords(int? category = null)
    {
        SwitchTo(_records);
        if (category is int c)
            _records.SelectCategory(c);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _ui = new ImGuiRenderer(this);
        UiSetup.Configure(_ui);

        var records = new RecordStore(AppPaths.RecordsFile);
        records.Load();
        _services = new AppServices
        {
            Device = GraphicsDevice,
            SpriteBatch = _spriteBatch,
            Pixel = _pixel,
            ExpandEffect = Content.Load<Effect>("BoardExpand"),
            DisplayEffect = Content.Load<Effect>("BoardDisplay"),
            Records = records,
            View = new ViewSettings(),
            Navigator = this,
        };
        _sandbox = new SandboxScreen(_services);
        _challenge = new ChallengeScreen(_services);
        _records = new RecordsScreen(_services);
        SwitchTo(_sandbox);

        Window.FileDrop += (_, e) =>
        {
            if (e.Files.FirstOrDefault() is { } file)
            {
                ShowSandbox();
                _sandbox.LoadPatternFile(file);
            }
        };
    }

    protected override void UnloadContent()
    {
        _sandbox.Dispose();
        _challenge.Dispose();
        _records.Dispose();
        _ui.Dispose();
        _pixel.Dispose();
        _spriteBatch.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        _ui.BeginFrame(gameTime);
        var io = ImGui.GetIO();
        _input.Update(io.WantCaptureMouse, io.WantCaptureKeyboard);

        HandleGlobalKeys();
        DrawMainMenu();
        _current.Update(gameTime, _input);
        DrawHelp();
        AppCursor.Apply();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BoardColors.Outside);
        _current.Draw(gameTime);
        _ui.Render();
        base.Draw(gameTime);
    }

    private void SwitchTo(IScreen screen)
    {
        _current = screen;
        screen.OnEnter();
    }

    private void HandleGlobalKeys()
    {
        if (_input.Pressed(Keys.F1)) ShowSandbox();
        if (_input.Pressed(Keys.F2)) ShowChallenge();
        if (_input.Pressed(Keys.F3)) ShowRecords();
        if (_input.Pressed(Keys.F11)) ToggleFullScreen();
    }

    private void DrawMainMenu()
    {
        if (!ImGui.BeginMainMenuBar())
            return;

        if (ImGui.BeginMenu("モード"))
        {
            if (ImGui.MenuItem("サンドボックス", "F1", _current == _sandbox)) ShowSandbox();
            if (ImGui.MenuItem("記録チャレンジ", "F2", _current == _challenge)) ShowChallenge();
            if (ImGui.MenuItem("記録一覧", "F3", _current == _records)) ShowRecords();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("表示"))
        {
            var view = _services.View;
            if (ImGui.MenuItem("観察パネル", "", view.ShowObservation)) view.ShowObservation = !view.ShowObservation;
            if (ImGui.MenuItem("全画面", "F11", _graphics.IsFullScreen)) ToggleFullScreen();

            ImGui.Separator();
            ImGui.TextDisabled("描画方式");
            for (int i = 0; i < ViewSettings.RendererNames.Length; i++)
            {
                if (ImGui.MenuItem(ViewSettings.RendererNames[i], "", view.RendererIndex == i))
                    view.RendererIndex = i;
            }

            if (_current.Session is { } session)
            {
                ImGui.Separator();
                ImGui.TextDisabled("演算方式");
                for (int i = 0; i < SimulatorCatalog.Names.Count; i++)
                {
                    if (ImGui.MenuItem(SimulatorCatalog.Names[i], "", session.SimulatorIndex == i))
                        session.SwitchSimulator(i);
                }
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("ヘルプ"))
        {
            if (ImGui.MenuItem("操作方法")) _showHelp = true;
            ImGui.EndMenu();
        }

        if (UiSetup.FontWarning is { } warning)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(warning);
        }
        ImGui.EndMainMenuBar();
    }

    private void DrawHelp()
    {
        if (!_showHelp)
            return;
        float s = UiSetup.Scale;
        ImGui.SetNextWindowSize(new NumVector2(520 * s, 0), ImGuiCond.Appearing);
        if (ImGui.Begin("操作方法", ref _showHelp, ImGuiWindowFlags.AlwaysAutoResize))
        {
            (string Key, string Action)[] rows =
            [
                ("ホイール", "拡大・縮小(マウスの位置が中心)"),
                ("右ドラッグ / 中ボタンでドラッグ / 矢印キー", "表示位置を動かす"),
                ("左クリック・ドラッグ", "今の道具で描く・消す・スタンプを置く・動かす"),
                ("Space / N", "再生・一時停止 / 1世代進める"),
                ("+ / -", "速度を上げる・下げる"),
                ("1 / 2 / 3 / 4", "道具: 描く / 消す / スタンプ / 移動"),
                ("Q / E / X", "スタンプを左に回す / 右に回す / 反転"),
                ("Ctrl+Z / Ctrl+Y", "元に戻す / やり直す"),
                ("R / Delete / Home", "ランダム / 全消去 / いちばん引いて見る"),
                ("パネルの見出しをクリック", "パネルを畳む・開く"),
                ("F1 / F2 / F3", "サンドボックス / 記録チャレンジ / 記録一覧"),
                ("F11", "全画面"),
                (".rle をドロップ", "パターンを読み込んでスタンプにする"),
            ];
            if (ImGui.BeginTable("help", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                foreach (var (key, action) in rows)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(key);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(action);
                }
                ImGui.EndTable();
            }
        }
        ImGui.End();
    }

    private void ToggleFullScreen()
    {
        _applyingSize = true;
        if (_graphics.IsFullScreen)
        {
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = _windowedSize.X;
            _graphics.PreferredBackBufferHeight = _windowedSize.Y;
        }
        else
        {
            _windowedSize = new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = mode.Width;
            _graphics.PreferredBackBufferHeight = mode.Height;
            _graphics.IsFullScreen = true;
        }
        _graphics.ApplyChanges();
        _applyingSize = false;
    }

    // ウィンドウの大きさを変えたら、描画の解像度も合わせる(合わせないと引き伸ばされてぼやける)
    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        if (_applyingSize || _graphics.IsFullScreen)
            return;
        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        if (bounds.Width == _graphics.PreferredBackBufferWidth && bounds.Height == _graphics.PreferredBackBufferHeight)
            return;

        _applyingSize = true;
        _graphics.PreferredBackBufferWidth = bounds.Width;
        _graphics.PreferredBackBufferHeight = bounds.Height;
        _graphics.ApplyChanges();
        _applyingSize = false;
    }
}
