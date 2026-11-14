using System.Windows.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Maui.Tests;

/// <summary>
/// Tests for <see cref="MauiActionButtonDefinition"/>: its MAUI <c>Color</c> properties reach the renderer as
/// <see cref="KumikoColor"/> values through <see cref="IActionButtonDefinition"/> (#112).
/// </summary>
public class MauiActionButtonDefinitionTests
{
    [Fact]
    public void Defaults_AreTheBlueButtonWithWhiteText()
    {
        IActionButtonDefinition core = new MauiActionButtonDefinition();

        Assert.Equal(new KumikoColor(13, 110, 253), core.BackgroundColor);
        Assert.Equal(KumikoColor.White, core.TextColor);
    }

    [Fact]
    public void ColorChanges_ReachTheCoreColors()
    {
        var definition = new MauiActionButtonDefinition
        {
            BackgroundColor = Color.FromRgba(220, 53, 69, 128),
            TextColor = Colors.Black,
        };
        IActionButtonDefinition core = definition;

        Assert.Equal(new KumikoColor(220, 53, 69, 128), core.BackgroundColor);
        Assert.Equal(new KumikoColor(0, 0, 0), core.TextColor);
    }

    [Fact]
    public void HexText_ConvertsLikeAnyMauiColor()
    {
        var definition = new MauiActionButtonDefinition { BackgroundColor = Color.FromArgb("#0D6EFD") };

        Assert.Equal(new KumikoColor(13, 110, 253), ((IActionButtonDefinition)definition).BackgroundColor);
    }

    // ── The grid's BindingContext and redraws (API review F3) ─────

    private sealed class RecordingCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) { }
    }

    private sealed class ViewModel
    {
        public ICommand DetailsCommand { get; } = new RecordingCommand();

        public string DetailsLabel { get; set; } = "Details";
    }

    private static (KumikoDataGrid Grid, MauiActionButtonDefinition Button) ActionsGrid()
    {
        var button = new MauiActionButtonDefinition();
        button.SetBinding(MauiActionButtonDefinition.CommandProperty, nameof(ViewModel.DetailsCommand));
        button.SetBinding(MauiActionButtonDefinition.LabelProperty, nameof(ViewModel.DetailsLabel));
        var grid = new KumikoDataGrid { AnnounceChanges = false };
        grid.Columns.Add(new KumikoColumn
        {
            Header = "Actions",
            PropertyName = "",
            ColumnType = KumikoColumnType.Template,
            CustomCellRenderer = new ActionButtonsCellRenderer { Buttons = new List<IActionButtonDefinition> { button } },
        });
        return (grid, button);
    }

    [Fact]
    public void TheGridsBindingContext_ReachesTheDefinitionsOfItsColumns()
    {
        var (grid, button) = ActionsGrid();
        var viewModel = new ViewModel();

        grid.BindingContext = viewModel;

        Assert.Same(viewModel, button.BindingContext);
        Assert.Same(viewModel.DetailsCommand, button.Command);
        Assert.Equal("Details", button.Label);

        var other = new ViewModel { DetailsLabel = "Open" };
        grid.BindingContext = other;
        Assert.Same(other.DetailsCommand, button.Command);
        Assert.Equal("Open", button.Label);
    }

    [Fact]
    public void ADefinitionAddedToAColumnLater_GetsTheBindingContextToo()
    {
        var viewModel = new ViewModel();
        var grid = new KumikoDataGrid { AnnounceChanges = false, BindingContext = viewModel };
        var button = new MauiActionButtonDefinition();
        button.SetBinding(MauiActionButtonDefinition.CommandProperty, nameof(ViewModel.DetailsCommand));

        grid.Columns.Add(new KumikoColumn
        {
            Header = "Actions",
            PropertyName = "",
            CustomCellRenderer = new ActionButtonsCellRenderer { Buttons = new List<IActionButtonDefinition> { button } },
        });

        Assert.Same(viewModel.DetailsCommand, button.Command);
    }

    [Fact]
    public void AnyPropertyChange_RaisesChanged_ButABindingContextChangeDoesNot()
    {
        var button = new MauiActionButtonDefinition();
        int raised = 0;
        button.Changed += (_, _) => raised++;

        button.Label = "A";
        button.BackgroundColor = Colors.Red;
        button.TextColor = Colors.Black;
        button.Command = new RecordingCommand();
        button.CommandParameter = 1;
        Assert.Equal(5, raised);

        button.BindingContext = new object();
        Assert.Equal(5, raised);
    }

    [Fact]
    public void AGridThatLeftThePage_NoLongerListensToItsDefinitions()
    {
        var (grid, button) = ActionsGrid();
        grid.BindingContext = new ViewModel();
        var changed = typeof(MauiActionButtonDefinition).GetField("Changed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.NotNull(changed.GetValue(button)); // the grid redraws on a change

        var unsubscribe = typeof(KumikoDataGrid).GetMethod("UnsubscribeActionButtonDefinitions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        unsubscribe.Invoke(grid, null);

        Assert.Null(changed.GetValue(button));
    }
}
