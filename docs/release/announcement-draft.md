# KumikoUI 1.0.0 announcement (draft)

**Status:** draft for the maintainer. Nothing here has been posted. Every number comes from [bench/README.md](../../bench/README.md),
[docs/maui-verification.md](../maui-verification.md) or [docs/uno/verification.md](../uno/verification.md); if a number there is
re-measured, change it here too. Placeholders in `<angle brackets>` need filling at release time. Post it only after the
`v1.0.0` tag is pushed and the four packages are live on NuGet.org. The `1.0.0-preview.1` announcement is in the git
history of this file.

Claims deliberately left out, because nothing measured supports them: a frame rate ("60 fps"), "faster than <competitor>",
"production-proven", and any accessibility compliance statement.

---

## Long form (GitHub Discussions "Announcements", a blog post, or the GitHub Release introduction)

### KumikoUI 1.0.0: a canvas-drawn DataGrid for .NET MAUI and Uno

KumikoUI 1.0.0 is out. It is the first stable release of a DataGrid for .NET MAUI and the Uno Platform that draws
everything itself, cells, headers, editors, the filter popup, pickers and the grouping panel, on one SkiaSharp canvas.
The same grid, with the same API, looks and behaves the same on iOS, Android, macOS, Windows, Linux and the browser.

All four packages (KumikoUI.Core, KumikoUI.SkiaSharp, KumikoUI.Maui, KumikoUI.Uno) are at `1.0.0`. The public API is
frozen, and from here on it follows Semantic Versioning: a breaking change waits for 2.0.0.

```shell
dotnet add package KumikoUI.Maui   # .NET MAUI
dotnet add package KumikoUI.Uno    # Uno Platform
```

```xml
<kumiko:KumikoDataGrid ItemsSource="{Binding Employees}">
    <core:KumikoColumn Header="Name" PropertyName="Name" Width="180" />
    <core:KumikoColumn Header="Salary" PropertyName="Salary" ColumnType="Numeric" Format="C0" />
    <core:KumikoColumn Header="Active" PropertyName="IsActive" ColumnType="Boolean" />
</kumiko:KumikoDataGrid>
```

**What it does**

- Text, numeric, boolean, date, combo box, picker, image, progress bar and template columns, with editors, typed
  conversion and validation.
- Sorting (multi-column), Excel-style filtering, nested grouping with a grouping panel, and table, group and caption
  summaries.
- Frozen columns and rows, column resize and reorder, row drag and drop, saved layouts as JSON.
- Item-based selection that survives sorting and filtering, with MVVM bindings; clipboard copy; keyboard navigation that
  never traps focus.
- Light, dark and high-contrast themes that follow the system, every color and size on one style object, and a
  replaceable icon provider.
- Input methods that work: Japanese, Chinese and Korean composition, emoji, dictation and swipe typing go through native
  text-input adapters on iOS, Mac Catalyst, Android, Windows and Uno.
- Native AOT and trimming: source-generated accessors (`[KumikoGridItem]`) or per-column delegates, `IsAotCompatible`
  packages, and the sample published trimmed to WebAssembly in CI.

**What changed since the preview**

- The API is frozen, with the two open decisions taken: 8-digit hex colors stay `#AARRGGBB` and `rgb()`/`rgba()` are
  added, and custom editors have one factory shape (`CellEditorContext`).
- SkiaSharp 4.157.0, which needs Uno.Sdk 7 and .NET MAUI 10.0.110.
- Touch header interaction: a long press on a header now starts a column drag, and narrow headers keep room to reorder.
- The full list, with migration steps, is in the release notes.

**What we measured, and what we did not**

- A steady-state frame of a 30 x 10 visible grid takes 2.9 microseconds in the managed render loop and allocates nothing
  (6.3 microseconds with sorting, filtering, grouping, summaries, a frozen column and the drag handle). That excludes
  SkiaSharp's own drawing time, so we do not quote a frame rate; the grid has a frame-time overlay (`ShowFrameStats`) for
  measuring on your device.
