using System.Text;

namespace P06;

public enum ItemUseKind { None, Inventory, Object, ObjectLocation, Location, Name, Teleport, Cooking, Spell, Letter }

// Arguments describe player input. The wire layout always comes from the owned inventory item.
public abstract record ItemUseArguments {
    public sealed record InventoryTarget(uint ObjectId) : ItemUseArguments;
    public sealed record WorldTarget(uint ObjectId, int X, int Y) : ItemUseArguments;
    public sealed record Location(int X, int Y) : ItemUseArguments;
    public sealed record Name(string Value) : ItemUseArguments;
    public sealed record Teleport(int MapId, uint BookmarkId) : ItemUseArguments;
    public sealed record Cooking(int Mode, int Recipe) : ItemUseArguments;
    public sealed record Spell(int Index) : ItemUseArguments;
    public sealed record Letter(int Code, string Receiver, string Subject, string Body) : ItemUseArguments;
}

public static class ItemUseCodec {
    public static ItemUseKind Kind(byte useType) => useType switch {
        7 or 14 or 26 or 27 or 46 or 55 => ItemUseKind.Inventory,
        8 or 30 => ItemUseKind.Object,
        5 or 17 or 39 => ItemUseKind.ObjectLocation,
        42 => ItemUseKind.Location,
        16 => ItemUseKind.Name,
        6 or 29 => ItemUseKind.Teleport,
        248 => ItemUseKind.Cooking, // signed use_type -8 on the server
        28 => ItemUseKind.Spell,
        12 or 31 or 33 or 35 => ItemUseKind.Letter,
        _ => ItemUseKind.None
    };

    public static string Prompt(byte useType) => Kind(useType) switch {
        ItemUseKind.Inventory => "請選擇背包內的目標物品",
        ItemUseKind.Object or ItemUseKind.ObjectLocation => "請點選場景中的目標，再確認使用",
        ItemUseKind.Location => "請點選地面座標，再確認使用",
        ItemUseKind.Name => "請輸入變身名稱",
        ItemUseKind.Teleport => "請填寫傳送書籤 ID；0 表示隨機傳送",
        ItemUseKind.Cooking => "請選擇料理操作與食譜編號",
        ItemUseKind.Spell => "請輸入要記錄的魔法編號（從 1 起）",
        ItemUseKind.Letter => "請填寫收件人、主旨與本文",
        _ => "此道具的使用尚未支援"
    };

    static ushort Word(int value) => checked((ushort)value);
    static uint Target(uint value) => value != 0 ? value : throw new ArgumentException("請選擇有效目標");
    static byte[] Text(string value, int maxBytes) {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new ArgumentException("名稱不可空白或含控制字元");
        byte[] bytes = LoginPackets.TextEncoding.GetBytes(value);
        if (bytes.Length > maxBytes) throw new ArgumentException("名稱過長");
        return bytes;
    }

    public static byte[] Encode(InventoryItem item, ItemUseArguments? arguments) {
        if (item.ObjectId == 0 || item.Count == 0 || !item.SupportedUse)
            throw new ArgumentException("道具已耗盡或使用尚未支援");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)164);
        writer.Write(item.ObjectId);
        switch (Kind(item.UseType), arguments) {
            case (ItemUseKind.None, null): break;
            case (ItemUseKind.Inventory, ItemUseArguments.InventoryTarget target):
                writer.Write(Target(target.ObjectId)); break;
            case (ItemUseKind.Object, ItemUseArguments.WorldTarget target):
                writer.Write(Target(target.ObjectId)); break;
            case (ItemUseKind.ObjectLocation, ItemUseArguments.WorldTarget target):
                writer.Write(Target(target.ObjectId)); writer.Write(Word(target.X)); writer.Write(Word(target.Y)); break;
            case (ItemUseKind.Location, ItemUseArguments.Location location):
                writer.Write(Word(location.X)); writer.Write(Word(location.Y)); break;
            case (ItemUseKind.Name, ItemUseArguments.Name name):
                writer.Write(Text(name.Value, 200)); writer.Write((byte)0); break;
            case (ItemUseKind.Teleport, ItemUseArguments.Teleport teleport):
                writer.Write(Word(teleport.MapId)); writer.Write(teleport.BookmarkId); break;
            case (ItemUseKind.Cooking, ItemUseArguments.Cooking cooking):
                if (cooking.Mode is < 0 or > 1) throw new ArgumentException("料理操作無效");
                writer.Write((byte)cooking.Mode); writer.Write(checked((byte)cooking.Recipe)); break;
            case (ItemUseKind.Spell, ItemUseArguments.Spell spell):
                writer.Write(checked((byte)spell.Index)); break;
            case (ItemUseKind.Letter, ItemUseArguments.Letter letter):
                writer.Write(Word(letter.Code)); writer.Write(Text(letter.Receiver, 200)); writer.Write((byte)0);
                // Native letter content is two UTF-16LE fields, each terminated by a UTF-16 NUL.
                if (letter.Subject.Length > 200 || letter.Body.Length > 2000 ||
                    letter.Subject.Contains('\0') || letter.Body.Contains('\0'))
                    throw new ArgumentException("信件過長或含空字元");
                var unicode = new UnicodeEncoding(false, false, true);
                writer.Write(unicode.GetBytes(letter.Subject + "\0" + letter.Body + "\0")); break;
            default: throw new ArgumentException("道具使用參數與類型不符");
        }
        return LoginPackets.Pad(stream.ToArray());
    }
}

