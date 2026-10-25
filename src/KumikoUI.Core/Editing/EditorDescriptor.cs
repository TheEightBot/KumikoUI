using KumikoUI.Core.Components;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Base class for XAML-declarable editor descriptors.
/// Subclasses configure how a cell editor is created without
/// requiring code-behind lambdas or factory delegates.
/// </summary>
public abstract class EditorDescriptor
{
    /// <summary>
    /// Create an editor component for the given value and cell bounds.
    /// </summary>
    public abstract DrawnComponent? CreateEditor(object? currentValue, GridRect cellBounds);
}
