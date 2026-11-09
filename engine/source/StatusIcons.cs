namespace P06;

/// <summary>WinForms 狀態列的一筆。ValueText 非 null 時是沒有倒數的狀態（S250 75／82／180），顯示文字取代秒數。</summary>
public sealed record StatusIconEntry(int Id,string Name,int Seconds,bool Debuff,bool Approximate,string? ValueText=null) {
    public string Detail=>ValueText!=null?$"{Name} · {ValueText}":$"{Name} · {(Approximate?"約":"")}{Seconds} 秒";
    /// <summary>T265：S250 B2 圖示的 Id＝−(1_000_000＋effect)，不和 MagicBuffs 的技能鍵相撞。</summary>
    public static int PacketBoxId(int effect)=>-1_000_000-effect;
}

public static class StatusIconFeed {
    /// <summary>
    /// 狀態列內容：MagicBuffs 的倒數狀態（秒數 &gt; 0），再接 S250 75／82／180 的圖示（T265，資料見 PacketBoxStatusIcons）。
    /// 75 有預設秒數但引擎不知道 effect 135 的 duration（在 effectlist2），所以只顯示階段，不顯示秒數。
    /// </summary>
    public static StatusIconEntry[] Read(WorldSession w){
        long now=w.MagicStatusClock();
        var timed=w.MagicBuffs.Select(b=>new StatusIconEntry(b.Skill,b.Name,b.Remaining(now),b.Debuff,b.Approximate)).Where(b=>b.Seconds>0);
        var boxed=w.PacketBoxStatusIcons.Select(i=>new StatusIconEntry(StatusIconEntry.PacketBoxId(i.Effect),i.Name,0,false,false,i.ValueText));
        return [..timed,..boxed];
    }
}

public sealed partial class ClientForm {
    internal StatusIconEntry[] ReadStatusIcons()=>PlayerHudActive?StatusIconFeed.Read(login.World):[];
}

// Compact HUD badges use original vector symbols; no packaged game artwork is required.
// Overflow remains accessible through the arrow buttons, wheel and full-list tooltip.
public sealed class StatusIcons:Control {
    readonly ToolTip tips=new(){AutoPopDelay=15000};
    StatusIconEntry[] entries=[];int first;
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
        int width=Math.Max(1,(Width-36)/count);
        for(int i=0;i<count;i++){
            var item=entries[first+i];int x=18+i*width;
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
