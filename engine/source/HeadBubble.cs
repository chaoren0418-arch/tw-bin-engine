namespace P06;

// R11-E（T419／#961）頭上對話框：照 TW16 `sub_7CE710`（建立）、`sub_7D01E0`（斷行）、`sub_7CE370`（夾邊界）、
// `sub_7CF290`（繪製）做，數值全部來自 docs/reference/followups-16.md（FU16）§5 的 B1–B19。
// 本輪只接「個人商店訊息」（`sub_7CFFC0` 每 5000 ms 重建一次、5 秒、一律有外框）；一般聊天（8 秒）的入口保留在
// HeadBubbleLayout.Create，沒有接到聊天流程（任務列為可選做）。
// 不用任何 surf／img：外框、圓角、尾巴是 BIN `.data` 0x1368CEC 的 52 bytes 1 bpp 點陣（FU16§3.5），寫成程式常數。
// 座標：TW16 畫面座標（與名稱、頭上條同一個 P／R）。引擎把「場景座標 − 視窗左上角的場景點」當成這個畫面座標，
// 夾邊界的 48／880／540 直接套在上面；這個對應**待驗證**（FU16§6 第 4 點）。

/// <summary>對話框的版面結果（相對 TW16 畫面座標）。</summary>
public sealed record HeadBubbleGeometry(Rectangle Box,int Tail,bool TailUp);
/// <summary>一個畫的動作：填色矩形（1 px 線段、點陣的單點都用這個）或一行文字。</summary>
public sealed record HeadBubbleFill(Rectangle Rect,ushort Color);
public sealed record HeadBubbleText(string Text,Point Position,ushort Color);
public sealed record HeadBubbleDraw(HeadBubbleFill[] Fills,HeadBubbleText[] Texts);

/// <summary>
/// 一個頭上對話框（TW16 0x3A4 bytes 的物件）。Anchor＝+892／+896（建立時算一次，不跟著說話的人移動，B17）；
/// Starts＝+770 的每行開頭（最後一個元素是長度）；Width＝最長一行的 byte 數 w；Frame＝+872；Shop＝+926（a5）。
/// </summary>
public sealed record HeadBubble(uint ObjectId,byte[] Text,int[] Starts,int Width,ushort Color,bool Frame,bool Shop,Point Anchor,int ObjectBottom,long CreatedAt,long ExpiresAt){
    public int Lines=>Math.Max(0,Starts.Length-1);
}

public static class HeadBubbleLayout {
    public const int GlyphWidth=6,LineHeight=12,MaxBytes=768;
    /// <summary>B3：泡泡話框（byte_13AAF28）開＝16、關＝20（目標寬度 W0，byte）。</summary>
    public const int TargetWidthBubbleOn=16,TargetWidthBubbleOff=20;
    /// <summary>B15：一般聊天 8 秒、商店訊息 5 秒（sub_A231C0(1000·a4, sub_7CFE50)）。</summary>
    public const int ChatSeconds=8,ShopSeconds=5;
    /// <summary>B8：A.x＝P.x＋24；A.y＝R.Top − k − m（k＝17／32／12，m 先當 0）。</summary>
    public const int AnchorDx=24,SelfHpMpLift=17,AllNamesLift=32,DefaultLift=12;
    /// <summary>B10：sub_7CE370 的邊界（反組譯 0x7CE3BF–0x7CE522）；左邊超出時平移到 2（照原碼）。</summary>
    public const int ClampLeft=48,ClampRight=880,ClampTop=48,ClampBottom=540,ClampShiftLeftTo=2;
    /// <summary>B14／B16 用到的 palette（RGB555）：[0] 黑、[2] 綠、[14] 白、[23] 灰、[33] 喊話。</summary>
    public const ushort Black=0x0000,Keyword=0x03E0,White=0x7FFF,Gray=0x4A52,Shout=0x7F99;
    /// <summary>B2：顏色是白色時換成兩個空白的 10 個 Big5 字（`Buf2`＝0x1368D20 → .rdata 0x1172F80）。</summary>
    public static readonly ushort[] BlankedBig5=[0xA2C6,0xA1E1,0xA1E0,0xA2C8,0xA2CB,0xA2CC,0xA2C9,0xA2CA,0xA2C3,0xA2C7];

