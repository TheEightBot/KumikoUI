# KumikoUI on the Uno Platform: user guide

`KumikoUI.Uno` is the Uno Platform host of the KumikoUI data grid. `KumikoDataGrid` (namespace `KumikoUI.Uno`) is a
`UserControl` that draws the whole grid, cells, headers, editors and popups, on one SkiaSharp surface. It shares its
engine (`KumikoUI.Core`) and drawing code (`KumikoUI.SkiaSharp`) with the MAUI host, so the columns, models, styles,
sorting, filtering, grouping, summaries and editing described in the [project README](../../README.md) are the same
types and the same behavior on both. This guide covers what is specific to Uno: setting up each head, usage, input,
theming, fonts, images and the limits that apply today.

For the grid's features (column types, editing, validation, selection, summaries, theming reference, localization)
read the [README](../../README.md); this guide does not repeat them. For a working app, see
[`samples/SampleApp.Uno`](../../samples/SampleApp.Uno) (`samples/README.md` has the run commands).

> The numbered notes in [`plan/`](plan/README.md) are the design record the host was built from. They are history,
> not documentation.

## Contents

- [Supported heads](#supported-heads)
- [Set up your app](#set-up-your-app)
- [Use the grid](#use-the-grid)
- [Render surface](#render-surface)
- [Keyboard, input methods and focus](#keyboard-input-methods-and-focus)
- [Pointer, touch and scrolling](#pointer-touch-and-scrolling)
- [Theming](#theming)
- [Fonts](#fonts)
- [Images](#images)
- [Platform notes](#platform-notes)
- [Known limits](#known-limits)

---

## Supported heads

`KumikoUI.Uno` targets .NET 10 and SkiaSharp 4.153.1. Every Uno head of the library uses the Skia renderer.

| Head | Target framework | App build host | How it is verified |
|---|---|---|---|
| Desktop (Windows, macOS, Linux) | `net10.0-desktop` | any OS | CI build and unit tests; the sample runs on macOS |
| WebAssembly | `net10.0-browserwasm` | any OS (`wasm-tools` workload) | CI build |
| iOS | `net10.0-ios` | macOS with Xcode | CI build |
| Android | `net10.0-android` | any OS (`android` workload) | CI build |
| Windows App SDK | `net10.0-windows10.0.26100` | Windows | CI build |

"CI build" means the head is compiled and packed in CI; it does not mean the head was exercised by hand. Interactive
verification on devices and in browsers is recorded per head in [verification.md](verification.md) (#67). There is no Mac Catalyst head: Uno serves macOS through the
desktop head. Minimum OS versions are in the README's "Supported platforms" table.

---

## Set up your app

1. Add the package. It brings `KumikoUI.Core` and `KumikoUI.SkiaSharp` with it:

   ```shell
   dotnet add package KumikoUI.Uno
   ```

2. Use the Skia renderer, and make Uno use the SkiaSharp major KumikoUI is built with. Uno.Sdk defaults to SkiaSharp
   3.x; an app must resolve one SkiaSharp major, so set `SkiaSharpVersion` to the version `KumikoUI.Uno` depends on:

   ```xml
   <PropertyGroup>
     <SkiaSharpVersion>4.153.1</SkiaSharpVersion>
     <UnoFeatures>
       SkiaRenderer;
     </UnoFeatures>
   </PropertyGroup>
   ```

3. On the Windows App SDK head only, reference the WinUI SkiaSharp views, which the `SkiaRenderer` feature does not add:

   ```xml
   <ItemGroup Condition="$(TargetFramework.Contains('-windows'))">
     <PackageReference Include="SkiaSharp.Views.WinUI" Version="4.153.1" />
   </ItemGroup>
   ```

4. If your row types are reflected at run time, nothing more is needed on desktop, Windows, Android and iOS. For
   WebAssembly, Native AOT and trimmed builds, mark the row types with `[KumikoGridItem]` (from `KumikoUI.Core.Data`) so
   the grid reads properties through generated accessors instead of reflection; the `KumikoUI.Core` package carries the
   generator. See "Native AOT and Trimming" in the README.

Unlike MAUI, Uno needs no startup call: there is no `UseKumikoUI()` on the Uno host. Register fonts in `App.OnLaunched`
(see [Fonts](#fonts)).

### Per head

| Head | What to know |
|---|---|
| Desktop | Nothing beyond the steps above. `dotnet run --project <app> -f net10.0-desktop`. |
| WebAssembly | Install the workload (`dotnet workload install wasm-tools`). SkiaSharp 4.153.1's WebAssembly native assets need a link-step workaround, see [Known limits](#known-limits). The grid keeps a hidden `TextBox` for keyboard input, which is what brings up the keyboard on a phone browser. |
| iOS | Build on macOS with Xcode. The soft keyboard appears only while a cell editor or the filter popup's search box is open. |
| Android | Set the activity's `WindowSoftInputMode` to `AdjustResize` so the window shrinks for the keyboard; the sample does. With `AdjustNothing` the grid still scrolls the edited cell above the keyboard (see [Keyboard](#keyboard-input-methods-and-focus)). |
| Windows App SDK | Reference `SkiaSharp.Views.WinUI` (step 3). The grid uses the software surface here. |

---

## Use the grid

Declare the grid in XAML. `Columns` is the content property, so columns go straight inside the element:

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:kumiko="using:KumikoUI.Uno"
      xmlns:core="using:KumikoUI.Core.Models">

    <kumiko:KumikoDataGrid x:Name="kumiko"
                           ItemsSource="{Binding Employees}"
                           RowHeight="36"
                           HeaderHeight="40"
                           AllowSorting="True"
                           AllowFiltering="True">
        <core:KumikoColumn Header="Name" PropertyName="Name" Width="180" />
        <core:KumikoColumn Header="Age" PropertyName="Age" Width="80" ColumnType="Numeric" TextAlignment="Right" />
        <core:KumikoColumn Header="Active" PropertyName="IsActive" Width="80" ColumnType="Boolean" />
    </kumiko:KumikoDataGrid>
</Page>
```

Everything the README documents for `KumikoDataGrid` applies: the properties, commands and events are the same on both
hosts (`DependencyProperty` here, `BindableProperty` on MAUI), so "Host Properties, Commands & Events" is the
reference. What differs on Uno:

| | MAUI | Uno |
|---|---|---|
| Base class | `ContentView` | `UserControl` |
| Namespace in XAML | `clr-namespace:KumikoUI.Maui;assembly=KumikoUI.Maui` | `using:KumikoUI.Uno` |
| Startup | `builder.UseKumikoUI()` | none |
| `RowHeight`, `HeaderHeight` | `float` | `double` |
| Render modes | `Auto`, `Software`, `Hardware` | `Auto`, `Software`, `Hardware` |
| Fonts | `ConfigureFonts` fonts reach the grid; `KumikoFonts` | `KumikoFonts` (`KumikoUI.Uno`) |
| Action buttons from code | `MauiActionButtonDefinition` | `UnoActionButtonDefinition` |

The grid does not use templated parts or visual states, and it has no layout API of its own: its content (the surface
and the keyboard proxy) is private, and replacing `Content` is not supported.

The grid belongs to the UI thread, like every WinUI control. Change the items from any thread only through an
`ObservableCollection` raised on the UI thread; see "Live Data" and "Threading" in the README. Sorting, filtering,
grouping and summaries of large sources (10,000 rows or more by default, `BackgroundRebuildThreshold`) are rebuilt on a
worker; `ViewUpdated` on `DataSource` tells you when the new view is in place.

---

## Render surface

`RenderMode` chooses what the grid draws on, and `ActiveRenderMode` reports what it uses:

| `RenderMode` | Surface | Notes |
|---|---|---|
| `Auto` (default) | `Hardware` where available, else `Software` | |
| `Hardware` | Uno's `SKCanvasElement`: draws straight into the window's canvas, GPU-accelerated where Uno renders on the GPU | Skia-rendered heads: desktop, WebAssembly, iOS and Android. Falls back to `Software` elsewhere. |
| `Software` | `SKXamlCanvas`: a device-pixel bitmap copied to the screen | Works on every head; the only surface on the Windows App SDK head. |

Switching at run time removes the old surface and redraws on the new one. If a device draws the grid wrongly,
set `RenderMode="Software"`.

Every redraw is coalesced to one paint per frame. The surface paints through `SkiaResourceCache.CreateContext`, so a
frame allocates no managed memory once warm. A fling, a row drag's edge autoscroll and an editor animation step from
`CompositionTarget.Rendering`, which the grid subscribes only while there is such work. While an editor or the filter
popup's search box has focus, the caret redraws only at its blink transitions. A change of display scale (`XamlRoot`
rasterization scale) redraws the grid. `ShowFrameStats="True"` draws a frame-time overlay. How a frame is built is in
[docs/RENDERING.md](../RENDERING.md).

---

## Keyboard, input methods and focus

The grid keeps a hidden `TextBox` as its keyboard target: on iOS, Android and in the browser, focusing a non-text element
does not summon the on-screen keyboard, and a real text box is the one element every head routes the keyboard and the
input method to. The `TextBox` mirrors the open editor's text and selection and is empty when no editor is open. It is
invisible, takes no pointer input and is kept out of assistive technology.

- **Text and input methods.** The adapter (`KumikoTextInputAdapter`) replays what the keyboard changes on the editor's
  `ITextInputSession`, so typing, backspace, selection, undo and redo behave as in the MAUI host.
  Input-method composition (Japanese, Chinese, Korean, dead keys) reaches the editor through the text box's composition
  events. See [docs/TEXT-INPUT.md](../TEXT-INPUT.md).
- **Keys.** Which keys do what is in the README's "Keyboard" section. On Uno every head gets characters, arrows, Home,
  End, Page Up and Down, Tab, Escape, F2, Delete and the editing shortcuts; Command acts as Control on macOS, iOS and
  WebAssembly, so Cmd+A, C, X, V, Z work there. Typing on a cell opens an editor when the column's `EditTriggers` allow it.
- **Focus and Tab.** The grid is a tab stop (`IsTabStop` is on unless you turn it off) and never traps focus: Tab past the
  last tab stop, Shift+Tab past the first, and Escape with nothing to cancel are left to the page, so focus moves on. A
  `TabIndex` or `IsTabStop="False"` you set is respected. The grid draws its own focus (the current cell), so
  `UseSystemFocusVisuals` is off.
- **Clipboard.** Ctrl/Cmd+C copies the selected rows as tab-separated text, and copy, cut and paste work inside editors.
  The `Clipboard` property replaces the platform clipboard (for tests or a custom target).
- **The soft keyboard.** While an editor is open on iOS, Android and (where the browser reports a keyboard) WebAssembly, the
  grid follows `InputPane.Showing` and `Hiding`, scrolls the edited cell above the keyboard and gives the scroll range a
  bottom inset (`ScrollState.BottomInset`) so the last rows can scroll clear of it. Where the platform already resizes
  the window, nothing is covered and nothing changes. Desktop and Windows have no input pane.

---

## Pointer, touch and scrolling

The grid takes mouse, touch and pen through WinUI pointer events; coordinates are in the same logical units the grid
draws in, so hit testing lines up at every display scale. Double click is 400 ms and 20 px, long press 500 ms and 15 px
(`docs/INTERACTION.md` lists every threshold). Scrolling uses the mouse wheel, the trackpad and touch panning with
inertia; `ScrollSettings` tunes it, and `KumikoMomentumPreset` picks the feel (the grid chooses a preset for the head).
A long press on touch starts a row drag when dragging is on, or opens a context menu or an editor.

`EnableHaptics="True"` adds a short vibration for picking up or dropping a row or column and for a long press, on iOS
(`UIImpactFeedbackGenerator`) and Android (`PerformHapticFeedback`); the other heads have none to give. It is off by
default.

Hover feedback (resize and hand cursors) shows where the platform supports a cursor.

---

## Theming

By default the grid follows the app's theme: with `FollowSystemTheme` on (the default), a dark app gets a dark grid with no
code, from `ActualTheme` (the application theme, or a `RequestedTheme` set on an ancestor).

```xml
<kumiko:KumikoDataGrid Theme="Dark" ... />                <!-- Light, Dark or HighContrast; turns FollowSystemTheme off -->
<kumiko:KumikoDataGrid FollowSystemTheme="False" ... />   <!-- keep the style you set, whatever the app theme -->
```

Precedence, highest first: the style-backed properties you set on the control (`RowHeight`, `HeaderHeight`,
`ShowHorizontalGridLines`, `ShowVerticalGridLines`, `AlternateRowBackground`, `AllowRowDragDrop`, `ShowRowDragHandle`),
then the last of `Theme` and `GridStyle` you assigned, then the app theme while `FollowSystemTheme` is on. Assigning
`Theme` or `GridStyle` turns following off unless you set `FollowSystemTheme` explicitly. The README's "Theming and
Styling" section covers `KumikoStyle`, per-cell and per-row styles and the icon providers.

`HighContrast` is a preset: the grid does not switch to it when the operating system's high-contrast mode is on, and its
font sizes do not follow the system text-size setting.

---

## Fonts

The grid draws text with SkiaSharp, not with WinUI text, so a font must be registered with `SkiaFontRegistrar` before a
`KumikoFont` can use it by name. On Uno that is `KumikoFonts` (`KumikoUI.Uno`). The library ships no fonts; the app
supplies them.

1. Put the font files in the app project as `Content` (for example `Assets/Fonts/NotoSansJP-Regular.ttf`).
2. Register them in `App.OnLaunched`, before the window is activated:

   ```csharp
   protected override async void OnLaunched(LaunchActivatedEventArgs args)
   {
       await KumikoFonts.RegisterFromAppPackageAsync(new[]
       {
           ("NotoSansJP", "ms-appx:///Assets/Fonts/NotoSansJP-Regular.ttf"),
           ("MaterialIcons", "ms-appx:///Assets/Fonts/MaterialIcons-Regular.ttf"),
       });

       // ... create and activate the window
   }
   ```

3. Use the family name in a `KumikoFont`, a column's `CellStyle` or `KumikoStyle`.

`RegisterFromAppPackageAsync` never throws for a font problem: a missing or unreadable font is logged with
`Debug.WriteLine` and the grid uses the default font. `KumikoFonts.RegisterFromStream(family, stream)` registers a stream
you opened yourself and does throw on an unreadable font. Text a font lacks (CJK, emoji, icons) falls back through
`SkiaFontRegistrar.FallbackFamilies` and the system font manager; emoji sequences are shaped with HarfBuzz. See "Custom
Fonts" in the README and "Font fallback" in [docs/RENDERING.md](../RENDERING.md). The sample's `CustomFontsPage` shows
Japanese headers and an icon column.

---

## Images

Image columns (`ColumnType="Image"`) load and decode off the UI thread through `UnoImageLoader`, in front of the shared
loader, into the grid's bounded `ImageCache`, so a large grid does not decode every picture. A cell value can be:

- an `ms-appx:///` or `ms-appdata:///` URI or string: a file of the app package or app data;
- a relative path or relative `Uri`: an app package file (`Assets/Photos/a.png` is `ms-appx:///Assets/Photos/a.png`);
- a `http`, `https`, `file` or `data` URL, an absolute path, `byte[]`, `Stream`, `SKImage` or `SKBitmap`, as the README's
  table lists;
- a `BitmapImage` with a `UriSource`: loaded like that URI, so it shares the cached image of the same URL.

Other image sources (a `WriteableBitmap`, an `SvgImageSource`) are not loaded; the grid reports each kind once and draws
nothing. Package images as `Content`. The README's "Images" section covers the cache limits and custom loaders
(`IKumikoImageLoader`), and the sample's `ImagesPage` shows 10,000 rows of them.

---

## Platform notes

- **Desktop (Skia).** On macOS the grid sees Command as the shortcut key. Resize and hand cursors work. There is no
  on-screen keyboard.
- **WebAssembly.** The browser owns the keyboard and the clipboard: paste arrives through the text box. `KeyDown` and
  `CharacterReceived` do not fire on a non-text element on WASM, which is why the text box exists. A trimmed or AOT build
  needs `[KumikoGridItem]` row types. The wheel reports pixels or lines depending on the browser, and the grid normalizes
  both. Touch gets the touch momentum preset when the main pointer is coarse.
- **iOS and Android.** The soft keyboard, haptics and keyboard avoidance apply. With a Bluetooth keyboard, keys reach the
  grid through the same text box.
- **Windows App SDK.** The software surface is used (`SKXamlCanvas`).
- **Accessibility.** The grid has a UI Automation peer with the DataGrid control type. Its name comes from
  `GridDescription` (default: `KumikoStrings.AccessibleGridName`) and its help text from `GridHint` or a computed
  description of the rows and columns. With `AnnounceChanges` on (the default) it announces the current cell, sort
  changes, edit start and end, and validation errors to a running screen reader.

---

## Known limits

These hold today. The 1.0 plan is in [docs/ROADMAP.md](../ROADMAP.md).

- **Not every head has been exercised by hand.** CI compiles and packs all five heads and the unit tests run the
  platform-independent code on the desktop head, but interactive verification on devices and in browsers is still open
  (#67). Treat a head as "builds" until that pass reports on it.
- **One element for screen readers.** Cells, headers, editors and popups are not exposed individually, so a screen reader
  cannot read or navigate cell contents. A per-cell accessibility tree is planned for 1.x.
- **Right to left is not supported.** The grid always lays out and draws left to right, also inside a right-to-left page,
  and Hebrew or Arabic text is drawn without bidi reordering (#80).
- **No Mac Catalyst head.** Use the desktop head on macOS.
- **Only the listed image sources load**, see [Images](#images).
- **Haptics only on iOS and Android**, and only when `EnableHaptics` is on.
- **High-contrast and text-size settings of the operating system** are not followed.
- **Fonts must be registered before the first grid renders.** Register them in `App.OnLaunched`.
- **SkiaSharp 4.153.1 on WebAssembly** needs a link-step workaround in the app project (#184); the comment in
  `samples/SampleApp.Uno/SampleApp.Uno.csproj` explains it, and SkiaSharp fixes it upstream from 4.156.

Report a problem with the package, version, head and a short reproduction through the
[issue forms](https://github.com/TheEightBot/KumikoUI/issues/new/choose).
