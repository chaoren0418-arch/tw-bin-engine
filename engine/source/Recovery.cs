using System.Globalization;
using P06.NativeUi;
namespace P06;

// M7-X（T224／#531）：死亡統計窗 UserPlayStatsUI 與死亡逾時離線。依據 docs/reference/world-objects.md 第 6 節。
// 已驗證（WO§6）：自身死亡處理 sub_8C4EE0（FO 0x4C42E0）顯示 UserPlayStatsUI（地圖 2600 延後 5000 ms），
//   啟動計時器 0x29A：一般 501,264 ms，地圖 1017–1023 為 1,801,234 ms；到期（0x8BEEA9／FO 0x4BE2A9）仍死亡就送 C_QUIT 並關窗。
// 線上 opcode：TW16 C_QUIT(68) 對應 3.81 伺服器 C122 QUITGAME（C_Disconnect，無欄位；gap-matrix 第 2 節、SP C122）。
public static class DeathTimeoutRules {
    public const int QuitTimeoutMs=501264;
    public const int LongQuitTimeoutMs=1801234;
    public const int DelayedWindowMs=5000;
    public const ushort DelayedWindowMap=2600;
    public const byte QuitOpcode=122;
    public static int QuitTimeoutFor(ushort? map)=>map is >=1017 and <=1023?LongQuitTimeoutMs:QuitTimeoutMs;
    public static int WindowDelayFor(ushort? map)=>map==DelayedWindowMap?DelayedWindowMs:0;
    public static byte[] Quit()=>LoginPackets.Pad([QuitOpcode]);
}

public sealed record DeathView(string Phase,ushort? Map,long? DiedAt,long? WindowAt,long? QuitAt,long? RemainingMs,bool WindowOpen,bool Dismissed,bool QuitSent);

/// <summary>死亡計時的純狀態機；時間由呼叫端傳入，不讀時鐘、不送封包。</summary>
public sealed class DeathWatch {
    ushort? map;long? diedAt;bool dismissed,quitSent;
    public bool Active=>diedAt!=null;
    /// <summary>死亡起點：已有起點時不覆寫（同一次死亡只計一次）。</summary>
    public void Stamp(long now,ushort? deathMap){if(diedAt!=null)return;diedAt=now;map=deathMap;dismissed=false;quitSent=false;}
    public void Update(bool dead,long now,ushort? currentMap){if(!dead){Clear();return;}Stamp(now,currentMap);}
    public void Clear(){diedAt=null;map=null;dismissed=false;quitSent=false;}
    public long? WindowAt=>diedAt+DeathTimeoutRules.WindowDelayFor(map);
    public long? QuitAt=>diedAt+DeathTimeoutRules.QuitTimeoutFor(map);
    public bool WindowOpen(long now)=>diedAt!=null&&!dismissed&&!quitSent&&now>=WindowAt;
    public bool QuitDue(long now)=>diedAt!=null&&!quitSent&&now>=QuitAt;
    /// <summary>取消／關閉鈕：只關窗，計時照走（原版計時器與視窗分開）。</summary>
    public bool Dismiss(){if(diedAt==null||dismissed)return false;dismissed=true;return true;}
    public bool Reopen(){if(diedAt==null||!dismissed||quitSent)return false;dismissed=false;return true;}
    public void MarkQuitSent(){if(diedAt!=null)quitSent=true;}
    public DeathView View(long now)=>diedAt==null?new("alive",null,null,null,null,null,false,false,false)
        :new(quitSent?"quit-sent":dismissed?"dismissed":now<WindowAt?"window-delayed":"window-open",map,diedAt,WindowAt,QuitAt,Math.Max(0,QuitAt!.Value-now),WindowOpen(now),dismissed,quitSent);
}

