using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace KumikoUI.SourceGenerators;

/// <summary>Diagnostics reported by <see cref="KumikoGridItemGenerator"/>.</summary>
internal static class Diagnostics
{
    private const string Category = "KumikoUI";

    public static readonly DiagnosticDescriptor GenericType = new(
        id: "KUMIKO001",
        title: "[KumikoGridItem] does not support generic types",
        messageFormat: "'{0}' is generic, so no accessors are generated for it. Use KumikoColumn.ValueAccessor/ValueSetter or register an IKumikoAccessorProvider.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleType = new(
        id: "KUMIKO002",
        title: "[KumikoGridItem] type is not accessible",
        messageFormat: "'{0}' (or a type containing it) is private, protected or file-local, so generated accessors cannot reference it. Make it internal or public.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

/// <summary>A diagnostic captured in an equatable form, so the incremental pipeline can cache it.</summary>
internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, DiagnosticLocation? Location, string Argument)
{
    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), Argument);
}

/// <summary>An equatable stand-in for <see cref="Location"/>.</summary>
internal sealed record DiagnosticLocation(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static DiagnosticLocation? From(Location location) =>
        location.SourceTree is null
            ? null
            : new DiagnosticLocation(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}
