using System.Text.Json;
namespace P06.AssetPrep;

// T463 (#1048, B15): recipe "pickup" — the own pickup presentation frames that used to be two embedded JSON resources.
// Contract: docs/design/asset-recipe.md 2 (row 20), 3.2 (pickup), 6 (order 3); rule: docs/reference/asset-recipes/sprites.md §5.
//
//   order 3  pickup  "pickup" → pickup/pickup-own-base15-gfx0-weapon0.json       gfx 0 / weapon 0 (the base group)
//            (needs objects, players)  pickup/pickup-own-base15-profile-extension.json  every other group and profile
//
// One item writes both files, so the extension's requiresResource SHA and the base file always come from the same sources.
// Which (gfx, weapon) pairs get a pickup profile follows the published players manifest (sprites.md §5: "the 42 profiles
// follow the players list"); nothing here lists gfx numbers. Rules, per group (one gfx):
//   - action 15 of list.spz, eight directions, exactly four steps with ticks 1, 1, 4, 4 (the product's step contract);
//   - step 0 is not embedded: its frame already is in the body profile (storage "base-profile"); steps 1–3 are ("embedded");
//   - frames: direction 0–7, each direction steps 1, 2, 3; frameOrder [1, 2, 3, 0]; blocks keep a, b, z, source_order, pixels.
// A gfx whose action 15 does not fit, or a profile whose body file lacks the step-0 frame, gets no pickup profile (recorded in
// the receipt as "skipped:<gfx>-<weapon>"); the product then shows no pickup animation for it, as for any unsupported stance.
// gfx 0 / weapon 0 is the base the product requires: if it cannot be built the recipe stops.

