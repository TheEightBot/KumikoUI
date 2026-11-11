using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// Typed entry in the numeric up/down and the date picker through an edit session (#48): the text is parsed
/// in the grid's culture, a valid entry commits, and anything else goes through the session's ordinary
/// validation, keeping the editor open with a message instead of writing garbage.
/// </summary>
public class DateAndNumericEntryTests
{
    private static readonly KumikoRect Bounds = new(0, 0, 200, 30);

    private sealed class Row
    {
        public double Amount { get; set; } = 10;
        public DateTime When { get; set; } = new(2024, 3, 15);
    }

    private sealed class Fixture
    {
        public Row Item { get; } = new();
        public KumikoSource Source { get; } = new();
        public EditSession Session { get; } = new();
        public KumikoColumn Amount { get; } = new()
        {
            Header = "Amount",
            PropertyName = nameof(Row.Amount),
            ColumnType = KumikoColumnType.Template,
            EditorDescriptor = new NumericUpDownEditorDescriptor { Minimum = 0, Maximum = 100, Step = 0.5 },
        };
        public KumikoColumn When { get; } = new()
        {
            Header = "When",
            PropertyName = nameof(Row.When),
            ColumnType = KumikoColumnType.Date,
            EditorMaxDate = new DateTime(2024, 12, 31),
        };

        public Fixture(string culture)
        {
            Source.Culture = CultureInfo.GetCultureInfo(culture);
            Source.SetColumns(new[] { Amount, When });
            Source.SetItems(new[] { Item });
        }

        public bool Begin(KumikoColumn column, char? initial = null) =>
            Session.BeginEdit(0, column == Amount ? 0 : 1, column, Source, Bounds, initial);

