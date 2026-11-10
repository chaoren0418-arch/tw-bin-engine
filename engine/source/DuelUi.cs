namespace P06;

public sealed partial class ClientForm {
    readonly Label duelNotice=new(){Dock=DockStyle.Fill,ForeColor=Color.White,Padding=new(10)};
    readonly Button duelInvite=new(){Text="邀請面前角色決鬥",AutoSize=true};
    // R2-J (#624): no accept/reject buttons; S219/630 is answered from the shared input-line prompt (YesNoPrompt.cs).
    DuelView? displayedDuel;
    bool duelUiReady;

    // Form lifecycle keeps this module self-contained; no constructor or HUD dispatch edits.
    // OnLoad and OnShown are owned by BuddyUi and LocationShare; VisibleChanged can repeat, so guard it.
    protected override void OnVisibleChanged(EventArgs e){
        base.OnVisibleChanged(e);
        if(!Visible||duelUiReady)return;
        duelUiReady=true;
        var page=new TabPage("決鬥"){BackColor=Color.FromArgb(23,38,41)};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=80,AutoScroll=true};
        buttons.Controls.Add(duelInvite);page.Controls.AddRange([duelNotice,buttons]);
        var proposal=MarriageEntryPage(); // Marriage.cs owns the proposal entry; mounted here because this hook is free.
        if(normalPanels!=null)normalPanels.TabPages.AddRange([page,proposal]);
        else {var tabs=new TabControl{Dock=DockStyle.Right,Width=300};tabs.TabPages.AddRange([page,proposal]);Controls.Add(tabs);}
        page.Enter+=(_,_)=>LeaveGameFocus("duel");
        duelInvite.Click+=async(_,_)=>{if(displayedDuel is {} view)await RunDuel("invite",view);};
        timer.Tick+=(_,_)=>{SyncDuel();SyncMarriageEntry();};SyncDuel();SyncMarriageEntry();
    }
    async Task<bool> RunDuel(string command,DuelView view){
        ClearInput("duel-command");
        if(World.Walking||command=="invite"&&(login.World.FacingTradeTarget is not {} target||!HasVisibleObjectPixel(target.ObjectId)))return false;
        bool sent=await login.Duel(command,view.Revision,view.Epoch);SyncDuel();Invalidate();return sent;
    }
    void SyncDuel(){
        var view=login.World.DuelSnapshot;
        displayedDuel=view;
        duelNotice.Text=(view.Peer.Length==0?"":"對手："+view.Peer+"\n")+view.Notice+"\n\n邀請：與相鄰角色面對面後，在聊天列輸入 /duel（或 /決鬥），或按下方按鈕（開發工具入口）。攻擊仍由伺服器判定。";
        bool ready=login.State==LoginState.InWorld&&!World.Walking;
        duelInvite.Enabled=ready&&login.CanInviteDuel&&login.World.FacingTradeTarget is {} target&&HasVisibleObjectPixel(target.ObjectId);
    }
    // The original never labels the duel opponent (0x15442DC is read only by sub_A3FE30 and sub_F044C0):
    // it changes the attack cursor and direct-attack admission, not the name. Same for pink names and enemy clans.
    bool attackCursorShown;
    internal static Cursor AttackCursor=>Cursors.Cross;
    void UpdateAttackCursor(uint? hovered){
        bool attack=hovered is uint id&&login.World.CanAttackDirectly(id);
        if(attack==attackCursorShown)return;attackCursorShown=attack;Cursor=attack?AttackCursor:Cursors.Default;
    }
    internal bool DirectAttackTarget(uint id)=>login.World.CanAttackDirectly(id);
}
