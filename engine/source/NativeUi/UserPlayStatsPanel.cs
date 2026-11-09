namespace P06.NativeUi;

// R2-D（T254／#615）：死亡統計窗 UserPlayStatsUI 的原生面板（UserPlayUI.xml，WO§6；gap-matrix 第 4.1 節）。
// 已驗證（XML）：底 BaseSurf 5322、外框 FrameSurf 5323；重新開始 (66,40) 1953／1955／1954、結束 (66,61) 1956–1958、
//   取消 (66,82) 1959–1961、關閉 (215,15) 4429／4430；統計清單 (34,120)，四列的圖示、名稱（字串 3123–3126）與數值欄寬。
// 推測（標明）：外框疊在底圖上、兩者都畫在視窗原點；按鈕狀態 BaseSurf＝平常、HighlightSurf＝滑鼠停留、PushSurf＝按下；
//   Moveable="20"＝頂端 20 px 可拖曳；視窗畫面位置（XML 是 0,0）照 QUIT_WINDOW 的已驗證規則水平置中、y＝150；Esc＝取消。
// 不做（等 Q-2 或沒有來源）：統計數值（sub_C0AB50 沒查，顯示「—」，不顯示合成的 0）、NewItemList／Reset 鈕、地監時間清單。

public sealed record UserPlayStatsView(bool CanRestart);

/// <summary>UserPlayStatsUI 的資料與動作（由 Recovery.cs 的 ClientForm 接上既有死亡流程；面板不碰 LoginConnection）。</summary>
public interface IUserPlayStatsSource {
    /// <summary>null＝不開窗（活著、已取消或延後中）。</summary>
    UserPlayStatsView? Current {get;}
    void Restart();
    void Quit();
    /// <summary>取消／關閉／Esc：只關窗，計時照走（DeathWatch.Dismiss）。</summary>
    void Dismiss();
}

public sealed class UserPlayStatsPanel:IUiMovablePanel {
    [UiPanel(150)] internal static UiPanelRegistration Registration=>new(UserPlayStatsPanel.PanelId,UserPlayStatsPanel.LayoutName,_=>new UserPlayStatsPanel());   // R2-D (#615): death window (UserPlayUI.xml)
    public const string PanelId=UserPlayStatsLayout.RootName;
    public const string LayoutName=UserPlayStatsLayout.FileName;
    /// <summary>推測：沿用 QUIT_WINDOW 已驗證的畫面位置（400 − 寬/2, 150）。</summary>
    public const int ScreenY=150;
    public const string ValuePlaceholder="—";

