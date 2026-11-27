using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>
/// The filter popup's condition filters (#325): a Values | Conditions switch under the sort rows, and on the
/// Conditions page two rows of an operator drop-down and a value editor chosen by the column type
/// (<see cref="FilterConditionTypes"/>), joined by And or Or. The drop-downs and the calendar open inside the popup's
/// drawing; a press outside them, or Escape, closes only them.
/// </summary>
public sealed partial class DrawnFilterPopup : INestedPopupHost
{
    /// <summary>A place the keyboard can be in the popup; Tab and Shift+Tab move through those the page shows.</summary>
    internal enum FocusStop { ModeSwitch, Search, Operator1, Value1, Combination, Operator2, Value2, Ok, Cancel, Clear }

    private const float ModeSwitchHeight = 26f;
    private const float ConditionRowHeight = 28f;
    private const float CombinationRowHeight = 24f;
    private const float MessageHeight = 16f;
    private const float ConditionsBodyHeight =
        ConditionRowHeight + 4 + ConditionRowHeight + 6 + CombinationRowHeight + 6 + ConditionRowHeight + 4 + ConditionRowHeight + MessageHeight;

    private readonly FilterConditionKind _kind;
    private readonly DrawnComboBox[] _operators = new DrawnComboBox[2];
    private readonly DrawnTextBox[] _texts = new DrawnTextBox[2];
    private readonly DrawnDatePicker[] _dates = new DrawnDatePicker[2];
    private KumikoFilterPopupMode _mode;
    private FilterCombination _combination = FilterCombination.And;
    private FocusStop _stop = FocusStop.Search;
    private bool _conditionsEdited;
    private bool _loadingConditions;
    private string? _conditionError;
    private int _errorRow = -1;

    // The child whose press is in progress (a press captured by an operator drop-down, a value box or a calendar)
    private DrawnComponent? _pressChild;

    /// <summary>The conditions page's rectangles, in viewport coordinates.</summary>
    private readonly record struct ConditionsLayout(
        KumikoRect Operator1, KumikoRect Value1, KumikoRect And, KumikoRect Or, KumikoRect Operator2, KumikoRect Value2,
        KumikoRect Message);

    /// <summary>
    /// The page the popup shows. It opens on <see cref="KumikoFilterPopupMode.Conditions"/> when the column's filter has a
    /// condition, else on <see cref="KumikoFilterPopupMode.Values"/>. A column without condition filters (Boolean, Image and
    /// Template columns, or <see cref="KumikoColumn.AllowConditionFilter"/> false) stays on Values whatever is set.
    /// </summary>
    /// <remarks>
    /// OK applies both pages: the checked values and the conditions, combined with And. A page the user did not change keeps
    /// what the filter had, so OK on Values keeps the conditions and OK on Conditions keeps the checked values. A page that
    /// holds criteria while the other one shows has a dot on its tab.
    /// </remarks>
    public KumikoFilterPopupMode Mode
    {
        get => _mode;
        set => SetMode(value, announce: false);
    }

    /// <summary>Whether the column has a Conditions page.</summary>
    internal bool HasConditionPage => _kind != FilterConditionKind.None;

    /// <summary>The condition editor of the column (for tests).</summary>
    internal FilterConditionKind ConditionKind => _kind;

    /// <summary>Where the keyboard is.</summary>
    internal FocusStop CurrentStop => _stop;

    /// <summary>The operator drop-down of condition <paramref name="row"/> (0 or 1).</summary>
    internal DrawnComboBox OperatorBox(int row) => _operators[row];

    /// <summary>The value text box of condition <paramref name="row"/> (text and number columns).</summary>
    internal DrawnTextBox ValueTextBox(int row) => _texts[row];

    /// <summary>The value date picker of condition <paramref name="row"/> (date columns).</summary>
    internal DrawnDatePicker ValueDatePicker(int row) => _dates[row];

    /// <summary>How the two conditions combine.</summary>
    internal FilterCombination Combination => _combination;

    /// <summary>The message shown for a value that does not parse, or null.</summary>
    internal string? ConditionError => _conditionError;

