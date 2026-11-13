using System.Text.Json;
namespace P06.NativeUi;

/// <summary>
/// The native UI tree on the 800×600 logical canvas (docs/design/native-ui.md §1, §3.1): world layer, bar, windows, float.
/// Built only from verified client resources (UiResources pins); any UiResourceException disables the whole native UI.
/// </summary>
public sealed class UiRoot {
    public const int LogicalWidth=800,LogicalHeight=600;
    public static readonly Rectangle Canvas=new(0,0,LogicalWidth,LogicalHeight);
    /// <summary>
    /// Where the world is drawn. V5 (c) is unverified (world behind the bar vs. only 800×470); evidence leans to "full 800×600,
    /// covered by the bar", so that is the value. Change only this line once verified.
    /// </summary>
    public static readonly Rectangle WorldRect=new(0,0,LogicalWidth,LogicalHeight);
    /// <summary>One-line error shown when the switch is on but the client resources do not match the pins (§2.1).</summary>
    public const string PinMismatchMessage="原客戶端 UI 資源與鎖定版本不符";

    readonly List<IUiPanel> panels=[];
    readonly List<IUiPanel> windowOrder=[];
    readonly HashSet<IUiPanel> faulted=[];
    readonly Dictionary<string,UiSlotGroup> groups=new(StringComparer.Ordinal);
    public IReadOnlyList<IUiPanel> Panels=>panels;
    public IReadOnlyCollection<IUiPanel> Faulted=>faulted;
    public UiResources? Resources{get;}
    public UiRoot(UiResources? resources=null){Resources=resources;}

    public UiSlotGroup SlotGroup(string name){
        if(!groups.TryGetValue(name,out var g))groups[name]=g=new UiSlotGroup(name);
        return g;
    }
    public IReadOnlyDictionary<string,UiSlotGroup> SlotGroups=>groups;

    public void Add(IUiPanel panel){
        if(panels.Any(p=>p.Id==panel.Id))throw new InvalidOperationException($"原生 UI 面板 {panel.Id} 重複註冊");
        if(!Enum.IsDefined(panel.Layer))throw new InvalidOperationException($"{panel.Id}: 不明圖層 {panel.Layer}");
        panels.Add(panel);if(panel.Layer==UiLayer.Window)windowOrder.Add(panel);
    }
    public IUiPanel? Find(string id)=>panels.FirstOrDefault(p=>p.Id==id);

    /// <summary>Paint order, bottom to top: layer, then (windows) z-order, then registration order.</summary>
    public IEnumerable<IUiPanel> PaintOrder(){
        SyncWindows();
        return panels.Select((p,i)=>(p,i)).OrderBy(t=>(int)t.p.Layer)
            .ThenBy(t=>t.p.Layer==UiLayer.Window?windowOrder.IndexOf(t.p):t.i).Select(t=>t.p);
    }
    /// <summary>Hit-test order, top to bottom (§3.3: from the float layer down).</summary>
    public IEnumerable<IUiPanel> HitOrder()=>PaintOrder().Reverse();
    /// <summary>Painted, hit and given input: visible, not faulted, and not pushed out of the window stack.</summary>
    public bool Usable(IUiPanel p)=>p.Visible&&!faulted.Contains(p)&&!evicted.Contains(p)&&!refused.Contains(p);
    public IUiPanel? HitTest(Point logical)=>HitOrder().FirstOrDefault(p=>Usable(p)&&p.Layer!=UiLayer.World&&p.Bounds.Contains(logical));

    /// <summary>
    /// Raises a window to the top of the z-order (sub_A596B0, VA 0xA596B0: moves an entry of dword_153D220 to slot 0, the
    /// others keep their relative order). Closed windows may be reordered too; they go to the top again when they open.
    /// </summary>
    public void BringToFront(IUiPanel panel){if(panel.Layer==UiLayer.Window&&windowOrder.Remove(panel))windowOrder.Add(panel);}

