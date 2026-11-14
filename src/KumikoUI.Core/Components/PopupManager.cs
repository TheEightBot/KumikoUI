using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Manages popup/overlay components that render above the grid surface.
/// Used by ComboBox dropdowns, DatePicker calendars, filter popups, etc.
/// 
/// All popups registered here automatically receive touch/pointer/keyboard
/// priority over the underlying grid. Call <see cref="HandlePointer"/> and
/// <see cref="HandleKey"/> at the top of the input pipeline — if they
/// return true the event was consumed and the grid should skip processing.
/// </summary>
/// <remarks>
/// A press inside a popup captures the pointer: its moves and the release go to that popup even outside
/// it. A press outside every popup closes them all and is swallowed with its moves and release, so it
/// selects, edits or sorts nothing (<see cref="PopupClosed"/> reports each closed popup). Wheel input over
/// a popup goes to <see cref="DrawnComponent.OnScroll"/>.
/// </remarks>
internal class PopupManager
{
    private readonly List<PopupEntry> _popups = new();
    private int _nextZOrder = 1000;
    private DrawnComponent? _captured;
    private bool _swallowUntilRelease;

    /// <summary>
    /// The visible area popups must stay inside. A popup placed <see cref="PopupPlacement.Below"/> or
    /// <see cref="PopupPlacement.Above"/> opens on the other side of its anchor when it does not fit on the
    /// requested one, and every popup slides into the area. Empty (the default) leaves popups where the anchor
    /// puts them.
    /// </summary>
    public KumikoRect Viewport { get; set; }

    /// <summary>Are there any active popups?</summary>
    public bool HasActivePopups => _popups.Count > 0;

    /// <summary>
    /// Raised when the popup manager needs the hosting surface to redraw.
    /// Wire this to the same redraw callback used by the grid.
    /// </summary>
    public event Action? NeedsRedraw;

    /// <summary>Raised for each popup that closes, whether by <see cref="Close"/>, <see cref="CloseAll"/> or a press outside.</summary>
    public event Action<DrawnComponent>? PopupClosed;

    /// <summary>Show a component as a popup at the given position.</summary>
    public void Show(DrawnComponent component, KumikoRect anchor, PopupPlacement placement = PopupPlacement.Below) =>
        Show(component, () => anchor, placement);

    /// <summary>
    /// Show a component as a popup next to an anchor that can move (a column header that scrolls). The
    /// anchor is read again before every frame, so the popup follows it.
    /// </summary>
    public void Show(DrawnComponent component, Func<KumikoRect> anchor, PopupPlacement placement = PopupPlacement.Below)
    {
        var entry = new PopupEntry(component, anchor, placement);
        component.Bounds = CalculatePopupBounds(component, anchor(), placement, Viewport);
        component.IsVisible = true;
        component.ZOrder = _nextZOrder++; // above normal components, and above every earlier popup

        _popups.Add(entry);
        component.InvalidateVisual();
    }

    /// <summary>Close a specific popup.</summary>
    public void Close(DrawnComponent component)
    {
        var entry = _popups.Find(p => p.Component == component);
        if (entry == null) return;

        _popups.Remove(entry);
        if (_captured == component) _captured = null;
        component.IsVisible = false;
        component.InvalidateVisual();
        PopupClosed?.Invoke(component);
    }

    /// <summary>Close all popups.</summary>
    public void CloseAll()
    {
        var closed = _popups.ToList();
        _popups.Clear();
        _captured = null;
        foreach (var entry in closed)
            entry.Component.IsVisible = false;
        foreach (var entry in closed)
            PopupClosed?.Invoke(entry.Component);
    }

    /// <summary>Move every popup to its anchor's current position (called before each frame is drawn).</summary>
    public void UpdateAnchors()
    {
        foreach (var entry in _popups)
            entry.Component.Bounds = CalculatePopupBounds(entry.Component, entry.Anchor(), entry.Placement, Viewport);
    }

    /// <summary>Draw all active popups (call after main grid rendering).</summary>
    public void DrawPopups(IDrawingContext ctx)
    {
        UpdateAnchors();
        foreach (var entry in _popups.OrderBy(p => p.Component.ZOrder))
        {
            if (entry.Component.IsVisible)
            {
                // One shadow per popup, drawn here; popups don't draw their own
                DrawPopupShadow(ctx, entry.Component.Bounds);
                entry.Component.OnDraw(ctx);
            }
        }
    }

