using System.Windows.Input;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Describes a single action button rendered inside a grid cell.
/// Implement this interface to create custom button definition types —
/// for example a MAUI <c>BindableObject</c> subclass that supports
/// <c>{Binding}</c> expressions on <c>Command</c>.
/// </summary>
/// <remarks>
/// Both <see cref="ActionButtonsCellRenderer"/> (display pass) and
/// <see cref="KumikoUI.Core.Components.DrawnActionButtons"/> (interactive editor)
/// consume this interface, so any implementation works in both contexts.
/// </remarks>
public interface IActionButtonDefinition
{
    /// <summary>Text label rendered on the button.</summary>
    string Label { get; }

    /// <summary>Button pill background color.</summary>
    GridColor BackgroundColor { get; }

    /// <summary>Button label text color.</summary>
    GridColor TextColor { get; }

    /// <summary>
    /// Optional callback invoked when the button is tapped.
    /// Ignored by the display-only <see cref="ActionButtonsCellRenderer"/>.
    /// </summary>
    Action? Action { get; }

    /// <summary>
    /// Command executed when the button is tapped.
    /// Ignored by the display-only <see cref="ActionButtonsCellRenderer"/>.
    /// The parameter is <see cref="CommandParameter"/> if set, otherwise the row
    /// item supplied by
    /// <see cref="KumikoUI.Core.Components.DrawnActionButtons.RowItem"/>.
    /// </summary>
    ICommand? Command { get; }

    /// <summary>
    /// Explicit parameter passed to <see cref="Command"/>.
    /// When <see langword="null"/>,
    /// <see cref="KumikoUI.Core.Components.DrawnActionButtons.RowItem"/> is used.
    /// </summary>
    object? CommandParameter { get; }
}
