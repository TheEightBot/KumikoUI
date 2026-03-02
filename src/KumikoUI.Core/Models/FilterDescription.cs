namespace KumikoUI.Core.Models;

/// <summary>
/// Filter operator for text columns.
/// </summary>
public enum TextFilterOperator
{
    /// <summary>Text equals the filter value (case-insensitive).</summary>
    Equals,
    /// <summary>Text does not equal the filter value.</summary>
    NotEquals,
    /// <summary>Text contains the filter value.</summary>
    Contains,
    /// <summary>Text does not contain the filter value.</summary>
    NotContains,
    /// <summary>Text starts with the filter value.</summary>
    StartsWith,
    /// <summary>Text ends with the filter value.</summary>
    EndsWith,
    /// <summary>Text is null or empty.</summary>
    Empty,
    /// <summary>Text is not null or empty.</summary>
    NotEmpty
}

/// <summary>
/// Filter operator for numeric columns.
/// </summary>
public enum NumericFilterOperator
{
    /// <summary>Value equals the filter value.</summary>
    Equals,
    /// <summary>Value does not equal the filter value.</summary>
    NotEquals,
    /// <summary>Value is less than the filter value.</summary>
    LessThan,
    /// <summary>Value is less than or equal to the filter value.</summary>
    LessThanOrEqual,
    /// <summary>Value is greater than the filter value.</summary>
    GreaterThan,
    /// <summary>Value is greater than or equal to the filter value.</summary>
    GreaterThanOrEqual
}

/// <summary>
/// Filter operator for date columns.
/// </summary>
public enum DateFilterOperator
{
    /// <summary>Date equals the filter value.</summary>
    Equals,
    /// <summary>Date does not equal the filter value.</summary>
    NotEquals,
    /// <summary>Date is before the filter value.</summary>
    Before,
    /// <summary>Date is after the filter value.</summary>
    After,
    /// <summary>Date is on or before the filter value.</summary>
    BeforeOrEqual,
    /// <summary>Date is on or after the filter value.</summary>
    AfterOrEqual
}

/// <summary>
/// How to combine multiple filter conditions within a compound filter.
/// </summary>
public enum FilterCombination
{
    /// <summary>All conditions must match (logical AND).</summary>
    And,
    /// <summary>Any condition may match (logical OR).</summary>
    Or
}

/// <summary>
/// Describes a single filter condition for a column.
/// </summary>
public class FilterCondition
{
    /// <summary>The text filter operator (for Text columns).</summary>
    public TextFilterOperator TextOperator { get; set; } = TextFilterOperator.Contains;

    /// <summary>The numeric filter operator (for Numeric columns).</summary>
    public NumericFilterOperator NumericOperator { get; set; } = NumericFilterOperator.Equals;

    /// <summary>The date filter operator (for Date columns).</summary>
    public DateFilterOperator DateOperator { get; set; } = DateFilterOperator.Equals;

    /// <summary>The value to compare against (string, double, DateTime, bool).</summary>
    public object? Value { get; set; }

    /// <summary>
    /// Evaluate this condition against a cell value for a given column type.
    /// </summary>
    public bool Evaluate(object? cellValue, DataGridColumnType columnType)
    {
        return columnType switch
        {
            DataGridColumnType.Text or DataGridColumnType.ComboBox or DataGridColumnType.Picker
                => EvaluateText(cellValue),
            DataGridColumnType.Numeric => EvaluateNumeric(cellValue),
            DataGridColumnType.Date => EvaluateDate(cellValue),
            DataGridColumnType.Boolean => EvaluateBoolean(cellValue),
            _ => true // Template/Image columns pass through
        };
    }

