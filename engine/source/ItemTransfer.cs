using System.Buffers.Binary;
namespace P06;

public static class ItemTransferPackets {
    public static byte[] Delete(uint item) {
        if(item==0)throw new ArgumentOutOfRangeException(nameof(item));
        byte[] b=new byte[8];b[0]=138;BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1),item);return b;
    }
    public static byte[] Give(uint target,ushort x,ushort y,uint item,uint count) {
        if(target==0||item==0||count is 0 or >int.MaxValue)throw new ArgumentOutOfRangeException(nameof(count));
        byte[] b=new byte[24];b[0]=45;BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1),target);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(5),x);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(7),y);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(9),item);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(13),count);return b;
    }
}

// The confirmation/drag owns the displayed item, not whichever item is selected later.
public sealed record ItemTransferSelection(int Session,GroundQuantitySelection Quantity,InventoryItem Item,
    OwnActor Actor,int Correction,int Appearance,int Equipment);
public sealed class ItemTransferRequest {
    internal ItemTransferRequest(ItemTransferSelection selection,WorldObject? target,uint count){Selection=selection;Target=target;Count=count;}
    public ItemTransferSelection Selection {get;}
    public WorldObject? Target {get;}
    public uint Count {get;}
    public bool Delete=>Target==null;
    internal int Consumed;
}

public sealed partial class LoginConnection {
    public ItemTransferSelection? CaptureItemTransfer()=>World.GroundQuantity(false) is {} q&&World.SelectedItem is {} i&&World.Actor is {} a
        ?new(generation,q,i,a,World.Corrections,World.AppearanceRevision,World.EquipmentRevision):null;
    bool CurrentItemTransfer(ItemTransferSelection s)=>generation==s.Session&&World.CurrentGroundQuantity(false,s.Quantity)
        &&ReferenceEquals(World.SelectedItem,s.Item)&&World.Actor==s.Actor&&World.Corrections==s.Correction
        &&World.AppearanceRevision==s.Appearance&&World.EquipmentRevision==s.Equipment
        &&s.Item.Count>0&&World.ItemEquipped(s.Item)!=true&&!World.EquipmentSlots.Any(e=>e.ObjectId==s.Item.ObjectId);
    bool TransferTarget(WorldObject t)=>t.ObjectId!=World.Actor?.ObjectId&&!t.Dead&&!t.AppearancePending
        &&World.Objects.Any(o=>ReferenceEquals(o,t))&&World.Actor is {} a
        &&Math.Max(Math.Abs(a.X-t.X),Math.Abs(a.Y-t.Y))<=2;
    public ItemTransferRequest? PrepareItemTransfer(ItemTransferSelection? selection,uint? target,string quantity) {
        if(selection==null||!CurrentItemTransfer(selection)||!GroundAvailable||MovementBusy){World.RejectGroundQuantity(false,"未送出：物品已變更、已裝備或目前無法操作");return null;}
        if(target==null)return new(selection,null,selection.Item.Count);
        if(!GroundCatalog.TryCount(quantity,selection.Item.Count,out uint count)){World.RejectGroundQuantity(false,"未送出：數量不合法，請輸入 1 到持有量（最多 2147483647）的整數");return null;}
        var recipient=World.Objects.FirstOrDefault(o=>o.ObjectId==target);
        if(recipient==null||!TransferTarget(recipient)){World.RejectGroundQuantity(false,"未送出：目標不存在或超過 2 格，請自行靠近");return null;}
        return new(selection,recipient,count);
    }
    public async Task<bool> SendItemTransfer(ItemTransferRequest request,bool confirmed,Func<bool>? uiCurrent=null) {
        if(!confirmed){Interlocked.Exchange(ref request.Consumed,1);return false;}
        bool Valid()=>CurrentItemTransfer(request.Selection)&&GroundAvailable&&(request.Target==null||TransferTarget(request.Target))&&(uiCurrent?.Invoke()??true);
        if(Interlocked.Exchange(ref request.Consumed,1)!=0||!Valid()||MovementBusy||sendMovementGate==null)return false;
        var sender=sendMovementGate;int lease=++itemLease,input=inputVersion,speed=World.SpeedRevision;
        byte[] bytes=request.Target is {} target?ItemTransferPackets.Give(target.ObjectId,target.X,target.Y,request.Selection.Item.ObjectId,request.Count):ItemTransferPackets.Delete(request.Selection.Item.ObjectId);
        itemSending=true;
        bool Owner()=>generation==request.Selection.Session&&World.WorldEpoch==request.Selection.Quantity.Epoch&&lease==itemLease;
        bool Ready()=>!combatSending&&!movementSending&&!magicSending&&!groundSending&&!CombatBusy&&!MagicBusy&&!GroundBusy
            &&MovementClock()>=MovementReadyAt&&Environment.TickCount64>=nextItemAt;
        try {
            bool sent=await sender(bytes,()=>Owner()&&Valid()&&input==inputVersion&&speed==World.SpeedRevision&&Ready(),()=>{
                nextItemAt=Environment.TickCount64+GroundClickGuardMs;
                World.RejectGroundQuantity(false,(request.Delete?"刪除":"給予")+"請求已送出；等待伺服器背包／訊息回報");
            });
            return Owner()&&sent;
        }catch(Exception ex){if(Owner()){Cancel();Set(LoginState.Faulted,"物品轉移失敗："+ex.Message);}return false;}
        finally{if(Owner())itemSending=false;}
    }
}

