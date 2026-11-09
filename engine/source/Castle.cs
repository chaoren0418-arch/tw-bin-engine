using System.Buffers.Binary;
namespace P06;

// M7-C（T205／#511）：城堡城主、稅率、金庫。協定、資料與文字通知。
// W2-B（T235／#557）：S185／S141／S4 在 NPC 對話面板開 tax／withdraw／deposit 頁；王冠只做資料（12 格、500 ms、錨點），圖源待查不畫。
// R2-F（T261／#622）：頁內的數字框與送出鈕接 SendCastleReply（WinForms NPC 面板與 web 面板都有；原生 HYPER_TEXT_WINDOW 版面仍待 T190），
// 只在頁還開著時能送（SendCastlePageReply）；拿掉暫代聊天行「[城堡]」（原版沒有這些文字，issue-511），通知只留在 castle 快照。
// 稅率數字框範圍 10–50，初始值：3.81 不送 S185 第 3 個 C（SS2§8），由畫面決定＝下限。
// 原版行為依 docs/reference/windows-2.md §5.1（T226）：S69 在城堡編號 < 9 時寫入城主表 table[城堡]=物件 id；
// 只有第 1、3–8 格畫王冠，第 2 格不畫；type 0 只寫第 0 格（不會清掉其他格）。
// 格式依 docs/reference/server-protocol.md（#132）：
//   S69  S_CastleMaster  C(69) C(type) D(leaderId)         type＝城堡編號；原服失城送 0，妖魔城（2）改送 8 以便顯示王冠。
//   S185 S_TaxRate       C(185) D(objId) C(10) C(50)       objId＝自己角色 id；後兩個 byte 是可設定稅率下限／上限（TW16 多讀一個 C，落在補零，意義待驗證）。
//   S4   S_Deposit       C(4) D(objId)                     打開「存入金庫」。
//   S141 S_Drawal        C(141) D(objId) D(count)          打開「提領金庫」；count＝可提領上限（原服已截在 2,000,000,000）。
//   C19  C_TaxRate       C(19) D(objId) C(rate)            原服只收 objId＝自己角色 id、10 ≤ rate ≤ 50。
//   C56  C_Deposit       C(56) D(objId) D(count)           原服只收 1 ≤ count ≤ int.MaxValue。
//   C44  C_Drawal        C(44) D(objId) D(count)           原服只收 1 ≤ count ≤ S141 的 count。
// 原服每次詢問只發一張一次性票（takeCurrent），所以每個詢問最多回一次；回覆或取消後就清掉。

public enum CastleOfferKind:byte {TaxRate=1,Deposit=2,Withdraw=3}
/// <summary>城主表一格。Crowned＝原版會畫王冠（第 1、3–8 格）。</summary>
public sealed record CastleCrown(byte Castle,uint Leader,bool Crowned);
public sealed record CastleOffer(CastleOfferKind Kind,uint ObjectId,long Min,long Max,long Revision);
public sealed record CastleView(int Generation,int WorldEpoch,uint Actor,long Revision,bool CanSend,string Notice,CastleOffer? Offer,CastleCrown[] Crowns,CastlePage? Page=null);
/// <summary>W2-B（T235）：S185／S141／S4 在 NPC 對話面板開的 HTML 頁。Html＝templates.json 的頁名，Args＝&lt;var src="#n"&gt; 參數（`$n` 由畫面依 names.json 解析）。</summary>
public sealed record CastlePage(CastleOfferKind Kind,string Html,string[] Args);
/// <summary>王冠一格的資料（圖源未查到，不畫）：Frame＝0–11，每格 500 ms，各城自己計時。</summary>
public sealed record CastleCrownFrame(byte Castle,uint Leader,int Frame);

public static class CastleProtocol {
    public const byte SCastleMaster=69,STaxRate=185,SDrawal=141,SDeposit=4,CTaxRate=19,CDrawal=44,CDeposit=56;
    public const long DepositMax=int.MaxValue,WithdrawCap=2_000_000_000;
    // 城堡編號依原服 L1CastleLocation（1 肯特 … 8 狄亞得）。原服把妖魔城城主改送 8（第 2 格原版不畫王冠），所以第 8 格的文字只能照編號，可能其實是妖魔城。
    public static string CastleName(byte id)=>id switch{
        1=>"肯特城",2=>"妖魔城",3=>"風木城",4=>"奇巖城",5=>"海音城",6=>"侏儒城",7=>"亞丁城",8=>"狄亞得要塞",_=>$"城堡 {id}"};

