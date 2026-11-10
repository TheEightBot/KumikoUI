using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Tests.Input;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Which edit triggers start an edit, driven through the real input controller: a column's own
/// <see cref="KumikoColumn.EditTriggers"/> fully replaces the grid-level triggers, a null column value
/// inherits them, and <see cref="EditTrigger.None"/> on a column blocks every trigger for that column.
/// </summary>
public class EditTriggerResolutionTests
{
    private const EditTrigger Every = EditTrigger.SingleTap | EditTrigger.DoubleTap | EditTrigger.F2Key |
                                      EditTrigger.Typing | EditTrigger.LongPress;

    private static readonly EditTrigger[] Triggers =
    {
        EditTrigger.SingleTap, EditTrigger.DoubleTap, EditTrigger.F2Key, EditTrigger.Typing, EditTrigger.LongPress,
    };

    public static IEnumerable<object[]> EachTrigger() => Triggers.Select(t => new object[] { t });

    /// <summary>Performs the gesture for one trigger on the Name cell of row 2.</summary>
    private static void Perform(InputGrid g, EditTrigger trigger)
    {
        switch (trigger)
        {
            case EditTrigger.SingleTap: g.Tap(60, g.RowY(2)); break;
            case EditTrigger.DoubleTap: g.Tap(60, g.RowY(2), clicks: 2); break;
            case EditTrigger.F2Key: g.Select(2, 0); g.Key(KumikoKey.F2); break;
            case EditTrigger.Typing: g.Select(2, 0); g.Type('x'); break;
            case EditTrigger.LongPress: g.Pointer(InputAction.LongPress, 60, g.RowY(2)); break;
            default: throw new ArgumentOutOfRangeException(nameof(trigger));
        }
    }

    [Theory]
    [MemberData(nameof(EachTrigger))]
    public void GridLevelTrigger_StartsAnEditOnItsGesture(EditTrigger trigger)
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = trigger);

        Perform(g, trigger);

        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 0), g.Edit.EditCell);
    }

    [Theory]
    [MemberData(nameof(EachTrigger))]
    public void GridLevelNone_StartsNoEdit_ForAnyGesture(EditTrigger trigger)
    {
        var g = new InputGrid(configure: x => x.Edit.EditTriggers = EditTrigger.None);

        Perform(g, trigger);

        Assert.False(g.Edit.IsEditing);
    }

    [Theory]
    [MemberData(nameof(EachTrigger))]
    public void ColumnNone_BlocksEveryTrigger_EvenWhenTheGridAllowsAll(EditTrigger trigger)
    {
        var g = new InputGrid(configure: x =>
        {
            x.Edit.EditTriggers = Every;
            x.Name.EditTriggers = EditTrigger.None;
        });

        Perform(g, trigger);

        Assert.False(g.Edit.IsEditing);
    }

    [Theory]
    [MemberData(nameof(EachTrigger))]
    public void ColumnOverride_ReplacesTheGridTriggers_NotAddsToThem(EditTrigger trigger)
    {
        // The grid allows only this trigger; the column allows only a different one.
        var other = Triggers.First(t => t != trigger);
        var g = new InputGrid(configure: x =>
        {
            x.Edit.EditTriggers = trigger;
            x.Name.EditTriggers = other;
        });

        Perform(g, trigger);
        Assert.False(g.Edit.IsEditing);

        var h = new InputGrid(configure: x =>
        {
            x.Edit.EditTriggers = trigger;
            x.Name.EditTriggers = other;
        });
        Perform(h, other);
        Assert.True(h.Edit.IsEditing);
    }

    [Theory]
    [MemberData(nameof(EachTrigger))]
    public void ColumnWithNoOverride_InheritsTheGridTrigger(EditTrigger trigger)
    {
        var g = new InputGrid(configure: x =>
        {
            x.Edit.EditTriggers = trigger;
            x.Name.EditTriggers = null;
        });

        Perform(g, trigger);

        Assert.True(g.Edit.IsEditing);
    }

    [Fact]
    public void DefaultTriggers_AreDoubleTapF2AndTyping_AndNotSingleTapOrLongPress()
    {
        foreach (var trigger in new[] { EditTrigger.DoubleTap, EditTrigger.F2Key, EditTrigger.Typing })
        {
            var g = new InputGrid();
            Perform(g, trigger);
            Assert.True(g.Edit.IsEditing, trigger.ToString());
        }

        foreach (var trigger in new[] { EditTrigger.SingleTap, EditTrigger.LongPress })
        {
            var g = new InputGrid();
            Perform(g, trigger);
            Assert.False(g.Edit.IsEditing, trigger.ToString());
        }
    }

    [Fact]
    public void ColumnsResolveTheirTriggersIndependently()
    {
        var g = new InputGrid(configure: x =>
        {
            x.Edit.EditTriggers = EditTrigger.DoubleTap;
            x.Name.EditTriggers = EditTrigger.None;
            x.Age.EditTriggers = EditTrigger.SingleTap;
        });

        g.Tap(60, g.RowY(2), clicks: 2); // Name: blocked
        Assert.False(g.Edit.IsEditing);

        g.Tap(150, g.RowY(2)); // Age: single tap edits
        Assert.True(g.Edit.IsEditing);
        Assert.Equal(new CellPosition(2, 1), g.Edit.EditCell);
    }
}
