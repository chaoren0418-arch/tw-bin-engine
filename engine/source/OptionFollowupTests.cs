using System.Reflection;
using P06.NativeUi;
namespace P06;

// R10-I（T393／#908，gap-matrix 15.12.6）option-followup 離線測試：EnableElfTelControl 存檔與 Lv<50 清除、ShowDropItemName／
// TransparentDeadMonster 兩個顯示開關、音量捲軸對到主音量。版面 XML 與圖都是合成資料（不是真實 OptionUI.xml），不讀客戶端或參考
// 客戶端、不建 ClientForm、不開視窗、不寫檔（OptionSettingsFile 只測文字格式）。CI 由 --module-tests 自動發現執行。
static class OptionFollowupTests {
    const uint BaseC=0xFF202020,OffC=0xFFFFFF00,OnC=0xFF00FF00,TrackC=0xFF404080,ThumbC=0xFFFF0000;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static readonly Dictionary<int,UiSurface> Pngs=new(){[901]=Surface(901,320,200,BaseC),[930]=Surface(930,12,12,OffC),[931]=Surface(931,12,12,OnC),
        [934]=Surface(934,219,2,TrackC),[935]=Surface(935,9,12,ThumbC)};

    static string Check(string name,int x,int y,string ev)=>
        $"""<CheckButton Name="{name}" X="{x}" Y="{y}" BasePng="930" PushPng="931" Width="12" Height="12" MouseEvent="{ev}"/>""";
    // 與 kor25 OptionUI.xml 相同的元素型態與名稱（Application 頁 4 條音量捲軸：Background_Image＋Move_Button，ScrollWidth 219）。
    static string Bar(string name,int y)=>$"""
        <ScrollBar Name="{name}" X="20" Y="{y}" Width="219" Height="12" ScrollStart="0" ScrollWidth="219" Horizontal="1">
          <Image Name="Background_Image" Y="6" Png="934"/>
          <Button Name="Move_Button" X="0" Y="0" BasePng="935" PushPng="935"/>
        </ScrollBar>
        """;
    static string Xml()=>$"""
        <?xml version="1.0"?>
        <OptionUI>
          <Window Name="Option" X="100" Y="50" BasePng="901;1" Moveable="30">
            <Button Name="ApplicationBtn" X="0" Y="0" BasePng="930" MouseEvent="ApplicationBtn"/>
            <Control Name="Home" X="0" Y="0">
              {Check("ShowDropItemName",10,40,"ShowDropItemNameBtn")}
              {Check("TransparentDeadMonster",40,40,"TransparentDeadMonsterBtn")}
              {Check("EnableElfTelControl",70,40,"EnableElfTelControlBtn")}
            </Control>
            <Control Name="Application" X="0" Y="0">
              {Bar("MusicVolumeScrollBar",60)}
              {Bar("EffectVolumeScrollBar",80)}
              {Bar("EnvVolumeScrollBar",100)}
              {Bar("VoiceVolumeScrollBar",120)}
            </Control>
          </Window>
        </OptionUI>
        """;

    /// <summary>Stands in for ClientForm.OptionWindowHost: same rules (master gain for 3 bars, elf option cleared below Lv 50).</summary>
    sealed class Host(WorldSession world):IOptionWindowHost {
        public readonly Dictionary<string,bool> Values=new(){[OptionPanel.ShowDropItemNameEvent]=true,[OptionPanel.TransparentDeadMonsterEvent]=false};
        public double Gain=.5;public bool AudioClosed;
        public readonly List<string> Calls=[];
        public bool? Value(string e)=>e==OptionPanel.ElfTeleportEvent?world.ElfBookmarkTeleportOption:Values.TryGetValue(e,out var v)?v:null;
        public bool Set(string e,bool v){
            Calls.Add($"{e}={v}");
            if(e==OptionPanel.ElfTeleportEvent){world.ElfBookmarkTeleportOption=v;world.ClearElfTeleportOptionBelowLevel();return world.ElfBookmarkTeleportOption==v;}
            if(!Values.ContainsKey(e))return false;Values[e]=v;return true;
        }
        public double? Level(string bar)=>OptionPanel.MasterVolumeBars.Contains(bar)?Gain:null;
        public bool SetLevel(string bar,double v){Calls.Add($"{bar}={v:0.##}");if(AudioClosed||!OptionPanel.MasterVolumeBars.Contains(bar))return false;Gain=v;return true;}
    }

