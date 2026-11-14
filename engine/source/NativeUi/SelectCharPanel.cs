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
// - Window size: declared Width/Height, else BaseSurf image, else Back_Image image. #1206: Character_Select_Window without a
//   declared size covers the whole canvas (FullScreen), Message_Window grows to hold its pieces (CoverPieces); see below.
// - "_Multi"/"_multi" images duplicate the "_Screen" ones; only the screen variant is built.
// - BigSlot_List has 4 Slot_Controls = the visible rows. Row r = scroll + slot index. r < characters → Select_Button (or
//   WaitDelete_Button + WaitToDel_Image when the server marks the character for 7-day deletion, class 32–38); r < Slots →
//   Create_Button; otherwise Slot_Lock_Back + Lock_Button (slot purchase: no 3.81 packet, disabled). Up/Down page (R10-N).
// - A double click on the selected character's Select_Button enters the world.
// - CharacterInfo_List: Name, Level, Class, Pledge (clan), Alignment (lawful) and Birthday of the selected character.
//
// R9-O (T375/#859, FU9§14.1; TW16 CSelectUI decompiled read-only, docs/reports/issue-859.md), 已驗證 in the binary:
// - sub_EF0060 looks for "Character_List" first; the kor25 SelectUI.xml has none (only BigSlot_List, this+360 set in
//   sub_EEC090), so TW16 takes its BigSlot branch sub_EF13F0 — the rules used here:
//   class surf c = sub_EEDD90(class, sex) = 6430 + 8×class + 4×sex while BigSlot_List exists (7049 + 6×class + 3×sex
//   without it; class 7: 8365/8368). Select_Button: normal c, highlight c+1, push c+2; the selected row: normal = highlight
//   = c+2. WaitDelete_Button (7-day pending): all three = c+3 (sub_EEDE30; class 7: 8371/8372). Slot Level_Label "Lv.%d".
//   6430–6489 are in the pinned local Sprite archives; a missing surf is not drawn.
// - The Character_List layout (sub_EF0060: Class_Image = c + 2 on the selected row, Select_Button 7045/7046/7047, the
//   selected row 7047 in every state) is not in any SelectUI.xml we have; CharacterListSurfs keeps the rule as data only.
// - Birthday_Label (sub_EECB10): sprintf("%d") of the value, then chars 0–3 '.' 4–5 '.' 6–7 (YYYY.MM.DD for an 8-digit
//   value; a shorter one stops at its end like the C string). Whether 3.81 S_CharPacks time is YYYYMMDD is 待驗證.
// R10-N (T398/#913; TW16 CSelectUI decompiled read-only, docs/reports/issue-913.md), 已驗證 in the binary:
// - Up/Down page the slot list (this+352 = page): Up_Button (sub_EF13A0) page−1 while > 0, Down_Button (sub_EECF90) page+1
//   while < 1, each followed by sub_EF13F0. Row = slot + rows × page (sub_EF13F0 i + v29 × page; the click handlers
//   sub_EEC3B0 / sub_EEFF10 add 4 × page). TW16 has exactly two pages (the cap 1 is a constant); the engine keeps that and only
//   adds pages when a server sends more rows than two pages hold (3.81 has 8 slots = 2 × 4, so never in practice).
// - sub_EF13F0 sets the two buttons' surfs on every refresh: page 0: Up 6411/6411/6411, Down 6413/6414/6414; page > 0:
//   Up 6411/6412/6412, Down 6413/6413/6413 (normal/highlight/push = sub_AFE580/sub_AFF270/sub_AFFB20 → sub_B0D810). These
//   replace the XML BaseSurf/HighlightSurf/PushSurf; 6411–6414 are in the pinned local Sprite07–10. A missing one is not drawn.
// - Keys (sub_EEDF00): Up/Down arrows move the selection and the page follows it (sub_EEEE60: page = index / 4); PageUp /
//   PageDown (0x1021 / 0x1022) page like the buttons. The mouse wheel pages too (推測: no wheel handler was found in CSelectUI).
// R11-D (T418/#960; TW16 CSelectUI decompiled read-only, docs/reports/issue-960.md), 已驗證 in the binary:
// - The selection this+344 is a row index, so an empty slot can be selected. CreateChar_SelectWin = sub_EEC3B0: 514 (click)
//   selects the row when it differs (sub_EF0060), 515 (double click) opens the create screen (sub_EEC4A0). Up/Down keys
//   move ±1 inside 0 … slots − 1 (sub_EEDF00 / sub_EEEE60: < byte_1542A97 and < 8) over characters and empty slots alike;
//   locked rows are never selected. Enter on an empty slot opens the create screen (sub_EEDF00 → sub_EEC4A0). Opening the
//   screen with nothing selected selects row 0 (sub_EF0490), i.e. the first empty slot when there is no character.
// - Create_Button surfs (sub_EF13F0, empty row): p = its XML PushSurf; the selected row: normal = highlight = p; otherwise
//   normal p − 2, highlight p − 1; push stays p. No PushSurf in the XML → its XML surfs are kept.
// - An empty slot selected: the six CharacterInfo_List labels are set to "" (sub_EECB10; the six strings at 0x11E3C63 …
//   0x11E3C77 are empty), Ok_SelectWin (sub_EEFDD0 → sub_EED8A0) and DeleteChar_SelectWin (sub_EEC6B0 → sub_EEC6D0) do
//   nothing (class −1). The engine keeps those three disabled / hidden, which shows the same.
// - The engine keeps the selection by name for characters (the list can change under it); an empty-slot selection is kept
//   by row and becomes the character that later appears in that row (TW16: the index simply points at it).
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
    /// <summary>A Button's XML PushSurf reference (R11-D: Create_Button derives its surfs from it, sub_EF13F0).</summary>
    public OptionImageRef? PushImage{get;init;}
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
/// Runtime (R10-N) marks an Image without an id whose picture the screen sets at run time: built with no surface, not skipped.
/// </summary>
internal sealed class CharUiBuilder(string file,Func<int,UiSurface?> surf,Func<UiNode,string?>? skip=null,Func<UiNode,bool>? runtime=null) {
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
            string? textKind=null,Color? color=null,int alpha=255,OptionImageRef? pushImage=null)=>new(){PushImage=pushImage,Kind=kind,Path=c.Path,Name=name,Owner=ctx.Owner,ListName=ctx.List,Slot=ctx.Slot,Parent=ctx.Parent,Offset=at,
            DeclaredSize=new(c.TryInt("Width")??0,c.TryInt("Height")??0),MouseEvent=c.Attr("MouseEvent"),
            Align=(c.Attr("Align")??c.Attr("HAlign")??"left").Trim().ToLowerInvariant(),VAlign=(c.Attr("VAlign")??"top").Trim().ToLowerInvariant(),LineMargin=c.TryInt("LineMargin")??0,
            Image=image,Surface=surface,Highlight=high,Push=push,TextKind=textKind,TextValue=textKind==null?null:c.Attr(textKind)!.Trim(),Color=color??Color.White,Alpha=alpha,
            Limit=c.TryInt("LimitText")??0};
        switch(c.Name){
            case "Image":{
                if(MultiVariant(name)){Skipped.Add(c.Path+"（multi 版，只建 screen 版）");return;}
                var r=ResetStatPanel.ImageRef(c,"Surf");
                if(r==null&&c.Children.Count==0&&runtime?.Invoke(c)!=true){Skipped.Add(c.Path+"（沒有圖號，執行期才定圖）");return;}
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
                var b=Piece(CharUiKind.Button,normal,ns,hs,ps,pushImage:push);
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
    [UiPanel(186)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,r=>new SelectCharPanel(r.Resources,r){TextResolver=RefClientTextSource.Resolver(r.Resources)});   // T327 (#765): SelectUI.xml from the reference client (P-A), loaded inside Load
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
    /// <summary>R11-D: the selected empty slot's row (TW16 this+344 on an empty row); null while a character (or nothing) is selected.</summary>
    public int? SelectedEmpty{get;private set;}
    /// <summary>R11-D: the selected row (TW16 this+344): the character's index, the empty slot's row, or -1.</summary>
    public int SelectedRow=>SelectedEmpty??(View is {} v&&Selected!=null?IndexOf(v,Selected):-1);
    static int IndexOf(CharSelectView v,string name){for(int i=0;i<v.Characters.Count;i++)if(v.Characters[i].Name==name)return i;return -1;}
    public CharSelectMessage Shown{get;private set;}
    public string? MessageText{get;private set;}
    public bool Pending{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    public Func<string,string,string?>? TextResolver{get;set;}
    Func<int,UiSurface?>? lookup;readonly Dictionary<int,UiSurface?> dynamic=[];readonly HashSet<int> dynamicMissing=[];

    // ---- R9-O formulas (sub_EEDD90 / sub_EEDE30 / sub_EF13F0 / sub_EF0060 / sub_EECB10) -------------------------------------

    /// <summary>sub_EEDD90: the class picture surf; -1 for an unknown class or sex.</summary>
    public static int ClassSurf(int cls,int sex,bool bigSlot){
        if(cls>6)return cls==7&&sex is 0 or 1?(sex==0?8365:8368):-1;
        if(cls<0||sex is <0 or >1)return -1;
        return bigSlot?6430+8*cls+4*sex:7049+6*cls+3*sex;
    }
    /// <summary>sub_EEDE30: WaitDelete_Button surf (all three states).</summary>
    public static int WaitDeleteSurf(int classSurf,int cls,int sex)=>cls is >=0 and <=6?classSurf+3:cls==7&&sex is 0 or 1?(sex==0?8371:8372):classSurf;
    /// <summary>sub_EF13F0: Select_Button (normal, highlight, push) of a BigSlot row.</summary>
    public static (int Normal,int Highlight,int Push) SlotSurfs(int classSurf,bool selected)=>selected?(classSurf+2,classSurf+2,classSurf+2):(classSurf,classSurf+1,classSurf+2);
    /// <summary>sub_EF0060 (Character_List layout, data only): Class_Image and Select_Button (normal, highlight, push).</summary>
    public static (int ClassImage,int Normal,int Highlight,int Push) CharacterListSurfs(int cls,int sex,bool selected){
        int c=ClassSurf(cls,sex,false);
        return selected?(c==-1?-1:c+2,7047,7047,7047):(c,7045,7046,7047);
    }
    /// <summary>sub_EECB10 Birthday_Label: "%d", then chars 0–3 '.' 4–5 '.' 6–7, cut at the first NUL like the C string.</summary>
    public static string Birthday(uint value){
        string s=unchecked((int)value).ToString(CultureInfo.InvariantCulture);
        char At(int i)=>i<s.Length?s[i]:'\0';
        var c=new[]{At(0),At(1),At(2),At(3),'.',At(4),At(5),'.',At(6),At(7)};
        int end=Array.IndexOf(c,'\0');
        return new string(c,0,end<0?c.Length:end);
    }
    /// <summary>sub_EF13F0: slot Level_Label.</summary>
    public static string SlotLevel(int level)=>"Lv."+level.ToString(CultureInfo.InvariantCulture);
    /// <summary>R10-N sub_EF13F0: Up_Button (normal, highlight, push) for a page.</summary>
    public static (int Normal,int Highlight,int Push) UpSurfs(int page)=>page<=0?(6411,6411,6411):(6411,6412,6412);
    /// <summary>R10-N sub_EF13F0: Down_Button (normal, highlight, push) for a page.</summary>
    public static (int Normal,int Highlight,int Push) DownSurfs(int page)=>page<=0?(6413,6414,6414):(6413,6413,6413);
    /// <summary>R11-D sub_EF13F0: Create_Button (normal, highlight, push) of an empty row from its XML PushSurf p.</summary>
    public static (int Normal,int Highlight,int Push) CreateSurfs(int push,bool selected)=>selected?(push,push,push):(push-2,push-1,push);
    /// <summary>R10-N: TW16's last page (sub_EECF90 / sub_EEDF00: page &lt; 1).</summary>
    public const int Tw16LastPage=1;
    /// <summary>Class index of a S93 class byte (32–38 = pending deletion).</summary>
    static int ClassIndex(int cls)=>cls>=32?cls-32:cls;

    /// <summary>(normal, highlight, push) surf ids of a character row's slot button; null when the class has no picture.</summary>
    public (int Normal,int Highlight,int Push)? RowSurfs(LoginCharacter c){
        int cls=ClassIndex(c.Class),surf=ClassSurf(cls,c.Sex,true);
        if(surf<0)return null;
        if(CharacterManagementPackets.PendingDeletion(c)){int w=WaitDeleteSurf(surf,cls,c.Sex);return (w,w,w);}
        return SlotSurfs(surf,c.Name==Selected);
    }
    /// <summary>R10-N: (normal, highlight, push) of Up_Button / Down_Button on the current page; null for other pieces.</summary>
    public (int Normal,int Highlight,int Push)? PageButtonSurfs(CharUiPiece p)=>p.MouseEvent switch{UpEvent=>UpSurfs(Page),DownEvent=>DownSurfs(Page),_=>null};
    UiSurface? Dynamic(int id){
        if(id<0||lookup==null)return null;
        if(!dynamic.TryGetValue(id,out var s)){dynamic[id]=s=lookup(id);if(s==null&&dynamicMissing.Add(id))UiResourceLog.Write($"select-char-ui 缺圖不畫：surf {id}");}
        return s;
    }

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
        Host=null;View=null;LayoutFile=null;Main=null;Message=null;Missing=[];Skipped=[];Unavailable="not loaded";Rows=0;lookup=null;dynamic.Clear();dynamicMissing.Clear();
        ResetState();
    }
    void ResetState(){Scroll=0;Selected=null;SelectedEmpty=null;Shown=CharSelectMessage.None;MessageText=null;Pending=false;deleting=null;closed=false;managing=false;pressed=null;hover=null;pressedInside=false;}

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
            bool declared=(w.TryInt("Width")??0)>0&&(w.TryInt("Height")??0)>0;
            if(name==MainWindow)main=FullScreen(built,declared);else message=CoverPieces(built);
        }
        if(main==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {MainWindow}");
        if(message==null)throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {MessageWindow}（無法確認刪除）");
        int rows=main.Pieces.Where(p=>p.ListName==SlotList&&p.Kind==CharUiKind.Button&&p.MouseEvent==SelectEvent&&p.Name=="Select_Button").Select(p=>p.Slot).Distinct().Count();
        if(rows==0)throw new UiLayoutException(layout.FileName,top.Path,$"{SlotList} 沒有 Select_Button 欄位");
        foreach(var ev in new[]{EnterEvent,BackEvent})if(!main.Pieces.Any(p=>p.MouseEvent==ev))throw new UiLayoutException(layout.FileName,top.Path,$"沒有 {ev} 按鈕");
        LayoutFile=layout.FileName;Main=main;Message=message;Missing=b.Missing;Skipped=b.Skipped;Rows=rows;lookup=surf;
        Unavailable=main.Size.IsEmpty?$"{layout.FileName}: {MainWindow} 沒有大小":message.Size.IsEmpty?$"{layout.FileName}: {MessageWindow} 底圖都缺，無法定出確認框範圍":null;
    }

    // ---- #1206 window geometry ---------------------------------------------------------------------------------------------
    // #1186 SM07: SelectUI.xml gives Character_Select_Window no Width/Height and BaseSurf 6421. In both clients 6421 is
    // Sprite08/6421.img, a 60×80 class portrait (0-客戶端 sha256 4E2BC47C…, kor25 1A5AB3F8…), so "size = BaseSurf" made the
    // whole screen a 60×80 box at (0,0): UiRoot.Compose clips Paint to Bounds and UiRoot.HitTest uses Bounds, so only that
    // portrait showed (top left of the SM07 capture), the world layer's "等待真實世界初始化" stayed visible, and clicks went to
    // the world. The select screen is a full-screen scene like CreateUI's two scenes (declared 800×600 in CreateUI.xml):
    // without a declared size the window now covers the logical canvas from its origin. Message_Window (BaseSurf 6488, also
    // a 60×80 .img) is widened to hold its own pieces (Message_Label 394×190, Ok/Cancel at Y 237), so the delete confirm and the
    // result notice can be read and clicked. Whether TW16 draws 6421 / 6488 at all is 待驗證; they are drawn as the XML says.

    /// <summary>#1206: a window without a declared Width/Height covers the logical canvas from its origin.</summary>
    internal static CharUiWindow FullScreen(CharUiWindow w,bool declared){
        if(declared)return w;
        var size=new Size(UiRoot.LogicalWidth-w.Origin.X,UiRoot.LogicalHeight-w.Origin.Y);
        return size.Width>0&&size.Height>0?w with{Size=size}:w;
    }
    /// <summary>#1206: a sized window grows to hold its pieces (offsets ≥ 0); an unsized one stays empty (→ unavailable).</summary>
    internal static CharUiWindow CoverPieces(CharUiWindow w){
        if(w.Size.IsEmpty)return w;
        int width=w.Size.Width,height=w.Size.Height;
        foreach(var p in w.Pieces){
            var r=p.HitLocal;
            if(r.IsEmpty||r.X<0||r.Y<0)continue;
            width=Math.Max(width,r.Right);height=Math.Max(height,r.Bottom);
        }
        return width==w.Size.Width&&height==w.Size.Height?w:w with{Size=new(width,height)};
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
        // R11-D: an empty-slot selection stays on its row; a character that now fills the row takes it (TW16 index).
        if(SelectedEmpty is int e){
            var kind=Row(e);
            if(kind is RowKind.Character or RowKind.Pending){Selected=v.Characters[e].Name;SelectedEmpty=null;}
            else if(kind!=RowKind.Empty)SelectedEmpty=null;
        }
        if(SelectedEmpty==null&&(Selected==null||!v.Characters.Any(c=>c.Name==Selected))){
            Selected=v.Characters.FirstOrDefault()?.Name;
            if(Selected==null&&Row(0)==RowKind.Empty)SelectedEmpty=0;   // sub_EF0490: nothing selected → row 0
        }
        Scroll=Math.Clamp(Page,0,MaxPage)*Rows;
        if(Shown==CharSelectMessage.ConfirmDelete&&(deleting==null||!v.Characters.Any(c=>c.Name==deleting)))Shown=CharSelectMessage.None;
    }

    void ShowMessage(CharSelectMessage kind,string text){Shown=kind;MessageText=text;pressed=null;}

    public int TotalRows=>View is not {} v?Rows:Math.Max(Rows,Math.Max(v.Slots,v.Characters.Count));
    /// <summary>R10-N: this+352. Scroll is always Page × Rows.</summary>
    public int Page=>Rows>0?Scroll/Rows:0;
    /// <summary>R10-N: TW16's two pages (0, 1); more only when the list holds more than two pages of rows.</summary>
    public int MaxPage=>Rows>0?Math.Max(Tw16LastPage,(TotalRows+Rows-1)/Rows-1):0;
    public int MaxScroll=>MaxPage*Rows;
    void SetPage(int page)=>Scroll=Math.Clamp(page,0,MaxPage)*Rows;
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
            CreateEvent=>true,   // R11-D: a click selects the empty slot; the double click opens the create screen only while CanCreate
            UpEvent=>Page>0,
            DownEvent=>Page<MaxPage,
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
            return p.Name switch{"Level_Label"=>SlotLevel(c.Level),"Name_Label"=>c.Name,_=>null};
        if(p.ListName==InfoList&&SelectedCharacter is {} s)
            return p.Name switch{"Name_Label"=>s.Name,"Level_Label"=>N(s.Level),"Class_Label"=>ClassName(s.Class),"Pledge_Label"=>s.Clan,"Alignment_Label"=>N(s.Lawful),"Birthday_Label"=>Birthday(s.CreateTime),_=>null};
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
        // #1206: a full-screen modal scene. Back_Image (X 0, Y 0, no Surf) is set at run time in TW16 and is not built (待驗證),
        // so the area under the screen is cleared to black instead of letting the world layer / lower windows show through.
        canvas.Graphics.FillRectangle(Brushes.Black,Main.Bounds);
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
            case CharUiKind.Button when p.ListName==SlotList&&p.Name is "Select_Button" or "WaitDelete_Button"&&RowCharacter(Scroll+p.Slot) is {} rc:{
                // R9-O (sub_EF13F0): the class picture of the character; selection / pending as in RowSurfs.
                bool enabled=Enabled(p);
                if(RowSurfs(rc) is not (int n,int h,int push))break;
                int id=ReferenceEquals(p,pressed)&&pressedInside&&enabled?push:ReferenceEquals(p,hover)&&pressed==null&&enabled?h:n;
                canvas.DrawSurface(Dynamic(id),x,y);break;
            }
            case CharUiKind.Button when p.ListName==SlotList&&p.Name=="Create_Button"&&p.PushImage is {IsSurf:true} pushRef:{
                // R11-D (sub_EF13F0): surfs from the XML PushSurf; the selected empty row shows it in every state.
                bool enabled=Enabled(p);
                var (n,h,push)=CreateSurfs(pushRef.Id,SelectedEmpty==Scroll+p.Slot);
                int id=ReferenceEquals(p,pressed)&&pressedInside&&enabled?push:ReferenceEquals(p,hover)&&pressed==null&&enabled?h:n;
                canvas.DrawSurface(Dynamic(id),x,y);break;
            }
            case CharUiKind.Button when p.MouseEvent is UpEvent or DownEvent:{
                // R10-N (sub_EF13F0): the page decides the surfs; the XML ones are replaced.
                var (n,h,push)=PageButtonSurfs(p)!.Value;
                bool enabled=Enabled(p);
                int id=ReferenceEquals(p,pressed)&&pressedInside&&enabled?push:ReferenceEquals(p,hover)&&pressed==null&&enabled?h:n;
                canvas.DrawSurface(Dynamic(id),x,y);break;
            }
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
        if(e.Kind==UiMouseKind.Wheel){if(Shown==CharSelectMessage.None)SetPage(Page+(e.Delta>0?-1:e.Delta<0?1:0));return true;}
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

    /// <summary>Modal: Enter = message Ok / enter world, Esc = message Cancel, Up/Down = selection (the page follows),
    /// PageUp/PageDown = page (R10-N, sub_EEDF00).</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(!e.Down||e.Repeat&&e.KeyCode is not (Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown))return true;
        switch(e.KeyCode){
            case Keys.Escape:if(Shown!=CharSelectMessage.None)Shown=CharSelectMessage.None;break;
            case Keys.Enter:
                if(Shown!=CharSelectMessage.None)FireEvent(MsgOkEvent,commands);
                else if(SelectedEmpty!=null){if(Ready)OpenCreate();}   // R11-D: sub_EEDF00 13 on an empty slot → sub_EEC4A0
                else if(Main!.Pieces.FirstOrDefault(p=>p.MouseEvent==EnterEvent) is {} enter&&Enabled(enter))FireEvent(EnterEvent,commands);
                break;
            case Keys.Up or Keys.Down when Shown==CharSelectMessage.None&&View is {} v:{
                // R11-D (sub_EEDF00 4134 / 4136 → sub_EEEE60): ±1 inside the slot rows, characters and empty slots alike.
                int i=SelectedRow+(e.KeyCode==Keys.Up?-1:1);
                if(i>=0&&i<Math.Max(v.Slots,v.Characters.Count)&&SelectRow(i)&&Rows>0)SetPage(i/Rows);   // sub_EEEE60: page = index / 4
                break;
            }
            case Keys.PageUp when Shown==CharSelectMessage.None&&Ready:SetPage(Page-1);break;
            case Keys.PageDown when Shown==CharSelectMessage.None&&Ready:SetPage(Page+1);break;
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
                bool again=Selected==picked.Name;Selected=picked.Name;SelectedEmpty=null;
                if(clicks>=2&&again&&v is {CanEnter:true})_=Run(h=>h.Enter(v,picked.Name),false);
                return;
            }
            case CreateEvent:{
                // R11-D sub_EEC3B0: 514 selects the empty slot (sub_EF0060); 515 opens the create screen (sub_EEC4A0).
                if(!SelectRow(Scroll+p.Slot))return;
                if(clicks>=2)OpenCreate();
                return;
            }
            case UpEvent:SetPage(Page-1);return;     // sub_EF13A0
            case DownEvent:SetPage(Page+1);return;   // sub_EECF90
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

    /// <summary>R11-D sub_EF0060 / sub_EEEE60: select a character or empty-slot row (locked rows: false, nothing changes).</summary>
    bool SelectRow(int row){
        switch(Row(row)){
            case RowKind.Character or RowKind.Pending:Selected=View!.Characters[row].Name;SelectedEmpty=null;return true;
            case RowKind.Empty:Selected=null;SelectedEmpty=row;return true;
            default:return false;
        }
    }

    /// <summary>Create_Button double click / Enter on an empty slot: the native create screen when it is usable, otherwise the
    /// WinForms dialog. Nothing while the list cannot take a new character (CanCreate false).</summary>
    void OpenCreate(){
        if(Host==null||View is not {CanCreate:true})return;
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
