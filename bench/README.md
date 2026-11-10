# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) projects. They are not part of `KumikoUI.sln` and do not run on pull requests: timings on a shared CI runner are too noisy to gate a build. The allocation budgets below are enforced by unit tests instead.

```bash
dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*'              # everything
dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*Render*'       # one class
dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*' --job short  # quick, approximate
```

The `Benchmarks` workflow (`.github/workflows/benchmarks.yml`) runs the suite on demand (Actions, "Run workflow") and every Monday, and uploads the reports as the `benchmark-results` artifact. Its numbers come from a shared Linux runner and drift from run to run; compare them with each other, not with the table below.

## Benchmarks

All use `[MemoryDiagnoser]` and deterministic data (row `i` is a pure function of `i`).

- `SkiaFrameBenchmarks` (#62): one grid-like frame (header, 30 x 8 cells, grid lines) drawn through `SkiaDrawingContext`, creating every `SKPaint`/`SKFont`/`SKTypeface` per frame (`PerFrameObjects`, the behavior before #62) versus reusing a long-lived `SkiaResourceCache` (`SharedCache`, what the hosts do). `WithText=false` leaves out the text calls, because SkiaSharp's `SKCanvas.DrawText` allocates a text blob per call either way. On an Apple M4 Max: 3,416 B and 148 us per frame without text versus 40 B (the context object) and 136 us with the cache; with text, 43,400 B / 527 us versus 39,720 B / 496 us.
- `PropertyAccessBenchmarks` (#17): reading a column for 10 000 rows through `KumikoSource.GetCellValue`, and sorting by it, with generated accessors (`[KumikoGridItem]`), `KumikoColumn.ValueAccessor`, the reflection fallback, and the compiled-expression lookup used before #17.
- `RenderBenchmarks` (#68, #70): one `KumikoRenderer.Render` frame of a 30-row × 10-column visible grid into a no-op `IDrawingContext`. `Plain` is a bare grid, `Featured` adds multi-column sort, a filter, grouping, group, caption and table summaries, a frozen column and the drag handle. `Scrolling` moves the plain grid a row and 7 px further each frame (200 offsets, then wraps), so each frame reveals a row the display text cache hasn't seen. `Plain_FrameStatsOn` draws the frame-time overlay too. The first two and `FrameStatsOn` measure the steady state after warm-up frames.
- `DataBenchmarks` (#70): the data pipeline on 100 000 rows, each benchmark one view rebuild: sort by int, string and date columns and by two columns; text-contains and numeric-greater-than filters; one- and two-level grouping with group and table summaries; sort, filter and group together; and `KumikoLayoutEngine.ComputeColumnWidths` for 10 columns.
- `IncrementalBenchmarks` (#70): adding and removing one item at the end and in the middle of an `ObservableCollection` of 100 000 items with no sort, filter or grouping active (handled without rebuilding the view).

## Measured numbers

Apple M4 Max (16 cores), macOS 27.2, .NET SDK 10.0.403 / runtime 10.0.14, Arm64 RyuJIT, Release build, 2026-11-10. Render rows are `--job medium` (2 launches × 15 iterations); the 100 000-row rows are `--job short` (3 iterations; the multi-millisecond rebuilds vary a few percent, GC-heavy ones more).

| Operation | Mean | Allocated |
|---|---:|---:|
| Render 30×10, plain | 2.9 µs | 0 B |
| Render 30×10, featured (sort, filter, group, summaries, frozen column, drag handle) | 6.3 µs | 0 B |
| Render 30×10 while scrolling (one new row per frame) | 3.7 µs | 501 B |
| Render 30×10, plain, frame-time overlay on | 3.2 µs (median) | 0 B |
| `ComputeColumnWidths`, 10 columns | 0.1 µs | 0 B |
| Sort 100K rows, int column | 54 ms | 86 MB |
| Sort 100K rows, string column | 72 ms | 1.1 KB |
| Sort 100K rows, date column | 211 ms | 94 MB |
| Sort 100K rows, two columns | 51 ms | 84 MB |
| Filter 100K rows, text contains | 2.4 ms | 0.5 KB |
| Filter 100K rows, numeric greater than | 2.2 ms | 5.3 MB |
| Group 100K rows, one level, with summaries | 28 ms | 17.5 MB |
| Group 100K rows, two levels, with summaries | 46 ms | 18.8 MB |
| Sort + filter + group together | 83 ms | 96 MB |
| `ObservableCollection` add and remove one item, 100K rows | 128 µs | 432 B |
| `ObservableCollection` insert and remove in the middle, 100K rows | 178 µs | 432 B |

At 60 fps a frame has 16.7 ms; the render loop itself uses well under 1% of that for a 30 × 10 viewport. What a user sees also includes the drawing backend (SkiaSharp), which these benchmarks leave out by drawing into a no-op context, so per-device frame rates must be measured on the device with the frame-time overlay (below).

Two numbers stand out and are the next targets rather than something to hide: sorting an int or date column allocates about 900 bytes per row (the string column sorts in place and allocates almost nothing, so the cost is in the int and date key handling; not yet investigated), and every sort, filter or grouping change rebuilds the whole 100K-row view on the calling thread.

## Performance budgets

A budget is a number a change must not break. The first column is the intent, the second says how it is checked.

| Operation | Time budget (reference machine above) | Allocation budget | How it is checked |
|---|---|---|---|
| Render a 30×10 frame, steady state | ≤ 20 µs | exactly 0 B | `RenderAllocationTests` (0 B, every CI run); time by benchmark |
| Render with the frame-time overlay on, between text updates | ≤ 20 µs | exactly 0 B | `PerformanceBudgetTests` (0 B); time by benchmark |
| Scrolling frame (one new row) | ≤ 20 µs | ≤ 2 KB | by benchmark |
| Sort 100K rows | ≤ 300 ms | ≤ 160 MB | `PerformanceBudgetTests` (allocation); time by benchmark |
| Filter 100K rows (text or numeric) | ≤ 10 ms | ≤ 12 MB | `PerformanceBudgetTests` (allocation); time by benchmark |
| Group 100K rows, 1–2 levels, with summaries | ≤ 100 ms | ≤ 40 MB | `PerformanceBudgetTests` (allocation); time by benchmark |
| Add or remove one item (`ObservableCollection`, no transforms) | ≤ 1 ms | ≤ 4 KB | `PerformanceBudgetTests` (allocation); time by benchmark |

- **Allocation budgets gate CI.** They are unit tests in `tests/KumikoUI.Core.Tests/Rendering` that count bytes with `GC.GetAllocatedBytesForCurrentThread`, which is deterministic. The data-pipeline ceilings are about 1.7 to 2× what the operations allocate today: they catch an accidental per-row allocation, not drift. When an optimization lowers the real number, lower the ceiling with it.
- **Time budgets do not gate CI.** They are about 3× the measured mean, for the reference machine. Check them by running the benchmarks before and after a change to a hot path; a change that breaks one explains why in its pull request.
- The frame rate a device reaches depends on the device, the data and the Skia backend. Publish a device's number from the overlay, not from these benchmarks.

## Frame-time overlay

`ShowFrameStats` (MAUI bindable property, Uno dependency property, or `KumikoRenderer.ShowFrameStats` in Core) draws `render ms | fps | bytes allocated per frame` in the grid's top-right corner, averaged over 250 ms windows. Render time covers `KumikoRenderer.Render` (layout, cells and chrome into the drawing context); fps is the rate of frames actually drawn, so it reads low while the grid is idle and redraws only on demand. Turn it on in the Large Data page of either sample app, scroll, and read it. Off, it costs one boolean check per frame.
