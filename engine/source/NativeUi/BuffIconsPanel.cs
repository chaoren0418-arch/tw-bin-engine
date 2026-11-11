using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
namespace P06.NativeUi;

// ui-buff-icons (Issue #473, docs/design/native-ui.md §5.3). Every rule here is docs/reference/ui-layout.md §4 (V3, #458):
//   table   Tile.pak effectlist2.xml (sub_AA0610 / sub_81DF30), ids 0..count−1; an effect ≥ count creates no icon (sub_F6D560)
//   icon    "<icon>.tbt" from the Icon archive group (sub_87B3A0, "%d.tbt"), 32×32, not through SurfList.xml
//   lookup  Icon group = HashFileManager + LocalFileNonDashFormatter (V4 (d)(e)): <n>c.tbt in Sprite{h}, Sprite; <n>.tbt in Sprite{h}, Sprite
//   layout  sub_903470: cell n at (798 − 35·(n mod 22 + 1), 5 + 35·⌊n/22⌋); buffs first, debuffs (kind slow/curse/paralyse) from
//           row ⌈buffs/22⌉
//   order   key sub_903EB0 (priority > 0 → priority; kind 0–2 → kind; else 100); comparator sub_8FDE00 (either key < 100 → keys,
//           both ≥ 100 → earlier first); a new icon goes before the first element the comparator puts it ahead of, else last
//   draw    sub_9001F0: icon; offIcon when offIcon > 0, display mode 0/6 and the time ran out; icon 0 → the effect number
//   time    never drawn on the icon; tooltip only (sub_900FC0 + sub_9061E0): 160 wide at (x, y+32), "%s: %d %s" (930, s, 931)
// Not done (see the issue-473 report): the 0x800 "sort by remaining time" option, the +21 hidden-cell flag, effect 470's special
// case, gauge overlays (no effect uses gaugePng). Display modes: 0–4, 6–8, 12 since R5-F; 5, 9, 10, 11 write no text.
// R4-F (T297/#709): display mode 1 (percent tooltip "%s: %d%%" with string 1551, sub_9061E0 VA 0x9062FD), per-entry icon override
// (S250/75 rewrites effect 135's icon), and table-duration countdown for entries that carry ElapsedMs.
// R5-F (T311/#738): the rest of the sub_9061E0 switch (jump table VA 0x9066D0, 13 cases on byte +0x260), see TooltipText.
// R7-E (T344/#807): effect 470's tail (VA 0x90666D, reached by every case): player dword +0x328 = 0 → colour index 3 and
// sprintf(buf, string 3399) (replaces the text); otherwise strcpy(buf, "") (no tooltip). Tooltip colour: index 10 of the
// dword_1369D70 palette (VA 0x906216), see TooltipColorIndex. Effect 470's special case in the icon draw (sub_9001F0) is not done.

/// <summary>Effect kind list off_138D8FC (13 names, index = kind byte).</summary>
public enum BuffKind { Slow=0, Curse=1, Paralyse=2, Einhasad=3, Security=4, Dragon=5, MagicDoll=6, Cook=7, Speed=8, Attack=9, Defense=10, Neutrality=11, Legacy=12 }

/// <summary>One &lt;effect&gt; row. Icon/OffIcon 0 = none. Kind null = attribute absent (推測: treated as a buff).
/// StartMsg: attribute startMsg (record +12, a string-c.tbl id; 0 = none), printed by sub_F05B10 (R5-F, S250/180).</summary>
public sealed record BuffEffect(int Id,int Icon,int OffIcon,BuffKind? Kind,int Priority,int Duration,string Name,int StartMsg=0) {
    public bool Debuff=>Kind is BuffKind.Slow or BuffKind.Curse or BuffKind.Paralyse;
    /// <summary>sub_903EB0.</summary>
    public int SortKey=>Priority>0?Priority:Kind is BuffKind k&&(int)k<=2?(int)k:100;
}

