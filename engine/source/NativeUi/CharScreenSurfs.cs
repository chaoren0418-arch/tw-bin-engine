using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06.NativeUi;

// #1242 (#1211 SM07/SM08): surf lookup of the native select / create screens (SelectCharPanel, CreateCharPanel) only.
//
// Root cause (已驗證, PakViewer CLI lists + scratch/i1242/surf-compare.tsv, docs/reports/issue-1242.md):
// - The surf loader formats "%d.img" (sub_E55DD0, VA 0x11DFA2C) and HashFileManager tries "<id>c.img" before "<id>.img"
//   (SurfCatalog.Candidates). XmlImage (vftable VA 0x11A0D64) keeps the id of "id;N" (sub_B0F460: this[72] = id,
//   this[70] = N); N does not pick another file (sub_B04440 sizes from this[72] only).
// - 0-客戶端 ships "<id>.img" for every id 6400–6529, all 60×80 class portraits (an older surf set), and no "<id>c.img".
//   SurfCatalog.Get therefore always found the local portrait, and the T322 reference fallback (kor25) was never asked:
//   the select window's buttons, slot cards, info back and the create screen's Prev/Next/sex/stat buttons all became
//   60×80 red / blue portraits (m7-SM07-1.png, m7-SM08-class-0.png).
// - kor25 has the real art only as "<id>c.img" (141 ids); its own "<id>.img" for 6400–6403, 6411–6414, 6421–6425, 6429,
//   6487, 6488, 6495–6507, 6509 are the same kind of 60×80 portrait. The 雷雨天堂 reference client (ly) has the same 141
//   "<id>c.img" byte for byte (SHA-256 equal) and real pictures for those 30 plain ids (6421 = 800×600 select background,
//   6429 / 6422–6425 = 337×110 slot backs, 6411–6414 = 58×50 page buttons, 6487 / 6488 = 459×295 message back, …).
//
// Order (per id, first hit wins; decoded surfaces cached here, nothing written anywhere):
//   1. The art client (TW_CHAR_ART_CLIENT → native-ui.json "charArtClient" → ly default; false / "" = off), HashFileManager
//      order "<id>c.img" then "<id>.img" (RefClientSurfFallback.TryRead, idx / payload SHA-256 logged once per id).
//   2. SurfCatalog.Get (local, then the T322 reference fallback) — but a plain "<id>.img" for an id in 6400–6529 is the
//      legacy portrait set (0-客戶端 and kor25) and is rejected, then
//   3. the reference client's "<id>c.img" / "<id>.img" (SurfCatalog.Reference, kor25), with the same rejection.
// Rejected / missing → null: the element is not drawn (no substitute picture). Other screens (login …) keep SurfCatalog.Get.

/// <summary>One payload of an image archive lookup: entry name and raw .img bytes.</summary>
public readonly record struct CharArtPayload(string Archive,string Name,byte[] Data);

public sealed class CharScreenSurfs {
    public const string EnvironmentVariable="TW_CHAR_ART_CLIENT";
    /// <summary>Key in &lt;stage&gt;/runtime/native-ui.json: a path string, or false / "" to turn the art client off.</summary>
    public const string SettingsKey="charArtClient";
    /// <summary>#1242: 雷雨天堂, the reference client whose 64xx/65xx images match SelectUI / CreateUI.</summary>
    public const string DefaultRoot=@"D:\天堂參考用客戶單\雷雨天堂";
    /// <summary>Ids whose plain "&lt;id&gt;.img" in 0-客戶端 and kor25 is a 60×80 legacy class portrait.</summary>
    public const int LegacyFirst=6400,LegacyLast=6529;

    readonly Func<int,CharArtPayload?>? art;
    readonly Func<int,UiSurface?> catalog;
    readonly Func<int,CharArtPayload?>? reference;
    readonly object gate=new();
    readonly Dictionary<int,UiSurface?> cache=[];
    /// <summary>Where each surf came from ("art:Sprite08/6421.img", "local:…", "ref:…") or why it is missing.</summary>
    public IReadOnlyDictionary<int,string> Origins{get{lock(gate)return new Dictionary<int,string>(origins);}}
    readonly Dictionary<int,string> origins=[];
    public string? ArtRoot{get;}

    /// <param name="art">Art client lookup by id (c then plain); null = no art client.</param>
    /// <param name="catalog">SurfCatalog.Get (or a synthetic one in the offline checks).</param>
    /// <param name="reference">Reference client lookup by id (c then plain); null = none.</param>
    internal CharScreenSurfs(Func<int,CharArtPayload?>? art,Func<int,UiSurface?> catalog,Func<int,CharArtPayload?>? reference,string? artRoot=null){
        this.art=art;this.catalog=catalog;this.reference=reference;ArtRoot=artRoot;
    }

    /// <summary>A plain "&lt;id&gt;.img" of the legacy portrait range (never used by the character screens).</summary>
    public static bool Legacy(int id,string fileName)=>id is >=LegacyFirst and <=LegacyLast
        &&string.Equals(fileName,id.ToString(System.Globalization.CultureInfo.InvariantCulture)+SurfCatalog.SurfExtension,StringComparison.OrdinalIgnoreCase);

