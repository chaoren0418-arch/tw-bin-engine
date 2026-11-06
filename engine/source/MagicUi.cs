namespace P06;
public sealed partial class ClientForm {
    readonly Panel magicPanel=new(){Location=new(266,150),Size=new(280,280),BackColor=Color.FromArgb(23,38,41),Visible=false};
    readonly ComboBox magicList=new(){Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name"};
    readonly Button castButton=new(){Dock=DockStyle.Top,Height=34,Text="手動單次施法 (M)",ForeColor=Color.White};
    readonly Panel magicStatusViewport=new(){Dock=DockStyle.Fill,AutoScroll=true};
    readonly Label magicLabel=new(){AutoSize=true,Location=new(0,0),ForeColor=Color.White,Padding=new(5)};
    readonly ComboBox magicTargetMode=new(){Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox magicEnemyList=new(){Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name",AccessibleName="可見施法目標"};
    sealed record MagicEnemy(uint Id,string Name);
    string? magicEnemySignature;
    void SyncMagicEnemies(){
        if(magicEnemyList.DroppedDown)return;
        var rows=objectVisuals.Where(v=>login.World.Objects.Any(o=>o.ObjectId==v.ObjectId&&!o.Dead&&!o.AppearancePending)&&HasVisibleObjectPixel(v.ObjectId)).Select(v=>new MagicEnemy(v.ObjectId,v.Name+" #"+v.ObjectId)).DistinctBy(v=>v.Id).OrderBy(v=>v.Id).ToArray();
        string signature=string.Join("|",rows.Select(v=>$"{v.Id}:{v.Name}"));
        syncingMagic=true;
        try{
            if(signature!=magicEnemySignature){magicEnemySignature=signature;magicEnemyList.Items.Clear();magicEnemyList.Items.Add(new MagicEnemy(0,"選擇可見敵人（也可點場景）"));magicEnemyList.Items.AddRange(rows);}
            magicEnemyList.SelectedIndex=0;
            for(int i=1;i<magicEnemyList.Items.Count;i++)if(((MagicEnemy)magicEnemyList.Items[i]).Id==login.World.TargetId)magicEnemyList.SelectedIndex=i;
            magicEnemyList.Enabled=!login.World.EffectiveMagicTargetSelf;
        }finally{syncingMagic=false;}
    }
    int magicUiRevision=-1;bool syncingMagic;
    void InitMagic(){
        magicStatusViewport.Controls.Add(magicLabel);magicPanel.Controls.Add(magicStatusViewport);magicStatusViewport.Resize+=(_,_)=>magicLabel.MaximumSize=new(Math.Max(100,magicStatusViewport.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4),0);magicPanel.Controls.Add(castButton);magicPanel.Controls.Add(magicEnemyList);magicTargetMode.Items.AddRange(new object[]{"自己","目前選取的對象"});magicTargetMode.SelectedIndex=0;magicPanel.Controls.Add(magicTargetMode);magicPanel.Controls.Add(magicList);magicPanel.Controls.Add(new Label{Text="原服已學魔法",Dock=DockStyle.Top,Height=26,ForeColor=Color.White});Controls.Add(magicPanel);magicPanel.BringToFront();
        magicEnemyList.Enter+=(_,_)=>LeaveGameFocus("magic-enemy");
        magicEnemyList.SelectionChangeCommitted+=(_,_)=>{
            if(syncingMagic||magicEnemyList.SelectedItem is not MagicEnemy {Id:>0} row)return;
            ClearInput("magic-enemy-select");
            login.World.MagicNotice=HasVisibleObjectPixel(row.Id)&&login.World.SelectMagicTarget(row.Id)?"已選取施法目標；按施放一次。":"目標已不可見，請重新選取。";
            SyncMagic();PublishWebState();
        };
        magicList.SelectedIndexChanged+=(_,_)=>{if(syncingMagic)return;ClearInput("magic-select");login.World.SelectMagic((magicList.SelectedItem as LearnedMagic)?.Id);lastWebState="";PublishWebState();};
        magicTargetMode.SelectedIndexChanged+=(_,_)=>{if(syncingMagic)return;ClearInput("magic-target");login.World.UseSelectedMagicTarget(magicTargetMode.SelectedIndex==0);lastWebState="";PublishWebState();};
        magicTargetMode.Enter+=(_,_)=>LeaveGameFocus("magic-target");
        magicList.Enter+=(_,_)=>LeaveGameFocus("magic-list");castButton.Enter+=(_,_)=>LeaveGameFocus("cast-button");castButton.Click+=async(_,_)=>{ClearInput("cast-click");await login.CastSelectedMagic();SyncMagic();PublishWebState();};
    }
    void SetMagicLabel(string text){if(magicLabel.Text==text)return;var scroll=magicStatusViewport.AutoScrollPosition;magicLabel.Text=text;magicStatusViewport.AutoScrollPosition=new(-scroll.X,-scroll.Y);}
    void SyncMagic(){SyncMagicEnemies();magicPanel.Visible=login.State==LoginState.InWorld;if(magicUiRevision!=login.World.MagicRevision){syncingMagic=true;magicUiRevision=login.World.MagicRevision;magicList.Items.Clear();foreach(var m in login.World.LearnedMagic){magicList.Items.Add(m);if(m.Id==login.World.SelectedMagicId)magicList.SelectedIndex=magicList.Items.Count-1;}syncingMagic=false;}syncingMagic=true;magicTargetMode.SelectedIndex=login.World.EffectiveMagicTargetSelf?0:1;magicTargetMode.Enabled=login.World.SelectedMagicId is 1 or 8;syncingMagic=false;castButton.Enabled=login.MagicAvailable&&!login.MovementBusy&&!World.Walking;var selected=login.World.LearnedMagic.FirstOrDefault(m=>m.Id==login.World.SelectedMagicId);var vitals=login.World.Status;SetMagicLabel((vitals==null?"等待原服 HP／MP\n":$"HP {vitals.Hp}/{vitals.MaxHp}　MP {vitals.Mp}/{vitals.MaxMp}\n{login.World.AcNotice} · 抗性 火{vitals.Fire} 水{vitals.Water} 風{vitals.Wind} 地{vitals.Earth}\n")+(selected==null?"請選擇魔法\n":$"{selected.Target} · 原模板MP {selected.NominalMp}\n")+$"對象：{login.World.MagicTargetName}\n"+login.World.MagicBuffNotice+"\n"+login.World.MagicNotice+"\n"+ResolveNpcText(login.World.LastServerMessage??""));}
    object MagicWeb()=>new{hp=login.World.Status?.Hp,maxHp=login.World.Status?.MaxHp,mp=login.World.Status?.Mp,maxMp=login.World.Status?.MaxMp,ac=login.World.CurrentAc,acNotice=login.World.AcNotice,fire=login.World.Status?.Fire,water=login.World.Status?.Water,wind=login.World.Status?.Wind,earth=login.World.Status?.Earth,buffs=login.World.MagicBuffNotice,targetName=login.World.MagicTargetName,epoch=login.World.WorldEpoch,skills=login.World.LearnedMagic,selectedId=login.World.SelectedMagicId,available=login.MagicAvailable&&!login.MovementBusy&&!World.Walking,notice=login.World.MagicNotice,message=login.World.LastServerMessage is {} message?ResolveNpcText(message):null,targetId=login.World.SelectedMagicId is int selected&&login.World.MagicTargetsOwn(selected)?(uint?)0:login.World.TargetId,targetMode=MagicSpells.Get(login.World.SelectedMagicId??0)?.Target??"none",targets=objectVisuals.Where(v=>login.World.Objects.Any(o=>o.ObjectId==v.ObjectId&&!o.Dead&&!o.AppearancePending)&&HasVisibleObjectPixel(v.ObjectId)).Select(v=>new{id=v.ObjectId,name=v.Name}).ToArray()};
    bool HasVisibleObjectPixel(uint id)=>objectPatch?.VisibleObjectIds.Contains(id)==true;
    async Task WebCast(int id){bool sent=!World.Walking&&await login.CastSelectedMagic();Reply(id,sent?"手動單次施法已送出":login.World.MagicNotice);PublishWebState();}
}
