using P06;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text.Json;

static class Checks {
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> passed = [];
    static void Check(bool condition, string label) {
        if (!condition) throw new Exception(label);
        passed.Add(label);
    }
    static void Set(object owner, string name, object? value) {
        var property = owner.GetType().GetProperty(name, Flags);
        if (property != null) property.SetValue(owner, value);
        else owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    }
    static InventoryItem Item(byte type, uint id = 0x11223344) => new(id, 0, type, 0, 0, 1, 2, 1, "測試道具", "", "");
    static byte[] Packet(Action<BinaryWriter> write) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        return LoginPackets.Pad(stream.ToArray());
    }
    static void Text(BinaryWriter writer, string value) { writer.Write(LoginPackets.TextEncoding.GetBytes(value)); writer.Write((byte)0); }
    static void Feed(LoginConnection connection, byte[] bytes) => connection.World.Receive(bytes, "offline");
    static void Add(LoginConnection connection, InventoryItem item) => Feed(connection, Packet(writer => {
        writer.Write((byte)15); writer.Write(item.ObjectId); writer.Write(item.DescriptionId); writer.Write(item.UseType);
        writer.Write(item.Charges); writer.Write(item.IconId); writer.Write(item.Bless); writer.Write(item.Count); writer.Write(item.Flags);
        Text(writer, item.Name); writer.Write((byte)0); writer.Write(new byte[11]);
    }));
    static byte[] ObjectPacket(uint id, ushort x = 32601, byte action = 0) => Packet(writer => {
        writer.Write((byte)87); writer.Write(x); writer.Write((ushort)32940); writer.Write(id); writer.Write((ushort)1110);
        writer.Write(action); writer.Write(new byte[3]); writer.Write(0u); writer.Write((ushort)0);
        Text(writer, "target"); Text(writer, ""); writer.Write((byte)0); writer.Write(0u); Text(writer, ""); Text(writer, "");
        writer.Write((byte)0); writer.Write((byte)100); writer.Write(new byte[3]); writer.Write((byte)255); writer.Write((byte)255);
    });
    static LoginConnection Seed(byte type = 26) {
        var connection = new LoginConnection();
        Set(connection, "State", LoginState.InWorld);
        Set(connection.World, "Entered", true); Set(connection.World, "MapId", (ushort)0); Set(connection.World, "TimeReplies", 1);
        Set(connection.World, "Status", JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(connection.World, "Actor", JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940}"));
        Add(connection, Item(type)); Add(connection, Item(1, 0x55667788));
        Feed(connection, ObjectPacket(456)); connection.World.SelectItem(0x11223344);
        Sender(connection, (bytes, valid, admit) => { if (!valid()) return Task.FromResult(false); admit(); return Task.FromResult(true); });
        return connection;
    }
    static void Sender(LoginConnection connection, Func<byte[], Func<bool>, Action, Task<bool>> sender) => Set(connection, "sendMovementGate", sender);
    static async Task<ItemUseRequest> Begin(LoginConnection connection) {
        int prompts = 0;
        connection.ItemArgumentsRequested += _ => prompts++;
        Check(!await connection.UseSelectedItem() && connection.PendingItemUse != null, "使用入口先要求參數");
        Check(!await connection.UseSelectedItem() && prompts == 1 && !connection.ItemBusy, "重複使用只保留一個選取流程");
        return connection.PendingItemUse!;
    }
    static void Vector(byte type, ItemUseArguments arguments, string suffix) {
        var expected = Convert.FromHexString("A444332211" + suffix);
        Array.Resize(ref expected, (expected.Length + 7) & ~7);
        Check(ItemUseCodec.Encode(Item(type), arguments).SequenceEqual(expected), $"use_type {type} 完整位元組與補齊");
    }
    static void Reject(Action action, string label) {
        try { action(); } catch (Exception ex) when (ex is ArgumentException or OverflowException) { Check(true, label); return; }
        throw new Exception(label);
    }
    static void Vectors() {
        foreach (byte type in new byte[] { 7, 14, 26, 27, 46, 55 })
            Vector(type, new ItemUseArguments.InventoryTarget(0x55667788), "88776655");
        foreach (byte type in new byte[] { 8, 30 })
            Vector(type, new ItemUseArguments.WorldTarget(0x55667788, 0x1234, 0x5678), "88776655");
        foreach (byte type in new byte[] { 5, 17, 39 })
            Vector(type, new ItemUseArguments.WorldTarget(0x55667788, 0x1234, 0x5678), "8877665534127856");
        foreach (byte type in new byte[] { 6, 29 })
            Vector(type, new ItemUseArguments.Teleport(0x1234, 0x55667788), "341288776655");
        Vector(42, new ItemUseArguments.Location(0x1234, 0x5678), "34127856");
        Vector(16, new ItemUseArguments.Name("測試"), "B4FAB8D500");
        Vector(248, new ItemUseArguments.Cooking(1, 7), "0107");
        Vector(28, new ItemUseArguments.Spell(255), "FF");
        foreach (byte type in new byte[] { 12, 31, 33, 35 })
            Vector(type, new ItemUseArguments.Letter(0x1234, "A", "主", "文"), "341241003B4E000087650000");
        var equipment = Item(1);
        Check(Convert.ToHexString(ItemUseCodec.Encode(equipment, null)) == "A444332211000000", "裝備既有無尾段格式");
        InventoryItem[] supplies = [
            new(0x11223344, 1342, 249, 0, 80, 1, 1, 1, "$26", "", ""),
            new(0x11223344, 17, 246, 0, 77, 1, 1, 1, "$234", "", ""),
            new(0x11223344, 10, 38, 0, 124, 1, 1, 1, "$23", "", ""),
            new(0x11223344, 1338, 9, 0, 474, 1, 1, 1, "$505", "", "")
        ];
        foreach (var supply in supplies)
            Check(Convert.ToHexString(ItemUseCodec.Encode(supply, null)) == "A444332211000000", "既有補給品格式 " + supply.UseType);
        foreach (byte type in new byte[] { 0, 4, 11, 41, 56, 96, 97, 255 })
            Reject(() => ItemUseCodec.Encode(Item(type), null), "未知類型維持拒絕 " + type);
        foreach (byte type in new byte[] { 5, 6, 7, 8, 12, 14, 16, 17, 26, 27, 28, 29, 30, 31, 33, 35, 39, 42, 46, 55, 248 }) {
            Reject(() => ItemUseCodec.Encode(Item(type), null), "不可缺參數 " + type);
            Reject(() => ItemUseCodec.Encode(Item(type) with { Count = 0 }, null), "耗盡拒絕 " + type);
        }
        Reject(() => ItemUseCodec.Encode(Item(26), new ItemUseArguments.Name("name")), "拒絕錯誤參數種類");
        Reject(() => ItemUseCodec.Encode(Item(26), new ItemUseArguments.InventoryTarget(0)), "拒絕零目標");
        Reject(() => ItemUseCodec.Encode(Item(42), new ItemUseArguments.Location(-1, 3)), "拒絕負座標");
        Reject(() => ItemUseCodec.Encode(Item(42), new ItemUseArguments.Location(65536, 3)), "拒絕座標溢位");
        Reject(() => ItemUseCodec.Encode(Item(28), new ItemUseArguments.Spell(256)), "拒絕魔法索引溢位");
        Reject(() => ItemUseCodec.Encode(Item(248), new ItemUseArguments.Cooking(2, 0)), "拒絕料理操作溢位");
        foreach (string invalid in new[] { "", " ", "a\0b", "a\nb", "😀", new string('A', 201) })
            Reject(() => ItemUseCodec.Encode(Item(16), new ItemUseArguments.Name(invalid)), "名稱空白／控制字元／Big5／長度拒絕");
        Reject(() => ItemUseCodec.Encode(Item(12), new ItemUseArguments.Letter(0, "A", "a\0b", "")), "拒絕信件內嵌 NUL");
    }
    static async Task SendPaths() {
        (byte Type, ItemUseArguments Arguments)[] cases = [
            (26, new ItemUseArguments.InventoryTarget(0x55667788)), (7, new ItemUseArguments.InventoryTarget(0x55667788)),
            (5, new ItemUseArguments.WorldTarget(456, 32601, 32940)), (8, new ItemUseArguments.WorldTarget(456, 32601, 32940)),
            (42, new ItemUseArguments.Location(32602, 32941)), (16, new ItemUseArguments.Name("測試")),
            (6, new ItemUseArguments.Teleport(0, 0)), (248, new ItemUseArguments.Cooking(1, 7)),
            (28, new ItemUseArguments.Spell(0)), (12, new ItemUseArguments.Letter(0, "測試", "主旨", "本文"))
        ];
        foreach (var test in cases) {
            using var connection = Seed(test.Type);
            int sends = 0;
            Sender(connection, (bytes, valid, admit) => {
                if (!valid()) return Task.FromResult(false);
                Check(bytes.SequenceEqual(ItemUseCodec.Encode(connection.World.SelectedItem!, test.Arguments)), "真實送出入口採模組編碼器 " + test.Type);
                sends++; admit(); return Task.FromResult(true);
            });
            var before = connection.World.Inventory;
            var status = connection.World.Status;
            var request = await Begin(connection);
            Check(sends == 0, "選取前不送出封包");
            Check(await connection.UseItemWithArguments(request, test.Arguments) && sends == 1, "明示確認送一次 " + test.Type);
            Check(!await connection.UseItemWithArguments(request, test.Arguments) && sends == 1, "重複確認被拒絕");
            Check(before.SequenceEqual(connection.World.Inventory) && status == connection.World.Status, "等待原服回報才改數量／屬性");
        }
        using (var connection = Seed()) {
            var stamp = PlayerHudActions.Stamp(connection);
            Check(!await PlayerHudActions.Activate(connection, stamp, new(PlayerHudKind.Item, 0x11223344), false) && connection.PendingItemUse != null,
                "HUD 共用入口要求參數");
            var request = connection.PendingItemUse!;
            Check(!await connection.UseItemWithArguments(request, new ItemUseArguments.InventoryTarget(999)), "拒絕非持有目標");
            connection.CancelItemUse();
            Check(!await connection.UseItemWithArguments(request, new ItemUseArguments.InventoryTarget(0x55667788)), "取消後確認無效");
        }
        using (var connection = Seed(1)) {
            Check(await connection.UseSelectedItem() && connection.PendingItemUse == null, "既有裝備入口直接送出");
        }
    }
    static async Task Races() {
        foreach (bool queued in new[] { false, true })
        foreach (string change in new[] { "selection", "source", "target", "map", "death", "disconnect", "cancel", "move", "input" }) {
            using var connection = Seed();
            int sends = 0;
            var gate = new TaskCompletionSource<bool>();
            Sender(connection, async (_, valid, admit) => {
                if (queued) await gate.Task;
                if (!valid()) return false;
                sends++; admit(); return true;
            });
            var request = await Begin(connection);
            var arguments = new ItemUseArguments.InventoryTarget(0x55667788);
            Task<bool>? pending = queued ? connection.UseItemWithArguments(request, arguments) : null;
            switch (change) {
                case "selection": connection.World.SelectItem(0x55667788); break;
                case "source": Add(connection, Item(26) with { Count = 1 }); break;
                case "target": Feed(connection, LoginPackets.Pad([57, 0x88, 0x77, 0x66, 0x55])); break;
                case "map": Feed(connection, LoginPackets.Pad([206, 1, 0, 0])); break;
                case "death": Set(connection.World, "OwnDead", true); break;
                case "disconnect": connection.Cancel(); break;
                case "cancel": connection.CancelItemUse(); break;
                case "move": Set(connection.World, "Actor", connection.World.Actor! with { X = 32602 }); break;
                case "input":
                    connection.CancelMovementInput();
                    // Dialog focus/target clicks may clear input before submission. Only queued sends capture it.
                    if (!queued) connection.CancelItemUse();
                    break;
            }
            gate.SetResult(true);
            bool sent = await (pending ?? connection.UseItemWithArguments(request, arguments));
            Check(!sent && sends == 0, $"過期操作零封包 {queued}/{change}");
        }
        foreach (string change in new[] { "move", "remove", "death", "replace" }) {
            using var connection = Seed(5);
            var gate = new TaskCompletionSource<bool>(); int sends = 0;
            Sender(connection, async (_, valid, admit) => { await gate.Task; if (!valid()) return false; sends++; admit(); return true; });
            var request = await Begin(connection);
            var pending = connection.UseItemWithArguments(request, new ItemUseArguments.WorldTarget(456, 32601, 32940));
            if (change == "remove") Feed(connection, LoginPackets.Pad([120, 0xC8, 1, 0, 0]));
            else Feed(connection, ObjectPacket(456, (ushort)(change == "move" ? 32602 : 32601), (byte)(change == "death" ? 8 : 0)));
            gate.SetResult(true);
            Check(!await pending && sends == 0, "等待傳送鎖時目標異動被拒絕 " + change);
        }
        using (var connection = Seed(8)) {
            Feed(connection, ObjectPacket(456, action: 8));
            var request = await Begin(connection);
            Check(await connection.UseItemWithArguments(request, new ItemUseArguments.WorldTarget(456, 32601, 32940)), "復活卷軸允許選擇屍體");
        }
    }
    static void Dialogs() {
        foreach (byte type in new byte[] { 26, 5, 8, 42, 16, 6, 248, 28, 12 }) {
            var request = new ItemUseRequest(Item(type), 0, 0, 0, 123, 0, 0, 0, 32600, 32940);
            using var dialog = new ItemUseDialog(request, [new(0x55667788, "測試目標")]);
            var fields = dialog.Controls.OfType<FlowLayoutPanel>().Single();
            foreach (var text in fields.Controls.OfType<TextBox>()) text.Text = "測試";
            foreach (var combo in fields.Controls.OfType<ComboBox>()) combo.SelectedIndex = 0;
            if (type is 5 or 8) dialog.SelectScene(new ItemUseArguments.WorldTarget(456, 32601, 32940), "測試目標");
            if (type == 42) dialog.SelectScene(new ItemUseArguments.Location(32601, 32940), "測試座標");
            var read = (Func<ItemUseArguments>)typeof(ItemUseDialog).GetField("readArguments", Flags)!.GetValue(dialog)!;
            Check(ItemUseCodec.Encode(request.Item, read()).Length >= 8, "原生參數控制項可產生正確類型 " + type);
            Check(dialog.CancelButton != null && !dialog.Visible, "取消按鈕已配置，測試未顯示視窗");
        }
    }
    [STAThread]
    static int Main(string[] args) {
        try {
            using var guard = new SocketGuard();
            Vectors(); SendPaths().GetAwaiter().GetResult(); Races().GetAwaiter().GetResult(); Dialogs();
            File.WriteAllText(args[0], JsonSerializer.Serialize(new { status = "pass", checks = passed.Count,
                scope = "產品 DLL、合成封包、記憶體 sender、未顯示的參數控制項", game = false, network = false, passed }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS {passed.Count} item-use checks"); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    sealed class SocketGuard : EventListener {
        protected override void OnEventSourceCreated(EventSource source) { if (source.Name == "System.Net.Sockets") EnableEvents(source, EventLevel.LogAlways); }
        protected override void OnEventWritten(EventWrittenEventArgs e) => throw new Exception("離線測試禁止網路");
    }
}
