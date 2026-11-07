using System.Drawing.Drawing2D;
namespace P06;

public sealed partial class ClientForm {
    internal MinimapSnapshot? ReadMinimap()=>login.CaptureWorld(()=>PlayerHudActive?MinimapSnapshot.Capture(login.World):null);
    internal static bool MinimapKeyAllowed(Keys key,bool active,bool inputAllowed,bool focused)=>key==Keys.F5&&active&&inputAllowed&&focused;
    bool ProcessMinimapKeyboard(ref Message msg,Keys key){
        if(!MinimapKeyAllowed(key,PlayerHudActive,GameInputAllowed,Focused))return false;
        if(((long)msg.LParam&(1L<<30))==0)Controls.OfType<MinimapUi>().FirstOrDefault()?.Toggle();
        return true;
    }
}

// Owned by the HUD but mounted above the scene so opening it never resizes the HUD.
public sealed class MinimapUi:Panel {
    readonly Minimap model=new();
    readonly System.Windows.Forms.Timer refresh=new(){Interval=200};
    readonly Button toggle,minus,plus,close;
    readonly ToolTip tips=new();
    readonly PlayerHudView hud;
    ClientForm? host;
    MinimapUi(PlayerHudView hud,FlowLayoutPanel menu){
        this.hud=hud;DoubleBuffered=true;BackColor=PlayerHudPainter.Ink;ForeColor=Color.White;
        AccessibleName="小地圖，已載入區域";TabStop=false;
        toggle=MakeButton("小地圖 F5",94,Toggle);menu.Controls.Add(toggle);
        minus=MakeButton("−",30,()=>Zoom(-1));plus=MakeButton("＋",30,()=>Zoom(1));close=MakeButton("×",30,Toggle);
        minus.AccessibleName="縮小小地圖";plus.AccessibleName="放大小地圖";close.AccessibleName="關閉小地圖";
        Controls.AddRange([minus,plus,close]);
        tips.SetToolTip(this,"白點：自己；青點：隊友；金點：NPC／非玩家物件（含怪物）。不在視野內的隊友位置約每 5 秒更新。只顯示已載入區域；＋／− 或滾輪縮放。F5 開關。");
        hud.ParentChanged+=HudChanged;hud.VisibleChanged+=HudChanged;hud.Disposed+=HudDisposed;
        refresh.Tick+=(_,_)=>RefreshMap();RefreshMap();
    }
    public static void Attach(PlayerHudView hud,FlowLayoutPanel menu)=>_ =new MinimapUi(hud,menu);
    Button MakeButton(string text,int width,Action action){
        var button=new Button{Text=text,AccessibleName=text,Width=width,Height=28,Margin=new(3,2,3,2),FlatStyle=FlatStyle.Flat,BackColor=PlayerHudPainter.Panel,ForeColor=Color.White};
        button.Click+=(_,_)=>{action();if(host!=null){host.ActiveControl=null;host.Focus();}};return button;
    }
    void HudChanged(object? sender,EventArgs e)=>RefreshMap();
    void HudDisposed(object? sender,EventArgs e)=>Dispose();
    public void Toggle(){model.Toggle();RefreshMap();}
    void Zoom(int direction){model.ZoomBy(direction);Invalidate();}
    void RefreshMap(){
        if(IsDisposed)return;
        if(host==null&&hud.FindForm() is ClientForm form){host=form;host.Controls.Add(this);Font=hud.Font;}
        bool active=host!=null&&hud.Visible;
        refresh.Enabled=active;
        if(!active||!model.IsOpen){Visible=false;model.Clear();return;}
        var scene=host!.PlayerSceneBounds;
        // Tiny viewports leave the map accessible via F5 without covering the HUD.
        if(scene.Width<180||scene.Height<140){Visible=false;model.Clear();return;}
        int width=Math.Min(272,scene.Width-12),height=Math.Min(218,scene.Height-12);
        SetBounds(scene.Right-width-6,scene.Top+6,width,height);
        minus.SetBounds(Width-96,3,30,26);plus.SetBounds(Width-65,3,30,26);close.SetBounds(Width-34,3,30,26);
        model.Update(host.ReadMinimap());Visible=true;BringToFront();
        minus.Enabled=model.Zoom>1;plus.Enabled=model.Zoom<4;
        AccessibleDescription=model.Snapshot is {} state?$"地圖 {state.MapId}，{model.Zoom} 倍；白點自己，青點隊友，金點 NPC／非玩家物件；已載入區域":"等待目前地圖快取";
        Invalidate();
    }
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(e.Delta!=0)Zoom(Math.Sign(e.Delta));}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);
        TextRenderer.DrawText(e.Graphics,$"小地圖 ×{model.Zoom}",Font,new Rectangle(7,3,Width-105,26),PlayerHudPainter.Gold,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        var area=new Rectangle(6,34,Width-12,Height-61);
        if(model.Image is not {} image||model.Snapshot is not {} snapshot||snapshot.Markers.LastOrDefault(m=>m.Self) is not {} self){
            TextRenderer.DrawText(e.Graphics,"等待目前地圖快取",Font,area,Color.Silver,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;
        }
        var source=image.Viewport(model.Zoom,self);float scale=Math.Min(area.Width/source.Width,area.Height/source.Height);
        var target=new RectangleF(area.X+(area.Width-source.Width*scale)/2,area.Y+(area.Height-source.Height*scale)/2,source.Width*scale,source.Height*scale);
        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        e.Graphics.DrawImage(image.Bitmap,target,source,GraphicsUnit.Pixel);
        var saved=e.Graphics.Save();e.Graphics.SetClip(area);
        foreach(var marker in snapshot.Markers){
            var p=image.Project(marker.X,marker.Y);if(!source.Contains(p))continue;
            float x=target.X+(p.X-source.X)*scale,y=target.Y+(p.Y-source.Y)*scale,r=marker.Self?4:marker.Party?3:2;
            e.Graphics.FillEllipse(Brushes.Black,x-r-1,y-r-1,r*2+2,r*2+2);
            using var color=new SolidBrush(marker.Self?Color.White:marker.Party?Color.Cyan:PlayerHudPainter.Gold);e.Graphics.FillEllipse(color,x-r,y-r,r*2,r*2);
        }
        e.Graphics.Restore(saved);
        TextRenderer.DrawText(e.Graphics,$"地圖 {snapshot.MapId} · 已載入區域 · 白我／青隊友／金NPC",Font,new Rectangle(6,Height-24,Width-12,22),Color.Silver,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
    }
    protected override void Dispose(bool disposing){
        if(disposing){hud.ParentChanged-=HudChanged;hud.VisibleChanged-=HudChanged;hud.Disposed-=HudDisposed;refresh.Dispose();model.Dispose();tips.Dispose();toggle.Dispose();}
        base.Dispose(disposing);
    }
}
