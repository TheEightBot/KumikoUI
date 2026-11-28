using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Platform-independent column definition.
/// </summary>
/// <remarks>
/// A column has two kinds of properties. <b>Configuration</b> (<see cref="Header"/>, <see cref="IsVisible"/>,
/// <see cref="Width"/>, <see cref="MinWidth"/>, <see cref="MaxWidth"/>, <see cref="SizeMode"/>, <see cref="Format"/>,
/// <see cref="IsReadOnly"/>, <see cref="FreezeMode"/>, alignment, styles and the like) is yours to set at any time,
/// and the grid repaints and lays out again by itself: the column raises <see cref="PropertyChanged"/> and the
/// <see cref="KumikoSource"/> that holds it requests a redraw. <b>Runtime state</b> (<see cref="SortDirection"/>,
/// <see cref="SortOrder"/>, <see cref="ActiveFilter"/>, <see cref="HeaderFitWidth"/>, <see cref="ContentFitWidth"/>)
/// is owned by the grid: it has no public setter and is changed through <see cref="KumikoSource.SetSort"/> and
/// <see cref="KumikoSource.SetColumnFilter"/>. The editor settings (<c>Editor*</c> properties and editor factories)
/// are read when an editor opens and raise no notification.
/// </remarks>
public class KumikoColumn : INotifyPropertyChanged
{
    /// <summary>
    /// Raised after a configuration property changed; <see cref="PropertyChangedEventArgs.PropertyName"/> names it.
    /// Runtime state, such as <see cref="SortDirection"/>, and the width layout computes for a Star or Auto
    /// column do not raise it.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        RaisePropertyChanged(propertyName!);
        return true;
    }

    /// <summary>Display header text.</summary>
    public string Header
    {
        get => _header;
        set => SetField(ref _header, value);
    }
    private string _header = string.Empty;

    /// <summary>
    /// Property path on the data item (supports nested: "Address.City"; empty means the item itself).
    /// Resolved through <see cref="Data.KumikoAccessorRegistry"/>: accessors generated for types marked
    /// <see cref="Data.KumikoGridItemAttribute"/>, registered providers, then reflection where it is
    /// supported. <see cref="ValueAccessor"/> and <see cref="ValueSetter"/> take precedence.
    /// </summary>
    public string PropertyName
    {
        get => _propertyName;
        set => SetField(ref _propertyName, value);
    }
    private string _propertyName = string.Empty;

    /// <summary>
    /// Reads the cell value from a row item. When set, it is used instead of <see cref="PropertyName"/>
    /// for display, sorting, grouping, filtering and summaries. Reflection-free, so it works under
    /// Native AOT and full trimming.
    /// </summary>
    public Func<object, object?>? ValueAccessor
    {
        get => _valueAccessor;
        set => SetField(ref _valueAccessor, value);
    }
    private Func<object, object?>? _valueAccessor;

    /// <summary>
    /// Writes an edited value back to a row item. When set, it is used instead of
    /// <see cref="PropertyName"/>; the editor's value is passed as is, without conversion.
    /// Reflection-free, so it works under Native AOT and full trimming.
    /// </summary>
    public Action<object, object?>? ValueSetter
    {
        get => _valueSetter;
        set => SetField(ref _valueSetter, value);
    }
    private Action<object, object?>? _valueSetter;

    // The path resolved for the last item type seen, so a cell read costs no registry lookup.
    private Data.KumikoValuePath? _resolvedPath;

    // The display text of this column's drawn cells, cached by the source that draws them (#68).
    internal CellTextCache? TextCache;

    /// <summary>Resolves <see cref="PropertyName"/> for <paramref name="itemType"/>, caching the last result.</summary>
    internal Data.KumikoValuePath ResolvePath(Type itemType)
    {
        var path = _resolvedPath;
        if (path == null || !path.IsCurrentFor(itemType, PropertyName))
            _resolvedPath = path = Data.KumikoAccessorRegistry.GetPath(itemType, PropertyName);
        return path;
    }

    /// <summary>Type of column for rendering dispatch.</summary>
    public KumikoColumnType ColumnType
    {
        get => _columnType;
        set => SetField(ref _columnType, value);
    }
    private KumikoColumnType _columnType = KumikoColumnType.Text;

    /// <summary>The width a column starts with when <see cref="Width"/> is never set: 120 pixels.</summary>
    public const float DefaultWidth = 120f;

    /// <summary>
    /// Current width in pixels (after layout). Setting it, in code, in XAML, by dragging the column's resize
    /// grip or by auto-fit, makes the width explicit (<see cref="HasExplicitWidth"/>): a
    /// <see cref="ColumnSizeMode.Fixed"/> column is drawn at that width and a header that does not fit is truncated
    /// with an ellipsis. A column whose width was never set is at least wide enough for its whole header (see
    /// <see cref="HeaderFitWidth"/>), unless <see cref="MaxWidth"/> is smaller.
    /// </summary>
    /// <remarks>
    /// For <see cref="ColumnSizeMode.Star"/> and <see cref="ColumnSizeMode.Auto"/> columns the width is an output of
    /// layout: a Star column gets its share of the viewport and an Auto column its <see cref="ContentFitWidth"/> once
    /// measured, whatever is set here. To pin such a column's width, set <see cref="SizeMode"/> to
    /// <see cref="ColumnSizeMode.Fixed"/>; a resize drag or an auto-fit does that for you.
    /// </remarks>
    public float Width
    {
        get => _width;
        set
        {
            bool wasExplicit = HasExplicitWidth;
            HasExplicitWidth = true;
            if (!SetField(ref _width, value) && !wasExplicit)
                RaisePropertyChanged(nameof(Width)); // the explicit flag changed the header-fit minimum
        }
    }
    private float _width = DefaultWidth;

    /// <summary>
    /// True once <see cref="Width"/> has been set (in code, in XAML, by a resize drag or by auto-fit). An explicit
    /// width turns off the header-fit minimum: the column keeps its width and truncates a header that does not fit.
    /// </summary>
    public bool HasExplicitWidth { get; private set; }

    /// <summary>
    /// The narrowest width that shows the whole header: the header text measured in the header font, the header
    /// padding on both sides, room for the sort arrow when the column can be sorted and for the filter icon when
    /// it can be filtered, and a small margin. Zero until the renderer has measured the header. It is the minimum
    /// width of a column without an explicit <see cref="Width"/>; see <see cref="Layout.KumikoLayoutEngine"/>.
    /// </summary>
    public float HeaderFitWidth { get; internal set; }

    // The header text width last measured, and the text and font it was measured for (#182).
    internal readonly Layout.HeaderMeasurement HeaderMeasure = new();

    /// <summary>
    /// The width that fits the header and the widest sampled cell of a <see cref="ColumnSizeMode.Auto"/> column:
    /// the wider of <see cref="HeaderFitWidth"/> and the widest sampled cell's display text plus the cell padding on
    /// both sides and a small buffer, before <see cref="MinWidth"/> and <see cref="MaxWidth"/> are applied. Zero
    /// until the renderer has measured the column, and for columns of other size modes.
    /// </summary>
    /// <remarks>
    /// The renderer samples the cells on the first frame and again on the first frame after the data or the view
    /// changes (a rebuild, a collection change, a cell or item property change), or after the cell font changes; it
    /// measures the rows on screen plus up to <see cref="Layout.KumikoLayoutEngine.AutoFitSampleRows"/> rows at each
    /// end of the view, never every row. Scrolling alone does not measure again, so the column keeps its width while
    /// the grid scrolls.
    /// </remarks>
    public float ContentFitWidth { get; internal set; }

    // The widest sampled cell text last measured for an Auto column, and what it was measured for (#24).
    internal readonly Layout.ContentMeasurement ContentMeasure = new();

    /// <summary>Sets the width layout computed, without making it explicit.</summary>
    internal void SetLayoutWidth(float width) => _width = width;

    /// <summary>
    /// Applies a width the user chose by dragging the resize grip or by auto-fit: it becomes explicit and, for a
    /// Star or Auto column, the column switches to <see cref="ColumnSizeMode.Fixed"/> so layout keeps it (#24).
    /// </summary>
    internal void ApplyUserWidth(float width)
    {
        Width = width;
        if (SizeMode == ColumnSizeMode.Fixed) return;
        SizeMode = ColumnSizeMode.Fixed;
        ContentFitWidth = 0f;
        ContentMeasure.Clear();
    }

    /// <summary>Minimum allowed width.</summary>
    public float MinWidth
    {
        get => _minWidth;
        set => SetField(ref _minWidth, value);
    }
    private float _minWidth = 40f;

    /// <summary>Maximum allowed width.</summary>
    public float MaxWidth
    {
        get => _maxWidth;
        set => SetField(ref _maxWidth, value);
    }
    private float _maxWidth = float.MaxValue;

    /// <summary>
    /// Sizing mode for layout calculation. Dragging the resize grip of a <see cref="ColumnSizeMode.Star"/> or
    /// <see cref="ColumnSizeMode.Auto"/> column, or auto-fitting it, switches it to <see cref="ColumnSizeMode.Fixed"/>
    /// at the chosen width, so the width persists instead of being shared out or measured again on the next frame.
    /// </summary>
    public ColumnSizeMode SizeMode
    {
        get => _sizeMode;
        set => SetField(ref _sizeMode, value);
    }
    private ColumnSizeMode _sizeMode = ColumnSizeMode.Fixed;

    /// <summary>
    /// Star weight when SizeMode == Star: the column's share of the width Fixed and Auto columns leave, relative to
    /// the other Star columns' weights. A resize drag or an auto-fit does not change it; it switches the column to
    /// <see cref="ColumnSizeMode.Fixed"/> instead (see <see cref="SizeMode"/>).
    /// </summary>
    public float StarWeight
    {
        get => _starWeight;
        set => SetField(ref _starWeight, value);
    }
    private float _starWeight = 1f;

    /// <summary>Is the column visible?</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }
    private bool _isVisible = true;

    /// <summary>
    /// Whether a <see cref="KumikoColumnType.Boolean"/> column has three states. When true, a toggle (tap, Space,
    /// Enter) cycles unchecked, checked, indeterminate (the value <c>null</c>) and back to unchecked, and a null
    /// value is drawn as a filled dash; the column's property must be a <c>bool?</c> to hold the indeterminate
    /// state. When false (the default) a toggle flips between unchecked and checked, and null is drawn unchecked.
    /// </summary>
    public bool IsThreeState
    {
        get => _isThreeState;
        set => SetField(ref _isThreeState, value);
    }
    private bool _isThreeState;

    /// <summary>The value at the empty end of a <see cref="KumikoColumnType.ProgressBar"/> column's bar. Default: 0.</summary>
    public double ProgressMinimum
    {
        get => _progressMinimum;
        set => SetField(ref _progressMinimum, value);
    }
    private double _progressMinimum;

    /// <summary>The value at the full end of a <see cref="KumikoColumnType.ProgressBar"/> column's bar. Default: 100.</summary>
    public double ProgressMaximum
    {
        get => _progressMaximum;
        set => SetField(ref _progressMaximum, value);
    }
    private double _progressMaximum = 100;

    /// <summary>
    /// Whether a <see cref="KumikoColumnType.ProgressBar"/> column draws its value as text over the bar,
    /// formatted with <see cref="Format"/> ("N0" when that is not set). Default: true.
    /// </summary>
    public bool ProgressShowText
    {
        get => _progressShowText;
        set => SetField(ref _progressShowText, value);
    }
    private bool _progressShowText = true;

    /// <summary>
    /// The fill color of a <see cref="KumikoColumnType.ProgressBar"/> column's bar, or null (the default) for the
    /// style's <see cref="KumikoStyle.AccentColor"/>.
    /// </summary>
    public KumikoColor? ProgressFillColor
    {
        get => _progressFillColor;
        set => SetField(ref _progressFillColor, value);
    }
    private KumikoColor? _progressFillColor;

    /// <summary>Is the column read-only?</summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetField(ref _isReadOnly, value);
    }
    private bool _isReadOnly;

    /// <summary>
    /// When true (the default), Tab and Shift+Tab stop on this column's cells, whether or not a cell is being
    /// edited, as long as the column is visible and not read-only; every column type is a tab stop, including
    /// Boolean (Space toggles it) and action-button columns. When false, Tab skips the column. Read-only and
    /// hidden columns are always skipped.
    /// </summary>
    public bool AllowTabStop
    {
        get => _allowTabStop;
        set => SetField(ref _allowTabStop, value);
    }
    private bool _allowTabStop = true;

    /// <summary>
    /// Can the user sort this column (header taps)? By default, true unless the column has nothing meaningful
    /// to order by: <see cref="KumikoColumnType.Template"/> and <see cref="KumikoColumnType.Image"/> columns,
    /// and columns that show the whole row item (empty <see cref="PropertyName"/> without a
    /// <see cref="ValueAccessor"/>). Setting it overrides the default.
    /// </summary>
    public bool AllowSorting
    {
        get => _allowSorting ?? (ColumnType is not (KumikoColumnType.Template or KumikoColumnType.Image)
                                 && (!string.IsNullOrEmpty(PropertyName) || ValueAccessor != null));
        set
        {
            if (_allowSorting == value) return;
            _allowSorting = value;
            RaisePropertyChanged(nameof(AllowSorting));
        }
    }
    private bool? _allowSorting;

    /// <summary>Can this column be resized?</summary>
    public bool AllowResize
    {
        get => _allowResize;
        set => SetField(ref _allowResize, value);
    }
    private bool _allowResize = true;

    /// <summary>Can this column be reordered via drag?</summary>
    public bool AllowReorder
    {
        get => _allowReorder;
        set => SetField(ref _allowReorder, value);
    }
    private bool _allowReorder = true;

    /// <summary>Can this column be filtered?</summary>
    public bool AllowFiltering
    {
        get => _allowFiltering;
        set => SetField(ref _allowFiltering, value);
    }
    private bool _allowFiltering = true;

    /// <summary>
    /// Whether the filter popup offers condition filters (an operator and a typed value, #325) for this column. Default
    /// true. Only Text, ComboBox, Picker, Numeric, ProgressBar and Date columns have them; the others, and a column
    /// with this set to false, show only the value checklist. Conditions set from code still apply.
    /// </summary>
    public bool AllowConditionFilter
    {
        get => _allowConditionFilter;
        set => SetField(ref _allowConditionFilter, value);
    }
    private bool _allowConditionFilter = true;

    /// <summary>
    /// Whether the grid-wide search (<see cref="KumikoSource.SearchText"/>) looks at this column's display text.
    /// Default true. Columns without a text representation are never searched, whatever this says:
    /// <see cref="KumikoColumnType.Boolean"/>, <see cref="KumikoColumnType.Image"/>,
    /// <see cref="KumikoColumnType.Template"/> and <see cref="KumikoColumnType.ProgressBar"/> columns, columns drawn
    /// by an interactive renderer (action buttons), and hidden columns.
    /// </summary>
    public bool AllowSearching
    {
        get => _allowSearching;
        set => SetField(ref _allowSearching, value);
    }
    private bool _allowSearching = true;

    /// <summary>
    /// Whether the grid-wide search reads this column: <see cref="AllowSearching"/>, visible, and a column type
    /// that shows text.
    /// </summary>
    internal bool IsSearchable =>
        _allowSearching && IsVisible &&
        ColumnType is not (KumikoColumnType.Boolean or KumikoColumnType.Image or KumikoColumnType.Template
            or KumikoColumnType.ProgressBar or KumikoColumnType.Selection) &&
        CustomCellRenderer is not IInteractiveCellRenderer;

    /// <summary>
    /// Active filter for this column. Null means no filter. Set it with
    /// <see cref="KumikoSource.SetColumnFilter"/>.
    /// </summary>
    public FilterDescription? ActiveFilter { get; internal set; }

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
    public ColumnFreezeMode FreezeMode
    {
        get => _freezeMode;
        set => SetField(ref _freezeMode, value);
    }
    private ColumnFreezeMode _freezeMode = ColumnFreezeMode.None;

    /// <summary>
    /// The <see cref="FreezeMode"/> the layout last suspended because the frozen panes did not fit the viewport
    /// (#268, see <see cref="Layout.FrozenPanes"/>), or <see cref="ColumnFreezeMode.None"/>. It only suspends the
    /// value it was set for: change <see cref="FreezeMode"/> and the column is pinned again until the next layout
    /// pass decides otherwise.
    /// </summary>
    internal ColumnFreezeMode SuspendedFreezeMode;

    /// <summary>
    /// The pane the column is drawn in: its <see cref="FreezeMode"/>, or <see cref="ColumnFreezeMode.None"/> (the
    /// scrolling pane) while the layout has suspended that freeze mode because the frozen panes did not fit.
    /// </summary>
    internal ColumnFreezeMode Pane =>
        FreezeMode != ColumnFreezeMode.None && FreezeMode == SuspendedFreezeMode ? ColumnFreezeMode.None : FreezeMode;

    /// <summary>
    /// The direction of the first header tap, or null (the default) for the column type's default: a
    /// <see cref="KumikoColumnType.Boolean"/> column sorts checked rows first
    /// (<see cref="KumikoSortDirection.Descending"/>), a <see cref="KumikoColumnType.Date"/> or
    /// <see cref="KumikoColumnType.DateTime"/> column newest first (<see cref="KumikoSortDirection.Descending"/>) and
    /// every other column, a <see cref="KumikoColumnType.Time"/> column included, ascending. Later taps switch to the
    /// opposite direction and then remove the sort. <see cref="KumikoSortDirection.None"/> is the same as null.
    /// The filter popup's first sort row follows it.
    /// </summary>
    public KumikoSortDirection? InitialSortDirection
    {
        get => _initialSortDirection;
        set => SetField(ref _initialSortDirection, value);
    }
    private KumikoSortDirection? _initialSortDirection;

    /// <summary>
    /// The direction of the first header tap: <see cref="InitialSortDirection"/>, or the column type's default.
    /// Never <see cref="KumikoSortDirection.None"/>.
    /// </summary>
    public KumikoSortDirection FirstSortDirection =>
        InitialSortDirection is { } initial && initial != KumikoSortDirection.None
            ? initial
            : ColumnType is KumikoColumnType.Boolean or KumikoColumnType.Date or KumikoColumnType.DateTime
                ? KumikoSortDirection.Descending
                : KumikoSortDirection.Ascending;

    internal static KumikoSortDirection Opposite(KumikoSortDirection direction) =>
        direction == KumikoSortDirection.Ascending ? KumikoSortDirection.Descending : KumikoSortDirection.Ascending;

    /// <summary>
    /// The stable name that identifies this column in a saved layout (<see cref="KumikoLayoutState"/>), or null (the
    /// default) to use <see cref="PropertyName"/>. Set it for a column without a property name, such as a template
    /// column, for two columns bound to the same property, and to keep a saved layout working after renaming the
    /// property. A column with neither is left out of saved layouts.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// True when the grid generated this column from a property of its items (<see cref="KumikoAutoGenerateColumnsMode"/>)
    /// rather than the app declaring it. The grid removes and replaces generated columns when the item type or the mode
    /// changes; declared columns are never touched.
    /// </summary>
    public bool IsAutoGenerated { get; internal set; }

    /// <summary>The name a saved layout knows this column by: <see cref="Key"/>, else <see cref="PropertyName"/>.</summary>
    internal string LayoutKey => string.IsNullOrEmpty(Key) ? PropertyName : Key;

    /// <summary>Current sort direction. Set it with <see cref="KumikoSource.SetSort"/>.</summary>
    public KumikoSortDirection SortDirection { get; internal set; } = KumikoSortDirection.None;

    /// <summary>
    /// Sort priority: 0 = not sorted, 1 = primary sort, 2 = secondary, etc. Set it with
    /// <see cref="KumikoSource.SetSort"/>.
    /// </summary>
    public int SortOrder { get; internal set; }

    /// <summary>
    /// Optional custom comparer for sorting this column.
    /// When set, this is used instead of the default Comparer&lt;object&gt;.Default.
    /// </summary>
    public IComparer? CustomComparer
    {
        get => _customComparer;
        set => SetField(ref _customComparer, value);
    }
    private IComparer? _customComparer;

    /// <summary>Display format for the cell value (e.g., "C2", "yyyy-MM-dd").</summary>
    public string? Format
    {
        get => _format;
        set => SetField(ref _format, value);
    }
    private string? _format;

    /// <summary>
    /// The format cells are shown with: <see cref="Format"/>, or the culture's short time ("t") for a
    /// <see cref="KumikoColumnType.Time"/> column and short date and time ("g") for a
    /// <see cref="KumikoColumnType.DateTime"/> column; null for the type's own default. The strings are
    /// constants, so a cache can compare them by reference.
    /// </summary>
    internal string? DisplayFormat => _format ?? ColumnType switch
    {
        KumikoColumnType.Time => "t",
        KumikoColumnType.DateTime => "g",
        _ => null,
    };

    /// <summary>
    /// Whether the time editor of a <see cref="KumikoColumnType.Time"/> or <see cref="KumikoColumnType.DateTime"/>
    /// column counts hours from 0 to 23 (true), or from 1 to 12 with an AM/PM designator (false); null (the default)
    /// follows the grid's culture: 24-hour unless its short time pattern has an AM/PM designator.
    /// </summary>
    public bool? Use24HourTime
    {
        get => _use24HourTime;
        set => SetField(ref _use24HourTime, value);
    }
    private bool? _use24HourTime;

    /// <summary>
    /// The minutes the up and down arrows of the time editor step by, from 1 to 30 (default 1). Stepping goes to
    /// the next multiple of the step, so 15 steps through :00, :15, :30 and :45. Typing a time is not limited by
    /// it. Values outside the range are limited to it.
    /// </summary>
    public int TimeMinuteStep
    {
        get => _timeMinuteStep;
        set => SetField(ref _timeMinuteStep, Math.Clamp(value, 1, 30));
    }
    private int _timeMinuteStep = 1;

    /// <summary>Whether the time editor uses 24-hour time in <paramref name="culture"/>: <see cref="Use24HourTime"/> or the culture's pattern.</summary>
    internal bool UsesTwentyFourHourTime(System.Globalization.CultureInfo culture) =>
        _use24HourTime ?? !culture.DateTimeFormat.ShortTimePattern.Contains('t');

    /// <summary>Horizontal alignment of cell content.</summary>
    public Rendering.KumikoTextAlignment TextAlignment
    {
        get => _textAlignment;
        set => SetField(ref _textAlignment, value);
    }
    private Rendering.KumikoTextAlignment _textAlignment = Rendering.KumikoTextAlignment.Left;

    /// <summary>
    /// How the text of this column's cells is laid out when it is wider than the cell. With
    /// <see cref="Rendering.KumikoTextWrap.None"/> (the default) a cell shows one line, cut with an ellipsis; with
    /// <see cref="Rendering.KumikoTextWrap.Word"/> or <see cref="Rendering.KumikoTextWrap.Character"/> it shows as
    /// many wrapped lines as fit the row height (line breaks in the value start a new line), and the last one ends
    /// with an ellipsis when there is more. With <see cref="KumikoStyle.RowHeightMode"/>
    /// <see cref="KumikoRowHeightMode.Fixed"/> (the default) rows keep <see cref="KumikoStyle.RowHeight"/>; with
    /// <see cref="KumikoRowHeightMode.Auto"/> a wrapping column makes its row tall enough for all of its lines (within
    /// <see cref="KumikoStyle.MaxRowHeight"/>). Applies to the built-in text cells (Text, Numeric and Date columns).
    /// </summary>
    public Rendering.KumikoTextWrap TextWrap
    {
        get => _textWrap;
        set => SetField(ref _textWrap, value);
    }
    private Rendering.KumikoTextWrap _textWrap = Rendering.KumikoTextWrap.None;

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

    /// <summary>
    /// The earliest date a <see cref="KumikoColumnType.Date"/> column's editor accepts, or null for none.
    /// Earlier days are drawn disabled in the calendar and cannot be picked or typed.
    /// </summary>
    public DateTime? EditorMinDate { get; set; }

    /// <summary>
    /// The latest date a <see cref="KumikoColumnType.Date"/> column's editor accepts, or null for none.
    /// Later days are drawn disabled in the calendar and cannot be picked or typed.
    /// </summary>
    public DateTime? EditorMaxDate { get; set; }

    /// <summary>Property path for display text in ComboBox and Picker editor items.</summary>
    public string? EditorDisplayMemberPath { get; set; }

    /// <summary>Property path for value in ComboBox and Picker editor items.</summary>
    public string? EditorValueMemberPath { get; set; }

    /// <summary>
    /// Returns the display text of a ComboBox or Picker editor item. Takes precedence over
    /// <see cref="EditorDisplayMemberPath"/>; reflection-free.
    /// </summary>
    public Func<object, string?>? EditorDisplaySelector { get; set; }

    /// <summary>
    /// Returns the value of a ComboBox or Picker editor item. Takes precedence over
    /// <see cref="EditorValueMemberPath"/>; reflection-free.
    /// </summary>
    public Func<object, object?>? EditorValueSelector { get; set; }

    /// <summary>
    /// Whether the ComboBox editor is searchable: typed characters narrow its dropdown to the items that
    /// contain them (<see cref="Components.DrawnComboBox.IsFilterable"/>). When false (the default), a typed
    /// character jumps to the next item that starts with it.
    /// </summary>
    public bool EditorIsFilterable { get; set; }

    /// <summary>
    /// Custom editor factory for Template columns. Receives a <see cref="Editing.CellEditorContext"/> with the
    /// cell's value (<see cref="Editing.CellEditorContext.Value"/>), the rectangle to place the editor in
    /// (<see cref="Editing.CellEditorContext.Bounds"/>), the grid's culture and the character typed to open
    /// the edit (<see cref="Editing.CellEditorContext.InitialCharacter"/>), and returns the editor. Takes
    /// precedence over <see cref="EditorDescriptor"/>.
    /// </summary>
    public Func<Editing.CellEditorContext, Components.DrawnComponent?>? EditorFactory { get; set; }

    /// <summary>
    /// XAML-declarable editor descriptor for Template columns.
    /// When set and <see cref="EditorFactory"/> is not set, this descriptor
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
    public Rendering.ICellRenderer? CustomCellRenderer
    {
        get => _customCellRenderer;
        set => SetField(ref _customCellRenderer, value);
    }
    private Rendering.ICellRenderer? _customCellRenderer;

    /// <summary>
    /// Optional renderer for the content of this column's header, instead of the grid-wide
    /// <see cref="KumikoRenderer.HeaderCellRenderer"/> and the built-in <see cref="Rendering.TextHeaderCellRenderer"/>.
    /// Use it to add an icon, a subtitle or a marker to the header text. The grid keeps the background, the hover
    /// feedback, the sort and filter glyphs, the resize grip and the reorder ghost. Can be set declaratively in XAML.
    /// Changing it re-measures the header for auto-fit and redraws the grid.
    /// </summary>
    public Rendering.IHeaderCellRenderer? CustomHeaderRenderer
    {
        get => _customHeaderRenderer;
        set => SetField(ref _customHeaderRenderer, value);
    }
    private Rendering.IHeaderCellRenderer? _customHeaderRenderer;

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
    public KumikoCellStyle? CellStyle
    {
        get => _cellStyle;
        set => SetField(ref _cellStyle, value);
    }
    private KumikoCellStyle? _cellStyle;

    /// <summary>
    /// Style overrides for the header cell of this column.
    /// Null properties fall back to the grid-level <see cref="KumikoStyle"/>.
    /// </summary>
    public KumikoCellStyle? HeaderCellStyle
    {
        get => _headerCellStyle;
        set => SetField(ref _headerCellStyle, value);
    }
    private KumikoCellStyle? _headerCellStyle;
}
