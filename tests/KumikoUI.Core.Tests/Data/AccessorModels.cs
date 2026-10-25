using KumikoUI.Core.Data;

namespace KumikoUI.Core.Tests.Data;

// Models for the accessor tests. Types marked [KumikoGridItem] get generated accessors (the test project
// runs the source generator, as the KumikoUI.Core package does in apps); the others resolve through
// reflection. Each test touches its own types, because the accessor registry is process-wide.

[KumikoGridItem]
internal class GenEmployee
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public decimal Salary { get; set; }
    public GenAddress? Address { get; set; }
    public string ReadOnlyCode => "R-" + Name;

    // Write-only: no getter, so the cell reads as null, but edits still land.
    private string _secret = "";
    public string Secret { set => _secret = value; }
    public string SecretValue => _secret;
}

[KumikoGridItem]
internal class GenAddress
{
    public string City { get; set; } = "";
}

[KumikoGridItem]
internal class GenBase
{
    public virtual string Label { get; set; } = "base";
    public int Shared { get; set; }
}

// Not attributed: inherits the accessors registered for GenBase.
internal class GenDerivedUnmarked : GenBase
{
}

[KumikoGridItem]
internal class GenDerived : GenBase
{
    public override string Label { get; set; } = "derived";
    public string Extra { get; set; } = "";
}

[KumikoGridItem]
internal struct GenPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

[KumikoGridItem]
internal sealed record GenRecord(string Code, int Quantity)
{
    public string Note { get; set; } = "";
}

[KumikoGridItem]
internal class GenKeywords
{
    public string @class { get; set; } = "";
    public int @event { get; set; }
}

[KumikoGridItem]
internal class GenOption
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
}

// Reflection-only twins of the generated models.
internal class RefEmployee
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public RefAddress? Address { get; set; }
    public string ReadOnlyCode => "R-" + Name;
    private string _secret = "";
    public string Secret { set => _secret = value; }
    public string SecretValue => _secret;
}

internal class RefAddress
{
    public string City { get; set; } = "";
}

// Used only by the reflection-disabled tests, so no other test has cached an accessor for them.
internal class NoAccessorItem
{
    public string Name { get; set; } = "";
}

[KumikoGridItem]
internal class AotItem
{
    public string Name { get; set; } = "";
    public AotChild? Child { get; set; }
}

[KumikoGridItem]
internal class AotChild
{
    public int Value { get; set; }
}

// Read through an IKumikoAccessorProvider registered for the type.
internal sealed class PropertyBag : Dictionary<string, object?>
{
}

[KumikoGridItem]
internal class GenValidated
{
    private int _percent;
    public int Percent
    {
        get => _percent;
        set => _percent = value is >= 0 and <= 100 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
