#if ANDROID
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Java.Lang;
using KumikoUI.Core.Input;

namespace KumikoUI.Maui;

/// <summary>
/// The <c>InputConnection</c> an Android input method edits the grid through (#41). Each call is handed to
/// <see cref="AndroidTextInputMapper"/>, which drives Core's <see cref="ITextInputSession"/>; after a call (or at
/// the end of a batch edit) the grid tells the input method the new selection and composition. A connection is
/// tied to the text surface it was created for and does nothing once the grid has moved on to another one.
/// </summary>
internal sealed class KumikoInputConnection : BaseInputConnection
{
    // InputConnection.GET_EXTRACTED_TEXT_MONITOR
    private const int ExtractedTextMonitor = 1;

    private readonly KumikoDataGrid _owner;
    private readonly AndroidTextInputMapper _mapper;
    private readonly ITextInputSession? _session;
    private int _batchDepth;

    public KumikoInputConnection(Android.Views.View view, KumikoDataGrid owner, AndroidTextInputMapper mapper)
        : base(view, true)
    {
        _owner = owner;
        _mapper = mapper;
        _session = mapper.Session;
    }

    /// <summary>The token of an extracted-text request the input method monitors, or -1.</summary>
    public int ExtractedTextToken { get; private set; } = -1;

    /// <summary>Whether this connection still edits the grid's current text surface.</summary>
    private bool IsCurrent => ReferenceEquals(_mapper.Session, _session) && _owner.IsCurrentAndroidConnection(this);

    private bool Done()
    {
        if (_batchDepth == 0) _owner.ReportAndroidTextInputState();
        return true;
    }

    /// <inheritdoc/>
    public override bool BeginBatchEdit()
    {
        if (!IsCurrent) return false;
        _batchDepth++;
        return true;
    }

    /// <inheritdoc/>
    public override bool EndBatchEdit()
    {
        if (_batchDepth > 0) _batchDepth--;
        if (_batchDepth == 0 && IsCurrent) _owner.ReportAndroidTextInputState();
        return _batchDepth > 0;
    }

    /// <inheritdoc/>
    public override bool CommitText(ICharSequence? text, int newCursorPosition)
    {
        if (!IsCurrent) return false;
        _mapper.CommitText(text?.ToString() ?? string.Empty, newCursorPosition);
        return Done();
    }

    /// <inheritdoc/>
    public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
    {
        if (!IsCurrent) return false;
        _mapper.SetComposingText(text?.ToString() ?? string.Empty, newCursorPosition);
        return Done();
    }

    /// <inheritdoc/>
    public override bool FinishComposingText()
    {
        if (!IsCurrent) return false;
        _mapper.FinishComposingText();
        return Done();
    }

    /// <inheritdoc/>
    public override bool SetComposingRegion(int start, int end)
    {
        if (!IsCurrent) return false;
        _mapper.SetComposingRegion(start, end);
        return Done();
    }

    /// <inheritdoc/>
    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (!IsCurrent) return false;
        return _mapper.DeleteSurroundingText(beforeLength, afterLength) && Done();
    }

    /// <inheritdoc/>
    public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
    {
        if (!IsCurrent) return false;
        return _mapper.DeleteSurroundingTextInCodePoints(beforeLength, afterLength) && Done();
    }

    /// <inheritdoc/>
    public override bool SetSelection(int start, int end)
    {
        if (!IsCurrent) return false;
        _mapper.SetSelection(start, end);
        return Done();
    }

    /// <inheritdoc/>
    public override bool ReplaceText(int start, int end, ICharSequence text, int newCursorPosition, TextAttribute? textAttribute)
    {
        if (!IsCurrent) return false;
        _mapper.ReplaceText(start, end, text?.ToString() ?? string.Empty, newCursorPosition);
        return Done();
    }

    /// <inheritdoc/>
    public override bool CommitCompletion(CompletionInfo? text)
    {
        if (!IsCurrent) return false;
        _mapper.CommitText(text?.Text?.ToString() ?? string.Empty, 1);
        return Done();
    }

    /// <inheritdoc/>
    public override bool CommitCorrection(CorrectionInfo? correctionInfo) => IsCurrent;

    /// <inheritdoc/>
    public override ICharSequence? GetTextBeforeCursorFormatted(int length, GetTextFlags flags) =>
        IsCurrent ? new Java.Lang.String(_mapper.GetTextBeforeCursor(length)) : null;

    /// <inheritdoc/>
    public override ICharSequence? GetTextAfterCursorFormatted(int length, GetTextFlags flags) =>
        IsCurrent ? new Java.Lang.String(_mapper.GetTextAfterCursor(length)) : null;

    /// <inheritdoc/>
    public override ICharSequence? GetSelectedTextFormatted(GetTextFlags flags) =>
        IsCurrent && _mapper.GetSelectedText() is { } selected ? new Java.Lang.String(selected) : null;

    /// <inheritdoc/>
    public override Android.Text.CapitalizationMode GetCursorCapsMode(Android.Text.CapitalizationMode reqModes) => 0; // no auto-capitalization is requested

    /// <inheritdoc/>
    public override ExtractedText? GetExtractedText(ExtractedTextRequest? request, GetTextFlags flags)
    {
        if (!IsCurrent) return null;
        if (request != null && ((int)flags & ExtractedTextMonitor) != 0)
            ExtractedTextToken = request.Token;
        return CreateExtractedText(_mapper.Current);
    }

    /// <summary>The whole text as extracted text, for the input method's extract view and monitors.</summary>
    public static ExtractedText CreateExtractedText(TextInputSnapshot state) => new()
    {
        Text = new Java.Lang.String(state.Text),
        StartOffset = 0,
        PartialStartOffset = -1,
        PartialEndOffset = -1,
        SelectionStart = state.SelectionStart,
        SelectionEnd = state.SelectionEnd,
        Flags = ExtractedTextFlags.SingleLine,
    };

    /// <inheritdoc/>
    public override bool SendKeyEvent(KeyEvent? e)
    {
        if (!IsCurrent || e == null) return false;
        _owner.HandleAndroidKeyEvent(e);
        return true;
    }

    /// <inheritdoc/>
    public override bool PerformEditorAction(ImeAction actionCode)
    {
        if (!IsCurrent) return false;
        _mapper.FinishComposingText();
        _owner.HandleAndroidEditorAction();
        return true;
    }

    /// <inheritdoc/>
    public override bool PerformContextMenuAction(int id)
    {
        if (!IsCurrent || AndroidTextInputMapper.MapContextMenuAction(id) is not { } key) return false;
        _owner.HandleAndroidShortcut(key);
        return Done();
    }

    /// <inheritdoc/>
    public override TextSnapshot? TakeSnapshot() => null; // the text lives in Core, not in an Editable

    /// <inheritdoc/>
    public override SurroundingText? GetSurroundingText(int beforeLength, int afterLength, int flags)
    {
        if (!IsCurrent || !OperatingSystem.IsAndroidVersionAtLeast(31) || beforeLength < 0 || afterLength < 0)
            return null;
        var state = _mapper.Current;
        int start = System.Math.Max(0, state.SelectionStart - beforeLength);
        int end = (int)System.Math.Min((long)state.SelectionEnd + afterLength, state.Text.Length);
        return new SurroundingText(new Java.Lang.String(state.Text.Substring(start, end - start)),
            state.SelectionStart - start, state.SelectionEnd - start, start);
    }
}
#endif
