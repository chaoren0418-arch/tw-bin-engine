using System.Buffers.Binary;
using P06.NativeUi;
namespace P06.AssetPrep;

// T451 (#1036): in-memory checks run by --module-tests (name asset-prep): recipe discovery, path rules and the PNG
// container. Nothing is read or written on disk here; the file-system behaviour (staging, lock, resume, publish, junctions)
// is covered by the DemoChecks group in engine/_integration/dev/asset-prep-tests/ with synthetic idx / pak / TBT.
/// <summary>Invalid registrations for the discovery check; excluded from the product scan.</summary>
[ExcludeFromDiscovery]
static class AssetPrepTestFixtures {
    [AssetRecipe] internal sealed class NotARecipe{}
    [AssetRecipe] internal sealed class BadId:IAssetRecipe{
        public string Id=>"../escape";public int Version=>1;public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
        public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>items;
        public void Produce(string item,AssetRecipeItemContext context){}
    }
    [AssetRecipe] internal sealed class NoDefaultConstructor(int unused):IAssetRecipe{
        public string Id=>"needs-argument"+unused;public int Version=>1;public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
        public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>items;
        public void Produce(string item,AssetRecipeItemContext context){}
    }
    [AssetRecipe] internal sealed class SecondInventory:IAssetRecipe{
        public string Id=>InventoryIconRecipe.RecipeId;public int Version=>1;public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
        public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>items;
        public void Produce(string item,AssetRecipeItemContext context){}
    }
}

static class AssetPrepModuleTests {
    static bool Refused(Action action,AssetPrepFailure failure){
        try{action();return false;}catch(AssetPrepException ex){return ex.Failure==failure;}
    }

