using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// T330 (#768, P-D of docs/reference/pak-resources.md §6, row 4:OptionUI): the native option window Lineage::OptionUI.
// OptionUI.xml is not in 0-客戶端 (PAK§4.2: all paks checked); it is read at run time from the reference client through
// UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak; SHA-256 logged as "ui-xml-ref").
// Entry (R9-Q #861, #818 ruling): menu button 6 and Ctrl+V open this window in the native UI (MenuButtonsPanel); when it is
// unavailable (no reference OptionUI.xml, faulted) they fall back to the HTML option1 page, and --classic-ui keeps option1.
// The option2 HTML page (menu 5, Ctrl+C) is not touched.
//
// Loading. UiRoot.Build is all-or-nothing, so the registration uses the host layout "uistatus86" (already loaded for the bar,
// same as LocationListPanel) and Load reads OptionUI.xml itself: no reference client / no XML / malformed XML → the panel
// records Unavailable, never opens, and the rest of the native UI keeps working. The existing WinForms entry (dev tools
// 「選項」 page) and the --classic-ui path are not changed.
//
// Images. The XML draws mostly with Png attributes (BasePng / PushPng / Png / FramePng, "id;flag"), plus 8 surfs (Close 6125/
// 6126, shortcut scroll bar 2040–2045). Surfs go through SurfCatalog.Get (local, then the T322/#760 reference fallback).
// Pngs: OptionPngSource looks for "<id>c.png" then "<id>.png", each in Sprite{h} then Sprite (h = SurfCatalog.NameHash),
// first in the pinned local Sprite archives, then in the reference client's Sprite*.idx/.pak (read-only, idx and payload
// SHA-256 logged as "option-png"). The "c first" order and the hash rule are carried over from .img (推測); every kor25 hit
// matches the hash (issue-768 report). Missing images are not drawn (no substitute); they are listed in Missing.
//
// Layout rules (推測, the binder sub_919F30 / sub_919170 was not decompiled for this): coordinates are relative to the parent
// element and a missing X/Y is 0; the window size is the BasePng image (550×316 in kor25), else the largest declared
// Control Width/Height; FramePng is drawn at the window origin; the page shown on open is "Home"; tab button "<Page>Btn"
// shows page <Page> and is drawn with its PushPng while that page is shown; Moveable="30" = the top 30 px drag the window;
// ComboBox lists (UpDownList) stay collapsed; the shortcut List rows (Caption="name0" placeholders) are filled at run time
// in the original and are not drawn. Labels carry Desc/String ids of the reference client's text tables; they are drawn
// only through TextResolver. #1208 (SM57): the check-button names of every page are such Desc ids (Home: 18805 18806 18803
// 20826 18808 18807 18813 20829), not run-time Captions; kor25 stores "dummyNNNN" at those ids, so the product resolver
// is RefClientTextFill (placeholders filled from a second client whose ids line up; docs/reports/issue-1208.md).
// Not wired (待做): the radio/scroll/combo values, Help, Init, ChangeWarehousePassword — clicks on them do nothing.
//
// R9-Q (T377/#861, FU9§14.4): check buttons that match a setting the engine already has are wired, and a click takes effect
// at once (the original toggles its working copy dword_13D43E4+732+k and writes it straight back through sub_9184B0; OkBtn
// and CloseBtn only close, there is no cancel/restore). Matched by MouseEvent, so the same option on two pages (Home and
// Game / Etc) is one setting:
//   ShowCharacterHPMPBtn (byte_138BE7C), HPMPBarThickBtn, HPMPBarRecoveryBtn, HPMPBarPercentBtn → HpMpPanel.Settings
//     (HpMpPanel.Apply; the over-head bars read the same settings) — held by this panel through the UiRoot;
//   ShowNpcNameBtn (byte_138BE7F, "全部顯示名稱"), CheckMute (master mute), EnableElfTelControlBtn (byte_13A931C →
//     WorldSession.ElfBookmarkTeleportOption) → IOptionWindowHost (ClientForm, RingWindows.cs).
// A wired check button is drawn with its PushPng while the setting is on (推測: the original's checked look was not traced).
//
// R10-I (T393/#908, gap-matrix 15.12.6): the rest of the options that match an engine feature.
//   ShowDropItemNameBtn (byte_138BE7E) → ground item names (ObjectRendering.cs; off = only the selected item's name, 推測);
//   TransparentDeadMonsterBtn (byte_138BFAA) → dead monsters drawn half transparent (ObjectRendering.cs, alpha 16/31 推測);
//   EnableElfTelControlBtn is now kept in the option file and cleared below Lv 50 (0xEC99EB/0xEED7B9, RingWindows.cs).
//   These three are saved in OptionSettingsFile (engine user-data "option-ui.txt"; the original's settings file
//   sub_59DD30/sub_59E920 is not reproduced, its format was not traced).
//   Volume bars: the horizontal ScrollBar's Move_Button is a slider thumb. Music/Effect/Env bars all show and set the one
//   engine master gain (PlayerAudioSettings; the engine has no separate music/effect/environment gains). VoiceVolume (3.81
//   has no voice chat) and WindowAlpha stay unwired. Thumb travel = ScrollWidth − thumb width, value 0–1 in 0.01 steps
//   (推測: LScrollBar's range for these bars was not traced). A press sets the value at the pointer, dragging follows it.
// Still not wired: the TW16-only options (Unhandled diagnostics).