    public const int MasterSlots=9;
    public static bool CrownSlot(byte castle)=>castle is 1 or >= 3 and <= 8;
    public static (byte Castle,uint Leader) DecodeCastleMaster(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SCastleMaster)throw new ProtocolException("castle master opcode");
        byte castle=r.C();uint leader=r.D();r.Finish();
        return (castle,leader);
    }
    public static (uint ObjectId,byte Min,byte Max) DecodeTaxRate(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=STaxRate)throw new ProtocolException("tax rate opcode");
        uint id=r.D();byte min=r.C(),max=r.C();r.Finish();
        if(id==0||min>max)throw new ProtocolException("tax rate fields");
        return (id,min,max);
    }
    public static uint DecodeDeposit(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SDeposit)throw new ProtocolException("deposit opcode");
        uint id=r.D();r.Finish();if(id==0)throw new ProtocolException("deposit object");
        return id;
    }
    public static (uint ObjectId,long Count) DecodeDrawal(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SDrawal)throw new ProtocolException("drawal opcode");
        uint id=r.D();int count=unchecked((int)r.D());r.Finish();
        if(id==0||count<0)throw new ProtocolException("drawal fields");
        return (id,count);
    }

    static byte[] IdAndCount(byte opcode,uint id,long count){
        var p=new byte[9];p[0]=opcode;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1),id);BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(5),checked((int)count));
        return LoginPackets.Pad(p);
    }
    /// <summary>依目前詢問組出回覆；數值不在詢問的範圍內丟 ArgumentException（原服會直接丟棄，不回任何訊息）。</summary>
    public static byte[] Reply(CastleOffer offer,long value){
        if(value<offer.Min||value>offer.Max)throw new ArgumentException(offer.Kind==CastleOfferKind.TaxRate
            ?$"稅率必須在 {offer.Min}%～{offer.Max}% 之間。":$"金額必須在 {offer.Min:N0}～{offer.Max:N0} 之間。");
        return offer.Kind switch{
            CastleOfferKind.TaxRate=>TaxRate(offer.ObjectId,(byte)value),
            CastleOfferKind.Deposit=>IdAndCount(CDeposit,offer.ObjectId,value),
            CastleOfferKind.Withdraw=>IdAndCount(CDrawal,offer.ObjectId,value),
            _=>throw new ArgumentException("未知城堡操作。")};
    }
    public static byte[] TaxRate(uint id,byte rate){
        var p=new byte[6];p[0]=CTaxRate;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1),id);p[5]=rate;
        return LoginPackets.Pad(p);
    }
    public static byte[] Deposit(uint id,long count)=>IdAndCount(CDeposit,id,count);
    public static byte[] Withdraw(uint id,long count)=>IdAndCount(CDrawal,id,count);

    // W2-B（windows-2.md §5.1）：TW16 以 NPC 對話窗開頁；頁名在 .rdata（tax：VA 0x1183F78／FO 0xD82B78）。
    public const string TaxPage="tax",WithdrawPage="withdraw",DepositPage="deposit";
    public static string PageName(CastleOfferKind kind)=>kind switch{CastleOfferKind.TaxRate=>TaxPage,CastleOfferKind.Deposit=>DepositPage,_=>WithdrawPage};
    /// <summary>提領頁 #1：TW16 以「%s : %d %s」組字串 457（寶庫）與 4（金幣）。</summary>
    public static string WithdrawLine(long count)=>$"$457 : {count} $4";
    /// <summary>存入頁 #1：TW16 顯示目前持有金額。數字後接字串 4（金幣）的排法是推定（待驗證）。</summary>
    public static string DepositLine(long gold)=>$"{gold} $4";

    // 王冠（windows-2.md §5.1，sub_F08880）：12 格、每格 500 ms；錨點 x＋17、y − 高度 − 12，高度一般 40，特定外型 50。
    // 依外型類別、方向、動作的偏移表（dword_138CCC8 等）內容未取得，不在這裡套用。
    public const int CrownFrames=12,CrownFrameMs=500,CrownDx=17,CrownDy=12,CrownHeight=40,CrownTallHeight=50;
    public static int CrownFrame(long elapsedMs)=>elapsedMs<0?0:(int)(elapsedMs/CrownFrameMs%CrownFrames);
    public static (int X,int Y) CrownAnchor(int x,int y,bool tall)=>(x+CrownDx,y-(tall?CrownTallHeight:CrownHeight)-CrownDy);

    public static string Prompt(CastleOffer offer)=>offer.Kind switch{
        CastleOfferKind.TaxRate=>$"城堡管家詢問稅率設定（可設定 {offer.Min}%～{offer.Max}%）。",
        CastleOfferKind.Deposit=>"城堡管家詢問要存入金庫的金幣數量。",
        _=>$"城堡管家詢問要從金庫提領的金幣數量（最多 {offer.Max:N0}）。"};
    /// <summary>數字框初始值（畫面決定，SS2§8）：一律用詢問的下限（稅率 10、金額 1）。</summary>
    public static long Initial(CastleOffer offer)=>offer.Min;
    public static string Range(CastleOffer offer)=>offer.Kind==CastleOfferKind.TaxRate?$"稅率 {offer.Min}%～{offer.Max}%":$"金額 {offer.Min:N0}～{offer.Max:N0}";
    public static string Sent(CastleOfferKind kind,long value)=>kind switch{
        CastleOfferKind.TaxRate=>$"已送出稅率設定 {value}%，等待伺服器處理。",
        CastleOfferKind.Deposit=>$"已送出存入金庫 {value:N0} 金幣，等待伺服器處理。",
        _=>$"已送出從金庫提領 {value:N0} 金幣，等待伺服器處理。"};
}

