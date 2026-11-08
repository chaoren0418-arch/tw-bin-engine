using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

// ui-minimap (Issue #470, docs/design/native-ui.md §5.3, docs/reference/ui-layout.md §5 and V5 (d)).
// Data, coordinate conversion and the game-state adapter shared by MiniMapPanel (bar, BAR_485 slot) and FieldMapPanel
// (Field_Map window). Everything here runs on the UI thread (Update/Paint/input), so no locking.
namespace P06.NativeUi {

/// <summary>
/// One Field_Map row of MapSystem-c.xml: pngNum, ratio, baseLoc, basePixel. Game tile → png pixel follows TW16 sub_A67070
/// (VA 0xA67070 / FO 0x666470): X=(dx+dy)·f, Y=f·0.5·(dy−dx), both truncated to int, then + basePixel.
/// f = 24/ratio is inferred, not read from the BIN: it is the engine's own isometric step (Coordinates.Screen: 24·(dx+dy),
/// 12·(dy−dx)) scaled by 1/ratio, and with it the real Aden rows land inside their png at both ratio 24 (10101.png 3260×1660)
/// and ratio 144 (10001.png 550×316).
/// </summary>
public sealed record FieldMapEntry(int MapNum,int PngNum,float Ratio,Point BaseLoc,Point BasePixel) {
    public const float TileStep=24f;
    public float Factor=>TileStep/Ratio;
    /// <summary>Png file name built like sub_E557D0's "%d.png" (the ge/cs/ms variants are other map kinds).</summary>
    public string PngName=>PngNum.ToString(CultureInfo.InvariantCulture)+".png";
    public Point ToPixel(int x,int y){
        int dx=x-BaseLoc.X,dy=y-BaseLoc.Y;float f=Factor;
        float px=(dx+dy)*f;
        float py=(float)(f*0.5*(float)(dy-dx));
        return new(BasePixel.X+(int)px,BasePixel.Y+(int)py);
    }
}

/// <summary>An image element of MapSystem-c.xml (surfNum + pos relative to its window).</summary>
public sealed record MapSystemImage(int Surf,Point Pos);

/// <summary>TITLE/FRAME/EXIT/ZOOM of Field_Map or Dungern_Map. Zoom is null for Dungern_Map (the XML has none).</summary>
public sealed record MapWindowLayout(Point Pos,MapSystemImage Title,MapSystemImage Frame,MapSystemImage Exit,MapSystemImage? Zoom);

/// <summary>The parts of MapSystem-c.xml that the minimap and the Field_Map window use.</summary>
public sealed class MapSystemData {
    public MapWindowLayout Field{get;}
    public MapWindowLayout Dungeon{get;}
    /// <summary>Common_UI USER_POINT_IMG: first of the 8 direction arrows (2818–2825).</summary>
    public int UserPointSurf{get;}
    public int PartyPointSurf{get;}
    public IReadOnlyDictionary<int,FieldMapEntry> FieldMaps{get;}
    public IReadOnlyList<(int From,int To)> Restricted{get;}
    /// <summary>Field_Map rows that could not be used (malformed or duplicate); never repaired by guessing.</summary>
    public IReadOnlyList<string> Rejected{get;}
    MapSystemData(MapWindowLayout field,MapWindowLayout dungeon,int user,int party,Dictionary<int,FieldMapEntry> maps,List<(int,int)> restricted,List<string> rejected){
        Field=field;Dungeon=dungeon;UserPointSurf=user;PartyPointSurf=party;FieldMaps=maps;Restricted=restricted;Rejected=rejected;
    }

    public const string FileName="MapSystem";