    /// <summary>B4：一行上限 M＝W0＋W0/2；商店訊息 W0＋W0/4（整數除法）。</summary>
    public static int LineLimit(int targetWidth,bool shop)=>targetWidth+(shop?targetWidth/4:targetWidth/2);
    public static int TargetWidth(bool bubbleOn)=>bubbleOn?TargetWidthBubbleOn:TargetWidthBubbleOff;
    /// <summary>B7：有外框＝泡泡話框 ∨ 商店訊息（a4＝5）∨ 一般聊天以單一 `*` 開頭（a6）。</summary>
    public static bool HasFrame(bool bubbleOn,bool shop,bool forceFrame)=>bubbleOn||shop||forceFrame;
    static bool High(byte b)=>(b&0x80)!=0;
    static bool Space(byte b)=>b is (byte)' ' or (byte)'\t';

    /// <summary>
    /// B2 前處理：sub_7CE6B0（結尾是空白時，往前第一個非空白字是 `:` 就從那裡截斷）→ sub_7CF080（最多 768 bytes；
    /// 去掉 `\f?`、`\b?`（第三個 byte ≥ '0'）、`\i0`–`\i5`）→ sub_7CFE90（白色文字時 10 個 Big5 字換成兩個空白）。
    /// 掃描時跳過雙位元組字的第二個 byte（0x5C 可能是 Big5 的第二個 byte；原版是否這樣跳過**推測**）。
    /// </summary>
    public static byte[] Prepare(ReadOnlySpan<byte> raw,ushort color){
        int n=0;while(n<raw.Length&&raw[n]!=0)n++;
        var s=raw[..n];
        if(s.Length>0&&s[^1]==(byte)' '){int k=s.Length-1;while(k>=0&&s[k]==(byte)' ')k--;if(k>=0&&s[k]==(byte)':')s=s[..k];}
        if(s.Length>MaxBytes)s=s[..MaxBytes];
        var o=new List<byte>(s.Length);
        for(int i=0;i<s.Length;i++){
            byte c=s[i];
            if(High(c)&&i+1<s.Length){
                ushort code=(ushort)(c<<8|s[i+1]);
                if(color==White&&Array.IndexOf(BlankedBig5,code)>=0){o.Add((byte)' ');o.Add((byte)' ');}else{o.Add(c);o.Add(s[i+1]);}
                i++;continue;
            }
            if(c==(byte)'\\'&&i+2<s.Length){
                byte k=s[i+1],v=s[i+2];
                if(k==(byte)'f'||k==(byte)'b'&&v>=(byte)'0'||k==(byte)'i'&&v is >=(byte)'0' and <=(byte)'5'){i+=2;continue;}
            }
            o.Add(c);
        }
        return o.ToArray();
    }

    /// <summary>FU16§1.2 斷點代價表：在 j 後面斷行的代價（越低越優先）。</summary>
    public static int[] Costs(ReadOnlySpan<byte> s){
        var cost=new int[s.Length];bool trail=false;
        for(int j=0;j<s.Length;j++){
            byte c=s[j],nx=j+1<s.Length?s[j+1]:(byte)0;
            if(trail){trail=false;cost[j]=High(nx)?2:nx==(byte)'\n'?-99:3;continue;}
            if(High(c)){trail=true;cost[j]=999;continue;}
            if(c==(byte)'\n'&&nx==0){cost[j]=-999;continue;}
            if(Space(c)){cost[j]=Space(nx)?1:nx==(byte)'\n'?-99:0;continue;}
            cost[j]=nx==(byte)'\n'?-99:Space(nx)?3:nx!=0?4:0;
        }
        return cost;
    }

    /// <summary>
    /// FU16§1.3（sub_7D01E0 第二段）：回傳每行開頭（最後一個元素是長度），width＝最長一行的 byte 數。
    /// 同代價取最後一個；第二次遇到 −99 就停、用第一個；剩下的字在 M 之內放得下就整段當最後一行。
    /// </summary>
    public static int[] Wrap(ReadOnlySpan<byte> s,int limit,out int width){
        width=0;if(s.Length==0)return [0];
        var cost=Costs(s);var starts=new List<int>{0};int start=0;
        while(true){
            int end=Math.Min(start+limit,s.Length),best=99,bp=-1,minus99=0;bool reached=true;
            for(int j=start;j<end;j++){
                int c=cost[j];
                if(c==-99&&++minus99==2){reached=false;break;}
                if(c<best){best=c;bp=j+1;}
                else if(c==best&&c!=-99&&j+1>bp)bp=j+1;
            }
            if(reached&&end==s.Length)break;
            if(bp<=start)bp=end;   // 全部都不能斷（例如上限 1 byte）：硬切，避免無限迴圈（原版沒有這種輸入）
            start=bp;starts.Add(start);
        }
        starts.Add(s.Length);
        for(int k=0;k+1<starts.Count;k++)width=Math.Max(width,starts[k+1]-starts[k]);
        return starts.ToArray();
    }