public sealed partial class WorldSession {
    readonly uint[] castleMasters=new uint[CastleProtocol.MasterSlots];
    readonly long[] castleCrownStart=new long[CastleProtocol.MasterSlots];
    CastleOffer? castleOffer;long castleRevision;string castleNotice="";
    CastlePage? castlePage;int castleQueryRevision=-1;
    internal Func<long> CastleClock=()=>Environment.TickCount64;
    public CastleOffer? CastleOffer=>castleOffer;
    /// <summary>最近一次由 S185／S141／S4 開的 HTML 頁；面板被關掉或換頁後為 null。</summary>
    public CastlePage? CastlePage=>castlePage!=null&&QueryRevision==castleQueryRevision?castlePage:null;
    /// <summary>某物件頭上的王冠資料（第 1、3–8 格，各城一筆）。圖源待驗證，畫面不畫。</summary>
    public CastleCrownFrame[] CastleCrownFrames(uint objectId){
        if(objectId==0)return [];long now=CastleClock();
        return CastleCrowns.Where(c=>c.Crowned&&c.Leader==objectId).Select(c=>new CastleCrownFrame(c.Castle,c.Leader,CastleProtocol.CrownFrame(now-castleCrownStart[c.Castle]))).ToArray();
    }
    public long CastleRevision=>castleRevision;
    public string CastleNotice=>castleNotice;
    /// <summary>城主表第 1–8 格中有物件 id 的格子（第 0 格是失城通知寫入處，不列出）。</summary>
    public CastleCrown[] CastleCrowns=>Enumerable.Range(1,CastleProtocol.MasterSlots-1).Where(i=>castleMasters[i]!=0)
        .Select(i=>new CastleCrown((byte)i,castleMasters[i],CastleProtocol.CrownSlot((byte)i))).ToArray();
    public uint CastleMaster(byte castle)=>castle<CastleProtocol.MasterSlots?castleMasters[castle]:0;
    /// <summary>原版是否會在這個物件頭上畫王冠（物件 id 等於第 1、3–8 格之一）。</summary>
    public bool HasCastleCrown(uint objectId)=>objectId!=0&&CastleCrowns.Any(c=>c.Crowned&&c.Leader==objectId);

    // R2-F：不再寫聊天區（原版沒有這些文字）；只更新 castle 快照與面板上的提示。
    void CastleLine(string text){castleNotice=text;castleRevision++;}
    /// <summary>頁內數字框可用的詢問：城堡頁仍顯示在面板上（同一個查詢頁、同一個世界）且頁的種類和詢問相同。</summary>
    public CastleOffer? CastlePageOffer=>castleOffer is {} o&&QueryVisible&&CastlePage is {} p&&p.Kind==o.Kind?o:null;
    string CastleLeaderName(uint leader)=>objects.TryGetValue(leader,out var o)&&!string.IsNullOrWhiteSpace(o.Name)?PlayerChatProtocol.Display(o.Name):$"#{leader}";

