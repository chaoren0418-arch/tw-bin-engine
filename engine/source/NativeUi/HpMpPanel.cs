namespace P06.NativeUi;

// ui-hpmp (Issue #465, docs/design/native-ui.md §5.3). Two panels:
//   HpMpPanel        — the original floating HP/MP window (RTTI HpMpWindow, layout sub_ED9030, paint sub_ED2680/sub_ED56D0).
//   HpMpHeadBarPanel — the over-head HP/MP bars (Lineage::MiniHpMpBarManager, sub_8E7290/sub_8E77F0/sub_8E4D40/sub_8E4DE0).
// Neither has an XML layout: the original hard-codes every coordinate and surf, so the numbers below are the verified V1
// tables (docs/reference/ui-layout.md §2 V1), not invented defaults. HP/MP come only from the server values in IUiGameState.

/// <summary>Geometry for one (shape, status, thickness), relative to the window origin. V1 tables; sizes from the .img headers.</summary>
public sealed record HpMpLayout(
    int Shape,int Status,bool Thick,
    int Frame,Size FrameSize,
    int Hp,Rectangle HpBar,
    int Mp,Rectangle MpBar,
    int ShapeButton,Rectangle ShapeButtonRect,
    int LengthButton,Rectangle LengthButtonRect,
    Rectangle DragArea,
    int Warn,Point WarnAt) {

    static readonly int[] HFrameW=[265,320,378],HFrameWThick=[234,282,334];
    static readonly int[] HHpW=[85,112,141],HMpW=[80,107,136],HBarWThick=[92,116,142];
    static readonly int[] HMpX=[133,157,183],HShapeX=[85,109,135],HLengthX=[127,151,177],HDragX=[102,126,152];
    static readonly int[] VFrameW=[197,230,263],VFrameWThick=[141,167,193];
    static readonly int[] VBarW=[108,142,174],VBarWThick=[102,128,154];

    public static HpMpLayout For(int shape,int status,bool thick){
        int s=Math.Clamp(status,0,2);
        if(shape!=0){
            // Horizontal (shape 1, default). HP at (9, 11); MP x per status; buttons 22×7 at y 3; drag strip (x, 0)–(x+30, 30).
            var frame=thick?new Size(HFrameWThick[s],34):new Size(HFrameW[s],48);
            int barH=thick?14:20;
            return new(1,s,thick,
                (thick?2908:2353)+s,frame,
                (thick?2914:2359)+3*s,new(9,11,thick?HBarWThick[s]:HHpW[s],barH),
                (thick?2911:2356)+s,new(HMpX[s],11,thick?HBarWThick[s]:HMpW[s],barH),
                2368,new(HShapeX[s],3,22,7),
                2371,new(HLengthX[s],3,22,7),
                Rectangle.FromLTRB(HDragX[s],0,HDragX[s]+30,30),
                (thick?2941:2935)+s,new(6,8));
        }
        // Vertical (shape 0). HP (30, 9); MP (30, 22) or thick (30, 26); shape button (19, 0); length button (36, 0).
        var vframe=thick?new Size(VFrameWThick[s],46):new Size(VFrameW[s],48);
        int w=thick?VBarWThick[s]:VBarW[s],h=thick?14:15;
        return new(0,s,thick,
            (thick?2890:2332)+s,vframe,
            (thick?2896:2338)+3*s,new(30,9,w,h),
            (thick?2893:2335)+s,new(30,thick?26:22,w,h),
            thick?2905:2347,new(19,0,21,8),
            2350,new(36,0,26,8),
            thick?Rectangle.FromLTRB(0,9,33,42):Rectangle.FromLTRB(0,5,33,38),
            (thick?2938:2932)+s,new(27,6));
    }
}

/// <summary>Floating HP/MP window. Float layer, draggable by its drag strip; two buttons toggle shape and cycle length.</summary>
public sealed class HpMpPanel:IUiMovablePanel {
    public const string PanelId="HpMpWindow";
    /// <summary>Low-HP threshold (sub_EDB8E0). 0.40 when byte_1542AA1 == 7 (class unknown here, so not applied).</summary>
    public const double WarnRatio=0.35;
    public const int WarnTicks=6,WarnTickMs=300;
    /// <summary>Text colours: palette[14] white, second colour palette[0] black (sub_841770 / sub_841190).</summary>
    public static readonly Color TextColor=Color.FromArgb(255,255,255),TextSecond=Color.FromArgb(0,0,0);

