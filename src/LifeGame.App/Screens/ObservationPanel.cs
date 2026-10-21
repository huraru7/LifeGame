using ImGuiNET;
using LifeGame.App.Board;
using LifeGame.App.Ui;
using LifeGame.Core;
using LifeGame.Core.Analysis;
using NumVector2 = System.Numerics.Vector2;

namespace LifeGame.App.Screens;

/// <summary>画面右上の「観察」パネル。世代・人口・人口の推移・落ち着いたか、と計測値を出す。</summary>
internal sealed class ObservationPanel
{
    private readonly float[] _history = new float[PopulationStats.HistoryLength];

    public void Draw(string id, SimulationSession session, BoardView view)
    {
        float s = UiSetup.Scale;
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new NumVector2(io.DisplaySize.X - 10 * s, 40 * s), ImGuiCond.Always, new NumVector2(1, 0));
        ImGui.SetNextWindowSize(new NumVector2(300 * s, 0), ImGuiCond.Always);
        if (Panels.Begin($"観察##{id}", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
        {
            var stats = session.Stats;
            Row("世代", session.Generation.ToString("N0"));
            Row("人口", stats.Population.ToString("N0"));
            Row("最大人口", stats.MaxPopulation.ToString("N0"));
            Row("状態", StateText(stats));
            if (stats.Detector.IsStable && stats.Population > 0)
                Row("人口の周期", stats.Detector.PopulationPeriod.ToString());

            int count = stats.CopyHistory(_history);
            if (count > 1)
            {
                float max = 1f;
                for (int i = 0; i < count; i++)
                    max = Math.Max(max, _history[i]);
                ImGui.PlotLines("##population", ref _history[0], count, 0, null, 0f, max * 1.1f, new NumVector2(ImGui.GetContentRegionAvail().X, 56 * s));
            }

            ImGui.Separator();
            ImGui.TextDisabled($"演算 {session.Current.Name}  {session.AverageStepMilliseconds:F3} ms/世代");
            ImGui.TextDisabled($"描画 {view.Renderer.Name}  転送 {view.Renderer.LastUploadMilliseconds:F2} ms");
            ImGui.TextDisabled($"{io.Framerate:F0} FPS");
        }
        ImGui.End();
    }

    private static string StateText(PopulationStats stats)
    {
        var detector = stats.Detector;
        if (!detector.IsStable)
            return stats.Population == 0 ? "セルがありません" : "変化中";
        if (stats.Population == 0)
            return $"消滅(世代 {detector.StableSince:N0})";
        return $"落ち着いた(世代 {detector.StableSince:N0} から)";
    }

    private static void Row(string label, string value)
    {
        ImGui.TextDisabled(label);
        ImGui.SameLine(100 * UiSetup.Scale);
        ImGui.TextUnformatted(value);
    }
}
