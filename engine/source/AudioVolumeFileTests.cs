using P06.NativeUi;
namespace P06;

// R12-C（T430／#987，gap-matrix 15.14.5「4:OptionUI」）audio-volume-file 離線測試：四類音量存進引擎自己的 sound-volume.cfg
// （鍵名同 lineage.cfg），啟動時讀、捲軸改了寫。不讀不寫正式客戶端的 lineage.cfg、不開音訊裝置、不開視窗。
// 會寫檔的部分只寫在建置輸出目錄（AppContext.BaseDirectory）下的 module-test-audio-volume\，測完逐檔刪除（不遞迴）；
// 產品那一份 ClientSoundScene.Volumes 與 PlayerAudioSettings.VolumeFile 測完還原。CI 由 --module-tests 自動發現執行。
static class AudioVolumeFileTests {
    static string Levels(SoundVolumes v)=>$"sound={v[SoundCategory.Sound]} env={v[SoundCategory.EnvSound]} voice={v[SoundCategory.Voice]} music={v[SoundCategory.Music]}";

    [WorldModuleTest("audio-volume-file")]
    static void Run(WorldModuleTestContext t){
        Format(t);
        Gate(t);
        string dir=Path.Combine(AppContext.BaseDirectory,"module-test-audio-volume");
        string path=Path.Combine(dir,AudioVolumeFile.FileName);
        var shared=ClientSoundScene.Volumes;var saved=AudioVolumeFile.Keys.Select(k=>shared[k.Category]).ToArray();var prior=PlayerAudioSettings.VolumeFile;
        try{
            Directory.CreateDirectory(dir);Remove(path);
            FileRoundTrip(t,path);
            Remove(path);
            Product(t,path,shared);
        }finally{
            PlayerAudioSettings.VolumeFile=prior;
            for(int i=0;i<saved.Length;i++)shared[AudioVolumeFile.Keys[i].Category]=saved[i];
            Remove(path);Remove(path+".tmp");
            try{if(Directory.Exists(dir)&&!Directory.EnumerateFileSystemEntries(dir).Any())Directory.Delete(dir,false);}catch(IOException){}
        }
    }
    static void Remove(string file){if(File.Exists(file))File.Delete(file);}

