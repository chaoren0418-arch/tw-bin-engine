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

/// <summary>
/// R6-E（T335／#781，FU7§1.5）：刪除物品的本地確認（sub_98D2E0 的流程），提示放在聊天輸入列（sub_AC98E0），不是彈出框：
/// 1. 可堆疊且數量 &gt;1：先問數量（字串 4115，本機 string-c.tbl 沒有，用引擎文字）；輸入空白＝全部，有輸入就照數字（sub_500900）。
/// 2. 確認 Y/N：數量 1 用字串 3122（有表讀表），數量 &gt;1 用 4114（本機表沒有，用引擎文字）；Y 開頭才刪（同 YesNoProtocol.Accepts）。
/// 3.81 的 C138 只有物件編號、伺服器整疊刪除，所以數量只能是全部；輸入比持有量少時不送、說明原因。
/// 哪些物品需要確認（sub_996B10 的虛擬判斷）沒有逐類追（Q6-9）；BeginItemDelete 一律確認，產品的兩條路徑（原生背包窗、
/// WinForms 垃圾桶，R9-L／T403）都改照 alertitems.tbl（AlertOnly）。
/// </summary>
/// <param name="AskFirst">R9-L（#856）：這一步是 668「真的要把 "%s" 刪除嗎？」（背包窗的單件刪除）。</param>
/// <param name="AlertOnly">R9-L（#856）：背包窗流程——3122／4114 只在物品列在 alertitems.tbl 時才問（FU8§8.1）；false＝R6-E 一律問。</param>
public sealed record ItemDeletePrompt(int Id,ItemTransferRequest Request,string Name,bool AskAmount,string Text,Func<bool>? UiCurrent,bool AskFirst=false,bool AlertOnly=false);
public static class ItemDeleteText {
    public const int ConfirmOne=3122,ConfirmMany=4114,AskAmount=4115;
    /// <summary>R9-L（#856，FU9§13）：背包窗單件刪除先問 668；堆疊的數量提示本機表沒有 4115，3054 文字相同（推測是舊編號）。</summary>
    public const int AskDelete=668,AskAmountLocal=3054;
    public const string ConfirmOneFallback="\"%s\" 刪除後，即無法再次復原，真的要刪除嗎？ (Y/N)";
    /// <summary>0-客戶端 string-c.tbl 668／3054 原文（色碼拿掉；scratch 唯讀腳本讀出，見 docs/reports/issue-856.md）；字串表不在時用。</summary>
    public const string AskDeleteFallback="真的要把 \"%s\" 刪除嗎？(Y/N)",AskAmountLocalFallback="要刪除幾個呢？(1-%u, Enter=全部, Esc=取消)";
    /// <summary>字串表有這個編號時照表（%s＝名稱、%u＝數量，色碼拿掉）；沒有時回 null。</summary>
    static string? Table(int id,string name,uint count=0){
        if(ServerMessageText.Table is not {} t||id>=t.Count)return null;
        string text=ServerMessageText.DisplayText(Fill(t[id],name,count)).Trim();
        return text.Length>0?text:null;
    }
    /// <summary>第一個 %s 換名稱、第一個 %u 換數量（sprintf 的這兩個參數；其餘原樣）。</summary>
    static string Fill(string raw,string name,uint count){
        int at=raw.IndexOf("%s",StringComparison.Ordinal);
        if(at>=0)raw=string.Concat(raw.AsSpan(0,at),name,raw.AsSpan(at+2));
        int u=raw.IndexOf("%u",StringComparison.Ordinal);
        if(u>=0)raw=string.Concat(raw.AsSpan(0,u),count.ToString(System.Globalization.CultureInfo.InvariantCulture),raw.AsSpan(u+2));
        return raw;
    }
    public static string Confirm(string name,uint count)=>count<=1
        ?Table(ConfirmOne,name)??ConfirmOneFallback.Replace("%s",name)
        :Table(ConfirmMany,name)??$"\"{name}\" × {count} 刪除後，即無法再次復原，真的要刪除嗎？ (Y/N)";
    /// <summary>數量提示：4115（本機表沒有）→ 3054 → 原文代用。</summary>
    public static string Amount(string name,uint count)=>Table(AskAmount,name,count)??Table(AskAmountLocal,name,count)??Fill(AskAmountLocalFallback,name,count);
    /// <summary>R9-L：668。</summary>
    public static string AskFirst(string name)=>Table(AskDelete,name)??AskDeleteFallback.Replace("%s",name);
}

