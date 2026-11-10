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
    public abstract DrawnComponent? CreateEditor(object? currentValue, KumikoRect cellBounds);

    /// <summary>
    /// Create an editor component for the cell described by <paramref name="context"/>, which also carries
    /// the character typed to open the edit. The grid calls this overload; the default forwards to
    /// <see cref="CreateEditor(object?, KumikoRect)"/>, so an existing descriptor keeps working. Override
    /// it to honor <see cref="CellEditorContext.InitialCharacter"/>.
    /// </summary>
    public virtual DrawnComponent? CreateEditor(CellEditorContext context) =>
        CreateEditor(context.Value, context.Bounds);
}
