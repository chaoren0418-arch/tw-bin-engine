namespace P06;

// T172（#446）：背景音樂、戰鬥音樂、登入音樂。依據 docs/reference/sound.md §4.3–4.5、§6.2（T163／T171）。
// 這裡只有播放政策；串流由 IMusicStreamAudio 後端負責（原版是 mss32 AIL_open_stream 管線 B）。
public enum MusicScene { None, Login, CharacterSelect, World }
// Boss＝區域 BATTLE 曲；BossSpawn／BossDeath＝頭目出現／死亡曲（T280，FU4§6）。新值附加在最後，不改既有數值。
public enum MusicKind { Login, CharacterSelect, Region, Battle, Boss, LargeMap, BossSpawn, BossDeath }
// Music 是 sound\musicN.mp3 的 N；Loop=false 對應 AIL_set_stream_loop_count(s,1) 的單次播放。
// R7-L（T351）：Slot 是「明確系統槽」編號（登入 0、選角 8、戰鬥 9／10）；區域選曲、大地圖、頭目曲是 -1。
// 原版 MIDI 模式（沒有 music*.mp3）遇到 0..8 號系統槽時停播（8F1C30／8F2FB0／8F3B90／8F3D70，見 issue-737 報告）。
public sealed record MusicTrack(long Number,int Music,bool Loop,MusicKind Kind,int Slot=-1);
public interface IMusicStreamAudio
{
    // volume 是最終比例 0..1（淡出係數 × MusicVolume × 曲目音量）。回 false 表示沒有啟動。
    bool Start(MusicTrack track,double volume);
    void SetVolume(MusicTrack track,double volume);
    void Stop(MusicTrack track);
    // 單次播放的曲目自然播完時回 true（原版串流狀態不再是 4）。
    bool Finished(MusicTrack track);
    // R10-L（T396）：換曲。from＝正在播、要被換掉的曲目（沒有則 null）。預設＝先停舊的再開新的（原本的行為）。
    // MIDI 後端覆寫成「0.mid 在播就沿用、沒在播才開」（8F1C30／8F2FB0／8F3B90／8F3D70），見 ProductMusicAudio.Replace。
    bool Replace(MusicTrack? from,MusicTrack to,double volume){if(from!=null)Stop(from);return Start(to,volume);}
}
// Enabled：音樂選項開且未靜音；Volume：MusicVolume 類別 × 主音量（0..1）。Map／X／Y 只在 World 使用。
// LiveSprites：畫面上還活著（動作 ≠ 8）的物件 gfx（sub_8F15B0）。null＝呼叫端沒有物件資訊，登記過的頭目一律視為在場。
public sealed record MusicContext(MusicScene Scene,int Map,int X,int Y,bool Enabled,double Volume,IReadOnlySet<int>? LiveSprites=null);

public sealed class BackgroundMusic(MusicDefinition definition,IMusicStreamAudio audio,Random random)
{
    public const int CheckMs=1000,BattleHoldMs=15000,FadeStepMs=50,FadeStart=127,FadeDecrement=8;
    public MusicDefinition Definition=>definition;
    public MusicTrack? Current {get;private set;}
    public bool Playing {get;private set;}
    public bool Fading=>fadeAt!=null;
    public long LastBattle {get;private set;}=long.MinValue;
    public bool BattleMusicEnabled {get;set;}=true; // A4AD00(8) 戰鬥音樂選項；引擎目前沒有對應設定，預設開。
    public MusicRegion? Region {get;private set;}
    readonly HashSet<int> bossSprites=[];
    readonly Dictionary<int,int> lastIndex=[]; // 每筆區域記錄的前次候選索引。
    long number,startedAt,nextCheck=long.MinValue,retryAt=long.MinValue;long? fadeAt;int interval=-1;
    double volume;MusicContext? last;

    public int FadeLevel(long now)=>fadeAt is long at?Math.Max(0,FadeStart-FadeDecrement*(int)Math.Min(int.MaxValue,(now-at)/FadeStepMs)):FadeStart;

