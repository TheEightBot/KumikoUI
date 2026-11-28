using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// The drawn column chooser (#335, spec #106): a live panel that lists a <see cref="KumikoSource"/>'s columns with a
/// visibility check box and a drag grip each, a search box that narrows the list, and Show all and Reset buttons.
/// Toggling a box shows or hides the column at once (<see cref="KumikoSource.SetColumnVisible"/>); dragging a grip, or
/// Alt+Up and Alt+Down, moves the column within its pane (<see cref="KumikoSource.ReorderColumn"/>); Reset restores the
/// default layout (<see cref="KumikoSource.ResetColumnLayout"/>). There is no OK or Cancel: Reset is the undo.
/// </summary>
/// <remarks>
/// <para>
/// The list holds every column whose <see cref="KumikoColumn.ShowInChooser"/> is true (never a selection column), in the
/// order the grid draws the panes, left-frozen, scrolling, right-frozen, and in <see cref="KumikoSource.Columns"/>
/// order within each pane, with hidden columns in their place. A column moves only within its pane, and a column
/// whose <see cref="KumikoColumn.AllowReorder"/> is false has no grip. While a search is active the grips are dimmed
/// and the list cannot be reordered. The last visible column's box, and the box of a visible column whose
/// <see cref="KumikoColumn.AllowHide"/> is false, are disabled. Frozen columns show a pin, and a sorted or filtered
/// column its sort or filter glyph, so a hidden column that still orders or filters the rows can be told apart.
/// </para>
/// <para>
/// The panel reads the source live: a change made elsewhere while it is open (in code, by a header drag) shows at the
/// next frame. Keyboard: the search box has it first; Down moves into the list, where Up, Down, Home, End, PageUp and
/// PageDown move, Space or Enter toggles, and Alt+Up or Alt+Down moves the column; Tab and Shift+Tab cycle the search
/// box, the list, Show all, Reset and the close button; Escape clears a search, then closes the panel
/// (<see cref="CloseRequested"/>). Only the rows in view are drawn, and drawing allocates nothing in steady state.
/// </para>
/// </remarks>
public sealed class DrawnColumnChooser : DrawnComponent, IMeasuredPopup
{
    /// <summary>The panel's width, in device-independent pixels.</summary>
    public const float DefaultWidth = 260f;

    /// <summary>The tallest the panel gets by default (<see cref="MaxHeight"/>); a longer list scrolls.</summary>
    public const float DefaultMaxHeight = 340f;

    /// <summary>The smallest row height, whatever the grid's row height.</summary>
    public const float MinItemHeight = 28f;

    private const float Padding = 6f;
    private const float TitleHeight = 30f;
    private const float SearchBoxHeight = 30f;
    private const float FooterHeight = 34f;
    private const float SeparatorMargin = 4f;
    private const float GripWidth = 22f;
    private const float CheckboxSize = 16f;
    private const float CloseSize = 26f;
    private const float BadgeSize = 12f;
    private const float ScrollbarHitWidth = 12f;
    private const float MinThumbHeight = 20f;
    private const float DragThreshold = 6f;
    private const float GripDragSlop = 2f;
    private const int WheelRowsPerNotch = 3;

    // The fixed parts of the panel's height: everything but the list.
    private const float ChromeHeight = Padding + TitleHeight + SearchBoxHeight + SeparatorMargin + 1 + SeparatorMargin +
                                       SeparatorMargin + 1 + SeparatorMargin + FooterHeight + Padding;

    private readonly KumikoSource _source;
    private readonly DrawnTextBox _search;

    // The chooser's columns in list order, their labels, and the positions (into _all) that match the search.
    private readonly List<KumikoColumn> _all = new();
    private readonly List<string> _labels = new();
    private readonly List<int> _visible = new();
    private int _columnsVersion = int.MinValue;
    private string _appliedSearch = string.Empty;
    private bool _searchDirty;

    private float _scrollOffset;
    private float _itemHeight = 36f;
    private float _maxHeight = DefaultMaxHeight;
    private Stop _stop = Stop.Search;
    private int _focusIndex = -1;   // the entry the keyboard is on (index into _visible), -1 for none
    private Target _hoverTarget;
    private int _hoverIndex = -1;

    // The press in progress
    private Target _pressTarget;
    private int _pressIndex = -1;
    private float _pressY;
    private float _lastPanY;
    private bool _isPanning;
    private float _scrollbarGrab;

    // A grip drag: the entry (a position in _all; no search is active), where it was grabbed, the pointer, the slot.
    private int _dragIndex = -1;
    private float _dragGrab;
    private float _dragPointerY;
    private int _dropSlot = -1;

    /// <summary>A place the keyboard can be; Tab and Shift+Tab move through them in this order.</summary>
    internal enum Stop { Search, List, ShowAll, Reset, Close }

    private enum Target { None, Close, Search, List, Grip, Scrollbar, ShowAll, Reset }

    private readonly record struct ChooserLayout(
        KumikoRect Title, KumikoRect Close, KumikoRect Search, KumikoRect List, KumikoRect ShowAll, KumikoRect Reset,
        float TopSeparatorY, float BottomSeparatorY);

