namespace P06.NativeUi;

// T290（#698）：UserObtainUI 新物品清單窗（UserObtainUI.xml；gap-matrix 4.1、SS§3.1、FU3§4.2）。
// 已驗證：死亡統計窗的 NewItemList 鈕（sub_C0B470）在新物品清單不空時，於統計窗旁開這個窗——右側放不下就放左側（以 800 為界），
//   標題 "** %s **"＋字串 3126；資料是新物品清單 dword_154085C（WorldSession.UserPlayStats().Obtained）；重設鈕會關掉它（sub_C0BA30）。
// 已驗證（XML）：底 5194、外框 5195、Moveable 20；關閉鈕 (225,15) 4429／4430；Total_Label (30,20) String 3126；
//   ObtainNewItem_List (18,37) 194×250 Vertical Gap 1，7 個 CheckButton 列（高 35）：ItemIcon 34×35 → Gap 1 → ItemInfo_List（Y 2；
//   名稱 (4,4) 寬 140、數量 (130,5) 寬 19 靠右、底色黑）；捲軸 ItemsList_Scrollbar 在清單 (199,0)：底圖 5196 (0,14)、上 (0,0) 4178／4179、
//   下 (0,236) 4180／4181、拖曳鈕 (0,16) 4182／4183，ScrollStart 16、ScrollHeight 224。
// 推測（標明）：Y 與統計窗頂端對齊；名稱與數量標籤以 ItemInfo_List 的原點加 X／Y 擺放（兩個標籤在同一行）；數量欄寫差額；
//   捲軸每格一列、拖曳鈕位置依比例；Esc＝關閉。
// 已驗證（FU9 §6.1，R9-P／#860 更正）：列的 CheckButton 原版沒有回呼——ItemsListClick 在這個類別（建構子 sub_C03330）沒有
//   處理函式，整個 BIN 只在 ShopUI 建構子 sub_DEDC50 引用這個字串；XML 的 CheckButton 都是 Activate="0" → 不做選取。
//   滑鼠只認 CloseWindow、ItemListScroll。同一建構子的第二個實例 FishObtainUI 見 FishObtainPanel.cs。
// 沒做：Desc_Tooltip（Opacity="0"，未接）。
// 圖示來源：ItemIconSource（Sprite "<icon>.tbt"，R4-B），執行期從原客戶端讀；沒有圖示時只畫字。

public sealed class UserObtainPanel:IUiMovablePanel,IUiClosablePanel {
    [UiPanel(151)] internal static UiPanelRegistration Registration=>new(PanelId,LayoutName,root=>new UserObtainPanel(()=>root.Find(UserPlayStatsPanel.PanelId) as UserPlayStatsPanel,ItemIconSource.FromRoot(root)));   // T290 (#698): UserObtainUI
    public const string PanelId="UserObtainUI";
    public const string LayoutName="UserObtainUI";
    public const int ScreenWidth=UiRoot.LogicalWidth;

    public sealed record Button(string Name,Point At,UiSurface? Base,UiSurface? Push){
        public Rectangle Local=>Base==null?Rectangle.Empty:new(At.X,At.Y,Base.Width,Base.Height);
    }
    /// <summary>一列的幾何（視窗內座標）。</summary>
    public sealed record RowGeometry(Rectangle Row,Rectangle Icon,Point Name,int NameWidth,Rectangle Count);

