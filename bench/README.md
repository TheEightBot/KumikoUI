# Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) projects. They are not part of `KumikoUI.sln` and do not run in CI.

```bash
dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*'
```

- `PropertyAccessBenchmarks` (#17): reading a column for 10 000 rows through `KumikoSource.GetCellValue`, and sorting by it, with generated accessors (`[KumikoGridItem]`), `KumikoColumn.ValueAccessor`, the reflection fallback, and the compiled-expression lookup used before #17.
