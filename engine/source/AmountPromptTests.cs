using System.Reflection;
using System.Text.Json;
namespace P06;

// M7-E（T207／#513）數量輸入詢問的離線測試：只用合成封包（欄位順序依 server-protocol.md 的 S_HowManyMake／S_ItemCount／
// S_ApplyAuction 與 C_Amount），以記憶體內的假傳送端驗證 C11，不開遊戲、網路、視窗或素材。
static class AmountPromptTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Npc=456,Far=789;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Input(uint id,int initial,int min,int max,string html,string cmd,params string[] data)=>
        P([AmountPromptProtocol.SInputAmount],Le32(id),Le32(0),Le32((uint)initial),Le32((uint)min),Le32((uint)max),Le16(0),Str(html),Str(cmd),
          data.Length>0?[..Le16((ushort)data.Length),..data.SelectMany(Str)]:[]);
    // S_HowManyMake(objId,max,htmlId)／S_ItemCount(objId,max,cmd)：D 0、D 0、D 0、D max、H 0、"request"、cmd。
    static byte[] HowManyMake(uint id,int max,string cmd)=>Input(id,0,0,max,"request",cmd);

    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Text(string s)=>Str(s);
    // S87 物件出現（與 demo-tests 的 Seed 相同欄位）：自己 123、NPC 456（相鄰一格）、遠方 789。
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),Text(id==Me?"offline":"target"),Text(""),[0],Le32(0),Text(""),Text(""),[0,100,0,0,0,255,255]);
    static LoginConnection Seed(out List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");c.World.Receive(Appear(Npc,32601),"offline");c.World.Receive(Appear(Far,32700),"offline");
        c.World.SelectTarget(Npc);
        var log=new List<byte[]>();sent=log;
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    // 有角色時 WorldSession.Snapshot() 會讀素材（PlayerProfiles），所以直接呼叫本模組的快照方法。
    static JsonElement Ext(WorldSession s)=>JsonSerializer.SerializeToElement(typeof(WorldSession).GetMethod("AmountPromptSnapshotModel",F)!.Invoke(s,null));
    static bool Send(LoginConnection c,long value)=>c.SendAmountReply(c.AmountPromptSnapshot(),value).GetAwaiter().GetResult();

    [WorldModuleTest("amount-prompt")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S136 由本模組接；快照與 reset 掛上；登錄表沒有錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==136&&e.Subtype==-1&&e.Method=="WorldSession.ReceiveAmountPrompt"),"S136 registered");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="amountPrompt"),"amountPrompt snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetAmountPrompt"),"amountPrompt reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. C11 編碼：C(11) D(objId) D(amount) C(0) S(cmd)，補到 8 的倍數。
        t.Equal("0BC801000005000000"+"00"+"6D616B653100",Convert.ToHexString(AmountPromptProtocol.Reply(Npc,5,"make1")),"C11 bytes");
        t.Equal(0,AmountPromptProtocol.Reply(Npc,1,"agapply 12").Length%8,"C11 padded");
        t.Throws<ArgumentException>(()=>AmountPromptProtocol.Reply(Npc,0,"make1"),"C11 rejects amount 0 (server drops ≤ 0)");
        t.Throws<ArgumentException>(()=>AmountPromptProtocol.Reply(Npc,1,""),"C11 rejects empty command");
        t.Throws<ArgumentException>(()=>AmountPromptProtocol.Reply(Npc,1,"a\nb"),"C11 rejects control characters");
        t.Throws<ArgumentException>(()=>AmountPromptProtocol.Reply(Npc,1,new string('x',201)),"C11 rejects overlong command");

        // 3. S136 解碼：三種伺服器建構法。
        var how=AmountPromptProtocol.Decode(HowManyMake(Npc,10,"make1"));
        t.Check(how is {ObjectId:Npc,Reserved:0,Initial:0,Min:0,Max:10,Flag:0,Html:"request",Command:"make1"}&&how.Data.Length==0,"S_HowManyMake decoded",how);
        var item=AmountPromptProtocol.Decode(Input(Npc,1,1,1,"y_item_03","upgrade1","長劍","+1"));
        t.Check(item is {Initial:1,Min:1,Max:1,Html:"y_item_03",Command:"upgrade1"}&&item.Data.SequenceEqual(["長劍","+1"]),"S_ItemCount with data decoded",item);
        var apply=AmountPromptProtocol.Decode(Input(Npc,100000,100000,2000000000,"agapply","agapply 3"));
        t.Check(apply is {Initial:100000,Min:100000,Max:2000000000,Html:"agapply",Command:"agapply 3"},"S_ApplyAuction decoded",apply);
        foreach(int extra in new[]{0,1,2,3,4,5,6,7}){
            string cmd=new('c',extra);
            t.Check(AmountPromptProtocol.Decode(Input(Npc,0,0,5,"request",cmd)).Command==cmd,$"padding variant {extra}");
        }
        // 格式錯誤一律丟 ProtocolException。
        t.Throws<ProtocolException>(()=>AmountPromptProtocol.Decode(P([135],Le32(Npc))),"wrong opcode");
        t.Throws<ProtocolException>(()=>AmountPromptProtocol.Decode(P([136],Le32(Npc),Le32(0))),"truncated");
        var dirty=HowManyMake(Npc,10,"make1");dirty[^1]=1;
        t.Throws<ProtocolException>(()=>AmountPromptProtocol.Decode(dirty),"nonzero padding");
        t.Throws<ProtocolException>(()=>AmountPromptProtocol.Decode([..HowManyMake(Npc,10,"make1"),0,0,0,0,0,0,0,0]),"extra zero block (data count 0)");
        t.Throws<ProtocolException>(()=>AmountPromptProtocol.Decode(P([136],Le32(Npc),Le32(0),Le32(0),Le32(0),Le32(5),Le16(0),Str("request"),Str("c"),Le16(3),Str("a"))),"data count larger than strings");

        // 沒有角色的 session（Snapshot() 不需素材）：S136 被本模組消化，不計入 unhandled。
        {
            var bare=new WorldSession();bare.Receive(HowManyMake(Npc,10,"make1"),"offline");
            var snap=WorldModuleTestContext.SnapshotJson(bare);
            t.Equal(0,snap.GetProperty("unhandled").TryGetProperty("0x88",out var u)?u.GetInt32():0,"S136 not counted unhandled");
            t.Equal(1,snap.GetProperty("extensions").GetProperty("amountPrompt").GetProperty("ignored").GetInt32(),"S136 without target ignored");
        }

        // 4. 對話中收到 S136：顯示在 NPC 對話面板（dialog 頁）並帶數字框資料。
        using(var c=Seed(out var sent)){
            var w=c.World;
            t.Check(w.NearbyTarget,"seed: NPC nearby");
            w.BeginTalk();
            w.Receive(HowManyMake(Npc,10,"make1"),"offline");
            t.Equal("dialog",w.InteractionKind,"S136 opens NPC dialog page");
            t.Equal<uint?>(Npc,w.InteractionNpc,"interaction stays on NPC");
            var o=w.AmountPromptCurrent;
            t.Check(o is {Lower:1,Max:10,Suggested:1,Answerable:true,Command:"make1",Html:"request"},"current prompt (min 0 raised to 1)",o);
            t.Check(w.InteractionPage?.Text.Contains("請在下方輸入數量")==true,"fallback page text without templates",w.InteractionPage?.Text);
            t.Check(w.InteractionNotice.Contains("可輸入 1～10"),"notice shows range",w.InteractionNotice);
            var ext=Ext(w);
            t.Equal(1,ext.GetProperty("received").GetInt32(),"snapshot received");
            t.Equal("make1",ext.GetProperty("current").GetProperty("command").GetString(),"snapshot current command");
            var view=c.AmountPromptSnapshot();
            t.Check(view.CanSend&&view.Prompt!=null,"view can send");

            // 範圍外不送。
            t.Check(!Send(c,11),"value above max not sent");
            t.Check(!Send(c,0),"value 0 not sent");
            t.Equal(0,sent.Count,"nothing sent for invalid values");
            // 範圍內送一次 C11。
            t.Check(Send(c,3),"valid value sent");
            t.Equal(1,sent.Count,"one C11");
            t.Equal(Convert.ToHexString(AmountPromptProtocol.Reply(Npc,3,"make1")),Convert.ToHexString(sent[0]),"C11 carries objId, amount, command");
            t.Check(w.AmountPromptCurrent==null,"prompt consumed after send");
            t.Equal("waiting",w.InteractionKind,"waits for server reply");
            t.Check(w.InteractionNotice.Contains("已送出數量 3"),"sent notice",w.InteractionNotice);
            t.Check(!c.SendAmountReply(view,4).GetAwaiter().GetResult(),"stale view cannot send twice");
            t.Equal(1,sent.Count,"still one C11");
            t.Equal(1,Ext(w).GetProperty("sent").GetInt32(),"snapshot sent");
        }

        // 5. 失效：關閉對話後不能送；換頁（新的 S136）後舊詢問失效。
        using(var c=Seed(out var sent)){
            var w=c.World;w.BeginTalk();
            w.Receive(HowManyMake(Npc,10,"make1"),"offline");
            var old=c.AmountPromptSnapshot();
            w.Receive(Input(Npc,5,2,8,"request","make2"),"offline");
            t.Check(w.AmountPromptCurrent is {Command:"make2",Lower:2,Max:8,Suggested:5},"second S136 replaces prompt");
            t.Check(!c.SendAmountReply(old,3).GetAwaiter().GetResult(),"replaced prompt cannot send");
            w.CloseInteraction();
            t.Check(w.AmountPromptCurrent==null,"closing dialog drops prompt");
            t.Check(!Send(c,3),"closed prompt cannot send");
            t.Equal(0,sent.Count,"nothing sent after close");
        }

        // 6. 沒開對話但目標就是此 NPC（例如公告板的 agapply）：開一個對話脈絡顯示；其他物件的 S136 忽略。
        using(var c=Seed(out var sent)){
            var w=c.World;
            w.Receive(Input(Far,0,0,5,"request","x"),"offline");
            t.Equal("closed",w.InteractionKind,"S136 from non-target ignored");
            t.Equal(1,Ext(w).GetProperty("ignored").GetInt32(),"ignored counted");
            w.Receive(Input(Npc,100000,100000,2000000000,"agapply","agapply 3"),"offline");
            t.Equal<uint?>(Npc,w.InteractionNpc,"target S136 opens context");
            t.Check(w.AmountPromptCurrent is {Lower:100000,Max:2000000000,Suggested:100000},"agapply range");
            t.Check(!Send(c,99999),"below auction minimum not sent");
            t.Check(!Send(c,2000000001),"above int range cap not sent");
            t.Check(Send(c,150000),"auction bid sent");
            t.Equal(Convert.ToHexString(AmountPromptProtocol.Reply(Npc,150000,"agapply 3")),Convert.ToHexString(sent.Single()),"agapply C11");
        }

        // 7. html 為空時關窗；上限 0 的詢問顯示但不能送；Reset 清空。
        using(var c=Seed(out var sent)){
            var w=c.World;w.BeginTalk();
            w.Receive(HowManyMake(Npc,0,"make1"),"offline");
            t.Check(w.AmountPromptCurrent is {Answerable:false},"max 0 prompt shown but not answerable");
            t.Check(!c.AmountPromptSnapshot().CanSend,"max 0 cannot send");
            t.Check(!Send(c,1),"max 0 send refused");
            w.Receive(Input(Npc,0,0,5,"","make1"),"offline");
            t.Equal("closed",w.InteractionKind,"empty html closes dialog");
            t.Check(w.AmountPromptCurrent==null,"no prompt after close");
            w.BeginTalk();w.Receive(Input(Npc,0,1,3,"y_item_03","upgrade1","長劍"),"offline");
            t.Check(Ext(w).GetProperty("last").GetProperty("data")[0].GetString()=="長劍","data strings kept for HTML #0");
            w.Reset();
            var ext=Ext(w);
            t.Check(ext.GetProperty("current").ValueKind==JsonValueKind.Null&&ext.GetProperty("received").GetInt32()==0,"reset clears prompt state");
            t.Equal(0,sent.Count,"nothing sent in this block");
        }
    }
}