/// <summary>
/// R9-L（T372／#856，FU8§8.1）：<c>alertitems.tbl</c>（Text.pak，本機第 806 筆，1,382 B，232 個編號，每行一個整數，沒有筆數行）。
/// <c>ItemDeleteAlertChecker</c>（sub_996AA0）：物品的 16-bit 編號在清單內才問 3122／4114，不在就直接刪。
/// 推測：TW16 的 this[19] 對到 3.81 S_AddItem 的 DescriptionId（第 2 個欄位，H）；SP 原始碼未對（FU8§8.1 待驗證）。
/// 讀不到表（CI 沒有客戶端、pin 不符）時一律當成「要問」，比原版多問一次但不會少問。執行期唯讀，不寫入 repo。
/// </summary>
public sealed class ItemDeleteAlertList {
    public const string FileName="alertitems.tbl";
    /// <summary>本機解出內容的 SHA-256（Text.pak 第 806 筆）。</summary>
    public const string Sha256Pin="a39418ec37a4d62d57d8f48af79bdd4d127303bc9bc24d25a76158dc5149ceae";
    readonly HashSet<int> ids;
    ItemDeleteAlertList(HashSet<int> ids){this.ids=ids;}
    public int Count=>ids.Count;
    public bool Contains(int id)=>ids.Contains(id);
    /// <summary>每行一個非負整數（前後空白與 \r 忽略，空行略過）；其他內容 → InvalidDataException。</summary>
    public static ItemDeleteAlertList Parse(byte[] decoded){
        var set=new HashSet<int>();
        foreach(var raw in System.Text.Encoding.ASCII.GetString(decoded).Split('\n')){
            string line=raw.Trim();if(line.Length==0)continue;
            if(!int.TryParse(line,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int id))throw new InvalidDataException($"{FileName}：不是整數的行");
            set.Add(id);
        }
        return new(set);
    }
    public static ItemDeleteAlertList Load(TextArchive archive){
        var decoded=archive.Read(archive.Single(FileName)??throw new InvalidDataException($"Text 封存沒有 {FileName}"));
        if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(decoded)).Equals(Sha256Pin,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException($"{FileName} pin 不符");
        return Parse(decoded);
    }

    static readonly object gate=new();
    static ItemDeleteAlertList? list,overrideList;static bool loaded,overridden;
    public static string? LoadError{get;private set;}
    /// <summary>執行期的清單（延遲、唯讀，從 GroundSpriteReader.ClientRoot）；讀不到是 null。</summary>
    public static ItemDeleteAlertList? Current{get{
        lock(gate){
            if(overridden)return overrideList;
            if(!loaded){
                loaded=true;
                try{list=Load(TextArchive.Open(GroundSpriteReader.ClientRoot));LoadError=null;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException){
                    list=null;LoadError=ex.Message;System.Diagnostics.Trace.WriteLine("[item-delete] "+FileName+" 無法載入，刪除一律再確認："+ex.Message);
                }
            }
            return list;
        }
    }}
    /// <summary>Tests only: replace the list (null = no table) until the returned scope is disposed.</summary>
    internal static IDisposable Use(ItemDeleteAlertList? replacement){
        lock(gate){var previous=(overridden,overrideList);overridden=true;overrideList=replacement;return new Scope(()=>{lock(gate)(overridden,overrideList)=previous;});}
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}
    /// <summary>這件物品刪除前要不要再問 3122／4114（沒有表＝要問）。</summary>
    public static bool NeedsConfirm(InventoryItem item)=>Current is not {} l||l.Contains(item.DescriptionId);
}