/// <summary>Parsed effectlist2.xml.</summary>
public sealed class BuffEffectTable {
    public const string FileName="effectlist2";
    static readonly string[] KindNames=["slow","curse","paralyse","einhasad","security","dragon","magic doll","cook","speed","attack","defense","neutrality","legacy"];
    readonly Dictionary<int,BuffEffect> effects;
    /// <summary>Record count (sub_9015E0 +44): effects ≥ Count are never shown.</summary>
    public int Count{get;}
    public IReadOnlyList<string> OrderFaults{get;}
    BuffEffectTable(Dictionary<int,BuffEffect> effects,int count,List<string> faults){this.effects=effects;Count=count;OrderFaults=faults;}
    public BuffEffect? this[int id]=>id>=0&&id<Count?effects.GetValueOrDefault(id):null;
    public IEnumerable<BuffEffect> All=>effects.Values.OrderBy(e=>e.Id);

    public static BuffKind? ParseKind(string? raw){
        if(raw==null)return null;
        int i=Array.IndexOf(KindNames,raw.Trim().ToLowerInvariant());
        return i<0?null:(BuffKind)i;
    }

    public static BuffEffectTable Parse(UiLayout layout){
        if(layout.Root.Name!="effectlist")throw new UiLayoutException(layout.FileName,layout.Root.Path,"根元素不是 effectlist");
        var map=new Dictionary<int,BuffEffect>();var faults=new List<string>();int order=0;
        foreach(var n in layout.Root.ChildrenNamed("effect")){
            int id=n.Int("id");
            if(id<0)throw new UiLayoutException(layout.FileName,n.Path,"effect id 為負");
            if(id!=order)faults.Add($"Order fault: id {id} at #{order}");   // sub_81DF30 only logs this
            order++;
            if(n.Attr("kind") is string k&&ParseKind(k)==null)throw new UiLayoutException(layout.FileName,n.Path,$"未知的 kind \"{k}\"");
            var e=new BuffEffect(id,n.TryInt("icon")??0,n.TryInt("offIcon")??0,ParseKind(n.Attr("kind")),n.TryInt("priority")??0,n.TryInt("duration")??0,n.Attr("startMsgName")??"",n.TryInt("startMsg")??0);
            if(!map.TryAdd(id,e))throw new UiLayoutException(layout.FileName,n.Path,$"effect id {id} 重複");
        }
        if(map.Count==0)throw new UiLayoutException(layout.FileName,layout.Root.Path,"沒有任何 effect");
        return new(map,order,faults);
    }
}

/// <summary>Decoded .tbt (port of the original helper LoadL1Image, as in the inventory icons): header x, y, w, h bytes, then per row
/// a segment count and (skip bytes, pixel count, RGB555 pixels). Skipped and value-0 pixels are transparent.</summary>
public sealed class TbtImage {
    public int OffsetX{get;}
    public int OffsetY{get;}
    public int Width{get;}
    public int Height{get;}
    public uint[] Argb{get;}
    TbtImage(int x,int y,int w,int h,uint[] argb){OffsetX=x;OffsetY=y;Width=w;Height=h;Argb=argb;}
    public static TbtImage Decode(ReadOnlySpan<byte> data,string name="tbt"){
        if(data.Length<4)throw new UiResourceException($"{name}: .tbt 標頭不足");
        int ox=data[0],oy=data[1],w=data[2],h=data[3],at=4;
        if(w==0||h==0)throw new UiResourceException($"{name}: .tbt 尺寸為 0");
        var argb=new uint[w*h];
        for(int y=0;y<h;y++){
            if(at>=data.Length)throw new UiResourceException($"{name}: .tbt 第 {y} 列截斷");
            int segments=data[at++],x=0;
            for(int s=0;s<segments;s++){
                if(at+2>data.Length)throw new UiResourceException($"{name}: .tbt 第 {y} 列截斷");
                int skip=data[at++];if(skip%2!=0)throw new UiResourceException($"{name}: .tbt 位移不是偶數");
                x+=skip/2;int count=data[at++];
                if(x+count>w||at+count*2>data.Length)throw new UiResourceException($"{name}: .tbt 第 {y} 列超出範圍");
                for(int p=0;p<count;p++,x++,at+=2){
                    ushort v=(ushort)(data[at]|data[at+1]<<8);
                    if(v!=0)argb[y*w+x]=UiImage.ToArgb(v)|0xFF000000;
                }
            }
        }
        if(at!=data.Length)throw new UiResourceException($"{name}: .tbt 結尾有多餘資料");
        return new(ox,oy,w,h,argb);
    }
    public Bitmap ToBitmap(){
        var bmp=new Bitmap(Width,Height,PixelFormat.Format32bppArgb);
        var bits=bmp.LockBits(new(0,0,Width,Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<Height;y++)Marshal.Copy((int[])(object)Argb,y*Width,bits.Scan0+y*bits.Stride,Width);}
        finally{bmp.UnlockBits(bits);}
        return bmp;
    }
}

