using System.Globalization;
using System.Text;
namespace P06.NativeUi;

// T324 (#762, P-D of docs/reference/pak-resources.md §6, row 1:155 S_READ_MAIL / 1:99 S_MAIL_INFO / 3:C_MAIL / 4:RenewalMailUI):
// the native mail windows. TW16 reads RenewalMailUI.xml; 0-客戶端 does not ship it (PAK§4.2: all paks checked). It is read at
// run time from the reference client through UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference
// Tile.pak, "-c" first; SHA-256 logged as "ui-xml-ref"). Nothing is copied into the repository.
//
// Loading. Same as RenewalStatusPanel (#766): registered on the host layout "uistatus86" (UiRoot.Build is all-or-nothing) and
// Load reads RenewalMailUI itself. No reference client / no XML / malformed XML → Unavailable, the window never opens,
// ClientForm.RequestMail keeps the WinForms MailDialog (MailUi.cs), and --classic-ui is unchanged.
//
// Images. Surfs (close 9183/9184 — in no local pak, served by the T322/#760 reference fallback; scroll 4444–4449; prev/next
// 5429–5433) go through SurfCatalog.Get. Pngs (window bases 10782/10811/10810, buttons 10783–10808, 10892–10894,
// 11180–11182, combo 10746–10751, scroll track 10729) go through OptionPngSource (local Sprite archives, then the reference
// Sprite*.idx/.pak; SHA-256 logged as "option-png"). Missing images are not drawn (no substitute) and are listed in Missing.
// HighlightPng/HighlightSurf (hover) and DisablePng are not used.
//
// Layout rules (推測; the CRenewalMailUI binder was not decompiled for this):
// - The root holds three Windows at the same origin, shown one at a time: MailList_Window (on open), ReadMail_Window
//   (ReadMailEvent on a row), WriteMail_Window (WriteMail / ReplyMail). CloseMailUI closes the panel; CloseReadMailUI and
//   CloseWriteMailUI return to the list. The window size is its Width/Height (else its base image); Moveable = top drag strip.
// - Coordinates are relative to the parent. A List places its children left→right (Horizontal="1") or top→down
//   (Vertical="1"), each advancing by its declared Width/Height (else image size) plus Gap. MailList's CheckButtons are the
//   mail rows (row i = i-th mail after scrolling); empty rows are not drawn. An UpDownList stacks its buttons the same way;
//   its Image is a background that does not advance. The ComboBox list is drawn only while expanded.
// - Include (CommunityTab.xml, the community tab strip) is not built (listed in Skipped).
// - Texts: rows show sender / subject / "yy/mm/dd"; Count_Label = number of mails; Cost_Label = postage (50 / clan 1000, the
//   values the WinForms dialog uses); combo items use String 4071–4073 through TextResolver, otherwise 一般／血盟／保管箱.
//   XML Captions ("IU", "item00") are design-time placeholders and never drawn.
// - Edits: LimitText counts UTF-16 units; the final size check is MailPackets.Write (same as the WinForms dialog). Printable
//   characters come through IUiTextInput.Char (WM_CHAR); Back/Tab/Enter/Esc/arrows are taken in Key while an edit has focus.
//   Esc (KeyboardEvent) = the window's close event.
// - Delete/Store act on the checked rows (list) or on the mail being read; there is no confirmation prompt. ExcludeMail
//   (MailExcludeUI) and the scroll thumb drag are not wired (待做). Server notices (MailView.Notice) are not drawn.

/// <summary>Bridge to the existing mail flow (ClientForm, MailUi.cs). The panel never touches LoginConnection.</summary>
public interface IMailPanelHost {
    /// <summary>Latest snapshot (WorldSession.MailSnapshot); null when not in the world.</summary>
    MailView? View { get; }
    /// <summary>LoginConnection.Mail with the stamp of the shown view ("list", "read", "delete", "store", "write", "clan").</summary>
    Task<bool> Run(MailView shown,string command,byte box,uint id,string receiver="",string subject="",string body="");
}

public enum MailPieceKind { Image, Label, Button, Edit }

/// <summary>
/// One element of a mail window, in XML order. Offset is relative to the window origin. Surface null = missing image (not
/// drawn). Group: "row" (Index = mail row), "combo" (expanded list; Index = item, -1 = background), "combo-text", "scroll".
/// </summary>
public sealed record MailPiece(MailPieceKind Kind,string Path,string Element,string? Name,Point Offset,Size DeclaredSize,
    OptionImageRef? Image,UiSurface? Surface,OptionImageRef? PushImage,UiSurface? PushSurface,string? MouseEvent,Color Color,
    string HAlign="left",string VAlign="top",string? Group=null,int Index=-1,string? TextKind=null,string? TextValue=null,
    int Limit=0,bool ReadOnly=false,bool Multiline=false) {
    public Size ImageSize=>Surface==null?Size.Empty:new(Surface.Width,Surface.Height);
    public Rectangle HitLocal=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
}

