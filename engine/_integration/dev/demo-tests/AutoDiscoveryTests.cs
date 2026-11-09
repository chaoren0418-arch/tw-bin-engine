using P06.NativeUi;

// T270 (#645): offline checks for DemoChecks group discovery (AutoDiscovery.cs). Group "auto-discovery" only, so the
// counts of the pre-existing groups stay comparable. No game, network or assets.
partial class Checks {
    /// <summary>Every command-line group of the pre-T270 Program.cs switch (party/pets/polymorph only ran the preamble).</summary>
    static readonly string[] LegacyGroups=["timed-buffs","afflictions","resurrection","char-reset","clan","clan-emblem","clan-war","portals","doors","pickup","door-approach","party","pets","display-notices","name-display","map-continuity","marriage","yes-no","trade-yes-no","ghost-mode","ghost-limits","fishing","location-share","player-query","hypertext","character-config","hotbar","polymorph","polymorph-cadence","buddies","buddies-codec","minimap","ambience","map-ambient","background-music","invisibility","bookmarks","board","item-transfer","combat-loop","cast-cadence","combat-admission","title-emote","title-emote-assets","title-emote-compose","duel","attack-skills","attack-skill-resources","hit-reaction-suppress","slow-actions","item-status","native-ui-resources","native-ui-canvas","native-ui-left-status","native-ui-macro","native-ui-hpmp","native-ui-party","native-ui-chat-window","native-ui-menu-buttons","native-ui-minimap","keyboard","busy","world"];
    /// <summary>Pre-T270 call sequences (method names). New modules may join a group anywhere; these must keep their relative order.</summary>
    static readonly Dictionary<string,string[]> LegacySequences=new(){
        [CheckGroups.Preamble]=["PartyChecks","Pets","PolymorphChecks"],
        [CheckGroups.Default]=["BookmarkChecks","ItemStatusChecks","NativeUiResourceChecks","NativeUiCanvasChecks","NativeUiLeftStatusChecks","NativeUiHpMpChecks","NativeUiMacroChecks","NativeUiChatWindowChecks","NativeUiBoardChecks","NativeUiMenuButtonsChecks","NativeUiMiniMapChecks","NativeUiPartyChecks","MinimapChecks","CharacterConfigChecks","HotbarChecks","NativeTargetUi","Movement123","KeyboardMovement123","KeyboardBusy123","KeyboardWorld123","Combat","Magic","Quantity","PickupCorrection","Position","CombatLoop","DisplayNoticeChecks","NameDisplayChecks","BoardChecks","ClanChecks","ClanEmblemChecks","ClanWarChecks","MarriageChecks","YesNoPromptChecks","TradeYesNoChecks","CharResetChecks","LocationShareChecks","HitReactionSuppression","TitleAndEmoteChecks","PlayerQueryChecks","HyperTextChecks","FishingChecks","AmbienceChecks","MapAmbientChecks","BackgroundMusicChecks","InvisibilityChecks","GhostModeChecks","GhostLimitsChecks","TimedBuffChecks"],
        ["map-continuity"]=["MapContinuity","BookmarkChecks","DisplayNoticeChecks","NameDisplayChecks","BoardChecks","NativeUiBoardChecks","ClanChecks","ClanEmblemChecks","ClanWarChecks","MarriageChecks","YesNoPromptChecks","TradeYesNoChecks","CharResetChecks","PlayerQueryChecks","HyperTextChecks","CharacterConfigChecks","HotbarChecks","LocationShareChecks","FishingChecks","AmbienceChecks","MapAmbientChecks","BackgroundMusicChecks","InvisibilityChecks","GhostModeChecks","GhostLimitsChecks","PortalChecks","MinimapChecks","TimedBuffChecks"],
        ["combat-admission"]=["CombatAdmission","CastCadenceChecks","Run","HitReactionSuppression","DuelChecks","TitleAndEmoteChecks"],
        ["cast-cadence"]=["CombatAdmissionFixture","CastCadenceChecks"],["duel"]=["CombatAdmissionFixture","DuelChecks"],
        ["hotbar"]=["HotbarChecks","CharacterConfigChecks","MinimapChecks"],["player-query"]=["PlayerQueryChecks","HyperTextChecks"],
        ["yes-no"]=["YesNoPromptChecks","TradeYesNoChecks"],["ghost-mode"]=["GhostModeChecks","GhostLimitsChecks"],["board"]=["BoardChecks","NativeUiBoardChecks"],
        ["native-ui-resources"]=["NativeUiResourceChecks","NativeUiRealClientChecks"],["native-ui-left-status"]=["NativeUiLeftStatusRealRender","NativeUiLeftStatusChecks"],
        ["native-ui-hpmp"]=["NativeUiHpMpChecks","NativeUiHpMpRealClientChecks"],["native-ui-chat-window"]=["NativeUiChatWindowChecks","NativeUiChatWindowRealRender"],
        ["native-ui-menu-buttons"]=["NativeUiMenuButtonsChecks","NativeUiMenuButtonsRealClient"],["buddies-codec"]=["BuddyCodecChecks"],
    };
    static bool Subsequence(IEnumerable<string> whole,IReadOnlyList<string> part){int i=0;foreach(var w in whole)if(i<part.Count&&w==part[i])i++;return i==part.Count;}

