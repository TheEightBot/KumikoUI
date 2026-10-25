using KumikoUI.Core.Components;
using KumikoUI.Core.Data;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Data;

/// <summary>
/// Property access (#17): generated accessors, column delegates, providers and the reflection fallback,
/// through the public grid APIs that use them.
/// </summary>
public class KumikoAccessorTests
{
    private static KumikoSource Source(System.Collections.IList items, params KumikoColumn[] columns)
    {
        var source = new KumikoSource();
        source.SetColumns(columns);
        source.SetItems(items);
        return source;
    }

    private static KumikoColumn Col(string path) => new() { Header = path, PropertyName = path };

    // ── Generated accessors ───────────────────────────────────────

    [Fact]
    public void Generator_EmitsAndRegistersAProviderPerAttributedType()
    {
        var provider = typeof(GenEmployee).Assembly.GetType(
            "KumikoUI.Generated.__KumikoAccessors_KumikoUI_Core_Tests_Data_GenEmployee");

        Assert.NotNull(provider);
        Assert.True(typeof(IKumikoAccessorProvider).IsAssignableFrom(provider));
        Assert.Null(typeof(RefEmployee).Assembly.GetType(
            "KumikoUI.Generated.__KumikoAccessors_KumikoUI_Core_Tests_Data_RefEmployee"));
    }

    [Fact]
    public void Generated_ReadsAndWritesPropertiesCaseInsensitively()
    {
        var item = new GenEmployee { Name = "Ada", Age = 36 };
        var name = Col("name");
        var source = Source(new[] { item }, name, Col("AGE"));

        Assert.Equal("Ada", source.GetCellValue(0, name));
        Assert.Equal(36, source.GetCellValue(0, source.Columns[1]));

        source.SetCellValue(0, name, "Grace");
        Assert.Equal("Grace", item.Name);
    }

    [Fact]
    public void Generated_SetCellValue_ConvertsToThePropertyType()
    {
        var item = new GenEmployee();
        var age = Col("Age");
        var salary = Col("Salary");
        var source = Source(new[] { item }, age, salary);

        source.SetCellValue(0, age, 41.0);        // double → int
        source.SetCellValue(0, salary, "1250.5"); // string → decimal (current culture)
        source.SetCellValue(0, age, "not a number");

        Assert.Equal(41, item.Age);
        Assert.Equal(1250.5m, item.Salary);
    }

    [Fact]
    public void Generated_ThrowingSetter_LeavesTheItemUnchanged()
    {
        var item = new GenValidated { Percent = 40 };
        var percent = Col("Percent");
        var source = Source(new[] { item }, percent);

        source.SetCellValue(0, percent, 250);

        Assert.Equal(40, item.Percent);
    }

    [Fact]
    public void Generated_ReadOnlyProperty_IgnoresEdits()
    {
        var item = new GenEmployee { Name = "Ada" };
        var code = Col("ReadOnlyCode");
        var source = Source(new[] { item }, code);

        source.SetCellValue(0, code, "X");

        Assert.Equal("R-Ada", source.GetCellValue(0, code));
    }

    [Fact]
    public void Generated_WriteOnlyProperty_ReadsNullButAcceptsEdits()
    {
        var item = new GenEmployee();
        var secret = Col("Secret");
        var source = Source(new[] { item }, secret);

        Assert.Null(source.GetCellValue(0, secret));
        source.SetCellValue(0, secret, "s3cret");
        Assert.Equal("s3cret", item.SecretValue);
    }

    [Fact]
    public void Generated_NestedPath_ReadsThroughAndNullIntermediateReadsNull()
    {
        var withAddress = new GenEmployee { Name = "A", Address = new GenAddress { City = "Oslo" } };
        var withoutAddress = new GenEmployee { Name = "B", Address = null };
        var city = Col("Address.City");
        var source = Source(new[] { withAddress, withoutAddress }, city);

        Assert.Equal("Oslo", source.GetCellValue(0, city));
        Assert.Null(source.GetCellValue(1, city));

        source.SetCellValue(1, city, "Rome"); // no address to write into: ignored, no exception
        source.SetCellValue(0, city, "Rome");
        Assert.Null(withoutAddress.Address);
        Assert.Equal("Rome", withAddress.Address!.City);
    }

