# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) projects. They are not part of `KumikoUI.sln` and do not run in CI.

```bash
dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*'
```

- `PropertyAccessBenchmarks` (#17): reading a column for 10 000 rows through `KumikoSource.GetCellValue`, and sorting by it, with generated accessors (`[KumikoGridItem]`), `KumikoColumn.ValueAccessor`, the reflection fallback, and the compiled-expression lookup used before #17.
- `RenderBenchmarks` (#68): one `KumikoRenderer.Render` frame of a 30-row × 10-column visible grid into a no-op `IDrawingContext`, bare (`Plain`) and with sorting, filtering, grouping, summaries, a frozen column and the drag handle (`Featured`). After the warm-up frames each measures the steady state, which allocates 0 bytes. Run just these with `--filter '*Render*' --job short`.
