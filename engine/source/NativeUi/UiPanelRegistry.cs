namespace P06.NativeUi;

/// <summary>One native panel: Layout is the Tile.pak XML base name (UiXmlSource tries "&lt;name&gt;-c.xml" first).</summary>
public sealed record UiPanelRegistration(string Id,string Layout,Func<UiRoot,IUiPanel> Create);

/// <summary>
/// The native UI panel list (§5.1). T270 (#645): built automatically from every static member marked [UiPanel(order)]
/// (AutoDiscovery.cs). Do NOT add lines here — declare the registration next to the panel. Order only breaks z-order ties
/// inside one layer (later = on top).
/// </summary>
public static class UiPanelRegistry {
    static readonly UiPanelDiscovery.Result discovered=UiPanelDiscovery.Discover(typeof(UiPanelRegistry).Assembly);
    public static IReadOnlyList<UiPanelRegistration> Panels=>discovered.Panels;
    /// <summary>Invalid [UiPanel] members (skipped). The auto-discovery module test requires this to be empty.</summary>
    public static IReadOnlyList<string> Errors=>discovered.Errors;
}