    /// <summary>B8 的 k：自己而且 showCharacterHPMP 打開＝17；全部顯示名稱打開而且 +252 不是空的＝32；其他＝12。</summary>
    public static int Lift(bool self,bool hpMpShown,bool allNames,bool has252)=>self&&hpMpShown?SelfHpMpLift:allNames&&has252?AllNamesLift:DefaultLift;
    /// <summary>B8：錨點。footX＝P.x＋24＝腳下格的螢幕 X（引擎的 P＝Coordinates.Screen −(24,12)）。</summary>
    public static Point Anchor(int footX,Rectangle objectBounds,int lift,int attachmentHeight=0)=>new(footX,objectBounds.Top-lift-attachmentHeight);
    /// <summary>B9：框的矩形（bottom＝A.y）與尾巴位移 t＝A.x − L。</summary>
    public static (Rectangle Box,int Tail) Box(Point anchor,int width,int lines,bool frame){
        int half=3*width+(frame?5:0),top=anchor.Y-12*lines-(frame?14:6);
        return(Rectangle.FromLTRB(anchor.X-half,top,anchor.X+half,anchor.Y),half);
    }
    /// <summary>B10（sub_7CE370）：夾邊界；null＝不畫（原版直接刪掉這個框）。objectBottom＝物件外框下緣。</summary>
    public static HeadBubbleGeometry? Clamp(Rectangle box,int tail,int objectBottom){
        if(box.Left<ClampLeft){int d=ClampShiftLeftTo-box.Left;box.Offset(d,0);tail-=d;if(tail<8)return null;}
        else if(box.Right>ClampRight){int d=ClampRight-box.Right;box.Offset(d,0);tail-=d;if(tail>box.Width-9)return null;}
        bool up=false;
        if(box.Top<ClampTop){box.Offset(0,objectBottom-box.Top);up=true;}
        return box.Top>=ClampTop&&box.Bottom<=ClampBottom?new(box,tail,up):null;
    }
    /// <summary>平移到 TW16 畫面座標（origin＝視窗左上角的場景點）後做 B9＋B10。</summary>
    public static HeadBubbleGeometry? Place(HeadBubble b,Point origin){
        var a=new Point(b.Anchor.X-origin.X,b.Anchor.Y-origin.Y);var (box,tail)=Box(a,b.Width,b.Lines,b.Frame);
        return Clamp(box,tail,b.ObjectBottom-origin.Y);
    }

    /// <summary>
    /// sub_7CE710 的 B2–B9：前處理、斷行、決定外框。文字空白（前處理後）回 null（原版呼叫者都給非空字串）。
    /// 關鍵字過濾 sub_4FED10 引擎沒有，當作「沒設過濾」（回 2），所以秒數與框色都不變。
    /// </summary>
    public static HeadBubble? Create(uint objectId,ReadOnlySpan<byte> raw,ushort color,int seconds,bool shop,bool forceFrame,bool bubbleOn,Point anchor,int objectBottom,long now){
        var text=Prepare(raw,color);if(text.Length==0)return null;
        var starts=Wrap(text,LineLimit(TargetWidth(bubbleOn),shop),out int width);
        return new(objectId,text,starts,width,color,HasFrame(bubbleOn,shop,forceFrame),shop,anchor,objectBottom,now,now+1000L*seconds);
    }

