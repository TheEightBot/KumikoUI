using System.Collections.ObjectModel;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// A drawn popup menu: a vertical list of <see cref="DrawnMenuItem"/>s with optional icons and check marks,
/// separators, disabled items, hover and pressed feedback, keyboard navigation and scrolling when it is taller than
/// the room it has. The grid shows one as the column header menu (see <c>IsColumnMenuEnabled</c> on the MAUI and Uno
/// grids) and opens, places and closes it itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pointer.</b> Hovering highlights an item; a click or tap on an enabled item chooses it, on the release. A press
/// with a finger or pen that drags scrolls the list instead, and a mouse wheel scrolls it. A press outside the menu
/// closes it without choosing anything.
/// </para>
/// <para>
/// <b>Keyboard.</b> Up and Down move the highlight over the enabled items (wrapping around), Home and End go to the
/// first and the last one, a letter goes to the next item whose text starts with it, Enter or Space chooses the
/// highlighted item, and Escape, Tab or the context-menu key closes the menu. While it is open the menu takes every
/// key.
/// </para>
/// <para>
/// Choosing an item closes the menu, raises <see cref="ItemInvoked"/>, then runs the item's
/// <see cref="DrawnMenuItem.Action"/> and <see cref="DrawnMenuItem.Command"/>. <see cref="Closed"/> is raised however
/// the menu closes. The items are laid out when they change, not on every frame, and drawing allocates nothing.
/// </para>
/// </remarks>
public sealed class DrawnMenu : DrawnComponent, IMeasuredPopup
{
    /// <summary>The default height of an item, for a mouse or a keyboard.</summary>
    public const float DefaultItemHeight = 32f;

    /// <summary>The height of an item when the menu was opened with a finger: a 44 px touch target.</summary>
    public const float TouchItemHeight = 44f;

    private const float VerticalPadding = 4f;
    private const float SeparatorHeight = 9f;
    private const float HorizontalPadding = 12f;
    private const float SlotSize = 14f;
    private const float SlotGap = 10f;
    private const float HighlightInset = 4f;
    private const float CornerRadius = 6f;
    private const float MinMenuWidth = 160f;
    private const float MaxMenuWidth = 360f;
    private const float TrailingSpace = 12f;
    private const float ScrollbarWidth = 4f;
    private const float DragThreshold = 6f;
    private const int WheelItemsPerNotch = 3;

    private readonly ItemCollection _items;

    // Layout: per item its top (in content coordinates, from the menu's top) and height; 0 for a separator that is
    // not drawn (at the start or the end, or after another separator).
    private float[] _tops = Array.Empty<float>();
    private float[] _heights = Array.Empty<float>();
    private float _contentHeight;
    private float _width;
    private bool _hasSlot;
    private bool _layoutDirty = true;
    private bool _widthDirty = true;
    private float _maxHeight = float.PositiveInfinity;

    private float _scrollOffset;
    private int _highlightedIndex = -1;
    private int _pressedIndex = -1;

    // The press in progress
    private bool _pressActive;
    private int _pressIndex = -1;
    private float _pressY;
    private float _lastY;
    private bool _isPanning;
    private KumikoPointerDeviceType _pressDevice;

    /// <summary>Creates an empty menu; add <see cref="Items"/>.</summary>
    public DrawnMenu()
    {
        _items = new ItemCollection(this);
        Bounds = new KumikoRect(0, 0, MinMenuWidth, VerticalPadding * 2);
    }

    /// <summary>The menu's items, in order. Separators at the start or the end, or doubled, are not drawn.</summary>
    public IList<DrawnMenuItem> Items => _items;

    /// <summary>
    /// The height of an item: <see cref="DefaultItemHeight"/> (32) by default, <see cref="TouchItemHeight"/> (44) when
    /// the grid opens the menu for a finger.
    /// </summary>
    public float ItemHeight
    {
        get;
        set
        {
            float height = float.IsFinite(value) && value > 0 ? value : DefaultItemHeight;
            if (field == height) return;
            field = height;
            InvalidateLayout();
        }
    } = DefaultItemHeight;

    /// <summary>The font of the item texts.</summary>
    public KumikoFont Font
    {
        get;
        set
        {
            if (field.Equals(value)) return;
            field = value;
            InvalidateLayout();
        }
    } = new("Default", 13);

