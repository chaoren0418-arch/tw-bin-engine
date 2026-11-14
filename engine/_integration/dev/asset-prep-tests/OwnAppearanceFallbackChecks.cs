using P06;
using P06.AssetPrep;

// #1243（f1211-player-appearance）：自己的外觀沒有玩家規則 profile 時的寬鬆建置（RuntimeAppearances.Lenient、PlayerProfiles.Resolve）。
// 合成的 17 個 Sprite 容器與 list.spz 由 SprRecipeChecks 的同一組工具產生（像素是依編號算出的測試值，不是原客戶端的圖），
// 寫在 scratch/issue-1243/run-<時間>/ 下；不讀客戶端、不需要遊戲、網路或 DB，也不提交任何幀或像素。測試本身不做遞迴刪除。
// 合成的 gfx 對應 #1211 的實例（動作組合照 docs/reports/issue-1243.md 由原客戶端 list.spz 讀出的形狀，數字是測試值）：
//   138  七個動作都有，但 SPR 有不透明像素落在幀 bounds 外（L7，玩家規則拒收）
//   1062 有 0／1／2／3／8，沒有 18／19（drake 變身）
//   1461 只有 0／3／4（great dane 變身）
//   1395 沒有任何動作（椅子靜態外型）
//   7000 有動作但 SPR 檔不在任何 Sprite 容器
// 七職基本外型（0／1／61／48／37／734／1186／2786／2796／6658／6661／6671／6650）都已在 players/manifest.json（預建），
// 由既有的 polymorph／action-timing 群組驗；這裡驗的是「不在預建清單」的路徑。
partial class Checks {
    [CheckGroup("own-appearance-fallback",Order=1),CheckGroup("polymorph-cadence",Order=50),CheckGroup(CheckGroups.Default)]
    static void OwnAppearanceFallbackChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1243","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        var previousActive=RuntimeAppearances.Active;

        const string flat=" 0.walk(0 1,0.0:4) 1.attack(0 1,0.1:3!) 2.damage(0 1,0.2:2) 3.breath(0 1,0.3:4) 8.death(0 1,0.0:5) 18.spell dir(0 1,0.1:2!) 19.spell nodir(0 1,0.2:4!)";
        const string list=
            "S1223\r\n"+
            "#61\t96=3213 knight"+flat+"\r\n"+
            "#138\t1=3217 elf"+flat+"\r\n"+
            "#1062\t1=3300 drake 0.walkfastI(1 2,0.0:2 0.1:2) 1.attack(1 2,8.0:4 8.1:4!) 2.damage(1 1,16.0:4) 3.breath(1 1,24.0:3) 8.death(1 1,32.0:4)\r\n"+
            "#1461\t1=3400 dane 0.run_one(1 1,8.0:2) 3.breath(1 1,16.0:4) 4.run_two(1 1,0.0:2)\r\n"+
            "#1395\t1 chair 102.type(0) 101.shadow(1396) 112.altdir(1410)\r\n"+
            "#7000\t1=3500 ghost 0.walk(0 1,0.0:4) 3.breath(0 1,0.0:4)\r\n";
        var files=new List<(int,string,byte[])>();
        void Put(int sprite,int sid,SrSpr spr)=>files.Add((0,$"{sprite}-{sid}.spr",SrEncode(spr)));
        Put(3213,0,SbWide(3213,0,4));                                           // 61: wide bounds, the player rule accepts it
        Put(3217,0,SrMake(3217,0,4));                                           // 138: tight bounds, pixels outside the frame
        for(int sid=0;sid<40;sid++)Put(3300,sid,SbWide(3300,sid,2));
        for(int sid=0;sid<24;sid++)Put(3400,sid,SbWide(3400,sid,1));
        string client=Path.Combine(root,"client");var pins=SrClient(client,list,files);
        string clientBefore=ApTreeHash(client);

