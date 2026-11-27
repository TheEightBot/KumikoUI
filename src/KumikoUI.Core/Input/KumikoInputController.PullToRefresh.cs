using KumikoUI.Core.Models;

namespace KumikoUI.Core.Input;

/// <summary>
/// Pull-to-refresh (#330): a finger or pen pan that starts with the grid scrolled to the top and moves down pulls an
/// indicator out from under the header instead of scrolling; released past <see cref="PullToRefreshThreshold"/> it
/// refreshes. The rows never move, so row geometry and hit testing are untouched. The indicator's state is a
/// <see cref="PullToRefreshState"/> the renderer draws; its transitions and spinner run on the host's frame loop
/// through <see cref="UpdateAnimations"/>.
/// </summary>
/// <remarks>
/// Arbitration: only a pan that is mostly vertical when it starts (past <see cref="PanThreshold"/>) and starts at the
/// top can pull; a horizontal pan, a column header drag, a row drag, a drag handle, a scroll bar press and an open
/// editor's own gestures are decided before a pan starts and keep their priority. A pan that starts lower never
/// pulls, even when it reaches the top, and neither does a fling, the wheel, a trackpad or a mouse drag.
/// </remarks>
public sealed partial class KumikoInputController
{
    /// <summary>The scroll offset, in pixels, that still counts as the top for a pull to start.</summary>
    private const float PullTopSlop = 0.5f;

    private readonly PullToRefreshState _pull = new();
    private bool _isPullToRefreshEnabled;

    // The pan in progress may pull: it started at the top, mostly vertical, with a finger or a pen.
    private bool _pullEligible;

    // How far the finger has pulled down past the top, in pixels, before the resistance; 0 while it scrolls.
    private float _pullFinger;

    // The last style's KumikoStyle.EnableAnimations: the setter of IsRefreshing has no style to ask.
    private bool _pullStyleAnimates = true;

    /// <summary>The pull-to-refresh indicator a connected renderer draws.</summary>
    internal PullToRefreshState PullToRefresh => _pull;

    /// <summary>
    /// Whether a finger or pen can pull the grid down from the top to refresh (#330). Default false. A pan that starts
    /// with the grid scrolled to the top and moves down pulls an indicator out from under the header (the rows do not
    /// move); released past <see cref="PullToRefreshThreshold"/> it sets <see cref="IsRefreshing"/> and raises
    /// <see cref="RefreshRequested"/>, released short of it the indicator goes back. A mouse, the wheel and a trackpad
    /// never pull. Turning it off lets go of a pull in progress; it does not end a refresh.
    /// </summary>
    public bool IsPullToRefreshEnabled
    {
        get => _isPullToRefreshEnabled;
        set
        {
            if (_isPullToRefreshEnabled == value) return;
            _isPullToRefreshEnabled = value;
            if (value) return;
            _pullEligible = false;
            _pullFinger = 0;
            if (_pull.IsTracking)
            {
                _pull.MoveTo(0, PullToRefreshState.CancelMs, CanAnimatePull);
                Redraw();
            }
        }
    }

    /// <summary>
    /// Whether the app is refreshing (#330). A pull released past the threshold sets it to true; the app reloads its
    /// data and sets it back to false. While it is true the indicator holds below the header with a spinner, input
    /// stays enabled and another pull is ignored. Setting it in code shows or hides the indicator without a gesture
    /// (and raises no <see cref="RefreshRequested"/>), so a refresh button or menu item can show the same progress,
    /// also when <see cref="IsPullToRefreshEnabled"/> is false. Default false.
    /// </summary>
    public bool IsRefreshing
    {
        get => _pull.IsRefreshing;
        set => SetRefreshing(value, fromGesture: false);
    }

    /// <summary>
    /// How far, in pixels, the indicator must be pulled down for a release to refresh (#330). The indicator follows
    /// the finger at half its speed, up to twice this distance, and holds at it while refreshing. A value that is not
    /// a positive number restores the default, 64.
    /// </summary>
    public float PullToRefreshThreshold
    {
        get => _pull.Threshold;
        set
        {
            float threshold = float.IsFinite(value) && value > 0 ? value : PullToRefreshState.DefaultThreshold;
            if (!InertialScroller.Differs(_pull.Threshold, threshold)) return;
            _pull.Threshold = threshold;
            if (_pull.IsRefreshing && !_pull.IsTracking)
            {
                _pull.MoveTo(threshold, 0, animate: false);
                Redraw();
            }
        }
    }

    /// <summary>
    /// Raised when a pull is released past <see cref="PullToRefreshThreshold"/>, after <see cref="IsRefreshing"/> turned
    /// true: reload the data, then set <see cref="IsRefreshing"/> to false. The MAUI and Uno grids raise their
    /// <c>Refreshing</c> event and execute their <c>RefreshCommand</c> from it. Not raised when
    /// <see cref="IsRefreshing"/> is set in code.
    /// </summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Raised when <see cref="IsRefreshing"/> changes, from a pull or from code.</summary>
    public event EventHandler? IsRefreshingChanged;

    /// <summary>Whether the indicator's transitions and spinner move: the host drives them and the last style allows them.</summary>
    private bool CanAnimatePull => _animationsEnabled && _pullStyleAnimates;

    /// <summary>Whether the frame loop has to advance the indicator: a transition, or the spinner turning on screen.</summary>
    private bool IsPullToRefreshAnimating => _pull.IsActive(CanAnimatePull);

