using KumikoUI.Core.Data;

namespace SampleApp.Shared;

/// <summary>A row of the Load More pages: an order loaded in batches from <see cref="RemoteOrderService"/>.</summary>
[KumikoGridItem]
public class RemoteOrder
{
    /// <summary>The order number.</summary>
    public int Id { get; set; }

    /// <summary>The customer.</summary>
    public string Customer { get; set; } = "";

    /// <summary>The region the order ships to.</summary>
    public string Region { get; set; } = "";

    /// <summary>The order total.</summary>
    public decimal Total { get; set; }

    /// <summary>When the order was placed.</summary>
    public DateTime Placed { get; set; }
}

/// <summary>
/// A pretend remote API for the Load More pages, shared by the MAUI and Uno samples: it returns pages of orders after a
/// delay, ends after <see cref="TotalOrders"/>, and fails a request on demand so the retry row can be tried.
/// </summary>
public sealed class RemoteOrderService
{
    private static readonly string[] Customers = { "Contoso", "Fabrikam", "Northwind", "Tailspin", "Wingtip", "Litware", "Adatum", "Proseware" };
    private static readonly string[] Regions = { "North", "South", "East", "West", "Central" };

    /// <summary>How many orders the server holds.</summary>
    public int TotalOrders { get; set; } = 500;

    /// <summary>The simulated network latency.</summary>
    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(900);

    /// <summary>When true, the next request fails (then the flag clears, so a retry succeeds).</summary>
    public bool FailNextRequest { get; set; }

    /// <summary>Fetches up to <paramref name="count"/> orders starting at <paramref name="start"/>.</summary>
    /// <param name="start">The index of the first order.</param>
    /// <param name="count">How many to fetch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The orders; fewer than asked for at the end of the data.</returns>
    /// <exception cref="KumikoLoadMoreException">The simulated failure.</exception>
    public async Task<IReadOnlyList<RemoteOrder>> FetchAsync(int start, int count, CancellationToken cancellationToken)
    {
        await Task.Delay(Latency, cancellationToken);
        if (FailNextRequest)
        {
            FailNextRequest = false;
            throw new KumikoLoadMoreException("The server did not answer. Tap Retry.");
        }

        var page = new List<RemoteOrder>();
        for (int i = start; i < Math.Min(TotalOrders, start + count); i++)
        {
            page.Add(new RemoteOrder
            {
                Id = 10_000 + i,
                Customer = Customers[i % Customers.Length],
                Region = Regions[i * 7 % Regions.Length],
                Total = Math.Round(25m + i * 37 % 1900 + i % 7 * 0.25m, 2),
                Placed = new DateTime(2026, 1, 1).AddHours(-i * 5),
            });
        }
        return page;
    }

    /// <summary>A grid-ready collection that loads its orders from this service in batches.</summary>
    public KumikoIncrementalCollection<RemoteOrder> CreateCollection() => new(FetchAsync);
}
