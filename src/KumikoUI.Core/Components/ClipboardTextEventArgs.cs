using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Clipboard text event (for copy/cut — host reads the Text).</summary>
public sealed class ClipboardTextEventArgs : EventArgs
{
    /// <summary>The text to be placed on the clipboard.</summary>
    public string Text { get; }

    /// <summary>
    /// Set to <c>true</c> by the host once it has put <see cref="Text"/> on the clipboard. A cut deletes the
    /// selected text only when this is set, so text is never lost to a clipboard that did not take it.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>Initializes a new instance of the <see cref="ClipboardTextEventArgs"/> class.</summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    public ClipboardTextEventArgs(string text) { Text = text; }
}