    /// <summary>The text input with the keyboard: the search box on Values, a focused value text box on Conditions, else null.</summary>
    internal ITextInputSession? ActiveTextInput => _mode == KumikoFilterPopupMode.Values
        ? _search
        : _stop switch
        {
            FocusStop.Value1 when UsesTextBox => _texts[0],
            FocusStop.Value2 when UsesTextBox => _texts[1],
            _ => null,
        };

    /// <summary>Raised with a text for a screen reader: the page, the operator or the place the keyboard moved to.</summary>
    internal event Action<string>? AnnouncementRequested;

    /// <summary>Raised when a value text box copies or cuts text: the host takes it.</summary>
    internal event EventHandler<ClipboardTextEventArgs>? ValueCopyRequested;

    /// <summary>Raised when a value text box pastes: the host supplies the text.</summary>
    internal event EventHandler<ClipboardRequestEventArgs>? ValuePasteRequested;

    /// <summary>Pastes <paramref name="text"/> into the focused value text box.</summary>
    internal void PasteValueText(string? text)
    {
        if (ActiveTextInput is DrawnTextBox box && box != _search) box.PasteText(text);
    }

    private bool UsesTextBox => _kind is FilterConditionKind.Text or FilterConditionKind.Number;

    private KumikoStrings Strings => _dataSource.Strings;

    // ── Setup ────────────────────────────────────────────────────

    private void InitConditions()
    {
        for (int row = 0; row < 2; row++)
        {
            int r = row;
            var op = new DrawnComboBox { ItemHeight = 24f, Font = SmallFont };
            op.ValueChanged += (_, _) => OnOperatorChanged(r);
            op.RedrawRequested += (_, _) => InvalidateVisual();
            _operators[row] = op;

            var text = new DrawnTextBox
            {
                Font = SmallFont,
                Padding = 6f,
                InitialSelectionMode = EditTextSelectionMode.CursorAtEnd,
            };
            text.ValueChanged += (_, _) => OnValueEdited(r);
            text.RedrawRequested += (_, _) => InvalidateVisual();
            text.CopyRequested += (s, e) => ValueCopyRequested?.Invoke(s, e);
            text.PasteRequested += (s, e) => ValuePasteRequested?.Invoke(s, e);
            _texts[row] = text;

            var date = new DrawnDatePicker { Font = SmallFont, DayFont = SmallFont, Culture = _dataSource.Comparer.Culture };
            date.SelectedDate = null;
            date.ValueChanged += (_, _) => OnValueEdited(r);
            date.RedrawRequested += (_, _) => InvalidateVisual();
            _dates[row] = date;
        }

        if (_kind == FilterConditionKind.None) return;

        var items = new List<DrawnComboBoxItem>();
        foreach (var op in FilterConditionTypes.Operators(_kind))
            items.Add(new DrawnComboBoxItem(Strings.FilterOperatorLabel(op), op));
        _operators[0].SetItems(items);
        _operators[1].SetItems(items);
        LoadConditions();

        if (_column.ActiveFilter?.Condition1 != null)
        {
            _mode = KumikoFilterPopupMode.Conditions;
            _stop = FocusStop.Operator1;
        }
    }

    /// <summary>Shows the column's conditions: the first in row 1, the second in row 2, each with the default operator when absent.</summary>
    private void LoadConditions()
    {
        _loadingConditions = true;
        var filter = _column.ActiveFilter;
        LoadRow(0, filter?.Condition1);
        LoadRow(1, filter?.Condition1 != null ? filter.Condition2 : null);
        _combination = filter?.Combination ?? FilterCombination.And;
        _loadingConditions = false;
    }

