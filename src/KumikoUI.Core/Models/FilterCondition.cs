namespace KumikoUI.Core.Models;

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
    public bool Evaluate(object? cellValue, GridColumnType columnType)
    {
        return columnType switch
        {
            GridColumnType.Text or GridColumnType.ComboBox or GridColumnType.Picker
                => EvaluateText(cellValue),
            GridColumnType.Numeric => EvaluateNumeric(cellValue),
            GridColumnType.Date => EvaluateDate(cellValue),
            GridColumnType.Boolean => EvaluateBoolean(cellValue),
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
