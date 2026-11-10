using System.Globalization;
namespace P06.NativeUi;

// T328 (#766, P-D of docs/reference/pak-resources.md §6, row 4:RenewalStatusUI): the native status window CRenewalStatusUI.
// TW16 reads ReNStatusUIEx.xml (WIN§0.1, VA 0x119CAB0); 0-客戶端 does not ship it (PAK§4.2: all paks checked; the local
// ReNStatusWinUI(-c).xml is not referenced by TW16, WIN§1.4). It is read at run time from the reference client through
// UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak, "-c" first; SHA-256 logged as
// "ui-xml-ref"). Nothing is copied into the repository.
//
// Loading. Same as OptionPanel (#768): registered on the host layout "uistatus86" (UiRoot.Build is all-or-nothing) and Load
// reads ReNStatusUIEx itself. No reference client / no XML / malformed XML → Unavailable, the window never opens, the rest
// of the native UI keeps working, and the WinForms bonus-stats panel (BonusStatsUi.cs) and --classic-ui are unchanged.
//
// Images. Surfs (Close 5902/5903, tabs 5366–5369, slot frame 5479, open-slot 5376/5377/6295, alignment 2455) go through
// SurfCatalog.Get (local "Fc.img" first — 5902c/5903c.img exist locally, issue-758 — then the T322/#760 reference
// fallback). Pngs (BasePng 10910/10903, gauges 10898–10900, stat buttons 10901–10909) go through OptionPngSource (local
// Sprite archives, then the reference Sprite*.idx/.pak; SHA-256 logged as "option-png"). Missing images are not drawn (no
// substitute) and are listed in Missing. No .tbt is used as a surf (T320/#758: TW16 surfs never read .tbt).
//
// Layout rules (推測; the CRenewalStatusUI binder sub_EDCBE0 was not decompiled for this):
// - The root holds Windows. Equip_Window and Status_Window are the two tab pages, shown one at a time at the same origin;
//   the first page in XML order is shown on open. KarmaTooltip_Window is a tooltip window: not built (listed in Skipped).
// - Coordinates are relative to the parent element, a missing X/Y is 0. The page size is its BasePng image; a page whose
//   BasePng is missing borrows the other page's size; no size at all → Unavailable. Moveable="20" = top 20 px drag.
// - A List with Horizontal="1" places its children left to right from the List origin, each advancing by its normal image
//   width. RadioButton "<X>Tab_Button" (MouseEvent ChangeTab) shows page "<X>_Window". DisableSurf is not used.
// - ItemIcon = an equipment slot: kept in Slots (rect only); the equipped item icons are not drawn (no slot data yet, 待做).
// - Labels named in ValueLabels show the character's values (OwnStatus, via RenewalStatusView); the design-time Caption is
//   never drawn. A value the engine does not have (elixir, magic level/bonus, DG/ER/ME, remaining slot time) is left blank.
//   Labels with only String/Desc ids go through TextResolver (null in the product: the reference ids would not match the
//   local string-c.tbl, see #768). HP/MP labels are "cur/max" and the level label is the level number (format 推測).
// - Gauges HpGauge/MpGauge draw their png cropped to cur/max width; LevGauge is not drawn (exp % is not known here).
// - Stat Up_Button is shown (normal / push while held) only while bonus points can be chosen (RenewalStatusView.CanRaise);
//   a click calls StatPointUp with the attribute key ("str"…). Down_Button is never shown (the engine has no "undo" step).
// - Tooltip elements are not built. ShowStatInfoTooltip toggles the check image; StatInfoPanel (#763) follows it. Karma not wired.
//
// Entry (R7-I, #811; FU4§1.1 TW13 off_8F023C／sub_4D3910): menu button 2 and Ctrl+A toggle this window through
// UiRoot.Toggle (MenuButtonsPanel.Invoke → Open). Unavailable (no reference XML / faulted) → the dev-tools 「角色」 page,
// as before. --classic-ui has no native root: the WinForms bonus-stats panel is the only entry, unchanged.

