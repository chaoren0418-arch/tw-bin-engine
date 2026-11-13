using System.Buffers.Binary;
using System.Security.Cryptography;
namespace P06;
public sealed class ShipLac {
    public int Width{get;}public int Height{get;}public int OffsetX{get;}public int OffsetY{get;}
    public ushort[] Palette{get;}public byte[][] Frames{get;}public bool[][] Masks{get;}
    public ShipLac(byte[] bytes){
        if(bytes.Length<592)throw new InvalidDataException("LAC header");
        using var r=new BinaryReader(new MemoryStream(bytes));Width=r.ReadUInt16();OffsetX=r.ReadInt16();Height=r.ReadUInt16();OffsetY=r.ReadInt16();int count=r.ReadInt32(),mode=r.ReadInt32();
        if(Width<=0||Height<=0||Width>2048||Height>2048||count is <1 or >15||mode is <0 or >1)throw new InvalidDataException("LAC profile");
        int[] offsets=Enumerable.Range(0,count+1).Select(_=>r.ReadInt32()).ToArray();if(offsets[0]!=0||offsets[^1]!=bytes.Length-592||offsets.Zip(offsets.Skip(1)).Any(v=>v.First>=v.Second))throw new InvalidDataException("LAC frame offsets");
        Palette=Enumerable.Range(0,256).Select(i=>BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(80+2*i))).ToArray();Frames=new byte[count][];Masks=new bool[count][];
        for(int frame=0;frame<count;frame++){
            int end=592+offsets[frame+1];r.BaseStream.Position=592+offsets[frame];var pixels=new byte[Width*Height];var mask=new bool[pixels.Length];
            if(mode==0){if(end-r.BaseStream.Position!=pixels.Length)throw new InvalidDataException("LAC raw frame");r.BaseStream.ReadExactly(pixels);Array.Fill(mask,true);}
            else for(int y=0;y<Height;y++){
                int x=0;while(x<Width){if(r.BaseStream.Position+2>end)throw new InvalidDataException("LAC RLE row");int skip=r.ReadByte(),run=r.ReadByte();if(y==Height-1&&x+skip>=Width){r.BaseStream.Position=end;x=Width;break;}if(skip+run==0||x+skip+run>Width||r.BaseStream.Position+run>end)throw new InvalidDataException("LAC RLE run");x+=skip;for(int n=0;n<run;n++){pixels[y*Width+x]=r.ReadByte();mask[y*Width+x++]=true;}}
            }
            if(r.BaseStream.Position!=end)throw new InvalidDataException("LAC trailing data");Frames[frame]=pixels;Masks[frame]=mask;
        }
    }
}
public sealed class ShipBackdropState {
    public int Phase{get;private set;}public int DockX{get;private set;}public int DockY{get;private set;}
    public int WaterX{get;private set;}public int WaterY{get;private set;}public int Wave{get;private set;}public int Shade{get;private set;}
    long last=-1,accumulator;
    // BIN VA9E9C90 and VA9E90A0. Visual state never authorizes a ship packet.
    public void Synchronize(ushort map,uint seconds){
        int minute=ShipRules.Minute(seconds);while(minute>=240)minute-=180;
        int previous=Phase;Phase=minute==60?2:minute>60&&minute<85?3:minute==85?4:minute>85&&minute<150?5:minute==150?6:minute>150&&minute<175?7:minute==175?8:1;
        Shade=Phase==3?5-(int)(Math.Abs(72.5-minute)/2.5):Phase==7?5-(int)(Math.Abs(162.5-minute)/2.5):0;
        // C95 is queried every 2s on ships: re-entering the same native minute would rewind the pier.
        // Initialize only on a phase change; shade still follows each authoritative game minute.
        if(Phase==previous)return;
        if(Phase==1){DockX=392;DockY=-164;}else if(Phase==5){DockX=DockY=0;}else if(Phase==4){DockX=-1000;DockY=500;accumulator=0;}else if(Phase==8){DockX=1392;DockY=map==5?-864:-664;accumulator=0;}else if(Phase is 2 or 6)accumulator=0;
    }
    public void Advance(long now){
        if(now==last)return;long dt=last<0?0:Math.Clamp(now-last,0,500);if(last>=0)Wave=(Wave+1)%11;last=now;
        if(Phase is not (2 or 3 or 4 or 6 or 7 or 8))return;
        accumulator+=dt;int steps=Phase is 4 or 8?(int)(accumulator/100):accumulator>=100?1:0;
        if(steps==0)return;accumulator=Phase is 4 or 8?accumulator%100:0;int sign=Phase<=4?1:-1;
        WaterX+=sign*2*steps;WaterY-=sign*steps;
        if(Phase is not (3 or 7)){DockX+=sign*2*steps;DockY-=sign*steps;}
        if(Phase==4){DockX=Math.Min(DockX,0);DockY=Math.Max(DockY,0);}else if(Phase==8){DockX=Math.Max(DockX,392);DockY=Math.Min(DockY,-164);}
    }
}
public sealed partial class WorldSession {
    ShipBackdropState shipBackdrop=new();
    internal ShipBackdropState? PrepareShipBackdrop(long now){if(!ShipRules.Supported(MapId))return null;shipBackdrop.Advance(now);return shipBackdrop;}
}
public sealed partial class Renderer {
    ShipBackdropState? shipBackdrop;ushort shipMap;ShipLac? waterLac,pier1Lac,pier5Lac;MapRawSource? shipSource;
    public bool HasShipBackdrop=>shipBackdrop!=null&&mapCache?.MapId==shipMap;
    void PrepareShipBackdrop(WorldSession session,long now){shipBackdrop=session.PrepareShipBackdrop(now);shipMap=session.MapId??0;if(shipBackdrop==null)return;
        // T456: assets/maps/ship (packaged tree) or the Tile.pak entry (prepared tree); the three pins below are unchanged.
        ShipLac Load(string name,string hash){shipSource??=MapRawSource.Open(assets.Root,session.MapClientRoot);return new(shipSource.ShipLac(name,hash));}
        waterLac??=Load("Water.lac","36055e0acb7e5234c1f1cce27d41ca102be736991fd29b0f5071ca7d4fc7f2b3");pier1Lac??=Load("Pier1.lac","2202bf29f48675004fb2e2e73d6296ccd2c30e0789d1d5ba882a57bd67609f0a");pier5Lac??=Load("Pier5.lac","c5e619634f9cb34c6c7e7f5fd0b340c213fe3f6fd78edf336e363bb558c52fd4");
    }
    void FillShipBackdrop(Rectangle area,ushort[] colors,bool[] mask){
        if(!HasShipBackdrop)return;var s=shipBackdrop!;var water=waterLac!;
        // VA11434B0 anchor (32696,32778) uses internal half-cell X; VA F1A750 projection.
        int dx=32696-(2*Coordinates.MinX-32768),dy=32778-Coordinates.MinY;
        int ax=12*((dx+1)&~1)+24*dy-24,ay=12*dy-6*(dx&~1)+756;
        var indices=water.Frames[s.Wave];
        for(int y=0;y<area.Height;y++)for(int x=0;x<area.Width;x++){
            int index=indices[((area.Top+y-ay-s.WaterY)&127)*128+((area.Left+x-ax-s.WaterX)&127)];ushort color=water.Palette[index];
            if(index<128&&s.Shade!=0)color=unchecked((ushort)(((water.Palette[index+128]&31)-s.Shade)|(32*(((water.Palette[index+128]&0x3e0)>>5)-s.Shade))|(water.Palette[index+128]&0xfc00)));
            colors[y*area.Width+x]=color;mask[y*area.Width+x]=true;
        }
        ShipLac? pier=s.Phase is 1 or 2 or 8?pier5Lac:s.Phase is 4 or 5 or 6?pier1Lac:null;if(pier==null)return;
        int px=ax+s.DockX+(s.Phase is 4 or 5 or 6?-379:shipMap==6&&s.Phase is 1 or 2?-60:0)+pier.OffsetX;
        int py=ay+s.DockY+(s.Phase is 4 or 5 or 6?-321:shipMap==6&&s.Phase is 1 or 2?-240:0)+pier.OffsetY;
        var overlap=Rectangle.Intersect(area,new(px,py,pier.Width,pier.Height));for(int y=overlap.Top;y<overlap.Bottom;y++)for(int x=overlap.Left;x<overlap.Right;x++){int src=(y-py)*pier.Width+x-px;if(!pier.Masks[0][src])continue;int dst=(y-area.Top)*area.Width+x-area.Left;colors[dst]=pier.Palette[pier.Frames[0][src]];mask[dst]=true;}
    }
    Bitmap ShipSceneView(Patch patch,Rectangle area){
        var colors=new ushort[area.Width*area.Height];var mask=new bool[colors.Length];FillShipBackdrop(area,colors,mask);
        foreach(var draw in ActiveScene)if(draw.Px<area.Right&&draw.Px+24>area.Left&&draw.Py<area.Bottom&&draw.Py+24>area.Top)Blit(draw,area,colors,mask,null,null,backgroundState?.Get(draw));
        var overlap=Rectangle.Intersect(area,patch.Bounds);for(int y=overlap.Top;y<overlap.Bottom;y++){int src=(y-patch.Bounds.Top)*patch.Bounds.Width+overlap.Left-patch.Bounds.Left,dst=(y-area.Top)*area.Width+overlap.Left-area.Left;Array.Copy(patch.Colors,src,colors,dst,overlap.Width);Array.Copy(patch.Mask,src,mask,dst,overlap.Width);}return BitmapOf(area.Width,area.Height,colors,mask);
    }
}
