using P06;
using P06.AssetPrep;

// T477（#1073）：執行期按需外觀（ObjectAppearanceOnDemand）的離線測試。合成的 17 個 Sprite 容器與 list.spz 由
// SprRecipeChecks 的同一組工具產生（像素是依編號算出的測試值，不是原客戶端的圖），寫在 scratch/issue-1073/run-<時間>/ 下；
// 不讀客戶端、不需要遊戲、網路或 DB，也不提交任何幀或像素。
// 驗：清單外的 gfx 先是 pending、解完與「事先準備同一個 gfx」逐幀相同；解碼在背景執行緒、同一個 gfx 只解一次；
// 不可用的 gfx 明確標示原因且不重試；解碼快取鍵是（容器、offset、size、recipe 版本、list.spz SHA）；
// 執行期 context 拒絕寫檔；engine/assets（本 repo 的與測試自己準備的）在整個測試前後逐檔 SHA 不變。
partial class Checks {
    [CheckGroup("asset-prep-ondemand",Order=1),CheckGroup("native-ui-resources",Order=65),CheckGroup(CheckGroups.Default)]
    static void OnDemandAppearanceChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1073","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        string repoAssets=Path.Combine(Path.GetFullPath(repo),"engine","assets");
        string RepoAssetsHash()=>Directory.Exists(repoAssets)?ApTreeHash(repoAssets):"absent";
        string repoAssetsBefore=RepoAssetsHash();

        // --- 合成的動畫表：50、52 共用 sprite 51（52 只用到 50 已解過的檔，應全部命中快取）；53 沒有 walk（stepMs 取 breath）。
        const string list=
            "S1223\r\n"+
            "#50\t3=51 orc 0.walk(1 2,0.0:4 0.1:4) 1.attack(1 2,8.0:3 8.1:3!) 3.breath(0 1,16.0:6) 8.fly(0 2,24.0:3 24.1:5)\r\n"+
            "#52\t3=51 orc two 0.walk(1 2,0.0:5 0.1:5) 3.breath(0 1,16.0:7)\r\n"+
            "#53\t1=55 statue 3.breath(0 2,0.0:24 0.1:23)\r\n"+
            "#60\t1 conflict 3.breath(0 1,0.0:4)\r\n"+
            "#61\t1 nofile 3.breath(0 1,0.0:4)\r\n"+
            "#62\t1 corrupt 3.breath(0 1,0.0:4)\r\n"+
            "#63\t1 beyond 3.breath(0 1,0.9:4)\r\n"+
            "#70\t1 sign 70.act shop(1 1,0.0:4)\r\n"+
            "#234\t3=200 tree 3.breath(0 1,0.0:4) 105.clothes(1 201)\r\n";
        var files=new List<(int,string,byte[])>();
        void Put(int archive,int sprite,int sid,SrSpr spr)=>files.Add((archive,$"{sprite}-{sid}.spr",SrEncode(spr)));
        for(int sid=0;sid<8;sid++)Put(0,51,sid,SrMake(51,sid,2));                 // walk：主包
        for(int sid=8;sid<16;sid++)Put(3,51,sid,SrMake(51,sid,2));                // attack：只在替代包 Sprite02
        Put(1,51,16,SrMake(51,16,1));Put(7,51,16,SrMake(51,16,1));               // breath：兩個替代包內容相同（可用）
        Put(0,51,24,SrMake(51,24,3,emptyAt:2));                                   // die：含空幀
        Put(0,55,0,SrMake(55,0,2));
        files.Add((4,"60-0.spr",SrEncode(SrMake(60,0,1))));files.Add((9,"60-0.spr",SrEncode(SrMake(60,1,1))));   // 兩個替代包內容不同
        files.Add((0,"62-0.spr",[..SrEncode(SrMake(62,0,1)),0]));                 // 檔尾多一個 byte
        Put(0,63,0,SrMake(63,0,2));
        foreach(int sprite in new[]{200,201})Put(0,sprite,0,SrMake(sprite,0,1));
        string client=Path.Combine(root,"client");var pins=SrClient(client,list,files);
        string clientBefore=ApTreeHash(client);

