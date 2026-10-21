using Microsoft.Xna.Framework.Input;

namespace LifeGame.App.Input;

/// <summary>
/// マウスカーソルの形。各画面がフレームの中で欲しい形を頼み、アプリ本体がフレームの終わりに1回だけ反映する。
/// 何も頼まれなかったフレームは矢印に戻す。
/// </summary>
internal static class AppCursor
{
    private static MouseCursor _requested = MouseCursor.Arrow;
    private static MouseCursor? _applied;

    public static void Request(MouseCursor cursor) => _requested = cursor;

    public static void Apply()
    {
        // 形が変わったときだけ切り替える
        if (!ReferenceEquals(_applied, _requested))
        {
            Mouse.SetCursor(_requested);
            _applied = _requested;
        }
        _requested = MouseCursor.Arrow;
    }
}
