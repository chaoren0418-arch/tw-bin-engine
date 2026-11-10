using System.Drawing.Imaging;
using System.Globalization;
namespace P06.NativeUi;

// T327 (#765, P-D of docs/reference/pak-resources.md §6, row 4:CreateUI): the native character create screen.
// TW16 reads CreateUI.xml; 0-客戶端 does not ship it (PAK§4.2: all paks checked). It is read at run time from the reference
// client through UiResources.Shared.Xml (P-A, SHA-256 logged as "ui-xml-ref"); nothing is copied into the repository.
//
// Loading. Same as SelectCharPanel: host layout "uistatus86", Load reads CreateUI itself; unusable → Unavailable and the
// select screen's Create_Button opens the existing WinForms CharacterCreationDialog (ICharSelectHost.CreateFallback).
//
// Images. Surfs go through SurfCatalog.Get (local first, then the T322/#760 reference fallback). Not in the pinned local
// Sprite archives (PakViewer lists, see docs/reports/issue-765.md): 7099, 7100, 7128–7143, 7156, 7170, 7171, 7224, 7225,
// 7707, 7708; kor25 has all of them ("<id>c.img" / "<id>.img"). 7707/7708 (Warrior) are never looked up: 3.81 has no
// Warrior class. Missing images are not drawn (no substitute) and listed in Missing.
//
// Data. The request is the existing CharacterCreation; it is checked with CharacterManagementPackets.Validate (the same
// rules as the WinForms dialog) and sent through LoginConnection.CreateCharacter with the select screen's stamp.
//
// Layout rules (推測; the CreateUI binder was not decompiled for this):
// - Two full-screen scenes: Class_Select_Scene (class + sex) → Next → Stat_Setting_Scene (points + name) → OK sends.
//   Prev on the class scene returns to the select screen; Prev on the stat scene returns to the class scene.
// - Paint/hit order and coordinates: as SelectCharPanel (reverse sibling order, parent before children, relative X/Y).
// - Class_Buttons: Prince…Illusionist = class 0…6 (CharacterManagementPackets.Classes). The selected class button is drawn
//   with its PushSurf. Warrior_Button is not built (no 3.81 class 7). Male_Back / Female_Back are drawn only for the
//   selected sex.
// - ClassName_Screen 7099;4 / ClassDesc_Screen 7142;4 are drawn as they are (one image; whether the binder picks a frame per
//   class is 待驗證). Front_Image / Back_Image of the class scene have no image id: R10-N below sets them at run time.
// - Stat_Change_Control: the six stat Controls carry Point + Down/Up (Strength, Dexterity, Constitution, Wisdom, Charisma,
//   Intelligence = CharacterCreation order). Up while points remain and the value is below base+points; Down above base.
//   Remain_Point = 75 − sum. Reset_Button = class base values.
// - Not built: Customizing_Win (derived bonus table, the engine has no values — same as #763); Customizing_Button stays
//   disabled. StatType_Group: only the selected class's Attack/Defense/Mix buttons are shown.
//
// R9-O (T375/#859, FU9§14.2; TW16 CCreateUI decompiled read-only, docs/reports/issue-859.md):
// - Attack/Defense/Mix (Auto_StatSettingScene → sub_EE2F70 → sub_EE6760): type 1/2/3 (the button's index in its class
//   Control + 1; here by name, same order as CreateUI.xml). The same type again = Reset (sub_EE5D60: type 0, base stats);
//   otherwise the six stats are set to the CharAttrType.tbl row (class, type). The selected type's button is drawn with its
//   PushSurf. Entering the stat scene sets type 0 and applies type 1 (sub_EE6DD0 → sub_EE6760(1)). A class change or Reset
//   sets type 0. The table is the local client's Text.pak CharAttrType.tbl (CharAttrTypeTable); a row is only applied when
//   it passes the 3.81 rules (CharacterManagementPackets: sum 75, base ≤ v ≤ base + points) — the TW16 per-stat limit
//   table byte_138B248 is NOT used (3.81 C_CreateChar rules stay). No table / no valid row → the buttons are disabled.
//   StatType_Title / StatType_Desc (strings 3607/3628 + 3×class + type) are commented out in the kor25 XML → not built.
// - Gauge_Group (sub_EE5430): bar k is drawn clipped to width = image width × StatGuage[class][gauge type][stat − 7] ÷ 100
//   (float, truncated; stat outside 7–20 or no row → 0 → not drawn). 5 bars: gauge types 0,1,4,3,5 ← STR,DEX,INT,CON,WIS;
//   6 bars: types 0..5 ← STR,DEX,DEX,CON,INT,WIS (child order = XML order). StatGuage.tbl is not in 0-客戶端 (FU9§15): the
//   local Text.pak is tried, then the reference client's (UiResources.Text root); SHA-256 logged ("create-char-ui
//   StatGuage.tbl"), compared with kor25. No table → every bar 0 → nothing drawn.
// - Stat scene Back_Image = surf 7156 + 2×class + sex (sub_EE4030, called from sub_EE6DD0; class 7: 7265/7266). Missing → not
//   drawn.
//
// R10-N (T398/#913; TW16 CCreateUI decompiled read-only, docs/reports/issue-913.md), 已驗證 in the binary unless marked:
// - The class scene's pictures are Png (XmlImage this[83] = png id, this[81] = png mode, this[82] = mode-2 level; FU13§5),
//   base png = sub_EE3F40(class, sex) = 1573 + 36×class + 18×sex (class 7: 1862 / 1880).
// - Loop (sub_EE3A00, the scene's per-frame update): on the class scene, while Back_Image's png mode is 0 and more than
//   200 ms passed since the last change: frame + 1 (18 → 0), Back_Image png = base + frame. The last-change time starts at 0,
//   so the first update after a reset changes at once.
// - Entering the class scene (sub_EE6DD0 scene 1 → sub_EE5F50(class, 0) / sub_EE6450(sex, 0)): Back_Image mode 0, png =
//   base, frame and time 0, Front_Image hidden. No fade.
// - Class change (sub_EE3410 → sub_EE5F50(…, 1)) or sex change (sub_EE3CF0 / sub_EE47B0 → sub_EE6450(…, 1)): the timer
//   sub_EE3BE0 is cancelled, count this[104] = 31, Back_Image mode 2 level 31 (its png is kept), Front_Image mode 2, png =
//   new base, level 0, shown; then sub_EE3BE0 every 19 ms (sub_A231C0 one-shot timer, re-armed from the callback): count − 1;
//   while > 0 Back level = count, Front level = 31 − count; at 0 Back mode 0 with Front's png, frame and time 0, Front hidden.
// - Mode 2 is drawn by sub_7D7740 with the level (XmlImage draw sub_AFB910 case 2). Level ÷ 31 as the opacity is 推測
//   (31 = the level the fade starts the old picture at; the end hands over to mode 0 = opaque).
// - The pngs are not in 0-客戶端 (PakViewer: only 1888–1891.png, class 7 only); kor25 / ly have all 288 (Sprite00–15).
//   They are read at run time through OptionPngSource (local first, then the reference client, SHA-256 logged); never
//   copied. A missing png is not drawn.
// - Not done: double click on a class button (sub_EE3410 515 → set the class without a fade and go to the stat scene).
// - Username_Edit takes typed characters (IUiTextInput, WM_CHAR after IME) up to LimitText characters; Backspace deletes.
//   Enter = OK on the stat scene / Next on the class scene; Esc = Prev.
// - OK: a local Validate error, a refused send or the server's refusal notice is shown in Message_Window (Ok/Cancel close
//   it). When the server confirms, the new character is in the list and the screen closes back to the select screen.

