using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// A narrow column of row check boxes bound to the grid's <see cref="KumikoSelectionModel"/>, with a tri-state
/// check-all box in its header. It is the touch path to multi-select: no Ctrl or Shift is needed to add a row to
/// the selection. Declare it like any other column (in code, in XAML or through <c>SetColumns</c>), or set the
/// grid's <c>ShowSelectionColumn</c> to have the grid add one.
/// </summary>
/// <remarks>
/// <para>
/// The constructor applies the column's defaults: <see cref="KumikoColumnType.Selection"/>, a fixed width of 44
/// pixels (a touch-sized target), pinned to the left (<see cref="ColumnFreezeMode.Left"/>), read-only, centered, and
/// no sorting, filtering, searching, resizing or reordering. Tab skips it because it is read-only. Change the
/// freeze mode if you want it elsewhere.
/// </para>
/// <para>
/// The box of a row follows <see cref="KumikoSelectionModel.IsRowSelected"/>; what a tap on it does depends on
/// <see cref="KumikoSelectionModel.Mode"/> (see <see cref="KumikoSelectionModel.HandleCheckBoxToggle"/>). The header
/// box shows <see cref="KumikoSelectionModel.CheckAllState"/> and is hidden in
/// <see cref="KumikoSelectionMode.Single"/> and <see cref="KumikoSelectionMode.None"/> mode. The column is left
/// out of copy and of saved column layouts.
/// </para>
/// </remarks>
public class KumikoSelectionColumn : KumikoColumn
{
    /// <summary>The width of the column: a touch-sized 44 pixels.</summary>
    public const float DefaultSelectionWidth = 44f;

    /// <summary>Creates a selection column with the defaults described in the type's remarks.</summary>
    public KumikoSelectionColumn()
    {
        ColumnType = KumikoColumnType.Selection;
        SizeMode = ColumnSizeMode.Fixed;
        MinWidth = DefaultSelectionWidth;
        MaxWidth = DefaultSelectionWidth;
        Width = DefaultSelectionWidth;
        FreezeMode = ColumnFreezeMode.Left;
        IsReadOnly = true;
        AllowSorting = false;
        AllowFiltering = false;
        AllowSearching = false;
        AllowResize = false;
        AllowReorder = false;
        AllowTabStop = false;
        TextAlignment = KumikoTextAlignment.Center;
    }
}
