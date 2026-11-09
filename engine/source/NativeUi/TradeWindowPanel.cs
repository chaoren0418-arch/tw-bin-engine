namespace P06.NativeUi;

// 原生 TRADE_WINDOW（uiextra86.xml 第 5 段；M7-S T219／#526 建立，R2-E T262／#623 依 Q-1 改版）。
// 依據：docs/reference/trade-window-and-projectiles.md（TWP）§1.2–§1.6（T266／#627，已驗證）、followups-3.md §6（T275）。
//
// 已驗證（TWP）：
//   - 模式與左鈕（sub_A59780／FO 0x658B80）：0／13／15／7＝BUY 1992、1／14＝SELL 1998、6／16＝OK 1996；右鈕固定 CANCEL 1994。
//   - 元件（§1.3）：底圖 1983 (6,9)、框 1984（無座標＝(0,0)，視窗大小）、關閉 X 1962、捲軸 1970、上下鈕 2042／2040、拉桿 2044、
//     TRADE_UP／DOWN 1985／1987、ADENA 1990、TOTAL 1991 (19,23)。標題列拖曳區 (1,1,239,20)。
//   - 按鈕兩態（LImageButton，sub_8FF930）：平常 surf n、按下畫 n+1；放開時在鈕內才觸發；沒有 hover／停用圖。
//   - 列版面（§1.4，sub_A27EF0／sub_A29AA0）：一次 7 列、列距 36、列原點 (19, 38+36k)；圖示 (0,0)、名稱 (38,3)、
//     數量右緣 x=176／y=19、「－」在數量左緣−13、「＋」在 (180,19)、熱區 12×12、單價右緣＝「－」左緣−4；
//     已選列畫 surf 24 於 (列x−2, 列y−2)；數量 >0 的列從 (24, 列y−2) 填 192×35 底色。點列用 ListBox (16,34)–(213,287) 格高 37。
//   - 頂部：TOTAL 1991 (19,23)、「合計/持有」(75,22)；合計超過持有時換警告色。
//   - 數量（§1.5）：「＋」到上限回 0、「－」到 0 跳上限；按下時「＋」畫 555、「－」畫 557；數字鍵 ×10＋數字、Backspace ÷10。
//   - 送出（§1.6）：把所有數量 >0 的列一次交給來源（商店 C161 一包多筆；修理每列一包 C20）。
// 推測（標明）：底色的顏色、警告色、ADENA 圖與單價的間距、拉桿拖曳換算、數字鍵輸入超過上限時夾到上限、
//   名稱只畫一行（第二行與截短規則未做）、「＋」「－」鍵（原版鍵碼 4203／4205）對應 Add／Subtract。

/// <summary>TRADE_WINDOW 一列：Maximum 是數量上限（TradeItem +296；買＝依單價的上限，其他＝物品數量）。</summary>
public sealed record TradeListItem(uint Id,string Name,ushort Icon,uint Price,uint Maximum,bool Enabled);
/// <summary>
/// TRADE_WINDOW 目前內容。Mode＝原版 TradeWindow 模式（0 買、1 賣、6 修理、7 學魔法）；(Mode, Revision) 變了代表是新清單。
/// Holding＝「持有」（null＝不畫 TOTAL 與合計，例如貨幣不是金幣）；ShowPrice＝TradeBox +153；CanSubmit＝false 時左鈕不送。
/// </summary>
public sealed record TradeListView(int Mode,int Revision,TradeListItem[] Items,long? Holding,bool ShowPrice,bool CanSubmit,string Notice);

/// <summary>TRADE_WINDOW 的資料來源（由 ClientForm 接上；面板不碰 LoginConnection）。R3-F 的學魔法窗也實作這個介面。</summary>
public interface ITradeWindowSource {
    /// <summary>目前要顯示的內容；null＝不開窗。</summary>
    TradeListView? Current {get;}
    /// <summary>左鈕（BUY／SELL／OK）：所有數量 &gt;0 的列。既有送出路徑會再驗證一次。</summary>
    void Submit(IReadOnlyList<ShopChoice> choices,int revision);
    /// <summary>關閉 X／CANCEL／Esc。</summary>
    void Close();
}