    private void LoadRow(int row, FilterCondition? condition)
    {
        var operators = FilterConditionTypes.Operators(_kind);
        object? op = condition != null ? FilterConditionTypes.OperatorOf(_kind, condition) : null;
        int index = 0;
        for (int i = 0; i < operators.Count; i++)
            if (Equals(operators[i], op)) index = i;
        _operators[row].SelectedIndex = index;

        object? value = condition?.Value;
        var culture = _dataSource.Comparer.Culture;
        switch (_kind)
        {
            case FilterConditionKind.Text:
                _texts[row].Text = value == null ? string.Empty : _dataSource.Comparer.Format(value);
                break;
            case FilterConditionKind.Number:
                _texts[row].Text = value == null ? string.Empty
                    : KumikoComparer.TryGetNumber(value, out _) && value is IFormattable number ? number.ToString(null, culture)
                    : Convert.ToString(value, culture) ?? string.Empty;
                break;
            case FilterConditionKind.Date:
                _dates[row].SelectedDate = value switch
                {
                    DateTime dt => dt,
                    DateTimeOffset dto => dto.DateTime,
                    DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                    string s when DateTime.TryParse(s, culture, DateTimeStyles.None, out var parsed) => parsed,
                    _ => null,
                };
                break;
        }
    }

    // ── Changes ──────────────────────────────────────────────────

    private void OnOperatorChanged(int row)
    {
        if (_loadingConditions) return;
        _conditionsEdited = true;
        if (_errorRow == row && !FilterConditionTypes.NeedsValue(_operators[row].SelectedValue)) ClearConditionError();
        Announce(_operators[row].SelectedText);
        InvalidateVisual();
    }

    private void OnValueEdited(int row)
    {
        if (_loadingConditions) return;
        _conditionsEdited = true;
        if (_errorRow == row) ClearConditionError();
        InvalidateVisual();
    }

    private void SetCombination(FilterCombination combination)
    {
        if (_combination == combination) return;
        _combination = combination;
        _conditionsEdited = true;
        Announce(combination == FilterCombination.And ? Strings.FilterAnd : Strings.FilterOr);
        InvalidateVisual();
    }

    private void SetMode(KumikoFilterPopupMode mode, bool announce)
    {
        if (!HasConditionPage) mode = KumikoFilterPopupMode.Values;
        if (_mode == mode) return;
        CloseNestedPopup();
        _mode = mode;
        _pressTarget = Target.None;
        _hoveredItemIndex = -1;
        if (_stop != FocusStop.ModeSwitch)
            _stop = mode == KumikoFilterPopupMode.Values ? FocusStop.Search : FocusStop.Operator1;
        ApplyFocus();
        Bounds = new KumikoRect(Bounds.X, Bounds.Y, PopupWidth, ComputePopupHeight());
        if (announce) Announce(mode == KumikoFilterPopupMode.Values ? Strings.FilterValuesTab : Strings.FilterConditionsTab);
        InvalidateVisual();
    }

    private void Announce(string text)
    {
        if (!string.IsNullOrEmpty(text)) AnnouncementRequested?.Invoke(text);
    }

    // ── Reading the conditions ───────────────────────────────────

    /// <summary>
    /// The conditions on the page, in order (a blank row is skipped, so a filled second row alone becomes the first
    /// condition). False, with the message shown and the keyboard on the value, when a value does not parse.
    /// </summary>
    private bool TryReadConditions(out FilterCondition? first, out FilterCondition? second)
    {
        first = second = null;
        ClearConditionError();
        for (int row = 0; row < 2; row++)
        {
            if (!TryReadRow(row, out var condition))
                return false;
            if (condition == null) continue;
            if (first == null) first = condition;
            else second = condition;
        }
        return true;
    }

    private bool TryReadRow(int row, out FilterCondition? condition)
    {
        condition = null;
        if (_operators[row].SelectedValue is not { } op) return true;
        if (!FilterConditionTypes.NeedsValue(op))
        {
            condition = FilterConditionTypes.Create(op, null);
            return true;
        }

        switch (_kind)
        {
            case FilterConditionKind.Text:
                string text = _texts[row].Text;
                if (text.Length > 0) condition = FilterConditionTypes.Create(op, text);
                return true;

            case FilterConditionKind.Number:
                string number = _texts[row].Text.Trim();
                if (number.Length == 0) return true;
                if (!double.TryParse(number, NumberStyles.Float | NumberStyles.AllowThousands, _dataSource.Comparer.Culture, out double d))
                    return Fail(row, Strings.EnterNumber);
                condition = FilterConditionTypes.Create(op, d);
                return true;

            case FilterConditionKind.Date:
                object? value = ((IValueEditor)_dates[row]).Value;
                if (value == null) return true;
                if (value is not DateTime date) return Fail(row, Strings.EnterDate);
                condition = FilterConditionTypes.Create(op, date.Date);
                return true;
        }
        return true;
    }

