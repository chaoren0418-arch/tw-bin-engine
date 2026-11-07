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
        return new(p.Number,66,heading,step,remaining,(ix+32768)/2,iy,$"{heading}:{(remaining==1?1:0)}",release);
    }
}
public sealed partial class Renderer {
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
        int? delay=p.Timing?.ReleaseMs??RangedRules.ReleaseFor(p.CasterGfx);if(delay==null)continue;long projectileRelease=(p.ActionAt??p.At)+delay.Value;
        bool own=p.Attacker==session.Actor?.ObjectId;
        var other=session.Objects.FirstOrDefault(o=>o.ObjectId==p.Attacker);
        if(!projectileFlights.ContainsKey(p.Number)&&(own?session.OwnDead||session.SelfAppearancePending||p.Timing is {} ownTiming&&!ownTiming.Matches(session.Actor!.Gfx,session.Actor.Weapon):other==null||other.Dead||other.AppearancePending||p.Timing is {} otherTiming&&!otherTiming.Matches(other.Gfx,other.Action)))continue;
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
        var v=ProjectileMotion.At(p,flight.X,flight.Y,flight.Release,now);if(v==null||!session.SceneContains(v.X,v.Y))continue;
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
