using System.Text.Json;
namespace P06;

// M7-G（T208／#514）以物品學魔法的離線測試：只用合成封包（依 server-protocol.md 欄位順序），不開遊戲、網路或素材。
static class SpellExchangeTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] S(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] List(params uint[] indices)=>S([[SpellExchangeProtocol.SList,(byte)indices.Length,(byte)(indices.Length>>8)],..indices.Select(Le32)]);
    /// <summary>S164 S_AddSkill：C(164) C(32) 28 個位元組遮罩；skillId＝row*8+bit+1。</summary>
    static byte[] AddSkill(params int[] ids){var p=new byte[30];p[0]=164;p[1]=32;foreach(int id in ids)p[2+(id-1)/8]|=(byte)(1<<((id-1)%8));return LoginPackets.Pad(p);}
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("spellExchange");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);

    [WorldModuleTest("spell-exchange")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S13 由本模組接；快照與 reset 掛上；登錄表沒有錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==13&&e.Subtype==-1&&e.Method=="WorldSession.ReceiveSpellExchangeList"),"S13 registered to spell-exchange module");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="spellExchange"),"spellExchange snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetSpellExchange"),"spellExchange reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. C 封包編碼（長度補到 8 的倍數）。
        t.Equal("F556341200000000",Hex(SpellExchangeProtocol.RequestList(0x00123456)),"C245 = C(245) D(npcObjId)");
        t.Equal("BF01001600000000",Hex(SpellExchangeProtocol.Learn([22])),"C191 = C(191) H(1) D(skillIndex)");
        t.Equal("BF0200160000002A0000000000000000",Hex(SpellExchangeProtocol.Learn([22,42])),"C191 two indices in order, 3+4n padded to 8");
        t.Throws<ArgumentException>(()=>SpellExchangeProtocol.RequestList(0),"C245 npc 0 rejected");
        t.Throws<ArgumentException>(()=>SpellExchangeProtocol.Learn([]),"C191 empty rejected (server needs count ≥ 1)");
        t.Throws<ArgumentException>(()=>SpellExchangeProtocol.Learn([5,5]),"C191 duplicate rejected");
        t.Throws<ArgumentException>(()=>SpellExchangeProtocol.Learn([-1]),"C191 negative rejected");

        // 3. S13 解碼。
        t.Equal("22,42",string.Join(",",SpellExchangeProtocol.DecodeList(List(22,42))),"S13 indices decoded in order");
        t.Equal(0,SpellExchangeProtocol.DecodeList(List()).Length,"S13 empty list = H(0)");
        t.Throws<ProtocolException>(()=>SpellExchangeProtocol.DecodeList(List(7,7)),"S13 duplicate index rejected");
        t.Throws<ProtocolException>(()=>SpellExchangeProtocol.DecodeList(S([13,2,0],Le32(1))),"S13 truncated rejected");
        t.Throws<ProtocolException>(()=>SpellExchangeProtocol.DecodeList(S([13,1,0],Le32(1),Le32(9))),"S13 trailing data rejected");
        t.Throws<ProtocolException>(()=>SpellExchangeProtocol.DecodeList(S([13,1,0],Le32(0x80000000))),"S13 index ≥ int.MaxValue rejected");

        // 4. WorldSession：要求 → 清單 → 送出 → 等 S_AddSkill。
        var s=new WorldSession();
        t.Equal("closed",s.SpellExchangePhase,"initial phase closed");
        s.SpellExchangeRequested(0x7777);
        t.Equal("waiting",s.SpellExchangePhase,"C245 admitted → waiting");
        s.Receive(List(22,42,43),"");
        t.Equal(0,Unhandled(s,"0x0D"),"S13 not counted unhandled");
        var offer=s.SpellExchangeOffer;
        t.Check(offer is {Npc:0x7777u}&&offer.Indices.SequenceEqual([22,42,43]),"S13 opens offer bound to requested NPC",offer);
        t.Equal("list",s.SpellExchangePhase,"phase list");
        var rows=s.SpellExchangeRows;
        t.Equal("23,43,44",string.Join(",",rows.Select(r=>r.SkillId)),"row skillId = index + 1");
        t.Check(rows.All(r=>!r.Learned&&r.Name.Length>0),"rows have names, none learned yet",rows);
        var snap=Snap(s);
        t.Equal(3,snap.GetProperty("offer").GetProperty("rows").GetArrayLength(),"snapshot lists 3 rows");
        t.Equal(0x7777,snap.GetProperty("offer").GetProperty("npc").GetInt32(),"snapshot carries NPC id");

        // 驗證選擇。
        long rev=offer!.Revision;
        t.Throws<ArgumentException>(()=>s.SpellExchangeRequest(rev,[]),"no selection rejected");
        t.Throws<ArgumentException>(()=>s.SpellExchangeRequest(rev,[99]),"index not in list rejected");
        t.Throws<ArgumentException>(()=>s.SpellExchangeRequest(rev,[22,22]),"duplicate selection rejected");
        t.Equal("(null)",Hex(s.SpellExchangeRequest(rev+99,[22])),"stale revision refused");
        s.Receive(AddSkill(43),"");
        t.Check(s.SpellExchangeRows.Single(r=>r.Index==42).Learned,"S164 marks skill 43 learned in offer rows");
        t.Throws<ArgumentException>(()=>s.SpellExchangeRequest(rev,[42]),"already learned skill rejected");
        t.Equal(Hex(SpellExchangeProtocol.Learn([22,43])),Hex(s.SpellExchangeRequest(rev,[22,43])),"valid selection builds C191");

        // 送出後清單作廢（原服授權票只用一次），等原服回報。
        s.SpellExchangeSent(rev,[22,43]);
        t.Equal((SpellExchangeOffer?)null,s.SpellExchangeOffer,"offer consumed after send");
        t.Equal("submitted",s.SpellExchangePhase,"phase submitted");
        t.Equal("(null)",Hex(s.SpellExchangeRequest(rev,[22])),"no second C191 for the same list");
        t.Equal(0,s.SpellExchangeLearnedCount,"nothing learned yet");
        s.Receive(AddSkill(23),"");
        t.Equal(1,s.SpellExchangeLearnedCount,"S164 for skill 23 counted");
        t.Check(Snap(s).GetProperty("notice").GetString()!.Contains("1/2"),"notice shows 1/2 learned",Snap(s).GetProperty("notice").GetString());
        s.Receive(AddSkill(44),"");
        t.Equal(2,s.SpellExchangeLearnedCount,"both learned");

        // 5. 空清單、未要求的清單、關閉、取代。
        s.SpellExchangeRequested(0x7777);
        s.Receive(List(),"");
        t.Equal((SpellExchangeOffer?)null,s.SpellExchangeOffer,"empty S13 opens no offer");
        t.Equal("empty",s.SpellExchangePhase,"phase empty");
        s.Receive(List(5),"");
        t.Check(s.SpellExchangeOffer is {Npc:null},"unsolicited S13 accepted without NPC binding",s.SpellExchangeOffer);
        long old=s.SpellExchangeOffer!.Revision;
        s.Receive(List(6),"");
        t.Check(s.SpellExchangeOffer!.Revision!=old,"newer list replaces older one");
        t.Equal("(null)",Hex(s.SpellExchangeRequest(old,[5])),"old list revision refused after replacement");
        t.Check(!s.CloseSpellExchange(s.SpellExchangeRevision-1),"close with stale revision is no-op");
        t.Check(s.CloseSpellExchange(s.SpellExchangeRevision)&&s.SpellExchangeOffer==null&&s.SpellExchangePhase=="closed","close clears offer without sending");
        t.Throws<ProtocolException>(()=>s.Receive(List(1,1),""),"bad S13 surfaces protocol error");

        // 6. Reset 清空。
        s.Receive(List(8),"");
        s.Reset();
        var cleared=Snap(s);
        t.Equal(JsonValueKind.Null,cleared.GetProperty("offer").ValueKind,"Reset clears offer");
        t.Equal("closed",cleared.GetProperty("phase").GetString(),"Reset closes");
        t.Equal(0,cleared.GetProperty("pending").GetArrayLength(),"Reset clears pending");

        // 7. 連線層：未進入世界時不送任何封包。
        using var c=new LoginConnection();
        var v=c.SpellExchangeSnapshot();
        t.Check(!v.CanRequest&&!v.CanSend,"offline connection cannot request or send",v);
        t.Check(!c.RequestSpellExchangeList(openWindow:false).GetAwaiter().GetResult(),"offline C245 refused");
        c.World.Receive(List(3),"");
        t.Check(!c.LearnSpellExchange(c.SpellExchangeSnapshot(),[3]).GetAwaiter().GetResult(),"offline C191 refused even with a list");
    }
}
