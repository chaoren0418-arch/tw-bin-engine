namespace P06;

// R11-A（T415／#957，gap-matrix 15.13.5–15.13.6）離線測試：合成 S87、記憶體內假傳送端；不開遊戲、網路、視窗，不讀客戶端素材。
// 涵蓋：① 組隊選對象（`/invite` 不帶名稱）與通用選對象也出瞄準游標 8（FU13 M35「回呼不為空」），結束後回 3；
// ② 通用選對象入口 LoginConnection.BeginObjectPick：點物件交給回呼、點地面取消、攻擊路徑也是選對象、交談不走、
//    同時只有一個回呼、換世界失效。施法選目標的 8／19 已由 spell-cursor、combat-queue 測試涵蓋，這裡只測不受影響的部分。
// 由 --module-tests 自動發現執行（名稱 target-pick-cursor）。
static class TargetPickCursorTests {
    const uint Own=123,Peer=456,Npc=458;
    const ushort X=32600,Y=32940;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    /// <summary>S87 物件（同 PartyInvitePickTests 的形狀）：flags 4＝玩家。</summary>
    static byte[] Appear(uint id,ushort x,ushort y,string name,byte flags=0)=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[flags],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();

    static LoginConnection World(out List<byte[]> sent){
        var c=new LoginConnection();sent=c.SeedPointerFlow(Own,X,Y);var w=c.World;
        w.Receive(Appear(Peer,(ushort)(X+5),Y,"甲",4),"target-pick-cursor");
        w.Receive(Appear(Npc,(ushort)(X+2),Y,"怪"),"target-pick-cursor");
        return c;
    }
    static PlayerPointerController Pointer(LoginConnection c)=>new(c,()=>true,()=>false,_=>true);

    [WorldModuleTest("target-pick-cursor")]
    static void Run(WorldModuleTestContext t){WorldObjectDrag.Clear();PartyCursor(t);ObjectPickFlow(t);SingleSlot(t);Stale(t);WorldObjectDrag.Clear();}

    // ① 組隊選對象：游標 8（不看射程）；結束後回 3。
    static void PartyCursor(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;var p=Pointer(c);
        t.Check(w.Ready,"seed: world ready");
        t.Equal(GameCursors.Hand,TargetPickCursor.Index(c,null),"不在選目標：手形 3");
        t.Equal(GameCursors.Hand,TargetPickCursor.Index(c,Peer),"不在選目標、滑鼠在人上：仍是 3（攻擊游標另由 CanAttackDirectly 決定）");
        w.BeginPartyInvitePick();
        t.Check(c.TargetPicking&&!w.SpellTargetPicking,"組隊選對象算選目標模式（回呼不為空）");
        t.Equal(GameCursors.Aim,TargetPickCursor.Index(c,null),"組隊選對象、滑鼠下沒有對象：8");
        t.Equal(GameCursors.Aim,TargetPickCursor.Index(c,Peer),"組隊選對象、滑鼠在玩家上：8");
        t.Equal(GameCursors.Aim,TargetPickCursor.Index(c,Npc),"組隊選對象、滑鼠在非玩家上：8（不是 type 4，不出 19）");
        t.Check(!p.Select(Peer)&&Wait(p.LastPartyInvitePick!)&&sent.Count==1,"點人送出邀請");
        t.Check(!c.TargetPicking,"選完：離開模式");
        t.Equal(GameCursors.Hand,TargetPickCursor.Index(c,Peer),"選完：回手形 3（sub_F68E60）");
        w.BeginPartyInvitePick();p.MoveTo(new Point(X+1,Y));
        t.Equal(GameCursors.Hand,TargetPickCursor.Index(c,null),"點地面取消：回 3");
    }

