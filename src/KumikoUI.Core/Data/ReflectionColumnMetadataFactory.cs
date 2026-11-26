using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace KumikoUI.Core.Data;

/// <summary>
/// The reflection fallback for column metadata: reads the public instance properties of a type and their
/// DataAnnotations attributes. Only reachable while
/// <see cref="KumikoAccessorRegistry.IsReflectionPropertyAccessSupported"/>.
/// </summary>
internal static class ReflectionColumnMetadataFactory
{
    [RequiresUnreferencedCode("Reads item properties and their attributes through reflection; their metadata may be trimmed.")]
    public static IReadOnlyList<KumikoColumnMetadata> Create(Type type)
    {
        // Base type properties first, in declaration order; an override or 'new' member keeps the base position.
        var chain = new List<Type>();
        for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            chain.Add(t);
        chain.Reverse();

        var order = new List<string>();
        var byName = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in chain)
        {
            foreach (var property in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetIndexParameters().Length > 0 || property.GetMethod is not { IsPublic: true })
                    continue;
                if (!byName.ContainsKey(property.Name))
                    order.Add(property.Name);
                byName[property.Name] = property;
            }
        }

        var result = new List<KumikoColumnMetadata>(order.Count);
        foreach (string name in order)
            result.Add(Describe(byName[name]));
        return result;
    }

    [RequiresUnreferencedCode("Reads attributes through reflection.")]
    private static KumikoColumnMetadata Describe(PropertyInfo property)
    {
        var display = property.GetCustomAttribute<DisplayAttribute>(inherit: true);
        var displayFormat = property.GetCustomAttribute<DisplayFormatAttribute>(inherit: true);
        var editable = property.GetCustomAttribute<EditableAttribute>(inherit: true);
        var readOnly = property.GetCustomAttribute<ReadOnlyAttribute>(inherit: true);
        var browsable = property.GetCustomAttribute<BrowsableAttribute>(inherit: true);

        bool canWrite = property.SetMethod is { IsPublic: true } set
                        && !set.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

        var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        return new KumikoColumnMetadata(property.Name, property.PropertyType)
        {
            DisplayName = display?.GetName(),
            ShortName = display?.ShortName != null ? display.GetShortName() : null, // GetShortName falls back to Name
            Description = display?.GetDescription(),
            Order = display?.GetOrder(),
            Format = displayFormat?.DataFormatString,
            IsReadOnly = !canWrite || editable is { AllowEdit: false } || readOnly is { IsReadOnly: true },
            AutoGenerate = display?.GetAutoGenerateField() != false && browsable is not { Browsable: false },
            EnumValues = enumType.IsEnum ? EnumValues(enumType) : null,
        };
    }

    private static object[] EnumValues(Type enumType)
    {
        // GetValuesAsUnderlyingType needs no dynamic code, unlike Enum.GetValues(Type); aliases appear once.
        var values = new List<object>();
        var seen = new HashSet<object>();
        foreach (object raw in Enum.GetValuesAsUnderlyingType(enumType))
        {
            if (seen.Add(raw))
                values.Add(Enum.ToObject(enumType, raw));
        }
        return values.ToArray();
    }
}
