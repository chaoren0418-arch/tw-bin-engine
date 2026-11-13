using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06.AssetPrep;

// T455 (#1040, B15): recipes "players", "shops", "doors", "appearance-lists" and "shadows" on the T454 SPR layer.
// Contract: docs/design/asset-recipe.md 1.3, 3.2, 4.6, 5, 6, 9 (T455 row); rules: docs/reference/asset-recipes/sprites.md
// §4.2, §4.4–§4.6, §6; times: action-timing.md R1–R3 (serverActionMs = 40 × ticks; no blocked field).
//
//   order 1  shops             "<gfx>"            → shops/<gfx>.json             action 70, floor length
//                              "manifest"         → shops/manifest.json          p24-original-shop-appearance-v1
//   order 1  doors             "<gfx>"            → doors/<gfx>.json             actions 28 open / 29 close, non-directional, stepMs 167
//                              "manifest"         → doors/manifest.json          p24-original-inn-door-v1
//   order 2  players           "<gfx>-<weapon>"   → players/<gfx>-<weapon>.json  seven actions; (0, 0/4/20/50) point at objects/0*.json
//            (needs objects)   "manifest"         → players/manifest.json        p24-player-profiles-v1 (no className)
//   order 2  appearance-lists  "monster-<gfx>"    one row of encountered-monsters.json, read from objects/<gfx>.json
//            (needs objects)   "world-<gfx>"      one row of world-appearances.json, objects/world-<gfx>.json if objects made it, else objects/<gfx>.json
//                              "encountered-monsters" → encountered-monsters.json  p24-original-encountered-monsters-v1
//                              "world-appearances"    → world-appearances.json     p24-original-world-appearances-v1
//   order 3  shadows           "<gfx>"            → shadows/<gfx>.json           101.shadow sprite, frames = keys the body sequences use
//            (needs objects, players) "manifest"  → shadows/manifest.json        p24-original-shadows-v1
//
// ground is not a recipe (asset-recipe.md 3.2 / 6): the product draws ground items straight from the client through
// GroundSpriteReader (GroundRendering.cs, WebPanel.cs); nothing reads assets/ground/*.
//
// Every manifest is built from what this run actually prepared (the finished items' receipt records), never copied from a
// list. An item whose rules cannot be met (Unavailable) writes no file and becomes an "unavailable" row of its manifest,
// as the baseline does (L7); any other failure (missing or corrupt SPR, wrong order) stops the run with nothing published.
// Policy strings and className of the baseline manifests are not produced (provenance only, asset-recipe.md 1.3 / 3.2).

/// <summary>Helpers shared by the T455 recipes: item numbers, manifest rows kept in the receipt, reading earlier outputs.</summary>
public static class SprManifest {
    /// <summary>Receipt metadata: the item's manifest row (JSON object), or the row of an unavailable item.</summary>
    public const string Row="manifestRow",UnavailableRow="unavailableRow";
    public const string ManifestItem="manifest";

