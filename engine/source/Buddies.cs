namespace P06;

public enum BuddyAction { Query, Add, Delete, ToggleExclude }
public sealed record BuddyEntry(string Name,bool Online);
/// <param name="All">S39 第一個字串原文（全部名單，空格分隔）；R10-V 開 buddy 頁用。</param>
/// <param name="Online">S39 第二個字串原文（在線名單）。</param>
public sealed record BuddyListPacket(uint Owner,BuddyEntry[] Entries,string All="",string Online="");
public sealed record ExcludePacket(byte Kind,string[] Names);
public sealed record BuddyView(int Generation,int Epoch,uint Actor,int Revision,bool CanSend,bool CanToggle,
    bool Loaded,bool Stale,string Notice,BuddyEntry[] Friends,string[] Excluded,string[] MailExcluded);

/// <summary>
/// R10-C（T387／#902）：好友相關的 HyperAct（FU9§9.2，已驗證）：`addbuddy` → 輸入框 588「在好友清單上增加誰？」。
/// 588 原文用 docs/reference/rea-scripts/t267/text_tables.py 從 0-客戶端 Text.pak 的 string-c.tbl（sha256 4ebbc1a7…2d43e9）讀出；
/// 執行期照字串表，表不在時用同一句。
/// R10-V（T409／#926，FU13§3.1）：HyperAct `buddy` 原版只送 `c` 56（＝3.81 C4），是好友名單的入口之一（另一個是 `/buddy`）。
/// R11-C（T417／#959，FU9§9.2 第 357 行，已驗證）：`buddy`／`refreshbuddy` → `c` 56（3.81 C4）；`delbuddy` → 輸入框 589「在好友清單上刪除誰？」
/// （589 原文同樣從 string-c.tbl sha256 4ebbc1a7…2d43e9 讀出）→ 名稱送 C202＋C4（同 `/delbuddy 名稱`，原版 `cs` 124＋`c` 56）。
/// 分派入口是 LoginConnection.BuddyHyperAction（NpcInteraction.cs 的 ClientHyperAction／QueryOption 各一行）。
/// 推測／引擎自訂（標明）：589 框名稱不合法時印 53、不送（比照 588／sub_F43160；delbuddy 輸入框原版的名稱檢查沒查）；
/// 送出後頁面不關（同 588，原版關窗時機沒查）；C4 回覆的 S39 buddy 照 R10-V 開 buddy 頁。
/// </summary>
public enum BuddyHyperAct { Query, Add, Delete }
public static class BuddyHyperActs {
    public const string AddBuddy="addbuddy",Buddy="buddy",RefreshBuddy="refreshbuddy",DelBuddy="delbuddy";
    public const int AddInputText=588,DelInputText=589;
    public const string AddInputFallback="在好友清單上增加誰？",DelInputFallback="在好友清單上刪除誰？";
    /// <summary>動作名稱比照 checkpk／accepttrade：去頭尾空白、不分大小寫（原版比對方式沒查）。</summary>
    public static bool IsAddBuddy(string? action)=>string.Equals(action?.Trim(),AddBuddy,StringComparison.OrdinalIgnoreCase);
    /// <summary>本組（好友 HyperAct）處理的動作；不是本組的回 null（照舊交給其他分派或送 C125）。</summary>
    public static BuddyHyperAct? Classify(string? action){
        string a=action?.Trim()??"";
        bool Is(string n)=>string.Equals(a,n,StringComparison.OrdinalIgnoreCase);
        return Is(AddBuddy)?BuddyHyperAct.Add:Is(DelBuddy)?BuddyHyperAct.Delete:Is(Buddy)||Is(RefreshBuddy)?BuddyHyperAct.Query:null;
    }
    /// <summary>名稱框是好友輸入框（588／589）。</summary>
    public static bool IsInput(HtmlNameKind kind)=>kind is HtmlNameKind.AddBuddy or HtmlNameKind.DelBuddy;
    public static int InputText(HtmlNameKind kind)=>kind==HtmlNameKind.DelBuddy?DelInputText:AddInputText;
    public static string InputPrompt(HtmlNameKind kind)=>kind==HtmlNameKind.DelBuddy?DelInputPrompt:AddInputPrompt;
    public static string AddInputPrompt=>Prompt(AddInputText,AddInputFallback);
    public static string DelInputPrompt=>Prompt(DelInputText,DelInputFallback);
    static string Prompt(int id,string fallback)=>ServerMessageText.Table is {} t&&id<t.Count
        &&ServerMessageText.DisplayText(t[id]).Trim() is {Length:>0} s?s:fallback;
}

