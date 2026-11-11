using System.Reflection;
using System.Text.Json;
namespace P06;

// R10-Q（T401／#916）個人商店頭上訊息的離線測試：TW16 sub_A4EF80 的切行（0xFF、第 40 byte 強制換行、80 byte 上限、
// 只存兩行）、sub_7CFFC0 的 5000 ms 輪流，以及從合成封包（S87＋S158 動作 70）走到 NameLayout.Plan 的全名模式。
// 只用合成封包，不開遊戲、網路、視窗或素材。
static class PrivateShopTitleTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Shop=789;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Big5(string s)=>LoginPackets.TextEncoding.GetBytes(s);
    static byte[] Str(string s)=>[..Big5(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87 欄位同 ObjectDrawTests.Appear。
    static byte[] Appear(uint id,ushort x,string name,ushort gfx)=>
        P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,40,0,255,255]);
    static byte[] ShopAction(uint id,byte[] message)=>P([158],Le32(id),[70],message,[0]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static WorldSession Seed(){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",1);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":30,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        w.Receive(Appear(Me,32600,"offline",61),"offline");
        return w;
    }
    static string Hex(byte[] b)=>Convert.ToHexString(b);

    [WorldModuleTest("private-shop-title")]
    static void Run(WorldModuleTestContext t){
        Split(t);Rotation(t);Packets(t);Registry(t);
    }

    // TW16 sub_A4EF80：逐 byte 放進 +98／+139 兩行。
    static void Split(WorldModuleTestContext t){
        var (a,b)=PersonalShopTitle.Split([..Big5("賣藥水"),0xFF,..Big5("便宜")]);
        t.Check(a.SequenceEqual(Big5("賣藥水"))&&b.SequenceEqual(Big5("便宜")),"0xFF 切成兩行",Hex(a)+"|"+Hex(b));
        (a,b)=PersonalShopTitle.Split(Big5("只有一行"));
        t.Check(a.SequenceEqual(Big5("只有一行"))&&b.Length==0,"沒有 0xFF：只有第一行",Hex(a)+"|"+Hex(b));
        var ascii=Enumerable.Range(0,45).Select(i=>(byte)('A'+i%26)).ToArray();
        (a,b)=PersonalShopTitle.Split(ascii);
        t.Check(a.SequenceEqual(ascii[..40])&&b.SequenceEqual(ascii[40..]),"第一行滿 40 byte 強制換行，第 41 個 byte 當第二行開頭",Hex(a)+"|"+Hex(b));
        byte[] at40=[..ascii[..40],0xFF,(byte)'x',0xFF,(byte)'y'];
        (a,b)=PersonalShopTitle.Split(at40);
        t.Check(a.SequenceEqual(ascii[..40])&&b.SequenceEqual(new byte[]{0xFF,(byte)'x'}),"第 41 個 byte 是 0xFF 也照樣放進第二行；下一個 0xFF 結束第二行",Hex(a)+"|"+Hex(b));
        (a,b)=PersonalShopTitle.Split([(byte)'a',0xFF,(byte)'b',0xFF,(byte)'c']);
        t.Check(a.SequenceEqual(new[]{(byte)'a'})&&b.SequenceEqual(new[]{(byte)'b'}),"第三行以後不存（TW16 只存兩行）",Hex(a)+"|"+Hex(b));
        byte[] early=[(byte)'a',0xFF,..Enumerable.Repeat((byte)'z',100)];
        (a,b)=PersonalShopTitle.Split(early);
        t.Check(a.SequenceEqual(new[]{(byte)'a'})&&b.Length==78&&b.All(x=>x==(byte)'z'),"最多複製 80 byte；第一行先遇到 0xFF 時第二行不受 40 byte 限制",b.Length);
        (a,b)=PersonalShopTitle.Split([(byte)'a',0,(byte)'b']);
        t.Check(a.SequenceEqual(new[]{(byte)'a'})&&b.Length==0,"遇 0 停");
        (a,b)=PersonalShopTitle.Split([0xFF,..Big5("第二行")]);
        t.Check(a.Length==0&&b.SequenceEqual(Big5("第二行")),"開頭就是 0xFF：第一行空白",Hex(a)+"|"+Hex(b));
        // 引擎自己送的格式（PersonalShopPackets.Chat：訊息1 0xFF 訊息2 0x00）切回來要相同。
        var chat=PersonalShopPackets.Chat("收購鐵塊","每個 50");
        (a,b)=PersonalShopTitle.Split(chat);
        t.Check(PersonalShopTitle.Text(a)=="收購鐵塊"&&PersonalShopTitle.Text(b)=="每個 50","Chat() 的輸出切回兩行");
    }

    // TW16 sub_7CFFC0：+97 每 5000 ms 反轉；第二行不空且輪到才顯示第二行；第一行空白就不顯示。
    static void Rotation(WorldModuleTestContext t){
        t.Equal(5000,PersonalShopTitle.RotateMs,"5000 ms（sub_A231C0(5000, sub_7CFFC0)）");
        t.Equal("一",PersonalShopTitle.Shown("一","二",0),"第一輪：第一行");
        t.Equal("一",PersonalShopTitle.Shown("一","二",4999),"4999 ms 仍是第一行");
        t.Equal("二",PersonalShopTitle.Shown("一","二",5000),"5000 ms：第二行");
        t.Equal("二",PersonalShopTitle.Shown("一","二",9999),"9999 ms 仍是第二行");
        t.Equal("一",PersonalShopTitle.Shown("一","二",10000),"10000 ms：回到第一行");
        t.Equal("一",PersonalShopTitle.Shown("一","",5000),"只有一行：一直顯示那一行");
        t.Equal("",PersonalShopTitle.Shown("","二",5000),"第一行空白：不顯示（原版不排下一次）");
        t.Equal("一",PersonalShopTitle.Shown("一","二",-20),"時間倒退當 0");
    }

    // 合成封包：S158 動作 70 帶 0xFF 的訊息 → 頭上訊息依時間輪流；全名模式的 +252 行跟著換。
    static void Packets(WorldModuleTestContext t){
        var w=Seed();
        w.Receive(Appear(Shop,32602,"商人",1110),"offline");
        w.Receive(ShopAction(Shop,[..Big5("賣藥水"),0xFF,..Big5("便宜賣")]),"offline");
        var o=w.Objects.Single(x=>x.ObjectId==Shop);
        t.Check(o.ShopActive,"S158 70（含 0xFF）仍進入擺攤狀態",o);
        // R11-E（#961）：商店訊息不再是 +252 行，改走頭上對話框（HeadBubbleTests 另外測版面）。
        string Shown(long at)=>w.ShopTitleLine(w.Objects.Single(x=>x.ObjectId==Shop),at);
        t.Equal("賣藥水",Shown(o.ShopAt),"開店時：第一行");
        t.Equal("便宜賣",Shown(o.ShopAt+5000),"5 秒後：第二行");
        t.Equal("賣藥水",Shown(o.ShopAt+10000),"10 秒後：回到第一行");
        t.Equal("",w.NameMarksFor(Shop,o.ShopAt).Line252,"商店訊息不是 +252（#916）");
        var targets=new[]{new NameTarget(Shop,"商人",new(200,200,48,64),false)};
        t.Check(!NameLayout.Plan(w,targets,null,true,o.ShopAt+1).Texts.Any(x=>x.Line252),"全名模式不再畫商店訊息");
        var board=new HeadBubbleBoard();var bubbleTargets=new[]{new HeadBubbleTarget(Shop,new(300,200,48,64),324,false)};
        string Bubble(long at){HeadBubbleShops.Sync(board,w,bubbleTargets,true,true,at,new(0,0));return board.Bubbles.Count==1?PersonalShopTitle.Text(board.Bubbles[0].Text):"";}
        HeadBubbleOptions.SetCached(true);
        try{
            t.Equal("賣藥水",Bubble(o.ShopAt+1),"頭上對話框：第一行");
            t.Equal("便宜賣",Bubble(o.ShopAt+5001),"頭上對話框：5 秒後換第二行");
        }finally{HeadBubbleOptions.SetCached(null);}
        // 沒有 0xFF、超過 40 byte：第 41 byte 起是第二行。
        var longText=new string('a',40)+"bcd";
        w.Receive(ShopAction(Shop,Big5(longText)),"offline");o=w.Objects.Single(x=>x.ObjectId==Shop);
        t.Equal(new string('a',40),Shown(o.ShopAt),"滿 40 byte：第一行");
        t.Equal("bcd",Shown(o.ShopAt+5000),"滿 40 byte：第二行");
        // 收攤（S158 其他動作）→ 沒有訊息。
        w.Receive(P([158],Le32(Shop),[0]),"offline");
        t.Equal("",Shown(Environment.TickCount64),"收攤後不顯示");
        // 世界重設清掉原始 byte。
        var raw=(System.Collections.ICollection)typeof(WorldSession).GetField("shopTitleRaw",F)!.GetValue(w)!;
        t.Check(raw.Count==1,"觀察器存了原始 byte",raw.Count);
        w.Reset();
        t.Check(raw.Count==0,"世界重設清空原始 byte",raw.Count);
    }

    static void Registry(WorldModuleTestContext t){
        t.Check(WorldExtensionRegistry.Default.Entries.Any(e=>e is {Kind:"observe",Opcode:158,Subtype:-1,Method:"WorldSession.ObserveShopTitle"}),"S158 觀察器自動登錄（不改 WorldSession.cs）");
        t.Check(WorldExtensionRegistry.Default.Entries.Any(e=>e is {Kind:"reset",Method:"WorldSession.ResetShopTitles"}),"重設自動登錄");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
    }
}
