using System.Reflection;
namespace P06.NativeUi;

// T270（#645）：原生 UI 面板自動發現。新面板在自己的檔案裡宣告一個帶 [UiPanel(順序)] 的 static 成員即自動註冊，
// 不必再改 UiPanelRegistry.cs。規則見 docs/design/m6-contract.md「自動發現」。
//   [UiPanel(150)] internal static UiPanelRegistration Registration=>new(PanelId,"uiextra86",root=>new XxxPanel());
//   [UiPanel(120)] public static IReadOnlyList<UiPanelRegistration> Registrations{get;}=…;   // 一個模組多個視窗
// 成員可以是 static 屬性、欄位或無參數方法，型別為 UiPanelRegistration 或 IEnumerable<UiPanelRegistration>。
// 順序：Order 小的先註冊（同一層內較晚註冊者畫在上面、先收到點擊）；同 Order 時依宣告型別全名、成員名稱（ordinal）。
// 違規（型別錯、Id 重複、Id／Layout 空白）不生效並記在 Errors，由 auto-discovery 模組測試擋下；產品不因此崩潰。

/// <summary>
/// 測試夾具用：標在型別上（含其巢狀型別）時，組件層級的自動發現（UiPanelDiscovery.Discover(Assembly)、DemoChecks 的 CheckGroups）
/// 會略過它；直接把型別傳給 Discover(types) 才會被掃描。
/// </summary>
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
public sealed class ExcludeFromDiscoveryAttribute:Attribute{
    public static bool Excluded(Type? t){for(;t!=null;t=t.DeclaringType)if(t.IsDefined(typeof(ExcludeFromDiscoveryAttribute),false))return true;return false;}
}

[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field|AttributeTargets.Method)]
public sealed class UiPanelAttribute(int order):Attribute{
    /// <summary>註冊順序（z-order 同層時的先後）。現有面板以 10 為間隔，新面板挑一個空位。</summary>
    public int Order{get;}=order;
}

public static class UiPanelDiscovery {
    public sealed record Result(IReadOnlyList<UiPanelRegistration> Panels,IReadOnlyList<string> Errors);

    public static Result Discover(Assembly assembly)=>Discover(assembly.GetTypes().Where(t=>!ExcludeFromDiscoveryAttribute.Excluded(t)));

    public static Result Discover(IEnumerable<Type> types){
        const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        var errors=new List<string>();
        var members=types.SelectMany(t=>t.GetMembers(Flags))
            .Select(m=>(member:m,attr:m.GetCustomAttribute<UiPanelAttribute>()))
            .Where(x=>x.attr!=null)
            .OrderBy(x=>x.attr!.Order).ThenBy(x=>x.member.DeclaringType?.FullName,StringComparer.Ordinal).ThenBy(x=>x.member.Name,StringComparer.Ordinal)
            .ToList();
        var panels=new List<UiPanelRegistration>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var (member,_) in members){
            string name=$"{member.DeclaringType?.FullName}.{member.Name}";
            object? value;
            try{
                value=member switch{
                    PropertyInfo p when p.GetIndexParameters().Length==0=>p.GetValue(null),
                    FieldInfo f=>f.GetValue(null),
                    MethodInfo m when m.GetParameters().Length==0&&!m.IsGenericMethodDefinition=>m.Invoke(null,null),
                    _=>throw new InvalidOperationException("只支援 static 屬性、欄位或無參數方法"),
                };
            }catch(Exception ex){errors.Add($"{name}: {(ex is TargetInvocationException{InnerException:{} inner}?inner.Message:ex.Message)}");continue;}
            IEnumerable<UiPanelRegistration?>? items=value switch{
                UiPanelRegistration one=>[one],
                IEnumerable<UiPanelRegistration?> many=>many,
                _=>null,
            };
            if(items==null){errors.Add($"{name}: 型別應為 UiPanelRegistration 或 IEnumerable<UiPanelRegistration>");continue;}
            foreach(var r in items){
                if(r==null||string.IsNullOrWhiteSpace(r.Id)||string.IsNullOrWhiteSpace(r.Layout)||r.Create==null){errors.Add($"{name}: 註冊的 Id、Layout、Create 不可空白");continue;}
                if(!ids.Add(r.Id)){errors.Add($"{name}: 面板 Id \"{r.Id}\" 重複註冊");continue;}
                panels.Add(r);
            }
        }
        return new(panels,errors);
    }
}
