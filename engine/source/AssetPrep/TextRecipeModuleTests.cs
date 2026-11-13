using System.Text;
using System.Text.Json;
namespace P06.AssetPrep;

// T453 (#1038): in-memory checks of the "text" recipe's rules, run by --module-tests (name asset-prep-text). Every archive
// here is synthetic (TextArchive.FromBytes); the file-system side (publish, resume, refusals, the product readers) is in
// engine/_integration/dev/asset-prep-tests/TextRecipeChecks.cs.
static class TextRecipeModuleTests {
    static readonly Encoding big5=CodePagesEncodingProvider.Instance.GetEncoding(950)!;
    static TextArchive Archive(params (string Name,byte[] Data)[] items){
        using var pak=new MemoryStream();using var rows=new MemoryStream();
        foreach(var (name,data) in items){
            var row=new byte[28];
            BitConverter.GetBytes((uint)pak.Position).CopyTo(row,0);Encoding.ASCII.GetBytes(name).CopyTo(row,4);BitConverter.GetBytes((uint)data.Length).CopyTo(row,24);
            rows.Write(row);pak.Write(TextArchive.Encode(data));
        }
        return TextArchive.FromBytes([..BitConverter.GetBytes((uint)items.Length),..TextArchive.Encode(rows.ToArray())],pak.ToArray());
    }
    static bool Refused(Action action,AssetPrepFailure failure){
        try{action();return false;}catch(AssetPrepException ex){return ex.Failure==failure;}
    }