public sealed partial class LoginConnection {
    ItemDeletePrompt? itemDeletePrompt;int itemDeletePromptIds;
    /// <summary>目前在聊天輸入列等待答覆的刪除提示；連線世代或世界變了就失效。</summary>
    public ItemDeletePrompt? ItemDeletePromptCurrent{get{
        if(itemDeletePrompt is {} p&&(p.Request.Selection.Session!=generation||World.WorldEpoch!=p.Request.Selection.Quantity.Epoch||Volatile.Read(ref p.Request.Consumed)!=0))itemDeletePrompt=null;
        return itemDeletePrompt;
    }}
    bool ItemDeletePromptActive=>ItemDeletePromptCurrent!=null;
    /// <summary>開始刪除確認（只限 PrepareItemTransfer 給的刪除請求）。新的刪除會取代舊的提示，舊的不送。</summary>
    public ItemDeletePrompt? BeginItemDelete(ItemTransferRequest request,string name,Func<bool>? uiCurrent=null){
        if(!request.Delete||request.Selection.Session!=generation)return null;
        bool amount=request.Count>1;
        return StartItemDelete(new(++itemDeletePromptIds,request,name,amount,amount?ItemDeleteText.Amount(name,request.Count):ItemDeleteText.Confirm(name,request.Count),uiCurrent));
    }
    /// <summary>
    /// R9-L（T372／#856，FU9§13、FU8§8.1）：背包窗（垃圾桶、Delete 鍵）的刪除流程，sub_9A52C0 → sub_98D2E0：
    /// 單件 → 668 Y/N → 是 → 物品在 alertitems.tbl 才再問 3122，否則直接送；
    /// 堆疊（數量 &gt;1）→ 數量提示（4115／3054）→ 物品在 alertitems.tbl 才再問 4114，否則直接送。
    /// 「堆疊」用數量 &gt;1 判斷（推測：數量 1 的可堆疊物品原版走哪一支沒追，真機再確認）。WinForms 垃圾桶走 BeginClassicItemDelete（T403，同樣照 alertitems.tbl，不問 668）。
    /// </summary>
    public ItemDeletePrompt? BeginInventoryDelete(ItemTransferRequest request,string name,Func<bool>? uiCurrent=null){
        if(!request.Delete||request.Selection.Session!=generation)return null;
        bool amount=request.Count>1;
        return StartItemDelete(new(++itemDeletePromptIds,request,name,amount,amount?ItemDeleteText.Amount(name,request.Count):ItemDeleteText.AskFirst(name),uiCurrent,AskFirst:!amount,AlertOnly:true));
    }
    /// <summary>
    /// T403（#918，gap-matrix 15.12.6 小清理）：classic 路徑（WinForms 垃圾桶、拖到垃圾桶）也照 alertitems.tbl 決定要不要問 3122／4114：
    /// 單件 → 物品在清單內才問 3122，不在就直接送（不問 668：668 是原生背包窗的單件刪除，WinForms 垃圾桶沒有這一步）；
    /// 堆疊（數量 &gt;1）→ 照舊先問數量（4115／3054），之後物品在清單內才問 4114。讀不到表時一律問（同 R9-L）。
    /// 回傳等待中的提示；直接送出時回 null（結果寫在聊天列提示）。
    /// </summary>
    public async Task<ItemDeletePrompt?> BeginClassicItemDelete(ItemTransferRequest request,string name,Func<bool>? uiCurrent=null){
        if(!request.Delete||request.Selection.Session!=generation)return null;
        bool amount=request.Count>1;
        var p=new ItemDeletePrompt(++itemDeletePromptIds,request,name,amount,amount?ItemDeleteText.Amount(name,request.Count):ItemDeleteText.Confirm(name,request.Count),uiCurrent,AlertOnly:true);
        if(amount||ItemDeleteAlertList.NeedsConfirm(request.Selection.Item))return StartItemDelete(p);
        if(itemDeletePrompt is {} old){Interlocked.Exchange(ref old.Request.Consumed,1);itemDeletePrompt=null;}
        await FinishItemDelete(p,true,generation);
        return null;
    }
    ItemDeletePrompt StartItemDelete(ItemDeletePrompt p){
        if(itemDeletePrompt is {} old)Interlocked.Exchange(ref old.Request.Consumed,1);
        itemDeletePrompt=p;playerChatNotice=p.Text;Changed?.Invoke();return p;
    }
    /// <summary>下一步：要再問 3122／4114 就換成 Y/N 提示；背包窗流程且物品不在 alertitems.tbl → 直接送（null）。</summary>
    ItemDeletePrompt? NextConfirm(ItemDeletePrompt p)=>p.AlertOnly&&!ItemDeleteAlertList.NeedsConfirm(p.Request.Selection.Item)?null
        :p with{Id=++itemDeletePromptIds,AskAmount=false,AskFirst=false,Text=ItemDeleteText.Confirm(p.Name,p.Request.Count)};
    /// <summary>Esc 或換頁：放棄提示，不送。</summary>
    public bool CancelItemDelete(){
        if(ItemDeletePromptCurrent is not {} p)return false;
        Interlocked.Exchange(ref p.Request.Consumed,1);itemDeletePrompt=null;playerChatNotice="已取消刪除。";Changed?.Invoke();return true;
    }
    /// <summary>聊天輸入列的答覆（PlayerChat.SendPlayerChat 轉來）。回 true＝這一列已當作答覆處理（輸入列可清掉）。</summary>
    async Task<bool> AnswerItemDelete(string typed,int session,int world,uint actor){
        if(ItemDeletePromptCurrent is not {} p||!PlayerChatStamp(session,world,actor))return false;
        string answer=typed.Trim();
        if(p.AskAmount){
            uint count=p.Request.Count;
            if(answer.Length>0&&!(uint.TryParse(answer,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out count)&&count>0)){
                CancelItemDelete();playerChatNotice="未刪除：數量不是正整數。";Changed?.Invoke();return true;
            }
            if(count<p.Request.Count){
                CancelItemDelete();playerChatNotice="未刪除：3.81 的刪除封包只帶物件編號，伺服器會整疊刪除，不能只刪一部分。";Changed?.Invoke();return true;
            }
            if(count>p.Request.Count){CancelItemDelete();playerChatNotice="未刪除：數量超過持有量。";Changed?.Invoke();return true;}
            if(NextConfirm(p) is not {} next)return await FinishItemDelete(p,true,session);
            itemDeletePrompt=next;playerChatNotice=next.Text;Changed?.Invoke();return true;
        }
        bool yes=YesNoProtocol.Accepts(answer);
        if(p.AskFirst&&yes){
            // 668 答是：alertitems.tbl 內的物品再問 3122（FU8§8.1），其他直接送。
            if(NextConfirm(p) is not {} confirm)return await FinishItemDelete(p,true,session);
            itemDeletePrompt=confirm;playerChatNotice=confirm.Text;Changed?.Invoke();return true;
        }
        return await FinishItemDelete(p,yes,session);
    }
    async Task<bool> FinishItemDelete(ItemDeletePrompt p,bool yes,int session){
        itemDeletePrompt=null;
        bool sent=await SendItemTransfer(p.Request,yes,p.UiCurrent);
        if(session!=generation)return true;
        playerChatNotice=!yes?"已取消刪除。":sent?"刪除請求已送出；等待伺服器背包／訊息回報":"未送出：物品或操作狀態已變更，請重新操作";
        Changed?.Invoke();return true;
    }
}

