namespace P06;
// Only the parsed packet shapes wrapped by Teleportation.abortPcTeleport.
// There is no wrapper opcode; this recognizes the wire variant, not its provenance.
internal static class TeleportRecoveryPackets {
    internal static bool Matches(ReadOnlySpan<byte> fields){
        if(fields.IsEmpty)return false;
        return fields[0] switch {
            202=>fields.Length==2&&fields[1]==7, // TYPE_TELEPORT_UNLOCK, false
            206=>fields.Length==4&&fields[3]<=1, // S_MapID(int, boolean)
            119=>fields.Length==6, // S_CharVisualUpdate
            87=>OwnCharacter(fields),
            _=>false
        };
    }
    static bool OwnCharacter(ReadOnlySpan<byte> fields){
        // S_OwnCharPack: 21 fixed bytes, name/title, status/clanId, clan/empty owner,
        // then rank, HP%, brave, 00, 00, FF, FF. Different S87 layouts are excluded.
        if(fields.Length<37||fields[12]>7)return false;
        int at=21;
        if(!SkipString(fields,ref at)||!SkipString(fields,ref at))return false;
        if(fields.Length-at<5||(fields[at]&4)==0)return false;
        at+=5;
        if(!SkipString(fields,ref at)||fields.Length-at!=8||fields[at]!=0)return false;
        return fields[^4]==0&&fields[^3]==0&&fields[^2]==255&&fields[^1]==255;
    }
    static bool SkipString(ReadOnlySpan<byte> fields,ref int at){
        int end=fields[at..].IndexOf((byte)0);
        if(end<0)return false;
        at+=end+1;return true;
    }
}
public sealed partial class WorldSession {
    public int TeleportGeneration {get;private set;}
    public string TeleportState {get;private set;}="idle";
    public string TeleportNotice {get;private set;}="";
    public ushort? TeleportMarker {get;private set;}
    public uint? TeleportPortal {get;private set;}
    public bool TeleportAckPending=>TeleportState=="ack-pending";
    public bool TeleportPending=>TeleportState is "ack-pending" or "awaiting-world" or "awaiting-own" or "recovery";
    readonly HashSet<byte> paralysis=[];long teleportAt;
    public bool ActionLocked=>BasicSupplyPending||ShipPending||TeleportPending||paralysis.Count>0||CacheEnabled&&MapResourceState!="ready";
    void ResetTeleport(){TeleportGeneration++;TeleportState="idle";TeleportNotice="";TeleportMarker=null;TeleportPortal=null;paralysis.Clear();teleportAt=0;}
    bool ReceiveTeleport(byte opcode,LoginReader r){
        if(opcode==149){
            ushort marker=r.H();uint portal=r.D();r.Finish();
            if(TeleportPending&&TeleportMarker==marker&&TeleportPortal==portal)return true;
            TeleportGeneration++;TeleportMarker=marker;TeleportPortal=portal;TeleportState="ack-pending";teleportAt=SceneClock();TeleportNotice="伺服器要求傳送，正在確認；等待新世界資料。";
            ClearObjects();Actor=null;ResetMovement();ResetCombat();ClearMagicEffects();return true;
        }
        if(opcode!=202)return false;byte mode=r.C();r.Finish();
        if(mode==7){paralysis.Remove(6);if(TeleportPending){TeleportGeneration++;TeleportState="recovery";TeleportNotice="原服解除傳送鎖定；等待地圖與角色恢復資料。";}return true;}
        if(mode is 2 or 4 or 6 or 10 or 12 or 22 or 24)paralysis.Add(mode);
        else if(mode is 3 or 5 or 11 or 13 or 23 or 25)paralysis.Remove((byte)(mode-1));else Unhandled(opcode);
        return true;
    }
    internal void AdmitTeleportAck(){if(!TeleportAckPending)throw new InvalidOperationException("stale teleport acknowledgement");TeleportState="awaiting-world";TeleportNotice="傳送確認已送出；等待原服地圖與角色，沒有自動重送。";}
    void MapArrived(){ShipMapReceived();TeleportGeneration++;TeleportState="awaiting-own";teleportAt=SceneClock();TeleportNotice="已收到地圖；等待角色位置與原始資源載入。";}
    void OwnArrived(){ShipOwnReceived();if(!TeleportPending)return;TeleportState="arrived";RefreshArrivalNotice();paralysis.Remove(6);}
    void RefreshArrivalNotice(){if(TeleportState=="arrived")TeleportNotice=SupportedMap?"已收到原服角色位置並載入地圖。":MapResourceState=="loading"?"已收到原服角色位置；正在載入地圖。":"已收到原服角色位置；地圖資源尚不可用。";}
    internal void CheckTeleportTimeout(){CheckBasicSupplyTimeout();if(TeleportPending&&SceneClock()-teleportAt>=15000)TeleportNotice="傳送資料尚未完整；操作保持鎖定，可取消連線或回離線預覽。沒有自動重送。";}
    public object TeleportSnapshot()=>new{generation=TeleportGeneration,state=TeleportState,notice=TeleportNotice,marker=TeleportMarker,portal=TeleportPortal,pending=TeleportPending,actionLocked=ActionLocked,paralysis=paralysis.Order().ToArray()};
}
public sealed partial class LoginConnection {
    bool teleportAckSending;int teleportAckLease;
    void ResetTeleportSend(){teleportAckLease++;teleportAckSending=false;}
    async Task AcknowledgeTeleport(){
        if(!World.TeleportAckPending||teleportAckSending||sendMovementGate==null)return;
        int session=generation,transition=World.TeleportGeneration,world=World.WorldEpoch,lease=++teleportAckLease;var sender=sendMovementGate;teleportAckSending=true;
        try{await sender(LoginPackets.Pad([52]),()=>session==generation&&transition==World.TeleportGeneration&&world==World.WorldEpoch&&World.TeleportAckPending&&State is LoginState.EnteringWorld or LoginState.InWorld,()=>{World.AdmitTeleportAck();Set(State,World.TeleportNotice);});}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"傳送確認失敗，已斷線；沒有自動重送。");}}
        finally{if(session==generation&&lease==teleportAckLease){teleportAckSending=false;if(World.TeleportAckPending&&transition!=World.TeleportGeneration)_=AcknowledgeTeleport();}}
    }
}