    [WorldModuleTest("asset-prep")]
    static void Run(WorldModuleTestContext t){
        // --- Discovery: the product assembly registers its recipes by attribute only.
        var product=AssetRecipeDiscovery.Product;
        t.Check(product.Errors.Count==0,"[AssetRecipe] registrations are all valid",product.Errors);
        t.Check(product.Recipes.Count(r=>r.Id==InventoryIconRecipe.RecipeId)==1,"inventory recipe discovered once");
        t.Check(product.Recipes.Select(r=>r.Id).SequenceEqual(product.Recipes.Select(r=>r.Id).Order(StringComparer.Ordinal)),"recipes ordered by id");
        t.Check(product.Recipes.All(r=>AssetPrepPaths.IsRecipeId(r.Id)&&r.Version>0),"every recipe has a valid id and version");
        var inventory=product.Recipes.FirstOrDefault(r=>r.Id==InventoryIconRecipe.RecipeId);
        t.Check(inventory is {Coverage:AssetRecipeCoverage.Partial},"inventory recipe is partial (no ready state)");
        t.Check(AssetPrepRunner.Product().RecipeIds.Contains(InventoryIconRecipe.RecipeId),"product runner knows the inventory recipe");
        // Invalid registrations are left out and reported; [ExcludeFromDiscovery] keeps these fixtures out of the product scan.
        var fixtures=AssetRecipeDiscovery.Discover(new[]{typeof(AssetPrepTestFixtures.NotARecipe),typeof(AssetPrepTestFixtures.BadId),typeof(AssetPrepTestFixtures.NoDefaultConstructor),typeof(InventoryIconRecipe),typeof(AssetPrepTestFixtures.SecondInventory)});
        t.Equal(1,fixtures.Recipes.Count,"fixture scan keeps only the valid recipe");
        t.Equal(4,fixtures.Errors.Count,"fixture scan reports non-recipe, bad id, missing constructor and duplicate id");

        // --- Path rules.
        foreach(string id in new[]{"inventory","maps-r1","a","0"})t.Check(AssetPrepPaths.IsRecipeId(id),"recipe id accepted: "+id);
        foreach(string id in new[]{"","..",".","../inventory","a/b","a\\b","A","a_b","a.b","-a","a-","a--b","con","nul","inventory\n","a b","C:"})
            t.Check(!AssetPrepPaths.IsRecipeId(id),"recipe id refused: "+id.Replace("\n","\\n"));
        t.Check(Refused(()=>new AssetPrepLayout("relative/engine"),AssetPrepFailure.InvalidRequest),"relative engine root refused");
        t.Equal("a/b/1.png",AssetPrepPaths.SafeRelative("a\\b/1.png"),"relative output path normalised to '/'");
        foreach(string path in new[]{"","..","../x.png","a/../x.png","a/..\\..\\x.png","/x.png","\\x.png","\\\\server\\share\\x.png","C:\\x.png","C:x.png","x.png:stream","a//b.png","a/./b.png","x.png.","x .png","nul","NUL.png","a/con.txt/b",".hidden","a/b/"})
            t.Check(Refused(()=>AssetPrepPaths.SafeRelative(path),AssetPrepFailure.UnsafePath),"output path refused: "+path);
        t.Check(AssetPrepPaths.IsItemToken("124")&&!AssetPrepPaths.IsItemToken("../124")&&!AssetPrepPaths.IsItemToken("1..2")&&!AssetPrepPaths.IsItemToken("")&&!AssetPrepPaths.IsItemToken("1/2"),"item tokens are never paths");

        // --- Inventory items: plain icon ids only, canonical order, duplicates folded.
        var recipe=new InventoryIconRecipe();
        t.Check(recipe.Normalize(new[]{"124","7","40","7"}).SequenceEqual(new[]{"7","40","124"}),"inventory items sorted numerically without duplicates");
        foreach(string item in new[]{"0","01","-1","+1","1.0","65536","999999","1e3","abc"," 1","1 ",""})
            t.Check(Refused(()=>recipe.Normalize(new[]{item}),AssetPrepFailure.InvalidRequest),"inventory item refused: \""+item+"\"");
        t.Check(InventoryIconRecipe.ParseIcon("65535")==65535&&InventoryIconRecipe.FileName(124)=="124.png","inventory icon range and file name");

        // --- PNG container: signature, IHDR, one stored IDAT, IEND; pixels in R, G, B, A order.
        uint[] pixels=[0xFF102030u,0x00000000u,0x80FFEEDDu,0xFF000001u,0xFFFFFFFFu,0x01020304u];
        byte[] png=PngWriter.Encode(3,2,pixels);
        t.Check(png.AsSpan(0,8).SequenceEqual(new byte[]{0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A}),"PNG signature");
        t.Check(BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(8))==13&&png.AsSpan(12,4).SequenceEqual("IHDR"u8)
            &&BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16))==3&&BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20))==2&&png[24]==8&&png[25]==6&&png[28]==0,"PNG IHDR 3×2, 8-bit RGBA, no interlace");
        int idat=33,length=(int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(idat));
        int raw=2*(1+3*4);
        t.Check(png.AsSpan(idat+4,4).SequenceEqual("IDAT"u8)&&length==2+5+raw+4,"PNG IDAT holds one stored deflate block");
        var body=png.AsSpan(idat+8,length);
        t.Check(body[0]==0x78&&body[1]==0x01&&body[2]==1&&BinaryPrimitives.ReadUInt16LittleEndian(body[3..])==raw&&BinaryPrimitives.ReadUInt16LittleEndian(body[5..])==(ushort)~raw,"zlib header and final stored block length");
        t.Check(body[7]==0&&body.Slice(8,4).SequenceEqual(new byte[]{0x10,0x20,0x30,0xFF})&&body.Slice(16,4).SequenceEqual(new byte[]{0xFF,0xEE,0xDD,0x80})&&body[7+13]==0,"rows start with filter 0, pixels are R, G, B, A");
        t.Check(png.AsSpan(png.Length-12).SequenceEqual(new byte[]{0,0,0,0,0x49,0x45,0x4E,0x44,0xAE,0x42,0x60,0x82}),"PNG IEND with the standard CRC (CRC-32 table is right)");
        t.Check(PngWriter.Encode(3,2,pixels).AsSpan().SequenceEqual(png),"PNG encoding is deterministic");
        t.Throws<ArgumentException>(()=>PngWriter.Encode(3,2,new uint[5]),"PNG pixel count must match the size");

        // --- Same decoder as ItemIconSource: a synthetic .tbt keeps its offset and transparency through Rgba().
        byte[] tbt=[3,1,2,1, 1,0,2,0x00,0x7C,0x00,0x00];
        var image=TbtImage.Decode(tbt,"synthetic.tbt");
        byte[] rgba=PngWriter.Rgba(image.Argb);
        t.Check(image is {OffsetX:3,OffsetY:1,Width:2,Height:1}&&rgba.Length==8&&rgba[3]==0xFF&&rgba[0]==(byte)(UiImage.ToArgb(0x7C00)>>16)&&rgba.AsSpan(4,4).SequenceEqual(new byte[4]),"TBT pixel → opaque RGBA, value 0 → transparent");
    }
}
