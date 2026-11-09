namespace P06.NativeUi;

// R5-A（T306／#733）離線測試：sub_A57C60（VA 0xA57C60／FO 0x657060）剩下的兩條規則——「再開一次＝關掉」（UiRoot.Toggle）與
// 地圖 509 拒絕推入 SpellWindow（UiRoot.RefusesPush）；以及選單鈕 5／6 在原生頁窗可用時不再開開發工具「NPC」分頁。
// 依據：docs/reports/issue-677.md 第 1 節、windows-2.md 2.1 第 4 點、gap-matrix.md 14.2 第 4 點、14.6 R5-A。
// 只用合成面板，不建 ClientForm、不讀客戶端資源。由 --module-tests 執行（名稱 native-ui-stack-followup）。
static class UiStackFollowupTests {
    class Win(string id,int slot):IUiPanel {
        public string Id=>id;
        public UiLayer Layer=>UiLayer.Window;
        public Rectangle Bounds=>Open?new(slot*150,10,140,100):Rectangle.Empty;
        public bool Open{get;set;}
        public bool Visible=>Open;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>true;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }
    sealed class ClosableWin(string id,int slot):Win(id,slot),IUiClosablePanel {
        public int Closes;
        public void Close(){Closes++;Open=false;}
    }
    sealed class BarPanel(string id):IUiPanel {
        public string Id=>id;public UiLayer Layer=>UiLayer.Bar;public Rectangle Bounds=>new(0,470,800,130);public bool Visible=>true;
        public void Load(UiLayout layout,SurfCatalog surfs){}public void Update(IUiGameState state){}public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }

