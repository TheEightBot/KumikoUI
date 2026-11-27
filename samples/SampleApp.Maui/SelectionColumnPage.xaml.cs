using KumikoUI.Core.Models;

namespace SampleApp.Maui;

/// <summary>
/// The selection column (#321): a column of row check boxes with a tri-state check-all box in its header, the
/// touch way to select several rows. The page turns the column and selecting by tapping a row on and off, and
/// switches the selection mode.
/// </summary>
public partial class SelectionColumnPage : ContentPage
{
    private const int RowCount = 200;

    private static readonly string[] Titles = { "Review pull request", "Update the docs", "Fix flaky test", "Plan the release", "Triage issues", "Write the changelog" };
    private static readonly string[] Owners = { "Ada", "Grace", "Linus", "Margaret", "Alan" };
    private static readonly string[] Statuses = { "Open", "In progress", "Done" };

    public SelectionColumnPage()
    {
        InitializeComponent();
        kumiko.ItemsSource = Enumerable.Range(1, RowCount).Select(i => new TaskItem
        {
            Title = $"{Titles[i % Titles.Length]} #{i}",
            Owner = Owners[i % Owners.Length],
            Status = Statuses[i % Statuses.Length],
            Hours = 1 + i % 8,
        }).ToList();
        kumiko.SelectionChanged += (_, _) => UpdateStatus();
        UpdateStatus();
    }

    private void UpdateStatus() =>
        statusLabel.Text = $"{kumiko.SelectedItems?.Count ?? 0} of {RowCount} selected";

    private void OnColumnChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.ShowSelectionColumn = e.Value;
    }

    private void OnRowClickChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.SelectOnRowClick = e.Value;
    }

    private void OnModeClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string mode })
            kumiko.GridSelectionMode = Enum.Parse<KumikoSelectionMode>(mode);
        UpdateStatus();
    }

    private sealed class TaskItem
    {
        public string Title { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Status { get; set; } = "";
        public int Hours { get; set; }
    }
}
