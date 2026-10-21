using LifeGame.Core.Patterns;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App.Board;

/// <summary>盤面の上に重ねて描くもの(スタンプの予告、枠、マウスの下のセル)。座標は盤面座標で渡す。</summary>
internal sealed class BoardOverlay
{
    // これより多いセルのパターンは、予告を外枠だけにする(描画が重くなるため)
    private const int MaxPreviewCells = 20_000;

    private readonly AppServices _services;

    public BoardOverlay(AppServices services)
    {
        _services = services;
    }

    public void Begin(Camera2D camera) =>
        _services.SpriteBatch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp, transformMatrix: camera.BoardToScreen);

    public void End() => _services.SpriteBatch.End();

    /// <summary>パターンの左上を origin に置いたときの予告。セルの内側を少し小さく描く。</summary>
    public void DrawPattern(Camera2D camera, Pattern pattern, Point origin, Color color)
    {
        if (pattern.CellCount > MaxPreviewCells)
        {
            DrawRectangleOutline(camera, new Rectangle(origin.X, origin.Y, pattern.Width, pattern.Height), color, 2f);
            return;
        }
        float inset = camera.Zoom >= 4f ? 0.12f : 0f;
        foreach (var cell in pattern.Cells)
        {
            var rect = new Vector2(origin.X + cell.X + inset, origin.Y + cell.Y + inset);
            _services.SpriteBatch.Draw(_services.Pixel, rect, null, color, 0f, Vector2.Zero, new Vector2(1f - inset * 2f), SpriteEffects.None, 0f);
        }
        DrawRectangleOutline(camera, new Rectangle(origin.X, origin.Y, pattern.Width, pattern.Height), color * 0.6f, 1f);
    }

    /// <summary>盤面上の長方形の枠。太さは画面のピクセル数で指定する(拡大率によらず同じ太さに見える)。</summary>
    public void DrawRectangleOutline(Camera2D camera, Rectangle area, Color color, float thicknessPixels)
    {
        float t = thicknessPixels / camera.Zoom;
        var batch = _services.SpriteBatch;
        var pixel = _services.Pixel;
        batch.Draw(pixel, new Vector2(area.Left - t, area.Top - t), null, color, 0f, Vector2.Zero, new Vector2(area.Width + t * 2, t), SpriteEffects.None, 0f);
        batch.Draw(pixel, new Vector2(area.Left - t, area.Bottom), null, color, 0f, Vector2.Zero, new Vector2(area.Width + t * 2, t), SpriteEffects.None, 0f);
        batch.Draw(pixel, new Vector2(area.Left - t, area.Top), null, color, 0f, Vector2.Zero, new Vector2(t, area.Height), SpriteEffects.None, 0f);
        batch.Draw(pixel, new Vector2(area.Right, area.Top), null, color, 0f, Vector2.Zero, new Vector2(t, area.Height), SpriteEffects.None, 0f);
    }

    public void FillRectangle(Rectangle area, Color color) =>
        _services.SpriteBatch.Draw(_services.Pixel, area, color);
}
