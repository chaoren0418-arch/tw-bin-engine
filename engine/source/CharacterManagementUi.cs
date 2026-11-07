namespace P06;

public sealed class CharacterCreationDialog : Form {
    readonly TextBox characterName=new(){Width=200,MaxLength=12};
    readonly ComboBox characterClass=new(){Width=200,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox sex=new(){Width=200,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly NumericUpDown[] stats=Enumerable.Range(0,6).Select(_=>new NumericUpDown{Width=100,Minimum=0,Maximum=75}).ToArray();
    readonly Label notice=new(){AutoSize=false,Width=380,Height=70};
    readonly Button create=new(){Text="建立角色",AutoSize=true};
    readonly int count,slots;
    public CharacterCreation? Creation {get;private set;}
    CharacterCreation Read()=>new(characterName.Text,(byte)characterClass.SelectedIndex,(byte)sex.SelectedIndex,(byte)stats[0].Value,(byte)stats[1].Value,(byte)stats[2].Value,(byte)stats[3].Value,(byte)stats[4].Value,(byte)stats[5].Value);
    public CharacterCreationDialog(int count,int slots){
        this.count=count;this.slots=slots;Text="建立角色";ClientSize=new(440,590);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=2,RowCount=13};layout.ColumnStyles.Add(new(SizeType.Absolute,100));layout.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(layout);
        void Row(int row,string label,Control control){layout.Controls.Add(new Label{Text=label,AutoSize=true,Padding=new(0,5,0,0)},0,row);layout.Controls.Add(control,1,row);}
        Row(0,"角色名稱",characterName);Row(1,"職業",characterClass);Row(2,"性別",sex);
        characterClass.Items.AddRange(CharacterManagementPackets.Classes.Cast<object>().ToArray());sex.Items.AddRange(["男性","女性"]);sex.SelectedIndex=0;
        string[] labels=["力量","敏捷","體質","精神","魅力","智力"];
        for(int i=0;i<6;i++){Row(i+3,labels[i],stats[i]);stats[i].ValueChanged+=(_,_)=>ValidateCreation();}
        layout.Controls.Add(notice,0,9);layout.SetColumnSpan(notice,2);
        var policy=new Label{Text="名稱最多 12 個 Big5 位元組／5 個雙位元組字。\n職業開放、禁用名稱與重名由伺服器最後檢查。",Width=390,Height=48};layout.Controls.Add(policy,0,10);layout.SetColumnSpan(policy,2);
        var buttons=new FlowLayoutPanel{AutoSize=true};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([create,cancel]);layout.Controls.Add(buttons,0,11);layout.SetColumnSpan(buttons,2);CancelButton=cancel;AcceptButton=create;
        characterClass.SelectedIndexChanged+=(_,_)=>{byte[] minimum=CharacterManagementPackets.BaseStats(characterClass.SelectedIndex);int points=CharacterManagementPackets.AvailablePoints(characterClass.SelectedIndex);for(int i=0;i<6;i++){stats[i].Minimum=0;stats[i].Maximum=minimum[i]+points;stats[i].Value=minimum[i];stats[i].Minimum=minimum[i];}ValidateCreation();};
        characterName.TextChanged+=(_,_)=>ValidateCreation();sex.SelectedIndexChanged+=(_,_)=>ValidateCreation();
        create.Click+=(_,_)=>{var request=Read();if(CharacterManagementPackets.Validate(request,count,slots)!=null){ValidateCreation();return;}Creation=request;DialogResult=DialogResult.OK;Close();};
        characterClass.SelectedIndex=0;
    }
    void ValidateCreation(){
        if(characterClass.SelectedIndex<0||sex.SelectedIndex<0)return;
        int remaining=75-stats.Sum(s=>(int)s.Value);string? error=CharacterManagementPackets.Validate(Read(),count,slots);
        notice.Text=$"剩餘點數：{remaining}\n"+(error??"資料符合本機檢查，可以建立。");create.Enabled=error==null;
    }
}

public sealed partial class ClientForm {
    readonly Button createCharacter=new(){Text="建立角色",Width=92};
    readonly Button deleteCharacter=new(){Text="刪除角色",Width=110};
    // The normal HUD hides the login strip in-world, so this entry is a separate native control.
    readonly Button switchCharacter=new(){Text="切換角色",Size=new(104,30),Visible=false};
    string characterUiNotice="";int characterNoticeRevision=-1;
    void InitCharacterManagement(FlowLayoutPanel row){
        row.AutoScroll=true;row.Controls.AddRange([createCharacter,deleteCharacter]);row.Controls.SetChildIndex(createCharacter,row.Controls.IndexOf(loginLabel));row.Controls.SetChildIndex(deleteCharacter,row.Controls.IndexOf(loginLabel));
        Controls.Add(switchCharacter);switchCharacter.Enter+=(_,_)=>LeaveGameFocus("character-switch");
        charactersBox.FormattingEnabled=true;charactersBox.Format+=(_,e)=>{if(e.ListItem is string name&&login.Characters.FirstOrDefault(c=>c.Name==name) is {} character)e.Value=CharacterManagementPackets.Display(character);};
        charactersBox.SelectedIndexChanged+=(_,_)=>SyncCharacterManagement();
        createCharacter.Click+=async(_,_)=>{
            ClearInput("character-create");int session=login.SessionGeneration,revision=login.Revision;
            using var dialog=new CharacterCreationDialog(login.Characters.Count,login.Slots);
            if(dialog.ShowDialog(this)==DialogResult.OK&&dialog.Creation is {} request)ShowCharacterAction(await login.CreateCharacter(request,session,revision),session);
        };
        deleteCharacter.Click+=async(_,_)=>{
            if(charactersBox.SelectedItem is not string name||login.Characters.FirstOrDefault(c=>c.Name==name) is not {} character)return;
            ClearInput("character-delete");int session=login.SessionGeneration,revision=login.Revision;
            string message=CharacterManagementPackets.PendingDeletion(character)?$"要取消「{character.Name}」的七日刪除排程嗎？":$"確定刪除「{character.Name}」嗎？\n伺服器可能立即刪除，或依等級設定七日刪除排程。";
            bool confirmed=MessageBox.Show(this,message,"確認角色刪除",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
            ShowCharacterAction(await login.DeleteCharacter(character.Name,confirmed,session,revision),session);
        };
        switchCharacter.Click+=async(_,_)=>{ClearInput("character-switch");int session=login.SessionGeneration;ShowCharacterAction(await login.SwitchCharacter(session,login.Revision),session);};
        Resize+=(_,_)=>PositionCharacterSwitch();
    }
    void PositionCharacterSwitch(){switchCharacter.Location=new(Math.Max(0,ClientSize.Width-(webHost.Visible?webHost.Width:0)-switchCharacter.Width-12),8);switchCharacter.BringToFront();}
    void ShowCharacterAction(LoginAction action,int session){if(IsDisposed||session!=login.SessionGeneration)return;characterUiNotice=action.Accepted?"":action.Message;characterNoticeRevision=login.Revision;SyncLoginControls();}
    void SyncCharacterManagement(){
        createCharacter.Enabled=login.CanCreateCharacter;deleteCharacter.Enabled=login.CanDeleteCharacter&&charactersBox.SelectedItem is string;
        deleteCharacter.Text=charactersBox.SelectedItem is string name&&login.Characters.FirstOrDefault(c=>c.Name==name) is {} character&&CharacterManagementPackets.PendingDeletion(character)?"取消七日刪除":"刪除角色";
        switchCharacter.Visible=login.State==LoginState.InWorld;switchCharacter.Enabled=login.CanSwitchCharacter;PositionCharacterSwitch();
        if(characterNoticeRevision!=login.Revision)characterUiNotice="";
    }
}
