using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;

namespace SampleApp.Uno;

/// <summary>
/// Navigation shell hosting the demo pages — the Uno analogue of SampleApp.Maui's AppShell.
/// A <see cref="NavigationView"/> drives a <see cref="Frame"/>; the selected item's Tag maps to
/// the page type to navigate to.
/// </summary>
public sealed partial class MainShell : Page
{
    private bool _initialNavigationDone;

    public MainShell()
    {
        this.InitializeComponent();

        // Select the first item so the basic grid is shown on launch (this raises
        // SelectionChanged, which performs the initial navigation). Loaded can fire again (for example when the
        // shell leaves and rejoins the visual tree), so the initial selection happens once.
        Loaded += (_, _) =>
        {
            if (_initialNavigationDone) return;
            _initialNavigationDone = true;
            NavView.SelectedItem = NavView.MenuItems[0];
        };

        // The mobile heads draw edge to edge, so the shell insets itself by the system bars (status bar, notch,
        // home indicator / gesture bar). Desktop and WebAssembly windows are not drawn under system chrome.
        // NavigationView's VisibleBoundsPadding did not take effect on iOS, so the insets are applied by hand
        // from ApplicationView.VisibleBounds (#280).
#if __IOS__ || __ANDROID__
        Loaded += (_, _) => ApplicationView.GetForCurrentView().VisibleBoundsChanged += OnVisibleBoundsChanged;
        Unloaded += (_, _) => ApplicationView.GetForCurrentView().VisibleBoundsChanged -= OnVisibleBoundsChanged;
        SizeChanged += (_, _) => ApplySafeAreaPadding();
#endif
    }

#if __IOS__ || __ANDROID__
    private void OnVisibleBoundsChanged(ApplicationView sender, object args) => ApplySafeAreaPadding();

    private void ApplySafeAreaPadding()
    {
        var visible = ApplicationView.GetForCurrentView().VisibleBounds;
        if (ActualWidth <= 0 || ActualHeight <= 0 || visible.Width <= 0 || visible.Height <= 0)
            return;

        // This page is the window's content, so its size is the window's; the part outside VisibleBounds is the
        // system bars.
        SafeAreaRoot.Padding = new Thickness(
            Math.Max(0, visible.Left),
            Math.Max(0, visible.Top),
            Math.Max(0, ActualWidth - visible.Right),
            Math.Max(0, ActualHeight - visible.Bottom));
    }
#endif

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        sender.Header = item.Content;

        Type? pageType = tag switch
        {
            "BasicGrid" => typeof(BasicGridPage),
            "Grouping" => typeof(GroupingPage),
            "LargeData" => typeof(LargeDataPage),
            "Theming" => typeof(ThemingPage),
            "MvvmActions" => typeof(MvvmActionsPage),
            "CustomFonts" => typeof(CustomFontsPage),
            "Images" => typeof(ImagesPage),
            "Styling" => typeof(StylingPage),
            "Events" => typeof(EventsPage),
            _ => null,
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }
}
