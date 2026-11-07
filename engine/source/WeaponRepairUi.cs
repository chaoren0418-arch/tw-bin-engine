namespace P06;

public sealed partial class ClientForm {
    Form? weaponRepairWindow;
    void InitWeaponRepairService() {
        var entry=new Button{Text="修理",Location=new(714,150),Size=new(80,34),Visible=false};
        Controls.Add(entry);entry.BringToFront();
        entry.Enter+=(_,_)=>LeaveGameFocus("weapon-repair");
        entry.Click+=(_,_)=>OpenWeaponRepair();
        var timer=new System.Windows.Forms.Timer{Interval=200};
        timer.Tick+=(_,_)=>{entry.Visible=login.State==LoginState.InWorld;};timer.Start();
        Disposed+=(_,_)=>{timer.Dispose();weaponRepairWindow?.Dispose();};
    }
    void OpenWeaponRepair() {
        if(login.State!=LoginState.InWorld)return;
        if(weaponRepairWindow is {IsDisposed:false}) {weaponRepairWindow.Activate();return;}
        LeaveGameFocus("weapon-repair");ClearInput("weapon-repair");
        var window=new Form{Text="武器修理與 PK 查詢",ClientSize=new(580,420),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false};
        weaponRepairWindow=window;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=40};
        var query=new Button{Text="查詢／更新清單",AutoSize=true};
        var submit=new Button{Text="修理選取武器",AutoSize=true};
        var pk=new Button{Text="查詢 PK 次數",AutoSize=true};
        bar.Controls.AddRange([query,submit,pk]);
        var rows=new ListBox{Dock=DockStyle.Fill};
        var notice=new Label{Dock=DockStyle.Bottom,Height=110,Padding=new(8)};
        window.Controls.AddRange([rows,notice,bar]);
        int shown=-1;WeaponRepairRow[] displayed=[];
        void RefreshRepair() {
            if(window.IsDisposed)return;
            if(login.State!=LoginState.InWorld){window.Close();return;}
            var w=login.World;int revision=w.WeaponRepairRevision;
            if(shown!=revision){shown=revision;displayed=w.WeaponRepairRows;rows.Items.Clear();foreach(var row in displayed){var item=w.Inventory.FirstOrDefault(i=>i.ObjectId==row.ObjectId);rows.Items.Add($"{ResolveNpcText(item?.Name??("未持有物件 "+row.ObjectId))}　損壞 {row.Damage}　預計 {row.Cost:N0} 金幣");}}
            query.Enabled=login.CanQueryWeaponRepair&&!World.Walking&&w.TargetId is uint id&&HasVisibleObjectPixel(id);
            submit.Enabled=rows.SelectedIndex>=0 && w.CanRepairWeapon(displayed[rows.SelectedIndex].ObjectId,shown) && !World.Walking;
            notice.Text=w.WeaponRepairNotice+"\n"+login.CheckPkNotice;
        }
        query.Click+=async(_,_)=>{if(query.Enabled)await login.SendWeaponRepair(true);RefreshRepair();};
        submit.Click+=async(_,_)=>{int index=rows.SelectedIndex;if(index>=0&&!World.Walking)await login.SendWeaponRepair(false,displayed[index].ObjectId,shown);RefreshRepair();};
        pk.Click+=async(_,_)=>{await login.CheckPk();RefreshRepair();};
        rows.SelectedIndexChanged+=(_,_)=>RefreshRepair();
        var timer=new System.Windows.Forms.Timer{Interval=150};timer.Tick+=(_,_)=>RefreshRepair();
        window.FormClosed+=(_,_)=>{timer.Dispose();login.World.CloseWeaponRepair();weaponRepairWindow=null;};
        RefreshRepair();window.Show(this);timer.Start();
    }
}
