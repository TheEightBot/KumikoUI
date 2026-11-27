using System.Globalization;
using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.IncrementalLoading;

/// <summary>The load-more announcements (spec 104 §9.5) and their strings (§9.6).</summary>
public class LoadMoreAnnouncementsTests
{
    private static KumikoLoadMoreCompletedEventArgs Completed(int rows, KumikoLoadMoreState state, string? error = null) =>
        new(rows, rows, state, state == KumikoLoadMoreState.Failed ? new InvalidOperationException() : null) { ErrorText = error };

    [Fact]
    public void Started_IsTheStartedString() =>
        Assert.Equal("Loading more rows.", KumikoAnnouncements.LoadMoreStarted(KumikoStrings.Default));

    [Fact]
    public void Completed_WithRows_CountsThemInTheCulture() =>
        Assert.Equal("1.250 more rows loaded.",
            KumikoAnnouncements.LoadMoreCompleted(KumikoStrings.Default with { AnnounceLoadMoreCompleted = "{0:N0} more rows loaded." },
                Completed(1250, KumikoLoadMoreState.Idle), CultureInfo.GetCultureInfo("de-DE")));

    [Fact]
    public void Completed_AtTheEnd_SaysAllRowsLoaded() =>
        Assert.Equal("All rows loaded.", KumikoAnnouncements.LoadMoreCompleted(KumikoStrings.Default, Completed(5, KumikoLoadMoreState.Completed)));

    [Fact]
    public void Completed_WithNoRowAndMoreToCome_SaysNothing() =>
        Assert.Null(KumikoAnnouncements.LoadMoreCompleted(KumikoStrings.Default, Completed(0, KumikoLoadMoreState.Idle)));

    [Fact]
    public void Failed_ReadsTheErrorText() =>
        Assert.Equal("Could not load more rows. No connection",
            KumikoAnnouncements.LoadMoreCompleted(KumikoStrings.Default, Completed(0, KumikoLoadMoreState.Failed, "No connection")));

    [Fact]
    public void Failed_WithoutAnErrorText_ReadsTheDefaultMessageOnce() =>
        Assert.Equal("Could not load more rows.",
            KumikoAnnouncements.LoadMoreCompleted(KumikoStrings.Default, Completed(0, KumikoLoadMoreState.Failed)));

    [Fact]
    public void TheStrings_HaveEnglishDefaults()
    {
        var s = KumikoStrings.Default;
        Assert.Equal("Loading more...", s.LoadMoreLoading);
        Assert.Equal("Load more", s.LoadMoreButton);
        Assert.Equal("Could not load more rows.", s.LoadMoreFailed);
        Assert.Equal("Retry", s.LoadMoreRetry);
        Assert.Equal("All rows loaded.", s.AnnounceAllRowsLoaded);
        Assert.Equal("{0} more rows loaded.", s.AnnounceLoadMoreCompleted);
        Assert.Equal("Could not load more rows. {0}", s.AnnounceLoadMoreFailed);
    }
}
