using System.Globalization;

namespace KumikoUI.Core.Models;

// Every summary is computed in one pass per property over its rows, with streaming aggregates: nothing collects a
// list of boxed values except a Custom aggregate, which receives one (#69). Sum, Average and Count can also follow a
// changed value in constant time (subtract the old value, add the new one), which is how table, group and caption
// summaries stay current when an item raises PropertyChanged without the view being rebuilt (#69, #254).

/// <summary>
/// The aggregates of one property over a set of rows, computed in a single pass without collecting the values
/// (except for a Custom aggregate, which receives them as a list).
/// </summary>
internal sealed class PropertyAggregate
{
    /// <summary>Marks, in <see cref="Values"/>, a source index that has no visible row.</summary>
    public static readonly object NoRow = new();

    public PropertyAggregate(string propertyName) => PropertyName = propertyName;

    /// <summary>The aggregated property.</summary>
    public string PropertyName { get; }

    /// <summary>Reads the property from an item (set by each pass).</summary>
    public Func<object, object?> Reader { get; set; } = static _ => null;

    /// <summary>Whether a description still uses this aggregate (table summaries keep it between rebuilds).</summary>
    public bool InUse { get; private set; }

    private bool _numeric, _minMax, _custom;

    /// <summary>Rows aggregated: the result of Count.</summary>
    public int Rows { get; private set; }

    /// <summary>Totals of the numeric values: Sum and Average. A field, so it can be updated in place.</summary>
    public NumericTotal Total;

    private object? _min, _max;
    private List<object?>? _customValues;

    /// <summary>
    /// Each source index's value for a table aggregate that can follow a changed value, or <see cref="NoRow"/>
    /// for an index without a visible row; null otherwise.
    /// </summary>
    public object?[]? Values { get; set; }

    /// <summary>Whether a changed value can change a result (anything but Count).</summary>
    public bool DependsOnValues => _numeric || _minMax || _custom;

    /// <summary>Whether a changed value can be applied without the other rows: only Sum, Average and Count.</summary>
    public bool CanUpdateInPlace => !_minMax && !_custom;

    public void ResetRequirements() => InUse = _numeric = _minMax = _custom = false;

    public void Require(SummaryType type)
    {
        InUse = true;
        switch (type)
        {
            case SummaryType.Sum:
            case SummaryType.Average: _numeric = true; break;
            case SummaryType.Min:
            case SummaryType.Max: _minMax = true; break;
            case SummaryType.Custom: _custom = true; break;
        }
    }

    /// <summary>Start a pass.</summary>
    public void Begin()
    {
        Rows = 0;
        Total = default;
        _min = _max = null;
        _customValues = _custom ? new List<object?>() : null;
    }

    public void Add(object? value, KumikoComparer comparer)
    {
        Rows++;
        if (_numeric) Total.Add(value);
        if (_minMax && value != null)
        {
            if (_min == null || comparer.Compare(value, _min) < 0) _min = value;
            if (_max == null || comparer.Compare(value, _max) > 0) _max = value;
        }
        _customValues?.Add(value);
    }

    /// <summary>
    /// Replace one row's <paramref name="oldValue"/> with <paramref name="newValue"/> (Sum, Average and Count only).
    /// False when the exact decimal total overflowed and must be computed again in order.
    /// </summary>
    public bool Replace(object? oldValue, object? newValue)
    {
        Total.Remove(oldValue);
        Total.Add(newValue);
        return !Total.Overflow;
    }

    public object? Result(SummaryColumnDescription description) => description.SummaryType switch
    {
        SummaryType.Count => Rows,
        SummaryType.Sum => Total.Sum,
        SummaryType.Average => Total.Average,
        SummaryType.Min => _min,
        SummaryType.Max => _max,
        SummaryType.Custom => description.CustomAggregate?.Invoke(
            (IReadOnlyList<object?>?)_customValues ?? Array.Empty<object?>()),
        _ => null,
    };

    /// <summary>Add an aggregate (or a requirement of an existing one) for each of <paramref name="descriptions"/>.</summary>
    public static void Require(Dictionary<string, PropertyAggregate> aggregates, IReadOnlyList<SummaryColumnDescription> descriptions)
    {
        foreach (var description in descriptions)
        {
            if (!aggregates.TryGetValue(description.PropertyName, out var aggregate))
                aggregates[description.PropertyName] = aggregate = new PropertyAggregate(description.PropertyName);
            aggregate.Require(description.SummaryType);
        }
    }
}

/// <summary>
/// Totals of numeric values that can be added and removed. When a <c>decimal</c> is present and no floating-point
/// value is, the total is exact (a <c>decimal</c>); otherwise it is a <c>double</c>. Values that are not numbers
/// (text, dates, booleans, other objects) are skipped by a type-code switch, so no exception is raised per value.
/// Removing values keeps the double total within rounding of a fresh sum, not bit-identical to it.
/// </summary>
internal struct NumericTotal
{
    private decimal _decimalSum;
    private double _doubleSum;
    private int _count, _decimals, _floats;

    /// <summary>The decimal total overflowed; it stays unusable until the totals are computed again.</summary>
    public bool Overflow { get; private set; }

    public void Add(object? value) => Apply(value, add: true);

    public void Remove(object? value) => Apply(value, add: false);