/// <summary>
/// R10-V（T409／#926，FU13 D4）：收到 S39 `buddy` 時 3.81 開的 HTML 頁（已驗證，靜態，3.81 OEP 快照
/// `LiTo.unpacked-oep-current.exe` SHA-256 4271d92f…2ab6e9；地址皆 VA，快照 FO＝VA−0x400000）：
///   - S_HYPERTEXT 處理函式 sub_4A5220 用 "dssh" 讀 objid、htmlid、（第一個 H 當 1 字元字串吃掉）、參數數 n；
///     htmlid 是 "pledgeM"／"pledge"／"buddy"（0x4A549B–0x4A554D）→ HTML 物件 dword_647E14 的旗標 +0x181＝1。
///   - 逐一讀 n 個字串（0x4A5567）：旗標為 1 且（不是第 0 個，或 htmlid＝"buddy"）時，先把「字串裡空格的個數」
///     用 "%d" 加成一個變數（0x4A55D6–0x4A561F），再加字串本身（0x4A5631；sub_43A710＝strdup 後 push 到 +0x290 的陣列）。
///   - 然後 vtable+0x54(htmlid,1) 開頁（0x4A5654）。頁面 `<var src="#n">` 直接取陣列第 n 個（0x4369A8 解析 '#'＋數字，n＜900 時 0x438740 取 [+0x294]+4n）。
/// 所以 3.81 伺服器（S_Buddy：H2 H2 S 全部 S 在線）的變數是 [全部空格數, 全部, 在線空格數, 在線]；
/// buddy-c.html（`好友名單(全部#0人／在線#1人)：<br><p>#2`）實際顯示 #0＝全部人數、#1＝全部名單字串、#2＝在線人數。
/// 這和 FU13 推測的「#0／#1 人數、#2 名單」不同；本引擎照 3.81 的實際陣列（真機再確認畫面）。
/// </summary>
public static class BuddyPage {
    public const string Html="buddy";
    /// <summary>3.81 數的是空格字元個數（伺服器每個名字後面帶一個空格，所以通常＝人數）。</summary>
    public static int SpaceCount(string text)=>text.Count(c=>c==' ');
    public static string[] Arguments(string all,string online)=>
        [SpaceCount(all).ToString(System.Globalization.CultureInfo.InvariantCulture),all,SpaceCount(online).ToString(System.Globalization.CultureInfo.InvariantCulture),online];
}

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
        packet=new(owner,names.Select(n=>new BuddyEntry(n,connected.Contains(n))).ToArray(),all,online);return true;
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
            BuddyNotice="已收到好友清單；在線狀態以本次查詢為準。";
            // R10-V（#926，FU13 D4）：3.81 每次收到 S39 buddy 都開 buddy 頁（BuddyPage）；放在伺服器查詢頁（HYPER_TEXT_WINDOW），
            // 不動 NPC 對話。沒有模板時以解碼後的參數文字顯示（HyperTextFallbackNotice）。
            if(Ready){var args=BuddyPage.Arguments(friends.All,friends.Online);ShowQueryPage(friends.Owner,BuddyPage.Html,args,args);}
            return true;
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
    /// <summary>R10-C（#902）：FriendUI Delete 的 1813「是」送出 C202 後本地移除（FU9§9.2：sub_B18F10 本地移除，不送 56）。</summary>
    internal void RemoveBuddyLocal(string name){
        var next=buddies.Where(b=>!string.Equals(b.Name,name,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(next.Length!=buddies.Length)buddies=next;
        BuddyNotice="已送出刪除好友「"+name+"」；名單已先在本機移除，結果以原服回覆為準。";BuddyRevision++;
    }
    /// <summary>R10-C：好友輸入框的本機訊息（例如 53）放進聊天欄（預設字色），同時更新名單提示。</summary>
    internal void BuddyLocalLine(string text){
        playerChatLines.Enqueue(new(++playerChatSequence,0,PlayerChatProtocol.Display(text)));
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;BuddyNotice=text;BuddyRevision++;
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
    /// <param name="localRemove">R10-C：FriendUI Delete（1813 是）＝送 C202 後本地移除，不另查詢（FU9§9.2）。只對 Delete 有效。</param>
    public async Task<bool> SendBuddy(BuddyAction action,string name,BuddyView view,bool localRemove=false){
        CheckBuddySendGeneration();if(!BuddyAccess||buddySending||!BuddyStamp(view)||sendMovementGate==null||action==BuddyAction.ToggleExclude&&World.ExcludePending)return false;
        byte[] packet=BuddyPackets.Encode(action,name);var sender=sendMovementGate;int lease=++buddyLease;buddySending=true;
        try{
            bool sent=await sender(packet,()=>lease==buddyLease&&BuddyAccess&&BuddyStamp(view),()=>{
                World.BeginBuddyRequest(action,name);if(localRemove&&action==BuddyAction.Delete)World.RemoveBuddyLocal(name);});
            if(view.Generation==generation)Changed?.Invoke();return sent&&view.Generation==generation;
        }catch(Exception ex){if(view.Generation==generation){Cancel();Set(LoginState.Faulted,"名單傳送失敗："+ex.Message);}return false;}
        finally{if(lease==buddyLease)buddySending=false;}
    }

    /// <summary>
    /// R9-C（#847，FU9§9.2）：好友／封鎖聊天指令。3.81：C4 C_Buddy、C207 C_AddBuddy、C202 C_DelBuddy、C171 C_Exclude（切換）。
    /// - `/buddy`（947）→ C4（FU13§3.1：TW16 沒有開 FriendUI 的鍵；名單入口就是 /buddy 與 HyperAct buddy）。回覆 S39 buddy 開 buddy 頁（BuddyPage）。
    /// - `/addbuddy 名稱`（948，sub_F43160）：名稱檢查失敗印 53；成功送 C207＋C4（原版 `cs` 138＋`c` 56）。
    /// - `/delbuddy 名稱`（949）→ C202＋C4（原版 `cs` 124＋56）。
    /// - `/exclude 名稱`（366）：原版固定傳空字串（開封鎖頁或送 TW16 擴充包；已驗證，FU13§3.2：Commandexclude sub_F54D40 → sub_F40FC0("")）；
    ///   3.81 伺服器只認以名稱切換的 C171，所以帶名稱時維持送 C171。
    ///   已在封鎖名單時不送（C171 會變成解除），提示改用 /include；不帶名稱只提示。
    /// - `/include 名稱`（470）：原版送 C217，3.81 沒有；改用 C171 切換，只在名稱已在封鎖名單（以伺服器回覆的名單為準）時送。
    /// </summary>
    internal ChatCommandPlan BuddyChatCommand(ChatFollowupCommand c){
        string name=c.Argument;
        switch(c.Kind){
        case ChatFollowupKind.Buddy:
            return ChatCommandPlan.Send("已查詢好友名單，等待原服回覆。",()=>World.BeginBuddyRequest(BuddyAction.Query,""),BuddyPackets.Encode(BuddyAction.Query));
        case ChatFollowupKind.AddBuddy:{
            byte[] add;try{add=BuddyPackets.Encode(BuddyAction.Add,name);}catch(ArgumentException){return ChatCommandPlan.Refuse(ChatFollowupCommands.InvalidNameText);}
            return ChatCommandPlan.Send("已送出新增好友「"+name+"」並查詢名單；結果以原服回覆為準。",()=>World.BeginBuddyRequest(BuddyAction.Add,name),add,BuddyPackets.Encode(BuddyAction.Query));}
        case ChatFollowupKind.DelBuddy:
            if(name.Length==0)return ChatCommandPlan.Refuse("格式：/delbuddy 名稱");
            return ChatCommandPlan.Send("已送出刪除好友「"+name+"」並查詢名單；結果以原服回覆為準。",()=>World.BeginBuddyRequest(BuddyAction.Delete,name),BuddyPackets.Encode(BuddyAction.Delete,name),BuddyPackets.Encode(BuddyAction.Query));
        case ChatFollowupKind.Exclude:
            if(name.Length==0)return ChatCommandPlan.Refuse("格式：/exclude 名稱（3.81 只能以名稱封鎖）");
            if(World.ExcludePending)return ChatCommandPlan.Refuse("前一個封鎖切換還在等待原服回覆；未送出。");
            if(World.IsExcluded(name))return ChatCommandPlan.Refuse(name+" 已在封鎖名單；要解除請用 /include "+name+"。");
            return ExcludeToggle(name,"已送出封鎖「"+name+"」，等待原服回覆。");
        default:
            if(name.Length==0)return ChatCommandPlan.Refuse("格式：/include 名稱");
            if(World.ExcludePending)return ChatCommandPlan.Refuse("前一個封鎖切換還在等待原服回覆；未送出。");
            if(!World.IsExcluded(name))return ChatCommandPlan.Refuse(name+" 不在封鎖名單（以原服回覆的名單為準）；未送出。");
            return ExcludeToggle(name,"已送出解除封鎖「"+name+"」，等待原服回覆。");
        }
    }
    /// <summary>
    /// R11-C（#959）：好友 HyperAct 分派（NPC 頁與伺服器主動頁共用）。回 null＝不是好友動作（照舊走其他分派或 C125）；
    /// 否則回「有沒有送出或開框」，不會再落到 C125。
    /// addbuddy → 588 框；delbuddy → 589 框（不送包）；buddy／refreshbuddy → 只送 C4。
    /// </summary>
    internal Task<bool>? BuddyHyperAction(string action,bool query)=>BuddyHyperActs.Classify(action) switch{
        BuddyHyperAct.Add=>OpenBuddyInput(HtmlNameKind.AddBuddy,query),
        BuddyHyperAct.Delete=>OpenBuddyInput(HtmlNameKind.DelBuddy,query),
        BuddyHyperAct.Query=>SendBuddySequence(()=>World.BeginBuddyRequest(BuddyAction.Query,""),BuddyPackets.Encode(BuddyAction.Query)),
        _=>null};
    /// <summary>R10-C／R11-C：開 588／589 輸入框（頁內名稱框，同 R7-D 的 HtmlNamePrompt）；不送包。</summary>
    Task<bool> OpenBuddyInput(HtmlNameKind kind,bool query){
        World.OpenHtmlNamePrompt(kind,query);Changed?.Invoke();return Task.FromResult(true);
    }
    /// <summary>588／589 輸入框與 HyperAct C4 現在可以送出（名單存取同 SendBuddy；沒有正在送的名單請求）。</summary>
    internal bool BuddyInputCanSend{get{CheckBuddySendGeneration();return BuddyAccess&&!buddySending&&sendMovementGate!=null;}}
    /// <summary>
    /// R10-C：588 輸入框確定 → 同 `/addbuddy 名稱`（sub_F43160）：名稱檢查失敗印 53、不送；成功送 C207＋C4（原版 `cs` 138＋`c` 56）。
    /// R11-C：589 輸入框確定 → 同 `/delbuddy 名稱`：送 C202＋C4（原版 `cs` 124＋`c` 56）；不本地移除（那是 FriendUI Delete 的做法）。
    /// 名稱不合法印 53 是比照 588（推測）。
    /// </summary>
    internal Task<bool> BuddyFromInput(HtmlNameKind kind,string name){
        if(!BuddyInputCanSend)return Task.FromResult(false);
        var action=kind==HtmlNameKind.DelBuddy?BuddyAction.Delete:BuddyAction.Add;
        byte[] first;
        try{first=BuddyPackets.Encode(action,name);}
        catch(ArgumentException){World.BuddyLocalLine(ChatFollowupCommands.InvalidNameText);Changed?.Invoke();return Task.FromResult(false);}
        return SendBuddySequence(()=>World.BeginBuddyRequest(action,name),first,BuddyPackets.Encode(BuddyAction.Query));
    }
    /// <summary>依序送出；第一包放行後才送後面的包（C4 被擋只少了查詢）。apply 只在第一包放行時執行。</summary>
    async Task<bool> SendBuddySequence(Action apply,byte[] first,params byte[][] rest){
        if(!BuddyInputCanSend)return false;
        var sender=sendMovementGate!;int session=generation,lease=++buddyLease;buddySending=true;
        bool Valid()=>session==generation&&lease==buddyLease&&BuddyAccess;
        try{
            bool sent=await sender(first,Valid,apply);
            foreach(var packet in rest){if(!sent||session!=generation)break;await sender(packet,Valid,()=>{});}
            if(session==generation)Changed?.Invoke();return sent&&session==generation;
        }catch(Exception ex){if(session==generation){Cancel();Set(LoginState.Faulted,"名單傳送失敗："+ex.Message);}return false;}
        finally{if(lease==buddyLease)buddySending=false;}
    }
    ChatCommandPlan ExcludeToggle(string name,string notice){
        bool wasExcluded=World.IsExcluded(name);
        return new(notice,[BuddyPackets.Encode(BuddyAction.ToggleExclude,name)],()=>World.BeginBuddyRequest(BuddyAction.ToggleExclude,name),
            ()=>!World.ExcludePending&&World.IsExcluded(name)==wasExcluded);
    }
}
