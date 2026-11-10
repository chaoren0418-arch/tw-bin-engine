using P06.NativeUi;
namespace P06;

// ---------------------------------------------------------------------------------------------------------------------
// R2-N（T259／#620）：把 M7-T 的 PersonalShopPanel（WIN§1.1 PersonalShopUI）接上產品。
// - 註冊：T270（#645）之後由 [UiPanel] 自動發現（見下方 PersonalShopPanelRegistration），不改 UiPanelRegistry.cs。
// - 選格：面板點出售／收購格 → Setup.Selected；過渡用的「個人商店」頁（舊 WinForms）以背包目前選取的物品＋單價＋數量
//   呼叫 WorldSession.PersonalShopCandidate 放進該格（原版的拖放與單價／數量輸入窗未查證，不做假畫面）。
// - 送出：面板 OK → Confirmed(C38 type 0) → 送出前每筆重新以背包核對 → LoginConnection.SendPersonalShop。
// - 關店：C38 type 1（PersonalShopPackets.Close）。伺服器對開店／關店沒有專用確認回包，本端只記「已送出」。
// - 物品圖示：ClientForm.ItemIcon（背包同一來源）。
// 推測（報告列出）：OK 送出成功後關閉設定窗、保留設定以便再開；開窗入口原版待查證（這裡由過渡頁按鈕開窗）。
//
// R9-J（T370／#854，FU9§9.4、gap-matrix 15.11）個人商店流程：
// - 放物品：原版從背包拖進窗（sub_98F330）。引擎沒有跨窗拖曳 → **替代做法**：背包先選取物品，再點設定窗的出售／收購空格。
// - 單價／數量在聊天輸入列（已驗證）：先問 898；解析失敗 938、單價 ≥ 10,000,000 印 900、收購單價超過持有金幣 936；
//   物品是堆疊時再問 933（%s＝934「販售」／935「購買」，Enter＝全部、Esc＝取消）。提示由 PersonalShopPromptPanel 畫在
//   聊天輸入列的位置；按鍵與字元由 PersonalShopPanel 轉進來（聊天送出流程 PlayerChat.cs 不在本任務範圍，不經過它）。
// - 放入檢查（sub_DDED00）：本端做得到的只有 932（裝備中）、940（已在清單）；1181／1188／941／1497／937 要物品屬性，交給伺服器。
// - 雙擊移除：PersonalShopPanel（Pick 515）。
// - OK 檢查（sub_DE1AD0）：本端做 876（地圖 303／70）、904（出售總額，上限值推測）＋既有的背包重核；3425／3405／334／927／
//   4393 需要伺服器狀態或未查清，不做。原版「已開店時 OK 只關窗、Cancel＝關店」要知道真的開店了（自己的動作 70），
//   引擎沒有這個狀態（OpenSent 只代表已送出），**不做**。
// - 訊息：PersonalShopPackets.Chat（PrivateShop.cs）改成 0xFF 分隔、0x00 結尾；外型（Poly）不送（C_Shop 不讀）。
// 推測（報告列出）：「堆疊」以背包數量 > 1 判斷；收購數量上限＝持有金幣 ÷ 單價；輸入錯誤後提示留著重打；900 的 %d 填 10000000。
// ---------------------------------------------------------------------------------------------------------------------

/// <summary>R2-N（#620）：PersonalShop_Window 註冊（順序 115：TRADE_WINDOW 110 之後、Friend 120 之前）。</summary>
static class PersonalShopPanelRegistration {
    [UiPanel(115)] internal static UiPanelRegistration Registration=>PersonalShopPanel.Registration;
    /// <summary>R9-J（#854）：單價／數量提示（聊天輸入列位置）。共用 PersonalShopUI 版面，不讀任何元素。</summary>
    [UiPanel(116)] internal static UiPanelRegistration Prompt=>PersonalShopPromptPanel.Registration;
}

