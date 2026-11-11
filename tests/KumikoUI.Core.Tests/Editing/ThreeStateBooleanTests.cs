using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Three-state Boolean columns (<see cref="KumikoColumn.IsThreeState"/>, #38): a toggle cycles unchecked, checked,
/// indeterminate (null) through tap, Space and Enter, commits through the edit pipeline, and null sorts and
/// filters sensibly.
/// </summary>
public class ThreeStateBooleanTests
{
    private sealed class Flag
    {
        public bool? State { get; set; }
    }

    private sealed class Grid
    {
        public required KumikoColumn Column { get; init; }
        public required Flag[] Items { get; init; }
        public required KumikoSource Source { get; init; }
        public required KumikoSelectionModel Selection { get; init; }
        public required ScrollState Scroll { get; init; }
        public required KumikoStyle Style { get; init; }
        public required EditSession Edit { get; init; }
        public required KumikoInputController Input { get; init; }

        public static Grid Create(bool threeState = true, params bool?[] values)
        {
            var column = new KumikoColumn
            {
                Header = "State", PropertyName = "State", Width = 100,
                ColumnType = KumikoColumnType.Boolean, IsThreeState = threeState,
            };
            var items = values.Select(v => new Flag { State = v }).ToArray();
            var source = new KumikoSource();
            source.SetColumns(new[] { column });
            source.SetItems(items);
            var style = new KumikoStyle();
            var edit = new EditSession { Style = style };
            return new Grid
            {
                Column = column, Items = items, Source = source,
                Selection = new KumikoSelectionModel(source),
                Scroll = new ScrollState { ViewportWidth = 300, ViewportHeight = 200 },
                Style = style, Edit = edit, Input = new KumikoInputController { EditSession = edit },
            };
        }

        public float RowY(int row) => Style.HeaderHeight + row * Style.RowHeight + Style.RowHeight / 2;

        public void Tap(float x, float y)
        {
            foreach (var action in new[] { InputAction.Pressed, InputAction.Released })
                Input.HandlePointer(new KumikoPointerEventArgs { X = x, Y = y, Action = action, ClickCount = 1 },
                    Scroll, Selection, Style, Source);
        }

        public void Key(KumikoKey key)
        {
            var e = new KumikoKeyEventArgs { Key = key, Modifiers = InputModifiers.None };
            Input.HandleKey(e, Scroll, Selection, Style, Source);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, null)]
    [InlineData(null, false)]
    public void NextBooleanValue_ThreeState_CyclesUncheckedCheckedIndeterminate(bool? current, bool? expected) =>
        Assert.Equal(expected, EditSession.NextBooleanValue(current, isThreeState: true));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(null, true)]
    public void NextBooleanValue_TwoState_Flips(bool? current, bool expected) =>
        Assert.Equal(expected, EditSession.NextBooleanValue(current, isThreeState: false));

    [Fact]
    public void Tap_OnTheCheckBox_CyclesThroughAllThreeStates()
    {
        var g = Grid.Create(true, false);

        g.Tap(50, g.RowY(0));
        Assert.True(g.Items[0].State);
        g.Tap(50, g.RowY(0));
        Assert.Null(g.Items[0].State);
        g.Tap(50, g.RowY(0));
        Assert.False(g.Items[0].State);
    }

    [Fact]
    public void Space_CyclesTheCurrentCell()
    {
        var g = Grid.Create(true, false);
        g.Selection.CurrentCell = new CellPosition(0, 0);

        g.Key(KumikoKey.Space);
        Assert.True(g.Items[0].State);
        g.Key(KumikoKey.Space);
        Assert.Null(g.Items[0].State);
        g.Key(KumikoKey.Space);
        Assert.False(g.Items[0].State);
    }

    [Fact]
    public void Enter_CyclesTheCurrentCell_FromIndeterminateToUnchecked()
    {
        var g = Grid.Create(true, new bool?[] { null });
        g.Selection.CurrentCell = new CellPosition(0, 0);

        g.Key(KumikoKey.Enter);

        Assert.False(g.Items[0].State);
    }

    [Fact]
    public void TwoStateColumn_NullBecomesCheckedAndNeverReturnsToNull()
    {
        var g = Grid.Create(false, new bool?[] { null });
        g.Selection.CurrentCell = new CellPosition(0, 0);

        g.Key(KumikoKey.Space);
        Assert.True(g.Items[0].State);
        g.Key(KumikoKey.Space);
        Assert.False(g.Items[0].State);
    }

    [Fact]
    public void Toggle_RaisesCellValueChangedWithTheNullValue()
    {
        var g = Grid.Create(true, true);
        CellValueChangedEventArgs? seen = null;
        g.Edit.CellValueChanged += (_, e) => seen = e;

        g.Edit.ToggleBooleanCell(0, 0, g.Column, g.Source);

        Assert.NotNull(seen);
        Assert.Equal(true, seen!.OldValue);
        Assert.Null(seen.NewValue);
    }

    [Fact]
    public void Toggle_ValidatorSeesTheIndeterminateValue_AndCanRefuseIt()
    {
        var g = Grid.Create(true, true);
        g.Edit.CellValidator = (_, _, value) =>
            value is null ? CellValidationResult.Error("No indeterminate") : CellValidationResult.Valid;

        g.Edit.ToggleBooleanCell(0, 0, g.Column, g.Source);

        Assert.True(g.Items[0].State);
    }

    [Fact]
    public void Toggle_ReadOnlyColumn_DoesNotCycle()
    {
        var g = Grid.Create(true, false);
        g.Column.IsReadOnly = true;
        g.Selection.CurrentCell = new CellPosition(0, 0);

        g.Key(KumikoKey.Space);

        Assert.False(g.Items[0].State);
    }

    [Fact]
    public void Sort_PutsIndeterminateFirst_ThenUncheckedThenChecked()
    {
        var g = Grid.Create(true, true, false, null, true, null);

        g.Source.ToggleSort(g.Column); // first tap on a Boolean column is checked-first (descending)
        g.Source.ToggleSort(g.Column); // second tap: ascending

        var order = Enumerable.Range(0, g.Source.RowCount).Select(i => ((Flag)g.Source.GetItem(i)).State).ToArray();
        Assert.Equal(new bool?[] { null, null, false, true, true }, order);
    }

    [Fact]
    public void Filter_ByDisplayValue_KeepsIndeterminateRowsOnTheBlankEntry()
    {
        var g = Grid.Create(true, true, false, null, null);

        g.Source.SetColumnFilter(g.Column, new FilterDescription(g.Column) { SelectedValues = new HashSet<string> { "" } });

        Assert.Equal(2, g.Source.RowCount);
        Assert.All(Enumerable.Range(0, 2), i => Assert.Null(((Flag)g.Source.GetItem(i)).State));
    }
}
