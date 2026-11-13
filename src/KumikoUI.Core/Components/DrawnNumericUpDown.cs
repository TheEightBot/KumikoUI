using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn numeric up/down control with side-by-side [−] value [+] layout.
/// </summary>
/// <remarks>
/// <para>
/// The number is shown and parsed in <see cref="Culture"/> (decimal and group separators, negative sign) and
/// is always rounded: to <see cref="DecimalPlaces"/>, or, when that is -1, to the precision of
/// <see cref="Step"/> (so stepping by 0.1 shows 0.3, never 0.30000000000000004).
/// <see cref="Minimum"/>, <see cref="Maximum"/> and <see cref="Step"/> are kept sane: the value is clamped to
/// the limits, <see cref="Minimum"/> never exceeds <see cref="Maximum"/>, and stepping near the ends of the
/// <see cref="double"/> range cannot overflow.
/// </para>
/// <para>
/// A tap on the number, or typing, starts typed entry in a <see cref="DrawnTextBox"/>: the text is applied
/// (parsed in the culture, clamped and rounded) on Enter, when the control loses focus, or when a button or
/// arrow key steps. Text that is not a number is not applied: the entry is drawn with an error border and
/// <see cref="IValueEditor.Value"/> reports something no type converts from, so the edit session refuses it through its
/// ordinary validation path instead of committing garbage.
/// </para>
/// <para>
/// Holding a [−]/[+] button, or the Up/Down arrow key, repeats the step after <see cref="RepeatDelayMs"/>,
/// faster the longer it is held, and in larger steps after a long hold. The repetition is driven by
/// <see cref="UpdateAnimation"/> (the grid's hosts call it every frame while <see cref="IsAnimating"/>),
/// not by a timer. A held key is detected by the operating system's key repeat: a host that delivers key-up
/// events ends the hold as soon as the key is released, otherwise it ends when the repeat stops.
/// </para>
/// </remarks>
public class DrawnNumericUpDown : DrawnComponent, IValueEditor
{
    private const int MaxPrecision = 15;
    private const int AutoPrecision = 10;
    private const int MaxTicksPerFrame = 16;
    private const int TicksPerAccelerationStage = 20;
    private const double MaxStepMultiplier = 1000;
    private const float KeyReleaseTimeoutMs = 300f;
    private const float KeyInitialRepeatTimeoutMs = 1500f;

    private static readonly string[] FixedFormats =
        Enumerable.Range(0, MaxPrecision + 1).Select(i => "F" + i.ToString(CultureInfo.InvariantCulture)).ToArray();

    private double _value;
    private double _minimum = double.MinValue;
    private double _maximum = double.MaxValue;
    private double _step = 1;
    private int _decimalPlaces = -1;
    private string _format = "G";
    private CultureInfo? _culture;

    // Typed entry
    private readonly DrawnTextBox _box;
    private bool _typing;
    private bool _textDirty;
    private bool _invalidText;

    // Hold-to-repeat: a pressed button or a held arrow key, stepped by UpdateAnimation
    private int _holdDirection;        // +1 increment, -1 decrement, 0 not holding
    private KumikoKey _holdKey = KumikoKey.None;
    private bool _holdIsKey;
    private bool _keyRepeatSeen;
    private float _keyIdleMs;
    private float _holdMs;
    private float _nextTickMs;
    private int _ticks;

    // Display text cache: the text is rebuilt only when what it shows changes, so drawing allocates nothing.
    private string _text = string.Empty;
    private bool _textValid;
    private double _textValue;
    private int _textDecimalPlaces;
    private string? _textFormat;
    private CultureInfo? _textCulture;

    // Paints reused by every draw
    private readonly KumikoPaint _fillPaint = new() { Style = PaintStyle.Fill };
    private readonly KumikoPaint _strokePaint = new() { Style = PaintStyle.Stroke };
    private readonly KumikoPaint _symbolPaint = new() { Style = PaintStyle.Stroke, StrokeWidth = 2f, IsAntiAlias = true };
    private readonly KumikoPaint _textPaint = new() { Style = PaintStyle.Fill, IsAntiAlias = true };