    readonly Func<UserPlayStatsPanel?> stats;
    public ItemIconSource Icons{get;set;}
    public UserObtainPanel(Func<UserPlayStatsPanel?> stats,ItemIconSource? icons=null){this.stats=stats;Icons=icons??ItemIconSource.None;}

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds{get;private set;}
    public Point Origin{get;private set;}
    public bool Visible=>open;
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public Button? CloseButton{get;private set;}
    public Button? Up{get;private set;}
    public Button? Down{get;private set;}
    public Button? Thumb{get;private set;}
    public UiSurface? ScrollBack{get;private set;}
    public Point ScrollBackAt{get;private set;}
    public Point TitleAt{get;private set;}
    public int? TitleStringId{get;private set;}
    public Rectangle ListArea{get;private set;}
    public IReadOnlyList<RowGeometry> Rows{get;private set;}=[];
    public int ScrollStart{get;private set;}
    public int ScrollHeight{get;private set;}
    public int? Moveable{get;private set;}
    /// <summary>第一個顯示的物品索引（捲軸位置）。</summary>
    public int Scroll{get;private set;}
    public IReadOnlyList<UserObtainEntry> Entries=>entries;
    public Button? Pressed{get;private set;}
    IReadOnlyList<UserObtainEntry> entries=[];
    bool open,moved;Size size;Point scrollAt;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var root=layout.Root;
        if(root.Name!=PanelId)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {PanelId}");
        var window=root.Child("Window");
        UiSurface? S(int? id)=>id is int n?surf(n):null;
        int? F(UiNode n,string a)=>UserPlayStatsLayout.FirstSurf(n,a);
        BaseSurface=S(F(window,"BaseSurf"));FrameSurface=S(F(window,"FrameSurf"));Moveable=window.TryInt("Moveable");
        Button B(UiNode n,Point offset)=>new(n.RequiredAttr("Name"),new(offset.X+n.Int("X"),offset.Y+n.Int("Y")),S(F(n,"BaseSurf")),S(F(n,"PushSurf")));
        CloseButton=window.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="CloseWindow_Button") is {} c?B(c,Point.Empty):null;
        var total=window.ChildrenNamed("Label").FirstOrDefault(l=>l.Attr("Name")=="Total_Label");
        TitleAt=total==null?Point.Empty:new(total.Int("X"),total.Int("Y"));
        TitleStringId=total?.Attr("String") is {} sid&&int.TryParse(new string(sid.Trim().TakeWhile(char.IsAsciiDigit).ToArray()),out int id)?id:null;
        var list=window.ChildrenNamed("List").SingleOrDefault(l=>l.Attr("Name")=="ObtainNewItem_List")
            ??throw new UiLayoutException(layout.FileName,window.Path+"/List[@Name=ObtainNewItem_List]","缺少節點");
        int lx=list.Int("X"),ly=list.Int("Y"),gap=list.TryInt("Gap")??0;
        ListArea=new(lx,ly,list.Int("Width"),list.Int("Height"));
        var rows=new List<RowGeometry>();int y=ly;
        foreach(var row in list.ChildrenNamed("CheckButton")){
            int h=row.Int("Height"),w=row.Int("Width");
            var inner=row.ChildrenNamed("List").FirstOrDefault()??throw new UiLayoutException(layout.FileName,row.Path,"列需要 Item_List");
            var icon=inner.ChildrenNamed("ItemIcon").FirstOrDefault()??throw new UiLayoutException(layout.FileName,inner.Path,"列需要 ItemIcon");
            var info=inner.ChildrenNamed("List").FirstOrDefault()??throw new UiLayoutException(layout.FileName,inner.Path,"列需要 ItemInfo_List");
            var labels=info.ChildrenNamed("Label").ToList();
            if(labels.Count<2)throw new UiLayoutException(layout.FileName,info.Path,"列需要名稱與數量兩個 Label");
            int iw=icon.Int("Width"),ih=icon.Int("Height"),ig=inner.TryInt("Gap")??0;
            int ix=lx+iw+ig,iy=y+(info.TryInt("Y")??0);
            var name=labels[0];var count=labels[1];
            rows.Add(new(new(lx,y,w,h),new(lx,y,iw,ih),new(ix+(name.TryInt("X")??0),iy+(name.TryInt("Y")??0)),name.TryInt("Width")??0,
                new(ix+(count.TryInt("X")??0),iy+(count.TryInt("Y")??0),count.TryInt("Width")??0,count.TryInt("Height")??0)));
            y+=h+gap;
        }
        Rows=rows;
        Up=Down=Thumb=null;ScrollBack=null;ScrollStart=ScrollHeight=0;
        if(window.ChildrenNamed("WheelControl").FirstOrDefault() is {} wheel&&wheel.ChildrenNamed("ScrollBar").FirstOrDefault() is {} bar){
            scrollAt=new(wheel.Int("X")+bar.Int("X"),wheel.Int("Y")+bar.Int("Y"));
            ScrollStart=bar.TryInt("ScrollStart")??0;ScrollHeight=bar.TryInt("ScrollHeight")??0;
            if(bar.ChildrenNamed("Image").FirstOrDefault() is {} back){ScrollBack=S(F(back,"Surf"));ScrollBackAt=new(scrollAt.X+(back.TryInt("X")??0),scrollAt.Y+(back.TryInt("Y")??0));}
            foreach(var b in bar.ChildrenNamed("Button")){
                var btn=B(b,scrollAt);
                switch(btn.Name){case "Up_Button":Up=btn;break;case "Down_Button":Down=btn;break;case "Move_Button":Thumb=btn;break;}
            }
        }
        var rects=new List<Rectangle>();
        if(BaseSurface!=null)rects.Add(new(0,0,BaseSurface.Width,BaseSurface.Height));
        if(FrameSurface!=null)rects.Add(new(0,0,FrameSurface.Width,FrameSurface.Height));
        var union=rects.Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        size=union.IsEmpty?Size.Empty:new(union.Right,union.Bottom);
        open=false;moved=false;Place(Point.Empty);
    }
    void Place(Point origin){Origin=origin;Bounds=size.IsEmpty||!open?Rectangle.Empty:new(origin,size);}

    /// <summary>sub_C0B470 的擺法：統計窗右側放得下（右緣 ≤ 800）就放右側，否則放左側；Y 對齊統計窗（推測）。</summary>
    public static Point Beside(Rectangle stats,Size window)=>stats.Right+window.Width<=ScreenWidth?new(stats.Right,stats.Top):new(stats.Left-window.Width,stats.Top);
    public int MaxScroll=>Math.Max(0,entries.Count-Rows.Count);
    public string Title=>TitleStringId is int id?"** "+UserPlayStatsText.Label(id,"").Text+" **":"";

    public void Update(IUiGameState state){
        var s=stats();
        bool want=s!=null&&s.Visible&&s.ObtainListOpen&&!s.Bounds.IsEmpty&&!size.IsEmpty;
        entries=want?s!.View?.Values?.Obtained??[]:[];
        if(!want){if(open){open=false;moved=false;Pressed=null;Scroll=0;}Place(Origin);return;}
        bool opening=!open;open=true;
        if(opening){Scroll=0;moved=false;}
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
        Place(moved?Origin:Beside(s!.Bounds,size));
    }

    public bool IsDragHandle(Point location)=>Moveable is int h&&h>0&&Bounds.Contains(location)&&location.Y<Origin.Y+h&&ButtonAt(location)==null;
    public void MoveTo(Point location){moved=true;Place(location);}
    public void Close(){if(stats() is {} s)s.ObtainListOpen=false;open=false;moved=false;Pressed=null;Place(Origin);}

    IEnumerable<Button> Buttons=>new[]{CloseButton,Up,Down}.OfType<Button>();
    Rectangle At(Rectangle local)=>local.IsEmpty?Rectangle.Empty:new(Origin.X+local.X,Origin.Y+local.Y,local.Width,local.Height);
    public Button? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b.Local).Contains(p));
    /// <summary>拖曳鈕位置（視窗內）：ScrollStart＋(ScrollHeight − 鈕高) × Scroll／MaxScroll（推測）。</summary>
    public Point ThumbAt(){
        if(Thumb==null)return Point.Empty;
        int h=Thumb.Base?.Height??0,range=Math.Max(0,ScrollHeight-h);
        return new(Thumb.At.X,scrollAt.Y+ScrollStart+(MaxScroll==0?0:range*Scroll/MaxScroll));
    }
    public void ScrollBy(int delta)=>Scroll=Math.Clamp(Scroll+delta,0,MaxScroll);

    public void Paint(UiCanvas canvas){
        if(!open)return;
        canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        if(Title.Length>0)canvas.DrawText(Title,Origin.X+TitleAt.X,Origin.Y+TitleAt.Y,Color.White);
        for(int i=0;i<Rows.Count&&Scroll+i<entries.Count;i++){
            var e=entries[Scroll+i];var g=Rows[i];
            ItemIconSource.Draw(canvas,Icons.Get(e.Icon),new(Origin.X+g.Icon.X,Origin.Y+g.Icon.Y));
            string name=ItemText.Resolve(e.Name);
            if(name.Length>0)canvas.DrawText(name,Origin.X+g.Name.X,Origin.Y+g.Name.Y,Color.White);
            string count=e.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture);int w=UiText.Measure(canvas.Graphics,count).Width;
            canvas.DrawText(count,Origin.X+g.Count.Right-w,Origin.Y+g.Count.Y,Color.White,Color.Black);
        }
        canvas.DrawSurface(ScrollBack,Origin.X+ScrollBackAt.X,Origin.Y+ScrollBackAt.Y);
        foreach(var b in Buttons)canvas.DrawSurface(Pressed==b?b.Push??b.Base:b.Base,Origin.X+b.At.X,Origin.Y+b.At.Y);
        if(Thumb!=null){var t=ThumbAt();canvas.DrawSurface(Thumb.Base,Origin.X+t.X,Origin.Y+t.Y);}
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Wheel:
                if(!At(ListArea).Contains(e.Location)&&!Bounds.Contains(e.Location))return false;
                ScrollBy(e.Delta>0?-1:e.Delta<0?1:0);return true;
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left)Pressed=hit;
                return Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var pressed=Pressed;Pressed=null;
                if(pressed!=null&&pressed==hit){
                    if(pressed==CloseButton)Close();
                    else if(pressed==Up)ScrollBy(-1);
                    else if(pressed==Down)ScrollBy(1);
                }
                return true;
            }
            default:return Bounds.Contains(e.Location)||Pressed!=null;
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!open||!e.Down||e.KeyCode!=Keys.Escape)return false;
        Close();return true;
    }
}
