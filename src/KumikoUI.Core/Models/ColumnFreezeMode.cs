using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines how a column is frozen (pinned).
/// </summary>
public enum ColumnFreezeMode
{
    /// <summary>Column scrolls normally.</summary>
    None,
    /// <summary>Column is pinned to the left edge.</summary>
    Left,
    /// <summary>Column is pinned to the right edge.</summary>
    Right
}
