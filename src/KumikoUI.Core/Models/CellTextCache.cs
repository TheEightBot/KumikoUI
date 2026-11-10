namespace KumikoUI.Core.Models;

/// <summary>
/// One column's cache of cell values and display text for the rows the renderer draws, owned by a
/// <see cref="KumikoSource"/> and kept on the column (<see cref="KumikoColumn.TextCache"/>) so a cell finds it
/// without a lookup. Slots are indexed by visible row: frozen rows have one slot each, and scrolling rows share a
/// ring larger than the visible range, so rows on screen never evict each other. An entry is valid while it holds
/// the item now at its row and the source's current stamp.
/// </summary>
/// <remarks>
/// The cache is built for one value path and format: a change to the column's <see cref="KumikoColumn.PropertyName"/>,
/// <see cref="KumikoColumn.ValueAccessor"/> or <see cref="KumikoColumn.Format"/>, or to the registered accessors,
/// replaces it.
/// </remarks>
internal sealed class CellTextCache
{
    internal struct Entry
    {
        public object? Item;
        public object? Value;
        public string? Text;
        public int Stamp;
    }

    public CellTextCache(KumikoSource owner, KumikoColumn column, int layoutVersion, int size)
    {
        Owner = owner;
        PropertyName = column.PropertyName;
        Accessor = column.ValueAccessor;
        Format = column.Format;
        AccessorVersion = Data.KumikoAccessorRegistry.Version;
        LayoutVersion = layoutVersion;
        Entries = new Entry[size];
    }

    public KumikoSource Owner { get; }

    public string PropertyName { get; }

    public Func<object, object?>? Accessor { get; }

    public string? Format { get; }

    public int AccessorVersion { get; }

    /// <summary>The source's slot layout (frozen rows and ring size) the entries were indexed for.</summary>
    public int LayoutVersion { get; }

    public Entry[] Entries { get; }

    /// <summary>Whether the cache was built by <paramref name="owner"/> for <paramref name="column"/> as it is now.</summary>
    public bool IsCurrentFor(KumikoSource owner, KumikoColumn column, int layoutVersion) =>
        ReferenceEquals(Owner, owner)
        && LayoutVersion == layoutVersion
        && ReferenceEquals(PropertyName, column.PropertyName)
        && ReferenceEquals(Accessor, column.ValueAccessor)
        && ReferenceEquals(Format, column.Format)
        && AccessorVersion == Data.KumikoAccessorRegistry.Version;

    /// <summary>Mark every entry holding <paramref name="item"/> stale.</summary>
    public void Invalidate(object item)
    {
        var entries = Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (ReferenceEquals(entries[i].Item, item))
                entries[i].Stamp = 0;
        }
    }
}
