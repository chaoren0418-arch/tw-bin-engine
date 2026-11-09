using System.Buffers.Binary;
using System.Text.Json;
namespace P06;

// R2-C（T253／#614）離線測試：合成 S87／S158／S139、記憶體 sender 與記憶體走近；不開遊戲、網路，不讀客戶端素材。
// 依據：WO 第 1、2、4 節（world-objects.md）、PG§4（protocol-gaps.md）、gap-matrix 11.5 R2-C。
public sealed partial class WorldSession {
    internal void SeedPointerFlowWorld(uint id,ushort x,ushort y){
        Entered=true;MapId=0;TimeReplies=1;CacheEnabled=false;
        Status=JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{id},\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}}");
        Actor=JsonSerializer.Deserialize<OwnActor>($"{{\"ObjectId\":{id},\"X\":{x},\"Y\":{y},\"Direction\":0,\"Name\":\"{PointerFlowTests.Name}\"}}");
    }
    internal void SetPointerFlowActor(OwnActor actor)=>Actor=actor;
}
public sealed partial class LoginConnection {
    internal List<byte[]> SeedPointerFlow(uint id,ushort x,ushort y){
        World.SeedPointerFlowWorld(id,x,y);State=LoginState.InWorld;var sent=new List<byte[]>();
        sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
        return sent;
    }
    internal long PointerFlowGroundWait=>nextGroundAt-Environment.TickCount64;
    internal void PointerFlowReleaseGround(){nextGroundAt=0;World.InvalidatePickupRequest();}
}

