using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace SampleApp.Maui;

/// <summary>
/// Demonstrates custom font support in KumikoUI:
/// <list type="bullet">
///   <item>Japanese (CJK) column headers using the NotoSansJP typeface registered at startup.</item>
///   <item>Material Design icon glyphs in a status column using the MaterialIcons typeface.</item>
/// </list>
/// </summary>
public partial class CustomFontsPage : ContentPage
{
    // ── Material Icons Unicode code points ────────────────────────
    // These are the glyph addresses in the MaterialIcons-Regular.ttf font.
    private const string IconCheck = "\uE876";      // check
    private const string IconClose = "\uE5CD";      // close
    private const string IconWarning = "\uE002";    // warning (filled)
    private const string IconStar = "\uE838";       // star
    private const string IconPerson = "\uE7FD";     // person

    public CustomFontsPage()
    {
        InitializeComponent();
        BuildJapaneseGrid();
        BuildIconGrid();
    }

    // ── Japanese headers demo ────────────────────────────────────

    private void BuildJapaneseGrid()
    {
        // Apply NotoSansJP for both header and cell text so Japanese characters render correctly.
        var style = new DataGridStyle
        {
            HeaderFont = new GridFont("NotoSansJP", 14, bold: true),
            CellFont = new GridFont("NotoSansJP", 13),
        };

        japaneseGrid.GridStyle = style;
        japaneseGrid.Columns.Add(new DataGridColumn
        {
            Header = "現品票ID",
            PropertyName = nameof(InventoryItem.ItemId),
            Width = 110,
            ColumnType = DataGridColumnType.Numeric,
            TextAlignment = GridTextAlignment.Right,
            IsReadOnly = true,
            IsFrozen = true,
        });
        japaneseGrid.Columns.Add(new DataGridColumn
        {
            Header = "RFID",
            PropertyName = nameof(InventoryItem.Rfid),
            Width = 160,
        });
        japaneseGrid.Columns.Add(new DataGridColumn
        {
            Header = "品番",
            PropertyName = nameof(InventoryItem.PartNumber),
            Width = 130,
        });
        japaneseGrid.Columns.Add(new DataGridColumn
        {
            Header = "数量",
            PropertyName = nameof(InventoryItem.Quantity),
            Width = 90,
            ColumnType = DataGridColumnType.Numeric,
            TextAlignment = GridTextAlignment.Right,
        });
        japaneseGrid.Columns.Add(new DataGridColumn
        {
            Header = "倉庫",
            PropertyName = nameof(InventoryItem.Warehouse),
            Width = 120,
        });

        japaneseGrid.ItemsSource = GenerateInventoryData();
    }

    private static ObservableCollection<InventoryItem> GenerateInventoryData()
    {
        string[] rfidPrefixes = ["JP-TK-", "JP-OS-", "JP-KY-"];
        string[] partNumbers = ["A-001-α", "B-002-β", "C-003-γ", "D-004-δ", "E-005-ε"];
        string[] warehouses = ["東京", "大阪", "京都", "名古屋", "福岡"];
        var rng = new Random(1);
        var items = new ObservableCollection<InventoryItem>();
        for (int i = 1; i <= 60; i++)
        {
            items.Add(new InventoryItem
            {
                ItemId = 1000 + i,
                Rfid = $"{rfidPrefixes[rng.Next(rfidPrefixes.Length)]}{i:D4}",
                PartNumber = partNumbers[rng.Next(partNumbers.Length)],
                Quantity = rng.Next(1, 500),
                Warehouse = warehouses[rng.Next(warehouses.Length)],
            });
        }
        return items;
    }

    // ── Icon font demo ───────────────────────────────────────────

    private void BuildIconGrid()
    {
        // Regular columns use the default font; only the Status column uses MaterialIcons.
        iconGrid.Columns.Add(new DataGridColumn
        {
            Header = "Name",
            PropertyName = nameof(TaskItem.Name),
            Width = 180,
            IsFrozen = true,
        });
        iconGrid.Columns.Add(new DataGridColumn
        {
            Header = "Assignee",
            PropertyName = nameof(TaskItem.Assignee),
            Width = 130,
        });
        iconGrid.Columns.Add(new DataGridColumn
        {
            Header = "Priority",
            PropertyName = nameof(TaskItem.PriorityIcon),
            Width = 80,
            IsReadOnly = true,
            TextAlignment = GridTextAlignment.Center,
            // Override the font for this column's cells so glyphs are drawn
            // from the MaterialIcons typeface registered at startup.
            CustomCellRenderer = new IconFontCellRenderer("MaterialIcons", 22f),
        });
        iconGrid.Columns.Add(new DataGridColumn
        {
            Header = "Done",
            PropertyName = nameof(TaskItem.DoneIcon),
            Width = 60,
            IsReadOnly = true,
            TextAlignment = GridTextAlignment.Center,
            CustomCellRenderer = new IconFontCellRenderer("MaterialIcons", 22f),
        });

        iconGrid.ItemsSource = GenerateTaskData();
    }