    /// <summary>Creates a numeric up/down showing 0, with no limits and a step of 1.</summary>
    public DrawnNumericUpDown()
    {
        _box = new DrawnTextBox
        {
            NumericOnly = true,
            BorderWidth = 0,
            Padding = 2f,
            BackgroundColor = BackgroundColor,
            EditingBackgroundColor = BackgroundColor,
        };
        _box.TextChanged += (_, _) => OnTextEdited();
        _box.RedrawRequested += InvalidateVisual;
    }

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used to render the numeric value text.</summary>
    public KumikoFont Font { get; set; } = new("Default", 14);
    /// <summary>Color of the displayed numeric value text.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the control.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border color of the control when not focused.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color of the control when focused.</summary>
    public KumikoColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Border color of the control while the typed text is not a number.</summary>
    public KumikoColor ErrorBorderColor { get; set; } = new(220, 50, 50);
    /// <summary>Background color of the increment and decrement buttons.</summary>
    public KumikoColor ButtonBackgroundColor { get; set; } = new(240, 240, 240);
    /// <summary>Color of the plus and minus symbols on the buttons.</summary>
    public KumikoColor ButtonSymbolColor { get; set; } = new(60, 60, 60);
    /// <summary>Background color of a button while it is being pressed.</summary>
    public KumikoColor ButtonPressedColor { get; set; } = new(200, 200, 200);
    /// <summary>Inner padding between the control border and its content.</summary>
    public float Padding { get; set; } = 4f;

    /// <summary>
    /// Width of each [−] / [+] button, or 0 (the default) to size them from the control: as wide as the
    /// control is tall (square touch targets, at least 24), but never more than 30% of the width, so the
    /// number keeps room in a narrow cell. An explicit width is limited to a third of the control's width.
    /// </summary>
    public float ButtonWidth { get; set; }

    /// <summary>Corner radius for the outer shape and buttons.</summary>
    public float CornerRadius { get; set; } = 4f;

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Sets the font of the number, so the editor draws and measures its text like the grid's cells.
    /// <see cref="ApplyTheme"/> calls it with the grid's cell font; the grid calls it again with the column's
    /// own font when the column has one.
    /// </summary>
    /// <param name="font">The font to use: family, size, bold and italic.</param>
    public void ApplyFont(KumikoFont font)
    {
        Font = font;
        _box.Font = font;
        InvalidateVisual();
    }

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the numeric
    /// up/down visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        IconProvider = style.IconProvider;
        ApplyFont(style.CellFont);
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;

        // Button background: slight shift from main background toward text
        ButtonBackgroundColor = new KumikoColor(
            (byte)(bg.R + (txt.R - bg.R) * 0.08),
            (byte)(bg.G + (txt.G - bg.G) * 0.08),
            (byte)(bg.B + (txt.B - bg.B) * 0.08));

        ButtonSymbolColor = txt;

        // Button pressed: stronger shift toward text
        ButtonPressedColor = new KumikoColor(
            (byte)(bg.R + (txt.R - bg.R) * 0.2),
            (byte)(bg.G + (txt.G - bg.G) * 0.2),
            (byte)(bg.B + (txt.B - bg.B) * 0.2));

        _box.ApplyTheme(style);
        _box.BackgroundColor = bg;
        _box.EditingBackgroundColor = bg;
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>
    /// Current value. Setting it rounds the number (see the class remarks) and clamps it to
    /// <see cref="Minimum"/> and <see cref="Maximum"/>; NaN is ignored.
    /// </summary>
    public double Value
    {
        get => _value;
        set => ApplyValue(value);
    }

