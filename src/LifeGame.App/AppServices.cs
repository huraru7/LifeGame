using LifeGame.App.Screens;
using LifeGame.Core.Challenge;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App;

/// <summary>各画面が共通で使うもの。アプリ本体が1つだけ作り、画面へ渡す。</summary>
internal sealed class AppServices
{
    public required GraphicsDevice Device { get; init; }

    public required SpriteBatch SpriteBatch { get; init; }

    /// <summary>1×1の白い画像。枠や塗りつぶしに使う。</summary>
    public required Texture2D Pixel { get; init; }

    public required Effect ExpandEffect { get; init; }

    public required Effect DisplayEffect { get; init; }

    public required RecordStore Records { get; init; }

    public required ViewSettings View { get; init; }

    public required INavigator Navigator { get; init; }
}

/// <summary>表示の設定(メニューから切り替える)。</summary>
internal sealed class ViewSettings
{
    public static readonly string[] RendererNames = ["標準(GPUで展開)", "比較用(CPUで色に展開)"];

    public int RendererIndex { get; set; }

    public bool ShowObservation { get; set; } = true;
}

/// <summary>データの保存先。Windowsでは %APPDATA%\LifeGame。</summary>
internal static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LifeGame");

    public static string RecordsFile => Path.Combine(DataDirectory, "records.json");

    public static string PatternsDirectory => Path.Combine(DataDirectory, "patterns");
}
