namespace P06.NativeUi;

/// <summary>
/// Several panels sharing one bar slot; exactly one member is shown at a time (§5.1). "BAR_485" is the (485, 475) slot of
/// MINI_MAP_WINDOW / EX_MACRO_WINDOW / PARTY_PET_RT_WINDOW. TW13 cycles it with Alt+F3 in mode order 0 minimap → 1 extended
/// macro → 2 party/pet → 3 ChatFrameSub (ui-layout.md V5 (e)); a member that is not registered is simply skipped.
/// Members call IsShown(Id) from their Visible property.
/// </summary>
public sealed class UiSlotGroup {
    public const string Bar485="BAR_485";
    public static readonly Point Bar485Location=new(485,475);
    /// <summary>V5 (e) mode order. CHAT_FRAME_SUB has no module yet; the name is a placeholder id, not an XML element.</summary>
    public static readonly string[] Bar485Order=["MINI_MAP_WINDOW","EX_MACRO_WINDOW","PARTY_PET_RT_WINDOW","CHAT_FRAME_SUB"];
    readonly List<string> members=[];
    public string Name{get;}
    public string? Current{get;private set;}
    public IReadOnlyList<string> Members=>members;
    public event Action<UiSlotGroup>? Changed;
    public UiSlotGroup(string name){if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("slot group name",nameof(name));Name=name;}

    /// <summary>Adds a member; the first member added becomes current. Cycle order: V5 order for BAR_485, otherwise Add order.</summary>
    public void Add(string id){
        if(members.Contains(id))throw new InvalidOperationException($"{Name}: {id} 已在槽位群組中");
        int at=members.FindIndex(m=>OrderOf(Name,m)>OrderOf(Name,id));
        if(at<0)members.Add(id);else members.Insert(at,id);
        if(Current==null)Show(id);
    }
    public bool IsShown(string id)=>Current==id;
    /// <summary>Back to the first member (TW13 resets BAR_485 to mode 0 when entering the game, V5 (e) 3).</summary>
    public void Reset(){if(members.Count>0)Show(members[0]);}
    public void Show(string id){
        if(!members.Contains(id))throw new InvalidOperationException($"{Name}: {id} 不在槽位群組中");
        if(Current==id)return;Current=id;Changed?.Invoke(this);
    }
    /// <summary>Next member in cycle order (Alt+F3 behaviour); returns the new current id, or null when empty.</summary>
    public string? Cycle(){
        if(members.Count==0)return null;
        int i=Current==null?-1:members.IndexOf(Current);
        Show(members[(i+1)%members.Count]);return Current;
    }
    /// <summary>Members of BAR_485 are added in V5 order regardless of registration order.</summary>
    public static int OrderOf(string groupName,string id)=>groupName==Bar485&&Array.IndexOf(Bar485Order,id) is var i and >=0?i:int.MaxValue;
}
