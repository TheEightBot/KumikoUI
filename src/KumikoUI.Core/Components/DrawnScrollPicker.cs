using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn scroll-wheel picker (mobile-style).
/// Vertically scrolling list with center-selection indicator and snap-to-item.
/// </summary>
/// <remarks>
/// Each item has display text and a value (<see cref="SetItems(IReadOnlyList{string}, IReadOnlyList{object?})"/>);
/// <see cref="SelectedIndex"/> is -1 when nothing is selected. The choice is confirmed
/// (<see cref="DrawnComponent.EditCompleted"/>) by releasing the wheel, which snaps to the nearest item and
/// stops there, or by Enter.
/// </remarks>
public class DrawnScrollPicker : DrawnComponent, IValueEditor
{
    private readonly List<string> _items = new();
    private readonly List<object?> _values = new();
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
    public KumikoFont Font { get; set; } = new("Default", 14);
    /// <summary>Font used for the currently selected (center) item.</summary>
    public KumikoFont SelectedFont { get; set; } = new("Default", 16, bold: true);
    /// <summary>Text color for non-selected items.</summary>
    public KumikoColor TextColor { get; set; } = new(120, 120, 120);
    /// <summary>Text color for the currently selected item.</summary>
    public KumikoColor SelectedTextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the picker.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Background color of the selection band highlighting the selected item.</summary>
    public KumikoColor SelectionBandColor { get; set; } = new(0, 120, 215, 20);
    /// <summary>Color of the horizontal separator lines above and below the selection band.</summary>
    public KumikoColor SeparatorColor { get; set; } = new(200, 200, 200);

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the scroll picker
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        SelectedTextColor = txt;
        BackgroundColor = bg;
        SelectionBandColor = style.AccentColor.WithAlpha(20);
        SeparatorColor = style.GridLineColor;

        // Non-selected text: midpoint between text and background
        TextColor = new KumikoColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Items to display.</summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>The value of each item (its display text unless set with values).</summary>
    public IReadOnlyList<object?> ItemValues => _values;

