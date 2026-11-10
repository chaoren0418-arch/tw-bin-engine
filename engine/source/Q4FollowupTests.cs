using System.Reflection;
using System.Text;
using P06.NativeUi;
namespace P06;

// T316（#746）離線測試：Q4 補做（FU6＝docs/reference/followups-6.md）。
//   §1 C129 關注血盟（子碼 0／1／2、/focus、血盟窗新增／勾選／刪除）、§2 職業碼來源（LoginCharacter.Class → 盲目加成）、
//   §3 S84 印聊天欄與 C227 入口（/war、/surrender、/cease、血盟窗 ChangeWarState）、§4 /propose、/divorce（C50 0／1）、
//   §6 S161 的 `$數字` 用 desc-c.tbl 展開。
// 只用依 3.81／TW16 欄位合成的封包、合成字串表（假文字，不是遊戲字串）與記憶體送出閘門；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
public sealed partial class WorldSession {
    internal void SeedQ4(uint id,string name,string clan,byte rank,ushort gfx,byte level){
        Entered=true;MapId=0;TimeReplies=1;CacheEnabled=false;
        Status=new(id,level,0,10,10,10,10,10,10,100u,100,100,100,10,0,0,0,0,0,0,0,0,0);
        Actor=new(32600,32940,id,gfx,0,0,0,0,0,0,name,"",0,clan.Length>0?9u:0u,clan,"",rank,100,0,level,0,0,0);OwnDead=false;
    }
    internal void Q4SetActor(Func<OwnActor,OwnActor> change)=>Actor=change(Actor!);
    internal void Q4SetLevel(byte level)=>Status=Status! with{Level=level};
}
public sealed partial class LoginConnection {
    internal void SeedQ4(string name,byte cls,string clan,byte rank,ushort gfx,byte level,List<byte[]> sent){
        World.SeedQ4(Q4FollowupTests.Own,name,clan,rank,gfx,level);State=LoginState.InWorld;
        characters.Clear();characters.Add(new LoginCharacter(name,clan,cls,0,0,100,100,0,level,10,10,10,10,10,10,0,0,0));worldSelection=name;
        sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
    }
    internal void Q4SetClass(byte cls){var c=characters[0];characters[0]=c with{Class=cls};}
    internal void Q4ClearCharacters(){characters.Clear();worldSelection=null;}
    /// <summary>聊天送出後本機有 1 秒防連點；測試逐條送，所以每次先歸零。</summary>
    internal Task<bool> Q4Chat(string text){
        playerChatNextAt=0;var v=PlayerChatSnapshot();
        return SendPlayerChat(text,v.Generation,v.WorldEpoch,v.Actor);
    }
}

static class Q4FollowupTests {
    internal const uint Own=746;
    const string Me="q4-self",MyClan="紅血盟",Enemy="藍血盟";
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Big5(string s)=>LoginPackets.TextEncoding.GetBytes(s);
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static byte[] War(byte type,string a,string b)=>P([84,type],Str(a),Str(b));
    static byte[] Code(byte type,uint id,string text)=>P([161,type],Le32(id),Str(text));
    static string[] Chat(WorldSession s)=>s.PlayerChatLines.Select(l=>l.Text).ToArray();

