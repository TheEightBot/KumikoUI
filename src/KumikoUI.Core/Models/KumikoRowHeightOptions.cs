namespace KumikoUI.Core.Models;

/// <summary>
/// The row height settings a host control exposes as properties (#320): <see cref="KumikoStyle.RowHeightMode"/>,
/// <see cref="KumikoStyle.MinRowHeight"/>, <see cref="KumikoStyle.MaxRowHeight"/>,
/// <see cref="KumikoStyle.CellVerticalPadding"/> and <see cref="KumikoStyle.RowHeightResolver"/>. Each one the host
/// has set is written into the current style at once and again after the style or theme is replaced, so an explicit
/// setting always wins over a theme, like <see cref="KumikoStyleOptions"/>. Unlike those, setting one back to its
/// default (<c>NaN</c> for a limit, <c>null</c> for the resolver) is also written, so it clears the style's value.
/// </summary>
internal sealed class KumikoRowHeightOptions
{
    private bool _hasMode, _hasMin, _hasMax, _hasPadding, _hasResolver;
    private KumikoRowHeightMode _mode;
    private float? _min, _max;
    private float _padding;
    private KumikoRowHeightResolver? _resolver;

    /// <summary>Sets <see cref="KumikoStyle.RowHeightMode"/> and writes it into <paramref name="style"/>.</summary>
    public void SetMode(KumikoRowHeightMode mode, KumikoStyle style)
    {
        _hasMode = true;
        _mode = mode;
        Apply(style);
    }

    /// <summary>Sets <see cref="KumikoStyle.MinRowHeight"/> (<c>NaN</c>: none) and writes it into <paramref name="style"/>.</summary>
    public void SetMinRowHeight(double value, KumikoStyle style)
    {
        _hasMin = true;
        _min = ToLimit(value);
        Apply(style);
    }

    /// <summary>Sets <see cref="KumikoStyle.MaxRowHeight"/> (<c>NaN</c>: none) and writes it into <paramref name="style"/>.</summary>
    public void SetMaxRowHeight(double value, KumikoStyle style)
    {
        _hasMax = true;
        _max = ToLimit(value);
        Apply(style);
    }

    /// <summary>Sets <see cref="KumikoStyle.CellVerticalPadding"/> and writes it into <paramref name="style"/>.</summary>
    public void SetCellVerticalPadding(double value, KumikoStyle style)
    {
        _hasPadding = true;
        _padding = double.IsFinite(value) ? (float)Math.Max(0, value) : 0f;
        Apply(style);
    }

    /// <summary>Sets <see cref="KumikoStyle.RowHeightResolver"/> and writes it into <paramref name="style"/>.</summary>
    public void SetResolver(KumikoRowHeightResolver? resolver, KumikoStyle style)
    {
        _hasResolver = true;
        _resolver = resolver;
        Apply(style);
    }

    /// <summary>Writes every setting the host has set into <paramref name="style"/>.</summary>
    public void Apply(KumikoStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (_hasMode) style.RowHeightMode = _mode;
        if (_hasMin) style.MinRowHeight = _min;
        if (_hasMax) style.MaxRowHeight = _max;
        if (_hasPadding) style.CellVerticalPadding = _padding;
        if (_hasResolver) style.RowHeightResolver = _resolver;
    }

    /// <summary>A host limit (<c>double</c>, <c>NaN</c> or infinite meaning none) as the style's nullable value.</summary>
    internal static float? ToLimit(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? null : (float)Math.Max(1, value);
}
