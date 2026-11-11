namespace P06;

// Append-only: Whisper = received (S73), WhisperTo = sender echo (S243/9). Types 12–17 are receive-only (T175).
public enum PlayerChatChannel { Normal, Shout, Global, Whisper, Clan, Party, WhisperTo, Trade, Prince, ChatParty, Alliance, ClanNotice }
public sealed record PlayerChatLine(long Sequence,uint Speaker,string Text,PlayerChatChannel Channel=PlayerChatChannel.Normal) {
    public SharedLocation? Location {get;init;}
    /// <summary>S243 type 18（黃金號角）／25（彩色）字串後的 H 色號（調色盤索引 1–95）；其他類型為 null（R2-G）。</summary>
    public int? ColorIndex {get;init;}
    /// <summary>
    /// R3-G（#675）：op 61 S_SAY_CODE（`sub_94AF00`；3.81 S161 NPCSHOUT，NPC／角色代碼聊天）收到的行。
    /// 顯示仍走同類型的 Printer（UI§3），但 `*` 黃字／紅字是 S_SAY（`0x94BA36`）與 S_MESSAGE（`0x94525D`）處理函式裡的判斷，
    /// 不在 Printer 裡，所以代碼聊天不套用（推測，S_SAY_CODE 本身未逐指令讀完）。
    /// </summary>
    public bool Code {get;init;}
}
public sealed record PlayerChatView(int Generation,int WorldEpoch,uint Actor,int Revision,bool CanSend,string Notice,PlayerChatLine[] Lines);
public sealed record PlayerChatInput(PlayerChatChannel Channel,string Text,string Receiver="");

