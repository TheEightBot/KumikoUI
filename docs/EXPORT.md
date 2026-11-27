# Exporting

`KumikoUI.Core` exports what a grid shows as CSV, and the optional `KumikoUI.Export.Excel` package as an Excel
workbook (`.xlsx`). Both build on the same format-neutral core (`IKumikoExporter`, `KumikoExportOptions`,
`KumikoExportSnapshot`), which a PDF exporter will use too. The design is in
[specs/105-export.md](specs/105-export.md); the clipboard's tab-separated text ([README](../README.md#keyboard)) uses
the same column order and escaping.

The exporter writes to a `Stream` and never touches the file system or shows a dialog: choosing where the bytes go
is the app's job (see [Saving the file](#saving-the-file)).

## Contents

- [A first export](#a-first-export)
- [What is exported](#what-is-exported)
- [CSV options](#csv-options)
- [Culture and values](#culture-and-values)
- [Formula injection](#formula-injection)
- [Excel](#excel)
- [Async, cancellation and progress](#async-cancellation-and-progress)
- [Saving the file](#saving-the-file)
- [Writing another format](#writing-another-format)
- [Performance](#performance)

## A first export

```csharp
// On the UI thread (or whichever thread owns the source).
await using var file = File.Create(path);
await grid.DataSource.ExportToCsvAsync(file);

string csv = grid.DataSource.ExportToCsvString();   // small data, synchronous, no byte order mark
```

With no options the export writes what the user sees: the rows that pass the filters in the grid's sort order, the
visible columns in the order they are drawn, a header row, the group and table summaries, and the cells as display
text (the column's `Format` in the grid's culture).

## What is exported

`KumikoExportOptions` is an immutable record; the defaults are in the first column.

| Option | Default | Meaning |
|---|---|---|
| `Rows` | `Filtered` | `Filtered`: every row passing the filters, sorted and grouped as in the grid, including the rows and summaries of collapsed groups. `Visible`: exactly the rows in the grid's flat view (a collapsed group contributes only its header). `Selected`: the rows of `Selection`, in view order. `All`: every item of the source in source order, ignoring filters, sorting, grouping and summaries. |
| `Selection` | `null` | Required for `Selected`: `KumikoExportSelection.FromItems(items)`, `FromRows(visibleRows)` (the input of `KumikoTsvFormatter.Format`) or `FromSelection(model)` (read when the export starts). Matching is by item reference; duplicates and group rows are ignored. A group header is written for a group that has selected rows. Summaries are not written for a selection. |
| `Columns` | `null` | An explicit list of columns, in this order, hidden or valueless ones included. `null`: the visible columns in pane order (left-frozen, scrolling, right-frozen). |
| `IncludeHiddenColumns`, `ColumnFilter` | `false`, `null` | Applied when `Columns` is `null`. Image and Template columns, and columns drawn by a custom renderer with no property and no `ValueAccessor`, are skipped unless listed in `Columns`. |
| `IncludeHeaders`, `HeaderSelector` | `true`, `null` | The header row and the header text of a column (default `KumikoColumn.Header`). |
| `GroupLayout` | `Rows` | `Rows`: a group header row (`Header: Key (count)` plus the caption summary), the group's rows, then its summary rows. `Columns`: no group rows; one leading column per grouping level holds the group's key text (right for pivot tables and re-import). `None`: ignore grouping. |
| `IncludeSummaries` | `null` | `null` means "when `GroupLayout` is `Rows`". Table summaries are written at the top or the bottom as in the grid; group summaries only with the `Rows` layout. A summary value goes in the column whose `PropertyName` matches (ignoring case), with the label the grid shows; several aggregates of one property are joined as the grid joins them. The summary title goes in the first column when it has no value. |
| `ValueMode`, `Culture`, `ReadOnCallerThread` | `Auto`, `null`, `false` | See below. |

The snapshot is taken on the source's thread and freezes row membership and order. It reads nothing from the live
grid afterwards, so sorting, filtering or replacing the items while the export runs does not change the output.
Values are read as the rows are written; an item that changes meanwhile shows its value at that moment.

## CSV options

`KumikoCsvOptions` (how the file is written) is given to `ExportToCsvAsync`, `ExportToCsvString` or
`new KumikoCsvExporter(options)`.

| Option | Default | Meaning |
|---|---|---|
| `Delimiter` | `,` | One character, not a quote, CR or LF. |
| `UseCultureListSeparator` | `false` | Use the export culture's list separator instead (`;` in de-DE, fr-FR and tr-TR), which Excel in that locale expects when it opens a `.csv`. |
| `LineEnding`, `EndWithLineEnding` | `\r\n`, `true` | RFC 4180 line endings; the last row ends with one too. |
| `Encoding`, `EmitByteOrderMark` | UTF-8, `true` | Excel needs the byte order mark to read non-ASCII UTF-8 when it opens a `.csv` directly. Turn it off for strict machine consumers. |
| `QuoteAllFields` | `false` | Quote every field; an empty one is written `""`. |
| `FormulaInjection` | `Prefix` | See below. |
| `NullText` | empty | Text for a `null` value in a data row. |

Fields are quoted when they contain the delimiter, a double quote, a carriage return or a line feed, or start or end
with a space; an embedded quote is doubled. The escaping is the code the clipboard uses
(`KumikoTsvFormatter.EscapeField` with a tab delimiter), so copy and export agree.

## Culture and values

Display text uses `KumikoExportOptions.Culture`, else `KumikoSource.Culture`, else the current culture. It is read
once when the export starts, so a thread culture that changes mid-export has no effect. Group key text is the text
the grid shows.

`ValueMode`:

- `DisplayText`: what the grid shows (numbers and dates as the column's format and the culture write them).
- `RawValue`: invariant, round-trippable text: numbers with `R`, `DateTime` and `DateTimeOffset` as ISO 8601,
  `DateOnly` as `yyyy-MM-dd`, `TimeOnly` and `TimeSpan` in their invariant forms, booleans as `true` and `false`.
- `Auto` (default): the format decides: display text for CSV (a CSV for a person who opens it in Excel wants what the
  grid showed; pick `RawValue` for a CSV another program reads) and typed values for Excel.

## Formula injection

A cell that starts with `=`, `+`, `-`, `@`, a tab or a carriage return is read as a formula by Excel, LibreOffice and
Google Sheets, and exported data is often user-supplied (OWASP "CSV Injection"). With `FormulaInjection.Prefix` (the
default) the CSV writer puts a single quote in front of such a **string** value (and of group key text, summary titles
and values of other reference types, whose text comes from `ToString`). Numbers, dates, booleans and headers are never
altered: a negative number stays `-5`.

The cost is a visible `'` on a legitimate string such as a phone number `+49 171 ...` in Excel. Use
`FormulaInjection.None` for a consumer that is not a spreadsheet. There is deliberately no "looks like a number"
heuristic: guessing would weaken a security control.

## Excel

The `KumikoUI.Export.Excel` package adds `ExportToExcelAsync` and `KumikoXlsxExporter` (an `IKumikoExporter`, so code
that picks a format at run time treats it like CSV). It writes one sheet, row by row, through the BCL's `ZipArchive`
and plain XML text: no third-party dependency, no reflection, trimming and Native AOT safe.

```csharp
await using var file = File.Create(path);
await grid.DataSource.ExportToExcelAsync(file, xlsx: new KumikoXlsxOptions { SheetName = "Orders", Title = "Orders" });
```

`KumikoExportOptions` chooses the rows and columns as for CSV. With `ValueMode = Auto` (the default) the cells are
typed:

| Value | Cell |
|---|---|
| `int`, `long`, `decimal`, `double` and the other numbers | a number, in the number format the column's `Format` maps to |
| `bool` | a boolean |
| `DateTime`, `DateOnly` | a date (1900 date system; dates before 1900 are written as the grid's text) |
| `DateTimeOffset` | its clock time in its own offset, as the grid shows it |
| `TimeOnly`, `TimeSpan` | a time, and an elapsed time `[h]:mm:ss` (a negative duration is written as text) |
| `string`, enums and anything else | text (an inline string: Excel never evaluates it, so `=1+2` stays text) |

The format mapping covers the .NET standard formats `N`, `F`, `D`, `C`, `P`, `E`, `G` with or without a precision,
custom numeric formats made of `0 # . , %`, exponents, literals and up to three sections, and date and time patterns
(`d`, `D`, `f`, `F`, `g`, `G`, `M`, `s`, `t`, `T`, `Y` and custom ones such as `yyyy-MM-dd`, `HH:mm` or `dd MMM yyyy`).
Currency symbols and patterns, percent patterns, separators and month names come from the export culture (with a
locale prefix for names). A format Excel cannot show exactly (`X`, per mille, eras, offsets, a 12-hour clock without
AM/PM, a one-digit year, more than three fraction digits) writes the grid's display text instead, so the sheet never
shows a different value than the grid. Excel draws digit grouping and the decimal separator in the reader's locale.
`ValueMode = DisplayText` writes every cell as the grid's text.

`KumikoXlsxOptions`:

| Option | Default | Meaning |
|---|---|---|
| `SheetName` | "Sheet1" | Made valid for Excel: `[ ] : * ? / \` and control characters removed, no leading or trailing apostrophe, at most 31 characters, "History" gets an underscore. |
| `FreezeHeader` | `true` | Freeze the header row and the columns frozen at the left of the grid. |
| `AutoFilter` | `true` | Filter and sort buttons on the header row. |
| `ColumnWidths` | `FromGrid` | `FromGrid`: the grid's `Width` in Excel's character units. `Fixed`: Excel's default width. |
| `ApplyColumnAlignment` | `true` | Center and right `TextAlignment` on the cells and the header. |
| `UseGridColors`, `Style` | `false`, `null` | Header, group header and summary rows in the colors of `Style` (pass the grid's `GridStyle`) instead of plain bold. |
| `Title`, `Author` | `null` | The workbook's document properties. |

Group headers (`GroupLayout = Rows`) become rows with outline levels, so Excel shows collapsible groups with the button
on the group header row; summary rows follow their group. Summary values are numbers in Excel's General format (a
count of a currency column is not currency). Excel's limits are checked before any byte is written: more than
1,048,576 rows (the header and summaries included) or 16,384 columns throws `ArgumentException`; text over 32,767
characters is cut. Characters XML cannot hold (control characters other than tab, CR and LF) are removed.

## Async, cancellation and progress

```csharp
var progress = new Progress<KumikoExportProgress>(p => status.Text = $"{p.RowsWritten} of {p.TotalRows}");
await grid.DataSource.ExportToCsvAsync(stream, progress: progress, cancellationToken: token);
```

- Call it on the thread that owns the source (the UI thread). The rows are copied before the first `await`; reading
  the values and writing then continue on a thread-pool thread, so the UI stays responsive. The item getters must be
  thread-safe for that; if an item is bound to UI state, set `ReadOnCallerThread = true` to read on the calling
  thread in chunks of about 2,000 rows with a yield (and a flush of the buffered text) in between.
- The token is checked every 256 rows. A canceled export throws `OperationCanceledException` and leaves the stream
  with partial content: discard it.
- `progress` is called at most every 1% of the rows and every 100 ms, and once at the end; `TotalRows` is known up
  front (the header row is not counted).
- `ArgumentException` (invalid combination: `Rows = Selected` without a `Selection`, no column to write, a delimiter
  that is a quote or a line break) is thrown before any byte is written.
- The destination is never closed or disposed, and need not be seekable. It is written from its current position.
- An exception from a `ValueAccessor` or a property getter surfaces unchanged.

## Saving the file

The grid has no save dialog. Export to a `MemoryStream` for modest sizes or to a temporary file for large ones, then
hand it to the platform. Neither snippet is compiled in the libraries.

.NET MAUI with CommunityToolkit.Maui `FileSaver`:

```csharp
await using var stream = new MemoryStream();
await grid.DataSource.ExportToCsvAsync(stream, cancellationToken: ct);
stream.Position = 0;
var result = await FileSaver.Default.SaveAsync("orders.csv", stream, ct);
if (!result.IsSuccessful) { /* canceled or failed */ }
```

Without that package, write to `FileSystem.CacheDirectory` and use `Share.Default.RequestAsync(new ShareFileRequest { ... })`
(what the MAUI sample does).

Uno Platform with `FileSavePicker` (on the WinAppSDK head, initialize it with the window handle first):

```csharp
var picker = new FileSavePicker { SuggestedFileName = "orders" };
picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
var file = await picker.PickSaveFileAsync();
if (file is null) return;
await using var stream = await file.OpenStreamForWriteAsync();
stream.SetLength(0);
await grid.DataSource.ExportToCsvAsync(stream, cancellationToken: ct);
```

Both sample apps have an Export page that does this, for CSV and for Excel (`.xlsx`, media type
`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`).

## Writing another format

An exporter implements `IKumikoExporter` (`FormatName`, `FileExtension`, `MediaType`, `ExportAsync`). It takes a
`KumikoExportSnapshot` on the calling thread and writes its row stream:

```csharp
public async Task ExportAsync(KumikoSource source, Stream destination, KumikoExportOptions? options = null,
    IProgress<KumikoExportProgress>? progress = null, CancellationToken cancellationToken = default)
{
    var snapshot = KumikoExportSnapshot.Create(source, options, autoValueMode: KumikoExportValueMode.RawValue);
    // snapshot.Columns: header, Format, Width, TextAlignment, ColumnType, IsFrozen; group key columns come first
    await snapshot.ForEachRowAsync(row =>
    {
        // row.Kind, row.Level (outline depth), row.Cells[i].Value (typed) and .Text
    }, flushAsync: null, progress, cancellationToken);
}
```

- `KumikoExportSnapshot.Create` validates the options and copies the rows, groups and computed summaries.
  `autoValueMode` is what `KumikoExportValueMode.Auto` stands for in your format; `snapshot.ValueMode` is the
  resolved mode.
- `Rows` is the lazy stream: the header row (`Kind = Header`), top table summaries, then data, group header, group
  summary rows, then bottom table summaries. One `KumikoExportRow` instance is reused for every row; a cell is
  `{ Value, Text }` (`Value` is the typed value for data rows and the text for header and title cells).
- `ForEachRowAsync` adds the async contract: pool-thread or chunked caller-thread reads, cancellation every 256 rows,
  throttled progress. A writer that buffers passes `flushAsync` so a caller-thread export can send its buffer between
  chunks.
- `FormatDisplayText(value, column)` gives the display text of a value, for a typed writer whose number format cannot
  express the column's `Format`.
- `snapshot.TotalRows`, `FrozenColumnCount`, `Culture` and `Options` complete the picture.

## Performance

CSV is written row by row through a buffer of about 64 KB: memory use does not grow with the row count beyond the
snapshot (one reference and a small struct per row, about 40 bytes). A 100,000-row by 10-column export allocates a
few tens of megabytes in all (the cell texts) and retains none of it; the unit tests assert a bound. Prefer a file
stream over a `MemoryStream` for very large exports so the output is not held either.

The Excel writer streams the same way: each row is written as XML into a buffer that is compressed into the
`ZipArchive` entry whenever it passes about 64 KB, so memory stays flat whatever the row count, and the destination
need not be seekable. The style sheet is built from the columns before the first row, not from the data.
