namespace P06;

public enum PlayerQueryKind {None,Who,Rank,Check}

/// <summary>T331 (#769): a self-addressed ranking page; a handler that shows it natively sets Handled.</summary>
public sealed class RankingWindowEventArgs(uint objectId,string html,IReadOnlyList<string> arguments,IReadOnlyList<string> lines):EventArgs {
    public uint ObjectId=>objectId;
    public string Html=>html;
    public IReadOnlyList<string> Arguments=>arguments;
    public IReadOnlyList<string> Lines=>lines;
    public bool Handled{get;set;}
}

// Verified source + d287 JAR contract: docs/protocol/player-query.md.
// C99 is anti-cheat telemetry and C63/sub1 changes clan rank, so neither is ever sent from here.
public static class PlayerQueryPackets {
    public const int MaxNameCharacters=52; // Local input budget, same as whisper recipients.
    public const string RankAction="lv_ranking",CheckAction="pc_list",CheckPrompt="請選擇要查詢的玩家。";
    static readonly string[] CheckFailures=["目標不存在。","只能對玩家查詢狀態。","查詢失敗。"];
    static readonly string[] Equipment=["武器","頭盔","盔甲","內衣","斗篷","手套","長靴","盾牌","項鍊","戒指","戒指","戒指","戒指","耳環","耳環","腰帶","臂甲"];
    public static byte[] Who(string name){
        if(name.Length>MaxNameCharacters||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException("查詢格式：/who 名字；只輸入 /who 可查詢線上資訊。名字不可含空白，最多 52 字。");
        try{return LoginPackets.Pad([206,..LoginPackets.TextEncoding.GetBytes(name),0]);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("名字含有遊戲字集無法表示的字元。");}
    }
    // Self-addressed helper actions: L1ActionPc and PcScannerHandler take the player's own id.
    public static byte[] Rank(uint self)=>NpcPackets.Action(self,RankAction);
    public static byte[] CheckBegin(uint self)=>NpcPackets.Action(self,CheckAction);
    public static byte[] CheckTarget(uint self,uint target)=>LoginPackets.Pad([223,..BitConverter.GetBytes(self),0,..BitConverter.GetBytes(target)]);
    public static PlayerQueryKind Parse(string input,out string argument){
        argument="";
        int split=0;while(split<input.Length&&!char.IsWhiteSpace(input[split]))split++;
        var kind=input[..split].ToLowerInvariant() switch{"/who"=>PlayerQueryKind.Who,"/rank"=>PlayerQueryKind.Rank,"/check"=>PlayerQueryKind.Check,_=>PlayerQueryKind.None};
        if(kind==PlayerQueryKind.None)return kind;
        if(input.Any(char.IsControl))throw new ArgumentException("查詢指令必須是單行文字，不可含控制字元。");
        argument=input[split..].Trim();
        if(kind==PlayerQueryKind.Rank&&argument.Length>0)throw new ArgumentException("排名查詢不需要參數，請只輸入 /rank。");
        if(kind==PlayerQueryKind.Check&&argument.Length>0)throw new ArgumentException("請先選取玩家，再只輸入 /check。");
        return kind;
    }
    public static byte[]? Encode(string input,uint self,uint? target)=>Parse(input,out string argument) switch{
        PlayerQueryKind.Who=>Who(argument),
        PlayerQueryKind.Rank=>Rank(self),
        PlayerQueryKind.Check=>target is null or 0?throw new ArgumentException("請先選取要查詢的玩家，再輸入 /check。"):CheckBegin(self),
        _=>null};
    // S71/166 is the server's generic "%0" message (352 source call sites), not only S_WhoCharinfo.
    public static string? DecodeTextMessage(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=71||r.H()!=166||r.C()!=1)return null;
        string text=r.S();r.Finish();return text;
    }
    // S_WhoCharinfo always carries exactly one alignment token; other 166 text keeps its normal route.
    public static bool IsWhoInfo(string text)=>text.Contains("($1501)")||text.Contains("($1502)")||text.Contains("($1503)");
    public static bool IsCheckFailure(string text)=>CheckFailures.Contains(text);
    // Known server alignment tokens only; render as plain text, never as HTML or commands.
    public static string FormatWho(string text)=>"玩家資訊："+Display(text.Replace("($1501)","（正義者）").Replace("($1502)","（中立者）").Replace("($1503)","（邪惡者）"));
    public static string? DecodeWhoMessage(byte[] plain)=>DecodeTextMessage(plain) is {} text&&IsWhoInfo(text)?FormatWho(text):null;
    public static (uint ObjectId,string[] Lines,string Html,string[] Arguments)? DecodeWhoHtml(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=39)return null;
        uint id=r.D();if(r.S()!="y_who")return null;
        if(id==0||r.H()!=1||r.H()!=9)throw new ProtocolException("who HTML identity/arguments");
        string[] labels=["線上人數","經驗倍率","掉寶倍率","金錢倍率","手點能力上限","單項萬能藥上限","萬能藥總瓶數","目前時間","重啟時間"];
        var lines=new string[labels.Length];var args=new string[labels.Length];
        for(int i=0;i<lines.Length;i++){args[i]=Display(r.S());lines[i]=labels[i]+"："+args[i];}
        r.Finish();return(id,lines,"y_who",args);
    }
    public static (uint ObjectId,string[] Lines,string Html,string[] Arguments)? DecodeCheckHtml(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=39)return null;
        uint id=r.D();if(r.S()!="pc_show")return null;
        if(id==0||r.H()!=1||r.H()!=29)throw new ProtocolException("player check HTML identity/arguments");
        var a=new string[29];for(int i=0;i<a.Length;i++)a[i]=Display(r.S());
        r.Finish();
        var lines=new List<string>{$"目標：{a[0]}｜等級 {a[1]}",$"HP {a[2]}｜MP {a[3]}｜AC {a[4]}｜MR {a[5]}",$"力量 {a[6]}｜敏捷 {a[7]}｜體質 {a[8]}｜智力 {a[9]}｜精神 {a[10]}｜魅力 {a[11]}"};
        for(int i=0;i<Equipment.Length;i++)if(a[12+i].Length>0)lines.Add($"裝備・{Equipment[i]}：{a[12+i]}");
        return(id,lines.ToArray(),"pc_show",a);
    }
    // Returns null for anything but a complete known ranking page, so ordinary NPC dialogs stay untouched.
    public static (uint ObjectId,string[] Lines,string Html,string[] Arguments)? DecodeRankHtml(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=39)return null;
        uint id=r.D();string html=r.S();
        if(id==0||html is not ("y_h_2" or "y_k_2" or "y_k_3" or "y_w_1" or "y_c_1"))return null;
        int expected=html switch{"y_h_2"=>11,"y_c_1"=>20,_=>10};
        if(r.H()!=1||r.H()!=expected)return null;
        var a=new string[expected];for(int i=0;i<a.Length;i++)a[i]=Display(r.S());
        r.Finish();
        string title=html switch{"y_h_2"=>"英雄風雲榜・"+a[10],"y_k_2"=>"殺人榜","y_k_3"=>"往生榜","y_w_1"=>"財富風雲榜",_=>"血盟風雲榜"};
        var lines=new List<string>{"排名："+title};
        // Only rows 0-9 are ranks: y_c_1 #10-19 are war-declaration labels, y_h_2 #10 is the class title.
        for(int i=0;i<10;i++){
            string row=a[i].Trim();if(row.Length==0)continue;
            if(html is "y_k_2" or "y_k_3"&&row.Split(',') is [var name,var count])row=$"{name}（{count}）";
            lines.Add($"No.{i+1} {row}");
        }
        if(lines.Count==1)lines.Add("目前沒有排名資料。");
        return(id,lines.ToArray(),html,a);
    }
    static string Display(string text)=>PlayerChatProtocol.Display(text);
    // T331 (#769): open-window event for the native ranking window (NativeUi/GameRankingPanel). Nobody handles it with
    // --classic-ui or when the reference GameRankingUI.xml is unavailable, so the query page below stays the fallback.
    public static event EventHandler<RankingWindowEventArgs>? RankingWindowRequested;
    /// <summary>true when a handler took the page; a throwing handler counts as not handled (logged, never fatal).</summary>
    internal static bool RequestRankingWindow(uint id,string html,string[] args,string[] lines){
        if(RankingWindowRequested is not {} handlers)return false;
        var e=new RankingWindowEventArgs(id,html,args,lines);
        foreach(EventHandler<RankingWindowEventArgs> h in handlers.GetInvocationList()){
            try{h(null,e);}catch(Exception ex){NativeUi.UiResourceLog.Write("ranking-ui 開窗事件失敗，改用查詢頁："+ex.Message);return false;}
            if(e.Handled)return true;
        }
        return false;
    }
}

