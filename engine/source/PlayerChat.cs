namespace P06;

// Append-only: Whisper = received (S73), WhisperTo = sender echo (S243/9). Types 12–17 are receive-only (T175).
public enum PlayerChatChannel { Normal, Shout, Global, Whisper, Clan, Party, WhisperTo, Trade, Prince, ChatParty, Alliance, ClanNotice }
public sealed record PlayerChatLine(long Sequence,uint Speaker,string Text,PlayerChatChannel Channel=PlayerChatChannel.Normal) {
    public SharedLocation? Location {get;init;}
}
public sealed record PlayerChatView(int Generation,int WorldEpoch,uint Actor,int Revision,bool CanSend,string Notice,PlayerChatLine[] Lines);
public sealed record PlayerChatInput(PlayerChatChannel Channel,string Text,string Receiver="");

// Source contract and hashes: docs/protocol/player-chat.md. Server remains authoritative.
public static class PlayerChatProtocol {
    public const int MaxCharacters=52;
    // Local input budget: quote + receiver (52) + separator + body (52), or /say + body.
    public const int MaxInputCharacters=106;
    public const string InputHelp="一般：直接輸入；!喊話 &全體 @血盟 #隊伍；\"對象 訊息：密語；/say 原文：一般；/loc 角色：發送座標";
    public static PlayerChatInput ParseInput(string input){
        if(string.IsNullOrWhiteSpace(input))throw new ArgumentException("請輸入 1–52 字的單行訊息。");
        if(input.StartsWith("/say ",StringComparison.Ordinal))return new(PlayerChatChannel.Normal,input[5..]);
        if(input[0]=='\"'){
            int split=input.IndexOf(' ',1);
            if(split<=1)throw new ArgumentException("密語格式：\"對象 訊息");
            return new(PlayerChatChannel.Whisper,input[(split+1)..],input[1..split]);
        }
        var channel=input[0] switch{'!'=>PlayerChatChannel.Shout,'&'=>PlayerChatChannel.Global,'@'=>PlayerChatChannel.Clan,'#'=>PlayerChatChannel.Party,_=>PlayerChatChannel.Normal};
        return new(channel,channel==PlayerChatChannel.Normal?input:input[1..]);
    }
    public static byte[] Encode(string input)=>Encode(ParseInput(input));
    public static byte[] Encode(PlayerChatInput message){
        string text=message.Text;
        if(string.IsNullOrWhiteSpace(text)||text.Length>MaxCharacters||text.Any(char.IsControl))
            throw new ArgumentException("請輸入 1–52 字的單行訊息。");
        try{
            byte[] body=LoginPackets.TextEncoding.GetBytes(text);
            if(message.Channel==PlayerChatChannel.Whisper){
                string receiver=message.Receiver;
                if(string.IsNullOrWhiteSpace(receiver)||receiver.Length>MaxCharacters||receiver.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
                    throw new ArgumentException("請輸入密語對象名稱（不可含空白，最多 52 字）。");
                return LoginPackets.Pad([184,..LoginPackets.TextEncoding.GetBytes(receiver),0,..body,0]);
            }
            byte type=message.Channel switch{PlayerChatChannel.Normal=>0,PlayerChatChannel.Shout=>2,PlayerChatChannel.Global=>3,PlayerChatChannel.Clan=>4,PlayerChatChannel.Party=>11,_=>throw new ArgumentException("未知聊天頻道。")};
            return LoginPackets.Pad([(byte)(message.Channel==PlayerChatChannel.Global?40:136),type,..body,0]);
        }
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("訊息含有遊戲字集無法表示的字元。");}
    }
    public static string ChannelName(PlayerChatChannel channel)=>channel switch{
        PlayerChatChannel.Shout=>"喊話",PlayerChatChannel.Global=>"全體",PlayerChatChannel.Whisper or PlayerChatChannel.WhisperTo=>"密語",
        PlayerChatChannel.Clan=>"血盟",PlayerChatChannel.Party=>"隊伍",PlayerChatChannel.Trade=>"交易",PlayerChatChannel.Prince=>"盟主",
        PlayerChatChannel.ChatParty=>"聊天隊伍",PlayerChatChannel.Alliance=>"同盟",PlayerChatChannel.ClanNotice=>"血盟公告",_=>"一般"};
    // Original printers (docs/reference/ui-layout.md §3) show the server-formatted string as is:
    // `name: msg`, `[name] msg`, `{name} msg`, `{{name}} msg`, `(name) msg`, `-> (target) msg`. No local channel tag.
    public static string FormatLine(PlayerChatLine line)=>line.Location is {} location
        ?$"[座標] {line.Text}：{LocationShareProtocol.Format(location)}"
        :line.Text;
    // S243 chat types beyond whisper replies. 13/15/17 are not emitted by the d287 source; accepted for display only.
    static PlayerChatChannel? GlobalChannel(byte type)=>type switch{
        3=>PlayerChatChannel.Global,4=>PlayerChatChannel.Clan,9=>PlayerChatChannel.WhisperTo,11=>PlayerChatChannel.Party,
        12=>PlayerChatChannel.Trade,13=>PlayerChatChannel.Prince,15=>PlayerChatChannel.Alliance,17=>PlayerChatChannel.ClanNotice,_=>null};
    public static bool TryDecode(byte[] plain,out PlayerChatLine? line){
        line=null;
        if(plain.Length==0)return false;
        var r=new LoginReader(plain);byte opcode=r.C();uint speaker=0;string text;PlayerChatChannel channel;
        switch(opcode){
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
            if(GlobalChannel(r.C()) is not {} global)return false;
            text=r.S();
            // Type 9 is shared with S_SystemMessage. Leave non-whisper replies to Inventory.
            if(global==PlayerChatChannel.WhisperTo&&!(text.StartsWith("-> (",StringComparison.Ordinal)&&text.IndexOf(") ",4,StringComparison.Ordinal)>4))return false;
            channel=global;break;
        default:return false;
        }
        r.Finish();line=new(0,speaker,Display(text),channel);return true;
    }
    internal static string Display(string text)=>new(text.Where(c=>!char.IsControl(c)).Take(1024).ToArray());
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
        if(text=="/loc"||text.StartsWith("/loc ",StringComparison.Ordinal))return await SendLocation(text.Length>5?text[5..]:"",session,world,actor);
        if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor)||playerChatSending||WorldClock()<playerChatNextAt||sendMovementGate==null)return false;
        byte[] packet;
        try{packet=PlayerQueryPacket(text,session,world,actor)??PlayerChatProtocol.Encode(text);}
        catch(ArgumentException ex){playerChatNotice=ex.Message;Changed?.Invoke();return false;}
        var sender=sendMovementGate;int lease=++playerChatLease;playerChatSending=true;
        try{
            bool sent=await sender(packet,()=>lease==playerChatLease&&PlayerChatAccess&&PlayerChatStamp(session,world,actor),()=>{
                // Local double-submit protection, not a claim about the server's chat cadence.
                playerChatNextAt=WorldClock()+1000;
            });
            if(session!=generation||lease!=playerChatLease)return false;
            playerChatNotice=sent?"訊息已送出；聊天內容以伺服器回傳為準。":"角色或場景已變更，訊息未送出。";
            Changed?.Invoke();return sent;
        }catch(Exception ex){
            if(session==generation){Cancel();Set(LoginState.Faulted,"聊天傳送失敗："+ex.Message);}
            return false;
        }finally{if(lease==playerChatLease)playerChatSending=false;}
    }
}
