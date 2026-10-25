namespace KumikoUI.Core.Models;

/// <summary>
/// Selection mode for the grid.
/// </summary>
public enum GridSelectionMode
{
    /// <summary>No selection allowed.</summary>
    None,
    /// <summary>Single row or cell selection.</summary>
    Single,
    /// <summary>Multiple selections via Ctrl+Click.</summary>
    Multiple,
    /// <summary>Extended selection: single click = single, Shift = range, Ctrl = toggle.</summary>
    Extended
}