    // 8F3990 的登記（dword_137223C）：只記 sprite，不換曲。產品由 BossAppeared／BossDied 呼叫（T280，FU4§6）。
    public void RegisterBoss(int sprite){if(sprite>0)bossSprites.Add(sprite);}
    public void ClearBosses(){bossSprites.Clear();spawnTriggered.Clear();twinSpawned=false;twinDead.Clear();}
    readonly HashSet<int> spawnTriggered=[]; // 區域記錄 +44「已觸發」：SPAWN 每筆區域只回傳一次。
    bool twinSpawned;readonly HashSet<int> twinDead=[]; // byte_13D42BE（雙頭目 SPAWN 全域一次）、byte_13D42BF／13D42C8（各自死亡）。
    // music.def 裡所有頭目 sprite（SPRITE、TWIN_SPRITE1/2）；ClientSoundScene 用來篩出畫面上活著的頭目。
    public IReadOnlySet<int> BossSpriteIds {get;}=definition.Regions.SelectMany(r=>new[]{r.Sprite,r.TwinSprite1,r.TwinSprite2}).Where(s=>s>0).ToHashSet();

    static bool IsBossOf(MusicRegion r,int sprite)=>sprite>0&&(r.TwinBoss?sprite==r.TwinSprite1||sprite==r.TwinSprite2:r.Sprite==sprite);
    // sub_8F16B0：目前地圖、玩家所在矩形內，SPRITE（或雙頭目之一）等於 sprite 的那筆區域。
    MusicRegion? BossRegion(int sprite)=>last is {Scene:MusicScene.World} ctx&&ctx.Map<=MusicDefinition.LargeMapThreshold?definition.Regions.FirstOrDefault(r=>r.Matches(ctx.Map,ctx.X,ctx.Y)&&IsBossOf(r,sprite)):null;
    bool MusicOn=>last is {Scene:MusicScene.World,Enabled:true};

    // sub_A50F20 → 8F3990(sprite,0)：頭目物件出現（不在死亡狀態）時播 SPAWN（單次）；查到而且沒在播才換曲，不檢查保護曲目。
    public void BossAppeared(int sprite,long now)
    {
        if(sprite<=0||!BossSpriteIds.Contains(sprite))return;
        RegisterBoss(sprite);
        if(!MusicOn||BossRegion(sprite) is not {} r)return;
        int music=-1;
        if(r.TwinBoss){twinDead.Remove(sprite);if(!twinSpawned){twinSpawned=true;music=r.Spawn;}} // 推測：重新出現的雙頭目清掉自己的死亡旗標。
        else if(spawnTriggered.Add(r.Ordinal))music=r.Spawn;
        if(music>=0&&!IsPlaying(music))Start(music,false,MusicKind.BossSpawn,now,-1);
    }
    // S_ACTION（92CB70）動作 8 → 8F3990(sprite,2)：播 DEATH；雙頭目要兩隻都死。
    public void BossDied(int sprite,long now)
    {
        if(sprite<=0||!BossSpriteIds.Contains(sprite))return;
        RegisterBoss(sprite);
        if(!MusicOn||BossRegion(sprite) is not {} r)return;
        if(r.TwinBoss){twinDead.Add(sprite);if(!(twinDead.Contains(r.TwinSprite1)&&twinDead.Contains(r.TwinSprite2)))return;}
        if(r.Death>=0&&!IsPlaying(r.Death))Start(r.Death,false,MusicKind.BossDeath,now,-1);
    }

