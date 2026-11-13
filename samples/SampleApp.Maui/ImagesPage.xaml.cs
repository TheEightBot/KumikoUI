namespace SampleApp.Maui;

/// <summary>
/// Image columns (#63): one column bound to bundled asset file names, one to <see cref="ImageSource"/>s of the same
/// files and one to web URLs, over 10,000 rows. The grid's <c>ImageCache</c> loads and decodes them off the UI
/// thread and keeps a bounded number; the label shows how many it holds while you scroll.
/// </summary>
public partial class ImagesPage : ContentPage
{
    private const int RowCount = 10_000;
    private const int AvatarCount = 8;

    // picsum.photos serves stable images by id; ids 0-84 all exist. 85 distinct URLs repeat down the rows.
    private const int PhotoCount = 85;

    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Brown", "Garcia", "Miller", "Davis", "Wilson", "Taylor" };

    private IDispatcherTimer? _timer;

    public ImagesPage()
    {
        InitializeComponent();
        kumiko.ItemsSource = CreateRows();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _timer ??= CreateTimer();
        _timer.Start();
        UpdateCacheLabel();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        base.OnDisappearing();
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => UpdateCacheLabel();
        return timer;
    }

    private void UpdateCacheLabel()
    {
        var cache = kumiko.ImageCache;
        cacheLabel.Text = $"Image cache: {cache.Count} of at most {cache.MaxCount} images, {cache.Bytes / 1024.0:N0} KB decoded";
    }

    private static List<ImageRow> CreateRows()
    {
        // One ImageSource per asset, shared by the rows: the cache keys it by file name either way.
        var sources = Enumerable.Range(1, AvatarCount)
            .Select(n => ImageSource.FromFile($"Avatars/avatar-{n}.png"))
            .ToArray();

        var rows = new List<ImageRow>(RowCount);
        for (int i = 0; i < RowCount; i++)
        {
            int avatar = i % AvatarCount;
            rows.Add(new ImageRow
            {
                Id = i + 1,
                Name = $"{FirstNames[i % FirstNames.Length]} {LastNames[i / FirstNames.Length % LastNames.Length]}",
                Avatar = $"Avatars/avatar-{avatar + 1}.png",
                AvatarSource = sources[(avatar + 3) % AvatarCount],
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
    public ImageSource? AvatarSource { get; set; }
    public string PhotoUrl { get; set; } = "";
}
