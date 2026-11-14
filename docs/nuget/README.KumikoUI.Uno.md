![KumikoUI](https://raw.githubusercontent.com/TheEightBot/KumikoUI/main/images/logo.png)

# KumikoUI.Uno

[![NuGet](https://img.shields.io/nuget/v/KumikoUI.Uno.svg)](https://www.nuget.org/packages/KumikoUI.Uno)
[![Downloads](https://img.shields.io/nuget/dt/KumikoUI.Uno.svg)](https://www.nuget.org/packages/KumikoUI.Uno)
[![License: MIT](https://img.shields.io/github/license/TheEightBot/KumikoUI.svg)](https://github.com/TheEightBot/KumikoUI/blob/main/LICENSE)

KumikoUI.Uno is a high-performance data grid control for the Uno Platform. The whole grid is drawn with SkiaSharp on one canvas, with no native cell controls, using the same engine and features as KumikoUI.Maui: virtual scrolling, sorting, filtering, grouping, summaries, cell editing, frozen columns and rows, row drag and drop, theming and localization.

It targets .NET 10 on Windows (WinUI), desktop (Windows, macOS, Linux), WebAssembly, Android and iOS.

![KumikoUI data grid](https://raw.githubusercontent.com/TheEightBot/KumikoUI/main/images/main.png)

![Dark mode](https://raw.githubusercontent.com/TheEightBot/KumikoUI/main/images/dark_mode.png)

## Which package do I install?

| You are building | Install |
|---|---|
| An Uno Platform app | KumikoUI.Uno (this package) |
| A .NET MAUI app | [KumikoUI.Maui](https://www.nuget.org/packages/KumikoUI.Maui) |
| Your own host, or rendering to an image | [KumikoUI.SkiaSharp](https://www.nuget.org/packages/KumikoUI.SkiaSharp) |
| Only the data model, with your own renderer | [KumikoUI.Core](https://www.nuget.org/packages/KumikoUI.Core) |

Installing KumikoUI.Uno brings in KumikoUI.Core and KumikoUI.SkiaSharp. All four packages share one version.

## Quick start

Install the package in your Uno app project:

```
dotnet add package KumikoUI.Uno
```

The app uses the Skia renderer. In your app's `.csproj`, include `SkiaRenderer` in `UnoFeatures`:

```xml
<UnoFeatures>
  SkiaRenderer;
</UnoFeatures>
```

On the Windows (WinAppSDK) head, also reference `SkiaSharp.Views.WinUI`, which the `SkiaRenderer` feature does not add there. `samples/SampleApp.Uno` shows the full project setup.

Declare the grid and its columns in XAML:

```xml
<Page x:Class="MyApp.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:kumiko="using:KumikoUI.Uno"
      xmlns:core="using:KumikoUI.Core.Models">

    <kumiko:KumikoDataGrid ItemsSource="{Binding People}"
                           RowHeight="36"
                           HeaderHeight="40"
                           AllowSorting="True"
                           AllowFiltering="True">
        <core:KumikoColumn Header="Name" PropertyName="Name" Width="160" />
        <core:KumikoColumn Header="Age" PropertyName="Age" ColumnType="Numeric" Width="80" />
        <core:KumikoColumn Header="Active" PropertyName="IsActive" ColumnType="Boolean" Width="80" />
    </kumiko:KumikoDataGrid>
</Page>
```

Provide the data. Marking the row type with `[KumikoGridItem]` generates reflection-free property access, which Native AOT, WebAssembly and trimmed builds need.

```csharp
using System.Collections.ObjectModel;
using KumikoUI.Core.Data;
using Microsoft.UI.Xaml.Controls;

public sealed partial class MainPage : Page
{
    public ObservableCollection<Person> People { get; } = new()
    {
        new Person { Name = "Ada", Age = 36, IsActive = true },
        new Person { Name = "Grace", Age = 45, IsActive = false },
    };

    public MainPage()
    {
        InitializeComponent();
        DataContext = this;
    }
}

[KumikoGridItem]
public class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public bool IsActive { get; set; }
}
```

`Columns` is the content property of `KumikoDataGrid`, so columns go directly inside the element. To use a custom font, register it before the first grid renders with `KumikoFonts.RegisterFromAppPackageAsync("Family", "ms-appx:///Assets/Fonts/File.ttf")`.

## Documentation

- [Project README](https://github.com/TheEightBot/KumikoUI#readme): column types, editing, validation, theming, fonts and more.
- [Uno sample app](https://github.com/TheEightBot/KumikoUI/tree/main/samples/SampleApp.Uno).
- [Uno user guide](https://github.com/TheEightBot/KumikoUI/blob/main/docs/uno/README.md): setup per head, keyboard and input methods, theming, fonts, images and known limits.
- [Changelog](https://github.com/TheEightBot/KumikoUI/blob/main/CHANGELOG.md) and [versioning policy](https://github.com/TheEightBot/KumikoUI/blob/main/docs/VERSIONING.md).
- [Report an issue](https://github.com/TheEightBot/KumikoUI/issues).

## License

MIT. See [LICENSE](https://github.com/TheEightBot/KumikoUI/blob/main/LICENSE).
