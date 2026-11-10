namespace P06;

// R10-G（T391／#906，gap-matrix 1:92，AN§1）離線測試：合成封包、記憶體送出閘門、假時鐘；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
// 原版 0xDF5401–0xDF5416 在 sub_A4C270(0)（按住輸入的延續）前檢查 byte_153CE69／byte_153CE6A：表情保留只暫停按住的移動。
// 涵蓋：保留不再進 MoveLocked／MovementAvailable（攻擊、施法、門、傳送點共用的閘門）；按住的步行在 RequestMovement 等保留結束後
// 繼續、不清輸入；同一段等待共用一個工作；放開按鍵或換世界就丟掉；轉向不等待；S241 仍是 MoveLocked。
static class EmoteHoldScopeTests {
    const ushort HoldGfx=6080; // horse helm form: menu item 1 is immediate with hold (TitleAndEmotePackets.Plan)
    static LoginConnection Seed(List<byte[]> sent,ushort gfx=HoldGfx){var c=new LoginConnection();c.SeedInputLineTest(sent,false,gfx,4);return c;}
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();

    [WorldModuleTest("emote-hold-scope")]
    static void Run(WorldModuleTestContext t){Scope(t);SettleScope(t);HeldStepWaits(t);SharedWaitAndCancel(t);WorldChangeDrops(t);TurnDoesNotWait(t);ServerLockUnchanged(t);}

    static void Scope(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
        bool movement=c.MovementAvailable,combat=c.CombatAvailable;string reason=c.MovementBlockReason;
        t.Check(Wait(c.SendEmote(c.CaptureTitleEmote(),1))&&TitleAndEmotePackets.Plan(HoldGfx,1,true).Hold,"hold emote sent");
        t.Check(c.World.EmoteHeld&&c.HeldMovementPaused,"byte_153CE6A set: held movement paused");
        t.Check(!c.World.MoveLocked&&!c.World.ServerMoveLocked,"hold is not MoveLocked");
        t.Check(c.MovementAvailable==movement&&c.CombatAvailable==combat,"hold does not change MovementAvailable／CombatAvailable (attack, magic, doors, portals)",new{movement,combat});
        t.Equal(reason,c.MovementBlockReason,"hold does not add a block reason");
        now=1000+TitleAndEmotePackets.HoldMs;t.Check(!c.HeldMovementPaused,"pause over after 1234 ms");
    }

    // byte_153CE69 (700 ms after an immediate emote ends) is the same narrow pause.
    static void SettleScope(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent,61);long now=1000;c.ActionClock=()=>now;
        Wait(c.SendEmote(c.CaptureTitleEmote(),1));now=1500;c.World.EndEmote(InputLineFollowupTests.Own);
        t.Check(c.World.EmoteSettling&&c.HeldMovementPaused&&!c.World.MoveLocked,"settle tail pauses held movement, not MoveLocked");
        now=2200;t.Check(!c.HeldMovementPaused,"settle tail over at 700 ms");
    }

    // The held step waits (no packet, no input clear) and resumes into SendMovement when the hold ends.
    static void HeldStepWaits(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
        Wait(c.SendEmote(c.CaptureTitleEmote(),1));int before=sent.Count,waits=0;
        c.EmotePauseWait=()=>{waits++;t.Check(sent.Count==before,"no step packet while paused");now+=100;return Task.CompletedTask;};
        var r=Wait(c.RequestMovement(2,true,false));
        t.Check(waits>=12&&now>=1000+TitleAndEmotePackets.HoldMs,"held step waited for the whole hold",new{waits,now});
        // The seed is not renderable (map 4, no cache), so the resumed SendMovement refuses with its own reason: proves it resumed.
        t.Check(!r.Sent&&r.Notice==c.MovementBlockReason&&r.Notice.Length>0,"after the hold the step goes through SendMovement",r);
        t.Check(sent.Count==before,"nothing sent by the paused step",sent.Count);
    }

    static void SharedWaitAndCancel(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
        Wait(c.SendEmote(c.CaptureTitleEmote(),1));
        var gate=new TaskCompletionSource();int waits=0;c.EmotePauseWait=()=>{waits++;return gate.Task;};
        var a=c.RequestMovement(2,true,false);var b=c.RequestMovement(4,true,false);
        t.Check(!a.IsCompleted&&ReferenceEquals(a,b)&&waits==1,"repeated held input shares one wait",new{a.IsCompleted,waits});
        t.Equal(LoginConnection.EmotePauseNotice,c.World.MotionNotice,"pause notice shown");
        c.CancelMovementInput();gate.SetResult();var r=Wait(a);
        t.Check(!r.Sent&&r.Notice.Contains("已取消"),"key release drops the paused step",r);
        var gate2=new TaskCompletionSource();c.EmotePauseWait=()=>gate2.Task;
        var d=c.RequestMovement(2,true,false);
        t.Check(!ReferenceEquals(a,d)&&!d.IsCompleted,"a new held input after a finished wait starts a new one");
        now=1000+TitleAndEmotePackets.HoldMs;gate2.SetResult();
        t.Check(!Wait(d).Sent&&sent.Count==1,"resumed step refused by the non-renderable seed; only the emote was sent",sent.Count);
    }

    static void WorldChangeDrops(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
        Wait(c.SendEmote(c.CaptureTitleEmote(),1));
        var gate=new TaskCompletionSource();c.EmotePauseWait=()=>gate.Task;
        var a=c.RequestMovement(2,true,false);int epoch=c.World.WorldEpoch;
        c.World.Reset();t.Check(c.World.WorldEpoch!=epoch,"Reset starts a new world");
        gate.SetResult();var r=Wait(a);
        t.Check(!r.Sent&&r.Notice.Contains("不沿用表情暫停"),"world change drops the paused step",r);
    }

    // Only step continuation waits; a turn (and every non-movement action) is not routed through the pause.
    static void TurnDoesNotWait(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
        Wait(c.SendEmote(c.CaptureTitleEmote(),1));int waits=0;c.EmotePauseWait=()=>{waits++;return new TaskCompletionSource().Task;};
        var turn=c.RequestMovement(2,false,false);
        t.Check(turn.IsCompleted&&waits==0&&Wait(turn).Notice!=LoginConnection.EmotePauseNotice,"turn is not paused by the hold",new{turn.IsCompleted,waits});
        var walking=c.RequestMovement(2,true,true);
        t.Check(walking.IsCompleted&&waits==0,"local walk refusal keeps its own path");
    }

    static void ServerLockUnchanged(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        c.World.Receive(LoginPackets.Pad([241,0]),"offline");
        t.Check(c.World.ServerMoveLocked&&c.World.MoveLocked&&!c.MovementAvailable&&!c.HeldMovementPaused,"S241 still locks through MoveLocked");
    }
}
