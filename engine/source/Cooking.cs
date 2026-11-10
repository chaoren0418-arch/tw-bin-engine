using System.Security.Cryptography;
using System.Text;
using P06.NativeUi;
namespace P06;

// W2-F（T237／#559）：料理窗（CCookWindow，版面 uiextra86.xml COOK_BOOK_WINDOW）的資料模型。
// 依據 docs/reference/windows-2.md 第 7 節（T226／#533）與 docs/reference/server-protocol.md S_PacketBoxCooking（#132）：
//   3.81 伺服器：S250 C(52) C(0xdb) C(0x31) C(0xdf) C(0x02) C(1) C(value)
//   TW16 原版（S_EVENT 子碼 52）讀 D＋C＋C；前 4 byte 正好是 D＝0x02DF31DB，第二欄為 1 時開／切換料理窗，第三欄＋1 是料理書等級。
//   伺服器 Cooking_Book：操作 0（開啟料理書）時送 value＝料理書道具編號 − 41255；操作 1（製作）不送本封包。
// 已驗證（文件）：欄位順序與長度、第二欄 1＝開窗、等級＝value＋1。
// 已驗證（docs/reference/packetbox-windows.md 第 2.3 節）：第二欄 1 時，窗已開且等級相同 → 關窗；否則設等級後開（R4-D 改照此實作）。
// 推定／待驗證：第二欄不是 1 時原版改送 C_USE_ITEM（3.81 不會送，本模組只記錄、不開窗也不送包）；
//   第一欄 D 原版不檢查（本模組照收、只記錄是否等於 3.81 常數）；等級圖 2545–2555 依 value 取第幾張。
// 版面常數只記錄位置；畫面見 NativeUi/CookBookPanel.cs（R3-C #672、R4-D #707），原生 UI 關閉時見 CookBookFallbackUi.cs。
//
// R4-D（T295／#707）：料理清單與製作。
// - 清單：執行期從原客戶端 Text.pak 的 recipe.tbl 讀（CP950，記憶體內解碼，不存檔、不進 repo）。本機 0-客戶端 唯讀核對（2026-11-10）：
//   第一行筆數 27；每行 "名稱" "全名" 編號 欄4 材料數 ("材料" 數量)×材料數；名稱、材料是 $n（道具名稱表）；編號 0–23 與 80–82；欄4 全是 3（意義未查）。
// - 製作：C_USE_ITEM(164) D(料理書物件) C(1) C(編號)（docs/protocol/item-use.md，ItemUseArguments.Cooking）；料理書是收到 S250 52 時選取中的料理書。
//
// R6-F（T336／#782，依據 docs/reference/followups-7.md 第 3 節＝FU7§3，本輪 IDA 再核對）：
// - 已驗證 讀表 sub_A5CE60（FO 0x65C260）：每行「名稱、全名、編號 d、欄 4 h、材料數 c、材料×n」；欄 4 存在食譜 +0x104（word），
//   是製作秒數（S_MESSAGE_CODE 6392／6394 時倒數＝欄4×2，見 CookCountdown）。編號 ≥ 0x30（48）的整筆不算數（v27[4] −= v23）。
//   編號 < 24 的說明字串 id＝dword_137A0C0[3i+2]（.data FO 0xF788C0，i＝表內位置），≥ 24 時 0。
// - 已驗證 清單 sub_A5CDE0（FO 0x65C1E0）：表內位置 0..23 各加一格，圖示＝dword_137A0C0[3i+1]（RecipeIcons）。
// - 已驗證 等級篩選 sub_A5CB70（FO 0x65BF70）：前 8×L 格設為顯示（L＝料理書等級）。所以篩選是「表內順序、≥48 排除、前 24 格、前 8×L 筆」，
//   不是「編號 < 8×L」（本機 recipe.tbl 0–23 已依序排，兩者結果相同；80–82 因 ≥48 排除）。
// - 已驗證 製作 sub_A5D2A0（FO 0x65C6A0）：料理書不明（dword_153F294＝−1）→ 聊天列印字串 1167；倒數中（dword_153F28C＞0）→ 印 1100；
//   都不是才送 cdcc 208（C_USE_ITEM）。兩個提示都是 sub_7DC850(字, 色 24, 色 14)。
// - 推測：送出的「編號」原版是清單格的位置 i（清單項目 +4）；本機表位置＝編號，引擎沿用食譜的編號欄。
// - 推測：3.81 伺服器不送 6392／6394（本機 string-c.tbl 只有 3484 筆），所以倒數不會開始、量表不畫；本模組只提供倒數模型（BeginCookCountdown），
//   S_MESSAGE_CODE 的收包檔不在本任務範圍，沒有接。

