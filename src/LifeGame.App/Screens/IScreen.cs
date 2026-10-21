using LifeGame.App.Input;
using LifeGame.Core;
using LifeGame.Core.Challenge;
using LifeGame.Core.Patterns;
using Microsoft.Xna.Framework;

namespace LifeGame.App.Screens;

/// <summary>
/// 画面の1つ。Update で入力を読み、状態を進め、UIを組み立てる(ボタンの処理もここで起きる)。
/// Draw では盤面などを描くだけにする。UIは最後にアプリ本体がまとめて描く。
/// </summary>
internal interface IScreen : IDisposable
{
    /// <summary>メニューの「演算方式」で切り替える盤面。盤面の無い画面は null。</summary>
    SimulationSession? Session { get; }

    /// <summary>この画面に切り替わったとき。</summary>
    void OnEnter();

    void Update(GameTime gameTime, InputState input);

    void Draw(GameTime gameTime);
}

/// <summary>画面の切り替え。画面どうしが直接お互いを知らなくて済むよう、アプリ本体が受け持つ。</summary>
internal interface INavigator
{
    /// <summary>サンドボックスへ。stamp を渡すと、それをスタンプとして持った状態にする。</summary>
    void ShowSandbox(Pattern? stamp = null);

    /// <summary>記録チャレンジへ。retry を渡すと、その記録の初期配置を置いた状態にする。</summary>
    void ShowChallenge(ChallengeRecord? retry = null);

    /// <summary>記録一覧へ。category を渡すと、その部門のタブを開く。</summary>
    void ShowRecords(int? category = null);
}