/// <summary>控制器需要的外部能力；產品由 ClientForm 實作，離線測試用假的。</summary>
public interface IPersonalShopHost {
    /// <summary>連線世代；改變時清掉本機設定（換角／重登）。</summary>
    int Generation{get;}
    bool InWorld{get;}
    /// <summary>可以送封包（在世界中、沒有傳送中、沒有正在送的個人商店封包）。</summary>
    bool CanSend{get;}
    uint? SelectedItemId{get;}
    PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell);
    Task<bool> Send(byte[] packet);
    Bitmap? Icon(ushort iconId);
    // R9-J（#854）：以下有預設值（不知道＝null／不做事），舊的實作不必改。
    /// <summary>背包裡這個物件的數量；不在背包或不知道＝null（不知道時不問數量，數量 1）。</summary>
    uint? HeldCount(uint objectId)=>null;
    /// <summary>持有金幣；不知道＝null（不做 936 檢查）。</summary>
    long? Adena=>null;
    /// <summary>目前地圖；不知道＝null（不做 876 檢查）。</summary>
    int? MapId=>null;
    /// <summary>提示開始：鍵盤離開遊戲（數字鍵不當快捷鍵）。</summary>
    void BeginTextInput(){}
    /// <summary>提示結束：鍵盤還給遊戲。</summary>
    void EndTextInput(){}
}

/// <summary>R9-J（#854）：字串表（string-c.tbl）的提示；讀不到表時用本機 Text.pak 原文（PakViewer 讀出，見報告）。</summary>
public static class PersonalShopText {
    public const int Price=898,PriceTooHigh=900,TotalTooHigh=904,CannotOpenHere=876,Equipped=932,Count=933,SellWord=934,BuyWord=935,NoAdena=936,DigitsOnly=938,Listed=940;
    /// <summary>單價 ≥ 這個值印 900（FU9§9.4 已驗證）。</summary>
    public const uint PriceLimit=10_000_000;
    /// <summary>904 的上限（推測：同 C_Shop 收購單價上限；BIN 傳給 %d 的值未查）。</summary>
    public const long TotalLimit=2_000_000_000;
    /// <summary>876：不能開店的地圖（sub_DE1AD0，FU9§9.4）。</summary>
    public static readonly int[] ClosedMaps=[303,70];
    static readonly Dictionary<int,string> Fallback=new(){
        [Price]="請設定1件物品的價格。",[PriceTooHigh]="1件物品的價格無法超過 %d金幣。",[TotalTooHigh]="總共販賣價格無法超過 %d金幣。",
        [CannotOpenHere]="無法在此開設個人商店。",[Equipped]="裝備中的道具無法交易。",[Count]="想要\\f2%s\\f>幾個呢？(1-%u，Enter=全部，Esc=取消)",
        [SellWord]="販售",[BuyWord]="購買",[NoAdena]="金幣不足，無法購買。",[DigitsOnly]="只能輸入數字。",[Listed]="已經陳列到視窗了。"};
    /// <summary>C sprintf 的 %s／%d／%u 依序代入（%% → %），再去掉色碼（\fN、\f&gt;）。</summary>
    public static string Get(int id,params object[] args){
        string raw=ServerMessageText.Table is {} t&&id<t.Count&&t[id].Trim().Length>0?t[id]:Fallback.GetValueOrDefault(id,$"訊息 {id}");
        var sb=new System.Text.StringBuilder();int next=0;
        for(int i=0;i<raw.Length;i++){
            if(raw[i]!='%'||i+1>=raw.Length){sb.Append(raw[i]);continue;}
            char k=raw[++i];
            if(k is 's' or 'd' or 'u'){if(next<args.Length)sb.Append(Convert.ToString(args[next],System.Globalization.CultureInfo.InvariantCulture));next++;}
            else sb.Append(k);
        }
        return ServerMessageText.DisplayText(sb.ToString()).Trim();
    }
}

/// <summary>R9-J（#854）：聊天輸入列上的單價（898）→ 數量（933）提示狀態。</summary>
public sealed record PersonalShopPrompt(bool Sell,int Slot,uint ObjectId,string Name,bool AskCount,bool CountStep,uint Price,uint MaxCount,string Typed,string Text) {
    public const int MaxTyped=10;
    public string Line=>Text+" "+Typed;
}

public sealed class PersonalShopController(IPersonalShopHost host):IPersonalShopPromptInput {
    public PersonalShopSetup Setup{get;}=new();
    public PersonalShopPanel? Panel{get;private set;}
    public string Notice{get;private set;}="";
    public bool Sending{get;private set;}
    /// <summary>本端最後送出的是開店（true）或關店／尚未開店（false）。伺服器沒有確認回包，只代表「已送出」。</summary>
    public bool OpenSent{get;private set;}
    public int Revision{get;private set;}
    public int SentPackets{get;private set;}
    int generation=int.MinValue;

