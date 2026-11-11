namespace P06.NativeUi;

/// <summary>
/// RIGHT_WINDOW macro box (Issue #466; docs/design/native-ui.md §5.3 ui-macro-bar; geometry docs/reference/ui-layout.md §2, V5 (a)(b)).
/// Frame 3176 at (643,476); 8 slots on F5–F12 in 4 columns × 2 rows, pitch 34 (TW16 sub_ED33F0 / TW13 sub_783A90);
/// 3 tabs (TAB_BUTTON actions 10–12, also F1–F3); SELECT_MARK 2331; MACRO_LOCK (ONOFF_BUTTON action 8).
/// The menu buttons of the same window (BUTTON_2STAGE_LIST, y=91) belong to ui-menu-buttons and are not drawn here.
/// <para>R7-O (T353/#816, gap-matrix 15.2 point 3): when WidgetSlot.xml is found (main Tile.pak, else the reference client
/// through UiXmlSource.Reference) the box also draws CornerFrame (surf 3292), replaces the MACRO_LOCK padlock by the XML
/// ButtonLock (BaseSurf 3293 / PushSurf 3294) and draws ButtonMode (BasePng 3350 / PushPng 3351 via OptionPngSource).
/// That TW16 reads WidgetSlot.xml is 推測 (the file name is not in the BIN XML table); the look is confirmed on the real
/// client (M30). Without the file (synthetic tests, no reference client) the uistatus86 look is kept unchanged.</para>
/// <para>T425 (#969, FU15§2.4 / FU17§2.2): ButtonMode (MouseEvent ModeChange, on release = 514) and Alt+R switch the WidgetSlot
/// mode 0 (MINI_MAP_WINDOW) ↔ 1 (EX_MACRO_WINDOW) in BAR_485 (<see cref="WidgetSlotMode"/>); the mode is saved and restored
/// when entering the world. Only with WidgetSlot.xml (3.81 has no WidgetSlot; without it Alt+R is left to the game).</para>
/// </summary>
public sealed class MacroPanel(UiResources? resources=null,UiSlotGroup? bar485=null):IUiPanel {
    [UiPanel(80)] internal static UiPanelRegistration Registration=>new(MacroPanel.WindowId,"uistatus86",root=>new MacroPanel(root.Resources,root.SlotGroup(UiSlotGroup.Bar485)));   // ui-macro-bar #466 (RIGHT_WINDOW last of the bar: MACRO_LOCK overlaps BAR_485)
    public const string WindowId="RIGHT_WINDOW";
    /// <summary>Slot pitch in both directions and columns per row (V5 (a), verified).</summary>
    public const int Pitch=34,Columns=4;
    /// <summary>Tab actions 10/11/12 → pages 0/1/2 (ui-layout.md §2 RIGHT_WINDOW).</summary>
    public const int FirstTabAction=10,LockAction=8;
    /// <summary>
    /// Image offsets inside a surf range. ONOFF_BUTTON is "2 images per 1 button" (XML comment); in the real client 3191 is a
    /// closed padlock and 3192 an open one, so locked = a, unlocked = a+1 (推測 from the image content — V4 traced only
    /// IMAGE_BUTTON/3Stage; the BIN state byte_C31EAF and its default are not traced). TAB_BUTTON is "3 images per 1 button"; its state order is unverified (V4 §(b)
    /// last line), so the selected tab uses a+1 like a pressed button (推測). Change only these two constants once verified.
    /// </summary>
    public const int LockOpenOffset=1,TabSelectedOffset=1;
    /// <summary>Item count text: palette[13] yellow on palette[0] black, capped at 99999 (V5 (a)).</summary>
    public static readonly Color CountColor=Color.FromArgb(255,255,255,0),CountShadow=Color.FromArgb(255,0,0,0);
    public const long CountCap=99999;

