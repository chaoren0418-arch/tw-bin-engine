using System.Buffers.Binary;
using System.Text;
namespace P06;

// This profile describes the inspected JAR, not the TW16 binary or a verified TW13 client.
public sealed record LoginProfile(string Id, bool WideVitals) {
    public const string JarSha256="d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf";
    public static LoginProfile Select(string id)=>id switch {
        "p24-real-jar-d287-client2"=>new(id,true),
        "jar-d287-client1"=>new(id,false), "jar-d287-client2"=>new(id,true),
        _=>throw new NotSupportedException("Unverified login profile: "+id)
    };
}

public sealed class LoginFrames {
    readonly byte[] buffer=new byte[10240];int used,expected=2;
    public IEnumerable<byte[]> Feed(byte[] bytes) {
        var frames=new List<byte[]>();
        foreach(byte value in bytes) {
            buffer[used++]=value;
            if(used==2) {expected=BinaryPrimitives.ReadUInt16LittleEndian(buffer);if(expected<6||expected>10240)throw new ProtocolException("invalid frame length");}
            if(used==expected) {frames.Add(buffer.AsSpan(2,used-2).ToArray());used=0;expected=2;}
        }
        return frames;
    }
    public void End(){if(used!=0)throw new ProtocolException("truncated EOF");}
    public static byte[] Wrap(byte[] body){if(body.Length<4||body.Length>10238)throw new ProtocolException("invalid body length");byte[] frame=new byte[body.Length+2];BinaryPrimitives.WriteUInt16LittleEndian(frame,(ushort)frame.Length);body.CopyTo(frame,2);return frame;}
}

public sealed class LoginCipher {
    uint k0,k1;
    public LoginCipher(uint seed) {
        // JAR Blowfish.getSeeds calls blowfish_init, which mutates private tables,
        // but never writes caller keys. Its return is discarded. Confirmed by JAR oracle.
        uint x=seed^0x9c30d539;k0=(x>>13)|(x<<19);k1=0x930fd7e2^k0^0x7c72e993;
    }
    public string KeyHex {get {byte[] b=new byte[8];BinaryPrimitives.WriteUInt32LittleEndian(b,k0);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),k1);return Convert.ToHexString(b);}}
    public byte[] Transform(byte[] input,bool encrypt){
        if(input.Length<4)throw new ProtocolException("cipher requires >=4 bytes");
        byte[] b=(byte[])input.Clone(),k=Convert.FromHexString(KeyHex);uint mask=0;
        if(encrypt){mask=BinaryPrimitives.ReadUInt32LittleEndian(b);b[0]^=k[0];for(int i=1;i<b.Length;i++)b[i]^=(byte)(b[i-1]^k[i&7]);b[3]^=k[2];b[2]^=(byte)(b[3]^k[3]);b[1]^=(byte)(b[2]^k[4]);b[0]^=(byte)(b[1]^k[5]);}
        else {byte b3=b[3],b2=b[2],b1=b[1];b[3]^=k[2];b[2]^=(byte)(b3^k[3]);b[1]^=(byte)(b2^k[4]);byte previous=(byte)(b[0]^b1^k[5]);b[0]=(byte)(previous^k[0]);for(int i=1;i<b.Length;i++){byte t=b[i];b[i]^=(byte)(k[i&7]^previous);previous=t;}mask=BinaryPrimitives.ReadUInt32LittleEndian(b);}
        k0^=mask;k1=unchecked(k1+0x287effc3);return b;
    }
}

public sealed record LoginCharacter(string Name,string Clan,byte Class,byte Sex,short Lawful,uint Hp,uint Mp,sbyte Ac,byte Level,byte Strength,byte Dexterity,byte Constitution,byte Wisdom,byte Charisma,byte Intelligence,byte Gm,uint CreateTime,byte CheckCode);

public sealed class LoginReader(byte[] data) {
    int offset;
    public int Remaining=>data.Length-offset;
    public byte C(){if(offset>=data.Length)throw new ProtocolException($"truncated field at offset {offset}, length {data.Length}");return data[offset++];}
    public ushort H()=>(ushort)(C()|(C()<<8));
    public uint D()=>(uint)(H()|((uint)H()<<16));
    public string S(){int start=offset;while(C()!=0){}return LoginPackets.TextEncoding.GetString(data,start,offset-start-1);}
    public void Finish(){
        int pad=(8-(offset&7))&7;
        // Teleportation.S_TeleportRecovery appends 00 to an already padded payload.
        if(data.Length-offset==pad+8&&TeleportRecoveryPackets.Matches(data.AsSpan(0,offset)))pad+=8;
        if(data.Length-offset!=pad)throw new ProtocolException("unexpected packet length");
        while(offset<data.Length)if(C()!=0)throw new ProtocolException("nonzero padding");
    }
}

public static class LoginPackets {
    public static readonly Encoding TextEncoding;
    static LoginPackets(){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);TextEncoding=Encoding.GetEncoding(950,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);}
    public static uint Seed(byte[] body){if(body.Length!=16||body[0]!=150||!body.AsSpan(5).SequenceEqual(Convert.FromHexString("9DD1D67AF462E7A06602FA")))throw new ProtocolException("unrecognized seed/profile packet");return BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(1));}
    public static byte[] Login(string account,string password){
        if(LoginValidationError(account,password) is {} error)throw new ArgumentException(error);
        return Pad([119,..Encoding.ASCII.GetBytes(account.ToLowerInvariant()),0,..Encoding.ASCII.GetBytes(password),0]);
    }
    // C_AuthLogin.start/checkLogin (server-rules R03); shared by UI and packet encoding.
    // Messages describe the rule only, never the supplied credentials or offending character.
    public static string? LoginValidationError(string? account,string? password){
        if(string.IsNullOrEmpty(account)||account.Length>12)return "帳號長度需為 1–12 位。";
        if(account.Any(c=>!char.IsAsciiLetterOrDigit(c)))return "帳號只能使用 ASCII 英文字母與數字。";
        if(string.IsNullOrEmpty(password)||password.Length>13)return "密碼長度需為 1–13 位。";
        if(password.Any(c=>!char.IsAsciiLetterOrDigit(c)&&!"!_=+-?.#".Contains(c)))return "密碼只能使用 ASCII 英文字母、數字與符號 !_=+-?.#。";
        return null;
    }
    // Explicit client policy: zero-pad to 8; server structure validator reads required fields and allows trailing bytes.
    public static byte[] Pad(byte[] bytes){Array.Resize(ref bytes,(bytes.Length+7)&~7);return bytes;}
    public static LoginCharacter Character(LoginReader r,LoginProfile p){
        string name=r.S(),clan=r.S();byte type=r.C(),sex=r.C();short lawful=unchecked((short)r.H());uint hp=p.WideVitals?r.D():r.H(),mp=p.WideVitals?r.D():r.H();
        var character=new LoginCharacter(name,clan,type,sex,lawful,hp,mp,unchecked((sbyte)r.C()),r.C(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C(),r.D(),r.C());r.Finish();return character;
    }
}
