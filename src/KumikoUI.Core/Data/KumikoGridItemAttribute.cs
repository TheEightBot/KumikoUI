namespace KumikoUI.Core.Data;

/// <summary>
/// Generates reflection-free accessors for the public properties of this type, so the grid can read
/// and write them under Native AOT and full trimming. The <c>KumikoUI.Core</c> package's source
/// generator emits the accessors and registers them with <see cref="KumikoAccessorRegistry"/> when the
/// assembly loads.
/// </summary>
/// <remarks>
/// Apply it to every type a column path walks through: for <c>PropertyName="Address.City"</c>, mark both
/// the row type and the type of <c>Address</c>. Generic, private and file-local types are not supported;
/// use <see cref="Models.KumikoColumn.ValueAccessor"/> or an <see cref="IKumikoAccessorProvider"/> for them.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class KumikoGridItemAttribute : Attribute
{
}
