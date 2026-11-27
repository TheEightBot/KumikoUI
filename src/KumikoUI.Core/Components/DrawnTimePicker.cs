using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn time-of-day editor: an hour, a minute and, in 12-hour time, an AM/PM segment with
/// up and down buttons, the editor of a <see cref="KumikoColumnType.Time"/> column.
/// </summary>
/// <remarks>
/// <para>
/// One segment is active at a time. A tap on a segment activates it (a tap on AM/PM flips it), and the up and
/// down buttons step the active segment. From the keyboard, Up and Down step it, Left and Right move between
/// the segments, digits type it (two digits, or one when no second digit could follow, then on to the next
/// segment), ":" moves to the minutes and, in 12-hour time, A and P set AM and PM. Enter, Escape and Tab are
/// left to the grid, which commits or cancels the edit.
/// </para>
/// <para>
/// The picker edits hours and minutes only. Seconds and fractions of the value it was given
/// (<see cref="TimeOfDay"/>) are kept until the hour or minute is changed. <see cref="Use24HourFormat"/>,
/// <see cref="MinuteStep"/> and the AM/PM designators of <see cref="Culture"/> shape the display and the stepping.
/// An empty picker (<see cref="HasTime"/> false) shows dashes and holds no value until it is set or stepped.
/// </para>
/// </remarks>
public sealed class DrawnTimePicker : DrawnComponent, IValueEditor
{
    private const int HourSegment = 0;
    private const int MinuteSegment = 1;
    private const int PeriodSegment = 2;

    private static readonly string[] TwoDigits = Enumerable.Range(0, 60).Select(i => i.ToString("D2", CultureInfo.InvariantCulture)).ToArray();

    private int _hour;
    private int _minute;
    private long _subMinuteTicks;
    private bool _hasTime;
    private int _segment;
    private int _pendingDigits; // 0: none; 1: the first digit of the active segment was typed
    private int _pendingFirst;  // that digit
    private int _use24Hour = -1; // -1 never set (follows the culture), 0 false, 1 true
    private int _minuteStep = 1;
    private CultureInfo? _culture;

    private readonly KumikoPaint _fillPaint = new();
    private readonly KumikoPaint _strokePaint = new();
    private readonly KumikoPaint _textPaint = new() { IsAntiAlias = true };
    private readonly KumikoPaint _iconPaint = new() { StrokeWidth = 1.2f, IsAntiAlias = true };

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font of the time text.</summary>
    public KumikoFont Font { get; set; } = new("Default", 14);
    /// <summary>Color of the time text.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the field.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border color when the picker is not focused.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color when the picker is focused.</summary>
    public KumikoColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Color of the highlight behind the active segment.</summary>
    public KumikoColor SegmentColor { get; set; } = new(0, 120, 215, 60);
    /// <summary>Background color of the up and down buttons.</summary>
    public KumikoColor ButtonBackgroundColor { get; set; } = new(240, 240, 240);
    /// <summary>Color of the arrows on the up and down buttons.</summary>
    public KumikoColor ArrowColor { get; set; } = new(60, 60, 60);
    /// <summary>Corner radius of the field.</summary>
    public float CornerRadius { get; set; } = 4f;

    /// <summary>
    /// Sets the font of the time, so the editor draws and measures its text like the grid's cells.
    /// <see cref="ApplyTheme"/> calls it with the grid's cell font; the grid calls it again with the column's
    /// own font when the column has one.
    /// </summary>
    /// <param name="font">The font to use: family, size, bold and italic.</param>
    public void ApplyFont(KumikoFont font)
    {
        Font = font;
        InvalidateVisual();
    }

