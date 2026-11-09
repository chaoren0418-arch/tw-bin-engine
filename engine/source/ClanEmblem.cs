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

    // TW16 emblem pixels (sub_96E790): 192 little-endian RGB555 values, R high, 0 = transparent.
    public const int Width=16,Height=12,RawBytes=Width*Height*2;
    public static int[] DecodeRgb555(ReadOnlySpan<byte> data){
        if(data.Length!=RawBytes)throw new ProtocolException("emblem RGB555 length");
        var pixels=new int[Width*Height];
        for(int i=0;i<pixels.Length;i++){int p=BinaryPrimitives.ReadUInt16LittleEndian(data[(i*2)..]);pixels[i]=p==0?0:unchecked((int)0xff000000)|(((p>>10)&31)*255/31<<16)|(((p>>5)&31)*255/31<<8)|((p&31)*255/31);}
        return pixels;
    }

    // R2-K C18 (3.81 C_Emblem; TW16 C_UPLOAD_EMBLEM op 20). The server keeps the whole body after the opcode as the
    // emblem (ClientBasePacket.readByte) and pushes it back in S118, so the body is exactly what clients will draw.
    // 推定: the body is the TW16 S_EMBLEM layout (384 bytes RGB555) because that is what sub_96E790 reads; the original
    // upload send site was not traced. Transparent (alpha < 128) → 0. Opaque pure black also becomes 0 in RGB555, i.e.
    // transparent in the original reader; it is kept that way rather than invented as another colour.
    public const byte UploadOpcode=18;
    public static byte[] EncodeUpload(int[] pixels){
        if(pixels==null||pixels.Length!=Width*Height)throw new ArgumentException("盟徽必須是 16×12 像素。");
        var packet=new byte[1+RawBytes];packet[0]=UploadOpcode;
        for(int i=0;i<pixels.Length;i++){
            uint argb=unchecked((uint)pixels[i]);ushort p=0;
            if(argb>>24>=128)p=(ushort)((((argb>>16)&255)>>3)<<10|(((argb>>8)&255)>>3)<<5|((argb&255)>>3));
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(1+i*2),p);
        }
        return LoginPackets.Pad(packet);
    }
    // The player picks the file (engine-defined entry, not the original's): a 16x12 uncompressed BMP, as S118 accepts.
    public static int[] ReadUploadFile(byte[] file){
        try{return ClanProtocol.DecodeBmp(file,out _);}
        catch(ProtocolException){throw new ArgumentException("盟徽檔必須是 16×12、未壓縮的 BMP（1／4／8／16／24／32 位元）。");}
    }
}

// R2-K PBW-D: S250/173 PLEDGE_EMBLEM_STATUS (packetbox-windows.md §2.12). TW16 sub_C15540 reads C(type) C(value):
// type 1 → sub_B30990(value≠0) writes PledgeUI+1220 (推定: the clan window's "show emblem" check box);
// type 130 → message 3383. 3.81 sends C(173) C(1) C(show) D(0) at login, clan create/join and C_Windows.
// TW16 ignores the trailing D; a 4-byte TW16 body padded to 8 reads the same.
public sealed record ClanEmblemStatus(byte Type,byte Value);
public static class ClanEmblemStatusPackets {
    public const byte Opcode=250,Subtype=173,ShowType=1,WarType=130;
    public const ushort WarMessageId=3383;
    public static ClanEmblemStatus Parse(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=Opcode||r.C()!=Subtype)throw new ProtocolException("not S250/173 emblem status");
        var value=new ClanEmblemStatus(r.C(),r.C());r.D();r.Finish();return value;
    }
}

public sealed partial class WorldSession {
    readonly object clanEmblemStatusGate=new();
    bool? clanShowEmblem;ServerMessageReport? clanEmblemStatusNotice;int clanEmblemStatusCount;
    /// <summary>S250/173 type 1: null until the server sends it this connection.</summary>
    public bool? ClanShowEmblem{get{lock(clanEmblemStatusGate)return clanShowEmblem;}}
    public ServerMessageReport? ClanEmblemStatusNotice{get{lock(clanEmblemStatusGate)return clanEmblemStatusNotice;}}

    [WorldReceive(ClanEmblemStatusPackets.Opcode,Subtype=ClanEmblemStatusPackets.Subtype)]
    bool ReceiveClanEmblemStatus(byte[] plain){
        var status=ClanEmblemStatusPackets.Parse(plain);
        if(status.Type==ClanEmblemStatusPackets.ShowType){lock(clanEmblemStatusGate){clanShowEmblem=status.Value!=0;clanEmblemStatusCount++;}return true;}
        if(status.Type!=ClanEmblemStatusPackets.WarType)return false; // other types: no branch in sub_C15540
        var notice=ServerMessageText.Decode(ClanEmblemStatusPackets.WarMessageId,[]);
        lock(clanEmblemStatusGate){clanEmblemStatusNotice=notice;clanEmblemStatusCount++;}
        // Channel 推定 (same as ClanWarehouseHistory 1889): centre notice only, LastServerMessage untouched so pending
        // clan/trade prompts are not invalidated. 3.81 never sends type 130 (packetbox-windows.md §2.12).
        var(text,recognized)=DisplayNoticeText.Format(ClanEmblemStatusPackets.WarMessageId,[]);
        redNotice=new(ClanEmblemStatusPackets.WarMessageId,[],text,recognized,Environment.TickCount64);
        return true;
    }

    [WorldReset]
    void ResetClanEmblemStatus(){lock(clanEmblemStatusGate){clanShowEmblem=null;clanEmblemStatusNotice=null;clanEmblemStatusCount=0;}}

    [WorldSnapshot("clanEmblemStatus")]
    object? ClanEmblemStatusSnapshot(){
        lock(clanEmblemStatusGate)return new{
            packets=clanEmblemStatusCount,showEmblem=clanShowEmblem,
            notice=clanEmblemStatusNotice is {} n?new{id=n.Id,text=n.Text,recognized=n.Recognized}:null,
            ui="flag only: PledgeUI+1220 check box needs the native clan window (PledgePanel); names still draw emblems",
        };
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
