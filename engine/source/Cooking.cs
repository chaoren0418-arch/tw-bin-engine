namespace P06;

// W2-F（T237／#559）：料理窗（CCookWindow，版面 uiextra86.xml COOK_BOOK_WINDOW）的資料模型；畫面等 T190。
// 依據 docs/reference/windows-2.md 第 7 節（T226／#533）與 docs/reference/server-protocol.md S_PacketBoxCooking（#132）：
//   3.81 伺服器：S250 C(52) C(0xdb) C(0x31) C(0xdf) C(0x02) C(1) C(value)
//   TW16 原版（S_EVENT 子碼 52）讀 D＋C＋C；前 4 byte 正好是 D＝0x02DF31DB，第二欄為 1 時開／切換料理窗，第三欄＋1 是料理書等級。
//   伺服器 Cooking_Book：操作 0（開啟料理書）時送 value＝料理書道具編號 − 41255；操作 1（製作）不送本封包。
// 已驗證（文件）：欄位順序與長度、第二欄 1＝開窗、等級＝value＋1。
// 推定／待驗證：第二欄不是 1 時原版不動作（本模組只記錄、不開窗）；已開著時再收到 1 是「切換到新等級」而非關窗；
//   第一欄 D 原版不檢查（本模組照收、只記錄是否等於 3.81 常數）；等級圖 2545–2555 依 value 取第幾張。
// 版面常數只記錄位置供 T190 使用，不讀取任何遊戲素材。

/// <summary>料理窗目前狀態。Level＝value＋1（料理書等級）；Revision 每次變動遞增。</summary>
public sealed record CookBookState(bool Open,byte Value,int Level,uint Magic,long Revision);

public static class CookingProtocol {
    public const byte SPacketBox=250,CookWindow=52;
    /// <summary>3.81 伺服器固定送的第一欄（小端 db 31 df 02）。</summary>
    public const uint ServerMagic=0x02DF31DB;
    /// <summary>伺服器 Cooking_Book：value＝料理書道具編號 − 41255。</summary>
    public const int BookItemBase=41255;
    // uiextra86.xml COOK_BOOK_WINDOW（windows-2.md 第 7 節）：底圖、捲軸、等級圖（11 張）與位置、量表與位置。
    public const int BackgroundSurf=2254,ScrollSurf=2039,LevelSurfFirst=2545,LevelSurfLast=2555,GaugeSurf=2256;
    public static readonly (int X,int Y) LevelPosition=(116,257),GaugePosition=(35,251);

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
}

public sealed partial class WorldSession {
    CookBookState cookBook=new(false,0,0,0,0);
    public CookBookState CookBook=>cookBook;

    [WorldReceive(CookingProtocol.SPacketBox,Subtype=CookingProtocol.CookWindow)]
    bool ReceiveCookWindow(byte[] plain){
        var (magic,open,value)=CookingProtocol.Decode(plain);
        // 推定：第二欄不是 1 時原版不開窗；本模組只記錄 magic，不改開關與等級。
        if(open!=1){cookBook=cookBook with{Magic=magic,Revision=cookBook.Revision+1};return true;}
        cookBook=new(true,value,CookingProtocol.Level(value),magic,cookBook.Revision+1);
        return true;
    }
    /// <summary>本機關閉料理窗（不送封包；原版關窗是本機動作）。已關閉時回 false。</summary>
    public bool CloseCookBook(){
        if(!cookBook.Open)return false;
        cookBook=cookBook with{Open=false,Revision=cookBook.Revision+1};return true;
    }

    [WorldReset]
    void ResetCooking(){cookBook=new(false,0,0,0,cookBook.Revision+1);}
    [WorldSnapshot("cooking")]
    object? CookingSnapshot()=>new{open=cookBook.Open,value=cookBook.Value,level=cookBook.Level,
        bookItemId=cookBook.Level>0?CookingProtocol.BookItemId(cookBook.Value):0,
        levelSurf=cookBook.Level>0?CookingProtocol.LevelSurf(cookBook.Value):null,
        magic=$"0x{cookBook.Magic:X8}",magicMatches381=cookBook.Magic==CookingProtocol.ServerMagic,revision=cookBook.Revision};
}
