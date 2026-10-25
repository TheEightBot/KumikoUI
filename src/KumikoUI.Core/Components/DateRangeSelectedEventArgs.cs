using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Components;

/// <summary>Date range selected event args.</summary>
public class DateRangeSelectedEventArgs : EventArgs
{
    /// <summary>The start date of the selected range.</summary>
    public DateTime StartDate { get; }
    /// <summary>The end date of the selected range, or <see langword="null"/> if only the start was chosen.</summary>
    public DateTime? EndDate { get; }
    /// <summary>Initializes a new instance of the <see cref="DateRangeSelectedEventArgs"/> class.</summary>
    /// <param name="start">The start date of the range.</param>
    /// <param name="end">The end date of the range.</param>
    public DateRangeSelectedEventArgs(DateTime start, DateTime? end) { StartDate = start; EndDate = end; }
}
