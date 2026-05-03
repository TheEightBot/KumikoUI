using System.Windows.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui;

/// <summary>
/// A MAUI <see cref="BindableObject"/> implementation of
/// <see cref="IActionButtonDefinition"/> that supports full XAML data-binding
/// on every property, including <see cref="Command"/>.
/// </summary>
/// <remarks>
/// Use this type instead of <see cref="ActionButtonDefinition"/> whenever you
/// want to bind a ViewModel <c>ICommand</c> directly in XAML:
///
/// <code><![CDATA[
/// <dg:MauiActionButtonDefinition
///     Label="Details"
///     BackgroundColor="13,110,253"
///     Command="{Binding Path=BindingContext.ViewDetailsCommand,
///                       Source={x:Reference actionsGrid}}" />
/// ]]></code>
///
/// Because <c>MauiActionButtonDefinition</c> is not part of the visual tree, it
/// does <b>not</b> inherit <c>BindingContext</c> automatically. Use
/// <c>Source={x:Reference …}</c> or <c>Source={RelativeSource …}</c> to point
/// bindings at the correct source object.
/// </remarks>
public class MauiActionButtonDefinition : BindableObject, IActionButtonDefinition
{
    // ── BindableProperty declarations ────────────────────────────

    /// <summary>Backing store for <see cref="Label"/>.</summary>
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(MauiActionButtonDefinition), string.Empty);

    /// <summary>Backing store for <see cref="BackgroundColor"/>.</summary>
    public static readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(nameof(BackgroundColor), typeof(GridColor), typeof(MauiActionButtonDefinition), new GridColor(13, 110, 253));

    /// <summary>Backing store for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(GridColor), typeof(MauiActionButtonDefinition), GridColor.White);

    /// <summary>Backing store for <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(MauiActionButtonDefinition), null);

    /// <summary>Backing store for <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(MauiActionButtonDefinition), null);

    // ── Properties ───────────────────────────────────────────────

    /// <summary>Text label rendered on the button face.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Button pill background color.
    /// Supports the <c>R,G,B</c> / <c>#RRGGBB</c> / named-color string formats
    /// from <see cref="GridColorTypeConverter"/> in XAML.
    /// </summary>
    public GridColor BackgroundColor
    {
        get => (GridColor)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>Button label text color. Defaults to <see cref="GridColor.White"/>.</summary>
    public GridColor TextColor
    {
        get => (GridColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>
    /// Command executed when this button is tapped.
    /// Bind to a ViewModel command using:
    /// <c>{Binding Path=BindingContext.MyCommand, Source={x:Reference gridName}}</c>
    /// </summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Explicit command parameter. When <see langword="null"/>, the row item
    /// (<see cref="KumikoUI.Core.Components.DrawnActionButtons.RowItem"/>) is passed
    /// to <see cref="Command"/> at runtime.
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// Optional callback invoked when the button is tapped (code-behind alternative
    /// to <see cref="Command"/>). Ignored by the display-only renderer.
    /// </summary>
    public Action? Action { get; set; }
}
