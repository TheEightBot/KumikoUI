using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Check state for a checkbox.</summary>
internal enum DrawnCheckState
{
    /// <summary>The checkbox is unchecked.</summary>
    Unchecked,
    /// <summary>The checkbox is checked.</summary>
    Checked,
    /// <summary>The checkbox is in an indeterminate (tri-state) state.</summary>
    Indeterminate
}
