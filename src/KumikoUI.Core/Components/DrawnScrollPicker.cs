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
/// <para>
/// Each item has display text and a value (<see cref="SetItems(IReadOnlyList{string}, IReadOnlyList{object?})"/>);
/// <see cref="SelectedIndex"/> is -1 when nothing is selected.
/// </para>
/// <para>
/// A drag scrolls the wheel; on release it coasts with the drag's velocity, then snaps to the nearest
/// item. A tap on an item other than the centered one scrolls it to the center. Either way, the item is
/// selected and confirmed (<see cref="DrawnComponent.EditCompleted"/>) once the wheel settles, which needs
/// <see cref="UpdateAnimation"/> to be called every frame while <see cref="IsAnimating"/> (the grid's hosts do
/// it through <see cref="EditSession.UpdateAnimation"/>). A tap on the centered item, or Enter, confirms
/// it at once. Up, Down and the mouse wheel step the selection without confirming it.
/// </para>
/// </remarks>
public class DrawnScrollPicker : DrawnComponent, IValueEditor
{
    private readonly List<string> _items = new();
    private readonly List<object?> _values = new();
    private int _selectedIndex;
    private float _scrollOffset;
    private float _pressY;
    private float _lastDragY;
    private bool _isDragging;
    private bool _hasDragged;
    private bool _isSnapping;
    private int _snapTarget;
    private bool _confirmWhenSettled;
    private float _wheelRemainder;

    // Coasting after a drag: velocity in px/s from timestamped moves, stopping below MinVelocity
    private readonly InertialScroller _fling = new() { Settings = new ScrollSettings { MinVelocity = 20f } };

    private const float SnapSpeed = 0.25f;
    private const float DragThreshold = 6f;

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
    /// Sets the font of the items; the selected item is bold and two pixels larger, so the editor draws and measures its text like the grid's cells.
    /// <see cref="ApplyTheme"/> calls it with the grid's cell font; the grid calls it again with the column's
    /// own font when the column has one.
    /// </summary>
    /// <param name="font">The font to use: family, size, bold and italic.</param>
    public void ApplyFont(KumikoFont font)
    {
        Font = font;
        SelectedFont = new KumikoFont(font.Family, font.Size + 2f, bold: true, italic: font.IsItalic);
        InvalidateVisual();
    }

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the scroll picker
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        ApplyFont(style.CellFont);
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

        StopAnimation();
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
            bool isSelected = i == center && (_selectedIndex >= 0 || _isDragging || IsAnimating);
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