    /// <summary>
    /// Gets the smallest rectangle holding every visible popup, in grid coordinates, as placed by the last
    /// <see cref="DrawPopups"/> (shadows not included). A host uses it to redraw only the popups' area, for example
    /// while the caret of a popup's search box blinks. Allocation-free.
    /// </summary>
    /// <param name="bounds">The union of the visible popups' bounds; <see cref="KumikoRect.Empty"/> when there is none.</param>
    /// <returns>False when no popup is visible.</returns>
    public bool TryGetVisibleBounds(out KumikoRect bounds)
    {
        bool any = false;
        float left = 0, top = 0, right = 0, bottom = 0;
        for (int i = 0; i < _popups.Count; i++)
        {
            var component = _popups[i].Component;
            if (!component.IsVisible) continue;
            var b = component.Bounds;
            if (!any)
            {
                (left, top, right, bottom) = (b.Left, b.Top, b.Right, b.Bottom);
                any = true;
                continue;
            }
            left = MathF.Min(left, b.Left);
            top = MathF.Min(top, b.Top);
            right = MathF.Max(right, b.Right);
            bottom = MathF.Max(bottom, b.Bottom);
        }

        bounds = any ? new KumikoRect(left, top, right - left, bottom - top) : KumikoRect.Empty;
        return any;
    }

