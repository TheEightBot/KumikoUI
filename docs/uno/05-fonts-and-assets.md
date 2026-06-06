# 05 — Fonts & Assets

Goal: achieve the same custom-typeface support on Uno that the MAUI sample has (CJK via `NotoSansJP`,
icon glyphs via `MaterialIcons`), reusing `KumikoUI.SkiaSharp`'s `SkiaFontRegistrar` **unchanged**.

Why this matters: SkiaSharp renders text with whatever typeface it can resolve. On Android in
particular, CJK and icon fonts garble without an explicitly registered typeface — exactly what
`SkiaFontRegistrar` solves (see commit history / [../RENDERING.md](../RENDERING.md)).

---

## 1. The reused API (no changes)

`KumikoUI.SkiaSharp.SkiaFontRegistrar` (static) is platform-agnostic — it just needs a `Stream` or an
`SKTypeface`:

| Member | Use |
|---|---|
| `RegisterTypefaceFromStream(string family, Stream stream)` | Register a TTF/OTF from any stream (the common path). |
| `RegisterTypeface(string family, SKTypeface typeface)` | Register a pre-built typeface. |
| `TryGetTypeface(string family, out SKTypeface?)` | Used internally by `SkiaDrawingContext`. |
| `Clear()` | Reset (tests). |

A family registered here is then usable from `DataGridStyle` / `GridFont.Family` (e.g. header/cell
fonts), identical to MAUI.

## 2. Loading assets on Uno (the platform-specific bit)

MAUI used `FileSystem.OpenAppPackageFileAsync(...)`. The Uno/WinUI equivalent is `StorageFile` with the
`ms-appx:///` scheme, which resolves correctly on every head.

**Improvement over MAUI:** the MAUI sample carried an *app-private* `TryRegisterFont` helper inside
`MauiProgram` (every consumer copy-pastes it). On Uno the registration helper instead ships **in the
library** as `KumikoUI.Uno.KumikoFonts` (`src/KumikoUI.Uno/KumikoFonts.cs`), so every consumer reuses
one well-documented, XML-doc'd entry point. The library itself **still ships no fonts** — it stays
font-agnostic exactly like `KumikoUI.Maui`; only the *helper* moved into the library, not the assets.

- [x] Reusable, library-provided helper (`KumikoFonts`) that mirrors the sample's `TryRegisterFont`:
  ```csharp
  public static async Task RegisterFromAppPackageAsync(string family, string appxUri)
  {
      try
      {
          var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(appxUri));
          using var randomAccessStream = await file.OpenReadAsync();
          using var stream = randomAccessStream.AsStreamForRead();   // Uno stream surface
          SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);
      }
      catch (Exception ex)
      {
          // Non-fatal — the grid falls back to the system default font.
          System.Diagnostics.Debug.WriteLine($"[KumikoUI] Could not register font '{family}' from '{appxUri}': {ex.Message}");
      }
  }
  ```
  > **Stream API note:** the helper uses `(await file.OpenReadAsync()).AsStreamForRead()`, **not** the
  > `System.IO` `OpenStreamForReadAsync()` extension shown in the original draft above. That extension
  > comes from the `System.Runtime.WindowsRuntime` interop shim, which is **not present on Uno's
  > non-WinAppSDK heads**. `AsStreamForRead()` is part of Uno's own stream surface and was verified to
  > compile uniformly on `net9.0-desktop` (and is available on the Windows head too).
  >
  > The helper also provides an `IEnumerable<(string Family, string AppxUri)>` batch overload and a
  > `RegisterFromStream(string family, Stream)` pass-through (for fonts sourced outside the app package).
- [x] Register the same families the MAUI sample does (the consuming sample app calls these in Phase 06):
  ```csharp
  await KumikoFonts.RegisterFromAppPackageAsync("NotoSansJP",    "ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf");
  await KumikoFonts.RegisterFromAppPackageAsync("MaterialIcons", "ms-appx:///Assets/Fonts/MaterialIcons-Regular.ttf");
  // …or in one batch call:
  await KumikoFonts.RegisterFromAppPackageAsync(new[]
  {
      ("NotoSansJP",    "ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf"),
      ("MaterialIcons", "ms-appx:///Assets/Fonts/MaterialIcons-Regular.ttf"),
  });
  ```

