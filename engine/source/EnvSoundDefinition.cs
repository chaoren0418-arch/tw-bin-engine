using System.Text.RegularExpressions;

namespace P06;

// T172（#446）：`envsound.def` 的解析與區域選擇。依據 docs/reference/sound.md §4.7、§6.3（T163／T171）。
public sealed record EnvSoundCandidate(int Id,int Seconds,int Panning);
// 群組是共用物件：冷卻（NextAt）與前次索引存在群組上，不是每個區域一份（829FD0）。
public sealed class EnvSoundGroup(string name,int seconds,EnvSoundCandidate[] candidates)
{
    public string Name {get;}=name;
    public int Seconds {get;}=seconds;
    public EnvSoundCandidate[] Candidates {get;}=candidates;
    public long NextAt {get;internal set;}=long.MinValue;
    public int LastIndex {get;internal set;}=-1;
}
public enum EnvSoundTime { Any=0, Day=1, Night=2 }
public sealed record EnvSoundArea(int Map,int X1,int Y1,int X2,int Y2)
{
    // 7CFC10：x1 == x2 視為不限制矩形；否則 543830 使用 x1 ≤ x < x2、y1 ≤ y < y2（與 music 的包含右下邊界不同）。
    public bool Contains(int map,int x,int y)=>map==Map&&(X1==X2||x>=X1&&x<X2&&y>=Y1&&y<Y2);
}
public sealed record EnvSoundRegion(int Ordinal,EnvSoundArea[] Areas,EnvSoundTime Time,int MainSound,EnvSoundGroup[] OneSounds)
{
    public bool Matches(int map,int x,int y,bool night)=>(Time==EnvSoundTime.Any||Time==(night?EnvSoundTime.Night:EnvSoundTime.Day))&&Areas.Any(a=>a.Contains(map,x,y));
}

public sealed class EnvSoundDefinition
{
    public EnvSoundGroup[] Groups {get;private set;}=[];
    public EnvSoundRegion[] Regions {get;private set;}=[];
    static readonly Regex Ints=new(@"-?\d+",RegexOptions.CultureInvariant);
    static readonly Regex Element=new(@"\[(/?)([^\]:]*)(?::([^\]]*))?\]|<(/?)([A-Za-z_]+)>",RegexOptions.CultureInvariant);

    static int Int(string s){int sign=1,i=0;if(s.StartsWith('-')){sign=-1;i=1;}long v=0;for(;i<s.Length&&char.IsAsciiDigit(s[i]);i++){v=v*10+(s[i]-'0');if(v>int.MaxValue)return sign*int.MaxValue;}return (int)(sign*v);}

    public static EnvSoundDefinition Parse(string text)
    {
        // 8287D0：分號起頭的行整行略過（行首空白之後）。
        var lines=(text??"").Replace("\r\n","\n").Replace('\r','\n').Split('\n').Where(l=>!l.TrimStart().StartsWith(';'));
        string body=string.Join('\n',lines);
        var def=new EnvSoundDefinition();
        var groups=new List<EnvSoundGroup>();var byName=new Dictionary<string,EnvSoundGroup>(StringComparer.Ordinal);
        var regions=new List<EnvSoundRegion>();
        EnvSoundArea[]? areas=null;EnvSoundTime time=EnvSoundTime.Any;int main=-1;List<EnvSoundGroup> one=[];bool pending=false;
        void Commit()
        {
            if(areas!=null&&pending)regions.Add(new(regions.Count,areas,time,main,one.ToArray()));
            pending=false;
        }
        int pos=0;
        while(pos<body.Length)
        {
            var m=Element.Match(body,pos);
            if(!m.Success)break;
            pos=m.Index+m.Length;
            if(m.Groups[2].Success&&m.Value.StartsWith('['))
            {
                if(m.Groups[1].Value=="/")continue; // 孤立的結尾標記。
                string name=m.Groups[2].Value;int seconds=m.Groups[3].Success?Int(m.Groups[3].Value.Trim()):0;
                // 結尾只按開頭名稱長度做前綴比對：`[/DEEPFORESTBIRD_SOUND0]` 也能結束 DEEPFORESTBIRD_SOUND。
                int end=body.IndexOf("[/"+name,pos,StringComparison.Ordinal);
                string inner=end<0?body[pos..]:body[pos..end];
                int closeBracket=end<0?-1:body.IndexOf(']',end);
                pos=closeBracket<0?body.Length:closeBracket+1;
                var candidates=new List<EnvSoundCandidate>();
                foreach(var token in inner.Split([' ','\t','\n'],StringSplitOptions.RemoveEmptyEntries))
                {
                    if(token.Length==0||!char.IsAsciiDigit(token[0]))continue;
                    var parts=token.Split(':');
                    candidates.Add(new(Int(parts[0]),parts.Length>1&&parts[1].Length>0?Int(parts[1]):seconds,parts.Length>2&&parts[2].Length>0?Int(parts[2]):-1));
                }
                var group=new EnvSoundGroup(name,seconds,candidates.ToArray());
                groups.Add(group);byName.TryAdd(name,group); // 同名時第一個生效（推測）。
                continue;
            }
            if(m.Groups[4].Value=="/")continue;
            string tag=m.Groups[5].Value;
            int close2=body.IndexOf("</"+tag+">",pos,StringComparison.Ordinal);
            string content=close2<0?"":body[pos..close2];
            if(close2>=0)pos=close2+tag.Length+3;
            switch(tag)
            {
                case "LOC":
                {
                    Commit();
                    var list=new List<EnvSoundArea>();
                    foreach(var part in content.Split('\\'))
                    {
                        var v=Ints.Matches(part).Select(x=>Int(x.Value)).ToArray();
                        if(v.Length==0)continue; // 每組都需自己的 map；末尾多的反斜線不產生位置。
                        int At(int k)=>k<v.Length?v[k]:0; // 省略的座標初始化為 0。
                        list.Add(new(At(0),At(1),At(2),At(3),At(4)));
                    }
                    areas=list.ToArray();time=EnvSoundTime.Any;main=-1;one=[];pending=false;
                    break;
                }
                case "TIME":
                    if(areas==null)break;
                    Commit(); // 同 LOC 再遇 TIME：先提交前一個時間版本，沿用位置清單並重設音效欄位。
                    string t=content.Trim();
                    time=t=="DAY"?EnvSoundTime.Day:t=="NIGHT"?EnvSoundTime.Night:EnvSoundTime.Any;
                    main=-1;one=[];pending=true;
                    break;
                case "MAIN_SOUND":
                    if(areas==null)break;
                    foreach(Match n in Ints.Matches(content))main=Int(n.Value); // 多個數字時最後一個覆寫。
                    pending=true;
                    break;
                case "ONE_SOUND":
                    if(areas==null)break;
                    foreach(var name in content.Split([' ','\t','\n'],StringSplitOptions.RemoveEmptyEntries))
                        if(byName.TryGetValue(name,out var g))one.Add(g); // 名稱區分大小寫；找不到就忽略。
                    pending=true;
                    break;
                default:break; // WEATHER、ONESOUND 等此版沒有對應分支。
            }
        }
        Commit();
        def.Groups=groups.ToArray();def.Regions=regions.ToArray();
        return def;
    }

    // 825660：依原順序選第一個同地圖、符合位置與時間的設定。
    public EnvSoundRegion? Region(int map,int x,int y,bool night)=>Regions.FirstOrDefault(r=>r.Matches(map,x,y,night));
}
