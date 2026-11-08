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
        if(petTargetSending)return;
        var target=login.PetTargetSnapshot();if(!target.CanSend)return;
        petTargetSending=true;_ = SendPetTarget(target);
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
        bool active=login.State==LoginState.InWorld;
        string signature=System.Text.Json.JsonSerializer.Serialize(new{active,view,companions=companions.Select(o=>new{o.ObjectId,o.Name,o.HpPercent}),collars,walking=World.Walking,target,summon,native});
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
        Label("召喚怪物選擇 · "+(summon.Notice.Length>0?summon.Notice:"召喚選擇窗使用原客戶端 SummonUI。"));
        if(native)Button("開啟召喚怪物選擇（以技能 51 施放；原版開窗時機待查證）",!summon.Open&&summon.SkillLearned,()=>{login.OpenLocalSummon();petUiSignature="";SyncPetUi(this,EventArgs.Empty);});
        else if(summon.Open)Label("原服要求選擇召喚怪物；召喚選擇窗需開啟原生 UI（--native-ui）。");
        if(summon.Open)Button("關閉召喚選擇",true,()=>{login.CloseSummon(summon);petUiSignature="";SyncPetUi(this,EventArgs.Empty);});
        Button("查詢／重新整理寵物背包",can,()=>_ = RunPet(view.Stamp,"refresh"));
        Label("召喚物沒有寵物背包；背包內容以原服回覆為準。");
        if(view.Bag is {} bag){
            Label($"寵物背包 · AC {bag.Ac} · {bag.Items.Length} 項");
            for(int i=0;i<bag.Items.Length;i++){int index=i;var item=bag.Items[i];Button($"{ResolveNpcText(item.Name)} × {item.Count}"+(item.Equipped?"（裝備中）":""),can&&item.Usable&&i<=255,()=>_ = RunPet(view.Stamp,"item",index,item.ItemId));}
        }
        if(collars.Length>0){Label("可領取的寵物項圈（原服單價 70）");foreach(var collar in collars)Label((login.World.Inventory.FirstOrDefault(i=>i.ObjectId==collar.ItemId)?.Name??$"項圈 {collar.ItemId}")+$" × {collar.Count}");}
    }
}
