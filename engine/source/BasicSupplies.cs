namespace P06;
public sealed partial class WorldSession {
    // Only ordinary meat (40056) and ordinary home scroll (40079), matched by
    // the full wire identity in InventoryItem. No template IDs or destination sent.
    InventoryItem? pendingBasicSupply;int basicSupplyEpoch;long basicSupplyAt;
    public bool BasicSupplyPending=>pendingBasicSupply!=null;
    public string BasicSupplyNotice=>pendingBasicSupply==null?"":SceneClock()-basicSupplyAt>=15000?
        "補給品回報尚未完整；操作保持鎖定，可取消連線後重新登入核對背包。沒有自動重送。":
        pendingBasicSupply.SupportedHomeScroll?"回家卷軸請求已送出；等待原服傳送或解除鎖定。":"吃肉請求已送出；等待原服背包與飽食回報。";
    // Values are raw server scales, never a locally predicted hunger or percentage.
    public string SustenanceText=>Status is {} s?$"飽食 {s.Food}/255 · 負重 {s.Weight}/240（原服刻度）"+((s.Food<3||s.Weight>=120)?" · 過餓／過重可能停止自然恢復":""):"等待原服飽食／負重";
    public bool CurrentItemSelection(uint id,int epoch,int selection)=>epoch==WorldEpoch&&selection==ItemSelectionRevision&&SelectedItemId==id&&SelectedItem!=null;
    void ResetBasicSupply(){pendingBasicSupply=null;basicSupplyAt=0;basicSupplyEpoch=0;}
    bool MarkBasicSupplySent(InventoryItem item){
        if(!item.SupportedBasicSupply)return false;
        pendingBasicSupply=item;basicSupplyEpoch=WorldEpoch;basicSupplyAt=SceneClock();ItemNotice=BasicSupplyNotice;return true;
    }
    void FinishBasicSupply(string notice){ResetBasicSupply();SelectItem(null);ItemNotice=notice;}
    void CheckBasicSupplyTimeout(){if(BasicSupplyPending)ItemNotice=BasicSupplyNotice;}
    void BasicSupplyReply(byte opcode,byte[] plain){
        if(pendingBasicSupply is not {} item)return;
        if(basicSupplyEpoch!=WorldEpoch){FinishBasicSupply("世界已更新；已清除補給品舊選取，位置與背包以原服回報為準。");return;}
        if(OwnDead||Status?.Hp==0){FinishBasicSupply("角色已倒下；已清除補給品選取，請核對原服背包。");return;}
        // The shared C_ItemUSe rejection path also unlocks food requests.
        // S202/7 acknowledges unlocking, not consumption or successful travel.
        // S149/S206 changes WorldEpoch and transfers protection to TeleportPending.
        if(opcode==202&&plain[1]==7){FinishBasicSupply("原服已解除操作鎖定；未確認使用成功，請核對位置與背包。");return;}
        if(item.SupportedMeat){
            if(opcode is 5 or 24 or 242 or 57 && (!inventory.TryGetValue(item.ObjectId,out var current)||current.Count<item.Count)){
                FinishBasicSupply("已收到原服背包數量更新；飽食以原服狀態為準。");return;
            }
            if(opcode==71&&LastServerReply is {Id:698,Arguments.Length:0}){
                FinishBasicSupply("原服回覆目前無法飲食；請核對訊息與背包。");return;
            }
        }
        ItemNotice=BasicSupplyNotice;
    }
    bool ReceiveSustenance(byte opcode,LoginReader r,byte[] plain){
        // Fixed S_PacketBox(int,int): C250 C10/11 Cvalue, zero padding only.
        if(opcode!=250||plain.Length<2||plain[1] is not (10 or 11))return false;
        byte kind=r.C(),value=r.C();r.Finish();
        if(Status==null)return true; // Initial full S8 supplies identity and both values.
        Status=kind==11?Status with{Food=value}:Status with{Weight=value};return true;
    }
}
