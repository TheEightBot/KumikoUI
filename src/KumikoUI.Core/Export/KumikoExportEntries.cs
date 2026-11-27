using KumikoUI.Core.Models;

namespace KumikoUI.Core.Export;

/// <summary>A group of the export layout, copied from the grid's group header so the snapshot never reads live state.</summary>
internal sealed class ExportGroup
{
    public ExportGroup(GroupHeaderInfo info, ExportGroup? parent)
    {
        Level = info.Level;
        Parent = parent;
        KeyText = info.DisplayText;
        string title = info.GetTitle() + info.GetCountText();
        string? caption = info.GetCaptionLabel(info.CaptionText);
        Title = caption == null ? title : title + caption;
    }

    public int Level { get; }

    public ExportGroup? Parent { get; }

    /// <summary>The text of the group's key, as the group header shows it.</summary>
    public string KeyText { get; }

    /// <summary>The group header's text: "Header: Key (count)" and the caption summary.</summary>
    public string Title { get; }

    /// <summary>The key text of the enclosing group (or this one) at <paramref name="level"/>.</summary>
    public string KeyTextAt(int level)
    {
        for (var g = this; g != null; g = g.Parent)
            if (g.Level == level) return g.KeyText;
        return string.Empty;
    }
}

/// <summary>One row of the export layout; what a row is made of is read from the item when the row is enumerated.</summary>
internal readonly struct ExportEntry(KumikoExportRowKind kind, int level, object? item, ExportGroup? group,
    ComputedSummaryRow? summary)
{
    public KumikoExportRowKind Kind { get; } = kind;
    public int Level { get; } = level;
    public object? Item { get; } = item;
    public ExportGroup? Group { get; } = group;
    public ComputedSummaryRow? Summary { get; } = summary;
}
