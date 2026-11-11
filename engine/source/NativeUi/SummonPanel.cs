using System.Text.Json;
namespace P06.NativeUi;

// M7-W（T223／#530）ui-summon：CSummonUI（SummonUI.xml＋summonList.xml，docs/reference/windows.md §1.3）。
// 版面全部在執行期從 Tile.pak 讀（不複製遊戲資源）；元件位置＝XML 座標（相對視窗），清單依 XML 的 Horizontal／Vertical／Gap
// 與元件底圖尺寸排列。缺 surf 的元件不畫（規則 5）；視窗底圖缺少時整個面板停用並記一行日誌。
// 已驗證（XML／協定）：快速 3 格、12 個等級鈕、每級最多 6 隻（2×3）、魅力標籤、確定／取消／返回、31 隻 action 與 surf。
// 已驗證（真實 surf 渲染，#530 報告）：等級鈕底圖本身寫著「等級 72 以上」…「等級 28 以上」，列優先由高到低。
// R9-M（T373／#857，依 followups-9.md §3「Q7-3 召喚窗」，CSummonUI 建構子 sub_BFFB60）已驗證並實作：
// - 怪物格（sub_C01090 → sub_C003F0）：513 開始拖曳；514 放開時沒拖曳＝立即召喚並關窗。OK（sub_C006E0）＝只關窗、不送。
// - 停用（sub_C00BF0 → sub_B0B6F0(1)）：等級公式 (等級−28)/4+7 < action%256（SummonCatalog.RingLevelAllows）**或** 魅力 < minCha。
// - 快速 3 格（sub_C00990 → sub_C00310、sub_C00A80、sub_C01150）：513 按下＝直接召喚該格；從怪物格拖來放開＝放進這一格；
//   空格底圖＝XML BaseSurf（4958）、提示＝XML Desc（13714「你可以標示一個怪物」）。3 格存在本機設定。
// - 徽章 LevelLabel_Image＝XML Surf（4944）＋type（本機 summonList.xml 沒有 type → 一律 4944）；Level_Label＝怪物等級。
// - 提示（sub_B0DAF0 FO 0x70CEF0）：desc-c.tbl 第 desc 行；`^`＝強制換行（最多 64 行）；框寬＝最寬一行 6 px×位元組數 +20、
//   取 4 的倍數，框高＝12×（行數＋1）；字色 0xBDF7（RGB565）。
// - 鍵盤 sub_C01060：27（ESC）→ 關窗。
// R10-T（T407／#924，FU13§4，D6–D10）已驗證：
// - OverSurf＝可用格滑鼠移上去時畫；DisableOverSurf="13" 不是 surf 編號，是 XmlBoardImage 的色塊強度（sub_AED660 存 +393）：
//   停用格**每一幀都畫**一塊格子大小的 palette[0] 半透明矩形（sub_B44890 → sub_970B80），與滑鼠無關；
//   畫在怪物圖（＝格子的 BaseSurf）之上、徽章與等級字（子元件）之下。強度 1–30 半透明、≥31 實心、0＝不畫。
// - 停用格不吃點擊、不能拖，提示仍顯示（sub_B47010，FU13§4.3）。
// - 可用格的順序（sub_B45320）：Base → Highlight（滑鼠在上且沒按著）→ Over（滑鼠在上）→ Push（按著）；SummonUI 的格子沒有 Highlight／Push。
// 推測（真機再確認）：palette[0] 先用黑色、強度刻度 /32（13/32 ≈ 40%）；快速格裡的怪物停用時也畫色塊（同一套 XmlListItem 規則）。
// 推測（報告已列，真機再確認 M39）：
// - 「放進這一格（或交換）」的交換：拖來的怪物已在另一格時兩格互換；快速格有內容時只畫怪物圖（不畫空格底圖 4958）。
// - 提示框位置：XML 的 HAlign="center" VAlign="bottom" 解讀成「格子正下方置中」，再夾進 800×600；沒有 `^` 時不自動換行（框寬來源沒追）；
//   框底色＝XML BackColor（0,0,0）不透明；字從框內 (10,6) 起每行 12 px。
// - 本機設定的原版格式待驗證（寫入函式 sub_C009F0 的呼叫者 sub_59DD30／sub_998840）：本引擎存在 runtime/user/summon-quick.json
//   （全角色共用；原版是否分角色待驗證）。
// - 召喚後關窗只在本機藏起這一次請求（不呼叫宿主 Close，以免關掉請求後送出被判為過期）。
// R10-O（T399／#914，gap-matrix 15.12.6、FU9§3.1）：
// - 送出（sub_C00F50）：格子可不可點看面板上次讀到的狀態（停用＝等級公式或魅力）；按下去要送時再用宿主**目前**的等級只檢查等級公式，
//   不成立 → 宿主顯示 743（ISummonUiHost.LevelTooLow）、不送，然後關窗（FU9「最後一律關窗」；沒送出，所以直接請宿主關掉這一次請求）。
// - 拖曳中游標上的怪物圖：SummonUI.xml 根層的 Image SummonDrag_Image（Surf="0;2;13"，surf 0＝執行期換成被拖的怪物圖）。
//   推測（真機再確認 M39）：圖的中心對齊游標；第三欄 13 當透明度 13/32（同 DisableOverSurf 的 13，FU13§4.2）；第二欄 2 的意義沒查。
//   XML 沒有這個元素或怪物圖缺 surf → 不畫（規則 5）。