    /// <summary>MacroChecks' registry probe must pick exactly what the pre-T270 hand-kept filter picked (uistatus86 minus LEFT/CENTER/MINI_MAP).</summary>
    [CheckGroup("auto-discovery",Order=10)]
    static void AutoDiscoveryMacroProbe(string repo){
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-645","synthetic-macro");Directory.CreateDirectory(dir);
        using var res=MacroResources(dir,new Dictionary<int,byte[]>{[3176]=MacroImg(157,124,0,0,(_,_)=>0x0010)},MacroLayoutXml);
        var probed=UiPanelRegistry.Panels.Where(p=>string.Equals(p.Layout,"uistatus86",StringComparison.OrdinalIgnoreCase)&&MacroBuilds(res,p)).Select(p=>p.Id).ToArray();
        string[] kept=[HpMpPanel.PanelId,HpMpHeadBarPanel.PanelId,ExMacroPanel.WindowId,MacroPanel.WindowId,MenuButtonsPanel.PanelId],dropped=["LEFT_WINDOW","CENTER_WINDOW",MiniMapPanel.PanelId];
        Check(Subsequence(probed,kept)&&!probed.Intersect(dropped).Any(),"macro registry probe = pre-T270 exclusion list for the pre-T270 panels: "+string.Join(",",probed));
    }

