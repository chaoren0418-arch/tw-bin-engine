namespace P06;
public sealed partial class ClientForm {
    readonly Panel inventoryPanel=new(){Location=new(12,150),Size=new(246,246),BackColor=Color.FromArgb(23,38,41),Visible=false};
    readonly ListBox inventoryList=new(){Dock=DockStyle.Fill,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=38,BackColor=Color.FromArgb(27,44,51),ForeColor=Color.White,BorderStyle=BorderStyle.None};
    readonly Button useItemButton=new(){Dock=DockStyle.Bottom,Height=33,Text="單次使用 (U)",Enabled=false,ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80),FlatStyle=FlatStyle.Flat};
    readonly Label itemNoticeLabel=new(){Dock=DockStyle.Bottom,Height=95,ForeColor=Color.White,Padding=new(5)};
    readonly Dictionary<ushort,Bitmap?> itemIcons=[];int inventoryUiRevision=-1;bool syncingInventory;
    Bitmap? ItemIcon(ushort id){if(itemIcons.TryGetValue(id,out var b))return b;string p=Path.Combine(stageRoot,"assets/inventory",id+".png");b=File.Exists(p)?new Bitmap(p):null;itemIcons[id]=b;return b;}
    string ItemName(InventoryItem i){string name=(objectAssets??=new(World.Assets.Root)).Name(i.Name);string suffix=" ("+i.Count+")";return name.EndsWith(suffix,StringComparison.Ordinal)?name[..^suffix.Length]:name;}
    void InitInventory(){
        var title=new Label{Text="背包",Dock=DockStyle.Top,Height=26,ForeColor=Color.White,Padding=new(6)};
        inventoryPanel.Controls.AddRange([inventoryList,itemNoticeLabel,useItemButton,title]);Controls.Add(inventoryPanel);inventoryPanel.BringToFront();
        inventoryList.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index<0||e.Index>=inventoryList.Items.Count)return;var i=(InventoryItem)inventoryList.Items[e.Index];var icon=ItemIcon(i.IconId);if(icon!=null)e.Graphics.DrawImageUnscaled(icon,e.Bounds.X+4,e.Bounds.Y+3);else TextRenderer.DrawText(e.Graphics,"缺圖",Font,new Point(e.Bounds.X+2,e.Bounds.Y+8),Color.Gray);TextRenderer.DrawText(e.Graphics,ItemName(i)+" × "+i.Count,Font,new Rectangle(e.Bounds.X+40,e.Bounds.Y+4,e.Bounds.Width-42,32),Color.White,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};
        inventoryList.SelectedIndexChanged+=(_,_)=>{if(syncingInventory)return;ClearInput("inventory-select");login.World.SelectItem(inventoryList.SelectedItem is InventoryItem i?i.ObjectId:null);SyncQuantitySelection(false);lastWebState="";PublishWebState();};
        inventoryList.Enter+=(_,_)=>LeaveGameFocus("inventory-list");useItemButton.Enter+=(_,_)=>LeaveGameFocus("inventory-button");
        useItemButton.Click+=async(_,_)=>{ClearInput("inventory-use");await login.UseSelectedItem();SyncInventory();PublishWebState();};
        FormClosed+=(_,_)=>{foreach(var b in itemIcons.Values)b?.Dispose();};
    }
    void SyncInventory(){
        inventoryPanel.Visible=login.State==LoginState.InWorld;
        if(inventoryUiRevision!=login.World.InventoryRevision){inventoryUiRevision=login.World.InventoryRevision;syncingInventory=true;inventoryList.BeginUpdate();inventoryList.Items.Clear();foreach(var i in login.World.Inventory){inventoryList.Items.Add(i);if(i.ObjectId==login.World.SelectedItemId)inventoryList.SelectedIndex=inventoryList.Items.Count-1;}inventoryList.EndUpdate();syncingInventory=false;}
        useItemButton.Text=(login.World.SelectedItem is {} selected?login.World.ItemUseAction(selected):"單次使用")+" (U)";
        useItemButton.Enabled=login.ItemAvailable&&!login.MovementBusy&&!World.Walking;
        itemNoticeLabel.Text=(login.World.SelectedItem is {} noticeItem?login.World.ItemUseState(noticeItem):"")+"\n"+login.World.ItemNotice+(login.World.LastServerMessage is {} message?"\n"+ResolveNpcText(message):"")+(login.World.LastAnnouncement.Length>0?"\n"+login.World.LastAnnouncement:"");
    }
    object InventoryWeb()=>new{epoch=login.World.WorldEpoch,selectionRevision=login.World.ItemSelectionRevision,revision=login.World.InventoryRevision,quantityRevision=login.World.InventoryContentRevision,selectedId=login.World.SelectedItemId,notice=login.World.ItemNotice,message=login.World.LastServerMessage is {} message?ResolveNpcText(message):null,busy=login.MovementBusy,available=login.ItemAvailable,items=login.World.Inventory.Select(i=>new{id=i.ObjectId,name=ItemName(i),count=i.Count,selected=i.ObjectId==login.World.SelectedItemId,usable=i.SupportedUse,equipped=login.World.ItemEquipped(i),action=login.World.ItemUseAction(i),droppable=login.World.CanDropGroundItem(i),state=login.World.ItemUseState(i),icon=ItemIcon(i.IconId)!=null?$"https://p08.example/icons/{i.IconId}.png":null}).ToArray()};
}