        // --- 1. 事先準備同一批 gfx（objects recipe 的 JSON），當作比對基準。
        string engine=Path.Combine(root,"engine");
        var prepared=AssetPrepRunner.Product().Run(new AssetPrepRequest(client,engine,"objects",["50","52","53","manifest"]){Pins=pins});
        Check(prepared.Outcome==AssetPrepOutcome.Published,"OD baseline: objects recipe prepares 50, 52, 53");
        string objRoot=Path.Combine(engine,"assets","objects","objects");
        ObjectAppearance Prepared(int gfx)=>new(Path.Combine(objRoot,gfx+".json"));
        string engineBefore=ApTreeHash(engine);

        static bool Same(ObjectAppearance a,ObjectAppearance b,out string why){
            why="";
            if(a.Gfx!=b.Gfx||a.Sprite!=b.Sprite||a.StepMs!=b.StepMs){why=$"head {a.Gfx}/{a.Sprite}/{a.StepMs} vs {b.Gfx}/{b.Sprite}/{b.StepMs}";return false;}
            if(!a.ActionMs.OrderBy(k=>k.Key,StringComparer.Ordinal).SequenceEqual(b.ActionMs.OrderBy(k=>k.Key,StringComparer.Ordinal))){why="actionMs "+string.Join(",",a.ActionMs)+" vs "+string.Join(",",b.ActionMs);return false;}
            if(!a.Frames.Keys.Order(StringComparer.Ordinal).SequenceEqual(b.Frames.Keys.Order(StringComparer.Ordinal))){why="frame keys";return false;}
            foreach(var (key,fa) in a.Frames){
                var fb=b.Frames[key];
                if(fa.Key!=fb.Key||!fa.Bounds.SequenceEqual(fb.Bounds)||fa.Blocks.Length!=fb.Blocks.Length){why="frame "+key;return false;}
                for(int i=0;i<fa.Blocks.Length;i++){
                    var x=fa.Blocks[i];var y=fb.Blocks[i];
                    if(x.A!=y.A||x.B!=y.B||x.Z!=y.Z||x.Order!=y.Order||x.Tile.Type!=y.Tile.Type||!x.Tile.Pixels.SequenceEqual(y.Tile.Pixels)){why=$"frame {key} block {i}";return false;}
                }
            }
            if(!a.Sequences.Keys.Order().SequenceEqual(b.Sequences.Keys.Order())){why="sequence keys";return false;}
            foreach(var (k,sa) in a.Sequences)if(!sa.SequenceEqual(b.Sequences[k])){why=$"sequence {k}";return false;}
            // 同一個時間點取到同一幀（At 走 ActionMs／Duration 的整條時間路徑）。
            foreach(var (action,d) in a.Sequences.Keys)foreach(double t in new[]{0d,37.5,a.Duration(action)/2,a.Duration(action)*3})
                if(a.At(action,d,t).Key!=b.At(action,d,t).Key){why=$"At({action},{d},{t})";return false;}
            return true;
        }