public sealed record SummonMonster(int Id,int Level,string Action,int Desc,int Surf,int MinCha,int Type=0);

/// <summary>summonList.xml：&lt;summonlist&gt;&lt;summon level&gt;&lt;monster id level action desc surf minCha [type]/&gt;…</summary>
public static class SummonCatalog {
    public const string FileName="summonList";
    public static IReadOnlyList<SummonMonster> Parse(UiLayout list){
        if(list.Root.Name!="summonlist")throw new UiLayoutException(list.FileName,list.Root.Path,"根元素不是 summonlist");
        var rows=new List<SummonMonster>();var ids=new HashSet<int>();var actions=new HashSet<string>(StringComparer.Ordinal);var levels=new HashSet<int>();
        foreach(var group in list.Root.ChildrenNamed("summon")){
            int level=group.Int("level");
            if(level<=0||!levels.Add(level))throw new UiLayoutException(list.FileName,group.Path,$"summon level {level} 不合法或重複");
            foreach(var m in group.ChildrenNamed("monster")){
                string action=m.RequiredAttr("action").Trim();
                var row=new SummonMonster(m.Int("id"),m.Int("level"),action,m.Int("desc"),m.Int("surf"),m.Int("minCha"),m.TryInt("type")??0);
                if(row.Level!=level)throw new UiLayoutException(list.FileName,m.Path,"monster level 與所屬 summon level 不同");
                if(!P06.PetPackets.ValidSummonAction(action))throw new UiLayoutException(list.FileName,m.Path,$"action=\"{action}\" 不是召喚編號");
                if(!ids.Add(row.Id)||!actions.Add(action))throw new UiLayoutException(list.FileName,m.Path,"monster id 或 action 重複");
                if(row.Surf<0||row.MinCha<0||row.Type<0)throw new UiLayoutException(list.FileName,m.Path,"surf／minCha／type 為負數");
                rows.Add(row);
            }
        }
        if(rows.Count==0)throw new UiLayoutException(list.FileName,list.Root.Path,"沒有任何 monster");
        return rows;
    }
    public static int[] Levels(IReadOnlyList<SummonMonster> rows)=>rows.Select(r=>r.Level).Distinct().Order().ToArray();
    /// <summary>
    /// T276（#667）召喚控制戒指入口：S36 type 5＋施放技能 51 時，AbilityStatus.cs 的 TryOpenRingWindow 開本機請求（Source＝"local"），
    /// 本面板照常顯示。送出前的等級檢查（sub_C00890 FO 0x7FFC90／sub_C00F50 FO 0x800350）：
    /// (等級 − 28)/4 + 7 &lt; action % 256 時顯示 743「等級太低而無法召喚怪物」、不送。整數除法向零取整（同 x86 idiv）。
    /// </summary>
    public static bool RingLevelAllows(int level,string action)=>
        P06.PetPackets.ValidSummonAction(action)&&uint.TryParse(action,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out uint v)&&(level-28)/4+7>=(int)(v%256);
    /// <summary>R9-M：怪物格可用＝等級公式成立且魅力 ≥ minCha（sub_C00BF0；任一不成立 → sub_B0B6F0(1) 停用）。沒收到等級或魅力 → 停用（不推定）。</summary>
    public static bool Usable(int? level,int? charisma,SummonMonster m)=>level is int lv&&charisma is int cha&&RingLevelAllows(lv,m.Action)&&cha>=m.MinCha;
}

/// <summary>面板讀的狀態（由 PetUi.cs 的轉接提供）。Level／Charisma 是原服 S8 的值，沒送就是 null（顯示空白）。</summary>
public sealed record SummonUiState(bool Open,long Revision,string Source,int? Level,int? Charisma,bool CanSend);
public interface ISummonUiHost {
    SummonUiState State{get;}
    /// <summary>召喚：對 shown 這一次的請求送出 monster；不合法或已過期時由宿主拒絕。</summary>
    void Choose(SummonUiState shown,SummonMonster monster);
    void Close(SummonUiState shown);
    /// <summary>R10-O：送出前的等級檢查不通過（sub_C00F50）→ 顯示 743。預設不做事（測試宿主）；接著面板會呼叫 Close。</summary>
    void LevelTooLow(SummonUiState shown,SummonMonster monster){}
}