/// <summary>料理窗目前狀態。Level＝value＋1（料理書等級）；Revision 每次變動遞增；BookObjectId＝開窗時選取中的料理書（0＝不明）。</summary>
public sealed record CookBookState(bool Open,byte Value,int Level,uint Magic,long Revision,uint BookObjectId=0);

public static class CookingProtocol {
    public const byte SPacketBox=250,CookWindow=52;
    /// <summary>3.81 伺服器固定送的第一欄（小端 db 31 df 02）。</summary>
    public const uint ServerMagic=0x02DF31DB;
    /// <summary>伺服器 Cooking_Book：value＝料理書道具編號 − 41255。</summary>
    public const int BookItemBase=41255;
    // uiextra86.xml COOK_BOOK_WINDOW（windows-2.md 第 7 節）：底圖、捲軸、等級圖（11 張）與位置、量表與位置。
    public const int BackgroundSurf=2254,ScrollSurf=2039,LevelSurfFirst=2545,LevelSurfLast=2555,GaugeSurf=2256;
    public static readonly (int X,int Y) LevelPosition=(116,257),GaugePosition=(35,251);
    /// <summary>已驗證（sub_A5CB70）：等級 L 顯示前 8×L 格。</summary>
    public const int RecipesPerLevel=8;
    /// <summary>已驗證（sub_A5CE60）：編號 ≥ 48 的整筆不算數。</summary>
    public const int ExcludedIndexFirst=48;
    /// <summary>已驗證（sub_A5CDE0 i&lt;24）：清單最多 24 格。</summary>
    public const int ListCapacity=24;
    /// <summary>已驗證（sub_A5CE60）：編號 &lt; 24 才有說明字串 id。</summary>
    public const int DescribedIndexLimit=24;
    /// <summary>C_USE_ITEM 料理書第一個參數：1＝製作。</summary>
    public const int MakeMode=1;
    /// <summary>已驗證（sub_A5D2A0）：沒有料理書時印的字串、倒數中印的字串、聊天列色（sub_7DC850 第 1 色）。</summary>
    public const int NeedBookText=1167,BusyText=1100,NoticeColorIndex=24;
    /// <summary>本機 string-c.tbl 的原文（字串表讀不到時用）。</summary>
    public const string NeedBookFallback="為了製作料理，所以需要料理書。",BusyFallback="正在執行中...";
    /// <summary>
    /// 已驗證：dword_137A0C0（.data FO 0xF788C0）每 3 個 dword 一組 (等級, 圖示, 說明字串)，表內位置 0..23。
    /// 圖示傳給清單項目（sub_88BAC0 的第 1 參數，與背包圖示同一套 id）；說明字串交給 sub_89AAE0 以 23 byte 一行切開，畫在提示框。
    /// </summary>
    public static readonly int[] RecipeIcons=[2612,2578,2609,2576,2610,2611,2580,2582, 2723,2721,2726,2725,2720,2722,2719,2724, 2949,2942,2948,2943,2945,2944,2947,2946];
    public static readonly int[] RecipeTexts=[1144,1145,1146,1147,1148,1149,1150,1151, 1211,1212,1213,1214,1215,1216,1217,1218, 1394,1395,1396,1397,1398,1399,1400,1401];
    /// <summary>字串表第 id 筆（ServerMessageText.Table）；讀不到時用 fallback。</summary>
    public static string Text(int id,string fallback)=>ServerMessageText.Table is {} t&&id>=0&&id<t.Count&&t[id].Trim().Length>0?ServerMessageText.DisplayText(t[id]):fallback;

