using P06;
using System.Text.Json;

partial class Checks {
    static LoginConnection TitleEmoteSeed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Title\":\"\",\"X\":32600,\"Y\":32940,\"Direction\":5}"));
        Set(c.World,"ActionClock",(Func<long>)(()=>1000));Feed(c,Pack(456));return c;
    }
    static byte[] TitleUpdate(uint id,string title)=>Packet(b=>{b.Write((byte)183);b.Write(id);Text(b,title);});
    static byte[] EmoteUpdate(uint id,byte action)=>Packet(b=>{b.Write((byte)158);b.Write(id);b.Write(action);});
    static void TitleEmoteReject(Action action,string label){try{action();}catch(Exception ex) when(ex is ArgumentException or ProtocolException or InvalidDataException){Check(true,label);return;}throw new Exception("accepted malformed input: "+label);}
    static byte[] EmoteSpriteFixture()=>PacketRaw(b=>{
        b.Write((byte)1);for(int i=0;i<4;i++)b.Write((short)0);b.Write((ushort)0);b.Write((ushort)0);b.Write((short)1);
        b.Write((byte)0);b.Write((byte)0);b.Write((byte)0);b.Write((ushort)0);
        b.Write(1u);b.Write(0u);b.Write(9u);
        b.Write(new byte[]{0,0,1,1,1,0,1});b.Write((ushort)0x7c00);
    });
    static byte[] PacketRaw(Action<BinaryWriter> write){using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);write(writer);return stream.ToArray();}
    static async Task TitleAndEmoteChecks(){
        Check(Convert.ToHexString(TitleAndEmotePackets.Title("中","文"))=="5AA4A400A4E50000","C90 independent CP950 SS vector");
        foreach(int action in Enumerable.Range(66,4))Check(TitleAndEmotePackets.Emote(action).SequenceEqual(new byte[]{120,(byte)action,0,0,0,0,0,0}),"C120 C vector "+action);
        foreach(int action in new[]{-1,0,65,70,256,322})TitleEmoteReject(()=>TitleAndEmotePackets.Emote(action),"C120 allowlist "+action);
        foreach(var pair in new[]{("","title"),("name",""),("a\0b","title"),("name","a\0b"),("name","😀")})TitleEmoteReject(()=>TitleAndEmotePackets.Title(pair.Item1,pair.Item2),"C90 invalid strings");
        Check(TitleAndEmotePackets.Title("a",new string('b',10228)).Length==10232,"C90 maximum padded transport length");
        TitleEmoteReject(()=>TitleAndEmotePackets.Title("a",new string('b',10229)),"C90 oversized packet");
        Check(TitleAndEmotePackets.Title("a",new string('中',40)).Length>40,"no invented 16-character server limit");
        Check(TitleAndEmotePackets.DisplayText("\f2封號\r\n") == "封號","display removes legacy color/control codes only");
        Check(TitleAndEmotePackets.DisplayText(new string('中',100)).Length==81,"display text bounded without changing wire title");
        using(var c=TitleEmoteSeed()){
            Feed(c,Convert.FromHexString("B77B000000A4E500"));Check(c.World.Actor!.Title=="文","S183 own independent DS vector");
            Feed(c,TitleUpdate(456,"遠方"));Check(c.World.Objects.Single().Title=="遠方","S183 remote title updated");
            Feed(c,TitleUpdate(456,""));Check(c.World.Objects.Single().Title=="","S183 empty title clears");
            Feed(c,TitleUpdate(999,"忽略"));Check(c.World.Objects.Length==1&&c.World.UnknownObjectUpdates==1,"unknown title does not spawn actor");
            byte[] packet=TitleUpdate(123,"中文稱號");
            for(int n=0;n<packet.Length;n++){var before=c.World.Actor;TitleEmoteReject(()=>Feed(c,packet[..n]),"truncated S183 "+n);Check(ReferenceEquals(before,c.World.Actor),"S183 truncation atomic "+n);}
            var bad=(byte[])packet.Clone();bad[^1]=1;TitleEmoteReject(()=>Feed(c,bad),"S183 nonzero padding");
            Feed(c,EmoteUpdate(456,68));Check(c.World.ActiveEmotes(1000).Single().Action==68,"S158 remote emote begins");
            for(byte action=66;action<=69;action++){Feed(c,EmoteUpdate(456,action));Check(c.World.ActiveEmotes(1000).Single().Action==action,"S158 all actions replace previous "+action);}
            var emote=EmoteUpdate(456,68);for(int n=0;n<emote.Length;n++)TitleEmoteReject(()=>Feed(c,emote[..n]),"truncated S158 "+n);
            bad=(byte[])emote.Clone();bad[^1]=1;TitleEmoteReject(()=>Feed(c,bad),"S158 nonzero padding");
            Feed(c,EmoteUpdate(999,68));Check(c.World.ActiveEmotes(1000).Length==1,"unknown emote ignored");
            Feed(c,Move(456,32600,32940,2));Check(c.World.ActiveEmotes(1000).Length==0,"movement ends emote");
            Feed(c,EmoteUpdate(456,66));Feed(c,Pack(456));Check(c.World.ActiveEmotes(1000).Length==0,"full S87 replaces incarnation");
            Feed(c,EmoteUpdate(456,67));Feed(c,LoginPackets.Pad([120,200,1,0,0]));Feed(c,Pack(456));Check(c.World.ActiveEmotes(1000).Length==0,"remove/readd ABA cannot replay emote");
            Feed(c,EmoteUpdate(456,68));Check(c.World.ActiveEmotes(11000).Length==0,"bounded emote lifetime");
            Feed(c,Packet(b=>{b.Write((byte)158);b.Write(456u);b.Write((byte)70);Text(b,"商店");}));Check(c.World.Objects.Single().ShopActive,"S158 shop dispatch preserved");
            Feed(c,EmoteUpdate(456,68));Check(c.World.ActiveEmotes(1000).Length==0,"shop actor does not emote");
            Feed(c,Pack(456,32600,32940,8));Feed(c,EmoteUpdate(456,68));Check(c.World.ActiveEmotes(1000).Length==0,"dead actor does not emote");
            Feed(c,EmoteUpdate(123,68));Check(c.World.ActiveEmotes(1000).Length==1,"own S158 emote accepted");
            Feed(c,LoginPackets.Pad([206,0,0,0]));Check(c.World.ActiveEmotes(1000).Length==0,"map epoch clears emotes");
        }
        using(var c=TitleEmoteSeed()){
            // S158 is shared: emote actions 66-69 stop at the emote branch, action 71 still reaches fishing.
            Feed(c,EmoteUpdate(123,68));Check(c.World.ActiveEmotes(1000).Single().Action==68&&!c.ReadFishing().Active,"S158 emote does not start fishing");
            Feed(c,FishingStart());Check(c.ReadFishing().Active&&c.ReadFishing().Notice.Contains("32602, 32940"),"S158 fishing start reaches observer beside emote branch");
            // Doors own every action on their objects, so a door ID never becomes an emote actor.
            Feed(c,DoorPack());Feed(c,EmoteUpdate(700,68));var door=c.World.Objects.Single(o=>o.ObjectId==700);
            Check(door.ReceivedAction==68&&door.DoorAction==null&&c.World.ActiveEmotes(1000).All(e=>e.Id!=700),"S158 door object keeps door dispatch before emote");
        }
        using(var c=TitleEmoteSeed()){
            int sends=0;byte[]? sent=null;Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sends++;sent=bytes;admit();return Task.FromResult(true);});
            var stamp=c.CaptureTitleEmote();Check(await c.SendTitle(stamp,"offline","稱號")&&sends==1&&sent![0]==90,"C90 memory sender");Check(c.World.Actor!.Title=="","title never predicted before S183");
            for(int item=1;item<=4;item++){int action=new[]{68,69,67,66}[item-1];
                Check(await c.SendEmote(stamp,item)&&sent![0]==120&&sent[1]==action,"C120 memory sender original order item "+item);Check(c.World.ActiveEmotes(1000).Single().Action==action,"class form plays admitted emote locally "+action);
                int busy=sends;Check(!await c.SendEmote(stamp,item)&&sends==busy,"playing own emote is a busy action "+item);EndEmote(c);}
            int before=sends;Check(!await c.SendEmote(stamp,0)&&!await c.SendEmote(stamp,5)&&!await c.SendTitle(stamp,"offline","😀")&&sends==before,"bad request sends nothing");
            Set(c.World,"OwnDead",true);Check(!await c.SendEmote(stamp,1)&&sends==before,"own death blocks send");Set(c.World,"OwnDead",false);
            Feed(c,LoginPackets.Pad([206,0,0,0]));Check(!await c.SendTitle(stamp,"offline","stale")&&sends==before,"old map stamp cannot send");
        }
        using(var c=TitleEmoteSeed()){
            var release=new TaskCompletionSource();int sends=0;Sender(c,async(bytes,current,admit)=>{await release.Task;if(!current())return false;sends++;admit();return true;});
            var stamp=c.CaptureTitleEmote();var pending=c.SendEmote(stamp,1);Check(!await c.SendEmote(stamp,2),"duplicate pending request blocked");
            c.World.Reset();release.SetResult();Check(!await pending&&sends==0&&c.World.ActiveEmotes(1000).Length==0,"delayed send revalidates reset before admission");
        }
        using(var c=TitleEmoteSeed()){
            Sender(c,(_,_,_)=>throw new IOException("synthetic transport failure"));Check(!await c.SendEmote(c.CaptureTitleEmote(),1)&&c.State==LoginState.Faulted,"send exception fails closed without retry");
        }
        EmotePlanChecks();await EmoteDeliveryChecks();
        var steps=EmoteSprites.ParseSteps("#0 1\n 110.framerate(40)\n 68.wave(1 2,56.0:4[123 56.1:6)",68,7);
        Check(steps[0]==new EmoteSprites.Step(63,0,100)&&steps[1]==new EmoteSprites.Step(63,1,150),"original direction offset/frame ticks/FPS");
        Check(EmoteSprites.ParseSteps("66.shop loop((1 1,368.0:4)",66,0).Single().File==368,"gfx2796 original doubled parenthesis");
        TitleEmoteReject(()=>EmoteSprites.ParseSteps("68.wave(1 2,56.0:4)",68,0),"metadata frame count mismatch");
        TitleEmoteReject(()=>EmoteSprites.ParseSteps("68.wave(1 1,56.0:0)",68,0),"metadata zero duration");
        // T160 3.4 fallback inputs: absent action is "missing", not malformed; pose/0 rows parse like the original table.
        const string horse="#6080 112 princess horse\n 3.breath(1 1,0.0:4)\n 0.walkleft(1 2,8.0:2 8.1:2<4353)\n 20.walkright(1 1,16.0:2)";
        Check(EmoteSprites.TryParseSteps(horse,66,3)==null&&EmoteSprites.TryParseSteps(horse,2,0)==null,"missing action row reports null without exception");
        Check(EmoteSprites.TryParseSteps(horse,0,3)!.Select(s=>s.File).SequenceEqual(new[]{11,11})&&EmoteSprites.TryParseSteps(horse,20,1)!.Single().File==17,"walk/pose rows apply direction offset");
        Check(EmoteSprites.TryParseSteps("#1120 1 X_mark 9.(0 1,0.0:4)\n 0.(0 1,5.0:4)",0,6)!.Single().File==5,"single-direction (0 n) row ignores direction");
        TitleEmoteReject(()=>EmoteSprites.TryParseSteps(horse+"\n 66.bad(1 2,1.0:4)",66,0),"present but malformed action still fails closed");
        TitleEmoteReject(()=>EmoteSprites.TryParseSteps(horse,121,0),"action table bound 0..120");
        string?[] art=new string?[121];art[0]="a0";art[20]="a20";var asked=new List<int>();string? Exact(int a){asked.Add(a);return art[a];}
        Check(EmoteSprites.Fallback(66,20,Exact)=="a20"&&asked.SequenceEqual(new[]{66,20}),"missing emote falls back to pose action");
        asked.Clear();Check(EmoteSprites.Fallback(66,4,Exact)=="a0"&&asked.SequenceEqual(new[]{66,4,0}),"missing pose art falls back to action 0");
        asked.Clear();Check(EmoteSprites.Fallback(66,0,Exact)=="a0"&&asked.SequenceEqual(new[]{66,0}),"pose 0 falls back once to action 0");
        art[0]=null;asked.Clear();Check(EmoteSprites.Fallback(66,4,Exact)==null,"no art at all yields null instead of exception");
        art[68]="a68";Check(EmoteSprites.Fallback(68,4,Exact)=="a68","present emote art is used directly");
        art[119]=null;asked.Clear();Check(EmoteSprites.Fallback(119,20,Exact)==null&&asked.SequenceEqual(new[]{119}),"actions 119/120 are excluded from fallback");
        var bytes=EmoteSpriteFixture();var frame=EmoteSprites.Decode(bytes,56).Single();Check(frame.Key=="56:0"&&frame.Blocks.Single().Tile.Pixels.Single()==new Pixel(0,0,0x7c00),"synthetic original SPR pixel decoder");
        for(int n=0;n<bytes.Length;n++)TitleEmoteReject(()=>EmoteSprites.Decode(bytes[..n],56),"truncated SPR "+n);
        var sequence=new EmoteSprites.EmoteSequence(0,steps,[frame,frame with{Key="second"}]);Check(sequence.At(0)?.Key=="56:0"&&sequence.At(100)?.Key=="second"&&sequence.At(250)==null,"animation advances once and terminates");
        TitleEmoteLabelRendering();
    }
    static void EndEmote(LoginConnection c)=>typeof(WorldSession).GetMethod("EndEmote",F)!.Invoke(c.World,[123u]);
    static string PlanText(int gfx,bool menu)=>string.Join(" ",Enumerable.Range(1,4).Select(i=>{var p=TitleAndEmotePackets.Plan(gfx,i,menu);return p.Delivery switch{EmoteDelivery.Immediate=>"I",EmoteDelivery.Throttled=>"T",_=>"-"}+(p.Delivery==EmoteDelivery.None?"":p.Action.ToString())+(p.Hold?"h":"");}));
    static void EmotePlanChecks(){
        Check(TitleAndEmotePackets.MenuActions.SequenceEqual(new[]{68,69,67,66})&&TitleAndEmotePackets.MenuActions.Select(TitleAndEmotePackets.EmoteName).SequenceEqual(new[]{"敬禮","歡呼","挑釁","思考"}),"original item order salute/cheer/aggress/think");
        // T160 1.2 action-slot table (sub_ADACB0..) and menu table (sub_8C14F0), one row per group.
        var slot=new (int Gfx,string Plan)[]{(0,"I68 I69 I67 I66"),(12494,"I68 I69 I67 I66"),(6822,"I68 I69 I67 T66"),(12542,"I68 I69 I67 T66"),(6080,"I67 I68 I69 T66"),(6094,"I67 I68 I69 T66"),
            (6743,"I67 I68 I69 T66"),(8719,"I66 I67 I68 I69"),(10413,"I68 - - T66"),(3750,"I68 T69 T67 T66"),(11635,"T68 T69 T67 T66"),(3225,"T68 T69 T67 T66")};
        foreach(var row in slot)Check(PlanText(row.Gfx,false)==row.Plan,$"slot remap gfx {row.Gfx} = {row.Plan} (got {PlanText(row.Gfx,false)})");
        var menu=new (int Gfx,string Plan)[]{(0,"I68 I69 I67 I66"),(7139,"I68 I69 I67 I66"),(6080,"I68h I69h I67h T66"),(6094,"I68h I69h I67h T66"),(6753,"I67h I68h I69h T66"),
            (14695,"I68h I69h I67h I66h"),(10413,"I68h - - T66"),(11650,"I68h I69h T67 T66"),(3858,"I68 T69 T67 T66"),(3225,"T68 T69 T67 T66")};
        foreach(var row in menu)Check(PlanText(row.Gfx,true)==row.Plan,$"menu remap gfx {row.Gfx} = {row.Plan} (got {PlanText(row.Gfx,true)})");
        foreach(int gfx in new[]{0,1,37,48,61,138,734,1186,2786,2796,6650,6658,6661,6671,12490,12494})Check(TitleAndEmotePackets.ClassForm(gfx),"G1 class form "+gfx);
        Check(!TitleAndEmotePackets.ClassForm(2)&&!TitleAndEmotePackets.SpecialForm(6821)&&!TitleAndEmotePackets.SpecialForm(6892)&&TitleAndEmotePackets.SpecialForm(11421)&&!TitleAndEmotePackets.SpecialForm(11422),"group boundaries");
        TitleEmoteReject(()=>TitleAndEmotePackets.Plan(0,0,true),"plan item lower bound");TitleEmoteReject(()=>TitleAndEmotePackets.Plan(0,5,false),"plan item upper bound");
        Check(TitleAndEmotePackets.ThrottleMs()==2500&&TitleAndEmotePackets.ThrottleMs(true)==2265,"throttle 2500 ms, flag547 2500*29>>5");
        Check(TitleAndEmotePackets.ThrottleMs(speedA:1)==1875&&TitleAndEmotePackets.ThrottleMs(speedA:2)==5000&&TitleAndEmotePackets.ThrottleMs(speedA:3)==2187,"throttle speed state A factors");
        Check(TitleAndEmotePackets.ThrottleMs(speedB:1)==1875&&TitleAndEmotePackets.ThrottleMs(speedB:3)==2187&&TitleAndEmotePackets.ThrottleMs(speedB:6)==1875&&TitleAndEmotePackets.ThrottleMs(speedB:5)==937&&TitleAndEmotePackets.ThrottleMs(speedB:2)==2500,"throttle speed state B factors");
    }
    static async Task EmoteDeliveryChecks(){
        using(var c=TitleEmoteSeed()){
            long now=1000;Set(c.World,"ActionClock",(Func<long>)(()=>now));int sends=0;byte[]? sent=null;
            Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sends++;sent=bytes;admit();return Task.FromResult(true);});
            Set(c.World,"Actor",c.World.Actor! with{Gfx=3225});var stamp=c.CaptureTitleEmote();
            Check(await c.SendEmote(stamp,1)&&sent![0]==120&&sent[1]==68&&c.World.ActiveEmotes(now).Length==0,"polymorph sends throttled C120 without local playback");
            Check(!await c.SendEmote(stamp,2)&&sends==1,"throttle refuses within interval");
            now=3499;Check(!await c.SendEmote(stamp,2)&&sends==1,"throttle refuses at 2499 ms");
            now=3500;Check(await c.SendEmote(stamp,2)&&sent![1]==69&&sends==2,"throttle admits at 2500 ms");
            Feed(c,EmoteUpdate(123,69));Check(c.World.ActiveEmotes(now).Single().Action==69,"server S158 for self still plays if it arrives");EndEmote(c);
            now=10000;Set(c.World,"Actor",c.World.Actor! with{Gfx=6080});
            Check(await c.SendEmote(stamp,1)&&sent![1]==68&&c.World.ActiveEmotes(now).Single().Action==68,"horse helm menu item 1 immediate 68 with local playback");EndEmote(c);
            Check(await c.SendEmote(stamp,1,menu:false)&&sent![1]==67,"horse helm slot 1 remaps to 67");EndEmote(c);
            Check(await c.SendEmote(stamp,4)&&sent![1]==66&&c.World.ActiveEmotes(now).Length==0,"horse helm item 4 throttled 66");
            Set(c.World,"Actor",c.World.Actor! with{Gfx=10413});int before=sends;now=20000;
            Check(!await c.SendEmote(stamp,2)&&!await c.SendEmote(stamp,3)&&sends==before,"gfx 10413 items 2/3 send nothing");
        }
        using(var c=TitleEmoteSeed()){
            int sends=0;Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);});var stamp=c.CaptureTitleEmote();
            // Original only checks the busy action: movement, shop pose and the old Flags&2 gate no longer block.
            Set(c,"movementSending",true);Check(c.MovementBusy&&await c.SendEmote(stamp,1)&&sends==1,"movement in progress does not block emote");Set(c,"movementSending",false);EndEmote(c);
            Set(c.World,"Actor",c.World.Actor! with{Weapon=70,Flags=2});stamp=c.CaptureTitleEmote();Check(await c.SendEmote(stamp,2)&&sends==2,"shop pose / flags no longer block emote");EndEmote(c);
            Set(c,"nextCombatAt",long.MaxValue);Check(!await c.SendEmote(stamp,1)&&sends==2,"attack in progress blocks emote");Set(c,"nextCombatAt",0L);
            Set(c,"nextMagicAt",long.MaxValue);Check(!await c.SendEmote(stamp,1)&&sends==2,"spell in progress blocks emote");Set(c,"nextMagicAt",0L);
            Check(await c.SendEmote(stamp,3)&&sends==3,"busy release admits emote");
        }
        using(var c=TitleEmoteSeed()){
            var release=new TaskCompletionSource();int sends=0;Sender(c,async(bytes,current,admit)=>{await release.Task;if(!current())return false;sends++;admit();return true;});
            var pending=c.SendEmote(c.CaptureTitleEmote(),1);Set(c.World,"Actor",c.World.Actor! with{Gfx=3225});release.SetResult();
            Check(!await pending&&sends==0&&c.World.ActiveEmotes(1000).Length==0,"appearance change before admission refuses stale remap");
        }
    }
    static void Backing(object instance,string property,object value)=>instance.GetType().GetField("<"+property+">k__BackingField",F)!.SetValue(instance,value);
    static void TitleEmoteLabelRendering(){
        // Minimal synthetic scene exercises the product compositor, without loading any game resource.
        var assets=(Assets)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Assets));
        Backing(assets,"CanvasWidth",4096);Backing(assets,"CanvasHeight",4096);Backing(assets,"Scene",new List<Draw>());
        var renderer=(Renderer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Renderer));Set(renderer,"assets",assets);
        var own=(World)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(World));Backing(own,"X",32600);Backing(own,"Y",32940);
        using var c=TitleEmoteSeed();Feed(c,TitleUpdate(123,"測試稱號"));
        var draw=new Draw(48,44,0,1000000,"actor",0,-1,-1,new Tile(2,[new Pixel(0,0,0x7c00)]));
        var bounds=new System.Drawing.Rectangle(draw.Px,draw.Py,24,24);var original=new Patch(bounds,new ushort[576],new bool[576],new int[576],[draw],new uint?[576]);
        var catalog=(ObjectAssets)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ObjectAssets));
        var result=(Patch)typeof(Renderer).GetMethod("ComposeTitlesAndEmotes",F)!.Invoke(renderer,[own,c.World,catalog,original,new List<ObjectVisual>(),1000L])!;
        Check(result.Bounds.Top<original.Bounds.Top,"title above actor expands patch");
        int colored=0;for(int y=0;y<result.Bounds.Height;y++)for(int x=0;x<result.Bounds.Width;x++){
            int i=y*result.Bounds.Width+x;if(result.Mask[i]&&result.Bounds.Y+y<draw.Py){colored++;Check(result.EntityOwners![i]==null,"title pixels cannot become selectable actor");}
        }
        Check(colored>20,"product renders title and name glyphs above actor");
        int actor=(draw.Py-result.Bounds.Y)*result.Bounds.Width+draw.Px-result.Bounds.X;Check(result.Colors[actor]==0x7c00,"title overlay preserves actor pixel");
        // The compositor re-blits every draw, so a title must not restore an invisible actor to full opacity.
        Feed(c,InvisPacket(123,1));var hidden=(Patch)typeof(Renderer).GetMethod("ComposeTitlesAndEmotes",F)!.Invoke(renderer,[own,c.World,catalog,original,new List<ObjectVisual>(),1000L])!;
        Check(hidden.Colors[(draw.Py-hidden.Bounds.Y)*hidden.Bounds.Width+draw.Px-hidden.Bounds.X]==0x3c00,"title overlay keeps invisible actor half blend");
        Feed(c,InvisPacket(123,0));Feed(c,Poison(123,1,0));Set(c.World,"MagicStatusClock",(Func<long>)(()=>0L));var poisoned=(Patch)typeof(Renderer).GetMethod("ComposeTitlesAndEmotes",F)!.Invoke(renderer,[own,c.World,catalog,original,new List<ObjectVisual>(),1000L])!;
        Check(poisoned.Colors[(draw.Py-poisoned.Bounds.Y)*poisoned.Bounds.Width+draw.Px-poisoned.Bounds.X]==AfflictionColors.Tint(0x7c00,AfflictionTint.Poison,0L),"title recomposition keeps poison tint");
        TitleEmoteBlindLabels();
    }
    static void TitleEmoteBlindLabels(){
        // Mirrors the ObjectRendering hook: the affliction patch feeds the title compositor.
        var assets=(Assets)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Assets));
        Backing(assets,"CanvasWidth",2000);Backing(assets,"CanvasHeight",1100);Backing(assets,"Scene",new List<Draw>());
        var renderer=(Renderer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Renderer));Set(renderer,"assets",assets);
        var own=(World)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(World));Backing(own,"X",32600);Backing(own,"Y",32940);
        var catalog=(ObjectAssets)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ObjectAssets));
        using var c=TitleEmoteSeed();Feed(c,TitleUpdate(123,"自身"));Feed(c,TitleUpdate(456,"遠方"));
        var self=new Draw(48,44,0,1000000,"actor",0,-1,-1,new Tile(2,[new Pixel(0,0,0x7c00)]));var peer=self with{X=60,ObjectId=456};
        var foot=new System.Drawing.PointF(peer.Px,peer.Py);var visual=new ObjectVisual(456,0,0,"peer",32612,32940,"breath","8:0",foot,System.Drawing.Rectangle.Empty,false,0);
        var area=new System.Drawing.Rectangle(1400,700,500,350);var compose=typeof(Renderer).GetMethod("ComposeAfflictionPatch",F)!;var titles=typeof(Renderer).GetMethod("ComposeTitlesAndEmotes",F)!;
        Patch Compose(){var patch=(Patch)compose.Invoke(renderer,[c.World,area,new List<Draw>{self,peer},null])!;return (Patch)titles.Invoke(renderer,[own,c.World,catalog,patch,new List<ObjectVisual>{visual},1000L])!;}
        ushort[] Glyphs(Patch p,System.Drawing.PointF at)=>Enumerable.Range((int)at.Y-115,45).SelectMany(y=>Enumerable.Range((int)at.X-60,120).Select(x=>p.Colors[(y-p.Bounds.Y)*p.Bounds.Width+x-p.Bounds.X])).Where(v=>v!=0).ToArray();
        var ownFoot=own.DisplayPosition;
        var normal=Compose();Check(Glyphs(normal,foot).Length>20&&Glyphs(normal,ownFoot).Length>20,"sighted title labels drawn for self and peer");
        Feed(c,Blind(1));var blind=Compose();Check(blind.Bounds==renderer.Bounds,"blind title keeps full-screen affliction patch");
        Check(Glyphs(blind,foot).Length==0,"blind mode 1 title does not reveal peer position");Check(Glyphs(blind,ownFoot).Length>20,"blind mode 1 keeps own title");
        Check(blind.Colors[(peer.Py-blind.Bounds.Y)*blind.Bounds.Width+peer.Px-blind.Bounds.X]==0,"blind mode 1 peer stays black under title compositor");
        Feed(c,Blind(2));var dim=Compose();var dimmed=Glyphs(dim,foot);ushort expected=AfflictionColors.Darken(0x7ffc);
        Check(dimmed.Length>20&&dimmed.All(v=>v==expected),"blind mode 2 darkens title glyphs with the scene");
        Feed(c,Blind(0));Check(Compose().Colors.SequenceEqual(normal.Colors),"blind release restores exact title composition");
    }
    static void TitleEmoteOriginalAssets(){
        using var reader=new EmoteSprites();foreach(int gfx in new[]{0,1,37,48,61,734,1186,2786,2796,6650,6658,6661,6671})foreach(int action in Enumerable.Range(66,4))foreach(int direction in Enumerable.Range(0,gfx==0?8:1)){
            var sequence=reader.Load(gfx,action,direction)!;Check(sequence.Duration>0&&sequence.Frames.All(f=>f.Blocks.Length>0),$"read-only original gfx {gfx} action {action} direction {direction}");
            Check(sequence.At(0)!=null&&sequence.At(sequence.Duration)==null,"original animation lifecycle");
        }
        // T160 3.4 on real rows: 6080 has 67-69 but no 66, and has 0/20 but no 4.
        var horse=reader.Load(6080,66,0,20);Check(horse!=null&&ReferenceEquals(horse,reader.Load(6080,20,0)),"real horse emote falls back to pose 20");
        var horseZero=reader.Load(6080,66,0,4);Check(horseZero!=null&&ReferenceEquals(horseZero,reader.Load(6080,0,0)),"real horse missing pose 4 falls back to action 0");
        // gfx 2129 has rows 0/4 but no usable sprite frames in the pinned paks: the chain ends in null instead of an exception.
        var knight=reader.Load(2129,66,2,4);Check(ReferenceEquals(knight,reader.Load(2129,4,2)),"real gfx 2129 follows the same pose/0 chain without throwing");
        Check(reader.Load(3225,66,0,0)?.Duration is null or >0,"dummy gfx missing emote does not throw");
        Check(reader.Load(6080,68,0,20)?.Frames.Length==12&&!ReferenceEquals(reader.Load(6080,68,0,20),horse),"real horse 68 keeps its own sprite frames");
    }
    static void TitleEmoteCompositeAssets(){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"));
        var assets=(Assets)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Assets));
        Backing(assets,"Root",root);Backing(assets,"CanvasWidth",4096);Backing(assets,"CanvasHeight",4096);Backing(assets,"Scene",new List<Draw>());
        var renderer=(Renderer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Renderer));Set(renderer,"assets",assets);
        var own=(World)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(World));Backing(own,"X",32600);Backing(own,"Y",32940);
        var catalog=new ObjectAssets(root);using var c=TitleEmoteSeed();
        Patch Compose(List<ObjectVisual> visuals){
            var draw=new Draw(48,44,0,1000000,"actor",0,-1,-1,new Tile(2,[new Pixel(0,0,0x7c00)]));
            var bounds=new System.Drawing.Rectangle(draw.Px-100,draw.Py-150,250,250);
            var original=new Patch(bounds,new ushort[bounds.Width*bounds.Height],new bool[bounds.Width*bounds.Height],new int[bounds.Width*bounds.Height],[draw],new uint?[bounds.Width*bounds.Height]);
            return (Patch)typeof(Renderer).GetMethod("ComposeTitlesAndEmotes",F)!.Invoke(renderer,[own,c.World,catalog,original,visuals,1400L])!;
        }
        foreach(byte action in new byte[]{66,67,68,69}){
            Feed(c,EmoteUpdate(123,action));var patch=Compose([]);
            Check(c.World.EmoteAssetNotice==""&&renderer.OwnCombatVisual?.Action=="emote-"+action,"real original own pose composited "+action);
            Check(patch.Commands.Count(d=>d.Layer=="actor")>1,"original emote replaces base actor tiles "+action);
            Check(patch.Mask.Count(x=>x)>100,"original emote pixels present "+action);
        }
        Feed(c,Blind(1));Feed(c,EmoteUpdate(123,68));Check(Compose([]).Colors.Count(v=>v!=0)>100,"blind mode 1 own emote stays visible");Feed(c,Blind(0));
        byte[] remote=Pack(456);remote[9]=remote[10]=0;Feed(c,remote);Feed(c,EmoteUpdate(456,68));
        var visual=new ObjectVisual(456,0,3225,"target",32600,32940,"breath","8:0",Coordinates.Screen(32600,32940),System.Drawing.Rectangle.Empty,false,0);
        var list=new List<ObjectVisual>{visual};var result=Compose(list);
        Check(list.Single().Action=="emote-68"&&result.VisibleObjectIds.Contains(456),"remote emote visual and selectable pixel owner preserved");
    }
}
