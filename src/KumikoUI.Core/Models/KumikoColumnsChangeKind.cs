namespace KumikoUI.Core.Models;

/// <summary>What changed in a <see cref="KumikoSource.ColumnsChanged"/> notification.</summary>
public enum KumikoColumnsChangeKind
{
    /// <summary>A column was shown or hidden (<see cref="KumikoColumn.IsVisible"/>).</summary>
    Visibility = 0,

    /// <summary>A column moved in <see cref="KumikoSource.Columns"/> (<see cref="KumikoSource.ReorderColumn"/>).</summary>
    Order = 1,

    /// <summary>
    /// Several columns changed at once: <see cref="KumikoSource.ResetColumnLayout"/>, or a
    /// <see cref="KumikoSource.BeginUpdate"/> batch that changed more than one column. Read the columns again.
    /// </summary>
    Reset = 2,

    /// <summary>A column moved to another pane (<see cref="KumikoColumn.FreezeMode"/>).</summary>
    Freeze = 3,
}
