using System.Reflection;
using System.Text.Json;
namespace P06;

// M7-A（T203／#509）：WorldSession 的模組擴充點。
// 新模組以 partial class WorldSession 新檔加上下列屬性即自動註冊，不必再改 WorldSession.cs／Program.cs：
//   [WorldReceive(122)]                 bool ReceiveAbility(byte[] plain)   // 消化封包回 true；回 false 視同未處理
//   [WorldReceive(250,Subtype=0x2A)]    bool ReceiveX(byte[] plain)         // S250 等以 plain[1] 為子碼的封包
//   [WorldReceive(87,Observe=true)]     void ObserveX(byte[] plain)         // 只觀察：每個該 opcode（子碼）封包都會先收到，不影響原分派
//   [WorldReset]                        void ResetX()                       // WorldSession.Reset() 結尾呼叫
//   [WorldSnapshot("ability")]          object? AbilitySnapshot()           // 放進 Snapshot().extensions["ability"]
//   [WorldModuleTest("ability")] static void Checks(WorldModuleTestContext t) // 由 --module-tests 自動執行（任何型別的 static 方法）
// 規則：
// - 消化型處理器只在原主分派「沒有任何 case 處理」時才被呼叫（即原本會被記為 unhandled 的封包），不會搶走既有模組的封包。
// - 同一 opcode：先找完全符合子碼的處理器，找不到再找不限子碼（Subtype=-1）的處理器。
// - 同一 (opcode,子碼) 只能有一個消化型處理器；快照 key 不可重複。違規或簽章錯誤的註冊不生效，記在 Errors，
//   由 world-extensions 模組測試擋下（CI 會跑），產品不因此崩潰。
// - 註冊順序以方法名稱（ordinal）排序，與反射列舉順序無關，結果可重現。

[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]
public sealed class WorldReceiveAttribute(byte opcode):Attribute{
    public byte Opcode{get;}=opcode;
    /// <summary>plain[1] 子碼（0–255）；-1 表示不限子碼。</summary>
    public int Subtype{get;set;}=-1;
    /// <summary>true＝只觀察（void，不消化）；false＝消化型（bool）。</summary>
    public bool Observe{get;set;}
}
[AttributeUsage(AttributeTargets.Method)]
public sealed class WorldResetAttribute:Attribute{}
[AttributeUsage(AttributeTargets.Method)]
public sealed class WorldSnapshotAttribute(string key):Attribute{public string Key{get;}=key;}
[AttributeUsage(AttributeTargets.Method)]
public sealed class WorldModuleTestAttribute(string name):Attribute{public string Name{get;}=name;}

public sealed record WorldExtensionEntry(string Kind,byte? Opcode,int? Subtype,string? Key,string Method);

public sealed class WorldExtensionRegistry {
    public static WorldExtensionRegistry Default {get;}=FromType(typeof(WorldSession));
    readonly Dictionary<(byte op,int sub),Func<WorldSession,byte[],bool>> handlers=[];
    readonly Dictionary<(byte op,int sub),List<Action<WorldSession,byte[]>>> observers=[];
    readonly List<Action<WorldSession>> resets=[];
    readonly List<(string key,Func<WorldSession,object?> read)> snapshots=[];
    readonly List<WorldExtensionEntry> entries=[];
    readonly List<string> errors=[];
    public IReadOnlyList<WorldExtensionEntry> Entries=>entries;
    public IReadOnlyList<string> Errors=>errors;
    public bool HasObservers=>observers.Count>0;

