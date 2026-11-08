namespace P06;

// T233（#551）：protocol-gaps E（docs/reference/protocol-gaps.md 第 4、8 節）。
// 原版 TW16 在 S_KEY 後送 C_VERSION（op 122 `chdcddc`），收 S_VERSION_CHECK（op 107，`sub_94DBB0`）保存語系與
// 伺服器功能旗標 dword_13CE514。3.81 對應 C14（C_ServerVersion，不讀欄位）／S139（S_ServerVersion）。
// 3.81 不要求這組封包：送出失敗、沒有回應或回應格式不符都只記錄，不影響登入流程。
// 本檔只做協定與資料；拾取節流等行為改跟旗標走需另開任務（Ground.cs 不在本任務範圍）。
public sealed record ServerFeatures(byte Status,byte ServerNo,byte LanguageCode,int CodePage,uint Flags,uint Uptime,int TrailingBytes) {
    public const byte C14Opcode=14,S139Opcode=139;
    /// <summary>TW16 C_VERSION 的第一個欄位 H(52)。</summary>
    public const ushort ClientVersionField=52;
    /// <summary>功能旗標 0x10000：拾取節流 600 ms（否則 986 ms）。3.81 的 0x77d82 有這一位（已驗證，PG§4）。</summary>
    public const uint FastPickupFlag=0x10000;
    /// <summary>功能旗標 0x800000：伺服器端連續攻擊 C_ATTACK_CONTINUE。3.81 沒有這一位，引擎不實作連續攻擊。</summary>
    public const uint ContinuousAttackFlag=0x800000;

    /// <summary>S139 第一個 byte 非 0：原版顯示 52／45／3347 後結束；3.81 永遠送 0，引擎只記錄，不中斷登入。</summary>
    public bool Accepted=>Status==0;
    public bool FastPickup=>Accepted&&(Flags&FastPickupFlag)!=0;
    public bool ContinuousAttack=>Accepted&&(Flags&ContinuousAttackFlag)!=0;
    /// <summary>依功能旗標的 C_GET 間隔（WO §2：986 ms，旗標開時 600 ms）。</summary>
    public int PickupThrottleMs=>GroundRules.PickupThrottle(FastPickup);
    /// <summary>原版 932／936 與系統 CodePage 不合時顯示 558 並結束；引擎文字固定用 950，只標記不中斷。</summary>
    public bool CodePageMismatch=>Accepted&&CodePage is 932 or 936&&CodePage!=LoginPackets.TextEncoding.CodePage;

    /// <summary>sub_94DBB0 的語系對照：0→949、3→950、4→932、5→936，其他→1252。</summary>
    public static int CodePageOf(byte language)=>language switch{0=>949,3=>950,4=>932,5=>936,_=>1252};

    /// <summary>C14：沿用 TW16 C_VERSION 欄位形狀 H(52) D(CodePage) C(FromCD) D(雜湊) D C。3.81 伺服器不讀任何欄位；
    /// 雜湊等引擎沒有的值一律送 0，不偽造原 BIN 的值。</summary>
    public static byte[] C14(){
        var b=new List<byte>{C14Opcode};
        void H(ushort v){b.Add((byte)v);b.Add((byte)(v>>8));}
        void D(uint v){H((ushort)v);H((ushort)(v>>16));}
        H(ClientVersionField);D((uint)LoginPackets.TextEncoding.CodePage);b.Add(0);D(0);D(0);b.Add(0);
        return LoginPackets.Pad([..b]);
    }

    /// <summary>解析 S139。格式不符時回 false 與原因，不丟例外。
    /// 讀法照 sub_94DBB0：c(狀態)；狀態 0 時 c(SERVER_NO) d×5 c c c(語系) d(功能旗標) d(+460，3.81 為 UPTIME)；
    /// 之後的欄位（3.81 是 H(1) C(0)＋補齊）不使用，只記剩餘長度。</summary>
    public static bool TryParse(byte[] plain,out ServerFeatures? features,out string? error){
        features=null;error=null;
        try{
            var r=new LoginReader(plain);
            if(r.C()!=S139Opcode){error="not S139";return false;}
            byte status=r.C();
            if(status!=0){features=new(status,0,0,0,0,0,r.Remaining);return true;}
            byte serverNo=r.C();for(int i=0;i<5;i++)r.D();r.C();r.C();
            byte language=r.C();uint flags=r.D(),uptime=r.D();
            features=new(status,serverNo,language,CodePageOf(language),flags,uptime,r.Remaining);return true;
        }catch(ProtocolException ex){error=ex.Message;return false;}
    }

    public object Snapshot()=>new{status=Status,accepted=Accepted,serverNo=ServerNo,language=LanguageCode,codePage=CodePage,codePageMismatch=CodePageMismatch,flags=$"0x{Flags:X}",fastPickup=FastPickup,pickupThrottleMs=PickupThrottleMs,continuousAttack=ContinuousAttack,uptime=Uptime,trailingBytes=TrailingBytes};
}