    sealed record Tab(int Page,Rectangle Rect,UiSurface? Normal,UiSurface? Selected);
    UiSurface? frame,selectMark,lockClosed,lockOpen;
    Point frameAt,selectAt,iconAt;
    Rectangle lockRect;
    Tab[] tabs=[];
    IUiMacroState? macro;bool inWorld;
    WidgetSlotWindow? widget;bool modePressed;

    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>inWorld&&macro!=null&&frame!=null;
    /// <summary>Logical rectangle of the frame image (643,476,157,124 with the real 3176).</summary>
    public Rectangle FrameRect=>frame==null?Rectangle.Empty:new(frameAt,new(frame.Width,frame.Height));
    public Rectangle LockRect=>lockRect;
    public Point IconOrigin=>iconAt;
    public Point SelectOrigin=>selectAt;
    public IReadOnlyList<Rectangle> TabRects=>tabs.Select(t=>t.Rect).ToArray();
    /// <summary>WidgetSlot.xml look in use (R7-O); null = uistatus86 look (file absent or refused, see WidgetStatus).</summary>
    public WidgetSlotWindow? Widget=>widget;
    /// <summary>"absent", "refused: …" or the XML file name actually used.</summary>
    public string WidgetStatus{get;private set;}="absent";
    /// <summary>Surf / png ids of the current lock button: (3191,3192) uistatus86, (3293,3294) WidgetSlot.</summary>
    public (int Locked,int Unlocked) LockImages{get;private set;}
    public bool ModePressed=>modePressed;
    /// <summary>Where the WidgetSlot mode is saved (default: option-ui.txt); offline checks put an in-memory store here.</summary>
    internal IWidgetSlotModeStore ModeStore{get;set;}=WidgetSlotMode.SettingsFile;

    public void Load(UiLayout layout,SurfCatalog surfs){
        var (xml,png,error)=WidgetSlotLayout.Open(resources,WidgetSlotLayout.MainFile);
        Load(layout,surfs.Get,xml,png,error);
    }