/// <summary>Values shown by the window; null = the server never sent it / the engine does not know it (drawn blank).</summary>
public sealed record RenewalStatusView(int? Level,long? Hp,long? MaxHp,long? Mp,long? MaxMp,int? Str,int? Int,int? Dex,int? Wis,int? Con,int? Cha,
    int? Lawful,int? EarthResist,int? FireResist,int? WaterResist,int? WindResist,long? PkCount,bool CanRaise=false,string? Selected=null) {
    public static readonly RenewalStatusView Empty=new(null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null);
}

public enum StatusPieceKind { Image, Gauge, Label, Button, Slot }

/// <summary>
/// One element of a page, in XML order. Offset is relative to the window origin. Surface null = missing image (not drawn).
/// Owner = the enclosing Label name for the stat Up/Down buttons. Active=false (Activate="0") is not drawn and not hit.
/// </summary>
public sealed record StatusPiece(StatusPieceKind Kind,string Path,string Element,string? Name,Point Offset,Size DeclaredSize,
    OptionImageRef? Image,UiSurface? Surface,OptionImageRef? PushImage,UiSurface? PushSurface,string? MouseEvent,
    string? TextKind,string? TextValue,Color Color,string? Owner=null,bool Active=true,string HAlign="left",string VAlign="top") {
    public Size ImageSize=>Surface==null?Size.Empty:new(Surface.Width,Surface.Height);
    public Rectangle HitLocal=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
}

/// <summary>A tab page (Equip_Window, Status_Window). Size = BasePng image (or borrowed, see the file header).</summary>
public sealed record StatusPage(string Name,StatusPiece? BaseImage,Size Size,int MoveableHeight,IReadOnlyList<StatusPiece> Pieces);