    [Fact]
    public void Generated_OverridesAndBaseProperties_UseTheMostDerivedType()
    {
        var derived = new GenDerived { Label = "d", Shared = 7, Extra = "x" };
        var unmarked = new GenDerivedUnmarked { Label = "u", Shared = 3 };
        var label = Col("Label");
        var shared = Col("Shared");
        var extra = Col("Extra");
        var source = Source(new GenBase[] { derived, unmarked }, label, shared, extra);

        Assert.Equal("d", source.GetCellValue(0, label));
        Assert.Equal(7, source.GetCellValue(0, shared));
        Assert.Equal("x", source.GetCellValue(0, extra));
        // An unmarked subclass resolves through its marked base type's accessors.
        Assert.Equal("u", source.GetCellValue(1, label));
        Assert.Equal(3, source.GetCellValue(1, shared));
    }

    [Fact]
    public void Generated_RecordsAndKeywordNamedProperties()
    {
        var record = new GenRecord("C-1", 4) { Note = "n" };
        var keywords = new GenKeywords { @class = "A", @event = 2 };

        var source = Source(new object[] { record }, Col("Code"), Col("Quantity"), Col("Note"));
        Assert.Equal("C-1", source.GetCellValue(0, source.Columns[0]));
        Assert.Equal(4, source.GetCellValue(0, source.Columns[1]));
        source.SetCellValue(0, source.Columns[1], 9); // init-only: not writable
        Assert.Equal(4, record.Quantity);

        var kw = Source(new[] { keywords }, Col("class"), Col("event"));
        Assert.Equal("A", kw.GetCellValue(0, kw.Columns[0]));
        Assert.Equal(2, kw.GetCellValue(0, kw.Columns[1]));
    }

    [Fact]
    public void Generated_StructSetter_WritesIntoTheBoxedValue()
    {
        object boxed = new GenPoint { X = 1, Y = 2 };
        var source = Source(new[] { boxed }, Col("X"), Col("Y"));

        source.SetCellValue(0, source.Columns[0], 10);

        Assert.Equal(10, ((GenPoint)boxed).X);
        Assert.Equal(2, source.GetCellValue(0, source.Columns[1]));
    }

    // ── Column delegates ──────────────────────────────────────────

    [Fact]
    public void ValueAccessor_TakesPrecedenceOverPropertyName()
    {
        var item = new GenEmployee { Name = "Ada", Age = 36 };
        var column = new KumikoColumn { PropertyName = "Name", ValueAccessor = o => ((GenEmployee)o).Age * 2 };
        var source = Source(new[] { item }, column);

        Assert.Equal(72, source.GetCellValue(0, column));
        Assert.Equal("72", source.GetCellDisplayText(0, column));
    }

    [Fact]
    public void ValueSetter_TakesPrecedence_AndReceivesTheValueUnconverted()
    {
        var item = new GenEmployee();
        object? received = null;
        var column = new KumikoColumn
        {
            PropertyName = "Age",
            ValueSetter = (o, v) => { received = v; ((GenEmployee)o).Name = $"set:{v}"; },
        };
        var source = Source(new[] { item }, column);

        source.SetCellValue(0, column, 41.5);

        Assert.Equal(41.5, received);
        Assert.Equal("set:41.5", item.Name);
        Assert.Equal(0, item.Age);
    }

    [Fact]
    public void ValueAccessor_DrivesSortingGroupingAndSummaries()
    {
        var items = new[]
        {
            new GenEmployee { Name = "a", Age = 30 },
            new GenEmployee { Name = "b", Age = 10 },
            new GenEmployee { Name = "c", Age = 20 },
        };
        // "Computed" is not a property: every value comes from the delegate.
        var computed = new KumikoColumn { Header = "Computed", PropertyName = "Computed", ValueAccessor = o => ((GenEmployee)o).Age / 10 };
        var name = Col("Name");
        var source = Source(items, name, computed);
        source.AddTableSummaryRow(new TableSummaryRow("Totals") { Columns = { new SummaryColumnDescription("Computed", SummaryType.Sum) } });

        source.ToggleSort(computed);
        Assert.Equal(new[] { "b", "c", "a" }, Enumerable.Range(0, 3).Select(r => source.GetCellValue(r, name)));
        Assert.Contains("6", source.ComputedBottomSummaries[0].Values["Computed"]);

        source.AddGroupDescription(new GroupDescription("Computed"));
        Assert.Equal("1", source.GetGroupHeaderInfo(0)!.DisplayText);
    }

