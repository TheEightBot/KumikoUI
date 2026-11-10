using System.Collections.ObjectModel;
using System.Windows.Input;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Components;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Input;

/// <summary>
/// Action buttons as an interactive cell (#35): a tap runs the button without an editor or any edit event,
/// <c>CanExecute</c> gates it and draws it disabled, <c>CanExecuteChanged</c> redraws, and a command that
/// removes its own row (even the last one) is safe.
/// </summary>
public class ActionButtonsInteractionTests
{
    // The Name column is the action column: cell x 0-120, rendered rect 8-112, two buttons 12-57 and 63-108.
    private const float First = 30, Second = 90;

    private sealed class TestCommand : ICommand
    {
        private readonly Func<object?, bool> _canExecute;
        private readonly Action<object?>? _execute;
        public TestCommand(Func<object?, bool>? canExecute = null, Action<object?>? execute = null)
        {
            _canExecute = canExecute ?? (_ => true);
            _execute = execute;
        }
        public List<object?> Executed { get; } = new();
        public int Subscribers { get; private set; }
        private EventHandler? _changed;
        public event EventHandler? CanExecuteChanged
        {
            add { _changed += value; Subscribers++; }
            remove { _changed -= value; Subscribers--; }
        }
        public bool CanExecute(object? parameter) => _canExecute(parameter);
        public void Execute(object? parameter) { Executed.Add(parameter); _execute?.Invoke(parameter); }
        public void RaiseCanExecuteChanged() => _changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Fixture
    {
        public InputGrid Grid { get; }
        public ActionButtonsCellRenderer Buttons { get; }
        public ObservableCollection<InputGrid.Item> Rows { get; }
        public List<string> Events { get; } = new();

        public Fixture(TestCommand first, TestCommand? second = null, int rows = 8)
        {
            Buttons = new ActionButtonsCellRenderer
            {
                Buttons =
                {
                    new ActionButtonDefinition { Label = "Details", Command = first },
                    new ActionButtonDefinition { Label = "Delete", Command = second },
                },
            };
            InputGrid? grid = null;
            Grid = grid = new InputGrid(rows, configure: g =>
            {
                g.Name.PropertyName = "";
                g.Name.ColumnType = KumikoColumnType.Template;
                g.Name.CustomCellRenderer = Buttons;
                g.Name.EditTriggers = EditTrigger.SingleTap | EditTrigger.DoubleTap | EditTrigger.F2Key;
            });
            Rows = new ObservableCollection<InputGrid.Item>(Grid.Items);
            Grid.Source.SetItems(Rows);
            Grid.Render();

            Grid.Edit.CellBeginEdit += (_, _) => Events.Add("CellBeginEdit");
            Grid.Edit.CellEndEdit += (_, _) => Events.Add("CellEndEdit");
            Grid.Edit.CellValueChanged += (_, _) => Events.Add("CellValueChanged");
            Grid.Source.ViewChanged += (_, _) => Events.Add("ViewChanged");
        }
    }