/// <summary>"&lt;n&gt;.tbt" lookup over the 17 Sprite archives (index 0 = Sprite, 1+h = Sprite{h:00}); decoded icons are cached.</summary>
public sealed class BuffIconCatalog(IReadOnlyList<ClientArchive> sprites) {
    readonly object gate=new();
    readonly Dictionary<int,(TbtImage Image,Bitmap Bitmap,string Where)?> cache=new();
    readonly HashSet<int> reported=[];
    public static IEnumerable<(int Archive,string Name)> Candidates(int icon){
        foreach(var name in new[]{$"{icon}{SurfCatalog.LocalizedImgSuffix}.tbt",$"{icon}.tbt"}){
            yield return (1+SurfCatalog.NameHash(name),name);
            yield return (0,name);
        }
    }
    /// <summary>Decoded icon or null (missing → one log line per icon, nothing drawn).</summary>
    public (TbtImage Image,Bitmap Bitmap,string Where)? Get(int icon){
        if(icon<=0)return null;
        lock(gate){
            if(cache.TryGetValue(icon,out var hit))return hit;
            (TbtImage,Bitmap,string)? found=null;
            if(sprites.Count==17)foreach(var (i,name) in Candidates(icon)){
                var a=sprites[i];var e=a.Find(name);if(e==null)continue;
                var img=TbtImage.Decode(a.Read(e),$"{a.Pin.Name}/{name}");found=(img,img.ToBitmap(),$"{a.Pin.Name}/{e.Name}");break;
            }
            if(found==null&&reported.Add(icon))UiResourceLog.Write($"buff-icons: {icon}.tbt 不在 Sprite*.pak，不畫");
            cache[icon]=found;return found;
        }
    }
}

/// <summary>The two ordered icon lists of the original container (+4 buffs, +12 debuffs) with insertion semantics.</summary>
public sealed class BuffIconOrder {
    public sealed record Item(BuffEffect Effect,long AddedSeq);
    readonly List<Item> buffs=[],debuffs=[];long seq;
    public IReadOnlyList<Item> Buffs=>buffs;
    public IReadOnlyList<Item> Debuffs=>debuffs;
    /// <summary>sub_8FDE00: true = a goes before b.</summary>
    public static bool Before(Item a,Item b){
        int ka=a.Effect.SortKey,kb=b.Effect.SortKey;
        if(ka<100||kb<100)return ka<kb;
        return a.AddedSeq<b.AddedSeq;
    }
    /// <summary>Removes effects no longer present and inserts new ones (in the given order); kept icons never move.</summary>
    public void Sync(IEnumerable<BuffEffect> present){
        var list=present.ToList();var ids=list.Select(e=>e.Id).ToHashSet();
        buffs.RemoveAll(i=>!ids.Contains(i.Effect.Id));debuffs.RemoveAll(i=>!ids.Contains(i.Effect.Id));
        foreach(var e in list){
            if(buffs.Any(i=>i.Effect.Id==e.Id)||debuffs.Any(i=>i.Effect.Id==e.Id))continue;
            var item=new Item(e,++seq);var target=e.Debuff?debuffs:buffs;
            int at=target.FindIndex(x=>Before(item,x));
            if(at<0)target.Add(item);else target.Insert(at,item);
        }
    }
    public void Clear(){buffs.Clear();debuffs.Clear();}
    /// <summary>(effect, cell index) in layout order: buffs 0.., debuffs from row ⌈buffs/22⌉.</summary>
    public IEnumerable<(BuffEffect Effect,int Cell)> Cells(){
        for(int i=0;i<buffs.Count;i++)yield return (buffs[i].Effect,i);
        int start=(buffs.Count+BuffIconsPanel.PerRow-1)/BuffIconsPanel.PerRow*BuffIconsPanel.PerRow;
        for(int i=0;i<debuffs.Count;i++)yield return (debuffs[i].Effect,start+i);
    }
}

