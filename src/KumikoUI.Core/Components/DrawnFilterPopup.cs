using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Custom-drawn Excel-style filter popup:
/// - sort ascending/descending;
/// - a search box (a <see cref="DrawnTextBox"/>) that narrows the value list;
/// - Select All / Deselect All for the values shown;
/// - a virtualized, scrollable checkbox list of the column's distinct values;
/// - OK / Cancel / Clear.
/// </summary>
/// <remarks>
/// The list scrolls by dragging (a press toggles its item only when released without moving past a small
/// threshold), by the mouse wheel, by dragging or clicking the scrollbar, and by the keyboard: Up/Down and
/// PageUp/PageDown move through the values, Space toggles the current one, Enter applies, Escape cancels.
/// Only the visible rows are drawn, and the search is applied once per frame however fast the user types.
/// </remarks>
public class DrawnFilterPopup : DrawnComponent
{
    private readonly KumikoColumn _column;
    private readonly KumikoSource _dataSource;
    private readonly DrawnTextBox _search;
    private List<FilterItem> _allItems = new();
    private List<int> _visible = new(); // indices into _allItems that match the search
    private string _appliedSearch = string.Empty;
    private bool _searchDirty;

    private float _scrollOffset;
    private int _hoveredItemIndex = -1;
    private int _focusIndex = -1; // the item the keyboard is on (index into _visible), -1: the search box

    // The press in progress
    private Target _pressTarget;
    private float _pressY;
    private float _lastPanY;
    private int _pressItem = -1;
    private bool _isPanning;
    private float _scrollbarGrab;

    // Layout constants
    private const float PopupWidth = 220f;
    private const float SearchBoxHeight = 30f;
    private const float ButtonRowHeight = 32f;
    private const float SortRowHeight = 28f;
    private const float CheckboxItemHeight = 24f;
    private const float CheckboxSize = 14f;
    private const float MaxListHeight = 250f;
    private const float PopupPadding = 6f;
    private const float SeparatorMargin = 4f;
    private const float ScrollbarHitWidth = 12f;
    private const float DragThreshold = 6f;
    private const int WheelRowsPerNotch = 3;

    private enum Target { None, FirstSortRow, SecondSortRow, Search, SelectAll, DeselectAll, List, Scrollbar, Ok, Cancel, Clear }

    private readonly record struct PopupLayout(
        KumikoRect Popup, KumikoRect FirstSortRow, KumikoRect SecondSortRow, KumikoRect Search,
        KumikoRect SelectAll, KumikoRect DeselectAll, KumikoRect List, KumikoRect Ok, KumikoRect Cancel, KumikoRect Clear,
        float TopSeparatorY, float BottomSeparatorY);

    // Styling
    /// <summary>Font used for item labels and button text.</summary>
    public KumikoFont Font { get; set; } = new("Default", 12);
    /// <summary>Smaller font used for secondary labels and helper text.</summary>
    public KumikoFont SmallFont { get; set; } = new("Default", 11);
    /// <summary>Primary text color for items and labels.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the popup.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border color of the popup.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Background color shown when an item is hovered.</summary>
    public KumikoColor HoverColor { get; set; } = new(0, 120, 215, 30);
    /// <summary>Accent color used for links, active controls, and highlights.</summary>
    public KumikoColor AccentColor { get; set; } = new(0, 120, 215);
    /// <summary>Background color of action buttons (OK).</summary>
    public KumikoColor ButtonColor { get; set; } = new(0, 120, 215);
    /// <summary>Text color used on action buttons.</summary>
    public KumikoColor ButtonTextColor { get; set; } = KumikoColor.White;
    /// <summary>Border color of the search text box when not focused.</summary>
    public KumikoColor SearchBorderColor { get; set; } = new(200, 200, 200);
    /// <summary>Border color of the search text box when focused.</summary>
    public KumikoColor SearchFocusBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of placeholder text in the search box.</summary>
    public KumikoColor PlaceholderColor { get; set; } = new(160, 160, 160);
    /// <summary>Color of horizontal separator lines between sections.</summary>
    public KumikoColor SeparatorColor { get; set; } = new(220, 220, 220);
    /// <summary>Color of the checkmark drawn inside checked checkboxes.</summary>
    public KumikoColor CheckmarkColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the text cursor in the search box.</summary>
    public KumikoColor CursorColor { get; set; } = new(0, 0, 0);
    /// <summary>Background of the search box (the popup background under a theme).</summary>
    public KumikoColor SearchBackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Outline of the item the keyboard is on (Up/Down).</summary>
    public KumikoColor KeyboardFocusColor { get; set; } = new(0, 120, 215);

