using Microsoft.Xna.Framework;

namespace LifeGame.App.Rendering;

/// <summary>盤面の配色(見た目「クリーン」)。</summary>
internal static class BoardColors
{
    public static readonly Color Alive = new(236, 222, 150);
    // 死んだセルの四角と、セルの間の隙間。拡大したときに格子として見える
    public static readonly Color Dead = new(27, 30, 40);
    public static readonly Color Gap = new(18, 20, 28);
    // 盤面の外側
    public static readonly Color Outside = new(10, 11, 15);
    public static readonly Color Highlight = new(236, 222, 150);
    public static readonly Color Accent = new(126, 196, 255);
}
