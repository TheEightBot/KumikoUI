using System.Windows.Input;
using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// The action-buttons editor and its shared layout: press and release on a button fires its action and
/// command with the right parameter, the edit completes, and the buttons are laid out and drawn as equal
/// pills (<see cref="ActionButtonDefinition"/>, <see cref="IActionButtonDefinition"/>,
/// <see cref="ActionButtonLayout"/>).
/// </summary>
public class DrawnActionButtonsTests
{
    // Bounds (0,0)-(200,30), padding 4, spacing 6: two buttons are 93 wide, at x 4-97 and 103-196.
    private const float Left = 50, Right = 150, Gap = 100, Y = 15;

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    private sealed class CustomButton : IActionButtonDefinition
    {
        public string Label => "Custom";
        public KumikoColor BackgroundColor => new(9, 9, 9);
        public KumikoColor TextColor => new(250, 250, 250);
        public Action? Action { get; init; }
        public ICommand? Command => null;
        public object? CommandParameter => null;
    }

    private static DrawnActionButtons Buttons(params IActionButtonDefinition[] defs) => new()
    {
        Bounds = new KumikoRect(0, 0, 200, 30),
        Buttons = defs.Length == 0
            ? new List<IActionButtonDefinition>
            {
                new ActionButtonDefinition { Label = "Edit", BackgroundColor = new KumikoColor(100, 100, 100) },
                new ActionButtonDefinition { Label = "Delete", BackgroundColor = new KumikoColor(200, 50, 50) },
            }
            : defs.ToList(),
    };

    // ── Input ─────────────────────────────────────────────────────

    [Fact]
    public void TapOnAButton_RunsItsActionAndCommandThenCompletesTheEdit()
    {
        var order = new List<string>();
        var command = new RecordingCommand();
        var row = new object();
        var bar = Buttons(
            new ActionButtonDefinition { Label = "Edit", Action = () => order.Add("edit") },
            new ActionButtonDefinition { Label = "Delete", Action = () => order.Add("delete-action"), Command = command });
        bar.RowItem = row;
        int completed = 0;
        bar.EditCompleted += (_, _) => completed++;

        bar.Tap(Right, Y);

        Assert.Equal(new[] { "delete-action" }, order);
        Assert.Same(row, Assert.Single(command.Parameters));
        Assert.Equal(1, completed);
    }

    [Fact]
    public void TapOnTheFirstButton_DoesNotRunTheSecond()
    {
        var order = new List<string>();
        var bar = Buttons(
            new ActionButtonDefinition { Action = () => order.Add("first") },
            new ActionButtonDefinition { Action = () => order.Add("second") });

        bar.Tap(Left, Y);

        Assert.Equal(new[] { "first" }, order);
    }

    [Fact]
    public void ExplicitCommandParameter_WinsOverTheRowItem()
    {
        var command = new RecordingCommand();
        var bar = Buttons(new ActionButtonDefinition { Command = command, CommandParameter = "explicit" });
        bar.RowItem = "row";

        bar.Tap(Left, Y);

        Assert.Equal("explicit", Assert.Single(command.Parameters));
    }