public sealed record UserPlayStatsButton(string Name,int X,int Y,int? BaseSurf,string MouseEvent);
public sealed record UserPlayStatsRow(string Name,int? IconSurf,int IconY,int? StringId,int LabelWidth,int ValueWidth,int Height,int Gap,Color LabelColor,Color ValueColor);
/// <summary>執行期由原客戶端 Tile.pak 的 UserPlayUI.xml 解析；缺節點或屬性丟 UiLayoutException，不補預設座標。</summary>
public sealed record UserPlayStatsLayout(int? BaseSurf,int? FrameSurf,bool Toggle,UserPlayStatsButton Restart,UserPlayStatsButton Quit,UserPlayStatsButton Cancel,UserPlayStatsButton? Close,int ListX,int ListY,int RowHeight,IReadOnlyList<UserPlayStatsRow> Rows) {
    public const string FileName="UserPlayUI";
    public const string RootName="UserPlayStatsUI";
    public static UserPlayStatsLayout From(UiLayout layout){
        var root=layout.Root;
        if(root.Name!=RootName)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {RootName}");
        var window=root.Child("Window");
        UserPlayStatsButton? Find(string name,bool required){
            var matches=window.ChildrenNamed("Button").Where(b=>b.Attr("Name")==name).ToList();
            if(matches.Count>1)throw new UiLayoutException(layout.FileName,window.Path+"/Button[@Name="+name+"]","節點不唯一");
            if(matches.Count==0){if(required)throw new UiLayoutException(layout.FileName,window.Path+"/Button[@Name="+name+"]","缺少節點");return null;}
            var node=matches[0];
            return new(name,node.Int("X"),node.Int("Y"),FirstSurf(node,"BaseSurf"),node.RequiredAttr("MouseEvent"));
        }
        var list=window.ChildrenNamed("List").SingleOrDefault(l=>l.Attr("Name")=="UserPlayStats_List")
            ??throw new UiLayoutException(layout.FileName,window.Path+"/List[@Name=UserPlayStats_List]","缺少節點");
        var rows=list.ChildrenNamed("List").Select(row=>{
            var labels=row.ChildrenNamed("Label").ToList();
            if(labels.Count<2)throw new UiLayoutException(layout.FileName,row.Path,"統計列需要名稱與數值兩個 Label");
            var icon=row.ChildrenNamed("Image").FirstOrDefault();
            return new UserPlayStatsRow(row.RequiredAttr("Name"),icon==null?null:FirstSurf(icon,"Surf"),icon?.TryInt("Y")??0,labels[0].TryInt("String"),
                labels[0].Int("Width"),labels[1].Int("Width"),row.Int("Height"),row.TryInt("Gap")??0,ParseColor(labels[0]),ParseColor(labels[1]));
        }).ToArray();
        int rowHeight=rows.Length==0?0:rows[0].Height;
        return new(FirstSurf(window,"BaseSurf"),FirstSurf(window,"FrameSurf"),string.Equals(root.Attr("Toggle"),"true",StringComparison.OrdinalIgnoreCase),
            Find("Restart_Button",true)!,Find("Quit_Button",true)!,Find("Cancel_Button",true)!,Find("CloseWindow_Button",false),list.Int("X"),list.Int("Y"),rowHeight,rows);
    }
    /// <summary>"5322;1" → 5322；原版解析只取第一個數字（V4 (b)）。沒有屬性或空值＝不畫。</summary>
    public static int? FirstSurf(UiNode node,string name){
        string? raw=node.Attr(name);if(raw==null||raw.Trim().Length==0)return null;
        string head=new(raw.Trim().TakeWhile(char.IsAsciiDigit).ToArray());
        if(head.Length==0||!int.TryParse(head,NumberStyles.None,CultureInfo.InvariantCulture,out int v))throw new UiLayoutException(node.Layout.FileName,node.Path,$"屬性 {name}=\"{raw}\" 不是 surf 編號");
        return v;
    }
    static Color ParseColor(UiNode node){
        string? raw=node.Attr("FontColor");if(raw==null)return Color.White;
        var parts=raw.Split(',');
        if(parts.Length!=3||parts.Any(p=>!byte.TryParse(p.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out _)))throw new UiLayoutException(node.Layout.FileName,node.Path,$"FontColor=\"{raw}\" 不是 R,G,B");
        return Color.FromArgb(byte.Parse(parts[0].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[1].Trim(),CultureInfo.InvariantCulture),byte.Parse(parts[2].Trim(),CultureInfo.InvariantCulture));
    }
}