    /// <summary>The menu's background.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;

    /// <summary>The menu's outline.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);

    /// <summary>The text and icon color of an enabled item.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);

    /// <summary>The text and icon color of a disabled item.</summary>
    public KumikoColor DisabledTextColor { get; set; } = new(160, 160, 160);

    /// <summary>The background of the highlighted item (under the pointer or the keyboard).</summary>
    public KumikoColor HighlightColor { get; set; } = new(0, 120, 215, 30);

    /// <summary>The background of an item while it is pressed.</summary>
    public KumikoColor PressedColor { get; set; } = new(0, 120, 215, 60);

    /// <summary>The color of the check mark of a checked item.</summary>
    public KumikoColor CheckColor { get; set; } = new(0, 120, 215);

    /// <summary>The color of a separator line.</summary>
    public KumikoColor SeparatorColor { get; set; } = new(220, 220, 220);

    /// <summary>The color of the scrollbar thumb of a menu taller than the room it has.</summary>
    public KumikoColor ScrollbarColor { get; set; } = new(0, 0, 0, 70);

    /// <summary>
    /// The index of the highlighted item (under the pointer, or reached with the keyboard), or -1 for none. Only an
    /// enabled item that is not a separator is ever highlighted.
    /// </summary>
    public int HighlightedIndex => _highlightedIndex;

    /// <summary>How far the items are scrolled, in pixels; 0 unless the menu is taller than the room it has.</summary>
    public float ScrollOffset => _scrollOffset;

    /// <summary>The height of every item and the padding together: the menu's height when it has room for all of them.</summary>
    public float ContentHeight
    {
        get
        {
            EnsureLayout();
            return _contentHeight;
        }
    }

    /// <summary>
    /// Raised when an item is chosen (a click, a tap, Enter or Space), after the menu asked to close and before the
    /// item's <see cref="DrawnMenuItem.Action"/> and <see cref="DrawnMenuItem.Command"/> run.
    /// </summary>
    public event EventHandler<DrawnMenuItem>? ItemInvoked;

    /// <summary>Raised when the menu closes, whether an item was chosen or not.</summary>
    public event EventHandler? Closed;

    /// <summary>Raised when the menu wants to close (an item chosen, Escape); its owner closes it.</summary>
    internal event Action<DrawnMenu>? CloseRequested;

    /// <summary>Raised when the keyboard moves the highlight to an item, for a screen-reader announcement.</summary>
    internal event Action<DrawnMenuItem>? KeyboardHighlightChanged;

    /// <summary>
    /// The tallest the menu may be: the room the grid has for it beside the point it opens at. A menu with more items
    /// scrolls. Infinity (the default) for no limit.
    /// </summary>
    internal float MaxHeight
    {
        get => _maxHeight;
        set
        {
            float max = float.IsNaN(value) || value <= 0 ? float.PositiveInfinity : value;
            if (_maxHeight == max) return;
            _maxHeight = max;
            ApplySize();
        }
    }

