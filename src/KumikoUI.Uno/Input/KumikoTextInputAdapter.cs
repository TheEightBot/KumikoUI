using KumikoUI.Core.Input;

namespace KumikoUI.Uno.Input;

/// <summary>
/// The Uno text-input adapter (<c>docs/TEXT-INPUT.md</c>): translates what the platform keyboard and input method
/// do to the grid's hidden <c>TextBox</c> into calls on Core's <see cref="ITextInputSession"/>, and keys into
/// <see cref="KumikoInputController.HandleKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Mirroring.</strong> While an editor with a text-input surface is open, the proxy holds a copy of the
/// editor's text and selection; with none open it is empty. Each change the platform makes to the proxy is
/// diffed against the copy (<see cref="ProxyTextInput.Diff"/>, located from the editor's selection) and replayed
/// on the session as a selection and an
/// insertion or deletion; then the proxy is set back to the session's text and selection when they differ (an
/// input rule refused a character, Core moved the caret). Nothing resets the proxy after each keystroke, so an
/// input method's composition and its view of the surrounding text survive.
/// </para>
/// <para>
/// <strong>Composition.</strong> Between the text box's <c>TextCompositionStarted</c> and
/// <c>TextCompositionEnded</c> the proxy's text changes are marked text: the adapter passes the composed range to
/// <see cref="ITextInputSession.SetComposition"/>, and commits it when the composition ends. The range comes from
/// <c>TextCompositionChanged</c> when the head raises it, else from the text around the composition, which an
/// input method leaves alone. Where a head raises no composition events, composed text arrives as ordinary edits
/// and is still correct once the input method finishes.
/// </para>
/// <para>
/// <strong>Keys.</strong> <see cref="OnKeyDown"/> sends command keys to Core and reports whether Core handled
/// them, so the host marks a key handled only then: Tab past the last tab stop leaves the grid, and Escape with
/// nothing to cancel reaches the page.
/// </para>
/// <para>Every member is called on the UI thread.</para>
/// </remarks>
internal sealed class KumikoTextInputAdapter
{
    private readonly IKumikoTextInputProxy _proxy;
    private readonly IKumikoTextInputTarget _target;

    // The session whose TextInputStateChanged the adapter listens to.
    private ITextInputSession? _session;

    // The proxy text the adapter last saw or wrote: each change of the proxy is diffed against it.
    private string _mirror = string.Empty;

    // >0 while the adapter writes the proxy (its change notifications are ignored) or calls into Core (session
    // state changes are synced once the call returns).
    private int _writing;
    private int _dispatching;

    // The command key (Tab or Enter) OnKeyDown last sent to Core. A head can still insert its text form ("\t",
    // "\n") into the proxy after KeyDown: that echo is dropped once instead of delivering the key twice.
    private KumikoKey _keyHandledOnKeyDown = KumikoKey.None;

    // ── Composition state (proxy coordinates) ──
    private bool _composing;
    private int _compositionStart;
    // The number of characters after the composition: an input method changes only the composed text, so the
    // composition's end is the text's length minus this.
    private int _compositionSuffix;
    // The composition started an edit (type-to-edit): the editor's text differs from the proxy's, so only the
    // composed text is mirrored, and the proxy is synced when the composition ends.
    private bool _compositionDetached;
    private bool _typeToEditTried;

    /// <summary>Creates an adapter between <paramref name="proxy"/> and <paramref name="target"/>.</summary>
    public KumikoTextInputAdapter(IKumikoTextInputProxy proxy, IKumikoTextInputTarget target)
    {
        _proxy = proxy;
        _target = target;
    }

    /// <summary>Whether an input-method composition is in progress in the proxy.</summary>
    public bool IsComposing => _composing;

    // ── Sync ─────────────────────────────────────────────────────

    /// <summary>
    /// Mirrors the active text-input surface into the proxy, or empties the proxy when there is none. The host
    /// calls it when an edit begins or ends, a filter popup opens or closes, and before focusing the proxy. A
    /// composition in the same editor is left alone; one whose editor closed is abandoned.
    /// </summary>
    public void Sync()
    {
        if (_dispatching > 0) return; // the call into Core in progress syncs when it returns
        var session = AttachSession();
        if (_composing)
        {
            if (session is not null) return;
            EndCompositionState();
        }
        WriteProxy(session);
    }

    /// <summary>Stops listening to the active session (the host is unloaded).</summary>
    public void Detach()
    {
        if (_session is not null) _session.TextInputStateChanged -= OnSessionStateChanged;
        _session = null;
    }

