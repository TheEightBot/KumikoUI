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
///     BackgroundColor="#0D6EFD"
///     Command="{Binding ViewDetailsCommand}" />
/// ]]></code>
///
/// Because <c>MauiActionButtonDefinition</c> is not part of the visual tree, it
/// does not inherit <c>BindingContext</c> by itself. <see cref="KumikoDataGrid"/> hands its own
/// <c>BindingContext</c> to the definitions of every column whose <c>CustomCellRenderer</c> is an
/// <see cref="ActionButtonsCellRenderer"/>, so a plain <c>{Binding Path}</c> resolves against the grid's
/// view model (the page's <c>BindingContext</c>, unless the grid sets its own), as
/// <c>UnoActionButtonDefinition</c> does with <c>DataContext</c>. <c>Source={x:Reference …}</c> still works. The
/// grid redraws when a property of a definition changes after it is shown.
/// The colors are <see cref="Color"/> values, like the grid's other color properties (named colors, hex, resources and
/// <c>AppThemeBinding</c> work); the <see cref="IActionButtonDefinition"/> members expose them as
/// <see cref="KumikoColor"/>, cached when they change so the render pass reads them without converting.
/// </remarks>
public sealed class MauiActionButtonDefinition : BindableObject, IActionButtonDefinition
{
    private static readonly KumikoColor DefaultBackground = new(13, 110, 253);

    private KumikoColor _backgroundColor = DefaultBackground;
    private KumikoColor _textColor = KumikoColor.White;

    // ── BindableProperty declarations ────────────────────────────

    /// <summary>Backing store for <see cref="Label"/>.</summary>
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(MauiActionButtonDefinition), string.Empty);

    /// <summary>Backing store for <see cref="BackgroundColor"/>.</summary>
    public static readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(MauiActionButtonDefinition),
            Color.FromRgb(13, 110, 253),
            propertyChanged: (b, _, n) => ((MauiActionButtonDefinition)b)._backgroundColor = ToKumikoColor((Color?)n, DefaultBackground));

    /// <summary>Backing store for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(MauiActionButtonDefinition), Colors.White,
            propertyChanged: (b, _, n) => ((MauiActionButtonDefinition)b)._textColor = ToKumikoColor((Color?)n, KumikoColor.White));

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

    /// <summary>Button pill background color. Defaults to a blue (13, 110, 253).</summary>
    public Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>Button label text color. Defaults to white.</summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
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

    /// <summary>Raised after any property of this definition changed, so a host can redraw.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc/>
    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName != BindingContextProperty.PropertyName)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── IActionButtonDefinition ──────────────────────────────────

    KumikoColor IActionButtonDefinition.BackgroundColor => _backgroundColor;

    KumikoColor IActionButtonDefinition.TextColor => _textColor;

    private static KumikoColor ToKumikoColor(Color? color, KumikoColor fallback)
    {
        if (color is null) return fallback;
        color.ToRgba(out byte r, out byte g, out byte b, out byte a);
        return new KumikoColor(r, g, b, a);
    }
}