    static readonly Regex number=new(@"\A(?:0|[1-9][0-9]{0,4})\z",RegexOptions.CultureInvariant);
    public static int? Number(string text)=>number.IsMatch(text)&&int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out int v)&&v<=ushort.MaxValue?v:null;
    public static string Text(int value)=>value.ToString(CultureInfo.InvariantCulture);

    public static string Json(Action<Utf8JsonWriter> body)=>Encoding.UTF8.GetString(SprJson.Write(body));

    /// <summary>Runs <paramref name="produce"/>; an Unavailable refusal becomes the item's unavailable row instead of stopping the run.</summary>
    public static void OrUnavailable(AssetRecipeItemContext context,Action produce,Func<string,string> row){
        try{produce();}
        catch(AssetPrepException ex) when(ex.Failure==AssetPrepFailure.Unavailable){
            context.Set(UnavailableRow,row(ex.Message));
            context.Set("unavailable",ex.Message);
        }
    }

    /// <summary>The rows of the finished items this run requested (canonical order), split into available and unavailable.</summary>
    public static (List<string> Rows,List<string> Unavailable) Collect(AssetRecipeItemContext context,Func<string,bool> member){
        var rows=new List<string>();var unavailable=new List<string>();
        foreach(string item in context.Requested.Where(member)){
            var done=context.Completed(item)??throw new InvalidOperationException($"{item} 應在 manifest 之前完成（Normalize 的順序錯誤）");
            if(done.Metadata.TryGetValue(Row,out string? r))rows.Add(r);
            else if(done.Metadata.TryGetValue(UnavailableRow,out string? u))unavailable.Add(u);
            else throw new InvalidOperationException($"{item} 沒有 manifest 列");
        }
        return (rows,unavailable);
    }
    public static void Rows(Utf8JsonWriter w,string name,IEnumerable<string> rows){
        w.WriteStartArray(name);foreach(string r in rows)w.WriteRawValue(r);w.WriteEndArray();
    }

    /// <summary>The frames, sprite and action rows of an appearance file an earlier recipe published.</summary>
    public sealed record Appearance(int Gfx,int? Weapon,int Sprite,int StepMs,string? EntryHash,int Frames,IReadOnlyList<(string Action,int Direction,string[] Keys)> Sequences){
        public IEnumerable<string> Keys=>Sequences.SelectMany(s=>s.Keys);
        public bool Supports(string action)=>Enumerable.Range(0,8).All(d=>Sequences.Any(s=>s.Action==action&&s.Direction==d));
    }
    public static Appearance ReadAppearance(byte[] data,string what){
        try{
            using var doc=JsonDocument.Parse(data);var j=doc.RootElement;
            int? weapon=j.TryGetProperty("weapon",out var w)?w.GetInt32():null;
            string? hash=j.TryGetProperty("entryHash",out var h)?h.GetString():null;
            var sequences=j.GetProperty("sequences").EnumerateArray().Select(s=>(s.GetProperty("action").GetString()!,s.GetProperty("direction").GetInt32(),
                s.GetProperty("sequence").EnumerateArray().Select(t=>t.GetProperty("key").GetString()!).ToArray())).ToList();
            return new(j.GetProperty("gfx").GetInt32(),weapon,j.GetProperty("sprite").GetInt32(),j.GetProperty("stepMs").GetInt32(),hash,j.GetProperty("frames").GetArrayLength(),sequences);
        }catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){
            throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"{what} 不是可讀的外觀檔（{ex.Message}）；要重新準備它的 recipe");
        }
    }

    /// <summary>"&lt;sid&gt;:&lt;frame&gt;" → (sid, frame); null when it is not that form.</summary>
    public static (int Sid,int Frame)? ParseKey(string key){
        int colon=key.IndexOf(':');
        return colon>0&&Number(key[..colon]) is int sid&&Number(key[(colon+1)..]) is int frame?(sid,frame):null;
    }
}

[AssetRecipe]
public sealed class PlayersRecipe:IAssetRecipe {
    public const string RecipeId="players",ManifestPath="players/manifest.json",Schema="p24-player-profiles-v1";
    /// <summary>Stances of the player body gfx 0 that objects already publishes (objects/0.json, objects/0-weapon-&lt;n&gt;.json):
    /// their profile rows point there, marked inherited, instead of writing a second copy (sprites.md §4.2).</summary>
    public static readonly IReadOnlySet<int> InheritedWeapons=new HashSet<int>{0,4,20,50};
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public IReadOnlyList<string> Requires=>[ObjectsRecipe.RecipeId];
    public bool DependsOnRequest(string item)=>item==SprManifest.ManifestItem;

