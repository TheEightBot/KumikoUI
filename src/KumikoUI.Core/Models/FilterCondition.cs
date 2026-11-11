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
    /// Evaluate this condition against a cell value for a given column type, with the current culture and
    /// case-insensitive text rules (<see cref="KumikoComparer.Default"/>).
    /// </summary>
    public bool Evaluate(object? cellValue, KumikoColumnType columnType) =>
        Evaluate(cellValue, columnType, KumikoComparer.Default);

    /// <summary>Evaluate with a source's culture and text rules.</summary>
    internal bool Evaluate(object? cellValue, KumikoColumnType columnType, KumikoComparer comparer)
    {
        return columnType switch
        {
            KumikoColumnType.Text or KumikoColumnType.ComboBox or KumikoColumnType.Picker
                => EvaluateText(cellValue, comparer),
            KumikoColumnType.Numeric or KumikoColumnType.ProgressBar => EvaluateNumeric(cellValue, comparer),
            KumikoColumnType.Date => EvaluateDate(cellValue, comparer),
            KumikoColumnType.Boolean => EvaluateBoolean(cellValue),
            _ => true // Template/Image columns pass through
        };
    }

    private bool EvaluateText(object? cellValue, KumikoComparer comparer)
    {
        string cellText = comparer.Format(cellValue);
        string filterText = comparer.Format(Value);

        return TextOperator switch
        {
            TextFilterOperator.Equals => comparer.TextEquals(cellText, filterText),
            TextFilterOperator.NotEquals => !comparer.TextEquals(cellText, filterText),
            TextFilterOperator.Contains => comparer.TextContains(cellText, filterText),
            TextFilterOperator.NotContains => !comparer.TextContains(cellText, filterText),
            TextFilterOperator.StartsWith => comparer.TextStartsWith(cellText, filterText),
            TextFilterOperator.EndsWith => comparer.TextEndsWith(cellText, filterText),
            TextFilterOperator.Empty => string.IsNullOrEmpty(cellText),
            TextFilterOperator.NotEmpty => !string.IsNullOrEmpty(cellText),
            _ => true
        };
    }

    private bool EvaluateNumeric(object? cellValue, KumikoComparer comparer)
    {
        if (!TryConvertToDouble(cellValue, comparer, out double cell)) return false;
        if (!TryConvertToDouble(Value, comparer, out double filter)) return false;

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

    private bool EvaluateDate(object? cellValue, KumikoComparer comparer)
    {
        if (!TryConvertToDateTime(cellValue, comparer, out DateTime cell)) return false;
        if (!TryConvertToDateTime(Value, comparer, out DateTime filter)) return false;

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

    private static bool TryConvertToDouble(object? value, KumikoComparer comparer, out double result)
    {
        result = 0;
        if (value == null) return false;
        if (KumikoComparer.TryGetNumber(value, out var number)) { result = number.AsDouble; return true; }
        return double.TryParse(comparer.Format(value), System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, comparer.Culture, out result);
    }

    private static bool TryConvertToDateTime(object? value, KumikoComparer comparer, out DateTime result)
    {
        result = default;
        if (value == null) return false;
        if (value is DateTime dt) { result = dt; return true; }
        if (value is DateTimeOffset dto) { result = dto.DateTime; return true; }
        if (value is DateOnly d) { result = d.ToDateTime(TimeOnly.MinValue); return true; }
        return DateTime.TryParse(comparer.Format(value), comparer.Culture, System.Globalization.DateTimeStyles.None, out result);
    }
}
