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
        login.ItemArgumentsRequested+=OpenItemUseDialog;
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape&&itemUseDialog!=null){itemUseDialog.Close();e.Handled=true;}};
        var title=new Label{Text="背包",Dock=DockStyle.Top,Height=26,ForeColor=Color.White,Padding=new(6)};
        inventoryPanel.Controls.AddRange([inventoryList,itemNoticeLabel,useItemButton,title]);Controls.Add(inventoryPanel);inventoryPanel.BringToFront();
        inventoryList.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index<0||e.Index>=inventoryList.Items.Count)return;var i=(InventoryItem)inventoryList.Items[e.Index];var icon=ItemIcon(i.IconId);if(icon!=null)e.Graphics.DrawImageUnscaled(icon,e.Bounds.X+4,e.Bounds.Y+3);else TextRenderer.DrawText(e.Graphics,"缺圖",Font,new Point(e.Bounds.X+2,e.Bounds.Y+8),Color.Gray);TextRenderer.DrawText(e.Graphics,ItemName(i)+" × "+i.Count,Font,new Rectangle(e.Bounds.X+40,e.Bounds.Y+4,e.Bounds.Width-42,32),Color.White,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};
        inventoryList.SelectedIndexChanged+=(_,_)=>{if(syncingInventory)return;ClearInput("inventory-select");login.World.SelectItem(inventoryList.SelectedItem is InventoryItem i?i.ObjectId:null);SyncQuantitySelection(false);lastWebState="";PublishWebState();};
        inventoryList.Enter+=(_,_)=>LeaveGameFocus("inventory-list");useItemButton.Enter+=(_,_)=>LeaveGameFocus("inventory-button");
        useItemButton.Click+=async(_,_)=>{ClearInput("inventory-use");await login.UseSelectedItem();SyncInventory();PublishWebState();};
        InitItemTransfer();
        FormClosed+=(_,_)=>{foreach(var b in itemIcons.Values)b?.Dispose();};
    }
    void SyncInventory(){
        if(itemUseDialog is {} dialog&&!login.CurrentItemUse(dialog.Request))dialog.Close();
        if(itemUseDialog is {} selecting)SetSingleActionNotice(ItemUseCodec.Prompt(selecting.Request.Item.UseType));
        inventoryPanel.Visible=login.State==LoginState.InWorld;
        if(inventoryUiRevision!=login.World.InventoryRevision){inventoryUiRevision=login.World.InventoryRevision;syncingInventory=true;inventoryList.BeginUpdate();inventoryList.Items.Clear();foreach(var i in login.World.Inventory){inventoryList.Items.Add(i);if(i.ObjectId==login.World.SelectedItemId)inventoryList.SelectedIndex=inventoryList.Items.Count-1;}inventoryList.EndUpdate();syncingInventory=false;}
        useItemButton.Text=(login.World.SelectedItem is {} selected?login.World.ItemUseAction(selected):"單次使用")+" (U)";
        useItemButton.Enabled=login.ItemAvailable&&!login.MovementBusy&&!World.Walking;
        itemNoticeLabel.Text=(login.World.SelectedItem is {} noticeItem?login.World.ItemUseState(noticeItem):"")+"\n"+login.World.ItemNotice+(login.World.LastServerMessage is {} message?"\n"+ResolveNpcText(message):"")+(login.World.LastAnnouncement.Length>0?"\n"+login.World.LastAnnouncement:"");
    }
    object InventoryWeb()=>new{epoch=login.World.WorldEpoch,selectionRevision=login.World.ItemSelectionRevision,revision=login.World.InventoryRevision,quantityRevision=login.World.InventoryContentRevision,selectedId=login.World.SelectedItemId,notice=login.World.ItemNotice,message=login.World.LastServerMessage is {} message?ResolveNpcText(message):null,busy=login.MovementBusy,available=login.ItemAvailable,items=login.World.Inventory.Select(i=>new{id=i.ObjectId,name=ItemName(i),count=i.Count,selected=i.ObjectId==login.World.SelectedItemId,usable=i.SupportedUse,equipped=login.World.ItemEquipped(i),action=login.World.ItemUseAction(i),droppable=login.World.CanDropGroundItem(i),state=login.World.ItemUseState(i),icon=ItemIcon(i.IconId)!=null?$"https://p08.example/icons/{i.IconId}.png":null}).ToArray()};

    ItemUseDialog? itemUseDialog;

    void OpenItemUseDialog(ItemUseRequest request){
        itemUseDialog?.Close();
        ClearInput("item-arguments");
        var dialog=new ItemUseDialog(request,login.World.Inventory.Select(i=>new ItemUseChoice(i.ObjectId,ItemName(i)+" × "+i.Count)).ToArray());
        itemUseDialog=dialog;
        dialog.Text=ItemName(request.Item);
        dialog.Submit=async arguments=>{
            bool sent=await login.UseItemWithArguments(request,arguments);
            dialog.ShowNotice(login.World.ItemNotice);
            if(sent||!login.CurrentItemUse(request))dialog.Close();
            SetSingleActionNotice(login.World.ItemNotice);
            SyncInventory();PublishWebState();
        };
        dialog.FormClosed+=(_,_)=>{
            if(ReferenceEquals(login.PendingItemUse,request))login.CancelItemUse();
            if(ReferenceEquals(itemUseDialog,dialog))itemUseDialog=null;
        };
        dialog.Show(this);
    }

    protected override void OnMouseDown(MouseEventArgs e){
        if(itemUseDialog is not {} dialog){base.OnMouseDown(e);return;}
        // Consume target clicks before the normal move/attack/talk handlers run.
        if(!login.CurrentItemUse(dialog.Request)||e.Button==MouseButtons.Right){dialog.Close();return;}
        if(e.Button!=MouseButtons.Left||!PlayerPointerSceneHit(e.Location))return;
        ClearInput("item-target");
        var kind=ItemUseCodec.Kind(dialog.Request.Item.UseType);
        if(kind==ItemUseKind.Location){
            var scene=Camera.ToScene(e.Location);var tile=Coordinates.Pick(scene.X,scene.Y);
            dialog.SelectScene(new ItemUseArguments.Location(tile.X,tile.Y),$"地面 ({tile.X}, {tile.Y})");
        }else if(kind is ItemUseKind.Object or ItemUseKind.ObjectLocation){
            uint? id=PointerPixel(e.Location);
            if(id==login.World.Actor?.ObjectId&&login.World.Actor is {} actor)
                dialog.SelectScene(new ItemUseArguments.WorldTarget(actor.ObjectId,actor.X,actor.Y),$"自身 ({actor.X}, {actor.Y})");
            else if(login.World.Objects.FirstOrDefault(o=>o.ObjectId==id) is {} target)
                dialog.SelectScene(new ItemUseArguments.WorldTarget(target.ObjectId,target.X,target.Y),$"目標 #{target.ObjectId} ({target.X}, {target.Y})");
            else dialog.ShowNotice("請點選場景中的角色或物件");
        }
    }
}

