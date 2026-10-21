using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App.Board;

/// <summary>
/// 盤面の拡大率と表示位置。盤面座標は1セル = 1単位。
/// 拡大・縮小は目標の倍率へ滑らかに近づける(マウスの下の位置は動かさない)。
/// </summary>
internal sealed class Camera2D
{
    private const float AbsoluteMinZoom = 0.02f;
    private const float MaxZoom = 96f;
    // 大きいほど目標の倍率に早く追いつく
    private const float ZoomSharpness = 16f;

    private float _targetZoom = 1f;
    private Vector2 _zoomAnchor;

    /// <summary>1セルあたりの画面ピクセル数。</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>縮小できる限界(表示する範囲が画面に収まる程度)。</summary>
    public float MinZoom { get; private set; } = AbsoluteMinZoom;

    /// <summary>画面の左上に映る盤面座標。</summary>
    public Vector2 Position { get; private set; }

    public Matrix BoardToScreen =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f) * Matrix.CreateScale(Zoom, Zoom, 1f);

    public Vector2 ScreenToBoard(Vector2 screen) => Position + screen / Zoom;

    /// <summary>画面上の点 screen を中心に、目標の倍率を factor 倍にする。</summary>
    public void ZoomAt(Vector2 screen, float factor)
    {
        _targetZoom = MathHelper.Clamp(_targetZoom * factor, MinZoom, MaxZoom);
        _zoomAnchor = screen;
    }

    /// <summary>画面の中央を中心に、縮小できる限界まで引く。</summary>
    public void ZoomOutFully(Viewport viewport)
    {
        _targetZoom = MinZoom;
        _zoomAnchor = new Vector2(viewport.Width, viewport.Height) / 2f;
    }

    /// <summary>縮小の限界を変える。今の倍率が限界より小さければ、画面の中央を中心に合わせる。</summary>
    public void SetMinZoom(float minZoom, Viewport viewport)
    {
        MinZoom = MathHelper.Clamp(minZoom, AbsoluteMinZoom, MaxZoom);
        _targetZoom = Math.Max(_targetZoom, MinZoom);
        if (Zoom < MinZoom)
            SetZoomAround(new Vector2(viewport.Width, viewport.Height) / 2f, MinZoom);
    }

    public void Pan(Vector2 screenDelta)
    {
        Position -= screenDelta / Zoom;
    }

    public void Update(float deltaSeconds)
    {
        if (MathF.Abs(_targetZoom - Zoom) <= Zoom * 1e-4f)
        {
            SetZoomAround(_zoomAnchor, _targetZoom);
            return;
        }
        // 倍率の比で近づけるので、拡大でも縮小でも同じ速さに感じる
        float t = 1f - MathF.Exp(-deltaSeconds * ZoomSharpness);
        SetZoomAround(_zoomAnchor, Zoom * MathF.Pow(_targetZoom / Zoom, t));
    }

    /// <summary>
    /// 盤面上の範囲 area が画面の fill の割合に収まるよう、すぐに合わせる。
    /// leftInset には、画面の左端から隠れている幅(UIのパネル)を渡す。その右側の領域の中央に寄せる。
    /// </summary>
    public void FocusOn(RectangleF area, Viewport viewport, float fill = 0.9f, float leftInset = 0f)
    {
        float visibleWidth = Math.Max(viewport.Width - leftInset, viewport.Width * 0.3f);
        float zoom = Math.Min(visibleWidth * fill / area.Width, viewport.Height * fill / area.Height);
        Zoom = _targetZoom = MathHelper.Clamp(zoom, MinZoom, MaxZoom);
        var visibleCenter = new Vector2(viewport.Width - visibleWidth / 2f, viewport.Height / 2f);
        Position = area.Center - visibleCenter / Zoom;
    }

    private void SetZoomAround(Vector2 screen, float zoom)
    {
        Vector2 anchor = ScreenToBoard(screen);
        Zoom = zoom;
        Position = anchor - screen / Zoom;
    }
}

/// <summary>盤面座標の長方形(float)。</summary>
internal readonly record struct RectangleF(float X, float Y, float Width, float Height)
{
    public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);
}
