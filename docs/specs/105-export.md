# Spec: export to CSV, Excel and PDF (#105)

Status: draft for review. Blocked by #44 (clipboard, PR #211). Related: #74 (font fallback), #108 (cell selection).

## 1. Problem and goals

The grid cannot export what it shows. Four of the seven competitors in the release-readiness research ship export (Syncfusion MAUI: Excel and PDF; DevExpress: many formats; Syncfusion WinUI: Excel; WinUI.TableView: CSV). CSV is cheap and dependency-free; Excel and PDF need an explicit library decision.

Goals:

1. One small, format-neutral export API in `KumikoUI.Core` that reads a `KumikoSource` and writes to a `Stream`.
2. CSV in Core, RFC 4180 compliant, with formula-injection protection, sharing the field-escaping code with the clipboard (`KumikoTsvFormatter`, #44).
3. Excel (`.xlsx`) in an optional package, with typed cells and the column formats, without pulling a heavy dependency into every app.
4. PDF in `KumikoUI.SkiaSharp` via `SKDocument`, paginated, header repeated per page.
5. 100K rows export without freezing the UI and without materializing the output (or an intermediate cell table) in memory; cancellation and progress are first-class.
6. Everything stays trim and Native AOT safe (`IsAotCompatible` is `true` on Core and SkiaSharp today).

Non-goals:

- A save dialog, share sheet or file picker (app-owned, see section 9). The exporter writes to a `Stream` and never touches the file system.
- Import (CSV/Excel to grid).
- Export of arbitrary cell renderer output (Image, Template, progress bars, action buttons). Those columns are skipped by default or exported as their text.
- Pixel-exact printing of the on-screen grid (screenshot or print preview). PDF is a table report in the grid's colors, not a canvas capture.
- Rich Excel features: formulas, charts, conditional formatting, multiple sheets, cell merging.
- Right-to-left layout and complex text shaping in PDF (follows X10, HarfBuzz).
- PDF/A, tagged PDF (accessibility), encryption.
- Built-in export UI (toolbar button, context menu). A future context-menu or column-chooser spec can call the API.

## 2. What the code gives us

| Need | Where | Notes |
|---|---|---|
| Column set and order | `KumikoSource.Columns` (`IReadOnlyList<KumikoColumn>`), `KumikoColumn.IsVisible`, `Header`, `PropertyName`, `ValueAccessor`, `Format`, `ColumnType`, `TextAlignment`, `CellStyle`, `HeaderCellStyle`, `Width`, `IsFrozen`, `IsFrozenRight` | `ReorderColumn` mutates the list, so list order is the user's order. Frozen columns draw at the edges regardless of list position (`KumikoLayoutEngine.GetVisibleFrozenColumns`, `GetVisibleRightFrozenColumns`), so the *visual* order is left-frozen, then the rest, then right-frozen. |
| Rows in view | `RowCount`, `GetItem(visibleRow)`, `IsGroupHeaderRow`, `IsGroupSummaryRow`, `IsNonDataRow`, `GetGroupHeaderInfo`, `GetGroupSummaryRowInfo` | `RowCount` includes group header and group summary rows. The internal `FlatViewRow`/`FlatRowKind` list is not exposed. |
| Cell value and text | `GetCellValue`, `GetCellDisplayText`, `FormatCellValue(value, column)` (uses `KumikoComparer.Format` with `Culture` and `KumikoColumn.Format`), private `CreateColumnReader` | `Culture` null means the current culture at the time of use. The renderer reads through a per-column text cache (`CellTextCache`, UI thread); export must not use it. |
| Grouping | `GroupDescriptions`, `GroupHeaderInfo` (`Level`, `Key`, `DisplayText`, `HeaderText`, `ItemCount`, `GroupPath`) | **Collapsed groups are absent from the flat view and their group summaries are not computed** (`BuildGroupedFlatView` only recurses and calls `ComputeGroupSummaries` when `isExpanded`). So "export the view" and "export what is on screen" differ whenever a group is collapsed. |
| Summaries | `ComputedTopSummaries`, `ComputedBottomSummaries` (table level), `GetGroupSummaries(groupPath)`, `GetCaptionSummaryText(groupPath)`; `ComputedSummaryRow` has `Title`, `Values` (text by property name), `RawValues`, `Aggregates` (`ComputedSummaryValue.Value`/`Text`) | Table summaries cover all filtered rows, not only the visible window. |
| Filtered-out items | none | `_sourceItems` is private; there is no public way to read items hidden by a filter. |
| Selection | `KumikoSelectionModel.SelectedItems`, `SelectedRows`, `IsRowSelected` | Row selection only today. #108 adds cell ranges. |
| Style | `KumikoStyle` (colors, `CellFont`, `HeaderFont`, `RowHeight`, `CellPadding`, `GridLineColor`), `KumikoCellStyle` (nullable overrides; `Merge`) | |
| Accessors and AOT | `KumikoAccessorRegistry` (`ReflectionSwitchName`), `IKumikoAccessorProvider`, `[KumikoGridItem]` source generator | Export reads through `KumikoColumn.ValueAccessor` or the registry, so it needs no new reflection. |
| Clipboard format | `KumikoTsvFormatter` (#44): `EscapeField`, `Format(source, rows, includeHeaders)`; display text, visible columns, display order, group rows skipped | Same column and text rules as export; escaping rules are RFC 4180 with a tab delimiter. |
| PDF | `SkiaDrawingContext(SKCanvas, SkiaResourceCache)` implements `IDrawingContext`; `SkiaResourceCache` is bounded and per-owner; `SkiaFontRegistrar` | `SKDocument.CreatePdf(Stream, SKDocumentPdfMetadata)` gives a canvas per page that `SkiaDrawingContext` can draw on. |

Consequences for the design:

- The exporter needs a **view snapshot** from `KumikoSource` that includes collapsed groups and their summaries, and (optionally) the unfiltered items. This is a small internal addition to `KumikoSource`, not a public surface.
- Display text must be produced by a path that does not touch the `CellTextCache` and is safe off the UI thread.

## 3. Scope: what gets exported

### 3.1 Rows

`KumikoExportRows` selects the row set. Order is always the grid's current sort; grouping always follows the current `GroupDescriptions` unless `GroupLayout` says otherwise (3.4).

| Value | Meaning | Default |
|---|---|---|
| `Filtered` | Every row passing the current filters, sorted and grouped as in the grid, **including rows in collapsed groups** and the summaries of collapsed groups | yes |
| `Visible` | Exactly the rows currently in the flat view (`0..RowCount-1`): collapsed groups contribute only their header | |
| `Selected` | Only selected rows, in view order (3.2) | |
| `All` | Every source item, ignoring filters; sort and grouping are ignored (source order) so "all" means "the data", not "a view" | |

`Filtered` is the default because the common intent ("download my table") is "the rows I filtered", and hiding rows because a group is collapsed surprises users. `Visible` is for exact WYSIWYG.

`All` needs the source items. `KumikoSource` gets an internal accessor; whether it becomes public API is an open question (section 14, Q4).

Frozen top rows (`FrozenRowCount`) are ordinary rows for export purposes.

### 3.2 Selection and ranges

`Selected` takes its rows from an explicit argument rather than from the control, so Core stays independent of the host:

```csharp
public sealed record KumikoExportOptions
{
    public KumikoExportRows Rows { get; init; } = KumikoExportRows.Filtered;
    public KumikoExportSelection? Selection { get; init; }   // required when Rows == Selected
    // ...
}

public sealed class KumikoExportSelection
{
    public static KumikoExportSelection FromItems(IEnumerable<object> items);
    public static KumikoExportSelection FromRows(IEnumerable<int> visibleRows);   // same input as KumikoTsvFormatter.Format
    public static KumikoExportSelection FromSelection(KumikoSelectionModel selection);
}
```

Rows are matched to the snapshot by item reference (`FromItems`, `FromSelection`) or by visible-row index (`FromRows`, resolved at snapshot time, group rows skipped, duplicates ignored), and written in view order. Selected rows inside a group keep their group header as context only when `GroupLayout` is `Rows` (an empty group is not written).

Cell ranges (#108): once the cell-selection model lands, add `FromRange(CellRange)`/`FromRanges(...)` that restrict both rows and columns (the column subset is the range's columns in display order). The export design needs nothing else from #108 beyond a stable "set of (row, column) rectangles in visible coordinates". #108 should expose exactly that, and `KumikoTsvFormatter` should accept the same type so copy and export agree. A range that spans group header rows skips them, as `KumikoTsvFormatter` does.

### 3.3 Columns

Default: columns with `IsVisible`, in visual order (left-frozen, scrollable, right-frozen; list order inside each), the same as what the user sees. `KumikoTsvFormatter` uses plain list order of visible columns; the two should share one internal helper (`KumikoExportColumns.Resolve`) so frozen-column ordering cannot drift between copy and export (see Q7).

Columns that have no text representation (`KumikoColumnType.Image`, `Template`, or a `CustomCellRenderer` with no `ValueAccessor`/`PropertyName`) are skipped unless the caller lists them explicitly in `Columns`.

Options: `IReadOnlyList<KumikoColumn>? Columns` (explicit set and order), `bool IncludeHiddenColumns`, `Func<KumikoColumn, bool>? ColumnFilter`. The column's `Header` is the header text; `HeaderSelector` can override it (for example to localize or to export machine names).

### 3.4 Headers, groups and summaries

- `IncludeHeaders` (default true): one header row (repeated per page in PDF).
- `GroupLayout` (default `Rows`):
  - `Rows`: mirror the grid. A group header row (`HeaderText: DisplayText (ItemCount)`, plus `GetCaptionSummaryText` when present) in the first exported column, the group's data rows, then the group summary rows. Nested groups indent by `Level` in PDF and use an outline level in Excel; in CSV they are plain rows (no indentation, because leading spaces corrupt data).
  - `Columns`: data-friendly. No group header rows; each data row is prefixed by one column per `GroupDescription` holding the group key text. This is the right shape for pivot tables and re-import. Group summary rows are omitted.
  - `None`: ignore grouping; rows are in the grid's sort order.
- `IncludeSummaries` (default true when `GroupLayout` is `Rows`): table summaries (`ComputedTopSummaries` at the top, `ComputedBottomSummaries` at the bottom) and group summaries. A summary value is placed in the exported column whose `PropertyName` matches `ComputedSummaryValue.PropertyName` (case-insensitive, as `ComputedSummaryRow.GetRawValue` does); the summary `Title` goes in the first column when it is empty. If a summary row has several aggregates for one property, the exporter follows the renderer's rule for which one a column shows; this needs confirming in `KumikoRenderer` during implementation, with the fallback of one export row per aggregate type (Q9).
- Summary values are exported computed (value, not a spreadsheet formula), because custom aggregates (`SummaryColumnDescription.CustomAggregate`) cannot be expressed as formulas and the numbers must equal what the grid shows.

### 3.5 Raw value versus display text

`KumikoExportValueMode`:

| Mode | CSV | XLSX | PDF |
|---|---|---|---|
| `DisplayText` | the text on screen (`FormatCellValue`: column `Format` in the export culture) | text cell | always |
| `RawValue` | invariant, round-trippable text (numbers `R`, dates ISO 8601, bool `true`/`false`) | typed cell (number, date serial, bool) plus an Excel number format derived from `Format` | treated as `DisplayText` |
| `Auto` (default) | `DisplayText` | `RawValue` | `DisplayText` |

Rationale: a CSV for a human who opens it in Excel wants what the grid showed; a CSV for another program wants raw (callers pick `RawValue`). An `.xlsx` is for a spreadsheet, so numbers and dates must be real numbers and dates, otherwise sort and sum do not work.

Excel number-format mapping is best effort: .NET standard formats `N0..N4`, `C`, `P`, `D`, `d`, `g` and common custom strings (`0.00`, `#,##0`, `yyyy-MM-dd`, `HH:mm`) map to Excel formats; an unmappable `Format` falls back to a text cell with the display text so the sheet never shows a different value than the grid. Currency symbols come from the export culture.

### 3.6 Culture

`KumikoExportOptions.Culture` defaults to `source.Culture ?? CultureInfo.CurrentCulture`, captured at snapshot time so the whole export is consistent even if the thread culture changes. It drives display text and the CSV list-separator preset. Group key text is computed by the source with its own culture and is unchanged. Raw mode always formats with the invariant culture.

## 4. API shape

All new public types are documented (`<summary>` on every public member; doc warnings are errors, #54).

### 4.1 Core (`KumikoUI.Core`, namespace `KumikoUI.Core.Export`)

```csharp
public interface IKumikoExporter
{
    string FormatName { get; }       // "CSV", "Excel", "PDF"
    string FileExtension { get; }    // ".csv"
    string MediaType { get; }        // "text/csv"

    Task ExportAsync(
        KumikoSource source,
        Stream destination,
        KumikoExportOptions? options = null,
        IProgress<KumikoExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public readonly record struct KumikoExportProgress(int RowsWritten, int TotalRows);

public sealed class KumikoCsvExporter : IKumikoExporter
{
    public KumikoCsvExporter(KumikoCsvOptions? csv = null);
    // ...
}
```

The issue text proposes `IGridExporter`; this spec renames it to `IKumikoExporter` to follow the `Kumiko*` naming used by every other public type.

Two layers of options keep "what" separate from "how":

- `KumikoExportOptions` (what): `Rows`, `Selection`, `Columns`, `IncludeHiddenColumns`, `ColumnFilter`, `IncludeHeaders`, `HeaderSelector`, `GroupLayout`, `IncludeSummaries`, `ValueMode`, `Culture`, `ReadOnCallerThread`.
- Per-exporter options (how), given to the exporter constructor: `KumikoCsvOptions`, `KumikoXlsxOptions`, `KumikoPdfOptions` (sections 5 to 7).

Both are immutable `sealed record`s with `init` properties and sensible defaults (`new KumikoExportOptions()` exports what the user sees). Records and plain properties keep everything AOT-safe (no serializer, no reflection).

Convenience extensions in `KumikoExportExtensions` (CSV only, since Core knows only CSV):

```csharp
public static Task ExportToCsvAsync(this KumikoSource source, Stream destination,
    KumikoExportOptions? options = null, KumikoCsvOptions? csv = null,
    IProgress<KumikoExportProgress>? progress = null, CancellationToken cancellationToken = default);
public static string ExportToCsvString(this KumikoSource source, KumikoExportOptions? options = null, KumikoCsvOptions? csv = null);
```

Optional packages add their own extension (`ExportToExcelAsync`, `ExportToPdfAsync`) so a call site reads the same whichever format.

### 4.2 Internals

```
KumikoSource --CreateExportSnapshot(options)--> KumikoExportSnapshot   (internal, immutable)
                                                  item references in output order
                                                  group structure incl. collapsed groups
                                                  computed table + group summaries
                                                  column readers, comparer/culture
                                                       |
                                                       v
                                        IEnumerable<KumikoExportRow>   (lazy, one row at a time)
                                                       |
                                                       v
                                      format writer (CSV / XLSX / PDF) --> Stream
```

- `KumikoExportRow` has a `Kind` (`Header`, `Data`, `GroupHeader`, `GroupSummary`, `TableSummary`), a `Level`, and cells of `{ object? Value; string Text }`. The enumerator reads the cells of one row, hands them to the writer, and reuses its buffers; nothing proportional to the row count is allocated beyond the snapshot.
- The snapshot copies item references (100K references is about 800 KB) and reads **nothing** from the items on the calling thread. It captures the `KumikoComparer` (culture) and the column readers. This is the only part that must run on the UI thread: O(rows) reference copying plus, for collapsed groups only, a grouping pass (`BuildGroupedFlatView` with expansion forced on, written to a separate list so the live view is untouched).
- Reading values (the user's `ValueAccessor` / property getters) then runs on a thread-pool thread by default so the UI stays responsive. That is only safe if the item getters are thread-safe, which is not guaranteed (an item can be bound to UI state). `KumikoExportOptions.ReadOnCallerThread` (default false) forces chunked reads on the calling thread with `await Task.Yield()` between chunks of about 2,000 rows. This trade-off is open question Q3.
- A concurrent mutation of an item during export produces whatever the getter returns at that moment; the snapshot freezes row membership and order, not values. Documented.

### 4.3 Async, cancellation, progress, streaming

- `ExportAsync` returns when the last byte is flushed to `destination`. The stream is **not** closed or disposed (the caller owns it).
- Cancellation: the token is checked every 256 rows and between pages. On cancellation the method throws `OperationCanceledException`; the stream is left with partial content and the caller discards it. The exporter never writes anywhere but `destination`.
- Progress: `IProgress<KumikoExportProgress>` is called at most every 1% of rows or 100 ms, whichever is less frequent, and once at completion. `TotalRows` is known up front from the snapshot (exported rows, not counting the header).
- No full materialization: CSV writes through a `StreamWriter` with a 64 KB buffer, row by row. XLSX streams the sheet XML into a `ZipArchive` entry as rows are produced (6.3). PDF writes a page at a time through `SKDocument`.
- Exceptions: `ArgumentException` for invalid combinations before anything is written (for example `Rows == Selected` with no selection, or more than Excel's 1,048,576 rows or 16,384 columns), so a failed validation leaves the stream untouched.

## 5. CSV (Core)

`KumikoCsvOptions` (all `init`, with defaults):

| Option | Default | Notes |
|---|---|---|
| `Delimiter` | `,` | Any single char other than quote, CR, LF. |
| `UseCultureListSeparator` | false | When true and `Delimiter` is not set, use `Culture.TextInfo.ListSeparator` (`;` in de-DE), the delimiter Excel in that locale expects when double-clicking a `.csv`. |
| `LineEnding` | `"\r\n"` | RFC 4180. Same as `KumikoTsvFormatter.RowSeparator`. |
| `EndWithLineEnding` | true | RFC 4180 allows either; matches clipboard output. |
| `Encoding` | UTF-8 | |
| `EmitByteOrderMark` | true | Excel needs the BOM to read UTF-8 non-ASCII when opening a `.csv` directly. Turn off for strict machine consumers (Q1). |
| `QuoteAllFields` | false | |
| `FormulaInjection` | `Prefix` | See 5.2. |
| `NullText` | `""` | Text for null values. |

### 5.1 Format and escaping

RFC 4180: records separated by CRLF, fields separated by the delimiter, a field is quoted if it contains the delimiter, a double quote, CR or LF, and an embedded quote is doubled. Empty string and null are both written as an empty (unquoted) field; `QuoteAllFields` writes `""` for empty. A field with leading or trailing spaces is quoted so parsers that trim keep them.

Shared escaping with the clipboard: the quoting logic moves to one internal helper used by both formatters:

```csharp
internal static class KumikoDelimitedText
{
    // Quote when the field contains the delimiter, '"', '\r' or '\n'.
    internal static string EscapeField(string? field, char delimiter);
}
// KumikoTsvFormatter.EscapeField(f) => KumikoDelimitedText.EscapeField(f, '\t')
```

For the tab delimiter this is exactly the rule #44 ships (`\t`, `\r`, `\n`, `"` trigger quoting), so TSV output does not change; a parity test asserts that for every generated input. This is a small follow-up refactor of #44's file once it merges, not a change to PR #211.

Cell text is the same display text the clipboard uses (`FormatCellValue(GetCellValue(...))`), produced through the snapshot's captured readers rather than the UI-thread cache, so copy, CSV and the grid agree character for character.

### 5.2 Formula (CSV) injection

A cell whose text starts with `=`, `+`, `-`, `@`, tab (0x09) or CR (0x0D) is interpreted as a formula by Excel, LibreOffice and Google Sheets. Data exported from a grid is frequently user-supplied, so this is a real attack vector (OWASP "CSV Injection").

Default behavior (`FormulaInjection.Prefix`): when the **raw value is a string** and its text starts with one of those characters, prefix a single quote `'` before quoting and escaping. Numbers, dates, booleans and other non-string raw values are never altered, so a negative number formatted `-5` stays `-5`. The same applies to group key text and summary titles that come from data. Column headers come from the developer, not data, and are not altered.

Trade-offs, documented in the XML docs and the cookbook:

- A string value that really is `-5` or `+49 171 ...` (a phone number) in a text column gets a visible leading `'` in Excel. `FormulaInjection.None` turns the protection off for callers who export to a non-spreadsheet consumer. A middle option (skip the prefix for strings that parse as numbers) is not in v1 because heuristics weaken a security control (Q2).
- The XLSX and PDF writers do not need this: XLSX writes text as an inline string cell (`t="inlineStr"`), which Excel never evaluates, and PDF is not a spreadsheet.

## 6. Excel (.xlsx)

### 6.1 Options compared

| Option | License | Streaming write | Trim / Native AOT | Footprint | Notes |
|---|---|---|---|---|---|
| **A. Hand-written minimal OOXML writer** over `System.IO.Compression.ZipArchive` + `XmlWriter` | ours | yes, row by row into the zip entry | safe: only BCL APIs, no reflection, no serializer | about 700 lines, no package dependency | We own the file-validity risk (6.3); the feature surface stays small by design. |
| B. `DocumentFormat.OpenXml` (Open XML SDK) with `OpenXmlWriter` | MIT | yes (SAX-style writer; the DOM API does not stream) | v3 improved trimming but the assembly is very large and trim/AOT support should be verified per release | several MB | Verbose: styles, shared strings and relationships by hand anyway. |
| C. ClosedXML | MIT | no: whole workbook in memory | reflection heavy, builds on B, not annotated for trimming/AOT | large, pulls B plus font libraries | Friendly API, but 100K rows by 10 columns is hundreds of MB. Disqualifying for the memory goal. |
| D. MiniExcel | Apache-2.0 | yes | partially reflection-based mapping; dictionary and `IDataReader` paths avoid it; AOT status must be verified per version | small | Styling and number formats are limited; third-party dependency for a core feature. |
| E. EPPlus | PolyForm Noncommercial (paid commercial) | no | n/a | n/a | Rejected: license incompatible with an open-source library. |

The library facts above (trim/AOT status, memory behavior) are from general knowledge of those packages and are not measured in this repo; the implementation issue E3 should confirm them before the decision is final if maintainers want to reopen it.

**Recommendation: A, a hand-written writer.** The surface we need is small and fixed (one sheet, typed cells, a handful of cell styles, column widths, freeze pane, autofilter, outline levels). It is the only option that is both streaming and trim/AOT safe with no third-party dependency, and it removes a dependency decision from every downstream app's supply chain. The cost is correctness risk, which we contain with validation tests (section 11). If the feature surface later grows (multiple sheets, images, rich styles), the `IKumikoExporter` boundary allows replacing the implementation with a library behind the same package without touching Core.

### 6.2 Package placement

`KumikoUI.Export.Excel` (new, `net10.0`, `IsAotCompatible`, no dependencies beyond `KumikoUI.Core`). Even without a third-party dependency it is a separate package so that:

- Core and apps that only need CSV carry no OOXML code (the trimmer would remove it, but the package boundary also keeps the public surface and docs focused);
- the OOXML implementation can change (or move to a library) without a Core release;
- it matches the optional-package approach the issue asks for.

Public surface: `KumikoXlsxExporter : IKumikoExporter`, `KumikoXlsxOptions`, and the `ExportToExcelAsync` extension.

`KumikoXlsxOptions`: `SheetName` (default "Sheet1", sanitized to Excel's 31-character and forbidden-character rules), `FreezeHeader` (true), `AutoFilter` (true), `ColumnWidths` (`FromGrid` default: `KumikoColumn.Width` scaled to Excel character units; or `Fixed`), `HeaderStyle` (bold, fill from `KumikoStyle.HeaderBackgroundColor`), `ApplyColumnAlignment` (from `KumikoColumn.TextAlignment`), `UseGridColors` (false by default; when true, header and group header fills use `KumikoStyle`), `Title`/`Author` (document properties).

### 6.3 Implementation outline and correctness

Parts written: `[Content_Types].xml`, `_rels/.rels`, `xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `xl/styles.xml` (fonts, fills, borders, `numFmts`, `cellXfs` built from the columns' formats and alignment before the sheet is written), `xl/worksheets/sheet1.xml` (streamed), `docProps/core.xml` (optional).

- Cells: numbers `t="n"`, booleans `t="b"`, dates as serial numbers with a date style (1900 system; the 1900 leap-year bug handled for early dates, `DateTimeOffset` converted to its local `DateTime`), text as inline strings `t="inlineStr"` (no shared-string table to hold in memory, no formula evaluation). Characters illegal in XML 1.0 are stripped; `xml:space="preserve"` is set when text has leading or trailing whitespace.
- Streaming: the zip entry for the sheet is written through an `XmlWriter` over `ZipArchiveEntry.Open()`; `ZipArchive` in `Create` mode works on non-seekable streams (it writes data descriptors), so `destination` need not be seekable. The styles part must be known before the sheet is written, which is why styles are derived from the column list, not the data.
- Excel limits are pre-validated: 1,048,576 rows, 16,384 columns, 32,767 characters per cell (longer text is truncated by default, Q6).
- Group layout: `Rows` uses `<row outlineLevel="n">` and `<sheetFormatPr outlineLevelRow>` so Excel shows collapsible groups; `Columns` writes the key columns.
- Tests validate every generated workbook with the Open XML SDK validator (test-only dependency) plus a round trip; manual smoke test in Excel, Numbers and LibreOffice before release.

## 7. PDF (`KumikoUI.SkiaSharp`)

PDF belongs in `KumikoUI.SkiaSharp` rather than a new package: it already depends on SkiaSharp, `SKDocument` is part of it, and the drawing goes through the shared `IDrawingContext`. No new dependency and no new package (see Q11).

Public surface (namespace `KumikoUI.SkiaSharp.Export`): `KumikoPdfExporter : IKumikoExporter`, `KumikoPdfOptions`, `KumikoPdfStyle`, and the `ExportToPdfAsync` extension.

### 7.1 Rendering approach

`SKDocument.CreatePdf(destination, SKDocumentPdfMetadata)` gives an `SKCanvas` per page (`BeginPage(width, height)` / `EndPage()` / `Close()`). Each page is drawn through a `SkiaDrawingContext` over that canvas, so text measurement, ellipsizing (`DrawTextInRect`), clipping and font resolution use the code the grid uses on screen, including `SkiaFontRegistrar` fonts and the fallback logic from #74.

The export uses **its own `SkiaResourceCache`**, not the control's, because the control's cache is per-frame and tied to the UI thread (`BeginFrame`). Each page is a "frame" on the export cache; the cache is disposed when the export ends.

PDF layout is independent of the screen layout. The exporter does not reuse `KumikoRenderer` or `KumikoLayoutEngine` (they model an interactive viewport, frozen panes and scroll offsets); it does its own simple table layout from `KumikoPdfOptions`, drawing with the shared `IDrawingContext` primitives and `KumikoPaint`/`KumikoFont`.

### 7.2 `KumikoPdfOptions`

| Option | Default | Notes |
|---|---|---|
| `PageSize` | A4, in points (595 x 842) | `KumikoPageSize.A4`, `.Letter`, `.Legal`, `.Custom(w, h)`. |
| `Orientation` | `Auto` | `Auto` picks landscape when the columns' natural width exceeds the portrait content width. |
| `Margins` | 36 pt | |
| `ColumnFitting` | `ShrinkToFit` | See 7.3. |
| `MinFontScale` | 0.7 | Lowest font scale `ShrinkToFit` may use before it falls back to `SplitColumns`. |
| `RowHeight` | from `KumikoStyle.RowHeight` scaled with the font | |
| `WrapText` | false | When true a cell wraps and its row grows (the exporter measures; the grid itself has fixed row height until X4). |
| `RepeatHeader` | true | Column header on every page. |
| `RepeatGroupHeader` | true | The current group header(s) are repeated at the top of a page that continues a group, with "(continued)". |
| `Header` / `Footer` | footer `Page {page} of {pages}` | Text templates with `{page}`, `{pages}`, `{date}`, `{title}`. `{pages}` is computed from the snapshot row count and row heights when `WrapText` is false, otherwise omitted (Q10). |
| `Style` | `KumikoPdfStyle.Print` | Colors, fonts, grid line width, banding. `KumikoPdfStyle.FromGridStyle(KumikoStyle)` copies the on-screen look. Print default is white background, dark text, light borders, banding off, so it does not burn toner on a dark theme. |
| `Metadata` | title "Export" | Title, author, subject, creator; mapped to `SKDocumentPdfMetadata`. |
| `MaxPages` | 10,000 | Safety valve; exceeding it throws before writing the page rather than producing a multi-gigabyte file. |

### 7.3 Column fitting

Widths start from `KumikoColumn.Width` (what the user sees) so the PDF resembles the screen, scaled to the page.

- `ShrinkToFit` (default): scale all widths down proportionally to the content width, clamped by each column's `MinWidth`; if still too wide, scale the font down to `MinFontScale`; if still too wide, behave as `SplitColumns`.
- `SplitColumns`: columns are divided into **column bands** that each fit a page; pages are emitted band by band (all rows for band 1, then all rows for band 2). The first `RepeatColumns` columns (default 1) repeat in every band so rows stay identifiable. The snapshot is re-enumerable, so this costs one extra pass per band and no extra memory.
- `Fixed`: use widths as is; text is ellipsized; columns that do not fit are dropped.

Streaming constraint: the column plan needs only the column list, never the row content, so it is known before the first page. A data-driven `Auto` width that measures cell text would require a pass over all rows; v1 samples the first 1,000 rows at most.

### 7.4 Paging, size and speed

- Rows per page follow from page height and row height, so the page count is predictable when `WrapText` is false (100K rows at about 45 rows per page is about 2,200 pages).
- Pages are written as they complete. Skia's PDF backend buffers a page's content until `EndPage`, and writes the cross-reference table at `Close`. The memory behavior at 100K rows must be measured (benchmark in the test plan); if Skia's per-document state proves proportional to page count we rely on `MaxPages` and docs rather than a custom PDF writer.
- `SKDocument` throughput is far below CSV, so progress and cancellation (checked per page) matter most here.

### 7.5 Fonts and text

- Fonts resolve through `KumikoStyle.CellFont` / `HeaderFont` or `KumikoCellStyle.Font` and `SkiaFontRegistrar`, so a registered brand font is embedded in the PDF (Skia embeds typeface subsets) when the font's license permits embedding. That is the app's responsibility and is called out in the docs.
- Fallback: with #74 (per-glyph fallback, run segmentation via `MatchCharacter`) a cell like "José 👍 東京" draws every glyph, and Skia embeds each used typeface. Without #74 the PDF shows the same missing-glyph boxes as the screen. The PDF exporter takes no code dependency on #74 because it draws through `SkiaDrawingContext`, so the fix applies to PDF automatically. This is a *soft* dependency: ship PDF without it, document the limitation, and add one PDF test (CJK plus emoji) when #74 merges.
- Bold header uses a bold `KumikoFont`; alignment per column from `KumikoColumn.TextAlignment` and `KumikoCellStyle`; numeric columns are right-aligned by default, as in the grid.
- Not supported in v1: bidirectional text, complex shaping (X10), dynamic per-cell styles supplied by host-side resolvers (column-level static styles only: `KumikoColumn.CellStyle`), embedded images (`Image` column).

### 7.6 Trim/AOT

`SKDocument` is a thin native wrapper in a package that is already AOT-compatible. The exporter adds no reflection; a Native AOT smoke test (publish a console app that runs the PDF export) is part of the test plan.

## 8. Trim and AOT summary

| Piece | Trim | Native AOT | Why |
|---|---|---|---|
| CSV (Core) | safe | safe | `StreamWriter` and string operations; values come from existing accessors (`KumikoAccessorRegistry`, `ValueAccessor`) so the existing `IsReflectionPropertyAccessSupported` rules apply unchanged |
| XLSX hand-written | safe | safe | `ZipArchive`, `XmlWriter`, no reflection; number-format mapping is a switch |
| XLSX via Open XML SDK / ClosedXML / MiniExcel | needs verification, large | not guaranteed | reflection and type loading; one reason we do not ship them |
| PDF (SkiaSharp) | safe | safe | native interop already in the package |

Rule for all exporters: do not call `Type.GetProperty` or similar; read values only through `KumikoColumn.ValueAccessor` or the accessor registry the grid already uses. CI builds a small app per format with `PublishAot=true` to enforce it.

## 9. Host integration (documentation only)

The exporter writes to a `Stream`; **choosing where the bytes go is the app's job**. Core has no UI and no file-system dependency, and the grid controls (`KumikoUI.Maui`, `KumikoUI.Uno`) do not add a save dialog.

Recommended pattern: export to a `MemoryStream` for modest sizes or a temp-file `FileStream` for large ones, then hand it to the platform.

MAUI (CommunityToolkit.Maui `FileSaver`):

```csharp
await using var stream = new MemoryStream();
await grid.Source.ExportToCsvAsync(stream, cancellationToken: ct);
stream.Position = 0;
var result = await FileSaver.Default.SaveAsync("orders.csv", stream, ct);
if (!result.IsSuccessful) { /* canceled or failed */ }
```

Uno (WinUI `FileSavePicker`; initialize it with the window handle on WinAppSDK):

```csharp
var picker = new FileSavePicker { SuggestedFileName = "orders" };
picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
var file = await picker.PickSaveFileAsync();
if (file is null) return;
await using var stream = await file.OpenStreamForWriteAsync();
await source.ExportToCsvAsync(stream, cancellationToken: ct);
```

Both go into the cookbook (X9) and are not compiled in the libraries. Mobile apps often want "share" instead of "save": write to a temp file and use MAUI `Share.RequestAsync` or Uno `DataTransferManager`; also documentation only.

UI-thread rule for hosts: call `ExportAsync` from the UI thread (the snapshot reads `KumikoSource`); the awaited work continues off-thread by default.

## 10. Compatibility and public API impact

- All additions. The only changes to existing code are two internal additions to `KumikoSource` (snapshot creation, unfiltered item access) and the internal shared escaping helper that `KumikoTsvFormatter` (from #44) will call.
- New public API in Core: `IKumikoExporter`, `KumikoExportOptions`, `KumikoExportRows`, `KumikoExportSelection`, `KumikoExportValueMode`, `KumikoExportGroupLayout`, `KumikoExportProgress`, `KumikoCsvExporter`, `KumikoCsvOptions`, `KumikoFormulaInjection`, `KumikoExportExtensions`. Each fully documented.
- New package: `KumikoUI.Export.Excel` (added to packing, `IsAotCompatible`, versioned with the rest by MinVer).
- CHANGELOG and README entries are added by the implementation PRs, not by this spec.

## 11. Test plan

Core (`tests/KumikoUI.Core.Tests`, xunit; `KumikoUI.Core` already exposes internals to it):

- **CSV format**: golden outputs for embedded commas, quotes, CR/LF/CRLF, leading/trailing spaces, empty string vs null, Unicode (BMP and surrogate pairs), very long fields; BOM on/off; `QuoteAllFields`; `LineEnding`. A small RFC 4180 reader in the test project parses the output back and compares to the source cells (round trip) for randomized data (fixed seed).
- **Escaping parity** with `KumikoTsvFormatter`: for all generated inputs, `KumikoTsvFormatter.EscapeField(x) == KumikoDelimitedText.EscapeField(x, '\t')`.
- **Formula injection**: strings starting with `=`, `+`, `-`, `@`, tab, CR are prefixed; numbers/dates/bools with negative or `+` formatting are not; `None` disables; headers unaffected; group key text protected.
- **Scope**: `Filtered` includes rows and summaries of collapsed groups; `Visible` does not; `All` ignores filters and sort; `Selected` honors view order, ignores duplicates and group rows; selection by item after a sort change.
- **Columns**: hidden skipped, `IncludeHiddenColumns`, explicit `Columns`, frozen-left/right visual order, Image/Template columns skipped, `HeaderSelector`.
- **Groups and summaries**: `Rows` layout equals the grid's flat view for expanded groups (compare against `GetGroupHeaderInfo` and `GetGroupSummaries`); `Columns` layout adds key columns; table top and bottom summaries; placement by property name; nested groups.
- **Culture**: de-DE, fr-FR (NBSP thousands separator), tr-TR, ar-SA digits; `source.Culture` vs current culture vs override; raw mode invariant regardless of culture; thread culture changed mid-export has no effect.
- **Async behavior**: cancellation mid-export throws `OperationCanceledException` within a bound (checked every 256 rows); already-canceled token throws before writing; progress is monotonic, throttled and ends at `TotalRows`; the stream is not disposed; a non-seekable destination works; an exception in a user `ValueAccessor` surfaces unchanged.
- **Large data**: 100K rows x 10 columns CSV: allocated bytes and peak memory far below what holding the output or a cell table would need (assert a bound on `GC.GetTotalAllocatedBytes` delta and on retained memory after a forced GC mid-export); caller-thread chunk mode yields between chunks (observed with a test `SynchronizationContext`).
- **Snapshot isolation**: mutating the source (sort, filter, `SetItems`) after `ExportAsync` starts does not change the output.

Excel package (`tests/KumikoUI.Export.Excel.Tests`):

- Structural: required parts present, XML well-formed, content types correct, sheet dimension ref correct.
- **Validation**: each generated workbook passes the Open XML SDK validator (test-only reference), including styles with many number formats and 100K rows.
- Round trip: read back with the SDK reader; types (number, date, bool, text), number formats, header style, freeze pane, autofilter, outline levels.
- Dates: 1900 leap-year edge, `DateTimeOffset`, `DateOnly`, `TimeOnly`, min/max values; illegal XML characters stripped; sheet-name sanitizing; Excel limits (rows, columns, 32,767 chars).
- Non-seekable stream; streaming memory bound at 100K rows.
- Manual release check: open in Excel, Numbers, LibreOffice and Google Sheets; no repair prompt.

PDF (SkiaSharp tests):

- Output starts with `%PDF-`; page count matches the prediction for fixed row height; the header repeats on every page and group headers repeat with "(continued)", verified with a text extractor (for example PdfPig, test-only, Apache-2.0).
- Column fitting: `ShrinkToFit` never exceeds the content width; `SplitColumns` repeats the key columns; `Orientation.Auto`.
- Text: right alignment for numeric columns, ellipsizing, `WrapText` growth; CJK plus emoji (after #74); registered font embedding.
- `MaxPages` guard; cancellation between pages; 100K-row run with recorded time and peak memory (benchmark, not a gate).
- Native AOT smoke test publishing a console app that exports all three formats.

CI additions: the new test project, the Native AOT publish check, and a size/time benchmark kept as a BenchmarkDotNet entry (the repo already pins BenchmarkDotNet) rather than a unit test.

## 12. Implementation plan

Each item is one issue and one PR, in this order (E1 after #44 merges).

**E1. Core: export snapshot, options and CSV exporter**
- `IKumikoExporter`, `KumikoExportOptions`, `KumikoExportRows`, `KumikoExportSelection`, `KumikoExportValueMode`, `KumikoExportGroupLayout`, `KumikoExportProgress`, `KumikoCsvExporter`, `KumikoCsvOptions`, `ExportToCsvAsync`/`ExportToCsvString`, all documented.
- Internal `KumikoSource.CreateExportSnapshot` including collapsed groups, summaries, and unfiltered items.
- Acceptance: scope, column, group and summary behaviors in section 3 covered by tests; a 100K-row export streams with bounded memory; cancellation and progress work; the stream is not disposed; doc warnings clean; existing tests unchanged.

**E2. CSV hardening: shared escaping, formula injection, culture**
- `KumikoDelimitedText.EscapeField` shared with `KumikoTsvFormatter` (clipboard output unchanged); `FormulaInjection`, `UseCultureListSeparator`, BOM and line-ending options.
- Acceptance: parity test with TSV passes for generated inputs; injection tests (strings prefixed, numbers untouched); round trip against an RFC 4180 reader; culture matrix tests (de-DE, fr-FR, tr-TR).

**E3. Excel package `KumikoUI.Export.Excel`**
- New project and pack entry; hand-written streaming writer; typed cells, number-format mapping, styles, freeze pane, autofilter, outline groups; `ExportToExcelAsync`.
- Acceptance: workbooks validate with the Open XML SDK validator; open without a repair prompt in Excel, LibreOffice and Numbers; 100K x 10 export streams with bounded memory; trim and Native AOT publish succeed with no warnings; Excel limits validated before any byte is written.

**E4. PDF exporter in `KumikoUI.SkiaSharp`**
- `KumikoPdfExporter`, `KumikoPdfOptions`, `KumikoPdfStyle`, page sizes, paging, repeated headers, `ShrinkToFit` and `SplitColumns`, own `SkiaResourceCache`, `ExportToPdfAsync`.
- Acceptance: multi-page PDF with repeated header and footer page numbers; grouped data renders group headers and summaries; registered fonts embedded; cancellation between pages; `MaxPages` guard; Native AOT smoke passes; a 100K-row run is recorded.

**E5. Selection and range export integration**
- `KumikoExportSelection.FromSelection`, `FromRange(s)` once #108 lands; `KumikoTsvFormatter` and export share the range type and column-order helper.
- Acceptance: copy and export of the same range produce identical cell text; a range spanning group rows skips them; tests with row and cell selection.

**E6. Documentation and cookbook**
- A `docs/EXPORT.md` guide (options, scopes, culture, injection note, size and performance guidance), MAUI `FileSaver` and Uno `FileSavePicker` examples, sample-app buttons (CSV, Excel, PDF), CHANGELOG and README entries.
- Acceptance: every example compiles in the sample app on the MAUI and Uno heads; docs cross-link the clipboard and selection specs.

Possible later issues, outside this spec: multi-sheet Excel, per-cell dynamic styles in PDF, HarfBuzz shaping and RTL in PDF (X10), image columns in PDF.

## 13. Risks

- **Hand-written OOXML validity.** Mitigated by SDK validation in tests and a manual multi-application release check; the format subset is tiny and fixed.
- **Thread safety of user getters** when reading off the UI thread. Mitigated by the `ReadOnCallerThread` option and clear docs; the default favors responsiveness (Q3).
- **PDF memory at very large page counts** if Skia retains per-page state. Mitigated by `MaxPages` and a recorded benchmark; no custom PDF writer is planned.
- **Display text versus typed values drifting** (number-format mapping). Mitigated by falling back to text cells whenever a `Format` cannot be mapped exactly.
- **Spec drift with #44 and #108.** The shared escaping helper, the column-order helper and the range type are the three integration points; both should land these as internal helpers that this work can adopt.

## 14. Open questions

1. **CSV BOM default.** On (Excel opens non-ASCII correctly) or off (RFC-clean, friendlier to parsers)? Proposed: on, documented, one switch.
2. **Formula injection default.** Prefix every string starting with `=+-@`, accepting a visible `'` on legitimate values such as phone numbers, or add a numeric-looking heuristic? Proposed: strict prefix by default, `None` to opt out, no heuristic in v1.
3. **Where are values read?** Thread pool by default (UI stays responsive, but user getters must be thread-safe) versus caller-thread chunks with yields (always safe, slight UI hitches). Proposed: thread pool default, caller-thread option.
4. **Public access to unfiltered items.** `Rows = All` needs the source items. Expose `KumikoSource.Items` publicly, or keep access internal? Proposed: internal for now.
5. **Default `Rows` scope.** `Filtered` (collapsed groups expanded in output) versus `Visible` (strict WYSIWYG). Proposed: `Filtered`.
6. **Over-long cells and over-limit sheets in Excel.** Truncate long text silently versus throw; for too many rows, throw versus continue on a second sheet. Proposed: truncate text, throw for rows and columns.
7. **Frozen-column order in the clipboard.** `KumikoTsvFormatter` uses list order; export uses visual order. Should #44 adopt the shared helper before it merges?
8. **Excel dependency.** Is a roughly 700-line owned writer acceptable to maintainers, or should we depend on the Open XML SDK despite its size and trim status? Proposed: owned writer, revisit if the surface grows beyond one sheet.
9. **Summary rows with several aggregates per property.** Confirm how the renderer chooses the value a column shows, and whether export should emit one row per aggregate type.
10. **PDF page total** (`{pages}`) needs the total ahead of time. Accept omitting it when `WrapText` is on, or do a measuring pre-pass (doubles the time)?
11. **Package naming.** `KumikoUI.Export.Excel` versus `KumikoUI.Export.Xlsx`; and whether PDF should be its own `KumikoUI.Export.Pdf` package if a second PDF backend is ever added.
12. **Cell ranges from #108.** Needs a public, host-independent "set of (row, column) rectangles in visible coordinates"; confirm the type and its location (Core, near `KumikoSelectionModel`) with that spec.
