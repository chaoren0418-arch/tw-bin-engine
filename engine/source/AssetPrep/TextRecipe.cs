using System.Globalization;
using System.Text;
using System.Text.Json;
namespace P06.AssetPrep;

// T453 (#1038, B15): recipe "text" — the two text files of the assets tree, rebuilt from the user's own Text.idx / Text.pak.
// Contract: docs/design/asset-recipe.md 1.1–1.3, 3.1 and 6; rules: docs/reference/asset-recipes/text.md 4 and 5.
//
//   item "names"     → objects/names.json      desc-c.tbl, one name per line; array index = name id ($n)
//   item "templates" → dialogs/templates.json  every *-c.html / *-c.htm, key = file name without "-c.<ext>"
//
// - Entries are found by name in the decoded index; no File Offset or record number of any earlier Text.pak is used.
// - Supported input version = the decoded SHA-256 of desc-c.tbl (the anchor of asset-recipe.md 1.2, the same value as
//   TextArchive.DescSha256). Both items check it first: another Text version stops the recipe with the entry named.
//   The templates themselves have no anchor (they legitimately differ between client builds, text.md 5).
// - Output paths are relative to the assets tree (asset-recipe.md 4.5). The single-recipe publish of AssetPrepRunner puts
//   them below assets/text/; composing the tree the product reads is the total flow's job (T467).
// - Nothing here holds a name or a template: only the rules, two file names and one hash.
// - Not produced here: dialogs/skills.json and magic/catalog.json (read at run time, T462); LiTo\LTtext is not overlaid.

[AssetRecipe]
public sealed class TextRecipe:IAssetRecipe {
    public const string RecipeId="text",NamesItem="names",TemplatesItem="templates";
    public const string NamesPath="objects/names.json",TemplatesPath="dialogs/templates.json";
    /// <summary>The whole recipe (what the total flow requests), in canonical order.</summary>
    public static IReadOnlyList<string> AllItems{get;}=[NamesItem,TemplatesItem];
    readonly string descSha256;

    public TextRecipe():this(TextArchive.DescSha256){}
    /// <summary>A recipe for another desc-c.tbl anchor. The product only uses the parameterless one; this is for synthetic archives in tests.</summary>
    public TextRecipe(string descSha256){
        if(descSha256 is not {Length:64}||!descSha256.All(Uri.IsHexDigit))throw new ArgumentException("desc-c.tbl 的錨點必須是 SHA-256 十六進位字串");
        this.descSha256=descSha256;
    }

