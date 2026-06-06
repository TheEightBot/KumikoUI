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
`ms-appx:///` scheme, which resolves correctly on every head:

- [ ] Helper that mirrors the sample's `TryRegisterFont`:
  ```csharp
  private static async Task TryRegisterFontAsync(string family, string appxPath)
  {
      try
      {
          var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(appxPath));
          using var stream = await file.OpenStreamForReadAsync();   // System.IO extension
          SkiaFontRegistrar.RegisterTypefaceFromStream(family, stream);
      }
      catch (Exception ex)
      {
          // Non-fatal — the grid falls back to the system default font.
          System.Diagnostics.Debug.WriteLine($"[KumikoUI] Could not register font '{family}': {ex.Message}");
      }
  }
  ```
- [ ] Register the same families the MAUI sample does:
  ```csharp
  await TryRegisterFontAsync("NotoSansJP",    "ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf");
  await TryRegisterFontAsync("MaterialIcons", "ms-appx:///Assets/Fonts/MaterialIcons-Regular.ttf");
  ```

## 3. When to register (startup)

- [ ] Register **before** the first grid render. In the Uno app, `await` registration early in
  `App.OnLaunched` (before activating the main window), or inside `UseKumikoUI()` startup. Because the
  grid falls back to the system font, an acceptable alternative is fire-and-forget registration followed
  by a `DataGridView.Invalidate()` once complete.
  > Avoid `.GetAwaiter().GetResult()` blocking on Uno startup (unlike MAUI's pre-loop `CreateMauiApp`,
  > Uno has a live dispatcher and could deadlock). Prefer `await`.

## 4. Where the font files live

- [ ] **Consuming app (recommended, matches MAUI sample):** put TTFs under the app's `Assets/Fonts/`
  and mark them as content so they ship with the package:
  ```xml
  <ItemGroup>
    <Content Include="Assets/Fonts/*.ttf" />
  </ItemGroup>
  ```
  Reference with `ms-appx:///Assets/Fonts/<file>.ttf`. This keeps `KumikoUI.Uno` font-agnostic, exactly
  like `KumikoUI.Maui` (the library ships no fonts; the app supplies them).

## 5. Shipping assets *inside* the library (only if needed)

If `KumikoUI.Uno` itself must carry default assets, follow Uno's library-asset rules (Uno 4.6+):

- [ ] Set `<GenerateLibraryLayout>true</GenerateLibraryLayout>` (already added in [02 §5](02-library-scaffold.md#5-library-layout--assets)).
- [ ] Reference library assets via `ms-appx:///KumikoUI.Uno/<asset path>`.
- [ ] **Lower-case the library name on non-WinAppSDK heads:** use `ms-appx:///kumikoui.uno/...` for
  Android/iOS/WASM/Desktop, `ms-appx:///KumikoUI.Uno/...` for the Windows head. Branch on the target.
- [ ] **MSIX caveat:** WinAppSDK does **not** serve library assets in MSIX-packaged mode — use the
  unpackaged deployment mode for the Windows head if you rely on library-shipped assets.

---

## ✅ Exit criteria

- [ ] Japanese text renders correctly (no tofu/garbling) on Android and WASM, not just desktop.
- [ ] Material icon glyphs render in cells that use the `MaterialIcons` family.
- [ ] Removing the registration cleanly falls back to the system font (no crash).
- [ ] `KumikoUI.Uno` ships no fonts unless §5 was deliberately chosen.

➡️ Next: [06 — Sample app](06-sample-app.md)
