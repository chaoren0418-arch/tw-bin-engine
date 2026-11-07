namespace P06;

// Bounded local click-to-walk policy, not a claim to reproduce the original BIN pathfinder.
public static class PlayerPointerPath {
    public const int MaxDistance=64,MaxSteps=128,MaxExpanded=8192;
    public static bool Step(Point p,int h,Func<int,int,int,bool> passable){
        if(h is <0 or >7||!passable(p.X,p.Y,h))return false;
        if((h&1)==0)return true;
        int a=(h+7)%8,b=(h+1)%8;
        return passable(p.X,p.Y,a)&&passable(p.X,p.Y,b)
            &&passable(p.X+MovementRules.Dx[a],p.Y+MovementRules.Dy[a],b)
            &&passable(p.X+MovementRules.Dx[b],p.Y+MovementRules.Dy[b],a);
    }
    public static Point[]? Find(Point start,Point goal,Func<int,int,int,bool> passable){
        int Distance(Point p)=>Math.Max(Math.Abs(p.X-goal.X),Math.Abs(p.Y-goal.Y));
        int Cost(Point p){int x=Math.Abs(p.X-goal.X),y=Math.Abs(p.Y-goal.Y);return 10*Math.Max(x,y)+4*Math.Min(x,y);}
        if(Distance(start)>MaxDistance)return null;if(start==goal)return [];
        var open=new PriorityQueue<Point,(int,int)>();var scores=new Dictionary<Point,int>{{start,0}};var parents=new Dictionary<Point,Point>();var closed=new HashSet<Point>();int serial=0,expanded=0;open.Enqueue(start,(Cost(start),serial++));
        while(open.TryDequeue(out var p,out _)){
            if(!closed.Add(p))continue;if(++expanded>MaxExpanded)return null;
            if(p==goal){var path=new List<Point>();while(p!=start){path.Add(p);p=parents[p];if(path.Count>MaxSteps)return null;}path.Reverse();return path.ToArray();}
            for(int h=0;h<8;h++){
                var n=new Point(p.X+MovementRules.Dx[h],p.Y+MovementRules.Dy[h]);
                if(Math.Max(Math.Abs(n.X-start.X),Math.Abs(n.Y-start.Y))>MaxDistance||closed.Contains(n)||!Step(p,h,passable))continue;
                int score=scores[p]+((h&1)==0?10:14);if(scores.TryGetValue(n,out int old)&&old<=score)continue;scores[n]=score;parents[n]=p;open.Enqueue(n,(score+Cost(n),serial++));
            }
        }
        return null;
    }
}