    public static (int Gfx,int Weapon)? Parse(string item){
        int dash=item.IndexOf('-');if(dash<=0)return null;
        return SprManifest.Number(item[..dash]) is int gfx&&SprManifest.Number(item[(dash+1)..]) is int weapon and <=255?(gfx,weapon):null;
    }
    public static string? InheritedPath(int gfx,int weapon)=>
        ObjectsRecipe.CharacterGfx.Contains(gfx)&&InheritedWeapons.Contains(weapon)?(weapon==0?$"objects/{gfx}.json":$"objects/{gfx}-weapon-{weapon}.json"):null;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        var parsed=new List<(string Item,int Gfx,int Weapon)>();bool manifest=false;
        foreach(string item in items){
            if(item==SprManifest.ManifestItem){manifest=true;continue;}
            var p=Parse(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"players：\"{item}\" 不是可準備的項目（<gfx>-<weapon>、{SprManifest.ManifestItem}）");
            parsed.Add((item,p.Gfx,p.Weapon));
        }
        var result=parsed.DistinctBy(p=>p.Item).OrderBy(p=>p.Gfx).ThenBy(p=>p.Weapon).Select(p=>p.Item).ToList();
        if(manifest)result.Add(SprManifest.ManifestItem);
        return result;
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item==SprManifest.ManifestItem){WriteManifest(context);return;}
        var (gfx,weapon)=Parse(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"players：\"{item}\" 不是可準備的項目");
        SprManifest.OrUnavailable(context,()=>Profile(context,gfx,weapon),reason=>SprManifest.Json(w=>{
            w.WriteStartObject();w.WriteNumber("gfx",gfx);w.WriteNumber("weapon",weapon);w.WriteString("reason",reason);w.WriteEndObject();
        }));
        context.Set("gfx",SprManifest.Text(gfx));context.Set("weapon",SprManifest.Text(weapon));
    }

    static AssetPrepException Unavailable(string reason)=>new(AssetPrepFailure.Unavailable,reason,AssetPrepInput.TextContainer);

    /// <summary>The seven actions; the first one the entry lacks makes the profile unavailable ("missing complete action &lt;id&gt;/&lt;label&gt;").</summary>
    static (string Label,SprAction Action)[] Actions(SprEntry entry,int weapon)=>
        SprCreature.PlayerActions(weapon).Select(a=>(a.Label,entry.Action(a.Action)??throw Unavailable($"missing complete action {a.Action}/{a.Label}"))).ToArray();

    sealed record Timing(int StepMs,(string Label,int Ms)[] Server,int CastDir,int CastNoDir,int? Projectile);
    /// <summary>stepMs and serverActionMs = 40 × ticks (R1); release points = floor sum before the first '!' (R3).</summary>
    static Timing Times(Func<string,SprAction> Of,int weapon){
        int Release(string label,int id)=>SprTiming.Release(Of(label))??throw Unavailable($"action {id}/{label} has no release event");
        int? projectile=weapon==SprCreature.BowWeapon?Release("attack",weapon+1):null;
        return new(SprTiming.Times40(Of("walk")),[..new[]{"walk","attack","spellDir","spellNoDir"}.Select(l=>(l,SprTiming.Times40(Of(l))))],Release("spellDir",18),Release("spellNoDir",19),projectile);
    }

    static string Row(int gfx,int weapon,string path,string sha,Timing t,int sprite,int frames,bool inherited)=>SprManifest.Json(w=>{
        w.WriteStartObject();
        w.WriteNumber("gfx",gfx);w.WriteNumber("weapon",weapon);w.WriteString("path",path);w.WriteString("sha256",sha);w.WriteNumber("stepMs",t.StepMs);
        w.WriteStartObject("serverActionMs");foreach(var (label,ms) in t.Server)w.WriteNumber(label,ms);w.WriteEndObject();
        SprJson.NullableNumber(w,"projectileReleaseMs",t.Projectile);
        w.WriteStartObject("castReleaseMs");w.WriteNumber("spellDir",t.CastDir);w.WriteNumber("spellNoDir",t.CastNoDir);w.WriteEndObject();
        w.WriteNumber("sprite",sprite);w.WriteNumber("frames",frames);
        if(inherited)w.WriteBoolean("inherited",true);
        w.WriteEndObject();
    });

    static void Profile(AssetRecipeItemContext context,int gfx,int weapon){
        var entry=context.ReadSpriteList().Find(gfx)??throw Unavailable($"list.spz has no entry for gfx {gfx}");
        var actions=Actions(entry,weapon);
        SprAction Of(string label)=>actions.Single(a=>a.Label==label).Action;
        var times=Times(Of,weapon);

        if(InheritedPath(gfx,weapon) is string inherited){
            // objects already holds this stance (T454): check it is the same body and record where it is.
            byte[] data=context.ReadPrior(ObjectsRecipe.RecipeId,inherited);
            var a=SprManifest.ReadAppearance(data,inherited);
            if(a.Gfx!=gfx||(a.Weapon??0)!=weapon||a.Sprite!=entry.Sprite||a.StepMs!=times.StepMs||a.EntryHash!=entry.Sha256)
                throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"{inherited} 不是 gfx {gfx} weapon {weapon} 目前的 profile（gfx、sprite、stepMs 或 entryHash 不符）；要重新準備 objects");
            context.Set(SprManifest.Row,Row(gfx,weapon,inherited,AssetPrepPaths.Sha256(data),times,a.Sprite,a.Frames,inherited:true));
            context.Set("inherited",inherited);
            return;
        }

        var body=new SprBody(context,entry.Sprite);
        var sequences=actions.SelectMany(a=>body.Sequences(a.Label,a.Action)).ToArray();
        // sprites.md §4.2: a profile with an opaque pixel outside its frame's bounds is unavailable (gfx 138, L7).
        int frames=0;
        foreach(int sid in body.Sids){
            var file=body.File(sid);
            for(int f=0;f<file.Frames.Count;f++){
                if(file.FirstPixelOutsideBounds(f) is {} p){var b=file.Frames[f].Bounds;throw Unavailable($"opaque pixel outside frame: {p.X},{p.Y} versus ({b[0]}, {b[1]}, {b[2]}, {b[3]})");}
            }
            frames+=file.Frames.Count;
        }
        string path=$"players/{gfx}-{weapon}.json";
        byte[] json=SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteNumber("gfx",gfx);w.WriteNumber("weapon",weapon);w.WriteNumber("sprite",entry.Sprite);w.WriteNumber("stepMs",times.StepMs);
            // actionMs (sprites.md §4.2): walk ×40, the other six the floor sum.
            w.WriteStartObject("actionMs");
            foreach(var (label,action) in actions)w.WriteNumber(label,label=="walk"?SprTiming.Times40(action):SprTiming.Floor(action));
            w.WriteEndObject();
            w.WriteStartObject("serverActionMs");foreach(var (label,ms) in times.Server)w.WriteNumber(label,ms);w.WriteEndObject();
            SprJson.NullableNumber(w,"projectileReleaseMs",times.Projectile);
            w.WriteStartObject("castReleaseMs");w.WriteNumber("spellDir",times.CastDir);w.WriteNumber("spellNoDir",times.CastNoDir);w.WriteEndObject();
            w.WriteString("entryHash",entry.Sha256);
            SprJson.Sequences(w,sequences,s=>s.Label!="walk");   // walk steps carry no milliseconds
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
        context.Write(path,json);
        context.Set(SprManifest.Row,Row(gfx,weapon,path,AssetPrepPaths.Sha256(json),times,entry.Sprite,frames,inherited:false));
    }

    static void WriteManifest(AssetRecipeItemContext context){
        var (rows,unavailable)=SprManifest.Collect(context,i=>i!=SprManifest.ManifestItem);
        context.Write(ManifestPath,SprJson.Write(w=>{
            w.WriteStartObject();w.WriteString("schema",Schema);
            SprManifest.Rows(w,"profiles",rows);SprManifest.Rows(w,"unavailable",unavailable);
            w.WriteEndObject();
        }));
        context.Set("profiles",SprManifest.Text(rows.Count));context.Set("unavailable",SprManifest.Text(unavailable.Count));
    }
}

