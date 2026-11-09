using System.Globalization;
using System.Text.RegularExpressions;
namespace P06.NativeUi;

// W2-I (Issue #562; docs/reference/windows-2.md §2.2): SPELL_WINDOW of uimagicitem.xml. Shared reader/base: ItemWindowPanel.cs.

/// <summary>Level-button groups of SPELL_WINDOW, by surfName prefix (PRINCE / plain / ELF / DARKELF).</summary>
public enum LegacySpellClass { Prince, General, Elf, DarkElf }

/// <summary>Data for the spell window: learned spells (Magic.cs) and the class that picks the level-button group.</summary>
public interface IUiSpellWindowState {
    /// <summary>null when the character's class is not known; then no level buttons are drawn.</summary>
    LegacySpellClass? Class{get;}
    /// <summary>null before the server skill list arrived.</summary>
    IReadOnlyList<LearnedMagic>? Learned{get;}
    /// <summary>The game's selected spell (WorldSession.SelectedMagicId), shared with the WinForms magic list.</summary>
    int? SelectedId=>null;
    /// <summary>Selects a spell in the game model (local state only, no packet: WorldSession.SelectMagic). false = not taken.</summary>
    bool Select(int? id)=>false;
}
public sealed record UiSpellWindowData(LegacySpellClass? Class,IReadOnlyList<LearnedMagic>? Learned,int? SelectedId=null,Func<int?,bool>? Selector=null):IUiSpellWindowState {
    /// <summary>Adapter over WorldSession (Magic.cs). The class type comes from the character list (not kept by WorldSession yet).</summary>
    public static UiSpellWindowData From(WorldSession world,int? classType)=>new(classType is int c?SpellWindowPanel.ClassOf(c):null,world.MagicListReceived?world.LearnedMagic:null,world.SelectedMagicId,world.SelectMagic);
    public bool Select(int? id)=>Selector?.Invoke(id)??false;
}
public sealed class UiSpellWindowGameState(IUiGameState inner,IUiSpellWindowState spells):IUiGameState,IUiSpellWindowState,ILegacyWindowStateDecorator {
    public IUiGameState Inner=>inner;
    public bool InWorld=>inner.InWorld;public string? CharacterName=>inner.CharacterName;public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;public long? MaxHp=>inner.MaxHp;public long? Mp=>inner.Mp;public long? MaxMp=>inner.MaxMp;public long Now=>inner.Now;
    public LegacySpellClass? Class=>spells.Class;
    public IReadOnlyList<LearnedMagic>? Learned=>spells.Learned;
    public int? SelectedId=>spells.SelectedId;
    public bool Select(int? id)=>spells.Select(id);
}

/// <summary>
/// SPELL_WINDOW (windows-2.md §2.2): base 2056 (6, 8), frame 2057, close 2037 (179, 15), scrollbar and arrows as in the item
/// window, level buttons at (12, 37): prince 2058–2064 (7), general 2065–2074 (10), elf 2075–2085 (11), dark elf 2086– (as many
/// as the XML lists; the local file has 5). Only the first element of each group has a position.
/// </summary>
public sealed class SpellWindowPanel:LegacyWindowPanel {
    [UiPanel(175)] internal static UiPanelRegistration Registration=>new(WindowId,Layout,_=>new SpellWindowPanel());   // R3-A (#678): menu 3 / Ctrl+S
    public const string WindowId="SPELL_WINDOW",Layout="uimagicitem";
    static readonly Regex LevelName=new(@"\ASURF_RENEWAL_SPELL_(?:(PRINCE|ELF|DARKELF)_)?LEVEL(\d{2})\z",RegexOptions.CultureInvariant);
    public override string Id=>WindowId;
    public override string LayoutName=>Layout;
    readonly Dictionary<LegacySpellClass,Part[]> groups=[];
    IReadOnlyList<LearnedMagic>? learned;

