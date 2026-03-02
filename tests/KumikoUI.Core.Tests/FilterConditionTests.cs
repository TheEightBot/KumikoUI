using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class FilterConditionTests
{
    // ── Text filter ───────────────────────────────────────────────

    [Theory]
    [InlineData("hello", "hello", TextFilterOperator.Equals, true)]
    [InlineData("HELLO", "hello", TextFilterOperator.Equals, true)]        // case-insensitive
    [InlineData("hello", "world", TextFilterOperator.Equals, false)]
    public void EvaluateText_Equals(string cell, string filter, TextFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { TextOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Text));
    }

    [Theory]
    [InlineData("hello", "world", TextFilterOperator.NotEquals, true)]
    [InlineData("hello", "hello", TextFilterOperator.NotEquals, false)]
    public void EvaluateText_NotEquals(string cell, string filter, TextFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { TextOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Text));
    }

    [Theory]
    [InlineData("hello world", "world", TextFilterOperator.Contains, true)]
    [InlineData("hello world", "WORLD", TextFilterOperator.Contains, true)] // case-insensitive
    [InlineData("hello world", "xyz", TextFilterOperator.Contains, false)]
    public void EvaluateText_Contains(string cell, string filter, TextFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { TextOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Text));
    }

    [Theory]
    [InlineData("hello world", "xyz", TextFilterOperator.NotContains, true)]
    [InlineData("hello world", "world", TextFilterOperator.NotContains, false)]
    public void EvaluateText_NotContains(string cell, string filter, TextFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { TextOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Text));
    }

    [Theory]
    [InlineData("hello world", "hello", TextFilterOperator.StartsWith, true)]
    [InlineData("hello world", "HELLO", TextFilterOperator.StartsWith, true)]
    [InlineData("hello world", "world", TextFilterOperator.StartsWith, false)]
    public void EvaluateText_StartsWith(string cell, string filter, TextFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { TextOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Text));
    }

    [Theory]
    [InlineData("hello world", "world", TextFilterOperator.EndsWith, true)]
    [InlineData("hello world", "WORLD", TextFilterOperator.EndsWith, true)]
    [InlineData("hello world", "hello", TextFilterOperator.EndsWith, false)]
    public void EvaluateText_EndsWith(string cell, string filter, TextFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { TextOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Text));
    }

    [Fact]
    public void EvaluateText_Empty_TrueForNullOrEmptyCell()
    {
        var condition = new FilterCondition { TextOperator = TextFilterOperator.Empty };
        Assert.True(condition.Evaluate(null, DataGridColumnType.Text));
        Assert.True(condition.Evaluate("", DataGridColumnType.Text));
        Assert.False(condition.Evaluate("hello", DataGridColumnType.Text));
    }

    [Fact]
    public void EvaluateText_NotEmpty_TrueForNonEmptyCell()
    {
        var condition = new FilterCondition { TextOperator = TextFilterOperator.NotEmpty };
        Assert.True(condition.Evaluate("hello", DataGridColumnType.Text));
        Assert.False(condition.Evaluate("", DataGridColumnType.Text));
        Assert.False(condition.Evaluate(null, DataGridColumnType.Text));
    }

    // ── Numeric filter ────────────────────────────────────────────

    [Theory]
    [InlineData(42.0, 42.0, NumericFilterOperator.Equals, true)]
    [InlineData(42.0, 43.0, NumericFilterOperator.Equals, false)]
    [InlineData(42.0, 43.0, NumericFilterOperator.NotEquals, true)]
    [InlineData(10.0, 20.0, NumericFilterOperator.LessThan, true)]
    [InlineData(20.0, 20.0, NumericFilterOperator.LessThan, false)]
    [InlineData(20.0, 20.0, NumericFilterOperator.LessThanOrEqual, true)]
    [InlineData(30.0, 20.0, NumericFilterOperator.GreaterThan, true)]
    [InlineData(20.0, 20.0, NumericFilterOperator.GreaterThan, false)]
    [InlineData(20.0, 20.0, NumericFilterOperator.GreaterThanOrEqual, true)]
    public void EvaluateNumeric(double cell, double filter, NumericFilterOperator op, bool expected)
    {
        var condition = new FilterCondition { NumericOperator = op, Value = filter };
        Assert.Equal(expected, condition.Evaluate(cell, DataGridColumnType.Numeric));
    }

    [Fact]
    public void EvaluateNumeric_NullCellValue_ReturnsFalse()
    {
        var condition = new FilterCondition { NumericOperator = NumericFilterOperator.Equals, Value = 5.0 };
        Assert.False(condition.Evaluate(null, DataGridColumnType.Numeric));
    }

    [Fact]
    public void EvaluateNumeric_IntCellValue_WorksCorrectly()
    {
        var condition = new FilterCondition { NumericOperator = NumericFilterOperator.Equals, Value = 42.0 };
        Assert.True(condition.Evaluate(42, DataGridColumnType.Numeric));
    }

    [Fact]
    public void EvaluateNumeric_StringCellValue_ParsedCorrectly()
    {
        var condition = new FilterCondition { NumericOperator = NumericFilterOperator.GreaterThan, Value = 10.0 };
        Assert.True(condition.Evaluate("15.5", DataGridColumnType.Numeric));
    }

    // ── Date filter ───────────────────────────────────────────────

    [Fact]
    public void EvaluateDate_Equals_SameDate_ReturnsTrue()
    {
        var date = new DateTime(2024, 6, 15);
        var condition = new FilterCondition { DateOperator = DateFilterOperator.Equals, Value = date };
        Assert.True(condition.Evaluate(date, DataGridColumnType.Date));
    }

    [Fact]
    public void EvaluateDate_Equals_DifferentDate_ReturnsFalse()
    {
        var date = new DateTime(2024, 6, 15);
        var filterDate = new DateTime(2024, 6, 16);
        var condition = new FilterCondition { DateOperator = DateFilterOperator.Equals, Value = filterDate };
        Assert.False(condition.Evaluate(date, DataGridColumnType.Date));
    }

    [Fact]
    public void EvaluateDate_Before_EarlierDate_ReturnsTrue()
    {
        var cellDate = new DateTime(2024, 1, 1);
        var filterDate = new DateTime(2024, 6, 1);
        var condition = new FilterCondition { DateOperator = DateFilterOperator.Before, Value = filterDate };
        Assert.True(condition.Evaluate(cellDate, DataGridColumnType.Date));
    }

    [Fact]
    public void EvaluateDate_After_LaterDate_ReturnsTrue()
    {
        var cellDate = new DateTime(2024, 12, 1);
        var filterDate = new DateTime(2024, 6, 1);
        var condition = new FilterCondition { DateOperator = DateFilterOperator.After, Value = filterDate };
        Assert.True(condition.Evaluate(cellDate, DataGridColumnType.Date));
    }

    [Fact]
    public void EvaluateDate_BeforeOrEqual_SameDate_ReturnsTrue()
    {
        var date = new DateTime(2024, 6, 15);
        var condition = new FilterCondition { DateOperator = DateFilterOperator.BeforeOrEqual, Value = date };
        Assert.True(condition.Evaluate(date, DataGridColumnType.Date));
    }

    [Fact]
    public void EvaluateDate_IgnoresTimeComponent()
    {
        var cellDate = new DateTime(2024, 6, 15, 10, 30, 0);
        var filterDate = new DateTime(2024, 6, 15, 20, 0, 0);
        var condition = new FilterCondition { DateOperator = DateFilterOperator.Equals, Value = filterDate };
        Assert.True(condition.Evaluate(cellDate, DataGridColumnType.Date));
    }

    [Fact]
    public void EvaluateDate_NullCellValue_ReturnsFalse()
    {
        var condition = new FilterCondition
        {
            DateOperator = DateFilterOperator.Equals,
            Value = new DateTime(2024, 1, 1)
        };
        Assert.False(condition.Evaluate(null, DataGridColumnType.Date));
    }

    // ── Template column passthrough ───────────────────────────────

    [Fact]
    public void Evaluate_TemplateColumn_AlwaysReturnsTrue()
    {
        var condition = new FilterCondition { TextOperator = TextFilterOperator.Equals, Value = "anything" };
        Assert.True(condition.Evaluate("irrelevant", DataGridColumnType.Template));
        Assert.True(condition.Evaluate(null, DataGridColumnType.Template));
    }
}

