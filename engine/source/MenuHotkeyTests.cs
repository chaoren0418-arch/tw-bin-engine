using System.Reflection;
using P06.NativeUi;
namespace P06;

// R4-A（T293／#705）離線測試：選單鈕 1 開信件、5／6 開本機 HTML option2（王族 option2p）／option1、2 與 Ctrl+A 開「角色」分頁，
// Ctrl+Q／Alt+X 切換遊玩統計窗，Ctrl+H／V／C 開 help／option1／option2，Ctrl+P 截圖（存到 <stage>/runtime/Capture）。
// 依據 FU4§1.1（TW13 sub_4D3910、off_8F023C）。只用假指令物件，不建 ClientForm、不讀客戶端、不開視窗。
// 由 --module-tests 執行（名稱 menu-hotkeys）。
static class MenuHotkeyTests {
    sealed class Commands:IUiCommands,IUiMenuCommands,IUiMenuWindowCommands {
        public readonly List<string> Calls=[];
        public bool Available=true;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool TogglePlayStats(){Calls.Add("play-stats");return Available;}
        public bool OpenMail(){Calls.Add("mail");return Available;}
        public bool ToggleLocalPage(MenuLocalPage page){Calls.Add("page:"+page);return Available;}
        public bool CaptureScreen(){Calls.Add("capture");return Available;}
    }
    // 舊的指令物件（只認得遊玩統計窗）：信件與 HTML 頁要退回開發工具分頁。
    sealed class StatsOnly:IUiCommands,IUiMenuCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool TogglePlayStats(){Calls.Add("play-stats");return true;}
    }

    static UiKeyEvent Down(Keys k,bool repeat=false)=>new(k,true,repeat);
    static UiKeyEvent Up(Keys k)=>new(k,false,false);

    [WorldModuleTest("menu-hotkeys")]
    static void Run(WorldModuleTestContext t){
        // 1. 選單對應表（FU4§1.1）。
        var a=MenuButtonsPanel.Actions;
        t.Check(a[1].Mail&&a[1].Page==null&&a[1].Window==null,"1 → mail (NewMailWindow)");
        t.Check(a[5].Page==MenuLocalPage.Option2&&a[6].Page==MenuLocalPage.Option1&&!a[5].Mail&&!a[6].Mail,"5 → HTML option2, 6 → HTML option1");
        t.Check(!a[2].Mail&&a[2].Page==null&&a[2].Window==null&&a[2].DevToolsPage=="角色","2 → dev-tools 角色 page (CRenewalStatusUI XML missing, WIN§1.4)");
        t.Check(a.Values.Count(x=>x.Mail)==1&&a.Values.Count(x=>x.Page!=null)==2&&a.Values.Count(x=>x.PlayStats)==1,"one mail, two HTML pages, one play-stats action");
        t.Check(new[]{1,2,5,6}.All(k=>a[k].DevToolsPage!=null),"1/2/5/6 keep a dev-tools fallback page");

        // 2. 熱鍵表：WM_CHAR 表的 7 個鍵；與既有視窗熱鍵（Tab／Ctrl+I／Ctrl+S）不重疊；Ctrl+M、單鍵不收。
        var h=MenuButtonsPanel.CommandHotkeys;
        t.Equal(7,h.Count,"seven command hotkeys");
        t.Check(h[Keys.Control|Keys.A]==MenuHotkeyCommand.Status&&h[Keys.Control|Keys.Q]==MenuHotkeyCommand.PlayStats&&h[Keys.Alt|Keys.X]==MenuHotkeyCommand.PlayStats
            &&h[Keys.Control|Keys.H]==MenuHotkeyCommand.Help&&h[Keys.Control|Keys.V]==MenuHotkeyCommand.Option1&&h[Keys.Control|Keys.C]==MenuHotkeyCommand.Option2
            &&h[Keys.Control|Keys.P]==MenuHotkeyCommand.Screenshot,"Ctrl+A status, Ctrl+Q／Alt+X stats, Ctrl+H help, Ctrl+V option1, Ctrl+C option2, Ctrl+P capture");
        t.Check(!h.Keys.Any(MenuButtonsPanel.Hotkeys.ContainsKey),"no overlap with the window hotkeys");
        t.Check(!h.ContainsKey(Keys.Control|Keys.M)&&!h.ContainsKey(Keys.Back)&&!h.ContainsKey(Keys.H)&&!h.ContainsKey(Keys.Control|Keys.O),"Ctrl+M, Backspace, single H and Ctrl+O not taken");

        // 3. 本機 HTML 頁名：王族（職業 0）看 option2p。
        t.Equal("help",MenuButtonsPanel.PageKey(MenuLocalPage.Help,0),"help page");
        t.Equal("option1",MenuButtonsPanel.PageKey(MenuLocalPage.Option1,0),"option1 page (same for 王族)");
        t.Equal("option2p",MenuButtonsPanel.PageKey(MenuLocalPage.Option2,0),"王族 → option2p");
        t.Check(new int?[]{1,2,3,4,5,6,null}.All(c=>MenuButtonsPanel.PageKey(MenuLocalPage.Option2,c)=="option2"),"other classes / unknown class → option2");

        // 4. 截圖路徑：只在 <stage>/runtime/Capture 下；名稱重複時加序號。
        string stage=Path.GetFullPath(Path.Combine("stage-root-for-test"));
        var now=new DateTime(2026,11,10,9,8,7,654);
        string first=MenuButtonsPanel.CapturePath(stage,now,_=>false);
        t.Equal(Path.Combine(stage,"runtime","Capture","20261110-090807-654.png"),first,"capture file under runtime/Capture");
        var taken=new HashSet<string>(StringComparer.OrdinalIgnoreCase){first,Path.Combine(stage,"runtime","Capture","20261110-090807-654-2.png")};
        t.Equal(Path.Combine(stage,"runtime","Capture","20261110-090807-654-3.png"),MenuButtonsPanel.CapturePath(stage,now,taken.Contains),"existing names are skipped, never overwritten");
        t.Check(Path.GetFullPath(first).StartsWith(Path.GetFullPath(MenuButtonsPanel.CaptureDirectory(stage))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"capture stays inside the stage capture folder");

        // 5. 熱鍵經過選單面板（原版在視窗堆疊之後統一分派）。
        var root=new UiRoot();var menu=new MenuButtonsPanel(root);var cmd=new Commands();
        menu.Update(new UiStaticGameState(InWorld:true));
        foreach(var (key,expected) in new (Keys,string)[]{
            (Keys.Control|Keys.A,"dev-tools:角色"),(Keys.Control|Keys.Q,"play-stats"),(Keys.Alt|Keys.X,"play-stats"),
            (Keys.Control|Keys.H,"page:Help"),(Keys.Control|Keys.V,"page:Option1"),(Keys.Control|Keys.C,"page:Option2"),(Keys.Control|Keys.P,"capture")}){
            cmd.Calls.Clear();
            bool used=menu.Key(Down(key),cmd);
            t.Check(used&&cmd.Calls.SequenceEqual(new[]{expected}),key+" → "+expected,cmd.Calls.ToArray());
            cmd.Calls.Clear();
            t.Check(menu.Key(Up(key),cmd)&&menu.Key(Down(key,repeat:true),cmd)&&cmd.Calls.Count==0,key+": key up swallowed, auto-repeat ignored",cmd.Calls.ToArray());
        }
        cmd.Calls.Clear();
        t.Check(!menu.Key(Down(Keys.Control|Keys.O),cmd)&&!menu.Key(Down(Keys.X),cmd)&&!menu.Key(Down(Keys.Control|Keys.X),cmd)&&cmd.Calls.Count==0,"unlisted keys not taken");

        // 6. 選單鈕：1 開信件、5／6 開 HTML、2 開角色分頁、7 開統計窗；都不登出。
        cmd.Calls.Clear();
        foreach(int k in new[]{1,5,6,2,7})MenuButtonsPanel.Invoke(a[k],cmd,root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"mail","page:Option2","page:Option1","dev-tools:角色","play-stats"}),"menu 1/5/6/2/7 → mail, option2, option1, 角色, play stats",cmd.Calls.ToArray());
        t.Check(!cmd.Calls.Contains("logout"),"no menu action logs out");

        // 7. 橋接做不到（不在世界、模板沒載入）：退回開發工具分頁；Ctrl+H 退回 NPC 分頁；Ctrl+P 什麼都不做。
        cmd.Available=false;cmd.Calls.Clear();
        foreach(int k in new[]{1,5,6})MenuButtonsPanel.Invoke(a[k],cmd,root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"mail","dev-tools:信件","page:Option2","dev-tools:血盟","page:Option1","dev-tools:選項"}),"bridge refuses → dev-tools fallback page",cmd.Calls.ToArray());
        cmd.Calls.Clear();
        MenuButtonsPanel.Run(MenuHotkeyCommand.Help,cmd,root);MenuButtonsPanel.Run(MenuHotkeyCommand.Screenshot,cmd,root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"page:Help","dev-tools:NPC","capture"}),"help refused → NPC page; capture refused → nothing else",cmd.Calls.ToArray());
        cmd.Available=true;

        // 8. 只有舊介面（IUiMenuCommands）的指令物件：信件與 HTML 頁走開發工具；包在 UiMacroCommands 裡也找得到新介面。
        var old=new StatsOnly();
        foreach(int k in new[]{1,5,6})MenuButtonsPanel.Invoke(a[k],old,root);
        MenuButtonsPanel.Run(MenuHotkeyCommand.Help,old,root);MenuButtonsPanel.Run(MenuHotkeyCommand.Screenshot,old,root);
        t.Check(old.Calls.SequenceEqual(new[]{"dev-tools:信件","dev-tools:血盟","dev-tools:選項","dev-tools:NPC"}),"without IUiMenuWindowCommands → dev-tools pages, capture skipped",old.Calls.ToArray());
        cmd.Calls.Clear();
        var wrapped=new UiMacroCommands(cmd,(_,_)=>{});
        MenuButtonsPanel.Invoke(a[1],wrapped,root);MenuButtonsPanel.Run(MenuHotkeyCommand.Screenshot,wrapped,root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"mail","capture"}),"window commands found through UiMacroCommands",cmd.Calls.ToArray());

        // 9. 離開世界：新熱鍵也不收。
        menu.Update(new UiStaticGameState(InWorld:false));cmd.Calls.Clear();
        t.Check(!menu.Key(Down(Keys.Control|Keys.P),cmd)&&!menu.Key(Down(Keys.Alt|Keys.X),cmd)&&cmd.Calls.Count==0,"outside the world the command hotkeys are not taken");

        // 10. 產品橋接指令實作新介面（離線不建 ClientForm，只查型別）；信件入口在 ClientForm 上（原生 UI 關閉時舊 HUD 按鈕也用它）。
        var bridge=typeof(ClientForm).GetNestedType("NativeUiCommands",BindingFlags.NonPublic);
        t.Check(bridge!=null&&typeof(IUiMenuWindowCommands).IsAssignableFrom(bridge)&&typeof(IUiMenuCommands).IsAssignableFrom(bridge),"bridge commands implement IUiMenuWindowCommands");
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        t.Check(typeof(ClientForm).GetMethod("RequestMail",flags)?.ReturnType==typeof(bool)&&typeof(ClientForm).GetMethod("OpenMail",flags)!=null,"ClientForm.RequestMail／OpenMail exist");
        t.Check(typeof(ClientForm).GetMethod("ToggleMenuLocalPage",flags)!=null&&typeof(ClientForm).GetMethod("CaptureMenuScreen",flags)!=null,"ClientForm page toggle and capture exist");
    }
}
