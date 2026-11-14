using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Time changed event args.</summary>
public sealed class KumikoTimeChangedEventArgs : EventArgs
{
    /// <summary>The hour value before the change (0–23).</summary>
    public int OldHour { get; }
    /// <summary>The minute value before the change (0–59).</summary>
    public int OldMinute { get; }
    /// <summary>The hour value after the change (0–23).</summary>
    public int NewHour { get; }
    /// <summary>The minute value after the change (0–59).</summary>
    public int NewMinute { get; }
    /// <summary>Initializes a new instance of the <see cref="KumikoTimeChangedEventArgs"/> class.</summary>
    /// <param name="oldHour">The previous hour.</param>
    /// <param name="oldMinute">The previous minute.</param>
    /// <param name="newHour">The new hour.</param>
    /// <param name="newMinute">The new minute.</param>
    public KumikoTimeChangedEventArgs(int oldHour, int oldMinute, int newHour, int newMinute)
    { OldHour = oldHour; OldMinute = oldMinute; NewHour = newHour; NewMinute = newMinute; }
}
