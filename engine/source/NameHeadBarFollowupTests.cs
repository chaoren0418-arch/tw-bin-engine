using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R10-H（T392／#907）離線測試：
//  1. 自己的名稱 P.x 改用顯示中的腳下點（World.DisplayPosition＝OwnCombatVisual.Foot，走路內插；FU8§1.1）。
//  2. HP 浮動窗低血門檻依 OwnClass：職業 7（戰士）0.40，其餘／未知 0.35（sub_EDB8E0、dword_138B62C）；
//     頭上條的戰士檢查器（40, 無限）也由同一個 OwnClass 選（FU8§1.2）。
//  3. +86／+87 灰條、綠條（推測，FU8§1.3）：HP 條 surf＝基準＋1／＋2。
// 只用合成 S87 封包與合成狀態，不開遊戲、網路、視窗、伺服器、DB、素材，不寫檔。由 --module-tests 自動發現執行。
static class NameHeadBarFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Other=790;
    const ushort SelfX=32600,SelfY=32940;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87：x y id gfx 姿勢 方向 光 速度 exp lawful 名稱 稱號 旗標 盟id 盟名 主人 rank hp brave 等級 unknown tail1 tail2。
    static byte[] Appear(uint id,ushort x,ushort y,string name)=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(61),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,30,0,255,255]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static WorldSession Seed(){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",1);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":30,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        w.Receive(Appear(Me,SelfX,SelfY,"offline"),"offline");
        w.Receive(Appear(Other,32603,SelfY,"路人"),"offline");
        return w;
    }

    sealed class VariantState(IUiGameState inner,HpBarVariant variant,byte? cls):IUiGameState,IUiHpMpState,IUiHpMpVariantState {
        public IReadOnlyList<HpMpHeadTarget> HeadTargets=>[];
        public byte? OwnClass=>cls;
        public HpBarVariant OwnHpVariant=>variant;
        public bool InWorld=>inner.InWorld;
        public string? CharacterName=>inner.CharacterName;
        public int? Level=>inner.Level;
        public long? Hp=>inner.Hp;
        public long? MaxHp=>inner.MaxHp;
        public long? Mp=>inner.Mp;
        public long? MaxMp=>inner.MaxMp;
        public long Now=>inner.Now;
    }
    static UiStaticGameState G(long hp,long now=0)=>new(true,"me",50,hp,100,50,100,now);
    static UiHpMpGameState S(long hp,byte? cls,long now=0)=>new(G(hp,now),[],cls);

    [WorldModuleTest("name-headbar-followup")]
    static void Run(WorldModuleTestContext t){NameFoot(t);WarnThreshold(t);HeadBarClass(t);Variants(t);}

    // 1. 自己的名稱 X 跟著顯示中的腳下點。
    static void NameFoot(WorldModuleTestContext t){
        var standing=Coordinates.Screen(SelfX,SelfY);
        t.Equal(NameLayout.SelfOriginX(SelfX,SelfY),NameLayout.SelfOriginX(standing),"站著：腳下點版與格座標版相同");
        t.Equal(1632,NameLayout.SelfOriginX(new PointF(1656.9f,700)),"P.x＝floor(foot.X) − 24（取整同頭上條 SelfHeadOrigin）");
        var w=Seed();var bounds=new Rectangle(1610,690,48,64);
        var targets=new[]{new NameTarget(Me,"offline",bounds,true),new NameTarget(Other,"路人",new(1700,690,48,64),false)};
        float Name(PointF? foot,uint hovered)=>NameLayout.Plan(w,targets,hovered,false,selfFoot:foot).Texts.Single().Position.X;
        float old=Name(null,Me);
        t.Equal((float)NameLayout.SelfX(NameLayout.SelfOriginX(SelfX,SelfY),"offline"),old,"沒給腳下點：退回 Actor 格座標（#784 行為）");
        t.Equal(old,Name(standing,Me),"站著：結果不變");
        // 走一步的一半（FrameIndex 2/4）：往 +X 一格的腳下點在兩格中間。
        var next=Coordinates.Screen(SelfX+1,SelfY);var mid=new PointF((standing.X+next.X)/2,(standing.Y+next.Y)/2);
        float walking=Name(mid,Me);
        t.Equal((float)NameLayout.SelfX(NameLayout.SelfOriginX(mid),"offline"),walking,"走路中：名稱用內插的腳下點");
        t.Check(walking!=old&&Math.Abs(walking-old-(mid.X-standing.X))<1,"名稱跟著身體移動（差＝腳下點的位移）",new{old,walking,mid,standing});
        t.Equal(Name(null,Other),Name(mid,Other),"他人的名稱不受 selfFoot 影響");
        var all=NameLayout.Plan(w,targets,null,true,selfFoot:mid).Texts.Single(x=>x.ObjectId==Me);
        t.Equal(new PointF(NameLayout.CenterX(bounds,"offline"),690-16),all.Position,"全名模式：自己仍用外框置中");
    }

    // 2. HP 浮動窗：戰士 40%。
    static void WarnThreshold(WorldModuleTestContext t){
        t.Equal(0.40,HpMpPanel.WarnRatioFor(7),"職業 7：0.40（dword_138B62C）");
        foreach(byte? c in new byte?[]{null,0,1,2,3,4,5,6})t.Equal(0.35,HpMpPanel.WarnRatioFor(c),$"職業 {c?.ToString()??"未知"}：0.35");
        HpMpPanel Fresh(IUiGameState s){var p=new HpMpPanel();p.Update(s);return p;}
        t.Check(Fresh(S(40,7)) is {WarnCounter:6,CurrentWarnRatio:0.40},"戰士 HP 40%（＝門檻）：計數器設 6");
        t.Check(Fresh(S(38,7)).WarnCounter==6,"戰士 HP 38%：觸發");
        t.Check(Fresh(S(41,7)).WarnCounter==0,"戰士 HP 41%：不觸發");
        t.Check(Fresh(S(38,3)) is {WarnCounter:0,CurrentWarnRatio:0.35},"一般職業 HP 38%：不觸發（門檻 35）");
        t.Check(Fresh(S(35,3)).WarnCounter==6,"一般職業 HP 35%：觸發");
        t.Check(Fresh(G(38)) is {WarnCounter:0,CurrentWarnRatio:0.35},"狀態沒有 IUiHpMpState：當未知職業 0.35");
        var p=Fresh(S(38,7));p.Update(S(38,7,300));
        t.Check(p.WarnCounter==5&&p.WarnVisible,"300 ms 後遞減到 5（奇數畫紅框），計數規則不變");
    }

    // 頭上條：同一個 OwnClass 選檢查器（戰士 40／無限）。
    static void HeadBarClass(WorldModuleTestContext t){
        var self=new HpMpHeadTarget(Me,true,new(380,220,48,64),Origin:new Point(456,288));
        var bar=new HpMpHeadBarPanel();
        bar.Update(new UiHpMpGameState(G(38),[self],7));
        t.Check(bar.Blink is {Threshold:40,Times:-1},"OwnClass 7：頭上條檢查器 (40, −1)",bar.Blink);
        bar.Update(new UiHpMpGameState(G(38,10),[self],2));
        t.Check(bar.Blink is {Threshold:35,Times:5},"OwnClass 改成 2：換回 (35, 5)",bar.Blink);
    }

    // 3. +86／+87 變體（推測）。
    static void Variants(WorldModuleTestContext t){
        t.Equal(HpBarVariant.Normal,IUiHpMpVariantState.FromFlags(false,false),"沒有旗標：基準");
        t.Equal(HpBarVariant.Gray,IUiHpMpVariantState.FromFlags(true,false),"+86：灰");
        t.Equal(HpBarVariant.Green,IUiHpMpVariantState.FromFlags(false,true),"+87：綠");
        t.Equal(HpBarVariant.Gray,IUiHpMpVariantState.FromFlags(true,true),"兩者都有：灰優先（推測）");
        // 12 組 (shape, status, thick) 的基準與變體：本機客戶端都有（issue-907 報告的 pak 清單）。
        var expected=new Dictionary<(int,int,bool),int>{
            [(1,0,false)]=2359,[(1,1,false)]=2362,[(1,2,false)]=2365,[(1,0,true)]=2914,[(1,1,true)]=2917,[(1,2,true)]=2920,
            [(0,0,false)]=2338,[(0,1,false)]=2341,[(0,2,false)]=2344,[(0,0,true)]=2896,[(0,1,true)]=2899,[(0,2,true)]=2902};
        foreach(var ((shape,status,thick),hp) in expected){
            var L=HpMpLayout.For(shape,status,thick);
            t.Check(L.HpSurf(HpBarVariant.Normal)==hp&&L.HpSurf(HpBarVariant.Gray)==hp+1&&L.HpSurf(HpBarVariant.Green)==hp+2,
                $"shape {shape} status {status} thick {thick}：HP {hp}／灰 {hp+1}／綠 {hp+2}",new{L.Hp});
            t.Check(L.HpSurf(HpBarVariant.Green)!=L.Mp,"綠條不與 MP 條撞號");
        }
        var p=new HpMpPanel();
        p.Update(G(80));t.Equal(HpBarVariant.Normal,p.Variant,"狀態不帶變體：基準");
        p.Update(new VariantState(G(80),HpBarVariant.Green,null));t.Equal(HpBarVariant.Green,p.Variant,"狀態帶 +87：綠");
        p.Update(new VariantState(G(80),HpBarVariant.Gray,7));
        t.Check(p.Variant==HpBarVariant.Gray&&p.CurrentWarnRatio==0.40,"同一個狀態同時帶變體與職業",new{p.Variant,p.CurrentWarnRatio});
        p.Update(S(80,null));t.Equal(HpBarVariant.Normal,p.Variant,"變體解除：回基準");
    }
}
