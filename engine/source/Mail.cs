using System.Buffers.Binary;
using System.Text;

namespace P06;

public sealed record MailHeader(uint Id, byte ReadStatus, byte Year, byte Month, byte Day, string Sender, string Subject);
public sealed record MailReply(byte Type, MailHeader[] Headers, uint Id = 0, string Body = "", byte Result = 0);
public sealed record MailStamp(int Session, int Epoch, uint Actor);
/// <summary>
/// One mailbox snapshot. Notice = the text of the last mail result line the original prints (Line, string-c.tbl ids, FU15§2.7);
/// "" until the first result. NoticeSerial counts result lines, so the same text twice (two deletions) is still two lines.
/// Status = the engine's own messages (send acknowledgements, validation, unparsable replies); the original has no such lines,
/// so they never go to the chat window and are shown only in the --classic-ui dialog.
/// Recent = the texts of the last result lines, oldest first, ending with Notice (at most WorldSession.MailRecentLines), so a
/// reader that skipped serials (two replies in one frame) can still print each line once.
/// </summary>
public sealed record MailView(MailStamp Stamp, int Revision, MailHeader[] Headers, uint? ReadingId, string Body, string Notice,
    MailNoticeLine? Line = null, int NoticeSerial = 0, string Status = "", string[]? Recent = null);

/// <summary>
/// T431 (#988, FU15§2.7 已驗證; IDA sub_941000 / FO 0x540400): the result line the op 99 handler prints in the chat window.
/// Id is a string-c.tbl record (sub_4EAFF0); Prefix != 0 = the "%s %s" form (sub_4D4880 at VA 0x941A3C–0x941A6A pushes 1261
/// first, then 400, so 400 is the first %s): "血盟 信箱已滿，無法再收信件。". Colours: sub_7DC800 / sub_7DC850 with palette[24],
/// palette[14] (ClientForm.MailChatColor / MailChatSecondColor).
/// </summary>
public sealed record MailNoticeLine(int Id, int Prefix = 0) {
    public const int Sent = 1239, RecipientFull = 1240, Stored = 1241, StoreFailed = 1242, Deleted = 1243, DeleteFailed = 1244;
    public const int ClanFull = 1261, Clan = 400, NewMail = 428;
    // Texts of the local Text.pak string-c.tbl (3484 records, SHA-256 4ebbc1a7…43e9, read with PakViewer for #988); used only
    // when the table cannot be read at run time (e.g. CI without the client), same rule as ServerMessageText's fallback.
    static readonly Dictionary<int, string> Fallback = new() {
        [Sent] = "已將信件送出了。", [RecipientFull] = "對方的信箱已經滿了，所以無法再寄信到對方信箱。",
        [Stored] = "信件已被儲存。", [StoreFailed] = "無法再儲存信件。", [Deleted] = "信件已被刪除了。", [DeleteFailed] = "無法刪除信件。",
        [ClanFull] = "信箱已滿，無法再收信件。", [Clan] = "血盟", [NewMail] = "您收到鴿子信差給你的信件。" };