/// <summary>R9-M：快速 3 格的本機保存（每格是 summonList 的 monster id，null＝空格）。</summary>
public interface ISummonQuickStore {
    int?[] Load();
    void Save(int?[] ids);
}
/// <summary>只存在記憶體（測試與沒有設定檔時）。</summary>
public sealed class SummonQuickMemory:ISummonQuickStore {
    int?[] ids=[];
    public int Saves{get;private set;}
    public int?[] Load()=>ids.ToArray();
    public void Save(int?[] ids){this.ids=ids.ToArray();Saves++;}
}
/// <summary>
/// runtime/user/summon-quick.json：{"v":1,"quick":[id 或 null ×3]}。原版存檔格式待驗證（sub_C009F0），本格式是本引擎自訂。
/// 讀不到或格式不對 → 全部空格並記一行日誌；寫入先寫 .tmp 再取代。
/// </summary>
public sealed class SummonQuickFile(string path):ISummonQuickStore {
    public const string FileName="summon-quick.json";
    public static string DefaultPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","runtime","user",FileName));
    public string FilePath=>path;
    public int?[] Load(){
        try{return File.Exists(path)?Parse(File.ReadAllText(path)):[];}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException){UiResourceLog.Write($"{FileName} 無法讀取，快速召喚格全部空白：{ex.Message}");return [];}
    }
    public void Save(int?[] ids){
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp=path+".tmp";File.WriteAllText(tmp,Serialize(ids));File.Move(tmp,path,true);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){UiResourceLog.Write($"{FileName} 無法寫入，快速召喚格只保留到關閉程式：{ex.Message}");}
    }
    public static string Serialize(int?[] ids){
        if(ids.Length>SummonPanel.QuickSlots)throw new ArgumentException("快速召喚格超過 3 格",nameof(ids));
        return JsonSerializer.Serialize(new{v=1,quick=ids});
    }
    /// <summary>格式錯誤丟 FormatException。</summary>
    public static int?[] Parse(string json){
        try{
            using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("v",out var v)||v.ValueKind!=JsonValueKind.Number||!v.TryGetInt32(out int version)||version!=1)throw new FormatException("v 不是 1");
            if(!root.TryGetProperty("quick",out var quick)||quick.ValueKind!=JsonValueKind.Array||quick.GetArrayLength()>SummonPanel.QuickSlots)throw new FormatException("quick 不是最多 3 格的陣列");
            return quick.EnumerateArray().Select(e=>e.ValueKind==JsonValueKind.Null?(int?)null
                :e.ValueKind==JsonValueKind.Number&&e.TryGetInt32(out int id)?id:throw new FormatException("quick 的元素不是整數或 null")).ToArray();
        }catch(JsonException ex){throw new FormatException("不是 JSON："+ex.Message,ex);}
    }
}

/// <summary>R9-M 提示框：Box＝邏輯座標，Lines＝依 `^` 切好的行。</summary>
public sealed record SummonTooltip(Rectangle Box,IReadOnlyList<string> Lines);

public sealed class SummonPanel:IUiMovablePanel,IUiClosablePanel {
    [UiPanel(30)] internal static UiPanelRegistration Registration=>new(SummonPanel.PanelId,SummonPanel.LayoutName,
        root=>new SummonPanel(root.Resources){QuickStore=new SummonQuickFile(SummonQuickFile.DefaultPath),DescText=ClientDesc});   // ui-summon (M7-W, #530; R9-M #857)
    public const string PanelId="SummonMonster_Window",LayoutName="SummonUI";
    public const int QuickSlots=3,LevelButtons=12,MonsterSlots=6;
    /// <summary>sub_B0DAF0：`^` 換行最多 64 行；每位元組 6 px；框寬 +20；行高 12。</summary>
    public const int TooltipMaxLines=64,TooltipCharWidth=6,TooltipExtraWidth=20,TooltipLineHeight=12,TooltipTextX=10,TooltipTextY=6;
    /// <summary>sub_C00BF0：提示字色 0xBDF7（RGB565）。</summary>
    public const ushort TooltipColor565=0xBDF7;
    public static Color TooltipTextColor=>From565(TooltipColor565);
    internal static Color From565(ushort c)=>Color.FromArgb((c>>11&31)*255/31,(c>>5&63)*255/63,(c&31)*255/31);
    internal sealed record Part(string Key,Rectangle Rect,int Base,int? Push);
    /// <summary>DisableOver＝停用色塊強度（DisableOverSurf 的數字，不是 surf；null＝XML 沒寫）；Highlight／Push＝ListItem 的 HighlightSurf／PushSurf（沒寫＝null）。</summary>
    internal sealed record Slot(Part Part,Point LabelOrigin,Size LabelSize,string Caption,Point LabelImageAt,int LabelImage,int Over,int? DisableOver,int? TooltipDesc,Color TooltipBack,int? Highlight=null,int? Push=null);
    /// <summary>FU13§4.1：palette[0]（dword_1369D70[0]）推測是黑色。</summary>
    public static readonly Color DisableOverColor=Color.Black;
    /// <summary>sub_B44890：強度 ≥ 31 畫實心色塊。</summary>
    public const int DisableOverSolid=31;
    /// <summary>強度 → 色塊不透明度（0–255）：0＝不畫、1–30＝強度/32、≥31＝實心。</summary>
    public static int DisableOverAlpha(int strength)=>strength<=0?0:strength>=DisableOverSolid?255:(strength*255+DragAlphaScale/2)/DragAlphaScale;

    readonly UiResources? resources;
    Func<int,UiSurface?> surf=_=>null;
    IReadOnlyList<SummonMonster> monsters=[];int[] levels=[];
    Point origin;Size size;int moveable;int baseSurf,frameSurf;bool usable;
    Part close=null!,ok=null!,cancel=null!,back=null!;
    (Point At,int Surf) title,charismaText,charismaImage;Rectangle charismaLabel;
    Slot[] quick=[],monsterSlots=[];Part[] levelParts=[];
    readonly SummonMonster?[] quickMonsters=new SummonMonster?[QuickSlots];
    SummonUiState? shown;long dismissed=-1;int? selectedLevel;string? pressed,hover;
    SummonMonster? dragging;bool dragMoved;Point dragAt;
    /// <summary>SummonDrag_Image 的透明度（第三欄，/32）；XML 沒有這個元素 → null（不畫拖曳圖）。</summary>
    int? dragAlpha;
    public const int DragAlphaScale=32;

