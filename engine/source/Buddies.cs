namespace P06;

public enum BuddyAction { Query, Add, Delete, ToggleExclude }
public sealed record BuddyEntry(string Name,bool Online);
public sealed record BuddyListPacket(uint Owner,BuddyEntry[] Entries);
public sealed record ExcludePacket(byte Kind,string[] Names);
public sealed record BuddyView(int Generation,int Epoch,uint Actor,int Revision,bool CanSend,bool CanToggle,
    bool Loaded,bool Stale,string Notice,BuddyEntry[] Friends,string[] Excluded,string[] MailExcluded);

// Field layouts and source identities: docs/protocol/buddies.md.
public static class BuddyPackets {
    public static byte[] Encode(BuddyAction action,string name="") {
        if(action==BuddyAction.Query){if(name.Length!=0)throw new ArgumentException("查詢不接受角色名稱。");return LoginPackets.Pad([4]);}
        byte opcode=action switch{BuddyAction.Add=>207,BuddyAction.Delete=>202,BuddyAction.ToggleExclude=>171,_=>throw new ArgumentException("未知名單操作。")};
        ValidateName(name);
        try{return LoginPackets.Pad([opcode,..LoginPackets.TextEncoding.GetBytes(name),0]);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("角色名稱含有遊戲字集無法表示的字元。");}
    }
    internal static void ValidateName(string name){
        // Local input budget, not a claim about server character creation rules.
        if(string.IsNullOrWhiteSpace(name)||name.Length>52||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException("請輸入角色名稱（不可含空白，最多 52 字）。");
    }
    public static bool TryBuddy(byte[] plain,out BuddyListPacket? packet){
        packet=null;if(plain.Length==0||plain[0]!=39)return false;
        var r=new LoginReader(plain);r.C();uint owner=r.D();if(r.S()!="buddy")return false;
        if(owner==0||r.H()!=2||r.H()!=2)throw new ProtocolException("buddy header");
        string all=r.S(),online=r.S();r.Finish();
        var names=Names(all);var connected=Names(online).ToHashSet(StringComparer.OrdinalIgnoreCase);
        packet=new(owner,names.Select(n=>new BuddyEntry(n,connected.Contains(n))).ToArray());return true;
    }
    static string[] Names(string text){
        var names=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(names.Length>4096||names.Any(n=>n.Any(char.IsControl)))throw new ProtocolException("buddy names");
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static bool TryExclude(byte[] plain,out ExcludePacket? packet){
        packet=null;if(plain.Length<2||plain[0]!=250||plain[1] is not (17 or 18 or 19))return false;
        var r=new LoginReader(plain);r.C();byte kind=r.C();int count=kind==17?r.C():1;
        if(count>16)throw new ProtocolException("exclude count");
        var names=new string[count];for(int i=0;i<count;i++){
            names[i]=r.S();if(names[i].Length==0||names[i].Any(char.IsControl))throw new ProtocolException("exclude name");
        }
        r.Finish();packet=new(kind,names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());return true;
    }
}

public sealed partial class WorldSession {
    BuddyEntry[] buddies=[];
    readonly HashSet<string> excluded=new(StringComparer.OrdinalIgnoreCase);
    // M7-V（#529）：信件封鎖名單（MailExcludeUI）。伺服器沒有對應協定（docs/reference/server-protocol.md 只有 C_Exclude），
    // 所以這份名單只存在本機記憶體、重新連線或角色重置即清空，也尚未用來過濾信件（Mail.cs 不在本任務範圍）。
    readonly HashSet<string> mailExcluded=new(StringComparer.OrdinalIgnoreCase);
    public const string MailExcludeNotice="信件封鎖名單只存在本機（伺服器沒有對應協定），重新連線即清空，尚未用來過濾信件。";
    public int BuddyRevision {get;private set;}
    public bool BuddyLoaded {get;private set;}
    public bool BuddyStale {get;private set;}
    public string BuddyNotice {get;private set;}="請查詢好友；封鎖名單依伺服器回覆更新。";
    string? pendingExclude;
    public BuddyEntry[] Buddies=>buddies.ToArray();
    public string[] ExcludedNames=>excluded.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public bool ExcludePending=>pendingExclude!=null;
    public bool IsExcluded(string name)=>excluded.Contains(name);
    public string[] MailExcludedNames=>mailExcluded.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public bool IsMailExcluded(string name)=>mailExcluded.Contains(name);
    /// <summary>Local toggle (no packet). Returns true when the name is now on the list.</summary>
    internal bool ToggleMailExclude(string name){
        BuddyPackets.ValidateName(name);
        bool added=mailExcluded.Remove(name)?false:mailExcluded.Add(name);
        BuddyRevision++;BuddyNotice=(added?"已加入信件封鎖：":"已移出信件封鎖：")+name+"。"+MailExcludeNotice;return added;
    }
    void ResetBuddies(){buddies=[];excluded.Clear();mailExcluded.Clear();BuddyLoaded=BuddyStale=false;pendingExclude=null;BuddyNotice="請查詢好友；封鎖名單依伺服器回覆更新。";BuddyRevision++;}
    bool ReceiveBuddies(byte opcode,byte[] plain){
        if(BuddyPackets.TryBuddy(plain,out var friends)){
            if(Status?.ObjectId!=friends!.Owner)throw new ProtocolException("buddy owner");
            buddies=friends.Entries;BuddyLoaded=true;BuddyStale=false;BuddyRevision++;
            BuddyNotice="已收到好友清單；在線狀態以本次查詢為準。";return true;
        }
        if(BuddyPackets.TryExclude(plain,out var block)){
            if(block!.Kind==17){excluded.Clear();excluded.UnionWith(block.Names);pendingExclude=null;}
            else{
                foreach(string name in block.Names){if(block.Kind==18)excluded.Add(name);else excluded.Remove(name);}
                if(block.Names.Contains(pendingExclude,StringComparer.OrdinalIgnoreCase))pendingExclude=null;
            }
            BuddyRevision++;BuddyNotice="封鎖名單已依伺服器回覆更新。";return true;
        }
        // Observe only these errors; Inventory still owns S71 and displays the original reply.
        if(opcode==71){
            var r=new LoginReader(plain);r.C();ushort id=r.H();
            if(id is 109 or 472){int count=r.C();for(int i=0;i<count;i++)r.S();r.Finish();
                if(id==472){pendingExclude=null;BuddyNotice="原服拒絕：被拒絕的玩家太多（上限 16 人）。";}
                else BuddyNotice="原服回覆：沒有這個角色；請核對名稱並重新查詢。";
                BuddyRevision++;
            }
        }
        return false;
    }
    internal void BeginBuddyRequest(BuddyAction action,string name){
        if(action==BuddyAction.ToggleExclude){pendingExclude=name;BuddyNotice="已送出封鎖切換，等待原服回覆；未回覆前不會再次切換。";}
        else if(action==BuddyAction.Query)BuddyNotice="已查詢，等待原服好友清單。";
        else{BuddyStale=true;BuddyNotice="已送出好友變更；原服非同步處理，請稍後按「查詢好友」確認結果。";}
        BuddyRevision++;
    }
    internal bool BuddyBlocksChat(byte[] plain){
        if(excluded.Count==0||plain.Length<2)return false;
        var r=new LoginReader(plain);byte opcode=r.C();string name;
        if(opcode==73){name=r.S();r.S();r.Finish();}
        else if(opcode==81&&plain[1]==0){
            r.C();r.D();string text=r.S();r.Finish();int separator=text.IndexOf(": ",StringComparison.Ordinal);
            if(separator<=0)return false;name=text[..separator];
        }else return false;
        return IsExcluded(name);
    }
}

public sealed partial class LoginConnection {
    bool buddySending;int buddyGeneration=-1,buddyLease;
    void CheckBuddySendGeneration(){if(buddyGeneration==generation)return;buddyGeneration=generation;buddyLease++;buddySending=false;}
    bool BuddyAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {} s&&World.Actor?.ObjectId==s.ObjectId&&!World.TeleportPending;
    public BuddyView BuddySnapshot()=>CaptureWorld(()=>{
        CheckBuddySendGeneration();return new BuddyView(generation,World.WorldEpoch,World.Status?.ObjectId??0,World.BuddyRevision,
            BuddyAccess&&!buddySending,BuddyAccess&&!buddySending&&!World.ExcludePending,World.BuddyLoaded,World.BuddyStale,World.BuddyNotice,World.Buddies,World.ExcludedNames,World.MailExcludedNames);
    });
    bool BuddyStamp(BuddyView view)=>view.Generation==generation&&view.Epoch==World.WorldEpoch&&view.Actor!=0&&view.Actor==World.Status?.ObjectId&&view.Actor==World.Actor?.ObjectId&&view.Revision==World.BuddyRevision;
    /// <summary>Mail exclude is local only: same access/stamp rules as the server lists, but nothing is sent.</summary>
    public bool ToggleMailExclude(string name,BuddyView view)=>CaptureWorld(()=>{
        CheckBuddySendGeneration();if(!BuddyAccess||!BuddyStamp(view))return false;
        World.ToggleMailExclude(name);return true;
    });
    public async Task<bool> SendBuddy(BuddyAction action,string name,BuddyView view){
        CheckBuddySendGeneration();if(!BuddyAccess||buddySending||!BuddyStamp(view)||sendMovementGate==null||action==BuddyAction.ToggleExclude&&World.ExcludePending)return false;
        byte[] packet=BuddyPackets.Encode(action,name);var sender=sendMovementGate;int lease=++buddyLease;buddySending=true;
        try{
            bool sent=await sender(packet,()=>lease==buddyLease&&BuddyAccess&&BuddyStamp(view),()=>World.BeginBuddyRequest(action,name));
            if(view.Generation==generation)Changed?.Invoke();return sent&&view.Generation==generation;
        }catch(Exception ex){if(view.Generation==generation){Cancel();Set(LoginState.Faulted,"名單傳送失敗："+ex.Message);}return false;}
        finally{if(lease==buddyLease)buddySending=false;}
    }
}
