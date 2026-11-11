using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06.NativeUi;

// R4-B (T294, Issue #706; gap-matrix 13.5): shared item-icon source and cell geometry for the native legacy windows
// (ITEM_WINDOW, WAREHOUSE_WINDOW) and, later, R4-C (TRANSACTION_WINDOW, trade). Do not copy icons into the repo: they are read
// at runtime from the pinned client's Sprite*.pak.
//
// What the icon id means (已驗證, FU5§4): the inventory object's +36 word is the S_ADD_INVENTORY record's `h` = the 3.81
// S_AddItem 背包圖示 gfx (InventoryItem.IconId / ShopRow.Icon). sub_518CD0 (FO 0x1180D0) stores it through sub_C02C40 and
// replaces gfx 487 or 1617 with 1617 when the bless byte is 0 (IconOf below).
// Where the picture comes from (推測): "<icon>.tbt" through the Icon archive group — the same lookup as the status icons
// (BuffIconCatalog, sub_87B3A0 "%d.tbt"); the stage's assets/inventory PNGs were generated from Sprite "<icon>.tbt" (issue-102).

/// <summary>One decoded item icon: bitmap plus the .tbt header offset (drawn at cell + offset, as the status icons).</summary>
public sealed record ItemIcon(int Icon,Bitmap Bitmap,Point Offset,string Where);

/// <summary>Item icon id → decoded icon. Missing icons are null (nothing drawn, no placeholder; one log line per id).</summary>
public sealed class ItemIconSource {
    public const int IconSize=32;
    readonly Func<int,ItemIcon?> lookup;
    public ItemIconSource(Func<int,ItemIcon?> lookup)=>this.lookup=lookup;
    /// <summary>No archives (offline checks without icons, or the pre-resource canvas): every icon is missing.</summary>
    public static ItemIconSource None{get;}=new(_=>null);

    static readonly ConditionalWeakTable<IReadOnlyList<ClientArchive>,ItemIconSource> shared=new();
    /// <summary>One source (one decode cache) per client archive list, shared by every window that asks for it.</summary>
    /// <param name="reference">T403 (#918): asked only when the pinned local Sprite*.pak has no &lt;icon&gt;.tbt (null = off).
    /// Only the first call for a given archive list decides it (the shared cache).</param>
    public static ItemIconSource FromSprites(IReadOnlyList<ClientArchive>? sprites,RefClientIconFallback? reference=null){
        if(sprites==null||sprites.Count!=17)return None;
        return shared.GetValue(sprites,s=>Combine(new BuffIconCatalog(s).Get,reference));
    }
    /// <summary>Local lookup first; the reference client only for an icon the local client lacks (main client wins).</summary>
    internal static ItemIconSource Combine(Func<int,(TbtImage Image,Bitmap Bitmap,string Where)?> local,RefClientIconFallback? reference)=>
        new(icon=>(local(icon)??reference?.Get(icon)) is {} hit?new ItemIcon(icon,hit.Bitmap,new(hit.Image.OffsetX,hit.Image.OffsetY),hit.Where):null);
    public static ItemIconSource FromRoot(UiRoot? root)=>FromSprites(root?.Resources?.Sprites,RefClientIconFallback.For(root?.Resources?.Surfs.Reference));

    /// <summary>FU5§4 (sub_518CD0): gfx 487 or 1617 with bless 0 becomes 1617; every other value is used as sent.</summary>
    public static int IconOf(ushort icon,byte bless)=>(icon is 487 or 1617)&&bless==0?1617:icon;
    public ItemIcon? Get(int icon)=>icon<=0?null:lookup(icon);
    public ItemIcon? For(InventoryItem item)=>Get(IconOf(item.IconId,item.Bless));
    /// <summary>Warehouse / shop rows carry the icon only (no bless byte is kept by NpcServices), so it is used as sent.</summary>
    public ItemIcon? For(ShopRow row)=>Get(row.Icon);

    /// <summary>Draws the icon inside a cell (top-left + .tbt offset). Returns false when the icon is missing (nothing drawn).</summary>
    public static bool Draw(UiCanvas canvas,ItemIcon? icon,Point cell){
        if(icon==null)return false;
        canvas.DrawBitmap(icon.Bitmap,cell.X+icon.Offset.X,cell.Y+icon.Offset.Y);
        return true;
    }
}

