using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn combo box with dropdown popup.
/// Supports item selection, keyboard navigation, and filterable search.
/// </summary>
public class DrawnComboBox : DrawnComponent, IValueEditor
{
    private readonly List<DrawnComboBoxItem> _items = new();
    private int _selectedIndex = -1;
    private int _highlightedIndex = -1;
    private bool _isDropdownOpen;
    private int _pressedItemIndex = -1;
    private string _filterText = string.Empty;
    private List<DrawnComboBoxItem>? _filteredItems;
    private int _selectedIndexAtOpen = -1;

    // Pointer state of a press in the open dropdown: on the list (a tap picks, a drag scrolls) or the scrollbar
    private PressTarget _pressTarget;
    private float _pressY;
    private float _lastPanY;
    private bool _isPanning;
    private float _scrollbarGrab;

    private const float DragThreshold = 6f;
    private const float ScrollbarHitWidth = 12f;
    private const float MinThumbHeight = 20f;
    private const int WheelItemsPerNotch = 3;

    private enum PressTarget { None, List, Scrollbar }

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used for both the selected text and dropdown items.</summary>
    public KumikoFont Font { get; set; } = new("Default", 13);
    /// <summary>Text color for items and the selected display text.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the main combo box area.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border color of the combo box when not focused.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color applied when the combo box has input focus.</summary>
    public KumikoColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Background fill color of the dropdown list area.</summary>
    public KumikoColor DropdownBackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Color used to highlight the item under the pointer or keyboard focus.</summary>
    public KumikoColor HighlightColor { get; set; } = new(0, 120, 215, 40);
    /// <summary>Color of the dropdown toggle arrow.</summary>
    public KumikoColor ArrowColor { get; set; } = new(100, 100, 100);
    /// <summary>Horizontal padding inside the combo box text area.</summary>
    public float Padding { get; set; } = 6f;
    /// <summary>Width reserved for the dropdown arrow button.</summary>
    public float ArrowWidth { get; set; } = 24f;
    /// <summary>Height of each item row in the dropdown list.</summary>
    public float ItemHeight { get; set; } = 28f;
    /// <summary>Maximum height of the dropdown list before scrolling is enabled.</summary>
    public float MaxDropdownHeight { get; set; } = 200f;
    /// <summary>Current vertical scroll offset within the dropdown list.</summary>
    public float DropdownScrollOffset { get; set; }

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Sets the font of the selected text and the dropdown items, so the editor draws and measures its text like the grid's cells.
    /// <see cref="ApplyTheme"/> calls it with the grid's cell font; the grid calls it again with the column's
    /// own font when the column has one.
    /// </summary>
    /// <param name="font">The font to use: family, size, bold and italic.</param>
    public void ApplyFont(KumikoFont font)
    {
        Font = font;
        InvalidateVisual();
    }

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the combo box
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        IconProvider = style.IconProvider;
        ApplyFont(style.CellFont);
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;
        DropdownBackgroundColor = bg;
        HighlightColor = style.AccentColor.WithAlpha(40);

        // Arrow: midpoint between text and background
        ArrowColor = new KumikoColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Items in the dropdown.</summary>
    public IReadOnlyList<DrawnComboBoxItem> Items => _items;