    /// <summary>
    /// Minimum value (default: <see cref="double.MinValue"/>). Raising it above <see cref="Maximum"/> raises
    /// the maximum to match, so the minimum never exceeds the maximum; the value is clamped to the new
    /// limit. NaN is ignored.
    /// </summary>
    public double Minimum
    {
        get => _minimum;
        set
        {
            if (double.IsNaN(value)) return;
            _minimum = value;
            if (_minimum > _maximum) _maximum = _minimum;
            ApplyValue(_value);
        }
    }

    /// <summary>
    /// Maximum value (default: <see cref="double.MaxValue"/>). Lowering it below <see cref="Minimum"/> lowers
    /// the minimum to match, so the minimum never exceeds the maximum; the value is clamped to the new
    /// limit. NaN is ignored.
    /// </summary>
    public double Maximum
    {
        get => _maximum;
        set
        {
            if (double.IsNaN(value)) return;
            _maximum = value;
            if (_maximum < _minimum) _minimum = _maximum;
            ApplyValue(_value);
        }
    }

    /// <summary>
    /// Increment/decrement step. A negative step is stored as its absolute value; zero, NaN and infinity
    /// are ignored, so the buttons always move the value.
    /// </summary>
    public double Step
    {
        get => _step;
        set
        {
            if (!double.IsFinite(value) || value == 0) return;
            _step = Math.Abs(value);
        }
    }

    /// <summary>Display format string, used when <see cref="DecimalPlaces"/> is -1. Default: "G".</summary>
    public string Format
    {
        get => _format;
        set
        {
            _format = string.IsNullOrEmpty(value) ? "G" : value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Number of decimal places shown and kept (0 to 15), or -1 (the default) for as many as the value
    /// needs, with the rounding noise of stepping removed. Values outside -1..15 are limited to it.
    /// </summary>
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            _decimalPlaces = Math.Clamp(value, -1, MaxPrecision);
            ApplyValue(_value);
        }
    }

    /// <summary>
    /// The culture the number is shown and typed in (decimal and group separators, negative sign), or null
    /// (the default) for the current culture. The grid sets it to its <see cref="KumikoSource.Culture"/>
    /// when it creates this editor from a descriptor.
    /// </summary>
    public CultureInfo? Culture
    {
        get => _culture;
        set
        {
            _culture = value;
            InvalidateVisual();
        }
    }

    private CultureInfo EffectiveCulture => _culture ?? CultureInfo.CurrentCulture;

    /// <summary>Is the control read-only?</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Delay before auto-repeat starts when a button or arrow key is held, in ms (default 400).</summary>
    public int RepeatDelayMs { get; set; } = 400;

    /// <summary>
    /// Interval between the first auto-repeat steps, in ms (default 100). It shrinks the longer the button
    /// is held, down to <see cref="MinRepeatIntervalMs"/>.
    /// </summary>
    public int RepeatIntervalMs { get; set; } = 100;

    /// <summary>The shortest interval between auto-repeat steps once the repeat has accelerated, in ms (default 25).</summary>
    public int MinRepeatIntervalMs { get; set; } = 25;

    // ── Value rules ─────────────────────────────────────────────

    /// <summary>The decimal places a value is rounded to: <see cref="DecimalPlaces"/>, else the step's precision (at least 10).</summary>
    private int RoundingDigits => _decimalPlaces >= 0 ? _decimalPlaces : Math.Max(AutoPrecision, StepDecimals());

    private int StepDecimals()
    {
        // The digits after the decimal point that make the step exact, at most MaxPrecision
        for (int digits = 0; digits < MaxPrecision; digits++)
        {
            double scaled = _step * Math.Pow(10, digits);
            if (!double.IsFinite(scaled)) break;
            if (Math.Abs(scaled - Math.Round(scaled)) < 1e-9 * Math.Max(1, Math.Abs(scaled))) return digits;
        }
        return MaxPrecision;
    }

    private double Normalize(double value)
    {
        if (double.IsNaN(value)) return _value;
        double clamped = Math.Clamp(value, _minimum, _maximum);
        double rounded = Math.Round(clamped, RoundingDigits, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, _minimum, _maximum);
    }