public sealed partial class WorldSession {
    public bool RestartRequested {get;internal set;}
    bool recoveryAppearancePending;
    readonly DeathWatch deathWatch=new();
    internal Func<long> DeathClock=()=>Environment.TickCount64;
    internal void ResetRecovery(){RestartRequested=false;recoveryAppearancePending=false;deathWatch.Clear();}
    void CompleteRecoveryAppearance(){if(!recoveryAppearancePending)return;recoveryAppearancePending=false;SelfAppearancePending=false;}
    bool ReceiveRecovery(byte opcode,LoginReader r){
        if(opcode!=85)return false;
        uint id=r.D();byte type=r.C();r.D();uint gfx=r.D();r.Finish();
        if(type!=0||gfx>ushort.MaxValue){Unhandled(opcode);return true;}
        if(Actor?.ObjectId==id&&OwnDead){
            CancelPickupPresentation("resurrection");pendingAction=null;OwnCombat=null;OwnDead=false;RestartRequested=false;
            Actor=Actor with{Gfx=(ushort)gfx};AppearanceRevision++;
            // S85 has no weapon or HP. Wait for S119 and positive authoritative HP.
            recoveryAppearancePending=true;SelfAppearancePending=true;
            // Original S_RESURRECT closes UserPlayStatsUI (WO§6); the timer stops with the death.
            deathWatch.Clear();
            CombatNotice="收到復活回報；等待角色外觀與生命值同步。";
        }else UpdateObject(id,o=>o.Dead?o with{Dead=false,Combat=null,ReceivedAction=null,Gfx=(ushort)gfx,MotionAt=0}:o);
        return true;
    }
    // Stamp the death moment from the S158 action 8 that Combat turns into OwnDead (observers run before core dispatch).
    [WorldReceive(158,Observe=true)]
    void ObserveOwnDeathAction(byte[] plain){
        if(plain.Length<6||plain[5]!=8||OwnDead||Actor is not {} own||BitConverter.ToUInt32(plain,1)!=own.ObjectId)return;
        deathWatch.Stamp(DeathClock(),MapId);
    }
    /// <summary>以目前 OwnDead 同步計時；漏掉 S158 時以第一次觀察到死亡為起點。</summary>
    internal void TrackDeath()=>deathWatch.Update(OwnDead,DeathClock(),MapId);
    internal DeathView DeathSnapshot(){TrackDeath();return deathWatch.View(DeathClock());}
    internal bool DeathQuitDue{get{TrackDeath();return OwnDead&&deathWatch.QuitDue(DeathClock());}}
    internal void DeathQuitSent(){deathWatch.MarkQuitSent();CombatNotice="死亡後逾時未重新開始，已依原版送出離線。";}
    internal bool DismissDeathWindow(){TrackDeath();return deathWatch.Dismiss();}
    internal bool ReopenDeathWindow(){TrackDeath();return OwnDead&&deathWatch.Reopen();}
    [WorldSnapshot("death")]
    object DeathExtensionSnapshot(){var v=DeathSnapshot();return new{phase=v.Phase,map=v.Map,diedAt=v.DiedAt,windowAt=v.WindowAt,quitAt=v.QuitAt,remainingMs=v.RemainingMs,windowOpen=v.WindowOpen,dismissed=v.Dismissed,quitSent=v.QuitSent,restartRequested=RestartRequested};}
}