    /// <summary>Offline checks: images from the caller; widgetXml null = uistatus86 look only.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,UiLayout? widgetXml,Func<int,UiSurface?> png,string? readError=null){
        widget=null;modePressed=false;WidgetStatus=readError==null?"absent":"refused: "+readError;
        var w=layout.Root.Child(WindowId);int x=w.Int("x"),y=w.Int("y");
        var frameNode=w.Child("MACRO_MAIN_WINDOW/MACRO_FRAME");
        int frameSurf=MacroLayout.FirstSurf(frameNode);
        frame=surf(frameSurf);frameAt=MacroLayout.At(frameNode,x,y);
        var mark=w.Child("ETC/SELECT_MARK");selectMark=surf(MacroLayout.FirstSurf(mark));selectAt=MacroLayout.At(mark,x,y);
        iconAt=MacroLayout.At(w.Child("ETC/MACRO_ICON"),x,y);   // surfNumber="" here: the icon is chosen at runtime
        var tabNodes=w.Child("BUTTON_TAB_LIST").ChildrenNamed("TAB_BUTTON").Where(n=>n.Attr("title")=="MACRO_SELECT").ToArray();
        var list=new List<Tab>();
        foreach(var n in tabNodes){
            int page=n.Int("action")-FirstTabAction;
            if(page is <0 or >=MacroBook.PageCount||list.Any(t=>t.Page==page))throw new UiLayoutException(layout.FileName,n.Path,$"MACRO_SELECT action {page+FirstTabAction} 不在 10–12 或重複");
            int a=MacroLayout.FirstSurf(n);var normal=surf(a);var at=MacroLayout.At(n,x,y);
            list.Add(new(page,MacroLayout.Rect(at,normal),normal,surf(a+TabSelectedOffset)));
        }
        if(list.Count!=MacroBook.PageCount)throw new UiLayoutException(layout.FileName,w.Path+"/BUTTON_TAB_LIST",$"MACRO_SELECT 分頁鈕應有 {MacroBook.PageCount} 個，實際 {list.Count}");
        tabs=list.OrderBy(t=>t.Page).ToArray();
        var locks=w.Child("BUTTON_ONOFF_LIST").ChildrenNamed("ONOFF_BUTTON").Where(n=>n.Attr("title")=="MACRO_LOCK").ToArray();
        if(locks.Length!=1)throw new UiLayoutException(layout.FileName,w.Path+"/BUTTON_ONOFF_LIST","MACRO_LOCK 應恰有 1 個");
        int lockSurf=MacroLayout.FirstSurf(locks[0]);lockClosed=surf(lockSurf);lockOpen=surf(lockSurf+LockOpenOffset);
        lockRect=MacroLayout.Rect(MacroLayout.At(locks[0],x,y),lockClosed??lockOpen);
        LockImages=(lockSurf,lockSurf+LockOpenOffset);
        // MACRO_LOCK sits at xRPos=-9, i.e. left of the frame (over the BAR_485 slot's last 9 columns), so Bounds is the union.
        Bounds=MacroLayout.Union(FrameRect,lockRect);
        if(widgetXml==null)return;
        try{widget=WidgetSlotWindow.Parse(widgetXml,WidgetSlotLayout.MainWindow,frameAt,frameSurf,surf,png);}
        catch(UiLayoutException ex){WidgetStatus="refused: "+ex.Message;UiResourceLog.Write($"widget-slot {widgetXml.FileName} 不符，維持 uistatus86 外觀：{ex.Message}");return;}
        WidgetStatus=widgetXml.FileName;
        if(widget.Lock is {} l){
            // ButtonLock replaces MACRO_LOCK: Base = locked, Push = unlocked (3293 shows a closed shackle, 3294 an open one;
            // same order as 3191/3192 — 推測 from the image content, the BIN lock state is not traced).
            lockClosed=l.Base;lockOpen=l.Push;lockRect=l.Rect;LockImages=(l.BaseRef.Id,l.PushRef?.Id??0);
        }
        Bounds=new[]{lockRect,widget.CornerRect,widget.Mode?.Rect??Rectangle.Empty}.Aggregate(FrameRect,MacroLayout.Union);
        if(widget.Missing.Count>0)UiResourceLog.Write($"widget-slot {widgetXml.FileName} 缺圖不畫：{string.Join("、",widget.Missing)}");
    }

    /// <summary>
    /// Entering the world restores the saved WidgetSlot mode (sub_59DD30 → sub_705770(mode)); only with WidgetSlot.xml, so the
    /// uistatus86 / 3.81 BAR_485 start (minimap) is unchanged.
    /// </summary>
    public void Update(IUiGameState state){
        bool was=inWorld;inWorld=state.InWorld;macro=MacroBook.StateOf(state);
        if(!was&&inWorld&&widget!=null)WidgetSlotMode.Restore(bar485,ModeStore);
    }

    /// <summary>Top-left of slot k's icon: (iconX + 34·(k%4), iconY + 34·(k/4)).</summary>
    public Point SlotIcon(int k)=>new(iconAt.X+Pitch*(k%Columns),iconAt.Y+Pitch*(k/Columns));
    public Point SlotMark(int k)=>new(selectAt.X+Pitch*(k%Columns),selectAt.Y+Pitch*(k/Columns));
    /// <summary>Click → slot (TW13 sub_782740): (mx−markX)/34 + 4·((my−markY)/34), only inside the 4×2 grid.</summary>
    public int SlotAt(Point p){
        int dx=p.X-selectAt.X,dy=p.Y-selectAt.Y;
        if(dx<0||dy<0||dx>=Pitch*Columns||dy>=Pitch*(MacroBook.SlotsPerPage/Columns))return -1;
        return dx/Pitch+Columns*(dy/Pitch);
    }

    public void Paint(UiCanvas canvas){
        if(macro==null)return;var book=macro.Macros;
        canvas.DrawSurface(frame,frameAt.X,frameAt.Y);
        foreach(var t in tabs)canvas.DrawSurface(t.Page==book.Page?t.Selected:t.Normal,t.Rect.X,t.Rect.Y);
        for(int k=0;k<MacroBook.SlotsPerPage;k++)MacroLayout.PaintSlot(canvas,macro,book.Binding(book.Page,k),SlotIcon(k),SlotMark(k));
        if(!book.SelectionInExtended){var m=SlotMark(book.Selected);canvas.DrawSurface(selectMark,m.X,m.Y);}
        canvas.DrawSurface(book.Locked?lockClosed:lockOpen,lockRect.X,lockRect.Y);
        // WidgetSlot.xml order after the lock: CornerFrame, then ButtonMode (child order = draw order, 推測).
        if(widget!=null){canvas.DrawSurface(widget.Corner,widget.CornerRect.X,widget.CornerRect.Y);widget.Mode?.Paint(canvas,modePressed);}
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        // ModeChange fires on release over the button (514 = WM_LBUTTONUP, FU15§2.4); a release elsewhere only ends the press.
        if(e.Kind==UiMouseKind.Up&&modePressed){
            modePressed=false;
            if(widget?.Mode is {} m&&m.Hit(e.Location))WidgetSlotMode.Toggle(bar485,ModeStore,WidgetSlotLayout.MainFile);
            return true;
        }
        if(macro==null||e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return false;
        var book=macro.Macros;
        if(widget?.Mode is {} mode&&mode.Hit(e.Location)){modePressed=true;WidgetSlotMode.Pressed(bar485,WidgetSlotLayout.MainFile);return true;}
        if(!lockRect.IsEmpty&&lockRect.Contains(e.Location)){book.ToggleLock();return true;}
        foreach(var t in tabs)if(!t.Rect.IsEmpty&&t.Rect.Contains(e.Location)){book.SetPage(t.Page);return true;}
        int k=SlotAt(e.Location);if(k<0)return false;
        book.Select(k);
        // Double click uses the slot like its F-key (TW13 sub_782740 case 515 → sub_782480).
        if(e.Clicks>=2)MacroBook.TryActivate(macro,book.Binding(book.Page,k),commands);
        return true;
    }

    /// <summary>
    /// F1–F3 = tab, F5–F12 = slot of the current tab (V5 (b)). Only without modifiers (Ctrl+F is the dev teleport list, Alt+F2/F3
    /// are window/slot modes) and only with game keyboard focus. Auto-repeat and key-up are swallowed so nothing else sees them.
    /// </summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(macro==null||!inWorld)return false;
        // Alt+R = the same switch as ModeChange (WM_CHAR 0x8072, sub_8C14F0 case at 0x8C19A3; the edit-box paths sub_8C2290 /
        // sub_8C5380 have it too, so InputAllowed is not required). Only with WidgetSlot.xml; key-up / repeat are swallowed.
        if(e.KeyData==(Keys.Alt|Keys.R)&&widget!=null&&bar485!=null){
            if(e.Down&&!e.Repeat)WidgetSlotMode.Toggle(bar485,ModeStore,"Alt+R");
            return true;
        }
        if(e.Modifiers!=Keys.None)return false;
        int page=MacroBook.KeyPage(e.KeyCode),slot=MacroBook.KeySlot(e.KeyCode);
        if(page<0&&slot<0)return false;
        if(!e.Down)return true;
        if(!macro.InputAllowed)return false;
        if(e.Repeat)return true;
        var book=macro.Macros;
        if(page>=0){book.SetPage(page);return true;}
        book.Select(slot);   // 推測: the key also moves the select mark (sub_782480 argument 0 not traced)
        MacroBook.TryActivate(macro,book.Binding(book.Page,slot),commands);
        return true;
    }
}