    [WorldReceive(CastleProtocol.SCastleMaster)]
    bool ReceiveCastleMaster(byte[] plain){
        var (castle,leader)=CastleProtocol.DecodeCastleMaster(plain);
        // 原版：編號 ≥ 9 不寫入；同值重送不另行通知。
        if(castle>=CastleProtocol.MasterSlots||castleMasters[castle]==leader){castleRevision++;return true;}
        castleMasters[castle]=leader;castleCrownStart[castle]=CastleClock();
        // 第 0 格不畫王冠也不清別格（原服失城通知）；本引擎只給文字，不推測王冠會消失。
        if(castle==0)CastleLine($"伺服器通知：{CastleLeaderName(leader)} 的血盟失去城堡。");
        else if(leader==0)CastleLine($"{CastleProtocol.CastleName(castle)}目前沒有城主。");
        else CastleLine($"{CastleLeaderName(leader)} 成為{CastleProtocol.CastleName(castle)}城主。");
        return true;
    }
    void OpenCastleOffer(CastleOfferKind kind,uint id,long min,long max){
        castleOffer=new(kind,id,min,max,castleRevision+1);
        CastleLine(CastleProtocol.Prompt(castleOffer));
    }
    // TW16 把欄位寫進 NPC 對話窗再開頁（模式 1／2／3）。引擎的 NPC 面板以 QueryPage 承載伺服器主動送的頁（WIN§2.1，同一個 HYPER_TEXT_WINDOW），
    // 不覆寫進行中的 NPC 對話或商店。#0＝目前對話 NPC 的名稱（原始 `$n` 名稱，畫面解析）；查不到就留空，不拿自己的名字代替。
    string CastleNpcName()=>InteractionNpc is uint n&&objects.TryGetValue(n,out var o)?o.Name??"":"";
    long CastleGoldHeld()=>inventory.Values.Where(i=>i.DescriptionId==0&&i.IconId==5&&(i.Name=="$4"||i.Name.StartsWith("$4 (",StringComparison.Ordinal))).Sum(i=>(long)i.Count);
    void OpenCastlePage(CastleOfferKind kind,params string[] extra){
        string[] args=[CastleNpcName(),..extra];
        string html=CastleProtocol.PageName(kind);
        castlePage=new(kind,html,args);
        ShowQueryPage(Actor?.ObjectId??0,html,args,[]);
        castleQueryRevision=QueryRevision;
    }
    void CloseCastlePage(){
        if(castlePage!=null&&QueryRevision==castleQueryRevision)CloseQueryPage();
        castlePage=null;castleQueryRevision=-1;
    }
    [WorldReceive(CastleProtocol.STaxRate)]
    bool ReceiveCastleTaxRate(byte[] plain){var (id,min,max)=CastleProtocol.DecodeTaxRate(plain);OpenCastleOffer(CastleOfferKind.TaxRate,id,min,max);OpenCastlePage(CastleOfferKind.TaxRate);return true;}
    [WorldReceive(CastleProtocol.SDeposit)]
    bool ReceiveCastleDeposit(byte[] plain){
        OpenCastleOffer(CastleOfferKind.Deposit,CastleProtocol.DecodeDeposit(plain),1,CastleProtocol.DepositMax);
        OpenCastlePage(CastleOfferKind.Deposit,CastleProtocol.DepositLine(CastleGoldHeld()));return true;
    }
    [WorldReceive(CastleProtocol.SDrawal)]
    bool ReceiveCastleDrawal(byte[] plain){
        var (id,count)=CastleProtocol.DecodeDrawal(plain);
        // 原版照樣開 withdraw 頁並顯示金額；金額 0 時不開詢問（沒有可提領的範圍）。
        if(count<1){castleOffer=null;CastleLine("城堡金庫目前沒有可以提領的金幣。");OpenCastlePage(CastleOfferKind.Withdraw,CastleProtocol.WithdrawLine(0));return true;}
        OpenCastleOffer(CastleOfferKind.Withdraw,id,1,Math.Min(count,CastleProtocol.WithdrawCap));
        OpenCastlePage(CastleOfferKind.Withdraw,CastleProtocol.WithdrawLine(count));return true;
    }

