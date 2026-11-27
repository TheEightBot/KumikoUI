using System.Windows.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// One entry of a <see cref="DrawnMenu"/>: a command with a text and an optional icon or check mark, or a separator
/// line between groups of commands.
/// </summary>
/// <remarks>
/// Choosing an enabled item (a click, a tap, Enter or Space) closes the menu, then raises
/// <see cref="DrawnMenu.ItemInvoked"/>, runs <see cref="Action"/> and executes <see cref="Command"/> with
/// <see cref="CommandParameter"/> when it can execute. Changing an item while its menu is open redraws the menu.
/// </remarks>
public sealed class DrawnMenuItem
{
    /// <summary>Creates an empty item; set <see cref="Text"/> and what it does.</summary>
    public DrawnMenuItem()
    {
    }

    /// <summary>Creates an item with a key, a text and an action.</summary>
    /// <param name="key">A stable identifier (see <see cref="Key"/>).</param>
    /// <param name="text">The text shown.</param>
    /// <param name="action">What choosing the item does, or null.</param>
    public DrawnMenuItem(string key, string text, Action? action = null)
    {
        Key = key ?? string.Empty;
        _text = text ?? string.Empty;
        Action = action;
    }

    /// <summary>Creates a separator: a line between two groups of items, which cannot be chosen.</summary>
    public static DrawnMenuItem Separator() => new() { IsSeparator = true };

    /// <summary>
    /// A stable identifier for the item, so code that edits a menu finds it whatever its (localized) text. The
    /// column menu's items use the keys in <see cref="KumikoColumnMenuItems"/>. Default: empty.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The text shown, and read by screen readers. Default: empty.</summary>
    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? string.Empty);
    }
    private string _text = string.Empty;

    /// <summary>
    /// The icon drawn before the text, through the menu's <see cref="DrawnComponent.IconProvider"/>, or null for
    /// none. A checked item shows its check mark instead.
    /// </summary>
    public KumikoIcon? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }
    private KumikoIcon? _icon;

    /// <summary>
    /// Whether the item can be chosen. A disabled item is drawn dimmed, is skipped by the arrow keys and does nothing
    /// when clicked. Default true.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }
    private bool _isEnabled = true;

    /// <summary>
    /// The item's check state: true draws a check mark before the text, false leaves the space empty, and null (the
    /// default) makes the item not checkable. Choosing an item does not change it; the action does that.
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }
    private bool? _isChecked;

    /// <summary>
    /// Whether the item is a separator line rather than a command. A separator at the start or the end of a menu,
    /// or right after another separator, is not drawn. Default false.
    /// </summary>
    public bool IsSeparator { get; init; }

    /// <summary>The command executed when the item is chosen, if it can execute. Default null.</summary>
    public ICommand? Command { get; set; }

    /// <summary>The parameter passed to <see cref="Command"/>. Default null.</summary>
    public object? CommandParameter { get; set; }

    /// <summary>The action run when the item is chosen, before <see cref="Command"/>. Default null.</summary>
    public Action? Action { get; set; }

    /// <summary>Whether the item can be highlighted and chosen: an enabled command, not a separator.</summary>
    internal bool IsSelectable => !IsSeparator && _isEnabled;

    /// <summary>Raised when something the menu draws changed (the text, the icon, the enabled or check state).</summary>
    internal event Action? Changed;

    /// <summary>Runs <see cref="Action"/>, then <see cref="Command"/> when it can execute.</summary>
    internal void Invoke()
    {
        Action?.Invoke();
        if (Command is { } command && command.CanExecute(CommandParameter))
            command.Execute(CommandParameter);
    }

    /// <inheritdoc />
    public override string ToString() => IsSeparator ? "----" : _text;

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke();
    }
}
