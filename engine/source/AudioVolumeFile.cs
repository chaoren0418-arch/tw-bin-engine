using System.Globalization;
using System.Text;
using P06.NativeUi;
namespace P06;

/// <summary>
/// R12-C（T430／#987，gap-matrix 15.14.5「4:OptionUI」、15.14.7 第 1 點裁定「照原版做，類別音量要存檔」）：四類音量的存檔。
/// 原版在 `sub_814800`（FO `0x413C00`）讀 `lineage.cfg`、`sub_814020` 寫回（sound.md §4.1）；引擎**不讀也不寫正式客戶端的
/// `lineage.cfg`**（CLAUDE.md 規則 7），改存引擎自己的 `runtime/user/sound-volume.cfg`（同 `summon-quick.json` 的位置）。
/// 鍵名照 lineage.cfg：`SoundVolume=`、`EnvSoundVolume=`、`VoiceVolume=`、`MusicVolume=`，一行一個。
/// 值是 0–100 的整數（引擎取捨：sound.md 第 256 行曲目音量是 `MusicVolume / 100`，推定原版也是百分比；正式 lineage.cfg 是二進位檔，
/// 欄位格式沒有逐 byte 對照）。引擎的類別音量是 0..1、0.01 一格（捲軸刻度），所以存回讀出逐值相同。
/// 讀：檔案不存在 → 什麼都不改（四類維持預設 1）；某一行格式不對或超界 → 那一類不改，其他照讀；不認得的行忽略。
/// 寫：先寫 .tmp 再取代；內容與上次寫的相同就不寫（拖捲軸時 0.01 才變一次）。讀寫失敗只記一行日誌，不擋遊戲。
/// </summary>
public sealed class AudioVolumeFile(string path) {
    public const string FileName="sound-volume.cfg";
    public static string DefaultPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","runtime","user",FileName));
    /// <summary>寫出順序＝類別編號 0..3（sound.md §4.1 的表）。</summary>
    public static readonly IReadOnlyList<(SoundCategory Category,string Key)> Keys=[
        (SoundCategory.Sound,"SoundVolume"),(SoundCategory.EnvSound,"EnvSoundVolume"),(SoundCategory.Voice,"VoiceVolume"),(SoundCategory.Music,"MusicVolume")];
    public const int Scale=100;
    public string FilePath=>path;
    string? written;

    /// <summary>讀檔套進 volumes；回傳實際讀到的類別數（檔案不存在或讀不到為 0）。</summary>
    public int Load(SoundVolumes volumes){
        string text;
        try{
            if(!File.Exists(path))return 0;
            text=File.ReadAllText(path,Encoding.UTF8);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){UiResourceLog.Write($"{FileName} 無法讀取，類別音量維持預設：{ex.Message}");return 0;}
        var levels=Parse(text);
        foreach(var (category,level) in levels)volumes[category]=level;
        written=text==Serialize(volumes)?text:null;
        return levels.Count;
    }

    /// <summary>把四類音量寫回檔案；內容沒變就不寫。成功（或不必寫）回 true。</summary>
    public bool Save(SoundVolumes volumes){
        string text=Serialize(volumes);
        if(text==written)return true;
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp=path+".tmp";File.WriteAllText(tmp,text,new UTF8Encoding(false));File.Move(tmp,path,true);
            written=text;return true;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){UiResourceLog.Write($"{FileName} 無法寫入，類別音量只保留到關閉程式：{ex.Message}");return false;}
    }

    /// <summary>解析 `鍵=值` 行；只收四個鍵、值為 0–100 的整數，同一鍵出現多次以最後一次為準。</summary>
    public static Dictionary<SoundCategory,double> Parse(string text){
        var result=new Dictionary<SoundCategory,double>();
        foreach(string raw in text.Split('\n')){
            string line=raw.Trim();
            int eq=line.IndexOf('=');
            if(eq<=0)continue;
            string key=line[..eq].Trim(),value=line[(eq+1)..].Trim();
            int k=-1;for(int i=0;i<Keys.Count;i++)if(Keys[i].Key==key){k=i;break;}
            if(k<0)continue;
            if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n>Scale){result.Remove(Keys[k].Category);continue;}
            result[Keys[k].Category]=n/(double)Scale;
        }
        return result;
    }

    /// <summary>四行 `鍵=值`（CRLF），值＝類別音量 × 100 四捨五入。</summary>
    public static string Serialize(SoundVolumes volumes){
        var sb=new StringBuilder();
        foreach(var (category,key) in Keys)sb.Append(key).Append('=').Append(Percent(volumes[category]).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        return sb.ToString();
    }
    public static int Percent(double level)=>(int)Math.Round(Math.Clamp(level,0,1)*Scale,MidpointRounding.AwayFromZero);
}
