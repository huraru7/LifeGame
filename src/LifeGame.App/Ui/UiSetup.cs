using System.Runtime.InteropServices;
using ImGuiNET;
using NumVector4 = System.Numerics.Vector4;

namespace LifeGame.App.Ui;

/// <summary>UIのフォント(日本語)と配色・大きさを整える。画面の拡大率(DPI)に合わせて大きさをそろえる。</summary>
internal static class UiSetup
{
    private const float BaseFontSize = 17f;

    // 上から順に探し、最初に見つかったフォントを使う
    private static readonly string[] JapaneseFontCandidates =
    [
        @"C:\Windows\Fonts\YuGothM.ttc",
        @"C:\Windows\Fonts\meiryo.ttc",
        @"C:\Windows\Fonts\msgothic.ttc",
        "/System/Library/Fonts/ヒラギノ角ゴシック W4.ttc",
        "/System/Library/Fonts/Hiragino Sans GB.ttc",
    ];

    /// <summary>フォントが見つからず、日本語が表示できないときの理由。</summary>
    public static string? FontWarning { get; private set; }

    public static float Scale { get; private set; } = 1f;

    /// <summary>画面左の操作パネルが占める幅(カメラを寄せるとき、この右側の中央に寄せる)。</summary>
    public static float LeftPanelWidth => 440f * Scale;

    public static unsafe void Configure(ImGuiRenderer renderer)
    {
        Scale = DetectDisplayScale();
        var io = ImGui.GetIO();

        string? fontPath = JapaneseFontCandidates.FirstOrDefault(File.Exists);
        ApplyStyle();
        if (fontPath is null)
        {
            FontWarning = "日本語フォントが見つからないため、日本語の文字が表示されません。";
            io.Fonts.AddFontDefault();
        }
        else
        {
            // 日本語の範囲には「…」などの記号が入っていないので、使う記号を足す
            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddRanges(io.Fonts.GetGlyphRangesJapanese());
            foreach (char symbol in "…〜―→←↑↓")
                builder.AddChar(symbol);
            builder.BuildRanges(out ImVector ranges);

            ImFontConfigPtr config = ImGuiNative.ImFontConfig_ImFontConfig();
            // 横方向に2倍の細かさで文字を作り、小さな文字でも滲みにくくする
            config.OversampleH = 2;
            config.OversampleV = 1;
            io.Fonts.AddFontFromFileTTF(fontPath, BaseFontSize * Scale, config, ranges.Data);
            // 文字の範囲は、文字の画像を作り終えるまで残しておく必要がある
            renderer.BuildFontAtlas();
            config.Destroy();
            builder.Destroy();
            return;
        }
        renderer.BuildFontAtlas();
    }

    private static void ApplyStyle()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 8f;
        style.ChildRounding = 6f;
        style.FrameRounding = 5f;
        style.PopupRounding = 6f;
        style.GrabRounding = 5f;
        style.TabRounding = 5f;
        style.WindowBorderSize = 0f;
        style.FrameBorderSize = 0f;
        style.WindowPadding = new System.Numerics.Vector2(12, 10);
        style.FramePadding = new System.Numerics.Vector2(8, 5);
        style.ItemSpacing = new System.Numerics.Vector2(8, 7);
        style.ScaleAllSizes(Scale);

        // 盤面の配色(紺に近い黒と、生きたセルの淡い黄色)に合わせる
        var colors = style.Colors;
        var accent = new NumVector4(0.925f, 0.871f, 0.588f, 1f);
        colors[(int)ImGuiCol.WindowBg] = new NumVector4(0.086f, 0.094f, 0.125f, 0.94f);
        colors[(int)ImGuiCol.PopupBg] = new NumVector4(0.086f, 0.094f, 0.125f, 0.98f);
        colors[(int)ImGuiCol.MenuBarBg] = new NumVector4(0.071f, 0.078f, 0.106f, 1f);
        colors[(int)ImGuiCol.TitleBg] = new NumVector4(0.071f, 0.078f, 0.106f, 1f);
        colors[(int)ImGuiCol.TitleBgActive] = new NumVector4(0.110f, 0.122f, 0.165f, 1f);
        colors[(int)ImGuiCol.FrameBg] = new NumVector4(0.141f, 0.153f, 0.200f, 1f);
        colors[(int)ImGuiCol.FrameBgHovered] = new NumVector4(0.188f, 0.204f, 0.267f, 1f);
        colors[(int)ImGuiCol.FrameBgActive] = new NumVector4(0.227f, 0.247f, 0.322f, 1f);
        colors[(int)ImGuiCol.Button] = new NumVector4(0.165f, 0.180f, 0.235f, 1f);
        colors[(int)ImGuiCol.ButtonHovered] = new NumVector4(0.227f, 0.247f, 0.322f, 1f);
        colors[(int)ImGuiCol.ButtonActive] = new NumVector4(0.290f, 0.314f, 0.408f, 1f);
        colors[(int)ImGuiCol.Header] = new NumVector4(0.165f, 0.180f, 0.235f, 1f);
        colors[(int)ImGuiCol.HeaderHovered] = new NumVector4(0.227f, 0.247f, 0.322f, 1f);
        colors[(int)ImGuiCol.HeaderActive] = new NumVector4(0.290f, 0.314f, 0.408f, 1f);
        colors[(int)ImGuiCol.Tab] = new NumVector4(0.141f, 0.153f, 0.200f, 1f);
        colors[(int)ImGuiCol.TabHovered] = new NumVector4(0.227f, 0.247f, 0.322f, 1f);
        colors[(int)ImGuiCol.TabSelected] = new NumVector4(0.227f, 0.247f, 0.322f, 1f);
        colors[(int)ImGuiCol.CheckMark] = accent;
        colors[(int)ImGuiCol.SliderGrab] = accent with { W = 0.8f };
        colors[(int)ImGuiCol.SliderGrabActive] = accent;
        colors[(int)ImGuiCol.PlotLines] = accent;
        colors[(int)ImGuiCol.PlotHistogram] = accent;
        colors[(int)ImGuiCol.Separator] = new NumVector4(0.2f, 0.216f, 0.282f, 1f);
        colors[(int)ImGuiCol.TableHeaderBg] = new NumVector4(0.141f, 0.153f, 0.200f, 1f);
        colors[(int)ImGuiCol.TableRowBgAlt] = new NumVector4(1f, 1f, 1f, 0.03f);
    }

    // Windowsでは画面の拡大率(125%など)を読み取る。それ以外は等倍
    private static float DetectDisplayScale()
    {
        if (!OperatingSystem.IsWindows())
            return 1f;
        try
        {
            return GetDpiForSystem() / 96f;
        }
        catch (EntryPointNotFoundException)
        {
            return 1f;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
