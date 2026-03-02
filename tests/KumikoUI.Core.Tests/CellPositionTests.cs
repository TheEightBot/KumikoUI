using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

public class CellPositionTests
{
    [Fact]
    public void Constructor_SetsRowAndColumn()
    {
        var pos = new CellPosition(3, 7);
        Assert.Equal(3, pos.Row);
        Assert.Equal(7, pos.Column);
    }

    [Fact]
    public void IsValid_NonNegativeRowAndColumn_ReturnsTrue()
    {
        Assert.True(new CellPosition(0, 0).IsValid);
        Assert.True(new CellPosition(5, 10).IsValid);
    }

    [Fact]
    public void IsValid_NegativeRow_ReturnsFalse()
    {
        Assert.False(new CellPosition(-1, 0).IsValid);
    }

    [Fact]
    public void IsValid_NegativeColumn_ReturnsFalse()
    {
        Assert.False(new CellPosition(0, -1).IsValid);
    }

    [Fact]
    public void Invalid_Sentinel_IsNotValid()
    {
        Assert.False(CellPosition.Invalid.IsValid);
        Assert.Equal(-1, CellPosition.Invalid.Row);
        Assert.Equal(-1, CellPosition.Invalid.Column);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new CellPosition(2, 5);
        var b = new CellPosition(2, 5);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_DifferentRow_ReturnsFalse()
    {
        var a = new CellPosition(2, 5);
        var b = new CellPosition(3, 5);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_DifferentColumn_ReturnsFalse()
    {
        var a = new CellPosition(2, 5);
        var b = new CellPosition(2, 6);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ObjectOverload_WorksCorrectly()
    {
        var a = new CellPosition(2, 5);
        object b = new CellPosition(2, 5);
        Assert.True(a.Equals(b));
        Assert.False(a.Equals("not a cell position"));
    }

    [Fact]
    public void GetHashCode_EqualCells_SameHash()
    {
        var a = new CellPosition(2, 5);
        var b = new CellPosition(2, 5);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsRowAndColumn()
    {
        var pos = new CellPosition(3, 7);
        var str = pos.ToString();
        Assert.Contains("3", str);
        Assert.Contains("7", str);
    }
}

