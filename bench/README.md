# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) projects. They are not part of `KumikoUI.sln` and do not run in CI.

```bash
dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*'
```

- `SkiaFrameBenchmarks` (#62): one grid-like frame (header, 30 x 8 cells, grid lines) drawn through `SkiaDrawingContext`, creating every `SKPaint`/`SKFont`/`SKTypeface` per frame (`PerFrameObjects`, the behavior before #62) versus reusing a long-lived `SkiaResourceCache` (`SharedCache`, what the hosts do). `WithText=false` leaves out the text calls, because SkiaSharp's `SKCanvas.DrawText` allocates a text blob per call either way. On an Apple M4 Max: 3,416 B and 148 us per frame without text versus 40 B (the context object) and 136 us with the cache; with text, 43,400 B / 527 us versus 39,720 B / 496 us.
- `PropertyAccessBenchmarks` (#17): reading a column for 10 000 rows through `KumikoSource.GetCellValue`, and sorting by it, with generated accessors (`[KumikoGridItem]`), `KumikoColumn.ValueAccessor`, the reflection fallback, and the compiled-expression lookup used before #17.
- `RenderBenchmarks` (#68): one `KumikoRenderer.Render` frame of a 30-row × 10-column visible grid into a no-op `IDrawingContext`, bare (`Plain`) and with sorting, filtering, grouping, summaries, a frozen column and the drag handle (`Featured`). After the warm-up frames each measures the steady state, which allocates 0 bytes. Run just these with `--filter '*Render*' --job short`.
