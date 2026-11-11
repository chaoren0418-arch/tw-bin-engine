using P06.NativeUi;
namespace P06;

// T433（#991，classic-ui-audit §3 G1、G2、G6、G7、G8）離線測試：--classic-ui 入口可見。只測純函式與 WinForms 控制項清單，
// 不開視窗、不連線；原生 UI 在時（nativeActive=true）每一項都必須維持原行為。
static class ClassicEntryTests {
    [WorldModuleTest("classic-entry")]
    static void Run(WorldModuleTestContext t){
        // G1：死亡窗與「死亡選單」鈕不被搬進開發工具；HUD 照舊留下；其他控制項照舊搬走、順序不變。
        using(var hud=new Panel())using(var recovery=new Panel())using(var reopen=new Button())using(var other=new Label())using(var other2=new Label()){
            var keep=new HashSet<Control>{recovery,reopen};
            var moved=ClientForm.DevToolsMovedControls([other,recovery,hud,reopen,other2],hud,keep);
            t.Check(moved.Length==2&&moved[0]==other&&moved[1]==other2,"G1 scene-resident controls and HUD stay on the main window",moved.Select(c=>c.GetType().Name).ToArray());
            var none=ClientForm.DevToolsMovedControls([other,recovery],null,new HashSet<Control>());
            t.Check(none.Length==2,"G1 nothing registered → everything moves as before (diagnostic window)");
        }
        // G1：WinForms 死亡窗／死亡選單鈕的可見條件；原生面板可用時一律不顯示（:551 補 !native）。
        t.Check(ClientForm.ClassicRecoveryVisible(false,true,true,false),"G1 classic death window shows while dead");
        t.Check(!ClientForm.ClassicRecoveryVisible(true,true,true,false)&&!ClientForm.ClassicRecoveryVisible(true,true,false,true),"G1 native panel usable → WinForms window hidden");
        t.Check(!ClientForm.ClassicRecoveryVisible(false,false,true,true),"G1 outside the world → hidden");
        t.Check(ClientForm.ClassicDeathReopenVisible(false,true,true,false,true,false),"G1 reopen button after dismiss while dead");
        t.Check(!ClientForm.ClassicDeathReopenVisible(true,true,true,false,true,false),"G1 reopen button hidden with the native panel");
        t.Check(!ClientForm.ClassicDeathReopenVisible(false,true,true,true,true,false)&&!ClientForm.ClassicDeathReopenVisible(false,true,true,false,false,false)
            &&!ClientForm.ClassicDeathReopenVisible(false,true,false,false,true,false),"G1 reopen button: not after quit, not alive, not before dismiss");
        t.Check(!ClientForm.ClassicDeathReopenVisible(false,true,true,false,true,true),"G1 reopen button not doubled while the window is shown");

        // G2：不在世界中時自動開「登入／選角」一次（邊緣觸發）；原生 UI、自動化情境不動。
        t.Check(ClientForm.ShouldRevealClassicLogin(true,false,true,false),"G2 classic login screen → reveal");
        t.Check(!ClientForm.ShouldRevealClassicLogin(true,false,true,true),"G2 already on the login screen → not again (user may have hidden it)");
        t.Check(!ClientForm.ShouldRevealClassicLogin(true,true,true,false),"G2 native UI → unchanged");
        t.Check(!ClientForm.ShouldRevealClassicLogin(false,false,true,false),"G2 automation / diagnostic entry → unchanged");
        t.Check(!ClientForm.ShouldRevealClassicLogin(true,false,false,true),"G2 in world → no reveal");

        // G6：NPC 分頁三顆本機 HTML 頁按鈕涵蓋全部 MenuLocalPage。
        t.Check(ClientForm.LocalPageButtons.Select(b=>b.Page).Order().SequenceEqual(Enum.GetValues<MenuLocalPage>().Order()),"G6 dev-tools buttons cover help／option1／option2");
        t.Check(ClientForm.LocalPageButtons.All(b=>b.Text.Length>0),"G6 buttons labelled");

        // G8（與 G6 的快捷鍵）：classic 快捷鍵與 MenuButtonsPanel.CommandHotkeys 同一張表；Ctrl+A 不接；條件不符一律不接。
        MenuHotkeyCommand? C(Keys k,bool normal=true,bool native=false,bool focus=true,bool world=true)=>ClientForm.ClassicMenuHotkey(k,normal,native,focus,world);
        t.Equal<MenuHotkeyCommand?>(MenuHotkeyCommand.PlayStats,C(Keys.Control|Keys.Q),"G8 Ctrl+Q stats");
        t.Equal<MenuHotkeyCommand?>(MenuHotkeyCommand.PlayStats,C(Keys.Alt|Keys.X),"G8 Alt+X stats");
        t.Equal<MenuHotkeyCommand?>(MenuHotkeyCommand.Screenshot,C(Keys.Control|Keys.P),"G8 Ctrl+P capture");
        t.Equal<MenuHotkeyCommand?>(MenuHotkeyCommand.Help,C(Keys.Control|Keys.H),"G6 Ctrl+H help");
        t.Equal<MenuHotkeyCommand?>(MenuHotkeyCommand.Option1,C(Keys.Control|Keys.V),"G6 Ctrl+V option1");
        t.Equal<MenuHotkeyCommand?>(MenuHotkeyCommand.Option2,C(Keys.Control|Keys.C),"G6 Ctrl+C option2");
        t.Equal<MenuHotkeyCommand?>(null,C(Keys.Control|Keys.A),"Ctrl+A not taken in classic (HUD 「角色」)");
        t.Equal<MenuHotkeyCommand?>(null,C(Keys.Control|Keys.P,native:true),"native UI → classic branch never takes keys");
        t.Equal<MenuHotkeyCommand?>(null,C(Keys.Control|Keys.V,focus:false),"chat or text box focused → Ctrl+V left to the control");
        t.Equal<MenuHotkeyCommand?>(null,C(Keys.Control|Keys.Q,world:false),"outside the world → not taken");
        t.Equal<MenuHotkeyCommand?>(null,C(Keys.Control|Keys.Q,normal:false),"diagnostic entry → not taken");
        t.Equal<MenuHotkeyCommand?>(null,C(Keys.P),"plain P stays the pickup key");
        t.Check(!ClientForm.IsDevToolsKey(Keys.Control|Keys.Q,true)&&!ClientForm.IsDevToolsKey(Keys.Control|Keys.P,true),"classic hotkeys do not overlap the dev-tools keys");

        // G8：統計窗切換＝原生 MenuPlayStatsSource（沒顯示→手動開；顯示中→關，死亡窗開著就 Dismiss）。
        t.Equal((true,false),ClientForm.ClassicPlayStatsToggle(false,false),"G8 open by hand");
        t.Equal((false,false),ClientForm.ClassicPlayStatsToggle(true,false),"G8 close the hand-opened window");
        t.Equal((false,true),ClientForm.ClassicPlayStatsToggle(true,true),"G8 close while dead → dismiss the death window");
        t.Check(ClientForm.ClassicRecoveryVisible(false,true,false,true),"G8 hand-opened window shows while alive");

        // G7：S39 伺服器主動頁跳 NPC 分頁；同一版不重跳；換角色重算；舊呼叫（沒有 query 參數）不變。
        var tracker=new PlayerHudServiceTracker();var stamp=new PlayerHudStamp(1,1,123);
        string? Q(int rev,bool active)=>tracker.Observe(stamp,0,false,0,false,0,false,0,false,rev,active);
        t.Equal<string?>(null,Q(0,false),"G7 no page");
        t.Equal("NPC",Q(1,true),"G7 server query page → NPC tab");
        t.Equal<string?>(null,Q(1,true),"G7 same page → not again");
        t.Equal("NPC",Q(2,true),"G7 next page (link / new S39) → NPC tab");
        t.Equal<string?>(null,Q(3,false),"G7 page closed / local menu page → nothing");
        stamp=new PlayerHudStamp(2,1,123);
        t.Equal("NPC",Q(3,true),"G7 new character stamp → shown again");
        var legacy=new PlayerHudServiceTracker();
        t.Equal<string?>(null,legacy.Observe(stamp,0,false,0,false,0,false,0,false),"old call without query arguments unchanged");
        t.Equal("NPC",legacy.Observe(stamp,1,true,0,false,0,false,0,false),"NPC dialog still opens the NPC tab");
        t.Equal("交易",new PlayerHudServiceTracker().Observe(stamp,0,false,1,true,0,false,0,false,1,true),"trade still wins over the query page");
    }
}
