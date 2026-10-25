using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Text changed event args.</summary>
public class GridTextChangedEventArgs : EventArgs
{
    /// <summary>The text value before the change.</summary>
    public string OldText { get; }
    /// <summary>The text value after the change.</summary>
    public string NewText { get; }
    /// <summary>Initializes a new instance of the <see cref="GridTextChangedEventArgs"/> class.</summary>
    /// <param name="oldText">The previous text value.</param>
    /// <param name="newText">The new text value.</param>
    public GridTextChangedEventArgs(string oldText, string newText) { OldText = oldText; NewText = newText; }
}