/// <summary>Shared layout/paint helpers of the macro panels.</summary>
static class MacroLayout {
    public static int FirstSurf(UiNode n){var s=n.Surfs();return s.Length>0?s[0]:throw new UiLayoutException(n.Layout.FileName,n.Path,"surfNumber 是空的");}
    /// <summary>xRPos/yRPos are relative to the window; the parser adds the window x/y (V5 (a)).</summary>
    public static Point At(UiNode n,int x,int y)=>new(x+n.Int("xRPos"),y+n.Int("yRPos"));
    public static Rectangle Rect(Point at,UiSurface? s)=>s==null?Rectangle.Empty:new(at,new(s.Width,s.Height));
    public static Rectangle Union(Rectangle a,Rectangle b)=>a.IsEmpty?b:b.IsEmpty?a:Rectangle.Union(a,b);

    /// <summary>
    /// One slot: item icon (existing engine icon) and, for items with count &gt; 1, the count right-aligned at
    /// x = markX + 27 − 5·digits, y = markY + 22 (V5 (a)). Magic has no verified icon source yet → nothing drawn.
    /// Unresolved bindings (item gone, spell not learned) draw nothing.
    /// </summary>
    public static void PaintSlot(UiCanvas canvas,IUiMacroState state,PlayerHudBinding? binding,Point icon,Point mark){
        if(state.Inventory?.Resolve(binding) is not {} entry)return;
        if(entry.IconId is ushort id&&state.ItemIcon(id) is {} bitmap)canvas.DrawBitmap(bitmap,icon.X,icon.Y);
        if(entry.Binding.Kind==PlayerHudKind.Item&&entry.Count is long count&&count>1){
            string text=Math.Min(count,MacroPanel.CountCap).ToString(System.Globalization.CultureInfo.InvariantCulture);
            canvas.DrawText(text,mark.X+27-5*text.Length,mark.Y+22,MacroPanel.CountColor,MacroPanel.CountShadow);
        }
    }
}

