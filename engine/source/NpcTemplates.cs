using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06;
public sealed record NpcOption(int Id,string Label,string Kind,string Value,bool Enabled);
public sealed record NpcPage(string Text,NpcOption[] Options,string Limitation);
// Original markup is data. Arguments are inserted only into text, never reparsed as markup/actions.
public sealed class NpcTemplates {
    readonly Dictionary<string,string> templates;
    static readonly Regex tags=new(@"(<[^>]*>)",RegexOptions.Compiled);
    static readonly Regex attributes=new("([a-zA-Z]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))",RegexOptions.Compiled);
    public NpcTemplates(string path){templates=new(JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))!,StringComparer.OrdinalIgnoreCase);}
    static Dictionary<string,string> Attr(string token){var a=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(Match m in attributes.Matches(token))a[m.Groups[1].Value]=WebUtility.HtmlDecode(m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Success?m.Groups[3].Value:m.Groups[4].Value);return a;}
    public NpcPage Render(string key,string[] args,string npc,string player){
        if(!templates.TryGetValue(key,out string? html))return new("缺少此對話的原始繁中模板："+key,[],"未提供可操作選項；可關閉後重新對話。");
        var text=new StringBuilder();var label=new StringBuilder();var options=new List<NpcOption>();Dictionary<string,string>? anchor=null;bool unsupported=false;var missing=new HashSet<string>();
        void Append(string value){text.Append(value);if(anchor!=null)label.Append(value);}
        void Option(Dictionary<string,string> a,string title){string kind=a.ContainsKey("action")?"action":a.ContainsKey("link")?"link":"unsupported";string value=a.GetValueOrDefault(kind,"");bool enabled=value.Length is >0 and <=200&&!value.Any(char.IsControl)&&!value.Contains('<')&&!value.Contains('>')&&!a.ContainsKey("param");if(kind=="link")enabled&=templates.ContainsKey(value);else if(kind!="action")enabled=false;options.Add(new(options.Count,title.Trim(),kind,value,enabled));}
        foreach(string part in tags.Split(html)){
            if(!part.StartsWith('<')){Append(WebUtility.HtmlDecode(part));continue;}
            string tag=Regex.Match(part,@"^<\s*(/?[a-zA-Z]+)").Groups[1].Value.ToLowerInvariant();var a=Attr(part);
            switch(tag){
            case "a":if(anchor!=null){unsupported=true;anchor=null;}anchor=a;label.Clear();break;
            case "/a":if(anchor!=null){Option(anchor,label.ToString());anchor=null;}break;
            case "img":if(a.ContainsKey("action")||a.ContainsKey("link")){string cmd=a.GetValueOrDefault("action","");string name=cmd switch{"buy"=>"購買","sell"=>"出售",_=>a.GetValueOrDefault("alt","原圖示 "+a.GetValueOrDefault("src",""))};Option(a,name);}break;
            case "var":string src=a.GetValueOrDefault("src","");
                if(src.StartsWith('#')&&int.TryParse(src.AsSpan(1),out int i)&&i>=0){
                    // Q9-C (T438/#1009, followups-17 §3.4): #998 = player name, #999 = game time "%2d:%2d" (client variables, not arguments).
                    if(i==NativeUi.HyperTextClock.PlayerNameVar)Append(player);
                    else if(i==NativeUi.HyperTextClock.ClockVar)Append(NativeUi.HyperTextClock.VarTime(NativeUi.HyperTextClock.Value));
                    else if(i<args.Length)Append(args[i]);
                    // Original BIN 0x8676F0: missing index 0 uses the current dialog object's name.
                    // An explicitly supplied empty argument remains empty; no fallback for other indices.
                    else if(i==0&&npc.Length>0)Append(npc);
                    else{Append($"〔缺少參數 #{i}〕");missing.Add("#"+i);}
                }else{Append("〔不支援的參數〕");unsupported=true;}break;
            case "name":Append(npc);break;case "username":Append(player);break;
            // Q9-C (followups-17 §3.2): <date> = "%2d%s %2d%s" (month, string 762, day, string 763); <time> = "%02d:%02d".
            case "date":Append(NativeUi.HyperTextClock.Date(NativeUi.HyperTextClock.Value));break;
            case "time":Append(NativeUi.HyperTextClock.Time(NativeUi.HyperTextClock.Value));break;
            case "br":case "p":case "/p":Append("\n");break;
            case "body":case "/body":case "html":case "/html":case "font":case "/font":case "center":case "/center":case "pre":case "/pre":case "/img":case "b":case "/b":case "/var":break;
            default:unsupported=true;break;
            }
        }
        if(anchor!=null)unsupported=true;
        // Unsupported input/subHtml/dynamic structures cannot supply incomplete actions.
        bool dynamic=Regex.IsMatch(html,@"<\s*(input|select|subHtml|script)\b",RegexOptions.IgnoreCase);
        if(dynamic){options=options.Select(o=>o with{Enabled=false}).ToList();unsupported=true;}
        var notices=new List<string>();if(missing.Count>0)notices.Add("對話缺少參數 "+string.Join("、",missing)+"，相關文字無法完整顯示。");
        if(unsupported)notices.Add(dynamic?"此對話的輸入結構尚未支援，選項已停用。":"部分原始標記尚未支援。");
        return new(Regex.Replace(text.ToString(),@"\n\s*\n\s*\n","\n\n").Trim(),options.ToArray(),string.Join("\n",notices));
    }
}

// Q9-C (T438/#1009): game-time source for <date>/<time>/<var #999>. Observe-only (M7-A auto-registration); the core
// S123 / S8 handling is unchanged. The clock reads ServerTime / Status.Time of the session that last received one.
public sealed partial class WorldSession {
    [WorldReceive(123,Observe=true)]
    void ObserveHyperTextServerTime(byte[] plain)=>NativeUi.HyperTextClock.Observed(this,false);
    [WorldReceive(8,Observe=true)]
    void ObserveHyperTextStatusTime(byte[] plain)=>NativeUi.HyperTextClock.Observed(this,true);
}
