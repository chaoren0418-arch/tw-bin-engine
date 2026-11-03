namespace P06;
public sealed partial class ClientForm {
    readonly Panel groundPanel=new(){Location=new(12,438),Size=new(246,180),BackColor=Color.FromArgb(23,38,41),Visible=false};
    readonly ListBox groundList=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(27,44,51),ForeColor=Color.White,BorderStyle=BorderStyle.None,DisplayMember="Label"};
    readonly Button pickupButton=new(){Dock=DockStyle.Bottom,Height=30,Text="單次拾取 (P)",ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80)};
    readonly Label groundNoticeLabel=new(){Dock=DockStyle.Bottom,Height=46,ForeColor=Color.White};
    readonly NumericUpDown pickupCount=new(){Dock=DockStyle.Bottom,Minimum=1,Maximum=1,Value=1};
    readonly Button dropButton=new(){Dock=DockStyle.Bottom,Height=30,Text="手動丟棄選取數量",ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80)};
    readonly NumericUpDown dropCount=new(){Dock=DockStyle.Bottom,Minimum=1,Maximum=1,Value=1};
    int groundUiRevision=-1;bool syncingGround;
    sealed record GroundRow(GroundItem Item,string Label);
    string GroundName(GroundItem i)=>(objectAssets??=new(World.Assets.Root)).Name(i.Name);
    void InitGround(){
        inventoryPanel.Height=282;inventoryPanel.Controls.Add(dropButton);inventoryPanel.Controls.Add(dropCount);
        groundPanel.Controls.AddRange([groundList,groundNoticeLabel,pickupCount,pickupButton,new Label{Text="地面物品",Dock=DockStyle.Top,Height=25,ForeColor=Color.White}]);Controls.Add(groundPanel);groundPanel.BringToFront();
        foreach(Control control in new Control[]{groundList,pickupButton,pickupCount,dropButton,dropCount})control.Enter+=(_,_)=>LeaveGameFocus("ground-controls");
        groundList.SelectedIndexChanged+=(_,_)=>{if(syncingGround)return;ClearInput("ground-select");login.World.SelectGround(groundList.SelectedItem is GroundRow r?r.Item.ObjectId:null);PublishWebState();};
        pickupButton.Click+=async(_,_)=>{ClearInput("ground-pickup");await login.PickupSelected((uint)pickupCount.Value);SyncGround();PublishWebState();};
        dropButton.Click+=async(_,_)=>{ClearInput("ground-drop");await login.DropSelected((uint)dropCount.Value);SyncGround();PublishWebState();};
    }
    void SyncGround(){
        groundPanel.Visible=login.State==LoginState.InWorld;
        if(groundUiRevision!=login.World.GroundRevision){groundUiRevision=login.World.GroundRevision;syncingGround=true;groundList.BeginUpdate();groundList.Items.Clear();foreach(var g in login.World.Ground){groundList.Items.Add(new GroundRow(g,GroundName(g)));if(g.ObjectId==login.World.SelectedGroundId)groundList.SelectedIndex=groundList.Items.Count-1;}groundList.EndUpdate();syncingGround=false;}
        pickupCount.Maximum=Math.Max(1,login.World.SelectedGround?.Count??1);dropCount.Maximum=Math.Max(1,login.World.SelectedItem?.Count??1);
        pickupButton.Enabled=login.GroundAvailable&&!login.MovementBusy&&login.World.SelectedGround!=null&&!World.Walking;
        dropButton.Enabled=login.GroundAvailable&&!login.MovementBusy&&login.World.SelectedItem is {} i&&GroundRules.Droppable(i)&&!World.Walking;
        groundNoticeLabel.Text=login.World.GroundStatus;
    }
    object GroundWeb()=>new{epoch=login.World.WorldEpoch,selectedId=login.World.SelectedGroundId,notice=login.World.GroundStatus,busy=login.MovementBusy,items=login.World.Ground.Select(i=>new{id=i.ObjectId,name=GroundName(i),count=i.Count,x=i.X,y=i.Y,selected=i.ObjectId==login.World.SelectedGroundId,image=i.Gfx is 36 or 66?$"https://p08.example/ground/{i.Gfx}.png":null}).ToArray()};
    async Task WebGround(int id,bool pickup,uint count){bool sent=!World.Walking&&await(pickup?login.PickupSelected(count):login.DropSelected(count));Reply(id,sent?"已送出一次，等待原服回報":login.World.GroundNotice);PublishWebState();}
}
