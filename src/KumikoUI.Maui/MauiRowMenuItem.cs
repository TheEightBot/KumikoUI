using System.Windows.Input;
using KumikoUI.Core.Components;

namespace KumikoUI.Maui;

/// <summary>
/// A <see cref="BindableObject"/> item for the grid's row menu (<see cref="KumikoDataGrid.RowMenuItems"/>, #331) that
/// supports XAML data-binding on every property, including <see cref="Command"/>.
/// </summary>
/// <remarks>
/// <code><![CDATA[
/// <dg:KumikoDataGrid.RowMenuItems>
///     <dg:MauiRowMenuItem Text="Duplicate" Command="{Binding DuplicateCommand}" />
/// </dg:KumikoDataGrid.RowMenuItems>
/// ]]></code>
/// Like <see cref="MauiActionButtonDefinition"/>, an item is not in the visual tree, so <see cref="KumikoDataGrid"/> hands
/// it its own <c>BindingContext</c> and a plain <c>{Binding Path}</c> resolves against the page's view model. When the
/// menu opens the item becomes a <see cref="DrawnMenuItem"/>: its command receives the row's data item (null on a group
/// header row) unless <see cref="CommandParameter"/> is set, and the item is enabled only while
/// <see cref="IsEnabled"/> is true and the command can execute.
/// </remarks>
public sealed class MauiRowMenuItem : BindableObject
{
    /// <summary>Backing store for <see cref="Text"/>.</summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(MauiRowMenuItem), string.Empty);

    /// <summary>Backing store for <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(MauiRowMenuItem), null);

    /// <summary>Backing store for <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(MauiRowMenuItem), null);

    /// <summary>Backing store for <see cref="IsEnabled"/>.</summary>
    public static readonly BindableProperty IsEnabledProperty =
        BindableProperty.Create(nameof(IsEnabled), typeof(bool), typeof(MauiRowMenuItem), true);

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