/// <summary>
/// Display text for item rows. Names arrive as server strings with "$n" references; they are resolved through the stage's
/// existing objects/names.json (the same table ObjectAssets.Name uses for the WinForms inventory). Without the table the raw
/// string is shown (nothing invented).
/// </summary>
public static class ItemText {
    static readonly Lazy<string[]?> names=new(LoadNames,LazyThreadSafetyMode.ExecutionAndPublication);
    static string[]? LoadNames(){
        try{
            string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/objects/names.json"));
            if(!File.Exists(path))return null;
            using var doc=JsonDocument.Parse(File.ReadAllBytes(path));
            return doc.RootElement.EnumerateArray().Select(x=>x.GetString()??"").ToArray();
        }catch(Exception ex){UiResourceLog.Write("item-text: names.json 讀取失敗："+ex.Message);return null;}
    }
    /// <summary>"$n" → names[n] (same rule as ObjectAssets.Name); unknown references stay as they are.</summary>
    public static string Resolve(string raw,IReadOnlyList<string>? table){
        if(table==null||raw.IndexOf('$')<0)return raw;
        return Regex.Replace(raw,@"\$(\d+)",m=>int.TryParse(m.Groups[1].Value,out int id)&&id<table.Count?table[id]:m.Value);
    }
    public static string Resolve(string raw)=>Resolve(raw,names.Value);
    /// <summary>Item name without the trailing " (count)" the server appends (InventoryUi.ItemName does the same).</summary>
    public static string Name(string resolved,uint count){
        string suffix=" ("+count+")";
        return resolved.EndsWith(suffix,StringComparison.Ordinal)?resolved[..^suffix.Length]:resolved;
    }
    /// <summary>Count label drawn on a cell: only stacks (count &gt; 1) show a number (推測; the original font/position is 待驗證).</summary>
    public static string Count(uint count)=>count>1?count.ToString(System.Globalization.CultureInfo.InvariantCulture):"";
}

/// <summary>
/// A grid of equal cells relative to a window origin. The legacy XML gives no cell geometry (windows-2.md §2.2), so every
/// window derives it from placed XML parts (推測, see Fit): cells fill the free area between them, row-major, Pitch apart.
/// </summary>
public sealed record LegacyCellGrid(Point Origin,Size Cell,Size Pitch,int Columns,int Rows) {
    /// <summary>Icon cell pitch: 32-pixel icon + 1 (the PersonalShop ItemIcon cells are 33×33; 推測 for the legacy windows).</summary>
    public const int IconPitch=33;
    public static readonly LegacyCellGrid Empty=new(Point.Empty,Size.Empty,Size.Empty,0,0);
    public int PageSize=>Columns*Rows;
    public Rectangle Area=>PageSize==0?Rectangle.Empty:new(Origin.X,Origin.Y,(Columns-1)*Pitch.Width+Cell.Width,(Rows-1)*Pitch.Height+Cell.Height);

    /// <summary>
    /// As many cells of the given pitch as fit in [left, right) × [top, bottom), centred in that area. A cell is pitch−1 wide
    /// (one-pixel gap) unless a cell width is given (list rows use the full width).
    /// </summary>
    public static LegacyCellGrid Fit(int left,int top,int right,int bottom,Size pitch,int? cellWidth=null){
        int w=right-left,h=bottom-top;
        if(w<=0||h<=0||pitch.Width<=0||pitch.Height<=0)return Empty;
        int cw=cellWidth??pitch.Width-1;
        int cols=cellWidth!=null?(cw<=w?1:0):(w+1)/pitch.Width,rows=(h+1)/pitch.Height;
        if(cols==0||rows==0)return Empty;
        int ch=pitch.Height-1;
        int usedW=(cols-1)*pitch.Width+cw,usedH=(rows-1)*pitch.Height+ch;
        return new(new(left+(w-usedW)/2,top+(h-usedH)/2),new(cw,ch),pitch,cols,rows);
    }
    /// <summary>Cell rectangle of slot n of the page (0-based, row-major), relative to the window origin.</summary>
    public Rectangle CellRect(int slot){
        if(slot<0||slot>=PageSize)throw new ArgumentOutOfRangeException(nameof(slot));
        return new(Origin.X+slot%Columns*Pitch.Width,Origin.Y+slot/Columns*Pitch.Height,Cell.Width,Cell.Height);
    }
    /// <summary>Slot under a window-relative point, or null (outside, or in the gap between cells).</summary>
    public int? SlotAt(Point local){
        if(PageSize==0)return null;
        int dx=local.X-Origin.X,dy=local.Y-Origin.Y;
        if(dx<0||dy<0)return null;
        int c=dx/Pitch.Width,r=dy/Pitch.Height;
        if(c>=Columns||r>=Rows||dx%Pitch.Width>=Cell.Width||dy%Pitch.Height>=Cell.Height)return null;
        return r*Columns+c;
    }
    /// <summary>Rows needed for count entries (a partly filled row counts).</summary>
    public int RowsFor(int count)=>Columns==0?0:(count+Columns-1)/Columns;
    /// <summary>Scroll positions (first visible row 0..n−1); 0 when everything fits on one page.</summary>
    public int ScrollPositions(int count)=>Math.Max(0,RowsFor(count)-Rows)+(RowsFor(count)>Rows?1:0);
}