    /// <summary>
    /// sub_941000 by subtype (high nibble; low nibble = box): 0x00 list / 0x10 body → nothing; 0x20 → 1239 when the byte after
    /// the type is 1, else 1261＋400 for box 1 (clan), else 1240; 0x30 → 1243 / 1244; 0x40 → 1241 / 1242 (result byte 1 / other);
    /// 0x50 → 428. The 3.81 server sends 0x20/0x21 only when the mailbox is full and with no result byte (docs/protocol/mail.md;
    /// the byte read there is padding 0), so 1239 does not occur with it; MailPackets.Parse has no 0x50 (the server never sends it).
    /// </summary>
    public static MailNoticeLine? For(byte type, byte result) => (type & 0xF0) switch {
        0x20 when result == 1 => new(Sent),
        0x20 => (type & 0x0F) == 1 ? new(ClanFull, Clan) : new(RecipientFull),
        0x30 => new(result == 1 ? Deleted : DeleteFailed),
        0x40 => new(result == 1 ? Stored : StoreFailed),
        0x50 => new(NewMail),
        _ => null
    };
    /// <summary>One string-c.tbl record as displayed (colour controls removed); the verified text when there is no table.</summary>
    public static string String(int id) {
        if (ServerMessageText.Table is { } table) return id >= 0 && id < table.Count ? ServerMessageText.DisplayText(table[id]) : "";
        return Fallback.GetValueOrDefault(id, "");
    }
    public string Text => Prefix == 0 ? String(Id) : $"{String(Prefix)} {String(Id)}";
}

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
    string mailBody = "", mailStatus = MailStatusInitial;
    // T431 (#988): the last result line as string ids (FU15§2.7) and how many result lines there were.
    MailNoticeLine? mailLine;
    readonly Queue<MailNoticeLine> mailRecent = new();
    int mailRevision, mailNoticeSerial;
    const string MailStatusInitial = "尚未要求信件清單。";
    /// <summary>How many result lines MailView.Recent keeps.</summary>
    public const int MailRecentLines = 8;
    MailStamp CurrentMailStamp => new(PickupSessionGeneration?.Invoke() ?? 0, WorldEpoch, Status?.ObjectId ?? 0);
    void CheckMailIdentity() {
        var current = CurrentMailStamp;
        if (mailStamp == current) return;
        mailStamp = current; mailHeaders = []; mailReadingId = null; mailBody = "";
        mailLine = null; mailRecent.Clear(); mailStatus = MailStatusInitial; mailRevision++;
    }
    public MailView MailSnapshot() {
        CheckMailIdentity();
        return new(mailStamp!, mailRevision, mailHeaders.ToArray(), mailReadingId, mailBody, mailLine?.Text ?? "", mailLine, mailNoticeSerial, mailStatus,
            mailRecent.Select(x => x.Text).ToArray());
    }
    /// <summary>Engine-only message (MailView.Status): never a chat line.</summary>
    internal void MailStatus(string status) { CheckMailIdentity(); mailStatus = status; mailRevision++; }
    internal void ReceiveMail(byte[] plain) {
        // Parse completely before modifying the current mailbox.
        MailReply reply;
        try { reply = MailPackets.Parse(plain); }
        catch (ProtocolException) { MailStatus("收到無法解析的信件回包，保留原清單；請重新整理。"); return; }
        CheckMailIdentity();
        if (reply.Type <= 2) {
            // sub_941000 case 0: rows only, no chat line.
            mailHeaders = reply.Headers;
            if (!mailHeaders.Any(x => x.Id == mailReadingId)) { mailReadingId = null; mailBody = ""; }
            mailStatus = "";
        } else if (reply.Type is >= 0x10 and <= 0x12) {
            // case 0x10: the body only, no chat line.
            if (!mailHeaders.Any(x => x.Id == reply.Id)) return;
            mailReadingId = reply.Id; mailBody = reply.Body;
            mailHeaders = mailHeaders.Select(x => x.Id == reply.Id ? x with { ReadStatus = 1 } : x).ToArray();
            mailStatus = "";
        } else {
            // Cases 0x20 / 0x30 / 0x40: the line is printed whether or not the id is in the list (sub_9B14C0 / sub_9B3120 only
            // touch the window); the list changes only for a successful deletion of a known mail. Storing keeps the row (the
            // server list has no box field, docs/protocol/mail.md).
            if (reply.Result == 1 && reply.Type is >= 0x30 and <= 0x32 && mailHeaders.Any(x => x.Id == reply.Id)) {
                mailHeaders = mailHeaders.Where(x => x.Id != reply.Id).ToArray();
                if (mailReadingId == reply.Id) { mailReadingId = null; mailBody = ""; }
            }
            mailLine = MailNoticeLine.For(reply.Type, reply.Result)!; mailNoticeSerial++; mailStatus = "";
            mailRecent.Enqueue(mailLine); while (mailRecent.Count > MailRecentLines) mailRecent.Dequeue();
        }
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
        } catch (ArgumentException ex) { World.MailStatus(ex.Message); return false; }
        var sender = sendMovementGate; mailSending = stamp;
        try {
            return await sender(packet, () => Current() && HasId(), () => World.MailStatus(command switch {
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
