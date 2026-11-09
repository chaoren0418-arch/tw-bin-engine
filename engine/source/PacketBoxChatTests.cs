using System.Text;
namespace P06;

// R4-H（T299／#711）S250 27／30／31 印聊天欄的離線測試（FU5§2）。只用依 3.81／TW16 欄位合成的封包與合成字串表
// （假文字，不是遊戲字串），不開遊戲、網路或素材。
static class PacketBoxChatTests {
    internal const uint Own=1000,Other=2000;
    internal const string Name="自己甲",Member="成員乙";
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Box(byte sub,params byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    // S87 開頭：C(op) H(x) H(y) D(id)；觀察器只讀這幾個欄位。
    static byte[] Put(uint id,ushort x,ushort y)=>LoginPackets.Pad([87,..Le16(x),..Le16(y),..Le32(id),61,0]);
    static string[] Chat(WorldSession s)=>s.PlayerChatLines.Select(l=>l.Text).ToArray();

    // 合成字串表 2200 筆（含 2125；不含 3503，模擬本機表 3484 筆查不到 3503）。
    static ServerStringTable FakeTable(){
        var lines=new string[2200];
        for(int i=0;i<lines.Length;i++)lines[i]="";
        lines[773]="一般";lines[1878]="副君主";lines[771]="聯盟君主";lines[1879]="修習騎士";lines[772]="守護騎士";
        lines[784]="R自 %s";lines[2125]="R他 %s %s";lines[782]="%s 盟 %s 敗";lines[783]="%s 主";lines[77]="\\f1舒服";lines[764]="T %s";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes("2200\r\n"+string.Join("\r\n",lines)));
    }

