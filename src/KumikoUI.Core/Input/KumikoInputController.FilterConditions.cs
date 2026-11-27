using KumikoUI.Core.Components;

namespace KumikoUI.Core.Input;

public sealed partial class KumikoInputController
{
    /// <summary>
    /// Wires the filter popup's condition filters (#325): its value boxes copy and paste through <see cref="Clipboard"/>
    /// like the search box, and its announcements (the page, the operator, where Tab moved) reach the host's screen
    /// reader through <see cref="AnnouncementRequested"/>.
    /// </summary>
    private void WireFilterConditions(DrawnFilterPopup popup)
    {
        popup.ValueCopyRequested += OnEditorCopyRequested;
        popup.ValuePasteRequested += (_, e) =>
            ReadClipboardForPaste(e, popup, static (p, text) => p.PasteValueText(text), static p => p.ActiveTextInput);
        popup.AnnouncementRequested += Announce;
    }
}