/// <summary>A Button of WidgetSlot.xml / WidgetSubSlot.xml: Base / Push image (surf or png), hit rect = image size.</summary>
public sealed record WidgetSlotButton(string Name,Rectangle Rect,OptionImageRef BaseRef,UiSurface? Base,OptionImageRef? PushRef,UiSurface? Push,string? MouseEvent) {
    public bool Hit(Point p)=>!Rect.IsEmpty&&Rect.Contains(p);
    /// <summary>Pressed draws Push when it exists, otherwise Base (the same button, not a substitute image).</summary>
    public void Paint(UiCanvas canvas,bool pressed){var s=pressed&&Push!=null?Push:Base;canvas.DrawSurface(s,Rect.X,Rect.Y);}
}

/// <summary>
/// R7-O (T353/#816): the parts of WidgetSlot.xml ("Slotwindow") / WidgetSubSlot.xml ("SubSlotWindow") the engine draws on top
/// of the uistatus86 boxes. Coordinates are relative to the Window (X/Y of the children are added to the Window X/Y). The
/// Window must sit where the uistatus86 frame sits and use the same BaseSurf, otherwise the file is refused (no mixing of two
/// layouts). Slots, tabs and the select mark stay on uistatus86 (same surfs, see gap-matrix 15.2 point 3).
/// </summary>
public sealed class WidgetSlotWindow {
    public string File{get;}
    public Point Origin{get;}
    /// <summary>Image "CornerFrame" (Surf 3292 in kor25); null when the XML has none.</summary>
    public OptionImageRef? CornerRef{get;}
    public UiSurface? Corner{get;}
    public Rectangle CornerRect{get;}
    /// <summary>Button "ButtonLock" (BaseSurf / PushSurf).</summary>
    public WidgetSlotButton? Lock{get;}
    /// <summary>Button "ButtonMode" (BasePng / PushPng, MouseEvent ModeChange → <see cref="WidgetSlotMode.Toggle"/>).</summary>
    public WidgetSlotButton? Mode{get;}
    /// <summary>"surf 3292 (…/Image[0])" for images named in the XML but not found (not drawn).</summary>
    public IReadOnlyList<string> Missing{get;}

    WidgetSlotWindow(string file,Point origin,OptionImageRef? cornerRef,UiSurface? corner,Rectangle cornerRect,WidgetSlotButton? lockButton,WidgetSlotButton? mode,IReadOnlyList<string> missing){
        File=file;Origin=origin;CornerRef=cornerRef;Corner=corner;CornerRect=cornerRect;Lock=lockButton;Mode=mode;Missing=missing;
    }

