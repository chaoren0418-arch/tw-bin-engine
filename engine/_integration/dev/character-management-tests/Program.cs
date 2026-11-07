using P06;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics.Tracing;

// Synthetic packets, memory transport, built product DLL. No windows, sockets, game assets or credentials.
static class Checks {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly List<string> passed=[];
    static void Check(bool value,string label){if(!value)throw new Exception(label);passed.Add(label);}
    static void Set(object obj,string name,object? value){if(obj.GetType().GetProperty(name,Flags) is {} p)p.SetValue(obj,value);else obj.GetType().GetField(name,Flags)!.SetValue(obj,value);}
    static object? Get(object obj,string name)=>obj.GetType().GetField(name,Flags)!.GetValue(obj);
    static CharacterCreation Creation(byte type=0,byte sex=0,string name="合成測試"){
        byte[] values=CharacterManagementPackets.BaseStats(type);values[0]+=(byte)CharacterManagementPackets.AvailablePoints(type);
        return new(name,type,sex,values[0],values[1],values[2],values[3],values[4],values[5]);
    }
    static LoginCharacter Character(string name="原角色",byte type=0)=>new(name,"",type,0,0,100,20,10,1,21,10,10,11,13,10,0,20261107,0);
    static byte[] CharacterPacket(byte opcode,LoginCharacter c,bool wide=true){
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);
        void Text(string s){writer.Write(LoginPackets.TextEncoding.GetBytes(s));writer.Write((byte)0);}
        writer.Write(opcode);Text(c.Name);Text(c.Clan);writer.Write(c.Class);writer.Write(c.Sex);writer.Write(c.Lawful);
        if(wide){writer.Write(c.Hp);writer.Write(c.Mp);}else{writer.Write((ushort)c.Hp);writer.Write((ushort)c.Mp);}
        writer.Write(c.Ac);writer.Write(c.Level);writer.Write(c.Strength);writer.Write(c.Dexterity);writer.Write(c.Constitution);writer.Write(c.Wisdom);writer.Write(c.Charisma);writer.Write(c.Intelligence);writer.Write(c.Gm);writer.Write(c.CreateTime);writer.Write(c.CheckCode);
        return LoginPackets.Pad(memory.ToArray());
    }
    sealed class Session : IDisposable {
        public readonly LoginConnection Connection=new();
        public readonly List<byte[]> Sent=[];
        public readonly Func<byte[],Func<bool>,Action,Task<bool>> Transport;
        long sequence;
        public Session(bool inWorld=false){
            Set(Connection,"State",inWorld?LoginState.InWorld:LoginState.CharacterList);Set(Connection,"Slots",8);Set(Connection,"Expected",1);
            ((List<LoginCharacter>)Get(Connection,"characters")!).Add(Character());
            Transport=(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);admit();Sent.Add(bytes);return Task.FromResult(true);};
            Set(Connection,"sendMovementGate",Transport);Set(Connection,"sendWorld",(Func<byte[],Task>)(bytes=>{Sent.Add(bytes);return Task.CompletedTask;}));
            if(inWorld){Set(Connection.World,"Actor",new OwnActor(1,2,3,0,0,0,0,0,0,0,"原角色","",0,0,"","",0,100,0,1,0,255,255));Set(Connection,"worldSelection","原角色");}
        }
        public Task<bool> Feed(byte[] packet,bool wide=true)=>(Task<bool>)typeof(LoginConnection).GetMethod("ReceiveCharacterManagement",Flags)!.Invoke(Connection,[packet,LoginProfile.Select(wide?"jar-d287-client2":"jar-d287-client1"),++sequence])!;
        public Task<LoginAction> Create(CharacterCreation? creation=null)=>Connection.CreateCharacter(creation??Creation(),Connection.SessionGeneration,Connection.Revision);
        public Task<LoginAction> Delete(string name="原角色",bool confirm=true)=>Connection.DeleteCharacter(name,confirm,Connection.SessionGeneration,Connection.Revision);
        public Task<LoginAction> Switch()=>Connection.SwitchCharacter(Connection.SessionGeneration,Connection.Revision);
        public void Dispose()=>Connection.Dispose();
    }
    static async Task RejectPacket(Session session,byte[] packet,string label){try{await session.Feed(packet);throw new Exception("accepted invalid packet: "+label);}catch(ProtocolException){Check(true,label);}}
    static void Rules(){
        byte[][] expectedBases=[[13,10,10,11,13,10],[16,12,14,9,12,8],[11,12,12,12,9,12],[8,7,12,12,8,12],[12,15,8,10,9,11],[13,11,14,12,8,11],[11,10,12,12,8,12]];
        int[] points=[8,4,7,16,10,6,10];
        for(byte type=0;type<7;type++){
            Check(CharacterManagementPackets.BaseStats(type).SequenceEqual(expectedBases[type]),$"職業 {type} 六項基礎能力");
            Check(CharacterManagementPackets.AvailablePoints(type)==points[type],$"職業 {type} 可分配點數");
            for(byte sex=0;sex<2;sex++){
                var request=Creation(type,sex,"測試A");var bytes=CharacterManagementPackets.Create(request,0,8);var r=new LoginReader(bytes);
                Check(r.C()==84&&r.S()=="測試A"&&r.C()==type&&r.C()==sex,$"C84 職業 {type}／性別 {sex} 欄位順序");
                for(int i=0;i<6;i++)Check(r.C()==request.Stats[i],$"C84 能力欄位 {type}/{sex}/{i}");r.Finish();
                Check(CharacterManagementPackets.Validate(request with{Strength=(byte)(request.Strength-1)},0,8)!=null,"能力總和不足拒絕");
                Check(CharacterManagementPackets.Validate(request with{Dexterity=(byte)(expectedBases[type][1]-1),Strength=(byte)(request.Strength+1)},0,8)!=null,"低於職業基礎拒絕");
            }
        }
        foreach(string name in new[]{"","　 ","1234567890123","中文名字超過","A!","A\\","あ","丶","\uE6C1","😀","A\0B"})Check(CharacterManagementPackets.Validate(Creation(name:name),0,8)!=null,"無效名稱拒絕 "+name.Length);
        foreach(string name in new[]{"123456789012","中文名字五","中文名字五AB","玩家_1"})Check(CharacterManagementPackets.Validate(Creation(name:name),0,8)==null,"合法 Big5 邊界名稱");
        Check(CharacterManagementPackets.NormalizeName(" 合　成\t名 ")=="合成名","與伺服器一致的空白正規化");
        Check(CharacterManagementPackets.Validate(Creation() with{Class=7},0,8)!=null&&CharacterManagementPackets.Validate(Creation() with{Sex=2},0,8)!=null,"無效職業／性別拒絕");
        Check(CharacterManagementPackets.Validate(Creation(),8,8)!=null,"滿欄位拒絕");
        Check(CharacterManagementPackets.Switch().SequenceEqual(new byte[]{7,0,0,0,0,0,0,0}),"C7 只有 opcode 與 padding");
        var delete=new LoginReader(CharacterManagementPackets.Delete("原角色"));Check(delete.C()==162&&delete.S()=="原角色","C162 傳名稱而非物件 ID");delete.Finish();
    }
    static async Task CreateFlows(){
        foreach(bool wide in new[]{false,true}){
            using var s=new Session();int generation=s.Connection.SessionGeneration;
            Check((await s.Create()).Accepted&&s.Sent.Count==1&&s.Connection.State==LoginState.ManagingCharacters,"建立請求進入等待");
            Check(!(await s.Create()).Accepted&&!(await s.Delete()).Accepted&&!(await s.Switch()).Accepted&&s.Sent.Count==1,"等待建立時拒絕重複及跨操作");
            try{await s.Connection.SelectCharacter("原角色");throw new Exception("entered during create");}catch(InvalidOperationException){Check(true,"建立中不能進世界");}
            await s.Feed(LoginPackets.Pad([98,2]));Check(s.Connection.Characters.Count==1&&s.Connection.State==LoginState.ManagingCharacters,"S98 成功不預先插入角色");
            await s.Feed(CharacterPacket(127,Character("合成測試"),wide),wide);
            Check(s.Connection.State==LoginState.CharacterList&&s.Connection.Characters.Count==2&&s.Connection.Expected==2,"S127 寬／窄生命值新增權威角色");
            Check(s.Connection.SessionGeneration==generation&&ReferenceEquals(Get(s.Connection,"sendMovementGate"),s.Transport),"建立保持同連線世代及傳輸器");
        }
        foreach(byte reason in new byte[]{6,9,21,255}){
            using var s=new Session();await s.Create();await s.Feed(LoginPackets.Pad([98,reason]));
            Check(s.Connection.State==LoginState.CharacterList&&s.Connection.Characters.Count==1&&!s.Connection.CharacterManagementPending,"建立拒絕不更動清單且解除等待");
        }
        using(var s=new Session()){await s.Create();await RejectPacket(s,CharacterPacket(127,Character("合成測試")),"拒絕缺少 S98 的 S127");}
        using(var s=new Session()){await s.Create();await s.Feed(LoginPackets.Pad([98,2]));await RejectPacket(s,CharacterPacket(127,Character("錯誤名稱")),"拒絕不相符的新角色");}
        using(var s=new Session()){await s.Create();await RejectPacket(s,[98],"拒絕截斷建立結果");}
        using(var s=new Session()){await s.Create();await RejectPacket(s,[98,2,1,0,0,0,0,0],"拒絕非零 padding");}
        using(var s=new Session()){Check(!(await s.Connection.CreateCharacter(Creation(),99,0)).Accepted&&s.Sent.Count==0,"舊 session 建立不送出");Check(!(await s.Connection.CreateCharacter(Creation(),0,99)).Accepted&&s.Sent.Count==0,"舊 revision 建立不送出");}
    }
    static async Task DeleteFlows(){
        using(var s=new Session()){Check(!(await s.Delete(confirm:false)).Accepted&&!(await s.Delete("其他帳號角色")).Accepted&&s.Sent.Count==0,"取消確認／非清單角色皆不送出");}
        using(var s=new Session()){Check(!(await s.Connection.DeleteCharacter("原角色",true,0,99)).Accepted&&s.Sent.Count==0,"過期刪除確認不送出");}
        foreach(byte result in new byte[]{5,81}){
            using var s=new Session();await s.Delete();Check(s.Connection.Characters.Count==1,"刪除不樂觀移除角色");
            await s.Feed(LoginPackets.Pad([6,result]));Check(s.Sent.Count==2&&s.Sent[0][0]==162&&s.Sent[1][0]==16,"刪除成功後只送一次 C16 刷新");
            await s.Feed(LoginPackets.Pad([178,(byte)(result==5?0:1),8]));
            if(result==81){Check(s.Connection.State==LoginState.ManagingCharacters,"七日刪除等待完整清單");await s.Feed(CharacterPacket(93,Character(type:32)));}
            Check(s.Connection.State==LoginState.CharacterList&&s.Connection.Characters.Count==(result==5?0:1),"立即刪除／七日待刪依權威清單呈現");
            if(result==81){Check(CharacterManagementPackets.PendingDeletion(s.Connection.Characters[0]),"七日待刪有明確標示");await s.Delete();await s.Feed(LoginPackets.Pad([6,81]));await s.Feed(LoginPackets.Pad([178,1,8]));await s.Feed(CharacterPacket(93,Character()));Check(!CharacterManagementPackets.PendingDeletion(s.Connection.Characters[0]),"再次 C162 取消七日待刪");}
        }
        using(var s=new Session()){await s.Delete();await s.Feed(LoginPackets.Pad([6,99]));Check(s.Connection.Characters.Count==1&&s.Sent.Count==1&&s.Connection.CharacterNotice.Contains("未確認"),"未知刪除結果保留角色");}
        using(var s=new Session()){await s.Delete();await s.Feed(LoginPackets.Pad([6,5]));await RejectPacket(s,LoginPackets.Pad([178,9,8]),"拒絕超過槽位的清單");}
        using(var s=new Session()){await s.Delete();await s.Feed(LoginPackets.Pad([6,5]));await s.Feed(LoginPackets.Pad([178,2,8]));await s.Feed(CharacterPacket(93,Character()));await RejectPacket(s,CharacterPacket(93,Character()),"刷新清單拒絕重名");Check(s.Connection.Characters.Count==1,"不發布部分刷新清單");}
    }
    static async Task SwitchFlows(){
        using(var s=new Session(true)){
            int session=s.Connection.SessionGeneration,world=s.Connection.World.WorldEpoch;object sender=Get(s.Connection,"sendWorld")!;
            Check((await s.Switch()).Accepted&&s.Sent.Count==1&&s.Sent[0][0]==7,"同連線切角只送 C7");
            Check(s.Connection.State==LoginState.SwitchingCharacter&&s.Connection.World.Actor==null&&s.Connection.World.WorldEpoch>world,"切角退役舊世界及身分");
            Check(!(await s.Switch()).Accepted&&!(await s.Create()).Accepted,"切角中拒絕其他操作");
            Check(await s.Feed(LoginPackets.Pad([123,1,0,0,0]))&&s.Connection.World.ServerTime==null,"切角等待期間不套用舊世界封包");
            await s.Feed(LoginPackets.Pad([250,42]));Check(s.Connection.State==LoginState.ManagingCharacters,"S250/42 才接受新清單");
            await s.Feed(LoginPackets.Pad([178,2,8]));await s.Feed(CharacterPacket(93,Character()));Check(s.Connection.State==LoginState.ManagingCharacters,"切角清單未齊禁止選角");
            await s.Feed(CharacterPacket(93,Character("另一角色",1)));
            Check(s.Connection.State==LoginState.CharacterList&&s.Connection.SessionGeneration==session&&ReferenceEquals(Get(s.Connection,"sendWorld"),sender)&&s.Sent.Count==1,"完整新清單保持原 session／sender，無重連無 C16");
            await s.Connection.SelectCharacter("另一角色");Check(s.Sent.Count==2&&s.Sent[1][0]==137&&s.Connection.State==LoginState.EnteringWorld,"原連線 C137 進入另一角色");
            Check(!await s.Feed(LoginPackets.Pad([223,3,21,139,123,148,240,47])),"新世界回到既有分派");
        }
        using(var s=new Session(true)){await s.Switch();await RejectPacket(s,LoginPackets.Pad([178,0,8]),"拒絕切角確認前的清單");}
        using(var s=new Session(true)){await s.Switch();await RejectPacket(s,[250,42,0],"拒絕截斷切角確認");}
        using(var s=new Session(true)){await s.Switch();await RejectPacket(s,LoginPackets.Pad([227]),"切角收到斷線封包失敗");}
        using(var s=new Session(true)){await s.Switch();await s.Feed(LoginPackets.Pad([250,42]));await s.Feed(LoginPackets.Pad([178,0,8]));Check(s.Connection.State==LoginState.CharacterList&&s.Connection.Characters.Count==0&&s.Connection.CanCreateCharacter,"切角空清單仍可建立角色");}
        using(var s=new Session(true)){Set(s.Connection,"worldProbeSending",true);Check(!(await s.Switch()).Accepted&&s.Sent.Count==0,"心跳尚未送完時不切角，避免排隊舊探測");}
    }
    static async Task Races(){
        using(var s=new Session()){
            s.Connection.Changed+=()=>{if(s.Connection.State==LoginState.ManagingCharacters)s.Connection.Cancel();};
            Check(!(await s.Create()).Accepted&&s.Sent.Count==0&&Get(s.Connection,"characterDeadline")==null,"通知回呼同步取消不建立殘留 deadline");
        }
        using(var s=new Session()){
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Set(s.Connection,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)(async(bytes,valid,admit)=>{await release.Task;return await s.Transport(bytes,valid,admit);}));
            var pending=s.Create();s.Connection.Cancel();release.SetResult();Check(!(await pending).Accepted&&s.Sent.Count==0&&s.Connection.State==LoginState.Cancelled,"排隊建立被取消後不得寫入新連線");
            Check(!await s.Feed(LoginPackets.Pad([98,2]))&&s.Connection.Characters.Count==0,"取消後不消費舊建立回包");
        }
        using(var s=new Session(true)){
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Set(s.Connection,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)(async(bytes,valid,admit)=>{await release.Task;throw new IOException("synthetic");}));
            var pending=s.Switch();s.Connection.Cancel();release.SetResult();await pending;Check(s.Connection.State==LoginState.Cancelled,"舊傳送例外不得改寫取消狀態");
        }
        using(var s=new Session()){Set(s.Connection,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((_,_,_)=>throw new IOException("synthetic")));Check(!(await s.Create()).Accepted&&s.Connection.State==LoginState.Faulted,"傳送失敗關閉連線且不重送");}
        using(var s=new Session()){
            Set(s.Connection,"CharacterManagementTimeoutMs",30);await s.Create();await Task.Delay(150);
            Check(s.Connection.State==LoginState.Faulted&&s.Sent.Count==1&&s.Connection.Characters.Count==0,"缺回包逾時關閉，不自動重送破壞性請求");
        }
        using(var s=new Session()){
            Set(s.Connection,"CharacterManagementTimeoutMs",30);await s.Create();await s.Feed(LoginPackets.Pad([98,6]));await Task.Delay(150);
            Check(s.Connection.State==LoginState.CharacterList,"完成操作取消舊 deadline");
        }
    }
    static async Task Main(string[] args){
        using var guard=new SocketGuard();
        Rules();await CreateFlows();await DeleteFlows();await SwitchFlows();await Races();
        string output=args.Length==0?"scratch/issue-195/checks.json":args[0];Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(new{status="pass",checks=passed.Count,synthetic=true,gameStarted=false,network=false,database=false,labels=passed},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS character management: {passed.Count} checks; synthetic, no game/network/database");
    }
    sealed class SocketGuard:EventListener {
        protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}
        protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network forbidden in character management checks");
    }
}
