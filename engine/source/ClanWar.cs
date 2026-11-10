namespace P06;

public enum ClanWarCommand:byte {Declare=0,Surrender=2,Cease=3}
public sealed record ClanWarEvent(byte Type,string First,string Second);
public sealed record ClanWarPrompt(long Revision,ushort Message,string Clan,string Leader,int Epoch,int Interaction);
public sealed record ClanWarView(int Session,int Epoch,uint Actor,int Revision,bool CanRequest,bool CanAnswer,string Clan,string[] Enemies,string Notice,ClanWarPrompt? Prompt);

// Wire evidence: docs/protocol/clan-war.md. The zero D in S219/C121 is not a request ID.
public static class ClanWarProtocol {
    public static bool IsPrompt(ushort message)=>message is 217 or 221 or 222;
    static bool WireName(string name)=>!string.IsNullOrWhiteSpace(name)&&!name.Any(char.IsControl);
    public static bool Same(string left,string right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
    public static byte[] Encode(ClanWarCommand command,string name){
        if(command is not (ClanWarCommand.Declare or ClanWarCommand.Surrender or ClanWarCommand.Cease))throw new ArgumentException("未知血盟戰爭操作。");
        // A single space selects the server's castle path; this module only names a clan.
        if(string.IsNullOrWhiteSpace(name)||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))throw new ArgumentException("血盟名稱不可為空白或含控制字元。");
        byte[] text;
        try{text=LoginPackets.TextEncoding.GetBytes(name);}catch(System.Text.EncoderFallbackException){throw new ArgumentException("血盟名稱含遊戲字集無法表示的字元。");}
        if(text.Length>32)throw new ArgumentException("血盟名稱最多 32 個 Big5 位元組。");
        return LoginPackets.Pad([227,(byte)command,..text,0]);
    }
    public static byte[] Answer(ushort message,bool accept){
        if(!IsPrompt(message))throw new ArgumentException("未知血盟戰爭確認訊息。");
        return LoginPackets.Pad([121,0,0,0,0,0,0,(byte)message,(byte)(message>>8),(byte)(accept?1:0)]);
    }
    public static ClanWarEvent Decode(byte[] packet){
        var r=new LoginReader(packet);if(r.C()!=84)throw new ProtocolException("clan war opcode");
        var value=new ClanWarEvent(r.C(),r.S(),r.S());r.Finish();
        if(!WireName(value.First)||!WireName(value.Second))throw new ProtocolException("clan war names");
        return value;
    }
    internal static (string Clan,string Leader) DecodePrompt(ushort message,uint reserved,LoginReader r){
        string clan=r.S(),leader=message==217?r.S():"";r.Finish();
        if(reserved!=0||!WireName(clan)||message==217&&!WireName(leader))throw new ProtocolException("clan war confirmation");
        return (clan,leader);
    }
    public static string Describe(ClanWarEvent e,string own)=>e.Type switch{
        1=>$"{e.First} 血盟向 {e.Second} 血盟宣戰。",
        2=>$"{e.First} 血盟向 {e.Second} 血盟投降了。",
        3=>$"{e.First} 血盟與 {e.Second} 血盟之間的戰爭結束了。",
        4=>$"{e.First} 血盟贏了對 {e.Second} 血盟的戰爭。",
        6=>$"{e.First} 血盟與 {e.Second} 血盟成為同盟關係。",
        7=>$"毀掉 {e.First} 血盟與 {e.Second} 血盟之間的同盟。",
        8=>$"目前你的血盟與 {(Same(e.Second,own)?e.First:e.Second)} 血盟交戰當中。",
        _=>$"原服血盟戰爭訊息 {e.Type}：{e.First}、{e.Second}"};
    // ---- T316（#746，FU6§3）----
    /// <summary>S84 各 type 的字串表 id（sub_94F190）：1→226、8→235、2→228、3→227、4→231、6→224、7→225；其他沒有字（-1）。</summary>
    public static int EventText(byte type)=>type switch{1=>226,8=>235,2=>228,3=>227,4=>231,6=>224,7=>225,_=>-1};
    /// <summary>S84 一律印在聊天欄，字色 palette[24]（sub_7DC850(文字, palette[24], palette[14], …)，與 S250 27／30／31 同一條路）。</summary>
    public const int ChatColorIndex=24;
    /// <summary>
    /// S84 的聊天行：有 string-c.tbl 時照字串表（`%0`＝血盟 1、`%1`＝血盟 2）；沒有字串表時用 Describe 的同義文字。
    /// type 8 的 `%0` 代入「對方血盟」（3.81 C_LoginToServer 送 S_War(8, 自己, 對方)；原版代入哪一個沒逐指令查，推測）。
    /// </summary>
    public static string? ChatText(ClanWarEvent e,string own){
        int id=EventText(e.Type);if(id<0)return null;
        string[] args=e.Type==8?[Same(e.Second,own)?e.First:e.Second]:[e.First,e.Second];
        if(ServerMessageText.Table is {} t)return id<t.Count?ServerMessageText.DisplayText(PacketBoxMessageRules.Format(t[id],args)):null;
        return Describe(e,own);
    }
    // 本機檢查的訊息（sub_F46290 等）：478 非王族、785 不再是君主、234 已在戰爭中、232 等級不足（聊天／血盟窗）、475 等級不足（HTML 攻城）、272 沒有血盟。
    public const ushort NotPrinceMessage=478,NotLordMessage=785,AlreadyAtWarMessage=234,LowLevelMessage=232,SiegeLevelMessage=475,NoClanMessage=272;
    /// <summary>血盟窗 ChangeWarState 的輸入框字串：577 宣戰、578 投降、579 停戰。</summary>
    public const int DeclareInputText=577,SurrenderInputText=578,CeaseInputText=579;
    /// <summary>宣戰的等級門檻：血盟窗（sub_B34930 → sub_F46290(名稱, 15)）15；HTML declarewar（sub_582280）25。
    /// 聊天指令 /war 傳給 sub_F46290 的門檻本輪沒查（FU6§3.2），先比照血盟窗用 15（推測）。</summary>
    public const int WindowLevel=15,SiegeLevel=25,ChatLevel=WindowLevel;
    /// <summary>
    /// 血盟窗 ChangeWarState 控制項的值（vtable +324）：1＝宣戰、2＝停戰、3＝投降（與封包類型的 2／3 相反，FU6§3.2）。
    /// </summary>
    public static ClanWarCommand? WindowState(int value)=>value switch{1=>ClanWarCommand.Declare,2=>ClanWarCommand.Cease,3=>ClanWarCommand.Surrender,_=>null};
    public static int InputText(ClanWarCommand command)=>command switch{ClanWarCommand.Declare=>DeclareInputText,ClanWarCommand.Surrender=>SurrenderInputText,_=>CeaseInputText};
    /// <summary>本機檢查用的自己狀態。Class＝選角清單的職業碼（byte_1542AA1，FU6§2）；null＝不知道（不做職業檢查，交給伺服器）。</summary>
    public sealed record Self(byte? Class,byte Rank,ushort Gfx,byte Level,string Clan,string[] Enemies);
    /// <summary>檢查結果：Send＝要送；Message＝要印到聊天欄的字串 id（null＝不印）。</summary>
    public readonly record struct Decision(bool Send,ushort? Message);
    static bool Prince(Self s)=>s.Class is null or 0;
    static bool Lord(byte rank)=>rank is 0 or 4 or 10;
    /// <summary>
    /// sub_F46290（宣戰）：非王族→478；階級不是 0／4／10→785；戰爭中且對象已在戰爭清單→234（比對函式 sub_A0D4D0 的語意沒追，推測同名）；
    /// 外型 &gt; 1（變身中）→不送也不提示；等級 &lt; 門檻→232（HTML 是 475）；沒有血盟→272；名稱空白→不送。
    /// </summary>
    public static Decision CheckDeclare(Self s,string name,int level,ushort lowLevel=LowLevelMessage){
        if(!Prince(s))return new(false,NotPrinceMessage);
        if(!Lord(s.Rank))return new(false,NotLordMessage);
        if(s.Enemies.Length>0&&s.Enemies.Any(e=>Same(e,name.Trim())))return new(false,AlreadyAtWarMessage);
        if(s.Gfx>1)return new(false,null);
        if(s.Level<level)return new(false,lowLevel);
        if(s.Clan.Length==0)return new(false,NoClanMessage);
        return new(name.Trim().Length>0,null);
    }
    /// <summary>血盟窗值 1：開輸入框 577 之前先查 478／785（sub_B34930）。</summary>
    public static ushort? WindowDeclarePrecheck(Self s)=>!Prince(s)?NotPrinceMessage:!Lord(s.Rank)?NotLordMessage:null;
    /// <summary>聊天指令 /surrender、/cease（sub_F49970、sub_F44FB0）：只查職業碼 == 0，不是王族就不送也不提示；名稱空白不送。</summary>
    public static Decision CheckChatSurrenderOrCease(Self s,string name)=>new(Prince(s)&&name.Trim().Length>0,null);
    /// <summary>
    /// HTML 動作 surrender／cease（sub_584E50、sub_581D40）：職業碼 ≠ 0 什麼都不做（null）；階級不是 4／10 印 785（Message）；
    /// 否則開輸入框（578／579）。回傳 (開輸入框, 訊息)。HTML 入口要接在 NpcInteraction.cs（本任務沒有接，見報告）。
    /// </summary>
    public static (bool Input,ushort? Message) HtmlSurrenderOrCease(Self s)=>!Prince(s)?(false,null):s.Rank is not (4 or 10)?(false,NotLordMessage):(true,null);
    /// <summary>本機訊息的聊天文字：有字串表照表；沒有時用 ServerResult 的同義文字，再沒有就是「原服訊息 id」。</summary>
    public static string LocalText(ushort id){
        if(ServerMessageText.Table is {} t)return id<t.Count?ServerMessageText.DisplayText(t[id]):"";
        return id==NotLordMessage?"你不再是君主了。":ServerResult(new ServerMessageReport(id,[],"",false))??$"原服訊息 {id}";
    }