    static void Format(WorldModuleTestContext t){
        t.Check(AudioVolumeFile.Keys.Select(k=>k.Key).SequenceEqual(["SoundVolume","EnvSoundVolume","VoiceVolume","MusicVolume"])
            &&AudioVolumeFile.Keys.Select(k=>(int)k.Category).SequenceEqual([0,1,2,3]),"鍵名同 lineage.cfg，依類別 0..3（sound.md §4.1）");
        var v=new SoundVolumes();
        t.Equal("SoundVolume=100\r\nEnvSoundVolume=100\r\nVoiceVolume=100\r\nMusicVolume=100\r\n",AudioVolumeFile.Serialize(v),"預設四類都是 100");
        v[SoundCategory.Sound]=.3;v[SoundCategory.EnvSound]=0;v[SoundCategory.Music]=.07;
        t.Equal("SoundVolume=30\r\nEnvSoundVolume=0\r\nVoiceVolume=100\r\nMusicVolume=7\r\n",AudioVolumeFile.Serialize(v),"0..1 → 0–100 整數");
        var p=AudioVolumeFile.Parse("SoundVolume=30\r\nEnvSoundVolume=0\nVoiceVolume=40\r\nMusicVolume=7\r\n");
        t.Check(p.Count==4&&p[SoundCategory.Sound]==.3&&p[SoundCategory.EnvSound]==0&&p[SoundCategory.Voice]==.4&&p[SoundCategory.Music]==.07,"四鍵讀回（CRLF／LF 都可）",p);
        var w=new SoundVolumes();foreach(var (c,l) in p)w[c]=l;
        t.Equal(AudioVolumeFile.Serialize(v).Replace("VoiceVolume=100","VoiceVolume=40"),AudioVolumeFile.Serialize(w),"存回讀出逐值相同");
        // 0.01 一格的每個值都能逐值往返（捲軸 SliderGain 的輸出）。
        bool exact=Enumerable.Range(0,211).All(pos=>{double g=PlayerAudioSettings.SliderGain(pos,210);var x=new SoundVolumes();x[SoundCategory.Music]=g;
            return AudioVolumeFile.Parse(AudioVolumeFile.Serialize(x))[SoundCategory.Music]==g;});
        t.Check(exact,"捲軸 0..210 每一格存回讀出相同");
        p=AudioVolumeFile.Parse(" SoundVolume = 55 \r\nFoo=3\r\n# EnvSoundVolume=10\r\nEnvSoundVolume=101\r\nVoiceVolume=-1\r\nMusicVolume=abc\r\n=5\r\nsoundvolume=9\r\n");
        t.Check(p.Count==1&&p[SoundCategory.Sound]==.55,"空白可、不認得的鍵與註解忽略、超界／負數／非數字那一類不收、鍵名分大小寫",p);
        p=AudioVolumeFile.Parse("MusicVolume=20\r\nMusicVolume=60\r\nSoundVolume=10\r\nSoundVolume=x\r\n");
        t.Check(p.Count==1&&p[SoundCategory.Music]==.6,"同一鍵以最後一次為準（最後一次不合法 → 那一類不收）",p);
        t.Check(AudioVolumeFile.Parse("").Count==0&&AudioVolumeFile.Parse("lineage configuration file\0\x01").Count==0,"空檔、非文字檔 → 什麼都不收");
        t.Check(AudioVolumeFile.DefaultPath.EndsWith(Path.Combine("runtime","user","sound-volume.cfg"),StringComparison.OrdinalIgnoreCase)
            &&!AudioVolumeFile.DefaultPath.EndsWith("lineage.cfg",StringComparison.OrdinalIgnoreCase),"存在引擎 runtime/user，不是 lineage.cfg",AudioVolumeFile.DefaultPath);
    }

    // --module-tests 不是正常產品入口：預設建構子不接檔，SetBarLevel 不寫任何檔（既有 split-volume 等測試照舊不寫檔）。
    static void Gate(WorldModuleTestContext t){
        t.Check(!PlayerAudioSettings.ProductLaunch(),"--module-tests 不算正常產品入口");
        var prior=PlayerAudioSettings.VolumeFile;
        try{
            PlayerAudioSettings.VolumeFile=null;
            _=new PlayerAudioSettings();
            t.Check(PlayerAudioSettings.VolumeFile==null,"預設建構子在測試中不接類別音量檔");
            _=new PlayerAudioSettings(null);
            t.Check(PlayerAudioSettings.VolumeFile==null,"傳 null → 不接");
        }finally{PlayerAudioSettings.VolumeFile=prior;}
    }

    static void FileRoundTrip(WorldModuleTestContext t,string path){
        var file=new AudioVolumeFile(path);var v=new SoundVolumes();
        t.Check(file.Load(v)==0&&v.Revision==0&&!File.Exists(path),"檔案不存在 → 什麼都不改、不建檔",Levels(v));
        v[SoundCategory.Sound]=.25;v[SoundCategory.Voice]=.5;v[SoundCategory.Music]=0;
        t.Check(file.Save(v)&&File.Exists(path)&&!File.Exists(path+".tmp"),"寫檔成功，.tmp 已取代");
        t.Equal("SoundVolume=25\r\nEnvSoundVolume=100\r\nVoiceVolume=50\r\nMusicVolume=0\r\n",File.ReadAllText(path),"檔案內容");
        var stamp=File.GetLastWriteTimeUtc(path);File.SetLastWriteTimeUtc(path,stamp.AddMinutes(-5));stamp=File.GetLastWriteTimeUtc(path);
        t.Check(file.Save(v)&&File.GetLastWriteTimeUtc(path)==stamp,"內容沒變 → 不重寫");
        var w=new SoundVolumes();
        t.Check(new AudioVolumeFile(path).Load(w)==4&&Levels(w)==Levels(v),"另一個實例讀回四類（含 Voice）",Levels(w));
        File.WriteAllText(path,"SoundVolume=80\r\nEnvSoundVolume=oops\r\n");
        var x=new SoundVolumes();x[SoundCategory.EnvSound]=.4;
        t.Check(new AudioVolumeFile(path).Load(x)==1&&x[SoundCategory.Sound]==.8&&x[SoundCategory.EnvSound]==.4&&x[SoundCategory.Music]==1,"壞掉的一行只略過那一類，其他照讀",Levels(x));
    }

