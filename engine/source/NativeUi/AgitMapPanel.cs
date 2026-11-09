namespace P06.NativeUi;

// W2-C（T232／#550）：S187 HouseMap 開的舊式 AgitMapWindow（沒有 XML）。
// 依據：docs/reference/windows-2.md 第 5.2 節、docs/reference/sprites-and-strings.md 第 2 節（T267／#628）。
// 已驗證（BIN）：
//   - 繪製 sub_542640（VA 0x542640／FO 0x141A40）：地圖 surf 畫在視窗原點＝UI 原點偏移 dword_154524C／dword_1545250，
//     800×600 下為 0（ui-layout.md V5）。地圖表 dword_135DA34（VA 0x135DA34／FO 0xF5C234）＝區域 4–8：689、741、741、801、1482，
//     區域 1 讀 dword_135DA34[4]（＝1482）。區域 0、2、3 讀到 RTTI 字串（原版缺陷）→ 本面板不開窗；≥ 9 也不開。
//   - 標記：編號 1–70 時畫在「原點＋表內 (x, y)」，surf 176，區域 7 用 34；表是靜態初始化 sub_113F600（VA 0x113F600）
//     寫入的 BIN 內數值（不是遊戲資源）。區域 1 的公式落在區域 8 的表。超出有資料筆數的編號，原版表內是 (0, 0)，照畫。
//   - 閃爍 sub_543880：亮 800 ms、暗 200 ms。
//   - 之後畫 surf 160 (312, 56)、164 (312, 110)。建構子 sub_5419E0 另建按鈕 surf 678 (213, 297)＝關窗（sub_A59AB0）、
//     surf 674 (90, 299)＝sub_543810。
// 推測：閃爍相位——原版開窗時先排 1000 ms 計時，本面板以開窗那一格為 0，做「每 1000 ms 前 800 ms 亮」；
//       Bounds 取上述所有圖的聯集；按鈕在「按下與放開都在同一鈕」時才作用（與其他原生窗一致）；不可拖曳。
// T276（#667）：674 鈕＝sub_543810 → sub_542BF0，送 `cdss` 227（＝3.81 C125 C_NPCAction）D(S187 物件 id) S("select") S("%u" 區域<<16|編號)，
//   伺服器 Npc_AuctionBoard 回 S39 `agsel`（拍賣資訊頁），由既有 NPC 對話窗顯示（followups-3.md 第 4.1 節，已驗證）。
//   面板不碰 LoginConnection：按下＋放開都在 674 鈕內時呼叫宿主（IAgitMapHost，RingWindows.cs 的轉接），送出由宿主擋重複與過期。
//   推測：送出後地圖窗不關（原版 sub_543810 之後沒有看到關窗呼叫，未逐行追）。
// 待驗證（不做）：按鈕的按下圖；ESC 行為；3.81 伺服器實際送的盟屋編號（DB house 表）；agsel 頁在原版是否另開窗。

/// <summary>674 鈕的宿主：對 shown 這一次的 S187 送「select」。不合法或過期時由宿主拒絕。</summary>
public interface IAgitMapHost { void Select(AgitMapOpenRequest shown); }

/// <summary>AgitMapWindow 的 BIN 常數（sprites-and-strings.md 第 2.1 節）。</summary>
public static class AgitMapLayout {
    public const int MarkerSurf=176,MarkerSurfRegion7=34;
    public const int RightImageTop=160,RightImageBottom=164,CloseSurf=678,ActionSurf=674;
    public static readonly Point RightImageTopAt=new(312,56),RightImageBottomAt=new(312,110),CloseAt=new(213,297),ActionAt=new(90,299);
    public const int BlinkPeriodMs=1000,BlinkOnMs=800;
    public const int MaxNumber=70;

