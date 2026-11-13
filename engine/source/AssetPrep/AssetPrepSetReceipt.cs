using System.Text.Encodings.Web;
using System.Text.Json;
namespace P06.AssetPrep;

// T467: deterministic set metadata. No machine paths, clocks or free-space readings belong here.
public sealed record AssetSetRecipe(string Id,int Version,string Coverage,int Items,int OutputFiles,long OutputBytes,string ReceiptSha256);
public sealed record AssetSetProvider(string Category,string Component,string Verification);
public sealed record AssetSetLimit(string Id,string Description,int? Count,IReadOnlyList<string> Items);
public sealed record AssetSetAnchor(string Container,string Entry,string Sha256);
public sealed record AssetSetReceipt(string Schema,string ToolVersion,string State,
    IReadOnlyList<AssetContainerRecord> Inputs,IReadOnlyList<AssetSetAnchor> Anchors,IReadOnlyList<AssetSetRecipe> Recipes,
    IReadOnlyList<AssetSetProvider> Providers,IReadOnlyList<AssetSetLimit> KnownLimits,string PreheatSha256) {
    public const string SchemaId="tw-asset-prep-set-v1",FileName="prep-receipt.json";
    internal static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,NewLine="\n",Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    public byte[] ToJson()=>JsonSerializer.SerializeToUtf8Bytes(this,JsonOptions);

    internal static IReadOnlyList<AssetSetProvider> DirectProviders()=>[
        new("cache/map; cache/tiles; maps 原檔; TIL","MapRawSource / OriginalMapCache","manifest SHA-256；SEG 程式 pin"),
        new("LAC","ShipBackdrop / MapRawSource","程式 pin"),
        new("dialogs/skills; magic/catalog","ClientSpellTable","Text entry 程式 pin；等級算式"),
        new("ground","GroundSpriteReader","結構檢查、entry pin"),
        new("inventory","ItemIconSource / WebIconGenerator","容器結構檢查、解碼驗證；不驗整包 SHA"),
        new("motion","MotionRules","產品規則，不驗外部檔案"),
        new("cache 混色表","Renderer","整數算式，不驗外部檔案"),
        new("sound; audio-manifest","ProductSound / AudioAssets","Sound 解碼檢查；6016 WAV 程式 pin，MP3 不可用時記錄並退回 WAV"),
        new("EXP","OriginalExpTable","內建已知例外 L1"),
        new("ground-catalog","GroundCatalog","本機條件與伺服器裁定，不驗外部檔案")
    ];

    internal static IReadOnlyList<AssetSetLimit> Limits(IReadOnlyList<AssetPrepResult> results){
        AssetReceipt Receipt(string id)=>results.Single(r=>r.Receipt.RecipeId==id).Receipt;
        string[] List(string id,string key)=>Receipt(id).Completed.SelectMany(c=>c.Metadata.TryGetValue(key,out var v)?v.Split(',',StringSplitOptions.RemoveEmptyEntries):[])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] Unavailable(string id)=>Receipt(id).Completed.Where(c=>!c.Item.Equals("manifest",StringComparison.Ordinal)&&c.Metadata.ContainsKey("unavailable"))
            .Select(c=>c.Item).Order(StringComparer.Ordinal).ToArray();
        var zones=List("terrain","zoneLimitMaps");
        var players=Unavailable("players");
        int effects=Receipt("map-effects").Completed.Sum(c=>c.Metadata.TryGetValue("unavailable",out var n)&&int.TryParse(n,out int value)?value:0);
        var unsupported=List("terrain","unsupportedMaps");
        return [
            new("L1","EXP 百分比沿用內建門檻表；伺服器門檻不同時不準",1,["OriginalExpTable"]),
            new("L2","移除可丟棄白名單，保留確認視窗，是否可丟由伺服器裁定",0,[]),
            new("L3","已產生地圖中，區域位元與基線有已知差異者；不影響產品讀取的通行位元",zones.Length,zones),
            new("L4","地圖顯示名稱沒有客戶端來源；manifest 不含 name",null,[]),
            new("L5","伺服器動作間隔採 R1 算式；基線以外姿勢仍待實機驗證",null,[]),
            new("L6","地圖特效 unavailable 保留原原因；詳見 maps/effects/manifest.json",effects,[]),
            new("L7","玩家預熱姿勢 unavailable；不放寬 gfx 138 的 bounds 規則",players.Length,players),
            new("D3","無法解析的地圖不產生 terrain，明列不支援",unsupported.Length,unsupported)
        ];
    }
}

