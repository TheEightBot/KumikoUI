using System;
using System.Linq;
using KumikoUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SampleApp.Uno;

/// <summary>
/// The selection column (#321, ← SampleApp.Maui SelectionColumnPage): a column of row check boxes with a tri-state
/// check-all box in its header, the touch way to select several rows. The page turns the column and selecting by
/// tapping a row on and off, and switches the selection mode.
/// </summary>
public sealed partial class SelectionColumnPage : Page
{
    private const int RowCount = 200;

    private static readonly string[] Titles = { "Review pull request", "Update the docs", "Fix flaky test", "Plan the release", "Triage issues", "Write the changelog" };
    private static readonly string[] Owners = { "Ada", "Grace", "Linus", "Margaret", "Alan" };
    private static readonly string[] Statuses = { "Open", "In progress", "Done" };

    public SelectionColumnPage()
    {
        this.InitializeComponent();
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

    private void OnColumnToggled(object sender, RoutedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.ShowSelectionColumn = columnSwitch.IsOn;
    }

    private void OnRowClickToggled(object sender, RoutedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.SelectOnRowClick = rowClickSwitch.IsOn;
    }

    private void OnModeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string mode })
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