    public static string Question(ClanWarPrompt prompt)=>prompt.Message switch{
        217=>$"{prompt.Clan} 血盟（{prompt.Leader}）向你的血盟宣戰。是否接受？",
        221=>$"{prompt.Clan} 血盟要向你投降。是否接受？",
        _=>$"{prompt.Clan} 血盟要結束戰爭。是否接受？"};
    // These replies stay in the normal S71 path; this only names the ones C_War and C_Attr can produce.
    public static string? ServerResult(ServerMessageReport? reply){
        if(reply==null)return null;string name=reply.Arguments.Length==1?reply.Arguments[0]:"";
        return (reply.Id,reply.Arguments.Length) switch{
            (218,1)=>name+" 血盟君主不在線上。",(232,0)=>"等級 25 以下的君主無法宣戰。",(234,0)=>"你的血盟已經在戰爭中。",
            (236,1)=>name+" 血盟拒絕你的宣戰。",(237,1)=>name+" 血盟拒絕你的提案。",(272,0)=>"若想要戰爭，首先必須創立血盟。",
            (474,0)=>"你已經擁有城堡，無法再擁有其他城堡。",(475,0)=>"若要攻城，君主的等級得 25 以上。",(476,0)=>"尚未到攻城時間。",
            (477,0)=>"包含你，所有的血盟成員到城外，才能宣佈攻城。",(478,0)=>"只有王子和公主才能宣戰。",(812,0)=>"想要參加攻城戰需有盟徽。",_=>null};
    }
}