// Source contract and hashes: docs/protocol/player-chat.md. Server remains authoritative.
public static class PlayerChatProtocol {
    public const int MaxCharacters=52;
    // Local input budget: quote + receiver (52) + separator + body (52), or /say + body.
    public const int MaxInputCharacters=106;
    public const string InputHelp="一般：直接輸入；!喊話 &全體 @血盟 #隊伍 $交易；\"對象 訊息：密語；/say 原文：一般；/loc 角色：發送座標；/whisper、/tradechat on 或 off：頻道開關；/accepttrade：交易詢問開關；/who（/誰）、/rank（/階級）、/checkpk：查詢；/chatparty、/chatoutparty、/chatbanish 名稱：聊天隊伍；/focus [血盟]：關注；/war、/surrender、/cease 血盟：盟戰；/propose、/divorce：求婚、離婚；/invite、/banish 名稱、/outparty、/chatinvite 名稱：組隊；/buddy、/addbuddy、/delbuddy 名稱：好友；/exclude、/include 名稱：封鎖；/trade：交易；/duel（/決鬥）：決鬥；/shop：個人商店；/bookmark [名稱]：記憶座標";
    public static PlayerChatInput ParseInput(string input){
        if(string.IsNullOrWhiteSpace(input))throw new ArgumentException("請輸入 1–52 字的單行訊息。");
        if(input.StartsWith("/say ",StringComparison.Ordinal))return new(PlayerChatChannel.Normal,input[5..]);
        if(input[0]=='\"'){
            int split=input.IndexOf(' ',1);
            if(split<=1)throw new ArgumentException("密語格式：\"對象 訊息");
            return new(PlayerChatChannel.Whisper,input[(split+1)..],input[1..split]);
        }
        // `$`→交易（C40 type 12）：前綴字元集合已驗證（sub_7DE200），字元→頻道的對照是推測（ui-layout.md §3）；
        // 伺服器 C_ChatGlobal 的 case 0x0c 註明「交易頻道」（R2-G）。
        var channel=input[0] switch{'!'=>PlayerChatChannel.Shout,'&'=>PlayerChatChannel.Global,'@'=>PlayerChatChannel.Clan,'#'=>PlayerChatChannel.Party,'$'=>PlayerChatChannel.Trade,_=>PlayerChatChannel.Normal};
        return new(channel,channel==PlayerChatChannel.Normal?input:input[1..]);
    }
    /// <summary>
    /// 聊天列輸入的檢查與說明（UI 用）：本機指令（/on、/off、C199 聊天隊伍指令、/loc）回傳指令說明；
    /// 一般訊息回傳頻道與正文長度。無效時丟 ArgumentException。不送出任何東西。
    /// </summary>
    public static string Describe(string input){
        if(ChatChannelCommands.IsCommand(input)){var c=ChatChannelCommands.Parse(input)!;return "指令 · "+ChatChannelCommands.Describe(c);}
        if(TradeAskCommand.Is(input))return "指令 · 交易詢問開關";
        if(ChatPartyCommands.IsCommand(input)){ChatPartyCommands.Encode(input);return "指令 · 聊天隊伍";}
        if(input=="/loc"||input.StartsWith("/loc ",StringComparison.Ordinal))return "指令 · 發送座標";
        if(ClientChatCommands.Parse(input) is {} client)return "指令 · "+ClientChatCommands.Label(client.Kind);
        if(DuelChatCommand.Is(input))return "指令 · 決鬥"; // R8-C（#822）
        if(ChatFollowupCommands.Parse(input) is {} followup)return "指令 · "+ChatFollowupCommands.Label(followup.Kind); // R9-C（#847）
        var message=ParseInput(input);Encode(message);
        return $"{ChannelName(message.Channel)} · 正文 {message.Text.Length}/52 字";
    }
    public static bool Valid(string input){try{Describe(input);return true;}catch(ArgumentException){return false;}}
    public static byte[] Encode(string input)=>Encode(ParseInput(input));
    public static byte[] Encode(PlayerChatInput message){
        string text=message.Text;
        if(string.IsNullOrWhiteSpace(text)||text.Length>MaxCharacters||text.Any(char.IsControl))
            throw new ArgumentException("請輸入 1–52 字的單行訊息。");
        try{
            byte[] body=LoginPackets.TextEncoding.GetBytes(ChatFormat.EncodeColorCodes(text));
            if(message.Channel==PlayerChatChannel.Whisper){
                string receiver=message.Receiver;
                if(string.IsNullOrWhiteSpace(receiver)||receiver.Length>MaxCharacters||receiver.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
                    throw new ArgumentException("請輸入密語對象名稱（不可含空白，最多 52 字）。");
                return LoginPackets.Pad([184,..LoginPackets.TextEncoding.GetBytes(receiver),0,..body,0]);
            }
            byte type=message.Channel switch{PlayerChatChannel.Normal=>0,PlayerChatChannel.Shout=>2,PlayerChatChannel.Global=>3,PlayerChatChannel.Clan=>4,PlayerChatChannel.Party=>11,PlayerChatChannel.Trade=>12,_=>throw new ArgumentException("未知聊天頻道。")};
            // 3.81：全體（3）與交易（12）走 C40（C_ChatGlobal），其他走 C136（C_Chat）。
            return LoginPackets.Pad([(byte)(message.Channel is PlayerChatChannel.Global or PlayerChatChannel.Trade?40:136),type,..body,0]);
        }
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("訊息含有遊戲字集無法表示的字元。");}
    }
    public static string ChannelName(PlayerChatChannel channel)=>channel switch{
        PlayerChatChannel.Shout=>"喊話",PlayerChatChannel.Global=>"全體",PlayerChatChannel.Whisper or PlayerChatChannel.WhisperTo=>"密語",
        PlayerChatChannel.Clan=>"血盟",PlayerChatChannel.Party=>"隊伍",PlayerChatChannel.Trade=>"交易",PlayerChatChannel.Prince=>"盟主",
        PlayerChatChannel.ChatParty=>"聊天隊伍",PlayerChatChannel.Alliance=>"同盟",PlayerChatChannel.ClanNotice=>"血盟公告",_=>"一般"};
    // Original printers (docs/reference/ui-layout.md §3) show the server-formatted string as is:
    // `name: msg`, `[name] msg`, `{name} msg`, `{{name}} msg`, `(name) msg`, `-> (target) msg`. No local channel tag.
    // T290（#698）：座標分享行有 string-c.tbl 時整行照 1784「%s從 %s地區發送了座標位置情報。」（寄件者、地名，sub_8D43B0）；
    // 沒有字串表時保留引擎標示的「[座標] 寄件者：」格式（LocationShareProtocol.ChatLine）。
    public static string FormatLine(PlayerChatLine line)=>line.Location is {} location
        ?LocationShareProtocol.ChatLine(line.Text,location)
        :line.Text;
    // S243 chat types beyond whisper replies. 13/15/17 are not emitted by the d287 source; accepted for display only.
    static PlayerChatChannel? GlobalChannel(byte type)=>type switch{
        3=>PlayerChatChannel.Global,4=>PlayerChatChannel.Clan,9=>PlayerChatChannel.WhisperTo,11=>PlayerChatChannel.Party,
        12=>PlayerChatChannel.Trade,13=>PlayerChatChannel.Prince,15=>PlayerChatChannel.Alliance,17=>PlayerChatChannel.ClanNotice,
        // 18 黃金號角／25 彩色：`[name] msg`，歸入全體頁籤（推測），顏色由封包自帶（R2-G）。
        18 or 25=>PlayerChatChannel.Global,_=>null};
    public static bool TryDecode(byte[] plain,out PlayerChatLine? line){
        line=null;
        if(plain.Length==0)return false;
        var r=new LoginReader(plain);byte opcode=r.C();uint speaker=0;string text;PlayerChatChannel channel;int? color=null;bool code=false;
        switch(opcode){
        case 161:
            // op 61 S_SAY_CODE（R3-G #675）：3.81 `C(161) C(type) D(id) S(text)`（docs/reference/server-protocol.md）：
            // type 0 S_NpcChat `name: msg` → 一般 Printer；type 2 S_NpcChatShouting `<name> msg` → 喊話 Printer 顯示 `[name] msg`；
            // type 3 S_NpcChatGlobal `[name] msg` → 全體 Printer。字串由伺服器組好，這裡不加前綴。
            // id 0 是 S_ServerMessage／S_BoxMessage／S_SystemMessage 等系統文字：不當聊天，留給 Inventory（伺服器訊息）。
            byte codeType=r.C();speaker=r.D();
            if(speaker==0)return false;
            PlayerChatChannel? codeChannel=codeType switch{0=>PlayerChatChannel.Normal,2=>PlayerChatChannel.Shout,3=>PlayerChatChannel.Global,_=>null};
            if(codeChannel is not {} cc)return false;
            // T316（FU6§6）：`$數字` 用 desc-c.tbl 展開（sub_94AF00 → sub_A11520）。3.81 的 S161 只有 NPC 會送，所以走「一律展開」那條；
            // 職業外型說話者「只展開開頭的 $」的分支 3.81 用不到（ExpandDesc 的 leadingOnly）。
            channel=cc;text=CodeText(ExpandDesc(r.S()));code=true;break;
        case 81:
            byte localType=r.C();
            if(localType is not (0 or 2 or 14))return false;
            channel=localType switch{2=>PlayerChatChannel.Shout,14=>PlayerChatChannel.ChatParty,_=>PlayerChatChannel.Normal};
            speaker=r.D();text=r.S();
            if(localType==2){r.H();r.H();} // Shout carries x/y after its string, including invisible speakers.
            break;
        case 73:
            // ChatTextRecvWhisperPrinter: `(name) msg`.
            channel=PlayerChatChannel.Whisper;text=$"({r.S()}) {r.S()}";break;
        case 243:
            byte globalType=r.C();
            if(GlobalChannel(globalType) is not {} global)return false;
            text=r.S();
            if(globalType is 18 or 25){
                // S_MESSAGE（sub_944F50）格式 `cs` 之後，case 18／25 再讀一個 H（sub_926CA0）當調色盤索引。
                // Printer（sub_6BD720／sub_6BD680）只在 0<色號<0x60 時輸出；超出範圍不顯示，所以這裡不收。
                int index=r.H();
                if(index is <=0 or >=ChatFormat.PaletteSize)return false;
                color=index;
            }
            // Type 9 is shared with S_SystemMessage. Leave non-whisper replies to Inventory.
            if(global==PlayerChatChannel.WhisperTo&&!(text.StartsWith("-> (",StringComparison.Ordinal)&&text.IndexOf(") ",4,StringComparison.Ordinal)>4))return false;
            channel=global;break;
        default:return false;
        }
        r.Finish();line=new(0,speaker,Display(text),channel){ColorIndex=color,Code=code};return true;
    }
    internal static string Display(string text)=>new(text.Where(c=>!char.IsControl(c)).Take(1024).ToArray());
    /// <summary>
    /// R9-C（#847，FU9§4.1）：S161 展開 `$` 之後的後處理（sub_94AF00）。
    /// 1. 第一個 `^` 換成空白（sub_941E40，`strchr`）、第一個 `!` 換成空白（sub_941DE0(33,…)）；各只換第一個。
    ///    原版用位元組 `strchr`，Big5 第二位元組是 0x5E 時會換錯位元組（原版缺陷，推測）；這裡對解碼後的字元操作，等於 DBCS 安全的換法。
    /// 2. 開頭是 `\f` 而且第 3 字元 ≥ '0' 時拿掉這 3 個字（從第 4 字元開始顯示）；顏色仍由 type 決定。中間的 `\f` 不動，交給 Printer。
    /// 一般說話者與 id 0 系統文字（Inventory.cs）都套用。786「伊娃之祝福」分支 3.81 不會出現（原始碼沒有這個字串），不做。
    /// </summary>
    public static string CodeText(string text){
        if(string.IsNullOrEmpty(text))return text;
        var chars=text.ToCharArray();
        int caret=Array.IndexOf(chars,'^');if(caret>=0)chars[caret]=' ';
        int bang=Array.IndexOf(chars,'!');if(bang>=0)chars[bang]=' ';
        string result=new(chars);
        return result.Length>=3&&result[0]=='\\'&&result[1]=='f'&&result[2]>='0'?result[3..]:result;
    }

