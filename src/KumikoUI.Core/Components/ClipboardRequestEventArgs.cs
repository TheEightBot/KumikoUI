using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Clipboard request (for paste — host fills in the Text).</summary>
public class ClipboardRequestEventArgs : EventArgs
{
    /// <summary>The clipboard text provided by the host for paste operations.</summary>
    public string? Text { get; set; }
}
