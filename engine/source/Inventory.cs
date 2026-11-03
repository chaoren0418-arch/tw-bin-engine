using System.Buffers.Binary;
namespace P06;
// Template IDs are not carried by these packets. Description/icon/use type remain distinct.
public sealed record InventoryItem(uint ObjectId,ushort DescriptionId,byte UseType,byte Charges,ushort IconId,byte Bless,uint Count,byte Flags,string Name,string StatusHex,string TailHex) {
    // Original L1ItemStatus.getNumberedViewName offsets 478..493 and 562..576.
    public bool? Equipped=>UseType==1?Name.EndsWith(" ($9)",StringComparison.Ordinal):EquipmentRules.IsArmor(UseType)?Name.EndsWith(" ($117)",StringComparison.Ordinal):null;
    bool NormalPotionName(string name)=>ObjectId!=0&&Count>0&&Charges==0&&Bless==1&&(Flags&0xE0)==0&&Name==(Count==1?name:name+" ("+Count.ToString(System.Globalization.CultureInfo.InvariantCulture)+")");
    public bool SupportedHealingPotion=>DescriptionId==1342&&IconId==80&&UseType==249&&NormalPotionName("$26");
    public bool SupportedHastePotion=>DescriptionId==17&&IconId==77&&UseType==246&&NormalPotionName("$234");
    public bool SupportedMeat=>DescriptionId==10&&IconId==124&&UseType==38&&Flags==1&&NormalPotionName("$23");
    public bool SupportedHomeScroll=>DescriptionId==1338&&IconId==474&&UseType==9&&Flags==1&&NormalPotionName("$505");
    public bool SupportedBasicSupply=>SupportedMeat||SupportedHomeScroll;
    // Fixed etcitem: meat delay_id/time=0/0; ordinary home scroll=4/1500 ms.
    public int UseDelayMs=>SupportedHomeScroll?1500:SupportedMeat?0:SupportedHealingPotion?500:SupportedHastePotion?0:LoginConnection.ItemUseDelayMs;
    public bool SupportedPotion=>SupportedHealingPotion||DescriptionId==130&&IconId==81&&UseType==249&&Charges==0&&Bless<128&&(Flags&32)==0&&(Name=="$238"||Name.StartsWith("$238 (",StringComparison.Ordinal));
    // The server routes these packet use-types using only the owned object ID.
    // Names, enchantment, icons, identification, blessing and class eligibility do not alter that wire shape.
    public bool SupportedEquipment=>ObjectId!=0&&Count>0&&EquipmentRules.IsEquipment(UseType);
    public bool SupportedArrow=>DescriptionId==0&&UseType==254&&IconId==7&&Bless==1&&Charges==0&&(Name=="$61"||Name.StartsWith("$61 (",StringComparison.Ordinal));
    public bool SupportedUse=>SupportedBasicSupply||SupportedPotion||SupportedHastePotion||SupportedEquipment;
    public string UseAction=>SupportedEquipment?(Equipped==true?"卸下":"穿上"):"單次使用";
    public string UseState=>Count==0?"已耗盡":SupportedMeat?"吃一個肉；飽食高時仍會消耗，數量與飽食以原服回報為準":SupportedHomeScroll?"用一張回家卷軸；回城落點由原服決定，部分地圖禁止使用":SupportedEquipment?(Equipped==true?"已裝備；可單次卸下":"未裝備；可單次穿上"):SupportedArrow?"有限箭矢；原服射擊消耗，數量以回包為準":SupportedHastePotion?"可單次使用；消耗一瓶，約 5 分鐘加速，以伺服器回報為準":SupportedPotion?"可單次使用；滿血仍會消耗":"此道具的使用尚未支援";
}
public sealed partial class WorldSession {
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,InventoryItem> inventory=new();
    public InventoryItem[] Inventory=>inventory.Values.OrderBy(i=>i.ObjectId).ToArray();
    public uint? SelectedItemId {get;private set;}
    public InventoryItem? SelectedItem=>SelectedItemId is uint id&&inventory.TryGetValue(id,out var i)?i:null;
    public int InventoryRevision {get;private set;}
    public int ItemSelectionRevision {get;private set;}
    public int UnknownItemUpdates {get;private set;}
    public bool InventoryLoaded {get;private set;}
    public bool EmptyInventoryFromWorldSync {get;private set;}
    public string ItemNotice {get;internal set;}="等待原服背包";
    public long ItemEffectNumber {get;private set;}
    public object? LastItemEffect {get;private set;}
    public string? LastServerMessage {get;private set;}
    public ServerMessageReport? LastServerReply {get;private set;}
    public string LastAnnouncement {get;private set;}="";
    void ResetInventory(){ResetBasicSupply();inventory.Clear();SelectedItemId=null;InventoryLoaded=false;EmptyInventoryFromWorldSync=false;InventoryRevision++;ItemSelectionRevision++;UnknownItemUpdates=0;ItemEffectNumber=0;LastItemEffect=null;LastServerMessage=null;LastServerReply=null;LastAnnouncement="";ItemNotice="等待原服背包";}
    void CompleteInitialInventory(){if(!InventoryLoaded&&inventory.IsEmpty){InventoryLoaded=true;EmptyInventoryFromWorldSync=true;InventoryRevision++;ItemNotice="原服世界已同步；目前背包空白（原服省略空清單）";}}
    public bool SelectItem(uint? id){if(id!=null&&!inventory.ContainsKey(id.Value))return false;SelectedItemId=id;ItemSelectionRevision++;InventoryRevision++;return true;}
    static string Bytes(LoginReader r,int n)=>Convert.ToHexString(Enumerable.Range(0,n).Select(_=>r.C()).ToArray());
    static InventoryItem ReadItem(LoginReader r){
        uint id=r.D();ushort desc=r.H();byte use=r.C(),charge=r.C();ushort icon=r.H();byte bless=r.C();uint count=r.D();byte flags=r.C();string name=r.S(),status=Bytes(r,r.C()),tail=Bytes(r,11);
        if(id==0||count>2000000000)throw new ProtocolException("invalid inventory identity/count");
        return new(id,desc,use,charge,icon,bless,count,flags,name,status,tail);
    }
    void RemoveItem(uint id){inventory.TryRemove(id,out _);if(SelectedItemId==id)SelectItem(null);}
    void UpdateItem(uint id,Func<InventoryItem,InventoryItem> change){if(inventory.TryGetValue(id,out var old)){var item=change(old);if(item.Count>2000000000)throw new ProtocolException("invalid item count");if(item.Count==0)RemoveItem(id);else inventory[id]=item;}else UnknownItemUpdates++;}
    bool ReceiveInventory(byte opcode,LoginReader r,byte[] plain){
        bool record=InventoryLoaded;var before=record?Inventory:[];bool statusOnly=false;
        switch(opcode){
        case 5:
            int count=r.C();var list=new Dictionary<uint,InventoryItem>();for(int n=0;n<count;n++){var i=ReadItem(r);if(!list.TryAdd(i.ObjectId,i))throw new ProtocolException("duplicate inventory ID in list");}r.Finish();
            inventory.Clear();foreach(var i in list.Values)if(i.Count>0)inventory[i.ObjectId]=i;SelectItem(null);InventoryLoaded=true;EmptyInventoryFromWorldSync=false;ItemNotice="原服背包已同步";break;
        case 15:var added=ReadItem(r);r.Finish();if(added.Count>0)inventory[added.ObjectId]=added;else RemoveItem(added.ObjectId);break;
        case 24:
            uint id=r.D();string name=r.S();uint amount=r.D();string status=Bytes(r,r.C());r.Finish();UpdateItem(id,i=>i with{Name=name,Count=amount,StatusHex=status});ItemNotice="收到原服數量／狀態更新";break;
        case 242:
            if(plain.Length<2||plain[1]!=1)return false;r.C();uint longId=r.D();string longName=r.S();uint longCount=r.D();string longStatus=Bytes(r,r.H());r.Finish();UpdateItem(longId,i=>i with{Name=longName,Count=longCount,StatusHex=longStatus});break;
        case 57:uint removed=r.D();r.Finish();RemoveItem(removed);ItemNotice="原服已移除道具";break;
        case 100:uint named=r.D();string updatedName=r.S();r.Finish();UpdateItem(named,i=>i with{Name=updatedName});break;
        case 240:uint colored=r.D();byte color=r.C();r.Finish();UpdateItem(colored,i=>i with{Bless=color});break;
        case 55:
            if(plain.Length!=8)return false;uint actor=r.D();ushort effect=r.H();r.Finish();LastItemEffect=new{number=++ItemEffectNumber,actor,effect};break;
        case 71:
            ushort message=r.H();int args=r.C();var values=new string[args];for(int n=0;n<args;n++)values[n]=r.S();r.Finish();BonusStatsServerMessage(message,values);LastServerReply=ServerMessageText.Decode(message,values);LastServerMessage=LastServerReply.Text;PrivateShopMessage();statusOnly=ServerStatusMessages.IsInformational(message,values);if(!statusOnly)TradeServerMessage(message,values);break;
        case 161:
            // Fixed d287 S_ServerMessage(String): C161 C0 D0 S(text).
            // Other channels and nonzero speakers are chat, not an NPC/service result.
            if(plain.Length<6||plain[1]!=0||BitConverter.ToUInt32(plain,2)!=0)return false;
            r.C();r.D();string directText=r.S();r.Finish();if(directText.Length==0)return true;
            if(SystemAnnouncements.TryClassify(directText,out string directAnnouncement)){LastAnnouncement=directAnnouncement;return true;}
            LastServerReply=null;LastServerMessage=directText;PrivateShopMessage();statusOnly=ServerStatusMessages.IsInformational(directText);break;
        case 243:
            if(plain.Length<2||plain[1]!=9)return false;r.C();string systemText=r.S();r.Finish();
            // Keep source-proven broadcasts separate from personal status text.
            if(SystemAnnouncements.TryClassify(systemText,out string announcement)){LastAnnouncement=announcement;return true;}
            LastServerReply=null;LastServerMessage=systemText;statusOnly=ServerStatusMessages.IsInformational(systemText);break;
        default:return false;
        }
        if(opcode==161){
            // This envelope also carries unsolicited server text. Display it without
            // acknowledging equipment or player trade. Only the fixed inn requirement
            // denial (npc_action_teleport.deny_message) releases a waiting NPC context.
            if(LastServerMessage=="請先購買此旅館的房間或會議廳鑰匙。"&&InteractionKind=="waiting")InteractionMessage();
            else CheckInteraction();
            CheckTrade();
        }else if(opcode is 71 or 243){
            // Status text is displayed, but does not acknowledge another operation.
            // Range/target/world guards still invalidate stale pending contexts.
            if(statusOnly){CheckInteraction();CheckTrade();}
            else{EquipmentMessageReceived();InteractionMessage();if(opcode==243)TradeServerMessage();}
        }else InteractionInventoryUpdate(opcode);PrivateShopInventoryChanged(opcode);if(record)RecordInventoryChanges(opcode,before);InventoryRevision++;return true;
    }
    public object InventorySnapshot()=>new{loaded=InventoryLoaded,emptyInventoryFromWorldSync=EmptyInventoryFromWorldSync,revision=InventoryRevision,items=Inventory,selectedId=SelectedItemId,selectionRevision=ItemSelectionRevision,unknownUpdates=UnknownItemUpdates,notice=ItemNotice,effectNumber=ItemEffectNumber,lastEffect=LastItemEffect,serverMessage=LastServerMessage,serverReply=LastServerReply};
}
public sealed partial class LoginConnection {
    // Original etcitem 40012 delay_id=2, delay_time=1000ms; one explicit command, no queue.
    public const int ItemUseDelayMs=1000;
    // Conservative local guard only: TickCount64 granularity and arrival/runtime fences differ.
    public const int ItemUseMarginMs=100;
    long nextItemAt;bool itemSending;int itemLease;
    public bool ItemBusy=>itemSending||Environment.TickCount64<nextItemAt;
    public event Action<object>? ItemSent;
    public bool ItemAvailable=>State==LoginState.InWorld&&!World.ActionLocked&&!World.TradeBlocksActions&&World.Ready&&!World.OwnDead&&World.Status?.Hp>0&&World.Actor is {} actor&&NormalSpeed.Supported(actor.Speed,actor.Brave)&&World.SelectedItem is {Count:>0,SupportedUse:true} item&&(actor.Speed!=2||item.SupportedHastePotion);
    void ResetItemSend(){itemLease++;itemSending=false;nextItemAt=0;}
    // Explicit original-server negative test only; absent from UI/web commands.
    internal Task ProbeOriginalItemDelay(uint id)=>sendWorld!(LoginPackets.Pad([164,..BitConverter.GetBytes(id)]));
    public async Task<bool> UseSelectedItem(){
        if(World.BasicSupplyPending){World.ItemNotice=World.BasicSupplyNotice;return false;}
        if(!ItemAvailable||ItemBusy||MovementBusy||sendMovementGate==null){World.ItemNotice=SlowActionsBlocked&&World.SelectedItem?.SupportedHastePotion!=true?SlowActionNotice:"未送出：道具／速度未支援、忙碌或已耗盡";return false;}
        var item=World.SelectedItem!;var actor=World.Actor!;int delayMs=item.UseDelayMs;int epoch=generation,worldEpoch=World.WorldEpoch,selection=World.ItemSelectionRevision,input=inputVersion,speedRevision=World.SpeedRevision,equipmentRevision=World.EquipmentRevision,appearance=World.AppearanceRevision,correction=World.Corrections,lease=++itemLease;var sender=sendMovementGate;
        byte[] bytes=new byte[8];bytes[0]=164;BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1),item.ObjectId);itemSending=true;
        // Authoritative speed/count/selection replies may precede transport completion.
        // Completion ownership intentionally excludes those admission-only snapshots.
        bool CurrentOwner()=>epoch==generation&&worldEpoch==World.WorldEpoch&&World.Actor?.ObjectId==actor.ObjectId&&lease==itemLease;
        bool OtherActionsReady()=>!combatSending&&!movementSending&&!magicSending&&!groundSending&&!CombatBusy&&!MagicBusy&&!GroundBusy&&Environment.TickCount64>=MovementReadyAt&&Environment.TickCount64>=nextItemAt;
        try{
            bool sent=await sender(bytes,()=>CurrentOwner()&&selection==World.ItemSelectionRevision&&input==inputVersion&&speedRevision==World.SpeedRevision&&appearance==World.AppearanceRevision&&correction==World.Corrections&&World.Actor?.X==actor.X&&World.Actor.Y==actor.Y&&World.Actor.Gfx==actor.Gfx&&World.Actor.Weapon==actor.Weapon&&(!item.SupportedEquipment||equipmentRevision==World.EquipmentRevision)&&ItemAvailable&&World.SelectedItemId==item.ObjectId&&World.SelectedItem==item&&OtherActionsReady(),()=>{nextItemAt=Environment.TickCount64+delayMs+ItemUseMarginMs;World.MarkItemUseSent(item);Set(State,World.ItemNotice);});
            if(!CurrentOwner()||!sent)return false;
            nextItemAt=Math.Max(nextItemAt,Environment.TickCount64+delayMs+ItemUseMarginMs);ItemSent?.Invoke(new{utc=DateTimeOffset.UtcNow,monotonicMs=Environment.TickCount64,plainHex=Convert.ToHexString(bytes),worldEpoch,item.ObjectId,delayMs,localMarginMs=ItemUseMarginMs});return true;
        }catch(Exception ex){if(CurrentOwner()){Cancel();Set(LoginState.Faulted,"道具傳送失敗："+ex.Message);}return false;}
        finally{if(CurrentOwner())itemSending=false;}
    }
}
