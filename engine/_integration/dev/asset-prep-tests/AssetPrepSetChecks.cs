using P06;
using P06.AssetPrep;
using P06.NativeUi;
using System.Text;
using System.Text.Json;

// T467 (#1052): real thirteen recipes, entirely synthetic client bytes. No game/client/DB/network is opened.
// Registered in an existing CI group; no Program.cs, csproj or registry edits. Only CI executes this code.
partial class Checks {
    sealed record SetFixture(string Client,AssetPrepPins Pins,AssetPrepSetPins SetPins,IAssetRecipe[] Recipes);

    static SetFixture SetClient(string root){
        Directory.CreateDirectory(root);
        // All preheated gfx use algorithmically generated sprites. gfx 138 deliberately has out-of-bounds pixels (L7).
        var gfx=PreheatList.Objects.Concat(PreheatList.Monsters).Concat(PreheatList.World).Concat(PreheatList.Magic)
            .Concat(PreheatList.Projectiles).Concat(PreheatList.Shops).Concat(PreheatList.Shadows).Concat(PreheatList.Doors)
            .Concat(PreheatList.PlayerStances.Keys).Distinct().Order().ToArray();
        var list=new StringBuilder("S1223\r\n");
        foreach(int g in gfx){
            list.Append('#').Append(g).Append("\t96=").Append(g==138?30003:30000).Append(" synthetic");
            foreach(int action in new[]{0,1,2,3,4,5,6,7,8,18,19,20,21,22,23,46,47,48,49,50,51,52,53,70}){
                int sid=action==3?8:0;
                string events=action is 1 or 5 or 18 or 19 or 21 or 47 or 51?"!":"";
                list.Append(' ').Append(action).Append(".action(1 1,").Append(sid).Append(".0:4").Append(events).Append(')');
            }
            list.Append(" 15.get(1 4,0.0:1 88.0:1 88.1:4<206 88.2:4) 28.open(0 1,0.1:4) 29.close(0 1,0.0:4) 101.shadow(30001)");
            if(g==234)list.Append(" 105.clothes(1 30002)");
            list.Append("\r\n");
        }
        var sprites=new List<(int,string,byte[])>();
        foreach(int sprite in new[]{30000,30001,30002,30003})
            foreach(int sid in Enumerable.Range(0,16).Concat(Enumerable.Range(88,8))){
                var spr=sprite==30003?SrMake(sprite,sid,6):SbWide(sprite,sid,6);
                sprites.Add((0,$"{sprite}-{sid}.spr",SrEncode(spr)));
            }
        SrClient(root,list.ToString(),sprites);
        byte[] plain=Encoding.ASCII.GetBytes(list.ToString()),desc=TxBig5("3\r\n測試甲\r\n測試乙\r\n測試丙\r\n"),spell=TxBig5("1\r\n0 測試術 (1/0)\r\n");
        TxArchive(root,("list.spz",SpriteList.EncodeSpz(plain)),("list.spr",plain),("desc-c.tbl",desc),("spelldesc-c.tbl",spell),("test-c.html",TxBig5("<html><body>測試</body></html>")));
        byte[] s32=SeS32(i=>(1u<<8)|(uint)(i%2),[],[],[],[9999]);
        string map=Path.Combine(root,"map","0");Directory.CreateDirectory(map);
        foreach(string name in MapsRecipe.CacheBlocks)File.WriteAllBytes(Path.Combine(map,name+".s32"),s32);
        // An unsupported map exercises D3, and map/6 is SEG-only so the recipes correctly skip it.
        Directory.CreateDirectory(Path.Combine(root,"map","12004"));File.WriteAllBytes(Path.Combine(root,"map","12004","7fff8000.s32"),[1,2,3]);
        byte[] seg=[1,3,5,7];Directory.CreateDirectory(Path.Combine(root,"map","6"));File.WriteAllBytes(Path.Combine(root,"map","6","7fff8000.seg"),seg);
        var lac=UiResourcePins.Tile.AnchorList.Select((a,i)=>(a.Entry,Data:new byte[]{(byte)(i+1),7,9})).ToArray();
        Archive(root,"Tile",[("1.til",SeTil(SeDiamond(0,1),SeDiamond(1,2))),("140.til",SeTil(SeDiamond(0,3))),..lac]);
        var pins=new AssetPrepPins(Sha(plain)){ListSprSha256=Sha(plain)};
        var setPins=new AssetPrepSetPins{DescSha256=Sha(desc),SpellDescSha256=Sha(spell),SegSha256=Sha(seg),Tile=lac.Select(x=>new ClientArchiveAnchor(x.Entry,Sha(x.Data))).ToArray()};
        return new(root,pins,setPins,AssetRecipeDiscovery.Product.Recipes.Select(r=>r.Id=="text"?(IAssetRecipe)new TextRecipe(Sha(desc)):r).ToArray());
    }

