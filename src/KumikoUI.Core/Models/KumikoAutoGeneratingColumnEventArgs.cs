using KumikoUI.Core.Data;

namespace KumikoUI.Core.Models;

/// <summary>
/// Arguments of the host's <c>AutoGeneratingColumn</c> event, raised once for each column the grid is about to
/// generate, before it is added to <c>Columns</c>.
/// </summary>
public sealed class KumikoAutoGeneratingColumnEventArgs : EventArgs
{
    private KumikoColumn _column;

    /// <summary>Creates the arguments for <paramref name="column"/>, generated from <paramref name="metadata"/>.</summary>
    public KumikoAutoGeneratingColumnEventArgs(KumikoColumnMetadata metadata, KumikoColumn column)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(column);
        Metadata = metadata;
        _column = column;
    }

    /// <summary>The property the column is generated for.</summary>
    public KumikoColumnMetadata Metadata { get; }

    /// <summary>
    /// The generated column. Change its properties (header, width, format, styles and the like), or replace it with
    /// another column; it is added as it is when the handler returns. Never null.
    /// </summary>
    public KumikoColumn Column
    {
        get => _column;
        set => _column = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Set to true to leave this property out: no column is added for it.</summary>
    public bool Cancel { get; set; }
}