    public static (uint Magic,byte Open,byte Value) Decode(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=SPacketBox||r.C()!=CookWindow)throw new ProtocolException("cook window opcode");
        uint magic=r.D();byte open=r.C(),value=r.C();r.Finish();
        return (magic,open,value);
    }
    public static int Level(byte value)=>value+1;
    public static int BookItemId(byte value)=>BookItemBase+value;
    /// <summary>等級圖（推定：第 value 張）；超出 11 張回 null。</summary>
    public static int? LevelSurf(byte value)=>LevelSurfFirst+value<=LevelSurfLast?LevelSurfFirst+value:null;
    /// <summary>
    /// 已驗證（sub_A5CE60＋sub_A5CDE0＋sub_A5CB70）：等級 level 的清單＝表內順序、編號 ≥ 48 排除、最多 24 格、前 8×level 筆。
    /// 回傳 (表內位置, 食譜)；位置決定圖示與說明字串。
    /// </summary>
    public static IEnumerable<(int Position,CookRecipe Recipe)> Listed(IEnumerable<CookRecipe> table,int level){
        if(level<=0)return [];
        return table.Where(r=>r.Index<ExcludedIndexFirst).Take(ListCapacity).Select((r,i)=>(i,r)).Take(Math.Min(ListCapacity,RecipesPerLevel*level));
    }
    public static int IconAt(int position)=>position>=0&&position<RecipeIcons.Length?RecipeIcons[position]:0;
    /// <summary>說明字串 id：編號 ≥ 24 時 0（sub_A5CE60 +264）。</summary>
    public static int TextAt(int position,int index)=>index<DescribedIndexLimit&&position>=0&&position<RecipeTexts.Length?RecipeTexts[position]:0;
    /// <summary>已驗證（sub_A5C5E0）：量表寬＝50 ×（剩餘 &gt;&gt; 1）／總量，高 8；剩餘 ≤ 0 或總量 ≤ 0 時不畫（0）。</summary>
    public const int GaugeWidth=50,GaugeHeight=8;
    public static int GaugePixels(int remaining,int total)=>remaining<=0||total<=0?0:Math.Clamp(GaugeWidth*(remaining>>1)/total,0,GaugeWidth);
}

/// <summary>
/// 製作倒數（已驗證，S_MESSAGE_CODE sub_942B40 VA 0x9441F0 起＋sub_A5B920）：開始時 剩餘＝食譜欄4×2、總量＝欄4，之後每 500 ms 剩餘減 1；
/// 剩餘已是 0 的下一個 500 ms 才把總量歸 0（倒數結束）。Start＝開始的毫秒時刻。
/// </summary>
public sealed record CookCountdown(int Seconds,long Start) {
    public const int TickMs=500;
    long Ticks(long now)=>Math.Max(0,(now-Start)/TickMs);
    /// <summary>now 時的剩餘（dword_153F288，半秒數）；結束後 0。</summary>
    public int Remaining(long now)=>Seconds<=0?0:(int)Math.Max(0,2L*Seconds-Ticks(now));
    /// <summary>總量還沒歸 0（dword_153F28C ＞ 0）：製作時印 1100、不送。</summary>
    public bool Busy(long now)=>Seconds>0&&Ticks(now)<=2L*Seconds;
    /// <summary>量表寬（只在剩餘 ＞ 0 時畫）。</summary>
    public int GaugePixels(long now)=>CookingProtocol.GaugePixels(Remaining(now),Busy(now)?Seconds:0);
}

/// <summary>recipe.tbl 的一行：名稱、全名、編號（C_USE_ITEM 送的值）、欄 4（意義未查）、材料。</summary>
public sealed record CookRecipe(string Name,string FullName,int Index,int Column4,IReadOnlyList<CookMaterial> Materials);
public sealed record CookMaterial(string Name,int Count);

