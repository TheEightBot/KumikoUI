using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>
/// Tests that verify the column-level EditTrigger resolution semantics:
///   - null column.EditTriggers → falls back to grid-level value
///   - non-null column.EditTriggers → fully overrides grid-level value
///   - EditTrigger.None on a column blocks all trigger-based editing for that column
/// The resolution logic (GetEffectiveEditTriggers) lives in GridInputController but the
/// semantics are fully expressed through the DataGridColumn model and are validated here
/// using a simple inline resolution helper that mirrors the production code exactly.
/// </summary>
public class EditTriggerResolutionTests
{
    // ── Mirrors the production resolution rule ──────────────────────────────
    private static EditTrigger Resolve(DataGridColumn? column, EditTrigger gridLevel)
        => column?.EditTriggers ?? gridLevel;

    // ── Null column (no column in scope) ────────────────────────────────────

    [Fact]
    public void Resolve_NullColumn_ReturnsGridLevel()
    {
        var result = Resolve(null, EditTrigger.Default);
        Assert.Equal(EditTrigger.Default, result);
    }

    [Fact]
    public void Resolve_NullColumn_NoneGridLevel_ReturnsNone()
    {
        var result = Resolve(null, EditTrigger.None);
        Assert.Equal(EditTrigger.None, result);
    }

    // ── Column with null EditTriggers (inherit) ──────────────────────────────

    [Fact]
    public void Resolve_ColumnWithNullEditTriggers_ReturnsGridLevel()
    {
        var col = new DataGridColumn { EditTriggers = null };
        var result = Resolve(col, EditTrigger.Default);
        Assert.Equal(EditTrigger.Default, result);
    }

    [Theory]
    [InlineData(EditTrigger.None)]
    [InlineData(EditTrigger.SingleTap)]
    [InlineData(EditTrigger.DoubleTap)]
    [InlineData(EditTrigger.F2Key)]
    [InlineData(EditTrigger.Typing)]
    [InlineData(EditTrigger.LongPress)]
    [InlineData(EditTrigger.Default)]
    public void Resolve_ColumnWithNullEditTriggers_InheritsAnyGridLevel(EditTrigger gridLevel)
    {
        var col = new DataGridColumn { EditTriggers = null };
        Assert.Equal(gridLevel, Resolve(col, gridLevel));
    }

    // ── Column with explicit EditTriggers (override) ─────────────────────────

    [Fact]
    public void Resolve_ColumnOverrideNone_BlocksEvenWhenGridAllowsAll()
    {
        var col = new DataGridColumn { EditTriggers = EditTrigger.None };
        var result = Resolve(col, EditTrigger.Default);
        Assert.Equal(EditTrigger.None, result);
        Assert.Equal(0, (int)result);   // no flags set
    }

    [Fact]
    public void Resolve_ColumnOverrideSingleTap_IgnoresGridDoubleTap()
    {
        var col = new DataGridColumn { EditTriggers = EditTrigger.SingleTap };
        var result = Resolve(col, EditTrigger.DoubleTap | EditTrigger.F2Key | EditTrigger.Typing);
        Assert.Equal(EditTrigger.SingleTap, result);
        Assert.Equal(0, (int)(result & EditTrigger.DoubleTap));
    }

    [Fact]
    public void Resolve_ColumnOverrideF2Only_DoesNotHaveTypingFlag()
    {
        var col = new DataGridColumn { EditTriggers = EditTrigger.F2Key };
        var result = Resolve(col, EditTrigger.Default);
        Assert.True((result & EditTrigger.F2Key) != 0);
        Assert.Equal(0, (int)(result & EditTrigger.Typing));
        Assert.Equal(0, (int)(result & EditTrigger.DoubleTap));
    }

    [Fact]
    public void Resolve_ColumnOverrideCombined_ReturnsCombined()
    {
        var col = new DataGridColumn
        {
            EditTriggers = EditTrigger.SingleTap | EditTrigger.F2Key
        };
        var result = Resolve(col, EditTrigger.DoubleTap);   // grid has DoubleTap only
        Assert.Equal(EditTrigger.SingleTap | EditTrigger.F2Key, result);
        Assert.Equal(0, (int)(result & EditTrigger.DoubleTap));
    }

    // ── Different columns can carry independent trigger sets ─────────────────

    [Fact]
    public void Resolve_TwoColumns_ReturnIndependentValues()
    {
        var salary = new DataGridColumn { EditTriggers = EditTrigger.DoubleTap };
        var name   = new DataGridColumn { EditTriggers = EditTrigger.SingleTap | EditTrigger.F2Key };
        var id     = new DataGridColumn { EditTriggers = EditTrigger.None };
        var notes  = new DataGridColumn { EditTriggers = null };  // inherit

        EditTrigger gridLevel = EditTrigger.Default;

        Assert.Equal(EditTrigger.DoubleTap,                      Resolve(salary, gridLevel));
        Assert.Equal(EditTrigger.SingleTap | EditTrigger.F2Key,  Resolve(name,   gridLevel));
        Assert.Equal(EditTrigger.None,                            Resolve(id,     gridLevel));
        Assert.Equal(EditTrigger.Default,                         Resolve(notes,  gridLevel));
    }

    // ── Flag membership helpers ──────────────────────────────────────────────

    [Theory]
    [InlineData(EditTrigger.None,      EditTrigger.SingleTap,  false)]
    [InlineData(EditTrigger.None,      EditTrigger.DoubleTap,  false)]
    [InlineData(EditTrigger.None,      EditTrigger.F2Key,      false)]
    [InlineData(EditTrigger.None,      EditTrigger.Typing,     false)]
    [InlineData(EditTrigger.None,      EditTrigger.LongPress,  false)]
    [InlineData(EditTrigger.SingleTap, EditTrigger.SingleTap,  true)]
    [InlineData(EditTrigger.SingleTap, EditTrigger.DoubleTap,  false)]
    [InlineData(EditTrigger.Default,   EditTrigger.DoubleTap,  true)]
    [InlineData(EditTrigger.Default,   EditTrigger.F2Key,      true)]
    [InlineData(EditTrigger.Default,   EditTrigger.Typing,     true)]
    [InlineData(EditTrigger.Default,   EditTrigger.SingleTap,  false)]
    [InlineData(EditTrigger.Default,   EditTrigger.LongPress,  false)]
    public void FlagCheck_ReportsCorrectMembership(
        EditTrigger columnTrigger, EditTrigger flag, bool expected)
    {
        var col = new DataGridColumn { EditTriggers = columnTrigger };
        var effective = Resolve(col, EditTrigger.Default);
        Assert.Equal(expected, (effective & flag) != 0);
    }
}