    public string Id=>RecipeId;
    public int Version=>1;
    /// <summary>Complete for the two files this recipe owns; a request for one item only is still recorded as such in the receipt's requested list.</summary>
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Complete;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        foreach(string item in items)
            if(item is not (NamesItem or TemplatesItem))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"text：\"{item}\" 不是可準備的項目（只有 {NamesItem}、{TemplatesItem}）");
        return AllItems.Where(known=>items.Any(item=>item==known)).ToList();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item is not (NamesItem or TemplatesItem))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"text：\"{item}\" 不是可準備的項目");
        // Anchor first, for both items: missing, repeated or of another version → refused with the entry named.
        byte[] desc=context.ReadText(TextArchive.DescFileName,descSha256);
        if(item==NamesItem){
            var (declared,names)=ParseNames(desc);
            context.Write(NamesPath,SerializeNames(names));
            context.Set("declared",declared);
            context.Set("names",names.Count.ToString(CultureInfo.InvariantCulture));
            return;
        }
        var built=BuildTemplates(context.TextEntries(),context.ReadTextEntry,context.Cancel);
        context.Write(TemplatesPath,SerializeTemplates(built.Templates));
        string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
        context.Set("candidates",N(built.Candidates));
        context.Set("templates",N(built.Templates.Count));
        context.Set("duplicateIdentical",N(built.DuplicateIdentical));
        context.Set("invalidEncoding",N(built.InvalidEncoding.Count));
        context.Set("invalidEncodingEntries",string.Join(",",built.InvalidEncoding));
        context.Set("conflictKeys",N(built.ConflictKeys.Count));
        context.Set("conflictKeyList",string.Join(",",built.ConflictKeys));
        context.Set("conflictSkipped",N(built.ConflictSkipped));
    }

    // Code page 950 from the .NET code-pages provider (not the platform's), with exception fallbacks: text.md 5 — Python's
    // cp950 rejects three more files and maps five codes differently, so the table matters.
    static readonly Encoding cp950=CodePagesEncodingProvider.Instance.GetEncoding(950,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback)
        ??throw new InvalidOperationException("code page 950 不可用");
    /// <summary>Strict code page 950 → text; null when the bytes are not valid in that code page.</summary>
    public static string? DecodeCp950(ReadOnlySpan<byte> data){
        try{return cp950.GetString(data);}catch(DecoderFallbackException){return null;}
    }

    // ---- objects/names.json (text.md 4) ----

    /// <summary>
    /// desc-c.tbl → the name array: split on CR LF, drop the first line (the declared count, returned as written), keep
    /// everything else untouched — no trimming and no dropping of empty lines, or the ids would shift.
    /// </summary>
    public static (string Declared,IReadOnlyList<string> Names) ParseNames(ReadOnlySpan<byte> decoded){
        string text=DecodeCp950(decoded)??throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"text：容器 {AssetPrepInput.TextContainer} 的 {TextArchive.DescFileName} 不是合法的 code page 950 文字",AssetPrepInput.TextContainer);
        string[] lines=text.Split("\r\n");
        if(lines[0].Length is 0 or >9||lines[0].Any(c=>c is <'0' or >'9'))
            throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"text：容器 {AssetPrepInput.TextContainer} 的 {TextArchive.DescFileName} 第一行不是筆數",AssetPrepInput.TextContainer);
        return (lines[0],lines[1..]);
    }

    /// <summary>
    /// The byte form of the baseline names.json: a JSON string array, UTF-8 without BOM, ", " between elements, no line
    /// break, non-ASCII written as is; only '"', '\' and control characters are escaped (\" \\ \n \r \t \b \f, others \u00xx).
    /// System.Text.Json cannot be configured to this form, so it is written out here.
    /// </summary>
    public static byte[] SerializeNames(IReadOnlyList<string> names){
        var json=new StringBuilder("[");
        for(int i=0;i<names.Count;i++){
            if(i>0)json.Append(", ");
            json.Append('"');
            foreach(char c in names[i]){
                switch(c){
                    case '"':json.Append("\\\"");break;
                    case '\\':json.Append("\\\\");break;
                    case '\n':json.Append("\\n");break;
                    case '\r':json.Append("\\r");break;
                    case '\t':json.Append("\\t");break;
                    case '\b':json.Append("\\b");break;
                    case '\f':json.Append("\\f");break;
                    default:
                        if(c<' ')json.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));else json.Append(c);
                        break;
                }
            }
            json.Append('"');
        }
        json.Append(']');
        try{return new UTF8Encoding(false,true).GetBytes(json.ToString());}
        catch(EncoderFallbackException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"text：容器 {AssetPrepInput.TextContainer} 的 {TextArchive.DescFileName} 解出不成對的代理字元",AssetPrepInput.TextContainer,ex);}
    }

    // ---- dialogs/templates.json (text.md 5) ----

    /// <param name="Templates">Key and text in output order (first appearance in the index); conflict keys already removed.</param>
    /// <param name="Candidates">Index rows named *-c.html / *-c.htm.</param>
    /// <param name="DuplicateIdentical">Rows whose key was already taken with the same text.</param>
    /// <param name="InvalidEncoding">Names of the rows skipped because they are not valid code page 950 (one per row).</param>
    /// <param name="ConflictKeys">Keys removed because two rows had different text, in the order the conflicts appeared.</param>
    /// <param name="ConflictSkipped">Rows of a conflict key that came after the conflict.</param>
    public sealed record TemplateSet(IReadOnlyList<KeyValuePair<string,string>> Templates,int Candidates,int DuplicateIdentical,
        IReadOnlyList<string> InvalidEncoding,IReadOnlyList<string> ConflictKeys,int ConflictSkipped);

    public static bool IsTemplateName(string name)=>name.EndsWith("-c.html",StringComparison.OrdinalIgnoreCase)||name.EndsWith("-c.htm",StringComparison.OrdinalIgnoreCase);
    /// <summary>File name without its extension and without the last two characters ("-c").</summary>
    public static string TemplateKey(string name)=>name[..(name.LastIndexOf('.')-2)];

    /// <summary>
    /// Walks the index in order and takes the rows named *-c.html / *-c.htm (case-insensitive; a name with a space before
    /// the extension does not qualify). Text = strict code page 950, trailing NULs removed; an undecodable row is skipped.
    /// Keys compare case-insensitively and keep the spelling of their first row. A later row of the same key is ignored when
    /// its text is the same; when it differs, the key is removed for good (no row is preferred over another).
    /// </summary>
    public static TemplateSet BuildTemplates(IReadOnlyList<TextArchive.Entry> entries,Func<TextArchive.Entry,byte[]> read,CancellationToken cancel=default){
        var order=new List<string>();
        var texts=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var conflicts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conflictOrder=new List<string>();var invalid=new List<string>();
        int candidates=0,identical=0,skipped=0;
        foreach(var entry in entries){
            if(!IsTemplateName(entry.Name))continue;
            cancel.ThrowIfCancellationRequested();
            candidates++;
            if(DecodeCp950(read(entry)) is not {} decoded){invalid.Add(entry.Name);continue;}
            string text=decoded.TrimEnd('\0'),key=TemplateKey(entry.Name);
            if(conflicts.Contains(key)){skipped++;continue;}
            if(!texts.TryGetValue(key,out string? first)){texts.Add(key,text);order.Add(key);continue;}
            if(string.Equals(first,text,StringComparison.Ordinal)){identical++;continue;}
            texts.Remove(key);order.RemoveAll(k=>k.Equals(key,StringComparison.OrdinalIgnoreCase));
            conflicts.Add(key);conflictOrder.Add(key);
        }
        return new(order.Select(k=>new KeyValuePair<string,string>(k,texts[k])).ToList(),candidates,identical,invalid,conflictOrder,skipped);
    }

    /// <summary>
    /// The byte form of the baseline templates.json: one JSON object, no white space, ASCII only — what
    /// JsonSerializer.Serialize(Dictionary&lt;string,string&gt;) writes with its default encoder (the same encoder Utf8JsonWriter
    /// uses by default), in the given order.
    /// </summary>
    public static byte[] SerializeTemplates(IReadOnlyList<KeyValuePair<string,string>> templates){
        using var stream=new MemoryStream();
        using(var writer=new Utf8JsonWriter(stream)){
            writer.WriteStartObject();
            foreach(var (key,text) in templates)writer.WriteString(key,text);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}
