using P24.Audio;
using System.Security.Cryptography;

// T460（#1045）：6016 聲音 provider 的離線測試。1340／3332 改由 TW_CLIENT_ROOT/Sound/<id>.wav 直讀並驗程式內 pin，
// 不再讀 assets/audio-manifest.json。這裡只用合成 WAV（寫在 scratch/issue-1045/run-<時間>/，不進 git）、假驅動
// （不開 DirectShow 或音訊裝置）；不讀遊戲素材、不連網路、不啟動遊戲。
// 例外：有設 TW_CLIENT_ROOT 且那裡有 Sound/3332.wav 時，額外以唯讀方式核對原檔與程式 pin（CI 沒有原客戶端，會略過並記在 labels）。
// 群組 audio-provider（CI：build.yml 的 map-continuity job）。
partial class Checks {
    sealed class ApDriver:IAudioDriver {
        public readonly List<(string Path,string Format)> Opened=[];
        public NativeReply Open(string path,string format,string voice){Opened.Add((path,format));return new(0,"ok");}
        public NativeReply Gain(string voice,double normalized)=>new(0,"ok");
        public NativeReply Play(string voice)=>new(0,"ok");
        public NativeReply Status(string voice)=>new(0,"playing");
        public NativeReply Stop(string voice)=>new(0,"ok");
        public NativeReply Close(string voice)=>new(0,"ok");
    }
    /// <summary>16-bit mono PCM RIFF/WAVE，內容由 seed 決定（不同 seed＝不同 SHA）。</summary>
    static byte[] ApWav(int seed,int frames=2205){
        using var s=new MemoryStream();using var w=new BinaryWriter(s);
        w.Write("RIFF"u8);w.Write(36+frames*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(22050);w.Write(44100);w.Write((short)2);w.Write((short)16);
        w.Write("data"u8);w.Write(frames*2);for(int i=0;i<frames;i++)w.Write((short)(4000*Math.Sin(2*Math.PI*(300+seed)*i/22050)));
        w.Flush();return s.ToArray();
    }
    static string ApSha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static AssetPin ApPin(int id,string ext,byte[]? bytes)=>new(id,ext,$"Sound/{id}.{ext}",bytes?.Length,bytes==null?null:ApSha(bytes));
    static string ApWrite(string root,string rel,byte[] bytes){string p=Path.Combine(root,rel);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllBytes(p,bytes);return p;}
    /// <summary>以假驅動播放一次；回傳結果與開檔紀錄。</summary>
    static (AudioReply Reply,ApDriver Driver) ApPlay(AudioAssets assets,int id){
        var driver=new ApDriver();var core=new AudioCore(assets,driver,8);var owner=new AudioOwner("audio-provider",1);
        Check(core.Register(owner).Success,$"audio-provider：{id} 註冊 owner");
        core.Configure(new(1,false,true));
        var reply=core.Play(id,owner);
        core.Shutdown();
        return(reply,driver);
    }
    static string ApStages(AudioReply r)=>string.Join(",",r.Events.Select(e=>e.Format+":"+e.Stage));

    [CheckGroup("audio-provider",Order=1),CheckGroup(CheckGroups.Default)]
    static void AudioProviderChecks(string repo){
        string run=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1045","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(run);int caseNo=0;string Root(string tag){string r=Path.Combine(run,$"{++caseNo:00}-{tag}");Directory.CreateDirectory(r);return r;}

        // 1. 程式內 pin 的契約：與舊 audio-manifest（p24-audio-assets-v2，SHA 87c6cd68…）的四列相同，路徑全是 Sound/ 相對路徑。
        var pins=AudioAssets.Effect6016Pins;
        Check(pins.Count==4&&pins.Select(p=>p.Id).Distinct().OrderBy(x=>x).SequenceEqual(new[]{1340,3332}),"audio-provider：6016 pin 只有 1340、3332 兩個 ID");
        Check(pins.All(p=>p.RelativePath==$"Sound/{p.Id}.{p.Extension}"&&!Path.IsPathRooted(p.RelativePath)&&!p.RelativePath.Contains(':')),"audio-provider：pin 路徑是 Sound/<id>.<ext>，沒有絕對路徑");
        Check(pins.Where(p=>p.Extension=="mp3").All(p=>p.Size==null&&p.Sha256==null),"audio-provider：兩個 MP3 宣告為不可用（原客戶端沒有）");
        Check(pins.Single(p=>p.Id==3332&&p.Extension=="wav") is {Size:24748,Sha256:"973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a"},"audio-provider：3332.wav pin 與舊 manifest 相同");
        Check(pins.Single(p=>p.Id==1340&&p.Extension=="wav") is {Size:14322,Sha256:"30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3"},"audio-provider：1340.wav pin 與舊 manifest 相同");
        Check(AudioPlayer.Effect6016Contract==P24.HostAudio.AudioApiTransport.AudioContract&&AudioPlayer.Effect6016Contract!="p24-audio-assets-v2","audio-provider：host 與 audio 的契約版本字串一致，且不是舊 manifest schema");

        // 2. 正常：MP3 不可用 → 記 SourceUnavailable，同 ID WAV fallback，開的是 <root>/Sound/<id>.wav。
        {
            string root=Root("normal");var wav=ApWav(1);ApWrite(root,"Sound/4401.wav",wav);
            var assets=AudioAssets.FromClientRoot(root,[ApPin(4401,"mp3",null),ApPin(4401,"wav",wav)]);
            Check(assets.Formats(4401).SequenceEqual(new[]{"mp3","wav"}),"audio-provider：載入順序 mp3 → wav");
            var(r,d)=ApPlay(assets,4401);
            Check(r.Success&&r.Status=="StartedWavFallback",$"audio-provider：正常 WAV fallback 播放成功（{r.Status}）");
            Check(ApStages(r)=="mp3:SourceUnavailable,wav:PlaySubmitted","audio-provider：正常路徑記 MP3 不可用後送出 WAV（"+ApStages(r)+"）");
            Check(d.Opened.Count==1&&d.Opened[0].Format=="wav"&&string.Equals(d.Opened[0].Path,Path.Combine(root,"Sound","4401.wav"),StringComparison.OrdinalIgnoreCase),"audio-provider：直讀 <TW_CLIENT_ROOT>/Sound/4401.wav");
        }
        // 3. 缺檔：WAV 不存在 → FileMissing，不開驅動。
        {
            string root=Root("missing");var wav=ApWav(2);
            var(r,d)=ApPlay(AudioAssets.FromClientRoot(root,[ApPin(4402,"mp3",null),ApPin(4402,"wav",wav)]),4402);
            Check(!r.Success&&r.Status=="FileMissing"&&ApStages(r)=="mp3:SourceUnavailable,wav:FileMissing"&&d.Opened.Count==0,"audio-provider：缺 WAV 回 FileMissing、不開驅動（"+ApStages(r)+"）");
        }
        // 4. pin 不符：WAV 內容與 pin 不同 → IntegrityFailure，不開驅動；MP3 pin 不符也不 fallback 到 WAV。
        {
            string root=Root("pin-mismatch");var pinned=ApWav(3);ApWrite(root,"Sound/4403.wav",ApWav(4));
            var(r,d)=ApPlay(AudioAssets.FromClientRoot(root,[ApPin(4403,"mp3",null),ApPin(4403,"wav",pinned)]),4403);
            Check(!r.Success&&r.Status=="IntegrityFailure"&&ApStages(r)=="mp3:SourceUnavailable,wav:IntegrityFailure"&&d.Opened.Count==0,"audio-provider：WAV pin 不符回 IntegrityFailure（"+ApStages(r)+"）");
            var mp3Pinned=ApWav(5);var wav=ApWav(6);ApWrite(root,"Sound/4404.mp3",ApWav(7));ApWrite(root,"Sound/4404.wav",wav);
            var(r2,d2)=ApPlay(AudioAssets.FromClientRoot(root,[ApPin(4404,"mp3",mp3Pinned),ApPin(4404,"wav",wav)]),4404);
            Check(!r2.Success&&r2.Status=="IntegrityFailure"&&ApStages(r2)=="mp3:IntegrityFailure"&&d2.Opened.Count==0,"audio-provider：MP3 pin 不符不 fallback 到 WAV（"+ApStages(r2)+"）");
        }
        // 5. fallback：MP3 有 pin 但檔案不存在 → FileMissing 後同 ID WAV；MP3 存在且相符時優先 MP3。
        {
            string root=Root("fallback");var mp3=ApWav(8);var wav=ApWav(9);ApWrite(root,"Sound/4405.wav",wav);
            var(r,d)=ApPlay(AudioAssets.FromClientRoot(root,[ApPin(4405,"mp3",mp3),ApPin(4405,"wav",wav)]),4405);
            Check(r.Success&&r.Status=="StartedWavFallback"&&ApStages(r)=="mp3:FileMissing,wav:PlaySubmitted"&&d.Opened.Single().Format=="wav","audio-provider：MP3 缺檔 fallback 到 WAV（"+ApStages(r)+"）");
            ApWrite(root,"Sound/4406.mp3",mp3);ApWrite(root,"Sound/4406.wav",wav);
            var(r2,d2)=ApPlay(AudioAssets.FromClientRoot(root,[ApPin(4406,"mp3",mp3),ApPin(4406,"wav",wav)]),4406);
            Check(r2.Success&&r2.Status=="StartedMp3"&&ApStages(r2)=="mp3:PlaySubmitted"&&d2.Opened.Single().Format=="mp3","audio-provider：MP3 存在且相符時優先 MP3（"+ApStages(r2)+"）");
        }
        // 6. 產品 pin 套在合成根目錄：合成 3332.wav 不是原檔 → IntegrityFailure；1340 沒放 → FileMissing；其他 ID 不准播。
        {
            string root=Root("product-pins");ApWrite(root,"Sound/3332.wav",ApWav(10));
            var assets=AudioAssets.LoadEffect6016(root);
            var(r,d)=ApPlay(assets,3332);
            Check(!r.Success&&r.Status=="IntegrityFailure"&&ApStages(r)=="mp3:SourceUnavailable,wav:IntegrityFailure"&&d.Opened.Count==0,"audio-provider：產品 pin 拒絕非原檔的 3332.wav（"+ApStages(r)+"）");
            var(r2,_)=ApPlay(assets,1340);
            Check(!r2.Success&&r2.Status=="FileMissing"&&ApStages(r2)=="mp3:SourceUnavailable,wav:FileMissing","audio-provider：產品 pin 下缺 1340.wav 回 FileMissing（"+ApStages(r2)+"）");
            var(r3,_)=ApPlay(assets,4401);
            Check(!r3.Success&&r3.Status=="IdNotAllowed","audio-provider：6016 catalog 只准 1340、3332");
        }
        // 7. 根目錄與 pin 格式的拒絕；host 工廠的契約版本。
        {
            string absent=Path.Combine(run,"absent-client");
            static bool Throws<T>(Action a) where T:Exception{try{a();return false;}catch(T){return true;}}
            Check(Throws<DirectoryNotFoundException>(()=>AudioAssets.LoadEffect6016(absent)),"audio-provider：TW_CLIENT_ROOT 不存在時建立失敗");
            Check(Throws<InvalidDataException>(()=>AudioAssets.LoadEffect6016("")),"audio-provider：TW_CLIENT_ROOT 空字串時建立失敗");
            string root=Root("reject");var wav=ApWav(11);
            Check(Throws<InvalidDataException>(()=>AudioAssets.FromClientRoot(root,[new(4407,"mp3","sound/4407.mp3",null,null),ApPin(4407,"wav",wav)])),"audio-provider：pin 路徑不是 Sound/<id>.<ext> 時拒絕");
            Check(Throws<InvalidDataException>(()=>AudioAssets.FromClientRoot(root,[ApPin(4407,"mp3",null),new(4407,"wav",Path.Combine(root,"Sound","4407.wav"),wav.Length,ApSha(wav))])),"audio-provider：pin 帶絕對路徑時拒絕");
            Check(Throws<InvalidDataException>(()=>AudioAssets.FromClientRoot(root,[ApPin(4407,"wav",wav)])),"audio-provider：缺 MP3 宣告時拒絕（載入順序要完整）");
            Check(Throws<InvalidDataException>(()=>AudioAssets.FromClientRoot(root,[ApPin(4407,"mp3",null),new(4407,"wav","Sound/4407.wav",wav.Length,"zz")])),"audio-provider：SHA 格式錯誤時拒絕");
            Check(!AudioPlayer.TryCreate(root,"p24-audio-assets-v2",32,out var none,out var e1)&&none==null&&e1.Contains("contract"),"audio-provider：舊 manifest 版本被拒（"+e1+"）");
            Check(!AudioPlayer.TryCreate(absent,AudioPlayer.Effect6016Contract,32,out _,out var e2)&&e2.Length>0,"audio-provider：根目錄不存在時工廠回錯誤（"+e2+"）");
            // 建立播放器只起命令執行緒，不碰裝置（第一個 Play 才開 DirectShow）；這裡不播放，直接關閉。
            Check(AudioPlayer.TryCreate(root,AudioPlayer.Effect6016Contract,32,out var player,out var e3),"audio-provider：工廠以客戶端根目錄建立成功（"+e3+"）");
            Check(player!.ShutdownWithinAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult().Success,"audio-provider：工廠建立的播放器可釋放");
            Check(!File.Exists(Path.Combine(root,"audio-manifest.json"))&&!Directory.EnumerateFiles(run,"audio-manifest.json",SearchOption.AllDirectories).Any(),"audio-provider：整個流程不需要也不產生 audio-manifest.json");
        }
        // 8. 選用：本機原客戶端（唯讀）核對產品 pin。
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(client is {Length:>0}&&File.Exists(Path.Combine(client,"Sound","3332.wav"))){
            var assets=AudioAssets.LoadEffect6016(client);
            foreach(int id in new[]{3332,1340}){
                var(lease,error,_)=assets.Acquire(id,"wav");lease?.Dispose();
                Check(lease!=null&&error==null,$"audio-provider：原客戶端 Sound/{id}.wav 與程式 pin 相符（{error?.Stage}）");
            }
        }else labels.Add("audio-provider：略過原客戶端核對（未設 TW_CLIENT_ROOT 或沒有 Sound/3332.wav）");
    }
}
