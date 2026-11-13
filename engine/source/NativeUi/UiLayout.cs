using System.Globalization;
using System.Xml;
using System.Xml.Linq;
namespace P06.NativeUi;

/// <summary>Missing / malformed layout data. Always carries the XML file name and the node path; no default coordinates are invented.</summary>
public sealed class UiLayoutException(string file,string path,string message,Exception? inner=null)
    :UiResourceException($"{file}{(path.Length>0?":"+path:"")}: {message}",inner){
    public string File=>file;
    public string NodePath=>path;
    /// <summary>The bare reason, without the "file:path: " prefix of Message.</summary>
    public string Reason=>message;
    /// <summary>Registered panel whose Load threw (set by UiRoot.Build; null when the XML itself failed before any panel ran).</summary>
    public string? PanelId{get;set;}
}

/// <summary>Parsed UI XML (element names kept verbatim, including original misspellings such as FOOD_GUAGE_BAR).</summary>
public sealed class UiLayout {
    public string FileName{get;}
    public UiNode Root{get;}
    UiLayout(string fileName,XElement root){FileName=fileName;Root=new UiNode(this,root,root.Name.LocalName);}

    public static UiLayout Parse(string text,string fileName){
        try{
            var doc=XDocument.Parse(text,LoadOptions.None);
            return new UiLayout(fileName,doc.Root??throw new UiLayoutException(fileName,"","沒有根元素"));
        }catch(XmlException ex){throw new UiLayoutException(fileName,"",$"XML 語法錯誤（行 {ex.LineNumber}）",ex);}
    }
    /// <summary>Load "&lt;baseName&gt;-c.xml" (fallback "&lt;baseName&gt;.xml") from the pinned Tile.pak under TW_CLIENT_ROOT.</summary>
    public static UiLayout Load(string baseName)=>UiResources.Shared.Xml.Load(baseName);
    public static UiLayout Load(UiXmlSource source,string baseName)=>source.Load(baseName);
    public UiNode Child(string path)=>Root.Child(path);
}

public sealed class UiNode {
    readonly UiLayout layout;
    readonly XElement element;
    public string Path{get;}
    internal UiNode(UiLayout layout,XElement element,string path){this.layout=layout;this.element=element;Path=path;}
    public string Name=>element.Name.LocalName;
    public UiLayout Layout=>layout;
    UiLayoutException Error(string message,string? path=null)=>new(layout.FileName,path??Path,message);

    public IReadOnlyList<UiNode> Children{get{
        var list=new List<UiNode>();var counts=new Dictionary<string,int>();
        foreach(var e in element.Elements()){
            string n=e.Name.LocalName;int k=counts.GetValueOrDefault(n);counts[n]=k+1;
            list.Add(new UiNode(layout,e,$"{Path}/{n}[{k}]"));
        }
        return list;
    }}
    public IEnumerable<UiNode> ChildrenNamed(string name)=>Children.Where(c=>c.Name==name);

    /// <summary>Unique child by "A/B/C" path (exact, case-sensitive names). Missing or ambiguous → UiLayoutException.</summary>
    public UiNode Child(string path){
        var node=this;
        foreach(var part in path.Split('/',StringSplitOptions.RemoveEmptyEntries)){
            var matches=node.element.Elements().Where(e=>e.Name.LocalName==part).Take(2).ToList();
            string at=node.Path+"/"+part;
            if(matches.Count==0)throw Error("缺少節點",at);
            if(matches.Count>1)throw Error("節點不唯一（有多個同名元素）",at);
            node=new UiNode(layout,matches[0],at);
        }
        return node;
    }
    public bool HasChild(string name)=>element.Elements().Any(e=>e.Name.LocalName==name);

    /// <summary>Raw attribute value, or null when absent.</summary>
    public string? Attr(string name)=>element.Attribute(name)?.Value;
    public string RequiredAttr(string name)=>Attr(name)??throw Error($"缺少屬性 {name}");

    public int Int(string name){
        string raw=RequiredAttr(name).Trim();
        if(!int.TryParse(raw,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int v))throw Error($"屬性 {name}=\"{raw}\" 不是整數");
        return v;
    }
    public int? TryInt(string name)=>Attr(name)==null?null:Int(name);

    /// <summary>
    /// "5726~5728" → [5726,5727,5728]; "3168" → [3168]; "" → []. The original parser only atoi()s the first number (V4 (b));
    /// the range is kept so panels can pick state images by the verified button rules.
    /// </summary>
    public int[] Surfs(string name="surfNumber"){
        string raw=RequiredAttr(name);
        try{return ParseSurfRange(raw);}catch(FormatException ex){throw new UiLayoutException(layout.FileName,Path,$"屬性 {name}=\"{raw}\"："+ex.Message,ex);}
    }
    public const int MaxSurfRange=64;
    public static int[] ParseSurfRange(string raw){
        // Real data: MACRO_ICON has surfNumber="" in 4 files (icon is chosen at runtime). The BIN's atoi gives 0;
        // what it draws for that is unverified, so an empty value means "no surf" here.
        if(raw.Trim().Length==0)return [];
        var parts=raw.Split('~');
        if(parts.Length>2)throw new FormatException("surf 範圍格式不合法");
        static int P(string s)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out int v)?v:throw new FormatException("surf 不是非負整數");
        int a=P(parts[0]);
        if(parts.Length==1)return [a];
        int b=P(parts[1]);
        if(b<a||b-a>=MaxSurfRange)throw new FormatException("surf 範圍顛倒或過大");
        return Enumerable.Range(a,b-a+1).ToArray();
    }
    public override string ToString()=>Path;
}