        using(var od=new RuntimeAppearances(client,pins)){
            var claimed=new HashSet<int>{61,138,1062,1461,1395,7000};
            var prepared=new Dictionary<(int,int),PlayerProfile>();
            void Settle(int g,int w){foreach(string k in new[]{RuntimeAppearances.PlayerKey(g,w),RuntimeAppearances.PlayerKey(g,0),RuntimeAppearances.LenientKey(g,w),RuntimeAppearances.LenientKey(g,0)})od.Wait(k,TimeSpan.FromSeconds(60));}
            PlayerProfile? Resolve(int g,int w,out bool pending){
                PlayerProfile? p=null;pending=true;
                for(int i=0;i<8&&pending;i++){p=PlayerProfiles.Resolve(prepared,od,x=>claimed.Contains(x),g,w,out pending);if(pending)Settle(g,w);}
                return p;
            }

            // --- 1. 玩家規則照舊（L7 不變）：138 因 bounds 拒收、1062 缺 18、1461 缺 1。
            foreach(var (g,why) in new[]{(138,"opaque pixel outside frame"),(1062,"missing complete action 18/spellDir"),(1461,"missing complete action 1/attack")}){
                od.Player(g,0);od.Wait(RuntimeAppearances.PlayerKey(g,0),TimeSpan.FromSeconds(60));
                Check(od.State(RuntimeAppearances.PlayerKey(g,0))==OnDemandState.Unavailable&&od.Reason(RuntimeAppearances.PlayerKey(g,0)) is {} r&&r.StartsWith(why,StringComparison.Ordinal),$"OAF player rule still refuses {g}/0 ({why}): {od.Reason(RuntimeAppearances.PlayerKey(g,0))}");
            }
            od.Player(61,0);od.Wait(RuntimeAppearances.PlayerKey(61,0),TimeSpan.FromSeconds(60));
            prepared[(61,0)]=od.Player(61,0)!.Profile;

            // --- 2. 寬鬆建置：138 七個動作全部、八方向；不因 bounds 拒收；時間照 R1（walk 4 ticks × 40）。
            var p138=Resolve(138,0,out bool pend138);
            Check(p138 is {Gfx:138,Weapon:0,Lenient:true,StepMs:160}&&!pend138&&p138.Runtime is {} a138&&new[]{"walk","breath","attack","damage","die","spellDir","spellNoDir"}.All(n=>Enumerable.Range(0,8).All(d=>a138.Supports(n,d))),
                "OAF 138/0 (L7 bounds) resolves through the lenient build with all seven actions: "+od.Reason(RuntimeAppearances.LenientKey(138,0)));
            PlayerProfiles.Check(p138!.Runtime!,p138);
            Check(p138.Runtime!.Frames.Count==4&&p138.Runtime.Frames.Values.All(f=>f.Blocks.Length==2),"OAF 138 keeps every frame and tile of the SPR (nothing clipped to the bounds)");
            Check(p138.Runtime.ActionMs["walk"]==160&&p138.Runtime.ActionMs["attack"]==125&&p138.Runtime.ActionMs["breath"]==166,"OAF lenient actionMs as a players file (walk ×40, others floor)");
            // 姿勢 4 不在 138 的 entry：先退回玩家規則的姿勢 0（不可用），再寬鬆姿勢 4（沒有 walk，不可用），最後寬鬆姿勢 0（同一個實例）。
            Check(ReferenceEquals(Resolve(138,4,out bool pend1384),p138)&&!pend1384&&od.Reason(RuntimeAppearances.LenientKey(138,4)) is {} r4&&r4.Contains("walk",StringComparison.Ordinal),"OAF 138/4 falls back to the lenient stance 0: "+od.Reason(RuntimeAppearances.LenientKey(138,4)));

            // --- 3. 1062：有的動作照畫，沒有 18／19；攻擊節奏照 R1。
            var p1062=Resolve(1062,0,out bool pend1062);
            Check(p1062 is {Gfx:1062,Weapon:0,Lenient:true,StepMs:160}&&!pend1062&&p1062.Runtime!.Supports("attack",3)&&!p1062.Runtime.Supports("spellDir",0)&&!p1062.Runtime.Supports("spellNoDir",0),"OAF 1062/0 lenient: walk/attack/damage/breath/die, no spell actions");
            Check(ActionTiming.ServerMs(p1062!,"attack")==320&&ActionTiming.ServerMs(p1062,"walk")==160&&ActionTiming.Has(p1062,"attack")&&!ActionTiming.Has(p1062,"spellDir"),"OAF 1062 R1 from the runtime steps (attack 8 ticks = 320 ms)");
            bool threw=false;try{ActionTiming.StepsOf(p1062,"spellDir");}catch(InvalidDataException){threw=true;}
            Check(threw&&ActionTiming.CastReleaseMs(p1062,"attack")==166,"OAF missing action → InvalidDataException like a file; release point from the runtime events");
            Check(ActionTiming.Has(prepared[(61,0)],"spellDir"),"OAF a player-rule profile has every action");

            // --- 4. 1461：只有 walk 與 breath。
            var p1461=Resolve(1461,0,out _);
            Check(p1461 is {Gfx:1461,Lenient:true,StepMs:80}&&p1461.Runtime!.Supports("breath",7)&&!p1461.Runtime.Supports("attack",0),"OAF 1461/0 lenient: walk 2 ticks = 80 ms, breath, no attack");

            // --- 5. 真的建不出來：照實不可用（原因可查），不拿別的 gfx 湊。
            Check(Resolve(1395,0,out bool pend1395)==null&&!pend1395&&od.Reason(RuntimeAppearances.LenientKey(1395,0)) is {} r1395&&r1395.Contains("沒有姿勢 0 的任何玩家動作",StringComparison.Ordinal),"OAF 1395 (no actions) stays unavailable with its reason: "+od.Reason(RuntimeAppearances.LenientKey(1395,0)));
            Check(Resolve(7000,0,out bool pend7000)==null&&!pend7000&&od.Failure(RuntimeAppearances.LenientKey(7000,0))==AssetPrepFailure.MissingEntry,"OAF 7000 (SPR in no container) unavailable as MissingEntry: "+od.Reason(RuntimeAppearances.LenientKey(7000,0)));

            // --- 6. 既有順序不變：預建優先、不認領的 gfx 不建、非法姿勢不建。
            Check(ReferenceEquals(Resolve(61,0,out _),prepared[(61,0)])&&od.State(RuntimeAppearances.LenientKey(61,0))==OnDemandState.None,"OAF a prepared profile wins; nothing lenient asked");
            Check(ReferenceEquals(Resolve(61,50,out _),prepared[(61,0)])&&od.State(RuntimeAppearances.LenientKey(61,50))==OnDemandState.None,"OAF missing stance of a prepared gfx still falls back to the prepared stance 0, not a lenient build");
            Check(PlayerProfiles.Resolve(prepared,od,_=>false,1461,4,out bool unclaimed)==null&&!unclaimed&&od.State(RuntimeAppearances.PlayerKey(1461,4))==OnDemandState.None&&od.State(RuntimeAppearances.LenientKey(1461,4))==OnDemandState.None,"OAF an unclaimed gfx (another object) is never built with the player rule");
            Check(Resolve(1062,1,out _)==null&&od.State(RuntimeAppearances.LenientKey(1062,1))==OnDemandState.None,"OAF an illegal stance is never requested");
            Check(PlayerProfiles.Resolve(prepared,null,x=>true,138,0,out bool noOd)==null&&!noOd,"OAF no on-demand source → prepared set only (offline tests unchanged)");
            int attempts=od.Attempts;for(int i=0;i<5;i++){Resolve(138,0,out _);Resolve(1395,0,out _);}
            Check(od.Attempts==attempts,"OAF ready and unavailable lenient items are remembered, never rebuilt");

            // --- 7. WorldSession：自己變成 138／1062 時外觀可用（不再「移動與戰鬥已停止」）；1395 照實告知資料未到。
            // WorldSession 經 PlayerProfiles.Own 讀行程共用的 players/manifest.json（engine/assets）；CI 沒有 assets 時略過這段
            // （上面 1–6 段的 Resolve 規則不依賴 assets）。
            if(!File.Exists(Path.Combine(ActionTiming.AssetsRoot,"integrity.json"))){Check(true,"OAF WorldSession wiring skipped: no engine/assets here ("+ActionTiming.AssetsRoot+")");}
            else try{
                RuntimeAppearances.Active=od;
                var w=new WorldSession();w.CacheEnabled=false;
                Set(w,"Status",new OwnStatus(123,1,0,0,0,0,0,0,0,100,100,100,100,0,0,0,0,0,0,0,0,0,0));
                bool Supported(int gfx){
                    for(int i=0;i<8&&(w.SelfAppearancePending||w.AppearanceProfile==null);i++){_=w.AppearanceProfile;Settle(gfx,0);if(!w.SelfAppearancePending&&w.AppearanceProfile==null&&od.State(RuntimeAppearances.LenientKey(gfx,0))==OnDemandState.Unavailable)break;}
                    return w.SupportedAppearance;
                }
                PolyFeed(w,PolyPack(123,138,0,0,0));
                Check(Supported(138)&&w.AppearanceProfile is {Gfx:138,Lenient:true}&&w.AppearanceNotice=="角色外觀已載入"&&w.MovementAnimationMs==NormalSpeed.AnimationMs(160,0,0),"OAF own gfx 138: appearance supported, movement timing from the lenient walk: "+w.AppearanceNotice);
                PolyFeed(w,Poly(123,1062));
                Check(Supported(1062)&&w.AppearanceProfile is {Gfx:1062,Lenient:true}&&w.AppearanceNotice=="角色外觀已載入","OAF own polymorph 1062: appearance supported: "+w.AppearanceNotice);
                Check(ActionCadence.Supported(1062,0,1)&&!ActionCadence.Supported(1062,0,18)&&ActionCadence.Capture(1062,0,1,0,0) is {ServerBaseMs:320},"OAF 1062 attack cadence from the runtime appearance; spell cadence reported unsupported (no exception)");
                PolyFeed(w,Poly(123,1395));
                Check(!Supported(1395)&&w.AppearanceNotice.Contains("外觀資料未到",StringComparison.Ordinal)&&w.AppearanceNotice.Contains("1395",StringComparison.Ordinal),"OAF own 1395 (no actions) is reported as data not available: "+w.AppearanceNotice);
                PolyFeed(w,Poly(123,138));
                Check(Supported(138)&&w.AppearanceProfile is {Gfx:138},"OAF back to 138: supported again");
            }
            finally{RuntimeAppearances.Active=previousActive;}
        }
        Check(ApTreeHash(client)==clientBefore,"OAF client directory byte-identical (read-only input)");
    }
}
