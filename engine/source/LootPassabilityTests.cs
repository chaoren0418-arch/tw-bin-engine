using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
namespace P06;

// R10-K（T395／#910）離線測試：拾取通行判斷（FU8 §6／Q6-7：sub_5001C0、sub_500640 → sub_F12490 bit 0）。
// 物品與自己不同格時，自己往物品方向那一步要能走（地圖屬性＋S_Door）；腳下物品不查。
// 地形用 WorldSession.PickupTerrain 合成、門用合成 S209、送出用記憶體閘門；不開遊戲、網路、視窗，不讀客戶端素材。
static class LootPassabilityTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Direction\":0,\"Name\":\"offline\"}"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        c.World.PickupTerrain=(_,_,_)=>true;
        return c;
    }
    static void GroundAt(LoginConnection c,uint id,int dx,int dy,uint count){
        ushort x=(ushort)(c.World.Actor!.X+dx),y=(ushort)(c.World.Actor.Y+dy);
        c.World.Receive(Packet(b=>{b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);Text(b,"ground "+id);Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}),"offline");
    }
    // S209 (S_OPCODE_ATTRIBUTE): H x, H y, C direction, C flag; flag 1 overlays the entrance and its two neighbours.
    static void DoorAttr(LoginConnection c,int dx,int dy,byte direction,byte flag){
        ushort x=(ushort)(c.World.Actor!.X+dx),y=(ushort)(c.World.Actor.Y+dy);
        c.World.Receive(Packet(b=>{b.Write((byte)209);b.Write(x);b.Write(y);b.Write(direction);b.Write(flag);}),"offline");
    }
    static uint Id(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5));
    static void Release(LoginConnection c){c.World.InvalidatePickupRequest();Set(c,"nextGroundAt",0L);}
    static GroundItem G(uint id,int x,int y,uint count=1)=>new(id,(ushort)x,(ushort)y,1,count,"g",null!);
    // Blocks exactly the given headings out of the actor tile.
    static Func<int,int,int,bool> Blocking(params int[] headings)=>(_,_,h)=>!headings.Contains(h);

    [WorldModuleTest("loot-passability")]
    static void Run(WorldModuleTestContext t){Rules(t);Click(t);Loot(t);Doors(t);}

    static void Rules(WorldModuleTestContext t){
        int calls=0;bool Never(int x,int y,int h){calls++;return false;}
        t.Check(GroundRules.PickupStepClear(100,100,100,100,Never)&&calls==0,"腳下物品：不查通行（sub_F12490 只在不同格時呼叫）");
        var seen=new List<int>();bool Record(int x,int y,int h){seen.Add(h);return true;}
        GroundRules.PickupStepClear(100,100,101,99,Record);GroundRules.PickupStepClear(100,100,100,101,Record);GroundRules.PickupStepClear(100,100,99,100,Record);
        t.Check(seen.SequenceEqual(new[]{1,4,6}),"查的方向＝PickupHeading（東北 1、南 4、西 6）",string.Join(",",seen));
        t.Check(!GroundRules.PickupStepClear(100,100,101,100,Blocking(2)),"往物品那一步被擋 → 不可拾取");
        t.Check(GroundRules.PickupStepClear(100,100,101,100,Blocking(6)),"其他方向被擋不影響");
        t.Check(GroundRules.LootTarget(100,100,[G(3,101,100),G(5,100,101)],Blocking(2))?.ObjectId==5,"LootTarget：被擋的跳過，繼續找下一個");
        t.Check(GroundRules.LootTarget(100,100,[G(3,101,100),G(5,100,101)],Blocking(2,4))==null,"LootTarget：1 格內全被擋 → null");
        t.Check(GroundRules.LootTarget(100,100,[G(3,101,100),G(9,100,100)],(_,_,_)=>false)?.ObjectId==9,"LootTarget：全擋時腳下物品仍可取");
        t.Check(GroundRules.LootTarget(100,100,[G(3,101,100)])?.ObjectId==3,"LootTarget：不給 step 時行為同舊版");
    }

    static void Click(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            GroundAt(c,710001,1,0,1);c.World.PickupTerrain=Blocking(2);
            t.Check(!c.PickupGround(710001).GetAwaiter().GetResult()&&sent.Count==0,"點擊：1 格內但往東那步被擋 → 不送");
            t.Equal(LoginConnection.StepBlockedNotice,c.World.GroundNotice,"點擊：被擋的提示");
            t.Equal((byte)0,c.World.Actor!.Direction,"點擊：被擋時不轉向");
            c.World.PickupTerrain=Blocking(6);
            t.Check(c.PickupGround(710001).GetAwaiter().GetResult()&&Id(sent.Single())==710001,"點擊：其他方向被擋時照常送 C_GET");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            GroundAt(c,710002,0,1,7);c.World.PickupTerrain=Blocking(4);
            t.Check(!c.PickupGround(710002).GetAwaiter().GetResult()&&c.World.PickupPrompt==null&&sent.Count==0,"點擊多個：被擋時不開數量提示（步驟 1 在步驟 3 之前）");
            c.World.PickupTerrain=(_,_,_)=>true;
            t.Check(!c.PickupGround(710002).GetAwaiter().GetResult()&&c.World.PickupPrompt!=null,"點擊多個：不擋時開數量提示");
            c.World.PickupTerrain=Blocking(4);
            t.Check(!c.AnswerPickupPrompt("").GetAwaiter().GetResult()&&sent.Count==0&&c.World.GroundNotice==LoginConnection.StepBlockedNotice,"數量回答：送出前同一個 SendGround 也擋");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            GroundAt(c,710003,0,0,1);c.World.PickupTerrain=(_,_,_)=>false;
            t.Check(c.PickupGround(710003).GetAwaiter().GetResult()&&sent.Count==1,"腳下物品：四周全擋仍可拾取");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            GroundAt(c,710004,-1,-1,1);c.World.PickupTerrain=Blocking(7);c.World.SelectGround(710004);
            t.Check(!c.PickupSelected(1).GetAwaiter().GetResult()&&sent.Count==0,"直接 PickupSelected：斜向被擋也不送");
            // Approach arrival (mode 3) goes through PickupArrived: the same check applies there.
            var arrived=(Task<bool>)typeof(LoginConnection).GetMethod("PickupArrived",F)!.Invoke(c,[710004u])!;
            t.Check(!arrived.GetAwaiter().GetResult()&&sent.Count==0&&c.World.GroundNotice==LoginConnection.StepBlockedNotice,"走近到達後（PickupArrived）同樣檢查");
        }
    }

    static void Loot(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            GroundAt(c,710011,1,0,2);GroundAt(c,710012,0,1,3);c.World.PickupTerrain=Blocking(2);
            t.Check(c.LootNearby().GetAwaiter().GetResult()&&Id(sent.Single())==710012,"F4：跳過被擋的第一個，撿下一個");
            t.Equal((byte)4,c.World.Actor!.Direction,"F4：轉向實際撿的物品（南）");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            GroundAt(c,710013,1,0,2);c.World.PickupTerrain=Blocking(2);
            t.Check(!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==0,"F4：1 格內唯一的物品被擋 → 不送");
            t.Equal(LoginConnection.StepBlockedNotice,c.World.GroundNotice,"F4：提示是被擋，不是 1 格內沒有");
        }
    }

    static void Doors(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            GroundAt(c,710021,1,0,1);
            DoorAttr(c,1,0,1,1);
            t.Check(c.World.DoorBlocks(c.World.Actor!.X,c.World.Actor.Y,2),"S209 旗標 1：門格被標為不可通行");
            t.Check(!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==0,"門關（S209 旗標 1）：F4 不撿門後物品");
            t.Check(!c.PickupGround(710021).GetAwaiter().GetResult()&&sent.Count==0,"門關：點擊也不撿");
            DoorAttr(c,1,0,1,0);
            t.Check(c.PickupGround(710021).GetAwaiter().GetResult()&&sent.Count==1,"門開（S209 旗標 0）：可拾取");
            Release(c);
        }
        sent.Clear();
        using(var c=Seed(sent)){
            // Door segment at (+1,−1) overlays (+1,−2..0): the SE target tile (+1,+1) itself stays clear.
            GroundAt(c,710022,1,1,1);
            DoorAttr(c,1,-1,1,1);
            t.Check(!c.World.DoorBlocks(c.World.Actor!.X+1,c.World.Actor.Y,4),"前提：目標格本身沒被門覆蓋");
            t.Check(!c.LootNearby().GetAwaiter().GetResult()&&sent.Count==0,"斜向：相鄰格是門也擋（與移動同判斷）");
        }
    }
}
