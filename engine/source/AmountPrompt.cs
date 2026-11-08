using System.Buffers.Binary;
namespace P06;

// M7-E（T207／#513）：S136 數量輸入詢問 → NPC 對話面板的 HTML 頁加數字框 → 回 C11。
// 原版行為依 docs/reference/windows-2.md §5.3（T226）：S136（TW16 op 224，sub_93FDF0／FO 0x53F1F0）不開 InputWindow，
// 而是把欄位寫進 NPC 對話窗（模式 0）並顯示該 HTML 頁，頁內帶數字輸入框；HTML id 為空時關窗。
// 格式以 3.81 伺服器為準（docs/reference/server-protocol.md，#132；TW16 的 `dddddcsssh` 後段對應待驗證）：
//   S136 C(136) D(objId) D(0) D(初始值) D(下限) D(上限) H(0) S(html) S(cmd) [H(n) S×n]
//        S_HowManyMake／S_ItemCount(objId,max,cmd)：初始值 0、下限 0、上限 max、html "request"。
//        S_ItemCount(…,data)：附加字串清單＝HTML 頁的 #0、#1… 參數。S_ApplyAuction／S_SellHouse：html "agapply"／"agsell"。
//   C11  C(11) D(objId) D(amount) C(0) S(cmd)
//        原服 C_Amount 讀 objId、amount（≤ 0 直接丟棄）、一個忽略的 byte、命令字串（＝S136 第二個字串）；
//        每次詢問只有一張一次性票（UiOperationContext.take），所以每個詢問最多回一次。
// 推測／待驗證：第 2 個 D 與 H(0) 的意義（3.81 一律送 0，只保存不使用）；C11 的那個 byte 原版送什麼（原服忽略，本引擎送 0）；
// `agapply` 原版改交 CAgitAuctionUI（XML 缺），本引擎照一般 HTML 頁加數字框顯示。

/// <summary>S136 解碼結果（欄位照 3.81 伺服器）。</summary>
public sealed record AmountPromptPacket(uint ObjectId,uint Reserved,int Initial,int Min,int Max,ushort Flag,string Html,string Command,string[] Data);
/// <summary>目前顯示在 NPC 對話面板的數量詢問。Lower＝可送出的最小值（原服不收 ≤ 0）。</summary>
public sealed record AmountOffer(uint ObjectId,int Initial,int Min,int Max,string Html,string Command,string[] Data,long Revision,int InteractionRevision){
    public int Lower=>Math.Max(1,Min);
    public bool Answerable=>Command.Length>0&&Lower<=Max;
    public int Suggested=>Answerable?Math.Clamp(Initial,Lower,Max):0;
}
public sealed record AmountPromptView(int Generation,int WorldEpoch,uint Actor,long Revision,bool CanSend,string Notice,AmountOffer? Prompt);

public static class AmountPromptProtocol {
    public const byte SInputAmount=136,CAmount=11;
    public const int MaxData=256,MaxCommand=200;
    public static AmountPromptPacket Decode(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SInputAmount)throw new ProtocolException("input amount opcode");
        uint id=r.D(),reserved=r.D();int initial=unchecked((int)r.D()),min=unchecked((int)r.D()),max=unchecked((int)r.D());
        ushort flag=r.H();string html=r.S(),cmd=r.S();
        string[] data=[];
        // 附加字串清單只在伺服器有資料時才寫（H(n)，n ≥ 1）；沒有資料時剩下的只會是補零。
        int pad=(8-((plain.Length-r.Remaining)&7))&7;
        if(r.Remaining!=pad){
            int n=r.H();if(n is <1 or >MaxData)throw new ProtocolException("input amount data count");
            data=new string[n];for(int i=0;i<n;i++)data[i]=r.S();
        }
        r.Finish();
        return new(id,reserved,initial,min,max,flag,html,cmd,data);
    }
    public static void ValidateCommand(string command){
        if(command.Length is <1 or >MaxCommand||command.Any(char.IsControl))throw new ArgumentException("數量詢問的命令字串無效。");
    }
    /// <summary>C11：objId、數量、一個 0、命令字串。數量必須 ≥ 1；命令必須能以 CP950 編碼。</summary>
    public static byte[] Reply(uint objectId,int amount,string command){
        if(amount<1)throw new ArgumentException("數量必須大於 0。");
        ValidateCommand(command);
        byte[] text=LoginPackets.TextEncoding.GetBytes(command);
        var p=new byte[10+text.Length+1];p[0]=CAmount;
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1),objectId);BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(5),amount);
        p[9]=0;text.CopyTo(p,10);
        return LoginPackets.Pad(p);
    }
    /// <summary>依目前詢問組回覆；數值不在範圍內丟 ArgumentException（原服會直接丟棄，不回任何訊息）。</summary>
    public static byte[] Reply(AmountOffer offer,long value){
        if(!offer.Answerable)throw new ArgumentException("此詢問沒有可以送出的數量。");
        if(value<offer.Lower||value>offer.Max)throw new ArgumentException($"數量必須在 {offer.Lower:N0}～{offer.Max:N0} 之間。");
        return Reply(offer.ObjectId,(int)value,offer.Command);
    }
    /// <summary>缺少 HTML 模板時顯示的文字（模板存在時不用）。</summary>
    public static string[] Fallback(AmountPromptPacket p)=>[$"（缺少輸入頁 {p.Html} 的原始繁中模板）","請在下方輸入數量。"];
    public static string Range(AmountOffer o)=>o.Answerable?$"可輸入 {o.Lower:N0}～{o.Max:N0}":$"此詢問沒有可輸入的數量（上限 {o.Max:N0}）";
}

