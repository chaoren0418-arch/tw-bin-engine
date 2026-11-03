namespace P06;
public sealed record MagicVisual(long Number,int Gfx,int X,int Y,int Direction,string Frame,string Phase,long At);
public static class MagicMotion {
    // 8AB390 initializes frame index0; 8ABC10 immediately calls A43560 (++index),
    // then schedules the selected frame duration. The terminal wrap displays index0 once.
    public static Frame PureFrame(ObjectAppearance a,string action,long elapsed){var seq=a.Sequences[(action,0)];double duration=a.Duration(action);return a.At(action,0,(elapsed+seq[0].Milliseconds!.Value)%duration);}
    // Fixed BIN effect4:8211E0 resolves direction &3;821360 steps every60ms, not bow30ms.
    public static (int X,int Y,int Direction,int Step,bool Arrived) At(MagicEffect p,int tx,int ty,long release,long now){
        int ax=Math.Abs(tx-p.SourceX),ay=Math.Abs(ty-p.SourceY),major=Math.Max(ax,ay),direction=ProjectileMotion.Direction(p.SourceX,p.SourceY,tx,ty,p.Heading)%4;
        if(major==0)return(tx,ty,direction,0,true);
        int vx=(ax<<16)/major*Math.Sign(tx-p.SourceX)*2,vy=(ay<<16)/major*Math.Sign(ty-p.SourceY);
        uint fx=unchecked((uint)(2*p.SourceX-32768)<<16),fy=(uint)p.SourceY<<16;int remaining=Math.Max(2*ax,ay),steps=(int)Math.Min(128,(now-release)/60+1),ix=2*p.SourceX-32768,iy=p.SourceY;
        for(int n=1;n<=steps;n++){fx=unchecked(fx+(uint)vx);fy=unchecked(fy+(uint)vy);ix=(int)(unchecked(fx+0x8000u)>>16)&~1;iy=(int)(unchecked(fy+0x8000u)>>16);remaining--;if(ix==2*tx-32768&&iy==ty||remaining<=0)return((ix+32768)/2,iy,direction,n,true);}
        return((ix+32768)/2,iy,direction,steps,false);
    }
}
public sealed partial class Renderer {
    readonly Dictionary<int,ObjectAppearance> magicAppearances=[];
    readonly Dictionary<long,(int X,int Y,long Release,long Impact)> magicFlights=[];
    int magicEpoch=-1;
    public MagicVisual[] MagicVisuals {get;private set;}=[];
    ObjectAppearance MagicAppearance(int gfx){if(!magicAppearances.TryGetValue(gfx,out var a)){a=new(Path.Combine(assets.Root,$"objects/magic-{gfx}.json"));magicAppearances[gfx]=a;}return a;}
    void ComposeMagic(WorldSession session,List<Draw> draws,long now){
        if(magicEpoch!=session.WorldEpoch){magicFlights.Clear();magicEpoch=session.WorldEpoch;}var visuals=new List<MagicVisual>();
        foreach(var e in session.MagicEffects){
            int gfx=e.Gfx,x=e.TargetX,y=e.TargetY,d=e.Heading;long at=e.At;Frame? frame=null;string phase="heal";
            if(gfx==11722){var a=MagicAppearance(gfx);if(now-at>=a.Duration("heal"))continue;frame=MagicMotion.PureFrame(a,"heal",now-at);}
            else if(gfx is 221 or 2165 or 189 or 191 or 197){phase="effect";var a=MagicAppearance(gfx);if(now-at>=a.Duration(phase))continue;frame=MagicMotion.PureFrame(a,phase,now-at);}
            else if(gfx==167){
                long release=(e.ActionAt??e.At)+(e.Timing?.ReleaseMs??MagicRules.ReleaseFor(e.CasterGfx));
                if(now<release)continue;
                if(!magicFlights.TryGetValue(e.Number,out var flight)){
                    var caster=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Caster);if(e.Caster==session.Actor?.ObjectId?session.OwnDead||session.SelfAppearancePending||e.Timing is {} ownTiming&&!ownTiming.Matches(session.Actor!.Gfx,session.Actor.Weapon):caster==null||caster.Dead||caster.AppearancePending||e.Timing is {} otherTiming&&!otherTiming.Matches(caster.Gfx,caster.Action))continue;
                    var target=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Target);int tx=target?.X??e.TargetX,ty=target?.Y??e.TargetY;long impact=release;
                    while(impact<release+6000&&!MagicMotion.At(e,tx,ty,release,impact).Arrived)impact+=60;
                    flight=(tx,ty,release,impact);magicFlights[e.Number]=flight;
                }
                var motion=MagicMotion.At(e,flight.X,flight.Y,flight.Release,Math.Min(now,flight.Impact));x=motion.X;y=motion.Y;d=motion.Direction;
                if(now<flight.Impact){phase="fly";frame=MagicAppearance(167).Frames[$"{d}:{(motion.Step-1)%4}"];at=flight.Release;}
                else {gfx=219;phase="impact";at=flight.Impact;var a=MagicAppearance(gfx);if(now-at>=a.Duration("impact"))continue;frame=MagicMotion.PureFrame(a,"impact",now-at);if(e.Target!=0&&e.DamageWord>0&&(!projectileHits.TryGetValue(e.Target,out var hit)||hit.At<at))projectileHits[e.Target]=new(2,at,false);}
            }
            if(frame==null||!session.SceneContains(x,y))continue;visuals.Add(new(e.Number,gfx,x,y,d,frame.Key,phase,at));draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1200000+b.Order,"magic",b.Order,-1,-1,b.Tile)));
        }
        MagicVisuals=visuals.ToArray();foreach(var k in magicFlights.Where(x=>now-x.Value.Release>15000).Select(x=>x.Key).ToArray())magicFlights.Remove(k);
    }
}
