using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Picker selection changed event args.</summary>
public class PickerSelectionChangedEventArgs : EventArgs
{
    /// <summary>Index of the currently selected item.</summary>
    public int SelectedIndex { get; }
    /// <summary>Initializes a new instance of the <see cref="PickerSelectionChangedEventArgs"/> class.</summary>
    /// <param name="index">The index of the selected item.</param>
    public PickerSelectionChangedEventArgs(int index) { SelectedIndex = index; }
}
