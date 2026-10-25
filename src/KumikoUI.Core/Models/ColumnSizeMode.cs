using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines how a column is sized.
/// </summary>
public enum ColumnSizeMode
{
    /// <summary>Fixed pixel width.</summary>
    Fixed,
    /// <summary>Weighted share of remaining space (star sizing).</summary>
    Star,
    /// <summary>Size to fit content (measure cells on first load).</summary>
    Auto
}