    // ── Text changes ─────────────────────────────────────────────

    /// <summary>
    /// The proxy's text changed (<c>TextBox.TextChanging</c>, which is raised synchronously). Outside a
    /// composition the edit is replayed on the session, or sent to Core as typed characters when no session is
    /// open (type-to-edit); during a composition it updates the marked text.
    /// </summary>
    public void OnProxyTextChanged()
    {
        if (_writing > 0) return;
        string text = _proxy.Text;
        if (string.Equals(text, _mirror, StringComparison.Ordinal)) return;

        if (_composing)
        {
            _mirror = text;
            int start = Math.Min(_compositionStart, text.Length);
            int end = Math.Max(start, text.Length - _compositionSuffix);
            UpdateComposition(text, start, end);
            return;
        }

        // The edit is located from the editor's selection: the text box's own caret can lag behind its text
        // while TextChanging is raised.
        var selection = AttachSession()?.Selection ?? default;
        var change = ProxyTextInput.Diff(_mirror, text, selection.Start, selection.Length);
        _mirror = text;
        _dispatching++;
        try
        {
            ApplyTyping(change);
        }
        finally
        {
            _dispatching--;
        }
        Finish();
    }

    /// <summary>
    /// Replays a typed edit: the replaced range becomes the session's selection, then inserted text goes to
    /// <see cref="ITextInputSession.InsertText"/>, Tab and Enter (from a soft keyboard) to Core as keys, and a
    /// pure deletion to <see cref="ITextInputSession.DeleteBackward"/>.
    /// </summary>
    private void ApplyTyping(ProxyTextInput.TextChange change)
    {
        var plan = ProxyTextInput.Plan(change.Inserted, _keyHandledOnKeyDown);
        _keyHandledOnKeyDown = KumikoKey.None;

        var session = AttachSession();
        if (session is not null)
        {
            Select(session, change.Start, change.Start + change.RemovedLength);
            if (plan.Count == 0)
            {
                if (change.RemovedLength > 0) Delete(session);
                return;
            }
        }

        int i = 0;
        while (i < plan.Count)
        {
            if (plan[i].Key != KumikoKey.Character)
            {
                DispatchKey(plan[i].Key);
                i++;
                continue;
            }

            int runStart = i;
            while (i < plan.Count && plan[i].Key == KumikoKey.Character) i++;
            InsertTyped(CharactersOf(plan, runStart, i));
        }
    }