public sealed record AssetIntegrityFile(string Path,long Size,string Sha256);
public sealed record AssetSetIntegrity(string Schema,string ReceiptSha256,IReadOnlyList<AssetIntegrityFile> Files) {
    public const string SchemaId="tw-assets-integrity-v2",FileName="integrity.json";
    public byte[] ToJson()=>JsonSerializer.SerializeToUtf8Bytes(this,AssetSetReceipt.JsonOptions);

    /// <summary>Runtime gate for v2; maps are still checked lazily by their existing readers.</summary>
    public static void VerifyReceipt(string root,JsonElement integrity)=>VerifyReceipt(root,integrity,false);
    static void VerifyReceipt(string root,JsonElement integrity,bool allowPartial){
        byte[] bytes=File.ReadAllBytes(Path.Combine(root,AssetSetReceipt.FileName));
        if(AssetPrepPaths.Sha256(bytes)!=integrity.GetProperty("receiptSha256").GetString())throw new InvalidDataException("Asset preparation receipt SHA-256 mismatch");
        using var receipt=JsonDocument.Parse(bytes);
        string? state=receipt.RootElement.GetProperty("state").GetString();
        if(receipt.RootElement.GetProperty("schema").GetString()!=AssetSetReceipt.SchemaId||!(state=="ready"||allowPartial&&state=="partial"))
            throw new InvalidDataException("Asset preparation is not ready");
    }

    /// <summary>Publisher gate: exact file set and EVERY hash, including maps and the per-recipe receipts.</summary>
    internal static void VerifyTree(string root,byte[]? receiptBytes=null,byte[]? integrityBytes=null,bool allowPartial=false){
        AssetPrepPaths.RejectReparsePoints(root,"完整資產樹");
        byte[] actual=File.ReadAllBytes(Path.Combine(root,FileName));
        if(integrityBytes!=null&&!actual.AsSpan().SequenceEqual(integrityBytes))throw new InvalidDataException("integrity 讀回不符");
        byte[] receipt=File.ReadAllBytes(Path.Combine(root,AssetSetReceipt.FileName));
        if(receiptBytes!=null&&!receipt.AsSpan().SequenceEqual(receiptBytes))throw new InvalidDataException("總收據讀回不符");
        using var doc=JsonDocument.Parse(actual);var j=doc.RootElement;
        if(j.GetProperty("schema").GetString()!=SchemaId)throw new InvalidDataException("不是 integrity v2");
        VerifyReceipt(root,j,allowPartial);
        var files=j.GetProperty("files").EnumerateArray().ToArray();
        var expected=new HashSet<string>(StringComparer.OrdinalIgnoreCase){FileName,AssetSetReceipt.FileName};
        string? last=null;
        foreach(var f in files){
            string relative=f.GetProperty("path").GetString()!;
            if(AssetPrepPaths.SafeRelative(relative)!=relative||!expected.Add(relative)||last!=null&&StringComparer.Ordinal.Compare(last,relative)>=0)
                throw new InvalidDataException("integrity 路徑重複或排序錯誤");
            last=relative;
            string path=AssetPrepPaths.Combine(root,relative);AssetPrepPaths.RejectReparsePoints(path,"驗證輸出");
            if(!File.Exists(path)||new FileInfo(path).Length!=f.GetProperty("size").GetInt64()||AssetPrepPaths.Sha256File(path)!=f.GetProperty("sha256").GetString())
                throw new InvalidDataException("輸出驗證失敗："+relative);
        }
        if(!expected.SetEquals(AssetPrepPaths.ListFiles(root)))throw new InvalidDataException("完整樹有未登記或缺少的檔案");
    }
}