public enum CreateScene { Class, Stat }

/// <summary>Shared text-table helpers for the two CCreateUI tables (both read through ServerStringTable: count line + rows).</summary>
static class CharTableText {
    /// <summary>C atoi: leading blanks, optional sign, digits; anything else → 0.</summary>
    public static int Atoi(string? s){
        if(s==null)return 0;
        int i=0;while(i<s.Length&&s[i] is ' ' or '\t' or '\r' or '\n' or '\v' or '\f')i++;
        bool neg=false;if(i<s.Length&&s[i] is '+' or '-'){neg=s[i]=='-';i++;}
        long v=0;while(i<s.Length&&s[i] is >='0' and <='9'&&v<=int.MaxValue){v=v*10+(s[i]-'0');i++;}
        return (int)Math.Clamp(neg?-v:v,int.MinValue,int.MaxValue);
    }
    public static string[] Tokens(string line)=>line.Split([' ','\t','\r'],StringSplitOptions.RemoveEmptyEntries);
    public static bool Is(string sha,string known)=>sha.Equals(known,StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// R9-O (FU9§14.2): CharAttrType.tbl — the Attack/Defense/Mix presets. sub_EE4110 reads rows 2…count−1 (rows 0–1 are the
/// two comment lines), each non-empty row = 8 whitespace tokens through atoi as bytes: class, type, STR, DEX, CON, INT, WIS,
/// CHA. sub_EE3D30 takes the first row with that class and type. 0-客戶端 Text.pak record 2774 (PakViewer numbering), 584 B,
/// 21 rows (classes 0–6 × types 1–3), SHA-256 below; all 21 pass the 3.81 rules (checked offline, docs/reports/issue-859.md).
/// </summary>
public sealed class CharAttrTypeTable {
    public const string FileName="CharAttrType.tbl";
    public const int FirstRow=2;
    /// <summary>0-客戶端 Text.pak CharAttrType.tbl (decoded). Compared and logged only.</summary>
    public const string LocalSha256="64DA36ED6D718821A4B4A0B491ECFAAE72CF064B16894DE65C90B500D841575D";
    /// <summary>kor25 Text.pak CharAttrType.tbl (adds class 7 rows). Compared and logged only.</summary>
    public const string Kor25Sha256="17791EF622F9943F9791F3DFC2BD98B614980AAAF7B9DDFCFDFF6A5EDD3E3632";
    /// <summary>One row; Stats is in CharacterCreation order (STR, DEX, CON, WIS, CHA, INT).</summary>
    public readonly record struct Row(sbyte Class,byte Type,byte Str,byte Dex,byte Con,byte Int,byte Wis,byte Cha){
        public byte[] Stats=>[Str,Dex,Con,Wis,Cha,Int];
    }
    public IReadOnlyList<Row> Rows{get;}
    public string Sha256{get;}
    CharAttrTypeTable(IReadOnlyList<Row> rows,string sha){Rows=rows;Sha256=sha;}

    public static CharAttrTypeTable Parse(byte[] decoded){
        var table=ServerStringTable.Parse(decoded);var rows=new List<Row>();
        for(int i=FirstRow;i<table.Count;i++){
            var line=table[i];if(line.Length==0)continue;
            var t=CharTableText.Tokens(line);
            byte B(int k)=>unchecked((byte)CharTableText.Atoi(k<t.Length?t[k]:null));
            rows.Add(new(unchecked((sbyte)B(0)),B(1),B(2),B(3),B(4),B(5),B(6),B(7)));
        }
        return new(rows,table.Sha256);
    }
    public Row? Find(int cls,int type){foreach(var r in Rows)if(r.Class==cls&&r.Type==type)return r;return null;}
    /// <summary>3.81 rules (CharacterManagementPackets.Validate's stat part); null = the row may be applied.</summary>
    public static string? Check381(Row r){
        if(r.Class is <0 or >=7)return "3.81 沒有這個職業";
        var min=CharacterManagementPackets.BaseStats(r.Class);int points=CharacterManagementPackets.AvailablePoints(r.Class);var s=r.Stats;
        if(s.Sum(v=>(int)v)!=CreateCharPanel.TotalPoints)return "總和不是 75";
        for(int i=0;i<6;i++)if(s[i]<min[i]||s[i]>min[i]+points)return $"第 {i} 項超出 3.81 上下限";
        return null;
    }
    /// <summary>The preset for (class, type) when its row exists and passes the 3.81 rules; otherwise null.</summary>
    public byte[]? Preset(int cls,int type)=>Find(cls,type) is {} r&&Check381(r)==null?r.Stats:null;
}

/// <summary>
/// R9-O (FU9§14.2): StatGuage.tbl — Gauge_Group bar percentages. sub_EE4110 reads rows 3…count−1 (rows 0–2 are comments):
/// class, gauge type, then 14 values for stat 7…20, parsed one after another (sub_879EC0); extra tokens are ignored (kor25
/// row "5 5 6 0 …" has 15 values: the 15th is dropped), missing ones are 0. sub_EE3F80 takes the first row with the class
/// and type; stat outside 7–20 → 0. Not in 0-客戶端; kor25 / ly / t89 have it (FU9§15). Read at run time, never copied.
/// </summary>
public sealed class StatGaugeTable {
    public const string FileName="StatGuage.tbl";
    public const int FirstRow=3,Values=14,MinStat=7;
    /// <summary>kor25 Text.pak record 33375 (PakViewer numbering) = ly record 33799, decoded. Compared and logged only.</summary>
    public const string Kor25Sha256="2CF977EE45E6762271BAFE629A9894952701FA30019FC656FD0F493571D80C10";
    public readonly record struct Row(byte Class,byte Type,int[] Percent);
    public IReadOnlyList<Row> Rows{get;}
    public string Sha256{get;}
    StatGaugeTable(IReadOnlyList<Row> rows,string sha){Rows=rows;Sha256=sha;}

    public static StatGaugeTable Parse(byte[] decoded){
        var table=ServerStringTable.Parse(decoded);var rows=new List<Row>();
        for(int i=FirstRow;i<table.Count;i++){
            var line=table[i];if(line.Length==0)continue;
            var t=CharTableText.Tokens(line);
            int V(int k)=>CharTableText.Atoi(k<t.Length?t[k]:null);
            var pct=new int[Values];for(int k=0;k<Values;k++)pct[k]=V(2+k);
            rows.Add(new(unchecked((byte)V(0)),unchecked((byte)V(1)),pct));
        }
        return new(rows,table.Sha256);
    }
    public int Percent(int cls,int type,int stat){
        int k=stat-MinStat;if(k<0||k>=Values)return 0;
        foreach(var r in Rows)if(r.Class==cls&&r.Type==type)return r.Percent[k];
        return 0;
    }
    /// <summary>Gauge_Group bar order: (gauge type, CharacterCreation stat index) per child, for 5 and 6 bars (sub_EE5430).</summary>
    public static readonly IReadOnlyList<(int Type,int Stat)> FiveBars=[(0,0),(1,1),(4,5),(3,2),(5,3)];
    public static readonly IReadOnlyList<(int Type,int Stat)> SixBars=[(0,0),(1,1),(2,1),(3,2),(4,5),(5,3)];
    public static IReadOnlyList<(int Type,int Stat)> Bars(int count)=>count==5?FiveBars:count==6?SixBars:[];
    /// <summary>(int)((float)width × ((float)percent / 100)), kept inside 0…width.</summary>
    public static int Width(int imageWidth,int percent)=>Math.Clamp((int)(imageWidth*(percent/100f)),0,Math.Max(0,imageWidth));
}

public sealed class CreateCharPanel(UiResources? resources=null):IUiClosablePanel,IUiTextInput {
    [UiPanel(187)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,r=>new CreateCharPanel(r.Resources){TextResolver=RefClientTextSource.Resolver(r.Resources)});   // T327 (#765): CreateUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="CreateUI";
    public const string LayoutName="CreateUI",RootNode="CreateUI",ClassWindow="Class_Select_Scene",StatWindow="Stat_Setting_Scene",MessageWindow="Message_Window";
    public const string HostLayout="uistatus86";
    public const string ClassEvent="Class_ClassSelectScene",MaleEvent="Male_ClassSelectScene",FemaleEvent="Female_ClassSelectScene",PrevClassEvent="Prev_ClassSelectScene",
        NextEvent="Next_ClassSelectScene",UpEvent="Up_CustomizingWin",DownEvent="Down_CustomizingWin",ResetEvent="Reset_CustomizingWin",OkEvent="OK_StatSettingScene",
        PrevStatEvent="Prev_StatSettingScene",AutoEvent="Auto_StatSettingScene",CustomizingEvent="Customizing_StatSettingScene",MsgOkEvent="Ok_MessageWin",MsgCancelEvent="Cancel_MessageWin";
    public const int TotalPoints=75;

    /// <summary>Class button name → class index (CharacterManagementPackets.Classes order).</summary>
    public static readonly IReadOnlyDictionary<string,int> ClassButtons=new Dictionary<string,int>(StringComparer.Ordinal){
        ["Prince_Button"]=0,["Knight_Button"]=1,["Elf_Button"]=2,["Magician_Button"]=3,["Darkelf_Button"]=4,["Dragon_Button"]=5,["Illusionist_Button"]=6,
    };
    /// <summary>StatType_Group Control per class index.</summary>
    public static readonly IReadOnlyList<string> ClassControls=["Class_Prince","Class_Knight","Class_Elf","Class_Magician","Class_DarkElf","Class_Dragon","Class_Illusionist"];
    /// <summary>Stat Control name → CharacterCreation.Stats index (STR, DEX, CON, WIS, CHA, INT).</summary>
    public static readonly IReadOnlyDictionary<string,int> StatControls=new Dictionary<string,int>(StringComparer.Ordinal){
        ["Strength"]=0,["Dexterity"]=1,["Constitution"]=2,["Wisdom"]=3,["Charisma"]=4,["Intelligence"]=5,
    };
    /// <summary>Auto_StatSettingScene button name → stat type (child index + 1 in CreateUI.xml order).</summary>
    public static readonly IReadOnlyDictionary<string,int> StatTypeButtons=new Dictionary<string,int>(StringComparer.Ordinal){
        ["Attack_Button"]=1,["Defense_Button"]=2,["Mix_Button"]=3,
    };
    public const string GaugeGroup="Gauge_Group",BackImage="Back_Image";
    /// <summary>sub_EE4030: stat scene background surf.</summary>
    public static int BackgroundSurf(int cls,int sex)=>cls==7?(sex!=0?7266:7265):sex+2*cls+7156;
    /// <summary>R10-N sub_EE3F40: the class scene's base png (frame 0).</summary>
    public static int ClassScenePng(int cls,int sex)=>cls==7?(sex!=0?1880:1862):36*cls+18*sex+1573;
    public const string FrontImage="Front_Image";
    /// <summary>R10-N timing (sub_EE3A00 / sub_EE5F50 / sub_EE3BE0).</summary>
    public const int Frames=18,FrameMs=200,FadeSteps=31,FadeStepMs=19;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public CharUiWindow? ClassScene{get;private set;}
    public CharUiWindow? StatScene{get;private set;}
    public CharUiWindow? Message{get;private set;}
    public IReadOnlyList<string> Missing{get;private set;}=[];
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public Func<string,string,string?>? TextResolver{get;set;}
    public ICharSelectHost? Host{get;private set;}
    public CharSelectView? View{get;private set;}
    public CreateScene Scene{get;private set;}
    public int Class{get;private set;}
    public int Sex{get;private set;}
    public string Name{get;private set;}="";
    public byte[] Stats=>(byte[])draft.Clone();
    public bool Pending{get;private set;}
    public string? MessageText{get;private set;}
    public bool MessageShown=>MessageText!=null;
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    /// <summary>Attack/Defense/Mix presets (local CharAttrType.tbl); null = not readable → the three buttons are disabled.</summary>
    public CharAttrTypeTable? AttrTable{get;internal set;}
    /// <summary>Gauge_Group percentages (StatGuage.tbl, reference client); null → every bar is 0 (not drawn).</summary>
    public StatGaugeTable? GaugeTable{get;internal set;}
    /// <summary>Where GaugeTable came from ("本機" / "參考客戶端"); null when there is none.</summary>
    public string? GaugeSource{get;private set;}
    /// <summary>0 = none, 1 Attack, 2 Defense, 3 Mix (this+428).</summary>
    public int StatType{get;private set;}
    /// <summary>Gauge_Group's bars in XML (child) order.</summary>
    public IReadOnlyList<CharUiPiece> Gauges{get;private set;}=[];
    // ---- R10-N class scene pictures (XmlImage png id / mode / level of Back_Image and Front_Image) ----
    /// <summary>Back_Image png (this[91] [83]).</summary>
    public int BackPng{get;private set;}
    /// <summary>Back_Image png mode: 0 plain, 2 = drawn at BackLevel (crossfade).</summary>
    public int BackMode{get;private set;}
    public int BackLevel{get;private set;}
    /// <summary>Front_Image png (this[90] [83]), level and visibility (shown only during a crossfade).</summary>
    public int FrontPng{get;private set;}
    public int FrontLevel{get;private set;}
    public bool FrontShown{get;private set;}
    /// <summary>Crossfade count (this[104]): 31 → 0; 0 = no fade running.</summary>
    public int Fade{get;private set;}
    /// <summary>Loop frame (this[109], 0–17) and the time of the last change (this[108], ms; 0 after a reset).</summary>
    public int Frame{get;private set;}
    long lastFrame,fadeDue;
    /// <summary>Png lookup (product: OptionPngSource, local then the reference client); null → nothing drawn.</summary>
    internal Func<int,UiSurface?>? PngLookup{get;set;}
    /// <summary>Millisecond clock (sub_A228B0); tests drive it.</summary>
    internal Func<long> Clock{get;set;}=()=>Environment.TickCount64;
    readonly Dictionary<int,UiSurface?> pngs=[];readonly HashSet<int> pngMissing=[];
    bool open,waiting;int generation;string? sentName;byte[] draft=new byte[6];int nameLimit;
    Func<int,UiSurface?>? lookup;readonly Dictionary<int,UiSurface?> dynamic=[];readonly HashSet<int> dynamicMissing=[];readonly HashSet<string> logged=new(StringComparer.Ordinal);
    CharUiPiece? pressed,hover;bool pressedInside;readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

    public bool Visible=>open&&Available&&View is {Shown:true};
    CharUiWindow? Current=>Scene==CreateScene.Class?ClassScene:StatScene;
    public Rectangle Bounds=>Visible&&Current!=null?Current.Bounds:Rectangle.Empty;

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"create-char-ui 無法載入 {LayoutName}，建角畫面停用（WinForms 建角對話框照舊）：{ex.Message}");return;}
        LoadTables(resources.Root,resources.Text?.Root);
        try{Load(layout,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"create-char-ui {layout.FileName} 版面不符，建角畫面停用：{ex.Message}");return;}
        PngLookup=new OptionPngSource(resources.Sprites,resources.Xml.Reference).Get;   // R10-N class scene pngs
        if(Missing.Count>0)UiResourceLog.Write($"create-char-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"create-char-ui {Unavailable}");
    }