[AssetRecipe]
public sealed class PickupRecipe:IAssetRecipe {
    public const string RecipeId="pickup",Item="pickup";
    public const string BasePath="pickup/pickup-own-base15-gfx0-weapon0.json",ExtensionPath="pickup/pickup-own-base15-profile-extension.json";
    public const string BaseSchema="pickup-own-base15-gfx0-weapon0-v1",ExtensionSchema="pickup-own-base15-profile-extension-v1";
    /// <summary>"15.get" in list.spz.</summary>
    public const int PickupAction=15;
    public const int BaseGfx=0,BaseWeapon=0;
    /// <summary>Where the base group's step-0 frame lives (the inherited gfx 0 stance objects publishes).</summary>
    public const string BaseProfilePath="objects/0.json";
    /// <summary>The product's step contract (PickupAssets.ReadBody): four steps, these ticks.</summary>
    public static readonly int[] StepTicks=[1,1,4,4];
    public static readonly int[] FrameOrder=[1,2,3,0];
    public const string BaseBody="r34-base-resource",ExtensionBody="extension";

    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public IReadOnlyList<string> Requires=>[ObjectsRecipe.RecipeId,PlayersRecipe.RecipeId];

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        if(items.Count==0)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"pickup：沒有要準備的項目");
        foreach(string item in items)
            if(item!=Item)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"pickup：\"{item}\" 不是可準備的項目（只有 {Item}）");
        return [Item];
    }

    sealed record ProfileRow(int Gfx,int Weapon,string Path,string Sha256,int Sprite);
    sealed record Group(int Gfx,int Sprite,SprBody Body,SprSequence[] Sequences);

    static AssetPrepException Stale(string message)=>new(AssetPrepFailure.Prerequisite,message);

    static List<ProfileRow> ReadProfiles(byte[] data){
        try{
            using var doc=JsonDocument.Parse(data);
            return doc.RootElement.GetProperty("profiles").EnumerateArray().Select(p=>new ProfileRow(p.GetProperty("gfx").GetInt32(),p.GetProperty("weapon").GetInt32(),
                p.GetProperty("path").GetString()!,p.GetProperty("sha256").GetString()!,p.GetProperty("sprite").GetInt32())).ToList();
        }catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){
            throw Stale($"{PlayersRecipe.ManifestPath} 不是可讀的 players manifest（{ex.Message}）；要重新準備 players");
        }
    }

    /// <summary>gfx, sprite and the frame keys of a published body profile.</summary>
    static (int Gfx,int Sprite,HashSet<string> Keys) ReadBodyProfile(byte[] data,string path){
        try{
            using var doc=JsonDocument.Parse(data);var j=doc.RootElement;
            var keys=j.GetProperty("frames").EnumerateArray().Select(f=>f.GetProperty("key").GetString()!).ToHashSet(StringComparer.Ordinal);
            return (j.GetProperty("gfx").GetInt32(),j.GetProperty("sprite").GetInt32(),keys);
        }catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){
            throw Stale($"{path} 不是可讀的外觀檔（{ex.Message}）；要重新準備它的 recipe");
        }
    }

    /// <summary>The group of one gfx, or the reason it has none.</summary>
    static (Group? Group,string? Reason) BuildGroup(AssetRecipeItemContext context,SpriteList list,int gfx,int sprite){
        var entry=list.Find(gfx);
        if(entry==null)return (null,$"list.spz has no entry for gfx {gfx}");
        if(entry.Sprite!=sprite)throw Stale($"players manifest 記 gfx {gfx} 的 sprite 是 {sprite}，list.spz 是 {entry.Sprite}；要重新準備 players");
        var action=entry.Action(PickupAction);
        if(action==null)return (null,$"list.spz gfx {gfx} has no action {PickupAction}");
        if(!action.Directional)return (null,$"action {PickupAction} of gfx {gfx} is not eight-directional");
        if(!action.Steps.Select(s=>s.Ticks).SequenceEqual(StepTicks))
            return (null,$"action {PickupAction} of gfx {gfx} has ticks [{string.Join(",",action.Steps.Select(s=>s.Ticks))}], not [{string.Join(",",StepTicks)}]");
        var body=new SprBody(context,entry.Sprite);
        var sequences=body.Sequences("get",action);
        var embedded=sequences.SelectMany(s=>s.Keys.Skip(1)).ToList();
        if(embedded.Distinct(StringComparer.Ordinal).Count()!=embedded.Count)return (null,$"action {PickupAction} of gfx {gfx} repeats an embedded frame");
        if(sequences.Any(s=>embedded.Contains(s.Keys[0],StringComparer.Ordinal)))return (null,$"action {PickupAction} of gfx {gfx}: a step-0 frame is also an embedded frame");
        return (new(gfx,entry.Sprite,body,sequences),null);
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item!=Item)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"pickup：\"{item}\" 不是可準備的項目");
        var rows=ReadProfiles(context.ReadPrior(PlayersRecipe.RecipeId,PlayersRecipe.ManifestPath));
        if(rows.Select(r=>(r.Gfx,r.Weapon)).Distinct().Count()!=rows.Count)throw Stale($"{PlayersRecipe.ManifestPath} 有重複的 profile；要重新準備 players");
        if(!rows.Any(r=>r.Gfx==BaseGfx&&r.Weapon==BaseWeapon))
            throw Stale($"players 沒有 gfx {BaseGfx} weapon {BaseWeapon} 的 profile（pickup 的基底）；先用 players 準備 {BaseGfx}-{BaseWeapon}");
        var list=context.ReadSpriteList();

        var groups=new SortedDictionary<int,Group>();
        var accepted=new List<ProfileRow>();
        foreach(var byGfx in rows.GroupBy(r=>r.Gfx).OrderBy(g=>g.Key)){
            int gfx=byGfx.Key;
            if(byGfx.Select(r=>r.Sprite).Distinct().Count()!=1)throw Stale($"players manifest 的 gfx {gfx} 用了不同的 sprite；要重新準備 players");
            var (group,reason)=BuildGroup(context,list,gfx,byGfx.First().Sprite);
            foreach(var row in byGfx.OrderBy(r=>r.Weapon)){
                string? skip=reason;
                if(group!=null){
                    string recipe=row.Path.StartsWith("objects/",StringComparison.Ordinal)?ObjectsRecipe.RecipeId:row.Path.StartsWith("players/",StringComparison.Ordinal)?PlayersRecipe.RecipeId
                        :throw Stale($"players manifest 的 {gfx}-{row.Weapon} 指向 {row.Path}，不是 objects／players 的輸出");
                    byte[] data=context.ReadPrior(recipe,row.Path);
                    if(AssetPrepPaths.Sha256(data)!=row.Sha256)throw Stale($"{row.Path} 與 players manifest 記的 SHA-256 不符；要重新準備 players");
                    var (bodyGfx,bodySprite,keys)=ReadBodyProfile(data,row.Path);
                    if(bodyGfx!=gfx||bodySprite!=group.Sprite)throw Stale($"{row.Path} 的 gfx／sprite 是 {bodyGfx}／{bodySprite}，不是 {gfx}／{group.Sprite}；要重新準備它的 recipe");
                    if(group.Sequences.FirstOrDefault(s=>!keys.Contains(s.Keys[0])) is {} missing)skip=$"{row.Path} has no frame {missing.Keys[0]} (step 0, direction {missing.Direction})";
                }
                if(gfx==BaseGfx&&row.Weapon==BaseWeapon){
                    if(skip!=null)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"pickup 的基底 gfx {BaseGfx} weapon {BaseWeapon} 無法產生：{skip}",AssetPrepInput.TextContainer);
                    if(row.Path!=BaseProfilePath)throw Stale($"players manifest 的 {BaseGfx}-{BaseWeapon} 指向 {row.Path}，不是 {BaseProfilePath}；要重新準備 players");
                }
                if(skip!=null){context.Set($"skipped:{gfx}-{row.Weapon}",skip);continue;}
                accepted.Add(row);groups[gfx]=group!;
            }
        }

        var baseRow=accepted.Single(r=>r.Gfx==BaseGfx&&r.Weapon==BaseWeapon);var baseGroup=groups[BaseGfx];
        byte[] baseJson=SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteString("schema",BaseSchema);w.WriteNumber("gfx",BaseGfx);w.WriteNumber("weapon",BaseWeapon);w.WriteNumber("sprite",baseGroup.Sprite);
            w.WriteString("baseProfilePath",baseRow.Path);w.WriteString("baseProfileSha256",baseRow.Sha256);
            WriteFrameOrder(w);WriteGroupBody(w,baseGroup);
            w.WriteEndObject();
        });
        context.Write(BasePath,baseJson);

        var extensionGroups=groups.Values.Where(g=>g.Gfx!=BaseGfx).ToList();
        var extensionProfiles=accepted.Where(r=>!(r.Gfx==BaseGfx&&r.Weapon==BaseWeapon)).ToList();
        context.Write(ExtensionPath,SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteString("schema",ExtensionSchema);WriteFrameOrder(w);
            w.WriteStartObject("requiresResource");w.WriteString("path",BasePath);w.WriteString("sha256",AssetPrepPaths.Sha256(baseJson));w.WriteEndObject();
            w.WriteStartArray("groups");
            foreach(var g in extensionGroups){w.WriteStartObject();w.WriteNumber("gfx",g.Gfx);w.WriteNumber("sprite",g.Sprite);WriteGroupBody(w,g);w.WriteEndObject();}
            w.WriteEndArray();
            w.WriteStartArray("profiles");
            foreach(var r in extensionProfiles){
                w.WriteStartObject();
                w.WriteNumber("gfx",r.Gfx);w.WriteNumber("weapon",r.Weapon);w.WriteNumber("sprite",groups[r.Gfx].Sprite);w.WriteNumber("sequenceGroup",r.Gfx);
                w.WriteString("bodySource",r.Gfx==BaseGfx?BaseBody:ExtensionBody);
                w.WriteString("baseProfilePath",r.Path);w.WriteString("baseProfileSha256",r.Sha256);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }));
        context.Set("groups",SprManifest.Text(groups.Count));
        context.Set("profiles",SprManifest.Text(accepted.Count));
        context.Set("frames",SprManifest.Text(groups.Values.Sum(g=>g.Sequences.Sum(s=>s.Keys.Count-1))));
    }

    public const string RuntimeGroupPath="pickup/runtime-group.json";
    /// <summary>
    /// T478 (#1074): one pickup group for a body the runtime holds (an on-demand player stance, outside the prepared pickup data).
    /// The same rule as preparation: action 15 of list.spz, eight directions, ticks 1, 1, 4, 4, no repeated embedded frame,
    /// and every step-0 frame present in the body (<paramref name="bodyKeys"/>). Writes one extension group object
    /// ({gfx, sprite, sequences, frames}) to <see cref="RuntimeGroupPath"/> of the (memory) context; a gfx without a usable
    /// action 15 is refused (Unavailable) — the product then shows no pickup animation, as for any stance without one.
    /// </summary>
    public static void RuntimeGroup(AssetRecipeItemContext context,int gfx,int sprite,IReadOnlySet<string> bodyKeys){
        var (group,reason)=BuildGroup(context,context.ReadSpriteList(),gfx,sprite);
        if(group==null)throw new AssetPrepException(AssetPrepFailure.Unavailable,reason!,AssetPrepInput.TextContainer);
        if(group.Sequences.FirstOrDefault(s=>!bodyKeys.Contains(s.Keys[0])) is {} missing)
            throw new AssetPrepException(AssetPrepFailure.Unavailable,$"body of gfx {gfx} has no frame {missing.Keys[0]} (step 0, direction {missing.Direction})",AssetPrepInput.TextContainer);
        context.Write(RuntimeGroupPath,SprJson.Write(w=>{
            w.WriteStartObject();w.WriteNumber("gfx",gfx);w.WriteNumber("sprite",group.Sprite);WriteGroupBody(w,group);w.WriteEndObject();
        }));
    }

    static void WriteFrameOrder(Utf8JsonWriter w){w.WriteStartArray("frameOrder");foreach(int i in FrameOrder)w.WriteNumberValue(i);w.WriteEndArray();}

    /// <summary>"sequences" (eight directions × four steps) then "frames" (direction 0–7, steps 1–3 of each).</summary>
    static void WriteGroupBody(Utf8JsonWriter w,Group g){
        w.WriteStartArray("sequences");
        foreach(var s in g.Sequences){
            w.WriteStartObject();w.WriteNumber("direction",s.Direction);
            w.WriteStartArray("steps");
            for(int i=0;i<s.Keys.Count;i++){
                var step=s.Action.Steps[i];
                w.WriteStartObject();
                w.WriteString("key",s.Keys[i]);w.WriteNumber("ticks",step.Ticks);w.WriteString("events",step.Events);
                w.WriteNumber("metadataIndex",i);w.WriteString("storage",i==0?"base-profile":"embedded");
                w.WriteEndObject();
            }
            w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("frames");
        foreach(var s in g.Sequences)
            for(int i=1;i<s.Keys.Count;i++){
                var step=s.Action.Steps[i];int sid=s.Action.Sid(step,s.Direction);
                SprJson.Frame(w,s.Keys[i],g.Body.File(sid),step.Frame);
            }
        w.WriteEndArray();
    }
}
