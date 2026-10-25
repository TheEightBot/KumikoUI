using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Combo box selection changed event args.</summary>
public class ComboBoxSelectionChangedEventArgs : EventArgs
{
    /// <summary>The previously selected item, or <see langword="null"/> if none was selected.</summary>
    public DrawnComboBoxItem? OldItem { get; }
    /// <summary>The newly selected item, or <see langword="null"/> if the selection was cleared.</summary>
    public DrawnComboBoxItem? NewItem { get; }
    /// <summary>Initializes a new instance of the <see cref="ComboBoxSelectionChangedEventArgs"/> class.</summary>
    /// <param name="old">The previously selected item.</param>
    /// <param name="new">The newly selected item.</param>
    public ComboBoxSelectionChangedEventArgs(DrawnComboBoxItem? old, DrawnComboBoxItem? @new)
    {
        OldItem = old;
        NewItem = @new;
    }
}
