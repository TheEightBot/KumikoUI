namespace KumikoUI.Core.Data;

/// <summary>
/// Fails a batch with a user-facing message. Throw it from <see cref="IKumikoIncrementalSource.LoadMoreItemsAsync"/>
/// to show its <see cref="Exception.Message"/> on the grid's error row; the hosts' command adapter fails a batch with it
/// when the view model sets <c>LoadMoreErrorText</c>.
/// </summary>
public sealed class KumikoLoadMoreException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public KumikoLoadMoreException()
    {
    }

    /// <summary>Creates the exception with the message the error row shows.</summary>
    /// <param name="message">The user-facing message.</param>
    public KumikoLoadMoreException(string? message) : base(message)
    {
    }

    /// <summary>Creates the exception with the message the error row shows and the failure behind it.</summary>
    /// <param name="message">The user-facing message.</param>
    /// <param name="innerException">The failure behind it.</param>
    public KumikoLoadMoreException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