    private bool Fail(int row, string message)
    {
        _conditionError = message;
        _errorRow = row;
        if (UsesTextBox) _texts[row].Validation = ValidationState.Error;
        SetMode(KumikoFilterPopupMode.Conditions, announce: false);
        SetStop(row == 0 ? FocusStop.Value1 : FocusStop.Value2, announce: false);
        Announce(message);
        InvalidateVisual();
        return false;
    }

    private void ClearConditionError()
    {
        if (_errorRow >= 0) _texts[_errorRow].Validation = ValidationState.None;
        _conditionError = null;
        _errorRow = -1;
    }

    /// <summary>Whether condition <paramref name="row"/> holds criteria: an operator without a value, or a value.</summary>
    private bool RowHasCriteria(int row)
    {
        if (_operators[row].SelectedValue is not { } op) return false;
        if (!FilterConditionTypes.NeedsValue(op)) return true;
        return _kind == FilterConditionKind.Date
            ? ((IValueEditor)_dates[row]).HasValue
            : _texts[row].Text.Length > 0;
    }

    /// <summary>Whether the Conditions page holds criteria (its tab shows a dot while Values shows).</summary>
    internal bool ConditionsHaveCriteria => HasConditionPage && (RowHasCriteria(0) || RowHasCriteria(1));

    /// <summary>Whether the Values page holds criteria: a value is unchecked (its tab shows a dot while Conditions shows).</summary>
    internal bool ValuesHaveCriteria
    {
        get
        {
            foreach (var item in _allItems)
                if (!item.IsChecked) return true;
            return false;
        }
    }

    // ── Layout ───────────────────────────────────────────────────

    private static ConditionsLayout LayoutConditions(float x, float y, float width)
    {
        var op1 = new KumikoRect(x, y, width, ConditionRowHeight);
        y += ConditionRowHeight + 4;
        var value1 = new KumikoRect(x, y, width, ConditionRowHeight);
        y += ConditionRowHeight + 6;
        float half = width / 2;
        var and = new KumikoRect(x, y, half - 2, CombinationRowHeight);
        var or = new KumikoRect(x + half + 2, y, half - 2, CombinationRowHeight);
        y += CombinationRowHeight + 6;
        var op2 = new KumikoRect(x, y, width, ConditionRowHeight);
        y += ConditionRowHeight + 4;
        var value2 = new KumikoRect(x, y, width, ConditionRowHeight);
        y += ConditionRowHeight;
        var message = new KumikoRect(x, y, width, MessageHeight);
        return new ConditionsLayout(op1, value1, and, or, op2, value2, message);
    }

    /// <summary>Puts the condition editors where the layout says (setting the same bounds again does nothing).</summary>
    private void PlaceConditionEditors(in PopupLayout layout)
    {
        if (_mode != KumikoFilterPopupMode.Conditions) return;
        var c = layout.Conditions;
        Place(0, c.Operator1, c.Value1);
        Place(1, c.Operator2, c.Value2);
    }

    private void Place(int row, KumikoRect op, KumikoRect value)
    {
        _operators[row].Bounds = op;
        _operators[row].PopupViewport = PopupViewport;
        _texts[row].Bounds = value;
        _dates[row].Bounds = value;
        _dates[row].PopupViewport = PopupViewport;
    }

    private DrawnComponent ValueEditor(int row) =>
        _kind == FilterConditionKind.Date ? _dates[row] : _texts[row];

    private bool ShowsValue(int row) => FilterConditionTypes.NeedsValue(_operators[row].SelectedValue);

