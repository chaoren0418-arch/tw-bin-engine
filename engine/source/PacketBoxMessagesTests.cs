using System.Text;
using System.Text.Json;
namespace P06;

// T229（#547）S250 聊天訊息類子碼的離線測試：只用依 3.81 伺服器寫法合成的封包與合成字串表（假文字，不是遊戲字串），
// 不開遊戲、網路或素材。
static class PacketBoxMessagesTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Box(byte sub,params byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    static JsonElement State(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("packetBoxMessages");
    static JsonElement Unhandled(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandledSubtypes");
    static string[] Chat(WorldSession s)=>s.PlayerChatLines.Select(l=>l.Text).ToArray();

    // 合成字串表：1600 筆，只有測試用到的 id 有假文字；`\f3` 測顏色控制字元被去掉。
    static ServerStringTable FakeTable(){
        var lines=new string[1600];
        for(int i=0;i<lines.Length;i++)lines[i]="";
        lines[639]="W0 %s 開";lines[640]="W1 %s 結";lines[641]="W2 %s 中";lines[642]="\\f3W3";lines[643]="W4";
        lines[672]="L上 %d";lines[673]="L下 %d";lines[679]="E %s 100%%";lines[680]="屬1";lines[683]="\\f2屬4";
        lines[764]="T %s";lines[1511]="O";lines[1513]="H";lines[1514]="[%s = %s]";
        for(int i=1;i<=8;i++)lines[912+i]="城"+i;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes("1600\r\n"+string.Join("\r\n",lines)));
    }

    [WorldModuleTest("packetbox-messages")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：12 個子碼都掛在同一個處理器；登錄表無錯誤；不與 B2 模組衝突。
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        t.Equal(12,PacketBoxMessageRules.Subtypes.Length,"12 B1 subtypes");
        foreach(byte sub in PacketBoxMessageRules.Subtypes)
            t.Check(reg.Entries.Any(e=>e.Kind=="receive"&&e.Opcode==250&&e.Subtype==sub&&e.Method=="WorldSession.ReceivePacketBoxMessage"),$"S250/{sub} registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="snapshot"&&e.Key=="packetBoxMessages"),"snapshot registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPacketBoxMessages"),"reset registered");

        // 2. 格式化規則。
        t.Equal("a1b2",PacketBoxMessageRules.Format("a%sb%d",["1","2"]),"printf sequential");
        t.Equal("x-y",PacketBoxMessageRules.Format("%1-%0",["y","x"]),"positional");
        t.Equal("100% ",PacketBoxMessageRules.Format("100%% %s",[]),"percent and missing arg");

        using(ServerMessageText.UseTable(FakeTable())){
            var s=new WorldSession();var events=new List<int>();s.PacketBoxMessageReceived+=events.Add;

            // 3. 0–2 攻城：C(城堡) H(0)，%s＝城堡名。
            s.Receive(Box(0,4,0,0),"");s.Receive(Box(1,7,0,0),"");s.Receive(Box(2,1,0,0),"");
            t.Check(Chat(s).SequenceEqual(["W0 城4 開","W1 城7 結","W2 城1 中"]),"war begin/end/going (castle name = string 912+id)",Chat(s));
            s.Receive(Box(0,9,0,0),"");t.Equal("W0 城堡 9 開",Chat(s)[^1],"castle outside 1-8 uses engine name");
            // 4. 3／4：無欄位；顏色控制字元去掉。
            s.Receive(Box(3),"");s.Receive(Box(4),"");
            t.Check(Chat(s)[^2..].SequenceEqual(["W3","W4"]),"war initiative/occupy",Chat(s));

            // 5. 12：S_PacketBoxItemLv(min,0)／(0,max)。
            s.Receive(Box(12,45,0),"");s.Receive(Box(12,0,30),"");
            t.Check(Chat(s)[^2..].SequenceEqual(["L上 45","L下 30"]),"level over min/max",Chat(s));

            // 6. 15：字串 679，%s＝字串 679＋n（1 火…4 地）；23：S(名稱)。
            s.Receive(Box(15,1),"");s.Receive(Box(15,4),"");s.Receive(Box(23,Str("村長甲")),"");
            t.Check(Chat(s)[^3..].SequenceEqual(["E 屬1 100%","E 屬4 100%","T 村長甲"]),"elf and town leader",Chat(s));

            // 7. 79：C(7) 與 6 個血盟名，無主寫 " "。
            s.Receive(Box(79,[7,..Str("盟A"),..Str(" "),..Str(" "),..Str("盟D"),..Str(" "),..Str(" ")]),"");
            var tail=Chat(s)[^4..];
            t.Check(tail.SequenceEqual(["O","H","[城1 = 盟A]","[城4 = 盟D]"]),"war over lines (owned castles only)",tail);
            t.Check(s.WarOverClans!.SequenceEqual(["盟A","","","盟D","",""]),"war over clans",s.WarOverClans);

            // 8. 只存資料：27 兩種寫法、30、31；不進聊天欄。
            int before=Chat(s).Length;
            s.Receive(Box(27),"");t.Equal(new PacketBoxRankChange(null,null),s.RankChange,"rank change without fields");
            s.Receive(Box(27,[10,..Str("角色乙")]),"");t.Equal(new PacketBoxRankChange(10,"角色乙"),s.RankChange,"rank change with name");
            s.Receive(Box(27,[11,..Str("")]),"");t.Equal(new PacketBoxRankChange(11,""),s.RankChange,"rank change banned (11, empty)");
            s.Receive(Box(30,[..Le32(77),..Str("王"),..Str("盟")]),"");t.Equal(new PacketBoxLastavard(77,"王","盟"),s.LastavardWin,"lastavard");
            s.Receive(Box(31),"");s.Receive(Box(31),"");t.Equal(2,s.FeelGoodCount,"feel good counted");
            t.Equal(before,Chat(s).Length,"data-only subtypes add no chat line");

                        // 7b. 79 全部無主：只有 1511。
            int beforeEmpty=Chat(s).Length;s.Receive(Box(79,[3,..Str(" "),..Str(" ")]),"");
            t.Check(Chat(s).Length==beforeEmpty+1&&Chat(s)[^1]=="O","war over without owners",Chat(s));
            s.Receive(Box(79,[7,..Str("盟A"),..Str(" "),..Str(" "),..Str("盟D"),..Str(" "),..Str(" ")]),"");

            // 9. id ≥ 表筆數：原版不顯示。
            using(ServerMessageText.UseTable(ServerStringTable.Parse(Encoding.ASCII.GetBytes("700\n")))){
                int n=Chat(s).Length;s.Receive(Box(23,Str("x")),"");
                t.Equal(n,Chat(s).Length,"id past table count dropped");
                t.Equal(1,State(s).GetProperty("dropped").GetInt32(),"dropped counted");
            }

            // 10. 快照、事件、全部被消化。
            var st=State(s);
            t.Equal(2,st.GetProperty("packets").GetProperty("0").GetInt32(),"per-subtype count");
            t.Equal(1514,s.PacketBoxMessageLines.Last(l=>l.Subtype==79).StringId,"war over last line string id");
            t.Equal(3,st.GetProperty("packets").GetProperty("79").GetInt32(),"war over packet count");
            t.Equal(764,s.PacketBoxMessageLines.Last(l=>l.Subtype==23).StringId,"town leader string id");
            t.Equal("string-c.tbl",st.GetProperty("table").GetString(),"table source");
            foreach(byte sub in PacketBoxMessageRules.Subtypes)t.Check(!Unhandled(s).TryGetProperty($"0xFA/0x{sub:X2}",out _),$"S250/{sub} consumed");
            t.Check(events.Count==21&&events.Contains(79)&&events.Contains(31),"events",events);

            // 11. 既有 S250 子碼（5 決鬥、B2 的 56）不被本模組搶走。
            s.Receive(Box(56,0x10,0x0E),"");t.Check(s.MagicBuffs.Any(b=>b.Skill==PacketBoxStatusRules.DollBuff),"S250/56 still handled by PacketBoxStatus");

            // 12. 格式錯誤：丟 ProtocolException 且不改狀態、不加聊天行。
            string snap=State(s).GetRawText();int chat=Chat(s).Length;
            void Reject(byte[] p,string label){t.Throws<ProtocolException>(()=>s.Receive(p,""),label);}
            Reject(Box(0,4,1,0),"war tail nonzero");
            Reject([250,0,4],"war truncated");
            Reject(Box(12,0,0),"level over no limit");
            Reject(Box(79,0),"war over count 0");
            Reject(Box(79,200),"war over count exceeds packet");
            Reject([250,23,0x41,0x42,0x43,0x44,0x45,0x46],"town leader unterminated");
            Reject(Box(30,[..Le32(1),..Str("a")]),"lastavard missing clan");
            var bad=Box(3);bad[^1]=1;Reject(bad,"nonzero padding");
            Reject([..Box(31),0,0,0,0,0,0,0,0],"extra block");
            t.Equal(snap,State(s).GetRawText(),"malformed packets keep state");
            t.Equal(chat,Chat(s).Length,"malformed packets add no chat");

            // 13. Reset 清除。
            s.Reset();st=State(s);
            t.Equal(0,st.GetProperty("lines").GetArrayLength(),"reset lines");
            t.Check(s.RankChange==null&&s.LastavardWin==null&&s.WarOverClans==null&&s.FeelGoodCount==0,"reset data");
        }

        // 14. 沒有字串表（CI）：照 ServerMessageText 慣例顯示「原服訊息 id：參數」。
        using(ServerMessageText.UseTable(null)){
            var s=new WorldSession();
            s.Receive(Box(0,5,0,0),"");s.Receive(Box(4),"");
            s.Receive(Box(15,2),"");
            t.Check(Chat(s).SequenceEqual(["原服訊息 639：海音城","原服訊息 643","原服訊息 679：字串 681"]),"fallback text",Chat(s));
            t.Equal("fallback",State(s).GetProperty("table").GetString(),"fallback table source");
        }
    }
}