    public static MapSystemData Parse(UiLayout layout){
        var root=layout.Root;
        var field=Window(root.Child("Field_Map"),true);
        var dungeon=Window(root.Child("Dungern_Map"),false);
        var common=root.Child("Common_UI");
        int user=Surf(common.Child("USER_POINT_IMG")),party=Surf(common.Child("PARTY_POINT_IMG"));
        var maps=new Dictionary<int,FieldMapEntry>();var rejected=new List<string>();var duplicate=new HashSet<int>();
        foreach(var n in root.Child("Field_Map").ChildrenNamed("Map")){
            // Real data: mapNum 300 has baseLoc="32896 32523" (space) and 1003 has "bbaseLoc" (no baseLoc). How the BIN's
            // parser treats them is unverified, so such rows are skipped (the map then shows only the frame).
            if(!TryInt(n.Attr("mapNum"),out int num)){rejected.Add($"{n.Path}: mapNum 不是整數");continue;}
            if(!TryInt(n.Attr("pngNum"),out int png)||png<0){rejected.Add($"mapNum {num}: pngNum 不合法");continue;}
            if(!float.TryParse(n.Attr("ratio")?.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out float ratio)||!(ratio>0)||float.IsInfinity(ratio)){rejected.Add($"mapNum {num}: ratio 不合法");continue;}
            if(TryPair(n.Attr("baseLoc")) is not Point loc){rejected.Add($"mapNum {num}: baseLoc=\"{n.Attr("baseLoc")}\" 不是 x,y");continue;}
            if(TryPair(n.Attr("basePixel")) is not Point pixel){rejected.Add($"mapNum {num}: basePixel=\"{n.Attr("basePixel")}\" 不是 x,y");continue;}
            if(duplicate.Contains(num)||!maps.TryAdd(num,new(num,png,ratio,loc,pixel))){
                // Which duplicate the BIN keeps is unverified: drop all of them instead of picking one.
                duplicate.Add(num);maps.Remove(num);rejected.Add($"mapNum {num}: 重複");
            }
        }
        var restricted=new List<(int,int)>();
        if(root.HasChild("Restricted_Map"))foreach(var n in root.Child("Restricted_Map").ChildrenNamed("Map_Range")){
            // "5001~5134" (also "16384~65535", far wider than a surf range, so parsed here).
            string raw=n.RequiredAttr("mapRange");var parts=raw.Split('~');
            if(parts.Length is <1 or >2||!TryInt(parts[0],out int from)||!TryInt(parts[^1],out int to)||to<from)
                throw new UiLayoutException(layout.FileName,n.Path,$"mapRange=\"{raw}\" 不是 a~b");
            restricted.Add((from,to));
        }
        return new(field,dungeon,user,party,maps,restricted,rejected);
    }
    public bool IsRestricted(int mapId)=>Restricted.Any(r=>mapId>=r.From&&mapId<=r.To);
    /// <summary>Field map row for a game map, or null when the map is restricted or not in the Field_Map list.</summary>
    public FieldMapEntry? Find(int mapId)=>IsRestricted(mapId)?null:FieldMaps.GetValueOrDefault(mapId);

    static MapWindowLayout Window(UiNode n,bool zoom)=>new(Pos(n),Image(n.Child("TITLE_IMG")),Image(n.Child("FRAME_IMG")),Image(n.Child("EXIT_IMG")),zoom?Image(n.Child("ZOOM_IMG")):null);
    static MapSystemImage Image(UiNode n)=>new(Surf(n),Pos(n));
    static int Surf(UiNode n){var s=n.Surfs("surfNum");return s.Length>0?s[0]:throw new UiLayoutException(n.Layout.FileName,n.Path,"surfNum 是空的");}
    static Point Pos(UiNode n)=>TryPair(n.RequiredAttr("pos"))??throw new UiLayoutException(n.Layout.FileName,n.Path,$"pos=\"{n.Attr("pos")}\" 不是 x,y");
    static bool TryInt(string? raw,out int v)=>int.TryParse(raw?.Trim(),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out v);
    /// <summary>"x,y" with optional spaces around each number; anything else → null.</summary>
    public static Point? TryPair(string? raw){
        if(raw==null)return null;var parts=raw.Split(',');
        return parts.Length==2&&TryInt(parts[0],out int x)&&TryInt(parts[1],out int y)?new Point(x,y):null;
    }
}

/// <summary>Party member position as known to the client (live object, else the last server row for this map).</summary>
public sealed record UiMapPartyMember(string Name,int X,int Y);
/// <summary>Own position and heading (0–7, server order) on a map, plus party members on the same map.</summary>
public sealed record UiMapSnapshot(int MapId,int X,int Y,int Heading,IReadOnlyList<UiMapPartyMember> Party);

/// <summary>Adapter to the existing world model (no packet parsing). ClientForm implements it below.</summary>
public interface IUiMapSource { UiMapSnapshot? ReadMap(); }

/// <summary>
/// Placement of the 160×120 map view (TW16 sub_A695E0, FO 0x6689E0): the player's png pixel P is always drawn at
/// origin+(80,60); the png source window is P−(80,60) clamped into the image, and the image is offset so P stays centred.
/// Markers are drawn only inside the source window inset by 3 (Field_Map) or by (15,18,15,4) (bar minimap).
/// </summary>
public sealed record MapViewLayout(Point Origin,Point Player,Rectangle Source,Rectangle Markers) {
    public const int HalfWidth=80,HalfHeight=60,Width=160,Height=120,Inset=3;
    /// <summary>Arrow top-left = view centre + (−5,−4) (sub_A695E0 tail).</summary>
    public static readonly Size ArrowOffset=new(-5,-4);

