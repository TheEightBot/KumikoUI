using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Grid-wide search (#317): highlight mode keeps the view, filter mode hides the rows with no match (synchronous and
/// background rebuilds), the source's culture and case rules apply, <see cref="KumikoColumn.AllowSearching"/> and the
/// column types without text are left out, matches are counted, and FindNext / FindPrevious walk the matches in
/// reading order, wrapping, skipping group rows and expanding collapsed groups.
/// </summary>
public class GridSearchTests
{
    public sealed class Person : INotifyPropertyChanged
    {
        private string _name = "";

        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public string City { get; set; } = "";
        public int Age { get; set; }
        public bool Active { get; set; }
        public DateTime Joined { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Name;
    }

    private static List<Person> People() => new()
    {
        new() { Name = "Anna", City = "Oslo", Age = 31 },
        new() { Name = "Bruno", City = "Rome", Age = 42 },
        new() { Name = "Carla", City = "Lima", Age = 25 },
        new() { Name = "Dan", City = "Oslo", Age = 37 },
        new() { Name = "Olga", City = "Kyiv", Age = 29 },
    };

    private static (KumikoSource Source, KumikoColumn Name, KumikoColumn City, KumikoColumn Age) NewSource(
        IEnumerable<Person>? people = null)
    {
        var name = new KumikoColumn { Header = "Name", PropertyName = nameof(Person.Name) };
        var city = new KumikoColumn { Header = "City", PropertyName = nameof(Person.City) };
        var age = new KumikoColumn { Header = "Age", PropertyName = nameof(Person.Age), ColumnType = KumikoColumnType.Numeric };
        var source = new KumikoSource();
        source.SetColumns(new[] { name, city, age });
        source.SetItems(people ?? People());
        return (source, name, city, age);
    }

    private static List<string> Names(KumikoSource source)
    {
        var names = new List<string>();
        for (int row = 0; row < source.RowCount; row++)
            if (!source.IsNonDataRow(row)) names.Add(((Person)source.GetItem(row)).Name);
        return names;
    }

    // ── Highlight mode and filter mode ───────────────────────────

    [Fact]
    public void HighlightMode_KeepsEveryRow_AndCountsMatchingCells()
    {
        var (source, _, _, _) = NewSource();
        int rebuilds = 0;
        source.ViewChanged += (_, e) => { if (e.Kind == KumikoViewChangeKind.Rebuild) rebuilds++; };

        source.SearchText = "o";

        Assert.Equal(5, source.RowCount);
        Assert.Equal(0, rebuilds);
        // Oslo (twice), Bruno, Rome and Olga contain an "o" (ignoring case)
        Assert.Equal(5, source.SearchMatchCount);
        Assert.True(source.IsSearchActive);
    }

    [Fact]
    public void FilterMode_HidesRowsWithNoMatchingCell()
    {
        var (source, _, _, _) = NewSource();
        source.SearchFiltersRows = true;

        source.SearchText = "oslo";

        Assert.Equal(new[] { "Anna", "Dan" }, Names(source));
        Assert.Equal(2, source.SearchMatchCount);

        source.SearchText = "";
        Assert.Equal(5, source.RowCount);
        Assert.Equal(0, source.SearchMatchCount);
    }

    [Fact]
    public void SwitchingFilterModeOff_ShowsEveryRowAgain()
    {
        var (source, _, _, _) = NewSource();
        source.SearchText = "Rome";
        source.SearchFiltersRows = true;
        Assert.Equal(new[] { "Bruno" }, Names(source));

        source.SearchFiltersRows = false;

        Assert.Equal(5, source.RowCount);
    }

    [Fact]
    public void FilterMode_CombinesWithColumnFilters()
    {
        var (source, _, city, _) = NewSource();
        source.SetColumnFilter(city, new FilterDescription(city) { SelectedValues = new HashSet<string> { "Oslo" } });
        source.SearchFiltersRows = true;

        source.SearchText = "a"; // Anna, Carla, Dan (and Olga) contain "a"; only the Oslo rows pass the column filter

        Assert.Equal(new[] { "Anna", "Dan" }, Names(source));
    }

    [Fact]
    public void FilterMode_FollowsAPropertyChangeOfASearchedColumn()
    {
        var people = People();
        var (source, _, _, _) = NewSource(people);
        source.SearchFiltersRows = true;
        source.SearchText = "Zed";
        Assert.Equal(0, source.RowCount);

        people[2].Name = "Zed";

        Assert.Equal(new[] { "Zed" }, Names(source));
    }

    [Fact]
    public void FilterMode_AddedItemsAreTested()
    {
        var people = new ObservableCollection<Person>(People());
        var (source, _, _, _) = NewSource(people);
        source.SearchFiltersRows = true;
        source.SearchText = "Lima";

        people.Add(new Person { Name = "Eve", City = "Lima" });
        people.Add(new Person { Name = "Finn", City = "Bern" });

        Assert.Equal(new[] { "Carla", "Eve" }, Names(source));
    }

    [Fact]
    public void SearchChanged_IsRaisedForTextAndMode()
    {
        var (source, _, _, _) = NewSource();
        int raised = 0;
        source.SearchChanged += (_, _) => raised++;

        source.SearchText = "a";
        source.SearchText = "a"; // unchanged
        source.SearchFiltersRows = true;
        source.SearchText = null!; // null is empty

        Assert.Equal(3, raised);
        Assert.Equal(string.Empty, source.SearchText);
    }

    // ── Text rules ───────────────────────────────────────────────

    [Fact]
    public void Matching_IgnoresCaseByDefault_AndFollowsStringCompareOptions()
    {
        var (source, _, _, _) = NewSource();
        source.SearchText = "OSLO";
        Assert.Equal(2, source.SearchMatchCount);

        source.StringCompareOptions = CompareOptions.Ordinal;
        Assert.Equal(0, source.SearchMatchCount);
    }

    [Fact]
    public void Matching_ReadsTheDisplayText_InTheSourceCulture()
    {
        var joined = new KumikoColumn
        {
            Header = "Joined", PropertyName = nameof(Person.Joined), ColumnType = KumikoColumnType.Date, Format = "MMMM",
        };
        var source = new KumikoSource { Culture = CultureInfo.GetCultureInfo("de-DE") };
        source.SetColumns(new[] { joined });
        source.SetItems(new[] { new Person { Joined = new DateTime(2024, 3, 1) }, new Person { Joined = new DateTime(2024, 5, 1) } });

        source.SearchText = "märz"; // German month name, case-insensitive
        Assert.Equal(1, source.SearchMatchCount);

        source.SearchText = "March";
        Assert.Equal(0, source.SearchMatchCount);
    }

    [Fact]
    public void AllowSearchingFalse_AndColumnsWithoutText_AreNotSearched()
    {
        var name = new KumikoColumn { Header = "Name", PropertyName = nameof(Person.Name) };
        var city = new KumikoColumn { Header = "City", PropertyName = nameof(Person.City), AllowSearching = false };
        var active = new KumikoColumn { Header = "Active", PropertyName = nameof(Person.Active), ColumnType = KumikoColumnType.Boolean };
        var template = new KumikoColumn { Header = "T", PropertyName = nameof(Person.Name), ColumnType = KumikoColumnType.Template };
        var hidden = new KumikoColumn { Header = "Hidden", PropertyName = nameof(Person.City), IsVisible = false };
        var source = new KumikoSource();
        source.SetColumns(new[] { name, city, active, template, hidden });
        source.SetItems(new[] { new Person { Name = "Anna", City = "Oslo", Active = true } });

        source.SearchText = "Oslo";
        Assert.Equal(0, source.SearchMatchCount);
        source.SearchText = "True";
        Assert.Equal(0, source.SearchMatchCount);
        source.SearchText = "Anna";
        Assert.Equal(1, source.SearchMatchCount); // the Template column showing Name is not searched

        city.AllowSearching = true;
        source.SearchText = "Oslo";
        Assert.Equal(1, source.SearchMatchCount);
    }

    [Fact]
    public void FilterMode_RebuildsWhenAColumnStopsBeingSearched()
    {
        var (source, _, city, _) = NewSource();
        source.SearchFiltersRows = true;
        source.SearchText = "Oslo";
        Assert.Equal(2, source.RowCount);

        city.AllowSearching = false;

        Assert.Equal(0, source.RowCount);
    }

    // ── Background ───────────────────────────────────────────────

    [Fact]
    public async Task FilterMode_RunsOnTheBackgroundWorker_AndCountsInTheBackground()
    {
        using var ui = new TestUiThread();
        var people = Enumerable.Range(0, 400).Select(i => new Person { Name = $"P{i:D3}", City = i % 4 == 0 ? "Oslo" : "Rome" }).ToList();
        var source = ui.Invoke(() =>
        {
            var (s, _, _, _) = NewSource(people);
            s.BackgroundRebuildThreshold = 100;
            return s;
        });

        int builds = ui.Invoke(() =>
        {
            int before = source.BackgroundBuildsStarted;
            source.SearchFiltersRows = true;
            source.SearchText = "oslo";
            return before;
        });
        await ui.Invoke(source.WhenViewUpdatedAsync).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(ui.Invoke(() => source.BackgroundBuildsStarted) > builds);
        Assert.Equal(100, ui.Invoke(() => source.RowCount));

        // The count of a large source arrives later, with SearchChanged.
        var counted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int first = ui.Invoke(() =>
        {
            source.SearchChanged += (_, _) => { if (source.SearchMatchCount >= 0) counted.TrySetResult(); };
            return source.SearchMatchCount;
        });
        if (first < 0) await counted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(100, ui.Invoke(() => source.SearchMatchCount));
    }

    // ── FindNext / FindPrevious ──────────────────────────────────

    private sealed class Grid
    {
        public Grid(KumikoSource source)
        {
            Source = source;
            Selection = new KumikoSelectionModel(source);
        }

        public KumikoSource Source { get; }
        public KumikoSelectionModel Selection { get; }
        public ScrollState Scroll { get; } = new() { ViewportWidth = 600, ViewportHeight = 400 };
        public KumikoStyle Style { get; } = new();
        public KumikoInputController Input { get; } = new() { SearchDebounceMs = 0 };

        public bool Next() => Input.FindNext(Scroll, Selection, Style, Source);
        public bool Previous() => Input.FindPrevious(Scroll, Selection, Style, Source);

        public string Current
        {
            get
            {
                var cell = Selection.CurrentCell;
                var item = (Person)Source.GetItem(cell.Row);
                return $"{item.Name}.{Source.Columns[cell.Column].Header}";
            }
        }
    }

    [Fact]
    public void FindNext_WalksTheMatchesInReadingOrder_AndWraps()
    {
        var (source, _, _, _) = NewSource();
        var grid = new Grid(source);
        source.SearchText = "o";

        var visited = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            Assert.True(grid.Next());
            visited.Add(grid.Current);
        }

        Assert.Equal(new[] { "Anna.City", "Bruno.Name", "Bruno.City", "Dan.City", "Olga.Name", "Anna.City" }, visited);
        Assert.Contains(grid.Selection.CurrentCell.Row, grid.Selection.SelectedRows);
    }

    [Fact]
    public void FindPrevious_WalksBackwards_AndWraps()
    {
        var (source, _, _, _) = NewSource();
        var grid = new Grid(source);
        source.SearchText = "o";

        Assert.True(grid.Previous());
        Assert.Equal("Olga.Name", grid.Current);
        Assert.True(grid.Previous());
        Assert.Equal("Dan.City", grid.Current);
        grid.Selection.CurrentCell = new CellPosition(0, 1); // Anna.City
        Assert.True(grid.Previous());
        Assert.Equal("Olga.Name", grid.Current);
    }

    [Fact]
    public void FindNext_FollowsTheDisplayOrderOfColumns()
    {
        var (source, _, city, _) = NewSource();
        source.SetDisplayIndex(city, 0); // City, Name, Age
        var grid = new Grid(source);
        source.SearchText = "o";

        Assert.True(grid.Next());
        Assert.Equal("Anna.City", grid.Current);
        Assert.True(grid.Next());
        Assert.Equal("Bruno.City", grid.Current); // Rome comes before Bruno on screen
        Assert.True(grid.Next());
        Assert.Equal("Bruno.Name", grid.Current);
    }

    [Fact]
    public void FindNext_ReturnsFalse_WithoutASearchOrAMatch()
    {
        var (source, _, _, _) = NewSource();
        var grid = new Grid(source);

        Assert.False(grid.Next());
        source.SearchText = "zzz";
        Assert.False(grid.Next());
        Assert.False(grid.Previous());
        Assert.False(grid.Selection.CurrentCell.IsValid);
    }

    [Fact]
    public void FindNext_SkipsGroupRows_AndExpandsACollapsedGroup()
    {
        var (source, _, _, _) = NewSource();
        source.AddGroupDescription(new GroupDescription(nameof(Person.City)));
        var grid = new Grid(source);
        // Groups in key order: Kyiv, Lima, Oslo, Rome. Collapse Oslo.
        int oslo = Enumerable.Range(0, source.RowCount).First(r =>
            source.GetGroupHeaderInfo(r) is { DisplayText: "Oslo" });
        source.ToggleGroupExpansion(oslo);
        Assert.False(source.GetGroupHeaderInfo(oslo)!.IsExpanded);
        source.SearchText = "an"; // Anna and Dan, both in Oslo

        grid.Selection.CurrentCell = new CellPosition(0, 0); // the Kyiv group header
        Assert.True(grid.Next());

        Assert.Equal("Anna.Name", grid.Current);
        Assert.True(source.GetGroupHeaderInfo(oslo)!.IsExpanded);
        Assert.False(source.IsNonDataRow(grid.Selection.CurrentCell.Row));
        Assert.True(grid.Next());
        Assert.Equal("Dan.Name", grid.Current);
        Assert.True(grid.Next());
        Assert.Equal("Anna.Name", grid.Current); // wrapped
    }

    [Fact]
    public void FindNext_ScrollsTheMatchIntoView()
    {
        var people = Enumerable.Range(0, 200).Select(i => new Person { Name = i == 150 ? "Needle" : $"P{i}" }).ToList();
        var (source, _, _, _) = NewSource(people);
        var grid = new Grid(source);
        grid.Scroll.ContentHeight = 200 * grid.Style.RowHeight + grid.Style.HeaderHeight;
        source.SearchText = "needle";

        Assert.True(grid.Next());

        Assert.Equal(150, grid.Selection.CurrentCell.Row);
        Assert.True(grid.Scroll.OffsetY > 0);
    }

    [Fact]
    public void MatchOrdinal_CountsTheMatchesBeforeTheCurrentCell()
    {
        var (source, _, _, _) = NewSource();
        var grid = new Grid(source);
        source.SearchText = "o";

        grid.Next();
        grid.Next();
        grid.Next(); // Bruno.City: the third match

        Assert.Equal(3, source.GetSearchMatchOrdinal(grid.Selection.CurrentCell));
        Assert.Equal(0, source.GetSearchMatchOrdinal(new CellPosition(2, 0))); // Carla.Name is no match
    }

    // ── Announcements ────────────────────────────────────────────

    [Fact]
    public void Announcements_DescribeTheCountAndTheMatch()
    {
        var strings = KumikoStrings.Default;
        var column = new KumikoColumn { Header = "City" };

        Assert.Equal("No matches", KumikoAnnouncements.SearchMatchCount(strings, 0));
        Assert.Equal("12 matches", KumikoAnnouncements.SearchMatchCount(strings, 12));
        Assert.Equal("City: Oslo, row 4", KumikoAnnouncements.SearchMatch(strings, column, 1, 3, "Oslo"));
    }
}