    private bool EvaluateText(object? cellValue)
    {
        string cellText = cellValue?.ToString() ?? string.Empty;
        string filterText = Value?.ToString() ?? string.Empty;

        return TextOperator switch
        {
            TextFilterOperator.Equals => cellText.Equals(filterText, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.NotEquals => !cellText.Equals(filterText, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.Contains => cellText.Contains(filterText, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.NotContains => !cellText.Contains(filterText, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.StartsWith => cellText.StartsWith(filterText, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.EndsWith => cellText.EndsWith(filterText, StringComparison.OrdinalIgnoreCase),
            TextFilterOperator.Empty => string.IsNullOrEmpty(cellText),
            TextFilterOperator.NotEmpty => !string.IsNullOrEmpty(cellText),
            _ => true
        };
    }

    private bool EvaluateNumeric(object? cellValue)
    {
        if (!TryConvertToDouble(cellValue, out double cell)) return false;
        if (!TryConvertToDouble(Value, out double filter)) return false;

        return NumericOperator switch
        {
            NumericFilterOperator.Equals => Math.Abs(cell - filter) < 1e-10,
            NumericFilterOperator.NotEquals => Math.Abs(cell - filter) >= 1e-10,
            NumericFilterOperator.LessThan => cell < filter,
            NumericFilterOperator.LessThanOrEqual => cell <= filter,
            NumericFilterOperator.GreaterThan => cell > filter,
            NumericFilterOperator.GreaterThanOrEqual => cell >= filter,
            _ => true
        };
    }

    private bool EvaluateDate(object? cellValue)
    {
        if (!TryConvertToDateTime(cellValue, out DateTime cell)) return false;
        if (!TryConvertToDateTime(Value, out DateTime filter)) return false;

        cell = cell.Date;
        filter = filter.Date;

        return DateOperator switch
        {
            DateFilterOperator.Equals => cell == filter,
            DateFilterOperator.NotEquals => cell != filter,
            DateFilterOperator.Before => cell < filter,
            DateFilterOperator.After => cell > filter,
            DateFilterOperator.BeforeOrEqual => cell <= filter,
            DateFilterOperator.AfterOrEqual => cell >= filter,
            _ => true
        };
    }

    private static bool EvaluateBoolean(object? cellValue)
    {
        // Boolean filters are handled via the value list (checked/unchecked),
        // not via operator. Always pass here.
        return true;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        if (value == null) return false;
        if (value is double d) { result = d; return true; }
        if (value is float f) { result = f; return true; }
        if (value is int i) { result = i; return true; }
        if (value is long l) { result = l; return true; }
        if (value is decimal m) { result = (double)m; return true; }
        if (value is short s) { result = s; return true; }
        if (value is byte b) { result = b; return true; }
        return double.TryParse(value.ToString(), out result);
    }

    private static bool TryConvertToDateTime(object? value, out DateTime result)
    {
        result = default;
        if (value == null) return false;
        if (value is DateTime dt) { result = dt; return true; }
        if (value is DateTimeOffset dto) { result = dto.DateTime; return true; }
        return DateTime.TryParse(value.ToString(), out result);
    }
}

/// <summary>
/// Complete filter description for a column.
/// Supports both a value list (Excel-style checkboxes) and
/// condition-based filtering (operator + value).
/// </summary>
public class FilterDescription
{
    /// <summary>The column this filter applies to.</summary>
    public DataGridColumn Column { get; }

    /// <summary>
    /// When non-null, items are filtered to only show rows whose value
    /// (as display text) is in this set. This is the Excel-style checkbox filter.
    /// Null means "no value-list filter" (show all).
    /// </summary>
    public HashSet<string>? SelectedValues { get; set; }

    /// <summary>
    /// Advanced condition-based filter. Null means no condition filter.
    /// </summary>
    public FilterCondition? Condition1 { get; set; }

    /// <summary>
    /// Second condition for compound filters. Null means single condition.
    /// </summary>
    public FilterCondition? Condition2 { get; set; }

    /// <summary>
    /// How to combine Condition1 and Condition2.
    /// </summary>
    public FilterCombination Combination { get; set; } = FilterCombination.And;

    /// <summary>Whether this filter has any active criteria.</summary>
    public bool IsActive =>
        SelectedValues != null || Condition1 != null;

    public FilterDescription(DataGridColumn column)
    {
        Column = column;
    }

    /// <summary>
    /// Evaluate this filter against a cell value and its display text.
    /// </summary>
    public bool Evaluate(object? cellValue, string displayText)
    {
        // Value list filter (Excel-style checkboxes)
        if (SelectedValues != null)
        {
            if (!SelectedValues.Contains(displayText))
                return false;
        }

        // Condition filter
        if (Condition1 != null)
        {
            bool result1 = Condition1.Evaluate(cellValue, Column.ColumnType);

            if (Condition2 != null)
            {
                bool result2 = Condition2.Evaluate(cellValue, Column.ColumnType);
                bool combined = Combination == FilterCombination.And
                    ? result1 && result2
                    : result1 || result2;
                if (!combined) return false;
            }
            else
            {
                if (!result1) return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Event args for FilterChanging. Set Cancel=true to prevent.
/// </summary>
public class FilterChangingEventArgs : EventArgs
{
    public DataGridColumn Column { get; }
    public FilterDescription? NewFilter { get; }
    public bool Cancel { get; set; }

    public FilterChangingEventArgs(DataGridColumn column, FilterDescription? newFilter)
    {
        Column = column;
        NewFilter = newFilter;
    }
}

/// <summary>
/// Event args for FilterChanged.
/// </summary>
public class FilterChangedEventArgs : EventArgs
{
    public DataGridColumn Column { get; }
    public IReadOnlyList<FilterDescription> ActiveFilters { get; }

    public FilterChangedEventArgs(DataGridColumn column, IReadOnlyList<FilterDescription> activeFilters)
    {
        Column = column;
        ActiveFilters = activeFilters;
    }
}