    /// <summary>
    /// Inserts typed text into the session, or, with no session open, sends it to Core one character at a time,
    /// which can start an edit (type-to-edit); the rest then goes into the new editor whole.
    /// </summary>
    private void InsertTyped(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            var session = AttachSession();
            if (session is not null)
            {
                session.InsertText(i == 0 ? text : text.Substring(i));
                return;
            }
            DispatchCharacter(text[i]);
        }
    }

    // ── Composition ──────────────────────────────────────────────

    /// <summary>
    /// A composition started (<c>TextBox.TextCompositionStarted</c>) at <paramref name="startIndex"/>, replacing
    /// <paramref name="length"/> characters (the selection).
    /// </summary>
    public void OnCompositionStarted(int startIndex, int length)
    {
        if (_composing) return;
        if (!string.Equals(_proxy.Text, _mirror, StringComparison.Ordinal))
            OnProxyTextChanged(); // a change that arrived before the event is ordinary typing

        _composing = true;
        _compositionDetached = false;
        _typeToEditTried = false;
        string text = _proxy.Text;
        _mirror = text;
        _compositionStart = Math.Clamp(startIndex, 0, text.Length);
        _compositionSuffix = text.Length - Math.Clamp(startIndex + length, _compositionStart, text.Length);
    }

    /// <summary>
    /// The composition is now the <paramref name="length"/> characters at <paramref name="startIndex"/>
    /// (<c>TextBox.TextCompositionChanged</c>). The start moves forward when the input method commits part of
    /// the composition, and back when it recomposes text before the caret.
    /// </summary>
    public void OnCompositionChanged(int startIndex, int length)
    {
        string text = _proxy.Text;
        if (!_composing)
        {
            // No start event (or the adapter abandoned the composition): start one here.
            _composing = true;
            _compositionDetached = false;
            _typeToEditTried = false;
        }

        _mirror = text;
        int start = Math.Clamp(startIndex, 0, text.Length);
        int end = Math.Clamp(startIndex + length, start, text.Length);
        _compositionStart = start;
        _compositionSuffix = text.Length - end;
        UpdateComposition(text, start, end);
    }

    /// <summary>The composition ended (<c>TextBox.TextCompositionEnded</c>): its text is committed.</summary>
    public void OnCompositionEnded()
    {
        if (!_composing) return;
        string text = _proxy.Text;
        int start = Math.Min(_compositionStart, text.Length);
        int end = Math.Max(start, text.Length - _compositionSuffix);
        bool detached = _compositionDetached, typeToEditTried = _typeToEditTried;
        EndCompositionState();
        _mirror = text;

        _dispatching++;
        try
        {
            var session = AttachSession();
            if (session is null)
            {
                // No text surface: the composed text is ordinary typing (an editor without one, or type-to-edit
                // that never got to start because nothing was composed yet).
                if (!typeToEditTried && end > start) InsertTyped(text.Substring(start, end - start));
            }
            else if (detached || (session.Composition is not null && string.Equals(session.Text, text, StringComparison.Ordinal)))
            {
                session.CommitComposition();
            }
            else
            {
                // The editor and the proxy disagree (the platform changed text outside the composition):
                // make the editor's text the proxy's, as committed text.
                session.CancelComposition();
                var selection = session.Selection;
                ApplyToSession(session, ProxyTextInput.Diff(session.Text, text, selection.Start, selection.Length));
            }
        }
        finally
        {
            _dispatching--;
        }
        Finish();
    }

    /// <summary>
    /// Makes the session show <paramref name="text"/> with <c>[start, end)</c> as its marked text: the text
    /// outside the composition is committed, the rest goes to <see cref="ITextInputSession.SetComposition"/>.
    /// </summary>
    private void UpdateComposition(string text, int start, int end)
    {
        string marked = text.Substring(start, end - start);
        int caret = Math.Clamp(_proxy.SelectionStart - start, 0, marked.Length);

        _dispatching++;
        try
        {
            var session = AttachSession();
            if (session is null)
            {
                // Composing over a cell that is not being edited: the first composed character starts the edit
                // (type-to-edit), and the composition continues in the new editor.
                if (marked.Length == 0 || _typeToEditTried || _target.IsEditing) return;
                _typeToEditTried = true;
                DispatchCharacter(marked[0]);
                session = AttachSession();
                if (session is null) return;
                _compositionDetached = true;
                // The editor took the character as typed text: take it back so it is marked instead.
                int cursor = session.CursorPosition;
                if (cursor > 0 && cursor <= session.Text.Length && session.Text[cursor - 1] == marked[0])
                    session.DeleteBackward();
                session.SetComposition(marked, caret);
                return;
            }

            if (_compositionDetached)
            {
                session.SetComposition(marked, caret);
                return;
            }

            // The common case: the session's composition (or, before the first update, its selection) is where
            // the proxy's is, and the text around it matches. Only the marked text changes.
            string committed = text.Remove(start, end - start);
            var region = session.Composition ?? session.Selection;
            if (region.Start == start && session.Text.Length - region.Length == committed.Length &&
                string.CompareOrdinal(session.Text, 0, committed, 0, region.Start) == 0 &&
                string.CompareOrdinal(session.Text, region.End, committed, start, committed.Length - start) == 0)
            {
                session.SetComposition(marked, caret);
                return;
            }

            // The input method committed part of the composition, or recomposes other text: restore the text
            // from before the composition, commit what is now outside it, and mark the rest again.
            session.CancelComposition();
            ApplyToSession(session, ProxyTextInput.Diff(session.Text, committed, start, 0));
            Select(session, start, start);
            if (marked.Length > 0) session.SetComposition(marked, caret);
        }
        finally
        {
            _dispatching--;
        }
    }

    private void EndCompositionState()
    {
        _composing = false;
        _compositionDetached = false;
        _typeToEditTried = false;
    }

    // ── Keys ─────────────────────────────────────────────────────

    /// <summary>The result of <see cref="OnKeyDown"/>.</summary>
    /// <param name="Outcome">What the host does with the key event.</param>
    /// <param name="Backwards">Shift was held: an unhandled Tab moves focus backwards.</param>
    internal readonly record struct KeyResult(InputMapping.ProxyKeyOutcome Outcome, bool Backwards);

    /// <summary>
    /// A key went down in the proxy. Command keys are sent to Core; the result says whether Core handled the
    /// key, which is the only time the host marks it handled (an unhandled Tab moves focus out of the grid).
    /// Returns null for keys that are text, which the text box types and <see cref="OnProxyTextChanged"/>
    /// picks up: printable keys, plain letters that double as shortcuts, Space while editing, and every key
    /// during a composition, which belongs to the input method.
    /// </summary>
    public KeyResult? OnKeyDown(KumikoKey key)
    {
        _keyHandledOnKeyDown = KumikoKey.None; // a new keystroke: any earlier Tab/Enter echo is stale
        if (key == KumikoKey.None || _composing) return null;

        var modifiers = _target.Modifiers;
        if ((key is KumikoKey.A or KumikoKey.C or KumikoKey.V or KumikoKey.X or KumikoKey.Z or KumikoKey.Y) &&
            (modifiers & (InputModifiers.Control | InputModifiers.Meta)) == 0)
            return null; // a plain letter: typed

        if (key == KumikoKey.Space && (_target.IsEditing || AttachSession() is not null))
            return null; // a space typed into an editor or a search box

        var e = new KumikoKeyEventArgs { Key = key, Modifiers = modifiers, IsKeyDown = true };
        _dispatching++;
        try
        {
            _target.HandleKey(e);
        }
        finally
        {
            _dispatching--;
        }
        Finish();

        _keyHandledOnKeyDown = key is KumikoKey.Tab or KumikoKey.Enter ? key : KumikoKey.None;
        return new KeyResult(InputMapping.GetProxyKeyOutcome(key, e.Handled), e.HasShift);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private ITextInputSession? AttachSession()
    {
        var current = _target.ActiveTextInput;
        if (!ReferenceEquals(current, _session))
        {
            if (_session is not null) _session.TextInputStateChanged -= OnSessionStateChanged;
            _session = current;
            if (current is not null) current.TextInputStateChanged += OnSessionStateChanged;
        }
        return current;
    }

    /// <summary>
    /// The editor changed by another route (a pointer gesture, undo, a programmatic change): the platform's view
    /// of the text is stale. A composition in progress is abandoned, which resets the input method.
    /// </summary>
    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        if (_dispatching > 0 || _writing > 0) return;
        if (_composing) EndCompositionState();
        WriteProxy(AttachSession());
    }

    /// <summary>After a call into Core: mirror the result into the proxy, unless a composition is in progress.</summary>
    private void Finish()
    {
        if (!_composing) WriteProxy(AttachSession());
    }

    private void WriteProxy(ITextInputSession? session)
    {
        string text = session?.Text ?? string.Empty;
        int start = 0, length = 0;
        if (session is not null)
        {
            var selection = session.Selection;
            start = selection.Start;
            length = selection.Length;
        }

        _mirror = text;
        if (string.Equals(_proxy.Text, text, StringComparison.Ordinal) &&
            _proxy.SelectionStart == start && _proxy.SelectionLength == length)
            return;

        _writing++;
        try
        {
            _proxy.SetTextAndSelection(text, start, length);
        }
        finally
        {
            _writing--;
        }
    }

    /// <summary>Replays a committed edit (no keys, no echoes) on the session.</summary>
    private static void ApplyToSession(ITextInputSession session, ProxyTextInput.TextChange change)
    {
        if (change.RemovedLength == 0 && change.Inserted.Length == 0) return;
        Select(session, change.Start, change.Start + change.RemovedLength);
        if (change.Inserted.Length > 0) session.InsertText(change.Inserted);
        else Delete(session);
    }

    private static void Select(ITextInputSession session, int start, int end)
    {
        var selection = session.Selection;
        if (selection.Start != start || selection.End != end)
            session.SetSelection(start, end);
    }

    /// <summary>
    /// Deletes the selected range. If the range lay inside one grapheme cluster (the platform deleted part of
    /// an emoji sequence) the session moved it to the cluster's start and it is empty: delete that cluster.
    /// </summary>
    private static void Delete(ITextInputSession session)
    {
        if (session.Selection.Length > 0) session.DeleteBackward();
        else session.DeleteForward();
    }

    private void DispatchKey(KumikoKey key) =>
        _target.HandleKey(new KumikoKeyEventArgs { Key = key, Modifiers = _target.Modifiers, IsKeyDown = true });

    private void DispatchCharacter(char c) =>
        _target.HandleKey(new KumikoKeyEventArgs
        {
            Key = KumikoKey.Character, // the contract DrawnFilterPopup's search box and the Typing trigger expect
            Character = c,
            Modifiers = _target.Modifiers,
            IsKeyDown = true,
        });

    private static string CharactersOf(List<ProxyTextInput.Dispatch> plan, int start, int end)
    {
        var chars = new char[end - start];
        for (int i = start; i < end; i++) chars[i - start] = plan[i].Character!.Value;
        return new string(chars);
    }
}
