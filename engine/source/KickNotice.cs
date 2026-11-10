namespace P06;

// T273（#651）：伺服器踢除時的模態訊息框。依據 docs/reference/send-sites-2.md §9（SS2§9）。
//   TW16 S_KICK（op 245，sub_940960／FO 0x53FD60）：讀 C(原因) 取字串 → 收尾（dword_136E998=2、sub_A23DE0、sub_8C2D10）
//   → sub_8E4670(文字,1,1) 通用模態訊息框 → PostQuitMessage(0)，結束程式、不回登入畫面。
//   3.81 對應 S227 S_Disconnect＝C(227) H(0x01F4) D(0)（docs/reference/server-protocol.md「S_Disconnect」）。
//   第一個 byte 是 0xF4（244），不在 TW16 的原因表內，所以一律顯示字串 64「連線中斷。」。
// 掛接：M7-A 觀察型處理器（WorldExtensions.cs）。WorldSession.cs 的 case 227 照舊丟 ProtocolException 結束連線
//   （對應原版開框前的收尾），觀察器在那之前記下踢除通知，並交給擁有這個 session 的 ClientForm 開框；按確定後關閉程式。
public sealed record KickNotice(int Revision,byte? Reason,int StringId,string Text);

public static class KickNoticeRules {
    /// <summary>3.81 S_Disconnect 的 opcode（TW16 S_KICK 245 的對應）。</summary>
    public const byte Opcode=227;
    /// <summary>TW16 原因表「其他」分支的字串；3.81 的 0xF4 一律落在這裡。</summary>
    public const int StringId=64;
    /// <summary>沒有字串表（CI、沒有原客戶端）或該筆空白時的顯示字；與本機 string-c.tbl 第 64 筆相同（SS2§9）。</summary>
    public const string Fallback="連線中斷。";
    /// <summary>字串表第 64 筆（去掉色碼）；讀不到或空白時用 Fallback。</summary>
    public static string Text()=>ServerMessageText.Table is {} t&&StringId<t.Count&&ServerMessageText.DisplayText(t[StringId]).Trim() is {Length:>0} s?s:Fallback;
    /// <summary>封包的第一個資料 byte（TW16 讀成原因）；封包只有 opcode 時為 null。</summary>
    public static byte? Reason(byte[] plain)=>plain.Length>1?plain[1]:null;
}

public sealed partial class WorldSession {
    int kickRevision;
    /// <summary>最後一次收到的踢除通知。刻意不在 Reset() 清掉：case 227 丟出的例外會讓 LoginConnection 立刻 World.Reset()，
    /// 之後畫面才開框；只靠 Revision 判斷是不是新的通知。</summary>
    public KickNotice? Kick {get;private set;}

    [WorldReceive(KickNoticeRules.Opcode,Observe=true)]
    void ObserveKick(byte[] plain){
        var notice=new KickNotice(Interlocked.Increment(ref kickRevision),KickNoticeRules.Reason(plain),KickNoticeRules.StringId,KickNoticeRules.Text());
        Kick=notice;
        ClientForm.OfferKickNotice(this,notice);
    }

    [WorldSnapshot("kick")]
    object? KickSnapshot()=>Kick is {} k?new{revision=k.Revision,reason=k.Reason,stringId=k.StringId,text=k.Text}:null;
}

public sealed partial class ClientForm {
    int shownKickRevision;
    /// <summary>由收封包的執行緒呼叫：找出擁有這個 session 的視窗開框。離線測試沒有視窗，什麼都不做。</summary>
    internal static void OfferKickNotice(WorldSession world,KickNotice notice){
        foreach(var owner in Application.OpenForms.OfType<ClientForm>().ToArray())owner.QueueKickNotice(world,notice);
    }
    void QueueKickNotice(WorldSession world,KickNotice notice){
        if(!ReferenceEquals(login.World,world)||!IsHandleCreated||IsDisposed)return;
        // BeginInvoke：觀察器在收封包迴圈內，例外與 World.Reset() 跑完之後才在 UI 執行緒開框（原版也是先收尾再開框）。
        BeginInvoke((Action)(()=>{
            if(IsDisposed||notice.Revision<=shownKickRevision)return;
            shownKickRevision=notice.Revision;
            events.Add(new{kind="server-kick",revision=notice.Revision,reason=notice.Reason,stringId=notice.StringId,text=notice.Text,prompt=login.NewsConfirmationRequired});
            // 自動化情境（demoPort、非 real-interactive）沒有人按確定，和 S48 公告一樣不開模態框，也不關視窗。
            if(!login.NewsConfirmationRequired)return;
            LeaveGameFocus("server-kick");
            MessageBox.Show(this,notice.Text,Text,MessageBoxButtons.OK,MessageBoxIcon.None);
            // PostQuitMessage(0)：結束程式，不回登入畫面。
            if(!IsDisposed)Close();
        }));
    }
}