    /// <summary>Selected item index, or -1 when nothing is selected.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            value = Math.Clamp(value, -1, _items.Count - 1);
            if (_selectedIndex == value) return;
            var old = SelectedValue;
            _selectedIndex = value;
            _scrollOffset = Math.Max(0, value) * ItemHeight;
            RaiseValueChanged(old, SelectedValue);
            InvalidateVisual();
        }
    }

    /// <summary>Selected item text, or null when nothing is selected.</summary>
    public string? SelectedValue => _selectedIndex >= 0 && _selectedIndex < _items.Count
        ? _items[_selectedIndex] : null;

    /// <summary>Selected item's value, or null when nothing is selected.</summary>
    public object? SelectedItemValue => _selectedIndex >= 0 && _selectedIndex < _values.Count
        ? _values[_selectedIndex] : null;

    /// <summary>Select nothing (the wheel shows the first item, unhighlighted).</summary>
    public void ClearSelection() => SelectedIndex = -1;

    /// <summary>
    /// Select the item whose value equals <paramref name="value"/>, or whose value or display text has
    /// the same text. Returns false, selecting nothing, when no item matches.
    /// </summary>
    public bool SelectValue(object? value)
    {
        int index = -1;
        for (int i = 0; i < _values.Count && index < 0; i++)
            if (Equals(_values[i], value)) index = i;

        if (index < 0 && value != null)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            for (int i = 0; i < _items.Count && index < 0; i++)
                if (Convert.ToString(_values[i], CultureInfo.InvariantCulture) == text || _items[i] == text) index = i;
        }

        SelectedIndex = index;
        return index >= 0;
    }

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when the selected item changes via scroll or keyboard navigation.</summary>
    public event EventHandler<PickerSelectionChangedEventArgs>? SelectionChanged;

    // ── Items management ────────────────────────────────────────

    /// <summary>Replace the picker's items with the given collection; each item's value is its text.</summary>
    /// <param name="items">The new set of display strings to show.</param>
    public void SetItems(IEnumerable<string> items)
    {
        var texts = items.ToList();
        SetItems(texts, texts.Cast<object?>().ToList());
    }

    /// <summary>Replace the picker's items with display texts and their values (the same count).</summary>
    public void SetItems(IReadOnlyList<string> displayTexts, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(displayTexts);
        ArgumentNullException.ThrowIfNull(values);
        if (displayTexts.Count != values.Count)
            throw new ArgumentException("Each display text needs a value.", nameof(values));

        _items.Clear();
        _items.AddRange(displayTexts);
        _values.Clear();
        _values.AddRange(values);
        _selectedIndex = _items.Count == 0 ? -1 : Math.Clamp(_selectedIndex, -1, _items.Count - 1);
        _scrollOffset = Math.Max(0, _selectedIndex) * ItemHeight;
        InvalidateVisual();
    }

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        var b = Bounds;

        // Background
        ctx.FillRect(b, new KumikoPaint { Color = BackgroundColor, Style = PaintStyle.Fill });

        ctx.Save();
        ctx.ClipRect(b);

        int halfVisible = VisibleItemCount / 2;
        float centerY = b.Y + b.Height / 2;

        // Selection band
        float bandY = centerY - ItemHeight / 2;
        ctx.FillRect(new KumikoRect(b.X, bandY, b.Width, ItemHeight),
            new KumikoPaint { Color = SelectionBandColor, Style = PaintStyle.Fill });

        // Separator lines
        var sepPaint = new KumikoPaint { Color = SeparatorColor, Style = PaintStyle.Stroke, StrokeWidth = 1 };
        ctx.DrawLine(b.X, bandY, b.X + b.Width, bandY, sepPaint);
        ctx.DrawLine(b.X, bandY + ItemHeight, b.X + b.Width, bandY + ItemHeight, sepPaint);

        // Draw the items around the center of the wheel
        int center = GetNearestIndex();
        int startIndex = Math.Max(0, center - halfVisible - 1);
        int endIndex = Math.Min(_items.Count - 1, center + halfVisible + 1);

        for (int i = startIndex; i <= endIndex; i++)
        {
            float actualItemY = centerY + (i * ItemHeight - _scrollOffset);

            // Calculate distance from center for fade effect
            float distFromCenter = Math.Abs(actualItemY + ItemHeight / 2 - centerY);
            float maxDist = (halfVisible + 1) * ItemHeight;

            if (distFromCenter > maxDist) continue;

            float alpha = 1f - (distFromCenter / maxDist) * 0.6f;

            // With nothing selected (and the wheel untouched) no item is highlighted
            bool isSelected = i == center && (_selectedIndex >= 0 || _isDragging || Math.Abs(_velocity) > MinVelocity);
            var font = isSelected ? SelectedFont : Font;
            var color = isSelected ? SelectedTextColor : TextColor;
            color = new KumikoColor(color.R, color.G, color.B, (byte)(color.A * alpha));

            string text = _items[i];
            var itemPaint = new KumikoPaint { Color = color, Style = PaintStyle.Fill, Font = font };
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
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        _isDragging = true;
        _lastDragY = e.Y;
        _velocity = 0;
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(KumikoPointerEventArgs e)
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

    /// <summary>
    /// Release: snap to the nearest item, stop there (the wheel doesn't coast past the item it confirms),
    /// and confirm it.
    /// </summary>
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        _isDragging = false;
        _velocity = 0;
        SnapToNearest();
        RaiseEditCompleted();
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        switch (e.Key)
        {
            case KumikoKey.Up:
                MoveSelection(_selectedIndex < 0 ? 0 : _selectedIndex - 1);
                return true;
            case KumikoKey.Down:
                MoveSelection(_selectedIndex < 0 ? 0 : _selectedIndex + 1);
                return true;
            case KumikoKey.Enter when _selectedIndex >= 0:
                RaiseEditCompleted(); // confirm the selection
                return true;
        }
        return false;
    }

    private void MoveSelection(int index)
    {
        if (_items.Count == 0) return;
        index = Math.Clamp(index, 0, _items.Count - 1);
        if (index == _selectedIndex) return;
        SelectedIndex = index;
        SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
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
            var old = SelectedValue;
            _selectedIndex = nearest;
            SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
            RaiseValueChanged(old, SelectedValue);
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
                var old = SelectedValue;
                _selectedIndex = newIdx;
                SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
                RaiseValueChanged(old, SelectedValue);
            }
            InvalidateVisual();
            return false;
        }

        _scrollOffset += diff * SnapSpeed;
        InvalidateVisual();
        return true;
    }

    // ── IValueEditor ────────────────────────────────────────────

    object? IValueEditor.Value => SelectedItemValue;

    bool IValueEditor.HasValue => _selectedIndex >= 0;

    bool IValueEditor.IsDirty => HasChangedSinceTracking;
}
