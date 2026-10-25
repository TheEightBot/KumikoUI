using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Sort direction used by columns and the data source.
/// </summary>
public enum DataGridSortDirection
{
    /// <summary>No sorting applied.</summary>
    None,
    /// <summary>Sorted in ascending order (A–Z, 0–9).</summary>
    Ascending,
    /// <summary>Sorted in descending order (Z–A, 9–0).</summary>
    Descending
}