    /// <summary>The operator drop-down or calendar that is open, or null.</summary>
    private DrawnComponent? OpenChild
    {
        get
        {
            if (_mode != KumikoFilterPopupMode.Conditions) return null;
            for (int row = 0; row < 2; row++)
            {
                if (_operators[row].IsDropdownOpen) return _operators[row];
                if (_kind == FilterConditionKind.Date && _dates[row].IsCalendarOpen) return _dates[row];
            }
            return null;
        }
    }

    /// <inheritdoc />
    bool INestedPopupHost.CloseNestedPopup() => CloseNestedPopup();

    /// <summary>Closes the open operator drop-down or calendar; false when none is open.</summary>
    private bool CloseNestedPopup()
    {
        switch (OpenChild)
        {
            case DrawnComboBox combo:
                combo.CloseDropdown();
                break;
            case DrawnDatePicker picker:
                picker.CloseCalendar();
                break;
            default:
                return false;
        }
        _pressChild = null;
        InvalidateVisual();
        return true;
    }

    // ── Drawing ──────────────────────────────────────────────────

    private void DrawModeSwitch(IDrawingContext ctx, in PopupLayout layout)
    {
        bool values = _mode == KumikoFilterPopupMode.Values;
        DrawSegment(ctx, layout.ValuesTab, Strings.FilterValuesTab, values, !values && ValuesHaveCriteria);
        DrawSegment(ctx, layout.ConditionsTab, Strings.FilterConditionsTab, !values, values && ConditionsHaveCriteria);
        if (IsFocused && _stop == FocusStop.ModeSwitch)
            DrawFocusRing(ctx, Join(layout.ValuesTab, layout.ConditionsTab));
    }