    // ---- Window stack (R3-I, #677): sub_A57C60 (VA 0xA57C60 / FO 0x657060), dword_153D220[4].
    // Original: slot 0 = newest/top; opening a window that is not in the array closes slot 3 (vtable+36), shifts 0..2 down
    // and puts the new one in slot 0; opening one that is already there closes it and compacts the array (a toggle);
    // a1 == 0 closes all four. Input goes to slots 0..3 in order (sub_8CA740 / sub_8CAB40).
    // Engine: panels own their open state (Visible + non-empty Bounds), so the root observes it. A window that newly opens
    // goes to the top; when more than MaxWindows are open, the bottom-most (= oldest, slot 3) is pushed out: closed through
    // IUiClosablePanel when the panel offers it, otherwise kept hidden (not painted, not hit) until it closes by itself.
    // R5-A (#733): Toggle (open again = close) and the 509 rule (RefusesPush) are below Evict.
    public const int MaxWindows=4;
    readonly List<IUiPanel> stack=[];
    readonly HashSet<IUiPanel> evicted=[];
    /// <summary>Open windows in the original array order: [0] = top (newest or last raised), at most MaxWindows.</summary>
    public IReadOnlyList<IUiPanel> WindowStack{get{SyncWindows();return windowOrder.Where(stack.Contains).Reverse().ToArray();}}
    /// <summary>true while a window is pushed out of the stack and has not closed itself yet.</summary>
    public bool IsEvicted(IUiPanel p)=>evicted.Contains(p);
    /// <summary>Fired once per window pushed out by a newer one (for logs and checks).</summary>
    public event Action<IUiPanel>? WindowEvicted;

    static bool IsOpen(IUiPanel p)=>p.Visible&&!p.Bounds.IsEmpty;

    /// <summary>
    /// Brings the stack in line with the panels' open state. Called from Update, PaintOrder (so Compose and hit testing) and
    /// UiInput.Key; cheap (a handful of windows) and idempotent.
    /// </summary>
    public void SyncWindows(){
        if(windowOrder.Count==0)return;
        // Closed (or faulted) windows leave the stack; an evicted or refused window that has closed may open again later.
        foreach(var w in windowOrder)if(faulted.Contains(w)||!IsOpen(w)){stack.Remove(w);evicted.Remove(w);refused.Remove(w);}
        // Newly opened windows go to the top, in z-order (registration order on the first pass).
        foreach(var w in windowOrder.ToArray()){
            if(faulted.Contains(w)||evicted.Contains(w)||refused.Contains(w)||stack.Contains(w)||!IsOpen(w))continue;
            if(RefusesPush(w,CurrentMap)){Refuse(w);continue;}
            Push(w);
        }
    }

    void Push(IUiPanel w){
        windowOrder.Remove(w);windowOrder.Add(w);stack.Add(w);
        while(stack.Count>MaxWindows)Evict(windowOrder.First(stack.Contains));
    }

    void Evict(IUiPanel oldest){
        stack.Remove(oldest);evicted.Add(oldest);
        UiResourceLog.Write($"視窗堆疊已滿（{MaxWindows} 層），關閉最舊的 {oldest.Id}");
        if(oldest is IUiClosablePanel closable){
            try{closable.Close();}catch(Exception ex){Fault(oldest,"Close",ex);}
            if(!IsOpen(oldest))evicted.Remove(oldest);
        }
        WindowEvicted?.Invoke(oldest);
    }

