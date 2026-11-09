using System.Text.Json;
namespace P06;

// T280（#673，R3-D）離線測試：S22 距離音量／ext、登入與選角音樂、頭目曲（出現→SPAWN、死亡→DEATH、SPAWN 播完且頭目在→BATTLE）。
// 只用合成的 music.def 文字、合成封包與記錄器後端；不開音訊裝置、不讀遊戲素材、不連網路。
// 依據：SD§2.3（sound.md）、FU4§6（followups-4.md）、gap-matrix 12.5 R3-D。
public sealed partial class WorldSession {
    internal void SeedSoundFollowup(uint id,ushort x,ushort y){
        Entered=true;MapId=30;TimeReplies=1;CacheEnabled=false;
        Status=JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{id},\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}}");
        Actor=JsonSerializer.Deserialize<OwnActor>($"{{\"ObjectId\":{id},\"X\":{x},\"Y\":{y},\"Direction\":0,\"Name\":\"sound-followup\"}}");
    }
}

static class SoundFollowupTests {
    const string Name="sound-followup";
    sealed class Recorder:IMusicStreamAudio {
        public readonly List<MusicTrack> Started=[];
        public readonly List<MusicTrack> Stopped=[];
        public readonly HashSet<long> Done=[];
        public bool Start(MusicTrack track,double volume){Started.Add(track);return true;}
        public void SetVolume(MusicTrack track,double volume){}
        public void Stop(MusicTrack track)=>Stopped.Add(track);
        public bool Finished(MusicTrack track)=>Done.Contains(track.Number);
        public MusicTrack Last=>Started[^1];
    }
    // 合成定義（自編數值，不是原檔內容）：槽 0→82（登入）、槽 8→5（選角）。
    const string Def=
        "System Setting Begin\r\n\tA=82;\r\n\tB=1;\r\n\tC=2;\r\n\tD=3;\r\n\tE=4;\r\n\tF=5;\r\n\tG=6;\r\n\tH=7;\r\n\tI=5;\r\nSystemMenu Exit\r\n\r\n"+
        ";BOSS\r\nLOC 30 32700 32800 32710 32810\r\nMUSIC 40\r\nINTERVAL 5\r\nSPRITE 1500\r\nSPAWN 150\r\nBATTLE 162 166\r\nDEATH 120\r\n\r\n"+
        ";TWIN\r\nLOC 31\r\nMUSIC 42\r\nTWIN_SPRITE1 1601\r\nTWIN_SPRITE2 1602\r\nSPAWN 171\r\nBATTLE 170\r\nDEATH 172\r\n\r\n"+
        ";TOWN\r\nLOC 4\r\nMUSIC 30\r\nSPRITE 1700\r\nSPAWN 175\r\nBATTLE -1\r\n";
    static MusicContext World(int map,int x,int y,params int[] live)=>new(MusicScene.World,map,x,y,true,1,live.ToHashSet());

    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    // S87 物件（NPC 形狀，與 PointerFlowTests.NpcPack 同欄位），gfx／動作可指定。
    static byte[] Pack(uint id,ushort x,ushort y,ushort gfx,byte action=0)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write(gfx);b.Write(action);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);
        Text(b,"boss");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] Death(uint id)=>LoginPackets.Pad([158,..BitConverter.GetBytes(id),8]);

    [WorldModuleTest("sound-followup")]
    static void Run(WorldModuleTestContext t){Attenuation(t);PacketLevels(t);FrontScreens(t);Boss(t);TwinAndTown(t);Observers(t);}

    // SD§2.3：vol＝100−14·(nx²+ny²)/20、ext＝64−63·(nx+ny)/14；曼哈頓 > 14 夾成 (14,0)。
    static void Attenuation(WorldModuleTestContext t){
        t.Equal(new EmitterLevel(100,64),SoundAttenuation.Level(0,0),"同格：100、64");
        t.Equal(new EmitterLevel(83,42),SoundAttenuation.Level(5,0),"直線 5 格：83、42");
        t.Equal(new EmitterLevel(83,33),SoundAttenuation.Level(-3,-4),"取絕對值：(−3,−4) → 83、33");
        t.Equal(new EmitterLevel(32,1),SoundAttenuation.Level(7,7),"斜向 7＋7：32、1");
        t.Equal(new EmitterLevel(0,1),SoundAttenuation.Level(15,0),"曼哈頓 15 夾成 (14,0)：vol 夾 0、ext 1");
        t.Equal(new EmitterLevel(0,1),SoundAttenuation.Level(int.MinValue,int.MaxValue),"極值不溢位");
        t.Equal(EmitterLevel.Self,SoundAttenuation.Level(null),"沒有發聲體：A06C30 預設");
        t.Equal(.5,EmitterLevel.Self.ExtraVolume,"ext 64 → 0.5（A06C30 0x3F000000）");
        for(int d=0;d<=14;d++)t.Equal(SoundRules.DistanceVolume(d,0),SoundAttenuation.Level(d,0).Volume,$"vol 與 SoundRules 一致（{d}）");
    }

    // S22 每次更新重算；S22 沒有發聲體欄位 → 固定 Self；播完移除。
    static void PacketLevels(WorldModuleTestContext t){
        var w=new WorldSession();long clock=1000;w.PacketMediaClock=()=>clock;
        w.ReceiveSoundPacket(LoginPackets.Pad([22,0,55,0]));
        var first=w.ComposePacketSounds([],1200,_=>500);
        t.Check(first.Length==1&&w.PacketSoundLevel(first[0].Instance)==EmitterLevel.Self,"S22 第一次更新：Self（100、64）");
        int changes=w.PacketSoundLevelChanges;
        w.ComposePacketSounds([],1300,_=>500);
        t.Equal(changes,w.PacketSoundLevelChanges,"S22 每次更新重算，值不變不記變動");
        var snap=WorldModuleTestContext.SnapshotJson(w).GetProperty("extensions").GetProperty("packetSoundLevels");
        t.Check(snap.ValueKind==JsonValueKind.Array&&snap.GetArrayLength()==1&&snap[0].GetProperty("ext").GetInt32()==64,"快照 packetSoundLevels",snap.ToString());
        clock=2000;w.ComposePacketSounds([],2000,_=>500);
        t.Check(w.PacketSoundLevel(first[0].Instance)==null,"播完：移除");
    }

    // 登入（Offline／Rejected 等）→ 槽 0；選角 → 槽 8 循環；EnteringWorld → 停；InWorld 不處理。
    static void FrontScreens(WorldModuleTestContext t){
        var rec=new Recorder();
        try{
            ClientSoundScene.Configure(new SoundDefinitions(MusicDefinition.Parse(Def),null,"合成"),rec,null);
            ClientSoundScene.ObserveFront(LoginState.Offline,1,false,0);
            t.Check(rec.Started.Count==1&&rec.Last is {Music:82,Kind:MusicKind.Login,Loop:false},"登入畫面：槽 0 單次");
            ClientSoundScene.ObserveFront(LoginState.Offline,1,false,16);
            t.Equal(1,rec.Started.Count,"每個 tick 呼叫不重開");
            ClientSoundScene.ObserveFront(LoginState.CharacterList,1,false,100);
            t.Check(rec.Last is {Music:5,Kind:MusicKind.CharacterSelect,Loop:true},"選角畫面：槽 8 循環");
            ClientSoundScene.ObserveFront(LoginState.EnteringWorld,1,false,200);
            t.Check(ClientSoundScene.Music!.Current==null&&rec.Stopped.Count>=2,"進入世界中：停音樂");
            int started=rec.Started.Count;
            ClientSoundScene.ObserveFront(LoginState.InWorld,1,false,300);
            t.Equal(started,rec.Started.Count,"InWorld 交給 Observe(WorldSession)");
            ClientSoundScene.ObserveFront(LoginState.Disconnected,1,true,400);
            t.Check(ClientSoundScene.Music!.Current==null&&rec.Started.Count==started,"靜音：不播");
            ClientSoundScene.ObserveFront(LoginState.Disconnected,1,false,500);
            t.Check(rec.Last.Kind==MusicKind.Login,"斷線回登入畫面：登入音樂");
        }finally{ClientSoundScene.Reset();}
    }

    // FU4§6：出現→SPAWN（區域一次）；SPAWN 播放中不被換鍵淡出；SPAWN 播完且頭目活著→BATTLE（重複）；死亡→DEATH，播完回一般曲。
    static void Boss(WorldModuleTestContext t){
        var rec=new Recorder();var m=new BackgroundMusic(MusicDefinition.Parse(Def),rec,new Random(1));
        m.Update(World(30,32705,32805),0);
        t.Check(rec.Last is {Music:40,Kind:MusicKind.Region},"頭目區一般曲");
        m.BossAppeared(1499,10);t.Equal(1,rec.Started.Count,"不是頭目 sprite：不換曲");
        m.BossAppeared(1500,20);
        t.Check(rec.Last is {Music:150,Kind:MusicKind.BossSpawn,Loop:false},"頭目出現：SPAWN 單次立即換");
        m.Update(World(30,32705,32805,1500),40);m.Update(World(30,32705,32805,1500),2000);
        t.Check(m.Current is {Music:150}&&!m.Fading,"SPAWN 播放中：不因頭目在場換鍵而淡出");
        m.BossAppeared(1500,2100);t.Equal(2,rec.Started.Count,"同一區域 SPAWN 只觸發一次");
        rec.Done.Add(rec.Last.Number);m.Update(World(30,32705,32805,1500),2200);
        t.Check(rec.Last is {Music:166,Kind:MusicKind.Boss},"SPAWN 播完且頭目在：BATTLE（單值最後值）");
        rec.Done.Add(rec.Last.Number);m.Update(World(30,32705,32805,1500),2300);m.Update(World(30,32705,32805,1500),2300+5000);
        t.Check(rec.Last is {Music:166,Kind:MusicKind.Boss}&&rec.Started.Count==4,"BATTLE 播完、頭目仍在：再選 BATTLE");
        m.BossDied(1500,8000);
        t.Check(rec.Last is {Music:120,Kind:MusicKind.BossDeath},"動作 8：DEATH");
        m.Update(World(30,32705,32805),8100);
        t.Check(m.Current is {Music:120}&&!m.Fading,"DEATH 播放中：頭目消失的換鍵不淡出");
        rec.Done.Add(rec.Last.Number);m.Update(World(30,32705,32805),8200);
        t.Check(rec.Last is {Music:40,Kind:MusicKind.Region},"DEATH 播完、頭目不在：回一般曲");
        // 頭目在場但離開視野（LiveSprites 不含）→ 不播 BATTLE。
        var rec2=new Recorder();var away=new BackgroundMusic(MusicDefinition.Parse(Def),rec2,new Random(1));
        away.Update(World(30,32705,32805),0);away.BossAppeared(1500,10);rec2.Done.Add(rec2.Last.Number);
        away.Update(World(30,32705,32805),20);
        t.Check(rec2.Last is {Music:40,Kind:MusicKind.Region},"SPAWN 播完、頭目已離開：一般曲");
        // 不在頭目區、音樂關閉：只登記不換曲。
        var rec3=new Recorder();var off=new BackgroundMusic(MusicDefinition.Parse(Def),rec3,new Random(1));
        off.Update(World(30,1,1),0);off.BossAppeared(1500,10);
        t.Equal(0,rec3.Started.Count(s=>s.Kind==MusicKind.BossSpawn),"玩家不在頭目區矩形內：不播 SPAWN");
        off.Update(World(30,32705,32805) with{Enabled=false},20);off.BossDied(1500,30);
        t.Equal(0,rec3.Started.Count(s=>s.Kind==MusicKind.BossDeath),"音樂關閉：不播 DEATH");
    }

    // 雙頭目：任一隻出現→SPAWN（全域一次）；BATTLE 要兩隻都在；兩隻都死才 DEATH。BATTLE -1 的區域只播 SPAWN。
    static void TwinAndTown(WorldModuleTestContext t){
        var rec=new Recorder();var m=new BackgroundMusic(MusicDefinition.Parse(Def),rec,new Random(1));
        m.Update(World(31,5,5),0);
        m.BossAppeared(1601,10);t.Check(rec.Last is {Music:171,Kind:MusicKind.BossSpawn},"雙頭目之一出現：SPAWN");
        m.BossAppeared(1602,20);t.Equal(2,rec.Started.Count,"另一隻出現：不重播 SPAWN");
        rec.Done.Add(rec.Last.Number);m.Update(World(31,5,5,1601),30);
        t.Check(rec.Last is {Music:42,Kind:MusicKind.Region},"只有一隻在場：不播 BATTLE");
        m.Update(World(31,5,5,1601,1602),40);m.Update(World(31,5,5,1601,1602),900);
        t.Check(rec.Last is {Music:170,Kind:MusicKind.Boss},"兩隻都在：BATTLE");
        m.BossDied(1601,1000);t.Check(rec.Last.Music==170,"一隻死：不播 DEATH");
        m.BossDied(1602,1100);t.Check(rec.Last is {Music:172,Kind:MusicKind.BossDeath},"兩隻都死：DEATH");
        var rec2=new Recorder();var town=new BackgroundMusic(MusicDefinition.Parse(Def),rec2,new Random(1));
        town.Update(World(4,5,5),0);town.BossAppeared(1700,10);
        t.Check(rec2.Last is {Music:175,Kind:MusicKind.BossSpawn},"BATTLE -1 的區域：SPAWN 照播");
        rec2.Done.Add(rec2.Last.Number);town.Update(World(4,5,5,1700),20);
        t.Check(rec2.Last is {Music:30,Kind:MusicKind.Region},"BATTLE -1：SPAWN 播完回一般曲");
    }

    // 封包觀察器：S87（不是自己、動作 ≠ 8）→ BossAppeared；S158 動作 8 → BossDied（gfx 取自物件表）。
    static void Observers(WorldModuleTestContext t){
        var rec=new Recorder();
        try{
            ClientSoundScene.Configure(new SoundDefinitions(MusicDefinition.Parse(Def),null,"合成"),rec,null);
            var music=ClientSoundScene.Music!;music.Update(World(30,32705,32805),0);
            var w=new WorldSession();w.SeedSoundFollowup(123,32705,32805);
            w.Receive(Pack(901,32706,32805,1500,8),Name);
            t.Check(rec.Started.All(s=>s.Kind!=MusicKind.BossSpawn),"S87 帶死亡動作：不觸發 SPAWN");
            w.Receive(Pack(902,32706,32805,1500),Name);
            t.Check(rec.Last is {Music:150,Kind:MusicKind.BossSpawn},"S87 頭目出現：SPAWN");
            w.Receive(Death(902),Name);
            t.Check(rec.Last is {Music:120,Kind:MusicKind.BossDeath},"S158 動作 8：DEATH");
            int n=rec.Started.Count;
            w.Receive(Death(999),Name);w.Receive(LoginPackets.Pad([158,..BitConverter.GetBytes(902u),1]),Name);
            t.Equal(n,rec.Started.Count,"未知物件、其他動作：不換曲");
            bool rejected=false;
            try{w.Receive(LoginPackets.Pad([87,1]),Name);}catch(ProtocolException){rejected=true;}
            t.Check(rejected&&rec.Started.Count==n,"截斷 S87：觀察器略過、由原處理器拒絕");
        }finally{ClientSoundScene.Reset();}
    }
}