    public static MapViewLayout Compute(Point origin,Point player,Size image,bool minimap){
        int l=player.X-HalfWidth,t=player.Y-HalfHeight,r,b;
        if(l<0){l=0;r=Width;}else if(l+Width>image.Width){r=image.Width;l=r-Width;}else r=l+Width;
        if(t<0){t=0;b=Height;}else if(t+Height>image.Height){b=image.Height;t=b-Height;}else b=t+Height;
        var source=Rectangle.FromLTRB(l,t,r,b);
        var markers=Rectangle.FromLTRB(l+Inset,t+Inset,r-Inset,b-Inset);
        if(minimap)markers=Rectangle.FromLTRB(markers.Left+12,markers.Top+15,markers.Right-12,markers.Bottom-1);
        return new(origin,player,source,markers);
    }
    /// <summary>Canvas position of a png pixel.</summary>
    public Point ToCanvas(Point png)=>new(Origin.X+HalfWidth+png.X-Player.X,Origin.Y+HalfHeight+png.Y-Player.Y);
    /// <summary>Canvas clip for the image: the 160×120 view inset by 3.</summary>
    public Rectangle ImageClip=>new(Origin.X+Inset,Origin.Y+Inset,Width-2*Inset,Height-2*Inset);
    public bool ShowsMarker(Point png)=>Markers.Contains(png);
    public Point ArrowAt=>new(Origin.X+HalfWidth+ArrowOffset.Width,Origin.Y+HalfHeight+ArrowOffset.Height);
    /// <summary>Heading 0–7 → USER_POINT_IMG + heading; anything else → no arrow (never a guessed image).</summary>
    public static int? ArrowSurf(int userPointSurf,int heading)=>heading is >=0 and <=7?userPointSurf+heading:null;
}

/// <summary>png maps (10000+) from Sprite{h}.pak then Sprite.pak, h = byte sum of the name % 16 (same rule as surfs).</summary>
public sealed class MapPngSource(IReadOnlyList<ClientArchive> sprites) {
    readonly HashSet<string> reported=new(StringComparer.OrdinalIgnoreCase);
    public (ClientArchive Archive,ClientArchiveEntry Entry)? Resolve(string name){
        if(sprites.Count!=17)return null;
        foreach(var a in new[]{sprites[1+SurfCatalog.NameHash(name)],sprites[0]})if(a.Find(name) is {} e)return (a,e);
        return null;
    }
    /// <summary>Decoded 32bpp PArgb copy, or null (missing / unreadable → one log line, the window shows only its frame).</summary>
    public Bitmap? Load(string name){
        try{
            if(Resolve(name) is not {} found){Report(name,"不在 Sprite*.pak");return null;}
            var (archive,entry)=found;
            using var stream=new MemoryStream(archive.Read(entry),false);
            using var decoded=new Bitmap(stream);
            var copy=new Bitmap(decoded.Width,decoded.Height,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(copy)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImage(decoded,new Rectangle(0,0,copy.Width,copy.Height),0,0,decoded.Width,decoded.Height,GraphicsUnit.Pixel);}
            return copy;
        }catch(Exception ex) when(ex is UiResourceException or ArgumentException or ExternalException){Report(name,ex.Message);return null;}
    }
    void Report(string name,string why){if(reported.Add(name))UiResourceLog.Write($"map png {name}: {why}");}
}

public enum MapViewKind {
    /// <summary>Not in the world: frame only.</summary>
    None,
    /// <summary>Map listed in Field_Map with a usable row: png + arrow + party dots.</summary>
    Field,
    /// <summary>Not listed (original hands it to the dungeon viewer, whose content is not implemented): frame only.</summary>
    Dungeon,
    /// <summary>Restricted_Map range (original minimap mode 4): frame only.</summary>
    Restricted,
}

/// <summary>Shared state of the two map panels: parsed MapSystem, the current snapshot and one cached png.</summary>
public sealed class MapState:IDisposable {
    readonly MapPngSource? png;
    readonly Func<IUiMapSource?> fallback;
    object? lastState;
    Bitmap? image;string? imageName;
    public MapSystemData? Data{get;private set;}
    public UiMapSnapshot? Current{get;private set;}
    public FieldMapEntry? Entry{get;private set;}
    public MapViewKind Kind{get;private set;}
    public bool FieldOpen{get;private set;}

    public MapState(MapPngSource? png,Func<IUiMapSource?>? fallback=null){this.png=png;this.fallback=fallback??ClientSource;}
    /// <summary>
    /// The product's source: the ClientForm (implements IUiMapSource). IUiGameState has no map fields yet and this task may not
    /// change the C2 contract, so the form is found through Application.OpenForms (UI thread only).
    /// </summary>
    public static IUiMapSource? ClientSource()=>Application.OpenForms.OfType<IUiMapSource>().FirstOrDefault();

