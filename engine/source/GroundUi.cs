namespace P06;
public sealed partial class ClientForm {
    readonly Panel groundPanel=new(){Location=new(12,438),Size=new(246,180),BackColor=Color.FromArgb(23,38,41),Visible=false};
    readonly ListBox groundList=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(27,44,51),ForeColor=Color.White,BorderStyle=BorderStyle.None,DisplayMember="Label"};
    readonly Button pickupButton=new(){Dock=DockStyle.Bottom,Height=30,Text="拾取選取物品（自動走近）",ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80)};
    readonly Label groundNoticeLabel=new(){Dock=DockStyle.Bottom,Height=46,ForeColor=Color.White};
    readonly TextBox pickupCount=new(){Dock=DockStyle.Bottom,Text="",Enabled=false,AccessibleName="拾取數量提示（空白＝整疊，Enter 送出，Esc 取消）"};
    readonly CheckBox pickupAllBox=new(){Dock=DockStyle.Bottom,Height=24,Text="全部拾取（一律整疊）",ForeColor=Color.White};
    readonly Button dropButton=new(){Dock=DockStyle.Bottom,Height=30,Text="手動丟棄選取數量",ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80)};
    readonly TextBox dropCount=new(){Dock=DockStyle.Bottom,Text="1",AccessibleName="丟棄數量（1 到背包持有量）"};
    GroundQuantitySelection? pickupSelection,dropSelection;
    int groundUiRevision=-1;bool syncingGround;
    sealed record GroundRow(GroundItem Item,string Label);
    string GroundName(GroundItem i)=>string.IsNullOrEmpty(i.Name)?$"地面物品（gfx {i.Gfx}，數量 {i.Count}）":(objectAssets??=new(World.Assets.Root)).Name(i.Name);
    void InitGround(){
        inventoryPanel.Height=282;inventoryPanel.Controls.Add(dropButton);inventoryPanel.Controls.Add(dropCount);
        groundPanel.Controls.AddRange([groundList,groundNoticeLabel,pickupCount,pickupAllBox,pickupButton,new Label{Text="地面物品",Dock=DockStyle.Top,Height=25,ForeColor=Color.White}]);Controls.Add(groundPanel);groundPanel.BringToFront();
        foreach(Control control in new Control[]{groundList,pickupButton,pickupCount,pickupAllBox,dropButton,dropCount})control.Enter+=(_,_)=>LeaveGameFocus("ground-controls");
        groundList.SelectedIndexChanged+=(_,_)=>{if(syncingGround)return;ClearInput("ground-select");login.World.SelectGround(groundList.SelectedItem is GroundRow r?r.Item.ObjectId:null);SyncQuantitySelection(true);PublishWebState();};
        groundList.MouseClick+=(_,_)=>{if(groundList.SelectedItem is GroundRow r){ClearInput("ground-reselect");login.World.SelectGround(r.Item.ObjectId);SyncQuantitySelection(true);PublishWebState();}};
        inventoryList.MouseClick+=(_,_)=>{if(inventoryList.SelectedItem is InventoryItem i){ClearInput("drop-reselect");login.World.SelectItem(i.ObjectId);SyncQuantitySelection(false);PublishWebState();}};
        pickupButton.Click+=async(_,_)=>{ClearInput("ground-pickup");if(login.World.SelectedGroundId is uint gid)await PickupGroundClicked(gid);SyncGround();PublishWebState();};
        pickupAllBox.CheckedChanged+=(_,_)=>{if(syncingGround||pickupAllBox.Checked==login.World.PickupAll)return;login.World.TogglePickupAll();SyncGround();PublishWebState();};
        pickupCount.KeyDown+=async(_,e)=>{
            if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;string text=pickupCount.Text;pickupCount.Text="";await login.AnswerPickupPrompt(text);ReturnGameFocus();SyncGround();PublishWebState();}
            else if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;pickupCount.Text="";login.CancelPickupPrompt();ReturnGameFocus();SyncGround();PublishWebState();}
        };
        login.PickupPromptOpened+=_=>BeginInvokeIfReady(()=>{SyncGround();pickupCount.Text="";pickupCount.Focus();});
        dropButton.Click+=async(_,_)=>{ClearInput("ground-drop");await NativeGroundQuantity(false);SyncInventory();SyncGround();PublishWebState();};
    }
    void SyncGround(){
        groundPanel.Visible=login.State==LoginState.InWorld;
        if(groundUiRevision!=login.World.GroundRevision){groundUiRevision=login.World.GroundRevision;syncingGround=true;groundList.BeginUpdate();groundList.Items.Clear();foreach(var g in login.World.Ground){groundList.Items.Add(new GroundRow(g,GroundName(g)));if(g.ObjectId==login.World.SelectedGroundId)groundList.SelectedIndex=groundList.Items.Count-1;}groundList.EndUpdate();syncingGround=false;}
        SyncQuantitySelection(true);SyncQuantitySelection(false);
        pickupButton.Enabled=login.PickupAvailable&&login.World.SelectedGround!=null&&!login.Approach.Active;
        pickupCount.Enabled=login.World.PickupPrompt!=null;if(pickupCount.Enabled==false&&pickupCount.Text.Length>0)pickupCount.Text="";
        syncingGround=true;pickupAllBox.Checked=login.World.PickupAll;syncingGround=false;
        dropButton.Enabled=login.GroundAvailable&&!login.MovementBusy&&login.World.SelectedItem is {} i&&login.World.CanDropGroundItem(i)&&!World.Walking;
        groundNoticeLabel.Text=login.World.GroundStatus;
    }
    void SyncQuantitySelection(bool pickup){
        var current=login.World.GroundQuantity(pickup);var old=pickup?pickupSelection:dropSelection;
        if(current?.Epoch!=old?.Epoch||current?.Id!=old?.Id||current?.Selection!=old?.Selection){if(pickup)pickupSelection=current;else dropSelection=current;}
    }
    // Keyboard P = Action_Loot (WO §2): first item within 1 tile, whole stack. Drop keeps the explicit count.
    Task<bool> NativeGroundQuantity(bool pickup)=>World.Walking?Task.FromResult(false):pickup?login.LootNearby():ConfirmedDrop(dropCount.Text,dropSelection);
    // T464（L2＝X）：本機不再用模板白名單擋丟棄；每一次丟棄都先確認，能不能丟由伺服器決定。
    // Automation seam (GroundDemo): answers the same prompt instead of the modal box; the prompt is still built and logged.
    Func<string,bool>? dropConfirmAnswer;
    async Task<bool> ConfirmedDrop(string text,GroundQuantitySelection? selection){
        if(transferDialogOpen)return false;
        if(login.NeedsDropConfirm(text,selection,out var item,out uint count)){
            transferDialogOpen=true;bool yes;string prompt=GroundCatalog.DropConfirmPrompt(ItemName(item!),count);
            try{yes=dropConfirmAnswer?.Invoke(prompt)??MessageBox.Show(this,prompt,"確認丟棄物品",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;}
            finally{transferDialogOpen=false;}
            if(!yes){login.CancelDrop();return false;}
            if(World.Walking)return false;
        }
        return await login.SendGroundQuantity(false,text,selection);
    }
    // Object click on a ground item (sub_F112F0): walk to 1 tile, then whole stack or the quantity prompt.
    // Used by the scene double-click (PlayerPointerUi → PlayerPointerController.ClickGroundAsync, R2-C) and the pickup button.
    internal Task<bool> PickupGroundClicked(uint id)=>login.PickupGround(id,()=>World.Walking);
    void BeginInvokeIfReady(Action action){if(IsDisposed)return;if(IsHandleCreated&&InvokeRequired)BeginInvoke(action);else action();}
    object GroundWeb()=>new{epoch=login.World.WorldEpoch,selectionRevision=login.World.GroundSelectionRevision,revision=login.World.GroundRevision,selectedId=login.World.SelectedGroundId,notice=login.World.GroundStatus,busy=login.MovementBusy,items=login.World.Ground.Select(i=>new{id=i.ObjectId,name=GroundName(i),count=i.Count,x=i.X,y=i.Y,selected=i.ObjectId==login.World.SelectedGroundId,image=GroundCatalog.Find(i.Gfx)!=null?$"https://p08.example/ground/{i.Gfx}.png":null}).ToArray()};
    async Task WebGround(int id,bool pickup,string count,GroundQuantitySelection selection){bool sent=!pickup?!World.Walking&&await ConfirmedDrop(count,selection):login.World.PickupPrompt is {} prompt&&prompt.Id==selection.Id?await login.AnswerPickupPrompt(count):await login.PickupGround(selection.Id,()=>World.Walking);Reply(id,sent?"已送出一次，等待原服回報":login.World.GroundNotice);PublishWebState();}
}
