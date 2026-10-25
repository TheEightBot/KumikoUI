using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Where to place a popup relative to its anchor.</summary>
public enum PopupPlacement
{
    /// <summary>Place the popup below the anchor.</summary>
    Below,
    /// <summary>Place the popup above the anchor.</summary>
    Above,
    /// <summary>Place the popup to the left of the anchor.</summary>
    Left,
    /// <summary>Place the popup to the right of the anchor.</summary>
    Right
}
