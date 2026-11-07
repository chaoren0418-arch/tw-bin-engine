namespace P06;

public sealed class CharResetUi:Form {
    static readonly string[] Labels=["力量","智力","精神","敏捷","體質","魅力"];
    readonly LoginConnection connection;
    readonly int generation;int epoch;
    readonly Label heading=new(){Dock=DockStyle.Top,Height=76};
    readonly Label notice=new(){Dock=DockStyle.Bottom,Height=88};
    readonly NumericUpDown[] values=new NumericUpDown[6];
    readonly ComboBox attribute=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
    readonly Button confirm=new(){Text="確認配點",AutoSize=true},one=new(){Text="提升 1 級",AutoSize=true},ten=new(){Text="提升 10 級",AutoSize=true};
    readonly System.Windows.Forms.Timer timer=new(){Interval=150};
    CharResetView displayed;int revision=-1;bool busy,loading;
    public CharResetUi(LoginConnection connection){
        this.connection=connection;displayed=connection.CharResetSnapshot();generation=displayed.Generation;epoch=displayed.Epoch;
        Text="能力值重置";ClientSize=new(540,490);MinimumSize=new(550,530);StartPosition=FormStartPosition.CenterParent;Padding=new(12);
        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=6};
        for(int i=0;i<6;i++){grid.RowStyles.Add(new(SizeType.Percent,100f/6));grid.Controls.Add(new Label{Text=Labels[i],AutoSize=true},0,i);values[i]=new(){Minimum=0,Maximum=255,Width=160};values[i].ValueChanged+=(_,_)=>UpdateActions();grid.Controls.Add(values[i],1,i);}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=76};attribute.Items.AddRange(Labels);attribute.SelectedIndex=0;attribute.SelectedIndexChanged+=(_,_)=>UpdateActions();actions.Controls.AddRange([attribute,one,ten,confirm]);
        Controls.Add(grid);Controls.Add(actions);Controls.Add(notice);Controls.Add(heading);
        one.Click+=async(_,_)=>await Run("level",displayed.Level<50?0:attribute.SelectedIndex+1);
        ten.Click+=async(_,_)=>await Run("level",7);
        confirm.Click+=async(_,_)=>{var cmd=displayed.Phase=="level"?"finish":displayed.Phase;var stamp=displayed;var stats=Draft();if(MessageBox.Show(this,"確認送出目前配點？送出後由原服處理，不能在此復原。","確認能力重置",MessageBoxButtons.OKCancel)==DialogResult.OK)await Run(cmd,0,stamp,stats);};
        timer.Tick+=(_,_)=>Sync();Shown+=(_,_)=>{Sync();timer.Start();};FormClosed+=(_,_)=>timer.Dispose();
        FormClosing+=(_,e)=>{var v=connection.CharResetSnapshot();if(e.CloseReason==CloseReason.UserClosing&&v.Generation==generation&&v.Phase is not ("idle" or "ended")){
            if(MessageBox.Show(this,"原服沒有取消重置封包。中斷連線並於重新登入後核對？","離開重置",MessageBoxButtons.OKCancel)!=DialogResult.OK)e.Cancel=true;else connection.Cancel();
        }};Sync();
    }
    ResetStats Draft()=>ResetStats.From(values.Select(n=>(int)n.Value).ToArray());
    void Sync(){
        var v=connection.CharResetSnapshot();if(v.Generation==generation&&v.Phase=="initial")epoch=v.Epoch;
        if(v.Generation!=generation||v.Epoch!=epoch){Close();return;}
        displayed=v;
        if(revision!=v.Revision){loading=true;for(int i=0;i<6;i++)values[i].Value=v.Stats.Values[i];loading=false;revision=v.Revision;}
        UpdateActions();
    }
    void UpdateActions(){
        if(loading)return;var v=displayed;bool ready=v.CanAct&&!busy;bool edit=v.Phase is "initial" or "elixir";
        int remaining=edit?v.Points-(Draft().Sum-v.Stats.Sum):v.Points;
        heading.Text=$"階段：{v.Phase}　等級 {v.Level}/{v.MaxLevel}\nHP {v.Hp}　MP {v.Mp}　AC {v.Ac}\n剩餘{(edit?"配點":"升級能力點")}：{remaining}";
        notice.Text=v.Notice+(v.OriginalBonus is {} bonus?"\n出生加點（原服資訊）："+string.Join("／",bonus.Values):"");
        foreach(var n in values)n.Enabled=ready&&edit;
        attribute.Enabled=ready&&v.Phase=="level"&&v.Level>=50&&v.Level<v.MaxLevel;
        one.Enabled=ready&&v.Phase=="level"&&v.Level<v.MaxLevel&&(v.Level<50||v.Stats.Values[attribute.SelectedIndex]<35);
        ten.Enabled=ready&&v.Phase=="level"&&v.Level+10<=Math.Min(50,v.MaxLevel);
        confirm.Text=v.Phase=="level"?"確認完成重置":"確認配點";
        confirm.Enabled=ready&&(v.Phase=="level"?v.Level==v.MaxLevel:edit&&remaining==0&&Draft().Values.Zip(v.Stats.Values).All(p=>p.First>=p.Second&&p.First<=35));
    }
    async Task Run(string command,int step,CharResetView? stamp=null,ResetStats? stats=null){
        if(busy)return;var captured=stamp??displayed;busy=true;UpdateActions();
        bool sent=await connection.SendCharReset(captured,command,stats??Draft(),step);busy=false;
        if(IsDisposed)return;Sync();if(!sent)notice.Text="未送出：配點不合法、操作過期或連線忙碌。";
    }
    internal static void Offer(WorldSession world){
        // The production form owns this session. Offline tests have no form and open no UI.
        foreach(var owner in Application.OpenForms.OfType<ClientForm>().ToArray())owner.OfferCharReset(world);
    }
}
public sealed partial class ClientForm {
    internal void OfferCharReset(WorldSession world){
        if(!ReferenceEquals(login.World,world)||!IsHandleCreated||IsDisposed)return;
        int generation=login.SessionGeneration;
        BeginInvoke((Action)(()=>{
            if(IsDisposed||login.SessionGeneration!=generation||login.CharResetSnapshot().Phase!="initial"||OwnedForms.OfType<CharResetUi>().Any())return;
            ClearInput("char-reset");LeaveGameFocus("char-reset");
            using var dialog=new CharResetUi(login);dialog.ShowDialog(this);
        }));
    }
}