    public sealed record Button(UserPlayStatsButton Spec,UiSurface? Base,UiSurface? Highlight,UiSurface? Push) {
        public Rectangle Local=>Base==null?Rectangle.Empty:new(Spec.X,Spec.Y,Base.Width,Base.Height);
    }
    public sealed record Row(UserPlayStatsRow Spec,UiSurface? Icon,string Label,bool LabelFromTable);

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get;private set;}
    public bool Visible=>view!=null;
    public IUserPlayStatsSource? Source{get;set;}
    public UserPlayStatsLayout? Layout{get;private set;}
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public Button? Restart{get;private set;}
    public Button? Quit{get;private set;}
    public Button? Cancel{get;private set;}
    public Button? Close{get;private set;}
    public IReadOnlyList<Row> Rows{get;private set;}=[];
    public UserPlayStatsView? View=>view;
    /// <summary>滑鼠停留／按下中的按鈕（狀態圖用）。</summary>
    public Button? Hover{get;private set;}
    public Button? Pressed{get;private set;}
    UserPlayStatsView? view;Size size;bool placed;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var l=UserPlayStatsLayout.From(layout);Layout=l;
        UiSurface? S(int? id)=>id is int n?surf(n):null;
        BaseSurface=S(l.BaseSurf);FrameSurface=S(l.FrameSurf);
        Button B(UserPlayStatsButton spec)=>new(spec,S(spec.BaseSurf),S(spec.HighlightSurf),S(spec.PushSurf));
        Restart=B(l.Restart);Quit=B(l.Quit);Cancel=B(l.Cancel);Close=l.Close is {} c?B(c):null;
        Rows=l.Rows.Select(r=>{var (text,table)=UserPlayStatsText.Label(r.StringId,r.Name);return new Row(r,S(r.IconSurf),text,table);}).ToArray();
        var rects=new List<Rectangle>();
        if(BaseSurface!=null)rects.Add(new(0,0,BaseSurface.Width,BaseSurface.Height));
        if(FrameSurface!=null)rects.Add(new(0,0,FrameSurface.Width,FrameSurface.Height));
        rects.AddRange(Buttons.Select(b=>b.Local).Where(r=>!r.IsEmpty));
        var union=rects.Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        size=union.IsEmpty?Size.Empty:new(union.Right,union.Bottom);
        Place(new(UiRoot.LogicalWidth/2-size.Width/2+l.WindowX,ScreenY+l.WindowY));placed=false;
    }
    IEnumerable<Button> Buttons=>new[]{Restart,Quit,Cancel,Close}.OfType<Button>();
    void Place(Point origin){Origin=origin;Bounds=size.IsEmpty?Rectangle.Empty:new(origin,size);}

    public bool IsDragHandle(Point location)=>Layout?.Moveable is int h&&h>0&&Bounds.Contains(location)&&location.Y<Origin.Y+h&&ButtonAt(location)==null;
    public void MoveTo(Point location){Place(location);placed=true;}
    public bool Moved=>placed;

    public void Update(IUiGameState state){
        var v=Source?.Current;
        if(v==null){Hover=null;Pressed=null;}
        view=v;
    }

    Rectangle At(Button b)=>b.Local.IsEmpty?Rectangle.Empty:new(Origin.X+b.Local.X,Origin.Y+b.Local.Y,b.Local.Width,b.Local.Height);
    public Button? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b).Contains(p));
    /// <summary>狀態圖（推測，見檔頭）：按下中且滑鼠在鈕上→PushSurf；停留→HighlightSurf；缺圖退回 BaseSurf。重新開始不可用時只畫 BaseSurf。</summary>
    public UiSurface? StateSurface(Button b){
        if(b==Restart&&view is {CanRestart:false})return b.Base;
        if(Pressed==b&&Hover==b)return b.Push??b.Base;
        if(Hover==b)return b.Highlight??b.Base;
        return b.Base;
    }
    /// <summary>統計列在畫布上的位置（清單 X/Y＋列高；列內 圖示 → 間距 → 名稱 → 間距 → 數值，推測）。</summary>
    public (Point Icon,Rectangle Label,Rectangle Value) RowRects(int index){
        var l=Layout!;var r=Rows[index].Spec;int x=Origin.X+l.ListX,y=Origin.Y+l.ListY+index*l.RowHeight;
        var icon=new Point(x,y+r.IconY);
        if(Rows[index].Icon is {} s)x+=s.Width+r.Gap;
        var label=new Rectangle(x,y,r.LabelWidth,r.Height);
        return (icon,label,new Rectangle(label.Right+r.Gap,y,r.ValueWidth,r.Height));
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        if(BaseSurface!=null)canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);
        if(FrameSurface!=null)canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        foreach(var b in Buttons)if(StateSurface(b) is {} s)canvas.DrawSurface(s,Origin.X+b.Spec.X,Origin.Y+b.Spec.Y);
        for(int i=0;i<Rows.Count;i++){
            var row=Rows[i];var (icon,label,value)=RowRects(i);
            if(row.Icon!=null)canvas.DrawSurface(row.Icon,icon.X,icon.Y);
            canvas.DrawText(row.Label,label.X,label.Y,row.Spec.LabelColor);
            int w=UiText.Measure(canvas.Graphics,ValuePlaceholder).Width;
            canvas.DrawText(ValuePlaceholder,value.Right-w,value.Y,row.Spec.ValueColor);
        }
    }

    void Fire(Button b){
        if(Source==null)return;
        if(b==Restart){if(view is {CanRestart:true})Source.Restart();}
        else if(b==Quit)Source.Quit();
        else Source.Dismiss();
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:Hover=hit;return Bounds.Contains(e.Location)||Pressed!=null;
            case UiMouseKind.Down:
                if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location);
                Hover=hit;Pressed=hit;return Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var pressed=Pressed;Pressed=null;Hover=hit;
                if(pressed!=null&&pressed==hit)Fire(pressed);
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down||e.KeyCode!=Keys.Escape)return false;
        Source?.Dismiss();return true;
    }
}