public sealed record ItemUseChoice(uint ObjectId,string Name){public override string ToString()=>Name;}

// Owned modeless window keeps text entry outside game keyboard routing and leaves the scene clickable.
public sealed class ItemUseDialog:Form {
    public ItemUseRequest Request {get;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<ItemUseArguments,Task>? Submit {get;set;}
    readonly FlowLayoutPanel fields=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new(12)};
    readonly Label notice=new(){Width=310,Height=64};
    readonly Button confirm=new(){Text="確認使用",AutoSize=true};
    readonly Func<ItemUseArguments> readArguments;
    ItemUseArguments? sceneArguments;

    public ItemUseDialog(ItemUseRequest request,ItemUseChoice[] inventory){
        Request=request;ClientSize=new(350,440);FormBorderStyle=FormBorderStyle.FixedToolWindow;
        StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;AutoScaleMode=AutoScaleMode.Dpi;
        Controls.Add(fields);
        fields.Controls.Add(new Label{Text=ItemUseCodec.Prompt(request.Item.UseType),Width=310,Height=52});
        readArguments=CreateFields(ItemUseCodec.Kind(request.Item.UseType),inventory);
        var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};
        var buttons=new FlowLayoutPanel{Width=310,Height=40};buttons.Controls.AddRange([confirm,cancel]);
        fields.Controls.Add(buttons);fields.Controls.Add(notice);CancelButton=cancel;
        cancel.Click+=(_,_)=>Close();
        confirm.Click+=async(_,_)=>{
            confirm.Enabled=false;
            try{if(Submit!=null)await Submit(readArguments());}
            catch(Exception ex) when(ex is ArgumentException or OverflowException){ShowNotice(ex.Message);}
            finally{if(!IsDisposed)confirm.Enabled=true;}
        };
    }

    public void ShowNotice(string text){if(!IsDisposed)notice.Text=text;}
    public void SelectScene(ItemUseArguments arguments,string description){sceneArguments=arguments;ShowNotice(description);confirm.Enabled=true;Activate();}
    void Label(string text)=>fields.Controls.Add(new Label{Text=text,Width=310,Height=24});
    TextBox TextField(string label,int maxLength,bool multiline=false){
        Label(label);var field=new TextBox{Width=310,MaxLength=maxLength,Multiline=multiline,Height=multiline?80:25};fields.Controls.Add(field);return field;
    }
    NumericUpDown NumberField(string label,decimal maximum,decimal minimum=0){
        Label(label);var field=new NumericUpDown{Width=310,Minimum=minimum,Maximum=maximum,Value=minimum};fields.Controls.Add(field);return field;
    }
    Func<ItemUseArguments> CreateFields(ItemUseKind kind,ItemUseChoice[] inventory){
        switch(kind){
            case ItemUseKind.Inventory:
                var choices=new ComboBox{Width=310,DropDownStyle=ComboBoxStyle.DropDownList};
                choices.Items.AddRange(inventory);fields.Controls.Add(choices);
                return ()=>choices.SelectedItem is ItemUseChoice item?new ItemUseArguments.InventoryTarget(item.ObjectId):throw new ArgumentException("請先選擇目標物品");
            case ItemUseKind.Object:
            case ItemUseKind.ObjectLocation:
            case ItemUseKind.Location:
                confirm.Enabled=false;Label("左鍵選取；右鍵或 Esc 取消");
                return ()=>sceneArguments??throw new ArgumentException("請先點選場景目標");
            case ItemUseKind.Name:
                var name=TextField("變身名稱",200);
                return ()=>new ItemUseArguments.Name(name.Text);
            case ItemUseKind.Teleport:
                Label("所在場景 "+Request.MapId);
                Label("無效書籤可能由原服改為隨機傳送");
                var bookmark=NumberField("原服書籤 ID（0 為隨機）",int.MaxValue);
                return ()=>new ItemUseArguments.Teleport(Request.MapId??throw new ArgumentException("尚無場景資料"),(uint)bookmark.Value);
            case ItemUseKind.Cooking:
                var mode=new ComboBox{Width=310,DropDownStyle=ComboBoxStyle.DropDownList};
                mode.Items.AddRange(["開啟料理書","製作料理"]);mode.SelectedIndex=0;fields.Controls.Add(mode);
                var recipe=NumberField("食譜編號（從 0 起）",255);
                return ()=>new ItemUseArguments.Cooking(mode.SelectedIndex,(int)recipe.Value);
            case ItemUseKind.Spell:
                var spell=NumberField("魔法編號（從 1 起）",256,1);
                return ()=>new ItemUseArguments.Spell((int)spell.Value-1);
            case ItemUseKind.Letter:
                var code=NumberField("信紙代碼",65535);
                var receiver=TextField("收件人",200);var subject=TextField("主旨",200);var body=TextField("本文",2000,true);
                return ()=>new ItemUseArguments.Letter((int)code.Value,receiver.Text,subject.Text,body.Text);
            default:throw new ArgumentException("此道具沒有可輸入的使用參數");
        }
    }
}
