#if ANDROID
using Android.Content;
using Android.Views;
using Android.Views.InputMethods;

namespace KumikoUI.Maui;

/// <summary>
/// The native view that holds Android's keyboard focus for the grid (#41). It draws nothing and takes no touches;
/// it is added to the grid's platform view and placed over the edited cell, so the window's pan and an enclosing
/// scroll view keep that cell on screen while the soft keyboard is up. While an edit or the filter popup's search
/// box is open it is a text editor: <see cref="OnCreateInputConnection"/> hands the input method a
/// <see cref="KumikoInputConnection"/>. Hardware keys reach the grid through <see cref="DispatchKeyEvent"/>.
/// </summary>
internal sealed class KumikoTextInputView : Android.Views.View
{
    private KumikoDataGrid? _owner;

    public KumikoTextInputView(Context context, KumikoDataGrid owner) : base(context)
    {
        _owner = owner;
        Focusable = true;
        FocusableInTouchMode = true;
        SetWillNotDraw(true);
        // Plumbing for the keyboard: never a screen reader stop or an autofill target
        ImportantForAccessibility = ImportantForAccessibility.No;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            ImportantForAutofill = ImportantForAutofill.NoExcludeDescendants;
    }

    /// <summary>Breaks the link to the grid when the view is removed.</summary>
    public void Detach() => _owner = null;

    /// <inheritdoc/>
    public override bool OnCheckIsTextEditor() => _owner?.IsAndroidTextInputActive ?? false;

    /// <inheritdoc/>
    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs) =>
        _owner != null && outAttrs != null ? _owner.CreateAndroidInputConnection(this, outAttrs) : null;

    /// <inheritdoc/>
    public override bool DispatchKeyEvent(KeyEvent? e) =>
        (e != null && _owner != null && _owner.HandleAndroidKeyEvent(e)) || base.DispatchKeyEvent(e);

    /// <inheritdoc/>
    public override bool OnKeyPreIme(Keycode keyCode, KeyEvent? e) =>
        (e != null && _owner != null && _owner.HandleAndroidKeyEventPreIme(e)) || base.OnKeyPreIme(keyCode, e);

    /// <inheritdoc/>
    public override bool DispatchTouchEvent(MotionEvent? e) => false;

    /// <inheritdoc/>
    public override bool OnTouchEvent(MotionEvent? e) => false;

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool gainFocus, FocusSearchDirection direction, Android.Graphics.Rect? previouslyFocusedRect)
    {
        base.OnFocusChanged(gainFocus, direction, previouslyFocusedRect);
        _owner?.OnAndroidTextInputFocusChanged(gainFocus);
    }
}
#endif
