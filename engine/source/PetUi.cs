namespace P06;

public sealed partial class ClientForm {
    readonly FlowLayoutPanel petContent=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    uint selectedPet;
    string petUiSignature="";
    protected override void OnShown(EventArgs e) {
        InitPetUi();
        base.OnShown(e);
    }
    void InitPetUi() {
        // The buddy module owns OnLoad; attach this page once after the form is ready.
        if(normalPanels!=null&&petContent.Parent==null){
            var page=new TabPage("寵物"){BackColor=Color.FromArgb(23,38,41),ForeColor=Color.White};
            page.Controls.Add(petContent);normalPanels.TabPages.Add(page);
            petContent.Enter+=(_,_)=>LeaveGameFocus("pets");
            timer.Tick+=SyncPetUi;
            FormClosed+=(_,_)=>timer.Tick-=SyncPetUi;
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
        bool active=login.State==LoginState.InWorld;
        string signature=System.Text.Json.JsonSerializer.Serialize(new{active,view,companions=companions.Select(o=>new{o.ObjectId,o.Name,o.HpPercent}),collars,walking=World.Walking});
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
        Button("查詢／重新整理寵物背包",can,()=>_ = RunPet(view.Stamp,"refresh"));
        Label("召喚物沒有寵物背包；背包內容以原服回覆為準。");
        if(view.Bag is {} bag){
            Label($"寵物背包 · AC {bag.Ac} · {bag.Items.Length} 項");
            for(int i=0;i<bag.Items.Length;i++){int index=i;var item=bag.Items[i];Button($"{ResolveNpcText(item.Name)} × {item.Count}"+(item.Equipped?"（裝備中）":""),can&&item.Usable&&i<=255,()=>_ = RunPet(view.Stamp,"item",index,item.ItemId));}
        }
        if(collars.Length>0){Label("可領取的寵物項圈（原服單價 70）");foreach(var collar in collars)Label((login.World.Inventory.FirstOrDefault(i=>i.ObjectId==collar.ItemId)?.Name??$"項圈 {collar.ItemId}")+$" × {collar.Count}");}
    }
}
