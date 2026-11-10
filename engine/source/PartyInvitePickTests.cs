using System.Buffers.Binary;
using P06.NativeUi;
namespace P06;

// R10-D（T388／#903，FU9§9.1、gap-matrix 15.12.6）離線測試：合成 S87、記憶體內假傳送端；不開遊戲、網路、視窗，不讀客戶端素材。
// 涵蓋：`/invite` 選目標模式中滑鼠點人（PlayerPointerController.Select／ActAsync）→ PickPartyInviteTarget 送 C230 type 0；
// 點地面取消不走路；拖人到隊伍窗（WorldObjectDrag ＋ PartyPanel 放開）→ PartyAction("invite") → SendParty C230 type 0。
// 由 --module-tests 執行（名稱 party-invite-pick）。
static class PartyInvitePickTests {
    const uint Own=123,Peer=456,Far=457,Npc=458;
    const ushort X=32600,Y=32940;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    /// <summary>S87 物件（同 ChatCommandR9cTests 的形狀）：flags 4＝玩家。</summary>
    static byte[] Appear(uint id,ushort x,ushort y,string name,byte flags=0)=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[flags],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static string Hex(IEnumerable<byte[]> s)=>string.Join(",",s.Select(Convert.ToHexString));
    static uint Target(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(2));

    static LoginConnection World(out List<byte[]> sent){
        var c=new LoginConnection();sent=c.SeedPointerFlow(Own,X,Y);var w=c.World;
        w.Receive(Appear(Peer,(ushort)(X+5),Y,"甲",4),"party-invite-pick");
        w.Receive(Appear(Far,(ushort)(X+31),Y,"遠",4),"party-invite-pick");
        w.Receive(Appear(Npc,(ushort)(X+2),Y,"怪"),"party-invite-pick");
        return c;
    }
    static PlayerPointerController Pointer(LoginConnection c)=>new(c,()=>true,()=>false,_=>true);

    [WorldModuleTest("party-invite-pick")]
    static void Run(WorldModuleTestContext t){WorldObjectDrag.Clear();ClickPick(t);GroundCancel(t);AttackPath(t);DragCandidate(t);Drop(t);WorldObjectDrag.Clear();}

