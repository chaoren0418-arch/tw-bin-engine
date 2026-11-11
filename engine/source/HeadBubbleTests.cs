using System.Reflection;
using System.Text.Json;
namespace P06;

// R11-E（T419／#961）頭上對話框的離線測試：FU16§5 B1–B19（前處理、斷行、錨點、矩形、夾邊界、點陣外框、
// 重疊變灰、到時移除、商店訊息 5 秒重建、byte_136E99F 開關、+252 改回真正欄位）。
// 只用合成封包與純函式，不開遊戲、網路、視窗或素材。
static class HeadBubbleTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Shop=789,Shop2=790;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Big5(string s)=>LoginPackets.TextEncoding.GetBytes(s);
    static byte[] Ascii(string s)=>System.Text.Encoding.ASCII.GetBytes(s);
    static byte[] Str(string s)=>[..Big5(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
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
    static string S(int[] a)=>string.Join(",",a);

    [WorldModuleTest("head-bubble")]
    static void Run(WorldModuleTestContext t){
        Bitmaps(t);Prepare(t);Wrap(t);Geometry(t);Clamp(t);Draw(t);Board(t);
        HeadBubbleOptions.SetCached(true);
        try{Shops(t);}finally{HeadBubbleOptions.SetCached(null);}
        Field252(t);Registry(t);
    }

    // FU16§3.5：52 bytes 點陣，SHA-256 與 BIN FO 0xF674EC 相同。
    static void Bitmaps(WorldModuleTestContext t){
        t.Equal(52,HeadBubbleLayout.BitmapData.Length,"點陣 52 bytes");
        t.Equal("9d9ffa30c0e53d1aad2ea9fc163d6fc88aa5b015e573cb615127c16ba003f028",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(HeadBubbleLayout.BitmapData)).ToLowerInvariant(),"點陣 SHA-256（FU16§3.5）");
        string Row(HeadBubbleLayout.Glyph g,int r){var px=HeadBubbleLayout.Pixels(g,0,0).Where(p=>p.Y==r).Select(p=>p.X).ToHashSet();return new string(Enumerable.Range(0,g.Width).Select(x=>px.Contains(x)?'#':'.').ToArray());}
        t.Equal("##...##",Row(HeadBubbleLayout.TailFrame,0),"TF 第 0 列");
        t.Equal("...#...",Row(HeadBubbleLayout.TailFrame,3),"TF 第 3 列（尖端）");
        t.Equal("##...##",Row(HeadBubbleLayout.TailFrameUp,3),"TF↑ 上下翻轉");
        t.Equal("..##",Row(HeadBubbleLayout.CornerTL,0),"TL 圓角");
        t.Equal("...#",Row(HeadBubbleLayout.CornerBR,0),"BR 圓角");
        t.Equal("#######",Row(HeadBubbleLayout.TailFill,0),"TC 色三角");
        t.Equal("#########",Row(HeadBubbleLayout.TailOutline,0),"TO 黑邊三角 9 寬（每列 2 bytes）");
        t.Equal("....#....",Row(HeadBubbleLayout.TailOutline,4),"TO 尖端");
        t.Equal("#########",Row(HeadBubbleLayout.TailOutlineUp,4),"TO↑");
    }

    // B2：sub_7CE6B0／sub_7CF080／sub_7CFE90。
    static void Prepare(WorldModuleTestContext t){
        ushort W=HeadBubbleLayout.White;
        t.Check(HeadBubbleLayout.Prepare(Big5("名字: "),W).SequenceEqual(Big5("名字")),"結尾空白、前一個字是 ':' → 截斷");
        t.Check(HeadBubbleLayout.Prepare(Ascii("a: b"),W).SequenceEqual(Ascii("a: b")),"結尾不是空白：不處理");
        t.Check(HeadBubbleLayout.Prepare(Ascii("ab  "),W).SequenceEqual(Ascii("ab  ")),"結尾空白但前面不是 ':'：不處理");
        t.Check(HeadBubbleLayout.Prepare(Ascii("\\f3abc\\b0d\\i5e"),W).SequenceEqual(Ascii("abcde")),"去掉 \\f? \\b? \\i0-5");
        t.Check(HeadBubbleLayout.Prepare(Ascii("\\i6x\\b!y\\jz"),W).SequenceEqual(Ascii("\\i6x\\b!y\\jz")),"\\i6、\\b 後面 < '0'、\\j 保留");
        t.Check(HeadBubbleLayout.Prepare([0xA2,0xC6,(byte)'x'],W).SequenceEqual(Ascii("  x")),"白色：〤 換成兩個空白");
        t.Check(HeadBubbleLayout.Prepare([0xA2,0xC6,(byte)'x'],HeadBubbleLayout.Shout).SequenceEqual(new byte[]{0xA2,0xC6,(byte)'x'}),"非白色：不換");
        byte[] gong=[0xA5,0x5C,(byte)'f',(byte)'1'];   // 「功」的第二個 byte 是 0x5C
        t.Check(HeadBubbleLayout.Prepare(gong,W).SequenceEqual(gong),"Big5 第二個 byte 0x5C 不當控制碼（推測）");
        t.Equal(768,HeadBubbleLayout.Prepare(Enumerable.Repeat((byte)'a',900).ToArray(),W).Length,"最多 768 bytes");
        t.Check(HeadBubbleLayout.Prepare([(byte)'a',0,(byte)'b'],W).SequenceEqual(Ascii("a")),"遇 0 停");
    }

    // B3–B6：FU16§1.2–1.3。
    static void Wrap(WorldModuleTestContext t){
        t.Equal(30,HeadBubbleLayout.LineLimit(20,false),"一般、泡泡話框關：30");
        t.Equal(24,HeadBubbleLayout.LineLimit(16,false),"一般、泡泡話框開：24");
        t.Equal(25,HeadBubbleLayout.LineLimit(20,true),"商店、關：25");
        t.Equal(20,HeadBubbleLayout.LineLimit(16,true),"商店、開：20");
        t.Equal(20,HeadBubbleLayout.TargetWidth(false),"W0 關＝20");t.Equal(16,HeadBubbleLayout.TargetWidth(true),"W0 開＝16");
        var c=HeadBubbleLayout.Costs(Ascii("ab c"));
        t.Equal("4,3,0,0",S(c),"代價：其他字 4、下一個是空白 3、空白 0、結尾 0");
        c=HeadBubbleLayout.Costs([0xA4,0x40,0xA4,0x40]);
        t.Equal("999,2,999,3",S(c),"代價：雙位元組第一個 999、第二個（下一個是雙位元組）2、結尾 3");
        t.Equal("4,-99,-999",S(HeadBubbleLayout.Costs(Ascii("ab\n"))),"代價：下一個是 \\n → −99；\\n 在結尾 → −999");
        t.Equal("1,0",S(HeadBubbleLayout.Costs(Ascii("  "))[..1].Append(HeadBubbleLayout.Costs(Ascii(" a"))[0]).ToArray()),"空白接空白 1、空白接字 0");
        var a40=HeadBubbleLayout.Wrap(Enumerable.Repeat((byte)'a',40).ToArray(),30,out int w);
        t.Equal("0,30,40",S(a40),"40 個字母、上限 30：同代價取最後一個");t.Equal(30,w,"w＝最長一行");
        var words=HeadBubbleLayout.Wrap(Ascii("hello world foo"),8,out w);
        t.Equal("0,6,12,15",S(words),"在空白後面斷");t.Equal(6,w,"w＝6");
        t.Equal("0,8",S(HeadBubbleLayout.Wrap(Ascii("hi there"),30,out w)),"放得下：一行");t.Equal(8,w,"w＝8");
        var big5=Big5(new string('字',20));var lines=HeadBubbleLayout.Wrap(big5,30,out w);
        t.Equal("0,30,40",S(lines),"20 個中文字、上限 30");t.Check(lines.All(s=>s%2==0),"雙位元組字不切開",S(lines));
        var odd=HeadBubbleLayout.Wrap([(byte)'a',..Big5(new string('字',20))],30,out w);
        t.Check(odd.Skip(1).Take(odd.Length-2).All(s=>s%2==1),"前面多 1 byte 時斷點落在字的邊界",S(odd));
        // 剩下的字 ≤ M：整段當最後一行（第 2 步找到的斷點不用）。
        t.Equal("0,25,45",S(HeadBubbleLayout.Wrap(Enumerable.Repeat((byte)'a',45).ToArray(),25,out w)),"25＋20：最後 20 放得下");
        t.Equal("0",S(HeadBubbleLayout.Wrap([],30,out w)),"空字串：0 行");t.Equal(0,w,"空字串 w＝0");
    }

    // B7–B9：錨點與矩形。
    static void Geometry(WorldModuleTestContext t){
        t.Check(HeadBubbleLayout.HasFrame(false,true,false)&&HeadBubbleLayout.HasFrame(true,false,false)&&HeadBubbleLayout.HasFrame(false,false,true)&&!HeadBubbleLayout.HasFrame(false,false,false),"B7 有外框的條件");
        t.Equal(17,HeadBubbleLayout.Lift(true,true,true,true),"自己＋HPMP：17");
        t.Equal(32,HeadBubbleLayout.Lift(true,false,true,true),"自己但 HPMP 關，全名＋+252：32");
        t.Equal(32,HeadBubbleLayout.Lift(false,true,true,true),"他人、全名＋+252：32");
        t.Equal(12,HeadBubbleLayout.Lift(false,true,true,false),"只開全名模式不算：12（FU16 更正）");
        t.Equal(12,HeadBubbleLayout.Lift(false,true,false,true),"+252 有但全名關：12");
        t.Equal(new Point(324,188),HeadBubbleLayout.Anchor(324,new(300,200,48,64),12),"A＝(P.x＋24, R.Top − 12)");
        var (box,tail)=HeadBubbleLayout.Box(new(100,100),6,1,true);
        t.Equal(Rectangle.FromLTRB(77,74,123,100),box,"有外框：[A.x−3w−5, A.y−12n−14, A.x+3w+5, A.y]");t.Equal(23,tail,"t＝3w＋5");
        (box,tail)=HeadBubbleLayout.Box(new(100,100),6,2,false);
        t.Equal(Rectangle.FromLTRB(82,70,118,100),box,"沒有外框：[A.x−3w, A.y−12n−6, A.x+3w, A.y]");t.Equal(18,tail,"t＝3w");
    }

    // B10：sub_7CE370。
    static void Clamp(WorldModuleTestContext t){
        var (box,tail)=HeadBubbleLayout.Box(new(300,200),6,1,true);
        t.Check(HeadBubbleLayout.Clamp(box,tail,264) is {TailUp:false} g0&&g0.Box==box&&g0.Tail==23,"範圍內：不動");
        (box,tail)=HeadBubbleLayout.Box(new(60,200),6,1,true);
        var g=HeadBubbleLayout.Clamp(box,tail,264);
        t.Check(g is {} &&g.Box.Left==2&&g.Box.Width==46&&g.Tail==58,"L<48：平移讓 L＝2（照原碼），t −= d",g);
        (box,tail)=HeadBubbleLayout.Box(new(870,200),6,1,true);g=HeadBubbleLayout.Clamp(box,tail,264);
        t.Check(g is {} &&g.Box.Right==880&&g.Tail==36,"R>880：平移讓 R＝880",g);
        (box,tail)=HeadBubbleLayout.Box(new(878,200),6,1,true);
        t.Check(HeadBubbleLayout.Clamp(box,tail,264)==null,"R>880 而且 t > 寬 − 9：不畫");
        (box,tail)=HeadBubbleLayout.Box(new(300,50),6,1,true);g=HeadBubbleLayout.Clamp(box,tail,114);
        t.Check(g is {TailUp:true} &&g.Box.Top==114&&g.Box.Height==26,"T<48：上緣移到物件外框下緣、尾巴朝上",g);
        (box,tail)=HeadBubbleLayout.Box(new(300,560),6,1,true);
        t.Check(HeadBubbleLayout.Clamp(box,tail,600)==null,"B>540：不畫");
        // 平移到畫面座標再夾。
        var b=HeadBubbleLayout.Create(1,Ascii("abcdef"),HeadBubbleLayout.White,5,true,false,false,new(1300,1200),1264,0)!;
        t.Check(HeadBubbleLayout.Place(b,new(1000,1000)) is {} p&&p.Box.Left==277&&p.Box.Bottom==200,"Place：場景座標 − 視窗左上角",HeadBubbleLayout.Place(b,new(1000,1000)));
    }

    // B11–B13：sub_7CF290。
    static void Draw(WorldModuleTestContext t){
        var b=HeadBubbleLayout.Create(1,Ascii("abcdef"),HeadBubbleLayout.White,5,true,false,false,new(100,100),164,0)!;
        t.Check(b is {Frame:true,Width:6,Lines:1},"商店：有外框、w＝6、n＝1",b);
        var geo=new HeadBubbleGeometry(Rectangle.FromLTRB(77,74,123,100),23,false);
        var d=HeadBubbleLayout.Draw(b,geo,HeadBubbleLayout.White);
        var px=d.Fills.SelectMany(f=>Enumerable.Range(f.Rect.X,f.Rect.Width).SelectMany(x=>Enumerable.Range(f.Rect.Y,f.Rect.Height).Select(y=>new Point(x,y)))).ToHashSet();
        t.Check(d.Fills.All(f=>f.Color==HeadBubbleLayout.White),"外框與尾巴都用文字色；不填底色");
        t.Check(px.Contains(new(100,99))&&!px.Contains(new(100,100)),"尾巴尖端在 (A.x, A.y−1)");
        t.Check(px.Contains(new(97,96))&&px.Contains(new(103,96))&&!px.Contains(new(100,96)),"尾巴 TF 畫在 (L+t−3, Bm−4)");
        t.Check(d.Fills.Any(f=>f.Rect==new Rectangle(77,79,1,13))&&d.Fills.Any(f=>f.Rect==new Rectangle(122,79,1,13)),"左右直線 (L, top+5, 1, bot−top−9)");
        t.Check(d.Fills.Any(f=>f.Rect==new Rectangle(82,96,15,1))&&d.Fills.Any(f=>f.Rect==new Rectangle(104,96,14,1)),"下邊有缺口 (L+5, t−8) 與 (L+t+4, R−L−t−9)");
        t.Check(d.Fills.Any(f=>f.Rect==new Rectangle(82,74,36,1)),"上邊完整 (L+5, T, R−L−10)");
        t.Check(px.Contains(new(80,75))&&px.Contains(new(78,77))&&px.Contains(new(119,75)),"TL／TR 圓角");
        t.Check(d.Texts.Length==1&&d.Texts[0] is {Text:"abcdef",Color:HeadBubbleLayout.White}&&d.Texts[0].Position==new Point(82,79),"文字從 (L+5, top+5)",d.Texts);
        var up=HeadBubbleLayout.Draw(b,geo with{TailUp=true},HeadBubbleLayout.White);
        var upx=up.Fills.SelectMany(f=>Enumerable.Range(f.Rect.X,f.Rect.Width).SelectMany(x=>Enumerable.Range(f.Rect.Y,f.Rect.Height).Select(y=>new Point(x,y)))).ToHashSet();
        t.Check(upx.Contains(new(100,74))&&up.Fills.Any(f=>f.Rect==new Rectangle(82,78,15,1))&&up.Fills.Any(f=>f.Rect==new Rectangle(82,99,36,1)),"尾巴朝上：上邊 T+4 有缺口、下邊 Bm−1");
        t.Equal(new Point(82,83),up.Texts[0].Position,"尾巴朝上：文字 (L+5, T+9)");
        // 沒有外框：9×5 黑三角＋7×4 色三角，x 都是 L+t−3；文字 (L, T)。
        var plain=HeadBubbleLayout.Create(1,Ascii("abcdef"),HeadBubbleLayout.White,8,false,false,false,new(100,100),164,0)!;
        var pg=new HeadBubbleGeometry(Rectangle.FromLTRB(82,82,118,100),18,false);var pd=HeadBubbleLayout.Draw(plain,pg,HeadBubbleLayout.White);
        t.Check(!plain.Frame&&pd.Fills.Count(f=>f.Color==HeadBubbleLayout.Black)==25&&pd.Fills.Count(f=>f.Color==HeadBubbleLayout.White)==16,"沒有外框：黑三角 25 點、色三角 16 點、沒有框線",pd.Fills.Length);
        t.Check(pd.Fills.Any(f=>f.Color==HeadBubbleLayout.Black&&f.Rect.Location==new Point(97,95))&&pd.Fills.Any(f=>f.Color==HeadBubbleLayout.White&&f.Rect.Location==new Point(97,96)),"黑三角 (L+t−3, Bm−5)、色三角 (L+t−3, Bm−4)");
        t.Equal(new Point(82,82),pd.Texts[0].Position,"沒有外框：文字 (L, T)");
        var two=HeadBubbleLayout.Create(1,Enumerable.Repeat((byte)'a',40).ToArray(),HeadBubbleLayout.White,8,false,true,false,new(300,300),364,0)!;
        var tg=HeadBubbleLayout.Place(two,new(0,0))!;var td=HeadBubbleLayout.Draw(two,tg,HeadBubbleLayout.White);
        t.Check(td.Texts.Length==2&&td.Texts[1].Position.Y-td.Texts[0].Position.Y==12,"每行 +12",td.Texts);
    }

    // B1、B15、B16。
    static void Board(WorldModuleTestContext t){
        var board=new HeadBubbleBoard();Point o=new(0,0);
        HeadBubble Make(uint id,int x,long now)=>HeadBubbleLayout.Create(id,Ascii("hello"),HeadBubbleLayout.White,8,false,false,false,new(x,300),364,now)!;
        var a=Make(1,300,0);t.Check(board.Add(a,b=>HeadBubbleLayout.Place(b,o)),"加入");
        var a2=Make(1,500,100);board.Add(a2,b=>HeadBubbleLayout.Place(b,o));
        t.Check(board.Bubbles.Count==1&&ReferenceEquals(board.Bubbles[0],a2),"B1：同一物件只留新的");
        var near=Make(2,510,200);board.Add(near,b=>HeadBubbleLayout.Place(b,o));
        t.Equal(HeadBubbleLayout.Gray,board.ColorOf(a2),"B16：被新框蓋到的舊框變灰");
        t.Equal(HeadBubbleLayout.White,board.ColorOf(near),"新框本身維持原色");
        var far=Make(3,800,300);board.Add(far,b=>HeadBubbleLayout.Place(b,o));
        t.Equal(HeadBubbleLayout.White,board.ColorOf(near),"沒相交：不變灰");
        var off=HeadBubbleLayout.Create(4,Ascii("x"),HeadBubbleLayout.White,8,false,false,false,new(300,600),664,0)!;
        t.Check(!board.Add(off,b=>HeadBubbleLayout.Place(b,o))&&board.Bubbles.All(b=>b.ObjectId!=4),"夾邊界不過：不加入");
        board.Expire(8099);t.Check(board.Bubbles.Count==3,"8 秒還沒到",board.Bubbles.Count);
        board.Expire(8100);t.Check(board.Bubbles.Count==2&&board.Bubbles.All(b=>b.ObjectId!=1),"B15：到時間直接移除（8 秒）");
        board.RemoveObject(2);t.Check(board.Bubbles.Single().ObjectId==3,"移除物件的框");
    }

    // sub_7CFFC0：商店訊息 5 秒重建、a4＝5、一律有外框、不受全名模式影響；byte_136E99F 關閉就不畫。
    static void Shops(WorldModuleTestContext t){
        var w=Seed();
        w.Receive(Appear(Shop,32602,"商人",1110),"offline");w.Receive(ShopAction(Shop,[..Big5("賣藥水"),0xFF,..Big5("便宜賣")]),"offline");
        var o=w.Objects.Single(x=>x.ObjectId==Shop);var board=new HeadBubbleBoard();
        var targets=new[]{new HeadBubbleTarget(Shop,new(300,200,48,64),324,false)};
        HeadBubbleShops.Sync(board,w,targets,false,true,o.ShopAt+10,new(0,0));
        var b=board.Bubbles.Single();
        t.Check(b is {Shop:true,Frame:true,Color:HeadBubbleLayout.White}&&b.CreatedAt==o.ShopAt&&b.ExpiresAt==o.ShopAt+5000,"商店框：有外框、白色、5 秒",b);
        t.Equal(new Point(324,188),b.Anchor,"錨點：R.Top − 12");
        t.Equal("賣藥水",PersonalShopTitle.Text(b.Text),"第一輪：第一行");
        HeadBubbleShops.Sync(board,w,targets,true,true,o.ShopAt+20,new(0,0));
        t.Check(ReferenceEquals(board.Bubbles.Single(),b),"同一輪不重建（全名模式切換也不重建）");
        HeadBubbleShops.Sync(board,w,targets,true,true,o.ShopAt+5000,new(0,0));
        var b2=board.Bubbles.Single();
        t.Check(!ReferenceEquals(b2,b)&&PersonalShopTitle.Text(b2.Text)=="便宜賣"&&b2.CreatedAt==o.ShopAt+5000,"5 秒後：重建成第二行",b2);
        t.Equal(new Point(324,188),b2.Anchor,"全名模式但 +252 空：仍是 −12");
        w.SetField252(Shop,"x");
        HeadBubbleShops.Sync(board,w,targets,true,true,o.ShopAt+10000,new(0,0));
        t.Equal(new Point(324,168),board.Bubbles.Single().Anchor,"全名模式而且 +252 不空：−32");
        w.SetField252(Shop,"");
        // 長訊息：商店一行上限 25 byte。
        w.Receive(ShopAction(Shop,Big5(new string('a',40))),"offline");o=w.Objects.Single(x=>x.ObjectId==Shop);
        HeadBubbleShops.Sync(board,w,targets,false,true,o.ShopAt,new(0,0));
        t.Check(board.Bubbles.Single() is {Width:25,Lines:2},"商店訊息：M＝W0＋W0/4＝25",board.Bubbles.Single());
        // 開關關閉：移除、不畫。
        HeadBubbleOptions.SetCached(false);
        HeadBubbleShops.Sync(board,w,targets,false,true,o.ShopAt+1,new(0,0));
        t.Equal(0,board.Bubbles.Count,"個人商店名稱關閉：不畫");
        HeadBubbleOptions.SetCached(true);
        HeadBubbleShops.Sync(board,w,targets,false,true,o.ShopAt+2,new(0,0));
        t.Equal(1,board.Bubbles.Count,"再打開：照畫");
        HeadBubbleShops.Sync(board,w,[],false,true,o.ShopAt+3,new(0,0));
        t.Equal(0,board.Bubbles.Count,"看不到：移除");
        HeadBubbleShops.Sync(board,w,targets,false,true,o.ShopAt+4,new(0,0));
        w.Receive(P([158],Le32(Shop),[0]),"offline");
        HeadBubbleShops.Sync(board,w,targets,false,true,o.ShopAt+5,new(0,0));
        t.Equal(0,board.Bubbles.Count,"收攤：移除");
        // 兩家相鄰的店：後建的蓋到先建的，先建的變灰。
        w.Receive(ShopAction(Shop,Big5("一號店")),"offline");
        w.Receive(Appear(Shop2,32603,"商人二",1110),"offline");w.Receive(ShopAction(Shop2,Big5("二號店")),"offline");
        var o1=w.Objects.Single(x=>x.ObjectId==Shop);var o2=w.Objects.Single(x=>x.ObjectId==Shop2);
        var pair=new[]{targets[0],new HeadBubbleTarget(Shop2,new(310,200,48,64),334,false)};
        var latest=Math.Max(o1.ShopAt,o2.ShopAt);
        HeadBubbleShops.Sync(board,w,pair,false,true,latest,new(0,0));
        t.Equal(2,board.Bubbles.Count,"兩個框");
        t.Equal(HeadBubbleLayout.Gray,board.ColorOf(board.Bubbles[0]),"先加入的被後加入的蓋到：灰");
        // 自己不畫（引擎的自己沒有商店訊息欄位）。
        HeadBubbleShops.Sync(board,w,[new(Me,new(300,200,48,64),324,true)],false,true,latest,new(0,0));
        t.Equal(0,board.Bubbles.Count,"自己：不畫");
    }

    // +252 改回真正的欄位：正式流程沒有來源，空字串；寫入後才會出現全名模式的 +252 行。
    static void Field252(WorldModuleTestContext t){
        var w=Seed();
        w.Receive(Appear(Shop,32602,"商人",1110),"offline");w.Receive(ShopAction(Shop,Big5("賣藥水")),"offline");
        t.Equal("",w.NameMarksFor(Shop).Line252,"商店訊息不進 +252");
        var targets=new[]{new NameTarget(Shop,"商人",new(200,200,48,64),false)};
        t.Check(!NameLayout.Plan(w,targets,null,true).Texts.Any(x=>x.Line252),"全名模式：沒有 +252 行");
        w.SetField252(Shop,"欄位");
        t.Check(NameLayout.Plan(w,targets,null,true).Texts.Single(x=>x.Line252).Text=="欄位","寫入 +252 後畫 +252 行");
        w.Reset();
        t.Equal("",w.Field252Of(Shop),"世界重設清掉 +252");
    }

    static void Registry(WorldModuleTestContext t){
        t.Check(WorldExtensionRegistry.Default.Entries.Any(e=>e is {Kind:"reset",Method:"WorldSession.ResetField252"}),"+252 重設自動登錄");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
    }
}