    void Say(string m){Notice=m;Revision++;}
    public void Attach(PersonalShopPanel panel){
        if(ReferenceEquals(Panel,panel))return;
        if(Panel!=null){
            Panel.Confirmed-=OnConfirmed;Panel.SlotClicked-=OnSlotClicked;Panel.SlotRemoved-=OnSlotRemoved;
            if(ReferenceEquals(Panel.Prompt,this))Panel.Prompt=null;
            CancelPrompt("");
        }
        Panel=panel;panel.IconSource=host.Icon;panel.Confirmed+=OnConfirmed;panel.SlotClicked+=OnSlotClicked;panel.SlotRemoved+=OnSlotRemoved;panel.Prompt=this;Revision++;
    }
    void OnConfirmed(byte[] packet)=>_=SendOpen(packet);

    /// <summary>每次計時器呼叫：連線世代改變 → 清掉設定、關窗（舊角色的物件編號不能帶到新角色）。設定窗關了或離開世界 → 提示取消。</summary>
    public void Update(){
        if(Prompt!=null&&(Panel?.Visible!=true||!host.InWorld||host.Generation!=generation))CancelPrompt("");
        if(host.Generation==generation)return;
        generation=host.Generation;Setup.Clear();OpenSent=false;Sending=false;Panel?.Close();Say("");
    }

    // ---- R9-J：背包選取＋點清單 → 898 單價 → 933 數量 ------------------------------------------------------------
    public PersonalShopPrompt? Prompt{get;private set;}
    public bool PromptActive=>Prompt!=null;
    public string? PromptLine=>Prompt?.Line;
    void SetPrompt(PersonalShopPrompt? p){bool was=Prompt!=null;Prompt=p;Revision++;if(was&&p==null)host.EndTextInput();else if(!was&&p!=null)host.BeginTextInput();}
    void CancelPrompt(string notice){if(Prompt==null)return;SetPrompt(null);Say(notice);}

    void OnSlotClicked(bool sell,int slot){
        Update();
        var list=sell?Setup.Sell:Setup.Buy;
        if(list[slot] is PersonalShopEntry held){CancelPrompt("");Say($"{(sell?"出售":"收購")}第 {slot+1} 格：{held.Name} × {held.Count:N0}，單價 {held.Price:N0}（雙擊移除）。");return;}
        BeginPrompt(sell,slot);
    }
    void OnSlotRemoved(bool sell,int slot,PersonalShopEntry e){CancelPrompt("");Say($"已移除{(sell?"出售":"收購")}第 {slot+1} 格：{e.Name}。");}