public sealed partial class WorldSession {
    const string ClanWarIdle="輸入對方血盟名稱後可宣戰；資格與結果由伺服器判定。";
    readonly object clanWarGate=new();
    // Wars outlive map changes and S84/8 is only sent at login, so this is not keyed by WorldEpoch.
    readonly List<(string First,string Second)> clanWarPairs=[];
    ClanWarPrompt? clanWarPrompt;long clanWarPromptRevision;int clanWarRevision;
    string clanWarOwnClan="",clanWarNotice=ClanWarIdle;bool clanWarSubscribed;
    // R2-K (#625): 221/222 are generic S_ASK questions (WIN §2.1) and also sit in the shared input-line slot; this is
    // the slot prompt id (0 = none). 217 is a special case of the original S_ASK switch and stays in the war window.
    int clanWarSlot;
    public static bool ClanWarUsesSharedSlot(ushort message)=>message is 221 or 222;
    void ExpireClanWarPrompt(string notice){
        int slot;
        lock(clanWarGate){if(clanWarPrompt==null)return;clanWarPrompt=null;clanWarNotice=notice;clanWarRevision++;slot=clanWarSlot;clanWarSlot=0;}
        RetireClanYesNo(slot,notice);
    }
    void CheckClanWar(){
        // Read outside clanWarGate; the slot never takes clanWarGate.
        int slot;lock(clanWarGate)slot=clanWarSlot;bool held=ClanYesNoHeld(slot);
        lock(clanWarGate){
            // Answered on the input line (the same C121 H0 D0 H(221/222) C) or replaced by a newer S219.
            if(slot!=0&&slot==clanWarSlot&&!held&&clanWarPrompt!=null){clanWarPrompt=null;clanWarSlot=0;clanWarNotice="已在輸入列回覆或被新的詢問取代；結果以伺服器回報為準。";clanWarRevision++;}
            if(!clanWarSubscribed){clanWarSubscribed=true;CombatSoundStateChanged+=CheckClanWar;}
            // Reset nulls Status; S8 always precedes S84/8 in C_LoginToServer, so no login state is lost here.
            if(!Entered||Status==null){
                if(clanWarPairs.Count>0||clanWarPrompt!=null||clanWarOwnClan.Length>0){clanWarPairs.Clear();clanWarPrompt=null;clanWarOwnClan="";clanWarNotice=ClanWarIdle;clanWarRevision++;}
                RetireClanYesNo(clanWarSlot,"世界已更新，舊詢問已失效。");clanWarSlot=0;
                return;
            }
            if(Actor is {} actor&&!ClanWarProtocol.Same(actor.Clan,clanWarOwnClan)){
                clanWarPairs.Clear();clanWarOwnClan=actor.Clan;clanWarRevision++;
                ExpireClanWarPrompt("血盟已變更，舊戰爭確認不可再回答。");
            }
            // Party and Duel consume their S219 before Trade; a live invitation here is newer than this prompt.
            if(clanWarPrompt is {} prompt&&(prompt.Epoch!=WorldEpoch||prompt.Interaction!=InteractionRevision||OwnDead||partyInvitation!=null||DuelSnapshot.Phase=="invited"))
                ExpireClanWarPrompt("戰爭確認已失效，請對方重新提出；沒有自動回覆。");
        }
    }
    internal ClanWarPrompt? PendingClanWarPrompt {get{CheckClanWar();lock(clanWarGate)return clanWarPrompt;}}
    internal string ClanWarNotice {get{CheckClanWar();lock(clanWarGate)return clanWarNotice;}}
    internal int ClanWarRevision {get{CheckClanWar();lock(clanWarGate)return clanWarRevision;}}
    internal void ClanWarNote(string notice){lock(clanWarGate){clanWarNotice=notice;clanWarRevision++;}}
    internal void ClanWarAnswered(string notice){int slot;lock(clanWarGate){clanWarPrompt=null;clanWarNotice=notice;clanWarRevision++;slot=clanWarSlot;clanWarSlot=0;}RetireClanYesNo(slot,notice);}
    // Uses the retained clan: Actor is briefly null across a map change, while the war continues.
    string[] CurrentClanWarEnemies(){
        lock(clanWarGate)return clanWarPairs.Select(p=>ClanWarProtocol.Same(p.First,clanWarOwnClan)?p.Second:p.First).ToArray();
    }
    // War mode = at least one current war. Object +197 (sub_A45920): 2 enemy clan, 1 own clan, 0 otherwise.
    public bool ClanWarMode=>ClanWarEnemies.Length>0;
    public byte ClanWarFlag(string clan){
        if(clan.Length==0)return 0;var enemies=ClanWarEnemies;if(enemies.Length==0)return 0;
        if(enemies.Contains(clan,StringComparer.OrdinalIgnoreCase))return 2;
        lock(clanWarGate)return ClanWarProtocol.Same(clan,clanWarOwnClan)?(byte)1:(byte)0;
    }
    public byte ClanWarFlag(uint id)=>Actor?.ObjectId==id?ClanWarFlag(Actor.Clan):objects.TryGetValue(id,out var o)?ClanWarFlag(o.Clan):(byte)0;
    // sub_A3FE30 / sub_F044C0: pink name, the duel opponent and an enemy clan (+197=2) may be attacked directly.
    public bool CanAttackDirectly(uint id)=>Actor?.ObjectId!=id&&objects.ContainsKey(id)&&(IsPinkName(id)||IsDuelOpponent(id)||ClanWarFlag(id)==2);
    // Every S219 passes here first, so one shared TempID slot never keeps two answerable prompts.
    bool ReceiveClanWarYesNo(ushort message,uint reserved,LoginReader r){
        CheckClanWar();
        if(!ClanWarProtocol.IsPrompt(message)){ExpireClanWarPrompt("收到其他確認訊息，原戰爭確認已失效。");return false;}
        var (clan,leader)=ClanWarProtocol.DecodePrompt(message,reserved,r);
        // Returning true skips Trade's other routes, which would have expired these older prompts.
        ReceiveMarriageYesNo(message,reserved,r);
        if(TradeState=="invited"){TradeState="attention";TradeRevision++;TradeNotice="原服改送其他確認訊息；舊交易邀請不可再回答，可取消交易。";}
        if(clanInvitation!=null)ClearClanInvitation("收到血盟戰爭確認，原入盟申請已失效，請對方重新申請。");
        if(!Ready||OwnDead||Status?.Hp<=0||string.IsNullOrEmpty(Actor?.Clan)){ExpireClanWarPrompt("收到無法回答的戰爭確認。");return true;}
        lock(clanWarGate){
            clanWarPrompt=new(++clanWarPromptRevision,message,clan,leader,WorldEpoch,InteractionRevision);
            clanWarNotice=ClanWarProtocol.Question(clanWarPrompt);clanWarRevision++;clanWarSlot=0;
        }
        if(ClanWarUsesSharedSlot(message)){int slot=OfferClanYesNo(message,[clan]);lock(clanWarGate)clanWarSlot=slot;}
        return true;
    }
    void ReceiveClanWar(byte[] plain){
        var value=ClanWarProtocol.Decode(plain);CheckClanWar();
        if(value.Type is not (1 or 2 or 3 or 4 or 6 or 7 or 8)){Unhandled(84);return;}
        if(Status==null)return;
        lock(clanWarGate){
            bool Pair((string First,string Second) p)=>ClanWarProtocol.Same(p.First,value.First)&&ClanWarProtocol.Same(p.Second,value.Second)||ClanWarProtocol.Same(p.First,value.Second)&&ClanWarProtocol.Same(p.Second,value.First);
            // Castle wars end on 2 or 4 without a following 3 in some paths; repeated ends are harmless.
            if(value.Type is 2 or 3 or 4)clanWarPairs.RemoveAll(Pair);
            // S84(1,clan,"NPC") opens an ownerless castle and has no matching end message.
            // Castle S84 is broadcast to everyone, so only this clan's wars are kept; S87 precedes S84/8.
            else if(value.Type is 1 or 8&&!ClanWarProtocol.Same(value.First,value.Second)&&value.First!="NPC"&&value.Second!="NPC"&&clanWarOwnClan.Length>0
                &&(ClanWarProtocol.Same(value.First,clanWarOwnClan)||ClanWarProtocol.Same(value.Second,clanWarOwnClan))&&!clanWarPairs.Any(Pair)&&clanWarPairs.Count<256)
                clanWarPairs.Add((value.First,value.Second));
            clanWarNotice=ClanWarProtocol.Describe(value,clanWarOwnClan);clanWarRevision++;
        }
        // T316（FU6§3.1）：原版沒有盟戰窗也沒有畫面中央的字，文字一律印在聊天欄（palette[24]）。
        string own;lock(clanWarGate)own=clanWarOwnClan;
        if(ClanWarProtocol.ChatText(value,own) is {Length:>0} text)AddClanWarChatLine(text,ClanWarProtocol.ChatColorIndex);
    }
    void AddClanWarChatLine(string text,int? color){
        playerChatLines.Enqueue(new(++playerChatSequence,0,PlayerChatProtocol.Display(text)){ColorIndex=color});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
    /// <summary>T316：本機檢查擋下宣戰／投降／停戰時，把原版會印的字（478、785、234、232、475、272）放進聊天欄（預設字色）。</summary>
    internal void ClanWarLocalMessage(ushort id){if(ClanWarProtocol.LocalText(id) is {Length:>0} text)AddClanWarChatLine(text,null);}
    /// <summary>本機檢查用的自己狀態；ownClass＝選角清單的職業碼（LoginConnection.SelectedCharacterClass）。</summary>
    internal ClanWarProtocol.Self ClanWarSelf(byte? ownClass)=>new(ownClass,Actor?.Rank??0,Actor?.Gfx??0,Status?.Level??0,Actor?.Clan??"",ClanWarEnemies);
}

public sealed partial class LoginConnection {
    int clanWarSendingGeneration=-1;
    public ClanWarView ClanWarSnapshot()=>CaptureWorld(()=>{
        var prompt=World.PendingClanWarPrompt;string clan=World.Actor?.Clan??"";bool access=ClanAccess&&clanWarSendingGeneration!=generation;
        return new ClanWarView(generation,World.WorldEpoch,World.Actor?.ObjectId??0,World.ClanWarRevision,access&&clan.Length>0&&prompt==null,access&&prompt!=null,clan,World.ClanWarEnemies,World.ClanWarNotice,prompt);
    });
    async Task<bool> SendClanWarPacket(byte[] packet,Func<bool> current,Action admit,string failure){
        int session=generation;var sender=sendMovementGate!;clanWarSendingGeneration=session;
        try{bool sent=await sender(packet,current,admit);if(session!=generation)return false;Changed?.Invoke();return sent;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,failure);}return false;}
        finally{if(clanWarSendingGeneration==session)clanWarSendingGeneration=-1;}
    }
    public async Task<bool> SendClanWar(ClanWarCommand command,ClanWarView view,string name){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&view.Clan==World.Actor?.Clan&&World.PendingClanWarPrompt==null;
        if(!Current()||clanWarSendingGeneration==generation||sendMovementGate==null)return false;
        byte[] packet;
        try{
            // The server drops both cases silently, so the player would otherwise get no result.
            if(view.Clan.Length==0)throw new ArgumentException("你沒有血盟，不能操作血盟戰爭。");
            if(ClanWarProtocol.Same(view.Clan,name))throw new ArgumentException("對象不可是自己的血盟。");
            packet=ClanWarProtocol.Encode(command,name);
        }catch(ArgumentException ex){World.ClanWarNote(ex.Message);return false;}
        string notice=(command switch{ClanWarCommand.Declare=>"宣戰",ClanWarCommand.Surrender=>"投降",_=>"停戰"})+"請求已送出；結果以伺服器回報為準。";
        return await SendClanWarPacket(packet,Current,()=>World.ClanWarNote(notice),"血盟戰爭請求傳送失敗，已斷線；沒有自動重送。");
    }
    /// <summary>
    /// T316（FU6§3.2）：原版入口共用的本機檢查與編碼。source＝"chat"（/war、/surrender、/cease）、"window"（血盟窗 ChangeWarState 的輸入框）。
    /// 擋下時把原版的訊息印到聊天欄並回 null；名稱格式錯誤（空白、控制字元、過長）也回 null，notice 寫原因。
    /// </summary>
    internal byte[]? ClanWarRequestPacket(ClanWarCommand command,string name,string source,out string notice){
        notice="";name=name.Trim();
        var self=World.ClanWarSelf(SelectedCharacterClass);
        ClanWarProtocol.Decision d=command==ClanWarCommand.Declare
            ?ClanWarProtocol.CheckDeclare(self,name,source=="chat"?ClanWarProtocol.ChatLevel:ClanWarProtocol.WindowLevel)
            :source=="chat"?ClanWarProtocol.CheckChatSurrenderOrCease(self,name):new(name.Length>0,null);   // 血盟窗的投降／停戰沒有本機檢查
        if(d.Message is ushort id)World.ClanWarLocalMessage(id);
        if(!d.Send){notice=d.Message is ushort m?ClanWarProtocol.LocalText(m):name.Length==0?"請輸入對方血盟名稱。":"目前不能"+Label(command)+"。";Changed?.Invoke();return null;}
        try{return ClanWarProtocol.Encode(command,name);}catch(ArgumentException ex){notice=ex.Message;return null;}
    }
    static string Label(ClanWarCommand c)=>c switch{ClanWarCommand.Declare=>"宣戰",ClanWarCommand.Surrender=>"投降",_=>"停戰"};
    /// <summary>血盟窗 ChangeWarState 值 1：開輸入框 577 前的 478／785 檢查；擋下時印字並回 false。</summary>
    public bool ClanWarWindowPrecheck(){
        if(ClanWarProtocol.WindowDeclarePrecheck(World.ClanWarSelf(SelectedCharacterClass)) is ushort id){World.ClanWarLocalMessage(id);Changed?.Invoke();return false;}
        return true;
    }
    /// <summary>血盟窗輸入框確定後送 C227（type 0／2／3）。</summary>
    public async Task<bool> SendClanWarFromWindow(ClanWarCommand command,string name){
        if(!ClanAccess||clanWarSendingGeneration==generation||sendMovementGate==null)return false;
        if(ClanWarRequestPacket(command,name,"window",out string notice) is not {} packet){if(notice.Length>0)World.ClanWarNote(notice);return false;}
        int session=generation,epoch=World.WorldEpoch;uint? actor=World.Actor?.ObjectId;
        bool Current()=>ClanAccess&&session==generation&&epoch==World.WorldEpoch&&actor==World.Actor?.ObjectId;
        return await SendClanWarPacket(packet,Current,()=>World.ClanWarNote(Label(command)+"請求已送出；結果以伺服器回報為準。"),"血盟戰爭請求傳送失敗，已斷線；沒有自動重送。");
    }
    public async Task<bool> AnswerClanWar(ClanWarView view,bool accept){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&view.Prompt!=null&&view.Prompt==World.PendingClanWarPrompt;
        if(!Current()||clanWarSendingGeneration==generation||sendMovementGate==null)return false;
        return await SendClanWarPacket(ClanWarProtocol.Answer(view.Prompt!.Message,accept),Current,
            ()=>World.ClanWarAnswered(accept?"已接受；等待伺服器回報戰爭狀態。":"已拒絕。"),"戰爭確認傳送失敗，已斷線；沒有自動重送。");
    }
}

