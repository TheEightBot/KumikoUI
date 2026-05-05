using System.Collections;
using System.ComponentModel;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Defines column types for data binding and rendering dispatch.
/// </summary>
public enum DataGridColumnType
{
    /// <summary>Plain text column.</summary>
    Text,
    /// <summary>Numeric column with numeric formatting.</summary>
    Numeric,
    /// <summary>Boolean column rendered as a checkbox.</summary>
    Boolean,
    /// <summary>Date/time column with date formatting.</summary>
    Date,
    /// <summary>Custom template column.</summary>
    Template,
    /// <summary>Image column.</summary>
    Image,
    /// <summary>Drop-down combo box column.</summary>
    ComboBox,
    /// <summary>Scroll-picker column.</summary>
    Picker
}

/// <summary>
/// Sort direction used by columns and the data source.
/// </summary>
public enum SortDirection
{
    /// <summary>No sorting applied.</summary>
    None,
    /// <summary>Sorted in ascending order (A–Z, 0–9).</summary>
    Ascending,
    /// <summary>Sorted in descending order (Z–A, 9–0).</summary>
    Descending
}

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

/// <summary>
/// Platform-independent column definition.
/// </summary>
public class DataGridColumn
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Display header text.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Property path on the data item (supports nested: "Address.City").</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>Type of column for rendering dispatch.</summary>
    public DataGridColumnType ColumnType { get; set; } = DataGridColumnType.Text;

    /// <summary>Current width in pixels (after layout).</summary>
    public float Width { get; set; } = 120f;

    /// <summary>Minimum allowed width.</summary>
    public float MinWidth { get; set; } = 40f;

    /// <summary>Maximum allowed width.</summary>
    public float MaxWidth { get; set; } = float.MaxValue;

    /// <summary>Sizing mode for layout calculation.</summary>
    public ColumnSizeMode SizeMode { get; set; } = ColumnSizeMode.Fixed;

    /// <summary>Star weight when SizeMode == Star.</summary>
    public float StarWeight { get; set; } = 1f;

    /// <summary>Is the column visible?</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Is the column read-only?</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// When true, Tab navigation will stop on this column's cells during editing.
    /// When false, Tab will skip this column. Default: true.
    /// </summary>
    public bool AllowTabStop { get; set; } = true;

    /// <summary>Can this column be sorted?</summary>
    public bool AllowSorting { get; set; } = true;

    /// <summary>Can this column be resized?</summary>
    public bool AllowResize { get; set; } = true;

    /// <summary>Can this column be reordered via drag?</summary>
    public bool AllowReorder { get; set; } = true;

    /// <summary>Can this column be filtered?</summary>
    public bool AllowFiltering { get; set; } = true;

    /// <summary>Active filter for this column. Null means no filter.</summary>
    public FilterDescription? ActiveFilter { get; set; }

    /// <summary>
    /// When true, the column is frozen (pinned) to the left edge
    /// and does not scroll horizontally with the rest of the grid.
    /// Setting this to true sets FreezeMode to Left; false sets it to None.
    /// </summary>
    public bool IsFrozen
    {
        get => FreezeMode == ColumnFreezeMode.Left;
        set => FreezeMode = value ? ColumnFreezeMode.Left : ColumnFreezeMode.None;
    }

    /// <summary>
    /// When true, the column is frozen (pinned) to the right edge.
    /// Setting this to true sets FreezeMode to Right; false sets it to None.
    /// </summary>
    public bool IsFrozenRight
    {
        get => FreezeMode == ColumnFreezeMode.Right;
        set => FreezeMode = value ? ColumnFreezeMode.Right : ColumnFreezeMode.None;
    }

    /// <summary>
    /// Controls how the column is frozen (pinned). None = scrollable, Left = pinned left, Right = pinned right.
    /// </summary>
    public ColumnFreezeMode FreezeMode { get; set; } = ColumnFreezeMode.None;

    /// <summary>Current sort direction.</summary>
    public SortDirection SortDirection { get; set; } = SortDirection.None;

    /// <summary>
    /// Sort priority in multi-sort mode. 0 = not part of multi-sort.
    /// 1 = primary sort, 2 = secondary, etc.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Optional custom comparer for sorting this column.
    /// When set, this is used instead of the default Comparer&lt;object&gt;.Default.
    /// </summary>
    public IComparer? CustomComparer { get; set; }

    /// <summary>Display format for the cell value (e.g., "C2", "yyyy-MM-dd").</summary>
    public string? Format { get; set; }

    /// <summary>Horizontal alignment of cell content.</summary>
    public Rendering.GridTextAlignment TextAlignment { get; set; } = Rendering.GridTextAlignment.Left;

    // ── Editor configuration ─────────────────────────────────────

    /// <summary>
    /// Items for ComboBox or Picker column editors.
    /// For ComboBox: can be any IEnumerable (uses DisplayMemberPath/ValueMemberPath).
    /// For Picker: typically IEnumerable&lt;string&gt;.
    /// Can also be populated automatically via <see cref="EditorItemsString"/>.
    /// </summary>
    public IEnumerable? EditorItems { get; set; }

    /// <summary>
    /// XAML-friendly comma-separated list of editor items.
    /// Setting this automatically populates <see cref="EditorItems"/> with a string array.
    /// Use for simple string-based Picker or ComboBox items without code-behind.
    /// Example: EditorItemsString="Junior,Mid,Senior,Staff,Principal,Director"
    /// </summary>
    public string? EditorItemsString
    {
        get => _editorItemsString;
        set
        {
            _editorItemsString = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                EditorItems = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }
        }
    }
    private string? _editorItemsString;

    /// <summary>Property path for display text in ComboBox editor items.</summary>
    public string? EditorDisplayMemberPath { get; set; }

    /// <summary>Property path for value in ComboBox editor items.</summary>
    public string? EditorValueMemberPath { get; set; }

    /// <summary>
    /// Custom editor factory for Template columns.
    /// Receives (currentValue, cellBounds) and returns a DrawnComponent editor.
    /// </summary>
    public Func<object?, Rendering.GridRect, Components.DrawnComponent?>? CustomEditorFactory { get; set; }

    /// <summary>
    /// XAML-declarable editor descriptor for Template columns.
    /// When set and <see cref="CustomEditorFactory"/> is null, this descriptor
    /// is used to create editors. Allows full XAML configuration of editor
    /// properties without code-behind.
    /// </summary>
    public EditorDescriptor? EditorDescriptor { get; set; }

    /// <summary>
    /// Optional per-column cell renderer for display mode.
    /// When set, this renderer is used instead of the default type-based renderer.
    /// Use for Template columns that need custom display rendering (e.g., progress bars, sparklines).
    /// Can be set declaratively in XAML.
    /// </summary>
    public Rendering.ICellRenderer? CustomCellRenderer { get; set; }

    // ── Per-column editing behaviour ───────────────────────────────

    /// <summary>
    /// Optional column-level edit trigger override.
    /// When set, this column uses its own <see cref="Editing.EditTrigger"/> flags instead of
    /// the grid-level <c>EditSession.EditTriggers</c> value.
    /// Set to <see cref="Editing.EditTrigger.None"/> to disable all trigger-based editing for
    /// this column without making it fully read-only via <see cref="IsReadOnly"/>.
    /// <c>null</c> (default) means inherit from the grid-level setting.
    /// </summary>
    public Editing.EditTrigger? EditTriggers { get; set; }

    // ── Per-column styling ─────────────────────────────────────────

    /// <summary>
    /// Style overrides for data cells in this column.
    /// Null properties fall back to the grid-level <see cref="DataGridStyle"/>.
    /// </summary>
    public CellStyle? CellStyle { get; set; }

    /// <summary>
    /// Style overrides for the header cell of this column.
    /// Null properties fall back to the grid-level <see cref="DataGridStyle"/>.
    /// </summary>
    public CellStyle? HeaderCellStyle { get; set; }
}
