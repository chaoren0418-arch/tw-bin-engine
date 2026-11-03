using System.Buffers.Binary;
namespace P06;

// Native BIN e12949cf: VA 0x9E9C90, base 0x400000; fixed JAR d287 uses C231.
// Native opcode60 is not a wire opcode for this server profile.
public static class ShipRules {
    public const int CycleSeconds=10800,FreshMs=3000,ProbeMs=2000;
    public static bool Supported(ushort? map)=>map is 5 or 6;
    public static int Minute(uint seconds)=>(int)(seconds%86400/60);
    public static bool Arrival(ushort map,uint seconds)=>Supported(map)&&Minute(seconds)%180==(map==5?0:90);
    public static ushort Destination(ushort map)=>map switch{5=>4,6=>0,_=>throw new ArgumentOutOfRangeException(nameof(map))};
    public static string DestinationName(ushort map)=>map==5?"古魯丁":"說話之島";
    // SQL names + icon/description/use mapping; ObjectId is never a template ID.
    public static bool Ticket(InventoryItem item,ushort map)=>Supported(map)&&item.ObjectId!=0&&item.Count==1&&item.DescriptionId==0&&item.UseType==255&&item.Charges==0&&item.IconId==138&&item.Bless==1&&(item.Flags&0xF0)==0&&item.Name==(map==5?"$314":"$315");
    public static (ushort X,ushort Y) Landing(ushort map,int rand1,int rand2){
        if(rand1 is <0 or >32767||rand2 is <0 or >32767)throw new ArgumentOutOfRangeException(nameof(rand1));
        return map switch{5=>((ushort)((3*rand1/32767-408)/2+32768),(ushort)(7*rand2/32767+32723)),6=>((ushort)((2*rand1/32767-274)/2+32768),(ushort)(10*rand2/32767+32972)),_=>throw new ArgumentOutOfRangeException(nameof(map))};
    }
    internal static byte[] Packet(ushort map,ushort x,ushort y){
        if(map==5?(x is <32564 or >32566||y is <32723 or >32730):map==6?(x is <32631 or >32632||y is <32972 or >32982):true)throw new ArgumentOutOfRangeException(nameof(x));
        byte[] bytes=new byte[8];bytes[0]=231;BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1),Destination(map));BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(3),x);BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(5),y);return bytes;
    }
    public static bool BoardingOpen(ushort sourceMap,uint seconds){
        int day=(int)(seconds%86400),cycle=day%CycleSeconds;
        return sourceMap==0?cycle>=5400&&cycle<9000:sourceMap==4&&(day<360||day>=10800&&cycle<3600);
    }
}
public sealed record ShipPassage(ushort Source,ushort Destination,ushort X,ushort Y,uint TicketObjectId,uint ServerSeconds,int SourceEpoch,int ClockRevision);
public sealed partial class WorldSession {
    internal Func<long> ShipClock=()=>Environment.TickCount64;
    uint? shipSeconds;long shipSampleAt,shipSentAt;int shipClockEpoch=-1;
    bool shipClockStable,shipDestinationMapReceived;
    public int ShipClockRevision {get;private set;}
    public string ShipState {get;private set;}="idle";
    public ShipPassage? ShipPassage {get;private set;}
    string shipOutcome="";
    public bool ShipPending=>ShipState is "awaiting-arrival" or "unconfirmed";
    public bool ShipClockFresh=>shipClockStable&&shipClockEpoch==WorldEpoch&&shipSeconds!=null&&ShipClock()-shipSampleAt is >=0 and <=ShipRules.FreshMs;
    public uint? ShipServerSeconds=>ShipClockFresh?shipSeconds:null;
    public InventoryItem? ShipTicket=>MapId is ushort map&&InventoryLoaded?Inventory.FirstOrDefault(i=>ShipRules.Ticket(i,map)):null;
    void ResetShip(){shipBackdrop=new();shipSeconds=null;shipSampleAt=shipSentAt=0;shipClockEpoch=-1;shipClockStable=shipDestinationMapReceived=false;ShipClockRevision++;ShipPassage=null;ShipState="idle";shipOutcome="";}
    void ShipTimeReceived(uint seconds){
        long now=ShipClock(),elapsed=now-shipSampleAt;
        if(shipClockEpoch==WorldEpoch&&shipSeconds==seconds)return; // Duplicate samples cannot renew freshness.
        // A clock discontinuity needs a second consistent server sample. No wall clock is used.
        bool consistent=shipSeconds==null||shipClockEpoch!=WorldEpoch||elapsed>=0&&seconds>=shipSeconds.Value&&Math.Abs((double)seconds-shipSeconds.Value-elapsed*0.006)<=30;
        shipSeconds=seconds;shipSampleAt=now;shipClockEpoch=WorldEpoch;shipClockStable=consistent;ShipClockRevision++;
        if(consistent&&MapId is ushort map&&ShipRules.Supported(map))shipBackdrop.Synchronize(map,seconds);
    }
    internal void AdmitShip(ShipPassage passage){
        if(ShipPending||ShipPassage!=null&&ShipPassage.SourceEpoch==WorldEpoch)throw new InvalidOperationException("ship attempt already admitted");
        ShipPassage=passage;shipDestinationMapReceived=false;ShipState="awaiting-arrival";shipSentAt=ShipClock();shipOutcome="到港下船請求已送出，等待伺服器確認；不會自動重送。";
    }
    void ShipMapReceived(){
        shipBackdrop=new();
        shipClockEpoch=-1;shipClockStable=false;
        if(ShipPending&&ShipPassage is {} p){
            shipDestinationMapReceived=MapId==p.Destination;
            if(!shipDestinationMapReceived){ShipState="interrupted";shipOutcome="伺服器切換至其他地圖，本次航程已中止。";}
        }else if(ShipState!="interrupted"&&ShipRules.Supported(MapId)){ShipPassage=null;ShipState="idle";shipOutcome="";}
    }
    void ShipOwnReceived(){
        if(!ShipPending||!shipDestinationMapReceived||ShipPassage is not {} p||Actor is not {} a)return;
        if(MapId==p.Destination&&a.X==p.X&&a.Y==p.Y){ShipState="arrived";shipOutcome=$"已抵達{ShipRules.DestinationName(p.Source)}，伺服器已確認位置。";}
        else {ShipState="unconfirmed";shipOutcome="已收到伺服器位置，但與本次到港落點不同；不會重送，可取消連線。";}
    }
    internal void CheckShipTimeout(){if(ShipPending&&ShipClock()-shipSentAt>=15000){ShipState="unconfirmed";shipOutcome="到港資料尚未完整；不會重送，可取消連線後重新登入確認。";}}
    public string ShipNotice {
        get {
            if(ShipPending||ShipState is "arrived" or "interrupted")return shipOutcome;
            if(MapId is not ushort map||!ShipRules.Supported(map))return "";
            string route="前往"+ShipRules.DestinationName(map)+"：";
            if(!ShipClockFresh)return route+"正在同步航程時間，等候伺服器回覆。";
            if(ShipTicket==null)return route+(InventoryLoaded?"未確認有效船票，無法自動下船；請確認背包或重新登入。":"正在確認背包與船票。");
            uint estimate=shipSeconds!.Value+(uint)Math.Max(0,(ShipClock()-shipSampleAt)*6/1000);
            int phase=(int)(estimate%ShipRules.CycleSeconds),arrival=map==5?0:5400,departure=map==5?9000:3600;
            if(ShipRules.Arrival(map,shipSeconds.Value))return route+"已到港，正在確認下船條件。";
            int remaining=(arrival-phase+ShipRules.CycleSeconds)%ShipRules.CycleSeconds;
            bool waiting=map==5?phase>=5400&&phase<departure:phase<departure;
            int realSeconds=(remaining+5)/6;
            return route+(waiting?"停泊等候出航":"航行中")+$"，預估 {realSeconds/60} 分 {realSeconds%60:00} 秒後到港。";
        }
    }
    public object ShipSnapshot()=>new{state=ShipState,notice=ShipNotice,pending=ShipPending,clockFresh=ShipClockFresh,serverSeconds=shipSeconds,clockRevision=ShipClockRevision,passage=ShipPassage,ticketConfirmed=ShipTicket!=null};
}
public sealed partial class LoginConnection {
    bool shipSending;int shipLease;
    public event Action<object>? ShipSent;
    void ResetShipSend(){shipLease++;shipSending=false;}
    bool CanSendShip()=>State==LoginState.InWorld&&sendMovementGate!=null&&World.Ready&&World.SupportedMap&&World.SupportedAppearance&&!World.ActionLocked&&!World.TradeBlocksActions&&!World.OwnDead&&World.Status?.Hp>0&&ShipRules.Supported(World.MapId)&&World.ShipServerSeconds is uint seconds&&ShipRules.Arrival(World.MapId!.Value,seconds)&&World.ShipTicket!=null&&World.ShipPassage==null&&!MovementBusy&&!interactionSending&&!tradeSending;
    // No UI command accepts a destination or coordinates; the server clock drives this only.
    internal async Task AdvanceShip(){
        World.CheckShipTimeout();if(shipSending||!CanSendShip())return;
        var ticket=World.ShipTicket!;ushort map=World.MapId!.Value;uint seconds=World.ShipServerSeconds!.Value;
        var landing=ShipRules.Landing(map,Random.Shared.Next(32768),Random.Shared.Next(32768));
        int session=generation,world=World.WorldEpoch,clock=World.ShipClockRevision,lease=++shipLease,correction=World.Corrections,teleport=World.TeleportGeneration;uint self=World.Actor!.ObjectId;
        var passage=new ShipPassage(map,ShipRules.Destination(map),landing.X,landing.Y,ticket.ObjectId,seconds,world,clock);
        var sender=sendMovementGate!;byte[] bytes=ShipRules.Packet(map,landing.X,landing.Y);shipSending=true;
        try{
            bool sent=await sender(bytes,()=>session==generation&&lease==shipLease&&world==World.WorldEpoch&&clock==World.ShipClockRevision&&teleport==World.TeleportGeneration&&correction==World.Corrections&&World.Actor?.ObjectId==self&&World.MapId==map&&World.Inventory.Contains(ticket)&&CanSendShip(),()=>{CancelMovementInput();World.AdmitShip(passage);Set(State,World.ShipNotice);});
            if(sent&&session==generation)ShipSent?.Invoke(new{utc=DateTimeOffset.UtcNow,plainHex=Convert.ToHexString(bytes),passage});
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"下船傳送失敗，已斷線；請重新登入確認，沒有自動重送。");}}
        finally{if(session==generation&&lease==shipLease)shipSending=false;}
    }
}
