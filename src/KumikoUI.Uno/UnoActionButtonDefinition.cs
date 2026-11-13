using System.Windows.Input;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace KumikoUI.Uno;

/// <summary>
/// An Uno <see cref="FrameworkElement"/> implementation of <see cref="IActionButtonDefinition"/>
/// that supports XAML data-binding on every property, including <see cref="Command"/>.
/// </summary>
/// <remarks>
/// Use this type instead of <see cref="ActionButtonDefinition"/> to declare action buttons in XAML
/// and bind a view model <c>ICommand</c>:
///
/// <code><![CDATA[
/// <render:ActionButtonsCellRenderer.Buttons>
///     <kumiko:UnoActionButtonDefinition
///         Label="Details"
///         BackgroundColor="#FF0D6EFD"
///         Command="{Binding ViewDetailsCommand}" />
/// </render:ActionButtonsCellRenderer.Buttons>
/// ]]></code>
///
/// It derives from <see cref="FrameworkElement"/> only for <c>DataContext</c>, which WinUI on Windows offers
/// on framework elements alone; it is never added to the visual tree and never measured or drawn.
/// Because it is not part of the visual tree, it does not inherit
/// <c>DataContext</c> by itself. <see cref="KumikoDataGrid"/> hands its own <c>DataContext</c> to the
/// definitions of every column whose <c>CustomCellRenderer</c> is an
/// <see cref="ActionButtonsCellRenderer"/>, so a plain <c>{Binding Path}</c> resolves against the grid's
/// view model (the page's <c>DataContext</c>, unless the grid sets its own).
/// The colors are <see cref="Color"/> values, like the grid's other color properties; the
/// <see cref="IActionButtonDefinition"/> members expose them as <see cref="KumikoColor"/>.
/// The values are cached when they change, so the render pass reads them without allocating.
/// </remarks>
public partial class UnoActionButtonDefinition : FrameworkElement, IActionButtonDefinition
{
    private static readonly Color DefaultBackgroundColor = Color.FromArgb(255, 13, 110, 253);
    private static readonly Color DefaultTextColor = Color.FromArgb(255, 255, 255, 255);

    private string _label = string.Empty;
    private KumikoColor _backgroundColor = new(13, 110, 253);
    private KumikoColor _textColor = KumikoColor.White;
    private ICommand? _command;
    private object? _commandParameter;

    // ── DependencyProperty declarations ──────────────────────────

    /// <summary>Identifies the <see cref="Label"/> dependency property.</summary>
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(UnoActionButtonDefinition),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var self = (UnoActionButtonDefinition)d;
                self._label = e.NewValue as string ?? string.Empty;
                self.RaiseChanged();
            }));

    /// <summary>Identifies the <see cref="BackgroundColor"/> dependency property.</summary>
    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(
            nameof(BackgroundColor), typeof(Color), typeof(UnoActionButtonDefinition),
            new PropertyMetadata(DefaultBackgroundColor, (d, e) =>
            {
                var self = (UnoActionButtonDefinition)d;
                self._backgroundColor = ToKumikoColor((Color)e.NewValue);
                self.RaiseChanged();
            }));

    /// <summary>Identifies the <see cref="TextColor"/> dependency property.</summary>
    public static readonly DependencyProperty TextColorProperty =
        DependencyProperty.Register(
            nameof(TextColor), typeof(Color), typeof(UnoActionButtonDefinition),
            new PropertyMetadata(DefaultTextColor, (d, e) =>
            {
                var self = (UnoActionButtonDefinition)d;
                self._textColor = ToKumikoColor((Color)e.NewValue);
                self.RaiseChanged();
            }));

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command), typeof(ICommand), typeof(UnoActionButtonDefinition),
            new PropertyMetadata(null, (d, e) =>
            {
                var self = (UnoActionButtonDefinition)d;
                self._command = e.NewValue as ICommand;
                self.RaiseChanged();
            }));

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter), typeof(object), typeof(UnoActionButtonDefinition),
            new PropertyMetadata(null, (d, e) =>
            {
                var self = (UnoActionButtonDefinition)d;
                self._commandParameter = e.NewValue;
                self.RaiseChanged();
            }));

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
    /// Command executed when this button is tapped. Bind it to a view model command with
    /// <c>{Binding MyCommand}</c>; the grid supplies its <c>DataContext</c> to the definition.
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
    public event EventHandler? Changed;

    // ── IActionButtonDefinition ──────────────────────────────────

    string IActionButtonDefinition.Label => _label;

    KumikoColor IActionButtonDefinition.BackgroundColor => _backgroundColor;

    KumikoColor IActionButtonDefinition.TextColor => _textColor;

    ICommand? IActionButtonDefinition.Command => _command;

    object? IActionButtonDefinition.CommandParameter => _commandParameter;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static KumikoColor ToKumikoColor(Color c) => new(c.R, c.G, c.B, c.A);
}