    // ---- T316（#746，FU6§6）：S161 的 `$數字` ----
    static readonly object descGate=new();
    static ServerStringTable? desc,descOverride;static bool descLoaded,descOverridden;
    /// <summary>desc-c.tbl（dword_153C9C4，sub_8CA220 以 "desc" 載入）：執行期從客戶端 Text.pak 在記憶體解出並核 pin（TextArchive.ReadDesc）；
    /// 座標分享已經載入時直接共用。讀不到（例如 CI 沒有客戶端）是 null，文字照原樣顯示。</summary>
    public static ServerStringTable? Desc{get{
        lock(descGate){
            if(descOverridden)return descOverride;
            if(!descLoaded){
                if(LocationShareProtocol.Desc is {} shared){desc=shared;descLoaded=true;return desc;}
                descLoaded=true;
                try{desc=TextArchive.Open(GroundSpriteReader.ClientRoot).ReadDesc();}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or System.Text.DecoderFallbackException){
                    desc=null;System.Diagnostics.Trace.WriteLine("[player-chat] "+TextArchive.DescFileName+" 無法載入，代碼聊天的 $ 不展開："+ex.Message);
                }
            }
            return desc;
        }
    }}
    /// <summary>離線測試用：替換 desc 表（null＝沒有表），直到回傳的 scope 釋放。</summary>
    internal static IDisposable UseDesc(ServerStringTable? replacement){
        lock(descGate){var previous=(descOverridden,descOverride);descOverridden=true;descOverride=replacement;return new DescScope(()=>{lock(descGate)(descOverridden,descOverride)=previous;});}
    }
    sealed class DescScope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}
    /// <summary>沒有 `$` 時不載入表。</summary>
    public static string ExpandDesc(string text)=>text.IndexOf('$')<0?text:ExpandDesc(text,Desc);
    /// <summary>
    /// sub_A11520（FO 0x610920）的展開規則：`$` 後的十進位數字＝desc-c.tbl 行號；超出範圍換成 `???`；數字後緊接 `;` 一併吃掉；
    /// 有展開時結果開頭是 `+0` 就去掉前三個字元。`$` 後面不是數字時照原樣保留（推測）。表內容的 `%i` 要代入額外參數，S161 沒有參數，照原樣保留（推測）。
    /// leadingOnly＝說話者是職業外型的角色時只有文字開頭是 `$` 才展開。沒有表（null）時不改。
    /// </summary>
    public static string ExpandDesc(string text,ServerStringTable? table,bool leadingOnly=false){
        if(table==null||text.IndexOf('$')<0||leadingOnly&&!text.StartsWith('$'))return text;
        var sb=new System.Text.StringBuilder(text.Length+16);bool expanded=false;
        for(int i=0;i<text.Length;i++){
            if(text[i]=='$'&&i+1<text.Length&&char.IsAsciiDigit(text[i+1])){
                int j=i+1;long id=0;
                while(j<text.Length&&char.IsAsciiDigit(text[j])){id=Math.Min(id*10+(text[j]-'0'),int.MaxValue);j++;}
                sb.Append(id<table.Count?table[(int)id]:"???");expanded=true;
                if(j<text.Length&&text[j]==';')j++;
                i=j-1;continue;
            }
            sb.Append(text[i]);
        }
        string result=sb.ToString();
        return expanded&&result.StartsWith("+0",StringComparison.Ordinal)?result[Math.Min(3,result.Length)..]:result;
    }
}

/// <summary>
/// T316（#746，FU6§1、§3、§4）：原版客戶端自己處理的斜線指令（sub_F56320 依序註冊「建構子 → 英文名 → 字串表名」）。
/// 中文別名是字串表的文字：3322「注視」、380「宣戰」、381「投降」、382「停戰」、788「求婚」、789「離婚」（本機 string-c.tbl 讀到的字；
/// 這裡寫死，原版執行期由字串表決定）。指令與參數以第一個空白分開，參數前後空白去掉。
/// </summary>
public enum ClientChatCommandKind { Focus, War, Surrender, Cease, Propose, Divorce }
public sealed record ClientChatCommand(ClientChatCommandKind Kind,string Argument);
public static class ClientChatCommands {
    static readonly (string English,string Chinese,ClientChatCommandKind Kind)[] Table=[
        ("focus","注視",ClientChatCommandKind.Focus),("war","宣戰",ClientChatCommandKind.War),("surrender","投降",ClientChatCommandKind.Surrender),
        ("cease","停戰",ClientChatCommandKind.Cease),("propose","求婚",ClientChatCommandKind.Propose),("divorce","離婚",ClientChatCommandKind.Divorce)];
    /// <summary>不是這些指令回 null。英文指令不分大小寫（原版比對方式沒查，推測）。</summary>
    public static ClientChatCommand? Parse(string input){
        if(string.IsNullOrEmpty(input)||input[0]!='/')return null;
        int space=input.IndexOf(' ');
        string word=space<0?input[1..]:input[1..space],arg=space<0?"":input[(space+1)..].Trim();
        foreach(var (en,zh,kind) in Table)
            if(string.Equals(word,en,StringComparison.OrdinalIgnoreCase)||word==zh)return new(kind,arg);
        return null;
    }
    public static string Label(ClientChatCommandKind kind)=>kind switch{
        ClientChatCommandKind.Focus=>"關注血盟",ClientChatCommandKind.War=>"宣戰",ClientChatCommandKind.Surrender=>"投降",
        ClientChatCommandKind.Cease=>"停戰",ClientChatCommandKind.Propose=>"求婚",_=>"離婚"};
}