    [CheckGroup("auto-discovery",Order=20)]
    static async Task AutoDiscoveryChecks(){
        var entries=CheckGroups.Discover(typeof(Checks).Assembly);var names=CheckGroups.Names(entries);
        Check(CheckGroups.Errors(entries).Count==0,"every [CheckGroup] method binds");
        foreach(var g in LegacyGroups)Check(names.Contains(g),"legacy group still on the command line: "+g);
        foreach(var (group,sequence) in LegacySequences){
            var actual=entries.Where(e=>e.Group==group).Select(e=>e.Method.Name).ToList();
            Check(Subsequence(actual,sequence),"legacy order kept in "+group);
            Check(sequence.All(s=>actual.Count(a=>a==s)==1),"legacy members once in "+group);
        }
        Check(entries.Where(e=>e.Group==CheckGroups.Preamble).Select(e=>e.Method.Name).SequenceEqual(LegacySequences[CheckGroups.Preamble]),"preamble is exactly party, pets, polymorph");
        Check(entries.Single(e=>e.Group==CheckGroups.Preamble&&e.Method.Name=="Pets").DeferFailure,"pets failure is deferred like before");
        var reversed=CheckGroups.Discover(typeof(Checks).Assembly.GetTypes().Reverse().Where(t=>!ExcludeFromDiscoveryAttribute.Excluded(t)));
        Check(reversed.Select(e=>e.Label+"@"+e.Group).SequenceEqual(entries.Select(e=>e.Label+"@"+e.Group)),"discovery order is reproducible");
        Check(entries.All(e=>e.Method.DeclaringType!=typeof(CheckFixtures)&&e.Method.DeclaringType?.DeclaringType!=typeof(CheckFixtures)),"[ExcludeFromDiscovery] fixtures stay out of the real run");

        // Fixtures: binding, order, preamble de-duplication and deferred failure.
        var fixture=CheckGroups.Discover([typeof(CheckFixtures.Good)]);var calls=new List<string>();CheckFixtures.Calls=calls;
        Check(CheckGroups.Errors(fixture).Count==0,"fixture binds");
        var args=new[]{"repo-path","output-path","fixture-a","x3"};var reported=new List<string>();
        var deferred=await CheckGroups.Run(fixture,"fixture-a",args,(ok,label)=>reported.Add(label));
        Check(deferred?.Message=="deferred","deferred preamble failure is returned, not thrown");
        Check(string.Join(",",calls)=="pre1,pre2(deferred),order10:repo-path,order20:output-path|x3,async,unordered","preamble first, then Order, then unordered; preamble method not repeated:"+string.Join(",",calls));
        Check(reported.SequenceEqual(["checked"]),"Action<bool,string> parameter receives the check delegate");
        calls.Clear();await CheckGroups.Run(fixture,null,args,(_,_)=>{});
        Check(string.Join(",",calls)=="pre1,pre2(deferred),default","no group runs the default group");
        bool unknown=false;try{await CheckGroups.Run(fixture,"no-such-group",args,(_,_)=>{});}catch(ArgumentException ex){unknown=ex.Message=="Unknown regression group";}
        Check(unknown,"unknown group is rejected with the same message");
        var bad=CheckGroups.Errors(CheckGroups.Discover([typeof(CheckFixtures.Bad)]));
        Check(bad.Count==3&&bad.Any(e=>e.Contains("ReturnsInt"))&&bad.Any(e=>e.Contains("Unbindable"))&&bad.Any(e=>e.Contains("重複")),"bad signatures and duplicates are reported:"+string.Join(" | ",bad));
        bool refused=false;try{await CheckGroups.Run(CheckGroups.Discover([typeof(CheckFixtures.Bad)]),"bad",args,(_,_)=>{});}catch(InvalidOperationException){refused=true;}
        Check(refused,"a run with registration errors refuses to start");
    }

    [ExcludeFromDiscovery]
    static class CheckFixtures {
        internal static List<string> Calls=[];
        internal static class Good {
            [CheckGroup(CheckGroups.Preamble,Order=1),CheckGroup("fixture-a",Order=5)] static void Pre1()=>Calls.Add("pre1");
            [CheckGroup(CheckGroups.Preamble,Order=2,DeferFailure=true)] static void Pre2(){Calls.Add("pre2(deferred)");throw new Exception("deferred");}
            [CheckGroup("fixture-a")] static async Task Unordered(){await Task.Yield();Calls.Add("unordered");}
            [CheckGroup("fixture-a",Order=20)] static void Second(string output,[CheckArg(3)] string third)=>Calls.Add("order20:"+output+"|"+third);
            [CheckGroup("fixture-a",Order=10)] static void First(string repo,Action<bool,string> check){Calls.Add("order10:"+repo);check(true,"checked");}
            [CheckGroup("fixture-a",Order=30)] static async Task Async(bool flag=true){await Task.Yield();if(flag)Calls.Add("async");}
            [CheckGroup(CheckGroups.Default)] static void DefaultOnly()=>Calls.Add("default");
        }
        internal static class Bad {
            [CheckGroup("bad")] static int ReturnsInt()=>0;
            [CheckGroup("bad")] static void Unbindable(int value){}
            [CheckGroup("bad"),CheckGroup("bad",Order=2)] static void Twice(){}
        }
    }
}
