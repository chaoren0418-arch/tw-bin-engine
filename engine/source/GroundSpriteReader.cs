using System.Security.Cryptography;
using System.Text;
namespace P06;

// Runtime gfx -> SPR lookup. Only hashes live in source; artwork stays in TW_CLIENT_ROOT.
// T479 (#1083, asset-recipe.md 1.1 point 3 / §8 D1): the 17 Sprite containers are opened through the shared ClientArchive
// (structure check, no whole-container pin; the pak handle stays read-only and denies writes/replacement on Windows).
// The container is chosen by SpriteContainerRule (sprites.md §3.3). A catalogue row with a GroundSprite pin keeps its
// per-entry check (main container, record, offset, size, payload SHA-256, header, bounds, RGBA) exactly as before.
public static class GroundSpriteReader {
    public static string ClientRoot=>Environment.GetEnvironmentVariable("TW_CLIENT_ROOT")??@"D:\lineage開發\0-客戶端";
    static void Require(bool valid){if(!valid)throw new InvalidDataException("地面原圖來源或解碼 pin 不符");}
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static readonly object archiveGate=new();
    static string? loadedRoot;
    static P06.NativeUi.ClientArchive[] archives=[];
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
                archives=P06.NativeUi.UiResourcePins.Sprites.Select(p=>new P06.NativeUi.ClientArchive(root,p)).ToArray();aliases=null;loadedRoot=root;
            }
            try{
                // Ground uses action 0/frame 0, preserving the existing static-ground policy.
                string name=SpriteId(row.GroundGfx,root).ToString(System.Globalization.CultureInfo.InvariantCulture)+"-0.spr";
                byte[] payload=ReadPayload(archives,name,row.Sprite)??throw new InvalidDataException($"地面原圖缺件（gfx {row.GroundGfx}，{name}）");
                return DecodeCore(payload,row.Sprite);
            }catch(EndOfStreamException ex){throw new InvalidDataException("地面原圖截斷",ex);}
        }
    }

    /// <summary>
    /// The stored bytes of <paramref name="name"/> from the Sprite containers (index 0 = Sprite, 1+h = Sprite{h:00}), or null
    /// when no container has it. Container choice: sprites.md §3.3 (main first; alternates must be byte-identical, otherwise
    /// refused). A pinned row must come from the main container with the pinned name, record, offset and size (the payload,
    /// header, bounds and RGBA are checked by the decoder). Every refusal is an InvalidDataException naming container and entry.
    /// </summary>
    public static byte[]? ReadPayload(IReadOnlyList<P06.NativeUi.ClientArchive> sprites,string name,GroundSprite? pin){
        try{
            if(pin!=null){
                var main=sprites.Count>0?sprites[0].Find(name):null;
                if(main!=null){
                    if(!(main.Record==pin.IndexRecord&&main.Offset==pin.Offset&&main.Size==pin.Size&&main.Name==pin.Entry))
                        throw new InvalidDataException($"地面原圖 {sprites[0].Pin.Name}/{main.Name} 與 GroundCatalog.Pinned 的 entry pin 不符（record {main.Record}、offset {main.Offset}、size {main.Size}）");
                    return sprites[0].Read(main);
                }
            }
            var chosen=P06.NativeUi.SpriteContainerRule.Choose(sprites.Count,i=>sprites[i].Find(name) is {} e?sprites[i].Read(e):null,
                (a,b)=>new InvalidDataException($"地面原圖 {name} 不在主包 {sprites[0].Pin.Name}，替代包 {sprites[a].Pin.Name} 與 {sprites[b].Pin.Name} 的內容不同；不選用任何一份"));
            if(chosen is {} hit&&pin!=null)
                throw new InvalidDataException($"地面原圖 {name} 有 entry pin，但不在主包 {sprites[0].Pin.Name}（在 {sprites[hit.Archive].Pin.Name}）");
            return chosen?.Data;
        }catch(P06.NativeUi.UiResourceException ex){throw new InvalidDataException("地面原圖容器被拒絕："+ex.Message,ex);}
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
