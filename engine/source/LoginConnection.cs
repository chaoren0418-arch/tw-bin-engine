using System.Net;
using System.Net.Sockets;
namespace P06;

public enum LoginState { Offline,Connecting,AwaitSeed,Authenticating,AwaitList,ReadingCharacters,CharacterList,EnteringWorld,InWorld,ManagingCharacters,SwitchingCharacter,Rejected,Disconnected,Cancelled,Faulted }
public sealed partial class LoginConnection:IDisposable,ILoginSession {
    readonly object worldCaptureGate=new();long receivedPacketSequence;public long AppliedPacketSequence {get;private set;}
    public T CaptureWorld<T>(Func<T> capture){lock(worldCaptureGate)return capture();}
    CancellationTokenSource? cancellation;TcpClient? socket;int generation;
    public int SessionGeneration=>generation;
    public LoginState State {get;private set;}=LoginState.Offline;
    public string Detail {get;private set;}="離線資產預覽";
    public string ProfileId {get;private set;}="none";
    public event Action<object>? PacketReceived;
    public event Action<object>? WorldPacketSent;
    public string News {get;private set;}="";
    public int Expected {get;private set;}
    public int Slots {get;private set;}
    public int Revision {get;private set;}
    public IReadOnlyList<LoginCharacter> Characters=>characters.ToArray();
    readonly List<LoginCharacter> characters=[];
    public event Action? Changed;
    public object Snapshot()=>new {state=State.ToString(),detail=Detail,profile=ProfileId,news=News,newsPending=NewsPending,expected=Expected,slots=Slots,characters=Characters,revision=Revision,appliedPacketSequence=AppliedPacketSequence,world=World.Snapshot(),worldEntry=EntrySnapshot(),decodeFailure=LastWorldDecodeFailure,entryFailure=LastWorldEntryFailure,serverFeatures=ServerFeaturesSnapshot(),scope=ProfileId=="p24-real-jar-d287-client2"?"P24 independent real JAR + MySQL world initialization":"loopback reference / offline"};
    void Set(LoginState state,string detail){State=state;Detail=detail;Revision++;Changed?.Invoke();}
    public void Cancel(bool offline=false){ResetCharacterManagement();ResetServerFeatures();restartSending=false;ResetPlayerChatSend();generation++;ResetPrivateShopSend();ResetBonusStatsSend();ResetWorldEntry();ResetTeleportSend();ResetShipSend();tradeSendLease++;tradeSending=false;interactionSending=false;ResetMovementSend();ResetCombatSend();ResetMagicSend();ResetItemSend();ResetGroundSend();World.Reset();worldSelection=null;sendWorld=null;sendMovementGate=null;var previous=cancellation;cancellation=null;previous?.Cancel();socket?.Dispose();socket=null;characters.Clear();Expected=Slots=0;News="";news.Reset();Set(offline?LoginState.Offline:LoginState.Cancelled,offline?"離線資產預覽":"使用者取消；連線已關閉");}
    public async Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default){
        Cancel();LastWorldDecodeFailure=null;LastWorldEntryFailure=null;receivedPacketSequence=AppliedPacketSequence=0;int epoch=++generation;using var cts=CancellationTokenSource.CreateLinkedTokenSource(token);cancellation=cts;
        using var client=new TcpClient(AddressFamily.InterNetwork);socket=client;
        try {
            // Task scope intentionally forbids connecting to existing services or arbitrary hosts.
            if(port<42061||port>42063)throw new ArgumentException("P24 owned loopback ports are 42061..42063");
            if(profileId!="p24-real-jar-d287-client2"&&port==42061)throw new ArgumentException("Synthetic profiles require 42062/42063");
            var profile=LoginProfile.Select(profileId);ProfileId=profile.Id;World.CacheEnabled=profileId=="p24-real-jar-d287-client2";World.AsyncMapLoading=World.CacheEnabled;
            if(profileId=="p24-real-jar-d287-client2"&&port!=42061)throw new ArgumentException("P24 real mode requires owned port42061");
            byte[] login=LoginPackets.Login(account,password);LoginCipher? outgoing=null,incoming=null;var frames=new LoginFrames();bool newsSeen=false;
            Set(LoginState.Connecting,"127.0.0.1:"+port);
            await client.ConnectAsync(IPAddress.Loopback,port,cts.Token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs),cts.Token);
            if(epoch!=generation)return;Set(LoginState.AwaitSeed,"等待已識別 profile 首包");var stream=client.GetStream();
            var sendLock=new SemaphoreSlim(1,1);
            long sentSequence=0;
            void Sent(byte[] bytes)=>WorldPacketSent?.Invoke(new{sequence=++sentSequence,utc=DateTimeOffset.UtcNow,monotonicMs=Environment.TickCount64,plainHex=Convert.ToHexString(bytes)});
            sendWorld=async bytes=>{await sendLock.WaitAsync(cts.Token);try{if(epoch!=generation)throw new OperationCanceledException();PacketTrace.Out(bytes);await stream.WriteAsync(LoginFrames.Wrap(outgoing!.Transform(bytes,true)),cts.Token);Sent(bytes);}finally{sendLock.Release();}};
            sendMovementGate=async (bytes,valid,admit)=>{await sendLock.WaitAsync(cts.Token);try{if(epoch!=generation||!valid())return false;admit();PacketTrace.Out(bytes);await stream.WriteAsync(LoginFrames.Wrap(outgoing!.Transform(bytes,true)),cts.Token);Sent(bytes);return true;}finally{sendLock.Release();}};
            var heartbeat=WorldHeartbeat(epoch,cts.Token);
            byte[] buffer=new byte[4096];
            // A whole phase has a deadline; partial byte trickles cannot keep it alive forever.
            long deadline=Environment.TickCount64+timeoutMs;bool uiContext=SynchronizationContext.Current is WindowsFormsSynchronizationContext;int slicePackets=0;long sliceAt=Environment.TickCount64;
            while(!cts.IsCancellationRequested){
                int n;
                if(CharacterManagementRead||State is LoginState.CharacterList or LoginState.EnteringWorld or LoginState.InWorld)n=await stream.ReadAsync(buffer,cts.Token);
                else {long left=deadline-Environment.TickCount64;if(left<=0)throw new TimeoutException("login phase timeout");n=await stream.ReadAsync(buffer,cts.Token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(left),cts.Token);}
                if(epoch!=generation)return;
                if(n==0){frames.End();if(!CharacterManagementRead&&State is not (LoginState.CharacterList or LoginState.EnteringWorld or LoginState.InWorld))throw new ProtocolException("disconnected before complete character list");World.Reset();characters.Clear();Set(LoginState.Disconnected,"對端已斷線；已清除本次資料");return;}
                foreach(byte[] body in frames.Feed(buffer.AsSpan(0,n).ToArray())){
                    if(uiContext&&(slicePackets>=8||Environment.TickCount64-sliceAt>=16)){await Task.Delay(1,cts.Token);slicePackets=0;sliceAt=Environment.TickCount64;}slicePackets++;
                    if(epoch!=generation)return;
                    if(State==LoginState.AwaitSeed){PacketReceived?.Invoke(new{kind="seed",bodyHex=Convert.ToHexString(body)});uint seed=LoginPackets.Seed(body);outgoing=new(seed);incoming=new(seed);Set(LoginState.Authenticating,"首包已解析；等待登入結果");await SendC14(async c14=>{PacketTrace.Out(c14);await stream.WriteAsync(LoginFrames.Wrap(outgoing.Transform(c14,true)),cts.Token);},cts.Token);PacketTrace.Out(login);await stream.WriteAsync(LoginFrames.Wrap(outgoing.Transform(login,true)),cts.Token);Array.Clear(login);deadline=Environment.TickCount64+timeoutMs;continue;}
                    if(incoming==null)throw new ProtocolException("packet before seed");byte[] plain=incoming.Transform(body,false);long sequence=++receivedPacketSequence;
                    // T233：S139 只回應 C14（3.81 不要求）；登入期任何階段收到都只保存旗標，不改狀態、不影響登入。進世界後才到的由 WorldSession 擴充點處理。
                    if(State is not (LoginState.EnteringWorld or LoginState.InWorld)&&plain.Length>0&&plain[0]==ServerFeatures.S139Opcode){PacketTrace.In(plain,sequence);PacketReceived?.Invoke(new{kind="server-packet",sequence,plainHex=Convert.ToHexString(plain)});AcceptServerVersion(plain);continue;}
                    if(await ReceiveCharacterManagement(plain,profile,sequence))continue;
                    if(State is LoginState.EnteringWorld or LoginState.InWorld){
                        lock(worldCaptureGate){
                            if(epoch!=generation)return;PacketTrace.In(plain,sequence);PacketReceived?.Invoke(new{kind="server-packet",sequence,plainHex=Convert.ToHexString(plain)});
                            if(epoch!=generation)return;long reportsBefore=World.AttackReports;ApplyWorldPacket(plain,worldSelection!,sequence);AdoptLateServerFeatures();ApplyCombatAcknowledgement(reportsBefore);AppliedPacketSequence=sequence;if(World.TradeCancelRequired)_=CancelChangedTrade();if(World.TeleportAckPending)_=AcknowledgeTeleport();
                            StartPendingMapLoad(epoch,cts.Token);PublishWorldProgress();
                        }continue;
                    }
                    PacketTrace.In(plain,sequence);PacketReceived?.Invoke(new{kind="server-packet",sequence,plainHex=Convert.ToHexString(plain)});
                    var r=new LoginReader(plain);byte opcode=r.C();
                    if(State==LoginState.Authenticating&&opcode==233){ushort reason=r.H();r.D();r.D();r.D();r.Finish();if(reason!=0){Set(LoginState.Rejected,$"伺服器拒絕：0x{reason:X4}");return;}Set(LoginState.AwaitList,"登入結果成功；等待角色數量");deadline=Environment.TickCount64+timeoutMs;}
                    else if(State==LoginState.AwaitList&&opcode==48&&!newsSeen){News=LoginNews.Parse(plain);newsSeen=true;
                        if(NewsConfirmationRequired){var (_,confirmed)=news.Begin();Set(State,"已收到公告；等待確認");await confirmed.WaitAsync(cts.Token);if(epoch!=generation)return;Set(State,"公告已確認");}
                        else Set(State,"已解析公告；自動確認（本機測試政策）");
                        deadline=Environment.TickCount64+timeoutMs;byte[] acknowledgement=LoginNews.Acknowledgement();PacketTrace.Out(acknowledgement);await stream.WriteAsync(LoginFrames.Wrap(outgoing!.Transform(acknowledgement,true)),cts.Token);}
                    else if(State==LoginState.AwaitList&&opcode==178){Expected=r.C();Slots=r.C();r.Finish();if(Expected>Slots)throw new ProtocolException("character count exceeds slots");Set(Expected==0?LoginState.CharacterList:LoginState.ReadingCharacters,$"收到角色數量 {Expected}/{Slots}");deadline=Environment.TickCount64+timeoutMs;}
                    else if(State==LoginState.ReadingCharacters&&opcode==93){var item=LoginPackets.Character(r,profile);if(characters.Any(c=>c.Name==item.Name))throw new ProtocolException("duplicate character name");characters.Add(item);Set(characters.Count==Expected?LoginState.CharacterList:State,$"已解析角色 {characters.Count}/{Expected}");}
                    else throw new ProtocolException($"unexpected opcode 0x{opcode:X2} in {State}");
                }
                // A synchronous socket backlog must yield the WinForms message loop to paint/timers/cancel.
                // This is scheduler fairness per receive buffer, independent of movement/attack cadence.
                if(uiContext){await Task.Delay(1,cts.Token);slicePackets=0;sliceAt=Environment.TickCount64;}
            }
        }catch(OperationCanceledException) when(cts.IsCancellationRequested){if(epoch==generation){World.Reset();characters.Clear();Expected=Slots=0;News="";Set(LoginState.Cancelled,"連線已取消");}}
        catch(Exception ex){if(epoch==generation){World.Reset();characters.Clear();Set(LoginState.Faulted,ex.GetType().Name+": "+ex.Message);}}
        finally {cts.Cancel();if(epoch==generation){news.Reset();ResetCharacterManagement();socket=null;cancellation=null;sendWorld=null;sendMovementGate=null;}client.Dispose();}
    }
    public void Dispose(){Cancel();cancellation=null;}
    // T260（#621，R2-Q）：S48 S_NEWS。PBW§5.3：原版以通用模態訊息框 sub_8E1E30 顯示，按確定（回呼 sub_ECA950，推定）才回 C16。
    // NewsConfirmationRequired=false（預設，探針／自動化測試）維持舊行為：收下就自動回 C16。
    readonly LoginNews news=new();
    public bool NewsConfirmationRequired {get;set;}
    public bool NewsPending=>news.Pending;
    public int NewsRevision=>news.Revision;
    /// <summary>使用者按下公告框的確定。只有公告正等待確認時才有效；回傳是否接受。</summary>
    public bool ConfirmNews(int revision)=>news.Confirm(revision);
}