    static OptionPanel Panel(){var p=new OptionPanel();p.Load(UiLayout.Parse(Xml(),"OptionUI.xml"),_=>null,id=>Pngs.GetValueOrDefault(id));return p;}
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(OptionPanel p,Point at){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Point CheckAt(int x)=>new(100+x+5,50+40+5);
    /// <summary>Logical point on a bar at window y (bar X 20, origin 100/50), x pixels from the bar's left.</summary>
    static Point BarAt(int barY,int x)=>new(100+20+x,50+barY+6);
    static int Pixel(OptionPanel p,Point at){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(at.X,at.Y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("option-followup")]
    static void Run(WorldModuleTestContext t){
        Settings(t);
        ElfOption(t);
        DisplayOptions(t);
        VolumeBars(t);
        Rendering(t);
        ProductTypes(t);
    }

    static void Settings(WorldModuleTestContext t){
        var parsed=OptionSettingsFile.Parse("elf-teleport-control=1\r\nshow-drop-item-names=0\n# note\nbad\n=1\ntransparent-dead-monsters=2\nelf-teleport-control=0\n");
        t.Check(parsed.Count==2&&parsed["elf-teleport-control"]==false&&parsed["show-drop-item-names"]==false,"parse: key=0/1 lines only, CRLF ok, later line wins",string.Join(",",parsed.Select(p=>$"{p.Key}={p.Value}")));
        var text=OptionSettingsFile.Format(new Dictionary<string,bool>{[OptionSettingsFile.TransparentDeadMonstersKey]=true,[OptionSettingsFile.ElfTeleportKey]=true,[OptionSettingsFile.DropItemNamesKey]=false});
        t.Equal("elf-teleport-control=1\nshow-drop-item-names=0\ntransparent-dead-monsters=1\n",text,"format: sorted key=0|1 lines");
        t.Check(OptionSettingsFile.Parse(text).Count==3,"format/parse round-trip");
        string? saved=OptionSettingsFile.FilePath;
        try{
            OptionSettingsFile.FilePath=null;
            t.Check(OptionSettingsFile.Read(OptionSettingsFile.ElfTeleportKey)==null&&!OptionSettingsFile.Write(OptionSettingsFile.ElfTeleportKey,true),"no file configured → nothing read or written");
        }finally{OptionSettingsFile.FilePath=saved;}
        t.Equal("option-ui.txt",OptionSettingsFile.FileName,"engine user-data file name");
    }

    static void ElfOption(WorldModuleTestContext t){
        var w=new WorldSession();w.ElfBookmarkTeleportOption=true;
        t.Check(!w.ClearElfTeleportOptionBelowLevel()&&w.ElfBookmarkTeleportOption,"no own status yet → kept");
        w.SeedProtocolFollowupTest(level:49);
        t.Check(w.ClearElfTeleportOptionBelowLevel()&&!w.ElfBookmarkTeleportOption,"Lv 49 → cleared to 0 (0xEC99EB), caller saves");
        t.Check(!w.ClearElfTeleportOptionBelowLevel(),"already off → nothing to clear");
        var hi=new WorldSession();hi.SeedProtocolFollowupTest(level:50);hi.ElfBookmarkTeleportOption=true;
        t.Check(!hi.ClearElfTeleportOptionBelowLevel()&&hi.ElfBookmarkTeleportOption,"Lv 50 → kept");

        // Option window: below Lv 50 the check cannot be turned on (cleared at once, reported as not applied).
        var p=Panel();var host=new Host(w);p.Attach(host);p.Open();
        Click(p,CheckAt(70));
        t.Check(!w.ElfBookmarkTeleportOption&&host.Calls.SequenceEqual(["EnableElfTelControlBtn=True"]),"Lv 49: click → set then cleared",host.Calls.ToArray());
        t.Check(UiResourceLog.Recent().Any(l=>l.Contains("EnableElfTelControlBtn")&&l.Contains("未套用")),"refusal logged");
        t.Equal(Argb(OffC),Pixel(p,CheckAt(70)),"Lv 49: drawn unchecked");
        var p2=Panel();var host2=new Host(hi);p2.Attach(host2);p2.Open();hi.ElfBookmarkTeleportOption=false;
        Click(p2,CheckAt(70));
        t.Check(hi.ElfBookmarkTeleportOption,"Lv 50: click → on");
        t.Equal(Argb(OnC),Pixel(p2,CheckAt(70)),"Lv 50: drawn checked");
    }

    static void DisplayOptions(WorldModuleTestContext t){
        t.Equal("ShowDropItemNameBtn,TransparentDeadMonsterBtn",string.Join(",",OptionPanel.DisplayEvents),"two display options (FU9§14.4)");
        t.Check(!OptionPanel.DisplayEvents.Intersect(OptionPanel.HostEvents.Concat(OptionPanel.HpMpEvents)).Any()&&!OptionPanel.NotWired.Keys.Intersect(OptionPanel.DisplayEvents).Any(),"display options are in no other list");
        var p=Panel();var host=new Host(new WorldSession());p.Attach(host);p.Open();
        t.Equal(Argb(OnC),Pixel(p,CheckAt(10)),"ShowDropItemName default on (engine always drew ground names) → checked");
        t.Equal(Argb(OffC),Pixel(p,CheckAt(40)),"TransparentDeadMonster default off → unchecked");
        Click(p,CheckAt(10));Click(p,CheckAt(40));
        t.Check(!host.Values[OptionPanel.ShowDropItemNameEvent]&&host.Values[OptionPanel.TransparentDeadMonsterEvent]&&p.Unhandled.Count==0,"clicks take effect at once, nothing unhandled",string.Join(",",p.Unhandled));
        t.Equal(Argb(OffC),Pixel(p,CheckAt(10)),"drop names now unchecked");
        t.Equal(Argb(OnC),Pixel(p,CheckAt(40)),"transparent corpses now checked");
        // ObjectRendering.cs: ground name rule (option off → only the selected item, 推測).
        t.Check(ClientForm.GroundNameShown(true,false)&&ClientForm.GroundNameShown(false,true)&&!ClientForm.GroundNameShown(false,false),"ground name: option on → all; off → selected only");
    }

    static void VolumeBars(WorldModuleTestContext t){
        t.Equal("MusicVolumeScrollBar,EffectVolumeScrollBar,EnvVolumeScrollBar",string.Join(",",OptionPanel.MasterVolumeBars),"three bars → master gain");
        t.Check(OptionPanel.NotWired.ContainsKey(OptionPanel.VoiceVolumeBar),"voice volume not wired (3.81 has no voice chat)");
        // Slider maths (PlayerAudioSettings): 0.01 steps, clamped.
        t.Check(PlayerAudioSettings.SliderGain(0,210)==0&&PlayerAudioSettings.SliderGain(210,210)==1&&PlayerAudioSettings.SliderGain(105,210)==.5
            &&PlayerAudioSettings.SliderGain(-5,210)==0&&PlayerAudioSettings.SliderGain(999,210)==1&&PlayerAudioSettings.SliderGain(5,0)==0,"SliderGain clamp / ends / middle");
        t.Check(PlayerAudioSettings.SliderGain(1,210)==0&&PlayerAudioSettings.SliderGain(2,210)==.01,"SliderGain rounds to 0.01");
        t.Check(PlayerAudioSettings.SliderPosition(.5,210)==105&&PlayerAudioSettings.SliderPosition(2,210)==210&&PlayerAudioSettings.SliderPosition(double.NaN,210)==0&&PlayerAudioSettings.SliderPosition(1,0)==0,"SliderPosition");

        var p=Panel();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        var page=p.Pages.Single(pg=>pg.Name=="Application");
        var sliders=page.Pieces.Where(x=>x.Kind==OptionPieceKind.Slider).ToArray();
        t.Equal("MusicVolumeScrollBar,EffectVolumeScrollBar,EnvVolumeScrollBar,VoiceVolumeScrollBar",string.Join(",",sliders.Select(s=>s.MouseEvent)),"four ScrollBars → sliders, keyed by Name");
        t.Check(sliders.All(s=>s.Travel==210&&s.ImageSize==new Size(9,12)&&s.DeclaredSize==new Size(219,12)),"travel = ScrollWidth 219 − thumb 9");
        t.Check(page.Pieces.Count(x=>x.Kind==OptionPieceKind.Image)==4&&!page.Pieces.Any(x=>x.Name=="Move_Button"),"background images kept, Move_Button only as the thumb");

        var host=new Host(new WorldSession());p.Attach(host);p.Open();p.ShowPage("Application");
        // Gain 0.5 → thumb at 105 (bar x 20 + 105): pixel inside the thumb is red, left end is the track.
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(60,105+4)),"music thumb drawn at the master gain (0.5)");
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(100,105+4)),"env thumb shows the same master gain");
        // Press at the left end → 0, drag to the right end → 1 (thumb centred on the pointer).
        var c=new UiNullCommands();
        p.Mouse(M(UiMouseKind.Down,BarAt(80,4)),c);
        t.Check(host.Gain==0,"press at the left end → gain 0 at once",host.Gain);
        p.Mouse(M(UiMouseKind.Move,new Point(BarAt(80,0).X+400,BarAt(80,0).Y+30)),c);
        t.Check(host.Gain==1,"drag past the right end (even outside the bar) → gain 1",host.Gain);
        p.Mouse(M(UiMouseKind.Up,BarAt(80,4+105)),c);
        t.Check(host.Gain==.5&&p.Unhandled.Count==0,"release in the middle → 0.5; nothing unhandled",host.Gain);
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(60,105+4)),"other bars follow (one master gain)");

        // Audio closed: SetLevel refused → value kept, logged.
        host.AudioClosed=true;host.Calls.Clear();
        Click(p,BarAt(60,4));
        t.Check(host.Gain==.5&&host.Calls.Count>0&&host.Calls.All(x=>x=="MusicVolumeScrollBar=0"),"refused (audio closed) → value kept",host.Calls.ToArray());
        t.Check(UiResourceLog.Recent().Any(l=>l.Contains("MusicVolumeScrollBar")&&l.Contains("未套用")),"refusal logged");

        // Voice bar: host does not hold it → thumb stays at its XML position, click Unhandled.
        host.AudioClosed=false;host.Calls.Clear();
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(120,4)),"voice thumb at the XML position (X 0)");
        Click(p,BarAt(120,150));
        t.Check(p.Unhandled.Contains("VoiceVolumeScrollBar")&&host.Calls.Count==0&&host.Gain==.5,"voice bar → Unhandled, gain untouched",string.Join(",",p.Unhandled));
        // No host → every bar unwired.
        var lone=Panel();lone.Open();lone.ShowPage("Application");Click(lone,BarAt(60,150));
        t.Check(lone.Level(OptionPanel.MusicVolumeBar)==null&&lone.Unhandled.Contains("MusicVolumeScrollBar"),"no host → not wired");
    }

    static WorldObject Obj(uint id,string name,ushort gfx=100)=>new(32600,32940,id,gfx,8,0,0,0,0,0,name,"",0,0,"","",0,0,0,10,0,0,0);
    static void Rendering(WorldModuleTestContext t){
        t.Check(Renderer.TransparentCorpse(Obj(5,"$936") with{Dead=true},1),"dead $nameid NPC → transparent");
        t.Check(!Renderer.TransparentCorpse(Obj(5,"$936"),1),"alive → normal");
        t.Check(!Renderer.TransparentCorpse(Obj(5,"玩家甲") with{Dead=true},1),"dead player (plain name) → normal");
        t.Check(!Renderer.TransparentCorpse(Obj(5,"$936") with{Dead=true},5),"own character → normal");
        t.Check(!Renderer.TransparentCorpse(Obj(5,"$1",2346) with{Dead=true},1),"door gfx 2346 → normal");
        t.Equal(16,Renderer.DeadMonsterAlpha,"corpse alpha 16/31 (fade scale, 推測)");
        // FadeMix at the corpse alpha: covered pixel → mix; corpse-only pixel → mixed with black (under mask off).
        var over=new Patch(new Rectangle(0,0,2,1),[0x7FFF,0x7FFF],[true,true],[0,0],[]);var under=new Patch(new Rectangle(0,0,2,1),[0x001F,0],[true,false],[0,0],[]);
        var mixed=Renderer.FadeMix(over,under,Renderer.DeadMonsterAlpha);
        t.Check(mixed.Mask.All(m=>m)&&mixed.Colors[0]!=0x7FFF&&mixed.Colors[0]!=0x001F,"corpse pixel blended with what is under it",mixed.Colors);
    }

    static void ProductTypes(WorldModuleTestContext t){
        var hostType=typeof(ClientForm).GetNestedType("OptionWindowHost",BindingFlags.NonPublic|BindingFlags.Public);
        t.Check(hostType!=null&&hostType.GetMethod("Level")!=null&&hostType.GetMethod("SetLevel")!=null,"ClientForm.OptionWindowHost implements the slider members");
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        t.Check(typeof(ClientForm).GetProperty("ShowDropItemNames",flags)!=null&&typeof(ClientForm).GetProperty("TransparentDeadMonsters",flags)!=null,"ClientForm display switches exist");
        t.Check(typeof(Renderer).GetProperty("TransparentDeadMonsters")!=null,"renderer switch exists");
    }
}
