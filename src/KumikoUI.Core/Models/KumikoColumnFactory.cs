using KumikoUI.Core.Data;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Models;

/// <summary>
/// Builds <see cref="KumikoColumn"/> definitions from the properties of an item type, as the grid does when it
/// generates columns (<see cref="KumikoAutoGenerateColumnsMode"/>).
/// </summary>
/// <remarks>
/// <para>The property type picks the column type:</para>
/// <list type="table">
/// <listheader><term>Property type</term><description>Column</description></listheader>
/// <item><term><see cref="bool"/></term><description><see cref="KumikoColumnType.Boolean"/>; three-state for <c>bool?</c></description></item>
/// <item><term>integer, floating-point and <see cref="decimal"/> types</term><description><see cref="KumikoColumnType.Numeric"/>, right-aligned</description></item>
/// <item><term><see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/></term><description><see cref="KumikoColumnType.Date"/></description></item>
/// <item><term>an enum</term><description><see cref="KumikoColumnType.ComboBox"/> offering the enum's values</description></item>
/// <item><term><see cref="string"/>, <see cref="char"/>, <see cref="Guid"/>, <see cref="TimeSpan"/>, <see cref="TimeOnly"/></term><description><see cref="KumikoColumnType.Text"/></description></item>
/// </list>
/// <para>Nullable value types map like their underlying type. Any other type (collections, byte arrays, other
/// classes and structs) gets no column; declare one with a <see cref="KumikoColumn.ValueAccessor"/> or a custom renderer
/// instead. The header is the property's <c>[Display]</c> short name, else its name, else the property name; a
/// <c>[DisplayFormat]</c> format becomes <see cref="KumikoColumn.Format"/>; a property without a public setter, or marked
/// <c>[Editable(false)]</c> or <c>[ReadOnly(true)]</c>, gets a read-only column.</para>
/// </remarks>
public static class KumikoColumnFactory
{
    /// <summary>
    /// Returns a column for every property of <paramref name="itemType"/> that can have one
    /// (<see cref="KumikoColumnMetadataRegistry.GetColumns"/>), in column order: properties with a <c>[Display(Order)]</c>
    /// first, by order, then the rest in declaration order. Properties marked as not generated are left out.
    /// </summary>
    public static IReadOnlyList<KumikoColumn> FromType(Type itemType)
    {
        ArgumentNullException.ThrowIfNull(itemType);
        return GetGeneratedMetadata(itemType)
            .Select(Create)
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
    }

    /// <summary>
    /// Returns the column for one property, or null when its type cannot be shown in a column (see the remarks of
    /// <see cref="KumikoColumnFactory"/>). <see cref="KumikoColumnMetadata.AutoGenerate"/> is not checked here.
    /// </summary>
    public static KumikoColumn? Create(KumikoColumnMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var type = Nullable.GetUnderlyingType(metadata.PropertyType) ?? metadata.PropertyType;
        bool isNullable = type != metadata.PropertyType;

        var column = new KumikoColumn
        {
            Header = metadata.ShortName ?? metadata.DisplayName ?? metadata.PropertyName,
            PropertyName = metadata.PropertyName,
            IsReadOnly = metadata.IsReadOnly,
            Format = NormalizeFormat(metadata.Format),
        };

        if (type == typeof(bool))
        {
            column.ColumnType = KumikoColumnType.Boolean;
            column.IsThreeState = isNullable;
        }
        else if (type.IsEnum)
        {
            if (metadata.EnumValues is { Count: > 0 } values)
            {
                column.ColumnType = KumikoColumnType.ComboBox;
                column.EditorItems = values;
            }
        }
        else if (IsNumeric(type))
        {
            column.ColumnType = KumikoColumnType.Numeric;
            column.TextAlignment = KumikoTextAlignment.Right;
        }
        else if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
        {
            column.ColumnType = KumikoColumnType.Date;
        }
        else if (type != typeof(string) && type != typeof(char) && type != typeof(Guid)
                 && type != typeof(TimeSpan) && type != typeof(TimeOnly))
        {
            return null;
        }

        return column;
    }

    /// <summary>The metadata of the properties that get a generated column, in column order.</summary>
    internal static List<KumikoColumnMetadata> GetGeneratedMetadata(Type itemType)
    {
        var all = KumikoColumnMetadataRegistry.GetColumns(itemType);
        var result = new List<(KumikoColumnMetadata Metadata, int Index)>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is { AutoGenerate: true } metadata)
                result.Add((metadata, i));
        }

        // Stable: an explicit order first, ascending, then declaration order.
        result.Sort(static (a, b) =>
        {
            int byOrder = (a.Metadata.Order ?? int.MaxValue).CompareTo(b.Metadata.Order ?? int.MaxValue);
            return byOrder != 0 ? byOrder : a.Index.CompareTo(b.Index);
        });
        return result.ConvertAll(static r => r.Metadata);
    }

    /// <summary>"{0:C2}" and "C2" give "C2"; a composite format with other text, or none, gives null.</summary>
    internal static string? NormalizeFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return null;
        if (!format.Contains('{')) return format;

        // Exactly one placeholder for argument 0, with an optional alignment and a format: {0[,alignment][:format]}
        if (!format.StartsWith("{0", StringComparison.Ordinal) || !format.EndsWith('}')) return null;
        string inner = format[2..^1];
        int colon = inner.IndexOf(':');
        string alignment = colon < 0 ? inner : inner[..colon];
        if (alignment.Length > 0 && (alignment[0] != ',' || !int.TryParse(alignment.AsSpan(1), out _))) return null;
        if (colon < 0) return null;
        string result = inner[(colon + 1)..];
        return result.Length == 0 || result.Contains('{') || result.Contains('}') ? null : result;
    }

    private static bool IsNumeric(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double)
        || type == typeof(float) || type == typeof(short) || type == typeof(byte) || type == typeof(uint)
        || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
}