public sealed partial class ClientForm {
    readonly Button deleteItemButton=new(){Dock=DockStyle.Bottom,Height=30,Text="垃圾桶：刪除整疊（可拖入）",ForeColor=Color.White,BackColor=Color.FromArgb(92,48,45),AllowDrop=true};
    Point transferDragStart;ItemTransferSelection? transferDragItem;bool transferDialogOpen;
    sealed record InventoryDrag(ClientForm Owner,ItemTransferSelection Selection);
    void InitItemTransfer() {
        inventoryPanel.Controls.Add(deleteItemButton);
        deleteItemButton.Enter+=(_,_)=>LeaveGameFocus("inventory-delete");
        deleteItemButton.Click+=async(_,_)=>{ClearInput("inventory-delete");await DispatchItemTransfer(login.CaptureItemTransfer(),null);};
        // Reuse the existing inventory quantity entry and its GroundCatalog.TryCount contract.
        dropCount.AccessibleName="丟棄／給予數量（1 到背包持有量）；給予前先輸入，再拖曳物品到目標";
        inventoryList.MouseDown+=(_,e)=>{
            transferDragItem=null;if(e.Button!=MouseButtons.Left)return;
            int index=inventoryList.IndexFromPoint(e.Location);if(index<0||!inventoryList.GetItemRectangle(index).Contains(e.Location))return;
            inventoryList.SelectedIndex=index;ClearInput("inventory-drag");transferDragStart=e.Location;transferDragItem=login.CaptureItemTransfer();
        };
        inventoryList.MouseUp+=(_,_)=>transferDragItem=null;
        inventoryList.MouseMove+=(_,e)=>{
            if(e.Button!=MouseButtons.Left||transferDragItem is not {} selection)return;
            var size=SystemInformation.DragSize;
            if(new Rectangle(transferDragStart.X-size.Width/2,transferDragStart.Y-size.Height/2,size.Width,size.Height).Contains(e.Location))return;
            transferDragItem=null;inventoryList.DoDragDrop(new InventoryDrag(this,selection),DragDropEffects.Move);
        };
        AllowDrop=true;
        foreach(Control receiver in new Control[]{this,deleteItemButton}) {
            receiver.DragEnter+=(_,e)=>ItemTransferDragEffect(receiver,e);
            receiver.DragOver+=(_,e)=>ItemTransferDragEffect(receiver,e);
            receiver.DragDrop+=async(_,e)=>{
                if(e.Data?.GetData(typeof(InventoryDrag)) is not InventoryDrag drag||drag.Owner!=this)return;
                Point point=PointToClient(new(e.X,e.Y));bool trash=receiver==deleteItemButton;
                if(!trash&&(!PlayerPointerSceneHit(point)||PointerPixel(point)==null))return;
                ClearInput("inventory-drop");await DispatchItemTransfer(drag.Selection,trash?null:PointerPixel(point));
            };
        }
    }
    void ItemTransferDragEffect(Control receiver,DragEventArgs e) {
        Point point=PointToClient(new(e.X,e.Y));
        bool valid=!transferDialogOpen&&e.Data?.GetData(typeof(InventoryDrag)) is InventoryDrag drag&&drag.Owner==this
            &&(receiver==deleteItemButton||PlayerPointerSceneHit(point)&&PointerPixel(point) is uint id&&login.World.Objects.Any(o=>o.ObjectId==id));
        e.Effect=valid?DragDropEffects.Move:DragDropEffects.None;
    }
    async Task DispatchItemTransfer(ItemTransferSelection? selection,uint? target) {
        if(transferDialogOpen||World.Walking)return;
        var request=login.PrepareItemTransfer(selection,target,dropCount.Text);
        if(request==null){SyncInventory();PublishWebState();return;}
        transferDialogOpen=true;
        try {
            string prompt=request.Delete?$"確定永久刪除整疊「{ItemName(request.Selection.Item)}」× {request.Count}？\n刪除後無法取回。"
                :$"確定給予「{ItemName(request.Selection.Item)}」× {request.Count} 給「{(objectAssets??=new(World.Assets.Root)).Name(request.Target!.Name)}」？\n數量沿用背包數量欄位；需修改時請取消後重填。";
            bool yes=MessageBox.Show(this,prompt,request.Delete?"確認刪除整疊物品":"確認給予物品",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
            bool sent=await login.SendItemTransfer(request,yes,()=>!World.Walking&&!IsDisposed&&(request.Target==null||HasVisibleObjectPixel(request.Target.ObjectId)));
            if(yes&&!sent&&login.SessionGeneration==request.Selection.Session&&login.World.WorldEpoch==request.Selection.Quantity.Epoch)
                login.World.RejectGroundQuantity(false,"未送出：物品、目標或操作狀態已變更，請重新操作");
        }finally{transferDialogOpen=false;if(!IsDisposed){SyncInventory();PublishWebState();}}
    }
}
