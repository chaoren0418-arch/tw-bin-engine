using System.Text.Json;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task GenerationRegression(){
        using var c=new LoginConnection();var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        c.State=LoginState.CharacterList;c.sendWorld=_=>pending.Task;
        var old=c.SelectForProbe("old");c.Cancel();c.State=LoginState.CharacterList;c.Detail="new generation";
        int current=c.generation;pending.SetException(new IOException("delayed failure from old write"));await old;
        if(c.generation!=current||c.State!=LoginState.CharacterList||c.Detail!="new generation")throw new Exception("old write fault damaged new session");
    }
}
public static class WorldRegression {
    public static async Task Run(string capture,string output){
        await LoginConnection.GenerationRegression();var root=Json.Read(capture);var packets=root.GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
        string selected=LoginConnection.CapturedCharacterName(packets);int expectedAc=System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(packets.Last(p=>p[0]==8).AsSpan(32));var model=new WorldSession();bool started=false;int count=0;
        foreach(var p in packets){if(p[0]==223)started=true;if(!started)continue;model.ProbeSent=true;model.Receive(p,selected);count++;}
        if(!model.Ready||!model.Renderable||model.Actor==null||model.Actor.Gfx!=0||model.Status?.Ac!=expectedAc)throw new Exception("real captured world mismatch");
        var rejected=new List<string>();
        foreach(byte opcode in new byte[]{8,206,87,123,225,33}){
            var original=packets.First(p=>p[0]==opcode);var bad=original[..2];bool failed=false;
            try{model.Receive(bad,selected);}catch(ProtocolException){failed=true;}if(!failed)throw new Exception("truncation accepted "+opcode);rejected.Add($"truncated 0x{opcode:X2}");
        }
        var own=packets.First(p=>p[0]==87&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))==model.Status!.ObjectId);
        bool wrongName=false;try{model.Receive(own,"another name");}catch(ProtocolException){wrongName=true;}if(!wrongName)throw new Exception("wrong self name accepted");
        var unsupported=(byte[])own.Clone();unsupported[9]=0xa2;unsupported[10]=4;model.Receive(unsupported,selected);if(model.Renderable||model.SupportedAppearance)throw new Exception("unsupported gfx substituted");
        model.Receive(own,selected);model.Receive(Convert.FromHexString("CE04000000000000"),selected);if(model.Renderable||model.SupportedMap)throw new Exception("unsupported map substituted");
        model.Reset();if(model.Ready||model.Actor!=null||model.Status!=null||model.MapId!=null)throw new Exception("reset retained world");
        Json.Save(output,new{status="pass",capture,source="replay of real JAR packet capture; mutated negatives explicitly synthetic",packets=count,rejected,wrongName,unsupportedGfxRejected=true,unsupportedMapRejected=true,generationDelayedFault=true,resetClears=true});
    }
}
