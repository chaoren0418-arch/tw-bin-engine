using System.Globalization;
namespace P06.NativeUi;

// T327 (#765, P-D of docs/reference/pak-resources.md §6, row 4:SelectUI): the native character select screen.
// TW16 reads SelectUI.xml; 0-客戶端 does not ship it (PAK§4.2: all paks checked). It is read at run time from the reference
// client through UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak, "-c" first; SHA-256
// logged as "ui-xml-ref"). Nothing is copied into the repository.
//
// Loading. Same as ResetStatPanel (#763): registered on the host layout "uistatus86" and Load reads SelectUI itself. No
// reference client / no XML / malformed XML / no window size → Unavailable; CharacterManagementUi.cs then never attaches a
// host and the existing WinForms path (dev-tools 「登入／選角」 page and the creation dialog) stays as it is. --classic-ui
// never routes here.
//
// Images. Surfs go through SurfCatalog.Get (local first; the T322/#760 reference fallback for ids the pinned local Sprite
// archives do not have). Every surf of Character_Select_Window and Message_Window is in the pinned local archives; the 15
// surfs PAK§4.1 lists as missing (6988, 6989, 7033–7038, 7232–7238) belong only to the name change / merge windows, which are
// not built (3.81 has no packets for them), so they are never looked up. Missing images are not drawn (no substitute).
//
// Data. The screen is a view of LoginConnection's character list through ICharSelectHost (CharacterManagementUi.cs); enter,
// delete and logout go through the existing LoginControl.Enter / LoginConnection.DeleteCharacter / CancelLogin with the same
// generation/revision stamps as the WinForms controls.
//
// Layout rules (推測; the SelectUI binder was not decompiled for this):
// - Coordinates are relative to the parent element (Window/Control/Image/Button); a missing X/Y is 0. A List stacks its
//   children by their Height (Vertical="1") or Width.
// - Paint order inside a window: siblings in reverse XML order (backgrounds come last in the XML), a parent before its
//   children. Hit testing is the reverse of the paint order. Same rule as #761/#763, extended to nested elements.
// - Window size: declared Width/Height, else BaseSurf image, else Back_Image image.
// - "_Multi"/"_multi" images duplicate the "_Screen" ones; only the screen variant is built.
// - BigSlot_List has 4 Slot_Controls = the visible rows. Row r = scroll + slot index. r < characters → Select_Button (or
//   WaitDelete_Button + WaitToDel_Image when the server marks the character for 7-day deletion, class 32–38); r < Slots →
//   Create_Button; otherwise Slot_Lock_Back + Lock_Button (slot purchase: no 3.81 packet, disabled). Up/Down scroll by one row.
// - The selected character's Select_Button is drawn with its PushSurf; a double click on it enters the world.
// - CharacterInfo_List: Name, Level, Class, Pledge (clan), Alignment (lawful) and Birthday (the raw S93 create-time value,
//   its format is 待驗證) of the selected character.
// - Delete_Button → Message_Window confirm (same text as the WinForms MessageBox) → Ok sends. Back_Button logs out.
//   A refused action or the server's result notice after a delete is shown in Message_Window.
// - Not built: Character_Ani (Effect, the selected character's animation), DeleteDelay_Image (no image id), Tooltips, the
//   CharacterName_Change / CharacterName_Merge / PledgeName_Merge windows.

/// <summary>Character list snapshot for the select/create screens (LoginConnection through the host).</summary>
public sealed record CharSelectView(int Generation,int Revision,LoginState State,IReadOnlyList<LoginCharacter> Characters,int Slots,
    bool CanEnter,bool CanCreate,bool CanDelete,string Notice) {
    public bool Shown=>State is LoginState.CharacterList or LoginState.ManagingCharacters;
    public bool Busy=>State==LoginState.ManagingCharacters;
}

/// <summary>Outward actions of the select/create screens; the product adapter (CharacterManagementUi.cs) forwards to LoginConnection.</summary>
public interface ICharSelectHost {
    CharSelectView Snapshot();
    Task<LoginAction> Enter(CharSelectView stamp,string name);
    Task<LoginAction> Create(CharSelectView stamp,CharacterCreation request);
    Task<LoginAction> Delete(CharSelectView stamp,string name);
    void Logout();
    /// <summary>The native create screen is not usable: open the existing WinForms creation dialog instead.</summary>
    void CreateFallback();
}