public sealed partial class ClientForm {
    readonly Button deleteItemButton=new(){Dock=DockStyle.Bottom,Height=30,Text="垃圾桶：刪除整疊（可拖入）",ForeColor=Color.White,BackColor=Color.FromArgb(92,48,45),AllowDrop=true};
    Point transferDragStart;ItemTransferSelection? transferDragItem;bool transferDialogOpen;
    sealed record InventoryDrag(ClientForm Owner,ItemTransferSelection Selection);
    /// <summary>
    /// R9-L（T372／#856）：原生背包窗（ItemWindowPanel）的雙擊使用與垃圾桶／Delete 刪除，轉到既有入口：
    /// 使用＝選取後 LoginConnection.UseSelectedItem（同 U 鍵與「使用」鈕）；刪除＝選取後 DispatchItemTransfer(…, inventoryWindow:true)。
    /// </summary>
    sealed class NativeItemWindowHost(ClientForm form):NativeUi.IItemWindowHost {
        public bool Use(uint objectId){
            if(form.IsDisposed||form.World.Walking||!form.login.World.SelectItem(objectId))return false;
            form.ClearInput("native-item-use");
            _=form.UseNativeItem();return true;
        }
        public bool Delete(uint objectId){
            if(form.IsDisposed||form.transferDialogOpen||form.World.Walking||!form.login.World.SelectItem(objectId))return false;
            form.ClearInput("native-item-delete");
            _=form.DispatchItemTransfer(form.login.CaptureItemTransfer(),null,inventoryWindow:true);return true;
        }
    }
    async Task UseNativeItem(){await login.UseSelectedItem();if(!IsDisposed){SyncInventory();PublishWebState();}}
    void InitItemTransfer() {
        NativeUi.ItemWindowPanel.Host=new NativeItemWindowHost(this);
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
    /// <param name="inventoryWindow">R9-L（#856）：從原生背包窗來（垃圾桶／Delete 鍵）→ 668／alertitems.tbl 流程（BeginInventoryDelete）。</param>
    async Task DispatchItemTransfer(ItemTransferSelection? selection,uint? target,bool inventoryWindow=false) {
        if(transferDialogOpen||World.Walking)return;
        var request=login.PrepareItemTransfer(selection,target,dropCount.Text);
        if(request==null){SyncInventory();PublishWebState();return;}
        if(request.Delete){
            // R6-E（FU7§1.5）：原版的刪除確認在聊天輸入列（數量→Y/N），不是彈出框；答覆由 SendPlayerChat 接手。
            string name=ItemName(request.Selection.Item);Func<bool> current=()=>!World.Walking&&!IsDisposed;
            // T403（#918）：classic 路徑也照 alertitems.tbl（BeginClassicItemDelete）；不在清單內的單件直接送，不開輸入列。
            bool asking=inventoryWindow?login.BeginInventoryDelete(request,name,current)!=null:await login.BeginClassicItemDelete(request,name,current)!=null;
            if(IsDisposed)return;
            if(asking)OpenPlayerChat(false);
            if(playerHud!=null)playerHud.UpdateChat(login.PlayerChatSnapshot());
            SyncInventory();PublishWebState();return;
        }
        transferDialogOpen=true;
        try {
            string prompt=$"確定給予「{ItemName(request.Selection.Item)}」× {request.Count} 給「{(objectAssets??=new(World.Assets.Root)).Name(request.Target!.Name)}」？\n數量沿用背包數量欄位；需修改時請取消後重填。";
            bool yes=MessageBox.Show(this,prompt,"確認給予物品",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
            bool sent=await login.SendItemTransfer(request,yes,()=>!World.Walking&&!IsDisposed&&(request.Target==null||HasVisibleObjectPixel(request.Target.ObjectId)));
            if(yes&&!sent&&login.SessionGeneration==request.Selection.Session&&login.World.WorldEpoch==request.Selection.Quantity.Epoch)
                login.World.RejectGroundQuantity(false,"未送出：物品、目標或操作狀態已變更，請重新操作");
        }finally{transferDialogOpen=false;if(!IsDisposed){SyncInventory();PublishWebState();}}
    }
}