    // ── Providers ─────────────────────────────────────────────────

    private sealed class BagProvider : IKumikoAccessorProvider
    {
        public KumikoPropertyAccessor? GetAccessor(Type itemType, string propertyName) =>
            new(propertyName, typeof(object),
                item => ((PropertyBag)item).TryGetValue(propertyName, out var v) ? v : null,
                (item, value) => ((PropertyBag)item)[propertyName] = value);
    }

    [Fact]
    public void RegisteredProvider_ServesItemsWithoutClrProperties()
    {
        KumikoAccessorRegistry.Register(typeof(PropertyBag), new BagProvider());
        var bag = new PropertyBag { ["Title"] = "Report", ["Pages"] = 12 };
        var title = Col("Title");
        var pages = Col("Pages");
        var source = Source(new[] { bag }, title, pages);

        Assert.Equal("Report", source.GetCellValue(0, title));
        Assert.Equal(12, source.GetCellValue(0, pages));

        source.SetCellValue(0, title, "Summary");
        Assert.Equal("Summary", bag["Title"]);
    }

    // ── Reflection fallback ───────────────────────────────────────

    [Fact]
    public void Reflection_ReadsNestedAndWriteOnlyProperties()
    {
        var item = new RefEmployee { Name = "Ada", Age = 36, Address = null };
        var city = Col("Address.City");
        var secret = Col("Secret");
        var source = Source(new[] { item }, Col("name"), city, secret);

        Assert.Equal("Ada", source.GetCellValue(0, source.Columns[0]));
        Assert.Null(source.GetCellValue(0, city)); // null intermediate no longer throws
        Assert.Null(source.GetCellValue(0, secret));

        source.SetCellValue(0, secret, "s");
        source.SetCellValue(0, city, "Oslo");
        Assert.Equal("s", item.SecretValue);
        Assert.Null(item.Address);
    }

    [Fact]
    public void EmptyPropertyName_IsTheItemItself()
    {
        var item = new RefEmployee();
        var whole = new KumikoColumn { PropertyName = "" };
        var source = Source(new[] { item }, whole);

        Assert.Same(item, source.GetCellValue(0, whole));
    }

    [Fact]
    public void ReflectionIsSupportedByDefault()
    {
        Assert.True(KumikoAccessorRegistry.IsReflectionPropertyAccessSupported);
    }

    // ── ComboBox editor ───────────────────────────────────────────

    [Fact]
    public void ComboBox_SelectorsTakePrecedenceOverMemberPaths()
    {
        var options = new[] { new GenOption { Id = 1, Label = "One" }, new GenOption { Id = 2, Label = "Two" } };
        var combo = new DrawnComboBox
        {
            DisplayMemberPath = "Label",
            ValueMemberPath = "Id",
            DisplaySelector = o => ((GenOption)o).Label.ToUpperInvariant(),
            ValueSelector = o => ((GenOption)o).Id * 10,
        };

        combo.SetItemsFromSource(options);
        combo.SetSelectedValue(20);

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("TWO", combo.SelectedItem!.DisplayText);
    }

    [Fact]
    public void ComboBoxEditor_AppliesColumnMemberPathsToItsItems()
    {
        var options = new[] { new GenOption { Id = 1, Label = "One" }, new GenOption { Id = 2, Label = "Two" } };
        var column = new KumikoColumn
        {
            PropertyName = "Age",
            ColumnType = KumikoColumnType.ComboBox,
            EditorItems = options,
            EditorDisplayMemberPath = "Label",
            EditorValueMemberPath = "Id",
        };

        var combo = Assert.IsType<DrawnComboBox>(CellEditorFactory.CreateEditor(column, 2, new KumikoRect(0, 0, 100, 30)));

        // Before #17 the paths were applied after the items were built, so labels showed ToString().
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("Two", combo.SelectedItem!.DisplayText);
    }
}
