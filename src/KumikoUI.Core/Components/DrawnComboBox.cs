using System.Diagnostics.CodeAnalysis;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn combo box with dropdown popup.
/// Supports item selection, keyboard navigation, and filterable search.
/// </summary>
public class DrawnComboBox : DrawnComponent
{
    private readonly List<ComboBoxItem> _items = new();
    private int _selectedIndex = -1;
    private int _highlightedIndex = -1;
    private bool _isDropdownOpen;
    private int _pressedItemIndex = -1;
    private string _filterText = string.Empty;
    private List<ComboBoxItem>? _filteredItems;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used for both the selected text and dropdown items.</summary>
    public GridFont Font { get; set; } = new("Default", 13);
    /// <summary>Text color for items and the selected display text.</summary>
    public GridColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the main combo box area.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Border color of the combo box when not focused.</summary>
    public GridColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color applied when the combo box has input focus.</summary>
    public GridColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Background fill color of the dropdown list area.</summary>
    public GridColor DropdownBackgroundColor { get; set; } = GridColor.White;
    /// <summary>Color used to highlight the item under the pointer or keyboard focus.</summary>
    public GridColor HighlightColor { get; set; } = new(0, 120, 215, 40);
    /// <summary>Color of the dropdown toggle arrow.</summary>
    public GridColor ArrowColor { get; set; } = new(100, 100, 100);
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
    /// Applies theme colors from a <see cref="DataGridStyle"/> so the combo box
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(DataGridStyle style)
    {
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;
        DropdownBackgroundColor = bg;
        HighlightColor = style.AccentColor.WithAlpha(40);

        // Arrow: midpoint between text and background
        ArrowColor = new GridColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Items in the dropdown.</summary>
    public IReadOnlyList<ComboBoxItem> Items => _items;

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
    public ComboBoxItem? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    /// <summary>The value of the selected item.</summary>
    public object? SelectedValue => SelectedItem?.Value;

    /// <summary>Display text of the selected item.</summary>
    public string SelectedText => SelectedItem?.DisplayText ?? string.Empty;

    /// <summary>Is the dropdown currently open?</summary>
    public bool IsDropdownOpen => _isDropdownOpen;

    /// <summary>Enable search/filter mode.</summary>
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
        _items.Add(new ComboBoxItem(displayText, value ?? displayText));
        _filteredItems = null;
    }

    /// <summary>Add multiple items.</summary>
    public void AddItems(IEnumerable<string> displayTexts)
    {
        foreach (var text in displayTexts)
            _items.Add(new ComboBoxItem(text, text));
        _filteredItems = null;
    }

    /// <summary>Set items from a collection with display/value paths.</summary>
    public void SetItems(IEnumerable<ComboBoxItem> items)
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
    [RequiresUnreferencedCode("Accesses properties on data item types by name via DisplayMemberPath/ValueMemberPath. Ensure the public properties of your data types are preserved when trimming.")]
    public void SetItemsFromSource(System.Collections.IEnumerable source)
    {
        _items.Clear();
        _filteredItems = null;
        _selectedIndex = -1;

        foreach (var item in source)
        {
            if (item == null) continue;

            string displayText = DisplayMemberPath != null
                ? GetPropertyValue(item, DisplayMemberPath)?.ToString() ?? string.Empty
                : item.ToString() ?? string.Empty;

            object? value = ValueMemberPath != null
                ? GetPropertyValue(item, ValueMemberPath)
                : item;

            _items.Add(new ComboBoxItem(displayText, value));
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Select the item whose Value equals the given value.
    /// </summary>
    public void SetSelectedValue(object? value)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (Equals(_items[i].Value, value))
            {
                SelectedIndex = i;
                return;
            }
        }
        SelectedIndex = -1;
    }

    [RequiresUnreferencedCode("Accesses properties on data item types by name. Ensure the public properties of your data types are preserved when trimming.")]
    private static object? GetPropertyValue(object obj, string propertyPath)
    {
        object? current = obj;
        foreach (var part in propertyPath.Split('.'))
        {
            if (current == null) return null;
            var type = current.GetType();
            var prop = type.GetProperty(part,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop == null) return null;
            current = prop.GetValue(current);
        }
        return current;
    }

