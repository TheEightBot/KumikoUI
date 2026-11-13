namespace KumikoUI.Core.Models;

/// <summary>
/// The type a <see cref="KumikoFilterConditionLayoutState.Value"/> is read back as, so a restored
/// <see cref="FilterCondition.Value"/> compares the way the saved one did.
/// </summary>
public enum KumikoFilterValueKind
{
    /// <summary>No value (null).</summary>
    None,

    /// <summary>Text, restored as a <see cref="string"/>. Values of other types are saved as their invariant text.</summary>
    Text,

    /// <summary>A number, saved in the invariant culture and restored as a <see cref="double"/>.</summary>
    Number,

    /// <summary>
    /// A <see cref="DateTime"/>, <see cref="DateTimeOffset"/> (its clock time) or <see cref="DateOnly"/>, saved in the
    /// round-trip format and restored as a <see cref="DateTime"/>.
    /// </summary>
    Date,

    /// <summary>A <see cref="bool"/>.</summary>
    Boolean,
}
