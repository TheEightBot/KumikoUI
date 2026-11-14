using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using Microsoft.Maui.ApplicationModel;

namespace KumikoUI.Maui;

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
/// 3) The operating system theme (<see cref="Application.RequestedTheme"/>), while
/// <see cref="FollowSystemTheme"/> is on. This is the default until you set a theme or style, so a dark app
/// gets a dark grid with no code.
/// </para>
/// <para>
/// While following the system, <see cref="Theme"/> keeps the value you last set (default
/// <see cref="KumikoThemeMode.Light"/>); the grid shows the system theme. Use
/// <c>Theme="{AppThemeBinding Light=Light, Dark=Dark}"</c> to choose the theme per OS theme yourself.
/// </para>
/// </remarks>
public partial class KumikoDataGrid
{
    // The precedence rules (shared with the Uno host and unit tested in Core).
    private readonly KumikoAppearance _appearance = new();
    private Application? _themedApplication;
    private bool _themingDisconnected;

    // ── Theme ──

    /// <summary>Identifies the <see cref="Theme"/> bindable property.</summary>
    public static readonly BindableProperty ThemeProperty = BindableProperty.Create(
        nameof(Theme), typeof(KumikoThemeMode), typeof(KumikoDataGrid), KumikoThemeMode.Light,
        // Coercion runs on every assignment, including one equal to the current value, which a plain
        // propertyChanged would miss (assigning Light to a default grid must still stop following the system).
        coerceValue: (b, v) =>
        {
            ((KumikoDataGrid)b).OnThemeAssigned((KumikoThemeMode)v);
            return v;
        });

    /// <summary>
    /// Gets or sets the built-in theme. Assigning it replaces <see cref="GridStyle"/> with a preset for the
    /// theme and turns <see cref="FollowSystemTheme"/> off; style-backed properties you set explicitly
    /// (<see cref="RowHeight"/>, <see cref="ShowHorizontalGridLines"/> and the others) are re-applied on top.
    /// Bindable, including <c>{AppThemeBinding Light=Light, Dark=Dark}</c>.
    /// </summary>
    public KumikoThemeMode Theme
    {
        get => (KumikoThemeMode)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    private void OnThemeAssigned(KumikoThemeMode theme)
    {
        var apply = _appearance.ThemeAssigned(theme);
        RefreshFollowing();
        if (apply is { } preset)
            SetStyle(KumikoTheme.Create(preset));
    }

    // ── GridStyle ──

    /// <summary>Identifies the <see cref="GridStyle"/> bindable property.</summary>
    public static readonly BindableProperty GridStyleProperty = BindableProperty.Create(
        nameof(GridStyle), typeof(KumikoStyle), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) => ((KumikoDataGrid)b).OnGridStyleAssigned((KumikoStyle?)n));

    /// <summary>
    /// Gets or sets the grid style. Assigning it replaces the current theme preset and turns
    /// <see cref="FollowSystemTheme"/> off; style-backed properties you set explicitly
    /// (<see cref="RowHeight"/>, <see cref="ShowHorizontalGridLines"/> and the others) are re-applied to the
    /// style you assign (the same object is modified). Assigning null goes back to the preset for
    /// <see cref="Theme"/> (or the system theme while following it). Reading never returns null.
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
        RefreshFollowing();
        SetStyle(style);
    }

    // ── ScrollSettings ──

    /// <summary>
    /// The momentum preset of the platform the grid runs on (#101): iOS on iPhone and iPad, Android on Android, desktop
    /// on Mac Catalyst and Windows.
    /// </summary>
    private static KumikoMomentumPreset PlatformMomentumPreset =>
#if IOS
        KumikoMomentumPreset.IOS;
#elif ANDROID
        KumikoMomentumPreset.Android;
#elif MACCATALYST || WINDOWS
        KumikoMomentumPreset.Desktop;
#else
        KumikoMomentumPreset.Default;
