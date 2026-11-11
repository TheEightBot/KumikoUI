namespace KumikoUI.Core.Models;

/// <summary>
/// Decides which built-in theme a host should apply, given what the app assigned (<c>Theme</c>,
/// <c>GridStyle</c>, <c>FollowSystemTheme</c>) and the operating system theme. Each method returns the theme
/// preset the host must create and apply now, or null when nothing needs to change. Hosts own the
/// style instance; this type only holds the precedence rules so both hosts behave the same.
/// </summary>
/// <remarks>
/// Precedence: the last of Theme and GridStyle assigned wins; assigning either turns system following off unless
/// following was set explicitly (an explicit value always wins, in either order). Assigning a theme always
/// applies its preset unless the current style already is that untouched preset, so assigning the theme the
/// grid already reports after a custom style was assigned still replaces the custom style.
/// </remarks>
public sealed class KumikoAppearance
{
    private bool _explicit;
    private bool? _followExplicit;

    /// <summary>The preset the current style was created from, or null when the app supplied the style.</summary>
    public KumikoThemeMode? AppliedTheme { get; private set; } = KumikoThemeMode.Light;

    /// <summary>Whether the grid follows the system theme right now.</summary>
    public bool ShouldFollowSystem => _followExplicit ?? !_explicit;

    /// <summary>Records an explicit theme assignment; returns the preset to apply, or null if already applied.</summary>
    public KumikoThemeMode? ThemeAssigned(KumikoThemeMode theme)
    {
        _explicit = true;
        return Apply(AppliedTheme == theme ? null : theme);
    }

    /// <summary>Records an explicit style assignment; the host applies the style itself.</summary>
    public void StyleAssigned()
    {
        _explicit = true;
        AppliedTheme = null;
    }

    /// <summary>Records an explicit follow setting; returns the preset to apply, or null.</summary>
    public KumikoThemeMode? FollowAssigned(bool follow, KumikoThemeMode theme, KumikoThemeMode system)
    {
        _followExplicit = follow;
        if (follow) return Apply(system);
        return AppliedTheme != null ? Apply(theme) : null;
    }

    /// <summary>Returns the preset to apply for the system theme when following and it differs, else null.</summary>
    public KumikoThemeMode? SystemThemeChanged(KumikoThemeMode system)
        => ShouldFollowSystem && AppliedTheme != system ? Apply(system) : null;

    /// <summary>Returns the theme a style reset (assigning a null style) should apply.</summary>
    public KumikoThemeMode Resolve(KumikoThemeMode theme, KumikoThemeMode system)
        => Apply(ShouldFollowSystem ? system : theme)!.Value;

    private KumikoThemeMode? Apply(KumikoThemeMode? theme)
    {
        if (theme != null) AppliedTheme = theme;
        return theme;
    }
}