    /// <summary>Press: stop any coasting or snapping (and the confirmation it would make) and start a drag.</summary>
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        StopAnimation();
        _isDragging = true;
        _hasDragged = false;
        _pressY = _lastDragY = e.Y;
        _fling.TrackVelocity(0, 0, e.TimestampMs);
        return true;
    }

    /// <summary>Drag: past a small threshold the wheel follows the pointer, and the velocity is tracked for coasting.</summary>
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (!_isDragging) return false;
        if (!_hasDragged && Math.Abs(e.Y - _pressY) <= DragThreshold) return true;

        _hasDragged = true;
        float dy = _lastDragY - e.Y;
        _scrollOffset = Math.Clamp(_scrollOffset + dy, 0, MaxScrollOffset);
        _fling.TrackVelocity(0, dy, e.TimestampMs);
        _lastDragY = e.Y;
        InvalidateVisual();
        return true;
    }

    /// <summary>
    /// Release. After a drag the wheel coasts and snaps to the nearest item, confirming it once settled. A
    /// tap on the centered item confirms it at once; a tap on another item scrolls it to the center and
    /// confirms it once settled.
    /// </summary>
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (!_isDragging) return true;

        bool dragged = _hasDragged;
        _isDragging = false;
        _hasDragged = false;

        if (dragged)
        {
            _fling.StartFling();
            if (!_fling.IsActive) BeginSnap(GetNearestIndex());
            _confirmWhenSettled = true;
        }
        else if (IndexAt(e.Y) is var tapped and >= 0)
        {
            if (tapped == GetNearestIndex())
            {
                _fling.Reset();
                _scrollOffset = tapped * ItemHeight;
                SelectSettled(tapped);
                RaiseEditCompleted(); // a tap on the centered item confirms it
            }
            else
            {
                BeginSnap(tapped);
                _confirmWhenSettled = true;
            }
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>The mouse wheel steps the selection one item per notch, without confirming it.</summary>
    public override bool OnScroll(KumikoPointerEventArgs e)
    {
        if (_items.Count == 0) return false;

        _wheelRemainder -= e.ScrollDeltaY; // positive is up: toward earlier items
        int steps = (int)_wheelRemainder;
        _wheelRemainder -= steps;
        if (steps != 0)
            MoveSelection(Math.Max(0, (_selectedIndex < 0 ? -1 : _selectedIndex) + steps));
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
            case KumikoKey.Enter:
                FinishAnimation();
                if (_selectedIndex < 0) return false; // nothing selected: nothing to confirm
                RaiseEditCompleted(); // confirm the selection
                return true;
        }
        return false;
    }

    private void MoveSelection(int index)
    {
        if (_items.Count == 0) return;
        StopAnimation();
        index = Math.Clamp(index, 0, _items.Count - 1);
        if (index == _selectedIndex)
        {
            _scrollOffset = index * ItemHeight; // a drag left the wheel between items
            InvalidateVisual();
            return;
        }
        SelectedIndex = index;
        SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
    }

    // ── Animation ───────────────────────────────────────────────

    /// <summary>Whether the wheel is coasting after a drag or snapping to an item.</summary>
    public override bool IsAnimating => !_isDragging && (_fling.IsActive || _isSnapping);

    /// <summary>
    /// Advance the coasting and snapping by one frame; when the wheel settles on an item after a release,
    /// that item is selected and confirmed (<see cref="DrawnComponent.EditCompleted"/>).
    /// </summary>
    /// <param name="frameIntervalMs">Time since the previous frame, in milliseconds.</param>
    /// <returns>True while the wheel is still moving.</returns>
    public override bool UpdateAnimation(float frameIntervalMs)
    {
        if (_isDragging) return false;

        if (_fling.IsActive)
        {
            var (_, dy) = _fling.Update(frameIntervalMs);
            float before = _scrollOffset;
            _scrollOffset = Math.Clamp(_scrollOffset + dy, 0, MaxScrollOffset);
            if (_fling.IsActive && Math.Abs(_scrollOffset - before) > 0.01f)
            {
                InvalidateVisual();
                return true;
            }

            // Slowed down, or ran into an end: snap to the nearest item
            _fling.Reset();
            BeginSnap(GetNearestIndex());
        }

        if (!_isSnapping) return false;

        float target = _snapTarget * ItemHeight;
        float diff = target - _scrollOffset;
        if (Math.Abs(diff) >= 0.5f)
        {
            _scrollOffset += diff * Math.Min(1f, SnapSpeed * frameIntervalMs / 16f);
            InvalidateVisual();
            return true;
        }

        _scrollOffset = target;
        _isSnapping = false;
        SelectSettled(_snapTarget);
        InvalidateVisual();
        if (_confirmWhenSettled)
        {
            _confirmWhenSettled = false;
            RaiseEditCompleted();
        }
        return false;
    }

    private float MaxScrollOffset => Math.Max(0, (_items.Count - 1) * ItemHeight);

    /// <summary>The item at viewport y <paramref name="y"/>, or -1 when none is there.</summary>
    private int IndexAt(float y)
    {
        if (ItemHeight <= 0 || _items.Count == 0) return -1;
        float centerY = Bounds.Y + Bounds.Height / 2;
        int index = (int)MathF.Round((y - centerY + _scrollOffset) / ItemHeight);
        return index >= 0 && index < _items.Count ? index : -1;
    }

    private void BeginSnap(int index)
    {
        _snapTarget = Math.Clamp(index, 0, Math.Max(0, _items.Count - 1));
        _isSnapping = _items.Count > 0;
    }

    /// <summary>Stop coasting and snapping where the wheel is, dropping the pending confirmation.</summary>
    private void StopAnimation()
    {
        _fling.Reset();
        _isSnapping = false;
        _confirmWhenSettled = false;
    }

    /// <summary>Jump to where the coasting or snapping would settle, selecting that item, without confirming it.</summary>
    private void FinishAnimation()
    {
        if (!IsAnimating) return;
        int target = _isSnapping ? _snapTarget : GetNearestIndex();
        StopAnimation();
        _scrollOffset = target * ItemHeight;
        SelectSettled(target);
        InvalidateVisual();
    }

    private void SelectSettled(int index)
    {
        if (index == _selectedIndex || index < 0 || index >= _items.Count) return;
        var old = SelectedValue;
        _selectedIndex = index;
        SelectionChanged?.Invoke(this, new PickerSelectionChangedEventArgs(_selectedIndex));
        RaiseValueChanged(old, SelectedValue);
    }

    // ── IValueEditor ────────────────────────────────────────────

    object? IValueEditor.Value => SelectedItemValue;

    bool IValueEditor.HasValue => _selectedIndex >= 0;

    bool IValueEditor.IsDirty => HasChangedSinceTracking;
}