        // --- 2. 先 pending、背景執行緒、同一個 gfx 只解一次；解完與事先準備逐幀相同。
        int testThread=Environment.CurrentManagedThreadId;var decodeThreads=new System.Collections.Concurrent.ConcurrentBag<int>();
        using var gate=new ManualResetEventSlim(false);
        using(var od=new ObjectAppearanceOnDemand(client,pins,g=>{decodeThreads.Add(Environment.CurrentManagedThreadId);gate.Wait(TimeSpan.FromSeconds(60));})){
            Check(od.State(50)==OnDemandState.None&&od.Request(50)==null&&od.State(50)==OnDemandState.Pending,"OD gfx outside the prepared list: first request returns null and the gfx is pending");
            Parallel.For(0,64,i=>{if(od.Request(i%2==0?50:52)!=null)throw new Exception("OD a pending gfx returned an appearance");});
            Check(od.State(52)==OnDemandState.Pending&&od.AttemptsFor(50)<=1&&od.AttemptsFor(52)==0,"OD 64 concurrent requests while the worker is held: still pending, nothing decoded twice");
            gate.Set();
            Check(od.Wait(50,TimeSpan.FromSeconds(60))&&od.Wait(52,TimeSpan.FromSeconds(60))&&od.State(50)==OnDemandState.Ready&&od.State(52)==OnDemandState.Ready,"OD 50 and 52 become ready after the worker runs");
            for(int i=0;i<10;i++){od.Request(50);od.Request(52);}
            Check(od.AttemptsFor(50)==1&&od.AttemptsFor(52)==1&&od.Attempts==2,"OD each gfx decoded exactly once (attempts "+od.Attempts+")");
            Check(decodeThreads.Count==2&&!decodeThreads.Contains(testThread),"OD decoding runs on a background worker, never on the requesting thread");
            foreach(int gfx in new[]{50,52}){
                var live=od.Request(gfx)!;
                Check(Same(live,Prepared(gfx),out string why),"OD gfx "+gfx+": on-demand appearance equals the prepared objects/"+gfx+".json frame by frame "+why);
                Check(ReferenceEquals(live,od.Request(gfx)),"OD gfx "+gfx+": the ready appearance is kept (same instance on every request)");
            }
            var m50=od.Request(50)!;
            Check(m50.StepMs==320&&m50.ActionMs["walk"]==320&&m50.ActionMs["attack"]==240&&Math.Abs(m50.ActionMs["breath"]-250)<1e-9&&Math.Abs(m50.ActionMs["die"]-8000d/24)<1e-9&&m50.Sequences.Values.All(s=>s.All(t=>t.Milliseconds==null)),"OD times per contract 3.2: stepMs/walk/attack ×40, breath/die exact, no step milliseconds");
            Check(m50.Frames["24:2"].Blocks.Length==0&&m50.Sequences[("attack",3)].Select(s=>s.Events).SequenceEqual(new[]{"","!"})&&m50.Sequences.Keys.Select(k=>k.Item1).Distinct().Order().SequenceEqual(new[]{"attack","breath","die","walk"}),"OD creature actions only, action 8 labelled die, empty frame kept, events copied");

            // 快取鍵：容器＋offset＋size＋recipe 版本＋list.spz SHA；52 用到的檔全部是 50 已解過的。
            var cache=od.Cache!;
            Check(cache.Decodes==18&&cache.Count==18&&cache.Hits==9,$"OD decode cache: 18 files decoded once for 50, 52 reuses 9 of them (decodes {cache.Decodes}, hits {cache.Hits})");
            Check(cache.Keys.All(k=>k.RecipeId=="objects"&&k.RecipeVersion==ObjectAppearanceOnDemand.RecipeVersion&&k.ListSpzSha256==pins.ListSpzSha256&&k.Size>0)&&cache.Keys.Select(k=>(k.Container,k.Offset)).Distinct().Count()==18,"OD cache key = (container, idx offset, idx size, recipe id/version, list.spz SHA-256), not the gfx");
            Check(cache.Keys.Count(k=>k.Container=="Sprite02")==8&&cache.Keys.Count(k=>k.Container=="Sprite")==9&&cache.Keys.Count(k=>k.Container=="Sprite00")==1,"OD main container first; files only in an alternate keyed by that alternate");
            var src=od.Sources(50);
            Check(src.Any(s=>s is {Container:"Text",Entry:"list.spz"}&&s.Sha256==pins.ListSpzSha256)&&src.Count(s=>s.Entry=="51-16.spr")==2,"OD sources: list.spz with its decoded SHA-256 and each .spr read (both identical alternates of 51-16)");

            // 53：沒有 walk → stepMs = breath 長度取整（sprites.md §4.3）。
            Check(od.Request(53)==null&&od.Wait(53,TimeSpan.FromSeconds(60))&&od.State(53)==OnDemandState.Ready,"OD gfx 53 becomes ready: "+od.Reason(53));
            var m53=od.Request(53)!;
            Check(Same(m53,Prepared(53),out string why53)&&m53.StepMs==1958,"OD gfx 53 without walk equals the prepared file (stepMs from breath) "+why53);

            // --- 3. 不可用：記住原因、不重試、不挑一個湊數。
            var unavailable=new (int Gfx,AssetPrepFailure Failure,string Text)[]{
                (60,AssetPrepFailure.Unavailable,"60-0.spr"),(61,AssetPrepFailure.MissingEntry,"61-0.spr"),(62,AssetPrepFailure.CorruptEntry,"多餘"),
                (63,AssetPrepFailure.CorruptEntry,"超出"),(70,AssetPrepFailure.Unavailable,"生物動作"),(9999,AssetPrepFailure.Unavailable,"9999"),(234,AssetPrepFailure.InvalidRequest,"clothes"),
            };
            foreach(var u in unavailable){
                Check(od.Request(u.Gfx)==null&&od.Wait(u.Gfx,TimeSpan.FromSeconds(60))&&od.State(u.Gfx)==OnDemandState.Unavailable,"OD gfx "+u.Gfx+" is marked unavailable");
                Check(od.Failure(u.Gfx)==u.Failure&&od.Reason(u.Gfx) is {} reason&&reason.Contains(u.Text,StringComparison.Ordinal),$"OD gfx {u.Gfx} unavailable as {u.Failure}: {od.Reason(u.Gfx)}");
            }
            int before=od.Attempts;
            for(int i=0;i<5;i++)foreach(var u in unavailable)Check(od.Request(u.Gfx)==null,"OD unavailable gfx "+u.Gfx+" stays null");
            Thread.Sleep(200);
            Check(od.Attempts==before&&unavailable.All(u=>od.AttemptsFor(u.Gfx)==1&&od.State(u.Gfx)==OnDemandState.Unavailable),"OD unavailable gfx are remembered: asked again, never retried");
            Check(od.EnvironmentFailure==null&&od.State(50)==OnDemandState.Ready,"OD a refused gfx does not affect the others");
        }

