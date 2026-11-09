using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R3-B（T278／#671）離線測試：PetControl 原生面板（合成 surf，座標＝原版建構子的寫死值）、開窗追蹤（真的 S39 anicom／moncom 進
// WorldSession）、原生 UI 關閉時的召喚暫行清單（合成 summonList.xml；選定後送出的 C125 與原生 SummonPanel 同一條路徑）。
// 不讀真實客戶端、不連線。
static class PetControlTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Pet=456,Stranger=790;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);
    static byte[] Appear(uint id,ushort x,string name,string owner="",byte hp=100)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(owner),[0,hp,0,0,0,255,255]);
    static byte[] SummonList(uint id)=>P([39],Le32(id),Str("summonlist"));
    /// <summary>S39 C(39) D(id) S(html) C(0) H(count) S×count：anicom 10 個、moncom 6 個參數（Pets.cs ReadInfo）。</summary>
    static byte[] Companion(uint id,string html){
        string[] args=html=="anicom"?["$1","70","100","5","10","12","夥伴","0","0","0"]:["$2","70","100","5","10","12"];
        return P([[39],Le32(id),Str(html),[0],Le16((ushort)args.Length),..args.Select(Str)]);
    }
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static LoginConnection Seed(List<byte[]> log){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":20,\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");c.World.Receive(Appear(Pet,32601,"夥伴","offline",70),"offline");
        c.World.Receive(Appear(Stranger,32603,"他人寵物","someone",70),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();
    static (uint,object?,bool)[] Rows(LoginConnection c)=>c.World.PetCompanions.Select(o=>{var i=c.World.PetInformation(o.ObjectId);return (o.ObjectId,(object?)i,i?.Kind=="summon");}).ToArray();

    sealed class Host:IPetControlHost {
        public PetControlView? Current{get;set;}
        public readonly List<string> Calls=[];
        public void Bag(PetControlView shown)=>Calls.Add($"bag:{shown.Revision}:{shown.PetId}");
        public void Target(PetControlView shown)=>Calls.Add($"target:{shown.Revision}:{shown.PetId}");
    }
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Red=0xFFFF0000,Green=0xFF00FF00,Blue=0xFF0000FF,White=0xFFFFFFFF;
    static Dictionary<int,UiSurface> Surfaces()=>new(){[1159]=Surface(1159,20,10,Red),[886]=Surface(886,20,10,Green),[1156]=Surface(1156,12,12,Blue),[885]=Surface(885,12,12,White)};
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(PetControlPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static int Pixel(UiCanvas canvas,Point at)=>canvas.Bitmap.GetPixel(at.X,at.Y).ToArgb();
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("pet-control")]
    static void Panel(WorldModuleTestContext t){
        // 1. 註冊（自動發現，一次）與原版寫死值。
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==PetControlPanel.PanelId)==1,"PET_CONTROL registered once");
        t.Check(UiPanelRegistry.Errors.Count==0,"panel registry valid",UiPanelRegistry.Errors);
        t.Equal("1159,886,1156,885",string.Join(",",PetControlPanel.BagSurf,PetControlPanel.BagPushSurf,PetControlPanel.TargetSurf,PetControlPanel.TargetPushSurf),"button surfs = sub_F3A0C0 constants");
        t.Equal(new Point(6,30),PetControlPanel.BagOffset,"button 0 at (6,30)");
        t.Equal(new Point(-31,20),PetControlPanel.TargetOffset,"button 1 at (-31,20)");

        // 2. 版面與可見性。
        var host=new Host();var p=new PetControlPanel();p.Load(id=>Surfaces().GetValueOrDefault(id));p.Attach(host);var cmd=new UiNullCommands();
        t.Check(p.Usable,"all four surfs present → usable");
        p.Update(InWorld);t.Check(!p.Visible,"hidden without an open");
        host.Current=new(1,Pet,false,true,new(400,300));
        p.Update(new UiStaticGameState());t.Check(!p.Visible,"hidden outside the world");
        p.Update(InWorld);t.Check(p.Visible,"visible for an open");
        t.Equal(new Rectangle(406,330,20,10),p.PartRect(PetControlPanel.BagKey),"bag button = anchor + (6,30), base surf size");
        t.Equal(new Rectangle(369,320,12,12),p.PartRect(PetControlPanel.TargetKey),"target button = anchor + (-31,20)");
        t.Equal(new Rectangle(369,320,57,20),p.Bounds,"bounds = union of both buttons");

        // 3. 繪製：平常畫第一個 surf，按住畫第二個。
        using(var canvas=new UiCanvas()){canvas.Clear(Color.Black);p.Paint(canvas);
            t.Equal(Argb(Red),Pixel(canvas,new(407,331)),"bag base 1159 drawn");
            t.Equal(Argb(Blue),Pixel(canvas,new(370,321)),"target base 1156 drawn");
            t.Equal(Color.Black.ToArgb(),Pixel(canvas,new(390,325)),"gap between buttons not drawn");}
        p.Mouse(M(UiMouseKind.Down,Center(p.PartRect(PetControlPanel.BagKey))),cmd);
        t.Equal(PetControlPanel.BagKey,p.Pressed,"press records the button");
        using(var canvas=new UiCanvas()){canvas.Clear(Color.Black);p.Paint(canvas);t.Equal(Argb(Green),Pixel(canvas,new(407,331)),"pressed bag shows 886");}
        p.Mouse(M(UiMouseKind.Up,Center(p.PartRect(PetControlPanel.TargetKey))),cmd);
        t.Equal(0,host.Calls.Count,"press and release on different buttons does nothing");

        // 4. 按鈕：背包、指定目標；按下後關窗，同一 revision 不再出現。
        Click(p,Center(p.PartRect(PetControlPanel.BagKey)),cmd);
        t.Equal("bag:1:456",host.Calls.LastOrDefault(),"button 0 → Bag for the shown pet");
        t.Check(!p.Visible,"hidden right after a click");
        p.Update(InWorld);t.Check(!p.Visible,"same revision stays dismissed");
        host.Current=host.Current with{Revision=2};p.Update(InWorld);t.Check(p.Visible,"next anicom shows again");
        Click(p,Center(p.PartRect(PetControlPanel.TargetKey)),cmd);
        t.Equal("target:2:456",host.Calls.LastOrDefault(),"button 1 → Target for a pet");
        // 召喚物（moncom）：第 1 鈕照畫但不可點；不能送時兩鈕都不可點。
        host.Current=host.Current with{Revision=3,Summon=true};p.Update(InWorld);
        t.Check(p.HitKey(Center(p.PartRect(PetControlPanel.TargetKey)))==null,"summon: target button disabled (kind 1)");
        Click(p,Center(p.PartRect(PetControlPanel.TargetKey)),cmd);
        t.Check(p.Visible&&host.Calls.Count==2,"disabled button neither sends nor closes");
        t.Check(p.Mouse(M(UiMouseKind.Down,Center(p.PartRect(PetControlPanel.TargetKey))),cmd),"click on the disabled button is still consumed");
        p.Mouse(M(UiMouseKind.Up,new(0,0)),cmd);
        host.Current=host.Current with{Revision=4,Summon=false,CanSend=false};p.Update(InWorld);
        Click(p,Center(p.PartRect(PetControlPanel.BagKey)),cmd);
        t.Check(host.Calls.Count==2&&p.Visible,"nothing fires while the host cannot send");
        t.Check(!p.Mouse(M(UiMouseKind.Down,new(10,10)),cmd),"click outside passes through to the world");
        t.Check(!p.Key(new UiKeyEvent(Keys.Escape,true,false),cmd),"Esc not handled (unverified)");

        // 5. 缺 surf：整個面板停用（不畫替代圖）。
        var noTarget=Surfaces();noTarget.Remove(1156);
        var broken=new PetControlPanel();broken.Load(id=>noTarget.GetValueOrDefault(id));broken.Attach(new Host{Current=new(1,Pet,false,true,new(400,300))});broken.Update(InWorld);
        t.Check(!broken.Usable&&!broken.Visible,"missing button base disables the panel");
    }

    [WorldModuleTest("pet-control-tracker")]
    static void Tracker(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;var tracker=new PetControlTracker();
        t.Check(tracker.Observe(true,Rows(c))==null,"first observation only primes");
        w.Receive(Companion(Pet,"anicom"),"offline");
        var open=tracker.Observe(true,Rows(c));
        t.Check(open is {Revision:1,PetId:Pet,Summon:false},"S39 anicom for an own pet opens PetControl",open);
        t.Equal(open,tracker.Observe(true,Rows(c)),"same PetInfo keeps the same open");
        w.Receive(Companion(Pet,"anicom"),"offline");
        t.Equal(2L,tracker.Observe(true,Rows(c))?.Revision,"another anicom reply = new revision");
        w.Receive(Companion(Pet,"moncom"),"offline");
        t.Check(tracker.Observe(true,Rows(c)) is {Revision:3,Summon:true},"moncom opens with kind summon (button 1 disabled)");
        w.Receive(Companion(Stranger,"anicom"),"offline");
        t.Equal(3L,tracker.Observe(true,Rows(c))?.Revision,"S39 for someone else's pet does not open");
        tracker.Dismiss(2);t.Check(tracker.Current!=null,"dismissing an old revision keeps the current open");
        tracker.Dismiss(3);t.Check(tracker.Observe(true,Rows(c))==null,"dismiss closes until the next reply");
        w.Receive(Companion(Pet,"anicom"),"offline");t.Equal(4L,tracker.Observe(true,Rows(c))?.Revision,"next reply after dismiss opens again");
        t.Check(tracker.Observe(false,Rows(c))==null&&tracker.Current==null,"leaving the world closes");
        t.Check(tracker.Observe(true,Rows(c))==null,"re-entering primes again (old info is not a new reply)");
        // 按鈕送出的封包＝既有寵物指令：attackchr（C125）與 refresh（C103）。
        var view=c.PetSnapshot(Pet);
        t.Check(view.CanSend,"own nearby pet can be commanded",view);
        t.Check(Run(c.SendPet(view.Stamp,PetPackets.TargetCommand)),"target command admitted");
        t.Equal(Hex(PetPackets.Command(Pet,PetPackets.TargetCommand)),Hex(log.LastOrDefault()),"button 1 = C125 D(pet) S(\"attackchr\") (TW16 op 227 cdss)");
        view=c.PetSnapshot(Pet);
        t.Check(Run(c.SendPet(view.Stamp,"refresh")),"bag request admitted");
        t.Equal(Hex(PetPackets.Menu(Pet)),Hex(log.LastOrDefault()),"button 0 = C103 D(pet) (3.81 replacement for TW16 op 160 → C99 no reply)");
    }

    static string SummonXml()=>"<?xml version=\"1.0\"?><summonlist>"+
        "<summon level=\"10\"><monster id=\"1\" level=\"10\" action=\"7\" desc=\"1\" surf=\"100\" minCha=\"8\"/><monster id=\"2\" level=\"10\" action=\"263\" desc=\"2\" surf=\"101\" minCha=\"8\"/></summon>"+
        "<summon level=\"20\"><monster id=\"3\" level=\"20\" action=\"519\" desc=\"3\" surf=\"102\" minCha=\"12\"/></summon>"+
        "<summon level=\"28\"><monster id=\"4\" level=\"28\" action=\"775\" desc=\"4\" surf=\"103\" minCha=\"16\"/></summon></summonlist>";

    [WorldModuleTest("summon-fallback")]
    static void Fallback(WorldModuleTestContext t){
        var catalog=SummonCatalog.Parse(UiLayout.Parse(SummonXml(),"summonList.xml"));
        var groups=SummonFallback.Groups(catalog,20);
        t.Equal("28,20,10",string.Join(",",groups.Select(g=>g.Level)),"levels listed high → low (same order as SummonUI level buttons)");
        t.Equal("7,263",string.Join(",",groups[2].Monsters.Select(e=>e.Monster.Action)),"XML order kept inside a level");
        t.Check(!groups[0].Monsters[0].Enabled&&groups[1].Monsters[0].Enabled&&groups[2].Monsters.All(e=>e.Enabled),"character level 20: level ≤ 20 enabled, 28 not");
        t.Check(SummonFallback.Groups(catalog,null).SelectMany(g=>g.Monsters).All(e=>!e.Enabled),"no S8 level → nothing selectable (not estimated)");
        t.Equal("召喚編號 263 · 需等級 10 · minCha 8",SummonFallback.Caption(catalog[1]),"caption = action, level, minCha");

        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        var closed=c.SummonSnapshot();
        t.Check(!SummonFallback.CanChoose(closed,catalog,catalog[0]),"no request → cannot choose");
        t.Check(!SummonFallback.ShouldReveal(closed,false,-1),"no request → pet tab not revealed");
        w.Receive(SummonList(Me),"offline");var v=c.SummonSnapshot();
        t.Check(v.Open&&v.Source=="server"&&v.CanSend,"S39 summonlist opens a server request",v);
        t.Check(SummonFallback.ShouldReveal(v,false,-1),"native UI off: new summonlist reveals the pet tab");
        t.Check(!SummonFallback.ShouldReveal(v,false,v.Revision),"same revision revealed once");
        t.Check(!SummonFallback.ShouldReveal(v,true,-1),"native UI on: SummonPanel handles it");
        t.Check(SummonFallback.CanChoose(v,catalog,catalog[2]),"level 20 monster selectable at level 20");
        t.Check(!SummonFallback.CanChoose(v,catalog,catalog[3]),"level 28 monster refused at level 20");
        t.Check(!SummonFallback.CanChoose(v,catalog,catalog[0] with{Action="8"}),"monster not in the loaded catalog refused");
        t.Check(!SummonFallback.CanChoose(v with{CanSend=false},catalog,catalog[0]),"host cannot send → refused");
        // 送出：與原生 SummonPanel 同一個 SendSummonChoice → C125 D(自己) S(action)，一次。
        t.Check(Run(c.SendSummonChoice(v,catalog[1].Action)),"fallback choice admitted");
        t.Equal(Hex(PetPackets.SummonChoice(Me,"263")),Hex(log.LastOrDefault()),"fallback sends the same C125 D(self) S(\"263\")");
        var after=c.SummonSnapshot();
        t.Check(!after.Open&&!SummonFallback.CanChoose(after,catalog,catalog[1])&&!Run(c.SendSummonChoice(v,"263")),"request closed; the old list cannot resend");
        t.Equal(1,log.Count,"exactly one packet");
    }
}
