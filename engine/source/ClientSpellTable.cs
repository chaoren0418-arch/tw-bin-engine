using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace P06;

// T462（#1047）：技能名稱、名目 MP、「只能對自己」改讀使用者客戶端 Text.pak，取代伺服器 SQL 產生的
// dialogs/skills.json 與 magic/catalog.json。依據 docs/reference/asset-recipes/rule-tables.md 第 1、2 節（T446／#1031）。
// 執行期從 GroundSpriteReader.ClientRoot 的 Text.idx／Text.pak 在記憶體解出並核 pin，不寫檔、不複製素材。
//   spelldesc-c.tbl（TW16 字串 SpellDesc，VA 0x11801FC）：CP950；第一行是宣告筆數；之後每行
//     "<索引> <名稱> (<MP>/<HP>[/<材料數>])"，索引＝技能 id − 1。有幾行括號前沒有空白（例：索引 50「召喚術(30/30/3)」）；
//     括號內第一欄不是數字時（索引 107「會心一擊 (HP-1/MP-1)」）MP 未知。
//   SpellExDesc-c.tbl：CP950；每行 "<索引> <說明>"，說明以 ^ 分段、\fX 是色碼；「對象：施法者」→ 只能對自己（rule-tables.md 2.2）。
// 技能等級不用表：原版學魔法窗的算式 ((id−1)>>3)+1（rule-tables.md 1.4，TW16 sub_931FF0）。
public sealed class ClientSpellTable {
    public sealed record Spell(int Id,string Name,int? Mp);
    public const string NamesFileName="spelldesc-c.tbl",DetailFileName="SpellExDesc-c.tbl";
    // 解碼後內容的 SHA-256（rule-tables.md 1.2、2.2；Text.pak 第 32427、32434 筆）。
    public const string NamesSha256="387c4a9d6ffb3c1bd867127a0161c23d624d75eeb0db999e79691ff2e3d30ab9";
    public const string DetailSha256="2fcb23e0fd7f6278602f9de54e83ffcd3beb416a749eb8d1c901421f8a5feeb2";
    const string TargetLabel="對象：",CasterOnlyTarget="施法者";
    static readonly Regex colour=new(@"\\f.",RegexOptions.CultureInvariant|RegexOptions.Singleline);

    readonly Dictionary<int,Spell> spells;readonly HashSet<int>? casterOnly;
    ClientSpellTable(int declared,Dictionary<int,Spell> spells,HashSet<int>? casterOnly,string? detailError){
        Declared=declared;this.spells=spells;this.casterOnly=casterOnly;DetailError=detailError;
    }
    /// <summary>spelldesc-c.tbl 第一行宣告的筆數（真客戶端是 220）。</summary>
    public int Declared {get;}
    public IReadOnlyCollection<Spell> Spells=>spells.Values;
    /// <summary>SpellExDesc-c.tbl 讀不到的原因；null＝有讀到。讀不到時 <see cref="CasterOnly"/> 一律 null（不知道）。</summary>
    public string? DetailError {get;}
    /// <summary>技能 id（1 起算）；表裡沒有就是 null。</summary>
    public Spell? Get(int id)=>spells.GetValueOrDefault(id);
    /// <summary>說明的「對象：」是不是「施法者」；沒有說明表時 null。</summary>
    public bool? CasterOnly(int id)=>casterOnly?.Contains(id);
    /// <summary>原版學魔法窗的技能等級：((id−1)>>3)+1。</summary>
    public static int Level(int id)=>((id-1)>>3)+1;

    static Encoding Cp950(bool strict)=>CodePagesEncodingProvider.Instance.GetEncoding(950,EncoderFallback.ExceptionFallback,
        strict?DecoderFallback.ExceptionFallback:new DecoderReplacementFallback("�"))
        ??throw new InvalidDataException("code page 950 不可用");
    static IEnumerable<(int Index,string Text)> Rows(string text,out int declared){
        var lines=text.Split('\n');
        if(lines.Length==0||!int.TryParse(lines[0].TrimEnd('\r').Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out declared))
            throw new InvalidDataException("技能表第一行不是筆數");
        var rows=new List<(int,string)>();
        foreach(var raw in lines.Skip(1)){
            string line=raw.TrimEnd('\r');int space=line.IndexOf(' ');
            if(space<=0||!int.TryParse(line.AsSpan(0,space),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int index))continue;
            rows.Add((index,line[(space+1)..]));
        }
        return rows;
    }