    /// <summary>區域 → 地圖 surf（dword_135DA34）；不支援的區域回傳 null（不開窗）。</summary>
    public static int? MapSurf(int region)=>region switch{1=>1482,4=>689,5=>741,6=>741,7=>801,8=>1482,_=>null};
    public static int MarkerSurfFor(int region)=>region==7?MarkerSurfRegion7:MarkerSurf;
    /// <summary>所有會用到的 surf（缺任何一個就整個面板停用）。</summary>
    public static readonly int[] AllSurfs=[689,741,801,1482,MarkerSurf,MarkerSurfRegion7,RightImageTop,RightImageBottom,CloseSurf,ActionSurf];

    // sub_113F600 寫入 0x13A96F0 + 0x230×(r−4) + 8n 的 (x, y)；只列有資料的前段，之後到 70 都是 (0, 0)。區域 6 全為 (0, 0)。
    static readonly Point[] Region4=P(14,140,22,131,31,124,50,115,65,113,67,97,87,94,97,99,93,119,73,129,54,133,79,141,47,160,31,166,24,159,7,164,16,173,28,189,51,195,56,179,
        65,170,92,171,103,154,115,144,98,181,75,205,76,226,85,231,94,222,102,210,119,205,171,158,192,151,116,225,128,237,134,227,143,231,176,219,186,199,150,256,168,250,174,237,
        161,271,195,239,208,230);
    static readonly Point[] Region5=P(30,114,40,96,51,106,63,116,67,135,20,140,75,182,92,198,138,201,154,207,184,236);
    static readonly Point[] Region7=P(209,197,203,200,208,212,194,203,199,209,187,210,193,216,183,216,178,224,173,230,195,190,160,212,181,191,186,197,165,204,175,208,164,223,
        172,220,167,215,154,237,147,239,137,272,155,253,150,258,96,217,108,225,148,271,132,277,129,282,116,280,121,284,126,289,107,272,111,275,122,226,112,289,102,289,87,247,
        108,243,45,226,96,240,86,239,80,244,76,249,90,251,95,256,84,258,88,262,97,270,92,277,72,190,62,222,56,211,44,208,115,103,109,109,92,102,109,123,97,110,78,114,87,113,
        116,118,67,129,60,134,54,138,74,136,68,141);
    static readonly Point[] Region8=P(55,195,68,209,87,241,94,235,101,251,109,245);
    static Point[] P(params int[] xy){var r=new Point[xy.Length/2];for(int i=0;i<r.Length;i++)r[i]=new(xy[2*i],xy[2*i+1]);return r;}

    /// <summary>有資料的筆數（區域 1 同 8）。</summary>
    public static int MarkerCount(int region)=>Table(region)?.Length??0;
    static Point[]? Table(int region)=>region switch{4=>Region4,5=>Region5,6=>[],7=>Region7,1 or 8=>Region8,_=>null};
    /// <summary>標記相對視窗原點的位置；編號不在 1–70 或區域不支援時 null（不畫）。</summary>
    public static Point? MarkerAt(int region,int number){
        if(number is <1 or >MaxNumber||Table(region) is not {} t)return null;
        return number<=t.Length?t[number-1]:Point.Empty;
    }
    public static bool MarkerOn(long elapsedMs)=>elapsedMs>=0&&elapsedMs%BlinkPeriodMs<BlinkOnMs;
}

public sealed class AgitMapPanel:IUiPanel {
    // 沒有 XML：照 HpMpPanel 的做法掛在已載入的 uistatus86 上（版面不讀它）。
    [UiPanel(150)] internal static UiPanelRegistration Registration=>new(AgitMapPanel.PanelId,"uistatus86",_=>new AgitMapPanel());   // W2-C AgitMapWindow (#550)
    public const string PanelId="AgitMap_Window";
    public static readonly Point Origin=Point.Empty;

    readonly Func<AgitMapOpenRequest?> source;
    Func<int,UiSurface?> surf=_=>null;
    bool usable;
    AgitMapOpenRequest? shown;long openedAt;long now;long dismissed=-1;string? pressed;
    Rectangle bounds;