    private void DrawSegment(IDrawingContext ctx, KumikoRect rect, string text, bool selected, bool marker)
    {
        if (selected)
            ctx.FillRoundRect(rect, 3, Paints.Get(color: ButtonColor));
        else
            ctx.DrawRoundRect(rect, 3, Paints.Get(color: BorderColor, strokeWidth: 1));
        ctx.DrawTextInRect(text, rect,
            Paints.Get(color: selected ? ButtonTextColor : TextColor, font: SmallFont, isAntiAlias: true),
            KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
        if (marker)
            ctx.FillRoundRect(new KumikoRect(rect.Right - 9, rect.Y + 4, 5, 5), 2.5f, Paints.Get(color: AccentColor, isAntiAlias: true));
    }

    /// <summary>The rectangle from the left of <paramref name="a"/> to the right of <paramref name="b"/> (two segments side by side).</summary>
    private static KumikoRect Join(KumikoRect a, KumikoRect b) =>
        new(a.X, Math.Min(a.Y, b.Y), b.Right - a.X, Math.Max(a.Bottom, b.Bottom) - Math.Min(a.Y, b.Y));

    private void DrawFocusRing(IDrawingContext ctx, KumikoRect rect) =>
        ctx.DrawRoundRect(new KumikoRect(rect.X - 1.5f, rect.Y - 1.5f, rect.Width + 3, rect.Height + 3), 4,
            Paints.Get(color: KeyboardFocusColor, strokeWidth: 1.5f, isAntiAlias: true));

    private void DrawConditions(IDrawingContext ctx, in PopupLayout layout)
    {
        var c = layout.Conditions;
        var open = OpenChild;
        DrawConditionRow(ctx, 0, c.Value1, open);
        DrawSegment(ctx, c.And, Strings.FilterAnd, _combination == FilterCombination.And, marker: false);
        DrawSegment(ctx, c.Or, Strings.FilterOr, _combination == FilterCombination.Or, marker: false);
        if (IsFocused && _stop == FocusStop.Combination)
            DrawFocusRing(ctx, Join(c.And, c.Or));
        DrawConditionRow(ctx, 1, c.Value2, open);

        if (_conditionError is { } error)
            ctx.DrawTextInRect(error, c.Message, Paints.Get(color: ErrorColor, font: SmallFont, isAntiAlias: true),
                KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
    }

    private void DrawConditionRow(IDrawingContext ctx, int row, KumikoRect valueRect, DrawnComponent? open)
    {
        var op = _operators[row];
        StyleOperator(op);
        if (op != open) op.OnDraw(ctx);
        if (!ShowsValue(row)) return;

        if (_kind == FilterConditionKind.Date)
        {
            var picker = _dates[row];
            if (picker != open) picker.OnDraw(ctx);
            if (!((IValueEditor)picker).HasValue)
                ctx.DrawTextInRect(Strings.FilterValuePlaceholder, new KumikoRect(valueRect.X + 7, valueRect.Y, valueRect.Width - 12, valueRect.Height),
                    Paints.Get(color: PlaceholderColor, font: SmallFont, isAntiAlias: true),
                    KumikoTextAlignment.Left, KumikoVerticalAlignment.Center);
            return;
        }

        var box = _texts[row];
        box.Font = SmallFont;
        box.TextColor = TextColor;
        box.CursorColor = CursorColor;
        box.BackgroundColor = SearchBackgroundColor;
        box.EditingBackgroundColor = SearchBackgroundColor;
        box.BorderColor = SearchBorderColor;
        box.FocusedBorderColor = SearchFocusBorderColor;
        box.ErrorBorderColor = ErrorColor;
        box.PlaceholderColor = PlaceholderColor;
        box.Placeholder = Strings.FilterValuePlaceholder;
        box.BorderWidth = 1f;
        box.OnDraw(ctx);
    }

    private void StyleOperator(DrawnComboBox op)
    {
        op.TextColor = TextColor;
        op.BackgroundColor = SearchBackgroundColor;
        op.DropdownBackgroundColor = BackgroundColor;
        op.BorderColor = SearchBorderColor;
        op.FocusedBorderColor = SearchFocusBorderColor;
        op.HighlightColor = HoverColor;
    }

    /// <summary>Applies the theme to the operator drop-downs and date pickers (the text boxes are styled as they draw).</summary>
    private void ApplyThemeToConditions(KumikoStyle style)
    {
        for (int row = 0; row < 2; row++)
        {
            _operators[row].ApplyTheme(style);
            _operators[row].ApplyFont(SmallFont);
            _dates[row].ApplyTheme(style);
            _dates[row].ApplyFont(SmallFont);
            _texts[row].SelectionHighlightColor = style.AccentColor.WithAlpha(80);
        }
    }

    // ── Pointer input ────────────────────────────────────────────

    private static bool IsChildTarget(Target target) =>
        target is Target.Operator1 or Target.Value1 or Target.Operator2 or Target.Value2;

    private DrawnComponent ChildOf(Target target) => target switch
    {
        Target.Operator1 => _operators[0],
        Target.Operator2 => _operators[1],
        Target.Value1 => ValueEditor(0),
        _ => ValueEditor(1),
    };

    private static FocusStop StopOf(Target target) => target switch
    {
        Target.Operator1 => FocusStop.Operator1,
        Target.Value1 => FocusStop.Value1,
        Target.Operator2 => FocusStop.Operator2,
        Target.Value2 => FocusStop.Value2,
        _ => FocusStop.Combination,
    };

    /// <summary>
    /// A press while a drop-down or calendar is open: on it, it goes to it; anywhere else it closes it and does nothing
    /// more (like a press outside a popup).
    /// </summary>
    private void PressWithOpenChild(DrawnComponent open, KumikoPointerEventArgs e)
    {
        if (open.HitTest(e.X, e.Y))
        {
            _pressTarget = Target.Child;
            _pressChild = open;
            open.OnPointerDown(e);
        }
        else
        {
            CloseNestedPopup();
            _pressTarget = Target.None;
        }
    }

    /// <summary>A press on an operator drop-down or a value editor: the keyboard moves there and the editor takes the press.</summary>
    private void PressConditionEditor(Target target, KumikoPointerEventArgs e)
    {
        var child = ChildOf(target);
        SetStop(StopOf(target), announce: false);
        _pressTarget = Target.Child;
        _pressChild = child;
        child.OnPointerDown(e);
        if (child is DrawnTextBox) KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Keyboard ─────────────────────────────────────────────────

    /// <summary>The stops the page shows, in Tab order.</summary>
    private int Stops(Span<FocusStop> stops)
    {
        int n = 0;
        if (HasConditionPage) stops[n++] = FocusStop.ModeSwitch;
        if (_mode == KumikoFilterPopupMode.Values)
        {
            stops[n++] = FocusStop.Search;
        }
        else
        {
            stops[n++] = FocusStop.Operator1;
            if (ShowsValue(0)) stops[n++] = FocusStop.Value1;
            stops[n++] = FocusStop.Combination;
            stops[n++] = FocusStop.Operator2;
            if (ShowsValue(1)) stops[n++] = FocusStop.Value2;
        }
        stops[n++] = FocusStop.Ok;
        stops[n++] = FocusStop.Cancel;
        stops[n++] = FocusStop.Clear;
        return n;
    }

    /// <summary>Tab (+1) or Shift+Tab (-1): the next stop the page shows, wrapping.</summary>
    private void MoveStop(int direction)
    {
        Span<FocusStop> stops = stackalloc FocusStop[10];
        int n = Stops(stops);
        int at = -1;
        for (int i = 0; i < n; i++)
            if (stops[i] == _stop) at = i;
        int next = at < 0 ? (direction > 0 ? 0 : n - 1) : (at + direction + n) % n;
        SetStop(stops[next], announce: true);
    }

    /// <summary>Moves the keyboard to <paramref name="stop"/> and focuses its editor.</summary>
    internal void SetStop(FocusStop stop, bool announce)
    {
        if (_stop == stop) return;
        _search.CommitComposition();
        _texts[0].CommitComposition();
        _texts[1].CommitComposition();
        _stop = stop;
        if (stop != FocusStop.Search) _focusIndex = -1;
        ApplyFocus();
        if (ActiveTextInput != null) KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);
        if (announce) Announce(StopName(stop));
        InvalidateVisual();
    }

    /// <summary>Gives keyboard focus (the focused look, the caret) to the editor of the current stop, and takes it from the others.</summary>
    private void ApplyFocus()
    {
        bool focused = IsFocused;
        bool conditions = _mode == KumikoFilterPopupMode.Conditions;
        _search.IsFocused = focused && !conditions && _stop == FocusStop.Search;
        _operators[0].IsFocused = focused && conditions && _stop == FocusStop.Operator1;
        _operators[1].IsFocused = focused && conditions && _stop == FocusStop.Operator2;
        bool dates = _kind == FilterConditionKind.Date;
        _texts[0].IsFocused = focused && conditions && !dates && _stop == FocusStop.Value1;
        _texts[1].IsFocused = focused && conditions && !dates && _stop == FocusStop.Value2;
        _dates[0].IsFocused = focused && conditions && dates && _stop == FocusStop.Value1;
        _dates[1].IsFocused = focused && conditions && dates && _stop == FocusStop.Value2;
    }

    /// <summary>What a screen reader says for a stop.</summary>
    private string StopName(FocusStop stop) => stop switch
    {
        FocusStop.ModeSwitch => _mode == KumikoFilterPopupMode.Values ? Strings.FilterValuesTab : Strings.FilterConditionsTab,
        FocusStop.Search => _search.Text.Length > 0 ? _search.Text : Strings.FilterSearchPlaceholder,
        FocusStop.Operator1 => _operators[0].SelectedText,
        FocusStop.Operator2 => _operators[1].SelectedText,
        FocusStop.Value1 => ValueName(0),
        FocusStop.Value2 => ValueName(1),
        FocusStop.Combination => _combination == FilterCombination.And ? Strings.FilterAnd : Strings.FilterOr,
        FocusStop.Ok => Strings.FilterOk,
        FocusStop.Cancel => Strings.FilterCancel,
        _ => Strings.FilterClear,
    };

    private string ValueName(int row)
    {
        if (_kind == FilterConditionKind.Date)
            return _dates[row].SelectedDate is { } date ? date.ToString("d", _dataSource.Comparer.Culture) : Strings.FilterValuePlaceholder;
        return _texts[row].Text.Length > 0 ? _texts[row].Text : Strings.FilterValuePlaceholder;
    }

    /// <summary>A key on a stop that is not an editor: the mode switch, And/Or and the buttons. Null when the stop does not take it.</summary>
    private bool? HandleStopKey(KumikoKeyEventArgs e)
    {
        bool toggle = e.Key is KumikoKey.Left or KumikoKey.Right or KumikoKey.Space
                      || e.Key == KumikoKey.Character && e.Character == ' ' && string.IsNullOrEmpty(e.Text);
        switch (_stop)
        {
            case FocusStop.ModeSwitch when toggle:
                SetMode(e.Key == KumikoKey.Left ? KumikoFilterPopupMode.Values
                    : e.Key == KumikoKey.Right ? KumikoFilterPopupMode.Conditions
                    : _mode == KumikoFilterPopupMode.Values ? KumikoFilterPopupMode.Conditions : KumikoFilterPopupMode.Values,
                    announce: true);
                return true;

            case FocusStop.Combination when toggle:
                SetCombination(e.Key == KumikoKey.Left ? FilterCombination.And
                    : e.Key == KumikoKey.Right ? FilterCombination.Or
                    : _combination == FilterCombination.And ? FilterCombination.Or : FilterCombination.And);
                return true;

            case FocusStop.Combination:
                return IsPlainKey(e);

            case FocusStop.Ok or FocusStop.Cancel or FocusStop.Clear:
                if (e.Key is KumikoKey.Space || e.Key == KumikoKey.Character && e.Character == ' ')
                {
                    Activate(_stop);
                    return true;
                }
                if (e.Key is KumikoKey.Left or KumikoKey.Right)
                {
                    int delta = e.Key == KumikoKey.Right ? 1 : -1;
                    int button = Math.Clamp(_stop - FocusStop.Ok + delta, 0, 2);
                    SetStop(FocusStop.Ok + button, announce: true);
                    return true;
                }
                // Other typing on Values goes back to the search box
                if (_mode == KumikoFilterPopupMode.Values) return null;
                return IsPlainKey(e);
        }
        return null;
    }

    /// <summary>Enter, or Space on a button: OK applies, Cancel closes, Clear removes the filter.</summary>
    private void Activate(FocusStop stop)
    {
        switch (stop)
        {
            case FocusStop.Cancel: Cancel(); break;
            case FocusStop.Clear: ClearFilter(); break;
            default: ApplyFilter(); break;
        }
    }

    /// <summary>A key on the Conditions page's operator or value editor.</summary>
    private bool HandleConditionEditorKey(KumikoKeyEventArgs e)
    {
        DrawnComponent? editor = _stop switch
        {
            FocusStop.Operator1 => _operators[0],
            FocusStop.Operator2 => _operators[1],
            FocusStop.Value1 => ValueEditor(0),
            FocusStop.Value2 => ValueEditor(1),
            _ => null,
        };
        if (editor == null) return IsPlainKey(e);
        bool handled = editor.OnKeyDown(e);
        // A date being typed changes no value until it is applied, so the key itself counts as the edit
        if (handled && editor is DrawnDatePicker) OnValueEdited(_stop == FocusStop.Value1 ? 0 : 1);
        return handled || IsPlainKey(e);
    }

    /// <summary>
    /// A key without Ctrl, Cmd or Alt: the Conditions page keeps it even when its editor does nothing with it, so the
    /// grid under the popup does not move; a shortcut (Ctrl/Cmd+F) still reaches the grid.
    /// </summary>
    private static bool IsPlainKey(KumikoKeyEventArgs e) => !e.HasCommand && !e.HasAlt;

    /// <summary>The focused text box (the search box or a value box), or null.</summary>
    private DrawnTextBox? FocusedTextBox => ActiveTextInput as DrawnTextBox;

    /// <summary>The red of a value that does not parse.</summary>
    private static readonly KumikoColor ErrorColor = new(220, 50, 50);
}
