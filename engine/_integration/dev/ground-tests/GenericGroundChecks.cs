using P06;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

partial class Checks {
    static byte[] GroundPacket(WorldObject o)=>Packet(b=>{
        b.Write((byte)87);b.Write(o.X);b.Write(o.Y);b.Write(o.ObjectId);b.Write(o.Gfx);
        b.Write(o.Action);b.Write(o.Direction);b.Write(o.Light);b.Write(o.Speed);b.Write(o.WireExp);b.Write(o.Lawful);
        Text(b,o.Name);Text(b,o.Title);b.Write(o.Flags);b.Write(o.ClanId);Text(b,o.Clan);Text(b,o.Owner);
        b.Write(o.Rank);b.Write(o.HpByte);b.Write(o.Brave);b.Write(o.Level);b.Write(o.Unknown);b.Write(o.Tail1);b.Write(o.Tail2);
    });
    static async Task GenericGroundChecks(GroundTestItem gold){
        Check(GroundCatalog.Find(-1)==null&&GroundCatalog.Find(65536)==null,"gfx bounds");
        foreach(ushort gfx in new ushort[]{22,42,46,3247,999,65535}){
            using var c=Seed(gold);var o=c.World.SelectedGround!.Envelope with{Gfx=gfx,Name="$99999 [7]",Light=8};
            Feed(c,GroundPacket(o));Check(c.World.SelectedGround?.Gfx==gfx&&c.World.Objects.Length==0,"generic ground separate from actors");
            c.World.SelectTarget(o.ObjectId);Check(c.World.TargetId==null,"generic ground cannot attack");
            c.World.SelectGround(o.ObjectId);int sends=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();sends++;
                Check(b.SequenceEqual(GroundRules.Request(112,o.X,o.Y,o.ObjectId,17)),"generic C112 exact object/position/count");return Task.FromResult(true);});
            Check(await Send(c,true,"17")&&sends==1&&c.World.SelectedGround!.Count==250,"generic pickup without local deduction");
            Feed(c,GroundPacket(o with{WireExp=233,Name=""}));Check(c.World.SelectedGround?.Count==233,"empty wand name accepted");
            Feed(c,GroundPacket(o with{WireExp=2000000000,Name="$4 (3000000000)"}));Check(c.World.SelectedGround?.Count==2000000000,"wire count capped independently of display name");
            Feed(c,GroundPacket(o with{WireExp=0}));Check(c.World.Ground.Length==0&&c.World.SelectedGroundId==null,"generic zero clears selection");
            Feed(c,GroundPacket(o));c.World.SelectGround(o.ObjectId);
            Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(o.ObjectId)]));Check(c.World.Ground.Length==0&&c.World.SelectedGroundId==null,"generic S120 removes");
            Feed(c,GroundPacket(o));c.Cancel();Check(c.World.Ground.Length==0,"generic disconnect clears");
        }
        using(var c=Seed(gold)){
            var o=c.World.SelectedGround!.Envelope with{Gfx=42,Name="lit ($10)",Light=15};
            Feed(c,GroundPacket(o));string before=JsonSerializer.Serialize(c.World.Ground);
            var packet=GroundPacket(o);
            // Every prefix shorter than the semantic S87 body must fail atomically.
            for(int length=0;length<packet.Length-7;length++){
                bool refused=false;try{Feed(c,packet[..length]);}catch(TargetInvocationException ex) when(ex.InnerException is ProtocolException){refused=true;}
                Check(refused&&JsonSerializer.Serialize(c.World.Ground)==before,"truncated generic packet atomic "+length);
            }
            foreach(var bad in new[]{o with{Action=1},o with{Direction=1},o with{Speed=1},o with{WireExp=2000000001},o with{Lawful=1},o with{Title="NPC"},o with{Flags=4},o with{ClanId=1},o with{Clan="clan"},o with{Owner="owner"},o with{Rank=1},o with{HpByte=100},o with{Brave=1},o with{Level=1},o with{Unknown=1},o with{Tail1=0},o with{Tail2=0}}){
                Check(!GroundRules.IsGround(bad),"reject nonitem fixed field");Feed(c,GroundPacket(bad));
                Check(c.World.Ground.Length==0&&c.World.Objects.Single().ObjectId==o.ObjectId,"ground to actor replacement");
                c.World.SelectTarget(o.ObjectId);Feed(c,GroundPacket(o));Check(c.World.Objects.Length==0&&c.World.TargetId==null,"actor to ground replacement clears target");
            }
            c.World.SelectGround(o.ObjectId);Feed(c,LoginPackets.Pad([206,4,0,0]));
            Check(c.World.Ground.Length==0&&c.World.SelectedGroundId==null,"map epoch clears generic ground");
        }
        using(var c=Seed(gold)){
            var o=c.World.SelectedGround!.Envelope with{Gfx=46};Feed(c,GroundPacket(o));
            var selection=c.World.GroundQuantity(true);var gate=new TaskCompletionSource();int sends=0;
            Sender(c,async(b,v,a)=>{await gate.Task;if(!v())return false;a();sends++;return true;});
            var pending=Send(c,true,"1",selection);
            Feed(c,GroundPacket(o with{WireExp=249}));Feed(c,GroundPacket(o));gate.SetResult();
            Check(!await pending&&sends==0,"generic count ABA invalidates queued pickup");
        }
        cases.Add("通用 S87：任意 gfx／名稱／亮度、C112、零量／S120／換圖／斷線、分類切換、截斷原子性與 ABA");
    }

    static void RuntimeSprites(string[] args){
        SyntheticSpriteChecks(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"synthetic-spr"));
        string root=args.Length>2?args[2]:GroundSpriteReader.ClientRoot;
        var hashes=new Dictionary<ushort,string>{
            [22]="b2ef7b8643b4e92ef5a2613d1b0544b5de46fbf3b96344f2f246dad0fe48ff08",
            [42]="8f5069d7cc9a7f3ec9ec63d51ebf3b1d5d1ebbfbe65481c2a2141ea5f7b494d8",
            [46]="d9d2efc4a593b83582917a154a707d0e12817133befa09a2d024b9b910003802",
            [3247]="016b589e7f8b09054c8219d16d0a566c7658bb1c92bb28dc69d7007b916181a5"
        };
        foreach(ushort gfx in new ushort[]{22,42,46,3247}){
            var row=GroundCatalog.Find(gfx)!;var frame=GroundSpriteReader.Read(row,root);
            Check(frame.Blocks.Length>0,"runtime frame "+gfx);
            using var bitmap=GroundSpriteReader.Image(frame);var rgba=new List<byte>();
            for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){var p=bitmap.GetPixel(x,y);rgba.AddRange([p.R,p.G,p.B,p.A]);}
            string sha=Convert.ToHexString(SHA256.HashData(rgba.ToArray())).ToLowerInvariant();
            if(hashes.TryGetValue(gfx,out var expected))Check(sha==expected,"independent RGBA pin "+gfx);
            Console.WriteLine($"gfx {gfx}: bounds [{string.Join(',',frame.Bounds)}] RGBA {sha}");
            Check(new GroundAppearance("unused",gfx).Frame.Bounds.SequenceEqual(frame.Bounds),"production appearance runtime lookup "+gfx);
            if(args.Length>3){
                var assets=new Assets(Path.Combine(args[3],"assets"));using var renderer=new Renderer(assets);
                using var c=Seed(Synthetic(gfx));var draws=new List<Draw>();var visuals=new List<ObjectVisual>();
                typeof(Renderer).GetMethod("ComposeGround",F)!.Invoke(renderer,[c.World,new ObjectAssets(assets.Root),draws,visuals]);
                Check(visuals.Single().Gfx==gfx&&draws.All(d=>d.ObjectId==900101),"generic ComposeGround and hit ownership "+gfx);
            }
        }
        foreach(ushort gfx in new ushort[]{10069,10169,10188,10369,10210,10310,10032,10042,10034,10017,10027,10055,10047,10057,10058,10158})
            Check(GroundSpriteReader.Read(GroundCatalog.Find(gfx)!,root).Blocks.Length>0,"secondary archive sample "+gfx);
        var alias=GroundSpriteReader.Read(GroundCatalog.Find(256)!,root);
        var original=GroundSpriteReader.Read(GroundCatalog.Find(253)!,root);
        Check(JsonSerializer.Serialize(alias)==JsonSerializer.Serialize(original),"metadata alias 256 -> 253");
        bool missing=false;try{GroundSpriteReader.Read(GroundCatalog.Find(65535)!,root);}catch(InvalidDataException){missing=true;}
        Check(missing,"missing gfx fails closed without borrowed artwork");
        cases.Add("執行期 SPR：三種 B06 與大寫副檔名 3247；原圖解碼／Web 圖片／正式 renderer；缺圖拒絕");
    }

    static void SyntheticSpriteChecks(string root){
        Directory.CreateDirectory(root);
        // Hand-authored one-pixel SPR; no bytes copied from any client resource.
        using var output=new MemoryStream();using(var b=new BinaryWriter(output,System.Text.Encoding.UTF8,true)){
            b.Write((byte)1);foreach(short v in new short[]{0,0,0,0})b.Write(v);b.Write(0u);b.Write((short)1);
            b.Write(new byte[3]);b.Write((ushort)0);b.Write(1u);b.Write(0u);b.Write(9u);
            b.Write(new byte[]{0,0,1,1,1,0,1});b.Write((ushort)0x7c00);
        }
        var payload=output.ToArray();var decode=typeof(GroundSpriteReader).GetMethod("DecodeCore",BindingFlags.Static|BindingFlags.NonPublic)!;
        var frame=(Frame)decode.Invoke(null,[payload,null])!;
        Check(frame.Bounds.SequenceEqual(new[]{0,0,0,0})&&frame.Blocks.Single().Tile.Pixels.Single().Color==0x7c00,"synthetic direct RGB555 decoding");
        using var bitmap=GroundSpriteReader.Image(frame);Check(bitmap.GetPixel(0,0).ToArgb()==System.Drawing.Color.Red.ToArgb(),"synthetic RGB555 expansion");
        using var empty=new MemoryStream();using(var b=new BinaryWriter(empty,System.Text.Encoding.UTF8,true)){
            foreach(short v in new short[]{192,120,-128,-120})b.Write(v);b.Write(ushort.MaxValue);b.Write((ushort)0);b.Write((short)0);
        }
        byte[] withEmpty=[2,..payload[1..20],..empty.ToArray(),..payload[20..]];
        Check(((Frame)decode.Invoke(null,[withEmpty,null])!).Blocks.Length==1,"unused empty frame sentinel preserves selected ground frame");
        void Refused(Action action,string name){
            bool refused=false;try{action();}catch(TargetInvocationException ex) when(ex.InnerException is InvalidDataException or IOException){refused=true;}
            Check(refused,name);
        }
        for(int n=0;n<payload.Length;n++)Refused(()=>decode.Invoke(null,[payload[..n],null]),"truncated SPR "+n);
        var bad=(byte[])payload.Clone();bad[^1]|=0x80;Refused(()=>decode.Invoke(null,[bad,null]),"invalid RGB555 high bit");
        bad=(byte[])payload.Clone();bad[18]=1;Refused(()=>decode.Invoke(null,[bad,null]),"tile index outside block table");
        bad=(byte[])payload.Clone();bad[24]=1;Refused(()=>decode.Invoke(null,[bad,null]),"block offset drift");
        bad=(byte[])payload.Clone();bad[28]=8;Refused(()=>decode.Invoke(null,[bad,null]),"expanded length mismatch");
        Refused(()=>decode.Invoke(null,[payload.Concat(new byte[]{0}).ToArray(),null]),"trailing SPR bytes");

        string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
        byte[] Index(uint offset=0,uint? size=null,bool duplicate=false){
            using var m=new MemoryStream();using var b=new BinaryWriter(m);
            b.Write(duplicate?2u:1u);for(int i=0;i<(duplicate?2:1);i++){
                b.Write(offset);byte[] name=new byte[20];System.Text.Encoding.ASCII.GetBytes("900-0.SPR").CopyTo(name,0);b.Write(name);b.Write(size??(uint)payload.Length);
            }return m.ToArray();
        }
        var pinType=typeof(GroundSpriteReader).GetNestedType("ArchivePin",BindingFlags.NonPublic)!;
        var archiveType=typeof(GroundSpriteReader).GetNestedType("Archive",BindingFlags.NonPublic)!;
        var find=archiveType.GetMethod("Find")!;var read=archiveType.GetMethod("Read")!;
        object Archive(byte[] idx,string? indexHash=null,string? pakHash=null,long? size=null){
            File.WriteAllBytes(Path.Combine(root,"Fixture.idx"),idx);File.WriteAllBytes(Path.Combine(root,"Fixture.pak"),payload);
            var pin=Activator.CreateInstance(pinType,["Fixture",indexHash??Hash(idx).ToLowerInvariant(),pakHash??Hash(payload),size??payload.Length])!;
            return Activator.CreateInstance(archiveType,[pin,root])!;
        }
        foreach(string fault in new[]{"index hash","pak hash","pak size","offset range","duplicate name","truncated index"}){
            var idx=fault=="offset range"?Index(offset:1):fault=="duplicate name"?Index(duplicate:true):fault=="truncated index"?Index()[..^1]:Index();
            using var archive=(IDisposable)Archive(idx,fault=="index hash"?new string('0',64):null,fault=="pak hash"?new string('0',64):null,fault=="pak size"?payload.Length+1:null);
            Refused(()=>{var entry=find.Invoke(archive,["900-0.spr"]);read.Invoke(archive,[entry]);},"archive refuses "+fault);
        }
        using(var archive=(IDisposable)Archive(Index())){
            var entry=find.Invoke(archive,["900-0.spr"]);Check(entry!=null,"case-insensitive runtime entry lookup");
            Check(((byte[])read.Invoke(archive,[entry])!).SequenceEqual(payload),"verified archive bounded payload");
            bool locked=false;try{using var writer=new FileStream(Path.Combine(root,"Fixture.pak"),FileMode.Open,FileAccess.Write);}catch(IOException){locked=true;}
            Check(locked,"verified handle prevents post-hash mutation on Windows");
        }
        cases.Add("人工合成 SPR／IDX／PAK：截斷、色碼、索引、邊界、重名、SHA、長度與核驗後唯讀鎖");
    }
}
