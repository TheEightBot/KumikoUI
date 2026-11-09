namespace KumikoUI.Core.Models;

/// <summary>
/// Identifies a group header (<see cref="SummaryIndex"/> -1) or group summary row across view rebuilds,
/// for <see cref="KumikoViewChangedEventArgs.MapRow"/>.
/// </summary>
internal sealed record GroupRowKey(string GroupPath, int SummaryIndex);
