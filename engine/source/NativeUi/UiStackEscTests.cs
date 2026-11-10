using System.Reflection;
namespace P06.NativeUi;

// R7-H（T347／#810）離線測試：全域 ESC 關掉堆疊中的窗（FU4§1.1 已驗證：ESC＝27 → sub_A57C60(0)，a1==0 關掉 4 格），
// 以及 8 個原本沒有 IUiClosablePanel 的 Window 層面板（QUIT_WINDOW、Field_Map、SummonMonster_Window、PersonalShop_Window、
// R2M_LocationShareList、FriendUI 系列、AgitMap_Window、UserPlayStatsUI）的 Close 行為。
// 依據：gap-matrix.md 15.2 第 6 點、15.6 R7-H；UiRoot.cs 的 sub_A57C60 註解（#677、#733）。
// 只用合成面板、合成 surf 與記憶體內狀態，不建 ClientForm、不讀客戶端資源。由 --module-tests 執行（名稱 native-ui-stack-esc）。
static class UiStackEscTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;

    class Win(string id,int slot):IUiPanel {
        public string Id=>id;
        public UiLayer Layer=>UiLayer.Window;
        public Rectangle Bounds=>Open?new(slot*150,10,140,100):Rectangle.Empty;
        public bool Open{get;set;}
        public bool Visible=>Open;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>Bounds.Contains(e.Location);
        public virtual bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }
    class ClosableWin(string id,int slot):Win(id,slot),IUiClosablePanel {
        public int Closes;
        public void Close(){Closes++;Open=false;}
    }
    /// <summary>自己處理 ESC 的窗（像 PersonalShopPanel、QuitWindowPanel）：只關自己。</summary>
    sealed class EscEater(string id,int slot):ClosableWin(id,slot) {
        public override bool Key(UiKeyEvent e,IUiCommands commands){
            if(!Open||!e.Down||e.KeyData!=Keys.Escape)return false;
            Close();return true;
        }
    }
    sealed class BarPanel:IUiPanel {
        public string Id=>"BAR";public UiLayer Layer=>UiLayer.Bar;public Rectangle Bounds=>new(0,470,800,130);public bool Visible=>true;
        public int EscKeys;
        public void Load(UiLayout layout,SurfCatalog surfs){}public void Update(IUiGameState state){}public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
        public bool Key(UiKeyEvent e,IUiCommands commands){if(e.KeyData==Keys.Escape)EscKeys++;return false;}
    }

    sealed class QuitSource:IQuitWindowSource {
        public QuitWindowView? Current{get;set;}
        public int Closes,Restarts,Quits;
        public void Restart()=>Restarts++;
        public void Quit()=>Quits++;
        public void Close()=>Closes++;
    }
    sealed class StatsSource:IUserPlayStatsSource {
        public UserPlayStatsView? Current{get;set;}
        public int Dismisses,Restarts,Quits;
        public void Restart()=>Restarts++;
        public void Quit()=>Quits++;
        public void Dismiss(){Dismisses++;Current=null;}
    }
    sealed class SummonHost:ISummonUiHost {
        public SummonUiState State{get;set;}=new(false,0,"",null,null,false);
        public readonly List<long> Closed=[];public int Chosen;
        public void Choose(SummonUiState shown,SummonMonster monster)=>Chosen++;
        public void Close(SummonUiState shown)=>Closed.Add(shown.Revision);
    }
    sealed class ListHost:ILocationListHost {
        public LocationListView View{get;set;}=LocationListView.Empty;
        public void Select(LocationListView shown,long sequence){}
    }
    sealed class MapSource(Func<UiMapSnapshot?> read):IUiMapSource { public UiMapSnapshot? ReadMap()=>read(); }

    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static IUiGameState World(long now=0)=>new UiStaticGameState(InWorld:true,Now:now);
    static UiKeyEvent Esc(Keys mods=Keys.None,bool down=true,bool repeat=false)=>new(Keys.Escape|mods,down,repeat);
    static string Ids(IEnumerable<IUiPanel> ps)=>string.Join(",",ps.Select(p=>p.Id));

    [WorldModuleTest("native-ui-stack-esc")]
    static void Run(WorldModuleTestContext t){
        Types(t);CloseAll(t);GlobalEscape(t);EvictionDropsFocus(t);
        Quit(t);UserPlayStats(t);Summon(t);PersonalShop(t);Friend(t);AgitMap(t);FieldMap(t);LocationList(t);
        RealPanelsInStack(t);
    }

    // 1. 8 個面板型別都實作 IUiClosablePanel（gap-matrix 15.2 第 6 點；Board 按鈕窗歸 R7-F）。
    static void Types(WorldModuleTestContext t){
        var types=new[]{typeof(QuitWindowPanel),typeof(FieldMapPanel),typeof(SummonPanel),typeof(PersonalShopPanel),
            typeof(LocationListPanel),typeof(FriendPanel),typeof(AgitMapPanel),typeof(UserPlayStatsPanel)};
        foreach(var type in types)t.Check(typeof(IUiClosablePanel).IsAssignableFrom(type),type.Name+" implements IUiClosablePanel");
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations still valid",UiPanelRegistry.Errors);
    }

    // 2. UiRoot.CloseAll＝sub_A57C60(0)：堆疊中能關的全部關掉（由上而下）；不能關的留著；被擠出的不算在堆疊裡。
    static void CloseAll(WorldModuleTestContext t){
        var state=World();
        {
            var r=new UiRoot();
            var ws=Enumerable.Range(0,4).Select(i=>new ClosableWin("C"+i,i)).ToArray();foreach(var w in ws)r.Add(w);
            foreach(var w in ws){w.Open=true;r.Update(state);}
            t.Equal("C3,C2,C1,C0",Ids(r.WindowStack),"4 windows open, newest on top");
            var closed=r.CloseAll();
            t.Equal("C3,C2,C1,C0",Ids(closed),"CloseAll closes all four, top first");
            t.Check(ws.All(w=>w.Closes==1&&!w.Open)&&r.WindowStack.Count==0,"each closed once through Close(); stack empty");
            t.Check(UiResourceLog.Recent().Any(l=>l.Contains("視窗堆疊全部關閉")),"CloseAll logged");
            t.Equal(0,r.CloseAll().Count,"nothing open → CloseAll does nothing");
            t.Check(ws.All(w=>w.Closes==1),"…and does not call Close again");
        }
        {
            var r=new UiRoot();
            var stuck=new Win("STUCK",0);var a=new ClosableWin("A",1);var b=new ClosableWin("B",2);
            r.Add(stuck);r.Add(a);r.Add(b);
            stuck.Open=true;r.Update(state);a.Open=true;r.Update(state);b.Open=true;r.Update(state);
            var closed=r.CloseAll();
            t.Equal("B,A",Ids(closed),"non-closable window is skipped");
            t.Check(stuck.Open&&Ids(r.WindowStack)=="STUCK","non-closable window stays open in the stack");
        }
        {
            // 被擠出、不能關的窗：不在原版陣列裡，CloseAll 不碰；之後仍由它自己的切換鍵處理。
            var r=new UiRoot();
            var old=new Win("OLD",0);r.Add(old);
            var ws=Enumerable.Range(1,4).Select(i=>new ClosableWin("N"+i,i)).ToArray();foreach(var w in ws)r.Add(w);
            old.Open=true;r.Update(state);foreach(var w in ws)w.Open=true;r.Update(state);
            t.Check(r.IsEvicted(old),"OLD evicted (hidden, still open)");
            var closed=r.CloseAll();
            t.Equal("N4,N3,N2,N1",Ids(closed),"CloseAll closes the 4 stacked windows only");
            t.Check(old.Open&&r.IsEvicted(old)&&r.WindowStack.Count==0,"evicted OLD untouched");
        }
        {
            var r=new UiRoot();var boom=new BoomWin();var ok=new ClosableWin("OK",1);r.Add(boom);r.Add(ok);
            boom.Open=true;r.Update(state);ok.Open=true;r.Update(state);
            var closed=r.CloseAll();
            t.Check(r.Faulted.Contains(boom)&&!ok.Open&&closed.Contains(ok)&&r.WindowStack.Count==0,"Close that throws faults that panel; the others still close");
        }
    }
    sealed class BoomWin():ClosableWin("BOOM",0),IUiClosablePanel { void IUiClosablePanel.Close()=>throw new InvalidOperationException("close boom"); }

    // 3. UiInput 全域 ESC：沒有面板吃 ESC 才關堆疊；只有真的關了窗才吃掉（否則交給遊戲）。
    static void GlobalEscape(WorldModuleTestContext t){
        var state=World();
        var r=new UiRoot();var bar=new BarPanel();r.Add(bar);
        var ws=Enumerable.Range(0,3).Select(i=>new ClosableWin("W"+i,i)).ToArray();foreach(var w in ws)r.Add(w);
        var eater=new EscEater("EAT",3);r.Add(eater);
        var cmd=new UiNullCommands();
        var input=new UiInput(r,cmd){Viewport=UiPresenter.Compute(new(800,600))};
        foreach(var w in ws)w.Open=true;r.Update(state);
        t.Check(!input.Key(Esc(Keys.Shift))&&ws.All(w=>w.Open),"Shift+Esc is not the global ESC");
        t.Check(!input.Key(Esc(repeat:true))&&ws.All(w=>w.Open),"auto-repeat ESC does not close");
        t.Check(!input.Key(Esc(down:false))&&ws.All(w=>w.Open),"ESC release does not close");
        t.Check(input.Key(Esc())&&ws.All(w=>!w.Open&&w.Closes==1),"ESC closes every stacked window");
        t.Check(bar.EscKeys>=1,"bar panels were offered the key first");
        t.Equal("game-focus",cmd.Calls.LastOrDefault(),"ESC that closed windows hands focus back to the game");
        int calls=cmd.Calls.Count;
        t.Check(!input.Key(Esc())&&cmd.Calls.Count==calls,"nothing open → ESC goes to the game (YES/NO, ClearInput …)");

        // 自己處理 ESC 的窗先拿到（sub_8CA740 先給堆疊中的窗）：只關自己；下一次 ESC 才由全域關剩下的。
        foreach(var w in ws)w.Open=true;r.Update(state);eater.Open=true;r.Update(state);
        t.Equal("EAT,W2,W1,W0",Ids(r.WindowStack),"eater on top");
        t.Check(input.Key(Esc())&&!eater.Open&&ws.All(w=>w.Open),"window with its own ESC closes only itself");
        t.Check(input.Key(Esc())&&ws.All(w=>!w.Open),"next ESC: global close of the rest");
    }

    // 4. 被擠出／被拒絕的窗不再保有焦點與滑鼠捕捉（WindowEvicted／WindowRefused 有訂閱者）。
    static void EvictionDropsFocus(WorldModuleTestContext t){
        var state=World();
        var r=new UiRoot();
        var first=new Win("FIRST",0);r.Add(first);
        var ws=Enumerable.Range(1,4).Select(i=>new ClosableWin("N"+i,i)).ToArray();foreach(var w in ws)r.Add(w);
        var input=new UiInput(r,new UiNullCommands()){Viewport=UiPresenter.Compute(new(800,600))};
        first.Open=true;r.Update(state);
        input.Mouse(UiMouseKind.Down,new(20,20),MouseButtons.Left,1);
        t.Check(input.Focused==first&&input.Captured==first,"press on FIRST: focus and capture");
        foreach(var w in ws)w.Open=true;r.Update(state);
        t.Check(r.IsEvicted(first)&&input.Focused==null&&input.Captured==null,"FIRST pushed out → focus and capture dropped");

        var refusedRoot=new UiRoot{CurrentMap=UiRoot.SpellWindowRefusedMap};var spell=new Win(SpellWindowPanel.WindowId,0);refusedRoot.Add(spell);
        var other=new ClosableWin("OTHER",2);refusedRoot.Add(other);
        var input2=new UiInput(refusedRoot,new UiNullCommands()){Viewport=UiPresenter.Compute(new(800,600))};
        other.Open=true;refusedRoot.Update(state);
        input2.Mouse(UiMouseKind.Down,new(320,20),MouseButtons.Left,1);input2.Mouse(UiMouseKind.Up,new(320,20),MouseButtons.Left);
        spell.Open=true;refusedRoot.Update(state);
        t.Check(refusedRoot.IsRefused(spell)&&input2.Focused==other,"refused window: focus stays where it was");
    }

    // 5. QUIT_WINDOW：Close＝取消鈕（只關窗），已關著不再呼叫 Source。
    static void Quit(WorldModuleTestContext t){
        var src=new QuitSource{Current=new(7,false,true,"")};
        var p=new QuitWindowPanel{Source=src};
        p.Update(World());
        t.Check(p.Visible,"quit window open");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&src.Closes==1&&src.Restarts==0&&src.Quits==0,"Close = cancel: hides, Source.Close once, no restart/quit");
        p.Update(World());
        t.Check(!p.Visible,"same revision stays closed");
        ((IUiClosablePanel)p).Close();
        t.Equal(1,src.Closes,"closing a closed window is a no-op");
        src.Current=new(8,false,true,"");p.Update(World());
        t.Check(p.Visible,"next death timeout (new revision) opens again");
        t.Check(p.Key(Esc(),new UiNullCommands())&&!p.Visible&&src.Closes==2,"its own ESC still = cancel");
    }

    // 6. UserPlayStatsUI：Close＝取消（Dismiss），一併關 UserObtainUI 清單窗；公開的 Close 屬性（關閉鈕）不受影響。
    static void UserPlayStats(WorldModuleTestContext t){
        var src=new StatsSource{Current=new(true)};
        var p=new UserPlayStatsPanel{Source=src};
        p.Update(World());p.ObtainListOpen=true;
        t.Check(p.Visible,"stats window open");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&!p.ObtainListOpen&&src.Dismisses==1&&src.Restarts==0&&src.Quits==0,"Close = dismiss; obtain list closed too");
        ((IUiClosablePanel)p).Close();
        t.Equal(1,src.Dismisses,"closing a closed window is a no-op");
        p.Update(World());
        t.Check(!p.Visible,"source no longer offers it → stays closed");
    }

    // 7. SummonMonster_Window：Close＝關閉鈕（請宿主關這一次請求）。
    static void Summon(WorldModuleTestContext t){
        var host=new SummonHost{State=new(true,5,"local",60,20,true)};
        var p=new SummonPanel();p.Attach(host);p.Update(World());
        ((IUiClosablePanel)p).Close();
        t.Check(host.Closed.SequenceEqual([5L]),"Close asks the host to close request 5",host.Closed);
        ((IUiClosablePanel)p).Close();p.Update(World());((IUiClosablePanel)p).Close();
        t.Equal(1,host.Closed.Count,"the same request is closed only once");
        host.State=new(false,6,"local",60,20,true);p.Update(World());((IUiClosablePanel)p).Close();
        t.Equal(1,host.Closed.Count,"no open request → nothing to close");
        t.Equal(0,host.Chosen,"Close never sends a choice");
    }

    // 8. PersonalShop_Window：既有 Close（關閉鈕／取消／Esc）就是 IUiClosablePanel.Close。
    static void PersonalShop(WorldModuleTestContext t){
        var p=new PersonalShopPanel();int closed=0;p.Closed+=()=>closed++;
        p.Open();
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&closed==1,"Close hides and raises Closed once");
        ((IUiClosablePanel)p).Close();
        t.Equal(1,closed,"closing a closed window raises nothing");
    }

    // 9. FriendUI／ExcludeUI／MailExcludeUI：Close＝關閉鈕（Hide）。
    static void Friend(WorldModuleTestContext t){
        foreach(var spec in FriendWindowSpec.All){
            var p=new FriendPanel(spec,()=>null);
            typeof(FriendPanel).GetField("visible",F)!.SetValue(p,true);
            t.Check(p.Visible,spec.Id+" open");
            ((IUiClosablePanel)p).Close();
            t.Check(!p.Visible,spec.Id+": Close hides it");
        }
    }

    // 10. AgitMap_Window：Close＝關閉鈕 678（這一次請求不再開；新請求照開）。
    static void AgitMap(WorldModuleTestContext t){
        var surfs=AgitMapLayout.AllSurfs.Distinct().ToDictionary(id=>id,id=>Surface(id,30,20));
        AgitMapOpenRequest? current=new(1,new(0x1234,(uint)(4<<16|1)));
        var p=new AgitMapPanel(()=>current);p.Load(id=>surfs.GetValueOrDefault(id));
        p.Update(World(0));
        t.Check(p.Visible&&!p.Bounds.IsEmpty,"agit map open");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&p.Bounds.IsEmpty,"Close hides it");
        p.Update(World(10));
        t.Check(!p.Visible,"same request stays closed");
        ((IUiClosablePanel)p).Close();
        current=new(2,new(0x1234,(uint)(4<<16|2)));p.Update(World(20));
        t.Check(p.Visible,"new request opens again");
    }

    // 11. Field_Map：Close＝結束鈕（FieldOpen=false）；面板仍 Visible（Ctrl+M 照樣收得到）、Bounds 變空。
    static void FieldMap(WorldModuleTestContext t){
        UiMapSnapshot? snap=new(4,32800,32800,3,[]);var source=new MapSource(()=>snap);
        var p=new FieldMapPanel(new MapState(null,()=>source));
        typeof(FieldMapPanel).GetField("size",F)!.SetValue(p,new Size(161,121));p.MoveTo(new(5,250));
        p.Update(World());p.Map.ToggleField();
        t.Check(p.Map.FieldOpen&&!p.Bounds.IsEmpty,"Field_Map open");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Map.FieldOpen&&p.Visible&&p.Bounds.IsEmpty,"Close: map window closed, panel still offered keys");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Map.FieldOpen,"closing a closed map window keeps it closed (no toggle)");
        t.Check(p.Key(new(FieldMapPanel.ToggleKey,true,false),new UiNullCommands())&&p.Map.FieldOpen,"Ctrl+M opens it again");
    }

    // 12. R2M_LocationShareList：Close 藏起目前這一版清單，清單變更（新分享）時再顯示。
    static void LocationList(WorldModuleTestContext t){
        var host=new ListHost{View=new(1,[new(1,"甲","說話之島",4)],null)};
        var p=new LocationListPanel(()=>new(5,250,161,121));p.Attach(host);
        p.Update(World());
        t.Check(p.Visible&&!p.Bounds.IsEmpty,"list shown beside Field_Map");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&p.Bounds.IsEmpty,"Close hides the current list");
        p.Update(World());
        t.Check(!p.Visible,"same revision stays hidden");
        host.View=new(2,[new(1,"甲","說話之島",4),new(2,"乙","古魯丁",4)],null);p.Update(World());
        t.Check(p.Visible&&p.Bounds.Height==2*LocationListPanel.RowHeight+2*LocationListPanel.Padding,"new share (revision 2) shows the list again");
        var empty=new LocationListPanel(()=>new(5,250,161,121));((IUiClosablePanel)empty).Close();
        t.Check(!empty.Visible,"Close on an empty list is harmless");
    }

    // 13. 實際面板放進 UiRoot：第 5 個窗擠出 Field_Map 時真的關掉（不再只是藏起來）；ESC 一次關掉剩下的。
    static void RealPanelsInStack(WorldModuleTestContext t){
        var state=World();
        UiMapSnapshot? snap=new(4,32800,32800,3,[]);var source=new MapSource(()=>snap);
        var r=new UiRoot();
        var field=new FieldMapPanel(new MapState(null,()=>source));
        typeof(FieldMapPanel).GetField("size",F)!.SetValue(field,new Size(161,121));field.MoveTo(new(5,250));
        var surfs=AgitMapLayout.AllSurfs.Distinct().ToDictionary(id=>id,id=>Surface(id,30,20));
        AgitMapOpenRequest? req=null;
        var agit=new AgitMapPanel(()=>req);agit.Load(id=>surfs.GetValueOrDefault(id));
        var host=new ListHost();
        var list=new LocationListPanel(()=>field.Bounds);list.Attach(host);
        var a=new ClosableWin("A",3);var b=new ClosableWin("B",4);
        r.Add(field);r.Add(agit);r.Add(list);r.Add(a);r.Add(b);
        var cmd=new UiNullCommands();var input=new UiInput(r,cmd){Viewport=UiPresenter.Compute(new(800,600))};

        r.Update(state);field.Map.ToggleField();r.Update(state);
        req=new(1,new(0x1234,(uint)(4<<16|1)));r.Update(World(1));
        host.View=new(1,[new(1,"甲","說話之島",4)],null);r.Update(World(2));
        a.Open=true;r.Update(World(3));
        t.Equal("A,R2M_LocationShareList,AgitMap_Window,Field_Map",Ids(r.WindowStack),"4 windows: A, list, agit map, Field_Map");
        b.Open=true;r.Update(World(4));
        t.Check(!field.Map.FieldOpen&&!r.IsEvicted(field),"5th window: Field_Map (oldest) is closed through IUiClosablePanel, not just hidden");
        t.Check(!r.WindowStack.Contains(field)&&list.Bounds.IsEmpty&&!r.WindowStack.Contains(list),"Field_Map left the stack; the list beside it went with it (anchored)");
        t.Equal("B,A,AgitMap_Window",Ids(r.WindowStack),"stack after the push-out");
        t.Check(input.Key(Esc()),"ESC taken");
        t.Check(!agit.Visible&&!a.Open&&!b.Open&&r.WindowStack.Count==0,"ESC closes all remaining stacked windows");
        r.Update(World(5));
        t.Check(!agit.Visible&&!field.Map.FieldOpen&&r.WindowStack.Count==0,"closed real panels stay closed on the next frame");
    }
}
