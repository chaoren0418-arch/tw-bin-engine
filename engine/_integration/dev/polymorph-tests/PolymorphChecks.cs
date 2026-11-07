using P06;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

partial class Checks {
    static byte[] Poly(uint id,ushort gfx,ushort mode=29)=>Packet(b=>{
        b.Write((byte)76);b.Write(id);b.Write(gfx);b.Write(mode);
    });
    static byte[] Disguise(uint id,uint owner,ushort gfx,string name)=>Packet(b=>{
        b.Write((byte)76);b.Write(id);b.Write(owner);b.Write(gfx);Text(b,name);
    });
    static byte[] PolyPack(uint id,ushort gfx=1110,byte weapon=4,byte speed=2,byte brave=1)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(id);
        b.Write(gfx);b.Write(weapon);b.Write((byte)5);b.Write((byte)0);b.Write(speed);b.Write(0u);b.Write((ushort)0);
        Text(b,"offline");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");
        b.Write((byte)0);b.Write((byte)100);b.Write(brave);b.Write((byte)1);b.Write((byte)0);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] PolyAction(uint id,byte action)=>Packet(b=>{b.Write((byte)158);b.Write(id);b.Write(action);});
    static byte[] PolyWeapon(uint id,byte weapon)=>Packet(b=>{b.Write((byte)119);b.Write(id);b.Write(weapon);});
    static void PolyFeed(WorldSession world,byte[] bytes)=>world.Receive(bytes,"offline");
    static WorldSession PolyWorld(){
        var world=new WorldSession();world.CacheEnabled=false;
        // Only supply identity; all appearance changes enter through the real packet dispatcher.
        Set(world,"Status",new OwnStatus(123,1,0,0,0,0,0,0,0,100,100,100,100,0,0,0,0,0,0,0,0,0,0));
        PolyFeed(world,PolyPack(123,0));PolyFeed(world,PolyPack(456));
        return world;
    }
    static void RejectPoly(WorldSession world,byte[] bytes,string label){
        var actor=world.Actor;var objects=world.Objects;int appearance=world.AppearanceRevision,revision=world.ObjectRevision,unknown=world.UnknownObjectUpdates;
        bool rejected=false;
        try{PolyFeed(world,bytes);}catch(ProtocolException){rejected=true;}
        Check(rejected,"POLY rejects "+label);
        Check(world.Actor==actor&&world.Objects.SequenceEqual(objects)&&world.AppearanceRevision==appearance&&world.ObjectRevision==revision&&world.UnknownObjectUpdates==unknown,"POLY rejection is atomic: "+label);
    }
    static void PolymorphChecks(){
        // The three ordinary Java constructors share CDHH; the fourth uses CDDHS.
        foreach(ushort gfx in new ushort[]{0,1,6894,ushort.MaxValue})
        foreach(ushort flag in new ushort[]{0,29}){
            var p=PolymorphPacket.Parse(Poly(0xfedcba98,gfx,flag));
            Check(p.ObjectId==0xfedcba98&&p.Gfx==gfx&&p.WeaponTakeoff==(flag==0)&&p.OwnerId==null,"POLY CDHH unsigned fields and takeoff "+gfx+"/"+flag);
        }
        foreach(string name in new[]{"A","測試角色","long-owner-name"})
        foreach(uint owner in new uint[]{123,0x001d1234,0xfedcba98}){
            var bytes=Disguise(456,owner,6894,name);var p=PolymorphPacket.Parse(bytes);
            Check(p.ObjectId==456&&p.OwnerId==owner&&p.Gfx==6894&&p.OwnerName==name&&!p.WeaponTakeoff,"POLY CDDHS owner/name including overlapping CDHH flag "+owner+"/"+name);
            if(name=="A")Check(bytes.Length==16,"POLY short disguise is also 16 bytes");
        }
        var w=PolyWorld();w.SelectTarget(456);int selectedRevision=w.TargetRevision;
        PolyFeed(w,PolyAction(123,1));PolyFeed(w,PolyAction(456,1));
        Check(w.OwnCombat?.Action==1&&w.Target?.Combat?.Action==1,"POLY fixture receives server attack actions without assets");
        int appearance=w.AppearanceRevision,speedRevision=w.SpeedRevision;
        var original=w.Actor!;
        PolyFeed(w,Poly(123,6894));
        Check(w.Actor==original with{Gfx=6894}&&!w.SelfAppearancePending&&w.AppearanceRevision==appearance+1,"POLY self updates current gfx and preserves weapon/position/stats/speed");
        Check(w.OwnCombat==null&&w.SpeedRevision==speedRevision,"POLY self retires old action without inventing speed update");
        PolyFeed(w,PolyAction(123,1));
        Check(w.OwnCombat is {Action:1,Predicted:false}&&w.Actor!.Gfx==6894,"POLY subsequent server attack uses current actor");
        PolyFeed(w,Poly(123,6894,0));
        Check(w.Actor is {Gfx:6894,Weapon:0}&&w.OwnCombat==null,"POLY same-gfx takeoff still clears old attack pose");
        PolyFeed(w,PolyWeapon(123,20));
        Check(w.Actor is {Gfx:6894,Weapon:20},"POLY subsequent S119 is authoritative for weapon pose");
        PolyFeed(w,Poly(123,0));
        Check(w.Actor is {Gfx:0,Weapon:20,Speed:2,Brave:1},"POLY undo uses server gfx zero and does not restore a guessed weapon or speed");

        PolyFeed(w,Move(456,32600,32940,2));var remote=w.Target!;
        PolyFeed(w,Poly(456,6894));
        Check(w.Target is {Gfx:6894,Action:4,MotionAt:0,Combat:null,AppearancePending:false}&&w.Target.Speed==remote.Speed&&w.Target.Brave==remote.Brave&&w.Target.MotionSpeed==remote.MotionSpeed&&w.Target.MotionBrave==remote.MotionBrave,"POLY remote retires motion but keeps authoritative speed");
        Check(w.TargetId==456&&w.TargetRevision==selectedRevision+1,"POLY preserves target identity and invalidates queued old-target actions");
        PolyFeed(w,PolyAction(456,1));
        Check(w.Target!.Combat is {Action:1,Predicted:false},"POLY remote accepts server attack action");
        PolyFeed(w,Poly(456,65535,0));PolyFeed(w,PolyWeapon(456,50));
        Check(w.Target is {Gfx:65535,Action:50,Combat:null},"POLY accepts unknown gfx without class-based guess; S119 wins");
        PolyFeed(w,Poly(456,1110));
        Check(w.Target is {Gfx:1110,Action:50},"POLY remote undo uses supplied original gfx");

        var beforeOwner=w.Actor;
        PolyFeed(w,Disguise(456,123,7777,"測試角色"));
        Check(w.Target is {ObjectId:456,Gfx:7777,Action:50}&&w.Actor==beforeOwner,"POLY disguise changes NPC, never owner identity or owner appearance");
        PolyFeed(w,PolyPack(456,2222,46));
        Check(w.Target is {Gfx:2222,Action:46,AppearancePending:false},"POLY S87 replaces earlier override");
        PolyFeed(w,Packet(b=>{b.Write((byte)120);b.Write(456u);}));
        int unknown=w.UnknownObjectUpdates;
        PolyFeed(w,Poly(456,7777));
        Check(w.Objects.Length==0&&w.UnknownObjectUpdates==unknown+1,"POLY unknown/removed object is not resurrected or queued");
        PolyFeed(w,PolyPack(456,3333));
        Check(w.Objects.Single().Gfx==3333,"POLY reused object ID has no stale appearance");
        PolyFeed(w,PolyAction(456,8));PolyFeed(w,Poly(456,4444,0));
        Check(w.Objects.Single() is {Gfx:4444,Dead:true,ReceivedAction:8,Combat.Action:8},"POLY preserves remote death");
        PolyFeed(w,PolyAction(123,8));PolyFeed(w,Poly(123,5555));
        Check(w.OwnDead&&w.OwnCombat?.Action==8&&w.Actor!.Gfx==5555,"POLY preserves own death");

        w=PolyWorld();
        foreach(byte opcode in new byte[]{255,67}){
            PolyFeed(w,Packet(b=>{b.Write(opcode);b.Write(123u);b.Write((byte)2);b.Write((ushort)40);}));
            PolyFeed(w,Packet(b=>{b.Write(opcode);b.Write(456u);b.Write((byte)2);b.Write((ushort)40);}));
        }
        PolyFeed(w,Poly(123,6894));PolyFeed(w,Poly(456,6894));
        Check(w.Actor is {Speed:2,Brave:2}&&w.Objects.Single() is {Speed:2,Brave:2},"POLY retains S255/S67 modes including unsupported modes verbatim");
        PolyFeed(w,PolyPack(123,1,46,1,0));
        Check(w.Actor is {Gfx:1,Weapon:46,Speed:1,Brave:0},"POLY own S87 replaces complete appearance and speed");

        var good=Poly(456,6894);
        for(int length=1;length<good.Length;length++)RejectPoly(w,good[..length],"truncated CDHH "+length);
        RejectPoly(w,Poly(0,6894),"zero object ID");
        RejectPoly(w,Poly(456,6894,1),"unknown weapon flag");
        var padding=(byte[])good.Clone();padding[15]=1;RejectPoly(w,padding,"nonzero padding");
        RejectPoly(w,[..good,..new byte[8]],"extra block");
        RejectPoly(w,Disguise(456,0,6894,"A"),"zero owner ID");
        RejectPoly(w,Disguise(456,123,6894,""),"empty owner name");
        var unterminated=Disguise(456,123,6894,"A");Array.Fill(unterminated,(byte)'A',11,unterminated.Length-11);RejectPoly(w,unterminated,"unterminated owner name");
        var longDisguise=Disguise(456,123,6894,"long-owner-name");
        RejectPoly(w,longDisguise[..16],"truncated CDDHS");

        var before=w.Objects.Single();var ownBefore=w.Actor;
        var spoly=Poly(456,9999);spoly[0]=230;PolyFeed(w,spoly);
        Check(w.Objects.Single()==before&&w.Actor==ownBefore,"SPOLY is not a verified S76 alias and cannot change appearance");
        PolyFeed(w,Packet(b=>{b.Write((byte)206);b.Write((ushort)4);b.Write((byte)0);}));
        Check(w.Actor==null&&w.Objects.Length==0,"POLY world reset has no separate override cache");
        PolyFeed(w,Poly(456,6894));PolyFeed(w,PolyPack(456,777));
        Check(w.Objects.Single().Gfx==777,"POLY packet before full pack is not replayed across world epochs");
        w.Reset();PolyFeed(w,PolyPack(456,888));
        Check(w.Objects.Single().Gfx==888,"POLY session reset has no stale state");
    }

    // Separate process/group: PlayerProfiles is a process-wide lazy cache. These are authored
    // timing numbers, not copied game metadata, pixels or claims about any real sprite.
    static void PolymorphCadenceChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-263","synthetic-cadence");
        string assets=Path.Combine(root,"assets");Directory.CreateDirectory(Path.Combine(assets,"players"));
        var profiles=new List<object>();
        foreach(int gfx in new[]{0,6894})
        foreach(int weapon in new[]{0,4,20,50}){
            int ms=(gfx==0?640:960)+weapon;
            var actions=new{walk=ms,attack=ms,spellDir=ms,spellNoDir=ms};
            byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(new{
                actionMs=actions,
                sequences=new[]{"attack","spellDir","spellNoDir"}.Select(action=>new{
                    action,direction=0,sequence=new[]{new{milliseconds=ms,events="!"}}
                })
            });
            string path=$"players/synthetic-{gfx}-{weapon}.json";File.WriteAllBytes(Path.Combine(assets,path),metadata);
            profiles.Add(new{gfx,weapon,path,sha256=Convert.ToHexString(SHA256.HashData(metadata)),stepMs=ms,serverActionMs=actions,castReleaseMs=new{spellDir=ms},projectileReleaseMs=ms});
        }
        byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(new{schema="p24-player-profiles-v1",profiles});
        File.WriteAllBytes(Path.Combine(assets,"players/manifest.json"),manifest);
        File.WriteAllBytes(Path.Combine(assets,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{
            files=new[]{new{path="players/manifest.json",size=manifest.Length,sha256=Convert.ToHexString(SHA256.HashData(manifest))}}
        }));
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(root,"build","offline-checks")+Path.DirectorySeparatorChar);
        foreach(byte speed in new byte[]{0,1,2}){
            var w=PolyWorld();
            PolyFeed(w,PolyPack(123,0,4,speed,0));PolyFeed(w,PolyPack(456,0,4,speed,0));
            PolyFeed(w,PolyAction(123,1));var old=w.OwnCombat;
            Check(old?.Timing is {Gfx:0,Weapon:4,ServerBaseMs:644},"POLY cadence starts from old authoritative appearance "+speed);
            PolyFeed(w,Poly(123,6894));PolyFeed(w,Poly(456,6894));
            Check(w.OwnCombat==null,"POLY old cadence retired "+speed);
            foreach(uint id in new uint[]{123,456}){
                // S30 melee report, with zero coordinate/reserved bytes as required by its existing parser.
                PolyFeed(w,Packet(b=>{b.Write((byte)30);b.Write((byte)1);b.Write(id);b.Write(0u);b.Write((ushort)0);b.Write((byte)5);b.Write((ushort)0);b.Write((ushort)0);b.Write((byte)0);}));
                var attack=id==123?w.OwnCombat:w.Objects.Single().Combat;
                Check(attack?.Timing is {Gfx:6894,Weapon:4,ServerBaseMs:964},"POLY S30 captures new gfx/weapon cadence "+id+"/"+speed);
                int expected=speed==0?964:speed==1?723:1928;
                Check(attack!.Timing!.AnimationMs==expected,"POLY attack follows existing authoritative speed mode "+id+"/"+speed);
            }
            Check(w.MovementAnimationMs==(speed==0?964:speed==1?723:1928),"POLY movement resolves same current gfx and server speed "+speed);
            PolyFeed(w,Poly(123,6894,0));PolyFeed(w,PolyAction(123,1));
            Check(w.OwnCombat?.Timing is {Gfx:6894,Weapon:0,ServerBaseMs:960},"POLY takeoff changes following attack pose "+speed);
            PolyFeed(w,PolyWeapon(123,50));PolyFeed(w,PolyAction(123,1));
            Check(w.OwnCombat?.Timing is {Gfx:6894,Weapon:50,ServerBaseMs:1010},"POLY S119 changes following attack pose "+speed);
            PolyFeed(w,Poly(123,0));PolyFeed(w,PolyAction(123,1));
            Check(w.OwnCombat?.Timing is {Gfx:0,Weapon:50,ServerBaseMs:690},"POLY undo selects original gfx cadence with current server weapon "+speed);
            PolyFeed(w,Poly(123,6894));
            PolyFeed(w,Packet(b=>{b.Write((byte)255);b.Write(123u);b.Write((byte)2);b.Write((ushort)10);}));
            PolyFeed(w,PolyAction(123,1));
            Check(w.OwnCombat?.Timing is {Gfx:6894,Slow:true,AnimationMs:2020},"POLY later S255 determines next attack speed "+speed);
        }
    }
}
