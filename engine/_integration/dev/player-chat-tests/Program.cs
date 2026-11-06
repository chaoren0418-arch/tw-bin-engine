using P06;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text.Json;

// Synthetic packets + in-memory transport against the actual product DLL.
// No game entry point, visible forms, server, database or game assets.
static class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> labels=[];
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);labels.Add(label);}
    static void Reject(Action action,string label){try{action();}catch(ArgumentException){Check(true,label);return;}throw new Exception(label);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F)!.GetValue(o)!;
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static byte[] Local(byte type,string text,uint id=123)=>Packet(b=>{b.Write((byte)81);b.Write(type);b.Write(id);Text(b,text);if(type==2){b.Write((ushort)32600);b.Write((ushort)32940);}});
    static byte[] Global(byte type,string text)=>Packet(b=>{b.Write((byte)243);b.Write(type);Text(b,text);});
    static byte[] Incoming()=>Packet(b=>{b.Write((byte)73);Text(b,"測試者");Text(b,"你好");});
    static void Feed(LoginConnection c,byte[] bytes)=>c.World.Receive(bytes,"offline");
    static LoginConnection Seed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Feed(c,Packet(b=>{
            b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(123u);b.Write((ushort)0);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);
            Text(b,"offline");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
        }));return c;
    }
    static void Encoding(){
        foreach(var (input,opcode,type) in new[]{("你好",136,0),("!你好",136,2),("&你好",40,3),("@你好",136,4),("#你好",136,11)}){
            var actual=PlayerChatProtocol.Encode(input);
            Check(actual.SequenceEqual(Packet(b=>{b.Write((byte)opcode);b.Write((byte)type);Text(b,"你好");})),"精確送出欄位與 padding："+input);
        }
        Check(PlayerChatProtocol.Encode("\"測試者 你好").SequenceEqual(Packet(b=>{b.Write((byte)184);Text(b,"測試者");Text(b,"你好");})),"C184 無 type，兩個 Big5 字串");
        Check(PlayerChatProtocol.ParseInput("/say !literal")==new PlayerChatInput(PlayerChatChannel.Normal,"!literal"),"一般頻道可逃脫命令前綴");
        foreach(string prefix in new[]{"","!","&","@","#","\"測試者 ","/say "}){
            Check(PlayerChatProtocol.Encode(prefix+new string('中',52)).Length%8==0,"接受 52 個中文字，不按 Big5 bytes 計長："+prefix);
            Reject(()=>PlayerChatProtocol.Encode(prefix+new string('中',53)),"拒絕 53 字："+prefix);
            Reject(()=>PlayerChatProtocol.Encode(prefix),"拒絕空正文："+prefix);
        }
        foreach(string bad in new[]{"\"測試者","\" 訊息","\"測\t試 文字","\"😀 文字","!a\0b","&a\nb","@😀","#   "})Reject(()=>PlayerChatProtocol.Encode(bad),"拒絕無效輸入："+JsonSerializer.Serialize(bad));
        Reject(()=>PlayerChatProtocol.Encode(new PlayerChatInput((PlayerChatChannel)99,"text")),"拒絕未知頻道");
        Reject(()=>PlayerChatProtocol.Encode(new PlayerChatInput(PlayerChatChannel.Whisper,"text",new string('x',53))),"對象長度本機上限");
    }
    static void Receiving(){
        using var c=new LoginConnection();
        var packets=new[]{
            (Local(0,"玩家: 一般"),PlayerChatChannel.Normal,"玩家: 一般",123u),
            (Local(2,"<玩家> 喊話",0),PlayerChatChannel.Shout,"<玩家> 喊話",0u),
            (Global(3,"[玩家] 全體"),PlayerChatChannel.Global,"[玩家] 全體",0u),
            (Global(4,"{玩家} 血盟"),PlayerChatChannel.Clan,"{玩家} 血盟",0u),
            (Global(11,"(玩家) 隊伍"),PlayerChatChannel.Party,"(玩家) 隊伍",0u),
            (Local(14,"(玩家) 聊天隊伍"),PlayerChatChannel.Party,"(玩家) 聊天隊伍",123u),
            (Incoming(),PlayerChatChannel.Whisper,"測試者 -> 我：你好",0u),
            (Global(9,"-> (測試者) 你好"),PlayerChatChannel.Whisper,"-> (測試者) 你好",0u)};
        foreach(var (bytes,channel,text,speaker) in packets){
            int revision=c.World.PlayerChatRevision;Feed(c,bytes);var line=c.World.PlayerChatLines.Last();
            Check(line.Channel==channel&&line.Text==text&&line.Speaker==speaker&&c.World.PlayerChatRevision==revision+1,"實際 Receive 分派："+text);
            Check(PlayerChatProtocol.FormatLine(line).StartsWith("["+PlayerChatProtocol.ChannelName(channel)+"] "),"頻道前綴："+text);
        }
        Feed(c,Global(9,"系統狀態"));
        Check(c.World.PlayerChatLines.Length==8&&c.World.LastServerMessage=="系統狀態","S243/type9 系統訊息仍進 Inventory");
        foreach(var bytes in new[]{Local(7,"unknown"),Global(12,"交易未納入"),LoginPackets.Pad([161,0])}){
            Check(!PlayerChatProtocol.TryDecode(bytes,out _),"不搶其他頻道／NPC opcode "+bytes[0]+"/"+bytes[1]);
        }
        int before=c.World.PlayerChatRevision;
        byte[][] malformed=[[81],[73,65],[243,3,65],Local(2,"<a> b")[..^1],Local(0,"a").Concat(new byte[8]).ToArray(),[81,0,1,0,0,0,0,1]];
        foreach(var bytes in malformed){
            bool rejected=false;try{Feed(c,bytes);}catch(ProtocolException){rejected=true;}
            Check(rejected&&c.World.PlayerChatRevision==before,"截斷／壞 padding 不追加歷史："+Convert.ToHexString(bytes));
        }
        Feed(c,Global(3,"清\n除\t控制字"));Check(c.World.PlayerChatLines.Last().Text=="清除控制字","顯示過濾控制字");
        for(int i=0;i<110;i++)Feed(c,Global(11,"line"+i));
        Check(c.World.PlayerChatLines.Length==100&&c.World.PlayerChatLines.First().Text=="line10","混合頻道歷史保留最近 100 筆");
        c.World.Reset();Check(c.World.PlayerChatLines.Length==0,"session reset 清除歷史");
    }
    static async Task Sending(){
        foreach(string input in new[]{"一般","!喊話","&全體","\"測試者 密語","@血盟","#隊伍"}){
            using var c=Seed();int sends=0;byte[]? actual=null;
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((bytes,current,admitted)=>{if(!current())return Task.FromResult(false);sends++;actual=bytes;admitted();return Task.FromResult(true);}));
            var stamp=c.PlayerChatSnapshot();
            Check(await c.SendPlayerChat(input,stamp.Generation,stamp.WorldEpoch,stamp.Actor)&&sends==1&&actual!.SequenceEqual(PlayerChatProtocol.Encode(input)),"實際 sender 使用頻道編碼："+input);
            Check(c.World.PlayerChatLines.Length==0,"沒有本機假回聲："+input);
            Check(!await c.SendPlayerChat(input,stamp.Generation,stamp.WorldEpoch,stamp.Actor)&&sends==1,"所有頻道保留重送節流："+input);
        }
        using(var c=Seed()){
            int sends=0;Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((_,_,_)=>{sends++;return Task.FromResult(true);}));
            var s=c.PlayerChatSnapshot();
            Check(!await c.SendPlayerChat("\"缺少正文",s.Generation,s.WorldEpoch,s.Actor)&&sends==0,"無效密語不進 sender");
            Check(!await c.SendPlayerChat("&文字",s.Generation+1,s.WorldEpoch,s.Actor)&&sends==0,"過期 session 拒絕");
            Check(!await c.SendPlayerChat("@文字",s.Generation,s.WorldEpoch+1,s.Actor)&&sends==0,"過期 world 拒絕");
            Check(!await c.SendPlayerChat("#文字",s.Generation,s.WorldEpoch,s.Actor+1)&&sends==0,"錯誤 actor 拒絕");
            Set(c.World,"OwnDead",true);Check(!await c.SendPlayerChat("!文字",s.Generation,s.WorldEpoch,s.Actor)&&sends==0,"死亡拒絕");
        }
        using(var c=Seed()){
            int sends=0;var release=new TaskCompletionSource<bool>();
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)(async(_,current,admitted)=>{await release.Task;if(!current())return false;sends++;admitted();return true;}));
            var s=c.PlayerChatSnapshot();var pending=c.SendPlayerChat("\"測試者 文字",s.Generation,s.WorldEpoch,s.Actor);
            Check(!await c.SendPlayerChat("&重複",s.Generation,s.WorldEpoch,s.Actor),"傳送中阻擋另一頻道");
            Feed(c,LoginPackets.Pad([206,4,0,0]));release.SetResult(true);
            Check(!await pending&&sends==0,"等候傳送時切換世界，不送舊密語");
        }
    }
    static void Ui(){
        using var panel=new PlayerHudChatPanel();panel.Size=new(720,260);panel.CreateControl();panel.PerformLayout();
        var input=Field<TextBox>(panel,"input");var send=Field<Button>(panel,"send");var history=Field<RichTextBox>(panel,"history");
        var channels=Enum.GetValues<PlayerChatChannel>();
        var lines=channels.Select((c,i)=>new PlayerChatLine(i+1,0,"文字"+i,c)).ToArray();
        panel.UpdateChat(new(1,1,123,1,true,"",lines));
        Check(channels.Select(c=>PlayerHudChatPanel.ChannelColor(c).ToArgb()).Distinct().Count()==6,"六頻道顏色互異");
        foreach(var line in lines){int start=history.Text.IndexOf(PlayerChatProtocol.FormatLine(line),StringComparison.Ordinal);history.Select(start,1);Check(start>=0&&history.SelectionColor.ToArgb()==PlayerHudChatPanel.ChannelColor(line.Channel).ToArgb(),"RichTextBox 實際行色："+line.Channel);}
        input.Text="\"測試者 "+new string('中',52);Check(send.Enabled,"UI 密語對象不占正文 52 字額度");
        input.Text="!"+new string('中',53);Check(!send.Enabled,"UI 阻擋超長正文");
        input.Text="\"測試者";Check(!send.Enabled,"UI 阻擋缺少密語正文");
        input.Text="&廣播";Check(send.Enabled&&Field<Label>(panel,"state").Text.Contains("全體"),"UI 即時顯示目標頻道");
        panel.UpdateChat(new(2,1,123,1,true,"",[]));Check(input.TextLength==0&&history.TextLength==0,"換 session 清除草稿與顯示");
        var enter=new PlayerChatEnterPress();enter.Open(true);Check(!enter.Down(Keys.None),"開啟聊天的 Enter 不送出");enter.Release();Check(enter.Down(Keys.None)&&!enter.Down(Keys.None),"下次 Enter 僅送一次");enter.Release();Check(!enter.Down(Keys.Control),"修飾鍵不送出");
    }
    [STAThread] static int Main(string[] args){try{
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(Path.GetFullPath(args[0]),"engine","build","offline-checks")+Path.DirectorySeparatorChar);
        using var guard=new SocketGuard();Encoding();Receiving();Sending().GetAwaiter().GetResult();Ui();
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{status="pass",checks=labels.Count,scope="合成封包／記憶體 sender／未顯示的聊天控制項；非真機",labels},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {labels.Count} player chat checks");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    sealed class SocketGuard:EventListener {
        protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}
        protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network forbidden in offline checks");
    }
}
