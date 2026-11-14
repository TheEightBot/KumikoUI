using System.Globalization;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Input;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace SampleApp.Shared;

/// <summary>
/// A row of the Styling and Events pages, shared by the MAUI and Uno samples so both hosts show the same data.
/// </summary>
[KumikoUI.Core.Data.KumikoGridItem]
public class ShowcaseRow
{
    /// <summary>The row number.</summary>
    public int Id { get; set; }

    /// <summary>The person's name.</summary>
    public string Name { get; set; } = "";

    /// <summary>An app package path of an avatar image, drawn by an <c>Image</c> column.</summary>
    public string Avatar { get; set; } = "";

    /// <summary>The department.</summary>
    public string Department { get; set; } = "";

    /// <summary>The salary; the styling page highlights values above <see cref="ShowcaseData.HighSalary"/>.</summary>
    public decimal Salary { get; set; }

    /// <summary>A score from 0 to 100.</summary>
    public double Score { get; set; }

    /// <summary>False rows are drawn dimmed by the row style resolver.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// The data, the cell and row styles, the validator and the event descriptions of the Styling and Events pages.
/// </summary>
public static class ShowcaseData
{
    /// <summary>Salaries above this are drawn bold and red by <see cref="Apply"/>.</summary>
    public const decimal HighSalary = 100_000m;

    private static readonly string[] Departments = { "Engineering", "Marketing", "Sales", "HR", "Finance", "Design", "Support" };
    private static readonly string[] FirstNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Iris", "Jack" };
    private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };

    // Returned for every matching cell on every frame, so they are created once.
    private static readonly KumikoCellStyle HighSalaryStyle = new()
    {
        TextColor = new KumikoColor(204, 26, 26),
        Font = new KumikoFont("Default", 14, bold: true),
    };

    private static readonly KumikoCellStyle TopScoreStyle = new()
    {
        BackgroundColor = new KumikoColor(220, 245, 224),
        TextColor = new KumikoColor(20, 90, 40),
    };

    private static readonly KumikoRowStyle InactiveRowStyle = new() { TextColor = KumikoColor.Gray };

    /// <summary>Creates deterministic rows.</summary>
    /// <param name="count">How many rows.</param>
    /// <param name="avatarPath">Maps an avatar number (1 to 8) to the host's app package path of that image.</param>
    /// <returns>The rows.</returns>
    public static List<ShowcaseRow> CreateRows(int count, Func<int, string> avatarPath)
    {
        var random = new Random(7);
        var rows = new List<ShowcaseRow>(count);
        for (int i = 1; i <= count; i++)
        {
            rows.Add(new ShowcaseRow
            {
                Id = i,
                Name = $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}",
                Avatar = avatarPath(1 + (i - 1) % 8),
                Department = Departments[random.Next(Departments.Length)],
                Salary = random.Next(45, 160) * 1000m,
                Score = Math.Round(random.NextDouble() * 100, 1),
                IsActive = random.NextDouble() > 0.2,
            });
        }
        return rows;
    }

    /// <summary>
    /// Installs the per-cell and per-row style resolvers on a style: high salaries bold red, top scores green,
    /// inactive rows dimmed.
    /// </summary>
    /// <param name="style">The style to change; assign it back to the grid's <c>GridStyle</c>.</param>
    public static void Apply(KumikoStyle style)
    {
        style.CellStyleResolver = (item, column) => (item, column.PropertyName) switch
        {
            (ShowcaseRow { Salary: > HighSalary }, nameof(ShowcaseRow.Salary)) => HighSalaryStyle,
            (ShowcaseRow { Score: >= 90 }, nameof(ShowcaseRow.Score)) => TopScoreStyle,
            _ => null,
        };
        style.RowStyleResolver = item => item is ShowcaseRow { IsActive: false } ? InactiveRowStyle : null;
    }

    /// <summary>Removes the style resolvers installed by <see cref="Apply"/>.</summary>
    /// <param name="style">The style to change; assign it back to the grid's <c>GridStyle</c>.</param>
    public static void Clear(KumikoStyle style)
    {
        style.CellStyleResolver = null;
        style.RowStyleResolver = null;
    }

    /// <summary>The grid's <c>CellValidator</c>: salaries are limited to 200,000 and warned about above 150,000.</summary>
    /// <param name="row">The visible row.</param>
    /// <param name="column">The column index.</param>
    /// <param name="value">The converted value.</param>
    /// <returns>The validation result.</returns>
    public static CellValidationResult Validate(int row, int column, object? value) => value switch
    {
        decimal salary when salary > 200_000m => CellValidationResult.Error("Salary is at most 200,000."),
        decimal salary when salary > 150_000m => CellValidationResult.Warning("That is a high salary."),
        double score when score is < 0 or > 100 => CellValidationResult.Error("Score is 0 to 100."),
        _ => CellValidationResult.Valid,
    };

    /// <summary>The German strings of the localization demo (the rest keep the English defaults).</summary>
    public static readonly KumikoStrings GermanStrings = KumikoStrings.Default with
    {
        GroupPanelLabel = "Gruppiert nach:",
        BlankGroupText = "(Leer)",
        SummarySum = "Summe: ",
        FilterSelectAll = "Alle ausw\u00e4hlen",
        FilterOk = "\u00dcbernehmen",
        FilterCancel = "Abbrechen",
        EnterNumber = "Geben Sie eine Zahl ein.",
    };

    /// <summary>Scroll settings of the scrolling demo: a heavy fling that stops sooner, and a fast wheel.</summary>
    public static ScrollSettings HeavyScrolling => new() { Friction = 0.85f, WheelScrollMultiplier = 6f };

    /// <summary>Switches a data source between English and German (culture and strings).</summary>
    /// <param name="source">The grid's data source.</param>
    /// <param name="german">True for <c>de-DE</c>, false for the English defaults.</param>
    public static void SetLanguage(KumikoSource source, bool german)
    {
        source.Culture = CultureInfo.GetCultureInfo(german ? "de-DE" : "en-US");
        source.Strings = german ? GermanStrings : KumikoStrings.Default;
    }

    /// <summary>Describes a cell tap for the event log.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>One line of text.</returns>
    public static string Describe(KumikoCellTappedEventArgs e) =>
        $"CellTapped: row {e.RowIndex}, {e.Column.Header} ({(e.DataItem as ShowcaseRow)?.Name}), clicks {e.ClickCount}";

    /// <summary>Describes a header tap for the event log.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>One line of text.</returns>
    public static string Describe(KumikoHeaderTappedEventArgs e) =>
        $"HeaderTapped: {e.Column.Header} (column {e.ColumnIndex})";

    /// <summary>Describes a context menu request for the event log.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>One line of text.</returns>
    public static string Describe(KumikoContextRequestedEventArgs e) =>
        $"ContextMenuRequested: {e.Region} via {e.Trigger}, row {e.RowIndex}, {e.Column?.Header ?? "no column"}";

    /// <summary>Describes a validation failure for the event log.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>One line of text.</returns>
    public static string Describe(CellValidationEventArgs e) =>
        $"ValidationFailed: {e.Column.Header} = {e.Value} ({e.Severity}: {e.Message})";

    /// <summary>Describes a selection change for the event log.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>One line of text.</returns>
    public static string Describe(KumikoSelectionChangedEventArgs e) =>
        $"SelectionChanged: +{e.AddedItems.Count} / -{e.RemovedItems.Count}";
}
