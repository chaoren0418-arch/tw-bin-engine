namespace P06;
public sealed record MagicSpell(int Id,string Name,int Mp,int Action,int Effect,int ReuseMs,string Target,string Wire);
public static class MagicSpells {
    static readonly Lazy<Dictionary<int,MagicSpell>> spells=new(()=>{
        var json=Json.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/magic/catalog.json")));
        if(json.S("schema")!="p24-magic-casting-v1")throw new InvalidDataException("Unsupported casting catalog");
        var rows=json.GetProperty("spells").EnumerateArray().Select(s=>new MagicSpell(s.I("id"),s.S("name"),s.I("mp"),s.I("action"),s.I("effect"),s.I("reuseMs"),s.S("target"),s.S("wire"))).ToDictionary(s=>s.Id);
        if(!rows.Keys.Order().SequenceEqual(new[]{1,3,4,8})||rows.Values.Any(s=>s.Action is not (18 or 19)||s.Wire is not ("D" or "DHH")||s.Mp<0||s.ReuseMs<0))throw new InvalidDataException("Unsupported casting profile");
        return rows;
    });
    public static MagicSpell? Get(int id)=>spells.Value.GetValueOrDefault(id);
    public static bool AttachedEffect(int gfx)=>gfx is 11722 or 221 or 2165 or 189 or 191 or 197;
    public static string? AttachedEffect(ObjectAppearance appearance)=>new[]{"effect","heal","impact","breath"}
        .FirstOrDefault(action=>appearance.Supports(action,0));
}