    [WorldModuleTest("asset-prep-text")]
    static void Run(WorldModuleTestContext t){
        // --- Registration and items.
        var product=AssetRecipeDiscovery.Product;
        t.Check(product.Recipes.Count(r=>r.Id==TextRecipe.RecipeId)==1&&product.Recipes.Single(r=>r.Id==TextRecipe.RecipeId) is TextRecipe{Coverage:AssetRecipeCoverage.Complete,Version:>0},"text recipe discovered once");
        var recipe=new TextRecipe();
        t.Check(recipe.Normalize(new[]{"templates","names","templates"}).SequenceEqual(TextRecipe.AllItems),"text items in canonical order without duplicates");
        t.Check(recipe.Normalize(new[]{"templates"}).SequenceEqual(new[]{"templates"}),"a single text item is accepted");
        foreach(string item in new[]{"skills","Names","names.json","magic","desc-c.tbl"})
            t.Check(Refused(()=>recipe.Normalize(new[]{item}),AssetPrepFailure.InvalidRequest),"text item refused: "+item);
        t.Throws<ArgumentException>(()=>new TextRecipe("not-a-hash"),"anchor must be a SHA-256");
        t.Equal("objects/names.json",AssetPrepPaths.SafeRelative(TextRecipe.NamesPath),"names path is tree-relative");
        t.Equal("dialogs/templates.json",AssetPrepPaths.SafeRelative(TextRecipe.TemplatesPath),"templates path is tree-relative");

        // --- names: first line dropped, nothing trimmed, empty lines kept, no trailing CR LF needed.
        var (declared,names)=TextRecipe.ParseNames(big5.GetBytes("7\r\n劍\r\n\r\n a b \r\nx\ny\r\n"));
        t.Equal("7",declared,"declared count is the first line as written");
        t.Check(names.SequenceEqual(new[]{"劍",""," a b ","x\ny",""}),"names keep empty lines, spaces and a lone LF",names);
        t.Check(TextRecipe.ParseNames("0"u8).Names.Count==0,"a table with only the count line has no names");
        t.Check(Refused(()=>TextRecipe.ParseNames("alpha\r\nbeta"u8),AssetPrepFailure.CorruptEntry),"names without a count line refused");
        t.Check(Refused(()=>TextRecipe.ParseNames(""u8),AssetPrepFailure.CorruptEntry),"empty names table refused");
        t.Check(Refused(()=>TextRecipe.ParseNames(new byte[]{(byte)'1',13,10,0xA4}),AssetPrepFailure.CorruptEntry),"names with a cut double-byte character refused");
        // Byte form: ", " separator, non-ASCII as is, only quote, backslash and control characters escaped.
        string json=Encoding.UTF8.GetString(TextRecipe.SerializeNames(new[]{"劍","","a\"b\\c","t\tn\nr\rb\bf\f","\u0001\u001f\u007f","<&'+>"}));
        t.Equal("[\"劍\", \"\", \"a\\\"b\\\\c\", \"t\\tn\\nr\\rb\\bf\\f\", \"\\u0001\\u001f\u007f\", \"<&'+>\"]",json,"names.json byte form");
        t.Equal("[]",Encoding.UTF8.GetString(TextRecipe.SerializeNames([])),"empty names.json");
        t.Check(TextRecipe.SerializeNames(new[]{"劍"}) is [(byte)'[',(byte)'"',0xE5,0x8A,0x8D,(byte)'"',(byte)']'],"names.json is UTF-8 without BOM or line break");
        t.Check(JsonSerializer.Deserialize<string[]>(json)!.SequenceEqual(new[]{"劍","","a\"b\\c","t\tn\nr\rb\bf\f","\u0001\u001f\u007f","<&'+>"}),"names.json parses back to the same strings");

        // --- templates: name rule and key.
        foreach(string name in new[]{"a-c.html","a-c.htm","A-C.HTML","x.y-c.HTM","-c.html"})t.Check(TextRecipe.IsTemplateName(name),"template name: "+name);
        foreach(string name in new[]{"a-c .html","a.html","a-c.tbl","a-c.html.bak","a-e.html","a-c.xhtml","ac.htm"})t.Check(!TextRecipe.IsTemplateName(name),"not a template name: "+name);
        t.Check(TextRecipe.TemplateKey("Shop1-c.html")=="Shop1"&&TextRecipe.TemplateKey("x.y-C.HTM")=="x.y"&&TextRecipe.TemplateKey("-c.htm")=="","template key drops \"-c\" and the extension");

        // --- templates: index order, same-name rows, undecodable rows.
        byte[] Html(string text)=>big5.GetBytes(text);
        var archive=Archive(
            ("zeta-c.html",Html("<p>z</p>\0\0\0")),
            ("desc-c.tbl",Html("1\r\nname")),
            ("Alpha-c.htm",Html("<a action=\"buy\">買 & 'q' +</a>")),
            ("ALPHA-C.HTML",Html("<a action=\"buy\">買 & 'q' +</a>\0")),          // same key, same text after the NULs are dropped
            ("dup-c.html",Html("one")),
            ("mid-c.html",Html("m")),
            ("DUP-c.html",Html("two")),                                           // conflict: the key goes away
            ("dup-c.html",Html("one")),                                           // … and stays away
            ("bad-c.html",[(byte)'<',0xA4,0x20,(byte)'x',0xA4]),                  // not code page 950
            ("note-c .html",Html("skipped by name")),
            ("dup-c.htm",Html("three")),
            ("mid-c.html",Html("m")),
            ("list.spr",Html("#0")));
        int reads=0;
        var set=TextRecipe.BuildTemplates(archive.Entries,e=>{reads++;return archive.Read(e);});
        t.Check(set.Templates.Select(p=>p.Key).SequenceEqual(new[]{"zeta","Alpha","mid"}),"keys in first-appearance order, first spelling kept, conflict key removed",set.Templates.Select(p=>p.Key).ToArray());
        t.Check(set.Templates[0].Value=="<p>z</p>"&&set.Templates[1].Value=="<a action=\"buy\">買 & 'q' +</a>","template text decoded, trailing NULs dropped");
        t.Check(set is {Candidates:10,DuplicateIdentical:2,ConflictSkipped:2}&&set.ConflictKeys.SequenceEqual(new[]{"DUP"})&&set.InvalidEncoding.SequenceEqual(new[]{"bad-c.html"}),"template classification counts",
            new{set.Candidates,set.DuplicateIdentical,set.ConflictSkipped,set.ConflictKeys,set.InvalidEncoding});
        t.Equal(10,reads,"only *-c.html / *-c.htm rows are read");
        // Byte form: what JsonSerializer writes for a Dictionary<string,string>, in index order.
        byte[] bytes=TextRecipe.SerializeTemplates(set.Templates);
        var reference=new Dictionary<string,string>();foreach(var (key,text) in set.Templates)reference.Add(key,text);
        t.Check(bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(reference)),"templates.json equals JsonSerializer's default output");
        string templatesJson=Encoding.ASCII.GetString(bytes);
        t.Check(bytes.All(b=>b is >=0x20 and <0x7F)&&templatesJson.StartsWith("{\"zeta\":\"\\u003Cp\\u003Ez\\u003C/p\\u003E\",\"Alpha\":",StringComparison.Ordinal)
            &&templatesJson.Contains("\\u8CB7 \\u0026 \\u0027q\\u0027 \\u002B",StringComparison.Ordinal)&&templatesJson.Contains("action=\\u0022buy\\u0022",StringComparison.Ordinal),"templates.json is ASCII only with the default escapes",templatesJson);
        t.Equal("{}",Encoding.ASCII.GetString(TextRecipe.SerializeTemplates([])),"empty templates.json");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        t.Throws<OperationCanceledException>(()=>TextRecipe.BuildTemplates(archive.Entries,archive.Read,cancelled.Token),"template walk honours cancellation");
    }
}
