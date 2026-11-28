namespace KumikoUI.Core.Input;

/// <summary>What asked the grid to delete rows (#328): <see cref="KumikoRowsDeletingEventArgs.Trigger"/>.</summary>
public enum KumikoRowsDeleteTrigger
{
    /// <summary>The Delete key (on Apple keyboards also Command+Backspace) with <see cref="KumikoInputController.AllowDeleting"/> on.</summary>
    Key,

    /// <summary>A call to <c>DeleteSelectedRows</c> from code or a bound command.</summary>
    Command,
}