/// <summary>原客戶端 Text.pak 的 recipe.tbl（執行期唯讀載入，不進 repo）。</summary>
public static class CookRecipeTable {
    public const string FileName="recipe.tbl";
    static Encoding Cp950{get{Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950);}}

    /// <summary>嚴格解析：筆數要相符、每行欄位要齊全、編號不重複且在 0–255（C_USE_ITEM 是一個 byte）。</summary>
    public static IReadOnlyList<CookRecipe> Parse(byte[] decoded){
        var lines=Cp950.GetString(decoded).Split('\n').Select(l=>l.TrimEnd('\r')).ToList();
        while(lines.Count>0&&lines[^1].Trim().Length==0)lines.RemoveAt(lines.Count-1);
        if(lines.Count==0||!int.TryParse(lines[0].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int count)||count<0||count>256)
            throw new InvalidDataException(FileName+"：第一行不是筆數");
        var body=lines.Skip(1).Where(l=>l.Trim().Length>0).ToList();
        if(body.Count!=count)throw new InvalidDataException($"{FileName}：筆數 {count} 與資料行數 {body.Count} 不符");
        var result=new List<CookRecipe>(count);var seen=new HashSet<int>();
        for(int i=0;i<body.Count;i++){
            var f=Tokens(body[i],i+2);
            CookRecipe r;
            try{
                if(f.Count<5||!f[0].Quoted||!f[1].Quoted)throw new FormatException();
                int index=Number(f[2]),column4=Number(f[3]),materials=Number(f[4]);
                if(index>255||f.Count!=5+2*materials)throw new FormatException();
                var list=new List<CookMaterial>(materials);
                for(int m=0;m<materials;m++){
                    var name=f[5+2*m];if(!name.Quoted)throw new FormatException();
                    list.Add(new(name.Text,Number(f[6+2*m])));
                }
                r=new(f[0].Text,f[1].Text,index,column4,list);
            }catch(FormatException){throw new InvalidDataException($"{FileName}：第 {i+2} 行欄位不符");}
            if(!seen.Add(r.Index))throw new InvalidDataException($"{FileName}：編號 {r.Index} 重複");
            result.Add(r);
        }
        return result;
    }
    readonly record struct Token(string Text,bool Quoted);
    static int Number(Token t)=>!t.Quoted&&int.TryParse(t.Text,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int v)?v:throw new FormatException();
    static List<Token> Tokens(string line,int lineNo){
        var result=new List<Token>();int i=0;
        while(i<line.Length){
            if(char.IsWhiteSpace(line[i])){i++;continue;}
            if(line[i]=='"'){
                int end=line.IndexOf('"',i+1);
                if(end<0)throw new InvalidDataException($"{FileName}：第 {lineNo} 行引號未結束");
                result.Add(new(line[(i+1)..end],true));i=end+1;
            }else{
                int start=i;while(i<line.Length&&!char.IsWhiteSpace(line[i]))i++;
                result.Add(new(line[start..i],false));
            }
        }
        return result;
    }

    static readonly object gate=new();
    static IReadOnlyList<CookRecipe>? table,overrideTable;static bool loaded,overridden;
    public static string? TableError{get;private set;}
    /// <summary>執行期的表（第一次用到才從 <see cref="GroundSpriteReader.ClientRoot"/> 讀）；讀不到時 null（清單空白，仍可從背包對話框輸入編號）。</summary>
    public static IReadOnlyList<CookRecipe>? Table{get{
        lock(gate){
            if(overridden)return overrideTable;
            if(!loaded){
                loaded=true;
                try{
                    var archive=TextArchive.Open(GroundSpriteReader.ClientRoot);
                    table=Parse(archive.Read(archive.Single(FileName)??throw new InvalidDataException("Text 封存沒有 "+FileName)));TableError=null;
                }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or DecoderFallbackException){
                    table=null;TableError=ex.Message;System.Diagnostics.Trace.WriteLine("[cooking] "+FileName+" 無法載入："+ex.Message);
                }
            }
            return table;
        }
    }}
    /// <summary>測試用：暫時換掉表（null＝沒有表），直到回傳的 scope 被 Dispose。</summary>
    internal static IDisposable UseTable(IReadOnlyList<CookRecipe>? replacement){
        lock(gate){var previous=(overridden,overrideTable);overridden=true;overrideTable=replacement;return new Scope(()=>{lock(gate)(overridden,overrideTable)=previous;});}
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}

    /// <summary>料理窗要列的食譜（R6-F：表內順序、≥48 排除、前 8×L 筆，見 CookingProtocol.Listed），名稱與材料經 names 代換 $n。</summary>
    public static IReadOnlyList<CookBookRecipe> Rows(int level,Func<string,string>? names=null,IReadOnlyList<CookRecipe>? recipes=null){
        var source=recipes??Table;
        if(source==null||level<=0)return [];
        string N(string s)=>names==null?s:names(s);
        return CookingProtocol.Listed(source,level)
            .Select(x=>new CookBookRecipe(x.Recipe.Index,N(x.Recipe.Name),string.Join("、",x.Recipe.Materials.Select(m=>$"{N(m.Name)} ×{m.Count}")),
                CookingProtocol.IconAt(x.Position),CookingProtocol.TextAt(x.Position,x.Recipe.Index),x.Recipe.Column4)).ToArray();
    }
}

