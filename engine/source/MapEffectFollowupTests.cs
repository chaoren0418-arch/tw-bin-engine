using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using P24.Audio;
using P24.HostAudio;
namespace P06;

// T332／R6-A（#778）離線測試：S55 用對象收包當下的朝向與位置、只有 7 個 gfx 每格跟著對象（SV§2.5），
// 以及地圖特效 `<`／`[` 的單次音效（A060F0：距離音量、與 `^` 共用 200ms 冷卻；sound.md §2.4、REA B04）。
// 由 --module-tests 執行（名稱 map-effect-followup）。只用合成封包、合成地圖特效 JSON、記錄器與合成時鐘；
// 不讀原客戶端、不讀 engine/assets、不開音訊裝置、遊戲、網路或 DB。
static class MapEffectFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Set(object o,string name,object? value){
        var t=o.GetType();
        if(t.GetProperty(name,F) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
        (t.GetField(name,F)??t.GetField($"<{name}>k__BackingField",F))!.SetValue(o,value);
    }
    static ConcurrentDictionary<uint,WorldObject> Objects(WorldSession w)=>(ConcurrentDictionary<uint,WorldObject>)w.GetType().GetField("objects",F)!.GetValue(w)!;
    // 3.81 S_SkillSound／S_EFFECT（S55）帶 time 的 16-byte 版本：C(55) D(target) H(gfx) H(time)。
    static byte[] S55(uint target,ushort gfx,ushort time=2){
        using var s=new MemoryStream();using var b=new BinaryWriter(s);
        b.Write((byte)55);b.Write(target);b.Write(gfx);b.Write(time);
        return LoginPackets.Pad(s.ToArray());
    }
    static WorldSession Seed(Func<long> clock){
        var w=new WorldSession();w.PacketMediaClock=clock;
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"Direction\":3}")!);
        Objects(w)[456]=JsonSerializer.Deserialize<WorldObject>("{\"ObjectId\":456,\"Gfx\":65000,\"X\":32603,\"Y\":32942,\"Direction\":5,\"HpByte\":75}")!;
        return w;
    }
    // ticks 12 per step = 500 ms; two steps = 1000 ms cycle.
    static MapEffectAppearance Appearance(int id,params string[] events){
        var steps=string.Join(",",events.Select(e=>$"{{\"frame\":0,\"ticks\":12,\"events\":\"{e}\"}}"));
        string json=$"{{\"id\":{id},\"flags\":0,\"tickRate\":24,\"frames\":[{{\"index\":0,\"bounds\":[0,0,0,0],\"blocks\":[{{\"a\":0,\"b\":0,\"z\":0,\"order\":0,\"pixels\":[[0,0,1]]}}]}}],\"sequence\":[{steps}]}}";
        return new(Encoding.UTF8.GetBytes(json));
    }

    sealed class LoopRecorder:IAmbientLoopAudio {
        public readonly List<(MapAmbientVoice Voice,int Volume)> Started=[];
        public bool Start(MapAmbientVoice voice,int volume){Started.Add((voice,volume));return true;}
        public void SetVolume(MapAmbientVoice voice,int volume){}
        public void Stop(MapAmbientVoice voice){}
    }
    sealed class OneShotRecorder:IMapOneShotAudio {
        public readonly List<(MapOneShotVoice Voice,int Volume)> Played=[];
        public double? Scale;public bool Accept=true;
        public bool Play(MapOneShotVoice voice,int volume){if(Accept)Played.Add((voice,volume));return Accept;}
        public void SetScale(double scale)=>Scale=scale;
    }
    sealed class StreamRecorder:IAudioStream {
        public readonly List<(string[] Files,bool Loop,double Volume)> Started=[];
        public readonly HashSet<long> Done=[];
        long next;
        public long Start(IReadOnlyList<string> candidates,bool loop,double volume){Started.Add((candidates.ToArray(),loop,volume));return ++next;}
        public void SetVolume(long handle,double volume){}
        public void Stop(long handle)=>Done.Add(handle);
        public bool Finished(long handle)=>Done.Contains(handle);
    }

    [WorldModuleTest("map-effect-followup")]
    static void Run(WorldModuleTestContext t){
        FollowList(t);PacketDirectionAndAnchor(t);OneShotSound(t);OneShotWiring(t);ProductBackend(t);
    }

    static void FollowList(WorldModuleTestContext t){
        t.Check(PacketEffectRules.Following.OrderBy(x=>x).SequenceEqual([6985,7179,7834,7836,8150,8152,8981]),"SV§2.5: exactly seven following gfx",PacketEffectRules.Following);
        t.Check(!PacketEffectRules.Follows(1036)&&!PacketEffectRules.Follows(0)&&PacketEffectRules.Follows(8981),"other gfx do not follow");
    }

    // SV§2.5：S55 收包立刻在對象位置、以對象朝向播放；只有 7 個 gfx 跟著對象。
    static void PacketDirectionAndAnchor(WorldModuleTestContext t){
        long clock=1000;var w=Seed(()=>clock);
        var appearance=new PacketEffectAppearance(Appearance(70010,"",""));
        w.Receive(S55(456,1036),"offline");
        t.Check(w.PacketEffects is [{Direction:5,Follows:false} e]&&e.Origin==(32603,32942),"S55 stores target heading and position at receipt",w.PacketEffects);
        w.Receive(S55(123,6985),"offline");
        t.Check(w.PacketEffects.Length==2&&w.PacketEffects[1] is {Direction:3,Follows:true}&&w.PacketEffects[1].Origin==(32600,32940),"self target: own heading; 6985 follows",w.PacketEffects);

        // 對象移動並轉向：不跟隨的留在收包位置、朝向不變；跟隨的每格重讀位置，朝向仍是收包時的。
        Objects(w)[456]=Objects(w)[456] with{X=32610,Y=32950,Direction=1};
        Set(w,"Actor",w.Actor! with{X=32605,Y=32945,Direction=7});
        clock=1100;
        var drawn=new List<(int X,int Y)>();
        var frames=w.PacketEffectFrames(clock,_=>appearance,(_,x,y)=>drawn.Add((x,y)));
        t.Check(frames.Length==2&&frames[0] is {X:32603,Y:32942,Direction:5},"non-following S55 stays at receipt position",frames);
        t.Check(frames[1] is {X:32605,Y:32945,Direction:3},"following gfx tracks the current position each frame",frames);
        t.Check(drawn.SequenceEqual([(32603,32942),(32605,32945)]),"drawn at the same positions",drawn);

        // 對象離開可見世界：照舊結束（PrunePacketMedia 不變）。
        Objects(w).TryRemove(456,out _);
        t.Check(w.PacketEffects.All(p=>p.Packet.Target!=456),"target gone still retires the effect");

        // S106 座標特效：方向 0、位置是封包座標。
        using var s=new MemoryStream();using(var b=new BinaryWriter(s,Encoding.UTF8,true)){b.Write((byte)106);b.Write((ushort)32620);b.Write((ushort)32960);b.Write((ushort)1036);}
        w.Receive(LoginPackets.Pad(s.ToArray()),"offline");
        var loc=w.PacketEffectFrames(clock,_=>appearance).Single(v=>v.X==32620);
        t.Check(loc is {Y:32960,Direction:0},"S106 location effect: packet coordinates, direction 0",loc);

        // 地圖素材沒有方向：任何方向取同一格與同一 timeline。
        t.Check(appearance.At(600,5).Key==appearance.At(600).Key&&ReferenceEquals(appearance.TimelineFor(5),appearance.Timeline),"map appearance ignores direction");
        t.Check(appearance.At(0,99).Key==appearance.At(0).Key,"out-of-range direction falls back to 0");
    }

    // sound.md §2.1／§2.4：`<`、`[` 走 A060F0，單次、可重疊、每 id 全域 200ms 冷卻（與 `^` 同一份）。
    static void OneShotSound(WorldModuleTestContext t){
        var cue=MapAmbientCue.Parse("[97");
        t.Check(cue.Mode==1&&MapAmbientSound.HasSound(Appearance(1,"[97"))&&!MapAmbientSound.HasAmbient(Appearance(1,"[97")),"`[` is a sound but not ambient");
        t.Check(MapAmbientSound.HasSound(Appearance(1,"<3"))&&!MapAmbientSound.HasSound(Appearance(1,"",""))&&!MapAmbientSound.HasSound(Appearance(1,"]40100")),"`<` is a sound; plain or `]` frames are not");

        var self=(X:100,Y:100);
        var loops=new LoopRecorder();var once=new OneShotRecorder();var cooldown=new SoundCooldown();
        var model=new MapAmbientSound(loops,new Random(7),cooldown,once);
        var lt=Appearance(1100,"<300","");
        model.Update([new("a",100,100,lt,0)],self,10000,true);
        t.Check(once.Played is [{Voice:{SoundId:300,Mode:0,Emitter:"a"},Volume:100}],"`<` frame entry plays once at distance volume",once.Played);
        t.Equal(0,loops.Started.Count,"`<` never starts a loop");
        model.Update([new("a",100,100,lt,100)],self,10100,true);
        t.Equal(1,once.Played.Count,"same frame: not replayed");
        model.Update([new("a",100,100,lt,500)],self,10500,true);
        t.Equal(1,once.Played.Count,"empty frame: nothing");
        // 下一輪：A060F0 在 A091C0 之前，所以先用上一輪音量（100），之後才改成 5 格的 83。
        var five=(X:105,Y:100);
        model.Update([new("a",100,100,lt,1000)],five,11000,true);
        t.Check(once.Played.Count==2&&once.Played[1].Volume==100,"next cycle replays (overlap allowed) with previous-round volume",once.Played);
        model.Update([new("a",100,100,lt,2000)],five,12000,true);
        t.Check(once.Played.Count==3&&once.Played[2].Volume==83,"then the updated distance volume",once.Played);

        // `[` 與 `<` 相同；共用冷卻：同一 id 同時兩個發聲體只播一個；200ms 後再播。
        once=new();cooldown=new();model=new(loops,new Random(7),cooldown,once);
        var br=Appearance(1101,"[400","");
        model.Update([new("b1",100,100,br,0),new("b2",101,100,br,0)],self,20000,true);
        t.Check(once.Played is [{Voice:{SoundId:400,Mode:1,Emitter:"b1"}}],"`[` plays; same id in the same tick is blocked by the shared cooldown",once.Played);
        model.Update([new("b1",100,100,br,0),new("b2",101,100,br,0),new("b3",102,100,br,0)],self,20199,true);
        t.Equal(1,once.Played.Count,"cooldown still blocks at +199");
        model.Update([new("b1",100,100,br,0),new("b2",101,100,br,0),new("b3",102,100,br,0),new("b4",103,100,br,0)],self,20200,true);
        t.Check(once.Played.Count==2&&once.Played[1].Voice.Emitter=="b4","cooldown expires at +200",once.Played);

        // 冷卻與 `^` 同一份（sound.md §2.7）。
        once=new();loops=new();cooldown=new();model=new(loops,new Random(7),cooldown,once);
        model.Update([new("loop",100,100,Appearance(1102,"^500",""),0),new("shot",100,100,Appearance(1103,"<500",""),0)],self,30000,true);
        t.Check(loops.Started.Count==1&&once.Played.Count==0,"`^` and `<` share the per-id cooldown",new{loops=loops.Started.Count,once=once.Played.Count});

        // 音量 0（太遠）：原版仍播（無聲）並佔冷卻；引擎不送 voice，但冷卻一樣佔用。
        once=new();cooldown=new();model=new(new LoopRecorder(),new Random(7),cooldown,once);
        var far=Appearance(1104,"<600","");
        model.Update([new("far",130,100,far,0),new("near",100,100,far,0)],self,40000,true);
        t.Equal(0,once.Played.Count,"zero-volume emitter sends nothing but consumes the cooldown");
        t.Check(!cooldown.TryAcquire(600,40100)&&cooldown.TryAcquire(600,40200),"cooldown taken by the silent play");

        // 三類音量都是 0（audible=false）：不播、不佔冷卻。
        once=new();cooldown=new();model=new(new LoopRecorder(),new Random(7),cooldown,once);
        model.Update([new("m",100,100,Appearance(1105,"<700",""),0)],self,50000,false);
        t.Check(once.Played.Count==0&&cooldown.TryAcquire(700,50000),"categories silent: no play, cooldown untouched");

        // 沒有單次音效後端：不播、不佔冷卻。
        cooldown=new();model=new(new LoopRecorder(),new Random(7),cooldown);
        model.Update([new("n",100,100,Appearance(1106,"<800",""),0)],self,60000,true);
        t.Check(model.OneShotsPlayed==0&&cooldown.TryAcquire(800,60000),"no one-shot backend: inert, cooldown untouched");

        // 候選：抽到 0 略過不重抽；多候選抽一個；沒有自身角色不選音。
        once=new();model=new(new LoopRecorder(),new Random(7),new SoundCooldown(),once);
        model.Update([new("z",100,100,Appearance(1107,"<0",""),0)],self,70000,true);
        t.Equal(0,once.Played.Count,"candidate 0 is skipped");
        model.Update([new("c",100,100,Appearance(1108,"<901<902<903",""),0)],self,70000,true);
        t.Check(once.Played is [{Voice.SoundId:901 or 902 or 903}],"multiple candidates choose one",once.Played);
        model.Update([new("s",100,100,Appearance(1109,"<950",""),0)],null,70000,true);
        t.Check(once.Played.All(p=>p.Voice.SoundId!=950),"no self actor: no selection");
        // 後端拒絕：不計數。
        once=new(){Accept=false};model=new(new LoopRecorder(),new Random(7),new SoundCooldown(),once);
        model.Update([new("r",100,100,Appearance(1110,"<960",""),0)],self,80000,true);
        t.Equal(0L,model.OneShotsPlayed,"failed play is not counted");
    }

    // 產品掛載：Observe 每次把 OneShotBackend 交給 session 的模型；沒有地圖快取時沒有音源。
    static void OneShotWiring(WorldModuleTestContext t){
        var w=new WorldSession();var loops=new LoopRecorder();var once=new OneShotRecorder();
        var oldLoop=MapAmbientSound.Backend;var oldOnce=MapAmbientSound.OneShotBackend;
        try{
            MapAmbientSound.Backend=loops;MapAmbientSound.OneShotBackend=once;
            MapAmbientSound.Observe(w,1);
            t.Check(ReferenceEquals(MapAmbientSound.Of(w)?.OneShotAudio,once),"Observe wires the one-shot backend");
            t.Check(MapAmbientSound.Sources(w,1).Length==0&&once.Played.Count==0&&once.Scale==null,"no map cache: no sources, no scale push");
            MapAmbientSound.OneShotBackend=null;MapAmbientSound.Observe(w,2);
            t.Check(MapAmbientSound.Of(w)?.OneShotAudio==null,"backend removed: model follows");
            t.Equal(0d,MapAmbientSound.OneShotScale(w),"not renderable / no 6016 host: one-shot scale 0");
        }finally{MapAmbientSound.Backend=oldLoop;MapAmbientSound.OneShotBackend=oldOnce;}
    }

    static void ProductBackend(WorldModuleTestContext t){
        var stream=new StreamRecorder();var audio=new ProductOneShotAudio(stream);
        t.Check(audio.Play(new(1,300,"a",0),83),"product one-shot starts");
        t.Check(stream.Started is [{Loop:false} s]&&s.Files.SequenceEqual(["300.mp3","300.wav"])&&Math.Abs(s.Volume-.83)<1e-9,"single play, mp3 then wav, distance volume × scale",stream.Started);
        audio.SetScale(.5);audio.Play(new(2,301,"a",1),100);
        t.Check(Math.Abs(stream.Started[^1].Volume-.5)<1e-9,"scale applied",stream.Started);
        t.Equal(2,audio.Live,"two live handles");
        stream.Done.Add(1);t.Equal(1,audio.Live,"finished handles are pruned");
        audio.SetScale(0);t.Check(!audio.Play(new(3,302,"a",0),100)&&stream.Started.Count==2,"scale 0: nothing started");
        audio.SetScale(double.NaN);t.Equal(0d,audio.Scale,"non-finite scale clamps to 0");
    }
}
