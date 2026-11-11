using System.Runtime.CompilerServices;
using P06.NativeUi;
namespace P06;

// R11-F（T420／#962，gap-matrix 15.13.5／15.13.6）split-volume 離線測試：OptionUI 的 Music／Effect／Env 三條音量捲軸各自對到
// MusicVolume／SoundVolume／EnvSoundVolume 類別音量（sound.md 4.1），互不影響；主音量與靜音另一層不動。
// 版面 XML 與圖都是合成資料（不是真實 OptionUI.xml），不讀客戶端或參考客戶端、不開音訊裝置、不開視窗、不寫檔。
// 產品宿主（ClientForm.OptionWindowHost）用 GetUninitializedObject 建 ClientForm（不跑建構子），改的是產品唯一一份
// ClientSoundScene.Volumes，測完還原。CI 由 --module-tests 自動發現執行。
static class SplitVolumeTests {
    const uint BaseC=0xFF202020,TrackC=0xFF404080,ThumbC=0xFFFF0000;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static readonly Dictionary<int,UiSurface> Pngs=new(){[901]=Surface(901,320,200,BaseC),[934]=Surface(934,219,2,TrackC),[935]=Surface(935,9,12,ThumbC)};
    // 與 kor25 OptionUI.xml 相同的元素型態與名稱（Application 頁音量捲軸：Background_Image＋Move_Button，ScrollWidth 219 → travel 210）。
    static string Bar(string name,int y)=>$"""
        <ScrollBar Name="{name}" X="20" Y="{y}" Width="219" Height="12" ScrollStart="0" ScrollWidth="219" Horizontal="1">
          <Image Name="Background_Image" Y="6" Png="934"/>
          <Button Name="Move_Button" X="0" Y="0" BasePng="935" PushPng="935"/>
        </ScrollBar>
        """;
    static string Xml()=>$"""
        <?xml version="1.0"?>
        <OptionUI>
          <Window Name="Option" X="100" Y="50" BasePng="901;1" Moveable="30">
            <Control Name="Home" X="0" Y="0"/>
            <Control Name="Application" X="0" Y="0">
              {Bar("MusicVolumeScrollBar",60)}
              {Bar("EffectVolumeScrollBar",80)}
              {Bar("EnvVolumeScrollBar",100)}
              {Bar("VoiceVolumeScrollBar",120)}
            </Control>
          </Window>
        </OptionUI>
        """;
    const int MusicY=60,EffectY=80,EnvY=100;

    /// <summary>與 ClientForm.OptionWindowHost 同一套規則（PlayerAudioSettings.BarLevel／SetBarLevel），但用自己的一份 SoundVolumes。</summary>
    sealed class Host(SoundVolumes volumes):IOptionWindowHost {
        public readonly List<string> Calls=[];
        public bool? Value(string e)=>null;
        public bool Set(string e,bool v)=>false;
        public double? Level(string bar)=>PlayerAudioSettings.BarLevel(volumes,bar);
        public bool SetLevel(string bar,double v){Calls.Add($"{bar}={v:0.##}");return PlayerAudioSettings.SetBarLevel(volumes,bar,v);}
    }
    sealed class Recorder:IMusicStreamAudio {
        public readonly List<double> Volumes=[];
        public bool Start(MusicTrack track,double volume){Volumes.Add(volume);return true;}
        public void SetVolume(MusicTrack track,double volume)=>Volumes.Add(volume);
        public void Stop(MusicTrack track){}
        public bool Finished(MusicTrack track)=>false;
    }
    // 合成 music.def（自編數值）：槽 0→82（登入）。
    const string Def="System Setting Begin\r\n\tA=82;\r\n\tB=1;\r\n\tC=2;\r\n\tD=3;\r\n\tE=4;\r\n\tF=5;\r\n\tG=6;\r\n\tH=7;\r\n\tI=5;\r\nSystemMenu Exit\r\n";

