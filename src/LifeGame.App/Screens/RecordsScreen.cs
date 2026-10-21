using System.Text;
using ImGuiNET;
using LifeGame.App.Input;
using LifeGame.App.Ui;
using LifeGame.Core;
using LifeGame.Core.Challenge;
using LifeGame.Core.Patterns;
using Microsoft.Xna.Framework;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace LifeGame.App.Screens;

/// <summary>記録一覧。部門ごとに上位10件を表で見て、再挑戦やサンドボックスでの再生へ進める。</summary>
internal sealed class RecordsScreen : IScreen
{
    private readonly AppServices _services;
    // 次のフレームで開くタブ(開いたら null に戻す)
    private int? _categoryToSelect;

    public RecordsScreen(AppServices services)
    {
        _services = services;
    }

    public SimulationSession? Session => null;

    public void SelectCategory(int category) => _categoryToSelect = category;

    public void OnEnter()
    {
    }

    public void Update(GameTime gameTime, InputState input)
    {
        float s = UiSetup.Scale;
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(io.DisplaySize * 0.5f, ImGuiCond.Always, new NumVector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new NumVector2(Math.Min(820 * s, io.DisplaySize.X - 40 * s), Math.Min(520 * s, io.DisplaySize.Y - 80 * s)), ImGuiCond.Always);
        if (ImGui.Begin("記録一覧", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
        {
            if (_services.Records.LoadError is { } error)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(1f, 0.55f, 0.45f, 1f));
                ImGui.TextUnformatted($"記録ファイルを読み込めなかったため、新しく始めています(元のファイルは .broken として残しました): {error}");
                ImGui.PopStyleColor();
            }

            if (ImGui.BeginTabBar("categories"))
            {
                foreach (int category in ChallengeRules.Categories)
                {
                    var flags = _categoryToSelect == category ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                    if (BeginTabItem($"{category}セル以下", flags))
                    {
                        DrawRanking(category, s);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
                _categoryToSelect = null;
            }

            ImGui.Separator();
            ImGui.TextDisabled($"保存先: {AppPaths.RecordsFile}");
        }
        ImGui.End();
    }

    public void Draw(GameTime gameTime)
    {
    }

    public void Dispose()
    {
    }

    private void DrawRanking(int category, float s)
    {
        var ranking = _services.Records.GetRanking(category);
        if (ranking.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted($"{category}セル以下の部門には、まだ記録がありません。");
            if (ImGui.Button("記録チャレンジへ"))
                _services.Navigator.ShowChallenge();
            return;
        }

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("ranking", 7, flags))
            return;

        ImGui.TableSetupColumn("順位", ImGuiTableColumnFlags.WidthFixed, 44 * s);
        ImGui.TableSetupColumn("寿命(世代)");
        ImGui.TableSetupColumn("結末");
        ImGui.TableSetupColumn("最大人口");
        ImGui.TableSetupColumn("セル数", ImGuiTableColumnFlags.WidthFixed, 56 * s);
        ImGui.TableSetupColumn("日時");
        ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 200 * s);
        ImGui.TableHeadersRow();

        for (int i = 0; i < ranking.Count; i++)
        {
            var record = ranking[i];
            ImGui.TableNextRow();
            Cell((i + 1).ToString());
            Cell(record.Lifespan.ToString("N0"));
            Cell(record.Died ? "消滅" : $"安定(人口 {record.FinalPopulation:N0})");
            Cell(record.MaxPopulation.ToString("N0"));
            Cell(record.CellCount.ToString());
            Cell(record.AchievedAt.LocalDateTime.ToString("yyyy/MM/dd HH:mm"));
            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"再挑戦##{i}"))
                _services.Navigator.ShowChallenge(record);
            ImGui.SameLine();
            if (ImGui.SmallButton($"サンドボックス##{i}"))
                _services.Navigator.ShowSandbox(Rle.Parse(record.SeedRle).WithName($"{category}セル部門 {i + 1}位"));
        }
        ImGui.EndTable();
    }

    // ImGui.NET の BeginTabItem はフラグを渡すと閉じるボタンも付くので、閉じるボタンなしの版を直接呼ぶ
    private static unsafe bool BeginTabItem(string label, ImGuiTabItemFlags flags)
    {
        int byteCount = Encoding.UTF8.GetByteCount(label);
        byte* utf8 = stackalloc byte[byteCount + 1];
        fixed (char* chars = label)
            Encoding.UTF8.GetBytes(chars, label.Length, utf8, byteCount);
        utf8[byteCount] = 0;
        return ImGuiNative.igBeginTabItem(utf8, null, flags) != 0;
    }

    private static void Cell(string text)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(text);
    }
}