    // ---- R5-A (T306, #733): the rest of sub_A57C60 — the 509 rule and "open again = close" at the root.
    /// <summary>
    /// sub_A57C60 refuses to push dword_1541DF8 (SpellWindow, windows-2.md §2.1 item 4) while *dword_138BE60 == 509.
    /// dword_138BE60 is the current map (followups-3.md §3.1: the teleport map checks read it); that the dereferenced value is
    /// the map id is 推測 (decompiler typing). The engine's SPELL_WINDOW (uimagicitem legacy layout) stands for SpellWindow.
    /// </summary>
    public const int SpellWindowRefusedMap=509;
    /// <summary>Current map id (WorldSession.MapId), set by the host before Update; null outside the world.</summary>
    public int? CurrentMap{get;set;}
    readonly HashSet<IUiPanel> refused=[];
    /// <summary>true while a window that opened itself was refused by the stack and has not closed yet (kept hidden).</summary>
    public bool IsRefused(IUiPanel p)=>refused.Contains(p);
    /// <summary>Fired once per window the stack refused to push (509 rule).</summary>
    public event Action<IUiPanel>? WindowRefused;

    /// <summary>The original push rule: only SpellWindow on map 509 is refused (sub_A57C60).</summary>
    public static bool RefusesPush(IUiPanel panel,int? map)=>map==SpellWindowRefusedMap&&panel.Id==SpellWindowPanel.WindowId;

    // The panel opened itself (menu, hotkey); the original never opens it, so close it again right away (closable) or keep it
    // hidden like an evicted window until it closes by itself. Done inside SyncWindows, so it is never painted or hit.
    void Refuse(IUiPanel w){
        refused.Add(w);
        UiResourceLog.Write($"視窗堆疊拒絕開啟 {w.Id}（地圖 {CurrentMap}）");
        if(w is IUiClosablePanel closable){
            try{closable.Close();}catch(Exception ex){Fault(w,"Close",ex);}
            if(!IsOpen(w))refused.Remove(w);
        }
        WindowRefused?.Invoke(w);
    }

    /// <summary>
    /// sub_A57C60 for one window (sub_A59AB0 wraps it): a window in the stack is closed (vtable+36 → IUiClosablePanel.Close)
    /// and the stack compacts — "open again = close"; a window not in the stack is pushed on top (vtable+32 → open), pushing
    /// out the oldest when 4 are open, unless the 509 rule refuses it. A window that is still open but hidden (evicted, or
    /// refused before) counts as "not in the stack": it comes back on top without being reopened — this is what a panel's own
    /// toggle cannot do (it would close the hidden window first, #677 FieldMap note). open = how the panel opens itself.
    /// </summary>
    public UiToggleResult Toggle(IUiPanel panel,Action? open=null){
        if(panel.Layer!=UiLayer.Window||!windowOrder.Contains(panel)||faulted.Contains(panel))return UiToggleResult.Unavailable;
        SyncWindows();
        if(stack.Contains(panel)){
            if(panel is not IUiClosablePanel closable)return UiToggleResult.Unavailable;
            try{closable.Close();}catch(Exception ex){Fault(panel,"Close",ex);}
            SyncWindows();
            return stack.Contains(panel)?UiToggleResult.Unavailable:UiToggleResult.Closed;
        }
        if(RefusesPush(panel,CurrentMap))return UiToggleResult.Refused;
        if(evicted.Remove(panel)|refused.Remove(panel)){
            if(IsOpen(panel)){Push(panel);return UiToggleResult.Opened;}
        }
        if(open==null)return UiToggleResult.Unavailable;
        try{open();}catch(Exception ex){Fault(panel,"Open",ex);return UiToggleResult.Unavailable;}
        SyncWindows();
        return stack.Contains(panel)?UiToggleResult.Opened:UiToggleResult.Unavailable;
    }