[AssetRecipe]
public sealed class ShopsRecipe:IAssetRecipe {
    public const string RecipeId="shops",ManifestPath="shops/manifest.json",Schema="p24-original-shop-appearance-v1";
    /// <summary>The private-shop pose: action 70 ("70.act shop"), eight directions (sprites.md §4.4).</summary>
    public const int ShopAction=70;
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public bool DependsOnRequest(string item)=>item==SprManifest.ManifestItem;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>GfxItems.Normalize(RecipeId,items);

    public void Produce(string item,AssetRecipeItemContext context){
        if(item==SprManifest.ManifestItem){WriteManifest(context);return;}
        int gfx=GfxItems.Parse(RecipeId,item);
        SprManifest.OrUnavailable(context,()=>Shop(context,gfx),reason=>SprManifest.Json(w=>{w.WriteStartObject();w.WriteNumber("gfx",gfx);w.WriteString("reason",reason);w.WriteEndObject();}));
        context.Set("gfx",SprManifest.Text(gfx));
    }

    static void Shop(AssetRecipeItemContext context,int gfx){
        var entry=context.ReadSpriteList().Require(gfx);
        var action=SprCreature.Require(entry,ShopAction,"shop");
        if(!action.Directional)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz：gfx {gfx} 的動作 {ShopAction} 不是八方向",AssetPrepInput.TextContainer);
        var body=new SprBody(context,entry.Sprite);
        var sequences=body.Sequences("shop",action);
        int length=SprTiming.Floor(action);
        string path=$"shops/{gfx}.json";
        byte[] json=SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteNumber("gfx",gfx);w.WriteNumber("sprite",entry.Sprite);w.WriteNumber("stepMs",length);
            w.WriteStartObject("actionMs");w.WriteNumber("shop",length);w.WriteEndObject();
            w.WriteNumber("sourceAction",ShopAction);
            w.WriteStartArray("sequences");
            foreach(var s in sequences){
                w.WriteStartObject();w.WriteString("action",s.Label);w.WriteNumber("direction",s.Direction);
                w.WriteStartArray("sequence");
                for(int i=0;i<s.Keys.Count;i++){
                    var step=s.Action.Steps[i];
                    w.WriteStartObject();w.WriteString("key",s.Keys[i]);w.WriteNumber("ticks",step.Ticks);w.WriteNumber("milliseconds",step.Milliseconds);
                    if(step.Events.Length>0)w.WriteString("events",step.Events);   // the baseline shop tokens carry none
                    w.WriteEndObject();
                }
                w.WriteEndArray();w.WriteEndObject();
            }
            w.WriteEndArray();
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
        context.Write(path,json);
        int frames=body.Sids.Sum(s=>body.File(s).Frames.Count);
        context.Set(SprManifest.Row,SprManifest.Json(w=>{
            w.WriteStartObject();
            w.WriteNumber("gfx",gfx);w.WriteNumber("sprite",entry.Sprite);w.WriteString("path",path);w.WriteString("sha256",AssetPrepPaths.Sha256(json));
            w.WriteNumber("frames",frames);w.WriteNumber("durationMs",length);w.WriteString("metadataSha256",entry.Sha256);
            w.WriteEndObject();
        }));
    }

