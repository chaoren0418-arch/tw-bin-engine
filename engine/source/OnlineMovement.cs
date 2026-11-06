using System.Buffers.Binary;
namespace P06;
public static class MovementRules {
    public const int StepMs=640;
    public static readonly int[] Dx=[0,1,1,1,0,-1,-1,-1],Dy=[-1,-1,0,1,1,1,0,-1];
    public static int SpriteDirection(int heading)=>(heading);
    static readonly Lazy<byte[]> terrain=new(()=>File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/terrain0.bin"))));
    public static bool Passable(int x,int y,int h){
        if(h is <0 or >7)return false;int nx=x+Dx[h],ny=y+Dy[h];
        if(!Coordinates.Inside(x,y)||!Coordinates.Inside(nx,ny))return false;
        var b=terrain.Value;if(b.Length!=Coordinates.Columns*Coordinates.Rows)throw new InvalidDataException("terrain dimensions");
        int a=b[(y-Coordinates.MinY)*Coordinates.Columns+x-Coordinates.MinX],v=b[(ny-Coordinates.MinY)*Coordinates.Columns+nx-Coordinates.MinX];
        if(v==0||(v&128)!=0||(v&3)==0)return false;
        return h switch{0=>(a&2)!=0,2=>(a&1)!=0,4=>(v&2)!=0,6=>(v&1)!=0,_=>(v&3)!=0};
    }
    public static byte[] Move(int x,int y,int h){if(h is <0 or >7)throw new ArgumentOutOfRangeException(nameof(h));byte[] b=new byte[8];b[0]=29;BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(1),(ushort)x);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(3),(ushort)y);b[5]=(byte)h;return b;}
}
public sealed partial class WorldSession {
    public OwnActor? AuthoritativeActor {get;private set;}
    public ushort? AuthoritativeMapId {get;private set;}
    public long AuthoritativePositionRevision {get;private set;}
    public byte? AuthoritativePositionOpcode {get;private set;}
    public bool PositionPredictedSinceAuthority {get;private set;}
    public string PositionDisplayText=>Actor is {} a&&MapId is {} map
        ?$"目前位置 map {map} / x {a.X} / y {a.Y}"+(PositionPredictedSinceAuthority?"（本地預測，非原服確認）":"（原服回報）")+" · "+AuthoritativePositionText
        :"目前位置：等待角色資料";
    void RecordAuthoritativePosition(OwnActor a,byte opcode){AuthoritativeActor=a;AuthoritativeMapId=MapId;AuthoritativePositionOpcode=opcode;AuthoritativePositionRevision++;PositionPredictedSinceAuthority=false;}
    public string AuthoritativePositionText=>AuthoritativeActor is {} a&&AuthoritativeMapId is {} map
        ?$"原服座標 map {map} / x {a.X} / y {a.Y} · S{AuthoritativePositionOpcode} #{AuthoritativePositionRevision}"+(PositionPredictedSinceAuthority?" · 移動後尚無位置回包":"")
        :"原服座標：等待位置回包";
    public object AuthoritativePositionSnapshot()=>new{map=AuthoritativeMapId,x=(ushort?)AuthoritativeActor?.X,y=(ushort?)AuthoritativeActor?.Y,opcode=AuthoritativePositionOpcode,revision=AuthoritativePositionRevision,predictedSince=PositionPredictedSinceAuthority,text=AuthoritativePositionText,displayText=PositionDisplayText};
    public int Corrections {get;private set;}
    public int PredictedSteps {get;private set;}
    public int PredictedTurns {get;private set;}
    public string MotionNotice {get;internal set;}="等待世界";
    void ResetMovement(){AuthoritativeActor=null;AuthoritativeMapId=null;AuthoritativePositionOpcode=null;PositionPredictedSinceAuthority=false;Corrections=PredictedSteps=PredictedTurns=0;MotionNotice="等待世界";}
    void ApplyAuthoritative(OwnActor a){CancelPickupPresentation("authoritative-pack");pickupShop=a.Weapon==70;pickupRemoved=false;AppearanceRevision++;if(Actor!=null){Corrections++;MotionNotice="伺服器位置更新／校正；輸入已停止";}else MotionNotice="已初始化；移動使用本地預測";RecordAuthoritativePosition(a,87);Actor=a;OwnDead=a.Weapon==8;OwnCombat=OwnDead?new(8,Environment.TickCount64,false):null;UpdateOpacity();}
    internal void Predict(int x,int y,int h,bool step){CancelPickupPresentation("movement/turn");pendingAction=null;OwnCombat=null;Actor=Actor! with{X=(ushort)x,Y=(ushort)y,Direction=(byte)h};if(step){PositionPredictedSinceAuthority=true;PredictedSteps++;UpdateOpacity();}else PredictedTurns++;MotionNotice=step?"已送出步行；本地預測（無逐步 ACK）":"已送出轉向；本地預測";}
    public object MovementSnapshot()=>new{predictedSteps=PredictedSteps,predictedTurns=PredictedTurns,corrections=Corrections,authoritativeActor=AuthoritativeActor,authoritativePosition=AuthoritativePositionSnapshot(),notice=MotionNotice,stepMs=MovementIntervalMs,animationMs=MovementAnimationMs,speedRevision=SpeedRevision,speedNotice=SpeedNotice,successAck=false};
}
public sealed record MovementAttempt(bool Sent,string Notice);
public sealed partial class LoginConnection {
    public LoginConnection(){World.SpeedChanged+=ExtendMovementCooldown;World.SpeedChanged+=ExtendActionCooldown;World.ActionClock=()=>ActionClock();World.PickupSessionGeneration=()=>generation;}
    internal Func<long> MovementClock=()=>Environment.TickCount64;
    // Record every authoritative slowdown, even if it reverts before the next UI poll.
    void ExtendMovementCooldown(){if(lastMovementAt>=0&&World.MovementIntervalMs is int interval)nextMovementAt=Math.Max(nextMovementAt,lastMovementAt+interval);}
    // This increment only enables walking. Other slowed actions remain explicitly unsupported.
    public bool SlowActionsBlocked=>World.Actor?.Speed==2;
    const string SlowActionNotice="未送出：緩速狀態尚未支援道具、地面物品或交談操作。";
    long lastMovementAt=-1;
    internal long MovementReadyAt=>Math.Max(nextMovementAt,lastMovementAt>=0&&World.MovementIntervalMs is int interval?lastMovementAt+interval:0);
    long nextMovementAt;bool movementSending;int movementLease,inputVersion;
    Func<byte[],Func<bool>,Action,Task<bool>>? sendMovementGate;
    public void CancelMovementInput(){inputVersion++;}
    public event Action<OwnActor,OwnActor>? MovePlanned;
    public event Action<object>? MovementSent;
    public bool MovementAvailable=>State==LoginState.InWorld&&!World.ActionLocked&&!World.TradeBlocksActions&&World.Renderable&&!World.OwnDead&&World.Status?.Hp>0&&World.Actor is {} speedActor&&NormalSpeed.Supported(speedActor.Speed,speedActor.Brave);
    public bool MovementBusy=>MagicBusy||GroundBusy||ItemBusy||CombatBusy||movementSending||MovementClock()<MovementReadyAt;
    void ResetMovementSend(){inputVersion++;movementLease++;movementSending=false;nextMovementAt=0;lastMovementAt=-1;}
    public async Task<bool> Move(int heading)=>(await SendMovement(heading,true,false)).Sent;
    public async Task<bool> Turn(int heading)=>(await SendMovement(heading,false,false)).Sent;
    public Task<MovementAttempt> RequestMovement(int heading,bool step,bool walking)=>walking
        ?Task.FromResult(RefuseMovement("未送出：本地步行尚未結束，請稍候。"))
        :SendMovement(heading,step,false);
    public string MovementBlockReason {
        get {
            if(State!=LoginState.InWorld)return "未送出：尚未進入在線世界。";
            if(World.ActionLocked)return "未送出：世界切換中，請等待載入完成。";
            if(World.TradeBlocksActions)return "未送出：交易操作尚未結束，請先完成或取消交易。";
            if(World.OwnDead||World.Status?.Hp==0)return "未送出：角色已死亡。";
            if(!World.Renderable)return "未送出：角色或地圖尚未就緒／目前外觀未支援。";
            if(World.Actor is not {} speedActor||!NormalSpeed.Supported(speedActor.Speed,speedActor.Brave))return "未送出：目前速度狀態尚未支援步行。";
            if(!MovementAvailable)return "未送出：目前角色狀態不能移動。";
            if(sendMovementGate==null)return "未送出：連線傳送尚未就緒。";
            if(ItemBusy)return "未送出：道具使用或冷卻中，請稍候。";
            if(MagicBusy)return "未送出：施法或冷卻中，請稍候。";
            if(GroundBusy)return "未送出：拾取／丟棄操作或冷卻中，請稍候。";
            if(CombatBusy)return "未送出：攻擊或冷卻中，請稍候。";
            if(movementSending)return "未送出：上一個移動請求仍在處理。";
            if(MovementClock()<MovementReadyAt)return "未送出：移動間隔尚未結束，請稍候。";
            return "";
        }
    }
    public object MovementControlSnapshot(bool walking)=>new{available=MovementAvailable&&sendMovementGate!=null,busy=MovementBusy||walking,reason=walking?"本地步行尚未結束，請稍候。":MovementBlockReason};
    MovementAttempt RefuseMovement(string notice){if(notice.Length==0)notice="未送出：本次操作未通過移動狀態檢查，請重試。";World.MotionNotice=notice;Set(State,notice);return new(false,notice);}
    // Negative-test-only seam; bypasses local terrain and uses a deliberately false base.
    internal async Task<bool> DriftProbe(int heading)=>(await SendMovement(heading,true,true)).Sent;
    async Task<MovementAttempt> SendMovement(int heading,bool step,bool drift,Func<bool>? pointerCurrent=null){
        if(heading is <0 or >7)throw new ArgumentOutOfRangeException(nameof(heading));
        if(pointerCurrent?.Invoke()==false)return new(false,"滑鼠操作已失效。");
        if(!MovementAvailable||MovementBusy||sendMovementGate==null)return RefuseMovement(MovementBlockReason);
        var a=World.Actor!;int stepMs=World.MovementIntervalMs!.Value;int nx=a.X+(step?MovementRules.Dx[heading]:0),ny=a.Y+(step?MovementRules.Dy[heading]:0);
        if(step&&!drift&&!(World.MapCache?.Passable(a.X,a.Y,heading)??MovementRules.Passable(a.X,a.Y,heading)))return RefuseMovement("未送出：靜態地形／已載入範圍邊界阻擋。");
        int epoch=generation,lease=++movementLease,input=inputVersion,world=World.WorldEpoch,corrections=World.Corrections,appearanceRevision=World.AppearanceRevision,speedRevision=World.SpeedRevision;var sender=sendMovementGate;movementSending=true;
        byte[] bytes=step?MovementRules.Move(a.X+(drift?3:0),a.Y,heading):LoginPackets.Pad([225,(byte)heading]);
        try{
            bool sent=await sender(bytes,()=>(pointerCurrent?.Invoke()??true)&&MovementAvailable&&epoch==generation&&world==World.WorldEpoch&&input==inputVersion&&corrections==World.Corrections&&appearanceRevision==World.AppearanceRevision&&speedRevision==World.SpeedRevision&&World.Actor?.Speed==a.Speed&&World.Actor?.Brave==a.Brave&&World.Actor?.Gfx==a.Gfx&&World.Actor?.Weapon==a.Weapon,()=>{
                lastMovementAt=MovementClock();nextMovementAt=lastMovementAt+stepMs;
                if(World.MapCache?.Contains(nx,ny)??Coordinates.Inside(nx,ny))World.Predict(nx,ny,heading,step);
                if(step)MovePlanned?.Invoke(a,World.Actor!);
                Set(State,World.MotionNotice);
            });
            if(epoch!=generation)return new(false,"本次移動流程所屬連線已變更；不沿用舊送出結果。");
            if(world!=World.WorldEpoch)return new(false,"本次移動流程所屬世界已變更；不沿用舊送出結果。");
            if(!sent){
                // Rejection must not discard spacing already extended by a speed report.
                string reason=input!=inputVersion?"未送出：本次移動輸入已取消。":world!=World.WorldEpoch||corrections!=World.Corrections||appearanceRevision!=World.AppearanceRevision||speedRevision!=World.SpeedRevision?"未送出：世界、位置、外觀或速度已變更，舊移動請求已取消。":"未送出：送出前狀態已變更，移動請求未通過驗證。";
                return RefuseMovement(reason);
            }
            lastMovementAt=MovementClock();nextMovementAt=Math.Max(nextMovementAt,lastMovementAt+Math.Max(stepMs,World.MovementIntervalMs??stepMs));
            MovementSent?.Invoke(new{utc=DateTimeOffset.UtcNow,monotonicMs=Environment.TickCount64,plainHex=Convert.ToHexString(bytes),step,drift,from=a,predicted=World.Actor});return new(true,"請求已送出；本地預測，伺服器校正優先（無逐步 ACK）。");
        }
        catch(Exception ex){if(epoch==generation){Cancel();Set(LoginState.Faulted,"移動傳送失敗："+ex.Message);}return new(false,"本次移動傳送失敗或已取消；未確認送出結果。");}
        finally{if(epoch==generation&&lease==movementLease)movementSending=false;}
    }
}
