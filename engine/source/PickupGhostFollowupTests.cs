using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
namespace P06;

// R6-D（T334／#780）離線測試：F4 鍵盤拾取（sub_500640）與幽靈中擋巨集格（byte_13CE51E，WO §4）。
// 合成 S87 地面物品、記憶體送出閘門；不開遊戲、網路、視窗，也不讀客戶端素材。
static class PickupGhostFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Own=123;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static object? Get(object o,string name)=>o.GetType().GetProperty(name,F)?.GetValue(o)??o.GetType().GetField(name,F)!.GetValue(o);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static LoginConnection Seed(List<byte[]> sent,byte flags=0){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Direction\":0,\"Name\":\"offline\"}")! with{Flags=flags});
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    // Same envelope as the pickup-tests ground S87 (GroundRules.IsGround): count in the exp field.
    static void GroundAt(LoginConnection c,uint id,int dx,int dy,uint count){
        ushort x=(ushort)(c.World.Actor!.X+dx),y=(ushort)(c.World.Actor.Y+dy);
        c.World.Receive(Packet(b=>{b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);Text(b,"ground "+id);Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}),"offline");
    }
    static uint Id(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5));
    static uint Count(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(9));
    static void Release(LoginConnection c){c.World.InvalidatePickupRequest();Set(c,"nextGroundAt",0L);}
    static GroundItem G(uint id,int x,int y,uint count=1)=>new(id,(ushort)x,(ushort)y,1,count,"g",null!);

    [WorldModuleTest("pickup-ghost-followup")]
    static void Run(WorldModuleTestContext t){Target(t);Loot(t);Throttle(t);Macro(t);}

    static void Target(WorldModuleTestContext t){
        t.Check(GroundRules.LootTarget(100,100,[G(5,102,100),G(9,99,99)])?.ObjectId==9,"LootTarget：只取 1 格內（含斜角）");
        t.Check(GroundRules.LootTarget(100,100,[G(7,101,100),G(3,100,101),G(4,100,100)])?.ObjectId==3,"LootTarget：多個時取 ObjectId 最小的第一個");
        t.Check(GroundRules.LootTarget(100,100,[G(2,100,100,0),G(8,101,101)])?.ObjectId==8,"LootTarget：數量 0 的不算");
        t.Check(GroundRules.LootTarget(100,100,[G(2,102,102)])==null&&GroundRules.LootTarget(100,100,[])==null,"LootTarget：1 格內沒有 → null");
    }

    static void Loot(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            GroundAt(c,700002,2,0,4);
            t.Check(!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==0&&c.World.GroundNotice.Contains("1 格內沒有"),"F4：2 格外不走近、不送");
            GroundAt(c,700005,1,0,12);GroundAt(c,700003,0,1,30);
            t.Check(c.LootNearby().GetAwaiter().GetResult()&&sent.Count==1,"F4：1 格內送一次 C_GET");
            t.Equal((byte)112,sent[0][0],"送 C112");
            t.Equal(700003u,Id(sent[0]),"取第一個（ObjectId 序）");
            t.Equal(30u,Count(sent[0]),"整疊，不跳數量提示（多個也一樣）");
            t.Check(c.World.PickupPrompt==null&&!c.World.PickupAll,"沒有開數量提示，也不依賴全部拾取選項");
            t.Equal((byte)4,c.World.Actor!.Direction,"送出前先轉向物品（南）");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            GroundAt(c,700010,0,0,3);
            t.Check(c.LootNearby().GetAwaiter().GetResult()&&Count(sent.Single())==3,"F4：腳下物品整疊");
            t.Equal((byte)0,c.World.Actor!.Direction,"腳下物品不轉向");
        }
        sent.Clear();
        using(var c=Seed(sent,0x80)){
            GroundAt(c,700020,1,0,1);
            t.Check(c.World.IsGhost&&!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==0&&c.World.GroundNotice.Contains("幽靈"),"F4：幽靈中不拾取");
        }
    }

    static void Throttle(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            GroundAt(c,700031,1,0,1);GroundAt(c,700032,0,1,1);
            t.Check(c.PickupGround(700031).GetAwaiter().GetResult()&&sent.Count==1,"點擊拾取送出");
            c.World.InvalidatePickupRequest();
            long left=(long)Get(c,"nextGroundAt")!-Environment.TickCount64;
            t.Check(left>900&&left<=GroundRules.PickupThrottleMs,"點擊後節流 986 ms",left);
            t.Check(!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==1,"點擊後節流內 F4 不送（共用節流）");
            Release(c);
            t.Check(c.LootNearby().GetAwaiter().GetResult()&&sent.Count==2,"節流過後 F4 可送");
            c.World.InvalidatePickupRequest();
            t.Check(!c.PickupGround(700032).GetAwaiter().GetResult()&&sent.Count==2,"F4 後節流內點擊也不送（共用節流）");
            t.Check(!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==2,"F4 連按：節流內不送也不排隊");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            Set(c,"serverFeatures",new ServerFeatures(0,0,3,950,ServerFeatures.FastPickupFlag,0,0));GroundAt(c,700040,1,1,1);
            t.Check(c.LootNearby().GetAwaiter().GetResult(),"S139 旗標下 F4 可送");
            long left=(long)Get(c,"nextGroundAt")!-Environment.TickCount64;
            t.Check(left>500&&left<=GroundRules.PickupFastThrottleMs,"F4 同樣用 S139 的 600 ms 節流",left);
        }
    }

    static void Macro(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent,0x80)){
            var stamp=PlayerHudActions.Stamp(c);
            t.Check(c.World.IsGhost&&PlayerHudActions.Current(c,stamp)&&PlayerHudActions.GhostBlocks(c),"幽靈：巨集格判定為擋");
            uint? selectedItem=c.World.SelectedItemId;
            t.Check(!PlayerHudActions.Activate(c,stamp,new(PlayerHudKind.Item,555),false).GetAwaiter().GetResult(),"幽靈：道具格（F5–F12／雙擊）不使用");
            t.Check(!PlayerHudActions.Activate(c,stamp,new(PlayerHudKind.Magic,1),false).GetAwaiter().GetResult(),"幽靈：技能格不施放");
            t.Check(sent.Count==0&&c.World.SelectedItemId==selectedItem,"幽靈：不送封包、不改選取");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            t.Check(!c.World.IsGhost&&!PlayerHudActions.GhostBlocks(c),"非幽靈：巨集格不被幽靈規則擋");
        }
    }
}
