using P06;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

// All fixture archives below contain invented one-pixel sprites, never game assets.
static partial class AttackSkillChecks
{
    public static void NativeResources(string client,string assets,Action<bool,string> report){
        var catalog=new EffectSpriteCatalog(client);
        foreach(int gfx in new[]{66,167,180,193,214,219,221,2349,2989,11722}){
            var effect=catalog.Get(gfx);
            if(gfx==221)report(effect.TileType==18,"runtime attr8 screen blend matches prepared shield asset");
            for(int d=0;d<8;d++){
                long age=0;
                for(int i=1;i<=effect.Steps.Length;i++){
                    var frame=catalog.Frame(effect,d,age);report(frame.Blocks.Length>0||frame.Bounds.SequenceEqual(new[]{192,120,-128,-120}),$"runtime original gfx={gfx} direction={d} frame={frame.Key} (including native empty frame)");
                    if(gfx is 167 or 219 or 221 or 11722){
                        var known=new ObjectAppearance(Path.Combine(assets,$"objects/magic-{gfx}.json"));var expected=known.Frames[frame.Key];
                        report(frame.Bounds.SequenceEqual(expected.Bounds)&&frame.Blocks.Length==expected.Blocks.Length&&frame.Blocks.Zip(expected.Blocks).All(p=>p.First.A==p.Second.A&&p.First.B==p.Second.B&&p.First.Z==p.Second.Z&&p.First.Tile.Pixels.SequenceEqual(p.Second.Tile.Pixels)),"runtime SPR matches existing asset pixels "+gfx+"/"+frame.Key);
                    }
                    age+=effect.Steps[i%effect.Steps.Length].Milliseconds;
                }
            }
        }
    }
    const BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
    static byte[] Bytes(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return s.ToArray();}
    static byte[] Sprite(ushort color,bool indexed=true)=>Bytes(b=>{
        if(indexed){b.Write((byte)255);b.Write((byte)2);b.Write(color);b.Write((ushort)(color+1));}
        b.Write((byte)2);
        for(int i=0;i<2;i++){
            for(int j=0;j<4;j++)b.Write((short)0);
            b.Write((ushort)0);b.Write((ushort)0);b.Write((short)1);
            b.Write((sbyte)0);b.Write((sbyte)0);b.Write((byte)(7+i));b.Write((ushort)i);
        }
        b.Write(2u);b.Write(0u);b.Write(indexed?8u:9u);b.Write(18u);
        for(int i=0;i<2;i++){
            b.Write(new byte[]{0,0,1,1,1,0,1});
            if(indexed)b.Write((byte)i);else b.Write((ushort)(color+i));
        }
    });
    static byte[] EncryptText(byte[] data){
        using var des=DES.Create();des.Key=Convert.FromHexString("7E214023255E243C");var result=(byte[])data.Clone();
        des.EncryptEcb(data.AsSpan(0,data.Length/8*8),PaddingMode.None).CopyTo(result,0);return result;
    }
    static void Archive(string root,string name,Dictionary<string,byte[]> entries,bool encrypted=false){
        using var pak=new MemoryStream();var index=Bytes(b=>{
            b.Write(entries.Count);
            foreach(var (file,raw) in entries){var data=encrypted?EncryptText(raw):raw;
                b.Write((uint)pak.Position);var key=new byte[20];Encoding.ASCII.GetBytes(file).CopyTo(key,0);b.Write(key);b.Write(data.Length);pak.Write(data);
            }
        });
        if(encrypted)EncryptText(index[4..]).CopyTo(index,4);
        File.WriteAllBytes(Path.Combine(root,name+".idx"),index);File.WriteAllBytes(Path.Combine(root,name+".pak"),pak.ToArray());
    }
    static Renderer BareRenderer(string root){
        var renderer=(Renderer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Renderer));
        foreach(string name in new[]{"magicFlights","projectileFlights","projectileHits","effectResourceErrors","magicAppearances","projectileFrames"}){
            var f=typeof(Renderer).GetField(name,Private)!;f.SetValue(renderer,Activator.CreateInstance(f.FieldType));
        }
        typeof(Renderer).GetField("effectSprites",Private)!.SetValue(renderer,new EffectSpriteCatalog(root));
        typeof(Renderer).GetField("magicEpoch",Private)!.SetValue(renderer,-1);
        typeof(Renderer).GetField("projectileEpoch",Private)!.SetValue(renderer,-1);
        return renderer;
    }
    static List<Draw> Compose(Renderer renderer,WorldSession world,long now){
        var draws=new List<Draw>();
        foreach(string method in new[]{"ComposeProjectile","ComposeMagic"})typeof(Renderer).GetMethod(method,Private)!.Invoke(renderer,[world,draws,now]);
        return draws;
    }
    static void Rendering(string root){
        string client=Path.Combine(root,"client");Directory.CreateDirectory(client);
        string metadata="S12235 0 0\n";
        foreach(int gfx in new[]{40000,40001,40002,40003,40004})
            metadata+=$"#{gfx} 8{(gfx==40004?"=40000":"")} synthetic\n0.effect(1 2,0.1:2 0.0:1)"+
                (gfx==40002?" 109.effect(3 40001)":gfx==40003?" 109.effect(4 40001)":"")+"\n";
        var clear=Encoding.ASCII.GetBytes(metadata);var packed=(byte[])clear.Clone();
        using(var aes=Aes.Create()){
            aes.Key=Convert.FromHexString("DC8401212A40200ADD25B9A70DB9C94E");var iv=Convert.FromHexString("3E0978AAC4D53063300C5F9A807F2246");int n=(clear.Length-5)/16*16;
            aes.EncryptCbc(clear.AsSpan(5,n),iv,PaddingMode.None).CopyTo(packed,5);
            for(int i=5+n;i<clear.Length;i++)packed[i]=(byte)(clear[i]^packed[i-16]);
        }
        Archive(client,"Text",new(){["list.spz"]=packed},true);
        var sprites=new Dictionary<string,byte[]>();
        foreach(int gfx in new[]{40000,40001,40002,40003})for(int d=0;d<8;d++)sprites[$"{gfx}-{d}.spr"]=Sprite((ushort)(100+(gfx-40000)*100+d*2),gfx!=40002);
        Archive(client,"Sprite",sprites);
        var catalog=new EffectSpriteCatalog(client);
        foreach(var (flags,tile) in new[]{(8,18),(128,34),(256,6),(0,2)})
            Check(EffectSprite.Parse(1,$"#1 1 fixture 0.effect(0 1,0.0:1) 104.attr({flags})").TileType==tile,"native blend flags "+flags);
        for(int d=0;d<8;d++){
            var effect=catalog.Get(40000);var a=catalog.Frame(effect,d,0);var b=catalog.Frame(effect,d,41);
            Check(a.Key==$"{d}:0"&&b.Key==$"{d}:1"&&a.Blocks[0].Z==7&&b.Blocks[0].Z==8,"native sequence increment, direction, duration and depth "+d);
            Check(a.Blocks.Single().Tile.Pixels.Single().Color==100+d*2,"native gfx pixels "+d);
            Check(catalog.Frame(catalog.Get(40004),d,0)==a,"metadata sprite alias "+d);
        }
        Check(catalog.Frame(catalog.Get(40002),0,0).Blocks[0].Tile.Pixels[0].Color==300,"RGB555 unindexed sprite");
        foreach(var bad in new[]{Sprite(10)[..^1],Sprite(10).Concat(new byte[]{0}).ToArray(),new byte[]{255}}){
            bool rejected=false;try{EffectSpriteCatalog.Decode(bad,0,2);}catch(Exception ex)when(ex is IOException or InvalidDataException){rejected=true;}Check(rejected,"malformed sprite rejected");
        }
        var world=Seed();var renderer=BareRenderer(client);
        world.Receive(Attack(21,40002,127),"offline");world.Receive(Attack(19,40000,127),"offline");long at=world.Projectiles.Max(p=>p.At);
        var draws=Compose(renderer,world,at);
        Check(renderer.ProjectileVisuals.Length==2&&renderer.ProjectileVisuals.Select(v=>v.Gfx).Order().SequenceEqual(new[]{40000,40002}),"two different arrow gfx actually compose");
        Check(draws.SelectMany(d=>d.Tile.Pixels).Select(p=>p.Color).Distinct().Count()==2,"different arrows retain different original pixels");
        var command=draws[0];var colors=new ushort[24*24];var mask=new bool[colors.Length];Renderer.Blit(command,new(command.Px,command.Py,24,24),colors,mask,null);
        Check(mask.Count(x=>x)==1&&colors[0]==command.Tile.Pixels[0].Color,"compositor blits original sparse pixel");
        Compose(renderer,world,at+2000);Check(renderer.ProjectileVisuals.Length==0,"arrow flight expires");
        world=Seed();renderer=BareRenderer(client);world.Receive(Attack(255,40003,caster:0),"offline");at=world.MagicEffects[0].At;
        Check(Compose(renderer,world,at).Count>0&&renderer.MagicVisuals.Single().Phase=="fly","caster0 spell flies without actor animation");
        Compose(renderer,world,at+120);Check(renderer.MagicVisuals.Single().Gfx==40001&&renderer.MagicVisuals.Single().Phase=="impact","native spell impact gfx used");
        Compose(renderer,world,at+6000);Check(renderer.MagicVisuals.Length==0,"spell impact expires");
        foreach(byte type in new byte[]{0,8}){
            world=Seed();renderer=BareRenderer(client);var packet=Range([(456,32,70000),(457,0,0)],true,type);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(15),40000);world.Receive(packet,"offline");at=world.MagicEffects[0].At;
            draws=Compose(renderer,world,at);
            Check(draws.Count==1&&renderer.MagicVisuals.All(v=>v.Gfx==40000),"S42 type/multiple targets reach one actual draw "+type);
            Check(renderer.MagicVisuals.All(v=>v.X==32600&&v.Direction==6),"S42 origin (packet x,y) and heading "+type);
            world.Reset();Compose(renderer,world,at);Check(renderer.MagicVisuals.Length==0,"epoch clears render effects");
        }
        world=Seed();renderer=BareRenderer(client);world.Receive(Attack(gfx:65535),"offline");
        Check(Compose(renderer,world,world.MagicEffects[0].At).Count==0&&renderer.EffectResourceErrors.ContainsKey(65535),"missing gfx diagnosed without substitution or crash");
    }
}