    // Events
    /// <summary>Raised when the user confirms the filter selection (OK button).</summary>
    public event Action? FilterApplied;
    /// <summary>Raised when the user clears all filter criteria.</summary>
    public event Action? FilterCleared;
    /// <summary>Raised when the user cancels the filter popup without applying.</summary>
    public event Action? FilterCancelled;
    /// <summary>Raised when the user requests a sort direction from the popup.</summary>
    public event Action<KumikoSortDirection>? SortRequested;

    /// <summary>Raised when the search box is tapped and needs platform keyboard focus.</summary>
    public event Action? KeyboardFocusRequested;

    /// <summary>Initializes a new <see cref="DrawnFilterPopup"/> for the specified column and data source.</summary>
    /// <param name="column">The column whose values will populate the filter list.</param>
    /// <param name="dataSource">The data source used to extract unique column values.</param>
    public DrawnFilterPopup(KumikoColumn column, KumikoSource dataSource)
    {
        _column = column;
        _dataSource = dataSource;
        _search = new DrawnTextBox
        {
            Font = SmallFont,
            Padding = 6f,
            InitialSelectionMode = Editing.EditTextSelectionMode.CursorAtEnd,
        };
        _search.ValueChanged += (_, _) => { _searchDirty = true; InvalidateVisual(); };
        _search.RedrawRequested += InvalidateVisual;
        LoadUniqueValues();
        Bounds = new KumikoRect(0, 0, PopupWidth, ComputePopupHeight());
    }

    /// <summary>The search box's text-input surface, for a host's native text-input adapter.</summary>
    public ITextInputSession SearchTextInput => _search;

    /// <summary>The search text.</summary>
    public string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    /// <summary>The number of distinct values in the column.</summary>
    public int ValueCount => _allItems.Count;

    /// <summary>The values that match the search, in list order.</summary>
    public IReadOnlyList<string> VisibleValues
    {
        get
        {
            EnsureSearchApplied();
            return _visible.Select(i => _allItems[i].Value).ToList();
        }
    }

    /// <summary>The checked values (whether or not they match the search).</summary>
    public IReadOnlyList<string> CheckedValues => _allItems.Where(i => i.IsChecked).Select(i => i.Value).ToList();

    /// <summary>The vertical scroll offset of the value list, in pixels.</summary>
    public float ListScrollOffset => _scrollOffset;

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the popup
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        // Derive popup colors from the grid theme
        BackgroundColor = style.BackgroundColor;
        TextColor = style.CellTextColor;
        AccentColor = style.AccentColor;
        ButtonColor = style.AccentColor;
        CheckmarkColor = style.AccentColor;
        CursorColor = style.CellTextColor;
        BorderColor = style.HeaderBorderColor;
        SearchFocusBorderColor = style.AccentColor;
        SearchBackgroundColor = style.BackgroundColor;
        KeyboardFocusColor = style.AccentColor;

        // Derive secondary colors from primary theme colors
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        // Hover: accent with low alpha
        HoverColor = new KumikoColor(style.AccentColor.R, style.AccentColor.G, style.AccentColor.B, 30);

        // Search border: between grid line and border
        SearchBorderColor = style.GridLineColor;

        // Separator: use grid line color
        SeparatorColor = style.GridLineColor;