/// <summary>
/// T403 (#918, gap-matrix 15.12.6 小清理): item icons the local 3.81 client lacks, read at run time from the configured
/// reference client (the same root as RefClientSurfFallback / RefClientXmlSource: TW_REF_CLIENT → native-ui.json "refClient"
/// → kor25). FU9§11.1 / issue-851: 5 of the spell-learning list's icons (5731, 5734, 5737, 6458, 6501) are in none of the 24
/// local idx; PakViewer (2026-11-11, docs/reports/issue-918.md) finds each once in kor25 — Sprite08/11/14/15/04, i.e. the
/// same NameHash archive as the local layout — 2,146 B, 32×32 .tbt.
/// Lookup: BuffIconCatalog.Candidates order ("&lt;n&gt;c.tbt" then "&lt;n&gt;.tbt", hashed archive then Sprite). The idx is
/// read and hashed in memory, the one payload is read by offset, and its SHA-256 is logged ("icon-ref"); nothing is copied
/// into the repository or written anywhere, and the reference .pak files are not hashed whole. Missing, duplicated, out of
/// range or undecodable → null (nothing drawn).
/// </summary>
public sealed class RefClientIconFallback {
    const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;
    /// <summary>SHA-256 of the five kor25 payloads (PakViewer extract to scratch/, 2026-11-11). Compared and logged only.</summary>
    public static readonly IReadOnlyDictionary<int,string> Kor25Sha256=new Dictionary<int,string>{
        [5731]="135efe10efc6b2c854cd9d06b55e4d9289232c5a43526d067b66137206efa515",
        [5734]="1092e5f538af490218f5fd9609919f0b069327dffb1493425e36a17a089c6586",
        [5737]="0dbe7c260395caddf880e6e4fde6495c90f18c2dd1d5a8bd2307cf69830ad2df",
        [6458]="3a14434aa57f03a41dc8c2af4ba35a2a70e32f836426c859dfbff98184a1d0b0",
        [6501]="e79d1d7d72c9f0ab8c7084d563d6f33f65bed149dd80d06b3db1497f0f628277",
    };
    sealed record Index(Dictionary<string,(long Offset,int Size)> Entries,HashSet<string> Duplicates,string Sha256);
    readonly object gate=new();
    readonly Index?[] indexes=new Index?[RefClientSurfFallback.ArchiveCount];
    readonly bool[] tried=new bool[RefClientSurfFallback.ArchiveCount];
    readonly Dictionary<int,(TbtImage Image,Bitmap Bitmap,string Where)?> cache=new();
    readonly Func<int,byte[]?> readIndex;
    readonly Func<int,long,int,byte[]?> readBlob;
    public string Root{get;}
    public string Source{get;}
    /// <summary>Every icon served so far: id → (archive/name, payload SHA-256).</summary>
    public IReadOnlyList<(int Icon,string Where,string Sha256)> Served{get{lock(gate)return served.ToArray();}}
    readonly List<(int,string,string)> served=[];

    public RefClientIconFallback(string root,string source="explicit"):this(Path.GetFullPath(root),source,null,null){}
    RefClientIconFallback(string root,string source,Func<int,byte[]?>? index,Func<int,long,int,byte[]?>? blob){
        Root=root;Source=source;readIndex=index??FileIndex;readBlob=blob??FileBlob;
    }
    /// <summary>Same reference client as the surf fallback; null when that is off / not configured.</summary>
    public static RefClientIconFallback? For(RefClientSurfFallback? surf)=>surf==null?null:new(surf.Root,surf.Source);
    /// <summary>Test seam: archive index → in-memory idx / pak images (missing key = no such archive).</summary>
    internal static RefClientIconFallback FromImages(string root,IReadOnlyDictionary<int,(byte[] Idx,byte[] Pak)> images)=>new(root,"synthetic",
        a=>images.TryGetValue(a,out var i)?i.Idx:null,
        (a,offset,size)=>images.TryGetValue(a,out var i)&&offset>=0&&offset+size<=i.Pak.Length?i.Pak.AsSpan((int)offset,size).ToArray():null);