    public SummonPanel(UiResources? resources=null){this.resources=resources;}
    public ISummonUiHost? Host{get;private set;}
    public void Attach(ISummonUiHost host)=>Host=host;
    /// <summary>快速 3 格的保存處；Load 時讀一次，放進格子時寫入。預設只在記憶體。</summary>
    public ISummonQuickStore QuickStore{get;set;}=new SummonQuickMemory();
    /// <summary>desc-c.tbl 第 id 行（null＝沒有表或查不到 → 不畫提示）。</summary>
    public Func<int,string?> DescText{get;set;}=_=>null;
    /// <summary>主客戶端 Text.pak 的 desc-c.tbl（PlayerChatProtocol.Desc，執行期在記憶體解出）。</summary>
    public static string? ClientDesc(int id)=>PlayerChatProtocol.Desc is {} t&&id>=0&&id<t.Count&&t[id] is {Length:>0} s?s:null;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>視窗本身（命中判斷與拖曳用）。</summary>
    internal Rectangle Window=>new(origin,size);
    /// <summary>視窗＋顯示中的提示框（繪製範圍）。</summary>
    public Rectangle Bounds{get{
        var r=Window;if(!Visible)return r;
        if(Tooltip is {} tip)r=Rectangle.Union(r,tip.Box);
        if(DragImage is {} drag)r=Rectangle.Union(r,drag);
        return r;
    }}
    /// <summary>R10-O：拖曳中（已移出按下的格子）的怪物圖位置＝圖中心對齊游標（推測）；沒在拖、XML 沒有 SummonDrag_Image 或缺圖 → null。</summary>
    internal Rectangle? DragImage=>dragAlpha!=null&&dragging is {} m&&dragMoved&&surf(m.Surf) is {} s
        ?new Rectangle(dragAt.X-s.Width/2,dragAt.Y-s.Height/2,s.Width,s.Height):null;
    internal int? DragAlpha=>dragAlpha;
    public bool Visible=>usable&&shown is {Open:true} s&&s.Revision!=dismissed;
    internal bool Usable=>usable;
    internal int? SelectedLevel=>selectedLevel is int i?LevelOf(i):null;
    /// <summary>等級鈕 i（列優先）→ 等級：第 0 鈕是最高級（底圖文字「等級 72 以上」），最後一鈕是最低級。</summary>
    int LevelOf(int button)=>levels[levels.Length-1-button];
    internal IReadOnlyList<SummonMonster> Monsters=>monsters;
    internal IReadOnlyList<SummonMonster?> Quick=>quickMonsters;
    internal string? Hover=>hover;
    internal SummonMonster? Dragging=>dragging;

