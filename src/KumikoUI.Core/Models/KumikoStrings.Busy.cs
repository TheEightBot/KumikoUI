namespace KumikoUI.Core.Models;

/// <summary>The text of the grid's busy indicator (#327).</summary>
public sealed partial record KumikoStrings
{
    /// <summary>The announcement when the grid becomes busy (<see cref="KumikoRenderer.IsBusy"/>). Default: "Loading".</summary>
    public string AnnounceBusy { get; init; } = "Loading";
}