    public enum Button { None, Shape, Length }

    SurfCatalog? surfs;
    Point origin;Size frameSize;
    long? hp,maxHp,mp,maxMp;bool inWorld;
    long? lastHp;int warnCounter;long warnNext;
    Button pressed;

    public HpMpPanel(HpMpSettings? settings=null){
        Settings=(settings??HpMpSettings.Default).Normalized();
        Layout=HpMpLayout.For(Settings.Shape,Settings.Status,Settings.Thickness);
        origin=Settings.InitialPosition();
        Relayout();
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    public HpMpSettings Settings{get;private set;}
    public HpMpLayout Layout{get;private set;}
    /// <summary>
    /// Frame size of the surface actually drawn (the original clamps with the runtime frame width). In the Taiwan client the
    /// thick set resolves to &lt;n&gt;c.img with the normal sizes, so this can differ from the V1 table (plain .img); the table
    /// size is the fallback before Load or when the surf is missing.
    /// </summary>
    public Rectangle Bounds=>new(origin,frameSize);
    public Size FrameSize=>frameSize;
    public bool Visible=>inWorld&&Settings.Show;
    /// <summary>Low-HP warning frame drawn this frame (counter odd).</summary>
    public bool WarnVisible=>warnCounter%2==1;
    public int WarnCounter=>warnCounter;
    public Button Pressed=>pressed;

    public void Load(UiLayout layout,SurfCatalog surfs){this.surfs=surfs;Relayout();}

    /// <summary>Applies new settings (e.g. from an option page) and re-lays out like sub_ED8F20.</summary>
    public void Apply(HpMpSettings settings){
        Settings=settings.Normalized();
        Layout=HpMpLayout.For(Settings.Shape,Settings.Status,Settings.Thickness);
        Relayout();
    }
    void Relayout(){
        frameSize=Layout.FrameSize;
        if(surfs!=null){
            try{if(surfs.Get(Layout.Frame) is {} f)frameSize=new(f.Width,f.Height);}
            catch(UiResourceException ex){UiResourceLog.Write($"{PanelId}: surf {Layout.Frame} 無法解碼，改用 V1 表尺寸：{ex.Message}");}
        }
        if(origin.X+frameSize.Width>HpMpSettings.MaxRight)origin.X=Math.Max(0,HpMpSettings.MaxRight-frameSize.Width);
        Settings=Settings with{PositionX=origin.X,PositionY=origin.Y};
    }

    /// <summary>"切換排法" callback sub_EDC840: flip shape.</summary>
    public void ToggleShape()=>Apply(Settings with{Shape=Settings.Shape==0?1:0});
    /// <summary>"切換長度" callback sub_EDC620: status = (status + 1) % 3.</summary>
    public void CycleLength()=>Apply(Settings with{Status=(Settings.Status+1)%3});

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        hp=state.Hp;maxHp=state.MaxHp;mp=state.Mp;maxMp=state.MaxMp;
        // Low-HP frame: a new HP value at or below the threshold restarts the counter at 6; it counts down every 300 ms.
        if(inWorld&&hp is long h&&maxHp is long m&&m>0){
            if(h!=lastHp&&h<=m*WarnRatio){warnCounter=WarnTicks;warnNext=state.Now+WarnTickMs;}
            lastHp=h;
        }else{lastHp=null;warnCounter=0;}
        while(warnCounter>0&&state.Now>=warnNext){warnCounter--;warnNext+=WarnTickMs;}
    }

    /// <summary>Filled width of a bar w wide for value/max; null when the server has not sent both.</summary>
    public static int? FillWidth(int w,long? value,long? max){
        if(value is not long v||max is not long m||m<=0)return null;
        return (int)(w*Math.Clamp(v,0,m)/m);
    }
    /// <summary>"%g%%" value: ≤ 99 rounds down (sub_8611D0), > 99 rounds up (sub_8611F0), so 100% only at full.</summary>
    public static string? PercentText(long? value,long? max){
        if(value is not long v||max is not long m||m<=0)return null;
        double p=Math.Clamp(v,0,m)*100.0/m;
        return (p<=99?Math.Floor(p):Math.Ceiling(p)).ToString(System.Globalization.CultureInfo.InvariantCulture)+"%";
    }
    public static string? ValueText(long? value,long? max)=>value is long v&&max is long m?$"{v}/{m}":null;
    public bool PercentShown=>Settings.ShowPercent&&Settings.Status!=0;