/// <summary>"BasePng=\"10814;1\"" → Attr BasePng, Id 10814, Flag 1 (Flag 0 when absent; meaning of the flag 待驗證).</summary>
public sealed record OptionImageRef(string Attr,int Id,int Flag) {
    public bool IsSurf=>Attr.EndsWith("Surf",StringComparison.Ordinal);
    public override string ToString()=>$"{(IsSurf?"surf":"png")} {Id}";
}

public enum OptionPieceKind { Image, Board, Label, Button, Slider }

/// <summary>
/// One element of the window or of a page, in XML order. Offset is relative to the window origin. Surface null = missing
/// image (not drawn). Buttons hit-test on DeclaredSize when the XML gives Width/Height, otherwise on the image size.
/// R10-I (#908): a Slider is a horizontal ScrollBar (Offset/DeclaredSize = the bar); Image/Surface = its Move_Button thumb,
/// Thumb = the thumb's own X/Y, ScrollStart/ScrollWidth = the XML attributes (travel span). MouseEvent = the bar's Name.
/// </summary>
public sealed record OptionPiece(OptionPieceKind Kind,string Path,string Element,string? Name,Point Offset,Size DeclaredSize,
    OptionImageRef? Image,UiSurface? Surface,OptionImageRef? PushImage,UiSurface? PushSurface,string? MouseEvent,
    string? TextKind,string? TextValue,Color Color,int Alpha=255,Point Thumb=default,int ScrollStart=0,int ScrollWidth=0) {
    public Size ImageSize=>Surface==null?Size.Empty:new(Surface.Width,Surface.Height);
    public Rectangle HitLocal=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
    public bool Clickable=>(Kind is OptionPieceKind.Button or OptionPieceKind.Slider)&&HitLocal.Width>0&&HitLocal.Height>0;
    /// <summary>Slider: pixels the thumb can move (span − thumb width, never negative).</summary>
    public int Travel=>Math.Max(0,(ScrollWidth>0?ScrollWidth:DeclaredSize.Width)-ImageSize.Width);
}

/// <summary>A top-level Control of the window (Home, Application, UI, Game, Etc, Shortcut).</summary>
public sealed record OptionPage(string Name,string? EnableEvent,IReadOnlyList<OptionPiece> Pieces);

/// <summary>
/// R9-Q (#861): options held outside the native UI (ClientForm / WorldSession). Keyed by the check button's MouseEvent.
/// Value null = the host does not hold that option (the click stays Unhandled); Set false = not applied (e.g. audio closed).
/// </summary>
public interface IOptionWindowHost {
    bool? Value(string mouseEvent);
    bool Set(string mouseEvent,bool value);
    /// <summary>R10-I (#908): value 0–1 of a slider (ScrollBar Name); null = the host does not hold it.</summary>
    double? Level(string bar)=>null;
    /// <summary>R10-I (#908): sets a slider value; false = not applied (e.g. audio closed).</summary>
    bool SetLevel(string bar,double value)=>false;
}

/// <summary>
/// R10-I (#908): options the original keeps in its settings file (sub_59DD30 load / sub_59E920 save), kept by the engine
/// in its own user-data file "option-ui.txt", one "key=0|1" line each. FilePath null (offline checks) = nothing is read or
/// written. Unreadable / unwritable files are ignored (defaults are used, the change stays in memory).
/// </summary>
public static class OptionSettingsFile {
    public const string FileName="option-ui.txt";
    public const string DropItemNamesKey="show-drop-item-names",TransparentDeadMonstersKey="transparent-dead-monsters",ElfTeleportKey="elf-teleport-control";
    public static string? FilePath{get;set;}
    static readonly object gate=new();
    /// <summary>"a=1\nb=0" → {a:true,b:false}. Lines that are not key=0/1 are ignored (a later line wins).</summary>
    public static Dictionary<string,bool> Parse(string text){
        var map=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(var raw in text.Split('\n')){
            var line=raw.Trim();int eq=line.IndexOf('=');
            if(eq<=0)continue;
            string key=line[..eq].Trim(),value=line[(eq+1)..].Trim();
            if(value is "0" or "1")map[key]=value=="1";
        }
        return map;
    }
    /// <summary>Sorted "key=0|1" lines (stable file content).</summary>
    public static string Format(IReadOnlyDictionary<string,bool> values)=>
        string.Concat(values.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>$"{p.Key}={(p.Value?1:0)}\n"));
    static Dictionary<string,bool> ReadAll(string path){
        try{return File.Exists(path)?Parse(File.ReadAllText(path)):new(StringComparer.Ordinal);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return new(StringComparer.Ordinal);}
    }
    public static bool? Read(string key){
        if(FilePath is not {} p)return null;
        lock(gate)return ReadAll(p).TryGetValue(key,out var v)?v:null;
    }
    /// <summary>Writes one key, keeping the others. false = no file configured or the write failed.</summary>
    public static bool Write(string key,bool value){
        if(FilePath is not {} p)return false;
        lock(gate){
            var all=ReadAll(p);all[key]=value;
            try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(p))!);File.WriteAllText(p,Format(all));return true;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return false;}
        }
    }
}

