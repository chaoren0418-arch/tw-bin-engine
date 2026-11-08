namespace P06.NativeUi;

/// <summary>One native panel: Layout is the Tile.pak XML base name (UiXmlSource tries "&lt;name&gt;-c.xml" first).</summary>
public sealed record UiPanelRegistration(string Id,string Layout,Func<UiRoot,IUiPanel> Create);

/// <summary>
/// The single integration point for native UI modules (§5.1). Each module task adds exactly ONE line to Panels; on a merge
/// conflict keep both sides' lines. Order only breaks z-order ties inside one layer (later = on top).
/// </summary>
public static class UiPanelRegistry {
    public static IReadOnlyList<UiPanelRegistration> Panels{get;}=[
        // new("LEFT_WINDOW","uistatus86",root=>new LeftStatusPanel()),   ← example (ui-left-status)
        new("CENTER_WINDOW","uistatus86",root=>new ChatPanel()),
    ];
}
