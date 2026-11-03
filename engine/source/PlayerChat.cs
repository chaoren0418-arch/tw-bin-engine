namespace P06;

public sealed record PlayerChatLine(long Sequence,uint Speaker,string Text);
public sealed record PlayerChatView(int Generation,int WorldEpoch,uint Actor,int Revision,bool CanSend,string Notice,PlayerChatLine[] Lines);

// Fixed JAR d287: PacketHandler C136 -> C_Chat; type 0 normal chat;
// C_Chat reads C(type), S(text), rejects Java String.length > 52.
// S_Chat emits C81 C0 D(speaker) S(formatted text). C161 is NOT this message.
public static class PlayerChatProtocol {
    public const int MaxCharacters=52;
    public static byte[] Encode(string text){
        if(string.IsNullOrWhiteSpace(text)||text.Length>MaxCharacters||text.Any(char.IsControl))
            throw new ArgumentException("請輸入 1–52 字的單行訊息。");
        try{return LoginPackets.Pad([136,0,..LoginPackets.TextEncoding.GetBytes(text),0]);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("訊息含有遊戲字集無法表示的字元。");}
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
        if(opcode!=81||plain.Length<2||plain[1]!=0)return false;
        r.C();uint speaker=r.D();string text=r.S();r.Finish();
        playerChatLines.Enqueue(new(++playerChatSequence,speaker,PlayerChatProtocol.Display(text)));
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
