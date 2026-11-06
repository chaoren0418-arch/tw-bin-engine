using System.Text.Json;
namespace P06;

// One adapter over the existing connection. Tests may supply a socket-free session.
public interface ILoginSession {
    LoginState State {get;}
    int Revision {get;}
    int SessionGeneration {get;}
    IReadOnlyList<LoginCharacter> Characters {get;}
    Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default);
    Task SelectCharacter(string name);
    void Cancel(bool offline=false);
}
public readonly record struct LoginAction(bool Accepted,string Message);
public sealed record LoginStartFailure(string Phase,string Type,int HResult,string[] Frames);
public sealed class LoginControl(ILoginSession session,Action beforeStart,Action offlinePreview) {
    bool running,selecting;int operation;
    public int Revision {get;private set;}
    public string Notice {get;private set;}="請輸入帳號與密碼，或使用離線預覽。";
    public LoginStartFailure? StartFailure {get;private set;}
    void RecordStartFailure(string phase,Exception error){
        // Diagnostics deliberately exclude Message/ToString, parameters, locals and source paths.
        var frames=new System.Diagnostics.StackTrace(error,false).GetFrames()?.Select(f=>f.GetMethod()).Where(m=>m!=null).Take(8).Select(m=>(m!.DeclaringType?.FullName??"")+"."+m.Name).ToArray()??[];
        StartFailure=new(phase,error.GetType().FullName??error.GetType().Name,error.HResult,frames);
    }
    public event Action? Changed;
    public bool CanLogin=>!running&&!selecting&&session.State is LoginState.Offline or LoginState.Cancelled or LoginState.Disconnected or LoginState.Rejected or LoginState.Faulted;
    public bool CanEnter=>!selecting&&session.State==LoginState.CharacterList&&session.Characters.Count>0;
    public bool CanCancel=>running||selecting||session.State is not (LoginState.Offline or LoginState.Cancelled);
    LoginAction Report(bool accepted,string message){Notice=message;Revision++;Changed?.Invoke();return new(accepted,message);}
    bool Current(int generation)=>generation==session.SessionGeneration;
    public static bool ValidEndpoint(string profile,int port)=>profile switch {
        "p24-real-jar-d287-client2"=>port==42061,
        "jar-d287-client1" or "jar-d287-client2"=>port is 42062 or 42063,
        _=>false
    };
    public LoginAction Start(string profile,int port,string account,string password,int generation,int revision){
        if(!Current(generation)||revision!=session.Revision)return Report(false,"登入狀態已更新，請依目前狀態重試。");
        if(!CanLogin)return Report(false,"目前已有連線或登入作業，請先取消／斷線。");
        if(!ValidEndpoint(profile,port))return Report(false,"請選擇相符的本機連線設定與連接埠。");
        if(LoginPackets.LoginValidationError(account,password) is {} validationError)return Report(false,validationError);
        int current=++operation;running=true;StartFailure=null;string phase="before-start";
        try {beforeStart();phase="start-session";var task=session.Run(port,profile,account,password);phase="publish-status";var result=Report(true,"登入請求已送出，請查看連線狀態。");_ = Observe(task,current);return result;}
        catch(Exception error) {running=false;RecordStartFailure(phase,error);return Report(false,phase=="before-start"?"登入前的畫面準備失敗；可切回離線預覽後重試。":"無法開始登入，請查看連線狀態後重試。");}
    }
    async Task Observe(Task task,int current){
        try{await task;}catch(Exception error){if(current==operation){RecordStartFailure("session-task",error);Report(false,"連線作業未完成；可重新登入或切回離線預覽。");}}
        finally{if(current==operation){running=false;Revision++;Changed?.Invoke();}}
    }
    public async Task<LoginAction> Enter(string name,int generation,int revision){
        if(!Current(generation)||revision!=session.Revision)return Report(false,"角色清單已更新，請重新選擇。");
        if(!CanEnter||!session.Characters.Any(c=>c.Name==name))return Report(false,"請選擇目前伺服器清單中的角色。");
        int current=operation;selecting=true;Revision++;Changed?.Invoke();
        try {await session.SelectCharacter(name);return current==operation?Report(true,"已要求進入世界，請查看連線狀態。"):new(false,"選角已取消。");}
        catch{return current==operation?Report(false,"無法進入角色，請查看連線狀態後重試。"):new(false,"選角已取消。");}
        finally{if(current==operation){selecting=false;Revision++;Changed?.Invoke();}}
    }
    public LoginAction Cancel(int generation,bool offline=false){
        if(!Current(generation))return Report(false,"連線已更換，請依目前連線重試取消或離線預覽。");
        if(!offline&&!CanCancel)return Report(false,"目前沒有需要取消的連線。");
        operation++;running=selecting=false;session.Cancel(offline);
        if(offline)offlinePreview();
        return Report(true,offline?"已關閉連線，返回離線預覽。":"已取消／斷線；可重新登入。");
    }
    public object Snapshot()=>new{revision=Revision,sessionRevision=session.Revision,generation=session.SessionGeneration,canLogin=CanLogin,canEnter=CanEnter,canCancel=CanCancel,notice=Notice,startFailure=StartFailure};
}

// Deliberately not a record: neither ToString nor JSON properties expose a secret.
public sealed class LoginWebCommand {
    public string Command {get;}
    public int Revision {get;}
    public int Generation {get;}
    public string Profile {get;}
    public int Port {get;}
    public string Account {get;}
    public string Character {get;}
    string secret;
    LoginWebCommand(string command,int generation,int revision,string profile="",int port=0,string account="",string password="",string character=""){
        Command=command;Generation=generation;Revision=revision;Profile=profile;Port=port;Account=account;secret=password;Character=character;
    }
    public string TakePassword(){string value=secret;secret="";return value;}
    public static bool IsLoginCommand(string command)=>command is "login" or "enterWorld" or "cancelLogin" or "offlinePreview";
    public static LoginWebCommand Parse(JsonElement value){
        if(value.ValueKind!=JsonValueKind.Object)throw new FormatException("login schema");
        string Text(string key,int max){if(!value.TryGetProperty(key,out var p)||p.ValueKind!=JsonValueKind.String)throw new FormatException("login field type");string text=p.GetString()!;if(text.Length==0||text.Length>max||text.Any(c=>char.IsControl(c)))throw new FormatException("login field length");return text;}
        int Number(string key){if(!value.TryGetProperty(key,out var p)||p.ValueKind!=JsonValueKind.Number||!p.TryGetInt32(out int n))throw new FormatException("login number");return n;}
        string command=Text("cmd",24);
        if(!IsLoginCommand(command)||Number("v")!=1||Number("id")<1||Number("revision")<0||Number("generation")<0)throw new FormatException("login schema");
        string[] fields=command switch {"login"=>["v","id","cmd","generation","revision","profile","port","account","password"],"enterWorld"=>["v","id","cmd","generation","revision","character"],_=>["v","id","cmd","generation","revision"]};
        if(!value.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(fields.Order()))throw new FormatException("login fields");
        int revision=Number("revision"),generation=Number("generation");
        return command switch {
            "login"=>new(command,generation,revision,Text("profile",40),Number("port"),Text("account",12),Text("password",13)),
            "enterWorld"=>new(command,generation,revision,character:Text("character",64)),
            _=>new(command,generation,revision)
        };
    }
}
