using System.Reflection;
using System.Text.Json;
namespace P06;

// R10-R（T405／#922）離線測試：list.spr 附加層接線（FU14 §1.3 altdir、§2.4 stride、§3.6 cloth、動作 71 釣魚）。
// 由 --module-tests 執行（名稱 listspr-wiring）。只用合成 list.spr 列、合成影格與合成封包；
// 不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。數字照 FU14§6：1395 朝向 6→1410、1062 連走三步 0→4→0、
// gfx 0 子檔 304 的 6262 有圖而子檔 0 是空格、缺一層子檔時整組不畫。
static class ListSprWiringTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Drake=456,Plain=789;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){
        for(var t=o.GetType();t!=null;t=t.BaseType){
            if(t.GetProperty(name,F|BindingFlags.DeclaredOnly) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
            if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f){f.SetValue(o,value);return;}
            if(t.GetField($"<{name}>k__BackingField",F|BindingFlags.DeclaredOnly) is {} b){b.SetValue(o,value);return;}
        }
        throw new MissingMemberException(o.GetType().Name,name);
    }
    // S87（欄位同 AmbienceWiringTests.Appear）；action 8＝死亡。
    static byte[] Appear(uint id,ushort x,ushort gfx,byte action=0)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[action,0,0,0],Le32(0),Le16(0),
        Str(id==Me?"offline":"target"+id),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static byte[] Move(uint id,ushort x,ushort y,byte h)=>P([10],Le32(id),Le16(x),Le16(y),[h,128]);

    // 照本機 list.spr 的寫法（FU14§1.2、§2.3、§3.3）。
    static readonly Dictionary<int,string> Rows=new(){
        [1395]="#1395 1 a l chair a\n102.type(0) 101.shadow(1396) 112.altdir(1410)\n",
        [5940]="#5940 1=1395 a l chair a\n101.shadow(1396) 112.altdir(1410) 107.size(1 2)\n",
        [1062]="#1062 8 drake\n0.walkfastI(1 1,0.0:4)\n4.walkfastII(1 1,8.0:4)\n111.stride(1)\n",
        [1063]="#1063 8 plain\n0.walk(1 1,0.0:4)\n",
        // 標頭照本機 list.spr：`#0<tab>312=3225<tab>prince`（本體子檔是 3225-N.spr）；動作名稱可以有空白。
        [0]="#0\t312=3225\tprince\n\t0.walk(1 4,0.0:4<479 0.1:4 0.2:4<478 0.3:4)\n\t4.walk onehandsword(1 4,104.0:4<479 104.1:4 104.2:4<478 104.3:4)\n\t71.fishing(1 3,304.0:4 304.1:4 304.2:4)\n\t101.shadow(3226)\n\t104.attr(0)\n\t105.cloth(1 6262)\n",
        [6262]="#6262 8 prince fishing line\n104.attr(8)\n",
    };
    static ListSprFields? Fields(int gfx)=>Rows.TryGetValue(gfx,out var r)?ListSprFields.Parse(r):null;
    static Frame Tiles(string key,int count)=>new(key,[0,0,23,23],Enumerable.Range(0,count).Select(i=>new Block(i,0,0,i,new Tile(2,[new Pixel(1,1,0x1234)]))).ToArray());
    // 6262：子檔 0 是空格（0 個圖塊），子檔 304 有圖；其他子檔不存在。201 只有子檔 0。
    static Frame[]? Sprite(int gfx,int sid)=>(gfx,sid) switch{
        (6262,0)=>[Tiles("0:0",0),Tiles("0:1",0)],
        (6262,304)=>[Tiles("304:0",3),Tiles("304:1",2)],
        (201,0)=>[Tiles("0:0",1)],
        (3225,307)=>[Tiles("307:0",4),Tiles("307:1",4),Tiles("307:2",4)],
        _=>null};
    sealed class Source:IListSprSource {
        public string? Row(int gfx)=>Rows.GetValueOrDefault(gfx);
        public Frame[]? Sprite(int gfx,int sid)=>ListSprWiringTests.Sprite(gfx,sid);
    }

    [WorldModuleTest("listspr-wiring")]
    static void Run(WorldModuleTestContext t){AltDir(t);Stride(t);OthersStride(t);OwnStride(t);Cloth(t);Fishing(t);Runtime(t);}

    static void AltDir(WorldModuleTestContext t){
        var chair=Fields(1395);string row=Rows[1395];
        t.Check(ListSprLayers.StaticShape(row,0),"1395 沒有動作表＝靜態外型");
        t.Equal(1410,ListSprLayers.AltDirGfx(1395,6,0,row,chair),"1395 朝向 6 → 1410");
        t.Check(Enumerable.Range(0,8).Where(d=>d!=6).All(d=>ListSprLayers.AltDirGfx(1395,d,0,row,chair)==1395),"其他朝向維持 1395");
        // 每幀重算：6 → 2 → 6 依序換、換回、再換（不是建立時換一次）。
        t.Check(new[]{6,2,6,5}.Select(d=>ListSprLayers.AltDirGfx(1395,d,0,row,chair)).SequenceEqual(new[]{1410,1395,1410,1395}),"轉向時每次重算");
        t.Equal(1410,ListSprLayers.AltDirGfx(5940,6,0,Rows[5940],Fields(5940)),"5940（別名 1=1395）自己那一列也是靜態外型");
        // 有動作表的外型完全不看 112。
        string animated="#9 8 x\n0.walk(1 1,0.0:4)\n112.altdir(1410)\n";
        t.Check(!ListSprLayers.StaticShape(animated,0)&&ListSprLayers.AltDirGfx(9,6,0,animated,ListSprFields.Parse(animated))==9,"有動作 0 的外型不換 altdir");
        t.Check(!ListSprLayers.StaticShape(row,4),"動作 > 0 走一般分支（退回姿勢），不是靜態");
        t.Check(!ListSprLayers.StaticShape(null,0),"讀不到 list.spr 時照舊");
        // 欄位編號與影格參照不算動作：107.size／101.shadow 的 0、`24.0:4` 都不是動作 0。
        t.Check(!ListSprLayers.HasAction(Rows[5940],0)&&!ListSprLayers.HasAction("#9 1 x\n3.y(0 1,24.0:4)\n",0)&&ListSprLayers.HasAction("#9 1 x\n3.y(0 1,24.0:4)\n",3),"動作比對只認 `N.名稱(`");
    }
    static void Stride(WorldModuleTestContext t){
        t.Check(ListSprLayers.StrideActions.SequenceEqual(new[]{0,4,11,20,24,40}),"byte_1379D40 動作表");
        var drake=Fields(1062);int counter=0;var poses=new List<int?>();
        for(int i=0;i<3;i++){var s=ListSprLayers.StrideAdvance(counter,drake)!.Value;poses.Add(ListSprLayers.StridePose(drake,s.Step,0));counter=s.Next;}
        t.Check(poses.SequenceEqual(new int?[]{0,4,0}),"1062 連走三步 0→4→0",poses);
        t.Check(ListSprLayers.StrideAdvance(5,Fields(1063))==null&&ListSprLayers.StridePose(Fields(1063),1,0)==null,"沒有 stride：不動計數、不換動作");
        t.Check(ListSprLayers.StridePose(drake,1,ListSprLayers.StrideHoldAction)==null,"目前動作 8 時不換");
        var five=ListSprFields.Parse("#9 1 x\n111.stride(5)\n");counter=0;var all=new List<int?>();
        for(int i=0;i<7;i++){var s=ListSprLayers.StrideAdvance(counter,five)!.Value;all.Add(ListSprLayers.StridePose(five,s.Step,4));counter=s.Next;}
        t.Check(all.SequenceEqual(new int?[]{0,4,11,20,24,40,0}),"stride(5) 依序走完整張表再回 0",all);
    }
    static WorldSession World(){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)4);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        w.ListSprFieldsOf=Fields;
        w.Receive(Appear(Me,32600,1062),"offline");
        return w;
    }
    static WorldObject? Obj(WorldSession w,uint id)=>w.Objects.FirstOrDefault(o=>o.ObjectId==id);
    static void OthersStride(WorldModuleTestContext t){
        var w=World();
        t.Check(w.ExtensionEntries.Any(e=>e.Kind=="observe"&&e.Opcode==10&&e.Method.EndsWith("ObserveStrideMove")),"S_MOVE 觀察器自動註冊");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"擴充註冊沒有錯誤",WorldExtensionRegistry.Default.Errors);
        w.Receive(Appear(Drake,32610,1062),"offline");w.Receive(Appear(Plain,32620,1063),"offline");
        t.Check(Obj(w,Drake) is {StrideCounter:0,StrideStep:0},"S87 建立時計數 0（推測）");
        var steps=new List<int?>();ushort x=32610;
        for(int i=0;i<3;i++){
            w.Receive(Move(Drake,x,32940,2),"offline");x++;
            var o=Obj(w,Drake)!;steps.Add(ListSprLayers.StridePose(Fields(o.Gfx),o.StrideStep,o.Action));
            t.Check(o.MotionAt!=0&&o.X==x,"主分派照常更新位置與 MotionAt（保留計數欄位）",o);
        }
        t.Check(steps.SequenceEqual(new int?[]{0,4,0}),"他人 1062 連走三步 0→4→0",steps);
        t.Check(Obj(w,Drake)!.StrideCounter==1,"三步後計數＝1");
        w.Receive(Move(Plain,32620,32940,2),"offline");
        t.Check(Obj(w,Plain) is {StrideCounter:0,StrideStep:0},"沒有 stride 的 gfx 不動計數");
        // 轉向（S122）不是走路，不前進。
        w.Receive(P([122],Le32(Drake),[3]),"offline");
        t.Check(Obj(w,Drake)!.StrideCounter==1,"轉向不前進計數");
        // 信封不合（tail ≠ 128）時觀察器不動；主分派照舊拒絕。
        try{w.Receive(P([10],Le32(Drake),Le16(x),Le16(32940),[2,0]),"offline");}catch(ProtocolException){}
        t.Check(Obj(w,Drake)!.StrideCounter==1,"不合的移動封包不前進");
        // 自己的 S_MOVE 不走這條（自己由 MovePlanned 前進）。
        try{w.Receive(Move(Me,32600,32940,2),"offline");}catch(Exception ex) when(ex is ProtocolException or InvalidOperationException){}
        t.Check(w.OwnStrideCounter==0,"自己的 S_MOVE 不動計數");
        // 新的 S87 重建物件 → 歸 0。
        w.Receive(Appear(Drake,x,1062),"offline");
        t.Check(Obj(w,Drake) is {StrideCounter:0,StrideStep:0},"重新 S87 歸 0");
        // 死亡的不動。
        w.Receive(Appear(Plain,32630,1062,8),"offline");w.Receive(Move(Plain,32630,32940,2),"offline");
        t.Check(Obj(w,Plain) is {StrideCounter:0},"死亡物件不前進");
    }
    static void OwnStride(WorldModuleTestContext t){
        var w=World();var poses=new List<int?>();
        for(int i=0;i<3;i++){w.AdvanceOwnStride(1062);poses.Add(w.OwnStridePose(1062,0));}
        t.Check(poses.SequenceEqual(new int?[]{0,4,0}),"自己 1062 連走三步 0→4→0",poses);
        t.Check(w.OwnStridePose(1062,ListSprLayers.StrideHoldAction)==null,"自己姿勢 8 不換");
        w.AdvanceOwnStride(1063);t.Check(w.OwnStrideCounter==1&&w.OwnStrideStep==0,"沒有 stride 的外型不動計數");
        t.Check(w.OwnStridePose(1063,4)==null,"沒有 stride 的外型照原姿勢");
        // 只取擴充快照（完整 Snapshot() 會讀 engine/assets 的外型表，CI 沒有）。
        var ext=WorldExtensionRegistry.Default.Snapshot(w);
        t.Check(ext.TryGetValue("stride",out var s)&&JsonSerializer.SerializeToElement(s).GetProperty("own").GetProperty("counter").GetInt32()==1,"快照 stride");
        w.Reset();t.Check(w.OwnStrideCounter==0&&w.OwnStrideStep==0,"Reset 歸 0");
    }
    static void Cloth(WorldModuleTestContext t){
        IListSprSource src=new Source();
        var layers=ListSprLayers.ClothLayers(src.Fields(0));
        t.Check(layers.SequenceEqual(new[]{6262}),"gfx 0 的附加層 6262（不看裝備、動作、朝向）");
        var fishing=ListSprLayers.ClothFrames(layers,"304:1",src.Sprite);
        t.Check(fishing is [{Key:"304:1",Blocks.Length:2}],"子檔 304 與本體同一格，有圖",fishing);
        var idle=ListSprLayers.ClothFrames(layers,"0:1",src.Sprite);
        t.Check(idle is [{Blocks.Length:0}],"子檔 0 存在但是空格",idle);
        t.Check(ListSprLayers.ClothFrames(layers,"24:0",src.Sprite)==null,"缺子檔 → 整組不畫");
        t.Check(ListSprLayers.ClothFrames(new[]{201,6262},"304:0",src.Sprite)==null&&ListSprLayers.ClothFrames(new[]{201,6262},"0:0",src.Sprite) is {Length:2},"任何一層缺檔整組不畫；都在就全畫");
        t.Check(ListSprLayers.ClothFrames(layers,"304:5",src.Sprite)==null,"影格超出子檔 → 整組不畫");
        t.Check(ListSprLayers.ClothFrames([],"304:0",src.Sprite) is {Length:0},"沒有附加層＝空");
        t.Check(ListSprLayers.ClothFrames(layers,"breath",src.Sprite)==null&&ListSprLayers.FrameKey("304:7")==(304,7),"本體 key 解析");
        var draws=ListSprLayers.ClothDraws(ListSprLayers.ClothFrames(new[]{201,6262},"0:0",src.Sprite)!.Select((f,i)=>i==1?Tiles("0:0",2):f).ToArray(),32600,32940,Drake).ToList();
        t.Check(draws.Count==3&&draws.All(d=>d.Layer=="actor"&&d.ObjectId==Drake&&d.Serial>1000000+4095&&d.Serial<1100000),"層序號在本體與影子之間、跟著物件",draws.Select(d=>d.Serial));
        t.Check(draws.Where((d,i)=>i==0).Single().Serial<draws.Skip(1).Min(d=>d.Serial),"依 list.spr 順序（第 1 層在前）");
        t.Equal(18,ListSprLayers.TileType(Rows[6262]),"104.attr(8) → screen（不半透明的那一類）");
        t.Equal(2,ListSprLayers.TileType("#1 1 x\n"),"沒有 104 → 一般");
        t.Check(ListSprLayers.ClothVerified,"ClothVerified 打開（FU14§3.5）");
        // 全空子檔（格式同本機 6262-0.spr 的 65 B：4 格、每格 0 圖塊、0 block）：Decode 會拒絕，但檔案存在，不算缺檔。
        var empty=new List<byte>{4};
        for(int i=0;i<4;i++){foreach(short v in new short[]{192,120,-128,-120})empty.AddRange(BitConverter.GetBytes(v));empty.AddRange(BitConverter.GetBytes((ushort)65535));empty.AddRange(BitConverter.GetBytes((ushort)0));empty.AddRange(BitConverter.GetBytes((short)0));}
        empty.AddRange(new byte[8]);var bytes=empty.ToArray();
        t.Equal(65,bytes.Length,"合成全空子檔 65 B");
        t.Throws<InvalidDataException>(()=>EffectSpriteCatalog.Decode(bytes,0,18),"Decode 拒絕 0 block");
        var none=ListSprLayers.EmptySprite(bytes,0);
        t.Check(none is {Length:4}&&none.All(f=>f.Blocks.Length==0)&&none[3].Key=="0:3","全空子檔 → 4 個空格",none);
        t.Check(ListSprLayers.EmptySprite([..bytes,0],0)==null&&ListSprLayers.EmptySprite(bytes[..30],0)==null,"多出或截斷的資料不當全空");
    }
    static void Fishing(WorldModuleTestContext t){
        var act=ListSprAction.Parse(0,Rows[0],ListSprLayers.FishingAction);
        t.Check(act is {Gfx:0,Sprite:3225,Directional:true,Steps.Length:3},"gfx 0 動作 71（本體子檔走別名 3225）",act);
        t.Check(ListSprLayers.HasAction(Rows[0],4)&&ListSprLayers.HasAction(Rows[0],71)&&!ListSprLayers.HasAction(Rows[0],304)&&!ListSprLayers.HasAction(Rows[0],1),"名稱有空白的動作也認；影格參照不算");
        if(act!=null){
            t.Check(act.At(3,0)==(307,0),"子檔＝304＋朝向");
            t.Check(act.At(3,1000*4/24+1)==(307,1)&&act.At(3,act.Duration+1)==(307,0),"依 ticks 前進、播完重頭");
            IListSprSource src=new Source();var (sid,frame)=act.At(3,0);
            var body=src.Sprite(act.Sprite,sid)![frame];
            t.Check(ListSprLayers.ClothFrames(ListSprLayers.ClothLayers(src.Fields(0)),body.Key,src.Sprite)==null,"合成資料沒有 6262-307 → 整組不畫");
        }
        t.Check(ListSprAction.Parse(9,"#9 8=0 x\n71.f(1 1,304.0:4)\n",71)?.Sprite==0,"標頭別名 =K 決定子檔的 gfx");
        t.Check(ListSprAction.Parse(9,"#9 8 x\n1071.f(1 1,304.0:4)\n",71)==null&&ListSprAction.Parse(1063,Rows[1063],71)==null&&ListSprAction.Parse(0,null,71)==null,"沒有動作 71 回 null（呼叫端退回姿勢動作）");
        t.Check(ListSprAction.Parse(9,"#9 8 x\n71.f(1 2,304.0:4)\n",71)==null,"影格數不符拒絕");
        t.Equal(71,ListSprLayers.FishingAction,"S158 釣魚動作 71");
    }
    static void Runtime(WorldModuleTestContext t){
        var missing=new ListSprRuntime(Path.Combine(AppContext.BaseDirectory,"listspr-wiring-no-client"));
        try{
            t.Check(missing.Row(0)==null&&missing.Fields(0)==null&&missing.Sprite(6262,304)==null,"讀不到原客戶端時全部回 null");
            t.Check(missing.Error!=null,"記下原因");
        }catch(Exception ex){t.Check(false,"讀不到原客戶端時不丟例外",ex.GetType().Name+": "+ex.Message);}
    }
}