public sealed class RenewalStatusPanel(UiResources? resources=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(179)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new RenewalStatusPanel(root.Resources){TextResolver=RefClientTextSource.Resolver(root.Resources)});   // T328 (#766): ReNStatusUIEx.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="RenewalStatusUI";
    public const string LayoutName="ReNStatusUIEx",RootNode="RenewalStatusUI",WindowNode="Window",TooltipWindow="KarmaTooltip_Window";
    public const string CloseEvent="CloseWindow",TabEvent="ChangeTab",StatUpEvent="StatPointUP",StatDownEvent="StatPointDown",TabSuffix="Tab_Button",PageSuffix="_Window";
    /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
    public const string HostLayout="uistatus86";

    /// <summary>Stat label → BonusStatsRules key (the six of C_Attr, RaiseAttr-c.html).</summary>
    public static readonly IReadOnlyDictionary<string,string> StatLabels=new Dictionary<string,string>(StringComparer.Ordinal){
        ["Str_Label"]="str",["Dex_Label"]="dex",["Con_Label"]="con",["Int_Label"]="int",["Wis_Label"]="wis",["Cha_Label"]="cha",
    };
    static string? N(long? v)=>v?.ToString(CultureInfo.InvariantCulture);
    static string? Pair(long? a,long? b)=>a==null||b==null?null:$"{a}/{b}";
    /// <summary>Label name → runtime text (null = not known, drawn blank). Labels not listed here never draw their Caption.</summary>
    public static readonly IReadOnlyDictionary<string,Func<RenewalStatusView,string?>> ValueLabels=new Dictionary<string,Func<RenewalStatusView,string?>>(StringComparer.Ordinal){
        ["LevGauge_Label"]=v=>N(v.Level),["HpGauge_Label"]=v=>Pair(v.Hp,v.MaxHp),["MpGauge_Label"]=v=>Pair(v.Mp,v.MaxMp),
        ["Str_Label"]=v=>N(v.Str),["Int_Label"]=v=>N(v.Int),["Dex_Label"]=v=>N(v.Dex),["Wis_Label"]=v=>N(v.Wis),["Con_Label"]=v=>N(v.Con),["Cha_Label"]=v=>N(v.Cha),
        ["Align_Label"]=v=>N(v.Lawful),["PkCount_Label"]=v=>N(v.PkCount),
        ["EarthResist_Label"]=v=>N(v.EarthResist),["FireResist_Label"]=v=>N(v.FireResist),["WaterResist_Label"]=v=>N(v.WaterResist),["WindResist_Label"]=v=>N(v.WindResist),
        ["Elixir_Label"]=_=>null,["MagicLevel_Label"]=_=>null,["MagicBonus_Label"]=_=>null,["DG_Label"]=_=>null,["ER_Label"]=_=>null,["ME_Label"]=_=>null,
        ["RemainTimeSlot59_Label"]=_=>null,["RemainTimeSlot76_Label"]=_=>null,["RemainTimeSlot81_Label"]=_=>null,
    };
    /// <summary>Gauge name → fill ratio (null = not drawn).</summary>
    public static double? GaugeRatio(string? name,RenewalStatusView v){
        static double? R(long? a,long? b)=>a==null||b is null or <=0?null:Math.Clamp((double)a.Value/b.Value,0,1);
        return name switch{"HpGauge_Image"=>R(v.Hp,v.MaxHp),"MpGauge_Image"=>R(v.Mp,v.MaxMp),_=>null};
    }

    /// <summary>
    /// S_OwnCharStatus snapshot → window values (null status = everything blank). PkCount stays null: the last dword of the
    /// packet (OwnStatus.Kills) is not verified to be the PK count, so it is not shown (待驗證).
    /// </summary>
    public static RenewalStatusView ViewOf(OwnStatus? s,bool canRaise=false,string? selected=null)=>s==null?RenewalStatusView.Empty with{CanRaise=canRaise,Selected=selected}
        :new(s.Level,s.Hp,s.MaxHp,s.Mp,s.MaxMp,s.Strength,s.Intelligence,s.Dexterity,s.Wisdom,s.Constitution,s.Charisma,
            s.Lawful,s.Earth,s.Fire,s.Water,s.Wind,null,canRaise,selected);

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open (no XML, malformed XML, no size); null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size=>ActivePage?.Size??Pages.FirstOrDefault()?.Size??Size.Empty;
    public Rectangle Bounds=>Size.IsEmpty?Rectangle.Empty:new(Origin,Size);
    public bool Visible=>open;
    public IReadOnlyList<StatusPage> Pages{get;private set;}=[];
    public StatusPage? ActivePage{get;private set;}
    /// <summary>Equipment slots (ItemIcon) of all pages: name and window-relative rect. Item icons are not drawn yet.</summary>
    public IReadOnlyList<StatusPiece> Slots{get;private set;}=[];
    /// <summary>Images named in the XML but not found (not drawn): "surf 5902 (RenewalStatusUI/Window[0]/Button[0])".</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>Elements deliberately not built (tooltips, the karma tooltip window, unknown names), by path.</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public RenewalStatusView View{get;private set;}=RenewalStatusView.Empty;
    /// <summary>(kind "Desc"/"String", id) → text; null result or null resolver = the label is not drawn.</summary>
    public Func<string,string,string?>? TextResolver{get;set;}
    /// <summary>Stat Up_Button clicked: the BonusStatsRules key. Set by ClientForm (BonusStatsUi.cs); null = nothing happens.</summary>
    public Action<string>? StatPointUp{get;set;}
    /// <summary>StatusBonus_Button check state (drawn with its push image; StatInfoUI itself is not built).</summary>
    public bool StatInfoChecked{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    bool open;StatusPiece? pressed;bool pressedInside;readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"status-ui 無法載入 {LayoutName}，狀態窗停用（WinForms 能力點面板照舊）：{ex.Message}");return;}
        var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
        try{Load(layout,surfs.Get,png.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"status-ui {layout.FileName} 版面不符，狀態窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"status-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"status-ui {Unavailable}");
    }

    void Clear(){
        open=false;pressed=null;pressedInside=false;LayoutFile=null;Origin=Point.Empty;Pages=[];ActivePage=null;Slots=[];Missing=[];Skipped=[];
        StatInfoChecked=false;Unavailable="not loaded";
    }

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        Clear();
        var root=layout.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(layout.FileName,root.Path,$"根元素不是 {RootNode}");
        var missing=new List<string>();var skipped=new List<string>();var slots=new List<StatusPiece>();
        UiSurface? Resolve(OptionImageRef? r,string path){
            if(r==null)return null;
            var s=r.IsSurf?surf(r.Id):png(r.Id);
            if(s==null)missing.Add($"{r} ({path})");
            return s;
        }
        var built=new List<(string Name,StatusPiece? Base,int Moveable,List<StatusPiece> Pieces)>();
        foreach(var w in root.Children){
            if(w.Name!=WindowNode){skipped.Add(w.Path);continue;}
            string name=w.Attr("Name")??throw new UiLayoutException(layout.FileName,w.Path,"Window 沒有 Name");
            if(name==TooltipWindow){skipped.Add(w.Path+"（Karma 提示窗，不建）");continue;}
            if(built.Any(p=>p.Name==name))throw new UiLayoutException(layout.FileName,w.Path,$"Window {name} 重複");
            var baseRef=OptionPanel.ImageAttr(w,"BasePng")??OptionPanel.ImageAttr(w,"BaseSurf");
            var basePiece=baseRef==null?null:Piece(StatusPieceKind.Image,w,Point.Empty,baseRef,Resolve(baseRef,w.Path+"/@"+baseRef.Attr));
            var pieces=new List<StatusPiece>();
            Walk(w,Point.Empty,null,pieces,slots,skipped,Resolve);
            built.Add((name,basePiece,w.TryInt("Moveable")??0,pieces));
        }
        if(built.Count==0)throw new UiLayoutException(layout.FileName,root.Path,"沒有任何分頁 Window");
        var fallback=built.Select(b=>b.Base?.ImageSize??Size.Empty).FirstOrDefault(s=>!s.IsEmpty);
        Pages=built.Select(b=>new StatusPage(b.Name,b.Base,b.Base?.Surface!=null?b.Base.ImageSize:fallback,b.Moveable,b.Pieces)).ToArray();
        var first=root.ChildrenNamed(WindowNode).FirstOrDefault(w=>w.Attr("Name")==Pages[0].Name);
        LayoutFile=layout.FileName;Origin=first==null?Point.Empty:new(first.TryInt("X")??0,first.TryInt("Y")??0);
        Slots=slots;Missing=missing;Skipped=skipped;
        Unavailable=fallback.IsEmpty?$"{layout.FileName}: 所有分頁底圖都缺，無法定出視窗範圍":null;
    }

    static Point Rel(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));
    static Color ParseColor(UiNode n,string attr,Color fallback){
        var raw=n.Attr(attr);if(raw==null)return fallback;
        var parts=raw.Split(',');
        if(parts.Length!=3||!parts.All(p=>byte.TryParse(p.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _)))
            throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 r,g,b");
        return Color.FromArgb(byte.Parse(parts[0].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[1].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[2].Trim(),CultureInfo.InvariantCulture));
    }
    static OptionImageRef? FirstImage(UiNode n,params string[] attrs){foreach(var a in attrs)if(OptionPanel.ImageAttr(n,a) is {} r)return r;return null;}
    static StatusPiece Piece(StatusPieceKind kind,UiNode n,Point at,OptionImageRef? image=null,UiSurface? surface=null,OptionImageRef? push=null,UiSurface? pushSurface=null,string? owner=null)
        =>new(kind,n.Path,n.Name,n.Attr("Name"),at,new(n.TryInt("Width")??0,n.TryInt("Height")??0),image,surface,push,pushSurface,n.Attr("MouseEvent"),null,null,Color.Empty,owner,
            n.Attr("Activate")?.Trim()!="0",(n.Attr("HAlign")??n.Attr("Align")??"left").Trim().ToLowerInvariant(),(n.Attr("VAlign")??"top").Trim().ToLowerInvariant());

    static void Walk(UiNode parent,Point origin,string? owner,List<StatusPiece> into,List<StatusPiece> slots,List<string> skipped,Func<OptionImageRef?,string,UiSurface?> resolve){
        foreach(var c in parent.Children)Element(c,Rel(c,origin),owner,into,slots,skipped,resolve);
    }

    static void Element(UiNode c,Point at,string? owner,List<StatusPiece> into,List<StatusPiece> slots,List<string> skipped,Func<OptionImageRef?,string,UiSurface?> resolve){
        switch(c.Name){
            case "Image" or "Gauge":{
                var r=FirstImage(c,"Png","Surf","BaseSurf");
                if(r==null){skipped.Add(c.Path+"（沒有圖號）");return;}
                into.Add(Piece(c.Name=="Gauge"?StatusPieceKind.Gauge:StatusPieceKind.Image,c,at,r,resolve(r,c.Path)));return;
            }
            case "ItemIcon":
                slots.Add(Piece(StatusPieceKind.Slot,c,at));
                foreach(var k in c.Children)skipped.Add(k.Path);
                return;
            case "Label":{
                string? kind=c.Attr("Desc")!=null?"Desc":c.Attr("String")!=null?"String":null;
                string? name=c.Attr("Name");
                bool valued=name!=null&&ValueLabels.ContainsKey(name);
                var color=ParseColor(c,"FontColor",Color.White);
                if(!valued&&kind==null)skipped.Add(c.Path+"（Caption 執行期才填）");
                else into.Add(Piece(StatusPieceKind.Label,c,at) with{TextKind=kind,TextValue=kind==null?null:c.Attr(kind)!.Trim(),Color=color});
                // Stat labels carry their Up/Down buttons; positions are relative to the label.
                foreach(var k in c.Children){
                    if(k.Name is "Button")Element(k,Rel(k,at),name,into,slots,skipped,resolve);
                    else skipped.Add(k.Path);
                }
                return;
            }
            case "Button" or "CheckButton" or "RadioButton":{
                var normal=FirstImage(c,"BasePng","BaseSurf");var push=FirstImage(c,"PushPng","PushSurf");
                into.Add(Piece(StatusPieceKind.Button,c,at,normal,resolve(normal,c.Path),push,push==null?null:resolve(push,c.Path+"/@"+push.Attr),owner));
                foreach(var k in c.Children)skipped.Add(k.Path);
                return;
            }
            case "List":{
                bool horizontal=c.Attr("Horizontal")?.Trim()=="1";
                int advance=0;
                foreach(var k in c.Children){
                    var start=horizontal?new Point(at.X+advance,at.Y):at;
                    int before=into.Count;
                    Element(k,Rel(k,start),owner,into,slots,skipped,resolve);
                    if(horizontal&&into.Count>before)advance+=into[before].ImageSize.Width;
                }
                return;
            }
            case "Control":Walk(c,at,owner,into,slots,skipped,resolve);return;
            default:skipped.Add(c.Path);return;
        }
    }

    /// <summary>ClientForm (BonusStatsUi.cs) pushes the latest values; a pressed Up button is dropped when raising is no longer possible.</summary>
    public void Show(RenewalStatusView view){
        View=view;
        if(!view.CanRaise&&pressed is {MouseEvent:StatUpEvent}){pressed=null;pressedInside=false;}
    }

    public void Update(IUiGameState state){if(!state.InWorld){if(open)Close();View=RenewalStatusView.Empty;}}

    /// <summary>Opens on the first page. false when the layout is unavailable.</summary>
    public bool Open(){
        if(!Available)return false;
        open=true;pressed=null;pressedInside=false;ActivePage=Pages[0];
        return true;
    }
    public void Close(){open=false;pressed=null;pressedInside=false;}
    public bool Toggle(){if(open){Close();return true;}return Open();}

    /// <summary>Page a tab button selects ("StatusTab_Button" → "Status_Window"), or null.</summary>
    public StatusPage? TabTarget(StatusPiece p){
        if(p.Kind!=StatusPieceKind.Button||p.Name is not {} n||!n.EndsWith(TabSuffix,StringComparison.Ordinal))return null;
        string page=n[..^TabSuffix.Length]+PageSuffix;
        return Pages.FirstOrDefault(pg=>pg.Name==page);
    }
    public bool ShowPage(string name){
        if(!open||Pages.FirstOrDefault(p=>p.Name==name) is not {} page)return false;
        ActivePage=page;pressed=null;pressedInside=false;return true;
    }

    /// <summary>Stat Up buttons only while raising is possible; Down buttons never (see the file header).</summary>
    public bool Shown(StatusPiece p){
        if(!p.Active)return false;
        if(p.Kind!=StatusPieceKind.Button)return true;
        return p.MouseEvent switch{StatUpEvent=>View.CanRaise&&p.Owner!=null&&StatLabels.ContainsKey(p.Owner),StatDownEvent=>false,_=>true};
    }
    IEnumerable<StatusPiece> Live=>ActivePage?.Pieces.Where(Shown)??[];
    public Rectangle At(StatusPiece p)=>new(Origin.X+p.HitLocal.X,Origin.Y+p.HitLocal.Y,p.HitLocal.Width,p.HitLocal.Height);
    /// <summary>Topmost clickable piece at a logical point (later in XML order = drawn on top).</summary>
    public StatusPiece? ButtonAt(Point location)=>open?Live.Where(p=>p.Kind==StatusPieceKind.Button&&p.HitLocal.Width>0&&p.HitLocal.Height>0).LastOrDefault(p=>At(p).Contains(location)):null;

    /// <summary>The text a label draws now: its runtime value, or the resolved String/Desc id; null = nothing.</summary>
    public string? LabelText(StatusPiece p){
        if(p.Kind!=StatusPieceKind.Label)return null;
        if(p.Name!=null&&ValueLabels.TryGetValue(p.Name,out var value))return value(View);
        return p.TextKind==null?null:TextResolver?.Invoke(p.TextKind,p.TextValue!);
    }

    public void Paint(UiCanvas canvas){
        if(!open||ActivePage==null)return;
        if(ActivePage.BaseImage?.Surface is {} b)canvas.DrawSurface(b,Origin.X,Origin.Y);
        foreach(var p in Live)PaintPiece(canvas,p);
    }

    void PaintPiece(UiCanvas canvas,StatusPiece p){
        int x=Origin.X+p.Offset.X,y=Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case StatusPieceKind.Image:canvas.DrawSurface(p.Surface,x,y);break;
            case StatusPieceKind.Gauge:
                if(p.Surface is {} g&&GaugeRatio(p.Name,View) is double r){
                    int w=(int)Math.Round(g.Width*r);
                    if(w>0)canvas.DrawSurface(g,new Rectangle(0,0,w,g.Height),x,y);
                }
                break;
            case StatusPieceKind.Label:{
                if(LabelText(p) is not {Length:>0} text)break;
                var size=UiText.Measure(canvas.Graphics,text);
                int tx=p.DeclaredSize.Width<=0?x:p.HAlign switch{"center"=>x+(p.DeclaredSize.Width-size.Width)/2,"right"=>x+p.DeclaredSize.Width-size.Width,_=>x};
                int ty=p.DeclaredSize.Height<=0?y:p.VAlign switch{"middle"=>y+(p.DeclaredSize.Height-size.Height)/2,"bottom"=>y+p.DeclaredSize.Height-size.Height,_=>y};
                canvas.DrawText(text,tx,ty,p.Color);
                break;
            }
            case StatusPieceKind.Button:{
                bool held=p==pressed&&pressedInside;
                bool on=TabTarget(p) is {} t&&t==ActivePage||p.Element=="CheckButton"&&StatInfoChecked;
                canvas.DrawSurface((held||on)&&p.PushSurface!=null?p.PushSurface:p.Surface,x,y);
                break;
            }
        }
    }

    public bool IsDragHandle(Point location)=>open&&ActivePage!=null&&Bounds.Contains(location)&&location.Y<Origin.Y+ActivePage.MoveableHeight&&ButtonAt(location)==null;
    public void MoveTo(Point location)=>Origin=location;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel||e.Button!=MouseButtons.Left)return inside||pressed!=null;
        switch(e.Kind){
            case UiMouseKind.Down:
                pressed=ButtonAt(e.Location);pressedInside=pressed!=null;
                return inside;
            case UiMouseKind.Move:
                if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                return inside||pressed!=null;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;pressedInside=false;
                if(was!=null&&At(was).Contains(e.Location)&&Shown(was))Fire(was);
                return inside||was!=null;
            }
            default:return inside;
        }
    }

    void Fire(StatusPiece p){
        if(p.MouseEvent==CloseEvent){Close();return;}
        if(TabTarget(p) is {} page){ActivePage=page;return;}
        if(p.MouseEvent==StatUpEvent&&p.Owner!=null&&StatLabels.TryGetValue(p.Owner,out var key)){StatPointUp?.Invoke(key);return;}
        if(p.Element=="CheckButton")StatInfoChecked=!StatInfoChecked;
        string name=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(name))UiResourceLog.Write($"status-ui {name} 尚未接上");
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