## 3. When to register (startup)

- [x] Register **before** the first grid render. In the Uno app, `await` registration early in
  `App.OnLaunched` (before activating the main window), or inside `UseKumikoUI()` startup. Because the
  grid falls back to the system font, an acceptable alternative is fire-and-forget registration followed
  by a `DataGridView.Invalidate()` once complete.
  > Avoid `.GetAwaiter().GetResult()` blocking on Uno startup (unlike MAUI's pre-loop `CreateMauiApp`,
  > Uno has a live dispatcher and could deadlock). Prefer `await`.

  `KumikoFonts.RegisterFromAppPackageAsync(...)` is `async`/awaitable by design (it does **not** block),
  so it drops straight into an `await` in `App.OnLaunched`. Wiring this call into the sample's startup is
  done in **[06 — Sample app](06-sample-app.md)**; this phase delivers the library helper it calls.

## 4. Where the font files live

- [x] **Consuming app (recommended, matches MAUI sample):** put TTFs under the app's `Assets/Fonts/`
  and mark them as content so they ship with the package:
  ```xml
  <ItemGroup>
    <Content Include="Assets/Fonts/*.ttf" />
  </ItemGroup>
  ```
  Reference with `ms-appx:///Assets/Fonts/<file>.ttf`. This keeps `KumikoUI.Uno` font-agnostic, exactly
  like `KumikoUI.Maui` (the library ships no fonts; the app supplies them).
  > The library-side enabler for this — `KumikoFonts.RegisterFromAppPackageAsync` — is delivered in this
  > phase. The sample app actually drops the TTFs under `Assets/Fonts/` and calls the helper in
  > **[06 — Sample app](06-sample-app.md)**.

## 5. Shipping assets *inside* the library (only if needed) — **NOT APPLICABLE here**

> **Status: documented for completeness, intentionally not used.** `KumikoUI.Uno` ships **no** fonts
> (verified: no `.ttf`/`.otf` and no font/`Content` item-group in `KumikoUI.Uno.csproj`), so none of the
> rules below were exercised. They are retained as guidance for a consumer that chooses to ship default
> assets *inside a library* of their own.

If a library itself must carry default assets, follow Uno's library-asset rules (Uno 4.6+):

- [x] Set `<GenerateLibraryLayout>true</GenerateLibraryLayout>` (already added in [02 §5](02-library-scaffold.md#5-library-layout--assets)).
  Present in `KumikoUI.Uno.csproj` regardless, since it also produces the proper `.xr.xml` layout.
- [ ] Reference library assets via `ms-appx:///KumikoUI.Uno/<asset path>`. *(N/A — no library assets.)*
- [ ] **Lower-case the library name on non-WinAppSDK heads:** use `ms-appx:///kumikoui.uno/...` for
  Android/iOS/WASM/Desktop, `ms-appx:///KumikoUI.Uno/...` for the Windows head. Branch on the target.
  *(N/A — no library assets; recorded as the rule a consumer shipping library assets must follow.)*
- [ ] **MSIX caveat:** WinAppSDK does **not** serve library assets in MSIX-packaged mode — use the
  unpackaged deployment mode for the Windows head if you rely on library-shipped assets.
  *(N/A — no library assets.)*

---

## ✅ Exit criteria

- [ ] Japanese text renders correctly (no tofu/garbling) on Android and WASM, not just desktop.
  *(Deferred to [Phase 06](06-sample-app.md) — runtime rendering is verified with the sample. This phase
  delivers the registration helper that makes it possible and proves it compiles on `net9.0-desktop`.)*
- [ ] Material icon glyphs render in cells that use the `MaterialIcons` family. *(Deferred to Phase 06.)*
- [ ] Removing the registration cleanly falls back to the system font (no crash). *(Deferred to Phase 06;
  the helper is non-fatal by construction — every app-package registration is wrapped in try/catch and
  only logs via `Debug.WriteLine`.)*
- [x] `KumikoUI.Uno` ships no fonts unless §5 was deliberately chosen. §5 was **not** chosen — the
  library ships no fonts; the app supplies them via `KumikoFonts`.

➡️ Next: [06 — Sample app](06-sample-app.md)
