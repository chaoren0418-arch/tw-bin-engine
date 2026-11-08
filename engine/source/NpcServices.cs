using System.Text.Json;
namespace P06;
public sealed record SkillTemplate(int Id,string Name,int Level);
public sealed class SkillCatalog {
    readonly Dictionary<int,SkillTemplate> entries;
    public SkillCatalog(string path){entries=JsonSerializer.Deserialize<SkillTemplate[]>(File.ReadAllText(path))!.ToDictionary(s=>s.Id);}
    public SkillTemplate? Get(int id)=>entries.GetValueOrDefault(id);
    // Pinned C_SkillBuyOK.PRICE: level^2, except level16 has zero (server rejects).
    public uint? Price(int id,uint basis){var s=Get(id);if(s==null||s.Level is <1 or >28||s.Level==16||basis==0)return null;long value=(long)basis*s.Level*s.Level;return value<=int.MaxValue?(uint)value:null;}
}
public static class ServicePackets {
    public static byte[] SkillList(uint npc)=>LoginPackets.Pad([145,..BitConverter.GetBytes(npc)]);
    public static byte[] Learn(IReadOnlyList<ShopChoice> choices){if(choices.Count is <1 or >255||choices.Any(c=>c.Id>255||c.Count!=1)||choices.Select(c=>c.Id).Distinct().Count()!=choices.Count)throw new ArgumentException("skills");using var s=new MemoryStream();using var w=new BinaryWriter(s);w.Write((byte)39);w.Write((ushort)choices.Count);foreach(var c in choices)w.Write(c.Id);return LoginPackets.Pad(s.ToArray());}
    public static byte[] Warehouse(uint npc,bool withdraw,IReadOnlyList<ShopChoice> choices){byte[] packet=NpcPackets.Shop(npc,false,choices);packet[5]=(byte)(withdraw?3:2);return packet;}
}
public sealed partial class WorldSession {
    public SkillCatalog? SkillCatalog {get;set;}
    string pendingNpcReply="";int[] pendingLearned=[];
    public bool IsServiceSelection=>InteractionKind is "skills" or "deposit" or "withdraw" or "sell";
    public bool IsInteractionSelection=>InteractionKind is "buy" or "sell" or "skills" or "deposit" or "withdraw";
    public string InteractionTitle=>InteractionKind switch{"skills"=>"學習魔法","deposit"=>"存入個人倉庫","withdraw"=>"領取個人倉庫","buy"=>"購買物品","sell"=>"出售物品",_=>"NPC 對話"};
    public string InteractionPriceLabel=>InteractionKind switch{"skills"=>"預計學費","deposit"=>"存入費用","withdraw"=>"此列領取費","sell"=>"收購單價",_=>"單價"};
    public bool CanSubmitService=>InteractionValid&&IsServiceSelection&&ShopRows.Any(r=>r.Available>0);
    InventoryItem[] npcSellItems=[];uint[] npcSellEquipped=[];long npcSellAt;
    uint[] SaleEquippedItems()=>Inventory.Where(i=>ItemEquipped(i)==true).Select(i=>i.ObjectId).ToArray();
    void ClearNpcService(){pendingNpcReply="";pendingLearned=[];npcSellItems=[];}
    internal void CheckNpcSale(){
        if(InteractionKind!="sell")return;
        if(!Inventory.SequenceEqual(npcSellItems)||!SaleEquippedItems().SequenceEqual(npcSellEquipped))
            CloseInteraction("背包或裝備已改變，出售清單失效；請重新與商人對話取得收購報價。");
        else if(InteractionClock()-npcSellAt>=30000)
            CloseInteraction("出售報價已逾時；請重新與商人對話取得收購清單。沒有送出或重送交易。");
    }
    // S65: D(NPC), H(rows), repeated D(owned object), D(assessed price).
    // Routed through the existing service hook; names/counts come only from inventory.
    bool ReceiveNpcSale(LoginReader r){
        uint npc=r.D();int count=r.H();if(npc==0||count>2048)throw new ProtocolException("NPC sell header");
        var rows=new List<ShopRow>();var ids=new HashSet<uint>();
        for(int n=0;n<count;n++){
            uint id=r.D(),price=r.D();if(id==0||price>int.MaxValue||!ids.Add(id))throw new ProtocolException("NPC sell row");
            var item=inventory.GetValueOrDefault(id);
            uint maximum=item==null||ItemEquipped(item)==true?0:Math.Min(item.Count,(uint)int.MaxValue);
            if(price>0)maximum=Math.Min(maximum,(uint)int.MaxValue/price);
            rows.Add(new(id,item?.IconId??0,price,item?.Name??"未持有物件 "+id,maximum,item?.StatusHex??""));
        }
        r.Finish();CheckInteraction();
        if(!InteractionValid||npc!=InteractionNpc||InteractionKind!="waiting"||pendingNpcReply!="")return true;
        npcSellItems=Inventory;npcSellEquipped=SaleEquippedItems();npcSellAt=InteractionClock();
        InteractionRevision++;InteractionKind="sell";InteractionPage=null;ShopRows=rows.ToArray();shopCurrency=7;
        InteractionNotice=count==0?"原服未列出可出售給此商人的物品。":
            "選擇出售數量（0 表示不出售），再提交一次。收購單價以原服清單為準；物品與金幣等待原服更新，背包或裝備改變後須重新查詢。";
        return true;
    }
    // NpcInteraction 在送 C125 之前先問這裡：本機處理的對話動作（倉庫存入、M7-S 修理 fix／武器選項）回 true，不送 C125。
    internal bool OpenDeposit(int option,int revision){
        CheckInteraction();if(!InteractionValid||revision!=InteractionRevision||InteractionKind!="dialog")return false;
        var selected=InteractionPage?.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind=="action");
        if(selected!=null&&HandleRepairOption(selected.Value))return true;
        if(!InventoryLoaded||selected?.Value!="deposit")return false;
        ShopRows=Inventory.Where(i=>i.Count>0&&i.Equipped!=true).Select(i=>new ShopRow(i.ObjectId,i.IconId,0,i.Name,i.Count,i.StatusHex)).ToArray();InteractionRevision++;InteractionPage=null;InteractionKind="deposit";pendingNpcReply="";InteractionNotice="選擇目前背包物品存入個人倉庫。存入不收費；原服仍會檢查綁定、重量與可存入條件。";return true;
    }
    internal void AwaitNpcService(string kind,IReadOnlyList<ShopChoice>? choices=null){
        AwaitInteraction(kind is "skills" or "deposit" or "withdraw" or "sell");pendingNpcReply=kind;
        if(kind=="skill-list"){InteractionKind="waiting";InteractionNotice="已要求可學技能清單，等待原服回包。";}
        else if(kind=="withdraw-list"){InteractionKind="waiting";InteractionNotice="已要求個人倉庫清單，等待原服回包。";}
        else if(kind=="skills"){pendingLearned=choices!.Select(c=>(int)c.Id+1).ToArray();InteractionNotice="學習請求已送出，等待已學技能與金額回包；未自行授予技能。";}
        else if(kind=="deposit"||kind=="withdraw")InteractionNotice=(kind=="deposit"?"存入":"領取")+"請求已送出，尚未確認完成。請核對背包，重新開啟倉庫查看原服清單；不會自動重送。";
        else if(kind=="sell")InteractionNotice="出售請求已送出一次；物品與金幣尚未確認，僅依原服背包回包更新。再次出售須重新對話，沒有自動重送。";
    }
    internal void InteractionSkillsUpdated(){if(InteractionKind!="submitted"||pendingNpcReply!="skills")return;var learned=LearnedMagic.Select(s=>s.Id).ToHashSet();int received=pendingLearned.Count(learned.Contains);InteractionNotice=$"原服已學技能回報：本次選取已出現 {received}/{pendingLearned.Length} 項；實際扣款請核對背包金額。";InteractionRevision++;}
    public bool ValidateService(IReadOnlyList<ShopChoice> choices){
        CheckNpcSale();
        if(!CanSubmitService||choices.Count is <1 or >255||choices.Select(c=>c.Id).Distinct().Count()!=choices.Count)return false;long total=0;
        foreach(var choice in choices){var row=ShopRows.FirstOrDefault(r=>r.Id==choice.Id);if(row==null||choice.Count is 0 or >int.MaxValue||choice.Count>row.Available)return false;
            if(InteractionKind=="skills"&&(choice.Count!=1||LearnedMagic.Any(s=>s.Id==choice.Id+1)))return false;
            if(InteractionKind=="deposit"&&(!inventory.TryGetValue(choice.Id,out var item)||item.Count<choice.Count||item.Equipped==true))return false;
            if(InteractionKind=="sell"&&(!inventory.TryGetValue(choice.Id,out var held)||held.Count<choice.Count||ItemEquipped(held)==true))return false;
            total+=InteractionKind=="withdraw"?row.Price:(long)row.Price*choice.Count;if(total>int.MaxValue)return false;
        }return true;
    }
    bool ReceiveNpcService(byte opcode,LoginReader r){
        if(opcode==65)return ReceiveNpcSale(r);
        if(opcode==41){uint basis=r.D();int n=r.H();if(n>256||basis>int.MaxValue)throw new ProtocolException("skill list");var rows=new List<ShopRow>();var ids=new HashSet<uint>();for(int i=0;i<n;i++){uint index=r.D();if(index>=int.MaxValue||!ids.Add(index))throw new ProtocolException("skill identity");int id=(int)index+1;var template=SkillCatalog?.Get(id);uint? price=SkillCatalog?.Price(id,basis);rows.Add(new(index,0,price??0,template?.Name??$"技能 {id}（缺少名稱與價格依據）",price.HasValue&&id<=256&&!LearnedMagic.Any(s=>s.Id==id)?1u:0u,""));}r.Finish();CheckInteraction();if(!InteractionValid||pendingNpcReply!="skill-list"||InteractionKind!="waiting")return true;InteractionRevision++;InteractionPage=null;ShopRows=rows.ToArray();InteractionKind="skills";pendingNpcReply="";InteractionNotice=n==0?"原服目前沒有可學技能。":$"回包基價 {basis:N0} 金幣；預計學費依已核定技能等級表計算。選取數量 1 表示學習，送出後等待原服已學清單；未知價格者停用。";return true;}
        if(opcode==176){uint npc=r.D();int n=r.H();byte type=r.C();if(n>2048)throw new ProtocolException("warehouse list");var rows=new List<ShopRow>();var ids=new HashSet<uint>();for(int i=0;i<n;i++){uint id=r.D();r.C();ushort icon=r.H();r.C();uint count=r.D();r.C();string name=r.S();if(id==0||!ids.Add(id)||count>2000000000)throw new ProtocolException("warehouse item");rows.Add(new(id,icon,0,name,count,""));}uint fee=r.D();r.Finish();if(fee>int.MaxValue)throw new ProtocolException("warehouse fee");CheckInteraction();if(!InteractionValid||npc!=InteractionNpc||pendingNpcReply!="withdraw-list"||InteractionKind!="waiting")return true;if(type!=3){CloseInteraction("此倉庫類型尚未支援；沒有送出領取操作。");return true;}InteractionRevision++;InteractionPage=null;ShopRows=rows.Select(row=>row with{Price=fee}).ToArray();InteractionKind="withdraw";pendingNpcReply="";InteractionNotice=n==0?"原服個人倉庫清單為空，或本次無法領取。":$"原服領取費每個選取列 {fee:N0} 金幣（不乘堆疊數量）。物品與數量以此次倉庫回包為準。";return true;}
        return false;
    }
}
