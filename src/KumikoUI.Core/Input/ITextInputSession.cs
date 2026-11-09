namespace KumikoUI.Core.Input;

/// <summary>
/// The text-editing surface of a drawn editor that a host's native text-input adapter drives
/// (UITextInput on iOS and Mac Catalyst, an InputConnection on Android, CoreTextEditContext on Windows,
/// composition events on Uno). The adapter reports what the platform's keyboard or input method did;
/// the editor owns the text, the selection, the undo history and the drawing.
/// </summary>
/// <remarks>
/// <para>
/// Indices are UTF-16 code units into <see cref="Text"/>. The editor keeps its caret and selection on
/// grapheme-cluster boundaries (an emoji sequence or a letter with combining marks is one unit), so an
/// index the adapter sets inside a cluster moves to the start of that cluster.
/// </para>
/// <para>
/// Composition (marked text) is how input methods build text before committing it, for example a
/// Japanese reading before conversion or a dead-key accent. <see cref="SetComposition"/> replaces the
/// current composition (or the selection, when none is active) with the marked text, which
/// <see cref="Text"/> includes and the editor underlines. <see cref="CommitComposition"/> or
/// <see cref="InsertText"/> turns it into ordinary text, applying the editor's input rules (numeric
/// filtering, mask, validator, maximum length) to the result and recording one undo step;
/// <see cref="CancelComposition"/> restores the text and selection from before the composition began.
/// </para>
/// <para>
/// <see cref="TextInputStateChanged"/> is raised when the text, selection or composition change by any
/// route other than the adapter's own calls on this interface (keyboard shortcuts, pointer gestures,
/// undo, a programmatic change), so the adapter can update the platform's view of the text, for example
/// by resetting the input method.
/// </para>
/// </remarks>
public interface ITextInputSession
{
    /// <summary>The current text, including any composition.</summary>
    string Text { get; }

    /// <summary>The selection; empty at the caret when nothing is selected.</summary>
    KumikoTextRange Selection { get; }

    /// <summary>The caret index (the active end of the selection).</summary>
    int CursorPosition { get; }

    /// <summary>The composition (marked text) range, or null when no composition is active.</summary>
    KumikoTextRange? Composition { get; }

    /// <summary>Whether the text can be changed; when true the editing members do nothing.</summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Raised when the text, selection or composition change other than through this interface.
    /// </summary>
    event EventHandler? TextInputStateChanged;

    /// <summary>
    /// Insert committed text, replacing the composition if one is active, otherwise the selection.
    /// </summary>
    void InsertText(string text);

    /// <summary>
    /// Set the composition (marked text), replacing the current composition, or the selection when none
    /// is active. An empty <paramref name="text"/> cancels the composition.
    /// </summary>
    /// <param name="text">The marked text.</param>
    /// <param name="cursorInComposition">The caret position within <paramref name="text"/>.</param>
    void SetComposition(string text, int cursorInComposition);

    /// <summary>Commit the composition as ordinary text. Does nothing when no composition is active.</summary>
    void CommitComposition();

    /// <summary>
    /// Remove the composition and restore the text and selection from before it began. Does nothing when
    /// no composition is active.
    /// </summary>
    void CancelComposition();

    /// <summary>Delete the selection, or the grapheme cluster before the caret.</summary>
    void DeleteBackward();

    /// <summary>Delete the selection, or the grapheme cluster after the caret.</summary>
    void DeleteForward();

    /// <summary>
    /// Set the selection from <paramref name="anchor"/> to <paramref name="active"/> (the caret), in either
    /// order; equal values place the caret.
    /// </summary>
    void SetSelection(int anchor, int active);
}
