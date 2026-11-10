namespace P06.NativeUi;

// R9-P（T376／#860）：釣魚取得清單窗 FishObtainUI（FU9 §6.1／§6.2／§14.3、gap-matrix 15.11）。
// 已驗證（靜態）：
//   - 和 UserObtainUI 是同一個建構子 sub_C03330 建的兩個實例（dword_1540860＝FishObtainUI，0xB5791B）；CFishingUI 的
//     FishingNet 鈕（sub_E65B00）不送封包，只開關這個窗；標題是 "[ %s ]"＋字串 3674。
//   - 事件：CloseFishWindow（關閉鈕）、FishItemListScroll（捲軸）、FishKeyboard（Esc 關窗）。
//   - FishObtainUI.xml 本機 0-客戶端 24 個 idx 都沒有；參考客戶端 5 套都有且內容相同（FU9 §6.2），版面與 UserObtainUI.xml
//     相同：根 FishObtainUI、視窗 UserObtain_Window、底 5194／框 5195、Moveable 20、關閉鈕 (225,15) 4429／4430、
//     Total_Label (30,20) String 3674、ObtainNewItem_List (18,37) 194×250。執行期經 UiResources.Shared.Xml（本機 Tile.pak →
//     參考客戶端 Tile.pak）讀取，不複製進 repo；讀不到 → Unavailable，FishingNet 只記錄不開窗。
// 主控裁定（#818）：「釣魚物品表」unk_1542864 的內容與比對欄位（sub_C021B0 的 16 位元欄位）沒查到，3.81 背包封包也不帶模板
//   編號 → **不開清單**：清單列、捲軸都不畫、不列背包全部、不用假清單；ListArea 只保留位置。等物品表查到再做。
// 字串 3674：本機 string-c.tbl 只有 3484 筆（text_tables.py 讀出），沒有 3674 → 標題不畫（不用替代字）；表裡有時才畫。
// 推測（標明，真機再確認）：開窗位置放在釣魚窗右側（放不下放左側，同 UserObtainPanel.Beside）；結束釣魚（釣魚窗的狀態變成
//   非釣魚中）或離開世界時關窗（原版是「結束釣魚時清空清單」，窗是否一起關沒查）。
// --classic-ui 時沒有原生面板；舊式釣魚 HUD 不變。

