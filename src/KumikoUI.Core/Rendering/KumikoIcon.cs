namespace KumikoUI.Core.Rendering;

/// <summary>
/// The built-in glyphs of the grid and its drawn components. Every one is drawn by the
/// <see cref="IKumikoIconProvider"/> of the grid's <see cref="Models.KumikoStyle.IconProvider"/> (or a
/// component's <see cref="Components.DrawnComponent.IconProvider"/>), so an app can replace any of them.
/// </summary>
/// <remarks>
/// Each member names a slot, not a shape: two slots that look alike by default (the group chip separator and the
/// collapsed group chevron, for example) can be given different glyphs. Members are only ever added, so a provider
/// should draw nothing, or fall back, for a value it does not know.
/// </remarks>
public enum KumikoIcon
{
    /// <summary>The disclosure glyph of an expanded group header. Default: a triangle pointing down.</summary>
    GroupExpanded,

    /// <summary>The disclosure glyph of a collapsed group header. Default: a triangle pointing right.</summary>
    GroupCollapsed,

    /// <summary>
    /// Ascending sort: the column header's sort indicator, a grouping chip's sort direction and the filter popup's
    /// ascending sort row. Default: a triangle pointing up.
    /// </summary>
    SortAscending,

    /// <summary>
    /// Descending sort: the column header's sort indicator, a grouping chip's sort direction and the filter popup's
    /// descending sort row. Default: a triangle pointing down.
    /// </summary>
    SortDescending,

    /// <summary>The column header's filter button while the column has no active filter. Default: a funnel outline.</summary>
    Filter,

    /// <summary>The column header's filter button while the column is filtered. Default: a solid funnel.</summary>
    FilterActive,

    /// <summary>The remove button of a grouping chip in the grouping panel. Default: an "×".</summary>
    Remove,

    /// <summary>The separator between two grouping chips (the group levels). Default: a chevron pointing right.</summary>
    GroupSeparator,

    /// <summary>The grip of the row drag handle. Default: two columns of three dots.</summary>
    DragHandle,

    /// <summary>
    /// The check mark of a checked check box: boolean cells, <see cref="Components.DrawnCheckBox"/> and the filter
    /// popup's value list. Drawn inside the box, over its fill. Default: a tick.
    /// </summary>
    Check,

    /// <summary>
    /// The mark of an indeterminate check box: a three-state boolean cell's null and
    /// <see cref="Components.DrawnCheckBox"/>. Default: a dash.
    /// </summary>
    Indeterminate,

    /// <summary>The dropdown button of a closed <see cref="Components.DrawnComboBox"/>. Default: a triangle pointing down.</summary>
    DropDown,

    /// <summary>The dropdown button of an open <see cref="Components.DrawnComboBox"/>. Default: a triangle pointing up.</summary>
    DropDownOpen,

    /// <summary>The increment button of <see cref="Components.DrawnNumericUpDown"/>. Default: a plus sign.</summary>
    Increment,

    /// <summary>The decrement button of <see cref="Components.DrawnNumericUpDown"/>. Default: a minus sign.</summary>
    Decrement,

    /// <summary>The calendar button of <see cref="Components.DrawnDatePicker"/>'s field. Default: a calendar page.</summary>
    Calendar,

    /// <summary>The previous-month button of <see cref="Components.DrawnDatePicker"/>'s calendar. Default: a chevron pointing left.</summary>
    CalendarPrevious,

    /// <summary>The next-month button of <see cref="Components.DrawnDatePicker"/>'s calendar. Default: a chevron pointing right.</summary>
    CalendarNext,

    /// <summary>
    /// The increase arrow above the hour and the minute of <see cref="Components.DrawnDatePicker"/>'s time picker.
    /// Default: a chevron pointing up.
    /// </summary>
    SpinUp,

    /// <summary>
    /// The decrease arrow below the hour and the minute of <see cref="Components.DrawnDatePicker"/>'s time picker.
    /// Default: a chevron pointing down.
    /// </summary>
    SpinDown,
}
