namespace P06;

public sealed record Bookmark(uint Id,string Name,ushort MapId,ushort X,ushort Y,bool DeletePending=false);
public sealed record BookmarkView(int Session,int Epoch,int Revision,uint ActorId,bool CanEdit,ushort Capacity,string Notice,Bookmark[] Items);

public static class BookmarkPackets {
    // Client input policy; the server remains authoritative for capacity and markable maps.
    public static string? NameError(string? name){
        if(string.IsNullOrWhiteSpace(name)||name.Any(char.IsControl))return "請輸入不含控制字元的書籤名稱。";
        try{if(LoginPackets.TextEncoding.GetByteCount(name)>64)return "書籤名稱最多 64 個 Big5 位元組。";}
        catch(System.Text.EncoderFallbackException){return "書籤名稱含有 Big5 無法表示的字元。";}
        return null;
    }
    static byte[] NamePacket(byte opcode,string name){
        if(NameError(name) is {} error)throw new ArgumentException(error,nameof(name));
        return LoginPackets.Pad([opcode,..LoginPackets.TextEncoding.GetBytes(name),0]);
    }
    public static byte[] Add(string name)=>NamePacket(165,name);
    public static byte[] Delete(string name){
        // Existing server names may predate the new-name UI policy; preserve their exact bytes.
        if(string.IsNullOrEmpty(name)||name.Contains('\0'))throw new ArgumentException("書籤名稱不可為空或含 NUL。",nameof(name));
        byte[] bytes=LoginPackets.TextEncoding.GetBytes(name);
        if(bytes.Length>10230)throw new ArgumentException("書籤名稱超出封包上限。",nameof(name));
        return LoginPackets.Pad([3,..bytes,0]);
    }
}

public sealed class Bookmarks {
    readonly object gate=new();
    Dictionary<uint,Bookmark> entries=[];
    int revision;ushort capacity=50;string notice="等待伺服器書籤清單。";
    public int Revision {get{lock(gate)return revision;}}
    public ushort Capacity {get{lock(gate)return capacity;}}
    public string Notice {get{lock(gate)return notice;}}
    public Bookmark[] Items {get{lock(gate)return entries.Values.ToArray();}}
    // Pending deletion is deliberately unavailable to skill selection until a fresh server list.
    public Bookmark? TryGet(uint id){lock(gate)return entries.TryGetValue(id,out var entry)&&!entry.DeletePending?entry:null;}
    public bool TryGet(uint id,out Bookmark? bookmark){bookmark=TryGet(id);return bookmark!=null;}
    internal void Reset(){lock(gate){entries=[];capacity=50;notice="等待伺服器書籤清單。";revision++;}}
    internal void ReceiveList(LoginReader r){
        if(r.C()!=0x2A||r.H()!=0x80||r.C()!=2)throw new ProtocolException("bookmark list header");
        for(int i=0;i<127;i++)if(r.C()!=255)throw new ProtocolException("bookmark list slots");
        ushort max=r.H(),count=r.H();
        // Minimum entry is D + zero-terminated S + HHH. Bound before allocating/reading.
        if(max==0||count>r.Remaining/11)throw new ProtocolException("bookmark list count");
        var next=new Dictionary<uint,Bookmark>();
        for(int i=0;i<count;i++){
            var entry=new Bookmark(r.D(),r.S(),r.H(),r.H(),r.H());
            if(entry.Id==0||!next.TryAdd(entry.Id,entry))throw new ProtocolException("bookmark list identity");
        }
        r.Finish();
        lock(gate){entries=next;capacity=max;notice="已收到伺服器書籤清單。";revision++;}
    }
    internal void ReceiveOne(LoginReader r){
        string name=r.S();ushort map=r.H(),x=r.H(),y=r.H();uint id=r.D();r.Finish();
        if(id==0)throw new ProtocolException("bookmark identity");
        lock(gate){
            // A duplicate delivery cannot undo a locally pending delete (no server delete ack).
            bool pending=entries.TryGetValue(id,out var old)&&old.DeletePending;
            entries[id]=new(id,name,map,x,y,pending);notice="已收到伺服器書籤資料。";revision++;
        }
    }
    internal bool CanAdd(string name){lock(gate)return entries.Count<capacity&&!entries.Values.Any(b=>string.Equals(b.Name,name,StringComparison.OrdinalIgnoreCase));}
    internal void Sent(uint? deleted){lock(gate){
        if(deleted is uint id&&entries.TryGetValue(id,out var entry)){
            entries[id]=entry with{DeletePending=true};notice="已送出刪除；伺服器不提供確認回包，請重新登入核對。";
        }else notice="已要求記憶目前位置；等待伺服器書籤回覆。";
        revision++;
    }}
}

