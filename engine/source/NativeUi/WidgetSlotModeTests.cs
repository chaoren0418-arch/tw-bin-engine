namespace P06.NativeUi;

// T425（#969，FU15§2.4／FU17§2.2）widget-slot-mode 離線測試：ButtonMode（ModeChange）與 Alt+R 在 BAR_485 切換
// MINI_MAP_WINDOW（模式 0）↔ EX_MACRO_WINDOW（模式 1），存設定、進世界時還原。版面 XML 與圖都是合成資料（座標照 kor25
// WidgetSlot.xml／WidgetSubSlot.xml，檔案本身不放進 repo）；設定用記憶體存放處，不寫檔、不讀客戶端、不啟動遊戲、不連網路。
// CI 由 --module-tests 自動發現執行。
static class WidgetSlotModeTests {
    sealed class MemoryStore(int? initial=null):IWidgetSlotModeStore {
        public int? Value=initial;public int Saves;public bool Fail;
        public int? Load()=>Value;
        public bool Save(int mode){Saves++;if(Fail)return false;Value=mode;return true;}
    }
    sealed class State(MacroBook book,bool inWorld=true):IUiGameState,IUiMacroState {
        public bool World=inWorld;
        public bool InWorld=>World;public string? CharacterName=>null;public int? Level=>null;
        public long? Hp=>null;public long? MaxHp=>null;public long? Mp=>null;public long? MaxMp=>null;public long Now=>0;
        public MacroBook Macros=>book;public MacroInventory? Inventory=>null;public bool InputAllowed=>false;public Bitmap? ItemIcon(ushort id)=>null;
    }
    static UiSurface S(int id,int w,int h)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [3176]=S(3176,157,124),[3226]=S(3226,158,125),[2331]=S(2331,36,36),
        [3193]=S(3193,20,12),[3194]=S(3194,20,12),[3196]=S(3196,20,12),[3197]=S(3197,20,12),[3199]=S(3199,20,12),[3200]=S(3200,20,12),
        [3191]=S(3191,16,30),[3192]=S(3192,16,30),[3292]=S(3292,24,23),[3293]=S(3293,11,16),[3294]=S(3294,11,16),
    };
    static readonly Dictionary<int,UiSurface> Pngs=new(){[3350]=S(3350,14,14),[3351]=S(3351,14,14)};
    const string Host="""
        <?xml version="1.0"?>
        <MainStatusConfigulation>
          <RIGHT_WINDOW x = "643" y = "476">
            <MACRO_MAIN_WINDOW><MACRO_FRAME surfNumber = "3176" xRPos = "0" yRPos = "0"/></MACRO_MAIN_WINDOW>
            <BUTTON_ONOFF_LIST><ONOFF_BUTTON title = "MACRO_LOCK" action = "8" surfNumber = "3191" xRPos = "-9" yRPos = "37"/></BUTTON_ONOFF_LIST>
            <BUTTON_TAB_LIST>
              <TAB_BUTTON title = "MACRO_SELECT" action = "10" surfNumber = "3193" xRPos = "15" yRPos = "5"/>
              <TAB_BUTTON title = "MACRO_SELECT" action = "11" surfNumber = "3196" xRPos = "37" yRPos = "5"/>
              <TAB_BUTTON title = "MACRO_SELECT" action = "12" surfNumber = "3199" xRPos = "60" yRPos = "5"/>
            </BUTTON_TAB_LIST>
            <ETC>
              <SELECT_MARK surfNumber = "2331" xRPos = "12" yRPos = "19"/>
              <MACRO_ICON surfNumber = "" xRPos = "14" yRPos = "21"/>
            </ETC>
          </RIGHT_WINDOW>
          <EX_MACRO_WINDOW x = "485" y = "475">
            <MAIN_WINDOW><EX_MACRO_FRAME surfNumber = "3226" xRPos = "0" yRPos = "0"/></MAIN_WINDOW>
          </EX_MACRO_WINDOW>
        </MainStatusConfigulation>
        """;
    const string Main="""
        <?xml version="1.0"?>
        <WidgetSlot Name="WidgetSlot">
          <Window Name="Slotwindow" X="643" Y="476" BaseSurf="3176;1" Activate="1" MostTop="1">
            <Button Name="ButtonLock" X="0" Y="47" BaseSurf="3293;1" PushSurf="3294;1" MouseEvent="LockButton"/>
            <Image Name="CornerFrame" X="0" Y="0" Surf="3292;1"/>
            <Button Name="ButtonMode" X="-6" Y="22" BasePng="3350;1" PushPng="3351;1" MouseEvent="ModeChange"/>
          </Window>
        </WidgetSlot>
        """;
    const string Sub="""
        <?xml version="1.0"?>
        <WidgetSubSlot Name="WidgetSubSlot">
          <Window Name="SubSlotWindow" X="485" Y="475" BaseSurf="3226;1" Activate="1" MostTop="1">
            <Button Name="ButtonLock" X="149" Y="37" BaseSurf="3191;1" PushSurf="3192;1" MouseEvent="LockButton"/>
            <Button Name="ButtonMode" X="152" Y="23" BasePng="3350;1" PushPng="3351;1" MouseEvent="ModeChange"/>
          </Window>
        </WidgetSubSlot>
        """;
    const string MiniMap=MiniMapPanel.PanelId,Ex=ExMacroPanel.WindowId,PartyRt="PARTY_PET_RT_WINDOW";
    /// <summary>ButtonMode of both boxes: Window+(−6,22) = (637,498) for the main box, Window+(152,23) = (637,498) for the sub box.</summary>
    static readonly Point ModeAt=new(640,500);

    static UiLayout L(string xml,string name)=>UiLayout.Parse(xml,name);
    /// <summary>BAR_485 with the minimap added first (so it is current, as with the registry order 60 → 70).</summary>
    static UiSlotGroup Group(bool minimap=true){var g=new UiSlotGroup(UiSlotGroup.Bar485);if(minimap)g.Add(MiniMap);return g;}
    static MacroPanel MainPanel(UiSlotGroup? group,MemoryStore store,bool widget=true){
        var p=new MacroPanel(null,group){ModeStore=store};
        p.Load(L(Host,"uistatus86-c.xml"),id=>Surfs.GetValueOrDefault(id),widget?L(Main,"WidgetSlot.xml"):null,id=>Pngs.GetValueOrDefault(id));
        return p;
    }
    static ExMacroPanel SubPanel(UiSlotGroup group,MemoryStore store){
        var p=new ExMacroPanel(group){ModeStore=store};
        p.Load(L(Host,"uistatus86-c.xml"),id=>Surfs.GetValueOrDefault(id),L(Sub,"WidgetSubSlot.xml"),id=>Pngs.GetValueOrDefault(id));
        return p;
    }
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static UiKeyEvent AltR(bool down=true,bool repeat=false)=>new(Keys.Alt|Keys.R,down,repeat);

    [WorldModuleTest("widget-slot-mode")]
    static void Run(WorldModuleTestContext t){
        Mapping(t);
        Toggle(t);
        Restore(t);
        MainButton(t);
        AltRKey(t);
        EnterWorld(t);
        SubButton(t);
        Registration(t);
    }

    // FU17§2.1: widget 2 = MiniMapUI → MINI_MAP_WINDOW, widget 4 = WidgetSubSlot → EX_MACRO_WINDOW.
    static void Mapping(WorldModuleTestContext t){
        t.Equal(MiniMap,WidgetSlotMode.MemberOf(0),"mode 0 → MINI_MAP_WINDOW (widget 2 shown, 4 hidden)");
        t.Equal(Ex,WidgetSlotMode.MemberOf(1),"mode 1 → EX_MACRO_WINDOW (widget 2 hidden, 4 shown)");
        t.Equal("widget-slot-mode",WidgetSlotMode.SettingKey,"option-ui.txt key");
        var g=Group();g.Add(Ex);g.Add(PartyRt);
        t.Equal(0,WidgetSlotMode.Of(g),"minimap current → mode 0");
        g.Show(Ex);t.Equal(1,WidgetSlotMode.Of(g),"extended macro current → mode 1");
        g.Show(PartyRt);t.Equal(0,WidgetSlotMode.Of(g),"party RT current counts as mode 0 (推測, FU17§2.3)");
        t.Check(!WidgetSlotMode.Apply(g,2)&&g.Current==PartyRt,"mode 2 is not a WidgetSlot mode → nothing shown");
        t.Check(WidgetSlotMode.Apply(g,1)&&g.Current==Ex,"Apply(1) shows EX_MACRO_WINDOW");
        t.Check(WidgetSlotMode.Apply(g,0)&&g.Current==MiniMap,"Apply(0) shows MINI_MAP_WINDOW");
        t.Check(!WidgetSlotMode.Apply(Group(minimap:false),0),"member not registered → not applied");
    }

    static void Toggle(WorldModuleTestContext t){
        var g=Group();g.Add(Ex);g.Add(PartyRt);var store=new MemoryStore();int changed=0;g.Changed+=_=>changed++;
        t.Check(WidgetSlotMode.Toggle(g,store,"test")==1&&g.Current==Ex&&store.Value==1,"0 → 1, saved 1");
        t.Check(WidgetSlotMode.Toggle(g,store,"test")==0&&g.Current==MiniMap&&store.Value==0,"1 → 0, saved 0");
        t.Equal(2,changed,"each switch raises Changed once");
        t.Check(UiResourceLog.Recent().Any(l=>l.Contains("widget-slot test "+WidgetSlotLayout.ModeChangeEvent)&&l.Contains("模式 0")),"switch logged");
        g.Show(PartyRt);
        t.Check(WidgetSlotMode.Toggle(g,store,"test")==1&&g.Current==Ex,"party RT (mode 0) → mode 1");
        g.Cycle();t.Equal(PartyRt,g.Current,"Alt+F3 cycle order is unchanged (minimap → extended → party RT)");

        g.Show(Ex);store.Fail=true;
        t.Check(WidgetSlotMode.Toggle(g,store,"test")==0&&g.Current==MiniMap&&store.Value==1,"write fails → still switched, old saved value kept");
        store.Fail=false;

        var only=Group(minimap:false);only.Add(Ex);var s2=new MemoryStore();
        t.Check(WidgetSlotMode.Toggle(only,s2,"test")==null&&only.Current==Ex&&s2.Saves==0,"minimap not registered → no switch, nothing saved");
        var noEx=Group();
        t.Check(WidgetSlotMode.Toggle(noEx,s2,"test")==null&&noEx.Current==MiniMap&&s2.Saves==0,"extended macro not registered → no switch");
        t.Check(WidgetSlotMode.Toggle(null,s2,"test")==null&&s2.Saves==0,"no group → no switch");
    }

    static void Restore(WorldModuleTestContext t){
        var g=Group();g.Add(Ex);
        t.Check(!WidgetSlotMode.Restore(g,new MemoryStore())&&g.Current==MiniMap,"nothing saved → default mode 0 kept (sub_705910)");
        t.Check(WidgetSlotMode.Restore(g,new MemoryStore(1))&&g.Current==Ex,"saved 1 → EX_MACRO_WINDOW");
        t.Check(WidgetSlotMode.Restore(g,new MemoryStore(0))&&g.Current==MiniMap,"saved 0 → MINI_MAP_WINDOW");
        t.Check(!WidgetSlotMode.Restore(null,new MemoryStore(1)),"no group → nothing");
    }

    static void MainButton(WorldModuleTestContext t){
        var g=Group();var store=new MemoryStore();var p=MainPanel(g,store);var ex=SubPanel(g,store);
        var book=new MacroBook();p.Update(new State(book));ex.Update(new State(book));var cmd=new UiNullCommands();
        t.Check(p.Widget?.Mode?.Rect.Contains(ModeAt)==true,"main ButtonMode at (637,498)",p.Widget?.Mode?.Rect);
        t.Check(g.Current==MiniMap&&!ex.Visible,"start: minimap, extended box hidden");
        t.Check(p.Mouse(M(UiMouseKind.Down,ModeAt),cmd)&&p.ModePressed&&g.Current==MiniMap,"press: not switched yet (ModeChange is 514 = button up)");
        t.Check(p.Mouse(M(UiMouseKind.Up,ModeAt),cmd)&&!p.ModePressed&&g.Current==Ex&&ex.Visible&&store.Value==1,"release on the button → mode 1, extended box shown, saved");
        p.Mouse(M(UiMouseKind.Down,ModeAt),cmd);
        t.Check(p.Mouse(M(UiMouseKind.Up,new(700,560)),cmd)&&g.Current==Ex&&store.Saves==1,"release elsewhere → press ends, no switch");
        p.Mouse(M(UiMouseKind.Down,ModeAt),cmd);p.Mouse(M(UiMouseKind.Up,ModeAt),cmd);
        t.Check(g.Current==MiniMap&&!ex.Visible&&store.Value==0,"second click → mode 0, minimap again");
        t.Check(book.Page==0&&!book.Locked,"page and lock untouched");

        var classic=MainPanel(g,store,widget:false);classic.Update(new State(book));
        t.Check(!classic.Mouse(M(UiMouseKind.Down,ModeAt),cmd)&&g.Current==MiniMap,"no WidgetSlot.xml → no button, no switch");
    }

    static void AltRKey(WorldModuleTestContext t){
        var g=Group();var store=new MemoryStore();var p=MainPanel(g,store);_=SubPanel(g,store);
        var book=new MacroBook();var cmd=new UiNullCommands();
        t.Check(!p.Key(AltR(),cmd)&&g.Current==MiniMap,"before entering the world → not taken");
        p.Update(new State(book));
        t.Check(p.Key(AltR(),cmd)&&g.Current==Ex&&store.Value==1,"Alt+R → mode 1, saved (InputAllowed false: the edit-box paths have it too)");
        t.Check(p.Key(AltR(repeat:true),cmd)&&g.Current==Ex,"auto-repeat swallowed, no switch");
        t.Check(p.Key(AltR(down:false),cmd)&&g.Current==Ex,"key-up swallowed");
        t.Check(p.Key(AltR(),cmd)&&g.Current==MiniMap&&store.Value==0,"Alt+R again → mode 0");
        t.Check(!p.Key(new UiKeyEvent(Keys.R,true,false),cmd)&&!p.Key(new UiKeyEvent(Keys.Control|Keys.R,true,false),cmd)&&g.Current==MiniMap,"plain R / Ctrl+R are not the switch");
        t.Check(UiResourceLog.Recent().Any(l=>l.StartsWith("widget-slot Alt+R "+WidgetSlotLayout.ModeChangeEvent,StringComparison.Ordinal)),"Alt+R switch logged");

        var classic=MainPanel(g,store,widget:false);classic.Update(new State(book));
        t.Check(!classic.Key(AltR(),cmd)&&g.Current==MiniMap,"no WidgetSlot.xml (3.81 look) → Alt+R left to the game");
        var lone=MainPanel(null,store);lone.Update(new State(book));
        t.Check(!lone.Key(AltR(),cmd),"no BAR_485 group → Alt+R not taken");
    }

    static void EnterWorld(WorldModuleTestContext t){
        var g=Group();var store=new MemoryStore(1);var p=MainPanel(g,store);_=SubPanel(g,store);
        var state=new State(new MacroBook(),inWorld:false);
        p.Update(state);t.Equal(MiniMap,g.Current,"outside the world → nothing restored");
        state.World=true;p.Update(state);t.Equal(Ex,g.Current,"entering the world → saved mode 1 restored (sub_59DD30)");
        g.Show(MiniMap);p.Update(state);t.Equal(MiniMap,g.Current,"later updates do not re-apply");
        state.World=false;p.Update(state);state.World=true;p.Update(state);t.Equal(Ex,g.Current,"next entry restores again");
        t.Equal(0,store.Saves,"restoring does not write the setting");

        var g2=Group();var classic=MainPanel(g2,new MemoryStore(1),widget:false);_=SubPanel(g2,new MemoryStore());
        classic.Update(new State(new MacroBook()));
        t.Equal(MiniMap,g2.Current,"no WidgetSlot.xml → BAR_485 start unchanged (uistatus86 / 3.81)");
    }

    static void SubButton(WorldModuleTestContext t){
        var g=Group();var store=new MemoryStore();var ex=SubPanel(g,store);g.Show(Ex);
        var book=new MacroBook();ex.Update(new State(book));var cmd=new UiNullCommands();
        t.Check(ex.Visible&&ex.Widget?.Mode?.Rect.Contains(ModeAt)==true,"mode 1: extended box shown with its ButtonMode at (637,498)");
        t.Check(ex.Mouse(M(UiMouseKind.Down,ModeAt),cmd)&&g.Current==Ex,"press: not switched yet");
        t.Check(ex.Mouse(M(UiMouseKind.Up,ModeAt),cmd)&&g.Current==MiniMap&&!ex.Visible&&store.Value==0,"release → mode 0, extended box hidden, saved 0");
        t.Check(!ex.ModePressed,"press ended");
    }

    static void Registration(WorldModuleTestContext t){
        var regs=UiPanelRegistry.Panels.Where(r=>r.Id==MacroPanel.WindowId).ToArray();
        t.Check(regs.Length==1&&regs[0].Layout=="uistatus86","RIGHT_WINDOW still registered once on uistatus86",string.Join(",",regs.Select(r=>r.Layout)));
        t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(nameof(MacroPanel))),"no discovery error for MacroPanel");
        t.Check(ReferenceEquals(WidgetSlotMode.SettingsFile,new MacroPanel().ModeStore)&&ReferenceEquals(WidgetSlotMode.SettingsFile,new ExMacroPanel(Group()).ModeStore),"product store = option-ui.txt (OptionSettingsFile)");
    }
}