    /// <summary>對 (sell, slot) 這個空格，以背包目前選取的物品開始 898 單價提示。失敗回 false（Notice 寫原因）。</summary>
    public bool BeginPrompt(bool sell,int slot){
        Update();CancelPrompt("");
        if(Panel?.Visible!=true||!host.InWorld){Say("設定窗沒有開。");return false;}
        if(host.SelectedItemId is not uint item){Say("請先在背包選取物品，再點清單的空格（引擎沒有跨窗拖曳，以「背包選取＋點清單」代替）。");return false;}
        if((sell?Setup.Sell:Setup.Buy).Any(e=>e?.ObjectId==item)){Say(PersonalShopText.Get(PersonalShopText.Listed));return false;}
        if(host.Candidate(item,1,1,sell) is not PersonalShopEntry probe){
            Say(host.HeldCount(item)!=null?PersonalShopText.Get(PersonalShopText.Equipped):"背包裡沒有這個物品。");return false;
        }
        uint held=host.HeldCount(item)??1;
        SetPrompt(new(sell,slot,item,probe.Name,held>1,false,0,0,"",PersonalShopText.Get(PersonalShopText.Price)));
        Say(Prompt!.Text);return true;
    }
    public void PromptType(char c){
        if(Prompt is not {} p||char.IsControl(c)||p.Typed.Length>=PersonalShopPrompt.MaxTyped)return;
        Prompt=p with{Typed=p.Typed+c};Revision++;
    }
    public void PromptBack(){if(Prompt is {Typed.Length:>0} p){Prompt=p with{Typed=p.Typed[..^1]};Revision++;}}
    public void PromptCancel()=>CancelPrompt("已取消。");
    /// <summary>Enter：單價階段 → 檢查 938／900／936，堆疊再問 933；數量階段 → 空白＝全部，然後放進格子。錯誤時提示留著、清掉輸入。</summary>
    public void PromptEnter(){
        Update();if(Prompt is not {} p)return;
        static uint? Number(string s)=>uint.TryParse(s,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out uint v)?v:null;
        void Retry(string notice){Prompt=p with{Typed=""};Revision++;Say(notice);}
        if(!p.CountStep){
            if(Number(p.Typed) is not uint price||price==0){Retry(PersonalShopText.Get(PersonalShopText.DigitsOnly));return;}
            if(price>=PersonalShopText.PriceLimit){Retry(PersonalShopText.Get(PersonalShopText.PriceTooHigh,PersonalShopText.PriceLimit));return;}
            long? adena=host.Adena;
            if(!p.Sell&&adena is long a&&price>a){Retry(PersonalShopText.Get(PersonalShopText.NoAdena));return;}
            if(!p.AskCount){Place(p,price,1);return;}
            uint max=p.Sell?host.HeldCount(p.ObjectId)??1:adena is long b?(uint)Math.Min(b/price,int.MaxValue):int.MaxValue;
            if(max==0){Retry(PersonalShopText.Get(PersonalShopText.NoAdena));return;}
            var next=p with{CountStep=true,Price=price,MaxCount=max,Typed="",Text=PersonalShopText.Get(PersonalShopText.Count,PersonalShopText.Get(p.Sell?PersonalShopText.SellWord:PersonalShopText.BuyWord),max)};
            Prompt=next;Revision++;Say(next.Text);return;
        }
        uint count;
        if(p.Typed.Length==0)count=p.MaxCount;   // 933：Enter＝全部
        else if(Number(p.Typed) is uint n)count=n;
        else{Retry(PersonalShopText.Get(PersonalShopText.DigitsOnly));return;}
        if(count==0||count>p.MaxCount){Retry(p.Text);return;}
        Place(p,p.Price,count);
    }
    void Place(PersonalShopPrompt p,uint price,uint count){
        SetPrompt(null);
        if(host.Candidate(p.ObjectId,price,count,p.Sell) is not PersonalShopEntry e){Say(p.Sell?"不能出售：物品不在背包、裝備中或數量超過持有量。":"不能收購：背包沒有這個樣本物品或裝備中。");return;}
        if(!Setup.Set(p.Sell,p.Slot,e)){Say(Setup.Notice=="同一物品已在清單中"?PersonalShopText.Get(PersonalShopText.Listed):Setup.Notice);return;}
        Say($"已放入{(p.Sell?"出售":"收購")}第 {p.Slot+1} 格：{e.Name} × {e.Count:N0}，單價 {e.Price:N0}。");
    }

    public bool Open(){
        Update();
        if(Panel==null){Say("原生 UI 未啟動或開店設定窗載入失敗。");return false;}
        if(!host.InWorld){Say("進入世界後才能設定個人商店。");return false;}
        Panel.Open(Setup);Say("請先在背包選取物品，再點設定窗的出售或收購空格，依提示輸入單價（與數量）；雙擊格子移除。");return true;
    }

    /// <summary>把背包目前選取的物品以指定單價、數量放進設定窗目前選取的格子。</summary>
    public bool Place(uint price,uint count){
        Update();
        if(Setup.Selected is not (bool sell,int slot)){Say("請先在開店設定窗點選一個出售或收購格。");return false;}
        if(host.SelectedItemId is not uint item){Say("請先在背包選取一個物品。");return false;}
        if(host.Candidate(item,price,count,sell) is not PersonalShopEntry e){
            Say(sell?"不能出售：物品不在背包、裝備中、數量超過持有量，或單價／數量不合法。":"不能收購：背包沒有這個樣本物品、裝備中，或單價／數量不合法。");return false;
        }
        if(!Setup.Set(sell,slot,e)){Say(Setup.Notice);return false;}
        Say($"已放入{(sell?"出售":"收購")}第 {slot+1} 格：{e.Name} × {e.Count:N0}，單價 {e.Price:N0}。");return true;
    }
    public bool ClearSlot(){
        Update();
        if(Setup.Selected is not (bool sell,int slot)){Say("請先在開店設定窗點選要清除的格子。");return false;}
        Setup.Set(sell,slot,null);Say($"已清除{(sell?"出售":"收購")}第 {slot+1} 格。");return true;
    }
    public bool SetMessages(string line1,string line2){
        Update();
        if(!Setup.SetMessage(0,line1)||!Setup.SetMessage(1,line2)){Say(Setup.Notice);return false;}
        Say("訊息已套用。");return true;
    }

