namespace KumikoUI.Core.Input;

/// <summary>
/// Common keyboard keys for grid navigation and editing.
/// </summary>
public enum GridKey
{
    /// <summary>No key.</summary>
    None,
    // Navigation
    /// <summary>Up arrow key.</summary>
    Up,
    /// <summary>Down arrow key.</summary>
    Down,
    /// <summary>Left arrow key.</summary>
    Left,
    /// <summary>Right arrow key.</summary>
    Right,
    /// <summary>Home key.</summary>
    Home,
    /// <summary>End key.</summary>
    End,
    /// <summary>Page Up key.</summary>
    PageUp,
    /// <summary>Page Down key.</summary>
    PageDown,
    /// <summary>Tab key.</summary>
    Tab,
    // Actions
    /// <summary>Enter/Return key.</summary>
    Enter,
    /// <summary>Escape key.</summary>
    Escape,
    /// <summary>Space bar.</summary>
    Space,
    /// <summary>Delete key.</summary>
    Delete,
    /// <summary>Backspace key.</summary>
    Backspace,
    /// <summary>F2 function key (commonly starts editing).</summary>
    F2,
    // Letters/digits (for type-to-search, text entry)
    /// <summary>A key (select-all shortcut).</summary>
    A,
    /// <summary>C key (copy shortcut).</summary>
    C,
    /// <summary>V key (paste shortcut).</summary>
    V,
    /// <summary>X key (cut shortcut).</summary>
    X,
    /// <summary>Z key (undo shortcut).</summary>
    Z,
    // Generic character input
    /// <summary>Generic character input for text entry.</summary>
    Character
}
