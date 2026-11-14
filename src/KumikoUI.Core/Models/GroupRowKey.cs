namespace KumikoUI.Core.Models;

/// <summary>
/// Identifies a group header (<see cref="SummaryIndex"/> -1) or group summary row across view rebuilds,
/// for <see cref="KumikoViewChangedEventArgs.MapRow"/>. The group is identified by its raw key path, so no path
/// string is composed for it (#254).
/// </summary>
internal sealed record GroupRowKey(GroupKeyPath GroupPath, int SummaryIndex);
