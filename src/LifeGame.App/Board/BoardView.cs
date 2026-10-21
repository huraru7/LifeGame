using LifeGame.App.Input;
using LifeGame.App.Rendering;
using LifeGame.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace LifeGame.App.Board;

/// <summary>
/// 盤面1枚分の表示。カメラ(拡大・移動の操作を含む)と、描画方式(標準と比較用)を持つ。
/// 盤面は端が反対側とつながっているので、切れ目なく繰り返して描き、どこまでも続いているように見せる。
/// サンドボックスと記録チャレンジが、それぞれ自分の盤面用に1つずつ持つ。
/// </summary>
internal sealed class BoardView : IDisposable
{
    private const float WheelZoomStep = 1.2f;
    private const float ArrowPanSpeed = 900f; // 画面ピクセル/秒

    private readonly AppServices _services;
    private readonly IBoardRenderer[] _renderers;
    // 最後に転送した描画方式。切り替えたら、新しい方式へ転送し直す
    private int _uploadedRenderer = -1;
    // ドラッグで移動中のボタン(移動中でなければ null)
    private MouseButton? _panButton;

    public BoardView(AppServices services, int width, int height)
    {
        _services = services;
        Width = width;
        Height = height;
        _renderers =
        [
            new PackedBoardRenderer(services.Device, services.ExpandEffect, services.DisplayEffect, width, height),
            new ColorBoardRenderer(services.Device, width, height),
        ];
    }

    public int Width { get; }

    public int Height { get; }

    public Camera2D Camera { get; } = new();

    public IBoardRenderer Renderer => _renderers[_services.View.RendererIndex];

    /// <summary>ドラッグで表示を動かしている最中か。</summary>
    public bool IsPanning => _panButton is not null;

    /// <summary>
    /// ホイールで拡大縮小、右・中ボタンのドラッグと矢印キーで移動する。
    /// leftButtonPans が true(道具が「移動」)なら、左ボタンのドラッグでも移動する。
    /// </summary>
    public void UpdateCamera(InputState input, float deltaSeconds, bool leftButtonPans)
    {
        var viewport = _services.Device.Viewport;
        Camera.SetMinZoom(Math.Max(viewport.Width / (float)Width, viewport.Height / (float)Height), viewport);

        if (!input.UiWantsMouse && input.WheelDelta != 0)
            Camera.ZoomAt(input.MousePosition, input.WheelDelta > 0 ? WheelZoomStep : 1f / WheelZoomStep);

        if (_panButton is { } button)
        {
            if (input.IsDown(button))
                Camera.Pan(input.MouseDelta);
            else
                _panButton = null;
        }
        else if (!input.UiWantsMouse)
        {
            if (input.WasPressed(MouseButton.Right))
                _panButton = MouseButton.Right;
            else if (input.WasPressed(MouseButton.Middle))
                _panButton = MouseButton.Middle;
            else if (leftButtonPans && input.WasPressed(MouseButton.Left))
                _panButton = MouseButton.Left;
        }

        var pan = Vector2.Zero;
        if (input.Held(Keys.Left)) pan.X += 1;
        if (input.Held(Keys.Right)) pan.X -= 1;
        if (input.Held(Keys.Up)) pan.Y += 1;
        if (input.Held(Keys.Down)) pan.Y -= 1;
        if (pan != Vector2.Zero)
            Camera.Pan(pan * ArrowPanSpeed * deltaSeconds);

        Camera.Update(deltaSeconds, Width, Height);

        if (IsPanning)
            AppCursor.Request(MouseCursor.SizeAll);
        else if (leftButtonPans && !input.UiWantsMouse)
            AppCursor.Request(MouseCursor.Hand);
    }

    /// <summary>マウスの下のセル。盤面を繰り返して描いているので、範囲外の座標も返す(<see cref="Wrap"/> で盤面内に戻せる)。</summary>
    public Point CellUnderMouse(InputState input)
    {
        Vector2 board = Camera.ScreenToBoard(input.MousePosition);
        return new Point((int)MathF.Floor(board.X), (int)MathF.Floor(board.Y));
    }

    /// <summary>繰り返しのどこにある座標でも、盤面の中の座標に戻す。</summary>
    public Point Wrap(Point cell) => new(Mod(cell.X, Width), Mod(cell.Y, Height));

    /// <summary>盤面上の位置 cell の繰り返しのうち、画面の中央に最も近いものへのずれ。</summary>
    public Point NearestCopyOffset(Point cell)
    {
        var viewport = _services.Device.Viewport;
        Vector2 center = Camera.ScreenToBoard(new Vector2(viewport.Width, viewport.Height) / 2f);
        int copiesX = (int)MathF.Round((center.X - cell.X) / Width);
        int copiesY = (int)MathF.Round((center.Y - cell.Y) / Height);
        return new Point(copiesX * Width, copiesY * Height);
    }

    public void ZoomOutFully() => Camera.ZoomOutFully(_services.Device.Viewport);

    public void Draw(SimulationSession session)
    {
        var device = _services.Device;
        int rendererIndex = _services.View.RendererIndex;
        if (session.BoardChanged || _uploadedRenderer != rendererIndex)
        {
            _renderers[rendererIndex].Upload(session.GetPackedBoard());
            _uploadedRenderer = rendererIndex;
        }

        // 転送では描画先を一時的に切り替える。戻したとき画面の中身は捨てられる(RenderTargetUsage.DiscardContents)ので、
        // 画面を消すのは転送のあとにする。順番が逆だと、転送したフレームだけ範囲外が黒くなってちらつく
        device.Clear(BoardColors.Outside);
        _renderers[rendererIndex].Draw(_services.SpriteBatch, Camera.BoardToScreen, device.Viewport, VisibleArea(device.Viewport));
    }

    public void Dispose()
    {
        foreach (var renderer in _renderers)
            renderer.Dispose();
    }

    // 画面に映る盤面座標の範囲(整数に広げる)。この範囲を、盤面を繰り返したテクスチャとして描く
    private Rectangle VisibleArea(Viewport viewport)
    {
        Vector2 topLeft = Camera.Position;
        Vector2 bottomRight = Camera.ScreenToBoard(new Vector2(viewport.Width, viewport.Height));
        int left = (int)MathF.Floor(topLeft.X) - 1;
        int top = (int)MathF.Floor(topLeft.Y) - 1;
        int right = (int)MathF.Ceiling(bottomRight.X) + 1;
        int bottom = (int)MathF.Ceiling(bottomRight.Y) + 1;
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private static int Mod(int value, int size) => ((value % size) + size) % size;
}
