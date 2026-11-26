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
            var candidates = t.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => seen.Add(p.Name) && IsSupported(p))
                .Select(p => (Property: p, CanRead: CanRead(p), CanWrite: CanWrite(p)))
                .Where(c => c.CanRead || c.CanWrite);

            foreach (var (property, canRead, canWrite) in candidates)
            {
                bool dynamic = property.Type.TypeKind == TypeKind.Dynamic;
                string propertyType = dynamic ? "object" : property.Type.ToDisplayString(TypeFormat);
                string castType = dynamic ? "object?" : property.Type.ToDisplayString(CastFormat);
                properties.Add(new PropertyModel(property.Name, Identifier(property.Name), propertyType, castType, canRead, canWrite));
            }
        }

        var columns = AnalyzeColumns(type, ct);

        string sanitized = Sanitize(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""));
        return new GridItemModel(
            HintName: $"KumikoAccessors.{sanitized}.g.cs",
            ClassName: "__KumikoAccessors_" + sanitized,
            TypeName: typeName,
            IsValueType: type.IsValueType,
            Properties: new EquatableArray<PropertyModel>(properties.ToImmutableArray()),
            Columns: columns,
            Diagnostic: null);
    }

    /// <summary>
    /// The column metadata of the readable public properties, base type properties first in declaration order; an
    /// override or 'new' member keeps the position of the member it replaces. DataAnnotations attributes are read here,
    /// at compile time, so generating columns needs no reflection.
    /// </summary>
    private static EquatableArray<ColumnModel> AnalyzeColumns(INamedTypeSymbol type, CancellationToken ct)
    {
        var chain = new List<INamedTypeSymbol>();
        for (INamedTypeSymbol? t = type; t != null && t.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); t = t.BaseType)
            chain.Add(t);
        chain.Reverse();

        var columns = new List<ColumnModel>();
        var index = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var t in chain)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var property in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (!IsSupported(property) || !CanRead(property)) continue;
                var column = DescribeColumn(property);
                if (index.TryGetValue(property.Name, out int at))
                    columns[at] = column;
                else
                {
                    index[property.Name] = columns.Count;
                    columns.Add(column);
                }
            }
        }
        return new EquatableArray<ColumnModel>(columns.ToImmutableArray());
    }

    private static ColumnModel DescribeColumn(IPropertySymbol property)
    {
        var display = FindAttribute(property, "System.ComponentModel.DataAnnotations.DisplayAttribute");
        var displayFormat = FindAttribute(property, "System.ComponentModel.DataAnnotations.DisplayFormatAttribute");
        var editable = FindAttribute(property, "System.ComponentModel.DataAnnotations.EditableAttribute");
        var readOnly = FindAttribute(property, "System.ComponentModel.ReadOnlyAttribute");
        var browsable = FindAttribute(property, "System.ComponentModel.BrowsableAttribute");

        var resourceType = Named(display, "ResourceType") as ITypeSymbol;
        bool dynamic = property.Type.TypeKind == TypeKind.Dynamic;

        bool isReadOnly = !CanWrite(property)
                          || FirstArgument(editable) is false
                          || FirstArgument(readOnly) is true;
        bool autoGenerate = Named(display, "AutoGenerateField") is not false
                            && FirstArgument(browsable) is not false;

        return new ColumnModel(
            Name: property.Name,
            PropertyType: dynamic ? "object" : property.Type.ToDisplayString(TypeFormat),
            DisplayName: Text(Named(display, "Name") as string, resourceType),
            ShortName: Text(Named(display, "ShortName") as string, resourceType),
            Description: Text(Named(display, "Description") as string, resourceType),
            Order: Named(display, "Order") as int?,
            Format: Named(displayFormat, "DataFormatString") is string format ? Literal(format) : null,
            IsReadOnly: isReadOnly,
            AutoGenerate: autoGenerate,
            EnumValues: EnumValues(property.Type));
    }

    /// <summary>The attribute on the property or, for an override, on the member it overrides.</summary>
    private static AttributeData? FindAttribute(IPropertySymbol property, string fullName)
    {
        for (IPropertySymbol? p = property; p != null; p = p.OverriddenProperty)
        {
            foreach (var attribute in p.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == fullName)
                    return attribute;
            }
        }
        return null;
    }

    private static object? Named(AttributeData? attribute, string name)
    {
        if (attribute == null) return null;
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
                return argument.Value.Kind == TypedConstantKind.Error ? null : argument.Value.Value;
        }
        return null;
    }

    private static object? FirstArgument(AttributeData? attribute) =>
        attribute is { ConstructorArguments.Length: > 0 } && attribute.ConstructorArguments[0].Kind != TypedConstantKind.Error
            ? attribute.ConstructorArguments[0].Value
            : null;

    /// <summary>
    /// A C# expression for a display string: with a <c>ResourceType</c> that has a static string property of that name,
    /// a read of it (so a localized resource is read in the current culture), else the literal text.
    /// </summary>
    private static string? Text(string? value, ITypeSymbol? resourceType)
    {
        if (value == null) return null;
        if (resourceType is INamedTypeSymbol resources
            && resources.GetMembers(value).OfType<IPropertySymbol>().FirstOrDefault() is { IsStatic: true } property
            && property.Type.SpecialType == SpecialType.System_String
            && property.GetMethod is { } get && IsAccessible(get.DeclaredAccessibility) && IsAccessible(resources.DeclaredAccessibility))
        {
            return $"{resources.ToDisplayString(TypeFormat)}.{Identifier(property.Name)}";
        }
        return Literal(value);
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>For an enum or nullable enum, an array expression of its values (aliases once), else null.</summary>
    private static string? EnumValues(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (type.TypeKind != TypeKind.Enum) return null;

        string enumName = type.ToDisplayString(TypeFormat);
        var seen = new HashSet<object>();
        var values = new List<string>();
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.HasConstantValue && field.ConstantValue != null && seen.Add(field.ConstantValue))
                values.Add($"{enumName}.{Identifier(field.Name)}");
        }
        return "new object[] { " + string.Join(", ", values) + " }";
    }

    private static bool CanRead(IPropertySymbol property) =>
        property.GetMethod is { } get && IsAccessible(get.DeclaredAccessibility);

    private static bool CanWrite(IPropertySymbol property) =>
        property.SetMethod is { } set && !set.IsInitOnly && IsAccessible(set.DeclaredAccessibility);

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
        sb.AppendLine($"    internal sealed class {item.ClassName} : global::KumikoUI.Core.Data.IKumikoAccessorProvider, global::KumikoUI.Core.Data.IKumikoColumnMetadataProvider");
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
        // Built on each call: display texts read from resources follow the current culture.
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::KumikoUI.Core.Data.KumikoColumnMetadata>? GetColumns(global::System.Type itemType) =>");
        sb.AppendLine("            new global::KumikoUI.Core.Data.KumikoColumnMetadata[]");
        sb.AppendLine("            {");
        foreach (var c in item.Columns)
        {
            var init = new List<string>();
            if (c.DisplayName != null) init.Add($"DisplayName = {c.DisplayName}");
            if (c.ShortName != null) init.Add($"ShortName = {c.ShortName}");
            if (c.Description != null) init.Add($"Description = {c.Description}");
            if (c.Order is int order) init.Add($"Order = {order.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (c.Format != null) init.Add($"Format = {c.Format}");
            if (c.IsReadOnly) init.Add("IsReadOnly = true");
            if (!c.AutoGenerate) init.Add("AutoGenerate = false");
            if (c.EnumValues != null) init.Add($"EnumValues = {c.EnumValues}");
            string body = init.Count == 0 ? "" : " { " + string.Join(", ", init) + " }";
            sb.AppendLine($"                new global::KumikoUI.Core.Data.KumikoColumnMetadata(\"{c.Name}\", typeof({c.PropertyType})){body},");
        }
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable CA2255 // registering accessors at module load is the intent");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("#pragma warning restore CA2255");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var provider = new {item.ClassName}();");
        sb.AppendLine($"            global::KumikoUI.Core.Data.KumikoAccessorRegistry.Register(typeof({item.TypeName}), provider);");
        sb.AppendLine($"            global::KumikoUI.Core.Data.KumikoColumnMetadataRegistry.Register(typeof({item.TypeName}), provider);");
        sb.AppendLine("        }");
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
    EquatableArray<ColumnModel> Columns,
    DiagnosticInfo? Diagnostic)
{
    public static GridItemModel Fail(DiagnosticInfo diagnostic) =>
        new("", "", "", false, default, default, diagnostic);
}

internal sealed record PropertyModel(string Name, string Identifier, string PropertyType, string CastType, bool CanRead, bool CanWrite);

/// <summary>Column metadata of one property; the text members are C# expressions (literals or resource reads).</summary>
internal sealed record ColumnModel(
    string Name, string PropertyType, string? DisplayName, string? ShortName, string? Description,
    int? Order, string? Format, bool IsReadOnly, bool AutoGenerate, string? EnumValues);
