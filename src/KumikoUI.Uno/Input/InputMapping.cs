using KumikoUI.Core.Input;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace KumikoUI.Uno.Input;

/// <summary>
/// Pure, side-effect-free translation between WinUI/Uno input enums
/// (<see cref="VirtualKey"/>, <see cref="VirtualKeyModifiers"/>) and KumikoUI.Core's
/// platform-independent input contract (<see cref="GridKey"/>, <see cref="InputModifiers"/>).
/// </summary>
/// <remarks>
/// Kept deliberately free of any <c>DataGridView</c> / framework-element dependency so it can be
/// unit-tested directly in Phase 07. The <see cref="DataGridView"/> control is the only caller;
/// it owns event subscription and merely defers the enum mapping here.
/// </remarks>
public static class InputMapping
{
    /// <summary>
    /// Maps a WinUI <see cref="VirtualKey"/> to KumikoUI.Core's <see cref="GridKey"/>.
    /// Returns <see cref="GridKey.None"/> for keys the grid does not handle as commands
    /// (printable text arrives separately via the proxy's <c>TextChanged</c> event).
    /// </summary>
    public static GridKey ToGridKey(VirtualKey key) => key switch
    {
        // ── Navigation ──
        VirtualKey.Up => GridKey.Up,
        VirtualKey.Down => GridKey.Down,
        VirtualKey.Left => GridKey.Left,
        VirtualKey.Right => GridKey.Right,
        VirtualKey.Home => GridKey.Home,
        VirtualKey.End => GridKey.End,
        VirtualKey.PageUp => GridKey.PageUp,
        VirtualKey.PageDown => GridKey.PageDown,
        VirtualKey.Tab => GridKey.Tab,

        // ── Actions ──
        VirtualKey.Enter => GridKey.Enter,
        VirtualKey.Escape => GridKey.Escape,
        VirtualKey.Space => GridKey.Space,
        VirtualKey.Delete => GridKey.Delete,
        VirtualKey.Back => GridKey.Backspace, // WinUI "Back" == Backspace
        VirtualKey.F2 => GridKey.F2,

        // ── Letters with grid shortcuts (Ctrl+A/C/V/X/Z) ──
        VirtualKey.A => GridKey.A,
        VirtualKey.C => GridKey.C,
        VirtualKey.V => GridKey.V,
        VirtualKey.X => GridKey.X,
        VirtualKey.Z => GridKey.Z,

        _ => GridKey.None
    };

    /// <summary>
    /// Maps WinUI <see cref="VirtualKeyModifiers"/> flags to KumikoUI.Core's
    /// <see cref="InputModifiers"/> flags. <c>Menu</c> is Alt; <c>Windows</c> maps to
    /// <see cref="InputModifiers.Meta"/>.
    /// </summary>
    public static InputModifiers ToInputModifiers(VirtualKeyModifiers mods)
    {
        var result = InputModifiers.None;
        if ((mods & VirtualKeyModifiers.Shift) != 0) result |= InputModifiers.Shift;
        if ((mods & VirtualKeyModifiers.Control) != 0) result |= InputModifiers.Control;
        if ((mods & VirtualKeyModifiers.Menu) != 0) result |= InputModifiers.Alt; // Menu == Alt
        if ((mods & VirtualKeyModifiers.Windows) != 0) result |= InputModifiers.Meta;
        return result;
    }

    /// <summary>
    /// Reads the live modifier-key state for the current thread and folds it into
    /// <see cref="InputModifiers"/>. This is the WinUI 3 way to read modifiers during
    /// <c>KeyDown</c>/<c>CharacterReceived</c> (where <c>CoreWindow.GetKeyState</c> returns null and
    /// Uno's <c>KeyRoutedEventArgs.KeyboardModifiers</c> is <c>[NotImplemented]</c> on Skia/WASM).
    /// Backed by Uno's keyboard-state tracker on Skia and native key state on Windows. Defensively
    /// returns <see cref="InputModifiers.None"/> if the platform doesn't implement the lookup,
    /// so a keystroke never throws.
    /// </summary>
    public static InputModifiers GetLiveModifiers()
    {
        try
        {
            var result = InputModifiers.None;
            if (IsDown(VirtualKey.Shift)) result |= InputModifiers.Shift;
            if (IsDown(VirtualKey.Control)) result |= InputModifiers.Control;
            if (IsDown(VirtualKey.Menu)) result |= InputModifiers.Alt;
            if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
                result |= InputModifiers.Meta;
            return result;
        }
        catch (NotImplementedException)
        {
            return InputModifiers.None;
        }
    }

    private static bool IsDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down)
            == CoreVirtualKeyStates.Down;
}
