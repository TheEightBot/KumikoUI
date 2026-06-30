using KumikoUI.Core.Input;
using KumikoUI.Uno.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KumikoUI.Uno;

/// <summary>
/// Hidden-TextBox soft-keyboard proxy for <see cref="DataGridView"/>.
///
/// <para>
/// <strong>Why this exists:</strong> on iOS and Android, focusing a non-text element (such as this
/// Grid-derived control) does NOT summon the on-screen keyboard, and
/// <c>CharacterReceived</c>/<c>KeyDown</c> do not fire from on-screen keys. The desktop-only approach
/// of calling <c>Focus(FocusState.Programmatic)</c> on the grid therefore silently breaks typing on
/// mobile. MAUI solves this with a hidden <c>Entry</c> that becomes first-responder; we do the same
/// with a WinUI <c>TextBox</c>.
/// </para>
///
/// <para>
/// <strong>Design (mirrors MAUI's <c>DataGridView._keyboardProxy</c> strategy):</strong>
/// </para>
/// <list type="bullet">
///   <item>A 1×1 <c>TextBox _inputProxy</c> is added to the visual tree with <c>Opacity=0</c> and
///     <c>Visibility=Visible</c> (collapsed elements cannot focus). It is sized to 1×1 logical pixels
///     and receives no borders, so it is invisible to the user but reachable by the focus system.</item>
///   <item>A <em>sentinel</em> character (<c>"​"</c>, zero-width space) is kept in the TextBox at
///     all times. This gives the TextBox a non-empty cursor position so:
///     (a) a Backspace press makes the text empty → detected as Backspace, and
///     (b) a typed character is appended after the sentinel → detected as new input.
///     After each detected input the proxy is reset to the sentinel via <c>ResetProxy()</c>.</item>
///   <item>WinUI's <c>TextBox.TextChanged</c> does not expose old/new text (unlike MAUI's
///     <c>TextChangedEventArgs</c>), so we track the previous value ourselves in
///     <c>_lastProxyText</c>.</item>
///   <item><c>_suppressProxyTextChanged</c> prevents the reset itself from triggering another
///     round-trip.</item>
///   <item>On edit-begin (<c>OnEditSessionCellBeginEdit</c>): <c>FocusKeyboardInput()</c> resets the
///     proxy and focuses the TextBox → the iOS/Android soft keyboard appears.</item>
///   <item>On edit-end (<c>OnEditSessionCellEndEdit</c>): proxy is reset, focus returns to the grid
///     → soft keyboard dismisses (WinUI has no Unfocus(); focusing another element is the idiom).</item>
///   <item>On <c>_inputProxy.KeyDown</c> (hardware keyboards): special/navigation keys are mapped and
///     forwarded, and <c>e.Handled=true</c> is set to prevent the TextBox from consuming Backspace,
///     Enter, Tab, arrows, etc. This prevents Backspace from being double-handled (KeyDown handles it;
///     the TextChanged path fires only for soft keyboards that bypass KeyDown).</item>
///   <item>On <c>_inputProxy.TextChanged</c> (soft keyboards): the sentinel-diff algorithm extracts
///     any newly typed characters or detects Backspace, forwards to Core, then resets the proxy.</item>
/// </list>
/// </summary>
public partial class DataGridView
{
    // ── Sentinel / suppression ───────────────────────────────────
    // Zero-width space: avoids collision with user-typed spaces and is unlikely to appear in
    // IME suggestion lists. Mirrors MAUI's KeyboardSentinel.
    private const string ProxySentinel = "​";

    private readonly TextBox _inputProxy = new TextBox
    {
        // Keep in the visual tree (Visible) so the focus system can reach it and
        // the soft keyboard appears on focus. Opacity=0 makes it invisible to the user.
        Visibility = Visibility.Visible,
        Opacity = 0,
        Width = 1,
        Height = 1,
        // No border / padding so the 1×1 pixel has zero visual footprint.
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        // Disable IME features that would interfere with char-by-char sentinel diffing.
        IsSpellCheckEnabled = false,
        IsTextPredictionEnabled = false,
        // Single-line; Enter/Tab/Backspace are handled in KeyDown before they reach the TextBox.
        AcceptsReturn = false,
        // MUST be a tab stop — WinUI/Uno Focus() returns false on a non-tab-stop element, so with
        // IsTabStop=false the proxy could never become the keyboard sink (the whole keyboard pipeline
        // silently did nothing). TabIndex is pushed high so it's effectively last in tab order.
        IsTabStop = true,
        TabIndex = int.MaxValue,
        // Text starts at sentinel so the first Backspace is detectable.
        Text = ProxySentinel,
    };

    private bool _suppressProxyTextChanged;
    private string _lastProxyText = ProxySentinel;

    // ── Proxy setup / teardown ───────────────────────────────────

    /// <summary>
    /// Adds the proxy TextBox to the Children collection.
    /// Called once from the DataGridView constructor (after <c>_canvasView</c> is added).
    /// Event handlers are subscribed idempotently in <see cref="AttachInputHandlers"/>
    /// (DataGridView.Input.cs) and unsubscribed in <see cref="TeardownInputProxy"/> so that
    /// Uno's navigation-back <c>Loaded</c> re-raise never double-subscribes.
    /// </summary>
    private void SetupInputProxy()
    {
        Children.Add(_inputProxy);
        // Note: handlers are NOT subscribed here; AttachInputHandlers owns subscription
        // so idempotent re-attach on Loaded works correctly.
    }

