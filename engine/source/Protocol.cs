using System.Buffers.Binary;
using System.Security.Cryptography;
namespace P06;
public sealed class ProtocolException(string message):Exception(message);
public sealed class Frames {
    readonly byte[] buffer=new byte[64]; int used; int target=2;
    public int Buffered=>used;
    public void Feed(ReadOnlySpan<byte> input,Action<byte[]> emit) {
        foreach(byte b in input) {
            buffer[used++]=b;
            if(used==2) {target=BinaryPrimitives.ReadUInt16LittleEndian(buffer);if(target<3||target>64)throw new ProtocolException($"length {target} outside slice limit 3..64");}
            if(used==target){var packet=buffer[..used];used=0;target=2;emit(packet);}
        }
    }
    public void Eof(){if(used!=0)throw new ProtocolException($"truncated EOF: {used}/{target} bytes");}
}
public sealed class HeadingCodec {
    public const uint ObjectId=0x01020304;
    public const string Profile="TW1608262200-heading-postinit-fixture-v1";
    public static readonly byte[] InitialKey=Convert.FromHexString("78563412F0DEBC9A");
    uint k0=0x12345678,k1=0x9abcdef0;
    readonly byte[] permutation=Enumerable.Range(0,256).Select(x=>(byte)x).ToArray();
    public uint NextSequence {get;private set;}=1;
    public int Received {get;private set;}
    public HeadingCodec(){int j=0;for(int i=0;i<256;i++){j=(j+permutation[i]+InitialKey[i%8])&255;(permutation[i],permutation[j])=(permutation[j],permutation[i]);}}
    public string KeyHex {get {Span<byte> k=stackalloc byte[8];BinaryPrimitives.WriteUInt32LittleEndian(k,k0);BinaryPrimitives.WriteUInt32LittleEndian(k[4..],k1);return Convert.ToHexString(k).ToLowerInvariant();}}
    public string PermutationHash=>Convert.ToHexString(SHA256.HashData(permutation)).ToLowerInvariant();
    public byte[] Encode(int direction,ushort fixturePadding=0) {
        if(direction is <0 or >7)throw new ProtocolException("direction outside 0..7");
        if(NextSequence==uint.MaxValue)throw new ProtocolException("fixture sequence exhausted; reconnect required");
        byte[] p=new byte[10];p[0]=10;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(2),NextSequence++);p[6]=0x4f;p[7]=(byte)direction;
        // Original alignment tail is unspecified; the test fixture explicitly uses two zero bytes.
        BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(8),fixturePadding);
        uint mask=BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(6));byte[] key=Convert.FromHexString(KeyHex);var b=p.AsSpan(2);
        b[0]^=key[0];for(int i=1;i<b.Length;i++)b[i]^=(byte)(b[i-1]^key[i&7]);
        b[3]^=key[2];b[2]^=(byte)(b[3]^key[3]);b[1]^=(byte)(b[2]^key[4]);b[0]^=(byte)(b[1]^key[5]);
        k0^=mask;k1=unchecked(k1+0x287effc3);return p;
    }
    public (uint ObjectId,int Direction,string Plaintext) Decode(byte[] packet) {
        if(packet.Length!=8||BinaryPrimitives.ReadUInt16LittleEndian(packet)!=8)throw new ProtocolException("S heading frame must be exactly 8 bytes");
        byte[] body=packet[2..];int i=0,j=0;
        for(int n=0;n<body.Length;n++) {i=(i+1)&255;j=(j+permutation[i])&255;(permutation[i],permutation[j])=(permutation[j],permutation[i]);body[n]^=permutation[(permutation[i]+permutation[j])&255];}
        if(body[0]!=0x78)throw new ProtocolException($"unknown S opcode 0x{body[0]:X2}; initialized heading slice only");
        uint id=BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(1));if(id!=ObjectId)throw new ProtocolException($"unknown fixture object 0x{id:X8}");
        if(body[5]>7)throw new ProtocolException($"illegal heading {body[5]}");Received++;
        return(id,body[5],Convert.ToHexString(body).ToLowerInvariant());
    }
}