    [Fact]
    public void Tap_RunsTheButtonWithTheRowItem_WithoutAnEditorOrEditEvents()
    {
        var details = new TestCommand();
        var f = new Fixture(details);
        var row = f.Rows[2];

        f.Grid.Tap(First, f.Grid.RowY(2));

        Assert.Same(row, Assert.Single(details.Executed));
        Assert.False(f.Grid.Edit.IsEditing);
        Assert.False(f.Grid.Selection.IsEditing);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void Tap_OnTheSecondButton_RunsOnlyThatButton()
    {
        var details = new TestCommand();
        var delete = new TestCommand();
        var f = new Fixture(details, delete);

        f.Grid.Tap(Second, f.Grid.RowY(1));

        Assert.Empty(details.Executed);
        Assert.Same(f.Rows[1], Assert.Single(delete.Executed));
    }

    [Fact]
    public void Tap_BetweenOrOutsideTheButtons_RunsNothing()
    {
        var details = new TestCommand();
        var delete = new TestCommand();
        var f = new Fixture(details, delete);

        f.Grid.Tap(60, f.Grid.RowY(1)); // the gap
        f.Grid.Tap(2, f.Grid.RowY(1));  // the cell padding

        Assert.Empty(details.Executed);
        Assert.Empty(delete.Executed);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void DoubleTap_RunsTheButtonOnceAndOpensNoEditor()
    {
        var details = new TestCommand();
        var f = new Fixture(details);

        f.Grid.Tap(First, f.Grid.RowY(0), clicks: 1);
        f.Grid.Tap(First, f.Grid.RowY(0), clicks: 2);

        Assert.Single(details.Executed);
        Assert.False(f.Grid.Edit.IsEditing);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void ReadOnlyColumn_StillRunsItsButtons()
    {
        var details = new TestCommand();
        var f = new Fixture(details);
        f.Grid.Name.IsReadOnly = true;

        f.Grid.Tap(First, f.Grid.RowY(0));

        Assert.Single(details.Executed);
    }

    [Fact]
    public void F2AndTyping_NeverOpenAnEditorOnAnActionCell()
    {
        var f = new Fixture(new TestCommand());
        f.Grid.Select(0, 0);

        f.Grid.Key(KumikoKey.F2);
        f.Grid.Type('x');

        Assert.False(f.Grid.Edit.IsEditing);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void ColumnWithItsOwnEditorFactory_StillOpensTheEditor()
    {
        var details = new TestCommand();
        var f = new Fixture(details);
        f.Grid.Name.CustomEditorFactory = (value, bounds) => new DrawnActionButtons
        {
            Bounds = bounds, RowItem = value, Buttons = f.Buttons.Buttons,
        };

        f.Grid.Tap(First, f.Grid.RowY(0));

        // The editor opened and forwarded the tap: the command ran once, through the editor
        Assert.Single(details.Executed);
        Assert.Contains("CellBeginEdit", f.Events);
    }

    // ── CanExecute ──

    [Fact]
    public void CanExecuteFalse_BlocksTheTap()
    {
        var details = new TestCommand(canExecute: _ => false);
        var f = new Fixture(details);

        f.Grid.Tap(First, f.Grid.RowY(0));

        Assert.Empty(details.Executed);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void CanExecute_ReceivesTheRowItem_OrTheExplicitParameter()
    {
        object? seen = null;
        var command = new TestCommand(canExecute: p => { seen = p; return true; });
        var f = new Fixture(command);

        f.Grid.Tap(First, f.Grid.RowY(3));
        Assert.Same(f.Rows[3], seen);

        ((ActionButtonDefinition)f.Buttons.Buttons[0]).CommandParameter = "explicit";
        f.Grid.Tap(First, f.Grid.RowY(3));
        Assert.Equal("explicit", seen);
        Assert.Equal("explicit", command.Executed[^1]);
    }

    [Fact]
    public void CanExecuteDependsOnTheRow()
    {
        var delete = new TestCommand(canExecute: p => ((InputGrid.Item)p!).Age != 21);
        var f = new Fixture(new TestCommand(), delete);

        f.Grid.Tap(Second, f.Grid.RowY(1)); // Age 21: blocked
        f.Grid.Tap(Second, f.Grid.RowY(2)); // Age 22: runs

        Assert.Same(f.Rows[2], Assert.Single(delete.Executed));
    }

    [Fact]
    public void ActionOnlyButton_IsAlwaysEnabled()
    {
        int fired = 0;
        var f = new Fixture(new TestCommand());
        f.Buttons.Buttons.Add(new ActionButtonDefinition { Label = "Act", Action = () => fired++ });
        // Three buttons now: 12-44, 50-82, 88-108... the third is centered at about x 98
        f.Grid.Tap(98, f.Grid.RowY(0));

        Assert.Equal(1, fired);
    }

    [Fact]
    public void DisabledButton_IsDrawnWithTheStyleDisabledColors_AndEnabledOnesAreNot()
    {
        var disabled = new TestCommand(canExecute: _ => false);
        var enabled = new TestCommand();
        var f = new Fixture(disabled, enabled);
        f.Grid.Style.ActionButtonDisabledBackgroundColor = new KumikoColor(1, 2, 3);
        f.Grid.Style.ActionButtonDisabledTextColor = new KumikoColor(4, 5, 6);

        var ctx = new RecordingDrawingContext();
        f.Grid.Renderer.Render(ctx, f.Grid.Source, f.Grid.Scroll, f.Grid.Selection, f.Grid.Style, editSession: f.Grid.Edit);

        var firstRow = ctx.Texts.Where(t => t.Text == "Details").First();
        Assert.Equal(new KumikoColor(4, 5, 6), firstRow.Color);
        Assert.Contains(ctx.Fills, o => o.Kind == DrawOpKind.FillRoundRect && o.Color == new KumikoColor(1, 2, 3));
        var deleteText = ctx.Texts.Where(t => t.Text == "Delete").First();
        Assert.Equal(KumikoColor.White, deleteText.Color);
    }

    [Theory]
    [InlineData(KumikoThemeMode.Light)]
    [InlineData(KumikoThemeMode.Dark)]
    [InlineData(KumikoThemeMode.HighContrast)]
    public void EveryTheme_DefinesDistinctDisabledColors(KumikoThemeMode mode)
    {
        var style = KumikoTheme.Create(mode);

        Assert.NotEqual(style.ActionButtonDisabledBackgroundColor, style.ActionButtonDisabledTextColor);
        Assert.NotEqual(new KumikoColor(13, 110, 253), style.ActionButtonDisabledBackgroundColor);
    }

    [Fact]
    public void CanExecuteChanged_RedrawsTheGrid_AndTheButtonFollowsTheNewState()
    {
        bool allowed = false;
        var details = new TestCommand(canExecute: _ => allowed);
        var f = new Fixture(details);
        int redraws = 0;
        f.Grid.Renderer.RedrawRequested += () => redraws++;

        allowed = true;
        details.RaiseCanExecuteChanged();

        Assert.Equal(1, redraws);
        f.Grid.Tap(First, f.Grid.RowY(0));
        Assert.Single(details.Executed);
    }

    [Fact]
    public void Dispose_UnsubscribesFromEveryCommand()
    {
        var details = new TestCommand();
        var delete = new TestCommand();
        var f = new Fixture(details, delete);
        Assert.Equal(1, details.Subscribers);
        Assert.Equal(1, delete.Subscribers);

        f.Buttons.Dispose();

        Assert.Equal(0, details.Subscribers);
        Assert.Equal(0, delete.Subscribers);
        int redraws = 0;
        f.Grid.Renderer.RedrawRequested += () => redraws++;
        details.RaiseCanExecuteChanged();
        Assert.Equal(0, redraws);
    }

    [Fact]
    public void ReplacingTheButtons_UnsubscribesFromTheOldCommands()
    {
        var details = new TestCommand();
        var f = new Fixture(details);
        Assert.Equal(1, details.Subscribers);

        f.Buttons.Buttons = new List<IActionButtonDefinition>();

        Assert.Equal(0, details.Subscribers);
    }

    [Fact]
    public void RendererDispose_StopsTheRendererForwarding()
    {
        var details = new TestCommand();
        var f = new Fixture(details);
        int redraws = 0;
        f.Grid.Renderer.RedrawRequested += () => redraws++;

        f.Grid.Renderer.Dispose();
        details.RaiseCanExecuteChanged();

        Assert.Equal(0, redraws);
    }

    [Fact]
    public void RendersTwiceAndMany_SubscribesOncePerCommand()
    {
        var details = new TestCommand();
        var f = new Fixture(details);

        for (int i = 0; i < 5; i++) f.Grid.Render();

        Assert.Equal(1, details.Subscribers);
    }

    // ── A command that removes its own row ──

    [Fact]
    public void DeletingTheLastRow_FromTheCommand_DoesNotThrow()
    {
        var f = new Fixture(new TestCommand(), new TestCommand(), rows: 3);
        var remove = new TestCommand(execute: p => f.Rows.Remove((InputGrid.Item)p!));
        ((ActionButtonDefinition)f.Buttons.Buttons[1]).Command = remove;
        f.Grid.Select(2, 0);

        var ex = Record.Exception(() => f.Grid.Tap(Second, f.Grid.RowY(2)));

        Assert.Null(ex);
        Assert.Equal(2, f.Rows.Count);
        Assert.Equal(2, f.Grid.Source.RowCount);
        Assert.False(f.Grid.Edit.IsEditing);
        // The grid is still usable afterwards
        Assert.Null(Record.Exception(() => f.Grid.Render()));
        Assert.Null(Record.Exception(() => f.Grid.Key(KumikoKey.Down)));
        Assert.Null(Record.Exception(() => f.Grid.Tap(Second, f.Grid.RowY(1))));
        Assert.Equal(1, f.Rows.Count);
    }

    [Fact]
    public void DeletingTheOnlyRow_FromTheCommand_DoesNotThrow()
    {
        var f = new Fixture(new TestCommand(), new TestCommand(), rows: 1);
        var remove = new TestCommand(execute: p => f.Rows.Remove((InputGrid.Item)p!));
        ((ActionButtonDefinition)f.Buttons.Buttons[1]).Command = remove;

        var ex = Record.Exception(() => f.Grid.Tap(Second, f.Grid.RowY(0)));

        Assert.Null(ex);
        Assert.Equal(0, f.Grid.Source.RowCount);
        Assert.Null(Record.Exception(() => f.Grid.Render()));
    }

    [Fact]
    public void DeletingARow_ViaEnter_DoesNotThrow()
    {
        var f = new Fixture(new TestCommand(), rows: 1);
        var remove = new TestCommand(execute: p => f.Rows.Remove((InputGrid.Item)p!));
        ((ActionButtonDefinition)f.Buttons.Buttons[0]).Command = remove;
        f.Grid.Select(0, 0);

        var ex = Record.Exception(() => f.Grid.Key(KumikoKey.Enter));

        Assert.Null(ex);
        Assert.Empty(f.Rows);
    }

    // ── Keyboard ──

    [Fact]
    public void Enter_OnTheActionCell_RunsTheFirstButton_WithoutAnEditor()
    {
        var details = new TestCommand();
        var delete = new TestCommand();
        var f = new Fixture(details, delete);
        f.Grid.Select(3, 0);

        var e = f.Grid.Key(KumikoKey.Enter);

        Assert.True(e.Handled);
        Assert.Same(f.Rows[3], Assert.Single(details.Executed));
        Assert.Empty(delete.Executed);
        Assert.False(f.Grid.Edit.IsEditing);
        Assert.Empty(f.Events);
    }

    [Fact]
    public void Enter_WhenTheFirstButtonIsDisabled_DoesNothing()
    {
        var details = new TestCommand(canExecute: _ => false);
        var delete = new TestCommand();
        var f = new Fixture(details, delete);
        f.Grid.Select(0, 0);

        var e = f.Grid.Key(KumikoKey.Enter);

        Assert.False(e.Handled);
        Assert.Empty(details.Executed);
        Assert.Empty(delete.Executed);
    }

    [Fact]
    public void Space_OnTheActionCell_StillTogglesTheRowSelection()
    {
        var details = new TestCommand();
        var f = new Fixture(details);
        f.Grid.Select(1, 0);

        f.Grid.Key(KumikoKey.Space);

        Assert.Empty(details.Executed);
        Assert.Contains(1, f.Grid.Selection.SelectedRows);
    }

    [Fact]
    public void Enter_OnAnOrdinaryCell_StillOpensTheEditor()
    {
        var f = new Fixture(new TestCommand());
        f.Grid.Select(0, 2);

        f.Grid.Key(KumikoKey.Enter);

        Assert.True(f.Grid.Edit.IsEditing);
    }

    // ── The legacy editor ──

    [Fact]
    public void Editor_DisabledButton_DoesNotRun_ButCompletesTheEdit()
    {
        var command = new TestCommand(canExecute: _ => false);
        var bar = new DrawnActionButtons
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Buttons = new List<IActionButtonDefinition> { new ActionButtonDefinition { Label = "Go", Command = command } },
        };
        int completed = 0;
        bar.EditCompleted += () => completed++;

        bar.Tap(100, 15);

        Assert.Empty(command.Executed);
        Assert.Equal(1, completed);
    }

    [Fact]
    public void Editor_DisabledButton_IsDrawnDisabled_FromTheAppliedTheme()
    {
        var command = new TestCommand(canExecute: _ => false);
        var bar = new DrawnActionButtons
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Buttons = new List<IActionButtonDefinition> { new ActionButtonDefinition { Label = "Go", Command = command } },
        };
        bar.ApplyTheme(new KumikoStyle { ActionButtonDisabledBackgroundColor = new KumikoColor(7, 8, 9) });

        var ctx = new RecordingDrawingContext();
        bar.OnDraw(ctx);

        Assert.Equal(new KumikoColor(7, 8, 9), Assert.Single(ctx.Fills).Color);
    }
}
