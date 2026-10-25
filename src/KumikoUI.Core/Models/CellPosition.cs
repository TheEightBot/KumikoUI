namespace KumikoUI.Core.Models;

/// <summary>
/// Identifies a cell position.
/// </summary>
public readonly struct CellPosition : IEquatable<CellPosition>
{
    /// <summary>Row index (zero-based visible row).</summary>
    public int Row { get; }
    /// <summary>Column index (zero-based).</summary>
    public int Column { get; }

    public CellPosition(int row, int column)
    {
        Row = row;
        Column = column;
    }

    /// <summary>Whether the cell position refers to a valid (non-negative) row and column.</summary>
    public bool IsValid => Row >= 0 && Column >= 0;
    /// <summary>A sentinel value representing no cell (-1, -1).</summary>
    public static CellPosition Invalid => new(-1, -1);

    public bool Equals(CellPosition other) => Row == other.Row && Column == other.Column;
    public override bool Equals(object? obj) => obj is CellPosition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Row, Column);
    public static bool operator ==(CellPosition a, CellPosition b) => a.Equals(b);
    public static bool operator !=(CellPosition a, CellPosition b) => !a.Equals(b);
    public override string ToString() => $"({Row}, {Column})";
}
