using System.Buffers.Binary;
namespace P06;

// Verified source identities and the recipient-only contract: docs/protocol/location-share.md.
public sealed record SharedLocation(ushort Map,ushort X,ushort Y,uint Zone);
public static class LocationShareProtocol {
    public static string Format(SharedLocation location)=>$"地圖 {location.Map} ({location.X}, {location.Y})";
    public static byte[] Encode(string receiver,SharedLocation location){
        if(string.IsNullOrWhiteSpace(receiver)||receiver.Length>52||receiver.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException("請輸入收件角色名稱（不可含空白，最多 52 字）。");
        byte[] name;
        try{name=LoginPackets.TextEncoding.GetBytes(receiver);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("收件角色名稱含有遊戲字集無法表示的字元。");}
        var packet=new byte[2+name.Length+1+10];packet[0]=254;packet[1]=11;name.CopyTo(packet,2);
        var fields=packet.AsSpan(3+name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(fields,location.Map);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[2..],location.X);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[4..],location.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(fields[6..],location.Zone);
        return LoginPackets.Pad(packet);
    }
    public static bool IsReply(byte[] plain)=>plain.Length>=3&&plain[0]==71&&BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(1)) is 1782 or 1783;
    public static PlayerChatLine Decode(byte[] plain){
        var r=new LoginReader(plain);byte opcode=r.C();PlayerChatLine line;
        if(opcode==250&&r.C()==111){
            string sender=r.S();var location=new SharedLocation(r.H(),r.H(),r.H(),r.D());
            line=new(0,0,PlayerChatProtocol.Display(sender),PlayerChatChannel.Whisper){Location=location};
        }else if(opcode==71){
            ushort message=r.H();byte count=r.C();
            string text=message switch {
                1782 when count==0=>"無法找到該角色或角色不在線上。",
                1783 when count==1=>$"已發送座標位置給 {PlayerChatProtocol.Display(r.S())}。",
                _=>throw new ProtocolException("unexpected location reply")
            };
            line=new(0,0,text,PlayerChatChannel.Whisper);
        }else throw new ProtocolException("unexpected location packet");
        r.Finish();return line;
    }
}

public sealed partial class WorldSession {
    void ReceiveLocationShare(byte[] plain){
        var line=LocationShareProtocol.Decode(plain);
        playerChatLines.Enqueue(line with{Sequence=++playerChatSequence});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
}

public sealed partial class LoginConnection {
    async Task<bool> SendLocationCore(string receiver,int session,int world,uint actor){
        if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor)||playerChatSending||WorldClock()<playerChatNextAt||sendMovementGate==null||World.MapId is not {} map||World.Actor is not {} own)return false;
        // Actor is the current client position (possibly predicted after C29, which has no per-step ACK).
        // Zone is opaque client UI metadata in the server relay; 0 is our neutral value.
        var location=new SharedLocation(map,own.X,own.Y,0);byte[] packet;
        try{packet=LocationShareProtocol.Encode(receiver,location);}
        catch(ArgumentException ex){playerChatNotice=ex.Message;Changed?.Invoke();return false;}
        var sender=sendMovementGate;int lease=++playerChatLease;playerChatSending=true;
        try{
            bool sent=await sender(packet,()=>lease==playerChatLease&&PlayerChatAccess&&PlayerChatStamp(session,world,actor)&&World.MapId==map&&World.Actor is {} current&&current.X==location.X&&current.Y==location.Y,()=>playerChatNextAt=WorldClock()+1000);
            if(lease!=playerChatLease||!PlayerChatStamp(session,world,actor))return false;
            playerChatNotice=sent?"座標請求已送出，等待伺服器確認。":"角色或位置已變更，請重新發送座標。";
            Changed?.Invoke();return sent;
        }catch(Exception ex){
            if(session==generation){Cancel();Set(LoginState.Faulted,"座標傳送失敗："+ex.Message);}
            return false;
        }finally{if(lease==playerChatLease)playerChatSending=false;}
    }
}