    private ObservableCollection<TaskItem> GenerateTaskData()
    {
        string[] names = ["Implement login", "Fix crash on startup", "Write unit tests",
                          "Add dark theme", "Optimize SQL queries", "Update documentation",
                          "Code review PR #42", "Deploy to staging", "Load testing",
                          "Security audit"];
        string[] assignees = ["Alice", "Bob", "Charlie", "Diana", "Eve",
                               "Frank", "Grace", "Hank", "Iris", "Jack"];
        var rng = new Random(2);
        var items = new ObservableCollection<TaskItem>();
        for (int i = 0; i < names.Length; i++)
        {
            var priority = rng.Next(3);
            var done = rng.NextDouble() > 0.5;
            items.Add(new TaskItem
            {
                Name = names[i],
                Assignee = assignees[i % assignees.Length],
                PriorityIcon = priority switch { 0 => IconWarning, 1 => IconStar, _ => "" },
                DoneIcon = done ? IconCheck : IconClose,
                IsDone = done,
                Priority = priority,
            });
        }
        return items;
    }
}

// ── Data models ──────────────────────────────────────────────────

/// <summary>Inventory item with Japanese property-value data.</summary>
public class InventoryItem : INotifyPropertyChanged
{
    private int _itemId;
    private string _rfid = string.Empty;
    private string _partNumber = string.Empty;
    private int _quantity;
    private string _warehouse = string.Empty;

    public int ItemId { get => _itemId; set { _itemId = value; OnPropertyChanged(); } }
    public string Rfid { get => _rfid; set { _rfid = value; OnPropertyChanged(); } }
    public string PartNumber { get => _partNumber; set { _partNumber = value; OnPropertyChanged(); } }
    public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }
    public string Warehouse { get => _warehouse; set { _warehouse = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Task item whose Priority and Done columns are rendered as Material Icons glyphs.</summary>
public class TaskItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _assignee = string.Empty;
    private string _priorityIcon = string.Empty;
    private string _doneIcon = string.Empty;
    private bool _isDone;
    private int _priority;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Assignee { get => _assignee; set { _assignee = value; OnPropertyChanged(); } }
    public string PriorityIcon { get => _priorityIcon; set { _priorityIcon = value; OnPropertyChanged(); } }
    public string DoneIcon { get => _doneIcon; set { _doneIcon = value; OnPropertyChanged(); } }
    public bool IsDone { get => _isDone; set { _isDone = value; OnPropertyChanged(); } }
    public int Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Icon font renderer ───────────────────────────────────────────

/// <summary>
/// A simple <see cref="ICellRenderer"/> that draws cell text using a named icon font.
/// Set the cell value to a Unicode code-point string (e.g. <c>"\uE876"</c>) and the renderer
/// will draw the corresponding glyph from the registered typeface.
/// </summary>
internal sealed class IconFontCellRenderer : ICellRenderer
{
    private readonly string _fontFamily;
    private readonly float _fontSize;

    /// <param name="fontFamily">
    /// Family name registered in <see cref="KumikoUI.SkiaSharp.SkiaFontRegistrar"/>.
    /// </param>
    /// <param name="fontSize">Glyph size in pixels.</param>
    public IconFontCellRenderer(string fontFamily, float fontSize)
    {
        _fontFamily = fontFamily;
        _fontSize = fontSize;
    }

    /// <inheritdoc />
    public void Render(
        IDrawingContext ctx,
        GridRect cellRect,
        object? value,
        string displayText,
        DataGridColumn column,
        DataGridStyle style,
        bool isSelected,
        CellStyle? cellStyle = null)
    {
        if (string.IsNullOrEmpty(displayText))
            return;

        var textColor = isSelected ? style.SelectionTextColor : style.CellTextColor;
        var paint = new GridPaint
        {
            Color = textColor,
            IsAntiAlias = true,
            Font = new GridFont(_fontFamily, _fontSize),
        };

        ctx.DrawTextInRect(displayText, cellRect, paint,
            GridTextAlignment.Center, GridVerticalAlignment.Center);
    }
}