    // FU16§3.5：.data 0x1368CEC–0x1368D1F（FO 0xF674EC），SHA-256 9d9ffa30…f028。每列 ceil(寬/8) bytes，最高位元在左。
    public static readonly byte[] BitmapData=[
        0xc6,0x28,0x10,0x10, 0x10,0x10,0x28,0xc6, 0x30,0x40,0x80,0x80, 0xc0,0x20,0x10,0x10, 0x80,0x80,0x40,0x30, 0x10,0x10,0x20,0xc0,
        0xfe,0x7c,0x38,0x10, 0x10,0x38,0x7c,0xfe, 0xff,0x80,0x7f,0x00,0x3e,0x00,0x1c,0x00,0x08,0x00, 0x08,0x00,0x1c,0x00,0x3e,0x00,0x7f,0x00,0xff,0x80];
    public readonly record struct Glyph(int Offset,int Width,int Height);
    public static readonly Glyph TailFrame=new(0,7,4),TailFrameUp=new(4,7,4),CornerTL=new(8,4,4),CornerTR=new(12,4,4),CornerBL=new(16,4,4),CornerBR=new(20,4,4),
        TailFill=new(24,7,4),TailFillUp=new(28,7,4),TailOutline=new(32,9,5),TailOutlineUp=new(42,9,5);
    /// <summary>sub_97B150：位元 1 的點用該色畫，0 不畫。</summary>
    public static IEnumerable<Point> Pixels(Glyph g,int x,int y){
        int stride=(g.Width+7)/8;
        for(int r=0;r<g.Height;r++)for(int c=0;c<g.Width;c++)if((BitmapData[g.Offset+r*stride+c/8]&0x80>>(c&7))!=0)yield return new(x+c,y+r);
    }

    /// <summary>sub_7CF290：B11–B13 的畫法（不填底色；框色＝文字色；文字每行 +12）。</summary>
    public static HeadBubbleDraw Draw(HeadBubble b,HeadBubbleGeometry g,ushort color){
        var fills=new List<HeadBubbleFill>();var texts=new List<HeadBubbleText>();
        int L=g.Box.Left,T=g.Box.Top,R=g.Box.Right,Bm=g.Box.Bottom,t=g.Tail;
        void Dot(Glyph gl,int x,int y,ushort c){foreach(var p in Pixels(gl,x,y))fills.Add(new(new(p.X,p.Y,1,1),c));}
        void Line(int x,int y,int w,int h,ushort c){if(w>0&&h>0)fills.Add(new(new(x,y,w,h),c));}
        Point origin;
        if(b.Frame){
            ushort edge=color;   // 符合關鍵字時改綠（palette[2]）；引擎沒有關鍵字過濾
            int top=g.TailUp?T+4:T,bot=g.TailUp?Bm-1:Bm-4;
            Dot(g.TailUp?TailFrameUp:TailFrame,L+t-3,g.TailUp?T:Bm-4,color);
            Line(L,top+5,1,bot-top-9,edge);Line(R-1,top+5,1,bot-top-9,edge);
            int gapY=g.TailUp?top:bot,fullY=g.TailUp?bot:top;
            Line(L+5,gapY,t-8,1,edge);Line(L+t+4,gapY,R-L-t-9,1,edge);
            Line(L+5,fullY,R-L-10,1,edge);
            Dot(CornerTL,L+1,top+1,edge);Dot(CornerTR,R-5,top+1,edge);Dot(CornerBL,L+1,bot-4,edge);Dot(CornerBR,R-5,bot-4,edge);
            origin=new(L+5,top+5);
        }else{
            Dot(g.TailUp?TailOutlineUp:TailOutline,L+t-3,g.TailUp?T:Bm-5,Black);
            Dot(g.TailUp?TailFillUp:TailFill,L+t-3,g.TailUp?T+1:Bm-4,color);
            origin=g.TailUp?new(L,T+6):new(L,T);
        }
        for(int k=0;k<b.Lines;k++){
            var line=b.Text.AsSpan(b.Starts[k],b.Starts[k+1]-b.Starts[k]);
            texts.Add(new(PersonalShopTitle.Text(line.ToArray()),new(origin.X,origin.Y+LineHeight*k),color));
        }
        return new(fills.ToArray(),texts.ToArray());
    }
}