    private void Apply(object? v, bool add)
    {
        if (v == null) return;
        decimal m = 0;
        double d;
        int isDecimal = 0, isFloat = 0;
        switch (Type.GetTypeCode(v.GetType()))
        {
            case TypeCode.Decimal: m = (decimal)v; d = (double)m; isDecimal = 1; break;
            case TypeCode.Int32: m = (int)v; d = (int)v; break;
            case TypeCode.Int64: m = (long)v; d = (long)v; break;
            case TypeCode.Int16: m = (short)v; d = (short)v; break;
            case TypeCode.SByte: m = (sbyte)v; d = (sbyte)v; break;
            case TypeCode.Byte: m = (byte)v; d = (byte)v; break;
            case TypeCode.UInt16: m = (ushort)v; d = (ushort)v; break;
            case TypeCode.UInt32: m = (uint)v; d = (uint)v; break;
            case TypeCode.UInt64: m = (ulong)v; d = (ulong)v; break;
            case TypeCode.Double: d = (double)v; isFloat = 1; break;
            case TypeCode.Single: d = (float)v; isFloat = 1; break;
            default: return; // not a number
        }

        int sign = add ? 1 : -1;
        _count += sign;
        _decimals += sign * isDecimal;
        _floats += sign * isFloat;
        _doubleSum += add ? d : -d;
        if (isFloat == 0 && !Overflow)
        {
            try { _decimalSum = add ? _decimalSum + m : _decimalSum - m; }
            catch (OverflowException) { Overflow = true; }
        }
    }

    private readonly bool Exact => _decimals > 0 && _floats == 0 && !Overflow;

    /// <summary>The total: <c>decimal</c> when exact, otherwise <c>double</c> (0 when there are no numbers).</summary>
    public readonly object Sum => Exact ? (object)_decimalSum : _doubleSum;

    /// <summary>The mean of the numbers, or null when there are none.</summary>
    public readonly object? Average => _count == 0 ? null : Exact ? (object)(_decimalSum / _count) : _doubleSum / _count;
}

/// <summary>Builds the text of computed summary rows and caption summaries from their aggregates.</summary>
internal static class SummaryText
{
    /// <summary>Separates the aggregates of one property in a summary cell.</summary>
    public const string Separator = "  |  ";

    /// <summary>
    /// Build a computed summary row from its descriptions and the aggregates of their properties. Every
    /// description is kept: several aggregates of one property show side by side in that property's cell.
    /// </summary>
    public static ComputedSummaryRow BuildRow(
        string name, string? title, IReadOnlyList<SummaryColumnDescription> columnDescs,
        Func<string, PropertyAggregate?> aggregateOf, CultureInfo culture, KumikoStrings strings)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var computed = new List<ComputedSummaryValue>(columnDescs.Count);

        foreach (var colDesc in columnDescs)
        {
            // A description added to the row's list after the last rebuild has no aggregate until the next one.
            object? rawResult = aggregateOf(colDesc.PropertyName) is { } aggregate ? aggregate.Result(colDesc) : null;

            string label = colDesc.Label ?? strings.SummaryLabel(colDesc.SummaryType);
            string formatted = FormatValue(rawResult, colDesc, culture);
            string text = $"{label}{formatted}";

            computed.Add(new ComputedSummaryValue
            {
                PropertyName = colDesc.PropertyName,
                SummaryType = colDesc.SummaryType,
                Label = label,
                Value = rawResult,
                Text = text
            });

            if (values.TryGetValue(colDesc.PropertyName, out var existing))
                values[colDesc.PropertyName] = existing + Separator + text;
            else
            {
                values[colDesc.PropertyName] = text;
                rawValues[colDesc.PropertyName] = rawResult;
            }
        }

        return new ComputedSummaryRow
        {
            Name = name,
            Title = title,
            Values = values,
            RawValues = rawValues,
            Aggregates = computed
        };
    }

    /// <summary>The caption summary text of a group header from the aggregates of its properties.</summary>
    public static string BuildCaption(
        IReadOnlyList<SummaryColumnDescription> columns, string? formatTemplate,
        Func<string, PropertyAggregate?> aggregateOf, CultureInfo culture, KumikoStrings strings)
    {
        var summaryValues = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            var colDesc = columns[i];
            object? rawResult = aggregateOf(colDesc.PropertyName)?.Result(colDesc);
            string formatted = FormatValue(rawResult, colDesc, culture);
            string label = colDesc.Label ?? strings.SummaryLabel(colDesc.SummaryType);
            summaryValues[i] = $"{label}{formatted}";
        }

        if (formatTemplate != null)
        {
            try
            {
                // Format template uses {0}, {1}, etc. referencing formatted values
                return string.Format(formatTemplate, summaryValues);
            }
            catch
            {
                return string.Join(" | ", summaryValues);
            }
        }

        return string.Join(" | ", summaryValues);
    }

    /// <summary>Format an aggregate result using the column description's format.</summary>
    public static string FormatValue(object? value, SummaryColumnDescription colDesc, CultureInfo culture)
    {
        if (value == null) return "";

        if (!string.IsNullOrEmpty(colDesc.Format))
        {
            // A composite format ("Max {0:N1}") goes through string.Format; anything else is a format string
            if (colDesc.Format.Contains("{0"))
            {
                try { return string.Format(culture, colDesc.Format, value); }
                catch { /* fall through */ }
            }
            // Otherwise use IFormattable
            if (value is IFormattable fmt)
                return fmt.ToString(colDesc.Format, culture);
        }

        return value is IFormattable plain ? plain.ToString(null, culture) : value.ToString() ?? "";
    }
}
