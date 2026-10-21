using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App.Rendering;

/// <summary>詰めた形式の盤面を GPU へ転送し、画面に描く方式。</summary>
internal interface IBoardRenderer : IDisposable
{
    string Name { get; }

    /// <summary>直近の転送(CPU側の変換を含む)にかかった時間(ミリ秒)。</summary>
    double LastUploadMilliseconds { get; }

    void Upload(ReadOnlySpan<ulong> packed);

    /// <summary>
    /// 盤面座標の範囲 visibleArea を描く。範囲が盤面の外にはみ出した分は、盤面を繰り返して描く
    /// (盤面の端は反対側とつながっているので、継ぎ目なく続いて見える)。
    /// </summary>
    void Draw(SpriteBatch spriteBatch, Matrix boardToScreen, Viewport viewport, Rectangle visibleArea);
}