public sealed partial class WorldSession {
    AmountOffer? amountOffer;AmountPromptPacket? amountLast;long amountRevision;string amountNotice="";
    int amountReceived,amountIgnored,amountSent;
    public long AmountPromptRevision=>amountRevision;
    public string AmountPromptNotice=>amountNotice;
    /// <summary>仍然有效的數量詢問：同一個 NPC 對話、頁面沒有換掉（InteractionRevision 相同）、目標仍在附近。</summary>
    public AmountOffer? AmountPromptCurrent=>amountOffer is {} o&&o.InteractionRevision==InteractionRevision&&InteractionKind=="dialog"&&InteractionValid&&InteractionNpc==o.ObjectId?o:null;

    [WorldReceive(AmountPromptProtocol.SInputAmount)]
    bool ReceiveAmountPrompt(byte[] plain){
        var p=AmountPromptProtocol.Decode(plain);
        amountLast=p;amountReceived++;amountRevision++;
        CheckInteraction();
        bool current=InteractionValid&&InteractionNpc==p.ObjectId;
        if(!current){
            // 原服的 S136 都是 NPC（製作、拍賣、盟屋）送的；只接受目前選取、且在附近的那個目標。
            if(p.ObjectId==0||TargetId!=p.ObjectId||!NearbyTarget){amountIgnored++;amountNotice="收到數量詢問，但不是目前附近選取的對象；已忽略。";return true;}
            if(p.Html.Length==0){amountNotice="伺服器要求關閉輸入頁。";return true;}
            CloseInteraction();InteractionNpc=p.ObjectId;interactionEpoch=WorldEpoch;interactionTargetRevision=TargetRevision;
        }
        if(p.Html.Length==0){amountOffer=null;CloseInteraction("伺服器已關閉對話。");amountNotice="伺服器要求關閉輸入頁。";return true;}
        dialogArguments=p.Data;dialogNpcName=Target?.Name??"";
        ShowPage(p.Html,AmountPromptProtocol.Fallback(p));
        interactionWaitStarted=null;
        amountOffer=new(p.ObjectId,p.Initial,p.Min,p.Max,p.Html,p.Command,p.Data,amountRevision,InteractionRevision);
        string guide=amountOffer.Answerable?$"{AmountPromptProtocol.Range(amountOffer)}；按「確定」送出一次，結果以伺服器回包為準，不會自動重送。":AmountPromptProtocol.Range(amountOffer)+"；無法送出。";
        InteractionNotice=string.Join("\n",new[]{InteractionNotice,guide}.Where(s=>s.Length>0));
        amountNotice=guide;
        return true;
    }

    /// <summary>為目前詢問組回覆封包；詢問已換掉或失效時回 null，數值不合丟 ArgumentException。</summary>
    internal byte[]? AmountRequest(long revision,long value){
        if(AmountPromptCurrent is not {} offer||offer.Revision!=revision)return null;
        return AmountPromptProtocol.Reply(offer,value);
    }
    internal void AmountSent(long revision,long value){
        if(amountOffer is not {} offer||offer.Revision!=revision)return;
        amountOffer=null;amountSent++;amountRevision++;
        AwaitInteraction();
        InteractionNotice=$"已送出數量 {value:N0}，等待原服回包；可隨時關閉，不會自動重送。";
        amountNotice=InteractionNotice;
    }

    [WorldReset]
    void ResetAmountPrompt(){amountOffer=null;amountLast=null;amountNotice="";amountReceived=amountIgnored=amountSent=0;amountRevision++;}
    [WorldSnapshot("amountPrompt")]
    object? AmountPromptSnapshotModel()=>new{revision=amountRevision,received=amountReceived,ignored=amountIgnored,sent=amountSent,notice=amountNotice,
        last=amountLast is {} p?new{objectId=p.ObjectId,reserved=p.Reserved,initial=p.Initial,min=p.Min,max=p.Max,flag=p.Flag,html=p.Html,command=p.Command,data=p.Data}:null,
        current=AmountPromptCurrent is {} o?new{objectId=o.ObjectId,html=o.Html,command=o.Command,lower=o.Lower,max=o.Max,suggested=o.Suggested,answerable=o.Answerable,revision=o.Revision}:null};
}

