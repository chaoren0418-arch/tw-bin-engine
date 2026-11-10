using P06.NativeUi;
namespace P06;

public sealed partial class ClientForm {
    readonly FlowLayoutPanel petContent=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    uint selectedPet;
    string petUiSignature="";
    protected override void OnActivated(EventArgs e) {
        InitPetUi();
        base.OnActivated(e);
    }
    void InitPetUi() {
        // Buddies own OnLoad and location sharing owns OnShown. The form is always
        // activated before the player can open this page, so attach once from here.
        if(normalPanels!=null&&petContent.Parent==null){
            var page=new TabPage("寵物"){BackColor=Color.FromArgb(23,38,41),ForeColor=Color.White};
            page.Controls.Add(petContent);normalPanels.TabPages.Add(page);
            petContent.Enter+=(_,_)=>LeaveGameFocus("pets");
            timer.Tick+=SyncPetUi;timer.Tick+=SyncPetLinks;
            FormClosed+=(_,_)=>{timer.Tick-=SyncPetUi;timer.Tick-=SyncPetLinks;};
        }
    }
    // M7-W（#530）：原生召喚選擇窗的轉接（ISummonUiHost），以及寵物選目標模式下「下一次選取目標」送 C223。
    bool petTargetSending;
    void SyncPetLinks(object? sender,EventArgs e) {
        if(NativeUiRoot?.Find(SummonPanel.PanelId) is SummonPanel panel&&panel.Host==null)panel.Attach(new SummonUiHost(this));
        // R3-B（#671）：舊式 PetControl 原生面板；原生 UI 關閉時由 SummonFallbackUi.cs 的暫行清單接手召喚選擇。
        if(NativeUiRoot?.Find(PetControlPanel.PanelId) is PetControlPanel control&&control.Host==null)control.Attach(new PetControlHost(this));
        SyncSummonFallback();
        SyncPetBagRefresh();
        if(petTargetSending)return;
        var target=login.PetTargetSnapshot();if(!target.CanSend)return;
        petTargetSending=true;_ = SendPetTarget(target);
    }
    // R4-F（#709）：S250/37 之後背包過時 → 自動送一次既有 C103 重要背包（PetEquipment.cs PetBagRefresh；同一次 S250/37 只送一次）。
    // 不經 RunPet：自動送出不清使用者的輸入狀態。原生 UI 開關都一樣（資料層，不依畫面）。
    readonly Dictionary<uint,long> petBagRequested=[];
    bool petBagRefreshing;
    void SyncPetBagRefresh() {
        if(petBagRefreshing||login.State!=LoginState.InWorld||World.Walking)return;
        foreach(var o in login.World.PetCompanions){
            var eq=login.PetEquipmentSnapshot(o.ObjectId);
            var view=login.PetSnapshot(o.ObjectId);
            if(!PetBagRefresh.Due(eq,petBagRequested.TryGetValue(o.ObjectId,out long last)?last:null,view.CanSend))continue;
            petBagRequested[o.ObjectId]=eq.Sequence!.Value;petBagRefreshing=true;
            _ = RefreshPetBag(view.Stamp);
            return;
        }
    }
    async Task RefreshPetBag(PetStamp stamp) {
        try{await login.SendPet(stamp,"refresh");}finally{petBagRefreshing=false;petUiSignature="";}
    }
    async Task SendPetTarget(PetTargetView view) {
        try{await login.SendPetTarget(view);}finally{petTargetSending=false;petUiSignature="";}
    }
    sealed class SummonUiHost(ClientForm form):ISummonUiHost {
        public SummonUiState State{get{var v=form.login.SummonSnapshot();return new(v.Open,v.Revision,v.Source,v.Level,v.Charisma,v.CanSend);}}
        public void Choose(SummonUiState shown,SummonMonster monster){
            var v=form.login.SummonSnapshot();if(v.Revision!=shown.Revision||!v.CanSend)return;
            form.ClearInput("summon-choice");_ = form.login.SendSummonChoice(v,monster.Action);
        }
        public void Close(SummonUiState shown){var v=form.login.SummonSnapshot();if(v.Revision==shown.Revision)form.login.CloseSummon(v);}
    }
    /// <summary>
    /// PetControl 轉接：原服每回一次 anicom／moncom（Pets.cs 存一份新的 PetInfo）就開一次窗。
    /// 第 0 鈕 → 既有 "refresh"（3.81 C103 C_PetMenu，背包仍顯示在 WinForms 寵物分頁）；第 1 鈕 → 既有 attackchr（C125，接 S236／C223）。
    /// 錨點用世界區中心（推測，原版容器座標來源未查到）。
    /// </summary>
    sealed class PetControlHost(ClientForm form):IPetControlHost {
        readonly PetControlTracker tracker=new();
        public PetControlView? Current{get{
            var login=form.login;bool inWorld=login.State==LoginState.InWorld;
            var rows=inWorld?login.World.PetCompanions.Select(o=>{var info=login.World.PetInformation(o.ObjectId);return (o.ObjectId,(object?)info,info?.Kind=="summon");}).ToArray():[];
            if(tracker.Observe(inWorld,rows) is not {} open)return null;
            bool can=login.PetSnapshot(open.PetId).CanSend&&!form.World.Walking;
            var world=UiRoot.WorldRect;
            return new(open.Revision,open.PetId,open.Summon,can,new(world.X+world.Width/2,world.Y+world.Height/2));
        }}
        public void Bag(PetControlView shown)=>Run(shown,"refresh");
        public void Target(PetControlView shown){if(!shown.Summon)Run(shown,PetPackets.TargetCommand);}
        void Run(PetControlView shown,string command){
            tracker.Dismiss(shown.Revision);
            var view=form.login.PetSnapshot(shown.PetId);
            if(view.CanSend)_ = form.RunPet(view.Stamp,command);
        }
    }
    async Task RunPet(PetStamp stamp,string command,int index=-1,uint item=0) {
        ClearInput("pets");
        if(!World.Walking)await login.SendPet(stamp,command,index,item);
        petUiSignature="";SyncPetUi(this,EventArgs.Empty);
    }
    void SyncPetUi(object? sender,EventArgs e) {
        if(petContent.Parent is not TabPage page||normalPanels?.SelectedTab!=page)return;
        var companions=login.World.PetCompanions;
        if(!companions.Any(o=>o.ObjectId==selectedPet))selectedPet=companions.FirstOrDefault()?.ObjectId??0;
        var view=login.PetSnapshot(selectedPet);var collars=login.World.PetCollars;
        var target=login.PetTargetSnapshot();var summon=login.SummonSnapshot();bool native=NativeUiActive;
        var equipment=login.PetEquipmentSnapshot(selectedPet);
        bool active=login.State==LoginState.InWorld;
        string signature=System.Text.Json.JsonSerializer.Serialize(new{active,view,equipment,companions=companions.Select(o=>new{o.ObjectId,o.Name,o.HpPercent}),collars,walking=World.Walking,target,summon,native,fallback=SummonFallbackSignature});
        if(signature==petUiSignature)return;petUiSignature=signature;
        foreach(Control c in petContent.Controls.Cast<Control>().ToArray())c.Dispose();petContent.Controls.Clear();
        void Label(string text)=>petContent.Controls.Add(new Label{Text=text,AutoSize=true,MaximumSize=new(410,0),Margin=new(6)});
        void Button(string text,bool enabled,Action click){var b=new Button{Text=text,Width=390,Height=32,Enabled=enabled};b.Click+=(_,_)=>click();petContent.Controls.Add(b);}
        if(!active){Label("登入並進入世界後可控制寵物與召喚物。");return;}
        Label("寵物與召喚物 · 選擇自己的夥伴");
        foreach(var o in companions){uint id=o.ObjectId;Button((id==selectedPet?"✓ ":"")+ResolveNpcText(o.Name)+$" · HP {o.HpPercent?.ToString()??"未知"}%",true,()=>{selectedPet=id;petUiSignature="";SyncPetUi(this,EventArgs.Empty);});}
        Label(view.Notice);
        if(view.Info is {} info)Label($"{(info.Kind=="pet"?"寵物":"召喚物")} · 等級 {info.Level} · HP {info.Hp}/{info.MaxHp} · MP {info.Mp}/{info.MaxMp}\n{ResolveNpcText(info.Mode)}");
        bool can=view.CanSend&&!World.Walking;
        Button("查詢／重新整理寵物與召喚物資訊",can,()=>_ = RunPet(view.Stamp,"info"));
        string[] labels=["攻擊態勢","防禦態勢","停留／休息","散開","警戒","解散"];
        for(int i=0;i<labels.Length;i++){string command=PetPackets.Commands[i];Button(labels[i],can,()=>_ = RunPet(view.Stamp,command));}
        Button("指定攻擊目標（只對寵物；原服回應後點選目標）",can,()=>_ = RunPet(view.Stamp,PetPackets.TargetCommand));
        if(target.PetId!=null){
            Label($"指定目標中：{ResolveNpcText(target.PetName)} · 請點選目標，選取後自動送出一次。");
            Button("取消指定目標（只清本機）",true,()=>{login.CancelPetTarget(target);petUiSignature="";SyncPetUi(this,EventArgs.Empty);});
        }else if(target.Notice.Length>0)Label(target.Notice);
        Label("召喚怪物選擇 · "+(summon.Notice.Length>0?summon.Notice:native?"召喚選擇窗使用原客戶端 SummonUI。":"原生 UI 關閉時以下方暫行清單選擇。"));
        Button("開啟召喚怪物選擇（以技能 51 施放；原版開窗時機待查證）",!summon.Open&&summon.SkillLearned,()=>{login.OpenLocalSummon();petUiSignature="";SyncPetUi(this,EventArgs.Empty);});
        if(!native)AddSummonFallback(summon,Label,Button);
        if(summon.Open)Button("關閉召喚選擇",true,()=>{login.CloseSummon(summon);petUiSignature="";SyncPetUi(this,EventArgs.Empty);});
        Button("查詢／重新整理寵物背包",can,()=>_ = RunPet(view.Stamp,"refresh"));
        Label("召喚物沒有寵物背包；背包內容以原服回覆為準。");
        // R4-F：AC 取 PetAc（S250/37 與 S176 取較新的一份）；背包清單過時時標示，並由 SyncPetBagRefresh 自動重要一次。
        if(PetBagRefresh.Line(view.Bag,equipment) is string line)Label(line);
        if(view.Bag is {} bag){
            for(int i=0;i<bag.Items.Length;i++){int index=i;var item=bag.Items[i];Button($"{ResolveNpcText(item.Name)} × {item.Count}"+(item.Equipped?"（裝備中）":""),can&&item.Usable&&i<=255,()=>_ = RunPet(view.Stamp,"item",index,item.ItemId));}
        }
        if(collars.Length>0){Label("可領取的寵物項圈（原服單價 70）");foreach(var collar in collars)Label((login.World.Inventory.FirstOrDefault(i=>i.ObjectId==collar.ItemId)?.Name??$"項圈 {collar.ItemId}")+$" × {collar.Count}");}
    }
}
