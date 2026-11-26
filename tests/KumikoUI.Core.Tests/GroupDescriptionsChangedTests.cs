using KumikoUI.Core.Models;

namespace KumikoUI.Core.Tests;

/// <summary>The internal change notification hosts use to mirror grouping changes made from the UI.</summary>
public class GroupDescriptionsChangedTests
{
    [Fact]
    public void EachMutator_RaisesOnce_AndSetGroupDescriptionsRaisesOncePerBatch()
    {
        var source = new KumikoSource();
        int raised = 0;
        source.GroupDescriptionsChanged += (_, _) => raised++;

        source.AddGroupDescription(new GroupDescription("A"));
        source.InsertGroupDescription(0, new GroupDescription("B"));
        source.RemoveGroupDescription("A");
        source.ClearGroupDescriptions();
        Assert.Equal(4, raised);

        raised = 0;
        source.SetGroupDescriptions(new[] { new GroupDescription("A"), new GroupDescription("B") });
        Assert.Equal(1, raised);
        Assert.Equal(2, source.GroupDescriptions.Count);
    }
}
