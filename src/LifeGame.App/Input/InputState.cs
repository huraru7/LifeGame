using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace LifeGame.App.Input;

/// <summary>
/// このフレームと前のフレームの入力。UIがマウス・キーボードを使っているときは、盤面への操作として扱わない。
/// </summary>
internal sealed class InputState
{
    public KeyboardState Keyboard { get; private set; }

    public KeyboardState PreviousKeyboard { get; private set; }

    public MouseState Mouse { get; private set; }

    public MouseState PreviousMouse { get; private set; }

    /// <summary>マウスがUIの上にある、またはUIを操作中。</summary>
    public bool UiWantsMouse { get; private set; }

    /// <summary>UIの文字入力欄などにキーボードが向いている。</summary>
    public bool UiWantsKeyboard { get; private set; }

    public Vector2 MousePosition => Mouse.Position.ToVector2();

    public Vector2 MouseDelta => (Mouse.Position - PreviousMouse.Position).ToVector2();

    public int WheelDelta => Mouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;

    public bool Ctrl => Keyboard.IsKeyDown(Keys.LeftControl) || Keyboard.IsKeyDown(Keys.RightControl);

    public bool Shift => Keyboard.IsKeyDown(Keys.LeftShift) || Keyboard.IsKeyDown(Keys.RightShift);

    public bool LeftDown => Mouse.LeftButton == ButtonState.Pressed;

    public bool RightDown => Mouse.RightButton == ButtonState.Pressed;

    public bool LeftPressed => LeftDown && PreviousMouse.LeftButton == ButtonState.Released;

    public bool RightPressed => RightDown && PreviousMouse.RightButton == ButtonState.Released;

    public bool MiddleDragging =>
        Mouse.MiddleButton == ButtonState.Pressed && PreviousMouse.MiddleButton == ButtonState.Pressed;

    public void Update(bool uiWantsMouse, bool uiWantsKeyboard)
    {
        PreviousKeyboard = Keyboard;
        PreviousMouse = Mouse;
        Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        Mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        UiWantsMouse = uiWantsMouse;
        UiWantsKeyboard = uiWantsKeyboard;
    }

    /// <summary>このフレームで押されたか(UIが文字入力中なら false)。</summary>
    public bool Pressed(Keys key) =>
        !UiWantsKeyboard && Keyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);

    public bool Held(Keys key) => !UiWantsKeyboard && Keyboard.IsKeyDown(key);
}