    // Fault injection delegates to the REAL recipe, including its normalization, dependencies and source declaration.
    sealed class SetFault(IAssetRecipe inner,Action<string,AssetRecipeItemContext>? before=null,int version=0):IAssetRecipe,IAssetRecipeSources {
        public string Id=>inner.Id;public int Version=>inner.Version+version;public AssetRecipeCoverage Coverage=>inner.Coverage;
        public AssetPrepSources Sources=>(inner as IAssetRecipeSources)?.Sources??AssetPrepSources.Sprites;
        public IReadOnlyList<string> Requires=>inner.Requires;
        public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>inner.Normalize(items);
        public bool DependsOnRequest(string item)=>inner.DependsOnRequest(item);
        public void Produce(string item,AssetRecipeItemContext context){before?.Invoke(item,context);inner.Produce(item,context);}
    }

    [CheckGroup("asset-prep-set",Order=1),CheckGroup("native-ui-resources",Order=66),CheckGroup(CheckGroups.Default)]
    static void AssetPrepSetChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1052","run-"+Guid.NewGuid().ToString("N"));
        var fixture=SetClient(Path.Combine(root,"client-a"));var other=SetClient(Path.Combine(root,"客戶端 乙"));
        string clientHash=ApTreeHash(fixture.Client);
        AssetPrepSetRequest Request(string engine)=>new(fixture.Client,engine){Pins=fixture.Pins,SetPins=fixture.SetPins,EstimatedOutputBytes=16*1024*1024,SpaceReserveBytes=1024*1024,AvailableSpace=_=>long.MaxValue};
        string Active(string engine)=>Path.Combine(engine,"assets");
        var runner=new AssetPrepOrchestrator(fixture.Recipes);
        string a=Path.Combine(root,"engine-a"),b=Path.Combine(root,"引擎 乙");
        var progress=new List<AssetPrepProgress>();
        var first=runner.Run(Request(a) with{Progress=p=>progress.Add(p)});
        var second=new AssetPrepOrchestrator(other.Recipes).Run(Request(b) with{ClientRoot=other.Client,Pins=other.Pins,SetPins=other.SetPins});
        Check(first.Outcome==AssetPrepOutcome.Published&&second.Outcome==AssetPrepOutcome.Published&&first.Receipt.State=="ready","SET real thirteen recipes publish ready");
        Check(first.Receipt.Recipes.Select(r=>r.Id).SequenceEqual(AssetPrepOrchestrator.RecipeOrder)&&first.Receipt.Recipes.Count==13&&!first.Receipt.Recipes.Any(r=>r.Id=="inventory"),"SET contract order and exact set; inventory excluded");
        Check(first.Receipt.Inputs.Count==19&&first.Receipt.Inputs.Any(c=>c.Name=="Text")&&first.Receipt.Inputs.Any(c=>c.Name=="Tile")&&first.Receipt.Anchors.Count==8,"SET 17 Sprite + Text + Tile identities and actual anchors");
        Check(ApTreeHash(Active(a))==ApTreeHash(Active(b))&&Sha(first.Receipt.ToJson())==Sha(second.Receipt.ToJson()),"SET two distinct client/output directories: all outputs, receipts and integrity byte-identical");
        Check(first.Receipt.KnownLimits.Single(l=>l.Id=="L3").Items.SequenceEqual(["0"])&&first.Receipt.KnownLimits.Single(l=>l.Id=="L6").Count==1&&first.Receipt.KnownLimits.Single(l=>l.Id=="L7").Count==4&&first.Receipt.KnownLimits.Single(l=>l.Id=="D3").Items.SequenceEqual(["12004"]),"SET actual known-limit counts do not block ready");
        Check(progress.Where(p=>p.Phase=="prepare").Select(p=>p.Recipe).Distinct().SequenceEqual(AssetPrepOrchestrator.RecipeOrder)&&progress.Zip(progress.Skip(1)).All(p=>p.First.Completed<=p.Second.Completed)&&progress[^1].Completed==progress[^1].Total&&progress.Any(p=>p.EstimatedRequiredBytes>0),"SET category, monotonic completed count and space progress");
        foreach(string path in Directory.GetFiles(Active(a),"*.json",SearchOption.AllDirectories)){
            string text=File.ReadAllText(path);
            Check(!text.Contains(root,StringComparison.Ordinal)&&!text.Contains(JsonSerializer.Serialize(root)[1..^1],StringComparison.Ordinal),"SET no machine path: "+Path.GetRelativePath(Active(a),path));
        }
        Assets.VerifyIntegrity(Active(a));
        var assets=new Assets(Active(a));Check(assets.Frames.Count==96&&assets.Sequences.Count==16,"SET product Assets loads shared tree through v2 gate");
        var profiles=PlayerProfiles.LoadManifest(Active(a));
        Check(PlayerProfiles.Load(Active(a),profiles[(61,20)]).Gfx==61,"SET PlayerProfiles reads manifest and appearance SHA");
        Check(new PlayerShadowCatalog(Active(a)).Get(0,30000,"0:0")!=null,"SET PlayerShadows reads manifest and frame SHA");
        Check(new ObjectAssets(Active(a)).GetShop(61)?.Gfx==61,"SET ObjectAssets reads shared shop manifest and SHA");
        _=new MapEffectCatalog(Path.Combine(Active(a),"maps"));
        Check(new PacketEffectAssets(Active(a)).Get(189)!=null,"SET MapEffects reads objects/magic SHA");
        Check(new OriginalMapStore(Active(a),0,clientRoot:fixture.Client).Available(0x7ffd,0x8002),"SET OriginalMapCache provider reads maps manifest and terrain SHA");
        var portalType=typeof(Assets).Assembly.GetType("P06.PortalCatalog",throwOnError:true)!;
        _=Activator.CreateInstance(portalType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[Active(a),0,fixture.Client],null);
        Check(true,"SET Portals reads maps manifest SHA without game or network");
        var integrity=Json.Read(Path.Combine(Active(a),"integrity.json"));
        var listed=integrity.GetProperty("files").EnumerateArray().Select(f=>f.S("path")).ToArray();
        Check(listed.SequenceEqual(listed.Order(StringComparer.Ordinal))&&listed.Count(p=>p.StartsWith("receipts/",StringComparison.Ordinal))==13&&!listed.Contains("prep-receipt.json")&&!listed.Contains("integrity.json"),"SET ordinal integrity includes all recipe receipts, no circular hashes");
        foreach(string path in new[]{"objects/names.json","objects/manifest.json","players/manifest.json","shadows/manifest.json","maps/manifest.json","maps/effects/manifest.json","cache/manifest.json"})
            Check(integrity.GetProperty("files").EnumerateArray().Single(f=>f.S("path")==path).S("sha256")==Sha(File.ReadAllBytes(Path.Combine(Active(a),path))),"SET existing reader path/SHA contract: "+path);

