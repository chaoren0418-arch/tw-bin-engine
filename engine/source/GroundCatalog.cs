using System.Globalization;
using System.Text.Json;
namespace P06;

// S87/S15 do not carry template IDs. Match the entire verified wire envelope;
// this catalog is a finite server contract, not a native species classifier.
public sealed record GroundSprite(string Entry,int IndexRecord,long Offset,int Size,string PayloadSha256,int Frame,int HeaderOffset,int[] Bounds,string RgbaSha256,string Source);
public sealed record GroundSource(string Commit,string Path,string ContractSha256,string Location,string RowSha256);
public sealed record GroundDefinition(string Id,int TemplateId,string NameToken,ushort GroundGfx,ushort InventoryIcon,ushort InventoryDesc,byte WireUseType,bool Stackable,GroundSource Source,GroundSprite? Sprite,int[] InventoryBlesses) {
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
        if(Rows.Select(r=>r.GroundGfx).Distinct().Count()!=Rows.Count||Rows.Any(r=>r.Source.RowSha256.Length!=64||r.Source.ContractSha256.Length!=64||r.TemplateId<=0||r.Sprite is {} s&&(s.PayloadSha256.Length!=64||s.RgbaSha256.Length!=64||s.Frame!=0||s.Bounds.Length!=4)))throw new InvalidDataException("Incomplete ground evidence");
    }
    public static GroundDefinition? Find(int gfx)=>Rows.SingleOrDefault(r=>r.GroundGfx==gfx&&r.Sprite!=null);
    public static GroundDefinition? Match(InventoryItem i)=>Rows.SingleOrDefault(r=>r.Sprite!=null&&i.DescriptionId==r.InventoryDesc&&i.IconId==r.InventoryIcon&&i.UseType==r.WireUseType&&i.Name==r.CountName(i.Count));
    public static bool CanDrop(InventoryItem i)=>i.ObjectId!=0&&i.Count>0&&i.Charges==0&&i.Equipped!=true&&!EquipmentRules.IsEquipment(i.UseType)&&i.Bless<128&&(i.Flags&~1)==0&&Match(i) is {} r&&r.InventoryBlesses.Contains(i.Bless)&&(r.Stackable||i.Count==1);
    public static bool TryCount(string? text,uint available,out uint count){
        count=0;return !string.IsNullOrEmpty(text)&&text.All(c=>c is >= '0' and <= '9')&&uint.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out count)&&count>0&&count<=int.MaxValue&&count<=available;
    }
}

// The UI sends the snapshot it displayed, not a fresh selection inferred at dispatch.
public sealed record GroundQuantitySelection(int Epoch,uint Id,int Selection,int Revision,uint Available);
public sealed partial class WorldSession {
    public bool CanDropGroundItem(InventoryItem item)=>GroundRules.Droppable(item)&&!EquipmentSlots.Any(s=>s.ObjectId==item.ObjectId)&&ItemEquipped(item)!=true;
    public GroundQuantitySelection? GroundQuantity(bool pickup)=>pickup
        ?SelectedGround is {} g?new(WorldEpoch,g.ObjectId,GroundSelectionRevision,GroundRevision,g.Count):null
        :SelectedItem is {} i?new(WorldEpoch,i.ObjectId,ItemSelectionRevision,InventoryRevision,i.Count):null;
    public bool CurrentGroundQuantity(bool pickup,GroundQuantitySelection? selection)=>selection!=null&&selection==GroundQuantity(pickup);
}
public sealed partial class LoginConnection {
    public Task<bool> SendGroundQuantity(bool pickup,string text,GroundQuantitySelection? selection){
        if(!World.CurrentGroundQuantity(pickup,selection)||!GroundCatalog.TryCount(text,selection!.Available,out uint count)){
            World.GroundNotice="未送出：請輸入 1 到持有量的整數；數量或選取變更後請重新選取";return Task.FromResult(false);
        }
        return SendGround(pickup,count,()=>World.CurrentGroundQuantity(pickup,selection));
    }
}