#endif

    /// <summary>Identifies the <see cref="ScrollSettings"/> bindable property.</summary>
    public static readonly BindableProperty ScrollSettingsProperty = BindableProperty.Create(
        nameof(ScrollSettings), typeof(ScrollSettings), typeof(KumikoDataGrid), null,
        propertyChanged: (b, _, n) =>
            ((KumikoDataGrid)b)._inputController.ScrollSettings = (ScrollSettings?)n ?? ScrollSettings.FromPreset(PlatformMomentumPreset));

    /// <summary>
    /// Gets or sets the scroll and inertia settings. Unset (or null), the grid uses its platform's momentum preset
    /// (<see cref="ScrollSettings.FromPreset"/>: iOS, Android, or desktop on Mac Catalyst and Windows). Reading never
    /// returns null.
    /// </summary>
    public ScrollSettings ScrollSettings
    {
        get => (ScrollSettings?)GetValue(ScrollSettingsProperty) ?? _inputController.ScrollSettings;
        set => SetValue(ScrollSettingsProperty, value);
    }

    // ── FollowSystemTheme ──

    /// <summary>Identifies the <see cref="FollowSystemTheme"/> bindable property.</summary>
    public static readonly BindableProperty FollowSystemThemeProperty = BindableProperty.Create(
        nameof(FollowSystemTheme), typeof(bool), typeof(KumikoDataGrid), true,
        coerceValue: (b, v) =>
        {
            ((KumikoDataGrid)b).OnFollowAssigned((bool)v);
            return v;
        });

    /// <summary>
    /// Gets or sets whether the grid follows the operating system theme (light or dark) as it changes at
    /// runtime. It is on by default until you assign <see cref="Theme"/> or <see cref="GridStyle"/>, which turns
    /// it off; assigning it yourself always wins over that, whatever the order. Reading returns the effective
    /// state. Turning it off shows <see cref="Theme"/> (unless you assigned a <see cref="GridStyle"/>).
    /// </summary>
    public bool FollowSystemTheme
    {
        get => _appearance.ShouldFollowSystem;
        set => SetValue(FollowSystemThemeProperty, value);
    }

    private void OnFollowAssigned(bool follow)
    {
        var apply = _appearance.FollowAssigned(follow, Theme, GetSystemTheme());
        RefreshFollowing();
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

    private static KumikoThemeMode GetSystemTheme()
        => Application.Current?.RequestedTheme == AppTheme.Dark ? KumikoThemeMode.Dark : KumikoThemeMode.Light;

    // Subscribes while the grid is attached and following the system; called again whenever either changes.
    private void RefreshFollowing()
    {
        var wanted = _appearance.ShouldFollowSystem && !_themingDisconnected ? Application.Current : null;
        if (ReferenceEquals(wanted, _themedApplication))
            return;

        if (_themedApplication != null)
            _themedApplication.RequestedThemeChanged -= OnRequestedThemeChanged;
        _themedApplication = wanted;
        if (_themedApplication != null)
            _themedApplication.RequestedThemeChanged += OnRequestedThemeChanged;
    }

    /// <summary>Starts following the system theme if enabled. Called from the constructor and Reconnect.</summary>
    private void ConnectTheming()
    {
        _themingDisconnected = false;
        RefreshFollowing();
        ApplySystemThemeIfFollowing();
    }

    /// <summary>Stops listening to the application so it no longer roots this control. Called from Cleanup.</summary>
    private void DisconnectTheming()
    {
        _themingDisconnected = true;
        RefreshFollowing();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (Dispatcher.IsDispatchRequired)
        {
            Dispatcher.Dispatch(ApplySystemThemeIfFollowing);
            return;
        }

        ApplySystemThemeIfFollowing();
    }

    private void ApplySystemThemeIfFollowing()
    {
        if (!_themingDisconnected && _appearance.SystemThemeChanged(GetSystemTheme()) is { } preset)
            SetStyle(KumikoTheme.Create(preset));
    }
}
