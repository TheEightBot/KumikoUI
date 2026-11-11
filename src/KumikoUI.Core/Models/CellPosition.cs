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

    /// <summary>Creates a cell position.</summary>
    /// <param name="row">Zero-based visible row index.</param>
    /// <param name="column">Zero-based column index.</param>
    public CellPosition(int row, int column)
    {
        Row = row;
        Column = column;
    }

    /// <summary>Whether the cell position refers to a valid (non-negative) row and column.</summary>
    public bool IsValid => Row >= 0 && Column >= 0;
    /// <summary>A sentinel value representing no cell (-1, -1).</summary>
    public static CellPosition Invalid => new(-1, -1);

    /// <inheritdoc/>
    public bool Equals(CellPosition other) => Row == other.Row && Column == other.Column;
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CellPosition other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Row, Column);
    /// <summary>Returns true when both positions refer to the same row and column.</summary>
    /// <param name="a">First position.</param>
    /// <param name="b">Second position.</param>
    public static bool operator ==(CellPosition a, CellPosition b) => a.Equals(b);
    /// <summary>Returns true when the positions differ in row or column.</summary>
    /// <param name="a">First position.</param>
    /// <param name="b">Second position.</param>
    public static bool operator !=(CellPosition a, CellPosition b) => !a.Equals(b);
    /// <inheritdoc/>
    public override string ToString() => $"({Row}, {Column})";
}
