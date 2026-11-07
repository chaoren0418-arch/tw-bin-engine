namespace P06;

public sealed record ClanEmblemAssignment(uint ObjectId,uint EmblemId);

// Clan IDs and emblem IDs are separate number spaces: docs/protocol/clan-emblem.md.
public static class ClanEmblemProtocol {
    public const byte AssignmentSubtype=0x3c;
    // C C D D. A shorter S64/0x3c stays unhandled: an emblem must not cost the connection.
    public const int AssignmentLength=10,ReplyWindowMs=5000,MaxEmblems=256,MaxAssignments=256,MaxQueries=256;
    public static ClanEmblemAssignment DecodeAssignment(byte[] packet){
        var r=new LoginReader(packet);if(r.C()!=64||r.C()!=AssignmentSubtype)throw new ProtocolException("clan emblem assignment subtype");
        var value=new ClanEmblemAssignment(r.D(),r.D());r.Finish();
        if(value.ObjectId==0)throw new ProtocolException("clan emblem assignment identity");return value;
    }
}

public sealed partial class WorldSession {
    sealed record StoredClanEmblem(ClanEmblem Emblem,long Order);
    sealed record PublishedClanEmblem(uint EmblemId,string Clan);
    const long ClanEmblemQueryClosed=long.MinValue;
    // The paint thread reads this cache outside the world capture gate.
    readonly object clanEmblemGate=new();
    readonly Dictionary<uint,StoredClanEmblem> clanKeyedEmblems=[],emblemKeyedEmblems=[];
    readonly Dictionary<uint,PublishedClanEmblem> publishedClanEmblems=[];
    // Key presence means "already asked"; the value is the reply deadline.
    readonly Dictionary<uint,long> clanEmblemQueries=[];
    long clanEmblemOrder;
    // S118 is not repeated after a map change, so this lives until Reset, not WorldEpoch.
    void ResetClanEmblems(){lock(clanEmblemGate){clanKeyedEmblems.Clear();emblemKeyedEmblems.Clear();publishedClanEmblems.Clear();clanEmblemQueries.Clear();}}
    (string Clan,uint ClanId) ClanEmblemHolder(uint objectId)=>Actor is {} a&&a.ObjectId==objectId?(a.Clan,a.ClanId):objects.TryGetValue(objectId,out var o)?(o.Clan,o.ClanId):("",0);
    void ReceiveClanEmblemAssignment(byte[] packet){var value=ClanEmblemProtocol.DecodeAssignment(packet);ObserveClanEmblemId(value.ObjectId,value.EmblemId);}
    // The clan name fences the ID: a later clan change retires it without a new packet.
    void ObserveClanEmblemId(uint objectId,uint emblemId){
        lock(clanEmblemGate){
            publishedClanEmblems.Remove(objectId);string clan=ClanEmblemHolder(objectId).Clan;if(emblemId==0||clan.Length==0)return;
            if(publishedClanEmblems.Count>=ClanEmblemProtocol.MaxAssignments)publishedClanEmblems.Remove(publishedClanEmblems.Keys.First());
            publishedClanEmblems[objectId]=new(emblemId,clan);
        }
    }
    public ClanEmblem? FindClanEmblem(uint clanId){
        if(clanId==0)return null;
        lock(clanEmblemGate){
            clanKeyedEmblems.TryGetValue(clanId,out var best);
            foreach(var (holder,published) in publishedClanEmblems){
                var (clan,id)=ClanEmblemHolder(holder);
                if(id==clanId&&clan==published.Clan&&emblemKeyedEmblems.TryGetValue(published.EmblemId,out var emblem)&&(best==null||emblem.Order>best.Order))best=emblem;
            }
            return best==null?null:best.Emblem with{Pixels=(int[])best.Emblem.Pixels.Clone()};
        }
    }
    // Only an open C72 makes S118's D an emblem ID. Everything else is a clan-keyed push.
    bool TakeClanEmblemReply(uint id){
        if(!clanEmblemQueries.TryGetValue(id,out long deadline)||ActionClock()>=deadline)return false;
        clanEmblemQueries[id]=ClanEmblemQueryClosed;return true;
    }
    void StoreClanEmblem(ClanEmblem emblem){
        lock(clanEmblemGate){
            var target=TakeClanEmblemReply(emblem.Id)?emblemKeyedEmblems:clanKeyedEmblems;
            if(target.Count>=ClanEmblemProtocol.MaxEmblems&&!target.ContainsKey(emblem.Id))target.Remove(target.Keys.First());
            target[emblem.Id]=new(emblem,++clanEmblemOrder);
        }
    }
    void DropClanEmblem(uint id){lock(clanEmblemGate)(TakeClanEmblemReply(id)?emblemKeyedEmblems:clanKeyedEmblems).Remove(id);}
    bool ClanEmblemWanted(uint holder,PublishedClanEmblem published)=>!clanEmblemQueries.ContainsKey(published.EmblemId)&&!emblemKeyedEmblems.ContainsKey(published.EmblemId)&&ClanEmblemHolder(holder).Clan==published.Clan;
    internal uint NextClanEmblemQuery(){
        lock(clanEmblemGate){
            if(clanEmblemQueries.Count>=ClanEmblemProtocol.MaxQueries)return 0;
            foreach(var (holder,published) in publishedClanEmblems)if(ClanEmblemWanted(holder,published))return published.EmblemId;
            return 0;
        }
    }
    internal bool ClanEmblemQueryWanted(uint emblemId){lock(clanEmblemGate)return publishedClanEmblems.Any(p=>p.Value.EmblemId==emblemId&&ClanEmblemWanted(p.Key,p.Value));}
    internal void AdmitClanEmblemQuery(uint emblemId){lock(clanEmblemGate)clanEmblemQueries[emblemId]=ActionClock()+ClanEmblemProtocol.ReplyWindowMs;}
}

public sealed partial class LoginConnection {
    bool ClanEmblemQueryAccess=>State==LoginState.InWorld&&World.Ready&&!World.TeleportPending;
    // C_Clan has no dead/ghost/teleport gate of its own; one query per pump keeps it low-rate.
    async Task PumpClanEmblemQuery(int epoch){
        if(epoch!=generation||sendMovementGate is not {} sender)return;
        uint id=CaptureWorld(()=>ClanEmblemQueryAccess?World.NextClanEmblemQuery():0);if(id==0)return;
        await sender(ClanProtocol.Encode(ClanCommand.Emblem,emblemId:id),()=>epoch==generation&&ClanEmblemQueryAccess&&World.ClanEmblemQueryWanted(id),()=>World.AdmitClanEmblemQuery(id));
    }
}
