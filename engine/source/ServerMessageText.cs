namespace P06;
public sealed record ServerMessageReport(ushort Id,string[] Arguments,string Text,bool Recognized);
public static class ServerMessageText {
    // Original Text.pak/string-c.tbl: count header then zero-based IDs.
    // 125 = \f1你不能夠放棄此樣物品。 (work/issue-77/text-source.json)
    // 189 = \f1金幣不足。 ; 337 = \f1%0不足%s。 ; 403 = 獲得%0%o 。 .
    // Original BIN VA933850 (base400000): %0 is indexed; %s/%o emit only for CP949, not CP950.
    // Arguments are literal text, never parsed as format controls. Item tokens use the existing name catalog.
    // These are message meanings, not transaction acknowledgements or inferred item changes.
    public static ServerMessageReport Decode(ushort id,string[] arguments){
        string? known=id switch{125 when arguments.Length==0=>"你不能夠放棄此樣物品。",189 when arguments.Length==0=>"金幣不足。",337 when arguments.Length==1&&!string.IsNullOrWhiteSpace(arguments[0])=>arguments[0]+"不足。",403 when arguments.Length==1&&arguments[0].Length>0=>"獲得"+arguments[0]+" 。",_=>null};
        return new(id,arguments.ToArray(),known??$"原服訊息 {id}"+(arguments.Length>0?"："+string.Join("、",arguments):""),known!=null);
    }
}