    private void ApplyValue(double value)
    {
        double next = Normalize(value);
        if (next.Equals(_value)) return;
        var old = _value;
        _value = next;
        RaiseValueChanged(old, _value);
        InvalidateVisual();
    }

    /// <summary>Move the value by <paramref name="steps"/> steps (negative to go down).</summary>
    private void StepBy(double steps) =>
        // A huge step can overflow to infinity; the clamp in Normalize brings it back to the limit
        ApplyValue(_value + steps * _step);

    /// <summary>Increment the value by one step.</summary>
    public void Increment() => StepBy(1);

    /// <summary>Decrement the value by one step.</summary>
    public void Decrement() => StepBy(-1);

    // ── Text ────────────────────────────────────────────────────

    /// <summary>The value as text: <see cref="DecimalPlaces"/> fixed places, else <see cref="Format"/>, in the culture.</summary>
    private string DisplayText
    {
        get
        {
            var culture = EffectiveCulture;
            if (!_textValid || _textValue != _value || _textDecimalPlaces != _decimalPlaces
                || !ReferenceEquals(_textFormat, _format) || !ReferenceEquals(_textCulture, culture))
            {
                _text = _decimalPlaces >= 0
                    ? _value.ToString(FixedFormats[_decimalPlaces], culture)
                    : _value.ToString(_format, culture);
                _textValid = true;
                _textValue = _value;
                _textDecimalPlaces = _decimalPlaces;
                _textFormat = _format;
                _textCulture = culture;
            }
            return _text;
        }
    }

    /// <summary>The text the user edits: the value without a display format, so it parses back to the same number.</summary>
    private string EditText() =>
        _decimalPlaces >= 0
            ? _value.ToString(FixedFormats[_decimalPlaces], EffectiveCulture)
            : _value.ToString("R", EffectiveCulture);

    /// <summary>
    /// Parses typed text in the culture: an optional sign, digits, one decimal separator and, optionally,
    /// group separators in groups of three. The result is clamped and rounded like any value.
    /// </summary>
    private bool TryParseText(string text, out double value)
    {
        value = 0;
        text = text.Trim();
        if (text.Length == 0) return false;

        var number = EffectiveCulture.NumberFormat;
        string group = number.NumberGroupSeparator;

        // A culture's own minus sign (U+2212 in some) and the ASCII hyphen both mean negative
        if (number.NegativeSign != "-" && text[0] == '-')
            text = number.NegativeSign + text.Substring(1);

        var styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (group.Length > 0 && text.Contains(group, StringComparison.Ordinal))
        {
            // Group separators count only as proper thousands groups: "1.234,5" in de-DE, never "1.5" read as 15
            if (!HasProperGroups(text, number)) return false;
            styles |= NumberStyles.AllowThousands;
        }

        if (!double.TryParse(text, styles, EffectiveCulture, out var parsed) || !double.IsFinite(parsed))
            return false;

        value = Normalize(parsed);
        return true;
    }

    private static bool HasProperGroups(string text, NumberFormatInfo number)
    {
        string group = number.NumberGroupSeparator;
        int decimalAt = text.IndexOf(number.NumberDecimalSeparator, StringComparison.Ordinal);
        string integerPart = decimalAt >= 0 ? text.Substring(0, decimalAt) : text;
        if (decimalAt >= 0 && text.IndexOf(group, decimalAt, StringComparison.Ordinal) >= 0) return false;

        var parts = integerPart.Split(group);
        string first = parts[0];
        if (first.StartsWith(number.NegativeSign, StringComparison.Ordinal))
            first = first.Substring(number.NegativeSign.Length);
        if (first.Length is < 1 or > 3) return false;
        for (int i = 1; i < parts.Length; i++)
            if (parts[i].Length != 3) return false;
        return true;
    }

    private void OnTextEdited()
    {
        if (!_typing) return;
        _textDirty = true;
        _invalidText = !TryParseText(_box.Text, out _);
        InvalidateVisual();
    }

