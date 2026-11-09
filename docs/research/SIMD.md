# SIMD in KumikoUI: research notes

Can KumikoUI get real performance from .NET SIMD: `System.Numerics.Vector<T>`, `Vector128/256/512<T>`, the hardware intrinsics, and the SIMD the BCL already does internally?

- **Date:** 2026-11-09, at commit `df4a635` (merged to `main` in #151).
- **Scope:** `KumikoUI.Core` and `KumikoUI.SkiaSharp` as they target `net10.0`, on every MAUI and Uno head.
- **Labels:** **(inferred)** marks a conclusion I drew rather than read in a source. **(unmeasured)** marks a number or claim nobody has benchmarked. Every other statement cites a source file or a URL listed under [References](#references).

## Summary and verdict

**Mostly no.** Hand-written SIMD (`Vector128<T>`, `Vector<T>`, `AdvSimd`/`Avx2`/`PackedSimd`) has no target in KumikoUI today that is worth the code. Three things keep it out:

- **The work is boxed.** Cell values travel as `object?` through `Func<object, object?>` accessors, and aggregates collect a `List<object?>`. In KumikoUI's current data shape, a 100,000-row `Sum` costs 2.32 ms. The same sum over a `double[]` with `TensorPrimitives` costs 0.026 ms. Removing the boxing gets a 24x speedup with no SIMD. SIMD on top of that saves a further ~36 µs.
- **The work is string- and culture-bound.** Filtering and the filter popup search with `CompareInfo.IndexOf(..., IgnoreCase)`. For `en*` cultures that call runs a scalar ASCII loop and hands anything non-ASCII to ICU. It never reaches the vectorized ordinal search. A culture-aware search over 10,000 strings costs 2.39 ms; `OrdinalIgnoreCase` costs 0.029 ms. The fix is a choice of comparison semantics, not SIMD.
- **A frame's time is in Skia.** On an Apple M4 Max, the managed engine costs 12.6 µs of a 934 µs CPU-raster 1080p frame (1.4%). Skia rasterizes with its own SIMD (NEON/SSE/AVX/WASM) and measures text from its glyph cache. Making the managed part 4x faster would not show up on a frame.

**What helps instead**, in order:

1. Remove boxing and allocation from the data pipeline: typed column readers, single-pass aggregates, and comparisons without boxing. Today a `Sum` + `Max` summary turns a 0.39 ms rebuild of 100,000 rows into 5.6 ms with 6.4 MB of garbage.
2. Decide the text-matching semantics for filter and search. Use ordinal ignore-case, or cache a folded key per value. Today a "contains" filter turns the same rebuild into 24.6 ms.
3. Move rebuilds off the UI thread (#69).
4. Cache Skia objects and text measurements across frames (#23, SKIA-03).
5. Make the render loop allocation-free (#68).

Once the data is typed and contiguous, `TensorPrimitives.Sum/Min/Max` is a cheap, portable, AOT-safe way to get the last ~2x on numeric summaries. Use it then, not before.

## Machine and method

- **Machine:** Apple M4 Max, 16 cores, 64 GB, macOS 27.2 (26B123).
- **Runtime:** .NET SDK 10.0.403, runtime 10.0.14, Arm64 RyuJIT. BenchmarkDotNet 0.15.8, `--job short` (3 iterations; the error columns are wide).
- **On this machine:** `Vector.IsHardwareAccelerated=True`, `Vector<byte>.Count=16`, `Vector128=True`, `Vector256=False`, `Vector512=False`. Printed by the scratch harness.
- **Repo benchmark:** `dotnet run -c Release --project bench/KumikoUI.Benchmarks -- --filter '*' --job short`.
- **Scratch benchmarks:** a separate project outside the repo references `KumikoUI.Core` and `KumikoUI.SkiaSharp` (SkiaSharp 4.153.1). It is not committed. The code under test is KumikoUI's own `KumikoRenderer.Render`, `KumikoSource.Refresh` and `KumikoComparer`, plus BCL baselines. Each number below names the benchmark that produced it.
- **Coverage:** these numbers are from one desktop CoreCLR machine. Mobile, WebAssembly and Mono numbers are **(unmeasured)** unless the [runtime comparison](#runtime-comparison-on-the-same-machine) says otherwise.

## 1. Platform reality

KumikoUI targets `net10.0`. On .NET 10, MAUI uses Mono on Android, iOS and Mac Catalyst and CoreCLR on Windows ([MAUI runtimes](#ref-maui-runtimes), ".NET 10" table). Uno's mobile heads use the same .NET for Android/iOS SDKs, so they get the same runtimes **(inferred)**. In .NET 11, MAUI moves to CoreCLR on all mobile targets and Mono is no longer supported ([MAUI runtimes](#ref-maui-runtimes); [.NET blog, May 2026](#ref-maui-coreclr)). That matters for KumikoUI's next TFM bump.

| Target | Runtime and code generation (.NET 10) | `Vector128` accelerated? | Wider vectors / `Vector<T>` | Fallback and notes |
|---|---|---|---|---|
| Windows (MAUI, Uno WinAppSDK), Uno desktop on x64 | CoreCLR JIT plus ReadyToRun ([MAUI runtimes](#ref-maui-runtimes)) | Yes: `Vector128` "is accelerated on the broadest set of hardware" ([SIMD doc](#ref-simd), best practices). **Not measured on x64 here** | `Vector256` with AVX2, `Vector512` with AVX-512; `Vector<T>` can stay 256-bit even when `Vector512` is accelerated ([SIMD doc](#ref-simd), knob tables) | Hot R2R methods are re-JITted at tier 1 for the actual CPU **(inferred from tiered compilation, not read in a cited source)**; on x64, `Vector<T>` code has startup and JIT concerns unless the R2R baseline includes AVX2 ([BOTR vectors](#ref-botr-vectors) lines 41-61) |
| Uno desktop on Arm64 (macOS, Linux, Windows on Arm) | CoreCLR JIT | Yes (measured `True` on M4) | `Vector256`/`Vector512` not accelerated: "only x86/x64 offers widths above 128 today" ([SIMD doc](#ref-simd)); "arm64 hardware does not support `Vector256`" ([vectorization guidelines](#ref-vec-guidelines) line 178). `Vector<T>` = 16 bytes (measured) | Code written for `Vector256` takes the software path here, so start at `Vector128` |
| Android (MAUI, Uno), .NET 10 | Mono: JIT plus interpreter in Debug; Mono AOT in Release ([MAUI runtimes](#ref-maui-runtimes)) | Yes on arm64: Mono's `Vector128.IsHardwareAccelerated` intrinsic emits the constant 1, and mini JIT/AOT without LLVM handles 128-bit vectors on arm64 ([Mono simd-intrinsics.c](#ref-mono-simd) lines 2020-2069) | Mono rejects 256/512-bit intrinsics (`vector_size == 256 \|\| 512 → NULL`, same lines) | Some ops are not expanded without LLVM (for example 64-bit integer element ops, lines 1908-1911) and fall back to the managed software implementation. On x64 emulators, mini needs SSE4.1 (lines 2043-2052) |
| iOS / Mac Catalyst (MAUI, Uno), .NET 10 | Mono full AOT in Release; AOT plus interpreter in arm64 Debug ([MAUI runtimes](#ref-maui-runtimes)) | Yes: same Mono code generator as Android **(inferred: the arm64 path in simd-intrinsics.c is not OS-specific)**. The Mono interpreter also treats `Vector128.IsHardwareAccelerated` as true and executes `Vector128` ops as interpreter SIMD opcodes ([Mono interp transform-simd.c](#ref-mono-interp) lines 515-536, 1325-1345) | As Android | Interpreted SIMD still runs through the interpreter, so it is far slower than AOT code **(inferred)** |
| iOS / Mac Catalyst with `PublishAot=true` | NativeAOT, opt-in ([MAUI NativeAOT](#ref-maui-naot)) | Yes on arm64 (AdvSimd is the arm64 baseline) **(inferred)** | `Vector<T>` size is fixed at compile time: 16 or 32 bytes by targeted ISA, `IlcMaxVectorTBitWidth` to change ([NativeAOT optimizing](#ref-naot-opt) lines 8-9) | `DOTNET_Enable*` knobs don't affect AOT code ([SIMD doc](#ref-simd)) |
| Native AOT generally (desktop x64) | ILC | Yes | "By default, the compiler targets the minimum instruction set supported by the target OS and architecture"; `IlcInstructionSet` raises it, and the binary then requires that hardware ([NativeAOT optimizing](#ref-naot-opt) line 8) | Code above the baseline is guarded by runtime checks or not used **(inferred from [BOTR vectors](#ref-botr-vectors) lines 35-61, which describe the AOT model for crossgen2)** |
| WebAssembly (Uno) | Mono interpreter ("Jiterpreter") by default; Mono AOT when `RunAOTCompilation=true` ([Blazor build tools](#ref-blazor-aot)) | Yes by default. Browser targets set `WasmEnableSIMD` to the value of `WasmEnableExceptionHandling`, which defaults to `true` ([BrowserWasmApp.targets](#ref-browser-targets) lines 3-5). The runtime doc says SIMD "is currently enabled by default" ([wasm features](#ref-wasm-features) line 37). Uno's bootstrapper enables it by default since .NET 8 ([Uno SIMD](#ref-uno-simd)) | 128-bit only (`PackedSimd`) | With SIMD off, the trimmer stubs `Vector.IsHardwareAccelerated`, `Vector128.IsHardwareAccelerated` and `PackedSimd.IsSupported` to `false` ([NoWasmIntrinsics.xml](#ref-wasm-nosimd)). Old browsers need SIMD disabled ([Blazor runtime perf](#ref-blazor-perf)) |
| .NET 11 Android / iOS (next TFM) | CoreCLR: composite partial R2R plus JIT on Android; composite full R2R plus CoreCLR interpreter on iOS and Mac Catalyst ([MAUI runtimes](#ref-maui-runtimes), ".NET 11" table) | R2R and JIT code: yes on arm64 **(inferred)** | 128-bit on arm64 | I found no `Vector128` expansion in the CoreCLR interpreter (`src/coreclr/interpreter/compiler.cpp` on `release/11.0`). Code that falls through to the interpreter likely runs the software path. **(Unverified.)** |

**`IsHardwareAccelerated` and the fallback.** "SIMD-accelerated types work even on hardware or JIT configurations that don't support SIMD, because they fall back to non-accelerated software implementations." `IsHardwareAccelerated` and `Count` are JIT-time constants, so untaken branches cost nothing; don't cache them ([SIMD doc](#ref-simd), "Check for hardware acceleration"). Acceleration of a width does not guarantee every operation on it is accelerated (same section).

The software fallback is a per-element managed implementation behind the vector API. That makes it slower than a plain scalar loop: it pays the vector abstraction with none of the parallelism. Every hand-vectorized path therefore needs an explicit scalar branch, as the doc's `Sum` example shows **(inferred; the doc says only that a fallback exists and shows the scalar branch)**.

## 2. What the BCL already vectorizes for free

| API | Vectorized? | Source | KumikoUI uses it? |
|---|---|---|---|
| `Span`/`MemoryExtensions.IndexOf`, `SequenceEqual`, `IndexOfAny*` on primitive element types | Yes, `Vector128` and up, behind `IsHardwareAccelerated` | [SpanHelpers.T.cs](#ref-spanhelpers-t) lines 1321, 1472-1501, 1707; [SpanHelpers.Byte.cs](#ref-spanhelpers-byte) lines 31, 267, 460, 706 | Only indirectly (string internals). KumikoUI holds no large primitive spans to search |
| `string.Contains/IndexOf(..., StringComparison.Ordinal)` and `OrdinalIgnoreCase` | Yes. `Ordinal.IndexOfOrdinalIgnoreCase` has a `Vector128` path that compares the first and last chars of the needle at once ([Ordinal.cs](#ref-ordinal) lines 329-546) | Measured 29-32 µs for 10,000 strings | Only `StartsWith(..., OrdinalIgnoreCase)` in INPC path matching (`KumikoSource.cs:530`) |
| `CompareInfo.IndexOf/IsPrefix/IsSuffix` with `CompareOptions.IgnoreCase` (culture) | Mostly no. For `""`, `en` and `en-*` sort names, ICU builds take a managed ASCII fast path. It is a nested scalar loop: vectorized `ContainsAnyExcept(SearchValues)` pre-checks, then a char-by-char compare that jumps to ICU on the first non-ASCII or special char ([CompareInfo.Icu.cs](#ref-compareinfo-icu) lines 14-48, 64-75, 104-200). Other cultures go to ICU directly | Measured: all-ASCII data 187 µs vs 31 µs ordinal (6x); mixed data with accented names 2,394 µs vs 29 µs (80x). Invariant culture is no faster (2,356 µs) | **Yes, everywhere text is filtered.** `KumikoComparer.TextContains/StartsWith/EndsWith` (`KumikoComparer.cs:84-90`) back text filters (`FilterCondition.cs:41-56`) and the filter-popup search (`DrawnFilterPopup.cs:248-254`) |
| `CompareInfo.Compare` (culture, IgnoreCase) | No managed fast path for non-ordinal options; goes to `CompareStringCore` → ICU, NLS or Hybrid ([CompareInfo.cs](#ref-compareinfo) lines 414-430, 488-492) | Measured: sorting 10,000 strings 3.93 ms culture vs 2.07 ms `OrdinalIgnoreCase` | **Yes:** text sort, group ordering, unique-value sort (`KumikoComparer.cs:78`) |
| `CompareInfo.GetStringComparer(options)` hash and equality | Culture hash codes come from ICU sort keys **(inferred)** | (unmeasured) | `HashSet`/`Dictionary` keys in `GetUniqueColumnValues` (`KumikoSource.cs:820`), group map (`:1394`), prepared filter sets (`:1145`) |
| `Enumerable.Sum` | Only `int` and `long` spans, with checked overflow; `long` only when `Vector<T>` holds more than two elements, so **only `int` on arm64**. `double`/`float`/`decimal` sum is a scalar loop ([Linq Sum.cs](#ref-linq-sum) lines 47-77) | Measured `double[].Sum()` 62.3 µs, the same as a hand loop (61.9 µs) | No; KumikoUI sums boxed values |
| `Enumerable.Min/Max` | Integer spans: `Vector128/256/512` ([Linq MaxMin.cs](#ref-linq-maxmin) lines 22-75). Floating point: scalar, NaN-aware ([Linq Max.cs](#ref-linq-max) lines 95-130) | Measured `int[].Max()` 12.7 µs vs 25.7 µs scalar | No |
| `Enumerable.Average` | `int` spans widen to `Vector<long>` ([Linq Average.cs](#ref-linq-average) lines 18-43) | (unmeasured) | No |
| `TensorPrimitives.Sum/Min/Max/...` (System.Numerics.Tensors) | Yes, every type `Vector<T>` supports. "Exact results may differ between different operating systems or architectures" ([TensorPrimitives.Sum](#ref-tp-sum)); "often delivers most of the benefit of hand-written SIMD" ([SIMD doc](#ref-simd)) | Measured `Sum<double>` 25.6 µs vs 61.9 µs scalar (2.4x); `Max<double>` 26.0 µs | No. It would need a contiguous typed buffer that KumikoUI does not have |

**Which KumikoUI paths benefit today?** None in a way that matters. The paths that could switch to a span-based API without hand-written SIMD:

- **Text filters and popup search.** Switch to `StringComparison.OrdinalIgnoreCase`, or `SearchValues<string>` when there are many needles. This is 6-80x faster, measured, but it is a semantic change. Ordinal ignore-case folds case with invariant simple case mapping and does no linguistic matching. Precomposed `é` vs `e` + U+0301, ignorable characters, and culture-specific casing (Turkish `İ/i`) then behave differently **(inferred)**. Make it an explicit option (`StringCompareOptions = CompareOptions.OrdinalIgnoreCase` already exists, `KumikoSource.cs:132-141`) or fold once and cache, rather than silently changing the default.
- **Numeric summaries.** Gather a typed column once into a pooled `double[]`/`long[]`, then `TensorPrimitives.Sum/Min/Max`. This only pays after the boxing is gone (§4).

## 3. KumikoUI hot paths

Each path is judged for data-parallel, numeric, contiguous work.

| Path | SIMD verdict | Why | Source / evidence |
|---|---|---|---|
| `KumikoRenderer` cell loop (`DrawRows`) | **No** | ~300 visible cells. Each does an `object?` read through a delegate, a format to `string`, a virtual `ICellRenderer.Render`, a `new KumikoPaint`, and a P/Invoke-backed Skia draw. Nothing is a contiguous numeric array. Whole managed frame: 12.6 µs and 22.3 KB allocated | `KumikoRenderer.cs:631-660`; `TextCellRenderer.cs:16`; `FrameBenchmarks.Frame_ManagedOnly_NoOpContext` |
| `PaintCache.Update` | **No** | Allocates ~27 `KumikoPaint` objects per frame. The problem is allocation (#68), not arithmetic | `PaintCache.cs:112` and on; release-readiness CORE-10 |
| `KumikoLayoutEngine` column offsets and visible columns | **No** | Linear walks over tens of columns, each reading `col.Width` from a class (not contiguous). Microseconds at most. A prefix-sum `float[]` plus binary search would be the algorithmic fix if column counts ever reach thousands **(inferred)** | `KumikoLayoutEngine.cs:16`, `:163-187`, `:303` |
| `GetVisibleRowRangeWithOffset` | **No** | O(1) arithmetic: fixed row height, two divisions | `KumikoLayoutEngine.cs:220-241` |
| `CalculateAutoFitWidth` | **No** | Formats and measures every row through Skia. Bound by text measurement; the fix is a sample, a cache (#23) or a background thread | `KumikoLayoutEngine.cs:319-358`; MAUI-10 in release-readiness 03 |
| `KumikoHitTester` | **No** | Row is O(1) arithmetic; column is O(columns); runs once per pointer event | `KumikoHitTester.cs:135`, `:142`, `:240-280` |
| `KumikoSource.RebuildCore`: filter pass | **No SIMD.** Big win from text semantics | Per row: delegate read, boxing, `FormatCellValue` to `string`, then culture `IndexOf`. Measured 24.6 ms for a "contains" filter on 100,000 rows vs 0.39 ms with no filter; the difference is the culture search | `KumikoSource.cs:1155-1170`, `FilterCondition.cs:41-56`; `RebuildBenchmarks.Refresh_TextContainsFilter` |
| `RebuildCore`: sort | **No** | `List<int>.Sort` with a closure. Each comparison reads both values through `Func<object, object?>` (boxing), then `KumikoComparer.Compare` type-switches and compares numbers as `decimal`. Comparison sorts are branchy and pointer-chasing. The repo benchmark sorts 10,000 rows in 3.47 ms and allocates 9.1 MB: boxing on every comparison. Fix: read each key once into a typed array (Schwartzian transform), then sort keys | `KumikoSource.cs:1327-1349`, `KumikoComparer.cs:44-75`, `:101-153`; `PropertyAccessBenchmarks.Sort_Generated` |
| `RebuildCore`: group | **No** | Per row: format to display text, then a culture-hashed `Dictionary<string,int>`. String- and ICU-bound | `KumikoSource.cs:1375-1440` |
| `ComputeAggregate` (Sum/Average/Min/Max) | **Not yet. Later: `TensorPrimitives`, small win** | `CollectColumnValues` builds a `List<object?>` of boxed values (a LOH array at 100,000 rows), then `Convert.ToDouble` in a `try`, or `KumikoComparer.Compare` for Min/Max. Measured at 100,000 rows: this shape 2,320 µs and 3.2 MB with Gen2 GCs; a typed loop 96 µs; scalar `double[]` 62 µs; `TensorPrimitives.Sum` 26 µs. Max via the comparer over boxed values 592 µs vs 26 µs. In situ, a Sum + Max summary takes the rebuild from 0.39 ms to 5.65 ms and 6.4 MB | `KumikoSource.cs:1610-1713`; `AggregateBenchmarks`; `RebuildBenchmarks.Refresh_SumAndMaxSummary` |
| `GetUniqueColumnValues` | **No** | Per row: filter pass, format, culture-hashed `HashSet<string>` add; then a culture sort. String- and ICU-bound | `KumikoSource.cs:817-840` |
| `KumikoComparer` | **No** | Scalar type dispatch over `object`. Ints compare via `decimal` (`NumericValue(long)` → `decimal`); text via ICU. A typed comparer per column type avoids the dispatch and the boxing | `KumikoComparer.cs:44-75`, `:101-153` |
| `DrawnTextBox` prefix widths | **No** | Lazily measures `Substring(0, b[k])` through Skia, one call per grapheme boundary (allocates a substring and a `KumikoPaint` each), plus a binary search in `HitIndex`. Bound by Skia measurement. `SKFont.GetGlyphWidths` once per text, then a prefix sum, would cut the P/Invokes, but per-glyph advances ignore kerning **(inferred)**. Text boxes hold short strings | `DrawnTextBox.cs:256-279`, `:496-520`; `TextElements.cs:12-23` |
| `DrawnFilterPopup` search | **No SIMD. Ordinal or a cached key: 6-80x** | `Enumerable.Where` over up to thousands of display strings with culture `TextContains`; it already narrows incrementally when the query extends the last one. Measured 2.39 ms per keystroke for 10,000 mixed strings on M4 (expect several times that on a low-end phone **(inferred, unmeasured)**). `OrdinalIgnoreCase`: 0.029 ms | `DrawnFilterPopup.cs:239-260`; `TextSearchBenchmarks` |
| `InertialScroller` | **No** | Two floats per frame (`MathF.Pow`, clamp, smoothing). Nothing to vectorize | `InertialScroller.cs:28-109` |
| `KumikoColor` "blending" | **N/A** | No blending code exists in Core: `KumikoColor` is a 4-byte struct with `WithAlpha` and `ToUint32`. Colors go straight to Skia, which does all compositing | `KumikoColor.cs:9-91`; `grep Blend\|Lerp` finds nothing in `src/` |
| `SkiaDrawingContext` | **No (Skia already SIMD)** | Per frame: a new context with fresh `SKPaint`/`SKFont` caches (SKIA-03). Per text: `MeasureText`, and on overflow a binary search of `text[..mid]` substrings, each measured. `DrawText(string)` builds an `SKTextBlob` per call. The cost is P/Invoke, allocation and repeated measuring, not managed arithmetic | `SkiaDrawingContext.cs:15-25`, `:101-151`, `:307-327`; SkiaSharp `SKCanvas.cs:784-800` ([SkiaSharp SKCanvas](#ref-skiasharp-canvas)) |

## 4. Where the time actually goes

### Measured: one frame

1920×1080 frame, 10 columns, 100,000 rows, about 29 visible rows (default `RowHeight` 36), so about 300 cells. Scratch `FrameBenchmarks`, CoreCLR Arm64, M4 Max:

| Benchmark | Mean | Allocated | Share |
|---|---:|---:|---|
| `KumikoRenderer.Render` into a no-op `IDrawingContext` (managed engine only) | 12.6 µs | 22.3 KB | 1.4% |
| Same, but every text draw calls Skia `MeasureText` (no rasterization) | 122.5 µs | 23.7 KB | Text measurement ≈ 110 µs, ~12% |
| Full frame: `SKSurface` CPU raster, per-frame `SkiaDrawingContext` (what both hosts do) | 933.6 µs | 70.4 KB | Skia rasterization and glyph blitting ≈ 800 µs, ~86% **(by subtraction)** |

**What the frame numbers mean:**

- A 1080p frame on this machine is about 0.93 ms. Under 2% of it is KumikoUI's managed logic, ~12% is Skia measuring text, and the rest is Skia drawing.
- Even an infinitely fast managed engine saves 12.6 µs per frame. The allocation (22-70 KB per frame, so Gen0 GCs during a fling) matters more than its arithmetic. That is #68's target.
- GPU backends, high-DPI mobile surfaces and Mono AOT will change the proportions. The direction should hold, because the managed share is so small **(inferred, unmeasured on devices)**.

**What Skia already does natively:**

- Skia's CPU raster pipeline is compiled per ISA: `SKRP_CPU_NEON`, `SKRP_CPU_WASM` (`__wasm_simd128__`), AVX2/AVX/SSE4.1/SSE2, LASX/LSX, with a scalar fallback only for non-clang/GCC builds ([SkRasterPipeline_opts.h](#ref-skia-rp) lines 78-104).
- Its portable vector type `skvx::Vec` uses SIMD unless `SKVX_DISABLE_SIMD` is defined ([SkVx.h](#ref-skia-vx) lines 34-45).
- `SkFont::measureText` maps text to glyph IDs, then sums advances from cached glyph metrics (`SkStrikeSpec`/`SkBulkGlyphMetrics`) ([SkFont.cpp](#ref-skia-font) lines 187-232; [SkStrike.h](#ref-skia-strike) line 44).
- KumikoUI does no shaping (HarfBuzz/`SKShaper`): text is drawn as one `SKTextBlob` per string with a single font ([SkiaSharp SKCanvas](#ref-skiasharp-canvas) line 800; SKIA-02 in release-readiness 03).
- KumikoUI cannot add SIMD to any of this from C#. It can only call it less: cache measurements across frames (#23), keep `SKPaint`/`SKFont`/`SKTypeface` alive across frames (SKIA-03), and avoid re-measuring substrings for ellipsis.
- These Skia files are from the `main` branch. SkiaSharp 4.153.1 bundles a specific Skia milestone that I did not pin. The ISA selection has been stable for years **(inferred)**.

### Measured: off-frame data work, 100,000 rows

Scratch `RebuildBenchmarks`, `KumikoSource.Refresh()`:

| Case | Mean | Allocated |
|---|---:|---:|
| No filter, no sort, no summary | 0.39 ms | 224 B |
| Table summary: `Sum(Amount)` and `Max(Quantity)` | 5.65 ms | 6.4 MB (Gen2 collections) |
| Text filter: `Name` contains "customer 12" (culture, IgnoreCase) | 24.56 ms | 344 B |

The repo benchmark (`PropertyAccessBenchmarks`, 10,000 rows) reads a boxed `int` column in 40-61 µs (234 KB of boxes). It sorts by it in 3.47 ms, allocating 9.1 MB.

**What the off-frame numbers mean:** these are the operations that can drop frames, because they run on the UI thread today (#69). None of them is limited by arithmetic throughput. They are limited by boxing, allocation, delegate calls and ICU.

### Runtime comparison on the same machine

I tried `--runtimes net10.0 nativeaot10.0 mono10.0` on the same machine. Only CoreCLR produced numbers:

- **NativeAOT:** the ILC link failed in this Homebrew-installed SDK. First `ld: library 'ssl' not found`; with OpenSSL on `LIBRARY_PATH`, `ld: library 'brotlienc' not found`. This is an environment problem, not a KumikoUI one.
- **Mono:** restore failed. `NU1102: Unable to find package Microsoft.NETCore.App.Runtime.Mono.osx-arm64 with version (= 10.0.14)`.
- **CoreCLR repeat runs:** these reproduced the CoreCLR numbers above within noise. For example, aggregates: `TensorPrimitives.Sum` 26.6 µs, scalar 64-66 µs, comparer `Max` 587-593 µs. Culture search: 2,347 µs vs 29.7 µs ordinal. Frame: 12.4 µs.

So the NativeAOT and Mono columns are **(unmeasured)**. A CI runner or the official SDK install should fix NativeAOT; Mono needs a runtime pack that NuGet did not have for `osx-arm64` 10.0.14 **(inferred)**.

## 5. Recommendation

### Worth doing, in order

| # | Change | Expected effect | Where |
|---|---|---|---|
| 1 | **Typed, single-pass aggregates.** Compute Sum/Average/Min/Max while reading, with no `List<object?>`. Use a typed reader (`Func<object, double>` or a generated typed accessor) when the column's CLR type is known, and keep the boxed path for `object` columns and custom aggregates | ~20-25x on numeric summaries (2,320 → ~96 µs per 100,000 rows); removes 3.2 MB of LOH/Gen2 garbage per summary column per rebuild. Measured shapes | `KumikoSource.cs:1610-1713`; feeds #69 (incremental aggregates) |
| 2 | **Decide text-match semantics, then make them cheap.** Either an `OrdinalIgnoreCase` mode for filter and search (6-80x, measured), or keep culture rules and cache a folded or sort key per value once per rebuild instead of per keystroke | Filter popup on 10,000 values: 2.4 ms → 0.03 ms per keystroke. A 100,000-row "contains" filter: ~25 ms → ~1 ms **(inferred from the 10,000-string ratio)** | `KumikoComparer.cs:78-90`, `DrawnFilterPopup.cs:239-260`, `FilterCondition.cs:41-56` |
| 3 | **Sort on extracted keys.** Read each sort key once into a typed array (`double[]`, `long[]`, `string[]`, or `SortKey`/folded-string arrays), then sort indices by keys | Removes the ~9 MB of boxing per 10,000-row sort and most of the delegate calls; expect several-fold **(inferred, unmeasured)** | `KumikoSource.cs:1327-1349` |
| 4 | **#69 off-UI-thread rebuilds** with coalescing | Moves the 5-25 ms rebuilds above off the frame entirely | issue-plan I4 |
| 5 | **#23 text measurement cache plus SKIA-03 long-lived Skia objects** | Attacks the ~12% text-measurement share and the per-frame native object churn | `SkiaDrawingContext.cs`; issue-plan I2/I3 |
| 6 | **#68 allocation-free render loop** | 22-70 KB per frame → ~0; fewer Gen0 GCs during flings | `KumikoRenderer.cs`, `PaintCache.cs`, cell renderers |
| 7 | **Then, optionally, `TensorPrimitives`** for Sum/Min/Max over a typed, contiguous column buffer (pooled) | ~2.4x over a scalar loop: 62 → 26 µs per 100,000 doubles, measured. Worth it only if columns are stored or gathered contiguously; gathering from row objects costs about as much as the scalar sum itself (96 µs, measured) | after 1 |

### Not worth it

- Hand-written `Vector128<T>`/`Vector<T>`/intrinsics anywhere in Core: no contiguous numeric hot loop exists.
- SIMD for layout, hit testing, inertial scrolling or colors: tens of elements or scalars.
- Anything inside Skia: already NEON/SSE/AVX/WASM-SIMD, and out of reach from C#.
- Replacing `CompareInfo` with a hand-vectorized culture-aware search: the BCL deliberately sends non-ASCII to ICU, and reimplementing linguistic matching is out of scope.

### Portability and AOT risks if SIMD is added later

- **Prefer `TensorPrimitives` or BCL span APIs to raw vectors.** They are AOT- and trim-safe and already carry the width dispatch and scalar fallbacks ([SIMD doc](#ref-simd), best practices).
- **On arm64 (every phone, Apple Silicon and Windows on Arm), only 128-bit is accelerated.** A `Vector256`-only path runs the software fallback there.
- **`Vector<T>` size is fixed at compile time under Native AOT and R2R** ([NativeAOT optimizing](#ref-naot-opt) line 9; [BOTR vectors](#ref-botr-vectors) line 48). Don't make algorithm correctness depend on its width.
- **WebAssembly with `WasmEnableSIMD=false`** trims vector paths to their scalar branches ([NoWasmIntrinsics.xml](#ref-wasm-nosimd)), so the scalar branch must be correct and tested.
- **Test every path.** Use `DOTNET_EnableHWIntrinsic=0` and `DOTNET_EnableAVX2=0` to force the fallbacks in JIT tests. These knobs don't affect R2R or Native AOT code ([SIMD doc](#ref-simd), "Test all code paths").
- **Floating-point results can differ by platform.** `TensorPrimitives.Sum` may give slightly different results across platforms, and its order of operations differs from a sequential loop ([TensorPrimitives.Sum](#ref-tp-sum)). Summary values could change in the last bits between platforms and from today's sequential sum, which matters for golden tests.

## 6. How to verify with BenchmarkDotNet

Extend `bench/KumikoUI.Benchmarks` (#70) with the cases this note measured in a scratch project: frame render (no-op context and Skia raster), `Refresh` with a summary, with a text filter and with a sort, aggregate shapes, and text search. Then:

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[SimpleJob(RuntimeMoniker.Mono10_0)]          // closest desktop stand-in for Android/iOS Mono (.NET 10)
[DisassemblyDiagnoser(maxDepth: 2, exportGithubMarkdown: true)]
public class AggregateBenchmarks { /* ... */ }
```

- **Jobs:** `RuntimeMoniker.Net10_0`, `NativeAot10_0`, `Mono10_0`, `MonoAOTLLVMNet10_0` and `WasmNet10_0` exist in BenchmarkDotNet 0.15.8 (checked in `BenchmarkDotNet.Annotations.dll`). From the CLI: `--runtimes net10.0 nativeaot10.0 mono10.0`.
- **Forced fallback paths:** to compare scalar, `Vector128` and `Vector256` on one machine, add jobs with `DOTNET_EnableHWIntrinsic=0` and `DOTNET_EnableAVX2=0`, as the runtime's guide does. Its `DisassemblyDiagnoser` config shows whether a loop actually vectorized ([vectorization guidelines](#ref-vec-guidelines) lines 222-250).
- **Phones:** desktop jobs cannot represent a phone. Publish numbers per device from the frame-time overlay planned in #70, and say which runtime (Mono AOT, CoreCLR R2R, NativeAOT) each number came from.
- **Benchmark design:** benchmark at realistic sizes (300 cells per frame; 1,000-100,000 rows per rebuild; up to 10,000 filter values). Vectorized code can lose on small inputs ([SIMD doc](#ref-simd), "Benchmark to confirm the win").

## What I could not verify

- Mono numbers on device (Android Mono AOT, iOS full AOT, the interpreter), WebAssembly numbers, and even desktop NativeAOT and Mono runs (both failed to build here; see the runtime comparison): **unmeasured**.
- Whether `Vector128` code that falls through to the .NET 11 CoreCLR interpreter on iOS runs accelerated. I found no SIMD expansion in `interpreter/compiler.cpp`.
- The exact Skia milestone inside SkiaSharp 4.153.1. The Skia citations are from `main`.
- Whether culture `GetStringComparer(IgnoreCase).GetHashCode` costs as much as `IndexOf`. I did not benchmark it separately.
- GPU-backed frame proportions (MAUI `SKGLView`, Uno GPU paths): **unmeasured**.

## References

- <a id="ref-simd"></a>Use SIMD and hardware intrinsics in .NET (Microsoft Learn, ms.date 2026-07-18): https://learn.microsoft.com/en-us/dotnet/standard/simd
- <a id="ref-vec-guidelines"></a>dotnet/runtime vectorization guidelines (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/docs/coding-guidelines/vectorization-guidelines.md
- <a id="ref-botr-vectors"></a>dotnet/runtime BOTR, Vectors and intrinsics (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/docs/design/coreclr/botr/vectors-and-intrinsics.md
- <a id="ref-naot-opt"></a>Native AOT optimizing options (dotnet/runtime, `release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/coreclr/nativeaot/docs/optimizing.md
- Optimizing AOT deployments (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing
- <a id="ref-mono-simd"></a>Mono JIT/AOT SIMD intrinsics, `simd-intrinsics.c` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/mono/mono/mini/simd-intrinsics.c
- <a id="ref-mono-interp"></a>Mono interpreter SIMD, `transform-simd.c` and `interp-simd.c` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/mono/mono/mini/interp/transform-simd.c and https://github.com/dotnet/runtime/blob/release/10.0/src/mono/mono/mini/interp/interp-simd.c
- CoreCLR interpreter compiler (`release/11.0`): https://github.com/dotnet/runtime/blob/release/11.0/src/coreclr/interpreter/compiler.cpp
- <a id="ref-wasm-features"></a>Configuring and hosting .NET WebAssembly applications, SIMD (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/mono/wasm/features.md
- <a id="ref-browser-targets"></a>`BrowserWasmApp.targets` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/mono/browser/build/BrowserWasmApp.targets
- <a id="ref-wasm-nosimd"></a>`ILLink.Substitutions.NoWasmIntrinsics.xml` and `ILLink.Substitutions.WasmIntrinsics.xml` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/mono/browser/build/ILLink.Substitutions.NoWasmIntrinsics.xml and https://github.com/dotnet/runtime/blob/release/10.0/src/mono/browser/build/ILLink.Substitutions.WasmIntrinsics.xml
- `WasmApp.Common.targets` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/mono/wasm/build/WasmApp.Common.targets
- <a id="ref-blazor-aot"></a>Blazor WebAssembly build tools and AOT compilation (Microsoft Learn, ASP.NET Core 10.0): https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0
- <a id="ref-blazor-perf"></a>Blazor WebAssembly runtime performance, SIMD (Microsoft Learn, ASP.NET Core 10.0): https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/webassembly-runtime-performance?view=aspnetcore-10.0
- <a id="ref-uno-simd"></a>Uno.Wasm.Bootstrap, Support for SIMD (Uno Platform docs): https://platform.uno/docs/articles/external/uno.wasm.bootstrap/doc/features-simd.html
- <a id="ref-maui-runtimes"></a>Runtimes and compilation in .NET MAUI (Microsoft Learn, ms.date 2026-10-08): https://learn.microsoft.com/en-us/dotnet/maui/deployment/runtimes-compilation
- <a id="ref-maui-naot"></a>Native AOT deployment on iOS and Mac Catalyst (Microsoft Learn): https://learn.microsoft.com/en-us/dotnet/maui/deployment/nativeaot?view=net-maui-10.0
- <a id="ref-maui-coreclr"></a>.NET MAUI moves to CoreCLR in .NET 11 (.NET Blog, David Ortinau, 2026-05-13): https://devblogs.microsoft.com/dotnet/dotnet-maui-moves-to-coreclr-in-dotnet-11/
- <a id="ref-compareinfo-icu"></a>`CompareInfo.Icu.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Private.CoreLib/src/System/Globalization/CompareInfo.Icu.cs
- <a id="ref-compareinfo"></a>`CompareInfo.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Private.CoreLib/src/System/Globalization/CompareInfo.cs
- <a id="ref-ordinal"></a>`Ordinal.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Private.CoreLib/src/System/Globalization/Ordinal.cs
- <a id="ref-spanhelpers-t"></a>`SpanHelpers.T.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Private.CoreLib/src/System/SpanHelpers.T.cs
- <a id="ref-spanhelpers-byte"></a>`SpanHelpers.Byte.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Private.CoreLib/src/System/SpanHelpers.Byte.cs
- <a id="ref-linq-sum"></a>System.Linq `Sum.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Linq/src/System/Linq/Sum.cs
- <a id="ref-linq-max"></a>System.Linq `Max.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Linq/src/System/Linq/Max.cs
- <a id="ref-linq-maxmin"></a>System.Linq `MaxMin.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Linq/src/System/Linq/MaxMin.cs
- <a id="ref-linq-average"></a>System.Linq `Average.cs` (`release/10.0`): https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Linq/src/System/Linq/Average.cs
- <a id="ref-tp-sum"></a>`TensorPrimitives.Sum` (Microsoft Learn API reference): https://learn.microsoft.com/en-us/dotnet/api/system.numerics.tensors.tensorprimitives.sum
- <a id="ref-skiasharp-canvas"></a>SkiaSharp `SKCanvas.cs` at `v4.153.1`: https://github.com/mono/SkiaSharp/blob/v4.153.1/binding/SkiaSharp/SKCanvas.cs
- SkiaSharp `SKFont.cs` at `v4.153.1` (`MeasureText` → `sk_font_measure_text_no_return`, lines 391, 423): https://github.com/mono/SkiaSharp/blob/v4.153.1/binding/SkiaSharp/SKFont.cs
- <a id="ref-skia-rp"></a>Skia `SkRasterPipeline_opts.h` (`main`): https://github.com/google/skia/blob/main/src/opts/SkRasterPipeline_opts.h
- <a id="ref-skia-vx"></a>Skia `SkVx.h` (`main`): https://github.com/google/skia/blob/main/src/core/SkVx.h
- <a id="ref-skia-font"></a>Skia `SkFont.cpp` (`main`): https://github.com/google/skia/blob/main/src/core/SkFont.cpp
- <a id="ref-skia-strike"></a>Skia `SkStrike.h` (`main`): https://github.com/google/skia/blob/main/src/core/SkStrike.h
- BenchmarkDotNet: https://github.com/dotnet/BenchmarkDotNet
- KumikoUI context: `docs/ROADMAP.md` (row I), `docs/release-readiness/issue-plan.md` (I1 #68, I2 #23, I4 #69, I6 #70), `docs/release-readiness/01-core-data-layout-render.md` (CORE-10, CORE-20, CORE-34), `docs/release-readiness/03-skiasharp-maui.md` (SKIA-02, SKIA-03, MAUI-10)
