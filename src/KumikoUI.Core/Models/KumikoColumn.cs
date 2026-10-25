using System.Collections;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;

namespace KumikoUI.Core.Models;

/// <summary>
/// Platform-independent column definition.
/// </summary>
public class KumikoColumn
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Display header text.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// Property path on the data item (supports nested: "Address.City"; empty means the item itself).
    /// Resolved through <see cref="Data.KumikoAccessorRegistry"/>: accessors generated for types marked
    /// <see cref="Data.KumikoGridItemAttribute"/>, registered providers, then reflection where it is
    /// supported. <see cref="ValueAccessor"/> and <see cref="ValueSetter"/> take precedence.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Reads the cell value from a row item. When set, it is used instead of <see cref="PropertyName"/>
    /// for display, sorting, grouping, filtering and summaries. Reflection-free, so it works under
    /// Native AOT and full trimming.
    /// </summary>
    public Func<object, object?>? ValueAccessor { get; set; }

    /// <summary>
    /// Writes an edited value back to a row item. When set, it is used instead of
    /// <see cref="PropertyName"/>; the editor's value is passed as is, without conversion.
    /// Reflection-free, so it works under Native AOT and full trimming.
    /// </summary>
    public Action<object, object?>? ValueSetter { get; set; }

    // The path resolved for the last item type seen, so a cell read costs no registry lookup.
    private Data.KumikoValuePath? _resolvedPath;

    /// <summary>Resolves <see cref="PropertyName"/> for <paramref name="itemType"/>, caching the last result.</summary>
    internal Data.KumikoValuePath ResolvePath(Type itemType)
    {
        var path = _resolvedPath;
        if (path == null || !path.IsCurrentFor(itemType, PropertyName))
            _resolvedPath = path = Data.KumikoAccessorRegistry.GetPath(itemType, PropertyName);
        return path;
    }

    /// <summary>Type of column for rendering dispatch.</summary>
    public KumikoColumnType ColumnType { get; set; } = KumikoColumnType.Text;

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
    public KumikoSortDirection SortDirection { get; set; } = KumikoSortDirection.None;

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
    public Rendering.KumikoTextAlignment TextAlignment { get; set; } = Rendering.KumikoTextAlignment.Left;

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
    /// Returns the display text of a ComboBox editor item. Takes precedence over
    /// <see cref="EditorDisplayMemberPath"/>; reflection-free.
    /// </summary>
    public Func<object, string?>? EditorDisplaySelector { get; set; }

    /// <summary>
    /// Returns the value of a ComboBox editor item. Takes precedence over
    /// <see cref="EditorValueMemberPath"/>; reflection-free.
    /// </summary>
    public Func<object, object?>? EditorValueSelector { get; set; }

    /// <summary>
    /// Custom editor factory for Template columns.
    /// Receives (currentValue, cellBounds) and returns a DrawnComponent editor.
    /// </summary>
    public Func<object?, Rendering.KumikoRect, Components.DrawnComponent?>? CustomEditorFactory { get; set; }

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
    /// Null properties fall back to the grid-level <see cref="KumikoStyle"/>.
    /// </summary>
    public KumikoCellStyle? CellStyle { get; set; }

    /// <summary>
    /// Style overrides for the header cell of this column.
    /// Null properties fall back to the grid-level <see cref="KumikoStyle"/>.
    /// </summary>
    public KumikoCellStyle? HeaderCellStyle { get; set; }
}
