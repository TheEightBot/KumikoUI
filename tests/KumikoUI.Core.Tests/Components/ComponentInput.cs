using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Components;

/// <summary>Synthesizes pointer and keyboard input, focus and drawing for the drawn-component tests.</summary>
internal static class ComponentInput
{
    public static KumikoPointerEventArgs Ptr(InputAction action, float x, float y) =>
        new() { X = x, Y = y, Action = action };

    public static bool Down(this DrawnComponent c, float x, float y) => c.OnPointerDown(Ptr(InputAction.Pressed, x, y));
    public static bool Move(this DrawnComponent c, float x, float y) => c.OnPointerMove(Ptr(InputAction.Moved, x, y));
    public static bool Up(this DrawnComponent c, float x, float y) => c.OnPointerUp(Ptr(InputAction.Released, x, y));

    /// <summary>A full tap: press then release at the same point.</summary>
    public static void Tap(this DrawnComponent c, float x, float y)
    {
        c.Down(x, y);
        c.Up(x, y);
    }

    public static bool Key(this DrawnComponent c, KumikoKey key, char? character = null,
        InputModifiers modifiers = InputModifiers.None) =>
        c.OnKeyDown(new KumikoKeyEventArgs { Key = key, Character = character, Modifiers = modifiers });

    /// <summary>Registers the component with a new focus manager and gives it focus.</summary>
    public static DrawnFocusManager Focus(DrawnComponent c)
    {
        var manager = new DrawnFocusManager();
        manager.Register(c);
        manager.SetFocus(c);
        return manager;
    }

    public static RecordingDrawingContext Draw(DrawnComponent c)
    {
        var ctx = new RecordingDrawingContext();
        c.OnDraw(ctx);
        return ctx;
    }
}