public sealed class BuffIconsPanel(IReadOnlyList<ClientArchive>? sprites=null):IUiPanel {
    [UiPanel(140)] internal static UiPanelRegistration Registration=>new(BuffIconsPanel.PanelId,BuffEffectTable.FileName,root=>new BuffIconsPanel(root.Resources?.Sprites));   // ui-buff-icons #473 (layout = Tile.pak effectlist2.xml; icons = <n>.tbt)
    public const string PanelId="STATUS_EFFECT_ICONS";
    public const int PerRow=22,Pitch=35,IconSize=32,RightEdge=798,Top=5,TooltipWidth=160,TooltipMargin=5;
    public static Point Cell(int n)=>new(RightEdge-Pitch*(n%PerRow+1),Top+Pitch*(n/PerRow));

    public sealed record Placed(BuffEffect Effect,BuffEntry Entry,Rectangle Rect,int Icon,bool Off);

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>placed.Count>0;
    public BuffEffectTable? Table{get;private set;}
    public BuffIconCatalog? Icons{get;private set;}
    public IReadOnlyList<Placed> Layout=>placed;
    public IReadOnlyList<BuffUnmapped> Unmapped{get;private set;}=[];
    /// <summary>Effect id under the mouse (last Move delivered to this panel).</summary>
    public int? Hover{get;private set;}
    readonly BuffIconOrder order=new();
    List<Placed> placed=[];
    BuffTooltipText text=BuffTooltipText.None;
    int vipFlag;
    readonly HashSet<string> logged=[];

    public void Load(UiLayout layout,SurfCatalog surfs){
        Table=BuffEffectTable.Parse(layout);
        foreach(var f in Table.OrderFaults)UiResourceLog.Write($"buff-icons {layout.FileName}: {f}");
        Icons=new BuffIconCatalog(sprites??[]);
    }
    /// <summary>Tests: a table and icon source without archives.</summary>
    public void Load(BuffEffectTable table,BuffIconCatalog icons){Table=table;Icons=icons;}

    public void Update(IUiGameState state){
        var buffs=state.InWorld?BuffState.Of(state)??BuffState.Empty:BuffState.Empty;
        text=buffs.Text;vipFlag=buffs.VipFlag;Unmapped=buffs.Unmapped;
        foreach(var u in buffs.Unmapped)Log($"buff-icons 未對照（不畫）{u.Origin}：{u.Reason}");
        var byEffect=new Dictionary<int,BuffEntry>();var present=new List<BuffEffect>();
        if(Table!=null)foreach(var e in buffs.Entries){
            var def=Table[e.Effect];
            if(def==null){Log($"buff-icons effect {e.Effect} ≥ effectlist2 筆數 {Table.Count}（{e.Origin}），不建立圖示");continue;}
            if(byEffect.TryAdd(e.Effect,e))present.Add(def);
        }
        order.Sync(present);
        var list=new List<Placed>();
        foreach(var (def,cell) in order.Cells()){
            var entry=ApplyDefaultDuration(byEffect[def.Id],def);
            var (icon,off)=IconFor(def,entry,vipFlag);
            list.Add(new(def,entry,new Rectangle(Cell(cell),new Size(IconSize,IconSize)),icon,off));
        }
        placed=list;
        if(Hover is int h&&!placed.Any(x=>x.Effect.Id==h))Hover=null;
        UpdateBounds();
    }
    void Log(string line){if(logged.Add(line))UiResourceLog.Write(line);}

    /// <summary>
    /// Icon id and "offIcon drawn" for one entry. Normal effects: sub_902840 — offIcon only when it exists, the time is over
    /// and the display mode is 0/6. T426 (#970, FU15§2.9 已驗證): sub_9001F0 (VA 0x9001F0 / FO 0x4FF5F0) skips sub_902840 for
    /// effect 470 and reads the player dword +0x328 (vipFlag) instead: ≠ 0 → icon, 0 → offIcon (table column +9), regardless
    /// of time and mode. offIcon 0 (none in the table) → the icon (no substitute).
    /// </summary>
    public static (int Icon,bool Off) IconFor(BuffEffect def,BuffEntry entry,int vipFlag){
        bool off=entry.Effect==Vip470Effect
            ?vipFlag==0&&def.OffIcon>0
            :def.OffIcon>0&&entry.Expired&&entry.DisplayMode is 0 or 6;
        return (off?def.OffIcon:entry.IconOverride??def.Icon,off);
    }

