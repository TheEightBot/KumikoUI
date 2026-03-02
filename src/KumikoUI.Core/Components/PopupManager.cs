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
public class PopupManager
{
    private readonly List<PopupEntry> _popups = new();

    /// <summary>Are there any active popups?</summary>
    public bool HasActivePopups => _popups.Count > 0;

    /// <summary>
    /// Raised when the popup manager needs the hosting surface to redraw.
    /// Wire this to the same redraw callback used by the grid.
    /// </summary>
    public event Action? NeedsRedraw;

    /// <summary>Show a component as a popup at the given position.</summary>
    public void Show(DrawnComponent component, GridRect anchor, PopupPlacement placement = PopupPlacement.Below)
    {
        // Calculate popup position based on anchor and placement
        var bounds = CalculatePopupBounds(component, anchor, placement);
        component.Bounds = bounds;
        component.IsVisible = true;
        component.ZOrder = 1000 + _popups.Count; // Above normal components

        _popups.Add(new PopupEntry(component, anchor, placement));
        component.InvalidateVisual();
    }

    /// <summary>Close a specific popup.</summary>
    public void Close(DrawnComponent component)
    {
        var entry = _popups.Find(p => p.Component == component);
        if (entry != null)
        {
            _popups.Remove(entry);
            component.IsVisible = false;
            component.InvalidateVisual();
        }
    }

    /// <summary>Close all popups.</summary>
    public void CloseAll()
    {
        foreach (var entry in _popups.ToList())
        {
            entry.Component.IsVisible = false;
        }
        _popups.Clear();
    }

    /// <summary>Draw all active popups (call after main grid rendering).</summary>
    public void DrawPopups(IDrawingContext ctx)
    {
        foreach (var entry in _popups.OrderBy(p => p.Component.ZOrder))
        {
            if (entry.Component.IsVisible)
            {
                // Draw shadow
                DrawPopupShadow(ctx, entry.Component.Bounds);
                entry.Component.OnDraw(ctx);
            }
        }
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
    /// Behaviour:
    ///  • If the pointer hits an active popup → forward to that popup, consume event.
    ///  • If Pressed lands outside all popups → dismiss all popups.
    ///    The press is NOT consumed so the grid can still handle it (e.g. selecting a cell).
    /// </summary>
    public bool HandlePointer(GridPointerEventArgs e)
    {
        if (!HasActivePopups) return false;

        var hitPopup = HitTest(e.X, e.Y);

        if (hitPopup != null)
        {
            // Pointer is inside a popup — route the event to it
            switch (e.Action)
            {
                case InputAction.Pressed:
                    hitPopup.OnPointerDown(e);
                    break;
                case InputAction.Moved:
                    hitPopup.OnPointerMove(e);
                    break;
                case InputAction.Released:
                    hitPopup.OnPointerUp(e);
                    break;
            }

            e.Handled = true;
            NeedsRedraw?.Invoke();
            return true; // consumed
        }

        // Pointer is outside all popups
        if (e.Action == InputAction.Pressed)
        {
            // Dismiss all popups on press-outside
            CloseAll();
            NeedsRedraw?.Invoke();
            // Return false so the grid can still process the press
            // (e.g. selecting a cell, starting a pan, etc.)
        }

        if (e.Action == InputAction.Moved)
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
    /// Route a keyboard event to popups. Call this at the TOP of your
    /// key-handling pipeline.
    /// 
    /// Returns true if the event was consumed.
    /// </summary>
    public bool HandleKey(GridKeyEventArgs e)
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

    private static GridRect CalculatePopupBounds(
        DrawnComponent component, GridRect anchor, PopupPlacement placement)
    {
        float width = component.Bounds.Width > 0 ? component.Bounds.Width : anchor.Width;
        float height = component.Bounds.Height > 0 ? component.Bounds.Height : 200; // Default height

        return placement switch
        {
            PopupPlacement.Below => new GridRect(anchor.X, anchor.Y + anchor.Height, width, height),
            PopupPlacement.Above => new GridRect(anchor.X, anchor.Y - height, width, height),
            PopupPlacement.Right => new GridRect(anchor.X + anchor.Width, anchor.Y, width, height),
            PopupPlacement.Left => new GridRect(anchor.X - width, anchor.Y, width, height),
            _ => new GridRect(anchor.X, anchor.Y + anchor.Height, width, height)
        };
    }

    private static void DrawPopupShadow(IDrawingContext ctx, GridRect bounds)
    {
        var shadowColor = new GridColor(0, 0, 0, 40);
        var shadowPaint = new GridPaint { Color = shadowColor, Style = PaintStyle.Fill };
        // Offset shadow
        ctx.FillRect(new GridRect(bounds.X + 2, bounds.Y + 2, bounds.Width, bounds.Height), shadowPaint);
    }

    private class PopupEntry
    {
        public DrawnComponent Component { get; }
        public GridRect Anchor { get; }
        public PopupPlacement Placement { get; }

        public PopupEntry(DrawnComponent component, GridRect anchor, PopupPlacement placement)
        {
            Component = component;
            Anchor = anchor;
            Placement = placement;
        }
    }
}

/// <summary>Where to place a popup relative to its anchor.</summary>
public enum PopupPlacement
{
    /// <summary>Place the popup below the anchor.</summary>
    Below,
    /// <summary>Place the popup above the anchor.</summary>
    Above,
    /// <summary>Place the popup to the left of the anchor.</summary>
    Left,
    /// <summary>Place the popup to the right of the anchor.</summary>
    Right
}
