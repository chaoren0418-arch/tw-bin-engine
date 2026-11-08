namespace P06.NativeUi;

/// <summary>One native panel: Layout is the Tile.pak XML base name (UiXmlSource tries "&lt;name&gt;-c.xml" first).</summary>
public sealed record UiPanelRegistration(string Id,string Layout,Func<UiRoot,IUiPanel> Create);

/// <summary>
/// The single integration point for native UI modules (§5.1). Each module task adds exactly ONE line to Panels; on a merge
/// conflict keep both sides' lines. Order only breaks z-order ties inside one layer (later = on top).
/// </summary>
public static class UiPanelRegistry {
    public static IReadOnlyList<UiPanelRegistration> Panels{get;}=[
        new("LEFT_WINDOW","uistatus86",root=>new LeftStatusPanel()),
        new("CENTER_WINDOW","uistatus86",root=>new ChatPanel()),
        new(SummonPanel.PanelId,SummonPanel.LayoutName,root=>new SummonPanel(root.Resources)),   // ui-summon (M7-W, #530)
        new(HpMpPanel.PanelId,"uistatus86",root=>new HpMpPanel()),new(HpMpHeadBarPanel.PanelId,"uistatus86",root=>new HpMpHeadBarPanel(()=>(root.Find(HpMpPanel.PanelId) as HpMpPanel)?.Settings??HpMpSettings.Default)),   // ui-hpmp #465 (no XML: V1 hard-coded layout)
        new(MiniMapPanel.PanelId,"uistatus86",root=>MiniMapPanel.Create(root)),   // ui-minimap (#470); also adds Field_Map; BAR_485 member, before RIGHT_WINDOW
        new(ExMacroPanel.WindowId,"uistatus86",root=>new ExMacroPanel(root.SlotGroup(UiSlotGroup.Bar485))),new(MacroPanel.WindowId,"uistatus86",_=>new MacroPanel()),   // ui-macro-bar #466 (RIGHT_WINDOW last: MACRO_LOCK overlaps BAR_485)
        new(TradeWindowPanel.PanelId,TradeWindowPanel.LayoutName,root=>new TradeWindowPanel()),   // M7-S (#526)
        ..FriendPanels.Registrations,   // M7-V (#529): Friend_Window／Exclude_Window／Mailexclude_Window
        new(MenuButtonsPanel.PanelId,"uistatus86",_=>new MenuButtonsPanel()),   // ui-menu-buttons #467: after MacroPanel (buttons y=567 lie inside the 3176 frame; later Bar = painted/hit on top)
    ];
}