public sealed class OptionPanel(UiResources? resources=null,Func<HpMpPanel?>? hpMp=null):IUiMovablePanel,IUiClosablePanel {
    // T330 (#768): OptionUI.xml from the reference client (P-A), loaded inside Load. #1208: label text = reference tables with
    // the kor25 "dummyNNNN" placeholders filled from a second client (RefClientTextFill, RefClientTextSource.cs).
    [UiPanel(176)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new OptionPanel(root.Resources,()=>HpMpWindow(root)){TextResolver=RefClientTextFill.Resolver(root.Resources)});
    public const string PanelId="OptionUI";

    // R9-Q (#861): MouseEvent names of the wired check buttons (kor25 OptionUI.xml, SHA-256 fe32d2fd…c8846d; FU9§14.4).
    public const string ShowCharacterHpMpEvent="ShowCharacterHPMPBtn",HpMpBarThickEvent="HPMPBarThickBtn",HpMpBarRecoveryEvent="HPMPBarRecoveryBtn",
        HpMpBarPercentEvent="HPMPBarPercentBtn",ShowNpcNameEvent="ShowNpcNameBtn",MuteEvent="CheckMute",ElfTeleportEvent="EnableElfTelControlBtn";
    /// <summary>Options this panel changes itself (HpMpPanel settings).</summary>
    public static readonly IReadOnlyList<string> HpMpEvents=[ShowCharacterHpMpEvent,HpMpBarThickEvent,HpMpBarRecoveryEvent,HpMpBarPercentEvent];
    /// <summary>Options IOptionWindowHost changes (ClientForm: name display, audio, WorldSession elf option).</summary>
    public static readonly IReadOnlyList<string> HostEvents=[ShowNpcNameEvent,MuteEvent,ElfTeleportEvent];
    /// <summary>R10-I (#908): display options IOptionWindowHost changes (ObjectRendering.cs switches).</summary>
    public const string ShowDropItemNameEvent="ShowDropItemNameBtn",TransparentDeadMonsterEvent="TransparentDeadMonsterBtn";
    public static readonly IReadOnlyList<string> DisplayEvents=[ShowDropItemNameEvent,TransparentDeadMonsterEvent];
    /// <summary>R10-I (#908): volume ScrollBar names (kor25 OptionUI.xml, Application page).</summary>
    public const string MusicVolumeBar="MusicVolumeScrollBar",EffectVolumeBar="EffectVolumeScrollBar",EnvVolumeBar="EnvVolumeScrollBar",VoiceVolumeBar="VoiceVolumeScrollBar";
    /// <summary>Bars that show and set the engine master gain (one gain for all engine sound).</summary>
    public static readonly IReadOnlyList<string> MasterVolumeBars=[MusicVolumeBar,EffectVolumeBar,EnvVolumeBar];
    /// <summary>Option controls FU9§14.4 lists as matching engine features but which have no engine setting to change (reason).</summary>
    public static readonly IReadOnlyDictionary<string,string> NotWired=new Dictionary<string,string>(StringComparer.Ordinal){
        [VoiceVolumeBar]="3.81 沒有語音聊天，引擎沒有語音音量",
    };
    public const string SliderThumb="Move_Button";

    /// <summary>The HP/MP window of the root when it is usable (registered, not faulted); else null.</summary>
    public static HpMpPanel? HpMpWindow(UiRoot root)=>root.Find(HpMpPanel.PanelId) is HpMpPanel h&&!root.Faulted.Contains(h)?h:null;
    public static bool? HpMpValue(HpMpSettings s,string mouseEvent)=>mouseEvent switch{
        ShowCharacterHpMpEvent=>s.ShowCharacterHpMp,HpMpBarThickEvent=>s.Thickness,HpMpBarRecoveryEvent=>s.ShowRecovery,HpMpBarPercentEvent=>s.ShowPercent,_=>null};
    public static HpMpSettings? HpMpWith(HpMpSettings s,string mouseEvent,bool value)=>mouseEvent switch{
        ShowCharacterHpMpEvent=>s with{ShowCharacterHpMp=value},HpMpBarThickEvent=>s with{Thickness=value},
        HpMpBarRecoveryEvent=>s with{ShowRecovery=value},HpMpBarPercentEvent=>s with{ShowPercent=value},_=>null};

    /// <summary>R9-Q (#861): ClientForm options (attached by RingWindows.cs while the native UI is on); null = not attached.</summary>
    public IOptionWindowHost? Host{get;private set;}
    public void Attach(IOptionWindowHost host)=>Host=host;

