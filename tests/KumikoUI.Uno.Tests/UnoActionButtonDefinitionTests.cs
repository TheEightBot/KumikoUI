using System.Windows.Input;
using KumikoUI.Core.Rendering;
using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// Unit tests for <see cref="UnoActionButtonDefinition"/>: dependency property changes reach the
/// <see cref="IActionButtonDefinition"/> view the renderer reads, and a bound command executes (#64).
/// </summary>
public class UnoActionButtonDefinitionTests
{
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    private sealed class ViewModel
    {
        public ICommand? DetailsCommand { get; init; }
    }

    [Fact]
    public void Defaults_MatchTheMauiDefinition()
    {
        IActionButtonDefinition definition = new UnoActionButtonDefinition();

        Assert.Equal(string.Empty, definition.Label);
        Assert.Equal(new KumikoColor(13, 110, 253), definition.BackgroundColor);
        Assert.Equal(KumikoColor.White, definition.TextColor);
        Assert.Null(definition.Command);
        Assert.Null(definition.CommandParameter);
        Assert.Null(definition.Action);
    }

    [Fact]
    public void PropertyChanges_PropagateToTheCoreInterface()
    {
        var command = new RecordingCommand();
        var definition = new UnoActionButtonDefinition
        {
            Label = "Delete",
            BackgroundColor = Color.FromArgb(255, 220, 53, 69),
            TextColor = Color.FromArgb(128, 1, 2, 3),
            Command = command,
            CommandParameter = 42,
        };
        IActionButtonDefinition core = definition;

        Assert.Equal("Delete", core.Label);
        Assert.Equal(new KumikoColor(220, 53, 69), core.BackgroundColor);
        Assert.Equal(new KumikoColor(1, 2, 3, 128), core.TextColor);
        Assert.Same(command, core.Command);
        Assert.Equal(42, core.CommandParameter);

        definition.Label = "Remove";
        definition.Command = null;
        Assert.Equal("Remove", core.Label);
        Assert.Null(core.Command);
    }

    [Fact]
    public void Changed_IsRaisedForEachPropertyChange()
    {
        var definition = new UnoActionButtonDefinition();
        int raised = 0;
        definition.Changed += (_, _) => raised++;

        definition.Label = "A";
        definition.BackgroundColor = Color.FromArgb(255, 1, 1, 1);
        definition.TextColor = Color.FromArgb(255, 2, 2, 2);
        definition.Command = new RecordingCommand();
        definition.CommandParameter = "p";

        Assert.Equal(5, raised);
    }

    [Fact]
    public void Command_ExecutesWithTheExplicitParameter()
    {
        var command = new RecordingCommand();
        var definition = new UnoActionButtonDefinition { Command = command, CommandParameter = "row-7" };
        IActionButtonDefinition core = definition;

        core.Command!.Execute(core.CommandParameter);

        Assert.Equal(new object?[] { "row-7" }, command.Parameters);
    }

    [Fact]
    public void BindingToCommand_ResolvesAgainstTheDataContext()
    {
        var command = new RecordingCommand();
        var definition = new UnoActionButtonDefinition();
        BindingOperations.SetBinding(definition, UnoActionButtonDefinition.CommandProperty,
            new Binding { Path = new Microsoft.UI.Xaml.PropertyPath(nameof(ViewModel.DetailsCommand)) });

        definition.DataContext = new ViewModel { DetailsCommand = command };

        Assert.Same(command, ((IActionButtonDefinition)definition).Command);
    }
}
