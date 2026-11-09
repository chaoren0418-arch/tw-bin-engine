using System.Reflection;

// T270（#645）：DemoChecks 測試群組自動發現。新增測試只要在 static 方法上加 [CheckGroup("名稱")]，
// 不必再改 Program.cs 或 DemoChecks.csproj（csproj 以萬用字元納入 ../*-tests/**/*.cs）。規則見 docs/design/m6-contract.md「自動發現」。
//
//   [CheckGroup("doors")]                                   static async Task DoorChecks(){…}        // 命令列群組 doors
//   [CheckGroup(CheckGroups.Default)]                        static void X(){…}                       // 不帶群組的預設全套也跑
//   [CheckGroup("map-continuity",Order=260)]                 …                                         // 群組內順序（小的先跑）
//
// 方法須為 static、回傳 void 或 Task、宣告在任何型別上（通常是 partial class Checks）。參數依名稱／型別繫結：
//   string repo|root → args[0]（repo 根目錄）；string output → args[1]（結果 JSON 路徑）；[CheckArg(n)] string x → args[n]；
//   string[] args → 整個命令列；Action<bool,string> → Checks.Check；有預設值的參數用預設值。
// 每次執行都先跑 CheckGroups.Preamble 群組（隊伍、寵物、變身基本組），之後的群組不會再跑同一個方法。

[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]
sealed class CheckGroupAttribute(string name):Attribute{
    public string Name{get;}=name;
    /// <summary>群組內順序；未指定時排在所有指定順序之後，再依型別與方法名稱（ordinal）排序。</summary>
    public int Order{get;set;}=CheckGroups.Unordered;
    /// <summary>只用於 Preamble：失敗時記下來、繼續跑其他群組，結束時才回報失敗（寵物組的既有行為）。</summary>
    public bool DeferFailure{get;set;}
}

/// <summary>把 string 參數繫結到 args[index]。</summary>
[AttributeUsage(AttributeTargets.Parameter)]
sealed class CheckArgAttribute(int index):Attribute{public int Index{get;}=index;}

static class CheckGroups {
    /// <summary>不帶群組名稱時執行的預設全套。</summary>
    public const string Default="(default)";
    /// <summary>每次執行都先跑的群組。</summary>
    public const string Preamble="(preamble)";
    public const int Unordered=100000;

    public sealed record Entry(string Group,int Order,bool DeferFailure,MethodInfo Method){
        public string Label=>$"{Method.DeclaringType?.FullName}.{Method.Name}";
    }

