using System.Reflection;
using System.Text.Json;
namespace P06;

// R6-B（T338／#784）離線測試：滑鼠指到自己時，名稱、稱號、盟徽的 X 改以 P.x 為基準
// （AN§2.3「自己：腳下格的螢幕 X − 3·len − 56」；FU8§1.1：P＝sub_F1A750(obj+16)＝Coordinates.Screen −(24,12)，
// sub_EFF300 自己分支 X0＝P.x − 3·len − 56）。他人、全名模式、頭上盟徽不變。
// 只用合成 S87 封包，不開遊戲、網路、視窗、伺服器、DB、素材，不寫檔。由 --module-tests 自動發現執行。
static class SelfNameLayoutTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Other=790,ClanId=77;
    const ushort SelfX=32600,SelfY=32940;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87：x y id gfx 姿勢 方向 光 速度 exp lawful 名稱 稱號 旗標 盟id 盟名 主人 rank hp brave 等級 unknown tail1 tail2。
    static byte[] Appear(uint id,ushort x,ushort y,string name,string title="",uint clanId=0,string clan="")=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(61),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(title),[0],Le32(clanId),Str(clan),Str(""),[0,100,0,30,0,255,255]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static WorldSession Seed(string title,uint clanId,string clan){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",1);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":30,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        w.Receive(Appear(Me,SelfX,SelfY,"offline",title,clanId,clan),"offline");
        w.Receive(Appear(Other,32603,SelfY,"路人"),"offline");
        return w;
    }

    [WorldModuleTest("self-name-layout")]
    static void Run(WorldModuleTestContext t){Formula(t);HoverLayout(t);Plans(t);}

    // 純公式：P.x＝Screen.X − 24；X0＝P.x − 56 − 3·len（6 px／byte 的一半）。
    static void Formula(WorldModuleTestContext t){
        // Screen(32600,32940)＝(24·(24+44)+24, …)＝1656 → P.x＝1632。
        t.Equal(1656f,Coordinates.Screen(SelfX,SelfY).X,"Coordinates.Screen X of the self tile (fixture)");
        t.Equal(1632,NameLayout.SelfOriginX(SelfX,SelfY),"P.x = Screen.X − 24 (sub_F1A750 origin, same as CrownLayout)");
        t.Equal(1632-56-3*7,NameLayout.SelfX(1632,"offline"),"ASCII name: X0 = P.x − 56 − 3·7");
        t.Equal(1632-56-3*4,NameLayout.SelfX(1632,"測試"),"Big5 name: len counts bytes (2 per character)");
        foreach(var s in new[]{"a","offline","測試角色","GM管理員xyz"})
            t.Equal(1632-56,NameLayout.SelfX(1632,s)+NameLayout.GlyphWidth*NameLayout.ByteLength(s)/2,$"text '{s}' is centred on P.x − 56 regardless of length");
    }

    // Hover：有 selfOriginX 時 X 不看外框；Y 和他人一樣看外框 T。
    static void HoverLayout(WorldModuleTestContext t){
        var a=new Rectangle(1600,700,48,64);var b=new Rectangle(1500,650,90,80);int p=1632;
        var sa=NameLayout.Hover(a,"稱號","offline",true,false,p);var sb=NameLayout.Hover(b,"稱號","offline",true,false,p);
        int x0=NameLayout.SelfX(p,"offline");
        t.Equal(new PointF(x0+NameLayout.ClanNameShift,700-3+13),sa.Name,"self name: X0 + 10 with clan, Y = T − 3 + 13 with a title");
        t.Equal(new PointF(x0-20,700-4+13),sa.Emblem,"self emblem: name X0 − 20, Y = T − 4 + 13");
        t.Equal((PointF?)new PointF(NameLayout.SelfX(p,"稱號"),700-3),sa.Title,"self title: its own X0 = P.x − 56 − 3·len, Y = T − 3");
        t.Check(sa.Name.X==sb.Name.X&&sa.Emblem.X==sb.Emblem.X&&sa.Title!.Value.X==sb.Title!.Value.X,"self X ignores the frame bounds (does not jump with the animation frame)");
        t.Check(sa.Name.Y!=sb.Name.Y,"self Y still follows the frame top");
        var noClan=NameLayout.Hover(a,"","offline",false,false,p);
        t.Equal(new PointF(x0,700-3),noClan.Name,"self without clan or title: X0, T − 3");
        t.Check(noClan.Title==null,"no title line when the title is empty");
        var other=NameLayout.Hover(a,"稱號","offline",true,false);
        t.Equal(new PointF(NameLayout.CenterX(a,"offline")+10,700-3+13),other.Name,"others unchanged: L + (W − 6·len)/2");
        t.Equal((PointF?)new PointF(NameLayout.CenterX(a,"稱號"),700-3),other.Title,"others' title unchanged");
    }

    // 從合成 S87 走完 NameLayout.Plan。
    static void Plans(WorldModuleTestContext t){
        var w=Seed("騎士",ClanId,"測試盟");
        t.Check(w.Actor is {ObjectId:Me,X:SelfX,Y:SelfY,ClanId:ClanId,Title:"騎士"},"fixture: S87 for our own id fills Actor",w.Actor);
        var bounds=new Rectangle(1610,690,48,64);
        var targets=new[]{new NameTarget(Me,"offline",bounds,true),new NameTarget(Other,"路人",new(1700,690,48,64),false)};
        var hover=NameLayout.Plan(w,targets,Me,false);
        int p=NameLayout.SelfOriginX(SelfX,SelfY),x0=NameLayout.SelfX(p,"offline");
        var name=hover.Texts.Single(x=>!x.Title);var title=hover.Texts.Single(x=>x.Title);
        t.Equal(new PointF(x0+10,690-3+13),name.Position,"hovered self: name at P.x − 56 − 3·len + 10 (clan)");
        t.Equal(new PointF(NameLayout.SelfX(p,"騎士"),690-3),title.Position,"hovered self: title at P.x − 56 − 3·len(title)");
        var emblem=hover.Emblems.Single(e=>!e.OverHead);
        t.Equal(new PointF(x0-20,690-4+13),emblem.Position,"hovered self: emblem follows the name X0 − 20");
        var moved=NameLayout.Plan(w,[targets[0] with {Bounds=new(1590,670,70,90)}],Me,false);
        t.Equal(name.Position.X,moved.Texts.Single(x=>!x.Title).Position.X,"self name X does not depend on the frame bounds");
        // 自己有血盟：頭上盟徽（sub_F09070）照舊用外框置中。
        var head=hover.Emblems.Where(e=>e.OverHead&&e.ObjectId==Me).ToArray();
        t.Check(head.Length==1&&head[0].Position==NameLayout.HeadEmblem(bounds),"head emblem unchanged (L + (W − 16)/2)",head);
        var otherHover=NameLayout.Plan(w,targets,Other,false).Texts.Single();
        t.Equal(new PointF(NameLayout.CenterX(targets[1].Bounds,"路人"),690-3),otherHover.Position,"hovered other: centred on bounds as before");
        // 全名模式：sub_F01380 沒查到自己分支，保持外框置中。
        var all=NameLayout.Plan(w,targets,null,true).Texts.Single(x=>x.ObjectId==Me);
        t.Equal(new PointF(NameLayout.CenterX(bounds,"offline")+10,690-16),all.Position,"all-names mode: self unchanged");
        // 沒有 Actor 時退回外框置中（純 Hover，selfOriginX＝null）。
        t.Equal(new PointF(NameLayout.CenterX(bounds,"offline"),690-3),NameLayout.Hover(bounds,"","offline",false,false,null).Name,"no actor: falls back to bounds centring");
    }
}
