using KumikoUI.Core.Input;
#if MACCATALYST
using System.Runtime.InteropServices;
using ObjCRuntime;
using UIKit;
#endif
#if WINDOWS
using System.Reflection;
using Microsoft.UI.Input;
#endif

namespace KumikoUI.Maui;

/// <summary>
/// Hover cursor feedback (#181): Core's <see cref="KumikoInputController.Cursor"/> shown as the platform cursor
/// on the desktop heads, and the hover moves that drive it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Mac Catalyst</b>: SKCanvasView reports touches only, so a <c>UIHoverGestureRecognizer</c> on the
/// grid's platform view sends <see cref="InputAction.Hover"/> and <see cref="InputAction.Exited"/>. The cursor
/// is AppKit's <c>NSCursor</c>, reached through the Objective-C runtime (<c>objc_msgSend</c> with plain
/// handles, so trimming and Native AOT safe): UIKit's pointer styles have no left-right resize cursor.
/// Because UIKit may restore the arrow as the pointer moves, a non-default cursor is set again on every
/// hover and drag move.</item>
/// <item><b>Windows</b>: SKCanvasView reports hover as a move that is not in contact (sent to Core as
/// <see cref="InputAction.Hover"/>) and the pointer leaving as Exited. The cursor is
/// <c>UIElement.ProtectedCursor</c> on the canvas's platform view; the property is protected, and the
/// handler's view cannot be subclassed, so it is set through reflection on a statically known member.</item>
/// <item><b>iOS and Android</b>: touch only; the cursor is ignored.</item>
/// </list>
/// </remarks>
public partial class KumikoDataGrid
{
    private void OnInputControllerCursorChanged(object? sender, KumikoCursor cursor) => ApplyPlatformCursor(cursor);

#if MACCATALYST
    private UIHoverGestureRecognizer? _nativeHoverGesture;

    private void SetupNativeHover(UIView platformView)
    {
        if (_nativeHoverGesture != null) return;
        _nativeHoverGesture = new UIHoverGestureRecognizer(HandleNativeHoverGesture);
        platformView.AddGestureRecognizer(_nativeHoverGesture);
    }

    private void TeardownNativeHover(UIView? platformView)
    {
        if (_nativeHoverGesture == null) return;
        platformView?.RemoveGestureRecognizer(_nativeHoverGesture);
        _nativeHoverGesture = null;
    }

    private void HandleNativeHoverGesture(UIHoverGestureRecognizer recognizer)
    {
        InputAction action;
        switch (recognizer.State)
        {
            case UIGestureRecognizerState.Began:
            case UIGestureRecognizerState.Changed:
                action = InputAction.Hover;
                break;
            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed:
                action = InputAction.Exited;
                break;
            default:
                return;
        }

        // The platform view's points are the canvas's logical coordinates (the canvas fills the grid).
        var location = recognizer.LocationInView(recognizer.View);
        var gridEvent = new KumikoPointerEventArgs
        {
            X = (float)location.X,
            Y = (float)location.Y,
            Action = action,
            Button = PointerButton.None,
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        _inputController.HandlePointer(gridEvent, _scroll, _selection, _style, _dataSource);
        ReassertPlatformCursor();
    }

    // [NSCursor <shape>] then [cursor set]. Selectors and the class are looked up once.
    private static readonly IntPtr s_nsCursorClass = Class.GetHandle("NSCursor");
    private static readonly IntPtr s_selSet = Selector.GetHandle("set");
    private static readonly IntPtr s_selArrow = Selector.GetHandle("arrowCursor");
    private static readonly IntPtr s_selPointingHand = Selector.GetHandle("pointingHandCursor");
    private static readonly IntPtr s_selResizeLeftRight = Selector.GetHandle("resizeLeftRightCursor");

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend(IntPtr receiver, IntPtr selector);

    private static void ApplyPlatformCursor(KumikoCursor cursor)
    {
        if (s_nsCursorClass == IntPtr.Zero) return; // AppKit not loaded: nothing to set
        var shape = cursor switch
        {
            KumikoCursor.ResizeHorizontal => s_selResizeLeftRight,
            KumikoCursor.Hand => s_selPointingHand,
            _ => s_selArrow,
        };
        var nsCursor = IntPtr_objc_msgSend(s_nsCursorClass, shape);
        if (nsCursor != IntPtr.Zero) void_objc_msgSend(nsCursor, s_selSet);
    }

    /// <summary>Sets a non-default cursor again, in case UIKit restored the arrow since the last move.</summary>
    private void ReassertPlatformCursor()
    {
        if (_inputController.Cursor != KumikoCursor.Default) ApplyPlatformCursor(_inputController.Cursor);
    }
#elif WINDOWS
    // UIElement.ProtectedCursor is protected and the canvas's platform view belongs to SkiaSharp, so it is set
    // through reflection. typeof(...).GetProperty with constant arguments is understood by the trimmer.
    private static readonly PropertyInfo? s_protectedCursor = typeof(Microsoft.UI.Xaml.UIElement).GetProperty(
        "ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);
    private static InputCursor? s_resizeCursor, s_handCursor, s_arrowCursor;

    private void ApplyPlatformCursor(KumikoCursor cursor)
    {
        if (s_protectedCursor == null || _canvasView.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement view)
            return;
        InputCursor value = cursor switch
        {
            KumikoCursor.ResizeHorizontal => s_resizeCursor ??= InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast),
            KumikoCursor.Hand => s_handCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Hand),
            _ => s_arrowCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Arrow),
        };
        s_protectedCursor.SetValue(view, value);
    }

    private static void ReassertPlatformCursor() { }
#else
    private static void ApplyPlatformCursor(KumikoCursor cursor) { }

    private static void ReassertPlatformCursor() { }
#endif
}
