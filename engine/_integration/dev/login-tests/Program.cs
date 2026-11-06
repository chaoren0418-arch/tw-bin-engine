using P06;
using System.Text;
using System.Text.Json;

// Tests the built product with generated credentials and a socket-free session.
// Never print credentials, packet bytes, input characters or exception details.
static class Checks {
    static int assertions;
    const string LettersDigits="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    const string PasswordCharacters=LettersDigits+"!_=+-?.#";
    const string AccountLength="帳號長度需為 1–12 位。";
    const string AccountCharacters="帳號只能使用 ASCII 英文字母與數字。";
    const string PasswordLength="密碼長度需為 1–13 位。";
    const string PasswordRule="密碼只能使用 ASCII 英文字母、數字與符號 !_=+-?.#。";

    static void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);assertions++;}
    static string Synthetic(int length)=>new('A',length);
    static void Packet(string account,string password,bool accepted,string? error=null){
        byte[] bytes;
        try{bytes=LoginPackets.Login(account,password);}
        catch(ArgumentException ex){
            Check(!accepted,"encoder rejected an allowed input");
            Check(ex.Message==error,"encoder rule message differs");
            Check(ex.ParamName==null&&ex.Data.Count==0,"encoder diagnostics contain input metadata");
            return;
        }
        Check(accepted,"encoder accepted a forbidden input");
        int used=1+account.Length+1+password.Length+1;
        Check(bytes.Length==(used+7)/8*8,"C119 padding length");
        Check(bytes[0]==119,"C119 opcode");
        Check(bytes.AsSpan(1,account.Length).SequenceEqual(Encoding.ASCII.GetBytes(account.ToLowerInvariant())),"account normalized to lowercase");
        Check(bytes[account.Length+1]==0,"account terminator");
        Check(bytes.AsSpan(account.Length+2,password.Length).SequenceEqual(Encoding.ASCII.GetBytes(password)),"password bytes preserve case and symbols");
        Check(bytes.AsSpan(used-1).IndexOfAnyExcept((byte)0)<0,"password terminator and zero padding");
        Array.Clear(bytes);
    }
    static void Controller(string account,string password,bool accepted,string? error=null){
        var session=new MemorySession();int prepares=0,changes=0;
        var control=new LoginControl(session,()=>prepares++,()=>throw new InvalidOperationException("unexpected preview"));
        control.Changed+=()=>changes++;
        var result=control.Start("p24-real-jar-d287-client2",42061,account,password,session.SessionGeneration,session.Revision);
        Check(result.Accepted==accepted,"controller admission differs");
        Check(prepares==(accepted?1:0)&&session.Runs==(accepted?1:0),"invalid input must not prepare UI or start session");
        Check(changes>0&&control.Notice==result.Message,"login notice published");
        if(accepted){Check(control.StartFailure==null,"allowed input has no start failure");return;}
        Check(result.Message==error,"controller describes the failing rule");
        Check(control.CanLogin&&control.StartFailure==null&&control.Revision==1,"validation leaves login retry available");
        using var snapshot=JsonDocument.Parse(JsonSerializer.Serialize(control.Snapshot()));
        Check(snapshot.RootElement.GetProperty("notice").GetString()==error,"snapshot contains only rule notice");
        Check(!snapshot.RootElement.TryGetProperty("account",out _)&&!snapshot.RootElement.TryGetProperty("password",out _),"snapshot omits credentials");
        var retry=control.Start("p24-real-jar-d287-client2",42061,Synthetic(3),Synthetic(4),session.SessionGeneration,session.Revision);
        Check(retry.Accepted&&prepares==1&&session.Runs==1,"valid retry after rejection");
    }
    static void Main(){
        try{
            // Full UTF-16 domain: includes all ASCII punctuation/control codes, Unicode and surrogates.
            for(int value=0;value<=char.MaxValue;value++){
                char c=(char)value;string input=Synthetic(2)+c;
                bool accountAllowed=LettersDigits.Contains(c),passwordAllowed=PasswordCharacters.Contains(c);
                Packet(input,Synthetic(4),accountAllowed,accountAllowed?null:AccountCharacters);
                Packet(Synthetic(3),input,passwordAllowed,passwordAllowed?null:PasswordRule);
                if(value<128||value is 0x00e9 or 0x4e2d or 0xff21 or 0xd800 or 0xdc00){
                    Controller(input,Synthetic(4),accountAllowed,accountAllowed?null:AccountCharacters);
                    Controller(Synthetic(3),input,passwordAllowed,passwordAllowed?null:PasswordRule);
                }
            }
            foreach(int length in new[]{0,1,12,13,14}){
                bool accepted=length is >=1 and <=12;
                Packet(Synthetic(length),Synthetic(4),accepted,accepted?null:AccountLength);
                Controller(Synthetic(length),Synthetic(4),accepted,accepted?null:AccountLength);
                accepted=length is >=1 and <=13;
                Packet(Synthetic(3),Synthetic(length),accepted,accepted?null:PasswordLength);
                Controller(Synthetic(3),Synthetic(length),accepted,accepted?null:PasswordLength);
            }
            Packet(null!,Synthetic(4),false,AccountLength);
            Packet(Synthetic(3),null!,false,PasswordLength);
            Controller(null!,Synthetic(4),false,AccountLength);
            Controller(Synthetic(3),null!,false,PasswordLength);
            // Every valid length pairing exercises C119 terminators and 8-byte padding boundaries.
            for(int accountLength=1;accountLength<=12;accountLength++)
                for(int passwordLength=1;passwordLength<=13;passwordLength++)
                    Packet(LettersDigits[..accountLength],PasswordCharacters.Substring(PasswordCharacters.Length-passwordLength),true);
            CultureInvariant();
            WebRoute();
            Console.WriteLine(JsonSerializer.Serialize(new{status="pass",assertions,utf16Values=65536,network=false,game=false,database=false}));
        }catch{
            // Deliberately avoid dumping potentially sensitive argument/packet values on failure.
            Console.Error.WriteLine("FAIL: login credential checks; completed assertions="+assertions);
            Environment.ExitCode=1;
        }
    }
    static void CultureInvariant(){
        var previous=System.Globalization.CultureInfo.CurrentCulture;
        try{
            System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            Packet(LettersDigits[..12],new string(new[]{'I','i','Z','z','0','!'}),true);
        }finally{System.Globalization.CultureInfo.CurrentCulture=previous;}
    }
    static void WebRoute(){
        // Same controller as the WinForms route, reached through the existing web command parser.
        foreach(bool accepted in new[]{false,true}){
            string account=Synthetic(3),password=Synthetic(3)+(accepted?'!':'@');
            using var json=JsonDocument.Parse(JsonSerializer.Serialize(new{v=1,id=1,cmd="login",generation=0,revision=0,profile="p24-real-jar-d287-client2",port=42061,account,password}));
            var command=LoginWebCommand.Parse(json.RootElement);
            Controller(command.Account,command.TakePassword(),accepted,accepted?null:PasswordRule);
            Check(command.TakePassword().Length==0,"web password consumed once");
        }
    }
    sealed class MemorySession:ILoginSession {
        public int Runs {get;private set;}
        public LoginState State=>LoginState.Offline;
        public int Revision=>0;
        public int SessionGeneration=>0;
        public IReadOnlyList<LoginCharacter> Characters=>[];
        public Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default){Runs++;return Task.CompletedTask;}
        public Task SelectCharacter(string name)=>throw new InvalidOperationException("unexpected selection");
        public void Cancel(bool offline=false)=>throw new InvalidOperationException("unexpected cancellation");
    }
}
