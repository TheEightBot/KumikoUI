using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using KumikoUI.Core.Components;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// An Uno <see cref="FrameworkElement"/> item for the grid's row menu (<see cref="KumikoDataGrid.RowMenuItems"/>, #331)
/// that supports XAML data-binding on every property, including <see cref="Command"/>.
/// </summary>
/// <remarks>
/// <code><![CDATA[
/// <kumiko:KumikoDataGrid.RowMenuItems>
///     <kumiko:UnoRowMenuItem Text="Duplicate" Command="{Binding DuplicateCommand}" />
/// </kumiko:KumikoDataGrid.RowMenuItems>
/// ]]></code>
/// Like <see cref="UnoActionButtonDefinition"/>, it derives from <see cref="FrameworkElement"/> only for
/// <c>DataContext</c>, is never added to the visual tree, and gets the grid's own <c>DataContext</c> from the grid, so a
/// plain <c>{Binding Path}</c> resolves against the page's view model. When the menu opens the item becomes a
/// <see cref="DrawnMenuItem"/>: its command receives the row's data item (null on a group header row) unless
/// <see cref="CommandParameter"/> is set, and the item is enabled only while <see cref="IsEnabled"/> is true and the
/// command can execute.
/// </remarks>
[SuppressMessage("Design", "CA1010:Generic interface should also be implemented",
    Justification = "Inherited from the Uno framework element, which enumerates its children; not part of the grid's own API.")]
public sealed partial class UnoRowMenuItem : FrameworkElement
{
    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(UnoRowMenuItem), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(UnoRowMenuItem), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(UnoRowMenuItem), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="IsEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(UnoRowMenuItem), new PropertyMetadata(true));

    /// <summary>The text shown. Default: empty.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>A stable identifier for the item, as <see cref="DrawnMenuItem.Key"/>. Default: empty.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The command executed when the item is chosen, if it can execute. Default null.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>The parameter passed to <see cref="Command"/>. When null, the row's data item is passed. Default null.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Whether the item can be chosen; a command that cannot execute disables it too. Default true.</summary>
    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>A callback run when the item is chosen, before <see cref="Command"/> (code-behind alternative). Default null.</summary>
    public Action? Action { get; set; }

    /// <summary>The menu item for the row whose data item is <paramref name="rowItem"/>.</summary>
    internal DrawnMenuItem ToDrawnMenuItem(object? rowItem)
    {
        var parameter = CommandParameter ?? rowItem;
        var command = Command;
        return new DrawnMenuItem(Key, Text, Action)
        {
            Command = command,
            CommandParameter = parameter,
            IsEnabled = IsEnabled && (command?.CanExecute(parameter) ?? true),
        };
    }
}