    string IndexPath(int a)=>Path.Combine(Root,RefClientSurfFallback.ArchiveName(a)+".idx");
    string PakPath(int a)=>Path.Combine(Root,RefClientSurfFallback.ArchiveName(a)+".pak");
    byte[]? FileIndex(int a){
        if(!File.Exists(IndexPath(a))||!File.Exists(PakPath(a)))return null;
        using var file=new FileStream(IndexPath(a),FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length<4||file.Length>4+(long)RecordSize*MaxRecords)throw new InvalidDataException($"{RefClientSurfFallback.ArchiveName(a)}.idx 大小不合理");
        var data=new byte[file.Length];file.ReadExactly(data);return data;
    }
    byte[]? FileBlob(int a,long offset,int size){
        using var pak=new FileStream(PakPath(a),FileMode.Open,FileAccess.Read,FileShare.Read);
        if(offset<0||offset+size>pak.Length)return null;
        var blob=new byte[size];pak.Position=offset;pak.ReadExactly(blob);return blob;
    }

    Index? Load(int a){
        if(tried[a])return indexes[a];
        tried[a]=true;
        try{
            if(readIndex(a) is not {} idx){UiResourceLog.Write($"icon-ref {Root} 沒有 {RefClientSurfFallback.ArchiveName(a)}.idx／.pak");return null;}
            if(idx.Length<4)throw new InvalidDataException("idx 太短");
            uint count=BitConverter.ToUInt32(idx,0);
            if(count>MaxRecords||(long)count*RecordSize+4!=idx.Length)throw new InvalidDataException($"{RefClientSurfFallback.ArchiveName(a)}.idx 筆數×28+4 不等於檔案大小");
            var table=new Dictionary<string,(long,int)>(StringComparer.OrdinalIgnoreCase);var dup=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<count;i++){
                int at=4+i*RecordSize;var raw=idx.AsSpan(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
                if(end==0)continue;
                bool printable=true;foreach(byte c in raw[..end])if(c is <32 or >=127){printable=false;break;}
                if(!printable)continue;
                string name=System.Text.Encoding.ASCII.GetString(raw[..end]);
                if(!table.TryAdd(name,(BitConverter.ToUInt32(idx,at),(int)Math.Min(BitConverter.ToUInt32(idx,at+4+NameSize),int.MaxValue))))dup.Add(name);
            }
            return indexes[a]=new(table,dup,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(idx)).ToLowerInvariant());
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){
            UiResourceLog.Write("icon-ref 無法讀取："+ex.Message);return null;
        }
    }

    /// <summary>Decoded reference icon, or null (not there / unreadable). Cached per id, success or not.</summary>
    public (TbtImage Image,Bitmap Bitmap,string Where)? Get(int icon){
        if(icon<=0)return null;
        lock(gate){
            if(cache.TryGetValue(icon,out var hit))return hit;
            (TbtImage,Bitmap,string)? found=null;
            foreach(var (a,name) in BuffIconCatalog.Candidates(icon)){
                if(Load(a) is not {} index)continue;
                string archive=RefClientSurfFallback.ArchiveName(a);
                if(index.Duplicates.Contains(name)){UiResourceLog.Write($"icon-ref {archive}/{name} 在 idx 重名，不使用");continue;}
                if(!index.Entries.TryGetValue(name,out var e))continue;
                try{
                    if(e.Size is <=0 or >ClientArchive.MaxEntrySize||readBlob(a,e.Offset,e.Size) is not {} blob){UiResourceLog.Write($"icon-ref {archive}/{name} 越界或大小不合理（{e.Offset}+{e.Size}）");continue;}
                    string sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(blob)).ToLowerInvariant();
                    var img=TbtImage.Decode(blob,$"{Root}\\{archive}.pak:{name}");
                    string where="ref:"+archive+"/"+name;
                    found=(img,img.ToBitmap(),where);served.Add((icon,where,sha));
                    bool known=Kor25Sha256.TryGetValue(icon,out var k)&&k==sha;
                    UiResourceLog.Write($"icon-ref {icon} -> {Root}\\{archive}.pak:{name} sha256 {sha} (idx {index.Sha256}, {Source}{(known?", 與 kor25 相同":"")})");
                    break;
                }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){
                    UiResourceLog.Write($"icon-ref {archive}/{name} 讀取失敗或不是可解的 .tbt：{ex.Message}");
                }
            }
            cache[icon]=found;return found;
        }
    }
}

/// <summary>Equal segments of a strip image (tab / level strip) — which segment a click is on (推測: equal widths).</summary>
public static class LegacyStrip {
    public static int? SegmentAt(Rectangle strip,int segments,Point p){
        if(segments<=0||strip.Width<segments||!strip.Contains(p))return null;
        return Math.Min(segments-1,(p.X-strip.X)*segments/strip.Width);
    }
}