        // --- 4. 客戶端本身不能用：每個 gfx 都不可用、原因相同，只試開一次。
        using(var missing=new ObjectAppearanceOnDemand(Path.Combine(root,"no-such-client"),pins)){
            missing.Request(50);missing.Request(52);
            Check(missing.Wait(50,TimeSpan.FromSeconds(60))&&missing.Wait(52,TimeSpan.FromSeconds(60))&&missing.Failure(50)==AssetPrepFailure.ClientRoot&&missing.Failure(52)==AssetPrepFailure.ClientRoot&&missing.EnvironmentFailure!=null&&missing.Cache==null,"OD missing client root: every gfx unavailable (ClientRoot), no cache");
        }
        using(var other=new ObjectAppearanceOnDemand(client,AssetPrepPins.Tw1608262200)){
            other.Request(50);
            Check(other.Wait(50,TimeSpan.FromSeconds(60))&&other.Failure(50)==AssetPrepFailure.ContainerMismatch&&other.Reason(50)!.Contains("list.spz",StringComparison.Ordinal),"OD list.spz of another version: refused by the anchor, nothing decoded");
        }

        // --- 5. 執行期 context 不能寫檔。
        using(var input=AssetPrepInput.Open(client,pins)){
            var context=AssetRecipeItemContext.ForRuntime("50",input,null,CancellationToken.None);
            Check(context.ReadOnly,"OD runtime context is read-only");
            ApRefused(()=>context.Write("objects/50.json",new byte[]{1}),AssetPrepFailure.InvalidRequest,"OD runtime context refuses every write");
            ApRefused(()=>context.Write("objects/50.json",s=>s.WriteByte(1)),AssetPrepFailure.InvalidRequest,"OD runtime context refuses a streamed write");
        }

        // --- 6. 整輪下來：原客戶端、測試準備的樹與本 repo 的 engine/assets 逐檔 SHA 不變；測試引擎目錄沒有多出任何檔（沒有磁碟快取）。
        Check(ApTreeHash(client)==clientBefore,"OD client directory byte-identical (read-only input)");
        Check(ApTreeHash(engine)==engineBefore,"OD prepared engine tree byte-identical after on-demand decoding: nothing written to assets/ or runtime/");
        Check(RepoAssetsHash()==repoAssetsBefore,"OD repo engine/assets file-by-file SHA-256 unchanged ("+(repoAssetsBefore=="absent"?"absent here":"present")+")");
    }
}