public sealed partial class ClientForm {
    bool clanWarUiHooked;
    // OnLoad, OnShown, OnVisibleChanged, OnCreateControl and OnHandleCreated belong to other modules.
    protected override void OnLayout(LayoutEventArgs e){
        base.OnLayout(e);
        if(!clanWarUiHooked){clanWarUiHooked=true;Shown+=(_,_)=>InitClanWarUi();}
    }
    // T316（FU6§3.2）：原版沒有三鈕的獨立盟戰窗；入口是 /war、/surrender、/cease 與血盟窗的 ChangeWarState。這個窗是引擎自訂的備援
    // （主要用來回答 217 宣戰詢問），不是原版畫面。
    void InitClanWarUi(){
        var button=new Button{Text="血盟戰爭",Dock=DockStyle.Top,Height=36,AccessibleName="開啟血盟戰爭"};
        if(normalPanels!=null){var page=new TabPage("血盟戰爭");page.Controls.Add(button);normalPanels.TabPages.Add(page);}
        else{button.Dock=DockStyle.Right;button.Width=88;bar.Controls.Add(button);button.BringToFront();}
        void Open(){
            if(login.State!=LoginState.InWorld)return;
            var existing=OwnedForms.OfType<ClanWarUi>().FirstOrDefault();if(existing!=null){existing.Activate();return;}
            ClearInput("clan-war");LeaveGameFocus("clan-war");new ClanWarUi(login).Show(this);
        }
        button.Click+=(_,_)=>Open();
        var refresh=new System.Windows.Forms.Timer{Interval=250};long shown=-1;
        // The snapshot also keeps a 221/222 prompt and the shared slot in step. Only 217 (S_ASK special case) opens this window.
        refresh.Tick+=(_,_)=>{var view=login.ClanWarSnapshot();button.Text=view.Enemies.Length>0?$"血盟戰爭（交戰 {view.Enemies.Length}）":"血盟戰爭";if(view.Prompt is {} prompt&&prompt.Revision!=shown&&!WorldSession.ClanWarUsesSharedSlot(prompt.Message)){shown=prompt.Revision;Open();}};
        FormClosed+=(_,_)=>refresh.Dispose();refresh.Start();
    }
    // Enemy clans have no extra label and keep their name colour (actions-and-names.md 2.7); see ClanWarFlag.
}

