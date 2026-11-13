using P06;
using System.Security.Cryptography;
using System.Text.Json;

// T461 (#1046): action timing comes from list.spr action metadata (R1/R2 of
// docs/reference/asset-recipes/action-timing.md), not from server spr_action numbers in players/manifest.json.
// Every read point is exercised against an authored synthetic tree: tick counts and event markers only,
// no game pixels, no copied client/server data. Own process/group: PlayerProfiles and the cadence caches are process-wide.
partial class Checks {
    static bool ThrowsInvalidData(Action run){try{run();return false;}catch(InvalidDataException){return true;}}
    static ActionStep[] TimingSteps(params (int Ticks,string Events)[] s)=>s.Select(x=>new ActionStep(x.Ticks,x.Events)).ToArray();

    static void ActionTimingRules(){
        // R1: 40 × Σticks.
        Check(ActionTiming.ServerMs(TimingSteps((0,""),(3,""),(3,""),(4,""),(6,"!"),(4,"")))==800,"R1 server interval = 40 × tick total (zero-tick first frame counts as 0)");
        Check(ThrowsInvalidData(()=>ActionTiming.ServerMs([]))&&ThrowsInvalidData(()=>ActionTiming.ServerMs(TimingSteps((0,""),(0,""))))&&ThrowsInvalidData(()=>ActionTiming.ServerMs(TimingSteps((3,""),(-1,"")))),"R1 refuses empty, zero-length or negative-tick actions (no made-up value)");
        // R2: floor(ticks×1000/24) of every frame before the first '!'.
        Check(ActionTiming.FrameMs(3)==125&&ActionTiming.FrameMs(4)==166&&ActionTiming.FrameMs(6)==250&&ActionTiming.FrameMs(0)==0,"native frame = floor(ticks × 1000 / 24)");
        Check(ActionTiming.ReleaseMs(TimingSteps((0,""),(3,""),(3,"[13"),(4,""),(6,"!"),(4,"!")))==416,"R2 release = floor sum before the first '!' (later '!' ignored, other events ignored)");
        Check(ActionTiming.ReleaseMs(TimingSteps((5,"!"),(5,"")))==0&&ActionTiming.ReleaseMs(TimingSteps((5,""),(5,"[1569")))==null,"R2: '!' on the first frame releases at 0; no '!' means no release point");
        Check(ActionTiming.ActionNumber("walk",4)==4&&ActionTiming.ActionNumber("attack",20)==21&&ActionTiming.ActionNumber("attack",0)==1&&ActionTiming.ActionNumber("spellDir",50)==18&&ActionTiming.ActionNumber("spellNoDir",46)==19,"R1 action numbers: stance+0 walk, stance+1 attack, 18, 19");
        var element=JsonDocument.Parse("""{"sequences":[{"action":"attack","direction":1,"sequence":[{"ticks":99,"events":""}]},{"action":"attack","direction":0,"sequence":[{"ticks":2,"events":"!"},{"ticks":3}]}]}""").RootElement;
        Check(ActionTiming.Steps(element,"attack").SequenceEqual(TimingSteps((2,"!"),(3,""))),"sequence reader takes direction 0 ticks/events; missing events = none");
        var noTicks=JsonDocument.Parse("""{"sequences":[{"action":"attack","direction":0,"sequence":[{"milliseconds":640,"events":"!"}]}]}""").RootElement;
        Check(ThrowsInvalidData(()=>ActionTiming.Steps(noTicks,"attack")),"a sequence without list.spr ticks is refused, not converted from milliseconds");
    }

    sealed record TimingProfile(int Gfx,int Weapon,Dictionary<string,(int Ticks,string Events)[]> Sequences,bool CorruptSha=false,bool DropTicks=false,object? Recorded=null);

