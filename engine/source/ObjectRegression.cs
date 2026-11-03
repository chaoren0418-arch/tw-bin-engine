using System.Buffers.Binary;
namespace P06;
public static class ObjectRegression {
    public static void Run(Assets assets,string capture,string output){
        var root=Json.Read(capture);var packets=root.GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
        string selected=LoginConnection.CapturedCharacterName(packets);var model=new WorldSession();bool start=false;foreach(var p in packets){if(p[0]==223)start=true;if(start){model.ProbeSent=true;model.Receive(p,selected);}}
        if(!model.Ready||model.Objects.Length<2)throw new Exception("capture world absent");
        uint self=model.Actor!.ObjectId;var obj=packets.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))!=self);uint id=BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(5));model.Receive(obj,selected);int n=model.Objects.Length;model.Receive(obj,selected);if(model.Objects.Length!=n)throw new Exception("duplicate full pack added entity");
        var before=model.Actor;var negatives=new List<string>();
        foreach(byte opcode in new byte[]{87,10,122,120,158}){try{model.Receive([opcode,1],selected);throw new Exception("accepted truncated "+opcode);}catch(ProtocolException){negatives.Add("truncated "+opcode);}}
        byte[] delta=LoginPackets.Pad([122,0xfe,0xff,0xff,0xff,3]);model.Receive(delta,selected);if(model.Actor!=before||model.Objects.Length!=n)throw new Exception("unknown ID changed state");
        model.SelectTarget(id);byte[] remove=LoginPackets.Pad([120,..BitConverter.GetBytes(id)]);model.Receive(remove,selected);model.Receive(LoginPackets.Pad([122,..BitConverter.GetBytes(id),2]),selected);if(model.TargetId!=null||model.Objects.Any(o=>o.ObjectId==id))throw new Exception("removed ID resurrected");
        model.Receive(obj,selected);model.SelectTarget(id);model.Receive(Convert.FromHexString("CE00000000000000"),selected);if(model.Objects.Length!=0||model.TargetId!=null||model.Actor!=null)throw new Exception("map generation retained entities");model.Reset();
        // Tagged entity IDs stay distinct from global map serials, local source indices and sentinels.
        var tile=new Tile(2,[new Pixel(0,0,123)]);var map=new Draw(0,0,0,7,"objects",42,0,0,tile);var secondMap=map with{Serial=100007};var own=map with{Layer="actor",ObjectId=0};var actor=map with{Layer="actor",ObjectId=7};var area=new Rectangle(map.Px,map.Py,1,1);var colors=new ushort[1];var mask=new bool[1];var legacy=new int[1];var entities=new uint?[1];
        if(map.SourceIndex!=secondMap.SourceIndex||map.Serial==secondMap.Serial||actor.ObjectId!=(uint)map.Serial||map.SourceIndex==map.Serial)throw new Exception("collision fixture must distinguish local index, map serial and tagged entity");
        foreach(var coveringMap in new[]{map,secondMap}){
            Renderer.Blit(actor,area,colors,mask,legacy,entities);if(entities[0]!=7)throw new Exception("actor identity missing");
            Renderer.Blit(coveringMap,area,colors,mask,legacy,entities);if(legacy[0]!=coveringMap.Serial||entities[0]!=null)throw new Exception("map serial collision selected actor or lost global source ownership");
        }
        Renderer.Blit(actor with{ObjectId=uint.MaxValue},area,colors,mask,legacy,entities);if(entities[0]!=uint.MaxValue)throw new Exception("unsigned ID lost");Renderer.Blit(own,area,colors,mask,legacy,entities);if(entities[0]!=null||legacy[0]!=-2)throw new Exception("self sentinel collision");
        var catalog=new ObjectAssets(assets.Root);foreach(int gfx in new[]{0,695,1307,931,936,945,4038,792,971,460}){var a=catalog.Get(gfx)!;foreach(int d in Enumerable.Range(0,8)){_=a.At("breath",d,0);_=a.At("walk",d,a.StepMs/2d);}}
        // Explicit synthetic exact overlap through the same packet parser and original tile compositor.
        var npc=packets.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(9))==1307);
        foreach(uint entityId in new uint[]{7,uint.MaxValue}){var b=(byte[])npc.Clone();BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(1),32589);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(3),32930);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5),entityId);model.Receive(b,selected);}
        using var renderer=new Renderer(assets);var presentation=new World(assets);presentation.Teleport(32589,32930);
        var overlap=renderer.ComposeObjects(presentation,model,catalog,out var visuals,out var missing);if(missing.Length!=0||!overlap.EntityOwners!.Any(v=>v==uint.MaxValue)||overlap.EntityOwners!.Any(v=>v==7))throw new Exception("deterministic topmost overlap failed");
        foreach(var visual in visuals)if(visual.Foot!=Coordinates.Screen(visual.X,visual.Y))throw new Exception("standing foot origin mismatch");
        model.Receive(LoginPackets.Pad([120,..BitConverter.GetBytes(uint.MaxValue)]),selected);var reveal=renderer.ComposeObjects(presentation,model,catalog,out _,out _);if(!reveal.EntityOwners!.Any(v=>v==7))throw new Exception("removing top object did not reveal lower object");
        if(catalog.Name("$1928")!="詹姆"||!catalog.Name("$955").Contains("瑟琳娜")||catalog.Get(65535)!=null)throw new Exception("source names/unsupported appearance");
        Json.Save(output,new{status="pass",source="explicit synthetic negatives and original captured wire replay",packets=packets.Length,negatives,duplicateFullUpdates=true,unknownIdIgnored=true,removedIdIgnored=true,targetRemoval=true,mapClears=true,taggedOwnershipLowIdMapCollision=true,duplicateLocalIndexDifferentGlobalSerial=true,mapOverlayClearsTaggedEntity=true,collisionFixture=new{entityId=7,localSourceIndex=42,mapSerials=new[]{7,100007}},unsignedSentinelCollision=true,tenGfxEightDirections=true,namesFromOriginalText=true,exactOverlapStableTopmost=true,removalRevealsLower=true,standingFootOrigins=true});
    }
}