    /// <summary>掃描型別上所有帶擴充屬性的方法：WorldSession 的實例方法，或第一個參數是 WorldSession 的 static 方法。</summary>
    public static WorldExtensionRegistry FromType(Type type){
        var r=new WorldExtensionRegistry();
        foreach(var m in type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).OrderBy(m=>m.Name,StringComparer.Ordinal).ThenBy(m=>m.ToString(),StringComparer.Ordinal))r.Add(m);
        return r;
    }
    void Add(MethodInfo m){
        string name=$"{m.DeclaringType?.Name}.{m.Name}";
        foreach(var a in m.GetCustomAttributes<WorldReceiveAttribute>()){
            if(a.Subtype is < -1 or > 255){errors.Add($"{name}: Subtype {a.Subtype} 超出 -1..255");continue;}
            var key=(a.Opcode,a.Subtype);
            if(a.Observe){
                if(Bind<Action<WorldSession,byte[]>>(m,name,"void (byte[] plain)") is not {} o)continue;
                if(!observers.TryGetValue(key,out var list))observers[key]=list=[];
                list.Add(o);entries.Add(new("observe",a.Opcode,a.Subtype,null,name));
            } else {
                if(handlers.ContainsKey(key)){errors.Add($"{name}: S{a.Opcode}{SubText(a.Subtype)} 已由 {entries.First(e=>e.Kind=="receive"&&e.Opcode==a.Opcode&&e.Subtype==a.Subtype).Method} 註冊");continue;}
                if(Bind<Func<WorldSession,byte[],bool>>(m,name,"bool (byte[] plain)") is not {} h)continue;
                handlers[key]=h;entries.Add(new("receive",a.Opcode,a.Subtype,null,name));
            }
        }
        if(m.GetCustomAttribute<WorldResetAttribute>()!=null&&Bind<Action<WorldSession>>(m,name,"void ()") is {} reset){resets.Add(reset);entries.Add(new("reset",null,null,null,name));}
        if(m.GetCustomAttribute<WorldSnapshotAttribute>() is {} s){
            if(string.IsNullOrWhiteSpace(s.Key))errors.Add($"{name}: 快照 key 不可空白");
            else if(snapshots.Any(x=>x.key==s.Key))errors.Add($"{name}: 快照 key \"{s.Key}\" 重複");
            else if(Bind<Func<WorldSession,object?>>(m,name,"object? ()") is {} read){snapshots.Add((s.Key,read));entries.Add(new("snapshot",null,null,s.Key,name));}
        }
    }
    T? Bind<T>(MethodInfo m,string name,string expected) where T:Delegate{
        // 實例方法以 open-instance delegate 綁定（第一個參數是 this），static 方法照原參數綁定。
        if(!m.IsStatic&&m.DeclaringType!=typeof(WorldSession)){errors.Add($"{name}: 實例方法只能宣告在 WorldSession（partial class）上");return null;}
        T? d=(T?)Delegate.CreateDelegate(typeof(T),m,throwOnBindFailure:false);
        if(d==null)errors.Add($"{name}: 簽章應為 {expected}{(m.IsStatic?"（static 方法第一個參數要加 WorldSession）":"")}");
        return d;
    }
    static string SubText(int sub)=>sub<0?"":$"/{sub}";
    internal static int SubtypeOf(byte[] plain)=>plain.Length>1?plain[1]:-1;
    internal void Observe(WorldSession session,byte opcode,byte[] plain){
        int sub=SubtypeOf(plain);
        if(sub>=0&&observers.TryGetValue((opcode,sub),out var exact))foreach(var o in exact)o(session,plain);
        if(observers.TryGetValue((opcode,-1),out var any))foreach(var o in any)o(session,plain);
    }
    internal bool Receive(WorldSession session,byte opcode,byte[] plain){
        int sub=SubtypeOf(plain);
        if(sub>=0&&handlers.TryGetValue((opcode,sub),out var exact))return exact(session,plain);
        return handlers.TryGetValue((opcode,-1),out var any)&&any(session,plain);
    }
    internal void Reset(WorldSession session){foreach(var r in resets)r(session);}
    internal Dictionary<string,object?> Snapshot(WorldSession session){var d=new Dictionary<string,object?>(StringComparer.Ordinal);foreach(var (key,read) in snapshots)d[key]=read(session);return d;}
}

public sealed partial class WorldSession {
    WorldExtensionRegistry extensionRegistry=WorldExtensionRegistry.Default;
    readonly System.Collections.Concurrent.ConcurrentDictionary<(byte op,byte sub),int> unhandledSubtypes=new();
    /// <summary>測試用：換成指定的登錄表（例如測試夾具型別），不影響其他 session。</summary>
    internal void UseExtensions(WorldExtensionRegistry registry)=>extensionRegistry=registry;
    public IReadOnlyList<WorldExtensionEntry> ExtensionEntries=>extensionRegistry.Entries;
    void ObserveExtensions(byte opcode,byte[] plain){if(extensionRegistry.HasObservers)extensionRegistry.Observe(this,opcode,plain);}
    bool ReceiveExtension(byte opcode,byte[] plain)=>extensionRegistry.Receive(this,opcode,plain);
    void ResetExtensions(){unhandledSubtypes.Clear();extensionRegistry.Reset(this);}
    Dictionary<string,object?> ExtensionSnapshot()=>extensionRegistry.Snapshot(this);
    // S250 依子碼分派；另記未處理子碼，給後續模組查缺。
    void CountUnhandledSubtype(byte opcode,byte[] plain){if(opcode==250&&plain.Length>1)unhandledSubtypes.AddOrUpdate((opcode,plain[1]),1,(_,c)=>c+1);}
    Dictionary<string,int> UnhandledSubtypeSnapshot()=>unhandledSubtypes.OrderBy(x=>x.Key.op).ThenBy(x=>x.Key.sub).ToDictionary(x=>$"0x{x.Key.op:X2}/0x{x.Key.sub:X2}",x=>x.Value);
}

