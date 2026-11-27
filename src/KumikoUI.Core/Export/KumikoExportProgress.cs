namespace KumikoUI.Core.Export;

/// <summary>Progress of an export.</summary>
/// <param name="RowsWritten">The rows written so far (the header row is not counted).</param>
/// <param name="TotalRows">The rows the export writes in all, known up front (the header row is not counted).</param>
public readonly record struct KumikoExportProgress(int RowsWritten, int TotalRows);