        // Placeholder: midpoint between text and background
        PlaceholderColor = new KumikoColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));

        // Button text: contrast against accent
        double accentLum = (0.299 * style.AccentColor.R + 0.587 * style.AccentColor.G + 0.114 * style.AccentColor.B) / 255.0;
        ButtonTextColor = accentLum > 0.5 ? new KumikoColor(0, 0, 0) : new KumikoColor(255, 255, 255);

        _search.SelectionHighlightColor = style.AccentColor.WithAlpha(80);
    }

    // ── Data ─────────────────────────────────────────────────────

    private void LoadUniqueValues()
    {
        var uniqueValues = _dataSource.GetUniqueColumnValues(_column);

        // Determine which values are currently selected
        var currentFilter = _column.ActiveFilter;
        // Under the source's text rules, like the filter itself: "oslo" checks "Oslo" when case is ignored.
        HashSet<string>? selectedValues = currentFilter?.SelectedValues is { } selected
            ? new HashSet<string>(selected, _dataSource.Comparer.TextComparer)
            : null;

        _allItems = new List<FilterItem>(uniqueValues.Count);
        foreach (var v in uniqueValues)
        {
            _allItems.Add(new FilterItem
            {
                DisplayText = string.IsNullOrEmpty(v) ? _dataSource.Strings.FilterBlanks : v,
                Value = v,
                IsChecked = selectedValues == null || selectedValues.Contains(v)
            });
        }

        _visible = Enumerable.Range(0, _allItems.Count).ToList();
        _appliedSearch = string.Empty;
        _searchDirty = false;
    }

    /// <summary>
    /// Apply the search text if it changed since the last time: once per frame (or per input that needs
    /// the list), however many keystrokes arrived. Text that extends the previous search narrows the
    /// current matches instead of scanning every value again.
    /// </summary>
    private void EnsureSearchApplied()
    {
        if (!_searchDirty) return;
        _searchDirty = false;

        string text = _search.Text;
        if (text == _appliedSearch) return;

        var comparer = _dataSource.Comparer;
        IEnumerable<int> candidates = _appliedSearch.Length > 0 && comparer.TextStartsWith(text, _appliedSearch)
            ? _visible
            : Enumerable.Range(0, _allItems.Count);

        _visible = text.Length == 0
            ? Enumerable.Range(0, _allItems.Count).ToList()
            : candidates.Where(i => comparer.TextContains(_allItems[i].DisplayText, text)).ToList();
        _appliedSearch = text;
        _scrollOffset = 0;
        _focusIndex = -1;
        _hoveredItemIndex = -1;
        Bounds = new KumikoRect(Bounds.X, Bounds.Y, PopupWidth, ComputePopupHeight());
    }

    private float ContentHeight => _visible.Count * CheckboxItemHeight;

    private float ListViewHeight => Math.Min(ContentHeight, MaxListHeight);

    private float MaxScrollOffset => Math.Max(0, ContentHeight - ListViewHeight);

    private float ComputePopupHeight()
    {
        float h = PopupPadding;
        h += SortRowHeight;               // Sort A-Z / Z-A row
        h += SeparatorMargin * 2 + 1;     // Separator
        h += SearchBoxHeight;             // Search box
        h += SeparatorMargin;
        h += ButtonRowHeight;             // Select All / Deselect All
        h += ListViewHeight;              // Checkbox list
        h += SeparatorMargin * 2 + 1;     // Separator
        h += ButtonRowHeight;             // OK / Cancel / Clear row
        h += PopupPadding;
        return h;
    }

    private PopupLayout Layout()
    {
        float x = Bounds.X + PopupPadding;
        float y = Bounds.Y + PopupPadding;
        float width = PopupWidth - PopupPadding * 2;
        float half = width / 2;

        var sortAsc = new KumikoRect(x, y, half, SortRowHeight);
        var sortDesc = new KumikoRect(x + half, y, half, SortRowHeight);
        y += SortRowHeight + SeparatorMargin;
        float topSeparator = y;
        y += 1 + SeparatorMargin;

        var search = new KumikoRect(x, y + 2, width, SearchBoxHeight - 4);
        y += SearchBoxHeight + SeparatorMargin;

        var selectAll = new KumikoRect(x, y, half, ButtonRowHeight);
        var deselectAll = new KumikoRect(x + half, y, half, ButtonRowHeight);
        y += ButtonRowHeight;

        var list = new KumikoRect(x, y, width, ListViewHeight);
        y += list.Height + SeparatorMargin;
        float bottomSeparator = y;
        y += 1 + SeparatorMargin;

        float buttonWidth = (width - 8) / 3;
        float buttonHeight = ButtonRowHeight - 6;
        var ok = new KumikoRect(x, y + 3, buttonWidth, buttonHeight);
        var cancel = new KumikoRect(x + buttonWidth + 4, y + 3, buttonWidth, buttonHeight);
        var clear = new KumikoRect(x + (buttonWidth + 4) * 2, y + 3, buttonWidth, buttonHeight);

        return new PopupLayout(new KumikoRect(Bounds.X, Bounds.Y, PopupWidth, ComputePopupHeight()),
            sortAsc, sortDesc, search, selectAll, deselectAll, list, ok, cancel, clear, topSeparator, bottomSeparator);
    }

    // ── Drawing ──────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (!IsVisible) return;

        EnsureSearchApplied();
        var layout = Layout();
        var popupRect = layout.Popup;

        // Background and border (the popup manager draws the shadow)
        ctx.FillRect(popupRect, new KumikoPaint { Color = BackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(popupRect, new KumikoPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

        float x = layout.Search.X;
        float contentWidth = layout.Search.Width;
        var separatorPaint = new KumikoPaint { Color = SeparatorColor, StrokeWidth = 1, Style = PaintStyle.Stroke };

        DrawSortRow(ctx, layout);
        ctx.DrawLine(x, layout.TopSeparatorY, x + contentWidth, layout.TopSeparatorY, separatorPaint);
        DrawSearchBox(ctx, layout.Search);
        DrawSelectAllRow(ctx, layout);
        DrawCheckboxList(ctx, layout.List);
        ctx.DrawLine(x, layout.BottomSeparatorY, x + contentWidth, layout.BottomSeparatorY, separatorPaint);
        DrawActionButtons(ctx, layout);
    }

    private void DrawSortRow(IDrawingContext ctx, PopupLayout layout)
    {
        var textPaint = new KumikoPaint { Color = AccentColor, Font = SmallFont, IsAntiAlias = true };
        var iconPaint = new KumikoPaint { Color = AccentColor, Style = PaintStyle.Fill, IsAntiAlias = true };

        // The first row sorts in the column's first header-tap direction, the second row in the opposite one.
        var firstDir = _column.FirstSortDirection;
        var secondDir = KumikoColumn.Opposite(firstDir);

        var firstRect = layout.FirstSortRow;
        if (_hoveredItemIndex == -1000)
            ctx.FillRect(firstRect, new KumikoPaint { Color = HoverColor, Style = PaintStyle.Fill });
        DrawSortArrow(ctx, firstRect.X + 8, firstRect.Y + SortRowHeight / 2, firstDir, iconPaint);
        ctx.DrawTextInRect(_dataSource.Strings.SortLabel(_column.ColumnType, firstDir), new KumikoRect(firstRect.X + 18, firstRect.Y, firstRect.Width - 18, SortRowHeight),
            textPaint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

        var secondRect = layout.SecondSortRow;
        if (_hoveredItemIndex == -1001)
            ctx.FillRect(secondRect, new KumikoPaint { Color = HoverColor, Style = PaintStyle.Fill });
        DrawSortArrow(ctx, secondRect.X + 8, secondRect.Y + SortRowHeight / 2, secondDir, iconPaint);
        ctx.DrawTextInRect(_dataSource.Strings.SortLabel(_column.ColumnType, secondDir), new KumikoRect(secondRect.X + 18, secondRect.Y, secondRect.Width - 18, SortRowHeight),
            textPaint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
    }

    private static void DrawSortArrow(IDrawingContext ctx, float x, float centerY, KumikoSortDirection direction, KumikoPaint paint)
    {
        // A filled triangle (#118): pointing down for descending, up otherwise
        if (direction == KumikoSortDirection.Descending)
            GlyphDrawing.FillTriangleDown(ctx, x + 4, centerY, 4, 4, paint);
        else
            GlyphDrawing.FillTriangleUp(ctx, x + 4, centerY, 4, 4, paint);
    }

    private void DrawSearchBox(IDrawingContext ctx, KumikoRect searchRect)
    {
        // The search box is a DrawnTextBox styled from this popup's (themed) colors
        _search.Bounds = searchRect;
        _search.Font = SmallFont;
        _search.TextColor = TextColor;
        _search.CursorColor = CursorColor;
        _search.BackgroundColor = SearchBackgroundColor;
        _search.EditingBackgroundColor = SearchBackgroundColor;
        _search.BorderColor = SearchBorderColor;
        _search.FocusedBorderColor = SearchFocusBorderColor;
        _search.BorderWidth = 1f;
        _search.OnDraw(ctx);

        if (_search.Text.Length == 0)
        {
            ctx.DrawTextInRect(_dataSource.Strings.FilterSearchPlaceholder, new KumikoRect(searchRect.X + 8, searchRect.Y, searchRect.Width - 12, searchRect.Height),
                new KumikoPaint { Color = PlaceholderColor, Font = SmallFont, IsAntiAlias = true },
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }
    }

    private void DrawSelectAllRow(IDrawingContext ctx, PopupLayout layout)
    {
        var textPaint = new KumikoPaint { Color = AccentColor, Font = SmallFont, IsAntiAlias = true };

        // Select All
        var selectAllRect = layout.SelectAll;
        if (_hoveredItemIndex == -2000)
            ctx.FillRect(selectAllRect, new KumikoPaint { Color = HoverColor, Style = PaintStyle.Fill });
        // Draw checked checkbox icon
        float cbIX = selectAllRect.X + 8;
        float cbIY = selectAllRect.Y + (ButtonRowHeight - 12) / 2;
        ctx.DrawRect(new KumikoRect(cbIX, cbIY, 12, 12), new KumikoPaint { Color = AccentColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        ctx.FillRect(new KumikoRect(cbIX + 2, cbIY + 2, 8, 8), new KumikoPaint { Color = AccentColor, Style = PaintStyle.Fill });
        GlyphDrawing.Chevron(ctx, cbIX + 2.5f, cbIY + 6, cbIX + 5, cbIY + 9, cbIX + 9.5f, cbIY + 3,
            new KumikoPaint { Color = ButtonTextColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        ctx.DrawTextInRect(_dataSource.Strings.FilterSelectAll, new KumikoRect(selectAllRect.X + 24, selectAllRect.Y, selectAllRect.Width - 24, ButtonRowHeight),
            textPaint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);

        // Deselect All
        var deselectAllRect = layout.DeselectAll;
        if (_hoveredItemIndex == -2001)
            ctx.FillRect(deselectAllRect, new KumikoPaint { Color = HoverColor, Style = PaintStyle.Fill });
        // Draw empty checkbox icon
        float cbIX2 = deselectAllRect.X + 8;
        ctx.DrawRect(new KumikoRect(cbIX2, cbIY, 12, 12), new KumikoPaint { Color = AccentColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        ctx.DrawTextInRect(_dataSource.Strings.FilterDeselectAll, new KumikoRect(deselectAllRect.X + 24, deselectAllRect.Y, deselectAllRect.Width - 24, ButtonRowHeight),
            textPaint, KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
    }

    private void DrawCheckboxList(IDrawingContext ctx, KumikoRect list)
    {
        if (list.Height <= 0) return;

        ctx.Save();
        ctx.ClipRect(new KumikoRect(list.X - 1, list.Y, list.Width + 2, list.Height));

        var textPaint = new KumikoPaint { Color = TextColor, Font = SmallFont, IsAntiAlias = true };
        var boxPaint = new KumikoPaint { Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true };
        var fillPaint = new KumikoPaint { Color = AccentColor, Style = PaintStyle.Fill };
        var checkPaint = new KumikoPaint { Color = ButtonTextColor, Style = PaintStyle.Stroke, StrokeWidth = 2f, IsAntiAlias = true };
        bool scrollable = MaxScrollOffset > 0;
        float textRight = scrollable ? ScrollbarHitWidth : 0;

        // Only the rows in view
        int first = Math.Max(0, (int)(_scrollOffset / CheckboxItemHeight));
        int last = Math.Min(_visible.Count - 1, (int)((_scrollOffset + list.Height) / CheckboxItemHeight));
        for (int i = first; i <= last; i++)
        {
            float itemY = list.Y + i * CheckboxItemHeight - _scrollOffset;
            var item = _allItems[_visible[i]];
            var itemRect = new KumikoRect(list.X, itemY, list.Width, CheckboxItemHeight);

            if (_hoveredItemIndex == i)
                ctx.FillRect(itemRect, new KumikoPaint { Color = HoverColor, Style = PaintStyle.Fill });
            if (_focusIndex == i)
                ctx.DrawRect(new KumikoRect(itemRect.X + 0.5f, itemRect.Y + 0.5f, itemRect.Width - 1, itemRect.Height - 1),
                    new KumikoPaint { Color = KeyboardFocusColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

            // Checkbox
            float cbX = list.X + 4;
            float cbY = itemY + (CheckboxItemHeight - CheckboxSize) / 2;
            boxPaint.Color = item.IsChecked ? CheckmarkColor : BorderColor;
            ctx.DrawRect(new KumikoRect(cbX, cbY, CheckboxSize, CheckboxSize), boxPaint);

            if (item.IsChecked)
            {
                fillPaint.Color = CheckmarkColor;
                ctx.FillRect(new KumikoRect(cbX + 2, cbY + 2, CheckboxSize - 4, CheckboxSize - 4), fillPaint);
                GlyphDrawing.Chevron(ctx,
                    cbX + 3, cbY + CheckboxSize / 2f,
                    cbX + CheckboxSize * 0.4f, cbY + CheckboxSize - 4,
                    cbX + CheckboxSize - 3, cbY + 3, checkPaint);
            }

            // Text
            var textRect = new KumikoRect(cbX + CheckboxSize + 6, itemY, list.Width - CheckboxSize - 14 - textRight, CheckboxItemHeight);
            ctx.DrawTextInRect(item.DisplayText, textRect, textPaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }

        // Scrollbar
        if (scrollable)
        {
            var thumb = ScrollbarThumb(list);
            ctx.FillRoundRect(new KumikoRect(list.Right - 5, thumb.Y, 4, thumb.Height), 2,
                new KumikoPaint { Color = PlaceholderColor.WithAlpha(_pressTarget == Target.Scrollbar ? (byte)220 : (byte)150), Style = PaintStyle.Fill });
        }

        ctx.Restore();
    }

    private KumikoRect ScrollbarThumb(KumikoRect list)
    {
        float thumbHeight = Math.Max(20, list.Height * (list.Height / ContentHeight));
        float max = MaxScrollOffset;
        float thumbY = list.Y + (max > 0 ? _scrollOffset / max : 0) * (list.Height - thumbHeight);
        return new KumikoRect(list.Right - ScrollbarHitWidth, thumbY, ScrollbarHitWidth, thumbHeight);
    }

    private void DrawActionButtons(IDrawingContext ctx, PopupLayout layout)
    {
        // OK button
        ctx.FillRoundRect(layout.Ok, 3, new KumikoPaint { Color = ButtonColor, Style = PaintStyle.Fill });
        ctx.DrawTextInRect(_dataSource.Strings.FilterOk, layout.Ok,
            new KumikoPaint { Color = ButtonTextColor, Font = SmallFont, IsAntiAlias = true },
            KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);

        // Cancel button
        ctx.DrawRoundRect(layout.Cancel, 3, new KumikoPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });
        ctx.DrawTextInRect(_dataSource.Strings.FilterCancel, layout.Cancel,
            new KumikoPaint { Color = TextColor, Font = SmallFont, IsAntiAlias = true },
            KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);

        // Clear button
        ctx.DrawRoundRect(layout.Clear, 3, new KumikoPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });
        ctx.DrawTextInRect(_dataSource.Strings.FilterClear, layout.Clear,
            new KumikoPaint { Color = AccentColor, Font = SmallFont, IsAntiAlias = true },
            KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
    }

    // ── Hit testing ──────────────────────────────────────────────

    /// <inheritdoc />
    public override bool HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled) return false;
        EnsureSearchApplied();
        return new KumikoRect(Bounds.X, Bounds.Y, PopupWidth, ComputePopupHeight()).Contains(x, y);
    }

    private Target TargetAt(PopupLayout layout, float x, float y)
    {
        if (layout.FirstSortRow.Contains(x, y)) return Target.FirstSortRow;
        if (layout.SecondSortRow.Contains(x, y)) return Target.SecondSortRow;
        if (layout.Search.Contains(x, y)) return Target.Search;
        if (layout.SelectAll.Contains(x, y)) return Target.SelectAll;
        if (layout.DeselectAll.Contains(x, y)) return Target.DeselectAll;
        if (layout.List.Contains(x, y))
            return MaxScrollOffset > 0 && x >= layout.List.Right - ScrollbarHitWidth ? Target.Scrollbar : Target.List;
        if (layout.Ok.Contains(x, y)) return Target.Ok;
        if (layout.Cancel.Contains(x, y)) return Target.Cancel;
        if (layout.Clear.Contains(x, y)) return Target.Clear;
        return Target.None;
    }

    /// <summary>The index (into the visible values) of the row at a viewport y, or -1.</summary>
    private int ItemAt(KumikoRect list, float y)
    {
        if (y < list.Y || y >= list.Bottom) return -1;
        int index = (int)((y - list.Y + _scrollOffset) / CheckboxItemHeight);
        return index >= 0 && index < _visible.Count ? index : -1;
    }

    // ── Pointer input ────────────────────────────────────────────

    /// <summary>
    /// A press starts a gesture on what it lands on: buttons act on release over the same button, the
    /// list toggles an item on release unless the pointer moved (then it scrolls), the scrollbar drags,
    /// and the search box places its caret.
    /// </summary>
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        EnsureSearchApplied();
        var layout = Layout();

        _pressTarget = TargetAt(layout, e.X, e.Y);
        _pressY = _lastPanY = e.Y;
        _isPanning = false;
        _pressItem = -1;

        switch (_pressTarget)
        {
            case Target.Search:
                _focusIndex = -1;
                _search.OnPointerDown(e);
                KeyboardFocusRequested?.Invoke();
                break;

            case Target.List:
                _pressItem = ItemAt(layout.List, e.Y);
                break;

            case Target.Scrollbar:
                var thumb = ScrollbarThumb(layout.List);
                if (e.Y < thumb.Y || e.Y > thumb.Bottom)
                    ScrollThumbTo(layout.List, e.Y - thumb.Height / 2); // jump: center the thumb on the press
                _scrollbarGrab = e.Y - ScrollbarThumb(layout.List).Y;
                break;
        }

        InvalidateVisual();
        return true; // Consume all presses inside the popup
    }

    /// <summary>Drag to scroll the list or the scrollbar, select in the search box, or show hover.</summary>
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;
        EnsureSearchApplied();
        var layout = Layout();

        switch (_pressTarget)
        {
            case Target.List:
                if (!_isPanning && Math.Abs(e.Y - _pressY) > DragThreshold)
                    _isPanning = true;
                if (_isPanning)
                {
                    ScrollTo(_scrollOffset + (_lastPanY - e.Y));
                    _lastPanY = e.Y;
                }
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
        var layout = Layout();
        var pressed = _pressTarget;
        _pressTarget = Target.None;
        bool sameTarget = TargetAt(layout, e.X, e.Y) == pressed;

        switch (pressed)
        {
            case Target.Search:
                _search.OnPointerUp(e);
                break;
            case Target.List when !_isPanning:
                int item = ItemAt(layout.List, e.Y);
                if (item >= 0 && item == _pressItem)
                {
                    var filterItem = _allItems[_visible[item]];
                    filterItem.IsChecked = !filterItem.IsChecked;
                }
                break;
            case Target.FirstSortRow when sameTarget:
                SortRequested?.Invoke(_column.FirstSortDirection);
                break;
            case Target.SecondSortRow when sameTarget:
                SortRequested?.Invoke(KumikoColumn.Opposite(_column.FirstSortDirection));
                break;
            case Target.SelectAll when sameTarget:
                SelectAll();
                break;
            case Target.DeselectAll when sameTarget:
                DeselectAll();
                break;
            case Target.Ok when sameTarget:
                ApplyFilter();
                break;
            case Target.Cancel when sameTarget:
                Cancel();
                break;
            case Target.Clear when sameTarget:
                ClearFilter();
                break;
        }

        _isPanning = false;
        _pressItem = -1;
        InvalidateVisual();
        return true;
    }

    /// <summary>The mouse wheel scrolls the value list.</summary>
    public override bool OnScroll(KumikoPointerEventArgs e)
    {
        EnsureSearchApplied();
        ScrollTo(_scrollOffset - e.ScrollDeltaY * CheckboxItemHeight * WheelRowsPerNotch);
        UpdateHover(Layout(), e.X, e.Y);
        return true;
    }

    private void UpdateHover(PopupLayout layout, float x, float y)
    {
        int old = _hoveredItemIndex;
        _hoveredItemIndex = TargetAt(layout, x, y) switch
        {
            Target.FirstSortRow => -1000,
            Target.SecondSortRow => -1001,
            Target.SelectAll => -2000,
            Target.DeselectAll => -2001,
            Target.List when !_isPanning => ItemAt(layout.List, y),
            _ => -1,
        };
        if (_hoveredItemIndex != old)
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
        float thumbHeight = ScrollbarThumb(list).Height;
        float track = list.Height - thumbHeight;
        if (track <= 0) return;
        ScrollTo((thumbTop - list.Y) / track * MaxScrollOffset);
    }

    private void EnsureItemVisible(int index)
    {
        float top = index * CheckboxItemHeight;
        if (top < _scrollOffset)
            ScrollTo(top);
        else if (top + CheckboxItemHeight > _scrollOffset + ListViewHeight)
            ScrollTo(top + CheckboxItemHeight - ListViewHeight);
    }

    // ── Keyboard input ───────────────────────────────────────────

    /// <summary>
    /// Up/Down and PageUp/PageDown move through the values, Space toggles the current one (or types a
    /// space in the search), Enter applies, Escape cancels; everything else edits the search text.
    /// </summary>
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        EnsureSearchApplied();

        // An input method composing in the search box gets Enter and Escape first
        if (_search.Composition != null && e.Key is KumikoKey.Enter or KumikoKey.Escape)
            return _search.OnKeyDown(e);

        switch (e.Key)
        {
            case KumikoKey.Escape:
                Cancel();
                return true;

            case KumikoKey.Enter:
                ApplyFilter();
                return true;

            case KumikoKey.Down:
                MoveFocus(_focusIndex < 0 ? 0 : _focusIndex + 1);
                return true;

            case KumikoKey.Up:
                if (_focusIndex <= 0) { _focusIndex = -1; InvalidateVisual(); } // back to the search box
                else MoveFocus(_focusIndex - 1);
                return true;

            case KumikoKey.PageDown:
                MoveFocus(Math.Max(0, _focusIndex) + PageSize);
                return true;

            case KumikoKey.PageUp:
                MoveFocus(Math.Max(0, _focusIndex - PageSize));
                return true;

            case KumikoKey.Space when _focusIndex >= 0:
                ToggleFocused();
                return true;

            case KumikoKey.Character when e.Character == ' ' && _focusIndex >= 0 && string.IsNullOrEmpty(e.Text):
                ToggleFocused();
                return true;

            case KumikoKey.Space:
                // Space arrives as KumikoKey.Space (not Character) on Apple platforms
                // because PressesBegan intercepts the spacebar before InsertText is called.
                _search.InsertText(" ");
                return true;
        }

        // Everything else edits the search text, which takes the keyboard back from the list
        _focusIndex = -1;
        return _search.OnKeyDown(e) || e.Key == KumikoKey.Character;
    }

    private int PageSize => Math.Max(1, (int)(ListViewHeight / CheckboxItemHeight) - 1);

    private void MoveFocus(int index)
    {
        if (_visible.Count == 0) return;
        _focusIndex = Math.Clamp(index, 0, _visible.Count - 1);
        EnsureItemVisible(_focusIndex);
        InvalidateVisual();
    }

    private void ToggleFocused()
    {
        var item = _allItems[_visible[_focusIndex]];
        item.IsChecked = !item.IsChecked;
        InvalidateVisual();
    }

    // ── Focus ────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnGotFocus() => _search.IsFocused = true;

    /// <inheritdoc />
    protected override void OnLostFocus() => _search.IsFocused = false;

    // ── Actions ──────────────────────────────────────────────────

    private void SelectAll()
    {
        foreach (int i in _visible)
            _allItems[i].IsChecked = true;
        InvalidateVisual();
    }

    private void DeselectAll()
    {
        foreach (int i in _visible)
            _allItems[i].IsChecked = false;
        InvalidateVisual();
    }

    private void ApplyFilter()
    {
        _search.CommitComposition();

        // Collect selected values
        var selectedValues = new HashSet<string>();
        bool allSelected = true;

        foreach (var item in _allItems)
        {
            if (item.IsChecked)
                selectedValues.Add(item.Value);
            else
                allSelected = false;
        }

        if (allSelected)
        {
            // All selected = no filter needed
            _dataSource.SetColumnFilter(_column, null);
        }
        else
        {
            var filter = new FilterDescription(_column)
            {
                SelectedValues = selectedValues
            };
            _dataSource.SetColumnFilter(_column, filter);
        }

        FilterApplied?.Invoke();
    }

    private void Cancel()
    {
        FilterCancelled?.Invoke();
    }

    private void ClearFilter()
    {
        _dataSource.SetColumnFilter(_column, null);

        // Reset all items to checked
        foreach (var item in _allItems)
            item.IsChecked = true;

        FilterCleared?.Invoke();
    }

    // ── Internal types ───────────────────────────────────────────

    private sealed class FilterItem
    {
        public string DisplayText { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = true;
    }
}
