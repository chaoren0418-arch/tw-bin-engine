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

// R2-C (T253/#614) pointer texts shared by controller, form and HUD.
public static class PointerFlow {
    public const string Help="左鍵地面行走；左鍵選取，雙擊或 Ctrl＋左鍵攻擊，右鍵交談；雙擊地上物品拾取（遠的先走近，多個時詢問數量）。";
    public const string DeadNotice="未送出：死亡中不能操作物件";
    public const string GroundGone="未送出：地上物品已不可見，或上一個拾取仍在進行。";
    public const string TargetUnavailable="未送出：請確認目標仍可見，停下並等待可操作後再點擊。";
}

/// <summary>
/// R10-D（T388／#903，FU9§9.1）：從場景拖人到隊伍窗。原版 `sub_ED8AC0`（FO `0xAD7EC0`）收到 517、拖的是別人的 `userObject`
/// → 送 type 0／1。引擎：場景左鍵按下選中別的玩家時（<see cref="PlayerPointerController.Select"/>）記下物件 id；隊伍窗
/// （NativeUi/PartyPanel）收到「不是自己按下」的左鍵放開時取走，走既有 PartyAction("invite") → SendParty（C230 type 0）。
/// 任何新的場景點擊或停止（PlayerPointerController.Cancel）、在隊伍窗上按下都會清掉。只在 UI 執行緒使用。
/// 拖曳中游標上的人物圖沒畫；窗的類別與「只在隊伍分頁收」是推測（真機再確認）。
/// </summary>
public static class WorldObjectDrag {
    public static uint? Source {get;private set;}
    public static void Begin(uint id)=>Source=id==0?null:id;
    public static void Clear()=>Source=null;
    public static uint? Take(){var s=Source;Source=null;return s;}
    /// <summary>可以拖去邀請的對象：看得到、活著、玩家旗標（4）、不是自己。</summary>
    public static bool PlayerCandidate(WorldSession w,uint id)=>id!=0&&id!=w.Actor?.ObjectId&&id!=w.Status?.ObjectId&&w.Objects.Any(o=>o.ObjectId==id&&!o.Dead&&!o.AppearancePending&&(o.Flags&4)!=0);
}

