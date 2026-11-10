using KumikoUI.Core.Components;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Renders an arbitrary set of action buttons inside a cell and runs them on tap. The cell is
/// interactive without being edited (<see cref="IInteractiveCellRenderer"/>): a tap executes the button
/// under the finger directly, with no editor and no edit events.
/// </summary>
/// <remarks>
/// <para>
/// A button whose <see cref="IActionButtonDefinition.Command"/> reports
/// <see cref="System.Windows.Input.ICommand.CanExecute"/> false is drawn disabled (the style's
/// <c>ActionButtonDisabled*</c> colors) and does nothing when tapped. The renderer listens to
/// <c>CanExecuteChanged</c> and asks the grid to redraw; the listeners hold this renderer weakly, so a
/// long-lived view model command never keeps it alive, and <see cref="Dispose"/> removes them.
/// </para>
/// <para>
/// Keyboard: Enter on the current action cell runs its first button when that button is enabled.
/// (Space keeps toggling the row's selection, so a stray press never runs a command.)
/// </para>
///
/// <code>
/// actionsColumn.CustomCellRenderer = new ActionButtonsCellRenderer
/// {
///     Buttons =
///     [
///         new() { Label = "Edit",   BackgroundColor = new KumikoColor(13, 110, 253) },
///         new() { Label = "Delete", BackgroundColor = new KumikoColor(220, 53, 69)  }
///     ]
/// };
/// </code>
/// </remarks>
public class ActionButtonsCellRenderer : ICellRenderer, ICellEditorProvider, IInteractiveCellRenderer, IDisposable
{
    private IList<IActionButtonDefinition> _buttons = new List<IActionButtonDefinition>();
    private readonly Dictionary<System.Windows.Input.ICommand, CommandListener> _listeners = new();

    /// <summary>
    /// The ordered list of buttons to render.
    /// Buttons are distributed with equal widths across the cell.
    /// </summary>
    public IList<IActionButtonDefinition> Buttons
    {
        get => _buttons;
        set
        {
            if (ReferenceEquals(_buttons, value)) return;
            StopListening();
            _buttons = value ?? new List<IActionButtonDefinition>();
        }
    }

    /// <summary>Corner radius for all button pills.</summary>
    public float CornerRadius { get; set; } = 5f;

    /// <summary>Horizontal gap between adjacent buttons (pixels).</summary>
    public float ButtonSpacing { get; set; } = 6f;

    /// <summary>Inset from all four cell edges (pixels).</summary>
    public float CellPadding { get; set; } = 4f;

    /// <inheritdoc />
    public event Action? RedrawRequested;

    /// <inheritdoc />
    public void Render(
        IDrawingContext ctx, KumikoRect cellRect, object? value,
        string displayText, KumikoColumn column,
        KumikoStyle style, bool isSelected,
        KumikoCellStyle? cellStyle = null)
    {
        if (Buttons.Count == 0) return;
        var rects = ActionButtonLayout.ComputeButtonRects(cellRect, Buttons.Count, CellPadding, ButtonSpacing);

        for (int i = 0; i < Buttons.Count; i++)
        {
            var btn = Buttons[i];
            Listen(btn.Command);
            ActionButtonPainter.Draw(ctx, rects[i], btn, CornerRadius,
                enabled: ActionButtonPainter.IsEnabled(btn, value), pressed: false,
                style.ActionButtonDisabledBackgroundColor, style.ActionButtonDisabledTextColor);
        }
    }

    /// <inheritdoc />
    public bool OnCellTap(KumikoRect cellRect, float x, float y, object? value)
    {
        var rects = ActionButtonLayout.ComputeButtonRects(cellRect, Buttons.Count, CellPadding, ButtonSpacing);
        for (int i = 0; i < rects.Count; i++)
        {
            if (!rects[i].Contains(x, y)) continue;
            // The button may remove the row: nothing else is read after it runs
            return ActionButtonPainter.TryInvoke(Buttons[i], value);
        }
        return false;
    }

    /// <inheritdoc />
    /// <remarks>Runs the first button when it is enabled; a disabled first button does nothing.</remarks>
    public bool OnCellActivate(object? value)
        => Buttons.Count > 0 && ActionButtonPainter.TryInvoke(Buttons[0], value);

    /// <summary>Stops listening to every command's <c>CanExecuteChanged</c>.</summary>
    public void Dispose()
    {
        StopListening();
        GC.SuppressFinalize(this);
    }

    private void Listen(System.Windows.Input.ICommand? command)
    {
        if (command == null || _listeners.ContainsKey(command)) return;
        _listeners[command] = new CommandListener(this, command);
    }

    private void StopListening()
    {
        foreach (var listener in _listeners.Values) listener.Detach();
        _listeners.Clear();
    }

    private void OnCanExecuteChanged() => RedrawRequested?.Invoke();

    /// <summary>Weak link from a command to its renderer: the command never keeps the renderer alive.</summary>
    private sealed class CommandListener
    {
        private readonly WeakReference<ActionButtonsCellRenderer> _owner;
        private readonly System.Windows.Input.ICommand _command;

        public CommandListener(ActionButtonsCellRenderer owner, System.Windows.Input.ICommand command)
        {
            _owner = new WeakReference<ActionButtonsCellRenderer>(owner);
            _command = command;
            command.CanExecuteChanged += OnChanged;
        }

        public void Detach() => _command.CanExecuteChanged -= OnChanged;

        private void OnChanged(object? sender, EventArgs e)
        {
            if (_owner.TryGetTarget(out var owner)) owner.OnCanExecuteChanged();
            else Detach();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Kept for hosts that open the buttons as an editor themselves. The grid does not: a tap on an action
    /// cell runs the button without an editor (<see cref="IInteractiveCellRenderer"/>).
    /// </remarks>
    public DrawnComponent? CreateEditor(object? value, KumikoRect bounds)
        => new KumikoUI.Core.Components.DrawnActionButtons
        {
            Bounds  = bounds,
            RowItem = value,
            Buttons = Buttons,
            CornerRadius  = CornerRadius,
            ButtonSpacing = ButtonSpacing,
            CellPadding   = CellPadding
        };
}
