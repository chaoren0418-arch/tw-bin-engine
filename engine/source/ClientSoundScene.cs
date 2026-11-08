namespace P06;

// T172（#446）：背景音樂與地圖環境音的產品掛載點。
// 產品目前沒有串流／循環播放後端（P24.Audio 只播 pin 過的取樣，見 docs/protocol/music.md），
// 所以 Configure 尚未被呼叫；未設定時所有掛載點都是 no-op，不建立任何狀態、不讀 Text 包。
public static class ClientSoundScene
{
    static BackgroundMusic? music;
    static MapEnvSound? env;
    static SoundVolumes volumes=new();
    public static BackgroundMusic? Music=>music;
    public static MapEnvSound? EnvSound=>env;
    public static string Status {get;private set;}="未設定後端";

    // 執行期從使用者原客戶端唯讀載入 music.def／envsound.def；任一後端為 null 就不建立對應控制器。
    public static void Configure(string clientRoot,IMusicStreamAudio? musicBackend,IEnvSoundAudio? envBackend,SoundVolumes? categoryVolumes=null)
    {
        Reset();
        if(categoryVolumes!=null)volumes=categoryVolumes;
        if(musicBackend==null&&envBackend==null)return;
        var defs=SoundDefinitionSource.Load(clientRoot);Status=defs.Status;
        Configure(defs,musicBackend,envBackend);
    }
    public static void Configure(SoundDefinitions defs,IMusicStreamAudio? musicBackend,IEnvSoundAudio? envBackend)
    {
        if(defs.Music is {} m&&musicBackend!=null)music=new(m,musicBackend,Random.Shared);
        if(defs.EnvSound is {} e&&envBackend!=null)env=new(e,envBackend,Random.Shared,SoundCooldown.Shared);
    }
    public static void Reset(){music?.Clear();env?.Clear();music=null;env=null;Status="未設定後端";}

    static (bool On,double Gain) Host(WorldSession session)=>session.Effects6016 is {Closed:false,Muted:false} host&&host.Gain>0?(true,host.Gain):(false,0);

    // 登入／選角畫面（LoginConnection.State）。InWorld 由 Observe(WorldSession) 負責。
    public static MusicScene Scene(LoginState state)=>state switch
    {
        LoginState.InWorld=>MusicScene.World,
        LoginState.CharacterList or LoginState.ManagingCharacters=>MusicScene.CharacterSelect,
        LoginState.EnteringWorld or LoginState.SwitchingCharacter=>MusicScene.None,
        _=>MusicScene.Login,
    };
    public static void ObserveScene(MusicScene scene,double gain,bool muted,long now)
    {
        if(scene==MusicScene.World)return;
        music?.Update(new(scene,0,0,0,!muted&&gain>0,volumes[SoundCategory.Music]*gain),now);
        env?.Clear();
    }

    // 世界畫面：Renderer.ComposeMapEffects 每次組畫面呼叫一次。
    public static void Observe(WorldSession session,long now)
    {
        if(music==null&&env==null)return;
        var (on,gain)=Host(session);
        bool ready=on&&session.Renderable&&session.MapId is not null&&session.Actor is not null;
        int map=session.MapId??0,x=session.Actor?.X??0,y=session.Actor?.Y??0;
        music?.Update(new(ready?MusicScene.World:MusicScene.None,map,x,y,ready,volumes[SoundCategory.Music]*gain),now);
        env?.Update(new(map,x,y,session.Ambience.At(now).Night,ready,volumes[SoundCategory.EnvSound]*gain),now);
    }

    // Combat.ReceiveCombat：自己是攻擊者或對象時記錄戰鬥時間（A4DF10）。對象帶 PC 旗標時視為類別 5（推測）。
    public static void Attack(WorldSession session,uint attacker,uint target,long now)
    {
        if(music==null||session.Actor is not {} self)return;
        if(attacker!=self.ObjectId&&target!=self.ObjectId)return;
        uint other=attacker==self.ObjectId?target:attacker;
        bool player=session.Objects.FirstOrDefault(o=>o.ObjectId==other) is {} obj&&(obj.Flags&4)!=0;
        music.Attack(player,now);
    }
}
