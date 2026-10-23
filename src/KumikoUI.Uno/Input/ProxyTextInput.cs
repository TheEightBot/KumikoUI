using KumikoUI.Core.Input;

namespace KumikoUI.Uno.Input;

/// <summary>
/// Pure translation of text that arrived through the hidden keyboard-proxy <c>TextBox</c> into the
/// key and character events sent to KumikoUI.Core. Kept free of UI types so it can be unit-tested.
/// </summary>
internal static class ProxyTextInput
{
    /// <summary>One event to dispatch to Core: a command key, or a typed character.</summary>
    internal readonly record struct Dispatch(GridKey Key, char? Character);

    /// <summary>
    /// Returns the text inserted between <paramref name="oldText"/> and <paramref name="newText"/>,
    /// with the sentinel removed. Handles plain appends and IME-style full replacements.
    /// </summary>
    internal static string ExtractInserted(string oldText, string newText, string sentinel)
    {
        if (newText.Length > oldText.Length && newText.StartsWith(oldText, StringComparison.Ordinal))
            return newText.Substring(oldText.Length);

        int sentinelIdx = newText.IndexOf(sentinel, StringComparison.Ordinal);
        return sentinelIdx >= 0 ? newText.Remove(sentinelIdx, sentinel.Length) : newText;
    }

    /// <summary>
    /// True for characters in the Unicode private-use block U+F700–U+F8FF, which AppKit uses to encode
    /// function and navigation keys (F1–F35, arrows, Home/End, Page Up/Down, Delete…). They are never
    /// text, and on macOS some of them reach the proxy as "typed" characters.
    /// </summary>
    internal static bool IsFunctionKeyCharacter(char ch) => ch >= '' && ch <= '';

    /// <summary>
    /// Plans the Core events for <paramref name="inserted"/> proxy text.
    /// <paramref name="keyHandledOnKeyDown"/> is the command key that the proxy's KeyDown already sent
    /// to Core for this keystroke (Tab or Enter), or <see cref="GridKey.None"/>: its text form
    /// (<c>\t</c>, <c>\n</c>/<c>\r</c>) is dropped once so the key is not delivered twice. Function-key
    /// characters and other control characters are dropped; everything else is a typed character.
    /// </summary>
    internal static List<Dispatch> Plan(string inserted, GridKey keyHandledOnKeyDown)
    {
        var result = new List<Dispatch>(inserted.Length);
        bool echoConsumed = false;

        foreach (char ch in inserted)
        {
            GridKey key = ch switch
            {
                '\n' or '\r' => GridKey.Enter,
                '\t' => GridKey.Tab,
                _ => GridKey.None,
            };

            if (key != GridKey.None)
            {
                // The TextBox echoed a command key that KeyDown already dispatched — skip it once.
                if (!echoConsumed && key == keyHandledOnKeyDown)
                {
                    echoConsumed = true;
                    continue;
                }
                // "\r\n" from a single Return is one Enter.
                if (ch == '\n' && result.Count > 0 && result[^1].Key == GridKey.Enter && result[^1].Character == '\r')
                    continue;
                result.Add(new Dispatch(key, ch));
                continue;
            }

            if (IsFunctionKeyCharacter(ch) || char.IsControl(ch))
                continue;

            result.Add(new Dispatch(GridKey.Character, ch));
        }

        return result;
    }
}