public sealed class FishObtainPanel(UiResources? resources=null,Func<FishingPanel?>? fishing=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(196)] internal static UiPanelRegistration Registration=>new(PanelId,FishingPanel.HostLayout,root=>new FishObtainPanel(root.Resources,()=>root.Find(FishingPanel.PanelId) as FishingPanel));   // R9-P (#860): FishObtainUI.xml from the reference client, loaded inside Load
    public const string PanelId="FishObtainUI";
    public const string LayoutName="FishObtainUI",RootName="FishObtainUI",WindowName="UserObtain_Window";
    public const string CloseEvent="CloseFishWindow";
    /// <summary>標題字串（sub_E65B00："[ %s ]"＋3674）。</summary>
    public const int TitleStringId=3674;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open; null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public Rectangle Bounds=>open&&!Size.IsEmpty?new(Origin,Size):Rectangle.Empty;
    public bool Visible=>open;
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public UserObtainPanel.Button? CloseButton{get;private set;}
    public Point TitleAt{get;private set;}
    /// <summary>XML Total_Label 的 String（應為 3674）。</summary>
    public int? TitleString{get;private set;}
    /// <summary>ObtainNewItem_List 的位置（視窗內）；清單不畫（等物品表）。</summary>
    public Rectangle ListArea{get;private set;}
    public int Moveable{get;private set;}
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>刻意不建的元素與理由。</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public UserObtainPanel.Button? Pressed{get;private set;}

    bool open,moved;

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"fish-obtain-ui 無法載入 {LayoutName}，FishingNet 不開窗：{ex.Message}");return;}
        try{Load(layout,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"fish-obtain-ui {layout.FileName} 版面不符，停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"fish-obtain-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(!Available)UiResourceLog.Write($"fish-obtain-ui 停用：{Unavailable}");
    }

    void Clear(){
        open=moved=false;Pressed=null;LayoutFile=null;Origin=Point.Empty;Size=Size.Empty;BaseSurface=FrameSurface=null;CloseButton=null;
        TitleAt=Point.Empty;TitleString=null;ListArea=Rectangle.Empty;Moveable=0;Missing=[];Skipped=[];Unavailable="not loaded";
    }

    /// <summary>Offline checks: surfs come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var root=layout.Root;
        if(root.Name!=RootName)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {RootName}");
        var window=root.Child("Window");
        if(window.Attr("Name")!=WindowName)throw new UiLayoutException(layout.FileName,window.Path,$"Window Name 不是 {WindowName}");
        var missing=new List<string>();var skipped=new List<string>();
        UiSurface? S(UiNode n,string attr){
            if(FishingPanel.SurfAttr(n,attr) is not {} r)return null;
            var s=surf(r.Id);
            if(s==null)missing.Add($"surf {r.Id} ({n.Path}/@{attr})");
            return s;
        }
        BaseSurface=S(window,"BaseSurf");FrameSurface=S(window,"FrameSurf");Moveable=window.TryInt("Moveable")??0;
        var close=window.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="CloseWindow_Button");
        CloseButton=close==null?null:new(close.RequiredAttr("Name"),new(close.Int("X"),close.Int("Y")),S(close,"BaseSurf"),S(close,"PushSurf"));
        if(close!=null&&close.Attr("MouseEvent")!=CloseEvent)skipped.Add(close.Path+$"（事件 {close.Attr("MouseEvent")} 不是 {CloseEvent}）");
        var total=window.ChildrenNamed("Label").FirstOrDefault(l=>l.Attr("Name")=="Total_Label");
        TitleAt=total==null?Point.Empty:new(total.TryInt("X")??0,total.TryInt("Y")??0);
        TitleString=total?.Attr("String") is {} sid&&int.TryParse(new string(sid.Trim().TakeWhile(char.IsAsciiDigit).ToArray()),out int id)?id:null;
        var list=window.ChildrenNamed("List").FirstOrDefault(l=>l.Attr("Name")=="ObtainNewItem_List");
        if(list!=null){
            ListArea=new(list.TryInt("X")??0,list.TryInt("Y")??0,list.TryInt("Width")??0,list.TryInt("Height")??0);
            skipped.Add(list.Path+"（清單：釣魚物品表的內容與比對欄位待驗證，#818 裁定不開清單，等物品表）");
        }
        foreach(var w in window.ChildrenNamed("WheelControl"))skipped.Add(w.Path+"（捲軸：清單不開，一起不畫）");
        var rects=new[]{BaseSurface,FrameSurface}.OfType<UiSurface>().Select(s=>new Size(s.Width,s.Height)).ToList();
        Size=rects.Count==0?Size.Empty:new(rects.Max(s=>s.Width),rects.Max(s=>s.Height));
        LayoutFile=layout.FileName;Origin=new(window.TryInt("X")??0,window.TryInt("Y")??0);
        Missing=missing;Skipped=skipped;
        Unavailable=Size.IsEmpty?$"{layout.FileName}: 視窗底圖與外框都缺，無法定出視窗範圍（不用替代圖）":null;
    }

    /// <summary>"[ %s ]"＋字串 3674；字串表沒有這一筆 → null（不畫，不用替代字）。</summary>
    public string? Title{get{
        int id=TitleString??TitleStringId;
        if(ServerMessageText.Table is not {} t||id>=t.Count)return null;
        var s=ServerMessageText.DisplayText(t[id]).Trim();
        return s.Length==0?null:"[ "+s+" ]";
    }}

    /// <summary>FishingNet：開著就關、關著就開。anchor＝釣魚窗範圍（擺位用，推測）。回傳開窗後的 Visible。</summary>
    public bool Toggle(Rectangle anchor){
        if(open){Close();return false;}
        return Open(anchor);
    }
    public bool Open(Rectangle anchor){
        if(!Available)return false;
        open=true;Pressed=null;
        if(!moved&&!anchor.IsEmpty)Origin=UserObtainPanel.Beside(anchor,Size);
        return true;
    }
    public void Close(){open=false;moved=false;Pressed=null;}

    public void Update(IUiGameState state){
        if(!open)return;
        if(!state.InWorld){Close();return;}
        if(fishing?.Invoke() is {} f&&f.State is not {Active:true})Close();   // 推測：結束釣魚時關窗
    }

    public bool IsDragHandle(Point location)=>open&&Moveable>0&&Bounds.Contains(location)&&location.Y<Origin.Y+Moveable&&CloseAt(location)==null;
    public void MoveTo(Point location){moved=true;Origin=location;}

    Rectangle At(Rectangle local)=>local.IsEmpty?Rectangle.Empty:new(Origin.X+local.X,Origin.Y+local.Y,local.Width,local.Height);
    UserObtainPanel.Button? CloseAt(Point p)=>CloseButton is {} b&&At(b.Local).Contains(p)?b:null;

    public void Paint(UiCanvas canvas){
        if(!open)return;
        canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        if(Title is {} title)canvas.DrawText(title,Origin.X+TitleAt.X,Origin.Y+TitleAt.Y,Color.White);
        if(CloseButton is {} b)canvas.DrawSurface(Pressed==b?b.Push??b.Base:b.Base,Origin.X+b.At.X,Origin.Y+b.At.Y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left)Pressed=CloseAt(e.Location);
                return inside;
            case UiMouseKind.Up:{
                var was=Pressed;Pressed=null;
                if(was!=null&&CloseAt(e.Location)==was)Close();
                return inside||was!=null;
            }
            default:return inside||Pressed!=null;
        }
    }

    /// <summary>FishKeyboard：Esc 關窗（已驗證）。</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!open||!e.Down||e.KeyCode!=Keys.Escape)return false;
        Close();return true;
    }
}