public sealed record ItemUseRequest(InventoryItem Item, int Generation, int WorldEpoch, ushort? MapId,
    uint ActorId, int SelectionRevision, int InventoryRevision, int Corrections, ushort X, ushort Y);

public sealed partial class LoginConnection {
    public ItemUseRequest? PendingItemUse { get; private set; }
    public event Action<ItemUseRequest>? ItemArgumentsRequested;

    public bool CurrentItemUse(ItemUseRequest request) => ReferenceEquals(PendingItemUse, request) &&
        State == LoginState.InWorld && generation == request.Generation && World.WorldEpoch == request.WorldEpoch &&
        World.MapId == request.MapId && World.Actor is {} actor && actor.ObjectId == request.ActorId &&
        actor.X == request.X && actor.Y == request.Y && World.Corrections == request.Corrections &&
        World.ItemSelectionRevision == request.SelectionRevision && World.InventoryContentRevision == request.InventoryRevision &&
        World.SelectedItem == request.Item && ItemAvailable;

    public void CancelItemUse() { PendingItemUse = null; }

    bool RequestItemArguments(InventoryItem item) {
        if (ItemUseCodec.Kind(item.UseType) == ItemUseKind.None) return false;
        if (PendingItemUse is {} pending && CurrentItemUse(pending)) return true;
        var actor = World.Actor!;
        var request = new ItemUseRequest(item, generation, World.WorldEpoch, World.MapId, actor.ObjectId,
            World.ItemSelectionRevision, World.InventoryContentRevision, World.Corrections, actor.X, actor.Y);
        PendingItemUse = request;
        World.ItemNotice = ItemUseCodec.Prompt(item.UseType);
        ItemArgumentsRequested?.Invoke(request);
        return true;
    }

    public Task<bool> UseItemWithArguments(ItemUseRequest request, ItemUseArguments arguments) =>
        UseSelectedItemCore(request, arguments);

    bool TryItemArguments(InventoryItem item, ItemUseRequest? request, ItemUseArguments? arguments,
        out byte[] bytes, out Func<bool> currentTarget) {
        bytes = [];
        currentTarget = () => true;
        if (request != null && !CurrentItemUse(request)) {
            World.ItemNotice = "選取內容已失效，請重新使用道具";
            return false;
        }
        switch (arguments) {
            case ItemUseArguments.InventoryTarget target:
                var owned = World.Inventory.FirstOrDefault(i => i.ObjectId == target.ObjectId && i.Count > 0);
                if (owned == null) { World.ItemNotice = "目標物品已離開背包"; return false; }
                currentTarget = () => World.Inventory.Contains(owned);
                break;
            case ItemUseArguments.WorldTarget target:
                var worldTarget = World.Objects.FirstOrDefault(o => o.ObjectId == target.ObjectId);
                bool self = World.Actor?.ObjectId == target.ObjectId;
                bool Matches() => self
                    ? World.Actor is {} actor && actor.ObjectId == target.ObjectId && actor.X == target.X && actor.Y == target.Y
                    : worldTarget is { AppearancePending: false } && worldTarget.X == target.X && worldTarget.Y == target.Y &&
                        World.Objects.Any(o => ReferenceEquals(o, worldTarget));
                if (!Matches()) { World.ItemNotice = "目標已離開或移動，請重新選取"; return false; }
                currentTarget = Matches;
                break;
            case ItemUseArguments.Teleport teleport when teleport.MapId != World.MapId:
                World.ItemNotice = "所在場景已改變，請重新使用道具"; return false;
        }
        try { bytes = ItemUseCodec.Encode(item, arguments); return true; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException) {
            World.ItemNotice = "未送出道具使用請求，" + ex.Message;
            return false;
        }
    }
}
