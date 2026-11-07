namespace P06;

public enum PlayerChatChannel { Normal, Shout, Global, Whisper, Clan, Party }
public sealed record PlayerChatLine(long Sequence,uint Speaker,string Text,PlayerChatChannel Channel=PlayerChatChannel.Normal);
public sealed record PlayerChatView(int Generation,int WorldEpoch,uint Actor,int Revision,bool CanSend,string Notice,PlayerChatLine[] Lines);
public sealed record PlayerChatInput(PlayerChatChannel Channel,string Text,string Receiver="");

// Source contract and hashes: docs/protocol/player-chat.md. Server remains authoritative.
public static class PlayerChatProtocol {
    public const int MaxCharacters=52;
    // Local input budget: quote + receiver (52) + separator + body (52), or /say + body.
    public const int MaxInputCharacters=106;
    public const string InputHelp="一般：直接輸入；!喊話 &全體 @血盟 #隊伍；\"對象 訊息：密語；/say 原文：一般";
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
    public static string ChannelName(PlayerChatChannel channel)=>channel switch{PlayerChatChannel.Shout=>"喊話",PlayerChatChannel.Global=>"全體",PlayerChatChannel.Whisper=>"密語",PlayerChatChannel.Clan=>"血盟",PlayerChatChannel.Party=>"隊伍",_=>"一般"};
    public static string FormatLine(PlayerChatLine line)=>$"[{ChannelName(line.Channel)}] {line.Text}";
    public static bool TryDecode(byte[] plain,out PlayerChatLine? line){
        line=null;
        if(plain.Length==0)return false;
        var r=new LoginReader(plain);byte opcode=r.C();uint speaker=0;string text;PlayerChatChannel channel;
        switch(opcode){
        case 81:
            byte localType=r.C();
            if(localType is not (0 or 2 or 14))return false;
            channel=localType switch{2=>PlayerChatChannel.Shout,14=>PlayerChatChannel.Party,_=>PlayerChatChannel.Normal};
            speaker=r.D();text=r.S();
            if(localType==2){r.H();r.H();} // Shout carries x/y after its string, including invisible speakers.
            break;
        case 73:
            channel=PlayerChatChannel.Whisper;text=$"{r.S()} -> 我：{r.S()}";break;
        case 243:
            byte globalType=r.C();
            if(globalType is not (3 or 4 or 9 or 11))return false;
            text=r.S();
            // Type 9 is shared with S_SystemMessage. Leave non-whisper replies to Inventory.
            if(globalType==9&&!(text.StartsWith("-> (",StringComparison.Ordinal)&&text.IndexOf(") ",4,StringComparison.Ordinal)>4))return false;
            channel=globalType switch{3=>PlayerChatChannel.Global,4=>PlayerChatChannel.Clan,11=>PlayerChatChannel.Party,_=>PlayerChatChannel.Whisper};break;
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
    bool playerChatSending;int playerChatLease;long playerChatNextAt;
    string playerChatNotice="";
    void ResetPlayerChatSend(){playerChatLease++;playerChatSending=false;playerChatNextAt=0;playerChatNotice="";}
    bool PlayerChatAccess=>State==LoginState.InWorld&&World.Ready&&World.Actor is {} a&&World.Status is {Hp:>0} s&&a.ObjectId==s.ObjectId&&!World.OwnDead&&!World.TeleportPending&&!World.ActionLocked;
    bool PlayerChatStamp(int session,int world,uint actor)=>session==generation&&world==World.WorldEpoch&&actor!=0&&actor==World.Actor?.ObjectId&&actor==World.Status?.ObjectId;
    public PlayerChatView PlayerChatSnapshot()=>CaptureWorld(()=>new PlayerChatView(generation,World.WorldEpoch,World.Actor?.ObjectId??0,World.PlayerChatRevision,PlayerChatAccess&&!playerChatSending&&WorldClock()>=playerChatNextAt,playerChatNotice,World.PlayerChatLines));
    public async Task<bool> SendPlayerChat(string text,int session,int world,uint actor){
        if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor)||playerChatSending||WorldClock()<playerChatNextAt||sendMovementGate==null)return false;
        byte[] packet;
        try{packet=PlayerChatProtocol.Encode(text);}
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
