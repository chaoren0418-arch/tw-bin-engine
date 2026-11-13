using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace P06.AssetPrep;

// BCL-only startup gate, compiled into both product and launcher. Never creates or changes files.
public sealed record AssetPreparationStatus(string State,string Message,int Files=0,JsonElement? Receipt=null,string? ReceiptSha256=null) {
    public bool Ready=>State is "ready" or "legacy-ready";
}

public static class AssetPreparationState {
    public const string ClientRootHint="請用文字編輯器修改「啟動引擎.cmd」中的 set \"TW_CLIENT_ROOT=你的原客戶端完整路徑\"；直接呼叫 exe 時，請在呼叫端設定同名環境變數。";
    public static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    public static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();}
    static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}

    public static string RequireClientRoot(string? value){
        if(string.IsNullOrWhiteSpace(value)||!Path.IsPathFullyQualified(value)||!Directory.Exists(value))
            throw new InvalidDataException("TW_CLIENT_ROOT 未設定或不是存在的絕對目錄。"+ClientRootHint);
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        // An existing but unrelated directory must not reach normal startup and its development fallbacks.
        var containers=new[]{"Text","Tile","Sprite"}.Concat(Enumerable.Range(0,16).Select(i=>"Sprite"+i.ToString("D2")));
        foreach(string container in containers)
            foreach(string extension in new[]{".idx",".pak"})
                if(!File.Exists(Path.Combine(root,container+extension)))throw new InvalidDataException("TW_CLIENT_ROOT 缺少 "+container+extension+"（"+root+"）。"+ClientRootHint);
        if(!Directory.Exists(Path.Combine(root,"map")))throw new InvalidDataException("TW_CLIENT_ROOT 缺少 map 目錄（"+root+"）。"+ClientRootHint);
        return root;
    }

    public static string Resolve(string root,string relative){
        Require(!string.IsNullOrWhiteSpace(relative)&&!Path.IsPathRooted(relative)&&!relative.Contains('\\')&&!relative.Contains(':')&&relative.Split('/').All(p=>p.Length>0&&p is not "." and not ".."),"素材索引路徑無效。");
        string path=Path.GetFullPath(Path.Combine(root,relative));
        Require(path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"素材索引路徑超出素材目錄。");
        return path;
    }

    static void NoLinks(string path){
        for(string? p=path;p!=null;p=Path.GetDirectoryName(p))
            if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("新準備收據不可經過 junction 或符號連結："+p);
    }

    /// <param name="legacyIntegritySha256">Only a package-pinned v1 index may use the legacy development tree.</param>
    public static AssetPreparationStatus Inspect(string engineRoot,string? legacyIntegritySha256=null){
        string root=Path.Combine(engineRoot,"assets"),index=Path.Combine(root,"integrity.json"),receiptPath=Path.Combine(root,"prep-receipt.json");
        try{
            if(!File.Exists(index)){
                if(File.Exists(root))return new("receipt-mismatch","assets 位置是檔案，無法準備。");
                bool interrupted=Directory.Exists(Path.Combine(engineRoot,"runtime","assets-staging"))||Directory.Exists(root);
                return new(interrupted?"interrupted":"unprepared",(interrupted?"素材準備尚未完成；請重新執行啟動入口以續跑。":"尚未準備素材；請設定 TW_CLIENT_ROOT 後執行啟動入口。")+ClientRootHint);
            }
            using var document=JsonDocument.Parse(File.ReadAllBytes(index));var integrity=document.RootElement;
            string? schema=integrity.GetProperty("schema").GetString();
            bool legacy=schema=="p06-assets-integrity-v1";
            JsonElement? receipt=null;string? receiptHash=null;
            if(legacy){
                Require(legacyIntegritySha256!=null&&Hash(index)==legacyIntegritySha256,"舊素材索引未經產品 manifest 鎖定，或索引 SHA-256 不符。");
            }else{
                Require(schema=="tw-assets-integrity-v2","素材索引格式不符。");
                NoLinks(index);NoLinks(receiptPath);
                Require(File.Exists(receiptPath),"缺少素材準備收據 prep-receipt.json。");
                receiptHash=Hash(receiptPath);
                Require(receiptHash==integrity.GetProperty("receiptSha256").GetString(),"素材準備收據 SHA-256 不符。");
                using var doc=JsonDocument.Parse(File.ReadAllBytes(receiptPath));receipt=doc.RootElement.Clone();
                Require(receipt.Value.GetProperty("schema").GetString()=="tw-asset-prep-set-v1","素材準備收據格式不符。");
                string? state=receipt.Value.GetProperty("state").GetString();
                if(state=="partial")return new("interrupted","素材準備中斷（partial）；請重新執行啟動入口以續跑。",Receipt:receipt,ReceiptSha256:receiptHash);
                Require(state=="ready","素材準備收據不是 ready。");
            }
            int files=0;var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var pin in integrity.GetProperty("files").EnumerateArray()){
                string relative=pin.GetProperty("path").GetString()!;
                Require(paths.Add(relative)&&relative is not "integrity.json" and not "prep-receipt.json","素材索引有重複或保留路徑。");
                string path=Resolve(root,relative);if(!legacy)NoLinks(path);
                Require(File.Exists(path)&&new FileInfo(path).Length==pin.GetProperty("size").GetInt64(),"缺少或大小不符的素材："+relative);
                if(!legacy)Require(Hash(path)==pin.GetProperty("sha256").GetString(),"素材 SHA-256 不符："+relative);
                files++;
            }
            Require(files>0,"素材索引為空。");
            return new(legacy?"legacy-ready":"ready",legacy?"開發機舊素材索引檢查完成。":"本機素材已準備完成。",files,receipt,receiptHash);
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or NotSupportedException or FormatException){
            return new("receipt-mismatch","素材收據或輸出不符："+ex.Message);
        }
    }
}