/// <summary>
/// 目前畫面上的對話框（TW16 的串列）：B1 每個物件最多一個、B15 到時間移除（沒有淡出）、B16 新框通過夾邊界後，
/// 和它相交的其他框改灰（已經是灰的不再處理；新框本身維持原色）。依串列順序畫（舊的先畫）。
/// </summary>
public sealed class HeadBubbleBoard {
    readonly List<HeadBubble> list=[];
    readonly Dictionary<HeadBubble,ushort> shown=new(ReferenceEqualityComparer.Instance);
    public IReadOnlyList<HeadBubble> Bubbles=>list;
    public ushort ColorOf(HeadBubble b)=>shown.TryGetValue(b,out var c)?c:b.Color;
    /// <summary>B1：先移除這個物件原有的框（即使新的後面不畫）。</summary>
    public void RemoveObject(uint id){foreach(var b in list.Where(b=>b.ObjectId==id).ToArray())Remove(b);}
    /// <summary>sub_7CFCB0：移除這個物件的商店框。</summary>
    public void RemoveShop(uint id){foreach(var b in list.Where(b=>b.ObjectId==id&&b.Shop).ToArray())Remove(b);}
    void Remove(HeadBubble b){list.Remove(b);shown.Remove(b);}
    public void Clear(){list.Clear();shown.Clear();}
    /// <summary>加入；place＝目前的夾邊界結果，null＝不畫（不加入）。回傳是否加入。</summary>
    public bool Add(HeadBubble b,Func<HeadBubble,HeadBubbleGeometry?> place){
        RemoveObject(b.ObjectId);
        if(place(b) is not {} g)return false;
        foreach(var other in list){
            if(ColorOf(other)==HeadBubbleLayout.Gray)continue;
            if(place(other) is {} og&&og.Box.IntersectsWith(g.Box))shown[other]=HeadBubbleLayout.Gray;
        }
        list.Add(b);return true;
    }
    /// <summary>sub_7CFE50：到時間就移除。</summary>
    public void Expire(long now){foreach(var b in list.Where(b=>now>=b.ExpiresAt).ToArray())Remove(b);}
}

/// <summary>
/// 頭上對話框的兩個選項（option12，FU16§0.3）：
/// 泡泡話框 #1003（byte_13AAF28，bss 初值 0、原版不存檔）；個人商店名稱 #1027（byte_136E99F，初值 1、存設定檔）。
/// 個人商店名稱存在引擎的 option-ui.txt（OptionSettingsFile，鍵 show-shop-message）。
/// </summary>
public static class HeadBubbleOptions {
    public const string ShopMessageKey="show-shop-message";
    public static bool BubbleFrames {get;set;}
    static bool? shopMessages;
    public static bool ShopMessages{
        get=>shopMessages??=P06.NativeUi.OptionSettingsFile.Read(ShopMessageKey)??true;
        set{shopMessages=value;P06.NativeUi.OptionSettingsFile.Write(ShopMessageKey,value);}
    }
    /// <summary>離線測試用：只改記憶體裡的值、不寫檔；null＝下次重讀設定檔。</summary>
    internal static void SetCached(bool? value)=>shopMessages=value;
}

/// <summary>畫面上一個可能說話的物件：id、物件外框 R（sub_A3ED50）、腳下格螢幕 X（P.x＋24）。</summary>
public sealed record HeadBubbleTarget(uint ObjectId,Rectangle Bounds,int FootX,bool Self);

public static class HeadBubbleShops {
    /// <summary>
    /// sub_7CFFC0：每 5000 ms 以「此刻輪到的那一行」重建一次商店框（a4＝5、a5＝1、白色），所以這一輪的建立時間是
    /// ShopAt＋5000·輪數。選項關閉、收攤、看不到時移除（sub_7CFCB0）。已經是這一輪的框就不重建。
    /// </summary>
    public static void Sync(HeadBubbleBoard board,WorldSession w,IReadOnlyList<HeadBubbleTarget> targets,bool showAll,bool hpMpShown,long now,Point origin){
        board.Expire(now);
        bool on=HeadBubbleOptions.ShopMessages&&w.BlindMode!=1;   // 盲眼 1 不畫他人（推測，同名稱）
        var seen=new HashSet<uint>();
        foreach(var t in targets){
            if(t.Self||w.Objects.FirstOrDefault(x=>x.ObjectId==t.ObjectId) is not {ShopActive:true} o)continue;
            string line=on?w.ShopTitleLine(o,now):"";
            if(line.Length==0)continue;
            seen.Add(o.ObjectId);
            long turn=Math.Max(0,now-o.ShopAt)/PersonalShopTitle.RotateMs,created=o.ShopAt+turn*PersonalShopTitle.RotateMs;
            if(board.Bubbles.Any(b=>b.ObjectId==o.ObjectId&&b.Shop&&b.CreatedAt==created))continue;
            int lift=HeadBubbleLayout.Lift(t.Self,hpMpShown,showAll,w.Field252Of(o.ObjectId).Length>0);
            var anchor=HeadBubbleLayout.Anchor(t.FootX,t.Bounds,lift);
            var b=HeadBubbleLayout.Create(o.ObjectId,PersonalShopTitle.Bytes(line),HeadBubbleLayout.White,HeadBubbleLayout.ShopSeconds,true,false,HeadBubbleOptions.BubbleFrames,anchor,t.Bounds.Bottom,created);
            if(b==null){board.RemoveObject(o.ObjectId);continue;}
            board.Add(b,x=>HeadBubbleLayout.Place(x,origin));
        }
        foreach(var b in board.Bubbles.Where(b=>b.Shop&&!seen.Contains(b.ObjectId)).ToArray())board.RemoveShop(b.ObjectId);
    }
}

