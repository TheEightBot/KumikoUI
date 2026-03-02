using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// Fully custom-drawn date picker with a calendar popup.
/// Renders month grid, navigation arrows, and day selection.
/// </summary>
public class DrawnDatePicker : DrawnComponent
{
    private DateTime _selectedDate = DateTime.Today;
    private DateTime? _rangeEndDate;
    private DateTime _displayMonth;
    private bool _isCalendarOpen;
    private bool _isMonthDropdownOpen;
    private bool _isYearDropdownOpen;
    private DateTime? _pressedDate;
    private int _highlightedMonthIndex = -1;
    private int _highlightedYearIndex = -1;
    private float _yearScrollOffset;

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

    // Year range for dropdown
    private const int YearRangeBefore = 50;
    private const int YearRangeAfter = 10;

    public DrawnDatePicker()
    {
        _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    // ── Style ───────────────────────────────────────────────────

    /// <summary>Font used for the display field and calendar header.</summary>
    public GridFont Font { get; set; } = new("Default", 13);
    /// <summary>Font used for individual day numbers in the calendar grid.</summary>
    public GridFont DayFont { get; set; } = new("Default", 12);
    /// <summary>Primary text color for labels and day numbers.</summary>
    public GridColor TextColor { get; set; } = new(30, 30, 30);
    /// <summary>Background fill color of the display field.</summary>
    public GridColor BackgroundColor { get; set; } = GridColor.White;
    /// <summary>Border color of the display field when not focused.</summary>
    public GridColor BorderColor { get; set; } = new(180, 180, 180);
    /// <summary>Border color applied when the date picker has input focus.</summary>
    public GridColor FocusedBorderColor { get; set; } = new(0, 120, 215);
    /// <summary>Background fill color of the calendar popup.</summary>
    public GridColor CalendarBackgroundColor { get; set; } = GridColor.White;
    /// <summary>Highlight color drawn behind today’s date in the calendar.</summary>
    public GridColor TodayHighlightColor { get; set; } = new(0, 120, 215, 40);
    /// <summary>Background color of the selected day cell.</summary>
    public GridColor SelectedDayColor { get; set; } = new(0, 120, 215);
    /// <summary>Text color of the selected day number.</summary>
    public GridColor SelectedDayTextColor { get; set; } = GridColor.White;
    /// <summary>Text color for weekend day numbers (Saturday and Sunday).</summary>
    public GridColor WeekendColor { get; set; } = new(200, 60, 60);
    /// <summary>Text color for days that belong to the previous or next month.</summary>
    public GridColor OtherMonthColor { get; set; } = new(180, 180, 180);
    /// <summary>Color of the month navigation arrows.</summary>
    public GridColor ArrowColor { get; set; } = new(80, 80, 80);
    /// <summary>Horizontal padding inside the display field.</summary>
    public float Padding { get; set; } = 6f;
    /// <summary>Date format string used to display the selected date.</summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    // ── Theming ─────────────────────────────────────────────────

    /// <summary>
    /// Applies theme colors from a <see cref="DataGridStyle"/> so the date picker
    /// visually matches the current grid theme.
    /// </summary>
    public void ApplyTheme(DataGridStyle style)
    {
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
        SelectedDayTextColor = accentLum > 0.5 ? new GridColor(0, 0, 0) : new GridColor(255, 255, 255);

        // Weekend color: keep distinct but adapt to theme brightness
        double bgLum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        WeekendColor = bgLum > 0.5 ? new GridColor(200, 60, 60) : new GridColor(255, 120, 120);

        // Other month days: midpoint between text and background
        OtherMonthColor = new GridColor(
            (byte)((txt.R + bg.R) / 2),
            (byte)((txt.G + bg.G) / 2),
            (byte)((txt.B + bg.B) / 2));

        // Arrow: same as other month (secondary tone)
        ArrowColor = OtherMonthColor;
    }

    // ── Properties ──────────────────────────────────────────────

    /// <summary>Gets or sets the selected date.</summary>
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate.Date == value.Date) return;
            var old = _selectedDate;
            _selectedDate = value.Date;
            _displayMonth = new DateTime(value.Year, value.Month, 1);
            RaiseValueChanged(old, _selectedDate);
            DateSelected?.Invoke(this, new DateSelectedEventArgs(old, _selectedDate));
            InvalidateVisual();
        }
    }

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

    /// <summary>Minimum selectable date.</summary>
    public DateTime? MinDate { get; set; }

    /// <summary>Maximum selectable date.</summary>
    public DateTime? MaxDate { get; set; }

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
    public event EventHandler<TimeChangedEventArgs>? TimeChanged;

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

        ctx.FillRect(b, new GridPaint { Color = BackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(b, new GridPaint
        {
            Color = IsFocused ? FocusedBorderColor : BorderColor,
            Style = PaintStyle.Stroke,
            StrokeWidth = 1
        });

        // Date text
        string dateText = _selectedDate.ToString(DateFormat);
        if (ShowTimePicker)
        {
            int displayHour = SelectedHour;
            string amPm = "";
            if (!Use24HourFormat)
            {
                amPm = displayHour >= 12 ? " PM" : " AM";
                displayHour = displayHour % 12;
                if (displayHour == 0) displayHour = 12;
            }
            dateText += $" {displayHour:D2}:{SelectedMinute:D2}{amPm}";
        }
        if (IsRangeMode && _rangeEndDate.HasValue)
        {
            dateText += $" — {_rangeEndDate.Value.ToString(DateFormat)}";
        }
        float textX = b.X + Padding;
        float textY = b.Y + b.Height / 2;
        ctx.DrawText(dateText, textX, textY,
            new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font });

        // Calendar icon (simple grid/lines)
        float iconSize = 16f;
        float iconX = b.X + b.Width - iconSize - Padding;
        float iconY = b.Y + (b.Height - iconSize) / 2;
        DrawCalendarIcon(ctx, iconX, iconY, iconSize);
    }

    private static void DrawCalendarIcon(IDrawingContext ctx, float x, float y, float size)
    {
        var paint = new GridPaint { Color = new GridColor(100, 100, 100), Style = PaintStyle.Stroke, StrokeWidth = 1 };
        ctx.DrawRect(new GridRect(x, y + 3, size, size - 3), paint);
        ctx.DrawLine(x, y + 7, x + size, y + 7, paint);
        // Top "tabs"
        ctx.DrawLine(x + 4, y, x + 4, y + 4, paint);
        ctx.DrawLine(x + size - 4, y, x + size - 4, y + 4, paint);
    }

    private void DrawCalendar(IDrawingContext ctx)
    {
        float calWidth = DaysPerWeek * DayCellSize + 8;
        float calHeight = NavRowHeight + HeaderHeight + MaxWeeks * DayCellSize + 8;
        if (ShowTimePicker) calHeight += NavRowHeight + 8;

        var calRect = new GridRect(Bounds.X, Bounds.Y + Bounds.Height + 2, calWidth, calHeight);

        // Shadow + background
        ctx.FillRect(new GridRect(calRect.X + 2, calRect.Y + 2, calRect.Width, calRect.Height),
            new GridPaint { Color = new GridColor(0, 0, 0, 30), Style = PaintStyle.Fill });
        ctx.FillRect(calRect, new GridPaint { Color = CalendarBackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(calRect, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

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
        var arrowPaint = new GridPaint { Color = ArrowColor, Style = PaintStyle.Stroke, StrokeWidth = 1.5f };
        float arrowY = y + NavRowHeight / 2;
        ctx.DrawLine(x + 10, arrowY, x + 5, arrowY, arrowPaint);
        ctx.DrawLine(x + 5, arrowY, x + 8, arrowY - 4, arrowPaint);
        ctx.DrawLine(x + 5, arrowY, x + 8, arrowY + 4, arrowPaint);

        // Right arrow
        float rx = x + width - 10;
        ctx.DrawLine(rx - 5, arrowY, rx, arrowY, arrowPaint);
        ctx.DrawLine(rx, arrowY, rx - 3, arrowY - 4, arrowPaint);
        ctx.DrawLine(rx, arrowY, rx - 3, arrowY + 4, arrowPaint);

        // Month text (clickable for dropdown)
        string monthText = _displayMonth.ToString("MMMM");
        string yearText = _displayMonth.Year.ToString();
        var textPaint = new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font };
        var underlinePaint = new GridPaint { Color = FocusedBorderColor, Style = PaintStyle.Fill, Font = Font };

        float monthWidth = ctx.MeasureText(monthText, textPaint).Width;
        float yearWidth = ctx.MeasureText(yearText, textPaint).Width;
        float gap = 6f;
        float totalTextWidth = monthWidth + gap + yearWidth;
        float startX = x + (width - totalTextWidth) / 2;

        // Month (with underline hint if dropdown is available)
        ctx.DrawText(monthText, startX, arrowY, _isMonthDropdownOpen ? underlinePaint : textPaint);

        // Year
        float yearX = startX + monthWidth + gap;
        ctx.DrawText(yearText, yearX, arrowY, _isYearDropdownOpen ? underlinePaint : textPaint);
    }

    private void DrawDayHeaders(IDrawingContext ctx, float x, float y)
    {
        string[] dayNames = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        var paint = new GridPaint { Color = new GridColor(100, 100, 100), Style = PaintStyle.Fill };

        for (int d = 0; d < DaysPerWeek; d++)
        {
            float cx = x + d * DayCellSize + DayCellSize / 2;
            float cy = y + HeaderHeight / 2;
            var dayHeaderPaint = new GridPaint { Color = new GridColor(100, 100, 100), Style = PaintStyle.Fill, Font = DayFont };
            float tw = ctx.MeasureText(dayNames[d], dayHeaderPaint).Width;
            ctx.DrawText(dayNames[d], cx - tw / 2, cy, dayHeaderPaint);
        }
    }

    private void DrawDayGrid(IDrawingContext ctx, float x, float y)
    {
        var firstOfMonth = _displayMonth;
        int startDayOfWeek = (int)firstOfMonth.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);

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
                int dayOffset = week * DaysPerWeek + dow - startDayOfWeek;
                var date = firstOfMonth.AddDays(dayOffset);

                float cellX = x + dow * DayCellSize;
                float cellY = y + week * DayCellSize;
                var cellRect = new GridRect(cellX, cellY, DayCellSize, DayCellSize);

                bool isCurrentMonth = date.Month == firstOfMonth.Month;
                bool isToday = date.Date == today;
                bool isSelected = date.Date == _selectedDate.Date;
                bool isRangeEnd = IsRangeMode && _rangeEndDate.HasValue && date.Date == _rangeEndDate.Value.Date;
                bool isInRange = rangeStart.HasValue && rangeEnd.HasValue &&
                                 date.Date >= rangeStart.Value.Date && date.Date <= rangeEnd.Value.Date;
                bool isWeekend = dow == 0 || dow == 6;
                bool isDisabled = (MinDate.HasValue && date < MinDate.Value) ||
                                  (MaxDate.HasValue && date > MaxDate.Value);

                // Range background
                if (isInRange && !isSelected && !isRangeEnd)
                {
                    ctx.FillRect(cellRect, new GridPaint { Color = new GridColor(0, 120, 215, 25), Style = PaintStyle.Fill });
                }

                // Background highlights
                if (isSelected || isRangeEnd)
                {
                    ctx.FillRoundRect(cellRect, 4, new GridPaint { Color = SelectedDayColor, Style = PaintStyle.Fill });
                }
                else if (isToday)
                {
                    ctx.FillRoundRect(cellRect, 4, new GridPaint { Color = TodayHighlightColor, Style = PaintStyle.Fill });
                }

                // Day number text
                string dayText = date.Day.ToString();
                GridColor textColor;
                if (isSelected || isRangeEnd) textColor = SelectedDayTextColor;
                else if (isDisabled) textColor = new GridColor(200, 200, 200);
                else if (!isCurrentMonth) textColor = OtherMonthColor;
                else if (isWeekend) textColor = WeekendColor;
                else textColor = TextColor;

                var dayPaint = new GridPaint { Color = textColor, Style = PaintStyle.Fill, Font = DayFont };
                float tw = ctx.MeasureText(dayText, dayPaint).Width;
                float textX = cellX + (DayCellSize - tw) / 2;
                float textY = cellY + DayCellSize / 2;
                ctx.DrawText(dayText, textX, textY, dayPaint);
            }
        }
    }

    /// <summary>Get the bounding rect of the calendar popup.</summary>
    public GridRect GetCalendarBounds()
    {
        float calWidth = DaysPerWeek * DayCellSize + 8;
        float calHeight = NavRowHeight + HeaderHeight + MaxWeeks * DayCellSize + 8;
        if (ShowTimePicker) calHeight += NavRowHeight + 8;
        return new GridRect(Bounds.X, Bounds.Y + Bounds.Height + 2, calWidth, calHeight);
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
    public override bool OnPointerDown(GridPointerEventArgs e)
    {
        if (!IsEnabled) return false;

        _pressedDate = null;

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
                if (HandleYearDropdownClick(e.X, e.Y)) return true;
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
    public override bool OnPointerUp(GridPointerEventArgs e)
    {
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

    /// <summary>
    /// Hit-test which day cell is at the given pointer coordinates.
    /// Returns null if the pointer is not over a valid, selectable day.
    /// </summary>
    private DateTime? HitTestDayCell(float px, float py, GridRect calBounds)
    {
        float contentX = calBounds.X + 4;
        float contentY = calBounds.Y + 4 + NavRowHeight + HeaderHeight;

        float dayGridBottom = contentY + MaxWeeks * DayCellSize;
        if (py < contentY || py > dayGridBottom) return null;

        int col = (int)((px - contentX) / DayCellSize);
        int row = (int)((py - contentY) / DayCellSize);
        if (col < 0 || col >= DaysPerWeek || row < 0 || row >= MaxWeeks) return null;

        int startDayOfWeek = (int)_displayMonth.DayOfWeek;
        int dayOffset = row * DaysPerWeek + col - startDayOfWeek;
        var date = _displayMonth.AddDays(dayOffset);

        if (MinDate.HasValue && date < MinDate.Value) return null;
        if (MaxDate.HasValue && date > MaxDate.Value) return null;

        return date;
    }

    /// <summary>
    /// Handles the press phase on the calendar — navigates months/years immediately
    /// but only records the pressed day (selection is deferred to OnPointerUp).
    /// </summary>
    private void HandleCalendarPress(float clickX, float clickY, GridRect calBounds)
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
                // Click on month/year text — determine which one
                float midX = contentX + (calBounds.Width - 8) / 2;
                if (clickX < midX)
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
                    // Scroll to show current year
                    int currentYearIndex = _displayMonth.Year - (DateTime.Today.Year - YearRangeBefore);
                    _yearScrollOffset = Math.Max(0, currentYearIndex * DropdownItemHeight - YearDropdownMaxHeight / 2);
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

    /// <inheritdoc />
    public override bool OnKeyDown(GridKeyEventArgs e)
    {
        if (!_isCalendarOpen)
        {
            if (e.Key == GridKey.Space || e.Key == GridKey.Enter || e.Key == GridKey.Down)
            {
                OpenCalendar();
                return true;
            }
            return false;
        }

        switch (e.Key)
        {
            case GridKey.Left:
                SelectedDate = _selectedDate.AddDays(-1);
                return true;
            case GridKey.Right:
                SelectedDate = _selectedDate.AddDays(1);
                return true;
            case GridKey.Up:
                SelectedDate = _selectedDate.AddDays(-7);
                return true;
            case GridKey.Down:
                SelectedDate = _selectedDate.AddDays(7);
                return true;
            case GridKey.Enter:
                CloseCalendar();
                return true;
            case GridKey.Escape:
                CloseCalendar();
                return true;
        }
        return false;
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

    /// <summary>Navigate the calendar to the previous month.</summary>
    public void PreviousMonth()
    {
        _displayMonth = _displayMonth.AddMonths(-1);
        InvalidateVisual();
    }

    /// <summary>Navigate the calendar to the next month.</summary>
    public void NextMonth()
    {
        _displayMonth = _displayMonth.AddMonths(1);
        InvalidateVisual();
    }

    // ── Month dropdown ──────────────────────────────────────────

    private void DrawMonthDropdown(IDrawingContext ctx, GridRect calRect)
    {
        float dropX = calRect.X + (calRect.Width - MonthDropdownWidth) / 2 - 20;
        float dropY = calRect.Y + 4 + NavRowHeight;
        float dropH = 12 * DropdownItemHeight;

        var dropRect = new GridRect(dropX, dropY, MonthDropdownWidth, dropH);

        ctx.FillRect(new GridRect(dropX + 2, dropY + 2, MonthDropdownWidth, dropH),
            new GridPaint { Color = new GridColor(0, 0, 0, 30), Style = PaintStyle.Fill });
        ctx.FillRect(dropRect, new GridPaint { Color = CalendarBackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(dropRect, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

        for (int m = 0; m < 12; m++)
        {
            float itemY = dropY + m * DropdownItemHeight;
            var itemRect = new GridRect(dropX, itemY, MonthDropdownWidth, DropdownItemHeight);

            if (m == _highlightedMonthIndex)
                ctx.FillRect(itemRect, new GridPaint { Color = new GridColor(0, 120, 215, 40), Style = PaintStyle.Fill });

            if (m == _displayMonth.Month - 1)
                ctx.FillRect(itemRect, new GridPaint { Color = new GridColor(0, 120, 215, 20), Style = PaintStyle.Fill });

            string monthName = new DateTime(2000, m + 1, 1).ToString("MMMM");
            ctx.DrawText(monthName, dropX + 8, itemY + DropdownItemHeight / 2,
                new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = DayFont });
        }
    }

    private bool HandleMonthDropdownClick(float clickX, float clickY)
    {
        var calBounds = GetCalendarBounds();
        float dropX = calBounds.X + (calBounds.Width - MonthDropdownWidth) / 2 - 20;
        float dropY = calBounds.Y + 4 + NavRowHeight;
        float dropH = 12 * DropdownItemHeight;

        var dropRect = new GridRect(dropX, dropY, MonthDropdownWidth, dropH);
        if (!dropRect.Contains(clickX, clickY)) return false;

        int monthIndex = (int)((clickY - dropY) / DropdownItemHeight);
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

    private void DrawYearDropdown(IDrawingContext ctx, GridRect calRect)
    {
        float dropX = calRect.X + (calRect.Width - YearDropdownWidth) / 2 + 20;
        float dropY = calRect.Y + 4 + NavRowHeight;
        int totalYears = YearRangeBefore + YearRangeAfter + 1;
        float dropH = Math.Min(totalYears * DropdownItemHeight, YearDropdownMaxHeight);

        var dropRect = new GridRect(dropX, dropY, YearDropdownWidth, dropH);

        ctx.FillRect(new GridRect(dropX + 2, dropY + 2, YearDropdownWidth, dropH),
            new GridPaint { Color = new GridColor(0, 0, 0, 30), Style = PaintStyle.Fill });
        ctx.FillRect(dropRect, new GridPaint { Color = CalendarBackgroundColor, Style = PaintStyle.Fill });
        ctx.DrawRect(dropRect, new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 1 });

        ctx.Save();
        ctx.ClipRect(dropRect);

        int startYear = DateTime.Today.Year - YearRangeBefore;
        for (int i = 0; i < totalYears; i++)
        {
            float itemY = dropY + i * DropdownItemHeight - _yearScrollOffset;
            if (itemY + DropdownItemHeight < dropY) continue;
            if (itemY > dropY + dropH) break;

            int year = startYear + i;
            var itemRect = new GridRect(dropX, itemY, YearDropdownWidth, DropdownItemHeight);

            if (i == _highlightedYearIndex)
                ctx.FillRect(itemRect, new GridPaint { Color = new GridColor(0, 120, 215, 40), Style = PaintStyle.Fill });

            if (year == _displayMonth.Year)
                ctx.FillRect(itemRect, new GridPaint { Color = new GridColor(0, 120, 215, 20), Style = PaintStyle.Fill });

            ctx.DrawText(year.ToString(), dropX + 8, itemY + DropdownItemHeight / 2,
                new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = DayFont });
        }

        ctx.Restore();
    }

    private bool HandleYearDropdownClick(float clickX, float clickY)
    {
        var calBounds = GetCalendarBounds();
        float dropX = calBounds.X + (calBounds.Width - YearDropdownWidth) / 2 + 20;
        float dropY = calBounds.Y + 4 + NavRowHeight;
        int totalYears = YearRangeBefore + YearRangeAfter + 1;
        float dropH = Math.Min(totalYears * DropdownItemHeight, YearDropdownMaxHeight);

        var dropRect = new GridRect(dropX, dropY, YearDropdownWidth, dropH);
        if (!dropRect.Contains(clickX, clickY)) return false;

        int index = (int)((clickY - dropY + _yearScrollOffset) / DropdownItemHeight);
        int startYear = DateTime.Today.Year - YearRangeBefore;
        int year = startYear + index;

        if (index >= 0 && index < totalYears)
        {
            _displayMonth = new DateTime(year, _displayMonth.Month, 1);
            _isYearDropdownOpen = false;
            InvalidateVisual();
            return true;
        }
        return false;
    }

    // ── Time picker ─────────────────────────────────────────────

    private void DrawTimePicker(IDrawingContext ctx, float x, float y, float width)
    {
        // Separator line
        ctx.DrawLine(x, y - 2, x + width, y - 2,
            new GridPaint { Color = BorderColor, Style = PaintStyle.Stroke, StrokeWidth = 0.5f });

        var textPaint = new GridPaint { Color = TextColor, Style = PaintStyle.Fill, Font = Font };
        float centerX = x + width / 2;
        float centerY = y + NavRowHeight / 2;

        // Format time display
        int displayHour = SelectedHour;
        string amPm = "";
        if (!Use24HourFormat)
        {
            amPm = displayHour >= 12 ? " PM" : " AM";
            displayHour = displayHour % 12;
            if (displayHour == 0) displayHour = 12;
        }

        string timeText = $"{displayHour:D2}:{SelectedMinute:D2}{amPm}";
        float textWidth = ctx.MeasureText(timeText, textPaint).Width;

        // Draw time text centered
        ctx.DrawText(timeText, centerX - textWidth / 2, centerY, textPaint);

        // Draw up/down arrows for hour
        float hourX = centerX - textWidth / 2;
        var arrowPaint = new GridPaint { Color = ArrowColor, Style = PaintStyle.Stroke, StrokeWidth = 1.2f };

        // Hour up
        ctx.DrawLine(hourX + 8, y + 2, hourX + 4, y + 6, arrowPaint);
        ctx.DrawLine(hourX + 4, y + 6, hourX, y + 2, arrowPaint);
        // Hour down
        ctx.DrawLine(hourX, y + NavRowHeight - 2, hourX + 4, y + NavRowHeight - 6, arrowPaint);
        ctx.DrawLine(hourX + 4, y + NavRowHeight - 6, hourX + 8, y + NavRowHeight - 2, arrowPaint);

        // Minute arrows
        float minX = centerX + 4;
        // Minute up
        ctx.DrawLine(minX + 8, y + 2, minX + 4, y + 6, arrowPaint);
        ctx.DrawLine(minX + 4, y + 6, minX, y + 2, arrowPaint);
        // Minute down
        ctx.DrawLine(minX, y + NavRowHeight - 2, minX + 4, y + NavRowHeight - 6, arrowPaint);
        ctx.DrawLine(minX + 4, y + NavRowHeight - 6, minX + 8, y + NavRowHeight - 2, arrowPaint);
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
            TimeChanged?.Invoke(this, new TimeChangedEventArgs(oldHour, oldMinute, SelectedHour, SelectedMinute));
            InvalidateVisual();
        }
    }

    protected override void OnLostFocus()
    {
        CloseCalendar();
        base.OnLostFocus();
    }
}

/// <summary>Date selected event args.</summary>
public class DateSelectedEventArgs : EventArgs
{
    /// <summary>The previously selected date.</summary>
    public DateTime OldDate { get; }
    /// <summary>The newly selected date.</summary>
    public DateTime NewDate { get; }
    /// <summary>Initializes a new instance of the <see cref="DateSelectedEventArgs"/> class.</summary>
    /// <param name="oldDate">The previously selected date.</param>
    /// <param name="newDate">The newly selected date.</param>
    public DateSelectedEventArgs(DateTime oldDate, DateTime newDate) { OldDate = oldDate; NewDate = newDate; }
}

/// <summary>Date range selected event args.</summary>
public class DateRangeSelectedEventArgs : EventArgs
{
    /// <summary>The start date of the selected range.</summary>
    public DateTime StartDate { get; }
    /// <summary>The end date of the selected range, or <see langword="null"/> if only the start was chosen.</summary>
    public DateTime? EndDate { get; }
    /// <summary>Initializes a new instance of the <see cref="DateRangeSelectedEventArgs"/> class.</summary>
    /// <param name="start">The start date of the range.</param>
    /// <param name="end">The end date of the range.</param>
    public DateRangeSelectedEventArgs(DateTime start, DateTime? end) { StartDate = start; EndDate = end; }
}

/// <summary>Time changed event args.</summary>
public class TimeChangedEventArgs : EventArgs
{
    /// <summary>The hour value before the change (0–23).</summary>
    public int OldHour { get; }
    /// <summary>The minute value before the change (0–59).</summary>
    public int OldMinute { get; }
    /// <summary>The hour value after the change (0–23).</summary>
    public int NewHour { get; }
    /// <summary>The minute value after the change (0–59).</summary>
    public int NewMinute { get; }
    /// <summary>Initializes a new instance of the <see cref="TimeChangedEventArgs"/> class.</summary>
    /// <param name="oldH">The previous hour.</param>
    /// <param name="oldM">The previous minute.</param>
    /// <param name="newH">The new hour.</param>
    /// <param name="newM">The new minute.</param>
    public TimeChangedEventArgs(int oldH, int oldM, int newH, int newM)
    { OldHour = oldH; OldMinute = oldM; NewHour = newH; NewMinute = newM; }
}