public sealed partial class WorldSession {
    // R3-C（T279／#672）：網路執行緒收包、UI 執行緒關窗，兩邊都改 cookBook，所以加鎖。
    readonly object cookBookGate=new();
    CookBookState cookBook=new(false,0,0,0,0);
    public CookBookState CookBook{get{lock(cookBookGate)return cookBook;}}

    [WorldReceive(CookingProtocol.SPacketBox,Subtype=CookingProtocol.CookWindow)]
    bool ReceiveCookWindow(byte[] plain){
        var (magic,open,value)=CookingProtocol.Decode(plain);
        // 開窗時選取中的料理書＝剛送出「開啟料理書」的那一本（引擎的道具使用一律對選取中的道具）。
        uint book=SelectedItem is {} item&&ItemUseCodec.Kind(item.UseType)==ItemUseKind.Cooking?item.ObjectId:0;
        lock(cookBookGate){
            // 推定：第二欄不是 1 時原版不開窗；本模組只記錄 magic，不改開關與等級。
            if(open!=1){cookBook=cookBook with{Magic=magic,Revision=cookBook.Revision+1};return true;}
            int level=CookingProtocol.Level(value);
            // PBW§2.3（已驗證）：已開且等級相同 → 關。
            if(cookBook.Open&&cookBook.Level==level){cookBook=cookBook with{Open=false,Magic=magic,Revision=cookBook.Revision+1};return true;}
            cookBook=new(true,value,level,magic,cookBook.Revision+1,book!=0?book:cookBook.Open?cookBook.BookObjectId:0);
        }
        return true;
    }
    /// <summary>只關掉 revision 這一次開的窗；之後已收到新的 S250 52（revision 不同）時不動，回 false。本機動作，不送封包。</summary>
    public bool CloseCookBook(long revision){
        lock(cookBookGate){
            if(!cookBook.Open||cookBook.Revision!=revision)return false;
            cookBook=cookBook with{Open=false,Revision=cookBook.Revision+1};return true;
        }
    }
    /// <summary>製作時對哪一本料理書送 C_USE_ITEM：開窗時記下的那本；不明時背包裡唯一的一本料理書；否則 null。</summary>
    public InventoryItem? CookingBookItem(CookBookState book){
        var books=Inventory.Where(i=>i.Count>0&&ItemUseCodec.Kind(i.UseType)==ItemUseKind.Cooking).ToArray();
        if(book.BookObjectId!=0)return books.FirstOrDefault(i=>i.ObjectId==book.BookObjectId);
        return books.Length==1?books[0]:null;
    }

    // R6-F：製作倒數（CookCountdown）。原版由 S_MESSAGE_CODE 6392／6394 開始；3.81 不送（推測），所以平常是 null。
    CookCountdown? cookCountdown;
    public CookCountdown? CookingCountdown{get{lock(cookBookGate)return cookCountdown;}}
    /// <summary>開始製作倒數（秒數＝食譜欄 4）。收包端沒有接（S_MESSAGE_CODE 的檔案不在 R6-F 範圍），測試與日後接線用。</summary>
    internal void BeginCookCountdown(int seconds,long now){lock(cookBookGate)cookCountdown=seconds>0?new(seconds,now):null;}
    /// <summary>R6-F：sub_A5D2A0 的提示（1167／1100）印在聊天列，色 24。</summary>
    internal void AddCookingNotice(int id){
        string text=CookingProtocol.Text(id,id==CookingProtocol.NeedBookText?CookingProtocol.NeedBookFallback:CookingProtocol.BusyFallback);
        playerChatLines.Enqueue(new(++playerChatSequence,0,text){ColorIndex=CookingProtocol.NoticeColorIndex});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
        ItemNotice=text;
    }

    [WorldReset]
    void ResetCooking(){lock(cookBookGate){cookBook=new(false,0,0,0,cookBook.Revision+1);cookCountdown=null;}}
    [WorldSnapshot("cooking")]
    object? CookingSnapshot(){
        var book=CookBook;
        return new{open=book.Open,value=book.Value,level=book.Level,
            bookItemId=book.Level>0?CookingProtocol.BookItemId(book.Value):0,
            levelSurf=book.Level>0?CookingProtocol.LevelSurf(book.Value):null,
            magic=$"0x{book.Magic:X8}",magicMatches381=book.Magic==CookingProtocol.ServerMagic,revision=book.Revision,bookObjectId=book.BookObjectId,
            ui="native COOK_BOOK_WINDOW (NativeUi/CookBookPanel.cs) when native UI is on, WinForms list (CookBookFallbackUi.cs) otherwise; recipes from client recipe.tbl (R4-D #707)"};
    }
}

