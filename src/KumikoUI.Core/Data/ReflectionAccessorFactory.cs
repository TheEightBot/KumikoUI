using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace KumikoUI.Core.Data;

/// <summary>
/// The reflection fallback: builds an accessor from <see cref="PropertyInfo"/>. Getters are compiled
/// expression trees where dynamic code is supported, and <see cref="PropertyInfo.GetValue(object)"/>
/// otherwise. Only reachable while <see cref="KumikoAccessorRegistry.IsReflectionPropertyAccessSupported"/>.
/// </summary>
internal static class ReflectionAccessorFactory
{
    private const BindingFlags PropertyFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    [RequiresUnreferencedCode("Reads item properties through reflection; their metadata may be trimmed.")]
    public static KumikoPropertyAccessor? Create(Type type, string name)
    {
        var property = type.GetProperty(name, PropertyFlags);
        if (property == null || property.GetIndexParameters().Length > 0)
            return null;

        Func<object, object?>? getter = null;
        if (property.GetMethod != null)
        {
            getter = RuntimeFeature.IsDynamicCodeSupported
                ? CompileGetter(type, property)
                : property.GetValue;
        }

        Action<object, object?>? setter = property.SetMethod != null ? property.SetValue : null;

        return new KumikoPropertyAccessor(property.Name, property.PropertyType, getter, setter);
    }

    [RequiresUnreferencedCode("Reads item properties through reflection; their metadata may be trimmed.")]
    [RequiresDynamicCode("Compiles an expression tree.")]
    private static Func<object, object?> CompileGetter(Type type, PropertyInfo property)
    {
        var item = Expression.Parameter(typeof(object), "item");
        Expression body = Expression.Property(Expression.Convert(item, type), property);
        if (body.Type.IsValueType)
            body = Expression.Convert(body, typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, item).Compile();
    }
}