    static void ActionTimingFixture(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1046","synthetic-timing");
        string assets=Path.Combine(root,"assets");Directory.CreateDirectory(Path.Combine(assets,"players"));Directory.CreateDirectory(Path.Combine(assets,"objects"));
        (int,string)[] walk=[(4,""),(4,""),(4,""),(4,"")]; // R1 640
        var bare=new Dictionary<string,(int,string)[]>{
            ["walk"]=walk,
            ["attack"]=[(0,""),(3,""),(3,""),(4,""),(6,"!"),(4,"")],          // R1 800, R2 416
            ["spellDir"]=[(3,""),(3,""),(3,""),(6,"!"),(3,"")],               // R1 720, R2 375
            ["spellNoDir"]=[(4,""),(3,""),(3,"!"),(6,""),(4,"")],             // R1 800, R2 291
        };
        var sword=new Dictionary<string,(int,string)[]>(bare){["attack"]=[(2,""),(4,""),(4,""),(6,"!"),(6,"")]}; // R1 880
        var bow=new Dictionary<string,(int,string)[]>(bare){["attack"]=[(0,""),(4,""),(3,"[13"),(8,"!"),(8,""),(7,"")]}; // R1 1200, R2 291
        var silent=new Dictionary<string,(int,string)[]>(bare){["spellDir"]=[(3,""),(3,""),(3,"")]};       // no '!'
        TimingProfile[] list=[
            new(900,0,bare),
            // The manifest still may carry the preparer's record; the product must not read it.
            new(900,4,sword,Recorded:new{serverActionMs=new{walk=1,attack=1,spellDir=1,spellNoDir=1},castReleaseMs=new{spellDir=1,spellNoDir=1}}),
            new(900,20,bow,Recorded:new{projectileReleaseMs=1}),
            new(901,0,silent),
            new(902,0,bare,CorruptSha:true),
            new(903,0,bare,DropTicks:true),
        ];
        var profiles=new List<Dictionary<string,object?>>();
        foreach(var p in list){
            byte[] json=JsonSerializer.SerializeToUtf8Bytes(new{
                gfx=p.Gfx,weapon=p.Weapon,stepMs=640,
                actionMs=p.Sequences.ToDictionary(s=>s.Key,s=>s.Value.Sum(f=>ActionTiming.FrameMs(f.Ticks))),
                sequences=p.Sequences.Select(s=>new{action=s.Key,direction=0,sequence=s.Value.Select(f=>p.DropTicks?(object)new{milliseconds=ActionTiming.FrameMs(f.Ticks),events=f.Events}:new{ticks=f.Ticks,events=f.Events})})
            });
            string path=$"players/synthetic-{p.Gfx}-{p.Weapon}.json";File.WriteAllBytes(Path.Combine(assets,path),json);
            var row=new Dictionary<string,object?>{["gfx"]=p.Gfx,["weapon"]=p.Weapon,["path"]=path,["stepMs"]=640,
                ["sha256"]=p.CorruptSha?new string('0',64):Convert.ToHexString(SHA256.HashData(json))};
            if(p.Recorded!=null)foreach(var prop in JsonSerializer.SerializeToElement(p.Recorded).EnumerateObject())row[prop.Name]=prop.Value;
            profiles.Add(row);
        }
        // The two fallback files MagicRules/RangedRules read when there is no cadence or no caster profile.
        File.WriteAllBytes(Path.Combine(assets,"objects/0.json"),JsonSerializer.SerializeToUtf8Bytes(new{sequences=new[]{new{action="spellDir",direction=0,sequence=new[]{new{ticks=6,events=""},new{ticks=6,events=""},new{ticks=4,events="!"}}}},castReleaseMs=new{spellDir=1}}));       // R2 500
        File.WriteAllBytes(Path.Combine(assets,"objects/0-weapon-20.json"),JsonSerializer.SerializeToUtf8Bytes(new{sequences=new[]{new{action="attack",direction=0,sequence=new[]{new{ticks=0,events=""},new{ticks=12,events="[13"},new{ticks=1,events="!"}}}},projectileReleaseMs=1})); // R2 500
        byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(new{schema="p24-player-profiles-v1",profiles});
        File.WriteAllBytes(Path.Combine(assets,"players/manifest.json"),manifest);
        File.WriteAllBytes(Path.Combine(assets,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{
            files=new[]{new{path="players/manifest.json",size=manifest.Length,sha256=Convert.ToHexString(SHA256.HashData(manifest))}}
        }));
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(root,"build","offline-checks")+Path.DirectorySeparatorChar);
    }

    [CheckGroup("action-timing")]
    static void ActionTimingChecks(string repo){
        ActionTimingRules();
        ActionTimingFixture(repo);

        // Manifest: the three server-derived fields are optional and only kept as the preparer's record.
        Check(PlayerProfiles.Get(900,0) is {ServerActionMs.Count:0,CastReleaseMs.Count:0,ProjectileReleaseMs:null},"manifest without serverActionMs/castReleaseMs/projectileReleaseMs loads");
        Check(PlayerProfiles.Get(900,4)!.ServerActionMs["attack"]==1&&PlayerProfiles.Get(900,20)!.ProjectileReleaseMs==1,"fixture: recorded manifest values are deliberately wrong (1)");

        // ActionCadence.Capture (combat/cast cooldown, send throttle): R1 of the same list.spr sequence.
        var attack=ActionCadence.Capture(900,0,1,0,0);var sword=ActionCadence.Capture(900,4,1,0,0);var bowShot=ActionCadence.Capture(900,20,1,0,0);
        Check(attack.ServerBaseMs==800&&sword.ServerBaseMs==880&&bowShot.ServerBaseMs==1200,"Capture server base = 40 × attack ticks per stance (recorded 1 ignored)");
        var dir=ActionCadence.Capture(900,0,18,0,0);var noDir=ActionCadence.Capture(900,0,19,0,0);
        Check(dir.ServerBaseMs==720&&noDir.ServerBaseMs==800,"Capture spellDir/spellNoDir server base = 40 × action 18/19 ticks");
        Check(attack.NativeBaseMs==832&&attack.ReleaseBaseMs==416&&dir.ReleaseBaseMs==375,"native length and release stay the floor-per-frame schedule");
        Check(ActionCadence.Capture(900,0,1,1,0).ServerIntervalMs==(int)(800*0.755)&&ActionCadence.Capture(900,0,1,2,0).ServerIntervalMs==1600,"haste ×0.755 / slow ×2 apply to the R1 base (existing factors)");
        Check(dir.CastWaitMs==Math.Max(dir.NativeBaseMs,720)&&ActionCadence.Capture(900,0,18,0,0,reuse:10).CastWaitMs==Math.Max(dir.NativeBaseMs+10,720),"cast reuse floor is the R1 interval");
        Check(ActionCadence.Capture(900,46,1,0,0).ServerBaseMs==800,"legal stance without a profile uses the bare-hand sequence (sub_A3EF40 fallback)");

        // CombatRules.DurationFor (attack acknowledgement fallback) and ActionCadence.ServerMs.
        Check(CombatRules.DurationFor(900,0)==800&&CombatRules.DurationFor(900,4)==880&&CombatRules.DurationFor(900,20)==1200,"DurationFor = R1 attack interval");
        Check(ActionCadence.ServerMs(900,0,19)==800&&ThrowsInvalidData(()=>CombatRules.DurationFor(7777,0)),"unknown gfx has no cadence (no made-up value)");

        // MagicRules.CastMsFor / CastMs: max(skill reuse, R1 cast interval).
        int dirSkill=Enumerable.Range(1,250).First(id=>MagicSpells.Get(id) is {Action:18}),noDirSkill=Enumerable.Range(1,250).First(id=>MagicSpells.Get(id) is {Action:19});
        Check(MagicRules.CastMsFor(dirSkill,900,0)==720&&MagicRules.CastMsFor(noDirSkill,900,0)==800,"CastMsFor = R1 action 18/19 interval");

        // MagicRules.ReleaseFor (cast release without cadence): R2 of the caster profile, else the gfx 0 fallback (R2 of objects/0.json).
        Check(MagicRules.ReleaseMs==500,"gfx 0 cast release = R2 of objects/0.json spellDir (its castReleaseMs field 1 ignored)");
        Check(MagicRules.ReleaseFor(900)==375,"caster gfx profile: R2 spellDir release");
        Check(MagicRules.ReleaseFor(901)==500,"spellDir without '!' falls back to the gfx 0 release");
        Check(MagicRules.ReleaseFor(7777)==500,"gfx without a prepared profile falls back to the gfx 0 release");
        Check(MagicRules.ReleaseFor(902)==500&&MagicRules.ReleaseFor(903)==500,"unreadable profile (SHA mismatch / no ticks) falls back instead of breaking rendering");

        // RangedRules.ReleaseFor (arrow release without cadence): bow profile only.
        Check(RangedRules.ReleaseMs==500,"gfx 0 bow release = R2 of objects/0-weapon-20.json attack (its projectileReleaseMs field 1 ignored)");
        Check(RangedRules.ReleaseFor(900)==291,"bow profile: R2 of action 21");
        Check(RangedRules.ReleaseFor(901)==null,"no bow profile: null even though the bare-hand attack has '!' (not an arrow release)");
        Check(RangedRules.ReleaseFor(7777)==null,"unknown gfx: null");

        // Integrity and missing ticks stay explicit failures on the cadence path.
        Check(ThrowsInvalidData(()=>ActionCadence.Capture(902,0,1,0,0))&&ThrowsInvalidData(()=>CombatRules.DurationFor(902,0)),"profile SHA mismatch refused");
        Check(ThrowsInvalidData(()=>ActionCadence.Capture(903,0,1,0,0))&&ThrowsInvalidData(()=>CombatRules.DurationFor(903,0)),"profile without list.spr ticks refused");

        // Class gfx set: no class table; prepared profiles only, gfx always from S87/S76.
        Check(PlayerProfiles.IsCharacter(900)&&PlayerProfiles.IsCharacter(901)&&!PlayerProfiles.IsCharacter(7777)&&!PlayerProfiles.IsCharacter(61),"IsCharacter = has a prepared profile (no class table: gfx 61 is not assumed)");
        Check(PlayerProfiles.ForGfx(900) is {Weapon:0}&&PlayerProfiles.ForGfx(7777)==null,"ForGfx picks the lowest prepared stance");
    }
}
