namespace P06.NativeUi;

/// <summary>One thing the panel drew in the last Paint (logical coordinates); used by checks and the evidence table.</summary>
public sealed record LeftStatusDraw(string Element,int? Surf,Rectangle Rect,string? Text);

/// <summary>
/// ui-left-status (Issue #464, docs/design/native-ui.md §5.3): uistatus86 LEFT_WINDOW at (x, y) from the XML (TW: (0, 475)).
/// Every component position is read at run time: LEFT_WINDOW x/y + the child's xRPos/yRPos. Nothing is hard-coded or invented;
/// a missing node throws UiLayoutException (whole native UI refused), a missing surf draws nothing for that component only.
/// Scope: frame 3168, level bar 3169 + level / EXP %, AC, MR %, alignment icon 3170–3172 + value, SP, weight bar 3173–3175 + %,
/// food bar + %, adena. Not in scope (later tasks / unverified behaviour): LEVEL_BTN, KARMA_WINDOW, warn/shout buttons.
/// </summary>
public sealed class LeftStatusPanel:IUiPanel {
    [UiPanel(10)] internal static UiPanelRegistration Registration=>new("LEFT_WINDOW","uistatus86",root=>new LeftStatusPanel());   // ui-left-status #464
    public const string WindowName="LEFT_WINDOW";
    public string Id=>WindowName;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>inWorld;
    /// <summary>Text colour / 1-px shadow. 推測 until compared with an original screenshot (V4 (f) covers the face).</summary>
    public static readonly Color TextColor=Color.White,TextShadow=Color.Black;

    sealed record Part(string Element,Point At,int[] Surfs){public int? Surf=>Surfs.Length>0?Surfs[0]:null;}
    readonly Dictionary<string,Part> parts=new(StringComparer.Ordinal);
    SurfCatalog? surfs;
    LeftStatusState state=LeftStatusState.Empty;
    bool inWorld;
    readonly List<LeftStatusDraw> drawn=[];

    public Point Origin{get;private set;}
    public LeftStatusState State=>state;
    /// <summary>Logical position (window + relative) of every component that was read, keyed by "GROUP/ELEMENT".</summary>
    public IReadOnlyDictionary<string,Point> Components=>parts.ToDictionary(p=>p.Key,p=>p.Value.At);
    public IReadOnlyList<LeftStatusDraw> Drawn=>drawn;

    // Element paths inside LEFT_WINDOW (names verbatim from uistatus86-c.xml, including FOOD_GUAGE_BAR).
    public const string Frame="MAIN_WINDOW/MAIN_FRAME";
    public const string LevelBar="LEVEL_WINDOW/LEVEL_BAR",LevelNumber="LEVEL_WINDOW/LEVEL_NUMBER",ExpPercent="LEVEL_WINDOW/EXP_PERCENT_A";
    public const string AcNumber="AC_WINDOW/AC_NUMBER",MrPercent="MR_WINDOW/MR_PERCENTAGE",SpNumber="SP_WINDOW/SP_NUMBER";
    public const string ChaoticIcon="ALIGN_WINDOW/CHAOTIC_ICON",LawfulIcon="ALIGN_WINDOW/LAWFUL_ICON",NeutralIcon="ALIGN_WINDOW/NEUTRAL_ICON",AlignNumber="ALIGN_WINDOW/ALIGN_NUMBER";
    public const string CarryLow="CARRY_GAUGE_WINDOW/LOW_GAUGE_BAR",CarryMid="CARRY_GAUGE_WINDOW/MID_GAUGE_BAR",CarryHigh="CARRY_GAUGE_WINDOW/HIGH_GAUGE_BAR",CarryNumber="CARRY_GAUGE_WINDOW/CARRY_GAUGE_NUMBER";
    public const string FoodLow="FOOD_GAUGE_WINDOW/FOOD_LOW_GAUGE_BAR",FoodNormal="FOOD_GAUGE_WINDOW/FOOD_GUAGE_BAR",FoodMax="FOOD_GAUGE_WINDOW/FOOD_MAX_BAR",FoodNumber="FOOD_GAUGE_WINDOW/FOOD_NUMBER";
    public const string AdenaValue="ADENA_WINDOW/ADENA_VALUE";
    static readonly string[] SurfParts=[Frame,LevelBar,ChaoticIcon,LawfulIcon,NeutralIcon,CarryLow,CarryMid,CarryHigh,FoodLow,FoodNormal,FoodMax];
    static readonly string[] TextParts=[LevelNumber,ExpPercent,AcNumber,MrPercent,SpNumber,AlignNumber,CarryNumber,FoodNumber,AdenaValue];

