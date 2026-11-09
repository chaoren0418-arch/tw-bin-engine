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
// 推測（標明，待真機或 IDA 查 CCookWindow sub_A5AAC0 確認）：
//   - 料理書等級 L 列出編號 < 8×L 的食譜（每級 8 道）；編號 80–82 的特殊食譜不列（不知道原版何時顯示），仍可從背包的料理書對話框輸入編號製作。
//   - 量表 2256 的填充比例＝等級／11（等級圖 11 張）；原版量表的資料來源未查。

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
    /// <summary>推測：每一級料理書多 8 道食譜。</summary>
    public const int RecipesPerLevel=8;
    /// <summary>recipe.tbl 裡編號 80 起是特殊食譜（80–82）；推測：不在一般清單列出。</summary>
    public const int SpecialRecipeFirst=80;
    /// <summary>C_USE_ITEM 料理書第一個參數：1＝製作。</summary>
    public const int MakeMode=1;

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
    /// <summary>推測：等級 level 的料理書列出的食譜（編號 &lt; 8×level，特殊食譜 80 起不列）。</summary>
    public static bool Listed(CookRecipe recipe,int level)=>level>0&&recipe.Index<SpecialRecipeFirst&&recipe.Index<RecipesPerLevel*level;
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

    /// <summary>料理窗要列的食譜（編號順序），名稱與材料經 names 代換 $n。</summary>
    public static IReadOnlyList<CookBookRecipe> Rows(int level,Func<string,string>? names=null,IReadOnlyList<CookRecipe>? recipes=null){
        var source=recipes??Table;
        if(source==null||level<=0)return [];
        string N(string s)=>names==null?s:names(s);
        return source.Where(r=>CookingProtocol.Listed(r,level)).OrderBy(r=>r.Index)
            .Select(r=>new CookBookRecipe(r.Index,N(r.Name),string.Join("、",r.Materials.Select(m=>$"{N(m.Name)} ×{m.Count}")))).ToArray();
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

    [WorldReset]
    void ResetCooking(){lock(cookBookGate)cookBook=new(false,0,0,0,cookBook.Revision+1);}
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
        if(World.CookingBookItem(book) is not {} item){World.ItemNotice="未送出：背包裡找不到開窗的料理書";return Task.FromResult(false);}
        if(State!=LoginState.InWorld||World.Actor is not {} actor){World.ItemNotice="未送出：尚未進入遊戲";return Task.FromResult(false);}
        if(World.SelectedItemId!=item.ObjectId&&!World.SelectItem(item.ObjectId)){World.ItemNotice="未送出：料理書已離開背包";return Task.FromResult(false);}
        var request=new ItemUseRequest(item,generation,World.WorldEpoch,World.MapId,actor.ObjectId,
            World.ItemSelectionRevision,World.InventoryContentRevision,World.Corrections,actor.X,actor.Y);
        PendingItemUse=request;
        return UseItemWithArguments(request,new ItemUseArguments.Cooking(CookingProtocol.MakeMode,recipe));
    }
}

/// <summary>R3-C（#672）：把 WorldSession 的料理窗狀態轉給原生面板（面板不碰 LoginConnection）。R4-D：加食譜清單與製作。</summary>
public sealed class WorldCookBookSource(Func<WorldSession> world,Func<string,string>? names=null,Func<long,int,Task<bool>>? cook=null):ICookBookSource {
    (long Revision,int Level,IReadOnlyList<CookBookRecipe> Rows)? cache;
    public CookBookView? Current{get{
        var b=world().CookBook;if(!b.Open)return null;
        if(cache is not {} c||c.Revision!=b.Revision||c.Level!=b.Level){c=(b.Revision,b.Level,CookRecipeTable.Rows(b.Level,names));cache=c;}
        return new(b.Revision,b.Value,b.Level,c.Rows);
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
