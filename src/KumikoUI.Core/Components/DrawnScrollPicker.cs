using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn scroll-wheel picker (mobile-style).
/// Vertically scrolling list with center-selection indicator and snap-to-item.
/// </summary>
public class DrawnScrollPicker : DrawnComponent
{
    private readonly List<string> _items = new();
    private int _selectedIndex;
    private float _scrollOffset;
    private float _velocity;
    private float _lastDragY;
    private bool _isDragging;

    private const float Friction = 0.92f;
    private const float SnapSpeed = 0.15f;
    private const float MinVelocity = 0.5f;

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Height of each item row in the scroll wheel.</summary>
    public float ItemHeight { get; set; } = 40f;
    /// <summary>Number of items visible at once (odd numbers recommended for center alignment).</summary>
    public int VisibleItemCount { get; set; } = 5; // Odd number recommended
    /// <summary>Font used for non-selected items.</summary>
    public GridFont Font { get; set; } = new("Default", 14);
    /// <summary>Font used for the currently selected (center) item.</summary>
    public GridFont SelectedFont { get; set; } = new("Default", 16, bold: true);
    /// <summary>Text color for non-selected items.</summary>
    public GridColor TextColor { get; set; } = new(120, 120, 120);
    /// <summary>Text color for the currently selected item.</summary>
    public GridColor SelectedTextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the picker.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Background color of the selection band highlighting the selected item.</summary>
    public GridColor SelectionBandColor { get; set; } = new(0, 120, 215, 20);
    /// <summary>Color of the horizontal separator lines above and below the selection band.</summary>
    public GridColor SeparatorColor { get; set; } = new(200, 200, 200);

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Applies theme colors from a <see cref="DataGridStyle"/> so the scroll picker
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(DataGridStyle style)
    {
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        SelectedTextColor = txt;
        BackgroundColor = bg;
        SelectionBandColor = style.AccentColor.WithAlpha(20);
        SeparatorColor = style.GridLineColor;

        // Non-selected text: midpoint between text and background
        TextColor = new GridColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Items to display.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>Selected item index.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            value = Math.Clamp(value, 0, Math.Max(0, _items.Count - 1));
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            _scrollOffset = value * ItemHeight;
            RaiseValueChanged(null, SelectedValue);
            InvalidateVisual();
        }
    }

    /// <summary>Selected item text.</summary>
    public string? SelectedValue => _selectedIndex >= 0 && _selectedIndex < _items.Count
        ? _items[_selectedIndex] : null;

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when the selected item changes via scroll or keyboard navigation.</summary>
    public event EventHandler<PickerSelectionChangedEventArgs>? SelectionChanged;

    // ── Items management ────────────────────────────────────────

    /// <summary>Replace the picker's items with the given collection.</summary>
    /// <param name="items">The new set of display strings to show.</param>
    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _items.Count - 1));
        _scrollOffset = _selectedIndex * ItemHeight;
        InvalidateVisual();
    }

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        var b = Bounds;

        // Background
        ctx.FillRect(b, new GridPaint { Color = BackgroundColor, Style = PaintStyle.Fill });

        ctx.Save();
        ctx.ClipRect(b);

        int halfVisible = VisibleItemCount / 2;
        float centerY = b.Y + b.Height / 2;

        // Selection band
        float bandY = centerY - ItemHeight / 2;
        ctx.FillRect(new GridRect(b.X, bandY, b.Width, ItemHeight),
            new GridPaint { Color = SelectionBandColor, Style = PaintStyle.Fill });

        // Separator lines
        var sepPaint = new GridPaint { Color = SeparatorColor, Style = PaintStyle.Stroke, StrokeWidth = 1 };
        ctx.DrawLine(b.X, bandY, b.X + b.Width, bandY, sepPaint);
        ctx.DrawLine(b.X, bandY + ItemHeight, b.X + b.Width, bandY + ItemHeight, sepPaint);

        // Draw items
        float offsetFromCenter = _scrollOffset - (_selectedIndex * ItemHeight);
        int startIndex = Math.Max(0, _selectedIndex - halfVisible - 1);
        int endIndex = Math.Min(_items.Count - 1, _selectedIndex + halfVisible + 1);

        for (int i = startIndex; i <= endIndex; i++)
        {
            float itemY = centerY + (i - _selectedIndex) * ItemHeight - offsetFromCenter + (_scrollOffset - _selectedIndex * ItemHeight);
            float actualItemY = centerY + (i * ItemHeight - _scrollOffset) - ItemHeight / 2 + ItemHeight / 2;

            // Calculate distance from center for fade effect
            float distFromCenter = Math.Abs(actualItemY + ItemHeight / 2 - centerY);
            float maxDist = (halfVisible + 1) * ItemHeight;

            if (distFromCenter > maxDist) continue;

            float alpha = 1f - (distFromCenter / maxDist) * 0.6f;

            bool isSelected = i == GetNearestIndex();
            var font = isSelected ? SelectedFont : Font;
            var color = isSelected ? SelectedTextColor : TextColor;
            color = new GridColor(color.R, color.G, color.B, (byte)(color.A * alpha));

            string text = _items[i];
            var itemPaint = new GridPaint { Color = color, Style = PaintStyle.Fill, Font = font };
            float tw = ctx.MeasureText(text, itemPaint).Width;
            float textX = b.X + (b.Width - tw) / 2;
            float textY2 = centerY + (i * ItemHeight - _scrollOffset);

            ctx.DrawText(text, textX, textY2, itemPaint);
        }

        ctx.Restore();
    }

    private int GetNearestIndex()
    {
        int idx = (int)Math.Round(_scrollOffset / ItemHeight);
        return Math.Clamp(idx, 0, Math.Max(0, _items.Count - 1));
    }

    // ── Input ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        _isDragging = true;
        _lastDragY = e.Y;
        _velocity = 0;
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(GridPointerEventArgs e)
    {
        if (!_isDragging) return false;

        float dy = _lastDragY - e.Y;
        _scrollOffset += dy;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, (_items.Count - 1) * ItemHeight));
        _velocity = dy;
        _lastDragY = e.Y;
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
        _isDragging = false;
        // Snap to nearest item and signal edit complete
        SnapToNearest();
        RaiseEditCompleted();
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        switch (e.Key)
        {
            case GridKey.Up:
                SelectedIndex = Math.Max(0, _selectedIndex - 1);
                SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
                return true;
            case GridKey.Down:
                SelectedIndex = Math.Min(_items.Count - 1, _selectedIndex + 1);
                SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
                return true;
        }
        return false;
    }

    /// <summary>Animate scroll physics. Call from a frame timer. Returns true if still animating.</summary>
    public bool UpdateAnimation(float frameMs)
    {
        if (_isDragging) return false;

        // Apply friction to velocity
        if (Math.Abs(_velocity) > MinVelocity)
        {
            _scrollOffset += _velocity;
            _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, (_items.Count - 1) * ItemHeight));
            _velocity *= Friction;
            InvalidateVisual();
            return true;
        }

        // Snap to nearest
        return AnimateSnap();
    }

    private void SnapToNearest()
    {
        int nearest = GetNearestIndex();
        if (nearest != _selectedIndex)
        {
            var old = _selectedIndex;
            _selectedIndex = nearest;
            SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
            RaiseValueChanged(old, _selectedIndex);
        }
        _scrollOffset = nearest * ItemHeight;
        InvalidateVisual();
    }

    private bool AnimateSnap()
    {
        float target = GetNearestIndex() * ItemHeight;
        float diff = target - _scrollOffset;

        if (Math.Abs(diff) < 0.5f)
        {
            _scrollOffset = target;
            var newIdx = GetNearestIndex();
            if (newIdx != _selectedIndex)
            {
                _selectedIndex = newIdx;
                SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
            }
            InvalidateVisual();
            return false;
        }

        _scrollOffset += diff * SnapSpeed;
        InvalidateVisual();
        return true;
    }
}

/// <summary>Picker selection changed event args.</summary>
public class PickerSelectionChangedEventArgs : EventArgs
{
    /// <summary>Index of the currently selected item.</summary>
    public int SelectedIndex { get; }
    /// <summary>Initializes a new instance of the <see cref="PickerSelectionChangedEventArgs"/> class.</summary>
    /// <param name="index">The index of the selected item.</param>
    public PickerSelectionChangedEventArgs(int index) { SelectedIndex = index; }
}
