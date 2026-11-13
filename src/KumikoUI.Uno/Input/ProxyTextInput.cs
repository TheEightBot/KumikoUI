using KumikoUI.Core.Input;

namespace KumikoUI.Uno.Input;

/// <summary>
/// Pure helpers for the hidden keyboard-proxy <c>TextBox</c>: finding what an edit of the proxy text changed,
/// and turning inserted text into the key and text events sent to KumikoUI.Core. Kept free of UI types so it
/// can be unit-tested; <see cref="KumikoTextInputAdapter"/> uses them.
/// </summary>
internal static class ProxyTextInput
{
    /// <summary>One event to dispatch to Core: a command key, or a typed character.</summary>
    internal readonly record struct Dispatch(KumikoKey Key, char? Character);

    /// <summary>
    /// One edit of the proxy text: <see cref="RemovedLength"/> UTF-16 code units at <see cref="Start"/> were
    /// replaced by <see cref="Inserted"/>.
    /// </summary>
    internal readonly record struct TextChange(int Start, int RemovedLength, string Inserted);

    /// <summary>
    /// Finds the single edit that turns <paramref name="oldText"/> into <paramref name="newText"/>, given the
    /// selection in <paramref name="oldText"/> before the edit. The edits a keyboard makes are preferred: typing
    /// over the selection, Backspace before the caret, Delete after it; so typing a letter next to the same letter
    /// lands at the caret. Otherwise the edit is what lies between the longest common prefix and suffix. A surrogate
    /// pair is never split between the edit and the text around it.
    /// </summary>
    internal static TextChange Diff(string oldText, string newText, int selectionStart, int selectionLength)
    {
        int start = Math.Clamp(selectionStart, 0, oldText.Length);
        int selected = Math.Clamp(selectionLength, 0, oldText.Length - start);

        // Typing over the selection (or at the caret).
        int inserted = newText.Length - oldText.Length + selected;
        if (inserted >= 0 && inserted + selected > 0 && IsEditAt(oldText, newText, start, selected, inserted))
            return new TextChange(start, selected, newText.Substring(start, inserted));

        if (selected == 0 && newText.Length < oldText.Length)
        {
            int removed = oldText.Length - newText.Length;
            if (start - removed >= 0 && IsEditAt(oldText, newText, start - removed, removed, 0))
                return new TextChange(start - removed, removed, string.Empty); // Backspace
            if (start + removed <= oldText.Length && IsEditAt(oldText, newText, start, removed, 0))
                return new TextChange(start, removed, string.Empty); // Delete
        }

        int max = Math.Min(oldText.Length, newText.Length);
        int prefix = 0;
        while (prefix < max && oldText[prefix] == newText[prefix]) prefix++;
        if (SplitsSurrogatePair(oldText, prefix) || SplitsSurrogatePair(newText, prefix)) prefix--;

        int suffix = 0;
        while (suffix < max - prefix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix]) suffix++;
        if (suffix > 0 &&
            (SplitsSurrogatePair(oldText, oldText.Length - suffix) || SplitsSurrogatePair(newText, newText.Length - suffix)))
            suffix--;

        return new TextChange(prefix, oldText.Length - suffix - prefix,
            newText.Substring(prefix, newText.Length - suffix - prefix));
    }

    /// <summary>
    /// Whether <paramref name="newText"/> is <paramref name="oldText"/> with <paramref name="removed"/> units at
    /// <paramref name="start"/> replaced by <paramref name="inserted"/> units, without splitting a surrogate pair.
    /// </summary>
    private static bool IsEditAt(string oldText, string newText, int start, int removed, int inserted) =>
        string.CompareOrdinal(oldText, 0, newText, 0, start) == 0 &&
        string.CompareOrdinal(oldText, start + removed, newText, start + inserted, oldText.Length - start - removed) == 0 &&
        !SplitsSurrogatePair(oldText, start) && !SplitsSurrogatePair(oldText, start + removed) &&
        !SplitsSurrogatePair(newText, start) && !SplitsSurrogatePair(newText, start + inserted);

    /// <summary>Whether <paramref name="index"/> falls between the two halves of a surrogate pair.</summary>
    private static bool SplitsSurrogatePair(string text, int index) =>
        index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);

    /// <summary>
    /// True for characters in the Unicode private-use block U+F700–U+F8FF, which AppKit uses to encode
    /// function and navigation keys (F1–F35, arrows, Home/End, Page Up/Down, Delete…). They are never
    /// text, and on macOS some of them reach the proxy as "typed" characters.
    /// </summary>
    internal static bool IsFunctionKeyCharacter(char ch) => ch >= '' && ch <= '';

    /// <summary>
    /// Plans the Core events for <paramref name="inserted"/> proxy text.
    /// <paramref name="keyHandledOnKeyDown"/> is the command key that the proxy's KeyDown already sent
    /// to Core for this keystroke (Tab or Enter), or <see cref="KumikoKey.None"/>: its text form
    /// (<c>\t</c>, <c>\n</c>/<c>\r</c>) is dropped once so the key is not delivered twice. Function-key
    /// characters and other control characters are dropped; everything else is a typed character.
    /// </summary>
    internal static List<Dispatch> Plan(string inserted, KumikoKey keyHandledOnKeyDown)
    {
        var result = new List<Dispatch>(inserted.Length);
        bool echoConsumed = false;

        foreach (char ch in inserted)
        {
            KumikoKey key = ch switch
            {
                '\n' or '\r' => KumikoKey.Enter,
                '\t' => KumikoKey.Tab,
                _ => KumikoKey.None,
            };

            if (key != KumikoKey.None)
            {
                // The TextBox echoed a command key that KeyDown already dispatched — skip it once.
                if (!echoConsumed && key == keyHandledOnKeyDown)
                {
                    echoConsumed = true;
                    continue;
                }
                // "\r\n" from a single Return is one Enter.
                if (ch == '\n' && result.Count > 0 && result[^1].Key == KumikoKey.Enter && result[^1].Character == '\r')
                    continue;
                result.Add(new Dispatch(key, ch));
                continue;
            }

            if (IsFunctionKeyCharacter(ch) || char.IsControl(ch))
                continue;

            result.Add(new Dispatch(KumikoKey.Character, ch));
        }

        return result;
    }
}