    /// <summary>為目前詢問組回覆封包；詢問已換掉（revision 不同）或角色不符時回 null。</summary>
    internal byte[]? CastleRequest(long revision,uint actor,long value){
        if(castleOffer is not {} offer||offer.Revision!=revision)return null;
        if(actor==0||actor!=offer.ObjectId)return null;
        return CastleProtocol.Reply(offer,value);
    }
    internal void CastleSent(long revision,long value){
        if(castleOffer is not {} offer||offer.Revision!=revision)return;
        castleOffer=null;CloseCastlePage();CastleLine(CastleProtocol.Sent(offer.Kind,value));
    }
    /// <summary>不回覆就關掉詢問（原服的票會自然失效，不送任何封包）。</summary>
    public bool CancelCastleOffer(long revision){
        if(castleOffer is not {} offer||offer.Revision!=revision)return false;
        castleOffer=null;CloseCastlePage();castleNotice="已取消城堡操作。";castleRevision++;return true;
    }
    internal CastleView CastleSnapshot(int generation,bool canSend)=>new(generation,WorldEpoch,Actor?.ObjectId??0,castleRevision,canSend&&castleOffer!=null,castleNotice,castleOffer,CastleCrowns,CastlePage);

    [WorldReset]
    void ResetCastle(){Array.Clear(castleMasters);Array.Clear(castleCrownStart);castleOffer=null;castlePage=null;castleQueryRevision=-1;castleNotice="";castleRevision++;}
    [WorldSnapshot("castle")]
    object? CastleSnapshotModel()=>new{revision=castleRevision,notice=castleNotice,
        offer=castleOffer is {} o?new{kind=o.Kind.ToString(),objectId=o.ObjectId,min=o.Min,max=o.Max,revision=o.Revision}:null,
        page=CastlePage is {} p?new{kind=p.Kind.ToString(),html=p.Html,args=p.Args}:null,
        masters=CastleCrowns.Select(c=>new{castle=c.Castle,leader=c.Leader,crowned=c.Crowned,name=CastleProtocol.CastleName(c.Castle)}).ToArray(),lostNotice=castleMasters[0]};
}