    // ---- R7-H (T347, #810): sub_A57C60(0) — "a1 == 0 closes all four" (FU4§1.1: ESC closes the stacked windows, at most 4).
    /// <summary>
    /// Closes every window in the stack, top first (sub_A57C60 with a1 == 0 calls vtable+36 on slots 0..3 and clears the
    /// array). Only IUiClosablePanel windows can be closed; any other window stays where it is (the engine cannot close it for
    /// the panel). Windows pushed out of the stack (evicted / refused) are not in the original array and are left alone.
    /// Returns the windows that actually closed (empty = nothing happened, e.g. no window open), top first.
    /// </summary>
    public IReadOnlyList<IUiPanel> CloseAll(){
        var closed=new List<IUiPanel>();
        foreach(var w in WindowStack){
            if(w is not IUiClosablePanel closable)continue;
            try{closable.Close();}catch(Exception ex){Fault(w,"Close",ex);}
            if(!IsOpen(w)||faulted.Contains(w))closed.Add(w);
        }
        SyncWindows();
        if(closed.Count>0)UiResourceLog.Write($"視窗堆疊全部關閉：{string.Join("、",closed.Select(p=>p.Id))}");
        return closed;
    }

    /// <summary>A module that throws during Update/Paint/input is switched off (logged once) instead of taking the client down.</summary>
    internal void Fault(IUiPanel panel,string phase,Exception ex){
        if(faulted.Add(panel))UiResourceLog.Write($"panel {panel.Id} {phase} 失敗，已停用：{ex.GetType().Name}: {ex.Message}");
    }

    public void Update(IUiGameState state){
        foreach(var p in panels.ToArray()){if(faulted.Contains(p))continue;try{p.Update(state);}catch(Exception ex){Fault(p,"Update",ex);}}
        SyncWindows();
    }

    /// <summary>Composes one frame: black, then the world inside WorldRect, then every visible panel clipped to its Bounds.</summary>
    public void Compose(UiCanvas canvas,Action<Graphics,Rectangle>? world){
        canvas.Clear(Color.Black);
        if(world!=null){canvas.Restrict(WorldRect);world(canvas.Graphics,WorldRect);}
        foreach(var p in PaintOrder()){
            if(!Usable(p))continue;
            canvas.Restrict(p.Bounds);
            try{p.Paint(canvas);}catch(Exception ex){Fault(p,"Paint",ex);}
        }
        canvas.Release();
    }

    /// <summary>
    /// Forces every container check up front (§2.1; T479 three layers): Tile and all 17 Sprite idx are loaded and structure-checked
    /// against their pak, and one entry of each pak is read (which first compares that container's anchor entries), then
    /// SurfList.xml is parsed. Throws UiResourceException on any mismatch or gap. No pak is hashed whole any more.
    /// </summary>
    public static void VerifyResources(UiResources resources){
        foreach(var archive in new[]{resources.Tile}.Concat(resources.Sprites)){
            var entries=archive.Entries;
            if(entries.Count==0)throw new UiResourceException($"{archive.Pin.Name}: 缺少 idx 或 idx 沒有任何項目");
            var probe=entries.Values.OrderBy(e=>e.Record).FirstOrDefault(e=>e.Size is >0 and <=ClientArchive.MaxEntrySize)
                ??throw new UiResourceException($"{archive.Pin.Name}: 沒有可讀取的項目");
            archive.Read(probe);
        }
        _=resources.Surfs.Files;
    }

    /// <summary>Verifies resources, creates every registered panel and loads it from its XML. All-or-nothing.</summary>
    public static UiRoot Build(UiResources resources,IReadOnlyList<UiPanelRegistration> registrations){
        VerifyResources(resources);
        var root=new UiRoot(resources);
        var layouts=new Dictionary<string,UiLayout>(StringComparer.OrdinalIgnoreCase);
        foreach(var reg in registrations){
            if(!layouts.TryGetValue(reg.Layout,out var layout))layouts[reg.Layout]=layout=resources.Xml.Load(reg.Layout);
            var panel=reg.Create(root);
            if(panel.Id!=reg.Id)throw new InvalidOperationException($"註冊 {reg.Id} 建立出的面板 Id 是 {panel.Id}");
            panel.Load(layout,resources.Surfs);
            root.Add(panel);
        }
        foreach(var g in root.groups.Values)g.Reset();
        return root;
    }
}

