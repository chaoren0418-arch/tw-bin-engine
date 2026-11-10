using P06.NativeUi;
namespace P06;

/// <summary>
/// WinForms 狀態列的一筆。ValueText 非 null 時是沒有倒數的狀態（S250 75／82／180），顯示文字取代秒數。
/// R4-F（#709）：Effect＝原版 effectlist2 的 effect id（目前只有水中呼吸 11 與 S250 75／82／180 有查證過的對照；其他 null），
/// IconOverride＝原版改寫的 .tbt 編號（75）。有 Effect 時預設模式的狀態列畫原客戶端的 &lt;icon&gt;.tbt（見 StatusIconArt）。
/// </summary>
public sealed record StatusIconEntry(int Id,string Name,int Seconds,bool Debuff,bool Approximate,string? ValueText=null,int? Effect=null,int? IconOverride=null) {
    public string Detail=>ValueText!=null?$"{Name} · {ValueText}":$"{Name} · {(Approximate?"約":"")}{Seconds} 秒";
    /// <summary>T265：S250 B2 圖示的 Id＝−(1_000_000＋effect)，不和 MagicBuffs 的技能鍵相撞。</summary>
    public static int PacketBoxId(int effect)=>-1_000_000-effect;
}

public static class StatusIconFeed {
    /// <summary>
    /// 狀態列內容：MagicBuffs 的倒數狀態（秒數 &gt; 0），再接 S250 75／82／180 的圖示（T265，資料見 PacketBoxStatusIcons）。
    /// 75 有預設秒數但 WinForms 狀態列不讀 effect 135 的 duration，所以只顯示階段，不顯示秒數（原生 UI 會倒數）。
    /// R4-F：水中呼吸（S126 → effect 11，TimedBuffRules.StatusEffect）與 75／82／180 帶 effect id，供預設模式畫原版圖示。
    /// </summary>
    public static StatusIconEntry[] Read(WorldSession w){
        long now=w.MagicStatusClock();
        var timed=w.MagicBuffs.Select(b=>new StatusIconEntry(b.Skill,b.Name,b.Remaining(now),b.Debuff,b.Approximate,Effect:TimedBuffRules.StatusEffect(b.Skill))).Where(b=>b.Seconds>0);
        var boxed=w.PacketBoxStatusIcons.Select(i=>new StatusIconEntry(StatusIconEntry.PacketBoxId(i.Effect),i.Name,0,false,false,i.ValueText,i.Effect,i.IconOverride));
        return [..timed,..boxed];
    }
}

/// <summary>
/// R4-F（#709）預設模式（原生 UI 關閉）的狀態列圖示：和原生 STATUS_EFFECT_ICONS 同一來源——Tile.pak 的 effectlist2.xml 取 icon，
/// 再從 Sprite*.pak 讀 &lt;icon&gt;.tbt（BuffEffectTable／BuffIconCatalog，執行期從原客戶端讀，不複製資源）。
/// 表或圖檔讀不到時回 null，狀態列退回向量符號（不畫替代圖）。
/// </summary>
public sealed class StatusIconArt(BuffEffectTable table,BuffIconCatalog icons) {
    public BuffEffectTable Table=>table;
    /// <summary>要畫的 .tbt 編號：IconOverride 優先，否則 effect 的 icon；effect ≥ 表筆數（原版不建立）或 icon 0 → null。</summary>
    public static int? IconFor(BuffEffectTable table,int? effect,int? iconOverride){
        if(effect is not int id||table[id] is not {} def)return null;
        int icon=iconOverride??def.Icon;
        return icon>0?icon:null;
    }
    /// <summary>解碼後的圖示；沒有對照、圖檔不在或解碼失敗（記一行 log）為 null。</summary>
    public TbtImage? ImageFor(StatusIconEntry e,out Bitmap? bitmap){
        bitmap=null;
        if(IconFor(table,e.Effect,e.IconOverride) is not int icon)return null;
        try{if(icons.Get(icon) is {} hit){bitmap=hit.Bitmap;return hit.Image;}}
        catch(UiResourceException ex){if(failed.Add(icon))UiResourceLog.Write($"status-icons: {icon}.tbt 解碼失敗，不畫：{ex.Message}");}
        return null;
    }
    readonly HashSet<int> failed=[];

    static int loadState;   // 0＝未讀、1＝讀取中、2＝完成（成功或失敗）
    static StatusIconArt? shared;
    /// <summary>共用實例；第一次呼叫時在背景讀原客戶端資源，讀完前與失敗時為 null。</summary>
    public static StatusIconArt? Shared{get{
        if(Interlocked.CompareExchange(ref loadState,1,0)==0)_ = Task.Run(()=>{
            try{
                var table=SharedEffectTable()??throw new UiResourceException("effectlist2 不可用");
                Volatile.Write(ref shared,new StatusIconArt(table,new BuffIconCatalog(UiResources.Shared.Sprites)));
            }
            catch(Exception ex){UiResourceLog.Write($"status-icons: effectlist2／Sprite 不可用，狀態列改畫向量符號：{ex.GetType().Name}: {ex.Message}");}
            finally{Volatile.Write(ref loadState,2);}
        });
        return Volatile.Read(ref shared);
    }}