    // 產品路徑：PlayerAudioSettings(file) 啟動時讀進 ClientSoundScene.Volumes；產品宿主（ClientForm.OptionWindowHost 用的
    // SetBarLevel）改了就寫回；改別份 SoundVolumes（測試自己的）不寫。
    static void Product(WorldModuleTestContext t,string path,SoundVolumes shared){
        foreach(var (c,_) in AudioVolumeFile.Keys)shared[c]=1;
        File.WriteAllText(path,"SoundVolume=40\r\nEnvSoundVolume=15\r\nVoiceVolume=60\r\nMusicVolume=70\r\n");
        _=new PlayerAudioSettings(new AudioVolumeFile(path));
        t.Check(PlayerAudioSettings.VolumeFile?.FilePath==path,"接上類別音量檔");
        t.Check(shared[SoundCategory.Sound]==.4&&shared[SoundCategory.EnvSound]==.15&&shared[SoundCategory.Voice]==.6&&shared[SoundCategory.Music]==.7,"啟動時讀進產品那一份",Levels(shared));
        t.Check(PlayerAudioSettings.BarLevel(shared,OptionPanel.MusicVolumeBar)==.7&&PlayerAudioSettings.BarLevel(shared,OptionPanel.EffectVolumeBar)==.4
            &&PlayerAudioSettings.BarLevel(shared,OptionPanel.EnvVolumeBar)==.15,"捲軸一開就是存檔的值");
        t.Check(PlayerAudioSettings.SetBarLevel(shared,OptionPanel.MusicVolumeBar,.33),"Music 捲軸 0.33");
        t.Equal("SoundVolume=40\r\nEnvSoundVolume=15\r\nVoiceVolume=60\r\nMusicVolume=33\r\n",File.ReadAllText(path),"捲軸改了 → 寫回（Voice 保留 60）");
        PlayerAudioSettings.SetBarLevel(shared,OptionPanel.EffectVolumeBar,0);PlayerAudioSettings.SetBarLevel(shared,OptionPanel.EnvVolumeBar,1);
        t.Equal("SoundVolume=0\r\nEnvSoundVolume=100\r\nVoiceVolume=60\r\nMusicVolume=33\r\n",File.ReadAllText(path),"Effect／Env 也寫回");
        t.Check(!PlayerAudioSettings.SetBarLevel(shared,OptionPanel.VoiceVolumeBar,.1)&&!PlayerAudioSettings.SetBarLevel(shared,OptionPanel.MusicVolumeBar,2)
            &&File.ReadAllText(path).Contains("MusicVolume=33"),"拒絕的設定不寫");
        var own=new SoundVolumes();
        PlayerAudioSettings.SetBarLevel(own,OptionPanel.MusicVolumeBar,.9);
        t.Check(File.ReadAllText(path).Contains("MusicVolume=33")&&!File.ReadAllText(path).Contains("MusicVolume=90"),"改的不是產品那一份 → 不寫");
        // 重開：新的一份讀回最後的值。
        var reopened=new SoundVolumes();new AudioVolumeFile(path).Load(reopened);
        t.Check(reopened[SoundCategory.Music]==.33&&reopened[SoundCategory.Sound]==0&&reopened[SoundCategory.EnvSound]==1&&reopened[SoundCategory.Voice]==.6,"重開讀回最後一次的值",Levels(reopened));
        PlayerAudioSettings.VolumeFile=null;
        PlayerAudioSettings.SetBarLevel(shared,OptionPanel.MusicVolumeBar,.5);
        t.Check(File.ReadAllText(path).Contains("MusicVolume=33"),"沒接檔 → 不寫");
    }
}
