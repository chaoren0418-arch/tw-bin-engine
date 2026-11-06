namespace P06;
public sealed partial class ClientForm {
    PlayerHudView? playerHud;
    Control? playerLoginStrip,playerToolsStrip;
    bool playerLegacyOpen,playerHudSending,playerHudWasActive;
    string playerHudNotice="";
    long playerHudNoticeUntil;PlayerHudKind playerHudLastAction;
    readonly HashSet<Keys> playerHudHeld=[];
    bool PlayerHudActive=>normalProduct&&login.State==LoginState.InWorld;
    // Public contract: client coordinates, no hard-coded margins in the pointer module.
    public Rectangle PlayerSceneBounds=>PlayerHudActive
        ?PlayerHudGeometry.Scene(ClientSize,webHost.Visible?webHost.Width:0,true,playerHud?.DrawerOpen==true,NormalSceneLeft)
        :new(NormalSceneLeft,140,Math.Max(1,ClientSize.Width-(webHost.Visible?webHost.Width:0)-NormalSceneLeft),Math.Max(1,ClientSize.Height-bar.Height-140));
    public bool PlayerSceneAccepts(Point clientPoint)=>!PlayerChatFocused&&PlayerSceneBounds.Contains(clientPoint)
        &&!(playerHud?.Visible==true&&playerHud.Bounds.Contains(clientPoint))
        &&!(normalPanels?.Visible==true&&normalPanels.Bounds.Contains(clientPoint))
        &&!(webHost.Visible&&webHost.Bounds.Contains(clientPoint))
        &&!(playerLoginStrip?.Visible==true&&playerLoginStrip.Bounds.Contains(clientPoint))
        &&!(playerToolsStrip?.Visible==true&&playerToolsStrip.Bounds.Contains(clientPoint));
    void SyncPlayerViewport(){var viewport=PlayerSceneBounds;Camera.ViewOffset=viewport.Location;Camera.View=viewport.Size;Invalidate();}
    void InitPlayerHud(){
        if(!normalProduct)return;
        playerHud=new PlayerHudView{Visible=false};Controls.Add(playerHud);InitPlayerHudChat();InitPlayerPointerHud();
        // Enter cleared movement before the click; don't cancel the newly queued action on return.
        playerHud.GameFocusRequested+=()=>{ActiveControl=null;Focus();gameFocus=true;};
        playerHud.LayoutChanged+=()=>{ClearInput("hud-layout");playerLegacyOpen=false;SyncNormalPanels();SyncPlayerHud();};
        playerHud.MoreRequested+=()=>{ClearInput("hud-more");playerHud.CloseDrawer();playerLegacyOpen=!playerLegacyOpen;SyncNormalPanels();SyncPlayerHud();};
        playerHud.DiagnosticsRequested+=async()=>{ClearInput("hud-diagnostics");await TogglePanel();SyncPlayerHud();};
        playerHud.LogoutRequested+=()=>{ClearInput("hud-logout");CancelLogin(login.SessionGeneration,false);SyncPlayerHud();};
        playerHud.SelectEntry+=(stamp,binding)=>{
            if(!PlayerHudActions.Current(login,stamp))return;ClearInput("hud-select");
            if(binding.Kind==PlayerHudKind.Item)login.World.SelectItem(binding.Id);else login.World.SelectMagic((int)binding.Id);
            playerHudNotice="";SyncPlayerHud();PublishWebState();
        };
        playerHud.TargetSelfChanged+=self=>{ClearInput("hud-target");login.World.UseSelectedMagicTarget(self);SyncPlayerHud();PublishWebState();};
        playerHud.ActivateEntry+=async(stamp,binding)=>await ActivatePlayerHud(stamp,binding);
        playerHud.Enter+=(_,_)=>LeaveGameFocus("player-hud");
        Deactivate+=(_,_)=>playerHudHeld.Clear();
    }
    async Task ActivatePlayerHud(PlayerHudStamp stamp,PlayerHudBinding binding){
        if(playerHudSending||!PlayerHudActions.Current(login,stamp))return;
        playerHudSending=true;playerHudLastAction=binding.Kind;bool walking=World.Walking;ClearInput("hud-action");
        try{
            bool sent=await PlayerHudActions.Activate(login,stamp,binding,walking);
            // A delayed sender must not overwrite the notice belonging to a newer character/world.
            if(PlayerHudActions.Current(login,stamp)){playerHudNotice=sent?"":"未送出：請確認道具、已學技能、目標及動作狀態。";playerHudNoticeUntil=Environment.TickCount64+4000;}
        }finally{playerHudSending=false;if(!IsDisposed){SyncPlayerHud();PublishWebState();}}
    }
    bool TryPlayerHudKey(Keys key){
        if(!PlayerHudActive||key<Keys.F1||key>Keys.F4)return false;
        if(playerHudHeld.Add(key))playerHud?.ActivateSlot(key-Keys.F1);
        return true;
    }
    void SyncPlayerHud(){
        if(playerHud==null)return;
        bool active=PlayerHudActive;bool entered=active&&!playerHudWasActive;playerHudWasActive=active;
        playerHud.Visible=active;bar.Visible=!active;
        if(playerLoginStrip!=null)playerLoginStrip.Visible=!active;
        if(playerToolsStrip!=null)playerToolsStrip.Visible=!active;
        if(!active){playerLegacyOpen=false;playerHudHeld.Clear();playerHudNotice="";return;}
        if(entered&&ActiveForm==this&&!webHost.Visible)ReturnGameFocus();
        SyncPlayerServices();SyncPlayerPointerHud();var w=login.World;var status=w.Status;
        bool busy=playerHudSending||login.MovementBusy||World.Walking||!w.Ready||w.ActionLocked||w.TradeBlocksActions||w.OwnDead||status?.Hp is not >0;
        var items=w.Inventory.Select(item=>new PlayerHudEntry(new(PlayerHudKind.Item,item.ObjectId),ItemName(item),$"×{item.Count} · "+(item.SupportedEquipment?"裝備 · ":"")+(w.ItemEquipped(item)==true?"已裝備 · ":"")+w.ItemUseState(item),item.IconId,w.ItemEquipped(item)==true,item.SupportedUse)).ToArray();
        var skills=w.LearnedMagic.Select(skill=>new PlayerHudEntry(new(PlayerHudKind.Magic,(uint)skill.Id),skill.Name,skill.Supported?$"MP {skill.NominalMp} · {skill.Target}":"已學會 · 尚未支援施放",null,false,skill.Supported)).ToArray();
        string state=$"{w.AcNotice} · {(w.OwnDead?"死亡":busy?"動作冷卻／忙碌":"可操作")} · {w.SustenanceText}";
        string notice=playerHudNotice.Length>0&&Environment.TickCount64<playerHudNoticeUntil?playerHudNotice:(playerHudLastAction==PlayerHudKind.Magic?w.MagicNotice:w.ItemNotice)+(w.LastServerMessage is {} message?" · "+ResolveNpcText(message):"");
        var snapshot=new PlayerHudSnapshot(PlayerHudActions.Stamp(login),w.Actor?.Name??"等待角色資料",$"Lv.{status?.Level} · {w.MagicBuffNotice} · {w.AuthoritativePositionText}",status?.Hp??0,status?.MaxHp??0,status?.Mp??0,status?.MaxMp??0,state,notice,busy,w.MagicTargetName,w.MagicTargetSelf,items,skills);
        playerHud.UpdateState(snapshot,ItemIcon);playerHud.UpdateChat(login.PlayerChatSnapshot());
        int height=PlayerHudGeometry.BaseHeight+(playerHud.DrawerOpen?PlayerHudGeometry.DrawerHeight:0);
        playerHud.SetBounds(0,Math.Max(0,ClientSize.Height-height),Math.Max(1,ClientSize.Width-(webHost.Visible?webHost.Width:0)),height);playerHud.BringToFront();
        if(normalPanels?.Visible==true)normalPanels.Bounds=new(12,8,Math.Min(470,Math.Max(260,playerHud.Width-24)),Math.Max(100,playerHud.Top-16));
        SyncPlayerViewport();
    }
}
