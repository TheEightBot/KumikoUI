namespace KumikoUI.Core.Rendering;

/// <summary>
/// Measures the advance width of a run of text in one font, for <see cref="KumikoTextLayout"/>.
/// </summary>
/// <param name="text">The text to measure, on one line. Not retained after the call.</param>
/// <returns>The width of <paramref name="text"/>, in pixels; zero for empty text.</returns>
public delegate float KumikoTextMeasurer(ReadOnlySpan<char> text);
