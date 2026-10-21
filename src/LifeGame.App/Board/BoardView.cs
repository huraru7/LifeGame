using LifeGame.App.Input;
using LifeGame.App.Rendering;
using LifeGame.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace LifeGame.App.Board;

/// <summary>
/// 盤面1枚分の表示。カメラ(拡大・移動の操作を含む)と、描画方式(標準と比較用)を持つ。
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

    /// <summary>ホイールで拡大縮小、中ボタンのドラッグと矢印キーで移動する。</summary>
    public void UpdateCamera(InputState input, float deltaSeconds)
    {
        if (!input.UiWantsMouse && input.WheelDelta != 0)
            Camera.ZoomAt(input.MousePosition, input.WheelDelta > 0 ? WheelZoomStep : 1f / WheelZoomStep);
        if (input.MiddleDragging)
            Camera.Pan(input.MouseDelta);

        var pan = Vector2.Zero;
        if (input.Held(Keys.Left)) pan.X += 1;
        if (input.Held(Keys.Right)) pan.X -= 1;
        if (input.Held(Keys.Up)) pan.Y += 1;
        if (input.Held(Keys.Down)) pan.Y -= 1;
        if (pan != Vector2.Zero)
            Camera.Pan(pan * ArrowPanSpeed * deltaSeconds);

        Camera.Update(deltaSeconds);
    }

    /// <summary>マウスの下のセル(盤面の外でも座標を返す)。</summary>
    public Point CellUnderMouse(InputState input)
    {
        Vector2 board = Camera.ScreenToBoard(input.MousePosition);
        return new Point((int)MathF.Floor(board.X), (int)MathF.Floor(board.Y));
    }

    public bool Contains(Point cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    public void FitAll(Viewport viewport) => Camera.FocusOn(new RectangleF(0, 0, Width, Height), viewport, 0.95f);

    public void Draw(SimulationSession session)
    {
        int rendererIndex = _services.View.RendererIndex;
        if (session.BoardChanged || _uploadedRenderer != rendererIndex)
        {
            _renderers[rendererIndex].Upload(session.GetPackedBoard());
            _uploadedRenderer = rendererIndex;
        }
        _renderers[rendererIndex].Draw(_services.SpriteBatch, Camera.BoardToScreen, _services.Device.Viewport);
    }

    public void Dispose()
    {
        foreach (var renderer in _renderers)
            renderer.Dispose();
    }
}
