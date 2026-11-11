# Spec: HarfBuzz shaping for complex scripts on top of per-glyph fallback (#111)

| | |
|---|---|
| Issue | [#111](https://github.com/TheEightBot/KumikoUI/issues/111) (plan key X10, milestone 1.x – Fast follow-ons) |
| Status | Draft for review |
| Blocked by | [#74](https://github.com/TheEightBot/KumikoUI/issues/74) per-glyph font fallback (PR #216), which builds on [#23](https://github.com/TheEightBot/KumikoUI/issues/23) text measurement cache, ellipsis and wrapping (PR #199) |
| Related | [#80](https://github.com/TheEightBot/KumikoUI/issues/80) RTL layout mirroring (spec in progress), [#184](https://github.com/TheEightBot/KumikoUI/issues/184) / PR #200 Uno WebAssembly native relink, [#76](https://github.com/TheEightBot/KumikoUI/issues/76) / PR #196 AOT and trimming jobs, [#102](https://github.com/TheEightBot/KumikoUI/issues/102) variable row height |
| Findings | SKIA-02 follow-up ([03](../release-readiness/03-skiasharp-maui.md), decision 5), COMP-19 (RTL/bidi in `DrawnTextBox`, [02](../release-readiness/02-core-components-editing-input.md)), CORE-32 / MAUI-19 (RTL) |

## 1. Summary

After #74, `SkiaDrawingContext` draws every line run by run: `SkiaResourceCache.Segment` splits the line into
`FontRuns` by text element, each run uses the requested typeface or a fallback, and `DrawRuns` draws each run with
`SKCanvas.DrawText`. The glyphs come straight from each font's character map, so the text has the right glyphs but
is not shaped:

- **Arabic** is drawn as isolated letters, without joining or the lam-alef ligature.
- **Indic scripts** (Devanagari and the others) are drawn without reordering, conjuncts or mark positioning.
- **Thai** stacked marks collide.
- **Emoji ZWJ sequences and flags** are drawn as their component glyphs. A family shows as four people, and a flag
  shows as two regional-indicator letters.
- **Right-to-left text** (Hebrew and Arabic) is drawn in logical order from left to right, so it reads backwards.
  Nothing in the stack does bidi today.

This spec adds:

- **Shaping in an opt-in package.** A new `KumikoUI.SkiaSharp.HarfBuzz` package shapes complex-script runs with
  HarfBuzz through `HarfBuzzSharp`. It does not use `SkiaSharp.HarfBuzz`'s `SKShaper` (see §4). An app turns it on
  with one call. Without the package, nothing native is added.
- **Bidi in Core, always on.** A managed Unicode Bidirectional Algorithm (UAX #9) in `KumikoUI.Core` handles
  direction, because neither HarfBuzz nor `System.Globalization` does bidi. Hebrew becomes correct without the
  HarfBuzz package. Arabic gets the right order, but it is only joined when the package is installed.
- **One pipeline.** A line goes through itemize, then bidi, then script, then font fallback (#74), then shaping per
  run. The result is a cached `ShapedLine`: glyph ids, positions and clusters in visual order, plus one immutable
  `SKTextBlob` built on first draw. Steady-state frames draw cached blobs and allocate nothing. That is better than
  today, where `SKCanvas.DrawText(string)` allocates a blob on every call.
- **A fast path that keeps Latin unchanged.** Text with no right-to-left or complex-script code points (one
  vectorized scan on a cache miss) takes the existing #74 `FontRuns` path, so Latin grids keep the same pixels,
  allocations and timings.
- **Cluster-aware text layout.** Ellipsis cuts happen on cluster boundaries of the shaped text, and the ellipsis
  goes on the paragraph's end side. Wrapping uses UAX #14 line breaking. `DrawnTextBox` gets bidi- and
  cluster-aware caret, selection and hit testing.

## 2. Goals and non-goals

### Goals

1. With `KumikoUI.SkiaSharp.HarfBuzz` enabled, these render correctly in cells, headers, group headers, summaries,
   popups and editors: Arabic (joining, lam-alef, harakat), Hebrew (order, niqqud, mirrored brackets), Devanagari
   (reordering, conjuncts, reph), Thai (mark stacking), and emoji ZWJ, flag, keycap and skin-tone sequences (one
   glyph each when the emoji font has it).
2. Without the package, right-to-left text is still drawn in the correct visual order (bidi is in Core). Today it
   is drawn backwards.
3. Nothing changes for text that needs neither shaping nor bidi: same `FontRuns` path, same pixels, same
   `NativeTextMeasureCount`, and the same 0 B steady-state frame (`RenderAllocationTests`).
4. Shaped text is cached like #23/#74 text: shaped once on a miss, keyed like the width and layout entries,
   invalidated by the same events. A steady-state frame shapes nothing and allocates 0 B.
5. Measuring, ellipsis and wrapping agree with what is drawn. `MeasureText` returns the shaped width, ellipsis cuts
   never split a cluster, and wrapped lines are reordered one line at a time.
6. `DrawnTextBox` places the caret, draws the selection and hit tests correctly in RTL and mixed-direction text and
   inside ligatures and conjuncts.
7. The package works on every KumikoUI target: MAUI Android, iOS, Mac Catalyst and Windows; Uno desktop,
   WebAssembly, Android, iOS and Windows (WinAppSDK). It is AOT- and trim-safe.

### Non-goals

- Mirroring the grid layout (column order, frozen sides, popups) for `FlowDirection.RightToLeft`. That is #80. This
  spec only consumes the flow direction as the default paragraph direction (§7.3).
- Dictionary-based word breaking for Thai, Lao, Khmer and Myanmar (UAX #14 class SA). They wrap at spaces and, when
  no space fits, at grapheme boundaries.
- Justification (kashida), hyphenation, vertical text, and per-character formatting inside a cell.
- An API to choose OpenType features (`font-feature-settings`) or variable-font axes. HarfBuzz defaults apply.
- Platform shapers: CoreText, DirectWrite and Android's `TextRunShaper` (§4.3).
- Line heights that grow to fit tall scripts (Tibetan stacks, Thai above-marks). Lines still use the primary font's
  metrics, as in #74. #102 owns row heights.
- Localizing the built-in strings (CORE-32).

## 3. Current state (main, plus PRs #199 and #216)

| Area | Today | Problem |
|---|---|---|
| Fallback runs | `SkiaResourceCache.BuildRuns` / `Segment` / `ElementFaceLocked` pick a face per text element. `FontRuns` stores run ends, face indices and widths. | Runs can split a word between fonts, which breaks Arabic joining across the switch. |
| Drawing | `SkiaDrawingContext.DrawRuns` uses `SKCanvas.DrawText(string, …)` for one run, or creates an `SKTextBlob.Create(span, font)` per run on every draw. | No shaping, no bidi, and a blob is allocated per call. |
| Measuring | `MeasureNative` uses `SKFont.MeasureText(span)`, which sums advances from the character map. | Shaped widths differ (ligatures, marks, kerning in complex scripts). |
| Ellipsis / wrap | `KumikoTextLayout.LayOut`, `FitTextElements` (binary search over `StringInfo` text elements), `Ellipsize`, `FindWordBreak` (spaces). Prefix widths come from a `KumikoTextMeasurer` delegate. | It measures logical prefixes as separate strings. Shaping a prefix differs from the same prefix inside the line (Arabic final vs. medial forms). There is no UAX #14, so CJK wraps only by the character fallback. |
| Caches | `TextKey(Text, FontKey)` maps to `TextMeasurement(Width, HasControlCharacters, FontRuns)`. `TextLayoutKey(Text, FontKey, MaxWidth, Wrap, MaxLines)` maps to `TextLayoutEntry(KumikoTextLines, FontRuns[])`. LRU with `MaxTextMeasurements` (4096). `ClearTextLocked` bumps `_textGeneration`. | Neither key carries a direction. |
| Fallback language | `SystemTypefacePool.MatchCharacter(hint, style, [CurrentUICulture.Name], cp)` | One language per process. Script-appropriate fonts (Urdu Nastaliq, Han variants) are not requested. |
| Text box | `DrawnTextBox.PrefixWidth` / `XAt` / `GetIndexAtX` measure logical prefixes with `MeasureText(_text.Substring(…))`. `TextElements` gives grapheme boundaries. | It assumes x grows with the logical index. That is wrong for RTL and for clusters that reorder (Devanagari ि). |
| Bidi / line break / script data | None. `System.Globalization` exposes grapheme clusters (`StringInfo`) and general category only. | No public UBA, UAX #14 or Script property in .NET. |

The #74 report already flags this: "glyphs come from each font's character map, so ligatures, Arabic joining, Indic
reordering and multi-code-point emoji (ZWJ families, flags) are drawn as their component glyphs."

## 4. Options

### 4.1 Shaper

| | A. `SkiaSharp.HarfBuzz` `SKShaper` | B. `HarfBuzzSharp` directly (recommended) | C. Platform shapers |
|---|---|---|---|
| What | `new SKShaper(SKTypeface)`, then `Shape(string, SKFont)` returns `SKShaper.Result` (`Codepoints`, `Clusters`, `Points`, `Width`). `SKCanvas.DrawShapedText` builds a blob on every call. | `HarfBuzzSharp.Face` / `Font` / `Buffer`, with `Font.Shape(buffer, features)` and `Buffer.GetGlyphInfoSpan()` / `GetGlyphPositionSpan()`. | CoreText `CTLine` (Apple), DirectWrite `IDWriteTextAnalyzer` (Windows), Android `TextRunShaper` (API 31+). Nothing for WebAssembly. |
| Direction, script, language | `Buffer.GuessSegmentProperties()` guesses them from the text. Bidi levels cannot be passed. | We set `Direction` from the bidi level, `Script` from itemization and `Language` from the culture. | Each platform does its own itemization and bidi, which then differs between platforms. |
| Context across runs | No. Each call shapes an isolated string. | `Buffer.AddUtf16(text, itemOffset, itemLength)` passes the whole line as pre- and post-context, so joining is right across font runs. | n/a |
| Allocation | One `Buffer` and three result arrays per call. | We control the buffers: one reused `Buffer` per thread, and spans copied into the cached line. | Platform objects per call. |
| Font data | Opens the typeface as a stream (`SKTypeface.OpenStream`) and copies the whole file when the stream has no memory base. That is many MB for CJK fonts, and CoreText rebuilds an sfnt to do it. | A table callback (`Face` built from `SKTypeface.TryGetTableData`): HarfBuzz reads only the tables it needs (GSUB, GPOS, GDEF, cmap, hmtx, morx). Nothing else is copied. | Glyph ids belong to platform font objects and must be mapped back to `SKTypeface`s. They match only when both use the same font file. |
| Version coupling | `SkiaSharp.HarfBuzz` 4.153.1 requires `HarfBuzzSharp` 14.2.1.301, which conflicts with Uno 6.7's 8.3.1.1 (§5.2). | We choose the `HarfBuzzSharp` floor, which can be Uno's version. | n/a |
| Cost to build | Lowest. | Moderate, about 600 lines in the package. | Four implementations, and none for WebAssembly. |

**Decision: B.** `SKShaper` is a convenient demo API, but it guesses direction and script, cannot take bidi levels
or context, allocates on every call, and copies whole font files. It also pins a `HarfBuzzSharp` version that fights
Uno. The HarfBuzzSharp APIs we need (`Buffer.AddUtf16`, `ClusterLevel`, `GetGlyphInfoSpan`,
`GetGlyphPositionSpan`, `UnicodeFunctions.GetScript`, `Face` from a table callback) exist in both 8.3.1.1 and
14.2.1.301. **C is rejected:** there is no WebAssembly path, results would differ per platform, and the glyph-id
mapping is fragile.

### 4.2 Bidi

HarfBuzz shapes a run in one direction. It does not resolve levels or reorder runs. .NET has no public UBA:
`StringInfo` does UAX #29 graphemes only, and whatever bidi data the runtime holds for IDN mapping is internal.

| Option | Verdict |
|---|---|
| **Managed UBA in Core**, written from the UAX #9 text with tables generated from the UCD (`DerivedBidiClass.txt`, `BidiBrackets.txt`, `BidiMirroring.txt`) | **Chosen.** Works on every target including WebAssembly and AOT, with no native dependency. The tables are about 10–15 KB. It is validated with the official `BidiTest.txt` and `BidiCharacterTest.txt`. |
| Vendor an existing managed UBA (Avalonia's, derived from Topten.RichTextKit) | Fallback if the clean implementation stalls. License headers must be checked file by file: Avalonia is MIT, but parts derive from Apache-2.0 sources, which needs NOTICE handling. Avoid SixLabors.Fonts (Six Labors Split License). |
| A package: ICU4N, or Topten.RichTextKit | ICU4N is large and has been prerelease for years. RichTextKit is pinned to SkiaSharp 2.88 / HarfBuzzSharp and has its own layout model. Rejected. |
| Platform or ICU bidi: `android.icu.text.Bidi`, Windows `icu.dll` `ubidi_*`, the ICU that .NET bundles | Symbols are not exported on every target (WebAssembly and iOS link ICU statically into the runtime), and the P/Invokes would differ per platform. Rejected. |

UAX #14 line breaking follows the same plan (§8.3): a managed implementation with generated tables, validated with
`LineBreakTest.txt`.

### 4.3 Opt-in or always on

| | Always on in `KumikoUI.SkiaSharp` | Opt-in package `KumikoUI.SkiaSharp.HarfBuzz` (chosen) |
|---|---|---|
| Native cost | `libHarfBuzzSharp` in every MAUI app, about 1–1.35 MB compressed per ABI (§5.1), whether or not it shows non-Latin text. | Only apps that opt in pay it. |
| Behavior change | Kerning and ligatures could change widths for every grid. | Latin grids are unchanged. Shaping turns on only when the app enables it. |
| Discoverability | Nothing to do. | One line at startup, plus a README note. A one-time `Trace` warning when complex-script text is drawn without a shaper. |

A `SkiaShapingMode` setting covers the remaining choice (§6.2): `Off`, `ComplexScripts` (the default when the
package is enabled), or `AllText`.

## 5. Packaging, size, AOT and licensing

### 5.1 Native size per platform

Uncompressed and compressed sizes of `libHarfBuzzSharp` from `HarfBuzzSharp.NativeAssets.*` 14.2.1.301 (NuGet
catalog), with 8.3.1.1 for comparison. For scale: `libSkiaSharp` 4.153.1 is 9.8 MB (android-arm64), 13.9 MB (iOS)
and 12.9 MB (win-x64).

| Target | Asset | 14.2.1.301 (uncompressed / compressed) | 8.3.1.1 | Already in the app? |
|---|---|---|---|---|
| Android | `android-arm64/libHarfBuzzSharp.so` | 3.00 MB / 1.06 MB | 2.78 MB | MAUI: no. Uno (Skia renderer): yes, 8.3.1.1 |
| Android | `android-arm` / `x64` / `x86` | 2.16 / 2.94 / 2.89 MB (0.93 / 1.08 / 1.11 MB) | | per ABI in the APK; an AAB delivers one ABI |
| iOS | `libHarfBuzzSharp.framework` (device) | 2.88 MB / 1.35 MB | 2.89 MB | MAUI: no. Uno (Skia renderer): yes |
| Mac Catalyst | `libHarfBuzzSharp.framework.zip` | 1.31 MB zipped | n/a | MAUI: no |
| Windows | `win-x64` / `win-arm64` / `win-x86` `.dll` | 2.04 / 2.19 / 1.75 MB (1.02 / 0.98 / 0.84 MB) | 1.80 MB (x64) | MAUI and Uno WinAppSDK head: no. Uno desktop (Skia): yes |
| WebAssembly | `libHarfBuzzSharp.a` (static, per emscripten version) | 3.55–3.70 MB archive | 3.35 MB (3.1.56/st) | Uno WebAssembly: yes, linked by `Uno.WinUI.Runtime.Skia.WebAssembly.Browser` |
| Managed | `HarfBuzzSharp.dll` | 130 KB | | Uno: yes |

These are sizes of the native libraries, not app growth. Store compression, app thinning and the WebAssembly
linker (which keeps only referenced code from the `.a`) change the final number. Stage 0 measures the real delta
in the sample apps and records it here.

**Key point:** Uno 6.7's Skia runtimes (`uno.winui.runtime.skia.*` 6.7.135) already depend on `HarfBuzzSharp`
8.3.1.1 and its native assets for Uno's own text. On Uno Skia heads, the package costs about nothing if it uses the
same `HarfBuzzSharp` version. MAUI apps and the Uno WinAppSDK head pay the full size.

### 5.2 Version alignment

- `Directory.Packages.props` gets `<HarfBuzzSharpVersion>`, documented like `UnoWinUIVersion`: keep it at the
  version Uno.Sdk resolves.
- **Recommendation:** use `8.3.1.1` as the floor. NuGet then unifies on whatever is higher in the app (Uno's
  version, or one the app pins), and the package never forces a major `HarfBuzzSharp` upgrade onto Uno's runtime.
  The alternative, `14.2.1.301` (the version `SkiaSharp.HarfBuzz` 4.153.1 pins), would lift every Uno app to
  HarfBuzzSharp 14. That repeats the SkiaSharp 3→4 binding gamble that `Directory.Packages.props` already documents.
  Stage 0 verifies that Uno's `TextBlock` still renders with each candidate. libHarfBuzzSharp and libSkiaSharp are
  separate native libraries, so any HarfBuzzSharp version works with SkiaSharp 4.
- The package depends on `KumikoUI.SkiaSharp` with an **exact** version range (`[x.y.z]`), because the shaper seam
  is internal (§6.1).

### 5.3 WebAssembly native linking

- `HarfBuzzSharp.NativeAssets.WebAssembly` ships static libraries per emscripten version and threading/SIMD flavor
  (`libHarfBuzzSharp.a/3.1.56/{st,mt}[,simd]` for net9.0+). For Uno heads it adds them as `Content` (`IsUnoHead`).
  For plain .NET WebAssembly apps it adds `NativeFileReference` and sets `WasmBuildNative=true`, which needs the
  `wasm-tools` workload. Uno WebAssembly heads already relink natively for SkiaSharp.
- The #184 failure (fixed in PR #200) came from `SkiaSharp.NativeAssets.WebAssembly` 4.153.1 putting
  `-std=c++20 webgpu.cpp` on the emcc link line. HarfBuzz adds only a prebuilt static library, so it does not touch
  that workaround. Still, the relink is fragile, so the `build-sample-wasm` CI job must build the Uno sample **with
  the HarfBuzz package referenced** (Stage 0 and Stage 3 acceptance).
- Version alignment matters most here. Two `HarfBuzzSharp` versions cannot both link. NuGet's unification picks
  one, and Uno's runtime P/Invokes the same `hb_*` C ABI (stable across HarfBuzz versions). That still has to be
  verified on WebAssembly specifically.
- Plain (non-Uno) Blazor/WebAssembly is not a KumikoUI host. No extra work is needed there.

### 5.4 AOT and trimming

- `HarfBuzzSharp` 8.3.1.1 and 14.2.1.301 both mark themselves `IsTrimmable` and use `LibraryImport` with
  `UnmanagedCallersOnly` callbacks, so they need no runtime marshalling stubs. Table callbacks
  (`Face(GetTableDelegate)`) go through that same mechanism.
- `KumikoUI.SkiaSharp.HarfBuzz` sets `IsAotCompatible=true` like `KumikoUI.SkiaSharp`, and analyzer warnings are
  errors (`Directory.Build.targets`, #17).
- The AOT console smoke test from #76 (PR #196) gets a variant that enables HarfBuzz and draws Arabic, Devanagari
  and emoji text to a bitmap under `PublishAot`, with no trim or AOT warnings.

### 5.5 Licensing

| Component | License | Obligation |
|---|---|---|
| HarfBuzz (native) | "Old MIT" | Notice. Already in `HarfBuzzSharp`'s `THIRD-PARTY-NOTICES.txt`. |
| HarfBuzzSharp, SkiaSharp | MIT (Microsoft / Xamarin) | Notice in the package |
| UCD data (bidi, line break and mirroring tables generated into Core) | Unicode License v3 | Add a `THIRD-PARTY-NOTICES.md` at the repo root and pack it into `KumikoUI.Core` |
| Conformance test files (`BidiTest.txt`, `BidiCharacterTest.txt`, `LineBreakTest.txt`) | Unicode License v3 | Test-only, with the notice beside them |
| Test fonts (Noto Sans Arabic, Hebrew, Devanagari and Thai, Noto Emoji), subset | SIL OFL 1.1 | Test-only, with the license files beside them. Never packed. |

KumikoUI stays MIT. No copyleft enters the packages.

## 6. Public API and configuration

### 6.1 Seam between `KumikoUI.SkiaSharp` and the package

The seam is internal: `InternalsVisibleTo KumikoUI.SkiaSharp.HarfBuzz`, plus the exact-version dependency. That
way the shaping contract can change between minor versions without a public API commitment, and without the XML
documentation surface of #54.

```csharp
// KumikoUI.SkiaSharp (internal)
internal interface ISkiaTextShaper
{
    /// Splits [start, end) of `line` into script runs (HarfBuzz itemization) and calls `sink` per run.
    void ItemizeScripts(ReadOnlySpan<char> line, int start, int end, ref ScriptRunSink sink);

    /// Shapes one run: text[runStart..runEnd) in `face`, with the whole line as context.
    void Shape(in SkiaShapingRun run, SKTypeface face, float size, ref SkiaGlyphSink output);

    /// Drops per-typeface shaper state (HarfBuzz Face/Font) when the cache retires a typeface.
    void Release(SKTypeface face);
}

internal readonly ref struct SkiaShapingRun   // line, run range, bidi level (direction), script tag, language
internal ref struct SkiaGlyphSink             // appends glyph id, cluster, x/y advance, x/y offset, unsafe-to-break flag
```

### 6.2 Public surface

```csharp
// KumikoUI.Core.Rendering
public enum KumikoTextDirection { Auto = 0, LeftToRight, RightToLeft }

/// Options for the text drawing members; one struct instead of more overloads.
public readonly record struct KumikoTextOptions(
    KumikoTextAlignment HorizontalAlignment = KumikoTextAlignment.Left,
    KumikoVerticalAlignment VerticalAlignment = KumikoVerticalAlignment.Center,
    bool Clip = true,
    KumikoTextWrap Wrap = KumikoTextWrap.None,
    KumikoTextDirection Direction = KumikoTextDirection.Auto);

public interface IDrawingContext
{
    // New default interface members (implementers keep compiling, as in #23):
    void DrawTextInRect(string text, KumikoRect rect, KumikoPaint paint, in KumikoTextOptions options);   // default: #23 overload, ignoring Direction
    void DrawText(string text, float x, float y, KumikoPaint paint, KumikoTextDirection direction);      // default: DrawText(text, x, y, paint)
    KumikoTextCaretMap GetCaretMap(string text, KumikoPaint paint, KumikoTextDirection direction);       // default: LTR map from MeasureText prefixes (§10)
}

public static class KumikoBidi        // UAX #9, public like KumikoTextLayout so other IDrawingContexts can use it
{
    public static bool MayNeedBidi(ReadOnlySpan<char> text);                  // vectorized scan for R/AL/AN and explicit controls
    public static int ResolveParagraphLevel(ReadOnlySpan<char> text, KumikoTextDirection direction); // P2/P3, or forced
    public static void ResolveLevels(ReadOnlySpan<char> text, int paragraphLevel, Span<byte> levels); // X1–I2, per UTF-16 unit
    public static void ReorderLine(ReadOnlySpan<byte> levels, Span<int> visualToLogical);            // L1 (whitespace), L2
}

public static class KumikoLineBreaker  // UAX #14
{
    public static int NextBreak(ReadOnlySpan<char> text, int position, out bool mandatory);
}

// KumikoUI.Core.Models
public sealed class KumikoColumn { public KumikoTextDirection TextDirection { get; set; } }  // Auto by default (§7.3)

// KumikoUI.SkiaSharp
public enum SkiaShapingMode { Off, ComplexScripts, AllText }
public static class SkiaTextShaping
{
    public static SkiaShapingMode Mode { get; }        // Off until a shaper package enables it
    public static bool IsShaperInstalled { get; }
    public static void SetMode(SkiaShapingMode mode);   // bumps Version; every live SkiaResourceCache drops its text entries
    public static long Version { get; }
}

// KumikoUI.SkiaSharp.HarfBuzz
public static class KumikoHarfBuzz
{
    /// Installs the HarfBuzz shaper for every SkiaResourceCache in the process. Call once at startup (both hosts).
    public static void Enable(SkiaShapingMode mode = SkiaShapingMode.ComplexScripts);
}
```

- **`SkiaShapingMode.Off`**: bidi still runs (Core). Runs are drawn from the character map (the "simple shaper",
  §8.2).
- **`ComplexScripts`** (default when enabled): only runs that need shaping go through HarfBuzz (§7.1). Latin is
  untouched.
- **`AllText`**: every run is shaped, which adds kerning and standard ligatures for Latin. Widths and pixels of
  existing grids change. It is meant for apps whose fonts need it, and for testing.
- The setting is global, like `SkiaFontRegistrar`, and reaches every live cache the same way (the weak cache list
  and `OnRegistrarChanged`-style invalidation). A per-control override is an open question (§13).
- Hosts: no `KumikoUI.Maui` or `KumikoUI.Uno` change is required. The #51 `UseKumikoUI()` options may later forward
  to `KumikoHarfBuzz.Enable` if the app references the package. Hosts don't reference it.

## 7. Pipeline

```
line (after KumikoTextLayout.Normalize)
  │
  ├─ classify (once per cache miss): TextScan → HasRtl, NeedsShaping, HasControls
  │     neither RTL nor NeedsShaping ──────────────► #74 path: FontRuns + SKCanvas.DrawText (unchanged)
  ▼
1. bidi        KumikoBidi: paragraph level (§7.3), levels per UTF-16 unit
2. itemize     split into level runs → script runs (shaper.ItemizeScripts; one "script" when no shaper)
3. fallback    #74 face choice inside each run, run-level first (§7.4) → (level, script, face) runs
4. shape       per run: HarfBuzz (needs shaping, or AllText) or the simple shaper (character map)
5. reorder     L1/L2 on runs → visual order; pen positions; ShapedLine
6. draw        cached SKTextBlob (one positioned run per shaped run), built on first draw
```

### 7.1 Classification (the fast path)

`TextScan` (internal, SkiaSharp) runs once per text on a cache miss, stored in `TextMeasurement`:

- **HasRtl**: `KumikoBidi.MayNeedBidi`, a `SearchValues<char>` scan for U+0590–08FF, U+FB1D–FDFF, U+FE70–FEFF,
  explicit marks and embeddings (U+200F, U+202A–202E, U+2066–2069), and surrogates. A surrogate triggers a rune
  check for U+10800–10FFF and U+1E800–1EFFF.
- **NeedsShaping**: a `SearchValues<char>` scan for complex-script blocks. These are Arabic, Syriac, Thaana and NKo;
  Indic U+0900–0DFF; Thai, Lao and Tibetan U+0E00–0FFF; Myanmar; Hangul Jamo; Khmer and Mongolian; combining marks
  (Mn/Mc/Me via U+0300–036F, U+1AB0–1AFF, U+1DC0–1DFF, U+20D0–20FF, U+FE20–FE2F); ZWJ/ZWNJ; and variation
  selectors. Surrogates add a rune check for regional indicators, emoji modifiers, tags and SMP complex scripts.
  `AllText` sets it for any text.
- A text with neither flag never reaches the bidi or shaper code, and keeps `FontRuns.Primary` or #74's runs. The
  scan is one vectorized pass, about 50–100 ns for a 30-character cell, paid only on a miss.

### 7.2 Bidi

- `KumikoBidi.ResolveLevels` implements rules X1–X10 (explicit embeddings, overrides and isolates, with the
  125-level stack), W1–W7, N0 (paired brackets), N1–N2 and I1–I2. It writes into a caller span (`stackalloc` up to
  256 units, `ArrayPool` above that), and allocates nothing.
- `ReorderLine` applies L1 (trailing whitespace and separators reset to the paragraph level) and L2. L3 (combining
  marks) and L4 (mirroring) are left to the shaper. HarfBuzz mirrors in RTL runs. The simple shaper applies
  `Bidi_Mirroring_Glyph` from the generated table.
- Levels are computed **per paragraph** (the whole cell text). Each wrapped line is reordered on its own (§8.3).

### 7.3 Paragraph direction

The direction for a cell is resolved in this order:

1. `KumikoColumn.TextDirection` (or the header's, for header text) when it is `LeftToRight` or `RightToLeft`.
2. `Auto`: the first strong character (P2/P3, skipping isolates), as with HTML `dir="auto"`.
3. No strong character (numbers, punctuation, empty): the grid's flow direction from #80. Until #80 lands, this is
   left-to-right.

`DrawnTextBox` gets `TextDirection` (default `Auto`, set from the column by `CellEditorFactory`). While the user
types, `Auto` re-resolves from the text, so a box goes RTL when the first strong letter is Hebrew or Arabic.

Alignment stays physical: `KumikoTextAlignment.Left` means left. #80 owns whether `Start`/`End` alignments or
mirrored defaults are added (§13). This spec only guarantees that `Right` alignment of an RTL paragraph puts its
logical start at the right edge.

### 7.4 Font fallback with shaping (#74 integration)

The #74 rules stay (requested face, then `SkiaFontRegistrar.FallbackFamilies`, then
`SystemTypefacePool.MatchCharacter`), with two changes. Both apply only to text that needs shaping:

- **Run-level first.** For a script run the primary face doesn't cover, the fallback chosen for its first character
  is tried for the **whole run** (`SKFont.ContainsGlyphs` on the run span) before `Segment` falls back to choosing
  per element. An Arabic word is then never split between two fonts, which would break joining at the switch.
  `ElementFaceLocked` and `CharFaceLocked` keep their caches.
- **Script-aware language.** `MatchCharacter`'s BCP 47 list becomes the UI culture plus a representative language
  for the run's script when the two differ (`ar` for Arabic, `hi` for Devanagari, `th` for Thai). Android and
  Windows then return script-appropriate fonts. The `CharFaceKey` gains the script.

Synthetic bold and italic (`SKFont.Embolden`, `SkewX`) still apply at draw time through the run's `SKFont`.
HarfBuzz's advances come from the unemboldened face (see §13 on whether Skia widens emboldened advances).

### 7.5 Shaping (HarfBuzz)

- **Face cache.** One HarfBuzz `Face` and `Font` per `SKTypeface`, built from `SKTypeface.TryGetTableData` through a
  table callback, made immutable, with scale = units per em. Advances are converted to pixels by `size / upem` in
  managed code, so one `Font` serves every size. Entries live beside the cache's typeface entries (primary
  `TypefaceEntry` and `FallbackFace`). They are released through `ISkiaTextShaper.Release` when `RetireTypeface`,
  `ClearFallbackLocked` or `Dispose` runs, and are disposed at `EndFrame` like other retired objects.
- **Buffer.** One reused `Buffer` per thread (`[ThreadStatic]`). For each run: `ClearContents`;
  `AddUtf16(line, runStart, runLength)` (the whole line as context); `Direction` from the level parity; `Script`;
  `Language`; `ClusterLevel = MonotoneCharacters`; and `BufferFlags.BeginningOfText` / `EndOfText` only at
  paragraph edges.
- **Output.** `GetGlyphInfoSpan` and `GetGlyphPositionSpan` are copied into the line's arrays. That gives glyph id,
  cluster (a UTF-16 index into the line), advance and offset, plus `GlyphFlags.UnsafeToBreak` for §8.1.
- **Features.** HarfBuzz defaults (`liga`, `clig`, `kern`, `mark`, `mkmk`, `calt`, and the script-specific ones).
  `AllText` uses the same defaults for Latin.
- **AAT.** Apple fonts that use `morx` (some Apple system fonts, Apple Color Emoji) are shaped by HarfBuzz's AAT
  support, which is on in the HarfBuzzSharp build. Stage 0 confirms this on iOS and Mac Catalyst.

### 7.6 The simple shaper (no package, or `Off`)

For lines that need bidi but are not shaped, each run gets glyphs from `SKFont.GetGlyphs` and widths from
`SKFont.GetGlyphWidths`. Glyph order is reversed for odd (RTL) levels, and mirrored brackets come from the mirroring
table. This produces the same `ShapedLine`, so drawing, measuring, ellipsis and the caret map have one code path.
Hebrew is correct without HarfBuzz. Arabic is correctly ordered but unjoined.

### 7.7 `ShapedLine` and caching

```csharp
internal sealed class ShapedLine      // immutable after construction; owned by SkiaResourceCache
{
    ushort[] Glyphs;                  // visual order
    float[] X;                        // pen x of each glyph from the line's visual left (offsets applied)
    float[] Y;                        // y offset of each glyph (marks)
    int[] Clusters;                   // UTF-16 index (into the line) of each glyph's cluster
    ShapedRun[] Runs;                 // visual order: glyph range, text range, level, face, x, width
    float Width;
    byte ParagraphLevel;
    long Generation;                  // _textGeneration; stale faces draw nothing, as stale FontRuns do today
    SKTextBlob? Blob;                 // built on first draw, then reused; retired with the entry
}
internal readonly record struct ShapedRun(int GlyphStart, int GlyphCount, int TextStart, int TextEnd,
    byte Level, int Face, float X, float Width);
```

- **Keys.** `TextKey` becomes `(Text, FontKey, Direction)` and `TextLayoutKey` becomes
  `(Text, FontKey, MaxWidth, Wrap, MaxLines, Direction)`. Callers almost always pass `Auto`, so this adds no
  duplicates in practice. Mode changes clear the cache (§6.2) rather than being part of the key.
- **Entries.** `TextMeasurement` gains `ShapedLine? Shaped` (null means the `FontRuns` path).
  `TextLayoutEntry` gains `ShapedLine[]? Lines`. Both stay in the existing LRU maps under `MaxTextMeasurements`.
- **Blob.** On first draw, `SKTextBlobBuilder.AllocatePositionedRun(runFont, count)` is called once per shaped run,
  filling `SKPositionedRunBuffer.GetGlyphSpan()` and `GetPositionSpan()`. Then `Build()`. The blob is positioned at
  the origin. Every later draw is `SKCanvas.DrawText(blob, x, baseline, paint)`, with the color from the cached
  paint. That is a P/Invoke with no managed allocation.
- **Ownership.** Evicting or clearing an entry with a blob calls `Retire(blob)`, so it is disposed at `EndFrame`,
  never mid-frame. This is the same rule as fonts. `ResourceReleased` reports it for tests.
- **Invalidation.** Unchanged (a `SkiaFontRegistrar` change, `SetFallbackFamilies`, `Clear()`), plus
  `SkiaTextShaping.SetMode`. All go through `ClearTextLocked`, which bumps `_textGeneration`, retires blobs, and
  drops shaper faces that belong to retired typefaces.
- **Memory.** About 14 B per glyph managed, plus the blob (native, about 10 B per glyph plus about 100 B). A
  4096-entry cache of 30-glyph cells is about 3 MB in the worst case. Stage 6 measures it. A separate cap is an open
  question.
- **Thread safety.** As in `MeasureTextWidth`: look up under `_gate`, shape outside it, and publish under `_gate`
  if the generation is unchanged. HarfBuzz `Font`s are immutable and shared. Buffers are per thread.

## 8. Measurement, ellipsis and wrapping

### 8.1 Measuring

`MeasureText(text, paint)` returns `ShapedLine.Width` (the sum of advances) for shaped text, and the #74 width
otherwise. `MeasureText(text, paint, maxWidth, wrap)` returns the width of the laid-out shaped lines. Header-fit
width (#182), auto-size (#24) and #102 row measurement therefore become shaping-aware without changes.

### 8.2 Ellipsis (cluster-aware, on shaped output)

Measuring each logical prefix as its own string, as `FitTextElements` does now, is wrong for shaped text and costs a
shape per probe. For shaped lines, the new `KumikoTextLayout` entry point takes a **prefix measurer** instead of a
string measurer:

```csharp
public delegate float KumikoPrefixMeasurer(int start, int end);    // width of line[start..end) as shaped in the whole line
public static KumikoTextLines LayOut(string text, float maxWidth, KumikoTextWrap wrap, int maxLines,
    KumikoTextMeasurer measure, KumikoPrefixMeasurer? measureRange, ReadOnlySpan<int> breakCandidates);
```

The Skia measurer answers `measureRange` from the paragraph's `ShapedLine`. It sums the advances of glyphs whose
cluster lies in the range. Clusters are monotone per run (`MonotoneCharacters`), so this costs O(glyphs) with no
shaping.

The algorithm, for one line that does not fit:

1. **Candidates.** Cut positions are text-element boundaries (`StringInfo`, as today) that are also cluster starts
   in the shaped line, and preferably not marked `UnsafeToBreak`. A Devanagari conjunct, a lam-alef ligature or an
   emoji sequence is never cut.
2. **Search.** Binary-search the candidates for the longest logical prefix whose estimated width plus the ellipsis
   width fits.
3. **Reshape once.** Shape the shown string: prefix, then a direction mark (LRM U+200E for an LTR paragraph, RLM
   U+200F for RTL), then `KumikoTextLayout.Ellipsis`. The mark is default-ignorable, so HarfBuzz drops it from the
   output. It forces the ellipsis to resolve to the paragraph direction, so the ellipsis always sits at the
   paragraph's **end side**: the right edge for LTR and the left edge for RTL.
4. **Verify.** If the reshaped width exceeds `maxWidth` (an Arabic final form can be wider than the medial form it
   replaces), step back one candidate and reshape again. This is usually 0–1 retries, capped at 3, after which just
   the ellipsis is drawn.

In an LTR paragraph that ends with RTL text, the ellipsis appears at the right end and the cut is at the visual left
of the trailing RTL run. This matches browser `text-overflow: ellipsis`, and is documented. Everything else in
`Ellipsize` stays: the trimmed trailing space, a lone ellipsis when nothing fits, and nothing at width 0.

### 8.3 Wrapping (UAX #14)

- `KumikoLineBreaker` implements UAX #14 (LB1–LB31, the pair table, East Asian Width for LB30) with generated
  `LineBreak.txt` classes. SA maps to AL, or to CM for Mn/Mc, per LB1, so Thai, Lao, Khmer and Myanmar only break at
  spaces and at the grapheme fallback.
- `KumikoTextWrap.Word` uses `KumikoLineBreaker` break opportunities instead of `FindWordBreak`'s space scan, for
  every text, shaped or not. CJK then wraps between ideographs, and break rules around punctuation are honored. This
  benefits Latin too, and is staged separately (Stage 4). `KumikoTextWrap.Character` keeps breaking at any text
  element.
- Line fitting uses `measureRange` over the paragraph's shaped line (step 2 above), then **each line is reshaped and
  reordered on its own**. UBA L1/L2 are per line, but the levels come from the paragraph. That is one extra shape
  per wrapped line, on a miss only.
- The last allowed line ends with the ellipsis as in §8.2.

## 9. Drawing changes in `SkiaDrawingContext`

- `DrawText` and both `DrawTextInRect` overloads get the `ShapedLine` from the cache when classification says so.
  Otherwise they keep `DrawRuns`.
- `AlignX` is unchanged: the line's visual width is aligned in the rectangle. `DrawText(text, x, y, …)` takes x as
  the visual left of the line in every direction. This is documented, and callers that want right-anchored RTL text
  subtract the width.
- Shaped lines draw through their cached blob (§7.7). The per-call `SKTextBlob.Create` in `DrawRuns` stays only for
  unshaped multi-run LTR text. Caching blobs for that path too is a separate perf follow-up (§13).
- When complex-script text is drawn with `SkiaTextShaping.IsShaperInstalled == false`, a one-time
  `Trace.TraceWarning` says: "install KumikoUI.SkiaSharp.HarfBuzz and call KumikoHarfBuzz.Enable() to shape
  Arabic/Indic/emoji sequences". This follows the `ReportUnknownFamily` pattern.

## 10. Caret, selection and hit testing (`DrawnTextBox`)

### 10.1 Caret map

`KumikoTextCaretMap` is a new Core class: immutable, cached by the drawing context, and returned by
`IDrawingContext.GetCaretMap`.

```csharp
public sealed class KumikoTextCaretMap
{
    public int Length { get; }
    public bool IsRightToLeft { get; }                                           // paragraph direction
    public float Width { get; }
    public float GetCaretX(int index, KumikoCaretAffinity affinity);            // Upstream / Downstream at direction changes
    public int GetIndexAtX(float x, out KumikoCaretAffinity affinity);          // nearest grapheme boundary
    public int GetSelectionSpans(int start, int end, Span<KumikoTextSpanX> spans); // visual [left, right) pieces; count
    public int MoveVisual(int index, KumikoCaretAffinity affinity, bool toRight, out KumikoCaretAffinity newAffinity);
}
```

- **Default implementation** (in `IDrawingContext`): an LTR map from `MeasureText` prefix widths at `TextElements`
  boundaries. This is exactly what `DrawnTextBox.PrefixWidth` / `XAt` / `GetIndexAtX` do now, moved to Core so other
  `IDrawingContext`s keep today's behavior.
- **Skia implementation**: built from the `ShapedLine` and cached with it. Caret stops are text-element boundaries.
  Inside a cluster that spans several stops (a ligature, a conjunct), the cluster's advance is split evenly. GDEF
  ligature carets are an open question (§13). An index's x is the **leading edge in its run's direction**: the left
  edge of an LTR glyph, the right edge of an RTL glyph. At a level boundary the affinity picks which of the two
  positions to show (the primary caret). A secondary caret is not drawn, a deliberate simplification.

### 10.2 `DrawnTextBox` changes

- `PrefixWidth`, `XAt` and `GetIndexAtX` are replaced by the caret map, fetched each frame from
  `ctx.GetCaretMap(_text, paint, TextDirection)` (a cache hit) and held for input handling between frames, which is
  how `_measureContext` is used today.
- **Selection** fills one rectangle per span from `GetSelectionSpans`. A logical range across a direction change is
  2 or more visual pieces. The IME composition underline uses the same spans.
- **Hit testing.** Pointer down, drag-select and double-click word selection use `GetIndexAtX`. Word boundaries
  stay logical (`PreviousWordBoundary` / `NextWordBoundary`).
- **Arrow keys** move **visually** (`MoveVisual`), as native text boxes do on Windows, macOS, iOS and Android.
  Shift+arrow extends the logical selection to the new index. Home and End go to the visual line ends. Backspace and
  Delete stay logical, on grapheme boundaries (`TextElements`).
- **RTL paragraphs.** Text is drawn right-anchored in the inner rectangle, and `_scrollOffset` mirrors in
  `EnsureCursorVisible` (it scrolls from the right). Text drawing uses
  `DrawText(text, x, baseline, paint, TextDirection)`.
- Caret blink, undo/redo and composition are unchanged in logic.

## 11. Test plan

### 11.1 Unit and conformance

| Test | What it proves |
|---|---|
| `KumikoBidiConformanceTests` | 100% of `BidiTest.txt` and `BidiCharacterTest.txt` for the generated Unicode version (levels and reorder). The files are committed gzip-compressed with the Unicode notice. |
| `KumikoLineBreakerConformanceTests` | `LineBreakTest.txt` passes, except a documented list of SA/dictionary cases. |
| `KumikoBidiTests` | Paragraph level resolution (Auto, forced, no strong character falls back to the flow direction), isolates, brackets (N0), and no allocation for 256 units or fewer. |
| `TextScanTests` | Latin, CJK, Cyrillic and Greek (no combining marks) are neither RTL nor complex. Each complex block and each SMP emoji component sets the right flag. |
| `ShapingFastPathTests` | Latin text never calls the shaper (`NativeShapeCount == 0`) and keeps `FontRuns.Primary`. Pixels are identical to shaping `Off`. `NativeTextMeasureCount` matches #74's tests. |
| `HarfBuzzShapingTests` (glyph-level, platform independent) | For each test string and committed subset font, glyph ids, clusters and advances equal the reference output of `hb-shape` (JSON committed beside the fonts). HarfBuzz output does not depend on the OS rasterizer, so this runs on every CI OS. |
| `SimpleShaperBidiTests` | Hebrew without the package: glyph order reversed per run, numbers stay LTR inside RTL, and brackets are mirrored. |
| `ShapedEllipsisTests` | Never cuts inside a lam-alef, a conjunct (क्ष), a ZWJ family or a flag. Width ≤ `rect.Width` in every case. Ellipsis on the left for RTL and on the right for LTR. No more than 4 shapes per miss. |
| `ShapedWrapTests` | Arabic wraps at spaces with each line reordered on its own. CJK `Word` wrap breaks between ideographs. Mixed LTR/RTL across a wrap. |
| `ShapedCacheTests` | A hit allocates 0 B and shapes nothing. `SetMode`, `SetFallbackFamilies` and registrar changes drop shaped entries and retire blobs (`ResourceReleased`). Disposing the cache releases HarfBuzz faces. A stale generation draws nothing and does not crash. |
| `FallbackWithShapingTests` | An Arabic word whose letters span two fallback faces stays in one face when one covers the whole run. The script-aware BCP 47 list is passed to `CharacterMatcher` (seam). |
| `CaretMapTests` | `"abc אבג def"`: caret x at every boundary, affinity at the two direction changes, a selection across a boundary gives 2 spans, `GetIndexAtX` round-trips, visual arrows walk across runs, and ligature stops are interpolated. The default LTR map matches today's `DrawnTextBox` behavior (existing tests unchanged). |
| `DrawnTextBoxBidiTests` | Typing Hebrew in an `Auto` box flips it to RTL and right-anchors it. The scroll offset mirrors. Backspace removes one grapheme. Composition underline spans. |

Font-dependent tests use committed subset fonts registered through
`SkiaFontRegistrar.RegisterTypefaceFromStream`, the same way `SkiaTestFonts` embeds Open Sans. They do not depend on
installed system fonts. `SystemFontFactAttribute` stays for the tests that exercise `MatchCharacter` on the host.

### 11.2 Golden images

There are no golden images in the repository yet (`SkiaGlyphRasterTests` does pixel checks). This adds a small
harness:

- **Scripts.** Each case renders one 300×40 cell at 1× and 2× scale, LTR and RTL paragraphs, with the HarfBuzz
  package enabled:

  | Script | Cases |
  |---|---|
  | Arabic | `مرحبا بالعالم`, `لا`, harakat, `السعر 120 دولار`, and a truncated cell |
  | Hebrew | `שָׁלוֹם`, `(שלום)`, `גרסה 2.5`, and a truncated cell |
  | Devanagari | `हिन्दी`, `क्षत्रिय`, `नमस्ते` |
  | Thai | `สวัสดีครับ`, `ที่` |
  | Emoji | ZWJ family, `🇯🇵`, `👍🏽`, `1️⃣`, using monochrome Noto Emoji so the raster is deterministic |
  | Mixed | `Order #123 للعميل`, and Latin + Arabic + emoji in one cell |
- **Platform.** Images are compared on the Linux CI leg only. Skia's font backend (FreeType there, CoreText and
  DirectWrite elsewhere) rasterizes differently. The glyph-level tests (§11.1) cover the other OSes.
- **Tolerance.** At most 0.5% of pixels may differ, each by no more than 24 per channel.
- **Failures and updates.** A failure writes `actual.png` and `diff.png` to `TestResults/goldens` and uploads them as
  a workflow artifact. `KUMIKO_UPDATE_GOLDENS=1` rewrites the baselines.
- **Fonts.** Test fonts are subset with `hb-subset` to the test strings, keeping layout closure. Each is a few KB
  instead of hundreds, with the OFL license beside it.

### 11.3 Performance budgets (added to `bench/README.md`)

A new `ShapingBenchmarks` class in `bench/KumikoUI.Benchmarks` (`[MemoryDiagnoser]`, deterministic strings):

| Operation | Time budget (reference machine) | Allocation budget | How it is checked |
|---|---|---|---|
| Steady-state 30×10 frame of shaped Arabic and Devanagari cells (real `SkiaDrawingContext`) | ≤ 1.1× the same frame of Latin text | **0 B** managed | New `ShapedRenderAllocationTests` (every CI run); time by benchmark |
| Latin 30×10 frame with the package enabled (`ComplexScripts`) | within noise of the package not installed | unchanged from #74 | `ShapingFastPathTests` (allocation); time by benchmark |
| Shape one 30-character Arabic cell on a miss (bidi, itemize, fallback lookup, shape, build `ShapedLine`) | ≤ 30 µs | ≤ 2 KB | by benchmark; allocation in `PerformanceBudgetTests` |
| Ellipsis of a 200-character RTL cell into 120 px, cold | ≤ 120 µs, ≤ 4 shapes | ≤ 8 KB | by benchmark; shape count asserted in tests |
| Scrolling frame revealing one row of 10 new shaped cells | ≤ 400 µs | ≤ 24 KB | by benchmark |
| Cold first frame, 300 distinct shaped cells | ≤ 12 ms | n/a | by benchmark |
| `KumikoBidi.ResolveLevels`, 100 mixed characters | ≤ 2 µs | 0 B | by benchmark; allocation in tests |
| Classification scan, 30 Latin characters | ≤ 100 ns | 0 B | by benchmark |

As in `bench/README.md`, time budgets are about 3× the expected mean and do not gate CI. Allocation budgets do. The
numbers are targets to confirm in Stage 6. Lower them when the measurements are better.

### 11.4 Platform and packaging checks

- The AOT smoke test (#76) with HarfBuzz enabled: no warnings, and the bitmap matches the Linux golden within
  tolerance.
- CI builds `samples/SampleApp.Uno` for `net10.0-browserwasm` with the package referenced (the #184 job), and the
  MAUI sample for Android, iOS, Mac Catalyst and Windows.
- Manual device pass (Stage 6): an "International text" sample page on each host, with system fonts (no test fonts)
  on Android, iOS, Mac Catalyst, Windows, Uno desktop and Uno WebAssembly. Screenshots go in the PR.

## 12. Staged implementation issues

Every stage keeps the Latin fast path unchanged and the steady-state frame at 0 B. Every public member is documented
(#54). Each stage updates `docs/RENDERING.md` for what it ships.

### Stage 0: Spike — HarfBuzzSharp packaging on every target

Acceptance criteria:
- [ ] A throwaway branch draws Arabic through HarfBuzzSharp with a table-callback `Face` on MAUI Android, iOS,
      Mac Catalyst and Windows, and on Uno desktop, WebAssembly, Android, iOS and WinAppSDK.
- [ ] Uno's own `TextBlock` still renders with `HarfBuzzSharp` 8.3.1.1 and with 14.2.1.301 on desktop and
      WebAssembly. The chosen floor is recorded in §5.2.
- [ ] Measured app-size deltas (APK/AAB per ABI, IPA, MSIX, WebAssembly `dotnet.native.wasm`) replace the estimates
      in §5.1.
- [ ] `PublishAot` console draws shaped text with no warnings. AAT `morx` shaping is confirmed on Apple.

### Stage 1: Core — Unicode Bidirectional Algorithm (UAX #9)

Acceptance criteria:
- [ ] An `eng/UnicodeData` generator produces the bidi class, bracket and mirroring tables from the UCD version .NET
      10's `StringInfo` uses. The output is committed, and a check fails CI when it is stale.
- [ ] `KumikoBidi` passes 100% of `BidiTest.txt` and `BidiCharacterTest.txt`.
- [ ] No allocation for 256 UTF-16 units or fewer. `MayNeedBidi` is a single vectorized scan.
- [ ] `THIRD-PARTY-NOTICES.md` (Unicode License v3) is added and packed into `KumikoUI.Core`.

### Stage 2: SkiaSharp — glyph-run lines, bidi reordering and `KumikoTextDirection`

Acceptance criteria:
- [ ] `TextScan`, `ShapedLine`, the simple shaper and cached `SKTextBlob`s are in `SkiaResourceCache`. Keys carry
      the direction, and blobs are retired at `EndFrame`.
- [ ] Hebrew and unshaped Arabic draw in visual order, with mirrored brackets and LTR numbers (glyph-order tests).
- [ ] `KumikoTextDirection`, `KumikoTextOptions`, the new `IDrawingContext` default members and
      `KumikoColumn.TextDirection` are added. `TextCellRenderer` and header drawing pass the direction. The
      no-strong-character fallback reads #80's flow direction once #80 lands (LTR until then).
- [ ] Latin text takes the #74 path with identical pixels and counters. `ShapedRenderAllocationTests` shows 0 B
      for RTL text.

### Stage 3: New package `KumikoUI.SkiaSharp.HarfBuzz` — shaping of complex-script runs

Acceptance criteria:
- [ ] `src/KumikoUI.SkiaSharp.HarfBuzz` (net10.0, `IsAotCompatible`). It depends on `HarfBuzzSharp`
      (`$(HarfBuzzSharpVersion)`) and on `KumikoUI.SkiaSharp` with an exact version. It is packed and published by
      `publish.yml` and has a NuGet README (#95 pattern).
- [ ] `KumikoHarfBuzz.Enable()` and `SkiaTextShaping` with `Off`, `ComplexScripts` and `AllText`. Invalidation
      reaches every live cache.
- [ ] Script itemization, run-level fallback and script-aware `MatchCharacter` languages work. HarfBuzz faces are
      released with their typefaces.
- [ ] Glyph-level tests match `hb-shape` for Arabic (joining, lam-alef, harakat), Devanagari (ि reordering,
      conjuncts, reph), Thai (marks) and emoji (ZWJ family, flag, keycap and skin tone as one glyph each).
- [ ] A one-time warning when complex text is drawn without a shaper. The Uno WebAssembly CI job builds with the
      package.

### Stage 4: Shaping-aware ellipsis and UAX #14 line breaking

Acceptance criteria:
- [ ] `KumikoLineBreaker` passes `LineBreakTest.txt`, except the documented SA cases. `KumikoTextWrap.Word` uses it
      for all text, and CJK wraps between ideographs.
- [ ] `KumikoPrefixMeasurer` / the `LayOut` overload are added. The Skia implementation estimates from cluster
      advances and reshapes once, with at most 3 retries.
- [ ] Ellipsis is never inside a cluster, on the paragraph's end side, and width ≤ rect. Each wrapped line is
      reshaped and reordered on its own.
- [ ] Existing #23 `KumikoTextLayoutTests` pass unchanged for unshaped text.

### Stage 5: `DrawnTextBox` — bidi- and cluster-aware caret, selection and hit testing

Acceptance criteria:
- [ ] `KumikoTextCaretMap`, `KumikoCaretAffinity`, `IDrawingContext.GetCaretMap` (an LTR default that reproduces
      today's behavior) and the Skia implementation from `ShapedLine`.
- [ ] `DrawnTextBox.TextDirection` is set by `CellEditorFactory` from the column. RTL boxes are right-anchored with
      a mirrored scroll offset.
- [ ] Selection and composition spans across direction changes. Visual arrow movement and logical deletion.
      Ligature caret interpolation.
- [ ] Existing `DrawnTextBox` tests pass unchanged.

### Stage 6: Golden images, performance budgets and documentation for international text

Acceptance criteria:
- [ ] Linux golden-image suite (§11.2) with subset OFL fonts, an update switch and diff artifacts in CI.
- [ ] `ShapingBenchmarks` and the §11.3 rows in `bench/README.md`. Allocation budgets enforced in tests.
- [ ] `docs/RENDERING.md` gets a "Shaping and bidi" section that replaces the #74 "drawn without shaping"
      paragraph. The README lists the package and what works without it.
- [ ] An "International text" sample page on both hosts, with screenshots from each platform in the PR.

Dependencies: Stage 0 informs Stage 3. The order is Stage 1, then 2, then 3. Stages 4 and 5 follow Stage 3; Stage 4
can start after Stage 2 for the UAX #14 part. Stage 6 grows with each stage. The #80 flow direction plugs into
Stage 2's fallback whenever #80 lands.

## 13. Open questions

1. **`HarfBuzzSharp` floor.** 8.3.1.1 (aligned with Uno 6.7, no forced upgrade) or 14.2.1.301 (aligned with
   `SkiaSharp.HarfBuzz` 4.153.1)? The recommendation is 8.3.1.1 pending Stage 0. Also, how is the pin kept in step
   when Uno.Sdk moves?
2. **Bidi without the package.** Should bidi reordering be always on in `KumikoUI.SkiaSharp` (recommended: it fixes
   Hebrew and has no native cost), or ship only with the HarfBuzz package so the base package's output never
   changes?
3. **Default paragraph direction.** Is first-strong (`Auto`) with the grid's flow direction as the fallback right
   for grids, or should an RTL grid (#80) force RTL paragraphs? Should `Auto` alignment flip `Left`/`Right` for RTL
   cells (Excel's "context" behavior), or is that #80's `Start`/`End`?
4. **Per-control mode.** Is a global `SkiaTextShaping` mode enough, or do apps need a per-grid override (a
   `SkiaResourceCache` property set by the host)?
5. **Shaping language.** UI culture plus a script default, or a per-column `KumikoColumn.Language`? It matters for
   Urdu (Nastaliq vs. Naskh), Serbian and Macedonian Cyrillic forms, and Han variants in fallback.
6. **Synthetic bold.** Does Skia widen advances for `SKFont.Embolden`? If it does, shaped widths must add the same
   amount, or emboldened shaped text will overlap.
7. **Ligature carets.** Even split (proposed) or GDEF ligature caret positions? Does HarfBuzzSharp expose
   `hb_ot_layout_get_ligature_carets` in both candidate versions?
8. **Secondary caret.** Drawing a split caret at direction boundaries: needed, or is the primary caret with
   affinity enough?
9. **Cache sizing.** Should shaped entries and blobs get their own cap (for example `MaxShapedTexts`), or share
   `MaxTextMeasurements`?
10. **Conformance data in the repo.** Commit the gzip-compressed UCD test files (a few MB), or download them in CI
    by pinned URL and hash?
11. **Blob caching for unshaped LTR runs.** Cache `SKTextBlob`s for the #74 path too? That removes SkiaSharp's
    per-call blob allocation for all text (a measurable win in `SkiaFrameBenchmarks`), but it is a perf change
    outside this issue. File it separately?
12. **Dictionary line breaking.** Is Thai, Lao, Khmer and Myanmar wrapping only at spaces acceptable for 1.x, or
    should a later issue evaluate ICU dictionary data (MBs) behind the HarfBuzz package?
