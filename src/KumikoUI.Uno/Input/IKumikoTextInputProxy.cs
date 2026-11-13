using KumikoUI.Core.Input;

namespace KumikoUI.Uno.Input;

/// <summary>
/// The hidden native text box that <see cref="KumikoTextInputAdapter"/> drives: on the grid it is a
/// <c>TextBox</c>, in tests a fake. It holds a copy of the active editor's text and selection, so the platform's
/// keyboard and input method see the real text around the caret.
/// </summary>
internal interface IKumikoTextInputProxy
{
    /// <summary>The text box's text.</summary>
    string Text { get; }

    /// <summary>The start of the text box's selection (its caret when nothing is selected).</summary>
    int SelectionStart { get; }

    /// <summary>The length of the text box's selection.</summary>
    int SelectionLength { get; }

    /// <summary>
    /// Replaces the text box's text and selection. The change notifications this raises come back to the adapter,
    /// which ignores them.
    /// </summary>
    void SetTextAndSelection(string text, int selectionStart, int selectionLength);
}

/// <summary>The Core side of <see cref="KumikoTextInputAdapter"/>: the grid's input controller and edit session.</summary>
internal interface IKumikoTextInputTarget
{
    /// <summary>The text-input surface with the keyboard (<see cref="KumikoInputController.ActiveTextInput"/>), or null.</summary>
    ITextInputSession? ActiveTextInput { get; }

    /// <summary>Whether a cell editor is open, with or without a text-input surface.</summary>
    bool IsEditing { get; }

    /// <summary>The modifier keys held right now.</summary>
    InputModifiers Modifiers { get; }

    /// <summary>Sends a key to Core and returns whether Core handled it (<see cref="KumikoKeyEventArgs.Handled"/>).</summary>
    bool HandleKey(KumikoKeyEventArgs e);
}
