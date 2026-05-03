using System.Windows.Input;

namespace KumikoUI.Core.Rendering;

/// <summary>
/// Defines a single action button rendered inside a grid cell.
/// Used by <see cref="ActionButtonsCellRenderer"/> (display) and
/// <see cref="KumikoUI.Core.Components.DrawnActionButtons"/> (interactive editor).
/// </summary>
/// <remarks>
/// Any number of buttons can be added. They are distributed with equal widths
/// across the available cell area, separated by <c>ButtonSpacing</c> and inset
/// by <c>CellPadding</c>.
///
/// <b>Code-behind usage</b> (capture per-row item in a lambda):
/// <code>
/// new ActionButtonDefinition
/// {
///     Label           = "Archive",
///     BackgroundColor = new GridColor(108, 117, 125),
///     Action          = () => vm.ArchiveCommand.Execute(item)
/// }
/// </code>
///
/// <b>XAML-friendly usage</b> (bind a command; the row item is passed as the
/// parameter when <see cref="CommandParameter"/> is not explicitly set):
/// <code>
/// &lt;render:ActionButtonDefinition Label="Archive"
///                                BackgroundColor="108,117,125"
///                                Command="{Binding ArchiveCommand}" /&gt;
/// </code>
/// </remarks>
public class ActionButtonDefinition : IActionButtonDefinition
{
    /// <summary>Text displayed on the button.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Button background color.</summary>
    public GridColor BackgroundColor { get; set; } = new GridColor(13, 110, 253);

    /// <summary>Button label text color.</summary>
    public GridColor TextColor { get; set; } = GridColor.White;

    /// <summary>
    /// Action invoked when this button is tapped in the interactive editor.
    /// Ignored by the display-only <see cref="ActionButtonsCellRenderer"/>.
    /// </summary>
    public Action? Action { get; set; }

    /// <summary>
    /// Command executed when this button is tapped (XAML data-binding friendly).
    /// When set, executed in addition to <see cref="Action"/> (if any).
    /// The parameter passed to <see cref="ICommand.Execute"/> is
    /// <see cref="CommandParameter"/> if set, otherwise the row item supplied by
    /// <see cref="KumikoUI.Core.Components.DrawnActionButtons.RowItem"/>.
    /// </summary>
    public ICommand? Command { get; set; }

    /// <summary>
    /// Explicit parameter passed to <see cref="Command"/>. When <see langword="null"/>,
    /// <see cref="KumikoUI.Core.Components.DrawnActionButtons.RowItem"/> is used instead.
    /// </summary>
    public object? CommandParameter { get; set; }
}
