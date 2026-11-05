using System.Security.Cryptography;
using System.Text;
namespace P06;

// Finite pinned SPR reader, ported from #48's decoder.py. No archive extraction.
// Preserve tile A/B, mode/sort key and source order, including transparent holes.
public static class GroundSpriteReader {
    public static string ClientRoot=>Environment.GetEnvironmentVariable("TW_CLIENT_ROOT")??@"D:\lineage開發\0-客戶端";
    static void Require(bool valid){if(!valid)throw new InvalidDataException("地面原圖來源或解碼 pin 不符");}
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static Frame Read(GroundDefinition row,string? clientRoot=null){
        var pin=row.Sprite??throw new InvalidDataException("地面原圖未核定");string root=clientRoot??ClientRoot;
        byte[] idx=File.ReadAllBytes(Path.Combine(root,"Sprite.idx"));Require(Hash(idx)==GroundCatalog.IndexSha256);
        using(var ir=new BinaryReader(new MemoryStream(idx))){
            uint count=ir.ReadUInt32();Require(idx.Length==4L+28L*count&&pin.IndexRecord<count);ir.BaseStream.Position=4L+28L*pin.IndexRecord;
            Require(ir.ReadUInt32()==pin.Offset);Require(Encoding.ASCII.GetString(ir.ReadBytes(20)).TrimEnd('\0')==pin.Entry);Require(ir.ReadUInt32()==pin.Size);
        }
        byte[] payload=new byte[pin.Size];using(var pak=File.OpenRead(Path.Combine(root,"Sprite.pak"))){Require(pin.Offset>=0&&pin.Offset+pin.Size<=pak.Length);pak.Position=pin.Offset;pak.ReadExactly(payload);}
        Require(Hash(payload)==pin.PayloadSha256);return Decode(payload,pin);
    }
    internal static Frame Decode(byte[] payload,GroundSprite pin){
        Require(payload.Length<=8*1024*1024&&Hash(payload)==pin.PayloadSha256);
        using var r=new BinaryReader(new MemoryStream(payload));byte first=r.ReadByte();bool indexed=first==255;var palette=new List<ushort>();
        int n=first;if(indexed){int count=r.ReadByte();if(count==0)count=256;for(int i=0;i<count;i++)palette.Add(r.ReadUInt16());n=r.ReadByte();}
        Require(n is >0 and <=127&&palette.All(c=>(c&0x8000)==0));
        var frames=new List<(int[] Bounds,int Header,List<(int A,int B,int Z,int Index)> Tiles)>();
        for(int f=0;f<n;f++){
            int header=(int)r.BaseStream.Position;int[] bounds=[r.ReadInt16(),r.ReadInt16(),r.ReadInt16(),r.ReadInt16()];r.ReadUInt16();r.ReadUInt16();int nt=r.ReadInt16();
            Require(bounds[2]>=bounds[0]&&bounds[3]>=bounds[1]&&bounds[2]-bounds[0]<2048&&bounds[3]-bounds[1]<2048&&nt is >0 and <=4096);
            var tiles=new List<(int,int,int,int)>();for(int j=0;j<nt;j++)tiles.Add((r.ReadSByte(),r.ReadSByte(),r.ReadByte(),r.ReadUInt16()));frames.Add((bounds,header,tiles));
        }
        uint nb=r.ReadUInt32();Require(nb is >0 and <=10000);var offsets=new uint[nb];for(int i=0;i<nb;i++)offsets[i]=r.ReadUInt32();uint expected=r.ReadUInt32();long start=r.BaseStream.Position;int expanded=0;var blocks=new List<Pixel[]>();
        for(int i=0;i<nb;i++){
            Require(offsets[i]==r.BaseStream.Position-start);int sx=r.ReadByte(),sy=r.ReadByte(),w=r.ReadByte(),lines=r.ReadByte();Require(w>0&&lines>0&&sx+w<=24&&sy+lines<=24);expanded+=4;var pixels=new List<Pixel>();
            for(int y=sy;y<sy+lines;y++){
                int x=sx,segments=r.ReadByte();expanded++;
                for(int j=0;j<segments;j++){
                    int skip=r.ReadByte(),run=r.ReadByte();expanded+=2;Require(skip%2==0);x+=skip/2;Require(run>0&&x+run<=sx+w);
                    for(int k=0;k<run;k++){
                        ushort color;if(indexed){int index=r.ReadByte();Require(index<palette.Count);color=palette[index];}else color=r.ReadUInt16();
                        Require((color&0x8000)==0);pixels.Add(new(x++,y,color));expanded+=2;
                    }
                }
            }
            blocks.Add(pixels.ToArray());
        }
        Require(r.BaseStream.Position==payload.Length&&expanded==expected&&pin.Frame<frames.Count);
        var frame=frames[pin.Frame];Require(frame.Header==pin.HeaderOffset&&frame.Bounds.SequenceEqual(pin.Bounds));var result=new List<Block>();
        int width=pin.Bounds[2]-pin.Bounds[0]+1,height=pin.Bounds[3]-pin.Bounds[1]+1;byte[] rgba=new byte[width*height*4];
        foreach(var tile in frame.Tiles){
            Require(tile.Index<blocks.Count);int half=(int)Math.Floor(tile.A/2d),bx=24*(tile.B+tile.A-half),by=12*(tile.B-half);
            foreach(var p in blocks[tile.Index]){
                int x=bx+p.X-pin.Bounds[0],y=by+p.Y-pin.Bounds[1];Require(x>=0&&x<width&&y>=0&&y<height);int at=4*(y*width+x);
                for(int channel=0;channel<3;channel++){int v=(p.Color>>(10-5*channel))&31;rgba[at+channel]=(byte)(v*8+(v>>2));}rgba[at+3]=255;
            }
            result.Add(new(tile.A,tile.B,tile.Z,result.Count,new(2,blocks[tile.Index])));
        }
        Require(Hash(rgba)==pin.RgbaSha256);return new("0:"+pin.Frame,pin.Bounds,result.ToArray());
    }
    public static Bitmap Image(Frame frame){
        var b=frame.Bounds;var image=new Bitmap(b[2]-b[0]+1,b[3]-b[1]+1);
        foreach(var block in frame.Blocks){int half=(int)Math.Floor(block.A/2d),x=24*(block.B+block.A-half)-b[0],y=12*(block.B-half)-b[1];foreach(var p in block.Tile.Pixels)image.SetPixel(x+p.X,y+p.Y,Color.FromArgb(Renderer.Argb(p.Color)));}
        return image;
    }
}
