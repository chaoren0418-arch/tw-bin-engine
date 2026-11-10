using P06.NativeUi;
namespace P06;

// R7-I（T348／#811）離線測試：選單鈕 2／Ctrl+A 改開原生狀態窗（不可用才開開發工具「角色」分頁），選單熱鍵經 UiRoot.Toggle，
// op 43（3.81 S64 S_CharReset 子類 1／2）關 StatInfo 窗，StatInfo 數值接線。依據 FU4§1.1、WIN2§6、#766、#763 報告與
// scratch/issue-811 的靜態分析（sub_94E0E0 → StatInfoUI vtable+0x44 = SetShow(0)）。
// 版面 XML 與圖片全是記憶體內合成資料；不讀客戶端或參考客戶端、不建 ClientForm、不開視窗、不連網路。由 --module-tests 執行。
static class StatusWindowEntryTests {
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF303030u,w*h).ToArray()));
    static UiKeyEvent Down(Keys k,bool repeat=false)=>new(k,true,repeat);
    static UiKeyEvent Up(Keys k)=>new(k,false,false);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static readonly UiStaticGameState World=new(InWorld:true);

    const string StatusXml="""
        <RenewalStatusUI>
          <Window Name="Status_Window" X="100" Y="50" BasePng="903" Moveable="20">
            <Button Name="CloseWindow_Button" X="160" Y="5" BaseSurf="902" MouseEvent="CloseWindow"/>
            <CheckButton Name="StatusBonus_Button" X="10" Y="10" BasePng="904" PushPng="904" MouseEvent="ShowStatInfoTooltip"/>
          </Window>
        </RenewalStatusUI>
        """;
    static RenewalStatusPanel Status(){
        var p=new RenewalStatusPanel();
        p.Load(UiLayout.Parse(StatusXml,"ReNStatusUIEx.xml"),id=>id==902?Surface(902,10,10):null,id=>id switch{903=>Surface(903,180,200),904=>Surface(904,10,10),_=>null});
        return p;
    }
    const string InfoXml="""
        <StatInfoUI>
          <Window Name="BonusStatusTooltip_Window" X="0" Y="0" BasePng="911" Moveable="90">
            <List Name="StatList" X="10" Y="4" Vertical="1">
              <Control Name="StatDmg" Width="100" Height="15">
                <Label Name="DmgBonus_Label" Width="60" Height="14" String="3857"/>
                <Label Name="DmgBonus_Value" X="61" Width="30" Height="14" Caption="0"/>
              </Control>
              <Control Name="StatMR" Width="100" Height="15">
                <Label Name="MR_Label" Width="60" Height="14" String="3878"/>
                <Label Name="MR_Value" X="61" Width="30" Height="14" Caption="0"/>
              </Control>
            </List>
          </Window>
        </StatInfoUI>
        """;
    static StatInfoPanel Info(UiRoot root){
        var p=new StatInfoPanel(null,root);
        p.Load(UiLayout.Parse(InfoXml,"StatInfoUI.xml"),_=>null,id=>id==911?Surface(911,120,100):null);
        return p;
    }

    /// <summary>A closable window that opens and closes itself (stands in for any other stacked window).</summary>
    sealed class FakeWindow(string id):IUiClosablePanel {
        public bool Open;
        public string Id=>id;
        public UiLayer Layer=>UiLayer.Window;
        public Rectangle Bounds=>new(0,0,50,50);
        public bool Visible=>Open;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
        public void Close()=>Open=false;
    }

    static byte[] ResetOffer()=>LoginPackets.Pad([64,1,14,0,2,0,10,51]);
    static byte[] ResetProgress()=>LoginPackets.Pad([64,2,2,51,0xF4,1,100,0,0xF4,0xFF,18,13,11,10,10,13]);
    static byte[] ResetPoints()=>LoginPackets.Pad([64,3,5]);
    static byte[] BadOffer()=>LoginPackets.Pad([64,1,99,0,2,0,10,51]);

    [WorldModuleTest("status-window-entry")]
    static void Run(WorldModuleTestContext t){
        Mapping(t);
        Fallback(t);
        NativeEntry(t);
        RootToggle(t);
        ResetHides(t);
        Values(t);
    }

    // 1. 對應表：鈕 2 是唯一的狀態窗動作，仍保留「角色」分頁當退路；Ctrl+A 仍是 Status。
    static void Mapping(WorldModuleTestContext t){
        var a=MenuButtonsPanel.Actions;
        t.Check(a[2].Status&&a[2].Window==null&&a[2].DevToolsPage=="角色"&&!a[2].Mail&&a[2].Page==null&&!a[2].PlayStats,"2 → native status window, dev-tools 角色 kept as fallback");
        t.Equal(1,a.Values.Count(x=>x.Status),"only action 2 opens the status window");
        t.Check(MenuButtonsPanel.CommandHotkeys[Keys.Control|Keys.A]==MenuHotkeyCommand.Status,"Ctrl+A = Status");
    }

    // 2. 原生狀態窗不可用：沒有 root、root 裡沒有、XML 沒載入、已停用 → 開發工具「角色」分頁（原行為）。
    static void Fallback(WorldModuleTestContext t){
        var a=MenuButtonsPanel.Actions;var c=new UiNullCommands();
        MenuButtonsPanel.Invoke(a[2],c);
        MenuButtonsPanel.Invoke(a[2],c,new UiRoot());
        var root=new UiRoot();var empty=new RenewalStatusPanel();root.Add(empty);
        t.Check(!empty.Available&&MenuButtonsPanel.StatusWindow(root)==null,"not loaded → no status window");
        MenuButtonsPanel.Invoke(a[2],c,root);
        var faultedRoot=new UiRoot();var faulted=Status();faultedRoot.Add(faulted);faultedRoot.Fault(faulted,"test",new InvalidOperationException("synthetic"));
        MenuButtonsPanel.Run(MenuHotkeyCommand.Status,c,faultedRoot);
        t.Check(c.Calls.SequenceEqual(new[]{"dev-tools:角色","dev-tools:角色","dev-tools:角色","dev-tools:角色"}),"no root / not registered / unavailable / faulted → dev-tools 角色",c.Calls.ToArray());
        t.Check(!faulted.Visible,"faulted status window is not opened");
    }

    // 3. 可用時：鈕 2 與 Ctrl+A 開關原生狀態窗，進堆疊頂端，不開開發工具；放開與自動重複不切換。
    static void NativeEntry(WorldModuleTestContext t){
        var root=new UiRoot();var status=Status();root.Add(status);
        var menu=new MenuButtonsPanel(root);var c=new UiNullCommands();
        root.Update(World);menu.Update(World);
        t.Check(status.Available&&MenuButtonsPanel.StatusWindow(root)==status,"loaded synthetic status window usable",status.Unavailable);
        t.Check(menu.Key(Down(Keys.Control|Keys.A),c)&&status.Visible&&root.WindowStack.FirstOrDefault()==status,"Ctrl+A opens the status window on top");
        t.Check(menu.Key(Up(Keys.Control|Keys.A),c)&&menu.Key(Down(Keys.Control|Keys.A,repeat:true),c)&&status.Visible,"key up / auto-repeat do not toggle");
        t.Check(menu.Key(Down(Keys.Control|Keys.A),c)&&!status.Visible&&!root.WindowStack.Contains(status),"Ctrl+A again closes it (root.Toggle)");
        MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[2],c,root);
        t.Check(status.Visible&&status.ActivePage?.Name=="Status_Window","menu 2 opens it on the first page");
        Click(status,new(100+160+2,50+5+2),c);root.SyncWindows();
        t.Check(!status.Visible&&!root.WindowStack.Contains(status),"its own Close button still closes it");
        MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[2],c,root);
        t.Check(status.Visible,"menu 2 opens it again after a close");
        t.Check(c.Calls.Count==0,"no dev-tools page while the native window is usable",c.Calls.ToArray());
        menu.Update(new UiStaticGameState(InWorld:false));
        t.Check(!menu.Key(Down(Keys.Control|Keys.A),c)&&c.Calls.Count==0,"outside the world Ctrl+A is not taken");
    }

    // 4. 經 root.Toggle：被擠出堆疊（第 5 個窗）的狀態窗，再按 Ctrl+A 是回到頂端（原版 sub_A57C60），不是關掉；
    //    沒在 root 裡登錄的窗才退回自己的切換。
    static void RootToggle(WorldModuleTestContext t){
        var root=new UiRoot();var status=Status();root.Add(status);
        var others=Enumerable.Range(1,4).Select(i=>new FakeWindow("W"+i)).ToArray();foreach(var w in others)root.Add(w);
        var c=new UiNullCommands();
        MenuButtonsPanel.Run(MenuHotkeyCommand.Status,c,root);
        t.Check(status.Visible&&root.WindowStack.Count==1,"status window opened");
        foreach(var w in others){w.Open=true;root.SyncWindows();}
        t.Check(!status.Visible&&!root.WindowStack.Contains(status),"fifth window pushes the status window out (closed through IUiClosablePanel)");
        MenuButtonsPanel.Run(MenuHotkeyCommand.Status,c,root);
        t.Check(status.Visible&&root.WindowStack.FirstOrDefault()==status&&root.WindowStack.Count==UiRoot.MaxWindows,"Ctrl+A opens it on top again; the oldest other window leaves",root.WindowStack.Select(p=>p.Id).ToArray());
        t.Check(!others[0].Open&&others.Skip(1).All(w=>w.Open),"W1 (oldest) closed by the stack");

        // ToggleWindow：root 認得的窗走 root；open 已執行但沒開成 → 不再自己切換。
        var loose=new UiRoot();var stray=new FakeWindow("STRAY");int own=0;
        var r=MenuButtonsPanel.ToggleWindow(loose,stray,()=>stray.Open=true,()=>own++);
        t.Check(r==UiToggleResult.Unavailable&&own==1&&!stray.Open,"window unknown to the root → its own toggle once");
        loose.Add(stray);own=0;
        r=MenuButtonsPanel.ToggleWindow(loose,stray,()=>{},()=>own++);
        t.Check(r==UiToggleResult.Unavailable&&own==0,"open ran but nothing opened (Field_Map without a map) → nothing else happens");
        r=MenuButtonsPanel.ToggleWindow(loose,stray,()=>stray.Open=true,()=>own++);
        t.Check(r==UiToggleResult.Opened&&stray.Open&&own==0,"root opens it");
        r=MenuButtonsPanel.ToggleWindow(loose,stray,()=>stray.Open=true,()=>own++);
        t.Check(r==UiToggleResult.Closed&&!stray.Open&&own==0,"root closes it again");
    }

    // 5. op 43：S64 子類 1／2（格式正確）關 StatInfo；子類 3、格式錯誤、其他 opcode 不動。CharReset.cs 照常消化 S64。
    static void ResetHides(WorldModuleTestContext t){
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
        t.Check(entries.Any(e=>e.Kind=="observe"&&e.Opcode==64&&e.Subtype==-1&&e.Method.EndsWith(".ObserveStatInfoResetHide",StringComparison.Ordinal)),"S64 observer registered");
        t.Check(StatInfoResetHide.Hides(1)&&StatInfoResetHide.Hides(2)&&!StatInfoResetHide.Hides(3)&&!StatInfoResetHide.Hides(4),"kinds 1／2 hide (op 43 subcodes 1／2), 3／4 do not");

        var s=new WorldSession();long r0=StatInfoResetHide.Revision;
        s.Receive(ResetPoints(),"");
        s.Receive(LoginPackets.Pad([64,4,0x21,0x43,0x65,0,0,0]),"");
        // CharReset.cs still rejects the malformed offer (its exception is unchanged); the observer swallows its own parse error.
        t.Throws<ProtocolException>(()=>s.Receive(BadOffer(),""),"malformed kind 1 still rejected by CharReset.cs");
        t.Equal(r0,StatInfoResetHide.Revision,"kind 3, kind 4, malformed kind 1 → no hide request");
        s.Receive(ResetOffer(),"");
        t.Equal(r0+1,StatInfoResetHide.Revision,"well-formed kind 1 → one hide request");
        s.Receive(ResetProgress(),"");
        t.Equal(r0+2,StatInfoResetHide.Revision,"well-formed kind 2 → one hide request");

        var root=new UiRoot();var status=Status();var info=Info(root);root.Add(status);root.Add(info);var c=new UiNullCommands();
        MenuButtonsPanel.Run(MenuHotkeyCommand.Status,c,root);
        Click(status,new(100+10+2,50+10+2),c);info.Update(World);
        t.Check(status.StatInfoChecked&&info.Visible,"menu-opened status window → bonus check → StatInfo opens (product path now reachable)");
        info.Update(World);
        t.Check(info.Visible,"no new request → stays open");
        s.Receive(ResetOffer(),"");info.Update(World);
        t.Check(!info.Visible,"S64 kind 1 closes StatInfo");
        info.Update(World);
        t.Check(!info.Visible&&status.StatInfoChecked,"stays closed while still checked (check state kept, 原版未查)");
        Click(status,new(100+10+2,50+10+2),c);info.Update(World);Click(status,new(100+10+2,50+10+2),c);info.Update(World);
        t.Check(info.Visible,"uncheck + check → opens again");
        s.Receive(ResetProgress(),"");info.Update(World);
        t.Check(!info.Visible,"S64 kind 2 closes StatInfo");
        var late=Info(root);late.Open();late.Update(World);
        t.Check(late.Visible,"a panel created after the request does not replay it");
    }

    // 6. 數值：22 列依 XML 順序與能力分組；目前沒有一列有已查證的來源 → 全部空白（不拿總值冒充加成）。
    static void Values(WorldModuleTestContext t){
        var rows=StatInfoPanel.BonusRows;
        t.Equal(22,rows.Count,"22 rows (StatInfoUI.xml)");
        t.Equal("str:4,dex:5,int:5,con:4,wis:4",string.Join(",",rows.GroupBy(r=>r.Stat).Select(g=>$"{g.Key}:{g.Count()}")),"rows grouped STR／DEX／INT／CON／WIS (推測)");
        t.Equal(21,rows.Select(r=>r.ValueName).Distinct().Count(),"21 distinct value names (CarryBonus twice)");
        t.Check(rows.All(r=>r.ValueName.EndsWith("_Value",StringComparison.Ordinal)),"all value names end with _Value");
        var view=RenewalStatusPanel.ViewOf(new OwnStatus(1,52,0,18,12,14,13,15,16,30,120,10,40,-5,0,0,0,0,0,0,0,0,0));
        t.Check(rows.All(r=>r.Value==null&&StatInfoPanel.BonusValue(r.ValueName,view)==null),"no verified source → every value blank");
        t.Check(StatInfoPanel.BonusValue("Unknown_Value",view)==null,"unknown name → blank");

        var root=new UiRoot();var status=Status();var info=Info(root);root.Add(status);root.Add(info);
        status.Show(view);
        var provider=ClientForm.StatInfoValues(status);
        info.ValueProvider=provider;
        t.Check(info.Rows.All(r=>info.ValueText(r)==null),"product provider wired: rows stay blank (Caption \"0\" never drawn)");
        t.Check(provider("MR_Value")==null&&provider("MaxHP_Value")==null,"MR／MaxHP totals are not shown as bonuses");
    }
}
