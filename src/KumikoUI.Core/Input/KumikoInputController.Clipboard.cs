using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

public partial class KumikoInputController
{
    /// <summary>
    /// The system clipboard, supplied by the host. Used by <see cref="CopySelection"/> (Ctrl/Cmd+C), by copy,
    /// cut and paste in the active cell editor and by the filter popup's search box. <c>null</c> (the default)
    /// turns all of them off.
    /// </summary>
    public IKumikoClipboard? Clipboard { get; set; }

    /// <summary>
    /// Whether Ctrl/Cmd+C writes a first row with the visible columns' headers. Default <c>false</c>, which
    /// is what spreadsheets do for a plain copy and what pasting back into cells expects; set <c>true</c> to
    /// paste into a spreadsheet as a table with a header row.
    /// </summary>
    public bool CopyIncludesHeaders { get; set; }

    /// <summary>
    /// Raised when the selected rows are about to be copied, with their tab-separated text (see
    /// <see cref="KumikoTsvFormatter"/>). A handler that puts the text somewhere itself sets
    /// <see cref="ClipboardTextEventArgs.Handled"/> to keep <see cref="Clipboard"/> from also taking it.
    /// </summary>
    public event EventHandler<ClipboardTextEventArgs>? ClipboardCopyRequested;

    /// <summary>
    /// Copies the selected rows as tab-separated text: the visible columns in display order, each cell as its
    /// display text, the header row first when <see cref="CopyIncludesHeaders"/> is set. The text goes to
    /// <see cref="ClipboardCopyRequested"/> and then, if no handler took it, to <see cref="Clipboard"/>.
    /// </summary>
    /// <param name="selection">The selection to copy.</param>
    /// <param name="dataSource">The data source the selection belongs to.</param>
    /// <returns><c>true</c> when the text was handed over; <c>false</c> with no selected rows, no taker or no data.</returns>
    public bool CopySelection(KumikoSelectionModel selection, KumikoSource dataSource)
    {
        if (selection.SelectedRows.Count == 0) return false;

        string text = KumikoTsvFormatter.Format(dataSource, selection.SelectedRows, CopyIncludesHeaders);
        if (text.Length == 0) return false;

        var args = new ClipboardTextEventArgs(text);
        ClipboardCopyRequested?.Invoke(this, args);
        if (!args.Handled && Clipboard is { } clipboard)
            args.Handled = clipboard.TrySetText(text);
        return args.Handled;
    }

    private void WireEditorClipboard(EditSession? session)
    {
        if (session == null) return;
        session.ClipboardCopyRequested += OnEditorCopyRequested;
        session.ClipboardPasteRequested += OnEditorPasteRequested;
    }

    private void UnwireEditorClipboard(EditSession? session)
    {
        if (session == null) return;
        session.ClipboardCopyRequested -= OnEditorCopyRequested;
        session.ClipboardPasteRequested -= OnEditorPasteRequested;
    }

    private void WireSearchClipboard(DrawnFilterPopup popup)
    {
        popup.SearchCopyRequested += OnEditorCopyRequested;
        popup.SearchPasteRequested += (_, e) =>
            ReadClipboardForPaste(e, popup, static (p, text) => p.PasteSearchText(text), static p => p.SearchTextInput);
    }

    private void OnEditorCopyRequested(object? sender, ClipboardTextEventArgs e)
    {
        if (Clipboard is { } clipboard && clipboard.TrySetText(e.Text))
            e.Handled = true;
    }

    private void OnEditorPasteRequested(object? sender, ClipboardRequestEventArgs e)
    {
        if (_editSession is not { } session) return;
        ReadClipboardForPaste(e, session, static (s, text) => s.PasteText(text), static s => s.ActiveTextInput);
    }

    /// <summary>
    /// Answers a paste request from the clipboard: a text that is ready goes straight into the request; a
    /// clipboard that is still reading marks the request pending and pastes when it completes, provided the
    /// same text surface still has the keyboard (the user did not move on in the meantime).
    /// </summary>
    private void ReadClipboardForPaste<T>(ClipboardRequestEventArgs e, T owner, Action<T, string?> paste, Func<T, ITextInputSession?> surface)
    {
        if (Clipboard is not { } clipboard) return;

        Task<string?> read;
        try { read = clipboard.GetTextAsync(); }
        catch { return; }

        if (read.IsCompletedSuccessfully)
        {
            e.Text = read.Result;
            return;
        }
        if (read.IsFaulted || read.IsCanceled) return;

        e.IsPending = true;
        var expected = surface(owner);
        read.ContinueWith(
            t =>
            {
                if (t.IsCompletedSuccessfully && ReferenceEquals(surface(owner), expected))
                    paste(owner, t.Result);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            SynchronizationContext.Current is { } context
                ? TaskScheduler.FromCurrentSynchronizationContext()
                : TaskScheduler.Current);
    }
}