/// <summary>
/// R9-C（T363／#847，FU9§9.0、§1.1）：原版 sub_F56320 註冊的另一批斜線指令（英文名＋字串表別名）。
/// 別名是本機 string-c.tbl 的字：410「邀請隊員」、411「驅逐隊員」、412「離開隊伍」、957「邀請聊天」、947「好友名單」、948「新增好友」、
/// 949「刪除好友」、366「斷絕」、470「包含」、399「交換」、907「商店」、365「記憶」；`qbookmark` 沒有字串表名。
/// 英文不分大小寫（推測，同 ClientChatCommands）；指令與參數以第一個空白分開，參數前後空白去掉。
/// </summary>
public enum ChatFollowupKind { Invite, Banish, OutParty, ChatInvite, Buddy, AddBuddy, DelBuddy, Exclude, Include, Trade, Shop, Bookmark, QBookmark }
public sealed record ChatFollowupCommand(ChatFollowupKind Kind,string Argument);
/// <summary>
/// 一個聊天指令要做的事：依序送 Packets（走聊天列的送出閘門；Admitted 在第一包放行時執行，StillValid 是額外的放行條件）；
/// 沒有封包時執行 Local（本機動作，例如開窗、進選目標模式），回傳值就是 SendPlayerChat 的結果；兩者都沒有＝本機擋下，只顯示 Notice。
/// </summary>
public sealed record ChatCommandPlan(string Notice,byte[][] Packets,Action? Admitted=null,Func<bool>? StillValid=null,Func<bool>? Local=null){
    public static ChatCommandPlan Refuse(string notice)=>new(notice,[]);
    public static ChatCommandPlan Send(string notice,Action? admitted,params byte[][] packets)=>new(notice,packets,admitted);
    public static ChatCommandPlan Do(string notice,Func<bool> local)=>new(notice,[],Local:local);
}
public static class ChatFollowupCommands {
    static readonly (string English,string? Chinese,ChatFollowupKind Kind)[] Table=[
        ("invite","邀請隊員",ChatFollowupKind.Invite),("banish","驅逐隊員",ChatFollowupKind.Banish),("outparty","離開隊伍",ChatFollowupKind.OutParty),
        ("chatinvite","邀請聊天",ChatFollowupKind.ChatInvite),("buddy","好友名單",ChatFollowupKind.Buddy),("addbuddy","新增好友",ChatFollowupKind.AddBuddy),
        ("delbuddy","刪除好友",ChatFollowupKind.DelBuddy),("exclude","斷絕",ChatFollowupKind.Exclude),("include","包含",ChatFollowupKind.Include),
        ("trade","交換",ChatFollowupKind.Trade),("shop","商店",ChatFollowupKind.Shop),("bookmark","記憶",ChatFollowupKind.Bookmark),("qbookmark",null,ChatFollowupKind.QBookmark)];
    public static IEnumerable<(string English,string? Chinese,ChatFollowupKind Kind)> Names=>Table;
    /// <summary>不是這些指令回 null。</summary>
    public static ChatFollowupCommand? Parse(string input){
        if(string.IsNullOrEmpty(input)||input[0]!='/')return null;
        int split=1;while(split<input.Length&&!char.IsWhiteSpace(input[split]))split++;
        string word=input[1..split],arg=split<input.Length?input[(split+1)..].Trim():"";
        foreach(var (en,zh,kind) in Table)
            if(string.Equals(word,en,StringComparison.OrdinalIgnoreCase)||zh!=null&&word==zh)return new(kind,arg);
        return null;
    }
    public static string Label(ChatFollowupKind kind)=>kind switch{
        ChatFollowupKind.Invite=>"邀請隊員",ChatFollowupKind.Banish=>"驅逐隊員",ChatFollowupKind.OutParty=>"離開隊伍",ChatFollowupKind.ChatInvite=>"邀請聊天",
        ChatFollowupKind.Buddy=>"好友名單",ChatFollowupKind.AddBuddy=>"新增好友",ChatFollowupKind.DelBuddy=>"刪除好友",ChatFollowupKind.Exclude=>"封鎖",
        ChatFollowupKind.Include=>"解除封鎖",ChatFollowupKind.Trade=>"交易",ChatFollowupKind.Shop=>"個人商店",ChatFollowupKind.Bookmark=>"記憶座標",_=>"記憶座標（快速移動）"};
    /// <summary>
    /// 原版本機印的字串（string-c.tbl 第 id 筆，去掉色碼）；表讀不到或該筆空白時用 fallback（本機 Text.pak string-c.tbl 讀到的原文，
    /// 見 docs/reports/issue-847.md）。
    /// </summary>
    public static string Text(int id,string fallback)=>ServerMessageText.Table is {} t&&id<t.Count&&ServerMessageText.DisplayText(t[id]).Trim() is {Length:>0} s?s:fallback;
    public const int SelectTarget=179,InvalidName=53,ShopTrade=989,ShopClosed=903,ChatInvitePrompt=960,MarkTooLong=204;
    public static string SelectTargetText=>Text(SelectTarget,"請選擇對象。");
    public static string InvalidNameText=>Text(InvalidName,"無效的名字。 若您擅自修改，將無法進行遊戲。");
    public static string ShopTradeText=>Text(ShopTrade,"無法與開設個人商店的玩家進行交易。");
    public static string ShopClosedText=>Text(ShopClosed,"結束個人商店模式。");
    public static string ChatInvitePromptText=>Text(ChatInvitePrompt,"您想要邀請誰至聊天隊伍呢？");
    public static string MarkTooLongText=>Text(MarkTooLong,"所標記地點的名稱太長。");
    /// <summary>原版書籤名稱上限（sub_4FE1D0：&gt; 30 byte 印 204）。</summary>
    public const int MarkNameBytes=30;
}