        string before=ApTreeHash(Active(a));DateTime stamp=File.GetLastWriteTimeUtc(Path.Combine(Active(a),"integrity.json"));
        var again=runner.Run(Request(a));
        Check(again.Outcome==AssetPrepOutcome.UpToDate&&again.Produced==0&&again.Reused==first.Produced&&File.GetLastWriteTimeUtc(Path.Combine(Active(a),"integrity.json"))==stamp,"SET unchanged rerun decodes nothing, active not rewritten");

        // Every recipe can fail, including the last. Prior active always survives, byte for byte.
        foreach(string id in AssetPrepOrchestrator.RecipeOrder){
            var failing=new AssetPrepOrchestrator(fixture.Recipes.Select(r=>r.Id==id?new SetFault(r,(_,_)=>throw new AssetPrepException(AssetPrepFailure.CorruptEntry,"injected "+id),version:1):(IAssetRecipe)r));
            ApRefused(()=>failing.Run(Request(a)),AssetPrepFailure.CorruptEntry,"SET failure in "+id);
            Check(ApTreeHash(Active(a))==before,"SET "+id+" failure never replaces active");
            runner.Run(Request(a));
        }

        // Cancel after one durable text item. Retry must reuse it, then finish the other twelve categories.
        string resume=Path.Combine(root,"resume");using(var cancel=new CancellationTokenSource()){
            bool cancelled=false;
            try{runner.Run(Request(resume) with{Cancel=cancel.Token,Progress=p=>{if(p.Completed==1)cancel.Cancel();}});}
            catch(OperationCanceledException){cancelled=true;}
            Check(cancelled&&!Directory.Exists(Active(resume)),"SET cancellation before first publication leaves no active tree");
        }
        var resumed=runner.Run(Request(resume));
        Check(resumed.Reused==1&&resumed.Produced==first.Produced-1&&ApTreeHash(Active(resume))==before,"SET resume reuses the completed item and is identical to a clean run");

