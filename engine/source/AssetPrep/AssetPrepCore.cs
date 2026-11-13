using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using P06.NativeUi;
namespace P06.AssetPrep;

// T451 (#1036, B15): local asset preparer core. Design: docs/design/portable-delivery.md「本機生成與啟動」.
// The preparer turns the user's own original client (an explicit, absolute TW_CLIENT_ROOT) into derived files under
// <engine>/assets/<recipe-id>. Nothing here carries game content: only algorithms, anchor hashes and path rules.
//
// Adding a category (auto-discovery, docs/design/m6-contract.md「自動發現」): in your own file,
//   [AssetRecipe] public sealed class XxxRecipe:IAssetRecipe{ … }      // public or internal parameterless constructor
// Do not edit Program.cs or any registry; AssetRecipeDiscovery.Product finds it.
// Not wired into start-up on purpose: there is no --prepare-assets entry and no "ready" state until every category is closed.

public enum AssetPrepFailure {
    /// <summary>Bad request: unknown recipe, empty or malformed item list, roots that overlap.</summary>
    InvalidRequest,
    /// <summary>TW_CLIENT_ROOT is not given, not absolute or not a directory.</summary>
    ClientRoot,
    /// <summary>A pinned container file (idx or pak) is not in TW_CLIENT_ROOT.</summary>
    MissingContainer,
    /// <summary>An anchor entry (list.spz, a pinned Text entry) is not the supported version: its decoded SHA-256 differs.</summary>
    ContainerMismatch,
    /// <summary>The container's structure is unusable (index size, entry out of range, duplicate name, truncated pak).</summary>
    CorruptContainer,
    /// <summary>The client has the data but the recipe's rules cannot produce this item (an action the item needs is not
    /// in list.spz, same-named alternates differ, …). Nothing is substituted.</summary>
    Unavailable,
    /// <summary>The entry a recipe needs is in none of its candidate containers.</summary>
    MissingEntry,
    /// <summary>The entry is there but cannot be decoded (truncated or malformed payload).</summary>
    CorruptEntry,
    /// <summary>Path traversal, or the target / one of its ancestors is a junction or symlink.</summary>
    UnsafePath,
    /// <summary>Another writer holds the recipe lock.</summary>
    Busy,
    /// <summary>The staged output does not match its receipt; nothing was published.</summary>
    Verification,
    /// <summary>Other I/O failure (disk full, access denied …).</summary>
    Io,
}

/// <summary>Every refusal of the preparer. <see cref="Container"/> names the idx/pak pair at fault when there is one.</summary>
public sealed class AssetPrepException(AssetPrepFailure failure,string message,string? container=null,Exception? inner=null):Exception(message,inner){
    public AssetPrepFailure Failure{get;}=failure;
    public string? Container{get;}=container;
}

public enum AssetRecipeCoverage {
    /// <summary>Only part of the category is closed: the receipt says "partial" and must never be treated as ready.</summary>
    Partial,
    /// <summary>The recipe produces the whole category.</summary>
    Complete,
}

/// <summary>Marks an <see cref="IAssetRecipe"/> implementation for automatic discovery.</summary>
[AttributeUsage(AttributeTargets.Class,Inherited=false)]
public sealed class AssetRecipeAttribute:Attribute{}

/// <summary>One asset category. Implementations hold no state between items and never touch the file system themselves.</summary>
public interface IAssetRecipe {
    /// <summary>Recipe id = directory name under assets/ and assets-staging/ (lower-case letters, digits, single dashes).</summary>
    string Id{get;}
    /// <summary>Bump when the output of an unchanged input changes; staged work of another version is discarded.</summary>
    int Version{get;}
    AssetRecipeCoverage Coverage{get;}
    /// <summary>Validates the requested items and returns them in canonical order without duplicates.
    /// Throws <see cref="AssetPrepException"/> (InvalidRequest) for anything it does not understand.</summary>
    IReadOnlyList<string> Normalize(IReadOnlyList<string> items);
    /// <summary>Produces one item: reads through the context (sources are recorded) and writes through the context.</summary>
    void Produce(string item,AssetRecipeItemContext context);
    /// <summary>True for an item whose output is derived from the request itself (a manifest of the requested items):
    /// a resumed run only reuses it when the earlier run had exactly the same request.</summary>
    bool DependsOnRequest(string item)=>false;
}

public static class AssetRecipeDiscovery {
    public sealed record Result(IReadOnlyList<IAssetRecipe> Recipes,IReadOnlyList<string> Errors);
    static readonly Lazy<Result> product=new(()=>Discover(typeof(AssetRecipeDiscovery).Assembly),LazyThreadSafetyMode.ExecutionAndPublication);
    /// <summary>Recipes of the product assembly. Invalid registrations are left out and listed in Errors (the asset-prep module test requires none).</summary>
    public static Result Product=>product.Value;

