using System.Security.Cryptography;
using System.Text;
namespace P06;

// Runtime gfx -> SPR lookup. Only hashes/lengths live in source; artwork stays in TW_CLIENT_ROOT.
// The verified archive handle remains read-only and denies writes/replacement on Windows.
public static class GroundSpriteReader {
    public static string ClientRoot=>Environment.GetEnvironmentVariable("TW_CLIENT_ROOT")??@"D:\lineage開發\0-客戶端";
    static void Require(bool valid){if(!valid)throw new InvalidDataException("地面原圖來源或解碼 pin 不符");}
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    sealed record ArchivePin(string Name,string IndexSha256,string PakSha256,long Size);
    // SHA-256 of the installed TW client archives, independently read for #190.
    static readonly ArchivePin[] archivePins=[
        new("Sprite","327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b","4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62",1904012586L),
        new("Sprite00","15b46ae6995b5b3d64a27a509c78847f0329ba55360a200961a7ecb73b13c96c","64741119e853880e0e31009cf06145a5f21520b5264d07ece5541ba701a01c9d",450577933L),
        new("Sprite01","1e4c819debe37449691142025692ac1ff1d1f82c148830c76338e7fdff8cdd8f","a46a5a85e56c03cc6b1d7ed7cddf3d59e8887612313842cde9822b76932b5ac5",422714941L),
        new("Sprite02","a0208077cfa7e76280641f1a2bf7f7aeacc43b6f9a16e21771ebd732e791ae5c","88858f8eaf57854d50ea21f2d84dd3d3e8c1d3fb30c5c583af2175fc465cde0c",412811463L),
        new("Sprite03","4deb893637d40e1a9e91844a4a3274f9de25e7af42197e0ac69773241f233bf6","c8ee2e21f78c69eae9307db0dd7e721f929581ad07c386e984ee4735febb6c59",422961934L),
        new("Sprite04","6ea2246d89bfd1dc7ee3d4b70a36f0bf700889d1eb5f4cad9ab633a72bf539f9","ece9df4053fb06eab08f183d8c0b555de0ec04262fdae87f9775d9ce81ca82eb",396502716L),
        new("Sprite05","f4c6d5d7974ad355b066faf620aea9428104fa64274b5eeb24ce60bf54030158","606792a3a484065de833b1fcdaa1dc64f9b134da38945fc76084ce536f4d89a1",370792959L),
        new("Sprite06","0248238be94b99e61d2c515d59066b445af9faf5b3430132bbdce706d6812bcf","5ab3fc86d66557ebb0810228174b3702d7e07cd4bd766dfb1874661f7567d861",399491486L),
        new("Sprite07","ce996c526474647be2986c2610afe2e6b193f9d24069033615ffafb3a9136a60","30a4c7eccb62b3bec7d04a2891b8239e9bd98b3266e7406d0290d5ec7ab82065",428993498L),
        new("Sprite08","4f96ce84bcb0f733842b28b3dcb39984828985f6af994ce88aae214162782022","3d23ee72b9d70c61d1cd156e2ad99e4a100d46b13324c6440deb8d6386f1d8ef",405690494L),
        new("Sprite09","37edc3cbfb37ae667aa61a6ff3c141b7a7b351f60b6990d3bcee5c5eb55f8f9a","7256514d893846361b5e6854f5129a3f9e662fa44e41c3a4d7b44c0ce4a98b36",409910541L),
        new("Sprite10","589c47ecb1a56dc9d0956ad7716e599a00f68290005f401bbc595879e253ecb1","966089b4aac89ccf139833492b96ddc8c8aa342d8d0506d74f9de43c1571a128",401362413L),
        new("Sprite11","4d560607d2b290992d0cd6beaa7556cd6d03a009346418915636c01e06c20e88","cbcab1f612218ada11589a678f9ea85a07e393f62dea24529aded1c92712343e",408627491L),
        new("Sprite12","ada81449a16e323f90539cdf0571e59f120c52629855a9fe536e19c8e34481fe","a5f735d10a891150da0b0f9b97c81a0c8e8f3ba269e1595e1cc2352bd8ac9a7c",392469796L),
        new("Sprite13","e03cf669b50ee8fb86ac4e3242fb68a211580f78f8929770a9077d4fa776203b","216e300c8b6d0d7d312f95f2dc4323f9246c1d14a6cba63a0fc7ed172510408f",404075215L),
        new("Sprite14","c6d10d9c14ecac20c5b96546f9b4494eb23cc9b28c1ab07a96070eb574e84c9a","ba14f7a681a690deda925f812bcbfeb6af392bab7b292a0e48742455f933dcc8",442915587L),
        new("Sprite15","874b53afe762c43415e25204f4e8fcf405f36adfaca6b9258bd84c1eb1d4c441","864800d2afbd8b38ed9bfcc08a0079b27fbf53f018de562f4cba6fe54dccabd2",484407291L),
    ];
    sealed record Entry(int Record,string Name,long Offset,int Size);
    sealed class Archive(ArchivePin pin,string root):IDisposable {
        Dictionary<string,Entry>? entries;
        FileStream? pak;
        public Entry? Find(string name){
            if(entries==null){
                string path=Path.Combine(root,pin.Name+".idx");
                if(!File.Exists(path))return null;
                using var file=File.OpenRead(path);
                Require(file.Length<=4+28L*1000000);
                byte[] data=new byte[checked((int)file.Length)];file.ReadExactly(data);
                Require(Hash(data)==pin.IndexSha256);
                using var r=new BinaryReader(new MemoryStream(data));
                uint count=r.ReadUInt32();Require(data.Length==4+28L*count);
                var table=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<count;i++){
                    long offset=r.ReadUInt32();byte[] raw=r.ReadBytes(20);int end=Array.IndexOf(raw,(byte)0);
                    Require(end>0&&raw.Take(end).All(c=>c is >=32 and <127));
                    string entry=Encoding.ASCII.GetString(raw,0,end);uint size=r.ReadUInt32();
                    Require(size<=int.MaxValue&&offset+size<=pin.Size);
                    // Duplicate names are rejected rather than silently choosing different artwork.
                    Require(table.TryAdd(entry,new(i,entry,offset,(int)size)));
                }
                entries=table;
            }
            return entries.GetValueOrDefault(name);
        }
        public byte[] Read(Entry entry){
            Require(entry.Size is >0 and <=8*1024*1024);
            if(pak==null){
                var candidate=new FileStream(Path.Combine(root,pin.Name+".pak"),FileMode.Open,FileAccess.Read,FileShare.Read);
                try{
                    Require(candidate.Length==pin.Size);
                    Require(Convert.ToHexString(SHA256.HashData(candidate)).Equals(pin.PakSha256,StringComparison.OrdinalIgnoreCase));
                    pak=candidate;
                }catch{candidate.Dispose();throw;}
            }
            Require(entry.Offset>=0&&entry.Offset+entry.Size<=pak.Length);
            pak.Position=entry.Offset;byte[] payload=new byte[entry.Size];pak.ReadExactly(payload);return payload;
        }
        public void Dispose()=>pak?.Dispose();
    }
    static readonly object archiveGate=new();
    static string? loadedRoot;
    static Archive[] archives=[];
    static Dictionary<int,int>? aliases;
    static int SpriteId(int gfx,string root){
        if(aliases==null){
            byte[] metadata=File.ReadAllBytes(Path.Combine(root,"TW13081901.txt"));
            Require(Hash(metadata)=="28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8");
            var table=new Dictionary<int,int>();
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                Encoding.ASCII.GetString(metadata),@"(?m)^#(\d+)[ \t]+\d+[ \t]*=[ \t]*(\d+)"))
                Require(table.TryAdd(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value)));
            aliases=table;
        }
        var visited=new HashSet<int>();
        while(aliases.TryGetValue(gfx,out int next)){Require(visited.Add(gfx));gfx=next;}
        Require(gfx is >=0 and <=ushort.MaxValue);return gfx;
    }
    public static Frame Read(GroundDefinition row,string? clientRoot=null){
        lock(archiveGate){
            string root=Path.GetFullPath(clientRoot??ClientRoot);
            if(!string.Equals(root,loadedRoot,StringComparison.OrdinalIgnoreCase)){
                foreach(var archive in archives)archive.Dispose();
                archives=archivePins.Select(p=>new Archive(p,root)).ToArray();aliases=null;loadedRoot=root;
            }
            try{
                // Ground uses action 0/frame 0, preserving the existing static-ground policy.
                string name=SpriteId(row.GroundGfx,root).ToString(System.Globalization.CultureInfo.InvariantCulture)+"-0.spr";
                for(int i=0;i<archives.Length;i++){
                    var entry=archives[i].Find(name);if(entry==null)continue;
                    if(row.Sprite is {} pin){
                        Require(i==0&&entry.Record==pin.IndexRecord&&entry.Offset==pin.Offset&&entry.Size==pin.Size&&entry.Name==pin.Entry);
                    }
                    return DecodeCore(archives[i].Read(entry),row.Sprite);
                }
                throw new InvalidDataException($"地面原圖缺件（gfx {row.GroundGfx}，{name}）");
            }catch(EndOfStreamException ex){throw new InvalidDataException("地面原圖截斷",ex);}
        }
    }
    internal static Frame Decode(byte[] payload,GroundSprite pin)=>DecodeCore(payload,pin);
    static Frame DecodeCore(byte[] payload,GroundSprite? pin){
        Require(payload.Length is >0 and <=8*1024*1024);
        if(pin!=null)Require(Hash(payload)==pin.PayloadSha256);
        using var r=new BinaryReader(new MemoryStream(payload));byte first=r.ReadByte();bool indexed=first==255;var palette=new List<ushort>();
        int n=first;if(indexed){int count=r.ReadByte();if(count==0)count=256;for(int i=0;i<count;i++)palette.Add(r.ReadUInt16());n=r.ReadByte();}
        Require(n is >0 and <=127&&palette.All(c=>(c&0x8000)==0));
        var frames=new List<(int[] Bounds,int Header,List<(int A,int B,int Z,int Index)> Tiles)>();
        int totalTiles=0;
        for(int f=0;f<n;f++){
            int header=(int)r.BaseStream.Position;int[] bounds=[r.ReadInt16(),r.ReadInt16(),r.ReadInt16(),r.ReadInt16()];int u=r.ReadUInt16(),v=r.ReadUInt16(),nt=r.ReadInt16();
            bool empty=bounds.SequenceEqual(new[]{192,120,-128,-120})&&u==65535&&v==0&&nt==0;
            Require(empty||(bounds[2]>=bounds[0]&&bounds[3]>=bounds[1]&&bounds[2]-bounds[0]<2048&&bounds[3]-bounds[1]<2048&&nt is >0 and <=4096));
            totalTiles+=nt;Require(totalTiles<=65536);
            var tiles=new List<(int,int,int,int)>();for(int j=0;j<nt;j++)tiles.Add((r.ReadSByte(),r.ReadSByte(),r.ReadByte(),r.ReadUInt16()));frames.Add((bounds,header,tiles));
        }
        uint nb=r.ReadUInt32();Require(nb is >0 and <=10000);var offsets=new uint[nb];for(int i=0;i<nb;i++)offsets[i]=r.ReadUInt32();uint expected=r.ReadUInt32();long start=r.BaseStream.Position;int expanded=0,totalPixels=0;var blocks=new List<Pixel[]>();
        for(int i=0;i<nb;i++){
            Require(offsets[i]==r.BaseStream.Position-start);int sx=r.ReadByte(),sy=r.ReadByte(),w=r.ReadByte(),lines=r.ReadByte();Require(w>0&&lines>0&&sx+w<=24&&sy+lines<=24);expanded+=4;var pixels=new List<Pixel>();
            for(int y=sy;y<sy+lines;y++){
                int x=sx,segments=r.ReadByte();expanded++;
                for(int j=0;j<segments;j++){
                    int skip=r.ReadByte(),run=r.ReadByte();expanded+=2;Require(skip%2==0);x+=skip/2;Require(run>0&&x+run<=sx+w);
                    for(int k=0;k<run;k++){
                        ushort color;if(indexed){int index=r.ReadByte();Require(index<palette.Count);color=palette[index];}else color=r.ReadUInt16();
                        Require((color&0x8000)==0&&++totalPixels<=2000000);pixels.Add(new(x++,y,color));expanded+=2;
                    }
                }
            }
            blocks.Add(pixels.ToArray());
        }
        int selected=pin?.Frame??0;
        Require(r.BaseStream.Position==payload.Length&&expanded==expected&&selected>=0&&selected<frames.Count);
        var frame=frames[selected];
        Require(frame.Tiles.Count>0&&frames.All(f=>f.Tiles.All(t=>t.Index<blocks.Count)));
        if(pin!=null)Require(frame.Header==pin.HeaderOffset&&frame.Bounds.SequenceEqual(pin.Bounds));
        var result=new List<Block>();
        int width=frame.Bounds[2]-frame.Bounds[0]+1,height=frame.Bounds[3]-frame.Bounds[1]+1;byte[] rgba=new byte[width*height*4];
        foreach(var tile in frame.Tiles){
            Require(tile.Index<blocks.Count);int half=(int)Math.Floor(tile.A/2d),bx=24*(tile.B+tile.A-half),by=12*(tile.B-half);
            foreach(var p in blocks[tile.Index]){
                int x=bx+p.X-frame.Bounds[0],y=by+p.Y-frame.Bounds[1];Require(x>=0&&x<width&&y>=0&&y<height);int at=4*(y*width+x);
                for(int channel=0;channel<3;channel++){int v=(p.Color>>(10-5*channel))&31;rgba[at+channel]=(byte)(v*8+(v>>2));}rgba[at+3]=255;
            }
            result.Add(new(tile.A,tile.B,tile.Z,result.Count,new(2,blocks[tile.Index])));
        }
        if(pin!=null)Require(Hash(rgba)==pin.RgbaSha256);
        return new("0:"+selected,frame.Bounds,result.ToArray());
    }
    public static Bitmap Image(Frame frame){
        var b=frame.Bounds;var image=new Bitmap(b[2]-b[0]+1,b[3]-b[1]+1);
        foreach(var block in frame.Blocks){int half=(int)Math.Floor(block.A/2d),x=24*(block.B+block.A-half)-b[0],y=12*(block.B-half)-b[1];foreach(var p in block.Tile.Pixels)image.SetPixel(x+p.X,y+p.Y,Color.FromArgb(Renderer.Argb(p.Color)));}
        return image;
    }
}