/// <summary>
/// Optional: a window the root may close when the stack is full (sub_A57C60 calls the evicted window's vtable+36).
/// After Close the panel should report Visible=false or empty Bounds; until then UiRoot keeps it hidden.
/// </summary>
public interface IUiClosablePanel:IUiPanel {
    void Close();
}

/// <summary>R5-A (#733): what UiRoot.Toggle did. Unavailable = not a registered window, faulted, or no way to open/close it.</summary>
public enum UiToggleResult { Unavailable, Opened, Closed, Refused }

/// <summary>
/// The nativeUi switch (§1.1). T317 (#751, user decision #743-1): default ON for the normal product entry.
/// Precedence, lowest to highest: default (<see cref="DefaultOn"/>) → settings file
/// (&lt;stage&gt;/runtime/native-ui.json, {"nativeUi":true|false,"nativeUiFill":false}) → --native-ui (on, kept for
/// compatibility) → --classic-ui (off: legacy WinForms HUD, for comparison and troubleshooting). A malformed file, or a
/// nativeUi value that is not a JSON boolean, is ignored (the default stays).
/// </summary>
public sealed record UiOptions(bool Enabled,bool Fill,string Source) {
    public const string Argument="--native-ui";
    public const string ClassicArgument="--classic-ui";
    public const string SettingsFileName="native-ui.json";
    public static readonly UiOptions Off=new(false,false,"default");
    public static readonly UiOptions On=new(true,false,"default");
    /// <summary>Set by Program.Main when --native-ui was on the command line.</summary>
    public static bool CommandLine{get;set;}
    /// <summary>Set by Program.Main when --classic-ui was on the command line.</summary>
    public static bool Classic{get;set;}
    /// <summary>
    /// Default when neither the settings file nor an argument decides. true (native UI) for the normal product; Program.Main
    /// sets it to false for the diagnostic harness entries (--demo, --evidence, --login-demo …) so they keep the HUD they
    /// were written against unless --native-ui is given.
    /// </summary>
    public static bool DefaultOn{get;set;}=true;

    public static UiOptions Resolve(bool commandLine,string? settingsPath)=>Resolve(commandLine,Classic,settingsPath,DefaultOn);

    public static UiOptions Resolve(bool commandLine,bool classic,string? settingsPath,bool defaultOn){
        bool enabled=defaultOn,fill=false;string source="default";
        if(settingsPath!=null&&File.Exists(settingsPath)){
            try{
                using var doc=JsonDocument.Parse(File.ReadAllText(settingsPath));
                if(doc.RootElement.ValueKind==JsonValueKind.Object){
                    if(doc.RootElement.TryGetProperty("nativeUi",out var n)&&n.ValueKind is JsonValueKind.True or JsonValueKind.False){enabled=n.ValueKind==JsonValueKind.True;source="settings";}
                    if(doc.RootElement.TryGetProperty("nativeUiFill",out var f)&&f.ValueKind is JsonValueKind.True)fill=true;
                }
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){UiResourceLog.Write($"{SettingsFileName} 無法讀取，使用預設（{(defaultOn?"原生 UI":"舊介面")}）：{ex.Message}");}
        }
        if(commandLine){enabled=true;source="argument";}
        if(classic){enabled=false;source="argument";}
        return new(enabled,enabled&&fill,source); // records compare by value: equals Off / On when nothing but the default decided
    }

    /// <summary>
    /// Removes --native-ui and --classic-ui from args (so the normal product entry still sees "no arguments") and records them.
    /// Both given → --classic-ui wins (Resolve).
    /// </summary>
    public static string[] Strip(string[] args){
        if(!args.Contains(Argument)&&!args.Contains(ClassicArgument))return args;
        if(args.Contains(Argument))CommandLine=true;
        if(args.Contains(ClassicArgument))Classic=true;
        return args.Where(a=>a!=Argument&&a!=ClassicArgument).ToArray();
    }
}

public enum UiStartState { Off, Verifying, Active, Failed }