    /// <summary>Initializes a new column chooser for <paramref name="source"/>'s columns.</summary>
    /// <param name="source">The source whose columns the chooser lists and changes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public DrawnColumnChooser(KumikoSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _search = new DrawnTextBox
        {
            Font = SmallFont,
            Padding = 6f,
            InitialSelectionMode = Editing.EditTextSelectionMode.CursorAtEnd,
        };
        _search.ValueChanged += (_, _) => { _searchDirty = true; InvalidateVisual(); };
        _search.RedrawRequested += (_, _) => InvalidateVisual();
        EnsureEntries();
        UpdateSize();
    }

    // ── Styling ──────────────────────────────────────────────────

    /// <summary>Font of the column names and the buttons.</summary>
    public KumikoFont Font { get; set; } = new("Default", 13);
    /// <summary>Font of the search box and the "no matches" text.</summary>
    public KumikoFont SmallFont { get; set; } = new("Default", 12);
    /// <summary>Font of the title.</summary>
    public KumikoFont TitleFont { get; set; } = new("Default", 13, bold: true);
    /// <summary>Text color of the title, the column names and the buttons.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Color of disabled boxes and buttons, and of the grips while a search is active.</summary>
    public KumikoColor DisabledColor { get; set; } = new(170, 170, 170);
    /// <summary>Background fill of the panel.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border of the panel and of the buttons.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Background of the row or button under the pointer.</summary>
    public KumikoColor HoverColor { get; set; } = new(0, 120, 215, 30);
    /// <summary>Accent: checked boxes, the drop line of a drag and the search box's focused border.</summary>
    public KumikoColor AccentColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the check mark drawn on the accent.</summary>
    public KumikoColor CheckmarkColor { get; set; } = KumikoColor.White;
    /// <summary>Border of the search box when it does not have the keyboard.</summary>
    public KumikoColor SearchBorderColor { get; set; } = new(200, 200, 200);
    /// <summary>Background of the search box.</summary>
    public KumikoColor SearchBackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Color of the search box's placeholder, the grips and the badges.</summary>
    public KumikoColor PlaceholderColor { get; set; } = new(150, 150, 150);
    /// <summary>Color of the lines between the sections.</summary>
    public KumikoColor SeparatorColor { get; set; } = new(220, 220, 220);
    /// <summary>Color of the search box's caret.</summary>
    public KumikoColor CursorColor { get; set; } = new(0, 0, 0);
    /// <summary>Outline of the row or button the keyboard is on.</summary>
    public KumikoColor KeyboardFocusColor { get; set; } = new(0, 120, 215);

    /// <summary>
    /// The height of a row: the grid's row height (<see cref="KumikoStyle.RowHeight"/>, set by <see cref="ApplyTheme"/>),
    /// at least <see cref="MinItemHeight"/>, so a row is a finger-sized target. The whole row toggles its column.
    /// </summary>
    public float ItemHeight
    {
        get => _itemHeight;
        set
        {
            float height = float.IsFinite(value) ? Math.Max(MinItemHeight, value) : MinItemHeight;
            if (Math.Abs(height - _itemHeight) < 0.01f) return;
            _itemHeight = height;
            UpdateSize();
            ScrollTo(_scrollOffset);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The tallest the panel gets (<see cref="DefaultMaxHeight"/> by default); the grid lowers it to fit its viewport.
    /// The list scrolls when its rows do not fit.
    /// </summary>
    public float MaxHeight
    {
        get => _maxHeight;
        set
        {
            float height = float.IsFinite(value) ? Math.Max(ChromeHeight + MinItemHeight, value) : DefaultMaxHeight;
            if (Math.Abs(height - _maxHeight) < 0.01f) return;
            _maxHeight = height;
            UpdateSize();
            ScrollTo(_scrollOffset);
            InvalidateVisual();
        }
    }

    // ── Events ───────────────────────────────────────────────────

    /// <summary>Raised by Escape (with an empty search) and by the close button: the host closes the panel.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Raised when the search box is pressed and needs the platform keyboard (the soft keyboard on mobile).</summary>
    public event EventHandler? KeyboardFocusRequested;

    /// <summary>Raised when the search box copies or cuts text (Ctrl/Cmd+C, Ctrl/Cmd+X): the host takes the text.</summary>
    public event EventHandler<ClipboardTextEventArgs>? SearchCopyRequested
    {
        add => _search.CopyRequested += value;
        remove => _search.CopyRequested -= value;
    }

    /// <summary>Raised when the search box pastes (Ctrl/Cmd+V): the host supplies the text.</summary>
    public event EventHandler<ClipboardRequestEventArgs>? SearchPasteRequested
    {
        add => _search.PasteRequested += value;
        remove => _search.PasteRequested -= value;
    }

    /// <summary>Raised with a text for a screen reader: a column shown, hidden or moved, the layout reset, the row or button the keyboard moved to.</summary>
    internal event Action<string>? AnnouncementRequested;

    /// <summary>
    /// Asked before the chooser changes a column; false keeps everything as it is. The grid commits an open edit here,
    /// as a click on another cell would.
    /// </summary>
    internal Func<bool>? BeforeChange { get; set; }

    // ── State ────────────────────────────────────────────────────

    /// <summary>The search box's text-input surface, for a host's native text-input adapter.</summary>
    public ITextInputSession SearchTextInput => _search;

    /// <summary>The search text; the list shows the columns whose name contains it (under the source's text rules).</summary>
    public string SearchText
    {
        get => _search.Text;
        set => _search.Text = value ?? string.Empty;
    }

    /// <summary>Pastes <paramref name="text"/> into the search box (see <see cref="ClipboardRequestEventArgs.IsPending"/>).</summary>
    /// <param name="text">The text to paste.</param>
    public void PasteSearchText(string? text) => _search.PasteText(text);

    /// <summary>The columns the list shows, matching the search, in list order.</summary>
    public IReadOnlyList<KumikoColumn> VisibleEntries
    {
        get
        {
            EnsureEntries();
            var list = new List<KumikoColumn>(_visible.Count);
            for (int i = 0; i < _visible.Count; i++) list.Add(_all[_visible[i]]);
            return list;
        }
    }

    /// <summary>The vertical scroll offset of the list, in pixels.</summary>
    public float ListScrollOffset => _scrollOffset;

    /// <summary>Where the keyboard is.</summary>
    internal Stop CurrentStop => _stop;

    /// <summary>The entry the keyboard is on (an index into <see cref="VisibleEntries"/>), or -1.</summary>
    internal int FocusedIndex => _focusIndex;

    /// <summary>The column the keyboard is on, or null.</summary>
    internal KumikoColumn? FocusedColumn
    {
        get
        {
            EnsureEntries();
            return _focusIndex >= 0 && _focusIndex < _visible.Count ? _all[_visible[_focusIndex]] : null;
        }
    }

    /// <summary>Whether a grip drag is in progress.</summary>
    internal bool IsDragging => _dragIndex >= 0;

    /// <summary>The slot (a position in the list, 0 to the count) the drag would drop on, or -1 when it would not move.</summary>
    internal int DropSlot => IsDragging && WouldMove(_dragIndex, _dropSlot) ? _dropSlot : -1;

    /// <summary>The text input with the keyboard: always the search box, which takes typing from the list too.</summary>
    internal ITextInputSession ActiveTextInput => _search;

    /// <summary>The rectangle of the entry at <paramref name="index"/> in <see cref="VisibleEntries"/>, scrolled.</summary>
    internal KumikoRect EntryBounds(int index)
    {
        EnsureEntries();
        var list = Layout().List;
        return new KumikoRect(list.X, list.Y + index * _itemHeight - _scrollOffset, list.Width, _itemHeight);
    }

    /// <summary>The close button's rectangle.</summary>
    internal KumikoRect CloseBounds => Layout().Close;

    /// <summary>The Show all button's rectangle.</summary>
    internal KumikoRect ShowAllBounds => Layout().ShowAll;

    /// <summary>The Reset button's rectangle.</summary>
    internal KumikoRect ResetBounds => Layout().Reset;

    /// <summary>The search box's rectangle.</summary>
    internal KumikoRect SearchBounds => Layout().Search;

    /// <summary>The x of an entry's grip (the middle of the drag area at the row's left).</summary>
    internal float GripX => Layout().List.X + GripWidth / 2;

    /// <summary>The panel's accessible name (<see cref="KumikoStrings.ColumnChooserTitle"/>).</summary>
    internal string AccessibleName => _source.Strings.ColumnChooserTitle;

    /// <summary>The number of columns the chooser lists, whatever the search.</summary>
    internal int EntryCount
    {
        get
        {
            EnsureEntries();
            return _all.Count;
        }
    }

    /// <summary>Takes the panel's colors, fonts and row height from the grid's style, as the filter popup does.</summary>
    /// <param name="style">The grid's style.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is null.</exception>
    public void ApplyTheme(KumikoStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        IconProvider = style.IconProvider;
        var bg = style.BackgroundColor;
        var text = style.CellTextColor;
        BackgroundColor = bg;
        TextColor = text;
        AccentColor = style.AccentColor;
        BorderColor = style.HeaderBorderColor;
        SeparatorColor = style.GridLineColor;
        SearchBorderColor = style.GridLineColor;
        SearchBackgroundColor = bg;
        CursorColor = text;
        KeyboardFocusColor = style.AccentColor;
        HoverColor = style.AccentColor.WithAlpha(30);
        PlaceholderColor = new KumikoColor((byte)((text.R + bg.R) / 2), (byte)((text.G + bg.G) / 2), (byte)((text.B + bg.B) / 2));
        DisabledColor = PlaceholderColor.WithAlpha(160);
        double accentLuminance = (0.299 * style.AccentColor.R + 0.587 * style.AccentColor.G + 0.114 * style.AccentColor.B) / 255.0;
        CheckmarkColor = accentLuminance > 0.5 ? KumikoColor.Black : KumikoColor.White;
        Font = style.CellFont;
        TitleFont = style.HeaderFont;
        SmallFont = new KumikoFont(style.CellFont.Family, Math.Max(10f, style.CellFont.Size - 1));
        _search.SelectionHighlightColor = style.AccentColor.WithAlpha(80);
        ItemHeight = style.RowHeight;
        InvalidateVisual();
    }

    // ── Entries ──────────────────────────────────────────────────

    /// <summary>Rebuild the list when the source's columns changed, then apply the search; allocates only on a change.</summary>
    private void EnsureEntries()
    {
        if (_columnsVersion != _source.ColumnsVersion)
        {
            var focused = _focusIndex >= 0 && _focusIndex < _visible.Count ? _all[_visible[_focusIndex]] : null;
            RebuildEntries();
            ApplySearch(_search.Text, keepScroll: true);
            if (focused != null) _focusIndex = _visible.IndexOf(_all.IndexOf(focused));
            if (_stop == Stop.List && _focusIndex < 0 && _visible.Count > 0) _focusIndex = 0;
            UpdateSize();
            ScrollTo(_scrollOffset);
            return;
        }
        EnsureSearchApplied();
    }

    private void RebuildEntries()
    {
        _columnsVersion = _source.ColumnsVersion;
        _all.Clear();
        _labels.Clear();
        var columns = _source.Columns;
        for (int pane = 0; pane < 3; pane++)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                if (!IsListed(column) || PaneOf(column) != pane) continue;
                _all.Add(column);
                _labels.Add(KumikoAnnouncements.ColumnLabel(_source.Strings, column, i));
            }
        }
    }

    private static bool IsListed(KumikoColumn column) =>
        column.ShowInChooser && column.ColumnType != KumikoColumnType.Selection;

    /// <summary>The pane a column belongs to by its freeze mode: 0 left-frozen, 1 scrolling, 2 right-frozen.</summary>
    private static int PaneOf(KumikoColumn column) => column.FreezeMode switch
    {
        ColumnFreezeMode.Left => 0,
        ColumnFreezeMode.Right => 2,
        _ => 1,
    };

    private void EnsureSearchApplied()
    {
        if (!_searchDirty) return;
        _searchDirty = false;
        if (_search.Text == _appliedSearch) return;
        ApplySearch(_search.Text, keepScroll: false);
    }

    // Separate from EnsureSearchApplied, as in the filter popup, so the per-frame path enters no method with a closure.
    private void ApplySearch(string text, bool keepScroll)
    {
        _searchDirty = false;
        _visible.Clear();
        var comparer = _source.Comparer;
        for (int i = 0; i < _all.Count; i++)
        {
            if (text.Length == 0 || comparer.TextContains(_labels[i], text)) _visible.Add(i);
        }
        _appliedSearch = text;
        if (!keepScroll)
        {
            _scrollOffset = 0;
            _focusIndex = -1;
            _hoverIndex = -1;
        }
        if (_focusIndex >= _visible.Count) _focusIndex = _visible.Count - 1;
    }

    private bool IsSearching => _appliedSearch.Length > 0;

    // ── Size and layout ──────────────────────────────────────────

    /// <summary>
    /// Before each frame's placement: follow the columns, and ask for the full width again (the popup manager narrows a
    /// panel that does not fit the viewport, and must widen it when the viewport grows).
    /// </summary>
    void IMeasuredPopup.Measure(IDrawingContext ctx)
    {
        EnsureEntries();
        if (Bounds.Width < DefaultWidth - 0.01f) Bounds = new KumikoRect(Bounds.X, Bounds.Y, DefaultWidth, Bounds.Height);
    }

    private float ComputeHeight()
    {
        // Sized by every entry, not the matches, so the panel keeps its size while the user types.
        float list = Math.Max(1, _all.Count) * _itemHeight;
        return Math.Min(_maxHeight, ChromeHeight + list);
    }

    private void UpdateSize()
    {
        float height = ComputeHeight();
        if (Math.Abs(Bounds.Height - height) < 0.01f && Bounds.Width > 0) return;
        Bounds = new KumikoRect(Bounds.X, Bounds.Y, Bounds.Width > 0 ? Bounds.Width : DefaultWidth, height);
    }

    private float ListHeight => Math.Max(0, Bounds.Height - ChromeHeight);

    private float ContentHeight => _visible.Count * _itemHeight;

    private float MaxScrollOffset => Math.Max(0, ContentHeight - ListHeight);

    private ChooserLayout Layout()
    {
        var b = Bounds;
        float x = b.X + Padding;
        float width = Math.Max(0, b.Width - Padding * 2);
        float y = b.Y + Padding;

        var title = new KumikoRect(x, y, width, TitleHeight);
        var close = new KumikoRect(b.Right - Padding - CloseSize, y + (TitleHeight - CloseSize) / 2, CloseSize, CloseSize);
        y += TitleHeight;

        var search = new KumikoRect(x, y + 2, width, SearchBoxHeight - 4);
        y += SearchBoxHeight + SeparatorMargin;
        float topSeparator = y;
        y += 1 + SeparatorMargin;

        var list = new KumikoRect(x, y, width, ListHeight);
        y += list.Height + SeparatorMargin;
        float bottomSeparator = y;
        y += 1 + SeparatorMargin;

        float half = (width - 6) / 2;
        var showAll = new KumikoRect(x, y + 3, half, FooterHeight - 6);
        var reset = new KumikoRect(x + half + 6, y + 3, half, FooterHeight - 6);
        return new ChooserLayout(title, close, search, list, showAll, reset, topSeparator, bottomSeparator);
    }

    // ── Drawing ──────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (!IsVisible) return;
        EnsureEntries();
        var layout = Layout();
        var strings = _source.Strings;

        // Background and border (the popup manager draws the shadow)
        ctx.FillRect(Bounds, Paints.Get(color: BackgroundColor));
        ctx.DrawRect(Bounds, Paints.Get(color: BorderColor, strokeWidth: 1));

        // Title and close button
        ctx.DrawTextInRect(strings.ColumnChooserTitle,
            new KumikoRect(layout.Title.X + 2, layout.Title.Y, layout.Title.Width - CloseSize - 4, TitleHeight),
            Paints.Get(color: TextColor, font: TitleFont), KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        if (_hoverTarget == Target.Close || _pressTarget == Target.Close)
            ctx.FillRoundRect(layout.Close, 4, Paints.Get(color: HoverColor));
        const float iconSize = 12f;
        IconProvider.DrawIcon(ctx, KumikoIcon.Remove,
            new KumikoRect(layout.Close.X + (CloseSize - iconSize) / 2, layout.Close.Y + (CloseSize - iconSize) / 2, iconSize, iconSize),
            Paints.Get(color: TextColor, strokeWidth: 1.5f));
        if (IsFocused && _stop == Stop.Close) DrawFocusRing(ctx, layout.Close);

        DrawSearchBox(ctx, layout.Search, strings);

        var separator = Paints.Get(color: SeparatorColor, strokeWidth: 1);
        ctx.DrawLine(layout.List.X, layout.TopSeparatorY, layout.List.Right, layout.TopSeparatorY, separator);
        DrawList(ctx, layout.List, strings);
        ctx.DrawLine(layout.List.X, layout.BottomSeparatorY, layout.List.Right, layout.BottomSeparatorY, separator);

        DrawButton(ctx, layout.ShowAll, strings.ColumnChooserShowAll, Target.ShowAll, CanShowAll(), Stop.ShowAll);
        DrawButton(ctx, layout.Reset, strings.ColumnChooserReset, Target.Reset, CanReset, Stop.Reset);
    }

    private void DrawSearchBox(IDrawingContext ctx, KumikoRect rect, KumikoStrings strings)
    {
        _search.Bounds = rect;
        _search.Font = SmallFont;
        _search.TextColor = TextColor;
        _search.CursorColor = CursorColor;
        _search.BackgroundColor = SearchBackgroundColor;
        _search.EditingBackgroundColor = SearchBackgroundColor;
        _search.BorderColor = SearchBorderColor;
        _search.FocusedBorderColor = AccentColor;
        _search.BorderWidth = 1f;
        _search.OnDraw(ctx);
        if (_search.Text.Length == 0)
        {
            ctx.DrawTextInRect(strings.ColumnChooserSearchPlaceholder,
                new KumikoRect(rect.X + 8, rect.Y, rect.Width - 12, rect.Height),
                Paints.Get(color: PlaceholderColor, font: SmallFont), KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }
    }

    private void DrawList(IDrawingContext ctx, KumikoRect list, KumikoStrings strings)
    {
        if (list.Height <= 0) return;
        ctx.Save();
        ctx.ClipRect(new KumikoRect(list.X - 1, list.Y, list.Width + 2, list.Height));

        if (_visible.Count == 0)
        {
            ctx.DrawTextInRect(strings.ColumnChooserNoMatches, new KumikoRect(list.X, list.Y, list.Width, _itemHeight),
                Paints.Get(color: PlaceholderColor, font: SmallFont), KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
            ctx.Restore();
            return;
        }

        int visibleData = VisibleDataColumnCount();
        bool scrollable = MaxScrollOffset > 0;
        int first = Math.Max(0, (int)(_scrollOffset / _itemHeight));
        int last = Math.Min(_visible.Count - 1, (int)((_scrollOffset + list.Height) / _itemHeight));
        for (int i = first; i <= last; i++)
        {
            float y = list.Y + i * _itemHeight - _scrollOffset;
            var row = new KumikoRect(list.X, y, list.Width, _itemHeight);
            bool dragged = i == _dragIndex;
            if (!dragged && _hoverTarget is Target.List or Target.Grip && _hoverIndex == i && _dragIndex < 0)
                ctx.FillRect(row, Paints.Get(color: HoverColor));
            DrawEntry(ctx, row, _visible[i], visibleData, scrollable, dragged ? (byte)70 : (byte)255);
            if (IsFocused && _stop == Stop.List && _focusIndex == i && _dragIndex < 0)
                ctx.DrawRect(new KumikoRect(row.X + 0.5f, row.Y + 0.5f, row.Width - 1, row.Height - 1),
                    Paints.Get(color: KeyboardFocusColor, strokeWidth: 1));
        }

        if (_dragIndex >= 0) DrawDrag(ctx, list, visibleData, scrollable);

        if (scrollable)
        {
            var thumb = ScrollbarThumb(list);
            ScrollThumb.Fill(ctx, new KumikoRect(list.Right - 5, thumb.Y, 4, thumb.Height),
                Paints.Get(color: PlaceholderColor.WithAlpha(_pressTarget == Target.Scrollbar ? (byte)220 : (byte)150)));
        }
        ctx.Restore();
    }

    /// <summary>The dragged entry under the pointer at half alpha, and the 2 px drop line at its slot.</summary>
    private void DrawDrag(IDrawingContext ctx, KumikoRect list, int visibleData, bool scrollable)
    {
        float top = Math.Clamp(_dragPointerY - _dragGrab, list.Y - _itemHeight / 2, list.Bottom - _itemHeight / 2);
        var ghost = new KumikoRect(list.X, top, list.Width, _itemHeight);
        ctx.FillRect(ghost, Paints.Get(color: HoverColor));
        DrawEntry(ctx, ghost, _visible[_dragIndex], visibleData, scrollable, 128);

        int slot = DropSlot;
        if (slot < 0) return;
        float lineY = list.Y + slot * _itemHeight - _scrollOffset;
        ctx.FillRect(new KumikoRect(list.X, lineY - 1, list.Width - (scrollable ? ScrollbarHitWidth : 0), 2),
            Paints.Get(color: AccentColor));
    }

    private void DrawEntry(IDrawingContext ctx, KumikoRect row, int entry, int visibleData, bool scrollable, byte alpha)
    {
        var column = _all[entry];
        float centerY = row.Y + row.Height / 2;

        // The grip: dimmed while a search is active, absent when the column cannot move
        if (column.AllowReorder)
        {
            var gripColor = (IsSearching ? DisabledColor : PlaceholderColor).WithAlpha(Scale(alpha, IsSearching ? DisabledColor.A : PlaceholderColor.A));
            IconProvider.DrawIcon(ctx, KumikoIcon.DragHandle, new KumikoRect(row.X + 5, centerY - 6, 12, 12),
                Paints.Get(color: gripColor, strokeWidth: 1.5f));
        }

        // The box: checked when the column is visible, disabled when the user may not change it
        bool isChecked = column.IsVisible;
        bool enabled = !isChecked || (column.AllowHide && visibleData > 1);
        var box = new KumikoRect(row.X + GripWidth, centerY - CheckboxSize / 2, CheckboxSize, CheckboxSize);
        var boxColor = !enabled ? DisabledColor : isChecked ? AccentColor : BorderColor;
        if (isChecked)
        {
            ctx.FillRoundRect(box, 2, Paints.Get(color: boxColor.WithAlpha(Scale(alpha, boxColor.A))));
            IconProvider.DrawIcon(ctx, KumikoIcon.Check, box,
                Paints.Get(color: CheckmarkColor.WithAlpha(alpha), strokeWidth: 2f));
        }
        else
        {
            ctx.DrawRoundRect(box, 2, Paints.Get(color: boxColor.WithAlpha(Scale(alpha, boxColor.A)), strokeWidth: 1.5f));
        }

        // Badges at the right: a pin for a frozen column, the sort and filter glyphs of a sorted or filtered column
        float right = row.Right - 4 - (scrollable ? ScrollbarHitWidth : 0);
        var badgePaint = Paints.Get(color: PlaceholderColor.WithAlpha(Scale(alpha, PlaceholderColor.A)), strokeWidth: 1.5f);
        if (column.ActiveFilter is { IsActive: true })
        {
            right -= BadgeSize;
            IconProvider.DrawIcon(ctx, KumikoIcon.FilterActive, new KumikoRect(right, centerY - BadgeSize / 2, BadgeSize, BadgeSize), badgePaint);
            right -= 4;
        }
        if (column.SortDirection != KumikoSortDirection.None)
        {
            right -= BadgeSize;
            IconProvider.DrawIcon(ctx,
                column.SortDirection == KumikoSortDirection.Descending ? KumikoIcon.SortDescending : KumikoIcon.SortAscending,
                new KumikoRect(right, centerY - BadgeSize / 2, BadgeSize, BadgeSize), badgePaint);
            right -= 4;
        }
        if (column.FreezeMode != ColumnFreezeMode.None)
        {
            right -= BadgeSize;
            DrawPin(ctx, new KumikoRect(right, centerY - BadgeSize / 2, BadgeSize, BadgeSize), badgePaint);
            right -= 4;
        }

        float textX = box.Right + 8;
        // The label stays readable even when the box is disabled (a column that cannot be hidden is still listed)
        var textColor = TextColor;
        ctx.DrawTextInRect(_labels[entry], new KumikoRect(textX, row.Y, Math.Max(0, right - textX), row.Height),
            Paints.Get(color: textColor.WithAlpha(Scale(alpha, textColor.A)), font: Font),
            KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
    }

    /// <summary>A small push pin: a round head on a short needle.</summary>
    private static void DrawPin(IDrawingContext ctx, KumikoRect rect, KumikoPaint paint)
    {
        float cx = rect.X + rect.Width / 2;
        ctx.FillRoundRect(new KumikoRect(cx - 3.5f, rect.Y + 1, 7, 7), 3.5f, paint);
        ctx.DrawLine(cx, rect.Y + 7, cx, rect.Bottom - 1, paint);
    }

    private void DrawButton(IDrawingContext ctx, KumikoRect rect, string text, Target target, bool enabled, Stop stop)
    {
        if (enabled && (_hoverTarget == target || _pressTarget == target))
            ctx.FillRoundRect(rect, 3, Paints.Get(color: HoverColor));
        ctx.DrawRoundRect(rect, 3, Paints.Get(color: enabled ? BorderColor : DisabledColor, strokeWidth: 1));
        ctx.DrawTextInRect(text, rect, Paints.Get(color: enabled ? AccentColor : DisabledColor, font: Font),
            KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
        if (IsFocused && _stop == stop) DrawFocusRing(ctx, rect);
    }

    private void DrawFocusRing(IDrawingContext ctx, KumikoRect rect) =>
        ctx.DrawRoundRect(new KumikoRect(rect.X - 1.5f, rect.Y - 1.5f, rect.Width + 3, rect.Height + 3), 4,
            Paints.Get(color: KeyboardFocusColor, strokeWidth: 1.5f));

    private static byte Scale(byte alpha, byte colorAlpha) => (byte)(alpha * colorAlpha / 255);

    private KumikoRect ScrollbarThumb(KumikoRect list)
    {
        float thumbHeight = ScrollThumb.Length(list.Height, list.Height, ContentHeight, MinThumbHeight);
        float thumbY = ScrollThumb.Start(list.Y, list.Height, thumbHeight, _scrollOffset, MaxScrollOffset);
        return new KumikoRect(list.Right - ScrollbarHitWidth, thumbY, ScrollbarHitWidth, thumbHeight);
    }

    /// <summary>The visible columns that are not selection columns: the last one cannot be hidden.</summary>
    private int VisibleDataColumnCount()
    {
        int count = 0;
        var columns = _source.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].IsVisible && columns[i].ColumnType != KumikoColumnType.Selection) count++;
        }
        return count;
    }

    private bool CanShowAll()
    {
        for (int i = 0; i < _all.Count; i++)
        {
            if (!_all[i].IsVisible) return true;
        }
        return false;
    }

    private bool CanReset => !_source.IsDefaultColumnLayout;

    // ── Hit testing ──────────────────────────────────────────────

    private Target TargetAt(in ChooserLayout layout, float x, float y)
    {
        if (layout.Close.Contains(x, y)) return Target.Close;
        if (layout.Search.Contains(x, y)) return Target.Search;
        if (layout.List.Contains(x, y))
        {
            if (MaxScrollOffset > 0 && x >= layout.List.Right - ScrollbarHitWidth) return Target.Scrollbar;
            return x < layout.List.X + GripWidth ? Target.Grip : Target.List;
        }
        if (layout.ShowAll.Contains(x, y)) return Target.ShowAll;
        if (layout.Reset.Contains(x, y)) return Target.Reset;
        return Target.None;
    }

    /// <summary>The index (into the visible entries) of the row at a viewport y, or -1.</summary>
    private int ItemAt(KumikoRect list, float y)
    {
        if (y < list.Y || y >= list.Bottom) return -1;
        int index = (int)((y - list.Y + _scrollOffset) / _itemHeight);
        return index >= 0 && index < _visible.Count ? index : -1;
    }

    // ── Pointer input ────────────────────────────────────────────

    /// <summary>
    /// A press starts a gesture on what it lands on: a row toggles its column on release unless the pointer moved (then
    /// the list scrolls), a grip drags its column at the first move, the buttons act on release over the same button,
    /// the scroll bar drags, and the search box places its caret.
    /// </summary>
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        EnsureEntries();
        var layout = Layout();
        _pressY = _lastPanY = e.Y;
        _isPanning = false;
        _pressIndex = -1;
        _dragIndex = -1;
        _pressTarget = TargetAt(layout, e.X, e.Y);

        switch (_pressTarget)
        {
            case Target.Search:
                SetStop(Stop.Search, announce: false);
                _search.OnPointerDown(e);
                KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Target.List or Target.Grip:
                _pressIndex = ItemAt(layout.List, e.Y);
                break;
            case Target.Scrollbar:
                var thumb = ScrollbarThumb(layout.List);
                if (e.Y < thumb.Y || e.Y > thumb.Bottom)
                    ScrollThumbTo(layout.List, e.Y - thumb.Height / 2);
                _scrollbarGrab = e.Y - ScrollbarThumb(layout.List).Y;
                break;
        }

        InvalidateVisual();
        return true; // every press inside the panel is its own
    }

    /// <summary>Drag a grip, scroll the list or the scroll bar, select in the search box, or show hover.</summary>
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        EnsureEntries();
        var layout = Layout();

        switch (_pressTarget)
        {
            case Target.Grip:
                if (_dragIndex < 0 && Math.Abs(e.Y - _pressY) > GripDragSlop && CanDrag(_pressIndex))
                {
                    _dragIndex = _pressIndex;
                    _dragGrab = _pressY - (layout.List.Y + _pressIndex * _itemHeight - _scrollOffset);
                }
                if (_dragIndex >= 0)
                {
                    _dragPointerY = e.Y;
                    AutoScrollDuringDrag(layout.List, e.Y);
                    _dropSlot = SlotAt(layout.List, e.Y);
                    InvalidateVisual();
                    return true;
                }
                PanList(e.Y); // a grip that cannot drag scrolls like the rest of the row
                break;
            case Target.List:
                PanList(e.Y);
                break;
            case Target.Scrollbar:
                ScrollThumbTo(layout.List, e.Y - _scrollbarGrab);
                break;
            case Target.Search:
                _search.OnPointerMove(e);
                break;
        }

        UpdateHover(layout, e.X, e.Y);
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        EnsureEntries();
        var layout = Layout();
        var pressed = _pressTarget;
        _pressTarget = Target.None;
        bool sameTarget = TargetAt(layout, e.X, e.Y) == pressed;

        switch (pressed)
        {
            case Target.Search:
                _search.OnPointerUp(e);
                break;
            case Target.Grip when _dragIndex >= 0:
                int from = _dragIndex;
                int slot = SlotAt(layout.List, e.Y);
                EndDrag();
                MoveEntry(from, slot);
                break;
            case Target.List or Target.Grip when !_isPanning:
                int item = ItemAt(layout.List, e.Y);
                if (item >= 0 && item == _pressIndex) Toggle(item, fromKeyboard: false);
                break;
            case Target.Close when sameTarget:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Target.ShowAll when sameTarget:
                ShowAll();
                break;
            case Target.Reset when sameTarget:
                Reset();
                break;
        }

        _isPanning = false;
        _pressIndex = -1;
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override void OnPointerCancel(KumikoPointerEventArgs e)
    {
        _pressTarget = Target.None;
        _pressIndex = -1;
        _isPanning = false;
        EndDrag();
        InvalidateVisual();
    }

    /// <summary>The mouse wheel or a trackpad scrolls the list.</summary>
    public override bool OnScroll(KumikoPointerEventArgs e)
    {
        EnsureEntries();
        ScrollTo(_scrollOffset - e.GetScrollPixelsY(_itemHeight * WheelRowsPerNotch));
        UpdateHover(Layout(), e.X, e.Y);
        return true;
    }

    private void PanList(float y)
    {
        if (!_isPanning && Math.Abs(y - _pressY) > DragThreshold) _isPanning = true;
        if (!_isPanning) return;
        ScrollTo(_scrollOffset + (_lastPanY - y));
        _lastPanY = y;
    }

    private void UpdateHover(in ChooserLayout layout, float x, float y)
    {
        var target = _pressTarget == Target.None || _pressTarget == TargetAt(layout, x, y) ? TargetAt(layout, x, y) : Target.None;
        int index = target is Target.List or Target.Grip && !_isPanning ? ItemAt(layout.List, y) : -1;
        if (target == _hoverTarget && index == _hoverIndex) return;
        _hoverTarget = target;
        _hoverIndex = index;
        InvalidateVisual();
    }

    private void ScrollTo(float offset)
    {
        float clamped = Math.Clamp(offset, 0, MaxScrollOffset);
        if (Math.Abs(clamped - _scrollOffset) < 0.01f) return;
        _scrollOffset = clamped;
        InvalidateVisual();
    }

    private void ScrollThumbTo(KumikoRect list, float thumbTop)
    {
        float offset = ScrollThumb.OffsetAt(thumbTop, list.Y, list.Height, ScrollbarThumb(list).Height, MaxScrollOffset);
        if (float.IsNaN(offset)) return;
        ScrollTo(offset);
    }

    private void EnsureItemVisible(int index)
    {
        float top = index * _itemHeight;
        if (top < _scrollOffset)
            ScrollTo(top);
        else if (top + _itemHeight > _scrollOffset + ListHeight)
            ScrollTo(top + _itemHeight - ListHeight);
    }

    /// <summary>A drag near the list's top or bottom edge scrolls it, a row's height per move at most.</summary>
    private void AutoScrollDuringDrag(KumikoRect list, float y)
    {
        float band = _itemHeight / 2;
        if (y < list.Y + band) ScrollTo(_scrollOffset - Math.Min(_itemHeight, list.Y + band - y));
        else if (y > list.Bottom - band) ScrollTo(_scrollOffset + Math.Min(_itemHeight, y - (list.Bottom - band)));
    }

    // ── Reordering ───────────────────────────────────────────────

    /// <summary>Whether the entry at <paramref name="index"/> can be dragged: no search, and its column may move.</summary>
    private bool CanDrag(int index) =>
        index >= 0 && index < _visible.Count && !IsSearching && _all[_visible[index]].AllowReorder;

    /// <summary>
    /// The drop slot for a pointer at <paramref name="y"/>: the gap above the row under it (upper half) or below it
    /// (lower half), as a position from 0 to the count; -1 outside the dragged entry's pane, where nothing drops.
    /// </summary>
    private int SlotAt(KumikoRect list, float y)
    {
        if (_dragIndex < 0 || _all.Count == 0) return -1;
        float content = y - list.Y + _scrollOffset;
        int slot = Math.Clamp((int)MathF.Floor(content / _itemHeight + 0.5f), 0, _all.Count);
        var (start, end) = PaneRange(_dragIndex);
        return slot >= start && slot <= end + 1 ? slot : -1;
    }

    /// <summary>The first and last positions in the list of the pane the entry at <paramref name="index"/> is in.</summary>
    private (int Start, int End) PaneRange(int index)
    {
        int pane = PaneOf(_all[index]);
        int start = index, end = index;
        while (start > 0 && PaneOf(_all[start - 1]) == pane) start--;
        while (end < _all.Count - 1 && PaneOf(_all[end + 1]) == pane) end++;
        return (start, end);
    }

    private static bool WouldMove(int from, int slot) => slot >= 0 && slot != from && slot != from + 1;

    private void EndDrag()
    {
        _dragIndex = -1;
        _dropSlot = -1;
    }

    /// <summary>
    /// Moves the entry at list position <paramref name="from"/> to <paramref name="slot"/> (a gap, 0 to the count) by
    /// moving its column next to the entry beside that gap in <see cref="KumikoSource.Columns"/>. No search may be
    /// active, so list positions are entry positions.
    /// </summary>
    private bool MoveEntry(int from, int slot)
    {
        if (IsSearching || from < 0 || from >= _all.Count || !WouldMove(from, slot)) return false;
        var (start, end) = PaneRange(from);
        if (slot < start || slot > end + 1) return false; // never across panes

        var column = _all[from];
        bool after = slot > from;
        var target = after ? _all[slot - 1] : _all[slot];
        var columns = _source.Columns;
        int fromIndex = IndexOf(columns, column);
        int targetIndex = IndexOf(columns, target);
        if (fromIndex < 0 || targetIndex < 0 || !column.AllowReorder) return false;
        if (BeforeChange is { } gate && !gate()) return false;

        int targetAfterRemove = targetIndex > fromIndex ? targetIndex - 1 : targetIndex;
        _source.ReorderColumn(fromIndex, after ? targetAfterRemove + 1 : targetAfterRemove);

        EnsureEntries();
        int position = _all.IndexOf(column);
        Announce(KumikoAnnouncements.ColumnMoved(_source.Strings, _labels[position], position + 1, _all.Count));
        InvalidateVisual();
        return true;
    }

    /// <summary>Alt+Up or Alt+Down: the focused entry moves one place within its pane.</summary>
    private void MoveFocusedEntry(int direction)
    {
        if (IsSearching || _focusIndex < 0 || _focusIndex >= _visible.Count) return;
        int from = _visible[_focusIndex];
        var column = _all[from];
        if (!column.AllowReorder) return;
        int slot = direction < 0 ? from - 1 : from + 2;
        if (!MoveEntry(from, slot)) return;
        _focusIndex = _visible.IndexOf(_all.IndexOf(column));
        if (_focusIndex >= 0) EnsureItemVisible(_focusIndex);
    }

    private static int IndexOf(IReadOnlyList<KumikoColumn> columns, KumikoColumn column)
    {
        for (int i = 0; i < columns.Count; i++)
            if (ReferenceEquals(columns[i], column)) return i;
        return -1;
    }

    // ── Actions ──────────────────────────────────────────────────

    /// <summary>Shows or hides the column of the visible entry at <paramref name="index"/>.</summary>
    private void Toggle(int index, bool fromKeyboard)
    {
        if (index < 0 || index >= _visible.Count) return;
        int entry = _visible[index];
        var column = _all[entry];
        bool show = !column.IsVisible;
        if (!show && !_source.CanHideColumn(column))
        {
            Announce(_source.Strings.ColumnChooserCannotHide);
            return;
        }
        if (BeforeChange is { } gate && !gate()) return;
        if (!_source.SetColumnVisible(column, show)) return;
        if (fromKeyboard) _focusIndex = index;
        Announce(KumikoAnnouncements.ColumnVisibilityChanged(_source.Strings, _labels[entry], show));
        InvalidateVisual();
    }

    private void ShowAll()
    {
        if (!CanShowAll()) return;
        if (BeforeChange is { } gate && !gate()) return;
        using (_source.BeginUpdate())
        {
            for (int i = 0; i < _all.Count; i++) _source.SetColumnVisible(_all[i], true);
        }
        Announce(_source.Strings.AnnounceAllColumnsShown);
        InvalidateVisual();
    }

    private void Reset()
    {
        if (!CanReset) return;
        if (BeforeChange is { } gate && !gate()) return;
        _source.ResetColumnLayout();
        Announce(_source.Strings.AnnounceColumnLayoutReset);
        InvalidateVisual();
    }

    private void Announce(string text)
    {
        if (!string.IsNullOrEmpty(text)) AnnouncementRequested?.Invoke(text);
    }

    // ── Keyboard input ───────────────────────────────────────────

    /// <summary>
    /// The search box has the keyboard first (Down moves into the list); in the list Up, Down, Home, End, PageUp and
    /// PageDown move, Space or Enter toggles and Alt+Up or Alt+Down moves the column, and typing goes back to the search;
    /// Enter or Space on a button presses it; Tab and Shift+Tab move between the search box, the list and the buttons;
    /// Escape clears a search, then asks to close. Every key is the panel's while it is open.
    /// </summary>
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        EnsureEntries();

        // An input method composing in the search box gets Enter and Escape first
        if (_stop == Stop.Search && _search.Composition != null && e.Key is KumikoKey.Enter or KumikoKey.Escape)
            return _search.OnKeyDown(e);

        switch (e.Key)
        {
            case KumikoKey.Tab:
                MoveStop(e.HasShift ? -1 : 1);
                return true;
            case KumikoKey.Escape:
                if (_search.Text.Length > 0)
                {
                    _search.Text = string.Empty;
                    _searchDirty = true;
                    EnsureSearchApplied();
                    SetStop(Stop.Search, announce: false);
                }
                else
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
                InvalidateVisual();
                return true;
        }

        return _stop switch
        {
            Stop.Search => HandleSearchKey(e),
            Stop.List => HandleListKey(e),
            _ => HandleButtonKey(e),
        };
    }

    private bool HandleSearchKey(KumikoKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KumikoKey.Down:
            case KumikoKey.Enter when _visible.Count > 0:
                if (_visible.Count == 0) return true;
                _focusIndex = Math.Max(0, Math.Min(_focusIndex, _visible.Count - 1));
                SetStop(Stop.List, announce: true);
                EnsureItemVisible(_focusIndex);
                return true;
            case KumikoKey.Enter or KumikoKey.Up or KumikoKey.PageUp or KumikoKey.PageDown:
                return true;
            case KumikoKey.Space:
                // Space arrives as KumikoKey.Space (not Character) on Apple platforms
                _search.InsertText(" ");
                return true;
        }
        _search.OnKeyDown(e);
        return true;
    }

    private bool HandleListKey(KumikoKeyEventArgs e)
    {
        if (_visible.Count == 0)
        {
            SetStop(Stop.Search, announce: false);
            return HandleSearchKey(e);
        }

        switch (e.Key)
        {
            case KumikoKey.Up when e.HasAlt:
                MoveFocusedEntry(-1);
                return true;
            case KumikoKey.Down when e.HasAlt:
                MoveFocusedEntry(1);
                return true;
            case KumikoKey.Up:
                if (_focusIndex <= 0) SetStop(Stop.Search, announce: true); // back to the search box
                else MoveFocus(_focusIndex - 1);
                return true;
            case KumikoKey.Down:
                MoveFocus(_focusIndex + 1);
                return true;
            case KumikoKey.Home:
                MoveFocus(0);
                return true;
            case KumikoKey.End:
                MoveFocus(_visible.Count - 1);
                return true;
            case KumikoKey.PageUp:
                MoveFocus(_focusIndex - PageSize);
                return true;
            case KumikoKey.PageDown:
                MoveFocus(_focusIndex + PageSize);
                return true;
            case KumikoKey.Space or KumikoKey.Enter:
            case KumikoKey.Character when e.Character == ' ' && string.IsNullOrEmpty(e.Text):
                Toggle(Math.Max(0, _focusIndex), fromKeyboard: true);
                return true;
            case KumikoKey.Character or KumikoKey.Backspace or KumikoKey.Delete:
                // Typing goes to the search box, which takes the keyboard back
                SetStop(Stop.Search, announce: false);
                _search.OnKeyDown(e);
                return true;
        }
        return true;
    }

    private bool HandleButtonKey(KumikoKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KumikoKey.Enter or KumikoKey.Space:
            case KumikoKey.Character when e.Character == ' ' && string.IsNullOrEmpty(e.Text):
                switch (_stop)
                {
                    case Stop.ShowAll: ShowAll(); break;
                    case Stop.Reset: Reset(); break;
                    case Stop.Close: CloseRequested?.Invoke(this, EventArgs.Empty); break;
                }
                InvalidateVisual();
                return true;
            case KumikoKey.Character or KumikoKey.Backspace:
                SetStop(Stop.Search, announce: false);
                _search.OnKeyDown(e);
                return true;
        }
        return true;
    }

    private int PageSize => Math.Max(1, (int)(ListHeight / _itemHeight) - 1);

    private void MoveFocus(int index)
    {
        if (_visible.Count == 0) return;
        int clamped = Math.Clamp(index, 0, _visible.Count - 1);
        bool changed = clamped != _focusIndex;
        _focusIndex = clamped;
        EnsureItemVisible(_focusIndex);
        if (changed) AnnounceFocusedEntry();
        InvalidateVisual();
    }

    /// <summary>Moves the keyboard to the next (or previous) place that can take it, wrapping around.</summary>
    private void MoveStop(int direction)
    {
        const int count = 5;
        var stop = _stop;
        for (int i = 0; i < count; i++)
        {
            stop = (Stop)(((int)stop + direction + count) % count);
            if (CanFocus(stop)) break;
        }
        if (stop == Stop.List && (_focusIndex < 0 || _focusIndex >= _visible.Count)) _focusIndex = 0;
        SetStop(stop, announce: true);
        if (stop == Stop.List) EnsureItemVisible(_focusIndex);
    }

    private bool CanFocus(Stop stop) => stop switch
    {
        Stop.List => _visible.Count > 0,
        Stop.ShowAll => CanShowAll(),
        Stop.Reset => CanReset,
        _ => true,
    };

    private void SetStop(Stop stop, bool announce)
    {
        _stop = stop;
        ApplyFocus();
        if (announce)
        {
            switch (stop)
            {
                case Stop.Search: Announce(_source.Strings.ColumnChooserSearchPlaceholder); break;
                case Stop.List: AnnounceFocusedEntry(); break;
                case Stop.ShowAll: Announce(_source.Strings.ColumnChooserShowAll); break;
                case Stop.Reset: Announce(_source.Strings.ColumnChooserReset); break;
                case Stop.Close: Announce(_source.Strings.ColumnChooserClose); break;
            }
        }
        InvalidateVisual();
    }

    private void AnnounceFocusedEntry()
    {
        if (_focusIndex < 0 || _focusIndex >= _visible.Count) return;
        int entry = _visible[_focusIndex];
        var column = _all[entry];
        bool canToggle = !column.IsVisible || _source.CanHideColumn(column);
        Announce(KumikoAnnouncements.ColumnChooserEntry(_source.Strings, _labels[entry], column.IsVisible, canToggle,
            _focusIndex + 1, _visible.Count));
    }

    // ── Focus ────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnGotFocus() => ApplyFocus();

    /// <inheritdoc />
    protected override void OnLostFocus() => ApplyFocus();

    private void ApplyFocus() => _search.IsFocused = IsFocused && _stop == Stop.Search;
}