    public static WidgetSlotWindow Parse(UiLayout layout,string windowName,Point expectedOrigin,int expectedBaseSurf,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
        var w=layout.Root.Child("Window");
        if(w.Attr("Name")!=windowName)throw new UiLayoutException(layout.FileName,w.Path,$"Window Name 不是 {windowName}");
        var origin=new Point(w.Int("X"),w.Int("Y"));
        if(origin!=expectedOrigin)throw new UiLayoutException(layout.FileName,w.Path,$"Window 位置 {origin} 與 uistatus86 框 {expectedOrigin} 不同");
        var baseRef=OptionPanel.ImageAttr(w,"BaseSurf");
        if(baseRef?.Id!=expectedBaseSurf)throw new UiLayoutException(layout.FileName,w.Path,$"BaseSurf {baseRef?.Id.ToString()??"（無）"} 與 uistatus86 框 {expectedBaseSurf} 不同");
        var missing=new List<string>();
        UiSurface? Resolve(OptionImageRef? r,string path){
            if(r==null)return null;
            var s=r.IsSurf?surf(r.Id):png(r.Id);
            if(s==null)missing.Add($"{r} ({path})");
            return s;
        }
        Point At(UiNode n)=>new(origin.X+(n.TryInt("X")??0),origin.Y+(n.TryInt("Y")??0));
        OptionImageRef? cornerRef=null;UiSurface? corner=null;Rectangle cornerRect=Rectangle.Empty;
        if(Named(w,"Image","CornerFrame") is {} c){
            cornerRef=OptionPanel.ImageAttr(c,"Surf")??throw new UiLayoutException(layout.FileName,c.Path,"CornerFrame 沒有 Surf");
            corner=Resolve(cornerRef,c.Path);cornerRect=MacroLayout.Rect(At(c),corner);
        }
        WidgetSlotButton? Button(string name,string baseAttr,string pushAttr){
            if(Named(w,"Button",name) is not {} b)return null;
            var br=OptionPanel.ImageAttr(b,baseAttr)??throw new UiLayoutException(layout.FileName,b.Path,$"{name} 沒有 {baseAttr}");
            var pr=OptionPanel.ImageAttr(b,pushAttr);
            var bs=Resolve(br,b.Path+"/@"+baseAttr);var ps=Resolve(pr,b.Path+"/@"+pushAttr);
            return new(name,MacroLayout.Rect(At(b),bs??ps),br,bs,pr,ps,b.Attr("MouseEvent"));
        }
        var lockButton=Button("ButtonLock","BaseSurf","PushSurf");
        var mode=Button("ButtonMode","BasePng","PushPng");
        return new(layout.FileName,origin,cornerRef,corner,cornerRect,lockButton,mode,missing);
    }

    /// <summary>Direct child &lt;element Name="name"&gt;; absent → null, more than one → UiLayoutException.</summary>
    static UiNode? Named(UiNode w,string element,string name){
        var found=w.ChildrenNamed(element).Where(n=>n.Attr("Name")==name).Take(2).ToArray();
        if(found.Length>1)throw new UiLayoutException(w.Layout.FileName,found[1].Path,$"{element} {name} 重複");
        return found.Length==1?found[0]:null;
    }
}

/// <summary>Reading WidgetSlot.xml / WidgetSubSlot.xml (R7-O) for the macro panels.</summary>
public static class WidgetSlotLayout {
    public const string MainFile="WidgetSlot",SubFile="WidgetSubSlot",MainWindow="Slotwindow",SubWindow="SubSlotWindow";
    /// <summary>ButtonMode's MouseEvent: the WidgetSlot mode switch (<see cref="WidgetSlotMode"/>, T425 #969).</summary>
    public const string ModeChangeEvent="ModeChange";

    /// <summary>
    /// Main Tile.pak ("-c" then plain), then the reference client (UiXmlSource order). Absent → (null, no png, null);
    /// unreadable / malformed → (null, no png, reason) and one log line. Pngs go through OptionPngSource (local Sprite, then reference).
    /// </summary>
    public static (UiLayout? Layout,Func<int,UiSurface?> Png,string? Error) Open(UiResources? resources,string baseName){
        Func<int,UiSurface?> none=_=>null;
        if(resources==null)return (null,none,null);
        try{
            if(resources.Xml.TryReadPlain(baseName) is not {} found)return (null,none,null);
            var layout=UiLayout.Parse(UiXmlSource.DecodeText(found.Plain,found.ResolvedName),found.ResolvedName);
            var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
            UiResourceLog.Write($"widget-slot {baseName} -> {found.ResolvedName}");
            return (layout,png.Get,null);
        }catch(IOException ex){
            UiResourceLog.Write($"widget-slot {baseName} 無法讀取，維持 uistatus86 外觀：{ex.Message}");
            return (null,none,ex.Message);
        }
    }

}

/// <summary>Where the WidgetSlot mode survives a restart. Load null = nothing saved (the original default is 0, sub_705910).</summary>
public interface IWidgetSlotModeStore {
    int? Load();
    /// <summary>false = not written (no settings file configured or the write failed; the switch still happens).</summary>
    bool Save(int mode);
}

