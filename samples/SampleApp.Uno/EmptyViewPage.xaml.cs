using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SampleApp.Uno;

/// <summary>
/// The empty view (#326, ← SampleApp.Maui EmptyViewPage): the message the grid draws while no rows show. "Clear data"
/// shows the "no items" text; "Filter everything out" shows the "no rows match" text, and the header's filter icon or
/// "Clear filter" brings the rows back. The switches turn the view off and replace its text.
/// </summary>
public sealed partial class EmptyViewPage : Page
{
    private static readonly string[] Names = { "Ada", "Grace", "Linus", "Margaret", "Alan", "Barbara" };
    private static readonly string[] Cities = { "Oslo", "Rome", "Lima", "Kyiv" };

    public EmptyViewPage()
    {
        this.InitializeComponent();
        Load();
    }

    private void Load()
    {
        kumiko.ItemsSource = Enumerable.Range(1, 50).Select(i => new Person
        {
            Name = $"{Names[i % Names.Length]} {i}",
            City = Cities[i % Cities.Length],
            Age = 20 + i % 40,
        }).ToList();
        UpdateStatus();
    }

    private void UpdateStatus() =>
        statusLabel.Text = $"{kumiko.DataSource.RowCount} rows shown of {kumiko.DataSource.ItemCount}";

    private void OnLoadClicked(object sender, RoutedEventArgs e) => Load();

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        kumiko.ItemsSource = new List<Person>();
        UpdateStatus();
    }

    private void OnFilterOutClicked(object sender, RoutedEventArgs e)
    {
        kumiko.DataSource.SetFilter(_ => false);
        UpdateStatus();
    }

    private void OnClearFilterClicked(object sender, RoutedEventArgs e)
    {
        kumiko.DataSource.ClearAllFilters();
        UpdateStatus();
    }

    private void OnShowToggled(object sender, RoutedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.ShowEmptyView = showSwitch.IsOn;
    }

    private void OnCustomTextToggled(object sender, RoutedEventArgs e)
    {
        if (kumiko is null) return; // raised while the page is created, before the grid exists
        kumiko.EmptyViewText = customTextSwitch.IsOn ? "Nothing to see here" : null;
    }

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public int Age { get; set; }
    }
}