public sealed partial class WorldSession {
    public Bookmarks Bookmarks {get;}=new();
    void ResetBookmarks()=>Bookmarks.Reset();
    bool ReceiveBookmarkList(byte opcode,LoginReader r,byte[] plain){
        // S64 is shared: only the full list header claims the packet. Past it, ReceiveList stays strict.
        if(opcode!=64||plain.Length<5||plain[1]!=0x2A||plain[2]!=0x80||plain[3]!=0||plain[4]!=2)return false;
        Bookmarks.ReceiveList(r);return true;
    }
    void ReceiveBookmark(LoginReader r)=>Bookmarks.ReceiveOne(r);
}

public sealed partial class LoginConnection {
    int bookmarkSendSession=-1,bookmarkSendEpoch=-1;bool bookmarkSending;
    bool BookmarkBusy=>bookmarkSending&&bookmarkSendSession==generation&&bookmarkSendEpoch==World.WorldEpoch;
    bool BookmarkAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {Hp:>0} s&&World.Actor?.ObjectId==s.ObjectId&&!World.OwnDead&&!World.ActionLocked&&!World.TeleportPending&&!World.TradeBlocksActions&&!MovementBusy;
    public BookmarkView BookmarkSnapshot()=>CaptureWorld(()=>new BookmarkView(generation,World.WorldEpoch,World.Bookmarks.Revision,World.Actor?.ObjectId??0,BookmarkAccess&&!BookmarkBusy,World.Bookmarks.Capacity,World.Bookmarks.Notice,World.Bookmarks.Items));
    public async Task<bool> SendBookmark(BookmarkView view,string? name=null,uint? deleteId=null){
        var store=World.Bookmarks;
        bool Current()=>view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Revision==store.Revision&&view.ActorId!=0&&view.ActorId==World.Actor?.ObjectId&&BookmarkAccess;
        if(!Current()||BookmarkBusy||sendMovementGate==null||(deleteId==null)==(name==null))return false;
        var entry=deleteId is uint id?store.TryGet(id):null;
        if(deleteId!=null&&entry==null)return false;
        string requestedName=entry?.Name??name!;
        if(deleteId==null&&(BookmarkPackets.NameError(requestedName)!=null||!store.CanAdd(requestedName)))return false;
        var actor=World.Actor!;ushort? map=World.MapId;var sender=sendMovementGate;
        byte[] packet;
        try{packet=deleteId==null?BookmarkPackets.Add(requestedName):BookmarkPackets.Delete(requestedName);}
        catch(ArgumentException){return false;}
        bookmarkSending=true;bookmarkSendSession=view.Session;bookmarkSendEpoch=view.Epoch;
        try{
            bool Valid()=>Current()&&World.MapId==map&&World.Actor?.X==actor.X&&World.Actor?.Y==actor.Y&&(deleteId==null?store.CanAdd(requestedName):store.TryGet(deleteId.Value)==entry);
            bool sent=await sender(packet,Valid,()=>store.Sent(deleteId));
            return sent&&view.Session==generation&&view.Epoch==World.WorldEpoch;
        }catch(Exception ex){if(view.Session==generation){Cancel();Set(LoginState.Faulted,"書籤請求傳送失敗："+ex.Message);}return false;}
        finally{if(bookmarkSendSession==view.Session&&bookmarkSendEpoch==view.Epoch)bookmarkSending=false;}
    }
}
