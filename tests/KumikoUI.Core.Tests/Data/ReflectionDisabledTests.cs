using KumikoUI.Core.Data;
using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests.Data;

/// <summary>Runs alone: it flips the process-wide reflection switch that every other test relies on.</summary>
[CollectionDefinition(nameof(ReflectionSwitchCollection), DisableParallelization = true)]
public sealed class ReflectionSwitchCollection
{
}

/// <summary>
/// With the reflection fallback switched off (the default for Native AOT and full trimming), generated
/// accessors and column delegates keep working and anything else reads as null without throwing.
/// </summary>
[Collection(nameof(ReflectionSwitchCollection))]
public sealed class ReflectionDisabledTests : IDisposable
{
    public ReflectionDisabledTests() =>
        AppContext.SetSwitch(KumikoAccessorRegistry.ReflectionSwitchName, false);

    public void Dispose() =>
        AppContext.SetSwitch(KumikoAccessorRegistry.ReflectionSwitchName, true);

    private static KumikoSource Source(System.Collections.IList items, params KumikoColumn[] columns)
    {
        var source = new KumikoSource();
        source.SetColumns(columns);
        source.SetItems(items);
        return source;
    }

    [Fact]
    public void SwitchIsOff()
    {
        Assert.False(KumikoAccessorRegistry.IsReflectionPropertyAccessSupported);
    }

    [Fact]
    public void GeneratedAccessors_StillReadAndWriteNestedPaths()
    {
        var item = new AotItem { Name = "n", Child = new AotChild { Value = 3 } };
        var name = new KumikoColumn { PropertyName = "Name" };
        var value = new KumikoColumn { PropertyName = "Child.Value" };
        var source = Source(new[] { item }, name, value);

        Assert.Equal("n", source.GetCellValue(0, name));
        Assert.Equal(3, source.GetCellValue(0, value));

        source.SetCellValue(0, value, 8);
        Assert.Equal(8, item.Child!.Value);
    }

    [Fact]
    public void TypeWithoutAccessors_ReadsNullAndIgnoresEdits_ButDelegatesWork()
    {
        var item = new NoAccessorItem { Name = "kept" };
        var byPath = new KumikoColumn { PropertyName = "Name" };
        var byDelegate = new KumikoColumn
        {
            PropertyName = "Name",
            ValueAccessor = o => ((NoAccessorItem)o).Name,
            ValueSetter = (o, v) => ((NoAccessorItem)o).Name = (string)v!,
        };
        var source = Source(new[] { item }, byPath, byDelegate);

        Assert.Null(source.GetCellValue(0, byPath));
        source.SetCellValue(0, byPath, "lost");
        Assert.Equal("kept", item.Name);

        Assert.Equal("kept", source.GetCellValue(0, byDelegate));
        source.SetCellValue(0, byDelegate, "written");
        Assert.Equal("written", item.Name);
    }
}
