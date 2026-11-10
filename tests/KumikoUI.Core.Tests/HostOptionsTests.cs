using System.Windows.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

/// <summary>The shared logic behind the hosts' bindable options (#58): style overrides, group sync, commands.</summary>
public class HostOptionsTests
{
    // ── KumikoStyleOptions ──

    [Fact]
    public void Apply_WithNothingSet_LeavesTheStyleUntouched()
    {
        var style = KumikoTheme.Create(KumikoThemeMode.Dark);
        var before = (style.ShowHorizontalGridLines, style.ShowVerticalGridLines, style.AllowRowDragDrop,
            style.ShowRowDragHandle, style.AlternateRowBackground, style.AlternateRowColor);

        var options = new KumikoStyleOptions();
        options.Apply(style);

        Assert.False(options.HasOverrides);
        Assert.Equal(before, (style.ShowHorizontalGridLines, style.ShowVerticalGridLines, style.AllowRowDragDrop,
            style.ShowRowDragHandle, style.AlternateRowBackground, style.AlternateRowColor));
    }

    [Fact]
    public void Apply_WritesOnlyTheSettingsThatAreSet()
    {
        var style = new KumikoStyle();
        var options = new KumikoStyleOptions
        {
            ShowHorizontalGridLines = false,
            ShowRowDragHandle = true,
        };

        options.Apply(style);

        Assert.True(options.HasOverrides);
        Assert.False(style.ShowHorizontalGridLines);
        Assert.True(style.ShowVerticalGridLines);     // not set: the style default stays
        Assert.False(style.AllowRowDragDrop);
        Assert.True(style.ShowRowDragHandle);
    }

    [Fact]
    public void Apply_AlternateRowColor_SetsTheColorAndTurnsAlternationOn()
    {
        var style = new KumikoStyle { AlternateRowBackground = false };
        var color = new KumikoColor(10, 20, 30);

        new KumikoStyleOptions { AlternateRowBackground = color }.Apply(style);

        Assert.True(style.AlternateRowBackground);
        Assert.Equal(color, style.AlternateRowColor);
    }

    [Fact]
    public void Apply_ReappliedAfterAThemeChange_WinsOverTheTheme()
    {
        var options = new KumikoStyleOptions { ShowVerticalGridLines = false, AllowRowDragDrop = true };
        var light = KumikoTheme.Create(KumikoThemeMode.Light);
        options.Apply(light);

        var dark = KumikoTheme.Create(KumikoThemeMode.Dark);
        Assert.True(dark.ShowVerticalGridLines);   // the new theme knows nothing about the options
        options.Apply(dark);

        Assert.False(dark.ShowVerticalGridLines);
        Assert.True(dark.AllowRowDragDrop);
    }

    [Fact]
    public void Apply_NullStyle_Throws()
        => Assert.Throws<ArgumentNullException>(() => new KumikoStyleOptions().Apply(null!));

    // ── Group sync ──

    private static KumikoSource SourceWithPeople()
    {
        var source = new KumikoSource();
        source.SetColumns(new[]
        {
            new KumikoColumn { PropertyName = "Name" },
            new KumikoColumn { PropertyName = "Department" },
            new KumikoColumn { PropertyName = "Age" },
        });
        source.SetItems(new[]
        {
            new Person { Name = "Alice", Age = 30, Department = "Engineering" },
            new Person { Name = "Bob", Age = 25, Department = "Marketing" },
            new Person { Name = "Charlie", Age = 35, Department = "Engineering" },
        });
        return source;
    }

    [Fact]
    public void SetGroupDescriptions_ReplacesTheGroupsWithOneNotification()
    {
        var source = SourceWithPeople();
        source.AddGroupDescription(new GroupDescription("Name"));
        int notifications = 0;
        source.ViewChanged += (_, _) => notifications++;

        source.SetGroupDescriptions(new[] { new GroupDescription("Department") });

        Assert.Equal(1, notifications);
        Assert.Single(source.GroupDescriptions);
        Assert.Equal("Department", source.GroupDescriptions[0].PropertyName);
        // 2 group headers + 3 items
        Assert.Equal(5, source.RowCount);
    }

    [Fact]
    public void SetGroupDescriptions_NullOrEmpty_Ungroups()
    {
        var source = SourceWithPeople();
        source.SetGroupDescriptions(new[] { new GroupDescription("Department") });

        source.SetGroupDescriptions(null);

        Assert.Empty(source.GroupDescriptions);
        Assert.Equal(3, source.RowCount);
    }

    [Fact]
    public void SetGroupSummaryRows_ReplacesTheRows()
    {
        var source = SourceWithPeople();
        source.AddGroupSummaryRow(new GroupSummaryRow("old"));
        int notifications = 0;
        source.ViewChanged += (_, _) => notifications++;

        source.SetGroupSummaryRows(new[] { new GroupSummaryRow("a"), new GroupSummaryRow("b") });

        Assert.Equal(1, notifications);
        Assert.Equal(new[] { "a", "b" }, source.GroupSummaryRows.Select(r => r.Name));

        source.SetGroupSummaryRows(null);
        Assert.Empty(source.GroupSummaryRows);
    }

    // ── KumikoCommands ──

    private sealed class RecordingCommand : ICommand
    {
        public bool Allowed { get; set; } = true;
        public List<object?> Executed { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Allowed;
        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    [Fact]
    public void TryExecute_RunsAnExecutableCommandWithTheParameter()
    {
        var command = new RecordingCommand();
        var parameter = new object();

        Assert.True(KumikoCommands.TryExecute(command, parameter));

        Assert.Same(parameter, Assert.Single(command.Executed));
    }

    [Fact]
    public void TryExecute_SkipsANullOrDisabledCommand()
    {
        var command = new RecordingCommand { Allowed = false };

        Assert.False(KumikoCommands.TryExecute(command, 1));
        Assert.False(KumikoCommands.TryExecute(null, 1));
        Assert.Empty(command.Executed);
    }
}