    // ② 通用選對象：點物件交給回呼（不改選取目標、不攻擊）；攻擊路徑也是選對象；交談不走；點地面取消並通知。
    static void ObjectPickFlow(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;var p=Pointer(c);
        var picked=new List<uint>();int cancelled=0;
        void Begin(bool result=true)=>c.BeginObjectPick("加入好友",id=>{picked.Add(id);return Task.FromResult(result);},()=>cancelled++);
        Begin();
        t.Check(c.ObjectPicking&&p.ObjectPicking&&c.TargetPicking&&c.ObjectPickName=="加入好友","進入通用選對象");
        t.Equal(GameCursors.Aim,TargetPickCursor.Index(c,Peer),"通用選對象：游標 8");
        t.Check(!p.Select(Peer)&&p.LastObjectPick!=null&&Wait(p.LastObjectPick!),"點人：Select 回 false，交給回呼，回呼回 true");
        t.Check(picked.SequenceEqual([Peer]),"回呼拿到點中的 id",picked);
        t.Check(!c.ObjectPicking&&w.TargetId==null&&cancelled==0,"點中後結束模式；不改選取目標；不算取消");
        t.Equal(PlayerPointerController.ObjectPickSentNotice("加入好友"),p.Notice,"提示：已送出");
        t.Check(sent.Count==0,"入口本身不送任何封包",sent.Select(Convert.ToHexString));
        t.Equal(GameCursors.Hand,TargetPickCursor.Index(c,Peer),"選完回 3");

        Begin(false);
        t.Check(!p.Select(Npc)&&!Wait(p.LastObjectPick!)&&picked.Count==2&&picked[1]==Npc&&!c.ObjectPicking,"回呼拒絕：回 false、仍結束模式");
        t.Equal(PlayerPointerController.ObjectPickRefusedNotice("加入好友"),p.Notice,"提示：未送出");

        Begin();
        t.Check(!Wait(p.ActAsync(Peer,false))&&picked.Count==3&&picked[2]==Peer&&!c.ObjectPicking,"攻擊路徑（雙擊／Ctrl）＝選對象，回 false");
        t.Check(!sent.Any(b=>b.Length>0&&b[0] is 229 or 123),"沒有送攻擊（C229／C123）");

        Begin();var last=p.LastObjectPick;
        try{Wait(p.ActAsync(Npc,true));}catch(Exception){/* 交談路徑可能需要素材或伺服器狀態；這裡只看有沒有走選對象 */}
        t.Check(ReferenceEquals(last,p.LastObjectPick)&&c.ObjectPicking&&picked.Count==3,"右鍵交談不走選對象，模式保留");

        t.Check(!p.MoveTo(new Point(X+1,Y))&&!c.ObjectPicking&&p.RemainingSteps==0&&cancelled==1,"點地面＝取消、不走路、通知 Cancelled");
        t.Equal(PlayerPointerController.ObjectPickCancelNotice,p.Notice,"取消提示");
        t.Check(!c.CancelObjectPick()&&cancelled==1,"沒有回呼時取消回 false、不重複通知");

        var last2=p.LastObjectPick;
        try{p.Select(Peer);}catch(Exception){/* 一般選取會讀外觀素材（CI 沒有），這裡只看有沒有走選對象 */}
        t.Check(ReferenceEquals(last2,p.LastObjectPick)&&picked.Count==3,"不在模式中：Select 不呼叫回呼");
    }

    // 同一時間只有一個回呼：開通用選對象會結束組隊選對象；之後再開組隊選對象，點人走組隊、通用的丟掉（不通知）。
    static void SingleSlot(WorldModuleTestContext t){
        using var c=World(out var sent);var w=c.World;var p=Pointer(c);
        int calls=0,cancelled=0;
        w.BeginPartyInvitePick();
        c.BeginObjectPick("拒絕",_=>{calls++;return Task.FromResult(true);},()=>cancelled++);
        t.Check(!w.PartyInvitePicking&&c.ObjectPicking,"開通用選對象：組隊選對象結束");
        c.BeginObjectPick("聊天組隊",_=>{calls+=10;return Task.FromResult(true);});
        t.Check(cancelled==1&&c.ObjectPickName=="聊天組隊","開新的回呼：舊的收到取消");
        w.BeginPartyInvitePick();
        t.Check(!p.Select(Peer)&&Wait(p.LastPartyInvitePick!)&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Invite(Peer,0)),"組隊選對象優先：點人送 C230 type 0",sent.Select(Convert.ToHexString));
        t.Check(!c.ObjectPicking&&calls==0&&cancelled==1,"組隊選中後，通用回呼丟掉、不呼叫、不通知");
        t.Check(!c.TargetPicking,"全部結束");
    }

    // 換世界（WorldEpoch）後失效：不算選目標、不呼叫回呼、取消也不通知。
    static void Stale(WorldModuleTestContext t){
        using var c=World(out _);var w=c.World;
        int calls=0,cancelled=0;
        c.BeginObjectPick("加入好友",_=>{calls++;return Task.FromResult(true);},()=>cancelled++);
        w.Reset();
        t.Check(!c.ObjectPicking&&!c.TargetPicking&&c.ObjectPickName==null,"世界重置後失效");
        t.Equal(GameCursors.Hand,TargetPickCursor.Index(c,null),"失效後游標 3");
        t.Check(!c.CancelObjectPick()&&cancelled==0&&calls==0,"過期的只清掉、不通知");
        bool threw=false;try{c.BeginObjectPick("x",null!);}catch(ArgumentNullException){threw=true;}
        t.Check(threw,"回呼不可為 null");
    }
}
