using System.Text;
using LifeGame.App.Rendering;
using LifeGame.Core;
using LifeGame.Core.Simulators;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LifeGame.App;

/// <summary>画面左上の計測値と操作説明。文字列は毎フレーム作り直さず、StringBuilder を使い回す。</summary>
internal sealed class Hud
{
    private static readonly Vector2 Margin = new(10, 8);

    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly StringBuilder _text = new(1024);
    private int _frames;
    private double _frameSeconds;
    private double _fps;

    public Hud(SpriteFont font, Texture2D pixel)
    {
        _font = font;
        _pixel = pixel;
    }

    public bool Visible { get; set; } = true;

    /// <summary>FPSは描画の回数で数える(表示のための計測で、盤面の状態には関わらない)。</summary>
    public void CountFrame(GameTime gameTime)
    {
        _frames++;
        _frameSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        if (_frameSeconds >= 0.5)
        {
            _fps = _frames / _frameSeconds;
            _frames = 0;
            _frameSeconds = 0;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SimulationSession session, IBoardRenderer renderer)
    {
        if (!Visible)
            return;

        _text.Clear();
        _text.Append($"Board {session.Width}x{session.Height}   Gen {session.Generation}   FPS {_fps:F0}\n");
        _text.Append($"[1-{session.SimulatorCount}] Sim: {session.Current.Name}   step {session.AverageStepMilliseconds:F3} ms");
        if (session.Current is TileSkipSimulator tileSkip)
            _text.Append($"   active tiles {tileSkip.ActiveTileRatio:P0}");
        _text.Append('\n');
        _text.Append($"[Tab] Render: {renderer.Name}   upload {renderer.LastUploadMilliseconds:F3} ms\n");
        _text.Append($"[+/-] Speed: {session.GenerationsPerFrame:0.###} gen/frame   {(session.Paused ? "PAUSED [N] step" : "[Space] pause")}\n");
        _text.Append("[R] random [C] clear [F] fit [H] hide   LMB draw  RMB erase  MMB/arrows pan  wheel zoom");

        Vector2 size = _font.MeasureString(_text);
        spriteBatch.Begin();
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, (int)(size.X + Margin.X * 2), (int)(size.Y + Margin.Y * 2)), new Color(0, 0, 0, 170));
        spriteBatch.DrawString(_font, _text, Margin, Color.White);
        spriteBatch.End();
    }
}
