using P06.NativeUi;
namespace P06;

// ---------------------------------------------------------------------------------------------------------------------
// R2-N（T259／#620）：把 M7-T 的 PersonalShopPanel（WIN§1.1 PersonalShopUI）接上產品。
// - 註冊：UiPanelRegistry 加 PersonalShopPanel.Registration 一行。
// - 選格：面板點出售／收購格 → Setup.Selected；過渡用的「個人商店」頁（舊 WinForms）以背包目前選取的物品＋單價＋數量
//   呼叫 WorldSession.PersonalShopCandidate 放進該格（原版的拖放與單價／數量輸入窗未查證，不做假畫面）。
// - 送出：面板 OK → Confirmed(C38 type 0) → 送出前每筆重新以背包核對 → LoginConnection.SendPersonalShop。
// - 關店：C38 type 1（PersonalShopPackets.Close）。伺服器對開店／關店沒有專用確認回包，本端只記「已送出」。
// - 物品圖示：ClientForm.ItemIcon（背包同一來源）。
// 推測（報告列出）：OK 送出成功後關閉設定窗、保留設定以便再開；開窗入口原版待查證（這裡由過渡頁按鈕開窗）。
// ---------------------------------------------------------------------------------------------------------------------

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
}

public sealed class PersonalShopController(IPersonalShopHost host) {
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
        if(Panel!=null)Panel.Confirmed-=OnConfirmed;
        Panel=panel;panel.IconSource=host.Icon;panel.Confirmed+=OnConfirmed;Revision++;
    }
    void OnConfirmed(byte[] packet)=>_=SendOpen(packet);

    /// <summary>每次計時器呼叫：連線世代改變 → 清掉設定、關窗（舊角色的物件編號不能帶到新角色）。</summary>
    public void Update(){
        if(host.Generation==generation)return;
        generation=host.Generation;Setup.Clear();OpenSent=false;Sending=false;Panel?.Close();Say("");
    }

    public bool Open(){
        Update();
        if(Panel==null){Say("原生 UI 未啟動或開店設定窗載入失敗。");return false;}
        if(!host.InWorld){Say("進入世界後才能設定個人商店。");return false;}
        Panel.Open(Setup);Say("請在設定窗點選出售或收購格，再到此頁放入背包選取的物品。");return true;
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

public sealed partial class LoginConnection {
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
