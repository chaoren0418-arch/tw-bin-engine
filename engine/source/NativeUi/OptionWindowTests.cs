using System.Reflection;
namespace P06.NativeUi;

// R9-Q（T377／#861）option-window 離線測試：選項窗勾選框接引擎既有設定（點一下就生效）、主選單鈕 6／Ctrl+V 的入口裁定（#818）。
// 版面 XML 與圖都是合成資料（不是真實 OptionUI.xml），不讀客戶端或參考客戶端、不建 ClientForm、不開視窗。CI 由 --module-tests 自動發現執行。
static class OptionWindowTests {
    const uint BaseC=0xFF202020,OffC=0xFFFFFF00,OnC=0xFF00FF00;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static readonly Dictionary<int,UiSurface> Pngs=new(){[901]=Surface(901,200,160,BaseC),[930]=Surface(930,12,12,OffC),[931]=Surface(931,12,12,OnC)};

    static string Check(string name,int x,int y,string ev)=>
        $"""<CheckButton Name="{name}" X="{x}" Y="{y}" BasePng="930" PushPng="931" Width="12" Height="12" MouseEvent="{ev}"/>""";
    // Same element kinds and MouseEvent names as kor25 OptionUI.xml (Home／Game／Etc 各頁的勾選框；同一選項出現在兩頁)。
    static string Xml()=>$"""
        <?xml version="1.0"?>
        <OptionUI>
          <Window Name="Option" X="100" Y="50" BasePng="901;1" Moveable="30">
            <Button Name="GameBtn" X="0" Y="0" BasePng="930" MouseEvent="GameBtn"/>
            <Button Name="EtcBtn" X="20" Y="0" BasePng="930" MouseEvent="EtcBtn"/>
            <Control Name="Home" X="0" Y="0">
              {Check("ShowDropItemName",10,40,"ShowDropItemNameBtn")}
              {Check("ShowNpcName",40,40,"ShowNpcNameBtn")}
              {Check("EnableElfTelControl",70,40,"EnableElfTelControlBtn")}
              {Check("TransparentDeadMonster",100,40,"TransparentDeadMonsterBtn")}
              {Check("Mute_Button",130,40,"CheckMute")}
            </Control>
            <Control Name="Game" X="0" Y="0">
              {Check("EnableElfTelControl",10,40,"EnableElfTelControlBtn")}
              {Check("EnableTrade",40,40,"EnableTradeBtn")}
            </Control>
            <Control Name="Etc" X="0" Y="0">
              {Check("ShowCharacterHPMP",10,40,"ShowCharacterHPMPBtn")}
              {Check("HPMPBarThick",40,40,"HPMPBarThickBtn")}
              {Check("HPMPBarRecovery",70,40,"HPMPBarRecoveryBtn")}
              {Check("HPMPBarPercent",100,40,"HPMPBarPercentBtn")}
              {Check("ShowNpcName",130,40,"ShowNpcNameBtn")}
            </Control>
          </Window>
        </OptionUI>
        """;