    /// <summary>The number of items that can be chosen: enabled and not separators.</summary>
    internal int SelectableCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].IsSelectable) count++;
            return count;
        }
    }

    /// <summary>
    /// Derives the menu's colors and font from <paramref name="style"/>, as the filter popup does, and its icons from
    /// the style's <see cref="KumikoStyle.IconProvider"/>.
    /// </summary>
    /// <param name="style">The grid's style.</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is null.</exception>
    public void ApplyTheme(KumikoStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        IconProvider = style.IconProvider;
        Font = style.CellFont;
        BackgroundColor = style.BackgroundColor;
        TextColor = style.CellTextColor;
        BorderColor = style.HeaderBorderColor;
        SeparatorColor = style.GridLineColor;
        CheckColor = style.AccentColor;
        var accent = style.AccentColor;
        HighlightColor = new KumikoColor(accent.R, accent.G, accent.B, 30);
        PressedColor = new KumikoColor(accent.R, accent.G, accent.B, 60);
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;
        DisabledTextColor = new KumikoColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));
        ScrollbarColor = new KumikoColor(txt.R, txt.G, txt.B, 70);
        InvalidateVisual();
    }

    /// <summary>
    /// Highlights the first item that can be chosen, as a menu opened from the keyboard does, and scrolls it into view.
    /// </summary>
    /// <returns>False when no item can be chosen.</returns>
    public bool HighlightFirst() => MoveHighlightTo(NextSelectable(-1, 1), fromKeyboard: true);

    // ── Layout ───────────────────────────────────────────────────

    private void InvalidateLayout()
    {
        _layoutDirty = true;
        _widthDirty = true;
        EnsureLayout();
        InvalidateVisual();
    }

    private void OnItemsChanged()
    {
        _pressedIndex = -1;
        _pressIndex = -1;
        if (_highlightedIndex >= _items.Count || (_highlightedIndex >= 0 && !_items[_highlightedIndex].IsSelectable))
            _highlightedIndex = -1;
        InvalidateLayout();
    }

    /// <summary>Lays the rows out (no drawing context needed) and sizes the menu, when the items changed.</summary>
    private void EnsureLayout()
    {
        if (!_layoutDirty) return;
        _layoutDirty = false;

        int n = _items.Count;
        if (_tops.Length != n)
        {
            _tops = new float[n];
            _heights = new float[n];
        }

        int lastCommand = -1;
        for (int i = n - 1; i >= 0; i--)
        {
            if (!_items[i].IsSeparator) { lastCommand = i; break; }
        }

        float y = VerticalPadding;
        bool previousWasSeparatorOrStart = true;
        _hasSlot = false;
        for (int i = 0; i < n; i++)
        {
            var item = _items[i];
            float height;
            if (item.IsSeparator)
            {
                // Not drawn at the start, at the end, or right after another separator
                height = previousWasSeparatorOrStart || i > lastCommand ? 0f : SeparatorHeight;
                if (height > 0) previousWasSeparatorOrStart = true;
            }
            else
            {
                height = ItemHeight;
                previousWasSeparatorOrStart = false;
                if (item.Icon != null || item.IsChecked != null) _hasSlot = true;
            }
            _tops[i] = y;
            _heights[i] = height;
            y += height;
        }
        _contentHeight = y + VerticalPadding;

        if (_widthDirty) _width = EstimateWidth();
        ApplySize();
    }

    /// <summary>A width from the text lengths, used until the texts are measured with the first frame's context.</summary>
    private float EstimateWidth()
    {
        int longest = 0;
        for (int i = 0; i < _items.Count; i++)
            if (!_items[i].IsSeparator) longest = Math.Max(longest, _items[i].Text.Length);
        return WidthFor(longest * Font.Size * 0.6f);
    }

    private float WidthFor(float textWidth) =>
        Math.Clamp(HorizontalPadding * 2 + (_hasSlot ? SlotSize + SlotGap : 0f) + textWidth + TrailingSpace,
            MinMenuWidth, MaxMenuWidth);

    /// <inheritdoc cref="IMeasuredPopup.Measure"/>
    void IMeasuredPopup.Measure(IDrawingContext ctx) => Measure(ctx);

    /// <summary>Measures the item texts with <paramref name="ctx"/> and sizes the menu, when they changed.</summary>
    internal void Measure(IDrawingContext ctx)
    {
        EnsureLayout();
        if (!_widthDirty) return;
        _widthDirty = false;

        var paint = Paints.Get(TextColor, font: Font);
        float widest = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.IsSeparator || item.Text.Length == 0) continue;
            widest = Math.Max(widest, ctx.MeasureText(item.Text, paint).Width);
        }
        _width = WidthFor(widest);
        ApplySize();
    }

    private void ApplySize()
    {
        float height = Math.Min(_contentHeight, Math.Max(_maxHeight, Math.Min(_contentHeight, VerticalPadding * 2 + ItemHeight)));
        var b = Bounds;
        if (b.Width != _width || b.Height != height)
            Bounds = new KumikoRect(b.X, b.Y, _width, height);
        ClampScroll();
    }

    private float MaxScrollOffset => Math.Max(0f, _contentHeight - Bounds.Height);

    private void ClampScroll() => _scrollOffset = Math.Clamp(_scrollOffset, 0f, MaxScrollOffset);

    private void ScrollTo(float offset)
    {
        float clamped = Math.Clamp(offset, 0f, MaxScrollOffset);
        if (clamped == _scrollOffset) return;
        _scrollOffset = clamped;
        InvalidateVisual();
    }

    /// <summary>The item at a point, or -1: separators and disabled items included.</summary>
    internal int IndexAt(float x, float y)
    {
        EnsureLayout();
        var b = Bounds;
        if (x < b.X || x >= b.Right || y < b.Y || y >= b.Bottom) return -1;
        float contentY = y - b.Y + _scrollOffset;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_heights[i] <= 0) continue;
            if (contentY >= _tops[i] && contentY < _tops[i] + _heights[i]) return i;
        }
        return -1;
    }

    /// <summary>The item's row on screen (it may be scrolled out of the menu).</summary>
    internal KumikoRect ItemBounds(int index)
    {
        EnsureLayout();
        var b = Bounds;
        return new KumikoRect(b.X, b.Y + _tops[index] - _scrollOffset, b.Width, _heights[index]);
    }

    private int SelectableAt(float x, float y)
    {
        int index = IndexAt(x, y);
        return index >= 0 && _items[index].IsSelectable ? index : -1;
    }

    // ── Drawing ──────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (!IsVisible) return;
        EnsureLayout();
        var b = Bounds;

        ctx.FillRoundRect(b, CornerRadius, Paints.Get(BackgroundColor));
        ctx.DrawRoundRect(b, CornerRadius, Paints.Get(BorderColor));

        ctx.Save();
        ctx.ClipRect(new KumikoRect(b.X, b.Y + 1, b.Width, Math.Max(0, b.Height - 2)));

        var textPaint = Paints.Get(TextColor, font: Font);
        var disabledPaint = Paints.Get(DisabledTextColor, font: Font);
        var iconPaint = Paints.Get(TextColor, 1.5f);
        var disabledIconPaint = Paints.Get(DisabledTextColor, 1.5f);
        var checkPaint = Paints.Get(CheckColor, 2f);
        var separatorPaint = Paints.Get(SeparatorColor);

        float textLeft = b.X + HorizontalPadding + (_hasSlot ? SlotSize + SlotGap : 0f);
        for (int i = 0; i < _items.Count; i++)
        {
            float height = _heights[i];
            if (height <= 0) continue;
            float top = b.Y + _tops[i] - _scrollOffset;
            if (top + height < b.Y || top > b.Bottom) continue;

            var item = _items[i];
            if (item.IsSeparator)
            {
                float lineY = top + height / 2;
                ctx.DrawLine(b.X + HorizontalPadding / 2, lineY, b.Right - HorizontalPadding / 2, lineY, separatorPaint);
                continue;
            }

            if (i == _pressedIndex || i == _highlightedIndex)
            {
                var highlight = new KumikoRect(b.X + HighlightInset, top + 1, b.Width - HighlightInset * 2, height - 2);
                ctx.FillRoundRect(highlight, 4f, Paints.Get(i == _pressedIndex ? PressedColor : HighlightColor));
            }

            bool enabled = item.IsEnabled;
            var slot = new KumikoRect(b.X + HorizontalPadding, top + (height - SlotSize) / 2, SlotSize, SlotSize);
            if (item.IsChecked == true)
                IconProvider.DrawIcon(ctx, KumikoIcon.Check, slot, enabled ? checkPaint : disabledIconPaint);
            else if (item.Icon is { } icon)
                IconProvider.DrawIcon(ctx, icon, slot, enabled ? iconPaint : disabledIconPaint);

            var textRect = new KumikoRect(textLeft, top, b.Right - HorizontalPadding - textLeft, height);
            ctx.DrawTextInRect(item.Text, textRect, enabled ? textPaint : disabledPaint,
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
        }

        ctx.Restore();

        // A thin thumb on the right edge of a menu that scrolls
        float maxScroll = MaxScrollOffset;
        if (maxScroll > 0)
        {
            float track = b.Height - 4f;
            float thumbHeight = Math.Max(20f, track * (b.Height / _contentHeight));
            float thumbY = b.Y + 2f + _scrollOffset / maxScroll * (track - thumbHeight);
            ctx.FillRoundRect(new KumikoRect(b.Right - ScrollbarWidth - 2f, thumbY, ScrollbarWidth, thumbHeight),
                ScrollbarWidth / 2, Paints.Get(ScrollbarColor));
        }
    }

    // ── Pointer ──────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        EnsureLayout();
        _pressActive = true;
        _pressDevice = e.DeviceType;
        _pressY = _lastY = e.Y;
        _isPanning = false;
        _pressIndex = SelectableAt(e.X, e.Y);
        _pressedIndex = _pressIndex;
        if (e.DeviceType == KumikoPointerDeviceType.Mouse) _highlightedIndex = _pressIndex;
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        EnsureLayout();
        if (!_pressActive)
        {
            // A hover: highlight what is under the pointer (nothing outside the menu)
            if (e.DeviceType == KumikoPointerDeviceType.Touch) return false;
            int hovered = SelectableAt(e.X, e.Y);
            if (hovered != _highlightedIndex)
            {
                _highlightedIndex = hovered;
                InvalidateVisual();
            }
            return Bounds.Contains(e.X, e.Y);
        }

        if (_pressDevice != KumikoPointerDeviceType.Mouse)
        {
            // A finger or a pen that drags scrolls the list, and then chooses nothing
            if (!_isPanning && MathF.Abs(e.Y - _pressY) > DragThreshold && MaxScrollOffset > 0)
            {
                _isPanning = true;
                _pressedIndex = -1;
                InvalidateVisual();
            }
            if (_isPanning)
                ScrollTo(_scrollOffset - (e.Y - _lastY));
            _lastY = e.Y;
            return true;
        }

        // A mouse with its button down: the highlight and the pressed item follow it
        int under = SelectableAt(e.X, e.Y);
        if (under != _pressedIndex || under != _highlightedIndex)
        {
            _pressedIndex = under;
            _highlightedIndex = under;
            InvalidateVisual();
        }
        _lastY = e.Y;
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (!_pressActive) return false;
        _pressActive = false;
        _pressedIndex = -1;
        InvalidateVisual();
        if (_isPanning)
        {
            _isPanning = false;
            return true;
        }

        int under = SelectableAt(e.X, e.Y);
        // A mouse chooses the item it is released on; a finger or a pen only the item it pressed
        int chosen = _pressDevice == KumikoPointerDeviceType.Mouse ? under : under == _pressIndex ? under : -1;
        _pressIndex = -1;
        if (chosen >= 0) InvokeItem(chosen);
        return true;
    }

    /// <inheritdoc />
    public override void OnPointerCancel(KumikoPointerEventArgs e)
    {
        _pressActive = false;
        _isPanning = false;
        _pressIndex = -1;
        _pressedIndex = -1;
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override bool OnScroll(KumikoPointerEventArgs e)
    {
        EnsureLayout();
        ScrollTo(_scrollOffset - e.GetScrollPixelsY(ItemHeight * WheelItemsPerNotch));
        return true;
    }

    // ── Keyboard ─────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>Every key is handled while the menu is open, so none reaches the grid under it.</remarks>
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        EnsureLayout();
        switch (e.Key)
        {
            case KumikoKey.Up:
                MoveHighlightTo(NextSelectable(_highlightedIndex < 0 ? _items.Count : _highlightedIndex, -1), true);
                break;
            case KumikoKey.Down:
                MoveHighlightTo(NextSelectable(_highlightedIndex, 1), true);
                break;
            case KumikoKey.Home:
            case KumikoKey.PageUp:
                MoveHighlightTo(NextSelectable(-1, 1, wrap: false), true);
                break;
            case KumikoKey.End:
            case KumikoKey.PageDown:
                MoveHighlightTo(NextSelectable(_items.Count, -1, wrap: false), true);
                break;
            case KumikoKey.Enter:
            case KumikoKey.Space:
                if (_highlightedIndex >= 0 && _highlightedIndex < _items.Count && _items[_highlightedIndex].IsSelectable)
                    InvokeItem(_highlightedIndex);
                break;
            case KumikoKey.Escape:
            case KumikoKey.Tab:
            case KumikoKey.ContextMenu:
                CloseRequested?.Invoke(this);
                break;
            default:
                if (!e.HasCommand && !e.HasAlt && e.EnteredText is { Length: > 0 } text && !char.IsWhiteSpace(text[0]))
                    JumpToLetter(text[0]);
                break;
        }
        return true;
    }

    /// <summary>The next item that can be chosen from <paramref name="from"/> in <paramref name="step"/>'s direction, or -1.</summary>
    private int NextSelectable(int from, int step, bool wrap = true)
    {
        int n = _items.Count;
        if (n == 0) return -1;
        int i = from;
        for (int tries = 0; tries < n; tries++)
        {
            i += step;
            if (i < 0 || i >= n)
            {
                if (!wrap) return -1;
                i = i < 0 ? n - 1 : 0;
            }
            if (_items[i].IsSelectable && _heights.Length > i) return i;
        }
        return -1;
    }

    /// <summary>Moves the highlight to the next item (after the highlighted one, wrapping) whose text starts with <paramref name="letter"/>.</summary>
    private void JumpToLetter(char letter)
    {
        int n = _items.Count;
        int start = _highlightedIndex;
        for (int step = 1; step <= n; step++)
        {
            int i = ((start < 0 ? -1 : start) + step + n) % n;
            var item = _items[i];
            if (!item.IsSelectable) continue;
            string text = item.Text.TrimStart();
            if (text.Length > 0 && char.ToUpperInvariant(text[0]) == char.ToUpperInvariant(letter))
            {
                MoveHighlightTo(i, true);
                return;
            }
        }
    }

    private bool MoveHighlightTo(int index, bool fromKeyboard)
    {
        if (index < 0) return false;
        bool changed = index != _highlightedIndex;
        _highlightedIndex = index;
        EnsureItemVisible(index);
        InvalidateVisual();
        if (fromKeyboard && changed) KeyboardHighlightChanged?.Invoke(_items[index]);
        return true;
    }

    private void EnsureItemVisible(int index)
    {
        EnsureLayout();
        float top = _tops[index] - VerticalPadding;
        float bottom = _tops[index] + _heights[index] + VerticalPadding;
        float height = Bounds.Height;
        if (top < _scrollOffset) ScrollTo(top);
        else if (bottom > _scrollOffset + height) ScrollTo(bottom - height);
    }

    // ── Choosing and closing ─────────────────────────────────────

    /// <summary>Chooses the item at <paramref name="index"/>: closes the menu, raises <see cref="ItemInvoked"/>, runs the item.</summary>
    internal void InvokeItem(int index)
    {
        var item = _items[index];
        if (!item.IsSelectable) return;
        CloseRequested?.Invoke(this);
        ItemInvoked?.Invoke(this, item);
        item.Invoke();
    }

    /// <summary>Called by the menu's owner once it closed: resets the transient state and raises <see cref="Closed"/>.</summary>
    internal void OnClosed()
    {
        _pressActive = false;
        _isPanning = false;
        _pressIndex = -1;
        _pressedIndex = -1;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The item list: every change lays the menu out again, and so does a change to an item it holds.</summary>
    private sealed class ItemCollection : Collection<DrawnMenuItem>
    {
        private readonly DrawnMenu _owner;
        private readonly Action _itemChanged;

        public ItemCollection(DrawnMenu owner)
        {
            _owner = owner;
            _itemChanged = owner.OnItemsChanged;
        }

        protected override void InsertItem(int index, DrawnMenuItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            base.InsertItem(index, item);
            item.Changed += _itemChanged;
            _owner.OnItemsChanged();
        }

        protected override void SetItem(int index, DrawnMenuItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            this[index].Changed -= _itemChanged;
            base.SetItem(index, item);
            item.Changed += _itemChanged;
            _owner.OnItemsChanged();
        }

        protected override void RemoveItem(int index)
        {
            this[index].Changed -= _itemChanged;
            base.RemoveItem(index);
            _owner.OnItemsChanged();
        }

        protected override void ClearItems()
        {
            for (int i = 0; i < Count; i++) this[i].Changed -= _itemChanged;
            base.ClearItems();
            _owner.OnItemsChanged();
        }
    }
}

/// <summary>
/// A popup whose size depends on text measurement: <see cref="PopupManager"/> lets it measure with the frame's drawing
/// context before placing it.
/// </summary>
internal interface IMeasuredPopup
{
    /// <summary>Measure what changed since the last call and update the size; nothing when nothing changed.</summary>
    /// <param name="ctx">The drawing context of the frame being drawn.</param>
    void Measure(IDrawingContext ctx);
}
