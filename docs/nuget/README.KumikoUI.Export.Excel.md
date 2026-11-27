![KumikoUI](https://raw.githubusercontent.com/TheEightBot/KumikoUI/main/images/logo.png)

# KumikoUI.Export.Excel

[![NuGet](https://img.shields.io/nuget/v/KumikoUI.Export.Excel.svg)](https://www.nuget.org/packages/KumikoUI.Export.Excel)
[![Downloads](https://img.shields.io/nuget/dt/KumikoUI.Export.Excel.svg)](https://www.nuget.org/packages/KumikoUI.Export.Excel)
[![License: MIT](https://img.shields.io/github/license/TheEightBot/KumikoUI.svg)](https://github.com/TheEightBot/KumikoUI/blob/main/LICENSE)

KumikoUI.Export.Excel writes a KumikoUI grid's data as an Excel workbook (`.xlsx`). Numbers and dates arrive as real numbers and dates in the number formats the columns use, with the grid's column widths, a bold, frozen header row with filter buttons, and groups as collapsible row outlines. It works with every KumikoUI host (.NET MAUI, Uno Platform, or your own) and targets `net10.0`.

The workbook is written row by row with the BCL's `ZipArchive`, so a 100,000-row export does not hold the file in memory and the destination stream need not be seekable. There is no third-party dependency and no reflection: the package is trimming and Native AOT safe.

Use the same version as your other KumikoUI packages: they are released together. Release notes: [CHANGELOG](https://github.com/TheEightBot/KumikoUI/blob/main/CHANGELOG.md).

## Quick start

```csharp
using KumikoUI.Core.Export;
using KumikoUI.Core.Models;
using KumikoUI.Export.Excel;

var source = new KumikoSource();
source.SetColumns(new[]
{
    new KumikoColumn { Header = "Product", PropertyName = "Name", Width = 180 },
    new KumikoColumn { Header = "Price", PropertyName = "Price", ColumnType = KumikoColumnType.Numeric, Format = "C2" },
    new KumikoColumn { Header = "Shipped", PropertyName = "Shipped", ColumnType = KumikoColumnType.Date, Format = "yyyy-MM-dd" },
});
source.SetItems(new[] { new Order("Lamp", 39.5m, new DateTime(2026, 3, 1)) });

// On the UI thread, with the grid's source (grid.DataSource in an app).
string path = Path.Combine(Path.GetTempPath(), "orders.xlsx");
await using var file = File.Create(path);
await source.ExportToExcelAsync(file, xlsx: new KumikoXlsxOptions { SheetName = "Orders", Title = "Orders" });

record Order(string Name, decimal Price, DateTime Shipped);
```

## What you get

- **Typed cells.** Numbers, booleans, `DateTime`, `DateTimeOffset` (its clock time), `DateOnly`, `TimeOnly` and `TimeSpan` become numbers, booleans, dates and times Excel can sort and sum. Text is written as inline strings, which Excel never evaluates, so a value starting with `=` stays text.
- **Number formats.** A column's .NET `Format` (`N0`-`N4`, `C`, `P`, `D`, `E`, `F`, date and time patterns such as `d`, `g`, `yyyy-MM-dd`, `HH:mm`, and custom numeric formats such as `#,##0.00`) becomes the matching Excel format, with the currency symbol of the export culture. A format Excel cannot show exactly is written as the text the grid shows, so the sheet never shows a different value than the grid.
- **Layout.** A bold header row (or the grid's header, group and summary colors with `UseGridColors`), column widths from the grid, the header and left-frozen columns frozen, an autofilter, group header and summary rows with outline levels, and table summaries.
- **Limits.** Excel's 1,048,576 rows and 16,384 columns are checked before any byte is written (`ArgumentException`); text longer than 32,767 characters is cut.

`KumikoExportOptions` (from KumikoUI.Core) chooses what is exported: the rows (filtered, visible, selected or all), the columns, the group layout, summaries and the culture. `KumikoXlsxOptions` sets the sheet name, `FreezeHeader`, `AutoFilter`, `ColumnWidths`, `ApplyColumnAlignment`, `UseGridColors` with `Style`, and the `Title` and `Author` document properties. `KumikoXlsxExporter` implements `IKumikoExporter` for code that picks a format at run time.

## Documentation

- [Exporting guide](https://github.com/TheEightBot/KumikoUI/blob/main/docs/EXPORT.md): options, scopes, culture, async, cancellation and progress, saving through the MAUI and Uno pickers
- [Full documentation and samples](https://github.com/TheEightBot/KumikoUI#readme)
- [Versioning policy](https://github.com/TheEightBot/KumikoUI/blob/main/docs/VERSIONING.md)
- [Issues](https://github.com/TheEightBot/KumikoUI/issues)
