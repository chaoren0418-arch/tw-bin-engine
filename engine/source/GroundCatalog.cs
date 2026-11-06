using System.Globalization;
using System.Text.Json;
namespace P06;

// S15 has no template ID: the frozen rows remain a conservative drop policy.
// S87 appearance lookup is independent of that policy and never invents a template.
public sealed record GroundSprite(string Entry,int IndexRecord,long Offset,int Size,string PayloadSha256,int Frame,int HeaderOffset,int[] Bounds,string RgbaSha256,string Source);
public sealed record GroundSource(string Commit,string Path,string ContractSha256,string Location,string RowSha256);
public sealed record GroundDefinition(string Id,int TemplateId,string NameToken,ushort GroundGfx,ushort InventoryIcon,ushort InventoryDesc,byte WireUseType,bool Stackable,GroundSource? Source,GroundSprite? Sprite,int[] InventoryBlesses) {
    public string CountName(uint count)=>count>1?NameToken+" ("+count.ToString(CultureInfo.InvariantCulture)+")":NameToken;
}
public static class GroundCatalog {
    public static readonly IReadOnlyList<GroundDefinition> Rows;
    public static readonly string IndexSha256;
    static GroundCatalog(){
        using var stream=typeof(GroundCatalog).Assembly.GetManifestResourceStream("P06.Resources.GroundCatalog.json")!;
        using var doc=JsonDocument.Parse(stream);var root=doc.RootElement;
        if(root.S("schema")!="ground-catalog-v1")throw new InvalidDataException("Ground catalog schema");
        IndexSha256=root.S("indexSha256");
        Rows=Array.AsReadOnly(JsonSerializer.Deserialize<GroundDefinition[]>(root.GetProperty("rows"),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!);
        if(Rows.Select(r=>r.GroundGfx).Distinct().Count()!=Rows.Count||Rows.Any(r=>r.Source==null||r.Source.RowSha256.Length!=64||r.Source.ContractSha256.Length!=64||r.TemplateId<=0||r.Sprite is {} s&&(s.PayloadSha256.Length!=64||s.RgbaSha256.Length!=64||s.Frame!=0||s.Bounds.Length!=4)))throw new InvalidDataException("Incomplete ground evidence");
    }
    // A descriptor is not proof that a local SPR exists; Read validates the original archive.
    public static GroundDefinition? Find(int gfx)=>gfx is <0 or >ushort.MaxValue?null:
        Rows.SingleOrDefault(r=>r.GroundGfx==gfx)??new("s87-gfx-"+gfx,0,"",(ushort)gfx,0,0,0,false,null,null,[]);
    public static GroundDefinition? Match(InventoryItem i)=>Rows.SingleOrDefault(r=>r.Sprite!=null&&i.DescriptionId==r.InventoryDesc&&i.IconId==r.InventoryIcon&&i.UseType==r.WireUseType&&i.Name==r.CountName(i.Count));
    public static bool CanDrop(InventoryItem i)=>i.ObjectId!=0&&i.Count>0&&i.Charges==0&&i.Equipped!=true&&!EquipmentRules.IsEquipment(i.UseType)&&i.Bless<128&&(i.Flags&~1)==0&&Match(i) is {} r&&r.InventoryBlesses.Contains(i.Bless)&&(r.Stackable||i.Count==1);
    public static bool TryCount(string? text,uint available,out uint count){
        count=0;return !string.IsNullOrEmpty(text)&&text.All(c=>c is >= '0' and <= '9')&&uint.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out count)&&count>0&&count<=int.MaxValue&&count<=available;
    }
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
}