    static OptionPanel Panel(){var p=new OptionPanel();p.Load(UiLayout.Parse(Xml(),"OptionUI.xml"),_=>null,id=>Pngs.GetValueOrDefault(id));return p;}
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(OptionPanel p,Point at){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    /// <summary>Logical point on a bar at window y (bar X 20, origin 100/50), x pixels from the bar's left.</summary>
    static Point BarAt(int barY,int x)=>new(100+20+x,50+barY+6);
    static int Pixel(OptionPanel p,Point at){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(at.X,at.Y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static string Levels(SoundVolumes v)=>$"music={v[SoundCategory.Music]} sound={v[SoundCategory.Sound]} env={v[SoundCategory.EnvSound]} voice={v[SoundCategory.Voice]}";

    [WorldModuleTest("split-volume")]
    static void Run(WorldModuleTestContext t){
        Mapping(t);
        Independent(t);
        PanelBars(t);
        ProductHost(t);
        Consumers(t);
    }

    static void Mapping(WorldModuleTestContext t){
        t.Check(PlayerAudioSettings.BarCategory(OptionPanel.MusicVolumeBar)==SoundCategory.Music,"Music bar → MusicVolume（類別 3）");
        t.Check(PlayerAudioSettings.BarCategory(OptionPanel.EffectVolumeBar)==SoundCategory.Sound,"Effect bar → SoundVolume（類別 0）");
        t.Check(PlayerAudioSettings.BarCategory(OptionPanel.EnvVolumeBar)==SoundCategory.EnvSound,"Env bar → EnvSoundVolume（類別 1）");
        t.Check(PlayerAudioSettings.BarCategory(OptionPanel.VoiceVolumeBar)==null&&PlayerAudioSettings.BarCategory("WindowAlphaScrollBar")==null&&PlayerAudioSettings.BarCategory("")==null,"Voice／其他 → 不接");
        t.Equal(3,OptionPanel.MasterVolumeBars.Select(PlayerAudioSettings.BarCategory).Distinct().Count(b=>b!=null),"三條捲軸三個不同類別");
    }

    static void Independent(WorldModuleTestContext t){
        var v=new SoundVolumes();
        t.Check(PlayerAudioSettings.BarLevel(v,OptionPanel.MusicVolumeBar)==1&&PlayerAudioSettings.BarLevel(v,OptionPanel.VoiceVolumeBar)==null,"預設 1；Voice 回 null");
        int rev=v.Revision;
        t.Check(PlayerAudioSettings.SetBarLevel(v,OptionPanel.EffectVolumeBar,.3),"Effect 設 0.3");
        t.Check(v[SoundCategory.Sound]==.3&&v[SoundCategory.Music]==1&&v[SoundCategory.EnvSound]==1&&v[SoundCategory.Voice]==1&&v.Revision==rev+1,"只改 SoundVolume，其他三類不動",Levels(v));
        PlayerAudioSettings.SetBarLevel(v,OptionPanel.MusicVolumeBar,.7);PlayerAudioSettings.SetBarLevel(v,OptionPanel.EnvVolumeBar,0);
        t.Check(v[SoundCategory.Music]==.7&&v[SoundCategory.Sound]==.3&&v[SoundCategory.EnvSound]==0,"三類各自保有自己的值",Levels(v));
        rev=v.Revision;
        t.Check(!PlayerAudioSettings.SetBarLevel(v,OptionPanel.MusicVolumeBar,1.5)&&!PlayerAudioSettings.SetBarLevel(v,OptionPanel.MusicVolumeBar,-.1)
            &&!PlayerAudioSettings.SetBarLevel(v,OptionPanel.MusicVolumeBar,double.NaN)&&!PlayerAudioSettings.SetBarLevel(v,OptionPanel.VoiceVolumeBar,.5),"超界、NaN、Voice → false");
        t.Check(v.Revision==rev&&v[SoundCategory.Music]==.7&&v[SoundCategory.Voice]==1,"拒絕時不改",Levels(v));
        t.Check(!v.EffectCategoriesSilent,"Env 0 但 Sound 0.3 → 效果類不算全靜（A06020）");
        // 刻度：0.01 一格的線性換算（推定，LScrollBar 範圍沒追），三條共用。
        t.Check(PlayerAudioSettings.SliderGain(63,210)==.3&&PlayerAudioSettings.SliderPosition(.3,210)==63,"線性刻度（推定）：0.3 ↔ 63/210");
    }

    static void PanelBars(WorldModuleTestContext t){
        var v=new SoundVolumes();var host=new Host(v);var p=Panel();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        p.Attach(host);p.Open();p.ShowPage("Application");
        // 預設三類都是 1 → 拇指在 travel 210。
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(MusicY,210+4)),"music thumb at 1");
        // Effect：按左端 → 0，拖到中間放開 → 0.5；只有 SoundVolume 變。
        var c=new UiNullCommands();
        p.Mouse(M(UiMouseKind.Down,BarAt(EffectY,4)),c);
        t.Check(v[SoundCategory.Sound]==0&&v[SoundCategory.Music]==1&&v[SoundCategory.EnvSound]==1,"press Effect left end → SoundVolume 0 only",Levels(v));
        p.Mouse(M(UiMouseKind.Up,BarAt(EffectY,4+105)),c);
        t.Check(v[SoundCategory.Sound]==.5&&v[SoundCategory.Music]==1&&v[SoundCategory.EnvSound]==1&&p.Unhandled.Count==0,"release in the middle → SoundVolume 0.5 only",Levels(v));
        // Env：按左端 → 0。
        Click(p,BarAt(EnvY,4));
        t.Check(v[SoundCategory.EnvSound]==0&&v[SoundCategory.Sound]==.5&&v[SoundCategory.Music]==1,"click Env left end → EnvSoundVolume 0 only",Levels(v));
        // 三個拇指各畫在自己的值（1／0.5／0）：不再跟著同一個主音量。
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(MusicY,210+4)),"music thumb stays at 1");
        t.Check(Pixel(p,BarAt(MusicY,4))!=Argb(ThumbC),"music thumb not at 0");
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(EffectY,105+4)),"effect thumb at 0.5");
        t.Check(Pixel(p,BarAt(EffectY,210+4))!=Argb(ThumbC),"effect thumb not at 1");
        t.Equal(Argb(ThumbC),Pixel(p,BarAt(EnvY,4)),"env thumb at 0");
        t.Check(Pixel(p,BarAt(EnvY,105+4))!=Argb(ThumbC),"env thumb not at 0.5");
        // Voice 照舊不接。
        host.Calls.Clear();Click(p,BarAt(120,150));
        t.Check(p.Unhandled.Contains(OptionPanel.VoiceVolumeBar)&&host.Calls.Count==0&&v[SoundCategory.Voice]==1,"voice bar → Unhandled, VoiceVolume untouched",string.Join(",",p.Unhandled));
    }

    // 產品路徑：ClientForm.OptionWindowHost 改的是 form.SoundVolumes＝ClientSoundScene.Volumes（背景音樂、環境音、`^`、戰鬥音效共用）；
    // 主音量 audioSettings 不動。
    static void ProductHost(WorldModuleTestContext t){
        var shared=ClientSoundScene.Volumes;
        double music=shared[SoundCategory.Music],sound=shared[SoundCategory.Sound],env=shared[SoundCategory.EnvSound];
        try{
            var form=(ClientForm)RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
            t.Check(ReferenceEquals(form.SoundVolumes,shared),"form.SoundVolumes 是 ClientSoundScene.Volumes 那一份");
            IOptionWindowHost host=new ClientForm.OptionWindowHost(form);
            shared[SoundCategory.Music]=1;shared[SoundCategory.Sound]=1;shared[SoundCategory.EnvSound]=1;
            t.Check(host.SetLevel(OptionPanel.MusicVolumeBar,.25)&&shared[SoundCategory.Music]==.25&&shared[SoundCategory.Sound]==1&&shared[SoundCategory.EnvSound]==1,"產品宿主：Music → MusicVolume",Levels(shared));
            t.Check(host.SetLevel(OptionPanel.EffectVolumeBar,.6)&&shared[SoundCategory.Sound]==.6&&shared[SoundCategory.Music]==.25,"產品宿主：Effect → SoundVolume",Levels(shared));
            t.Check(host.SetLevel(OptionPanel.EnvVolumeBar,.1)&&shared[SoundCategory.EnvSound]==.1&&shared[SoundCategory.Sound]==.6,"產品宿主：Env → EnvSoundVolume",Levels(shared));
            t.Check(host.Level(OptionPanel.MusicVolumeBar)==.25&&host.Level(OptionPanel.EffectVolumeBar)==.6&&host.Level(OptionPanel.EnvVolumeBar)==.1&&host.Level(OptionPanel.VoiceVolumeBar)==null,"產品宿主：Level 讀回各自的值");
            t.Check(!host.SetLevel(OptionPanel.VoiceVolumeBar,.5),"產品宿主：Voice 不接");
        }finally{shared[SoundCategory.Music]=music;shared[SoundCategory.Sound]=sound;shared[SoundCategory.EnvSound]=env;}
    }

    // 消費端：背景音樂只看 MusicVolume（× 主音量），效果音判定只看 SoundVolume，環境音判定只看 EnvSoundVolume。
    static void Consumers(WorldModuleTestContext t){
        var shared=ClientSoundScene.Volumes;
        double music=shared[SoundCategory.Music],sound=shared[SoundCategory.Sound],env=shared[SoundCategory.EnvSound];
        var rec=new Recorder();
        try{
            shared[SoundCategory.Music]=1;shared[SoundCategory.Sound]=1;shared[SoundCategory.EnvSound]=1;
            ClientSoundScene.Configure(new SoundDefinitions(MusicDefinition.Parse(Def),null,"合成"),rec,null);
            ClientSoundScene.ObserveFront(LoginState.Offline,.5,false,0);
            t.Check(rec.Volumes.Count==1&&rec.Volumes[^1]==.5,"登入音樂：MusicVolume 1 × 主音量 0.5",rec.Volumes);
            PlayerAudioSettings.SetBarLevel(shared,OptionPanel.MusicVolumeBar,.4);
            ClientSoundScene.ObserveFront(LoginState.Offline,.5,false,16);
            t.Check(Math.Abs(rec.Volumes[^1]-.2)<1e-12,"Music 捲軸 0.4 → 在播的音樂下一次更新改成 0.2",rec.Volumes);
            PlayerAudioSettings.SetBarLevel(shared,OptionPanel.EffectVolumeBar,0);PlayerAudioSettings.SetBarLevel(shared,OptionPanel.EnvVolumeBar,0);
            ClientSoundScene.ObserveFront(LoginState.Offline,.5,false,32);
            t.Check(Math.Abs(rec.Volumes[^1]-.2)<1e-12,"Effect／Env 捲軸不影響音樂",rec.Volumes);

            var v=new SoundVolumes();var gate=new SoundGate(v,new SoundCooldown(),()=>0);
            PlayerAudioSettings.SetBarLevel(v,OptionPanel.EffectVolumeBar,.5);
            var once=gate.Admit(new SoundRequest(1,SoundCategory.Sound,Cooldown:false));
            var loop=gate.Admit(new SoundRequest(2,SoundCategory.EnvSound,Cooldown:false));
            t.Check(once.Dispatch&&once.Scale==.5&&loop.Dispatch&&loop.Scale==1,"Effect 0.5：效果音 0.5、環境音仍 1");
            PlayerAudioSettings.SetBarLevel(v,OptionPanel.EnvVolumeBar,.25);PlayerAudioSettings.SetBarLevel(v,OptionPanel.MusicVolumeBar,0);
            once=gate.Admit(new SoundRequest(1,SoundCategory.Sound,Cooldown:false));
            loop=gate.Admit(new SoundRequest(2,SoundCategory.EnvSound,Cooldown:false));
            t.Check(once.Scale==.5&&loop.Scale==.25,"Env 0.25、Music 0：效果音仍 0.5、環境音 0.25");
            PlayerAudioSettings.SetBarLevel(v,OptionPanel.EffectVolumeBar,0);
            t.Check(!gate.Admit(new SoundRequest(1,SoundCategory.Sound,Cooldown:false)).Dispatch&&gate.Admit(new SoundRequest(2,SoundCategory.EnvSound,Cooldown:false)).Dispatch,"Effect 0：效果音不播，環境音照播");
            // `^` 循環：類別 EnvSoundVolume × 主音量變動，在播的循環立即改音量（ProductAmbientLoopAudio.SetScale）。
            t.Check(Math.Abs(ProductAmbientLoopAudio.Final(80,v[SoundCategory.EnvSound]*1)-.2)<1e-12,"^：80/100 × Env 0.25 = 0.2");
        }finally{ClientSoundScene.Reset();shared[SoundCategory.Music]=music;shared[SoundCategory.Sound]=sound;shared[SoundCategory.EnvSound]=env;}
    }
}