    public void Paint(UiCanvas canvas){
        if(surfs==null)return;
        var L=Layout;int ox=origin.X,oy=origin.Y;
        canvas.DrawSurface(surfs.Get(L.Frame),ox,oy);
        if(WarnVisible)canvas.DrawSurface(surfs.Get(L.Warn),ox+L.WarnAt.X,oy+L.WarnAt.Y);
        PaintBar(canvas,surfs.Get(L.Hp),L.HpBar,hp,maxHp,rightAligned:L.Shape==1,ox,oy);
        PaintBar(canvas,surfs.Get(L.Mp),L.MpBar,mp,maxMp,rightAligned:false,ox,oy);
        canvas.DrawSurface(surfs.Get(L.ShapeButton+(pressed==Button.Shape?1:0)),ox+L.ShapeButtonRect.X,oy+L.ShapeButtonRect.Y);
        canvas.DrawSurface(surfs.Get(L.LengthButton+(pressed==Button.Length?1:0)),ox+L.LengthButtonRect.X,oy+L.LengthButtonRect.Y);
        int dy=L.Thick?0:-2;
        void Value(Rectangle bar,long? v,long? m){
            if(ValueText(v,m) is not {} t)return;
            int tw=UiText.Measure(canvas.Graphics,t).Width;
            canvas.DrawText(t,ox+bar.X+bar.Width/2-tw/2,oy+bar.Y+2+dy,TextColor,TextSecond);
        }
        Value(L.HpBar,hp,maxHp);Value(L.MpBar,mp,maxMp);
        if(PercentShown){
            void Percent(Rectangle bar,long? v,long? m,bool left){
                if(PercentText(v,m) is not {} t)return;
                int tw=UiText.Measure(canvas.Graphics,t).Width;
                int x=left?bar.X+7:bar.X+bar.Width-tw+4;
                int y=bar.Y+dy+bar.Height/2-(2*(L.Thick?1:0)+1);
                canvas.DrawText(t,ox+x,oy+y,TextColor,TextSecond);
            }
            Percent(L.HpBar,hp,maxHp,left:L.Shape==1);
            Percent(L.MpBar,mp,maxMp,left:false);
        }
    }

    /// <summary>
    /// sub_E53BD0 fill: horizontal HP keeps the right part [w − fill, w] at the same offset (shrinks toward the right);
    /// everything else keeps [0, fill]. Bar image width w is the drawn surface width.
    /// </summary>
    static void PaintBar(UiCanvas canvas,UiSurface? bar,Rectangle at,long? value,long? max,bool rightAligned,int ox,int oy){
        if(bar==null||FillWidth(bar.Width,value,max) is not int fill||fill<=0)return;
        int sx=rightAligned?bar.Width-fill:0;
        canvas.DrawSurface(bar,new Rectangle(sx,0,fill,bar.Height),ox+at.X+sx,oy+at.Y);
    }

