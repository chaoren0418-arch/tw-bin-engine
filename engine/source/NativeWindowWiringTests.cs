using System.Reflection;
using P06.NativeUi;
namespace P06;

// R3-A（T285／#678）離線測試：四個舊式窗的 [UiPanel] 註冊、橋接狀態鏈（最內層的舊式窗包裝仍被其他模組的查找穿過）、
// 選單 8 鈕與熱鍵（FU4§1：Tab／Ctrl+I 背包、Ctrl+S 魔法；7 開遊玩統計窗、不登出）、血盟窗的關注血盟資料（SS2§3）。
// 只用合成 XML／surf，不讀客戶端、不連線、不開視窗。由 --module-tests 執行（名稱 native-window-wiring）。
static class NativeWindowWiringTests {
    sealed class MenuCommands:IUiCommands,IUiMenuCommands {
        public readonly List<string> Calls=[];
        public bool Available=true;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool TogglePlayStats(){Calls.Add("play-stats");return Available;}
    }

    static UiKeyEvent Down(Keys k,bool repeat=false)=>new(k,true,repeat);
    static UiKeyEvent Up(Keys k)=>new(k,false,false);

    [WorldModuleTest("native-window-wiring")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：四個窗各一行，版面名稱正確，工廠建出正確型別；登錄表沒有錯誤。
        var regs=UiPanelRegistry.Panels;
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations valid",UiPanelRegistry.Errors);
        foreach(var (id,layout,type) in new (string,string,Type)[]{
            (ItemWindowPanel.WindowId,"uimagicitem",typeof(ItemWindowPanel)),(SpellWindowPanel.WindowId,"uimagicitem",typeof(SpellWindowPanel)),
            (WarehouseWindowPanel.WindowId,"uiextra86",typeof(WarehouseWindowPanel)),(PledgePanel.PanelId,"PledgeUI",typeof(PledgePanel))}){
            t.Equal(1,regs.Count(r=>r.Id==id&&r.Layout==layout),id+" registered once on "+layout);
            t.Check(regs.Single(r=>r.Id==id).Create(new UiRoot()).GetType()==type,id+" factory creates "+type.Name);
        }
        t.Check(regs.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()==regs.Count,"registry ids stay unique");
        t.Check(regs.Single(r=>r.Id==MenuButtonsPanel.PanelId).Create(new UiRoot()) is MenuButtonsPanel,"menu factory still builds MenuButtonsPanel");
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(LegacyWindowPanel))&&typeof(IUiClosablePanel).IsAssignableFrom(typeof(PledgePanel)),"legacy windows and the pledge window can be closed by the 4-window stack");

        // 2. 產品端指令：橋接指令類別實作倉庫與選單介面（離線不建 ClientForm，只查型別）。
        var bridge=typeof(ClientForm).GetNestedType("NativeUiCommands",BindingFlags.NonPublic);
        t.Check(bridge!=null&&typeof(IUiWarehouseCommands).IsAssignableFrom(bridge)&&typeof(IUiMenuCommands).IsAssignableFrom(bridge),"bridge commands implement IUiWarehouseCommands and IUiMenuCommands");

        // 3. 狀態鏈：照產品順序包（HpMp → LeftStatus → Chat → Party → Buff → RedNotice → 舊式窗四層 → basic）。
        InventoryItem Item(uint id,byte use,string name)=>new(id,1,use,0,10,1,1,1,name,"","");
        var inv=new[]{Item(1,1,"劍"),Item(2,0,"藥水")};
        var rows=new ShopRow[]{new(11,5,30,"長劍",1,""),new(12,6,30,"箭",500,"")};
        var pledge=PledgeWindowState.Empty with{OpenRevision=1,Revision=1,Clan="盟",Online=["Alice"]};
        var attention=Enumerable.Range(1,10).Select(i=>"關注"+i).ToArray();
        IUiGameState Chain(bool inWorld,UiWarehouseView? wh){
            var basic=new UiStaticGameState(InWorld:inWorld);
            IUiGameState s=new UiItemWindowGameState(basic,new UiItemWindowData(inv));
            s=new UiSpellWindowGameState(s,new UiSpellWindowData(LegacySpellClass.Elf,[]));
            s=new UiWarehouseGameState(s,wh);
            s=new UiPledgeGameState(s,pledge,attention);
            s=new UiRedNoticeGameState(s,RedNoticeState.Empty);
            s=new UiBuffGameState(s,BuffState.Empty);
            s=new UiPartyGameState(s,null);
            s=new UiChatGameState(s,UiChatSnapshot.Empty);
            s=new UiLeftStatusGameState(s,LeftStatusState.Empty);
            return new UiHpMpGameState(s,[]);
        }
        var state=Chain(true,new UiWarehouseView("withdraw",3,rows,30));
        t.Check(LegacyWindowPanel.FindState<IUiItemWindowState>(state)?.Items?.Count==2,"item data found under the whole product chain");
        t.Equal((LegacySpellClass?)LegacySpellClass.Elf,LegacyWindowPanel.FindState<IUiSpellWindowState>(state)?.Class,"spell class found under the whole chain");
        t.Equal(3,LegacyWindowPanel.FindState<IUiWarehouseState>(state)?.Warehouse?.Revision,"warehouse view found under the whole chain");
        t.Check(UiPledgeGameState.Of(state)?.Online is ["Alice"]&&UiPledgeGameState.AttentionOf(state)?.Count==10,"pledge data and attention found under the whole chain");
        t.Check(RedNoticeState.Of(state)!=null&&BuffState.Of(state)!=null&&PartyStates.StateOf(state)!=null,"red notice / buff / party lookups still find their layers (legacy decorators sit innermost)");
        t.Check(state.InWorld&&state.Hp==null,"IUiGameState members forwarded through the new layers");

        // 4. 面板讀到資料：倉庫清單開窗；血盟 171 開窗；關注清單只做資料（7 列、上限 (n−7)/2）。
        var whSurf=LegacyWindowTestKit.Surfaces(new(){[3001]=(220,320),[3002]=(250,360),[3031]=(14,14),[3024]=(10,220),[3025]=(10,12),[3027]=(10,12),
            [3007]=(60,14),[3014]=(50,18),[3017]=(50,18),[3008]=(30,12),[3009]=(30,12),[3010]=(9,9),[3012]=(9,9),[3020]=(180,20),[3021]=(180,20),[3022]=(180,20),[3023]=(180,20)});
        var wh=new WarehouseWindowPanel();wh.Load(UiLayout.Parse(WarehouseWindowPanel.TestXml,"uiextra86-c.xml"),whSurf.Get);
        wh.Update(state);
        t.Check(wh.Visible&&wh.View?.Kind=="withdraw"&&wh.Rows.Count==2,"S176 list in the bridge state opens WAREHOUSE_WINDOW");
        wh.Update(Chain(true,null));
        t.Check(!wh.Visible,"no warehouse list → closed");
        var pp=new PledgePanel();pp.Update(state);
        t.Check(pp.Attention.Count==10&&pp.AttentionMaxScroll==1&&pp.AttentionPage.Count==PledgePanel.AttentionRows,"attention: 10 names, 7 per page, max scroll (10−7)/2 = 1");
        pp.ScrollAttention(5);
        t.Check(pp.AttentionScroll==1&&pp.AttentionPage.SequenceEqual(attention.Skip(1).Take(7)),"attention scroll clamps; page starts at the scroll index (sub_B30040(a2))");
        pp.Update(Chain(false,null));
        t.Check(pp.Attention.Count==0&&pp.AttentionScroll==0&&!pp.Visible,"leaving the world clears attention and hides the window");
        pp.Update(new UiPledgeGameState(new UiStaticGameState(InWorld:true),pledge));
        t.Check(pp.Attention.Count==0,"no attention provided → empty, nothing estimated");

        // 5. 選單鈕：對應表（FU4§1.1）。
        var a=MenuButtonsPanel.Actions;
        t.Check(a[13].Window==FieldMapPanel.PanelId&&a[3].Window==SpellWindowPanel.WindowId&&a[4].Window==ItemWindowPanel.WindowId,"13 → Field_Map, 3 → SPELL_WINDOW, 4 → ITEM_WINDOW");
        t.Check(a[7].PlayStats&&a[7].Window==null&&a.Values.Count(x=>x.PlayStats)==1,"7 → play-stats window (not logout)");
        t.Check(new[]{1,2,5,6}.All(k=>a[k].Window==null&&!a[k].PlayStats&&a[k].DevToolsPage!=null),"1/2/5/6: no native window yet → dev-tools page (待驗證)");
        t.Check(MenuButtonsPanel.Hotkeys.Count==3&&MenuButtonsPanel.Hotkeys[Keys.Tab]==4&&MenuButtonsPanel.Hotkeys[Keys.Control|Keys.I]==4&&MenuButtonsPanel.Hotkeys[Keys.Control|Keys.S]==3,"hotkeys: Tab／Ctrl+I → item, Ctrl+S → spell; no warehouse hotkey");

        // 6. 選單與熱鍵實際切換窗（合成 ITEM_WINDOW／SPELL_WINDOW）。
        var itemSurf=LegacyWindowTestKit.Surfaces(new(){[2046]=(170,260),[2047]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10),
            [2048]=(130,20),[2049]=(130,20),[2050]=(130,20),[2051]=(130,20),[2052]=(22,22),[2054]=(22,22)});
        var spellSizes=new Dictionary<int,(int,int)>{[2056]=(170,260),[2057]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10)};
        for(int s=2058;s<=2090;s++)spellSizes[s]=(150,18);
        var spellSurf=LegacyWindowTestKit.Surfaces(spellSizes);
        var root=new UiRoot();
        var item=new ItemWindowPanel();item.Load(UiLayout.Parse(ItemWindowPanel.TestXml,"uimagicitem.xml"),itemSurf.Get);root.Add(item);
        var spell=new SpellWindowPanel();spell.Load(UiLayout.Parse(SpellWindowPanel.TestXml(),"uimagicitem.xml"),spellSurf.Get);root.Add(spell);
        var menu=new MenuButtonsPanel(root);
        var cmd=new MenuCommands();
        root.Update(state);menu.Update(state);
        t.Check(!item.Visible&&!spell.Visible,"both windows closed at start");
        t.Check(menu.Key(Down(Keys.Tab),cmd)&&item.IsOpen,"Tab opens ITEM_WINDOW");
        root.Update(state);
        t.Check(item.Visible&&item.TabItems.Count==2&&root.WindowStack.FirstOrDefault()==item,"opened item window shows the bridge inventory and is on top");
        t.Check(menu.Key(Up(Keys.Tab),cmd)&&item.IsOpen,"key up is swallowed without toggling");
        t.Check(menu.Key(Down(Keys.Tab,repeat:true),cmd)&&item.IsOpen,"auto-repeat does not toggle");
        t.Check(menu.Key(Down(Keys.Control|Keys.I),cmd)&&!item.IsOpen,"Ctrl+I toggles it closed");
        t.Check(menu.Key(Down(Keys.Control|Keys.S),cmd)&&spell.IsOpen,"Ctrl+S opens SPELL_WINDOW");
        root.Update(state);
        t.Check(spell.Visible&&spell.Class==LegacySpellClass.Elf,"spell window reads the class from the bridge state");
        t.Check(!menu.Key(Down(Keys.Control|Keys.M),cmd)&&!menu.Key(Down(Keys.I),cmd)&&!menu.Key(Down(Keys.S),cmd),"other keys not taken (Ctrl+M stays with Field_Map; single letters unused, FU4§1.1)");
        MenuButtonsPanel.Invoke(a[4],cmd,root);MenuButtonsPanel.Invoke(a[3],cmd,root);
        t.Check(item.IsOpen&&!spell.IsOpen&&cmd.Calls.Count==0,"menu 4 opens the item window, menu 3 closes the spell window; no dev tools");
        MenuButtonsPanel.Invoke(a[13],cmd,root);
        t.Equal("dev-tools:小地圖",cmd.Calls.LastOrDefault(),"Map without a Field_Map panel → dev tools (transition)");
        var mapRoot=new UiRoot();var field=new FieldMapPanel(new MapState(null));mapRoot.Add(field);cmd.Calls.Clear();
        MenuButtonsPanel.Invoke(a[13],cmd,mapRoot);
        t.Check(cmd.Calls.Count==0&&!field.Map.FieldOpen,"Map with Field_Map but no current map: nothing opens, no dev tools (message 1734 not wired, 待驗證)");
        foreach(int k in new[]{1,2,5,6})MenuButtonsPanel.Invoke(a[k],cmd,root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"dev-tools:信件","dev-tools:角色","dev-tools:血盟","dev-tools:選項"}),"1/2/5/6 keep their dev-tools page (page passed on)",cmd.Calls);
        cmd.Calls.Clear();
        MenuButtonsPanel.Invoke(a[7],cmd,root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"play-stats"}),"7 → TogglePlayStats, no logout",cmd.Calls);
        cmd.Calls.Clear();cmd.Available=false;
        MenuButtonsPanel.Invoke(a[7],new UiMacroCommands(cmd,(_,_)=>{}),root);
        t.Check(cmd.Calls.SequenceEqual(new[]{"play-stats","dev-tools:"}),"stats unavailable → dev tools; found through UiMacroCommands; still no logout",cmd.Calls);
        var plain=new UiNullCommands();MenuButtonsPanel.Invoke(a[7],plain);
        t.Check(plain.Calls.SequenceEqual(new[]{"dev-tools:"})&&!plain.Calls.Contains("logout"),"without menu commands → dev tools, never logout",plain.Calls);

        // 7. 停用的窗不切換，改開 dev tools；離開世界時熱鍵不作用。
        root.Fault(spell,"test",new InvalidOperationException("synthetic"));cmd.Calls.Clear();
        MenuButtonsPanel.Invoke(a[3],cmd,root);
        t.Equal("dev-tools:魔法",cmd.Calls.LastOrDefault(),"faulted spell window → dev tools");
        menu.Update(Chain(false,null));
        t.Check(!menu.Key(Down(Keys.Tab),cmd),"outside the world the hotkeys are not taken");
    }
}