    public static Result Discover(Assembly assembly)=>Discover(assembly.GetTypes().Where(t=>!ExcludeFromDiscoveryAttribute.Excluded(t)));
    /// <summary>Order is by id (ordinal), independent of reflection order.</summary>
    public static Result Discover(IEnumerable<Type> types){
        var errors=new List<string>();var found=new List<IAssetRecipe>();
        foreach(var type in types.Where(t=>t.IsDefined(typeof(AssetRecipeAttribute),false)).OrderBy(t=>t.FullName,StringComparer.Ordinal)){
            string name=type.FullName??type.Name;
            if(type.IsAbstract||type.IsGenericTypeDefinition||!typeof(IAssetRecipe).IsAssignableFrom(type)){errors.Add($"{name}: [AssetRecipe] 只能標在實作 IAssetRecipe 的非抽象類別");continue;}
            var ctor=type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,Type.EmptyTypes);
            if(ctor==null){errors.Add($"{name}: 需要無參數建構式");continue;}
            IAssetRecipe recipe;string id;
            try{recipe=(IAssetRecipe)ctor.Invoke(null);id=recipe.Id;}
            catch(Exception ex){errors.Add($"{name}: {(ex is TargetInvocationException{InnerException:{} inner}?inner.Message:ex.Message)}");continue;}
            if(!AssetPrepPaths.IsRecipeId(id)){errors.Add($"{name}: recipe id \"{id}\" 不合法");continue;}
            if(recipe.Version<=0){errors.Add($"{name}: Version 必須大於 0");continue;}
            if(found.Any(r=>r.Id==id)){errors.Add($"{name}: recipe id \"{id}\" 重複註冊");continue;}
            found.Add(recipe);
        }
        return new(found.OrderBy(r=>r.Id,StringComparer.Ordinal).ToList(),errors);
    }
}

/// <summary>Fixed layout below the engine root. Staging, lock and the swap directory share one parent so the publish is a same-volume rename.</summary>
public sealed class AssetPrepLayout {
    public string EngineRoot{get;}
    public string StagingRoot{get;}
    public string ActiveRoot{get;}
    public AssetPrepLayout(string engineRoot){
        EngineRoot=AssetPrepPaths.RequireAbsolute(engineRoot,AssetPrepFailure.InvalidRequest,"引擎根目錄");
        StagingRoot=Path.Combine(EngineRoot,"runtime","assets-staging");
        ActiveRoot=Path.Combine(EngineRoot,"assets");
    }
    public string Staging(string recipeId)=>Path.Combine(StagingRoot,AssetPrepPaths.RequireRecipeId(recipeId));
    public string Active(string recipeId)=>Path.Combine(ActiveRoot,AssetPrepPaths.RequireRecipeId(recipeId));
    /// <summary>Where the replaced active directory waits during a publish ('.' cannot occur in a recipe id).</summary>
    public string Previous(string recipeId)=>Staging(recipeId)+".previous";
    public string Lock(string recipeId)=>Staging(recipeId)+".lock";
}

public static class AssetPrepPaths {
    // \z, not $: "$" would also accept a trailing newline.
    static readonly Regex recipeIdPattern=new(@"\A[a-z0-9]+(-[a-z0-9]+)*\z",RegexOptions.CultureInvariant);
    static readonly Regex segmentPattern=new(@"\A[A-Za-z0-9][A-Za-z0-9._-]*\z",RegexOptions.CultureInvariant);
    static Regex RecipeIdPattern()=>recipeIdPattern;
    static Regex SegmentPattern()=>segmentPattern;
    static readonly HashSet<string> devices=new(StringComparer.OrdinalIgnoreCase){"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"};

    public static string Sha256(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    public static string Sha256File(string path){
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    }

    public static bool IsRecipeId(string? id)=>id is {Length:>0 and <=64}&&RecipeIdPattern().IsMatch(id)&&!devices.Contains(id);
    public static string RequireRecipeId(string? id)=>IsRecipeId(id)?id!:throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"recipe id \"{id}\" 不合法（只允許小寫英數與單一連字號）");

    /// <summary>One request item: a short token of letters, digits, '.', '_' and '-' (never a path).</summary>
    public static bool IsItemToken(string? item)=>item is {Length:>0 and <=64}&&SegmentPattern().IsMatch(item)&&!item.Contains("..",StringComparison.Ordinal);