/// <summary>聊天指令要開的既有視窗（R9-C）。</summary>
public enum ChatWindowRequest { Bookmark, PersonalShop }

public sealed partial class ClientForm {
    /// <summary>
    /// R9-C（#847）：聊天指令 `/bookmark`（不帶名稱）、`/shop` 呼叫既有的開窗入口（ToggleNativeBookmarkWindow、個人商店設定窗）。
    /// 找出擁有這個 session 的視窗；在 UI 執行緒就直接呼叫，否則排進 UI 執行緒。離線測試沒有視窗，回 false。
    /// </summary>
    internal static bool OfferChatWindow(WorldSession world,ChatWindowRequest request){
        bool handled=false;
        foreach(var owner in Application.OpenForms.OfType<ClientForm>().ToArray()){
            if(!ReferenceEquals(owner.login.World,world)||!owner.IsHandleCreated||owner.IsDisposed)continue;
            if(owner.InvokeRequired){owner.BeginInvoke((Action)(()=>{if(!owner.IsDisposed)owner.OpenChatWindow(request);}));handled=true;}
            else handled|=owner.OpenChatWindow(request);
        }
        return handled;
    }
    /// <summary>
    /// R10-J（T394／#909）：`/qbookmark 名稱` 的 C165 放行時呼叫，在擁有這個 session 的視窗的原生書籤窗設 qbookmark 旗標
    /// （sub_502720 的 this+486）。排進 UI 執行緒（面板狀態只在 UI 執行緒改）。離線測試、--classic-ui 或書籤窗停用時什麼都不做，回 false。
    /// </summary>
    internal static bool OfferQuickBookmark(WorldSession world,int session,uint actor){
        bool handled=false;
        foreach(var owner in Application.OpenForms.OfType<ClientForm>().ToArray()){
            if(!ReferenceEquals(owner.login.World,world)||!owner.IsHandleCreated||owner.IsDisposed)continue;
            void Mark(){if(!owner.IsDisposed)owner.NativeBookmarkWindow()?.RequestQuickOnNextBookmark(session,actor);}
            if(owner.InvokeRequired)owner.BeginInvoke((Action)Mark);else Mark();
            handled=true;
        }
        return handled;
    }
    bool OpenChatWindow(ChatWindowRequest request){
        if(request==ChatWindowRequest.Bookmark)return ToggleNativeBookmarkWindow();
        // 原版 sub_F480C0：不在擺攤時「切換」PersonalShopUI 窗。
        if(personalShop is not {} shop)return false;
        if(shop.Panel is {Visible:true} panel){panel.Close();Invalidate();return true;}
        bool opened=shop.Open();Invalidate();return opened;
    }
}

/// <summary>聊天行的一段文字；Color 是原版調色盤 dword_1369D70 的索引（R2-G）。</summary>
public readonly record struct ChatRun(string Text,int Color);

/// <summary>
/// R2-G（#616）：聊天行的顯示格式與顏色（舊 HUD 與原生聊天窗共用）。依據 docs/reference/ui-layout.md §3 與本任務的反組譯
/// （TW1608262200.bin，地址見 docs/reports/issue-616.md）。只回傳調色盤索引，換成 Color 在 PlayerHudChat.cs 的 ChatPalette。
/// line.Text 保持伺服器原字串；這裡只改顯示。
/// </summary>
public static class ChatFormat {
    public const int PaletteSize=96;
    public const int Black=0,Red=3,Grey=7,Yellow=13,White=14;
    /// <summary>各頻道的預設文字色（ui-layout.md §3 表的索引）。</summary>
    public static int ChannelIndex(PlayerChatChannel channel)=>channel switch{
        PlayerChatChannel.Shout=>33,PlayerChatChannel.Global=>44,PlayerChatChannel.Whisper=>32,PlayerChatChannel.WhisperTo=>24,
        PlayerChatChannel.Clan=>11,PlayerChatChannel.Party=>53,PlayerChatChannel.Trade=>34,PlayerChatChannel.Prince=>47,
        PlayerChatChannel.ChatParty=>29,PlayerChatChannel.Alliance=>30,PlayerChatChannel.ClanNotice=>22,_=>White};
    /// <summary>
    /// 一行的文字色（調色盤索引）：
    /// - type 18／25：封包自帶色號（S_MESSAGE case 18 `0x945460`、case 25 `0x945514`）；
    /// - 一般（S81/0）：字串第 0 字是 `*` 且第 1 字不是 `*` → 黃 13（S_SAY `0x94BA36`）；
    /// - 全體（S243/3）：CP950 且字串第 1 字是 `*`（例如 `[******] msg`）→ 紅 3（S_MESSAGE `0x94525D`）；
    /// - 血盟（S243/4）：字串第 1 字是 `(` → 盟主色 47（S_MESSAGE `0x9452DE`）；
    /// - 其他：頻道預設色。
    /// 本引擎固定使用 CP950（台灣版），所以 CP950 條件一律成立。
    /// </summary>
    public static int TextColor(PlayerChatLine line){
        if(line.ColorIndex is int index&&index>0&&index<PaletteSize)return index;
        if(line.Code)return ChannelIndex(line.Channel);
        string t=line.Text;
        switch(line.Channel){
        case PlayerChatChannel.Normal when t.Length>0&&t[0]=='*'&&(t.Length<2||t[1]!='*'):return Yellow;
        case PlayerChatChannel.Global when t.Length>1&&t[1]=='*':return Red;
        case PlayerChatChannel.Clan when t.Length>1&&t[1]=='(':return ChannelIndex(PlayerChatChannel.Prince);
        }
        return ChannelIndex(line.Channel);
    }
    /// <summary>
    /// 第二色（sub_7DC850 的第 3 個參數）：血盟 Printer（sub_6BDAA0）用 7（#BDBDBD），其他都是白 14。
    /// Q3-9 已由 T287／#680 確認：原版把第二色傳下去後沒有讀取，畫面上沒有用途，所以只當資料提供，不畫：舊 HUD 不用；原生聊天窗把它放進 ChatRow.SecondColor（R3-G #675）。
    /// 代碼聊天（op 61）沒有血盟類型，所以一律白 14。
    /// </summary>
    public static int SecondColor(PlayerChatLine line)=>line.Channel==PlayerChatChannel.Clan?Grey:White;
    /// <summary>喊話：3.81 伺服器送 `&lt;name&gt; msg`，原版 ChatTextShoutPrinter 顯示 `[name] msg`。</summary>
    public static string ShoutText(string text){
        if(text.Length<3||text[0]!='<')return text;
        int end=text.IndexOf("> ",1,StringComparison.Ordinal);
        return end<=1?text:"["+text[1..end]+"] "+text[(end+2)..];
    }
    /// <summary>
    /// 把聊天行切成顏色段：`\f`+字元 c（0x30 ≤ c &lt; 0x90）改用調色盤[c−0x30] 並拿掉這 3 個字；`\b`+c（背景／第二色，用途待驗證）拿掉不畫；
    /// `\d0`–`\d7`（喊話方向）拿掉；`\i0`–`\i5` 照原樣保留。c−0x30 的對應是推測（輸入端 `\f0`–`\f7` 轉成數字＋0x22 已驗證，
    /// 所以 `\f0` 顯示為索引 34，與 ui-layout.md §3 的「數字＋34」一致）；超出調色盤範圍的色號維持原色。
    /// </summary>
    public static List<ChatRun> Runs(PlayerChatLine line){
        string text=line.Location!=null?PlayerChatProtocol.FormatLine(line):line.Channel==PlayerChatChannel.Shout?ShoutText(line.Text):line.Text;
        var runs=new List<ChatRun>();var sb=new System.Text.StringBuilder();int color=TextColor(line);
        void Flush(){if(sb.Length>0){runs.Add(new(sb.ToString(),color));sb.Clear();}}
        for(int i=0;i<text.Length;i++){
            if(text[i]=='\\'&&i+2<text.Length){
                char kind=text[i+1],c=text[i+2];
                if(kind is 'f' or 'b'&&c>='0'&&c<(char)0x90){
                    if(kind=='f'&&c-'0'<PaletteSize){Flush();color=c-'0';}
                    i+=2;continue;
                }
                if(kind=='d'&&c is >='0' and <='7'){i+=2;continue;}
            }
            sb.Append(text[i]);
        }
        Flush();
        if(runs.Count==0)runs.Add(new("",color));
        return runs;
    }
    public static string DisplayText(PlayerChatLine line)=>string.Concat(Runs(line).Select(r=>r.Text));
    /// <summary>
    /// 輸入端色碼轉譯（sub_7DE200 `0x7DE516`）：`\f0`–`\f7`、`\b0`–`\b7` 的數字加 0x22 後送出；
    /// 第 3 字在 0x30–0x8F 但不是 0–7 的照原樣；其他字元不動。字數不變，所以 52 字上限照舊。
    /// </summary>
    public static string EncodeColorCodes(string body){
        if(!body.Contains('\\'))return body;
        var chars=body.ToCharArray();
        for(int i=0;i+2<chars.Length;i++){
            if(chars[i]=='\\'&&chars[i+1] is 'f' or 'b'&&chars[i+2] is >='0' and <='7'){chars[i+2]=(char)(chars[i+2]+0x22);i+=2;}
        }
        return new string(chars);
    }
}

