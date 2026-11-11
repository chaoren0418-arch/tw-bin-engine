using P06.NativeUi;
namespace P06;

// T436（#994，classic-ui-audit §3 G16–G19）離線測試：原生面板故障或不可用時退回 WinForms 的條件一致。
// 只測純函式與未載入的 UiRoot（合成面板），不讀客戶端、不開視窗、不連線。由 --module-tests 執行（名稱 fallback-consistency）。
// 原生 UI 正常（面板已註冊、沒故障）時每一項都必須維持原行為：交給原生窗、WinForms 不出現。
static class FallbackConsistencyTests {
    sealed class Stub(string id):IUiPanel {
        public string Id=>id;
        public UiLayer Layer=>UiLayer.Window;
        public Rectangle Bounds=>Rectangle.Empty;
        public bool Visible=>false;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }

    [WorldModuleTest("fallback-consistency")]
    static void Run(WorldModuleTestContext t){
        // G16：同一條規則（比照 QuitFallbackUi）：原生 UI 開啟、面板已註冊、沒故障 → 原生；其他 → 備援。
        foreach(var id in new[]{SummonPanel.PanelId,CookBookPanel.PanelId,ClanWarehouseHistoryPanel.PanelId}){
            var root=new UiRoot();var panel=new Stub(id);root.Add(panel);
            t.Check(ClientForm.NativePanelUsable(true,root,id),"G16 "+id+": registered, healthy → native (unchanged)");
            t.Check(!ClientForm.NativePanelUsable(false,root,id),"G16 "+id+": --classic-ui → fallback");
            t.Check(!ClientForm.NativePanelUsable(true,null,id),"G16 "+id+": no root → fallback");
            t.Check(!ClientForm.NativePanelUsable(true,new UiRoot(),id),"G16 "+id+": not registered → fallback");
            root.Fault(panel,"test",new InvalidOperationException("synthetic"));
            t.Check(!ClientForm.NativePanelUsable(true,root,id),"G16 "+id+": faulted → fallback (was: neither side showed)");
        }
        // G16：召喚——備援條件跟著「面板可用」走；HUD 不在（原生 UI 開著、面板故障）時改從開發工具開寵物分頁。
        var summon=new SummonView(1,1,5,true,"server",0,50,12,true,true,"");
        t.Check(SummonFallback.ShouldReveal(summon,false,-1),"G16 summon: native panel unusable → reveal pet tab");
        t.Check(!SummonFallback.ShouldReveal(summon,true,-1),"G16 summon: native panel usable → no reveal (unchanged)");
        t.Check(!SummonFallback.ShouldReveal(summon,false,5),"G16 summon: same revision → once");
        t.Check(SummonFallback.RevealInDevTools(true)&&!SummonFallback.RevealInDevTools(false),"G16 summon: native on + faulted → dev tools; classic → HUD service window");
        // G16：料理書——Wanted 的第一個參數是「原生料理窗可用」。
        var book=new CookBookState(true,0,3,0,7);
        t.Check(CookBookFallbackView.Wanted(false,true,book,-1),"G16 cook book: native panel unusable → WinForms window");
        t.Check(!CookBookFallbackView.Wanted(true,true,book,-1),"G16 cook book: native usable → no WinForms window (unchanged)");
        t.Check(!CookBookFallbackView.Wanted(false,true,book,7),"G16 cook book: dismissed revision stays closed");

        // G17：倉庫——除了故障，還要看這一次的清單是不是已經被 UiRoot 關掉（沒送取消）。
        t.Check(!ClientForm.WarehouseDismissed(null,false,4),"G17 list not picked up yet → not dismissed");
        t.Check(!ClientForm.WarehouseDismissed(3,false,4),"G17 older revision shown → new list not dismissed");
        t.Check(!ClientForm.WarehouseDismissed(4,true,4),"G17 shown and open → native");
        t.Check(ClientForm.WarehouseDismissed(4,false,4),"G17 shown, closed, interaction still open → dismissed");
        {
            var root=new UiRoot();var panel=new WarehouseWindowPanel();root.Add(panel);
            t.Check(ClientForm.NativeWarehouseActive(true,"withdraw",4,root)&&ClientForm.NativeWarehouseActive(true,"deposit",4,root),"G17 healthy panel, list not picked up → native (unchanged)");
            t.Check(!ClientForm.NativeWarehouseActive(true,"closed",4,root)&&!ClientForm.NativeWarehouseActive(true,"shop",4,root),"G17 not a warehouse list → WinForms NPC path");
            t.Check(!ClientForm.NativeWarehouseActive(false,"withdraw",4,root),"G17 --classic-ui → WinForms list");
            t.Check(!ClientForm.NativeWarehouseActive(true,"withdraw",4,new UiRoot()),"G17 not registered → WinForms list");
            root.Fault(panel,"test",new InvalidOperationException("synthetic"));
            t.Check(!ClientForm.NativeWarehouseActive(true,"withdraw",4,root),"G17 faulted → WinForms list");
        }

        // G18：原生 UI 開著，但這個狀態要的畫面（LoginUI／SelectUI）不能用 → 開「登入／選角」一次。
        t.Check(ClientForm.NativeLoginScreenMissing(LoginState.Offline,false,true),"G18 login screen without LoginUI → missing");
        t.Check(!ClientForm.NativeLoginScreenMissing(LoginState.Offline,true,false),"G18 login screen with LoginUI → fine (SelectUI not needed yet)");
        t.Check(ClientForm.NativeLoginScreenMissing(LoginState.Disconnected,false,true)&&ClientForm.NativeLoginScreenMissing(LoginState.Faulted,false,true),"G18 disconnected / faulted go back to the login screen");
        t.Check(ClientForm.NativeLoginScreenMissing(LoginState.CharacterList,true,false),"G18 character list without SelectUI → missing");
        t.Check(ClientForm.NativeLoginScreenMissing(LoginState.SwitchingCharacter,true,false)&&ClientForm.NativeLoginScreenMissing(LoginState.ManagingCharacters,true,false),"G18 switching / managing characters need SelectUI");
        t.Check(!ClientForm.NativeLoginScreenMissing(LoginState.CharacterList,false,true),"G18 character list with SelectUI → fine");
        t.Check(!ClientForm.NativeLoginScreenMissing(LoginState.InWorld,false,false)&&!ClientForm.NativeLoginScreenMissing(LoginState.EnteringWorld,false,false),"G18 in / entering the world → nothing missing");
        t.Check(ClientForm.ShouldRevealNativeLoginFallback(true,true,true,false),"G18 native on, screen missing → reveal");
        t.Check(!ClientForm.ShouldRevealNativeLoginFallback(true,true,true,true),"G18 still missing → not again (user may have hidden it)");
        t.Check(!ClientForm.ShouldRevealNativeLoginFallback(true,true,false,false),"G18 native screens usable → unchanged");
        t.Check(!ClientForm.ShouldRevealNativeLoginFallback(true,false,true,false),"G18 --classic-ui → G2 handles it, not here");
        t.Check(!ClientForm.ShouldRevealNativeLoginFallback(false,true,true,false),"G18 automation / diagnostic entry → unchanged");

        // G19：classic HUD「信件」鈕走 RequestMail（排隊；與原生選單鈕 1 同一個入口）。離線不建 ClientForm，只查 RequestMail 仍是內部入口。
        var request=typeof(ClientForm).GetMethod("RequestMail",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        t.Check(request!=null&&request.ReturnType==typeof(bool)&&request.GetParameters().Length==0,"G19 RequestMail() is the shared mail entry");
    }
}
