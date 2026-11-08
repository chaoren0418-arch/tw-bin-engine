using System.Globalization;
namespace P06.NativeUi;

/// <summary>
/// M7-T（T220／#527）：TW16 `PersonalShopUI`（個人商店開店設定窗，WIN§1.1）。版面在執行期從原客戶端 `PersonalShopUI.xml`
/// （Tile.pak）讀出；本檔沒有寫死任何座標或 surf，只寫死 XML 的元素名稱。
///
/// 已驗證（XML／WIN§1.1）：底圖、框、關閉鈕、出售與收購各 2×4 格（33×33，間距 1）、兩個訊息欄（LimitText=20）、
/// 3 張外型圖與 CheckButton、4 個翻頁鈕與頁碼、確定／取消。
/// 推測（報告裡列出）：`N;k` 的 k 意義（只取 N）；CheckButton 只有 PushSurf → 選中時疊在外型圖上；按鈕 Highlight＝滑過、
/// Push＝按住；訊息文字畫在欄位內 (2,2)；Moveable 以視窗上緣 18 px 當拖曳區；Esc 觸發 KeyboardEvent=ClosePersonalShop。
/// 待接：開窗的觸發點、物品放入格子的拖放流程與單價／數量輸入、文字（IME）輸入事件、OK 之後真的送 C38。
/// 本面板只改本機 <see cref="PersonalShopSetup"/>；OK 驗證成功時把 C38 封包交給 <see cref="Confirmed"/>，自己不送封包。
/// </summary>
public sealed class PersonalShopPanel:IUiMovablePanel {
    public const string WindowId="PersonalShop_Window",LayoutName="PersonalShopUI";
    /// <summary>給 UiPanelRegistry 用的一行註冊（本任務的可改檔案不含註冊表，由整合任務加上）。</summary>
    public static UiPanelRegistration Registration=>new(WindowId,LayoutName,_=>new PersonalShopPanel());
    public const int DragStripHeight=18;

    public enum Part { None, Close, SellSlot, BuySlot, Message, Poly, FirstPage, PrevPage, NextPage, LastPage, Ok, Cancel }
    public readonly record struct Hit(Part Part,int Index);
    sealed record Button(Part Part,Rectangle Rect,UiSurface? Base,UiSurface? Highlight,UiSurface? Push);

    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>new(origin,size);
    public bool Visible{get;private set;}
    public PersonalShopSetup Setup{get;private set;}=new();
    /// <summary>物品圖示來源（背包圖示）；null 或回傳 null 時格子不畫圖示，不畫替代色塊。</summary>
    public Func<ushort,Bitmap?>? IconSource{get;set;}
    /// <summary>OK 驗證通過：C38 type 0 封包（尚未送出）。整合端決定是否送。</summary>
    public event Action<byte[]>? Confirmed;
    /// <summary>關閉鈕、取消或 Esc。</summary>
    public event Action? Closed;
    public int? FocusedMessage{get;private set;}
    public Hit Hover{get;private set;}
    public Hit Pressed{get;private set;}
    public int MessageLimit{get;private set;}

    Point origin;Size size;
    UiSurface? baseSurface,frameSurface,polySurface,checkSurface,pageSurface;
    readonly List<Button> buttons=[];
    Rectangle[] sellSlots=[],buySlots=[],messageRects=[],polyRects=[];
    Rectangle pageLabel;

