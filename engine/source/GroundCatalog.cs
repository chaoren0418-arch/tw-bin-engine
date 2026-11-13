using System.Globalization;
namespace P06;

// T464 (#1049, asset-recipe.md §7 L2 = X): the 7 server-template rows (ground-catalog.json, taken
// from the original server's item templates in #49) are gone. S15/S87 carry no template ID and the client has no item
// template table, so whether an item may be dropped is decided by the server (R22); the client only
// keeps template-independent local conditions and asks for confirmation before every drop.
// What stays in code are content-free SPR pins measured from the client's Sprite.pak (#48): entry,
// idx record, PAK File Offset, size and SHA-256 of payload/RGBA. No pixels, no server template values.
public sealed record GroundSprite(string Entry,int IndexRecord,long Offset,int Size,string PayloadSha256,int Frame,int HeaderOffset,int[] Bounds,string RgbaSha256,string Source);
// S87 appearance descriptor: the gfx sent by the server plus an optional pin for that gfx.
public sealed record GroundDefinition(string Id,ushort GroundGfx,GroundSprite? Sprite);
public static class GroundCatalog {
    // #48 (docs/reports/issue-48.md): Sprite.idx records verified against the installed client.
    // Offset is a PAK File Offset; HeaderOffset is a File Offset inside the SPR payload.
    public static readonly IReadOnlyList<GroundDefinition> Pinned=Array.AsReadOnly(new GroundDefinition[]{
        new("s87-gfx-45",45,new("45-0.spr",62818,969626822L,174,"89ccae52b20c83ea26bdb9906f7c2c0bb1dde411bf3e59fd140fd39a01d69ca9",0,31,[20,4,28,14],"157926b43d9e7083c5e446df64a4d248fbf498de87935c97cced7d44bbdefc17","docs/reports/issue-48.md")),
        new("s87-gfx-36",36,new("36-0.spr",48732,971303715L,244,"1734424f17adb848324f8e6c2aba6ad4bafeb687f990aa59b62426d448899ee8",0,89,[18,5,29,14],"edcb31e9aca0869a8371a0aceb3fbf38b8dcecd221163a7178253d99587506c9","docs/reports/issue-48.md")),
        new("s87-gfx-7",7,new("7-0.spr",86493,962388973L,307,"65b754770b1915eb070e488ccb010ee2662741a09346f1d1be36fc3faadf2ce2",0,129,[18,5,32,15],"6eb4782f2c6c01d4e5c6b76494bf830e356175f026c64a627b423302e949786a","docs/reports/issue-48.md")),
        new("s87-gfx-66",66,new("66-0.spr",85892,962888499L,1056,"4795b0d2150d7d075d753efc378799b8d471e52d32cdce47eec77fa502bb758c",0,197,[3,-41,39,8],"9d2fabf6b13d9f0a3b8b062c3edf1e7edba6ba3b1cd023cc8fe7946ff1b6d9c6","docs/reports/issue-48.md")),
    });
    static GroundCatalog(){
        if(Pinned.Select(r=>r.GroundGfx).Distinct().Count()!=Pinned.Count||Pinned.Any(r=>r.Sprite is not {} s||s.Entry!=r.GroundGfx.ToString(CultureInfo.InvariantCulture)+"-0.spr"||s.PayloadSha256.Length!=64||s.RgbaSha256.Length!=64||s.Frame!=0||s.Bounds.Length!=4))
            throw new InvalidDataException("Incomplete ground sprite pin");
    }
    // A descriptor is not proof that a local SPR exists; GroundSpriteReader.Read validates the original archive.
    public static GroundDefinition? Find(int gfx)=>gfx is <0 or >ushort.MaxValue?null:
        Pinned.SingleOrDefault(r=>r.GroundGfx==gfx)??new("s87-gfx-"+gfx,(ushort)gfx,null);
    // Template-independent local conditions only (L2 = X). The server enforces template rules
    // (tradable, cant-delete, limited time, sealed, ...) and answers with S71 messages such as 125.
    public static bool CanDrop(InventoryItem i)=>i.ObjectId!=0&&i.Count>0&&i.Charges==0&&i.Equipped!=true&&!EquipmentRules.IsEquipment(i.UseType)&&i.Bless<128&&(i.Flags&~1)==0;
    public static bool TryCount(string? text,uint available,out uint count){
        count=0;return !string.IsNullOrEmpty(text)&&text.All(c=>c is >= '0' and <= '9')&&uint.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out count)&&count>0&&count<=int.MaxValue&&count<=available;
    }
    // Text of the confirmation shown before every drop (L2 = X keeps a confirmation for all items).
    public static string DropConfirmPrompt(string name,uint count)=>
        $"確定把「{name}」× {count.ToString(CultureInfo.InvariantCulture)} 丟到地上？\n能不能丟由伺服器決定；伺服器拒絕時會顯示原因。";
}

// The UI sends the snapshot it displayed, not a fresh selection inferred at dispatch.
public sealed record GroundQuantitySelection(int Epoch,uint Id,int Selection,int Revision,uint Available);
public sealed partial class WorldSession {
    internal void RejectGroundQuantity(bool pickup,string notice){
        GroundNotice=notice;LastServerMessage=null;LastServerReply=null;
        // Notices do not change ground contents or invalidate the displayed pickup snapshot.
        if(!pickup)ItemNotice=notice;InventoryRevision++;
    }
    public bool CanDropGroundItem(InventoryItem item)=>GroundRules.Droppable(item)&&!EquipmentSlots.Any(s=>s.ObjectId==item.ObjectId)&&ItemEquipped(item)!=true;
    public GroundQuantitySelection? GroundQuantity(bool pickup)=>pickup
        ?SelectedGround is {} g?new(WorldEpoch,g.ObjectId,GroundSelectionRevision,GroundRevision,g.Count):null
        :SelectedItem is {} i?new(WorldEpoch,i.ObjectId,ItemSelectionRevision,InventoryContentRevision,i.Count):null;
    public bool CurrentGroundQuantity(bool pickup,GroundQuantitySelection? selection)=>selection!=null&&selection==GroundQuantity(pickup);
}
public sealed partial class LoginConnection {
    public Task<bool> SendGroundQuantity(bool pickup,string text,GroundQuantitySelection? selection){
        if(selection==null){World.RejectGroundQuantity(pickup,"未送出：請先選取物品");return Task.FromResult(false);}
        if(!GroundCatalog.TryCount(text,selection.Available,out uint count)){
            World.RejectGroundQuantity(pickup,"未送出：數量不合法，請輸入 1 到持有量（最多 2147483647）的整數");return Task.FromResult(false);
        }
        if(!World.CurrentGroundQuantity(pickup,selection)){
            World.RejectGroundQuantity(pickup,"未送出：選取已過期，請重新選取物品");return Task.FromResult(false);
        }
        return SendGround(pickup,count,()=>World.CurrentGroundQuantity(pickup,selection));
    }
    // Drop confirmation (L2 = X). The UI asks only when the request would otherwise be sent;
    // invalid or stale input falls through to SendGroundQuantity's own notice, and a "No" sends nothing.
    public bool NeedsDropConfirm(string text,GroundQuantitySelection? selection,out InventoryItem? item,out uint count){
        item=null;count=0;
        if(selection==null||!GroundCatalog.TryCount(text,selection.Available,out count)||!World.CurrentGroundQuantity(false,selection))return false;
        item=World.SelectedItem;return item!=null&&World.CanDropGroundItem(item);
    }
    public void CancelDrop()=>World.RejectGroundQuantity(false,"未送出：已取消丟棄");
}