    /// <summary>The selected item index (-1 = none).</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            var old = _selectedIndex;
            _selectedIndex = Math.Clamp(value, -1, _items.Count - 1);
            RaiseValueChanged(old >= 0 ? _items[old].Value : null, SelectedValue);
            InvalidateVisual();
        }
    }

    /// <summary>The selected item (null if none).</summary>
    public DrawnComboBoxItem? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    /// <summary>The value of the selected item.</summary>
    public object? SelectedValue => SelectedItem?.Value;

    /// <summary>Display text of the selected item.</summary>
    public string SelectedText => SelectedItem?.DisplayText ?? string.Empty;

    /// <summary>Is the dropdown currently open?</summary>
    public bool IsDropdownOpen => _isDropdownOpen;

    /// <summary>
    /// Enable search/filter mode: typed characters narrow the dropdown to the items containing them. When
    /// false, a typed character jumps to the next item starting with it. In a grid, set from
    /// <see cref="KumikoColumn.EditorIsFilterable"/>.
    /// </summary>
    public bool IsFilterable { get; set; }

    /// <summary>Placeholder when no item is selected.</summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Property path on data objects to use for display text.
    /// When set, SetItemsFromSource() extracts display text from this property.
    /// </summary>
    public string? DisplayMemberPath { get; set; }

    /// <summary>
    /// Property path on data objects to use for the value.
    /// When set, SetItemsFromSource() extracts value from this property.
    /// </summary>
    public string? ValueMemberPath { get; set; }

    /// <summary>
    /// Returns the display text of a source item. Takes precedence over <see cref="DisplayMemberPath"/>;
    /// reflection-free.
    /// </summary>
    public Func<object, string?>? DisplaySelector { get; set; }

    /// <summary>
    /// Returns the value of a source item. Takes precedence over <see cref="ValueMemberPath"/>;
    /// reflection-free.
    /// </summary>
    public Func<object, object?>? ValueSelector { get; set; }

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when the selected item changes.</summary>
    public event EventHandler<ComboBoxSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when the dropdown opens.</summary>
    public event Action? DropdownOpened;

    /// <summary>Raised when the dropdown closes.</summary>
    public event Action? DropdownClosed;

    // ── Items management ────────────────────────────────────────

    /// <summary>Add an item to the dropdown.</summary>
    public void AddItem(string displayText, object? value = null)
    {
        _items.Add(new DrawnComboBoxItem(displayText, value ?? displayText));
        _filteredItems = null;
    }

    /// <summary>Add multiple items.</summary>
    public void AddItems(IEnumerable<string> displayTexts)
    {
        foreach (var text in displayTexts)
            _items.Add(new DrawnComboBoxItem(text, text));
        _filteredItems = null;
    }

    /// <summary>Set items from a collection with display/value paths.</summary>
    public void SetItems(IEnumerable<DrawnComboBoxItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _filteredItems = null;
        _selectedIndex = -1;
        InvalidateVisual();
    }

    /// <summary>Clear all items.</summary>
    public void ClearItems()
    {
        _items.Clear();
        _filteredItems = null;
        _selectedIndex = -1;
        InvalidateVisual();
    }

    /// <summary>
    /// Populate items from a data source using DisplayMemberPath and ValueMemberPath.
    /// Falls back to ToString() if paths are not set.
    /// </summary>
    public void SetItemsFromSource(System.Collections.IEnumerable source)
    {
        _items.Clear();
        _filteredItems = null;
        _selectedIndex = -1;

        foreach (var item in source)
        {
            if (item == null) continue;

            string displayText = DisplaySelector != null
                ? DisplaySelector(item) ?? string.Empty
                : DisplayMemberPath != null
                    ? GetPropertyValue(item, DisplayMemberPath)?.ToString() ?? string.Empty
                    : item.ToString() ?? string.Empty;

            object? value = ValueSelector != null
                ? ValueSelector(item)
                : ValueMemberPath != null
                    ? GetPropertyValue(item, ValueMemberPath)
                    : item;

            _items.Add(new DrawnComboBoxItem(displayText, value));
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Select the item whose Value equals the given value, or whose value has the same text (an enum
    /// property shown with a list of names, a number with a list of strings). Nothing is selected when no
    /// item matches.
    /// </summary>
    public void SetSelectedValue(object? value) => SelectedIndex = IndexOfValue(value);

    private int IndexOfValue(object? value)
    {
        for (int i = 0; i < _items.Count; i++)
            if (Equals(_items[i].Value, value)) return i;

        if (value == null) return -1;
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Value != null && Convert.ToString(_items[i].Value, CultureInfo.InvariantCulture) == text) return i;
        return -1;
    }

    private static object? GetPropertyValue(object obj, string propertyPath) =>
        Data.KumikoAccessorRegistry.GetPath(obj.GetType(), propertyPath).GetValue(obj);

    private List<DrawnComboBoxItem> GetVisibleItems()
    {
        if (!IsFilterable || string.IsNullOrEmpty(_filterText))
            return _items;

        _filteredItems ??= _items
            .Where(i => i.DisplayText.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return _filteredItems;
    }

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        DrawMainBox(ctx);

        if (_isDropdownOpen)
            DrawDropdown(ctx);
    }

    private void DrawMainBox(IDrawingContext ctx)
    {
        var b = Bounds;

        // Background
        ctx.FillRect(b, new KumikoPaint { Color = BackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(b, new KumikoPaint
        {
            Color = IsFocused ? FocusedBorderColor : BorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1
        });

        // Selected text or placeholder
        float textX = b.X + Padding;
        float textY = b.Y + b.Height / 2;
        float textWidth = b.Width - ArrowWidth - Padding * 2;

        ctx.Save();
        ctx.ClipRect(new KumikoRect(textX, b.Y, textWidth, b.Height));

        string displayText = IsFilterable && _isDropdownOpen && IsFocused
            ? _filterText
            : SelectedText;

        if (!string.IsNullOrEmpty(displayText))
        {
            ctx.DrawText(displayText, textX, textY, new KumikoPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font });
        }
        else if (!string.IsNullOrEmpty(Placeholder))
        {
            ctx.DrawText(Placeholder, textX, textY,
                new KumikoPaint { Color = new KumikoColor(160, 160, 160), Style = PaintStyle.Fill, Font = Font });
        }

        ctx.Restore();

        // Dropdown arrow
        DrawArrow(ctx, b.X + b.Width - ArrowWidth, b.Y, ArrowWidth, b.Height);
    }

    private void DrawArrow(IDrawingContext ctx, float x, float y, float w, float h)
    {
        var paint = new KumikoPaint { Color = ArrowColor, Style = PaintStyle.Fill, IsAntiAlias = true };
        // A 12 px square centered in the arrow area; the glyph shows whether the dropdown is open
        const float size = 12f;
        IconProvider.DrawIcon(ctx, _isDropdownOpen ? KumikoIcon.DropDownOpen : KumikoIcon.DropDown,
            new KumikoRect(x + (w - size) / 2, y + (h - size) / 2, size, size), paint);
    }

    private void DrawDropdown(IDrawingContext ctx)
    {
        var items = GetVisibleItems();
        if (items.Count == 0) return;

        var dropRect = GetDropdownBounds();

        // Shadow
        ctx.FillRect(new KumikoRect(dropRect.X + 2, dropRect.Y + 2, dropRect.Width, dropRect.Height),
            new KumikoPaint { Color = new KumikoColor(0, 0, 0, 30), Style = PaintStyle.Fill });

        // Background
        ctx.FillRect(dropRect, new KumikoPaint { Color = DropdownBackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(dropRect, new KumikoPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

        // Items
        ctx.Save();
        ctx.ClipRect(dropRect);

        float y = dropRect.Y - DropdownScrollOffset;
        for (int i = 0; i < items.Count; i++)
        {
            if (y + ItemHeight < dropRect.Y) { y += ItemHeight; continue; }
            if (y > dropRect.Y + dropRect.Height) break;

            var itemRect = new KumikoRect(dropRect.X, y, dropRect.Width, ItemHeight);

            // Highlight
            if (i == _highlightedIndex)
            {
                ctx.FillRect(itemRect, new KumikoPaint { Color = HighlightColor, Style = PaintStyle.Fill });
            }

            // Item text
            float itemTextX = dropRect.X + Padding;
            float itemTextY = y + ItemHeight / 2;
            ctx.DrawText(items[i].DisplayText, itemTextX, itemTextY,
                new KumikoPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font });

            y += ItemHeight;
        }

        // Scrollbar: a thin thumb on the right edge of a list that scrolls (its hit area is wider)
        if (MaxDropdownScrollOffset > 0)
        {
            var thumb = ScrollbarThumb(dropRect);
            byte alpha = _pressTarget == PressTarget.Scrollbar ? (byte)220 : (byte)150;
            ctx.FillRoundRect(new KumikoRect(dropRect.Right - 5, thumb.Y, 4, thumb.Height), 2,
                new KumikoPaint { Color = ArrowColor.WithAlpha(alpha), Style = PaintStyle.Fill });
        }

        ctx.Restore();
    }

    /// <summary>Get the bounding rect of the dropdown area (for popup hit testing).</summary>
    public KumikoRect GetDropdownBounds()
    {
        return PopupLayout.Place(Bounds, Bounds.Width, DropdownHeight, PopupViewport);
    }

    /// <summary>
    /// The height of the open dropdown: its items, up to <see cref="MaxDropdownHeight"/>, and no more than
    /// the room the viewport (<see cref="DrawnComponent.PopupViewport"/>) leaves above or below the combo box.
    /// </summary>
    private float DropdownHeight =>
        Math.Min(Math.Min(GetVisibleItems().Count * ItemHeight, MaxDropdownHeight), PopupLayout.RoomFor(Bounds, PopupViewport));

    /// <summary>
    /// Override HitTest to include the dropdown area when the dropdown is open.
    /// Without this, clicks on the dropdown items fall through to grid-level handling.
    /// </summary>
    public override bool HitTest(float x, float y)
    {
        if (base.HitTest(x, y)) return true;
        if (_isDropdownOpen)
            return GetDropdownBounds().Contains(x, y);
        return false;
    }

    // ── Input ───────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// A press on an item is picked on release (when the pointer has not dragged); a press on the list
    /// that drags scrolls it; a press on the scrollbar (the right edge of a list that scrolls) drags the
    /// thumb, or jumps to the press.
    /// </remarks>
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        _pressedItemIndex = -1;
        _pressTarget = PressTarget.None;
        _isPanning = false;

        if (_isDropdownOpen)
        {
            var dropBounds = GetDropdownBounds();
            if (dropBounds.Contains(e.X, e.Y))
            {
                _pressY = _lastPanY = e.Y;

                if (MaxDropdownScrollOffset > 0 && e.X >= dropBounds.Right - ScrollbarHitWidth)
                {
                    _pressTarget = PressTarget.Scrollbar;
                    var thumb = ScrollbarThumb(dropBounds);
                    if (e.Y < thumb.Y || e.Y > thumb.Bottom)
                        ScrollThumbTo(dropBounds, e.Y - thumb.Height / 2); // jump: center the thumb on the press
                    _scrollbarGrab = e.Y - ScrollbarThumb(dropBounds).Y;
                    InvalidateVisual();
                    return true;
                }

                // Record which item was pressed – selection happens on release
                _pressTarget = PressTarget.List;
                int itemIndex = ItemIndexAt(dropBounds, e.Y);
                if (itemIndex >= 0)
                {
                    _pressedItemIndex = itemIndex;
                    _highlightedIndex = itemIndex;
                    InvalidateVisual();
                }
                return true;
            }
            else
            {
                CloseDropdown();
                return true;
            }
        }
        else
        {
            OpenDropdown();
            return true;
        }
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        var pressed = _pressTarget;
        bool wasPanning = _isPanning;
        _pressTarget = PressTarget.None;
        _isPanning = false;

        if (_isDropdownOpen && pressed == PressTarget.List && !wasPanning && _pressedItemIndex >= 0)
        {
            var dropBounds = GetDropdownBounds();
            // Confirm selection only if pointer is still over the same item
            if (dropBounds.Contains(e.X, e.Y) && ItemIndexAt(dropBounds, e.Y) == _pressedItemIndex)
            {
                SelectItemFromFiltered(_pressedItemIndex);
                _pressedItemIndex = -1;
                CloseDropdown();
                RaiseEditCompleted();
                return true;
            }
        }

        _pressedItemIndex = -1;
        if (pressed == PressTarget.Scrollbar) InvalidateVisual();
        // Always consume pointer-up when the combobox editor is active.
        // Without this, the grid's HandlePointerUp falls through to
        // "click outside editor → commit" logic, immediately ending
        // the edit session before the user can interact with the dropdown.
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// With a press on the list, a move past a small threshold turns the press into a drag that scrolls
    /// the list (and picks nothing on release); with a press on the scrollbar, it drags the thumb.
    /// Otherwise it highlights the item under the pointer.
    /// </remarks>
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (!_isDropdownOpen) return false;

        var dropBounds = GetDropdownBounds();
        switch (_pressTarget)
        {
            case PressTarget.Scrollbar:
                ScrollThumbTo(dropBounds, e.Y - _scrollbarGrab);
                return true;

            case PressTarget.List:
                if (!_isPanning && Math.Abs(e.Y - _pressY) > DragThreshold)
                {
                    _isPanning = true;
                    _pressedItemIndex = -1; // a drag scrolls; it never picks
                }
                if (_isPanning)
                {
                    ScrollDropdownTo(DropdownScrollOffset + (_lastPanY - e.Y));
                    _lastPanY = e.Y;
                    return true;
                }
                break;
        }

        if (dropBounds.Contains(e.X, e.Y))
        {
            int index = ItemIndexAt(dropBounds, e.Y);
            if (index >= 0 && index != _highlightedIndex)
            {
                _highlightedIndex = index;
                InvalidateVisual();
            }
            return true;
        }
        return _pressTarget != PressTarget.None;
    }

    /// <summary>The mouse wheel scrolls the open dropdown list.</summary>
    public override bool OnScroll(KumikoPointerEventArgs e)
    {
        if (!_isDropdownOpen || !GetDropdownBounds().Contains(e.X, e.Y)) return false;

        ScrollDropdownTo(DropdownScrollOffset - e.GetScrollPixelsY(ItemHeight * WheelItemsPerNotch));
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>Down, Up (and PageDown, PageUp, Home, End in the open dropdown) move the highlight, and the
    /// selection with it; Down, Up and Space open a closed dropdown.</para>
    /// <para>Enter picks the highlighted item, closes the dropdown and completes the edit
    /// (<see cref="DrawnComponent.EditCompleted"/>); with the dropdown closed or nothing highlighted it is
    /// not handled. Escape closes the open dropdown, restoring the selection it opened with; with the
    /// dropdown closed it is not handled.</para>
    /// <para>A typed character narrows the list when <see cref="IsFilterable"/>; otherwise it jumps to the
    /// next item whose text starts with it (typeahead), and is not handled when no item does.</para>
    /// </remarks>
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KumikoKey.Down:
                if (!_isDropdownOpen) { OpenDropdown(); return true; }
                MoveHighlight(1);
                return true;

            case KumikoKey.Up:
                if (!_isDropdownOpen) { OpenDropdown(); return true; }
                MoveHighlight(-1);
                return true;

            case KumikoKey.PageDown when _isDropdownOpen:
                MoveHighlight(Math.Max(1, VisibleItemSlots - 1));
                return true;

            case KumikoKey.PageUp when _isDropdownOpen:
                MoveHighlight(-Math.Max(1, VisibleItemSlots - 1));
                return true;

            case KumikoKey.Home when _isDropdownOpen:
                MoveHighlightTo(0);
                return true;

            case KumikoKey.End when _isDropdownOpen:
                MoveHighlightTo(GetVisibleItems().Count - 1);
                return true;

            case KumikoKey.Enter:
                if (!_isDropdownOpen || _highlightedIndex < 0 || _highlightedIndex >= GetVisibleItems().Count)
                    return false;
                SelectItemFromFiltered(_highlightedIndex);
                CloseDropdown();
                RaiseEditCompleted();
                return true;

            case KumikoKey.Escape:
                if (!_isDropdownOpen) return false;
                if (_selectedIndex != _selectedIndexAtOpen)
                    SelectIndex(_selectedIndexAtOpen);
                CloseDropdown();
                return true;

            case KumikoKey.Space:
                return OnSpace();

            case KumikoKey.Backspace:
                if (!IsFilterable || _filterText.Length == 0) return false;
                _filterText = _filterText[..^1];
                ApplyFilterChange();
                return true;
        }

        if (e.HasCommand || e.HasAlt || e.EnteredText is not { } text || char.IsControl(text[0]))
            return false;

        if (IsFilterable)
        {
            if (!_isDropdownOpen) OpenDropdown();
            _filterText += text;
            ApplyFilterChange();
            return true;
        }

        // A space typed as text (hosts that capture the keyboard through a text box) is Space
        if (text == " ")
            return OnSpace();

        return TypeAhead(text);
    }

    /// <summary>Space opens a closed dropdown; in an open filterable one it is part of the filter.</summary>
    private bool OnSpace()
    {
        if (!_isDropdownOpen) { OpenDropdown(); return true; }
        if (!IsFilterable) return false;
        _filterText += " ";
        ApplyFilterChange();
        return true;
    }

    private void ApplyFilterChange()
    {
        _filteredItems = null;
        _highlightedIndex = GetVisibleItems().Count > 0 ? 0 : -1;
        DropdownScrollOffset = 0;
        InvalidateVisual();
    }

    /// <summary>
    /// Jump to the next item (after the highlighted one, wrapping) whose text starts with
    /// <paramref name="prefix"/>: typing the same letter again cycles through the items that start with it.
    /// </summary>
    private bool TypeAhead(string prefix)
    {
        var items = GetVisibleItems();
        int count = items.Count;
        if (count == 0) return false;

        int start = _isDropdownOpen ? _highlightedIndex : FilteredIndexOf(_selectedIndex);
        if (start < -1 || start >= count) start = -1;
        for (int step = 1; step <= count; step++)
        {
            int i = (start + step) % count;
            if (items[i].DisplayText.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                MoveHighlightTo(i);
                return true;
            }
        }
        return false;
    }

    private int FilteredIndexOf(int realIndex)
    {
        if (realIndex < 0 || realIndex >= _items.Count) return -1;
        return GetVisibleItems().IndexOf(_items[realIndex]);
    }

    /// <summary>The number of whole items the open dropdown shows at once.</summary>
    private int VisibleItemSlots =>
        ItemHeight > 0 ? Math.Max(1, (int)(DropdownHeight / ItemHeight)) : 1;

    private void MoveHighlight(int delta)
    {
        var items = GetVisibleItems();
        if (items.Count == 0) return;

        MoveHighlightTo(Math.Clamp(_highlightedIndex + delta, 0, items.Count - 1));
    }

    /// <summary>
    /// Highlight the item at <paramref name="filteredIndex"/> of the visible list and select it: the item
    /// the keyboard highlights is the combo box's value, so any commit (Enter, Tab, a click elsewhere)
    /// writes it.
    /// </summary>
    private void MoveHighlightTo(int filteredIndex)
    {
        var items = GetVisibleItems();
        if (items.Count == 0) return;

        _highlightedIndex = Math.Clamp(filteredIndex, 0, items.Count - 1);
        SelectItemFromFiltered(_highlightedIndex);
        EnsureHighlightVisible();
        InvalidateVisual();
    }

    private void EnsureHighlightVisible()
    {
        if (_highlightedIndex < 0) return;
        float itemTop = _highlightedIndex * ItemHeight;
        float dropdownHeight = DropdownHeight;

        if (itemTop < DropdownScrollOffset)
            DropdownScrollOffset = itemTop;
        else if (itemTop + ItemHeight > DropdownScrollOffset + dropdownHeight)
            DropdownScrollOffset = itemTop + ItemHeight - dropdownHeight;
    }

    // ── Dropdown scrolling ──────────────────────────────────────

    /// <summary>How far the dropdown list scrolls: its content height past the dropdown's height.</summary>
    private float MaxDropdownScrollOffset
    {
        get
        {
            float content = GetVisibleItems().Count * ItemHeight;
            return Math.Max(0, content - Math.Min(content, DropdownHeight));
        }
    }

    private void ScrollDropdownTo(float offset)
    {
        float clamped = Math.Clamp(offset, 0, MaxDropdownScrollOffset);
        if (Math.Abs(clamped - DropdownScrollOffset) < 0.01f) return;
        DropdownScrollOffset = clamped;
        InvalidateVisual();
    }

    /// <summary>The scrollbar thumb's hit area: the right edge of the dropdown, sized to the visible fraction.</summary>
    private KumikoRect ScrollbarThumb(KumikoRect dropBounds)
    {
        float content = GetVisibleItems().Count * ItemHeight;
        float max = MaxDropdownScrollOffset;
        float thumbHeight = content > 0
            ? Math.Min(dropBounds.Height, Math.Max(MinThumbHeight, dropBounds.Height * (dropBounds.Height / content)))
            : dropBounds.Height;
        float thumbY = dropBounds.Y + (max > 0 ? DropdownScrollOffset / max : 0) * (dropBounds.Height - thumbHeight);
        return new KumikoRect(dropBounds.Right - ScrollbarHitWidth, thumbY, ScrollbarHitWidth, thumbHeight);
    }

    private void ScrollThumbTo(KumikoRect dropBounds, float thumbTop)
    {
        float track = dropBounds.Height - ScrollbarThumb(dropBounds).Height;
        if (track <= 0) return;
        ScrollDropdownTo((thumbTop - dropBounds.Y) / track * MaxDropdownScrollOffset);
    }

    /// <summary>The index in the visible list of the item at viewport y <paramref name="y"/>, or -1.</summary>
    private int ItemIndexAt(KumikoRect dropBounds, float y)
    {
        if (ItemHeight <= 0) return -1;
        float offset = y - dropBounds.Y + DropdownScrollOffset;
        if (offset < 0) return -1;
        int index = (int)(offset / ItemHeight);
        return index < GetVisibleItems().Count ? index : -1;
    }

    private void SelectItemFromFiltered(int filteredIndex)
    {
        var items = GetVisibleItems();
        if (filteredIndex < 0 || filteredIndex >= items.Count) return;

        SelectIndex(_items.IndexOf(items[filteredIndex]));
    }

    /// <summary>
    /// Select the item at <paramref name="realIndex"/> of <see cref="Items"/> (-1 for none), raising
    /// <see cref="SelectionChanged"/> and <see cref="DrawnComponent.ValueChanged"/> when it changes.
    /// </summary>
    private void SelectIndex(int realIndex)
    {
        if (realIndex < -1 || realIndex >= _items.Count || realIndex == _selectedIndex) return;

        var old = _selectedIndex;
        _selectedIndex = realIndex;
        var oldItem = old >= 0 ? _items[old] : null;
        var newItem = realIndex >= 0 ? _items[realIndex] : null;
        SelectionChanged?.Invoke(this, new ComboBoxSelectionChangedEventArgs(oldItem, newItem));
        RaiseValueChanged(oldItem?.Value, newItem?.Value);
        InvalidateVisual();
    }

    /// <summary>
    /// Open the dropdown: clear the filter, highlight the selected item and scroll it into view. Escape
    /// restores the selection the dropdown opened with.
    /// </summary>
    public void OpenDropdown()
    {
        _isDropdownOpen = true;
        _highlightedIndex = _selectedIndex;
        _selectedIndexAtOpen = _selectedIndex;
        _filterText = string.Empty;
        _filteredItems = null;
        _pressTarget = PressTarget.None;
        _isPanning = false;
        DropdownScrollOffset = 0;
        EnsureHighlightVisible();
        DropdownOpened?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Close the dropdown and clear any active filter text.</summary>
    public void CloseDropdown()
    {
        _isDropdownOpen = false;
        _filterText = string.Empty;
        _filteredItems = null;
        DropdownClosed?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Closes the dropdown when the combo box loses focus.</summary>
    protected override void OnLostFocus()
    {
        CloseDropdown();
        base.OnLostFocus();
    }

    // ── IValueEditor ────────────────────────────────────────────

    object? IValueEditor.Value => SelectedValue;

    bool IValueEditor.HasValue => _selectedIndex >= 0;

    bool IValueEditor.IsDirty => HasChangedSinceTracking;
}