public enum CharUiKind { Image, Board, Label, MultiLabel, Button, Edit }

/// <summary>One built element. Offset is relative to its window. Surface null = missing image (not drawn).</summary>
public sealed class CharUiPiece {
    public required CharUiKind Kind{get;init;}
    public required string Path{get;init;}
    public string? Name{get;init;}
    /// <summary>Nearest named Control above the element (stat name, "Class_Prince", "Slot_Control" …).</summary>
    public string? Owner{get;init;}
    /// <summary>Name of the List the element is in, and its child index there (-1 outside a List).</summary>
    public string? ListName{get;init;}
    public int Slot{get;init;}=-1;
    /// <summary>Enclosing Image/Button piece; a piece is only shown while its parent is.</summary>
    public CharUiPiece? Parent{get;init;}
    public Point Offset{get;init;}
    public Size DeclaredSize{get;init;}
    public OptionImageRef? Image{get;init;}
    public UiSurface? Surface{get;init;}
    public UiSurface? Highlight{get;init;}
    public UiSurface? Push{get;init;}
    public string? MouseEvent{get;init;}
    public string? TextKind{get;init;}
    public string? TextValue{get;init;}
    public Color Color{get;init;}=Color.White;
    public int Alpha{get;init;}=255;
    public string Align{get;init;}="left";
    public string VAlign{get;init;}="top";
    public int LineMargin{get;init;}
    public int Limit{get;init;}
    public Size ImageSize=>Surface==null?Size.Empty:new(Surface.Width,Surface.Height);
    public Rectangle HitLocal=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
    public override string ToString()=>Path;
}

public sealed record CharUiWindow(string Name,Point Origin,Size Size,UiSurface? Base,IReadOnlyList<CharUiPiece> Pieces) {
    public Rectangle Bounds=>new(Origin,Size);
    public CharUiPiece? Find(string name,string? owner=null)=>Pieces.FirstOrDefault(p=>p.Name==name&&(owner==null||p.Owner==owner));
}