    /// <summary>The renderer passes the style it draws with, so the indicator turns static with reduced motion.</summary>
    internal void ObservePullToRefreshStyle(KumikoStyle style) => _pullStyleAnimates = style.EnableAnimations;

    /// <summary>Advances the indicator by one frame and redraws when it moved; returns whether it still animates.</summary>
    private bool StepPullToRefresh(float frameIntervalMs)
    {
        if (!CanAnimatePull)
        {
            // Reduced motion: a transition in progress ends where it was going, and the spinner stands still.
            if (!_pull.IsTransitioning) return false;
            _pull.Finish();
            Redraw();
            return false;
        }
        if (!_pull.IsActive(spin: true)) return false;
        if (_pull.Step(frameIntervalMs, spin: true)) Redraw();
        return _pull.IsActive(spin: true);
    }

    /// <summary>A pan starts: decide whether it may pull. Called once per pan, when it passes the pan threshold.</summary>
    private void BeginPullGesture(KumikoPointerEventArgs e, ScrollState scroll, KumikoStyle style)
    {
        _pullStyleAnimates = style.EnableAnimations;
        _pullFinger = 0;
        if (_pull.IsTracking) _pull.MoveTo(0, PullToRefreshState.CancelMs, CanAnimatePull); // a pull whose release was lost
        _pullEligible = _isPullToRefreshEnabled && !_pull.IsRefreshing
            && _pressDeviceType is KumikoPointerDeviceType.Touch or KumikoPointerDeviceType.Pen
            && scroll.OffsetY <= PullTopSlop
            && MathF.Abs(e.Y - _pressOriginY) > MathF.Abs(e.X - _pressOriginX);
    }

    /// <summary>
    /// One pan step (<paramref name="panDx"/>, <paramref name="panDy"/>: the scroll it asks for, positive toward the
    /// end): while the pan pulls, the downward movement at the top goes into the pull instead of the scroll, and the
    /// sideways one is dropped. Moving back up shrinks the pull, and what is left of the movement scrolls.
    /// </summary>
    private void ApplyPullToRefresh(ref float panDx, ref float panDy, ScrollState scroll)
    {
        if (!_pullEligible) return;
        if (_pull.IsRefreshing)
        {
            _pullEligible = false; // the app started a refresh in the middle of the pull
            return;
        }

        if (_pullFinger <= 0)
        {
            if (panDy >= 0 || scroll.OffsetY > PullTopSlop) return; // scrolling, or not at the top
            // The pull starts, taking over the indicator where a transition left it.
            _pullFinger = _pull.IsVisible ? _pull.FingerTravel : 0;
        }

        float before = _pull.Distance;
        float finger = _pullFinger - panDy;
        if (finger > 0)
        {
            _pullFinger = finger;
            panDy = 0;
        }
        else
        {
            _pullFinger = 0;
            panDy = -finger; // the pull is gone: the rest scrolls
        }
        panDx = 0;
        _pull.Track(_pullFinger);
        if (before < _pull.Threshold && _pull.Distance >= _pull.Threshold)
            RequestHaptic(KumikoHapticKind.RefreshThreshold, _pressDeviceType);
    }

    /// <summary>
    /// The pan ended: a pull released past the threshold refreshes, one released short of it (or
    /// <paramref name="released"/> false, a canceled touch) goes back.
    /// </summary>
    /// <returns>Whether the pan ended a pull, so it must not fling.</returns>
    private bool EndPullGesture(bool released)
    {
        _pullEligible = false;
        _pullFinger = 0;
        if (!_pull.IsTracking) return false;
        if (!_pull.IsVisible)
        {
            _pull.Release(); // pulled and pushed back: an ordinary pan
            return false;
        }

        if (released && _pull.Distance >= _pull.Threshold && !_pull.IsRefreshing)
        {
            _pull.MoveTo(_pull.Threshold, PullToRefreshState.HoldMs, CanAnimatePull);
            SetRefreshing(true, fromGesture: true);
        }
        else
        {
            _pull.MoveTo(0, PullToRefreshState.CancelMs, CanAnimatePull);
        }
        Redraw();
        return true;
    }

    /// <summary>The grid was disabled: a pull in progress lets go at once; a refresh keeps its indicator.</summary>
    private void StopPullGesture()
    {
        _pullEligible = false;
        _pullFinger = 0;
        if (_pull.IsTracking) _pull.MoveTo(0, 0, animate: false);
        _pull.Finish();
    }

    private void SetRefreshing(bool value, bool fromGesture)
    {
        if (_pull.IsRefreshing == value) return;
        _pull.IsRefreshing = value;
        if (value)
        {
            _pull.RestartSpinner();
            if (!fromGesture)
            {
                // Shown from code: a pull in progress gives way, and the indicator slides into the hold position.
                _pullEligible = false;
                _pullFinger = 0;
                _pull.MoveTo(_pull.Threshold, PullToRefreshState.ShowMs, CanAnimatePull);
            }
        }
        else
        {
            _pull.MoveTo(0, PullToRefreshState.HideMs, CanAnimatePull);
        }

        Redraw();
        IsRefreshingChanged?.Invoke(this, EventArgs.Empty);
        // A handler of IsRefreshingChanged may have ended the refresh already; then there is nothing to request.
        if (value && fromGesture && IsRefreshing) RefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}
