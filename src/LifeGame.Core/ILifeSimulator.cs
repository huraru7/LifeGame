namespace LifeGame.Core;

/// <summary>
/// ライフゲームの演算方式。Width × Height は、盤面の受け渡しに使う範囲(表示する範囲)。
/// 盤面の受け渡しは、どの方式でも共通の「詰めた形式」(<see cref="PackedBoard"/>)で行う。
/// </summary>
public interface ILifeSimulator
{
    string Name { get; }

    int Width { get; }

    int Height { get; }

    /// <summary>
    /// 盤面の端が反対側とつながっている(トーラス)か。false なら範囲の外にも盤面が続いていて、
    /// 範囲の外の座標にもセルを置ける。
    /// </summary>
    bool WrapsAround => true;

    /// <summary>詰めた形式の盤面を読み込む。</summary>
    void Load(ReadOnlySpan<ulong> packed);

    /// <summary>現在の盤面を詰めた形式で書き出す。</summary>
    void CopyTo(Span<ulong> packed);

    void SetCell(int x, int y, bool alive);

    /// <summary>生存セルの数(人口)を数える。</summary>
    long CountPopulation();

    /// <summary>1世代進める。</summary>
    void Step();
}
