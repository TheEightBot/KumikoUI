using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;

namespace KumikoUI.Uno;

/// <summary>
/// The appearance surface of <see cref="KumikoDataGrid"/>: <see cref="Theme"/>, <see cref="GridStyle"/>,
/// <see cref="ScrollSettings"/> and <see cref="FollowSystemTheme"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence, highest first.</b>
/// 1) The style-backed overrides you set on the control (<see cref="RowHeight"/>, <see cref="HeaderHeight"/>,
/// <see cref="ShowHorizontalGridLines"/> and the others): they are re-applied to whatever style is current,
/// so XAML attribute order never matters.
/// 2) The last of <see cref="Theme"/> and <see cref="GridStyle"/> you assigned. Assigning either one turns
/// <see cref="FollowSystemTheme"/> off (unless you also set <see cref="FollowSystemTheme"/> explicitly, which
/// always wins, in either order). Assigning <see cref="Theme"/> always applies that preset, even when the value
/// is unchanged and even after a custom <see cref="GridStyle"/> replaced it.
/// 3) The element theme (<see cref="FrameworkElement.ActualTheme"/>: the application theme, or a
/// <c>RequestedTheme</c> set on an ancestor), while <see cref="FollowSystemTheme"/> is on. This is the default
/// until you set a theme or style, so a dark app gets a dark grid with no code.
/// </para>
/// <para>
/// While following, <see cref="Theme"/> keeps the value you last set (default
/// <see cref="KumikoThemeMode.Light"/>); the grid shows the element theme. A value assigned in XAML that equals
/// a default is noticed when the control loads.
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    // The precedence rules (shared with the MAUI host and unit tested in Core).
    private readonly KumikoAppearance _appearance = new();
    private bool _themingConnected;
    private bool _localValuesSynced;

    // ── Theme ──

    /// <summary>Identifies the <see cref="Theme"/> dependency property.</summary>
    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(
            nameof(Theme), typeof(KumikoThemeMode), typeof(KumikoDataGrid),
            new PropertyMetadata(KumikoThemeMode.Light, (d, e) =>
                ((KumikoDataGrid)d).OnThemeAssigned((KumikoThemeMode)e.NewValue)));

    /// <summary>
    /// Gets or sets the built-in theme. Assigning it replaces <see cref="GridStyle"/> with a preset for the
    /// theme and turns <see cref="FollowSystemTheme"/> off; style-backed properties you set explicitly
    /// (<see cref="RowHeight"/>, <see cref="ShowHorizontalGridLines"/> and the others) are re-applied on top.
    /// </summary>
    public KumikoThemeMode Theme
    {
        get => (KumikoThemeMode)GetValue(ThemeProperty);
        set
        {
            var unchanged = Theme == value;
            SetValue(ThemeProperty, value);
            // No change callback for an equal value, but it is still an explicit assignment.
            if (unchanged)
                OnThemeAssigned(value);
        }
    }

    private void OnThemeAssigned(KumikoThemeMode theme)
    {
        var apply = _appearance.ThemeAssigned(theme);
        UpdateThemeSubscription();
        if (apply is { } preset)
            SetStyle(KumikoTheme.Create(preset));
    }

    // ── GridStyle ──

    /// <summary>Identifies the <see cref="GridStyle"/> dependency property.</summary>
    public static readonly DependencyProperty GridStyleProperty =
        DependencyProperty.Register(
            nameof(GridStyle), typeof(KumikoStyle), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) => ((KumikoDataGrid)d).OnGridStyleAssigned((KumikoStyle?)e.NewValue)));

    /// <summary>
    /// Gets or sets the grid style. Assigning it replaces the current theme preset and turns
    /// <see cref="FollowSystemTheme"/> off; style-backed properties you set explicitly
    /// (<see cref="RowHeight"/>, <see cref="ShowHorizontalGridLines"/> and the others) are re-applied to the
    /// style you assign (the same object is modified). Assigning null goes back to the preset for
    /// <see cref="Theme"/> (or the element theme while following it). Reading never returns null.
    /// </summary>
    public KumikoStyle GridStyle
    {
        get => (KumikoStyle?)GetValue(GridStyleProperty) ?? _style;
        set
        {
            var unchanged = ReferenceEquals(GetValue(GridStyleProperty), value);
            SetValue(GridStyleProperty, value);
            // Re-assigning the same instance (after editing it) raises no change notification but must repaint.
            if (unchanged)
                OnGridStyleAssigned(value);
        }
    }

    private void OnGridStyleAssigned(KumikoStyle? style)
    {
        if (style is null)
        {
            SetStyle(KumikoTheme.Create(_appearance.Resolve(Theme, GetSystemTheme())));
            return;
        }

        _appearance.StyleAssigned();
        UpdateThemeSubscription();
        SetStyle(style);
    }

    // ── ScrollSettings ──

    /// <summary>
    /// The momentum preset of the head the grid runs on (#101): iOS, Android, or desktop (Windows, macOS, Linux and
    /// the browser); in a browser whose main pointer is a finger (CSS <c>pointer: coarse</c>), the Android preset.
    /// </summary>
    private static KumikoMomentumPreset PlatformMomentumPreset =>
        OperatingSystem.IsIOS() ? KumikoMomentumPreset.IOS
        : OperatingSystem.IsAndroid() ? KumikoMomentumPreset.Android
        : KumikoUI.Uno.Input.BrowserMedia.HasCoarsePointer ? KumikoMomentumPreset.Android // a phone or tablet browser
        : KumikoMomentumPreset.Desktop;

    /// <summary>Identifies the <see cref="ScrollSettings"/> dependency property.</summary>
    public static readonly DependencyProperty ScrollSettingsProperty =
        DependencyProperty.Register(
            nameof(ScrollSettings), typeof(ScrollSettings), typeof(KumikoDataGrid),
            new PropertyMetadata(null, (d, e) =>
                ((KumikoDataGrid)d)._inputController.ScrollSettings =
                    (ScrollSettings?)e.NewValue ?? ScrollSettings.FromPreset(PlatformMomentumPreset)));

    /// <summary>
    /// Gets or sets the scroll and inertia settings. Unset (or null), the grid uses its head's momentum preset
    /// (<see cref="ScrollSettings.FromPreset"/>: iOS, Android, or desktop on Windows, macOS, Linux and the browser).
    /// Reading never returns null.
    /// </summary>
    public ScrollSettings ScrollSettings
    {
        get => (ScrollSettings?)GetValue(ScrollSettingsProperty) ?? _inputController.ScrollSettings;
        set => SetValue(ScrollSettingsProperty, value);
    }

    // ── FollowSystemTheme ──

    /// <summary>Identifies the <see cref="FollowSystemTheme"/> dependency property.</summary>
    public static readonly DependencyProperty FollowSystemThemeProperty =
        DependencyProperty.Register(
            nameof(FollowSystemTheme), typeof(bool), typeof(KumikoDataGrid),
            new PropertyMetadata(true, (d, e) => ((KumikoDataGrid)d).OnFollowAssigned((bool)e.NewValue)));

    /// <summary>
    /// Gets or sets whether the grid follows the element theme (light or dark) as it changes at runtime. It is
    /// on by default until you assign <see cref="Theme"/> or <see cref="GridStyle"/>, which turns it off;
    /// assigning it yourself always wins over that, whatever the order. Reading returns the effective state.
    /// Turning it off shows <see cref="Theme"/> (unless you assigned a <see cref="GridStyle"/>).
    /// </summary>
    public bool FollowSystemTheme
    {
        get => _appearance.ShouldFollowSystem;
        set
        {
            var unchanged = (bool)GetValue(FollowSystemThemeProperty) == value;
            SetValue(FollowSystemThemeProperty, value);
            if (unchanged)
                OnFollowAssigned(value);
        }
    }

    private void OnFollowAssigned(bool follow)
    {
        var apply = _appearance.FollowAssigned(follow, Theme, GetSystemTheme());
        UpdateThemeSubscription();
        if (apply is { } preset)
            SetStyle(KumikoTheme.Create(preset));
    }

    // ── Plumbing ──

    private void SetStyle(KumikoStyle style)
    {
        _style = style;
        ApplyStyleOptions();
        _editSession.Style = style;
        InvalidateSurface();
    }

    private KumikoThemeMode GetSystemTheme()
    {
        var theme = ActualTheme;
        if (theme == ElementTheme.Default)
            theme = Application.Current?.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        return theme == ElementTheme.Dark ? KumikoThemeMode.Dark : KumikoThemeMode.Light;
    }

    // Subscribes while the grid is loaded and following; called again whenever either changes.
    private void UpdateThemeSubscription()
    {
        var wanted = _themingConnected && _appearance.ShouldFollowSystem;
        ActualThemeChanged -= OnActualThemeChanged;
        if (wanted)
            ActualThemeChanged += OnActualThemeChanged;
    }

    // Values set in XAML that equal a default raise no change callback; notice them once, at first load.
    private void SyncLocalValues()
    {
        if (_localValuesSynced)
            return;
        _localValuesSynced = true;

        if (ReadLocalValue(GridStyleProperty) != DependencyProperty.UnsetValue && GetValue(GridStyleProperty) is { } style)
            OnGridStyleAssigned((KumikoStyle)style);
        if (ReadLocalValue(ThemeProperty) != DependencyProperty.UnsetValue)
            OnThemeAssigned(Theme);
        if (ReadLocalValue(FollowSystemThemeProperty) != DependencyProperty.UnsetValue)
            OnFollowAssigned((bool)GetValue(FollowSystemThemeProperty));
    }

    /// <summary>Starts following the element theme if enabled. Called from the constructor and, with <paramref name="loaded"/>, from OnLoaded.</summary>
    private void ConnectTheming(bool loaded = false)
    {
        _themingConnected = true;
        if (loaded)
            SyncLocalValues();
        UpdateThemeSubscription();
        ApplySystemThemeIfFollowing();
    }

    /// <summary>Stops listening so the control is not rooted by the element. Called from OnUnloaded.</summary>
    private void DisconnectTheming()
    {
        _themingConnected = false;
        UpdateThemeSubscription();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => ApplySystemThemeIfFollowing();

    private void ApplySystemThemeIfFollowing()
    {
        if (_appearance.SystemThemeChanged(GetSystemTheme()) is { } preset)
            SetStyle(KumikoTheme.Create(preset));
    }
}