public sealed partial class LoginConnection {
    bool restartSending;
    int deathQuitSendingGeneration=-1;
    public bool CanRestart=>State==LoginState.InWorld&&World.OwnDead&&World.Actor!=null&&!World.RestartRequested&&!restartSending&&!World.TeleportPending&&sendMovementGate!=null;
    public async Task<bool> RestartAfterDeath(){
        if(!CanRestart)return false;
        int session=generation,epoch=World.WorldEpoch,appearance=World.AppearanceRevision;
        uint id=World.Actor!.ObjectId;var sender=sendMovementGate!;restartSending=true;
        bool Current()=>session==generation&&epoch==World.WorldEpoch&&appearance==World.AppearanceRevision&&World.Actor?.ObjectId==id;
        try{
            bool sent=await sender(LoginPackets.Pad([177]),()=>Current()&&State==LoginState.InWorld&&World.OwnDead&&!World.RestartRequested&&!World.TeleportPending,()=>{
                // Original restart closes UserPlayStatsUI before sending (WO§6, sub_C0BB20).
                World.RestartRequested=true;World.DismissDeathWindow();CancelMovementInput();World.CombatNotice="已要求死亡後重新開始，等待伺服器恢復；不會自動重送。";Set(State,World.CombatNotice);
            });
            return sent&&session==generation;
        }catch(Exception ex){if(Current()){Cancel();Set(LoginState.Faulted,"重新開始傳送失敗："+ex.Message);}return false;}
        finally{if(session==generation)restartSending=false;}
    }
    public DeathView DeathSnapshot()=>CaptureWorld(()=>State==LoginState.InWorld?World.DeathSnapshot():new DeathView("alive",null,null,null,null,null,false,false,false));
    public bool DismissDeathWindow()=>State==LoginState.InWorld&&CaptureWorld(World.DismissDeathWindow);
    public bool ReopenDeathWindow()=>State==LoginState.InWorld&&CaptureWorld(World.ReopenDeathWindow);
    /// <summary>死亡逾時：到期且仍死亡時送一次 C122（TW16 C_QUIT），送出後關閉連線。未到期或不在世界中回 false。</summary>
    public async Task<bool> CheckDeathTimeout(){
        int session=generation;
        bool Eligible()=>session==generation&&State==LoginState.InWorld&&sendMovementGate!=null&&CaptureWorld(()=>World.DeathQuitDue);
        if(deathQuitSendingGeneration==session||!Eligible())return false;
        var sender=sendMovementGate!;deathQuitSendingGeneration=session;
        try{
            bool sent=await sender(DeathTimeoutRules.Quit(),Eligible,()=>{CaptureWorld(()=>{World.DeathQuitSent();return true;});CancelMovementInput();});
            if(sent&&session==generation){Cancel();Set(LoginState.Disconnected,"死亡後逾時未重新開始：已依原版送出離線（C122），連線已關閉。");}
            return sent;
        }catch(Exception ex){if(session==generation){Cancel();Set(LoginState.Faulted,"死亡逾時離線傳送失敗："+ex.Message);}return false;}
        finally{if(deathQuitSendingGeneration==session)deathQuitSendingGeneration=-1;}
    }
}

