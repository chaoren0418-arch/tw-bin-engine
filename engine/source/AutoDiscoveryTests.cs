using P06.NativeUi;
namespace P06;

// T270（#645）：自動發現的離線測試（不連遊戲、網路或素材）。由 --module-tests 執行（名稱 auto-discovery）。
static class AutoDiscoveryTests {
    /// <summary>T270 前 UiPanelRegistry.cs 手寫清單的順序與版面（遷移不得改變；新面板可以插在任何位置）。</summary>
    static (string Id,string Layout)[] LegacyPanels()=>[
        ("LEFT_WINDOW","uistatus86"),("CENTER_WINDOW","uistatus86"),(SummonPanel.PanelId,SummonPanel.LayoutName),
        (HpMpPanel.PanelId,"uistatus86"),(HpMpHeadBarPanel.PanelId,"uistatus86"),(MiniMapPanel.PanelId,"uistatus86"),
        (ExMacroPanel.WindowId,"uistatus86"),(MacroPanel.WindowId,"uistatus86"),
        (PartyPanel.WindowId,"uiextra86"),(PartyRtPanel.WindowId,"uiextra86"),(TradeWindowPanel.PanelId,TradeWindowPanel.LayoutName),
        ..FriendWindowSpec.All.Select(s=>(s.Id,s.Layout)),
        (MenuButtonsPanel.PanelId,"uistatus86"),
        (BuffIconsPanel.PanelId,BuffEffectTable.FileName),
    ];

    [WorldModuleTest("auto-discovery")]
    static void Run(WorldModuleTestContext t){
        var panels=UiPanelRegistry.Panels;
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations are all valid",UiPanelRegistry.Errors);
        t.Check(panels.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()==panels.Count,"panel ids unique");
        t.Check(panels.All(p=>p.Id.Length>0&&p.Layout.Length>0&&p.Create!=null),"every registration names its id, XML and factory");

        // Migration: every pre-T270 line is still registered once, same layout, same relative order.
        var legacy=LegacyPanels();
        foreach(var (id,layout) in legacy)t.Check(panels.Count(p=>p.Id==id&&p.Layout==layout)==1,"legacy panel registered once: "+id);
        var positions=legacy.Select(l=>panels.ToList().FindIndex(p=>p.Id==l.Id)).ToArray();
        t.Check(positions.All(i=>i>=0)&&positions.Zip(positions.Skip(1)).All(x=>x.First<x.Second),"legacy panels keep their order",positions);
        t.Check(ReferenceEquals(UiPanelRegistry.Panels,panels),"Panels is computed once");

        // Determinism: the result does not depend on reflection enumeration order.
        var types=typeof(UiPanelRegistry).Assembly.GetTypes().Where(x=>!ExcludeFromDiscoveryAttribute.Excluded(x)).ToArray();
        var forward=UiPanelDiscovery.Discover(types).Panels.Select(p=>p.Id);
        var backward=UiPanelDiscovery.Discover(types.Reverse()).Panels.Select(p=>p.Id);
        t.Check(forward.SequenceEqual(panels.Select(p=>p.Id))&&backward.SequenceEqual(forward),"discovery order is reproducible");

        // Fixtures: excluded from the assembly scan, scanned only when passed explicitly.
        t.Check(panels.All(p=>!p.Id.StartsWith("FIXTURE_",StringComparison.Ordinal)),"[ExcludeFromDiscovery] fixtures stay out of the product");
        var r=UiPanelDiscovery.Discover([typeof(Fixtures.Good),typeof(Fixtures.Bad)]);
        t.Equal("FIXTURE_A,FIXTURE_B1,FIXTURE_B2,FIXTURE_C",string.Join(",",r.Panels.Select(p=>p.Id)),"order: Order, then type, then member; lists keep their order");
        t.Equal(5,r.Errors.Count,"wrong type, duplicate id, blank layout, throwing getter and parameterised method are reported");
        t.Check(r.Errors.Any(e=>e.Contains("WrongType")&&e.Contains("型別"))&&r.Errors.Any(e=>e.Contains("重複")&&e.Contains("FIXTURE_A"))
            &&r.Errors.Any(e=>e.Contains("Blank"))&&r.Errors.Any(e=>e.Contains("Throws")&&e.Contains("boom"))&&r.Errors.Any(e=>e.Contains("WithArgument")),"error messages name the member",r.Errors);
        t.Check(r.Panels.Single(p=>p.Id=="FIXTURE_C").Create(null!) is Fixtures.Panel {Id:"FIXTURE_C"},"factory is the declared one");
        t.Check(UiPanelDiscovery.Discover([typeof(Fixtures.Good)]).Errors.Count==0,"valid fixture alone has no errors");
    }

    [ExcludeFromDiscovery]
    static class Fixtures {
        internal sealed class Panel(string id):IUiPanel{
            public string Id=>id;public UiLayer Layer=>UiLayer.Bar;public Rectangle Bounds=>Rectangle.Empty;public bool Visible=>false;
            public void Load(UiLayout layout,SurfCatalog surfs){}public void Update(IUiGameState state){}public void Paint(UiCanvas canvas){}
            public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
        }
        static UiPanelRegistration Reg(string id,string layout="uistatus86")=>new(id,layout,_=>new Panel(id));
        internal static class Good {
            [UiPanel(30)] static UiPanelRegistration C=>Reg("FIXTURE_C");
            [UiPanel(10)] static readonly UiPanelRegistration A=Reg("FIXTURE_A");
            [UiPanel(20)] static IEnumerable<UiPanelRegistration> B()=>[Reg("FIXTURE_B1"),Reg("FIXTURE_B2")];
        }
        internal static class Bad {
            [UiPanel(1)] static string WrongType=>"x";
            [UiPanel(40)] static UiPanelRegistration Duplicate=>Reg("FIXTURE_A");
            [UiPanel(41)] static UiPanelRegistration Blank=>Reg("FIXTURE_BLANK","");
            [UiPanel(42)] static UiPanelRegistration Throws=>throw new InvalidOperationException("boom");
            [UiPanel(43)] static UiPanelRegistration WithArgument(int x)=>Reg("FIXTURE_ARG");
        }
    }
}