    private void BeginTyping(bool selectAll)
    {
        if (_typing) return;
        _typing = true;
        _textDirty = false;
        _invalidText = false;
        _box.Bounds = GetValueRect();
        _box.Text = EditText();
        _box.IsFocused = true;
        if (selectAll) _box.SelectAll();
        InvalidateVisual();
    }

    private void EndTyping()
    {
        if (!_typing) return;
        _typing = false;
        _textDirty = false;
        _invalidText = false;
        _box.IsFocused = false;
        InvalidateVisual();
    }

    /// <summary>Applies pending typed text when it is a number, and ends the typed entry; false when the text was not a number.</summary>
    private bool CommitTyping()
    {
        if (!_typing) return true;
        bool valid = true;
        if (_textDirty)
        {
            if (TryParseText(_box.Text, out var typed)) ApplyValue(typed);
            else valid = false;
        }
        EndTyping();
        return valid;
    }

    // ── Layout helpers ──────────────────────────────────────────

    private float GetButtonWidth()
    {
        float width = Bounds.Width;
        float desired = ButtonWidth > 0 ? ButtonWidth : Math.Max(Bounds.Height, 24f);
        float limit = ButtonWidth > 0 ? width / 3f : width * 0.3f;
        return Math.Max(0f, Math.Min(desired, limit));
    }

    private KumikoRect GetMinusButtonRect()
    {
        var b = Bounds;
        return new KumikoRect(b.X, b.Y, GetButtonWidth(), b.Height);
    }

    private KumikoRect GetPlusButtonRect()
    {
        var b = Bounds;
        float bw = GetButtonWidth();
        return new KumikoRect(b.Right - bw, b.Y, bw, b.Height);
    }

    private KumikoRect GetValueRect()
    {
        var b = Bounds;
        float bw = GetButtonWidth();
        return new KumikoRect(b.X + bw, b.Y, Math.Max(0f, b.Width - bw * 2), b.Height);
    }

    /// <inheritdoc />
    protected override void OnBoundsChanged() => _box.Bounds = GetValueRect();

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        var b = Bounds;
        bool error = _typing && _invalidText;

        _fillPaint.Color = BackgroundColor;
        ctx.FillRoundRect(b, CornerRadius, _fillPaint);

        var valueRect = GetValueRect();
        if (_typing)
        {
            // Typed entry: the text box draws the text, the caret and the selection
            _box.Bounds = valueRect;
            _box.Font = Font;
            _box.TextColor = TextColor;
            _box.BorderColor = BackgroundColor;
            _box.FocusedBorderColor = BackgroundColor;
            _box.OnDraw(ctx);
        }
        else
        {
            // Value text (centered between buttons)
            _textPaint.Color = TextColor;
            _textPaint.Font = Font;
            ctx.DrawTextInRect(DisplayText, valueRect, _textPaint,
                KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
        }

        DrawButton(ctx, GetMinusButtonRect(), _holdDirection < 0 && !_holdIsKey, plus: false);
        DrawButton(ctx, GetPlusButtonRect(), _holdDirection > 0 && !_holdIsKey, plus: true);

        // Outer border
        _strokePaint.Color = error ? ErrorBorderColor : IsFocused || _typing ? FocusedBorderColor : BorderColor;
        _strokePaint.StrokeWidth = error ? 2 : 1;
        ctx.DrawRoundRect(b, CornerRadius, _strokePaint);
    }

    private void DrawButton(IDrawingContext ctx, KumikoRect r, bool pressed, bool plus)
    {
        _fillPaint.Color = pressed ? ButtonPressedColor : ButtonBackgroundColor;
        ctx.FillRect(r, _fillPaint);
        _strokePaint.Color = BorderColor;
        _strokePaint.StrokeWidth = 0.5f;
        ctx.DrawRect(r, _strokePaint);

        // The glyph's square: half the button's shorter side, centered
        _symbolPaint.Color = ButtonSymbolColor;
        float size = Math.Min(r.Width, r.Height) / 2;
        IconProvider.DrawIcon(ctx, plus ? KumikoIcon.Increment : KumikoIcon.Decrement,
            new KumikoRect(r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, size), _symbolPaint);
    }

