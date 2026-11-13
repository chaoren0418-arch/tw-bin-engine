namespace P06;
// T462（#1047）：Effect 欄已移除——特效編號由伺服器封包帶（rule-tables.md 2.2），讀進來之後沒有程式使用。
public sealed record MagicSpell(int Id,string Name,int Mp,int Action,int ReuseMs,string Target,string Wire);
public static class MagicSpells {
    // T462（#1047）：不再讀 assets/magic/catalog.json（伺服器 SQL 產物）。各欄位的替代見 docs/reference/asset-recipes/rule-tables.md 2.2：
    //   名稱、MP ← 客戶端 spelldesc-c.tbl（ClientSpellTable）；讀不到時名稱「原服技能 N」、MP 0（技能窗顯示空白）。
    //   動作 ← SkillCast.Wire：DHH／DHHS 有方向（18），其他無方向（19）。
    //   冷卻 ← 原版預設 SpellListCooldowns.DefaultDelayMs（呼叫端再和動作時間取大值）。
    //   對象 ← Wire 決定攻擊／座標／書籤／名字；其餘看 SpellExDesc-c.tbl 的「對象：施法者」→ none，否則 buff。
    // 缺說明資料不會讓已學會的技能不能用；MP、冷卻與特效仍由伺服器裁定。
    public static MagicSpell? Get(int id) {
        if(!SkillCast.Supported(id))return null;
        string wire=SkillCast.Wire(id);var table=ClientSpellTable.Current;var spell=table?.Get(id);
        string target=wire switch{"DHH" or "DHHS"=>"attack","HH"=>"location","HD"=>"bookmark","S"=>"name",_=>table?.CasterOnly(id)==true?"none":"buff"};
        return new(id,spell?.Name??$"原服技能 {id}",spell?.Mp??0,wire is "DHH" or "DHHS"?18:19,SpellListCooldowns.DefaultDelayMs,target,wire);
    }
    public static bool AttachedEffect(int gfx)=>gfx is 11722 or 221 or 2165 or 189 or 191 or 197;
    public static string? AttachedEffect(ObjectAppearance appearance)=>new[]{"effect","heal","impact","breath"}
        .FirstOrDefault(action=>appearance.Supports(action,0));
}