    /// <summary>OK：送出 C38 type 0。送出前每筆重新以背包核對（物品可能已用掉、裝備或數量改變）。</summary>
    public async Task<bool> SendOpen(byte[] packet){
        Update();
        if(packet.Length<2||packet[0]!=PersonalShopPackets.Opcode||packet[1]!=0){Say("開店封包格式錯誤，未送出。");return false;}
        if(Sending||!host.CanSend){Say("目前不能送出（不在世界中、傳送中或前一個封包尚未送完）。");return false;}
        CancelPrompt("");
        // R9-J：sub_DE1AD0 的本端檢查中做得到的兩項。
        if(host.MapId is int map&&PersonalShopText.ClosedMaps.Contains(map)){Say(PersonalShopText.Get(PersonalShopText.CannotOpenHere));return false;}
        if(Setup.Sell.OfType<PersonalShopEntry>().Sum(e=>Math.Min((long)e.Price*e.Count,PersonalShopText.TotalLimit+1))>PersonalShopText.TotalLimit){Say(PersonalShopText.Get(PersonalShopText.TotalTooHigh,PersonalShopText.TotalLimit));return false;}
        foreach(var(list,sell)in new[]{(Setup.Sell,true),(Setup.Buy,false)})
            foreach(var e in list.OfType<PersonalShopEntry>())
                if(host.Candidate(e.ObjectId,e.Price,e.Count,sell)==null){Say($"「{e.Name}」已不符合條件（不在背包、裝備中或數量不足），請重新放入。");return false;}
        int mine=generation;Sending=true;
        try{
            bool sent=await host.Send(packet);
            if(mine!=host.Generation)return false;
            if(!sent){Say("開店封包未送出。");return false;}
            SentPackets++;OpenSent=true;Panel?.Close();Say("已送出開店（C38）。伺服器沒有確認回包，請看角色是否進入開店狀態。");return true;
        }finally{if(mine==generation)Sending=false;}
    }
    /// <summary>關店：C38 type 1。</summary>
    public async Task<bool> CloseShop(){
        Update();
        if(Sending||!host.CanSend){Say("目前不能送出關店。");return false;}
        int mine=generation;Sending=true;
        try{
            bool sent=await host.Send(PersonalShopPackets.Close());
            if(mine!=host.Generation)return false;
            if(!sent){Say("關店封包未送出。");return false;}
            SentPackets++;OpenSent=false;Say("已送出關店（C38 type 1）。");return true;
        }finally{if(mine==generation)Sending=false;}
    }
}

