using System.Globalization;
namespace P06.NativeUi;

// T325 (#763, P-D of docs/reference/pak-resources.md §6, row 1:43 / 4:ResetStatUI): the native stat-reset window.
// TW16 reads ResetStatUI.xml; 0-客戶端 does not ship it (PAK§4.2: all paks checked). It is read at run time from the
// reference client through UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak, "-c"
// first; SHA-256 logged as "ui-xml-ref"). Nothing is copied into the repository.
//
// Loading. Same as OptionPanel (#768) / RenewalStatusPanel (#766): registered on the host layout "uistatus86" and Load reads
// ResetStatUI itself. No reference client / no XML / malformed XML / no window size → Unavailable; CharResetUi.cs then keeps
// opening the WinForms dialog, and --classic-ui never routes here.
//
// Images. Surfs go through SurfCatalog.Get (local first; 8670–8689, 8734, 8735 and 7156 are not in the pinned local Sprite
// archives and come from the T322/#760 reference fallback — kor25 has all of them as "<id>c.img"/"<id>.img"). Pngs
// (10933 Stat_Change back, 10935 Customizing back) go through OptionPngSource. Missing images are not drawn (no substitute)
// and are listed in Missing. HighlightSurf is drawn while the pointer is over an enabled button, PushSurf while held.
//
// Data. The window is a view of CharResetView (CharReset.cs, unchanged) through IResetStatHost; every send goes through the
// existing LoginConnection.SendCharReset with the same stamp/legality checks as the WinForms dialog (CharResetUi.cs).
//
// Layout rules (推測; the ResetStat binder was not decompiled for this):
// - Root holds two Windows. ResetStat_Win is the full 800×600 window (declared Width/Height; else its Back_Image size).
//   Message_Window (Modal, Top) is the confirm box drawn over it; its size = BaseSurf image (else Back_Image image).
// - Coordinates are relative to the parent element (Window/Control); a missing X/Y is 0.
// - Draw order inside a window is the reverse of the XML order (the backgrounds Back_Image / BG_Image / ProgressStatus_Back
//   come last in the XML but must be under everything); hit testing uses XML order (first = topmost). Same as #761.
// - "_Multi"/"_multi" images duplicate the "_Screen"/"_screen" ones at the same spot; only the screen variant is built.
// - Customizing_Win (derived bonus table, Exit_CustomizingWin) is not built: the engine has no values for it (待做).
// - Progress_*_On/Off: stage Reset (initial), LevelUp (level), Done (elixir and after). On = the stage is reached;
//   arrows On = the stage after the arrow is reached. While waiting for the server the last stage stays.
// - Stat Controls (Strength …) carry a Point label and Down/Up buttons. initial/elixir: Up/Down edit the draft (base…35,
//   remaining points ≥ 0). level phase at level ≥ 50: Up sends one level with that attribute (WinForms: attribute box +
//   "提升 1 級"); 1Level_Button only below level 50; 10Level_Button while level+10 ≤ min(50, max).
// - OK_Button: edit phase with 0 points left / level phase at max level → Message_Window confirm → send. Phase ended/idle →
//   closes the window. Esc while a reset is running → Message_Window "disconnect and check after relogin" (the server has
//   no cancel packet, same text as the WinForms dialog) → Ok = IResetStatHost.Cancel.
// - Msg_Label (MultiLabel) shows the phase notice / last error; Tip_Label is String 4082 through TextResolver (null in the
//   product, see #768). Message_Window DarkBack is not painted (no colour/alpha in the XML).

/// <summary>Outward actions of the reset window; the product adapter (CharResetUi.cs) forwards to LoginConnection.</summary>
public interface IResetStatHost {
    CharResetView Snapshot();
    Task<bool> Send(CharResetView stamp,string command,ResetStats stats,int step);
    /// <summary>Leave a running reset: the server has no cancel packet, so this disconnects (LoginConnection.Cancel).</summary>
    void Cancel();
}

public enum ResetPieceKind { Image, Board, Label, MultiLabel, Button }

