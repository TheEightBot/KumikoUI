namespace KumikoUI.Core.Models;

/// <summary>
/// Returns the height in pixels of the data row described by <paramref name="context"/>, or <c>null</c> for the
/// height its <see cref="KumikoStyle.RowHeightMode"/> gives it (see <see cref="KumikoStyle.RowHeightResolver"/>).
/// </summary>
/// <param name="context">The row being laid out; valid only during the call.</param>
/// <returns>The row's height, or <c>null</c> for the mode's height.</returns>
public delegate float? KumikoRowHeightResolver(in KumikoRowHeightContext context);