/// <summary>模組離線測試的共用紀錄器。</summary>
public sealed class WorldModuleTestContext(string name) {
    public string Name {get;}=name;
    readonly List<object> failures=[];
    public int Checks {get;private set;}
    public IReadOnlyList<object> Failures=>failures;
    public void Check(bool condition,string label,object? detail=null){Checks++;if(!condition)failures.Add(new{label,detail});}
    public void Equal<T>(T expected,T actual,string label){Checks++;if(!EqualityComparer<T>.Default.Equals(expected,actual))failures.Add(new{label,expected,actual});}
    public void Throws<TException>(Action action,string label) where TException:Exception{
        Checks++;
        try{action();failures.Add(new{label,error="沒有丟出 "+typeof(TException).Name});}
        catch(TException){}
        catch(Exception ex){failures.Add(new{label,error=ex.GetType().Name+": "+ex.Message});}
    }
    /// <summary>把 WorldSession.Snapshot() 轉成 JSON 元素，方便讀 extensions 等欄位。</summary>
    public static JsonElement SnapshotJson(WorldSession session)=>JsonSerializer.SerializeToElement(session.Snapshot());
}

/// <summary>Program.cs 的 --module-tests 入口：自動找出組件內所有 [WorldModuleTest] static 方法並執行。</summary>
public static class WorldModuleTests {
    public static IReadOnlyList<(string name,MethodInfo method)> Discover()=>
        typeof(WorldModuleTests).Assembly.GetTypes()
            .SelectMany(t=>t.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            .SelectMany(m=>m.GetCustomAttributes<WorldModuleTestAttribute>().Select(a=>(name:a.Name,method:m)))
            .OrderBy(x=>x.name,StringComparer.Ordinal).ThenBy(x=>x.method.DeclaringType?.FullName,StringComparer.Ordinal).ThenBy(x=>x.method.Name,StringComparer.Ordinal).ToList();
    /// <param name="filter">null 或空字串＝全部；否則以逗號分隔的測試名稱。</param>
    /// <returns>0＝全部通過；1＝有失敗或找不到指定的測試。</returns>
    public static int Run(string output,string? filter){
        var wanted=string.IsNullOrWhiteSpace(filter)?null:filter.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var all=Discover();var selected=all.Where(x=>wanted==null||wanted.Contains(x.name)).ToList();
        var missing=wanted?.Where(w=>all.All(x=>x.name!=w)).OrderBy(x=>x,StringComparer.Ordinal).ToArray()??[];
        var results=new List<object>();int checks=0,failed=0;
        foreach(var (name,method) in selected){
            var t=new WorldModuleTestContext(name);string? error=null;
            try{
                if(method.ReturnType!=typeof(void)||method.GetParameters() is not [{} p]||p.ParameterType!=typeof(WorldModuleTestContext))error="簽章應為 static void M(WorldModuleTestContext t)";
                else method.Invoke(null,[t]);
            }
            catch(TargetInvocationException ex) when(ex.InnerException!=null){error=ex.InnerException.ToString();}
            catch(Exception ex){error=ex.ToString();}
            bool pass=error==null&&t.Failures.Count==0;checks+=t.Checks;if(!pass)failed++;
            results.Add(new{name,method=$"{method.DeclaringType?.FullName}.{method.Name}",pass,checks=t.Checks,failures=t.Failures,error});
        }
        bool ok=failed==0&&missing.Length==0&&selected.Count>0;
        Json.Save(output,new{status=ok?"pass":"fail",kind="offline module tests (synthetic packets, no game, network or assets)",tests=selected.Count,failed,checks,missing,results});
        return ok?0:1;
    }
}
