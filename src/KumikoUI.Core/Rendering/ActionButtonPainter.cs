namespace KumikoUI.Core.Rendering;

/// <summary>Drawing and invoking of action buttons, shared by the display renderer and the editor.</summary>
internal static class ActionButtonPainter
{
    /// <summary>
    /// Whether the button can run: its command (if any) allows the parameter it would be run with.
    /// A button with only an <see cref="IActionButtonDefinition.Action"/> is always enabled.
    /// </summary>
    public static bool IsEnabled(IActionButtonDefinition button, object? rowItem)
        => button.Command?.CanExecute(button.CommandParameter ?? rowItem) ?? true;

    /// <summary>
    /// Runs a button when it is enabled: the command first, then the action. Returns whether it ran.
    /// The command may change the data, so callers must not touch row state afterwards.
    /// </summary>
    public static bool TryInvoke(IActionButtonDefinition button, object? rowItem)
    {
        var parameter = button.CommandParameter ?? rowItem;
        var command = button.Command;
        if (command != null && !command.CanExecute(parameter)) return false;

        command?.Execute(parameter);
        button.Action?.Invoke();
        return true;
    }

    public static void Draw(
        IDrawingContext ctx, KumikoRect rect, IActionButtonDefinition button, float cornerRadius,
        bool enabled, bool pressed, KumikoColor disabledBackground, KumikoColor disabledText)
    {
        KumikoColor bg, text;
        if (!enabled)
        {
            bg = disabledBackground;
            text = disabledText;
        }
        else
        {
            var c = button.BackgroundColor;
            bg = pressed
                ? new KumikoColor(
                    (byte)Math.Max(0, c.R - 40), (byte)Math.Max(0, c.G - 40), (byte)Math.Max(0, c.B - 40), c.A)
                : c;
            text = button.TextColor;
        }

        ctx.FillRoundRect(rect, cornerRadius, new KumikoPaint { Color = bg, IsAntiAlias = true });
        ctx.DrawTextInRect(button.Label, rect, new KumikoPaint
        {
            Color = text,
            Font = new KumikoFont("Default", 11, bold: true),
            IsAntiAlias = true
        }, KumikoTextAlignment.Center, KumikoVerticalAlignment.Center);
    }
}
