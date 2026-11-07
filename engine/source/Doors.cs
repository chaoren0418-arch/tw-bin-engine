using System.Buffers.Binary;
using System.Collections.Concurrent;
namespace P06;

public static class DoorPackets {
    // C_Door.start reads H,H,D (the summary table's DHH is incorrect).
    public static byte[] Toggle(uint id,ushort x,ushort y){
        if(id==0)throw new ArgumentOutOfRangeException(nameof(id));
        byte[] b=new byte[9];b[0]=41;
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(1),x);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(3),y);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5),id);
        return LoginPackets.Pad(b);
    }
    public static bool IsAction(byte action)=>action is 28 or 29 or >=32 and <=37;
    // CHARPACK has no type discriminator. Match the complete DoorPack signature,
    // not an empty name, gfx alone, or the shared non-PC flag.
    public static bool IsPack(byte[] b)=>b.Length==40&&b[0]==87&&IsAction(b[11])
        &&b.AsSpan(12,3).SequenceEqual(new byte[3])
        &&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(15))==1
        &&b.AsSpan(19,12).SequenceEqual(new byte[12])&&b[31]==255
        &&b.AsSpan(32,3).SequenceEqual(new byte[3])&&b[35]==255&&b[36]==255;
}

public sealed record DoorTile(ushort X,ushort Y,byte Direction,bool Closed);
// One S158 open/close transition, bound to the door incarnation and world that received it.
public sealed record DoorPlayback(byte Action,long StartedAt,long Generation,int World);
public sealed partial class WorldSession {
    // Id -> full-pack generation. Delta updates replace the WorldObject record but keep the door.
    readonly ConcurrentDictionary<uint,long> doorGenerations=new();
    readonly ConcurrentDictionary<uint,DoorPlayback> doorPlaybacks=new();
    readonly ConcurrentDictionary<(int X,int Y),byte> doorBlockedTiles=new();
    readonly ConcurrentDictionary<(ushort X,ushort Y,byte Direction),DoorTile> doorTiles=new();
    int doorEpoch=-1;long doorGeneration;
    void EnsureDoorEpoch(){
        if(doorEpoch==WorldEpoch)return;
        doorGenerations.Clear();doorPlaybacks.Clear();doorBlockedTiles.Clear();doorTiles.Clear();doorEpoch=WorldEpoch;
    }
    void ForgetDoor(uint id){doorGenerations.TryRemove(id,out _);doorPlaybacks.TryRemove(id,out _);}
    public DoorTile[] DoorTiles {get{EnsureDoorEpoch();return doorTiles.Values.ToArray();}}
    public bool IsDoor(uint id)=>GetDoor(id)!=null;
    public WorldObject? GetDoor(uint id){
        EnsureDoorEpoch();
        if(!doorGenerations.ContainsKey(id))return null;
        if(objects.TryGetValue(id,out var current))return current;
        ForgetDoor(id);return null;
    }
    public DoorPlayback? GetDoorPlayback(uint id){
        if(GetDoor(id) is not {Dead:false} door||!doorPlaybacks.TryGetValue(id,out var play))return null;
        return door.DoorAction==play.Action&&doorGenerations.TryGetValue(id,out long generation)
            &&play.Generation==generation&&play.World==WorldEpoch?play:null;
    }
    // Null means S87 current state: there is no transition to replay, so hold its last frame.
    public double? DoorPlaybackElapsed(uint id,long now)=>GetDoorPlayback(id) is {} play?Math.Max(0,now-play.StartedAt):null;
    bool ReceiveDoorPack(byte[] plain,LoginReader r,ushort x,ushort y,uint id){
        EnsureDoorEpoch();
        if(!DoorPackets.IsPack(plain)){
            if(!doorGenerations.ContainsKey(id))return false;
            // Parse first: a malformed pack must leave the current door intact.
            ReceiveObject(r,x,y,id);ForgetDoor(id);return true;
        }
        ReceiveObject(r,x,y,id);ForgetDoor(id);
        var door=objects[id] with{DoorAction=plain[11],Dead=plain[11]==37};
        objects[id]=door;doorGenerations[id]=++doorGeneration;
        if(door.Dead&&TargetId==id)SelectTarget(null);
        return true;
    }
    bool ReceiveDoorAction(byte[] plain){
        var r=new LoginReader(plain);r.C();uint id=r.D();
        if(GetDoor(id) is not {} before)return false;
        byte action=r.C();r.Finish();
        // Unknown actions invalidate presentation; they never imply passability.
        UpdateObject(id,o=>o with{DoorAction=DoorPackets.IsAction(action)?action:null,
            ReceivedAction=action,Dead=action==37||o.Dead,MotionAt=0,Combat=null});
        // A repeated report keeps its running playback; only a state change starts one.
        if(action is not (28 or 29)||objects[id].Dead)doorPlaybacks.TryRemove(id,out _);
        else if(before.DoorAction!=action)doorPlaybacks[id]=new(action,Environment.TickCount64,doorGenerations[id],WorldEpoch);
        if(objects[id].Dead&&TargetId==id)SelectTarget(null);
        return true;
    }
    void ReceiveDoorAttribute(LoginReader r){
        ushort x=r.H(),y=r.H();byte direction=r.C(),passable=r.C();r.Finish();
        if(direction>1||passable>1)throw new ProtocolException("invalid door attribute");
        EnsureDoorEpoch();doorTiles[(x,y,direction)]=new(x,y,direction,passable==1);
        // L1V1Map.set_door_0/1: the server overlays the reported entrance and
        // its two neighbours. Wide doors send one S209 per entrance segment.
        for(int i=-1;i<=1;i++){
            var p=(X:x+(direction==0?i:0),Y:y+(direction==1?i:0));
            if(passable==1)doorBlockedTiles[p]=1;else doorBlockedTiles.TryRemove(p,out _);
        }
    }
    public bool DoorBlocks(int x,int y,int heading){
        EnsureDoorEpoch();if(heading is <0 or >7)return true;
        int dx=MovementRules.Dx[heading],dy=MovementRules.Dy[heading];
        return doorBlockedTiles.ContainsKey((x+dx,y+dy))
            ||((heading&1)!=0&&(doorBlockedTiles.ContainsKey((x+dx,y))||doorBlockedTiles.ContainsKey((x,y+dy))));
    }
}

public sealed partial class LoginConnection {
    public async Task<bool> ToggleDoor(uint id,Func<bool>? current=null){
        if(!MovementAvailable||MovementBusy||sendMovementGate==null||current?.Invoke()==false
            ||World.GetDoor(id) is not {Dead:false,AppearancePending:false} door)return false;
        var actor=World.Actor;int epoch=generation,world=World.WorldEpoch,input=inputVersion,lease=++movementLease;
        var sender=sendMovementGate;movementSending=true;
        bool Valid()=>epoch==generation&&world==World.WorldEpoch&&input==inputVersion&&MovementAvailable
            &&Equals(actor,World.Actor)&&(current?.Invoke()??true)&&!MagicBusy&&!GroundBusy&&!ItemBusy&&!CombatBusy
            &&ReferenceEquals(door,World.GetDoor(id));
        try{return await sender(DoorPackets.Toggle(id,door.X,door.Y),Valid,()=>{})&&Valid();}
        catch(Exception ex){if(epoch==generation){Cancel();Set(LoginState.Faulted,"門操作傳送失敗："+ex.Message);}return false;}
        finally{if(epoch==generation&&lease==movementLease)movementSending=false;}
    }
}
