using System.Reflection;
using System.Text.Json;
namespace P06;

// R2-I（T257／#618）物件繪製收尾的離線測試：缺圖退回（AN§3.4）、名稱旁圖示與 +252 行（AN§2.3）、全名模式過濾、
// 怪物等級差外框（AN§2.4）、幽靈中略過自己的 S76（WO §4）。只用合成封包與假外觀表，不開遊戲、網路、視窗或素材。
static class ObjectDrawTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Pet=456,Shop=789,Other=790;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87：x y id gfx 姿勢 方向 光 速度 exp lawful 名稱 稱號 旗標 盟id 盟名 主人 rank hp brave 等級 unknown tail1 tail2。
    static byte[] Appear(uint id,ushort x,string name,ushort gfx,string title="",byte flags=0,string owner="",byte level=0,byte weapon=0)=>
        P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[weapon,0,0,0],Le32(0),Le16(0),Str(name),Str(title),[flags],Le32(0),Str(""),Str(owner),[0,100,0,level,0,255,255]);
    static byte[] Poly(uint id,ushort gfx,bool takeoff)=>P([76],Le32(id),Le16(gfx),Le16((ushort)(takeoff?0:29)));
    static byte[] ShopAction(uint id,string text)=>P([158],Le32(id),[70],Str(text));
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}

    static WorldSession Seed(byte selfFlags=0){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",1);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":30,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        w.Receive(Appear(Me,32600,"offline",61,flags:selfFlags),"offline");
        return w;
    }

    [WorldModuleTest("object-draw")]
    static void Run(WorldModuleTestContext t){
        Appearances(t);Actions(t);Icons(t);Plans(t);Ghost(t);Crowns(t);
    }

    // SS§1／FU3§2：crown.raw 解碼、錨點、可見條件、不裁切。
    static void Crowns(WorldModuleTestContext t){
        t.Equal(4608,CastleCrownImages.PayloadBytes,"crown.raw = 12 × 16 × 12 × 2 bytes");
        var raw=new byte[4608];raw[384*3+2*(16*5+7)]=0x1F;raw[384*3+2*(16*5+7)+1]=0x7C;
        var img=CastleCrownImages.Parse(raw);
        t.Equal((ushort)0x7C1F,img.Frame(3)[16*5+7],"frame 3 row 5 col 7 little-endian RGB555");
        t.Check(img.Frame(0).ToArray().All(v=>v==0)&&img.Frame(12).IsEmpty,"other frames transparent; frame 12 out of range");
        t.Throws<InvalidDataException>(()=>CastleCrownImages.Parse(new byte[4607]),"wrong length rejected");
        t.Throws<IOException>(()=>CastleCrownImages.Load(Path.Combine(AppContext.BaseDirectory,"no-such-client")),"missing client: load error, no substitute art");
        // 錨點：精靈原點＝Screen −(24,12)；x＋17、y − 40／50 − 12。
        var s=Coordinates.Screen(32600,32940);
        t.Equal(new PointF(s.X-24+17,s.Y-12-40-12),CrownLayout.Position(32600,32940,61),"crown anchor normal height 40");
        t.Equal(new PointF(s.X-24+17,s.Y-12-50-12),CrownLayout.Position(32600,32940,6094),"gfx 6094 height 50");
        t.Check(CrownLayout.Tall(6080)&&CrownLayout.Tall(6094)&&!CrownLayout.Tall(61),"tall gfx 6080/6094 only");

        var w=Seed(0x02);long clock=0;w.CastleClock=()=>clock;
        w.Receive(Appear(Other,32603,"路人",1110),"offline");w.Receive(Appear(Pet,32604,"隱身",1110,flags:0x02),"offline");w.Receive(Appear(Shop,32605,"二城",1110),"offline");
        static byte[] Master(byte c,uint id)=>P([CastleProtocol.SCastleMaster,c],Le32(id));
        w.Receive(Master(1,Me),"offline");w.Receive(Master(4,Other),"offline");w.Receive(Master(5,Pet),"offline");w.Receive(Master(2,Shop),"offline");
        CrownTarget T(uint id,bool self=false)=>new(id,61,32600,32940,self);
        var plan=CrownLayout.Plan(w,[T(Me,true),T(Other),T(Pet),T(Shop)]);
        t.Check(plan.Any(c=>c is {ObjectId:Me,Castle:1,Self:true}),"self crowned even with own invisible flag",plan);
        t.Check(plan.Any(c=>c is {ObjectId:Other,Castle:4,Frame:0}),"other castle-4 owner crowned",plan);
        t.Check(plan.All(c=>c.ObjectId!=Pet),"invisible other (Flags & 2) not crowned",plan);
        t.Check(plan.All(c=>c.ObjectId!=Shop),"castle 2 owner not crowned",plan);
        clock=1500;t.Equal(3,CrownLayout.Plan(w,[T(Other)]).Single().Frame,"500 ms per frame");
        Set(w,"BlindMode",(ushort)1);
        var blind=CrownLayout.Plan(w,[T(Me,true),T(Other)]);
        t.Check(blind.Length==1&&blind[0].Self,"blind mode 1: only own crown (推測，同名稱)",blind);
        var view=new SizeF(800,600);
        t.Check(CrownLayout.Fits(new(10,10),1,view,false),"inside view drawn");
        t.Check(!CrownLayout.Fits(new(10,0),1,view,false)&&CrownLayout.Fits(new(10,0),1,view,true),"other needs y > 0; self y = 0 ok");
        t.Check(!CrownLayout.Fits(new(-1,10),1,view,true)&&!CrownLayout.Fits(new(785,10),1,view,true)&&!CrownLayout.Fits(new(10,589),1,view,true),"no edge clipping: partial crowns skipped");
        t.Check(CrownLayout.Fits(new(784,588),1,view,true)&&!CrownLayout.Fits(new(784,588),2,view,true),"zoom scales the 16×12 box");
        if(Environment.GetEnvironmentVariable("TW_MODULE_CLIENT_CHECKS")=="1"){
            var real=CastleCrownImages.Load();int[] opaque=[43,37,27,18,30,37,48,45,47,50,51,45];
            t.Check(Enumerable.Range(0,12).All(f=>real.Frame(f).ToArray().Count(v=>v!=0)==opaque[f]),"original crown.raw opaque pixel counts (SS§1.1)");
        }
    }

    // AN§3.4：(gfx, 姿勢) → (gfx, 0) → 整個 gfx 沒有圖時 1120。
    static void Appearances(WorldModuleTestContext t){
        t.Equal(1120,ObjectDrawFallback.MissingSpriteGfx,"missing sprite gfx is 1120");
        t.Equal(1120,PlayerProfiles.MissingSpriteGfx,"PlayerProfiles exposes the same constant");
        var table=new Dictionary<(int,int),string>{[(61,0)]="61/0",[(61,4)]="61/4",[(900,0)]="900/0",[(1120,0)]="1120/0"};
        string? Get(int g,int w)=>table.GetValueOrDefault((g,w));
        var exact=ObjectDrawFallback.Appearance(61,4,Get);
        t.Check(exact is {Appearance:"61/4",Gfx:61,Weapon:4,Substituted:false},"exact (gfx, stance) kept",exact);
        var stance0=ObjectDrawFallback.Appearance(61,20,Get);
        t.Check(stance0 is {Appearance:"61/0",Gfx:61,Weapon:0,Substituted:false},"missing stance falls back to stance 0",stance0);
        var monster=ObjectDrawFallback.Appearance(900,0,Get);
        t.Check(monster is {Appearance:"900/0",Substituted:false},"monster gfx kept",monster);
        var missing=ObjectDrawFallback.Appearance(4444,0,Get);
        t.Check(missing is {Appearance:"1120/0",Gfx:1120,Weapon:0,Substituted:true},"unknown gfx becomes 1120 action 0",missing);
        var missingArmed=ObjectDrawFallback.Appearance(4444,20,Get);
        t.Check(missingArmed is {Gfx:1120,Substituted:true},"unknown armed gfx also becomes 1120",missingArmed);
        table.Remove((1120,0));
        t.Check(ObjectDrawFallback.Appearance(4444,0,Get)==null,"no 1120 art either: object stays unavailable (no borrowed art)");
        t.Check(ObjectDrawFallback.Appearance(1120,0,Get)==null,"1120 itself missing does not loop");
    }

    // 逐動作退回：動作 → 同姿勢的姿勢動作（walk）→ 姿勢 0 的 walk；動作 0 本身缺圖不再退。
    static void Actions(WorldModuleTestContext t){
        var armed=new HashSet<string>{"walk","breath"};var bare=new HashSet<string>{"walk","breath","attack"};var empty=new HashSet<string>();
        bool Supports(HashSet<string> a,string action,int d)=>d is >=0 and <8&&a.Contains(action);
        var same=ObjectDrawFallback.Action(bare,()=>bare,"attack",3,Supports);
        t.Check(same is {} s&&ReferenceEquals(s.Appearance,bare)&&s.Action=="attack","supported action kept");
        var stance=ObjectDrawFallback.Action(armed,()=>bare,"attack",3,Supports);
        t.Check(stance is {} s2&&ReferenceEquals(s2.Appearance,armed)&&s2.Action=="walk","missing attack plays the stance action (walk) of the same stance");
        var spell=ObjectDrawFallback.Action(empty,()=>bare,"spellDir",1,Supports);
        t.Check(spell is {} s3&&ReferenceEquals(s3.Appearance,bare)&&s3.Action=="walk","stance action missing too: stance 0 walk");
        t.Check(ObjectDrawFallback.Action(empty,()=>null,"die",1,Supports)==null,"no stance 0 art: not drawn");
        t.Check(ObjectDrawFallback.Action(empty,()=>empty,"walk",1,Supports)==null,"stance-0 action 0 missing: no further fallback");
        var walkArmed=ObjectDrawFallback.Action(empty,()=>bare,"walk",1,Supports);
        t.Check(walkArmed is {} s4&&ReferenceEquals(s4.Appearance,bare)&&s4.Action=="walk","armed walk missing: stance 0 walk (+194 reset to 0)");
        var breath=ObjectDrawFallback.Action(new HashSet<string>{"walk"},()=>null,"breath",0,Supports);
        t.Check(breath is {Action:"walk"},"missing breath plays walk");
    }

    // AN§2.3／§2.4 純版面。
    static void Icons(WorldModuleTestContext t){
        var name=new PointF(110,197);var emblem=new PointF(90,196);
        var withClan=NameLayout.HoverIcons(7,name,emblem,"測試",true,false,1,false);
        t.Check(withClan.Length==1&&withClan[0] is {Kind:"mark181-1"}&&withClan[0].Position==new PointF(80,196),"+181 icon: emblem X −10 with clan, emblem Y",withClan);
        var noClan=NameLayout.HoverIcons(7,name,emblem,"測試",false,false,2,false);
        t.Check(noClan.Length==1&&noClan[0] is {Kind:"mark181-2"}&&noClan[0].Position==new PointF(100,196),"+181 icon: emblem X +10 without clan",noClan);
        t.Equal(0,NameLayout.HoverIcons(7,name,emblem,"測試",true,false,3,false).Length,"+181 values other than 1/2 draw nothing");
        // 「測試」＝4 Big5 bytes＝24 px。
        var both=NameLayout.HoverIcons(7,name,emblem,"測試",false,true,0,true);
        t.Check(both.Any(i=>i.Kind=="party"&&i.Position==new PointF(110+24+2,197)),"party icon at name X + width + 2, name Y",both);
        t.Check(both.Any(i=>i.Kind=="master"&&i.Position==new PointF(110+24+7,197)),"master icon at name X + width + 7, name Y",both);
        var r=new Rectangle(100,200,48,64);
        var line=NameLayout.AllNamesWithLine(r,"賣藥水","測試",false);
        t.Equal(new PointF(NameLayout.CenterX(r,"賣藥水"),200-16),line.Line,"+252 line at T−16, centred");
        t.Equal(new PointF(NameLayout.CenterX(r,"測試")-3,200-29),line.Name,"name moves to T−29, X −3 with a +252 line");
        t.Equal((ushort)0x4B7F,NameLayout.Line252Color,"+252 colour palette[54]");
        foreach(int k in new[]{2,3,9})t.Check(NameLayout.AllNamesSkips(k,false,false),$"all-names skips kind {k}");
        t.Check(NameLayout.AllNamesSkips(10,false,false)&&!NameLayout.AllNamesSkips(10,true,false),"kind 10 only when listed");
        t.Check(NameLayout.AllNamesSkips(null,false,true),"+196=1 skipped");
        t.Check(!NameLayout.AllNamesSkips(null,false,false)&&!NameLayout.AllNamesSkips(1,false,false),"unknown kind and other kinds drawn");
        t.Equal((ushort?)0x7C00,NameLayout.LevelDiffOutline(1,10,10),"level diff >=10 → palette[3]");
        t.Equal((ushort?)0x4200,NameLayout.LevelDiffOutline(5,10,3),"level diff 3..9 → palette[16]");
        t.Equal((ushort?)0x4200,NameLayout.LevelDiffOutline(5,10,9),"level diff 9 → palette[16]");
        t.Equal((ushort?)0x001F,NameLayout.LevelDiffOutline(1,10,2),"level diff 2 → palette[1]");
        t.Equal((ushort?)0x001F,NameLayout.LevelDiffOutline(1,10,-1),"level diff −1 → palette[1]");
        t.Equal((ushort?)0x02E0,NameLayout.LevelDiffOutline(1,10,-2),"level diff <=−2 → palette[22]");
        t.Equal((ushort?)null,NameLayout.LevelDiffOutline(0,10,20),"option off → black outline");
        t.Equal((ushort?)null,NameLayout.LevelDiffOutline(1,null,20),"kind unknown → black outline");
        t.Equal((ushort?)null,NameLayout.LevelDiffOutline(1,9,20),"non-kind-10 → black outline");
    }

    // 從合成封包走完 NameLayout.Plan：主人圖示、+252 行、外框預設。
    static void Plans(WorldModuleTestContext t){
        var w=Seed();
        w.Receive(Appear(Pet,32601,"夥伴",1110,title:"offline的寵物",owner:"offline",level:12),"offline");
        w.Receive(Appear(Shop,32602,"商人",1110,level:40),"offline");w.Receive(ShopAction(Shop,"賣藥水"),"offline");
        w.Receive(Appear(Other,32603,"路人",1110),"offline");
        t.Check(w.Objects.Single(o=>o.ObjectId==Shop) is {ShopActive:true,ShopText:"賣藥水"},"S158 70 stores shop title");
        var marks=w.NameMarksFor(Pet);
        t.Check(marks is {Master:true,Line252:"",Mark181:0,Kind:null,LevelDiff:-18},"pet marks from S87 (owner, level − own level)",marks);
        // R11-E（#961）：商店訊息不是 +252（#916），改畫頭上對話框；+252 是另一個欄位，3.81 沒有已知來源。
        t.Check(w.NameMarksFor(Shop) is {Line252:"",Master:false},"shop title is not the +252 line");
        w.SetField252(Shop,"賣藥水");
        t.Check(w.NameMarksFor(Shop) is {Line252:"賣藥水"},"+252 field feeds Line252");
        t.Check(w.NameMarksFor(Me)==new NameMarks(),"self has no marks");
        var targets=new[]{new NameTarget(Pet,"夥伴",new(100,200,48,64),false),new NameTarget(Shop,"商人",new(200,200,48,64),false),new NameTarget(Other,"路人",new(300,200,48,64),false)};
        var hover=NameLayout.Plan(w,targets,Pet,false);
        t.Check(hover.Icons.Length==1&&hover.Icons[0].Kind=="master","hovered pet with title and owner gets the master icon",hover.Icons);
        var petName=hover.Texts.Single(x=>!x.Title);
        t.Check(hover.Icons[0].Position==new PointF(petName.Position.X+6*NameLayout.ByteLength("夥伴")+7,petName.Position.Y),"master icon right of the name");
        t.Check(hover.Texts.All(x=>x.Outline==NameColors.Outline&&!x.Line252),"black outline by default");
        t.Equal(0,NameLayout.Plan(w,targets,Other,false).Icons.Length,"plain object: no icons");
        var all=NameLayout.Plan(w,targets,null,true);
        var shopTexts=all.Texts.Where(x=>x.ObjectId==Shop).ToArray();
        t.Check(shopTexts.Length==2&&shopTexts[0] is {Line252:true,Text:"賣藥水",Color:0x4B7F}&&shopTexts[0].Position.Y==200-16&&shopTexts[1].Position.Y==200-29,"all-names: +252 line at T−16, name at T−29",shopTexts);
        t.Check(all.Texts.Single(x=>x.ObjectId==Other).Position.Y==200-16,"all-names without +252 line unchanged");
        t.Check(all.Texts.All(x=>!x.Title),"all-names never draws titles");
        t.Equal(0,all.Icons.Length,"all-names draws no hover icons");
        NameLayout.MonsterLevelOption=1;
        try{t.Check(NameLayout.Plan(w,targets,Pet,false).Texts.All(x=>x.Outline==NameColors.Outline),"level outline needs kind 10, which 3.81 S87 does not carry");}
        finally{NameLayout.MonsterLevelOption=0;}
    }

    // WO §4：幽靈中自己的 S76 略過；他人的 S76 照常；非幽靈照常。
    static void Ghost(WorldModuleTestContext t){
        var g=Seed(0x80);
        t.Check(g.IsGhost,"self S87 flag 0x80 → ghost");
        t.Check(g.GhostSkipsSelfAppearance(Me),"GhostSkipsSelfAppearance true for self while ghost");
        int rev=g.AppearanceRevision;
        g.Receive(Poly(Me,6080,false),"offline");
        t.Check(g.Actor is {Gfx:61}&&g.AppearanceRevision==rev,"ghost: own S76 skipped (gfx and revision unchanged)",g.Actor);
        g.Receive(Appear(Other,32603,"路人",1110),"offline");g.Receive(Poly(Other,6080,true),"offline");
        t.Check(g.Objects.Single(o=>o.ObjectId==Other).Gfx==6080,"ghost: other object's S76 still applies");
        t.Throws<ProtocolException>(()=>g.Receive(P([76],Le32(0),Le16(61),Le16(29)),"offline"),"ghost: malformed S76 still rejected");
        var n=Seed();
        t.Check(!n.IsGhost&&!n.GhostSkipsSelfAppearance(Me),"not ghost");
        n.Receive(Poly(Me,6080,false),"offline");
        t.Check(n.Actor is {Gfx:6080},"not ghost: own S76 applies",n.Actor);
    }
}
