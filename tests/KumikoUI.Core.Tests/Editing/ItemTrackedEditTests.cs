using System.Collections.ObjectModel;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// The edit session tracks the edited item, not a row index (#31): commits land on the item wherever it is,
/// edits on items that disappear are canceled, gestures that change the view commit first, and the editor
/// follows its cell while the grid scrolls.
/// </summary>
public class ItemTrackedEditTests
{
    private sealed class Grid
    {
        public ObservableCollection<Person> People { get; } = new()
        {
            new Person { Name = "Alice", Age = 30, Department = "Engineering" },
            new Person { Name = "Bob", Age = 25, Department = "Marketing" },
            new Person { Name = "Charlie", Age = 35, Department = "Engineering" },
            new Person { Name = "Diana", Age = 28, Department = "Marketing" },
            new Person { Name = "Eve", Age = 40, Department = "Engineering" },
        };

        public KumikoSource Source { get; } = new();
        public KumikoStyle Style { get; } = new();
        public ScrollState Scroll { get; } = new() { ViewportWidth = 400, ViewportHeight = 160 };
        public KumikoSelectionModel Selection { get; } = new();
        public EditSession Edit { get; } = new();
        public KumikoInputController Input { get; } = new();
        public KumikoRenderer Renderer { get; } = new();
        public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 150 };
        public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age", Width = 100, ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn Department { get; } = new() { Header = "Dept", PropertyName = "Department", Width = 150 };
        public List<CellEndEditEventArgs> Ended { get; } = new();

        public Grid()
        {
            Source.SetColumns(new[] { Name, Age, Department });
            Source.SetItems(People);
            Edit.Style = Style;
            Edit.CellEndEdit += (_, e) => Ended.Add(e);
            Input.EditSession = Edit;
            // What both hosts do: the selection follows its rows (the edit session follows on its own).
            Source.ViewChanged += (_, e) => Selection.ApplyViewChange(e);
            Render();
        }

        public RecordingDrawingContext Render()
        {
            var ctx = new RecordingDrawingContext();
            Renderer.Render(ctx, Source, Scroll, Selection, Style, editSession: Edit);
            return ctx;
        }

        public void Key(KumikoKey key, char? character = null, InputModifiers modifiers = InputModifiers.None) =>
            Input.HandleKey(new KumikoKeyEventArgs { Key = key, Character = character, Modifiers = modifiers },
                Scroll, Selection, Style, Source);