- On 100,000 rows: a sort takes 24 to 173 milliseconds depending on the column type, a text filter 2 milliseconds, grouping
  21 to 42 milliseconds. From 10,000 rows the grid does this on a worker thread and the UI thread pays about a millisecond,
  also for a sort of a million rows. Method, hardware and the budgets we hold ourselves to are in `bench/README.md`.
- The MAUI sample passes a 106-check device suite on an Android emulator, iPhone and iPad simulators and Mac Catalyst.
  The Uno sample was walked on WebAssembly, an iOS simulator, an Android emulator and the desktop head on Linux and
  Windows. Neither host has been run on a physical Android phone or on the Windows App SDK / MAUI Windows heads yet.

**Known limitations**

- The grid is one accessible element; cells are not exposed to screen readers individually (a per-cell accessibility tree
  is planned for 1.x), and right-to-left layout is not mirrored yet (also 1.x).
- On the Uno desktop head, screen-reader support depends on Uno's host: Windows exposes the grid through UI Automation,
  macOS VoiceOver reaches it only on the first page shown, and Linux shows it as a plain panel. These and a macOS
  hands-on pass are tracked for 1.0.1.
- No scrollbars: it scrolls by touch, wheel, trackpad and keyboard, and from code.
- The full list is in the release notes.

**Try it.** `samples/SampleApp.Maui` and `samples/SampleApp.Uno` have nine pages each (every column type, grouping,
100,000 rows, theming, MVVM actions, custom fonts, images, styling and events). The README is the reference, and its C# and
XAML snippets are compiled in CI.

**Feedback.** Open an issue or start a discussion: <link>. Include the package version, host, platform and a minimal
project. Security reports go through GitHub's private vulnerability reporting (`SECURITY.md`).

Thanks to <contributors, if any>.

Release notes: <GitHub Release link> - NuGet: <four package links> - Source: <repo link>

---

## Short form (a social post, about 280 characters)

KumikoUI 1.0.0: a canvas-drawn DataGrid for .NET MAUI and Uno, one SkiaSharp engine on iOS, Android, macOS, Windows,
Linux and the browser. Editing, grouping, filtering, frozen panes, IME-aware text input, an allocation-free render loop.
Stable API, MIT. <link>

## Alternative short form (a forum or r/dotnet post, a paragraph)

I have published KumikoUI 1.0.0, a DataGrid for .NET MAUI and Uno that draws all of its UI (cells, editors, popups) on a
SkiaSharp canvas instead of using native controls. The point is one engine and one look across platforms, and a render
loop that allocates nothing in steady state (2.9 microseconds per 30 x 10 frame in the managed loop on an M4 Max; device
frame rates not quoted, because I have not measured them on enough devices). MAUI is verified on Android, iOS and Mac
Catalyst emulators and simulators; Uno on WebAssembly, iOS and Android emulators and the Linux and Windows desktop.
Windows on MAUI and physical phones are not run yet. The API is now stable under SemVer, and bug reports are welcome. <link>

---

## Where to post (checklist for the maintainer)

- [ ] GitHub Release for the tag (the publish workflow creates it from the CHANGELOG section; paste the long form's
      introduction above the notes if you want one).
- [ ] GitHub Discussions, "Announcements" category.
- [ ] NuGet: nothing to post; the package READMEs are the page.
- [ ] .NET MAUI and Uno Platform community channels (Uno Discord, the .NET Community Toolkit and MAUI GitHub discussions are
      the usual places) - read their self-promotion rules first.
- [ ] A short social post with the sample screenshot (`images/main.png`) or the large-data GIF (`images/large_data.gif`).
- [ ] Before posting: install the published packages (no `--prerelease`) into a fresh MAUI app and a fresh Uno app and
      run the README getting started once (the compiled snippets prove the code compiles; they do not prove the restore
      from nuget.org works).