    /// <summary>掃描指定組件內所有 [CheckGroup] 方法；排序與反射列舉順序無關，結果可重現。</summary>
    public static IReadOnlyList<Entry> Discover(Assembly assembly)=>Discover(assembly.GetTypes().Where(t=>!P06.NativeUi.ExcludeFromDiscoveryAttribute.Excluded(t)));
    public static IReadOnlyList<Entry> Discover(IEnumerable<Type> types)=>
        types.SelectMany(t=>t.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            .SelectMany(m=>m.GetCustomAttributes<CheckGroupAttribute>().Select(a=>new Entry(a.Name,a.Order,a.DeferFailure,m)))
            .OrderBy(e=>e.Group,StringComparer.Ordinal).ThenBy(e=>e.Order)
            .ThenBy(e=>e.Method.DeclaringType?.FullName,StringComparer.Ordinal).ThenBy(e=>e.Method.Name,StringComparer.Ordinal)
            .ThenBy(e=>e.Method.ToString(),StringComparer.Ordinal).ToList();

    /// <summary>可在命令列使用的群組名稱（Preamble 方法上的其他名稱也算，例如 party、pets、polymorph）。</summary>
    public static IReadOnlySet<string> Names(IReadOnlyList<Entry> entries)=>
        entries.Select(e=>e.Group).Where(g=>g is not (Default or Preamble)).ToHashSet(StringComparer.Ordinal);

    /// <summary>簽章錯誤的註冊：非 void／Task 回傳、無法繫結的參數。</summary>
    public static IReadOnlyList<string> Errors(IReadOnlyList<Entry> entries){
        var errors=new List<string>();
        foreach(var e in entries.DistinctBy(e=>e.Method)){
            if(e.Method.ReturnType!=typeof(void)&&e.Method.ReturnType!=typeof(Task))errors.Add($"{e.Label}: 回傳型別應為 void 或 Task");
            if(e.Method.IsGenericMethodDefinition)errors.Add($"{e.Label}: 不可為泛型方法");
            foreach(var p in e.Method.GetParameters())if(BindError(p) is string error)errors.Add($"{e.Label}({p.Name}): {error}");
        }
        foreach(var dup in entries.GroupBy(e=>(e.Group,e.Method)).Where(g=>g.Count()>1))errors.Add($"{dup.First().Label}: 群組 {dup.Key.Group} 重複註冊");
        return errors;
    }
    static string? BindError(ParameterInfo p){
        if(p.GetCustomAttribute<CheckArgAttribute>() is {} a)return p.ParameterType==typeof(string)&&a.Index>=0?null:"[CheckArg] 只能用在 string 參數，位置不可為負";
        if(p.ParameterType==typeof(string)&&p.Name is "repo" or "root" or "output")return null;
        if(p.ParameterType==typeof(string[])&&p.Name=="args")return null;
        if(p.ParameterType==typeof(Action<bool,string>))return null;
        if(p.HasDefaultValue)return null;
        return "無法繫結（可用 string repo／root／output、[CheckArg(n)] string、string[] args、Action<bool,string>，或給預設值）";
    }
    static object? Bind(ParameterInfo p,string[] args,Action<bool,string> check){
        string Arg(int i)=>i<args.Length?args[i]:throw new ArgumentException($"參數 {p.Name} 需要命令列第 {i} 個引數");
        if(p.GetCustomAttribute<CheckArgAttribute>() is {} a)return Arg(a.Index);
        if(p.ParameterType==typeof(string)&&p.Name is "repo" or "root")return Arg(0);
        if(p.ParameterType==typeof(string)&&p.Name=="output")return Arg(1);
        if(p.ParameterType==typeof(string[])&&p.Name=="args")return args;
        if(p.ParameterType==typeof(Action<bool,string>))return check;
        return p.DefaultValue;
    }

    /// <summary>執行一個方法（同步或非同步）；例外以原本的型別往外丟。</summary>
    public static async Task Invoke(Entry e,string[] args,Action<bool,string> check){
        var values=e.Method.GetParameters().Select(p=>Bind(p,args,check)).ToArray();
        object? result;
        try{result=e.Method.Invoke(null,values);}
        catch(TargetInvocationException ex) when(ex.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();throw;}
        if(result is Task task)await task;
    }

    /// <summary>
    /// 先跑 Preamble，再跑指定群組（null＝預設全套）的方法；Preamble 已跑過的方法不重跑。
    /// 回傳延後回報的 Preamble 失敗（沒有則 null）。群組名稱不存在時丟 ArgumentException（與原本 switch 的 default 相同）。
    /// </summary>
    public static async Task<Exception?> Run(IReadOnlyList<Entry> entries,string? group,string[] args,Action<bool,string> check){
        if(group!=null&&!Names(entries).Contains(group))throw new ArgumentException("Unknown regression group");
        if(Errors(entries) is {Count:>0} errors)throw new InvalidOperationException("CheckGroup 註冊錯誤：\n"+string.Join("\n",errors));
        Exception? deferred=null;var ran=new HashSet<MethodInfo>();
        foreach(var e in entries.Where(e=>e.Group==Preamble)){
            ran.Add(e.Method);
            if(!e.DeferFailure){await Invoke(e,args,check);continue;}
            try{await Invoke(e,args,check);}catch(Exception ex){deferred??=ex;Console.Error.WriteLine(e.Method.Name+": "+ex);}
        }
        foreach(var e in entries.Where(e=>e.Group==(group??Default)))if(!ran.Contains(e.Method))await Invoke(e,args,check);
        return deferred;
    }
}