    // sub_8F15B0：頭目物件在場而且沒死（雙頭目兩隻都要在）。沒有物件資訊（LiveSprites=null）時以登記為準。
    static bool Present(MusicContext? ctx,int sprite)=>ctx?.LiveSprites is not {} live||live.Contains(sprite);
    bool BossActive(MusicRegion r)=>r.Battle!=-1&&(r.TwinBoss
        ?bossSprites.Contains(r.TwinSprite1)&&bossSprites.Contains(r.TwinSprite2)&&Present(last,r.TwinSprite1)&&Present(last,r.TwinSprite2)
        :r.Sprite!=-1&&bossSprites.Contains(r.Sprite)&&Present(last,r.Sprite));
    // 8F31F0：登記的頭目對到這筆區域，而且 SPAWN／BATTLE／DEATH 有一首正在播，就讓它播完。
    bool BossTrackPlaying(MusicRegion? r)=>r!=null&&Current is {Kind:MusicKind.BossSpawn or MusicKind.BossDeath or MusicKind.Boss} c&&Playing&&(c.Music==r.Spawn||c.Music==r.Battle||c.Music==r.Death);

    // 8F6160：目前曲目＝id 而且仍在播。
    bool IsPlaying(int music)=>Current is {} c&&Playing&&c.Music==music;
    bool Protected=>Current is {} c&&Playing&&MusicDefinition.UninterruptibleSlots.Any(s=>definition.Slot(s)==c.Music);

    void StopCurrent(){if(Current is {} c&&Playing)audio.Stop(c);Current=null;Playing=false;fadeAt=null;}

    bool Start(int music,bool loop,MusicKind kind,long now,int nextInterval,int slot=-1)
    {
        // R10-L：換曲交給後端的 Replace（停舊＋開新）；一般後端等同原本的 StopCurrent＋Start。
        var from=Current is {} c&&Playing?c:null;
        Current=null;Playing=false;fadeAt=null;
        if(music<0){if(from!=null)audio.Stop(from);return false;}
        var track=new MusicTrack(++number,music,loop,kind,slot);
        if(!audio.Replace(from,track,volume)){retryAt=now+CheckMs;return false;} // 缺檔／後端拒絕：每秒最多重試一次。
        Current=track;Playing=true;startedAt=now;interval=nextInterval;return true;
    }

    // 8F31F0 的選曲：大編號地圖固定 23；頭目在場改用 BATTLE；MUSIC 多首時隨機且不連續重複同一候選索引。
    (int Music,MusicKind Kind,int Interval) Choose(MusicContext ctx)
    {
        if(ctx.Map>MusicDefinition.LargeMapThreshold)return (MusicDefinition.LargeMapMusic,MusicKind.LargeMap,-1);
        if(Region is not {} r)return (-1,MusicKind.Region,-1);
        if(BossActive(r))return (r.Battle,MusicKind.Boss,r.Interval);
        if(r.Music.Length==0)return (-1,MusicKind.Region,r.Interval);
        int index=0;
        if(r.Music.Length>1)
        {
            int previous=lastIndex.TryGetValue(r.Ordinal,out int p)?p:-1;
            do index=random.Next(r.Music.Length);while(index==previous);
        }
        lastIndex[r.Ordinal]=index;
        return (r.Music[index],MusicKind.Region,r.Interval);
    }
    bool StartRegion(MusicContext ctx,long now){var c=Choose(ctx);return Start(c.Music,false,c.Kind,now,c.Interval);}

    // A4DF10：自己參與攻擊。對象是 PC 時用槽 10（PlayrtKilling），其他用槽 9（Hunting）；立即切換（8F3B90）。
    public void Attack(bool targetIsPlayer,long now)
    {
        LastBattle=now;
        if(last is not {Scene:MusicScene.World,Enabled:true}||!BattleMusicEnabled)return;
        int slot=targetIsPlayer?MusicDefinition.SlotPlayerKilling:MusicDefinition.SlotHunting;
        int music=definition.Slot(slot);
        if(IsPlaying(music)||Protected)return; // 正在播的曲目不重開；保護曲目不打斷。
        Start(music,false,MusicKind.Battle,now,-1,slot);
    }