/// <summary>
/// Builds SelectUI / CreateUI windows (shared by SelectCharPanel and CreateCharPanel). Pieces come out in paint order:
/// siblings in reverse XML order, a parent before its children. Skip returns a reason to leave an element out (null = build).
/// </summary>
internal sealed class CharUiBuilder(string file,Func<int,UiSurface?> surf,Func<UiNode,string?>? skip=null) {
    public readonly List<string> Missing=[],Skipped=[];
    readonly Dictionary<int,UiSurface?> resolved=[];
    UiSurface? Resolve(OptionImageRef? r,string path){
        if(r==null)return null;
        if(!r.IsSurf){Missing.Add($"{r} ({path}，只支援 Surf)");return null;}
        if(!resolved.TryGetValue(r.Id,out var s))resolved[r.Id]=s=surf(r.Id);
        if(s==null)Missing.Add($"{r} ({path})");
        return s;
    }
    static Point Rel(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));
    static bool MultiVariant(string? name)=>name!=null&&name.EndsWith("_multi",StringComparison.OrdinalIgnoreCase);

    public CharUiWindow Window(UiNode w){
        var origin=new Point(w.TryInt("X")??throw new UiLayoutException(file,w.Path,"Window 沒有 X"),w.TryInt("Y")??throw new UiLayoutException(file,w.Path,"Window 沒有 Y"));
        var baseRef=ResetStatPanel.ImageRef(w,"BaseSurf");
        var baseSurface=Resolve(baseRef,w.Path+"/@BaseSurf");
        var pieces=new List<CharUiPiece>();
        Walk(w,Point.Empty,new(null,null,-1,null),pieces);
        var back=pieces.FirstOrDefault(p=>p.Kind==CharUiKind.Image&&p.Name=="Back_Image"&&p.Offset==Point.Empty&&p.Surface!=null);
        var declared=new Size(w.TryInt("Width")??0,w.TryInt("Height")??0);
        var size=declared.Width>0&&declared.Height>0?declared:baseSurface!=null?new Size(baseSurface.Width,baseSurface.Height):back?.ImageSize??Size.Empty;
        return new(w.Attr("Name")??w.Name,origin,size,baseSurface,pieces);
    }

    readonly record struct Context(string? Owner,string? List,int Slot,CharUiPiece? Parent);

    void Walk(UiNode parent,Point origin,Context ctx,List<CharUiPiece> into){
        var kids=parent.Children;
        for(int i=kids.Count-1;i>=0;i--)Element(kids[i],Rel(kids[i],origin),ctx,into);
    }

    void Element(UiNode c,Point at,Context ctx,List<CharUiPiece> into){
        string? name=c.Attr("Name");
        if(skip?.Invoke(c) is {} reason){Skipped.Add($"{c.Path}（{name??c.Name}：{reason}）");return;}
        CharUiPiece Piece(CharUiKind kind,OptionImageRef? image=null,UiSurface? surface=null,UiSurface? high=null,UiSurface? push=null,
            string? textKind=null,Color? color=null,int alpha=255)=>new(){Kind=kind,Path=c.Path,Name=name,Owner=ctx.Owner,ListName=ctx.List,Slot=ctx.Slot,Parent=ctx.Parent,Offset=at,
            DeclaredSize=new(c.TryInt("Width")??0,c.TryInt("Height")??0),MouseEvent=c.Attr("MouseEvent"),
            Align=(c.Attr("Align")??c.Attr("HAlign")??"left").Trim().ToLowerInvariant(),VAlign=(c.Attr("VAlign")??"top").Trim().ToLowerInvariant(),LineMargin=c.TryInt("LineMargin")??0,
            Image=image,Surface=surface,Highlight=high,Push=push,TextKind=textKind,TextValue=textKind==null?null:c.Attr(textKind)!.Trim(),Color=color??Color.White,Alpha=alpha,
            Limit=c.TryInt("LimitText")??0};
        switch(c.Name){
            case "Image":{
                if(MultiVariant(name)){Skipped.Add(c.Path+"（multi 版，只建 screen 版）");return;}
                var r=ResetStatPanel.ImageRef(c,"Surf");
                if(r==null&&c.Children.Count==0){Skipped.Add(c.Path+"（沒有圖號，執行期才定圖）");return;}
                var p=Piece(CharUiKind.Image,r,Resolve(r,c.Path));
                into.Add(p);Walk(c,at,ctx with{Parent=p},into);return;
            }
            case "BoardImage":{
                int w=c.Int("Width"),h=c.Int("Height");int alpha=c.TryInt("CustomAlpha")??255;
                if(w<=0||h<=0||alpha is <0 or >255)throw new UiLayoutException(file,c.Path,"BoardImage 大小或 CustomAlpha 不合理");
                into.Add(Piece(CharUiKind.Board,color:ResetStatPanel.ParseColor(c,"CustomColor",Color.Black),alpha:alpha));
                return;
            }
            case "Label" or "MultiLabel":{
                string? kind=c.Attr("Desc")!=null?"Desc":c.Attr("String")!=null?"String":null;
                into.Add(Piece(c.Name=="MultiLabel"?CharUiKind.MultiLabel:CharUiKind.Label,textKind:kind,color:ResetStatPanel.ParseColor(c,"FontColor",Color.White)));
                foreach(var k in c.Children)Skipped.Add(k.Path);
                return;
            }
            case "Button":{
                var normal=ResetStatPanel.ImageRef(c,"BaseSurf");var high=ResetStatPanel.ImageRef(c,"HighlightSurf");var push=ResetStatPanel.ImageRef(c,"PushSurf");
                var ns=Resolve(normal,c.Path);
                var hs=high==null?null:Resolve(high,c.Path+"/@HighlightSurf");
                var ps=push==null?null:Resolve(push,c.Path+"/@PushSurf");
                var b=Piece(CharUiKind.Button,normal,ns,hs,ps);
                into.Add(b);Walk(c,at,ctx with{Parent=b},into);return;
            }
            case "Edit":{
                int w=c.Int("Width"),h=c.Int("Height");
                if(w<=0||h<=0)throw new UiLayoutException(file,c.Path,"Edit 沒有大小");
                into.Add(Piece(CharUiKind.Edit));
                return;
            }
            case "Control":Walk(c,at,ctx with{Owner=name??ctx.Owner},into);return;
            case "List":{
                bool vertical=c.Attr("Vertical")=="1";
                var kids=c.Children;var pos=new Point[kids.Count];var cursor=at;
                for(int i=0;i<kids.Count;i++){
                    pos[i]=Rel(kids[i],cursor);
                    cursor=vertical?new(cursor.X,cursor.Y+(kids[i].TryInt("Height")??0)):new(cursor.X+(kids[i].TryInt("Width")??0),cursor.Y);
                }
                for(int i=kids.Count-1;i>=0;i--)Element(kids[i],pos[i],ctx with{List=name,Slot=i},into);
                return;
            }
            default:Skipped.Add($"{c.Path}（{c.Name} 不建）");return;
        }
    }
}