        // Free-space preflight, volume becoming full at assembly, and physical write failure all keep active intact.
        ApRefused(()=>runner.Run(Request(a) with{AvailableSpace=_=>0}),AssetPrepFailure.DiskSpace,"SET no free disk");
        long space=long.MaxValue;
        ApRefused(()=>runner.Run(Request(a) with{AvailableSpace=_=>space,Progress=p=>{if(p.Completed==p.Total)space=0;}}),AssetPrepFailure.DiskSpace,"SET full disk before assembly");
        Check(ApTreeHash(Active(a))==before,"SET disk shortage preserves active");
        Check(runner.Run(Request(a)).Produced==0,"SET disk retry reuses every completed item");

        using(var cancel=new CancellationTokenSource()){
            bool cancelled=false;
            try{runner.Run(Request(a) with{Cancel=cancel.Token,Progress=p=>{if(p.Phase=="publish")cancel.Cancel();}});}
            catch(OperationCanceledException){cancelled=true;}
            Check(cancelled&&ApTreeHash(Active(a))==before,"SET cancel at last publication gate preserves active");
        }
        ApRefused(()=>runner.Run(Request(a) with{Progress=p=>{
            if(p.Phase=="publish")File.AppendAllText(Path.Combine(new AssetPrepLayout(a).SetCandidate,"objects","0.json")," ");
        }}),AssetPrepFailure.Verification,"SET candidate tamper at last publication gate");
        Check(ApTreeHash(Active(a))==before,"SET failed final verification preserves active");
        Check(runner.Run(Request(a)).Produced==0,"SET cancelled or damaged assembly resumes without decoding");

        // Source/output tamper invalidates the affected checkpoint, including its aggregate manifest.
        var layout=new AssetPrepLayout(a);
        File.AppendAllText(Path.Combine(layout.Staging("objects"),"objects","0.json")," ");
        var repaired=runner.Run(Request(a));
        Check(repaired.Produced==2&&repaired.Reused==first.Produced-2&&ApTreeHash(Active(a))==before,"SET corrupt object checkpoint rebuilds just the object and its manifest");

