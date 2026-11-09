using P06;

// T214／M7-N：幽靈本機限制（WO §4）。合成封包、記憶體 sender，不連網、不開視窗、不讀遊戲素材。
partial class Checks {
    [CheckGroup("ghost-limits"),CheckGroup("ghost-mode",Order=2),CheckGroup("map-continuity",Order=250),CheckGroup(CheckGroups.Default,Order=480)]
    static async Task GhostLimitsChecks(){
        // 1. 鍵盤 M／U／G：幽靈中不送任何封包，HUD 顯示原因並清掉按住的鍵。
        foreach(var key in new[]{Keys.M,Keys.U,Keys.G}){
            using var c=GhostSeed();var form=KeyboardForm(c);int sends=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(c.GhostLocalBlocked,"ghost seed blocks local actions "+key);
            Call(form,"OnlineDown",key);
            Check(sends==0,"ghost "+key+" sends no packet");
            CheckKeyboardRefusal(form,"幽靈模式","ghost "+key);
        }
        // 2. Web 面板的攻擊指令：幽靈中不送。
        //    非幽靈的對照組（實際送 C229）要讀外觀表（engine/assets），CI 沒有素材，只核對閘門旗標。
        using(var c=GhostSeed()){
            var form=KeyboardForm(c);int sends=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            // 幽靈閘門在第一個 await 之前返回；同步完成，避免在 WinForms 同步內容下等待沒人抽的訊息迴圈。
            var task=WebAttack(form);Check(task.IsCompletedSuccessfully,"ghost web attack returns synchronously");
            Check(sends==0,"web attack blocked in ghost");
        }
        using(var c=Seed())Check(!c.GhostLocalBlocked,"normal actor not ghost-limited");
        // 3. 移動不經幽靈閘門（閘門只套 M／U／G）。鍵盤移動要讀外觀表（engine/assets），CI 沒有素材，不在此跑。
        // 4. 幽靈結束（原服自身 S87 清 0x80）後立刻解除限制；限制只看當前狀態，不留本機旗標。
        using(var c=GhostSeed()){
            Feed(c,GhostPack(4));
            Check(!c.GhostLocalBlocked,"server exit clears local limit");
            Feed(c,GhostPack());Check(c.GhostLocalBlocked,"re-entry restores local limit");
            c.Cancel();Check(!c.GhostLocalBlocked,"cancelled session not ghost-limited");
        }
        // 5. HUD：幽靈快照標為忙碌（F5–F12 快捷槽與「使用一次」不送），不再有離開按鈕。
        using(var c=GhostSeed()){
            var stamp=PlayerHudActions.Stamp(c);
            var hud=new PlayerHudSnapshot(stamp,"offline","",100,100,100,100,"可操作","舊訊息",false,"",false,[],[]);
            var ghost=GhostHudApply(hud,c.GhostModeSnapshot());
            Check(ghost.Busy&&ghost.State.Contains("幽靈")&&ghost.Notice.StartsWith("幽靈模式")&&ghost.Notice.EndsWith("舊訊息"),"ghost HUD snapshot busy with notice");
            Check(ReferenceEquals(GhostHudApply(hud,null),hud),"no ghost view leaves HUD untouched");
            var stale=c.GhostModeSnapshot() with{Stamp=stamp with{WorldEpoch=stamp.WorldEpoch+1}};
            Check(ReferenceEquals(GhostHudApply(hud,stale),hud),"stale ghost view does not lock HUD");
            Feed(c,GhostPack(4));
            Check(ReferenceEquals(GhostHudApply(hud,c.GhostModeSnapshot()),hud),"non-ghost view leaves HUD untouched");
            var hudType=typeof(ClientForm).Assembly.GetType("P06.GhostModeHud")!;
            Check(hudType.IsAbstract&&hudType.IsSealed,"ghost HUD mount is stateless (no persistent exit button)");
            Check(typeof(ClientForm).GetMethod("ExitGhostFromHud",F)==null,"HUD exit entry removed");
        }
        // 6. 幽靈中 HUD 快捷路徑：ActivateSlot 看到 Busy 就不觸發。
        using(var c=GhostSeed()){
            var view=new PlayerHudView();
            try{
                var stamp=PlayerHudActions.Stamp(c);
                var entry=new PlayerHudEntry(new(PlayerHudKind.Item,900),"道具","",null,false,true);
                var state=GhostHudApply(new PlayerHudSnapshot(stamp,"offline","",100,100,100,100,"","",false,"",false,[entry],[]),c.GhostModeSnapshot());
                Set(view,"snapshot",state);
                var slots=(PlayerHudSlots)typeof(PlayerHudView).GetField("bindings",F)!.GetValue(view)!;
                slots.Observe(stamp);slots.Assign(0,entry.Binding);
                int fired=0;view.ActivateEntry+=(_,_)=>fired++;
                view.ActivateSlot(0);Check(fired==0,"ghost F5 slot does not activate");
                Set(view,"snapshot",state with{Busy=false});view.ActivateSlot(0);Check(fired==1,"control: same slot activates when not busy");
            }finally{view.Dispose();}
        }
        // 7. 自身外觀略過的判斷（呼叫點在 Polymorph.cs／Recovery.cs，尚未接上）：只對幽靈中的自己成立。
        using(var c=GhostSeed()){
            Check(Skip(c,123),"ghost self appearance change skipped");
            Check(!Skip(c,456),"other object appearance still applies in ghost");
            Feed(c,GhostPack(4));Check(!Skip(c,123),"non-ghost self appearance applies");
        }
        static bool Skip(LoginConnection c,uint id)=>(bool)typeof(WorldSession).GetMethod("GhostSkipsSelfAppearance",F)!.Invoke(c.World,[id])!;
    }
    static Task WebAttack(ClientForm form)=>(Task)typeof(ClientForm).GetMethod("OnlineWeb",F)!.Invoke(form,[1,"attack",0])!;
    // GhostModeHud 是產品內部型別（無 InternalsVisibleTo），以反射呼叫實際產品方法。
    static PlayerHudSnapshot GhostHudApply(PlayerHudSnapshot state,GhostModeView? view)=>
        (PlayerHudSnapshot)typeof(ClientForm).Assembly.GetType("P06.GhostModeHud")!.GetMethod("Apply",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)!.Invoke(null,[state,view])!;
}