    // 1. 選目標模式中左鍵點人 → C230 type 0 D id；不改選取目標；不在模式中不送；非玩家／太遠不送但結束模式。
    static void ClickPick(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;var p=Pointer(c);
        t.Check(w.Ready,"seed: world ready");
        w.BeginPartyInvitePick();
        t.Check(p.PartyInvitePicking,"進入選邀請對象模式");
        t.Check(!p.Select(Peer)&&p.LastPartyInvitePick!=null,"點人：Select 回 false（不是選取），交給邀請流程");
        t.Check(Wait(p.LastPartyInvitePick!)&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Invite(Peer,0)),"送 C230 type 0 D id",Hex(sent));
        t.Equal("E600C80100000000",Convert.ToHexString(sent[0]),"C230 00 D(456) little-endian，補到 8 byte");
        t.Equal(Peer,Target(sent[0]),"對象 id");
        t.Check(!w.PartyInvitePicking&&w.TargetId==null,"送出後結束模式；不改選取目標");
        t.Equal(PlayerPointerController.PartyInviteSentNotice,p.Notice,"滑鼠提示：已送出");
        t.Check(!sent.Any(b=>b[0] is 229 or 123),"沒有送攻擊");
        w.BeginPartyInvitePick();
        t.Check(!p.Select(Far)&&!Wait(p.LastPartyInvitePick!)&&sent.Count==1&&!w.PartyInvitePicking,"31 格外：不送、結束模式");
        t.Equal(PlayerPointerController.PartyInviteRefusedNotice,p.Notice,"滑鼠提示：未送出");
        w.BeginPartyInvitePick();
        t.Check(!p.Select(Npc)&&!Wait(p.LastPartyInvitePick!)&&sent.Count==1&&!w.PartyInvitePicking,"非玩家：不送、結束模式");
        w.BeginPartyInvitePick();
        t.Check(!p.Select(Own)&&!Wait(p.LastPartyInvitePick!)&&sent.Count==1,"自己：不送");
        var last=p.LastPartyInvitePick;
        try{p.Select(Peer);}catch(Exception){/* 一般選取會讀外觀素材（CI 沒有），這裡只看有沒有走邀請 */}
        t.Check(ReferenceEquals(last,p.LastPartyInvitePick)&&sent.Count==1,"不在模式中：Select 不送邀請");
    }

    // 2. 選目標模式中點地面：取消模式，不走路、不送。
    static void GroundCancel(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;var p=Pointer(c);
        w.BeginPartyInvitePick();
        t.Check(!p.MoveTo(new Point(X+1,Y))&&!w.PartyInvitePicking&&p.RemainingSteps==0&&sent.Count==0,"點地面＝取消、不走路、不送");
        t.Equal(PlayerPointerController.PartyInviteCancelNotice,p.Notice,"取消提示");
    }

    // 3. 攻擊路徑（雙擊／Ctrl）在模式中也是選對象；回 false（UI 不記成 attack）；交談（右鍵）不走邀請。
    static void AttackPath(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;var p=Pointer(c);
        w.BeginPartyInvitePick();
        t.Check(!Wait(p.ActAsync(Peer,false)),"ActAsync 回 false");
        t.Check(p.LastPartyInvitePick is {IsCompletedSuccessfully:true,Result:true}&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Invite(Peer,0))&&!w.PartyInvitePicking,"雙擊玩家 → 邀請，不攻擊",Hex(sent));
        t.Check(!sent.Any(b=>b[0] is 229 or 123),"沒有 C229／C123");
        w.BeginPartyInvitePick();var last=p.LastPartyInvitePick;
        try{Wait(p.ActAsync(Npc,true));}catch(Exception){}
        t.Check(ReferenceEquals(last,p.LastPartyInvitePick)&&w.PartyInvitePicking,"右鍵交談不走邀請，模式保留");
    }

    // 4. 拖曳來源：只認看得到、活著的別的玩家；Cancel 清掉。
    static void DragCandidate(WorldModuleTestContext t){
        using var c=World(out _);var w=c.World;
        t.Check(WorldObjectDrag.PlayerCandidate(w,Peer),"玩家可拖");
        t.Check(!WorldObjectDrag.PlayerCandidate(w,Npc),"非玩家不可拖");
        t.Check(!WorldObjectDrag.PlayerCandidate(w,Own),"自己不可拖");
        t.Check(!WorldObjectDrag.PlayerCandidate(w,999),"不存在不可拖");
        WorldObjectDrag.Begin(Peer);Pointer(c).Cancel("測試");
        t.Equal<uint?>(null,WorldObjectDrag.Source,"任何新的場景點擊／停止都清掉拖曳來源");
        WorldObjectDrag.Begin(0);t.Equal<uint?>(null,WorldObjectDrag.Source,"id 0 不記");
    }

    static UiMouseEvent M(UiMouseKind kind)=>new(kind,new(500,500),MouseButtons.Left,1,0,Keys.None);

    // 5. 拖人到隊伍窗：別處按下、在隊伍分頁放開 → PartyAction("invite") → SendParty C230 type 0；自己按下的放開、沒來源、
    //    角色分頁、不可邀請、沒有隊伍指令都不送。
    static void Drop(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;
        var log=new List<string>();
        var cmd=new UiMacroCommands(new UiPartyCommands(new UiNullCommands(),(command,v,n)=>{log.Add(command);Wait(c.SendParty(command,v,n,w.TargetId??0,0));}),(_,_)=>{});   // product nesting
        var panel=new PartyPanel();panel.SetPage(PartyPanel.Page.Party);
        void Sync(){panel.Update(new UiPartyGameState(new UiStaticGameState(InWorld:true),c.PartySnapshot()));}
        // 場景按下選中玩家（PlayerPointerController.Select 的結果；一般選取需要外觀素材，這裡直接設）。
        w.SelectTarget(Peer);WorldObjectDrag.Begin(Peer);Sync();
        t.Check(panel.Mouse(M(UiMouseKind.Up),cmd)&&log.SequenceEqual(["invite"]),"別處按下、隊伍窗放開 → invite",log);
        t.Check(sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Invite(Peer,0)),"C230 type 0 D(拖的玩家)",Hex(sent));
        t.Equal<uint?>(null,WorldObjectDrag.Source,"放開後來源取走");
        Sync();
        t.Check(panel.Mouse(M(UiMouseKind.Up),cmd)&&log.Count==1,"沒有來源的放開不送");
        WorldObjectDrag.Begin(Peer);panel.Mouse(M(UiMouseKind.Down),cmd);
        t.Equal<uint?>(null,WorldObjectDrag.Source,"在隊伍窗按下清掉來源");
        panel.Mouse(M(UiMouseKind.Up),cmd);t.Check(log.Count==1,"自己按下的放開不送");
        panel.Mouse(M(UiMouseKind.Down),cmd);WorldObjectDrag.Begin(Peer);panel.Mouse(M(UiMouseKind.Up),cmd);
        t.Check(log.Count==1&&WorldObjectDrag.Source==Peer,"本窗按下後的放開仍是自己的（來源不取走）");
        WorldObjectDrag.Clear();
        panel.SetPage(PartyPanel.Page.Character);WorldObjectDrag.Begin(Peer);panel.Mouse(M(UiMouseKind.Up),cmd);
        t.Check(log.Count==1&&WorldObjectDrag.Source==null,"角色分頁：吃掉放開、不送");
        panel.SetPage(PartyPanel.Page.Party);
        var shown=c.PartySnapshot();
        panel.Update(new UiPartyGameState(new UiStaticGameState(InWorld:true),shown with{CanInvite=false}));
        t.Check(!panel.DropInvite(cmd)&&log.Count==1,"不可邀請（隊伍不是自己帶）不送");
        panel.Update(new UiPartyGameState(new UiStaticGameState(InWorld:true),shown with{Available=false}));
        t.Check(!panel.DropInvite(cmd)&&log.Count==1,"忙碌不送");
        Sync();
        t.Check(!panel.DropInvite(new UiNullCommands())&&log.Count==1,"沒有隊伍指令不送");
        // SendParty 再擋：選中的不是玩家 → 不送。
        w.SelectTarget(Npc);Sync();WorldObjectDrag.Begin(Npc);
        panel.Mouse(M(UiMouseKind.Up),cmd);
        t.Check(log.Count==2&&sent.Count==1,"對象不是玩家：交出 invite 但 SendParty 不送",Hex(sent));
    }
}