public sealed class ClanWarUi:Form {
    readonly LoginConnection connection;
    readonly Label notice=new(){Dock=DockStyle.Fill,Padding=new(12)};
    readonly TextBox target=new(){Width=240,MaxLength=32};
    readonly Button declare=new(){Text="宣戰",AutoSize=true},surrender=new(){Text="投降",AutoSize=true},cease=new(){Text="停戰",AutoSize=true},yes=new(){Text="接受",AutoSize=true},no=new(){Text="拒絕",AutoSize=true};
    readonly System.Windows.Forms.Timer timer=new(){Interval=200};
    ClanWarView displayed;bool busy,prefilled;
    public ClanWarUi(LoginConnection connection){
        this.connection=connection;displayed=connection.ClanWarSnapshot();
        Text="血盟戰爭（引擎自訂）";Size=new(520,300);StartPosition=FormStartPosition.CenterParent;
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=84};
        actions.Controls.AddRange([new Label{Text="對方血盟",AutoSize=true},target,declare,surrender,cease,yes,no]);Controls.Add(notice);Controls.Add(actions);
        declare.Click+=async(_,_)=>await Request(ClanWarCommand.Declare,"宣戰");surrender.Click+=async(_,_)=>await Request(ClanWarCommand.Surrender,"投降");cease.Click+=async(_,_)=>await Request(ClanWarCommand.Cease,"提議停戰");
        yes.Click+=async(_,_)=>await Run(v=>connection.AnswerClanWar(v,true));no.Click+=async(_,_)=>await Run(v=>connection.AnswerClanWar(v,false));
        timer.Tick+=(_,_)=>Sync();FormClosed+=(_,_)=>timer.Dispose();Sync();timer.Start();
    }
    void Sync(){
        var v=connection.ClanWarSnapshot();if(v.Session!=displayed.Session||connection.State!=LoginState.InWorld){Close();return;}
        displayed=v;if(!prefilled&&v.Enemies.Length>0){prefilled=true;if(target.TextLength==0)target.Text=v.Enemies[0];}
        string result=ClanWarProtocol.ServerResult(connection.World.LastServerReply) is {} text?"\n伺服器："+text:"";
        notice.Text=(v.Clan.Length>0?"血盟："+v.Clan:"目前沒有血盟")+"\n交戰中："+(v.Enemies.Length>0?string.Join("、",v.Enemies):"（未收到戰爭狀態）")+"\n"+v.Notice+result;
        // R2-K: 221/222 are answered on the chat input line (shared slot); the window buttons only answer 217.
        bool own=v.Prompt is {} p&&!WorldSession.ClanWarUsesSharedSlot(p.Message);
        yes.Visible=no.Visible=v.Prompt==null||own;
        declare.Enabled=surrender.Enabled=cease.Enabled=!busy&&v.CanRequest;yes.Enabled=no.Enabled=!busy&&v.CanAnswer&&own;
    }
    async Task Request(ClanWarCommand command,string label){
        string name=target.Text;
        if(MessageBox.Show(this,$"確定向「{name}」{label}？","血盟戰爭",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        await Run(v=>connection.SendClanWar(command,v,name));
    }
    async Task Run(Func<ClanWarView,Task<bool>> send){
        var stamp=displayed;if(busy)return;busy=true;Sync();await send(stamp);busy=false;if(!IsDisposed)Sync();
    }
}
