using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    readonly LoginConnection login=new();int lastCorrection,lastWorldEpoch=-1,lastAppearanceRevision=-1;
    LoginControl loginControl=null!;
    readonly Button loginCancel=new(){Text="取消 / 斷線",Width=98};
    readonly Button loginOffline=new(){Text="離線預覽",Width=90};
    readonly ComboBox charactersBox=new(){Width=155,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Button enterWorld=new(){Text="進入世界",Width=95,Enabled=false};
    string listSignature="";bool pendingOfflineStepTest;
    bool OnlineMode=>login.State!=LoginState.Offline;
    void SyncWorld(){
        if(lastAppearanceRevision!=login.World.AppearanceRevision){lastAppearanceRevision=login.World.AppearanceRevision;ClearInput("appearance-revision");World.Stop();}
        if(lastWorldEpoch!=login.World.WorldEpoch){lastWorldEpoch=login.World.WorldEpoch;ClearInput("world-generation");World.Stop();renderer.ReleaseMap();objectPatch=null;patch?.Dispose();patch=null;previous="";}
        if(OnlineMode&&!login.World.Renderable){World.Stop();renderer.ReleaseMap();objectPatch=null;patch?.Dispose();patch=null;previous="";}
        if(OnlineMode&&login.World.LoadedMapBounds is {} bounds){World.StreamingBounds=bounds;World.AllowStreaming=true;}
        if(!OnlineMode)World.ResetOfflinePreview();
        if(login.World.Renderable&&login.World.Actor is {} appearanceActor){var selectedAppearance=Renderer.OwnStrideAppearance(objectAssets??=new(World.Assets.Root),login.World,appearanceActor,World.Walking);if(!ReferenceEquals(World.OnlineAppearance,selectedAppearance)){ClearInput("appearance-change");World.SetOnlineAppearance(selectedAppearance);}}
        SyncRecovery();SyncInventory();SyncGround();SyncRewards();SyncMagic();SyncInteraction();SyncPrivateShop();SyncTrade();SyncBonusStats();SyncNormalPanels();
        if(OnlineMode&&login.State!=LoginState.InWorld){ClearInput("online-transition");if(World.Walking)World.Stop();}
        if(login.State==LoginState.InWorld&&!login.MovementAvailable)ClearInput("unsupported-movement-state");
        if(lastCorrection!=login.World.Corrections){lastCorrection=login.World.Corrections;ClearInput("server-correction");if(login.World.Renderable&&login.World.Actor is {} corrected)World.Teleport(corrected.X,corrected.Y);}
        string signature=string.Join("|",login.Characters.Select(c=>$"{c.Name}:{c.Class}"));
        if(signature!=listSignature){listSignature=signature;charactersBox.Items.Clear();charactersBox.Items.AddRange(login.Characters.Select(c=>(object)c.Name).ToArray());if(charactersBox.Items.Count>0)charactersBox.SelectedIndex=0;}
        enterWorld.Enabled=login.State==LoginState.CharacterList&&charactersBox.SelectedItem!=null;
        SyncLoginControls();
        if(login.World.Renderable&&login.World.Actor is {} actor){if(!World.Walking){if(World.X!=actor.X||World.Y!=actor.Y)World.Teleport(actor.X,actor.Y);if(World.Direction!=MovementRules.SpriteDirection(actor.Direction))World.Face(MovementRules.SpriteDirection(actor.Direction));}Camera.Follow=true;}
        Invalidate();
    }
    readonly TextBox loginAccount=new(){Width=95,PlaceholderText="帳號",MaxLength=12};
    readonly TextBox loginPassword=new(){Width=95,PlaceholderText="密碼",MaxLength=13,UseSystemPasswordChar=true};
    readonly ComboBox loginProfile=new(){Width=175,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly NumericUpDown loginPort=new(){Minimum=42061,Maximum=42063,Value=ProductEntry.Port,Width=90};
    readonly Button loginConnect=new(){Text="登入",Width=88};
    readonly Label loginLabel=new(){AutoSize=false,Width=480,Height=52,ForeColor=Color.White};
    void InitLogin(string? demoPort,string? scenario){
        loginControl=new(login,BeforeLoginStart,()=>{ClearInput("offline-preview");World.ResetOfflinePreview();ReturnGameFocus();});
        loginControl.Changed+=()=>{if(IsDisposed)return;SyncLoginControls();lastWebState="";PublishWebState();};
        Text=normalProduct?"P24 客戶端":"P24 診斷客戶端";
        loginProfile.Items.AddRange(["jar-d287-client1","jar-d287-client2","p24-real-jar-d287-client2"]);loginProfile.SelectedIndex=scenario==null||scenario.StartsWith("real-")?2:scenario=="wide"?1:0;
        if(loginProfile.SelectedIndex==2)loginPort.Value=42061;
        loginProfile.SelectedIndexChanged+=(_,_)=>{if(loginProfile.SelectedIndex==2)loginPort.Value=42061;};
        var row=new FlowLayoutPanel{Dock=DockStyle.Top,Height=104,BackColor=Color.FromArgb(27,44,51),Padding=new(8),WrapContents=true};
        var cancel=loginCancel;var offline=loginOffline;
        foreach(var button in new[]{loginConnect,cancel,offline}){button.ForeColor=Color.White;button.BackColor=Color.FromArgb(42,74,80);button.FlatStyle=FlatStyle.Flat;}
        row.Controls.AddRange([loginProfile,loginPort,loginAccount,loginPassword,loginConnect,cancel,offline,charactersBox,enterWorld,loginLabel]);Controls.Add(row);row.BringToFront();playerLoginStrip=row;
        if(normalProduct){loginProfile.Visible=false;loginPort.Visible=false;var serverLabel=new Label{Text=ProductEntry.ServerName,ForeColor=Color.White,AutoSize=true,Padding=new(0,7,10,0)};row.Controls.Add(serverLabel);row.Controls.SetChildIndex(serverLabel,0);MinimumSize=new(900,650);}
        InitCharacterManagement(row);
        InitLoginKeyboardNavigation();
        foreach(Control control in row.Controls)control.Enter+=(_,_)=>LeaveGameFocus("login-controls");
        enterWorld.Click+=async(_,_)=>{if(charactersBox.SelectedItem is string name)await EnterLoginCharacter(name,login.SessionGeneration,login.Revision);};
        loginConnect.Click+=(_,_)=>StartLogin();cancel.Click+=(_,_)=>CancelLogin(login.SessionGeneration,false);offline.Click+=(_,_)=>CancelLogin(login.SessionGeneration,true);
        // R10-R（FU14§2.4）：每送一步前進一次 stride 計數（3.81 sub_4E2150），這一步的外型改用 stride 動作（Renderer.OwnStrideAppearance）。
        login.MovePlanned+=(from,to)=>{if(login.World.Renderable){login.World.AdvanceOwnStride(from.Gfx);World.SetOnlineAppearance(Renderer.OwnStrideAppearance(objectAssets??=new(World.Assets.Root),login.World,from,true));World.NetworkStep(from.X,from.Y,to.Direction,NormalSpeed.AnimationMs(login.World.AppearanceProfile!.StepMs,from.Speed,from.Brave));}};
        // T260（#621，R2-Q）：正常操作時 S48 公告以模態訊息框顯示，按確定才回 C16；自動化情境（demoPort，非 real-interactive）維持自動確認。
        login.NewsConfirmationRequired=demoPort==null||scenario=="real-interactive";
        login.Changed+=()=>{if(IsDisposed)return;QueueLoginNews();SyncWorld();lastWebState="";PublishWebState();if(evidence!=null)Json.Save(Path.Combine(evidence,"login-state.json"),login.Snapshot());};
        FormClosed+=(_,_)=>login.Dispose();
        // T433（#991，G2）：classic 的登入列在開發工具「登入／選角」分頁；正常入口、非自動化時在不在世界中的畫面自動打開它。
        // Shown 之後才啟用（InitNativeUi 在建構子最後才決定 NativeUiActive）。
        classicLoginEntryEnabled=normalProduct&&demoPort==null;
        Shown+=(_,_)=>{classicLoginEntryArmed=true;SyncClassicLoginEntry();};
        // T280（R3-D）：登入畫面音樂（系統槽 0，8F2E40／8F2F00）；進入世界中（None）停音樂。選角畫面由 CharacterManagementUi 負責，InWorld 由 Observe(WorldSession)。
        timer.Tick+=(_,_)=>{if(ClientSoundScene.Scene(login.State) is MusicScene.Login or MusicScene.None)ClientSoundScene.ObserveFront(login.State,masterGain,masterMute,Environment.TickCount64);};
        Shown+=async(_,_)=>{if(demoPort!=null){loginPort.Value=int.Parse(demoPort);if(scenario?.StartsWith("real-")==true){loginProfile.SelectedIndex=2;loginAccount.Text=Environment.GetEnvironmentVariable("P24_TEST_ACCOUNT")??"";loginPassword.Text=Environment.GetEnvironmentVariable("P24_TEST_PASSWORD")??"";}else{loginAccount.Text="engine11";loginPassword.Text="testpass";}if(scenario=="real-world-pending"){World.Teleport(32589,32930);HandleDown(Keys.Home);pendingOfflineStepTest=World.Walking;events.Add(new{kind="pending-offline-step-before-login",pendingOfflineStepTest,model=World.Snapshot()});}
                if(scenario=="real-opacity")PrepareOpacityCapture();if(scenario=="real-magic")PrepareMagicCapture();if(scenario=="real-ranged")PrepareRangedCapture();if(scenario=="real-equipment")PrepareEquipmentCapture();if(scenario=="real-reward")PrepareRewardCapture();if(scenario=="real-ground")PrepareGroundCapture();if(scenario=="real-inventory")PrepareInventoryCapture();StartLogin();if(scenario!=null&&scenario!="real-interactive")await (scenario=="real-opacity"?OpacityDemo():scenario=="real-magic"?MagicDemo():scenario=="real-ranged"?RangedDemo():scenario=="real-equipment"?EquipmentDemo():scenario=="real-reward"?RewardDemo():scenario=="real-ground"?GroundDemo():scenario=="real-inventory"?InventoryDemo():scenario=="real-combat"?CombatDemo():scenario=="real-objects"?ObjectDemo():scenario=="real-movement"?MovementDemo():scenario.StartsWith("real-world")?WorldDemo(scenario):LoginDemo(scenario));}};
    }
    int shownNewsRevision;
    void QueueLoginNews(){
        if(!login.NewsPending||login.NewsRevision==shownNewsRevision||!IsHandleCreated)return;
        int revision=shownNewsRevision=login.NewsRevision,session=login.SessionGeneration;
        // BeginInvoke: Changed is raised inside the receive loop; the modal must not run re-entrantly before the loop awaits confirmation.
        BeginInvoke(()=>{
            if(IsDisposed||!login.NewsPending||login.NewsRevision!=revision||login.SessionGeneration!=session)return;
            // T352（#815，R7-M）：原生登入畫面的 Notice_Window 可用時改由它顯示（UiClientBridge.ShowNativeLoginNotice）；不可用（--classic-ui、
            // 面板停用或 Faulted、版面缺 Notice_Window）時沿用模態訊息框。
            if(ShowNativeLoginNotice(LoginNewsText.Display(login.News),revision,session))return;
            ShowLoginNewsBox(revision,session);
        });
    }
    void ShowLoginNewsBox(int revision,int session){
        if(IsDisposed||!login.NewsPending||login.NewsRevision!=revision||login.SessionGeneration!=session)return;
        LeaveGameFocus("login-news");
        MessageBox.Show(this,LoginNewsText.Display(login.News),"公告",MessageBoxButtons.OK,MessageBoxIcon.None);
        ConfirmLoginNews(revision,session);
    }
    /// <summary>公告框（訊息框或原生 Notice_Window）按確定：同一 session 才確認，LoginConnection 隨後送 C16。</summary>
    void ConfirmLoginNews(int revision,int session){if(!IsDisposed&&login.SessionGeneration==session)login.ConfirmNews(revision);}
    internal void BeforeLoginStart(){networkMode=false;network.Disconnect("login selected");ClearInput("login");}
    bool classicLoginEntryEnabled,classicLoginEntryArmed,classicLoginScreenShown;
    void SyncClassicLoginEntry(){
        if(!classicLoginEntryArmed||IsDisposed)return;
        bool loginScreen=login.State!=LoginState.InWorld;
        bool reveal=ShouldRevealClassicLogin(classicLoginEntryEnabled,NativeUiActive,loginScreen,classicLoginScreenShown);
        classicLoginScreenShown=loginScreen;
        if(!reveal||playerLoginStrip==null)return;
        events.Add(new{kind="classic-login-entry",state=login.State.ToString()});
        BeginInvoke(()=>{if(!IsDisposed&&login.State!=LoginState.InWorld&&!NativeUiActive)ShowDevTools(playerLoginStrip);});
    }
    void SyncLoginControls(){if(loginControl==null)return;SyncClassicLoginEntry();SyncCharacterManagement();loginConnect.Enabled=loginControl.CanLogin;loginProfile.Enabled=loginPort.Enabled=loginAccount.Enabled=loginPassword.Enabled=loginControl.CanLogin;loginCancel.Enabled=loginControl.CanCancel;charactersBox.Enabled=loginControl.CanEnter;enterWorld.Enabled=loginControl.CanEnter&&charactersBox.SelectedItem!=null;loginLabel.Text=normalProduct?(login.State is LoginState.ManagingCharacters or LoginState.SwitchingCharacter?"處理角色操作":ProductEntry.StateName(login.State))+" · "+(login.World.BonusStatsNotice.Length>0?login.World.BonusStatsNotice:login.CharacterNotice.Length>0?login.CharacterNotice:loginControl.Notice):$"{login.State} • {login.Detail}\n{loginControl.Notice}";if(characterUiNotice.Length>0)loginLabel.Text=characterUiNotice;else if(login.State==LoginState.Faulted)loginLabel.Text=login.Detail;}
    LoginAction BeginLogin(string profile,int port,string account,string password,int generation,int revision){loginPassword.Clear();if(normalProduct&&(profile!=ProductEntry.Profile||port!=ProductEntry.Port))return new(false,"正常入口僅使用本機遊戲伺服器。");return loginControl.Start(profile,port,account,password,generation,revision);}
    Task<LoginAction> EnterLoginCharacter(string name,int generation,int revision){ClearInput("select-character");return loginControl.Enter(name,generation,revision);}
    LoginAction CancelLogin(int generation,bool offline){loginPassword.Clear();ClearInput("cancel-login");return loginControl.Cancel(generation,offline);}
    void StartLogin(){string password=loginPassword.Text;loginPassword.Clear();BeginLogin(loginProfile.Text,(int)loginPort.Value,loginAccount.Text,password,login.SessionGeneration,login.Revision);}
    async Task DispatchLoginCommand(LoginWebCommand command,int id){
        LoginAction result;
        switch(command.Command){
            case "login":result=BeginLogin(command.Profile,command.Port,command.Account,command.TakePassword(),command.Generation,command.Revision);break;
            case "enterWorld":result=await EnterLoginCharacter(command.Character,command.Generation,command.Revision);break;
            default:result=CancelLogin(command.Generation,command.Command=="offlinePreview");break;
        }
        Reply(id,result.Message);lastWebState="";PublishWebState();
    }
    async Task LoginDemo(string scenario){
        try{
            await Until(()=>webReady,15000);
            await Until(()=>login.State is LoginState.CharacterList or LoginState.Rejected or LoginState.Faulted,10000);
            await Task.Delay(250);loginAccount.Clear();CaptureEvidence("login-"+scenario);
            if(web!=null){await using var png=File.Create(Path.Combine(evidence!,"login-"+scenario+"-web.png"));await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,png);}
            string expected=scenario is "reject" or "wrong-seed" or "real-reject"?"Rejected":"CharacterList";
            if(login.State.ToString()!=expected)throw new Exception("unexpected UI login state "+login.State);
            string dom=web==null?"":await web.ExecuteScriptAsync("document.getElementById('loginResult').innerText");
            if(login.State==LoginState.CharacterList&&login.Expected>0&&!JsonSerializer.Deserialize<string>(dom)!.Contains(scenario.StartsWith("real-")?"透明王子":"測試王子"))throw new Exception("parsed character missing from web DOM");
            Json.Save(Path.Combine(evidence!,"login-ui-result.json"),new{status="pass",scenario,kind="actual WinForms/WebView2 in-process controller over loopback; not native keyboard input",snapshot=login.Snapshot(),dom});
        }catch(Exception ex){NetworkExitCode=3;Json.Save(Path.Combine(evidence!,"login-ui-result.json"),new{status="fail",scenario,error=ex.ToString()});}
        finally{login.Cancel();Close();}
    }
}
/// <summary>S48 公告文字轉成訊息框顯示用：伺服器用 \n 或 \r\n 斷行，統一成 Windows 斷行；空公告顯示提示字。</summary>
static class LoginNewsText {
    public static string Display(string news){string text=news.Replace("\r\n","\n").Replace("\r","\n").Replace("\n","\r\n");return text.Trim().Length==0?"（伺服器公告為空）":text;}
}