public sealed class PlayerPointerController {
    readonly LoginConnection login;readonly Func<bool> focused,walking;readonly Func<uint,bool> visible;readonly Func<long> now;
    readonly Queue<Point> route=new();int version;bool sending;long started;Point expected;Stamp? stamp;
    sealed record Stamp(int Session,int World,uint Actor,ushort? Map,int Correction,int Appearance,int Speed);
    public PlayerPointerController(LoginConnection login,Func<bool> focused,Func<bool> walking,Func<uint,bool> visible,Func<long>? clock=null){this.login=login;this.focused=focused;this.walking=walking;this.visible=visible;now=clock??(()=>Environment.TickCount64);}
    // A ground click that is still approaching (ApproachWalk) counts as active so keyboard/Esc stop it,
    // but it does not hold `sending`: a new click cancels it through inputVersion instead of being refused.
    public bool Active=>route.Count>0||sending||groundClicks>0;
    int groundClicks;
    public int RemainingSteps=>route.Count;
    public string Notice {get;private set;}=PointerFlow.Help;
    Stamp Capture(){var w=login.World;return new(login.SessionGeneration,w.WorldEpoch,w.Actor!.ObjectId,w.MapId,w.Corrections,w.AppearanceRevision,w.SpeedRevision);}
    bool Current(Stamp s)=>focused()&&login.MovementAvailable&&s==Capture();
    Point Position=>new(login.World.Actor!.X,login.World.Actor.Y);
    public void Cancel(string reason){version++;route.Clear();WorldObjectDrag.Clear();stamp=null;login.CancelMovementInput();Notice="已停止後續滑鼠動作（"+reason+"）；已送出的請求仍以伺服器為準。";}
    bool RawStep(int x,int y,int h){
        var w=login.World;int nx=x+MovementRules.Dx[h],ny=y+MovementRules.Dy[h];
        // Known doors block only through S209; their object tile is not occupancy.
        if(w.DoorBlocks(x,y,h)||w.Objects.Any(o=>!o.Dead&&o.ObjectId!=w.Actor?.ObjectId&&!w.IsDoor(o.ObjectId)&&o.X==nx&&o.Y==ny))return false;
        return w.MapCache?.Passable(x,y,h)??MovementRules.Passable(x,y,h);
    }
    bool Step(Point p,int h)=>PlayerPointerPath.Step(p,h,RawStep);
    // R7-A SV#3：選目標模式中（Magic.cs SpellTargetPick），點物件＝選這個目標並施法，點地面＝取消選目標、不走路（推定）。
    public bool SpellTargetPicking=>login.World.SpellTargetPicking;
    public Task<bool>? LastSpellPick {get;private set;}
    bool TryPickSpellTarget(uint id){
        if(!login.World.SpellTargetPicking)return false;
        Cancel("選施法目標");
        if(Refusal(id,false) is {} refused){Notice=refused;LastSpellPick=Task.FromResult(false);return true;}
        LastSpellPick=PickAsync(id);return true;
    }
    async Task<bool> PickAsync(uint id){
        bool sent=await login.PickSpellTarget(id,visible);
        Notice=sent?"已選取施法目標，單次施法已送出；等待原服結果。":login.World.MagicNotice;
        return sent;
    }
    // R10-D（T388／#903，FU9§9.1）：`/invite` 的選目標模式（Party.cs PartyInvitePick）。點物件＝sub_954FC0：結束選目標，
    // 是 30 格內的有效玩家才送 C230 type 0（PickPartyInviteTarget；type 1 要 byte_13CE519，本引擎固定 0）；點地面＝取消、不走路。
    // 施法選目標優先（兩種模式同時開時先處理施法，與原版的先後關係沒查，推測）。
    public bool PartyInvitePicking=>login.World.PartyInvitePicking;
    public Task<bool>? LastPartyInvitePick {get;private set;}
    public const string PartyInviteSentNotice="組隊邀請已送出；結果以原服回覆為準。",PartyInviteRefusedNotice="未送出組隊邀請：對象不是 30 格內可邀請的玩家，或目前不能操作。",PartyInviteCancelNotice="已取消選擇邀請對象。";
    bool TryPickPartyInvite(uint id){
        if(!login.World.PartyInvitePicking)return false;
        Cancel("選邀請對象");
        LastPartyInvitePick=PartyPickAsync(id);return true;
    }
    async Task<bool> PartyPickAsync(uint id){
        bool sent=await login.PickPartyInviteTarget(id);
        Notice=sent?PartyInviteSentNotice:PartyInviteRefusedNotice;
        return sent;
    }
    public bool MoveTo(Point goal){
        if(login.World.SpellTargetPicking){Cancel("取消選目標");login.World.CancelSpellTargetPick("點了地面");Notice=login.World.MagicNotice;return false;}
        if(login.World.PartyInvitePicking){Cancel("取消選邀請對象");login.World.CancelPartyInvitePick();Notice=PartyInviteCancelNotice;return false;}
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
    // 選目標模式中點到人：送邀請（或不送），不改選取目標，回 false。一般選取選中別的玩家時記下拖曳來源（WorldObjectDrag）。
    public bool Select(uint id){
        if(TryPickSpellTarget(id))return login.World.TargetId==id;
        if(TryPickPartyInvite(id))return false;
        Cancel("選取目標");if(!focused()||!login.World.Renderable||!TargetVisible(id))return false;login.World.SelectTarget(id);Notice=$"已選取 {login.World.Target?.Name} #{id}；G 攻擊，選技能後 M 施放。";
        if(login.World.TargetId==id&&WorldObjectDrag.PlayerCandidate(login.World,id))WorldObjectDrag.Begin(id);
        return login.World.TargetId==id;
    }
    // R2-C (WO §1–2): double-click on a ground item goes through the object-click flow
    // (GroundUi.PickupGroundClicked → LoginConnection.PickupGround): approach to 1 tile, then the whole
    // stack or the quantity prompt. `pickup` defaults to that flow; the UI passes PickupGroundClicked.
    // Returns true only when a C_GET was actually admitted; an opened prompt returns false.
    public async Task<bool> ClickGroundAsync(uint id,Func<uint,Task<bool>>? pickup=null){
        Cancel("點擊地上物品");
        if(sending||!focused()||!visible(id)||!login.World.Ground.Any(g=>g.ObjectId==id)){Notice=PointerFlow.GroundGone;return false;}
        int ticket=version;groundClicks++;
        try{
            bool sent=await (pickup??(g=>login.PickupGround(g,walking)))(id);
            if(ticket==version)Notice=login.World.GroundNotice;
            return sent&&ticket==version;
        }finally{groundClicks--;}
    }
    // Explicit-count pickup kept for callers that already chose a count (not the double-click path).
    public async Task<bool> PickupAsync(uint id,uint count=1){
        Cancel("單次拾取");
        if(sending||!focused()||!login.MovementAvailable||walking()||login.MovementBusy||!visible(id)||!login.World.Ground.Any(g=>g.ObjectId==id))return false;
        if(!login.World.SelectGround(id))return false;
        int ticket=version,selection=login.World.GroundSelectionRevision;var s=Capture();var from=Position;sending=true;
        bool Valid()=>ticket==version&&Current(s)&&!walking()&&Position==from&&login.World.GroundSelectionRevision==selection&&login.World.SelectedGroundId==id&&visible(id);
        try{bool sent=await login.PointerPickup(count,Valid);if(ticket==version)Notice=sent?$"單次拾取{count}件已送出；等待地面與背包回報。":"目前無法拾取；請走近後再雙擊，不會自動追物或重送。";return sent&&ticket==version;}
        finally{sending=false;}
    }
    // Local refusal before any send (R2-C). Doors are not refused here: ToggleDoor owns their death
    // gate and DoorNotice. WO §1: own death returns from sub_F112F0 for every object click (NPC talk
    // and attack included). WO §4: ghost blocks attack only; NPC talk and doors stay clickable.
    public string? Refusal(uint id,bool talk){
        if(login.World.IsDoor(id))return null;
        if(login.ObjectClicksBlocked)return PointerFlow.DeadNotice;
        if(!talk&&login.GhostLocalBlocked)return GhostLocalLimits.Notice;
        return null;
    }
    public async Task<bool> ActAsync(uint id,bool talk){
        // 選目標模式中，左鍵（攻擊路徑）點物件是選施法目標，不送攻擊。
        if(!talk&&TryPickSpellTarget(id))return await LastSpellPick!;
        // 選邀請對象時，攻擊路徑（雙擊／Ctrl／直接攻擊）點人也是選對象、不攻擊；回 false 讓 UI 不記成「attack」，結果看 LastPartyInvitePick。
        if(!talk&&TryPickPartyInvite(id)){await LastPartyInvitePick!;return false;}
        Cancel(talk?"單次交談":"單次攻擊");
        if(Refusal(id,talk) is {} refused){Notice=refused;return false;}
        bool door=login.World.IsDoor(id);
        // Doors: ToggleDoor checks availability itself and reports why through DoorNotice.
        if(sending||!focused()||login.World.Actor==null||!TargetVisible(id)||!door&&(!login.MovementAvailable||walking()||login.MovementBusy)){Notice=PointerFlow.TargetUnavailable;return false;}
        login.World.SelectTarget(id);if(login.World.TargetId!=id)return false;
        int ticket=version,selection=login.World.TargetRevision;var s=Capture();var from=Position;sending=true;
        bool Valid()=>ticket==version&&Current(s)&&!walking()&&Position==from&&login.World.TargetRevision==selection&&login.World.TargetId==id&&TargetVisible(id);
        try{
            if(door){bool doorSent=await login.ToggleDoor(id,Valid,walking);if(ticket==version)Notice=login.DoorNotice;return doorSent&&ticket==version;}
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