    [WorldModuleTest("native-ui-stack-followup")]
    static void Run(WorldModuleTestContext t){
        static string Ids(UiRoot r)=>string.Join(",",r.WindowStack.Select(p=>p.Id));
        var state=new UiStaticGameState(InWorld:true);
        const string Spell=SpellWindowPanel.WindowId;

        // 1. 推入規則：只有地圖 509 的 SPELL_WINDOW 被拒絕。
        {
            t.Equal(509,UiRoot.SpellWindowRefusedMap,"refused map = 509 (*dword_138BE60)");
            var spell=new Win(Spell,0);var other=new Win("ITEM_WINDOW",1);
            t.Check(UiRoot.RefusesPush(spell,509),"SPELL_WINDOW on map 509 → refused");
            t.Check(!UiRoot.RefusesPush(spell,4)&&!UiRoot.RefusesPush(spell,null)&&!UiRoot.RefusesPush(spell,508),"SPELL_WINDOW elsewhere / outside the world → pushed");
            t.Check(!UiRoot.RefusesPush(other,509),"other windows on map 509 → pushed");
        }

        // 2. 自己打開的 SPELL_WINDOW（選單 3／Ctrl+S）在 509：同一次同步就關回去，不進堆疊、不畫、不能點。
        {
            var r=new UiRoot{CurrentMap=509};var spell=new ClosableWin(Spell,0);var item=new ClosableWin("ITEM_WINDOW",1);r.Add(spell);r.Add(item);
            var refusedIds=new List<string>();r.WindowRefused+=p=>refusedIds.Add(p.Id);
            spell.Open=true;item.Open=true;
            t.Check(r.HitTest(new(5,15))==null&&r.HitTest(new(155,15))==item,"lazy sync on hit test: refused SPELL_WINDOW is not hit, ITEM_WINDOW is");
            t.Check(spell.Closes==1&&!spell.Open&&!r.IsRefused(spell)&&Ids(r)=="ITEM_WINDOW","refused closable window is closed through Close(); stack = ITEM_WINDOW");
            t.Check(refusedIds.SequenceEqual(new[]{Spell}),"WindowRefused fired once",refusedIds.ToArray());
            t.Check(UiResourceLog.Recent().Any(l=>l.Contains("拒絕開啟 "+Spell)),"refusal logged");
            r.Update(state);
            t.Check(spell.Closes==1&&refusedIds.Count==1,"repeated sync is idempotent");
            // 已經在堆疊裡的窗不受影響（原版只在推入時判斷）。
            r.CurrentMap=4;spell.Open=true;r.Update(state);
            r.CurrentMap=509;r.Update(state);
            t.Check(spell.Open&&Ids(r)==Spell+",ITEM_WINDOW"&&spell.Closes==1,"SPELL_WINDOW opened elsewhere stays open after moving to 509");
        }

        // 3. 不能關的窗被拒絕：藏起來直到自己關掉；換地圖後 Toggle 讓它回到最上層（不重開）。
        {
            var r=new UiRoot{CurrentMap=509};var spell=new Win(Spell,0);r.Add(spell);
            spell.Open=true;r.Update(state);
            t.Check(r.IsRefused(spell)&&!r.Usable(spell)&&r.WindowStack.Count==0,"non-closable refused window is held hidden");
            int opens=0;
            t.Equal(UiToggleResult.Refused,r.Toggle(spell,()=>opens++),"Toggle on 509 → Refused");
            t.Check(opens==0&&r.IsRefused(spell),"refused toggle does not open");
            r.CurrentMap=4;r.Update(state);
            t.Check(r.IsRefused(spell),"leaving 509 does not bring the held window back by itself");
            t.Equal(UiToggleResult.Opened,r.Toggle(spell,()=>opens++),"Toggle after leaving 509 → Opened");
            t.Check(opens==0&&!r.IsRefused(spell)&&r.Usable(spell)&&Ids(r)==Spell,"held window comes back on top without reopening");
            spell.Open=false;r.Update(state);spell.Open=true;r.CurrentMap=509;r.Update(state);
            spell.Open=false;r.Update(state);
            t.Check(!r.IsRefused(spell),"held window that closes itself is no longer refused");
        }

        // 4. Toggle＝sub_A57C60：不在堆疊 → 開到最上層；在堆疊 → 關掉並往前補；第 5 個擠掉最舊的。
        {
            var r=new UiRoot();
            var ws=Enumerable.Range(0,5).Select(i=>new ClosableWin("T"+i,i)).ToArray();foreach(var w in ws)r.Add(w);
            r.Add(new BarPanel("BAR"));
            r.Update(state);
            foreach(var w in ws.Take(4))t.Equal(UiToggleResult.Opened,r.Toggle(w,()=>w.Open=true),w.Id+" toggled open");
            t.Equal("T3,T2,T1,T0",Ids(r),"4 toggled windows: newest on top");
            t.Equal(UiToggleResult.Closed,r.Toggle(ws[1],()=>ws[1].Open=true),"T1 again → Closed (open again = close)");
            t.Check(ws[1].Closes==1&&!ws[1].Open&&Ids(r)=="T3,T2,T0","T1 closed through Close(); stack compacts to T3,T2,T0");
            t.Equal(UiToggleResult.Opened,r.Toggle(ws[1],()=>ws[1].Open=true),"T1 third time → Opened");
            t.Equal("T1,T3,T2,T0",Ids(r),"reopened T1 goes on top");
            t.Equal(UiToggleResult.Opened,r.Toggle(ws[4],()=>ws[4].Open=true),"5th window T4 → Opened");
            t.Check(Ids(r)=="T4,T1,T3,T2"&&ws[0].Closes==1&&!ws[0].Open,"5th window pushes out the oldest (T0, closed)");
            t.Equal(UiToggleResult.Unavailable,r.Toggle(ws[0]),"closed window without an open action → Unavailable");
            t.Equal(UiToggleResult.Unavailable,r.Toggle(r.Find("BAR")!),"bar panel → Unavailable");
            t.Equal(UiToggleResult.Unavailable,r.Toggle(new ClosableWin("NOT_ADDED",0),()=>{}),"unregistered window → Unavailable");
            t.Equal(UiToggleResult.Unavailable,r.Toggle(ws[0],()=>throw new InvalidOperationException("open boom")),"open throws → Unavailable");
            t.Check(r.Faulted.Contains(ws[0]),"window whose open throws is faulted");
        }

        // 5. 被擠出、不能關的窗：Toggle 直接拉回最上層（不先關掉）——#677 FieldMap 第一次按鍵像沒反應的問題。
        {
            var r=new UiRoot();
            var old=new Win("OLD",0);r.Add(old);
            var ws=Enumerable.Range(1,4).Select(i=>new Win("N"+i,i)).ToArray();foreach(var w in ws)r.Add(w);
            old.Open=true;r.Update(state);foreach(var w in ws)w.Open=true;r.Update(state);
            t.Check(r.IsEvicted(old)&&old.Open,"OLD evicted but still open (not closable)");
            int opens=0;
            t.Equal(UiToggleResult.Opened,r.Toggle(old,()=>opens++),"Toggle evicted OLD → Opened");
            t.Check(opens==0&&old.Open&&!r.IsEvicted(old)&&Ids(r)=="OLD,N4,N3,N2"&&r.IsEvicted(ws[0]),"OLD back on top without reopening; oldest N1 pushed out");
            t.Equal(UiToggleResult.Unavailable,r.Toggle(old),"non-closable window in the stack cannot be toggled closed");
            t.Check(old.Open&&Ids(r)=="OLD,N4,N3,N2","…and stays where it is");
        }

        // 6. 選單鈕 5／6：原生頁窗可用就不開開發工具；不可用（原生 UI 關閉、面板故障）才開「NPC」分頁。
        t.Check(ClientForm.MenuLocalPageDevTools(true)==null,"native HYPER_TEXT_WINDOW usable → no dev-tools page");
        t.Equal("NPC",ClientForm.MenuLocalPageDevTools(false),"no native page window → dev-tools NPC tab (legacy path)");
    }
}