    private List<ComboBoxItem> GetVisibleItems()
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
        ctx.FillRect(b, new GridPaint { Color = BackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(b, new GridPaint
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
        ctx.ClipRect(new GridRect(textX, b.Y, textWidth, b.Height));

        string displayText = IsFilterable && _isDropdownOpen && IsFocused
            ? _filterText
            : SelectedText;

        if (!string.IsNullOrEmpty(displayText))
        {
            ctx.DrawText(displayText, textX, textY, new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font });
        }
        else if (!string.IsNullOrEmpty(Placeholder))
        {
            ctx.DrawText(Placeholder, textX, textY,
                new GridPaint { Color = new GridColor(160, 160, 160), Style = PaintStyle.Fill, Font = Font });
        }

        ctx.Restore();

        // Dropdown arrow
        DrawArrow(ctx, b.X + b.Width - ArrowWidth, b.Y, ArrowWidth, b.Height);
    }

    private void DrawArrow(IDrawingContext ctx, float x, float y, float w, float h)
    {
        var paint = new GridPaint { Color = ArrowColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f };
        float cx = x + w / 2;
        float cy = y + h / 2;
        float aw = 7; // arrow half-width
        float ah = 4; // arrow height

        if (_isDropdownOpen)
        {
            // Up arrow
            ctx.DrawLine(cx - aw, cy + ah / 2, cx, cy - ah / 2, paint);
            ctx.DrawLine(cx, cy - ah / 2, cx + aw, cy + ah / 2, paint);
        }
        else
        {
            // Down arrow
            ctx.DrawLine(cx - aw, cy - ah / 2, cx, cy + ah / 2, paint);
            ctx.DrawLine(cx, cy + ah / 2, cx + aw, cy - ah / 2, paint);
        }
    }

    private void DrawDropdown(IDrawingContext ctx)
    {
        var items = GetVisibleItems();
        if (items.Count == 0) return;

        float dropdownHeight = Math.Min(items.Count * ItemHeight, MaxDropdownHeight);
        var dropRect = new GridRect(Bounds.X, Bounds.Y + Bounds.Height, Bounds.Width, dropdownHeight);

        // Shadow
        ctx.FillRect(new GridRect(dropRect.X + 2, dropRect.Y + 2, dropRect.Width, dropRect.Height),
            new GridPaint { Color = new GridColor(0, 0, 0, 30), Style = PaintStyle.Fill });

        // Background
        ctx.FillRect(dropRect, new GridPaint { Color = DropdownBackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(dropRect, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

        // Items
        ctx.Save();
        ctx.ClipRect(dropRect);

        float y = dropRect.Y - DropdownScrollOffset;
        for (int i = 0; i < items.Count; i++)
        {
            if (y + ItemHeight < dropRect.Y) { y += ItemHeight; continue; }
            if (y > dropRect.Y + dropRect.Height) break;

            var itemRect = new GridRect(dropRect.X, y, dropRect.Width, ItemHeight);

            // Highlight
            if (i == _highlightedIndex)
            {
                ctx.FillRect(itemRect, new GridPaint { Color = HighlightColor, Style = PaintStyle.Fill });
            }

            // Item text
            float itemTextX = dropRect.X + Padding;
            float itemTextY = y + ItemHeight / 2;
            ctx.DrawText(items[i].DisplayText, itemTextX, itemTextY,
                new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font });

            y += ItemHeight;
        }

        ctx.Restore();
    }

    /// <summary>Get the bounding rect of the dropdown area (for popup hit testing).</summary>
    public GridRect GetDropdownBounds()
    {
        var items = GetVisibleItems();
        float dropdownHeight = Math.Min(items.Count * ItemHeight, MaxDropdownHeight);
        return new GridRect(Bounds.X, Bounds.Y + Bounds.Height, Bounds.Width, dropdownHeight);
    }

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
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        _pressedItemIndex = -1;

        if (_isDropdownOpen)
        {
            var dropBounds = GetDropdownBounds();
            if (dropBounds.Contains(e.X, e.Y))
            {
                // Record which item was pressed – selection happens on release
                int itemIndex = (int)((e.Y - dropBounds.Y + DropdownScrollOffset) / ItemHeight);
                var items = GetVisibleItems();
                if (itemIndex >= 0 && itemIndex < items.Count)
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
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
        if (_isDropdownOpen && _pressedItemIndex >= 0)
        {
            var dropBounds = GetDropdownBounds();
            if (dropBounds.Contains(e.X, e.Y))
            {
                // Confirm selection only if pointer is still over the same item
                int itemIndex = (int)((e.Y - dropBounds.Y + DropdownScrollOffset) / ItemHeight);
                if (itemIndex == _pressedItemIndex)
                {
                    SelectItemFromFiltered(_pressedItemIndex);
                    CloseDropdown();
                    RaiseEditCompleted();
                }
            }
            _pressedItemIndex = -1;
            return true;
        }

        _pressedItemIndex = -1;
        // Always consume pointer-up when the combobox editor is active.
        // Without this, the grid's HandlePointerUp falls through to
        // "click outside editor → commit" logic, immediately ending
        // the edit session before the user can interact with the dropdown.
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(GridPointerEventArgs e)
    {
        if (!_isDropdownOpen) return false;

        var dropBounds = GetDropdownBounds();
        if (dropBounds.Contains(e.X, e.Y))
        {
            _highlightedIndex = (int)((e.Y - dropBounds.Y + DropdownScrollOffset) / ItemHeight);
            InvalidateVisual();
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        switch (e.Key)
        {
            case GridKey.Down:
                if (!_isDropdownOpen) { OpenDropdown(); return true; }
                MoveHighlight(1);
                return true;

            case GridKey.Up:
                if (!_isDropdownOpen) { OpenDropdown(); return true; }
                MoveHighlight(-1);
                return true;

            case GridKey.Enter:
                if (_isDropdownOpen && _highlightedIndex >= 0)
                {
                    SelectItemFromFiltered(_highlightedIndex);
                    CloseDropdown();
                }
                return true;

            case GridKey.Escape:
                CloseDropdown();
                return true;

            case GridKey.Space:
                if (!_isDropdownOpen) { OpenDropdown(); return true; }
                break;

            default:
                if (IsFilterable && e.Character.HasValue && e.Character.Value != '\0' && _isDropdownOpen)
                {
                    _filterText += e.Character.Value;
                    _filteredItems = null;
                    _highlightedIndex = 0;
                    InvalidateVisual();
                    return true;
                }
                if (e.Key == GridKey.Backspace && IsFilterable && _filterText.Length > 0)
                {
                    _filterText = _filterText[..^1];
                    _filteredItems = null;
                    _highlightedIndex = 0;
                    InvalidateVisual();
                    return true;
                }
                break;
        }
        return false;
    }

    private void MoveHighlight(int delta)
    {
        var items = GetVisibleItems();
        if (items.Count == 0) return;

        _highlightedIndex = Math.Clamp(_highlightedIndex + delta, 0, items.Count - 1);
        EnsureHighlightVisible();
        InvalidateVisual();
    }

    private void EnsureHighlightVisible()
    {
        float itemTop = _highlightedIndex * ItemHeight;
        float dropdownHeight = Math.Min(GetVisibleItems().Count * ItemHeight, MaxDropdownHeight);

        if (itemTop < DropdownScrollOffset)
            DropdownScrollOffset = itemTop;
        else if (itemTop + ItemHeight > DropdownScrollOffset + dropdownHeight)
            DropdownScrollOffset = itemTop + ItemHeight - dropdownHeight;
    }

    private void SelectItemFromFiltered(int filteredIndex)
    {
        var items = GetVisibleItems();
        if (filteredIndex < 0 || filteredIndex >= items.Count) return;

        var item = items[filteredIndex];
        int realIndex = _items.IndexOf(item);
        if (realIndex >= 0)
        {
            var old = _selectedIndex;
            _selectedIndex = realIndex;
            SelectionChanged?.Invoke(this, new ComboBoxSelectionChangedEventArgs(
                old >= 0 ? _items[old] : null, _items[realIndex]));
            RaiseValueChanged(old >= 0 ? _items[old].Value : null, _items[realIndex].Value);
        }
    }

    /// <summary>Open the dropdown and reset filter/scroll state.</summary>
    public void OpenDropdown()
    {
        _isDropdownOpen = true;
        _highlightedIndex = _selectedIndex;
        _filterText = string.Empty;
        _filteredItems = null;
        DropdownScrollOffset = 0;
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

    protected override void OnLostFocus()
    {
        CloseDropdown();
        base.OnLostFocus();
    }
}

/// <summary>An item in a combo box.</summary>
public class ComboBoxItem
{
    /// <summary>Text shown in the dropdown list.</summary>
    public string DisplayText { get; }
    /// <summary>Underlying value associated with this item.</summary>
    public object? Value { get; }

    /// <summary>Initializes a new <see cref="ComboBoxItem"/>.</summary>
    /// <param name="displayText">Text displayed in the dropdown list.</param>
    /// <param name="value">The value associated with this item; defaults to <paramref name="displayText"/> when <see langword="null"/>.</param>
    public ComboBoxItem(string displayText, object? value = null)
    {
        DisplayText = displayText;
        Value = value ?? displayText;
    }
}

/// <summary>Combo box selection changed event args.</summary>
public class ComboBoxSelectionChangedEventArgs : EventArgs
{
    /// <summary>The previously selected item, or <see langword="null"/> if none was selected.</summary>
    public ComboBoxItem? OldItem { get; }
    /// <summary>The newly selected item, or <see langword="null"/> if the selection was cleared.</summary>
    public ComboBoxItem? NewItem { get; }
    /// <summary>Initializes a new instance of the <see cref="ComboBoxSelectionChangedEventArgs"/> class.</summary>
    /// <param name="old">The previously selected item.</param>
    /// <param name="new">The newly selected item.</param>
    public ComboBoxSelectionChangedEventArgs(ComboBoxItem? old, ComboBoxItem? @new)
    {
        OldItem = old;
        NewItem = @new;
    }
}