public sealed partial class WorldSession {
    TaskCompletionSource<bool>? playerCheck;
    // A newer request releases the older continuation without sending its target.
    internal Task<bool> BeginPlayerCheck(){
        playerCheck?.TrySetResult(false);
        playerCheck=new(TaskCreationOptions.RunContinuationsAsynchronously);
        return playerCheck.Task;
    }
    internal bool AbandonPlayerCheck(Task<bool> request){
        if(playerCheck?.Task!=request)return false;
        playerCheck=null;return true;
    }
    void AddPlayerQueryLine(string text){
        playerChatLines.Enqueue(new(++playerChatSequence,0,"[查詢] "+text));
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
    bool ReceivePlayerQueryMessage(byte[] plain){
        if(PlayerQueryPackets.DecodeTextMessage(plain) is not {} text)return false;
        if(PlayerQueryPackets.IsWhoInfo(text)){
            if(Ready)AddPlayerQueryLine(PlayerQueryPackets.FormatWho(text));
            // Query information is not an equipment/trade/NPC acknowledgement.
            return true;
        }
        if(playerCheck is not {} check)return false;
        // S_SelectTarget precedes this prompt, so the server is already in select mode.
        if(text==PlayerQueryPackets.CheckPrompt&&!check.Task.IsCompleted){check.TrySetResult(true);return true;}
        if(PlayerQueryPackets.IsCheckFailure(text)&&check.Task.IsCompleted){playerCheck=null;if(Ready)AddPlayerQueryLine(text);}
        return false;
    }
    // WIN§2 T140: S_HYPERTEXT results belong to the NPC/HTML window, not to chat.
    // S71 text (who summary, check failures) stays a chat line because the server sends it as chat text.
    bool ReceivePlayerQueryHtml(byte[] plain){
        if(PlayerQueryPackets.DecodeWhoHtml(plain) is {} who){
            // y_who is addressed to self, not the currently selected NPC; never mutate its dialog.
            ShowSelfQuery(who.ObjectId,who.Html,who.Arguments,who.Lines);return true;
        }
        if(PlayerQueryPackets.DecodeCheckHtml(plain) is {} check){
            if(check.ObjectId==Status?.ObjectId)playerCheck=null;
            ShowSelfQuery(check.ObjectId,check.Html,check.Arguments,check.Lines);return true;
        }
        if(PlayerQueryPackets.DecodeRankHtml(plain) is not {} rank)return false;
        if(rank.ObjectId==Status?.ObjectId){
            if(Ready&&PlayerQueryPackets.RequestRankingWindow(rank.ObjectId,rank.Html,rank.Arguments,rank.Lines))return true;
            ShowSelfQuery(rank.ObjectId,rank.Html,rank.Arguments,rank.Lines);return true;}
        // Ranking NPC page: the normal dialog handles the same packet; decoded rows replace a missing template.
        OfferDialogFallback(rank.Lines);
        return false;
    }
    void ShowSelfQuery(uint id,string html,string[] args,string[] lines){
        if(Ready&&id==Status?.ObjectId)ShowQueryPage(id,html,args,lines);
    }
}

public sealed partial class LoginConnection {
    // Local recovery policy, not a server deadline.
    public int PlayerCheckTimeoutMs {get;set;}=3000;
    byte[]? PlayerQueryPacket(string text,int session,int world,uint actor){
        uint? target=World.TargetId;
        byte[]? packet=PlayerQueryPackets.Encode(text,actor,target);
        if(packet!=null&&PlayerQueryPackets.Parse(text,out _)==PlayerQueryKind.Check)
            _=ContinuePlayerCheck(World.BeginPlayerCheck(),target!.Value,session,world,actor);
        return packet;
    }
    // The target goes out only after the server confirms select mode; no reply means nothing more is sent.
    async Task ContinuePlayerCheck(Task<bool> request,uint target,int session,int world,uint actor){
        if(await Task.WhenAny(request,Task.Delay(PlayerCheckTimeoutMs))!=request){
            if(World.AbandonPlayerCheck(request)&&session==generation){
                playerChatNotice="未收到伺服器的選取提示，玩家資訊查詢未完成；不會自動重送。";Changed?.Invoke();
            }
            return;
        }
        if(!await request)return;
        bool Valid()=>PlayerChatAccess&&PlayerChatStamp(session,world,actor)&&World.TargetId==target;
        try{
            bool sent=sendMovementGate is {} sender&&await sender(PlayerQueryPackets.CheckTarget(actor,target),Valid,()=>{});
            if(session!=generation)return;
            playerChatNotice=sent?"已送出查詢目標；玩家資訊以伺服器回傳為準。":"目標或場景已變更，玩家資訊查詢未完成。";
            Changed?.Invoke();
        }catch(Exception ex){
            if(session==generation){Cancel();Set(LoginState.Faulted,"玩家資訊查詢傳送失敗："+ex.Message);}
        }
    }
}