/// <summary>One of the three windows. ScrollStart/ScrollHeight come from its ScrollBar (thumb track), 0 when absent.</summary>
public sealed record MailWindow(string Name,MailPiece? BaseImage,Size Size,int MoveableHeight,string? KeyboardEvent,
    IReadOnlyList<MailPiece> Pieces,int ScrollStart,int ScrollHeight);

public sealed class MailPanel(UiResources? resources=null):IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(188)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new MailPanel(root.Resources){TextResolver=RefClientTextSource.Resolver(root.Resources)});   // T324 (#762): RenewalMailUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="RenewalMailUI";
    public const string LayoutName="RenewalMailUI",RootNode="RenewalMailUI",WindowNode="Window";
    public const string ListWindow="MailList_Window",ReadWindow="ReadMail_Window",WriteWindow="WriteMail_Window";
    /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
    public const string HostLayout="uistatus86";
    public const string RowList="MailList",ToEdit="To_Label",SubjectEdit="Subject_Label",BodyEdit="MailText_Label",FromEdit="From_Label";
    public const int NormalPostage=50,ClanPostage=1000;
    static readonly string[] BoxNames=["一般","血盟","保管箱"];
    static readonly string[] WriteFocusOrder=[ToEdit,SubjectEdit,BodyEdit];

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open (no XML, malformed XML, no size); null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size=>Active?.Size??Windows.FirstOrDefault()?.Size??Size.Empty;
    public Rectangle Bounds=>open&&!Size.IsEmpty?new(Origin,Size):Rectangle.Empty;
    public bool Visible=>open;
    public IReadOnlyList<MailWindow> Windows{get;private set;}=[];
    public MailWindow? Active{get;private set;}
    /// <summary>Images named in the XML but not found (not drawn): "surf 9183 (RenewalMailUI/Window[0]/Button[0])".</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>Elements deliberately not built (Include, unknown elements), by path.</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    /// <summary>(kind "String"/"Desc", id) → text; null = built-in names (combo) or nothing.</summary>
    public Func<string,string,string?>? TextResolver{get;set;}
    /// <summary>Set by ClientForm (MailUi.cs); null (offline) = the window works on an empty mailbox and sends nothing.</summary>
    public IMailPanelHost? Host{get;set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;

    // ---- runtime state
    public MailView? View{get;private set;}
    public byte Box{get;private set;}
    public bool ComboExpanded{get;private set;}
    public IReadOnlyCollection<uint> Checked=>checkedIds;
    public string? SortColumn{get;private set;}
    public bool SortDescending{get;private set;}
    public int ListScroll{get;private set;}
    public int BodyScroll{get;private set;}
    public uint? ReadingId{get;private set;}
    public string To=>to.ToString();
    public string Subject=>subject.ToString();
    public string Body=>body.ToString();
    public bool Clan{get;private set;}
    public bool Submitted{get;private set;}
    /// <summary>Edit with keyboard focus (WriteMail_Window only), or null.</summary>
    public string? Focus{get;private set;}
    /// <summary>Last reason the draft was not sent (MailPackets.Write), null when none.</summary>
    public string? Error{get;private set;}
    public bool Busy=>pending!=null&&!pending.IsCompleted;
    /// <summary>The running request sequence (tests await it).</summary>
    public Task? Pending=>pending;

    bool open;MailPiece? pressed;bool pressedInside;MailStamp? stamp;Task? pending;
    readonly HashSet<uint> checkedIds=[];readonly StringBuilder to=new(),subject=new(),body=new();
    readonly HashSet<string> unhandled=new(StringComparer.Ordinal);
    int bodyLines,bodyVisible;
    (string Text,int Width,List<string> Lines)? wrapCache;

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"mail-ui 無法載入 {LayoutName}，信件改用 WinForms 視窗：{ex.Message}");return;}
        var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
        try{Load(layout,surfs.Get,png.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"mail-ui {layout.FileName} 版面不符，信件改用 WinForms 視窗：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"mail-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"mail-ui {Unavailable}");
    }

    void Clear(){
        open=false;pressed=null;pressedInside=false;LayoutFile=null;Origin=Point.Empty;Windows=[];Active=null;Missing=[];Skipped=[];
        Unavailable="not loaded";ResetState();
    }
    void ResetState(){
        View=null;stamp=null;Box=0;ComboExpanded=false;checkedIds.Clear();SortColumn=null;SortDescending=false;ListScroll=0;BodyScroll=0;
        ReadingId=null;ResetDraft();
    }
    void ResetDraft(){to.Clear();subject.Clear();body.Clear();Clan=false;Submitted=false;Focus=null;Error=null;}

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        Clear();
        var root=layout.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(layout.FileName,root.Path,$"根元素不是 {RootNode}");
        var missing=new List<string>();var skipped=new List<string>();
        UiSurface? Resolve(OptionImageRef? r,string path){
            if(r==null)return null;
            var s=r.IsSurf?surf(r.Id):png(r.Id);
            if(s==null)missing.Add($"{r} ({path})");
            return s;
        }
        var built=new List<MailWindow>();
        foreach(var w in root.Children){
            if(w.Name!=WindowNode){skipped.Add(w.Path);continue;}
            string name=w.Attr("Name")??throw new UiLayoutException(layout.FileName,w.Path,"Window 沒有 Name");
            if(name is not (ListWindow or ReadWindow or WriteWindow)){skipped.Add(w.Path+"（不明視窗）");continue;}
            if(built.Any(p=>p.Name==name))throw new UiLayoutException(layout.FileName,w.Path,$"Window {name} 重複");
            var baseRef=OptionPanel.ImageAttr(w,"BasePng")??OptionPanel.ImageAttr(w,"BaseSurf");
            var basePiece=baseRef==null?null:Piece(MailPieceKind.Image,w,Point.Empty,null,-1,baseRef,Resolve(baseRef,w.Path+"/@"+baseRef.Attr));
            var pieces=new List<MailPiece>();var scroll=(Start:0,Height:0);
            Walk(w,Point.Empty,null,-1,pieces,skipped,Resolve,ref scroll);
            var declared=new Size(w.TryInt("Width")??0,w.TryInt("Height")??0);
            var size=declared.Width>0&&declared.Height>0?declared:basePiece?.ImageSize??Size.Empty;
            if(size.IsEmpty)throw new UiLayoutException(layout.FileName,w.Path,"沒有 Width/Height 也沒有底圖，無法定出視窗範圍");
            built.Add(new(name,basePiece,size,w.TryInt("Moveable")??0,w.Attr("KeyboardEvent"),pieces,scroll.Start,scroll.Height));
        }
        foreach(var need in new[]{ListWindow,ReadWindow,WriteWindow})
            if(built.All(b=>b.Name!=need))throw new UiLayoutException(layout.FileName,root.Path,$"缺少 Window {need}");
        if(!built.Single(b=>b.Name==ListWindow).Pieces.Any(p=>p.Group=="row"))throw new UiLayoutException(layout.FileName,root.Path,$"{ListWindow} 沒有信件列（List {RowList}）");
        Windows=built.ToArray();
        var first=root.ChildrenNamed(WindowNode).First(w=>w.Attr("Name")==ListWindow);
        LayoutFile=layout.FileName;Origin=new(first.TryInt("X")??0,first.TryInt("Y")??0);
        Missing=missing;Skipped=skipped;Unavailable=null;
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
    static MailPiece Piece(MailPieceKind kind,UiNode n,Point at,string? group,int index,OptionImageRef? image=null,UiSurface? surface=null,OptionImageRef? push=null,UiSurface? pushSurface=null){
        string? kindAttr=n.Attr("String")!=null?"String":n.Attr("Desc")!=null?"Desc":null;
        return new(kind,n.Path,n.Name,n.Attr("Name"),at,new(n.TryInt("Width")??0,n.TryInt("Height")??0),image,surface,push,pushSurface,
            n.Attr("MouseEvent") is "Dummy"?null:n.Attr("MouseEvent"),ParseColor(n,"FontColor",Color.White),
            (n.Attr("HAlign")??n.Attr("Align")??"left").Trim().ToLowerInvariant(),(n.Attr("VAlign")??"top").Trim().ToLowerInvariant(),
            group,index,kindAttr,kindAttr==null?null:n.Attr(kindAttr)!.Trim());
    }

    static void Walk(UiNode parent,Point origin,string? group,int index,List<MailPiece> into,List<string> skipped,Func<OptionImageRef?,string,UiSurface?> resolve,ref (int Start,int Height) scroll){
        foreach(var c in parent.Children)Element(c,Rel(c,origin),group,index,into,skipped,resolve,ref scroll);
    }

    static void Element(UiNode c,Point at,string? group,int index,List<MailPiece> into,List<string> skipped,Func<OptionImageRef?,string,UiSurface?> resolve,ref (int Start,int Height) scroll){
        switch(c.Name){
            case "Image":{
                var r=FirstImage(c,"Png","Surf","BaseSurf");
                if(r==null){skipped.Add(c.Path+"（沒有圖號）");return;}
                into.Add(Piece(MailPieceKind.Image,c,at,group,index,r,resolve(r,c.Path)));return;
            }
            case "Label":
                into.Add(Piece(MailPieceKind.Label,c,at,group,index));
                foreach(var k in c.Children)skipped.Add(k.Path);
                return;
            case "Edit" or "MultiEdit":
                into.Add(Piece(MailPieceKind.Edit,c,at,group,index) with{Limit=c.TryInt("LimitText")??0,ReadOnly=c.Attr("ReadOnly")?.Trim()=="1",Multiline=c.Name=="MultiEdit"});
                return;
            case "Button" or "CheckButton" or "RadioButton":{
                var normal=FirstImage(c,"BasePng","BaseSurf");var push=FirstImage(c,"PushPng","PushSurf");
                into.Add(Piece(MailPieceKind.Button,c,at,group,index,normal,resolve(normal,c.Path),push,push==null?null:resolve(push,c.Path+"/@"+push.Attr)));
                Walk(c,at,group,index,into,skipped,resolve,ref scroll);   // row labels (From/Subject/Date) sit inside the row CheckButton
                return;
            }
            case "List" or "UpDownList":{
                bool horizontal=c.Attr("Horizontal")?.Trim()=="1";
                int gap=c.TryInt("Gap")??0,advance=0,item=0;
                bool rows=c.Name=="List"&&c.Attr("Name")==RowList,combo=c.Name=="UpDownList";
                foreach(var k in c.Children){
                    bool background=combo&&k.Name=="Image";
                    var start=background?at:horizontal?new Point(at.X+advance,at.Y):new Point(at.X,at.Y+advance);
                    int before=into.Count;
                    string? g=rows?"row":combo?"combo":group;int i=rows||combo?(background?-1:item):index;
                    Element(k,Rel(k,start),g,i,into,skipped,resolve,ref scroll);
                    if(background||into.Count==before)continue;
                    var p=into[before];
                    advance+=(horizontal?(p.DeclaredSize.Width>0?p.DeclaredSize.Width:p.ImageSize.Width):(p.DeclaredSize.Height>0?p.DeclaredSize.Height:p.ImageSize.Height))+gap;
                    item++;
                }
                return;
            }
            case "ComboBox":
                foreach(var k in c.Children){
                    if(k.Name=="Label"){into.Add(Piece(MailPieceKind.Label,k,Rel(k,at),"combo-text",-1));continue;}
                    Element(k,Rel(k,at),group,index,into,skipped,resolve,ref scroll);
                }
                return;
            case "ScrollBar":
                scroll=(c.TryInt("ScrollStart")??0,c.TryInt("ScrollHeight")??0);
                Walk(c,at,"scroll",-1,into,skipped,resolve,ref scroll);
                return;
            case "Include":skipped.Add($"{c.Path}（{c.Attr("Xml")}，不建）");return;
            default:skipped.Add(c.Path);return;
        }
    }

    // ---- data

    /// <summary>Mails in display order (server order, or sorted by the last sort button; same button again = reverse).</summary>
    public IReadOnlyList<MailHeader> Rows{get{
        var list=View?.Headers??[];
        IEnumerable<MailHeader> q=SortColumn switch{
            "From_Button"=>list.OrderBy(h=>h.Sender,StringComparer.OrdinalIgnoreCase).ThenBy(h=>h.Id),
            "Subject_Button"=>list.OrderBy(h=>h.Subject,StringComparer.OrdinalIgnoreCase).ThenBy(h=>h.Id),
            "Date_Button"=>list.OrderBy(h=>(h.Year,h.Month,h.Day,h.Id)),
            _=>list,
        };
        if(SortColumn!=null&&SortDescending)q=q.Reverse();
        return q.ToArray();
    }}
    int RowSlots=>Windows.FirstOrDefault(w=>w.Name==ListWindow)?.Pieces.Where(p=>p.Group=="row").Select(p=>p.Index+1).DefaultIfEmpty(0).Max()??0;
    MailHeader? RowMail(int index){var rows=Rows;int i=index+ListScroll;return index>=0&&i<rows.Count?rows[i]:null;}
    MailHeader? Reading=>ReadingId is uint id?View?.Headers.FirstOrDefault(h=>h.Id==id):null;
    public static string DateText(MailHeader h)=>h.Month==0?"":$"{h.Year:D2}/{h.Month:D2}/{h.Day:D2}";
    string BoxName(int i)=>TextResolver?.Invoke("String",(4071+i).ToString(CultureInfo.InvariantCulture))??BoxNames[i];

    /// <summary>The text a label draws now; null = nothing (Captions are placeholders).</summary>
    public string? LabelText(MailPiece p){
        if(p.Kind!=MailPieceKind.Label)return null;
        if(p.Group=="row")return RowMail(p.Index) is not {} m?null:p.Name switch{"From_Label"=>m.Sender,"Subject_Label"=>m.Subject,"Date_Label"=>DateText(m),_=>null};
        if(p.Group=="combo-text")return BoxName(Box);
        return p.Name switch{
            "Count_Label"=>View==null?null:View.Headers.Length.ToString(CultureInfo.InvariantCulture),
            "Cost_Label"=>$"郵資 {(Clan?ClanPostage:NormalPostage)} 金幣",
            _=>p.TextKind==null?null:TextResolver?.Invoke(p.TextKind,p.TextValue!),
        };
    }
    /// <summary>Text of an edit on the active window.</summary>
    public string EditText(MailPiece p){
        if(Active?.Name==ReadWindow){
            var m=Reading;
            return p.Name switch{FromEdit=>m?.Sender??"",SubjectEdit=>m?.Subject??"",BodyEdit=>m!=null&&View?.ReadingId==m.Id?View.Body:"",_=>""};
        }
        return p.Name switch{ToEdit=>To,SubjectEdit=>Subject,BodyEdit=>Body,_=>""};
    }
    /// <summary>Button text from String/Desc (combo items); null = none.</summary>
    public string? ButtonText(MailPiece p)=>p.Group=="combo"&&p.Index is >=0 and <3?BoxName(p.Index):p.TextKind==null?null:TextResolver?.Invoke(p.TextKind,p.TextValue!);

    // ---- open / close

    public void Update(IUiGameState state){
        if(!state.InWorld){if(open)Close();View=null;return;}
        if(!open||Host==null)return;
        var v=Host.View;
        if(v==null||stamp!=null&&v.Stamp!=stamp){Close();return;}
        View=v;
        ListScroll=Math.Clamp(ListScroll,0,Math.Max(0,v.Headers.Length-RowSlots));
        checkedIds.IntersectWith(v.Headers.Select(h=>h.Id));
    }

    /// <summary>Opens on the mail list and asks for the current box. false when the layout is unavailable.</summary>
    public bool Open(){
        if(!Available)return false;
        ResetState();open=true;pressed=null;pressedInside=false;Active=Find(ListWindow);
        View=Host?.View;stamp=View?.Stamp;
        Request("list");
        return true;
    }
    public void Close(){open=false;pressed=null;pressedInside=false;Focus=null;ComboExpanded=false;}
    public bool Toggle(){if(open){Close();return true;}return Open();}
    MailWindow? Find(string name)=>Windows.FirstOrDefault(w=>w.Name==name);
    public bool ShowWindow(string name){
        if(!open||Find(name) is not {} w)return false;
        Active=w;pressed=null;pressedInside=false;ComboExpanded=false;BodyScroll=0;
        Focus=name==WriteWindow?Focus:null;
        return true;
    }

    // ---- requests

    void Request(string command,uint id=0)=>Sequence([(command,id)]);
    void Sequence(IReadOnlyList<(string Command,uint Id)> steps,Action? after=null){
        if(Host==null||View==null||Busy||steps.Count==0)return;
        var shown=View;byte box=Box;
        pending=RunSteps(shown,box,steps,after);
    }
    async Task RunSteps(MailView shown,byte box,IReadOnlyList<(string Command,uint Id)> steps,Action? after){
        bool ok=true;
        foreach(var (command,id) in steps){
            if(!open)return;
            if(!await Host!.Run(shown,command,box,id)){ok=false;break;}
        }
        if(ok)after?.Invoke();
    }
    void SendDraft(){
        if(Host==null||View==null||Busy||Submitted)return;
        try{_=MailPackets.Write(Clan,To,Subject,Body);Error=null;}
        catch(ArgumentException ex){Error=ex.Message;UiResourceLog.Write("mail-ui 未寄出："+ex.Message);return;}
        var shown=View;string command=Clan?"clan":"write";
        pending=SendAsync(shown,command,To,Subject,Body);
    }
    async Task SendAsync(MailView shown,string command,string receiver,string title,string text){
        if(await Host!.Run(shown,command,Box,0,receiver,title,text)){Submitted=true;Focus=null;if(open)ShowWindow(ListWindow);}
    }

    // ---- visibility / hit testing

    public bool Shown(MailPiece p)=>p.Group switch{
        "row"=>RowMail(p.Index)!=null,
        "combo"=>ComboExpanded,
        _=>true,
    };
    IEnumerable<MailPiece> Live=>Active?.Pieces.Where(Shown)??[];
    public Rectangle At(MailPiece p)=>new(Origin.X+p.HitLocal.X,Origin.Y+p.HitLocal.Y,p.HitLocal.Width,p.HitLocal.Height);
    bool Clickable(MailPiece p)=>p.HitLocal.Width>0&&p.HitLocal.Height>0&&p.Kind switch{
        MailPieceKind.Button=>p.MouseEvent!=null||p.Group=="scroll",
        MailPieceKind.Label=>p.MouseEvent!=null,
        MailPieceKind.Edit=>!p.ReadOnly&&Active?.Name==WriteWindow,
        _=>false,
    };
    /// <summary>Topmost clickable piece at a logical point (later in XML order = on top; the open combo list wins).</summary>
    public MailPiece? HitAt(Point location){
        if(!open)return null;
        var live=Live.Where(Clickable).ToArray();
        return live.Where(p=>p.Group=="combo").LastOrDefault(p=>At(p).Contains(location))??live.LastOrDefault(p=>At(p).Contains(location));
    }

    // ---- painting

    bool On(MailPiece p){
        if(p==pressed&&pressedInside)return true;
        if(p.Group=="row")return p.Element=="CheckButton"&&RowMail(p.Index) is {} m&&checkedIds.Contains(m.Id);
        if(p.Group=="combo")return p.Index==Box;
        return p.Name switch{
            "AllSelectBtn"=>View is {Headers.Length:>0} v&&v.Headers.All(h=>checkedIds.Contains(h.Id)),
            "CheckBox_Button"=>Clan,
            "From_Button" or "Subject_Button" or "Date_Button"=>SortColumn==p.Name,
            "Expand_Button"=>ComboExpanded,
            _=>false,
        };
    }

    public void Paint(UiCanvas canvas){
        if(!open||Active==null)return;
        if(Active.BaseImage?.Surface is {} b)canvas.DrawSurface(b,Origin.X,Origin.Y);
        var live=Live.ToArray();
        foreach(var p in live.Where(p=>p.Group!="combo"))PaintPiece(canvas,p);
        foreach(var p in live.Where(p=>p.Group=="combo"))PaintPiece(canvas,p);   // the open combo list covers the rows
    }

    void PaintPiece(UiCanvas canvas,MailPiece p){
        int x=Origin.X+p.Offset.X,y=Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case MailPieceKind.Image:canvas.DrawSurface(p.Surface,x,y);break;
            case MailPieceKind.Label:
                if(LabelText(p) is {Length:>0} text)DrawText(canvas,text,At(p),p,false);
                break;
            case MailPieceKind.Button:{
                if(p.Group=="scroll"&&p.Name=="Move_Button")y+=ThumbOffset(p);
                canvas.DrawSurface(On(p)&&p.PushSurface!=null?p.PushSurface:p.Surface,x,y);
                if(ButtonText(p) is {Length:>0} t)DrawText(canvas,t,At(p),p,false);
                break;
            }
            case MailPieceKind.Edit:PaintEdit(canvas,p);break;
        }
    }

    int ScrollRange=>Active?.Name==ListWindow?Math.Max(0,(View?.Headers.Length??0)-RowSlots):Math.Max(0,bodyLines-bodyVisible);
    int ScrollPos=>Active?.Name==ListWindow?ListScroll:BodyScroll;
    int ThumbOffset(MailPiece thumb){
        int range=ScrollRange;if(range<=0||Active==null)return 0;
        int track=Math.Max(0,Active.ScrollHeight-thumb.ImageSize.Height);
        return (int)Math.Round(track*(double)Math.Clamp(ScrollPos,0,range)/range);
    }

    static void DrawText(UiCanvas canvas,string text,Rectangle box,MailPiece p,bool tail){
        var g=canvas.Graphics;var size=UiText.Measure(g,text);
        int tx=box.Width<=0?box.X:p.HAlign switch{"center"=>box.X+(box.Width-size.Width)/2,"right"=>box.X+box.Width-size.Width,_=>box.X};
        if(tail&&box.Width>0&&size.Width>box.Width)tx=box.X+box.Width-size.Width;
        int ty=box.Height<=0?box.Y:p.VAlign switch{"middle"=>box.Y+(box.Height-size.Height)/2,"bottom"=>box.Y+box.Height-size.Height,_=>box.Y};
        WithClip(canvas,box,()=>canvas.DrawText(text,tx,ty,p.Color));
    }
    static void WithClip(UiCanvas canvas,Rectangle box,Action draw){
        if(box.Width<=0||box.Height<=0){draw();return;}
        var g=canvas.Graphics;using var saved=g.Clip;
        g.SetClip(Rectangle.Intersect(box,canvas.Clip));
        try{draw();}finally{g.Clip=saved;}
    }

    void PaintEdit(UiCanvas canvas,MailPiece p){
        var box=At(p);string text=EditText(p);
        bool focused=Active?.Name==WriteWindow&&Focus==p.Name;
        if(focused)text+="|";
        if(!p.Multiline){DrawText(canvas,text,box,p with{VAlign="middle"},true);return;}
        var g=canvas.Graphics;
        int lineHeight=Math.Max(1,UiText.Measure(g,"國").Height);
        var lines=Wrap(g,text,box.Width);
        bodyLines=lines.Count;bodyVisible=Math.Max(1,box.Height/lineHeight);
        if(focused)BodyScroll=Math.Max(0,bodyLines-bodyVisible);
        BodyScroll=Math.Clamp(BodyScroll,0,Math.Max(0,bodyLines-bodyVisible));
        WithClip(canvas,box,()=>{
            for(int i=BodyScroll,row=0;i<lines.Count&&row<=bodyVisible;i++,row++)
                if(lines[i].Length>0)canvas.DrawText(lines[i],box.X,box.Y+row*lineHeight,p.Color);
        });
    }
    List<string> Wrap(Graphics g,string text,int width){
        if(wrapCache is {} c&&c.Text==text&&c.Width==width)return c.Lines;
        var lines=new List<string>();
        foreach(var para in text.Replace("\r\n","\n").Split('\n')){
            var line=new StringBuilder();
            var e=StringInfo.GetTextElementEnumerator(para);
            while(e.MoveNext()){
                string piece=e.GetTextElement();
                if(width>0&&line.Length>0&&UiText.Measure(g,line+piece).Width>width){lines.Add(line.ToString());line.Clear();}
                line.Append(piece);
            }
            lines.Add(line.ToString());
        }
        wrapCache=(text,width,lines);
        return lines;
    }

    // ---- input

    public bool IsDragHandle(Point location)=>open&&Active!=null&&Bounds.Contains(location)&&location.Y<Origin.Y+Active.MoveableHeight&&HitAt(location)==null;
    public void MoveTo(Point location)=>Origin=location;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel){if(inside&&e.Delta!=0)ScrollBy(e.Delta>0?-1:1);return inside;}
        if(e.Button!=MouseButtons.Left)return inside||pressed!=null;
        switch(e.Kind){
            case UiMouseKind.Down:{
                var hit=HitAt(e.Location);
                if(hit is {Kind:MailPieceKind.Edit}){Focus=hit.Name;pressed=null;pressedInside=false;return true;}
                if(hit==null&&ComboExpanded)ComboExpanded=false;
                if(hit==null)Focus=null;
                pressed=hit;pressedInside=hit!=null;
                return inside;
            }
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

    void ScrollBy(int delta){
        if(Active?.Name==ListWindow)ListScroll=Math.Clamp(ListScroll+delta,0,ScrollRange);
        else BodyScroll=Math.Clamp(BodyScroll+delta,0,Math.Max(0,bodyLines-bodyVisible));
    }

    void Fire(MailPiece p){
        if(p.Group=="scroll"){if(p.Name=="Up_Button")ScrollBy(-1);else if(p.Name=="Down_Button")ScrollBy(1);return;}
        switch(p.MouseEvent){
            case "CloseMailUI":Close();return;
            case "CloseReadMailUI" or "CloseWriteMailUI":ShowWindow(ListWindow);return;
            case "ComboBox_Expand":ComboExpanded=!ComboExpanded;return;
            case "ListType_Select":
                ComboExpanded=false;
                if(p.Index is >=0 and <=2&&p.Index!=Box){Box=(byte)p.Index;checkedIds.Clear();ListScroll=0;ReadingId=null;Request("list");}
                return;
            case "SelectMailItem":
                if(RowMail(p.Index) is {} m&&!checkedIds.Remove(m.Id))checkedIds.Add(m.Id);
                return;
            case "AllSelect":{
                var all=View?.Headers.Select(h=>h.Id).ToArray()??[];
                if(all.Length>0&&all.All(checkedIds.Contains))checkedIds.Clear();else checkedIds.UnionWith(all);
                return;
            }
            case "SortMailItem":
                if(SortColumn==p.Name)SortDescending=!SortDescending;else{SortColumn=p.Name;SortDescending=false;}
                return;
            case "ReadMailEvent":
                if(RowMail(p.Index) is {} r)ReadMail(r.Id);
                return;
            case "ReadPrevMail" or "ReadNextMail":{
                var rows=Rows;int i=rows.ToList().FindIndex(h=>h.Id==ReadingId);
                int j=i+(p.MouseEvent=="ReadPrevMail"?-1:1);
                if(i>=0&&j>=0&&j<rows.Count)ReadMail(rows[j].Id);
                return;
            }
            case "WriteMail":ResetDraft();ShowWindow(WriteWindow);Focus=ToEdit;return;
            case "ReplyMail":{
                var reading=Reading;ResetDraft();
                if(reading!=null)to.Append(reading.Sender);
                ShowWindow(WriteWindow);Focus=reading==null?ToEdit:SubjectEdit;
                return;
            }
            case "CheckPledgeMail":if(!Submitted&&!Busy)Clan=!Clan;return;
            case "SendMail":SendDraft();return;
            case "SaveMail" or "DeleteMail":{
                string command=p.MouseEvent=="SaveMail"?"store":"delete";
                if(Active?.Name==ReadWindow){
                    Action? back=command=="delete"?()=>{if(open)ShowWindow(ListWindow);}:null;
                    if(ReadingId is uint id)Sequence([(command,id)],back);
                }else{
                    var ids=Rows.Where(h=>checkedIds.Contains(h.Id)).Select(h=>(command,h.Id)).ToArray();
                    Sequence(ids,()=>checkedIds.ExceptWith(ids.Select(x=>x.Id)));
                }
                return;
            }
        }
        string name=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(name))UiResourceLog.Write($"mail-ui {name} 尚未接上");
    }

    void ReadMail(uint id){
        ReadingId=id;ShowWindow(ReadWindow);
        Request("read",id);
    }

    StringBuilder? FocusedText=>Focus switch{ToEdit=>to,SubjectEdit=>subject,BodyEdit=>body,_=>null};
    int FocusLimit=>Find(WriteWindow)?.Pieces.FirstOrDefault(p=>p.Kind==MailPieceKind.Edit&&p.Name==Focus)?.Limit??0;
    bool Editable=>open&&Active?.Name==WriteWindow&&FocusedText!=null&&!Submitted&&!Busy;
    void NextFocus(){int i=Array.IndexOf(WriteFocusOrder,Focus);Focus=WriteFocusOrder[(i+1)%WriteFocusOrder.Length];}
    bool Append(string s){
        var sb=FocusedText!;int limit=FocusLimit;
        if(limit>0&&sb.Length+s.Length>limit)return false;
        sb.Append(s);Error=null;return true;
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!open||Active==null)return false;
        bool editing=Active.Name==WriteWindow&&Focus!=null;
        if(e.KeyCode==Keys.Escape){
            if(e.Down&&!e.Repeat){
                if(ComboExpanded)ComboExpanded=false;
                else Fire(new MailPiece(MailPieceKind.Button,"key",Active.Name,null,Point.Empty,Size.Empty,null,null,null,null,Active.KeyboardEvent,Color.Empty));
            }
            return true;
        }
        if(!editing)return false;
        // Printable keys stay unhandled here so WM_CHAR reaches Char(); the keys below never produce text but would reach the game.
        if(e.KeyCode is not (Keys.Back or Keys.Enter or Keys.Tab or Keys.Delete or Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End))return false;
        if(!e.Down)return true;
        switch(e.KeyCode){
            case Keys.Back when Editable:{var sb=FocusedText!;if(sb.Length>0){int n=sb.Length>1&&char.IsLowSurrogate(sb[^1])?2:1;sb.Length-=n;Error=null;}break;}
            case Keys.Tab:NextFocus();break;
            case Keys.Enter:if(Focus==BodyEdit){if(Editable)Append("\n");}else NextFocus();break;   // ToNextFocus
        }
        return true;
    }

    public bool Char(char c){
        if(!open||Active?.Name!=WriteWindow||Focus==null)return false;
        if(char.IsControl(c))return true;   // Back／Enter／Tab handled in Key
        if(Editable)Append(c.ToString());
        return true;
    }
}
