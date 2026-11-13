using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06.AssetPrep;

// T454 (#1039, B15): recipes "character" and "objects" — SPR appearances rebuilt from the user's own list.spz and Sprite
// containers. Contract: docs/design/asset-recipe.md 1.1–1.3, 3.2, 5, 6; rules: docs/reference/asset-recipes/sprites.md §3,
// §4.1, §4.3, §6, §8; times: action-timing.md R1–R3. Shared decoding lives in SprShared.cs.
//
//   character  item "character"            → character/blocks.json (p06-spr-blocks-v1), character/animation.json
//                                             (p04-character-v1). No PNG, no preview, no entry text (asset-recipe.md 9).
//   objects    item "<gfx>"                → objects/<gfx>.json            creature rules (gfx 0: player rules, weapon 0)
//              item "<gfx>-weapon-<n>"     → objects/<gfx>-weapon-<n>.json player rules (seven actions, all required)
//              item "world-<gfx>"          → objects/world-<gfx>.json      creature rules; world-234 is the clothes composite
//              item "magic-<gfx>"          → objects/magic-<gfx>.json      action 0, one label, stepMs 1
//              item "projectile-<gfx>"     → objects/projectile-<gfx>.json action 0 "fly", frames for ProjectileRendering
//              item "manifest"             → objects/manifest.json         {"gfx":[the plain <gfx> items of this request]}
//
// Only rules are written here (action numbers, labels, the timing formulas, the constants of sprites.md §6 and the few
// gfx numbers whose rule differs). No frame, pixel or table content. Fields that only recorded provenance in the baseline
// (sourceOffset, spzOffset, sourceActions, serverSpeedSource, entry, sourceComponents, sourcePolicy, eventPolicy,
// source_offset, block_index, sourceEmpty, sourceHeaderOffset, sourceSprite/Sid/Frame) are not produced; entryHash is kept.
// Any item whose rules cannot be met (an action missing, alternates that differ, a step past the file's frames) stops the
// run with the item named: nothing is substituted and no partial file is written.

/// <summary>The creature-style appearance files (objects, and later players): action numbers, labels and timing rules.</summary>
public static class SprCreature {
    /// <summary>The seven actions of a player profile for stance <paramref name="weapon"/> (sprites.md §4.2).</summary>
    public static (string Label,int Action)[] PlayerActions(int weapon)=>
        [("walk",weapon),("attack",weapon+1),("damage",weapon+2),("breath",weapon+3),("die",8),("spellDir",18),("spellNoDir",19)];
    /// <summary>The creature actions an object file takes when the entry has them (sprites.md §4.3; action 8 is labelled die whatever its name).</summary>
    public static readonly (string Label,int Action)[] MonsterActions=[("walk",0),("attack",1),("damage",2),("breath",3),("die",8),("spellDir",18),("spellNoDir",19)];
    /// <summary>The bow stance: its attack, damage, breath and die lengths follow the floor rule (sprites.md §4.3 「時間欄位」).</summary>
    public const int BowWeapon=20;

    public enum Duration{Times40,Floor,Exact}
    /// <summary>actionMs rule of a player-profile action (sprites.md §4.3; D4 keeps the baseline formulas).</summary>
    public static Duration PlayerRule(string label,int weapon)=>label switch{
        "walk"=>Duration.Times40,
        "spellDir" or "spellNoDir"=>Duration.Floor,
        "attack"=>weapon==BowWeapon?Duration.Floor:Duration.Times40,
        _=>weapon==BowWeapon?Duration.Floor:Duration.Exact,
    };
    /// <summary>actionMs rule of a monster / NPC action: walk, attack and the spells ×40; breath, damage and die exact.</summary>
    public static Duration MonsterRule(string label)=>label is "walk" or "attack" or "spellDir" or "spellNoDir"?Duration.Times40:Duration.Exact;

