using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Editing;

/// <summary>
/// Defines what action triggers cell editing.
/// </summary>
[Flags]
public enum EditTrigger
{
    /// <summary>Editing is disabled.</summary>
    None = 0,
    /// <summary>A single tap starts editing.</summary>
    SingleTap = 1,
    /// <summary>A double-tap starts editing.</summary>
    DoubleTap = 2,
    /// <summary>Pressing F2 starts editing.</summary>
    F2Key = 4,
    /// <summary>Typing a character starts editing.</summary>
    Typing = 8,
    /// <summary>A long press starts editing.</summary>
    LongPress = 16,
    /// <summary>Default triggers: DoubleTap, F2, and Typing.</summary>
    Default = DoubleTap | F2Key | Typing
}