/// <summary>
/// R9-J（#854）：把個人商店的單價／數量提示畫在聊天輸入列（CENTER_WINDOW 的 CHAT_BAR）的位置——原版提示在聊天輸入列（FU9§9.4）。
/// 只在設定窗開著、有提示時可見；位置與字的起點（bar + (4,2)）照 ChatPanel 的輸入文字，原版字色與游標樣式待真機。
/// 沒有底圖（聊天列本身已畫底圖），不畫替代色塊。按下時吞掉滑鼠（提示期間聊天列不開輸入），按鍵一律放行給設定窗。
/// </summary>
public sealed class PersonalShopPromptPanel(UiRoot root):IUiClosablePanel {
    public const string PanelId="PersonalShop_Prompt";
    public static UiPanelRegistration Registration=>new(PanelId,PersonalShopPanel.LayoutName,r=>new PersonalShopPromptPanel(r));
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    PersonalShopPanel? Shop=>root.Find(PersonalShopPanel.WindowId) as PersonalShopPanel;
    /// <summary>目前要畫的提示行；設定窗沒開或沒有提示＝null。</summary>
    public string? Line=>Shop is {Visible:true,Prompt:{PromptActive:true} p}?p.PromptLine:null;
    public Rectangle Bounds{get{
        if(Line==null||root.Find(ChatPanel.WindowName) is not ChatPanel chat||!chat.Components.TryGetValue(ChatPanel.Bar,out var at))return Rectangle.Empty;
        int w=chat.Origin.X+chat.Width-at.X;return w>0?new(at.X,at.Y,w,ChatRules.LineHeight+4):Rectangle.Empty;
    }}
    public bool Visible=>!Bounds.IsEmpty;
    public void Load(UiLayout layout,SurfCatalog surfs){
        if(layout.Root.Name!=PersonalShopPanel.LayoutName)throw new UiLayoutException(layout.FileName,layout.Root.Path,$"根元素應為 {PersonalShopPanel.LayoutName}");
    }
    public void Update(IUiGameState state){}
    public void Paint(UiCanvas canvas){
        if(Line is not string line||Bounds is not {IsEmpty:false} b)return;
        canvas.DrawText(line,b.X+4,b.Y+2,ChatPanel.InputColor);
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>Visible&&Bounds.Contains(e.Location);
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    /// <summary>堆疊或全域 ESC：取消提示（設定窗不關）。</summary>
    public void Close(){if(Shop?.Prompt is {PromptActive:true} p)p.PromptCancel();}
}

public sealed partial class LoginConnection {
    /// <summary>R9-J（#854）：背包數量（不在背包＝null）。</summary>
    public uint? PersonalShopHeld(uint objectId)=>CaptureWorld(()=>World.Inventory.FirstOrDefault(i=>i.ObjectId==objectId)?.Count);
    /// <summary>R9-J（#854）：持有金幣（背包未載入＝null）。</summary>
    public long? PersonalShopAdena=>CaptureWorld(()=>ShopTradeSource.Holding(World));
    public int? PersonalShopMap=>CaptureWorld(()=>(int?)World.MapId);
    bool personalShopSending;int personalShopLease;
    bool PersonalShopAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {} s&&World.Actor?.ObjectId==s.ObjectId&&!World.TeleportPending;
    public bool PersonalShopCanSend=>CaptureWorld(()=>PersonalShopAccess&&!personalShopSending&&sendMovementGate!=null);
    public PersonalShopEntry? PersonalShopCandidate(uint objectId,uint price,uint count,bool sell)=>CaptureWorld(()=>World.PersonalShopCandidate(objectId,price,count,sell));
    /// <summary>只送 C38（type 0 開店或 type 1 關店）；同世代、仍在世界中才放行。</summary>
    public async Task<bool> SendPersonalShop(byte[] packet){
        if(packet.Length<2||packet[0]!=PersonalShopPackets.Opcode||packet[1] is not (0 or 1))return false;
        if(personalShopSending||sendMovementGate==null||!CaptureWorld(()=>PersonalShopAccess))return false;
        var sender=sendMovementGate;int session=generation,lease=++personalShopLease;personalShopSending=true;
        try{return await sender(packet,()=>lease==personalShopLease&&session==generation&&PersonalShopAccess,()=>{})&&session==generation;}
        catch(Exception ex){if(session==generation){Cancel();Set(LoginState.Faulted,"個人商店封包傳送失敗："+ex.Message);}return false;}
        finally{if(lease==personalShopLease)personalShopSending=false;}
    }
}

public sealed partial class ClientForm {
    PersonalShopController? personalShop;
    readonly FlowLayoutPanel personalShopActions=new(){Dock=DockStyle.Top,Height=150,AutoSize=false};
    readonly Button personalShopOpen=new(){Text="開店設定窗",Width=110,Height=28};
    readonly NumericUpDown personalShopPrice=new(){Width=110,Minimum=1,Maximum=int.MaxValue,Value=1};
    readonly NumericUpDown personalShopCount=new(){Width=80,Minimum=1,Maximum=int.MaxValue,Value=1};
    readonly Button personalShopPlace=new(){Text="放入選取格",Width=100,Height=28};
    readonly Button personalShopClear=new(){Text="清除選取格",Width=100,Height=28};
    readonly TextBox personalShopMessage1=new(){Width=170,MaxLength=PersonalShopSetup.MessageLimit};
    readonly TextBox personalShopMessage2=new(){Width=170,MaxLength=PersonalShopSetup.MessageLimit};
    readonly Button personalShopMessages=new(){Text="套用訊息",Width=90,Height=28};
    readonly Button personalShopStop=new(){Text="關閉我的個人商店",Width=140,Height=28};
    readonly Label personalShopNotice=new(){AutoSize=true,MaximumSize=new(400,0),ForeColor=Color.White};
    int personalShopShown=-1;

