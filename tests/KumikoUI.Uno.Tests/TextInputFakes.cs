using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Uno.Input;

namespace KumikoUI.Uno.Tests;

/// <summary>
/// A stand-in for the grid's hidden <c>TextBox</c>: holds text and a selection, reports every change to the
/// adapter as <c>TextChanging</c> does, and can play a platform keyboard or an input method the way Uno's text box
/// raises its events (text first, then <c>TextCompositionChanged</c>).
/// </summary>
internal sealed class FakeTextInputProxy : IKumikoTextInputProxy
{
    private int _compositionStart, _compositionLength;

    public string Text { get; private set; } = string.Empty;
    public int SelectionStart { get; private set; }
    public int SelectionLength { get; private set; }

    /// <summary>The adapter that receives the change notifications.</summary>
    public KumikoTextInputAdapter Adapter { get; set; } = null!;

    /// <summary>How many times the adapter wrote the proxy.</summary>
    public int Writes { get; private set; }

    /// <summary>
    /// Raise typing changes the way Uno's <c>TextBox</c> does, with <see cref="IsChangingText"/> true while the
    /// adapter handles them (its displayed text is still the old one then).
    /// </summary>
    public bool RaisesChangesLikeUno { get; set; }

    public bool IsChangingText { get; private set; }

    public void SetTextAndSelection(string text, int selectionStart, int selectionLength)
    {
        // Uno's TextBox.Select throws for a selection past the end of the text it still displays (#67)
        if (IsChangingText)
            throw new InvalidOperationException("The proxy was written while it was raising its own text change.");
        Writes++;
        bool changed = Text != text;
        Text = text;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
        if (changed) Adapter.OnProxyTextChanged(); // a TextBox raises TextChanging for programmatic changes too
    }

    // ── Keyboard ──

    /// <summary>Types <paramref name="text"/> over the selection, like a key press or a suggestion.</summary>
    public void Type(string text) => Replace(SelectionStart, SelectionLength, text);

    /// <summary>A soft keyboard's Backspace: deletes the selection, or the UTF-16 unit before the caret.</summary>
    public void Backspace()
    {
        if (SelectionLength > 0) Replace(SelectionStart, SelectionLength, string.Empty);
        else if (SelectionStart > 0) Replace(SelectionStart - 1, 1, string.Empty);
    }

    /// <summary>Replaces <paramref name="length"/> units at <paramref name="start"/> and puts the caret after it.</summary>
    public void Replace(int start, int length, string text)
    {
        Text = Text.Remove(start, length).Insert(start, text);
        if (!RaisesChangesLikeUno)
        {
            SelectionStart = start + text.Length;
            SelectionLength = 0;
            Adapter.OnProxyTextChanged();
            return;
        }

        // Uno raises TextChanging before it moves the caret
        IsChangingText = true;
        try
        {
            Adapter.OnProxyTextChanged();
        }
        finally
        {
            IsChangingText = false;
        }
        SelectionStart = start + text.Length;
        SelectionLength = 0;
    }

    /// <summary>The platform moves the caret on its own (Uno's mobile heads put it at the start on focus).</summary>
    public void MoveCaret(int position)
    {
        SelectionStart = position;
        SelectionLength = 0;
    }

    // ── Input method (mirrors Uno's TextBoxCore IME flow) ──

    public void StartComposition()
    {
        _compositionStart = SelectionStart;
        _compositionLength = SelectionLength;
        Adapter.OnCompositionStarted(_compositionStart, _compositionLength);
    }

    public void UpdateComposition(string marked, int cursor)
    {
        SetCompositionText(marked, cursor);
        _compositionLength = marked.Length;
        Adapter.OnCompositionChanged(_compositionStart, _compositionLength);
    }

    /// <summary>The input method commits <paramref name="committed"/> and keeps composing <paramref name="marked"/>.</summary>
    public void PartiallyCommit(string committed, string marked)
    {
        SetCompositionText(committed + marked, committed.Length + marked.Length);
        _compositionStart += committed.Length;
        _compositionLength = marked.Length;
        Adapter.OnCompositionChanged(_compositionStart, _compositionLength);
    }

    public void CompleteComposition(string committed)
    {
        SetCompositionText(committed, committed.Length);
        Adapter.OnCompositionEnded();
    }

    public void CancelComposition()
    {
        SetCompositionText(string.Empty, 0);
        Adapter.OnCompositionEnded();
    }

    private void SetCompositionText(string text, int cursor)
    {
        string newText = Text.Remove(_compositionStart, _compositionLength).Insert(_compositionStart, text);
        SelectionStart = _compositionStart + cursor;
        SelectionLength = 0;
        if (newText == Text) return;
        Text = newText;
        Adapter.OnProxyTextChanged();
    }
}

/// <summary>A target whose only text-input surface is one <see cref="DrawnTextBox"/>, always active.</summary>
internal sealed class TextBoxTarget : IKumikoTextInputTarget
{
    private readonly DrawnTextBox box;

    public TextBoxTarget(DrawnTextBox box)
    {
        this.box = box;
        var focus = new DrawnFocusManager();
        focus.Register(box);
        focus.SetFocus(box);
    }

    public List<KumikoKeyEventArgs> Keys { get; } = new();
    public ITextInputSession? ActiveTextInput => box;
    public bool IsEditing => true;
    public InputModifiers Modifiers => InputModifiers.None;

    public bool HandleKey(KumikoKeyEventArgs e)
    {
        Keys.Add(e);
        e.Handled = box.OnKeyDown(e);
        return e.Handled;
    }
}

/// <summary>
/// A real Core grid (source, selection, edit session, input controller) behind the adapter, as on the
/// <c>KumikoDataGrid</c>. Columns: Name (text) and Age (numeric); three rows.
/// </summary>
internal sealed class CoreGridTarget : IKumikoTextInputTarget
{
    public sealed class Item
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    public List<Item> Items { get; } = new()
    {
        new() { Name = "Ann", Age = 30 },
        new() { Name = "Bob", Age = 40 },
        new() { Name = "Cy", Age = 50 },
    };

    public KumikoSource Source { get; } = new();
    public KumikoStyle Style { get; } = new();
    public ScrollState Scroll { get; } = new() { ViewportWidth = 400, ViewportHeight = 300 };
    public KumikoSelectionModel Selection { get; }
    public EditSession Edit { get; } = new();
    public KumikoInputController Input { get; } = new();
    public KumikoColumn Name { get; } = new() { Header = "Name", PropertyName = "Name", Width = 120 };
    public KumikoColumn Age { get; } = new() { Header = "Age", PropertyName = "Age", Width = 80, ColumnType = KumikoColumnType.Numeric };
    public List<KumikoKeyEventArgs> Keys { get; } = new();

    public CoreGridTarget()
    {
        Selection = new KumikoSelectionModel(Source);
        Source.SetColumns(new[] { Name, Age });
        Source.SetItems(Items);
        Edit.Style = Style;
        Input.EditSession = Edit;
    }

    public ITextInputSession? ActiveTextInput => Input.ActiveTextInput;
    public bool IsEditing => Edit.IsEditing;
    public InputModifiers Modifiers { get; set; }

    public bool HandleKey(KumikoKeyEventArgs e)
    {
        Keys.Add(e);
        Input.HandleKey(e, Scroll, Selection, Style, Source);
        return e.Handled;
    }

    public void Select(int row, int column) => Selection.CurrentCell = new CellPosition(row, column);
}