/// <summary>
/// R2-G（#616）：C199 聊天隊伍控制（3.81 C_ChatParty，C_OPCODE_CAHTPARTY 199；docs/reference/server-protocol.md）：
/// type 0＋S 名稱＝`/chatbanish`（踢出成員）；type 1＝`/chatoutparty`（離開）；type 2＝`/chatparty`（查詢成員）。
/// 指令字是伺服器原始碼註解的寫法；原版客戶端輸入的指令字是推測。回應由伺服器送（訊息 425／426／427／109、S_Party）。
/// </summary>
public static class ChatPartyCommands {
    public const byte Opcode=199;
    public const byte Banish=0,Leave=1,Query=2;
    public const string BanishCommand="/chatbanish",LeaveCommand="/chatoutparty",QueryCommand="/chatparty";
    public static bool IsCommand(string input)=>input is LeaveCommand or QueryCommand or BanishCommand||input.StartsWith(BanishCommand+" ",StringComparison.Ordinal);
    /// <summary>不是指令回 null；指令格式錯誤丟 ArgumentException。</summary>
    public static byte[]? Encode(string input){
        if(!IsCommand(input))return null;
        if(input==LeaveCommand)return LoginPackets.Pad([Opcode,Leave]);
        if(input==QueryCommand)return LoginPackets.Pad([Opcode,Query]);
        string name=input.Length>BanishCommand.Length?input[(BanishCommand.Length+1)..].Trim():"";
        if(name.Length==0||name.Length>PlayerChatProtocol.MaxCharacters||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException("聊天隊伍踢人格式：/chatbanish 名稱（名稱不可含空白）");
        try{return LoginPackets.Pad([Opcode,Banish,..LoginPackets.TextEncoding.GetBytes(name),0]);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("名稱含有遊戲字集無法表示的字元。");}
    }
}

public sealed partial class WorldSession {
    readonly Queue<PlayerChatLine> playerChatLines=new();
    long playerChatSequence;int playerChatRevision;
    public PlayerChatLine[] PlayerChatLines=>playerChatLines.ToArray();
    public int PlayerChatRevision=>playerChatRevision;
    void ResetPlayerChat(){playerChatLines.Clear();playerChatSequence=0;playerChatRevision++;}
    bool ReceivePlayerChat(byte opcode,LoginReader r,byte[] plain){
        if(!PlayerChatProtocol.TryDecode(plain,out var line))return false;
        if(BuddyBlocksChat(plain))return true;
        playerChatLines.Enqueue(line! with{Sequence=++playerChatSequence});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;return true;
    }
}

public sealed partial class LoginConnection {
    public Task<bool> SendLocation(string receiver,int session,int world,uint actor)=>SendLocationCore(receiver,session,world,actor);
    bool playerChatSending;int playerChatLease;long playerChatNextAt;
    string playerChatNotice="";
    void ResetPlayerChatSend(){playerChatLease++;playerChatSending=false;playerChatNextAt=0;playerChatNotice="";}
    bool PlayerChatAccess=>State==LoginState.InWorld&&World.Ready&&World.Actor is {} a&&World.Status is {Hp:>0} s&&a.ObjectId==s.ObjectId&&!World.OwnDead&&!World.TeleportPending&&!World.ActionLocked;
    bool PlayerChatStamp(int session,int world,uint actor)=>session==generation&&world==World.WorldEpoch&&actor!=0&&actor==World.Actor?.ObjectId&&actor==World.Status?.ObjectId;
    public PlayerChatView PlayerChatSnapshot()=>CaptureWorld(()=>new PlayerChatView(generation,World.WorldEpoch,World.Actor?.ObjectId??0,World.PlayerChatRevision,PlayerChatAccess&&!playerChatSending&&WorldClock()>=playerChatNextAt,playerChatNotice,World.PlayerChatLines));
    public async Task<bool> SendPlayerChat(string text,int session,int world,uint actor){
        // R6-E（FU7§1.5）：刪除物品的確認在聊天輸入列（數量或 Y/N），這一列輸入是答覆，不當聊天送出。
        if(ItemDeletePromptActive)return await AnswerItemDelete(text,session,world,actor);
        // R10-F（T390／#905，gap-matrix 15.12.6 1:29）：私商 1028 高價確認也在聊天輸入列（sub_4FD770 → sub_AC98E0，回呼 sub_4FE4A0）：
        // 這一列是答覆（Y＝用原來那一包再送，其他＝不送），不當聊天送出。回 true＝已當作答覆處理（輸入列可清掉）。
        if(PrivateShopConfirmCurrent!=null){
            if(!PlayerChatStamp(session,world,actor))return false;
            await AnswerPrivateShopConfirm(text);return true;
        }
        if(text=="/loc"||text.StartsWith("/loc ",StringComparison.Ordinal))return await SendLocation(text.Length>5?text[5..]:"",session,world,actor);
        if(TradeAskCommand.Is(text)){
            // R6-E（FU7§1.1）：`/accepttrade` 只切換本機的交易詢問旗標（原版不送包），本引擎另存進 C244 角色設定。
            if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor))return false;
            bool done=await ToggleAcceptTrade(new PlayerHudStamp(session,world,actor));
            if(session!=generation)return false;
            playerChatNotice=TradeAskNotice;Changed?.Invoke();return done;
        }
        if(ChatChannelCommands.IsCommand(text)){
            // R6-E：`/whisper`、`/tradechat`、`/receive`（與中文別名）交給 C26 頻道開關（ChatChannelToggle.cs），不當聊天送出。
            if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor))return false;
            bool done=await SendChatChannelCommand(text,new PlayerHudStamp(session,world,actor));
            if(session!=generation)return false;
            playerChatNotice=ChatChannelNotice.Length>0?ChatChannelNotice:"頻道切換未送出。";Changed?.Invoke();return done;
        }
        if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor)||playerChatSending||WorldClock()<playerChatNextAt||sendMovementGate==null)return false;
        // R9-C（#847，FU9§9）：組隊、好友／封鎖、交易、個人商店、記憶座標的聊天指令（ChatFollowupCommands），不當聊天送出。
        if(ChatFollowupCommands.Parse(text) is {} followup)return await SendChatFollowup(followup,session,world,actor);
        byte[] packet;bool command=ChatPartyCommands.IsCommand(text);string? clientNotice=null;Action? clientAdmitted=null;
        try{
            if(ClientChatCommands.Parse(text) is {} client){
                // T316：/focus、/war、/surrender、/cease、/propose、/divorce（與中文別名）由客戶端自己編成封包，不當聊天送出。
                if(ClientCommandPacket(client,out string notice,out clientAdmitted) is not {} p){playerChatNotice=notice.Length>0?notice:"指令未送出。";Changed?.Invoke();return false;}
                packet=p;clientNotice=notice;
            }
            else if(DuelChatPacket(text,out clientNotice,out clientAdmitted) is {} duelPacket)packet=duelPacket; // R8-C（#822）：/duel、/決鬥 → C5（Duel.cs）
            else packet=PlayerQueryPacket(text,session,world,actor)??ChatPartyCommands.Encode(text)??PlayerChatProtocol.Encode(text);
        }
        catch(ArgumentException ex){playerChatNotice=ex.Message;Changed?.Invoke();return false;}
        var sender=sendMovementGate;int lease=++playerChatLease;playerChatSending=true;
        try{
            bool sent=await sender(packet,()=>lease==playerChatLease&&PlayerChatAccess&&PlayerChatStamp(session,world,actor),()=>{
                // Local double-submit protection, not a claim about the server's chat cadence.
                playerChatNextAt=WorldClock()+1000;
                clientAdmitted?.Invoke();
            });
            if(session!=generation||lease!=playerChatLease)return false;
            playerChatNotice=sent?(clientNotice??(command?"聊天隊伍指令已送出；結果以伺服器回傳為準。":"訊息已送出；聊天內容以伺服器回傳為準。")):"角色或場景已變更，訊息未送出。";
            Changed?.Invoke();return sent;
        }catch(Exception ex){
            if(session==generation){Cancel();Set(LoginState.Faulted,"聊天傳送失敗："+ex.Message);}
            return false;
        }finally{if(lease==playerChatLease)playerChatSending=false;}
    }
    /// <summary>
    /// T316：客戶端斜線指令 → 封包。null＝不送（notice 寫原因；本機檢查擋下時原版的訊息已印在聊天欄）。
    /// /focus：有參數 C129 子碼 0＋名稱，沒有參數子碼 2（取清單，只有這裡會送，FU6§1.1）。
    /// /war、/surrender、/cease：C227 type 0／2／3＋血盟名，本機檢查照 FU6§3.2（ClanWarRequestPacket）。
    /// /propose、/divorce：C50 `cc` 0／1，沒有目標欄位，原版不檢查（FU6§4.1）。
    /// </summary>
    byte[]? ClientCommandPacket(ClientChatCommand c,out string notice,out Action? admitted){
        admitted=null;notice="";
        switch(c.Kind){
        case ClientChatCommandKind.Focus:
            if(c.Argument.Length==0){notice="已要求關注血盟清單；結果以伺服器回傳為準。";return ClanProtocol.EncodeAttentionGetList();}
            try{var p=ClanProtocol.EncodeAttentionAdd(c.Argument);notice="關注血盟請求已送出；結果以伺服器回傳為準。";return p;}
            catch(ArgumentException ex){notice=ex.Message;return null;}
        case ClientChatCommandKind.War or ClientChatCommandKind.Surrender or ClientChatCommandKind.Cease:{
            var command=c.Kind switch{ClientChatCommandKind.War=>ClanWarCommand.Declare,ClientChatCommandKind.Surrender=>ClanWarCommand.Surrender,_=>ClanWarCommand.Cease};
            var packet=ClanWarRequestPacket(command,c.Argument,"chat",out notice);
            if(packet!=null){string sentNotice=ClientChatCommands.Label(c.Kind)+"請求已送出；結果以伺服器回報為準。";notice=sentNotice;admitted=()=>World.ClanWarNote(sentNotice);}
            return packet;}
        case ClientChatCommandKind.Propose:
            notice="求婚已送出；對象與結果由伺服器判定。";admitted=()=>World.MarriageCommandSent(false);return MarriagePackets.Propose();
        default:
            notice="離婚請求已送出；伺服器會再詢問確認。";admitted=()=>World.MarriageCommandSent(true);return MarriagePackets.Divorce();
        }
    }

    /// <summary>R9-C（#847）：各指令的內容在 Party.cs、Buddies.cs、Trade.cs；記憶座標在這裡（只用既有的 C165 編碼與開窗入口）。</summary>
    internal ChatCommandPlan ChatFollowupPlan(ChatFollowupCommand c){
        try{
            return c.Kind switch{
                ChatFollowupKind.Invite or ChatFollowupKind.Banish or ChatFollowupKind.OutParty or ChatFollowupKind.ChatInvite=>PartyChatCommand(c),
                ChatFollowupKind.Buddy or ChatFollowupKind.AddBuddy or ChatFollowupKind.DelBuddy or ChatFollowupKind.Exclude or ChatFollowupKind.Include=>BuddyChatCommand(c),
                ChatFollowupKind.Trade=>TradeChatCommand(),
                ChatFollowupKind.Shop=>ShopChatCommand(),
                _=>BookmarkChatCommand(c)};
        }
        catch(System.Text.EncoderFallbackException){return ChatCommandPlan.Refuse("名稱含有遊戲字集無法表示的字元。");}
        catch(ArgumentException ex){return ChatCommandPlan.Refuse(ex.Message);}
    }
    /// <summary>
    /// FU9§1.1 第 2 點：`bookmark 名稱`／`記憶 名稱`（sub_F43C30 → sub_4FE1D0）送新增書籤（3.81 C165），名稱 &gt; 30 byte 印 204；
    /// 不帶名稱＝開一般模式的書籤窗。`qbookmark 名稱`（sub_F48450 → sub_502720）同樣送 C165；原版另設 this+486＝1，
    /// 收到新書籤時也加進快速移動清單。R10-J（T394／#909）：C165 放行時經 ClientForm.OfferQuickBookmark 在原生書籤窗設這個旗標
    /// （BookmarkWindowPanel.RequestQuickOnNextBookmark）；--classic-ui 或書籤窗停用時沒有快速移動清單，只送新增。
    /// 限制地圖（214）的本機檢查沒做：部分地圖只限制特定區域，區域表待查；3.81 伺服器同樣回 214。
    /// </summary>
    ChatCommandPlan BookmarkChatCommand(ChatFollowupCommand c){
        string name=c.Argument;
        if(name.Length==0){
            if(c.Kind==ChatFollowupKind.QBookmark)return ChatCommandPlan.Refuse("格式：/qbookmark 名稱");
            return ChatCommandPlan.Do("已切換記憶座標視窗。",()=>{
                if(ClientForm.OfferChatWindow(World,ChatWindowRequest.Bookmark))return true;
                playerChatNotice="記憶座標視窗未開啟（原生 UI 未啟動）。";return false;});
        }
        if(BookmarkPackets.NameError(name) is {} error)return ChatCommandPlan.Refuse(error);
        if(LoginPackets.TextEncoding.GetByteCount(name)>ChatFollowupCommands.MarkNameBytes)return ChatCommandPlan.Refuse(ChatFollowupCommands.MarkTooLongText);
        var store=World.Bookmarks;
        if(!store.CanAdd(name))return ChatCommandPlan.Refuse("書籤已滿或已有同名書籤；未送出。");
        if(c.Kind==ChatFollowupKind.QBookmark){
            int session=generation;uint actor=World.Actor?.ObjectId??0;var world=World;
            return new ChatCommandPlan("已要求記憶目前位置「"+name+"」；收到伺服器書籤後加進快速移動清單。",
                [BookmarkPackets.Add(name)],()=>{store.Sent(null);ClientForm.OfferQuickBookmark(world,session,actor);},()=>store.CanAdd(name));
        }
        return new ChatCommandPlan("已要求記憶目前位置「"+name+"」；等待伺服器書籤回覆。",
            [BookmarkPackets.Add(name)],()=>store.Sent(null),()=>store.CanAdd(name));
    }
    async Task<bool> SendChatFollowup(ChatFollowupCommand c,int session,int world,uint actor){
        var plan=ChatFollowupPlan(c);
        if(plan.Packets.Length==0){
            playerChatNotice=plan.Notice;
            bool done=plan.Local?.Invoke()??false;
            Changed?.Invoke();return done;
        }
        var sender=sendMovementGate!;int lease=++playerChatLease;playerChatSending=true;
        bool Valid()=>lease==playerChatLease&&PlayerChatAccess&&PlayerChatStamp(session,world,actor);
        try{
            bool first=await sender(plan.Packets[0],()=>Valid()&&(plan.StillValid?.Invoke()??true),()=>{playerChatNextAt=WorldClock()+1000;plan.Admitted?.Invoke();});
            if(session!=generation||lease!=playerChatLease)return false;
            string notice=first?plan.Notice:"角色或場景已變更，指令未送出。";
            // `/addbuddy`、`/delbuddy` 原版接著送好友查詢（FU9§9.2）；第一包放行後才送，後續被擋只少了查詢。
            for(int i=1;first&&i<plan.Packets.Length;i++){
                if(!await sender(plan.Packets[i],Valid,()=>{})){notice+="（後續的名單查詢未送出）";break;}
                if(session!=generation||lease!=playerChatLease)return false;
            }
            playerChatNotice=notice;Changed?.Invoke();return first;
        }catch(Exception ex){
            if(session==generation){Cancel();Set(LoginState.Faulted,"聊天指令傳送失敗："+ex.Message);}
            return false;
        }finally{if(lease==playerChatLease)playerChatSending=false;}
    }
}