static class PointerFlowTests {
    public const string Name="pointer-flow";
    const uint Own=123,Npc=456,Door=700;
    const ushort X=32600,Y=32940;

    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    // S87 ground envelope (server S_NPCPack_Item shape, same fields GroundRules.IsGround checks).
    static byte[] GroundPack(uint id,ushort x,ushort y,uint count)=>Packet(b=>{
        var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write(row.GroundGfx);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);
        Text(b,row.CountName(count));Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] NpcPack(uint id,ushort x,ushort y)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1110);b.Write((byte)0);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);
        Text(b,"target");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    // DoorPack shape (Doors.cs DoorPackets.IsPack), synthetic id/gfx.
    static byte[] DoorPack(uint id,ushort x,ushort y)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)2346);b.Write((byte)29);
        b.Write(new byte[3]);b.Write(1u);b.Write(new byte[12]);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] Death(uint id)=>LoginPackets.Pad([158,..BitConverter.GetBytes(id),8]);
    static uint Count(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(9));
    static uint Id(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5));
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static PlayerPointerController Pointer(LoginConnection c,Func<uint,bool>? visible=null)=>new(c,()=>true,()=>false,visible??(_=>true));
    // Memory approach: one step = local position update (as PointerMove predicts); terrain all passable.
    static ApproachWalk MemoryApproach(LoginConnection c,List<int> steps)=>new(c,wait:()=>Task.CompletedTask,terrain:(_,_,_)=>true,available:()=>true,move:(h,valid)=>{
        if(!valid())return Task.FromResult(false);steps.Add(h);var a=c.World.Actor!;
        c.World.SetPointerFlowActor(a with{X=(ushort)(a.X+MovementRules.Dx[h]),Y=(ushort)(a.Y+MovementRules.Dy[h]),Direction=(byte)h});return Task.FromResult(true);});

    [WorldModuleTest("pointer-flow")]
    static void Run(WorldModuleTestContext t){GroundClick(t);Throttle(t);DeathBlock(t);GhostBlock(t);DoorNotice(t);}

    // 1. 雙擊地上物品走 PickupGroundClicked（WO §1–2）：1 格內單個整疊、多個開數量提示、遠處先走近。
    static void GroundClick(WorldModuleTestContext t){
        using(var c=new LoginConnection()){
            var sent=c.SeedPointerFlow(Own,X,Y);c.World.Receive(GroundPack(900401,(ushort)(X+1),Y,1),Name);var p=Pointer(c);
            t.Check(Wait(p.ClickGroundAsync(900401))&&sent.Count==1&&sent[0][0]==112&&Id(sent[0])==900401&&Count(sent[0])==1,"1 格內單個：雙擊直接送整疊 C_GET");
            t.Equal(c.World.GroundNotice,p.Notice,"滑鼠提示改讀地面流程的 GroundNotice");
            t.Check(!p.Active,"拾取完成後 pointer 不再 active");
        }
        using(var c=new LoginConnection()){
            var sent=c.SeedPointerFlow(Own,X,Y);c.World.Receive(GroundPack(900402,X,(ushort)(Y+1),7),Name);var p=Pointer(c);
            t.Check(!Wait(p.ClickGroundAsync(900402))&&sent.Count==0&&c.World.PickupPrompt?.Id==900402,"多個且未開全部拾取：雙擊開數量提示，不送");
            t.Check(p.Notice.StartsWith("拾取數量"),"提示文字顯示在滑鼠提示",p.Notice);
            t.Check(Wait(c.AnswerPickupPrompt(""))&&sent.Count==1&&Count(sent[0])==7,"提示空白＝整疊");
            c.PointerFlowReleaseGround();c.World.TogglePickupAll();c.World.Receive(GroundPack(900403,X,Y,4),Name);
            t.Check(Wait(p.ClickGroundAsync(900403))&&sent.Count==2&&Count(sent[1])==4,"全部拾取開：多個也整疊");
        }
        using(var c=new LoginConnection()){
            var sent=c.SeedPointerFlow(Own,X,Y);var steps=new List<int>();c.Approach=MemoryApproach(c,steps);c.World.Receive(GroundPack(900404,(ushort)(X+4),Y,1),Name);var p=Pointer(c);
            t.Check(Wait(p.ClickGroundAsync(900404))&&steps.SequenceEqual([2,2,2])&&sent.Count==1&&Id(sent[0])==900404,"遠處物品：走近到 1 格內再送一次");
            t.Check(c.World.Actor!.X==X+3&&c.World.Actor.Direction==2,"走近後送出前本機轉向");
        }
        using(var c=new LoginConnection()){
            var sent=c.SeedPointerFlow(Own,X,Y);c.World.Receive(GroundPack(900405,X,Y,1),Name);
            uint? routed=null;var p=Pointer(c);
            t.Check(!Wait(p.ClickGroundAsync(900405,id=>{routed=id;return Task.FromResult(false);}))&&routed==900405&&sent.Count==0,"UI 傳入的 PickupGroundClicked 收到被點的物品 id");
            var hidden=Pointer(c,_=>false);
            t.Check(!Wait(hidden.ClickGroundAsync(900405))&&sent.Count==0&&hidden.Notice==PointerFlow.GroundGone,"不可見的物品不送");
            t.Check(!Wait(p.ClickGroundAsync(999999))&&sent.Count==0&&p.Notice==PointerFlow.GroundGone,"不存在的物品不送");
        }
        t.Equal("拾取請求已送出（整疊或指定數量），等待地面與背包回報。",PlayerPointerHudFeedback.Submitted("pickup"),"HUD 不再寫「1 件」");
        t.Check(!PointerFlow.Help.Contains("1件"),"操作說明不再寫拾取 1 件");
    }

    // 2. 拾取節流改讀 S139 功能旗標（WO §2 第 4 步、PG§4）。
    static void Throttle(WorldModuleTestContext t){
        using(var c=new LoginConnection()){
            var sent=c.SeedPointerFlow(Own,X,Y);
            t.Check(c.ServerFeatures==null&&!c.PickupFastFlag&&c.PickupThrottleMs==986,"沒收到 S139：986 ms（原版 bss 旗標為 0）");
            c.World.SetPointerFlowActor(c.World.Actor! with{Speed=1});
            t.Check(!c.PickupFastFlag&&c.PickupThrottleMs==986,"自身加速不再當成 0x10000");
            c.AcceptServerVersion(ServerFeaturesTests.S139(flags:0x77d82));
            t.Check(c.PickupFastFlag&&c.PickupThrottleMs==600,"S139 旗標含 0x10000：600 ms");
            c.World.Receive(GroundPack(900411,X,Y,1),Name);
            t.Check(Wait(c.PickupGround(900411))&&sent.Count==1,"S139 下拾取可送");
            long left=c.PointerFlowGroundWait;t.Check(left>500&&left<=600,"送出後本機節流 600 ms",left);
            c.AcceptServerVersion(ServerFeaturesTests.S139(flags:0x800000));
            t.Check(!c.PickupFastFlag&&c.PickupThrottleMs==986,"S139 旗標沒有 0x10000：986 ms");
            c.AcceptServerVersion(ServerFeaturesTests.S139(status:1));
            t.Check(!c.PickupFastFlag&&c.PickupThrottleMs==986,"S139 狀態非 0：旗標不採用，986 ms");
        }
        using(var c=new LoginConnection()){
            c.SeedPointerFlow(Own,X,Y);c.World.Receive(ServerFeaturesTests.S139(),Name);
            t.Check(c.PickupFastFlag&&c.PickupThrottleMs==600,"進世界後才到的 S139 也採用");
        }
    }

    // 3. 死亡阻擋套到 NPC 交談與攻擊點擊（WO §1：byte_13CE3FD 時 sub_F112F0 直接返回）。
    static void DeathBlock(WorldModuleTestContext t){
        using var c=new LoginConnection();var sent=c.SeedPointerFlow(Own,X,Y);
        c.World.Receive(NpcPack(Npc,(ushort)(X+1),Y),Name);c.World.Receive(GroundPack(900421,X,Y,1),Name);c.World.Receive(Death(Own),Name);
        t.Check(c.World.OwnDead&&c.ObjectClicksBlocked,"S158 動作 8 設定自身死亡");
        var p=Pointer(c);
        foreach(bool talk in new[]{true,false}){
            var task=p.ActAsync(Npc,talk);
            t.Check(task.IsCompletedSuccessfully&&!task.Result&&sent.Count==0,"死亡中點 NPC 不送 "+(talk?"交談":"攻擊"));
            t.Equal(PointerFlow.DeadNotice,p.Notice,"死亡提示 "+(talk?"交談":"攻擊"));
            t.Equal<string?>(PointerFlow.DeadNotice,p.Refusal(Npc,talk),"Refusal（個人商店入口共用）"+(talk?"交談":"攻擊"));
        }
        t.Check(!Wait(p.ClickGroundAsync(900421))&&sent.Count==0,"死亡中雙擊地上物品不送");
    }

    // 4. 幽靈中擋滑鼠攻擊（WO §4：GhostLocalBlocked）；NPC 交談與門不在這裡擋。
    static void GhostBlock(WorldModuleTestContext t){
        using var c=new LoginConnection();var sent=c.SeedPointerFlow(Own,X,Y);
        c.World.Receive(NpcPack(Npc,(ushort)(X+1),Y),Name);c.World.Receive(DoorPack(Door,X,(ushort)(Y-1)),Name);
        c.World.SetPointerFlowActor(c.World.Actor! with{Flags=0x80});
        t.Check(c.World.IsGhost&&c.GhostLocalBlocked,"自身 flags 0x80 為幽靈");
        var p=Pointer(c);var task=p.ActAsync(Npc,false);
        t.Check(task.IsCompletedSuccessfully&&!task.Result&&sent.Count==0,"幽靈中滑鼠攻擊不送（同步返回）");
        t.Equal(GhostLocalLimits.Notice,p.Notice,"幽靈提示沿用 GhostLocalLimits");
        t.Equal<string?>(null,p.Refusal(Npc,true),"幽靈中 NPC 交談不在本機擋");
        t.Equal<string?>(null,p.Refusal(Door,false),"幽靈中點門不在本機擋");
        c.World.SetPointerFlowActor(c.World.Actor! with{Flags=0});
        t.Equal<string?>(null,p.Refusal(Npc,false),"離開幽靈後攻擊不再被本機擋");
    }

    // 5. 門提示改讀 DoorNotice（ToggleDoor 的實際理由）。
    static void DoorNotice(WorldModuleTestContext t){
        using var c=new LoginConnection();var sent=c.SeedPointerFlow(Own,X,Y);
        c.World.Receive(DoorPack(Door,X,(ushort)(Y-1)),Name);
        t.Check(c.World.IsDoor(Door),"合成 DoorPack 辨識為門");
        c.World.Receive(Death(Own),Name);
        var p=Pointer(c);
        t.Equal<string?>(null,p.Refusal(Door,true),"門的死亡阻擋交給 ToggleDoor");
        t.Check(!Wait(p.ActAsync(Door,true))&&sent.Count==0,"死亡中點門不送");
        t.Check(p.Notice==c.DoorNotice&&p.Notice.Contains("死亡"),"滑鼠提示＝DoorNotice（死亡）",new{pointer=p.Notice,door=c.DoorNotice});
    }
}
