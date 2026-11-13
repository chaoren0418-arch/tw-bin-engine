using System.Globalization;
namespace P06.AssetPrep;

// T455 (#1040): the preheat list of docs/design/asset-recipe.md 5 (decision D5, provisional: "keep it, equal to the members
// of the seven r29 baseline lists"). Integer numbers only — which appearances to decode first. It is not client data and
// not a substitute for anything: the server always names the gfx, and adding or removing a number changes only what is
// ready before it is first seen. Every manifest is still built from what the run actually prepared.
// The players pairs include the 15 the baseline lists as unavailable (L7): they are prepared and reported unavailable again.
public static class PreheatList {
    /// <summary>Player body gfx → stances (sprites.md §4.2; 42 profiles + 15 unavailable in the baseline).</summary>
    public static readonly IReadOnlyDictionary<int,int[]> PlayerStances=new SortedDictionary<int,int[]>{
        [0]=[0,4,20,46,50],[1]=[0,4,20,50],[37]=[0,4,20,50],[48]=[0,4,20,50],[61]=[0,4,20,50],[138]=[0,4,20,50],[734]=[0,4,20,50],
        [1186]=[0,4,20,50],[2786]=[0,4,20,50],[2796]=[0,4,20,50],[6650]=[0,4,20,50],[6658]=[0,4,20,50],[6661]=[0,4,20,50],[6671]=[0,4,20,50],
    };
    /// <summary>objects/manifest.json of the baseline (15).</summary>
    public static readonly int[] Objects=[0,98,143,148,237,460,695,792,931,936,945,971,1110,1307,4038];
    /// <summary>Stances of gfx 0 published by objects (objects/0-weapon-&lt;n&gt;.json).</summary>
    public static readonly int[] ObjectWeapons=[4,20,50];
    /// <summary>encountered-monsters.json of the baseline (19).</summary>
    public static readonly int[] Monsters=[29,31,32,52,54,95,929,938,1108,1184,1206,1477,1540,3101,3783,3864,3918,5065,6325];
    /// <summary>world-appearances.json of the baseline (6). 234 and 945 come from objects/world-&lt;gfx&gt;.json.</summary>
    public static readonly int[] World=[85,234,834,945,1002,1094];
    /// <summary>World appearances objects publishes as world-&lt;gfx&gt; (the clothes composite and the world variant of 945).</summary>
    public static readonly int[] ObjectWorld=[234,945];
    public static readonly int[] Magic=[167,189,191,197,219,221,2165,11722];
    public static readonly int[] Projectiles=[66];
    public static readonly int[] Shops=[0,37,61,138,734,1186,2786,6650,6658,6661,6671];
    public static readonly int[] Shadows=[0,1,37,48,61,98,234,695,734,792,1186,2786,2796,6650,6658,6661,6671];
    public static readonly int[] Doors=[2346];

    static string S(int v)=>v.ToString(CultureInfo.InvariantCulture);

    /// <summary>The preheat request of one SPR recipe (manifests included), in that recipe's own item names; empty for other recipes.</summary>
    public static IReadOnlyList<string> Items(string recipeId){
        var items=new List<string>();
        switch(recipeId){
            case ObjectsRecipe.RecipeId:
                // Objects plain items cover the objects list and the plain-file monsters and world appearances.
                items.AddRange(Objects.Concat(Monsters).Concat(World.Except(ObjectWorld)).Distinct().Order().Select(S));
                items.AddRange(ObjectWeapons.Select(w=>$"0-weapon-{S(w)}"));
                items.AddRange(ObjectWorld.Select(g=>"world-"+S(g)));
                items.AddRange(Magic.Select(g=>"magic-"+S(g)));
                items.AddRange(Projectiles.Select(g=>"projectile-"+S(g)));
                items.Add(ObjectsRecipe.ManifestItem);
                break;
            case PlayersRecipe.RecipeId:
                foreach(var (gfx,stances) in PlayerStances)items.AddRange(stances.Select(w=>$"{S(gfx)}-{S(w)}"));
                items.Add(SprManifest.ManifestItem);
                break;
            case ShopsRecipe.RecipeId:items.AddRange(Shops.Select(S));items.Add(SprManifest.ManifestItem);break;
            case ShadowsRecipe.RecipeId:items.AddRange(Shadows.Select(S));items.Add(SprManifest.ManifestItem);break;
            case DoorsRecipe.RecipeId:items.AddRange(Doors.Select(S));items.Add(SprManifest.ManifestItem);break;
            // T463: one item; which stances get a pickup profile follows the players manifest, not this list.
            case PickupRecipe.RecipeId:items.Add(PickupRecipe.Item);break;
            case AppearanceListsRecipe.RecipeId:
                items.AddRange(Monsters.Select(g=>"monster-"+S(g)));
                items.AddRange(World.Select(g=>"world-"+S(g)));
                items.Add(AppearanceListsRecipe.MonstersItem);items.Add(AppearanceListsRecipe.WorldItem);
                break;
        }
        return items;
    }
}
