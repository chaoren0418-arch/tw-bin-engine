using System.Buffers.Binary;
namespace P06;

// T406（#923，FU13§2 D1–D2）選目標瞄準游標的離線測試：游標索引規則、BIN 游標表、PE 資源解析（合成的最小 PE，不讀遊戲素材）。
// 本機有原版 TW1608262200.bin 時多核對一次真資源的熱點（CI 沒有會略過，不輸出圖）。不開遊戲、網路、視窗。
// 由 --module-tests 自動發現執行（名稱 spell-cursor）。
static class SpellCursorTests {
    [WorldModuleTest("spell-cursor")]
    static void Run(WorldModuleTestContext t){
        // sub_F044C0 選目標分支（FU13§2.1 第 4 點）與 sub_F68E60 → 3。
        t.Equal(GameCursors.Hand,SpellCursorRules.Index(false,true),"不在選目標模式：手形 3");
        t.Equal(3,GameCursors.Hand,"手形索引 3");
        t.Equal(8,SpellCursorRules.Index(true,false),"非技能回呼：8");
        t.Equal(15,SpellCursorRules.Index(true,false,alternate:true),"非技能回呼＋byte_139FBAF：15");
        t.Equal(8,SpellCursorRules.Index(true,true),"技能、滑鼠下沒有對象：8（推測）");
        t.Equal(8,SpellCursorRules.Index(true,true,targetInRange:true),"技能、對象在範圍內：8");
        t.Equal(19,SpellCursorRules.Index(true,true,targetInRange:false),"技能、對象不在範圍內：19");
        t.Equal(20,SpellCursorRules.Index(true,true,rangeKnown:false,targetInRange:true),"sub_E23BF0(0) 為假：20");
        t.Equal(15,SpellCursorRules.Index(true,true,targetInRange:true,alternate:true),"alternate 在範圍內：15");
        t.Equal(23,SpellCursorRules.Index(true,true,targetInRange:false,alternate:true),"alternate 不在範圍內：23");
        t.Equal(24,SpellCursorRules.Index(true,true,rangeKnown:false,alternate:true),"alternate sub_E23BF0 假：24");

        // lpCursorName／dword_1372180（FU13§2.1 表，pefile 重讀一致）。
        t.Equal(36,GameCursors.MonochromeIds.Length,"單色組 36 筆");t.Equal(36,GameCursors.ColorIds.Length,"彩色組 36 筆");
        t.Check(GameCursors.ResourceId(3,false)==105&&GameCursors.ResourceId(3,true)==202,"3：105／202");
        t.Check(GameCursors.ResourceId(8,false)==136&&GameCursors.ResourceId(8,true)==208,"8：136／208");
        t.Check(GameCursors.ResourceId(15,false)==145&&GameCursors.ResourceId(15,true)==216,"15：145／216");
        t.Check(GameCursors.ResourceId(19,false)==220&&GameCursors.ResourceId(19,true)==223,"19：220／223");
        t.Check(GameCursors.ResourceId(20,false)==227&&GameCursors.ResourceId(20,true)==229,"20：227／229");
        t.Check(GameCursors.ResourceId(23,false)==233&&GameCursors.ResourceId(24,false)==234&&GameCursors.ResourceId(23,true)==231&&GameCursors.ResourceId(24,true)==232,"23／24：233、234／231、232");
        t.Check(GameCursors.ResourceId(-1,true)==0&&GameCursors.ResourceId(36,false)==0,"超出表：0");

        // 合成最小 PE：RT_GROUP_CURSOR 208 → RT_CURSOR 7，熱點 (13,6)、32×64、24 bit。
        var pe=SyntheticPe(out var cursorBytes);
        var image=PeCursorResources.Read(pe,208);
        t.Check(image is {GroupId:208,CursorId:7,Width:32,Height:64,BitCount:24,HotX:13,HotY:6},"讀出群組 208 → 游標 7、熱點 (13,6)",image);
        t.Check(image!=null&&image.Bytes.AsSpan().SequenceEqual(cursorBytes),"位元組是完整 RT_CURSOR（熱點＋DIB）");
        t.Check(PeCursorResources.Read(pe,136)==null,"沒有的群組：null");
        t.Check(PeCursorResources.Read([],208)==null,"空檔：null");
        t.Check(PeCursorResources.Read(pe[..0x300],208)==null,"截斷（沒有資源節）：null");
        var notPe=(byte[])pe.Clone();notPe[0x40]=(byte)'X';t.Check(PeCursorResources.Read(notPe,208)==null,"不是 PE 簽章：null");
        var noRes=(byte[])pe.Clone();BinaryPrimitives.WriteUInt32LittleEndian(noRes.AsSpan(0x58+96+16),0);t.Check(PeCursorResources.Read(noRes,208)==null,"沒有資源目錄：null");
        var badGroup=(byte[])pe.Clone();BinaryPrimitives.WriteUInt16LittleEndian(badGroup.AsSpan(0x400+224+2),1);t.Check(PeCursorResources.Read(badGroup,208)==null,"群組 type 不是 2（游標）：null");

        // 本機有原版 BIN 才核對（CI 略過）。
        string bin=GameCursorSource.BinPath;
        if(File.Exists(bin)){
            var bytes=File.ReadAllBytes(bin);
            if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).Equals(GameCursorSource.KnownSha256,StringComparison.OrdinalIgnoreCase)){
                var aim=PeCursorResources.Read(bytes,208);var hand=PeCursorResources.Read(bytes,105);var far=PeCursorResources.Read(bytes,223);
                t.Check(aim is {CursorId:25,HotX:13,HotY:6,BitCount:24},"BIN 208：游標 25、熱點 (13,6)",aim==null?null:new{aim.CursorId,aim.HotX,aim.HotY});
                t.Check(hand is {CursorId:2,HotX:1,HotY:0,BitCount:1},"BIN 105：游標 2、熱點 (1,0)",hand==null?null:new{hand.CursorId,hand.HotX,hand.HotY});
                t.Check(far is {CursorId:40,HotX:13,HotY:6},"BIN 223：游標 40、熱點 (13,6)");
            }else Console.WriteLine("INFO spell-cursor: BIN SHA-256 與已知不同，略過真資源核對");
        }else Console.WriteLine("INFO spell-cursor: 沒有原版 BIN，略過真資源核對");
    }

    static void W16(byte[] a,int at,int v)=>BinaryPrimitives.WriteUInt16LittleEndian(a.AsSpan(at,2),(ushort)v);
    static void W32(byte[] a,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(a.AsSpan(at,4),v);
    // 版面（檔案位移）：MZ 0、PE 0x40、選用標頭 0x58（224 bytes）、節區表 0x138、.rsrc raw 0x400／RVA 0x1000（256 bytes）。
    static byte[] SyntheticPe(out byte[] cursorBytes){
        const int R=0x400;const uint Sub=0x80000000;
        var b=new byte[R+256];
        b[0]=(byte)'M';b[1]=(byte)'Z';W32(b,0x3C,0x40);
        W32(b,0x40,0x4550);W16(b,0x44,0x14C);W16(b,0x46,1);W16(b,0x54,224);
        const int opt=0x58;W16(b,opt,0x10B);
        W32(b,opt+96+16,0x1000);W32(b,opt+96+20,256);
        const int sec=opt+224;".rsrc"u8.CopyTo(b.AsSpan(sec));
        W32(b,sec+8,256);W32(b,sec+12,0x1000);W32(b,sec+16,256);W32(b,sec+20,R);
        void Dir(int at,params (uint id,uint off)[] entries){W16(b,R+at+14,entries.Length);for(int i=0;i<entries.Length;i++){W32(b,R+at+16+8*i,entries[i].id);W32(b,R+at+20+8*i,entries[i].off);}}
        void Leaf(int at,int dataAt,int size){W32(b,R+at,(uint)(0x1000+dataAt));W32(b,R+at+4,(uint)size);}
        Dir(0,(1,Sub|32),(12,Sub|80));
        Dir(32,(7,Sub|56));Dir(56,(1033,128));
        Dir(80,(208,Sub|104));Dir(104,(1033,144));
        // RT_CURSOR：熱點 (13,6)＋BITMAPINFOHEADER（40, 32, 64, 1, 24）＋16 bytes 像素。
        const int cur=160,curLen=4+40+16;
        W16(b,R+cur,13);W16(b,R+cur+2,6);W32(b,R+cur+4,40);W32(b,R+cur+8,32);W32(b,R+cur+12,64);W16(b,R+cur+16,1);W16(b,R+cur+18,24);
        for(int i=0;i<16;i++)b[R+cur+44+i]=(byte)(i*7+1);
        Leaf(128,cur,curLen);
        // RT_GROUP_CURSOR：reserved 0、type 2、count 1；項目 32、64、planes 1、bits 24、大小、id 7。
        const int grp=224;W16(b,R+grp+2,2);W16(b,R+grp+4,1);
        W16(b,R+grp+6,32);W16(b,R+grp+8,64);W16(b,R+grp+10,1);W16(b,R+grp+12,24);W32(b,R+grp+14,curLen);W16(b,R+grp+18,7);
        Leaf(144,grp,20);
        cursorBytes=b.AsSpan(R+cur,curLen).ToArray();
        return b;
    }}
