using System.Reflection;
using System.Text.Json;
namespace P06;

// R8-C（T358／#822，FU8§3）聊天指令 `/duel`、`/決鬥` 的離線測試：合成封包、記憶體內假傳送端，
// 不開遊戲、網路、視窗，不讀客戶端素材。由 --module-tests 執行（名稱 duel-command）。
static class DuelCommandTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static byte[] DuelAsk(string peer)=>P([219],Le16(0),Le32(0),Le16(630),Str(peer));
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    /// <summary>聊天送出後有 1 秒防連點；測試逐條送，所以每次先歸零。</summary>
    static bool Say(LoginConnection c,string text){
        Set(c,"playerChatNextAt",0L);var s=c.PlayerChatSnapshot();
        return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor).GetAwaiter().GetResult();
    }
    static readonly byte[] C5=LoginPackets.Pad([5]);

    [WorldModuleTest("duel-command")]
    static void Run(WorldModuleTestContext t){
        Parse(t);ChatGate(t);StateUntouchedWhileInvited(t);Texts(t);
    }

    // ① 指令字：英文 duel（不分大小寫，推測）＋字串表 644「決鬥」；以第一個空白切開，後面的文字不讀。
    static void Parse(WorldModuleTestContext t){
        foreach(var input in new[]{"/duel","/DUEL","/Duel","/決鬥","/duel 甲","/決鬥  乙 ","/duel\t甲"})
            t.Check(DuelChatCommand.Encode(input) is {} p&&p.SequenceEqual(C5),"決鬥指令 "+input);
        foreach(var other in new[]{"/duelx","/duels","duel","決鬥","/ duel","/決鬥者","/fight","","/","你好 /duel"})
            t.Check(!DuelChatCommand.Is(other)&&DuelChatCommand.Encode(other)==null,"不是決鬥指令："+other);
        t.Check(DuelPackets.Invite().SequenceEqual(C5),"C5 無欄位（3.81 C_Fight）");
        t.Equal("指令 · 決鬥",PlayerChatProtocol.Describe("/決鬥"),"聊天列說明");
        t.Equal("指令 · 決鬥",PlayerChatProtocol.Describe("/duel"),"聊天列說明（英文）");
        t.Check(PlayerChatProtocol.Valid("/duel 甲"),"帶參數仍是有效輸入");
        // 其他指令照舊。
        t.Check(ClientChatCommands.Parse("/duel")==null,"不進 ClientChatCommands 表");
        t.Equal(null,PlayerQueryPackets.Encode("/duel",Me,null),"不是查詢指令");
    }

    // ② 走聊天列的同一個送出閘門：送 C5、不送一般聊天；沒有決鬥流程時本機狀態改成邀請中。
    static void ChatGate(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            t.Equal("idle",c.World.DuelSnapshot.Phase,"初始 idle");
            t.Check(Say(c,"/duel")&&sent.Count==1&&sent[0].SequenceEqual(C5),"/duel 送 C5");
            t.Equal(DuelChatCommand.SentNotice,c.PlayerChatSnapshot().Notice,"送出提示");
            t.Equal("inviting",c.World.DuelSnapshot.Phase,"送出後 inviting");
            t.Check(Say(c,"/決鬥")&&sent.Count==2&&sent[1].SequenceEqual(C5),"/決鬥 再送一次（原版不檢查狀態）");
            t.Equal("inviting",c.World.DuelSnapshot.Phase,"等待回覆時狀態不動");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            t.Check(Say(c,"/duelx")&&sent.Count==1&&sent[0][0]==136,"/duelx 照一般聊天送出");
            t.Equal("idle",c.World.DuelSnapshot.Phase,"一般聊天不改決鬥狀態");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            var s=c.PlayerChatSnapshot();
            t.Check(!c.SendPlayerChat("/duel",s.Generation+1,s.WorldEpoch,s.Actor).GetAwaiter().GetResult()&&sent.Count==0,"過期的聊天戳記不送");
            t.Equal("idle",c.World.DuelSnapshot.Phase,"沒送出時狀態不動");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            Set(c,"State",LoginState.Faulted);
            t.Check(!Say(c,"/duel")&&sent.Count==0,"不在世界中不送");
        }
    }

    // ③ 正被邀請（S219/630）時打 /duel：封包照送，但不蓋掉詢問槽正在回答的邀請狀態。
    static void StateUntouchedWhileInvited(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            c.World.Receive(DuelAsk("甲"),"offline");
            var before=c.World.DuelSnapshot;
            t.Check(before is {Phase:"invited",Peer:"甲"},"收到 630 邀請",before);
            t.Check(Say(c,"/duel")&&sent.Count==1&&sent[0].SequenceEqual(C5),"被邀請時 /duel 照送 C5");
            var after=c.World.DuelSnapshot;
            t.Check(after is {Phase:"invited",Peer:"甲"}&&after.Revision==before.Revision,"邀請狀態不變",after);
        }
    }

    // ④ 過時提示已改：原版不另畫「決鬥對手」標記（AN#14、FU8§3）。
    static void Texts(WorldModuleTestContext t){
        using var c=new LoginConnection();
        // 讀欄位本身：DuelSnapshot 在世界紀元不同時會換成「世界已更新」提示。
        var initial=(DuelView)typeof(WorldSession).GetField("duel",F)!.GetValue(c.World)!;
        t.Check(initial.Notice.Contains("/duel")&&initial.Notice.Contains("/決鬥"),"初始提示寫出 /duel、/決鬥 入口",initial.Notice);
        // S250/5 開始決鬥的提示不再說有「決鬥對手」標記。
        var sent=new List<byte[]>();
        using var s=Seed(sent);
        s.World.Receive(P([250,5],Le32(456),Le32(Me)),"offline");
        var view=s.World.DuelSnapshot;
        t.Check(view.Phase=="active"&&view.OpponentId==456u,"S250/5 開始決鬥",view);
        t.Check(!view.Notice.Contains("「決鬥對手」")&&view.Notice.Contains("直接攻擊"),"開始決鬥提示已更新",view.Notice);
    }
}
