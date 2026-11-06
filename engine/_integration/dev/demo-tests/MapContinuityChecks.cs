using P06;
using P24.MapEffectEvents;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

partial class Checks {
    static object? Invoke(object o,string method,params object[] args)=>o.GetType().GetMethod(method,F)!.Invoke(o,args);
    static byte[] MapPacket(ushort map,bool water=false)=>Packet(b=>{b.Write((byte)206);b.Write(map);b.Write((byte)(water?1:0));});
    static byte[] TeleportPacket(uint portal=50001)=>Packet(b=>{b.Write((byte)149);b.Write((ushort)0xa7eb);b.Write(portal);});
    static byte[] TimePacket(uint seconds)=>Packet(b=>{b.Write((byte)123);b.Write(seconds);});
    static string mapFixture="";
    static long mapNow=10000;
    static LoginConnection MapSeed(ushort map=0,ushort x=32600,ushort y=32940,bool asyncLoad=false){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);
        Set(c.World,"SceneClock",(Func<long>)(()=>mapNow));Set(c.World,"ShipClock",(Func<long>)(()=>mapNow));
        Set(c.World,"MapAssetsRoot",mapFixture);Set(c.World,"AsyncMapLoading",asyncLoad);c.World.CacheEnabled=true;
        Feed(c,Convert.FromHexString("DF03158B7B94F02F"));
        var status=RewardTests.Status(1,0);BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(1),123);
        BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(16),100);Feed(c,status);
        Set(c.World,"ProbeSent",true);Feed(c,MapPacket(map));Feed(c,Pack(123,x,y));Feed(c,TimePacket(5400));
        return c;
    }
    static void Arrive(LoginConnection c,ushort map,ushort x,ushort y){
        int epoch=c.World.WorldEpoch;Feed(c,MapPacket(map));
        Check(c.World.WorldEpoch>epoch&&c.World.Actor==null&&!c.World.Renderable&&c.World.ActionLocked,"map boundary locks until own pack "+map);
        Feed(c,Pack(123,x,y));Check(c.World.MapId==map&&c.World.AuthoritativeMapId==map&&c.World.AuthoritativeActor is { } a&&a.X==x&&a.Y==y,"authoritative destination "+map);
        Check(c.World.Renderable&&!c.World.ActionLocked&&c.World.MapCache!.Contains(x,y),"fresh cache ready "+map);
    }
    static void SeedOldScene(LoginConnection c){
        var a=c.World.Actor!;Feed(c,Pack(456,(ushort)(a.X+1),a.Y));c.World.SelectTarget(456);
        Feed(c,LoginPackets.Pad([55,..BitConverter.GetBytes(123u),189,0]));
        GroundPacket(c,1);c.World.SelectGround(900102);
        Check(c.World.Objects.Length>0&&c.World.MagicEffects.Length>0&&c.World.Ground.Length>0,"seed old objects, magic and ground");
    }
    static void CleanScene(LoginConnection c,string label)=>Check(c.World.Objects.Length==0&&c.World.Ground.Length==0&&c.World.MagicEffects.Length==0&&c.World.TargetId==null&&c.World.SelectedGroundId==null&&c.World.InteractionKind=="closed",label);
    static async Task MapContinuity(){
        string fixtureRoot=Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../..")),"scratch","issue-140","synthetic-maps");
        mapFixture=Path.Combine(fixtureRoot,"assets");
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(fixtureRoot,"build","offline-checks")+Path.DirectorySeparatorChar);
        MakeMapFixture();await CaveAndHandshake();await ShipRoundTrip();await InnRoundTrip();MapLoadRaces();MapEffectLifecycle();
    }
    static async Task CaveAndHandshake(){
        using var c=MapSeed(0,32477,32852);SeedOldScene(c);int sends=0;
        Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);
            if(b[0]==29)Check(b.SequenceEqual(MovementRules.Move(32477,32852,0)),"cave entry sends C29 from adjacent tile");
            else{Check(b.SequenceEqual(LoginPackets.Pad([52])),"C52 handshake only");sends++;}a();return Task.FromResult(true);});
        Check(await c.Move(0)&&c.World.Actor!.Y==32851&&c.World.MapId==0,"walking reaches portal without locally choosing map");
        Feed(c,TeleportPacket());int epoch=c.World.WorldEpoch;Feed(c,TeleportPacket());
        Check(c.World.WorldEpoch==epoch&&c.World.TeleportAckPending,"duplicate S149 does not clear twice");
        CleanScene(c,"S149 retires old scene");Check(c.World.MapCache==null&&c.World.PendingMapLoad==null,"S149 drops cache/load");
        await (Task)Invoke(c,"AcknowledgeTeleport")!;await (Task)Invoke(c,"AcknowledgeTeleport")!;
        Check(sends==1&&c.World.TeleportState=="awaiting-world"&&!c.MovementAvailable,"ACK exactly once, waits for world");
        Arrive(c,1,32669,32802);SeedOldScene(c);var inside=c.World.MapCache;
        Arrive(c,0,32477,32853);CleanScene(c,"cave exit has no cave entities/effects");
        Check(!ReferenceEquals(inside,c.World.MapCache),"cave exit uses fresh cache");
        SeedOldScene(c);Arrive(c,0,32585,32929);CleanScene(c,"same-map S206 also resets scene");
        Feed(c,TeleportPacket(50002));Feed(c,LoginPackets.Pad([202,7]));
        Check(c.World.TeleportPending&&!c.World.Renderable,"unlock alone is not recovery completion");
        Arrive(c,0,32585,32929);Check(c.World.TeleportState=="arrived","abort recovery completes with map and own");
        using var queued=MapSeed();int staleSends=0;
        Sender(queued,(b,v,a)=>{Arrive(queued,1,32669,32802);if(!v())return Task.FromResult(false);staleSends++;a();return Task.FromResult(true);});
        Check(!await queued.Turn(2)&&staleSends==0,"queued old-world movement rejected at send gate");
    }
    static byte[] TicketPacket(ushort map,uint id)=>Packet(b=>{
        b.Write((byte)5);b.Write((byte)1);b.Write(id);b.Write((ushort)0);b.Write((byte)255);b.Write((byte)0);
        b.Write((ushort)138);b.Write((byte)1);b.Write(1u);b.Write((byte)1);Text(b,map==5?"$314":"$315");b.Write((byte)0);b.Write(new byte[11]);
    });
    static async Task ShipRoundTrip(){
        using var c=MapSeed();var sent=new List<byte[]>();
        Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        foreach(ushort ship in new ushort[]{5,6}){
            foreach(int r1 in new[]{0,32767})foreach(int r2 in new[]{0,32767}){
                var edge=ShipRules.Landing(ship,r1,r2);var cache=new OriginalMapCache(mapFixture,edge.X,edge.Y,ShipRules.Destination(ship));
                Check(cache.Contains(edge.X,edge.Y),$"ship landing endpoint cache {ship}/{r1}/{r2}");
            }
            Arrive(c,ship,32733,32797);Feed(c,TicketPacket(ship,(uint)(900300+ship)));
            uint arrival=ship==5?10800u:16200u;mapNow+=10000;Feed(c,TimePacket(arrival));
            int count=sent.Count;await (Task)Invoke(c,"AdvanceShip")!;
            Check(sent.Count==count+1&&sent[^1][0]==231&&BinaryPrimitives.ReadUInt16LittleEndian(sent[^1].AsSpan(1))==ShipRules.Destination(ship),"arrival C231 route "+ship);
            await (Task)Invoke(c,"AdvanceShip")!;Check(sent.Count==count+1,"pending ship does not resend "+ship);
            Feed(c,Pack(123,32733,32797));Check(c.World.ShipPending,"pre-teleport own pack is not arrival "+ship);
            var passage=c.World.ShipPassage!;Feed(c,LoginPackets.Pad([57,..BitConverter.GetBytes(passage.TicketObjectId)]));
            // The server may repair a random proposal onto a nearby passable tile.
            ushort x=(ushort)(passage.X+1);Arrive(c,passage.Destination,x,passage.Y);
            Check(c.World.ShipState=="arrived"&&!c.World.ShipPending&&c.World.Actor!.X==x&&c.World.ShipNotice.Contains("校正"),"server repaired landing unlocks "+ship);
            Check(c.World.Inventory.Length==0,"ticket removal is authoritative "+ship);
        }
        Arrive(c,5,32733,32797);Feed(c,TicketPacket(5,900399));mapNow+=10000;Feed(c,TimePacket(21600));
        await (Task)Invoke(c,"AdvanceShip")!;Arrive(c,1,32744,32809);
        Check(c.World.ShipState=="interrupted","unexpected map interrupts passage");
        Arrive(c,5,32733,32797);Check(c.World.ShipPassage==null&&c.World.ShipState=="idle","new boarding clears interrupted attempt");
        mapNow+=10000;Feed(c,TimePacket(32400));int before=sent.Count;await (Task)Invoke(c,"AdvanceShip")!;
        Check(sent.Count==before+1,"reboarded passage can send again");
        using var fresh=MapSeed(5,32733,32797);int refused=0;
        Sender(fresh,(b,v,a)=>{if(!v())return Task.FromResult(false);refused++;a();return Task.FromResult(true);});
        mapNow+=10000;Feed(fresh,TimePacket(10800));await (Task)Invoke(fresh,"AdvanceShip")!;
        Check(refused==0,"missing ticket does not send");Feed(fresh,TicketPacket(5,900400));mapNow+=3001;
        Feed(fresh,TimePacket(10800));await (Task)Invoke(fresh,"AdvanceShip")!;Check(refused==0&&!fresh.World.ShipClockFresh,"duplicate time cannot freshen expired sample");
        foreach(var (map,t,open) in new (ushort,uint,bool)[]{(0,5399,false),(0,5400,true),(0,8999,true),(0,9000,false),(4,359,true),(4,360,false),(4,10800,true),(4,14400,false)})
            Check(ShipRules.BoardingOpen(map,t)==open,$"boarding boundary {map}/{t}");
    }
    static async Task InnRoundTrip(){
        using var c=MapSeed(0,32596,32929);Feed(c,Pack(456,32597,32929));c.World.SelectTarget(456);
        string template=Path.Combine(mapFixture,"test-dialog.json");File.WriteAllText(template,"{\"test-inn\":\"<a action='room'>租房</a><a action='enter'>進入</a>\"}");
        c.World.DialogTemplates=new(template);var sent=new List<byte[]>();
        Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        void Dialog()=>Feed(c,Packet(b=>{b.Write((byte)39);b.Write(456u);Text(b,"test-inn");b.Write((ushort)0);b.Write((ushort)0);}));
        Check(await Service(c,"talk")&&sent[^1][0]==34,"inn C34");Dialog();
        Check(await Service(c,"option",0)&&sent[^1].SequenceEqual(NpcPackets.Action(456,"room")),"inn room C125 from server page");
        Check(c.World.Inventory.Length==0&&c.World.MapId==0&&!await Service(c,"option",0),"room request neither grants key nor repeats");
        // A key is opaque inventory data to the client, not a local permission grant.
        Feed(c,Packet(b=>{b.Write((byte)15);b.Write(900500u);b.Write((ushort)0);b.Write((byte)255);b.Write((byte)0);b.Write((ushort)1);b.Write((byte)1);b.Write(1u);b.Write((byte)1);Text(b,"測試旅館鑰匙");b.Write((byte)0);b.Write(new byte[11]);}));
        Dialog();Check(await Service(c,"option",1)&&sent[^1].SequenceEqual(NpcPackets.Action(456,"enter")),"inn enter C125");
        Arrive(c,16384,32745,32803);CleanScene(c,"inn entry closes old dialog and AOI");
        Check(c.World.Inventory.Single().ObjectId==900500,"enter does not locally consume key");
        SeedOldScene(c);Arrive(c,0,32599,32931);CleanScene(c,"inn exit clears room AOI");
    }
    static void MapLoadRaces(){
        using var c=MapSeed(asyncLoad:true);var old=c.World.PendingMapLoad!;
        var stale=new OriginalMapCache(mapFixture,old.X,old.Y,old.Map);
        Feed(c,MapPacket(1));Feed(c,Pack(123,32744,32809));var newer=c.World.PendingMapLoad!;
        Check(!(bool)Invoke(c.World,"CompleteMapLoad",old,stale)!&&c.World.PendingMapLoad==newer,"stale map completion rejected");
        Invoke(c.World,"FailMapLoad",old,new IOException("old failure"));Check(c.World.PendingMapLoad==newer&&c.World.MapResourceState=="loading","stale failure cannot erase new request");
        Feed(c,MapPacket(0));Feed(c,Pack(123,old.X,old.Y));var back=c.World.PendingMapLoad!;
        Check(back!=old&&!(bool)Invoke(c.World,"CompleteMapLoad",old,stale)!,"ABA same map/position still rejects old generation");
        Check((bool)Invoke(c.World,"CompleteMapLoad",back,new OriginalMapCache(mapFixture,back.X,back.Y,back.Map))!&&c.World.Renderable,"current async load publishes");
        Feed(c,Pack(123,33000,33000));var far=c.World.PendingMapLoad!;
        Check(c.World.MapCache==null&&far.X==33000&&!c.World.Renderable&&c.World.ActionLocked,"far correction retires cache and requests cold load");
        Check((bool)Invoke(c.World,"CompleteMapLoad",far,new OriginalMapCache(mapFixture,far.X,far.Y,far.Map))!&&c.World.SupportedMap,"far correction cold load contains destination");
        Feed(c,MapPacket(60000));Feed(c,Pack(123));var missing=c.World.PendingMapLoad!;
        Invoke(c.World,"FailMapLoad",missing,new FileNotFoundException("synthetic missing map"));
        Check(c.World.MapCache==null&&!c.World.Renderable&&c.World.MapResourceState=="unavailable","missing map cannot display old map");
        Feed(c,MapPacket(0));Feed(c,Pack(123));var cancel=c.World.PendingMapLoad!;c.World.Reset();
        Check(!(bool)Invoke(c.World,"CompleteMapLoad",cancel,stale)!&&c.World.MapCache==null,"reset invalidates pending load");
        using var sync=MapSeed();Feed(sync,Pack(123,33000,33000));Check(sync.World.SupportedMap&&sync.World.MapCache!.Contains(33000,33000),"synchronous far correction also cold loads");
    }
    static void MapEffectLifecycle(){
        using var c=MapSeed();var bytes=File.ReadAllBytes(Path.Combine(mapFixture,"maps/effects/6016.json"));
        var controller=new Effect6016Controller(Effect6016Definition.FromR27Metadata(bytes,Convert.ToHexString(SHA256.HashData(bytes))));
        var host=new Effect6016Host(controller,null,new EffectHostLane(),()=>mapNow);c.World.Attach6016(host);
        var owners=host.Frames().Select(f=>f.Owner).ToArray();Check(owners.Length==4,"four synthetic map effect owners registered");
        var renderer=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(renderer);
        var draws=new List<Draw>();Call(renderer,"ComposeMapEffects",c.World,draws,mapNow);
        Check(renderer.MapEffectVisuals.Length==4&&draws.Count==4&&draws.All(d=>d.Layer=="map-effect"),"real effect composition uses active blocks");
        for(int n=1;n<8;n++){
            bool rejected=false;try{Feed(c,MapPacket(1)[..n]);}catch(ProtocolException){rejected=true;}
            Check(rejected&&host.Frames().Select(f=>f.Owner).SequenceEqual(owners)&&c.World.Renderable,"truncated S206 is atomic "+n);
        }
        var malformed=MapPacket(1);malformed[7]=1;bool padding=false;try{Feed(c,malformed);}catch(ProtocolException){padding=true;}
        Check(padding&&host.Frames().Select(f=>f.Owner).SequenceEqual(owners),"bad S206 padding cannot retire effects");
        Feed(c,TeleportPacket());Check(host.Frames().Count==0,"S149 retires effect owners immediately");
        draws.Clear();Call(renderer,"ComposeMapEffects",c.World,draws,mapNow);Check(draws.Count==0&&renderer.MapEffectVisuals.Length==0,"no effects composed between maps");
        mapNow+=500;Arrive(c,1,32744,32809);Check(host.Frames().All(f=>f.Owner.Key.MapId==1)&&host.Frames().All(f=>!owners.Contains(f.Owner)),"new map owns only new effects");
        mapNow+=500;Arrive(c,0,32600,32940);
        Check(host.Frames().All(f=>f.Owner.Key.MapId==0&&!owners.Contains(f.Owner)&&f.StartedAtMs==mapNow),"return map starts fresh generations and timing");
        var cache=c.World.MapCache!;cache.Shift(2);Call(c.World,"Sync6016",mapNow);
        Check(host.Frames().Count==4&&host.Frames().All(f=>cache.Slots.Any(b=>b?.Name==f.Owner.Key.BlockKey)),"cache eviction retires detached effect blocks");
        c.World.Reset();Check(host.Frames().Count==0,"disconnect/reset retires map effects");
    }
    // Entirely authored test fixtures: one colored pixel, synthetic S32 records and terrain.
    // Generated only under scratch; never ships in the product or substitutes for native evidence.
    static void MakeMapFixture(){
        Directory.CreateDirectory(mapFixture);string root=Path.Combine(mapFixture,"maps");Directory.CreateDirectory(root);
        string Save(string path,byte[] bytes){string file=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllBytes(file,bytes);return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();}
        byte[] Raw(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return s.ToArray();}
        string tileHash=Save("tiles/1.til",Raw(b=>{b.Write(1);b.Write(0);b.Write(10);b.Write(new byte[]{2,0,0,1,1,1,0,1});b.Write((ushort)31);}));
        var frames=Enumerable.Range(0,12).Select(i=>new{index=i,bounds=new[]{0,0,1,1},blocks=new[]{new{a=0,b=0,z=0,order=0,pixels=new[]{new[]{0,0,31}}}}}).ToArray();
        var seq=Effect6016Definition.SequenceFrames.Select((f,i)=>new{frame=f,ticks=5,events=i==1?"<3332<1340<1340":""}).ToArray();
        string effectHash=Save("effects/6016.json",JsonSerializer.SerializeToUtf8Bytes(new{id=6016,tickRate=24,flags=0,frames,sequence=seq}));
        string effectsHash=Save("effects/manifest.json",JsonSerializer.SerializeToUtf8Bytes(new{schema="p24-map-effects-v1",effects=new[]{new{id=6016,sha256=effectHash}}}));
        string terrainHash=Save("terrain/test.bin",Enumerable.Repeat((byte)3,1024*1024).ToArray());
        var positions=new[]{(32600,32940),(32477,32853),(32669,32802),(32585,32929),(32744,32809),(32733,32797),(32564,32723),(32631,32972),(32745,32803),(32599,32931),(33000,33000)};
        // Include one extra row/column: randomized ship landings can cross a cold-origin edge.
        var blockCoords=new HashSet<(int,int)>();foreach(var (x,y) in positions){var (ox,oy)=OriginalMapCache.ColdOrigin(x,y);for(int by=0;by<3;by++)for(int bx=0;bx<3;bx++)blockCoords.Add((ox/128+32512+bx,oy/64+32256+by));}
        var blocks=new List<object>();var maps=new List<object>();
        foreach(int map in new[]{0,1,4,5,6,16384}){
            foreach(var (bx,by) in blockCoords){string name=$"{bx:x4}{by:x4}",path=$"test/{map}/{name}.s32";
                string hash=Save(path,Raw(b=>{for(int i=0;i<8192;i++)b.Write(256u);b.Write((ushort)0);for(int i=0;i<8192;i++)b.Write((ushort)3);b.Write(0);b.Write(0);b.Write(0);b.Write((ushort)0);b.Write((ushort)1);b.Write((ushort)6016);b.Write((ushort)((bx-0x7e00)*64));b.Write((ushort)((by-0x7e00)*64));}));
                blocks.Add(new{mapId=map,name=name+".s32",path,sha256=hash,noEffectTail=false});
            }
            maps.Add(new{mapId=map,minX=32256,minY=32256,maxX=33279,maxY=33279,unsupportedBlocks=Array.Empty<object>(),verifiedAbsent=Array.Empty<string>(),terrain=new{path="terrain/test.bin",sha256=terrainHash}});
        }
        string manifestHash=Save("manifest.json",JsonSerializer.SerializeToUtf8Bytes(new{schema="p24-map-provider-v1",maps,blocks,tiles=new[]{new{tile=1,sha256=tileHash}}}));
        var profiles=JsonSerializer.SerializeToUtf8Bytes(new{schema="p24-player-profiles-v1",profiles=new[]{new{gfx=0,weapon=0,path="players/test-only.json",sha256=new string('0',64),stepMs=640,serverActionMs=new{walk=640,attack=640,spellDir=640,spellNoDir=640},castReleaseMs=new{spellDir=320}}}});
        Directory.CreateDirectory(Path.Combine(mapFixture,"players"));File.WriteAllBytes(Path.Combine(mapFixture,"players/manifest.json"),profiles);
        File.WriteAllBytes(Path.Combine(mapFixture,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{files=new object[]{new{path="maps/manifest.json",sha256=manifestHash},new{path="maps/effects/manifest.json",sha256=effectsHash},new{path="players/manifest.json",sha256=Convert.ToHexString(SHA256.HashData(profiles)),size=profiles.Length}}}));
    }
}