    /// <summary>Current value of the option behind a check button's MouseEvent; null = no engine setting (not wired / no host).</summary>
    public bool? Checked(string mouseEvent){
        if(HpMpValue(HpMpSettings.Default,mouseEvent)!=null)return hpMp?.Invoke() is {} h?HpMpValue(h.Settings,mouseEvent):null;
        return Host?.Value(mouseEvent);
    }
    /// <summary>Flips the option at once (the original writes back on every click). false = nothing to change, nothing changed.</summary>
    public bool ToggleSetting(string mouseEvent){
        if(HpMpValue(HpMpSettings.Default,mouseEvent)!=null){
            if(hpMp?.Invoke() is not {} h||HpMpValue(h.Settings,mouseEvent) is not bool on)return false;
            h.Apply(HpMpWith(h.Settings,mouseEvent,!on)!);
            return true;
        }
        return Host is {} host&&host.Value(mouseEvent) is bool v&&host.Set(mouseEvent,!v);
    }
    /// <summary>R10-I (#908): slider value 0–1 (ScrollBar Name); null = not wired (no host / host does not hold it).</summary>
    public double? Level(string bar)=>Host?.Level(bar) is double v&&double.IsFinite(v)?Math.Clamp(v,0,1):null;
    /// <summary>Thumb left edge, window-relative, for a slider at value v (missing value = the XML position).</summary>
    public static int ThumbX(OptionPiece p,double? v)=>p.Offset.X+(v is double x?p.ScrollStart+PlayerAudioSettings.SliderPosition(x,p.Travel):p.Thumb.X);
    /// <summary>Value for a pointer at logical x: thumb centred on the pointer, clamped to the travel.</summary>
    public double ValueAt(OptionPiece p,int x)=>PlayerAudioSettings.SliderGain(x-(Origin.X+p.Offset.X+p.ScrollStart)-p.ImageSize.Width/2,p.Travel);
    bool Slide(OptionPiece p,int x){
        if(p.MouseEvent is not {} bar||Host is not {} host||Level(bar) is not double now)return false;
        double v=ValueAt(p,x);
        if(v==now)return true;
        if(!host.SetLevel(bar,v))UiResourceLog.Write($"option-ui {bar} 這次未套用（設定暫不可改）");
        return true;
    }
    public const string LayoutName="OptionUI",WindowNode="Window",WindowName="Option",HomePage="Home",TabSuffix="Btn",CloseEvent="CloseBtn";
    /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
    public const string HostLayout="uistatus86";

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open (no XML, malformed XML, no size); null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    /// <summary>The XML name actually read (e.g. "OptionUI.xml" from the reference Tile.pak).</summary>
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public int MoveableHeight{get;private set;}
    public Rectangle Bounds=>Size.IsEmpty?Rectangle.Empty:new(Origin,Size);
    public bool Visible=>open;
    public OptionPiece? BaseImage{get;private set;}
    public OptionPiece? FrameImage{get;private set;}
    /// <summary>Window-level pieces (tab buttons, Help, Close, Init), XML order.</summary>
    public IReadOnlyList<OptionPiece> WindowPieces{get;private set;}=[];
    public IReadOnlyList<OptionPage> Pages{get;private set;}=[];
    public OptionPage? ActivePage{get;private set;}
    /// <summary>Images named in the XML but not found (not drawn): "png 10914 (OptionUI/Window/Control[0]/Image[0])".</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>Elements deliberately not built (collapsed lists, runtime rows, tooltips, unknown names), by path.</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    /// <summary>(kind "Desc"/"String", id) → text; null result or null resolver = the label is not drawn.</summary>
    public Func<string,string,string?>? TextResolver{get;set;}
    /// <summary>MouseEvent names clicked that the engine does not handle (diagnostics, each once).</summary>
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    bool open;OptionPiece? pressed;bool pressedInside,sliding;readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"option-ui 無法載入 {LayoutName}，選項窗停用：{ex.Message}");return;}
        var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
        try{Load(layout,surfs.Get,png.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"option-ui {layout.FileName} 版面不符，選項窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"option-ui 缺圖不畫：{string.Join("、",Missing)}");
    }

    void Clear(){
        open=false;pressed=null;pressedInside=false;sliding=false;LayoutFile=null;Origin=Point.Empty;Size=Size.Empty;MoveableHeight=0;BaseImage=FrameImage=null;
        WindowPieces=[];Pages=[];ActivePage=null;Missing=[];Skipped=[];Unavailable="not loaded";
    }

    /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        Clear();
        var window=layout.Root.Child(WindowNode);
        if(window.Attr("Name")!=WindowName)throw new UiLayoutException(layout.FileName,window.Path,$"Window Name 不是 {WindowName}");
        var missing=new List<string>();var skipped=new List<string>();
        UiSurface? Resolve(OptionImageRef? r,string path){
            if(r==null)return null;
            var s=r.IsSurf?surf(r.Id):png(r.Id);
            if(s==null)missing.Add($"{r} ({path})");
            return s;
        }
        var windowPieces=new List<OptionPiece>();var pages=new List<OptionPage>();
        foreach(var c in window.Children){
            if(c.Name=="Control"){
                string name=c.Attr("Name")??throw new UiLayoutException(layout.FileName,c.Path,"頁面 Control 沒有 Name");
                if(pages.Any(p=>p.Name==name))throw new UiLayoutException(layout.FileName,c.Path,$"頁面 {name} 重複");
                var pieces=new List<OptionPiece>();
                Walk(c,Rel(c,Point.Empty),pieces,skipped,Resolve);
                pages.Add(new(name,c.Attr("EnableEvent"),pieces));
            }else Element(c,Point.Empty,windowPieces,skipped,Resolve);
        }
        if(pages.Count==0)throw new UiLayoutException(layout.FileName,window.Path,"沒有任何頁面 Control");
        var baseRef=ImageAttr(window,"BasePng");var frameRef=ImageAttr(window,"FramePng");
        var baseSurface=Resolve(baseRef,window.Path+"/@BasePng");var frameSurface=Resolve(frameRef,window.Path+"/@FramePng");
        BaseImage=baseRef==null?null:Piece(OptionPieceKind.Image,window,Point.Empty,baseRef,baseSurface);
        FrameImage=frameRef==null?null:Piece(OptionPieceKind.Image,window,Point.Empty,frameRef,frameSurface);
        var size=baseSurface!=null?new Size(baseSurface.Width,baseSurface.Height)
            :window.Children.Where(c=>c.Name=="Control").Select(c=>new Size(c.TryInt("Width")??0,c.TryInt("Height")??0))
                .Where(s=>s.Width>0&&s.Height>0).OrderByDescending(s=>s.Width*s.Height).FirstOrDefault();
        LayoutFile=layout.FileName;Origin=new(window.Int("X"),window.Int("Y"));Size=size;MoveableHeight=window.TryInt("Moveable")??0;
        WindowPieces=windowPieces;Pages=pages;Missing=missing;Skipped=skipped;
        Unavailable=size.IsEmpty?$"{layout.FileName}: 底圖缺而且沒有宣告大小，無法定出視窗範圍":null;
    }

    static Point Rel(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));
    static Color ParseColor(UiNode n,string attr,Color fallback){
        var raw=n.Attr(attr);if(raw==null)return fallback;
        var parts=raw.Split(',');
        if(parts.Length!=3||!parts.All(p=>byte.TryParse(p.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _)))
            throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 r,g,b");
        return Color.FromArgb(byte.Parse(parts[0].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[1].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[2].Trim(),CultureInfo.InvariantCulture));
    }

    /// <summary>"10814;1" → (10814, 1); "10817" → (10817, 0). Attribute absent → null.</summary>
    public static OptionImageRef? ImageAttr(UiNode n,string attr){
        var raw=n.Attr(attr);if(raw==null)return null;
        var parts=raw.Split(';');
        static bool P(string s,out int v)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out v);
        if(parts.Length>2||!P(parts[0],out int id)||id<=0)throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是圖號");
        int flag=0;if(parts.Length==2&&!P(parts[1],out flag))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 的旗標不是整數");
        return new(attr,id,flag);
    }
    static OptionImageRef? FirstImage(UiNode n,params string[] attrs){foreach(var a in attrs)if(ImageAttr(n,a) is {} r)return r;return null;}

    static OptionPiece Piece(OptionPieceKind kind,UiNode n,Point at,OptionImageRef? image=null,UiSurface? surface=null,OptionImageRef? push=null,UiSurface? pushSurface=null)
        =>new(kind,n.Path,n.Name,n.Attr("Name"),at,new(n.TryInt("Width")??0,n.TryInt("Height")??0),image,surface,push,pushSurface,n.Attr("MouseEvent"),null,null,Color.Empty);

    static void Walk(UiNode parent,Point origin,List<OptionPiece> into,List<string> skipped,Func<OptionImageRef?,string,UiSurface?> resolve){
        foreach(var c in parent.Children)Element(c,origin,into,skipped,resolve);
    }

    static void Element(UiNode c,Point origin,List<OptionPiece> into,List<string> skipped,Func<OptionImageRef?,string,UiSurface?> resolve){
        var at=Rel(c,origin);
        switch(c.Name){
            case "Image":{
                var r=ImageAttr(c,"Png")??FirstImage(c,"Surf","BaseSurf");
                if(r==null){skipped.Add(c.Path+"（沒有圖號）");return;}
                into.Add(Piece(OptionPieceKind.Image,c,at,r,resolve(r,c.Path)));return;
            }
            case "BoardImage":{
                int w=c.Int("Width"),h=c.Int("Height");int alpha=c.TryInt("CustomAlpha")??255;
                if(w<=0||h<=0||alpha is <0 or >255)throw new UiLayoutException(c.Layout.FileName,c.Path,"BoardImage 大小或 CustomAlpha 不合理");
                into.Add(Piece(OptionPieceKind.Board,c,at) with{Color=ParseColor(c,"CustomColor",Color.Black),Alpha=alpha});return;
            }
            case "Label":{
                string? kind=c.Attr("Desc")!=null?"Desc":c.Attr("String")!=null?"String":null;
                // Caption="name0" / "value" are design-time placeholders the original overwrites at run time: not drawn.
                if(kind==null){skipped.Add(c.Path+"（Caption 執行期才填）");return;}
                into.Add(Piece(OptionPieceKind.Label,c,at) with{TextKind=kind,TextValue=c.Attr(kind)!.Trim(),Color=ParseColor(c,"FontColor",Color.White)});return;
            }
            case "Button" or "CheckButton" or "RadioButton":{
                var normal=FirstImage(c,"BasePng","BaseSurf");var push=FirstImage(c,"PushPng","PushSurf");
                into.Add(Piece(OptionPieceKind.Button,c,at,normal,resolve(normal,c.Path),push,push==null?null:resolve(push,c.Path+"/@"+push.Attr)));
                foreach(var k in c.Children){
                    if(k.Name=="Label")Element(k,at,into,skipped,resolve);
                    else skipped.Add(k.Path);
                }
                return;
            }
            // R10-I (#908): horizontal ScrollBar with a Move_Button = slider (volume bars, WindowAlpha). The background image is
            // drawn as before; the thumb becomes the Slider piece (drawn at the current value, after the background).
            case "ScrollBar" when c.Attr("Horizontal")=="1"&&c.Children.FirstOrDefault(k=>k.Name=="Button"&&k.Attr("Name")==SliderThumb) is {} thumb:{
                foreach(var k in c.Children)if(k.Path!=thumb.Path)Element(k,at,into,skipped,resolve);
                var normal=FirstImage(thumb,"BasePng","BaseSurf");var push=FirstImage(thumb,"PushPng","PushSurf");
                into.Add(Piece(OptionPieceKind.Slider,c,at,normal,resolve(normal,thumb.Path),push,push==null?null:resolve(push,thumb.Path+"/@"+push.Attr))
                    with{MouseEvent=c.Attr("Name")??c.Path,Thumb=new(thumb.TryInt("X")??0,thumb.TryInt("Y")??0),ScrollStart=c.TryInt("ScrollStart")??0,ScrollWidth=c.TryInt("ScrollWidth")??0});
                return;
            }
            case "Control" or "ScrollBar" or "ComboBox":Walk(c,at,into,skipped,resolve);return;
            // Collapsed combo list, runtime-filled shortcut rows, tooltips: not built (see the file header).
            default:skipped.Add(c.Path);return;
        }
    }

    public void Update(IUiGameState state){if(!state.InWorld&&open)Close();}

    /// <summary>Opens on the Home page. false when the layout is unavailable.</summary>
    public bool Open(){
        if(!Available)return false;
        open=true;pressed=null;pressedInside=false;sliding=false;
        ActivePage=Pages.FirstOrDefault(p=>p.Name==HomePage)??Pages[0];
        return true;
    }
    public void Close(){open=false;pressed=null;pressedInside=false;sliding=false;}
    /// <summary>Open again = close (same as UiRoot.Toggle for a window in the stack).</summary>
    public bool Toggle(){if(open){Close();return true;}return Open();}

    /// <summary>Page a tab button selects ("GameBtn" → "Game"), or null.</summary>
    public OptionPage? TabTarget(OptionPiece p){
        if(p.Kind!=OptionPieceKind.Button||p.Name is not {} n||!n.EndsWith(TabSuffix,StringComparison.Ordinal))return null;
        string stem=n[..^TabSuffix.Length];
        return Pages.FirstOrDefault(pg=>pg.Name==stem);
    }
    public bool ShowPage(string name){
        if(!open||Pages.FirstOrDefault(p=>p.Name==name) is not {} page)return false;
        ActivePage=page;return true;
    }

    IEnumerable<OptionPiece> Live=>ActivePage==null?WindowPieces:WindowPieces.Concat(ActivePage.Pieces);
    public Rectangle At(OptionPiece p)=>new(Origin.X+p.HitLocal.X,Origin.Y+p.HitLocal.Y,p.HitLocal.Width,p.HitLocal.Height);
    /// <summary>Topmost clickable piece at a logical point (later in XML order = drawn on top).</summary>
    public OptionPiece? ButtonAt(Point location)=>open?Live.Where(p=>p.Clickable).LastOrDefault(p=>At(p).Contains(location)):null;

    public void Paint(UiCanvas canvas){
        if(!open)return;
        if(BaseImage?.Surface is {} b)canvas.DrawSurface(b,Origin.X,Origin.Y);
        if(FrameImage?.Surface is {} f)canvas.DrawSurface(f,Origin.X,Origin.Y);
        foreach(var p in Live)PaintPiece(canvas,p);
    }

    void PaintPiece(UiCanvas canvas,OptionPiece p){
        int x=Origin.X+p.Offset.X,y=Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case OptionPieceKind.Image:canvas.DrawSurface(p.Surface,x,y);break;
            case OptionPieceKind.Board:{
                using var brush=new SolidBrush(Color.FromArgb(p.Alpha,p.Color));
                canvas.Graphics.FillRectangle(brush,x,y,p.DeclaredSize.Width,p.DeclaredSize.Height);break;
            }
            case OptionPieceKind.Label:
                if(TextResolver?.Invoke(p.TextKind!,p.TextValue!) is {Length:>0} text)canvas.DrawText(text,x,y,p.Color);
                break;
            case OptionPieceKind.Button:{
                bool tabOn=ActivePage!=null&&TabTarget(p)==ActivePage;
                bool held=p==pressed&&pressedInside;
                // R9-Q (#861): a wired check button whose option is on is drawn checked (PushPng, 推測).
                bool on=p.Element=="CheckButton"&&p.MouseEvent is {} ev&&Checked(ev)==true;
                // Push image while held or while its page is shown; a missing push image falls back to the normal one.
                canvas.DrawSurface((held||tabOn||on)&&p.PushSurface!=null?p.PushSurface:p.Surface,x,y);
                break;
            }
            case OptionPieceKind.Slider:{
                // R10-I (#908): thumb at the current value (unwired bar: at its XML position, as before).
                bool held=p==pressed&&pressedInside;
                var thumb=held&&p.PushSurface!=null?p.PushSurface:p.Surface;
                canvas.DrawSurface(thumb,Origin.X+ThumbX(p,p.MouseEvent is {} bar?Level(bar):null),y+p.Thumb.Y);
                break;
            }
        }
    }

    public bool IsDragHandle(Point location)=>open&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight&&ButtonAt(location)==null;
    public void MoveTo(Point location)=>Origin=location;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel||e.Button!=MouseButtons.Left)return inside||pressed!=null;
        switch(e.Kind){
            case UiMouseKind.Down:
                pressed=ButtonAt(e.Location);pressedInside=pressed!=null;
                // R10-I (#908): a press on a wired slider sets the value at once; dragging follows (see Move).
                if(pressed?.Kind==OptionPieceKind.Slider)sliding=Slide(pressed,e.Location.X);
                return inside;
            case UiMouseKind.Move:
                if(pressed!=null&&sliding){Slide(pressed,e.Location.X);return true;}
                if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                return inside||pressed!=null;
            case UiMouseKind.Up:{
                var was=pressed;bool slid=sliding;pressed=null;pressedInside=false;sliding=false;
                if(slid){Slide(was!,e.Location.X);return true;}
                if(was!=null&&At(was).Contains(e.Location))Fire(was);
                return inside||was!=null;
            }
            default:return inside;
        }
    }

    void Fire(OptionPiece p){
        if(p.MouseEvent==CloseEvent){Close();return;}
        if(TabTarget(p) is {} page){ActivePage=page;return;}
        // R9-Q (#861): wired check button → the option changes now (no OK step).
        if(p.Element=="CheckButton"&&p.MouseEvent is {} ev&&Checked(ev)!=null){
            if(!ToggleSetting(ev))UiResourceLog.Write($"option-ui {ev} 這次未套用（設定暫不可改）");
            return;
        }
        string key=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(key))UiResourceLog.Write($"option-ui {key} 尚未接上（設定值不改）");
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}

