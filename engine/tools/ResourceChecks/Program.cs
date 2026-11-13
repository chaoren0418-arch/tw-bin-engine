using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if(args.Length!=1){Console.Error.WriteLine("Usage: ResourceChecks <product-build-directory>");return 2;}
var approved=new Dictionary<string,HashSet<string>> {
 ["P11.ClientEngine.dll"]=["P06.Resources.PickupOwnBase15Gfx0Weapon0.json","P06.Resources.PickupOwnBase15ProfileExtension.json"],
 ["P24.Audio.dll"]=["P24.Audio.CombatSfxPins.json"]
};
int count=0;
try {
 foreach(var (file,names) in approved){
  using var stream=File.OpenRead(Path.Combine(args[0],file));using var pe=new PEReader(stream);
  var metadata=pe.GetMetadataReader();var actual=new HashSet<string>();
  foreach(var handle in metadata.ManifestResources){
   var resource=metadata.GetManifestResource(handle);string name=metadata.GetString(resource.Name);
   if(!resource.Implementation.IsNil || !actual.Add(name) || !names.Contains(name)) throw new InvalidDataException("Unexpected/linked resource: "+file+"/"+name);
   count++;
  }
  if(!actual.SetEquals(names))throw new InvalidDataException("Resource inventory mismatch: "+file);
 }
 Console.WriteLine(JsonSerializer.Serialize(new {status="pass",assemblies=2,embeddedResources=count,assemblyCodeExecuted=false}));return 0;
} catch(Exception ex) when(ex is IOException or InvalidDataException or BadImageFormatException or UnauthorizedAccessException){Console.Error.WriteLine("REFUSED: "+ex.Message);return 1;}