    /// <summary>Button under a logical point (buttons win over the drag strip).</summary>
    public Button ButtonAt(Point p){
        var local=new Point(p.X-origin.X,p.Y-origin.Y);
        if(Layout.ShapeButtonRect.Contains(local))return Button.Shape;
        if(Layout.LengthButtonRect.Contains(local))return Button.Length;
        return Button.None;
    }
    public bool IsDragHandle(Point p)=>ButtonAt(p)==Button.None&&Layout.DragArea.Contains(p.X-origin.X,p.Y-origin.Y);
    /// <summary>Drag (sub_ED8D40): y capped at 550; x keeps the frame within 799.</summary>
    public void MoveTo(Point location){
        origin=new(Math.Clamp(location.X,0,Math.Max(0,HpMpSettings.MaxRight-frameSize.Width)),Math.Clamp(location.Y,0,HpMpSettings.MaxY));
        Settings=Settings with{PositionX=origin.X,PositionY=origin.Y};
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(e.Button!=MouseButtons.Left&&e.Kind!=UiMouseKind.Move)return Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Down:pressed=ButtonAt(e.Location);return true;
            case UiMouseKind.Up:{
                var b=pressed;pressed=Button.None;
                if(b!=Button.None&&ButtonAt(e.Location)==b){if(b==Button.Shape)ToggleShape();else CycleLength();}
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}

/// <summary>
/// Over-head HP/MP bars, drawn on the world (UiLayer.World is excluded from hit testing, so world clicks still reach the
/// world). Surfs 930 (base), 931 (HP), 932 (MP), each 44×5; each bar shows 4 rows; MP sits 4 px below HP.
/// </summary>
public sealed class HpMpHeadBarPanel(Func<HpMpSettings>? settings=null):IUiPanel {
    public const string PanelId="MiniHpMpBar";
    public const int BaseSurf=930,HpSurf=931,MpSurf=932,BarWidth=44,FillRows=4,MpOffsetY=4;
    SurfCatalog? surfs;bool inWorld;
    long? hp,maxHp,mp,maxMp;
    IReadOnlyList<HpMpHeadTarget> targets=[];

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.World;
    public Rectangle Bounds=>UiRoot.WorldRect;
    public bool Visible=>inWorld;
    HpMpSettings Settings=>settings?.Invoke()??HpMpSettings.Default;

    public void Load(UiLayout layout,SurfCatalog surfs){this.surfs=surfs;}
    public void Update(IUiGameState state){
        inWorld=state.InWorld;hp=state.Hp;maxHp=state.MaxHp;mp=state.Mp;maxMp=state.MaxMp;
        targets=state is IUiHpMpState s?s.HeadTargets:[];
    }

    /// <summary>Own percentage (sub_8E7110): value × 100 / max clamped to 0–100, truncated. null when not sent.</summary>
    public static int? OwnPercent(long? value,long? max){
        if(value is not long v||max is not long m||m<=0)return null;
        return (int)Math.Clamp(v*100/m,0,100);
    }
    /// <summary>sub_8E77F0: x = bbox centre − 24, y = bbox top − 12 (own x formula unverified; same rule used).</summary>
    public static Point Anchor(Rectangle box)=>new(box.Left+box.Width/2-24,box.Top-12);
    /// <summary>Draw only when x ≥ 0, y ≥ 0, x ≤ 800; others also need y ≤ 600.</summary>
    public static bool OnScreen(Point p,bool self)=>p.X>=0&&p.Y>=0&&p.X<=UiRoot.LogicalWidth&&(self||p.Y<=UiRoot.LogicalHeight);

    /// <summary>Bars that would be drawn this frame: (target, anchor, HP%, MP%).</summary>
    public IEnumerable<(HpMpHeadTarget Target,Point At,int? Hp,int? Mp)> Plan(){
        bool showSelf=Settings.ShowCharacterHpMp;
        foreach(var t in targets){
            if(t.Self&&!showSelf)continue;
            int? h=t.Self?OwnPercent(hp,maxHp):t.HpPercent is int a?Math.Clamp(a,0,100):null;
            int? m=t.Self?OwnPercent(mp,maxMp):t.MpPercent is int b?Math.Clamp(b,0,100):null;
            if(h==null&&m==null)continue;
            var at=Anchor(t.Box);
            if(!OnScreen(at,t.Self))continue;
            yield return (t,at,h,m);
        }
    }

    public void Paint(UiCanvas canvas){
        if(surfs==null)return;
        var bg=surfs.Get(BaseSurf);var hpBar=surfs.Get(HpSurf);var mpBar=surfs.Get(MpSurf);
        foreach(var (_,at,h,m) in Plan()){
            if(h is int hpPct)Bar(canvas,bg,hpBar,at.X,at.Y,hpPct);
            if(m is int mpPct)Bar(canvas,bg,mpBar,at.X,at.Y+MpOffsetY,mpPct);
        }
    }
    static void Bar(UiCanvas canvas,UiSurface? bg,UiSurface? fill,int x,int y,int pct){
        canvas.DrawSurface(bg,x,y);
        if(fill==null)return;
        int w=fill.Width*pct/100;
        if(w>0)canvas.DrawSurface(fill,new Rectangle(0,0,w,Math.Min(FillRows,fill.Height)),x,y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
