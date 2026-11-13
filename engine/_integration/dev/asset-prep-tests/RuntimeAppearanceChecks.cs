using P06;
using P06.AssetPrep;
using System.Text.Json;

// T478（#1074）：其餘 SPR 讀取點的執行期按需準備（RuntimeAppearances）的離線測試。合成的 17 個 Sprite 容器與 list.spz
// 由 SprRecipeChecks／SprRecipeBChecks 的同一組工具產生（像素是依編號算出的測試值，不是原客戶端的圖），寫在
// scratch/issue-1074/run-<時間>/ 下；不讀客戶端、不需要遊戲、網路或 DB，也不提交任何幀或像素。測試本身不做遞迴刪除。
// 每個讀取點各一組「清單外 → pending → 解完」與「不可用」：玩家姿勢（含 R1／R2 時間）、陰影（key 與 body 對得上）、
// 個人商店、門、魔法、飛行物、pickup 追加幀；外加讀取點接線（PlayerProfiles、PlayerShadowCatalog.Resolve、PacketEffectAssets）。
// 解完的結果與「事先準備同一個項目」逐幀相同；engine/assets（本 repo 的與測試自己準備的）前後逐檔 SHA 不變。
partial class Checks {
    static bool RaSame(ObjectAppearance a,ObjectAppearance b,out string why){
        why="";
        if(a.Gfx!=b.Gfx||a.Sprite!=b.Sprite||a.StepMs!=b.StepMs){why=$"head {a.Gfx}/{a.Sprite}/{a.StepMs} vs {b.Gfx}/{b.Sprite}/{b.StepMs}";return false;}
        if(!a.ActionMs.OrderBy(k=>k.Key,StringComparer.Ordinal).SequenceEqual(b.ActionMs.OrderBy(k=>k.Key,StringComparer.Ordinal))){why="actionMs";return false;}
        if(!RaSameFrames(a.Frames,b.Frames,out why))return false;
        if(!a.Sequences.Keys.Order().SequenceEqual(b.Sequences.Keys.Order())){why="sequence keys";return false;}
        foreach(var (k,sa) in a.Sequences)if(!sa.SequenceEqual(b.Sequences[k])){why=$"sequence {k}";return false;}
        return true;
    }
    static bool RaSameFrames(IReadOnlyDictionary<string,Frame> a,IReadOnlyDictionary<string,Frame> b,out string why){
        why="";
        if(!a.Keys.Order(StringComparer.Ordinal).SequenceEqual(b.Keys.Order(StringComparer.Ordinal))){why="frame keys "+string.Join(",",a.Keys.Order())+" vs "+string.Join(",",b.Keys.Order());return false;}
        foreach(var (key,fa) in a)if(!RaSameFrame(fa,b[key])){why="frame "+key;return false;}
        return true;
    }
    static bool RaSameFrame(Frame fa,Frame fb)=>fa.Key==fb.Key&&fa.Bounds.SequenceEqual(fb.Bounds)&&fa.Blocks.Length==fb.Blocks.Length
        &&fa.Blocks.Zip(fb.Blocks).All(p=>p.First.A==p.Second.A&&p.First.B==p.Second.B&&p.First.Z==p.Second.Z&&p.First.Order==p.Second.Order&&p.First.Tile.Type==p.Second.Tile.Type&&p.First.Tile.Pixels.SequenceEqual(p.Second.Tile.Pixels));

    [CheckGroup("asset-prep-runtime",Order=1),CheckGroup("native-ui-resources",Order=66),CheckGroup(CheckGroups.Default)]
    static void RuntimeAppearanceChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1074","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        string repoAssets=Path.Combine(Path.GetFullPath(repo),"engine","assets");
        string RepoAssetsHash()=>Directory.Exists(repoAssets)?ApTreeHash(repoAssets):"absent";
        string repoAssetsBefore=RepoAssetsHash();
        var previousActive=RuntimeAppearances.Active;

