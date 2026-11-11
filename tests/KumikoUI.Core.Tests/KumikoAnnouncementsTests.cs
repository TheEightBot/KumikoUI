using System.Globalization;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>The screen-reader texts the hosts announce (#77), built from <see cref="KumikoStrings"/>.</summary>
public class KumikoAnnouncementsTests
{
    private static readonly KumikoStrings English = KumikoStrings.Default;
    private static readonly KumikoColumn Name = new() { Header = "Name" };

    private static T WithInvariantCulture<T>(Func<T> action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void GridHint_WithoutCurrentCell_DescribesSize()
        => Assert.Equal("120 rows, 5 columns",
            KumikoAnnouncements.GridHint(English, 120, 5, CellPosition.Invalid));

    [Fact]
    public void GridHint_WithCurrentCell_IsOneBased()
        => Assert.Equal("120 rows, 5 columns, current cell row 3 column 2",
            KumikoAnnouncements.GridHint(English, 120, 5, new CellPosition(2, 1)));

    [Fact]
    public void CurrentCell_AnnouncesHeaderValueAndPosition()
        => Assert.Equal("Name: Ada, row 3, column 2",
            KumikoAnnouncements.CurrentCell(English, Name, 1, 2, "Ada"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CurrentCell_EmptyText_AnnouncesEmpty(string? text)
        => Assert.Equal("Name: empty, row 1, column 1",
            KumikoAnnouncements.CurrentCell(English, Name, 0, 0, text));

    [Fact]
    public void CurrentCell_ColumnWithoutHeader_UsesColumnNumber()
        => Assert.Equal("Column 4: x, row 1, column 4",
            KumikoAnnouncements.CurrentCell(English, new KumikoColumn(), 3, 0, "x"));

    [Theory]
    [InlineData(KumikoSortDirection.Ascending, "Sorted by Name, ascending")]
    [InlineData(KumikoSortDirection.Descending, "Sorted by Name, descending")]
    [InlineData(KumikoSortDirection.None, "Sort removed from Name")]
    public void SortChanged_FollowsDirection(KumikoSortDirection direction, string expected)
    {
        var column = new KumikoColumn { Header = "Name", SortDirection = direction };
        Assert.Equal(expected, KumikoAnnouncements.SortChanged(English, column, 0));
    }

    [Fact]
    public void EditStarted_AnnouncesCurrentValue()
        => Assert.Equal("Editing Name: Ada", KumikoAnnouncements.EditStarted(English, Name, 0, "Ada"));

    [Fact]
    public void EditEnded_Committed_AnnouncesNewValue()
        => Assert.Equal("Name changed to Grace", KumikoAnnouncements.EditEnded(English, Name, 0, true, "Grace"));

    [Fact]
    public void EditEnded_NotCommitted_AnnouncesEnd()
        => Assert.Equal("Editing Name ended", KumikoAnnouncements.EditEnded(English, Name, 0, false, null));

    [Fact]
    public void ValidationFailed_AnnouncesMessage()
        => Assert.Equal("Name is invalid: A value is required.",
            KumikoAnnouncements.ValidationFailed(English, Name, 0, English.ValueRequired));

    [Fact]
    public void Texts_FollowTheLocalizedTable()
    {
        var german = English with
        {
            AccessibleGridHint = "{0} Zeilen, {1} Spalten",
            AnnounceSortedAscending = "Sortiert nach {0}, aufsteigend",
            AnnounceEmptyValue = "leer"
        };
        var sorted = new KumikoColumn { Header = "Name", SortDirection = KumikoSortDirection.Ascending };

        Assert.Equal("3 Zeilen, 2 Spalten", KumikoAnnouncements.GridHint(german, 3, 2, CellPosition.Invalid));
        Assert.Equal("Sortiert nach Name, aufsteigend", KumikoAnnouncements.SortChanged(german, sorted, 0));
        Assert.Equal("Name: leer, row 1, column 1", KumikoAnnouncements.CurrentCell(german, Name, 0, 0, null));
    }

    [Fact]
    public void DefaultStrings_AccessibleGridName_IsDataGrid()
        => Assert.Equal("Data grid", English.AccessibleGridName);

    [Fact]
    public void Texts_AreStableAcrossCultures()
        => Assert.Equal("1000 rows, 5 columns",
            WithInvariantCulture(() => KumikoAnnouncements.GridHint(English, 1000, 5, CellPosition.Invalid)));
}
