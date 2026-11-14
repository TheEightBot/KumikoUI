using System.Diagnostics.CodeAnalysis;

namespace KumikoUI.Core.Models;

/// <summary>
/// Selection mode for the grid.
/// </summary>
public enum KumikoSelectionMode
{
    /// <summary>No selection allowed.</summary>
    None,
    /// <summary>Single row or cell selection.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name",
        Justification = "Single is the selection mode's standard name (WinUI ListViewSelectionMode.Single, MAUI SelectionMode.Single), not the System.Single type.")]
    Single,
    /// <summary>Multiple selections via Ctrl+Click.</summary>
    Multiple,
    /// <summary>Extended selection: single click = single, Shift = range, Ctrl = toggle.</summary>
    Extended
}