        // --- 合成的動畫表。61：完整職業外型（姿勢 0、20，商店 70，撿取 15，影子 3214）；62：同 sprite、姿勢 24 只有 walk；
        //     63：沒有 101.shadow；64：變身外型（同 sprite 3213）；70、2348：商店／門的動作方向不符；2347：門；189：魔法；66：箭。
        const string body=" 0.walk(1 2,0.0:4<478 0.1:4) 1.attack(1 2,8.0:3 8.1:3!) 2.damage(1 1,16.0:2) 3.breath(1 2,24.0:4 24.1:4) 8.death(0 2,32.0:5 32.1:7)"+
            " 18.spell dir(1 2,40.0:2 40.1:3!) 19.spell nodir(0 2,48.0:2 48.1:4!) 20.walk bow(1 1,56.0:4) 21.attack bow(1 3,64.0:5 64.1:5! 64.2:5) 22.damage bow(1 1,72.0:3) 23.breath bow(1 1,88.0:4)";
        const string pickup=" 15.get(1 4,24.0:1 100.1:1 100.2:4 100.3:4)";
        const string list=
            "S1223\r\n"+
            "#61\t96=3213 knight"+body+pickup+" 70.act shop(1 2,80.0:4 80.1:4) 101.shadow(3214)\r\n"+
            "#62\t96=3213 knight two"+body+" 24.walk spear(1 1,0.0:4) 101.shadow(3214)\r\n"+
            "#63\t96=3213 knight three"+body+"\r\n"+
            "#64\t96=3213 polymorph"+body+pickup+" 101.shadow(3214)\r\n"+
            "#66\t1 arrow 0.fly(1 1,0.0:2)\r\n"+
            "#70\t1 sign 70.act shop(0 1,0.0:4)\r\n"+
            "#189\t1 spark 0.effect(0 2,0.0:3 0.1:4)\r\n"+
            "#2347\t1 door 28.open(0 1,0.1:4[8) 29.close(0 1,0.0:4[9)\r\n"+
            "#2348\t1 door two 28.open(1 1,0.0:4) 29.close(0 1,0.0:4)\r\n";
        var files=new List<(int,string,byte[])>();
        void Put(int archive,int sprite,int sid,SrSpr spr)=>files.Add((archive,$"{sprite}-{sid}.spr",SrEncode(spr)));
        int[] bodySids=[..Enumerable.Range(0,33),..Enumerable.Range(40,9),..Enumerable.Range(56,40)];   // 0–32, 40–48, 56–95 (80–87 = shop)
        foreach(int sid in bodySids){Put(0,3213,sid,SbWide(3213,sid,3));if(sid is <80 or >87)Put(sid%3==0?5:0,3214,sid,SrMake(3214,sid,3));}
        for(int sid=100;sid<108;sid++)Put(0,3213,sid,SbWide(3213,sid,4));                     // 15.get embedded steps 1–3
        for(int sid=0;sid<8;sid++)Put(0,66,sid,SrMake(66,sid,1));
        Put(0,70,0,SrMake(70,0,1));Put(0,189,0,SrMake(189,0,2));Put(0,2347,0,SrMake(2347,0,2));
        string client=Path.Combine(root,"client");var pins=SrClient(client,list,files);
        string clientBefore=ApTreeHash(client);

        // --- 1. 事先準備同一批項目，當作比對基準（recipe 的 JSON）。
        string engine=Path.Combine(root,"engine");var runner=AssetPrepRunner.Product();
        AssetPrepResult Run(string recipe,params string[] items)=>runner.Run(new AssetPrepRequest(client,engine,recipe,items){Pins=pins});
        Check(Run("objects","magic-189","projectile-66","manifest").Outcome==AssetPrepOutcome.Published&&Run("shops","61","manifest").Outcome==AssetPrepOutcome.Published
            &&Run("doors","2347","manifest").Outcome==AssetPrepOutcome.Published&&Run("players","61-0","61-20","manifest").Outcome==AssetPrepOutcome.Published
            &&Run("shadows","61","manifest").Outcome==AssetPrepOutcome.Published,"RA baseline: objects, shops, doors, players, shadows prepare the same items");
        string Prepared(string recipe,string relative)=>Path.Combine(engine,"assets",recipe,relative);
        var preparedRows=Json.Read(Prepared("players","players/manifest.json")).GetProperty("profiles").EnumerateArray().Select(PlayerProfiles.Row).ToDictionary(p=>(p.Gfx,p.Weapon));
        string engineBefore=ApTreeHash(engine);