    /// <summary>Check if a point hits any popup. Returns the topmost hit popup or null.</summary>
    public DrawnComponent? HitTest(float x, float y)
    {
        // Check in reverse order (topmost first)
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (_popups[i].Component.HitTest(x, y))
                return _popups[i].Component;
        }
        return null;
    }

    /// <summary>Check if a point is outside all popups (for dismiss-on-click-outside).</summary>
    public bool IsPointOutsideAllPopups(float x, float y) =>
        _popups.All(p => !p.Component.HitTest(x, y));

    // ── Centralized input routing ────────────────────────────────

    /// <summary>
    /// Route a pointer event to popups. Call this at the TOP of your input
    /// pipeline, before any grid-level processing.
    /// 
    /// Returns true if the event was consumed (caller should skip grid input).
    /// 
    /// Behavior:
    ///  • A press inside a popup goes to it and captures the pointer: the moves and the release that follow go
    ///    to the same popup, even outside it.
    ///  • A press outside all popups closes them and is consumed, with its moves and release, so the grid
    ///    does nothing with it.
    ///  • Wheel input over a popup goes to it; elsewhere it reaches the grid.
    ///  • Hover moves (<see cref="InputAction.Hover"/>, or a move with no press) over a popup go to it and are
    ///    consumed; outside every popup they go to the topmost one (for hover-exit feedback) and reach the grid.
    /// </summary>
    public bool HandlePointer(KumikoPointerEventArgs e)
    {
        // The rest of a press that closed the popups (or of a captured press whose popup closed)
        if (_swallowUntilRelease)
        {
            if (e.Action is InputAction.Released or InputAction.Cancelled) _swallowUntilRelease = false;
            if (e.Action is InputAction.Pressed or InputAction.Moved or InputAction.Released or InputAction.Cancelled
                or InputAction.LongPress)
            {
                e.Handled = true;
                return true;
            }
        }

        if (!HasActivePopups) return false;

        // A captured press: everything up to the release goes to its popup
        if (_captured != null && e.Action is InputAction.Moved or InputAction.Released or InputAction.Cancelled
                or InputAction.LongPress)
        {
            var captured = _captured;
            if (e.Action is InputAction.Released or InputAction.Cancelled) _captured = null;
            if (e.Action == InputAction.Moved) captured.OnPointerMove(e);
            else if (e.Action is InputAction.Released or InputAction.Cancelled) captured.OnPointerUp(e);
            e.Handled = true;
            NeedsRedraw?.Invoke();
            return true;
        }

        var hitPopup = HitTest(e.X, e.Y);

        if (hitPopup != null)
        {
            switch (e.Action)
            {
                case InputAction.Pressed:
                    _captured = hitPopup;
                    hitPopup.OnPointerDown(e);
                    // The popup may have closed itself (a button): swallow the rest of the press
                    if (!IsOpen(hitPopup))
                    {
                        _captured = null;
                        _swallowUntilRelease = true;
                    }
                    break;
                case InputAction.Moved:
                case InputAction.Hover:
                    hitPopup.OnPointerMove(e);
                    break;
                case InputAction.Released:
                    hitPopup.OnPointerUp(e);
                    break;
                case InputAction.Scroll:
                    hitPopup.OnScroll(e);
                    break;
            }

            e.Handled = true;
            NeedsRedraw?.Invoke();
            return true; // consumed
        }

        // Pointer is outside all popups
        if (e.Action is InputAction.Pressed or InputAction.LongPress)
        {
            // A press outside closes the popups and does nothing else
            CloseAll();
            _swallowUntilRelease = e.Action == InputAction.Pressed;
            e.Handled = true;
            NeedsRedraw?.Invoke();
            return true;
        }

        if (e.Action is InputAction.Moved or InputAction.Hover)
        {
            // Still send move events to topmost popup for hover-exit feedback
            var topmost = GetTopmostPopup();
            if (topmost != null)
            {
                topmost.OnPointerMove(e);
                NeedsRedraw?.Invoke();
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="component"/> is still open. A loop rather than <c>Exists</c> with a lambda: the
    /// lambda's closure would be allocated on every <see cref="HandlePointer"/> call, hover moves included.
    /// </summary>
    private bool IsOpen(DrawnComponent component)
    {
        for (int i = 0; i < _popups.Count; i++)
            if (_popups[i].Component == component) return true;
        return false;
    }

    /// <summary>
    /// Route a keyboard event to popups. Call this at the TOP of your
    /// key-handling pipeline.
    /// 
    /// Returns true if the event was consumed.
    /// </summary>
    public bool HandleKey(KumikoKeyEventArgs e)
    {
        if (!HasActivePopups)
            return false;

        // Route to the topmost popup
        var topmost = GetTopmostPopup();
        if (topmost != null && topmost.OnKeyDown(e))
        {
            NeedsRedraw?.Invoke();
            e.Handled = true;
            return true;
        }

        return false;
    }

    /// <summary>Returns the topmost (highest ZOrder) visible popup, or null.</summary>
    private DrawnComponent? GetTopmostPopup()
    {
        DrawnComponent? best = null;
        foreach (var entry in _popups)
        {
            if (entry.Component.IsVisible && (best == null || entry.Component.ZOrder > best.ZOrder))
                best = entry.Component;
        }
        return best;
    }

    private static KumikoRect CalculatePopupBounds(
        DrawnComponent component, KumikoRect anchor, PopupPlacement placement, KumikoRect viewport)
    {
        float width = component.Bounds.Width > 0 ? component.Bounds.Width : anchor.Width;
        float height = component.Bounds.Height > 0 ? component.Bounds.Height : 200; // Default height

        if (placement is PopupPlacement.Below or PopupPlacement.Above)
            return PopupLayout.Place(anchor, width, height, viewport, preferAbove: placement == PopupPlacement.Above, mayCoverAnchor: false);

        var bounds = placement switch
        {
            PopupPlacement.Right => new KumikoRect(anchor.X + anchor.Width, anchor.Y, width, height),
            PopupPlacement.Left => new KumikoRect(anchor.X - width, anchor.Y, width, height),
            _ => new KumikoRect(anchor.X, anchor.Y + anchor.Height, width, height)
        };
        return PopupLayout.Clamp(bounds, viewport);
    }

    private static void DrawPopupShadow(IDrawingContext ctx, KumikoRect bounds)
    {
        var shadowColor = new KumikoColor(0, 0, 0, 40);
        var shadowPaint = new KumikoPaint { Color = shadowColor };
        // Offset shadow
        ctx.FillRect(new KumikoRect(bounds.X + 2, bounds.Y + 2, bounds.Width, bounds.Height), shadowPaint);
    }

    private class PopupEntry
    {
        public DrawnComponent Component { get; }
        public Func<KumikoRect> Anchor { get; }
        public PopupPlacement Placement { get; }

        public PopupEntry(DrawnComponent component, Func<KumikoRect> anchor, PopupPlacement placement)
        {
            Component = component;
            Anchor = anchor;
            Placement = placement;
        }
    }
}
