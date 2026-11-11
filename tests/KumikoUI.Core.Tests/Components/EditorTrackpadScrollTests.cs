using KumikoUI.Core.Components;
using KumikoUI.Core.Input;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Components;

/// <summary>
/// Trackpads report scroll distances in pixels, wheels in notches (#60). The drawn editors scroll by the same
/// distance either way instead of reading every pixel as a notch.
/// </summary>
public class EditorTrackpadScrollTests
{
    private static KumikoPointerEventArgs Scroll(float dy, KumikoScrollUnit unit) =>
        new() { Action = InputAction.Scroll, ScrollDeltaY = dy, ScrollUnit = unit };

    [Fact]
    public void ScrollPicker_PixelDeltas_StepOneItemPerItemHeight()
    {
        var picker = new DrawnScrollPicker { Bounds = new KumikoRect(0, 0, 120, 200), ItemHeight = 40 };
        picker.SetItems(Enumerable.Range(0, 20).Select(i => $"Item {i}"));
        picker.SelectedIndex = 0;

        // 30 px is less than one item: nothing moves yet
        picker.OnScroll(Scroll(-30, KumikoScrollUnit.Pixels));
        Assert.Equal(0, picker.SelectedIndex);

        // another 30 px makes 60 px: one item
        picker.OnScroll(Scroll(-30, KumikoScrollUnit.Pixels));
        Assert.Equal(1, picker.SelectedIndex);
    }

    [Fact]
    public void ScrollPicker_Notches_StepOneItemPerNotch()
    {
        var picker = new DrawnScrollPicker { Bounds = new KumikoRect(0, 0, 120, 200), ItemHeight = 40 };
        picker.SetItems(Enumerable.Range(0, 20).Select(i => $"Item {i}"));
        picker.SelectedIndex = 0;

        picker.OnScroll(Scroll(-2, KumikoScrollUnit.Notches));

        Assert.Equal(2, picker.SelectedIndex);
    }

    [Fact]
    public void DatePicker_PixelDeltas_AddUpBeforeChangingTheMonth()
    {
        var picker = new DrawnDatePicker
        {
            Bounds = new KumikoRect(0, 0, 200, 30),
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            SelectedDate = new DateTime(2026, 6, 15),
        };
        picker.OpenCalendar();
        int startMonth = picker.DisplayMonth.Month;

        // A few small trackpad deltas: less than a month's worth
        for (int i = 0; i < 5; i++)
            picker.OnScroll(Scroll(-10, KumikoScrollUnit.Pixels));
        Assert.Equal(startMonth, picker.DisplayMonth.Month);

        // Enough to pass the threshold: exactly one month later
        for (int i = 0; i < 2; i++)
            picker.OnScroll(Scroll(-10, KumikoScrollUnit.Pixels));
        Assert.Equal(startMonth % 12 + 1, picker.DisplayMonth.Month);
    }
}