    [WorldModuleTest("packetbox-chat")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S87 觀察器經 M7-A 自動登錄；27／30／31 仍由同一個處理器消化。
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        t.Check(reg.Entries.Any(e=>e.Kind=="observe"&&e.Opcode==87&&e.Method=="WorldSession.ObserveFeelGoodSelfAppear"),"S87 feel-good observer registered");
        foreach(byte sub in new byte[]{27,30,31})
            t.Check(reg.Entries.Any(e=>e.Kind=="receive"&&e.Opcode==250&&e.Subtype==sub&&e.Method=="WorldSession.ReceivePacketBoxMessage"),$"S250/{sub} registered");

        // 2. 規則：階級範圍與階級名字串 id（FU5§2.1）。
        t.Check(!PacketBoxMessageRules.RankHandled(0)&&!PacketBoxMessageRules.RankHandled(1)&&PacketBoxMessageRules.RankHandled(2)&&PacketBoxMessageRules.RankHandled(13)&&!PacketBoxMessageRules.RankHandled(14),"rank range 2–13");
        int[] ids=[..Enumerable.Range(2,12).Select(r=>PacketBoxMessageRules.RankNameId((byte)r))];
        t.Check(ids.SequenceEqual([773,1878,771,1879,772,773,1879,772,771,-1,3503,3503]),"rank name ids 2–13",ids);
        t.Equal(24,PacketBoxMessageRules.ChatColorIndex,"palette[24]");

        using(ServerMessageText.UseTable(FakeTable())){
            var s=new WorldSession();s.SeedPacketBoxChat(Own,Name,32700,32800,[new(Name,2,50,"",Own,0),new(Member,2,40,"",Other,0)]);

            // 3. 27 自己：784＋階級名；更新自己與成員清單的階級；顏色 palette[24]。
            s.Receive(Box(27,[3,..Str(Name)]),"");
            t.Equal("R自 副君主",Chat(s)[^1],"rank self prints 784");
            t.Equal<int?>(24,s.PlayerChatLines[^1].ColorIndex,"rank line colour index 24");
            t.Equal(24,ChatFormat.TextColor(s.PlayerChatLines[^1]),"rank line text colour 24");
            t.Equal((byte)3,s.Actor!.Rank,"own rank updated");
            t.Equal((byte)3,s.ClanMembers.Single(m=>m.Name==Name).Rank,"own member rank updated");
            t.Equal(784,s.PacketBoxMessageLines[^1].StringId,"string id 784");

            // 4. 27 別人：2125（名稱、階級名）；只更新成員清單。
            s.Receive(Box(27,[6,..Str(Member)]),"");
            t.Equal("R他 成員乙 守護騎士",Chat(s)[^1],"rank other prints 2125");
            t.Equal((byte)6,s.ClanMembers.Single(m=>m.Name==Member).Rank,"member rank updated");
            t.Equal((byte)3,s.Actor!.Rank,"own rank unchanged by other");
            s.Receive(Box(27,[10,..Str("路人")]),"");
            t.Equal("R他 路人 聯盟君主",Chat(s)[^1],"rank other not in clan list still prints");
            // 12／13＝3503：本機表查不到，階級名留空（待驗證）。
            s.Receive(Box(27,[12,..Str(Member)]),"");
            t.Equal("R他 成員乙 ",Chat(s)[^1],"rank 12 name 3503 missing → empty");

            // 5. 27 不印：階級 <2、>13、兩參數形式（無欄位）；11 只設自己階級、不比對名稱。
            int n=Chat(s).Length;
            s.Receive(Box(27,[1,..Str(Name)]),"");s.Receive(Box(27,[14,..Str(Name)]),"");s.Receive(Box(27),"");
            t.Equal(n,Chat(s).Length,"rank <2, >13, two-arg form print nothing");
            t.Equal((byte)3,s.Actor!.Rank,"ignored ranks keep own rank");
            s.Receive(Box(27,[11,..Str("")]),"");
            t.Equal(n,Chat(s).Length,"rank 11 prints nothing");
            t.Equal((byte)11,s.Actor!.Rank,"rank 11 sets own rank");
            t.Equal((byte)12,s.ClanMembers.Single(m=>m.Name==Member).Rank,"rank 11 does not touch members");
            t.Equal(new PacketBoxRankChange(11,""),s.RankChange,"rank change data kept");

            // 6. 30：TW16 讀 dssd。3.81 形式（D S S，城堡落在補零）→ 782（血盟、角色）＋783（血盟）。
            s.Receive(Box(30,[..Le32(5),..Str("勇者"),..Str("盟甲")]),"");
            t.Check(Chat(s)[^2..].SequenceEqual(["盟甲 盟 勇者 敗","盟甲 主"]),"lastavard 3.81 form prints 782+783",Chat(s));
            t.Equal(new PacketBoxLastavard(5,"勇者","盟甲",0),s.LastavardWin,"lastavard data (castle 0)");
            t.Check(s.PlayerChatLines[^2..].All(l=>l.ColorIndex==24),"lastavard lines colour 24");
            n=Chat(s).Length;
            s.Receive(Box(30,[..Le32(6),..Str("勇者"),..Str("盟乙"),..Le32(8)]),"");
            t.Check(Chat(s).Length==n+1&&Chat(s)[^1]=="盟乙 主","castle 8 prints only 783",Chat(s));
            t.Equal(8u,s.LastavardWin!.Castle,"castle read");
            s.Receive(Box(30,[..Le32(7),..Str("勇者"),..Str("盟丙"),..Le32(3)]),"");
            t.Check(Chat(s)[^2..].SequenceEqual(["盟丙 盟 勇者 敗","盟丙 主"]),"castle 3 prints both",Chat(s));
            t.Equal(783,s.PacketBoxMessageLines[^1].StringId,"string id 783");

            // 7. 31：印 77（去掉 \f1），設 gfx 831 旗標；別人的 S87 不觸發，自己的 S87 在封包座標放一次 gfx 831。
            s.Receive(Box(31),"");
            t.Equal("舒服",Chat(s)[^1],"feel good prints 77");
            t.Equal<int?>(24,s.PlayerChatLines[^1].ColorIndex,"feel good colour 24");
            t.Equal(77,s.PacketBoxMessageLines[^1].StringId,"string id 77");
            t.Check(s.FeelGoodEffectPending,"gfx 831 pending");
            s.ObservePacketBoxChatPut(Put(Other,32701,32801));
            t.Check(s.FeelGoodEffectPending&&!s.PacketEffects.Any(e=>e.Packet.GfxId==831),"other S87 does not trigger");
            s.ObservePacketBoxChatPut(Put(Own,32702,32802));
            var fx=s.PacketEffects.Where(e=>e.Packet.GfxId==831).ToArray();
            t.Check(fx.Length==1&&fx[0].Packet is {Target:null,X:32702,Y:32802},"own S87 places gfx 831 at its position",fx);
            t.Check(!s.FeelGoodEffectPending,"pending cleared");
            s.ObservePacketBoxChatPut(Put(Own,32703,32803));
            t.Equal(1,s.PacketEffects.Count(e=>e.Packet.GfxId==831),"only once per S250/31");
            // TW16 讀 `d`：帶物件 id 的形式也接受（284 不印，3.81 不會送）。
            n=Chat(s).Length;s.Receive(Box(31,Le32(Other)),"");
            t.Check(Chat(s).Length==n+1&&Chat(s)[^1]=="舒服","feel good with object id prints only 77",Chat(s));
            t.Equal(2,s.FeelGoodCount,"feel good counted");

            // 8. 其他 B1 子碼不套 palette[24]。
            s.Receive(Box(23,Str("村長")),"");
            t.Equal<int?>(null,s.PlayerChatLines[^1].ColorIndex,"other B1 subtype keeps default colour");

            // 9. 格式錯誤：不改狀態、不加聊天行。
            // 有 Actor 時完整快照會讀外觀素材（CI 沒有 engine/assets），所以比對公開狀態。
            object State()=>(s.RankChange,s.LastavardWin,s.FeelGoodCount,s.FeelGoodEffectPending,s.PacketBoxMessageLines.Length,s.Actor!.Rank);
            var snap=State();int chat=Chat(s).Length;
            t.Throws<ProtocolException>(()=>s.Receive(Box(30,[..Le32(1),..Str("a")]),""),"lastavard missing clan");
            t.Throws<ProtocolException>(()=>s.Receive(Box(30,[..Le32(1),..Str("a"),..Str("b"),..Le32(1),1]),""),"lastavard nonzero tail");
            t.Throws<ProtocolException>(()=>s.Receive([..Box(31),0,0,0,0,0,0,0,0],""),"feel good extra block");
            t.Throws<ProtocolException>(()=>s.Receive([250,27,3,0x41,0x42,0x43,0x44,0x45],""),"rank name unterminated");
            t.Equal(snap,State(),"malformed packets keep state");
            t.Equal(chat,Chat(s).Length,"malformed packets add no chat");

            // 10. Reset 清旗標。
            s.Receive(Box(31),"");t.Check(s.FeelGoodEffectPending,"pending again");
            s.Reset();t.Check(!s.FeelGoodEffectPending&&s.FeelGoodCount==0,"reset clears feel-good state");
        }

        // 11. 沒有字串表（CI）：照慣例顯示「原服訊息 id：參數」。
        using(ServerMessageText.UseTable(null)){
            var s=new WorldSession();s.SeedPacketBoxChat(Own,Name,1,1,[]);
            s.Receive(Box(27,[3,..Str(Name)]),"");s.Receive(Box(27,[6,..Str(Member)]),"");
            s.Receive(Box(30,[..Le32(5),..Str("勇者"),..Str("盟甲")]),"");s.Receive(Box(31),"");
            t.Check(Chat(s).SequenceEqual(["原服訊息 784：字串 1878","原服訊息 2125：成員乙、字串 772","原服訊息 782：盟甲、勇者","原服訊息 783：盟甲","原服訊息 77"]),"fallback text",Chat(s));
        }
    }
}

public sealed partial class WorldSession {
    internal void SeedPacketBoxChat(uint id,string name,ushort x,ushort y,ClanMember[] members){
        Actor=new OwnActor(x,y,id,61,0,0,0,0,0,0,name,"",0,0,"","",0,100,0,1,0,0,0);
        EnsureClanEpoch();clanMembers=members;
    }
    internal void ObservePacketBoxChatPut(byte[] plain)=>ObserveFeelGoodSelfAppear(plain);
}
