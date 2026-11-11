using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn date picker with a calendar popup.
/// Renders month grid, navigation arrows, and day selection.
/// </summary>
/// <remarks>
/// <para>
/// The picker is culture-aware (<see cref="Culture"/>): month and day names, the first day of the week
/// (<see cref="FirstDayOfWeek"/> overrides it), the order of month and year in the header, the AM/PM
/// designators and the date text, which follows <see cref="DateFormat"/> or else the culture's short date
/// pattern, so a German picker reads 15.03.2024 and starts its weeks on Monday.
/// </para>
/// <para>
/// A date can be typed into the field: typing (digits, separators, month names) starts typed entry in a
/// <see cref="DrawnTextBox"/>, and Enter, a tap elsewhere or losing focus applies the text parsed in the
/// culture. Text that is not a date, or a date outside <see cref="MinDate"/> and <see cref="MaxDate"/>, is
/// not applied: the field is drawn with an error border and <see cref="IValueEditor.Value"/> reports
/// something the edit session refuses through its ordinary validation path. Days outside the range are drawn
/// disabled and cannot be selected by tap, keyboard or typing.
/// </para>
/// <para>
/// In the open calendar the arrow keys move the selection by a day or a week, Page Up and Page Down by a
/// month (with Shift, a year), and Home and End to the first and last day of the week. While typed text is
/// pending, Left, Right, Home and End edit the text instead.
/// </para>
/// </remarks>
public class DrawnDatePicker : DrawnComponent, IValueEditor
{
    private DateTime _selectedDate = DateTime.Today; // the selection, or where the keyboard starts when there is none
    private bool _hasSelection = true;
    private DateTime? _rangeEndDate;
    private DateTime _displayMonth;
    private bool _isCalendarOpen;
    private bool _isMonthDropdownOpen;
    private bool _isYearDropdownOpen;
    private DateTime? _pressedDate;
    private int _highlightedMonthIndex = -1;
    private int _highlightedYearIndex = -1;
    private float _yearScrollOffset;
    private DateTime? _minDate;
    private DateTime? _maxDate;
    private string? _dateFormat;
    private DayOfWeek? _firstDayOfWeek;

    // Year list drag: a press waits for its release so a drag can scroll the list instead of picking a year
    private bool _yearPressed;
    private bool _yearDragged;
    private float _yearPressY;
    private float _yearPressOffset;
    private int _yearPressedYear;

    // Typed entry
    private readonly DrawnTextBox _box;
    private bool _typing;
    private bool _textDirty;
    private bool _invalidText;

    // Calendar layout constants
    private const int DaysPerWeek = 7;
    private const int MaxWeeks = 6;
    private const float DayCellSize = 32f;
    private const float HeaderHeight = 36f;
    private const float NavRowHeight = 32f;
    private const float DropdownItemHeight = 26f;
    private const float MonthDropdownWidth = 110f;
    private const float YearDropdownWidth = 70f;
    private const float YearDropdownMaxHeight = 200f;
    private const float YearDragThreshold = 4f;

    // The year list covers these years unless MinDate/MaxDate narrow or the displayed year lies outside
    private const int DefaultFirstYear = 1900;
    private const int DefaultLastYear = 2100;

    private static readonly string[] DayNumbers =
        Enumerable.Range(0, 32).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();

    /// <summary>What the field text shows; the text is rebuilt only when one of these changes.</summary>
    private readonly record struct FieldKey(
        bool HasSelection, DateTime Date, string? Format, CultureInfo? Culture, bool ShowTime, bool H24,
        int Hour, int Minute, bool Range, DateTime? RangeEnd);

    private FieldKey _fieldKey;
    private bool _fieldKeyValid;
    private string _fieldText = string.Empty;

    private int _yearTextYear = -1;
    private string _yearText = string.Empty;
    private readonly Dictionary<int, string> _yearStrings = new();

    // Paints reused by every draw
    private readonly KumikoPaint _fillPaint = new() { Style = PaintStyle.Fill };
    private readonly KumikoPaint _strokePaint = new() { Style = PaintStyle.Stroke };
    private readonly KumikoPaint _arrowPaint = new() { Style = PaintStyle.Stroke, IsAntiAlias = true };
    private readonly KumikoPaint _textPaint = new() { Style = PaintStyle.Fill };

    /// <summary>Creates a date picker showing the current month with no date selected.</summary>
    public DrawnDatePicker()
    {
        _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _box = new DrawnTextBox
        {
            BorderWidth = 0,
            MaxLength = 48,
            BackgroundColor = BackgroundColor,
            EditingBackgroundColor = BackgroundColor,
        };
        _box.TextChanged += (_, _) => OnTextEdited();
        _box.RedrawRequested += InvalidateVisual;
    }

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used for the display field and calendar header.</summary>
    public KumikoFont Font { get; set; } = new("Default", 13);
    /// <summary>Font used for individual day numbers in the calendar grid.</summary>
    public KumikoFont DayFont { get; set; } = new("Default", 12);
    /// <summary>Primary text color for labels and day numbers.</summary>
    public KumikoColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the display field.</summary>
    public KumikoColor BackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Border color of the display field when not focused.</summary>
    public KumikoColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color applied when the date picker has input focus.</summary>
    public KumikoColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Border color of the display field while the typed text is not an acceptable date.</summary>
    public KumikoColor ErrorBorderColor { get; set; } = new(220, 50, 50);
    /// <summary>Background fill color of the calendar popup.</summary>
    public KumikoColor CalendarBackgroundColor { get; set; } = KumikoColor.White;
    /// <summary>Highlight color drawn behind today’s date in the calendar.</summary>
    public KumikoColor TodayHighlightColor { get; set; } = new(0, 120, 215, 40);
    /// <summary>Background color of the selected day cell.</summary>
    public KumikoColor SelectedDayColor { get; set; } = new(0, 120, 215);
    /// <summary>Text color of the selected day number.</summary>
    public KumikoColor SelectedDayTextColor { get; set; } = KumikoColor.White;
    /// <summary>Text color for weekend day numbers (Saturday and Sunday).</summary>
    public KumikoColor WeekendColor { get; set; } = new(200, 60, 60);
    /// <summary>Text color for days that belong to the previous or next month.</summary>
    public KumikoColor OtherMonthColor { get; set; } = new(180, 180, 180);
    /// <summary>Text color of days outside <see cref="MinDate"/> and <see cref="MaxDate"/>, which cannot be selected.</summary>
    public KumikoColor DisabledDayColor { get; set; } = new(200, 200, 200);
    /// <summary>Color of the month navigation arrows.</summary>
    public KumikoColor ArrowColor { get; set; } = new(80, 80, 80);
    /// <summary>Horizontal padding inside the display field.</summary>
    public float Padding { get; set; } = 6f;