/// <summary>
/// Png images of OptionUI (see the file header): "&lt;id&gt;c.png" then "&lt;id&gt;.png", Sprite{h} then Sprite, local pinned
/// archives first, then the reference client (read-only; idx and payload SHA-256 logged). Decoded to 32bpp PArgb in memory.
/// </summary>
public sealed class OptionPngSource {
    public const string Extension=".png";
    const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;
    static readonly byte[] Signature=[0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A];
    readonly IReadOnlyList<ClientArchive> local;
    readonly string? refRoot;readonly string refSource;
    readonly object gate=new();
    readonly Dictionary<int,UiSurface?> cache=new();
    readonly Dictionary<int,(Dictionary<string,(long Offset,int Size)> Entries,HashSet<string> Duplicates,string Sha256)?> refIndexes=new();
    readonly List<string> served=[];
    public OptionPngSource(IReadOnlyList<ClientArchive> local,RefClientXmlSource? reference):this(local,reference?.Root,reference?.Source??"none"){}
    public OptionPngSource(IReadOnlyList<ClientArchive> local,string? referenceRoot,string source="explicit"){
        this.local=local;refRoot=referenceRoot==null?null:Path.GetFullPath(referenceRoot);refSource=source;
    }
    /// <summary>"local Sprite01/10814.png" or "ref D:\…\Sprite01.pak:10814.png sha256 …", one per image served.</summary>
    public IReadOnlyList<string> Served{get{lock(gate)return served.ToArray();}}