    // ── Pointer input ───────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled || IsReadOnly) return false;

        bool minus = GetMinusButtonRect().Contains(e.X, e.Y);
        bool plus = !minus && GetPlusButtonRect().Contains(e.X, e.Y);
        if (minus || plus)
        {
            CommitTyping();
            int direction = minus ? -1 : 1;
            StepBy(direction);
            StartHold(direction, KumikoKey.None);
            return true;
        }

        // A tap on the number starts typed entry, with the caret where it was tapped
        if (GetValueRect().Contains(e.X, e.Y))
        {
            BeginTyping(selectAll: false);
            _box.OnPointerDown(e);
        }
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(KumikoPointerEventArgs e) => _typing && _box.OnPointerMove(e);

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (_typing) _box.OnPointerUp(e);
        if (!_holdIsKey) StopHold();
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override void OnPointerCancel(KumikoPointerEventArgs e)
    {
        if (_typing) _box.OnPointerUp(e);
        if (!_holdIsKey) StopHold();
    }

    // ── Auto-repeat ─────────────────────────────────────────────

    private void StartHold(int direction, KumikoKey key)
    {
        _holdDirection = direction;
        _holdKey = key;
        _holdIsKey = key != KumikoKey.None;
        _keyRepeatSeen = false;
        _keyIdleMs = 0;
        _holdMs = 0;
        _nextTickMs = RepeatDelayMs;
        _ticks = 0;
        InvalidateVisual();
    }

    private void StopHold()
    {
        if (_holdDirection == 0) return;
        _holdDirection = 0;
        _holdKey = KumikoKey.None;
        _holdIsKey = false;
        InvalidateVisual();
    }

    /// <summary>Whether a spin button or an arrow key is held, so the host must call <see cref="UpdateAnimation"/> every frame.</summary>
    public override bool IsAnimating => _holdDirection != 0;

    /// <summary>
    /// Advances the auto-repeat by one frame: after <see cref="RepeatDelayMs"/> of holding a button or arrow
    /// key it steps the value every <see cref="RepeatIntervalMs"/>, the interval shrinking to
    /// <see cref="MinRepeatIntervalMs"/> the longer it is held and the step growing tenfold every 20 steps
    /// (up to a thousandfold) after that.
    /// </summary>
    /// <param name="frameIntervalMs">Time since the previous frame, in milliseconds.</param>
    /// <returns>True while a button or key is still held.</returns>
    public override bool UpdateAnimation(float frameIntervalMs)
    {
        if (_holdDirection == 0) return false;

        if (_holdIsKey)
        {
            // The operating system's key repeat tells that the key is still down: until the first repeat
            // arrives nothing steps, and once repeats stop the key was released
            _keyIdleMs += frameIntervalMs;
            if (_keyIdleMs > (_keyRepeatSeen ? KeyReleaseTimeoutMs : KeyInitialRepeatTimeoutMs))
            {
                StopHold();
                return false;
            }
            if (!_keyRepeatSeen) return true;
        }

        _holdMs += frameIntervalMs;
        int fired = 0;
        while (_holdMs >= _nextTickMs && fired < MaxTicksPerFrame)
        {
            StepBy(_holdDirection * StepMultiplier(_ticks));
            _ticks++;
            fired++;
            _nextTickMs += RepeatInterval(_ticks);
        }
        if (fired == MaxTicksPerFrame)
            _nextTickMs = _holdMs + RepeatInterval(_ticks); // a stalled frame doesn't queue a burst

        return true;
    }

    private float RepeatInterval(int ticks)
    {
        double interval = Math.Max(1, RepeatIntervalMs) * Math.Pow(0.92, ticks);
        return (float)Math.Max(Math.Min(MinRepeatIntervalMs, RepeatIntervalMs), interval);
    }

