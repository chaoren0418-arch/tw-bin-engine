namespace P06.NativeUi;

// C2 (Issue #463, docs/design/native-ui.md §5.1): the contract every native UI module implements.
// Coordinates are always logical 800×600 pixels (§3.1); modules never see client/window coordinates.

/// <summary>Paint/hit-test layers, bottom to top (§3.1).</summary>
public enum UiLayer { World=0, Bar=1, Window=2, Float=3 }

public enum UiMouseKind { Down, Move, Up, Wheel }

/// <summary>Mouse event already mapped to logical coordinates (UiInput, §3.3).</summary>
public sealed record UiMouseEvent(UiMouseKind Kind,Point Location,MouseButtons Button,int Clicks,int Delta,Keys Modifiers) {
    public int X=>Location.X;
    public int Y=>Location.Y;
}

/// <summary>Key event; KeyData includes modifiers. Repeat = auto-repeat of a held key.</summary>
public sealed record UiKeyEvent(Keys KeyData,bool Down,bool Repeat) {
    public Keys KeyCode=>KeyData&Keys.KeyCode;
    public Keys Modifiers=>KeyData&Keys.Modifiers;
}

public interface IUiPanel {
    /// <summary>XML element name, e.g. "LEFT_WINDOW".</summary>
    string Id { get; }
    UiLayer Layer { get; }
    /// <summary>Logical 800×600 rectangle; painting is clipped to it and hit testing uses it.</summary>
    Rectangle Bounds { get; }
    bool Visible { get; }
    /// <summary>Reads XML and surfaces only; throws UiLayoutException (never invents default coordinates).</summary>
    void Load(UiLayout layout,SurfCatalog surfs);
    /// <summary>Per frame or on state change. Must not block and must not send packets.</summary>
    void Update(IUiGameState state);
    void Paint(UiCanvas canvas);
    /// <summary>true = handled. Down/Up/Move after a handled Down are captured by the same panel.</summary>
    bool Mouse(UiMouseEvent e,IUiCommands commands);
    bool Key(UiKeyEvent e,IUiCommands commands);
}

/// <summary>Optional: a window (normally UiLayer.Window) that the user can drag. UiInput clamps it inside 800×600.</summary>
public interface IUiMovablePanel:IUiPanel {
    /// <summary>true when a left press at this logical point should start a window drag (e.g. the title strip).</summary>
    bool IsDragHandle(Point location);
    void MoveTo(Point location);
}

/// <summary>
/// Read-only snapshot of existing game models (§5.1). No packet parsing here. Values the server never sent are null and
/// are shown blank — never estimated. Modules that need more fields add adapters in NativeUi/State/&lt;Module&gt;State.cs.
/// </summary>
public interface IUiGameState {
    bool InWorld { get; }
    string? CharacterName { get; }
    int? Level { get; }
    long? Hp { get; }
    long? MaxHp { get; }
    long? Mp { get; }
    long? MaxMp { get; }
    /// <summary>Monotonic milliseconds (Environment.TickCount64 in the product), for blink/timeout logic.</summary>
    long Now { get; }
}

/// <summary>Outward actions. Every member forwards to an existing action; UI modules never touch LoginConnection.</summary>
public interface IUiCommands {
    /// <summary>Opens the transitional dev-tools window (optionally on a named page). Not original behaviour; modules must report it.</summary>
    void ShowDevTools(string? page=null);
    void ReturnGameFocus();
    /// <summary>Existing logout/cancel path (same as the legacy HUD logout button).</summary>
    void Logout();
}

/// <summary>Stand-alone state for offline checks and the pre-login canvas.</summary>
public sealed record UiStaticGameState(bool InWorld=false,string? CharacterName=null,int? Level=null,long? Hp=null,long? MaxHp=null,long? Mp=null,long? MaxMp=null,long Now=0):IUiGameState;

/// <summary>Commands that do nothing (offline checks); records calls for assertions.</summary>
public sealed class UiNullCommands:IUiCommands {
    public List<string> Calls{get;}=[];
    public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
    public void ReturnGameFocus()=>Calls.Add("game-focus");
    public void Logout()=>Calls.Add("logout");
}
