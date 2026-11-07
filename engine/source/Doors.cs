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
public sealed partial class WorldSession {
    readonly ConcurrentDictionary<uint,WorldObject> doorObjects=new();
    readonly ConcurrentDictionary<(int X,int Y),byte> doorBlockedTiles=new();
    readonly ConcurrentDictionary<(ushort X,ushort Y,byte Direction),DoorTile> doorTiles=new();
    int doorEpoch=-1;
    void EnsureDoorEpoch(){
        if(doorEpoch==WorldEpoch)return;
        doorObjects.Clear();doorBlockedTiles.Clear();doorTiles.Clear();doorEpoch=WorldEpoch;
    }
    public DoorTile[] DoorTiles {get{EnsureDoorEpoch();return doorTiles.Values.ToArray();}}
    public bool IsDoor(uint id)=>GetDoor(id)!=null;
    public WorldObject? GetDoor(uint id){
        EnsureDoorEpoch();
        if(!doorObjects.TryGetValue(id,out var door))return null;
        if(objects.TryGetValue(id,out var current)&&ReferenceEquals(door,current))return door;
        doorObjects.TryRemove(id,out _);return null;
    }
    bool ReceiveDoorPack(byte[] plain,LoginReader r,ushort x,ushort y,uint id){
        if(!DoorPackets.IsPack(plain))return false;
        EnsureDoorEpoch();ReceiveObject(r,x,y,id);
        var door=objects[id] with{DoorAction=plain[11],Dead=plain[11]==37};
        objects[id]=door;doorObjects[id]=door;
        if(door.Dead&&TargetId==id)SelectTarget(null);
        return true;
    }
    bool ReceiveDoorAction(byte[] plain){
        var r=new LoginReader(plain);r.C();uint id=r.D();
        if(!IsDoor(id))return false;
        byte action=r.C();r.Finish();
        // Unknown actions invalidate presentation; they never imply passability.
        UpdateObject(id,o=>o with{DoorAction=DoorPackets.IsAction(action)?action:null,
            ReceivedAction=action,Dead=action==37||o.Dead,MotionAt=0,Combat=null});
        doorObjects[id]=objects[id];if(objects[id].Dead&&TargetId==id)SelectTarget(null);
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
