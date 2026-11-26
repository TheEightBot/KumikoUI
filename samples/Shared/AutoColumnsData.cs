using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using KumikoUI.Core.Data;

namespace SampleApp.Shared;

/// <summary>The state of an <see cref="AutoOrder"/>; an enum property gets a drop-down column.</summary>
public enum AutoOrderStatus { Open, Shipped, Delivered, Returned }

/// <summary>
/// A row of the Auto Columns page. The grid generates its columns from these properties: the DataAnnotations
/// attributes set the headers, order, formats and read-only columns, and <c>[KumikoGridItem]</c> lets the source
/// generator emit that metadata, so generation needs no reflection (Native AOT and full trimming).
/// </summary>
[KumikoGridItem]
public class AutoOrder
{
    /// <summary>The order number, shown first and read-only.</summary>
    [Display(Name = "Order #", Order = 0)]
    [Editable(false)]
    public int Id { get; set; }

    /// <summary>The customer's name.</summary>
    public string Customer { get; set; } = "";

    /// <summary>When the order was placed; the short name is the header.</summary>
    [Display(Name = "Order date", ShortName = "Placed")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
    public DateTime Placed { get; set; }

    /// <summary>The order total, formatted as currency.</summary>
    [DisplayFormat(DataFormatString = "{0:C2}")]
    public decimal Total { get; set; }

    /// <summary>The number of items.</summary>
    public int Quantity { get; set; }

    /// <summary>Whether the order is paid: a check box column.</summary>
    public bool Paid { get; set; }

    /// <summary>The order state: a drop-down column with the enum's values.</summary>
    public AutoOrderStatus Status { get; set; }

    /// <summary>Internal notes, never shown as a column.</summary>
    [Display(AutoGenerateField = false)]
    public string InternalNotes { get; set; } = "";

    /// <summary>A computed value without a setter: a read-only column.</summary>
    [Display(Name = "Per item")]
    [DisplayFormat(DataFormatString = "{0:C2}")]
    public decimal PerItem => Quantity == 0 ? 0 : Math.Round(Total / Quantity, 2);
}

/// <summary>A second row type, to show the generated columns being replaced when the items change type.</summary>
[KumikoGridItem]
public class AutoProduct
{
    /// <summary>The stock keeping unit.</summary>
    [Display(Name = "SKU")]
    public string Sku { get; set; } = "";

    /// <summary>The product name.</summary>
    public string Name { get; set; } = "";

    /// <summary>The unit price.</summary>
    [DisplayFormat(DataFormatString = "{0:C2}")]
    public decimal Price { get; set; }

    /// <summary>Whether the product can be ordered.</summary>
    public bool? InStock { get; set; }

    /// <summary>A hidden property.</summary>
    [Browsable(false)]
    public string Supplier { get; set; } = "";
}

/// <summary>Sample data for the Auto Columns page.</summary>
public static class AutoColumnsData
{
    private static readonly string[] Customers = { "Avery", "Blake", "Casey", "Devon", "Emery", "Finley", "Harper", "Jordan" };

    /// <summary>Creates <paramref name="count"/> orders.</summary>
    public static List<AutoOrder> Orders(int count = 200) =>
        Enumerable.Range(1, count).Select(i => new AutoOrder
        {
            Id = 1000 + i,
            Customer = Customers[i % Customers.Length],
            Placed = new DateTime(2026, 1, 1).AddDays(i * 3 % 300),
            Total = 20 + i * 7.35m % 900,
            Quantity = 1 + i % 9,
            Paid = i % 3 != 0,
            Status = (AutoOrderStatus)(i % 4),
        }).ToList();

    /// <summary>Creates <paramref name="count"/> products.</summary>
    public static List<AutoProduct> Products(int count = 60) =>
        Enumerable.Range(1, count).Select(i => new AutoProduct
        {
            Sku = $"KU-{i:0000}",
            Name = $"Product {i}",
            Price = 4.99m + i % 40,
            InStock = (i % 5) switch { 0 => null, 1 => false, _ => true },
        }).ToList();
}
