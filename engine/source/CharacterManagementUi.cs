namespace P06;

public sealed class CharacterCreationDialog : Form {
    readonly TextBox characterName=new(){Width=200,MaxLength=12};
    readonly ComboBox characterClass=new(){Width=200,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox sex=new(){Width=200,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly NumericUpDown[] stats=Enumerable.Range(0,6).Select(_=>new NumericUpDown{Width=100,Minimum=0,Maximum=75}).ToArray();
    readonly Label notice=new(){AutoSize=false,Width=380,Height=70};
    readonly Button create=new(){Text="建立角色",AutoSize=true,AccessibleName="建立角色"};
    readonly int count,slots;
    internal static readonly string[] StatNames=["力量","敏捷","體質","精神","魅力","智力"];
    public CharacterCreation? Creation {get;private set;}
    CharacterCreation Read()=>new(characterName.Text,(byte)characterClass.SelectedIndex,(byte)sex.SelectedIndex,(byte)stats[0].Value,(byte)stats[1].Value,(byte)stats[2].Value,(byte)stats[3].Value,(byte)stats[4].Value,(byte)stats[5].Value);
    public CharacterCreationDialog(int count,int slots){
        this.count=count;this.slots=slots;Text="建立角色";
        // #1162 (#1150 F01/R07): unique UIA names so automation can find each field while the dialog is open.
        characterName.AccessibleName="角色名稱";characterClass.AccessibleName="職業";sex.AccessibleName="性別";notice.AccessibleName="本機檢查";
        for(int i=0;i<6;i++)stats[i].AccessibleName=StatNames[i];
ClientSize=new(440,590);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=2,RowCount=13};layout.ColumnStyles.Add(new(SizeType.Absolute,100));layout.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(layout);
        void Row(int row,string label,Control control){layout.Controls.Add(new Label{Text=label,AutoSize=true,Padding=new(0,5,0,0)},0,row);layout.Controls.Add(control,1,row);}
        Row(0,"角色名稱",characterName);Row(1,"職業",characterClass);Row(2,"性別",sex);
        characterClass.Items.AddRange(CharacterManagementPackets.Classes.Cast<object>().ToArray());sex.Items.AddRange(["男性","女性"]);sex.SelectedIndex=0;
        for(int i=0;i<6;i++){Row(i+3,StatNames[i],stats[i]);stats[i].ValueChanged+=(_,_)=>ValidateCreation();}
        layout.Controls.Add(notice,0,9);layout.SetColumnSpan(notice,2);
        var policy=new Label{Text="名稱最多 12 個 Big5 位元組／5 個雙位元組字。\n職業開放、禁用名稱與重名由伺服器最後檢查。",Width=390,Height=48};layout.Controls.Add(policy,0,10);layout.SetColumnSpan(policy,2);
        var buttons=new FlowLayoutPanel{AutoSize=true};var cancel=new Button{Text="取消",AccessibleName="取消",DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([create,cancel]);layout.Controls.Add(buttons,0,11);layout.SetColumnSpan(buttons,2);CancelButton=cancel;AcceptButton=create;
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
        // #1162 (#1150 F01/R07): UIA InvokePattern.Invoke runs Click synchronously on the UI thread, so a ShowDialog / MessageBox.Show
        // inside Click kept Invoke pending until the client timed out (0x80131505) and queued UIA requests behind it. Click now only
        // stamps session / revision and posts the modal with BeginInvoke, so Click (and Invoke) return at once.
        createCharacter.Click+=(_,_)=>{
            if(!BeginCharacterModal("character-create",out int session,out int revision))return;
            PostCharacterModal(async()=>await OpenCharacterCreationDialog(session,revision));
        };
        deleteCharacter.Click+=(_,_)=>{
            if(charactersBox.SelectedItem is not string name||login.Characters.FirstOrDefault(c=>c.Name==name) is not {} character)return;
            if(!BeginCharacterModal("character-delete",out int session,out int revision))return;
            string message=CharacterManagementPackets.PendingDeletion(character)?$"要取消「{character.Name}」的七日刪除排程嗎？":$"確定刪除「{character.Name}」嗎？\n伺服器可能立即刪除，或依等級設定七日刪除排程。";
            PostCharacterModal(async()=>{
                bool confirmed;
                try{confirmed=!IsDisposed&&MessageBox.Show(this,message,"確認角色刪除",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;}
                finally{characterModal.Exit();}
                if(!CharacterModalGate.StillCurrent(session,login.SessionGeneration,IsDisposed))return;
                ShowCharacterAction(await login.DeleteCharacter(character.Name,confirmed,session,revision),session);
            });
        };
        switchCharacter.Click+=async(_,_)=>{ClearInput("character-switch");int session=login.SessionGeneration;ShowCharacterAction(await login.SwitchCharacter(session,login.Revision),session);};
        Resize+=(_,_)=>PositionCharacterSwitch();
        // T280（R3-D）：選角畫面音樂（EC4FE0 狀態 20：系統槽 8 循環）。
        timer.Tick+=(_,_)=>{if(ClientSoundScene.Scene(login.State)==MusicScene.CharacterSelect)ClientSoundScene.ObserveFront(login.State,masterGain,masterMute,Environment.TickCount64);};
        // T327 (#765): the native UI may become active after the list arrived, so the native screens are attached on the tick too.
        timer.Tick+=(_,_)=>AttachNativeCharacterScreens();
        // T436 (#994, classic-ui-audit G18): native UI active but LoginUI / SelectUI unusable → reveal the dev-tools login page.
        timer.Tick+=(_,_)=>SyncNativeLoginFallback();
        // #1206: one native-ui.log line per change of the select screen's state (retest evidence for SM07–SM09).
        timer.Tick+=(_,_)=>TraceNativeSelect();
    }
    /// <summary>
    /// #1206 (#1186 SM07): the legacy scene's "等待真實世界初始化" text is drawn while online and the world is not renderable,
    /// except while the native select screen is shown (it covers the canvas; the text belonged to the classic path).
    /// --classic-ui / nativeUi false: nativeSelectShown is always false, so the classic path is unchanged.
    /// </summary>
    internal static bool WaitingTextShown(bool online,bool renderable,bool nativeSelectShown)=>online&&!renderable&&!nativeSelectShown;
    /// <summary>#1206: the native select screen is painted and gets input now (native UI active, panel usable on the root).</summary>
    bool NativeSelectScreenShown()=>NativeUiActive&&NativeUiRoot is {} root&&root.Find(P06.NativeUi.SelectCharPanel.PanelId) is P06.NativeUi.SelectCharPanel p&&root.Usable(p);
    /// <summary>#1206: one diagnostic line for the select screen (login state, Available, Host, View state, Faulted, Visible, Bounds, create screen).</summary>
    internal static string NativeSelectTrace(LoginState state,P06.NativeUi.SelectCharPanel p,bool faulted)=>
        $"select-char-ui 狀態 login={state} available={p.Available}{(p.Available?"":$"({p.Unavailable})")} host={p.Host!=null} view={p.View?.State.ToString()??"null"} "+
        $"characters={p.View?.Characters.Count??0} faulted={faulted} visible={p.Visible} bounds={p.Bounds.X},{p.Bounds.Y},{p.Bounds.Width}x{p.Bounds.Height} "+
        $"main={(p.Main is {} m?$"{m.Size.Width}x{m.Size.Height}":"null")} create-visible={p.Create?.Visible==true} selected={p.Selected??(p.SelectedEmpty is int e?$"empty#{e}":"null")}";
    string? nativeSelectTrace;
    void TraceNativeSelect(){
        if(!NativeUiActive||NativeUiRoot is not {} root||root.Find(P06.NativeUi.SelectCharPanel.PanelId) is not P06.NativeUi.SelectCharPanel p)return;
        string line=NativeSelectTrace(login.State,p,root.Faulted.Contains(p));
        if(line==nativeSelectTrace)return;
        nativeSelectTrace=line;P06.NativeUi.UiResourceLog.Write(line);
    }
    /// <summary>
    /// T436 (G18): the native screen this login state needs. Before the character list → LoginUI; list / character management /
    /// switching → SelectUI; entering or in the world → none (false = nothing missing).
    /// </summary>
    internal static bool NativeLoginScreenMissing(LoginState state,bool loginUsable,bool selectUsable)=>state switch{
        LoginState.InWorld or LoginState.EnteringWorld=>false,
        LoginState.AwaitList or LoginState.ReadingCharacters or LoginState.CharacterList or LoginState.ManagingCharacters or LoginState.SwitchingCharacter=>!selectUsable,
        _=>!loginUsable,
    };
    /// <summary>
    /// T436 (G18): open the dev-tools 「登入／選角」 page once when the native UI is active and the needed screen is missing
    /// (edge: missing now, not missing on the last tick). --classic-ui is G2 (ClientLogin SyncClassicLoginEntry), not here.
    /// </summary>
    internal static bool ShouldRevealNativeLoginFallback(bool enabled,bool nativeActive,bool missing,bool wasMissing)=>
        enabled&&nativeActive&&missing&&!wasMissing;
    bool nativeLoginFallbackMissing;
    void SyncNativeLoginFallback(){
        if(!classicLoginEntryArmed||IsDisposed)return;
        bool missing=NativeUiActive&&NativeLoginScreenMissing(login.State,NativeLoginScreenUsable(),NativeSelectScreenUsable());
        bool reveal=ShouldRevealNativeLoginFallback(classicLoginEntryEnabled,NativeUiActive,missing,nativeLoginFallbackMissing);
        nativeLoginFallbackMissing=missing;
        if(!reveal||playerLoginStrip==null)return;
        events.Add(new{kind="native-login-fallback",state=login.State.ToString()});
        BeginInvoke(()=>{if(!IsDisposed&&NativeUiActive&&login.State!=LoginState.InWorld)ShowDevTools(playerLoginStrip);});
    }
    bool NativeLoginScreenUsable()=>NativeUiRoot is {} root&&root.Find(P06.NativeUi.LoginPanel.PanelId) is P06.NativeUi.LoginPanel p&&p.Available&&!root.Faulted.Contains(p);
    bool NativeSelectScreenUsable()=>NativeUiRoot is {} root&&root.Find(P06.NativeUi.SelectCharPanel.PanelId) is P06.NativeUi.SelectCharPanel p&&p.Available&&!root.Faulted.Contains(p);
    // #1162: one character-management modal (create dialog or delete confirmation) at a time, counted from the Click that
    // scheduled it until the modal closes; a second Click meanwhile is ignored instead of stacking another modal.
    readonly CharacterModalGate characterModal=new();
    /// <summary>Click-time half: claim the gate, clear game input, and stamp session / revision (false = a modal is already pending).</summary>
    bool BeginCharacterModal(string input,out int session,out int revision){
        session=revision=0;
        if(!characterModal.TryEnter())return false;
        ClearInput(input);session=login.SessionGeneration;revision=login.Revision;return true;
    }
    /// <summary>Queue the modal behind the current message so the Click / UIA Invoke that requested it returns first.</summary>
    void PostCharacterModal(Action open){
        try{BeginInvoke(open);}catch{characterModal.Exit();throw;}
    }
    /// <summary>Show the creation dialog for a claimed gate (released when the dialog closes) and send it with the Click-time stamps.</summary>
    async Task OpenCharacterCreationDialog(int session,int revision){
        CharacterCreation? request=null;
        try{
            if(!IsDisposed){
                using var dialog=new CharacterCreationDialog(login.Characters.Count,login.Slots);
                if(dialog.ShowDialog(this)==DialogResult.OK)request=dialog.Creation;
            }
        }finally{characterModal.Exit();}
        if(request==null||!CharacterModalGate.StillCurrent(session,login.SessionGeneration,IsDisposed))return;
        ShowCharacterAction(await login.CreateCharacter(request,session,revision),session);
    }
    // T327 (#765) native routing only: with the native UI on and SelectUI.xml / CreateUI.xml usable, the native select and create
    // screens show the character list (same LoginControl / LoginConnection calls and stamps as the WinForms controls). Otherwise
    // (--classic-ui, no reference client, broken XML, faulted panel) nothing is attached and the WinForms path stays as before;
    // a usable select screen without a usable create screen opens the WinForms creation dialog (CreateFallback).
    CharacterScreenHost? nativeCharacterHost;
    void AttachNativeCharacterScreens(){
        if(!NativeUiActive||NativeUiRoot is not {} root)return;
        if(root.Find(P06.NativeUi.SelectCharPanel.PanelId) is not P06.NativeUi.SelectCharPanel select||!select.Available||select.Host!=null||root.Faulted.Contains(select))return;
        nativeCharacterHost??=new CharacterScreenHost(this);
        if(root.Find(P06.NativeUi.CreateCharPanel.PanelId) is P06.NativeUi.CreateCharPanel create&&create.Available&&!root.Faulted.Contains(create))select.Create=create;
        else select.Create=null;
        select.Attach(nativeCharacterHost);
        ClearInput("native-character-select");Invalidate();
    }
    sealed class CharacterScreenHost(ClientForm form):P06.NativeUi.ICharSelectHost {
        public P06.NativeUi.CharSelectView Snapshot(){
            var l=form.login;
            return new(l.SessionGeneration,l.Revision,l.State,l.Characters,l.Slots,form.loginControl?.CanEnter==true,l.CanCreateCharacter,l.CanDeleteCharacter,l.CharacterNotice);
        }
        public Task<LoginAction> Enter(P06.NativeUi.CharSelectView stamp,string name)=>form.EnterLoginCharacter(name,stamp.Generation,stamp.Revision);
        public async Task<LoginAction> Create(P06.NativeUi.CharSelectView stamp,CharacterCreation request){
            form.ClearInput("character-create");
            var r=await form.login.CreateCharacter(request,stamp.Generation,stamp.Revision);form.ShowCharacterAction(r,stamp.Generation);return r;
        }
        public async Task<LoginAction> Delete(P06.NativeUi.CharSelectView stamp,string name){
            form.ClearInput("character-delete");
            var r=await form.login.DeleteCharacter(name,true,stamp.Generation,stamp.Revision);form.ShowCharacterAction(r,stamp.Generation);return r;
        }
        public void Logout()=>form.CancelLogin(form.login.SessionGeneration,false);
        // Native SelectUI path: not inside a UIA Invoke, so the dialog still opens immediately (same stamps and gate as the button).
        public void CreateFallback(){if(form.BeginCharacterModal("character-create",out int session,out int revision))_=form.OpenCharacterCreationDialog(session,revision);}
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

/// <summary>
/// #1162 (#1150 F01/R07): re-entrancy guard for the character create / delete modals opened from the dev-tools buttons. Single UI
/// thread only; held from the Click that schedules a modal until that modal closes.
/// </summary>
internal sealed class CharacterModalGate {
    bool busy;
    public bool Busy=>busy;
    /// <summary>True = the caller now owns the gate and must <see cref="Exit"/> it; false = a modal is already open or scheduled.</summary>
    public bool TryEnter(){if(busy)return false;busy=true;return true;}
    public void Exit()=>busy=false;
    /// <summary>A closed modal may submit only if the form is alive and the login session is the one stamped at Click time.</summary>
    public static bool StillCurrent(int stampedSession,int currentSession,bool disposed)=>!disposed&&stampedSession==currentSession;
}

static class CharacterModalGateTests {
    // Pure logic only: no form, no window, no network. Run by --module-tests (name character-modal-gate).
    [WorldModuleTest("character-modal-gate")]
    static void Run(WorldModuleTestContext t){
        var gate=new CharacterModalGate();
        t.Check(!gate.Busy&&gate.TryEnter()&&gate.Busy,"first Click claims the gate");
        t.Check(!gate.TryEnter()&&gate.Busy,"second Click while the modal is scheduled / open is ignored");
        gate.Exit();
        t.Check(!gate.Busy&&gate.TryEnter(),"after the modal closes a new Click opens again");
        gate.Exit();gate.Exit();
        t.Check(!gate.Busy&&gate.TryEnter()&&!gate.TryEnter(),"double Exit does not let two modals through");
        gate.Exit();
        t.Check(CharacterModalGate.StillCurrent(3,3,false),"same session, form alive → submit");
        t.Check(!CharacterModalGate.StillCurrent(3,4,false),"session changed while the modal was open → do not submit");
        t.Check(!CharacterModalGate.StillCurrent(3,3,true),"form disposed → do not submit");
        string[] names=CharacterCreationDialog.StatNames;
        t.Check(names.Length==6&&names.Distinct().Count()==6&&string.Join("",names)=="力量敏捷體質精神魅力智力","six distinct stat AccessibleNames in dialog order");
    }
}