        // Interrupted first rename: recovery restores the old complete tree BEFORE attempting new work.
        Directory.Move(Active(a),layout.SetPrevious);
        ApRefused(()=>runner.Run(Request(a) with{AvailableSpace=_=>0}),AssetPrepFailure.DiskSpace,"SET interrupted swap recovery then failure");
        Check(ApTreeHash(Active(a))==before&&!Directory.Exists(layout.SetPrevious),"SET crash between renames recovers previous active");
        // Interrupted second rename: verified active wins and the backup is removed by the next run.
        Directory.Move(Active(b),new AssetPrepLayout(b).SetPrevious);
        Directory.Move(Active(resume),Active(b));
        Check(runner.Run(Request(b)).Outcome==AssetPrepOutcome.UpToDate&&!Directory.Exists(new AssetPrepLayout(b).SetPrevious),"SET crash after second rename accepts verified active");

        // Runtime accepts v1 unchanged, rejects an unready or modified v2 receipt, still skips lazy maps.
        string receiptPath=Path.Combine(Active(a),"prep-receipt.json"),integrityPath=Path.Combine(Active(a),"integrity.json");
        byte[] receiptBytes=File.ReadAllBytes(receiptPath),integrityBytes=File.ReadAllBytes(integrityPath);
        void RuntimeReject(string label){bool rejected=false;try{Assets.VerifyIntegrity(Active(a));}catch(InvalidDataException){rejected=true;}Check(rejected,label);}
        File.AppendAllText(receiptPath," ");RuntimeReject("SET runtime refuses receipt SHA mismatch");
        File.WriteAllBytes(receiptPath,(first.Receipt with{State="partial"}).ToJson());
        var doc=System.Text.Json.Nodes.JsonNode.Parse(integrityBytes)!;doc["receiptSha256"]=Sha(File.ReadAllBytes(receiptPath));File.WriteAllText(integrityPath,doc.ToJsonString());
        RuntimeReject("SET runtime refuses partial even with matching SHA");
        File.WriteAllBytes(receiptPath,receiptBytes);File.WriteAllBytes(integrityPath,integrityBytes);
        string legacy=Path.Combine(root,"legacy");Directory.CreateDirectory(legacy);File.WriteAllText(Path.Combine(legacy,"integrity.json"),"{\"schema\":\"p06-assets-integrity-v1\",\"files\":[]}");Assets.VerifyIntegrity(legacy);
        File.AppendAllText(Path.Combine(Active(a),"maps","manifest.json")," ");Assets.VerifyIntegrity(Active(a));
        Check(runner.Run(Request(a)).Outcome==AssetPrepOutcome.Published&&ApTreeHash(Active(a))==before,"SET lazy maps stay lazy at runtime; set verifier repairs changed map bytes");

        // One-writer lock also blocks the library runner; junctions are never followed or replaced.
        using(var held=AssetPrepLock.Acquire(layout.SetLock,"test")){
            ApRefused(()=>runner.Run(Request(a)),AssetPrepFailure.Busy,"SET concurrent set writer");
            ApRefused(()=>new AssetPrepRunner(fixture.Recipes).Run(new(fixture.Client,a,"text",TextRecipe.AllItems){Pins=fixture.Pins}),AssetPrepFailure.Busy,"SET concurrent library writer");
        }
        ApRefused(()=>new AssetPrepRunner(fixture.Recipes).Run(new(fixture.Client,a,"text",TextRecipe.AllItems){Pins=fixture.Pins}),AssetPrepFailure.InvalidRequest,"SET library runner cannot partially overwrite a shared set");
        string junctionEngine=Path.Combine(root,"junction-engine");Directory.CreateDirectory(junctionEngine);
        if(!ApJunction(Active(junctionEngine),Active(a)))throw new Exception("SET could not create test junction");
        try{ApRefused(()=>runner.Run(Request(junctionEngine)),AssetPrepFailure.UnsafePath,"SET active junction");Check(ApTreeHash(Active(a))==before,"SET junction target untouched");}
        finally{Directory.Delete(Active(junctionEngine),false);}
        Check(ApTreeHash(fixture.Client)==clientHash,"SET client is read-only throughout");
    }
}
