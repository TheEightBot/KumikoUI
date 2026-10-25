using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace KumikoUI.SourceGenerators;

/// <summary>
/// Emits a reflection-free <c>IKumikoAccessorProvider</c> for every type marked
/// <c>[KumikoGridItem]</c>, and a module initializer that registers it with
/// <c>KumikoAccessorRegistry</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class KumikoGridItemGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "KumikoUI.Core.Data.KumikoGridItemAttribute";

    /// <summary>Fully qualified, without nullable annotations (they are not valid in typeof).</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Fully qualified, with nullable annotations, so setter casts match the property type exactly.</summary>
    private static readonly SymbolDisplayFormat CastFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var items = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, ct) => Analyze((INamedTypeSymbol)ctx.TargetSymbol, ctx.TargetNode, ct));

        context.RegisterSourceOutput(items, static (spc, item) =>
        {
            if (item.Diagnostic is { } diagnostic)
            {
                spc.ReportDiagnostic(diagnostic.ToDiagnostic());
                return;
            }

            spc.AddSource(item.HintName, SourceText.From(Emit(item), Encoding.UTF8));
        });
    }

    private static GridItemModel Analyze(INamedTypeSymbol type, SyntaxNode node, CancellationToken ct)
    {
        string displayName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var location = DiagnosticLocation.From(((TypeDeclarationSyntax)node).Identifier.GetLocation());

        if (type.IsGenericType)
            return GridItemModel.Fail(new DiagnosticInfo(Diagnostics.GenericType, location, displayName));

        for (INamedTypeSymbol? t = type; t != null; t = t.ContainingType)
        {
            if (t.IsFileLocal || !IsAccessible(t.DeclaredAccessibility))
                return GridItemModel.Fail(new DiagnosticInfo(Diagnostics.InaccessibleType, location, displayName));
        }

        string typeName = type.ToDisplayString(TypeFormat);
        var properties = new List<PropertyModel>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // Most-derived declarations first, so overrides and 'new' members win over base members.
        for (INamedTypeSymbol? t = type; t != null && t.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); t = t.BaseType)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var member in t.GetMembers())
            {
                if (member is not IPropertySymbol property || !seen.Add(property.Name) || !IsSupported(property))
                    continue;

                bool canRead = property.GetMethod is { } get && IsAccessible(get.DeclaredAccessibility);
                bool canWrite = property.SetMethod is { } set && !set.IsInitOnly && IsAccessible(set.DeclaredAccessibility);
                if (!canRead && !canWrite)
                    continue;

                bool dynamic = property.Type.TypeKind == TypeKind.Dynamic;
                string propertyType = dynamic ? "object" : property.Type.ToDisplayString(TypeFormat);
                string castType = dynamic ? "object?" : property.Type.ToDisplayString(CastFormat);
                properties.Add(new PropertyModel(property.Name, Identifier(property.Name), propertyType, castType, canRead, canWrite));
            }
        }

        string sanitized = Sanitize(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""));
        return new GridItemModel(
            HintName: $"KumikoAccessors.{sanitized}.g.cs",
            ClassName: "__KumikoAccessors_" + sanitized,
            TypeName: typeName,
            IsValueType: type.IsValueType,
            Properties: new EquatableArray<PropertyModel>(properties.ToImmutableArray()),
            Diagnostic: null);
    }

    private static bool IsSupported(IPropertySymbol property) =>
        !property.IsStatic
        && !property.IsIndexer
        && property.DeclaredAccessibility == Accessibility.Public
        && property.ExplicitInterfaceImplementations.IsEmpty
        && property.RefKind == RefKind.None
        && !property.Type.IsRefLikeType
        && property.Type.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer)
        && !IsErrorObsolete(property);

    /// <summary>Using an [Obsolete(..., error: true)] member is a compile error, so it gets no accessor.</summary>
    private static bool IsErrorObsolete(IPropertySymbol property) =>
        property.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute"
            && a.ConstructorArguments.Length == 2
            && a.ConstructorArguments[1].Value is true);

    private static bool IsAccessible(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) == SyntaxKind.FieldKeyword
            ? "@" + name
            : name;

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString();
    }

    private static string Emit(GridItemModel item)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by KumikoUI.SourceGenerators for [KumikoGridItem]. Do not edit.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS0612, CS0618 // obsolete members are still accessible");
        sb.AppendLine();
        sb.AppendLine("namespace KumikoUI.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"KumikoUI.SourceGenerators\", \"1.0\")]");
        sb.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"    internal sealed class {item.ClassName} : global::KumikoUI.Core.Data.IKumikoAccessorProvider");
        sb.AppendLine("    {");
        sb.AppendLine("        private static readonly global::System.Collections.Generic.Dictionary<string, global::KumikoUI.Core.Data.KumikoPropertyAccessor> s_accessors =");
        sb.AppendLine("            new global::System.Collections.Generic.Dictionary<string, global::KumikoUI.Core.Data.KumikoPropertyAccessor>(global::System.StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("            {");
        foreach (var p in item.Properties)
        {
            string getter = p.CanRead
                ? $"static item => (({item.TypeName})item).{p.Identifier}"
                : "null";
            string target = item.IsValueType
                ? $"global::System.Runtime.CompilerServices.Unsafe.Unbox<{item.TypeName}>(item)"
                : $"(({item.TypeName})item)";
            string setter = p.CanWrite
                ? $"static (item, value) => {target}.{p.Identifier} = value is null ? default! : ({p.CastType})value"
                : "null";
            sb.AppendLine($"                [\"{p.Name}\"] = new global::KumikoUI.Core.Data.KumikoPropertyAccessor(\"{p.Name}\", typeof({p.PropertyType}),");
            sb.AppendLine($"                    {getter},");
            sb.AppendLine($"                    {setter}),");
        }
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("        public global::KumikoUI.Core.Data.KumikoPropertyAccessor? GetAccessor(global::System.Type itemType, string propertyName) =>");
        sb.AppendLine("            s_accessors.TryGetValue(propertyName, out var accessor) ? accessor : null;");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable CA2255 // registering accessors at module load is the intent");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("#pragma warning restore CA2255");
        sb.AppendLine("        internal static void Register() =>");
        sb.AppendLine($"            global::KumikoUI.Core.Data.KumikoAccessorRegistry.Register(typeof({item.TypeName}), new {item.ClassName}());");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}

internal sealed record GridItemModel(
    string HintName,
    string ClassName,
    string TypeName,
    bool IsValueType,
    EquatableArray<PropertyModel> Properties,
    DiagnosticInfo? Diagnostic)
{
    public static GridItemModel Fail(DiagnosticInfo diagnostic) =>
        new("", "", "", false, default, diagnostic);
}

internal sealed record PropertyModel(string Name, string Identifier, string PropertyType, string CastType, bool CanRead, bool CanWrite);
