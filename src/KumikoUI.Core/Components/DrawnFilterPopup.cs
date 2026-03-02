using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Custom-drawn Excel-style filter popup with:
/// - Search box to filter the value list
/// - Select All / Deselect All buttons
/// - Scrollable checkbox list of unique values
/// - Sort ascending/descending options
/// - OK / Cancel / Clear buttons
/// </summary>
public class DrawnFilterPopup : DrawnComponent
{
    private readonly DataGridColumn _column;
    private readonly DataGridSource _dataSource;
    private List<FilterItem> _allItems = new();
    private List<FilterItem> _filteredItems = new();
    private string _searchText = string.Empty;
    private int _searchCursorPosition;
    private long _searchCursorBlinkStart;
    private float _searchScrollOffset;
    private IDrawingContext? _measureContext;
    private float _scrollOffset;
    private float _maxScrollOffset;
    private bool _isPanning;
    private float _lastPanY;
    private int _hoveredItemIndex = -1;


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

    // Styling
    /// <summary>Font used for item labels and button text.</summary>
    public GridFont Font { get; set; } = new("Default", 12);
    /// <summary>Smaller font used for secondary labels and helper text.</summary>
    public GridFont SmallFont { get; set; } = new("Default", 11);
    /// <summary>Primary text color for items and labels.</summary>
    public GridColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the popup.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Border color of the popup.</summary>
    public GridColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Background color shown when an item is hovered.</summary>
    public GridColor HoverColor { get; set; } = new(0, 120, 215, 30);
    /// <summary>Accent color used for links, active controls, and highlights.</summary>
    public GridColor AccentColor { get; set; } = new(0, 120, 215);
    /// <summary>Background color of action buttons (OK).</summary>
    public GridColor ButtonColor { get; set; } = new(0, 120, 215);
    /// <summary>Text color used on action buttons.</summary>
    public GridColor ButtonTextColor { get; set; } = GridColor.White;
    /// <summary>Border color of the search text box when not focused.</summary>
    public GridColor SearchBorderColor { get; set; } = new(200, 200, 200);
    /// <summary>Border color of the search text box when focused.</summary>
    public GridColor SearchFocusBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of placeholder text in the search box.</summary>
    public GridColor PlaceholderColor { get; set; } = new(160, 160, 160);
    /// <summary>Color of horizontal separator lines between sections.</summary>
    public GridColor SeparatorColor { get; set; } = new(220, 220, 220);
    /// <summary>Color of the checkmark drawn inside checked checkboxes.</summary>
    public GridColor CheckmarkColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the text cursor in the search box.</summary>
    public GridColor CursorColor { get; set; } = new(0, 0, 0);

    // Events
    /// <summary>Raised when the user confirms the filter selection (OK button).</summary>
    public event Action? FilterApplied;
    /// <summary>Raised when the user clears all filter criteria.</summary>
    public event Action? FilterCleared;
    /// <summary>Raised when the user cancels the filter popup without applying.</summary>
    public event Action? FilterCancelled;
    /// <summary>Raised when the user requests a sort direction from the popup.</summary>
    public event Action<SortDirection>? SortRequested;

    /// <summary>Raised when the search box is tapped and needs platform keyboard focus.</summary>
    public event Action? KeyboardFocusRequested;

    /// <summary>Initializes a new <see cref="DrawnFilterPopup"/> for the specified column and data source.</summary>
    /// <param name="column">The column whose values will populate the filter list.</param>
    /// <param name="dataSource">The data source used to extract unique column values.</param>
    public DrawnFilterPopup(DataGridColumn column, DataGridSource dataSource)
    {
        _column = column;
        _dataSource = dataSource;
        LoadUniqueValues();
    }

    /// <summary>
    /// Applies theme colors from a <see cref="DataGridStyle"/> so the popup
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(DataGridStyle style)
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