public sealed class PlayerPointerController {
    readonly LoginConnection login;readonly Func<bool> focused,walking;readonly Func<uint,bool> visible;readonly Func<long> now;
    readonly Queue<Point> route=new();int version;bool sending;long started;Point expected;Stamp? stamp;
    sealed record Stamp(int Session,int World,uint Actor,ushort? Map,int Correction,int Appearance,int Speed);
    public PlayerPointerController(LoginConnection login,Func<bool> focused,Func<bool> walking,Func<uint,bool> visible,Func<long>? clock=null){this.login=login;this.focused=focused;this.walking=walking;this.visible=visible;now=clock??(()=>Environment.TickCount64);}
    public bool Active=>route.Count>0||sending;
    public int RemainingSteps=>route.Count;
    public string Notice {get;private set;}="左鍵地面行走；左鍵選取，雙擊或 Ctrl＋左鍵攻擊，右鍵交談；雙擊地上物品拾取1件。";
    Stamp Capture(){var w=login.World;return new(login.SessionGeneration,w.WorldEpoch,w.Actor!.ObjectId,w.MapId,w.Corrections,w.AppearanceRevision,w.SpeedRevision);}
    bool Current(Stamp s)=>focused()&&login.MovementAvailable&&s==Capture();
    Point Position=>new(login.World.Actor!.X,login.World.Actor.Y);
    public void Cancel(string reason){version++;route.Clear();stamp=null;login.CancelMovementInput();Notice="已停止後續滑鼠動作（"+reason+"）；已送出的請求仍以伺服器為準。";}
    bool RawStep(int x,int y,int h){
        var w=login.World;int nx=x+MovementRules.Dx[h],ny=y+MovementRules.Dy[h];
        // Known doors block only through S209; their object tile is not occupancy.
        if(w.DoorBlocks(x,y,h)||w.Objects.Any(o=>!o.Dead&&o.ObjectId!=w.Actor?.ObjectId&&!w.IsDoor(o.ObjectId)&&o.X==nx&&o.Y==ny))return false;
        return w.MapCache?.Passable(x,y,h)??MovementRules.Passable(x,y,h);
    }
    bool Step(Point p,int h)=>PlayerPointerPath.Step(p,h,RawStep);
    public bool MoveTo(Point goal){
        Cancel("新目的地");if(!focused()||!login.MovementAvailable){Notice="目前無法行走。";return false;}
        var w=login.World;if(!(w.MapCache?.Contains(goal.X,goal.Y)??Coordinates.Inside(goal.X,goal.Y))){Notice="目的地不在已載入地圖內。";return false;}
        // Snapshot occupancy once for bounded A*, then use live RawStep again for every sent edge.
        var occupied=w.Objects.Where(o=>!o.Dead&&o.ObjectId!=w.Actor?.ObjectId&&!w.IsDoor(o.ObjectId)).Select(o=>new Point(o.X,o.Y)).ToHashSet();var cache=w.MapCache;
        bool PlanStep(int x,int y,int h)=>!w.DoorBlocks(x,y,h)&&!occupied.Contains(new Point(x+MovementRules.Dx[h],y+MovementRules.Dy[h]))&&(cache?.Passable(x,y,h)??MovementRules.Passable(x,y,h));
        var start=Position;var path=PlayerPointerPath.Find(start,goal,PlanStep);if(path==null){Notice="沒有可通行的有限路徑，請選較近的地面。";return false;}
        expected=start;stamp=Capture();started=now();foreach(var p in path)route.Enqueue(p);Notice=path.Length==0?"已在目的地。":"沿已載入可通行地面行走；Esc／空白鍵停止。";return true;
    }
    public async Task TickAsync(){
        if(route.Count==0)return;
        if(stamp is not {} s||!Current(s)||now()-started>120000){Cancel("狀態變更或路徑逾時");return;}
        if(sending||walking()||login.MovementBusy)return;
        var next=route.Peek();int h=Enumerable.Range(0,8).FirstOrDefault(d=>expected.X+MovementRules.Dx[d]==next.X&&expected.Y+MovementRules.Dy[d]==next.Y,-1);
        if(Position!=expected||h<0||!Step(expected,h)){Cancel("位置校正或前方受阻");return;}
        int ticket=version;var from=expected;sending=true;
        try{
            bool sent=await login.PointerMove(h,()=>ticket==version&&Current(s)&&!walking()&&Position==from&&Step(from,h));
            if(ticket!=version)return;if(!sent){Cancel("移動請求未通過既有閘門");return;}
            route.Dequeue();expected=next;if(route.Count==0)Notice="目的地步行請求已送出；位置依伺服器校正。";
        }finally{sending=false;}
    }
    bool TargetVisible(uint id)=>visible(id)&&login.World.Objects.Any(o=>o.ObjectId==id&&!o.Dead&&!o.AppearancePending);
    public bool Select(uint id){Cancel("選取目標");if(!focused()||!login.World.Renderable||!TargetVisible(id))return false;login.World.SelectTarget(id);Notice=$"已選取 {login.World.Target?.Name} #{id}；G 攻擊，選技能後 M 施放。";return login.World.TargetId==id;}
    public async Task<bool> PickupAsync(uint id,uint count=1){
        Cancel("單次拾取");
        if(sending||!focused()||!login.MovementAvailable||walking()||login.MovementBusy||!visible(id)||!login.World.Ground.Any(g=>g.ObjectId==id))return false;
        if(!login.World.SelectGround(id))return false;
        int ticket=version,selection=login.World.GroundSelectionRevision;var s=Capture();var from=Position;sending=true;
        bool Valid()=>ticket==version&&Current(s)&&!walking()&&Position==from&&login.World.GroundSelectionRevision==selection&&login.World.SelectedGroundId==id&&visible(id);
        try{bool sent=await login.PointerPickup(count,Valid);if(ticket==version)Notice=sent?$"單次拾取{count}件已送出；等待地面與背包回報。":"目前無法拾取；請走近後再雙擊，不會自動追物或重送。";return sent&&ticket==version;}
        finally{sending=false;}
    }
    public async Task<bool> ActAsync(uint id,bool talk){
        Cancel(talk?"單次交談":"單次攻擊");
        if(sending||!focused()||!login.MovementAvailable||walking()||login.MovementBusy||!TargetVisible(id)){Notice="未送出：請確認目標仍可見，停下並等待可操作後再點擊。";return false;}
        login.World.SelectTarget(id);if(login.World.TargetId!=id)return false;
        int ticket=version,selection=login.World.TargetRevision;var s=Capture();var from=Position;sending=true;
        bool Valid()=>ticket==version&&Current(s)&&!walking()&&Position==from&&login.World.TargetRevision==selection&&login.World.TargetId==id&&TargetVisible(id);
        try{
            if(login.World.IsDoor(id)){bool doorSent=await login.ToggleDoor(id,Valid);if(ticket==version)Notice=doorSent?"門操作已送出；等待伺服器回報開關與通行狀態。":"門操作未送出；請確認狀態後重新點擊。";return doorSent&&ticket==version;}
            bool sent=talk?await login.PointerTalk(Valid):await login.PointerAttack(Valid);
            if(ticket==version)Notice=sent?(talk?"單次交談已送出，等待伺服器回覆。":"單次攻擊已送出，等待伺服器回覆。"):(talk?"目前無法交談；請走近後再點擊。":login.World.CombatNotice);
            return sent&&ticket==version;
        }finally{sending=false;}
    }
}

public sealed partial class LoginConnection {
    // Existing public methods and defaults remain unchanged. Pointer supplies an extra final admission fence.
    public async Task<bool> PointerMove(int heading,Func<bool> current)=>(await SendMovement(heading,true,false,current)).Sent;
    public Task<bool> PointerAttack(Func<bool> current)=>AttackCore(current);
    public Task<bool> PointerPickup(uint count,Func<bool> current)=>SendGround(true,count,current);
    public Task<bool> PointerTalk(Func<bool> current)=>InteractCore("talk",World.InteractionRevision,World.WorldEpoch,World.TargetRevision,0,null,current);
}