    /// <summary>
    /// Seconds of an entry whose time is the effect's table duration (S171 invisible, R4-F S250/75): duration −1 → no limit;
    /// otherwise counted down from ElapsedMs like sub_9061E0 (left = duration − elapsed/1000, Expired when left &lt; 0);
    /// start unknown → null (no tooltip, no guess).
    /// </summary>
    public static BuffEntry ApplyDefaultDuration(BuffEntry entry,BuffEffect def){
        if(!entry.DefaultDuration)return entry;
        if(def.Duration<0)return entry with{Seconds=-1};
        if(entry.ElapsedMs is not long ms)return entry with{Seconds=null};
        long left=def.Duration-ms/1000;
        return entry with{Seconds=(int)Math.Max(0,left),Expired=left<0};
    }

    /// <summary>
    /// Tooltip line for one icon; null when the string table or the value is unknown. sub_9061E0 (VA 0x9061E0 / FO 0x5055E0),
    /// switch on display mode (byte +0x260, jump table VA 0x9066D0). "value" = the seconds slot +0x50 as sent; "left" = remaining
    /// seconds (+0x50 − elapsed/1000; the engine's Seconds already holds it for timed entries):
    ///   0, 6  2854 when offIcon is drawn; "%s: -- %s" (930, 931) for −1 or time over; else "%s: %d %s" (930, left, 931)
    ///   1     "%s: %d%%"            (1551, value)                                   VA 0x9062FD
    ///   2     "%s: %d%s"            (978, value, 1587)                              VA 0x90632B
    ///   3     empty string → no tooltip                                             VA 0x906369
    ///   4     "%s: %d%s"            (978, left/60 + 1, 1587)                        VA 0x90637D
    ///   7     "%s: %d%s %d%s %d%s"  (978, d, 763, h, 1630, m + 1, 1587)             VA 0x9063CE
    ///   8     "%s:" (978) + at most two non-zero parts " %d%s" in the order d 763, h 1630, m 1587, s 931 (m without +1) VA 0x906485
    ///   12    "%s: %d%%"            (4829, value)                                   VA 0x906644
    ///   5, 9, 10, 11  nothing written → no tooltip
    /// d = left/86400, h = (left − 86400·d)/3600, m = (left − 86400·d − 3600·h)/60, s = left mod 60; C integer division (truncates
    /// toward zero, as C#). The local string-c.tbl has 3484 records, so 4829 is missing there → mode 12 shows no text (not invented).
    /// R7-E tail (VA 0x90666D, after every case including 5/9/10/11 and modes &gt; 12): effect 470 (cmp [+0x20], 0x1D6) →
    /// vipFlag (player dword +0x328) = 0 → string 3399 (sprintf with the string as the format; it has no '%') replacing the text;
    /// vipFlag ≠ 0 → strcpy "" → no tooltip.
    /// </summary>
    public string? TooltipText(Placed p)=>TooltipText(p.Entry,p.Off,text,vipFlag);
    public static string? TooltipText(BuffEntry entry,bool off,BuffTooltipText text,int vipFlag=0){
        if(entry.Effect==Vip470Effect)return vipFlag==0?text.Vip470:null;
        return CaseText(entry,off,text);
    }
    /// <summary>R7-E: effect id tested by the sub_9061E0 tail (0x1D6, STR_STATUS_EFFECT_CH_VIP_BUFF).</summary>
    public const int Vip470Effect=0x1D6;
    /// <summary>Tooltip colour slot ([ebp+0xC]): palette index 10 at entry (VA 0x906216); the 470 tail with vipFlag 0 sets 3 (VA 0x906687).</summary>
    public const int TooltipColor=10,Vip470TooltipColor=3;
    public static int TooltipColorIndex(BuffEntry entry,int vipFlag=0)=>entry.Effect==Vip470Effect&&vipFlag==0?Vip470TooltipColor:TooltipColor;
    static string? CaseText(BuffEntry entry,bool off,BuffTooltipText text){
        static string N(int v)=>v.ToString(CultureInfo.InvariantCulture);
        switch(entry.DisplayMode){
        case 0 or 6:
            if(off)return text.Over;
            if(text.Label==null||text.Unit==null||entry.Seconds is not int s)return null;
            return s<0||entry.Expired?$"{text.Label}: -- {text.Unit}":$"{text.Label}: {N(s)} {text.Unit}";
        case 1:return text.Percent!=null&&entry.Seconds is int v1?$"{text.Percent}: {N(v1)}%":null;
        case 12:return text.Percent12!=null&&entry.Seconds is int v12?$"{text.Percent12}: {N(v12)}%":null;
        }
        if(entry.DisplayMode is not (2 or 4 or 7 or 8)||text.Remain==null||text.Minute==null||entry.Seconds is not int left)return null;
        switch(entry.DisplayMode){
        case 2:return $"{text.Remain}: {N(left)}{text.Minute}";
        case 4:return $"{text.Remain}: {N(left/60+1)}{text.Minute}";
        }
        if(text.Day==null||text.Hour==null)return null;
        int d=left/86400,h=(left-d*86400)/3600,m=(left-d*86400-h*3600)/60;
        if(entry.DisplayMode==7)return $"{text.Remain}: {N(d)}{text.Day} {N(h)}{text.Hour} {N(m+1)}{text.Minute}";
        if(text.Unit==null)return null;
        var sb=new System.Text.StringBuilder(text.Remain).Append(':');int parts=0;
        foreach(var (n,unit) in new[]{(d,text.Day),(h,text.Hour),(m,text.Minute),(left%60,text.Unit)})
            if(n>0&&parts<2){sb.Append(' ').Append(N(n)).Append(unit);parts++;}
        return sb.ToString();
    }
    public const int TooltipHeight=16;
    /// <summary>Tooltip box: 160 wide at (x, y+32), moved left when closer than 5 px to the right edge.</summary>
    public static Rectangle TooltipRect(Rectangle icon){
        int x=Math.Min(icon.X,UiRoot.LogicalWidth-TooltipMargin-TooltipWidth);
        return new(x,icon.Bottom,TooltipWidth,TooltipHeight);
    }
    /// <summary>The hovered icon (Hover is an effect id, so it survives re-ordering).</summary>
    public Placed? Hovered=>Hover is int h?placed.FirstOrDefault(x=>x.Effect.Id==h):null;
    Rectangle? ShownTooltip()=>Hovered is {} p&&TooltipText(p)!=null?TooltipRect(p.Rect):null;
    void UpdateBounds(){
        var r=Rectangle.Empty;
        foreach(var p in placed)r=r.IsEmpty?p.Rect:Rectangle.Union(r,p.Rect);
        if(ShownTooltip() is Rectangle t)r=Rectangle.Union(r,t);
        Bounds=r;
    }
    public int IconAt(Point p)=>placed.FindIndex(x=>x.Rect.Contains(p));