    sealed class Host:IOptionWindowHost {
        public readonly Dictionary<string,bool> Values=new(){[OptionPanel.ShowNpcNameEvent]=false,[OptionPanel.MuteEvent]=false,[OptionPanel.ElfTeleportEvent]=false};
        public readonly List<string> Sets=[];
        public bool Refuse;
        public bool? Value(string e)=>Values.TryGetValue(e,out var v)?v:null;
        public bool Set(string e,bool v){Sets.Add($"{e}={v}");if(Refuse||!Values.ContainsKey(e))return false;Values[e]=v;return true;}
    }
    sealed class Commands:IUiCommands,IUiMenuWindowCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool OpenMail(){Calls.Add("mail");return true;}
        public bool ToggleLocalPage(MenuLocalPage page){Calls.Add("page:"+page);return true;}
        public bool CaptureScreen(){Calls.Add("capture");return true;}
    }

    static (UiRoot Root,HpMpPanel HpMp,OptionPanel Option) Setup(bool load=true){
        var root=new UiRoot();var hp=new HpMpPanel();root.Add(hp);
        var p=new OptionPanel(null,()=>OptionPanel.HpMpWindow(root));
        if(load)p.Load(UiLayout.Parse(Xml(),"OptionUI.xml"),_=>null,id=>Pngs.GetValueOrDefault(id));
        root.Add(p);
        return (root,hp,p);
    }
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(OptionPanel p,Point at){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    /// <summary>Window origin (100, 50) + check button (x, 40) + (5, 5) inside the 12×12 box.</summary>
    static Point At(int x)=>new(100+x+5,50+40+5);
    static int Pixel(OptionPanel p,Point at){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(at.X,at.Y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("option-window")]
    static void Run(WorldModuleTestContext t){
        Tables(t);
        HpMpOptions(t);
        HostOptions(t);
        NotWired(t);
        MenuEntry(t);
        ProductTypes(t);
    }

    static void Tables(WorldModuleTestContext t){
        t.Equal("ShowCharacterHPMPBtn,HPMPBarThickBtn,HPMPBarRecoveryBtn,HPMPBarPercentBtn",string.Join(",",OptionPanel.HpMpEvents),"four HP/MP options (FU9§14.4, ui-layout §2 Etc page)");
        t.Equal("ShowNpcNameBtn,CheckMute,EnableElfTelControlBtn",string.Join(",",OptionPanel.HostEvents),"host options: names, mute, elf teleport");
        t.Check(!OptionPanel.HpMpEvents.Intersect(OptionPanel.HostEvents).Any()&&!OptionPanel.NotWired.Keys.Intersect(OptionPanel.HpMpEvents.Concat(OptionPanel.HostEvents)).Any(),"wired and not-wired lists do not overlap");
        var s=HpMpSettings.Default;
        foreach(var e in OptionPanel.HpMpEvents){
            bool v=OptionPanel.HpMpValue(s,e)!.Value;
            t.Check(OptionPanel.HpMpValue(OptionPanel.HpMpWith(s,e,!v)!,e)==!v,e+": with/value round-trip");
        }
        t.Check(OptionPanel.HpMpValue(s,"ShowNpcNameBtn")==null&&OptionPanel.HpMpWith(s,"x",true)==null,"other events are not HP/MP");
    }

    static void HpMpOptions(WorldModuleTestContext t){
        var (_,hp,p)=Setup();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        p.Update(new UiStaticGameState(InWorld:true));p.Open();p.ShowPage("Etc");
        t.Check(p.Checked(OptionPanel.ShowCharacterHpMpEvent)==true&&p.Checked(OptionPanel.HpMpBarThickEvent)==false,"values read from HpMpPanel.Settings (defaults: show on, thick off)");
        t.Equal(Argb(OnC),Pixel(p,At(10)),"option on → drawn checked (PushPng)");
        t.Equal(Argb(OffC),Pixel(p,At(40)),"option off → normal image");

        Click(p,At(10));
        t.Check(!hp.Settings.ShowCharacterHpMp&&p.Checked(OptionPanel.ShowCharacterHpMpEvent)==false,"ShowCharacterHPMP click → off at once (no OK step)");
        t.Equal(Argb(OffC),Pixel(p,At(10)),"redrawn unchecked");
        Click(p,At(10));
        t.Check(hp.Settings.ShowCharacterHpMp,"second click → on again");

        Click(p,At(40));
        t.Check(hp.Settings.Thickness&&hp.Layout.Thick,"HPMPBarThick → thick layout (re-laid out like sub_ED8F20)");
        Click(p,At(70));
        t.Check(!hp.Settings.ShowRecovery,"HPMPBarRecovery → off");
        Click(p,At(100));
        t.Check(!hp.Settings.ShowPercent&&!hp.PercentShown,"HPMPBarPercent → percentage hidden");
        t.Check(p.Visible&&p.Unhandled.Count==0,"window stays open; nothing recorded as unhandled",string.Join(",",p.Unhandled));

        // No HP/MP window in the root → the HP/MP check buttons have nothing to change.
        var lone=new OptionPanel();lone.Load(UiLayout.Parse(Xml(),"OptionUI.xml"),_=>null,id=>Pngs.GetValueOrDefault(id));lone.Open();lone.ShowPage("Etc");
        Click(lone,At(10));
        t.Check(lone.Checked(OptionPanel.ShowCharacterHpMpEvent)==null&&lone.Unhandled.Contains("ShowCharacterHPMPBtn"),"no HpMpPanel → not wired (Unhandled)");
    }

    static void HostOptions(WorldModuleTestContext t){
        var (_,_,p)=Setup();p.Open();
        Click(p,At(40));
        t.Check(p.Unhandled.Contains("ShowNpcNameBtn"),"no host attached → ShowNpcName not wired");
        var host=new Host();p.Attach(host);
        t.Check(p.Host==host,"host attached");
        Click(p,At(40));
        t.Check(host.Values[OptionPanel.ShowNpcNameEvent]&&host.Sets.SequenceEqual(["ShowNpcNameBtn=True"]),"ShowNpcName → host Set(true) at once",host.Sets.ToArray());
        t.Equal(Argb(OnC),Pixel(p,At(40)),"drawn checked from the host value");
        Click(p,At(70));
        t.Check(host.Values[OptionPanel.ElfTeleportEvent],"EnableElfTelControl (Home) → elf teleport option on");
        p.ShowPage("Game");
        t.Equal(Argb(OnC),Pixel(p,At(10)),"same option on the Game page shows the same value");
        Click(p,At(10));
        t.Check(!host.Values[OptionPanel.ElfTeleportEvent],"Game page button flips the same option");
        p.ShowPage("Etc");
        t.Equal(Argb(OnC),Pixel(p,At(130)),"ShowNpcName on the Etc page = same setting as Home");
        p.ShowPage("Home");
        Click(p,At(130));
        t.Check(host.Values[OptionPanel.MuteEvent],"CheckMute → muted");
        host.Refuse=true;host.Sets.Clear();
        Click(p,At(130));
        t.Check(host.Values[OptionPanel.MuteEvent]&&host.Sets.SequenceEqual(["CheckMute=False"])&&!p.Unhandled.Contains("CheckMute"),"host refuses (audio closed) → value kept, not recorded as unwired",host.Sets.ToArray());
        t.Check(UiResourceLog.Recent().Any(l=>l.Contains("CheckMute")&&l.Contains("未套用")),"refusal logged");
    }

    static void NotWired(WorldModuleTestContext t){
        var (_,_,p)=Setup();var host=new Host();p.Attach(host);p.Open();
        Click(p,At(10));Click(p,At(100));
        t.Check(p.Unhandled.Contains("ShowDropItemNameBtn")&&p.Unhandled.Contains("TransparentDeadMonsterBtn")&&host.Sets.Count==0,"drop names / transparent corpses: no engine setting → Unhandled, host untouched",string.Join(",",p.Unhandled));
        t.Check(OptionPanel.NotWired.ContainsKey("ShowDropItemNameBtn")&&OptionPanel.NotWired.ContainsKey("TransparentDeadMonsterBtn"),"reasons listed");
        p.ShowPage("Game");Click(p,At(40));
        t.Check(p.Unhandled.Contains("EnableTradeBtn"),"TW16-only option stays unwired");
        t.Equal(Argb(OffC),Pixel(p,At(40)),"unwired check button drawn normal");
    }

    static void MenuEntry(WorldModuleTestContext t){
        var (root,_,p)=Setup();
        var menu=new MenuButtonsPanel(root);menu.Update(new UiStaticGameState(InWorld:true));
        var cmd=new Commands();
        t.Check(MenuButtonsPanel.OptionWindow(root)==p,"usable option window found");
        MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[6],cmd,root);
        t.Check(p.Visible&&root.WindowStack.Contains(p)&&cmd.Calls.Count==0,"menu 6 → native OptionUI opens (Home), no HTML option1",cmd.Calls.ToArray());
        t.Equal("Home",p.ActivePage?.Name,"opens on Home");
        MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[6],cmd,root);
        t.Check(!p.Visible&&cmd.Calls.Count==0,"menu 6 again → closes");
        t.Check(menu.Key(new UiKeyEvent(Keys.Control|Keys.V,true,false),cmd)&&p.Visible&&cmd.Calls.Count==0,"Ctrl+V → native OptionUI",cmd.Calls.ToArray());
        menu.Key(new UiKeyEvent(Keys.Control|Keys.V,false,false),cmd);
        menu.Key(new UiKeyEvent(Keys.Control|Keys.V,true,false),cmd);
        t.Check(!p.Visible,"Ctrl+V again → closes");
        MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[5],cmd,root);
        t.Check(cmd.Calls.SequenceEqual(["page:Option2"]),"menu 5 still HTML option2",cmd.Calls.ToArray());

        // Option window unavailable (no reference OptionUI.xml) → HTML option1 as before.
        var (root2,_,p2)=Setup(load:false);cmd.Calls.Clear();
        t.Check(!p2.Available&&MenuButtonsPanel.OptionWindow(root2)==null,"unloaded option window is not usable");
        MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[6],cmd,root2);
        MenuButtonsPanel.Run(MenuHotkeyCommand.Option1,cmd,root2);
        t.Check(cmd.Calls.SequenceEqual(["page:Option1","page:Option1"])&&!p2.Visible,"unavailable → menu 6 / Ctrl+V open HTML option1",cmd.Calls.ToArray());
        // No root (no native UI registry, e.g. callers without one) → HTML option1.
        cmd.Calls.Clear();MenuButtonsPanel.Invoke(MenuButtonsPanel.Actions[6],cmd,null);
        t.Check(cmd.Calls.SequenceEqual(["page:Option1"]),"no root → HTML option1",cmd.Calls.ToArray());
    }

    static void ProductTypes(WorldModuleTestContext t){
        // Registration: the registered panel reads HP/MP from the root it is created for.
        var reg=UiPanelRegistry.Panels.Single(r=>r.Id==OptionPanel.PanelId);
        var root=new UiRoot();var hp=new HpMpPanel(HpMpSettings.Default with{ShowPercent=false});root.Add(hp);
        var made=(OptionPanel)reg.Create(root);
        t.Check(made.Checked(OptionPanel.HpMpBarPercentEvent)==false&&made.Checked(OptionPanel.ShowCharacterHpMpEvent)==true,"registered panel bound to the root's HpMpPanel");
        // ClientForm host (RingWindows.cs): type only, ClientForm is not built offline.
        var hostType=typeof(ClientForm).GetNestedType("OptionWindowHost",BindingFlags.NonPublic|BindingFlags.Public);
        t.Check(hostType!=null&&typeof(IOptionWindowHost).IsAssignableFrom(hostType),"ClientForm.OptionWindowHost implements IOptionWindowHost");
        // EnableElfTelControl feeds the existing rule: elf (class 2), Lv ≥ 50, option on.
        t.Check(ElfBookmarkTeleportRules.Allows(2,50,true)&&!ElfBookmarkTeleportRules.Allows(2,50,false)&&!ElfBookmarkTeleportRules.Allows(2,49,true)&&!ElfBookmarkTeleportRules.Allows(0,60,true),"elf option gate unchanged");
        var w=new WorldSession();
        t.Check(!w.ElfBookmarkTeleportOption,"elf option default off (byte_13A931C bss 0)");
    }
}