public sealed partial class LoginConnection {
    ServerFeatures? serverFeatures;string? serverFeaturesError,c14Error;bool c14Sent;int s139Count;
    /// <summary>本連線收到的 S139（登入期或進世界後才到的都算）；沒收到為 null，呼叫端照 3.81 預設行為。</summary>
    public ServerFeatures? ServerFeatures=>serverFeatures??World.LateServerFeatures;
    void ResetServerFeatures(){serverFeatures=null;serverFeaturesError=c14Error=null;c14Sent=false;s139Count=0;}
    object ServerFeaturesSnapshot()=>new{c14Sent,c14Error,s139Count,error=serverFeaturesError,features=ServerFeatures?.Snapshot()};
    /// <summary>登入期（尚未進世界）的 S139。永遠消化、永遠不丟例外。</summary>
    internal void AcceptServerVersion(byte[] plain){
        s139Count++;
        if(ServerFeatures.TryParse(plain,out var f,out var error)){serverFeatures=f;serverFeaturesError=null;}
        else serverFeaturesError=error;
    }
    /// <summary>進世界後才到的 S139 由 WorldSession 擴充點解析；收進連線層，切角色（World.Reset）後仍保留。</summary>
    void AdoptLateServerFeatures(){if(World.LateServerFeatures is {} late){serverFeatures=late;s139Count++;World.ClearLateServerFeatures();}}
    /// <summary>送 C14；任何非取消的失敗只記錄，登入照常繼續。</summary>
    internal async Task SendC14(Func<byte[],Task> write,CancellationToken token){
        byte[] bytes=ServerFeatures.C14();
        try{await write(bytes);c14Sent=true;}
        catch(Exception ex) when(ex is not OperationCanceledException||!token.IsCancellationRequested){c14Error=ex.GetType().Name+": "+ex.Message;}
    }
}

public sealed partial class WorldSession {
    ServerFeatures? lateServerFeatures;string? lateServerFeaturesError;
    public ServerFeatures? LateServerFeatures=>lateServerFeatures;
    internal void ClearLateServerFeatures()=>lateServerFeatures=null;
    // 3.81 的 S139 只回應 C14；通常在 S233 前就到。若延遲到進世界後才到，主分派沒有 139 的 case，由此消化，避免記為 unhandled。
    [WorldReceive(ServerFeatures.S139Opcode)]
    bool ReceiveServerVersion(byte[] plain){
        if(ServerFeatures.TryParse(plain,out var f,out var error)){lateServerFeatures=f;lateServerFeaturesError=null;}
        else lateServerFeaturesError=error;
        return true;
    }
    [WorldReset] void ResetServerFeatures(){lateServerFeatures=null;lateServerFeaturesError=null;}
    [WorldSnapshot("serverFeatures")] object? ServerFeaturesSnapshot()=>lateServerFeatures==null&&lateServerFeaturesError==null?null:new{late=lateServerFeatures?.Snapshot(),error=lateServerFeaturesError};
}

static class ServerFeaturesTests {
    // 合成 S139：照 3.81 S_ServerVersion 的欄位順序（server-protocol.md），不含任何伺服器或遊戲資源。
    internal static byte[] S139(byte status=0,byte language=3,uint flags=0x77d82,uint uptime=0x12345678,bool full=true){
        var b=new List<byte>{ServerFeatures.S139Opcode,status};
        void H(ushort v){b.Add((byte)v);b.Add((byte)(v>>8));}
        void D(uint v){H((ushort)v);H((ushort)(v>>16));}
        if(status==0){b.Add(1);D(0x07cbf4dd);D(0x07cbf4dd);D(0x77fc692d);D(0x07cbf4d9);D(0);b.Add(0);b.Add(0);b.Add(language);D(flags);D(uptime);if(full){H(1);b.Add(0);}}
        return LoginPackets.Pad([..b]);
    }

