using KumikoUI.Core.Models;

namespace SampleApp.Maui;

/// <summary>
/// Variable row height (#320): 100,000 rows with two wrapping columns in <see cref="KumikoRowHeightMode.Auto"/> mode,
/// with a maximum height, a resolver for starred rows and a jump far into rows that were never drawn.
/// </summary>
public partial class WrappedTextPage : ContentPage
{
    private const int RowCount = 100_000;

    private static readonly string[] Words =
    {
        "invoice", "follow", "up", "with", "the", "customer", "about", "delivery", "window", "and", "confirm", "pricing",
        "for", "next", "quarter", "warehouse", "reported", "a", "delay", "on", "two", "pallets", "call", "back",
    };

    private static readonly string[] Streets = { "Main Street", "Harbor Road", "Elm Avenue", "Station Square", "Old Mill Lane" };
    private static readonly string[] Cities = { "Springfield", "Rivertown", "Lakeside", "Northbrook", "Fairview" };

    private static readonly KumikoRowHeightResolver StarredResolver =
        (in KumikoRowHeightContext context) => ((WrappedNote)context.Item).Starred ? 64 : null;

    public WrappedTextPage()
    {
        InitializeComponent();
        kumiko.ItemsSource = CreateNotes(RowCount);
        statusLabel.Text = $"{RowCount:N0} rows";
    }

    private void OnAutoChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.RowHeightMode = e.Value ? KumikoRowHeightMode.Auto : KumikoRowHeightMode.Fixed;
    }

    private void OnMaxChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.MaxRowHeight = e.Value ? 90 : double.NaN;
    }

    private void OnPinnedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.RowHeightResolver = e.Value ? StarredResolver : null;
    }

    private void OnFrameStatsChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.ShowFrameStats = e.Value;
    }

    private void OnJumpClicked(object? sender, EventArgs e) =>
        kumiko.ScrollToRow(75_000, KumikoScrollPosition.Start);

    private static List<WrappedNote> CreateNotes(int count)
    {
        var random = new Random(320);
        var notes = new List<WrappedNote>(count);
        for (int i = 1; i <= count; i++)
        {
            int words = 2 + random.Next(i % 7 == 0 ? 60 : 18);
            var text = string.Join(' ', Enumerable.Range(0, words).Select(_ => Words[random.Next(Words.Length)]));
            notes.Add(new WrappedNote
            {
                Id = i,
                Title = $"Ticket {i:N0}",
                Notes = char.ToUpperInvariant(text[0]) + text[1..] + ".",
                Address = $"{1 + random.Next(999)} {Streets[random.Next(Streets.Length)]}, {Cities[random.Next(Cities.Length)]}",
                Starred = i % 11 == 0,
            });
        }
        return notes;
    }
}

/// <summary>A row of the wrapped text sample.</summary>
[KumikoUI.Core.Data.KumikoGridItem]
public sealed class WrappedNote
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Starred { get; set; }
}
