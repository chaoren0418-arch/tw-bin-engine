using System.Text.Json;
namespace P06;
public static class ProtocolTests {
 public static void Run(string vectors,string output) {
  Directory.CreateDirectory(output);using var doc=JsonDocument.Parse(File.ReadAllText(vectors));var root=doc.RootElement;var checks=new List<object>();
  void Check(string name,bool ok){checks.Add(new{name,pass=ok});if(!ok)throw new Exception(name);}
  void Reject(string name,Action action,string message){try{action();throw new Exception("accepted invalid: "+name);}catch(ProtocolException e){Check(name,e.Message.Contains(message));}}
  static byte[] Wire(JsonElement e)=>Convert.FromHexString(e.GetProperty("wire").GetString()!);
  var c=new HeadingCodec();foreach(var e in root.GetProperty("c2s").EnumerateArray()) {int d=e.GetProperty("direction").GetInt32();Check($"C bytes seq {c.NextSequence}",c.Encode(d).SequenceEqual(Wire(e)));Check("C key evolution",c.KeyHex==e.GetProperty("key_after").GetString());}
  c=new();foreach(var e in root.GetProperty("nonzero_padding").EnumerateArray()){var plain=Convert.FromHexString(e.GetProperty("plaintext").GetString()!);ushort pad=(ushort)(plain[6]|plain[7]<<8);Check("nonzero tail bytes",c.Encode(e.GetProperty("direction").GetInt32(),pad).SequenceEqual(Wire(e)));Check("nonzero tail changes next key",c.KeyHex==e.GetProperty("key_after").GetString());}
  var packets=root.GetProperty("s2c").EnumerateArray().ToArray();byte[] stream=packets.SelectMany(Wire).ToArray();
  void Replay(string name,IEnumerable<byte[]> chunks){var codec=new HeadingCodec();var parser=new Frames();int n=0;foreach(var chunk in chunks)parser.Feed(chunk,p=>{var got=codec.Decode(p);Check(name+" heading",got.Direction==packets[n].GetProperty("direction").GetInt32());Check(name+" permutation",codec.PermutationHash==packets[n].GetProperty("permutation_after_sha256").GetString());n++;});parser.Eof();Check(name+" count",n==packets.Length);}
  Replay("coalesced",new[]{stream});Replay("bytewise",stream.Select(b=>new[]{b}));Replay("whole packets",packets.Select(Wire));
  foreach(int size in new[]{2,3,5,7,11,31,63})Replay("chunk "+size,Enumerable.Range(0,(stream.Length+size-1)/size).Select(i=>stream.Skip(i*size).Take(size).ToArray()));
  for(int split=0;split<=16;split++)Replay("split "+split,new[]{stream[..split],stream[split..]});
  foreach(ushort n in new ushort[]{0,1,2,65,32768,65535})Reject("bad length "+n,()=>new Frames().Feed(new byte[]{(byte)n,(byte)(n>>8)},_=>{}),"length");
  for(int n=1;n<8;n++){int count=n;Reject("EOF "+count,()=>{var f=new Frames();f.Feed(stream[..count],_=>{});f.Eof();},"truncated EOF");}
  foreach(var field in new[]{"opcode","object","direction"})Reject("invalid "+field,()=>new HeadingCodec().Decode(Wire(root.GetProperty("invalid_s").GetProperty(field))),field=="direction"?"heading":field);
  Reject("wrong fixed width",()=>new HeadingCodec().Decode(new byte[9]),"exactly 8");Reject("bad outbound direction",()=>new HeadingCodec().Encode(8),"direction");
  Reject("out of order packet state",()=>new HeadingCodec().Decode(Wire(packets[1])),"opcode");
  var reset=new HeadingCodec();Check("fresh generation accepts first",reset.Decode(Wire(packets[0])).Direction==0);
  Json.Save(Path.Combine(output,"protocol-results.json"),new{status="pass",checks,total=checks.Count,oracle="original archived x86 pure functions in bounded emulator; synthetic fixture"});
 }
}
