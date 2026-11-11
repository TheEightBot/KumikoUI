namespace KumikoUI.Core.Input;

/// <summary>
/// The system clipboard as the grid sees it: plain text in and out. KumikoUI.Core is platform-free, so each
/// host (MAUI, Uno) supplies the implementation and assigns it to the grid's <c>Clipboard</c> property; the
/// grid uses it for Ctrl/Cmd+C on the selected rows and for copy, cut and paste inside cell editors and the
/// filter popup's search box. With no clipboard assigned, none of those do anything.
/// </summary>
public interface IKumikoClipboard
{
    /// <summary>Replaces the clipboard content with <paramref name="text"/>.</summary>
    /// <param name="text">The text to place on the clipboard.</param>
    /// <returns>
    /// <c>true</c> when the text was placed; <c>false</c> when the clipboard refused it (no access, no
    /// platform support). A cut deletes the selected text only when this returned <c>true</c>.
    /// </returns>
    bool TrySetText(string text);

    /// <summary>Reads the clipboard's text.</summary>
    /// <returns>A task for the text; <c>null</c> or empty when the clipboard holds no text or cannot be read.</returns>
    /// <remarks>
    /// A task that is already complete when returned is used without leaving the key press; a pending one
    /// delivers the paste when it completes, on the context the grid called from (the UI thread).
    /// </remarks>
    Task<string?> GetTextAsync();
}
