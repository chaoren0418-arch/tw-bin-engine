namespace P06.NativeUi;

// R2-D（T254／#615）PBW-B：原生「離開遊戲」窗 QUIT_WINDOW（uiextra86-c.xml 第 0 段；PBW§4，gap-matrix 第 4.2 節）。
// 已驗證（PBW§4＋XML）：底 1951 (4,8)、框 1952、關閉 X 1962 (147,15)、重新開始 1953 (31,41)、離開遊戲 1956 (31,62)、
//   取消 1959 (31,83)；建構子以 (400 − 寬/2, 150) 擺放；唯一開窗點是死亡逾時（Recovery.cs）。
// 推測（標明）：框 1952 在 XML 沒有座標，比照 TRADE_WINDOW 不畫（不推定 0,0）；「寬」取已畫元件的右緣；
//   STAGE3_BTN 的三張圖照 UserPlayUI.xml 同一組 surf 的用法：n＝平常、n+1＝按下、n+2＝滑鼠停留；Esc＝取消。
// 不做：拖曳（surf 160 拖曳條的寬度來源沒查）、ESC／選單開窗（PBW§4 待驗證）。

/// <summary>QUIT_WINDOW 的資料與動作（Recovery.cs 的 ClientForm 依 QuitWindowRules 轉呼叫既有入口）。</summary>
public interface IQuitWindowSource {
    QuitWindowView? Current {get;}
    void Restart();
    void Quit();
    void Close();
}

public sealed class QuitWindowPanel:IUiClosablePanel {
    [UiPanel(160)] internal static UiPanelRegistration Registration=>new(QuitWindowPanel.PanelId,QuitWindowPanel.LayoutName,_=>new QuitWindowPanel());   // R2-D (#615) PBW-B: QUIT_WINDOW (uiextra86)
    public const string PanelId="QUIT_WINDOW";
    public const string LayoutName="uiextra86";
    public const int CenterX=400,ScreenY=150;
    const string BaseName="SURF_RENEWAL_WIN_QUIT_BASE",CloseName="SURF_RENEWAL_BTN_QUIT_X",
        RestartName="SURF_RENEWAL_BTN_QUIT_RESTART",QuitName="SURF_RENEWAL_BTN_QUIT_QUIT",CancelName="SURF_RENEWAL_BTN_QUIT_CANCEL";

    /// <summary>XML 元件：第一個 surf、相對窗原點的位置、狀態圖（平常／按下／停留；null＝缺圖）。</summary>
    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface,UiSurface? Push=null,UiSurface? Highlight=null) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get;private set;}
    public bool Visible=>view!=null;
    public IQuitWindowSource? Source{get;set;}
    public Element? Base{get;private set;}
    public Element? CloseButton{get;private set;}
    public Element? Restart{get;private set;}
    public Element? Quit{get;private set;}
    public Element? Cancel{get;private set;}
    public QuitWindowView? View=>view;
    public Element? Hover{get;private set;}
    public Element? Pressed{get;private set;}
    QuitWindowView? view;int closedRevision=-1;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(PanelId);
        var windowAt=new Point(window.Int("x"),window.Int("y"));
        Element Find(string name,bool stage3){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            // SURF_IMAGE／IMAGE_BTN use xPos/yPos; STAGE3_BTN uses xRPos/yRPos (window-relative). Missing → throw, no default.
            var at=n.Attr("xPos")!=null||n.Attr("yPos")!=null?new Point(n.Int("xPos"),n.Int("yPos")):new Point(n.Int("xRPos"),n.Int("yRPos"));
            int first=ids[0];
            return stage3?new(name,first,at,surf(first),surf(first+1),surf(first+2)):new(name,first,at,surf(first));
        }
        Base=Find(BaseName,false);CloseButton=Find(CloseName,false);
        Restart=Find(RestartName,true);Quit=Find(QuitName,true);Cancel=Find(CancelName,true);
        var union=All.Where(e=>e.Drawn).Select(e=>e.Local).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        // Constructor: translate by (400 − width/2, 150). Width = right edge of the drawn elements (frame 1952 has no coordinates).
        int width=union.IsEmpty?0:union.Right;
        Origin=new(windowAt.X+CenterX-width/2,windowAt.Y+ScreenY);
        Bounds=union.IsEmpty?Rectangle.Empty:new(Origin.X+union.X,Origin.Y+union.Y,union.Width,union.Height);
    }
    IEnumerable<Element> All=>new[]{Base,CloseButton,Restart,Quit,Cancel}.OfType<Element>();
    IEnumerable<Element> Buttons=>new[]{CloseButton,Restart,Quit,Cancel}.OfType<Element>();

    public void Update(IUiGameState state){
        // Not gated on InWorld: the death-timeout window opens after the connection is closed (PBW§4).
        var v=Source?.Current;
        if(v!=null&&v.Revision==closedRevision)v=null;
        if(v==null){Hover=null;Pressed=null;}
        view=v;
    }

    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    public Element? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b).Contains(p));
    public UiSurface? StateSurface(Element e){
        if(Pressed==e&&Hover==e)return e.Push??e.Surface;
        if(Hover==e)return e.Highlight??e.Surface;
        return e.Surface;
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var e in All)if(StateSurface(e) is {} s)canvas.DrawSurface(s,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
    }

    void Hide(){if(view!=null)closedRevision=view.Revision;view=null;Hover=null;Pressed=null;}
    void Fire(Element e){
        var source=Source;
        if(e==Restart){source?.Restart();return;}
        if(e==Quit){source?.Quit();return;}
        Close();   // 取消、關閉 X：sub_A59AD0 只關窗
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:Hover=hit;return Bounds.Contains(e.Location)||Pressed!=null;
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left){Hover=hit;Pressed=hit;}
                return Bounds.Contains(e.Location);
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
        Close();return true;
    }

    /// <summary>
    /// R7-H（#810）：堆疊關窗（被擠出、全域 ESC，sub_A57C60 的 vtable+36）＝取消鈕：只關窗（sub_A59AD0），不重新開始、不離開。
    /// 已關著時不呼叫 Source。
    /// </summary>
    public void Close(){
        if(view==null)return;
        Hide();Source?.Close();
    }
}