    public void Load(MapSystemData data){
        Data=data;
        foreach(var r in data.Rejected)UiResourceLog.Write($"{MapSystemData.FileName}: 略過 Field_Map {r}");
    }

    /// <summary>Called by both panels each frame; the second call with the same state object is a no-op.</summary>
    public void Update(IUiGameState state){
        if(ReferenceEquals(state,lastState))return;
        lastState=state;
        var source=state as IUiMapSource??fallback();
        Current=state.InWorld?source?.ReadMap():null;
        if(Current==null){Kind=MapViewKind.None;Entry=null;FieldOpen=false;return;}
        Entry=Data?.Find(Current.MapId);
        Kind=Entry!=null?MapViewKind.Field:Data?.IsRestricted(Current.MapId)==true?MapViewKind.Restricted:MapViewKind.Dungeon;
    }

    /// <summary>Ctrl+M / Map button: toggles Field_Map. Only in the world; returns false when nothing could happen.</summary>
    public bool ToggleField(){if(Current==null)return false;FieldOpen=!FieldOpen;return true;}
    public void CloseField()=>FieldOpen=false;

    /// <summary>The png for the current Field entry (decoded once per map change), or null.</summary>
    public Bitmap? Image{get{
        if(Kind!=MapViewKind.Field||Entry==null||png==null)return null;
        if(imageName!=Entry.PngName){image?.Dispose();image=png.Load(Entry.PngName);imageName=Entry.PngName;}
        return image;
    }}

    /// <summary>View for the current snapshot, or null when there is nothing to place (not Field, or no image).</summary>
    public MapViewLayout? View(Point origin,bool minimap){
        if(Image is not {} img||Entry==null||Current==null)return null;
        return MapViewLayout.Compute(origin,Entry.ToPixel(Current.X,Current.Y),img.Size,minimap);
    }

    /// <summary>Draws png, party dots and own arrow for a view (shared by both panels; original sub_A695E0 order).</summary>
    public void PaintMap(UiCanvas canvas,SurfCatalog? surfs,MapViewLayout view,Action? between=null){
        var clip=canvas.Clip;
        canvas.Restrict(Rectangle.Intersect(clip,view.ImageClip));
        var img=Image!;
        var src=Rectangle.Intersect(view.Source,new Rectangle(Point.Empty,img.Size));
        if(src.Width>0&&src.Height>0){var at=view.ToCanvas(src.Location);canvas.DrawBitmap(img,src,at.X,at.Y);}
        canvas.Restrict(clip);
        between?.Invoke();
        if(Data==null||Current==null||Entry==null)return;
        if(surfs?.Get(Data.PartyPointSurf) is {} dot)foreach(var m in Current.Party){
            var p=Entry.ToPixel(m.X,m.Y);if(!view.ShowsMarker(p))continue;
            var at=view.ToCanvas(p);canvas.DrawSurface(dot,at.X-dot.Width/2,at.Y-dot.Height/2);
        }
        if(view.ShowsMarker(view.Player)&&MapViewLayout.ArrowSurf(Data.UserPointSurf,Current.Heading) is int arrow)
            canvas.DrawSurface(surfs?.Get(arrow),view.ArrowAt.X,view.ArrowAt.Y);
    }

    public void Dispose(){image?.Dispose();image=null;imageName=null;}
}

}

namespace P06 {
using P06.NativeUi;

public sealed partial class ClientForm:IUiMapSource {
    UiMapSnapshot? IUiMapSource.ReadMap()=>login.State==LoginState.InWorld?login.CaptureWorld(()=>NativeMapSnapshot.Capture(login.World)):null;
}

static class NativeMapSnapshot {
    /// <summary>Own position/heading and party members on this map. Membership comes only from the Party roster (as Minimap.cs).</summary>
    public static UiMapSnapshot? Capture(WorldSession world){
        if(world.MapId is not ushort map||world.Actor is not {} actor||world.TeleportPending)return null;
        var party=new List<UiMapPartyMember>();
        foreach(var member in world.PartySnapshot(0,false).Members){
            if(member.ObjectId!=0&&member.ObjectId==actor.ObjectId)continue;
            var seen=world.Objects.Where(o=>member.ObjectId!=0?o.ObjectId==member.ObjectId:o.Name==member.Name&&(o.Flags&4)!=0).ToArray();
            var live=seen.Length==1?seen[0]:null;
            if(live==null&&member.MapId!=map)continue;
            party.Add(new(member.Name,live?.X??member.X,live?.Y??member.Y));
        }
        return new(map,actor.X,actor.Y,actor.Direction,party);
    }
}

}