    [WorldModuleTest("server-features")]
    static void Run(WorldModuleTestContext t){
        // C14
        byte[] c14=ServerFeatures.C14();
        t.Equal(24,c14.Length,"C14 length (17 bytes padded to 8)");
        t.Equal((byte)14,c14[0],"C14 opcode");
        t.Equal("0E3400B603000000"+new string('0',32),Convert.ToHexString(c14),"C14 fields H(52) D(950) C D D C zero-padded");

        // 3.81 正常回應
        t.Check(ServerFeatures.TryParse(S139(),out var f,out var e)&&f!=null,"3.81 S139 parses",e);
        t.Equal((byte)0,f!.Status,"status");t.Equal((byte)1,f.ServerNo,"server no");
        t.Equal((byte)3,f.LanguageCode,"language");t.Equal(950,f.CodePage,"language 3 -> cp950");
        t.Equal(0x77d82u,f.Flags,"feature flags");t.Equal(0x12345678u,f.Uptime,"uptime");
        t.Check(f.FastPickup,"0x77d82 has 0x10000");t.Equal(600,f.PickupThrottleMs,"pickup throttle 600 ms");
        t.Check(!f.ContinuousAttack,"0x77d82 lacks 0x800000");t.Check(!f.CodePageMismatch,"cp950 matches engine text encoding");
        t.Equal(3+3,f.TrailingBytes,"trailing H(1) C(0) + padding");
        t.Check(ServerFeatures.TryParse(S139(flags:0x800000),out var g,out _)&&!g!.FastPickup&&g.PickupThrottleMs==986&&g.ContinuousAttack,"flags without 0x10000 -> 986 ms; 0x800000 -> continuous attack");
        t.Check(ServerFeatures.TryParse(S139(full:false),out var h,out _)&&h!.Flags==0x77d82,"tail after uptime is optional");
        // 語系對照
        foreach(var (lang,cp) in new[]{(0,949),(3,950),(4,932),(5,936),(1,1252),(2,1252),(255,1252)})t.Equal(cp,ServerFeatures.CodePageOf((byte)lang),$"language {lang} -> cp{cp}");
        t.Check(ServerFeatures.TryParse(S139(language:4),out var j,out _)&&j!.CodePageMismatch,"cp932 flagged as mismatch (recorded only)");
        // 狀態非 0、截斷、錯 opcode：不丟例外
        t.Check(ServerFeatures.TryParse(S139(status:1),out var k,out _)&&!k!.Accepted&&!k.FastPickup&&k.PickupThrottleMs==986,"status 1 recorded, flags ignored");
        t.Check(!ServerFeatures.TryParse([139,0,1,2],out var m,out var me)&&m==null&&me!=null,"truncated S139 -> false with reason");
        t.Check(!ServerFeatures.TryParse([93,0,0,0,0,0,0,0],out _,out _),"wrong opcode rejected");
        t.Check(!ServerFeatures.TryParse([],out _,out _),"empty packet rejected");

        // LoginConnection：登入期 S139、格式錯誤、C14 送出失敗都不丟例外
        using(var c=new LoginConnection()){
            t.Check(c.ServerFeatures==null,"no S139 -> null features");
            c.AcceptServerVersion([139,0,1]);
            t.Check(c.ServerFeatures==null,"malformed S139 does not set features");
            c.AcceptServerVersion(S139());
            t.Equal(0x77d82u,c.ServerFeatures?.Flags??0,"login-phase S139 stored on connection");
            var snap=System.Text.Json.JsonSerializer.SerializeToElement(c.Snapshot());
            t.Equal(2,snap.GetProperty("serverFeatures").GetProperty("s139Count").GetInt32(),"snapshot counts S139");
            t.Equal(600,snap.GetProperty("serverFeatures").GetProperty("features").GetProperty("pickupThrottleMs").GetInt32(),"snapshot pickup throttle");
            byte[]? written=null;
            c.SendC14(b=>{written=b;return Task.CompletedTask;},CancellationToken.None).GetAwaiter().GetResult();
            t.Check(written!=null&&written[0]==14,"C14 handed to writer");
            bool threw=false;
            try{c.SendC14(_=>throw new IOException("synthetic write failure"),CancellationToken.None).GetAwaiter().GetResult();}catch{threw=true;}
            t.Check(!threw,"C14 write failure swallowed");
            snap=System.Text.Json.JsonSerializer.SerializeToElement(c.Snapshot());
            t.Check(snap.GetProperty("serverFeatures").GetProperty("c14Error").GetString()?.Contains("IOException")==true,"C14 failure recorded");
            using var cts=new CancellationTokenSource();cts.Cancel();threw=false;
            try{c.SendC14(_=>throw new OperationCanceledException(cts.Token),cts.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){threw=true;}
            t.Check(threw,"cancellation still propagates");
            c.Cancel();
            t.Check(c.ServerFeatures==null,"Cancel clears connection features");
        }

        // 進世界後才到的 S139：由 [WorldReceive(139)] 消化，不記 unhandled
        var s=new WorldSession();
        t.Check(s.ExtensionEntries.Any(x=>x.Kind=="receive"&&x.Opcode==139),"S139 registered through M7-A extension point");
        s.Receive(S139(),"");
        t.Equal(0x77d82u,s.LateServerFeatures?.Flags??0,"late S139 parsed by world extension");
        var ws=WorldModuleTestContext.SnapshotJson(s);
        t.Check(ws.GetProperty("extensions").GetProperty("serverFeatures").ValueKind==System.Text.Json.JsonValueKind.Object,"world snapshot exposes late S139");
        s.Receive([139,0,1,0,0,0,0,0],"");
        t.Equal(0x77d82u,s.LateServerFeatures?.Flags??0,"malformed late S139 keeps previous value");
        s.Reset();
        t.Check(s.LateServerFeatures==null,"WorldReset clears late features");
    }
}
