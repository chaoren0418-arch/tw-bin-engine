using System.Buffers.Binary;
namespace P06;

// Clan emblem cache and automatic C72 query: docs/reference/actions-and-names.md 2.8.
// sub_81F210 asks once per missing id with at most 32 replies outstanding; sub_81EDD0 keeps 512 entries and
// replaces the least recently used one; sub_81F390 keeps a cross-connection disk cache. The disk cache lives in
// the engine's own user-data directory, never in the original client directory.
public sealed class ClanEmblemCache {
    public const int Capacity=512,MaxPending=32;
    const int EntryBytes=4+192*4;
    static readonly byte[] Magic="TWEM"u8.ToArray();
    // Product default: <engine>/runtime/user/emblems.bin (engine/runtime is untracked). Null disables disk use.
    public static string? DiskPath {get;set;}
    public static string DefaultDiskPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","runtime","user","emblems.bin"));
    readonly object gate=new();
    readonly Dictionary<uint,LinkedListNode<ClanEmblem>> entries=[];
    readonly LinkedList<ClanEmblem> recent=new();
    readonly HashSet<uint> pending=[],failed=[];
    int pendingGeneration=int.MinValue;string? loadedPath;
    public int Count {get{lock(gate){EnsureLoaded();return entries.Count;}}}
    public int PendingCount {get{lock(gate)return pending.Count;}}
    public uint[] Pending {get{lock(gate)return pending.Order().ToArray();}}
    public uint[] Ids {get{lock(gate){EnsureLoaded();return recent.Select(e=>e.Id).ToArray();}}}
    public ClanEmblem? Find(uint id){
        lock(gate){
            EnsureLoaded();if(!entries.TryGetValue(id,out var node))return null;
            recent.Remove(node);recent.AddFirst(node);return node.Value with{Pixels=(int[])node.Value.Pixels.Clone()};
        }
    }
    public bool Contains(uint id){lock(gate){EnsureLoaded();return entries.ContainsKey(id);}}
    public void Store(ClanEmblem emblem){
        lock(gate){
            EnsureLoaded();pending.Remove(emblem.Id);failed.Remove(emblem.Id);
            if(entries.Remove(emblem.Id,out var old))recent.Remove(old);
            while(entries.Count>=Capacity){var last=recent.Last!;recent.RemoveLast();entries.Remove(last.Value.Id);}
            entries[emblem.Id]=recent.AddFirst(emblem with{Pixels=(int[])emblem.Pixels.Clone()});Save();
        }
    }
    // An unreadable reply is final for this connection: the original never asks again for a pending id.
    public void Reject(uint id){lock(gate){EnsureLoaded();pending.Remove(id);failed.Add(id);if(entries.Remove(id,out var node)){recent.Remove(node);Save();}}}
    // Generation is the connection; replies cannot arrive for an older connection, so its slots are freed.
    public bool TryReserve(uint id,int generation){
        lock(gate){
            EnsureLoaded();if(generation!=pendingGeneration){pendingGeneration=generation;pending.Clear();failed.Clear();}
            if(id==0||entries.ContainsKey(id)||pending.Contains(id)||failed.Contains(id)||pending.Count>=MaxPending)return false;
            pending.Add(id);return true;
        }
    }
    // Only for a request that never reached the wire.
    public void Release(uint id,int generation){lock(gate){if(generation==pendingGeneration)pending.Remove(id);}}
    void EnsureLoaded(){
        string? path=DiskPath;if(path==loadedPath)return;loadedPath=path;if(path==null)return;
        try{
            if(!File.Exists(path))return;var data=File.ReadAllBytes(path);
            if(data.Length<7||!data.AsSpan(0,4).SequenceEqual(Magic)||data[4]!=1)return;
            int count=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(5));if(count>Capacity||data.Length!=7+count*EntryBytes)return;
            var loaded=new List<ClanEmblem>();
            for(int i=0;i<count;i++){
                var span=data.AsSpan(7+i*EntryBytes,EntryBytes);uint id=BinaryPrimitives.ReadUInt32LittleEndian(span);if(id==0)return;
                var pixels=new int[192];for(int p=0;p<192;p++)pixels[p]=BinaryPrimitives.ReadInt32LittleEndian(span[(4+p*4)..]);loaded.Add(new(id,pixels));
            }
            // File order is most recent first; entries already received this run stay newer.
            foreach(var e in loaded)if(!entries.ContainsKey(e.Id)&&entries.Count<Capacity)entries[e.Id]=recent.AddLast(e);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
    }
    void Save(){
        string? path=DiskPath;if(path==null)return;
        try{
            var data=new byte[7+entries.Count*EntryBytes];Magic.CopyTo(data,0);data[4]=1;BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(5),(ushort)entries.Count);
            int offset=7;foreach(var e in recent){BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset),e.Id);for(int p=0;p<192;p++)BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset+4+p*4),e.Pixels[p]);offset+=EntryBytes;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";File.WriteAllBytes(temp,data);File.Move(temp,path,true);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){}
    }
}

