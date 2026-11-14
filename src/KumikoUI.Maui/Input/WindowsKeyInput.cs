using KumikoUI.Core.Input;

namespace KumikoUI.Maui;

/// <summary>
/// The platform-independent rules of the Windows keyboard path: which WinUI keys reach Core as keys, which key
/// events are marked handled, and how <c>CharacterReceived</c> becomes text. Virtual keys are the integer values
/// of <c>Windows.System.VirtualKey</c>, so the rules are unit-tested on any OS.
/// </summary>
internal static class WindowsKeyInput
{
    // Windows.System.VirtualKey values
    internal const int Back = 8, Tab = 9, Enter = 13, Escape = 27, Space = 32, PageUp = 33, PageDown = 34,
        End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40, Delete = 46,
        A = 65, C = 67, V = 86, X = 88, Y = 89, Z = 90, F2 = 113;

    /// <summary>
    /// The Core key for a WinUI virtual key, or <see cref="KumikoKey.None"/> for a key that is text (it arrives
    /// through the text services or <c>CharacterReceived</c>) or that the grid does not use. A, C, V, X, Y and Z
    /// are keys only as shortcuts (<see cref="IsShortcut"/>); otherwise they are letters.
    /// </summary>
    public static KumikoKey ToKumikoKey(int virtualKey, InputModifiers modifiers) => virtualKey switch
    {
        Up => KumikoKey.Up,
        Down => KumikoKey.Down,
        Left => KumikoKey.Left,
        Right => KumikoKey.Right,
        Home => KumikoKey.Home,
        End => KumikoKey.End,
        PageUp => KumikoKey.PageUp,
        PageDown => KumikoKey.PageDown,
        Tab => KumikoKey.Tab,
        Enter => KumikoKey.Enter,
        Escape => KumikoKey.Escape,
        Space => KumikoKey.Space,
        Delete => KumikoKey.Delete,
        Back => KumikoKey.Backspace,
        F2 => KumikoKey.F2,
        A when IsShortcut(modifiers) => KumikoKey.A,
        C when IsShortcut(modifiers) => KumikoKey.C,
        V when IsShortcut(modifiers) => KumikoKey.V,
        X when IsShortcut(modifiers) => KumikoKey.X,
        Y when IsShortcut(modifiers) => KumikoKey.Y,
        Z when IsShortcut(modifiers) => KumikoKey.Z,
        _ => KumikoKey.None,
    };

    /// <summary>
    /// Whether the modifiers make a letter a shortcut: Control without Alt. Control+Alt is AltGr, which types
    /// characters on many layouts (Polish AltGr+A is "ą", AltGr+C is "ć").
    /// </summary>
    public static bool IsShortcut(InputModifiers modifiers) =>
        (modifiers & InputModifiers.Control) != 0 && (modifiers & InputModifiers.Alt) == 0;

    /// <summary>
    /// Whether the WinUI key event is marked handled after Core saw <paramref name="key"/>. A key Core used is
    /// handled. Tab that Core left alone is the grid's Tab exit: it stays unhandled so WinUI moves focus to the
    /// next control. Navigation and deletion keys are always handled, so they never move focus or scroll a page
    /// around the grid (unless Alt is held, which leaves them to the app's accelerators). Enter, Escape, F2 and
    /// shortcuts Core left alone reach the page: its default and cancel buttons and its accelerators.
    /// </summary>
    public static bool ShouldMarkHandled(KumikoKey key, InputModifiers modifiers, bool handledByCore)
    {
        if (handledByCore) return true;
        return key switch
        {
            KumikoKey.Up or KumikoKey.Down or KumikoKey.Left or KumikoKey.Right or KumikoKey.Home or KumikoKey.End or
                KumikoKey.PageUp or KumikoKey.PageDown or KumikoKey.Backspace or KumikoKey.Delete or KumikoKey.Space
                => (modifiers & InputModifiers.Alt) == 0,
            _ => false,
        };
    }
}

/// <summary>
/// Turns WinUI <c>CharacterReceived</c> into text for Core, one keystroke at a time: control characters (the text
/// form of Enter, Tab, Backspace, Escape and Ctrl+letter, which arrive as keys) are dropped, a surrogate pair that
/// arrives as two characters is joined, the space of a Space key that Core already used is dropped, and nothing is
/// passed on while the text services (<c>CoreTextEditContext</c>) deliver the text themselves.
/// </summary>
internal sealed class WindowsCharacterInput
{
    private char _pendingHighSurrogate;
    private bool _dropSpaceEcho;
    private bool _textServicesDeliverText;
    private bool _textServicesUpdatedSinceKeyDown;

    /// <summary>
    /// A key went down. <paramref name="handledByCore"/> is whether it was sent to Core as
    /// <paramref name="key"/> and used there: the space a used Space key also types is then dropped.
    /// </summary>
    public void KeyDown(KumikoKey key, bool handledByCore)
    {
        _textServicesUpdatedSinceKeyDown = false;
        _dropSpaceEcho = key == KumikoKey.Space && handledByCore;
    }

    /// <summary>
    /// The text services gained or lost the editor. Until they deliver text (<see cref="TextServicesUpdated"/>),
    /// characters are passed on, so typing works where the text services are unavailable or inactive.
    /// </summary>
    public void TextServicesFocusChanged()
    {
        _textServicesDeliverText = false;
        _textServicesUpdatedSinceKeyDown = false;
        _pendingHighSurrogate = default;
    }

    /// <summary>
    /// The text services changed the text: from now on (until their focus changes) they are the source of text,
    /// and the characters of the same keystrokes are not passed on again.
    /// </summary>
    public void TextServicesUpdated()
    {
        _textServicesDeliverText = true;
        _textServicesUpdatedSinceKeyDown = true;
    }

    /// <summary>The text to enter for a received character, or null when it enters nothing.</summary>
    public string? Character(char c)
    {
        if (_textServicesDeliverText || _textServicesUpdatedSinceKeyDown)
        {
            _pendingHighSurrogate = default;
            return null;
        }

        bool dropSpace = _dropSpaceEcho;
        _dropSpaceEcho = false;
        if (c == ' ' && dropSpace) return null;

        if (char.IsHighSurrogate(c))
        {
            _pendingHighSurrogate = c;
            return null;
        }

        char high = _pendingHighSurrogate;
        _pendingHighSurrogate = default;
        if (char.IsLowSurrogate(c))
            return high != default ? new string(new[] { high, c }) : null;

        return char.IsControl(c) ? null : c.ToString();
    }
}