/// <summary>多個來源共用同一個窗（原版也只有一個 TradeWindow）：取第一個有內容的來源，送出與關閉轉給它。</summary>
public sealed class TradeWindowSources(params ITradeWindowSource[] sources):ITradeWindowSource {
    ITradeWindowSource? active;
    public IReadOnlyList<ITradeWindowSource> Sources=>sources;
    public TradeListView? Current{get{foreach(var s in sources)if(s.Current is {} v){active=s;return v;}active=null;return null;}}
    public void Submit(IReadOnlyList<ShopChoice> choices,int revision)=>active?.Submit(choices,revision);
    public void Close()=>active?.Close();
}

public sealed class TradeWindowPanel:IUiMovablePanel {
    [UiPanel(110)] internal static UiPanelRegistration Registration=>new(TradeWindowPanel.PanelId,TradeWindowPanel.LayoutName,root=>new TradeWindowPanel());   // M7-S (#526)
    public const string PanelId="TRADE_WINDOW";
    public const string LayoutName="uiextra86";
    public const int ModeBuy=0,ModeSell=1,ModeRepair=6,ModeSpell=7;
    public const int VisibleRows=7,RowPitch=36,HitPitch=37,ButtonHit=12;
    public const int NameX=38,NameY=3,DetailY=19,QuantityRight=176,PlusX=180,FillX=24;
    public const int MarkerSurf=24,PlusPressedSurf=555,MinusPressedSurf=557;
    public static readonly Point FirstRow=new(19,38),TotalText=new(75,22);
    public static readonly Size FillSize=new(192,35);
    /// <summary>TradeBox（ListBox）參數 (16, 34, 213, 287)：點選的列號＝(y−34)/37。</summary>
    public static readonly Rectangle ListBox=Rectangle.FromLTRB(16,34,213,287);
    public static readonly Rectangle TitleStrip=new(1,1,239,20);
    /// <summary>推測：數量 &gt;0 的列底色（原版顏色未查）、合計超過持有時的警告色（word_153F240[2] 的值未查）。</summary>
    public static readonly Color FillColor=Color.FromArgb(96,40,72,140),WarnColor=Color.FromArgb(255,96,96);

    const string BaseName="SURF_RENEWAL_WIN_TRADE_BASE",FrameName="SURF_RENEWAL_WIN_TRADE_FRAME",QuitName="SURF_RENEWAL_BTN_QUIT_X",
        ScrollName="SURF_RENEWAL_WIN_SCROLLBAR_LONG",UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF",
        KnobName="SURF_RENEWAL_WIN_KNOB_BUT_OFF",PlusName="SURF_RENEWAL_BTN_TRADE_UP_OFF",MinusName="SURF_RENEWAL_BTN_TRADE_DOWN_OFF",
        AdenaName="SURF_RENEWAL_IMG_TRADE_ADENA",TotalName="SURF_RENEWAL_IMG_TRADE_TOTAL",BuyName="SURF_RENEWAL_BTN_TRADE_BUY_OFF",
        CancelName="SURF_RENEWAL_BTN_TRADE_CANCEL_OFF",OkName="SURF_RENEWAL_BTN_TRADE_OK_OFF",SellName="SURF_RENEWAL_BTN_TRADE_SELL_OFF";

