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
    [CheckGroup("afflictions")]
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
        using(var c=Seed()){
            // Lifecycle hooks attach on first use, so each packet must arm reset on its own.
            Feed(c,Blind(2));c.World.Reset();Check(c.World.BlindMode==0,"S47 as first affliction packet clears on reset");
        }
        using(var c=Seed()){
            Feed(c,Poison(123,1,0));c.World.Reset();Check(c.World.TintFor(123)==AfflictionTint.Normal,"S165 as first affliction packet clears on reset");
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
            Check(c.World.BlindMode==2&&c.World.AfflictionInputLocked&&c.World.TintFor(123)==AfflictionTint.Poison,"malformed packets are atomic");
            // AM§4(a)(b): field values are never protocol faults. Gray outranks green; green >= 8 drops the packet.
            Feed(c,Poison(123,1,1));Check(c.World.TintFor(123)==AfflictionTint.Gray,"S165 green+gray shows gray without exception");
            Feed(c,Poison(123,7,0));Check(c.World.TintFor(123)==AfflictionTint.Poison,"S165 green 7 is nonzero green");
            Feed(c,Poison(123,0,255));Check(c.World.TintFor(123)==AfflictionTint.Gray,"S165 gray 255 is nonzero gray");
            Feed(c,Poison(123,7,9));Check(c.World.TintFor(123)==AfflictionTint.Gray,"S165 any green+gray shows gray");
            Feed(c,Poison(123,1,0));
            foreach(var (green,gray) in new (byte,byte)[]{(8,0),(8,1),(9,0),(255,255)}){
                Feed(c,Poison(123,green,gray));Check(c.World.TintFor(123)==AfflictionTint.Poison,$"S165 green {green} drops the whole packet ({gray})");
            }
            Feed(c,Poison(0,1,0));Feed(c,Poison(0,0,1));Check(c.World.TintFor(0)==AfflictionTint.Normal,"S165 zero identity ignored without exception");
            foreach(ushort mode in new ushort[]{3,255,256,65535}){Feed(c,Blind(mode));Check(c.World.BlindMode==2,"S47 mode above 2 ignored without exception "+mode);}
            Check(c.World.BlindMode==2&&c.World.AfflictionInputLocked&&c.World.TintFor(123)==AfflictionTint.Poison,"ignored boundary packets keep state");
        }
        foreach(bool wrapped in new[]{false,true}){
            // C_ItemUSe rejection answers with S202/7; it is the only release for a pending supply.
            using var c=Seed();
            Sender(c,(_,valid,admit)=>{if(!valid())return Task.FromResult(false);admit();return Task.FromResult(true);});
            SlowItem(c,"home");
            Check(await c.UseSelectedItem()&&c.World.BasicSupplyPending&&c.World.ActionLocked,"home scroll request stays pending "+wrapped);
            Feed(c,Paralysis(2));Check(c.World.BasicSupplyPending,"paralysis is not a supply reply "+wrapped);
            Feed(c,Paralysis(3));Check(c.World.BasicSupplyPending,"paralysis release is not a supply reply "+wrapped);
            Feed(c,wrapped?[..Paralysis(7),..new byte[8]]:Paralysis(7));
            Check(!c.World.BasicSupplyPending&&!c.World.ActionLocked,"S202/7 releases pending supply "+wrapped);
        }
        AfflictionColorVectors();
        AfflictionRendering();
    }

    static void AfflictionColorVectors(){
        // AM§4(a): poison adds k*8/16 to green only, k = 16+|12-(t%24)|, t = ms/50.
        int[] expected=[14,13,13,12,12,11,11,10,10,9,9,8,8,8,9,9,10,10,11,11,12,12,13,13];
        for(int t=0;t<24;t++){
            Check(AfflictionColors.PoisonPulse(t*50)==16+Math.Abs(12-t),"poison pulse k at t "+t);
            ushort black=AfflictionColors.Tint(0,AfflictionTint.Poison,t*50);
            Check(black==(ushort)(expected[t]<<5),$"poison adds green {expected[t]} at t {t}");
        }
        Check(AfflictionColors.Tint(0x7c00,AfflictionTint.Poison,0)==0x7dc0&&AfflictionColors.Tint(0x7c00,AfflictionTint.Poison,600)==0x7d00,"poison RGB555 vectors at peak and trough");
        Check(AfflictionColors.Tint(0x7c1f,AfflictionTint.Poison,49)==AfflictionColors.Tint(0x7c1f,AfflictionTint.Poison,1249)&&AfflictionColors.Tint(0x7c1f,AfflictionTint.Poison,0)==0x7ddf,"poison keeps R and B, 1.2 s period");
        Check(AfflictionColors.Tint(0x03e0,AfflictionTint.Poison,0)==0x03e0&&AfflictionColors.Tint(0x0300,AfflictionTint.Poison,0)==0x03e0,"poison green saturates at 31");
        // AM§4(a): paralysis gray is c/2 + (3,3,3) on every channel, not a luminance gray.
        Check(AfflictionColors.Tint(0x7fff,AfflictionTint.Gray,0)==0x4a52&&AfflictionColors.Tint(0,AfflictionTint.Gray,0)==0x0c63,"gray RGB555 vectors white/black");
        Check(AfflictionColors.Tint(0x7c00,AfflictionTint.Gray,0)==0x4863&&AfflictionColors.Tint(0x7c00,AfflictionTint.Gray,0)==AfflictionColors.Tint(0x7c00,AfflictionTint.Gray,600),"gray RGB555 red vector, no pulse");
        Check(AfflictionColors.Tint(0x1234,AfflictionTint.Normal,0)==0x1234,"normal tint unchanged");
        // Lighting table application c*f/31, blind coefficients from table levels 0 and 1.
        Check(AfflictionColors.Scale(0x7fff,4)==0x1084&&AfflictionColors.Scale(0x7fff,31)==0x7fff&&AfflictionColors.Scale(0x7fff,0)==0&&AfflictionColors.Scale(0x4210,10)==0x14a5,"lighting scale vectors");
        Check(AfflictionColors.Darken(0x7fff)==0x1084,"blind mode 2 unlit is 4/31");
        Check(AfflictionColors.BlindCoefficient(1,0)==0&&AfflictionColors.BlindCoefficient(1,17)==17&&AfflictionColors.BlindCoefficient(1,31)==31,"blind mode 1 coefficient is the light index");
        Check(AfflictionColors.BlindCoefficient(2,0)==4&&AfflictionColors.BlindCoefficient(2,31)==31&&AfflictionColors.BlindCoefficient(0,0)==31,"blind mode 2 coefficient spans 4..31");
        Check(AfflictionColors.BlindLight(0)==31&&AfflictionColors.BlindLight(AfflictionColors.BlindLightRadius)==0&&AfflictionColors.BlindLight(AfflictionColors.BlindLightRadius*2)==0,"own blind light disc full at center, none beyond k=5 radius");
        int previous=31;for(int d=0;d<=130;d+=5){int light=AfflictionColors.BlindLight(d);Check(light<=previous,"blind light falls off monotonically "+d);previous=light;}
    }

    static void AfflictionRendering(){
        using var c=Seed();
        var assets=(Assets)RuntimeHelpers.GetUninitializedObject(typeof(Assets));
        Set(assets,"<CanvasWidth>k__BackingField",96);Set(assets,"<CanvasHeight>k__BackingField",800);
        var tile=new Tile(0,[new(0,0,0x7fff),new(1,0,0x7c00),new(2,0,0)]);
        var terrain=new Draw(0,0,0,0,"ground",0,0,0,tile);
        var far=terrain with{X=4,Serial=4};
        var remote=terrain with{X=14,Y=-6,Serial=5}; // (24,600): beyond the own blind light radius.
        Set(assets,"<Scene>k__BackingField",new List<Draw>{terrain,far,remote});
        long clock=0;Set(c.World,"MagicStatusClock",(Func<long>)(()=>clock));
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
        Check(At(tinted,own,1)==0x7dc0&&At(tinted,own)==0x7fff&&At(tinted,other)==0x4a52&&At(tinted,other,1)==0x4863&&At(tinted,other,2)==0x0c63,"poison green and gray independent pixel vectors");
        clock=600;Check(At(Compose(),own,1)==0x7d00&&At(Compose(),other,1)==0x4863,"composition follows the poison pulse clock, gray steady");
        clock=1200;Check(At(Compose(),own,1)==0x7dc0,"poison pulse period 1.2 s in composition");clock=0;
        Check(At(tinted,shadow)==At(normal,shadow)&&tile.Pixels[0].Color==0x7fff,"shadow and shared source pixels unchanged");
        Check(tinted.Mask.SequenceEqual(normal.Mask)&&tinted.EntityOwners!.SequenceEqual(normal.EntityOwners!)&&tinted.Owners.SequenceEqual(normal.Owners),"tint preserves sparse mask/depth/picking");
        var cover=own with{Layer="objects",Z=2,Serial=99};commands.Add(cover);var covered=Compose();
        Check(At(covered,own)==0x7fff,"opaque scenery occludes tinted own actor");commands.Remove(cover);
        // Own light center is the own actor draw box (0,756)-(24,780); pixel centers are +0.5.
        double Distance(Draw d,int dx=0)=>Math.Sqrt(Math.Pow(d.Px+dx+.5-12,2)+Math.Pow(d.Py+.5-768,2));
        int Coefficient(ushort mode,Draw d,int dx=0)=>AfflictionColors.BlindCoefficient(mode,AfflictionColors.BlindLight(Distance(d,dx)));
        Feed(c,Blind(2));var dim=Compose();
        Check(dim.Bounds==renderer.Bounds&&At(dim,remote)==0x1084,"blind mode 2 unlit background outside actor patch is 4/31");
        int farLight=AfflictionColors.BlindLight(Distance(far));
        Check(farLight is >0 and <31&&At(dim,far)==AfflictionColors.Scale(0x7fff,Coefficient(2,far))&&At(dim,far)!=0x1084&&At(dim,far)!=0x7fff,"blind mode 2 own light disc partly lights nearby scenery");
        Check(At(dim,own,1)==At(tinted,own,1)&&At(dim,other)==At(tinted,other),"blind mode 2 own light keeps the center lit after poison/gray");
        Feed(c,Blind(1));var blind=Compose();
        Check(At(blind,remote)==0&&At(blind,remote,1)==0,"blind mode 1 unlit pixels are black");
        Check(At(blind,own,1)==At(tinted,own,1)&&At(blind,far)==AfflictionColors.Scale(0x7fff,farLight),"blind mode 1 keeps only the own k=5 light disc");
        Check(At(blind,other)==AfflictionColors.Scale(At(tinted,other),Coefficient(1,other)),"blind mode 1 lights a peer only through the own disc");
        Check(At(blind,own,2)==0x01c0&&At(blind,remote)==0&&blind.Mask[(remote.Py-blind.Bounds.Y)*blind.Bounds.Width+remote.Px],"black covered pixel remains opaque; lit black poisoned pixel shows the green add");
        Check(blind.Owners.SequenceEqual(dim.Owners)&&blind.EntityOwners!.SequenceEqual(dim.EntityOwners!),"blind light does not change depth/picking");
        Feed(c,Blind(0));Feed(c,Poison(123,0,0));Feed(c,Poison(456,0,0));var restored=Compose();
        Check(restored.Colors.SequenceEqual(normal.Colors)&&restored.Mask.SequenceEqual(normal.Mask),"release restores exact normal pixels");
        // S171 opacity must survive this entry point: it replaced the composition loop that applied it.
        Feed(c,Packet(b=>{b.Write((byte)171);b.Write(456u);b.Write((byte)1);}));var gone=Compose();
        int otherIndex=(other.Py-gone.Bounds.Y)*gone.Bounds.Width+other.Px-gone.Bounds.X;
        Check(!gone.Mask[otherIndex]&&gone.EntityOwners![otherIndex]==null&&!gone.VisibleObjectIds.Contains(456),"invisible peer is not drawn or pickable through affliction composition");
        Feed(c,Packet(b=>{b.Write((byte)171);b.Write(456u);b.Write((byte)0);}));
        Check(Compose().Colors.SequenceEqual(normal.Colors),"S171 clear restores composition");
    }
}
