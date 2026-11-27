namespace KumikoUI.Core.Export;

/// <summary>How the CSV exporter treats text a spreadsheet would read as a formula.</summary>
public enum KumikoFormulaInjection
{
    /// <summary>A text value starting with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, a tab or a carriage return gets a leading apostrophe (the default).</summary>
    Prefix,

    /// <summary>Values are written as they are.</summary>
    None,
}