    sealed class PersonalShopHost(ClientForm form):IPersonalShopHost {
        public int Generation=>form.login.SessionGeneration;
        public bool InWorld=>form.login.State==LoginState.InWorld;
        public bool CanSend=>!form.World.Walking&&form.login.PersonalShopCanSend;
        public uint? SelectedItemId=>form.login.World.SelectedItemId;
        public PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell)=>form.login.PersonalShopCandidate(objectId,price,count,sell);
        public Task<bool> Send(byte[] packet){form.ClearInput("personal-shop");return form.login.SendPersonalShop(packet);}
        public Bitmap? Icon(ushort iconId)=>form.ItemIcon(iconId);
        public uint? HeldCount(uint objectId)=>form.login.PersonalShopHeld(objectId);
        public long? Adena=>form.login.PersonalShopAdena;
        public int? MapId=>form.login.PersonalShopMap;
        public void BeginTextInput()=>form.LeaveGameFocus("personal-shop-prompt");
        public void EndTextInput()=>form.ReturnGameFocus();
    }

    /// <summary>由 InitPrivateShop 呼叫：在舊「個人商店」頁加入開店設定的過渡控制項，並定時把原生面板接上控制器。</summary>
    void InitPersonalShop(){
        personalShop=new PersonalShopController(new PersonalShopHost(this));
        personalShopActions.Controls.AddRange([new Label{Text="開店設定（原生 PersonalShopUI）：",AutoSize=true,ForeColor=Color.White},personalShopOpen,personalShopStop,
            new Label{Text="單價",AutoSize=true,ForeColor=Color.White},personalShopPrice,new Label{Text="數量",AutoSize=true,ForeColor=Color.White},personalShopCount,personalShopPlace,personalShopClear,
            personalShopMessage1,personalShopMessage2,personalShopMessages,personalShopNotice]);
        personalShopOpen.Click+=(_,_)=>{personalShop.Open();Invalidate();SyncPersonalShop();};
        personalShopPlace.Click+=(_,_)=>{personalShop.Place((uint)personalShopPrice.Value,(uint)personalShopCount.Value);Invalidate();SyncPersonalShop();};
        personalShopClear.Click+=(_,_)=>{personalShop.ClearSlot();Invalidate();SyncPersonalShop();};
        personalShopMessages.Click+=(_,_)=>{personalShop.SetMessages(personalShopMessage1.Text,personalShopMessage2.Text);Invalidate();SyncPersonalShop();};
        personalShopStop.Click+=async(_,_)=>{await personalShop.CloseShop();SyncPersonalShop();};
        var t=new System.Windows.Forms.Timer{Interval=200};t.Tick+=(_,_)=>SyncPersonalShop();t.Start();Disposed+=(_,_)=>t.Dispose();
    }
    void SyncPersonalShop(){
        if(personalShop==null)return;
        if(NativeUiActive&&NativeUiRoot?.Find(PersonalShopPanel.WindowId) is PersonalShopPanel panel&&!NativeUiRoot.Faulted.Contains(panel))personalShop.Attach(panel);
        personalShop.Update();
        bool inWorld=login.State==LoginState.InWorld,attached=personalShop.Panel!=null;
        personalShopOpen.Enabled=inWorld&&attached;
        personalShopPlace.Enabled=personalShopClear.Enabled=personalShopMessages.Enabled=inWorld&&attached;
        personalShopStop.Enabled=inWorld&&!personalShop.Sending;
        int shown=HashCode.Combine(personalShop.Revision,personalShop.Setup.Revision,attached);
        if(shown==personalShopShown)return;personalShopShown=shown;
        string sel=personalShop.Setup.Selected is (bool s,int i)?$"選取格：{(s?"出售":"收購")}第 {i+1} 格。":"尚未選格。";
        personalShopNotice.Text=(attached?"":"原生 UI 未啟動（--native-ui）時無法開啟開店設定窗。\n")+sel+(personalShop.OpenSent?" 已送出開店。":"")+"\n"+personalShop.Notice;
    }
}
