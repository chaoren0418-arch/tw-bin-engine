namespace P06;
public sealed record ProjectileVisual(long Number,int Gfx,int Direction,int Step,int Remaining,int X,int Y,string Frame,long ReleaseAt);
public static class ProjectileMotion {
    // Original BIN base00400000: F1A190 signed 16.16 delta; F1A340 direction;
    // 7C3950 initial step immediately, 7C3AB0 rounds and schedules next step30ms.
    public static int Direction(int x,int y,int tx,int ty,int old){int dx=2*(tx-x),dy=ty-y;if(dx==0&&dy==0)return old;if(20*dy<=24*dx){if(20*dy<=4*dx){if(20*dy<=-4*dx)return 20*dy>-24*dx?1:0;return 2;}return 3;}if(20*dy<=-24*dx){if(20*dy<=-4*dx)return 20*dy<=4*dx?7:6;return 5;}return 4;}
    public static ProjectileVisual? At(ProjectileReport p,int tx,int ty,long release,long now){
        if(now<release)return null;int dx=tx-p.SourceX,dy=ty-p.SourceY,ax=Math.Abs(dx),ay=Math.Abs(dy),major=Math.Max(ax,ay);
        if(major==0)return null;
        int vx=(ax<<16)/major*Math.Sign(dx)*2,vy=(ay<<16)/major*Math.Sign(dy);
        uint fx=unchecked((uint)(2*p.SourceX-32768)<<16),fy=(uint)p.SourceY<<16;
        int remaining=Math.Max(2*ax,ay),step=(int)Math.Min(64,(now-release)/30+1),ix=0,iy=0;
        for(int n=1;n<=step;n++){
            fx=unchecked(fx+(uint)vx);fy=unchecked(fy+(uint)vy);
            ix=(int)(unchecked(fx+0x8000u)>>16)&~1;iy=(int)(unchecked(fy+0x8000u)>>16);remaining--;
            if((ix==2*tx-32768&&iy==ty)||remaining<=0)return null;
        }
        int heading=Direction(p.SourceX,p.SourceY,tx,ty,p.Heading);
        // +12 is preset0/1/2 then A43560 increments it; metadata indices1/2 map to frame0/1.
        return new(p.Number,p.Gfx,heading,step,remaining,(ix+32768)/2,iy,$"{heading}:{(remaining==1?1:0)}",release);
    }
}
// T269 (#643), docs/reference/trade-window-and-projectiles.md §3.2–§3.3 (TW16, static analysis):
// kind 7 `Eruption` (S30 case 7 sub_81C490 FO 0x41B890, start sub_81CF30 FO 0x41C330, per frame sub_81C9D0 FO 0x41BDD0).
// Each segment is its own object that plays its animation once in place. When its frame cursor reaches index 2
// (the third frame) the next segment is built one fixed-point step further (gfx 7374/10145: two steps) and the
// remaining step count (Chebyshev distance) drops by 1. At the end tile, or when fewer than one step remains, no
// segment is kept: a kind 6 `EarthRising` (sub_81C340) of the 109 param gfx is built instead. Segments never hit.
public sealed record EruptionSegment(int Index,int X,int Y,long Offset);
/// <param name="TerminalOffset">Offset from the release of the terminal EarthRising; -1 when the chain never ends (Chains false).</param>
public sealed record EruptionPlan(int Direction,EruptionSegment[] Segments,int EndX,int EndY,long TerminalOffset,bool Chains);
public static class EruptionMotion {
    public const int MaxSegments=256;
    // sub_81C730(…, 2) for these two gfx (§3.3).
    public static int StepsPerSegment(int gfx)=>gfx is 7374 or 10145?2:1;
    // sub_A43560 increments the cursor before each display (index1 is shown first, for Steps[1] ms), so the
    // cursor reaches 2 Steps[1] ms after the segment starts. With fewer than 3 frames it wraps to 0 first.
    public static long? NextSegmentAfter(EffectSprite segment)=>segment.Steps.Length>=3?segment.Steps[1].Milliseconds:null;
    public static EruptionPlan Plan(int sx,int sy,int tx,int ty,int heading,EffectSprite segment){
        int direction=ProjectileMotion.Direction(sx,sy,tx,ty,heading);if(direction>3)direction-=4;
        int ax=Math.Abs(tx-sx),ay=Math.Abs(ty-sy),major=Math.Max(ax,ay),remaining=major,per=StepsPerSegment(segment.Gfx);
        int vx=major==0?0:(ax<<16)/major*Math.Sign(tx-sx)*2,vy=major==0?0:(ay<<16)/major*Math.Sign(ty-sy);
        uint fx=unchecked((uint)(2*sx-32768)<<16),fy=(uint)sy<<16;
        var segments=new List<EruptionSegment>{new(0,sx,sy,0)};
        if(NextSegmentAfter(segment) is not long gap)return new(direction,segments.ToArray(),tx,ty,-1,false);
        long at=0;
        while(true){
            at+=gap;
            for(int k=0;k<per;k++){fx=unchecked(fx+(uint)vx);fy=unchecked(fy+(uint)vy);}
            int ix=(int)(unchecked(fx+0x8000u)>>16)&~1,iy=(int)(unchecked(fy+0x8000u)>>16);remaining--;
            if(ix==2*tx-32768&&iy==ty||remaining<1||segments.Count>=MaxSegments)return new(direction,segments.ToArray(),tx,ty,at,true);
            segments.Add(new(segments.Count,(ix+32768)/2,iy,at));
        }
    }
}
// R4-G, TWP §3.2–§3.3: global dword_13CCBA4. Every EarthRising construction (S30 kind 6, kind 7 terminal) adds 29;
// the kind 7 terminal sets it back to 0 right after building its EarthRising. gfx 10147 (EarthRising, sub_A0BA50) and
// gfx 10145 (Eruption segment) add it to the layer depth, capped at 255, so stacked instances draw one above another.
// 推測: the value is taken when the object is built (a per-draw read of the global would give every live instance
// the same depth, contradicting "一個比一個高"), and Draw.Z (the SPR block z) stands for the native layer depth.
// `+40` (dword_13CD84C[gfx] = list.spr field 104, actions-and-names.md) is EffectSprite.TileType of the drawn gfx:
// segments use their own gfx, the terminal EarthRising uses the param gfx's own 104 (DrawNativeEffect(param)).
public static class EarthDepth {
    public const int Step=29,Max=255;
    public static bool Stacks(int gfx)=>gfx is 10147 or 10145;
    public static int Z(int z,int depth)=>depth<=0?z:Math.Min(Max,z+depth);
}
public sealed partial class Renderer {
    // Lazily created: constructor-free test renderers do not run field initializers.
    Dictionary<long,EruptionPlan>? eruptionPlans;Dictionary<long,(int X,int Y)>? eruptionEnds;
    // dword_13CCBA4 and the value each object took when built. Index -1 = S30 kind 6, -2 = kind 7 terminal, ≥0 = segment.
    int earthCounter;Dictionary<(long Number,int Index),int>? earthDepths;
    /// <summary>目前的 dword_13CCBA4（測試用）。</summary>
    public int EarthDepthCounter=>earthCounter;
    int EarthRisingBuilt(long number,int index){
        var d=earthDepths??=[];if(d.TryGetValue((number,index),out int v))return v;
        earthCounter+=EarthDepth.Step;d[(number,index)]=earthCounter;return earthCounter;
    }
    int EarthDepthOf(long number,int index)=>earthDepths!=null&&earthDepths.TryGetValue((number,index),out int v)?v:0;
    int SegmentBuilt(long number,int index){
        var d=earthDepths??=[];if(!d.TryGetValue((number,index),out int v)){v=earthCounter;d[(number,index)]=v;}return v;
    }
    void ForgetEarthDepth(long number){if(earthDepths!=null)foreach(var k in earthDepths.Keys.Where(k=>k.Number==number).ToArray())earthDepths.Remove(k);}
    // Called from ComposeMagic's world-epoch reset.
    void ClearEruptions(){eruptionPlans?.Clear();eruptionEnds?.Clear();earthDepths?.Clear();earthCounter=0;}
    /// <summary>The kind 7 plan fixed at the caster `!` (null before it starts).</summary>
    public EruptionPlan? EruptionPlanFor(long number)=>eruptionPlans?.GetValueOrDefault(number);
    // S30 kind 7. Starts at the caster `!`; the end is the target's current position at that moment (81CF30).
    void ComposeEruption(MagicEffect e,EffectSprite effect,WorldSession session,List<Draw> draws,List<MagicVisual> visuals,List<UndrawnMagic> undrawn,long release,long now){
        var plans=eruptionPlans??=[];
        if(!plans.TryGetValue(e.Number,out var plan)){
            var aim=session.VisibleTargetPosition(e.Target);
            plan=EruptionMotion.Plan(e.SourceX,e.SourceY,aim?.X??e.TargetX,aim?.Y??e.TargetY,e.Heading,effect);plans[e.Number]=plan;
            magicFlights[e.Number]=(plan.EndX,plan.EndY,release,plan.Chains?release+plan.TerminalOffset:release);
            foreach(long stale in plans.Keys.Where(k=>!magicFlights.ContainsKey(k)).ToArray()){plans.Remove(stale);eruptionEnds?.Remove(stale);}
        }
        foreach(var s in plan.Segments){
            long at=release+s.Offset;if(now<at)continue;
            // Segment built at its offset: gfx 10145 takes the current dword_13CCBA4 as extra depth.
            int segmentDepth=SegmentBuilt(e.Number,s.Index);if(now-at>=effect.Duration)continue;
            var frame=DrawNativeEffect(e.Gfx,plan.Direction,s.X,s.Y,now-at,session,draws,"magic",depth:EarthDepth.Stacks(e.Gfx)?segmentDepth:0);
            if(frame!=null)visuals.Add(new(e.Number,e.Gfx,s.X,s.Y,plan.Direction,frame.Key,"segment",at));
        }
        if(!plan.Chains){
            undrawn.Add(new(e.Number,e.Gfx,7,plan.Segments.Select(s=>(s.X,s.Y)).ToArray(),effect.Impact,"segment has fewer than 3 frames: cursor never reaches index 2, no next segment and no terminal EarthRising"));
            return;
        }
        long start=release+plan.TerminalOffset;if(now<start)return;
        if(effect.Impact<0){undrawn.Add(new(e.Number,e.Gfx,7,plan.Segments.Select(s=>(s.X,s.Y)).ToArray(),-1,"no 109 param: no terminal EarthRising"));return;}
        var ends=eruptionEnds??=[];
        // EarthRising start (81CEB0): moves to the target's current position if the target still exists.
        if(!ends.TryGetValue(e.Number,out var end)){var aim=session.VisibleTargetPosition(e.Target);end=(aim?.X??plan.EndX,aim?.Y??plan.EndY);ends[e.Number]=end;}
        // Terminal EarthRising built (+29), then dword_13CCBA4 = 0 (TWP §3.3), once per eruption.
        bool built=earthDepths!=null&&earthDepths.ContainsKey((e.Number,-2));int depth=EarthRisingBuilt(e.Number,-2);if(!built)earthCounter=0;
        // The terminal object is always the EarthRising class, whatever the param gfx's own 109 kind is.
        ComposeEarthRising(e,effect.Impact,plan.Direction,end.X,end.Y,start,session,draws,visuals,now,"terminal",depth);
    }
    // kind 6 EarthRising (sub_81C7C0): no movement, plays once; every `!` frame hits while damage≠0
    // (damage is never cleared); never processes `]`. Only gfx 10147 uses the stacking depth (sub_A0BA50).
    void ComposeEarthRising(MagicEffect e,int gfx,int direction,int x,int y,long start,WorldSession session,List<Draw> draws,List<MagicVisual> visuals,long now,string phase,int depth=0){
        var rising=NativeEffect(gfx);if(rising==null||now<start)return;
        if(e.DamageWord>0&&e.Target!=0&&rising.LatestHit(start,now) is long hitAt)MagicHit(session,e.Target,hitAt);
        var frame=DrawNativeEffect(gfx,direction,x,y,now-start,session,draws,"magic",depth:EarthDepth.Stacks(gfx)?depth:0);
        if(frame!=null)visuals.Add(new(e.Number,gfx,x,y,direction,frame.Key,phase,start));
    }
    readonly Dictionary<string,Frame> projectileFrames=[];
    sealed record Flight(int X,int Y,long Release,long Impact);
    readonly Dictionary<long,Flight> projectileFlights=[];
    readonly Dictionary<uint,CombatAnimation> projectileHits=[];
    CombatAnimation? WithProjectileHit(uint id,CombatAnimation? current,long now,bool dead){if(!dead&&projectileHits.TryGetValue(id,out var hit)&&hit.At<=now&&(current==null||hit.At>current.At))return hit;return current;}
    int projectileEpoch=-1;
    public ProjectileVisual? CurrentProjectileVisual {get;private set;}
    public ProjectileVisual[] ProjectileVisuals {get;private set;}=[];
    void ComposeProjectile(WorldSession session,List<Draw> draws,long now){
        CurrentProjectileVisual=null;
        var visuals=new List<ProjectileVisual>();
        if(projectileEpoch!=session.WorldEpoch){projectileFlights.Clear();projectileHits.Clear();projectileEpoch=session.WorldEpoch;}
        foreach(var p in session.Projectiles){
        int delay=p.Timing?.ReleaseMs??RangedRules.ReleaseFor(p.CasterGfx)??0;long projectileRelease=(p.ActionAt??p.At)+delay;
        bool own=p.Attacker==session.Actor?.ObjectId;
        var other=session.Objects.FirstOrDefault(o=>o.ObjectId==p.Attacker);
        if(!projectileFlights.ContainsKey(p.Number)&&(own?session.OwnDead||session.SelfAppearancePending||p.Timing is {} ownTiming&&!ownTiming.Matches(session.Actor!.Gfx,session.Actor.Weapon):other!=null&&(other.Dead||other.AppearancePending||p.Timing is {} otherTiming&&!otherTiming.Matches(other.Gfx,other.Action))))continue;
        if(now<projectileRelease||now-projectileRelease>2000)continue;
        if(!projectileFlights.TryGetValue(p.Number,out var flight)){
            // Original7C3950 resolves a still-known target once at release. A miss(target0)
            // and removed targets retain original S30 destination; no chasing in flight.
            var target=session.Objects.FirstOrDefault(o=>o.ObjectId==p.Target);
            int tx=target?.X??p.TargetX,ty=target?.Y??p.TargetY;long impact=projectileRelease;
            while(impact<projectileRelease+2000&&ProjectileMotion.At(p,tx,ty,projectileRelease,impact)!=null)impact+=30;
            flight=new(tx,ty,projectileRelease,impact);projectileFlights[p.Number]=flight;
        }
        if(p.DamageWord>0&&p.Target!=0&&now>=flight.Impact&&!session.IsHitReactionSuppressed(p.Target)&&(!projectileHits.TryGetValue(p.Target,out var hit)||hit.At<flight.Impact))projectileHits[p.Target]=new(2,flight.Impact,false);
        var v=ProjectileMotion.At(p,flight.X,flight.Y,flight.Release,now);
        if(p.Gfx!=66){
            var effect=NativeEffect(p.Gfx);if(effect==null)continue;
            if(v==null){
                // The kind 3 arrow class (7C36C0) never reads the 109 param: 2349 does not draw 219 (SV#10).
                if(now>=flight.Impact&&effect.Kind!=3&&effect.Impact>=0){
                    DrawNativeEffect(effect.Impact,p.Heading,flight.X,flight.Y,now-flight.Impact,session,draws,"projectile");
                    // R2-H: the landing effect's own `]` frames spawn children (default Effect class).
                    ObserveLandingFrames(session,$"projectile-impact:{p.Number}",NativeEffect(effect.Impact),now-flight.Impact,flight.X,flight.Y,p.Heading,now);
                }
                continue;
            }
            // Arrow class 3 uses the original fly sequence, including one-frame arrows.
            int? step=effect.Kind==3&&effect.Steps.Length==3?(v.Remaining==1?2:1):null;
            WithholdFlightFrames(session,$"projectile-fly:{p.Number}",effect,now-flight.Release,v.X,v.Y,v.Direction,now);
            var native=DrawNativeEffect(p.Gfx,v.Direction,v.X,v.Y,now-flight.Release,session,draws,"projectile",true,step);
            if(native!=null){v=v with{Frame=native.Key};CurrentProjectileVisual=v;visuals.Add(v);}continue;
        }
        if(v==null||!session.SceneContains(v.X,v.Y))continue;
        if(projectileFrames.Count==0){
            var j=Json.Read(Path.Combine(assets.Root,"objects/projectile-66.json"));
            foreach(var f in j.GetProperty("frames").EnumerateArray())projectileFrames.Add(f.S("key"),new(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(2,b.GetProperty("pixels").EnumerateArray().Select(pix=>{var n=pix.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new Pixel(n[0],n[1],(ushort)n[2]);}).ToArray()))).ToArray()));
        }
        var frame=projectileFrames[v.Frame];CurrentProjectileVisual=v;visuals.Add(v);
        // Original SPR depth keys participate in the same map compositor; no global overlay.
        draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(v.X-Coordinates.MinX)+b.A,v.Y-Coordinates.MinY+b.B,b.Z,1100000+b.Order,"projectile",b.Order,-1,-1,b.Tile)));
        }
        ProjectileVisuals=visuals.ToArray();
        foreach(var key in projectileFlights.Where(kv=>now-kv.Value.Release>10000).Select(kv=>kv.Key).ToArray())projectileFlights.Remove(key);
    }
}
