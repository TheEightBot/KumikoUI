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
/// Kept deliberately free of any <c>KumikoDataGrid</c> / framework-element dependency so it can be
/// unit-tested directly in Phase 07. The <see cref="KumikoDataGrid"/> control is the only caller;
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
    /// <see cref="InputModifiers"/> flags. <c>Menu</c> is Alt; <c>Windows</c> (the Command key on
    /// Apple keyboards) maps to <see cref="InputModifiers.Meta"/>, and also to
    /// <see cref="InputModifiers.Control"/> where Command is the shortcut modifier
    /// (see <see cref="CommandActsAsControl"/>).
    /// </summary>
    public static InputModifiers ToInputModifiers(VirtualKeyModifiers mods)
        => ToInputModifiers(mods, CommandActsAsControl);

    /// <summary>
    /// True on Apple platforms (macOS desktop, iOS, Mac Catalyst) and in the browser, where the
    /// Command key — reported by WinUI/Uno as <see cref="VirtualKeyModifiers.Windows"/> — is the
    /// primary shortcut modifier. KumikoUI.Core treats <see cref="InputModifiers.Control"/> as
    /// "Ctrl on Windows/Linux, Cmd on Mac", so on these platforms Command must also set
    /// <see cref="InputModifiers.Control"/> for Cmd+A/C/V/X, Cmd-click toggle selection and
    /// Cmd-click multi-sort to work. In the browser the Windows key never reaches a page with a
    /// shortcut, so treating Meta as Command there is safe on every OS.
    /// </summary>
    internal static bool CommandActsAsControl { get; } =
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ||
        OperatingSystem.IsMacCatalyst() || OperatingSystem.IsBrowser();

    /// <summary>
    /// Folds WinUI modifier flags into <see cref="InputModifiers"/>. The Windows/Command key always
    /// sets <see cref="InputModifiers.Meta"/>; when <paramref name="commandActsAsControl"/> is true it
    /// additionally sets <see cref="InputModifiers.Control"/> (see <see cref="CommandActsAsControl"/>).
    /// </summary>
    internal static InputModifiers ToInputModifiers(VirtualKeyModifiers mods, bool commandActsAsControl)
    {
        var result = InputModifiers.None;
        if ((mods & VirtualKeyModifiers.Shift) != 0) result |= InputModifiers.Shift;
        if ((mods & VirtualKeyModifiers.Control) != 0) result |= InputModifiers.Control;
        if ((mods & VirtualKeyModifiers.Menu) != 0) result |= InputModifiers.Alt; // Menu == Alt
        if ((mods & VirtualKeyModifiers.Windows) != 0)
        {
            result |= InputModifiers.Meta;
            if (commandActsAsControl) result |= InputModifiers.Control;
        }
        return result;
    }

    /// <summary>
    /// Reads the live modifier-key state for the current thread and folds it into
    /// <see cref="InputModifiers"/> with the same rules as <see cref="ToInputModifiers(VirtualKeyModifiers)"/>
    /// (so keyboard and pointer dispatch agree). This is the WinUI 3 way to read modifiers during
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
            var mods = VirtualKeyModifiers.None;
            if (IsDown(VirtualKey.Shift)) mods |= VirtualKeyModifiers.Shift;
            if (IsDown(VirtualKey.Control)) mods |= VirtualKeyModifiers.Control;
            if (IsDown(VirtualKey.Menu)) mods |= VirtualKeyModifiers.Menu;
            if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
                mods |= VirtualKeyModifiers.Windows;
            return ToInputModifiers(mods);
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
