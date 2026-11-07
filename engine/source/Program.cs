using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace P06;
static class Program {
    static void awaitReal(string path)=>RealLoginTests.Probe(path).GetAwaiter().GetResult();
    [STAThread] static int Main(string[] args) {
        string? Value(string k) {int i=Array.IndexOf(args,k);return i>=0&&i+1<args.Length?args[i+1]:null;}
        string root=Value("--assets")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"));
        try {
            bool normalProduct=ProductEntry.Normal(args);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(Value("--opacity-probe") is string opi){OpacityScenario.Probe(opi).GetAwaiter().GetResult();return 0;}
            if(Value("--opacity-replay") is string opr){OpacityReplayTests.Run(opr);return 0;}
            if(Value("--opacity-pixels") is string opp){OpacityPixelTests.Run(opp);return 0;}
            if(Value("--opacity-tests") is string opt){OpacityTests.Run(opt);return 0;}
            if(Value("--opacity-scheduler-tests") is string ost){OpacitySchedulerTests.Run(ost);return 0;}
            if(Value("--magic-target-tests") is string mtt){LoginConnection.MagicTargetTests(mtt).GetAwaiter().GetResult();return 0;}
            if(Value("--dual-block-tests") is string dbt){DualBlockTests.Run(dbt);return 0;}
            if(Value("--magic-probe") is string magicProbe){MagicScenario.Probe(magicProbe).GetAwaiter().GetResult();return 0;}
            if(Value("--magic-tests") is string mt){LoginConnection.MagicTests(mt).GetAwaiter().GetResult();return 0;}
            if(Value("--magic-render-tests") is string mrt){MagicRenderingTests.Run(mrt);return 0;}
            if(Value("--ranged-gates") is string rg){LoginConnection.RangedGateTests(rg).GetAwaiter().GetResult();return 0;}
            if(Value("--ranged-render-tests") is string rr){RangedRenderingTests.Run(rr);return 0;}
            if(Value("--ranged-tests") is string ranged){RangedTests.Run(ranged,Value("--output")!);return 0;}
            if(Value("--equipment-tests") is string et){EquipmentTests.Run(et,Value("--output")!);return 0;}
            if(Value("--equipment-gates") is string eg){LoginConnection.EquipmentGateTests(eg).GetAwaiter().GetResult();return 0;}
            if(Value("--ranged-probe") is string rangedProbe){RangedScenario.Probe(rangedProbe).GetAwaiter().GetResult();return 0;}
            if(Value("--equipment-probe") is string ep){EquipmentScenario.Probe(ep).GetAwaiter().GetResult();return 0;}
            if(Value("--reward-selection-tests") is string rs){RewardTests.SelectionReplay(rs,Value("--output")!);return 0;}
            if(Value("--reward-tests") is string rt){RewardTests.Run(rt,Value("--output")!);return 0;}
            if(Value("--reward-probe") is string rp24){RewardScenario.Probe(rp24).GetAwaiter().GetResult();return 0;}
            if(Value("--ground-probe") is string gp){GroundScenario.Probe(gp).GetAwaiter().GetResult();return 0;}
            if(Value("--ground-transfer") is string gt){GroundTransfer.Probe(gt).GetAwaiter().GetResult();return 0;}
            if(Value("--ground-regression") is string gr){LoginConnection.GroundRegression(gr).GetAwaiter().GetResult();return 0;}
            if(Value("--ground-oracle") is string go){GroundTests.Oracle(go,Value("--output")!);return 0;}
            if(Value("--inventory-oracle") is string io){InventoryIconOracle.Packets(io,Value("--output")!);return 0;}
            if(Value("--inventory-probe") is string ip){InventoryScenario.Probe(ip).GetAwaiter().GetResult();return 0;}
            if(Value("--inventory-regression") is string ir){LoginConnection.InventoryRegression(ir).GetAwaiter().GetResult();return 0;}
            if(Value("--inventory-icons") is string ii){InventoryIconOracle.Run(ii);return 0;}
            if(Value("--journal-test") is string jt){JournalTests.Run(jt).GetAwaiter().GetResult();return 0;}
            if(Value("--duplicate-recovery") is string dr){DuplicateRecoveryProbe.Run(dr).GetAwaiter().GetResult();return 0;}
            if(Value("--combat-visibility-plan") is string cv){using var renderer=new Renderer(new Assets(root));Json.Save(cv,renderer.CombatVisibilityPlan(new ObjectAssets(root)));return 0;}
            if(Value("--combat-regression") is string cr){LoginConnection.CombatRegression(cr,Value("--output")!).GetAwaiter().GetResult();return 0;}
            if(Value("--combat-probe") is string cp){CombatTests.Probe(cp).GetAwaiter().GetResult();return 0;}
            if(Value("--object-regression") is string ob){ObjectRegression.Run(new Assets(root),ob,Value("--output")!);return 0;}
            if(Value("--object-probe") is string op){ObjectTests.Probe(op).GetAwaiter().GetResult();return 0;}
            if(Value("--movement-regression") is string mr){LoginConnection.MovementGateRegression(mr,Value("--output")!).GetAwaiter().GetResult();return 0;}
            if(Value("--movement-probe") is string mp){MovementTests.Probe(mp).GetAwaiter().GetResult();return 0;}
            if(Value("--world-regression") is string wr){WorldRegression.Run(wr,Value("--output")!).GetAwaiter().GetResult();return 0;}
            if(Value("--world-probe") is string wp){WorldTests.Probe(wp).GetAwaiter().GetResult();return 0;}
            if(Value("--real-probe") is string rp){awaitReal(rp);return 0;}
            if(Value("--login-vectors") is string lv){LoginTests.Vectors(lv,Value("--output")!);return 0;}
            if(Value("--login-probe") is string lp){LoginTests.Probe(int.Parse(lp),Value("--scenario")!,Value("--output")!).GetAwaiter().GetResult();return 0;}
            if(Value("--protocol-test") is string vectors){ProtocolTests.Run(vectors,Value("--output")!);return 0;}
            if(Value("--cache-tests") is string cacheTests){CacheTests.Run(cacheTests);return 0;}
            if(Value("--cache-pixels") is string cachePixels){CacheTests.Pixels(cachePixels);return 0;}
            if(Value("--cache-opacity") is string cacheOpacity){CacheTests.Opacity(cacheOpacity);return 0;}
            if(Value("--cache-session") is string cacheSession){CacheTests.Session(cacheSession);return 0;}
            var assets=new Assets(root);
            if(Value("--test") is string output){Tests.Run(assets,output);return 0;}
            if(args.Contains("--validate"))return 0;
            using var form=new ClientForm(assets,Value("--evidence"),Value("--demo"),Value("--port"),Value("--network-demo"),Value("--web-demo"),Value("--web-fault"),Value("--login-port"),Value("--login-demo"),normalProduct);try{Application.Run(form);return form.NetworkExitCode;}finally{form.Dispose();form.CompleteWebJournal();}
        } catch(Exception ex) {
            var msg="客戶端無法啟動："+ex.Message;
            if(args.Contains("--opacity-scheduler-tests")){if(Value("--error-log") is string schedulerError)Json.Save(schedulerError,new{error=ex.ToString()});return 2;}
            if(args.Any(a=>a.StartsWith("--cache-"))){if(Value("--error-log") is string cacheError)Json.Save(cacheError,new{error=ex.ToString()});return 2;}
            if(Value("--error-log") is string log)Json.Save(log,new{error=msg,type=ex.GetType().FullName,stack=ex.ToString()});
            if(!args.Contains("--opacity-probe")&&!args.Contains("--opacity-tests")&&!args.Contains("--opacity-pixels")&&!args.Contains("--opacity-replay")&&!args.Contains("--magic-target-tests")&&!args.Contains("--dual-block-tests")&&!args.Contains("--magic-render-tests")&&!args.Contains("--magic-tests")&&!args.Contains("--magic-probe")&&!args.Contains("--ranged-gates")&&!args.Contains("--ranged-render-tests")&&!args.Contains("--ranged-probe")&&!args.Contains("--ranged-tests")&&!args.Contains("--equipment-probe")&&!args.Contains("--equipment-tests")&&!args.Contains("--reward-selection-tests")&&!args.Contains("--reward-tests")&&!args.Contains("--reward-probe")&&!args.Contains("--ground-transfer")&&!args.Contains("--ground-probe")&&!args.Contains("--ground-regression")&&!args.Contains("--ground-oracle")&&!args.Contains("--inventory-oracle")&&!args.Contains("--inventory-probe")&&!args.Contains("--inventory-regression")&&!args.Contains("--inventory-icons")&&!args.Contains("--journal-test")&&!args.Contains("--duplicate-recovery")&&!args.Contains("--combat-visibility-plan")&&!args.Contains("--combat-regression")&&!args.Contains("--combat-probe")&&!args.Contains("--object-probe")&&!args.Contains("--object-regression")&&!args.Contains("--validate")&&!args.Contains("--test")&&!args.Contains("--protocol-test")&&!args.Contains("--login-vectors")&&!args.Contains("--login-probe")&&!args.Contains("--real-probe")&&!args.Contains("--world-probe")&&!args.Contains("--world-regression")&&!args.Contains("--movement-probe")&&!args.Contains("--movement-regression"))MessageBox.Show(msg,"P24 啟動錯誤",MessageBoxButtons.OK,MessageBoxIcon.Error);
            return 2;
        }
    }
}
public sealed partial class ClientForm:Form {
    public World World {get;}
    public InputState Input {get;}=new();
    public Camera Camera {get;}=new();
    readonly Renderer renderer;
    readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    readonly Stopwatch clock=Stopwatch.StartNew();
    double last;
    readonly Panel bar=new(){Dock=DockStyle.Bottom,Height=80,BackColor=Color.FromArgb(25,34,36)};
    readonly Label status=new(){Dock=DockStyle.Fill,ForeColor=Color.FromArgb(225,232,220),Padding=new(14,8,10,4)};
    readonly List<object> events=[];
    readonly string? evidence,demo;
    Point? drag;
    Point? selected;
    Bitmap? patch;
    Rectangle patchRect;
    string previous="";
    string lastPaintMode="none";uint? lastPaintActorId;int paintCount;
    bool buildingBack,treeBack;
    public ClientForm(Assets assets,string? evidence,string? demo,string? port=null,string? networkDemo=null,string? webDemo=null,string? webFault=null,string? loginPort=null,string? loginDemo=null,bool normalProduct=false) {
        this.normalProduct=normalProduct;this.evidence=evidence;this.demo=demo;World=new(assets);renderer=new(assets);
        Text="P24 • 原場景透明 / 遮擋 • map0 / 7ffd8002 + 7ffd8003";ClientSize=new(1180,800);MinimumSize=new(720,500);StartPosition=FormStartPosition.CenterScreen;
        Camera.SetBounds(assets.CanvasWidth,assets.CanvasHeight);
        AutoScaleMode=AutoScaleMode.None;Font=new("Microsoft JhengHei UI",10);BackColor=Color.FromArgb(17,25,27);DoubleBuffered=true;KeyPreview=true;
        bar.Controls.Add(status);Controls.Add(bar);
        Camera.SetCenter(World.DisplayPosition);
        KeyDown+=(_,e)=>{if(OwnsGameKeyboardKey(e.KeyData)){HandleDown(e.KeyCode);e.Handled=true;e.SuppressKeyPress=true;}};
        KeyUp+=(_,e)=>{if(e.KeyCode==Keys.Enter)playerHud?.ReleaseChatEntryKey();if(OwnsGameKeyboardKey(e.KeyData)||Input.Held.Contains(e.KeyCode)||playerHudHeld.Contains(e.KeyCode)){HandleUp(e.KeyCode);e.Handled=true;}};
        Deactivate+=(_,_)=>ClearInput("deactivate");
        Resize+=(_,_)=>{if(WindowState==FormWindowState.Minimized)ClearInput("minimize");Invalidate();};
        MouseDown+=async(_,e)=>{if(PlayerChatFocused){ClearInput("聊天聚焦");return;}if(OnlineMode){if(!PlayerPointerSceneHit(e.Location)){ClearInput("HUD或場景外點擊");return;}ReturnGameFocus();if(e.Button is MouseButtons.Left or MouseButtons.Right){await HandlePlayerPointer(e);return;}}else ReturnGameFocus();if(e.Button==MouseButtons.Middle){drag=e.Location;Capture=true;}else if(e.Button==MouseButtons.Left){var p=Camera.ToScene(e.Location);selected=Coordinates.Pick(p.X,p.Y);events.Add(new{kind="pick",point=selected,inside=Coordinates.Inside(selected.Value.X,selected.Value.Y)});Invalidate();}};
        MouseMove+=(_,e)=>{if(drag is Point p){Camera.Pan(e.X-p.X,e.Y-p.Y);drag=e.Location;Invalidate();}};
        MouseUp+=(_,_)=>{drag=null;Capture=false;};
        MouseCaptureChanged+=(_,_)=>{if(!Capture)drag=null;};
        MouseWheel+=(_,e)=>{if(PlayerChatFocused)return;Camera.Scale(e.Delta>0?1.15f:1/1.15f,e.Location);Invalidate();};
        timer.Tick+=(_,_)=>{double now=clock.Elapsed.TotalMilliseconds;World.Advance(now-last);last=now;if(OnlineMode){SyncWorld();OnlineTick();PlayerPointerTick();}Pump6016();PumpCombatSfx();RefreshScene();};
        Shown+=async(_,_)=>{last=clock.Elapsed.TotalMilliseconds;timer.Start();events.Add(new{kind="shown",pid=Environment.ProcessId});if(demo!=null)await Demo();};
        InitNetwork(port,networkDemo);InitWeb(webDemo,webFault);InitLogin(loginPort,loginDemo);InitInventory();InitGround();InitRewards();InitRecovery();InitMagic();InitInteraction();InitTrade();InitBonusStats();InitPrivateShop();InitNormalPanels();Init6016();InitCombatSfx();
        FormClosed+=(_,_)=>{StopPlayerPointer("視窗關閉");modeEpoch++;timer.Stop();network.Dispose();wireLog.Add("form-closed",new{timerEnabled=timer.Enabled});wireLog.Dispose();timer.Dispose();Input.Clear();World.SetIntent(null);patch?.Dispose();renderer.Dispose();events.Add(new{kind="closed",timerEnabled=false,pid=Environment.ProcessId,model=World.Snapshot()});if(evidence!=null)Json.Save(Path.Combine(evidence,"window-events.json"),events);};
    }
    static bool GameKey(Keys k)=>k is Keys.Escape or Keys.M or Keys.P or Keys.U or Keys.G or Keys.F9 or Keys.F6 or Keys.F7 or Keys.F8 or Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.W or Keys.A or Keys.S or Keys.D or Keys.F or Keys.R or Keys.Space or Keys.F1 or Keys.F2 or Keys.F3 or Keys.F4 or Keys.F12 || (k>=Keys.D1&&k<=Keys.D8) || (k>=Keys.NumPad1&&k<=Keys.NumPad9);
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
        if(ProcessPlayerHudKeyboard(ref msg,keyData))return true;
        if(normalProduct&&keyData==Keys.F9){if(((long)msg.LParam&(1L<<30))==0)_=TogglePanel();return true;}
        if(keyData==Keys.F10){ReturnGameFocus();return true;}
        if(keyData==Keys.F11){_ = FocusWebInput();return true;}
        if(ProcessLoginKeyboardEnter(ref msg,keyData))return true;
        if(ProcessPlayerChatKeyboard(ref msg,keyData))return true;
        if(OwnsGameKeyboardKey(keyData) && keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown){HandleDown(keyData);return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    public void HandleDown(Keys key) {
        if(TryPlayerHudKey(key))return;
        if(key==Keys.F9){_ = TogglePanel();return;}
        if(OnlineMode){if(playerPointer?.Active==true)StopPlayerPointer("鍵盤操作");if(key==Keys.Escape){ClearInput("escape");return;}OnlineDown(key);return;}
        if(NetworkKey(key))return;
        bool fresh=!Input.Held.Contains(key);Input.Down(key);
        if(fresh) {
            if(key==Keys.F){Camera.Follow=!Camera.Follow;}
            if(key==Keys.R){World.Teleport(World.Assets.Spawn[0],World.Assets.Spawn[1]);Input.Clear();Camera.Follow=true;}
            if(key==Keys.Space){Input.Clear();}
            if(key==Keys.F2){buildingBack=!buildingBack;World.Teleport(buildingBack?32590:32589,32930);World.Face(5);Input.Clear();Camera.Follow=true;}
            if(key==Keys.F3){treeBack=!treeBack;World.Teleport(treeBack?32591:32589,treeBack?32938:32941);World.Face(5);Input.Clear();Camera.Follow=true;}
            if(key>=Keys.D1&&key<=Keys.D8)World.Face(key-Keys.D1);
            if(key==Keys.F12&&evidence!=null)CaptureEvidence("manual");
            events.Add(new{kind="keydown",key=key.ToString(),utc=DateTimeOffset.UtcNow});
        }
        World.SetIntent(Input.Direction);RefreshScene();
    }
    public void HandleUp(Keys key) {playerHudHeld.Remove(key);if(OnlineMode){Input.Up(key);login.CancelMovementInput();return;}Input.Up(key);World.SetIntent(Input.Direction);events.Add(new{kind="keyup",key=key.ToString(),utc=DateTimeOffset.UtcNow});}
    // Shared by focus/stop paths; independent of a native handle for offline regression.
    void ClearHeldInput(string reason){playerHudHeld.Clear();StopPlayerPointer(reason);login.CancelMovementInput();Input.Clear();World.SetIntent(null);}
    void ClearInput(string reason){ClearHeldInput(reason);drag=null;Capture=false;events.Add(new{kind="clear-input",reason,model=World.Snapshot()});}
    internal void RefreshSceneImage(){
        // Waiting/cancelled online state has no resident offline scene. Its last coordinates
        // may be on another cache/map; never compose them against the offline canvas.
        if(OnlineMode){
            previous="";
            if(login.World.Renderable){RefreshObjects();return;}
            renderer.ReleaseMap();objectPatch=null;objectVisuals=[];missingObjects=[];patch?.Dispose();patch=null;patchRect=Rectangle.Empty;return;
        }
        string signature=$"{World.X}/{World.Y}/{World.Frame.Key}";
        if(signature!=previous){objectPatch=null;objectVisuals=[];missingObjects=[];var p=renderer.Compose(World);patch?.Dispose();patch=Renderer.BitmapOf(p.Bounds.Width,p.Bounds.Height,p.Colors,p.Mask);patchRect=p.Bounds;previous=signature;}
    }
    public void RefreshScene() {
        if(Camera.Follow)Camera.SetCenter(World.DisplayPosition);
        RefreshSceneImage();
        status.Text=$"世界 {World.X}, {World.Y}   D{World.Direction}   {(World.Walking?"行走":"站立")} {World.Frame.Key}   縮放 {Camera.Zoom:0.00}×   攝影機 {(Camera.Follow?"跟隨":"自由")}   {World.BoundaryNotice}\n"+
            (networkMode?"轉向：1–8；收到回應才更新方向　測試期間移動暫停\n":"移動：方向鍵 / WASD / 數字鍵盤　斜向：Home PgUp End PgDn　轉向：1–8\n")+
            (networkMode?"鏡頭：F 跟隨、滾輪縮放、中鍵拖曳　F8 可回離線移動\n":"鏡頭：F 跟隨、滾輪縮放、中鍵拖曳　R 回起點　F2 建物 / F3 樹木前後示範\n")+
            $"單區塊 • 碰撞/尋路未知，只有區域限制 • 群組未觸發{(selected is Point s?$" • 選格 {s.X},{s.Y}（{(Coordinates.Inside(s.X,s.Y)?"區內":"區外")}）":"")}";
        if(OnlineMode)status.Text=$"P24 真實連線 • {login.State} • {login.Detail}\n"+(login.World.Actor is {} actor?$"{actor.Name} / object {actor.ObjectId} / gfx {actor.Gfx} / map {login.World.MapId} / {actor.X},{actor.Y} / D{actor.Direction}\n":"尚未收到本次完整世界角色資料\n")+$"在線步行：方向鍵／WASD，斜向 Home/PgUp/End/PgDn；轉向：1–8。\n單次攻擊：G；HP {login.World.Status?.Hp}/{login.World.Status?.MaxHp}；{login.World.CombatNotice}";
        else status.Text=NetworkStatus+"\n"+status.Text;
        if(normalProduct){status.Text=OnlineMode?(login.World.Actor is {} player?$"{player.Name} · HP {login.World.Status?.Hp}/{login.World.Status?.MaxHp} · MP {login.World.Status?.Mp}/{login.World.Status?.MaxMp}\n{login.World.SustenanceText}\n方向鍵／WASD 移動 · 點選目標後 G 攻擊 · M 施法 · U 使用道具 · P 拾取":loginControl.Notice):"離線預覽 · 登入後可選擇角色進入世界\n方向鍵／WASD 移動 · 滾輪縮放 · F9 開關面板 · F10 回遊戲";}
        SyncNormalPanels();
        bar.Height=TextRenderer.MeasureText(status.Text,status.Font,new Size(Math.Max(100,ClientSize.Width-(webHost.Visible?webHost.Width:0)-28),1000),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+24;
        SyncPlayerHud();PublishWebState();PublishSceneWeb();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {paintCount++;
        base.OnPaint(e);var viewport=PlayerSceneBounds;Camera.ViewOffset=viewport.Location;Camera.View=viewport.Size;
        e.Graphics.SetClip(viewport);e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        lastPaintMode=OnlineMode?(login.World.Renderable?"online-supported":login.World.Ready?"online-unsupported":"waiting"):"offline";lastPaintActorId=OnlineMode&&login.World.Renderable?login.World.Actor?.ObjectId:null;
        if(OnlineMode&&!login.World.Renderable){
            string message=login.World.TeleportPending?$"{login.World.TeleportNotice}\n{login.World.MapResourceNotice}":login.World.Ready?$"真實世界已連線\nmap {login.World.MapId} / gfx {login.World.Actor?.Gfx}\n{login.World.MapResourceNotice}\n{login.World.AppearanceNotice}":"等待真實世界初始化\n可取消連線或回離線預覽。";
            e.Graphics.DrawString(message,Font,Brushes.White,new PointF(viewport.Left+20,viewport.Top+80));return;
        }
        if(OnlineMode&&renderer.HasShipBackdrop&&objectPatch!=null){
            var topLeft=Camera.ToScene(viewport.Location);var bottomRight=Camera.ToScene(new PointF(viewport.Right,viewport.Bottom));
            var area=Rectangle.Intersect(renderer.Bounds,Rectangle.FromLTRB((int)Math.Floor(topLeft.X),(int)Math.Floor(topLeft.Y),(int)Math.Ceiling(bottomRight.X),(int)Math.Ceiling(bottomRight.Y)));
            if(area.Width>0&&area.Height>0){using var scene=renderer.SceneView(objectPatch,area);var p=Camera.ToView(area.Location);e.Graphics.DrawImage(scene,new RectangleF(p.X,p.Y,scene.Width*Camera.Zoom,scene.Height*Camera.Zoom));}
            PaintObjectSelection(e.Graphics);return;
        }
        var origin=Camera.ToView(renderer.Bounds.Location);e.Graphics.DrawImage(renderer.Background,new RectangleF(origin.X,origin.Y,renderer.Width*Camera.Zoom,renderer.Height*Camera.Zoom));
        if(patch!=null){var p=Camera.ToView(patchRect.Location);e.Graphics.DrawImage(patch,new RectangleF(p.X,p.Y,patch.Width*Camera.Zoom,patch.Height*Camera.Zoom));}
        if(OnlineMode)PaintObjectSelection(e.Graphics);
        if(!OnlineMode&&selected is Point s){var p=Camera.ToView(Coordinates.Screen(s.X,s.Y));using var pen=new Pen(Coordinates.Inside(s.X,s.Y)?Color.FromArgb(220,204,133):Color.OrangeRed,1.5f);e.Graphics.DrawPolygon(pen,new PointF[]{new(p.X-24*Camera.Zoom,p.Y),new(p.X,p.Y-12*Camera.Zoom),new(p.X+24*Camera.Zoom,p.Y),new(p.X,p.Y+12*Camera.Zoom)});}
    }
    void CaptureEvidence(string name) {
        string output=demo??evidence!;Directory.CreateDirectory(output);RefreshScene();Update();
        using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new(Point.Empty,Size));b.Save(Path.Combine(output,name+"-client.png"),ImageFormat.Png);}
        try {using var b=new Bitmap(Width,Height);using var g=Graphics.FromImage(b);g.CopyFromScreen(Left,Top,0,0,Size);b.Save(Path.Combine(output,name+"-screen.png"),ImageFormat.Png);events.Add(new{kind="screen-copy",name,success=true});}
        catch(Exception ex){events.Add(new{kind="screen-copy",name,success=false,error=ex.ToString()});}
        Json.Save(Path.Combine(output,name+"-state.json"),World.Snapshot());
        if(OnlineMode)Json.Save(Path.Combine(output,name+"-objects.json"),ObjectUiSnapshot());
        Json.Save(Path.Combine(output,name+"-network.json"),new{networkMode,network.State,network.Detail,network.Busy,network.Confirmed,network.Generation,model=World.Snapshot()});
    }
    async Task Demo() {
        // Explicit in-process UI/controller harness. This does not claim external native input.
        try {
            Directory.CreateDirectory(demo!);await Task.Delay(400);CaptureEvidence("01-spawn");
            foreach(var (key,n) in new[]{(Keys.Home,0),(Keys.Up,1),(Keys.PageUp,2),(Keys.Right,3),(Keys.PageDown,4),(Keys.Down,5),(Keys.End,6),(Keys.Left,7)}) {
                HandleDown(key);await Task.Delay(340);CaptureEvidence($"direction-{n}-middle");HandleUp(key);await Task.Delay(500);
            }
            HandleDown(Keys.Up);HandleDown(Keys.Right);await Task.Delay(180);
            using(var focusProbe=new Form{Text="P06 focus lifecycle test",ShowInTaskbar=false,Size=new(280,100),StartPosition=FormStartPosition.CenterParent}) {
                focusProbe.Show(this);focusProbe.Activate();await Task.Delay(150);
                if(Input.Held.Count!=0||World.Intent!=null)throw new Exception("Actual Deactivate event did not clear held keys");
                events.Add(new{kind="actual-focus-switch",heldCount=Input.Held.Count,intent=World.Intent});focusProbe.Close();
            }
            Activate();await Task.Delay(800);
            if(World.Walking||Input.Held.Count!=0)throw new Exception("Focus clear left moving");
            HandleDown(Keys.F2);HandleUp(Keys.F2);await Task.Delay(100);CaptureEvidence("building-back");HandleDown(Keys.F2);HandleUp(Keys.F2);await Task.Delay(100);CaptureEvidence("building-front");
            HandleDown(Keys.F3);HandleUp(Keys.F3);await Task.Delay(100);CaptureEvidence("tree-back");HandleDown(Keys.F3);HandleUp(Keys.F3);await Task.Delay(100);CaptureEvidence("tree-front");
            ClientSize=new(900,640);await Task.Delay(100);CaptureEvidence("02-resized");
            ClientSize=new(1180,800);World.Teleport(World.Assets.Spawn[0],World.Assets.Spawn[1]);Camera.Follow=false;Camera.SetCenter(new(1536,816));Camera.Scale(.45f,new(590,360));await Task.Delay(100);CaptureEvidence("03-overview");
            Json.Save(Path.Combine(demo!,"ui-result.json"),new{status="pass",kind="in-process WinForms message-loop/controller harness",nativeExternalInput=false,pid=Environment.ProcessId,events});
        } catch(Exception ex){Json.Save(Path.Combine(demo!,"ui-result.json"),new{status="fail",error=ex.ToString(),events});}
        finally{Close();}
    }
}

