using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;
using static KumikoUI.Core.Tests.Components.ComponentInput;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// <see cref="DrawnTextBox.TextChanged"/> and <see cref="KumikoTextChangedEventArgs"/>: the old and new
/// text of each user edit, for typing, deleting, pasting, undo and redo.
/// </summary>
public class DrawnTextBoxTextChangedTests
{
    private static (DrawnTextBox Box, List<KumikoTextChangedEventArgs> Changes) Make(string text)
    {
        var box = new DrawnTextBox
        {
            Bounds = new KumikoRect(0, 0, 400, 30),
            Padding = 0,
            InitialSelectionMode = EditTextSelectionMode.CursorAtEnd,
            Text = text,
        };
        box.SetMeasureContext(new RecordingDrawingContext());
        Focus(box);
        var changes = new List<KumikoTextChangedEventArgs>();
        box.TextChanged += (_, e) => changes.Add(e);
        return (box, changes);
    }

    [Fact]
    public void Typing_ReportsTheTextBeforeAndAfterEachCharacter()
    {
        var (box, changes) = Make("ab");

        box.Key(KumikoKey.Character, 'c');
        box.Key(KumikoKey.Character, 'd');

        Assert.Equal(2, changes.Count);
        Assert.Equal(("ab", "abc"), (changes[0].OldText, changes[0].NewText));
        Assert.Equal(("abc", "abcd"), (changes[1].OldText, changes[1].NewText));
    }

    [Fact]
    public void Backspace_ReportsTheShortenedText()
    {
        var (box, changes) = Make("abc");

        box.Key(KumikoKey.Backspace);

        var change = Assert.Single(changes);
        Assert.Equal("abc", change.OldText);
        Assert.Equal("ab", change.NewText);
    }

    [Fact]
    public void Backspace_OnEmptyText_ReportsNothing()
    {
        var (box, changes) = Make("");

        box.Key(KumikoKey.Backspace);

        Assert.Empty(changes);
    }

    [Fact]
    public void Undo_AndRedo_ReportTheReversedChange()
    {
        var (box, changes) = Make("ab");
        box.Key(KumikoKey.Character, 'c');
        changes.Clear();

        box.Undo();
        box.Redo();

        Assert.Equal(2, changes.Count);
        Assert.Equal(("abc", "ab"), (changes[0].OldText, changes[0].NewText));
        Assert.Equal(("ab", "abc"), (changes[1].OldText, changes[1].NewText));
    }

    [Fact]
    public void InsertText_ReportsThePastedResult()
    {
        var (box, changes) = Make("ad");
        box.Key(KumikoKey.Left);

        box.InsertText("bc");

        var change = Assert.Single(changes);
        Assert.Equal("ad", change.OldText);
        Assert.Equal("abcd", change.NewText);
    }

    [Fact]
    public void ArrowKeys_ChangeNoText_SoRaiseNoEvent()
    {
        var (box, changes) = Make("abc");

        box.Key(KumikoKey.Left);
        box.Key(KumikoKey.Home);

        Assert.Empty(changes);
    }
}
