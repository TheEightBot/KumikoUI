using System.Collections;
using System.ComponentModel;
using System.Globalization;
using KumikoUI.Core.Components;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.TestHelpers;

namespace KumikoUI.Core.Tests.Editing;

/// <summary>
/// The validation contract (#32): conversion, the grid's validator, the write and the item's own
/// validation run in order; errors keep the editor open with a visible message and a
/// <see cref="EditSession.ValidationFailed"/> event; the item never keeps a refused value; the editor
/// shows and parses text in the grid's culture.
/// </summary>
public class ValidationContractTests
{
    private sealed class Order : INotifyDataErrorInfo
    {
        private readonly Dictionary<string, string> _errors = new();
        private int _quantity = 1;

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                // Validates in the setter, like most MVVM models: errors describe the value just set
                SetError(nameof(Quantity), value > 100 ? "At most 100." : null);
            }
        }

        public decimal Price { get; set; } = 1.5m;

        public string Code
        {
            get => _code;
            set => _code = value.Length <= 5 ? value : throw new ArgumentException("Codes have at most five characters.");
        }
        private string _code = "A1";

        public bool HasErrors => _errors.Count > 0;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
        public IEnumerable GetErrors(string? propertyName) =>
            propertyName != null && _errors.TryGetValue(propertyName, out var e) ? new[] { e } : Array.Empty<string>();

        private void SetError(string property, string? error)
        {
            if (error == null) _errors.Remove(property); else _errors[property] = error;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
        }
    }

    private sealed class Fixture
    {
        public Order Order { get; } = new();
        public KumikoSource Source { get; } = new() { Culture = CultureInfo.InvariantCulture };
        public KumikoColumn Quantity { get; } = new() { Header = "Qty", PropertyName = nameof(Order.Quantity), ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn Price { get; } = new() { Header = "Price", PropertyName = nameof(Order.Price), ColumnType = KumikoColumnType.Numeric };
        public KumikoColumn Code { get; } = new() { Header = "Code", PropertyName = nameof(Order.Code) };
        public EditSession Session { get; } = new();
        public List<CellValidationEventArgs> Reported { get; } = new();
        public List<CellValueChangedEventArgs> Changed { get; } = new();

        public Fixture()
        {
            Source.SetColumns(new[] { Quantity, Price, Code });
            Source.SetItems(new[] { Order });
            Session.ValidationFailed += (_, e) => Reported.Add(e);
            Session.CellValueChanged += (_, e) => Changed.Add(e);
        }

        public DrawnTextBox Edit(KumikoColumn column, string text)
        {
            Assert.True(Session.BeginEdit(0, Source.Columns.ToList().IndexOf(column), column, Source, new KumikoRect(0, 30, 120, 30)));
            var box = (DrawnTextBox)Session.ActiveEditor!;
            box.Text = text;
            return box;
        }
    }

    [Fact]
    public void AConversionFailure_KeepsTheEditorOpen_ReportsTheMessage_AndChangesNothing()
    {
        var f = new Fixture();
        f.Edit(f.Quantity, "lots");

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.True(f.Session.IsEditing);
        Assert.Equal("Enter a number.", f.Session.CurrentValidation!.ErrorMessage);
        var report = Assert.Single(f.Reported);
        Assert.Equal("Enter a number.", report.Message);
        Assert.Equal(ValidationSeverity.Error, report.Severity);
        Assert.False(report.Committed);
        Assert.Same(f.Order, report.Item);
        Assert.Equal("lots", report.Value);
        Assert.Equal(1, f.Order.Quantity);
        Assert.Empty(f.Changed);
    }

    [Fact]
    public void TheGridsValidator_SeesTheConvertedValue_AndCanBlock()
    {
        var f = new Fixture();
        object? seen = null;
        f.Session.CellValidator = (_, _, value) =>
        {
            seen = value;
            return value is int n && n % 2 == 1 ? CellValidationResult.Error("Even numbers only.") : CellValidationResult.Valid;
        };
        f.Edit(f.Quantity, "7");

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.Equal(7, seen); // an int, not the text
        Assert.Equal("Even numbers only.", f.Reported.Single().Message);
        Assert.Equal(1, f.Order.Quantity);
    }

    [Fact]
    public void TheItemsOwnValidation_SeesTheNewValue_AndAnErrorRollsTheWriteBack()
    {
        var f = new Fixture();
        f.Edit(f.Quantity, "500");

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.Equal("At most 100.", f.Session.CurrentValidation!.ErrorMessage);
        Assert.Equal(1, f.Order.Quantity); // rolled back
        Assert.False(f.Order.HasErrors); // and the rollback revalidated it
        Assert.Empty(f.Changed);

        ((DrawnTextBox)f.Session.ActiveEditor!).Text = "50";
        Assert.True(f.Session.CommitEdit(f.Source));
        Assert.Equal(50, f.Order.Quantity);
        Assert.Single(f.Changed);
    }

    [Fact]
    public void ASetterThatThrows_IsAnError_WithItsMessage()
    {
        var f = new Fixture();
        f.Edit(f.Code, "TOOLONG");

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.Equal("Codes have at most five characters.", f.Session.CurrentValidation!.ErrorMessage);
        Assert.Equal("A1", f.Order.Code);
    }

    [Fact]
    public void Information_NeverBlocks_AndIsReportedWithTheCommittedValue()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, _) => new CellValidationResult { IsValid = true, ErrorMessage = "Rounded to cents.", Severity = ValidationSeverity.Info };
        f.Edit(f.Price, "2.25");

        Assert.True(f.Session.CommitEdit(f.Source));

        Assert.Equal(2.25m, f.Order.Price);
        var report = Assert.Single(f.Reported);
        Assert.True(report.Committed);
        Assert.Equal(ValidationSeverity.Info, report.Severity);
    }

    [Fact]
    public void AWarning_ForADifferentValue_IsShownAgain()
    {
        var f = new Fixture();
        f.Session.CellValidator = (_, _, value) => value is int n && n > 10 ? CellValidationResult.Warning("That's a lot.") : CellValidationResult.Valid;
        var box = f.Edit(f.Quantity, "20");

        Assert.False(f.Session.CommitEdit(f.Source));
        box.Text = "30";
        Assert.False(f.Session.CommitEdit(f.Source)); // a new value: warned again
        Assert.True(f.Session.CommitEdit(f.Source));  // the same value: accepted

        Assert.Equal(30, f.Order.Quantity);
    }

    [Fact]
    public void TheErrorIsDrawn_AsAnOutlineAnIconAndTheMessage()
    {
        var f = new Fixture();
        f.Edit(f.Quantity, "lots");
        f.Session.CommitEdit(f.Source);

        var ctx = new RecordingDrawingContext();
        f.Session.DrawEditor(ctx);

        var red = new KumikoColor(220, 50, 50);
        Assert.Contains(ctx.Ops, o => o.Kind == DrawOpKind.DrawRect && o.Color == red);
        Assert.Contains(ctx.Texts, t => t.Text == "!");
        var message = ctx.SingleText("Enter a number.");
        Assert.True(message.Rect.Y >= 60, "the message goes under the editor");
        Assert.Contains(ctx.Fills, o => o.Color == red && o.Rect.Y >= 60);
    }

    [Fact]
    public void TheEditor_ShowsAndParsesNumbers_InTheGridsCulture()
    {
        var f = new Fixture();
        f.Source.Culture = CultureInfo.GetCultureInfo("de-DE");

        f.Edit(f.Price, "1,5");
        Assert.True(f.Session.CommitEdit(f.Source));
        Assert.Equal(1.5m, f.Order.Price);

        Assert.True(f.Session.BeginEdit(0, 1, f.Price, f.Source, new KumikoRect(0, 30, 120, 30)));
        Assert.Equal("1,5", ((DrawnTextBox)f.Session.ActiveEditor!).Text);
    }

    [Fact]
    public void SetCellValue_ReportsWhetherTheValueWasWritten_AndOnlyThenRaisesAChange()
    {
        var f = new Fixture();
        int changes = 0;
        f.Source.ViewChanged += (_, _) => changes++;

        Assert.False(f.Source.SetCellValue(0, f.Quantity, "many"));
        Assert.False(f.Source.SetCellValue(0, f.Code, "TOOLONG"));
        Assert.False(f.Source.SetCellValue(5, f.Quantity, 3)); // no such row
        Assert.Equal(0, changes);

        Assert.True(f.Source.SetCellValue(0, f.Quantity, "12"));
        Assert.Equal(12, f.Order.Quantity);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void DelegateSetterColumns_ConvertToTheTypeOfTheValueTheyReplace()
    {
        var f = new Fixture();
        var viaDelegate = new KumikoColumn
        {
            Header = "Qty",
            ValueAccessor = o => ((Order)o).Quantity,
            ValueSetter = (o, v) => ((Order)o).Quantity = (int)v!,
            ColumnType = KumikoColumnType.Numeric,
        };
        f.Source.SetColumns(new[] { viaDelegate });
        Assert.True(f.Session.BeginEdit(0, 0, viaDelegate, f.Source, new KumikoRect(0, 30, 120, 30)));
        ((DrawnTextBox)f.Session.ActiveEditor!).Text = "42";

        Assert.True(f.Session.CommitEdit(f.Source));

        Assert.Equal(42, f.Order.Quantity);
    }

    [Fact]
    public void ADelegateSetterThatThrows_IsAnError()
    {
        var f = new Fixture();
        var strict = new KumikoColumn
        {
            Header = "Qty",
            ValueAccessor = o => ((Order)o).Quantity,
            ValueSetter = (_, _) => throw new InvalidOperationException("Locked."),
        };
        f.Source.SetColumns(new[] { strict });
        Assert.True(f.Session.BeginEdit(0, 0, strict, f.Source, new KumikoRect(0, 30, 120, 30)));
        ((DrawnTextBox)f.Session.ActiveEditor!).Text = "5";

        Assert.False(f.Session.CommitEdit(f.Source));

        Assert.Equal("Locked.", f.Session.CurrentValidation!.ErrorMessage);
    }

    [Fact]
    public void ToggleBooleanCell_RaisesNothing_WhenTheValueCannotBeWritten()
    {
        var source = new KumikoSource();
        var flag = new KumikoColumn { PropertyName = "Missing", ColumnType = KumikoColumnType.Boolean };
        source.SetColumns(new[] { flag });
        source.SetItems(new[] { new Order() });
        var session = new EditSession();
        int changed = 0;
        session.CellValueChanged += (_, _) => changed++;

        session.ToggleBooleanCell(0, 0, flag, source);

        Assert.Equal(0, changed);
    }
}
