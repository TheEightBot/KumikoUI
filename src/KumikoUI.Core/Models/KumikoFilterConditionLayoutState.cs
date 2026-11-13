using System.Globalization;

namespace KumikoUI.Core.Models;

/// <summary>A filter condition in a <see cref="KumikoLayoutState"/> (a <see cref="FilterCondition"/>).</summary>
public sealed class KumikoFilterConditionLayoutState
{
    /// <summary>The <see cref="FilterCondition.TextOperator"/>.</summary>
    public TextFilterOperator TextOperator { get; set; } = TextFilterOperator.Contains;

    /// <summary>The <see cref="FilterCondition.NumericOperator"/>.</summary>
    public NumericFilterOperator NumericOperator { get; set; } = NumericFilterOperator.Equals;

    /// <summary>The <see cref="FilterCondition.DateOperator"/>.</summary>
    public DateFilterOperator DateOperator { get; set; } = DateFilterOperator.Equals;

    /// <summary>The <see cref="FilterCondition.Value"/> as invariant text (see <see cref="ValueKind"/>), or null.</summary>
    public string? Value { get; set; }

    /// <summary>The type <see cref="Value"/> is read back as.</summary>
    public KumikoFilterValueKind ValueKind { get; set; }

    /// <summary>The state of <paramref name="condition"/>, or null when there is none.</summary>
    internal static KumikoFilterConditionLayoutState? From(FilterCondition? condition)
    {
        if (condition == null) return null;
        var state = new KumikoFilterConditionLayoutState
        {
            TextOperator = condition.TextOperator,
            NumericOperator = condition.NumericOperator,
            DateOperator = condition.DateOperator,
        };

        var invariant = CultureInfo.InvariantCulture;
        switch (condition.Value)
        {
            case null:
                break;
            case bool b:
                state.ValueKind = KumikoFilterValueKind.Boolean;
                state.Value = b ? "true" : "false";
                break;
            case DateTime dt:
                state.ValueKind = KumikoFilterValueKind.Date;
                state.Value = dt.ToString("O", invariant);
                break;
            case DateTimeOffset dto:
                state.ValueKind = KumikoFilterValueKind.Date;
                state.Value = dto.DateTime.ToString("O", invariant); // filters compare the clock time
                break;
            case DateOnly d:
                state.ValueKind = KumikoFilterValueKind.Date;
                state.Value = d.ToDateTime(TimeOnly.MinValue).ToString("O", invariant);
                break;
            case var number when KumikoComparer.TryGetNumber(number, out _):
                state.ValueKind = KumikoFilterValueKind.Number;
                state.Value = ((IFormattable)number).ToString(null, invariant);
                break;
            case var other:
                state.ValueKind = KumikoFilterValueKind.Text;
                state.Value = Convert.ToString(other, invariant);
                break;
        }
        return state;
    }

    /// <summary>A new condition with this state.</summary>
    internal FilterCondition ToFilterCondition() => new()
    {
        TextOperator = TextOperator,
        NumericOperator = NumericOperator,
        DateOperator = DateOperator,
        Value = ReadValue(),
    };

    private object? ReadValue()
    {
        if (Value == null) return null;
        var invariant = CultureInfo.InvariantCulture;
        switch (ValueKind)
        {
            case KumikoFilterValueKind.None:
                return null;
            case KumikoFilterValueKind.Boolean when bool.TryParse(Value, out var b):
                return b;
            case KumikoFilterValueKind.Number
                when double.TryParse(Value, NumberStyles.Float | NumberStyles.AllowThousands, invariant, out var d):
                return d;
            case KumikoFilterValueKind.Date
                when DateTime.TryParse(Value, invariant, DateTimeStyles.RoundtripKind, out var dt):
                return dt;
            default:
                return Value; // text, or a value that no longer parses as its kind
        }
    }
}
