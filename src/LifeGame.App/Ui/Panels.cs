using ImGuiNET;
using NumVector2 = System.Numerics.Vector2;

namespace LifeGame.App.Ui;

/// <summary>
/// 見出し(タイトルバー)を1回クリックするだけで畳む・開くパネル。ImGui の標準はダブルクリックで開閉する。
/// 見出しをドラッグしたとき(パネルを動かしたとき)は開閉しない。
/// </summary>
internal static class Panels
{
    // ドラッグとみなす、押してから離すまでのマウスの移動量(ピクセル)
    private const float ClickTolerance = 4f;

    private static readonly Dictionary<string, bool> Collapsed = [];
    private static string? _pressedTitle;
    private static NumVector2 _pressPosition;

    /// <summary>ImGui.Begin の代わりに使う。戻り値が false でも、呼び出し側は必ず ImGui.End() を呼ぶ。</summary>
    public static bool Begin(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        bool collapsed = Collapsed.GetValueOrDefault(name);
        // ImGui が自分で開閉(ダブルクリック)しても、毎フレームこちらの状態で上書きする
        ImGui.SetNextWindowCollapsed(collapsed, ImGuiCond.Always);
        bool open = ImGui.Begin(name, flags);

        var position = ImGui.GetWindowPos();
        var mouse = ImGui.GetMousePos();
        bool overTitle = ImGui.IsWindowHovered()
            && mouse.X >= position.X && mouse.X < position.X + ImGui.GetWindowWidth()
            && mouse.Y >= position.Y && mouse.Y < position.Y + ImGui.GetFrameHeight();

        if (overTitle && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _pressedTitle = name;
            _pressPosition = mouse;
        }
        if (_pressedTitle == name && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (overTitle && (mouse - _pressPosition).Length() < ClickTolerance)
                Collapsed[name] = !collapsed;
            _pressedTitle = null;
        }
        return open;
    }
}