    [Fact]
    public void AnyIActionButtonDefinition_WorksInTheEditor()
    {
        int fired = 0;
        var bar = Buttons(new CustomButton { Action = () => fired++ });

        bar.Tap(Left, Y);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Press_ClaimsTheEventOnlyOverAButton()
    {
        var bar = Buttons();

        Assert.True(bar.Down(Left, Y));
        Assert.False(bar.Down(Gap, Y));
        Assert.False(bar.Down(Left, 28)); // inside the cell, in the 4 px padding below the pills
    }

    [Fact]
    public void ReleasingOffTheButtonThatWasPressed_FiresNothing()
    {
        int fired = 0;
        var bar = Buttons(
            new ActionButtonDefinition { Action = () => fired++ },
            new ActionButtonDefinition { Action = () => fired++ });
        int completed = 0;
        bar.EditCompleted += (_, _) => completed++;

        bar.Down(Left, Y);
        Assert.False(bar.Up(Right, Y));
        bar.Down(Left, Y);
        Assert.False(bar.Up(Gap, Y));

        Assert.Equal(0, fired);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void ReleaseWithoutAPress_IsNotHandled()
    {
        int fired = 0;
        var bar = Buttons(new ActionButtonDefinition { Action = () => fired++ });

        Assert.False(bar.Up(Left, Y));
        Assert.Equal(0, fired);
    }

    [Fact]
    public void ARelease_ClearsThePress_SoAStrayReleaseDoesNotFireAgain()
    {
        int fired = 0;
        var bar = Buttons(new ActionButtonDefinition { Action = () => fired++ });

        bar.Tap(Left, Y);
        bar.Up(Left, Y);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void ActivatesImmediately_SoTheGridForwardsTheActivatingTap()
    {
        Assert.True(Buttons().ActivatesImmediately);
    }

    [Fact]
    public void NoButtons_DrawsNothingAndIgnoresInput()
    {
        var bar = new DrawnActionButtons { Bounds = new KumikoRect(0, 0, 200, 30) };

        Assert.Empty(Draw(bar).Ops);
        Assert.False(bar.Down(Left, Y));
        Assert.False(bar.Up(Left, Y));
    }

    // ── Drawing ───────────────────────────────────────────────────

    [Fact]
    public void Draw_ShowsEachButtonAsAFilledPillWithItsLabelCentered()
    {
        var bar = Buttons();

        var ctx = Draw(bar);

        var fills = ctx.Fills.ToList();
        Assert.Equal(new KumikoRect(4, 4, 93, 22), fills[0].Rect);
        Assert.Equal(new KumikoRect(103, 4, 93, 22), fills[1].Rect);
        Assert.Equal(new KumikoColor(100, 100, 100), fills[0].Color);
        Assert.Equal(new KumikoColor(200, 50, 50), fills[1].Color);
        var edit = ctx.SingleText("Edit");
        Assert.Equal(fills[0].Rect, edit.Rect);
        Assert.Equal(KumikoTextAlignment.Center, edit.HAlign);
        Assert.True(edit.Font!.Value.IsBold);
        Assert.Equal(KumikoColor.White, edit.Color);
    }

    [Fact]
    public void Draw_ADepressedButton_IsDarkenedUntilTheRelease()
    {
        var bar = Buttons();

        bar.Down(Left, Y);
        var held = Draw(bar).Fills.ToList();
        Assert.Equal(new KumikoColor(60, 60, 60), held[0].Color);
        Assert.Equal(new KumikoColor(200, 50, 50), held[1].Color);

        bar.Up(Gap, Y);
        Assert.Equal(new KumikoColor(100, 100, 100), Draw(bar).Fills.First().Color);
    }

    [Fact]
    public void Draw_DarkeningStopsAtZero()
    {
        var bar = Buttons(new ActionButtonDefinition { BackgroundColor = new KumikoColor(10, 255, 30, 128) });

        bar.Down(Left, Y);

        Assert.Equal(new KumikoColor(0, 215, 0, 128), Draw(bar).Fills.Single().Color);
    }

    // ── Layout ────────────────────────────────────────────────────

    [Fact]
    public void Layout_SplitsTheWidthEvenlyAfterPaddingAndSpacing()
    {
        var rects = ActionButtonLayout.ComputeButtonRects(new KumikoRect(10, 20, 300, 40), 3, 5, 10);

        Assert.Equal(3, rects.Count);
        Assert.All(rects, r =>
        {
            Assert.Equal(90, r.Width, 3);
            Assert.Equal(30, r.Height);
            Assert.Equal(25, r.Y);
        });
        Assert.Equal(new[] { 15f, 115f, 215f }, rects.Select(r => r.X).ToArray());
    }

    [Theory]
    [InlineData(0, 100, 30)]
    [InlineData(2, 0, 30)]
    [InlineData(2, 100, 0)]
    public void Layout_ProducesNothingForNoButtonsOrNoRoom(int count, float width, float height)
    {
        Assert.Empty(ActionButtonLayout.ComputeButtonRects(new KumikoRect(0, 0, width, height), count, 4, 6));
    }

    [Fact]
    public void Layout_NeverReturnsANegativeSize()
    {
        var rects = ActionButtonLayout.ComputeButtonRects(new KumikoRect(0, 0, 20, 6), 3, 4, 6);

        Assert.Equal(3, rects.Count);
        Assert.All(rects, r =>
        {
            Assert.True(r.Width >= 0);
            Assert.True(r.Height >= 0);
        });
    }

    [Fact]
    public void ButtonSpacingAndPadding_ChangeWhereTheButtonsSit()
    {
        var bar = Buttons();
        bar.CellPadding = 10;
        bar.ButtonSpacing = 20;

        var fills = Draw(bar).Fills.ToList();

        Assert.Equal(new KumikoRect(10, 10, 80, 10), fills[0].Rect);
        Assert.Equal(new KumikoRect(110, 10, 80, 10), fills[1].Rect);
        Assert.True(bar.Down(50, 15));
        Assert.False(bar.Down(50, 5)); // in the padding band
    }

    [Fact]
    public void ActionButtonDefinition_StartsWithUsableDefaults()
    {
        var def = new ActionButtonDefinition();

        Assert.Equal(string.Empty, def.Label);
        Assert.Equal(KumikoColor.White, def.TextColor);
        Assert.Null(def.Action);
        Assert.Null(def.Command);
        Assert.Null(def.CommandParameter);
        Assert.NotEqual(default, def.BackgroundColor);
    }
}