    /// <summary>Applies theme colors from a <see cref="KumikoStyle"/> so the picker matches the current grid theme.</summary>
    /// <param name="style">The grid's style.</param>
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
        SegmentColor = new KumikoColor(style.AccentColor.R, style.AccentColor.G, style.AccentColor.B, 70);
        ButtonBackgroundColor = new KumikoColor(
            (byte)(bg.R + (txt.R - bg.R) * 0.08),
            (byte)(bg.G + (txt.G - bg.G) * 0.08),
            (byte)(bg.B + (txt.B - bg.B) * 0.08));
        ArrowColor = txt;
    }

    // ── Value ───────────────────────────────────────────────────

    /// <summary>
    /// The culture the AM/PM designators and the time separator come from, and that decides 12-hour or 24-hour time
    /// when <see cref="Use24HourFormat"/> is not set; null (the default) for the current culture. The grid sets it
    /// to its <see cref="KumikoSource.Culture"/>.
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

    /// <summary>
    /// Whether hours run from 0 to 23 (true) or from 1 to 12 with an AM/PM segment (false). Until it is set, it
    /// follows <see cref="Culture"/>: 24-hour unless the culture's short time pattern has an AM/PM designator.
    /// </summary>
    public bool Use24HourFormat
    {
        get => _use24Hour >= 0 ? _use24Hour == 1 : !EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains('t');
        set
        {
            _use24Hour = value ? 1 : 0;
            if (value && _segment == PeriodSegment) _segment = MinuteSegment;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The minutes the up and down arrows step by, from 1 to 30 (default 1). Stepping goes to the next (or
    /// previous) multiple of the step. Typing and <see cref="Minute"/> are not limited by it.
    /// </summary>
    public int MinuteStep
    {
        get => _minuteStep;
        set => _minuteStep = Math.Clamp(value, 1, 30);
    }

    /// <summary>The hour, 0 to 23. Setting it (limited to that range) gives the picker a time.</summary>
    public int Hour
    {
        get => _hour;
        set => SetTime(Math.Clamp(value, 0, 23), _minute, _subMinuteTicks);
    }

    /// <summary>The minute, 0 to 59. Setting it (limited to that range) gives the picker a time.</summary>
    public int Minute
    {
        get => _minute;
        set => SetTime(_hour, Math.Clamp(value, 0, 59), _subMinuteTicks);
    }

    /// <summary>Whether the picker holds a time; false for a picker opened on an empty cell, until it is set or stepped.</summary>
    public bool HasTime => _hasTime;

    /// <summary>
    /// The time of day, including the seconds and fractions the picker was given and has not changed (zero when it
    /// holds none). Setting it keeps the seconds and fractions, which a change of hour or minute through the picker
    /// then drops; a value outside a day is limited to it.
    /// </summary>
    public TimeSpan TimeOfDay
    {
        get => new(_hour * TimeSpan.TicksPerHour + _minute * TimeSpan.TicksPerMinute + _subMinuteTicks);
        set
        {
            long ticks = Math.Clamp(value.Ticks, 0, TimeSpan.TicksPerDay - 1);
            SetTime((int)(ticks / TimeSpan.TicksPerHour), (int)(ticks / TimeSpan.TicksPerMinute % 60), ticks % TimeSpan.TicksPerMinute);
        }
    }

    /// <summary>Removes the time: the picker shows dashes and holds no value.</summary>
    public void Clear()
    {
        if (!_hasTime) return;
        var old = TimeOfDay;
        _hasTime = false;
        _hour = _minute = 0;
        _subMinuteTicks = 0;
        _pendingDigits = 0;
        RaiseValueChanged(old, null);
        InvalidateVisual();
    }

    /// <summary>
    /// A date the time is added to, for a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> property edited in a
    /// time column: the value is then that date at the time, instead of the time alone.
    /// </summary>
    internal DateTime? BaseDate { get; set; }

    /// <summary>Raised when the hour or minute changes.</summary>
    public event EventHandler<KumikoTimeChangedEventArgs>? TimeChanged;

    private void SetTime(int hour, int minute, long subMinuteTicks)
    {
        if (_hasTime && hour == _hour && minute == _minute && subMinuteTicks == _subMinuteTicks) return;
        var old = TimeOfDay;
        bool hadTime = _hasTime;
        int oldHour = _hour, oldMinute = _minute;
        _hour = hour;
        _minute = minute;
        _subMinuteTicks = subMinuteTicks;
        _hasTime = true;
        if (!hadTime || oldHour != hour || oldMinute != minute)
            TimeChanged?.Invoke(this, new KumikoTimeChangedEventArgs(oldHour, oldMinute, hour, minute));
        RaiseValueChanged(hadTime ? old : null, TimeOfDay);
        InvalidateVisual();
    }

    /// <summary>A change the user made: the seconds and fractions are dropped.</summary>
    private void UserSetTime(int hour, int minute) => SetTime(hour, minute, 0);

    // ── Stepping ────────────────────────────────────────────────

    /// <summary>Steps the active segment one step up.</summary>
    public void StepUp() => StepSegment(+1);

    /// <summary>Steps the active segment one step down.</summary>
    public void StepDown() => StepSegment(-1);

    private void StepSegment(int direction)
    {
        _pendingDigits = 0;
        switch (_segment)
        {
            case HourSegment:
                UserSetTime(((_hasTime ? _hour : 0) + direction + 24) % 24, _minute);
                break;
            case MinuteSegment:
                UserSetTime(_hour, StepMinute(_hasTime ? _minute : 0, direction));
                break;
            default:
                UserSetTime((_hour + 12) % 24, _minute);
                break;
        }
    }

    /// <summary>The next multiple of <see cref="MinuteStep"/> above or below a minute, wrapping within the hour.</summary>
    private int StepMinute(int minute, int direction)
    {
        int step = _minuteStep;
        if (direction > 0)
        {
            int next = (minute / step + 1) * step;
            return next >= 60 ? 0 : next;
        }
        int previous = minute % step != 0 ? minute / step * step : minute - step;
        return previous < 0 ? (59 / step) * step : previous;
    }

    // ── Layout ──────────────────────────────────────────────────

    private float ButtonsWidth => Math.Clamp(Bounds.Height, 20f, 28f);

    private KumikoRect FieldRect
    {
        get
        {
            var b = Bounds;
            return new KumikoRect(b.X, b.Y, Math.Max(0, b.Width - ButtonsWidth), b.Height);
        }
    }

    private int SegmentCount => Use24HourFormat ? 2 : 3;

    /// <summary>The horizontal span of a segment, as fractions of the field's width.</summary>
    private static (float Start, float End) Zone(int segment, bool is24)
    {
        if (is24) return segment == HourSegment ? (0f, 0.5f) : (0.5f, 1f);
        return segment switch
        {
            HourSegment => (0f, 0.36f),
            MinuteSegment => (0.36f, 0.68f),
            _ => (0.68f, 1f),
        };
    }

    private KumikoRect SegmentRect(int segment)
    {
        var field = FieldRect;
        var (start, end) = Zone(segment, Use24HourFormat);
        return new KumikoRect(field.X + field.Width * start, field.Y, field.Width * (end - start), field.Height);
    }

    private KumikoRect UpButtonRect()
    {
        var b = Bounds;
        return new KumikoRect(b.Right - ButtonsWidth, b.Y, ButtonsWidth, b.Height / 2);
    }

    private KumikoRect DownButtonRect()
    {
        var b = Bounds;
        return new KumikoRect(b.Right - ButtonsWidth, b.Y + b.Height / 2, ButtonsWidth, b.Height - b.Height / 2);
    }

    // ── Drawing ─────────────────────────────────────────────────

    /// <summary>The text of a segment: dashes when the picker holds no time.</summary>
    private string SegmentText(int segment)
    {
        if (!_hasTime) return "--";
        var info = EffectiveCulture.DateTimeFormat;
        switch (segment)
        {
            case HourSegment:
                if (Use24HourFormat) return TwoDigits[_hour];
                int h = _hour % 12;
                return TwoDigits[h == 0 ? 12 : h];
            case MinuteSegment:
                return TwoDigits[_minute];
            default:
                return _hour >= 12 ? info.PMDesignator : info.AMDesignator;
        }
    }

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        if (!IsVisible) return;
        var b = Bounds;
        var field = FieldRect;

        _fillPaint.Color = BackgroundColor;
        ctx.FillRoundRect(b, CornerRadius, _fillPaint);

        _textPaint.Font = Font;
        _textPaint.Color = TextColor;
        string separator = EffectiveCulture.DateTimeFormat.TimeSeparator;
        int count = SegmentCount;
        for (int i = 0; i < count; i++)
        {
            var rect = SegmentRect(i);
            if (i == _segment)
            {
                _fillPaint.Color = SegmentColor;
                ctx.FillRoundRect(rect.Inflate(-2, -3), 3, _fillPaint);
            }
            ctx.DrawTextInRect(SegmentText(i), rect, _textPaint, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
            if (i == HourSegment)
            {
                // The separator sits on the edge between the hour and the minute
                float edge = rect.Right;
                ctx.DrawTextInRect(separator, new KumikoRect(edge - 6, field.Y, 12, field.Height), _textPaint,
                    KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
            }
        }

        DrawButton(ctx, UpButtonRect(), KumikoIcon.SpinUp);
        DrawButton(ctx, DownButtonRect(), KumikoIcon.SpinDown);

        _strokePaint.Color = IsFocused ? FocusedBorderColor : BorderColor;
        _strokePaint.StrokeWidth = 1;
        ctx.DrawRoundRect(b, CornerRadius, _strokePaint);
    }

    private void DrawButton(IDrawingContext ctx, KumikoRect r, KumikoIcon icon)
    {
        _fillPaint.Color = ButtonBackgroundColor;
        ctx.FillRect(r, _fillPaint);
        _strokePaint.Color = BorderColor;
        _strokePaint.StrokeWidth = 0.5f;
        ctx.DrawRect(r, _strokePaint);

        _iconPaint.Color = ArrowColor;
        float size = Math.Min(10f, Math.Min(r.Width, r.Height) - 4);
        IconProvider.DrawIcon(ctx, icon, new KumikoRect(r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, size), _iconPaint);
    }

    // ── Pointer input ───────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        if (UpButtonRect().Contains(e.X, e.Y)) { StepSegment(+1); return true; }
        if (DownButtonRect().Contains(e.X, e.Y)) { StepSegment(-1); return true; }

        for (int i = 0; i < SegmentCount; i++)
        {
            if (!SegmentRect(i).Contains(e.X, e.Y)) continue;
            Activate(i);
            if (i == PeriodSegment) StepSegment(+1); // a tap on AM/PM flips it
            return true;
        }
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e) => true;

    private void Activate(int segment)
    {
        if (segment == _segment) return;
        _segment = segment;
        _pendingDigits = 0;
        InvalidateVisual();
    }

    // ── Keyboard input ──────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        if (!IsEnabled) return false;
        switch (e.Key)
        {
            case KumikoKey.Up:
                StepSegment(+1);
                return true;
            case KumikoKey.Down:
                StepSegment(-1);
                return true;
            case KumikoKey.Left:
                Activate(Math.Max(0, _segment - 1));
                return true;
            case KumikoKey.Right:
                Activate(Math.Min(SegmentCount - 1, _segment + 1));
                return true;
            case KumikoKey.Character:
                if (e.HasCommand) return false;
                if (e.EnteredText is { } text)
                    foreach (char c in text) TypeCharacter(c);
                return true;
            default:
                return false;
        }
    }

    private void TypeCharacter(char c)
    {
        if (c is >= '0' and <= '9')
        {
            TypeDigit(c - '0');
            return;
        }

        var info = EffectiveCulture.DateTimeFormat;
        if (c == ':' || info.TimeSeparator.Length == 1 && c == info.TimeSeparator[0])
        {
            if (_segment == HourSegment) Activate(MinuteSegment);
            return;
        }

        if (!Use24HourFormat && FirstLetter(info.AMDesignator, 'A') is var am && FirstLetter(info.PMDesignator, 'P') is var pm)
        {
            char upper = char.ToUpperInvariant(c);
            if (upper == am || upper == pm)
            {
                bool wantPm = upper == pm && upper != am;
                int hour = _hasTime ? _hour : 0;
                if (!_hasTime) _minute = 0;
                int adjusted = wantPm ? hour % 12 + 12 : hour % 12;
                UserSetTime(adjusted, _minute);
                _pendingDigits = 0;
            }
        }
    }

    private static char FirstLetter(string designator, char fallback) =>
        designator.Length > 0 ? char.ToUpperInvariant(designator[0]) : fallback;

    private void TypeDigit(int digit)
    {
        if (_segment == PeriodSegment) return;
        bool is24 = Use24HourFormat;

        if (_segment == HourSegment)
        {
            int limit = is24 ? 23 : 12;
            int value;
            bool complete;
            if (_pendingDigits == 1 && _pendingFirst * 10 + digit is var two && two >= (is24 ? 0 : 1) && two <= limit)
            {
                value = two;
                complete = true;
            }
            else
            {
                value = digit;
                // A first digit that no second digit can follow ends the segment
                complete = is24 ? digit > 2 : digit > 1;
            }

            SetTypedHour(value, is24);
            if (complete)
            {
                _pendingDigits = 0;
                Activate(MinuteSegment);
            }
            else
            {
                _pendingDigits = 1;
                _pendingFirst = digit;
            }
            return;
        }

        // Minutes
        int minuteValue;
        bool minuteComplete;
        if (_pendingDigits == 1 && _pendingFirst * 10 + digit is var m && m <= 59)
        {
            minuteValue = m;
            minuteComplete = true;
        }
        else
        {
            minuteValue = digit;
            minuteComplete = digit > 5;
        }
        UserSetTime(_hasTime ? _hour : 0, minuteValue);
        if (minuteComplete)
        {
            _pendingDigits = 0;
        }
        else
        {
            _pendingDigits = 1;
            _pendingFirst = digit;
        }
    }

    /// <summary>Sets the hour a user typed: in 12-hour time it keeps the AM or PM the picker is in.</summary>
    private void SetTypedHour(int typed, bool is24)
    {
        int hour = typed;
        if (!is24)
        {
            bool pm = _hasTime && _hour >= 12;
            hour = typed % 12 + (pm ? 12 : 0);
        }
        UserSetTime(hour, _hasTime ? _minute : 0);
    }

    // ── IValueEditor ────────────────────────────────────────────

    /// <summary>
    /// The value to commit: the time of day as a <see cref="TimeSpan"/> (which the edit session converts to a
    /// <see cref="TimeOnly"/> property), or, for a date and time edited here, that date at the time; null when the
    /// picker holds no time.
    /// </summary>
    object? IValueEditor.Value =>
        !_hasTime ? null
        : BaseDate is { } date ? date.Date + TimeOfDay
        : TimeOfDay;

    bool IValueEditor.HasValue => _hasTime;

    bool IValueEditor.IsDirty => HasChangedSinceTracking;
}