    // 合成 string-c.tbl：3484 筆（同本機表筆數，所以 4118 查不到）；只放本組要用的 id，文字是測試用的假字。
    static ServerStringTable Strings(){
        var lines=new string[3484];for(int i=0;i<lines.Length;i++)lines[i]="";
        lines[226]="%0 向 %1 宣";lines[235]="\\f1交戰 %0";lines[228]="%0 降 %1";lines[227]="%0 %1 結束";lines[231]="%0 勝 %1";lines[224]="%0 盟 %1";lines[225]="%0 破 %1";
        lines[478]="T478";lines[785]="T785";lines[234]="T234";lines[232]="T232";lines[475]="T475";lines[272]="T272";
        lines[3361]="T3361";lines[577]="T577";lines[578]="T578";lines[579]="T579";
        return Table(lines);
    }
    static ServerStringTable Table(string[] lines){
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(lines.Length+"\r\n"+string.Join("\r\n",lines)));
    }

    [WorldModuleTest("q4-followup")]
    static void Run(WorldModuleTestContext t){
        Attention(t);Commands(t);ClassSource(t);
        using(ServerMessageText.UseTable(Strings())){WarMessages(t);WarCommands(t);}
        using(ServerMessageText.UseTable(null)){WarFallback(t);Marriage(t);}
        CodeChat(t);
        using(ServerMessageText.UseTable(null))Pledge(t);
    }

    // ---- §1 C129 編碼 ----
    static void Attention(WorldModuleTestContext t){
        t.Check(ClanProtocol.EncodeAttentionAdd(Enemy).SequenceEqual(LoginPackets.Pad([129,0,..Big5(Enemy),0])),"C129 子碼 0 ccs");
        t.Check(ClanProtocol.EncodeAttentionRemove(Enemy).SequenceEqual(LoginPackets.Pad([129,1,..Big5(Enemy),0])),"C129 子碼 1 ccs");
        t.Check(ClanProtocol.EncodeAttentionGetList().SequenceEqual(LoginPackets.Pad([129,2])),"C129 子碼 2 cc");
        // 9 個中文字＝18 byte → 截到 8 字 16 byte（不切斷雙位元組字）；英文 20 字 → 16 字。
        var longName=ClanProtocol.AttentionName("一二三四五六七八九");
        t.Check(longName.Length==16&&LoginPackets.TextEncoding.GetString(longName)=="一二三四五六七八","名稱截到 16 byte、字元邊界",longName.Length);
        t.Equal(16,ClanProtocol.AttentionName("abcdefghijklmnopqrst").Length,"英文名稱截到 16 byte");
        t.Equal(15,ClanProtocol.AttentionName("a一二三四五六七八").Length,"單＋雙位元組：放不下的雙位元組字整個不收");
        t.Throws<ArgumentException>(()=>ClanProtocol.EncodeAttentionAdd("  "),"空白名稱不送");
        t.Check(ClanProtocol.AttentionInputText==3361&&ClanProtocol.AttentionDeleteConfirmText==4118,"字串 3361／4118");
    }

    // ---- 斜線指令解析 ----
    static void Commands(WorldModuleTestContext t){
        (string input,ClientChatCommandKind kind,string arg)[] cases=[
            ("/focus",ClientChatCommandKind.Focus,""),("/注視 藍血盟",ClientChatCommandKind.Focus,"藍血盟"),("/FOCUS  x ",ClientChatCommandKind.Focus,"x"),
            ("/war 藍血盟",ClientChatCommandKind.War,"藍血盟"),("/宣戰 甲",ClientChatCommandKind.War,"甲"),
            ("/surrender 甲",ClientChatCommandKind.Surrender,"甲"),("/投降 甲",ClientChatCommandKind.Surrender,"甲"),
            ("/cease 甲",ClientChatCommandKind.Cease,"甲"),("/停戰 甲",ClientChatCommandKind.Cease,"甲"),
            ("/propose",ClientChatCommandKind.Propose,""),("/求婚",ClientChatCommandKind.Propose,""),
            ("/divorce",ClientChatCommandKind.Divorce,""),("/離婚",ClientChatCommandKind.Divorce,"")];
        foreach(var (input,kind,arg) in cases)t.Check(ClientChatCommands.Parse(input) is {} c&&c.Kind==kind&&c.Argument==arg,"指令 "+input,ClientChatCommands.Parse(input));
        foreach(var other in new[]{"/say 你好","/loc","focus","/warx 甲","/ war","/chatparty","你好"})t.Check(ClientChatCommands.Parse(other)==null,"不是客戶端指令："+other);
        t.Equal("指令 · 宣戰",PlayerChatProtocol.Describe("/war 甲"),"Describe 標示指令");
        t.Equal("指令 · 關注血盟",PlayerChatProtocol.Describe("/focus"),"Describe /focus");
    }

    // ---- §2 職業碼來源 ----
    static void ClassSource(WorldModuleTestContext t){
        var sent=new List<byte[]>();var c=new LoginConnection();c.SeedQ4(Me,2,MyClan,0,0,30,sent);
        t.Equal((byte?)2,c.World.OwnClass,"OwnClass＝選中角色的 LoginCharacter.Class");
        t.Check(c.World.BlindClassBonus,"職業 2：盲目加成");
        c.Q4SetClass(4);t.Check(c.World.BlindClassBonus,"職業 4：盲目加成");
        foreach(byte k in new byte[]{0,1,3,5,6,7}){c.Q4SetClass(k);t.Check(!c.World.BlindClassBonus,"職業 "+k+"：沒有加成");}
        c.Q4ClearCharacters();
        t.Check(c.World.OwnClass==null&&!c.World.BlindClassBonus,"沒有選角資料：null、沒有加成");
        var w=new WorldSession();t.Check(w.OwnClass==null&&!w.BlindClassBonus,"沒有連線的 session：null");
        w.SetOwnClassForTest(4);t.Check(w.BlindClassBonus,"測試指定職業 4");
        w.BlindClassBonus=false;t.Check(!w.BlindClassBonus,"測試覆寫仍可用");
    }

    // ---- §3.1 S84 印聊天欄 ----
    static void WarMessages(WorldModuleTestContext t){
        var sent=new List<byte[]>();var c=new LoginConnection();c.SeedQ4(Me,0,MyClan,4,0,30,sent);var s=c.World;
        int[] ids=[..new byte[]{1,8,2,3,4,6,7}.Select(ClanWarProtocol.EventText)];
        t.Check(ids.SequenceEqual([226,235,228,227,231,224,225]),"S84 type → 字串 id",ids);
        s.Receive(War(1,MyClan,Enemy),"");
        t.Equal($"{MyClan} 向 {Enemy} 宣",Chat(s)[^1],"type 1 → 226（%0 血盟 1、%1 血盟 2）");
        t.Equal<int?>(24,s.PlayerChatLines[^1].ColorIndex,"S84 字色 palette[24]");
        t.Check(s.ClanWarEnemies.SequenceEqual([Enemy]),"type 1 記下對手");
        s.Receive(War(8,MyClan,"綠血盟"),"");
        t.Equal("交戰 綠血盟",Chat(s)[^1],"type 8 → 235（對方血盟；\\f1 色碼去掉）");
        s.Receive(War(2,"綠血盟",MyClan),"");t.Equal($"綠血盟 降 {MyClan}",Chat(s)[^1],"type 2 → 228");
        s.Receive(War(3,MyClan,Enemy),"");t.Equal($"{MyClan} {Enemy} 結束",Chat(s)[^1],"type 3 → 227");
        t.Check(s.ClanWarEnemies.Length==0&&!s.ClanWarMode,"2／3 移除對手，清單空了就不在戰爭中");
        s.Receive(War(4,"甲","乙"),"");t.Equal("甲 勝 乙",Chat(s)[^1],"type 4 → 231（別人的戰爭也印）");
        s.Receive(War(6,"甲","乙"),"");t.Equal("甲 盟 乙",Chat(s)[^1],"type 6 → 224");
        s.Receive(War(7,"甲","乙"),"");t.Equal("甲 破 乙",Chat(s)[^1],"type 7 → 225");
        int n=Chat(s).Length;s.Receive(War(5,"甲","乙"),"");
        t.Equal(n,Chat(s).Length,"未知 type 不印");
    }
    static void WarFallback(WorldModuleTestContext t){
        var c=new LoginConnection();c.SeedQ4(Me,0,MyClan,4,0,30,[]);var s=c.World;
        s.Receive(War(1,MyClan,Enemy),"");
        t.Equal($"{MyClan} 血盟向 {Enemy} 血盟宣戰。",Chat(s)[^1],"沒有字串表：用 Describe 的同義文字");
        t.Equal("只有王子和公主才能宣戰。",ClanWarProtocol.LocalText(478),"沒有字串表：478 同義文字");
        t.Equal("你不再是君主了。",ClanWarProtocol.LocalText(785),"沒有字串表：785 同義文字");
    }

    // ---- §3.2 C227 入口 ----
    static void WarCommands(WorldModuleTestContext t){
        // 純規則：sub_F46290 的檢查順序。
        ClanWarProtocol.Self Self(byte? k=0,byte rank=4,ushort gfx=0,byte level=30,string clan=MyClan,params string[] enemies)=>new(k,rank,gfx,level,clan,enemies);
        t.Equal(new ClanWarProtocol.Decision(false,478),ClanWarProtocol.CheckDeclare(Self(k:2),Enemy,15),"非王族 → 478");
        t.Equal(new ClanWarProtocol.Decision(false,785),ClanWarProtocol.CheckDeclare(Self(rank:7),Enemy,15),"階級不是 0／4／10 → 785");
        t.Equal(new ClanWarProtocol.Decision(false,234),ClanWarProtocol.CheckDeclare(Self(enemies:[Enemy]),Enemy,15),"已在戰爭清單 → 234");
        t.Equal(new ClanWarProtocol.Decision(false,null),ClanWarProtocol.CheckDeclare(Self(gfx:29),Enemy,15),"變身中：不送也不提示");
        t.Equal(new ClanWarProtocol.Decision(false,232),ClanWarProtocol.CheckDeclare(Self(level:14),Enemy,15),"等級 < 15 → 232");
        t.Equal(new ClanWarProtocol.Decision(false,475),ClanWarProtocol.CheckDeclare(Self(level:24),Enemy,ClanWarProtocol.SiegeLevel,ClanWarProtocol.SiegeLevelMessage),"HTML 等級 < 25 → 475");
        t.Equal(new ClanWarProtocol.Decision(false,272),ClanWarProtocol.CheckDeclare(Self(clan:""),Enemy,15),"沒有血盟 → 272");
        t.Equal(new ClanWarProtocol.Decision(false,null),ClanWarProtocol.CheckDeclare(Self(),"  ",15),"名稱空白不送");
        t.Equal(new ClanWarProtocol.Decision(true,null),ClanWarProtocol.CheckDeclare(Self(rank:0,gfx:1),Enemy,15),"王族、階級 0、外型 1：送");
        t.Equal(new ClanWarProtocol.Decision(true,null),ClanWarProtocol.CheckDeclare(Self(k:null,rank:10),Enemy,15),"職業不明：不擋（交給伺服器）");
        t.Check(!ClanWarProtocol.CheckChatSurrenderOrCease(Self(k:2),Enemy).Send&&ClanWarProtocol.CheckChatSurrenderOrCease(Self(k:2),Enemy).Message==null,"/surrender、/cease 非王族：不送也不提示");
        t.Check(ClanWarProtocol.CheckChatSurrenderOrCease(Self(rank:7,level:1,clan:""),Enemy).Send,"/surrender、/cease 只查職業");
        t.Check(ClanWarProtocol.HtmlSurrenderOrCease(Self(k:2))==(false,(ushort?)null)&&ClanWarProtocol.HtmlSurrenderOrCease(Self(rank:0))==(false,(ushort?)785)&&ClanWarProtocol.HtmlSurrenderOrCease(Self(rank:10))==(true,(ushort?)null),"HTML surrender／cease：非王族不動作、階級不是 4／10 → 785、否則開輸入框");
        t.Check(ClanWarProtocol.WindowState(1)==ClanWarCommand.Declare&&ClanWarProtocol.WindowState(2)==ClanWarCommand.Cease&&ClanWarProtocol.WindowState(3)==ClanWarCommand.Surrender&&ClanWarProtocol.WindowState(0)==null,"ChangeWarState 1 宣戰／2 停戰／3 投降");
        t.Check(ClanWarProtocol.InputText(ClanWarCommand.Declare)==577&&ClanWarProtocol.InputText(ClanWarCommand.Surrender)==578&&ClanWarProtocol.InputText(ClanWarCommand.Cease)==579,"輸入框 577／578／579");

        // 聊天指令：實際走 SendPlayerChat → 送出閘門。
        var sent=new List<byte[]>();var c=new LoginConnection();c.SeedQ4(Me,0,MyClan,4,0,30,sent);
        t.Check(Wait(c.Q4Chat("/war "+Enemy))&&sent.Count==1&&sent[^1].SequenceEqual(ClanWarProtocol.Encode(ClanWarCommand.Declare,Enemy)),"/war 名稱 → C227 type 0",sent.Count);
        t.Check(Wait(c.Q4Chat("/投降 "+Enemy))&&sent[^1].SequenceEqual(LoginPackets.Pad([227,2,..Big5(Enemy),0])),"/投降 → C227 type 2");
        t.Check(Wait(c.Q4Chat("/cease "+Enemy))&&sent[^1].SequenceEqual(LoginPackets.Pad([227,3,..Big5(Enemy),0])),"/cease → C227 type 3");
        int count=sent.Count;
        t.Check(!Wait(c.Q4Chat("/war"))&&sent.Count==count,"/war 沒有名稱：不送");
        c.World.Q4SetLevel(10);int lines=c.World.PlayerChatLines.Length;
        t.Check(!Wait(c.Q4Chat("/war "+Enemy))&&sent.Count==count,"等級不足不送");
        t.Check(c.World.PlayerChatLines.Length==lines+1&&Chat(c.World)[^1]=="T232","等級不足 → 聊天欄印 232",Chat(c.World).LastOrDefault());
        c.World.Q4SetLevel(30);c.Q4SetClass(2);
        t.Check(!Wait(c.Q4Chat("/war "+Enemy))&&Chat(c.World)[^1]=="T478","非王族 → 478");
        lines=c.World.PlayerChatLines.Length;
        t.Check(!Wait(c.Q4Chat("/surrender "+Enemy))&&sent.Count==count&&c.World.PlayerChatLines.Length==lines,"非王族 /surrender：不送也不印");
        c.Q4SetClass(0);c.World.Q4SetActor(a=>a with{Rank=7});
        t.Check(!Wait(c.Q4Chat("/war "+Enemy))&&Chat(c.World)[^1]=="T785","階級 7 → 785");
        c.World.Q4SetActor(a=>a with{Rank=4});
        c.World.Receive(War(1,MyClan,Enemy),"");
        t.Check(!Wait(c.Q4Chat("/war "+Enemy))&&Chat(c.World)[^1]=="T234"&&sent.Count==count,"已與對方交戰 → 234");

        // 血盟窗：值 1 先查 478／785；投降／停戰沒有本機檢查。
        var src=new WorldPledgeSource(c,()=>true);
        t.Check(src.WarPrecheck(),"王族＋階級 4：可以開 577");
        c.Q4SetClass(3);lines=c.World.PlayerChatLines.Length;
        t.Check(!src.WarPrecheck()&&Chat(c.World)[^1]=="T478"&&c.World.PlayerChatLines.Length==lines+1,"血盟窗宣戰前 478");
        t.Check(src.ChangeWar(ClanWarCommand.Surrender,"綠血盟")&&sent[^1].SequenceEqual(LoginPackets.Pad([227,2,..Big5("綠血盟"),0])),"血盟窗投降：沒有本機檢查，送 C227 type 2");
        c.Q4SetClass(0);c.World.Q4SetLevel(14);count=sent.Count;
        t.Check(!src.ChangeWar(ClanWarCommand.Declare,"綠血盟")&&sent.Count==count&&Chat(c.World)[^1]=="T232","血盟窗宣戰門檻 15");
        c.World.Q4SetLevel(15);
        t.Check(src.ChangeWar(ClanWarCommand.Declare,"綠血盟")&&sent[^1].SequenceEqual(ClanWarProtocol.Encode(ClanWarCommand.Declare,"綠血盟")),"血盟窗宣戰：等級 15 送出");

        // /focus：不帶參數子碼 2、帶參數子碼 0。
        t.Check(Wait(c.Q4Chat("/focus"))&&sent[^1].SequenceEqual(LoginPackets.Pad([129,2])),"/focus → C129 子碼 2");
        t.Check(Wait(c.Q4Chat("/注視 "+Enemy))&&sent[^1].SequenceEqual(ClanProtocol.EncodeAttentionAdd(Enemy)),"/注視 名稱 → C129 子碼 0");
        // 血盟窗的關注：新增一包、刪除每個名稱一包。
        count=sent.Count;
        t.Check(src.AddAttention("甲盟")&&sent.Count==count+1&&sent[^1].SequenceEqual(ClanProtocol.EncodeAttentionAdd("甲盟")),"WorldPledgeSource 新增 → C129 0");
        t.Check(src.RemoveAttention(["甲盟","乙盟"])&&sent.Count==count+3&&sent[^2].SequenceEqual(ClanProtocol.EncodeAttentionRemove("甲盟"))&&sent[^1].SequenceEqual(ClanProtocol.EncodeAttentionRemove("乙盟")),"WorldPledgeSource 刪除兩個 → 兩包 C129 1");
        t.Check(!src.AddAttention("")&&sent.Count==count+3,"空白名稱不送");
    }

    // ---- §4 /propose、/divorce ----
    static void Marriage(WorldModuleTestContext t){
        t.Check(MarriagePackets.Divorce().SequenceEqual(LoginPackets.Pad([50,1])),"C50 子碼 1");
        var sent=new List<byte[]>();var c=new LoginConnection();c.SeedQ4(Me,0,"",0,61,30,sent);
        t.Check(Wait(c.Q4Chat("/propose"))&&sent.Count==1&&sent[0].SequenceEqual(LoginPackets.Pad([50,0])),"/propose → C50 0（沒有目標欄位、不檢查婚禮區域）");
        t.Check(c.MarriageSnapshot().Notice.Contains("/propose"),"求婚提示",c.MarriageSnapshot().Notice);
        t.Check(Wait(c.Q4Chat("/離婚"))&&sent.Count==2&&sent[1].SequenceEqual(LoginPackets.Pad([50,1])),"/離婚 → C50 1");
        t.Check(c.MarriageSnapshot().Notice.Contains("653"),"離婚提示提到 653 確認",c.MarriageSnapshot().Notice);
        t.Check(Wait(c.Q4Chat("/求婚"))&&sent.Count==3,"/求婚 別名");
    }

    // ---- §6 S161 `$數字` ----
    static void CodeChat(WorldModuleTestContext t){
        var lines=new string[500];for(int i=0;i<lines.Length;i++)lines[i]="d"+i;
        lines[4]="金幣";lines[431]="戒指";lines[7]="+0 前綴";
        var desc=Table(lines);
        t.Equal("金幣: 你好",PlayerChatProtocol.ExpandDesc("$4: 你好",desc),"$4 → 第 4 行");
        t.Equal("<戒指> 小心",PlayerChatProtocol.ExpandDesc("<$431> 小心",desc),"字中間的 $ 也展開（一般 NPC）");
        t.Equal("???: x",PlayerChatProtocol.ExpandDesc("$99999: x",desc),"超出範圍 → ???");
        t.Equal("金幣x",PlayerChatProtocol.ExpandDesc("$4;x",desc),"數字後的 ; 吃掉");
        t.Equal("金幣 戒指",PlayerChatProtocol.ExpandDesc("$4 $431",desc),"多個 $");
        t.Equal("前綴",PlayerChatProtocol.ExpandDesc("$7",desc),"結果開頭 +0 去掉前三字");
        t.Equal("$a 與 $",PlayerChatProtocol.ExpandDesc("$a 與 $",desc),"$ 後不是數字照原樣");
        t.Equal("說 $4",PlayerChatProtocol.ExpandDesc("說 $4",desc,leadingOnly:true),"職業外型：開頭不是 $ 不展開");
        t.Equal("金幣 說",PlayerChatProtocol.ExpandDesc("$4 說",desc,leadingOnly:true),"職業外型：開頭是 $ 才展開");
        t.Equal("$4: x",PlayerChatProtocol.ExpandDesc("$4: x",null),"沒有表不改");
        using(PlayerChatProtocol.UseDesc(desc)){
            t.Check(PlayerChatProtocol.TryDecode(Code(0,900,"$4: 歡迎"),out var say)&&say!.Text=="金幣: 歡迎"&&say.Code,"S161/0 解碼時展開",say);
            t.Check(PlayerChatProtocol.TryDecode(Code(2,900,"<$431> 小心"),out var shout)&&ChatFormat.DisplayText(shout!)=="[戒指] 小心","S161/2 展開後照喊話格式",shout);
            t.Check(PlayerChatProtocol.TryDecode(Code(3,900,"[$4] 公告"),out var global)&&global!.Text=="[金幣] 公告","S161/3 展開");
            var c=new LoginConnection();c.SeedQ4(Me,0,"",0,61,30,[]);c.World.Receive(Code(0,900,"$4: 你好"),"");
            t.Check(c.World.PlayerChatLines.Length>0&&c.World.PlayerChatLines[^1].Text=="金幣: 你好","WorldSession 收到的行已展開",c.World.PlayerChatLines.LastOrDefault());
        }
        using(PlayerChatProtocol.UseDesc(null))
            t.Check(PlayerChatProtocol.TryDecode(Code(0,900,"$4: 歡迎"),out var raw)&&raw!.Text=="$4: 歡迎","沒有 desc 表：照原樣");
    }

    // ---- §1／§3 原生血盟窗 ----
    sealed class Fake:IPledgeWindowSource {
        public bool? ShowEmblem=>null;public ClanEmblem? Emblem=>null;
        public bool Precheck=true,Accept=true;
        public readonly List<string> Added=[];public readonly List<string[]> Removed=[];public readonly List<(ClanWarCommand,string)> Wars=[];
        public bool SendNotice(string text)=>false;public bool SetRank(string member,byte rank)=>false;public bool SetShowEmblem(bool show)=>false;public bool UploadEmblem()=>false;
        public bool AddAttention(string name){if(Accept)Added.Add(name);return Accept;}
        public bool RemoveAttention(IReadOnlyList<string> names){if(Accept)Removed.Add(names.ToArray());return Accept;}
        public bool WarPrecheck()=>Precheck;
        public bool ChangeWar(ClanWarCommand command,string name){if(Accept)Wars.Add((command,name));return Accept;}
    }
    static readonly Dictionary<int,(int w,int h)> Sizes=new(){
        [900]=(48,20),[901]=(48,20),[902]=(48,20),[903]=(48,20),[910]=(277,423),[911]=(12,12),[912]=(33,17),[913]=(33,17),
        [914]=(200,16),[915]=(12,16),[916]=(76,21),[917]=(12,263),[918]=(12,14),[919]=(12,20)};
    static UiSurface? Surf(int surf)=>Sizes.TryGetValue(surf,out var s)?new(surf,surf+".img","synthetic",new UiImage(s.w,s.h,0,0,Enumerable.Repeat(0xFF204060u,s.w*s.h).ToArray())):null;
    static UiMouseEvent Click(Rectangle r)=>new(UiMouseKind.Down,new(r.X+r.Width/2,r.Y+r.Height/2),MouseButtons.Left,1,0,Keys.None);
    static UiKeyEvent Down(Keys k)=>new(k,true,false);
    static void Type(PledgePanel p,string text){foreach(char ch in text)p.Char(ch);}

    static void Pledge(WorldModuleTestContext t){
        // 版面沿用 R4-E 的合成 PledgeUI.xml（PledgeWindowFollowupTests.Xml）。
        string xml=(string)typeof(PledgeWindowFollowupTests).GetField("Xml",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        var p=new PledgePanel();p.Load(UiLayout.Parse(xml,"PledgeUI.xml"),Surf);
        string[] attention=["甲盟","乙盟","丙盟"];
        var data=new PledgeWindowState(1,1,MyClan,["Alice"],new Dictionary<string,string>(),null);
        p.Update(new UiPledgeGameState(new UiStaticGameState(true),data,attention));
        var cmd=new UiNullCommands();
        t.Check(p.Visible,"171 開窗");
        t.Check(p.SideRect.Contains(p.FocusAddRect)&&p.SideRect.Contains(p.FocusDeleteRect)&&p.SideRect.Contains(p.WarStateRect(2))&&p.SideRect.Contains(p.SideInputRect)&&p.SideRect.Contains(p.StatusRect),"新鈕都在側欄內");
        t.Check(!p.FocusAddRect.IntersectsWith(p.ShowEmblemRect)&&!p.WarStateRect(0).IntersectsWith(p.FocusAddRect)&&!p.AttentionRow(6).IntersectsWith(p.FocusAddRect),"新鈕不和既有元件重疊");

        // 沒有 Source：只寫提示。
        t.Check(p.Mouse(Click(p.FocusAddRect),cmd)&&p.SideInput==PledgePanel.SideInputKind.None&&p.Status.Contains("沒有接上"),"沒有 Source 不開輸入框");
        var src=new Fake();p.Source=src;

        // 新增：輸入框 3361 → Enter → 子碼 0；空白不送。
        t.Check(p.Mouse(Click(p.FocusAddRect),cmd)&&p.SideInput==PledgePanel.SideInputKind.AddFocus&&p.Status=="請輸入想要查看的血盟名。","新增：開輸入框（3361）",p.Status);
        Type(p,"藍血盟");t.Equal("藍血盟",p.SideDraft,"輸入框收字");
        p.Key(Down(Keys.Back),cmd);Type(p,"盟");
        p.Key(Down(Keys.Enter),cmd);
        t.Check(src.Added.SequenceEqual(["藍血盟"])&&p.SideInput==PledgePanel.SideInputKind.None,"Enter → AddAttention",src.Added);
        p.Mouse(Click(p.FocusAddRect),cmd);p.Key(Down(Keys.Enter),cmd);
        t.Check(src.Added.Count==1,"名稱空白不送");
        p.Mouse(Click(p.FocusAddRect),cmd);Type(p,"abcdefghijklmnopqrst");
        t.Equal(PledgePanel.SideInputLimit,p.SideDraft.Length,"輸入框最多 16 字");
        p.Key(Down(Keys.Escape),cmd);t.Check(p.SideInput==PledgePanel.SideInputKind.None&&src.Added.Count==1,"Esc 取消");

        // 勾選：點清單列切換，不送包。
        t.Check(p.Mouse(Click(p.FocusDeleteRect),cmd)&&p.SideInput==PledgePanel.SideInputKind.None&&p.Status.Contains("勾選"),"沒勾選時刪除只提示");
        p.Mouse(Click(p.AttentionRow(0)),cmd);p.Mouse(Click(p.AttentionRow(2)),cmd);p.Mouse(Click(p.AttentionRow(1)),cmd);p.Mouse(Click(p.AttentionRow(1)),cmd);
        t.Check(p.CheckedAttention.SequenceEqual(["甲盟","丙盟"])&&src.Removed.Count==0,"點列切換勾選（不送包）",p.CheckedAttention);
        // 刪除：確認框（4118 本機缺 → 編號占位）→ Enter → 每個勾選的血盟各一個名稱。
        t.Check(p.Mouse(Click(p.FocusDeleteRect),cmd)&&p.SideInput==PledgePanel.SideInputKind.DeleteConfirm&&p.Status.Contains("4118"),"刪除：確認框 4118（占位）",p.Status);
        Type(p,"x");t.Equal("",p.SideDraft,"確認框不收字");
        p.Key(Down(Keys.Enter),cmd);
        t.Check(src.Removed.Count==1&&src.Removed[0].SequenceEqual(["甲盟","丙盟"])&&p.CheckedAttention.Count==0,"Enter → RemoveAttention(勾選的名稱)",src.Removed);
        // 伺服器回新清單後，不在清單的勾選拿掉。
        p.Mouse(Click(p.AttentionRow(1)),cmd);
        p.Update(new UiPledgeGameState(new UiStaticGameState(true),data,["甲盟"]));
        t.Check(p.CheckedAttention.Count==0,"清單變了：舊勾選清掉");

        // ChangeWarState：1 宣戰（先查）→ 577；2 停戰 → 579；3 投降 → 578。
        src.Precheck=false;
        t.Check(p.Mouse(Click(p.WarStateRect(0)),cmd)&&p.SideInput==PledgePanel.SideInputKind.None,"宣戰前檢查不過：不開輸入框");
        src.Precheck=true;
        p.Mouse(Click(p.WarStateRect(0)),cmd);
        t.Check(p.SideInput==PledgePanel.SideInputKind.War&&p.Status=="請輸入要宣戰的對象血盟名稱。","宣戰：輸入框 577",p.Status);
        Type(p,Enemy);p.Key(Down(Keys.Enter),cmd);
        p.Mouse(Click(p.WarStateRect(1)),cmd);t.Equal("請輸入要停戰的血盟名稱。",p.Status,"停戰：輸入框 579");
        Type(p,"甲");p.Key(Down(Keys.Enter),cmd);
        p.Mouse(Click(p.WarStateRect(2)),cmd);t.Equal("請輸入要投降的血盟名稱。",p.Status,"投降：輸入框 578");
        Type(p,"乙");p.Key(Down(Keys.Enter),cmd);
        t.Check(src.Wars.SequenceEqual([(ClanWarCommand.Declare,Enemy),(ClanWarCommand.Cease,"甲"),(ClanWarCommand.Surrender,"乙")]),"三個狀態依序送 C227 0／3／2",src.Wars);
        p.Mouse(Click(p.WarStateRect(2)),cmd);p.Key(Down(Keys.Enter),cmd);
        t.Equal(3,src.Wars.Count,"盟戰名稱空白不送");
        src.Accept=false;p.Mouse(Click(p.WarStateRect(0)),cmd);Type(p,"丁");p.Key(Down(Keys.Enter),cmd);
        t.Check(p.Status.Contains("未送出"),"本機檢查擋下：提示看聊天欄",p.Status);

        // 字串表有 3361 時用表的文字；4118 超出表筆數仍是占位。
        using(ServerMessageText.UseTable(Strings())){
            t.Equal("T3361",PledgePanel.PromptText(3361),"有字串表：3361 照表");
            t.Equal("〔字串 4118〕",PledgePanel.PromptText(4118),"4118 超出表（3484 筆）：占位");
        }
        // 畫面：勾選框與按鈕有畫（不丟例外）。
        p.Mouse(Click(p.AttentionRow(0)),cmd);
        using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        var box=p.AttentionCheckRect(0);int lit=0;
        for(int y=box.Top+3;y<box.Bottom-3;y++)for(int x=box.Left+3;x<box.Right-3;x++)if(canvas.Bitmap.GetPixel(x,y).ToArgb()==Color.White.ToArgb())lit++;
        t.Check(lit>0,"勾選列畫出勾選記號",lit);
        p.Close();t.Check(p.CheckedAttention.Count==0&&p.SideInput==PledgePanel.SideInputKind.None,"關窗清掉勾選與輸入");
    }
}