    public void Paint(UiCanvas canvas){
        foreach(var p in placed){
            if(p.Icon==0){canvas.DrawText(p.Effect.Id.ToString(CultureInfo.InvariantCulture),p.Rect.X,p.Rect.Y,Color.White);continue;}
            if(Icons?.Get(p.Icon) is {} icon)canvas.DrawBitmap(icon.Bitmap,p.Rect.X+icon.Image.OffsetX,p.Rect.Y+icon.Image.OffsetY);
        }
        if(Hovered is {} hp&&TooltipText(hp) is string line){
            // Box style (background, padding) is 待驗證; position, width, text and the text colour index (R7-E) are V3.
            var r=TooltipRect(hp.Rect);
            using var back=new SolidBrush(Color.FromArgb(192,0,0,0));
            canvas.Graphics.FillRectangle(back,r);
            canvas.DrawText(line,r.X+2,r.Y+2,ChatPalette.Color(TooltipColorIndex(hp.Entry,vipFlag)));
        }
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        int i=IconAt(e.Location);
        if(e.Kind==UiMouseKind.Move){var old=Hover;Hover=i<0?null:placed[i].Effect.Id;if(old!=Hover)UpdateBounds();return i>=0;}
        return i>=0;   // presses on an icon are taken (LStatusEffectButton); the tooltip area passes nothing to the world either
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
