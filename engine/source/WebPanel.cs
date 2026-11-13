using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    const string LocalPage="https://p08.example/index.html";
    readonly Panel webHost=new(){Dock=DockStyle.Fill,Visible=true,BackColor=Color.FromArgb(19,35,42)};
    readonly Button webToggle=new(){Text="開啟本機面板 (Ctrl+Shift+F9)",Dock=DockStyle.Top,Height=34};
    readonly Button gameButton=new(){Text="遊戲控制 (Ctrl+Shift+F10)",Dock=DockStyle.Top,Height=30};
    readonly Label webNotice=new(){Dock=DockStyle.Fill,ForeColor=Color.White,Padding=new(18),Text="WebView2 初始化中…"};
    readonly HashSet<Keys> webShortcutHeld=[];
    WebView2? web;CoreWebView2Environment? webEnvironment;Task? initializing;
    int webEpoch,revision;bool webClosing,finalClose,gameFocus=true,webReady;string? webFault;
    string lastWebState="";EventJournal? webJournal;
    readonly string stageRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
    bool GameInputAllowed=>gameFocus&&!PlayerChatFocused&&!(web?.ContainsFocus??false)&&ActiveForm==this;
    string WebEvidence=>evidence??Path.Combine(stageRoot,"runs",$"interactive-{Environment.ProcessId}");
    public void CompleteWebJournal()=>webJournal?.Complete();
    void WebLog(string kind,object? data=null){(webJournal??=new EventJournal(Path.Combine(WebEvidence,"web-events.json"))).Append(kind,new{kind,data,utc=DateTimeOffset.UtcNow,monotonic=clock.ElapsedTicks,pid=Environment.ProcessId});}
    void InitWeb(string? scenario,string? fault){
        webFault=fault;Font=new("Microsoft JhengHei UI",10f*96/DeviceDpi);webToggle.ForeColor=Color.White;gameButton.ForeColor=Color.White;webToggle.BackColor=Color.FromArgb(42,74,80);gameButton.BackColor=Color.FromArgb(42,74,80);webHost.Controls.Add(webNotice);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Top,Height=36,BackColor=Color.FromArgb(23,38,41)};
        webToggle.Dock=DockStyle.None;webToggle.Width=300;gameButton.Dock=DockStyle.None;gameButton.Width=260;tools.Controls.AddRange([webToggle,gameButton,tradeInvite]);playerToolsStrip=tools;
        webToggle.Enter+=(_,_)=>LeaveGameFocus("toolbar");gameButton.Enter+=(_,_)=>LeaveGameFocus("toolbar");
        webToggle.Click+=async(_,_)=>await TogglePanel();gameButton.Click+=(_,_)=>ReturnGameFocus();
        Deactivate+=(_,_)=>LeaveGameFocus("deactivate");
        Shown+=async(_,_)=>{if(normalProduct&&scenario==null)return;var open=OpenPanel();if(scenario!="close-init")await open;if(scenario!=null)await WebDemo(scenario);};
        FormClosing+=async(_,e)=>{if(finalClose)return;e.Cancel=true;if(webClosing)return;webClosing=true;var combatShutdown=ShutdownCombatSfx();var audioShutdown=await Shutdown6016();Record6016(new("form-closing",audioShutdown));await combatShutdown;await ClosePanel();finalClose=true;Close();};
    }
    void LeaveGameFocus(string why){gameFocus=false;ClearInput("web-"+why);PublishWebState();WebLog("focus-left-game",new{why,held=Input.Held.Count,model=World.Snapshot()});}
    void ReturnGameFocus(){ActiveControl=null;Activate();Focus();gameFocus=true;ClearInput("return-game");PublishWebState();WebLog("focus-game",new{held=Input.Held.Count,model=World.Snapshot()});}
    async Task TogglePanel(){if(webClosing)return;webToggle.Enabled=false;try{if(devTools?.Visible==true)devTools.HideTools();else await OpenPanel();}finally{if(!IsDisposed)webToggle.Enabled=true;}}
    Task OpenPanel(){ShowDevTools(webHost);if(web!=null)return initializing??Task.CompletedTask;webHost.Visible=true;webNotice.Text="WebView2 初始化中…";webToggle.Text="關閉本機面板 (Ctrl+Shift+F9)";LeaveGameFocus("open");int epoch=++webEpoch;
        web=new WebView2{Dock=DockStyle.Fill};webHost.Controls.Add(web);web.BringToFront();web.Enter+=WebEnter;web.KeyDown+=(_,e)=>{if(ProcessDevToolsKey(e.KeyData,!webShortcutHeld.Add(e.KeyCode))){e.Handled=true;e.SuppressKeyPress=true;}};web.KeyUp+=(_,e)=>webShortcutHeld.Remove(e.KeyCode);initializing=InitializePanel(web,epoch);return initializing;}
    void WebEnter(object? sender,EventArgs e){webShortcutHeld.Clear();LeaveGameFocus("webview-enter");}
    async Task FocusWebInput(){await OpenPanel();if(!webReady||web==null)return;LeaveGameFocus("Ctrl+Shift+F11-input");web.Focus();await web.ExecuteScriptAsync("document.getElementById('notes').focus()");WebLog("focus-web-input",new{source="native Ctrl+Shift+F11 command"});}
    async Task InitializePanel(WebView2 view,int epoch){
        try{
            string udf=Path.Combine(stageRoot,"runtime",$"session-{Environment.ProcessId}");
            string? runtime=webFault=="runtime-missing"?Path.Combine(stageRoot,"vendor","absent-runtime"):null;
            if(webFault=="init-failed"){udf=Path.Combine(stageRoot,"vendor","microsoft.web.webview2.1.0.4258.31.nupkg");}
            WebLog("initialization-start",new{epoch,udf,runtime,fault=webFault});
            // Preflight prevents Chromium's modal data-directory error from blocking shutdown.
            Directory.CreateDirectory(udf);string probe=Path.Combine(udf,"write-probe");File.WriteAllText(probe,"P08");File.Delete(probe);
            var options=new CoreWebView2EnvironmentOptions("--disable-background-networking --disable-component-update");
            var env=await CoreWebView2Environment.CreateAsync(runtime,udf,options);
            if(epoch!=webEpoch){WebLog("initialization-cancelled",new{epoch,phase="environment"});return;}
            webEnvironment=env;await view.EnsureCoreWebView2Async(env);
            WebLog("browser-created",new{epoch,browserPid=view.CoreWebView2.BrowserProcessId,version=env.BrowserVersionString,udf});
            if(epoch!=webEpoch){view.Dispose();WebLog("initialization-cancelled",new{epoch,phase="controller"});return;}
            view.ZoomFactor=96d/DeviceDpi;
            var core=view.CoreWebView2;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
            // All local requests use ResourceRequested; a virtual-host mapping bypasses it.

            core.NavigationStarting+=NavigationStarting;core.NavigationCompleted+=NavigationCompleted;core.WebMessageReceived+=WebMessage;
            core.NewWindowRequested+=NewWindow;core.PermissionRequested+=Permission;core.DownloadStarting+=Download;core.ProcessFailed+=ProcessFailed;
            WebLocalResource.Install(core,ResourceRequested);
            core.Navigate(webFault=="page-failed"?"https://p08.example/missing.html":LocalPage);
        }catch(Exception ex){if(epoch==webEpoch){webReady=false;webNotice.Text="面板無法啟動；離線遊戲仍可使用。\n"+ex.GetType().Name;webNotice.BringToFront();}WebLog("initialization-error",new{epoch,type=ex.GetType().Name,error=ex.Message});}
    }
    void NavigationStarting(object? s,CoreWebView2NavigationStartingEventArgs e){if(e.Uri!=LocalPage&&!(webFault=="page-failed"&&e.Uri=="https://p08.example/missing.html")){e.Cancel=true;WebLog("navigation-blocked",new{e.Uri});}else{webReady=false;WebLog("navigation-start",new{e.Uri});}}
    void NavigationCompleted(object? s,CoreWebView2NavigationCompletedEventArgs e){WebLog("navigation-completed",new{e.IsSuccess,status=e.HttpStatusCode,error=e.WebErrorStatus.ToString()});if(!e.IsSuccess||e.HttpStatusCode>=400){webReady=false;webNotice.Text="本機頁面載入失敗；關閉面板後可重試。";webNotice.BringToFront();}}
    void NewWindow(object? s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;WebLog("new-window-blocked",new{e.Uri});}
    void Permission(object? s,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;WebLog("permission-denied",new{kind=e.PermissionKind.ToString()});}
    void Download(object? s,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;WebLog("download-blocked");}
    void ProcessFailed(object? s,CoreWebView2ProcessFailedEventArgs e){webReady=false;webNotice.Text="網頁程序停止；可關閉後重開面板。";webNotice.BringToFront();LeaveGameFocus("process-failed");WebLog("process-failed",new{kind=e.ProcessFailedKind.ToString()});}
    void ResourceRequested(object? s,CoreWebView2WebResourceRequestedEventArgs e){
        var local=WebLocalResource.Read(stageRoot,e.Request.Uri,icon=>WebIconGenerator.Png(NativeUi.ItemIconSource.FromRoot(nativeUiRoot),icon));   // T469 (#1054)
        if(local.Status!=403){
            e.Response=webEnvironment!.CreateWebResourceResponse(local.Body is {} bytes?new MemoryStream(bytes,false):null,local.Status,local.Status==200?"OK":"Not Found",local.Headers);
            return;
        }
        Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out var uri);
        if(uri!=null&&uri.Scheme=="https"&&uri.Host=="p08.example"&&uri.Query==""&&ushort.TryParse(Path.GetFileNameWithoutExtension(uri.AbsolutePath),out ushort gfx)&&uri.AbsolutePath=="/ground/"+gfx+".png"&&GroundCatalog.Find(gfx) is {} groundRow){
            try{using var bitmap=GroundSpriteReader.Image(GroundSpriteReader.Read(groundRow));var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);stream.Position=0;e.Response=webEnvironment!.CreateWebResourceResponse(stream,200,"OK","Content-Type: image/png");}
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){e.Response=webEnvironment!.CreateWebResourceResponse(null,404,"Missing verified ground image","Content-Type: text/plain");}
            return;
        }
        e.Response=webEnvironment!.CreateWebResourceResponse(null,403,"Forbidden","Content-Type: text/plain\r\nCache-Control: no-store");WebLog("resource-blocked",new{e.Request.Uri});
    }
    void WebMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e)=>DispatchWeb(e.Source,e.WebMessageAsJson);
    void DispatchWeb(string source,string raw){
        int id=0;try{
            if(source!=LocalPage||raw.Length>32768)throw new FormatException("origin or size");
            using var doc=JsonDocument.Parse(raw);var o=doc.RootElement;
            if(o.ValueKind!=JsonValueKind.Object)throw new FormatException("object required");
            var names=o.EnumerateObject().Select(p=>p.Name).ToArray();
            if(names.Distinct().Count()!=names.Length||!o.GetProperty("v").TryGetInt32(out int v)||v!=1||!o.GetProperty("id").TryGetInt32(out id)||id<1||!o.TryGetProperty("cmd",out var c)||c.ValueKind!=JsonValueKind.String)throw new FormatException("schema");
            string cmd=c.GetString()!;
            if(cmd=="audioSettings"){var command=AudioSettingsCommand.Parse(o);DispatchAudioSettings(command);return;}
            if(cmd.StartsWith("private",StringComparison.Ordinal)){var command=PrivateShopCommand.Parse(o);_=DispatchPrivateShop(id,command);return;}
            if(cmd.StartsWith("bonus",StringComparison.Ordinal)){var command=BonusStatsCommand.Parse(o);_=DispatchBonusStats(id,command);return;}
            if(cmd.StartsWith("trade",StringComparison.Ordinal)){var command=TradeWebCommand.Parse(o);_=DispatchTrade(id,command);return;}
            if(cmd.StartsWith("npc",StringComparison.Ordinal)){var command=NpcWebCommand.Parse(o);_=DispatchNpc(id,command);return;}
            if(LoginWebCommand.IsLoginCommand(cmd)){
                var command=LoginWebCommand.Parse(o);
                // Do not log raw JSON, credentials, the command object, or parser exception text.
                WebLog("login-command",new{id,cmd});_ = DispatchLoginCommand(command,id);return;
            }
            var expected=cmd is "selectMagic" or "castMagic"?new[]{"v","id","cmd","skillId","epoch"}:cmd=="selectMagicTarget"?new[]{"v","id","cmd","targetId","epoch"}:cmd is "pickup" or "drop"?new[]{"v","id","cmd","itemId","epoch","count","selectionRevision","revision","available"}:cmd=="selectGround"?new[]{"v","id","cmd","itemId","epoch"}:cmd=="useItem"?new[]{"v","id","cmd","itemId","epoch","selectionRevision"}:cmd=="selectItem"?new[]{"v","id","cmd","itemId","epoch"}:cmd is "face" or "move"?new[]{"v","id","cmd","direction"}:new[]{"v","id","cmd"};
            if(!names.Order().SequenceEqual(expected.Order()))throw new FormatException("fields");
            if(cmd is not ("selectMagic" or "castMagic" or "selectMagicTarget" or "ready" or "face" or "move" or "attack" or "stop" or "focusGame" or "selectItem" or "useItem" or "selectGround" or "pickup" or "drop"))throw new FormatException("unknown command");
            int d=0;if(cmd is "face" or "move"&&(!o.GetProperty("direction").TryGetInt32(out d)||d<0||d>7))throw new FormatException("direction");
            WebLog("bridge-command",new{id,cmd,direction=d,source,model=World.Snapshot()});
            if(cmd is "selectMagic" or "castMagic" or "selectMagicTarget"){
                if(!o.GetProperty("epoch").TryGetInt32(out int epoch)||epoch!=login.World.WorldEpoch||login.State!=LoginState.InWorld){Reply(id,"世界已過期");return;}
                ClearInput("web-magic");
                if(cmd=="selectMagicTarget"){uint target=o.GetProperty("targetId").GetUInt32();if(target!=0&&!HasVisibleObjectPixel(target)){Reply(id,"目標沒有可見像素");return;}Reply(id,login.World.SelectMagicTarget(target==0?null:target)?"已選取施法對象":"目標已失效");}
                else {int skill=o.GetProperty("skillId").GetInt32();if(cmd=="selectMagic")Reply(id,login.World.SelectMagic(skill)?"已選取魔法":"非原服已學技能");else if(skill!=login.World.SelectedMagicId)Reply(id,"技能選取已改變");else {_=WebCast(id);return;}}
                lastWebState="";PublishWebState();return;
            }
            if(cmd is "selectGround" or "pickup" or "drop"){
                if(!o.GetProperty("itemId").TryGetUInt32(out uint groundId)||!o.GetProperty("epoch").TryGetInt32(out int groundEpoch))throw new FormatException("ground identity");
                if(login.State!=LoginState.InWorld||groundEpoch!=login.World.WorldEpoch){Reply(id,"世界已過期，未送出");return;}
                ClearInput("web-ground");
                if(cmd=="selectGround"){Reply(id,login.World.SelectGround(groundId)?"已選取地面物品":"地面物品已不存在");PublishWebState();return;}
                if(o.GetProperty("count").ValueKind!=JsonValueKind.String)throw new FormatException("ground count");
                string count=o.GetProperty("count").GetString()!;
                var quantitySelection=new GroundQuantitySelection(groundEpoch,groundId,o.GetProperty("selectionRevision").GetInt32(),o.GetProperty("revision").GetInt32(),o.GetProperty("available").GetUInt32());
                if((cmd=="drop"?login.World.SelectedItemId:login.World.SelectedGroundId)!=groundId){Reply(id,"選取已改變，未送出");return;}
                _=WebGround(id,cmd=="pickup",count,quantitySelection);return;
            }
            if(cmd is "selectItem" or "useItem"){
                if(!o.GetProperty("itemId").TryGetUInt32(out uint itemId)||!o.GetProperty("epoch").TryGetInt32(out int itemEpoch))throw new FormatException("item identity");
                if(login.State!=LoginState.InWorld||itemEpoch!=login.World.WorldEpoch){Reply(id,"背包已過期，未送出");return;}
                ClearInput("web-item");if(cmd=="selectItem"){Reply(id,login.World.SelectItem(itemId)?"已選取道具":"道具已不存在");PublishWebState();}else if(!o.GetProperty("selectionRevision").TryGetInt32(out int itemSelection)||!login.World.CurrentItemSelection(itemId,itemEpoch,itemSelection)){Reply(id,"選取已改變，未送出");}else _=WebUseItem(id);return;
            }
            if(cmd=="ready"){webReady=true;lastWebState="";Reply(id,"ready");PublishWebState();}
            else if(cmd=="focusGame"){ReturnGameFocus();Reply(id,"遊戲控制已恢復");}
            else if(cmd=="stop"){ClearInput("web-stop");Reply(id,"已停止輸入");}
            else if(OnlineMode){ClearInput("web-command");_ = OnlineWeb(id,cmd,d);}
            else if(cmd=="attack"){Reply(id,"單次攻擊需要在線世界與有效目標");}
            else if(cmd=="move"){Reply(id,"步行按鈕用於在線模式；離線使用鍵盤");}
            else if(World.Walking||networkMode&&(!network.Ready||network.Busy)){Reply(id,"忙碌／未連線，未套用");}
            else {HandleDown(Keys.D1+d);HandleUp(Keys.D1+d);Reply(id,networkMode?"轉向請求已送出":"已套用離線轉向");PublishWebState();}
        }catch(Exception ex) when(ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException){WebLog("bridge-rejected",new{sourceAllowed=source==LocalPage,rawLength=raw.Length,errorType=ex.GetType().Name});if(source==LocalPage)Reply(id,"操作資料無效，未送出；請檢查欄位與目前狀態。");}
    }
    async Task WebUseItem(int id){bool sent=!World.Walking&&await login.UseSelectedItem();Reply(id,sent?"單次使用已送出；等待原服回報":login.World.ItemNotice);PublishWebState();}
    void Reply(int id,string status,PlayerAudioState? audio=null){if(web?.CoreWebView2==null||web.IsDisposed)return;web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{v=1,kind="reply",id,status,audio}));WebLog("bridge-reply",new{id,status});}
    void PublishWebState(){if(!webReady||web?.CoreWebView2==null||web.IsDisposed)return;
        var nativeFocus=CaptureNativeFocusSnapshot();var audio=AudioSettingsWeb();
        string signature=$"{World.X}/{World.Y}/{World.Direction}/{World.Walking}/{World.Steps}/{networkMode}/{network.State}/{network.Busy}/{network.Confirmed}/{gameFocus}/{login.Revision}/{login.World.ObjectRevision}/{login.World.InventoryRevision}/{login.World.GroundRevision}/{login.World.MagicRevision}/{login.World.InteractionRevision}/{login.World.TradeRevision}/{login.MovementBusy}/{login.MovementAvailable}/{login.MovementBlockReason}/{login.World.PickupOutcomeTimedOut}/{login.World.ShipNotice}/{login.World.SpeedNotice}/{login.World.BonusStatsRevision}/{login.BonusStatsSnapshot().CanSubmit}/{login.BonusStatsSnapshot().CanRefresh}";signature+=$"/supplies/{login.World.SustenanceText}/{login.World.BasicSupplyNotice}";signature+="/private/"+JsonSerializer.Serialize(PrivateShopWeb());signature+="/nativeFocus/"+JsonSerializer.Serialize(nativeFocus);signature+="/audio/"+JsonSerializer.Serialize(audio);if(signature==lastWebState)return;lastWebState=signature;
        var state=new{audio,nativeFocus,nativeFocusSampledAtMs=Environment.TickCount64,privateShop=PrivateShopWeb(),v=1,kind="state",normalProduct,revision=++revision,onlineMode=OnlineMode,character=OnlineMode?login.World.Actor?.Name:"gfx0 → sprite3225",x=OnlineMode?(int?)login.World.Actor?.X:World.X,y=OnlineMode?(int?)login.World.Actor?.Y:World.Y,direction=OnlineMode?(int?)login.World.Actor?.Direction:World.Direction,walking=World.Walking,movementControl=login.MovementControlSnapshot(World.Walking),steps=World.Steps,networkMode,connection=network.State,busy=network.Busy,confirmed=network.Confirmed,gameFocus,presence=PresenceSnapshot(),targetName=login.World.Target is {} target?objectAssets?.DisplayName(target):null,bonusStats=login.BonusStatsSnapshot(),trade=TradeWeb(),interaction=InteractionWeb(),magic=MagicWeb(),inventory=InventoryWeb(),ground=GroundWeb(),login=login.Snapshot(),loginControl=loginControl?.Snapshot()};web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(state));WebLog("bridge-state",state);
    }
    async Task ClosePanel(){WebLog("close-request",new{initializing=initializing is {IsCompleted:false},epoch=webEpoch,formClosing=webClosing});++webEpoch;webReady=false;ClearInput("panel-close");var old=web;
        if(initializing!=null)await initializing;initializing=null;
        if(old!=null){old.Enter-=WebEnter;if(!old.IsDisposed&&old.CoreWebView2 is {} core){core.NavigationStarting-=NavigationStarting;core.NavigationCompleted-=NavigationCompleted;core.WebMessageReceived-=WebMessage;core.NewWindowRequested-=NewWindow;core.PermissionRequested-=Permission;core.DownloadStarting-=Download;core.ProcessFailed-=ProcessFailed;core.WebResourceRequested-=ResourceRequested;core.Stop();}old.Dispose();}
        web=null;webEnvironment=null;devTools?.Hide();webToggle.Text="開啟本機面板 (Ctrl+Shift+F9)";lastWebState="";WebLog("panel-disposed",new{held=Input.Held.Count});if(!webClosing)ReturnGameFocus();webJournal?.Flush();
    }
}
