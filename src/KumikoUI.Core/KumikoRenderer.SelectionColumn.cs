using KumikoUI.Core.Components;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core;

/// <summary>The selection column's header and the state its boxes are drawn from (#321).</summary>
public sealed partial class KumikoRenderer
{
    // The cell renderer of KumikoColumnType.Selection (a custom renderer set with SetCellRenderer replaces it)
    private readonly SelectionCellRenderer _selectionRenderer = new();

    // What the header box shows this frame: whether the mode has a check-all, and the state of the selection
    private bool _hasCheckAll;
    private bool? _checkAllState;

    /// <summary>Reads what the selection column draws from <paramref name="selection"/>; once per frame.</summary>
    private void BeginSelectionColumnFrame(KumikoSelectionModel selection)
    {
        _selectionRenderer.Mode = selection.Mode;
        _hasCheckAll = selection.HasCheckAll;
        _checkAllState = selection.CheckAllState;
    }

    /// <summary>
    /// Draws the check-all box of a selection column's header, centered in the column, instead of the header text; in
    /// the modes that have no check-all (<see cref="KumikoSelectionMode.Single"/>, <see cref="KumikoSelectionMode.None"/>)
    /// the header stays empty. Allocation-free.
    /// </summary>
    private void DrawSelectionHeader(IDrawingContext ctx, KumikoColumn col, float screenX, KumikoStyle style)
    {
        if (!_hasCheckAll) return;
        var box = CheckBoxGlyph.BoxBounds(new KumikoRect(screenX, 0, col.Width, style.HeaderHeight));
        var state = !_checkAllState.HasValue ? DrawnCheckState.Indeterminate
            : _checkAllState.Value ? DrawnCheckState.Checked
            : DrawnCheckState.Unchecked;
        CheckBoxGlyph.Draw(ctx, box, state, style, _paintCache);
    }
}