    /// <summary>
    /// 3.81 class type → level-button group: 0 royal → PRINCE, 2 elf → ELF, 4 dark elf → DARKELF, 3 wizard → plain LEVEL.
    /// Knight (1) → plain LEVEL is 推測 (no knight group exists in the XML). Other values → null.
    /// </summary>
    public static LegacySpellClass? ClassOf(int classType)=>classType switch{0=>LegacySpellClass.Prince,1 or 3=>LegacySpellClass.General,2=>LegacySpellClass.Elf,4=>LegacySpellClass.DarkElf,_=>null};

    IUiSpellWindowState? source;int? localSelection;
    public LegacySpellClass? Class{get;private set;}
    /// <summary>
    /// Selected level button (0-based). R4-B: a click on the level strip picks the button by equal segments of the drawn
    /// image's width (推測, like the item tabs), and the list switches to that level's page.
    /// </summary>
    public int SelectedLevel{get;private set;}
    public IReadOnlyList<LearnedMagic>? Learned=>learned;
    public int LevelCount(LegacySpellClass c)=>groups.TryGetValue(c,out var g)?g.Length:0;
    /// <summary>Rectangle of the drawn level image (empty without a class or with a missing surf).</summary>
    public Rectangle LevelRect=>Class is {} c&&groups.TryGetValue(c,out var g)&&g.Length>0?RectOf(g[SelectedLevel]):Rectangle.Empty;

    public void SetClass(LegacySpellClass? c){if(c!=Class){Class=c;SelectedLevel=0;ResetScroll();}}
    public void SetLevel(int level){
        int n=Class is {} c?LevelCount(c):0;
        if(level<0||level>=n)throw new ArgumentOutOfRangeException(nameof(level));
        if(level!=SelectedLevel){SelectedLevel=level;ResetScroll();}
    }

    /// <summary>
    /// Spell page of a skill id: the C164 / S_AddSkill mask row, (id − 1) / 8 — row k holds ids 8k+1 … 8k+8 (Magic.cs).
    /// Level button k shows page k (general magic level k+1). This is 已驗證 only for the meaning of the mask rows; that the
    /// class groups (王族 7, 妖精 11, 黑妖 5 buttons) also start at row 0 is 推測 — their own rows (97–176) are on no button
    /// until the original mapping is traced (待驗證), and stay reachable through the WinForms magic tab.
    /// </summary>
    public static int PageOf(int skillId)=>skillId<=0?-1:(skillId-1)/8;
    /// <summary>Learned spells on the selected level page (empty without a class: no level button is drawn).</summary>
    public IReadOnlyList<LearnedMagic> PageSpells=>learned==null||Class==null?[]:learned.Where(m=>PageOf(m.Id)==SelectedLevel).ToArray();
    /// <summary>Text rows (R4-B, 推測 geometry): 16-pixel rows between the level strip and the lower arrow, base edge to scrollbar.</summary>
    public LegacyCellGrid Rows{get;private set;}=LegacyCellGrid.Empty;
    public const int RowPitch=16;
    protected override int ScrollCount=>Rows.ScrollPositions(PageSpells.Count);
    public IReadOnlyList<LearnedMagic> VisibleSpells=>PageSpells.Skip(ScrollOffset).Take(Rows.PageSize).ToArray();
    public Rectangle RowRect(int row)=>Logical(Rows.CellRect(row));
    public int? SelectedId=>source?.SelectedId??localSelection;
    public LearnedMagic? SpellAt(Point logical)=>Rows.SlotAt(Local(logical)) is int r&&r<VisibleSpells.Count?VisibleSpells[r]:null;
    public void SelectSpell(int? id){
        if(id!=null&&learned?.Any(m=>m.Id==id)!=true)return;
        if(source!=null&&source.Select(id))return;
        localSelection=id;
    }