/// <summary>
/// T425 (#969): the TW16 WidgetSlot mode (FU15§2.4, FU17§2.1–2.2, 已驗證). sub_705770 always shows widgets 0/1/3/5
/// (MainCharInfo, MainChat, WidgetSlot, MainButton) and shows exactly one of widget 2 = MiniMapUI (mode 0) or widget 4 =
/// WidgetSubSlot (mode 1); sub_7059E0 (ModeChange 514 of both slot widgets, Alt+R) flips the mode. In the engine the two are
/// the BAR_485 members <see cref="MiniMapPanel"/> and <see cref="ExMacroPanel"/>, so a mode is "which of the two is current".
/// <list type="bullet">
/// <item>Any other current member (party RT, which TW16 does not have in this group) counts as mode 0 (推測, FU17§2.3), so the
/// next switch goes to mode 1. Alt+F3 cycling (V5 (e), PartyPanel) is unchanged.</item>
/// <item>A missing member (not registered) → no switch, one log line.</item>
/// <item>Saved like the original's option bit 0 (sub_997660 / sub_59E920), but in the engine's own option-ui.txt as
/// "widget-slot-mode=0|1" (the original settings file format is not reproduced, same as R10-I).</item>
/// </list>
/// </summary>
public static class WidgetSlotMode {
    public const string SettingKey="widget-slot-mode";
    public const int MiniMap=0,SubSlot=1;
    sealed class FileStore:IWidgetSlotModeStore {
        public int? Load()=>OptionSettingsFile.Read(SettingKey) is bool b?(b?SubSlot:MiniMap):null;
        public bool Save(int mode)=>OptionSettingsFile.Write(SettingKey,mode==SubSlot);
    }
    /// <summary>The product store: <see cref="OptionSettingsFile"/> key <see cref="SettingKey"/> (true = mode 1).</summary>
    public static readonly IWidgetSlotModeStore SettingsFile=new FileStore();

    /// <summary>BAR_485 member shown in a mode: 0 → MINI_MAP_WINDOW, 1 → EX_MACRO_WINDOW.</summary>
    public static string MemberOf(int mode)=>mode==SubSlot?ExMacroPanel.WindowId:MiniMapPanel.PanelId;
    /// <summary>1 when EX_MACRO_WINDOW is current, otherwise 0 (minimap, party RT, nothing).</summary>
    public static int Of(UiSlotGroup group)=>group.Current==ExMacroPanel.WindowId?SubSlot:MiniMap;
    /// <summary>Shows the member of the mode (sub_705770). false = mode not 0/1 or its member is not in the group.</summary>
    public static bool Apply(UiSlotGroup group,int mode){
        if(mode is not (MiniMap or SubSlot))return false;
        string id=MemberOf(mode);
        if(!group.Members.Contains(id))return false;
        group.Show(id);return true;
    }
    /// <summary>
    /// sub_7059E0: mode → 1 − mode, shown at once and saved. Returns the new mode, or null when nothing switched (no group, the
    /// other member missing). source = "WidgetSlot" / "WidgetSubSlot" / "Alt+R" for the log line.
    /// </summary>
    public static int? Toggle(UiSlotGroup? group,IWidgetSlotModeStore store,string source){
        if(group==null){NoGroup(source);return null;}
        int next=1-Of(group);
        if(!Apply(group,next)){
            UiResourceLog.Write($"widget-slot {source} {WidgetSlotLayout.ModeChangeEvent}：{group.Name} 沒有 {MemberOf(next)}，不切換");
            return null;
        }
        bool saved=store.Save(next);
        UiResourceLog.Write($"widget-slot {source} {WidgetSlotLayout.ModeChangeEvent} → 模式 {next}（{MemberOf(next)}；{(saved?"已存設定":"設定未寫入")}）");
        return next;
    }
    /// <summary>Press on ButtonMode: the switch itself happens on release; without a group (offline checks) say so at once.</summary>
    internal static void Pressed(UiSlotGroup? group,string source){if(group==null)NoGroup(source);}
    static void NoGroup(string source)=>
        UiResourceLog.Write($"widget-slot {source} {WidgetSlotLayout.ModeChangeEvent}：面板沒有 {UiSlotGroup.Bar485} 群組，不切換（離線測試；真機外觀待確認 M30）");
    /// <summary>Entering the world: the saved mode, if any, is shown (sub_59DD30). false = nothing saved / not applicable.</summary>
    public static bool Restore(UiSlotGroup? group,IWidgetSlotModeStore store){
        if(group==null||store.Load() is not int mode)return false;
        return Apply(group,mode);
    }
}
