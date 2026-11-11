using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace P06;

// T406（#923，FU13§2 D1–D2）：選目標模式的瞄準游標。
// 已驗證（FU13§2.1，TW1608262200.bin SHA-256 e12949cf…09641a2）：
// - sub_8EECE0(n)（VA 0x8EECE0／FO 0x4EE0E0）換游標；sub_8EEBF0（FO 0x4EDFF0）用 LoadCursorA(hInstance, MAKEINTRESOURCE(id)) 載入。
// - 單色組 id 表 lpCursorName（VA 0x13720E8／FO 0xF708E8），彩色組 id 表 dword_1372180（VA 0x1372180／FO 0xF70980）；
//   本任務用 pefile 重讀一次（scratch/t923/cur.py，不進 repo），下表數值相同。
// - 世界滑鼠處理 sub_F02BA0 → sub_F044C0（0xF04688 起）：選目標模式中預設 8（byte_139FBAF／byte_13CE3F8 為真時 15）；
//   回呼 type 4（CSpellCallback）時 sub_E23BF0(0) 為假 → 20（24），為真時在範圍內 → 8（15），不在 → 19（23）。
// - 離開模式 sub_F68E60 最後 sub_8EECE0(3)（手形）。
// 推測：彩色／單色由 byte_13A931E 決定，預設值沒查，引擎先用彩色組、讀不到才用單色組；
//       sub_E23BF0(0) 的意義沒查，引擎當作真；byte_139FBAF／byte_13CE3F8 引擎當作假（不出 15／23／24）。
// 游標圖只在執行時從使用者本機的原版 BIN 唯讀讀出（記 SHA-256），不複製進 git；讀不到時瞄準游標用系統 Cursors.Cross（引擎自訂）。

/// <summary>sub_8EECE0 的游標索引與 BIN 內 RT_GROUP_CURSOR 資源 id（FU13§2.1 表）。</summary>
public static class GameCursors {
    public const int Hand=3,Aim=8,AimAlternate=15,AimOutOfRange=19,AimNoRange=20,AimOutOfRangeAlternate=23,AimNoRangeAlternate=24;
    /// <summary>lpCursorName（VA 0x13720E8）全部 36 筆單色組資源 id。</summary>
    public static readonly int[] MonochromeIds=[0,130,131,105,127,132,133,144,136,138,139,140,141,142,143,145,150,224,219,220,227,225,226,233,234,165,236,181,180,138,138,133,136,224,105,143];
    /// <summary>dword_1372180（VA 0x1372180）全部 36 筆彩色組資源 id。</summary>
    public static readonly int[] ColorIds=[0,203,204,202,201,205,206,215,208,209,210,211,212,213,214,216,217,222,221,223,229,228,230,231,232,168,235,170,171,172,173,174,175,176,177,178];
    public static int ResourceId(int index,bool color)=>index>=0&&index<MonochromeIds.Length?(color?ColorIds:MonochromeIds)[index]:0;
}

/// <summary>sub_F044C0 選目標分支的游標選擇（純函式，可離線測試）。</summary>
public static class SpellCursorRules {
    /// <param name="picking">選目標模式中（sub_F68FE0 回呼不為空）。不在模式中回 3（sub_F68E60 → sub_8EECE0(3)）。</param>
    /// <param name="spellCallback">回呼類型 4（CSpellCallback，技能）。</param>
    /// <param name="rangeKnown">sub_E23BF0(0) 的結果（意義未查，引擎傳 true）。</param>
    /// <param name="targetInRange">滑鼠下對象是否在範圍內（sub_523AC0）；沒有對象時 null，視為在範圍內（推測）。</param>
    /// <param name="alternate">byte_139FBAF 或 byte_13CE3F8（引擎傳 false）。</param>
    public static int Index(bool picking,bool spellCallback,bool rangeKnown=true,bool? targetInRange=null,bool alternate=false){
        if(!picking)return GameCursors.Hand;
        if(!spellCallback)return alternate?GameCursors.AimAlternate:GameCursors.Aim;
        if(!rangeKnown)return alternate?GameCursors.AimNoRangeAlternate:GameCursors.AimNoRange;
        return targetInRange!=false?(alternate?GameCursors.AimAlternate:GameCursors.Aim):(alternate?GameCursors.AimOutOfRangeAlternate:GameCursors.AimOutOfRange);
    }
}

/// <summary>唯讀 PE 資源解析：RT_GROUP_CURSOR（12）→ RT_CURSOR（1），回傳 CreateIconFromResourceEx 可用的位元組（熱點 2×WORD＋DIB）。</summary>
public static class PeCursorResources {
    const int RtCursor=1,RtGroupCursor=12;
    public sealed record CursorImage(int GroupId,int CursorId,int Width,int Height,int BitCount,int HotX,int HotY,byte[] Bytes);

