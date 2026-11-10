using System.Text.Json;
using P06.NativeUi;
namespace P06;
public sealed record TradeWebCommand(string Command,int Revision,int Epoch,int Generation,uint Item=0,uint Count=0){
    public static TradeWebCommand Parse(JsonElement o){string cmd=o.GetProperty("cmd").GetString()??"";if(cmd is not ("tradeInvite" or "tradeAccept" or "tradeReject" or "tradeAdd" or "tradeConfirm" or "tradeCancel" or "tradeDismiss"))throw new FormatException("trade command");var expected=new List<string>{"v","id","cmd","revision","epoch","generation"};if(cmd=="tradeAdd")expected.AddRange(["itemId","count"]);if(!o.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(expected.Order())||o.GetProperty("v").GetInt32()!=1||o.GetProperty("id").GetInt32()<1)throw new FormatException("trade fields");int revision=o.GetProperty("revision").GetInt32(),epoch=o.GetProperty("epoch").GetInt32();int generation=o.GetProperty("generation").GetInt32();if(revision<0||epoch<0||generation<0)throw new FormatException("trade generation");uint item=0,count=0;if(cmd=="tradeAdd"){item=o.GetProperty("itemId").GetUInt32();count=o.GetProperty("count").GetUInt32();if(item==0||count is 0 or >int.MaxValue)throw new FormatException("trade quantity");}return new(cmd[5..].ToLowerInvariant(),revision,epoch,generation,item,count);}
}
public sealed record TradeInventoryChoice(uint Id,string Label,uint Count);
public sealed partial class ClientForm {
    readonly Button tradeInvite=new(){Text="邀請面前角色交易",Width=190,Height=30,Visible=false};
    readonly Panel tradePanel=new(){Dock=DockStyle.Bottom,Height=290,Visible=false,BackColor=Color.FromArgb(23,38,41)};
    readonly Label tradeNotice=new(){Dock=DockStyle.Top,Height=64,ForeColor=Color.White,Padding=new(5)};
    readonly ListBox tradeMine=new(){Dock=DockStyle.Fill,ForeColor=Color.White,BackColor=Color.FromArgb(27,44,51)};
    readonly ListBox tradeTheirs=new(){Dock=DockStyle.Fill,ForeColor=Color.White,BackColor=Color.FromArgb(27,44,51)};
    readonly ComboBox tradeInventory=new(){Width=245,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Label"};
    readonly NumericUpDown tradeQuantity=new(){Width=90,Minimum=1,Maximum=int.MaxValue,Value=1};
    readonly Button tradeAdd=new(){Text="加入一次",AutoSize=true},tradeConfirm=new(){Text="確認兩側物品",AutoSize=true},tradeCancel=new(){Text="取消交易",AutoSize=true},tradeAccept=new(){Text="接受邀請",AutoSize=true},tradeReject=new(){Text="拒絕邀請",AutoSize=true},tradeDismiss=new(){Text="關閉結果",AutoSize=true};
    readonly TableLayoutPanel tradeTable=new(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};
    readonly FlowLayoutPanel tradeControls=new(){Dock=DockStyle.Bottom,Height=70,AutoScroll=true};
    int tradeUiRevision=-1,tradeInventoryRevision=-1;TradeWebCommand? displayedTrade;bool tradeNativeShown;
    void InitTrade(){
        var table=tradeTable;table.ColumnStyles.Add(new(SizeType.Percent,50));table.ColumnStyles.Add(new(SizeType.Percent,50));table.RowStyles.Add(new(SizeType.Absolute,25));table.RowStyles.Add(new(SizeType.Percent,100));table.Controls.Add(new Label{Text="你提供的物品（原服回包）",ForeColor=Color.White,AutoSize=true},0,0);table.Controls.Add(new Label{Text="對方提供的物品（原服回包）",ForeColor=Color.White,AutoSize=true},1,0);table.Controls.Add(tradeMine,0,1);table.Controls.Add(tradeTheirs,1,1);
        var controls=tradeControls;controls.Controls.AddRange([tradeInventory,tradeQuantity,tradeAdd,tradeConfirm,tradeCancel,tradeAccept,tradeReject,tradeDismiss]);tradePanel.Controls.AddRange([table,tradeControls,tradeNotice]);Controls.Add(tradePanel);tradePanel.BringToFront();tradePanel.Enter+=(_,_)=>LeaveGameFocus("trade");tradeInvite.Enter+=(_,_)=>LeaveGameFocus("trade-invite");
        tradeInvite.Click+=async(_,_)=>await RunTrade(new("invite",login.World.TradeRevision,login.World.WorldEpoch,login.World.TradeGeneration));
        foreach(var (button,cmd) in new[]{(tradeAccept,"accept"),(tradeReject,"reject"),(tradeConfirm,"confirm"),(tradeCancel,"cancel"),(tradeDismiss,"dismiss")})button.Click+=async(_,_)=>{if(displayedTrade!=null)await RunTrade(displayedTrade with{Command=cmd});};
        tradeAdd.Click+=async(_,_)=>{if(displayedTrade!=null&&tradeInventory.SelectedItem is TradeInventoryChoice item)await RunTrade(displayedTrade with{Command="add",Item=item.Id,Count=(uint)tradeQuantity.Value});};
        // R9-K: the WinForms fallback keeps its quantity box; it now starts at the whole stack, like 140's blank Enter = all.
        tradeInventory.SelectedIndexChanged+=(_,_)=>{if(tradeInventory.SelectedItem is TradeInventoryChoice item){tradeQuantity.Maximum=Math.Max(1,Math.Min(item.Count,int.MaxValue));tradeQuantity.Value=tradeQuantity.Maximum;}};
    }
    bool TradeVisibleInvite=>login.CanInviteTrade&&login.World.FacingTradeTarget is {} t&&HasVisibleObjectPixel(t.ObjectId)&&!World.Walking;
    async Task<bool> RunTrade(TradeWebCommand c){ClearInput("trade-command");if(c.Command=="invite"&&!TradeVisibleInvite||World.Walking&&c.Command is "invite" or "add" or "confirm")return false;bool accepted=await login.Trade(c.Command,c.Revision,c.Epoch,c.Generation,c.Item,c.Count);SyncTrade();lastWebState="";PublishWebState();return accepted;}
    // Player invitation names are literal; only server/local notice text uses the original name catalog.
    string TradeDisplayNotice(){var w=login.World;return w.TradeState=="invited"&&w.TradeNotice.StartsWith(w.TradePeerName+" 邀請你交易。",StringComparison.Ordinal)?w.TradeNotice:ResolveNpcText(w.TradeNotice);}
    // M7-K (WIN§2.1): with byte_136E99D=1 the invitation is only the chat-input-line Y/N question; the trade window
    // (TRANSACTION_WINDOW, op 88/S52) appears after the server opens the trade. Flag 0 keeps the legacy buttons (推測 path).
    bool TradeInviteOnInputLine=>login.World.TradeState=="invited"&&login.World.TradeAskWindow;
    async Task DispatchTrade(int id,TradeWebCommand c){bool accepted=await RunTrade(c);Reply(id,accepted?TradeDisplayNotice():"交易操作已過期、數量無效或對象不符合；未送出。");}
    // R3-E (#674): with the native UI active, the open trade (S52 … S112) is the TRANSACTION_WINDOW panel (both lists, adena,
    // OK → C71, cancel → C86). R4-C (#715): items are added from the native inventory (ITEM_WINDOW selection + click on the own
    // list), so the WinForms panel is hidden completely while the native window shows the trade. R9-K (#855, FU9§12.1): a stack
    // (adena included — the engine-only adena box is gone, #818) first asks string 140 (blank Enter = all, Esc = cancel), then C37
    // with the typed amount; a single item goes straight to C37. Invitation/result states, the native-UI-off case (--classic-ui),
    // a faulted panel and a window the root closed (IUiClosablePanel → Dismissed) keep the full WinForms panel as the fallback entry.
    bool AttachTransactionWindow(){
        if(!NativeUiActive||NativeUiRoot is not {} root||root.Find(TransactionPanel.PanelId) is not TransactionPanel panel||root.Faulted.Contains(panel))return false;
        panel.Source??=new TransactionSource(this);
        return !panel.Dismissed;
    }
    bool TradeNativeWindow=>AttachTransactionWindow()&&login.State==LoginState.InWorld&&TransactionView.From(login.World,ResolveNpcText)!=null;
    sealed class TransactionSource(ClientForm form):ITransactionSource {
        public TransactionView? Current=>form.login.State==LoginState.InWorld?TransactionView.From(form.login.World,form.ResolveNpcText):null;
        public void Confirm(int revision)=>Run("confirm",revision);
        public void Cancel(int revision)=>Run("cancel",revision);
        public void Add(int revision,uint item,uint count){if(item!=0&&count is >0 and <=int.MaxValue)Run("add",revision,item,count);}
        void Run(string command,int revision,uint item=0,uint count=0){var w=form.login.World;_=form.RunTrade(new(command,revision,w.WorldEpoch,w.TradeGeneration,item,count));}
    }
    void SyncTrade(){var w=login.World;w.CheckTrade();tradeInvite.Visible=login.State==LoginState.InWorld;tradeInvite.Enabled=TradeVisibleInvite;
        bool native=TradeNativeWindow;tradePanel.Visible=login.State==LoginState.InWorld&&w.TradeState!="idle"&&!TradeInviteOnInputLine&&!native;
        tradeTable.Visible=tradeNotice.Visible=tradeConfirm.Visible=tradeCancel.Visible=true;tradePanel.Height=290;if(native!=tradeNativeShown){tradeNativeShown=native;Invalidate();}tradeNotice.Text=$"交易對象：{w.TradePeerName}\n{TradeDisplayNotice()}";tradeAccept.Visible=tradeReject.Visible=w.TradeState=="invited"&&!w.TradeAskWindow;tradeAccept.Enabled=w.TradePeerPresent;tradeReject.Enabled=w.TradeState=="invited";tradeCancel.Enabled=w.TradeCanCancel;tradeConfirm.Enabled=w.TradeCanConfirm;tradeAdd.Enabled=w.TradeCanAdd;tradeDismiss.Visible=!w.TradeBlocksActions;tradeInventory.Enabled=tradeQuantity.Enabled=w.TradeCanAdd;
        if(tradeUiRevision!=w.TradeRevision){tradeUiRevision=w.TradeRevision;displayedTrade=new("",w.TradeRevision,w.WorldEpoch,w.TradeGeneration);tradeMine.Items.Clear();tradeTheirs.Items.Clear();foreach(var offer in w.TradeOffers)(offer.Side==0?tradeMine:tradeTheirs).Items.Add(ResolveNpcText(offer.Name));}
        if(tradeInventoryRevision!=w.InventoryRevision){tradeInventoryRevision=w.InventoryRevision;uint? previous=(tradeInventory.SelectedItem as TradeInventoryChoice)?.Id;tradeInventory.Items.Clear();foreach(var i in w.Inventory.Where(i=>i.Count>0&&i.Equipped!=true)){tradeInventory.Items.Add(new TradeInventoryChoice(i.ObjectId,ItemName(i)+" × "+i.Count,i.Count));if(previous==i.ObjectId)tradeInventory.SelectedIndex=tradeInventory.Items.Count-1;}}
    }
    object TradeWeb()=>new{model=login.World.TradeSnapshot(),inviteOnInputLine=TradeInviteOnInputLine,canInvite=TradeVisibleInvite,facingName=login.World.FacingTradeTarget?.Name,notice=TradeDisplayNotice(),offers=login.World.TradeOffers.Select(o=>new{side=o.Side,name=ResolveNpcText(o.Name)}),inventory=login.World.Inventory.Where(i=>i.Count>0&&i.Equipped!=true).Select(i=>new{id=i.ObjectId,name=ItemName(i),count=i.Count}),inventoryRevision=login.World.InventoryRevision};
}
