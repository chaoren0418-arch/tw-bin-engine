using P06;
using System.Reflection;
using System.Runtime.CompilerServices;

partial class Checks {
    static byte[] Poison(uint id,byte green,byte gray)=>Packet(b=>{b.Write((byte)165);b.Write(id);b.Write(green);b.Write(gray);});
    static byte[] Paralysis(byte mode)=>LoginPackets.Pad([202,mode]);
    static byte[] Blind(ushort mode)=>Packet(b=>{b.Write((byte)47);b.Write(mode);});
    static void AfflictionReject(Action action,string label){
        try{action();}catch(ProtocolException){Check(true,label);return;}
        throw new Exception("accepted invalid affliction packet: "+label);
    }
    static async Task AfflictionChecks(){
        CombatAdmissionFixture(); // Authored metadata, never original game assets or sockets.
        using(var c=Seed()){
            Check(Convert.ToHexString(Poison(0x01020304,1,0))=="A504030201010000","S165 little endian DCC vector");
            Check(Convert.ToHexString(Blind(2))=="2F02000000000000","S47 H vector");
            foreach(uint id in new uint[]{123,456})foreach(var entry in new[]{(0,0,AfflictionTint.Normal),(1,0,AfflictionTint.Poison),(0,1,AfflictionTint.Gray),(0,0,AfflictionTint.Normal)}){
                Feed(c,Poison(id,(byte)entry.Item1,(byte)entry.Item2));
                Check(c.World.TintFor(id)==entry.Item3,"S165 self/other apply/clear "+id+entry.Item3);
            }
            Feed(c,Poison(999,1,0));Feed(c,Pack(999));Check(c.World.TintFor(999)==AfflictionTint.Normal,"unknown ID does not contaminate later spawn");
            Feed(c,Poison(456,0,1));Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Feed(c,Pack(456));
            Check(c.World.TintFor(456)==AfflictionTint.Normal,"removed ID reuse has no stale tint");
            var pack=Pack(456);int nameEnd=Array.IndexOf(pack,(byte)0,21);int titleEnd=Array.IndexOf(pack,(byte)0,nameEnd+1);pack[titleEnd+1]=1;Feed(c,pack);
            Check(c.World.TintFor(456)==AfflictionTint.Poison,"S87 initial poison flag");
            Feed(c,Poison(456,0,0));Check(c.World.TintFor(456)==AfflictionTint.Normal,"S165 clear overrides old S87 flag");
            Feed(c,Poison(123,1,0));Feed(c,Blind(2));Feed(c,Paralysis(10));
            Feed(c,LoginPackets.Pad([206,0,0,0]));Feed(c,Pack(123));
            Check(c.World.BlindMode==2&&c.World.TintFor(123)==AfflictionTint.Poison&&c.World.AfflictionInputLocked,"map change preserves self afflictions");
            Feed(c,Pack(456));Check(c.World.TintFor(456)==AfflictionTint.Normal,"map change discards remote tints");
            c.World.Reset();Check(c.World.BlindMode==0&&!c.World.AfflictionInputLocked&&c.World.TintFor(123)==AfflictionTint.Normal,"reset clears all afflictions immediately");
        }
        foreach(byte on in new byte[]{2,4,10,12,22,24}){
            using var c=Seed();int sends=0;
            Sender(c,(_,valid,admit)=>{if(!valid())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);});
            Feed(c,Paralysis(on));Feed(c,Paralysis(on));
            Check(c.World.AfflictionInputLocked&&!c.MovementAvailable&&!c.CombatAvailable,"S202 lock and duplicate "+on);
            Check(!await c.Move(0)&&!await c.Turn(0)&&!await c.Attack()&&sends==0,"locked keyboard/web API sends zero C29/C225/C229 "+on);
            var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(!await pointer.ActAsync(456,false)&&sends==0,"locked pointer attack denied "+on);
            var form=KeyboardForm(c);form.Input.Down(Keys.Up);Call(form,"OnlineDown",Keys.G);
            CheckKeyboardRefusal(form,"異常狀態","affliction keyboard "+on);
            Feed(c,Paralysis((byte)(on+1)));Feed(c,Paralysis((byte)(on+1)));
            Check(!c.World.AfflictionInputLocked&&c.MovementAvailable&&c.CombatAvailable,"S202 matching and duplicate release "+on);
            Check(sends==0,"release does not auto-send "+on);
            Check(await c.Attack()&&sends==1,"fresh action after release accepted "+on);
        }
        using(var c=Seed()){
            Feed(c,Paralysis(10));Feed(c,Paralysis(12));Feed(c,Paralysis(11));
            Check(c.World.AfflictionInputLocked,"sleep release retains overlapping freeze");
            Feed(c,Paralysis(6));Feed(c,[..Paralysis(7),..new byte[8]]);
            Check(c.World.AfflictionInputLocked,"S202/7 recovery wrapper cannot clear freeze");
            Feed(c,Paralysis(13));Check(!c.World.ActionLocked,"all corresponding releases unlock");
            Feed(c,Paralysis(6));Check(c.World.ActionLocked&&!c.World.AfflictionInputLocked,"teleport lock remains distinct");
            Feed(c,Paralysis(7));Check(!c.World.ActionLocked,"normal teleport release");
            Feed(c,TeleportPacket());Feed(c,[..Paralysis(7),..new byte[8]]);
            Check(c.World.TeleportState=="recovery"&&c.World.ActionLocked,"S202/7 pending teleport enters recovery without unlocking world");
            Feed(c,MapPacket(0));Feed(c,Pack(123));Check(!c.World.ActionLocked,"recovery map and own pack restore readiness");
            Feed(c,Paralysis(2));Feed(c,Paralysis(255));Check(c.World.AfflictionInputLocked,"unknown mode never releases existing lock");
        }
        foreach(bool unlockBeforeGate in new[]{false,true})foreach(bool attack in new[]{false,true}){
            using var c=Seed();int sends=0;
            Sender(c,(_,valid,admit)=>{
                Feed(c,Paralysis(22));if(unlockBeforeGate)Feed(c,Paralysis(23));
                if(!valid())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);
            });
            Check(!(attack?await c.Attack():await c.Turn(0))&&sends==0,"queued action invalidated even across rapid lock/unlock "+attack+unlockBeforeGate);
        }
        using(var c=Seed()){
            foreach(ushort mode in new ushort[]{1,2,0}){Feed(c,Blind(mode));Check(c.World.BlindMode==mode&&c.MovementAvailable,"S47 mode does not lock input "+mode);}
            Feed(c,Poison(123,1,0));Feed(c,Blind(2));Feed(c,Paralysis(12));
            foreach(var bytes in new[]{Poison(123,0,0),Paralysis(13),Blind(0)}){
                for(int length=0;length<bytes.Length;length++)AfflictionReject(()=>Feed(c,bytes[..length]),"truncated packet "+bytes[0]+"/"+length);
                var dirty=bytes.ToArray();dirty[^1]=1;AfflictionReject(()=>Feed(c,dirty),"nonzero padding "+bytes[0]);
                AfflictionReject(()=>Feed(c,[..bytes,..new byte[8]]),"extra block rejected except S202/7 "+bytes[0]);
            }
            foreach(var bytes in new[]{Poison(123,1,1),Poison(123,2,0),Poison(0,1,0),Blind(3),Blind(256)})AfflictionReject(()=>Feed(c,bytes),"invalid field "+Convert.ToHexString(bytes));
            Check(c.World.BlindMode==2&&c.World.AfflictionInputLocked&&c.World.TintFor(123)==AfflictionTint.Poison,"malformed packets are atomic");
        }
        AfflictionRendering();
    }

    static void AfflictionRendering(){
        using var c=Seed();
        var assets=(Assets)RuntimeHelpers.GetUninitializedObject(typeof(Assets));
        Set(assets,"<CanvasWidth>k__BackingField",96);Set(assets,"<CanvasHeight>k__BackingField",800);
        var tile=new Tile(0,[new(0,0,0x7fff),new(1,0,0x7c00),new(2,0,0)]);
        var terrain=new Draw(0,0,0,0,"ground",0,0,0,tile);
        var far=terrain with{X=4,Serial=4};
        Set(assets,"<Scene>k__BackingField",new List<Draw>{terrain,far});
        using var renderer=new Renderer(assets);
        var own=terrain with{Layer="actor",Z=1,Serial=1};
        var other=own with{X=2,ObjectId=456};
        var shadow=own with{X=1,Layer="shadow"};
        var commands=new List<Draw>{terrain,own,other,shadow};
        var area=new Rectangle(0,740,48,40);
        Patch Compose()=> (Patch)typeof(Renderer).GetMethod("ComposeAfflictionPatch",F)!.Invoke(renderer,[c.World,area,commands,null])!;
        ushort At(Patch p,Draw draw,int dx=0)=>p.Colors[(draw.Py-p.Bounds.Y)*p.Bounds.Width+draw.Px+dx-p.Bounds.X];
        var normal=Compose();Check(At(normal,own)==0x7fff&&At(normal,other)==0x7fff,"normal composition RGB555");
        Feed(c,Poison(123,1,0));Feed(c,Poison(456,0,1));var tinted=Compose();
        Check(At(tinted,own)==0x2bea&&At(tinted,other,1)==0x2529,"poison green and gray independent pixel vectors");
        Check(At(tinted,shadow)==At(normal,shadow)&&tile.Pixels[0].Color==0x7fff,"shadow and shared source pixels unchanged");
        Check(tinted.Mask.SequenceEqual(normal.Mask)&&tinted.EntityOwners!.SequenceEqual(normal.EntityOwners!)&&tinted.Owners.SequenceEqual(normal.Owners),"tint preserves sparse mask/depth/picking");
        var cover=own with{Layer="objects",Z=2,Serial=99};commands.Add(cover);var covered=Compose();
        Check(At(covered,own)==0x7fff,"opaque scenery occludes tinted own actor");commands.Remove(cover);
        Feed(c,Blind(2));var dim=Compose();Check(dim.Bounds==renderer.Bounds&&At(dim,far)==0x1ce7,"blind mode 2 darkens background outside actor patch");
        Check(At(dim,own)==AfflictionColors.Darken(At(tinted,own)),"blind mode 2 composes after poison");
        Feed(c,Blind(1));var blind=Compose();Check(At(blind,own)==At(tinted,own)&&At(blind,other)==0&&At(blind,far)==0,"blind mode 1 only own actor visible");
        Check(At(blind,own,2)==0&&blind.Mask[(own.Py-blind.Bounds.Y)*blind.Bounds.Width+own.Px+2],"black covered pixel remains opaque");
        Feed(c,Blind(0));Feed(c,Poison(123,0,0));Feed(c,Poison(456,0,0));var restored=Compose();
        Check(restored.Colors.SequenceEqual(normal.Colors)&&restored.Mask.SequenceEqual(normal.Mask),"release restores exact normal pixels");
    }
}