    /// <summary>解析已解碼的兩個表；<paramref name="detail"/> 為 null 時只有名稱與 MP。索引超出宣告筆數或重複時拒絕。</summary>
    public static ClientSpellTable Parse(byte[] names,byte[]? detail=null,string? detailError=null){
        string text;
        try{text=Cp950(true).GetString(names);}catch(DecoderFallbackException){throw new InvalidDataException(NamesFileName+" 不是合法的 code page 950");}
        var spells=new Dictionary<int,Spell>();var rows=Rows(text,out int declared);
        foreach(var (index,body) in rows){
            if(index>=declared)throw new InvalidDataException($"{NamesFileName} 索引 {index} 超出宣告筆數 {declared}");
            int id=index+1;string name=body.Trim();int? mp=null;
            int open=name.LastIndexOf('(');
            if(open>0&&name.EndsWith(')')){
                string cost=name[(open+1)..^1];name=name[..open].TrimEnd();
                if(int.TryParse(cost.Split('/')[0],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int value))mp=value;
            }
            if(name.Length==0)continue;
            if(!spells.TryAdd(id,new(id,name,mp)))throw new InvalidDataException($"{NamesFileName} 索引 {index} 重複");
        }
        HashSet<int>? caster=null;
        if(detail!=null){
            caster=[];
            foreach(var (index,body) in Rows(Cp950(false).GetString(detail),out _)){
                foreach(var segment in body.Split('^')){
                    string s=colour.Replace(segment,"").Trim();
                    if(s.StartsWith(TargetLabel,StringComparison.Ordinal)){if(s[TargetLabel.Length..].Trim()==CasterOnlyTarget)caster.Add(index+1);break;}
                }
            }
        }
        return new(declared,spells,caster,detail==null?detailError??DetailFileName+" 未提供":null);
    }

    static byte[] ReadPinned(TextArchive archive,string name,string sha){
        var entry=archive.Single(name)??throw new InvalidDataException("Text 封存沒有 "+name);
        var decoded=archive.Read(entry);
        if(!Convert.ToHexString(SHA256.HashData(decoded)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(name+" pin 不符（不是支援的客戶端版本）");
        return decoded;
    }
    /// <summary>從 Text 封存讀兩個表並核 pin。名稱表讀不到就拋例外；說明表讀不到只記在 <see cref="DetailError"/>。</summary>
    public static ClientSpellTable Load(TextArchive archive){
        var names=ReadPinned(archive,NamesFileName,NamesSha256);
        byte[]? detail=null;string? error=null;
        try{detail=ReadPinned(archive,DetailFileName,DetailSha256);}catch(InvalidDataException ex){error=ex.Message;}
        return Parse(names,detail,error);
    }

    static readonly object gate=new();
    static ClientSpellTable? table,overrideTable;static bool loaded,overridden;
    /// <summary>執行期的表為什麼是 null（CI 沒有客戶端、pin 不符…）；有表時是 null。</summary>
    public static string? LoadError{get{lock(gate)return overridden?(overrideTable==null?"測試停用客戶端技能表":null):LoadErrorValue;}}
    static string? LoadErrorValue;
    /// <summary>執行期的表（延遲、唯讀）。讀不到是 null：技能名稱、MP 與學費無法顯示，相關功能明確提示不可用，不放替代資料。</summary>
    public static ClientSpellTable? Current{get{
        lock(gate){
            if(overridden)return overrideTable;
            if(!loaded){
                loaded=true;
                try{table=Load(TextArchive.Open(GroundSpriteReader.ClientRoot));LoadErrorValue=null;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException){
                    table=null;LoadErrorValue=ex.Message;System.Diagnostics.Trace.WriteLine("[spells] "+NamesFileName+" 無法載入："+ex.Message);
                }
            }
            return table;
        }
    }}
    /// <summary>只給測試：在回傳的 scope 釋放前改用指定的表（null＝沒有表）。</summary>
    public static IDisposable Use(ClientSpellTable? replacement){
        lock(gate){var previous=(overridden,overrideTable);overridden=true;overrideTable=replacement;return new Scope(()=>{lock(gate)(overridden,overrideTable)=previous;});}
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}
}
