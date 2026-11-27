using KumikoUI.Core.Layout;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

// Row geometry (#320): the source owns the layout of its rows, so every consumer that receives the source agrees on
// where each row is, and the layout hears about view changes before any ViewChanged listener asks for geometry.
public partial class KumikoSource
{
    private KumikoRowLayout? _rowLayout;

    /// <summary>
    /// Where each row of the view is vertically: row tops, heights, the row at a y and the total height (see
    /// <see cref="KumikoRowLayout"/>). Every row-geometry calculation goes through it.
    /// </summary>
    internal KumikoRowLayout RowLayout => _rowLayout ??= new KumikoRowLayout(this);

    /// <summary>
    /// Re-measures the height of every row the next time it is drawn, for row heights that depend on something the
    /// grid cannot observe (what a <see cref="KumikoStyle.RowHeightResolver"/> or <see cref="KumikoStyle.CellStyleResolver"/>
    /// reads). Heights already measured stay in use until then. Raises a redraw. Only matters when
    /// <see cref="KumikoStyle.RowHeightMode"/> is <see cref="KumikoRowHeightMode.Auto"/> or a resolver is set.
    /// </summary>
    public void InvalidateRowHeights()
    {
        VerifyAccess();
        _rowLayout?.InvalidateAll();
        NotifyRedraw();
    }

    /// <summary>
    /// Re-measures the height of the row that shows <paramref name="item"/> the next time it is drawn (see
    /// <see cref="InvalidateRowHeights"/>). A change the item raises through <c>PropertyChanged</c> re-measures its row
    /// without this call. Raises a redraw.
    /// </summary>
    /// <param name="item">The item whose row height may have changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
    public void InvalidateRowHeight(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        VerifyAccess();
        _rowLayout?.InvalidateItem(item);
        NotifyRedraw();
    }

    /// <summary>
    /// Keeps the layout in step with a collection change: rows inserted or removed without a rebuild shift the
    /// per-row heights (a rebuild re-maps them through <see cref="KumikoRowLayout.OnViewRebuilt"/>), and removed items
    /// lose their measured height.
    /// </summary>
    private void OnRowLayoutCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e,
        bool incremental, int viewIndex, int count, bool rowsChanged)
    {
        var layout = _rowLayout;
        if (layout == null) return;
        if (e.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove
            or System.Collections.Specialized.NotifyCollectionChangedAction.Replace && e.OldItems != null)
        {
            foreach (var item in e.OldItems!)
                if (item != null) layout.ForgetItem(item);
        }
        if (!incremental || !rowsChanged || viewIndex < 0) return;
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            layout.OnRowsInserted(viewIndex, count);
        else
            layout.OnRowsRemoved(viewIndex, count);
    }

    /// <summary>
    /// An item's property changed (or a cell value was set): re-measure its row when the change can affect its height,
    /// that is when a resolver is set (it may read anything) or a measured column shows the property.
    /// </summary>
    private void InvalidateRowHeightOf(object item, string? propertyName)
    {
        var layout = _rowLayout;
        if (layout == null || !layout.IsActive) return;
        if (!layout.HasResolver && !string.IsNullOrEmpty(propertyName))
        {
            bool affected = false;
            foreach (var col in _columns.Where(c => c.TextWrap != KumikoTextWrap.None || c.CustomCellRenderer is IMeasurableCellRenderer))
            {
                if (col.ValueAccessor != null || string.IsNullOrEmpty(col.PropertyName) ||
                    PropertyPathAffected(col.PropertyName, propertyName))
                {
                    affected = true;
                    break;
                }
            }
            if (!affected) return;
        }
        layout.InvalidateItem(item);
    }
}