    private static double StepMultiplier(int ticks)
    {
        // Whole stages only: the multiplier steps up every TicksPerAccelerationStage ticks, so the division truncates on purpose
        int stage = ticks / TicksPerAccelerationStage;
        return Math.Min(MaxStepMultiplier, Math.Pow(10, stage));
    }

    // ── Keyboard input ──────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        if (IsReadOnly) return false;

        switch (e.Key)
        {
            case KumikoKey.Up:
            case KumikoKey.Down:
                return HandleArrow(e.Key);

            case KumikoKey.PageUp:
                CommitTyping();
                StepBy(10);
                return true;

            case KumikoKey.PageDown:
                CommitTyping();
                StepBy(-10);
                return true;

            case KumikoKey.Home when !_textDirty && !e.HasCommand:
                EndTyping();
                if (_minimum > double.MinValue) Value = _minimum;
                return true;

            case KumikoKey.End when !_textDirty && !e.HasCommand:
                EndTyping();
                if (_maximum < double.MaxValue) Value = _maximum;
                return true;

            case KumikoKey.Enter:
                // Apply what was typed; the key is left to the host, which commits the edit
                if (_typing && _textDirty && TryParseText(_box.Text, out var typed))
                {
                    ApplyValue(typed);
                    EndTyping();
                }
                return false;

            case KumikoKey.Escape:
                if (_typing && _textDirty)
                {
                    EndTyping();
                    return true;
                }
                return false;

            case KumikoKey.Tab:
                return false;
        }

        // Typed entry: digits, separators and the sign, editing keys and shortcuts go to the text box
        bool navigation = e.Key is KumikoKey.Left or KumikoKey.Right or KumikoKey.Home or KumikoKey.End;
        if (e.HasCommand || navigation || e.Key is KumikoKey.Backspace or KumikoKey.Delete || e.EnteredText != null)
        {
            BeginTyping(selectAll: !navigation || e.HasCommand);
            return _box.OnKeyDown(e);
        }

        return false;
    }

    private bool HandleArrow(KumikoKey key)
    {
        if (_holdIsKey && _holdKey == key)
        {
            // The operating system repeating the held key: the frame loop does the stepping
            _keyIdleMs = 0;
            if (!_keyRepeatSeen)
            {
                _keyRepeatSeen = true;
                _holdMs = 0;
                _nextTickMs = 0;
            }
            return true;
        }

        int direction = key == KumikoKey.Up ? 1 : -1;
        CommitTyping();
        StepBy(direction);
        StartHold(direction, key);
        return true;
    }

    /// <inheritdoc />
    public override bool OnKeyUp(KumikoKeyEventArgs e)
    {
        if (_holdIsKey && _holdKey == e.Key)
        {
            StopHold();
            return true;
        }
        return false;
    }

    // ── Focus ───────────────────────────────────────────────────

    /// <summary>Applies pending typed text when it is a number (else drops it) and stops any auto-repeat.</summary>
    protected override void OnLostFocus()
    {
        StopHold();
        CommitTyping();
        base.OnLostFocus();
    }

    // ── IValueEditor ────────────────────────────────────────────

    /// <summary>What an edit session is given for typed text that is not a number: nothing converts from it.</summary>
    private sealed class RejectedNumber(string text)
    {
        public override string ToString() => text;
    }

    /// <summary>
    /// The value to commit: the number, or, while typed text is pending, that text applied (parsed, clamped
    /// and rounded). Typed text that is not a number is returned as an object nothing converts from, so the
    /// edit session refuses it with its validation message (the text itself would be parsed again, more
    /// leniently, by the session's converter: "1.5" is 15 in German).
    /// </summary>
    object? IValueEditor.Value =>
        _typing && _textDirty
            ? TryParseText(_box.Text, out var typed) ? typed : new RejectedNumber(_box.Text)
            : _value;

    bool IValueEditor.HasValue => true;

    bool IValueEditor.IsDirty => HasChangedSinceTracking || _typing && _textDirty;
}
