using System.Reflection;
using KumikoUI.Core.Components;
using KumikoUI.Core.Data;
using KumikoUI.Core.Editing;
using KumikoUI.Core.Models;
using KumikoUI.Core.Rendering;
using KumikoUI.Core.Tests.Data;

namespace KumikoUI.Core.Tests;

/// <summary>A type marked [KumikoGridItem]: the test project runs the source generator over it.</summary>
[KumikoGridItem]
internal class MarkedLedgerEntry
{
    public string Account { get; set; } = "";
    public int Units { get; set; }
    public string Memo => "memo:" + Account;
}

/// <summary>The same shape without the attribute: it has no generated accessors.</summary>
internal class UnmarkedLedgerEntry
{
    public string Account { get; set; } = "";
    public int Units { get; set; }
}

/// <summary>
/// The [KumikoGridItem] attribute: how it can be applied, and that the source generator (which this test
/// project runs) gives an attributed type accessors that keep working, including through an edit session,
/// when reflection property access is switched off, as under Native AOT.
/// </summary>
[Collection(nameof(ReflectionSwitchCollection))]
public sealed class KumikoGridItemAttributeTests : IDisposable
{
    public KumikoGridItemAttributeTests() =>
        AppContext.SetSwitch(KumikoAccessorRegistry.ReflectionSwitchName, false);

    public void Dispose() =>
        AppContext.SetSwitch(KumikoAccessorRegistry.ReflectionSwitchName, true);

    [Fact]
    public void Attribute_TargetsClassesAndStructs_IsNotInheritedAndAllowsOneUse()
    {
        var usage = typeof(KumikoGridItemAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage!.ValidOn);
        Assert.False(usage.Inherited);
        Assert.False(usage.AllowMultiple);
        Assert.True(typeof(KumikoGridItemAttribute).IsSealed);
    }

    [Fact]
    public void AttributedTypeCarriesTheAttribute_AndOnlyItGetsAGeneratedProvider()
    {
        Assert.NotNull(typeof(MarkedLedgerEntry).GetCustomAttribute<KumikoGridItemAttribute>());
        Assert.Null(typeof(UnmarkedLedgerEntry).GetCustomAttribute<KumikoGridItemAttribute>());

        var assembly = typeof(MarkedLedgerEntry).Assembly;
        var provider = assembly.GetType("KumikoUI.Generated.__KumikoAccessors_KumikoUI_Core_Tests_MarkedLedgerEntry");
        Assert.NotNull(provider);
        Assert.True(typeof(IKumikoAccessorProvider).IsAssignableFrom(provider));
        Assert.Null(assembly.GetType("KumikoUI.Generated.__KumikoAccessors_KumikoUI_Core_Tests_UnmarkedLedgerEntry"));
    }

    [Fact]
    public void GeneratedProvider_DescribesReadableAndWritableProperties()
    {
        var providerType = typeof(MarkedLedgerEntry).Assembly
            .GetType("KumikoUI.Generated.__KumikoAccessors_KumikoUI_Core_Tests_MarkedLedgerEntry")!;
        var provider = (IKumikoAccessorProvider)Activator.CreateInstance(providerType, nonPublic: true)!;
        var entry = new MarkedLedgerEntry { Account = "cash", Units = 2 };

        var units = provider.GetAccessor(typeof(MarkedLedgerEntry), "units")!; // names are case-insensitive
        var memo = provider.GetAccessor(typeof(MarkedLedgerEntry), "Memo")!;

        Assert.Equal(typeof(int), units.PropertyType);
        Assert.True(units.CanRead);
        Assert.True(units.CanWrite);
        Assert.True(units.SetValue(entry, 9));
        Assert.Equal(9, entry.Units);
        Assert.Equal("memo:cash", memo.GetValue(entry));
        Assert.False(memo.CanWrite);
        Assert.False(memo.SetValue(entry, "x"));
        Assert.Null(provider.GetAccessor(typeof(MarkedLedgerEntry), "Nope"));
    }

    [Fact]
    public void AttributedType_IsEditableThroughTheEditSession_WithoutReflection()
    {
        Assert.False(KumikoAccessorRegistry.IsReflectionPropertyAccessSupported);
        var entry = new MarkedLedgerEntry { Account = "cash", Units = 2 };
        var account = new KumikoColumn { PropertyName = "Account" };
        var units = new KumikoColumn { PropertyName = "Units", ColumnType = KumikoColumnType.Numeric };
        var source = new KumikoSource();
        source.SetColumns(new[] { account, units });
        source.SetItems(new[] { entry });
        var session = new EditSession();

        session.BeginEdit(0, 1, units, source, new KumikoRect(0, 0, 100, 30));
        ((DrawnTextBox)session.ActiveEditor!).Text = "12";
        Assert.True(session.CommitEdit(source));

        Assert.Equal(12, entry.Units);
        Assert.Equal("cash", source.GetCellValue(0, account));
    }

    [Fact]
    public void UnattributedType_ReadsNullWithoutReflection()
    {
        var entry = new UnmarkedLedgerEntry { Account = "cash" };
        var account = new KumikoColumn { PropertyName = "Account" };
        var source = new KumikoSource();
        source.SetColumns(new[] { account });
        source.SetItems(new[] { entry });

        Assert.Null(source.GetCellValue(0, account));
    }
}
