using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Clipboard request (for paste — host fills in the Text).</summary>
public sealed class ClipboardRequestEventArgs : EventArgs
{
    /// <summary>The clipboard text provided by the host for paste operations.</summary>
    public string? Text { get; set; }

    /// <summary>
    /// Set to <c>true</c> by a host that reads the clipboard asynchronously and leaves <see cref="Text"/> empty:
    /// it delivers the text later through <see cref="DrawnTextBox.PasteText"/> (or the edit session's
    /// <c>PasteText</c>), and the request does nothing more.
    /// </summary>
    public bool IsPending { get; set; }
}