        // --- 2. 清單外 → pending → 解完（背景 worker 被測試閘門擋住時一律 pending）。
        int testThread=Environment.CurrentManagedThreadId;var decodeThreads=new System.Collections.Concurrent.ConcurrentBag<int>();
        using var gate=new ManualResetEventSlim(false);
        using(var od=new RuntimeAppearances(client,pins,_=>{decodeThreads.Add(Environment.CurrentManagedThreadId);gate.Wait(TimeSpan.FromSeconds(60));})){
            string kPlayer=RuntimeAppearances.PlayerKey(61,20),kShop=RuntimeAppearances.ShopKey(61),kDoor=RuntimeAppearances.DoorKey(2347),kMagic=RuntimeAppearances.MagicKey(189),kArrow=RuntimeAppearances.ProjectileKey(66);
            Check(od.Player(61,20)==null&&od.Shop(61)==null&&od.Door(2347)==null&&od.Magic(189)==null&&od.Projectile(66)==null,"RA first request of every reader returns null (queued, never decoded on the caller's thread)");
            Check(new[]{kPlayer,kShop,kDoor,kMagic,kArrow}.All(k=>od.State(k)==OnDemandState.Pending),"RA player stance, shop, door, magic and projectile are pending while the worker is held");
            Parallel.For(0,48,i=>{if(od.Player(61,20)!=null||od.Shop(61)!=null)throw new Exception("RA a pending item returned a value");});
            Check(od.AttemptsFor(kPlayer)<=1&&od.AttemptsFor(kShop)==0,"RA 48 concurrent requests while held: nothing built twice");
            gate.Set();
            Check(new[]{kPlayer,kShop,kDoor,kMagic,kArrow}.All(k=>od.Wait(k,TimeSpan.FromSeconds(60))&&od.State(k)==OnDemandState.Ready),"RA all five become ready: "+string.Join(" / ",new[]{kPlayer,kShop,kDoor,kMagic,kArrow}.Select(k=>k+"="+od.State(k)+" "+od.Reason(k))));
            Check(!decodeThreads.Contains(testThread)&&decodeThreads.Count>=5,"RA building runs on the background worker");
            for(int i=0;i<5;i++){od.Player(61,20);od.Shop(61);od.Door(2347);od.Magic(189);od.Projectile(66);}
            Check(new[]{kPlayer,kShop,kDoor,kMagic,kArrow}.All(k=>od.AttemptsFor(k)==1),"RA each item built exactly once");

            // 玩家姿勢：外觀與事先準備的 players/61-20.json 逐幀相同；row 的 SHA 等於準備檔（同一個 recipe 產生同一份 bytes）。
            var player=od.Player(61,20)!;var row=preparedRows[(61,20)];
            Check(RaSame(player.Appearance,new ObjectAppearance(Prepared("players","players/61-20.json")),out string whyP),"RA player 61/20 equals the prepared players/61-20.json frame by frame "+whyP);
            Check(player.Profile.Sha256==row.Sha256&&player.Profile.Path==row.Path&&player.Profile.StepMs==row.StepMs&&player.Profile.ProjectileReleaseMs==row.ProjectileReleaseMs
                &&player.Profile.ServerActionMs.OrderBy(k=>k.Key).SequenceEqual(row.ServerActionMs.OrderBy(k=>k.Key))&&player.Profile.CastReleaseMs.OrderBy(k=>k.Key).SequenceEqual(row.CastReleaseMs.OrderBy(k=>k.Key)),"RA player 61/20 row equals the prepared manifest row (same bytes, same times)");
            // R1：40 × ticks（walk 20 = 4、attack 21 = 15、18 = 5、19 = 6）；R2：第一個 '!' 之前 floor(ticks × 1000 ÷ 24)。
            Check(player.Profile.StepMs==160&&player.Profile.ServerActionMs["walk"]==160&&player.Profile.ServerActionMs["attack"]==600&&player.Profile.ServerActionMs["spellDir"]==200&&player.Profile.ServerActionMs["spellNoDir"]==240
                &&player.Profile.CastReleaseMs["spellDir"]==83&&player.Profile.CastReleaseMs["spellNoDir"]==83&&player.Profile.ProjectileReleaseMs==208,"RA player 61/20 times by action-timing.md R1 / R2 from list.spz");
            Check(ReferenceEquals(player.Profile.Runtime,player.Appearance)&&player.Appearance.Runtime&&ReferenceEquals(PlayerProfiles.Load(engine,player.Profile),player.Appearance),"RA the runtime profile carries its appearance; PlayerProfiles.Load returns it without reading a file");
            Check(od.Sources(kPlayer).Any(s=>s is {Container:"Text",Entry:"list.spz"})&&od.Sources(kPlayer).Any(s=>s.Entry=="3213-56.spr"),"RA player sources: list.spz and the .spr files read");

            Check(RaSame(od.Shop(61)!,new ObjectAppearance(Prepared("shops","shops/61.json")),out string whyS),"RA shop 61 (action 70) equals the prepared shops/61.json "+whyS);
            Check(RaSame(od.Door(2347)!,new ObjectAppearance(Prepared("doors","doors/2347.json")),out string whyD),"RA door 2347 (actions 28/29) equals the prepared doors/2347.json "+whyD);
            Check(RaSame(od.Magic(189)!,new ObjectAppearance(Prepared("objects","objects/magic-189.json")),out string whyM),"RA magic 189 equals the prepared objects/magic-189.json "+whyM);
            Check(RaSameFrames(od.Projectile(66)!,RuntimeAppearances.ProjectileFrames(Json.Read(Prepared("objects","objects/projectile-66.json"))),out string whyA),"RA projectile 66 frames equal the prepared objects/projectile-66.json "+whyA);

            // 陰影：sprite 取 101.shadow；幀＝body 序列實際引用的 key（不多不少），每幀與事先準備的 shadows/61.json 同 key 的幀相同。
            var bodyKeys=player.Appearance.Sequences.Values.SelectMany(s=>s.Select(t=>t.Key)).ToHashSet(StringComparer.Ordinal);
            Check(od.Shadow(player.Appearance)==null&&od.State(RuntimeAppearances.ShadowKey(player.Appearance))==OnDemandState.Pending,"RA shadow of a runtime body: first request pending");
            Check(od.Wait(RuntimeAppearances.ShadowKey(player.Appearance),TimeSpan.FromSeconds(60))&&od.Shadow(player.Appearance) is not null,"RA shadow becomes ready: "+od.Reason(RuntimeAppearances.ShadowKey(player.Appearance)));
            var shadow=od.Shadow(player.Appearance)!;
            var preparedShadow=PlayerShadowCatalog.ReadFrames(Json.Read(Prepared("shadows","shadows/61.json")),61,3213);
            Check(shadow.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(bodyKeys)&&bodyKeys.All(k=>RaSameFrame(shadow[k],preparedShadow[k]))&&preparedShadow.Count>shadow.Count,
                $"RA shadow keys = the body's sequence keys ({shadow.Count}), each frame equals the prepared 101.shadow frame (prepared union of stances 0 and 20 has {preparedShadow.Count})");
            Check(od.Cache("shadows")!.Keys.All(k=>k.RecipeId=="shadows"&&k.ListSpzSha256==pins.ListSpzSha256)&&od.Sources(RuntimeAppearances.ShadowKey(player.Appearance)).All(s=>s.Entry=="list.spz"||s.Entry.StartsWith("3214-",StringComparison.Ordinal)),"RA shadow read only the 3214 shadow sprite (cache keyed by recipe and list.spz SHA)");

            // pickup（動作 15）：清單外的 runtime body 有沒有追加幀看 list.spz 的動作 15，不看寫死清單。
            var p0=od.Player(61,0);
            Check(p0==null&&od.Wait(RuntimeAppearances.PlayerKey(61,0),TimeSpan.FromSeconds(60))&&od.Player(61,0) is not null,"RA player 61/0 built on demand");
            var body0=od.Player(61,0)!.Appearance;
            Check(od.Pickup(body0,0)==null&&od.Wait(RuntimeAppearances.PickupKey(body0,0),TimeSpan.FromSeconds(60))&&od.Pickup(body0,0) is {FrameCount:24},"RA pickup group of 61/0 (action 15) pending → ready, 24 embedded frames: "+od.Reason(RuntimeAppearances.PickupKey(body0,0)));
            var group=od.Pickup(body0,0)!;
            bool composed=Enumerable.Range(0,8).All(d=>PickupAssets.Compose(group,body0,d) is {Length:4} f&&ReferenceEquals(f[0],body0.Frames[$"{24+d}:0"])&&group.Keys(d).Skip(1).SequenceEqual(new[]{$"{100+d}:1",$"{100+d}:2",$"{100+d}:3"}));
            Check(composed,"RA pickup frames per direction: step 0 from the body, steps 1–3 embedded from action 15");

            // --- 3. 不可用：記住原因、不重試、不挑一份湊數。
            var body20=player.Appearance;
            var refusals=new (string Key,Action Ask,AssetPrepFailure Failure,string Text)[]{
                (RuntimeAppearances.PlayerKey(61,4),()=>od.Player(61,4),AssetPrepFailure.Unavailable,"missing complete action 4"),
                (RuntimeAppearances.PlayerKey(62,24),()=>od.Player(62,24),AssetPrepFailure.Unavailable,"missing complete action 25"),
                (RuntimeAppearances.PlayerKey(0,0),()=>od.Player(0,0),AssetPrepFailure.InvalidRequest,"objects"),
                (RuntimeAppearances.ShopKey(70),()=>od.Shop(70),AssetPrepFailure.Unavailable,"八方向"),
                (RuntimeAppearances.ShopKey(189),()=>od.Shop(189),AssetPrepFailure.Unavailable,"70"),
                (RuntimeAppearances.DoorKey(2348),()=>od.Door(2348),AssetPrepFailure.Unavailable,"方向"),
                (RuntimeAppearances.DoorKey(61),()=>od.Door(61),AssetPrepFailure.Unavailable,"28"),
                (RuntimeAppearances.MagicKey(9998),()=>od.Magic(9998),AssetPrepFailure.Unavailable,"9998"),
                (RuntimeAppearances.ProjectileKey(9997),()=>od.Projectile(9997),AssetPrepFailure.Unavailable,"9997"),
                (RuntimeAppearances.PickupKey(body20,20),()=>od.Pickup(body20,20),AssetPrepFailure.Unavailable,"has no frame 24:0"),
            };
            foreach(var r in refusals){
                r.Ask();
                Check(od.Wait(r.Key,TimeSpan.FromSeconds(60))&&od.State(r.Key)==OnDemandState.Unavailable&&od.Failure(r.Key)==r.Failure&&od.Reason(r.Key) is {} why&&why.Contains(r.Text,StringComparison.Ordinal),$"RA {r.Key} unavailable as {r.Failure}: {od.Reason(r.Key)}");
            }
            // 陰影不可用：63 沒有 101.shadow。
            var p63=od.Player(63,0);od.Wait(RuntimeAppearances.PlayerKey(63,0),TimeSpan.FromSeconds(60));var body63=od.Player(63,0)!.Appearance;
            od.Shadow(body63);string k63=RuntimeAppearances.ShadowKey(body63);
            Check(od.Wait(k63,TimeSpan.FromSeconds(60))&&od.State(k63)==OnDemandState.Unavailable&&od.Reason(k63)!.Contains("101",StringComparison.Ordinal),"RA shadow of a body without 101.shadow is unavailable: "+od.Reason(k63));
            int before=od.Attempts;
            for(int i=0;i<5;i++){foreach(var r in refusals)r.Ask();od.Shadow(body63);}
            Thread.Sleep(200);
            Check(od.Attempts==before&&refusals.All(r=>od.AttemptsFor(r.Key)==1&&od.State(r.Key)==OnDemandState.Unavailable),"RA unavailable items are remembered: asked again, never retried");
            Check(od.EnvironmentFailure==null&&od.State(kPlayer)==OnDemandState.Ready,"RA a refused item does not affect the others");
        }