    static void WriteManifest(AssetRecipeItemContext context){
        var (rows,unavailable)=SprManifest.Collect(context,i=>i!=SprManifest.ManifestItem);
        context.Write(ManifestPath,SprJson.Write(w=>{
            w.WriteStartObject();w.WriteString("schema",Schema);w.WriteNumber("sourceAction",ShopAction);
            SprManifest.Rows(w,"profiles",rows);SprManifest.Rows(w,"unavailable",unavailable);
            w.WriteEndObject();
        }));
        context.Set("profiles",SprManifest.Text(rows.Count));
    }
}

/// <summary>Items "&lt;gfx&gt;" plus "manifest" (shops, doors, shadows).</summary>
static class GfxItems {
    public static IReadOnlyList<string> Normalize(string recipe,IReadOnlyList<string> items){
        var gfx=new List<int>();bool manifest=false;
        foreach(string item in items){
            if(item==SprManifest.ManifestItem){manifest=true;continue;}
            gfx.Add(SprManifest.Number(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"{recipe}：\"{item}\" 不是可準備的項目（<gfx>、{SprManifest.ManifestItem}）"));
        }
        var result=gfx.Distinct().Order().Select(SprManifest.Text).ToList();
        if(manifest)result.Add(SprManifest.ManifestItem);
        return result;
    }
    public static int Parse(string recipe,string item)=>SprManifest.Number(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"{recipe}：\"{item}\" 不是可準備的項目");
}

[AssetRecipe]
public sealed class DoorsRecipe:IAssetRecipe {
    public const string RecipeId="doors",ManifestPath="doors/manifest.json",Schema="p24-original-inn-door-v1";
    /// <summary>Server actions 28 = open, 29 = close; one non-directional sequence for every heading (sprites.md §4.6).</summary>
    public const int OpenAction=28,CloseAction=29;
    /// <summary>Product rule constant (sprites.md §6): not in the SPR or list.spz.</summary>
    public const int DoorStepMs=167;
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public bool DependsOnRequest(string item)=>item==SprManifest.ManifestItem;
    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>GfxItems.Normalize(RecipeId,items);

    public void Produce(string item,AssetRecipeItemContext context){
        if(item==SprManifest.ManifestItem){WriteManifest(context);return;}
        int gfx=GfxItems.Parse(RecipeId,item);
        var entry=context.ReadSpriteList().Require(gfx);
        var open=SprCreature.Require(entry,OpenAction,"open");var close=SprCreature.Require(entry,CloseAction,"close");
        foreach(var a in new[]{open,close})
            if(a.Directional)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz：gfx {gfx} 的動作 {a.Id} 有方向性；門的規則只接受不分方向的動作",AssetPrepInput.TextContainer);
        var body=new SprBody(context,entry.Sprite);
        var sequences=body.Sequences("open",open).Concat(body.Sequences("close",close)).ToArray();
        string path=$"doors/{gfx}.json";
        byte[] json=SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteNumber("gfx",gfx);w.WriteNumber("sprite",entry.Sprite);w.WriteNumber("stepMs",DoorStepMs);
            w.WriteStartArray("sourceActions");w.WriteNumberValue(OpenAction);w.WriteNumberValue(CloseAction);w.WriteEndArray();
            w.WriteBoolean("nonDirectional",true);
            SprJson.Sequences(w,sequences,_=>false);
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
        context.Write(path,json);
        context.Set("gfx",SprManifest.Text(gfx));context.Set("entryHash",entry.Sha256);
        context.Set(SprManifest.Row,SprManifest.Json(w=>{
            w.WriteStartObject();w.WriteNumber("gfx",gfx);w.WriteNumber("sprite",entry.Sprite);w.WriteString("path",path);w.WriteString("sha256",AssetPrepPaths.Sha256(json));w.WriteEndObject();
        }));
    }