public sealed partial class ClientForm {
    // UserPlayStatsUI. Layout and images are read at runtime from the original client (UserPlayUI.xml, surf);
    // without client resources the same XML-free fallback below is used and labelled as an engine panel.
    readonly Panel recoveryPanel=new(){Size=new(250,200),BackColor=Color.FromArgb(40,32,32),Visible=false};
    readonly Label recoveryNotice=new(){AutoSize=false,ForeColor=Color.White,BackColor=Color.Transparent};
    readonly Button restartButton=new(){Text="重新開始"},deathQuitButton=new(){Text="結束"},deathCancelButton=new(){Text="取消"},deathCloseButton=new(){Text="×",Visible=false};
    readonly Button deathReopenButton=new(){Text="死亡選單",Size=new(96,26),Visible=false};
    readonly List<Label> deathStatLabels=[];
    readonly System.Windows.Forms.Timer deathTimer=new(){Interval=250};
    volatile UserPlayStatsLayout? deathLayout;volatile string deathLayoutNotice="正在讀取原客戶端 UserPlayUI.xml。";
    Dictionary<int,Bitmap>? deathSurfs;bool deathLayoutApplied,deathTicking;
    internal string? DeathLayoutError {get;private set;}
    // WO§6 names the four rows; the real Text.pak strings 3123–3126 are not extracted yet.
    static readonly Dictionary<int,string> DeathStatNames=new(){[3123]="經驗",[3124]="擊殺數",[3125]="金幣",[3126]="取得物品"};
    void InitRecovery(){
        recoveryPanel.Controls.AddRange([recoveryNotice,restartButton,deathQuitButton,deathCancelButton,deathCloseButton]);Controls.Add(recoveryPanel);Controls.Add(deathReopenButton);
        restartButton.Click+=async(_,_)=>{ClearInput("restart-after-death");await login.RestartAfterDeath();SyncRecovery();};
        // "結束" (MouseEvent=Quit): the original handler is not verified; it uses the existing logout path (no packet).
        deathQuitButton.Click+=(_,_)=>{ClearInput("death-quit");CancelLogin(login.SessionGeneration,false);SyncRecovery();};
        deathCancelButton.Click+=(_,_)=>{login.DismissDeathWindow();SyncRecovery();};
        deathCloseButton.Click+=(_,_)=>{login.DismissDeathWindow();SyncRecovery();};
        // Engine transition: the original re-opens the Toggle window by an unverified trigger.
        deathReopenButton.Click+=(_,_)=>{login.ReopenDeathWindow();SyncRecovery();};
        ApplyDeathFallbackLayout();
        _=Task.Run(LoadDeathLayout);
        deathTimer.Tick+=async(_,_)=>await TickDeath();
        FormClosed+=(_,_)=>deathTimer.Dispose();deathTimer.Start();
        InitResurrection();
    }
    void LoadDeathLayout(){
        try{
            var resources=UiResources.Shared;
            var layout=UserPlayStatsLayout.From(resources.Xml.Load(UserPlayStatsLayout.FileName));
            var surfs=new Dictionary<int,Bitmap>();
            foreach(int? id in new[]{layout.BaseSurf,layout.Restart.BaseSurf,layout.Quit.BaseSurf,layout.Cancel.BaseSurf,layout.Close?.BaseSurf}.Concat(layout.Rows.Select(r=>r.IconSurf)))
                if(id is int surf&&!surfs.ContainsKey(surf)&&resources.Surfs.Get(surf) is {} s)surfs[surf]=new Bitmap(s.Bitmap); // Own a copy: catalog bitmaps may be evicted.
            deathSurfs=surfs;deathLayout=layout;deathLayoutNotice="";
        }catch(Exception ex){deathLayoutNotice="（引擎面板：原客戶端 UserPlayUI 不可用）";DeathLayoutError=ex.GetType().Name+": "+ex.Message;}
    }
    void ApplyDeathFallbackLayout(){
        recoveryPanel.Size=new(250,170);recoveryPanel.BackgroundImage=null;
        recoveryNotice.SetBounds(10,8,230,36);
        restartButton.SetBounds(66,50,118,24);deathQuitButton.SetBounds(66,78,118,24);deathCancelButton.SetBounds(66,106,118,24);
        foreach(var b in new[]{restartButton,deathQuitButton,deathCancelButton}){b.BackgroundImage=null;b.FlatStyle=FlatStyle.Standard;b.Text=b==restartButton?"重新開始":b==deathQuitButton?"結束":"取消";}
    }
    void ApplyDeathLayout(UserPlayStatsLayout layout){
        var surfs=deathSurfs??[];
        Bitmap? Surf(int? id)=>id is int s&&surfs.TryGetValue(s,out var b)?b:null;
        if(Surf(layout.BaseSurf) is {} back){recoveryPanel.BackgroundImage=back;recoveryPanel.BackgroundImageLayout=ImageLayout.None;recoveryPanel.Size=back.Size;}
        void Place(Button control,UserPlayStatsButton spec){
            control.Location=new(spec.X,spec.Y);
            if(Surf(spec.BaseSurf) is {} image){control.Size=image.Size;control.BackgroundImage=image;control.BackgroundImageLayout=ImageLayout.None;control.FlatStyle=FlatStyle.Flat;control.FlatAppearance.BorderSize=0;control.BackColor=Color.Transparent;control.Text="";}
            else control.Size=new(118,20);
        }
        Place(restartButton,layout.Restart);Place(deathQuitButton,layout.Quit);Place(deathCancelButton,layout.Cancel);
        if(layout.Close is {} close){Place(deathCloseButton,close);deathCloseButton.Visible=true;}
        // Rows of UserPlayStats_List (Vertical list, Horizontal rows: icon, name, value). The list's exact flow rule is inferred.
        foreach(var l in deathStatLabels){recoveryPanel.Controls.Remove(l);l.Dispose();}deathStatLabels.Clear();
        for(int i=0;i<layout.Rows.Count;i++){
            var row=layout.Rows[i];int y=layout.ListY+i*layout.RowHeight,x=layout.ListX;
            if(Surf(row.IconSurf) is {} icon){var pic=new Label{Location=new(x,y+row.IconY),Size=icon.Size,Image=icon,BackColor=Color.Transparent};deathStatLabels.Add(pic);x+=icon.Width+row.Gap;}
            string name=row.StringId is int sid&&DeathStatNames.TryGetValue(sid,out var n)?n:row.StringId is int other?$"#{other}":row.Name;
            deathStatLabels.Add(new Label{Location=new(x,y),Size=new(row.LabelWidth,row.Height),Text=name,ForeColor=row.LabelColor,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft});
            // Values: the original data source is not verified, so no number is shown (no synthesized 0).
            deathStatLabels.Add(new Label{Location=new(x+row.LabelWidth+row.Gap,y),Size=new(row.ValueWidth,row.Height),Text="—",ForeColor=row.ValueColor,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleRight});
        }
        recoveryPanel.Controls.AddRange(deathStatLabels.ToArray());
        recoveryNotice.SetBounds(10,layout.ListY+layout.Rows.Count*layout.RowHeight+4,Math.Max(10,recoveryPanel.Width-20),30);
    }
    async Task TickDeath(){
        if(deathTicking)return;deathTicking=true;
        try{if(await login.CheckDeathTimeout())ClearInput("death-timeout-quit");SyncRecovery();}
        finally{deathTicking=false;}
    }
    void SyncRecovery(){
        if(!deathLayoutApplied&&deathLayout is {} layout){deathLayoutApplied=true;try{ApplyDeathLayout(layout);}catch(Exception ex){deathLayoutNotice="（引擎面板：UserPlayUI 版面套用失敗）";DeathLayoutError=ex.GetType().Name+": "+ex.Message;ApplyDeathFallbackLayout();}}
        var view=login.DeathSnapshot();
        recoveryPanel.Visible=login.State==LoginState.InWorld&&view.WindowOpen;
        deathReopenButton.Visible=login.State==LoginState.InWorld&&view.Dismissed&&!view.QuitSent&&login.World.OwnDead;
        if(deathReopenButton.Visible){deathReopenButton.Location=new(Math.Max(0,(ClientSize.Width-deathReopenButton.Width)/2),160);deathReopenButton.BringToFront();}
        if(!recoveryPanel.Visible)return;
        // Screen position of the window is not in the XML (X=0,Y=0 is window-relative); centred like the previous panel.
        recoveryPanel.Location=new(Math.Max(0,(ClientSize.Width-recoveryPanel.Width)/2),160);
        recoveryNotice.Text=(login.World.RestartRequested?"已送出重新開始，等待伺服器恢復。":"角色已死亡。")+(deathLayoutNotice.Length>0?"\n"+deathLayoutNotice:"");
        restartButton.Enabled=login.CanRestart;recoveryPanel.BringToFront();
    }
}