    /// <summary>XML 的一個元件：surf、相對窗原點的位置、圖與按下圖（null＝缺圖不畫，命中範圍為空）。</summary>
    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface,UiSurface? Pressed=null) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get;private set;}
    public bool Visible=>view!=null;
    public ITradeWindowSource? Source{get;set;}
    /// <summary>物品圖示（產品接背包圖示；null＝不畫圖示）。</summary>
    public Func<ushort,Bitmap?>? IconSource{get;set;}
    public Element? Base{get;private set;}
    public Element? Frame{get;private set;}
    public Element? Quit{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    public Element? Knob{get;private set;}
    public Element? Plus{get;private set;}
    public Element? Minus{get;private set;}
    public Element? Adena{get;private set;}
    public Element? Total{get;private set;}
    public Element? Buy{get;private set;}
    public Element? Cancel{get;private set;}
    public Element? Ok{get;private set;}
    public Element? Sell{get;private set;}
    UiSurface? marker,plusPressed,minusPressed;
    Rectangle localBounds;

    TradeListView? view;(int Mode,int Revision) shown=(-1,-1),closed=(-1,-1);
    uint[] quantities=[];
    public TradeListView? View=>view;
    public int Selected{get;private set;}=-1;
    public int Scroll{get;private set;}
    public IReadOnlyList<uint> Quantities=>quantities;

    enum Press{None,Quit,Cancel,Left,Up,Down,Knob,Plus,Minus}
    Press pressed;int pressedRow=-1,knobGrab;bool pressInside;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(PanelId);
        Origin=new(window.Int("x"),window.Int("y"));
        // positioned=false：XML 沒有座標的元件（框＝(0,0)；拉桿、＋／－的位置由程式計算）。其他元件缺座標時丟例外，不推定。
        Element Find(string name,bool positioned=true,bool button=false){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            var offset=positioned?new Point(n.Int("xPos"),n.Int("yPos")):new Point(n.TryInt("xPos")??0,n.TryInt("yPos")??0);
            return new(name,ids[0],offset,surf(ids[0]),button?surf(ids.Length>1?ids[1]:ids[0]+1):null);
        }
        Base=Find(BaseName);Frame=Find(FrameName,positioned:false);Quit=Find(QuitName,button:true);ScrollBar=Find(ScrollName);
        Up=Find(UpName,button:true);Down=Find(DownName,button:true);Knob=Find(KnobName,positioned:false,button:true);
        Plus=Find(PlusName,positioned:false);Minus=Find(MinusName,positioned:false);Adena=Find(AdenaName);Total=Find(TotalName);
        Buy=Find(BuyName,button:true);Cancel=Find(CancelName,button:true);Ok=Find(OkName,button:true);Sell=Find(SellName,button:true);
        marker=surf(MarkerSurf);plusPressed=surf(PlusPressedSurf);minusPressed=surf(MinusPressedSurf);
        // 視窗大小＝框 1984（TWP§1.3）；缺框圖時退回已畫元件的聯集。
        localBounds=Frame.Drawn?Frame.Local:new[]{Base,Quit,ScrollBar,Up,Down,Buy,Cancel}.Where(e=>e.Drawn).Select(e=>e.Local).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Place(Origin);
    }
    void Place(Point origin){Origin=origin;Bounds=localBounds.IsEmpty?Rectangle.Empty:new(origin.X+localBounds.X,origin.Y+localBounds.Y,localBounds.Width,localBounds.Height);}

    public bool IsDragHandle(Point p)=>view!=null&&new Rectangle(Origin.X+TitleStrip.X,Origin.Y+TitleStrip.Y,TitleStrip.Width,TitleStrip.Height).Contains(p)&&!At(Quit).Contains(p);
    public void MoveTo(Point location)=>Place(new(Origin.X+location.X-Bounds.X,Origin.Y+location.Y-Bounds.Y));

    /// <summary>依模式換左鈕（TWP§1.2）。</summary>
    public Element? LeftButton=>view?.Mode switch{null=>null,1 or 14=>Sell,6 or 16=>Ok,_=>Buy};
    /// <summary>買（模式 0）的單筆上限依單價（TWP§1.5）。</summary>
    public static uint BuyLimit(uint price)=>price<1000?9999u:price<10000?999u:price<3000000?20u:1u;

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v!=null&&(v.Mode,v.Revision)==closed)v=null;
        if(v!=null&&((v.Mode,v.Revision)!=shown||quantities.Length!=v.Items.Length)){shown=(v.Mode,v.Revision);Selected=-1;Scroll=0;quantities=new uint[v.Items.Length];pressed=Press.None;}
        view=v;
        if(view==null){Selected=-1;Scroll=0;pressed=Press.None;return;}
        for(int i=0;i<quantities.Length;i++){var item=view.Items[i];if(!item.Enabled)quantities[i]=0;else if(quantities[i]>item.Maximum)quantities[i]=item.Maximum;}
        if(Selected>=view.Items.Length)Selected=-1;
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
    }
    public int MaxScroll=>view==null?0:Math.Max(0,view.Items.Length-VisibleRows);
    /// <summary>合計＝Σ 數量×單價。</summary>
    public long TotalPrice=>view==null?0:view.Items.Select((item,i)=>(long)quantities[i]*item.Price).Sum();
    /// <summary>目前所有數量 &gt;0 的列（左鈕送出的內容）。</summary>
    public ShopChoice[] Choices=>view==null?[]:view.Items.Select((item,i)=>(item,q:quantities[i])).Where(x=>x.q>0&&x.item.Enabled).Select(x=>new ShopChoice(x.item.Id,x.q)).ToArray();

    Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    /// <summary>第 k 個可見列的原點（邏輯座標）。</summary>
    public Point RowOrigin(int k)=>new(Origin.X+FirstRow.X,Origin.Y+FirstRow.Y+RowPitch*k);
    /// <summary>點選位置對應的列索引（ListBox 格高 37）；不在列上為 -1。</summary>
    public int RowAt(Point p){
        if(view==null)return -1;
        int x=p.X-Origin.X,y=p.Y-Origin.Y;
        if(!ListBox.Contains(x,y))return -1;
        int k=(y-ListBox.Top)/HitPitch;if(k>=VisibleRows)return -1;
        int index=Scroll+k;return index<view.Items.Length?index:-1;
    }

    static readonly object measureLock=new();static Graphics? measure;
    /// <summary>UiText 的字寬（「－」鈕與單價的位置依數量字寬而定）。</summary>
    public static int TextWidth(string text){lock(measureLock){measure??=Graphics.FromImage(new Bitmap(1,1));return UiText.Measure(measure,text).Width;}}
    static string Quantity(uint q)=>q.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
    static string Money(long v)=>v.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
    /// <summary>第 k 個可見列的「－」與「＋」熱區（12×12）。</summary>
    public (Rectangle Minus,Rectangle Plus) RowButtons(int k){
        var o=RowOrigin(k);int index=Scroll+k;uint q=view!=null&&index<quantities.Length?quantities[index]:0;
        int left=o.X+QuantityRight-TextWidth(Quantity(q));
        return (new(left-13,o.Y+DetailY,ButtonHit,ButtonHit),new(o.X+PlusX,o.Y+DetailY,ButtonHit,ButtonHit));
    }
    /// <summary>拉桿位置：軌道上依「捲動列／(筆數−1)」（TWP§1.3）。</summary>
    public Rectangle KnobRect{get{
        var track=At(ScrollBar);if(view==null||track.IsEmpty||Knob?.Surface is not {} s)return Rectangle.Empty;
        int span=Math.Max(0,track.Height-s.Height),n=Math.Max(1,view.Items.Length-1);
        return new(track.X,track.Y+span*Math.Min(Scroll,n)/n,s.Width,s.Height);
    }}

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        void Draw(Element? e,bool down=false){if(e?.Surface==null)return;canvas.DrawSurface(down&&e.Pressed!=null?e.Pressed:e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);}
        bool Down(Press p)=>pressed==p&&pressInside;
        Draw(Base);Draw(Frame);Draw(Quit,Down(Press.Quit));Draw(ScrollBar);Draw(Up,Down(Press.Up));Draw(this.Down,Down(Press.Down));
        if(KnobRect is {IsEmpty:false} knob)canvas.DrawSurface(Down(Press.Knob)&&Knob!.Pressed!=null?Knob.Pressed:Knob!.Surface,knob.X,knob.Y);
        Draw(LeftButton,Down(Press.Left));Draw(Cancel,Down(Press.Cancel));
        if(view.Holding is long holding){
            Draw(Total);long total=TotalPrice;
            canvas.DrawText(Money(total)+"/"+Money(holding),Origin.X+TotalText.X,Origin.Y+TotalText.Y,total>holding&&view.Mode==ModeBuy?WarnColor:Color.White,Color.Black);
        }
        var g=canvas.Graphics;
        for(int k=0;k<VisibleRows&&Scroll+k<view.Items.Length;k++){
            int i=Scroll+k;var item=view.Items[i];var o=RowOrigin(k);uint q=quantities[i];
            if(q>0){using var fill=new SolidBrush(FillColor);g.FillRectangle(fill,Origin.X+FillX,o.Y-2,FillSize.Width,FillSize.Height);}
            if(i==Selected)canvas.DrawSurface(marker,o.X-2,o.Y-2);
            if(IconSource?.Invoke(item.Icon) is Bitmap icon)canvas.DrawBitmap(icon,o.X,o.Y);
            var color=item.Enabled?Color.White:Color.Gray;
            canvas.DrawText(item.Name,o.X+NameX,o.Y+NameY,color,Color.Black);
            string qt=Quantity(q);int qw=TextWidth(qt);
            canvas.DrawText(qt,o.X+QuantityRight-qw,o.Y+DetailY,color,Color.Black);
            var (minus,plus)=RowButtons(k);
            canvas.DrawSurface(pressed==Press.Minus&&pressInside&&pressedRow==i?minusPressed??Minus?.Surface:Minus?.Surface,minus.X,minus.Y);
            canvas.DrawSurface(pressed==Press.Plus&&pressInside&&pressedRow==i?plusPressed??Plus?.Surface:Plus?.Surface,plus.X,plus.Y);
            if(view.ShowPrice){
                string pt=Money(item.Price);int pw=TextWidth(pt),px=minus.X-4-pw;
                canvas.DrawText(pt,px,o.Y+DetailY,color,Color.Black);
                if(view.Holding!=null&&Adena?.Surface is {} adena)canvas.DrawSurface(adena,px-adena.Width-1,o.Y+DetailY);
            }
        }
    }

    void Close(){
        if(view==null)return;
        closed=(view.Mode,view.Revision);view=null;Selected=-1;Scroll=0;pressed=Press.None;Source?.Close();
    }
    void Submit(){
        if(view==null||!view.CanSubmit)return;
        var choices=Choices;if(choices.Length==0)return;
        Source?.Submit(choices,view.Revision);
    }
    /// <summary>「＋」到上限回 0、「－」到 0 跳上限（TWP§1.5）。</summary>
    public bool Adjust(int index,int direction){
        if(view==null||index<0||index>=view.Items.Length||!view.Items[index].Enabled)return false;
        uint max=view.Items[index].Maximum,q=quantities[index];
        quantities[index]=direction>0?(q>=max?0:q+1):(q==0?max:q-1);
        return true;
    }
    void ScrollToKnob(int y){
        var track=At(ScrollBar);if(view==null||track.IsEmpty||Knob?.Surface is not {} s)return;
        int span=Math.Max(1,track.Height-s.Height),n=Math.Max(1,view.Items.Length-1);
        Scroll=Math.Clamp((int)Math.Round((double)(y-knobGrab-track.Y)*n/span),0,MaxScroll);
    }
    Rectangle PressRect(Press p)=>p switch{Press.Quit=>At(Quit),Press.Cancel=>At(Cancel),Press.Left=>At(LeftButton),Press.Up=>At(Up),Press.Down=>At(this.Down),
        Press.Plus or Press.Minus when pressedRow-Scroll is int k&&k>=0&&k<VisibleRows=>p==Press.Plus?RowButtons(k).Plus:RowButtons(k).Minus,_=>Rectangle.Empty};

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var p=e.Location;
        if(e.Kind==UiMouseKind.Wheel){if(e.Delta!=0)Scroll=Math.Clamp(Scroll-Math.Sign(e.Delta),0,MaxScroll);return true;}
        if(e.Kind==UiMouseKind.Move){
            if(pressed==Press.Knob)ScrollToKnob(p.Y);
            else if(pressed!=Press.None)pressInside=PressRect(pressed).Contains(p);
            return pressed!=Press.None||Bounds.Contains(p);
        }
        if(e.Kind==UiMouseKind.Up){
            if(e.Button!=MouseButtons.Left||pressed==Press.None)return Bounds.Contains(p);
            var was=pressed;int row=pressedRow;bool inside=was==Press.Knob||PressRect(was).Contains(p);pressed=Press.None;pressedRow=-1;pressInside=false;
            if(!inside)return true;
            switch(was){
                case Press.Quit:case Press.Cancel:Close();break;
                case Press.Left:Submit();break;
                case Press.Up:Scroll=Math.Max(0,Scroll-1);break;
                case Press.Down:Scroll=Math.Min(MaxScroll,Scroll+1);break;
                case Press.Plus:Adjust(row,+1);break;
                case Press.Minus:Adjust(row,-1);break;
            }
            return true;
        }
        if(e.Button!=MouseButtons.Left)return Bounds.Contains(p);
        Press Start(Press what,int row=-1){pressed=what;pressedRow=row;pressInside=true;return what;}
        if(At(Quit).Contains(p)){Start(Press.Quit);return true;}
        if(At(Cancel).Contains(p)){Start(Press.Cancel);return true;}
        if(At(LeftButton).Contains(p)){Start(Press.Left);return true;}
        if(At(Up).Contains(p)){Start(Press.Up);return true;}
        if(At(this.Down).Contains(p)){Start(Press.Down);return true;}
        if(KnobRect.Contains(p)){Start(Press.Knob);knobGrab=p.Y-KnobRect.Y;return true;}
        if(At(ScrollBar).Contains(p)&&Knob?.Surface is {} ks){knobGrab=ks.Height/2;ScrollToKnob(p.Y);return true;}
        for(int k=0;k<VisibleRows&&Scroll+k<view.Items.Length;k++){
            var (minus,plus)=RowButtons(k);
            if(plus.Contains(p)){Start(Press.Plus,Scroll+k);return true;}
            if(minus.Contains(p)){Start(Press.Minus,Scroll+k);return true;}
        }
        int index=RowAt(p);
        if(index>=0)Selected=index;
        return Bounds.Contains(p);
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down)return false;
        if(e.KeyCode==Keys.Escape){Close();return true;}
        if(Selected<0||Selected>=view.Items.Length||!view.Items[Selected].Enabled)return false;
        uint max=view.Items[Selected].Maximum,q=quantities[Selected];
        int digit=e.KeyCode is >=Keys.D0 and <=Keys.D9?e.KeyCode-Keys.D0:e.KeyCode is >=Keys.NumPad0 and <=Keys.NumPad9?e.KeyCode-Keys.NumPad0:-1;
        if(digit>=0&&e.Modifiers==Keys.None){quantities[Selected]=(uint)Math.Min(max,(ulong)q*10+(uint)digit);return true;}
        if(e.KeyCode==Keys.Back){quantities[Selected]=q/10;return true;}
        if(e.KeyCode is Keys.Add or Keys.Oemplus)return Adjust(Selected,+1);
        if(e.KeyCode is Keys.Subtract or Keys.OemMinus)return Adjust(Selected,-1);
        return false;
    }
}