/// <summary>One built element, XML order. Offset is relative to its window. Surface null = missing image (not drawn).</summary>
public sealed record ResetPiece(ResetPieceKind Kind,string Path,string Element,string? Name,string? Owner,Point Offset,Size DeclaredSize,
    OptionImageRef? Image,UiSurface? Surface,UiSurface? Highlight,UiSurface? Push,string? MouseEvent,string? TextKind,string? TextValue,
    Color Color,int Alpha=255,string Align="left",string VAlign="top",int LineMargin=0) {
    public Size ImageSize=>Surface==null?Size.Empty:new(Surface.Width,Surface.Height);
    public Rectangle HitLocal=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
}

public sealed record ResetWindow(string Name,Point Origin,Size Size,UiSurface? Base,IReadOnlyList<ResetPiece> Pieces) {
    public Rectangle Bounds=>new(Origin,Size);
}

public enum ResetMessage { None, Confirm, Leave }

public sealed class ResetStatPanel(UiResources? resources=null):IUiClosablePanel {
    [UiPanel(183)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new ResetStatPanel(root.Resources));   // T325 (#763): ResetStatUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="ResetStatUI";
    public const string LayoutName="ResetStatUI",RootNode="ResetStatUI",MainWindow="ResetStat_Win",MessageWindow="Message_Window",SkippedControl="Customizing_Win";
    public const string HostLayout="uistatus86";
    public const string UpEvent="Up_ResetStatWin",DownEvent="Down_ResetStatWin",Level1Event="Level1_ResetStatWin",Level10Event="Level10_ResetStatWin",OkEvent="OK_ResetStatWin",
        MsgOkEvent="Ok_MessageWin",MsgCancelEvent="Cancel_MessageWin";
    public const int MaxStat=35;
    public const string ConfirmText="確認送出目前配點？送出後由原服處理，不能在此復原。";
    public const string LeaveText="原服沒有取消重置封包。中斷連線並於重新登入後核對？";
    public const string RejectedText="未送出：配點不合法、操作過期或連線忙碌。";