    public static void WriteDuration(Utf8JsonWriter w,string name,SprAction action,Duration rule){
        switch(rule){
            case Duration.Times40:w.WriteNumber(name,SprTiming.Times40(action));break;
            case Duration.Floor:w.WriteNumber(name,SprTiming.Floor(action));break;
            default:w.WriteNumber(name,SprTiming.Exact(action));break;
        }
    }

    /// <summary>The entry's action <paramref name="id"/> or a refusal naming the gfx (Unavailable).</summary>
    public static SprAction Require(SprEntry entry,int id,string label)=>entry.Action(id)
        ??throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz 的 gfx {entry.Gfx} 沒有動作 {id}（{label}）",AssetPrepInput.TextContainer);

    /// <summary>"sequences" of creature files: action, direction, totalTicks and the steps; milliseconds where <paramref name="withMilliseconds"/> says so.</summary>
    public static void Sequences(Utf8JsonWriter w,IEnumerable<SprSequence> sequences,Func<SprSequence,bool> withMilliseconds){
        w.WriteStartArray("sequences");
        foreach(var s in sequences){
            bool ms=withMilliseconds(s);
            w.WriteStartObject();
            w.WriteString("action",s.Label);w.WriteNumber("direction",s.Direction);w.WriteNumber("totalTicks",s.Action.TotalTicks);
            w.WriteStartArray("sequence");
            for(int i=0;i<s.Keys.Count;i++){
                var step=s.Action.Steps[i];
                w.WriteStartObject();
                w.WriteString("key",s.Keys[i]);w.WriteNumber("ticks",step.Ticks);
                if(ms)w.WriteNumber("milliseconds",step.Milliseconds);
                w.WriteString("events",step.Events);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}

[AssetRecipe]
public sealed class CharacterRecipe:IAssetRecipe {
    public const string RecipeId="character",Item="character";
    public const string BlocksPath="character/blocks.json",AnimationPath="character/animation.json";
    /// <summary>The demo character: gfx 0, actions 0 (walk) and 3 (breath) in eight directions (sprites.md §4.1).</summary>
    public const int Gfx=0,WalkAction=0,BreathAction=3;
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Complete;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        foreach(string item in items)if(item!=Item)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"character：\"{item}\" 不是可準備的項目（只有 {Item}）");
        return items.Count==0?items:new[]{Item};
    }

    /// <summary>"&lt;479" → [479]. The character events are sound numbers only; any other marker is refused rather than dropped.</summary>
    static readonly Regex soundEvents=new(@"\A(?:<(\d+))*\z",RegexOptions.CultureInvariant);
    public static int[] SoundEvents(string events,int gfx){
        var m=soundEvents.Match(events);
        if(!m.Success)throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"list.spz：gfx {gfx} 的事件 \"{events}\" 不是 character 支援的格式",AssetPrepInput.TextContainer);
        return m.Groups[1].Captures.Select(c=>int.Parse(c.Value,NumberStyles.None,CultureInfo.InvariantCulture)).ToArray();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item!=Item)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"character：\"{item}\" 不是可準備的項目");
        var list=context.ReadSpriteList();
        var entry=list.Require(Gfx);
        var walk=SprCreature.Require(entry,WalkAction,"walk");var breath=SprCreature.Require(entry,BreathAction,"breath");
        foreach(var action in new[]{walk,breath})
            if(!action.Directional)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz：gfx {Gfx} 的動作 {action.Id} 不是八方向",AssetPrepInput.TextContainer);
        var body=new SprBody(context,entry.Sprite);
        var sequences=body.Sequences("walk",walk).Concat(body.Sequences("breath",breath)).ToArray();

        context.Write(BlocksPath,SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteString("schema","p06-spr-blocks-v1");
            w.WriteStartArray("frames");
            foreach(int sid in body.Sids){
                var file=body.File(sid);
                for(int f=0;f<file.Frames.Count;f++){
                    var bounds=file.Frames[f].Bounds;
                    SprJson.Frame(w,SprJson.Key(sid,f),file,f,x=>{
                        x.WriteStartArray("anchor");x.WriteNumberValue(-bounds[0]);x.WriteNumberValue(-bounds[1]);x.WriteEndArray();
                        x.WriteString("source_sha256",file.Sha256);
                    });
                }
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }));
        context.Write(AnimationPath,SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteString("schema","p04-character-v1");
            w.WriteStartObject("character");w.WriteNumber("id",Gfx);w.WriteNumber("sprite_id",entry.Sprite);w.WriteEndObject();
            w.WriteStartObject("metadata");w.WriteString("spz_sha256",list.Sha256);w.WriteString("entry_sha256",entry.Sha256);w.WriteEndObject();
            w.WriteStartArray("mapping");
            foreach(var s in sequences){
                w.WriteStartObject();
                w.WriteString("action",s.Label);w.WriteNumber("direction",s.Direction);w.WriteBoolean("flip_x",false);
                w.WriteStartArray("sequence");
                for(int i=0;i<s.Keys.Count;i++){
                    var step=s.Action.Steps[i];
                    w.WriteStartObject();
                    w.WriteString("key",s.Keys[i]);w.WriteNumber("subid",s.Action.Sid(step,s.Direction));w.WriteNumber("frame",step.Frame);
                    w.WriteNumber("ticks",step.Ticks);w.WriteNumber("base_duration_ms",step.Milliseconds);
                    w.WriteStartArray("events");foreach(int e in SoundEvents(step.Events,Gfx))w.WriteNumberValue(e);w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }));
        context.Set("sprite",entry.Sprite.ToString(CultureInfo.InvariantCulture));
        context.Set("files",body.Sids.Count.ToString(CultureInfo.InvariantCulture));
        context.Set("frames",body.Sids.Sum(s=>body.File(s).Frames.Count).ToString(CultureInfo.InvariantCulture));
        context.Set("sequences",sequences.Length.ToString(CultureInfo.InvariantCulture));
    }
}