    public void Load(UiLayout layout,SurfCatalog surfs){
        var list=(resources??throw new UiLayoutException(layout.FileName,"","沒有客戶端資源，無法讀取 summonList.xml")).Xml.Load(SummonCatalog.FileName);
        Load(layout,list,surfs.Get);
    }
    /// <summary>測試與正式共用：layout＝SummonUI.xml、list＝summonList.xml、surfs＝surf id → 圖（可為 null）。</summary>
    internal void Load(UiLayout layout,UiLayout list,Func<int,UiSurface?> surfs){
        surf=surfs;monsters=SummonCatalog.Parse(list);levels=SummonCatalog.Levels(monsters);
        var window=Direct(layout.Root,"Window","SummonMonster_Window");
        dragAlpha=DragAlphaOf(layout.Root.ChildrenNamed("Image").FirstOrDefault(c=>c.Attr("Name")==DragImageName));
        origin=new(window.Int("X"),window.Int("Y"));moveable=window.Int("Moveable");
        baseSurf=SurfId(window,"BaseSurf");frameSurf=SurfId(window,"FrameSurf");
        var missing=new List<string>();
        Size? Measure(int id){var s=surf(id);return s==null?null:new Size(s.Width,s.Height);}
        Part Button(UiNode n,Point at,string key){
            int b=SurfId(n,"BaseSurf");int? push=n.Attr("PushSurf")!=null?SurfId(n,"PushSurf"):null;
            var s=Measure(b);if(s==null)missing.Add($"{n.Path} BaseSurf {b}");
            return new(key,new(at,s??Size.Empty),b,push);
        }
        Point Pos(UiNode n,Point parent)=>new(parent.X+n.Int("X"),parent.Y+n.Int("Y"));
        var windowSize=Measure(baseSurf);
        if(windowSize==null)missing.Add($"{window.Path} BaseSurf {baseSurf}");
        size=windowSize??Size.Empty;
        var zero=Point.Empty;
        close=Button(Direct(window,"Button","CloseWindow_Button"),Pos(Direct(window,"Button","CloseWindow_Button"),zero),"close");
        ok=Button(Direct(window,"Button","Ok_Button"),Pos(Direct(window,"Button","Ok_Button"),zero),"ok");
        cancel=Button(Direct(window,"Button","Cancel_Button"),Pos(Direct(window,"Button","Cancel_Button"),zero),"cancel");
        back=Button(Direct(window,"Button","Back_Button"),Pos(Direct(window,"Button","Back_Button"),zero),"back");
        (Point,int) Image(string name){var n=Direct(window,"Image",name);return (Pos(n,zero),SurfId(n,"Surf"));}
        title=Image("SummonTitle_Image");charismaText=Image("CharismaText_Image");charismaImage=Image("CharismaLabel_Image");
        var cha=Direct(window,"Label","Charisma_Label");charismaLabel=new(Pos(cha,zero),new(cha.Int("Width"),cha.Int("Height")));

        Slot ToSlot(UiNode item,Rectangle rect,string key){
            var label=Direct(item,"Label","Level_Label");var image=Direct(item,"Image","LevelLabel_Image");
            var tip=item.ChildrenNamed("Tooltip").FirstOrDefault(c=>c.Attr("Name")=="Desc_Tooltip");
            return new(new(key,rect,SurfId(item,"BaseSurf"),null),new(label.Int("X"),label.Int("Y")),new(label.Int("Width"),label.Int("Height")),
                label.RequiredAttr("Caption"),new(image.Int("X"),image.Int("Y")),SurfId(image,"Surf"),SurfId(item,"OverSurf"),
                item.Attr("DisableOverSurf")!=null?DisableOverStrength(item):null,tip?.TryInt("Desc"),ParseColor(tip?.Attr("BackColor"))??Color.Black,
                item.Attr("HighlightSurf")!=null?SurfId(item,"HighlightSurf"):null,item.Attr("PushSurf")!=null?SurfId(item,"PushSurf"):null);
        }
        var quickList=Direct(window,"List","QuickSummonLevel_List");
        quick=Flow(quickList,Pos(quickList,zero),Measure,missing).Select((x,i)=>ToSlot(x.Node,x.Rect,"quick:"+i)).ToArray();
        var levelList=Direct(window,"List","Level_List");
        levelParts=Flow(levelList,Pos(levelList,zero),Measure,missing).Select((x,i)=>Button(x.Node,x.Rect.Location,"level:"+i)).ToArray();
        var control=Direct(window,"Control","SummonLevelList_Control");var inner=Direct(control,"List","SummonLevel_List");
        monsterSlots=Flow(inner,Pos(inner,Pos(control,zero)),Measure,missing).Select((x,i)=>ToSlot(x.Node,x.Rect,"monster:"+i)).ToArray();
        if(quick.Length!=QuickSlots)throw new UiLayoutException(layout.FileName,quickList.Path,$"快速召喚格數 {quick.Length}，應為 {QuickSlots}");
        if(levelParts.Length!=LevelButtons||levels.Length!=LevelButtons)throw new UiLayoutException(layout.FileName,levelList.Path,$"等級鈕 {levelParts.Length} 個、summonList 等級 {levels.Length} 個，應各為 {LevelButtons}");
        if(monsterSlots.Length!=MonsterSlots)throw new UiLayoutException(layout.FileName,inner.Path,$"怪物格數 {monsterSlots.Length}，應為 {MonsterSlots}");
        if(levels.Any(l=>monsters.Count(m=>m.Level==l)>MonsterSlots))throw new UiLayoutException(list.FileName,list.Root.Path,$"同一等級超過 {MonsterSlots} 隻");
        var saved=QuickStore.Load();
        for(int i=0;i<QuickSlots;i++)quickMonsters[i]=i<saved.Length&&saved[i] is int id?monsters.FirstOrDefault(m=>m.Id==id):null;
        usable=missing.Count==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少版面所需的 surf（{string.Join("；",missing)}）");
    }

    static UiNode Direct(UiNode parent,string element,string name){
        var found=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToList();
        if(found.Count!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path,$"{element} Name=\"{name}\" {(found.Count==0?"缺少":"不唯一")}");
        return found[0];
    }
    public const string DragImageName="SummonDrag_Image";
    /// <summary>
    /// SummonDrag_Image 的 Surf="0;2;13"：第三欄當透明度（0–32，推測）；只有 surf 欄（"0"）→ 不透明 32；元素不存在 → null。
    /// 第三欄不是 0–32 的整數 → 版面錯誤（不猜）。
    /// </summary>
    internal static int? DragAlphaOf(UiNode? image){
        if(image==null)return null;
        var parts=image.RequiredAttr("Surf").Split(';');
        if(parts.Length<3)return DragAlphaScale;
        if(!int.TryParse(parts[2].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int a)||a>DragAlphaScale)
            throw new UiLayoutException(image.Layout.FileName,image.Path,$"{DragImageName} Surf=\"{image.Attr("Surf")}\" 第三欄不是 0–{DragAlphaScale}");
        return a;
    }
    /// <summary>DisableOverSurf="13" → 13（sub_AED660 以 atoi 存成色塊強度，FU13§4.1）；不是非負整數 → 版面錯誤（不猜）。</summary>
    internal static int DisableOverStrength(UiNode item){
        string raw=item.RequiredAttr("DisableOverSurf").Trim();
        if(!int.TryParse(raw.Split(';')[0].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int v))
            throw new UiLayoutException(item.Layout.FileName,item.Path,$"DisableOverSurf=\"{raw}\" 不是色塊強度");
        return v;
    }
    /// <summary>"0,0,0" → Color；格式不對 → null。</summary>
    static Color? ParseColor(string? raw){
        if(raw==null)return null;
        var parts=raw.Split(',');
        if(parts.Length!=3)return null;
        var v=new int[3];
        for(int i=0;i<3;i++)if(!int.TryParse(parts[i].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out v[i])||v[i]>255)return null;
        return Color.FromArgb(v[0],v[1],v[2]);
    }
    /// <summary>"4893;1" → 4893（';' 後的欄位意義待查證，只取 surf id）。</summary>
    internal static int SurfId(UiNode n,string attr){
        string raw=n.RequiredAttr(attr).Trim();string head=raw.Split(';')[0].Trim();
        if(!int.TryParse(head,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int v))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 surf id");
        return v;
    }
    /// <summary>List 排列：Horizontal="1" 或 Vertical="1"（擇一）＋Gap；子 List 用 Width／Height，Button／ListItem 用 BaseSurf 尺寸。回傳葉節點。</summary>
    static List<(UiNode Node,Rectangle Rect)> Flow(UiNode list,Point at,Func<int,Size?> measure,List<string> missing){
        bool h=list.Attr("Horizontal")=="1",v=list.Attr("Vertical")=="1";
        if(h==v)throw new UiLayoutException(list.Layout.FileName,list.Path,"List 必須是 Horizontal 或 Vertical 其中之一");
        int gap=list.Int("Gap");var result=new List<(UiNode,Rectangle)>();int x=at.X,y=at.Y;
        foreach(var child in list.Children){
            Size s;
            if(child.Name=="List"){s=new(child.Int("Width"),child.Int("Height"));result.AddRange(Flow(child,new(x,y),measure,missing));}
            else if(child.Name is "ListItem" or "Button"){
                int b=SurfId(child,"BaseSurf");var m=measure(b);
                if(m==null){missing.Add($"{child.Path} BaseSurf {b}");m=Size.Empty;}
                s=m.Value;result.Add((child,new(new(x,y),s)));
            }
            else continue;
            if(h)x+=s.Width+gap;else y+=s.Height+gap;
        }
        return result;
    }

