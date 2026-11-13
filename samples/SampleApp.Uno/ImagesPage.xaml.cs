using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SampleApp.Uno;

/// <summary>
/// Image columns (#63, ← SampleApp.Maui ImagesPage): one column bound to app package paths, one to
/// <see cref="BitmapImage"/>s of the same files and one to web URLs, over 10,000 rows. The grid's <c>ImageCache</c>
/// loads and decodes them off the UI thread and keeps a bounded number; the label shows how many it holds while you
/// scroll.
/// </summary>
public sealed partial class ImagesPage : Page
{
    private const int RowCount = 10_000;
    private const int AvatarCount = 8;

    // picsum.photos serves stable images by id; ids 0-84 all exist. 85 distinct URLs repeat down the rows.
    private const int PhotoCount = 85;

    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Brown", "Garcia", "Miller", "Davis", "Wilson", "Taylor" };

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ImagesPage()
    {
        this.InitializeComponent();
        kumiko.ItemsSource = CreateRows();
        _timer.Tick += (_, _) => UpdateCacheLabel();
        Loaded += (_, _) =>
        {
            UpdateCacheLabel();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private void UpdateCacheLabel()
    {
        var cache = kumiko.ImageCache;
        cacheLabel.Text = $"Image cache: {cache.Count} of at most {cache.MaxCount} images, {cache.Bytes / 1024.0:N0} KB decoded";
    }

    private static List<ImageRow> CreateRows()
    {
        // One BitmapImage per asset, shared by the rows: the cache keys it by its URI.
        var images = Enumerable.Range(1, AvatarCount)
            .Select(n => new BitmapImage(new Uri($"ms-appx:///Assets/Avatars/avatar-{n}.png")))
            .ToArray();

        var rows = new List<ImageRow>(RowCount);
        for (int i = 0; i < RowCount; i++)
        {
            int avatar = i % AvatarCount;
            rows.Add(new ImageRow
            {
                Id = i + 1,
                Name = $"{FirstNames[i % FirstNames.Length]} {LastNames[i / FirstNames.Length % LastNames.Length]}",
                Avatar = $"Assets/Avatars/avatar-{avatar + 1}.png",
                AvatarImage = images[(avatar + 3) % AvatarCount],
                PhotoUrl = $"https://picsum.photos/id/{i % PhotoCount}/64/64",
            });
        }
        return rows;
    }
}

/// <summary>A row of the images sample.</summary>
[KumikoUI.Core.Data.KumikoGridItem]
public class ImageRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public BitmapImage? AvatarImage { get; set; }
    public string PhotoUrl { get; set; } = "";
}
