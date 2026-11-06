namespace P06;

public sealed record StatusIconEntry(int Id,string Name,int Seconds,bool Debuff,bool Approximate) {
    public string Detail=>$"{Name} · {(Approximate?"約":"")}{Seconds} 秒";
}

public sealed partial class ClientForm {
    internal StatusIconEntry[] ReadStatusIcons(){
        if(!PlayerHudActive)return [];
        var w=login.World;long now=w.MagicStatusClock();
        return w.MagicBuffs.Select(b=>new StatusIconEntry(b.Skill,b.Name,b.Remaining(now),b.Debuff,b.Approximate)).Where(b=>b.Seconds>0).ToArray();
    }
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
