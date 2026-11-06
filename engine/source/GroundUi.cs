namespace P06;
public sealed partial class ClientForm {
    readonly Panel groundPanel=new(){Location=new(12,438),Size=new(246,180),BackColor=Color.FromArgb(23,38,41),Visible=false};
    readonly ListBox groundList=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(27,44,51),ForeColor=Color.White,BorderStyle=BorderStyle.None,DisplayMember="Label"};
    readonly Button pickupButton=new(){Dock=DockStyle.Bottom,Height=30,Text="單次拾取 (P)",ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80)};
    readonly Label groundNoticeLabel=new(){Dock=DockStyle.Bottom,Height=46,ForeColor=Color.White};
    readonly TextBox pickupCount=new(){Dock=DockStyle.Bottom,Text="1",AccessibleName="拾取數量（1 到地面持有量）"};
    readonly Button dropButton=new(){Dock=DockStyle.Bottom,Height=30,Text="手動丟棄選取數量",ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80)};
    readonly TextBox dropCount=new(){Dock=DockStyle.Bottom,Text="1",AccessibleName="丟棄數量（1 到背包持有量）"};
    GroundQuantitySelection? pickupSelection,dropSelection;
    int groundUiRevision=-1;bool syncingGround;
    sealed record GroundRow(GroundItem Item,string Label);
    string GroundName(GroundItem i)=>string.IsNullOrEmpty(i.Name)?$"地面物品（gfx {i.Gfx}，數量 {i.Count}）":(objectAssets??=new(World.Assets.Root)).Name(i.Name);
    void InitGround(){
        inventoryPanel.Height=282;inventoryPanel.Controls.Add(dropButton);inventoryPanel.Controls.Add(dropCount);
        groundPanel.Controls.AddRange([groundList,groundNoticeLabel,pickupCount,pickupButton,new Label{Text="地面物品",Dock=DockStyle.Top,Height=25,ForeColor=Color.White}]);Controls.Add(groundPanel);groundPanel.BringToFront();
        foreach(Control control in new Control[]{groundList,pickupButton,pickupCount,dropButton,dropCount})control.Enter+=(_,_)=>LeaveGameFocus("ground-controls");
        groundList.SelectedIndexChanged+=(_,_)=>{if(syncingGround)return;ClearInput("ground-select");login.World.SelectGround(groundList.SelectedItem is GroundRow r?r.Item.ObjectId:null);SyncQuantitySelection(true);PublishWebState();};
        groundList.MouseClick+=(_,_)=>{if(groundList.SelectedItem is GroundRow r){ClearInput("ground-reselect");login.World.SelectGround(r.Item.ObjectId);SyncQuantitySelection(true);PublishWebState();}};
        inventoryList.MouseClick+=(_,_)=>{if(inventoryList.SelectedItem is InventoryItem i){ClearInput("drop-reselect");login.World.SelectItem(i.ObjectId);SyncQuantitySelection(false);PublishWebState();}};
        pickupButton.Click+=async(_,_)=>{ClearInput("ground-pickup");await NativeGroundQuantity(true);SyncGround();PublishWebState();};
        dropButton.Click+=async(_,_)=>{ClearInput("ground-drop");await NativeGroundQuantity(false);SyncInventory();SyncGround();PublishWebState();};
    }
    void SyncGround(){
        groundPanel.Visible=login.State==LoginState.InWorld;
        if(groundUiRevision!=login.World.GroundRevision){groundUiRevision=login.World.GroundRevision;syncingGround=true;groundList.BeginUpdate();groundList.Items.Clear();foreach(var g in login.World.Ground){groundList.Items.Add(new GroundRow(g,GroundName(g)));if(g.ObjectId==login.World.SelectedGroundId)groundList.SelectedIndex=groundList.Items.Count-1;}groundList.EndUpdate();syncingGround=false;}
        SyncQuantitySelection(true);SyncQuantitySelection(false);
        pickupButton.Enabled=login.GroundAvailable&&!login.MovementBusy&&login.World.SelectedGround!=null&&!World.Walking;
        dropButton.Enabled=login.GroundAvailable&&!login.MovementBusy&&login.World.SelectedItem is {} i&&login.World.CanDropGroundItem(i)&&!World.Walking;
        groundNoticeLabel.Text=login.World.GroundStatus;
    }
    void SyncQuantitySelection(bool pickup){
        var current=login.World.GroundQuantity(pickup);var old=pickup?pickupSelection:dropSelection;
        if(current?.Epoch!=old?.Epoch||current?.Id!=old?.Id||current?.Selection!=old?.Selection){if(pickup)pickupSelection=current;else dropSelection=current;}
    }
    Task<bool> NativeGroundQuantity(bool pickup)=>World.Walking?Task.FromResult(false):login.SendGroundQuantity(pickup,pickup?pickupCount.Text:dropCount.Text,pickup?pickupSelection:dropSelection);
    object GroundWeb()=>new{epoch=login.World.WorldEpoch,selectionRevision=login.World.GroundSelectionRevision,revision=login.World.GroundRevision,selectedId=login.World.SelectedGroundId,notice=login.World.GroundStatus,busy=login.MovementBusy,items=login.World.Ground.Select(i=>new{id=i.ObjectId,name=GroundName(i),count=i.Count,x=i.X,y=i.Y,selected=i.ObjectId==login.World.SelectedGroundId,image=GroundCatalog.Find(i.Gfx)!=null?$"https://p08.example/ground/{i.Gfx}.png":null}).ToArray()};
    async Task WebGround(int id,bool pickup,string count,GroundQuantitySelection selection){bool sent=!World.Walking&&await login.SendGroundQuantity(pickup,count,selection);Reply(id,sent?"已送出一次，等待原服回報":login.World.GroundNotice);PublishWebState();}
}
