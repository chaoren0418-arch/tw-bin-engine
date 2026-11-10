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
    public static ItemIconSource FromSprites(IReadOnlyList<ClientArchive>? sprites){
        if(sprites==null||sprites.Count!=17)return None;
        return shared.GetValue(sprites,s=>{
            var catalog=new BuffIconCatalog(s);
            return new(icon=>catalog.Get(icon) is {} hit?new ItemIcon(icon,hit.Bitmap,new(hit.Image.OffsetX,hit.Image.OffsetY),hit.Where):null);
        });
    }
    public static ItemIconSource FromRoot(UiRoot? root)=>FromSprites(root?.Resources?.Sprites);

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

/// <summary>Equal segments of a strip image (tab / level strip) — which segment a click is on (推測: equal widths).</summary>
public static class LegacyStrip {
    public static int? SegmentAt(Rectangle strip,int segments,Point p){
        if(segments<=0||strip.Width<segments||!strip.Contains(p))return null;
        return Math.Min(segments-1,(p.X-strip.X)*segments/strip.Width);
    }
}