[AssetRecipe]
public sealed class ObjectsRecipe:IAssetRecipe {
    public const string RecipeId="objects",ManifestItem="manifest",ManifestPath="objects/manifest.json";
    public enum Kind{Plain,Weapon,World,Magic,Projectile,Manifest}
    public sealed record ObjectItem(Kind Kind,int Gfx,int Weapon,string Name){
        public string Path=>"objects/"+Name+".json";
    }

    // ---- Rule constants (sprites.md §4.3, §6). Integers and labels only. ----
    /// <summary>Plain object items built with the player rules: gfx 0 is the player body the product reads as objects/0.json.</summary>
    public static readonly IReadOnlySet<int> CharacterGfx=new HashSet<int>{0};
    /// <summary>The label of a magic file's action 0; every other magic gfx is "effect".</summary>
    public static readonly IReadOnlyDictionary<int,string> MagicLabels=new Dictionary<int,string>{[167]="fly",[219]="impact",[11722]="heal"};
    /// <summary>Magic files drawn with the product's additive blend (effectTileType 18).</summary>
    public static readonly IReadOnlySet<int> AdditiveMagic=new HashSet<int>{189,191,197,221,2165};
    public const int AdditiveTileType=18,MagicStepMs=1;
    /// <summary>Magic gfx whose directional action has fewer files than directions: sid = base + (d mod n). Only 167 is known (sprites.md §4.3).</summary>
    public static readonly IReadOnlyDictionary<int,int> MagicDirectionModulo=new Dictionary<int,int>{[167]=4};
    /// <summary>World appearances drawn as the body plus its 105.clothes sprites (sprites.md §4.3 world-234).</summary>
    public static readonly IReadOnlySet<int> ClothesComposite=new HashSet<int>{234};
    public const int ClothesField=105;

    public string Id=>RecipeId;
    public int Version=>1;
    /// <summary>Partial: the items are the appearances asked for, not every gfx of the client (asset-recipe.md 5).</summary>
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public bool DependsOnRequest(string item)=>item==ManifestItem;

