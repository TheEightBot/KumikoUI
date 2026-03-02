using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Manages keyboard focus among drawn components.
/// Only one component at a time can have focus.
/// </summary>
public class FocusManager
{
    private readonly List<DrawnComponent> _components = new();
    private DrawnComponent? _focused;

    /// <summary>The currently focused component (null = none).</summary>
    public DrawnComponent? FocusedComponent => _focused;

    /// <summary>Register a component for focus management.</summary>
    public void Register(DrawnComponent component)
    {
        if (!_components.Contains(component))
            _components.Add(component);
    }

    /// <summary>Unregister a component.</summary>
    public void Unregister(DrawnComponent component)
    {
        _components.Remove(component);
        if (_focused == component)
            _focused = null;
    }

    /// <summary>Set focus to the given component (null to clear).</summary>
    public void SetFocus(DrawnComponent? component)
    {
        if (_focused == component) return;

        var old = _focused;
        old?.SetFocusDirect(false);
        _focused = component;
        component?.SetFocusDirect(true);

        FocusChanged?.Invoke(this, new FocusChangedEventArgs(old, component));
    }

    /// <summary>Clear focus from all components.</summary>
    public void ClearFocus() => SetFocus(null);

    /// <summary>Move focus to the next component in tab order.</summary>
    public void FocusNext()
    {
        var focusable = _components.Where(c => c.IsVisible && c.IsEnabled).ToList();
        if (focusable.Count == 0) return;

        int idx = _focused != null ? focusable.IndexOf(_focused) : -1;
        int next = (idx + 1) % focusable.Count;
        SetFocus(focusable[next]);
    }

    /// <summary>Move focus to the previous component in tab order.</summary>
    public void FocusPrevious()
    {
        var focusable = _components.Where(c => c.IsVisible && c.IsEnabled).ToList();
        if (focusable.Count == 0) return;

        int idx = _focused != null ? focusable.IndexOf(_focused) : 0;
        int prev = (idx - 1 + focusable.Count) % focusable.Count;
        SetFocus(focusable[prev]);
    }

    /// <summary>Dispatch a key event to the focused component.</summary>
    public bool DispatchKey(GridKeyEventArgs e)
    {
        if (_focused == null || !_focused.IsEnabled) return false;
        return e.IsKeyDown ? _focused.OnKeyDown(e) : _focused.OnKeyUp(e);
    }

    /// <summary>Dispatch a pointer event. Tests components in Z-order (highest first).</summary>
    public bool DispatchPointer(GridPointerEventArgs e)
    {
        // Check components in reverse Z-order (highest on top first)
        var sorted = _components
            .Where(c => c.IsVisible && c.IsEnabled)
            .OrderByDescending(c => c.ZOrder)
            .ToList();

        foreach (var comp in sorted)
        {
            if (comp.HitTest(e.X, e.Y))
            {
                if (e.Action == InputAction.Pressed)
                    SetFocus(comp);

                bool handled = e.Action switch
                {
                    InputAction.Pressed => comp.OnPointerDown(e),
                    InputAction.Released => comp.OnPointerUp(e),
                    InputAction.Moved => comp.OnPointerMove(e),
                    _ => false
                };

                if (handled) return true;
            }
        }

        // Clicked outside all components — clear focus
        if (e.Action == InputAction.Pressed)
            ClearFocus();

        return false;
    }

    /// <summary>Raised when the focused component changes.</summary>
    public event EventHandler<FocusChangedEventArgs>? FocusChanged;
}

/// <summary>Provides data for the <see cref="FocusManager.FocusChanged"/> event.</summary>
public class FocusChangedEventArgs : EventArgs
{
    /// <summary>The component that previously held focus, or <c>null</c> if none.</summary>
    public DrawnComponent? OldFocus { get; }
    /// <summary>The component that now holds focus, or <c>null</c> if focus was cleared.</summary>
    public DrawnComponent? NewFocus { get; }

    /// <summary>Initializes a new instance of the <see cref="FocusChangedEventArgs"/> class.</summary>
    /// <param name="oldFocus">The previously focused component.</param>
    /// <param name="newFocus">The newly focused component.</param>
    public FocusChangedEventArgs(DrawnComponent? oldFocus, DrawnComponent? newFocus)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }
}

/// <summary>Extension to allow setting focus internally without going through FocusManager.</summary>
internal static class DrawnComponentFocusExtensions
{
    internal static void SetFocusDirect(this DrawnComponent component, bool focused)
    {
        component.IsFocused = focused;
    }
}
