namespace P06;
public sealed record MagicSpell(int Id,string Name,int Mp,int Action,int Effect,int ReuseMs,string Target,string Wire);
public static class MagicSpells {
    static readonly Lazy<Dictionary<int,MagicSpell>> spells=new(()=>{
        string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/magic/catalog.json"));
        if(!File.Exists(path))return new();
        var json=Json.Read(path);
        if(json.S("schema")!="p24-magic-casting-v1")throw new InvalidDataException("Unsupported casting catalog");
        var rows=json.GetProperty("spells").EnumerateArray().Select(s=>new MagicSpell(s.I("id"),s.S("name"),s.I("mp"),s.I("action"),s.I("effect"),s.I("reuseMs"),s.S("target"),SkillCast.Wire(s.I("id")))).ToDictionary(s=>s.Id);
        if(rows.Values.Any(s=>s.Action is not (18 or 19)||s.Mp<0||s.ReuseMs<0))throw new InvalidDataException("Unsupported casting profile");
        return rows;
    });
    public static MagicSpell? Get(int id) {
        if(!SkillCast.Supported(id))return null;
        if(spells.Value.TryGetValue(id,out var spell))return spell;
        string wire=SkillCast.Wire(id);
        // Missing presentation metadata must not disable an otherwise learned skill.
        // MP/effect/reuse remain unknown (0); the server owns costs, cooldown and effects.
        string target=wire switch{"DHH" or "DHHS"=>"attack","HH"=>"location","HD"=>"bookmark","S"=>"name",_=>"buff"};
        return new(id,$"原服技能 {id}",0,wire is "DHH" or "DHHS"?18:19,0,0,target,wire);
    }
    public static bool AttachedEffect(int gfx)=>gfx is 11722 or 221 or 2165 or 189 or 191 or 197;
}
