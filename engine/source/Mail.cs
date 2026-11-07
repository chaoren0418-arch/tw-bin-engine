using System.Buffers.Binary;
using System.Text;

namespace P06;

public sealed record MailHeader(uint Id, byte ReadStatus, byte Year, byte Month, byte Day, string Sender, string Subject);
public sealed record MailReply(byte Type, MailHeader[] Headers, uint Id = 0, string Body = "", byte Result = 0);
public sealed record MailStamp(int Session, int Epoch, uint Actor);
public sealed record MailView(MailStamp Stamp, int Revision, MailHeader[] Headers, uint? ReadingId, string Body, string Notice);

// C87/S186 contract and source identities: docs/protocol/mail.md.
public static class MailPackets {
    public const int MaxPacketBytes = 10238; // LoginFrames body budget, not a claimed server text limit.
    static readonly Encoding Unicode = new UnicodeEncoding(false, false, true);

    static void Box(byte box) { if (box > 2) throw new ArgumentOutOfRangeException(nameof(box)); }
    public static byte[] List(byte box) { Box(box); return LoginPackets.Pad([87, box]); }
    static byte[] WithId(byte type, uint id) {
        if (id == 0 || id > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(id));
        byte[] bytes = new byte[8]; bytes[0] = 87; bytes[1] = type;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2), id); return bytes;
    }
    public static byte[] Read(byte box, uint id) { Box(box); return WithId((byte)(0x10 + box), id); }
    public static byte[] Delete(byte box, uint id) { Box(box); return WithId((byte)(0x30 + box), id); }
    public static byte[] Store(uint id) => WithId(0x40, id);

    public static int RemainingBodyUnits(string receiver, string subject) {
        ValidateText(receiver); ValidateText(subject);
        return Math.Max(0, (MaxPacketBytes - 9 - LoginPackets.TextEncoding.GetByteCount(receiver)) / 2 - subject.Length);
    }
    static void ValidateText(string text) {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\0')) throw new ArgumentException("信件欄位不可包含 NUL 字元。");
        _ = Unicode.GetByteCount(text); // Reject unpaired surrogates, never silently replace text.
    }
    public static byte[] Write(bool clan, string receiver, string subject, string body) {
        ValidateText(receiver); ValidateText(subject); ValidateText(body);
        if (string.IsNullOrWhiteSpace(receiver)) throw new ArgumentException("請填寫收件人或自己的血盟名稱。");
        byte[] name = LoginPackets.TextEncoding.GetBytes(receiver);
        int size = checked(9 + name.Length + 2 * (subject.Length + body.Length));
        if (size > MaxPacketBytes) throw new ArgumentException("信件超過連線封包容量；請縮短標題或本文。");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((byte)87); writer.Write((byte)(clan ? 0x21 : 0x20)); writer.Write((ushort)0);
        writer.Write(name); writer.Write((byte)0);
        writer.Write(Unicode.GetBytes(subject)); writer.Write((ushort)0);
        writer.Write(Unicode.GetBytes(body)); writer.Write((ushort)0);
        // C_Mail.readByte consumes the remainder and requires an even byte count.
        // Do not Pad: an even-length Big5 name makes an 8-byte padded remainder odd.
        return stream.ToArray();
    }
    static string Wide(LoginReader reader) {
        using var stream = new MemoryStream();
        while (true) { ushort unit = reader.H(); if (unit == 0) break; stream.WriteByte((byte)unit); stream.WriteByte((byte)(unit >> 8)); }
        return Unicode.GetString(stream.ToArray());
    }
    public static MailReply Parse(byte[] plain) {
        if (plain.Length > MaxPacketBytes) throw new ProtocolException("mail packet exceeds frame budget");
        try {
            var reader = new LoginReader(plain);
            if (reader.C() != 186) throw new ProtocolException("not S186");
            byte type = reader.C(); MailReply reply;
            if (type <= 2) {
                int count = reader.H();
                if (count > reader.Remaining / 11) throw new ProtocolException("mail count exceeds packet");
                var headers = new MailHeader[count]; var ids = new HashSet<uint>();
                for (int i = 0; i < count; i++) {
                    uint id = reader.D(); byte read = reader.C(), year = reader.C(), month = reader.C(), day = reader.C();
                    if (id == 0 || id > int.MaxValue || !ids.Add(id) || read > 1) throw new ProtocolException("invalid mail header");
                    headers[i] = new(id, read, year, month, day, reader.S(), Wide(reader));
                }
                reply = new(type, headers);
            } else if (type is >= 0x10 and <= 0x12) {
                uint id = reader.D(); string body = Wide(reader);
                // Stored content already has a UTF-16 terminator; writeByte appends one more zero.
                if (reader.C() != 0 || id == 0 || id > int.MaxValue) throw new ProtocolException("invalid mail body");
                reply = new(type, [], id, body);
            } else if (type is 0x20 or 0x21) reply = new(type, []);
            else if (type is 0x30 or 0x31 or 0x32 or 0x40) {
                uint id = reader.D(); byte result = reader.C();
                if (id == 0 || id > int.MaxValue || result > 1) throw new ProtocolException("invalid mail result");
                reply = new(type, [], id, Result: result);
            } else throw new ProtocolException("unknown mail subtype");
            reader.Finish(); return reply;
        } catch (DecoderFallbackException) { throw new ProtocolException("invalid mail text encoding"); }
    }
}

