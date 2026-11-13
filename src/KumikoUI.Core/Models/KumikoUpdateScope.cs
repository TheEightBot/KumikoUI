namespace KumikoUI.Core.Models;

/// <summary>
/// A batch of changes to a <see cref="KumikoSource"/>, returned by <see cref="KumikoSource.BeginUpdate"/>.
/// Disposing it calls <see cref="KumikoSource.EndUpdate"/>, so a <c>using</c> block ends the batch even when a
/// change throws. Dispose it once; a default instance does nothing.
/// </summary>
/// <example>
/// <code>
/// using (source.BeginUpdate())
/// {
///     source.SetSort(column, KumikoSortDirection.Ascending);
///     source.AddGroupDescription(new GroupDescription("Department"));
/// } // one rebuild, one ViewChanged
/// </code>
/// </example>
public readonly struct KumikoUpdateScope : IDisposable
{
    private readonly KumikoSource? _source;

    internal KumikoUpdateScope(KumikoSource source) => _source = source;

    /// <summary>Ends the batch (<see cref="KumikoSource.EndUpdate"/>).</summary>
    public void Dispose() => _source?.EndUpdate();
}
