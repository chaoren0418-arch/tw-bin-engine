using P06;
using System.Buffers.Binary;

// T213 / #519 (M7-M): door click aligned to WO §1/§3. A door farther than 1 tile is approached
// with ApproachWalk first, then C41 is sent once; own death blocks object clicks (byte_13CE3FD).
// Synthetic S87/S120 packets, memory sender, memory movement, synthetic terrain; no game, socket or assets.
partial class Checks {
    static (LoginConnection C,List<byte[]> Sent,List<int> Steps) DoorApproachSeed(int dx,int dy,Func<int,int,int,bool>? terrain=null,Action<LoginConnection,int>? onStep=null){
        var c=Seed();Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));var sent=PickupSender(c);var steps=new List<int>();
        Feed(c,DoorPack(x:(ushort)(c.World.Actor!.X+dx),y:(ushort)(c.World.Actor.Y+dy)));
        Set(c,"Approach",new ApproachWalk(c,wait:()=>Task.Delay(1),terrain:terrain??((_,_,_)=>true),available:()=>c.MovementAvailable,move:(h,valid)=>{
            if(!valid())return Task.FromResult(false);steps.Add(h);var a=c.World.Actor!;
            Set(c.World,"Actor",a with{X=(ushort)(a.X+MovementRules.Dx[h]),Y=(ushort)(a.Y+MovementRules.Dy[h]),Direction=(byte)h});
            onStep?.Invoke(c,steps.Count);return Task.FromResult(true);}));
        return (c,sent,steps);
    }
    static bool IsToggle(byte[] b,ushort x,ushort y)=>b.SequenceEqual(DoorPackets.Toggle(700,x,y));

    static async Task DoorApproachChecks(){
        // Same authored synthetic fixture as DoorChecks: a supported own appearance makes MovementAvailable real.
        string fixtureRoot=Path.GetFullPath("scratch/issue-519/synthetic");
        mapFixture=Path.Combine(fixtureRoot,"assets");MakeMapFixture();
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(fixtureRoot,"build","offline-checks")+Path.DirectorySeparatorChar);
        Check(LoginConnection.DoorClickRange==1,"WO§3 門距離門檻 1 格");

        // Within 1 tile (diagonal included): send at once, no walking.
        {
            var (c,sent,steps)=DoorApproachSeed(1,-1);using var scope=c;
            Check(c.World.IsDoor(700)&&c.MovementAvailable,"合成門與可移動狀態");
            Check(await c.ToggleDoor(700)&&steps.Count==0&&sent.Count==1&&IsToggle(sent[0],32601,32939),"1 格內（斜角）直接送 C41，不走");
            Check(c.DoorNotice.Contains("已送出"),"送出提示");
        }
        // Farther: approach to 1 tile, then send exactly one C41 with the door's coordinates.
        {
            var (c,sent,steps)=DoorApproachSeed(4,0);using var scope=c;
            Check(await c.ToggleDoor(700),"遠處門走近後送出");
            Check(steps.SequenceEqual([2,2,2])&&c.World.Actor!.X==32603&&sent.Count==1&&IsToggle(sent[0],32604,32940),"走 3 步到 1 格內後送一次 C41（門座標）");
            Check(sent.All(b=>b[0]==41)&&!c.Approach.Active,"走近期間不送其他封包、走近結束");
        }
        {
            var (c,sent,steps)=DoorApproachSeed(-3,-3);using var scope=c;
            Check(await c.ToggleDoor(700)&&steps.SequenceEqual([7,7])&&sent.Count==1&&IsToggle(sent[0],32597,32937),"斜向走近 2 步後送出");
        }
        // Through the pointer controller (PlayerPointer.ActAsync → ToggleDoor): same approach.
        foreach(bool talk in new[]{false,true}){
            var (c,sent,steps)=DoorApproachSeed(0,3);using var scope=c;
            var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(await pointer.ActAsync(700,talk)&&steps.SequenceEqual([4,4])&&sent.Count==1&&IsToggle(sent[0],32600,32943),"滑鼠點遠處門：走近後送一次 C41 "+talk);
        }

        // Own death (WO §1 byte_13CE3FD): no object click handled, nothing sent, no walking.
        foreach(int d in new[]{0,4}){
            var (c,sent,steps)=DoorApproachSeed(d,1);using var scope=c;Set(c.World,"OwnDead",true);
            Check(c.ObjectClicksBlocked&&!await c.ToggleDoor(700)&&sent.Count==0&&steps.Count==0&&c.DoorNotice.Contains("死亡"),"死亡時擋點門（距離 "+d+"）");
        }
        {
            var (c,sent,steps)=DoorApproachSeed(4,0);using var scope=c;Set(c.World,"OwnDead",true);
            var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(!await pointer.ActAsync(700,true)&&sent.Count==0&&steps.Count==0,"死亡時滑鼠點門不送不走");
        }

        // Invalidation during the approach stops it without sending or resending.
        foreach(string change in new[]{"death","removed","input","reselect","replaced-elsewhere","destroyed"}){
            var (c,sent,steps)=DoorApproachSeed(4,0,onStep:(w,n)=>{
                if(n!=1)return;
                switch(change){
                    case "death":Set(w.World,"OwnDead",true);break;
                    case "removed":Feed(w,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));break;
                    case "input":w.CancelMovementInput();break;
                    case "reselect":w.World.SelectTarget(null);break;
                    case "replaced-elsewhere":Feed(w,DoorPack(x:32610,y:32940));break;
                    case "destroyed":Feed(w,DoorAction(37));break;
                }
            });using var scope=c;
            c.World.SelectTarget(700);
            Check(!await c.ToggleDoor(700)&&steps.Count==1&&sent.Count==0&&!c.Approach.Active,"走近中途 "+change+" 即停止、不送");
            Check(c.DoorNotice.StartsWith("未送出"),"中途停止提示 "+change);
            await Task.Delay(20);Check(sent.Count==0,"中途停止後不補送 "+change);
        }
        // Unselected door: a target change does not matter, but the door identity still does.
        {
            var (c,sent,steps)=DoorApproachSeed(4,0,onStep:(w,n)=>{if(n==1)w.World.SelectTarget(null);});using var scope=c;
            Check(await c.ToggleDoor(700)&&sent.Count==1,"未選取的門不受選取變更影響");
        }
        // No bounded path: refuse with the approach reason.
        {
            var (c,sent,steps)=DoorApproachSeed(4,0,terrain:(_,_,_)=>false);using var scope=c;
            Check(!await c.ToggleDoor(700)&&steps.Count==0&&sent.Count==0&&c.DoorNotice.Contains("路徑"),"無路徑時不送並提示");
        }
        // Arrival re-check: the C41 sender fence still rejects a door that changed in the send queue.
        {
            var (c,sent,steps)=DoorApproachSeed(2,0);using var scope=c;
            Sender(c,(b,v,a)=>{Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
            Check(!await c.ToggleDoor(700)&&steps.Count==1&&sent.Count==0,"到達後送出佇列中門消失：不送");
        }
        labels.Add("T213 門自動走近：WO§3 1 格內才送 C41、遠處先走近、死亡擋物件點擊、中途失效不送");
    }
}
