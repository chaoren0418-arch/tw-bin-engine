namespace P06;

public sealed partial class ClientForm {
    readonly Label duelNotice=new(){Dock=DockStyle.Fill,ForeColor=Color.White,Padding=new(10)};
    readonly Button duelInvite=new(){Text="邀請面前角色決鬥",AutoSize=true};
    readonly Button duelAccept=new(){Text="接受決鬥",AutoSize=true};
    readonly Button duelReject=new(){Text="拒絕決鬥",AutoSize=true};
    DuelView? displayedDuel;

    // Form lifecycle keeps this module self-contained; no constructor or HUD dispatch edits.
    protected override void OnLoad(EventArgs e){
        base.OnLoad(e);
        var page=new TabPage("決鬥"){BackColor=Color.FromArgb(23,38,41)};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=80,AutoScroll=true};
        buttons.Controls.AddRange([duelInvite,duelAccept,duelReject]);page.Controls.AddRange([duelNotice,buttons]);
        if(normalPanels!=null)normalPanels.TabPages.Add(page);
        else {var tabs=new TabControl{Dock=DockStyle.Right,Width=300};tabs.TabPages.Add(page);Controls.Add(tabs);}
        page.Enter+=(_,_)=>LeaveGameFocus("duel");
        foreach(var (button,command) in new[]{(duelInvite,"invite"),(duelAccept,"accept"),(duelReject,"reject")})
            button.Click+=async(_,_)=>{if(displayedDuel is {} view)await RunDuel(command,view);};
        timer.Tick+=(_,_)=>SyncDuel();SyncDuel();
    }
    async Task<bool> RunDuel(string command,DuelView view){
        ClearInput("duel-command");
        if(World.Walking||command=="invite"&&(login.World.FacingTradeTarget is not {} target||!HasVisibleObjectPixel(target.ObjectId)))return false;
        bool sent=await login.Duel(command,view.Revision,view.Epoch);SyncDuel();Invalidate();return sent;
    }
    void SyncDuel(){
        var view=login.World.DuelSnapshot;
        if(view.Phase=="invited"&&view.Revision!=displayedDuel?.Revision)ShowPlayerService("決鬥");
        displayedDuel=view;
        duelNotice.Text=(view.Peer.Length==0?"":"對手："+view.Peer+"\n")+view.Notice+"\n\n邀請：選取相鄰、與你面對面的角色。攻擊仍由伺服器判定。";
        bool ready=login.State==LoginState.InWorld&&!World.Walking;
        duelInvite.Enabled=ready&&login.CanInviteDuel&&login.World.FacingTradeTarget is {} target&&HasVisibleObjectPixel(target.ObjectId);
        duelAccept.Enabled=duelReject.Enabled=ready&&view.Phase=="invited"&&!login.World.ActionLocked&&!login.World.TradeBlocksActions;
    }
    void PaintDuelOpponent(Graphics g){
        foreach(var visual in objectVisuals.Where(v=>login.World.IsDuelOpponent(v.ObjectId))){
            var point=Camera.ToView(visual.Foot);
            g.DrawString("決鬥對手 · "+visual.Name,Font,Brushes.OrangeRed,point.X-40,point.Y-28);
        }
    }
}