    /// <summary>
    /// Detaches proxy events. Called from <c>DetachInputHandlers</c> (OnUnloaded) so the
    /// delegate does not root the control across navigation.
    /// </summary>
    private void TeardownInputProxy()
    {
        _inputProxy.TextChanged -= OnProxyTextChanged;
        _inputProxy.KeyDown -= OnProxyKeyDown;
    }

    // ── Reset helper ─────────────────────────────────────────────

    /// <summary>
    /// Resets the proxy TextBox text to the sentinel without triggering the TextChanged handler.
    /// Must be called on the UI thread.
    /// </summary>
    private void ResetProxy()
    {
        _suppressProxyTextChanged = true;
        _inputProxy.Text = ProxySentinel;
        _lastProxyText = ProxySentinel;
        _suppressProxyTextChanged = false;
    }

    // ── Focus ────────────────────────────────────────────────────

    /// <summary>
    /// Focuses the hidden TextBox proxy so the soft keyboard appears on iOS/Android,
    /// and resets the sentinel so subsequent input is detected cleanly.
    /// On desktop this also works — WinUI routes KeyDown/CharacterReceived to the focused element.
    /// </summary>
    private void FocusKeyboardInput()
    {
        ResetProxy();
        _inputProxy.Focus(FocusState.Programmatic);
    }

    // ── Proxy TextChanged (soft keyboard / IME path) ─────────────

    private void OnProxyTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressProxyTextChanged) return;

        // Forward input even when NOT editing: a typed character reaches Core's Typing edit trigger,
        // which starts an edit (type-to-edit). When editing is already in progress the same path
        // continues the edit. HandleEditChar routes to Core.HandleKey in both cases — Core owns the
        // "start edit vs. continue edit" decision; we are just the keyboard sink.

        string newText = _inputProxy.Text ?? string.Empty;
        string oldText = _lastProxyText;

        // No meaningful change (e.g. WinUI raises TextChanged on focus-gain sometimes).
        if (newText == oldText) return;

        // Programmatic sentinel reset — ignore (double-guard in addition to _suppress flag).
        if (newText == ProxySentinel) return;

        // ── Backspace: sentinel was deleted, text is now empty ───
        if (string.IsNullOrEmpty(newText))
        {
            HandleEditKey(GridKey.Backspace);
            ResetProxy();
            return;
        }

        // ── Character input: extract newly inserted characters ───
        // Use the same content-based diffing as MAUI's OnKeyboardProxyTextChanged:
        //   • Standard append:   sentinel + "a"  → append-detected suffix is "a".
        //   • IME replacement:   sentinel gone, just "a" → strip sentinel if present, else take all.
        string newInput;

        if (newText.Length > oldText.Length && newText.StartsWith(oldText, StringComparison.Ordinal))
        {
            // Standard append: characters added after existing content.
            newInput = newText.Substring(oldText.Length);
        }
        else
        {
            // IME full-replacement or other non-standard input:
            // Remove the sentinel character if still embedded; treat the rest as input.
            int sentinelIdx = newText.IndexOf(ProxySentinel, StringComparison.Ordinal);
            if (sentinelIdx >= 0)
                newInput = newText.Remove(sentinelIdx, ProxySentinel.Length);
            else
                newInput = newText; // sentinel fully replaced — all chars are new input
        }

        // Forward each character to Core.
        foreach (char ch in newInput)
        {
            if (ch == '\n' || ch == '\r')
                HandleEditKey(GridKey.Enter);
            else if (ch == '\t')
                HandleEditKey(GridKey.Tab);
            else
                HandleEditChar(ch);
        }

        ResetProxy();
    }

    // ── Proxy KeyDown (hardware keyboard path) ───────────────────

    private void OnProxyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Navigation/command keys are forwarded to Core and marked Handled so the TextBox doesn't
        // also act on them (move its caret, add a newline, delete the sentinel). Printable characters
        // are handled via TextChanged for correct IME/dead-key ordering — so anything that maps to
        // GridKey.None is left alone.
        var key = InputMapping.ToGridKey(e.Key);
        if (key == GridKey.None) return; // ordinary printable key → flows to TextChanged

        // CRITICAL: a few mapped keys double as TEXT and must reach TextChanged when typed plainly,
        // otherwise the user literally cannot type them:
        //   • A/C/V/X/Z map to GridKey only as Ctrl/Cmd clipboard & select-all shortcuts.
        //   • Space is a literal space character while editing.
        // Only intercept these when they carry their command meaning; otherwise let the TextBox type them.
        if (key is GridKey.A or GridKey.C or GridKey.V or GridKey.X or GridKey.Z)
        {
            var mods = InputMapping.GetLiveModifiers();
            if ((mods & (InputModifiers.Control | InputModifiers.Meta)) == 0)
                return; // plain letter → let TextChanged type it
        }
        else if (key == GridKey.Space && _editSession.IsEditing)
        {
            return; // a typed space inside an edit → let TextChanged type it
        }

        HandleEditKey(key);
        e.Handled = true;
    }

    // ── Core dispatch helpers ─────────────────────────────────────

    /// <summary>Dispatch a character to Core's HandleKey (text entry path).</summary>
    private void HandleEditChar(char ch)
    {
        var evt = new GridKeyEventArgs
        {
            Key = GridKey.None,
            Character = ch,
            Modifiers = InputMapping.GetLiveModifiers(),
            IsKeyDown = true,
        };
        _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
    }

    /// <summary>Dispatch a command key to Core's HandleKey (navigation / action path).</summary>
    private void HandleEditKey(GridKey key)
    {
        var evt = new GridKeyEventArgs
        {
            Key = key,
            Modifiers = InputMapping.GetLiveModifiers(),
            IsKeyDown = true,
        };
        _inputController.HandleKey(evt, _scroll, _selection, _style, _dataSource);
    }
}
