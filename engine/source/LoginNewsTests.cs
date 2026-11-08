using System.Text.Json;
namespace P06;

// T260（#621，R2-Q）：S48 登入公告確認閘門的離線測試（合成封包，不連遊戲、網路或素材）。由 --module-tests 執行。
static class LoginNewsTests {
    static byte[] S48(string text){var body=new List<byte>{48};body.AddRange(LoginPackets.TextEncoding.GetBytes(text));body.Add(0);return LoginPackets.Pad(body.ToArray());}

    [WorldModuleTest("login-news")]
    static void Run(WorldModuleTestContext t){
        // 1. 解析：op 48 + CP950 字串 + 補 0；opcode 錯誤、長度錯誤都要拒絕。
        t.Equal("歡迎來到測試伺服器",LoginNews.Parse(S48("歡迎來到測試伺服器")),"S48 CP950 text");
        t.Equal("",LoginNews.Parse(S48("")),"S48 empty text");
        t.Equal("第一行\n第二行",LoginNews.Parse(S48("第一行\n第二行")),"S48 keeps newlines");
        t.Throws<ProtocolException>(()=>LoginNews.Parse(LoginPackets.Pad([49,0])),"wrong opcode rejected");
        t.Throws<ProtocolException>(()=>LoginNews.Parse([48,0x41,0,0,0,0,0,0,0x41]),"bad padding rejected");
        t.Throws<ProtocolException>(()=>LoginNews.Parse([48,0x41,0x42]),"unterminated string rejected");

        // 2. 確認後回送的 C16：op 16 補滿 8 byte（與既有自動確認、CharacterManagement.Refresh 相同）。
        t.Check(LoginNews.Acknowledgement().SequenceEqual(LoginPackets.Pad([16])),"C16 bytes");
        t.Equal(8,LoginNews.Acknowledgement().Length,"C16 padded to 8");

        // 3. 閘門：沒有公告時確認無效；Begin 後確認才放行；同一則只能確認一次。
        var gate=new LoginNews();
        t.Check(!gate.Pending,"idle not pending");
        t.Check(!gate.Confirm(0),"confirm without news ignored");
        var (rev,confirmed)=gate.Begin();
        t.Check(gate.Pending&&!confirmed.IsCompleted,"news waits for user");
        t.Check(!gate.Confirm(rev-1),"stale revision ignored");
        t.Check(gate.Pending&&!confirmed.IsCompleted,"still waiting after stale confirm");
        t.Check(gate.Confirm(rev),"current revision confirms");
        t.Check(!gate.Pending,"confirm clears pending");
        t.Check(confirmed.Wait(1000)&&confirmed.Status==TaskStatus.RanToCompletion,"confirmation releases waiter");
        t.Check(!gate.Confirm(rev),"double confirm ignored");

        // 4. 取消／斷線：等待中的公告被取消，舊框之後按確定不會影響下一則。
        var (rev2,second)=gate.Begin();
        t.Check(rev2==rev+1,"revision increments per news");
        gate.Reset();
        t.Check(!gate.Pending,"reset clears pending");
        try{second.Wait(1000);}catch(AggregateException){}
        t.Check(second.IsCanceled,"reset cancels waiter");
        var (rev3,third)=gate.Begin();
        t.Check(!gate.Confirm(rev2),"old dialog cannot confirm newer news");
        t.Check(gate.Confirm(rev3)&&third.Wait(1000),"newer news confirmed by its own dialog");
        var (_,replaced)=gate.Begin();var (rev5,_)=gate.Begin();
        try{replaced.Wait(1000);}catch(AggregateException){}
        t.Check(replaced.IsCanceled&&gate.Pending,"second Begin supersedes first");
        t.Check(gate.Confirm(rev5),"superseding news confirmable");

        // 5. LoginConnection 表面：預設自動確認（探針／自動化相容）；未在等待時 ConfirmNews 無效；Snapshot 帶 newsPending；Cancel 清除。
        using(var c=new LoginConnection()){
            t.Check(!c.NewsConfirmationRequired,"default keeps auto-acknowledge for probes");
            t.Check(!c.NewsPending,"fresh connection not pending");
            t.Check(!c.ConfirmNews(c.NewsRevision),"ConfirmNews without news ignored");
            var snap=JsonSerializer.SerializeToElement(c.Snapshot());
            t.Check(snap.TryGetProperty("newsPending",out var p)&&p.ValueKind==JsonValueKind.False,"snapshot newsPending=false");
            c.NewsConfirmationRequired=true;c.Cancel();
            t.Check(!c.NewsPending&&c.News=="","cancel leaves no pending news");
        }

        // 6. 顯示文字：斷行統一成 CRLF，空公告顯示提示。
        t.Equal("甲\r\n乙\r\n丙",LoginNewsText.Display("甲\n乙\r\n丙"),"display normalizes newlines");
        t.Equal("（伺服器公告為空）",LoginNewsText.Display("  "),"display empty placeholder");
    }
}