    static void WriteManifest(AssetRecipeItemContext context){
        var (rows,_)=SprManifest.Collect(context,i=>i!=SprManifest.ManifestItem);
        context.Write(ManifestPath,SprJson.Write(w=>{
            w.WriteStartObject();w.WriteString("schema",Schema);SprManifest.Rows(w,"profiles",rows);w.WriteEndObject();
        }));
    }
}

[AssetRecipe]
public sealed class AppearanceListsRecipe:IAssetRecipe {
    public const string RecipeId="appearance-lists";
    public const string MonstersItem="encountered-monsters",WorldItem="world-appearances";
    public const string MonstersPath="encountered-monsters.json",WorldPath="world-appearances.json";
    public const string MonstersSchema="p24-original-encountered-monsters-v1",WorldSchema="p24-original-world-appearances-v1";
    /// <summary>What the product's catalogs require in all eight directions (ObjectAssets.cs: monsters / world appearances).</summary>
    public static readonly string[] MonsterActions=["walk","breath","attack","damage","die"],WorldActions=["breath"];
    /// <summary>Order of "supportedActions".</summary>
    public static readonly string[] ActionOrder=["walk","breath","attack","damage","die","spellDir","spellNoDir"];
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public IReadOnlyList<string> Requires=>[ObjectsRecipe.RecipeId];
    public bool DependsOnRequest(string item)=>item is MonstersItem or WorldItem;

    public static (bool World,int Gfx)? ParseMember(string item){
        foreach(var (prefix,world) in new[]{("monster-",false),("world-",true)})
            if(item.StartsWith(prefix,StringComparison.Ordinal)&&SprManifest.Number(item[prefix.Length..]) is int gfx)return (world,gfx);
        return null;
    }

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        var members=new List<(string Item,bool World,int Gfx)>();var lists=new HashSet<string>(StringComparer.Ordinal);
        foreach(string item in items){
            if(item is MonstersItem or WorldItem){lists.Add(item);continue;}
            var m=ParseMember(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"{RecipeId}：\"{item}\" 不是可準備的項目（monster-<gfx>、world-<gfx>、{MonstersItem}、{WorldItem}）");
            members.Add((item,m.World,m.Gfx));
        }
        var result=members.DistinctBy(m=>m.Item).OrderBy(m=>m.World).ThenBy(m=>m.Gfx).Select(m=>m.Item).ToList();
        foreach(string list in new[]{MonstersItem,WorldItem})if(lists.Contains(list))result.Add(list);
        return result;
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item is MonstersItem or WorldItem){WriteList(context,item==WorldItem);return;}
        var (world,gfx)=ParseMember(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"{RecipeId}：\"{item}\" 不是可準備的項目");
        // A world appearance uses objects/world-<gfx>.json when objects prepared one (the clothes composite, world-945), else objects/<gfx>.json.
        string path=$"objects/{gfx}.json";
        if(world&&context.PriorOutputs(ObjectsRecipe.RecipeId).Contains($"objects/world-{gfx}.json",StringComparer.Ordinal))path=$"objects/world-{gfx}.json";
        byte[] data=context.ReadPrior(ObjectsRecipe.RecipeId,path);
        var a=SprManifest.ReadAppearance(data,path);
        if(a.Gfx!=gfx)throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"{path} 的 gfx 是 {a.Gfx}，不是 {gfx}；要重新準備 objects");
        context.Set("gfx",SprManifest.Text(gfx));context.Set("path",path);
        var missing=(world?WorldActions:MonsterActions).Where(x=>!a.Supports(x)).ToArray();
        if(missing.Length>0){
            string reason=$"{path} lacks {string.Join(", ",missing)} in eight directions";
            context.Set("unavailable",reason);
            context.Set(SprManifest.UnavailableRow,SprManifest.Json(w=>{w.WriteStartObject();w.WriteNumber("gfx",gfx);w.WriteString("path",path);w.WriteString("reason",reason);w.WriteEndObject();}));
            return;
        }
        context.Set(SprManifest.Row,SprManifest.Json(w=>{
            w.WriteStartObject();
            w.WriteNumber("gfx",gfx);w.WriteNumber("sprite",a.Sprite);w.WriteString("path",path);w.WriteString("sha256",AssetPrepPaths.Sha256(data));
            w.WriteNumber("frames",a.Frames);w.WriteNumber("durationMs",a.StepMs);
            if(a.EntryHash!=null)w.WriteString("metadataSha256",a.EntryHash);
            w.WriteStartArray("supportedActions");foreach(string action in ActionOrder.Where(a.Supports))w.WriteStringValue(action);w.WriteEndArray();
            w.WriteEndObject();
        }));
    }

    static void WriteList(AssetRecipeItemContext context,bool world){
        var (rows,unavailable)=SprManifest.Collect(context,i=>ParseMember(i) is {} m&&m.World==world);
        context.Write(world?WorldPath:MonstersPath,SprJson.Write(w=>{
            w.WriteStartObject();w.WriteString("schema",world?WorldSchema:MonstersSchema);
            SprManifest.Rows(w,"profiles",rows);
            if(!world||unavailable.Count>0)SprManifest.Rows(w,"unavailable",unavailable);   // the baseline world list has no such key
            w.WriteEndObject();
        }));
        context.Set("profiles",SprManifest.Text(rows.Count));context.Set("unavailable",SprManifest.Text(unavailable.Count));
    }
}

