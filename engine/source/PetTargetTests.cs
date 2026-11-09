using System.Reflection;
using System.Text.Json;
namespace P06;

// M7-W（T223／#530）寵物指定目標與召喚選擇協定的離線測試：只用合成封包（欄位依 server-protocol.md 的 S_SelectTarget、
// C_SelectTarget、S_ShowSummonList 與 C_NPCAction）與記憶體內假傳送端，不開遊戲、網路、視窗或素材。
static class PetTargetTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Pet=456,Foe=789,Stranger=790;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);
    // S87（欄位同 AmountPromptTests／pets-tests）：owner 是第 2 個空字串欄位，HP 在其後第 2 個 byte。
    static byte[] Appear(uint id,ushort x,string name,string owner="",byte hp=100)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(owner),[0,hp,0,0,0,255,255]);
    static byte[] SelectTarget(uint id,byte a=0,byte b=0,byte c=2)=>P([PetTargetPackets.SSelect],Le32(id),[a,b,c]);
    static byte[] SummonList(uint id)=>P([39],Le32(id),Str("summonlist"));
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static JsonElement Ext(WorldSession s,string method)=>JsonSerializer.SerializeToElement(typeof(WorldSession).GetMethod(method,F)!.Invoke(s,null));
    static int Unhandled(WorldSession s,byte op)=>((System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(s)!).GetValueOrDefault(op);

    static LoginConnection Seed(List<byte[]> log){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":44,\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");c.World.Receive(Appear(Pet,32601,"夥伴","offline",70),"offline");
        c.World.Receive(Appear(Foe,32602,"目標"),"offline");c.World.Receive(Appear(Stranger,32603,"他人寵物","someone",70),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();

    [WorldModuleTest("pet-target")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==236&&e.Subtype==-1&&e.Method=="WorldSession.ReceivePetSelectTarget"),"S236 registered");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="petTarget")&&entries.Any(e=>e.Kind=="snapshot"&&e.Key=="summon"),"petTarget／summon snapshots registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPetTarget")&&entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetSummon"),"resets registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. 封包格式。
        t.Equal("DFC80100000015030000000000000000",Hex(PetTargetPackets.Select(Pet,Foe)),"C223 = C(223) D(pet) C(0) D(target), padded");
        t.Equal(Hex(PlayerQueryPackets.CheckTarget(Pet,Foe)),Hex(PetTargetPackets.Select(Pet,Foe)),"same C223 layout as /check");
        t.Throws<ArgumentException>(()=>PetTargetPackets.Select(0,Foe),"C223 pet 0 rejected");
        t.Throws<ArgumentException>(()=>PetTargetPackets.Select(Pet,Pet),"C223 pet cannot target itself");
        t.Equal(Pet,PetTargetPackets.DecodeSelect(SelectTarget(Pet)),"S236 D(id) C0 C0 C2 decoded");
        t.Throws<ProtocolException>(()=>PetTargetPackets.DecodeSelect(SelectTarget(Pet,c:1)),"S236 wrong tail rejected");
        t.Throws<ProtocolException>(()=>PetTargetPackets.DecodeSelect(SelectTarget(0)),"S236 id 0 rejected");
        t.Throws<ProtocolException>(()=>PetTargetPackets.DecodeSelect(P([236],Le32(Pet),[0,0,2],Le32(1))),"S236 trailing data rejected");
        t.Equal("7DC801000061747461636B6368720000",Hex(PetPackets.Command(Pet,PetPackets.TargetCommand)),"C125 D(pet) S(\"attackchr\")");
        t.Equal("7D7B0000003236330000000000000000",Hex(PetPackets.SummonChoice(Me,"263")),"C125 D(self) S(\"263\") summon choice");
        t.Throws<ArgumentException>(()=>PetPackets.SummonChoice(Me,"abc"),"summon action must be digits");
        t.Throws<ArgumentException>(()=>PetPackets.SummonChoice(Me,"0"),"summon action 0 rejected");

        // 3. 世界：S236 只接自己的寵物；自己 id 留給玩家查詢（未處理）；他人的寵物忽略。
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        t.Check(w.PetCompanions.Any(o=>o.ObjectId==Pet)&&!w.PetCompanions.Any(o=>o.ObjectId==Stranger),"seeded own companion only");
        w.Receive(SelectTarget(Me),"offline");
        t.Equal(1,Unhandled(w,236),"S236 for self stays unhandled (player-query selection mode)");
        t.Check(w.PetTargetState==null,"self S236 does not arm pet target");
        w.Receive(SelectTarget(Stranger),"offline");
        t.Check(w.PetTargetState==null&&Ext(w,"PetTargetSnapshotModel").GetProperty("ignored").GetInt32()==1,"foreign pet S236 ignored and counted");
        t.Equal(1,Unhandled(w,236),"foreign pet S236 consumed by module");

        // 4. 預先選好的目標不算：必須 S236 之後重新選取。
        w.SelectTarget(Foe);
        w.Receive(SelectTarget(Pet),"offline");
        var v=c.PetTargetSnapshot();
        t.Check(v.PetId==Pet&&v.Candidate==null&&!v.CanSend,"armed: earlier target selection is not reused",v);
        t.Check(!Run(c.SendPetTarget(v)),"no C223 without a new selection");
        w.SelectTarget(Pet);
        t.Check(c.PetTargetSnapshot().Candidate==null,"pet itself is not a candidate");
        w.SelectTarget(Foe);
        v=c.PetTargetSnapshot();
        t.Check(v.Candidate==Foe&&v.CanSend,"new selection after S236 becomes the candidate",v);
        t.Check(Run(c.SendPetTarget(v)),"C223 admitted");
        t.Equal(Hex(PetTargetPackets.Select(Pet,Foe)),Hex(log.LastOrDefault()),"C223 bytes = pet + selected target");
        t.Check(w.PetTargetState==null&&c.PetTargetSnapshot().PetId==null,"request consumed after one send");
        t.Check(!Run(c.SendPetTarget(v)),"old view cannot send twice");
        t.Equal(1,log.Count,"exactly one packet sent");

        // 5. 取消、刷新失效、地圖切換與 Reset。
        w.Receive(SelectTarget(Pet),"offline");var armed=c.PetTargetSnapshot();
        t.Check(c.CancelPetTarget(armed)&&w.PetTargetState==null,"local cancel clears selection mode");
        t.Check(!c.CancelPetTarget(armed),"cancel is one-shot");
        w.Receive(SelectTarget(Pet),"offline");w.SelectTarget(Foe);var stale=c.PetTargetSnapshot();
        w.SelectTarget(Stranger);
        t.Check(!Run(c.SendPetTarget(stale))&&log.Count==1,"view for an older selection refused after the target changed");
        t.Check(Run(c.SendPetTarget(c.PetTargetSnapshot()))&&Hex(log.Last())==Hex(PetTargetPackets.Select(Pet,Stranger)),"current selection sends once");
        w.Receive(SelectTarget(Pet),"offline");
        w.Receive(LoginPackets.Pad([206,4,0,0]),"offline");
        t.Check(w.PetTargetState==null,"map change cancels pet target mode");
        w.Reset();
        t.Check(w.PetTargetState==null&&Ext(w,"PetTargetSnapshotModel").GetProperty("ignored").GetInt32()==0,"reset clears pet target state");
    }

    [WorldModuleTest("summon-request")]
    static void Summon(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        int npcRevision=w.InteractionRevision;
        w.Receive(SummonList(Stranger),"offline");
        t.Check(w.SummonRequestState==null,"S39 summonlist for another object ignored");
        w.Receive(SummonList(Me),"offline");
        var v=c.SummonSnapshot();
        t.Check(v.Open&&v.Source=="server"&&v.ObjectId==Me&&v.Level==44&&v.Charisma==25&&v.CanSend,"S39 summonlist opens a server request with S8 level／charisma",v);
        t.Equal(npcRevision,w.InteractionRevision,"summonlist does not enter the NPC dialog parser");
        t.Throws<ProtocolException>(()=>w.Receive(P([39],Le32(Me),Str("summonlist"),Le32(9)),"offline"),"summonlist with extra fields rejected");
        t.Check(!Run(c.SendSummonChoice(v,"x1")),"non-numeric action refused");
        t.Check(Run(c.SendSummonChoice(v,"263")),"server summon choice admitted");
        t.Equal(Hex(PetPackets.SummonChoice(Me,"263")),Hex(log.LastOrDefault()),"reply is C125 D(self) S(action)");
        t.Check(!c.SummonSnapshot().Open&&!Run(c.SendSummonChoice(v,"263")),"request closed; old view cannot resend");
        t.Equal(1,log.Count,"one packet");
        // 關閉、地圖切換與本機入口。
        w.Receive(SummonList(Me),"offline");var open=c.SummonSnapshot();
        t.Check(c.CloseSummon(open)&&!c.SummonSnapshot().Open&&!c.CloseSummon(open),"close is local and one-shot");
        t.Check(c.OpenLocalSummon(),"local entry opens");
        var local=c.SummonSnapshot();
        t.Check(local.Open&&local.Source=="local"&&!local.SkillLearned&&!local.CanSend,"local entry needs learned skill 51 before it can send",local);
        t.Check(!Run(c.SendSummonChoice(local,"7"))&&log.Count==1,"local entry without skill 51 sends nothing");
        t.Check(c.CloseSummon(local),"local entry closes");
        w.Receive(SummonList(Me),"offline");
        w.Receive(LoginPackets.Pad([206,4,0,0]),"offline");
        t.Check(!c.SummonSnapshot().Open,"map change closes summon request");
        w.Reset();
        t.Check(w.SummonRequestState==null,"reset clears summon request");
        var off=new LoginConnection();
        t.Check(!off.OpenLocalSummon()&&!off.SummonSnapshot().Open,"offline connection cannot open summon selection");
    }
}
