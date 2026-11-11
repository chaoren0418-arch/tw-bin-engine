using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// R12-A（T428／#985）離線測試：自己的 S158 71（OwnActionState.cs）與隱身時附加層的層旗標例外（Invisibility.cs）。
// 由 --module-tests 執行（名稱 own-fishing-layer）。只用合成 list.spr 列、合成影格與合成封包；
// 不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。
static class OwnFishingLayerTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Other=456;
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
    // S87（欄位同 ListSprWiringTests.Appear）。
    static byte[] Appear(uint id,ushort x,ushort gfx)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[0,0,0,0],Le32(0),Le16(0),
        Str(id==Me?"offline":"target"+id),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static byte[] Action(uint id,byte action)=>P([158],Le32(id),[action],Le16(32700),Le16(32800));
    static byte[] Invisible(uint id,byte type)=>P([171],Le32(id),[type]);

    // 照本機 list.spr 的寫法（FU14§3.3）：gfx 0 的本體子檔走別名 3225，動作 71 用子檔 304＋朝向，cloth 6262（104.attr(8)）。
    static readonly Dictionary<int,string> Rows=new(){
        [0]="#0\t312=3225\tprince\n\t0.walk(1 4,0.0:4 0.1:4 0.2:4 0.3:4)\n\t71.fishing(1 3,304.0:4 304.1:4 304.2:4)\n\t101.shadow(3226)\n\t104.attr(0)\n\t105.cloth(1 6262)\n",
        [6262]="#6262 8 prince fishing line\n104.attr(8)\n",
        [1063]="#1063 8 plain\n0.walk(1 1,0.0:4)\n",
    };
    static Frame Tiles(string key,int count)=>new(key,[0,0,23,23],Enumerable.Range(0,count).Select(i=>new Block(i,0,0,i,new Tile(2,[new Pixel(1,1,0x1234)]))).ToArray());
    static Frame[]? Sprite(int gfx,int sid)=>(gfx,sid) switch{
        (3225,>=304 and <=311)=>[Tiles($"{sid}:0",4),Tiles($"{sid}:1",4),Tiles($"{sid}:2",4)],
        (6262,>=304 and <=311)=>[Tiles($"{sid}:0",2),Tiles($"{sid}:1",2),Tiles($"{sid}:2",2)],
        _=>null};
    sealed class Source:IListSprSource {
        public string? Row(int gfx)=>Rows.GetValueOrDefault(gfx);
        public Frame[]? Sprite(int gfx,int sid)=>OwnFishingLayerTests.Sprite(gfx,sid);
    }
    sealed class NoCloth:IListSprSource {
        public string? Row(int gfx)=>Rows.GetValueOrDefault(gfx);
        public Frame[]? Sprite(int gfx,int sid)=>gfx==6262?null:OwnFishingLayerTests.Sprite(gfx,sid);
    }

    [WorldModuleTest("own-fishing-layer")]
    static void Run(WorldModuleTestContext t){OwnAction(t);OwnFishingDraw(t);LayerTranslucency(t);}

    static WorldSession World(Func<long> clock,ushort gfx=0){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)4);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        w.ListSprFieldsOf=g=>Rows.TryGetValue(g,out var r)?ListSprFields.Parse(r):null;
        w.ActionClock=clock;
        w.Receive(Appear(Me,32600,gfx),"offline");
        return w;
    }
    static void Receive(WorldSession w,byte[] packet){
        // 主分派對合成封包可能拒絕（例如 S119 的外觀表在 CI 沒有）；觀察器先跑，這裡只看觀察器的結果。
        try{w.Receive(packet,"offline");}catch(Exception){}
    }

    static void OwnAction(WorldModuleTestContext t){
        long clock=1000;var w=World(()=>clock);
        t.Check(w.Actor is {ObjectId:Me},"合成世界有自己",w.Actor);
        t.Check(w.ExtensionEntries.Any(e=>e.Kind=="observe"&&e.Opcode==158&&e.Method.EndsWith("ObserveOwnAction")),"S158 自己動作觀察器自動註冊");
        t.Check(w.ExtensionEntries.Any(e=>e.Kind=="observe"&&e.Opcode==119&&e.Method.EndsWith("ObserveOwnActionAppearance")),"S119 觀察器自動註冊");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"擴充註冊沒有錯誤",WorldExtensionRegistry.Default.Errors);
        t.Check(w.OwnAction==null&&w.OwnFishingSince()==null,"一開始沒有自己的動作");
        // 他人的 S158 71 不算自己。
        w.Receive(Appear(Other,32610,0),"offline");Receive(w,Action(Other,71));
        t.Check(w.OwnAction==null,"他人的 71 不記成自己");
        int unknown=w.UnknownObjectUpdates;
        Receive(w,Action(Me,71));
        t.Check(w.OwnAction is {Action:71,At:1000,X:32600,Y:32940},"自己的 S158 71 記成自己的動作狀態",w.OwnAction);
        t.Check(w.OwnFishingSince()==1000,"自己在釣魚，開始時間＝收到時的 ActionClock",w.OwnFishingSince());
        t.Check(w.UnknownObjectUpdates>=unknown,"主分派照舊（觀察器不吃封包）");
        var ext=WorldExtensionRegistry.Default.Snapshot(w);
        t.Check(ext.TryGetValue("own-action",out var s)&&JsonSerializer.SerializeToElement(s).GetProperty("fishing").GetBoolean(),"快照 own-action");
        // 戰鬥動作比它新 → 不再釣魚；比它舊不影響。
        Set(w,"OwnCombat",new CombatAnimation(1,999,false));
        t.Check(w.OwnFishingSince()==1000,"比釣魚舊的戰鬥動作不影響");
        Set(w,"OwnCombat",new CombatAnimation(1,1000,false));
        t.Check(w.OwnFishingSince()==null,"之後的戰鬥動作結束釣魚");
        Set(w,"OwnCombat",null);
        // 別的動作蓋掉。
        clock=2000;Receive(w,Action(Me,71));Receive(w,Action(Me,0));
        t.Check(w.OwnAction is {Action:0}&&w.OwnFishingSince()==null,"自己的下一個 S158 動作蓋掉 71",w.OwnAction);
        // 移動結束。
        Receive(w,Action(Me,71));t.Check(w.OwnFishingSince()==2000,"再收到 71");
        Set(w,"Actor",w.Actor! with{X=32601});
        t.Check(w.OwnFishingSince()==null,"自己移動（格座標改變）結束釣魚");
        // S119（收竿時伺服器送外觀更新）結束。
        Receive(w,Action(Me,71));t.Check(w.OwnFishingSince()!=null,"新位置再收到 71");
        Receive(w,P([119],Le32(Other),[0]));
        t.Check(w.OwnFishingSince()!=null,"他人的 S119 不影響");
        Receive(w,P([119],Le32(Me),[0]));
        t.Check(w.OwnAction==null,"自己的 S119 清掉動作狀態");
        // 世界重設清掉。
        Receive(w,Action(Me,71));t.Check(w.OwnAction!=null,"重設前有");
        w.Reset();t.Check(w.OwnAction==null&&w.OwnFishingSince()==null,"Reset 清掉");
        // 格式錯誤（太短）留給主分派，觀察器不記。
        var w2=World(()=>clock);Receive(w2,[158,123,0]);
        t.Check(w2.OwnAction==null,"截斷的 S158 不記");
    }

    static Renderer Bare(IListSprSource src){
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);r.ListSpr=src;return r;
    }

    static void OwnFishingDraw(WorldModuleTestContext t){
        long clock=5000;var w=World(()=>clock);var r=Bare(new Source());
        t.Check(r.OwnFishingFrame(w,false,clock)==null,"沒有 71 時不畫釣魚");
        Receive(w,Action(Me,71));
        var actor=w.Actor!;int sid=304+(actor.Direction&7);
        var fish=r.OwnFishingFrame(w,false,clock);
        t.Check(fish is {Blocks.Length:4}&&fish.Key==$"{sid}:0","自己的釣魚當格＝3225-(304＋朝向) 第 0 格",fish?.Key);
        t.Check(r.OwnFishingFrame(w,false,clock+1000*4/24+1)?.Key==$"{sid}:1","依 ticks 前進");
        t.Check(r.OwnFishingFrame(w,true,clock)==null,"走路中不畫釣魚");
        if(fish!=null){
            var draws=r.OwnFishingDraws(actor,fish);
            var body=draws.Where(d=>d.Serial<ListSprLayers.ClothSerialBase).ToList();var line=draws.Where(Invisibility).ToList();
            t.Check(body.Count==4&&body.All(d=>d.Layer=="actor"&&d.ObjectId==0),"本體 4 塊，ObjectId 0（自己）",body.Count);
            t.Check(line.Count==2&&line.All(d=>d.ObjectId==0&&d.Layer=="actor"),"cloth 6262 同一個 sid:frame 畫出 2 塊（釣魚線）",line.Count);
            int x0=2*(actor.X-Coordinates.MinX),y0=actor.Y-Coordinates.MinY;
            t.Check(draws.All(d=>d.X==x0&&d.Y==y0),"位置照自己的格座標",draws.Select(d=>(d.X,d.Y)).Distinct());
            // 缺 6262 子檔 → 只畫本體（整組附加層不畫，FU14§3.1）。
            var only=Bare(new NoCloth()).OwnFishingDraws(actor,fish);
            t.Check(only.Count==4&&!only.Any(Invisibility),"缺 6262 子檔時附加層整組不畫");
        }
        // 沒有動作 71 的外型（1063）回 null → 呼叫端照舊畫姿勢。
        var plain=World(()=>clock,1063);Receive(plain,Action(Me,71));
        t.Check(plain.OwnFishingSince()!=null&&r.OwnFishingFrame(plain,false,clock)==null,"外型沒有動作 71 時退回姿勢");
        // 死亡不畫。
        Set(w,"OwnDead",true);t.Check(r.OwnFishingFrame(w,false,clock)==null,"死亡時不畫釣魚");
    }
    static bool Invisibility(Draw d)=>WorldSession.IsAttachedLayer(d);

    static void LayerTranslucency(WorldModuleTestContext t){
        long clock=1;var w=World(()=>clock);
        Draw D(uint id,int serial,int type,string layer="actor")=>new(0,0,0,serial,layer,0,-1,-1,new Tile(type,[new Pixel(0,0,31)]),id);
        int cloth=ListSprLayers.ClothSerialBase;
        t.Check(WorldSession.IsAttachedLayer(D(0,cloth,0))&&WorldSession.IsAttachedLayer(D(0,1099999,0)),"附加層序號範圍");
        t.Check(!WorldSession.IsAttachedLayer(D(0,1000000,0))&&!WorldSession.IsAttachedLayer(D(0,cloth-1,0))&&!WorldSession.IsAttachedLayer(D(0,1100000,0))&&!WorldSession.IsAttachedLayer(D(0,cloth,0,"shadow")),"本體、影子不是附加層");
        // 沒有隱身：全部照原值。
        t.Check(w.InvisibilityOpacity(D(0,cloth,0),null)==null&&w.InvisibilityOpacity(D(0,1000000,0),null)==null,"沒有隱身時不動");
        Receive(w,Invisible(Me,1));
        t.Check(w.Actor is {} a&&(a.Flags&2)!=0,"S171 自己隱身",w.Actor?.Flags);
        t.Check(w.InvisibilityOpacity(D(0,1000000,16),null) is {Half:true},"本體照舊半透明（即使圖塊是 screen）");
        t.Check(w.InvisibilityOpacity(D(0,1100000,0,"shadow"),null) is {Half:true},"影子照舊半透明");
        t.Check(w.InvisibilityOpacity(D(0,cloth,0),null) is {Half:true},"層旗標 0 的附加層半透明");
        t.Check(w.InvisibilityOpacity(D(0,cloth,4),null) is {Half:true},"層旗標 0x100 的附加層半透明");
        t.Check(w.InvisibilityOpacity(D(0,cloth,16),null)==null,"層旗標 8（screen，例如 6262 釣魚線）不半透明");
        t.Check(w.InvisibilityOpacity(D(0,cloth,32),null)==null,"層旗標 0x80（multiply）不半透明");
        var given=new OpacityValue(8,0,0);
        t.Check(w.InvisibilityOpacity(D(0,cloth,16),given)==given,"有原值時原樣傳回");
        t.Check(w.InvisibilityOpacity(D(Me,cloth+5,16),null)==null,"以自己的 id 畫的層也一樣");
        // 他人隱身（不在隊伍）：整組不畫，旗標例外不適用。
        w.Receive(Appear(Other,32610,0),"offline");Receive(w,Invisible(Other,1));
        t.Check(w.InvisibilityOpacity(D(Other,cloth,16),null) is {Hidden:true}&&w.InvisibilityOpacity(D(Other,cloth,0),null) is {Hidden:true},"隊伍外的隱身他人：附加層也不畫");
        // 隊伍成員：0x100 同自己。
        var members=(System.Collections.IDictionary)typeof(WorldSession).GetField("partyMembers",F)!.GetValue(w)!;
        members["target456"]=new PartyMember(Other,"target456",100);
        t.Check(w.InvisibilityOpacity(D(Other,cloth,16),null)==null&&w.InvisibilityOpacity(D(Other,cloth,0),null) is {Half:true}&&w.InvisibilityOpacity(D(Other,1000000,16),null) is {Half:true},"隊伍成員：層旗標 8 不半透明，其餘半透明");
        // 解除隱身。
        Receive(w,Invisible(Me,0));
        t.Check(w.InvisibilityOpacity(D(0,cloth,0),null)==null,"解除隱身後不動");
    }
}
