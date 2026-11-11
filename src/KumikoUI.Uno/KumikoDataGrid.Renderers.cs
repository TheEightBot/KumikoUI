using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Uno;

/// <summary>
/// Cell renderer registration for <see cref="KumikoDataGrid"/>. Same name and behavior as the MAUI control.
/// </summary>
public partial class KumikoDataGrid
{
    /// <summary>
    /// Registers <paramref name="renderer"/> as the cell renderer for every column of
    /// <paramref name="columnType"/> that has no <see cref="KumikoColumn.CustomCellRenderer"/> of its own,
    /// replacing the built-in renderer for that type, and repaints the grid. To change a single column, set
    /// its <see cref="KumikoColumn.CustomCellRenderer"/> instead, which takes precedence over this.
    /// </summary>
    /// <param name="columnType">The column type whose cells the renderer draws.</param>
    /// <param name="renderer">The renderer to use. Must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is null.</exception>
    public void SetCellRenderer(KumikoColumnType columnType, ICellRenderer renderer)
    {
        _renderer.SetCellRenderer(columnType, renderer);
        Invalidate();
    }
}
