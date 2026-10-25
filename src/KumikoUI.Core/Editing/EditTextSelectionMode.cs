using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Controls how text is selected when a cell enters edit mode.
/// </summary>
public enum EditTextSelectionMode
{
    /// <summary>All text is selected (default).</summary>
    SelectAll,
    /// <summary>Cursor is placed at the end of the text with no selection.</summary>
    CursorAtEnd
}