    public static IEnumerable<(int Archive,string Name)> Candidates(int id){
        foreach(var name in new[]{id.ToString(CultureInfo.InvariantCulture)+SurfCatalog.LocalizedImgSuffix+Extension,id.ToString(CultureInfo.InvariantCulture)+Extension}){
            yield return (1+SurfCatalog.NameHash(name),name);
            yield return (0,name);
        }
    }
    static string Hash(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public UiSurface? Get(int id){
        if(id<=0)return null;
        lock(gate){
            if(cache.TryGetValue(id,out var hit))return hit;
            var found=FromLocal(id)??FromReference(id);
            cache[id]=found;return found;
        }
    }

    UiSurface? FromLocal(int id){
        foreach(var (a,name) in Candidates(id)){
            if(a>=local.Count)continue;
            var archive=local[a];
            try{
                if(archive.Find(name) is not {} entry)continue;
                var s=new UiSurface(id,entry.Name,archive.Pin.Name,Decode(archive.Read(entry),$"{archive.Pin.Name}/{entry.Name}"));
                served.Add($"local {archive.Pin.Name}/{entry.Name}");return s;
            }catch(IOException ex){UiResourceLog.Write($"option-png {archive.Pin.Name}/{name} 無法使用：{ex.Message}");}
        }
        return null;
    }

    UiSurface? FromReference(int id){
        if(refRoot==null)return null;
        foreach(var (a,name) in Candidates(id)){
            if(RefIndex(a) is not {} index)continue;
            string archive=RefClientSurfFallback.ArchiveName(a);
            if(index.Duplicates.Contains(name)){UiResourceLog.Write($"option-png {archive}/{name} 在 idx 重名，不使用");continue;}
            if(!index.Entries.TryGetValue(name,out var e))continue;
            try{
                string pakPath=Path.Combine(refRoot,archive+".pak");
                using var pak=new FileStream(pakPath,FileMode.Open,FileAccess.Read,FileShare.Read);
                if(e.Size is <=0 or >ClientArchive.MaxEntrySize||e.Offset<0||e.Offset+e.Size>pak.Length){UiResourceLog.Write($"option-png {archive}/{name} 越界或大小不合理");continue;}
                var blob=new byte[e.Size];pak.Position=e.Offset;pak.ReadExactly(blob);
                var s=new UiSurface(id,name,"ref:"+archive,Decode(blob,$"{pakPath}:{name}"));
                string line=$"ref {pakPath}:{name} sha256 {Hash(blob)} (idx {index.Sha256}, {refSource})";
                served.Add(line);UiResourceLog.Write("option-png "+id+" -> "+line);
                return s;
            }catch(IOException ex){UiResourceLog.Write($"option-png {archive}/{name} 無法使用：{ex.Message}");}
        }
        return null;
    }

    (Dictionary<string,(long Offset,int Size)> Entries,HashSet<string> Duplicates,string Sha256)? RefIndex(int a){
        if(refIndexes.TryGetValue(a,out var done))return done;
        (Dictionary<string,(long,int)>,HashSet<string>,string)? result=null;
        string idxPath=Path.Combine(refRoot!,RefClientSurfFallback.ArchiveName(a)+".idx");
        try{
            if(File.Exists(idxPath)&&File.Exists(Path.ChangeExtension(idxPath,".pak"))){
                byte[] idx;
                using(var file=new FileStream(idxPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    if(file.Length<4||file.Length>4+(long)RecordSize*MaxRecords)throw new InvalidDataException("idx 大小不合理");
                    idx=new byte[file.Length];file.ReadExactly(idx);
                }
                uint count=BitConverter.ToUInt32(idx,0);
                if(count>MaxRecords||(long)count*RecordSize+4!=idx.Length)throw new InvalidDataException("idx 筆數×28+4 不等於檔案大小");
                var table=new Dictionary<string,(long,int)>(StringComparer.OrdinalIgnoreCase);var dup=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<count;i++){
                    int at=4+i*RecordSize;var raw=idx.AsSpan(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
                    if(end==0)continue;
                    bool printable=true;foreach(byte c in raw[..end])if(c is <32 or >=127){printable=false;break;}
                    if(!printable)continue;
                    string name=Encoding.ASCII.GetString(raw[..end]);
                    if(!table.TryAdd(name,(BitConverter.ToUInt32(idx,at),(int)Math.Min(BitConverter.ToUInt32(idx,at+4+NameSize),int.MaxValue))))dup.Add(name);
                }
                result=(table,dup,Hash(idx));
            }else UiResourceLog.Write($"option-png {refRoot} 沒有 {RefClientSurfFallback.ArchiveName(a)}.idx／.pak");
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){
            UiResourceLog.Write($"option-png {idxPath} 無法讀取：{ex.Message}");
        }
        refIndexes[a]=result;return result;
    }

    /// <summary>PNG bytes → premultiplied 32bpp pixels. Not a PNG / undecodable / larger than UiImage.MaxSide → UiResourceException.</summary>
    public static UiImage Decode(byte[] data,string name="png"){
        if(data.Length<Signature.Length||!data.AsSpan(0,Signature.Length).SequenceEqual(Signature))throw new UiResourceException($"{name}: 不是 PNG");
        try{
            using var ms=new MemoryStream(data,false);
            using var source=new Bitmap(ms);
            int w=source.Width,h=source.Height;
            if(w is <=0 or >UiImage.MaxSide||h is <=0 or >UiImage.MaxSide)throw new UiResourceException($"{name}: PNG 尺寸 {w}×{h} 不合理");
            using var bitmap=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(bitmap)){
                g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                g.DrawImage(source,new Rectangle(0,0,w,h),new Rectangle(0,0,w,h),GraphicsUnit.Pixel);
            }
            var bits=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            var argb=new uint[w*h];
            try{
                var row=new int[w];
                for(int y=0;y<h;y++){Marshal.Copy(bits.Scan0+y*bits.Stride,row,0,w);for(int x=0;x<w;x++)argb[y*w+x]=unchecked((uint)row[x]);}
            }finally{bitmap.UnlockBits(bits);}
            return new(w,h,0,0,argb);
        }catch(Exception ex) when(ex is ArgumentException or ExternalException or OutOfMemoryException){
            throw new UiResourceException($"{name}: PNG 無法解碼：{ex.Message}",ex);
        }
    }
}
