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
        }
        using(var c=TitleEmoteSeed()){
            int sends=0;byte[]? sent=null;Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sends++;sent=bytes;admit();return Task.FromResult(true);});
            var stamp=c.CaptureTitleEmote();Check(await c.SendTitle(stamp,"offline","稱號")&&sends==1&&sent![0]==90,"C90 memory sender");Check(c.World.Actor!.Title=="","title never predicted before S183");
            foreach(int action in Enumerable.Range(66,4)){Check(await c.SendEmote(stamp,action)&&sent![0]==120&&sent[1]==action,"C120 memory sender "+action);Check(c.World.ActiveEmotes(1000).Single().Action==action,"self predicts only admitted emote "+action);}
            int before=sends;Check(!await c.SendEmote(stamp,322)&&!await c.SendTitle(stamp,"offline","😀")&&sends==before,"bad request sends nothing");
            Set(c.World,"OwnDead",true);Check(!await c.SendEmote(stamp,68)&&sends==before,"own death blocks send");Set(c.World,"OwnDead",false);
            Feed(c,LoginPackets.Pad([206,0,0,0]));Check(!await c.SendTitle(stamp,"offline","stale")&&sends==before,"old map stamp cannot send");
        }
        using(var c=TitleEmoteSeed()){
            var release=new TaskCompletionSource();int sends=0;Sender(c,async(bytes,current,admit)=>{await release.Task;if(!current())return false;sends++;admit();return true;});
            var stamp=c.CaptureTitleEmote();var pending=c.SendEmote(stamp,68);Check(!await c.SendEmote(stamp,69),"duplicate pending request blocked");
            c.World.Reset();release.SetResult();Check(!await pending&&sends==0&&c.World.ActiveEmotes(1000).Length==0,"delayed send revalidates reset before admission");
        }
        using(var c=TitleEmoteSeed()){
            Sender(c,(_,_,_)=>throw new IOException("synthetic transport failure"));Check(!await c.SendEmote(c.CaptureTitleEmote(),68)&&c.State==LoginState.Faulted,"send exception fails closed without retry");
        }
        var steps=EmoteSprites.ParseSteps("#0 1\n 110.framerate(40)\n 68.wave(1 2,56.0:4[123 56.1:6)",68,7);
        Check(steps[0]==new EmoteSprites.Step(63,0,100)&&steps[1]==new EmoteSprites.Step(63,1,150),"original direction offset/frame ticks/FPS");
        Check(EmoteSprites.ParseSteps("66.shop loop((1 1,368.0:4)",66,0).Single().File==368,"gfx2796 original doubled parenthesis");
        TitleEmoteReject(()=>EmoteSprites.ParseSteps("68.wave(1 2,56.0:4)",68,0),"metadata frame count mismatch");
        TitleEmoteReject(()=>EmoteSprites.ParseSteps("68.wave(1 1,56.0:0)",68,0),"metadata zero duration");
        var bytes=EmoteSpriteFixture();var frame=EmoteSprites.Decode(bytes,56).Single();Check(frame.Key=="56:0"&&frame.Blocks.Single().Tile.Pixels.Single()==new Pixel(0,0,0x7c00),"synthetic original SPR pixel decoder");
        for(int n=0;n<bytes.Length;n++)TitleEmoteReject(()=>EmoteSprites.Decode(bytes[..n],56),"truncated SPR "+n);
        var sequence=new EmoteSprites.EmoteSequence(0,steps,[frame,frame with{Key="second"}]);Check(sequence.At(0)?.Key=="56:0"&&sequence.At(100)?.Key=="second"&&sequence.At(250)==null,"animation advances once and terminates");
        TitleEmoteLabelRendering();
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
    }
    static void TitleEmoteOriginalAssets(){
        using var reader=new EmoteSprites();foreach(int gfx in new[]{0,1,37,48,61,734,1186,2786,2796,6650,6658,6661,6671})foreach(int action in Enumerable.Range(66,4))foreach(int direction in Enumerable.Range(0,gfx==0?8:1)){
            var sequence=reader.Load(gfx,action,direction);Check(sequence.Duration>0&&sequence.Frames.All(f=>f.Blocks.Length>0),$"read-only original gfx {gfx} action {action} direction {direction}");
            Check(sequence.At(0)!=null&&sequence.At(sequence.Duration)==null,"original animation lifecycle");
        }
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
        byte[] remote=Pack(456);remote[9]=remote[10]=0;Feed(c,remote);Feed(c,EmoteUpdate(456,68));
        var visual=new ObjectVisual(456,0,3225,"target",32600,32940,"breath","8:0",Coordinates.Screen(32600,32940),System.Drawing.Rectangle.Empty,false,0);
        var list=new List<ObjectVisual>{visual};var result=Compose(list);
        Check(list.Single().Action=="emote-68"&&result.VisibleObjectIds.Contains(456),"remote emote visual and selectable pixel owner preserved");
    }
}
