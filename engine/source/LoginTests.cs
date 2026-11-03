using System.Buffers.Binary;
namespace P06;
public static class LoginTests {
    public static void Vectors(string file,string output){
        var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
        var rows=File.ReadAllLines(file).Select(l=>l.Split('\t')).ToArray();
        foreach(var group in rows.GroupBy(r=>uint.Parse(r[0]))){
            var enc=new LoginCipher(group.Key);var dec=new LoginCipher(group.Key);var stream=new List<byte>();
            foreach(var row in group){byte[] plain=Convert.FromHexString(row[2]),cipher=Convert.FromHexString(row[3]);Check(enc.Transform(plain,true).SequenceEqual(cipher),$"JAR encrypt {group.Key}/{row[1]}");Check(dec.Transform(cipher,false).SequenceEqual(plain),$"JAR decrypt {group.Key}/{row[1]}");
                string state=enc.KeyHex;byte[] k=Convert.FromHexString(state);string formatted=$"{BinaryPrimitives.ReadUInt32LittleEndian(k):X8}{BinaryPrimitives.ReadUInt32LittleEndian(k.AsSpan(4)):X8}";Check(formatted==row[4],$"JAR key evolution {group.Key}/{row[1]}");stream.AddRange(LoginFrames.Wrap(cipher));
            }
            byte[] all=stream.ToArray();for(int split=0;split<=all.Length;split++){var fr=new LoginFrames();var got=fr.Feed(all[..split]).Concat(fr.Feed(all[split..])).ToArray();fr.End();Check(got.Length==group.Count()&&got.Select(Convert.ToHexString).SequenceEqual(group.Select(r=>r[3])),$"all split positions {group.Key}/{split}");}
            var bytesFramer=new LoginFrames();int received=0;foreach(byte b in all)received+=bytesFramer.Feed([b]).Count();bytesFramer.End();Check(received==8,$"byte stream {group.Key}");
        }
        foreach(var invalid in new[]{new byte[]{0,0},new byte[]{5,0},new byte[]{1,40},new byte[]{255,255}}){bool failed=false;try{new LoginFrames().Feed(invalid);}catch(ProtocolException){failed=true;}Check(failed,"reject length "+Convert.ToHexString(invalid));}
        var trunc=new LoginFrames();trunc.Feed([18,0,150]);bool truncated=false;try{trunc.End();}catch(ProtocolException){truncated=true;}Check(truncated,"truncated EOF");
        bool unverified=false;try{LoginProfile.Select("TW1608262200");}catch(NotSupportedException){unverified=true;}Check(unverified,"TW16 not silently substituted");
        foreach(int type in new[]{1,2}){var row=rows.First(r=>r[1]==(type==1?"3":"4"));var reader=new LoginReader(Convert.FromHexString(row[2]));Check(reader.C()==93,"character opcode");var c=LoginPackets.Character(reader,LoginProfile.Select("jar-d287-client"+type));Check(c.Name=="測試王子"&&c.Clan=="P11"&&c.Hp==(type==1?1234u:70000u)&&c.Mp==222&&c.Ac==-15&&c.Lawful==-123&&c.Level==42&&c.CreateTime==123456,"JAR CP950 character fields client"+type);}
        Json.Save(output,new{status="pass",oracle="original JAR Encryption + ServerBasePacket/S_CharPacks serializers",vectors=rows.Length,checks=checks.Count});
    }
    public static async Task Probe(int port,string scenario,string output){
        using var c=new LoginConnection();var states=new List<object>();c.Changed+=()=>states.Add(c.Snapshot());
        int timeout=scenario=="stall"?350:4000;
        var task=c.Run(port,scenario=="wide"?"jar-d287-client2":"jar-d287-client1","engine11","testpass",timeout);
        Task? abandoned=null;
        if(scenario=="reconnect"){await Task.Delay(70);abandoned=task;task=c.Run(port+1,"jar-d287-client2","engine11","testpass",4000);}
        if(scenario=="cancel"){await Task.Delay(70);c.Cancel();}
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(!task.IsCompleted&&(scenario=="complete-drop"?c.State!=LoginState.Disconnected:c.State!=LoginState.CharacterList)){if(watch.ElapsedMilliseconds>7000)throw new Exception("probe timed out");await Task.Delay(10);}
        string expected=scenario switch {"reject" or "wrong-seed"=>"Rejected","cancel"=>"Cancelled","complete-drop"=>"Disconnected","length" or "truncate" or "bad-init" or "drop" or "stall"=>"Faulted",_=>"CharacterList"};
        bool pass=c.State.ToString()==expected;
        if(expected is "Rejected" or "Faulted" or "Cancelled")pass&=c.Characters.Count==0;
        if(expected=="CharacterList")pass&=c.Characters.Count==(scenario=="empty"?0:scenario=="multi"?2:1)&& (scenario=="empty"||c.Characters[0].Name=="測試王子");
        if(scenario is "wide" or "reconnect")pass&=c.Characters[0].Hp==70000;
        if(abandoned!=null){await abandoned;await Task.Delay(500);pass&=c.State==LoginState.CharacterList&&c.Characters[0].Hp==70000;}
        var snapshot=c.Snapshot();c.Cancel();c.Cancel();await task;c.Cancel();c.Dispose();
        Json.Save(output,new{status=pass?"pass":"fail",scenario,expected,snapshot,states});if(!pass)throw new Exception("unexpected login state "+System.Text.Json.JsonSerializer.Serialize(snapshot));
    }
}