public sealed partial class LoginConnection {
    bool AmountAccess=>State==LoginState.InWorld&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
    public AmountPromptView AmountPromptSnapshot()=>CaptureWorld(()=>{
        var o=World.AmountPromptCurrent;
        return new AmountPromptView(generation,World.WorldEpoch,World.Actor?.ObjectId??0,World.AmountPromptRevision,
            o is {Answerable:true}&&AmountAccess&&!interactionSending&&sendMovementGate!=null,World.AmountPromptNotice,o);
    });
    /// <summary>回覆目前的數量詢問（C11）。v 必須是目前的快照；數值不在範圍內不送。</summary>
    public async Task<bool> SendAmountReply(AmountPromptView v,long value){
        if(v.Prompt is not {} offer)return false;
        bool Valid()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&AmountAccess&&World.AmountPromptCurrent?.Revision==offer.Revision;
        if(interactionSending||sendMovementGate==null||!CaptureWorld(Valid))return false;
        byte[]? packet;
        try{packet=CaptureWorld(()=>World.AmountRequest(offer.Revision,value));}
        catch(ArgumentException){return false;}
        if(packet==null)return false;
        var sender=sendMovementGate;int mine=generation;interactionSending=true;
        try{return await sender(packet,()=>CaptureWorld(Valid),()=>{World.AmountSent(offer.Revision,value);Set(State,World.InteractionNotice);})&&mine==generation;}
        catch(Exception){if(mine==generation){Cancel();Set(LoginState.Faulted,"數量回覆傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(mine==generation)interactionSending=false;}
    }
}

// 畫面：在既有 NPC 對話面板（NpcUi.cs 的 npcPanel）底部加一列數字框與「確定」鈕，不另開 Form。
// HTML 頁本身由既有的 NPC 頁面顯示（InteractionPage）。原版數字框的位置與外觀待 T190 原生 HYPER_TEXT_WINDOW 版面。
public sealed partial class ClientForm {
    bool amountUiHooked,amountUiSending;long amountUiRevision=-1;
    FlowLayoutPanel? amountRow;NumericUpDown? amountInput;Button? amountSend;Label? amountRange;
    // OnLoad、OnShown、OnVisibleChanged、OnCreateControl、OnHandleCreated、OnLayout、OnActivated、OnResize 已由其他模組使用。
    protected override void OnSizeChanged(EventArgs e){
        base.OnSizeChanged(e);
        if(!amountUiHooked){amountUiHooked=true;Shown+=(_,_)=>InitAmountPromptUi();}
    }
    void InitAmountPromptUi(){
        if(amountRow!=null)return;
        amountRange=new Label{Width=170,Height=30,ForeColor=Color.Khaki,TextAlign=ContentAlignment.MiddleLeft};
        amountInput=new NumericUpDown{Width=140,Minimum=0,Maximum=int.MaxValue,ThousandsSeparator=true,AccessibleName="輸入數量"};
        amountSend=new Button{Text="確定",Width=100,Height=30};
        amountRow=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=40,Visible=false,WrapContents=false,Padding=new(4),BackColor=Color.FromArgb(30,50,54)};
        amountRow.Controls.AddRange([amountRange,amountInput,amountSend]);
        npcPanel.Controls.Add(amountRow);
        // 排在 npcContent（Fill）之後、npcNotice 之前，畫面上位於頁面內容下方、提示文字上方。
        npcPanel.Controls.SetChildIndex(amountRow,1);
        amountSend.Click+=async(_,_)=>await SubmitAmountPrompt();
        amountInput.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.Handled=true;e.SuppressKeyPress=true;await SubmitAmountPrompt();}};
        amountRow.Enter+=(_,_)=>LeaveGameFocus("amount");
        timer.Tick+=(_,_)=>SyncAmountPrompt();
    }
    void SyncAmountPrompt(){
        if(amountRow==null||amountInput==null||amountSend==null||amountRange==null)return;
        var v=npcPanel.Visible?login.AmountPromptSnapshot():null;
        bool show=v?.Prompt!=null;
        if(amountRow.Visible!=show)amountRow.Visible=show;
        if(!show){amountUiRevision=-1;return;}
        var o=v!.Prompt!;
        if(amountUiRevision!=o.Revision){
            amountUiRevision=o.Revision;
            if(o.Answerable){amountInput.Minimum=0;amountInput.Maximum=o.Max;amountInput.Minimum=o.Lower;amountInput.Value=o.Suggested;}
            else{amountInput.Minimum=0;amountInput.Maximum=0;amountInput.Value=0;}
            amountRange.Text=AmountPromptProtocol.Range(o);
        }
        amountInput.Enabled=o.Answerable;
        amountSend.Enabled=v.CanSend&&!amountUiSending&&!World.Walking;
    }
    async Task SubmitAmountPrompt(){
        if(amountInput==null||amountUiSending||World.Walking)return;
        var v=login.AmountPromptSnapshot();if(v.Prompt==null||!v.CanSend)return;
        ClearInput("amount-submit");amountUiSending=true;
        try{await login.SendAmountReply(v,(long)amountInput.Value);}
        finally{amountUiSending=false;}
        SyncInteraction();lastWebState="";PublishWebState();SyncAmountPrompt();
    }
}
