namespace P06;

// M7-G（T208／#514）以物品學魔法的暫行視窗（自有 Form，不改共用的 WebView 指令分派或 Program.cs）。
// op 175 原生視窗的版面待查證，這裡只是 WinForms 清單＋勾選＋送出一次，不冒充原版畫面。
// 開窗時機：RequestSpellExchangeList() 成功送出 C245 後由 LoginConnection 開啟；C245 的 NPC 對話入口待查證，尚未接上。
public sealed class SpellExchangeUi:Form {
    readonly LoginConnection connection;
    readonly CheckedListBox rows=new(){Dock=DockStyle.Fill,IntegralHeight=false,CheckOnClick=true};
    readonly Label notice=new(){Dock=DockStyle.Fill,AutoSize=false};
    readonly Button refresh=new(){Text="重新要求清單",AutoSize=true},learn=new(){Text="學習勾選的魔法",AutoSize=true},close=new(){Text="關閉",AutoSize=true};
    readonly System.Windows.Forms.Timer timer=new(){Interval=150};
    SpellExchangeView displayed;bool busy;string validationNotice="";readonly int openedGeneration,openedEpoch;

    public SpellExchangeUi(LoginConnection connection){
        this.connection=connection;displayed=connection.SpellExchangeSnapshot();openedGeneration=displayed.Generation;openedEpoch=displayed.WorldEpoch;
        Text="以物品學魔法（暫行視窗；原版版面待查證）";Size=new(460,520);MinimumSize=new(380,400);StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new(10)};
        layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,36));layout.RowStyles.Add(new(SizeType.Absolute,96));
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};actions.Controls.AddRange([learn,refresh,close]);
        layout.Controls.Add(rows,0,0);layout.Controls.Add(actions,0,1);layout.Controls.Add(notice,0,2);Controls.Add(layout);
        rows.ItemCheck+=(_,e)=>{if(e.Index>=0&&e.Index<rows.Items.Count&&rows.Items[e.Index] is RowItem {Row.Learned:true})e.NewValue=CheckState.Unchecked;BeginInvoke(SyncButtons);};
        learn.Click+=async(_,_)=>await Learn();
        refresh.Click+=async(_,_)=>await RequestAgain();
        close.Click+=(_,_)=>Close();
        timer.Tick+=(_,_)=>Sync();Shown+=(_,_)=>{Sync();timer.Start();};
        FormClosed+=(_,_)=>{timer.Dispose();connection.CloseSpellExchange(connection.SpellExchangeSnapshot());};Sync();
    }
    int[] Chosen()=>rows.CheckedItems.OfType<RowItem>().Where(r=>!r.Row.Learned).Select(r=>r.Row.Index).ToArray();
    async Task Learn(){
        if(busy)return;var stamp=displayed;int[] chosen=Chosen();if(chosen.Length==0)return;
        string names=string.Join("、",stamp.Rows.Where(r=>chosen.Contains(r.Index)).Select(r=>r.Name));
        if(MessageBox.Show(this,$"送出學習請求：{names}？\n需要的材料由原服檢查與扣除；一份清單只能送出一次。","確認學習",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
        validationNotice="";busy=true;Sync();
        bool sent=await connection.LearnSpellExchange(stamp,chosen);busy=false;if(IsDisposed)return;
        if(!sent)validationNotice="未送出：清單已失效、角色狀態不允許，或選擇不合法。";Sync();
    }
    async Task RequestAgain(){
        if(busy)return;validationNotice="";busy=true;Sync();
        bool sent=await connection.RequestSpellExchangeList(openWindow:false);busy=false;if(IsDisposed)return;
        if(!sent)validationNotice="未送出：需要與 NPC 的對話窗仍開著且在 5 格內。";Sync();
    }
    void Sync(){
        var v=connection.SpellExchangeSnapshot();
        if(v.Generation!=openedGeneration||v.WorldEpoch!=openedEpoch){Close();return;}
        if(v.Revision!=displayed.Revision||rows.Items.Count!=v.Rows.Length||v.Rows.Where((r,i)=>rows.Items[i] is RowItem old&&old.Row!=r).Any()){
            if(v.Revision!=displayed.Revision)validationNotice="";
            var keep=Chosen().ToHashSet();rows.BeginUpdate();rows.Items.Clear();
            foreach(var row in v.Rows)rows.Items.Add(new RowItem(row),!row.Learned&&keep.Contains(row.Index));
            rows.EndUpdate();
        }
        displayed=v;
        notice.Text=(validationNotice.Length>0?validationNotice:v.Notice)+(connection.World.LastServerMessage is {} message?"\n伺服器："+message:"");
        SyncButtons();
    }
    void SyncButtons(){
        if(IsDisposed)return;int n=Chosen().Length;
        learn.Enabled=!busy&&displayed.CanSend&&n>0;learn.Text=n>0?$"學習勾選的魔法（{n} 項）":"學習勾選的魔法";
        refresh.Enabled=!busy&&displayed.CanRequest;
    }
    sealed record RowItem(SpellExchangeRow Row){public override string ToString()=>$"{Row.Name}（技能 {Row.SkillId}）{(Row.Learned?"　已學會":"")}";}
}

public sealed partial class LoginConnection {
    void ShowSpellExchangeWindow(){
        // 離線測試沒有 ClientForm，不會建立視窗。
        var owner=Application.OpenForms.OfType<ClientForm>().FirstOrDefault(form=>form.OwnsSpellExchangeConnection(this));
        if(owner==null)return;
        if(owner.InvokeRequired)owner.BeginInvoke(()=>owner.ShowSpellExchangeUi(this));else owner.ShowSpellExchangeUi(this);
    }
}
public sealed partial class ClientForm {
    internal bool OwnsSpellExchangeConnection(LoginConnection connection)=>ReferenceEquals(login,connection);
    internal void ShowSpellExchangeUi(LoginConnection connection){
        if(OwnedForms.OfType<SpellExchangeUi>().FirstOrDefault() is {IsDisposed:false} open){open.Activate();return;}
        ClearInput("spell-exchange");LeaveGameFocus("spell-exchange");
        new SpellExchangeUi(connection).Show(this);
    }
}