    public AgitMapPanel(Func<AgitMapOpenRequest?>? source=null){this.source=source??(()=>AgitMapWindow.Current);}
    public IAgitMapHost? Host{get;private set;}
    public void Attach(IAgitMapHost host)=>Host=host;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>Visible?bounds:Rectangle.Empty;
    public bool Visible=>usable&&shown!=null;
    internal bool Usable=>usable;
    internal AgitMapOpenRequest? Shown=>shown;
    internal int? MapSurf=>shown is {} s?AgitMapLayout.MapSurf(s.Map.Region):null;
    internal Rectangle CloseRect=>Rect(AgitMapLayout.CloseSurf,AgitMapLayout.CloseAt);
    internal Rectangle ActionRect=>Rect(AgitMapLayout.ActionSurf,AgitMapLayout.ActionAt);
    /// <summary>目前這一格要畫的標記（絕對座標與 surf）；不畫時 null。</summary>
    internal (Point At,int Surf)? Marker{get{
        if(shown is not {} s||!AgitMapLayout.MarkerOn(now-openedAt)||AgitMapLayout.MarkerAt(s.Map.Region,s.Map.Number) is not Point p)return null;
        return (new(Origin.X+p.X,Origin.Y+p.Y),AgitMapLayout.MarkerSurfFor(s.Map.Region));
    }}

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    /// <summary>測試與正式共用：surf id → 圖（可為 null）。缺任一張 → 停用並記一行日誌（規則 5，不畫替代圖）。</summary>
    internal void Load(Func<int,UiSurface?> surfs){
        surf=surfs;
        var missing=AgitMapLayout.AllSurfs.Where(id=>surf(id) is not {Width:>0,Height:>0}).ToArray();
        usable=missing.Length==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少 surf {string.Join("、",missing)}");
    }

    Rectangle Rect(int id,Point at)=>surf(id) is {} s?new(Origin.X+at.X,Origin.Y+at.Y,s.Width,s.Height):Rectangle.Empty;

    public void Update(IUiGameState state){
        now=state.Now;
        var req=state.InWorld?source():null;
        if(req==null||req.Revision==dismissed||AgitMapLayout.MapSurf(req.Map.Region) is not int map){shown=null;pressed=null;return;}
        if(shown?.Revision!=req.Revision){
            shown=req;openedAt=now;pressed=null;
            bounds=new[]{Rect(map,Point.Empty),Rect(AgitMapLayout.RightImageTop,AgitMapLayout.RightImageTopAt),Rect(AgitMapLayout.RightImageBottom,AgitMapLayout.RightImageBottomAt),CloseRect,ActionRect}
                .Where(r=>!r.IsEmpty).Aggregate(Rectangle.Union);
        }
    }

    public void Paint(UiCanvas canvas){
        if(!Visible||MapSurf is not int map)return;
        void Draw(int id,Point at)=>canvas.DrawSurface(surf(id),Origin.X+at.X,Origin.Y+at.Y);
        Draw(map,Point.Empty);
        if(Marker is var (at,id))canvas.DrawSurface(surf(id),at.X,at.Y);
        Draw(AgitMapLayout.RightImageTop,AgitMapLayout.RightImageTopAt);
        Draw(AgitMapLayout.RightImageBottom,AgitMapLayout.RightImageBottomAt);
        Draw(AgitMapLayout.ActionSurf,AgitMapLayout.ActionAt);
        Draw(AgitMapLayout.CloseSurf,AgitMapLayout.CloseAt);
    }

    string? HitKey(Point p)=>CloseRect.Contains(p)?"close":ActionRect.Contains(p)?"action":null;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Button!=MouseButtons.Left)return inside;
        if(e.Kind==UiMouseKind.Down){pressed=HitKey(e.Location);return true;}
        if(e.Kind==UiMouseKind.Up){
            string? key=HitKey(e.Location),was=pressed;pressed=null;
            if(key=="close"&&was=="close"){dismissed=shown!.Revision;shown=null;commands.ReturnGameFocus();}
            else if(key=="action"&&was=="action")Host?.Select(shown!);   // 674（sub_543810）：C125 select（T276）
            return true;
        }
        return inside;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
