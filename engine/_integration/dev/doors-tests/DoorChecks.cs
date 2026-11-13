using P06;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

partial class Checks {
    static byte[] DoorPack(byte action=29,uint id=700,ushort gfx=2346,ushort x=32600,ushort y=32939)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write(gfx);b.Write(action);
        b.Write(new byte[3]);b.Write(1u);b.Write(new byte[12]);b.Write((byte)255);
        b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] DoorAttribute(byte direction=0,byte passable=1,ushort x=32600,ushort y=32940)=>
        Packet(b=>{b.Write((byte)209);b.Write(x);b.Write(y);b.Write(direction);b.Write(passable);});
    static byte[] DoorAction(byte action,uint id=700)=>Packet(b=>{b.Write((byte)158);b.Write(id);b.Write(action);});
    static void DoorReject(Action action,string label){bool rejected=false;try{action();}catch(ProtocolException){rejected=true;}Check(rejected,label);}
    [CheckGroup("doors")]
    static async Task DoorChecks(){
        string fixtureRoot=Path.GetFullPath("scratch/issue-322/synthetic");
        mapFixture=Path.Combine(fixtureRoot,"assets");MakeMapFixture();
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(fixtureRoot,"build","offline-checks")+Path.DirectorySeparatorChar);
        Check(DoorPackets.Toggle(0x12345678,0x2345,0x3456).SequenceEqual(Convert.FromHexString("29452356347856341200000000000000")),"C41 HHD byte order and zero padding");
        bool zero=false;try{DoorPackets.Toggle(0,0,0);}catch(ArgumentOutOfRangeException){zero=true;}Check(zero,"C41 zero identity rejected");
        foreach(byte action in new byte[]{28,29,32,33,34,35,36,37})foreach(ushort gfx in new ushort[]{2346,99}){
            using var c=Seed();Feed(c,DoorPack(action,gfx:gfx));
            Check(c.World.IsDoor(700)&&c.World.Objects.Single(o=>o.ObjectId==700) is {} d&&d.DoorAction==action&&d.Dead==(action==37),$"S87 door action {action}, gfx {gfx}");
            Check(!c.World.DoorBlocks(32600,32941,0),"S87 never guesses collision");
        }
        using(var c=Seed()){
            foreach(int offset in new[]{12,13,14,15,19,23,28,30,31,32,33,34,35,36}){
                var packet=DoorPack();packet[offset]^=1;Check(!DoorPackets.IsPack(packet),"S87 signature discriminator "+offset);
            }
            Feed(c,Pack(700));Check(!c.World.IsDoor(700),"ordinary NPC is not a door");
            Feed(c,DoorPack());Feed(c,DoorAction(28));Check(c.World.Objects.Single(o=>o.ObjectId==700).DoorAction==28,"S158 open persists");
            Feed(c,DoorAction(29));Check(c.World.Objects.Single(o=>o.ObjectId==700).DoorAction==29,"S158 close persists");
            Feed(c,DoorAttribute());Feed(c,DoorAction(28));Check(c.World.DoorBlocks(32600,32941,0),"S158 does not unlock S209 collision");
            Feed(c,DoorAttribute(passable:0));Check(!c.World.DoorBlocks(32600,32941,0),"S209 open removes collision");
            Feed(c,DoorAction(37));Check(c.World.Objects.Single(o=>o.ObjectId==700).Dead,"S158 door destruction");
            Feed(c,DoorPack());Check(!c.World.Objects.Single(o=>o.ObjectId==700).Dead,"new full pack restores door incarnation");
            c.World.SelectTarget(700);Feed(c,DoorPack(37));Check(c.World.TargetId==null,"destroyed full pack clears selected door");
            Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));Check(!c.World.IsDoor(700),"S120 removes door identity");
            Feed(c,Pack(700));Feed(c,DoorAction(28));Check(!c.World.IsDoor(700),"reused object ID is not a door");
            Feed(c,DoorAction(70,456));Check(c.World.Objects.Single(o=>o.ObjectId==456).ShopActive,"non-door S158 still reaches existing dispatcher");
        }
        // S158 is shared: fishing observes it first, then doors consume their own objects.
        using(var c=Seed()){
            Feed(c,DoorPack());Feed(c,FishingStart());var fishing=c.ReadFishing();
            Check(fishing.Active,"fishing S158 still starts with a door in view");
            Feed(c,DoorAction(28));
            Check(c.World.Objects.Single(o=>o.ObjectId==700).DoorAction==28&&c.ReadFishing()==fishing,"door S158 still works and leaves fishing state unchanged");
        }
        foreach(byte opcode in new byte[]{87,158,209}){
            var packet=opcode==87?DoorPack():opcode==158?DoorAction(28):DoorAttribute();
            for(int n=0;n<packet.Length;n++){
                using var c=Seed();Feed(c,DoorPack());var before=c.World.Objects.Single(o=>o.ObjectId==700);
                DoorReject(()=>Feed(c,packet[..n]),$"S{opcode} truncated {n}");
                Check(ReferenceEquals(before,c.World.Objects.Single(o=>o.ObjectId==700))&&c.World.DoorTiles.Length==0,"malformed packet is atomic");
            }
            using var bad=Seed();Feed(bad,DoorPack());packet[^1]=1;DoorReject(()=>Feed(bad,packet),"nonzero padding S"+opcode);
        }
        using(var c=Seed()){
            DoorReject(()=>Feed(c,DoorAttribute(direction:2)),"invalid S209 direction");
            DoorReject(()=>Feed(c,DoorAttribute(passable:2)),"invalid S209 passability");
            Check(c.World.DoorTiles.Length==0,"invalid attribute is atomic");
        }
        foreach(byte direction in new byte[]{0,1}){
            using var c=Seed();Feed(c,DoorAttribute(direction));
            Check(c.World.DoorTiles.Single()==new DoorTile(32600,32940,direction,true),"S209 preserves reported direction");
            for(int i=-1;i<=1;i++)for(int h=0;h<8;h++){
                int tx=32600+(direction==0?i:0),ty=32940+(direction==1?i:0);
                Check(c.World.DoorBlocks(tx-MovementRules.Dx[h],ty-MovementRules.Dy[h],h),$"closed span {direction}/{i}/{h}");
            }
            Check(!c.World.DoorBlocks(32610,32950,0),"unrelated tile unaffected");
            Feed(c,DoorAttribute(direction,0));Check(!c.World.DoorBlocks(32600,32941,0),"open restores original terrain policy");
            Feed(c,DoorAttribute(direction));Feed(c,MapPacket(0));Check(c.World.DoorTiles.Length==0&&!c.World.DoorBlocks(32600,32941,0),"same-map S206 retires attributes");
            Feed(c,DoorAttribute(direction));c.World.Reset();Check(c.World.DoorTiles.Length==0,"reset retires attributes");
        }
        foreach(string invalidation in new[]{"remove","replace","map","reset","focus","death","teleport"}){
            using var c=Seed();Feed(c,DoorPack());bool focused=true;int sent=0;
            var pointer=new PlayerPointerController(c,()=>focused,()=>false,_=>true);
            Sender(c,(b,v,a)=>{
                switch(invalidation){
                    case "remove":Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));break;
                    case "replace":Feed(c,DoorPack());break;
                    case "map":Feed(c,MapPacket(0));break;
                    case "reset":c.World.Reset();break;
                    case "focus":focused=false;break;
                    case "death":Feed(c,DoorAction(37));break;
                    case "teleport":Feed(c,TeleportPacket());break;
                }
                if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);
            });
            Check(!await pointer.ActAsync(700,true)&&sent==0,"queued C41 invalidated by "+invalidation);
        }
        foreach(bool talk in new[]{false,true}){
            using var c=Seed();Feed(c,DoorPack());Feed(c,DoorAttribute());var original=c.World.Objects.Single(o=>o.ObjectId==700);int sent=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b.SequenceEqual(DoorPackets.Toggle(700,32600,32939)),"pointer sends C41 only");sent++;a();return Task.FromResult(true);});
            var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(await pointer.ActAsync(700,talk)&&sent==1,"one pointer door request "+talk);
            Check(ReferenceEquals(original,c.World.Objects.Single(o=>o.ObjectId==700))&&c.World.DoorBlocks(32600,32941,0),"C41 has no predicted animation/collision");
        }
        // Exercise the actual movement sender using the existing authored map fixture.
        using(var c=MapSeed()){
            int sent=0;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);});
            Feed(c,DoorAttribute(y:32939));Check(!await c.Move(0)&&sent==0,"keyboard movement blocked by S209");
            Feed(c,DoorAttribute(passable:0,y:32939));Check(await c.Move(0)&&sent==1,"keyboard movement allowed after S209 open");
        }
        using(var c=MapSeed()){
            int sent=0;Sender(c,(b,v,a)=>{Feed(c,DoorAttribute(y:32939));if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);});
            Check(!await c.Move(0)&&sent==0,"queued movement rechecks newly closed door");
        }
        await DoorPointerChecks();
        DoorRenderChecks(Path.Combine(fixtureRoot,"render-multi"),3);
        DoorRenderChecks(Path.Combine(fixtureRoot,"render-single"),1);
    }

    // Door at 32600,32939; S209 reports the 32599..32601 span on that row.
    static (LoginConnection C,PlayerPointerController P,List<byte[]> Sent) DoorWalker(byte? passable,ushort x=32600,Func<byte[],bool>? beforeSend=null){
        long now=10000;var c=MapSeed(x:x);var sent=new List<byte[]>();Feed(c,DoorPack());
        if(passable is byte p)Feed(c,DoorAttribute(passable:p,y:32939));
        Set(c,"MovementClock",(Func<long>)(()=>now+=1000));
        Sender(c,(b,v,a)=>{beforeSend?.Invoke(b);if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        return (c,new PlayerPointerController(c,()=>true,()=>false,_=>true,()=>now),sent);
    }
    static async Task DoorWalk(PlayerPointerController p,Action? eachStep=null){for(int i=0;i<16&&p.Active;i++){await p.TickAsync();eachStep?.Invoke();}}
    static async Task DoorPointerChecks(){
        var beyond=new System.Drawing.Point(32600,32938);var onDoor=new System.Drawing.Point(32600,32939);
        foreach(byte? passable in new byte?[]{0,null}){
            var (c,p,sent)=DoorWalker(passable);using var _=c;
            Check(p.MoveTo(beyond)&&p.RemainingSteps==2,"mouse route crosses open door object "+passable);await DoorWalk(p);
            Check(sent.Count==2&&sent.All(b=>b[0]==29&&b[5]==0)&&c.World.Actor is {X:32600,Y:32938},"mouse crosses open door with C29 only "+passable);
        }
        {
            var (c,p,sent)=DoorWalker(1);using var _=c;
            Check(!p.MoveTo(onDoor)&&sent.Count==0&&!p.Active,"mouse refuses closed door tile");
            bool touched=false;Check(p.MoveTo(beyond)&&p.RemainingSteps>2,"mouse route detours closed span");
            await DoorWalk(p,()=>touched|=c.World.Actor is {Y:32939,X:>=32599 and <=32601});
            Check(!touched&&c.World.Actor is {X:32600,Y:32938},"detour never enters closed span");
        }
        {
            var (c,p,sent)=DoorWalker(0);using var _=c;
            Check(p.MoveTo(beyond),"route planned while door open");Feed(c,DoorAttribute(y:32939));await p.TickAsync();
            Check(sent.Count==0&&!p.Active,"door closed after planning stops the route");
        }
        {
            LoginConnection? live=null;
            var (c,p,sent)=DoorWalker(0,beforeSend:_=>{Feed(live!,DoorAttribute(y:32939));return true;});using var _=c;live=c;
            Check(p.MoveTo(beyond),"route planned before queued send");await p.TickAsync();
            Check(sent.Count==0&&!p.Active,"door closed in the send queue stops the route");
        }
        {
            var (c,p,_)=DoorWalker(1);using var __=c;Feed(c,DoorAction(28));
            Check(!p.MoveTo(onDoor),"S158 open does not replace closed S209");
        }
        {
            var (c,p,_)=DoorWalker(0);using var __=c;Feed(c,DoorAction(28));Feed(c,DoorAction(29));
            Check(p.MoveTo(onDoor),"S158 close does not replace open S209");
        }
        {
            var (c,p,_)=DoorWalker(0);using var __=c;Feed(c,Pack(456,32600,32939));
            Check(!p.MoveTo(onDoor),"open door tile still blocked by another object");
            Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Check(p.MoveTo(onDoor),"door object itself is not occupancy");
        }
        {
            var (c,p,_)=DoorWalker(0);using var __=c;
            var store=typeof(OriginalMapCache).GetField("store",F)!.GetValue(c.World.MapCache)!;
            var terrain=(byte[])store.GetType().GetField("terrain",F)!.GetValue(store)!;var bounds=c.World.MapCache!.Bounds;
            int index=(32939-bounds.MinY)*bounds.Width+32600-bounds.MinX;byte original=terrain[index];terrain[index]=0;
            Check(!p.MoveTo(onDoor),"open door still obeys static terrain");terrain[index]=original;
            Check(p.MoveTo(onDoor),"terrain fixture restored");
        }
        {
            var (c,p,_)=DoorWalker(1,x:32601);using var __=c;
            Check(p.MoveTo(new(32602,32939))&&p.RemainingSteps==2,"closed door keeps diagonal corner check");
        }
    }

    // Authored colored-pixel fixture; frame keys are synthetic and cover playback logic only.
    static string DoorRenderFixture(string root,int frames){
        var pins=new List<object>();
        void Save(string path,object data,bool pin=false){
            var bytes=JsonSerializer.SerializeToUtf8Bytes(data);string file=Path.Combine(root,path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllBytes(file,bytes);
            if(pin)pins.Add(new{path,sha256=Convert.ToHexString(SHA256.HashData(bytes)),size=bytes.Length});
        }
        object Pixel(string key)=>new{key,bounds=new[]{0,0,24,24},blocks=new[]{new{a=0,b=0,z=0,source_order=0,pixels=new[]{new[]{0,0,31}}}}};
        Save("scene/manifest.json",new{schema="p22-dual-map-v1",world_inclusive_bounds=new[]{Coordinates.MinX,Coordinates.MinY,Coordinates.MaxX,Coordinates.MaxY},pixel_origin=new[]{0,756},terrain_stride=Coordinates.Columns,terrain_rows=Coordinates.Rows,canvas_size=new[]{3200,2000},draw_count=0});
        Save("scene/tile-pixels.json",Array.Empty<object>());Save("scene/draw-commands.json",Array.Empty<object>());
        Save("character/blocks.json",new{schema="p06-spr-blocks-v1",frames=Enumerable.Range(0,96).Select(i=>Pixel("a"+i)).ToArray()});
        Save("character/animation.json",new{schema="p04-character-v1",mapping=Enumerable.Range(0,8).SelectMany(d=>new[]{("breath",8,d*12),("walk",4,d*12+8)}.Select(m=>new{action=m.Item1,direction=d,sequence=Enumerable.Range(m.Item3,m.Item2).Select(i=>new{key="a"+i}).ToArray()})).ToArray()});
        Save("objects/manifest.json",new{gfx=Array.Empty<int>()});Save("objects/names.json",Array.Empty<string>());
        Save("shops/manifest.json",new{schema="p24-original-shop-appearance-v1",profiles=Array.Empty<object>()},true);
        Save("encountered-monsters.json",new{schema="p24-original-encountered-monsters-v1",profiles=Array.Empty<object>()},true);
        Save("world-appearances.json",new{schema="p24-original-world-appearances-v1",profiles=Array.Empty<object>()},true);
        Save("shadows/manifest.json",new{schema="p24-original-shadows-v1",profiles=Array.Empty<object>()},true);
        string[] actions=["open","close"];
        var door=new{gfx=2346,sprite=9000,stepMs=640,frames=actions.SelectMany(a=>Enumerable.Range(0,frames).Select(i=>Pixel($"{a}-{i}"))).ToArray(),
            sequences=actions.SelectMany(a=>Enumerable.Range(0,8).Select(d=>new{action=a,direction=d,sequence=Enumerable.Range(0,frames).Select(i=>new{key=$"{a}-{i}",ticks=24/frames}).ToArray()})).ToArray()};
        Save("doors/2346.json",door);string sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"doors/2346.json"))));
        Save("doors/manifest.json",new{schema="p24-original-inn-door-v1",profiles=new[]{new{gfx=2346,path="doors/2346.json",sha256=sha,sprite=9000}}},true);
        Save("integrity.json",new{schema="p06-assets-integrity-v1",files=pins});
        return root;
    }
    static void DoorRenderChecks(string root,int frames){
        var assets=new Assets(DoorRenderFixture(root,frames));using var renderer=new Renderer(assets);var catalog=new ObjectAssets(assets.Root);var own=new World(assets);own.Teleport(32600,32940); // T459：起點是產品常數（MotionRules），夾具不再寫 motion.json，這裡維持原本在門旁的位置。
        string last=$"-{frames-1}",label=frames+"-frame ";
        LoginConnection Session(byte action=29,ushort gfx=2346){var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Feed(c,DoorPack(action,gfx:gfx));return c;}
        ObjectVisual? Shown(LoginConnection c,long time)=>Compose(c,time).Item1;
        (ObjectVisual?,WorldObject[]) Compose(LoginConnection c,long time){renderer.ComposeObjects(own,c.World,catalog,out var visuals,out var missing,time);return(visuals.SingleOrDefault(v=>v.ObjectId==700),missing);}
        string? Key(LoginConnection c,long time)=>Shown(c,time)?.Frame;
        long Started(LoginConnection c)=>c.World.GetDoorPlayback(700)!.StartedAt;
        using(var c=Session()){
            Check(c.World.GetDoorPlayback(700)==null&&Key(c,1)=="close"+last,label+"S87 close holds last frame without playback");
            Feed(c,DoorAction(28));long at=Started(c);
            Check(Key(c,at)=="open-0",label+"S158 open starts on first frame");
            Check(Key(c,at+500)==(frames==3?"open-1":"open-0"),label+"open middle uses At timing");
            Check(Key(c,at+999)=="open"+last&&Key(c,at+60000)=="open"+last,label+"open stops on last frame");
            Check(Shown(c,at)!.Action=="open",label+"visual reports open action");
            Feed(c,DoorAction(28));Check(Started(c)==at&&Key(c,at+500)==(frames==3?"open-1":"open-0"),label+"repeated S158 keeps running playback");
            var running=c.World.GetDoorPlayback(700);
            Feed(c,Packet(b=>{b.Write((byte)237);b.Write(700u);b.Write((byte)50);}));
            Check(c.World.IsDoor(700)&&ReferenceEquals(running,c.World.GetDoorPlayback(700)),label+"unrelated object update keeps door and playback");
            Feed(c,DoorAction(29));var reverse=c.World.GetDoorPlayback(700)!;
            Check(!ReferenceEquals(running,reverse)&&reverse.Action==29&&Key(c,reverse.StartedAt)=="close-0",label+"reverse S158 restarts on opposite first frame");
            Check(Key(c,reverse.StartedAt+60000)=="close"+last,label+"reverse stops on its last frame");
        }
        using(var c=Session(28)){
            Check(c.World.GetDoorPlayback(700)==null&&Key(c,1)=="open"+last,label+"S87 open holds last frame without playback");
            Feed(c,DoorAttribute(passable:0));Check(c.World.GetDoorPlayback(700)==null,label+"S209 does not trigger playback");
            Feed(c,DoorAttribute());Check(c.World.GetDoorPlayback(700)==null,label+"S209 closed does not trigger playback");
            Feed(c,DoorAction(28));Check(c.World.GetDoorPlayback(700)==null,label+"S158 matching S87 state does not replay");
        }
        using(var c=Session()){
            Feed(c,DoorAction(28));Feed(c,DoorPack(29));
            Check(c.World.GetDoorPlayback(700)==null&&Key(c,1)=="close"+last,label+"S87 replacement drops old playback");
            Feed(c,DoorAction(28));Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));
            Check(c.World.GetDoorPlayback(700)==null&&Shown(c,1)==null,label+"S120 drops old playback");
            Feed(c,DoorPack(28));Check(c.World.GetDoorPlayback(700)==null&&Key(c,1)=="open"+last,label+"reappearing door has no stale playback");
            Feed(c,DoorAction(29));Feed(c,Pack(700,32600,32939));Check(c.World.GetDoorPlayback(700)==null&&!c.World.IsDoor(700),label+"ID reuse drops door and playback");
        }
        foreach(string reset in new[]{"map","reset"}){
            using var c=Session();Feed(c,DoorAction(28));
            if(reset=="map")Feed(c,MapPacket(0));else c.World.Reset();
            Check(c.World.GetDoorPlayback(700)==null,label+"world invalidates playback: "+reset);
            Feed(c,DoorPack(28));Check(c.World.GetDoorPlayback(700)==null,label+"new world door has no stale playback: "+reset);
        }
        using(var c=Session()){
            Feed(c,DoorAction(28));Feed(c,DoorAction(34));Check(c.World.GetDoorPlayback(700)==null,label+"crack action stops open playback");
            var (shown,missing)=Compose(c,1);Check(shown==null&&missing.Any(o=>o.ObjectId==700),label+"crack action stays unavailable without asset");
            Feed(c,DoorAction(28));Feed(c,DoorAction(37));Check(c.World.GetDoorPlayback(700)==null&&Shown(c,1)==null,label+"destroyed door has no playback");
        }
        using(var c=Session(gfx:99)){
            var (shown,missing)=Compose(c,1);Check(c.World.IsDoor(700)&&shown==null&&missing.Any(o=>o.ObjectId==700),label+"unverified door gfx is not drawn with borrowed art");
        }
    }
}