        public void Type(string text)
        {
            foreach (char c in text)
                Session.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.Character, Character = c });
        }
    }

    // ── Numeric up/down ───────────────────────────────────────────

    [Theory]
    [InlineData("de-DE", "7,5", 7.5)]
    [InlineData("de-DE", "1.5", null)]     // not a number in German
    [InlineData("en-US", "7.5", 7.5)]
    [InlineData("ja-JP", "7.5", 7.5)]
    public void ATypedNumber_IsCommittedInTheGridsCulture(string culture, string typed, double? expected)
    {
        var f = new Fixture(culture);
        f.Begin(f.Amount);
        Assert.IsType<DrawnNumericUpDown>(f.Session.ActiveEditor);

        f.Type(typed);
        bool committed = f.Session.CommitEdit(f.Source);

        if (expected is { } value)
        {
            Assert.True(committed);
            Assert.Equal(value, f.Item.Amount);
            Assert.False(f.Session.IsEditing);
        }
        else
        {
            Assert.False(committed);
            Assert.Equal(10, f.Item.Amount);
            Assert.True(f.Session.IsEditing);
            Assert.False(f.Session.CurrentValidation!.IsValid);
        }
    }

    [Fact]
    public void ATypedNumber_OutsideTheLimits_IsClampedOnCommit()
    {
        var f = new Fixture("en-US");
        f.Begin(f.Amount);

        f.Type("500");
        f.Session.CommitEdit(f.Source);

        Assert.Equal(100, f.Item.Amount);
    }

    [Fact]
    public void ATypedNumber_ConfirmedWithEnter_IsWritten_AndTextThatIsNotANumberIsRefused()
    {
        var f = new Fixture("de-DE");
        f.Begin(f.Amount);
        f.Type("1,2,3");

        Assert.False(f.Session.CommitEdit(f.Source));
        Assert.True(f.Session.IsEditing);
        Assert.Equal(10, f.Item.Amount);
        Assert.NotNull(f.Session.CurrentValidation);

        // Fixing the text commits it
        f.Session.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.Escape }); // drops the pending text
        f.Type("2,5");
        Assert.True(f.Session.CommitEdit(f.Source));
        Assert.Equal(2.5, f.Item.Amount);
    }

    [Fact]
    public void ADigitThatOpensTheEdit_StartsTheNumber()
    {
        var f = new Fixture("de-DE");

        f.Begin(f.Amount, '4');
        f.Type("2,5");
        f.Session.CommitEdit(f.Source);

        Assert.Equal(42.5, f.Item.Amount);
    }

    [Fact]
    public void ALetterThatOpensTheEdit_LeavesTheNumberAlone()
    {
        var f = new Fixture("de-DE");

        f.Begin(f.Amount, 'x');
        f.Session.CommitEdit(f.Source);

        Assert.Equal(10, f.Item.Amount); // nothing typed: nothing written
    }

    [Fact]
    public void TheNumberIsShownInTheGridsCulture()
    {
        var f = new Fixture("de-DE");
        f.Item.Amount = 99.5;

        f.Begin(f.Amount);

        var ctx = new Tests.TestHelpers.RecordingDrawingContext();
        f.Session.ActiveEditor!.OnDraw(ctx);
        Assert.Single(ctx.TextsEqual("99,5"));
    }

    [Fact]
    public void AHeldSpinButton_RepeatsThroughTheSessionsFrameHook()
    {
        var f = new Fixture("en-US");
        f.Begin(f.Amount);
        var editor = (DrawnNumericUpDown)f.Session.ActiveEditor!;
        editor.Bounds = new KumikoRect(0, 0, 200, 40);

        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = 180, Y = 20, Action = InputAction.Pressed });
        Assert.True(f.Session.IsAnimating);

        int frames = 0;
        while (f.Session.UpdateAnimation(16f) && ++frames < 60) { }
        Assert.True(editor.Value >= 14, $"got {editor.Value}"); // 10 + the press + repeats of 0.5 steps

        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = 180, Y = 20, Action = InputAction.Released });
        Assert.False(f.Session.IsAnimating);
    }

    [Fact]
    public void ACanceledPress_EndsTheRepeatThroughTheSession()
    {
        var f = new Fixture("en-US");
        f.Begin(f.Amount);
        ((DrawnNumericUpDown)f.Session.ActiveEditor!).Bounds = new KumikoRect(0, 0, 200, 40);

        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = 180, Y = 20, Action = InputAction.Pressed });
        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = 180, Y = 20, Action = InputAction.Cancelled });

        Assert.False(f.Session.IsAnimating);
    }

    // ── Date picker ───────────────────────────────────────────────

    [Theory]
    [InlineData("de-DE", "20.03.2024")]
    [InlineData("en-US", "3/20/2024")]
    [InlineData("ja-JP", "2024/03/20")]
    public void ATypedDate_IsCommittedInTheGridsCulture(string culture, string typed)
    {
        var f = new Fixture(culture);
        f.Begin(f.When);

        f.Type(typed);
        f.Session.HandleKeyEvent(new KumikoKeyEventArgs { Key = KumikoKey.Enter }); // confirms the entry, which commits the edit

        Assert.False(f.Session.IsEditing);
        Assert.Equal(new DateTime(2024, 3, 20), f.Item.When);
    }

    [Theory]
    [InlineData("de-DE", "31.02.2024")]  // no such day
    [InlineData("de-DE", "gestern")]
    [InlineData("de-DE", "01.01.2025")]  // after the column's latest date
    public void ATypedDate_ThatIsNotAllowed_IsRefusedWithAMessage_AndNothingIsWritten(string culture, string typed)
    {
        var f = new Fixture(culture);
        f.Begin(f.When);
        f.Type(typed);

        bool committed = f.Session.CommitEdit(f.Source);

        Assert.False(committed);
        Assert.True(f.Session.IsEditing);
        Assert.Equal(new DateTime(2024, 3, 15), f.Item.When);
        Assert.False(f.Session.CurrentValidation!.IsValid);
        Assert.False(string.IsNullOrEmpty(f.Session.CurrentValidation.ErrorMessage));
    }

    [Fact]
    public void TheCalendar_FollowsTheColumnsLimits_AndCulture()
    {
        var f = new Fixture("de-DE");

        f.Begin(f.When);

        var picker = Assert.IsType<DrawnDatePicker>(f.Session.ActiveEditor);
        Assert.Equal(new DateTime(2024, 12, 31), picker.MaxDate);
        Assert.Equal(CultureInfo.GetCultureInfo("de-DE"), picker.Culture);
        var ctx = new Tests.TestHelpers.RecordingDrawingContext();
        picker.OnDraw(ctx);
        Assert.Single(ctx.TextsEqual("15.03.2024"));
    }

    [Fact]
    public void ADayPickedWithTheMouse_StillCommits()
    {
        var f = new Fixture("de-DE");
        f.Begin(f.When);
        var picker = (DrawnDatePicker)f.Session.ActiveEditor!;
        var calendar = picker.GetCalendarBounds();
        // March 2024, Monday first: the 20th (a Wednesday) is in the fourth row, third column
        float x = calendar.X + 4 + 2 * 32 + 16, y = calendar.Y + 4 + 32 + 36 + 3 * 32 + 16;

        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Pressed });
        f.Session.HandlePointerEvent(new KumikoPointerEventArgs { X = x, Y = y, Action = InputAction.Released });

        Assert.Equal(new DateTime(2024, 3, 20), f.Item.When);
    }
}