// T135 (#316): the server's emblem ID is a separate number from the clan ID (docs/protocol/clan-emblem.md).
// S167 and S64/0x3c publish it per character; S87 only carries the clan ID.
public sealed record ClanEmblemAssignment(uint ObjectId,uint EmblemId);

public static class ClanEmblemProtocol {
    public const byte AssignmentSubtype=0x3c;
    // C C D D. A shorter S64/0x3c stays unhandled: an emblem must not cost the connection.
    public const int AssignmentLength=10,MaxAssignments=256;
    public static ClanEmblemAssignment DecodeAssignment(byte[] packet){
        var r=new LoginReader(packet);if(r.C()!=64||r.C()!=AssignmentSubtype)throw new ProtocolException("clan emblem assignment subtype");
        var value=new ClanEmblemAssignment(r.D(),r.D());r.Finish();
        if(value.ObjectId==0)throw new ProtocolException("clan emblem assignment identity");return value;
    }
}

public sealed partial class WorldSession {
    public ClanEmblemCache Emblems {get;}=new();
    internal bool ReserveEmblemRequest(uint id,int generation)=>Emblems.TryReserve(id,generation);
    internal void ReleaseEmblemRequest(uint id,int generation)=>Emblems.Release(id,generation);
    sealed record PublishedClanEmblem(uint EmblemId,string Clan);
    // The paint thread reads this outside the world capture gate.
    readonly object clanEmblemGate=new();
    readonly Dictionary<uint,PublishedClanEmblem> publishedClanEmblems=[];
    // Images stay (cross-connection cache); who published which emblem ID belongs to this connection.
    void ResetClanEmblems(){lock(clanEmblemGate)publishedClanEmblems.Clear();}
    void ReceiveClanEmblemAssignment(byte[] packet){var value=ClanEmblemProtocol.DecodeAssignment(packet);ObserveClanEmblemId(value.ObjectId,value.EmblemId);}
    // The clan name fences the ID: a later clan change retires it without a new packet.
    void ObserveClanEmblemId(uint objectId,uint emblemId){
        lock(clanEmblemGate){
            publishedClanEmblems.Remove(objectId);string clan=ClanIdentity(objectId).Clan;if(emblemId==0||clan.Length==0)return;
            if(publishedClanEmblems.Count>=ClanEmblemProtocol.MaxAssignments)publishedClanEmblems.Remove(publishedClanEmblems.Keys.First());
            publishedClanEmblems[objectId]=new(emblemId,clan);
        }
    }
    // The emblem ID this holder's clan published, or 0 when the server never published one.
    public uint PublishedClanEmblemId(uint objectId){
        lock(clanEmblemGate){
            if(!publishedClanEmblems.TryGetValue(objectId,out var published))return 0;
            var (clan,clanId)=ClanIdentity(objectId);return clanId!=0&&clan==published.Clan?published.EmblemId:0;
        }
    }
}

public sealed partial class LoginConnection {
    // T128 paint mount. A published emblem ID is asked for by itself; while it is missing the clan-keyed push
    // (login/upload S118) still shows. Without one, the original's clan-ID request applies (#440).
    public ClanEmblem? ClanEmblemFor(uint objectId,uint clanId){
        if(clanId==0)return null;uint emblemId=World.PublishedClanEmblemId(objectId),id=emblemId!=0?emblemId:clanId;
        if(World.FindClanEmblem(id) is {} emblem)return emblem;
        RequestClanEmblem(id);return id!=clanId?World.FindClanEmblem(clanId):null;
    }
    // Called while drawing a name or head emblem whose pixels are missing (sub_81F210). Fire and forget.
    public bool RequestClanEmblem(uint id){
        if(id==0||State!=LoginState.InWorld||sendMovementGate is not {} sender||World.Emblems.Contains(id))return false;
        int session=generation;if(!World.ReserveEmblemRequest(id,session))return false;
        byte[] packet=ClanProtocol.Encode(ClanCommand.Emblem,emblemId:id);
        _=Send();return true;
        async Task Send(){
            bool sent=false;
            try{sent=await sender(packet,()=>session==generation&&State==LoginState.InWorld,()=>{});}
            catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"盟徽查詢傳送失敗，已斷線；沒有自動重送。");}}
            finally{if(!sent)World.ReleaseEmblemRequest(id,session);}
        }
    }
}
