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
//   class is 待驗證). Front_Image / Back_Image of the class scene have no image id (runtime artwork) → not built.
// - Stat_Change_Control: the six stat Controls carry Point + Down/Up (Strength, Dexterity, Constitution, Wisdom, Charisma,
//   Intelligence = CharacterCreation order). Up while points remain and the value is below base+points; Down above base.
//   Remain_Point = 75 − sum. Reset_Button = class base values.
// - Not built: Customizing_Win (derived bonus table, the engine has no values — same as #763), Gauge_Group (bar lengths,
//   no formula). StatType_Group: only the selected class's Attack/Defense/Mix buttons are shown; there are no preset
//   tables in the engine, so they are disabled (待做), and so is Customizing_Button.
// - Username_Edit takes typed characters (IUiTextInput, WM_CHAR after IME) up to LimitText characters; Backspace deletes.
//   Enter = OK on the stat scene / Next on the class scene; Esc = Prev.
// - OK: a local Validate error, a refused send or the server's refusal notice is shown in Message_Window (Ok/Cancel close
//   it). When the server confirms, the new character is in the list and the screen closes back to the select screen.

public enum CreateScene { Class, Stat }

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
    bool open,waiting;int generation;string? sentName;byte[] draft=new byte[6];int nameLimit;
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
        try{Load(layout,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"create-char-ui {layout.FileName} 版面不符，建角畫面停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"create-char-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"create-char-ui {Unavailable}");
    }

    void Clear(){
        open=false;Host=null;View=null;LayoutFile=null;ClassScene=null;StatScene=null;Message=null;Missing=[];Skipped=[];Unavailable="not loaded";nameLimit=0;
        ResetState(0);
    }
    void ResetState(int cls){
        Scene=CreateScene.Class;Class=cls;Sex=0;Name="";draft=CharacterManagementPackets.BaseStats(cls);Pending=false;MessageText=null;waiting=false;sentName=null;
        pressed=null;hover=null;pressedInside=false;
    }

    static string? SkipReason(UiNode n)=>n.Attr("Name") switch{
        "Warrior_Button"=>"3.81 沒有戰士職業",
        "Customizing_Win"=>"能力加成表，引擎沒有數值，不建（待做）",
        "Gauge_Group"=>"能力量表長度沒有對照，不建（待做）",
        _=>n.Name=="Tooltip"?"說明字串不建":null,
    };

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var top=layout.Root;
        if(top.Name!=RootNode)throw new UiLayoutException(layout.FileName,top.Path,$"根元素不是 {RootNode}");
        var b=new CharUiBuilder(layout.FileName,surf,SkipReason);
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
    public void Close(){open=false;MessageText=null;pressed=null;hover=null;pressedInside=false;}

    public void Update(IUiGameState state)=>Sync();

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

    public bool ShownPiece(CharUiPiece p){
        if(p.Parent!=null&&!ShownPiece(p.Parent))return false;
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
            _=>false,   // Auto_StatSettingScene / Customizing_StatSettingScene: no preset tables or bonus values in the engine
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
            case CharUiKind.Image:canvas.DrawSurface(p.Surface,x,y);break;
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
                bool selected=p.MouseEvent==ClassEvent&&p.Name!=null&&ClassButtons.TryGetValue(p.Name,out int k)&&k==Class;
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
                if(cls!=Class){Class=cls;draft=CharacterManagementPackets.BaseStats(cls);}
                return;
            case MaleEvent:Sex=0;return;
            case FemaleEvent:Sex=1;return;
            case NextEvent:Scene=CreateScene.Stat;return;
            case PrevStatEvent:Scene=CreateScene.Class;return;
            case PrevClassEvent:Close();return;
            case UpEvent when stat>=0:draft[stat]++;return;
            case DownEvent when stat>=0:draft[stat]--;return;
            case ResetEvent:draft=CharacterManagementPackets.BaseStats(Class);return;
            case OkEvent:Submit();return;
            case MsgOkEvent or MsgCancelEvent:MessageText=null;return;
        }
        string id=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(id))UiResourceLog.Write($"create-char-ui {id} 尚未接上");
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
