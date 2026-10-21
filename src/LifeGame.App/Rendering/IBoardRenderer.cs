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

    /// <summary>盤面を、盤面座標 (0, 0)〜(幅, 高さ) の四角形として描く。</summary>
    void Draw(SpriteBatch spriteBatch, Matrix boardToScreen, Viewport viewport);
}