        // --- 4. 讀取點接線（RuntimeAppearances.Active）：PlayerProfiles、陰影、封包特效。
        string tree=Path.Combine(root,"tree");
        foreach(var (recipe,rel) in new[]{("shadows","shadows/manifest.json"),("shadows","shadows/61.json")}){
            string target=Path.Combine(tree,rel);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Prepared(recipe,rel),target);
        }
        File.WriteAllBytes(Path.Combine(tree,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{schema="p06-assets-integrity-v1",files=new[]{"shadows/manifest.json","shadows/61.json"}.Select(rel=>{byte[] d=File.ReadAllBytes(Path.Combine(tree,rel));return new{path=rel,size=d.Length,sha256=Sha(d)};}).ToArray()}));
        string treeBefore=ApTreeHash(tree);
        using var wiringGate=new ManualResetEventSlim(false);
        using(var od=new RuntimeAppearances(client,pins,_=>wiringGate.Wait(TimeSpan.FromSeconds(60)))){
            try{
                RuntimeAppearances.Active=od;
                // PlayerProfiles 的解析規則（Resolve）用合成的「已準備」集合測，不碰行程共用的 players/manifest.json。
                // 61/0、61/20 是已準備的職業 gfx（IsCharacter）；64 只在自己認領（S87／S76）後才按需；其他 gfx 不轉成玩家規則。
                var prepared=new Dictionary<(int,int),PlayerProfile>{[(61,0)]=preparedRows[(61,0)],[(61,20)]=preparedRows[(61,20)]};
                var claimed=new HashSet<int>();bool OnDemandFor(int g)=>prepared.Keys.Any(k=>k.Item1==g)||claimed.Contains(g);
                PlayerProfile? Resolve(int g,int w,out bool pending)=>PlayerProfiles.Resolve(prepared,od,OnDemandFor,g,w,out pending);
                Check(Resolve(61,20,out bool p20) is {Runtime:null}&&!p20&&od.State(RuntimeAppearances.PlayerKey(61,20))==OnDemandState.None,"RA wiring: a prepared stance is used as is (nothing asked)");
                Check(Resolve(64,0,out bool p64) is null&&!p64&&od.State(RuntimeAppearances.PlayerKey(64,0))==OnDemandState.None,"RA wiring: another object's unprepared gfx is not asked as a player profile");
                claimed.Add(64);
                Check(Resolve(64,0,out bool pending) is null&&pending,"RA wiring: own polymorph gfx 64 → pending (WorldSession.SelfAppearancePending covers it)");
                Check(Resolve(61,50,out bool p50) is null&&p50,"RA wiring: prepared class gfx 61, stance 50 outside the preheat list → pending, not the stance 0 fallback");
                wiringGate.Set();
                Check(od.Wait(RuntimeAppearances.PlayerKey(64,0),TimeSpan.FromSeconds(60))&&Resolve(64,0,out bool after) is {Gfx:64,Weapon:0,Runtime:not null}&&!after,"RA wiring: own gfx 64 ready → the runtime profile");
                var own=Resolve(64,0,out _)!;
                // 清單外姿勢不可用：照原本的規則退回姿勢 0（61 退回已準備的、64 退回按需的）。
                od.Wait(RuntimeAppearances.PlayerKey(61,50),TimeSpan.FromSeconds(60));Resolve(64,4,out _);od.Wait(RuntimeAppearances.PlayerKey(64,4),TimeSpan.FromSeconds(60));
                Check(od.State(RuntimeAppearances.PlayerKey(61,50))==OnDemandState.Unavailable&&ReferenceEquals(Resolve(61,50,out bool q50),prepared[(61,0)])&&!q50,"RA wiring: unavailable stance 50 of 61 falls back to the prepared stance 0");
                Check(od.State(RuntimeAppearances.PlayerKey(64,4))==OnDemandState.Unavailable&&ReferenceEquals(Resolve(64,4,out bool q4),own)&&!q4,"RA wiring: unavailable stance 4 of 64 falls back to the runtime stance 0");
                Check(Resolve(64,1,out _)==null&&od.State(RuntimeAppearances.PlayerKey(64,1))==OnDemandState.None,"RA wiring: an illegal stance is never requested");
                Check(Resolve(64,0,out _) is {} again&&ReferenceEquals(again,own)&&PlayerProfiles.Resolve(prepared,null,OnDemandFor,64,0,out bool none)==null&&!none,"RA wiring: same instance on every request; no on-demand source → prepared set only");
                // 陰影讀取點：事先準備的陰影優先；runtime body（64）沒有準備的陰影 → 按需；非 runtime 的外觀不按需。
                var catalog=new PlayerShadowCatalog(tree);var body64=own.Runtime!;string key=body64.Sequences[("walk",0)][0].Key;
                var shadowOf61=PlayerShadowCatalog.Resolve(catalog,od,new ObjectAppearance(Prepared("players","players/61-20.json")),key);
                Check(shadowOf61!=null&&od.State(RuntimeAppearances.ShadowKey(new ObjectAppearance(Prepared("players","players/61-20.json"))))==OnDemandState.None,"RA wiring: a prepared body keeps its prepared shadow (nothing asked)");
                Check(PlayerShadowCatalog.Resolve(catalog,od,body64,key)==null,"RA wiring: runtime body 64 without a prepared shadow → no shadow while pending");
                od.Wait(RuntimeAppearances.ShadowKey(body64),TimeSpan.FromSeconds(60));
                Check(PlayerShadowCatalog.Resolve(catalog,od,body64,key) is {} s64&&RaSameFrame(s64,od.Shadow(body64)![key])&&PlayerShadowCatalog.Resolve(catalog,null,body64,key)==null,"RA wiring: runtime body 64 gets its 101.shadow frame once ready; no on-demand source → no shadow");

                // 封包特效（MapEffects.cs PacketEffectAssets）：沒有 objects/magic-<gfx>.json 也沒有地圖特效 → 按需；pending 時不快取。
                var effects=new PacketEffectAssets(tree);
                var first=effects.Get(189);bool wasPending=od.State(RuntimeAppearances.MagicKey(189))!=OnDemandState.None;
                od.Wait(RuntimeAppearances.MagicKey(189),TimeSpan.FromSeconds(60));
                Check(wasPending&&effects.Get(189) is {Duration:>0},"RA wiring: packet effect 189 without a prepared file is built on demand (first "+(first==null?"pending":"ready")+")");
                Check(effects.Get(9998)==null&&od.Wait(RuntimeAppearances.MagicKey(9998),TimeSpan.FromSeconds(60))&&effects.Get(9998)==null,"RA wiring: an unavailable packet effect stays null");
            }
            finally{RuntimeAppearances.Active=previousActive;}
        }

        // --- 5. 客戶端本身不能用：每個項目都不可用、原因相同。
        using(var missing=new RuntimeAppearances(Path.Combine(root,"no-such-client"),pins)){
            missing.Player(61,20);missing.Shop(61);
            Check(missing.Wait(RuntimeAppearances.PlayerKey(61,20),TimeSpan.FromSeconds(60))&&missing.Wait(RuntimeAppearances.ShopKey(61),TimeSpan.FromSeconds(60))
                &&missing.Failure(RuntimeAppearances.PlayerKey(61,20))==AssetPrepFailure.ClientRoot&&missing.Failure(RuntimeAppearances.ShopKey(61))==AssetPrepFailure.ClientRoot&&missing.EnvironmentFailure!=null,"RA missing client root: every item unavailable (ClientRoot)");
        }

        // --- 6. 記憶體 context 只寫記憶體。
        using(var input=AssetPrepInput.Open(client,pins)){
            var context=AssetRecipeItemContext.ForRuntimeMemory("x",input,null,CancellationToken.None);
            context.Write("objects/x.json",new byte[]{1});context.Write("objects/y.json",s=>s.WriteByte(2));
            Check(context.ReadOnly&&context.Captured.Count==2&&context.Captured["objects/y.json"].SequenceEqual(new byte[]{2}),"RA memory context keeps both writes in memory");
            ApRefused(()=>context.Write("objects/x.json",new byte[]{3}),AssetPrepFailure.InvalidRequest,"RA memory context: a path is written once");
            ApRefused(()=>context.Write("../x.json",new byte[]{3}),AssetPrepFailure.UnsafePath,"RA memory context: unsafe path refused");
        }

        // --- 7. 整輪下來：客戶端、測試準備的樹、接線用的樹與本 repo 的 engine/assets 逐檔 SHA 不變。
        Check(ApTreeHash(client)==clientBefore,"RA client directory byte-identical (read-only input)");
        Check(ApTreeHash(engine)==engineBefore,"RA prepared engine tree byte-identical: nothing written to assets/ or runtime/");
        Check(ApTreeHash(tree)==treeBefore,"RA wiring tree byte-identical");
        Check(RepoAssetsHash()==repoAssetsBefore,"RA repo engine/assets file-by-file SHA-256 unchanged ("+(repoAssetsBefore=="absent"?"absent here":"present")+")");
    }
}