public sealed partial class WorldSession {
    MailStamp? mailStamp;
    MailHeader[] mailHeaders = [];
    uint? mailReadingId;
    string mailBody = "", mailNotice = "尚未要求信件清單。";
    int mailRevision;
    MailStamp CurrentMailStamp => new(PickupSessionGeneration?.Invoke() ?? 0, WorldEpoch, Status?.ObjectId ?? 0);
    void CheckMailIdentity() {
        var current = CurrentMailStamp;
        if (mailStamp == current) return;
        mailStamp = current; mailHeaders = []; mailReadingId = null; mailBody = "";
        mailNotice = "尚未要求信件清單。"; mailRevision++;
    }
    public MailView MailSnapshot() {
        CheckMailIdentity();
        return new(mailStamp!, mailRevision, mailHeaders.ToArray(), mailReadingId, mailBody, mailNotice);
    }
    internal void MailNotice(string notice) { CheckMailIdentity(); mailNotice = notice; mailRevision++; }
    internal void ReceiveMail(byte[] plain) {
        // Parse completely before modifying the current mailbox.
        MailReply reply;
        try { reply = MailPackets.Parse(plain); }
        catch (ProtocolException) { MailNotice("收到無法解析的信件回包，保留原清單；請重新整理。"); return; }
        CheckMailIdentity();
        if (reply.Type <= 2) {
            mailHeaders = reply.Headers;
            if (!mailHeaders.Any(x => x.Id == mailReadingId)) { mailReadingId = null; mailBody = ""; }
            mailNotice = $"收到 {mailHeaders.Length} 封信；伺服器清單可能包含所有信匣。";
        } else if (reply.Type is >= 0x10 and <= 0x12) {
            if (!mailHeaders.Any(x => x.Id == reply.Id)) return;
            mailReadingId = reply.Id; mailBody = reply.Body;
            mailHeaders = mailHeaders.Select(x => x.Id == reply.Id ? x with { ReadStatus = 1 } : x).ToArray();
            mailNotice = "已收到信件本文。";
        } else if (reply.Type is 0x20 or 0x21) mailNotice = "伺服器回報無法寄出信件（一般信可能是收件匣已滿）。";
        else if (reply.Result == 1) {
            if (!mailHeaders.Any(x => x.Id == reply.Id)) return;
            if (reply.Type != 0x40) {
                mailHeaders = mailHeaders.Where(x => x.Id != reply.Id).ToArray();
                if (mailReadingId == reply.Id) { mailReadingId = null; mailBody = ""; }
            }
            mailNotice = reply.Type == 0x40 ? "伺服器已確認存入保管箱。" : "伺服器已確認刪除信件。";
        } else mailNotice = "伺服器未確認信件操作成功。";
        mailRevision++;
    }
}

public sealed partial class LoginConnection {
    MailStamp? mailSending;
    public async Task<bool> Mail(string command, byte box, uint id, MailStamp stamp, string receiver = "", string subject = "", string body = "", Func<bool>? viewCurrent = null) {
        bool Current() => State == LoginState.InWorld && stamp.Actor != 0 && World.MailSnapshot().Stamp == stamp && !World.ActionLocked && (viewCurrent?.Invoke() ?? true);
        if (!Current() || mailSending == stamp || sendMovementGate == null) return false;
        bool HasId() => command is "list" or "write" or "clan" || World.MailSnapshot().Headers.Any(x => x.Id == id);
        if (!HasId()) return false;
        byte[] packet;
        try {
            packet = command switch {
                "list" => MailPackets.List(box), "read" => MailPackets.Read(box, id),
                "delete" => MailPackets.Delete(box, id), "store" => MailPackets.Store(id),
                "write" or "clan" => MailPackets.Write(command == "clan", receiver, subject, body),
                _ => throw new ArgumentException("未知的信件操作。")
            };
        } catch (ArgumentException ex) { World.MailNotice(ex.Message); return false; }
        var sender = sendMovementGate; mailSending = stamp;
        try {
            return await sender(packet, () => Current() && HasId(), () => World.MailNotice(command switch {
                "write" or "clan" => "已送出寄信請求；伺服器沒有成功 ACK，請向收件人核對，勿重複寄送。",
                "list" => "已要求清單；空信箱可能不回包，未收到回應不代表沒有信。",
                _ => "已送出信件操作；等待伺服器回包。"
            }));
        } catch (Exception) {
            if (generation == stamp.Session) { Cancel(); Set(LoginState.Faulted, "信件傳送失敗，已斷線；結果未確認，沒有自動重送。"); }
            return false;
        } finally { if (mailSending == stamp) mailSending = null; }
    }
}