    /// <summary>
    /// Date format string used to display and parse the selected date, or null (the default) for the
    /// culture's short date pattern, which puts day, month and year in the order of <see cref="Culture"/>.
    /// The grid sets it to the column's <see cref="KumikoColumn.Format"/>.
    /// </summary>
    public string? DateFormat
    {
        get => _dateFormat;
        set
        {
            _dateFormat = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The culture for month and day names, the first day of the week, the AM/PM designators and the
    /// displayed and typed date, or null (the default) for the current culture. The grid sets it to its
    /// <see cref="KumikoSource.Culture"/> when it creates a date editor.
    /// </summary>
    public CultureInfo? Culture { get; set; }

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>
    /// The first column of the calendar's week, or null (the default) for the culture's first day of the
    /// week: Monday for de-DE, Sunday for en-US.
    /// </summary>
    public DayOfWeek? FirstDayOfWeek
    {
        get => _firstDayOfWeek;
        set
        {
            _firstDayOfWeek = value;
            InvalidateVisual();
        }
    }

    private DayOfWeek EffectiveFirstDay => _firstDayOfWeek ?? EffectiveCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>The designator after a 12-hour time (with its leading space), or "" when the culture has none.</summary>
    private string AmPmSuffix(int hour)
    {
        var info = EffectiveCulture.DateTimeFormat;
        string designator = hour >= 12 ? info.PMDesignator : info.AMDesignator;
        return designator.Length == 0 ? "" : " " + designator;
    }

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Sets the font of the date text and calendar header; the day numbers are one pixel smaller, so the editor draws and measures its text like the grid's cells.
    /// <see cref="ApplyTheme"/> calls it with the grid's cell font; the grid calls it again with the column's
    /// own font when the column has one.
    /// </summary>
    /// <param name="font">The font to use: family, size, bold and italic.</param>
    public void ApplyFont(KumikoFont font)
    {
        Font = font;
        DayFont = new KumikoFont(font.Family, Math.Max(1f, font.Size - 1f), font.IsBold, font.IsItalic);
        _box.Font = font;
        InvalidateVisual();
    }

    /// <summary>
    /// Applies theme colors from a <see cref="KumikoStyle"/> so the date picker
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(KumikoStyle style)
    {
        ApplyFont(style.CellFont);
        var bg = style.BackgroundColor;
        var txt = style.CellTextColor;

        TextColor = txt;
        BackgroundColor = bg;
        BorderColor = style.GridLineColor;
        FocusedBorderColor = style.AccentColor;
        CalendarBackgroundColor = bg;
        TodayHighlightColor = style.AccentColor.WithAlpha(40);
        SelectedDayColor = style.AccentColor;

        // Selected day text: contrast against accent
        double accentLum = (0.299 * style.AccentColor.R + 0.587 * style.AccentColor.G + 0.114 * style.AccentColor.B) / 255.0;
        SelectedDayTextColor = accentLum > 0.5 ? new KumikoColor(0, 0, 0) : new KumikoColor(255, 255, 255);

        // Weekend color: keep distinct but adapt to theme brightness
        double bgLum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        WeekendColor = bgLum > 0.5 ? new KumikoColor(200, 60, 60) : new KumikoColor(255, 120, 120);

        // Other month days: midpoint between text and background
        OtherMonthColor = new KumikoColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));

        // Disabled days: fainter than the other month's
        DisabledDayColor = new KumikoColor(
            (byte)((txt.R + bg.R * 3) / 4),
            (byte)((txt.G + bg.G * 3) / 4),
            (byte)((txt.B + bg.B * 3) / 4));

        // Arrow: same as other month (secondary tone)
        ArrowColor = OtherMonthColor;

        _box.ApplyTheme(style);
        _box.BackgroundColor = bg;
        _box.EditingBackgroundColor = bg;
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the selected date (the date part only), or null for no selection: the field is then
    /// blank and the calendar highlights no day. Today by default.
    /// </summary>
    public DateTime? SelectedDate
    {
        get => _hasSelection ? _selectedDate : null;
        set
        {
            if (value is not { } date)
            {
                if (!_hasSelection) return;
                var cleared = _selectedDate;
                _hasSelection = false;
                RaiseValueChanged(cleared, null);
                InvalidateVisual();
                return;
            }

            if (_hasSelection && _selectedDate.Date == date.Date) return;
            DateTime? old = _hasSelection ? _selectedDate : null;
            _hasSelection = true;
            _selectedDate = date.Date;
            _displayMonth = new DateTime(date.Year, date.Month, 1);
            RaiseValueChanged(old, _selectedDate);
            DateSelected?.Invoke(this, new DateSelectedEventArgs(old ?? _selectedDate, _selectedDate));
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The time of day kept with the selected date when <see cref="ShowTimePicker"/> is off: an edited
    /// cell's original time, so picking a new date doesn't reset it to midnight.
    /// </summary>
    public TimeSpan TimeOfDay { get; set; }

    /// <summary>
    /// End date for range selection mode. Null when not in range mode or no end selected.
    /// </summary>
    public DateTime? RangeEndDate
    {
        get => _rangeEndDate;
        set
        {
            if (_rangeEndDate == value) return;
            _rangeEndDate = value;
            RangeSelected?.Invoke(this, new DateRangeSelectedEventArgs(_selectedDate, _rangeEndDate));
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Minimum selectable date (compared by day), or null for none. Earlier days are drawn disabled and
    /// cannot be picked, stepped to with the keyboard or typed. Setting it after <see cref="MaxDate"/> and
    /// later than it moves <see cref="MaxDate"/> to the same day, so the minimum never exceeds the maximum.
    /// </summary>
    public DateTime? MinDate
    {
        get => _minDate;
        set
        {
            _minDate = value?.Date;
            if (_minDate is { } min && _maxDate is { } max && min > max) _maxDate = min;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Maximum selectable date (compared by day), or null for none. Later days are drawn disabled and
    /// cannot be picked, stepped to with the keyboard or typed. Setting it earlier than
    /// <see cref="MinDate"/> moves <see cref="MinDate"/> to the same day, so the minimum never exceeds the
    /// maximum.
    /// </summary>
    public DateTime? MaxDate
    {
        get => _maxDate;
        set
        {
            _maxDate = value?.Date;
            if (_maxDate is { } max && _minDate is { } min && max < min) _minDate = max;
            InvalidateVisual();
        }
    }

    /// <summary>Is the calendar popup currently open?</summary>
    public bool IsCalendarOpen => _isCalendarOpen;

    /// <summary>Enable date range selection mode (select start + end date).</summary>
    public bool IsRangeMode { get; set; }

    /// <summary>Enable the time picker component alongside the date picker.</summary>
    public bool ShowTimePicker { get; set; }

    /// <summary>Use 24-hour format for the time picker (default: false = 12h AM/PM).</summary>
    public bool Use24HourFormat { get; set; }

    /// <summary>Gets or sets the selected hour (0-23).</summary>
    public int SelectedHour { get; set; }

    /// <summary>Gets or sets the selected minute (0-59).</summary>
    public int SelectedMinute { get; set; }

    /// <summary>Gets the selected date and time combined.</summary>
    public DateTime SelectedDateTime => _selectedDate.Date.AddHours(SelectedHour).AddMinutes(SelectedMinute);

    // ── Events ──────────────────────────────────────────────────

    /// <summary>Raised when a date is selected in the calendar.</summary>
    public event EventHandler<DateSelectedEventArgs>? DateSelected;

    /// <summary>Raised when a date range is selected in range mode.</summary>
    public event EventHandler<DateRangeSelectedEventArgs>? RangeSelected;

    /// <summary>Raised when the time portion changes.</summary>
    public event EventHandler<KumikoTimeChangedEventArgs>? TimeChanged;

    // ── Dates ───────────────────────────────────────────────────

    private bool IsDisabled(DateTime date) =>
        (_minDate is { } min && date.Date < min) || (_maxDate is { } max && date.Date > max);

    private DateTime ClampToRange(DateTime date)
    {
        if (_minDate is { } min && date.Date < min) return min;
        if (_maxDate is { } max && date.Date > max) return max;
        return date;
    }

    private static DateTime AddDaysSafe(DateTime date, int days)
    {
        long ticks = date.Ticks + (long)days * TimeSpan.TicksPerDay;
        if (ticks < DateTime.MinValue.Ticks) return DateTime.MinValue;
        if (ticks > DateTime.MaxValue.Ticks) return DateTime.MaxValue;
        return new DateTime(ticks, date.Kind);
    }

    private static bool TryAddMonths(DateTime date, int months, out DateTime result)
    {
        long total = date.Year * 12L + (date.Month - 1) + months;
        long year = total / 12;
        if (total < 0 || year < 1 || year > 9999)
        {
            result = date;
            return false;
        }
        result = date.AddMonths(months);
        return true;
    }

    /// <summary>How many columns the first of the displayed month sits to the right of the week's first column.</summary>
    private int LeadingDays(DateTime firstOfMonth) =>
        ((int)firstOfMonth.DayOfWeek - (int)EffectiveFirstDay + DaysPerWeek) % DaysPerWeek;

    private DateTime CellDate(int row, int column) =>
        AddDaysSafe(_displayMonth, row * DaysPerWeek + column - LeadingDays(_displayMonth));

    /// <summary>The month name comes before the year in the header, in the culture's year-month pattern.</summary>
    private bool MonthFirst
    {
        get
        {
            string pattern = EffectiveCulture.DateTimeFormat.YearMonthPattern;
            int year = pattern.IndexOf('y');
            int month = pattern.IndexOf('M');
            return year < 0 || month < 0 || month < year;
        }
    }

    private string YearText(int year)
    {
        if (year != _yearTextYear)
        {
            _yearText = YearString(year);
            _yearTextYear = year;
        }
        return _yearText;
    }

    private string YearString(int year)
    {
        if (!_yearStrings.TryGetValue(year, out var text))
        {
            text = year.ToString(CultureInfo.InvariantCulture);
            _yearStrings[year] = text;
        }
        return text;
    }

    private int FirstYear => Math.Clamp(_minDate?.Year ?? Math.Min(DefaultFirstYear, _displayMonth.Year), 1, 9999);

    private int LastYear => Math.Clamp(_maxDate?.Year ?? Math.Max(DefaultLastYear, _displayMonth.Year), FirstYear, 9999);

    // ── Field text ──────────────────────────────────────────────

    private string EffectiveFormat => string.IsNullOrEmpty(_dateFormat) ? "d" : _dateFormat;

    private string FormatDate(DateTime date)
    {
        try
        {
            return date.ToString(EffectiveFormat, EffectiveCulture);
        }
        catch (FormatException)
        {
            return date.ToString("d", EffectiveCulture); // a format that is not a date format: the culture's
        }
    }

    private int _timeTextHour = -1, _timeTextMinute = -1;
    private bool _timeText24;
    private CultureInfo? _timeTextCulture;
    private string _timeText = string.Empty;

    private string TimeText()
    {
        var culture = EffectiveCulture;
        if (_timeTextHour != SelectedHour || _timeTextMinute != SelectedMinute || _timeText24 != Use24HourFormat
            || !ReferenceEquals(_timeTextCulture, culture))
        {
            int displayHour = SelectedHour;
            string amPm = "";
            if (!Use24HourFormat)
            {
                amPm = AmPmSuffix(displayHour);
                displayHour = displayHour % 12;
                if (displayHour == 0) displayHour = 12;
            }
            _timeText = $"{displayHour:D2}:{SelectedMinute:D2}{amPm}";
            _timeTextHour = SelectedHour;
            _timeTextMinute = SelectedMinute;
            _timeText24 = Use24HourFormat;
            _timeTextCulture = culture;
        }
        return _timeText;
    }

    /// <summary>The text of the field: the date, the time, and the end of a range.</summary>
    private string FieldText
    {
        get
        {
            var key = new FieldKey(_hasSelection, _selectedDate, _dateFormat, Culture, ShowTimePicker,
                Use24HourFormat, SelectedHour, SelectedMinute, IsRangeMode, _rangeEndDate);
            if (!_fieldKeyValid || !_fieldKey.Equals(key))
            {
                string text = _hasSelection ? FormatDate(_selectedDate) : string.Empty;
                if (ShowTimePicker) text += " " + TimeText();
                if (IsRangeMode && _rangeEndDate.HasValue) text += " — " + FormatDate(_rangeEndDate.Value);
                _fieldText = text;
                _fieldKey = key;
                _fieldKeyValid = true;
            }
            return _fieldText;
        }
    }

    // ── Paint helpers ───────────────────────────────────────────

    private KumikoPaint Fill(KumikoColor color)
    {
        _fillPaint.Color = color;
        return _fillPaint;
    }

    private KumikoPaint Stroke(KumikoColor color, float width)
    {
        _strokePaint.Color = color;
        _strokePaint.StrokeWidth = width;
        return _strokePaint;
    }

    private KumikoPaint Text(KumikoColor color, KumikoFont font)
    {
        _textPaint.Color = color;
        _textPaint.Font = font;
        return _textPaint;
    }

    // ── Drawing ─────────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnDraw(IDrawingContext ctx)
    {
        DrawDisplayField(ctx);

        if (_isCalendarOpen)
            DrawCalendar(ctx);
    }

    private void DrawDisplayField(IDrawingContext ctx)
    {
        var b = Bounds;
        ctx.FillRect(b, Fill(BackgroundColor));

        float iconSize = 16f;
        float iconX = b.X + b.Width - iconSize - Padding;

        if (_typing)
        {
            // Typed entry: the text box draws the text, the caret and the selection
            _box.Bounds = new KumikoRect(b.X, b.Y, Math.Max(0f, iconX - b.X), b.Height);
            _box.Padding = Padding;
            _box.Font = Font;
            _box.TextColor = TextColor;
            _box.BackgroundColor = BackgroundColor;
            _box.EditingBackgroundColor = BackgroundColor;
            _box.BorderColor = BackgroundColor;
            _box.FocusedBorderColor = BackgroundColor;
            _box.OnDraw(ctx);
        }
        else
        {
            float textY = b.Y + b.Height / 2;
            ctx.DrawText(FieldText, b.X + Padding, textY, Text(TextColor, Font));
        }

        bool error = _typing && _invalidText;
        ctx.DrawRect(b, Stroke(error ? ErrorBorderColor : IsFocused ? FocusedBorderColor : BorderColor, error ? 2 : 1));

        // Calendar icon (simple grid/lines)
        DrawCalendarIcon(ctx, iconX, b.Y + (b.Height - iconSize) / 2, iconSize);
    }

    private void DrawCalendarIcon(IDrawingContext ctx, float x, float y, float size)
    {
        var paint = Stroke(new KumikoColor(100, 100, 100), 1);
        ctx.DrawRect(new KumikoRect(x, y + 3, size, size - 3), paint);
        ctx.DrawLine(x, y + 7, x + size, y + 7, paint);
        // Top "tabs"
        ctx.DrawLine(x + 4, y, x + 4, y + 4, paint);
        ctx.DrawLine(x + size - 4, y, x + size - 4, y + 4, paint);
    }

    private void DrawCalendar(IDrawingContext ctx)
    {
        var calRect = GetCalendarBounds();
        float calWidth = calRect.Width;

        // Shadow + background
        ctx.FillRect(new KumikoRect(calRect.X + 2, calRect.Y + 2, calRect.Width, calRect.Height),
            Fill(new KumikoColor(0, 0, 0, 30)));
        ctx.FillRect(calRect, Fill(CalendarBackgroundColor));
        ctx.DrawRect(calRect, Stroke(BorderColor, 1));

        float contentX = calRect.X + 4;
        float contentY = calRect.Y + 4;

        // Navigation row: < Month Year >
        DrawNavRow(ctx, contentX, contentY, calWidth - 8);
        contentY += NavRowHeight;

        // Day-of-week headers
        DrawDayHeaders(ctx, contentX, contentY);
        contentY += HeaderHeight;

        // Day grid
        DrawDayGrid(ctx, contentX, contentY);
        contentY += MaxWeeks * DayCellSize;

        // Time picker row
        if (ShowTimePicker)
        {
            contentY += 4;
            DrawTimePicker(ctx, contentX, contentY, calWidth - 8);
        }

        // Month dropdown overlay
        if (_isMonthDropdownOpen)
            DrawMonthDropdown(ctx, calRect);

        // Year dropdown overlay
        if (_isYearDropdownOpen)
            DrawYearDropdown(ctx, calRect);
    }

    private void DrawNavRow(IDrawingContext ctx, float x, float y, float width)
    {
        // Left arrow
        _arrowPaint.Color = ArrowColor;
        _arrowPaint.StrokeWidth = 1.5f;
        float arrowY = y + NavRowHeight / 2;
        GlyphDrawing.Chevron(ctx, x + 8, arrowY - 4, x + 5, arrowY, x + 8, arrowY + 4, _arrowPaint);

        // Right arrow
        float rx = x + width - 10;
        GlyphDrawing.Chevron(ctx, rx - 3, arrowY - 4, rx, arrowY, rx - 3, arrowY + 4, _arrowPaint);

        // Month and year (each clickable for its dropdown), in the order of the culture
        string monthText = EffectiveCulture.DateTimeFormat.GetMonthName(_displayMonth.Month);
        string yearText = YearText(_displayMonth.Year);
        bool monthFirst = MonthFirst;
        string first = monthFirst ? monthText : yearText;
        string second = monthFirst ? yearText : monthText;
        bool firstActive = monthFirst ? _isMonthDropdownOpen : _isYearDropdownOpen;
        bool secondActive = monthFirst ? _isYearDropdownOpen : _isMonthDropdownOpen;

        float firstWidth = ctx.MeasureText(first, Text(TextColor, Font)).Width;
        float secondWidth = ctx.MeasureText(second, Text(TextColor, Font)).Width;
        float gap = 6f;
        float startX = x + (width - (firstWidth + gap + secondWidth)) / 2;

        // The label whose dropdown is open is drawn in the accent color
        ctx.DrawText(first, startX, arrowY, Text(firstActive ? FocusedBorderColor : TextColor, Font));
        ctx.DrawText(second, startX + firstWidth + gap, arrowY, Text(secondActive ? FocusedBorderColor : TextColor, Font));
    }

    private void DrawDayHeaders(IDrawingContext ctx, float x, float y)
    {
        var names = EffectiveCulture.DateTimeFormat;
        int first = (int)EffectiveFirstDay;
        var headerColor = new KumikoColor(100, 100, 100);

        for (int d = 0; d < DaysPerWeek; d++)
        {
            string name = names.GetShortestDayName((DayOfWeek)((first + d) % DaysPerWeek));
            float cx = x + d * DayCellSize + DayCellSize / 2;
            float cy = y + HeaderHeight / 2;
            var paint = Text(headerColor, DayFont);
            float tw = ctx.MeasureText(name, paint).Width;
            ctx.DrawText(name, cx - tw / 2, cy, paint);
        }
    }

    private void DrawDayGrid(IDrawingContext ctx, float x, float y)
    {
        var firstOfMonth = _displayMonth;
        var today = DateTime.Today;

        // Determine range for highlighting
        DateTime? rangeStart = null, rangeEnd = null;
        if (IsRangeMode && _rangeEndDate.HasValue)
        {
            rangeStart = _selectedDate < _rangeEndDate.Value ? _selectedDate : _rangeEndDate.Value;
            rangeEnd = _selectedDate < _rangeEndDate.Value ? _rangeEndDate.Value : _selectedDate;
        }

        for (int week = 0; week < MaxWeeks; week++)
        {
            for (int dow = 0; dow < DaysPerWeek; dow++)
            {
                var date = CellDate(week, dow);

                float cellX = x + dow * DayCellSize;
                float cellY = y + week * DayCellSize;
                var cellRect = new KumikoRect(cellX, cellY, DayCellSize, DayCellSize);

                bool isCurrentMonth = date.Month == firstOfMonth.Month;
                bool isToday = date.Date == today;
                bool isSelected = _hasSelection && date.Date == _selectedDate.Date;
                bool isRangeEnd = IsRangeMode && _rangeEndDate.HasValue && date.Date == _rangeEndDate.Value.Date;
                bool isInRange = rangeStart.HasValue && rangeEnd.HasValue &&
                                 date.Date >= rangeStart.Value.Date && date.Date <= rangeEnd.Value.Date;
                bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                bool isDisabled = IsDisabled(date);

                // Range background
                if (isInRange && !isSelected && !isRangeEnd)
                    ctx.FillRect(cellRect, Fill(new KumikoColor(0, 120, 215, 25)));

                // Background highlights
                if (isSelected || isRangeEnd)
                    ctx.FillRoundRect(cellRect, 4, Fill(SelectedDayColor));
                else if (isToday)
                    ctx.FillRoundRect(cellRect, 4, Fill(TodayHighlightColor));

                // Day number text
                string dayText = DayNumbers[date.Day];
                KumikoColor textColor;
                if (isSelected || isRangeEnd) textColor = SelectedDayTextColor;
                else if (isDisabled) textColor = DisabledDayColor;
                else if (!isCurrentMonth) textColor = OtherMonthColor;
                else if (isWeekend) textColor = WeekendColor;
                else textColor = TextColor;

                var dayPaint = Text(textColor, DayFont);
                float tw = ctx.MeasureText(dayText, dayPaint).Width;
                ctx.DrawText(dayText, cellX + (DayCellSize - tw) / 2, cellY + DayCellSize / 2, dayPaint);
            }
        }
    }

    /// <summary>
    /// Get the bounding rect of the calendar popup: below the date field, above it when there is no room
    /// below, and inside <see cref="DrawnComponent.PopupViewport"/>.
    /// </summary>
    public KumikoRect GetCalendarBounds()
    {
        float calWidth = DaysPerWeek * DayCellSize + 8;
        float calHeight = NavRowHeight + HeaderHeight + MaxWeeks * DayCellSize + 8;
        if (ShowTimePicker) calHeight += NavRowHeight + 8;
        return PopupLayout.Place(Bounds, calWidth, calHeight, PopupViewport, gap: 2);
    }

    // ── Input ───────────────────────────────────────────────────

    /// <summary>
    /// Expanded hit test that includes the calendar popup area when open.
    /// </summary>
    public override bool HitTest(float x, float y)
    {
        if (!IsVisible || !IsEnabled) return false;
        if (Bounds.Contains(x, y)) return true;
        if (_isCalendarOpen && GetCalendarBounds().Contains(x, y)) return true;
        return false;
    }

    /// <inheritdoc />
    public override bool OnPointerDown(KumikoPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        _pressedDate = null;
        _yearPressed = false;

        if (_isCalendarOpen)
        {
            // Check month dropdown first (overlays calendar)
            if (_isMonthDropdownOpen)
            {
                if (HandleMonthDropdownClick(e.X, e.Y)) return true;
                _isMonthDropdownOpen = false;
                InvalidateVisual();
                return true;
            }

            // Check year dropdown
            if (_isYearDropdownOpen)
            {
                if (HandleYearDropdownPress(e.X, e.Y)) return true;
                _isYearDropdownOpen = false;
                InvalidateVisual();
                return true;
            }

            var calBounds = GetCalendarBounds();
            if (calBounds.Contains(e.X, e.Y))
            {
                HandleCalendarPress(e.X, e.Y, calBounds);
                return true;
            }

            CloseCalendar();
            return true;
        }

        OpenCalendar();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerMove(KumikoPointerEventArgs e)
    {
        if (!_yearPressed) return false;

        float moved = e.Y - _yearPressY;
        if (!_yearDragged && Math.Abs(moved) < YearDragThreshold) return true;

        _yearDragged = true;
        _yearScrollOffset = ClampYearScroll(_yearPressOffset - moved);
        InvalidateVisual();
        return true;
    }

    /// <inheritdoc />
    public override bool OnPointerUp(KumikoPointerEventArgs e)
    {
        if (_yearPressed)
        {
            _yearPressed = false;
            if (!_yearDragged && _isYearDropdownOpen)
            {
                _displayMonth = new DateTime(_yearPressedYear, _displayMonth.Month, 1);
                _isYearDropdownOpen = false;
                InvalidateVisual();
            }
            _yearDragged = false;
            return true;
        }

        if (_isCalendarOpen && _pressedDate.HasValue)
        {
            var calBounds = GetCalendarBounds();
            var releasedDate = HitTestDayCell(e.X, e.Y, calBounds);
            if (releasedDate.HasValue && releasedDate.Value.Date == _pressedDate.Value.Date)
            {
                ApplyDateSelection(_pressedDate.Value);
            }
            _pressedDate = null;
            return true;
        }

        _pressedDate = null;
        return true;
    }

    /// <inheritdoc />
    public override void OnPointerCancel(KumikoPointerEventArgs e)
    {
        _pressedDate = null;
        _yearPressed = false;
        _yearDragged = false;
    }

    /// <summary>
    /// The mouse wheel scrolls the year list while it is open, and otherwise moves the calendar a month
    /// (wheel up: earlier).
    /// </summary>
    public override bool OnScroll(KumikoPointerEventArgs e)
    {
        if (!_isCalendarOpen || e.ScrollDeltaY == 0) return false;

        if (_isYearDropdownOpen)
        {
            _yearScrollOffset = ClampYearScroll(_yearScrollOffset - e.ScrollDeltaY * DropdownItemHeight * 2);
            InvalidateVisual();
            return true;
        }

        if (_isMonthDropdownOpen) return true;

        if (e.ScrollDeltaY > 0) PreviousMonth(); else NextMonth();
        return true;
    }

    /// <summary>
    /// Hit-test which day cell is at the given pointer coordinates.
    /// Returns null if the pointer is not over a valid, selectable day.
    /// </summary>
    private DateTime? HitTestDayCell(float px, float py, KumikoRect calBounds)
    {
        float contentX = calBounds.X + 4;
        float contentY = calBounds.Y + 4 + NavRowHeight + HeaderHeight;

        float dayGridBottom = contentY + MaxWeeks * DayCellSize;
        if (py < contentY || py > dayGridBottom) return null;

        int col = (int)((px - contentX) / DayCellSize);
        int row = (int)((py - contentY) / DayCellSize);
        if (col < 0 || col >= DaysPerWeek || row < 0 || row >= MaxWeeks) return null;

        var date = CellDate(row, col);
        return IsDisabled(date) ? null : date;
    }

    /// <summary>
    /// Handles the press phase on the calendar — navigates months/years immediately
    /// but only records the pressed day (selection is deferred to OnPointerUp).
    /// </summary>
    private void HandleCalendarPress(float clickX, float clickY, KumikoRect calBounds)
    {
        float contentX = calBounds.X + 4;
        float contentY = calBounds.Y + 4;

        // Check nav row — navigation controls respond on press
        if (clickY < contentY + NavRowHeight)
        {
            float leftArrowEnd = contentX + 20;
            float rightArrowStart = contentX + calBounds.Width - 28;

            if (clickX < leftArrowEnd)
            {
                PreviousMonth();
            }
            else if (clickX > rightArrowStart)
            {
                NextMonth();
            }
            else
            {
                // Click on the month or the year label: the one on the left of the middle is the first
                float midX = contentX + (calBounds.Width - 8) / 2;
                bool month = (clickX < midX) == MonthFirst;
                if (month)
                {
                    _isMonthDropdownOpen = !_isMonthDropdownOpen;
                    _isYearDropdownOpen = false;
                    _highlightedMonthIndex = _displayMonth.Month - 1;
                }
                else
                {
                    _isYearDropdownOpen = !_isYearDropdownOpen;
                    _isMonthDropdownOpen = false;
                    _highlightedYearIndex = -1;
                    // Scroll to show the current year in the middle of the list
                    int currentYearIndex = _displayMonth.Year - FirstYear;
                    _yearScrollOffset = ClampYearScroll(currentYearIndex * DropdownItemHeight - YearDropdownMaxHeight / 2);
                }
                InvalidateVisual();
            }
            return;
        }

        contentY += NavRowHeight;

        // Skip header row
        if (clickY < contentY + HeaderHeight) return;
        contentY += HeaderHeight;

        // Check time picker area — responds on press
        float dayGridBottom = contentY + MaxWeeks * DayCellSize;
        if (ShowTimePicker && clickY > dayGridBottom)
        {
            HandleTimePickerClick(clickX, clickY, calBounds.X + 4, dayGridBottom + 4, calBounds.Width - 8);
            return;
        }

        // Record pressed day — selection is deferred to OnPointerUp
        var date = HitTestDayCell(clickX, clickY, calBounds);
        if (date.HasValue)
        {
            _pressedDate = date;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Applies a day selection (called from OnPointerUp after confirming the release
    /// is on the same day that was pressed).
    /// </summary>
    private void ApplyDateSelection(DateTime date)
    {
        EndTyping();
        if (IsRangeMode)
        {
            // Range mode: first click = start, second click = end
            if (_rangeEndDate == null && date.Date != _selectedDate.Date)
            {
                RangeEndDate = date;
            }
            else
            {
                // Start new range
                var old = _selectedDate;
                _selectedDate = date.Date;
                _rangeEndDate = null;
                _displayMonth = new DateTime(date.Year, date.Month, 1);
                RaiseValueChanged(old, _selectedDate);
                DateSelected?.Invoke(this, new DateSelectedEventArgs(old, _selectedDate));
                InvalidateVisual();
            }
        }
        else
        {
            SelectedDate = date;
            CloseCalendar();
            RaiseEditCompleted();
        }
    }

    // ── Keyboard input ──────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyDown(KumikoKeyEventArgs e)
    {
        // A typed date waiting to be applied has its own keys
        if (_typing && _textDirty)
        {
            switch (e.Key)
            {
                case KumikoKey.Enter:
                    ConfirmTypedDate();
                    return true;
                case KumikoKey.Escape:
                    EndTyping();
                    return true;
                case KumikoKey.Tab:
                    return false;
                case KumikoKey.Up:
                case KumikoKey.Down:
                case KumikoKey.PageUp:
                case KumikoKey.PageDown:
                    // Moving through the calendar takes the typed date along if it is one, else drops it
                    if (TryParseTyped(_box.Text, out var typedDate)) ApplyTyped(typedDate);
                    EndTyping();
                    break;
                default:
                    return _box.OnKeyDown(e);
            }
        }

        if (!_isCalendarOpen)
        {
            if (e.Key == KumikoKey.Space || e.Key == KumikoKey.Enter || e.Key == KumikoKey.Down)
            {
                OpenCalendar();
                return true;
            }
            return IsTextKey(e) && StartTyping(e);
        }

        switch (e.Key)
        {
            case KumikoKey.Left:
                if (_typing) return _box.OnKeyDown(e);
                Move(-1);
                return true;
            case KumikoKey.Right:
                if (_typing) return _box.OnKeyDown(e);
                Move(1);
                return true;
            case KumikoKey.Up:
                Move(-7);
                return true;
            case KumikoKey.Down:
                Move(7);
                return true;
            case KumikoKey.PageUp:
                MoveMonths(e.HasShift ? -12 : -1);
                return true;
            case KumikoKey.PageDown:
                MoveMonths(e.HasShift ? 12 : 1);
                return true;
            case KumikoKey.Home:
                if (_typing) return _box.OnKeyDown(e);
                MoveToWeekEdge(end: false);
                return true;
            case KumikoKey.End:
                if (_typing) return _box.OnKeyDown(e);
                MoveToWeekEdge(end: true);
                return true;
            case KumikoKey.Enter:
                CloseCalendar();
                if (_hasSelection) RaiseEditCompleted(); // Enter confirms the date, like a click on it
                return true;
            case KumikoKey.Escape:
                CloseCalendar();
                return true;
        }

        return IsTextKey(e) && StartTyping(e);
    }

    private static bool IsTextKey(KumikoKeyEventArgs e) =>
        e.Key is KumikoKey.Backspace or KumikoKey.Delete || e.EnteredText != null
        || e.HasCommand && e.Key is KumikoKey.A or KumikoKey.C or KumikoKey.V or KumikoKey.X or KumikoKey.Z or KumikoKey.Y;

    /// <summary>Starts typed entry with <paramref name="e"/>, which replaces the text as it selects it.</summary>
    private bool StartTyping(KumikoKeyEventArgs e)
    {
        if (IsRangeMode) return false; // a range has no single date to type
        BeginTyping();
        return _box.OnKeyDown(e);
    }

    /// <summary>Move the selection by <paramref name="days"/>, staying inside the allowed dates; with nothing selected, select the starting date first.</summary>
    private void Move(int days)
    {
        if (!_hasSelection)
        {
            SelectedDate = ClampToRange(_selectedDate);
            return;
        }
        SelectedDate = ClampToRange(AddDaysSafe(_selectedDate, days));
    }

    private void MoveMonths(int months)
    {
        if (!TryAddMonths(_selectedDate, months, out var target)) return;
        if (!_hasSelection) target = _selectedDate;
        SelectedDate = ClampToRange(target);
    }

    private void MoveToWeekEdge(bool end)
    {
        int firstColumn = (int)EffectiveFirstDay;
        int column = ((int)_selectedDate.DayOfWeek - firstColumn + DaysPerWeek) % DaysPerWeek;
        int days = end ? DaysPerWeek - 1 - column : -column;
        SelectedDate = ClampToRange(AddDaysSafe(_selectedDate, days));
    }

    // ── Calendar management ─────────────────────────────────────

    /// <summary>Open the calendar popup and reset to the selected date’s month.</summary>
    public void OpenCalendar()
    {
        _isCalendarOpen = true;
        _isMonthDropdownOpen = false;
        _isYearDropdownOpen = false;
        _displayMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        InvalidateVisual();
    }

    /// <summary>Close the calendar popup and any open dropdowns.</summary>
    public void CloseCalendar()
    {
        _isCalendarOpen = false;
        _isMonthDropdownOpen = false;
        _isYearDropdownOpen = false;
        InvalidateVisual();
    }

    /// <summary>
    /// Navigate the calendar to the previous month (not past the month of <see cref="MinDate"/>, nor before year 1).
    /// </summary>
    public void PreviousMonth()
    {
        if (!TryAddMonths(_displayMonth, -1, out var previous)) return;
        if (_minDate is { } min && previous.AddMonths(1).AddDays(-1) < min) return;
        _displayMonth = previous;
        InvalidateVisual();
    }

    /// <summary>
    /// Navigate the calendar to the next month (not past the month of <see cref="MaxDate"/>, nor after year 9999).
    /// </summary>
    public void NextMonth()
    {
        if (!TryAddMonths(_displayMonth, 1, out var next)) return;
        if (_maxDate is { } max && next > max) return;
        _displayMonth = next;
        InvalidateVisual();
    }

    // ── Typed entry ─────────────────────────────────────────────

    /// <summary>The date text as the user edits it: what the field shows.</summary>
    private string EditText()
    {
        string text = _hasSelection ? FormatDate(_selectedDate) : string.Empty;
        return ShowTimePicker && _hasSelection ? text + " " + TimeText() : text;
    }

    private void BeginTyping()
    {
        if (_typing) return;
        _typing = true;
        _textDirty = false;
        _invalidText = false;
        _box.Text = EditText();
        _box.IsFocused = true;
        _box.SelectAll();
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

    /// <summary>
    /// Parses typed text in the culture: the picker's format first, then every pattern of the culture.
    /// A time of day in the text is returned too, when it has one.
    /// </summary>
    private bool TryParseTyped(string text, out DateTime date)
    {
        text = text.Trim();
        var culture = EffectiveCulture;
        var styles = DateTimeStyles.AllowWhiteSpaces;
        if (text.Length > 0 &&
            (DateTime.TryParseExact(text, EffectiveFormat, culture, styles, out date) ||
             DateTime.TryParse(text, culture, styles, out date)))
            return true;
        date = default;
        return false;
    }

    private bool IsTypedAcceptable(string text, out DateTime date) =>
        TryParseTyped(text, out date) && !IsDisabled(date);

    private void OnTextEdited()
    {
        if (!_typing) return;
        _textDirty = true;
        _invalidText = !IsTypedAcceptable(_box.Text, out var date);
        if (!_invalidText)
            _displayMonth = new DateTime(date.Year, date.Month, 1); // show the month of the date being typed
        InvalidateVisual();
    }

    /// <summary>Selects the typed date (and its time, with a time picker) and tells the editor's host.</summary>
    private void ApplyTyped(DateTime typed)
    {
        if (ShowTimePicker && typed.TimeOfDay != TimeSpan.Zero)
        {
            int oldHour = SelectedHour, oldMinute = SelectedMinute;
            SelectedHour = typed.Hour;
            SelectedMinute = typed.Minute;
            if (oldHour != SelectedHour || oldMinute != SelectedMinute)
                TimeChanged?.Invoke(this, new KumikoTimeChangedEventArgs(oldHour, oldMinute, SelectedHour, SelectedMinute));
        }
        SelectedDate = typed.Date;
    }

    /// <summary>
    /// Enter on typed text: a date is selected and confirmed like a click on a day; anything else is
    /// reported complete too, so the edit session refuses it with its validation message and keeps editing.
    /// </summary>
    private void ConfirmTypedDate()
    {
        if (IsTypedAcceptable(_box.Text, out var typed))
        {
            ApplyTyped(typed);
            EndTyping();
            CloseCalendar();
        }
        RaiseEditCompleted();
    }

    // ── Month dropdown ──────────────────────────────────────────

    private KumikoRect MonthDropdownRect(KumikoRect calRect)
    {
        float shift = MonthFirst ? -20 : 20;
        return new KumikoRect(calRect.X + (calRect.Width - MonthDropdownWidth) / 2 + shift,
            calRect.Y + 4 + NavRowHeight, MonthDropdownWidth, 12 * DropdownItemHeight);
    }

    private KumikoRect YearDropdownRect(KumikoRect calRect)
    {
        float shift = MonthFirst ? 20 : -20;
        int totalYears = LastYear - FirstYear + 1;
        return new KumikoRect(calRect.X + (calRect.Width - YearDropdownWidth) / 2 + shift,
            calRect.Y + 4 + NavRowHeight, YearDropdownWidth, Math.Min(totalYears * DropdownItemHeight, YearDropdownMaxHeight));
    }

    private void DrawMonthDropdown(IDrawingContext ctx, KumikoRect calRect)
    {
        var dropRect = MonthDropdownRect(calRect);

        ctx.FillRect(new KumikoRect(dropRect.X + 2, dropRect.Y + 2, dropRect.Width, dropRect.Height),
            Fill(new KumikoColor(0, 0, 0, 30)));
        ctx.FillRect(dropRect, Fill(CalendarBackgroundColor));
        ctx.DrawRect(dropRect, Stroke(BorderColor, 1));

        for (int m = 0; m < 12; m++)
        {
            float itemY = dropRect.Y + m * DropdownItemHeight;
            var itemRect = new KumikoRect(dropRect.X, itemY, MonthDropdownWidth, DropdownItemHeight);

            if (m == _highlightedMonthIndex)
                ctx.FillRect(itemRect, Fill(new KumikoColor(0, 120, 215, 40)));

            if (m == _displayMonth.Month - 1)
                ctx.FillRect(itemRect, Fill(new KumikoColor(0, 120, 215, 20)));

            string monthName = EffectiveCulture.DateTimeFormat.GetMonthName(m + 1);
            ctx.DrawText(monthName, dropRect.X + 8, itemY + DropdownItemHeight / 2, Text(TextColor, DayFont));
        }
    }

    private bool HandleMonthDropdownClick(float clickX, float clickY)
    {
        var dropRect = MonthDropdownRect(GetCalendarBounds());
        if (!dropRect.Contains(clickX, clickY)) return false;

        int monthIndex = (int)((clickY - dropRect.Y) / DropdownItemHeight);
        if (monthIndex >= 0 && monthIndex < 12)
        {
            _displayMonth = new DateTime(_displayMonth.Year, monthIndex + 1, 1);
            _isMonthDropdownOpen = false;
            InvalidateVisual();
            return true;
        }
        return false;
    }

    // ── Year dropdown ───────────────────────────────────────────

    private float ClampYearScroll(float offset)
    {
        float content = (LastYear - FirstYear + 1) * DropdownItemHeight;
        float visible = Math.Min(content, YearDropdownMaxHeight);
        return Math.Clamp(offset, 0f, Math.Max(0f, content - visible));
    }

    private void DrawYearDropdown(IDrawingContext ctx, KumikoRect calRect)
    {
        var dropRect = YearDropdownRect(calRect);
        int firstYear = FirstYear;
        int totalYears = LastYear - firstYear + 1;

        ctx.FillRect(new KumikoRect(dropRect.X + 2, dropRect.Y + 2, dropRect.Width, dropRect.Height),
            Fill(new KumikoColor(0, 0, 0, 30)));
        ctx.FillRect(dropRect, Fill(CalendarBackgroundColor));
        ctx.DrawRect(dropRect, Stroke(BorderColor, 1));

        ctx.Save();
        ctx.ClipRect(dropRect);

        // Only the rows in view
        int from = Math.Max(0, (int)(_yearScrollOffset / DropdownItemHeight));
        int to = Math.Min(totalYears - 1, (int)((_yearScrollOffset + dropRect.Height) / DropdownItemHeight));
        for (int i = from; i <= to; i++)
        {
            float itemY = dropRect.Y + i * DropdownItemHeight - _yearScrollOffset;
            int year = firstYear + i;
            var itemRect = new KumikoRect(dropRect.X, itemY, YearDropdownWidth, DropdownItemHeight);

            if (i == _highlightedYearIndex)
                ctx.FillRect(itemRect, Fill(new KumikoColor(0, 120, 215, 40)));

            if (year == _displayMonth.Year)
                ctx.FillRect(itemRect, Fill(new KumikoColor(0, 120, 215, 20)));

            ctx.DrawText(YearString(year), dropRect.X + 8, itemY + DropdownItemHeight / 2, Text(TextColor, DayFont));
        }

        ctx.Restore();
    }

    /// <summary>A press in the year list: the year under it is picked when the press is released without a drag.</summary>
    private bool HandleYearDropdownPress(float clickX, float clickY)
    {
        var dropRect = YearDropdownRect(GetCalendarBounds());
        if (!dropRect.Contains(clickX, clickY)) return false;

        int index = (int)((clickY - dropRect.Y + _yearScrollOffset) / DropdownItemHeight);
        int year = FirstYear + index;
        if (year < FirstYear || year > LastYear) return false;

        _yearPressed = true;
        _yearDragged = false;
        _yearPressY = clickY;
        _yearPressOffset = _yearScrollOffset;
        _yearPressedYear = year;
        return true;
    }

    // ── Time picker ─────────────────────────────────────────────

    private void DrawTimePicker(IDrawingContext ctx, float x, float y, float width)
    {
        // Separator line
        ctx.DrawLine(x, y - 2, x + width, y - 2, Stroke(BorderColor, 0.5f));

        var textPaint = Text(TextColor, Font);
        float centerX = x + width / 2;
        float centerY = y + NavRowHeight / 2;

        string timeText = TimeText();
        float textWidth = ctx.MeasureText(timeText, textPaint).Width;

        // Draw time text centered
        ctx.DrawText(timeText, centerX - textWidth / 2, centerY, textPaint);

        // Draw up/down arrows for hour
        float hourX = centerX - textWidth / 2;
        _arrowPaint.Color = ArrowColor;
        _arrowPaint.StrokeWidth = 1.2f;

        // Hour up
        GlyphDrawing.Chevron(ctx, hourX + 8, y + 2, hourX + 4, y + 6, hourX, y + 2, _arrowPaint);
        // Hour down
        GlyphDrawing.Chevron(ctx, hourX, y + NavRowHeight - 2, hourX + 4, y + NavRowHeight - 6, hourX + 8, y + NavRowHeight - 2, _arrowPaint);

        // Minute arrows
        float minX = centerX + 4;
        // Minute up
        GlyphDrawing.Chevron(ctx, minX + 8, y + 2, minX + 4, y + 6, minX, y + 2, _arrowPaint);
        // Minute down
        GlyphDrawing.Chevron(ctx, minX, y + NavRowHeight - 2, minX + 4, y + NavRowHeight - 6, minX + 8, y + NavRowHeight - 2, _arrowPaint);
    }

    private void HandleTimePickerClick(float clickX, float clickY, float x, float y, float width)
    {
        float centerX = x + width / 2;
        float centerY = y + NavRowHeight / 2;
        int oldHour = SelectedHour;
        int oldMinute = SelectedMinute;

        bool isUpper = clickY < centerY;
        bool isHourArea = clickX < centerX;

        if (isHourArea)
        {
            SelectedHour = isUpper
                ? (SelectedHour + 1) % 24
                : (SelectedHour + 23) % 24; // -1 mod 24
        }
        else
        {
            // Check for AM/PM toggle area
            if (!Use24HourFormat && clickX > centerX + 20)
            {
                SelectedHour = (SelectedHour + 12) % 24;
            }
            else
            {
                SelectedMinute = isUpper
                    ? (SelectedMinute + 1) % 60
                    : (SelectedMinute + 59) % 60; // -1 mod 60
            }
        }

        if (oldHour != SelectedHour || oldMinute != SelectedMinute)
        {
            TimeChanged?.Invoke(this, new KumikoTimeChangedEventArgs(oldHour, oldMinute, SelectedHour, SelectedMinute));
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Applies typed text that is a date, drops any other, and closes the calendar popup when the picker
    /// loses focus.
    /// </summary>
    protected override void OnLostFocus()
    {
        if (_typing && _textDirty && IsTypedAcceptable(_box.Text, out var typed))
            ApplyTyped(typed);
        EndTyping();
        CloseCalendar();
        base.OnLostFocus();
    }

    // ── IValueEditor ────────────────────────────────────────────

    /// <summary>What an edit session is given for typed text that is a date but not an allowed one: not convertible to anything.</summary>
    private sealed class RejectedDate(string text)
    {
        public override string ToString() => text;
    }

    /// <summary>
    /// The value to commit: the selected date and time, or, while typed text is pending, that text parsed.
    /// Text that is not a date is returned as the text itself and a date outside <see cref="MinDate"/> and
    /// <see cref="MaxDate"/> as an object nothing converts from; the edit session refuses both with its
    /// validation message.
    /// </summary>
    object? IValueEditor.Value
    {
        get
        {
            if (_typing && _textDirty)
            {
                if (!TryParseTyped(_box.Text, out var typed)) return _box.Text;
                if (IsDisabled(typed)) return new RejectedDate(_box.Text);

                if (ShowTimePicker)
                    return typed.TimeOfDay != TimeSpan.Zero ? typed : typed.Date.AddHours(SelectedHour).AddMinutes(SelectedMinute);
                return typed.Date + TimeOfDay;
            }

            return !_hasSelection ? null
                : ShowTimePicker ? SelectedDateTime
                : _selectedDate.Date + TimeOfDay;
        }
    }

    bool IValueEditor.HasValue => _hasSelection || _typing && _textDirty;

    bool IValueEditor.IsDirty => HasChangedSinceTracking || _typing && _textDirty;
}