    /// <summary>R9-O: CharAttrType.tbl from the main client; StatGuage.tbl from the main client, else the reference client.
    /// Read-only, decoded in memory, SHA-256 logged; a table that cannot be read stays null (logged).</summary>
    void LoadTables(string mainRoot,string? refRoot){
        bool other=refRoot!=null&&!string.Equals(Path.GetFullPath(refRoot),Path.GetFullPath(mainRoot),StringComparison.OrdinalIgnoreCase);
        LoadTables(OpenArchive(mainRoot,"本機"),mainRoot,other?()=>OpenArchive(refRoot!,"參考客戶端"):null,refRoot);
    }
    /// <summary>Test seam: archives instead of roots (reference = null when there is no other reference client).</summary>
    internal void LoadTables(TextArchive? main,string mainRoot,Func<TextArchive?>? reference,string? refRoot){
        AttrTable=ReadTable(main,mainRoot,"本機",CharAttrTypeTable.FileName,CharAttrTypeTable.Parse,t=>t.Sha256,t=>CharTableText.Is(t.Sha256,CharAttrTypeTable.LocalSha256)?"與 0-客戶端 相同":CharTableText.Is(t.Sha256,CharAttrTypeTable.Kor25Sha256)?"與 kor25 相同":"不是已知版本");
        if(AttrTable!=null){
            var bad=AttrTable.Rows.Where(r=>r.Class is >=0 and <7&&CharAttrTypeTable.Check381(r)!=null).Select(r=>$"{r.Class}/{r.Type}").ToArray();
            UiResourceLog.Write($"create-char-ui {CharAttrTypeTable.FileName} {AttrTable.Rows.Count} 列；不合 3.81 規則、不套用：{(bad.Length==0?"無":string.Join("、",bad))}");
        }
        string Known(StatGaugeTable t)=>CharTableText.Is(t.Sha256,StatGaugeTable.Kor25Sha256)?"與 kor25 相同":"不是已知的 kor25 版本";
        GaugeTable=ReadTable(main,mainRoot,"本機",StatGaugeTable.FileName,StatGaugeTable.Parse,t=>t.Sha256,Known);GaugeSource=GaugeTable==null?null:"本機";
        if(GaugeTable==null&&reference!=null){GaugeTable=ReadTable(reference(),refRoot??"","參考客戶端",StatGaugeTable.FileName,StatGaugeTable.Parse,t=>t.Sha256,Known);GaugeSource=GaugeTable==null?null:"參考客戶端";}
        if(GaugeTable==null)UiResourceLog.Write($"create-char-ui {StatGaugeTable.FileName} 讀不到，能力量表不畫");
    }
    static TextArchive? OpenArchive(string root,string label){
        try{return TextArchive.Open(root);}
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException){
            UiResourceLog.Write($"create-char-ui {label} {root} 的 Text.idx／Text.pak 無法讀取：{ex.Message}");return null;
        }
    }
    static T? ReadTable<T>(TextArchive? archive,string root,string label,string file,Func<byte[],T> parse,Func<T,string> sha,Func<T,string> known) where T:class{
        if(archive==null)return null;
        try{
            if(archive.Single(file) is not {} entry){UiResourceLog.Write($"create-char-ui {file}：{label} {root}\\Text.pak 沒有");return null;}
            var table=parse(archive.Read(entry));
            UiResourceLog.Write($"create-char-ui {file} -> {label} {root}\\Text.pak:#{entry.Record} plain sha256 {sha(table)}（{entry.Size} B，{known(table)}）");
            return table;
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or System.Text.DecoderFallbackException){
            UiResourceLog.Write($"create-char-ui {file}（{label} {root}）無法讀取：{ex.Message}");return null;
        }
    }

    void Clear(){
        open=false;Host=null;View=null;LayoutFile=null;ClassScene=null;StatScene=null;Message=null;Missing=[];Skipped=[];Unavailable="not loaded";nameLimit=0;
        Gauges=[];lookup=null;dynamic.Clear();dynamicMissing.Clear();pngs.Clear();pngMissing.Clear();
        ResetState(0);
    }
    void ResetState(int cls){
        Scene=CreateScene.Class;Class=cls;Sex=0;Name="";draft=CharacterManagementPackets.BaseStats(cls);StatType=0;Pending=false;MessageText=null;waiting=false;sentName=null;
        pressed=null;hover=null;pressedInside=false;
        SetClassPicture();
    }

    // ---- R10-N: class scene pictures --------------------------------------------------------------------------------------

    /// <summary>sub_EE5F50 / sub_EE6450 with a3 = 0 (entering the class scene): Back_Image plain at frame 0, no fade.</summary>
    void SetClassPicture(){
        BackMode=0;BackLevel=0;BackPng=ClassScenePng(Class,Sex);Frame=0;lastFrame=0;
        Fade=0;FrontShown=false;FrontLevel=0;
    }
    /// <summary>sub_EE5F50 / sub_EE6450 with a3 = 1 (class or sex changed): start the 31-step crossfade to the new base png.</summary>
    void StartFade(){
        Fade=FadeSteps;BackMode=2;BackLevel=FadeSteps;   // Back keeps its current png
        FrontPng=ClassScenePng(Class,Sex);FrontLevel=0;FrontShown=true;
        fadeDue=Clock()+FadeStepMs;
    }
    /// <summary>One update at time now (ms): the 200 ms loop (sub_EE3A00: class scene, Back mode 0), then the fade timer
    /// (sub_EE3BE0, one step per due time, re-armed from now). The order of the two inside one frame is 推測.</summary>
    internal void Advance(long now){
        if(!open)return;
        if(Scene==CreateScene.Class&&BackMode==0&&now-lastFrame>FrameMs){
            lastFrame=now;
            if(++Frame>=Frames)Frame=0;
            BackPng=ClassScenePng(Class,Sex)+Frame;
        }
        if(Fade>0&&now>=fadeDue){
            if(--Fade<=0){BackMode=0;BackLevel=0;BackPng=FrontPng;Frame=0;lastFrame=0;FrontShown=false;Fade=0;}
            else{BackLevel=Fade;FrontLevel=FadeSteps-Fade;fadeDue=now+FadeStepMs;}
        }
    }
    UiSurface? Png(int id){
        if(id<=0||PngLookup==null)return null;
        if(!pngs.TryGetValue(id,out var s)){pngs[id]=s=PngLookup(id);if(s==null&&pngMissing.Add(id))UiResourceLog.Write($"create-char-ui 缺圖不畫：png {id}");}
        return s;
    }
    /// <summary>Class scene Back_Image / Front_Image (top level, no id in the XML).</summary>
    bool ClassPicture(CharUiWindow w,CharUiPiece p,string name)=>ReferenceEquals(w,ClassScene)&&p.Kind==CharUiKind.Image&&p.Name==name&&p.Parent==null&&p.Owner==null&&p.Image==null;
    static void DrawLevel(UiCanvas canvas,UiSurface? s,int x,int y,int level){
        if(s==null||level<=0)return;
        if(level>=FadeSteps){canvas.DrawSurface(s,x,y);return;}
        using var attrs=new ImageAttributes();
        attrs.SetColorMatrix(new ColorMatrix{Matrix33=level/(float)FadeSteps});
        canvas.Graphics.DrawImage(s.Bitmap,new Rectangle(x,y,s.Width,s.Height),0,0,s.Width,s.Height,GraphicsUnit.Pixel,attrs);
    }

    static string? SkipReason(UiNode n)=>n.Attr("Name") switch{
        "Warrior_Button"=>"3.81 沒有戰士職業",
        "Customizing_Win"=>"能力加成表，引擎沒有數值，不建（待做）",
        _=>n.Name=="Tooltip"?"說明字串不建":null,
    };

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var top=layout.Root;
        if(top.Name!=RootNode)throw new UiLayoutException(layout.FileName,top.Path,$"根元素不是 {RootNode}");
        // R10-N: the class scene's Front_Image / Back_Image have no id; their pngs are set at run time (sub_EE6DD0).
        var b=new CharUiBuilder(layout.FileName,surf,SkipReason,n=>n.Attr("Name") is FrontImage or BackImage);
        CharUiWindow? cls=null,stat=null,message=null;
        foreach(var w in top.Children){
            string? name=w.Name=="Window"?w.Attr("Name"):null;
            if(name is not (ClassWindow or StatWindow or MessageWindow)){b.Skipped.Add(w.Path);continue;}
            if(name==ClassWindow&&cls!=null||name==StatWindow&&stat!=null||name==MessageWindow&&message!=null)throw new UiLayoutException(layout.FileName,w.Path,$"Window {name} 重複");
            var built=b.Window(w);
            if(name==ClassWindow)cls=built;else if(name==StatWindow)stat=built;else message=built;
        }
        if(cls==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {ClassWindow}");
        if(stat==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {StatWindow}");
        if(message==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {MessageWindow}（無法顯示錯誤）");
        if(!ClassButtons.Keys.All(k=>cls.Pieces.Any(p=>p.Name==k&&p.MouseEvent==ClassEvent)))throw new UiLayoutException(layout.FileName,top.Path,"七個職業按鈕不齊");
        if(!StatControls.Keys.All(k=>stat.Pieces.Any(p=>p.Owner==k&&p.Name=="Point")))throw new UiLayoutException(layout.FileName,top.Path,"六項能力的 Point 欄位不齊");
        var edit=stat.Pieces.FirstOrDefault(p=>p.Kind==CharUiKind.Edit)??throw new UiLayoutException(layout.FileName,top.Path,$"{StatWindow} 沒有名稱輸入欄（Edit）");
        if(!stat.Pieces.Any(p=>p.MouseEvent==OkEvent))throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {OkEvent} 按鈕");
        LayoutFile=layout.FileName;ClassScene=cls;StatScene=stat;Message=message;Missing=b.Missing;Skipped=b.Skipped;nameLimit=edit.Limit>0?edit.Limit:12;
        lookup=surf;
        // Pieces are in paint order (reverse XML order): the direct images of Gauge_Group reversed = child order.
        Gauges=stat.Pieces.Where(p=>p.Owner==GaugeGroup&&p.Kind==CharUiKind.Image&&p.Parent==null).Reverse().ToArray();
        Unavailable=cls.Size.IsEmpty?$"{layout.FileName}: {ClassWindow} 沒有大小":stat.Size.IsEmpty?$"{layout.FileName}: {StatWindow} 沒有大小"
            :message.Size.IsEmpty?$"{layout.FileName}: {MessageWindow} 底圖都缺，無法定出訊息框範圍":null;
    }

    // ---- state ------------------------------------------------------------------------------------------------------------

    /// <summary>Opens on the class scene for the host's list (Create_Button of the select screen). false = unusable / no free slot.</summary>
    public bool Begin(ICharSelectHost host){
        if(!Available)return false;
        var v=host.Snapshot();
        if(!v.Shown||!v.CanCreate)return false;
        Host=host;View=v;generation=v.Generation;ResetState(0);open=true;
        return true;
    }
    public void Close(){open=false;MessageText=null;pressed=null;hover=null;pressedInside=false;Fade=0;FrontShown=false;}   // sub_EE3AE0: fade timer cancelled

    public void Update(IUiGameState state){Sync();Advance(Clock());}

    public void Sync(){
        if(!open||Host==null)return;
        var v=Host.Snapshot();
        View=v;
        if(v.Generation!=generation||!v.Shown){Close();return;}
        if(waiting&&!Pending&&v.State==LoginState.CharacterList){
            waiting=false;
            if(sentName!=null&&v.Characters.Any(c=>c.Name==sentName)){Close();return;}
            MessageText=v.Notice.Length>0?v.Notice:"伺服器沒有建立角色。";
        }
    }

    public int Remaining=>TotalPoints-draft.Sum(s=>(int)s);
    int Points=>CharacterManagementPackets.AvailablePoints(Class);
    byte[] Base=>CharacterManagementPackets.BaseStats(Class);
    public bool CanUp(int i)=>Remaining>0&&draft[i]<Base[i]+Points;
    public bool CanDown(int i)=>draft[i]>Base[i];
    public CharacterCreation Request=>new(Name,(byte)Class,(byte)Sex,draft[0],draft[1],draft[2],draft[3],draft[4],draft[5]);
    bool Ready=>View is {State:LoginState.CharacterList}&&!Pending&&!waiting;

    // ---- R9-O: presets, gauges, background --------------------------------------------------------------------------------

    /// <summary>Stat type of an Auto_StatSettingScene button (0 for anything else).</summary>
    public static int TypeOf(CharUiPiece p)=>p.MouseEvent==AutoEvent&&p.Name!=null&&StatTypeButtons.TryGetValue(p.Name,out int t)?t:0;
    /// <summary>sub_EE6760: the same type again resets; otherwise the valid preset replaces the six stats (no valid preset → no change).</summary>
    public void ApplyType(int type){
        if(type==StatType){ResetStats();return;}
        if(AttrTable?.Preset(Class,type) is not {} preset){
            if(logged.Add($"preset {Class}/{type}"))UiResourceLog.Write($"create-char-ui {CharAttrTypeTable.FileName} 沒有可用（合 3.81 規則）的職業 {Class} 型態 {type}，不套用");
            return;
        }
        StatType=type;draft=preset;
    }
    /// <summary>sub_EE5D60: type 0, class base stats.</summary>
    void ResetStats(){StatType=0;draft=CharacterManagementPackets.BaseStats(Class);}

    /// <summary>Bar k of Gauge_Group: its width in pixels (0 = not drawn).</summary>
    public int GaugeWidth(CharUiPiece p){
        int k=-1;for(int i=0;i<Gauges.Count;i++)if(ReferenceEquals(Gauges[i],p)){k=i;break;}
        var bars=StatGaugeTable.Bars(Gauges.Count);
        if(k<0||k>=bars.Count||GaugeTable==null||p.Surface==null)return 0;
        var (type,stat)=bars[k];
        return StatGaugeTable.Width(p.Surface.Width,GaugeTable.Percent(Class,type,draft[stat]));
    }

    /// <summary>A surf chosen at run time (background); missing → null, logged once.</summary>
    UiSurface? Dynamic(int id){
        if(id<0||lookup==null)return null;
        if(!dynamic.TryGetValue(id,out var s)){dynamic[id]=s=lookup(id);if(s==null&&dynamicMissing.Add(id))UiResourceLog.Write($"create-char-ui 缺圖不畫：surf {id}");}
        return s;
    }
    /// <summary>The surface a piece is painted with: the stat scene's Back_Image follows class and sex.</summary>
    public UiSurface? SurfaceOf(CharUiWindow w,CharUiPiece p)=>ReferenceEquals(w,StatScene)&&p.Kind==CharUiKind.Image&&p.Name==BackImage&&p.Parent==null&&p.Owner==null
        ?Dynamic(BackgroundSurf(Class,Sex)):p.Surface;

    public bool ShownPiece(CharUiPiece p){
        if(p.Parent!=null&&!ShownPiece(p.Parent))return false;
        if(p.Owner==GaugeGroup&&p.Kind==CharUiKind.Image&&p.Parent==null)return GaugeWidth(p)>0;
        if(ClassScene!=null&&ClassPicture(ClassScene,p,FrontImage))return FrontShown;
        if(p.Owner is {} o&&o.StartsWith("Class_",StringComparison.Ordinal)&&o!="Class_Buttons")return o==ClassControls[Class];
        return p.Name switch{"Male_Back"=>Sex==0,"Female_Back"=>Sex==1,_=>true};
    }

    public bool Enabled(CharUiPiece p){
        if(p.MouseEvent is MsgOkEvent or MsgCancelEvent)return MessageShown;
        if(MessageShown)return false;
        if(p.MouseEvent is PrevClassEvent)return !Pending;
        if(!Ready)return false;
        int stat=p.Owner!=null&&StatControls.TryGetValue(p.Owner,out var i)?i:-1;
        return p.MouseEvent switch{
            ClassEvent=>p.Name!=null&&ClassButtons.ContainsKey(p.Name),
            MaleEvent or FemaleEvent or NextEvent or PrevStatEvent or ResetEvent=>true,
            UpEvent=>stat>=0&&CanUp(stat),
            DownEvent=>stat>=0&&CanDown(stat),
            OkEvent=>View!.CanCreate,
            // R9-O: a type button works while its row passes the 3.81 rules (the selected one too: pressing it again resets).
            AutoEvent=>TypeOf(p) is >0 and var type&&(type==StatType||AttrTable?.Preset(Class,type)!=null),
            _=>false,   // Customizing_StatSettingScene: no bonus values in the engine
        };
    }

    public string? LabelText(CharUiPiece p){
        if(p.Kind==CharUiKind.MultiLabel)return p.Name=="Message_Label"?MessageText:null;
        if(p.Kind!=CharUiKind.Label)return null;
        string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
        if(p.Name=="Point"&&p.Owner!=null&&StatControls.TryGetValue(p.Owner,out int i))return N(draft[i]);
        if(p.Name=="Remain_Point")return N(Remaining);
        return p.TextKind==null?null:TextResolver?.Invoke(p.TextKind,p.TextValue!);
    }

    // ---- painting ---------------------------------------------------------------------------------------------------------

    public void Paint(UiCanvas canvas){
        if(!Visible||Current is not {} w)return;
        PaintWindow(canvas,w);
        if(MessageShown&&Message!=null)PaintWindow(canvas,Message);
    }
    void PaintWindow(UiCanvas canvas,CharUiWindow w){
        canvas.DrawSurface(w.Base,w.Origin.X,w.Origin.Y);
        foreach(var p in w.Pieces)if(ShownPiece(p))PaintPiece(canvas,w,p);
    }
    void PaintPiece(UiCanvas canvas,CharUiWindow w,CharUiPiece p){
        int x=w.Origin.X+p.Offset.X,y=w.Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case CharUiKind.Image when p.Owner==GaugeGroup&&p.Parent==null&&p.Surface is {} bar:
                canvas.DrawSurface(bar,new Rectangle(0,0,GaugeWidth(p),bar.Height),x,y);break;
            // R10-N: class scene pngs; mode 2 at its level (÷ 31, 推測), Back mode 0 plain.
            case CharUiKind.Image when ClassPicture(w,p,BackImage):DrawLevel(canvas,Png(BackPng),x,y,BackMode==2?BackLevel:FadeSteps);break;
            case CharUiKind.Image when ClassPicture(w,p,FrontImage):DrawLevel(canvas,Png(FrontPng),x,y,FrontLevel);break;
            case CharUiKind.Image:canvas.DrawSurface(SurfaceOf(w,p),x,y);break;
            case CharUiKind.Board:CharUiPaint.Board(canvas,p,x,y);break;
            case CharUiKind.Label:CharUiPaint.Label(canvas,p,x,y,LabelText(p));break;
            case CharUiKind.MultiLabel:CharUiPaint.MultiLabel(canvas,p,x,y,LabelText(p));break;
            case CharUiKind.Edit:{
                var size=UiText.Measure(canvas.Graphics,Name.Length>0?Name:"國");
                int ty=y+Math.Max(0,(p.DeclaredSize.Height-size.Height)/2);
                if(Name.Length>0)canvas.DrawText(Name,x+2,ty,Color.White);
                // Caret after the text while the name can be typed (stat scene, no message).
                if(!MessageShown&&!Pending){
                    int cx=x+2+(Name.Length>0?UiText.Measure(canvas.Graphics,Name).Width:0);
                    using var pen=new Pen(Color.White);canvas.Graphics.DrawLine(pen,cx,ty,cx,ty+size.Height-1);
                }
                break;
            }
            case CharUiKind.Button:{
                bool enabled=Enabled(p);
                bool selected=p.MouseEvent==ClassEvent&&p.Name!=null&&ClassButtons.TryGetValue(p.Name,out int k)&&k==Class
                    ||StatType>0&&TypeOf(p)==StatType;   // sub_EE6760: the selected type's button shows its PushSurf in every state
                var s=ReferenceEquals(p,pressed)&&pressedInside&&enabled&&p.Push!=null?p.Push:selected&&p.Push!=null?p.Push:ReferenceEquals(p,hover)&&pressed==null&&enabled&&p.Highlight!=null?p.Highlight:p.Surface;
                canvas.DrawSurface(s,x,y);break;
            }
        }
    }

    // ---- input ------------------------------------------------------------------------------------------------------------

    CharUiWindow? InputWindow=>MessageShown?Message:Current;
    public CharUiPiece? ButtonAt(Point location)=>Visible&&InputWindow is {} w?CharUiPaint.ButtonAt(w,location,ShownPiece):null;

    /// <summary>Full screen and modal: every mouse event goes here while the screen is shown.</summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.Kind==UiMouseKind.Move&&pressed==null){hover=ButtonAt(e.Location);return true;}
        if(e.Kind==UiMouseKind.Wheel||e.Button!=MouseButtons.Left)return true;
        var w=InputWindow!;
        switch(e.Kind){
            case UiMouseKind.Down:pressed=ButtonAt(e.Location);pressedInside=pressed!=null;return true;
            case UiMouseKind.Move:pressedInside=pressed!=null&&CharUiPaint.At(w,pressed).Contains(e.Location);return true;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;pressedInside=false;
                if(was!=null&&CharUiPaint.At(w,was).Contains(e.Location)&&Enabled(was))Fire(was);
                return true;
            }
        }
        return true;
    }

    bool Typing=>Visible&&Scene==CreateScene.Stat&&!MessageShown&&Ready;

    /// <summary>Modal. Printable keys on the stat scene are left for WM_CHAR → Char (like the chat input).</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(!e.Down)return true;
        switch(e.KeyCode){
            case Keys.Escape when !e.Repeat:
                if(MessageShown)MessageText=null;
                else FireEvent(Scene==CreateScene.Stat?PrevStatEvent:PrevClassEvent);
                return true;
            case Keys.Enter when !e.Repeat:
                FireEvent(MessageShown?MsgOkEvent:Scene==CreateScene.Stat?OkEvent:NextEvent);
                return true;
            case Keys.Back:
                if(Typing&&Name.Length>0)Name=Name[..^1];
                return true;
        }
        return !(Typing&&(e.Modifiers&(Keys.Control|Keys.Alt))==0);
    }

    /// <summary>Typed character for Username_Edit (stat scene only); control characters are ignored.</summary>
    public bool Char(char c){
        if(!Typing)return false;
        if(char.IsControl(c))return true;
        if(Name.Length<nameLimit)Name+=c;
        return true;
    }

    void FireEvent(string ev){
        var w=InputWindow;
        if(w?.Pieces.FirstOrDefault(p=>p.MouseEvent==ev&&ShownPiece(p)) is {} p&&Enabled(p))Fire(p);
    }

    void Fire(CharUiPiece p){
        int stat=p.Owner!=null&&StatControls.TryGetValue(p.Owner,out var i)?i:-1;
        switch(p.MouseEvent){
            case ClassEvent when p.Name!=null&&ClassButtons.TryGetValue(p.Name,out int cls):
                if(cls!=Class){Class=cls;ResetStats();StartFade();}   // sub_EE3410 → sub_EE5F50(cls, 1)
                return;
            case MaleEvent:if(Sex!=0){Sex=0;StartFade();}return;     // sub_EE6450(0, 1)
            case FemaleEvent:if(Sex!=1){Sex=1;StartFade();}return;   // sub_EE6450(1, 1)
            case NextEvent:EnterStatScene();return;
            case PrevStatEvent:Scene=CreateScene.Class;SetClassPicture();return;   // sub_EE6DD0 scene 1
            case PrevClassEvent:Close();return;
            case UpEvent when stat>=0:draft[stat]++;return;
            case DownEvent when stat>=0:draft[stat]--;return;
            case ResetEvent:ResetStats();return;
            case AutoEvent when TypeOf(p) is >0 and var type:ApplyType(type);return;
            case OkEvent:Submit();return;
            case MsgOkEvent or MsgCancelEvent:MessageText=null;return;
        }
        string id=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(id))UiResourceLog.Write($"create-char-ui {id} 尚未接上");
    }

    /// <summary>sub_EE6DD0 scene 2: type 0, then type 1 (Attack) is applied (only with a readable table).</summary>
    void EnterStatScene(){
        Scene=CreateScene.Stat;StatType=0;
        if(AttrTable!=null)ApplyType(1);
    }

    void Submit(){
        if(View is not {} v)return;
        var request=Request;
        if(CharacterManagementPackets.Validate(request,v.Characters.Count,v.Slots) is {} error){MessageText=error;return;}
        _=Run(v,request);
    }

    /// <summary>One create through the host; a refusal is shown in Message_Window, a sent request waits for the server.</summary>
    internal async Task Run(CharSelectView stamp,CharacterCreation request){
        if(Pending||Host is not {} host)return;
        Pending=true;waiting=true;sentName=CharacterManagementPackets.NormalizeName(request.Name);
        LoginAction r;
        try{r=await host.Create(stamp,request);}
        catch(Exception ex){r=new(false,"角色操作未完成，請查看連線狀態。");UiResourceLog.Write($"create-char-ui 送出失敗：{ex.GetType().Name}: {ex.Message}");}
        Pending=false;
        if(!r.Accepted){waiting=false;sentName=null;}
        Sync();
        if(!r.Accepted&&open)MessageText=r.Message;
    }
}
