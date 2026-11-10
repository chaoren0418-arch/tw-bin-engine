using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R7-G（T346／#809）離線測試：合成封包＋合成字串表、記憶體內假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：897 的字（表有／沒有 %s、沒有表時的原文）與色 24；14 一次 >8 筆 → 897 進聊天列、不送、窗不關；
// 13 合計 >99999 → 1028 進聊天輸入列提示、記下待確認的一包；答 Y 才送（同一包、照舊驗證），答其他不送；
// 清單／目標變了確認失效；原生窗開著時 WinForms 私商面板讓位（PrivateShopLegacyYields）。
static class PrivateShopFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Owner=456,Gold=999;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    // 封包格式同 PrivateShopTradeTests（S15 背包、S140 買／賣、S87 擺攤中的店主）。
    static byte[] Item(uint id,ushort icon,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write((byte)0);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);});
    static byte[] BuyList(params (byte order,ushort icon,uint listed,uint price,string name)[] rows)=>Packet(b=>{b.Write((byte)140);b.Write((byte)0);b.Write(Owner);b.Write((ushort)Math.Max(rows.Length,rows.Select(r=>r.order+1).DefaultIfEmpty(0).Max()));
        foreach(var r in rows){b.Write(r.order);b.Write((byte)0);b.Write(r.icon);b.Write(r.listed);b.Write(r.price);Text(b,r.name);b.Write((byte)0);}});
    static byte[] SellList(ushort declared,params (byte order,uint item,uint listed,uint price)[] rows)=>Packet(b=>{b.Write((byte)140);b.Write((byte)1);b.Write(Owner);b.Write(declared);
        foreach(var r in rows){b.Write(r.order);b.Write(r.item);b.Write(r.listed);b.Write(r.price);}});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Owner);b.Write((ushort)1110);b.Write((byte)70);b.Write((byte)6);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);Text(b,"shopkeeper");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(Owner);Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,Item(Gold,5,500000,"$4"));
        for(uint i=0;i<9;i++)Feed(c,Item(901+i,(ushort)(20+i),50,"goods"+i));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static PrivateShopResult Query(LoginConnection c,bool sell)=>c.SendPrivateShop(true,sell,0,0,c.PrivateShopSnapshot()).GetAwaiter().GetResult();
    static PrivateShopResult Batch(LoginConnection c,int revision,params ShopChoice[] rows)=>c.SendPrivateShopBatch(rows,revision).GetAwaiter().GetResult();
    static PrivateShopResult Answer(LoginConnection c,string typed)=>c.AnswerPrivateShopConfirm(typed).GetAwaiter().GetResult();
    static string Hex(byte[] p)=>Convert.ToHexString(p);
    static byte[] Le(uint v)=>BitConverter.GetBytes(v);
    static ServerStringTable Table(params (int id,string text)[] rows){
        int count=rows.Max(r=>r.id)+1;var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\n"+string.Join("\n",lines)+"\n"));
    }

    [WorldModuleTest("private-shop-followup")]
    static void Run(WorldModuleTestContext t){
        Texts(t);
        using(ServerMessageText.UseTable(null)){SellRows(t);BuyConfirm(t);ConfirmStale(t);Yields(t);}
    }

    static void Texts(WorldModuleTestContext t){
        t.Check(PrivateShopTrade.RowsMessage==897&&PrivateShopTrade.RowsArgument==934&&PrivateShopTrade.TotalMessage==1028,"string ids 897 / 934 / 1028");
        t.Check(PrivateShopTrade.RowsColor==24&&PrivateShopTrade.RowsSecondColor==14,"897 colour palette[24] / palette[14] (sub_7DC850 args)");
        using(ServerMessageText.UseTable(null)){
            t.Equal("不能一次販賣8樣以上的物品。",PrivateShopTrade.RowsText(),"897 without a table = original text");
            t.Equal("這是高價物品。請再次進行確認，確定要購買嗎？(Y/N)",PrivateShopTrade.TotalText(),"1028 without a table = original text");
        }
        // 原版 897 沒有 %s：sprintf 的 934 不出現。
        using(ServerMessageText.UseTable(Table((897,"不能一次賣8樣。"),(934,"販售"),(1028,"\\f1高價確認(Y/N)")))){
            t.Equal("不能一次賣8樣。",PrivateShopTrade.RowsText(),"897 without %s: 934 ignored");
            t.Equal("高價確認(Y/N)",PrivateShopTrade.TotalText(),"1028 from the table, \\f1 removed");
        }
        using(ServerMessageText.UseTable(Table((897,"一次只能%s八樣。"),(934,"販售")))){
            t.Equal("一次只能販售八樣。",PrivateShopTrade.RowsText(),"897 with %s: 934 substituted");
        }
        var a=new PrivateShopRow(0,2,0,0,"a",10,30,30);
        t.Check(PrivateShopTrade.NeedsConfirm(false,[(a with{Price=100000},1)])&&!PrivateShopTrade.NeedsConfirm(false,[(a with{Price=99999},1)]),"1028 only above 99999");
        t.Check(!PrivateShopTrade.NeedsConfirm(true,[(a with{Price=100000},1)]),"sell never asks 1028");
        var nine=Enumerable.Range(0,9).Select(_=>(a,1u)).ToArray();
        t.Check(PrivateShopTrade.TooManyRows(true,nine)&&!PrivateShopTrade.TooManyRows(true,nine[..8])&&!PrivateShopTrade.TooManyRows(false,nine),"897 only for sell > 8 rows");
    }

    static void SellRows(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;w.PrivateShopNativeWindow=true;var src=new PrivateShopTradeSource(c,s=>s,()=>false);
        Query(c,true);Feed(c,SellList(9,Enumerable.Range(0,9).Select(i=>((byte)i,901u+(uint)i,30u,7u)).ToArray()));
        int rev=src.Current!.Revision,lines=w.PlayerChatLines.Length,chatRev=w.PlayerChatRevision,before=sent.Count;
        string notice=c.PlayerChatSnapshot().Notice;
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,Enumerable.Range(0,9).Select(i=>new ShopChoice((uint)i,1)).ToArray()),"9 rows refused");
        t.Equal(before,sent.Count,"897: no packet");
        t.Check(w.PlayerChatLines.Length==lines+1&&w.PlayerChatRevision==chatRev+1,"897: one chat line");
        var last=w.PlayerChatLines[^1];
        t.Check(last.Text==PrivateShopTrade.RowsText()&&last.ColorIndex==PrivateShopTrade.RowsColor&&last.Speaker==0,"897 on the chat line, colour 24, no speaker",last);
        t.Equal(notice,c.PlayerChatSnapshot().Notice,"897 is not an input-line prompt");
        t.Check(c.PrivateShopConfirmCurrent==null,"897 leaves nothing to confirm");
        t.Check(src.Current?.Revision==rev&&w.PrivateShopPhase=="list","897: window stays, same revision");
        t.Equal(PrivateShopResult.NotSent,Answer(c,"y"),"Y after 897 sends nothing");
        t.Equal(before,sent.Count,"still no packet");
        // 8 筆以內照常一包，不印 897。
        lines=w.PlayerChatLines.Length;
        t.Equal(PrivateShopResult.Accepted,Batch(c,rev,new(0,1),new(1,1)),"2 rows accepted");
        t.Check(sent.Count==before+1&&w.PlayerChatLines.Length==lines,"accepted batch: one packet, no chat line");
    }

    static void BuyConfirm(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;w.PrivateShopNativeWindow=true;var src=new PrivateShopTradeSource(c,s=>s,()=>false);
        t.Equal(PrivateShopResult.NotSent,Answer(c,"y"),"nothing pending → NotSent");
        Query(c,false);Feed(c,BuyList((0,11,10,90000,"gem"),(1,12,20000,1,"pebble")));
        int rev=src.Current!.Revision,lines=w.PlayerChatLines.Length,before=sent.Count;
        // 1. 合計 180000：不送；1028 進聊天輸入列提示，不進聊天紀錄；窗不關。
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,new ShopChoice(0,2)),"total 180000 asks first");
        t.Equal(before,sent.Count,"1028: no packet yet");
        t.Equal(PrivateShopTrade.TotalText(),c.PlayerChatSnapshot().Notice,"1028 in the input-line prompt");
        t.Equal(lines,w.PlayerChatLines.Length,"1028 is not a chat log line");
        t.Check(c.PrivateShopConfirmCurrent is {Revision:var r,Owner:Owner,Choices:[{Id:0,Count:2}]}&&r==rev,"pending confirm keeps the batch",c.PrivateShopConfirmCurrent);
        t.Check(src.Current?.Revision==rev&&w.PrivateShopPhase=="list","1028: window stays, same revision");
        // 2. 答 N：不送、提示收掉、窗還在。
        t.Equal(PrivateShopResult.NotSent,Answer(c,"n"),"N → not sent");
        t.Check(sent.Count==before&&c.PrivateShopConfirmCurrent==null&&c.PlayerChatSnapshot().Notice=="","N clears the prompt",c.PlayerChatSnapshot().Notice);
        t.Check(src.Current?.Revision==rev,"N: window stays");
        t.Equal(PrivateShopResult.NotSent,Answer(c,"y"),"Y after N → nothing pending");
        // 3. 再按一次 → 再問；答 Y（全形也算）→ 同一包送出。
        Batch(c,rev,new ShopChoice(0,2));t.Check(c.PrivateShopConfirmCurrent!=null,"asks again");
        t.Equal(PrivateShopResult.Accepted,Answer(c,"Ｙes"),"Y → sent");
        t.Equal(before+1,sent.Count,"one C161 after Y");
        t.Equal(Hex(LoginPackets.Pad([161,..Le(Owner),0,1,0,..Le(0),..Le(2)])),Hex(sent[^1]),"Y sends the confirmed batch");
        t.Check(w.PrivateShopPhase=="submitted"&&src.Current==null&&c.PrivateShopConfirmCurrent==null,"after Y: submitted, window closed, nothing pending");
        t.Check(c.PlayerChatSnapshot().Notice!=PrivateShopTrade.TotalText(),"1028 prompt removed after Y");
        t.Equal(PrivateShopResult.NotSent,Answer(c,"y"),"second Y never resends");
        t.Equal(before+1,sent.Count,"still one packet");
        // 4. 合計剛好 99999 不問、直接送。
        var sent2=new List<byte[]>();using var d=Seed(sent2);d.World.PrivateShopNativeWindow=true;
        Query(d,false);Feed(d,BuyList((0,11,10,90000,"gem"),(1,12,20000,1,"pebble")));
        t.Equal(PrivateShopResult.Accepted,Batch(d,d.World.PrivateShopRevision,new(0,1),new(1,9999)),"total 99999 sent without 1028");
        t.Check(d.PrivateShopConfirmCurrent==null,"no prompt at the limit");
    }

    static void ConfirmStale(WorldModuleTestContext t){
        // 新清單（版本變）→ 確認失效，Y 不送。
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;w.PrivateShopNativeWindow=true;
        Query(c,false);Feed(c,BuyList((0,11,10,90000,"gem")));
        Batch(c,w.PrivateShopRevision,new ShopChoice(0,2));t.Check(c.PrivateShopConfirmCurrent!=null,"pending");
        int n=sent.Count;Query(c,false);Feed(c,BuyList((0,11,10,90000,"gem")));
        t.Check(c.PrivateShopConfirmCurrent==null,"new list → confirm stale");
        t.Equal(PrivateShopResult.NotSent,Answer(c,"y"),"stale Y not sent");
        t.Equal(n+1,sent.Count,"only the query C47 was sent");
        // 關清單 → 失效。
        Batch(c,w.PrivateShopRevision,new ShopChoice(0,2));t.Check(c.PrivateShopConfirmCurrent!=null,"pending again");
        w.ClosePrivateShop();t.Check(c.PrivateShopConfirmCurrent==null,"closed list → confirm stale");
        // 正常送出一包 → 舊確認作廢。
        Query(c,false);Feed(c,BuyList((0,11,10,90000,"gem"),(1,12,20000,1,"pebble")));int rev=w.PrivateShopRevision;
        Batch(c,rev,new ShopChoice(0,2));t.Check(c.PrivateShopConfirmCurrent!=null,"pending third time");
        n=sent.Count;t.Equal(PrivateShopResult.Accepted,Batch(c,rev,new ShopChoice(1,5)),"small batch sent directly");
        t.Check(c.PrivateShopConfirmCurrent==null&&sent.Count==n+1,"direct send drops the old confirm");
        // 原生窗沒接上（--classic-ui）→ 確認失效。
        var sent2=new List<byte[]>();using var d=Seed(sent2);d.World.PrivateShopNativeWindow=true;
        Query(d,false);Feed(d,BuyList((0,11,10,90000,"gem")));Batch(d,d.World.PrivateShopRevision,new ShopChoice(0,2));
        d.World.PrivateShopNativeWindow=false;t.Check(d.PrivateShopConfirmCurrent==null,"native window detached → confirm stale");
    }

    static void Yields(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        t.Check(!c.PrivateShopLegacyYields,"idle: WinForms panel stays");
        Query(c,false);Feed(c,BuyList((0,11,5,300,"potion")));
        t.Check(!c.PrivateShopLegacyYields,"--classic-ui (native not attached): WinForms panel stays");
        w.PrivateShopNativeWindow=true;
        t.Check(c.PrivateShopLegacyYields,"native mode 13 open → WinForms panel yields");
        w.ClosePrivateShop();t.Check(!c.PrivateShopLegacyYields,"closed → WinForms panel back");
        Query(c,true);Feed(c,SellList(1,(0,901,30,7)));
        t.Check(c.PrivateShopLegacyYields,"native mode 14 open → WinForms panel yields");
        Query(c,false);Feed(c,BuyList());
        t.Check(!c.PrivateShopLegacyYields,"empty list → no native window → WinForms panel stays");
    }
}