    public void Update(IUiGameState state){
        var now=Host?.State;
        if(now==null||!state.InWorld){shown=null;Clear();return;}
        if(shown?.Revision!=now.Revision)Clear();
        shown=now;
    }
    void Clear(){selectedLevel=null;pressed=null;hover=null;dragging=null;dragMoved=false;}

    IEnumerable<SummonMonster> LevelMonsters=>selectedLevel is int i?monsters.Where(m=>m.Level==LevelOf(i)):[];
    internal bool Enabled(SummonMonster m)=>shown is {} s&&SummonCatalog.Usable(s.Level,s.Charisma,m);
    /// <summary>目前可點的元件（相對視窗座標）：快速格兩頁都在（按下召喚、也是拖放目標）；停用的怪物格不在
    /// （已驗證，FU13§4.3：停用的 XmlListItem 按下不呼叫 MouseEvent、不開始拖曳；提示仍顯示，見 SlotAt）。</summary>
    internal IEnumerable<Part> Active(){
        yield return close;
        foreach(var q in quick)yield return q.Part;
        if(selectedLevel==null){foreach(var p in levelParts)yield return p;yield return cancel;}
        else{
            int k=0;foreach(var m in LevelMonsters){if(Enabled(m))yield return monsterSlots[k].Part;k++;}
            yield return back;
        }
        yield return ok;
    }
    internal string? HitKey(Point logical){
        var p=new Point(logical.X-origin.X,logical.Y-origin.Y);
        return Active().FirstOrDefault(x=>x.Rect.Contains(p))?.Key;
    }
    /// <summary>滑鼠所在的格子（快速格或目前頁有怪物的格子，含停用格），以及它的怪物（空快速格＝null）。</summary>
    (Slot Slot,SummonMonster? Monster)? SlotAt(Point logical){
        var p=new Point(logical.X-origin.X,logical.Y-origin.Y);
        for(int i=0;i<quick.Length;i++)if(quick[i].Part.Rect.Contains(p))return (quick[i],quickMonsters[i]);
        int k=0;foreach(var m in LevelMonsters){if(monsterSlots[k].Part.Rect.Contains(p))return (monsterSlots[k],m);k++;}
        return null;
    }
    (Slot Slot,SummonMonster? Monster)? SlotOf(string? key){
        if(key==null)return null;
        if(key.StartsWith("quick:",StringComparison.Ordinal)){int i=int.Parse(key[6..]);return (quick[i],quickMonsters[i]);}
        if(key.StartsWith("monster:",StringComparison.Ordinal)){int i=int.Parse(key[8..]);var m=LevelMonsters.ElementAtOrDefault(i);return m==null?null:(monsterSlots[i],m);}
        return null;
    }
    internal Rectangle Absolute(Rectangle r)=>new(r.X+origin.X,r.Y+origin.Y,r.Width,r.Height);
    internal Rectangle PartRect(string key)=>Absolute(new[]{close,ok,cancel,back}.Concat(levelParts).Concat(quick.Select(q=>q.Part)).Concat(monsterSlots.Select(s=>s.Part)).First(p=>p.Key==key).Rect);

