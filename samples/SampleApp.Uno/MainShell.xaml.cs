using System;
using Microsoft.UI.Xaml.Controls;

namespace SampleApp.Uno;

/// <summary>
/// Navigation shell hosting the demo pages — the Uno analogue of SampleApp.Maui's AppShell.
/// A <see cref="NavigationView"/> drives a <see cref="Frame"/>; the selected item's Tag maps to
/// the page type to navigate to.
/// </summary>
public sealed partial class MainShell : Page
{
    public MainShell()
    {
        this.InitializeComponent();

        // Select the first item so the basic grid is shown on launch (this raises
        // SelectionChanged, which performs the initial navigation).
        Loaded += (_, _) => NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        Type? pageType = tag switch
        {
            "BasicGrid" => typeof(BasicGridPage),
            "Grouping" => typeof(GroupingPage),
            "LargeData" => typeof(LargeDataPage),
            "Theming" => typeof(ThemingPage),
            "MvvmActions" => typeof(MvvmActionsPage),
            "CustomFonts" => typeof(CustomFontsPage),
            _ => null,
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }
}
