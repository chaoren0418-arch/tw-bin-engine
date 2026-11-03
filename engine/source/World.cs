namespace P06;
public static class Coordinates {
    public const int MinX=32576,MinY=32896,MaxX=32639,MaxY=33023;
    public const int Columns=MaxX-MinX+1,Rows=MaxY-MinY+1;
    public static bool Inside(int x,int y)=>x>=MinX&&x<=MaxX&&y>=MinY&&y<=MaxY;
    public static PointF Screen(double x,double y)=>new((float)(24*(x-MinX+y-MinY)+24),(float)(12*(y-MinY-(x-MinX))+768));
    public static Point Pick(double x,double y) {var a=(x-24)/24;var b=(y-768)/12;return new(MinX+(int)Math.Floor((a-b)/2+.5),MinY+(int)Math.Floor((a+b)/2+.5));}
    public static (int Block,int Local) Split(int world) {int b=(int)Math.Floor(world/64.0);return(b+0x7e00,world-64*b);}
}
// This model owns only offline movement and animation. No transport or browser is constructed.
public sealed class World {
    public Assets Assets {get;}
    public int X {get;private set;}
    public int Y {get;private set;}
    public int Direction {get;private set;}=5;
    public bool Walking {get;private set;}
    public int FrameIndex {get;private set;}
    public int? Intent {get;private set;}
    public double Elapsed {get;private set;}
    public double DiscardedMs {get;private set;}
    public long Steps {get;private set;}
    public string BoundaryNotice {get;private set;}="";
    ObjectAppearance? onlineAppearance;double presentationAge;
    public int NetworkStepMs {get;private set;}=MovementRules.StepMs;
    public ObjectAppearance? OnlineAppearance=>onlineAppearance;
    public void SetOnlineAppearance(ObjectAppearance? appearance){if(ReferenceEquals(onlineAppearance,appearance))return;Stop();onlineAppearance=appearance;presentationAge=0;}
    public Frame Frame=>onlineAppearance?.At(Walking?"walk":"breath",Direction,Walking&&networkAnimation?presentationAge*onlineAppearance.StepMs/NetworkStepMs:presentationAge)??Assets.Frames[Assets.Sequences[(Walking?"walk":"breath",Direction)][FrameIndex]];
    public bool AllowStreaming {get;set;}
    public MapBounds? StreamingBounds {get;set;}
    bool Inside(int x,int y)=>AllowStreaming?(StreamingBounds??new MapBounds(32256,32768,32767,33279)).Contains(x,y):Coordinates.Inside(x,y);
    public PointF DisplayPosition=>Coordinates.Screen(X+(Walking?Assets.Directions[Direction].Dx*FrameIndex/4.0:0),Y+(Walking?Assets.Directions[Direction].Dy*FrameIndex/4.0:0));
    public World(Assets assets) {Assets=assets;Teleport(assets.Spawn[0],assets.Spawn[1]);}
    public void ResetOfflinePreview(){SetOnlineAppearance(null);AllowStreaming=false;StreamingBounds=null;Teleport(Assets.Spawn[0],Assets.Spawn[1]);Face(5);}
    public void Teleport(int x,int y) {if(!Inside(x,y))throw new ArgumentOutOfRangeException(nameof(x),"Outside supplied block");X=x;Y=y;networkAnimation=false;Walking=false;FrameIndex=0;Elapsed=0;presentationAge=0;Intent=null;BoundaryNotice="";}
    public void Stop(){networkAnimation=false;Walking=false;FrameIndex=0;Elapsed=0;presentationAge=0;Intent=null;}
    public void Face(int direction) {if(direction<0||direction>7)throw new ArgumentOutOfRangeException(nameof(direction));if(!Walking){Direction=direction;FrameIndex=0;Elapsed=0;presentationAge=0;}}
    public void SetIntent(int? direction) {if(direction is <0 or >7)throw new ArgumentOutOfRangeException(nameof(direction));Intent=direction;if(!Walking&&direction.HasValue)StartStep(direction.Value);}
    bool StartStep(int d) {
        Direction=d;var v=Assets.Directions[d];
        if(!Inside(X+v.Dx,Y+v.Dy)){BoundaryNotice="已載入區域邊界";return false;}
        Walking=true;FrameIndex=0;Elapsed=0;presentationAge=0;BoundaryNotice="";return true;
    }
    public void NetworkStep(int x,int y,int heading,int? stepMs=null){NetworkStepMs=stepMs??onlineAppearance?.StepMs??MovementRules.StepMs;if(NetworkStepMs<=0)throw new ArgumentOutOfRangeException(nameof(stepMs));Teleport(x,y);StartStep(MovementRules.SpriteDirection(heading));networkAnimation=true;}
    bool networkAnimation;
    public void Advance(double ms) {
        if(!double.IsFinite(ms)||ms<0)throw new ArgumentOutOfRangeException(nameof(ms));
        DiscardedMs+=Math.Max(0,ms-250);ms=Math.Min(ms,250);Elapsed+=ms;presentationAge+=ms;
        double frameMs=networkAnimation?NetworkStepMs/4.0:Assets.FrameMs;
        while(Elapsed>=frameMs) {
            Elapsed-=frameMs;FrameIndex++;
            if(Walking&&FrameIndex==4) {
                var d=Assets.Directions[Direction];X+=d.Dx;Y+=d.Dy;Steps++;
                Walking=false;networkAnimation=false;FrameIndex=0;presentationAge=Elapsed;var rest=Elapsed;
                if(Intent.HasValue)StartStep(Intent.Value);Elapsed=rest;
            } else if(!Walking&&FrameIndex==8)FrameIndex=0;
        }
    }
    public object Snapshot()=>new{X,Y,Direction,Walking,FrameIndex,Frame=Frame.Key,gfx=onlineAppearance?.Gfx??0,sprite=onlineAppearance?.Sprite??3225,presentationAge,networkStepMs=NetworkStepMs,Intent,Elapsed,Steps,DiscardedMs,BoundaryNotice};
}
public sealed class InputState {
    public HashSet<Keys> Held {get;}=[];
    public void Down(Keys k)=>Held.Add(k);
    public void Up(Keys k)=>Held.Remove(k);
    public void Clear()=>Held.Clear();
    public int? Direction {
        get {
            int x=0,y=0;
            foreach(var k in Held) {
                var v=k switch {Keys.Up or Keys.W or Keys.NumPad8 =>(0,-1),Keys.Down or Keys.S or Keys.NumPad2=>(0,1),Keys.Left or Keys.A or Keys.NumPad4=>(-1,0),Keys.Right or Keys.D or Keys.NumPad6=>(1,0),Keys.Home or Keys.NumPad7=>(-1,-1),Keys.PageUp or Keys.NumPad9=>(1,-1),Keys.End or Keys.NumPad1=>(-1,1),Keys.PageDown or Keys.NumPad3=>(1,1),_=>(0,0)};x+=v.Item1;y+=v.Item2;
            }
            return (Math.Sign(x),Math.Sign(y)) switch {(-1,-1)=>0,(0,-1)=>1,(1,-1)=>2,(1,0)=>3,(1,1)=>4,(0,1)=>5,(-1,1)=>6,(-1,0)=>7,_=>null};
        }
    }
}
public sealed class Camera {
    public PointF ViewOffset {get;set;}
    public float Zoom {get;private set;}=1.5f;
    public PointF Center {get;private set;}
    public bool Follow {get;set;}=true;
    public Size View {get;set;}=new(1100,650);
    int canvasWidth=3072,canvasHeight=1632;int canvasX,canvasY;
    public void SetBounds(Rectangle bounds){canvasX=bounds.X;canvasY=bounds.Y;SetBounds(bounds.Width,bounds.Height);}
    public void SetBounds(int width,int height){if(width<=0||height<=0)throw new ArgumentOutOfRangeException();canvasWidth=width;canvasHeight=height;SetCenter(Center);}
    public void SetCenter(PointF p) {Center=new(Math.Clamp(p.X,canvasX,canvasX+canvasWidth),Math.Clamp(p.Y,canvasY,canvasY+canvasHeight));}
    public PointF ToView(PointF p)=>new((p.X-Center.X)*Zoom+View.Width/2f+ViewOffset.X,(p.Y-Center.Y)*Zoom+View.Height/2f+ViewOffset.Y);
    public PointF ToScene(PointF p)=>new((p.X-ViewOffset.X-View.Width/2f)/Zoom+Center.X,(p.Y-ViewOffset.Y-View.Height/2f)/Zoom+Center.Y);
    public void Scale(float factor,PointF mouse) {var before=ToScene(mouse);Zoom=Math.Clamp(Zoom*factor,.35f,3f);if(!Follow){var after=ToScene(mouse);SetCenter(new(Center.X+before.X-after.X,Center.Y+before.Y-after.Y));}}
    public void Pan(float dx,float dy) {Follow=false;SetCenter(new(Center.X-dx/Zoom,Center.Y-dy/Zoom));}
}
