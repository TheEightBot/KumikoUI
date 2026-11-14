# KumikoUI 1.0.0-preview.1 announcement (draft)

**Status:** draft for the maintainer. Nothing here has been posted. Every number comes from [bench/README.md](../../bench/README.md),
[docs/maui-verification.md](../maui-verification.md) or [docs/uno/verification.md](../uno/verification.md); if a number there is
re-measured, change it here too. Placeholders in `<angle brackets>` need filling at release time. The release is a
prerelease of all four packages; the text says so and does not announce 1.0.0. When `1.0.0` ships, write a new
announcement rather than editing this one.

Claims deliberately left out, because nothing measured supports them: a frame rate ("60 fps"), "faster than <competitor>",
"production-proven", and any accessibility compliance statement.

---

## Long form (GitHub Discussions "Announcements", a blog post, or the GitHub Release introduction)

### KumikoUI 1.0.0-preview.1: a canvas-drawn DataGrid for .NET MAUI and Uno

KumikoUI 1.0.0-preview.1 is out. It is the first 1.0 prerelease of a DataGrid for .NET MAUI and the Uno Platform that
draws everything itself, cells, headers, editors, the filter popup, pickers and the grouping panel, on one SkiaSharp
canvas. The same grid, with the same API, looks and behaves the same on iOS, Android, macOS, Windows, Linux and the browser.

It is a preview: all four packages (KumikoUI.Core, KumikoUI.SkiaSharp, KumikoUI.Maui, KumikoUI.Uno) are at
`1.0.0-preview.1`, the API has been reviewed for 1.0 but can still change before 1.0.0, and we want your feedback while it can.
NuGet hides prereleases by default, so install with `--prerelease` or an explicit version:

```shell
dotnet add package KumikoUI.Maui --prerelease   # .NET MAUI
dotnet add package KumikoUI.Uno --prerelease    # Uno Platform (the least verified host)
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

**What we measured, and what we did not**

- A steady-state frame of a 30 x 10 visible grid takes 2.9 microseconds in the managed render loop and allocates nothing
  (6.3 microseconds with sorting, filtering, grouping, summaries, a frozen column and the drag handle). That excludes
  SkiaSharp's own drawing time, so we do not quote a frame rate; the grid has a frame-time overlay (`ShowFrameStats`) for
  measuring on your device.
- On 100,000 rows: a sort takes 24 to 173 milliseconds depending on the column type, a text filter 2 milliseconds, grouping
  21 to 42 milliseconds. From 10,000 rows the grid does this on a worker thread and the UI thread pays about a millisecond,
  also for a sort of a million rows. Method, hardware and the budgets we hold ourselves to are in `bench/README.md`.
- The MAUI sample passes a 106-check device suite on an Android emulator, iPhone and iPad simulators and Mac Catalyst. It
  has not been run on Windows or on a physical Android phone yet.

**Why KumikoUI.Uno is the least verified host**

KumikoUI.Uno shares the engine, the API and most of the input code with the MAUI package, and we walked its sample app on
WebAssembly, an iOS simulator and an Android emulator. We did not walk every head end to end: the desktop head has had no
interactive pass (#274), Windows and Linux have not been run, and several input and accessibility problems are open
(#275 to #280). The list is in `docs/uno/verification.md`.

**What a preview means**

- The API may change before 1.0.0; breaking changes are listed in the CHANGELOG. The compatibility promise in
  `docs/VERSIONING.md` starts with 1.0.0.
- Open before 1.0.0: the order of 8-digit hex colors, whether to merge the two custom-editor factory shapes, whether
  `RenderMode = Auto` stays Hardware on Android (to be confirmed on a physical phone), and the HarfBuzz dependency for
  complex scripts. Your feedback on these is useful.
- 1.0.0 also waits for the `PublicAPI.Shipped.txt` freeze, the package validation baseline, and hands-on passes on Windows
  and on physical devices.

**Known limitations**

- The grid is one accessible element; cells are not exposed to screen readers individually (a per-cell accessibility tree
  is planned for 1.x), and right-to-left layout is not mirrored yet (also 1.x).
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

KumikoUI 1.0.0-preview.1: a canvas-drawn DataGrid for .NET MAUI and Uno, one SkiaSharp engine on iOS, Android, macOS,
Windows and the browser. Editing, grouping, filtering, frozen panes, IME-aware text input, an allocation-free render loop. Preview: API feedback welcome. MIT. <link>

## Alternative short form (a forum or r/dotnet post, a paragraph)

I have published the first 1.0 prerelease of KumikoUI, a DataGrid for .NET MAUI and Uno that draws all of its UI (cells,
editors, popups) on a SkiaSharp canvas instead of using native controls. The point is one engine and one look across
platforms, and a render loop that allocates nothing in steady state (2.9 microseconds per 30 x 10 frame in the managed
loop on an M4 Max, device frame rates not quoted, because I have not measured them yet on enough devices). MAUI is
verified on Android, iOS and Mac Catalyst emulators and simulators (Windows is built but not run on a device yet); the Uno
package is the least verified while its desktop head waits for an interactive pass. It is a preview, so the API can still
change before 1.0.0, and feedback and bug reports are welcome. <link>

---

## Where to post (checklist for the maintainer)

- [ ] GitHub Release for the tag (the publish workflow creates it from the CHANGELOG section, marked as a prerelease;
      paste the long form's introduction above the notes if you want one).
- [ ] GitHub Discussions, "Announcements" category (Discussions are enabled per the release-readiness plan).
- [ ] NuGet: nothing to post; the package READMEs are the page.
- [ ] .NET MAUI and Uno Platform community channels (Uno Discord, the .NET Community Toolkit and MAUI GitHub discussions are
      the usual places) - read their self-promotion rules first.
- [ ] A short social post with the sample screenshot (`images/main.png`) or the large-data GIF (`images/large_data.gif`).
- [ ] Before posting: install the published packages (with `--prerelease`) into a fresh MAUI app and a fresh Uno app and
      run the README getting started once (the compiled snippets prove the code compiles; they do not prove the restore
      from nuget.org works).