public sealed partial class LoginConnection {
    /// <summary>
    /// 料理窗選食譜製作：對料理書送 C_USE_ITEM(164) D(物件) C(1) C(編號)，走既有道具使用的閘門（忙碌、速度、選取都要對）。
    /// revision＝畫面上那一次開的料理窗；窗已關或已換等級時不送。
    /// </summary>
    public Task<bool> CookRecipe(long revision,int recipe){
        var book=World.CookBook;
        if(!book.Open||book.Revision!=revision){World.ItemNotice="未送出：料理窗已關閉或已更新，請重新開啟料理書";return Task.FromResult(false);}
        if(recipe is <0 or >255){World.ItemNotice="未送出：食譜編號無效";return Task.FromResult(false);}
        // R6-F（sub_A5D2A0）：料理書不明 → 1167；倒數中 → 1100；兩者都印在聊天列、不送。
        if(World.CookingBookItem(book) is not {} item){World.AddCookingNotice(CookingProtocol.NeedBookText);return Task.FromResult(false);}
        if(World.CookingCountdown is {} countdown&&countdown.Busy(Environment.TickCount64)){World.AddCookingNotice(CookingProtocol.BusyText);return Task.FromResult(false);}
        if(State!=LoginState.InWorld||World.Actor is not {} actor){World.ItemNotice="未送出：尚未進入遊戲";return Task.FromResult(false);}
        if(World.SelectedItemId!=item.ObjectId&&!World.SelectItem(item.ObjectId)){World.ItemNotice="未送出：料理書已離開背包";return Task.FromResult(false);}
        var request=new ItemUseRequest(item,generation,World.WorldEpoch,World.MapId,actor.ObjectId,
            World.ItemSelectionRevision,World.InventoryContentRevision,World.Corrections,actor.X,actor.Y);
        PendingItemUse=request;
        return UseItemWithArguments(request,new ItemUseArguments.Cooking(CookingProtocol.MakeMode,recipe));
    }
}

/// <summary>R3-C（#672）：把 WorldSession 的料理窗狀態轉給原生面板（面板不碰 LoginConnection）。R4-D：加食譜清單與製作。</summary>
public sealed class WorldCookBookSource(Func<WorldSession> world,Func<string,string>? names=null,Func<long,int,Task<bool>>? cook=null,Func<long>? clock=null):ICookBookSource {
    (long Revision,int Level,IReadOnlyList<CookBookRecipe> Rows)? cache;
    public CookBookView? Current{get{
        var w=world();var b=w.CookBook;if(!b.Open)return null;
        if(cache is not {} c||c.Revision!=b.Revision||c.Level!=b.Level){c=(b.Revision,b.Level,CookRecipeTable.Rows(b.Level,names));cache=c;}
        // R6-F：量表只在製作倒數中畫（3.81 不開始倒數 → 0，不畫）。
        int gauge=w.CookingCountdown is {} cd?cd.GaugePixels((clock??(()=>Environment.TickCount64))()):0;
        return new(b.Revision,b.Value,b.Level,c.Rows,gauge);
    }}
    public void Close(long revision)=>world().CloseCookBook(revision);
    public bool Cook(long revision,int recipe){
        if(cook==null)return false;
        _=cook(revision,recipe);return true;
    }
}

public sealed partial class ClientForm {
    /// <summary>
    /// 每個 timer tick（ClanWarehouseHistoryUi.cs 的共用 tick）：原生 UI 開著時把料理窗面板接上本連線的 WorldSession；
    /// 原生 UI 沒開時改用 WinForms 暫行清單（CookBookFallbackUi.cs）。
    /// </summary>
    void AttachNativeCookBook(){
        if(NativeUiActive&&NativeUiRoot?.Find(CookBookPanel.PanelId) is CookBookPanel panel&&panel.Source==null)
            panel.Source=new WorldCookBookSource(()=>login.World,ResolveNpcText,(revision,recipe)=>login.CookRecipe(revision,recipe));
        SyncCookBookFallback();
    }
}
