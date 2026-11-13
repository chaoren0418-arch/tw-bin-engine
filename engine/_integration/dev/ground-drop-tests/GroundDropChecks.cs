using P06;
using System.Buffers.Binary;

// T464 (#1049, asset-recipe.md §7 L2 = X): drop is no longer limited by the 7 server-template rows.
// Synthetic S15 items (invented description/icon/name values), memory sender; no game, socket or assets.
partial class Checks {
    static void DropItem(LoginConnection c,uint id,ushort desc,byte use,ushort icon,byte bless,uint count,byte flags,string name,byte charges=0)=>Feed(c,Packet(b=>{
        b.Write((byte)15);b.Write(id);b.Write(desc);b.Write(use);b.Write(charges);b.Write(icon);b.Write(bless);b.Write(count);b.Write(flags);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);
    }));
    [CheckGroup("ground-drop"),CheckGroup("pickup",Order=200000)]
    static async Task GroundDropChecks(){
        // In-code SPR pins: content-free (entry, record, offset, size, SHA-256), no template fields.
        Check(GroundCatalog.Pinned.Select(p=>p.GroundGfx).OrderBy(g=>g).SequenceEqual(new ushort[]{7,36,45,66}),"四筆 SPR pin（gfx 7、36、45、66）");
        Check(GroundCatalog.Pinned.All(p=>p.Sprite is {} s&&s.Entry==p.GroundGfx+"-0.spr"&&s.PayloadSha256.Length==64&&s.RgbaSha256.Length==64&&s.Frame==0&&s.Bounds.Length==4),"pin 欄位完整");
        Check(GroundCatalog.Find(36)?.Sprite!=null&&GroundCatalog.Find(46) is {Sprite:null,GroundGfx:46}&&GroundCatalog.Find(9001) is {Sprite:null}&&GroundCatalog.Find(-1)==null&&GroundCatalog.Find(65536)==null,"沒有 pin 的 gfx 走通用描述；範圍外為 null");
        Check(typeof(GroundDefinition).GetProperties().Select(p=>p.Name).OrderBy(n=>n).SequenceEqual(new[]{"GroundGfx","Id","Sprite"}),"外觀描述不含模板欄位");

        // Template-independent local conditions only.
        InventoryItem Inv(byte use=0,byte bless=1,uint count=5,byte flags=0,byte charges=0,uint id=1)=>new(id,9003,use,charges,9002,bless,count,flags,"$9001","","");
        Check(GroundCatalog.CanDrop(Inv())&&GroundCatalog.CanDrop(Inv(count:1))&&GroundCatalog.CanDrop(Inv(bless:0))&&GroundCatalog.CanDrop(Inv(bless:2))&&GroundCatalog.CanDrop(Inv(flags:1)),"任何非裝備物品不受白名單限制");
        Check(GroundCatalog.CanDrop(Inv() with{DescriptionId=1,IconId=1,Name="anything"}),"說明編號、圖示、名稱都不再影響丟棄");
        foreach(var (bad,label) in new[]{(Inv(use:1),"武器"),(Inv(use:2),"防具"),(Inv(bless:128),"封印 bless"),(Inv(count:0),"數量 0"),(Inv(charges:3),"有充能數"),(Inv(flags:32),"旗標"),(Inv(id:0),"物件 ID 0")})
            Check(!GroundCatalog.CanDrop(bad),"本機仍擋："+label);

        // Confirmation is required for every drop that would be sent.
        Check(GroundCatalog.DropConfirmPrompt("合成物品",7).Contains("「合成物品」× 7")&&GroundCatalog.DropConfirmPrompt("合成物品",7).Contains("伺服器"),"確認文字含名稱、數量與伺服器裁定");
        using(var c=Seed()){
            DropItem(c,900201,9003,0,9002,1,5,0,"$9001 (5)");Check(c.World.SelectItem(900201),"選取合成物品");
            var selection=c.World.GroundQuantity(false);
            Check(c.World.CanDropGroundItem(c.World.SelectedItem!),"非白名單物品可送出丟棄");
            Check(c.NeedsDropConfirm("3",selection,out var item,out uint count)&&item?.ObjectId==900201&&count==3,"有效數量 → 需要確認");
            Check(!c.NeedsDropConfirm("0",selection,out _,out _)&&!c.NeedsDropConfirm("6",selection,out _,out _)&&!c.NeedsDropConfirm("3",null,out _,out _),"不合法數量或沒有選取 → 不問，交給既有訊息");
            int sends=0;Sender(c,(_,_,_)=>{sends++;return Task.FromResult(true);});
            c.CancelDrop();Check(sends==0&&c.World.ItemNotice.Contains("已取消丟棄")&&c.World.CurrentGroundQuantity(false,selection),"取消 → 不送、保留選取");
            DropItem(c,900201,9003,0,9002,1,4,0,"$9001 (4)");
            Check(!c.NeedsDropConfirm("3",selection,out _,out _),"選取過期 → 不問");
        }
        using(var c=Seed()){
            DropItem(c,900202,9013,0,9012,1,1,0,"$9011");c.World.SelectItem(900202);var selection=c.World.GroundQuantity(false);
            int sends=0;byte[]? sent=null;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();sends++;sent=b;return Task.FromResult(true);});
            Check(c.NeedsDropConfirm("1",selection,out _,out _)&&await c.SendGroundQuantity(false,"1",selection)&&sends==1,"確認後送出 C25");
            Check(sent![0]==25&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(5))==900202&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(9))==1,"C25 物件 ID 與數量");
            Check(c.World.SelectedItem!.Count==1,"送出不預扣");
            Feed(c,LoginPackets.Pad([71,125,0,0]));
            Check(c.World.LastServerReply?.Id==125&&c.World.SelectedItem!.Count==1,"伺服器拒絕（S71/125）沿用既有訊息顯示，背包不變");
        }
        using(var c=Seed()){
            DropItem(c,900203,9023,1,9022,1,1,0,"$9021");c.World.SelectItem(900203);
            Check(!c.World.CanDropGroundItem(c.World.SelectedItem!)&&!c.NeedsDropConfirm("1",c.World.GroundQuantity(false),out _,out _),"武器類不開確認也不送");
        }
    }
}
