namespace P06;

// WO §1 (T182/#484, verified): object clicks go through sub_F112F0. sub_500E40 accepts
// |self.x-target.x|<=2r && |self.y-target.y|<=r on the client's doubled x, i.e. r game tiles
// including diagonals. Out of range, dword_13CE400 becomes mode 3 ("walk, then interact"):
// every step re-checks the threshold and interacts once reached (or at the path end).
// The route itself reuses the engine's bounded PlayerPointerPath; it is not the native pathfinder.
public static class ApproachRules {
    public const int PickupRange=1;
    public static bool Within(int ax,int ay,int tx,int ty,int range)=>range>=0&&Math.Abs(ax-tx)<=range&&Math.Abs(ay-ty)<=range;
    // Path from start that stops at the first tile within range of target; [] when already in range.
    public static Point[]? Plan(Point start,Point target,int range,Func<int,int,int,bool> passable){
        if(Within(start.X,start.Y,target.X,target.Y,range))return [];
        var direct=PlayerPointerPath.Find(start,target,passable);
        if(direct!=null){int i=Array.FindIndex(direct,p=>Within(p.X,p.Y,target.X,target.Y,range));return i<0?null:direct[..(i+1)];}
        // The target tile itself may be unreachable (occupied, wall): approach any reachable tile in range.
        Point[]? best=null;
        for(int dy=-range;dy<=range;dy++)for(int dx=-range;dx<=range;dx++){
            if(dx==0&&dy==0)continue;var goal=new Point(target.X+dx,target.Y+dy);
            var path=PlayerPointerPath.Find(start,goal,passable);
            if(path!=null&&(best==null||path.Length<best.Length))best=path;
        }
        return best;
    }
    public static int Heading(Point from,Point to)=>Enumerable.Range(0,8).FirstOrDefault(d=>from.X+MovementRules.Dx[d]==to.X&&from.Y+MovementRules.Dy[d]==to.Y,-1);
}

// One approach at a time per connection. Any new approach, local input cancel, correction,
// world/session/appearance/speed change or target invalidation stops it; nothing is resent.
public sealed class ApproachWalk {
    public const int TimeoutMs=60000;
    readonly LoginConnection login;readonly Func<long> now;readonly Func<Task> idle;readonly Func<int,Func<bool>,Task<bool>> move;readonly Func<int,int,int,bool>? terrain;readonly Func<bool> available;
    int version;
    sealed record Stamp(int Session,int World,uint Actor,ushort? Map,int Correction,int Appearance,int Speed,int Input);
    // Optional seams are for offline tests only (memory move, synthetic terrain); production uses the defaults.
    public ApproachWalk(LoginConnection login,Func<long>? clock=null,Func<Task>? wait=null,Func<int,Func<bool>,Task<bool>>? move=null,Func<int,int,int,bool>? terrain=null,Func<bool>? available=null){
        this.login=login;now=clock??(()=>Environment.TickCount64);idle=wait??(()=>Task.Delay(15));this.move=move??login.PointerMove;this.terrain=terrain;this.available=available??(()=>login.MovementAvailable);
    }
    public bool Active {get;private set;}
    public int Steps {get;private set;}
    public Point? Target {get;private set;}
    public string Notice {get;private set;}="";
    public void Cancel(string reason){if(Active)Notice="已停止走近（"+reason+"）";version++;Active=false;Target=null;}
    Stamp Capture(){var w=login.World;return new(login.SessionGeneration,w.WorldEpoch,w.Actor!.ObjectId,w.MapId,w.Corrections,w.AppearanceRevision,w.SpeedRevision,login.InputVersion);}
    Point Position=>new(login.World.Actor!.X,login.World.Actor.Y);
    bool RawStep(int x,int y,int h){
        var w=login.World;int nx=x+MovementRules.Dx[h],ny=y+MovementRules.Dy[h];
        if(w.DoorBlocks(x,y,h)||w.Objects.Any(o=>!o.Dead&&o.ObjectId!=w.Actor?.ObjectId&&!w.IsDoor(o.ObjectId)&&o.X==nx&&o.Y==ny))return false;
        return terrain?.Invoke(x,y,h)??w.MapCache?.Passable(x,y,h)??MovementRules.Passable(x,y,h);
    }
    bool Step(Point p,int h)=>PlayerPointerPath.Step(p,h,RawStep);
    // Walks until within range of target, then awaits arrive() exactly once. Returns arrive()'s result,
    // or false when the approach is refused or cancelled before arrival.
    public async Task<bool> RunAsync(Point target,int range,Func<bool> targetValid,Func<Task<bool>> arrive,Func<bool>? walking=null){
        version++;int ticket=version;walking??=()=>false;
        if(!available()||login.World.Actor==null){Active=false;Notice="目前無法走近。";return false;}
        if(!targetValid()){Active=false;Notice="目標已不存在。";return false;}
        var stamp=Capture();var path=ApproachRules.Plan(Position,target,range,RawStep);
        if(path==null){Active=false;Notice="沒有可通行的有限路徑可以走近目標。";return false;}
        long started=now();var expected=Position;Active=path.Length>0;Target=target;Steps=0;Notice=path.Length==0?"已在目標旁。":"走近目標中；到達後自動執行一次。";
        bool Current()=>ticket==version&&stamp==Capture()&&available()&&targetValid();
        async Task<bool> Settle(){
            while(true){
                if(!Current()){if(ticket==version)Cancel("狀態變更或目標失效");return false;}
                if(now()-started>TimeoutMs){Cancel("走近逾時");return false;}
                if(!walking()&&!login.MovementBusy)return true;
                await idle();
            }
        }
        try{
            foreach(var next in path){
                if(!await Settle())return false;
                if(ApproachRules.Within(Position.X,Position.Y,target.X,target.Y,range))break;
                int h=ApproachRules.Heading(expected,next);var from=expected;
                if(Position!=expected||h<0||!Step(expected,h)){Cancel("位置校正或前方受阻");return false;}
                bool sent=await move(h,()=>Current()&&!walking()&&Position==from&&Step(from,h));
                if(ticket!=version)return false;
                if(!sent){Cancel("移動請求未通過既有閘門");return false;}
                expected=next;Steps++;
            }
            if(!await Settle())return false;
            if(!ApproachRules.Within(Position.X,Position.Y,target.X,target.Y,range)){Cancel("未到達目標距離");return false;}
            Active=false;Target=null;
            return await arrive();
        }finally{if(ticket==version){Active=false;Target=null;}}
    }
}

public sealed partial class LoginConnection {
    ApproachWalk? approach;
    public ApproachWalk Approach {get=>approach??=new(this);internal set=>approach=value;}
    internal int InputVersion=>inputVersion;
}