    public UiSurface? Get(int id){
        lock(gate){
            if(cache.TryGetValue(id,out var hit))return hit;
            string? why=null;
            UiSurface? s=Decode(id,art?.Invoke(id),"art",ref why);
            if(s==null){
                var local=catalog(id);
                if(local!=null&&!Legacy(id,local.FileName)){s=local;origins[id]=$"local:{local.Archive}/{local.FileName}";}
                else{
                    if(local!=null)why=$"{local.Archive}/{local.FileName} 是舊版 60×80 職業頭像，不用";
                    s=Decode(id,reference?.Invoke(id),"ref",ref why);
                }
            }
            if(s==null){
                origins[id]="missing: "+(why??"美術客戶端、本機與參考客戶端都沒有");
                UiResourceLog.Write($"char-art surf {id} 不畫：{origins[id]}");
            }
            cache[id]=s;return s;
        }
    }

    UiSurface? Decode(int id,CharArtPayload? found,string tag,ref string? why){
        if(found is not {} f)return null;
        if(tag!="art"&&Legacy(id,f.Name)){why=$"{tag}:{f.Archive}/{f.Name} 也是舊版 60×80 職業頭像，不用";return null;}
        try{
            var s=new UiSurface(id,f.Name,tag+":"+f.Archive,UiImage.Decode(f.Data,$"{tag}:{f.Archive}/{f.Name}"));
            origins[id]=$"{tag}:{f.Archive}/{f.Name}";
            return s;
        }catch(UiResourceException ex){why=ex.Message;UiResourceLog.Write($"char-art surf {id} {tag}:{f.Archive}/{f.Name} 無法解碼：{ex.Message}");return null;}
    }

    // ---- product wiring -----------------------------------------------------------------------------------------------

    static readonly ConditionalWeakTable<SurfCatalog,CharScreenSurfs> shared=new();

    /// <summary>The lookup shared by both character screens for one catalog (art client resolved once).</summary>
    public static CharScreenSurfs For(UiResources? resources,SurfCatalog catalog)=>shared.GetValue(catalog,c=>Create(resources?.Root,c));

    static CharScreenSurfs Create(string? mainRoot,SurfCatalog catalog){
        var artSource=ConfiguredArt(mainRoot);
        Func<int,CharArtPayload?>? art=artSource==null?null:id=>Payload(artSource.TryRead(Id(id)));
        Func<int,CharArtPayload?>? reference=catalog.Reference is {} r?id=>Payload(r.TryRead(Id(id))):null;
        return new(art,catalog.Get,reference,artSource?.Root);
    }
    static string Id(int id)=>id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    static CharArtPayload? Payload(RefClientSurf? s){
        if(s==null)return null;
        UiResourceLog.Write($"char-art {s.Root}\\{s.Archive}.pak:{s.Name} sha256 {s.BlobSha256} (idx {s.IndexSha256})");
        return new CharArtPayload(s.Archive,s.Name,s.Data);
    }

    /// <summary>Precedence: TW_CHAR_ART_CLIENT (non-empty) → native-ui.json "charArtClient" (string; false or "" = off) → ly default.</summary>
    public static (string? Root,string Source) ResolveRoot(string? environment,string? settingsPath){
        if(!string.IsNullOrWhiteSpace(environment))return (environment.Trim(),"env");
        if(settingsPath!=null&&File.Exists(settingsPath)){
            try{
                using var doc=JsonDocument.Parse(File.ReadAllText(settingsPath));
                if(doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty(SettingsKey,out var v)){
                    if(v.ValueKind==JsonValueKind.False)return (null,"settings");
                    if(v.ValueKind==JsonValueKind.String)return string.IsNullOrWhiteSpace(v.GetString())?(null,"settings"):(v.GetString()!.Trim(),"settings");
                    UiResourceLog.Write($"{UiOptions.SettingsFileName} 的 {SettingsKey} 不是字串或 false，忽略");
                }
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){UiResourceLog.Write($"{UiOptions.SettingsFileName} 無法讀取，選角美術客戶端用預設：{ex.Message}");}
        }
        return (DefaultRoot,"default");
    }

    static RefClientSurfFallback? ConfiguredArt(string? mainRoot){
        var (root,source)=ResolveRoot(Environment.GetEnvironmentVariable(EnvironmentVariable),RefClientXmlSource.DefaultSettingsPath);
        if(root==null){UiResourceLog.Write("char-art off");return null;}
        if(!Path.IsPathFullyQualified(root)){UiResourceLog.Write($"char-art 路徑不是絕對路徑，忽略：{root}");return null;}
        var full=Path.GetFullPath(root);
        if(mainRoot!=null&&string.Equals(Path.TrimEndingDirectorySeparator(full),Path.TrimEndingDirectorySeparator(Path.GetFullPath(mainRoot)),StringComparison.OrdinalIgnoreCase)){
            UiResourceLog.Write("char-art 與主客戶端是同一資料夾，忽略");return null;
        }
        if(!Directory.Exists(full)){UiResourceLog.Write($"char-art {full} 不存在，選角／建角畫面只用本機與參考客戶端（舊版頭像不畫）");return null;}
        UiResourceLog.Write($"char-art {full} ({source})");
        return new RefClientSurfFallback(full,source);
    }
}
