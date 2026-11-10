using System.Reflection;
using P06.NativeUi;
namespace P06;

// R5-I（T314／#741）離線測試：原生 UI 關閉時的 QUIT_WINDOW WinForms 暫行窗。
// 顯示條件、文字、暫行窗的 revision／取消／按鈕轉呼叫、與原生窗共用動作的接線，以及死亡逾時→開窗→取消的連線狀態。
// 合成封包、假送出閘門與假時鐘；不開遊戲、網路，也不讀客戶端素材（視窗物件只建立、不 Show）。
static class QuitFallbackTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
    const uint Own=123;
    static byte[] Death(uint id)=>LoginPackets.Pad([158,..BitConverter.GetBytes(id),8]);

    [WorldModuleTest("quit-fallback")]
    static void Run(WorldModuleTestContext t){
        Wiring(t);Rules(t);Window(t);Connection(t);
    }

    static void Wiring(WorldModuleTestContext t){
        var form=typeof(ClientForm);
        t.Check(form.GetMethod("SyncQuitFallback",F)!=null,"ClientForm.SyncQuitFallback declared");
        t.Check(form.GetProperty("NativeQuitWindowActive",F)!=null,"native QUIT_WINDOW availability check declared");
        foreach(var name in new[]{"QuitWindowRestart","QuitWindowQuit","QuitWindowClose"})
            t.Check(form.GetMethod(name,F,Type.EmptyTypes)!=null,$"shared action {name} declared on ClientForm");
        var source=form.GetNestedType("QuitWindowSource",BindingFlags.NonPublic);
        t.Check(source!=null&&typeof(IQuitWindowSource).IsAssignableFrom(source),"native QuitWindowSource still implements IQuitWindowSource");
        t.Check(typeof(QuitFallbackWindow).IsSubclassOf(typeof(Form)),"fallback is a WinForms window");
    }

    static void Rules(WorldModuleTestContext t){
        var dead=new QuitWindowView(3,false,false,"斷線。");
        t.Check(QuitFallbackView.Wanted(false,dead,-1),"no native panel + open window → fallback shown");
        t.Check(!QuitFallbackView.Wanted(true,dead,-1),"native QUIT_WINDOW usable → no fallback");
        t.Check(!QuitFallbackView.Wanted(false,null,-1),"window closed → no fallback");
        t.Check(!QuitFallbackView.Wanted(false,dead,3),"dismissed revision stays hidden");
        t.Check(QuitFallbackView.Wanted(false,dead with{Revision=4},3),"a new open shows again");
        t.Equal("斷線。\n連線已關閉：「重新開始」回到登入畫面，「離開遊戲」關閉程式。",QuitFallbackView.Body(dead),"offline body: message 47 + hint");
        t.Equal("角色已死亡：「重新開始」送出死亡後重新開始。",QuitFallbackView.Body(new(1,true,true,"")),"dead in world: hint only");
        t.Equal("「重新開始」回到選角畫面。",QuitFallbackView.Hint(new(1,true,false,"x")),"alive in world hint");
    }

    static void Window(WorldModuleTestContext t){
        using var w=new QuitFallbackWindow();
        int restarts=0,quits=0,dismissed=0;
        w.Restart=()=>restarts++;w.Quit=()=>quits++;w.Dismissed=()=>dismissed++;
        t.Check(w.ButtonTexts.SequenceEqual(["重新開始","離開遊戲","取消"]),"three buttons as in QUIT_WINDOW",w.ButtonTexts);
        t.Equal("離開遊戲",w.Text,"window caption");
        w.ClickRestart();w.ClickQuit();w.Dismiss();
        t.Check(restarts==0&&quits==0&&dismissed==0&&w.DismissedRevision==-1,"no view yet → buttons do nothing");
        w.SetView(new(5,false,false,"斷線。"));
        t.Check(w.Revision==5&&w.MessageText.StartsWith("斷線。\n",StringComparison.Ordinal),"view applied",w.MessageText);
        w.ClickRestart();w.ClickQuit();
        t.Check(restarts==1&&quits==1,"restart / quit forwarded to the shared actions");
        w.Dismiss();
        t.Check(dismissed==1&&w.DismissedRevision==5&&!w.Visible,"cancel closes this open only");
        t.Check(!QuitFallbackView.Wanted(false,new(5,false,false,"斷線。"),w.DismissedRevision),"same revision not re-shown after cancel");
        w.SetView(new(6,true,true,""));
        t.Check(w.Revision==6&&w.DismissedRevision==5,"next open keeps the old dismissed revision");
    }

    static void Connection(WorldModuleTestContext t){
        long now=0;var sent=new List<byte[]>();
        Task<bool> Sender(byte[] b,Func<bool> valid,Action admit){if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);}
        using(ServerMessageText.UseTable(null))
        using(var c=new LoginConnection()){
            c.SeedRecoveryTestWorld(Own,4,Sender);c.World.DeathClock=()=>now;
            c.World.Receive(Death(Own),"offline");
            t.Check(!QuitFallbackView.Wanted(false,c.QuitWindowSnapshot(),-1),"death alone does not open the fallback");
            now=DeathTimeoutRules.QuitTimeoutMs;
            t.Check(c.CheckDeathTimeout().GetAwaiter().GetResult(),"death timeout sends C122");
            var v=c.QuitWindowSnapshot();
            t.Check(QuitFallbackView.Wanted(false,v,-1),"after the timeout the fallback opens with the native UI off",v);
            t.Check(!QuitFallbackView.Wanted(true,v,-1),"…and stays hidden while the native panel is usable");
            t.Equal("7A00000000000000",Convert.ToHexString(sent.Single()),"exactly one C122");
            using var w=new QuitFallbackWindow();w.Dismissed=c.CloseQuitWindow;
            w.SetView(v!);w.Dismiss();
            t.Check(c.QuitWindowSnapshot()==null&&!QuitFallbackView.Wanted(false,c.QuitWindowSnapshot(),w.DismissedRevision),"cancel closes QUIT_WINDOW (no packet)");
            t.Equal(1,sent.Count,"cancel sends nothing");
        }
    }
}
