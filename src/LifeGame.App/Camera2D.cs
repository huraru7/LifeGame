using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App;

/// <summary>盤面の拡大率と表示位置。盤面座標は1セル = 1単位。</summary>
internal sealed class Camera2D
{
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 64f;

    /// <summary>1セルあたりの画面ピクセル数。</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>画面の左上に映る盤面座標。</summary>
    public Vector2 Position { get; private set; }

    public Matrix BoardToScreen =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f) * Matrix.CreateScale(Zoom, Zoom, 1f);

    public Vector2 ScreenToBoard(Vector2 screen) => Position + screen / Zoom;

    /// <summary>画面上の点 screen の下にある盤面の位置を保ったまま拡大・縮小する。</summary>
    public void ZoomAt(Vector2 screen, float factor)
    {
        Vector2 anchor = ScreenToBoard(screen);
        Zoom = MathHelper.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Position = anchor - screen / Zoom;
    }

    public void Pan(Vector2 screenDelta)
    {
        Position -= screenDelta / Zoom;
    }

    /// <summary>盤面全体が画面に収まるようにする。</summary>
    public void Fit(int boardWidth, int boardHeight, Viewport viewport)
    {
        Zoom = MathHelper.Clamp(Math.Min(viewport.Width / (float)boardWidth, viewport.Height / (float)boardHeight), MinZoom, MaxZoom);
        Position = new Vector2(boardWidth, boardHeight) / 2f - new Vector2(viewport.Width, viewport.Height) / 2f / Zoom;
    }
}