    /// <summary>`^` 強制換行（最多 64 行）；沒有 `^` 就是一行（自動換行的框寬來源沒追，不做）。</summary>
    public static string[] TooltipLines(string text)=>text.Split('^').Take(TooltipMaxLines).ToArray();
    /// <summary>sub_B0DAF0：框寬＝最寬一行（6 px × 位元組數）+20 後進位到 4 的倍數；框高＝12 ×（行數＋1）。</summary>
    public static Size TooltipSize(IReadOnlyList<string> lines){
        int w=lines.Count==0?0:lines.Max(l=>TooltipCharWidth*BoardText.Bytes(l));
        return new((w+TooltipExtraWidth+3)/4*4,TooltipLineHeight*(lines.Count+1));
    }
    /// <summary>滑鼠所在格子的提示：怪物＝desc-c.tbl 第 desc 行；空快速格＝XML 的 Desc（13714）。查不到 → null。</summary>
    internal SummonTooltip? Tooltip{get{
        if(SlotOf(hover) is not {} hit)return null;
        int? desc=hit.Monster?.Desc??hit.Slot.TooltipDesc;
        if(desc is not int id||DescText(id) is not {Length:>0} text)return null;
        var lines=TooltipLines(text);var s=TooltipSize(lines);var r=Absolute(hit.Slot.Part.Rect);
        var canvas=UiRoot.Canvas;
        int x=Math.Clamp(r.X+(r.Width-s.Width)/2,canvas.Left,Math.Max(canvas.Left,canvas.Right-s.Width));
        int y=Math.Clamp(r.Bottom,canvas.Top,Math.Max(canvas.Top,canvas.Bottom-s.Height));
        return new(new(new(x,y),s),lines);
    }}

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        void Draw(int id,Point at)=>canvas.DrawSurface(surf(id),origin.X+at.X,origin.Y+at.Y);
        void Text(string text,Point at,Size box){
            if(text.Length==0)return;
            var m=UiText.Measure(canvas.Graphics,text);
            canvas.DrawText(text,origin.X+at.X+(box.Width-m.Width)/2,origin.Y+at.Y+(box.Height-m.Height)/2,Color.White);
        }
        void Button(Part p)=>Draw(pressed==p.Key&&p.Push is int push?push:p.Base,p.Rect.Location);
        // 格子（sub_B45320，FU13§4.2）：底圖 → 怪物圖（原版＝這一格的 BaseSurf）→
        //   停用：DisableOverSurf 色塊（常駐，與滑鼠無關）；可用：Highlight（滑鼠在上且沒按著）→ Over（滑鼠在上）→ Push（按著）
        //   → 子元件：徽章（XML Surf＋type）→ 等級字。
        void DrawSlot(Slot s,SummonMonster? m,bool quickSlot){
            var at=s.Part.Rect.Location;
            if(!quickSlot||m==null||surf(m.Surf)==null)Draw(s.Part.Base,at);
            if(m!=null)Draw(m.Surf,at);
            if(m!=null&&!Enabled(m)){
                if(s.DisableOver is int strength)FillDisableOver(canvas,Absolute(s.Part.Rect),strength);
            }else{
                bool over=hover==s.Part.Key,down=pressed==s.Part.Key;
                if(over&&!down&&s.Highlight is int hl)Draw(hl,at);
                if(over)Draw(s.Over,at);
                if(down&&s.Push is int push)Draw(push,at);
            }
            Draw(s.LabelImage+(m?.Type??0),new(at.X+s.LabelImageAt.X,at.Y+s.LabelImageAt.Y));
            Text(m?.Level.ToString(System.Globalization.CultureInfo.InvariantCulture)??s.Caption,new(at.X+s.LabelOrigin.X,at.Y+s.LabelOrigin.Y),s.LabelSize);
        }
        Draw(baseSurf,Point.Empty);Draw(frameSurf,Point.Empty);
        Button(close);
        Draw(title.Surf,title.At);Draw(charismaText.Surf,charismaText.At);Draw(charismaImage.Surf,charismaImage.At);
        Text(shown?.Charisma?.ToString()??"",charismaLabel.Location,charismaLabel.Size);
        for(int i=0;i<quick.Length;i++)DrawSlot(quick[i],quickMonsters[i],true);
        if(selectedLevel==null){foreach(var p in levelParts)Button(p);Button(cancel);}
        else{
            int k=0;
            foreach(var m in LevelMonsters){DrawSlot(monsterSlots[k],m,false);k++;}
            Button(back);
        }
        Button(ok);
        if(Tooltip is {} tip){
            var slot=SlotOf(hover)!.Value.Slot;
            using(var brush=new SolidBrush(slot.TooltipBack))canvas.Graphics.FillRectangle(brush,tip.Box);
            for(int i=0;i<tip.Lines.Count;i++)canvas.DrawText(tip.Lines[i],tip.Box.X+TooltipTextX,tip.Box.Y+TooltipTextY+i*TooltipLineHeight,TooltipTextColor);
        }
        // R10-O：拖曳中的怪物圖畫在最上層（SummonDrag_Image，透明度 dragAlpha/32，推測）。
        if(DragImage is {} drag&&dragging is {} dm&&surf(dm.Surf) is {} ds&&dragAlpha is int alpha)DrawTranslucent(canvas,ds,drag.Location,alpha);
    }
    /// <summary>sub_B44890 → sub_970B80：格子大小的 palette[0] 色塊，不透明度＝DisableOverAlpha(強度)。</summary>
    internal static void FillDisableOver(UiCanvas canvas,Rectangle rect,int strength){
        int a=DisableOverAlpha(strength);
        if(a<=0||rect.Width<=0||rect.Height<=0)return;
        using var brush=new SolidBrush(Color.FromArgb(a,DisableOverColor));
        canvas.Graphics.FillRectangle(brush,rect);
    }
    static void DrawTranslucent(UiCanvas canvas,UiSurface s,Point at,int alpha){
        if(alpha<=0)return;
        if(alpha>=DragAlphaScale){canvas.DrawSurface(s,at.X,at.Y);return;}
        using var attributes=new System.Drawing.Imaging.ImageAttributes();
        attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix{Matrix33=alpha/(float)DragAlphaScale});
        canvas.Graphics.DrawImage(s.Bitmap,new Rectangle(at.X,at.Y,s.Width,s.Height),0,0,s.Width,s.Height,GraphicsUnit.Pixel,attributes);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.Kind!=UiMouseKind.Wheel)hover=SlotAt(e.Location) is {} h?h.Slot.Part.Key:null;
        if(e.Kind==UiMouseKind.Move){
            if(dragging!=null)dragAt=e.Location;
            if(dragging!=null&&!dragMoved&&pressed!=null&&!PartRect(pressed).Contains(e.Location))dragMoved=true;
            return pressed!=null||Window.Contains(e.Location);
        }
        if(e.Button!=MouseButtons.Left)return Window.Contains(e.Location);
        if(e.Kind==UiMouseKind.Down){
            pressed=HitKey(e.Location);dragging=null;dragMoved=false;
            if(pressed!=null&&pressed.StartsWith("quick:",StringComparison.Ordinal)){var q=quickMonsters[int.Parse(pressed[6..])];if(q!=null)Summon(q);}
            else if(SlotOf(pressed) is {Monster:{} m}){dragging=m;dragAt=e.Location;}
            return true;
        }
        if(e.Kind==UiMouseKind.Up){
            string? key=HitKey(e.Location),was=pressed;var drag=dragging;bool moved=dragMoved;
            pressed=null;dragging=null;dragMoved=false;
            if(drag!=null){
                if(moved){if(key!=null&&key.StartsWith("quick:",StringComparison.Ordinal))Assign(int.Parse(key[6..]),drag);}
                else if(key==was)Summon(drag);
                return true;
            }
            if(key!=null&&key==was)Activate(key);
            return true;
        }
        return Window.Contains(e.Location);
    }
    /// <summary>
    /// R7-H（#810）：堆疊關窗（被擠出、全域 ESC；sub_A57C60 的 vtable+36）＝關閉鈕：記下這次請求、請宿主關掉。
    /// 已關著（沒有請求或已關過這一次）時不呼叫宿主。R9-M：本面板的 Key 也吃 ESC（sub_C01060 的 27）。
    /// </summary>
    public void Close(){
        if(shown is not {Open:true} s||s.Revision==dismissed)return;
        dismissed=s.Revision;Clear();Host?.Close(s);
    }
    /// <summary>
    /// 召喚（sub_C00F50／sub_C00890）：可用的怪物才送，送出後一律關窗。關窗只在本機藏起這一次請求（不呼叫宿主 Close：
    /// 送出是非同步的，先關請求會讓送出被判過期）；送出成功時原服請求的 revision 也會改變。宿主不能送時不動作、窗保留。
    /// R10-O：送之前用宿主目前的等級再檢查一次等級公式（只看等級，FU9§3.1）；不成立 → 743（宿主顯示）、不送、關窗（請宿主關請求）。
    /// 宿主的請求已換成別次（revision 不同）或目前沒有等級 → 不動作。
    /// </summary>
    void Summon(SummonMonster m){
        if(shown is not {} s||Host is not {} host||!s.CanSend||!Enabled(m))return;
        var now=host.State;
        if(now.Revision!=s.Revision||now.Level==null)return;
        if(!SendAllowed(now.Level,m)){host.LevelTooLow(s,m);Close();return;}
        host.Choose(s,m);dismissed=s.Revision;Clear();
    }
    /// <summary>sub_C00F50 的送出檢查：只看等級公式（不看魅力）。沒有等級 → 不送（不推定）。</summary>
    public static bool SendAllowed(int? level,SummonMonster m)=>level is int lv&&SummonCatalog.RingLevelAllows(lv,m.Action);
    /// <summary>從怪物格拖來放進快速格 i；同一隻已在另一格時兩格互換（推測）。寫入本機設定。</summary>
    void Assign(int i,SummonMonster m){
        int j=Array.FindIndex(quickMonsters,q=>q?.Id==m.Id);
        if(j==i)return;
        if(j>=0)quickMonsters[j]=quickMonsters[i];
        quickMonsters[i]=m;
        QuickStore.Save(quickMonsters.Select(q=>q?.Id).ToArray());
    }
    void Activate(string key){
        if(shown is null)return;
        if(key is "close" or "cancel" or "ok"){Close();return;}   // OK（sub_C006E0）＝只關窗
        if(key=="back"){selectedLevel=null;return;}
        if(key.StartsWith("level:",StringComparison.Ordinal)){selectedLevel=int.Parse(key[6..]);return;}
    }
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible||!e.Down||e.Repeat||e.KeyData!=Keys.Escape)return false;
        Close();return true;
    }

    public bool IsDragHandle(Point location)=>Visible&&Window.Contains(location)&&location.Y-origin.Y<moveable&&HitKey(location)==null;
    public void MoveTo(Point location)=>origin=location;
}