    static readonly Regex number=new(@"\A(?:0|[1-9][0-9]{0,4})\z",RegexOptions.CultureInvariant);
    static int? Number(string text)=>number.IsMatch(text)&&int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out int v)&&v<=ushort.MaxValue?v:null;

    /// <summary>Parses an item name; null when it is not one this recipe knows.</summary>
    public static ObjectItem? Parse(string item){
        if(item==ManifestItem)return new(Kind.Manifest,-1,0,item);
        if(Number(item) is {} plain)return new(Kind.Plain,plain,0,item);
        int dash=item.IndexOf('-');if(dash<0)return null;
        string head=item[..dash],tail=item[(dash+1)..];
        if(Number(head) is {} gfx&&tail.StartsWith("weapon-",StringComparison.Ordinal)&&Number(tail[7..]) is int weapon and >0 and <=255)return new(Kind.Weapon,gfx,weapon,item);
        if(Number(tail) is not {} g)return null;
        return head switch{"world"=>new(Kind.World,g,0,item),"magic"=>new(Kind.Magic,g,0,item),"projectile"=>new(Kind.Projectile,g,0,item),_=>null};
    }

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        var parsed=new List<ObjectItem>();
        foreach(string item in items)
            parsed.Add(Parse(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"objects：\"{item}\" 不是可準備的項目（<gfx>、<gfx>-weapon-<n>、world-／magic-／projectile-<gfx>、{ManifestItem}）"));
        // Canonical order: by gfx, then kind, then weapon; the manifest last (it lists what the request holds).
        return parsed.DistinctBy(p=>p.Name).OrderBy(p=>p.Kind==Kind.Manifest?1:0).ThenBy(p=>p.Gfx).ThenBy(p=>p.Kind).ThenBy(p=>p.Weapon).Select(p=>p.Name).ToList();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        var o=Parse(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"objects：\"{item}\" 不是可準備的項目");
        if(o.Kind==Kind.Manifest){WriteManifest(context);return;}
        var entry=context.ReadSpriteList().Require(o.Gfx);
        byte[] json=o.Kind switch{
            Kind.Weapon=>Player(context,entry,o.Weapon),
            Kind.Plain when CharacterGfx.Contains(o.Gfx)=>Player(context,entry,0),
            Kind.World when ClothesComposite.Contains(o.Gfx)=>Composite(context,entry),
            Kind.Plain or Kind.World=>Monster(context,entry),
            Kind.Magic=>Magic(context,entry),
            _=>Projectile(context,entry),
        };
        context.Write(o.Path,json);
        context.Set("gfx",o.Gfx.ToString(CultureInfo.InvariantCulture));
        context.Set("sprite",entry.Sprite.ToString(CultureInfo.InvariantCulture));
        context.Set("entryHash",entry.Sha256);
    }

    static void WriteManifest(AssetRecipeItemContext context){
        var gfx=context.Requested.Select(Parse).Where(p=>p is {Kind:Kind.Plain}).Select(p=>p!.Gfx).Order().ToArray();
        context.Write(ManifestPath,SprJson.Write(w=>{
            w.WriteStartObject();
            w.WriteStartArray("gfx");foreach(int g in gfx)w.WriteNumberValue(g);w.WriteEndArray();
            w.WriteEndObject();
        }));
        context.Set("gfx",string.Join(",",gfx));
    }

    static void Head(Utf8JsonWriter w,SprEntry entry){w.WriteNumber("gfx",entry.Gfx);}

    /// <summary>Player profile (objects/0*.json): seven actions, every one required.</summary>
    static byte[] Player(AssetRecipeItemContext context,SprEntry entry,int weapon){
        var actions=SprCreature.PlayerActions(weapon).Select(a=>(a.Label,Action:SprCreature.Require(entry,a.Action,a.Label))).ToArray();
        var body=new SprBody(context,entry.Sprite);
        var sequences=actions.SelectMany(a=>body.Sequences(a.Label,a.Action)).ToArray();
        SprAction Of(string label)=>actions.Single(a=>a.Label==label).Action;
        return SprJson.Write(w=>{
            w.WriteStartObject();
            Head(w,entry);w.WriteNumber("weapon",weapon);w.WriteNumber("sprite",entry.Sprite);
            w.WriteNumber("stepMs",SprTiming.Times40(Of("walk")));
            w.WriteStartObject("actionMs");
            foreach(var (label,action) in actions)SprCreature.WriteDuration(w,label,action,SprCreature.PlayerRule(label,weapon));
            w.WriteEndObject();
            w.WriteString("entryHash",entry.Sha256);
            if(weapon==SprCreature.BowWeapon){
                w.WriteNumber("serverAttackMs",SprTiming.Times40(Of("attack")));
                SprJson.NullableNumber(w,"projectileReleaseMs",SprTiming.Release(Of("attack")));
            }
            w.WriteStartObject("castReleaseMs");
            foreach(string label in new[]{"spellDir","spellNoDir"})SprJson.NullableNumber(w,label,SprTiming.Release(Of(label)));
            w.WriteEndObject();
            SprCreature.Sequences(w,sequences,s=>SprCreature.PlayerRule(s.Label,weapon)==SprCreature.Duration.Floor);
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
    }

    /// <summary>stepMs of a creature: walk ×40; without a walk, the breath length truncated (sprites.md §4.3, gfx 85 and 1094).</summary>
    static int StepMs(SprEntry entry){
        if(entry.Action(0) is {} walk)return SprTiming.Times40(walk);
        if(entry.Action(3) is {} breath)return (int)SprTiming.Exact(breath);
        throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz 的 gfx {entry.Gfx} 沒有 walk 也沒有 breath，無法決定 stepMs",AssetPrepInput.TextContainer);
    }

    /// <summary>Monster / NPC / world appearance: the creature actions the entry has.</summary>
    static byte[] Monster(AssetRecipeItemContext context,SprEntry entry){
        var actions=SprCreature.MonsterActions.Select(a=>(a.Label,Action:entry.Action(a.Action))).Where(a=>a.Action!=null).Select(a=>(a.Label,Action:a.Action!)).ToArray();
        if(actions.Length==0)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz 的 gfx {entry.Gfx} 沒有任何生物動作（0、1、2、3、8、18、19）",AssetPrepInput.TextContainer);
        int stepMs=StepMs(entry);
        var body=new SprBody(context,entry.Sprite);
        var sequences=actions.SelectMany(a=>body.Sequences(a.Label,a.Action)).ToArray();
        return SprJson.Write(w=>{
            w.WriteStartObject();
            Head(w,entry);w.WriteNumber("sprite",entry.Sprite);w.WriteNumber("stepMs",stepMs);
            w.WriteStartObject("actionMs");
            foreach(var (label,action) in actions)SprCreature.WriteDuration(w,label,action,SprCreature.MonsterRule(label));
            w.WriteEndObject();
            w.WriteString("entryHash",entry.Sha256);
            SprCreature.Sequences(w,sequences,_=>false);
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
    }

    /// <summary>Magic effect: action 0 with the gfx's label, stepMs 1, floor duration, steps with milliseconds.</summary>
    static byte[] Magic(AssetRecipeItemContext context,SprEntry entry){
        string label=MagicLabels.GetValueOrDefault(entry.Gfx,"effect");
        var action=SprCreature.Require(entry,0,label);
        var body=new SprBody(context,entry.Sprite);
        Func<int,int>? direction=MagicDirectionModulo.TryGetValue(entry.Gfx,out int modulo)?d=>d%modulo:null;
        var sequences=body.Sequences(label,action,direction);
        return SprJson.Write(w=>{
            w.WriteStartObject();
            Head(w,entry);w.WriteNumber("sprite",entry.Sprite);w.WriteNumber("stepMs",MagicStepMs);
            if(AdditiveMagic.Contains(entry.Gfx))w.WriteNumber("effectTileType",AdditiveTileType);
            w.WriteStartObject("actionMs");w.WriteNumber(label,SprTiming.Floor(action));w.WriteEndObject();
            w.WriteString("entryHash",entry.Sha256);
            SprJson.Sequences(w,sequences,_=>true);
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
    }

    /// <summary>Projectile: action 0 "fly"; ProjectileRendering reads the frames, the sequences name the keys per direction.</summary>
    static byte[] Projectile(AssetRecipeItemContext context,SprEntry entry){
        var action=SprCreature.Require(entry,0,"fly");
        var body=new SprBody(context,entry.Sprite);
        var sequences=body.Sequences("fly",action);
        return SprJson.Write(w=>{
            w.WriteStartObject();
            Head(w,entry);w.WriteNumber("sprite",entry.Sprite);
            w.WriteString("entryHash",entry.Sha256);
            SprJson.Sequences(w,sequences,_=>false);
            SprJson.Frames(w,body);
            w.WriteEndObject();
        });
    }

    /// <summary>
    /// world-234: breath of the body sprite with its 105.clothes sprites drawn in. Each frame "&lt;sid&gt;:&lt;frame&gt;" is the body's
    /// frame followed by the same file and frame of every clothes sprite in field order; bounds = union of the non-empty frames;
    /// source_order renumbered. A clothes sprite without that file or frame stops the item.
    /// </summary>
    static byte[] Composite(AssetRecipeItemContext context,SprEntry entry){
        var breath=SprCreature.Require(entry,3,"breath");
        var field=entry.Field(ClothesField);
        if(field is not {Count:>1}||field[0]!=field.Count-1||field.Skip(1).Any(v=>v is <0 or >ushort.MaxValue))
            throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz 的 gfx {entry.Gfx} 沒有可用的 {ClothesField}.clothes 欄位（應為「件數 sprite…」）",AssetPrepInput.TextContainer);
        int stepMs=StepMs(entry);
        var body=new SprBody(context,entry.Sprite);
        var layers=field.Skip(1).Select(sprite=>new SprBody(context,sprite)).ToArray();
        var sequences=body.Sequences("breath",breath);
        return SprJson.Write(w=>{
            w.WriteStartObject();
            Head(w,entry);w.WriteNumber("sprite",entry.Sprite);w.WriteNumber("stepMs",stepMs);
            w.WriteStartObject("actionMs");SprCreature.WriteDuration(w,"breath",breath,SprCreature.MonsterRule("breath"));w.WriteEndObject();
            w.WriteString("entryHash",entry.Sha256);
            SprCreature.Sequences(w,sequences,_=>false);
            w.WriteStartArray("frames");
            foreach(int sid in body.Sids.ToArray()){
                var file=body.File(sid);
                for(int f=0;f<file.Frames.Count;f++){
                    var parts=new List<(SprFile File,SprFrame Frame)>{(file,file.Frames[f])};
                    foreach(var layer in layers){
                        var other=layer.File(sid);
                        if(f>=other.Frames.Count)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"gfx {entry.Gfx}：{SprBody.FileName(layer.Sprite,sid)} 沒有第 {f} 幀（clothes 疊圖需要與 body 相同的幀）");
                        parts.Add((other,other.Frames[f]));
                    }
                    var drawn=parts.Where(p=>!p.Frame.Empty).ToArray();
                    int[] bounds=drawn.Length==0?file.Frames[f].Bounds:[drawn.Min(p=>p.Frame.Bounds[0]),drawn.Min(p=>p.Frame.Bounds[1]),drawn.Max(p=>p.Frame.Bounds[2]),drawn.Max(p=>p.Frame.Bounds[3])];
                    w.WriteStartObject();
                    w.WriteString("key",SprJson.Key(sid,f));
                    w.WriteStartArray("bounds");foreach(int v in bounds)w.WriteNumberValue(v);w.WriteEndArray();
                    w.WriteStartArray("blocks");
                    int order=0;
                    foreach(var (part,frame) in parts)foreach(var tile in frame.Tiles)SprJson.Block(w,tile,order++,part.Blocks[tile.BlockIndex]);
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
            }
            w.WriteEndArray();
            w.WriteEndObject();
        });
    }
}