    /// <summary>Stat Control name → ResetStats index (wire order STR, INT, WIS, DEX, CON, CHA).</summary>
    public static readonly IReadOnlyDictionary<string,int> StatControls=new Dictionary<string,int>(StringComparer.Ordinal){
        ["Strength"]=0,["Intelligence"]=1,["Wisdom"]=2,["Dexterity"]=3,["Constitution"]=4,["Charisma"]=5,
    };
    static readonly string[] Stages=["Reset","LevelUp","Done"];

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public ResetWindow? Main{get;private set;}
    public ResetWindow? Message{get;private set;}
    public Rectangle Bounds=>open&&Main!=null?Main.Bounds:Rectangle.Empty;
    public bool Visible=>open;
    public IReadOnlyList<string> Missing{get;private set;}=[];
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public Func<string,string,string?>? TextResolver{get;set;}
    public IResetStatHost? Host{get;private set;}
    public CharResetView? View{get;private set;}
    public ResetStats Draft=>ResetStats.From(draft);
    public bool Busy{get;private set;}
    public ResetMessage Shown{get;private set;}
    /// <summary>0 Reset, 1 LevelUp, 2 Done.</summary>
    public int Stage{get;private set;}
    public string? Error{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    bool open;int generation,epoch,revision=-1;int[] draft=new int[6];
    CharResetView? confirmStamp;ResetStats confirmStats;
    ResetPiece? pressed,hover;bool pressedInside;readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"reset-stat-ui 無法載入 {LayoutName}，重置窗停用（WinForms 能力重置照舊）：{ex.Message}");return;}
        var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
        try{Load(layout,surfs.Get,png.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"reset-stat-ui {layout.FileName} 版面不符，重置窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"reset-stat-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"reset-stat-ui {Unavailable}");
    }

    void Clear(){
        open=false;Host=null;View=null;Busy=false;Shown=ResetMessage.None;Stage=0;Error=null;revision=-1;draft=new int[6];
        pressed=null;hover=null;pressedInside=false;LayoutFile=null;Main=null;Message=null;Missing=[];Skipped=[];Unavailable="not loaded";
    }

    /// <summary>"8670;4" / "10911;2;28" → id 8670 (the trailing fields are flags, not used). Throws on a non-number.</summary>
    internal static OptionImageRef? ImageRef(UiNode n,string attr){
        var raw=n.Attr(attr);if(raw==null)return null;
        var parts=raw.Split(';');
        if(!int.TryParse(parts[0].Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out int id)||id<=0)throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是圖號");
        int flag=0;if(parts.Length>1&&!int.TryParse(parts[1].Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out flag))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 的旗標不是整數");
        return new(attr,id,flag);
    }
    internal static Color ParseColor(UiNode n,string attr,Color fallback){
        var raw=n.Attr(attr);if(raw==null)return fallback;
        var parts=raw.Split(',');
        if(parts.Length!=3||!parts.All(p=>byte.TryParse(p.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _)))
            throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 r,g,b");
        return Color.FromArgb(byte.Parse(parts[0].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[1].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[2].Trim(),CultureInfo.InvariantCulture));
    }

    sealed class Builder(string file,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        public readonly List<string> Missing=[],Skipped=[];
        public UiSurface? Resolve(OptionImageRef? r,string path){
            if(r==null)return null;
            var s=r.IsSurf?surf(r.Id):png(r.Id);
            if(s==null)Missing.Add($"{r} ({path})");
            return s;
        }
        public static Point Rel(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));
        static bool MultiVariant(string? name)=>name!=null&&name.EndsWith("_multi",StringComparison.OrdinalIgnoreCase);
        public void Walk(UiNode parent,Point origin,string? owner,List<ResetPiece> into){foreach(var c in parent.Children)Element(c,Rel(c,origin),owner,into);}
        void Element(UiNode c,Point at,string? owner,List<ResetPiece> into){
            string? name=c.Attr("Name");
            ResetPiece Piece(ResetPieceKind kind)=>new(kind,c.Path,c.Name,name,owner,at,new(c.TryInt("Width")??0,c.TryInt("Height")??0),null,null,null,null,c.Attr("MouseEvent"),null,null,Color.White,
                255,(c.Attr("Align")??c.Attr("HAlign")??"left").Trim().ToLowerInvariant(),(c.Attr("VAlign")??"top").Trim().ToLowerInvariant(),c.TryInt("LineMargin")??0);
            switch(c.Name){
                case "Image":{
                    if(MultiVariant(name)){Skipped.Add(c.Path+"（multi 版，只建 screen 版）");return;}
                    var r=ImageRef(c,"Png")??ImageRef(c,"Surf");
                    if(r==null){Skipped.Add(c.Path+"（沒有圖號）");return;}
                    into.Add(Piece(ResetPieceKind.Image) with{Image=r,Surface=Resolve(r,c.Path)});return;
                }
                case "BoardImage":{
                    int w=c.Int("Width"),h=c.Int("Height");int alpha=c.TryInt("CustomAlpha")??255;
                    if(w<=0||h<=0||alpha is <0 or >255)throw new UiLayoutException(file,c.Path,"BoardImage 大小或 CustomAlpha 不合理");
                    into.Add(Piece(ResetPieceKind.Board) with{Color=ParseColor(c,"CustomColor",Color.Black),Alpha=alpha});return;
                }
                case "Label" or "MultiLabel":{
                    string? kind=c.Attr("Desc")!=null?"Desc":c.Attr("String")!=null?"String":null;
                    bool valued=c.Name=="MultiLabel"||name!=null&&(ValueLabel(name)||name=="Point"&&owner!=null);
                    if(!valued&&kind==null){Skipped.Add(c.Path+"（Caption 執行期才填）");return;}
                    into.Add(Piece(c.Name=="MultiLabel"?ResetPieceKind.MultiLabel:ResetPieceKind.Label) with{TextKind=kind,TextValue=kind==null?null:c.Attr(kind)!.Trim(),Color=ParseColor(c,"FontColor",Color.White)});
                    foreach(var k in c.Children)Skipped.Add(k.Path);
                    return;
                }
                case "Button":{
                    var normal=ImageRef(c,"BaseSurf")??ImageRef(c,"BasePng");var high=ImageRef(c,"HighlightSurf")??ImageRef(c,"HighlightPng");var push=ImageRef(c,"PushSurf")??ImageRef(c,"PushPng");
                    var ns=Resolve(normal,c.Path);
                    // Same picture as the normal one (6504 / 6506) is not looked up twice.
                    var hs=high==null?null:high.Id==normal?.Id&&high.IsSurf==normal.IsSurf?ns:Resolve(high,c.Path+"/@"+high.Attr);
                    var ps=push==null?null:Resolve(push,c.Path+"/@"+push.Attr);
                    into.Add(Piece(ResetPieceKind.Button) with{Image=normal,Surface=ns,Highlight=hs,Push=ps});
                    foreach(var k in c.Children)Skipped.Add(k.Path);
                    return;
                }
                case "Control":
                    if(name==SkippedControl){Skipped.Add(c.Path+"（Customizing_Win 能力加成表，引擎沒有數值，不建）");return;}
                    Walk(c,at,name!=null&&StatControls.ContainsKey(name)?name:owner,into);return;
                default:Skipped.Add(c.Path);return;
            }
        }
    }

    static bool ValueLabel(string name)=>name is "Level" or "HitPoint" or "ManaPoint" or "ArmorClass" or "Remain_Point";

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        Clear();
        var root=layout.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(layout.FileName,root.Path,$"根元素不是 {RootNode}");
        var b=new Builder(layout.FileName,surf,png);
        ResetWindow? main=null,message=null;
        foreach(var w in root.Children){
            string? name=w.Name=="Window"?w.Attr("Name"):null;
            if(name is not (MainWindow or MessageWindow)){b.Skipped.Add(w.Path);continue;}
            if(name==MainWindow&&main!=null||name==MessageWindow&&message!=null)throw new UiLayoutException(layout.FileName,w.Path,$"Window {name} 重複");
            var origin=new Point(w.TryInt("X")??throw new UiLayoutException(layout.FileName,w.Path,"Window 沒有 X"),w.TryInt("Y")??throw new UiLayoutException(layout.FileName,w.Path,"Window 沒有 Y"));
            var baseRef=ImageRef(w,"BaseSurf")??ImageRef(w,"BasePng");
            var baseSurface=b.Resolve(baseRef,w.Path+"/@"+baseRef?.Attr);
            var pieces=new List<ResetPiece>();
            b.Walk(w,Point.Empty,null,pieces);
            var back=pieces.FirstOrDefault(p=>p.Kind==ResetPieceKind.Image&&p.Name=="Back_Image"&&p.Offset==Point.Empty&&p.Surface!=null);
            var declared=new Size(w.TryInt("Width")??0,w.TryInt("Height")??0);
            var size=declared.Width>0&&declared.Height>0?declared:baseSurface!=null?new Size(baseSurface.Width,baseSurface.Height):back?.ImageSize??Size.Empty;
            var built=new ResetWindow(name,origin,size,baseSurface,pieces);
            if(name==MainWindow)main=built;else message=built;
        }
        if(main==null)throw new UiLayoutException(layout.FileName,root.Path,$"沒有 {MainWindow}");
        if(message==null)throw new UiLayoutException(layout.FileName,root.Path,$"沒有 {MessageWindow}（無法確認送出）");
        if(!StatControls.Keys.All(k=>main.Pieces.Any(p=>p.Owner==k&&p.Name=="Point")))throw new UiLayoutException(layout.FileName,root.Path,"六項能力的 Point 欄位不齊");
        LayoutFile=layout.FileName;Main=main;Message=message;Missing=b.Missing;Skipped=b.Skipped;
        Unavailable=main.Size.IsEmpty?$"{layout.FileName}: {MainWindow} 沒有大小":message.Size.IsEmpty?$"{layout.FileName}: {MessageWindow} 底圖都缺，無法定出確認框範圍":null;
    }

    // ---- state ------------------------------------------------------------------------------------------------------------

    /// <summary>Opens the window for the reset the host currently offers. false when unavailable or nothing to show.</summary>
    public bool Begin(IResetStatHost host){
        if(!Available)return false;
        var v=host.Snapshot();
        Host=host;generation=v.Generation;epoch=v.Epoch;revision=-1;Stage=0;Error=null;Busy=false;Shown=ResetMessage.None;
        pressed=null;hover=null;pressedInside=false;open=true;
        Sync();
        return open;
    }
    public void Close(){open=false;Shown=ResetMessage.None;pressed=null;hover=null;pressedInside=false;}

    /// <summary>Same rules as CharResetUi.Sync: a new login generation or a new world epoch (outside "initial") ends the window.</summary>
    public void Sync(){
        if(!open||Host==null)return;
        var v=Host.Snapshot();
        if(v.Generation==generation&&v.Phase=="initial")epoch=v.Epoch;
        if(v.Generation!=generation||v.Epoch!=epoch){Close();return;}
        View=v;
        if(revision!=v.Revision){draft=v.Stats.Values;revision=v.Revision;Error=null;}
        Stage=v.Phase switch{"initial"=>0,"level"=>1,"elixir" or "finishing" or "saving" or "review"=>2,_=>Stage};
        if(!Ready&&Shown==ResetMessage.Confirm)Shown=ResetMessage.None;
    }

    public void Update(IUiGameState state)=>Sync();

    bool Ready=>View is {CanAct:true}&&!Busy;
    bool Edit=>View?.Phase is "initial" or "elixir";
    bool Active=>View?.Phase is not (null or "idle" or "ended");
    /// <summary>Points still to place (edit phases) or level-up points left (level phase).</summary>
    public int Remaining=>View is not {} v?0:Edit?v.Points-(Draft.Sum-v.Stats.Sum):v.Points;
    public bool CanUp(int i)=>Ready&&View is {} v&&(Edit?Remaining>0&&draft[i]<MaxStat:v.Phase=="level"&&v.Level>=50&&v.Level<v.MaxLevel&&v.Stats.Values[i]<MaxStat);
    public bool CanDown(int i)=>Ready&&Edit&&draft[i]>View!.Stats.Values[i];
    public bool CanLevel1=>Ready&&View is {Phase:"level"} v&&v.Level<v.MaxLevel&&v.Level<50;
    public bool CanLevel10=>Ready&&View is {Phase:"level"} v&&v.Level+10<=Math.Min(50,v.MaxLevel);
    public bool CanOk=>View is {} v&&(!Active||Ready&&(v.Phase=="level"?v.Level==v.MaxLevel:Edit&&Remaining==0&&draft.Zip(v.Stats.Values).All(p=>p.First>=p.Second&&p.First<=MaxStat)));

    bool Enabled(ResetPiece p){
        if(p.MouseEvent is MsgOkEvent or MsgCancelEvent)return Shown!=ResetMessage.None;
        if(Shown!=ResetMessage.None)return false;
        int stat=p.Owner!=null&&StatControls.TryGetValue(p.Owner,out var i)?i:-1;
        return p.MouseEvent switch{
            UpEvent=>stat>=0&&CanUp(stat),DownEvent=>stat>=0&&CanDown(stat),Level1Event=>CanLevel1,Level10Event=>CanLevel10,OkEvent=>CanOk,_=>true,
        };
    }

    /// <summary>Progress images: "_On" while the stage is reached, "_Off" otherwise; other pieces always.</summary>
    public bool ShownPiece(ResetPiece p){
        if(p.Name is not {} n||!n.StartsWith("Progress_",StringComparison.Ordinal))return true;
        bool on=n.EndsWith("_On",StringComparison.Ordinal);
        if(!on&&!n.EndsWith("_Off",StringComparison.Ordinal))return true;
        string key=n["Progress_".Length..n.LastIndexOf('_')];
        int need=key switch{"Arrow1"=>1,"Arrow2"=>2,_=>Array.IndexOf(Stages,key)};
        if(need<0)return true;
        return on==(Stage>=need);
    }

    public string? LabelText(ResetPiece p){
        if(p.Kind==ResetPieceKind.MultiLabel)return p.Name=="Message_Label"?Shown switch{ResetMessage.Confirm=>ConfirmText,ResetMessage.Leave=>LeaveText,_=>null}:MessageText;
        if(p.Kind!=ResetPieceKind.Label)return null;
        if(View is {} v){
            string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
            switch(p.Name){
                case "Level":return N(v.Level);
                case "HitPoint":return N(v.Hp);
                case "ManaPoint":return N(v.Mp);
                case "ArmorClass":return N(v.Ac);
                case "Remain_Point":return N(Remaining);
                case "Point" when p.Owner!=null&&StatControls.TryGetValue(p.Owner,out int i):return N(draft[i]);
            }
        }
        return p.TextKind==null?null:TextResolver?.Invoke(p.TextKind,p.TextValue!);
    }
    string? MessageText=>Error??(View is not {} v?null:v.Notice+(v.OriginalBonus is {} bonus?"\n出生加點（原服資訊）："+string.Join("／",bonus.Values):""));

    // ---- painting ---------------------------------------------------------------------------------------------------------

    public void Paint(UiCanvas canvas){
        if(!open||Main==null)return;
        PaintWindow(canvas,Main);
        if(Shown!=ResetMessage.None&&Message!=null)PaintWindow(canvas,Message);
    }

    void PaintWindow(UiCanvas canvas,ResetWindow w){
        canvas.DrawSurface(w.Base,w.Origin.X,w.Origin.Y);
        for(int i=w.Pieces.Count-1;i>=0;i--)if(ShownPiece(w.Pieces[i]))PaintPiece(canvas,w,w.Pieces[i]);
    }

    void PaintPiece(UiCanvas canvas,ResetWindow w,ResetPiece p){
        int x=w.Origin.X+p.Offset.X,y=w.Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case ResetPieceKind.Image:canvas.DrawSurface(p.Surface,x,y);break;
            case ResetPieceKind.Board:{
                using var brush=new SolidBrush(Color.FromArgb(p.Alpha,p.Color));
                canvas.Graphics.FillRectangle(brush,x,y,p.DeclaredSize.Width,p.DeclaredSize.Height);break;
            }
            case ResetPieceKind.Label:{
                if(LabelText(p) is not {Length:>0} text)break;
                var size=UiText.Measure(canvas.Graphics,text);
                canvas.DrawText(text,AlignX(p,x,size.Width),AlignY(p,y,size.Height),p.Color);break;
            }
            case ResetPieceKind.MultiLabel:{
                if(LabelText(p) is not {Length:>0} text)break;
                var lines=Wrap(canvas.Graphics,text,p.DeclaredSize.Width);
                int lh=UiText.Measure(canvas.Graphics,"國").Height+p.LineMargin;
                int ty=AlignY(p,y,lines.Count*lh-p.LineMargin);
                foreach(var line in lines){if(line.Length>0)canvas.DrawText(line,AlignX(p,x,UiText.Measure(canvas.Graphics,line).Width),ty,p.Color);ty+=lh;}
                break;
            }
            case ResetPieceKind.Button:{
                bool enabled=Enabled(p);
                var s=p==pressed&&pressedInside&&enabled&&p.Push!=null?p.Push:p==hover&&pressed==null&&enabled&&p.Highlight!=null?p.Highlight:p.Surface;
                canvas.DrawSurface(s,x,y);break;
            }
        }
    }
    static int AlignX(ResetPiece p,int x,int width)=>p.DeclaredSize.Width<=0?x:p.Align switch{"center"=>x+(p.DeclaredSize.Width-width)/2,"right"=>x+p.DeclaredSize.Width-width,_=>x};
    static int AlignY(ResetPiece p,int y,int height)=>p.DeclaredSize.Height<=0?y:p.VAlign switch{"center" or "middle"=>y+(p.DeclaredSize.Height-height)/2,"bottom"=>y+p.DeclaredSize.Height-height,_=>y};

    /// <summary>Splits on '\n', then breaks each line at the last character that still fits the width (0 = no limit).</summary>
    internal static List<string> Wrap(Graphics g,string text,int width){
        var lines=new List<string>();
        foreach(var raw in text.Replace("\r","").Split('\n')){
            if(width<=0||raw.Length==0){lines.Add(raw);continue;}
            int start=0;
            while(start<raw.Length){
                int end=start+1;
                while(end<raw.Length&&UiText.Measure(g,raw[start..(end+1)]).Width<=width)end++;
                lines.Add(raw[start..end]);start=end;
            }
        }
        return lines;
    }

    // ---- input ------------------------------------------------------------------------------------------------------------

    ResetWindow? InputWindow=>Shown!=ResetMessage.None?Message:Main;
    public Rectangle At(ResetWindow w,ResetPiece p)=>new(w.Origin.X+p.HitLocal.X,w.Origin.Y+p.HitLocal.Y,p.HitLocal.Width,p.HitLocal.Height);
    /// <summary>Topmost button at a logical point: XML order (drawn last = first in XML). Only the message box while it is shown.</summary>
    public ResetPiece? ButtonAt(Point location){
        if(!open||InputWindow is not {} w)return null;
        return w.Pieces.FirstOrDefault(p=>p.Kind==ResetPieceKind.Button&&ShownPiece(p)&&!p.HitLocal.IsEmpty&&At(w,p).Contains(location));
    }

    /// <summary>Modal: every mouse event goes to this window while it is open.</summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        if(e.Kind==UiMouseKind.Move&&pressed==null){hover=ButtonAt(e.Location);return true;}
        if(e.Kind==UiMouseKind.Wheel||e.Button!=MouseButtons.Left)return true;
        var w=InputWindow!;
        switch(e.Kind){
            case UiMouseKind.Down:pressed=ButtonAt(e.Location);pressedInside=pressed!=null;return true;
            case UiMouseKind.Move:pressedInside=pressed!=null&&At(w,pressed).Contains(e.Location);return true;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;pressedInside=false;
                if(was!=null&&At(w,was).Contains(e.Location)&&Enabled(was))Fire(was,commands);
                return true;
            }
        }
        return true;
    }

    /// <summary>Modal: all keys stay here. Esc = message Cancel, or leave (prompt while a reset is running).</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!open)return false;
        if(e.Down&&!e.Repeat&&e.KeyCode==Keys.Escape){
            if(Shown!=ResetMessage.None)Shown=ResetMessage.None;
            else if(!Active)Finish(commands);
            else Shown=ResetMessage.Leave;
        }
        return true;
    }

    void Finish(IUiCommands commands){Close();commands.ReturnGameFocus();}

    void Fire(ResetPiece p,IUiCommands commands){
        var v=View;
        int stat=p.Owner!=null&&StatControls.TryGetValue(p.Owner,out var i)?i:-1;
        switch(p.MouseEvent){
            case UpEvent when stat>=0&&v!=null:
                if(Edit)draft[stat]++;
                else _=Run("level",stat+1);
                return;
            case DownEvent when stat>=0:draft[stat]--;return;
            case Level1Event:_=Run("level",0);return;
            case Level10Event:_=Run("level",7);return;
            case OkEvent when v!=null:
                if(!Active){Finish(commands);return;}
                confirmStamp=v;confirmStats=Draft;Shown=ResetMessage.Confirm;return;
            case MsgOkEvent:{
                var was=Shown;Shown=ResetMessage.None;
                if(was==ResetMessage.Leave){Host?.Cancel();Finish(commands);return;}
                if(was==ResetMessage.Confirm&&confirmStamp is {} stamp){
                    var cmd=stamp.Phase=="level"?"finish":stamp.Phase;
                    _=Run(cmd,0,stamp,confirmStats);
                }
                return;
            }
            case MsgCancelEvent:Shown=ResetMessage.None;return;
        }
        string name=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(name))UiResourceLog.Write($"reset-stat-ui {name} 尚未接上");
    }

    /// <summary>One send through the host (same legality checks as the WinForms dialog); a refusal is shown in Msg_Label.</summary>
    internal async Task Run(string command,int step,CharResetView? stamp=null,ResetStats? stats=null){
        if(Busy||Host is not {} host||View is not {} v)return;
        Busy=true;Error=null;
        bool sent;
        try{sent=await host.Send(stamp??v,command,stats??Draft,step);}
        catch(Exception ex){sent=false;UiResourceLog.Write($"reset-stat-ui 送出 {command} 失敗：{ex.GetType().Name}: {ex.Message}");}
        Busy=false;
        if(!open)return;
        Sync();
        if(!sent&&open)Error=RejectedText;
    }
}