        // Derive secondary colors from primary theme colors
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        // Hover: accent with low alpha
        HoverColor = new GridColor(style.AccentColor.R, style.AccentColor.G, style.AccentColor.B, 30);

        // Search border: between grid line and border
        SearchBorderColor = style.GridLineColor;

        // Separator: use grid line color
        SeparatorColor = style.GridLineColor;

        // Placeholder: midpoint between text and background
        PlaceholderColor = new GridColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));

        // Button text: contrast against accent
        double accentLum = (0.299 * style.AccentColor.R + 0.587 * style.AccentColor.G + 0.114 * style.AccentColor.B) / 255.0;
        ButtonTextColor = accentLum > 0.5 ? new GridColor(0, 0, 0) : new GridColor(255, 255, 255);
    }

    // ── Data loading ─────────────────────────────────────────────

    private void LoadUniqueValues()
    {
        var uniqueValues = _dataSource.GetUniqueColumnValues(_column);

        // Determine which values are currently selected
        var currentFilter = _column.ActiveFilter;
        HashSet<string>? selectedValues = currentFilter?.SelectedValues;

        _allItems = uniqueValues.Select(v => new FilterItem
        {
            DisplayText = string.IsNullOrEmpty(v) ? "(Blanks)" : v,
            Value = v,
            IsChecked = selectedValues == null || selectedValues.Contains(v)
        }).ToList();

        ApplySearch();
    }

    private void ApplySearch()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            _filteredItems = _allItems.ToList();
        }
        else
        {
            _filteredItems = _allItems
                .Where(i => i.DisplayText.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _scrollOffset = 0;
        ComputeMaxScroll();
    }



    private void ComputeMaxScroll()
    {
        float listContentHeight = _filteredItems.Count * CheckboxItemHeight;
        float listViewHeight = GetListViewHeight();
        _maxScrollOffset = Math.Max(0, listContentHeight - listViewHeight);
    }

    private float GetListViewHeight()
    {
        float totalContentHeight = _filteredItems.Count * CheckboxItemHeight;
        return Math.Min(totalContentHeight, MaxListHeight);
    }

    private float ComputePopupHeight()
    {
        float h = PopupPadding;
        h += SortRowHeight;               // Sort A-Z / Z-A row
        h += SeparatorMargin * 2 + 1;     // Separator
        h += SearchBoxHeight;             // Search box
        h += SeparatorMargin;
        h += ButtonRowHeight;             // Select All / Deselect All
        h += GetListViewHeight();         // Checkbox list
        h += SeparatorMargin * 2 + 1;     // Separator
        h += ButtonRowHeight;             // OK / Cancel / Clear row
        h += PopupPadding;
        return h;
    }

    // ── Drawing ──────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (!IsVisible) return;

        _measureContext = ctx;

        float popupHeight = ComputePopupHeight();
        var popupRect = new GridRect(Bounds.X, Bounds.Y, PopupWidth, popupHeight);

        // Update bounds to match actual size
        Bounds = popupRect;

        // Shadow
        ctx.FillRect(new GridRect(popupRect.X + 2, popupRect.Y + 2, popupRect.Width, popupRect.Height),
            new GridPaint { Color = new GridColor(0, 0, 0, 40), Style = PaintStyle.Fill });

        // Background
        ctx.FillRect(popupRect, new GridPaint { Color = BackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(popupRect, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

        float y = popupRect.Y + PopupPadding;
        float x = popupRect.X + PopupPadding;
        float contentWidth = PopupWidth - PopupPadding * 2;

        // Sort options
        DrawSortRow(ctx, x, y, contentWidth);
        y += SortRowHeight;

        // Separator
        y += SeparatorMargin;
        ctx.DrawLine(x, y, x + contentWidth, y,
            new GridPaint { Color = SeparatorColor, StrokeWidth = 1, Style = PaintStyle.Stroke });
        y += 1 + SeparatorMargin;

        // Search box
        DrawSearchBox(ctx, x, y, contentWidth);
        y += SearchBoxHeight + SeparatorMargin;

        // Select All / Deselect All
        DrawSelectAllRow(ctx, x, y, contentWidth);
        y += ButtonRowHeight;

        // Checkbox list (clipped)
        float listHeight = GetListViewHeight();
        DrawCheckboxList(ctx, x, y, contentWidth, listHeight);
        y += listHeight;

        // Separator
        y += SeparatorMargin;
        ctx.DrawLine(x, y, x + contentWidth, y,
            new GridPaint { Color = SeparatorColor, StrokeWidth = 1, Style = PaintStyle.Stroke });
        y += 1 + SeparatorMargin;

        // OK / Cancel / Clear buttons
        DrawActionButtons(ctx, x, y, contentWidth);
    }

    private void DrawSortRow(IDrawingContext ctx, float x, float y, float width)
    {
        float halfWidth = width / 2;
        var textPaint = new GridPaint { Color = AccentColor, Font = SmallFont, IsAntiAlias = true };
        var iconPaint = new GridPaint { Color = AccentColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true };

        // Sort A-Z button
        var sortAscRect = new GridRect(x, y, halfWidth, SortRowHeight);
        if (_hoveredItemIndex == -1000)
            ctx.FillRect(sortAscRect, new GridPaint { Color = HoverColor, Style = PaintStyle.Fill });
        // Draw up-arrow icon
        float arrowX = x + 8;
        float arrowCY = y + SortRowHeight / 2;
        ctx.DrawLine(arrowX + 4, arrowCY - 4, arrowX + 4, arrowCY + 4, iconPaint);
        ctx.DrawLine(arrowX + 1, arrowCY - 1, arrowX + 4, arrowCY - 4, iconPaint);
        ctx.DrawLine(arrowX + 7, arrowCY - 1, arrowX + 4, arrowCY - 4, iconPaint);
        var sortAscTextRect = new GridRect(x + 18, y, halfWidth - 18, SortRowHeight);
        ctx.DrawTextInRect("Sort A-Z", sortAscTextRect, textPaint,
            GridTextAlignment.Left, GridVerticalAlignment.Center);

        // Sort Z-A button
        var sortDescRect = new GridRect(x + halfWidth, y, halfWidth, SortRowHeight);
        if (_hoveredItemIndex == -1001)
            ctx.FillRect(sortDescRect, new GridPaint { Color = HoverColor, Style = PaintStyle.Fill });
        // Draw down-arrow icon
        float arrowX2 = x + halfWidth + 8;
        ctx.DrawLine(arrowX2 + 4, arrowCY + 4, arrowX2 + 4, arrowCY - 4, iconPaint);
        ctx.DrawLine(arrowX2 + 1, arrowCY + 1, arrowX2 + 4, arrowCY + 4, iconPaint);
        ctx.DrawLine(arrowX2 + 7, arrowCY + 1, arrowX2 + 4, arrowCY + 4, iconPaint);
        var sortDescTextRect = new GridRect(x + halfWidth + 18, y, halfWidth - 18, SortRowHeight);
        ctx.DrawTextInRect("Sort Z-A", sortDescTextRect, textPaint,
            GridTextAlignment.Left, GridVerticalAlignment.Center);
    }

    private void DrawSearchBox(IDrawingContext ctx, float x, float y, float width)
    {
        var searchRect = new GridRect(x, y + 2, width, SearchBoxHeight - 4);

        ctx.FillRect(searchRect, new GridPaint { Color = GridColor.White, Style = PaintStyle.Fill });
        ctx.DrawRect(searchRect, new GridPaint
        {
            Color = IsFocused ? SearchFocusBorderColor : SearchBorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = IsFocused ? 1.5f : 1f
        });

        float padding = 6f;
        float innerX = x + padding;
        float innerW = width - padding * 2;
        float innerY = y + 2;
        float innerH = SearchBoxHeight - 4;

        if (string.IsNullOrEmpty(_searchText) && !IsFocused)
        {
            // Draw magnifying glass icon + placeholder when empty & unfocused
            float mgX = x + 10;
            float mgCY = y + 2 + (SearchBoxHeight - 4) / 2;
            float mgR = 4;
            var mgPaint = new GridPaint { Color = PlaceholderColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true };
            ctx.DrawRoundRect(new GridRect(mgX - mgR, mgCY - mgR, mgR * 2, mgR * 2), mgR, mgPaint);
            ctx.DrawLine(mgX + mgR * 0.7f, mgCY + mgR * 0.7f, mgX + mgR + 3, mgCY + mgR + 3, mgPaint);

            var placeholderRect = new GridRect(x + 22, y + 2, width - 28, SearchBoxHeight - 4);
            ctx.DrawTextInRect("Search...", placeholderRect,
                new GridPaint { Color = PlaceholderColor, Font = SmallFont, IsAntiAlias = true },
                GridTextAlignment.Left, GridVerticalAlignment.Center);
            return;
        }

        // Clip to inner text area and apply scroll offset
        ctx.Save();
        ctx.ClipRect(new GridRect(innerX, innerY, innerW, innerH));
        ctx.Translate(-_searchScrollOffset, 0);

        var textPaint = new GridPaint { Color = TextColor, Font = SmallFont, IsAntiAlias = true };
        var fontMetrics = ctx.GetFontMetrics(textPaint);
        float textHeight = fontMetrics.TextHeight;
        float textBaseline = innerY + (innerH - textHeight) / 2 - fontMetrics.Ascent;

        if (_searchText.Length > 0)
        {
            ctx.DrawText(_searchText, innerX, textBaseline, textPaint);
        }
        else
        {
            // Focused but empty — show placeholder with cursor
            var placeholderRect = new GridRect(innerX, innerY, innerW, innerH);
            ctx.DrawTextInRect("Search...", placeholderRect,
                new GridPaint { Color = PlaceholderColor, Font = SmallFont, IsAntiAlias = true },
                GridTextAlignment.Left, GridVerticalAlignment.Center);
        }

        // Draw cursor
        if (IsFocused)
        {
            DrawSearchCursor(ctx, innerX, innerY, innerH, fontMetrics);
        }

        ctx.Restore();
    }

    private void DrawSearchCursor(IDrawingContext ctx, float textX, float y, float height, GridFontMetrics fontMetrics)
    {
        // Blink every 530ms — always visible for first 530ms after reset
        long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _searchCursorBlinkStart;
        if ((elapsed / 530) % 2 != 0) return;

        float cursorX = textX + MeasureSearchSubstring(0, _searchCursorPosition);

        float textHeight = fontMetrics.TextHeight;
        float textTop = y + (height - textHeight) / 2;
        float cursorTop = textTop + 1f;
        float cursorBottom = textTop + textHeight - 1f;

        var paint = new GridPaint { Color = CursorColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f };
        ctx.DrawLine(cursorX, cursorTop, cursorX, cursorBottom, paint);
    }

    private float MeasureSearchSubstring(int start, int end)
    {
        if (_measureContext == null || start >= end || end > _searchText.Length) return 0;
        string sub = _searchText.Substring(start, end - start);
        return _measureContext.MeasureText(sub, new GridPaint { Font = SmallFont }).Width;
    }

    private void EnsureSearchCursorVisible()
    {
        if (_measureContext == null) return;

        float cursorX = MeasureSearchSubstring(0, _searchCursorPosition);
        float padding = 6f;
        float visibleWidth = PopupWidth - PopupPadding * 2 - padding * 2;

        if (cursorX - _searchScrollOffset > visibleWidth)
            _searchScrollOffset = cursorX - visibleWidth + padding;
        else if (cursorX - _searchScrollOffset < 0)
            _searchScrollOffset = cursorX;

        if (_searchScrollOffset < 0) _searchScrollOffset = 0;
    }

    private int GetSearchCharIndexAtX(float screenX, float searchBoxX)
    {
        if (_measureContext == null) return 0;

        float padding = 6f;
        float localX = screenX - searchBoxX - PopupPadding - padding + _searchScrollOffset;
        if (localX <= 0) return 0;
        if (_searchText.Length == 0) return 0;

        float totalWidth = MeasureSearchSubstring(0, _searchText.Length);
        if (localX >= totalWidth) return _searchText.Length;

        for (int i = 0; i < _searchText.Length; i++)
        {
            float charEnd = MeasureSearchSubstring(0, i + 1);
            float charMid = (i > 0 ? MeasureSearchSubstring(0, i) : 0)
                + (charEnd - (i > 0 ? MeasureSearchSubstring(0, i) : 0)) / 2;
            if (localX < charMid)
                return i;
        }
        return _searchText.Length;
    }

    private void DrawSelectAllRow(IDrawingContext ctx, float x, float y, float width)
    {
        float halfWidth = width / 2;
        var textPaint = new GridPaint { Color = AccentColor, Font = SmallFont, IsAntiAlias = true };
        var iconPaint = new GridPaint { Color = AccentColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true };

        // Select All
        var selectAllRect = new GridRect(x, y, halfWidth, ButtonRowHeight);
        if (_hoveredItemIndex == -2000)
            ctx.FillRect(selectAllRect, new GridPaint { Color = HoverColor, Style = PaintStyle.Fill });
        // Draw checked checkbox icon
        float cbIX = x + 8;
        float cbIY = y + (ButtonRowHeight - 12) / 2;
        ctx.DrawRect(new GridRect(cbIX, cbIY, 12, 12), new GridPaint { Color = AccentColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        ctx.FillRect(new GridRect(cbIX + 2, cbIY + 2, 8, 8), new GridPaint { Color = AccentColor, Style = PaintStyle.Fill });
        ctx.DrawLine(cbIX + 2.5f, cbIY + 6, cbIX + 5, cbIY + 9, new GridPaint { Color = GridColor.White, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        ctx.DrawLine(cbIX + 5, cbIY + 9, cbIX + 9.5f, cbIY + 3, new GridPaint { Color = GridColor.White, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        var selectTextRect = new GridRect(x + 24, y, halfWidth - 24, ButtonRowHeight);
        ctx.DrawTextInRect("Select All", selectTextRect, textPaint,
            GridTextAlignment.Left, GridVerticalAlignment.Center);

        // Deselect All
        var deselectAllRect = new GridRect(x + halfWidth, y, halfWidth, ButtonRowHeight);
        if (_hoveredItemIndex == -2001)
            ctx.FillRect(deselectAllRect, new GridPaint { Color = HoverColor, Style = PaintStyle.Fill });
        // Draw empty checkbox icon
        float cbIX2 = x + halfWidth + 8;
        ctx.DrawRect(new GridRect(cbIX2, cbIY, 12, 12), new GridPaint { Color = AccentColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f, IsAntiAlias = true });
        var deselectTextRect = new GridRect(x + halfWidth + 24, y, halfWidth - 24, ButtonRowHeight);
        ctx.DrawTextInRect("Deselect All", deselectTextRect, textPaint,
            GridTextAlignment.Left, GridVerticalAlignment.Center);
    }

    private void DrawCheckboxList(IDrawingContext ctx, float x, float y, float width, float listHeight)
    {
        ctx.Save();
        ctx.ClipRect(new GridRect(x - 1, y, width + 2, listHeight));

        var textPaint = new GridPaint { Color = TextColor, Font = SmallFont, IsAntiAlias = true };

        for (int i = 0; i < _filteredItems.Count; i++)
        {
            float itemY = y + i * CheckboxItemHeight - _scrollOffset;
            if (itemY + CheckboxItemHeight < y || itemY > y + listHeight) continue;

            var item = _filteredItems[i];
            var itemRect = new GridRect(x, itemY, width, CheckboxItemHeight);

            // Hover highlight
            if (_hoveredItemIndex == i)
                ctx.FillRect(itemRect, new GridPaint { Color = HoverColor, Style = PaintStyle.Fill });

            // Checkbox
            float cbX = x + 4;
            float cbY = itemY + (CheckboxItemHeight - CheckboxSize) / 2;
            var cbRect = new GridRect(cbX, cbY, CheckboxSize, CheckboxSize);

            ctx.DrawRect(cbRect, new GridPaint
            {
                Color = item.IsChecked ? AccentColor : BorderColor,
                Style = PaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntiAlias = true
            });

            if (item.IsChecked)
            {
                // Draw checkmark
                ctx.FillRect(new GridRect(cbX + 2, cbY + 2, CheckboxSize - 4, CheckboxSize - 4),
                    new GridPaint { Color = AccentColor, Style = PaintStyle.Fill });

                var checkPaint = new GridPaint
                {
                    Color = GridColor.White,
                    Style = PaintStyle.Stroke,
                    StrokeWidth = 2f,
                    IsAntiAlias = true
                };
                ctx.DrawLine(cbX + 3, cbY + CheckboxSize / 2f, cbX + CheckboxSize * 0.4f, cbY + CheckboxSize - 4, checkPaint);
                ctx.DrawLine(cbX + CheckboxSize * 0.4f, cbY + CheckboxSize - 4, cbX + CheckboxSize - 3, cbY + 3, checkPaint);
            }

            // Text
            var textRect = new GridRect(cbX + CheckboxSize + 6, itemY, width - CheckboxSize - 14, CheckboxItemHeight);
            ctx.DrawTextInRect(item.DisplayText, textRect, textPaint,
                GridTextAlignment.Left, GridVerticalAlignment.Center);
        }

        // Scrollbar
        if (_maxScrollOffset > 0)
        {
            float totalHeight = _filteredItems.Count * CheckboxItemHeight;
            float scrollbarHeight = Math.Max(20, listHeight * (listHeight / totalHeight));
            float scrollbarY = y + (_scrollOffset / _maxScrollOffset) * (listHeight - scrollbarHeight);
            float scrollbarX = x + width - 4;

            ctx.FillRect(new GridRect(scrollbarX, scrollbarY, 3, scrollbarHeight),
                new GridPaint { Color = new GridColor(180, 180, 180, 120), Style = PaintStyle.Fill });
        }

        ctx.Restore();
    }

    private void DrawActionButtons(IDrawingContext ctx, float x, float y, float width)
    {
        float buttonWidth = (width - 8) / 3;
        float buttonHeight = ButtonRowHeight - 6;
        float buttonY = y + 3;

        // OK button
        var okRect = new GridRect(x, buttonY, buttonWidth, buttonHeight);
        ctx.FillRoundRect(okRect, 3, new GridPaint { Color = ButtonColor, Style = PaintStyle.Fill });
        ctx.DrawTextInRect("OK", okRect,
            new GridPaint { Color = ButtonTextColor, Font = SmallFont, IsAntiAlias = true },
            GridTextAlignment.Center, GridVerticalAlignment.Center);

        // Cancel button
        var cancelRect = new GridRect(x + buttonWidth + 4, buttonY, buttonWidth, buttonHeight);
        ctx.DrawRoundRect(cancelRect, 3, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });
        ctx.DrawTextInRect("Cancel", cancelRect,
            new GridPaint { Color = TextColor, Font = SmallFont, IsAntiAlias = true },
            GridTextAlignment.Center, GridVerticalAlignment.Center);

        // Clear button
        var clearRect = new GridRect(x + (buttonWidth + 4) * 2, buttonY, buttonWidth, buttonHeight);
        ctx.DrawRoundRect(clearRect, 3, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });
        ctx.DrawTextInRect("Clear", clearRect,
            new GridPaint { Color = AccentColor, Font = SmallFont, IsAntiAlias = true },
            GridTextAlignment.Center, GridVerticalAlignment.Center);
    }

    // ── Hit testing ──────────────────────────────────────────────

    /// <inheritdoc />
    public override bool HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled) return false;
        float popupHeight = ComputePopupHeight();
        var popupRect = new GridRect(Bounds.X, Bounds.Y, PopupWidth, popupHeight);
        return popupRect.Contains(x, y);
    }

    // ── Input ────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        float relX = e.X - Bounds.X - PopupPadding;
        float relY = e.Y - Bounds.Y - PopupPadding;
        float contentWidth = PopupWidth - PopupPadding * 2;

        // Sort row
        if (relY >= 0 && relY < SortRowHeight)
        {
            if (relX < contentWidth / 2)
                SortRequested?.Invoke(SortDirection.Ascending);
            else
                SortRequested?.Invoke(SortDirection.Descending);
            return true;
        }

        float y = SortRowHeight + SeparatorMargin * 2 + 1;

        // Search box — request keyboard focus and set cursor position
        if (relY >= y && relY < y + SearchBoxHeight)
        {
            _searchCursorPosition = GetSearchCharIndexAtX(e.X, Bounds.X);
            ResetSearchCursorBlink();
            KeyboardFocusRequested?.Invoke();
            InvalidateVisual();
            return true;
        }
        y += SearchBoxHeight + SeparatorMargin;

        // Select All / Deselect All row
        if (relY >= y && relY < y + ButtonRowHeight)
        {
            if (relX < contentWidth / 2)
                SelectAll();
            else
                DeselectAll();
            return true;
        }
        y += ButtonRowHeight;

        // Checkbox list area
        float listHeight = GetListViewHeight();
        if (relY >= y && relY < y + listHeight)
        {
            float listRelY = relY - y + _scrollOffset;
            int itemIndex = (int)(listRelY / CheckboxItemHeight);
            if (itemIndex >= 0 && itemIndex < _filteredItems.Count)
            {
                var item = _filteredItems[itemIndex];
                item.IsChecked = !item.IsChecked;
                InvalidateVisual();
            }

            _isPanning = false;
            _lastPanY = e.Y;
            return true;
        }
        y += listHeight + SeparatorMargin * 2 + 1;

        // Action buttons row
        if (relY >= y && relY < y + ButtonRowHeight)
        {
            float buttonWidth = (contentWidth - 8) / 3;
            if (relX < buttonWidth)
                ApplyFilter();
            else if (relX < buttonWidth * 2 + 4)
                Cancel();
            else
                ClearFilter();
            return true;
        }

        return true; // Consume all clicks inside popup
    }

    /// <inheritdoc />
    public override bool OnPointerMove(GridPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        float relX = e.X - Bounds.X - PopupPadding;
        float relY = e.Y - Bounds.Y - PopupPadding;
        float contentWidth = PopupWidth - PopupPadding * 2;

        int oldHovered = _hoveredItemIndex;
        _hoveredItemIndex = -1;

        // Sort row hover
        if (relY >= 0 && relY < SortRowHeight)
        {
            _hoveredItemIndex = relX < contentWidth / 2 ? -1000 : -1001;
        }
        else
        {
            float y = SortRowHeight + SeparatorMargin * 2 + 1 + SearchBoxHeight + SeparatorMargin;

            // Select All / Deselect All
            if (relY >= y && relY < y + ButtonRowHeight)
            {
                _hoveredItemIndex = relX < contentWidth / 2 ? -2000 : -2001;
            }
            else
            {
                y += ButtonRowHeight;
                float listHeight = GetListViewHeight();

                // Checkbox list hover
                if (relY >= y && relY < y + listHeight)
                {
                    // Handle scrolling via drag
                    if (_isPanning)
                    {
                        float dy = _lastPanY - e.Y;
                        _scrollOffset = Math.Clamp(_scrollOffset + dy, 0, _maxScrollOffset);
                        _lastPanY = e.Y;
                    }

                    float listRelY = relY - y + _scrollOffset;
                    int itemIndex = (int)(listRelY / CheckboxItemHeight);
                    if (itemIndex >= 0 && itemIndex < _filteredItems.Count)
                        _hoveredItemIndex = itemIndex;
                }
            }
        }

        if (_hoveredItemIndex != oldHovered)
            InvalidateVisual();

        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        if (e.Key == GridKey.Escape)
        {
            Cancel();
            return true;
        }

        if (e.Key == GridKey.Enter)
        {
            ApplyFilter();
            return true;
        }

        // Cursor movement
        if (e.Key == GridKey.Left)
        {
            if (_searchCursorPosition > 0)
            {
                _searchCursorPosition--;
                ResetSearchCursorBlink();
                EnsureSearchCursorVisible();
                InvalidateVisual();
            }
            return true;
        }

        if (e.Key == GridKey.Right)
        {
            if (_searchCursorPosition < _searchText.Length)
            {
                _searchCursorPosition++;
                ResetSearchCursorBlink();
                EnsureSearchCursorVisible();
                InvalidateVisual();
            }
            return true;
        }

        if (e.Key == GridKey.Home)
        {
            _searchCursorPosition = 0;
            ResetSearchCursorBlink();
            EnsureSearchCursorVisible();
            InvalidateVisual();
            return true;
        }

        if (e.Key == GridKey.End)
        {
            _searchCursorPosition = _searchText.Length;
            ResetSearchCursorBlink();
            EnsureSearchCursorVisible();
            InvalidateVisual();
            return true;
        }

        // Handle search text input
        if (e.Key == GridKey.Backspace)
        {
            if (_searchCursorPosition > 0)
            {
                _searchText = _searchText.Remove(_searchCursorPosition - 1, 1);
                _searchCursorPosition--;
                ResetSearchCursorBlink();
                EnsureSearchCursorVisible();
                ApplySearch();
                InvalidateVisual();
            }
            return true;
        }

        if (e.Key == GridKey.Delete)
        {
            if (_searchCursorPosition < _searchText.Length)
            {
                _searchText = _searchText.Remove(_searchCursorPosition, 1);
                ResetSearchCursorBlink();
                EnsureSearchCursorVisible();
                ApplySearch();
                InvalidateVisual();
            }
            return true;
        }

        // Space key arrives as GridKey.Space (not Character) on Apple platforms
        // because PressesBegan intercepts the spacebar before InsertText is called.
        if (e.Key == GridKey.Space)
        {
            InsertSearchText(" ");
            return true;
        }

        if (e.Key == GridKey.Character && e.Character.HasValue)
        {
            InsertSearchText(e.Character.Value.ToString());
            return true;
        }

        return false;
    }

    private void InsertSearchText(string text)
    {
        _searchText = _searchText.Insert(_searchCursorPosition, text);
        _searchCursorPosition += text.Length;
        ResetSearchCursorBlink();
        EnsureSearchCursorVisible();
        ApplySearch();
        InvalidateVisual();
    }

    private void ResetSearchCursorBlink()
    {
        _searchCursorBlinkStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    // ── Actions ──────────────────────────────────────────────────

    private void SelectAll()
    {
        foreach (var item in _filteredItems)
            item.IsChecked = true;
        InvalidateVisual();
    }

    private void DeselectAll()
    {
        foreach (var item in _filteredItems)
            item.IsChecked = false;
        InvalidateVisual();
    }

    private void ApplyFilter()
    {
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

    private class FilterItem
    {
        public string DisplayText { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = true;
    }
}
