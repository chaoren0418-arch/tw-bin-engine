namespace P06;

// T172（#446）：`music.def` 的解析與區域選曲。依據 docs/reference/sound.md §4.2–4.5、§6.2（T163／T171）。
// 內容在執行期從使用者原客戶端的 Text 包讀取（SoundDefinitionSource），本專案不封裝解碼表。
public sealed record MusicArea(int Map,int X1,int Y1,int X2,int Y2)
{
    // 8F31F0：x1 == 0 代表整張地圖；否則包含右下邊界（x1 ≤ x ≤ x2、y1 ≤ y ≤ y2）。
    public bool Contains(int map,int x,int y)=>map==Map&&(X1==0||x>=X1&&x<=X2&&y>=Y1&&y<=Y2);
}
// 欄位初值 -1；MUSIC 是實際檔名編號清單（sound\musicN.mp3 的 N），不是系統槽。
public sealed record MusicRegion(int Ordinal,MusicArea[] Areas,int[] Music,int Interval,int Sprite,int Spawn,int Battle,int Death,int TwinSprite1,int TwinSprite2)
{
    public bool Matches(int map,int x,int y)=>Areas.Any(a=>a.Contains(map,x,y));
    public bool TwinBoss=>TwinSprite1!=-1&&TwinSprite2!=-1;
}

public sealed class MusicDefinition
{
    public const int SystemSlots=202,LargeMapThreshold=15000,LargeMapMusic=23;
    // 系統槽（dword_13D3F90）用途：呼叫端使用的索引，值才是 musicN 的 N。
    public const int SlotLogin=0,SlotCredits=8,SlotHunting=9,SlotPlayerKilling=10,SlotPledgeWar=11,SlotDeath=18;
    // 8F6160：這 5 個槽的曲目正在播時，8F1C30／8F3B90 不會打斷。
    public static readonly int[] UninterruptibleSlots=[12,13,15,16,17];
    readonly int[] slots=Enumerable.Range(0,SystemSlots).ToArray(); // 8F1FA0：預設 i → i。
    public IReadOnlyList<int> Slots=>slots;
    public int SystemSettings {get;private set;}
    public MusicRegion[] Regions {get;private set;}=[];
    public int Slot(int index)=>index>=0&&index<SystemSlots?slots[index]:-1;

    static bool Numeric(string token)=>token.Length>0&&token[0] is >='0' and <='9';
    // 原版 atoi：讀前綴數字，溢位前截止；只在首字元為數字時呼叫（Numeric）。
    static int Atoi(string token,int start=0)
    {
        long value=0;int i=start;
        while(i<token.Length&&token[i] is >='0' and <='9'){value=value*10+(token[i]-'0');if(value>int.MaxValue)return int.MaxValue;i++;}
        return (int)value;
    }

    // 8F6810：以空白／tab／LF 分詞；CR 也當成分隔（本機檔案是 CRLF，相容處理，見 docs/protocol/music.md）。
    static List<string> Tokens(string text)=>text.Split([' ','\t','\n','\r','\v','\f'],StringSplitOptions.RemoveEmptyEntries).ToList();

    public static MusicDefinition Parse(string text)
    {
        var def=new MusicDefinition();var tokens=Tokens(text??"");int i=0;
        string? Next()=>i<tokens.Count?tokens[i++]:null;
        // 系統段：`System Setting Begin` 到 `SystemMenu Exit`；只找 `=` 後的數字，依出現順序寫入槽 0..。
        int begin=tokens.FindIndex(t=>t=="System");
        if(begin>=0&&begin+2<tokens.Count&&tokens[begin+1]=="Setting"&&tokens[begin+2]=="Begin")
        {
            int slot=0;i=begin+3;
            while(i<tokens.Count&&tokens[i]!="SystemMenu")
            {
                string t=tokens[i++];int eq=t.IndexOf('=');
                if(t.StartsWith(';')||eq<0)continue;
                if(slot<SystemSlots){def.slots[slot]=eq+1<t.Length&&t[eq+1] is >='0' and <='9'?Atoi(t,eq+1):0;slot++;}
            }
            def.SystemSettings=slot;
            if(i<tokens.Count)i++; // SystemMenu
        }
        else i=0;
        var regions=new List<MusicRegion>();
        // 主 parser 狀態。LOC 的座標暫存跨記錄保留（新位置只重設 map 與 x1；缺的座標留下先前值）。
        int[] loc=new int[5];
        List<MusicArea>? areas=null;List<int> music=[];
        int interval=-1,sprite=-1,spawn=-1,battle=-1,death=-1,twin1=-1,twin2=-1;
        void Commit()
        {
            if(areas==null)return;
            regions.Add(new(regions.Count,areas.ToArray(),music.ToArray(),interval,sprite,spawn,battle,death,twin1,twin2));
            areas=null;
        }
        // 單值欄位：連續數字 token 都讀，最後一個生效（BATTLE 162 … 166 → 166）；首字元不是數字就保留原值。
        int Single(int current){int value=current;while(i<tokens.Count&&Numeric(tokens[i]))value=Atoi(tokens[i++]);return value;}
        string? token;
        while((token=Next())!=null)
        {
            if(token.StartsWith(';'))continue;
            switch(token)
            {
                case "LOC":
                {
                    Commit();
                    areas=[];music=[];interval=sprite=spawn=battle=death=twin1=twin2=-1;
                    int field=0;bool any=false;
                    loc[0]=0;loc[1]=0;
                    void Flush(){if(any)areas!.Add(new(loc[0],loc[1],loc[2],loc[3],loc[4]));any=false;field=0;loc[0]=0;loc[1]=0;}
                    while(i<tokens.Count)
                    {
                        string t=tokens[i];
                        if(t=="\\"){i++;Flush();continue;}
                        if(!Numeric(t))break;
                        i++;
                        if(field<5){loc[field]=Atoi(t);any=true;}
                        field++;
                    }
                    Flush();
                    break;
                }
                case "MUSIC":
                    if(areas==null){Single(0);break;}
                    while(i<tokens.Count&&Numeric(tokens[i]))music.Add(Atoi(tokens[i++]));
                    break;
                case "INTERVAL":interval=Single(interval);break;
                case "SPRITE":sprite=Single(sprite);break;
                case "SPAWN":spawn=Single(spawn);break;
                case "BATTLE":battle=Single(battle);break;
                case "DEATH":death=Single(death);break;
                case "TWIN_SPRITE1":twin1=Single(twin1);break;
                case "TWIN_SPRITE2":twin2=Single(twin2);break;
                default:break; // 註解的後續詞、未支援段落（Planarization、LoginMusic 本檔沒有）略過。
            }
        }
        Commit();
        def.Regions=regions.ToArray();
        return def;
    }

    // 8F31F0：引擎座標已是地圖座標（原版 ((x-32768)>>1)+32768 換算後的值），直接比對。
    public MusicRegion? Region(int map,int x,int y)=>map>LargeMapThreshold?null:Regions.FirstOrDefault(r=>r.Matches(map,x,y));
}