public sealed partial class LoginConnection {
    int castleSendingGeneration=-1;
    bool CastleAccess=>State==LoginState.InWorld&&World.Ready&&World.Actor is {} a&&World.Status is {Hp:>0} s&&a.ObjectId==s.ObjectId
        &&!World.OwnDead&&!World.TeleportPending&&!World.ActionLocked;
    public CastleView CastleSnapshot()=>CaptureWorld(()=>World.CastleSnapshot(generation,CastleAccess&&castleSendingGeneration!=generation));
    public bool CancelCastleOffer(CastleView v)=>CaptureWorld(()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&v.Offer is {} o&&World.CancelCastleOffer(o.Revision));
    /// <summary>回覆目前的城堡詢問（稅率 C19／存入 C56／提領 C44）。v 必須是目前的快照；value 不在範圍內不送。</summary>
    public async Task<bool> SendCastleReply(CastleView v,long value){
        if(v.Offer is not {} offer)return false;
        bool Valid()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&v.Actor==World.Actor?.ObjectId&&CastleAccess&&World.CastleOffer?.Revision==offer.Revision;
        if(castleSendingGeneration==generation||sendMovementGate==null||!CaptureWorld(Valid))return false;
        byte[]? packet;
        try{packet=CaptureWorld(()=>World.Actor is {} own?World.CastleRequest(offer.Revision,own.ObjectId,value):null);}
        catch(ArgumentException){return false;}
        if(packet==null)return false;
        var sender=sendMovementGate;int mine=generation;castleSendingGeneration=mine;
        try{return await sender(packet,()=>CaptureWorld(Valid),()=>{World.CastleSent(offer.Revision,value);Changed?.Invoke();})&&v.Generation==generation;}
        catch(Exception){if(mine==generation){Cancel();Set(LoginState.Faulted,"城堡操作傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(castleSendingGeneration==mine)castleSendingGeneration=-1;}
    }
    /// <summary>R2-F：頁內「確定」鈕。epoch／page＝畫面顯示時的世界與查詢頁版本，offer＝詢問版本；頁已關、換頁或換詢問都不送。</summary>
    public Task<bool> SendCastlePageReply(int epoch,int page,long offer,long value){
        var v=CastleSnapshot();
        bool current=CaptureWorld(()=>v.WorldEpoch==epoch&&World.QueryRevision==page&&World.CastlePageOffer is {} o&&o.Revision==offer&&v.Offer?.Revision==offer);
        return current?SendCastleReply(v,value):Task.FromResult(false);
    }
    /// <summary>R2-F：頁內「取消」鈕。不送封包（原服的票會自然失效），關掉城堡頁。</summary>
    public bool CancelCastlePage(int epoch,int page,long offer){
        var v=CastleSnapshot();
        return CaptureWorld(()=>v.WorldEpoch==epoch&&World.QueryRevision==page&&World.CastlePageOffer?.Revision==offer)&&CancelCastleOffer(v);
    }
}

// R2-F：城堡頁內的數字框與送出鈕。放在既有 NPC 面板（npcPanel）底部，與 S136 的數量列同一種排法；原版位置與外觀待 T190。
public sealed partial class ClientForm {
    FlowLayoutPanel? castleRow;NumericUpDown? castleInput;Button? castleSend,castleCancel;Label? castleRange;
    long castleUiOffer=-1;bool castleUiSending;
    void InitCastlePromptUi(){
        if(castleRow!=null)return;
        castleRange=new Label{Width=150,Height=30,ForeColor=Color.Khaki,TextAlign=ContentAlignment.MiddleLeft};
        castleInput=new NumericUpDown{Width=120,Minimum=0,Maximum=int.MaxValue,ThousandsSeparator=true,AccessibleName="城堡數值"};
        castleSend=new Button{Text="確定",Width=70,Height=30};castleCancel=new Button{Text="取消",Width=70,Height=30};
        castleRow=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=40,Visible=false,WrapContents=false,Padding=new(4),BackColor=Color.FromArgb(30,50,54)};
        castleRow.Controls.AddRange([castleRange,castleInput,castleSend,castleCancel]);
        npcPanel.Controls.Add(castleRow);npcPanel.Controls.SetChildIndex(castleRow,1);
        castleSend.Click+=async(_,_)=>await SubmitCastlePrompt();
        castleCancel.Click+=(_,_)=>CancelCastlePrompt();
        castleInput.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.Handled=true;e.SuppressKeyPress=true;await SubmitCastlePrompt();}};
        castleRow.Enter+=(_,_)=>LeaveGameFocus("castle");
    }
    void SyncCastlePrompt(){
        if(castleRow==null||castleInput==null||castleSend==null||castleCancel==null||castleRange==null)return;
        var w=login.World;var o=npcPanel.Visible?w.CastlePageOffer:null;
        if(castleRow.Visible!=(o!=null))castleRow.Visible=o!=null;
        if(o==null){castleUiOffer=-1;return;}
        if(castleUiOffer!=o.Revision){
            castleUiOffer=o.Revision;castleInput.Minimum=0;castleInput.Maximum=o.Max;castleInput.Minimum=o.Min;castleInput.Value=CastleProtocol.Initial(o);
            castleRange.Text=CastleProtocol.Range(o);
        }
        castleSend.Enabled=login.CastleSnapshot().CanSend&&!castleUiSending&&!World.Walking;castleCancel.Enabled=!castleUiSending;
    }
    async Task SubmitCastlePrompt(){
        if(castleInput==null||castleUiSending||World.Walking||login.World.CastlePageOffer is not {} o)return;
        ClearInput("castle-submit");castleUiSending=true;
        try{await login.SendCastlePageReply(login.World.WorldEpoch,login.World.QueryRevision,o.Revision,(long)castleInput.Value);}
        finally{castleUiSending=false;}
        SyncInteraction();lastWebState="";PublishWebState();
    }
    void CancelCastlePrompt(){
        if(login.World.CastlePageOffer is not {} o)return;
        login.CancelCastlePage(login.World.WorldEpoch,login.World.QueryRevision,o.Revision);
        SyncInteraction();lastWebState="";PublishWebState();
    }
    // web 面板（interaction.js）用的城堡頁數字框資料；頁不在面板上時為 null。
    object? CastlePromptWeb(){
        var w=login.World;if(w.CastlePageOffer is not {} o)return null;
        return new{offer=o.Revision,page=w.QueryRevision,kind=o.Kind.ToString(),min=o.Min,max=o.Max,initial=CastleProtocol.Initial(o),range=CastleProtocol.Range(o),canSend=login.CastleSnapshot().CanSend&&!World.Walking,notice=w.CastleNotice};
    }
}