[AssetRecipe]
public sealed class ShadowsRecipe:IAssetRecipe {
    public const string RecipeId="shadows",ManifestPath="shadows/manifest.json";
    public const string Schema="p24-original-shadows-v1",FrameSchema="p24-original-shadow-frames-v1";
    /// <summary>101.shadow(&lt;sprite&gt;): the shadow sprite number. Taken as written, never body + 1 (sprites.md §4.5).</summary>
    public const int ShadowField=101;
    /// <summary>Product rule constant (PlayerShadows.cs: actorFlags | 5).</summary>
    public const int Flags=5;
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public IReadOnlyList<string> Requires=>[ObjectsRecipe.RecipeId,PlayersRecipe.RecipeId];
    public bool DependsOnRequest(string item)=>item==SprManifest.ManifestItem;
    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>GfxItems.Normalize(RecipeId,items);

    /// <summary>
    /// The body profiles of a gfx that objects and players published: objects/&lt;gfx&gt;.json, objects/&lt;gfx&gt;-weapon-&lt;n&gt;.json,
    /// objects/world-&lt;gfx&gt;.json and players/&lt;gfx&gt;-&lt;n&gt;.json (weapons in numeric order).
    /// </summary>
    public static IReadOnlyList<(string Recipe,string Path)> BodyProfiles(AssetRecipeItemContext context,int gfx){
        var objects=new Regex($@"\Aobjects/(?:{gfx}|{gfx}-weapon-([0-9]+)|world-{gfx})\.json\z",RegexOptions.CultureInvariant);
        var players=new Regex($@"\Aplayers/{gfx}-([0-9]+)\.json\z",RegexOptions.CultureInvariant);
        int Weapon(Match m)=>m.Groups[1].Success?int.Parse(m.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture):-1;
        var found=new List<(string,string)>();
        found.AddRange(context.PriorOutputs(ObjectsRecipe.RecipeId).Select(p=>(p,m:objects.Match(p))).Where(x=>x.m.Success)
            .OrderBy(x=>x.p.Contains("/world-",StringComparison.Ordinal)).ThenBy(x=>Weapon(x.m)).Select(x=>(ObjectsRecipe.RecipeId,x.p)));
        found.AddRange(context.PriorOutputs(PlayersRecipe.RecipeId).Select(p=>(p,m:players.Match(p))).Where(x=>x.m.Success)
            .OrderBy(x=>Weapon(x.m)).Select(x=>(PlayersRecipe.RecipeId,x.p)));
        return found;
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item==SprManifest.ManifestItem){WriteManifest(context);return;}
        int gfx=GfxItems.Parse(RecipeId,item);
        var bodies=BodyProfiles(context,gfx);
        if(bodies.Count==0)throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"先後順序錯誤：gfx {gfx} 沒有任何已發布的 body profile（先用 objects 或 players 準備它）");
        SprManifest.OrUnavailable(context,()=>Shadow(context,gfx,bodies),reason=>SprManifest.Json(w=>{w.WriteStartObject();w.WriteNumber("gfx",gfx);w.WriteString("reason",reason);w.WriteEndObject();}));
        context.Set("gfx",SprManifest.Text(gfx));
    }

    static void Shadow(AssetRecipeItemContext context,int gfx,IReadOnlyList<(string Recipe,string Path)> bodies){
        var profiles=bodies.Select(b=>(b.Path,Appearance:SprManifest.ReadAppearance(context.ReadPrior(b.Recipe,b.Path),b.Path))).ToArray();
        foreach(var (bodyPath,a) in profiles)
            if(a.Gfx!=gfx)throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"{bodyPath} 的 gfx 是 {a.Gfx}，不是 {gfx}；要重新準備它的 recipe");
        int bodySprite=profiles[0].Appearance.Sprite;
        if(profiles.Any(p=>p.Appearance.Sprite!=bodySprite))
            throw new AssetPrepException(AssetPrepFailure.Unavailable,$"gfx {gfx} 的 body profile 用了不同的 sprite（{string.Join("、",profiles.Select(p=>$"{p.Path}={p.Appearance.Sprite}"))}）");
        var entry=context.ReadSpriteList().Require(gfx);
        var field=entry.Field(ShadowField);
        if(field is not [int shadowSprite]||shadowSprite is <0 or >ushort.MaxValue)
            throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz 的 gfx {gfx} 沒有可用的 {ShadowField}.shadow 欄位",AssetPrepInput.TextContainer);

        // Keys = the union of the keys the body sequences actually use, by (sid, frame); not every frame of the body.
        var keys=new SortedSet<(int Sid,int Frame)>();
        foreach(var (bodyPath,a) in profiles)foreach(string key in a.Keys)
            keys.Add(SprManifest.ParseKey(key)??throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"{bodyPath} 的幀 key \"{key}\" 不是 sid:frame"));
        var shadow=new SprBody(context,shadowSprite);
        foreach(var (sid,frame) in keys){
            var file=shadow.File(sid);
            if(frame>=file.Frames.Count)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"gfx {gfx}：{SprBody.FileName(shadowSprite,sid)} 只有 {file.Frames.Count} 幀，body 用到第 {frame} 幀");
        }
        string path=$"shadows/{gfx}.json";
        byte[] json=SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteString("schema",FrameSchema);w.WriteNumber("gfx",gfx);w.WriteNumber("bodySprite",bodySprite);w.WriteNumber("shadowSprite",shadowSprite);w.WriteNumber("flags",Flags);
            w.WriteStartArray("frames");
            foreach(var (sid,frame) in keys)SprJson.Frame(w,SprJson.Key(sid,frame),shadow.File(sid),frame);
            w.WriteEndArray();
            w.WriteEndObject();
        });
        context.Write(path,json);
        context.Set("shadowSprite",SprManifest.Text(shadowSprite));
        context.Set(SprManifest.Row,SprManifest.Json(w=>{
            w.WriteStartObject();
            w.WriteNumber("gfx",gfx);w.WriteNumber("bodySprite",bodySprite);w.WriteNumber("shadowSprite",shadowSprite);w.WriteString("path",path);w.WriteString("sha256",AssetPrepPaths.Sha256(json));
            w.WriteStartArray("sourceBodyProfiles");foreach(var (p,_) in profiles)w.WriteStringValue(p);w.WriteEndArray();
            w.WriteStartArray("actions");foreach(string action in profiles.SelectMany(p=>p.Appearance.Sequences.Select(s=>s.Action)).Distinct().Order(StringComparer.Ordinal))w.WriteStringValue(action);w.WriteEndArray();
            w.WriteNumber("requiredKeys",keys.Count);w.WriteNumber("providedKeys",keys.Count);
            w.WriteEndObject();
        }));
    }

    static void WriteManifest(AssetRecipeItemContext context){
        var (rows,unavailable)=SprManifest.Collect(context,i=>i!=SprManifest.ManifestItem);
        context.Write(ManifestPath,SprJson.Write(w=>{
            w.WriteStartObject();w.WriteString("schema",Schema);
            SprManifest.Rows(w,"profiles",rows);SprManifest.Rows(w,"unavailable",unavailable);
            w.WriteEndObject();
        }));
        context.Set("profiles",SprManifest.Text(rows.Count));
    }
}
