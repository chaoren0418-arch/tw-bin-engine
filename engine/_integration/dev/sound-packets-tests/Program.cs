using P06;
using P24.Audio;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics.Tracing;

// Executes product code with synthetic packets/assets and an in-memory audio transport.
// No game entry point, GUI, network, database, native audio, or game assets.
static class Checks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly List<string> labels=[];
    static long now=10000;
    static string fixture="";
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);labels.Add(label);}
    static void Reject(Action action,string label){try{action();}catch(ProtocolException){Check(true,label);return;}throw new Exception("accepted malformed "+label);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static byte[] Sound(byte mode=0,ushort id=97)=>Packet(b=>{b.Write((byte)22);b.Write(mode);b.Write(id);});
    static byte[] Effect(byte op=106,ushort gfx=1036,ushort? time=null,uint target=123)=>Packet(b=>{
        b.Write(op);if(op==55)b.Write(target);else{b.Write((ushort)32600);b.Write((ushort)32940);}b.Write(gfx);if(time!=null)b.Write(time.Value);
    });
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static WorldSession Session(){var s=new WorldSession();Set(s,"PacketMediaClock",(Func<long>)(()=>now));return s;}
    static void Feed(WorldSession s,byte[] packet)=>s.Receive(packet,"offline");
    static void Fixtures()
    {
        // Test output is built under scratch/issue-192/tests/bin, never engine/assets.
        fixture=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"));
        if(!fixture.Contains(Path.DirectorySeparatorChar+"scratch"+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Build these checks under scratch/<task>/tests/bin before running");
        Directory.CreateDirectory(Path.Combine(fixture,"players"));Directory.CreateDirectory(Path.Combine(fixture,"objects"));
        var pins=new List<object>();
        void Save(string relative,object value)
        {
            var bytes=JsonSerializer.SerializeToUtf8Bytes(value);string path=Path.Combine(fixture,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);
            pins.Add(new{path=relative,size=bytes.Length,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))});
        }
        Save("players/manifest.json",new{schema="p24-player-profiles-v1",profiles=new[]{new{gfx=0,weapon=0,path="players/test-only.json",sha256=new string('0',64),stepMs=640,serverActionMs=new{walk=640,attack=640,spellDir=640,spellNoDir=640},castReleaseMs=new{spellDir=320}}}});
        Save("objects/manifest.json",new{gfx=Array.Empty<int>()});Save("objects/names.json",Array.Empty<string>());
        foreach(var (path,schema) in new[]{("shops/manifest.json","p24-original-shop-appearance-v1"),("encountered-monsters.json","p24-original-encountered-monsters-v1"),("world-appearances.json","p24-original-world-appearances-v1"),("doors/manifest.json","p24-original-inn-door-v1")})
            Save(path,new{schema,profiles=Array.Empty<object>()});
        File.WriteAllBytes(Path.Combine(fixture,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{files=pins}));
    }
    static void Routing()
    {
        var s=Session();int notifications=0;s.CombatSoundStateChanged+=()=>notifications++;
        var actor=JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}")!;
        Set(s,"Actor",actor);
        foreach(ushort gfx in new ushort[]{11722,221,2165,189,191,197})
        {
            long magic=s.MagicEffectCount,item=s.ItemEffectNumber;int inventory=s.InventoryRevision;
            Feed(s,Effect(55,gfx));
            Check(s.MagicEffectCount==magic+1&&s.PacketEffects.Length==0&&s.PacketSounds.Length==0,$"legacy S55 {gfx} has exactly one visual/audio path");
            Check(s.ItemEffectNumber==item+(gfx is 189 or 191 or 197?1:0)&&s.InventoryRevision==inventory+(gfx is 189 or 191 or 197?1:0),$"legacy S55 {gfx} item notification preserved once");
        }
        long count=s.MagicEffectCount;
        Feed(s,Effect(55,221,2));Feed(s,Effect(55,65535));Feed(s,Effect());Feed(s,Sound());
        Check(s.PacketEffects.Length==3&&s.PacketSounds.Length==1&&s.MagicEffectCount==count,"timed S55 precedes legacy parser; S22/S106 use production routing");
        Check(notifications==10,"Receive notifies audio pump once per packet");
        using var snapshot=JsonDocument.Parse(JsonSerializer.Serialize(s.Snapshot()));
        Check(!snapshot.RootElement.GetProperty("unhandled").EnumerateObject().Any(),"all media opcodes handled at shared receive entry");
        foreach(var bytes in new[]{Effect(),Effect(55),Effect(55,221,2)})
        {
            bytes[^1]=1;int effects=s.PacketEffects.Length;long items=s.ItemEffectNumber;
            Reject(()=>Feed(s,bytes),"malformed effect rejected by real receive");
            Check(s.PacketEffects.Length==effects&&s.ItemEffectNumber==items,"malformed effect leaves presentation unchanged");
        }
    }
    static void Decode()
    {
        foreach(byte mode in new byte[]{0,1,255})foreach(ushort id in new ushort[]{0,97,65535})
            Check(SoundPackets.Sound(Sound(mode,id))==new ServerSound(mode,id),$"S22 unsigned fields {mode}/{id}");
        foreach(ushort gfx in new ushort[]{0,221,65535})
        {
            var location=SoundPackets.Effect(Effect(gfx:gfx));
            Check(location==new ServerEffect(null,32600,32940,gfx,null),$"S106 coordinate fields {gfx}");
            foreach(ushort? time in new ushort?[]{null,0,1,65535})
            {
                var bytes=Effect(55,gfx,time,uint.MaxValue);
                Check(bytes.Length==(time==null?8:16),$"S55 variant length {gfx}/{time}");
                Check(SoundPackets.Effect(bytes)==new ServerEffect(uint.MaxValue,0,0,gfx,time),$"S55 unsigned fields {gfx}/{time}");
            }
        }
        foreach(var bytes in new[]{Sound(),Effect(),Effect(55),Effect(55,time:1)})
        {
            Action<byte[]> parse=b=>{if(bytes[0]==22)SoundPackets.Sound(b);else SoundPackets.Effect(b);};
            for(int n=0;n<bytes.Length;n++)
            {
                // A timed S55 truncated to 8 bytes is indistinguishable from a valid short S55
                // only when its eighth byte is zero. Use time=1 to make the truncation invalid.
                int length=n;Reject(()=>parse(bytes[..length]),$"truncation {bytes[0]}/{bytes.Length}/{n}");
            }
            var dirty=(byte[])bytes.Clone();dirty[^1]=1;Reject(()=>parse(dirty),$"nonzero padding {bytes[0]}/{bytes.Length}");
            if(bytes[0]==55&&bytes.Length==8)
                Check(SoundPackets.Effect([..bytes,..new byte[8]]).Time==0,"zero extension is legal timed S55");
            else Reject(()=>parse([..bytes,..new byte[8]]),$"extra block {bytes[0]}/{bytes.Length}");
        }
        Reject(()=>SoundPackets.Sound(Effect()),"wrong sound opcode");
        Reject(()=>SoundPackets.Effect(Sound()),"wrong effect opcode");
    }
    static PacketEffectAppearance Appearance()=>new(new MapEffectAppearance(JsonSerializer.SerializeToUtf8Bytes(new{
        id=1036,flags=0,tickRate=24,
        sequence=new[]{new{frame=0,ticks=12,events="[97"},new{frame=1,ticks=12,events=""}},
        frames=new[]{new{index=0,bounds=new[]{0,0,0,0},blocks=new[]{new{a=0,b=0,z=0,order=0,pixels=new[]{new[]{0,0,32767}}}}},new{index=1,bounds=new[]{0,0,0,0},blocks=new[]{new{a=0,b=0,z=0,order=0,pixels=new[]{new[]{0,0,31}}}}}}
    })));
    static void Effects()
    {
        var s=Session();var appearance=Appearance();Feed(s,Effect());
        Check(s.PacketEffects.Length==1&&s.PacketSounds.Length==0,"S106 stores gfx only, no direct sound");
        int draws=0;var first=s.PacketEffectFrames(now,_=>appearance,(_,x,y)=>{draws++;Check(x==32600&&y==32940,"coordinate draw position");});
        Check(first.Single().Frame=="0"&&draws==1,"first frame drawn once");
        Check(s.PacketEffectFrames(now+500,_=>appearance).Single().Frame=="1","advance animation frame");
        Check(s.PacketEffectFrames(now+1000,_=>appearance).Length==0&&s.PacketEffects.Length==0,"one-shot retires at real sequence end");

        var actor=JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}")!;
        Set(s,"Actor",actor);Feed(s,Effect(55,time:2));
        Check(s.PacketEffectFrames(now+1500,_=>appearance).Single().Frame=="1","timed effect loops until policy deadline");
        Set(s,"Actor",actor with{X=32601,Y=32941});
        Check(s.PacketEffectFrames(now+1600,_=>appearance).Single() is {X:32601,Y:32941},"S55 follows current target position");
        Check(s.PacketEffectFrames(now+2000,_=>appearance).Length==0,"timed effect retires exactly at deadline");
        Feed(s,Effect(55,time:0));Check(s.PacketEffectFrames(now+1000,_=>appearance).Length==0,"time zero is one sequence");
        Feed(s,Effect(55,time:2));Set(s,"Actor",null);
        Check(s.PacketEffects.Length==0,"missing target retires attached effect");
        Feed(s,Effect(55,time:2));Check(s.PacketEffects.Length==0,"unknown target never leaves orphan effect");
        Feed(s,Effect(gfx:65535));Check(s.PacketEffectFrames(now,_=>null).Length==0&&s.PacketMediaNotice.Contains("缺少"),"missing gfx has explicit notice and no placeholder");
        Feed(s,Effect());Feed(s,Sound());s.Reset();
        Check(s.PacketEffects.Length==0&&s.PacketSounds.Length==0,"reset retires media");
        Feed(s,Effect());Feed(s,Sound());s.Receive(LoginPackets.Pad([206,4,0,0]),"offline");
        Check(s.PacketEffects.Length==0&&s.PacketSounds.Length==0,"S206 map boundary retires media");
        Feed(s,Effect());Feed(s,Sound());s.Receive(LoginPackets.Pad([149,0xeb,0xa7,1,0,0,0]),"offline");
        Check(s.PacketEffects.Length==0&&s.PacketSounds.Length==0,"S149 teleport retires media");
        for(int i=0;i<300;i++){Feed(s,Effect());Feed(s,Sound());}
        Check(s.PacketEffects.Length==256&&s.PacketSounds.Length==256,"pending queues bounded under burst");
        int count=s.PacketSounds.Length;var invalid=Sound();invalid[^1]=1;
        Reject(()=>Feed(s,invalid),"invalid receive rejected");Check(s.PacketSounds.Length==count,"invalid receive does not mutate queue");
    }
    static async Task Audio()
    {
        // T173: the first byte is a mode (0 once, 1 ambience loop, 2 stop), not a repeat count.
        var schedule=SoundPackets.Schedule(new(0,65535),500);
        var sent=new List<int>();var cursor=new SoundCursor(schedule,new Random(1));
        cursor.Advance(0,(id,_,_)=>sent.Add(id));cursor.Advance(499,(id,_,_)=>sent.Add(id));
        Check(sent.SequenceEqual(new[]{65535}),"soundId not gfx, first play once");
        cursor.Advance(500,(id,_,_)=>sent.Add(id));cursor.Advance(5000,(id,_,_)=>sent.Add(id));cursor.Advance(5000,(id,_,_)=>sent.Add(id));
        Check(sent.Count==1,"mode 0 is a single play, no duplicate pump replay");
        Check(SoundPackets.Schedule(new(1,97),1).Loop,"mode 1 loops");

        var s=Session();Feed(s,Sound(1));var audio=new FakeAudio();var controller=new CombatSfxController(audio,new Random(1),_=>{});
        controller.SetControls(1,false,true);
        void Pump(long time,bool enabled=true){time+=WorldSession.PacketSoundMatchMs;controller.Pump(s.WorldEpoch,s.ComposePacketSounds([],time,_=>500),time,enabled);}
        Pump(now);Pump(now);Check(audio.Plays.SequenceEqual(new[]{97}),"S22 uses existing dispatcher exactly once");
        Pump(now+500);Check(audio.Plays.Count==2,"ambience loop uses WAV interval");
        s.Receive(LoginPackets.Pad([206,4,0,0]),"offline");Pump(now+600);
        Check(audio.Retired.Count==1&&audio.Plays.Count==2,"map change retires audio owner and cancels the loop");
        // Keep the clock monotonic: the #433 per-controller 200ms cooldown applies when no SoundGate is set.
        now+=2000;Feed(s,Sound());Pump(now);controller.SetControls(1,true,false);Pump(now+1,false);
        Check(audio.Retired.Count==2,"disabled dispatch retires packet owner");
        controller.SetControls(1,false,true);Pump(now+2);Check(audio.Plays.Count==3,"reenable does not replay consumed packet");
        Feed(s,Sound(id:65535));s.PacketSoundSnapshot(now,_=>null);
        Check(s.PacketSounds.Length==0&&s.PacketMediaNotice.Contains("未播放"),"unapproved sound explicit, never mapped to another id");
        await controller.Shutdown();

        var delayed=new FakeAudio{Delay=true};var late=new CombatSfxController(delayed,new Random(1),_=>{});
        late.SetControls(1,false,true);Feed(s,Sound());late.Pump(s.WorldEpoch,s.ComposePacketSounds([],now+100,_=>500),now+100,true);
        s.Reset();late.Pump(s.WorldEpoch,[],now+1,true);delayed.Pending.SetResult(FakeAudio.Ok);
        await Task.Delay(30);Check(delayed.Plays.Count==0,"late registration completion cannot play after reset");await late.Shutdown();
    }
    static async Task Dedup()
    {
        CombatSoundInstance Action(long at,int[] ids)=>new("actor/123",1,123,0,0,at,at+1000,new([new(0,0,ids)],1000,false));
        foreach(bool packetFirst in new[]{false,true})
        {
            var s=Session();var audio=new FakeAudio();var controller=new CombatSfxController(audio,new Random(1),_=>{});
            controller.SetControls(1,false,true);
            void Pump(long at,params CombatSoundInstance[] actions)=>controller.Pump(s.WorldEpoch,s.ComposePacketSounds(actions,at,_=>500),at,true);
            var action=Action(now+(packetFirst?50:-50),[97]);
            if(packetFirst){Feed(s,Sound());Pump(now);Pump(now+50,action);}
            else{Pump(now-50,action);Feed(s,Sound());}
            Pump(now+100);Pump(now+100);
            Check(audio.Plays.Count==1,$"same frame/S22 first play suppressed for arrival order {packetFirst}");
            Pump(now+600);Check(audio.Plays.Count==1,"suppressed mode-0 S22 does not play later");
            await controller.Shutdown();
        }
        foreach(var ids in new[]{new[]{98},new[]{97,98}})
        {
            var s=Session();Feed(s,Sound());var result=s.ComposePacketSounds([Action(now,ids)],now+100,_=>500);
            Check(result.Last().Schedule.Frames[0].Candidates.SequenceEqual(new[]{97}),"different or ambiguous frame candidates do not suppress S22");
        }
        {
            var s=Session();Feed(s,Sound());Feed(s,Sound());
            var result=s.ComposePacketSounds([Action(now,[97])],now+100,_=>500);
            Check(result.Length==2&&result[1].Schedule.Frames[0].Candidates.Length==0,"one frame matches one S22; same-ID mode-0 replay while playing is dropped (A05EA0)");
            s.Reset();Feed(s,Sound());
            Check(s.ComposePacketSounds([],now+100,_=>500).Single().Schedule.Frames[0].Candidates.Length==1,"epoch reset clears dedup history");
        }
        {
            var s=Session();Feed(s,Sound());
            Check(s.ComposePacketSounds([Action(now-101,[97])],now+100,_=>500).Last().Schedule.Frames[0].Candidates.Length==1,"outside match window not suppressed");
        }
    }
    static async Task ClientHook()
    {
        // Bypass the Form constructor: no window, renderer assets, audio device or login.
        var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
        GC.SuppressFinalize(form);
        using var login=new LoginConnection();var s=login.World;
        Set(login,"State",LoginState.InWorld);Set(s,"Entered",true);Set(s,"MapId",(ushort)0);Set(s,"TimeReplies",1);Set(s,"CacheEnabled",false);
        Set(s,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(s,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Set(s,"PacketMediaClock",(Func<long>)(()=>Environment.TickCount64-150));
        var audio=new FakeAudio();var controller=new CombatSfxController(audio,new Random(1),_=>{});
        var assets=new PacketSoundAssets("unused-offline");
        ((Dictionary<int,double?>)typeof(PacketSoundAssets).GetField("durations",F)!.GetValue(assets)!)[97]=10000;
        Set(form,"login",login);Set(form,"combatSfx",controller);Set(form,"combatSfxScene",new CombatSfxScene(fixture));
        Set(form,"packetSoundAssets",assets);Set(form,"audioSettings",new PlayerAudioSettings());
        var pump=typeof(ClientForm).GetMethod("PumpCombatSfx",F)!;
        Feed(s,Sound());pump.Invoke(form,null);pump.Invoke(form,null);
        Check(audio.Plays.SequenceEqual(new[]{97}),"real ClientForm PumpCombatSfx hook dispatches received S22 exactly once");
        s.Reset();pump.Invoke(form,null);
        Check(audio.Retired.Count==1,"real client hook retires packet audio on reset");
        await controller.Shutdown();
        GC.SuppressFinalize(form);
    }
    static void Wav()
    {
        using var stream=new MemoryStream();using var b=new BinaryWriter(stream,System.Text.Encoding.ASCII,true);
        b.Write(0x46464952u);b.Write(836u);b.Write(0x45564157u);b.Write(0x20746d66u);b.Write(16u);
        b.Write((ushort)1);b.Write((ushort)1);b.Write(8000u);b.Write(8000u);b.Write((ushort)1);b.Write((ushort)8);
        b.Write(0x61746164u);b.Write(800u);b.Write(new byte[800]);stream.Position=0;
        Check(PacketSoundAssets.WaveDuration(stream)==100,"PCM RIFF duration with synthetic bytes");
        stream.Position=0;b.Write(0u);stream.Position=0;
        try{PacketSoundAssets.WaveDuration(stream);throw new Exception("bad RIFF accepted");}catch(InvalidDataException){Check(true,"bad WAV rejected");}
    }
    static async Task<int> Main(string[] args)
    {
        try
        {
            using var guard=new SocketGuard();Fixtures();Decode();Routing();Effects();await Audio();await Dedup();await ClientHook();Wav();
            File.WriteAllText(args[0],JsonSerializer.Serialize(new{status="pass",checks=labels.Count,scope="合成封包／素材及記憶體音訊；非真機",labels},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS {labels.Count} sound packet checks");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    sealed class SocketGuard:EventListener
    {
        protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}
        protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network forbidden in offline checks");
    }
}
sealed class FakeAudio:IAudioPlayback
{
    public static readonly AudioReply Ok=new(true,"MemoryOnly",null,0,[]);
    public readonly List<int> Plays=[];public readonly List<AudioOwner> Retired=[];public bool Delay;
    public readonly TaskCompletionSource<AudioReply> Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<AudioReply> RegisterAsync(AudioOwner owner)=>Delay?Pending.Task:Task.FromResult(Ok);
    public Task<AudioReply> RetireAsync(AudioOwner owner){Retired.Add(owner);return Task.FromResult(Ok);}
    public Task<AudioReply> PlayAsync(int id,AudioOwner owner){Plays.Add(id);return Task.FromResult(Ok);}
    public Task<AudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>Task.FromResult(Ok);
    public Task<AudioReply> ShutdownWithinAsync(TimeSpan budget)=>Task.FromResult(Ok);
    public Task WhenReleased=>Task.CompletedTask;
    public AudioReply LastMaintenance=>Ok;
}
