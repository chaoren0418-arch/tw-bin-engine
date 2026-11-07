namespace P06;

// Verified source + d287 JAR contract: docs/protocol/player-query.md.
// C99 is anti-cheat telemetry; C63/sub1 changes clan rank. Neither queries a player leaderboard.
public static class PlayerQueryPackets {
    public const int MaxNameCharacters=52; // Local input budget, same as whisper recipients.
    public static byte[] Who(string name){
        if(name.Length>MaxNameCharacters||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException("查詢格式：/who 名字；只輸入 /who 可查詢線上資訊。名字不可含空白，最多 52 字。");
        try{return LoginPackets.Pad([206,..LoginPackets.TextEncoding.GetBytes(name),0]);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("名字含有遊戲字集無法表示的字元。");}
    }
    public static bool TryEncodeCommand(string input,out byte[] packet){
        packet=[];
        int split=0;while(split<input.Length&&!char.IsWhiteSpace(input[split]))split++;
        string command=input[..split];
        if(command.Equals("/check",StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("此伺服器的 C99 是防外掛回報，不支援物件資訊查詢；請用 /who 名字。");
        if(command.Equals("/rank",StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("此伺服器的 C63 是血盟階級與其他操作，不是排行榜；目前沒有已核定的排名查詢協定。");
        if(!command.Equals("/who",StringComparison.OrdinalIgnoreCase))return false;
        if(input.Any(char.IsControl))throw new ArgumentException("查詢指令必須是單行文字，不可含控制字元。");
        packet=Who(input[split..].Trim());return true;
    }
    public static string? DecodeWhoMessage(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=71||r.H()!=166)return null;
        if(r.C()!=1)throw new ProtocolException("who message argument count");
        string text=r.S();r.Finish();
        // Known server alignment tokens only; render as plain text, never as HTML or commands.
        return "玩家資訊："+Display(text.Replace("($1501)","（正義者）").Replace("($1502)","（中立者）").Replace("($1503)","（邪惡者）"));
    }
    public static (uint ObjectId,string[] Lines)? DecodeWhoHtml(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=39)return null;
        uint id=r.D();if(r.S()!="y_who")return null;
        if(id==0||r.H()!=1||r.H()!=9)throw new ProtocolException("who HTML identity/arguments");
        string[] labels=["線上人數","經驗倍率","掉寶倍率","金錢倍率","手點能力上限","單項萬能藥上限","萬能藥總瓶數","目前時間","重啟時間"];
        var lines=new string[labels.Length];
        for(int i=0;i<lines.Length;i++)lines[i]=labels[i]+"："+Display(r.S());
        r.Finish();return(id,lines);
    }
    static string Display(string text)=>PlayerChatProtocol.Display(text);
}

public sealed partial class WorldSession {
    void AddPlayerQueryLine(string text){
        playerChatLines.Enqueue(new(++playerChatSequence,0,"[查詢] "+text));
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
    bool ReceivePlayerQueryMessage(byte[] plain){
        if(PlayerQueryPackets.DecodeWhoMessage(plain) is not {} text)return false;
        if(Ready)AddPlayerQueryLine(text);
        // Query information is not an equipment/trade/NPC acknowledgement.
        return true;
    }
    bool ReceivePlayerQueryHtml(byte[] plain){
        if(PlayerQueryPackets.DecodeWhoHtml(plain) is not {} reply)return false;
        // y_who is addressed to self, not the currently selected NPC; never mutate its dialog.
        if(Ready&&reply.ObjectId==Status?.ObjectId)
            foreach(string line in reply.Lines)AddPlayerQueryLine(line);
        return true;
    }
}
