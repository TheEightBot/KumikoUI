# KumikoUI 1.0 announcement (draft)

**Status:** draft for the maintainer. Nothing here has been posted. Every number comes from [bench/README.md](../../bench/README.md),
[docs/maui-verification.md](../maui-verification.md) or [docs/uno/verification.md](../uno/verification.md); if a number there is
re-measured, change it here too. Placeholders in `<angle brackets>` need filling at release time. Text marked
**[Uno decision]** assumes KumikoUI.Uno ships as a preview (the recommendation); see
[1.0-checklist-status.md](1.0-checklist-status.md).

Claims deliberately left out, because nothing measured supports them: a frame rate ("60 fps"), "faster than <competitor>",
"production-proven", and any accessibility compliance statement.

---

## Long form (GitHub Discussions "Announcements", a blog post, or the GitHub Release introduction)

### KumikoUI 1.0: a canvas-drawn DataGrid for .NET MAUI and Uno

KumikoUI 1.0 is out. It is a DataGrid for .NET MAUI and the Uno Platform that draws everything itself, cells, headers,
editors, the filter popup, pickers and the grouping panel, on one SkiaSharp canvas. The same grid, with the same API, looks
and behaves the same on iOS, Android, macOS, Windows, Linux and the browser.

```shell
dotnet add package KumikoUI.Maui   # .NET MAUI
dotnet add package KumikoUI.Uno    # Uno Platform (preview)
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

**[Uno decision] Why Uno is a preview**

KumikoUI.Uno shares the engine, the API and most of the input code with the MAUI package, and we walked its sample app on
WebAssembly, an iOS simulator and an Android emulator. We did not walk every head end to end: the desktop head has had no
interactive pass, Windows and Linux have not been run, and two input problems on the mobile heads are open (typing over a
selected value on iOS, and the filter search box on Android). We would rather label it a preview than promise 1.0
stability for heads we have not exercised. The list is in `docs/uno/verification.md`, and the package moves to 1.0 when it
is closed.

**Known limitations at 1.0**

- The grid is one accessible element; cells are not exposed to screen readers individually (a per-cell accessibility tree
  is planned for 1.x), and right-to-left layout is not mirrored yet (also 1.x).
- No scrollbars: it scrolls by touch, wheel, trackpad and keyboard, and from code.
- The full list is in the release notes.

**API stability.** The public API of the four packages is frozen at 1.0. From here, breaking changes need a major version,
and removals go through `[Obsolete]` for at least one minor release. The policy is in `docs/VERSIONING.md`.

**Try it.** `samples/SampleApp.Maui` and `samples/SampleApp.Uno` have nine pages each (every column type, grouping,
100,000 rows, theming, MVVM actions, custom fonts, images, styling and events). The README is the reference, and its C# and
XAML snippets are compiled in CI.

**Feedback.** Open an issue or start a discussion: <link>. Security reports go through GitHub's private vulnerability
reporting (`SECURITY.md`).

Thanks to <contributors, if any>.

Release notes: <GitHub Release link> - NuGet: <four package links> - Source: <repo link>

---

## Short form (a social post, about 280 characters)

KumikoUI 1.0: a canvas-drawn DataGrid for .NET MAUI and Uno. One SkiaSharp engine, the same grid on iOS, Android, macOS,
Windows and the browser. Editing, grouping, filtering, frozen panes, IME-aware text input, an allocation-free render loop. MIT. <link>

## Alternative short form (a forum or r/dotnet post, a paragraph)

I have released 1.0 of KumikoUI, a DataGrid for .NET MAUI and Uno that draws all of its UI (cells, editors, popups) on a
SkiaSharp canvas instead of using native controls. The point is one engine and one look across platforms, and a render loop
that allocates nothing in steady state (2.9 microseconds per 30 x 10 frame in the managed loop on an M4 Max, device frame
rates not quoted, because I have not measured them yet on enough devices). MAUI is verified on Android, iOS and Mac Catalyst
(Windows is built but not run on a device yet); the Uno package is a preview while its desktop head gets an interactive
pass. Feedback and bug reports are welcome. <link>

---

## Where to post (checklist for the maintainer)

- [ ] GitHub Release for the tag (the publish workflow creates it from the CHANGELOG section; paste the long form's
      introduction above the notes if you want one).
- [ ] GitHub Discussions, "Announcements" category (Discussions are enabled per the release-readiness plan).
- [ ] NuGet: nothing to post; the package READMEs are the page.
- [ ] .NET MAUI and Uno Platform community channels (Uno Discord, the .NET Community Toolkit and MAUI GitHub discussions are
      the usual places) - read their self-promotion rules first.
- [ ] A short social post with the sample screenshot (`images/main.png`) or the large-data GIF (`images/large_data.gif`).
- [ ] Before posting: install the published packages into a fresh MAUI app and a fresh Uno app and run the README getting
      started once (the compiled snippets prove the code compiles; they do not prove the restore from nuget.org works).
