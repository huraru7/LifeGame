using LifeGame.App.Rendering;
using LifeGame.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace LifeGame.App;

/// <summary>
/// アプリ本体。Update で入力を読み盤面を進め、Draw で盤面と HUD を描く。
/// 演算は <see cref="SimulationSession"/>、表示位置は <see cref="Camera2D"/>、描画は <see cref="IBoardRenderer"/> に任せる。
/// </summary>
public sealed class LifeGameApp : Game
{
    private const double RandomDensity = 0.3;
    private const float ArrowPanSpeed = 900f; // 画面ピクセル/秒
    private const float WheelZoomStep = 1.15f;
    // 初期の演算方式(SIMD + Parallel)
    private const int DefaultSimulator = 3;

    private readonly GraphicsDeviceManager _graphics;
    private readonly SimulationSession _session;
    private readonly Camera2D _camera = new();
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Hud _hud = null!;
    private IBoardRenderer[] _renderers = [];
    private int _rendererIndex;
    // 描画方式を切り替えたとき、新しい方式のテクスチャはまだ空なので転送し直す
    private bool _rendererNeedsUpload = true;

    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private Point? _lastPaintedCell;

    public LifeGameApp(int boardWidth, int boardHeight)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 800,
            SynchronizeWithVerticalRetrace = true,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        // 再生速度は「1フレームあたりの世代数」で決めるので、フレームの間隔は固定しない
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        Window.Title = "LifeGame";

        _session = new SimulationSession(boardWidth, boardHeight, SimulatorCatalog.All, DefaultSimulator);
    }

    protected override void Initialize()
    {
        base.Initialize();
        _session.Randomize(RandomDensity, Environment.TickCount);
        _camera.Fit(_session.Width, _session.Height, GraphicsDevice.Viewport);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _hud = new Hud(Content.Load<SpriteFont>("Hud"), _pixel);
        _renderers =
        [
            new PackedBoardRenderer(GraphicsDevice, Content.Load<Effect>("BoardPacked"), _session.Width, _session.Height),
            new ColorBoardRenderer(GraphicsDevice, _session.Width, _session.Height),
        ];
    }

    protected override void UnloadContent()
    {
        foreach (var renderer in _renderers)
            renderer.Dispose();
        _pixel.Dispose();
        _spriteBatch.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (keyboard.IsKeyDown(Keys.Escape))
            Exit();

        HandleKeys(keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (IsActive)
            HandleMouse(mouse);

        _session.Tick();

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _hud.CountFrame(gameTime);
        var renderer = _renderers[_rendererIndex];

        // 盤面が変わったときだけ GPU へ転送する
        if (_session.BoardChanged || _rendererNeedsUpload)
        {
            renderer.Upload(_session.GetPackedBoard());
            _rendererNeedsUpload = false;
        }

        GraphicsDevice.Clear(new Color(8, 9, 12));
        renderer.Draw(_spriteBatch, _camera.BoardToScreen, GraphicsDevice.Viewport);
        _hud.Draw(_spriteBatch, _session, renderer);

        base.Draw(gameTime);
    }

    private void HandleKeys(KeyboardState keyboard, float deltaSeconds)
    {
        if (Pressed(keyboard, Keys.Space))
            _session.Paused = !_session.Paused;
        if (Pressed(keyboard, Keys.N) && _session.Paused)
            _session.Advance(1);
        if (Pressed(keyboard, Keys.OemPlus) || Pressed(keyboard, Keys.Add))
            _session.ChangeSpeed(+1);
        if (Pressed(keyboard, Keys.OemMinus) || Pressed(keyboard, Keys.Subtract))
            _session.ChangeSpeed(-1);
        if (Pressed(keyboard, Keys.R))
            _session.Randomize(RandomDensity, Environment.TickCount);
        if (Pressed(keyboard, Keys.C))
            _session.Clear();
        if (Pressed(keyboard, Keys.F))
            _camera.Fit(_session.Width, _session.Height, GraphicsDevice.Viewport);
        if (Pressed(keyboard, Keys.H))
            _hud.Visible = !_hud.Visible;
        if (Pressed(keyboard, Keys.Tab))
        {
            _rendererIndex = (_rendererIndex + 1) % _renderers.Length;
            _rendererNeedsUpload = true;
        }

        for (int i = 0; i < _session.SimulatorCount && i < 9; i++)
        {
            if (Pressed(keyboard, Keys.D1 + i) || Pressed(keyboard, Keys.NumPad1 + i))
                _session.SwitchSimulator(i);
        }

        var pan = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.Left)) pan.X += 1;
        if (keyboard.IsKeyDown(Keys.Right)) pan.X -= 1;
        if (keyboard.IsKeyDown(Keys.Up)) pan.Y += 1;
        if (keyboard.IsKeyDown(Keys.Down)) pan.Y -= 1;
        if (pan != Vector2.Zero)
            _camera.Pan(pan * ArrowPanSpeed * deltaSeconds);
    }

    private void HandleMouse(MouseState mouse)
    {
        var position = mouse.Position.ToVector2();
        bool insideWindow = GraphicsDevice.Viewport.Bounds.Contains(mouse.Position);

        int wheel = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheel != 0 && insideWindow)
            _camera.ZoomAt(position, wheel > 0 ? WheelZoomStep : 1f / WheelZoomStep);

        if (mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Pressed)
            _camera.Pan(position - _previousMouse.Position.ToVector2());

        bool painting = mouse.LeftButton == ButtonState.Pressed;
        bool erasing = mouse.RightButton == ButtonState.Pressed;
        if (!(painting || erasing) || !insideWindow)
        {
            _lastPaintedCell = null;
            return;
        }

        Vector2 board = _camera.ScreenToBoard(position);
        var cell = new Point((int)MathF.Floor(board.X), (int)MathF.Floor(board.Y));
        // マウスを速く動かしても線が途切れないよう、前のフレームの位置から線で結ぶ
        PaintLine(_lastPaintedCell ?? cell, cell, painting);
        _lastPaintedCell = cell;
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
            _session.SetCell(x, y, alive);
            if (x == to.X && y == to.Y)
                break;
            int doubled = 2 * error;
            if (doubled >= dy) { error += dy; x += sx; }
            if (doubled <= dx) { error += dx; y += sy; }
        }
    }

    private bool Pressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
}