        public void Tap(float x, float y, int clicks = 1)
        {
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed, ClickCount = clicks },
                Scroll, Selection, Style, Source);
            Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released, ClickCount = clicks },
                Scroll, Selection, Style, Source);
        }

        /// <summary>Start editing a cell by typing a character into it.</summary>
        public void TypeInto(int row, int column, char character)
        {
            Selection.CurrentCell = new CellPosition(row, column);
            Key(KumikoKey.Character, character);
            Assert.True(Edit.IsEditing, "typing did not open an editor");
        }

        public string NameAt(int row) => (string)Source.GetCellValue(row, Name)!;
    }

    // ── Commits land on the edited item ───────────────────────────

    [Fact]
    public void EditThenSortThenEnter_WritesTheOriginalItem()
    {
        var g = new Grid();
        g.TypeInto(0, 0, 'Z'); // Alice → "Z"

        g.Source.SetSort(g.Age, KumikoSortDirection.Descending); // Eve, Charlie, Alice, Diana, Bob
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
        g.Key(KumikoKey.Enter);

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Z", g.People[0].Name);   // Alice's item
        Assert.Equal("Eve", g.People[4].Name); // not the item now on row 0
        Assert.Equal("Z", g.NameAt(2));
    }

    [Fact]
    public void EditThenFilterRemovesTheItem_CancelsWithoutWriting()
    {
        var g = new Grid();
        g.TypeInto(1, 0, 'Q'); // Bob

        g.Source.SetFilter(o => ((Person)o).Department == "Engineering");

        Assert.False(g.Edit.IsEditing);
        var ended = Assert.Single(g.Ended);
        Assert.False(ended.Committed);
        Assert.Equal("Bob", g.People[1].Name);
    }

    [Fact]
    public void BackgroundInsertAboveTheEdit_CommitStillWritesTheEditedItem()
    {
        var g = new Grid();
        g.TypeInto(2, 0, 'X'); // Charlie

        g.People.Insert(0, new Person { Name = "Zoe" });
        g.People.RemoveAt(2); // Bob
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell); // Zoe, Alice, Charlie…
        g.Key(KumikoKey.Enter);

        Assert.Equal("X", g.People.Single(p => p.Age == 35).Name);
        Assert.Equal("Zoe", g.People[0].Name);
    }

    [Fact]
    public void RemovingTheEditedItem_Cancels()
    {
        var g = new Grid();
        g.TypeInto(3, 0, 'D');

        g.People.RemoveAt(3);

        Assert.False(g.Edit.IsEditing);
        Assert.False(Assert.Single(g.Ended).Committed);
    }

    [Fact]
    public void ColumnReorder_TheEditFollowsItsColumn()
    {
        var g = new Grid();
        g.TypeInto(0, 0, 'Z'); // Name column

        g.Source.ReorderColumn(0, 2); // Age, Dept, Name

        Assert.Equal(new CellPosition(0, 2), g.Edit.EditCell);
        Assert.True(g.Edit.IsCellBeingEdited(0, 2));
        g.Key(KumikoKey.Enter);
        Assert.Equal("Z", g.People[0].Name);
    }

    // ── Gestures that change the view commit first ────────────────

    [Fact]
    public void HeaderTapToSort_CommitsTheEditFirst()
    {
        var g = new Grid();
        g.TypeInto(0, 0, 'Z'); // Alice → "Z"

        g.Tap(160, 20); // Age header

        Assert.False(g.Edit.IsEditing);
        Assert.True(Assert.Single(g.Ended).Committed);
        Assert.Equal("Z", g.People[0].Name);
        Assert.Equal(new[] { g.Age }, g.Source.ActiveSortColumns);
    }

    [Fact]
    public void HeaderTap_WithAnInvalidValue_KeepsEditingAndDoesNotSort()
    {
        var g = new Grid();
        g.Edit.CellValidator = (_, _, value) => (string?)value == "Z" ? CellValidationResult.Error("no") : CellValidationResult.Valid;
        g.TypeInto(0, 0, 'Z');

        g.Tap(160, 20);

        Assert.True(g.Edit.IsEditing);
        Assert.Empty(g.Source.ActiveSortColumns);
        Assert.Equal("Alice", g.People[0].Name);
    }

    [Fact]
    public void GroupChevronTap_CommitsTheEditFirst()
    {
        var g = new Grid();
        g.Source.AddGroupDescription(new GroupDescription("Department")); // Engineering header row 0, Alice row 1
        g.Render();
        g.TypeInto(1, 0, 'Z');

        g.Tap(20, g.Style.GroupPanelHeight + g.Style.HeaderHeight + 18); // Engineering header row

        Assert.False(g.Edit.IsEditing);
        Assert.Equal("Z", g.People[0].Name);
    }

    // ── The editor follows its cell ───────────────────────────────

    [Fact]
    public void ScrollWhileEditing_KeepsTheEditorOnTheCell_AndHidesItOutOfView()
    {
        var g = new Grid(); // 160 px viewport: header 40, rows 40–76, 76–112, 112–148, 148–184…
        for (int i = 0; i < 10; i++) g.People.Add(new Person { Name = $"Extra{i}" }); // room to scroll
        g.Render();
        g.TypeInto(2, 0, 'Q');
        g.Render();
        Assert.Equal(new KumikoRect(0, 112, 150, 36), g.Edit.ActiveEditor!.Bounds);

        g.Scroll.OffsetY = 36;
        g.Render();
        Assert.Equal(new KumikoRect(0, 76, 150, 36), g.Edit.ActiveEditor!.Bounds);
        Assert.True(g.Edit.ActiveEditor.IsVisible);

        g.Scroll.OffsetY = 120; // row 2 (112–148) is now fully above the data area
        var ctx = g.Render();
        Assert.False(g.Edit.ActiveEditor!.IsVisible);
        Assert.True(g.Edit.IsEditing);
        Assert.DoesNotContain(ctx.Fills, f => f.Rect.Equals(g.Edit.ActiveEditor.Bounds) && f.Color == g.Style.BackgroundColor);

        g.Scroll.OffsetY = 0;
        g.Render();
        Assert.True(g.Edit.ActiveEditor!.IsVisible);
        Assert.Equal(new KumikoRect(0, 112, 150, 36), g.Edit.ActiveEditor.Bounds);
    }

    [Fact]
    public void HorizontalScroll_HidesTheEditorWhenItsColumnLeavesThePane()
    {
        var g = new Grid();
        g.Scroll.ViewportWidth = 200;
        g.Render();
        g.TypeInto(0, 0, 'Q'); // Name, 0–150

        g.Scroll.OffsetX = 200; // content is 400 wide: max offset 200
        g.Render();

        Assert.False(g.Edit.ActiveEditor!.IsVisible);
    }

    [Fact]
    public void SortWhileEditing_MovesTheEditorWithTheRow()
    {
        var g = new Grid();
        g.TypeInto(0, 0, 'Z'); // Alice, row 0 at y 40

        g.Source.SetSort(g.Age, KumikoSortDirection.Descending); // Alice → row 2
        g.Render();

        Assert.Equal(40 + 2 * 36, g.Edit.ActiveEditor!.Bounds.Y);
    }

    // ── No stale-row exceptions from input ────────────────────────

    [Fact]
    public void SetCellValue_IgnoresRowsThatAreOutOfRangeOrNotData()
    {
        var g = new Grid();
        g.Source.AddGroupDescription(new GroupDescription("Department"));

        g.Source.SetCellValue(-1, g.Name, "x");
        g.Source.SetCellValue(99, g.Name, "x");
        g.Source.SetCellValue(0, g.Name, "x"); // group header

        Assert.DoesNotContain(g.People, p => p.Name == "x");
    }

    [Fact]
    public void RandomInputAndDataChanges_NeverThrow()
    {
        var random = new Random(31);
        var keys = new[]
        {
            KumikoKey.Up, KumikoKey.Down, KumikoKey.Left, KumikoKey.Right, KumikoKey.Home, KumikoKey.End,
            KumikoKey.PageUp, KumikoKey.PageDown, KumikoKey.Tab, KumikoKey.Enter, KumikoKey.Escape,
            KumikoKey.Space, KumikoKey.F2, KumikoKey.Delete, KumikoKey.Character,
        };

        int editsOpened = 0, viewChanges = 0;
        for (int run = 0; run < 20; run++)
        {
            var g = new Grid();
            g.Edit.CellBeginEdit += (_, _) => editsOpened++;
            g.Source.ViewChanged += (_, e) => { if (e.IsStructural) viewChanges++; };
            for (int step = 0; step < 300; step++)
            {
                switch (random.Next(10))
                {
                    case 0 or 1 or 2 or 3:
                        var key = keys[random.Next(keys.Length)];
                        g.Key(key, key == KumikoKey.Character ? (char)('a' + random.Next(26)) : null,
                            random.Next(4) == 0 ? InputModifiers.Shift : InputModifiers.None);
                        break;
                    case 4 or 5:
                        g.Tap(random.Next(0, 400), random.Next(0, 160), random.Next(1, 3));
                        break;
                    case 6:
                        g.Source.ToggleSort(g.Source.Columns[random.Next(g.Source.Columns.Count)]);
                        break;
                    case 7:
                        if (random.Next(2) == 0 && g.People.Count > 0) g.People.RemoveAt(random.Next(g.People.Count));
                        else g.People.Insert(random.Next(g.People.Count + 1), new Person { Name = "N", Age = random.Next(50), Department = random.Next(2) == 0 ? "Engineering" : "Sales" });
                        break;
                    case 8:
                        int threshold = random.Next(45);
                        g.Source.SetFilter(random.Next(3) == 0 ? null : o => ((Person)o).Age >= threshold);
                        break;
                    default:
                        if (g.Source.IsGroupingActive) g.Source.ClearGroupDescriptions();
                        else g.Source.AddGroupDescription(new GroupDescription("Department"));
                        break;
                }
                g.Render();
            }
        }

        // The sequence must actually exercise edits open across view changes.
        Assert.True(editsOpened > 100, $"only {editsOpened} edits opened");
        Assert.True(viewChanges > 1000, $"only {viewChanges} view changes");
    }
}
