using System.Net;using System.Net.Sockets;using System.Text.Json;
namespace P06;
public static class DuplicateRecoveryProbe {
 public static async Task Run(string path){var spec=JsonSerializer.Deserialize<RealCase>(File.ReadAllText(path))!;var packets=new List<object>();using var client=new TcpClient(AddressFamily.InterNetwork);using var token=new CancellationTokenSource(12000);string end="";bool rejected=false;
 try{await client.ConnectAsync(IPAddress.Loopback,42061,token.Token);var stream=client.GetStream();var frames=new LoginFrames();LoginCipher? incoming=null;byte[] buffer=new byte[4096];
 while(!token.IsCancellationRequested){int n=await stream.ReadAsync(buffer,token.Token);if(n==0){end="original peer closed";break;}foreach(var body in frames.Feed(buffer.AsSpan(0,n).ToArray())){
 if(incoming==null){uint seed=LoginPackets.Seed(body);packets.Add(new{kind="seed",bodyHex=Convert.ToHexString(body)});incoming=new(seed);var outgoing=new LoginCipher(seed);var auth=LoginPackets.Login(spec.Account,spec.Password);await stream.WriteAsync(LoginFrames.Wrap(outgoing.Transform(auth,true)),token.Token);Array.Clear(auth);}
 else{byte[] plain=incoming.Transform(body,false);packets.Add(new{kind="server-packet",plainHex=Convert.ToHexString(plain),utc=DateTimeOffset.UtcNow});if(plain[0]==233){var reader=new LoginReader(plain);reader.C();rejected=reader.H()!=0;if(!rejected)throw new Exception("expected duplicate rejection; stop");}}
 }}
 }catch(OperationCanceledException){end="bounded12s";}finally{client.Close();Json.Save(spec.Output,new{status=rejected?"recovery-probe-rejected":"unexpected",end,packets,kind="duplicate-login original protocol recovery; kept rejected connection open for original delayed kick; not combat validation"});}
 }
}