/// <summary>Painting and pointer helpers shared by the two character screens.</summary>
internal static class CharUiPaint {
    public static Rectangle At(CharUiWindow w,CharUiPiece p)=>new(w.Origin.X+p.HitLocal.X,w.Origin.Y+p.HitLocal.Y,p.HitLocal.Width,p.HitLocal.Height);
    public static int AlignX(CharUiPiece p,int x,int width)=>p.DeclaredSize.Width<=0?x:p.Align switch{"center"=>x+(p.DeclaredSize.Width-width)/2,"right"=>x+p.DeclaredSize.Width-width,_=>x};
    public static int AlignY(CharUiPiece p,int y,int height)=>p.DeclaredSize.Height<=0?y:p.VAlign switch{"center" or "middle"=>y+(p.DeclaredSize.Height-height)/2,"bottom"=>y+p.DeclaredSize.Height-height,_=>y};
    public static void Label(UiCanvas canvas,CharUiPiece p,int x,int y,string? text){
        if(text is not {Length:>0})return;
        var size=UiText.Measure(canvas.Graphics,text);
        canvas.DrawText(text,AlignX(p,x,size.Width),AlignY(p,y,size.Height),p.Color);
    }
    public static void MultiLabel(UiCanvas canvas,CharUiPiece p,int x,int y,string? text){
        if(text is not {Length:>0})return;
        var lines=ResetStatPanel.Wrap(canvas.Graphics,text,p.DeclaredSize.Width);
        int lh=UiText.Measure(canvas.Graphics,"國").Height+p.LineMargin;
        int ty=AlignY(p,y,lines.Count*lh-p.LineMargin);
        foreach(var line in lines){if(line.Length>0)canvas.DrawText(line,AlignX(p,x,UiText.Measure(canvas.Graphics,line).Width),ty,p.Color);ty+=lh;}
    }
    public static void Board(UiCanvas canvas,CharUiPiece p,int x,int y){
        using var brush=new SolidBrush(Color.FromArgb(p.Alpha,p.Color));
        canvas.Graphics.FillRectangle(brush,x,y,p.DeclaredSize.Width,p.DeclaredSize.Height);
    }
    /// <summary>Topmost shown piece of the window that accepts the pointer at a logical point (reverse paint order).</summary>
    public static CharUiPiece? ButtonAt(CharUiWindow w,Point location,Func<CharUiPiece,bool> shown){
        for(int i=w.Pieces.Count-1;i>=0;i--){
            var p=w.Pieces[i];
            if(p.Kind==CharUiKind.Button&&!p.HitLocal.IsEmpty&&shown(p)&&At(w,p).Contains(location))return p;
        }
        return null;
    }
}

public enum CharSelectMessage { None, ConfirmDelete, Notice }

