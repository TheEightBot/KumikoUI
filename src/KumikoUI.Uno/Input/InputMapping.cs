using KumikoUI.Core.Input;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace KumikoUI.Uno.Input;

/// <summary>
/// Pure, side-effect-free translation between WinUI/Uno input enums
/// (<see cref="VirtualKey"/>, <see cref="VirtualKeyModifiers"/>) and KumikoUI.Core's
/// platform-independent input contract (<see cref="KumikoKey"/>, <see cref="InputModifiers"/>).
/// </summary>
/// <remarks>
/// Kept deliberately free of any <c>KumikoDataGrid</c> / framework-element dependency so it can be
/// unit-tested directly in Phase 07. The <see cref="KumikoDataGrid"/> control is the only caller;
/// it owns event subscription and merely defers the enum mapping here.
/// </remarks>
public static class InputMapping
{
    /// <summary>
    /// Maps a WinUI <see cref="VirtualKey"/> to KumikoUI.Core's <see cref="KumikoKey"/>.
    /// Returns <see cref="KumikoKey.None"/> for keys the grid does not handle as commands
    /// (printable text arrives separately through the proxy's text changes).
    /// </summary>
    public static KumikoKey ToGridKey(VirtualKey key) => key switch
    {
        // ── Navigation ──
        VirtualKey.Up => KumikoKey.Up,
        VirtualKey.Down => KumikoKey.Down,
        VirtualKey.Left => KumikoKey.Left,
        VirtualKey.Right => KumikoKey.Right,
        VirtualKey.Home => KumikoKey.Home,
        VirtualKey.End => KumikoKey.End,
        VirtualKey.PageUp => KumikoKey.PageUp,
        VirtualKey.PageDown => KumikoKey.PageDown,
        VirtualKey.Tab => KumikoKey.Tab,

        // ── Actions ──
        VirtualKey.Enter => KumikoKey.Enter,
        VirtualKey.Escape => KumikoKey.Escape,
        VirtualKey.Space => KumikoKey.Space,
        VirtualKey.Delete => KumikoKey.Delete,
        VirtualKey.Back => KumikoKey.Backspace, // WinUI "Back" == Backspace
        VirtualKey.F2 => KumikoKey.F2,

        // ── Letters with grid shortcuts (Ctrl+A/C/V/X/Z/Y) ──
        VirtualKey.A => KumikoKey.A,
        VirtualKey.C => KumikoKey.C,
        VirtualKey.V => KumikoKey.V,
        VirtualKey.X => KumikoKey.X,
        VirtualKey.Z => KumikoKey.Z,
        VirtualKey.Y => KumikoKey.Y,

        _ => KumikoKey.None
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

    /// <summary>
    /// What the hidden proxy <c>TextBox</c>'s <c>KeyDown</c> does with a command key after Core has seen it.
    /// </summary>
    internal enum ProxyKeyOutcome
    {
        /// <summary>Mark the key handled: Core used it.</summary>
        Handled,
        /// <summary>
        /// Leave the key unhandled: it reaches the text box (whose text mirrors the editor's, so anything it does
        /// arrives as an ordinary edit) and then the page (default and cancel buttons, accelerators, scrolling).
        /// </summary>
        PassToPage,
        /// <summary>Move keyboard focus out of the grid (Tab or Shift+Tab that Core left unhandled).</summary>
        MoveFocusOut,
    }

    /// <summary>
    /// Decides <see cref="ProxyKeyOutcome"/> for <paramref name="key"/> given whether Core handled it, so the
    /// grid is never a keyboard trap: a key is marked handled only when Core handled it. An unhandled Tab moves
    /// focus out of the grid (the grid and its proxy are both tab stops, so the host steps past both); any other
    /// unhandled key, Escape with nothing to cancel among them, is left to the page.
    /// </summary>
    internal static ProxyKeyOutcome GetProxyKeyOutcome(KumikoKey key, bool handledByCore)
    {
        if (handledByCore) return ProxyKeyOutcome.Handled;
        return key == KumikoKey.Tab ? ProxyKeyOutcome.MoveFocusOut : ProxyKeyOutcome.PassToPage;
    }

    private static bool IsDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down)
            == CoreVirtualKeyStates.Down;
}