    // ---- Load ------------------------------------------------------------------------------------------------------
    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>同 Load，但 surf 由呼叫端提供（離線測試用合成圖）。缺節點或屬性 → UiLayoutException，不補預設值。</summary>
    public void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var root=layout.Root;
        if(root.Name!=LayoutName)throw new UiLayoutException(layout.FileName,root.Path,$"根元素應為 {LayoutName}");
        var w=Named(root,"Window",WindowId);
        origin=new(w.Int("X"),w.Int("Y"));
        baseSurface=Surf(w,"BaseSurf",surf);frameSurface=Surf(w,"FrameSurf",surf);
        buttons.Clear();
        AddButton(Part.Close,Named(w,"Button","Close_Button"),Point.Empty,surf);
        sellSlots=Slots(Named(w,"List","SellingItem_List"));
        buySlots=Slots(Named(w,"List","BuyingItem_List"));
        if(sellSlots.Length!=PersonalShopSetup.SlotsPerPage||buySlots.Length!=PersonalShopSetup.SlotsPerPage)
            throw new UiLayoutException(layout.FileName,w.Path,$"出售／收購清單應各 {PersonalShopSetup.SlotsPerPage} 格");
        var edits=new[]{Named(w,"Edit","Message1_Edit"),Named(w,"Edit","Message2_Edit")};
        messageRects=edits.Select(Rect).ToArray();
        MessageLimit=edits[0].Int("LimitText");
        if(edits.Any(e=>e.Int("LimitText")!=PersonalShopSetup.MessageLimit))
            throw new UiLayoutException(layout.FileName,edits[0].Path,$"LimitText 應為 {PersonalShopSetup.MessageLimit}");
        var poly=Named(w,"List","Poly_List");
        var images=poly.ChildrenNamed("Image").ToArray();
        if(images.Length!=PersonalShopSetup.PolyCount)throw new UiLayoutException(layout.FileName,poly.Path,$"外型應為 {PersonalShopSetup.PolyCount} 張");
        // Horizontal="1" 無 Gap：每張寬 = List 寬 / 張數（144/3=48，與 surf 5752 寬一致）。
        int px=poly.Int("X"),py=poly.Int("Y"),pw=poly.Int("Width")/images.Length,ph=poly.Int("Height");
        polyRects=Enumerable.Range(0,images.Length).Select(i=>new Rectangle(px+i*pw,py,pw,ph)).ToArray();
        polySurface=Surf(images[0],"Surf",surf);
        checkSurface=Surf(images[0].Child("CheckButton"),"PushSurf",surf);
        var page=Named(w,"Control","Page_Control");var po=new Point(page.Int("X"),page.Int("Y"));
        AddButton(Part.FirstPage,Named(page,"Button","Prev1page_Button"),po,surf);
        AddButton(Part.PrevPage,Named(page,"Button","Prevpage_Button"),po,surf);
        AddButton(Part.NextPage,Named(page,"Button","Nextpage_Button"),po,surf);
        AddButton(Part.LastPage,Named(page,"Button","Next1page_Button"),po,surf);
        var pageImage=Named(page,"Image","Page_Image");pageSurface=Surf(pageImage,"Surf",surf);
        var label=Named(pageImage,"Label","Page_Label");
        pageLabel=new(po.X+pageImage.Int("X"),po.Y+pageImage.Int("Y"),label.Int("Width"),label.Int("Height"));
        AddButton(Part.Ok,Named(w,"Button","OK_Button"),Point.Empty,surf);
        AddButton(Part.Cancel,Named(w,"Button","Cancel_Button"),Point.Empty,surf);
        // 視窗大小＝底圖與所有子元件的聯集（OK／取消在 y=334，底圖高 332，超出部分照 XML 保留，原因待驗證）。
        var all=new List<Rectangle>{new(0,0,baseSurface?.Width??0,baseSurface?.Height??0)};
        // 按鈕的按下圖可能比底圖大（OK／取消 push 54×18，底圖 53×17）：視窗範圍包含最大的狀態圖，免得被裁掉。
        all.AddRange(buttons.Select(b=>new Rectangle(b.Rect.Location,new(new[]{b.Base,b.Highlight,b.Push}.Max(s=>s?.Width??0),new[]{b.Base,b.Highlight,b.Push}.Max(s=>s?.Height??0)))));
        all.AddRange(sellSlots);all.AddRange(buySlots);all.AddRange(messageRects);all.AddRange(polyRects);all.Add(pageLabel);
        var u=all.Aggregate(Rectangle.Union);
        size=new(u.Right,u.Bottom);
    }
    static UiNode Named(UiNode parent,string element,string name){
        var m=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToArray();
        if(m.Length!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path+$"/{element}[@Name={name}]",m.Length==0?"缺少節點":"節點不唯一");
        return m[0];
    }
    static Rectangle Rect(UiNode n)=>new(n.Int("X"),n.Int("Y"),n.Int("Width"),n.Int("Height"));
    /// <summary>"5734;1" → 5734。分號後的數字意義未查證（推測為張數／狀態數），只取第一個數字。</summary>
    public static int? SurfId(string? raw){
        if(raw==null)return null;string head=raw.Split(';')[0].Trim();
        return int.TryParse(head,NumberStyles.None,CultureInfo.InvariantCulture,out int v)?v:throw new FormatException($"surf \"{raw}\" 格式不合法");
    }
    static UiSurface? Surf(UiNode n,string attr,Func<int,UiSurface?> surf){
        int? id;try{id=SurfId(n.Attr(attr));}catch(FormatException ex){throw new UiLayoutException(n.Layout.FileName,n.Path,ex.Message,ex);}
        return id is int v?surf(v):null;
    }
    void AddButton(Part part,UiNode n,Point offset,Func<int,UiSurface?> surf){
        var b=Surf(n,"BaseSurf",surf);
        // 按鈕大小取底圖；底圖缺（null）時沒有可點範圍，也不畫。
        buttons.Add(new(part,new(offset.X+n.Int("X"),offset.Y+n.Int("Y"),b?.Width??0,b?.Height??0),b,Surf(n,"HighlightSurf",surf),Surf(n,"PushSurf",surf)));
    }
    /// <summary>List(Vertical, Gap) → 每列 List(Horizontal, Gap) → ItemIcon(Width, Height)：逐列、逐格排出絕對矩形（視窗內座標）。</summary>
    static Rectangle[] Slots(UiNode list){
        int x0=list.Int("X"),y=list.Int("Y"),vgap=list.Int("Gap");var r=new List<Rectangle>();
        foreach(var row in list.ChildrenNamed("List")){
            int x=x0,hgap=row.Int("Gap"),rowHeight=row.Int("Height");
            foreach(var icon in row.ChildrenNamed("ItemIcon")){int w=icon.Int("Width"),h=icon.Int("Height");r.Add(new(x,y,w,h));x+=w+hgap;}
            y+=rowHeight+vgap;
        }
        return r.ToArray();
    }

    // ---- 狀態 ------------------------------------------------------------------------------------------------------
    /// <summary>開窗（觸發點待接）。傳入既有設定或新的空設定。</summary>
    public void Open(PersonalShopSetup? setup=null){Setup=setup??new();Visible=true;FocusedMessage=null;Pressed=default;Hover=default;}
    public void Close(){if(!Visible)return;Visible=false;FocusedMessage=null;Pressed=default;Closed?.Invoke();}
    public void Update(IUiGameState state){if(!state.InWorld&&Visible)Close();}

    /// <summary>邏輯 800×600 座標 → 元件（視窗內由上而下：按鈕、格子、訊息、外型）。</summary>
    public Hit HitTest(Point p){
        if(!Visible||!Bounds.Contains(p))return default;
        var l=new Point(p.X-origin.X,p.Y-origin.Y);
        foreach(var b in buttons)if(b.Rect.Contains(l))return new(b.Part,0);
        for(int i=0;i<sellSlots.Length;i++)if(sellSlots[i].Contains(l))return new(Part.SellSlot,i);
        for(int i=0;i<buySlots.Length;i++)if(buySlots[i].Contains(l))return new(Part.BuySlot,i);
        for(int i=0;i<messageRects.Length;i++)if(messageRects[i].Contains(l))return new(Part.Message,i);
        for(int i=0;i<polyRects.Length;i++)if(polyRects[i].Contains(l))return new(Part.Poly,i);
        return new(Part.None,0);
    }
    public Rectangle? PartRect(Part part,int index=0){
        Rectangle? r=part switch{
            Part.SellSlot=>index<sellSlots.Length?sellSlots[index]:null,
            Part.BuySlot=>index<buySlots.Length?buySlots[index]:null,
            Part.Message=>index<messageRects.Length?messageRects[index]:null,
            Part.Poly=>index<polyRects.Length?polyRects[index]:null,
            Part.None=>null,
            _=>buttons.FirstOrDefault(b=>b.Part==part)?.Rect,
        };
        return r is Rectangle v?new Rectangle(v.X+origin.X,v.Y+origin.Y,v.Width,v.Height):null;
    }
    public bool IsDragHandle(Point p)=>Visible&&Bounds.Contains(p)&&p.Y<origin.Y+DragStripHeight&&HitTest(p).Part==Part.None;
    public void MoveTo(Point location)=>origin=location;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        var h=HitTest(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:Hover=h;break;
            case UiMouseKind.Down when e.Button==MouseButtons.Left:Pressed=h;if(h.Part!=Part.Message)FocusedMessage=null;break;
            case UiMouseKind.Up when e.Button==MouseButtons.Left:{var p=Pressed;Pressed=default;if(p.Part!=Part.None&&p==h)Activate(h);break;}
        }
        // 視窗範圍內的點擊一律吞掉，不穿透到世界。
        return Bounds.Contains(e.Location)||e.Kind==UiMouseKind.Up;
    }
    void Activate(Hit h){
        switch(h.Part){
            case Part.Close:case Part.Cancel:Close();break;
            case Part.Ok:if(Setup.BuildOpen() is byte[] packet)Confirmed?.Invoke(packet);break;
            case Part.SellSlot:Setup.Select(true,Setup.Page*PersonalShopSetup.SlotsPerPage+h.Index);break;
            case Part.BuySlot:Setup.Select(false,Setup.Page*PersonalShopSetup.SlotsPerPage+h.Index);break;
            case Part.Message:FocusedMessage=h.Index;break;
            case Part.Poly:Setup.SelectPoly(h.Index);break;
            case Part.FirstPage:Setup.GoPage(0);break;
            case Part.PrevPage:Setup.GoPage(Setup.Page-1);break;
            case Part.NextPage:Setup.GoPage(Setup.Page+1);break;
            case Part.LastPage:Setup.GoPage(Setup.PageCount-1);break;
        }
    }
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible||!e.Down)return false;
        if(e.KeyData==Keys.Escape){Close();return true;}
        if(FocusedMessage is int line){
            // 原生 UI 契約目前只有按鍵事件、沒有字元／IME 事件；文字由 Setup.SetMessage 設定，這裡只處理刪字與離開欄位。
            if(e.KeyData==Keys.Back){var t=Setup.Messages[line];if(t.Length>0)Setup.SetMessage(line,t[..^1]);return true;}
            if(e.KeyData is Keys.Tab or Keys.Enter){FocusedMessage=(line+1)%PersonalShopSetup.MessageLines;return true;}
        }
        return false;
    }

    // ---- Paint -----------------------------------------------------------------------------------------------------
    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        int ox=origin.X,oy=origin.Y;
        canvas.DrawSurface(baseSurface,ox,oy);canvas.DrawSurface(frameSurface,ox,oy);
        int first=Setup.Page*PersonalShopSetup.SlotsPerPage;
        void Icons(IReadOnlyList<PersonalShopEntry?> list,Rectangle[] slots){
            for(int i=0;i<slots.Length;i++){
                if(first+i>=list.Count||list[first+i] is not PersonalShopEntry e||IconSource?.Invoke(e.IconId) is not Bitmap icon)continue;
                canvas.DrawBitmap(icon,ox+slots[i].X,oy+slots[i].Y);
            }
        }
        Icons(Setup.Sell,sellSlots);Icons(Setup.Buy,buySlots);
        for(int i=0;i<messageRects.Length;i++)canvas.DrawText(Setup.Messages[i],ox+messageRects[i].X+2,oy+messageRects[i].Y+2,Color.White);
        for(int i=0;i<polyRects.Length;i++){
            canvas.DrawSurface(polySurface,ox+polyRects[i].X,oy+polyRects[i].Y);
            if(Setup.Poly==i)canvas.DrawSurface(checkSurface,ox+polyRects[i].X,oy+polyRects[i].Y);
        }
        canvas.DrawSurface(pageSurface,ox+pageLabel.X,oy+pageLabel.Y);
        string label=Setup.PageLabel;var ls=UiText.Measure(canvas.Graphics,label);
        canvas.DrawText(label,ox+pageLabel.X+(pageLabel.Width-ls.Width)/2,oy+pageLabel.Y+(pageLabel.Height-ls.Height)/2,Color.White);
        foreach(var b in buttons){
            var h=new Hit(b.Part,0);
            var s=Pressed==h&&b.Push!=null?b.Push:Hover==h&&b.Highlight!=null?b.Highlight:b.Base;
            canvas.DrawSurface(s,ox+b.Rect.X,oy+b.Rect.Y);
        }
    }
}