    protected override void Build(LegacyWindowLayout w,Func<int,UiSurface?> surfs){
        AddChrome(w,surfs,"SURF_RENEWAL_WIN_SPELL_BASE","SURF_RENEWAL_WIN_SPELL_FRAME","SURF_RENEWAL_BTN_PET_ITEM_X","SURF_RENEWAL_WIN_SCROLLBAR","SURF_RENEWAL_WIN_INC_BUT_OFF","SURF_RENEWAL_WIN_DEC_BUT_OFF");
        groups.Clear();
        var found=new Dictionary<LegacySpellClass,List<LegacyElement>>();
        foreach(var e in w.Elements){
            var m=LevelName.Match(e.SurfName);if(!m.Success)continue;
            var c=m.Groups[1].Value switch{"PRINCE"=>LegacySpellClass.Prince,"ELF"=>LegacySpellClass.Elf,"DARKELF"=>LegacySpellClass.DarkElf,_=>LegacySpellClass.General};
            var list=found.TryGetValue(c,out var l)?l:found[c]=[];
            int number=int.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture);
            if(number!=list.Count+1)throw new UiLayoutException(w.FileName,e.Path,$"{e.SurfName}：等級編號不連續（應為 {list.Count+1:00}）");
            list.Add(e);
        }
        foreach(var c in Enum.GetValues<LegacySpellClass>()){
            if(!found.TryGetValue(c,out var list))throw new UiLayoutException(w.FileName,w.WindowId,$"缺少 {c} 等級鈕");
            // The group's first element carries the position; the others are read as alternative images of the same strip
            // (推測, like the item tabs that explicitly share one position). An explicit position on a later one is honoured.
            var anchor=list[0].Offset??throw new UiLayoutException(w.FileName,list[0].Path,$"{list[0].SurfName} 沒有 xPos／yPos");
            var cls=c;
            groups[c]=list.Select((e,k)=>AddPart(e,surfs,()=>Class==cls&&SelectedLevel==k,e.Offset??anchor)).ToArray();
        }
        // Rows start below the tallest level image (or the upper arrow, whichever is lower) and end at the lower arrow.
        var up=w.Placed("SURF_RENEWAL_WIN_INC_BUT_OFF").Offset!.Value;var down=w.Placed("SURF_RENEWAL_WIN_DEC_BUT_OFF").Offset!.Value;
        int stripBottom=groups.Values.SelectMany(g=>g).Select(p=>p.Offset.Y+(p.Image?.Height??0)).DefaultIfEmpty(0).Max();
        int left=w.Placed("SURF_RENEWAL_WIN_SPELL_BASE").Offset!.Value.X,right=w.Placed("SURF_RENEWAL_WIN_SCROLLBAR").Offset!.Value.X;
        Rows=LegacyCellGrid.Fit(left,Math.Max(stripBottom,Math.Min(up.Y,down.Y)),right,Math.Max(up.Y,down.Y),new(right-left,RowPitch),right-left);
        if(Rows.PageSize==0)throw new UiLayoutException(w.FileName,w.WindowId,"魔法清單沒有空間（等級鈕／箭頭的位置不合理）");
        Class=null;SelectedLevel=0;localSelection=null;
    }
    protected override void OnUpdate(IUiGameState state){
        var s=FindState<IUiSpellWindowState>(state);
        source=s;learned=s?.Learned;
        if(s?.Class is {} c)SetClass(c);
        if(localSelection is int id&&learned?.Any(m=>m.Id==id)!=true)localSelection=null;
    }

    protected override IEnumerable<Rectangle> Controls()=>[LevelRect,Logical(Rows.Area)];
    protected override bool Click(Point p,IUiCommands commands){
        if(Class is {} c&&LegacyStrip.SegmentAt(LevelRect,LevelCount(c),p) is int level){SetLevel(level);return true;}
        if(SpellAt(p) is {} spell){SelectSpell(spell.Id);return true;}
        return false;
    }
    /// <summary>Row text: name on the left, the nominal MP on the right; unsupported spells in grey; selection framed.</summary>
    protected override void PaintContent(UiCanvas canvas){
        var rows=VisibleSpells;int? selected=SelectedId;
        for(int r=0;r<rows.Count;r++){
            var rect=RowRect(r);var m=rows[r];
            string mp=m.NominalMp>0?m.NominalMp.ToString(CultureInfo.InvariantCulture):"";
            int mpWidth=mp.Length==0?0:UiText.Measure(canvas.Graphics,mp).Width+6;
            var color=m.Supported?TextColor:Color.Gray;
            DrawClipped(canvas,m.Name,rect.X+3,rect.Y+2,rect.Right-3-mpWidth,color);
            DrawRightAligned(canvas,mp,rect.Right-3,rect.Y+2,color);
            if(m.Id==selected)DrawFrame(canvas,rect,SelectedColor);
        }
    }

    // ---------------------------------------------------------------- offline checks (synthetic XML and surfaces only)

    internal static string TestXml(){
        var sb=new System.Text.StringBuilder("""
<?xml version="1.0"?>
<ExtraWindowConfigulation>
<SPELL_WINDOW x = "0" y = "0">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_SPELL_BASE" surfNumber = "2056" xPos = "6" yPos = "8"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_SPELL_FRAME" surfNumber = "2057"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_PET_ITEM_X" surfNumber = "2037" xPos = "179" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WIN_SCROLLBAR" surfNumber = "2039" xPos = "171" yPos = "58"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber = "2040" xPos = "171" yPos = "225"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber = "2042" xPos = "171" yPos = "44"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber = "2044"/>

""");
        void Group(string prefix,int first,int count){
            for(int k=1;k<=count;k++)sb.Append($"\t<IMAGE_BTN surfName = \"SURF_RENEWAL_SPELL_{prefix}LEVEL{k:00}\" surfNumber = \"{first+k-1}\"{(k==1?" xPos = \"12\" yPos = \"37\"":"")}/>\n");
        }
        Group("PRINCE_",2058,7);Group("",2065,10);Group("ELF_",2075,11);Group("DARKELF_",2086,5);
        sb.Append("</SPELL_WINDOW>\n</ExtraWindowConfigulation>\n");
        return sb.ToString();
    }

    [WorldModuleTest("native-spell-window")]
    static void Checks(WorldModuleTestContext t){
        var sizes=new Dictionary<int,(int,int)>{[2056]=(170,260),[2057]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10)};
        for(int s=2058;s<=2090;s++)sizes[s]=(150,18);
        var surf=LegacyWindowTestKit.Surfaces(sizes);
        string xml=TestXml();
        var layout=UiLayout.Parse(xml,"uimagicitem.xml");
        var p=new SpellWindowPanel();p.Load(layout,surf.Get);
        t.Check(p.Id=="SPELL_WINDOW"&&p.Layer==UiLayer.Window,"SPELL_WINDOW on the window layer");
        t.Equal(new Rectangle(0,0,200,290),p.Bounds,"bounds");
        t.Equal(new Rectangle(179,15,12,12),p.CloseRect,"close 2037 at (179,15)");
        t.Check(p.ScrollUpRect==new Rectangle(171,44,10,12)&&p.ScrollDownRect==new Rectangle(171,225,10,12),"arrows as in the item window");
        t.Check(p.LevelCount(LegacySpellClass.Prince)==7&&p.LevelCount(LegacySpellClass.General)==10&&p.LevelCount(LegacySpellClass.Elf)==11&&p.LevelCount(LegacySpellClass.DarkElf)==5,"level groups 7/10/11/5 read from the XML");
        t.Check(p.Class==null&&p.LevelRect.IsEmpty&&p.PaintedParts.Select(x=>x.Surf).SequenceEqual(new int?[]{2056,2057,2037,2039,2040,2042}),"no class → chrome only, no level image");

        t.Check(ClassOf(0)==LegacySpellClass.Prince&&ClassOf(1)==LegacySpellClass.General&&ClassOf(2)==LegacySpellClass.Elf&&ClassOf(3)==LegacySpellClass.General&&ClassOf(4)==LegacySpellClass.DarkElf&&ClassOf(5)==null&&ClassOf(-1)==null,"3.81 class type → group");

        var basic=new UiStaticGameState(InWorld:true);
        var spells=new[]{new LearnedMagic(1,"合成技能 1",true,"自己",4),new LearnedMagic(2,"合成技能 2",true,"自己",4),new LearnedMagic(5,"合成技能 5",false,"指定對象",8)};
        p.Open();p.Update(new UiSpellWindowGameState(basic,new UiSpellWindowData(LegacySpellClass.Elf,spells)));
        t.Check(p.Visible&&p.Class==LegacySpellClass.Elf&&p.Learned?.Count==3,"state through the decorator: elf, 3 learned spells");
        t.Equal(new Rectangle(12,37,150,18),p.LevelRect,"level image at (12,37)");
        t.Check(p.PaintedParts.Where(x=>x.Surf is >=2058 and <=2090).Select(x=>x.Surf).SequenceEqual(new int?[]{2075}),"elf level 1 → 2075 only");
        p.SetLevel(10);
        t.Check(p.PaintedParts.Where(x=>x.Surf is >=2058 and <=2090).Select(x=>x.Surf).SequenceEqual(new int?[]{2085})&&p.LevelRect==new Rectangle(12,37,150,18),"elf level 11 → 2085 at the same anchor");
        t.Throws<ArgumentOutOfRangeException>(()=>p.SetLevel(11),"elf has no level 12");
        p.Update(new UiSpellWindowGameState(basic,new UiSpellWindowData(LegacySpellClass.DarkElf,spells)));
        t.Check(p.SelectedLevel==0&&p.PaintedParts.Any(x=>x.Surf==2086),"class change resets the level; dark elf 2086");
        t.Throws<ArgumentOutOfRangeException>(()=>p.SetLevel(5),"dark elf has 5 levels in the XML");
        p.Update(new UiSpellWindowGameState(basic,new UiSpellWindowData(null,null)));
        t.Check(p.Class==LegacySpellClass.DarkElf&&p.Learned==null,"unknown class keeps the last known one; skill list not received → null");

        var cmd=new UiNullCommands();
        p.Update(new UiSpellWindowGameState(basic,new UiSpellWindowData(LegacySpellClass.General,spells)));
        for(int k=0;k<5;k++)LegacyWindowTestKit.Click(p,new(175,230),cmd);
        t.Equal(0,p.ScrollOffset,"3 spells on level page 1 fit the rows → nothing to scroll (row scrolling: LegacyWindowContentTests)");
        p.Update(new UiSpellWindowGameState(basic,new UiSpellWindowData(LegacySpellClass.General,spells[..1])));
        t.Equal(0,p.ScrollOffset,"shorter list re-clamps the scroll");
        t.Check(LegacyWindowTestKit.Click(p,new(20,40),cmd)&&p.SelectedLevel==0,"click on the first segment of the level strip → level 1 (R4-B)");
        LegacyWindowTestKit.Click(p,new(185,20),cmd);
        t.Check(!p.Visible&&cmd.Calls.Count==0,"close hides, nothing sent");

        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);p.Open();var st=new UiSpellWindowGameState(basic,new UiSpellWindowData(LegacySpellClass.Prince,spells));
            root.Update(st);p.SetLevel(6);root.Compose(canvas,null);
            t.Equal(LegacyWindowTestKit.ColourOf(2064),canvas.Bitmap.GetPixel(20,45).ToArgb(),"prince level 7 image painted at (12,37)");
        }

        t.Throws<UiLayoutException>(()=>new SpellWindowPanel().Load(UiLayout.Parse(xml.Replace("SURF_RENEWAL_SPELL_ELF_LEVEL03","SURF_RENEWAL_SPELL_ELF_LEVEL13"),"uimagicitem.xml"),surf.Get),"gap in level numbers → UiLayoutException");
        t.Throws<UiLayoutException>(()=>new SpellWindowPanel().Load(UiLayout.Parse(System.Text.RegularExpressions.Regex.Replace(xml,"\\t<IMAGE_BTN surfName = \"SURF_RENEWAL_SPELL_DARKELF_[^\\n]*\\n",""),"uimagicitem.xml"),surf.Get),"no dark elf group → UiLayoutException");
        t.Throws<UiLayoutException>(()=>new SpellWindowPanel().Load(UiLayout.Parse(xml.Replace("surfNumber = \"2058\" xPos = \"12\" yPos = \"37\"","surfNumber = \"2058\""),"uimagicitem.xml"),surf.Get),"group without an anchor position → UiLayoutException");
    }
}
