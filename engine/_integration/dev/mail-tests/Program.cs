using P06;
using System.Buffers.Binary;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text;
using System.Text.Json;

static class Checks {
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    static void Reject(Action action, string name) {
        try { action(); } catch (Exception ex) when (ex is ArgumentException or ProtocolException) { checks++; return; }
        throw new Exception("Accepted invalid input: " + name);
    }
    static void Set(object target, string name, object? value) {
        var property = target.GetType().GetProperty(name, Flags);
        if (property != null) property.SetValue(target, value); else target.GetType().GetField(name, Flags)!.SetValue(target, value);
    }
    static byte[] Fixture(Action<BinaryWriter> write) {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        write(writer); return LoginPackets.Pad(stream.ToArray());
    }
    static void Wide(BinaryWriter writer, string text) { writer.Write(Encoding.Unicode.GetBytes(text)); writer.Write((ushort)0); }
    static byte[] List(byte type, params uint[] ids) => Fixture(w => {
        w.Write((byte)186); w.Write(type); w.Write((ushort)ids.Length);
        foreach (uint id in ids) {
            w.Write(id); w.Write((byte)0); w.Write(new byte[] { 26, 11, 7 });
            w.Write(LoginPackets.TextEncoding.GetBytes("寄件人")); w.Write((byte)0); Wide(w, "一封信 A😀");
        }
    });
    static byte[] Body(byte type, uint id, string text) => Fixture(w => { w.Write((byte)186); w.Write(type); w.Write(id); Wide(w, text); w.Write((byte)0); });
    static byte[] Ack(byte type, uint id, byte result = 1) => Fixture(w => { w.Write((byte)186); w.Write(type); w.Write(id); w.Write(result); });
    static void Feed(LoginConnection connection, byte[] bytes) => connection.World.Receive(bytes, "offline");
    static LoginConnection Seed() {
        var connection = new LoginConnection(); Set(connection, "State", LoginState.InWorld);
        Set(connection.World, "CacheEnabled", false);
        Set(connection.World, "Status", JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        return connection;
    }
    static void Sender(LoginConnection connection, Func<byte[], Func<bool>, Action, Task<bool>> sender) => Set(connection, "sendMovementGate", sender);
    static void EncodingChecks() {
        for (byte box = 0; box < 3; box++) {
            Check(MailPackets.List(box).SequenceEqual(new byte[] { 87, box, 0, 0, 0, 0, 0, 0 }), "C87 list " + box);
            foreach (bool delete in new[] { false, true }) {
                byte[] bytes = delete ? MailPackets.Delete(box, 0x12345678) : MailPackets.Read(box, 0x12345678);
                Check(bytes.SequenceEqual(new byte[] { 87, (byte)((delete ? 0x30 : 0x10) + box), 0x78, 0x56, 0x34, 0x12, 0, 0 }), "C87 id " + box + delete);
            }
        }
        Check(Convert.ToHexString(MailPackets.Store(0x12345678)) == "5740785634120000", "C87 store");
        Check(Convert.ToHexString(MailPackets.Write(false, "AB", "S", "B")) == "572000004142005300000042000000", "C87 exact raw bytes including ignored H");
        foreach (bool clan in new[] { false, true }) foreach (string receiver in new[] { "A", "AB", "測試", "測試A" }) {
            byte[] bytes = MailPackets.Write(clan, receiver, "一封信", "本文\r\n😀");
            int start = 5 + LoginPackets.TextEncoding.GetByteCount(receiver);
            Check(bytes[1] == (clan ? 0x21 : 0x20) && (bytes.Length - start) % 2 == 0, "even server readByte remainder " + clan + receiver);
            Check(Encoding.Unicode.GetString(bytes, start, bytes.Length - start) == "一封信\0本文\r\n😀\0", "UTF16 fields " + clan + receiver);
            var encrypted = new LoginCipher(123).Transform(bytes, true);
            Check(new LoginCipher(123).Transform(encrypted, false).SequenceEqual(bytes), "unpadded cipher roundtrip " + clan + receiver);
            Check(LoginFrames.Wrap(encrypted).Length == bytes.Length + 2, "valid transport frame");
        }
        Check(MailPackets.Write(false, "A", "", "").Length == 10, "server accepts empty subject and body");
        int capacity = MailPackets.RemainingBodyUnits("AB", "標題");
        Check(MailPackets.Write(false, "AB", "標題", new string('字', capacity)).Length <= MailPackets.MaxPacketBytes, "body boundary");
        Reject(() => MailPackets.Write(false, "AB", "標題", new string('字', capacity + 1)), "body overflow");
        foreach (string invalid in new[] { "x\0y", "\ud800" }) {
            Reject(() => MailPackets.Write(false, invalid, "", ""), "recipient delimiter/surrogate");
            Reject(() => MailPackets.Write(false, "A", invalid, ""), "subject delimiter/surrogate");
            Reject(() => MailPackets.Write(false, "A", "", invalid), "body delimiter/surrogate");
        }
        Reject(() => MailPackets.Write(false, "😀", "", ""), "non Big5 recipient");
        Reject(() => MailPackets.Write(false, "", "", ""), "empty receiver");
        Reject(() => MailPackets.List(3), "unknown box"); Reject(() => MailPackets.Read(3, 1), "unknown read box");
        Reject(() => MailPackets.Delete(3, 1), "unknown delete box"); Reject(() => MailPackets.Store(0), "zero id");
        Reject(() => MailPackets.Read(0, uint.MaxValue), "negative signed server id");
    }
    static void ParsingChecks() {
        for (byte box = 0; box < 3; box++) {
            var parsed = MailPackets.Parse(List(box, 1, 2));
            Check(parsed.Type == box && parsed.Headers.Length == 2 && parsed.Headers[1].Id == 2, "S186 multi row " + box);
            Check(parsed.Headers[0] is { Sender: "寄件人", Subject: "一封信 A😀", Year: 26, Month: 11, Day: 7, ReadStatus: 0 }, "mixed text and dates");
            Check(MailPackets.Parse(List(box)).Headers.Length == 0, "zero count " + box);
            Check(MailPackets.Parse(Body((byte)(0x10 + box), 42, "內文\r\n😀")).Body == "內文\r\n😀", "S186 read " + box);
            Check(MailPackets.Parse(Ack((byte)(0x30 + box), 42)).Result == 1, "S186 delete " + box);
        }
        Check(MailPackets.Parse(Body(0x10, 42, "")).Body == "", "empty body plus writeByte terminator");
        Check(MailPackets.Parse(Ack(0x40, 42)).Id == 42, "S186 store");
        foreach (byte type in new byte[] { 0x20, 0x21 }) Check(MailPackets.Parse(LoginPackets.Pad([186, type])).Type == type, "send failure " + type);
        Reject(() => MailPackets.Parse(List(0, 1, 1)), "duplicate ids");
        Reject(() => MailPackets.Parse(Ack(0x40, 1, 2)), "invalid result");
        Reject(() => MailPackets.Parse(LoginPackets.Pad([186, 99])), "unknown subtype");
        Reject(() => MailPackets.Parse(LoginPackets.Pad([186, 0, 255, 255])), "impossible count");
        Reject(() => MailPackets.Parse([186, 0x10, 1, 0, 0, 0, 1, 1]), "missing body terminator");
        Reject(() => MailPackets.Parse([186, 0x10, 1, 0, 0, 0, 0, 216, 0, 0, 0, 0, 0, 0, 0, 0]), "invalid UTF16 surrogate");
        foreach (var packet in new[] { List(0, 42), Body(0x10, 42, "x"), Ack(0x40, 42) }) {
            Reject(() => MailPackets.Parse(packet[..^1]), "truncated padded packet");
            byte[] dirty = packet.ToArray(); dirty[^1] = 255;
            Reject(() => MailPackets.Parse(dirty), "nonzero padding");
            Reject(() => MailPackets.Parse([.. packet, .. new byte[8]]), "excess padding");
        }
    }
    static async Task LifecycleChecks() {
        Check(new WorldSession().MailSnapshot().Headers.Length == 0, "standalone world has no connection delegate");
        using var connection = Seed(); int sends = 0; byte[]? sent = null;
        Sender(connection, (bytes, valid, admit) => { if (!valid()) return Task.FromResult(false); sends++; sent = bytes; admit(); return Task.FromResult(true); });
        var stamp = connection.World.MailSnapshot().Stamp;
        Check(await connection.Mail("list", 0, 0, stamp) && sends == 1 && sent![0] == 87, "real send gate");
        Feed(connection, List(0, 42, 43));
        Check(await connection.Mail("read", 0, 42, stamp), "read existing id");
        Check(connection.World.MailSnapshot().ReadingId == null, "no speculative read");
        Feed(connection, Body(0x10, 42, "本文"));
        Check(connection.World.MailSnapshot() is { ReadingId: 42, Body: "本文" } && connection.World.MailSnapshot().Headers[0].ReadStatus == 1, "read response applied");
        Check(await connection.Mail("store", 0, 42, stamp), "store existing id");
        Feed(connection, Ack(0x40, 42));
        Check(connection.World.MailSnapshot().Headers.Length == 2 && connection.World.MailSnapshot().Notice.Contains("確認存入"), "store keeps server mixed list");
        Check(await connection.Mail("delete", 2, 42, stamp) && connection.World.MailSnapshot().Headers.Length == 2, "delete waits for ACK");
        Feed(connection, Ack(0x32, 42));
        Check(connection.World.MailSnapshot() is { ReadingId: null, Body: "" } && connection.World.MailSnapshot().Headers.Length == 1, "delete response clears selected body");
        int before = sends;
        Check(!await connection.Mail("delete", 0, 999, stamp) && sends == before, "unknown id blocked");
        Feed(connection, Body(0x10, 42, "late"));
        Check(connection.World.MailSnapshot().ReadingId == null, "late read of removed mail ignored");
        Feed(connection, LoginPackets.Pad([186, 0, 255, 255]));
        Check(connection.World.MailSnapshot().Headers.Single().Id == 43 && connection.World.MailSnapshot().Notice.Contains("無法解析"), "invalid reply does not replace mailbox");
        Check(await connection.Mail("write", 0, 0, stamp, "AB", "標題", "本文") && connection.World.MailSnapshot().Notice.Contains("沒有成功 ACK"), "send is not delivery success");
        Feed(connection, LoginPackets.Pad([186, 0x20]));
        Check(connection.World.MailSnapshot().Notice.Contains("無法寄出"), "mailbox full failure");
        before = sends;
        Check(!await connection.Mail("write", 0, 0, stamp, "AB", "", "bad\0body") && before == sends, "validation at send boundary");
        var release = new TaskCompletionSource();
        Sender(connection, async (_, valid, admit) => { await release.Task; if (!valid()) return false; sends++; admit(); return true; });
        var pending = connection.Mail("delete", 0, 43, stamp);
        Check(!await connection.Mail("list", 0, 0, stamp), "single queued request");
        connection.World.Reset(); release.SetResult();
        Check(!await pending && before == sends && connection.World.MailSnapshot().Headers.Length == 0, "reset invalidates queued request and mailbox");
        Check(!await connection.Mail("list", 0, 0, stamp), "old stamp rejected");
        using var disconnected = Seed(); var old = disconnected.World.MailSnapshot().Stamp;
        Feed(disconnected, List(0, 1)); disconnected.Cancel();
        Check(disconnected.World.MailSnapshot().Headers.Length == 0 && !await disconnected.Mail("list", 0, 0, old), "disconnect clears private mail");
        using var failed = Seed(); Sender(failed, (_, _, _) => throw new IOException("synthetic transport failure"));
        Check(!await failed.Mail("list", 0, 0, failed.World.MailSnapshot().Stamp) && failed.State == LoginState.Faulted, "transport error disconnects without retry");
        using var closed = Seed(); bool open = true; int closedSends = 0;
        var closedRelease = new TaskCompletionSource();
        Sender(closed, async (_, valid, admit) => { await closedRelease.Task; if (!valid()) return false; closedSends++; admit(); return true; });
        var queuedWrite = closed.Mail("write", 0, 0, closed.World.MailSnapshot().Stamp, "AB", "", "charged", () => open);
        open = false; closedRelease.SetResult();
        Check(!await queuedWrite && closedSends == 0, "closed editor revokes queued paid write");
    }
    static void UiChecks() {
        using var connection = Seed(); Feed(connection, List(0, 1));
        using var hud = new PlayerHudView();
        IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
        Check(Descendants(hud).Count(x => x.AccessibleName == "信件") == 1, "single HUD mail button");
        using var dialog = new MailDialog(connection);
        var controls = Descendants(dialog).ToArray();
        Check(controls.OfType<TextBox>().Any(x => x.AccessibleName == "信件本文" && x.Multiline && x.AcceptsReturn), "multiline compose editor");
        Check(controls.OfType<TextBox>().Any(x => x.AccessibleName == "讀信本文" && x.ReadOnly), "read-only received body");
        Check(controls.OfType<ListView>().Single().Items.Count == 1, "UI receives actual world mailbox");
        Check(!dialog.Visible, "offline UI constructed without showing game or window");
    }
    [STAThread]
    static int Main() {
        try {
            using var guard = new SocketGuard(); EncodingChecks(); ParsingChecks(); LifecycleChecks().GetAwaiter().GetResult(); UiChecks();
            Console.WriteLine($"PASS {checks} mail checks; synthetic packets, real product DLL, no game/network/database"); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    sealed class SocketGuard : EventListener {
        protected override void OnEventSourceCreated(EventSource source) { if (source.Name == "System.Net.Sockets") EnableEvents(source, EventLevel.LogAlways); }
        protected override void OnEventWritten(EventWrittenEventArgs e) => throw new Exception("Network forbidden in mail tests");
    }
}
