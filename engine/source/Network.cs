using System.Net;
using System.Net.Sockets;
using System.Text.Json;
namespace P06;
public sealed class WireLog:IDisposable {
    readonly StreamWriter? writer;readonly object gate=new();
    public WireLog(string? folder){if(folder!=null){Directory.CreateDirectory(folder);writer=new(Path.Combine(folder,"client-network.jsonl"),false){AutoFlush=true};}}
    public void Add(string kind,object? data=null){lock(gate)writer?.WriteLine(JsonSerializer.Serialize(new{kind,utc=DateTimeOffset.UtcNow,monotonic=System.Diagnostics.Stopwatch.GetTimestamp(),pid=Environment.ProcessId,data}));}
    public void Dispose(){lock(gate)writer?.Dispose();}
}
public sealed class HeadingConnection:IDisposable {
    readonly WireLog log;readonly int timeoutMs;
    TcpClient? socket;CancellationTokenSource? lifetime;HeadingCodec codec=new();Frames frames=new();
    TaskCompletionSource<int>? pending;int? requested;int epoch;bool disposed;
    public string State {get;private set;}="Disconnected";
    public string Detail {get;private set;}="fixture 尚未連線";
    public bool Ready=>State=="Ready";
    public bool Busy=>pending!=null;
    public int Confirmed {get;private set;}
    public int Generation=>epoch;
    public event Action? Changed;
    public event Action<int,uint>? Applied;
    public Task ReaderTask {get;private set;}=Task.CompletedTask;
    // Construct and call on the WinForms UI synchronization context; async continuations retain it.
    public HeadingConnection(WireLog log,int timeoutMs=1500){this.log=log;this.timeoutMs=timeoutMs;}
    void Status(string state,string detail){State=state;Detail=detail;log.Add("status",new{state,detail,epoch});Changed?.Invoke();}
    public async Task Connect(int port) {
        if(disposed)throw new ObjectDisposedException(nameof(HeadingConnection));
        Disconnect("reset before connect");int run=epoch;codec=new();frames=new();Confirmed=0;
        if(port is <1 or >65535){Status("Faulted","invalid loopback port");return;}
        lifetime=new();var token=lifetime.Token;var s=new TcpClient(AddressFamily.InterNetwork){NoDelay=true};socket=s;Status("Connecting",$"127.0.0.1:{port}");
        try {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(timeoutMs);
            await s.ConnectAsync(IPAddress.Loopback,port,deadline.Token);
            if(run!=epoch)return;
            log.Add("connected",new{epoch,local=s.Client.LocalEndPoint?.ToString(),remote=s.Client.RemoteEndPoint?.ToString(),profile=HeadingCodec.Profile,key=codec.KeyHex,objectId=HeadingCodec.ObjectId});
            Status("Ready","已初始化 fixture；等待 1–8 轉向");ReaderTask=Read(s,run,token);
        }catch(Exception ex) when(ex is SocketException or OperationCanceledException or ObjectDisposedException){if(run==epoch)Fail(ex.Message);}
    }
    async Task Read(TcpClient s,int run,CancellationToken token) {
        try {
            byte[] b=new byte[64];while(true){int n=await s.GetStream().ReadAsync(b,token);if(run!=epoch)return;
                if(n==0){frames.Eof();throw new ProtocolException("peer disconnected");}
                byte[] received=b[..n];log.Add("wire-read",new{epoch=run,raw=Convert.ToHexString(received).ToLowerInvariant()});
                // UI synchronization context owns the codec, pending state and model callback.
                frames.Feed(received,packet=>Accept(packet,run));
            }
        }catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or ProtocolException){if(run==epoch&&!token.IsCancellationRequested)Fail(ex.Message);}
    }
    void Accept(byte[] packet,int run) {
        if(run!=epoch)return;if(pending==null)throw new ProtocolException("state order: unsolicited heading without pending request");
        var result=codec.Decode(packet);if(result.Direction!=requested)throw new ProtocolException("state order: response direction differs from pending request");
        uint sequence=codec.NextSequence-1;log.Add("decoded",new{epoch,sequence,raw=Convert.ToHexString(packet).ToLowerInvariant(),result.ObjectId,result.Direction,result.Plaintext,permutation=codec.PermutationHash});
        Confirmed++;var completion=pending;pending=null;requested=null;Applied?.Invoke(result.Direction,sequence);Status("Ready",$"已確認 #{sequence} D{result.Direction}");completion.TrySetResult(result.Direction);
    }
    public async Task<bool> Request(int direction) {
        if(!Ready||Busy){log.Add("request-rejected",new{State,Busy,direction});return false;}
        if(direction is <0 or >7){log.Add("request-rejected",new{direction,error="direction outside 0..7"});return false;}
        int run=epoch;var token=lifetime!.Token;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);requested=direction;var completion=pending;
        try {
            var raw=codec.Encode(direction);uint seq=codec.NextSequence-1;log.Add("request",new{epoch,sequence=seq,direction,raw=Convert.ToHexString(raw).ToLowerInvariant(),keyAfter=codec.KeyHex});Changed?.Invoke();
            // Deliberately segmented writes exercise the independent server's stream assembler.
            foreach(byte b in raw){await socket!.GetStream().WriteAsync(new byte[]{b},token);log.Add("wire-write",new{epoch=run,sequence=seq,raw=b.ToString("x2")});}
            await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs),token);return run==epoch;
        }catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or TimeoutException or ObjectDisposedException or ProtocolException){if(run==epoch)Fail(ex is TimeoutException?"response timeout":ex.Message);return false;}
    }
    void Fail(string message){Disconnect(message);Status("Faulted",message);}
    public void Disconnect(string reason="user cancel") {
        epoch++;lifetime?.Cancel();socket?.Dispose();socket=null;lifetime?.Dispose();lifetime=null;pending?.TrySetCanceled();pending=null;requested=null;frames=new();Status("Disconnected",reason);
    }
    public void Dispose(){if(disposed)return;Disconnect("form closing");disposed=true;}
}
