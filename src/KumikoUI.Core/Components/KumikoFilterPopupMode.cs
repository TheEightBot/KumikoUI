namespace KumikoUI.Core.Components;

/// <summary>The page a <see cref="DrawnFilterPopup"/> shows below its sort rows (#325).</summary>
public enum KumikoFilterPopupMode
{
    /// <summary>The search box and the checklist of the column's distinct values.</summary>
    Values,

    /// <summary>Up to two conditions (an operator and a typed value each), combined with And or Or.</summary>
    Conditions,
}
