using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Date selected event args.</summary>
public class DateSelectedEventArgs : EventArgs
{
    /// <summary>The previously selected date.</summary>
    public DateTime OldDate { get; }
    /// <summary>The newly selected date.</summary>
    public DateTime NewDate { get; }
    /// <summary>Initializes a new instance of the <see cref="DateSelectedEventArgs"/> class.</summary>
    /// <param name="oldDate">The previously selected date.</param>
    /// <param name="newDate">The newly selected date.</param>
    public DateSelectedEventArgs(DateTime oldDate, DateTime newDate) { OldDate = oldDate; NewDate = newDate; }
}