public sealed class SelectCharPanel(UiResources? resources=null,UiRoot? root=null):IUiClosablePanel {
    [UiPanel(186)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,r=>new SelectCharPanel(r.Resources,r));   // T327 (#765): SelectUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="SelectUI";
    public const string LayoutName="SelectUI",RootNode="SelectUI",MainWindow="Character_Select_Window",MessageWindow="Message_Window",SlotList="BigSlot_List",InfoList="CharacterInfo_List";
    public const string HostLayout="uistatus86";
    public const string SelectEvent="SelectChar_SelectWin",CreateEvent="CreateChar_SelectWin",LockEvent="SlotLock_SelectWin",UpEvent="UpSlot_SelectWin",DownEvent="DownSlot_SelectWin",
        EnterEvent="Ok_SelectWin",BackEvent="Cancel_SelectWin",DeleteEvent="DeleteChar_SelectWin",MsgOkEvent="Ok_MessageWin",MsgCancelEvent="Cancel_MessageWin";
    /// <summary>Windows of SelectUI.xml that are not built (no 3.81 packets): their surfs are never looked up.</summary>
    public static readonly IReadOnlySet<string> SkippedWindows=new HashSet<string>(StringComparer.Ordinal){"CharacterName_Change_Window","CharacterName_Merge_Window","PledgeName_Merge_Window"};

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public CharUiWindow? Main{get;private set;}
    public CharUiWindow? Message{get;private set;}
    public IReadOnlyList<string> Missing{get;private set;}=[];
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public ICharSelectHost? Host{get;private set;}
    public CharSelectView? View{get;private set;}
    /// <summary>Visible slot rows (Slot_Control count in BigSlot_List).</summary>
    public int Rows{get;private set;}
    public int Scroll{get;private set;}
    public string? Selected{get;private set;}
    public CharSelectMessage Shown{get;private set;}
    public string? MessageText{get;private set;}
    public bool Pending{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    public Func<string,string,string?>? TextResolver{get;set;}
    /// <summary>The create screen this one hands over to (found on the root when not set).</summary>
    public CreateCharPanel? Create{get=>create??root?.Find(CreateCharPanel.PanelId) as CreateCharPanel;set=>create=value;}
    CreateCharPanel? create;
    bool closed;string? deleting;int generation=-1;bool managing;
    CharUiPiece? pressed,hover;bool pressedInside;readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

    public bool Visible=>Available&&!closed&&Host!=null&&View is {Shown:true}&&Create is not {Visible:true};
    public Rectangle Bounds=>Visible&&Main!=null?Main.Bounds:Rectangle.Empty;

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"select-char-ui 無法載入 {LayoutName}，選角畫面停用（WinForms 選角照舊）：{ex.Message}");return;}
        try{Load(layout,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"select-char-ui {layout.FileName} 版面不符，選角畫面停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"select-char-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"select-char-ui {Unavailable}");
    }

    void Clear(){
        Host=null;View=null;LayoutFile=null;Main=null;Message=null;Missing=[];Skipped=[];Unavailable="not loaded";Rows=0;
        ResetState();
    }
    void ResetState(){Scroll=0;Selected=null;Shown=CharSelectMessage.None;MessageText=null;Pending=false;deleting=null;closed=false;managing=false;pressed=null;hover=null;pressedInside=false;}

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var top=layout.Root;
        if(top.Name!=RootNode)throw new UiLayoutException(layout.FileName,top.Path,$"根元素不是 {RootNode}");
        var b=new CharUiBuilder(layout.FileName,surf,n=>n.Name switch{
            "Effect"=>"角色動畫執行期才定，不建（待做）",
            "Tooltip"=>"說明字串不建",
            _=>null,
        });
        CharUiWindow? main=null,message=null;
        foreach(var w in top.Children){
            string? name=w.Name=="Window"?w.Attr("Name"):null;
            if(name!=null&&SkippedWindows.Contains(name)){b.Skipped.Add($"{w.Path}（{name}：3.81 沒有改名／合併封包，不建）");continue;}
            if(name is not (MainWindow or MessageWindow)){b.Skipped.Add(w.Path);continue;}
            if(name==MainWindow&&main!=null||name==MessageWindow&&message!=null)throw new UiLayoutException(layout.FileName,w.Path,$"Window {name} 重複");
            var built=b.Window(w);
            if(name==MainWindow)main=built;else message=built;
        }
        if(main==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {MainWindow}");
        if(message==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {MessageWindow}（無法確認刪除）");
        int rows=main.Pieces.Where(p=>p.ListName==SlotList&&p.Kind==CharUiKind.Button&&p.MouseEvent==SelectEvent&&p.Name=="Select_Button").Select(p=>p.Slot).Distinct().Count();
        if(rows==0)throw new UiLayoutException(layout.FileName,top.Path,$"{SlotList} 沒有 Select_Button 欄位");
        foreach(var ev in new[]{EnterEvent,BackEvent})if(!main.Pieces.Any(p=>p.MouseEvent==ev))throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {ev} 按鈕");
        LayoutFile=layout.FileName;Main=main;Message=message;Missing=b.Missing;Skipped=b.Skipped;Rows=rows;
        Unavailable=main.Size.IsEmpty?$"{layout.FileName}: {MainWindow} 沒有大小":message.Size.IsEmpty?$"{layout.FileName}: {MessageWindow} 底圖都缺，無法定出確認框範圍":null;
    }

    // ---- state ------------------------------------------------------------------------------------------------------------

    public void Attach(ICharSelectHost host){Host=host;ResetState();Sync();}
    /// <summary>Evicted by the stack (never for the float layer) or the host is gone: the screen hides until the next login.</summary>
    public void Close(){closed=true;Shown=CharSelectMessage.None;pressed=null;hover=null;pressedInside=false;}

    public void Update(IUiGameState state)=>Sync();

    public void Sync(){
        if(Host==null)return;
        var v=Host.Snapshot();
        if(v.Generation!=generation){generation=v.Generation;ResetState();}
        // A delete started here finished (ManagingCharacters → CharacterList): show the server's result.
        if(managing&&!Pending&&v.State==LoginState.CharacterList){managing=false;if(v.Notice.Length>0)ShowMessage(CharSelectMessage.Notice,v.Notice);}
        if(!v.Shown){managing=false;if(Shown==CharSelectMessage.ConfirmDelete)Shown=CharSelectMessage.None;}
        if(!v.Shown)closed=false;
        View=v;
        if(Selected==null||!v.Characters.Any(c=>c.Name==Selected))Selected=v.Characters.FirstOrDefault()?.Name;
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
        if(Shown==CharSelectMessage.ConfirmDelete&&(deleting==null||!v.Characters.Any(c=>c.Name==deleting)))Shown=CharSelectMessage.None;
    }

    void ShowMessage(CharSelectMessage kind,string text){Shown=kind;MessageText=text;pressed=null;}

    public int TotalRows=>View is not {} v?Rows:Math.Max(Rows,Math.Max(v.Slots,v.Characters.Count));
    public int MaxScroll=>Math.Max(0,TotalRows-Rows);
    public enum RowKind { Character, Pending, Empty, Locked }
    public RowKind Row(int row){
        var v=View;
        if(v!=null&&row<v.Characters.Count)return CharacterManagementPackets.PendingDeletion(v.Characters[row])?RowKind.Pending:RowKind.Character;
        return v!=null&&row<v.Slots?RowKind.Empty:RowKind.Locked;
    }
    LoginCharacter? RowCharacter(int row)=>View is {} v&&row>=0&&row<v.Characters.Count?v.Characters[row]:null;
    public LoginCharacter? SelectedCharacter=>View?.Characters.FirstOrDefault(c=>c.Name==Selected);
    bool Ready=>View is {State:LoginState.CharacterList}&&!Pending;

    /// <summary>Slot pieces by row state, info labels while a character is selected; a child only while its parent is shown.</summary>
    public bool ShownPiece(CharUiPiece p){
        if(p.Parent!=null&&!ShownPiece(p.Parent))return false;
        if(p.ListName==InfoList)return SelectedCharacter!=null;
        if(p.ListName!=SlotList)return true;
        var kind=Row(Scroll+p.Slot);
        return p.Name switch{
            "Slot_Back"=>kind!=RowKind.Locked,
            "Slot_Lock_Back" or "Lock_Button"=>kind==RowKind.Locked,
            "Select_Button"=>kind==RowKind.Character,
            "WaitDelete_Button"=>kind==RowKind.Pending,
            "Create_Button"=>kind==RowKind.Empty,
            _=>true,
        };
    }

    public bool Enabled(CharUiPiece p){
        if(p.MouseEvent is MsgOkEvent or MsgCancelEvent)return Shown!=CharSelectMessage.None;
        if(Shown!=CharSelectMessage.None)return false;
        if(p.MouseEvent==BackEvent)return true;
        if(!Ready)return false;
        var v=View!;
        return p.MouseEvent switch{
            SelectEvent=>true,
            CreateEvent=>v.CanCreate,
            UpEvent=>Scroll>0,
            DownEvent=>Scroll<MaxScroll,
            EnterEvent=>v.CanEnter&&SelectedCharacter!=null,
            DeleteEvent=>v.CanDelete&&SelectedCharacter!=null,
            _=>false,   // SlotLock_SelectWin: no 3.81 packet
        };
    }

    public string? LabelText(CharUiPiece p){
        if(p.Kind==CharUiKind.MultiLabel)return p.Name=="Message_Label"?MessageText:null;
        if(p.Kind!=CharUiKind.Label)return null;
        string N(long n)=>n.ToString(CultureInfo.InvariantCulture);
        if(p.ListName==SlotList&&p.Parent!=null&&RowCharacter(Scroll+p.Slot) is {} c)
            return p.Name switch{"Level_Label"=>N(c.Level),"Name_Label"=>c.Name,_=>null};
        if(p.ListName==InfoList&&SelectedCharacter is {} s)
            return p.Name switch{"Name_Label"=>s.Name,"Level_Label"=>N(s.Level),"Class_Label"=>ClassName(s.Class),"Pledge_Label"=>s.Clan,"Alignment_Label"=>N(s.Lawful),"Birthday_Label"=>N(s.CreateTime),_=>null};
        return p.TextKind=="Desc"||p.TextKind=="String"&&p.TextValue is {} t&&!t.StartsWith("dummy",StringComparison.Ordinal)?TextResolver?.Invoke(p.TextKind,p.TextValue!):null;
    }
    /// <summary>Class byte of S93: 0–6, or 32–38 while marked for 7-day deletion.</summary>
    public static string ClassName(int cls){
        int k=cls>=32?cls-32:cls;
        return k>=0&&k<CharacterManagementPackets.Classes.Count?CharacterManagementPackets.Classes[k]:cls.ToString(CultureInfo.InvariantCulture);
    }

    public static string DeleteText(LoginCharacter c)=>CharacterManagementPackets.PendingDeletion(c)?$"要取消「{c.Name}」的七日刪除排程嗎？":$"確定刪除「{c.Name}」嗎？\n伺服器可能立即刪除，或依等級設定七日刪除排程。";

    // ---- painting ---------------------------------------------------------------------------------------------------------

    public void Paint(UiCanvas canvas){
        if(!Visible||Main==null)return;
        PaintWindow(canvas,Main);
        if(Shown!=CharSelectMessage.None&&Message!=null)PaintWindow(canvas,Message);
    }
    void PaintWindow(UiCanvas canvas,CharUiWindow w){
        canvas.DrawSurface(w.Base,w.Origin.X,w.Origin.Y);
        foreach(var p in w.Pieces)if(ShownPiece(p))PaintPiece(canvas,w,p);
    }
    void PaintPiece(UiCanvas canvas,CharUiWindow w,CharUiPiece p){
        int x=w.Origin.X+p.Offset.X,y=w.Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case CharUiKind.Image:canvas.DrawSurface(p.Surface,x,y);break;
            case CharUiKind.Board:CharUiPaint.Board(canvas,p,x,y);break;
            case CharUiKind.Label:CharUiPaint.Label(canvas,p,x,y,LabelText(p));break;
            case CharUiKind.MultiLabel:CharUiPaint.MultiLabel(canvas,p,x,y,LabelText(p));break;
            case CharUiKind.Button:{
                bool enabled=Enabled(p);
                bool selected=p.MouseEvent==SelectEvent&&p.ListName==SlotList&&RowCharacter(Scroll+p.Slot)?.Name==Selected;
                var s=ReferenceEquals(p,pressed)&&pressedInside&&enabled&&p.Push!=null?p.Push:selected&&p.Push!=null?p.Push:ReferenceEquals(p,hover)&&pressed==null&&enabled&&p.Highlight!=null?p.Highlight:p.Surface;
                canvas.DrawSurface(s,x,y);break;
            }
        }
    }

    // ---- input ------------------------------------------------------------------------------------------------------------

    CharUiWindow? InputWindow=>Shown!=CharSelectMessage.None?Message:Main;
    public CharUiPiece? ButtonAt(Point location)=>Visible&&InputWindow is {} w?CharUiPaint.ButtonAt(w,location,ShownPiece):null;

    /// <summary>Full screen and modal: every mouse event goes here while the screen is shown.</summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.Kind==UiMouseKind.Move&&pressed==null){hover=ButtonAt(e.Location);return true;}
        if(e.Kind==UiMouseKind.Wheel){if(Shown==CharSelectMessage.None)Scroll=Math.Clamp(Scroll+(e.Delta>0?-1:e.Delta<0?1:0),0,MaxScroll);return true;}
        if(e.Button!=MouseButtons.Left)return true;
        var w=InputWindow!;
        switch(e.Kind){
            case UiMouseKind.Down:pressed=ButtonAt(e.Location);pressedInside=pressed!=null;return true;
            case UiMouseKind.Move:pressedInside=pressed!=null&&CharUiPaint.At(w,pressed).Contains(e.Location);return true;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;pressedInside=false;
                if(was!=null&&CharUiPaint.At(w,was).Contains(e.Location)&&Enabled(was))Fire(was,commands,e.Clicks);
                return true;
            }
        }
        return true;
    }

    /// <summary>Modal: Enter = message Ok / enter world, Esc = message Cancel, Up/Down = selection.</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(!e.Down||e.Repeat&&e.KeyCode is not (Keys.Up or Keys.Down))return true;
        switch(e.KeyCode){
            case Keys.Escape:if(Shown!=CharSelectMessage.None)Shown=CharSelectMessage.None;break;
            case Keys.Enter:
                if(Shown!=CharSelectMessage.None)FireEvent(MsgOkEvent,commands);
                else if(Main!.Pieces.FirstOrDefault(p=>p.MouseEvent==EnterEvent) is {} enter&&Enabled(enter))FireEvent(EnterEvent,commands);
                break;
            case Keys.Up or Keys.Down when Shown==CharSelectMessage.None&&View is {} v&&v.Characters.Count>0:{
                int i=Math.Max(0,v.Characters.ToList().FindIndex(c=>c.Name==Selected));
                i=Math.Clamp(i+(e.KeyCode==Keys.Up?-1:1),0,v.Characters.Count-1);
                Selected=v.Characters[i].Name;
                if(i<Scroll)Scroll=i;else if(i>=Scroll+Rows)Scroll=i-Rows+1;
                break;
            }
        }
        return true;
    }

    void FireEvent(string ev,IUiCommands commands){
        var w=Shown!=CharSelectMessage.None?Message:Main;
        if(w?.Pieces.FirstOrDefault(p=>p.MouseEvent==ev) is {} p)Fire(p,commands,1);
    }

    void Fire(CharUiPiece p,IUiCommands commands,int clicks){
        var v=View;
        switch(p.MouseEvent){
            case SelectEvent when RowCharacter(Scroll+p.Slot) is {} picked:{
                bool again=Selected==picked.Name;Selected=picked.Name;
                if(clicks>=2&&again&&v is {CanEnter:true})_=Run(h=>h.Enter(v,picked.Name),false);
                return;
            }
            case CreateEvent:OpenCreate();return;
            case UpEvent:Scroll=Math.Max(0,Scroll-1);return;
            case DownEvent:Scroll=Math.Min(MaxScroll,Scroll+1);return;
            case EnterEvent when v!=null&&Selected is {} entering:_=Run(h=>h.Enter(v,entering),false);return;
            case BackEvent:Host?.Logout();return;
            case DeleteEvent when SelectedCharacter is {} target:deleting=target.Name;ShowMessage(CharSelectMessage.ConfirmDelete,DeleteText(target));return;
            case MsgOkEvent:{
                var was=Shown;Shown=CharSelectMessage.None;
                if(was==CharSelectMessage.ConfirmDelete&&deleting is {} gone&&v!=null)_=Run(h=>h.Delete(v,gone),true);
                return;
            }
            case MsgCancelEvent:Shown=CharSelectMessage.None;return;
        }
        string id=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(id))UiResourceLog.Write($"select-char-ui {id} 尚未接上");
    }

    /// <summary>Create_Button: the native create screen when it is usable, otherwise the WinForms dialog.</summary>
    void OpenCreate(){
        if(Host==null)return;
        if(Create is {Available:true} c&&c.Begin(Host))return;
        Host.CreateFallback();
    }

    /// <summary>One action through the host; a refusal is shown in Message_Window. manage = wait for the server's result notice.</summary>
    internal async Task Run(Func<ICharSelectHost,Task<LoginAction>> action,bool manage){
        if(Pending||Host is not {} host)return;
        Pending=true;managing=manage;
        LoginAction r;
        try{r=await action(host);}
        catch(Exception ex){r=new(false,"操作未完成，請查看連線狀態。");UiResourceLog.Write($"select-char-ui 送出失敗：{ex.GetType().Name}: {ex.Message}");}
        Pending=false;
        if(!r.Accepted)managing=false;
        Sync();
        if(!r.Accepted&&View is {Shown:true})ShowMessage(CharSelectMessage.Notice,r.Message);
    }
}
