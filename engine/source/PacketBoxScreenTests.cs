using System.Drawing.Imaging;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T246／#607（R2-O）S250/83、84 的離線測試：只用依 3.81 伺服器寫法合成的封包、合成時鐘與 rand()；不開遊戲、網路、視窗，也不讀素材。
static class PacketBoxScreenTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Box(byte sub,byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    // 3.81 S_PacketBoxGree(int type)：C(83) D(type) C0 C0 C0；S_GlobalEffect(trigger)：C(83) C(trigger)。
    static byte[] Gree(uint type)=>Box(83,[..Le32(type),0,0,0]);
    static byte[] Global(byte trigger)=>Box(83,[trigger]);
    // 3.81 S_PacketBoxGree(String)：C(84) C(2) S(msg)。
    static byte[] Green(string msg,byte type=2)=>Box(84,[type,..Str(msg)]);
    static JsonElement State(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("packetBoxScreen");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandledSubtypes").TryGetProperty(key,out var v)?v.GetInt32():0;

    [WorldModuleTest("packetbox-screen")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Rules(t);Messages(t);Effects(t);Fireworks(t);Panel(t);
    }

    // 1. 註冊：83／84 消化型處理器、重設、快照；面板自動發現。
    static void Registration(WorldModuleTestContext t){
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        t.Check(reg.Entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:83,Method:"WorldSession.ReceivePacketBoxGlobalEffect"}),"S250/83 registered");
        t.Check(reg.Entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:84,Method:"WorldSession.ReceivePacketBoxGreenMessage"}),"S250/84 registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPacketBoxScreen"),"reset registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="snapshot"&&e.Key=="packetBoxScreen"),"snapshot registered");
        t.Equal(1,UiPanelRegistry.Panels.Count(p=>p.Id==CenterMessagePanel.PanelId),"CENTER_MESSAGE registered once");
        t.Check(UiPanelRegistry.Errors.Count==0,"panel registry has no errors",UiPanelRegistry.Errors);
        const System.Reflection.BindingFlags F=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly;
        t.Check(typeof(ClientForm).GetMethod("OnInvalidated",F)!=null&&typeof(ClientForm).GetMethod("AttachPacketBoxScreen",F)!=null,"ClientForm wiring present");
    }

    // 2. 純規則：83 寬度、晃動表與時間軸、83/1 計數器、佇列版面。
    static void Rules(WorldModuleTestContext t){
        t.Equal(2u,PacketBoxScreenRules.ReadEffect(Gree(2)),"S_PacketBoxGree D width");
        t.Equal(3u,PacketBoxScreenRules.ReadEffect(Global(3)),"S_GlobalEffect C width (padded, read as D)");
        t.Equal(1u,PacketBoxScreenRules.ReadEffect([250,83,1]),"unpadded 1-byte value read as C");
        t.Equal(0x0102u,PacketBoxScreenRules.ReadEffect([250,83,2,1,0,0]),"4 bytes read little-endian D");
        t.Throws<ProtocolException>(()=>PacketBoxScreenRules.ReadEffect([250,83,1,0,0,0,9,0]),"nonzero tail rejected");
        t.Throws<ProtocolException>(()=>PacketBoxScreenRules.ReadEffect([250,83]),"missing value rejected");

        t.Equal(12,PacketBoxScreenRules.SwayTable.Length,"12-step table");
        t.Check(PacketBoxScreenRules.SwayTable.All(p=>Math.Abs(p.X)<=2&&Math.Abs(p.Y)<=2&&p.X!=0&&p.Y!=0),"table values within ±2, never 0");
        t.Equal(new Point(-1,-1),PacketBoxScreenRules.SwayTable[0],"step 0 (0xF04297)");
        t.Equal(new Point(1,2),PacketBoxScreenRules.SwayTable[5],"step 5 (0xF04314)");
        t.Equal(new Point(2,-2),PacketBoxScreenRules.SwayTable[10],"step 10 (0xF04385)");
        t.Equal(Point.Empty,PacketBoxScreenRules.SwayRaw(49,null),"nothing before the 50 ms start");
        t.Equal(PacketBoxScreenRules.SwayTable[0],PacketBoxScreenRules.SwayRaw(50,null),"tick 0 at 50 ms");
        t.Equal(PacketBoxScreenRules.SwayTable[0],PacketBoxScreenRules.SwayRaw(66,null),"tick 0 holds 17 ms");
        t.Equal(PacketBoxScreenRules.SwayTable[1],PacketBoxScreenRules.SwayRaw(67,null),"tick 1 at 67 ms");
        t.Equal(PacketBoxScreenRules.SwayTable[0],PacketBoxScreenRules.SwayRaw(50+17*12,null),"wraps at 12");
        t.Equal(PacketBoxScreenRules.SwayTable[0],PacketBoxScreenRules.SwayRaw(50+17*24,null),"tick 24 still writes the table");
        t.Equal(Point.Empty,PacketBoxScreenRules.SwayRaw(50+17*25,null),"tick 25 → 0 (stopped by itself)");
        t.Check(PacketBoxScreenRules.SwayEnded(50+17*25,null)&&!PacketBoxScreenRules.SwayEnded(50+17*24,null),"ends after 24 steps");
        t.Check(PacketBoxScreenRules.SwayFlag(50+17*24-1,null)&&!PacketBoxScreenRules.SwayFlag(50+17*24,null),"flag cleared on tick 24");
        t.Equal(PacketBoxScreenRules.SwayTable[3],PacketBoxScreenRules.SwayRaw(50+17*3+5,50+17*3+5),"off between ticks: last offset kept");
        t.Equal(Point.Empty,PacketBoxScreenRules.SwayRaw(50+17*4,50+17*3+5),"first tick after off → 0");
        t.Equal(new Point(1,1),PacketBoxScreenRules.ScreenOffset(new(-1,-1)),"world moves by −offset");

        t.Equal(15,PacketBoxScreenRules.FlashLevel(0),"flash 15 at receipt");
        t.Equal(15,PacketBoxScreenRules.FlashLevel(99),"flash 15 until 100 ms");
        t.Equal(14,PacketBoxScreenRules.FlashLevel(100),"first decrement at 100 ms");
        t.Equal(1,PacketBoxScreenRules.FlashLevel(100+30*13),"1 at 490 ms");
        t.Equal(0,PacketBoxScreenRules.FlashLevel(PacketBoxScreenRules.FlashEndMs),"0 at 520 ms");
        t.Equal(520L,PacketBoxScreenRules.FlashEndMs,"flash lasts 520 ms");

        t.Equal(121,PacketBoxScreenRules.LineY(0),"newest line y=121");
        t.Equal(106,PacketBoxScreenRules.LineY(1),"next older y=106");
        t.Equal(1,PacketBoxScreenRules.LineY(8),"ninth line y=1");
        t.Equal(9,PacketBoxScreenRules.MaxVisibleLines,"800×600 shows at most 9 lines");
        t.Equal(340,PacketBoxScreenRules.LineX(120),"x = 400 − width/2");
        t.Equal("甲乙",PacketBoxScreenRules.Resolve("$7",s=>s.Replace("$7","甲乙")),"names resolve $nnn");
        t.Equal("$7",PacketBoxScreenRules.Resolve("$7",null),"no name table → literal");
    }

    // 3. 84：類型 2／1 入佇列、7 秒、14 則上限、最新在前；3／4 與格式錯誤。
    static void Messages(WorldModuleTestContext t){
        long now=5_000_000;var s=new WorldSession();s.PacketBoxScreenClock=()=>now;
        var changed=new List<int>();s.PacketBoxScreenChanged+=changed.Add;
        s.Receive(Green("$ 5秒後將會被傳送!"),"");
        var m=s.CenterMessages.Single();
        t.Check(m is {Type:2,Raw:"$ 5秒後將會被傳送!",DurationMs:7000,ColorIndex:2,OutlineIndex:0,Flag:true}&&m.At==now,"type 2 queued (7000 ms, green, outline black)",m);
        t.Equal(84,changed.LastOrDefault(),"changed event 84");
        now+=7000;t.Equal(1,s.VisibleCenterMessages(now).Length,"still shown at at+7000");
        now+=1;t.Equal(0,s.VisibleCenterMessages(now).Length,"hidden after at+7000");
        s.Receive(Green("一",1),"");
        t.Check(s.CenterMessages[0] is {Type:1,Flag:false},"type 1 queued with flag false");
        for(int i=0;i<20;i++){now+=10;s.Receive(Green("m"+i),"");}
        var all=s.CenterMessages;
        t.Equal(14,all.Length,"at most 14 kept");
        t.Equal("m19",all[0].Raw,"newest first");
        t.Equal("m6",all[^1].Raw,"oldest dropped first");
        t.Check(all.Zip(all.Skip(1)).All(p=>p.First.Sequence>p.Second.Sequence),"sequence descending");
        // 3／4：不處理，記為未處理子碼；不進佇列。
        int before=s.CenterMessages.Length;
        s.Receive(Green("12",3),"");s.Receive(Green("12",4),"");
        t.Equal(before,s.CenterMessages.Length,"types 3/4 not queued");
        t.Equal(2,Unhandled(s,"0xFA/0x54"),"types 3/4 counted as unhandled");
        t.Throws<ProtocolException>(()=>s.Receive(Box(84,[2,..Str("x"),0,0,0,0,0,0,0,0,9]),""),"nonzero padding rejected");
        var st=State(s);
        t.Equal(14,st.GetProperty("centerMessages").GetArrayLength(),"snapshot lists queue");
        t.Equal(21,st.GetProperty("packets").GetProperty("84/2").GetInt32(),"snapshot counts type 2");
        s.Reset();
        t.Equal(0,s.CenterMessages.Length,"reset clears queue");
    }

    // 4. 83：1 計數器、2 晃動開關與自己停止、其他值只計數。
    static void Effects(WorldModuleTestContext t){
        long now=9_000_000;var s=new WorldSession();s.PacketBoxScreenClock=()=>now;
        t.Equal(Point.Empty,s.ScreenShakeOffset,"no shake initially");
        s.Receive(Gree(1),"");
        t.Equal(15,s.ScreenFlashLevel,"83/1 counter 15");
        t.Equal(Point.Empty,s.ScreenShakeOffset,"83/1 does not translate the world");
        now+=600;t.Equal(0,s.ScreenFlashLevel,"83/1 counter back to 0");
        var st=State(s);
        t.Check(st.GetProperty("flash").GetProperty("surf").GetInt32()==1473&&!st.GetProperty("flash").GetProperty("drawn").GetBoolean(),"surf 1473 recorded, not drawn");

        long on=now;s.Receive(Gree(2),"");
        t.Equal(Point.Empty,s.ScreenShakeOffset,"sway waits 50 ms");
        now=on+50;t.Equal(PacketBoxScreenRules.ScreenOffset(PacketBoxScreenRules.SwayTable[0]),s.ScreenShakeOffset,"sway step 0");
        now=on+50+17*4;t.Equal(PacketBoxScreenRules.ScreenOffset(PacketBoxScreenRules.SwayTable[4]),s.ScreenShakeOffset,"sway step 4 (2,2) → (−2,−2)");
        t.Check(State(s).GetProperty("sway").GetProperty("active").GetBoolean(),"sway active in snapshot");
        now=on+50+17*25;t.Equal(Point.Empty,s.ScreenShakeOffset,"sway stops by itself after 24 steps");
        t.Check(!State(s).GetProperty("sway").GetProperty("active").GetBoolean(),"sway inactive after stop");
        // 第二次 2 在晃動中＝關閉。
        on=now;s.Receive(Global(2),"");
        now=on+60;t.Check(!s.ScreenShakeOffset.IsEmpty,"S_GlobalEffect(2) restarts the sway");
        long off=now;s.Receive(Gree(2),"");
        t.Check(!s.ScreenShakeOffset.IsEmpty,"off: offset kept until the next tick");
        now=on+50+17*2;t.Equal(Point.Empty,s.ScreenShakeOffset,"off: 0 from the next tick");
        // 關掉後再 2＝重新開始。
        on=now;s.Receive(Gree(2),"");now=on+50;
        t.Equal(PacketBoxScreenRules.ScreenOffset(PacketBoxScreenRules.SwayTable[0]),s.ScreenShakeOffset,"toggled on again");
        _=off;
        // 其他值（例：3.81 WearingArrows 的 10）：沒有畫面效果，只計數。
        int effects=s.MagicEffects.Length;
        s.Receive(Global(10),"");
        st=State(s);
        t.Check(st.GetProperty("other").GetProperty("count").GetInt32()==1&&st.GetProperty("other").GetProperty("last").GetUInt32()==10,"other value counted");
        t.Equal(effects,s.MagicEffects.Length,"other value adds no effect");
        t.Equal(0,Unhandled(s,"0xFA/0x53"),"S250/83 never unhandled");
        t.Throws<ProtocolException>(()=>s.Receive(Box(83,[2,0,0,0,7]),""),"nonzero tail rejected");
        s.Reset();
        t.Equal(Point.Empty,s.ScreenShakeOffset,"reset stops sway");
        t.Equal(0,s.ScreenFlashLevel,"reset clears flash");
    }

    // 5. 83/3：自己＋可見物件最多 5 個、rand()%3 選 gfx、每個間隔 10 ms、放在物件座標。
    static void Fireworks(WorldModuleTestContext t){
        var s=AmbienceWorld(out var me);
        long now=2_000_000;s.PacketBoxScreenClock=()=>now;int r=0;s.PacketBoxScreenRandom=n=>r++;
        for(uint i=0;i<7;i++)s.Receive(AppearPacket(1000+i,(ushort)(32601+i)),"offline");
        int before=s.MagicEffects.Length;
        s.Receive(Gree(3),"");
        var fx=s.MagicEffects.Skip(before).ToArray();
        t.Equal(5,fx.Length,"at most 5 fireworks");
        t.Check(fx.Select(e=>e.Gfx).SequenceEqual([2042,2030,2029,2042,2030]),"rand()%3 picks 2042/2030/2029",fx.Select(e=>e.Gfx));
        t.Check(fx.Select(e=>e.At-now).SequenceEqual([0L,10,20,30,40]),"10 ms apart",fx.Select(e=>e.At-now));
        t.Check(fx[0].TargetX==me.X&&fx[0].TargetY==me.Y&&fx[0].Caster==0&&fx[0].Target==0,"first at own position, location effect",fx[0]);
        t.Check(fx.Skip(1).Select(e=>(int)e.TargetX).SequenceEqual([32601,32602,32603,32604]),"then visible objects by id");
        t.Equal(1,s.FireworkBursts,"bursts counted");   // 有自己角色的 session 做完整快照會讀素材（CI 沒有），所以直接讀計數
        var lonely=new WorldSession();lonely.Receive(Gree(3),"");
        t.Equal(0,lonely.MagicEffects.Length,"nothing visible → no firework");
    }

    // 6. 原生面板：版面（y、x 置中）、過期、最多 9 行、`$` 代換、色碼、World 層不吃輸入、繪製。
    static void Panel(WorldModuleTestContext t){
        var p=new CenterMessagePanel();
        t.Equal(UiLayer.World,p.Layer,"World layer (not hit-tested)");
        p.Update(new UiStaticGameState(InWorld:true,Now:0));t.Check(!p.Visible&&p.Bounds.IsEmpty,"no source → hidden");
        long now=100_000;var s=new WorldSession();s.PacketBoxScreenClock=()=>now;
        p.Source=new WorldCenterMessageSource(()=>s,raw=>raw.Replace("$7647","任務對白"));
        s.Receive(Green("$7647"),"");
        p.Update(new UiStaticGameState(InWorld:false,Now:now));t.Check(!p.Visible,"hidden outside the world");
        p.Update(new UiStaticGameState(InWorld:true,Now:now));
        t.Check(p.Visible&&p.Lines.Single() is {Index:0,Y:121,Raw:"任務對白"},"one line at y=121 with $ resolved",p.Lines);
        t.Equal(RedNoticePanel.Palette[2],p.Lines[0].Color,"green text");
        t.Equal(RedNoticePanel.Palette[0],p.Lines[0].Outline,"black outline");
        t.Equal(CenterMessagePanel.Band,p.Bounds,"bounds = band above y=136");
        t.Check(!p.Mouse(new UiMouseEvent(UiMouseKind.Down,new(400,121),MouseButtons.Left,1,0,Keys.None),new UiNullCommands()),"takes no mouse input");
        for(int i=0;i<12;i++){now+=1;s.Receive(Green("第"+i+"則"),"");}
        p.Update(new UiStaticGameState(InWorld:true,Now:now));
        t.Equal(9,p.Lines.Count,"at most 9 lines drawn");
        t.Check(p.Lines[0].Raw=="第11則"&&p.Lines.Select(l=>l.Y).SequenceEqual([121,106,91,76,61,46,31,16,1]),"newest at bottom, older upward",p.Lines.Select(l=>l.Y));
        now+=7001;p.Update(new UiStaticGameState(InWorld:true,Now:now));
        t.Check(!p.Visible,"all expired → hidden");

        // 繪製：字在 y 附近、x 依字寬置中；色碼 \f3 換紅。
        s.Reset();s.Receive(Green("ab"),"");
        p.Update(new UiStaticGameState(InWorld:true,Now:now));
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            int width=CenterMessagePanel.Width(canvas.Graphics,"ab"),x=PacketBoxScreenRules.LineX(width);
            bool green=false,outside=false;
            for(int yy=0;yy<CenterMessagePanel.Band.Bottom+4;yy++)for(int xx=0;xx<UiRoot.LogicalWidth;xx++){
                var c=canvas.Bitmap.GetPixel(xx,yy);
                if(c.G>200&&c.R<50&&c.B<50){green=true;if(yy<120||yy>121+14||xx<x-2||xx>x+width+2)outside=true;}
            }
            t.Check(green,"green text drawn");
            t.Check(!outside,"text stays on line y=121, centred on x=400",new{x,width});
        }
        s.Reset();s.Receive(Green("\\f3紅"),"");
        p.Update(new UiStaticGameState(InWorld:true,Now:now));
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            bool red=false;
            for(int yy=110;yy<140;yy++)for(int xx=300;xx<500;xx++){var c=canvas.Bitmap.GetPixel(xx,yy);if(c.R>200&&c.G<50)red=true;}
            t.Check(red,"\\f3 switches to red");
        }
        using(var b=new Bitmap(1,1,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))t.Equal(UiText.Measure(g,"紅").Width,CenterMessagePanel.Width(g,"\\f3紅"),"width ignores colour codes");
    }

    // 世界夾具（同 AmbienceWiringTests.World 的做法）：進入世界、自己在 (32600,32940)。
    const uint Me=123;
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] AppearPacket(uint id,ushort x)=>LoginPackets.Pad([87,..Le16(x),..Le16(32940),..Le32(id),..Le16((ushort)(id==Me?0:1110)),0,0,0,0,..Le32(0),..Le16(0),
        ..Str(id==Me?"offline":"target"+id),..Str(""),0,..Le32(0),..Str(""),..Str(""),0,100,0,0,0,255,255]);
    static WorldSession AmbienceWorld(out OwnActor me){
        const System.Reflection.BindingFlags F=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var w=new WorldSession();
        void Set(string name,object? value){
            for(var ty=w.GetType();ty!=null;ty=ty.BaseType){
                if(ty.GetProperty(name,F|System.Reflection.BindingFlags.DeclaredOnly) is {} p&&p.CanWrite){p.SetValue(w,value);return;}
                if(ty.GetField(name,F|System.Reflection.BindingFlags.DeclaredOnly) is {} f){f.SetValue(w,value);return;}
                if(ty.GetField($"<{name}>k__BackingField",F|System.Reflection.BindingFlags.DeclaredOnly) is {} b){b.SetValue(w,value);return;}
            }
            throw new MissingMemberException(w.GetType().Name,name);
        }
        Set("Entered",true);Set("MapId",(ushort)4);Set("CacheEnabled",false);
        Set("Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        w.Receive(AppearPacket(Me,32600),"offline");
        me=w.Actor!;
        return w;
    }
}