    public static string RequireAbsolute(string? path,AssetPrepFailure failure,string what){
        if(string.IsNullOrWhiteSpace(path))throw new AssetPrepException(failure,$"未指定{what}");
        if(path.IndexOf('\0')>=0||!Path.IsPathFullyQualified(path))throw new AssetPrepException(failure,$"{what}必須是絕對路徑：{path}");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>
    /// Output path of a recipe, relative to its own directory: segments of letters, digits, '.', '_' and '-' separated by
    /// '/' or '\'. Rooted paths, drive or stream colons, "." / "..", trailing dots and device names are refused.
    /// Returns the path with '/' separators.
    /// </summary>
    public static string SafeRelative(string? relative){
        AssetPrepException Bad(string why)=>new(AssetPrepFailure.UnsafePath,$"輸出路徑 \"{relative}\" 被拒絕：{why}");
        if(string.IsNullOrEmpty(relative)||relative.Length>240)throw Bad("空白或過長");
        var parts=relative.Split('/','\\');
        foreach(var part in parts){
            if(part.Length==0)throw Bad("絕對路徑或空白區段");
            if(part=="."||part.Contains("..",StringComparison.Ordinal))throw Bad("路徑穿越");
            if(!SegmentPattern().IsMatch(part)||part.EndsWith('.'))throw Bad("含不允許的字元");
            int dot=part.IndexOf('.');
            if(devices.Contains(dot<0?part:part[..dot]))throw Bad("保留裝置名稱");
        }
        return string.Join('/',parts);
    }

    /// <summary>baseDirectory + a <see cref="SafeRelative"/> path, re-checked to stay inside baseDirectory.</summary>
    public static string Combine(string baseDirectory,string relative){
        string safe=SafeRelative(relative),root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        string full=Path.GetFullPath(Path.Combine(root,safe.Replace('/',Path.DirectorySeparatorChar)));
        if(!IsWithin(full,root)||full.Length==root.Length)throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"輸出路徑 \"{relative}\" 離開了 {root}");
        return full;
    }

    /// <summary>True when path is directory or lies below it.</summary>
    public static bool IsWithin(string path,string directory){
        string p=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),d=Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        return p.Equals(d,comparison)||p.StartsWith(d+Path.DirectorySeparatorChar,comparison);
    }

    public static bool SameVolume(string a,string b)=>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(a)),Path.GetPathRoot(Path.GetFullPath(b)),StringComparison.OrdinalIgnoreCase);

    static bool IsReparse(string path,out bool exists){
        try{exists=true;return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;}
        catch(Exception ex) when(ex is FileNotFoundException or DirectoryNotFoundException){exists=false;return false;}
    }

    /// <summary>Refuses when the path or any existing ancestor (up to the volume root) is a junction or symlink.</summary>
    public static void RejectReparsePoints(string path,string what){
        for(string? p=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))
            if(IsReparse(p,out _))throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"{what}：{p} 是 junction／symlink，拒絕寫入");
    }

    /// <summary>Files below a directory as '/'-separated relative paths (ordinal order). A junction or symlink anywhere inside is refused, never followed.</summary>
    public static List<string> ListFiles(string directory){
        var result=new List<string>();
        void Walk(DirectoryInfo dir,string prefix){
            foreach(var entry in dir.EnumerateFileSystemInfos()){
                if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"{entry.FullName} 是 junction／symlink，拒絕處理");
                if(entry is DirectoryInfo sub)Walk(sub,prefix+entry.Name+"/");else result.Add(prefix+entry.Name);
            }
        }
        var root=new DirectoryInfo(directory);
        if(root.Exists){RejectReparsePoints(root.FullName,"目錄");Walk(root,"");}
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Deletes a directory tree the preparer owns. A junction or symlink inside stops the deletion (it is never followed and never removed).</summary>
    public static void DeleteTree(string directory){
        var root=new DirectoryInfo(directory);
        if(!root.Exists)return;
        RejectReparsePoints(root.FullName,"刪除目標");
        void Remove(DirectoryInfo dir){
            foreach(var entry in dir.EnumerateFileSystemInfos()){
                if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"{entry.FullName} 是 junction／symlink，拒絕刪除");
                if(entry is DirectoryInfo sub)Remove(sub);
                else{if((entry.Attributes&FileAttributes.ReadOnly)!=0)entry.Attributes=FileAttributes.Normal;entry.Delete();}
            }
            dir.Delete(false);
        }
        Remove(root);
    }

    /// <summary>Removes empty directories below (not including) the given one.</summary>
    public static void PruneEmptyDirectories(string directory){
        bool Prune(DirectoryInfo dir,bool removeSelf){
            bool empty=true;
            foreach(var entry in dir.EnumerateFileSystemInfos()){
                if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"{entry.FullName} 是 junction／symlink，拒絕處理");
                if(entry is not DirectoryInfo sub||!Prune(sub,true))empty=false;
            }
            if(empty&&removeSelf)dir.Delete(false);
            return empty&&removeSelf;
        }
        var root=new DirectoryInfo(directory);
        if(root.Exists)Prune(root,false);
    }
}
