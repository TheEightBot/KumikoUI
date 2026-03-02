using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class FilterDescriptionTests
{
    private static DataGridColumn MakeTextColumn() =>
        new DataGridColumn { ColumnType = DataGridColumnType.Text, PropertyName = "Name" };

    private static DataGridColumn MakeNumericColumn() =>
        new DataGridColumn { ColumnType = DataGridColumnType.Numeric, PropertyName = "Value" };

    // ── IsActive ──────────────────────────────────────────────────

    [Fact]
    public void IsActive_NoFilter_ReturnsFalse()
    {
        var desc = new FilterDescription(MakeTextColumn());
        Assert.False(desc.IsActive);
    }

    [Fact]
    public void IsActive_WithSelectedValues_ReturnsTrue()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            SelectedValues = new HashSet<string> { "Alice" }
        };
        Assert.True(desc.IsActive);
    }

    [Fact]
    public void IsActive_WithCondition1_ReturnsTrue()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.Contains, Value = "a" }
        };
        Assert.True(desc.IsActive);
    }

    // ── Evaluate: value list filter ───────────────────────────────

    [Fact]
    public void Evaluate_ValueList_ValueInSet_ReturnsTrue()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            SelectedValues = new HashSet<string> { "Alice", "Bob" }
        };
        Assert.True(desc.Evaluate("Alice_raw", "Alice"));
    }

    [Fact]
    public void Evaluate_ValueList_ValueNotInSet_ReturnsFalse()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            SelectedValues = new HashSet<string> { "Alice", "Bob" }
        };
        Assert.False(desc.Evaluate("Charlie_raw", "Charlie"));
    }

    // ── Evaluate: single condition ────────────────────────────────

    [Fact]
    public void Evaluate_Condition1_MatchingValue_ReturnsTrue()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Al" }
        };
        Assert.True(desc.Evaluate("Alice", "Alice"));
    }

    [Fact]
    public void Evaluate_Condition1_NonMatchingValue_ReturnsFalse()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Al" }
        };
        Assert.False(desc.Evaluate("Bob", "Bob"));
    }

    // ── Evaluate: compound AND ────────────────────────────────────

    [Fact]
    public void Evaluate_And_BothMatch_ReturnsTrue()
    {
        var col = MakeNumericColumn();
        var desc = new FilterDescription(col)
        {
            Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 10.0 },
            Condition2 = new FilterCondition { NumericOperator = NumericFilterOperator.LessThan, Value = 50.0 },
            Combination = FilterCombination.And
        };
        Assert.True(desc.Evaluate(25.0, "25"));
    }

    [Fact]
    public void Evaluate_And_OneMatchFails_ReturnsFalse()
    {
        var col = MakeNumericColumn();
        var desc = new FilterDescription(col)
        {
            Condition1 = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 10.0 },
            Condition2 = new FilterCondition { NumericOperator = NumericFilterOperator.LessThan, Value = 20.0 },
            Combination = FilterCombination.And
        };
        // 25 > 10 but NOT 25 < 20
        Assert.False(desc.Evaluate(25.0, "25"));
    }

    // ── Evaluate: compound OR ─────────────────────────────────────

    [Fact]
    public void Evaluate_Or_OneMatchSucceeds_ReturnsTrue()
    {
        var col = MakeTextColumn();
        var desc = new FilterDescription(col)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Al" },
            Condition2 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Bo" },
            Combination = FilterCombination.Or
        };
        Assert.True(desc.Evaluate("Alice", "Alice"));
        Assert.True(desc.Evaluate("Bob", "Bob"));
    }

    [Fact]
    public void Evaluate_Or_NeitherMatches_ReturnsFalse()
    {
        var col = MakeTextColumn();
        var desc = new FilterDescription(col)
        {
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Al" },
            Condition2 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Bo" },
            Combination = FilterCombination.Or
        };
        Assert.False(desc.Evaluate("Charlie", "Charlie"));
    }

    // ── Evaluate: combined value list + condition ─────────────────

    [Fact]
    public void Evaluate_ValueListAndCondition_BothMustPass()
    {
        var desc = new FilterDescription(MakeTextColumn())
        {
            SelectedValues = new HashSet<string> { "Alice", "Alex" },
            Condition1 = new FilterCondition { TextOperator = TextFilterOperator.StartsWith, Value = "Ali" }
        };
        // "Alice" is in value list AND starts with "Ali" → true
        Assert.True(desc.Evaluate("Alice", "Alice"));
        // "Alex" is in value list but does NOT start with "Ali" → false
        Assert.False(desc.Evaluate("Alex", "Alex"));
    }

    // ── Column reference ──────────────────────────────────────────

    [Fact]
    public void Column_ReturnsConstructorArgument()
    {
        var col = MakeTextColumn();
        var desc = new FilterDescription(col);
        Assert.Same(col, desc.Column);
    }
}

