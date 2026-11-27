namespace KumikoUI.Core.Models;

/// <summary>
/// The grid's effective busy state (#327), shared by the MAUI and Uno hosts: busy when the app sets it
/// (<see cref="IsBusy"/>), or when a background rebuild (<see cref="KumikoSource.IsViewUpdatePending"/>) or the first load
/// of an empty grid has lasted <see cref="Delay"/> (a short one never flashes the indicator). Runs on the UI thread;
/// the host supplies the one-shot timer.
/// </summary>
internal sealed class KumikoBusyState : IDisposable
{
    /// <summary>The default delay before an automatic busy state shows: 300 ms.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(300);

    private readonly Func<TimeSpan, Action, Action> _startTimer;
    private KumikoSource? _source;
    private Action? _cancelTimer;
    private bool _isBusy;
    private bool _manual;
    private bool _showDuringViewUpdate = true;
    private bool _initialLoad;
    private bool _delayElapsed;
    private TimeSpan _delay = DefaultDelay;

    /// <summary>Create the state; <paramref name="startTimer"/> starts a one-shot timer and returns the action that cancels it.</summary>
    public KumikoBusyState(Func<TimeSpan, Action, Action> startTimer) => _startTimer = startTimer;

    /// <summary>Raised when <see cref="IsBusy"/> changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Whether the grid is busy now.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Whether the app says the grid is busy.</summary>
    public bool Manual
    {
        get => _manual;
        set { _manual = value; Evaluate(); }
    }

    /// <summary>Whether a pending background rebuild (and an initial load) turns the busy state on after <see cref="Delay"/>.</summary>
    public bool ShowDuringViewUpdate
    {
        get => _showDuringViewUpdate;
        set { _showDuringViewUpdate = value; Evaluate(); }
    }

    /// <summary>How long a rebuild or load must last before it shows; zero or less shows it at once.</summary>
    public TimeSpan Delay
    {
        get => _delay;
        set => _delay = value;
    }

    /// <summary>Whether the first load of an empty grid is under way (incremental loading with no rows yet).</summary>
    public bool InitialLoad
    {
        get => _initialLoad;
        set { _initialLoad = value; Evaluate(); }
    }

    /// <summary>Follow <paramref name="source"/>'s <see cref="KumikoSource.ViewUpdatePendingChanged"/>; null stops following.</summary>
    public void Attach(KumikoSource? source)
    {
        if (ReferenceEquals(_source, source)) return;
        if (_source != null) _source.ViewUpdatePendingChanged -= OnPendingChanged;
        _source = source;
        if (_source != null) _source.ViewUpdatePendingChanged += OnPendingChanged;
        Evaluate();
    }

    /// <summary>Stop following the source and cancel a running delay.</summary>
    public void Dispose()
    {
        Attach(null);
        CancelTimer();
    }

    private void OnPendingChanged(object? sender, EventArgs e) => Evaluate();

    private bool WantsAuto => _showDuringViewUpdate && (_source?.IsViewUpdatePending == true || _initialLoad);

    private void Evaluate()
    {
        if (!WantsAuto)
        {
            CancelTimer();
            _delayElapsed = false;
        }
        else if (!_delayElapsed && _cancelTimer == null)
        {
            if (_delay <= TimeSpan.Zero)
                _delayElapsed = true;
            else
                _cancelTimer = _startTimer(_delay, OnDelayElapsed);
        }

        bool busy = _manual || (WantsAuto && _delayElapsed);
        if (busy == _isBusy) return;
        _isBusy = busy;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnDelayElapsed()
    {
        _cancelTimer = null;
        if (WantsAuto) _delayElapsed = true;
        Evaluate();
    }

    private void CancelTimer()
    {
        var cancel = _cancelTimer;
        _cancelTimer = null;
        cancel?.Invoke();
    }
}
