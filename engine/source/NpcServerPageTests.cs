using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// W2-A（T234／#556，windows-2.md §4）：伺服器主動送、物件 id 是自己的 S39 頁（monlist、pledge 等）的離線測試。
// 只用合成封包、合成模板與記憶體送出器；不開遊戲、網路或素材。
static class NpcServerPageTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static void Set(object o,string name,object? value){
        for(var t=o.GetType();t!=null;t=t.BaseType){
            if(t.GetProperty(name,F|BindingFlags.DeclaredOnly) is {} p){p.SetValue(o,value);return;}
            if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f){f.SetValue(o,value);return;}
        }
        throw new MissingMemberException(o.GetType().Name,name);
    }
    static byte[] Packet(Action<BinaryWriter> write){using var m=new MemoryStream();using(var b=new BinaryWriter(m))write(b);return LoginPackets.Pad(m.ToArray());}
    static void Text(BinaryWriter b,string s){b.Write(LoginPackets.TextEncoding.GetBytes(s));b.Write((byte)0);}
    // 3.81 S_HYPERTEXT 欄位：C(39) D(objid) S(htmlid) H(flag) H(count) S…（S_ShowPolyList 只送 objid＋"monlist"）。
    static byte[] Hyper(uint id,string html,params string[] args)=>Packet(b=>{b.Write((byte)39);b.Write(id);Text(b,html);b.Write((ushort)(args.Length>0?1:0));b.Write((ushort)args.Length);foreach(var a in args)Text(b,a);});
    static byte[] ClosePage(uint id)=>Packet(b=>{b.Write((byte)39);b.Write(id);Text(b,"");b.Write((ushort)0);b.Write((ushort)0);});
    static NpcTemplates Templates(Dictionary<string,string> rows){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,new Dictionary<string,string>(rows,StringComparer.OrdinalIgnoreCase));
        return t;
    }
    static readonly Dictionary<string,string> Rows=new(){
        ["monlist"]="<html>變身清單 <var src=\"#0\"><br><a action=\"polyA\">變身甲</a><a action=\"polyB\">變身乙</a><a link=\"monlist2\">下一頁</a><a action=\"checkpk\">查詢</a></html>",
        ["monlist2"]="<html>第二頁 <var src=\"#0\"><br><a action=\"polyC\">變身丙</a></html>",
        ["npcpage"]="<html>NPC<br><a action=\"buy\">購買</a></html>",
    };
    static LoginConnection Seed(bool templates=true){
        var c=new LoginConnection();var w=c.World;
        Set(c,"State",LoginState.InWorld);Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",1);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Me},\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}}"));
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>($"{{\"ObjectId\":{Me},\"Name\":\"測試者\",\"Gfx\":0,\"X\":32600,\"Y\":32940,\"HpPercent\":100}}"));
        if(templates)w.DialogTemplates=Templates(Rows);
        return c;
    }
    static List<byte[]> Sender(LoginConnection c,Func<bool>? allow=null){
        var sent=new List<byte[]>();
        Func<byte[],Func<bool>,Action,Task<bool>> gate=(b,valid,admit)=>{if(!valid()||allow?.Invoke()==false)return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
        Set(c,"sendMovementGate",gate);return sent;
    }
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();

    [WorldModuleTest("npc-server-page")]
    static void Checks(WorldModuleTestContext t){
        // 1. 物件 id 是自己的 S39：不再丟掉，放在 NPC 面板的伺服器頁；選項可用；NPC 對話不受影響。
        using(var c=Seed()){
            var w=c.World;t.Check(w.Ready,"seed session is ready");
            int interaction=w.InteractionRevision;
            w.Receive(Hyper(Me,"monlist","測試者"),"");
            t.Check(w.QueryVisible&&w.QueryActionable&&w.QueryHtml=="monlist","self monlist shown as actionable server page");
            t.Check(w.QueryPage!.Text.StartsWith("變身清單 測試者",StringComparison.Ordinal),"template rendered with packet argument",w.QueryPage.Text);
            t.Equal(4,w.QueryPage.Options.Count(o=>o.Enabled),"template options enabled on server page");
            t.Check(w.InteractionKind=="closed"&&w.InteractionRevision==interaction&&w.CanStartInteraction,"server page does not open or block NPC interaction");
            var snap=JsonSerializer.SerializeToElement(w.InteractionSnapshot()).GetProperty("query");
            t.Check(snap.GetProperty("actionable").GetBoolean()&&snap.GetProperty("options").GetArrayLength()==4,"snapshot exposes actionable options");

            // 2. action → C125，物件 id＝自己；送出一次後關頁、不重送。
            var sent=Sender(c);int rev=w.QueryRevision;
            t.Check(Run(c.QueryOption(0,rev,w.WorldEpoch)),"action option admitted");
            t.Equal(1,sent.Count,"one packet sent");
            t.Equal(Convert.ToHexString(NpcPackets.Action(Me,"polyA")),Convert.ToHexString(sent[0]),"C125 with self objid and action value");
            t.Check(!w.QueryVisible&&!w.QueryActionable,"page closed after send");
            t.Check(!Run(c.QueryOption(1,rev,w.WorldEpoch))&&sent.Count==1,"stale revision after send is refused (no resend)");
        }
        // 3. link → 本地換頁（沿用同一組參數），不送封包；新頁的 action 照樣回 C125。
        using(var c=Seed()){
            var w=c.World;var sent=Sender(c);
            w.Receive(Hyper(Me,"monlist","測試者"),"");int rev=w.QueryRevision;
            t.Check(Run(c.QueryOption(2,rev,w.WorldEpoch))&&sent.Count==0,"link option handled locally without packet");
            t.Check(w.QueryHtml=="monlist2"&&w.QueryRevision==rev+1&&w.QueryPage!.Text.StartsWith("第二頁 測試者",StringComparison.Ordinal),"link renders next page with same arguments",w.QueryPage?.Text);
            t.Check(!Run(c.QueryOption(0,rev,w.WorldEpoch))&&sent.Count==0,"old revision refused after page change");
            t.Check(Run(c.QueryOption(0,w.QueryRevision,w.WorldEpoch))&&Convert.ToHexString(sent[^1])==Convert.ToHexString(NpcPackets.Action(Me,"polyC")),"linked page action sends C125");
        }
        // 4. checkpk 照 HyperAct 規則送 C51；送出閘門拒絕時頁面保留、不計送出。
        using(var c=Seed()){
            var w=c.World;bool allow=false;var sent=Sender(c,()=>allow);
            w.Receive(Hyper(Me,"monlist"),"");int rev=w.QueryRevision;
            t.Check(!Run(c.QueryOption(3,rev,w.WorldEpoch))&&sent.Count==0&&w.QueryVisible,"rejected send keeps page");
            allow=true;
            t.Check(Run(c.QueryOption(3,rev,w.WorldEpoch))&&Convert.ToHexString(sent[^1])==Convert.ToHexString(WeaponRepairPackets.CheckPk()),"checkpk on server page sends C51");
        }
        // 5. NPC 對話進行中：伺服器頁並存，不取代 NPC 頁；NPC 頁照舊接受 NPC id 的 S39。
        using(var c=Seed()){
            var w=c.World;
            typeof(WorldSession).GetMethod("BeginTalk",F)!.Invoke(w,null);
            // 合成 session 沒有選取目標；這裡只確認自己的頁不改「等待 NPC 回包」的狀態。
            t.Equal("waiting",w.InteractionKind,"NPC talk pending");
            int interaction=w.InteractionRevision;string kind=w.InteractionKind;
            w.Receive(Hyper(Me,"monlist"),"");
            t.Check(w.QueryActionable&&w.InteractionRevision==interaction&&w.InteractionKind==kind,"server page leaves pending NPC context untouched");
        }
        // 6. 空 htmlid 關閉伺服器頁；但不關玩家查詢結果（y_who 等仍由 PlayerQuery 處理）。
        using(var c=Seed()){
            var w=c.World;w.Receive(Hyper(Me,"pledge"),"");
            t.Check(w.QueryActionable&&w.QueryHtml=="pledge"&&w.QueryPage!.Text=="缺少此對話的原始繁中模板：pledge","missing template still shown on server page");
            w.Receive(ClosePage(Me),"");
            t.Check(!w.QueryVisible,"empty htmlid closes server page");
            int rev=w.QueryRevision;w.Receive(ClosePage(Me),"");
            t.Equal(rev,w.QueryRevision,"empty htmlid with no server page is a no-op");
        }
        // 7. 不是自己、也不是對話中 NPC 的 S39 仍丟掉；未就緒時自己的 S39 也不開頁。
        using(var c=Seed()){
            var w=c.World;w.Receive(Hyper(789,"monlist"),"");
            t.Check(!w.QueryVisible&&w.InteractionKind=="closed","foreign S39 still dropped");
            Set(w,"TimeReplies",0);w.Receive(Hyper(Me,"monlist"),"");
            t.Check(!w.QueryVisible,"self S39 ignored before world is ready");
        }
        // 8. 世界切換後頁面失效，選項不能再送。
        using(var c=Seed()){
            var w=c.World;var sent=Sender(c);w.Receive(Hyper(Me,"monlist"),"");int rev=w.QueryRevision,epoch=w.WorldEpoch;
            Set(w,"WorldEpoch",epoch+1);
            t.Check(!w.QueryVisible&&!Run(c.QueryOption(0,rev,epoch))&&!Run(c.QueryOption(0,rev,w.WorldEpoch))&&sent.Count==0,"world change invalidates server page");
        }
        // 9. 網頁命令契約：npcQueryOption → query-option。
        var cmd=NpcWebCommand.Parse(JsonDocument.Parse("{\"v\":1,\"id\":1,\"cmd\":\"npcQueryOption\",\"revision\":3,\"epoch\":0,\"targetRevision\":0,\"option\":2}").RootElement);
        t.Check(cmd.Command=="query-option"&&cmd.Option==2&&cmd.Revision==3,"web npcQueryOption contract");
        t.Throws<FormatException>(()=>NpcWebCommand.Parse(JsonDocument.Parse("{\"v\":1,\"id\":1,\"cmd\":\"npcQueryOption\",\"revision\":3,\"epoch\":0,\"targetRevision\":0}").RootElement),"npcQueryOption requires option");
        // 10. 畸形封包照常拒絕。
        using(var c=Seed()){
            t.Throws<ProtocolException>(()=>c.World.Receive(Packet(b=>{b.Write((byte)39);b.Write(Me);Text(b,"monlist");b.Write((ushort)0);b.Write((ushort)3);}),""),"flag 0 with arguments rejected");
        }
    }
}
