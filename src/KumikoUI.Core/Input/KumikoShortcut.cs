namespace KumikoUI.Core.Input;

/// <summary>
/// A keyboard shortcut: a key with the modifiers held with it, such as Ctrl+Alt+C for the column chooser
/// (<see cref="KumikoInputController.ColumnChooserShortcut"/>).
/// </summary>
/// <remarks>
/// <see cref="InputModifiers.Control"/> stands for the platform's command modifier: it matches Control, and Command on
/// Apple keyboards (<see cref="KumikoKeyEventArgs.HasCommand"/>), so one shortcut works on every platform. On Windows,
/// AltGr arrives as Control+Alt, so a Control+Alt shortcut also catches AltGr with that key on layouts where AltGr
/// types a character (AltGr+C is "ć" on a Polish keyboard).
/// </remarks>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifiers that must be held, and no others.</param>
public readonly record struct KumikoShortcut(KumikoKey Key, InputModifiers Modifiers)
{
    /// <summary>
    /// Whether <paramref name="e"/> is this shortcut: the same key, Shift and Alt exactly as in <see cref="Modifiers"/>,
    /// and the command modifier (Control or Meta) held exactly when <see cref="Modifiers"/> has Control or Meta.
    /// </summary>
    /// <param name="e">A key event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is null.</exception>
    public bool Matches(KumikoKeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key != Key || Key == KumikoKey.None) return false;
        const InputModifiers command = InputModifiers.Control | InputModifiers.Meta;
        bool wantsCommand = (Modifiers & command) != 0;
        const InputModifiers others = InputModifiers.Shift | InputModifiers.Alt;
        return e.HasCommand == wantsCommand && (e.Modifiers & others) == (Modifiers & others);
    }
}