public sealed partial class WorldSession {
    // TW16 物件 +252（sub_A41510 回傳 this+252）。#916 已查出商店訊息存在 +98／+139、不是 +252；3.81 目前沒有
    // 已知封包欄位會填 +252（語義與來源封包仍待查證，AN 待辦），所以正式流程一律是空字串，全名模式的 +252 行不會出現。
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,string> field252=new();
    internal string Field252Of(uint id)=>field252.TryGetValue(id,out var s)?s:"";
    /// <summary>給離線測試（與日後查到來源封包時）寫入 +252；空字串＝清掉。</summary>
    internal void SetField252(uint id,string text){if(text.Length==0)field252.TryRemove(id,out _);else field252[id]=text;}
    [WorldReset]
    void ResetField252()=>field252.Clear();
}

public sealed partial class ClientForm {
    readonly HeadBubbleBoard headBubbles=new();
    /// <summary>R11-E：同步商店框，再照串列順序畫（sub_7CF290）。文字走名稱同一套 GDI（MingLiU 12、1 px 黑框）。</summary>
    void PaintHeadBubbles(Graphics g,bool showAll){
        if(!login.World.Renderable){headBubbles.Clear();return;}
        var o=Camera.ToScene(Camera.ViewOffset);var origin=new Point((int)Math.Floor(o.X),(int)Math.Floor(o.Y));
        var targets=new List<HeadBubbleTarget>();
        foreach(var v in objectVisuals){
            if(v.Action=="ground"||objectPatch?.VisibleObjectIds.Contains(v.ObjectId)!=true)continue;
            var r=renderer.ActorBounds.TryGetValue(v.ObjectId,out var b)?b:v.Bounds;if(r.IsEmpty)continue;
            targets.Add(new(v.ObjectId,r,(int)Math.Floor(v.Foot.X),false));
        }
        long now=Environment.TickCount64;
        HeadBubbleShops.Sync(headBubbles,login.World,targets,showAll,true,now,origin);
        if(headBubbles.Bubbles.Count==0)return;
        float zoom=Camera.Zoom;var state=g.Save();
        try{
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            PointF View(int x,int y)=>Camera.ToView(new PointF(x+origin.X,y+origin.Y));
            Font? font=null;
            try{
                foreach(var bubble in headBubbles.Bubbles){
                    if(HeadBubbleLayout.Place(bubble,origin) is not {} geo)continue;
                    var plan=HeadBubbleLayout.Draw(bubble,geo,headBubbles.ColorOf(bubble));
                    foreach(var f in plan.Fills){using var brush=new SolidBrush(NameColors.ToColor(f.Color));var p=View(f.Rect.X,f.Rect.Y);g.FillRectangle(brush,p.X,p.Y,f.Rect.Width*zoom,f.Rect.Height*zoom);}
                    foreach(var t in plan.Texts){
                        var p=View(t.Position.X,t.Position.Y);var fill=NameColors.ToColor(t.Color);var edge=NameColors.ToColor(HeadBubbleLayout.Black);
                        if(NativeNameFont.Draw(g,t.Text,fill,edge,p.X,p.Y,zoom))continue;
                        font??=new Font(NativeNameFont.Face,NameLayout.FontPixels*zoom,FontStyle.Regular,GraphicsUnit.Pixel,NativeNameFont.CharSet);
                        using var brush=new SolidBrush(fill);using var outline=new SolidBrush(edge);
                        foreach(var (dx,dy) in NativeNameFont.OutlineOffsets)g.DrawString(t.Text,font,outline,p.X+dx*zoom,p.Y+dy*zoom,StringFormat.GenericTypographic);
                        g.DrawString(t.Text,font,brush,p.X,p.Y,StringFormat.GenericTypographic);
                    }
                }
            }finally{font?.Dispose();}
        }finally{g.Restore(state);}
    }
}
