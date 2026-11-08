using System.Text.Json;
namespace P06;

// M7-C（T205／#511）城堡模組的離線測試：只用合成封包（依 server-protocol.md 欄位順序），不開遊戲、網路或素材。
static class CastleTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] S(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] CastleMaster(byte type,uint leader)=>S([CastleProtocol.SCastleMaster,type],Le32(leader));
    static byte[] TaxRate(uint id)=>S([CastleProtocol.STaxRate],Le32(id),[0x0a,0x32]);
    static byte[] Deposit(uint id)=>S([CastleProtocol.SDeposit],Le32(id));
    static byte[] Drawal(uint id,uint count)=>S([CastleProtocol.SDrawal],Le32(id),Le32(count));
    static JsonElement Castle(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("castle");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;
    static string LastLine(WorldSession s)=>s.PlayerChatLines.LastOrDefault()?.Text??"";

    [WorldModuleTest("castle")]
    static void Run(WorldModuleTestContext t){
        const uint Me=0x00123456,Other=0x00ABCDEF;

        // 1. 註冊：四個 S 封包都由本模組接；登錄表沒有錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        foreach(byte op in new byte[]{69,185,141,4})
            t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==op&&e.Subtype==-1&&e.Method.StartsWith("WorldSession.ReceiveCastle",StringComparison.Ordinal)),$"S{op} registered to castle module");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="castle"),"castle snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetCastle"),"castle reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. C 封包編碼（不含 opcode 以外的推測欄位；長度補到 8 的倍數）。
        t.Equal("1356341200190000",Convert.ToHexString(CastleProtocol.TaxRate(Me,25)),"C19 = C(19) D(objId) C(rate)");
        t.Equal("3856341200A0860100" + "00000000000000",Convert.ToHexString(CastleProtocol.Deposit(Me,100000)),"C56 = C(56) D(objId) D(count)");
        t.Equal("2C5634120001000000" + "00000000000000",Convert.ToHexString(CastleProtocol.Withdraw(Me,1)),"C44 = C(44) D(objId) D(count)");

        // 3. S69 城主表（windows-2.md §5.1）：table[城堡]=物件 id；第 2 格不畫王冠；type 0 只寫第 0 格；≥ 9 忽略。
        var s=new WorldSession();
        s.Receive(CastleMaster(4,Me),"");
        t.Equal(Me,s.CastleMaster(4),"S69 type 4 writes slot 4");
        t.Check(s.HasCastleCrown(Me),"slot 4 owner gets crown");
        t.Equal("[城堡] #1193046 成為奇巖城城主。",LastLine(s),"S69 text notice");
        t.Equal(0,Unhandled(s,"0x45"),"S69 not counted unhandled");
        int lines=s.PlayerChatLines.Length;
        s.Receive(CastleMaster(4,Me),"");
        t.Equal(lines,s.PlayerChatLines.Length,"repeated identical S69 adds no notice");
        s.Receive(CastleMaster(1,Me),"");
        t.Equal(Me,s.CastleMaster(1),"one leader may occupy several slots");
        s.Receive(CastleMaster(4,Other),"");
        t.Equal(Other,s.CastleMaster(4),"new owner overwrites slot 4");
        t.Check(s.HasCastleCrown(Me),"previous owner still crowned via slot 1");
        s.Receive(CastleMaster(2,0x777),"");
        t.Equal(0x777u,s.CastleMaster(2),"slot 2 stored");
        t.Check(!s.HasCastleCrown(0x777),"slot 2 draws no crown");
        s.Receive(CastleMaster(8,0x888),"");
        t.Check(s.HasCastleCrown(0x888),"slot 8 crowned (server sends 8 for Orc castle owner)");
        s.Receive(CastleMaster(0,Other),"");
        t.Equal(Other,s.CastleMaster(4),"type 0 does not clear other slots");
        t.Equal(Other,s.CastleMaster(0),"type 0 writes slot 0");
        t.Check(s.HasCastleCrown(Other),"type 0 leaves existing crown (original only writes slot 0)");
        t.Equal("[城堡] 伺服器通知：#11259375 的血盟失去城堡。",LastLine(s),"type 0 text notice");
        lines=s.PlayerChatLines.Length;
        s.Receive(CastleMaster(9,Me),"");
        t.Equal(lines,s.PlayerChatLines.Length,"type 9 ignored");
        t.Equal(0u,s.CastleMaster(9),"type 9 not stored");
        s.Receive(CastleMaster(8,0),"");
        t.Equal("[城堡] 狄亞得要塞目前沒有城主。",LastLine(s),"leader 0 clears slot with notice");
        var snap=Castle(s);
        t.Equal(3,snap.GetProperty("masters").GetArrayLength(),"snapshot lists slots 1,2,4");
        t.Check(!snap.GetProperty("masters")[1].GetProperty("crowned").GetBoolean(),"snapshot marks slot 2 uncrowned");
        t.Throws<ProtocolException>(()=>s.Receive([69,1,0x56,0x34],""),"S69 truncated rejected");
        // 4. S185 稅率詢問 → C19。
        s.Receive(TaxRate(Me),"");
        var tax=s.CastleOffer;
        t.Check(tax is {Kind:CastleOfferKind.TaxRate,ObjectId:Me,Min:10,Max:50},"S185 opens tax-rate offer with min/max from packet",tax);
        t.Equal("[城堡] 城堡管家詢問稅率設定（可設定 10%～50%）。",LastLine(s),"S185 text notice");
        t.Equal(0,Unhandled(s,"0xB9"),"S185 not counted unhandled");
        t.Throws<ArgumentException>(()=>s.CastleRequest(tax!.Revision,Me,9),"tax 9% rejected (server range 10..50)");
        t.Throws<ArgumentException>(()=>s.CastleRequest(tax!.Revision,Me,51),"tax 51% rejected");
        t.Equal((byte[]?)null,s.CastleRequest(tax!.Revision,Other,20),"reply refused when actor differs from offer objId");
        t.Equal((byte[]?)null,s.CastleRequest(tax.Revision+99,Me,20),"stale revision refused");
        t.Equal(Convert.ToHexString(CastleProtocol.TaxRate(Me,10)),Convert.ToHexString(s.CastleRequest(tax.Revision,Me,10)!),"tax request at min bound");
        t.Equal(Convert.ToHexString(CastleProtocol.TaxRate(Me,50)),Convert.ToHexString(s.CastleRequest(tax.Revision,Me,50)!),"tax request at max bound");
        s.CastleSent(tax.Revision,30);
        t.Equal((CastleOffer?)null,s.CastleOffer,"offer consumed after send (server ticket is single use)");
        t.Equal("[城堡] 已送出稅率設定 30%，等待伺服器處理。",LastLine(s),"sent notice");
        t.Equal((byte[]?)null,s.CastleRequest(tax.Revision,Me,30),"no second reply for the same offer");

        // 5. S4 存入 → C56。
        s.Receive(Deposit(Me),"");
        var dep=s.CastleOffer!;
        t.Check(dep is {Kind:CastleOfferKind.Deposit,Min:1,Max:int.MaxValue},"S4 opens deposit offer 1..int.MaxValue",dep);
        t.Check(dep.Revision!=tax.Revision,"new offer gets new revision");
        t.Throws<ArgumentException>(()=>s.CastleRequest(dep.Revision,Me,0),"deposit 0 rejected");
        t.Equal(Convert.ToHexString(CastleProtocol.Deposit(Me,int.MaxValue)),Convert.ToHexString(s.CastleRequest(dep.Revision,Me,int.MaxValue)!),"deposit int.MaxValue encodes");
        t.Check(s.CancelCastleOffer(dep.Revision)&&s.CastleOffer==null,"cancel closes offer without sending");
        t.Check(!s.CancelCastleOffer(dep.Revision),"cancel twice is no-op");

        // 6. S141 提領 → C44；上限來自封包。
        s.Receive(Drawal(Me,5000),"");
        var wd=s.CastleOffer!;
        t.Check(wd is {Kind:CastleOfferKind.Withdraw,Min:1,Max:5000},"S141 opens withdraw offer capped at packet count",wd);
        t.Equal("[城堡] 城堡管家詢問要從金庫提領的金幣數量（最多 5,000）。",LastLine(s),"S141 text notice");
        t.Throws<ArgumentException>(()=>s.CastleRequest(wd.Revision,Me,5001),"withdraw above treasury rejected");
        t.Equal(Convert.ToHexString(CastleProtocol.Withdraw(Me,5000)),Convert.ToHexString(s.CastleRequest(wd.Revision,Me,5000)!),"withdraw at max encodes");
        s.Receive(TaxRate(Me),"");
        t.Equal((byte[]?)null,s.CastleRequest(wd.Revision,Me,10),"newer offer replaces older one");
        s.Receive(Drawal(Me,0),"");
        t.Equal((CastleOffer?)null,s.CastleOffer,"S141 count 0 opens nothing");
        t.Equal("[城堡] 城堡金庫目前沒有可以提領的金幣。",LastLine(s),"empty treasury notice");
        t.Throws<ProtocolException>(()=>s.Receive(Drawal(Me,0x80000000),""),"S141 negative count rejected");
        t.Throws<ProtocolException>(()=>s.Receive(S([CastleProtocol.STaxRate],Le32(Me),[0x32,0x0a]),""),"S185 min>max rejected");
        t.Throws<ProtocolException>(()=>s.Receive(Deposit(0),""),"S4 objId 0 rejected");

        // 7. Reset 清空。
        s.Receive(Deposit(Me),"");
        s.Reset();
        var cleared=Castle(s);
        t.Equal(0,cleared.GetProperty("masters").GetArrayLength(),"Reset clears castle master table");
        t.Equal(JsonValueKind.Null,cleared.GetProperty("offer").ValueKind,"Reset clears offer");
        t.Equal((CastleOffer?)null,s.CastleOffer,"Reset clears offer property");
    }
}