    public void Load(UiLayout layout,SurfCatalog surfs){
        this.surfs=surfs;parts.Clear();
        var window=layout.Root.Child(WindowName);
        Origin=new(window.Int("x"),window.Int("y"));
        foreach(var path in SurfParts){var n=window.Child(path);parts[path]=new(path,At(n),n.Surfs());}
        foreach(var path in TextParts){var n=window.Child(path);parts[path]=new(path,At(n),[]);}
        // Bounds: the frame's size; if the frame surf is missing, the union of the component surfs that exist (never invented).
        Rectangle? box=Rect(parts[Frame]);
        if(box==null)foreach(var path in SurfParts)if(Rect(parts[path]) is Rectangle r)box=box is Rectangle b?Rectangle.Union(b,r):r;
        Bounds=box??new Rectangle(Origin,Size.Empty);
    }
    Point At(UiNode n)=>new(Origin.X+n.Int("xRPos"),Origin.Y+n.Int("yRPos"));
    UiSurface? Surface(Part p)=>p.Surf is int s?surfs?.Get(s):null;
    Rectangle? Rect(Part p)=>Surface(p) is {} s?new Rectangle(p.At,new Size(s.Width,s.Height)):null;

    public void Update(IUiGameState state){inWorld=state.InWorld;this.state=inWorld?LeftStatusState.From(state):LeftStatusState.Empty;}

    public void Paint(UiCanvas canvas){
        drawn.Clear();
        var s=state;
        Image(canvas,Frame);
        Gauge(canvas,LevelBar,s.ExpPercent,100m);
        Text(canvas,LevelNumber,s.LevelText);
        Text(canvas,ExpPercent,s.ExpText);
        Text(canvas,AcNumber,s.AcText);
        Text(canvas,MrPercent,s.MrText);
        if(s.Alignment is UiAlignment a)Image(canvas,a switch{UiAlignment.Lawful=>LawfulIcon,UiAlignment.Chaotic=>ChaoticIcon,_=>NeutralIcon});
        Text(canvas,AlignNumber,s.LawfulText);
        Text(canvas,SpNumber,s.SpText);
        if(s.WeightBand is UiGaugeBand wb)Gauge(canvas,wb switch{UiGaugeBand.High=>CarryHigh,UiGaugeBand.Mid=>CarryMid,_=>CarryLow},s.Weight,LeftStatusState.WeightScale);
        Text(canvas,CarryNumber,s.WeightText);
        if(s.FoodBand is UiGaugeBand fb)Gauge(canvas,fb switch{UiGaugeBand.High=>FoodMax,UiGaugeBand.Low=>FoodLow,_=>FoodNormal},s.Food,LeftStatusState.FoodScale);
        Text(canvas,FoodNumber,s.FoodText);
        Text(canvas,AdenaValue,s.AdenaText,rightAligned:true);
    }
    void Image(UiCanvas canvas,string path){
        var p=parts[path];if(Surface(p) is not {} surf)return;
        canvas.DrawSurface(surf,p.At.X,p.At.Y);drawn.Add(new(path,p.Surf,new(p.At,new(surf.Width,surf.Height)),null));
    }
    /// <summary>Gauge: the surf cropped from the left to width × value / scale (推測: crop direction and rounding).</summary>
    void Gauge(UiCanvas canvas,string path,decimal? value,decimal scale){
        var p=parts[path];if(Surface(p) is not {} surf)return;
        int w=LeftStatusState.Fill(surf.Width,value,scale);if(w<=0)return;
        canvas.DrawSurface(surf,new Rectangle(0,0,w,surf.Height),p.At.X,p.At.Y);drawn.Add(new(path,p.Surf,new(p.At,new(w,surf.Height)),null));
    }
    /// <summary>Text at the XML point (top-left). ADENA_VALUE ends at its x (推測: x=115 leaves no room for left-aligned digits).</summary>
    void Text(UiCanvas canvas,string path,string text,bool rightAligned=false){
        if(text.Length==0)return;
        var p=parts[path];var size=UiText.Measure(canvas.Graphics,text);
        int x=rightAligned?p.At.X-size.Width:p.At.X;
        canvas.DrawText(text,x,p.At.Y,TextColor,TextShadow);drawn.Add(new(path,null,new(x,p.At.Y,size.Width,size.Height),text));
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