    public void Update(MusicContext ctx,long now)
    {
        bool sceneChanged=last is not {} prev||prev.Scene!=ctx.Scene||prev.Enabled!=ctx.Enabled;
        last=ctx;
        volume=Math.Clamp(double.IsFinite(ctx.Volume)?ctx.Volume:0,0,1);
        if(!ctx.Enabled||ctx.Scene==MusicScene.None){StopCurrent();Region=null;return;} // A06A10：關閉時停止。
        if(Current is {} c&&Playing&&audio.Finished(c)){audio.Stop(c);Playing=false;}
        // 8F1B60：每 50ms 音量 −8；到 0 後以全音量開新曲，沒有淡入。
        if(fadeAt!=null)
        {
            int level=FadeLevel(now);
            if(level>0){if(Current is {} f&&Playing)audio.SetVolume(f,volume*level/FadeStart);return;}
            fadeAt=null;
            if(ctx.Scene==MusicScene.World){Region=definition.Region(ctx.Map,ctx.X,ctx.Y);StartRegion(ctx,now);}
            else StopCurrent();
            return;
        }
        if(Current is {} live&&Playing)audio.SetVolume(live,volume);
        switch(ctx.Scene)
        {
            case MusicScene.Login:
            {
                // 8F2E40：本檔沒有 LoginMusic 清單，走系統槽 0；8F2F00 每秒檢查是否播完再換。
                int music=definition.Slot(MusicDefinition.SlotLogin);
                if(sceneChanged||now>=nextCheck&&(Current?.Kind!=MusicKind.Login||!Playing))Start(music,false,MusicKind.Login,now,-1,MusicDefinition.SlotLogin);
                if(sceneChanged||now>=nextCheck)nextCheck=now+CheckMs;
                Region=null;return;
            }
            case MusicScene.CharacterSelect:
                // EC4FE0 狀態 20：系統槽 8 循環播放（8F3AA0(1)，loop 0）。
                if(sceneChanged||now>=retryAt&&(Current?.Kind!=MusicKind.CharacterSelect||!Playing))Start(definition.Slot(MusicDefinition.SlotCredits),true,MusicKind.CharacterSelect,now,-1,MusicDefinition.SlotCredits);
                Region=null;return;
        }
        // World。
        bool large=ctx.Map>MusicDefinition.LargeMapThreshold;
        var region=large?null:definition.Region(ctx.Map,ctx.X,ctx.Y);
        var key=(large,region?.Ordinal??-1,region!=null&&BossActive(region));
        bool keyChanged=sceneChanged||key!=regionKey;
        regionKey=key;Region=region;
        if(Current?.Kind==MusicKind.Battle)
        {
            // F0F0C0：每秒檢查；距上次戰鬥超過 15 秒就淡回區域音樂（8F1C30(-1)）。
            if(now>=nextCheck){nextCheck=now+CheckMs;if(now-LastBattle>BattleHoldMs){if(Playing&&!Protected)fadeAt=now;else if(!Playing)StartRegion(ctx,now);}}
            return;
        }
        if(Protected)return;
        if(keyChanged)
        {
            if(sceneChanged||Current==null||!Playing){StartRegion(ctx,now);return;}
            if(BossTrackPlaying(region))return; // 頭目出現／死亡造成的換鍵：SPAWN／DEATH 播完才回選曲（FU4§6）。
            // 推測：換到另一筆區域記錄（或頭目出現／離開）時，目前曲目若不在新候選中，以 0.8 秒淡出切換（同 8F1C30）。
            int[] candidates=large?[MusicDefinition.LargeMapMusic]:region==null?[]:key.Item3?[region.Battle]:region.Music;
            if(candidates.Contains(Current.Music)){interval=region?.Interval??-1;return;}
            fadeAt=now;return;
        }
        // 一首播完後依 INTERVAL 換下一首；判斷式是「開始時間 + 1000 × INTERVAL」（未填 = -1，播完即換）。
        if(!Playing&&now>=retryAt&&(Current==null||now>=startedAt+1000L*interval))StartRegion(ctx,now);
    }
    (bool,int,bool) regionKey=(false,-2,false);
    public void Clear(){StopCurrent();Region=null;last=null;regionKey=(false,-2,false);LastBattle=long.MinValue;nextCheck=retryAt=long.MinValue;}
}