    static ushort U16(byte[] a,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(a.AsSpan(at,2));
    static uint U32(byte[] a,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(a.AsSpan(at,4));

    public static CursorImage? Read(byte[] pe,int groupId){
        try{
            if(pe.Length<0x40||pe[0]!='M'||pe[1]!='Z')return null;
            int nt=(int)U32(pe,0x3C);
            if(nt<0||nt+24>pe.Length||U32(pe,nt)!=0x4550)return null;
            int sections=U16(pe,nt+6),optSize=U16(pe,nt+20);
            int opt=nt+24;int dirs=opt+(U16(pe,opt)==0x20B?112:96);
            uint resRva=U32(pe,dirs+2*8);if(resRva==0)return null;
            int secTable=opt+optSize;
            int ToOffset(uint rva){
                for(int i=0;i<sections;i++){int s=secTable+40*i;uint vs=U32(pe,s+8),va=U32(pe,s+12),rs=U32(pe,s+16),raw=U32(pe,s+20);
                    if(rva>=va&&rva<va+Math.Max(vs,rs))return checked((int)(rva-va+raw));}
                return -1;
            }
            int root=ToOffset(resRva);if(root<0)return null;
            // 目錄項的 offset：高位元＝子目錄（低 31 位元是相對資源根的位移）；沒有高位元＝資料項（RVA＋大小）。找不到回 null。
            const uint Sub=0x80000000;
            uint? Entry(int dir,int id){
                int count=U16(pe,dir+12)+U16(pe,dir+14);
                for(int i=0;i<count;i++){int e=dir+16+8*i;uint name=U32(pe,e);if((name&Sub)==0&&name==(uint)id)return U32(pe,e+4);}
                return null;
            }
            uint? First(int dir)=>U16(pe,dir+12)+U16(pe,dir+14)==0?null:U32(pe,dir+20);
            byte[]? Data(int type,int id){
                if(Entry(root,type) is not uint t||(t&Sub)==0)return null;
                if(Entry(checked(root+(int)(t&~Sub)),id) is not uint n||(n&Sub)==0)return null;
                if(First(checked(root+(int)(n&~Sub))) is not uint l||(l&Sub)!=0)return null;
                int leaf=checked(root+(int)l);uint rva=U32(pe,leaf),size=U32(pe,leaf+4);
                int at=ToOffset(rva);if(at<0||size>pe.Length-at)return null;return pe.AsSpan(at,(int)size).ToArray();
            }
            var group=Data(RtGroupCursor,groupId);if(group==null||group.Length<6+14)return null;
            int count=U16(group,4);if(U16(group,2)!=2||count==0||group.Length<6+14*count)return null;
            // 取色深最高的一筆（BIN 每組只有 1 筆）。
            int best=0;for(int i=1;i<count;i++)if(U16(group,6+14*i+6)>U16(group,6+14*best+6))best=i;
            int g=6+14*best;
            int w=U16(group,g),h=U16(group,g+2),bits=U16(group,g+6),cid=U16(group,g+12);
            var cur=Data(RtCursor,cid);if(cur==null||cur.Length<4+40)return null;
            return new(groupId,cid,w,h,bits,U16(cur,0),U16(cur,2),cur);
        }catch(Exception ex) when(ex is ArgumentOutOfRangeException or ArgumentException or OverflowException or IndexOutOfRangeException){return null;}
    }}

/// <summary>執行時從本機原版 BIN 唯讀讀游標；讀不到時瞄準類用 Cursors.Cross、手形用 Cursors.Default（引擎自訂）。</summary>
public static class GameCursorSource {
    /// <summary>原版 TW1608262200.bin 的已知 SHA-256（FU13§0）。</summary>
    public const string KnownSha256="e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2";
    public static string BinPath=>Environment.GetEnvironmentVariable("TW_ORIGINAL_BIN")??@"D:\lineage開發\TW1608262200研究\P01-分析基準\input\TW1608262200.bin";
    static readonly object gate=new();
    static bool loaded;static byte[]? bin;
    static readonly Dictionary<int,Cursor> cache=[];
    public static string? Sha256 {get;private set;}
    /// <summary>沒讀到 BIN 游標、改用系統游標時為 true（報告與 UI 標「引擎自訂」）。</summary>
    public static bool EngineCustom {get;private set;}
    public static string Notice {get;private set;}="尚未讀取原版游標";

    static byte[]? Bin(){
        if(loaded)return bin;loaded=true;
        try{
            var bytes=File.ReadAllBytes(BinPath);Sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();bin=bytes;
            Notice=Sha256==KnownSha256?$"游標取自原版 BIN（SHA-256 {Sha256}）":$"游標取自 BIN，但 SHA-256 {Sha256} 與已知 {KnownSha256} 不同";
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){bin=null;Notice="讀不到原版 BIN（"+ex.Message+"），瞄準游標用系統十字（引擎自訂）";}
        return bin;
    }

    /// <summary>sub_8EECE0(index) 對應的游標。</summary>
    public static Cursor Get(int index){
        lock(gate){
            if(cache.TryGetValue(index,out var hit))return hit;
            Cursor? cursor=null;
            if(Bin() is {} pe&&OperatingSystem.IsWindows()){
                var image=PeCursorResources.Read(pe,GameCursors.ResourceId(index,true))??PeCursorResources.Read(pe,GameCursors.ResourceId(index,false));
                if(image!=null){IntPtr h=CreateIconFromResourceEx(image.Bytes,(uint)image.Bytes.Length,false,0x00030000,0,0,0);if(h!=IntPtr.Zero)cursor=new Cursor(h);}
            }
            if(cursor==null){EngineCustom=true;cursor=index==GameCursors.Hand?Cursors.Default:Cursors.Cross;}
            cache[index]=cursor;return cursor;
        }
    }

    [DllImport("user32.dll",SetLastError=true)]
    static extern IntPtr CreateIconFromResourceEx(byte[] presbits,uint dwResSize,bool fIcon,uint dwVer,int cxDesired,int cyDesired,uint flags);
}
