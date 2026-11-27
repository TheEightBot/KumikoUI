namespace KumikoUI.Core.Models;

/// <summary>Why the grid shows no rows, which decides the text of the empty view (#326).</summary>
public enum KumikoEmptyViewKind
{
    /// <summary>The bound collection holds no items (or none is bound yet).</summary>
    NoItems = 0,

    /// <summary>The collection holds items, but the column filters, the filter predicate or the search hide every one.</summary>
    NoMatches = 1,
}