/// <summary>S48 登入公告的確認閘門：收到公告後等使用者確認，確認後呼叫端才送 C16。</summary>
public sealed class LoginNews {
    readonly object gate=new();TaskCompletionSource? pending;
    public bool Pending {get{lock(gate)return pending!=null;}}
    /// <summary>每收到一則需要確認的公告就加 1；確認時必須帶顯示時的值，舊框的確定不會誤確認新公告。</summary>
    public int Revision {get;private set;}
    public static string Parse(byte[] plain){var r=new LoginReader(plain);if(r.C()!=48)throw new ProtocolException("not S48");string text=r.S();r.Finish();return text;}
    public static byte[] Acknowledgement()=>LoginPackets.Pad([16]);
    /// <summary>開始等待確認；回傳 (revision, 等待確認的工作)。</summary>
    public (int revision,Task confirmed) Begin(){lock(gate){pending?.TrySetCanceled();pending=new(TaskCreationOptions.RunContinuationsAsynchronously);return (++Revision,pending.Task);}}
    public bool Confirm(int revision){lock(gate){if(pending==null||revision!=Revision)return false;var p=pending;pending=null;p.TrySetResult();return true;}}
    public void Reset(){lock(gate){var p=pending;pending=null;p?.TrySetCanceled();}}
}
