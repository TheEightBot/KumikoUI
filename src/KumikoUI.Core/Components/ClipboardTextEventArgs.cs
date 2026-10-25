using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Clipboard text event (for copy/cut — host reads the Text).</summary>
public class ClipboardTextEventArgs : EventArgs
{
    /// <summary>The text to be placed on the clipboard.</summary>
    public string Text { get; }
    /// <summary>Initializes a new instance of the <see cref="ClipboardTextEventArgs"/> class.</summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    public ClipboardTextEventArgs(string text) { Text = text; }
}