    // R5-F（#738）：只讀 Tile.pak 的 effectlist2（不碰 Sprite），同步、只讀一次；S250/180 的 startMsg 與上面的狀態列共用。
    static readonly Lazy<BuffEffectTable?> sharedTable=new(()=>{
        try{return BuffEffectTable.Parse(UiResources.Shared.Xml.Load(BuffEffectTable.FileName));}
        catch(Exception ex){UiResourceLog.Write($"status-icons: effectlist2 不可用（180 不印 startMsg）：{ex.GetType().Name}: {ex.Message}");return null;}
    },LazyThreadSafetyMode.ExecutionAndPublication);
    /// <summary>原客戶端 effectlist2（讀不到為 null，之後不再重試）。</summary>
    public static BuffEffectTable? SharedEffectTable()=>sharedTable.Value;
}

public sealed partial class ClientForm {
    internal StatusIconEntry[] ReadStatusIcons()=>PlayerHudActive?StatusIconFeed.Read(login.World):[];
}

// Compact HUD badges: entries with a verified effectlist2 id draw the original <icon>.tbt (StatusIconArt, R4-F);
// the rest keep the vector symbols. Overflow remains accessible through the arrow buttons, wheel and full-list tooltip.
public sealed class StatusIcons:Control {
    readonly ToolTip tips=new(){AutoPopDelay=15000};
    StatusIconEntry[] entries=[];int first;
    // 有帶 effect 的狀態時才去讀原客戶端資源（沒有就不碰 Tile／Sprite）。
    StatusIconArt? CurrentArt=>entries.Any(e=>e.Effect!=null)?StatusIconArt.Shared:null;
    public StatusIcons(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleRole=AccessibleRole.StaticText;BackColor=PlayerHudPainter.Ink;}
    public void UpdateState(StatusIconEntry[] state){
        entries=state;first=Math.Clamp(first,0,Math.Max(0,entries.Length-Capacity));
        AccessibleName=entries.Length==0?"無持續狀態":"持續狀態："+string.Join("；",entries.Select(x=>x.Detail));
        tips.SetToolTip(this,AccessibleName+(entries.Length>Capacity?"\n點左右箭頭或滾輪查看其他狀態。":""));Invalidate();
    }
    int Capacity=>Math.Max(1,(Width-36)/140);
    public void ArrangeBeside(Control notice,Rectangle row){
        bool shown=entries.Length>0;Visible=shown;
        int split=shown?row.Width/3:row.Width;
        notice.SetBounds(row.X,row.Y,split,row.Height);
        SetBounds(row.X+split+4,row.Y,Math.Max(1,row.Width-split-4),row.Height);
    }
    void Page(int direction){first=Math.Clamp(first+direction*Capacity,0,Math.Max(0,entries.Length-Capacity));Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){if(e.X<18)Page(-1);else if(e.X>=Width-18)Page(1);}}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Page(e.Delta>0?-1:1);}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);int count=Math.Min(Capacity,entries.Length-first);if(count<=0)return;
        int width=Math.Max(1,(Width-36)/count);var art=CurrentArt;
        for(int i=0;i<count;i++){
            var item=entries[first+i];int x=18+i*width;
            if(art?.ImageFor(item,out var bmp) is {} img&&bmp!=null){
                // 原版圖示格 32×32（.tbt 帶 x／y 位移）；狀態列一列高度不到 32，整格等比縮進列高
                // （縮放是引擎自訂，原版圖示只在右上角原尺寸畫）。
                int size=Math.Clamp(Height-2,8,BuffIconsPanel.IconSize);float k=size/(float)BuffIconsPanel.IconSize;int top=(Height-size)/2;
                e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.DrawImage(bmp,new RectangleF(x+img.OffsetX*k,top+img.OffsetY*k,img.Width*k,img.Height*k));
                TextRenderer.DrawText(e.Graphics,item.Detail,Font,new Rectangle(x+size+3,0,Math.Max(1,width-size-6),Height),Color.White,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
                continue;
            }
            using var brush=new SolidBrush(item.Debuff?Color.Salmon:PlayerHudPainter.Gold);
            if(item.Debuff)e.Graphics.FillPolygon(brush,[new Point(x+7,3),new Point(x+14,17),new Point(x,17)]);
            else e.Graphics.FillPolygon(brush,[new Point(x+7,2),new Point(x+14,10),new Point(x+7,18),new Point(x,10)]);
            TextRenderer.DrawText(e.Graphics,item.Debuff?"−":"+",Font,new Rectangle(x,2,15,18),PlayerHudPainter.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics,item.Detail,Font,new Rectangle(x+17,0,Math.Max(1,width-20),Height),Color.White,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
        }
        if(entries.Length>Capacity){
            TextRenderer.DrawText(e.Graphics,"‹",Font,new Rectangle(0,0,18,Height),first>0?Color.White:Color.Gray,TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics,"›",Font,new Rectangle(Width-18,0,18,Height),first+Capacity<entries.Length?Color.White:Color.Gray,TextFormatFlags.VerticalCenter);
        }
    }
    protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
}
