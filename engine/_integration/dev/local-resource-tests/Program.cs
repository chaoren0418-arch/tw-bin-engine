using P06;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length!=3){Console.WriteLine("BLOCKED: Usage: <assets-root> <icon-pins.json> <scratch-test-root>");return 2;}
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
foreach(string url in new[]{"http://p08.example/icons/2.png","https://evil.example/icons/2.png","https://p08.example.evil/icons/2.png","https://p08.example:443/icons/2.png","https://user@p08.example/icons/2.png","https://p08.example/icons/2.png?x=1","https://p08.example/icons/2.png?","https://p08.example/icons/2.png#x","https://p08.example/x/../icons/2.png","https://p08.example/icons/../2.png","https://p08.example/icons/%32.png","https://p08.example/icons/02.png","https://p08.example/icons/+2.png","https://p08.example/icons/65536.png","https://p08.example/icons/２.png","https://p08.example/icons/2.PNG","https://p08.example/icons/2\\.png","https://p08.example/icons/.png"})
 Check(WebIconResource.Read(args[0],url).Status==(url.StartsWith("https://p08.example/icons/",StringComparison.Ordinal)?404:403),"Reject "+url);
Check(WebIconResource.Read(Path.Combine(args[2],"absent"),"https://p08.example/icons/2.png").Status==404,"missing assets is 404");
if(!Directory.Exists(args[0])){Console.WriteLine(JsonSerializer.Serialize(new{status="skipped",reason="Missing local assets",securityChecks=checks,localIconChecksExecuted=false}));return 77;}
var pins=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[1]))!;
foreach(var (id,sha) in pins){
 var response=WebIconResource.Read(args[0],"https://p08.example/icons/"+id+".png");
 Check(response.Status==200&&response.Body!=null,"local response "+id);
 Check(Convert.ToHexString(SHA256.HashData(response.Body!)).ToLowerInvariant()==sha,"unchanged icon bytes "+id);
}
Check(WebIconResource.Read(Path.Combine(args[2],"directory-link"),"https://p08.example/icons/2.png").Status==403,"inventory junction rejected");
Check(WebIconResource.Read(Path.Combine(args[2],"file-link"),"https://p08.example/icons/2.png").Status==403,"image path reparse point rejected");
Console.WriteLine(JsonSerializer.Serialize(new{status="pass",icons=pins.Count,checks,webViewStarted=false}));return 0;
