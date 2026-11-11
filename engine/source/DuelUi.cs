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
        timer.Tick+=(_,_)=>{SyncDuel();SyncMarriageEntry();SyncSpellCursor();};SyncDuel();SyncMarriageEntry();
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
    // T406（#923，FU13§2 D1）：選目標模式中滑鼠在世界上移動時，sub_F02BA0 → sub_F044C0 換瞄準游標（8；技能依射程 8／19／20），
    // 優先於攻擊游標；選完或取消時 sub_F68E60 → sub_8EECE0(3) 還原手形。圖由 GameCursorSource 從原版 BIN 唯讀讀出。
    int? spellCursorShown;uint? cursorHovered;
    void UpdateAttackCursor(uint? hovered){
        cursorHovered=hovered;
        if(login.World.SpellTargetPicking){int index=SpellCursorIndex(hovered);if(index!=spellCursorShown){spellCursorShown=index;Cursor=GameCursorSource.Get(index);}return;}
        bool restore=spellCursorShown!=null;spellCursorShown=null;
        bool attack=hovered is uint id&&login.World.CanAttackDirectly(id);
        if(attack==attackCursorShown&&!restore)return;attackCursorShown=attack;Cursor=attack?AttackCursor:GameCursorSource.Get(GameCursors.Hand);
    }
    int SpellCursorIndex(uint? hovered){
        bool? inRange=null;
        if(login.World.SpellTargetPick is {} pick&&login.World.Actor is {} a&&hovered is uint id&&login.World.Objects.FirstOrDefault(o=>o.ObjectId==id) is {} o)inRange=CombatRules.TileDistance(a.X,a.Y,o.X,o.Y)<=pick.Range;
        return SpellCursorRules.Index(true,spellCallback:true,rangeKnown:true,targetInRange:inRange);
    }
    /// <summary>選完或取消後（滑鼠沒動）也要還原：原版在 sub_F68E60 直接換回 3，這裡由表單計時器檢查。</summary>
    void SyncSpellCursor(){if(spellCursorShown!=null&&!login.World.SpellTargetPicking)UpdateAttackCursor(cursorHovered);}
    internal bool DirectAttackTarget(uint id)=>login.World.CanAttackDirectly(id);
}
