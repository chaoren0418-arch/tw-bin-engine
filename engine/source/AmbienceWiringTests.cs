using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
namespace P06;

// T251／#612（R2-A）：環境表現接上畫面的離線測試。只用合成封包、合成 LMP 與合成雨圖；不開遊戲、網路、視窗，也不讀素材。
// 設 TW_MODULE_CLIENT_CHECKS=1 時另外唯讀核對本機原客戶端的雨圖 924–927（CI 不設）。
static class AmbienceWiringTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Peer=456,Other=789;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){
        for(var t=o.GetType();t!=null;t=t.BaseType){
            if(t.GetProperty(name,F|BindingFlags.DeclaredOnly) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
            if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f){f.SetValue(o,value);return;}
            if(t.GetField($"<{name}>k__BackingField",F|BindingFlags.DeclaredOnly) is {} b){b.SetValue(o,value);return;}
        }
        throw new MissingMemberException(o.GetType().Name,name);
    }
    // S87（欄位同 AmountPromptTests.Appear），另帶 flags 與血盟 id。
    static byte[] Appear(uint id,ushort x,byte flags=0,uint clan=0)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(id==Me?"offline":"target"+id),Str(""),[flags],Le32(clan),Str(clan==0?"":"clan"),Str(""),[0,100,0,0,0,255,255]);
    static WorldSession World(uint ownClan=0){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)4);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        w.Receive(Appear(Me,32600,0,ownClan),"offline");
        return w;
    }
    static Bitmap Solid(int w,int h,int gray=248){
        var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b))g.Clear(Color.FromArgb(255,gray,gray,gray));
        return b;
    }
    // 合成 LMP：槽 4／5 共用 8×4（中央 2×2 為 255，其餘 64），其他槽 0×0。
    static AmbienceLightImages Lmp(){
        var data=new byte[AmbienceLightImages.HeaderBytes+32];
        BitConverter.GetBytes(192u).CopyTo(data,16);BitConverter.GetBytes(192u).CopyTo(data,20);
        BitConverter.GetBytes(8u).CopyTo(data,64+8*4);BitConverter.GetBytes(4u).CopyTo(data,68+8*4);
        BitConverter.GetBytes(8u).CopyTo(data,64+8*5);BitConverter.GetBytes(4u).CopyTo(data,68+8*5);
        for(int y=0;y<4;y++)for(int x=0;x<8;x++)data[192+y*8+x]=(byte)(x is 3 or 4&&y is 1 or 2?255:64);
        return AmbienceLightImages.Parse(data);
    }
    static Renderer NewRenderer(bool syntheticLmp=true){
        // Renderer 的建構子需要 Assets；本測試只用到明暗與天氣，因此以未初始化物件建立，只補 Bounds 相關欄位。
        var assets=(Assets)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Assets));
        Set(assets,"CanvasWidth",8);Set(assets,"CanvasHeight",8);Set(assets,"Scene",new List<Draw>());
        // 用合成 LMP，避免測試去讀本機原客戶端的 Source.lmp。
        var r=new Renderer(assets);if(syntheticLmp)r.AmbienceImagesOverride=Lmp();return r;
    }
    static ObjectVisual Own(PointF foot)=>new(Me,0,0,"offline",32600,32940,"breath","0",foot,Rectangle.Empty,false,0);

    [WorldModuleTest("ambience-wiring")]
    static void Run(WorldModuleTestContext t){
        Shading(t);Weather(t);Sprites(t);Blind(t);Liquor(t);Invisible(t);
        if(Environment.GetEnvironmentVariable("TW_MODULE_CLIENT_CHECKS")=="1"){
            var real=WeatherSpriteSet.Load();
            t.Check(real.HasRainImages&&real.RainImage(924)!.Width==10&&real.RainImage(925)!.Width==9&&real.RainImage(926)!.Width==9&&real.RainImage(927)!.Width==7,"原客戶端雨圖 924–927 尺寸 10/9/9/7");
            t.Check(new[]{924,925,926,927}.All(s=>real.RainImage(s)!.Flags==0&&real.RainImage(s)!.ColorKey==0),"原客戶端雨圖 flags／colorKey 為 0");
        }
    }

    // 1. PaintScene 的掛接：白天晴天不走離屏；夜晚整個畫面依係數表變暗；有光處較亮；盲目時不再重複變暗。
    static void Shading(WorldModuleTestContext t){
        var w=World();using var r=NewRenderer();
        w.Ambience.TimeReceived(12*3600,0);
        var day=r.PrepareAmbience(w,[],0);
        t.Check(!Renderer.AmbienceVisible(day)&&day.Level==15,"白天晴天：不需要離屏明暗（保持舊畫法）",day);
        t.Check(r.AmbienceLightSources.Length==0,"白天不蓋光照圖");
        w.Ambience.TimeReceived(0,0);
        var night=r.PrepareAmbience(w,[],0,Lmp());
        t.Check(Renderer.AmbienceVisible(night)&&night.Shaded&&night.Level==1,"午夜 L=1：要走明暗",night);
        using(var scene=Solid(16,8)){
            r.PaintAmbienceScene(scene,new(0,0,16,8),night,w.Ambience.Particles);
            int expected=AmbienceCurve.Channel8(248,AmbienceCurve.Coefficient(0,night.FrameLevel,0));
            t.Equal(expected,(int)scene.GetPixel(5,5).R,"沒有自己時整個畫面都是未照亮（光照 0）的係數");
        }
        using(var scene=Solid(16,8)){
            r.PaintAmbienceScene(scene,new(0,0,16,8),night,w.Ambience.Particles,blindMode:1);
            t.Equal(248,(int)scene.GetPixel(5,5).R,"盲目時明暗已在合成時做過，不重複變暗");
        }
        // 自己的光源：S40 值 5 → k=5，照到自己腳下。
        Set(r,"OwnCombatVisual",Own(new(400,300)));
        w.Receive(P([40],Le32(Me),[5]),"offline");
        var lit=r.PrepareAmbience(w,[Own(new(400,300))],0,Lmp());
        t.Check(r.AmbienceLightSources.Length==1&&r.AmbienceLightSources[0].Source==5&&r.AmbienceWindow==new Rectangle(0,60,800,480),"自己 k=5 光源、800×480 視窗以腳為中心",new{r.AmbienceLightSources,r.AmbienceWindow});
        using(var scene=Solid(800,600)){
            r.PaintAmbienceScene(scene,new(0,0,800,600),lit,w.Ambience.Particles);
            t.Check(scene.GetPixel(400,300).R>scene.GetPixel(100,100).R&&scene.GetPixel(400,10).R==scene.GetPixel(100,100).R,"腳下比遠處亮；視窗外是未照亮",new{centre=scene.GetPixel(400,300).R,far=scene.GetPixel(100,100).R,outside=scene.GetPixel(400,10).R});
        }
        // 畫面震動掛點（給 R2-O）：預設沒有。
        t.Check(r.ScreenShake==null,"畫面震動掛點預設為空");
    }

    // 2. 天氣：每格推進粒子（24 ms tick、500 ms 上限）、鏡頭補償、雨雪畫在 800×480 視窗內、先畫天氣再變暗。
    static void Weather(WorldModuleTestContext t){
        var w=World();using var r=NewRenderer();
        w.Ambience.TimeReceived(12*3600,0);
        w.Receive(P([115],[0x03]),"offline");
        var rain=r.PrepareAmbience(w,[],1000);
        t.Check(rain.Rain==3&&Renderer.AmbienceVisible(rain),"S115 0x03：雨 3，要走離屏",rain);
        var particles=w.Ambience.Particles;long start=particles.Ticks;
        r.PrepareAmbience(w,[],1240);
        t.Equal(start+10,particles.Ticks,"240 ms → 10 個 24 ms tick");
        r.PrepareAmbience(w,[],9240);
        t.Equal(start+10+20,particles.Ticks,"一格最多 500 ms（20 tick）");
        // 鏡頭補償：自己往右走 3 px，雨的 x 往左 3 px（推測的符號）。
        var probe=new WeatherParticles();var frame=new AmbienceFrame(null,3,0){Rain=3};
        Set(r,"OwnCombatVisual",Own(new(400,300)));r.StepWeather(probe,frame,0);
        float before=probe.Active(frame).First().X;
        Set(r,"OwnCombatVisual",Own(new(403,300)));r.StepWeather(probe,frame,0);
        float after=probe.Active(frame).First().X;
        t.Check(Math.Abs(((before-after)%800+800)%800-3)<.01,"自己右移 3 px → 雨往左 3 px",new{before,after});
        Set(r,"OwnCombatVisual",Own(new(5000,300)));r.StepWeather(probe,frame,0);
        t.Check(Math.Abs(probe.Active(frame).First().X-after)<.01,"一次超過一個畫面寬（傳送）不補償");
        // 畫在視窗內：用合成 sprite 記錄每顆粒子的位置。
        Set(r,"OwnCombatVisual",Own(new(400,300)));
        var shown=r.PrepareAmbience(w,[Own(new(400,300))],9264);
        var points=new List<PointF>();WeatherSprite record=(g,p,at)=>points.Add(at);
        using(var scene=Solid(800,600)){
            r.PaintAmbienceScene(scene,new(0,0,800,600),shown,particles,sprite:record);
            t.Equal(512,points.Count,"雨 3 畫 512 顆");
            t.Check(points.All(p=>p.X>=0&&p.X<800&&p.Y>=60&&p.Y<=60+485),"粒子位置換算到 800×480 視窗（以腳為中心）",points.Take(4));
        }
        // 先畫天氣再一起變暗（AM§2(b)）：夜晚的白點也會被變暗。
        w.Ambience.TimeReceived(0,0);
        var dark=r.PrepareAmbience(w,[Own(new(400,300))],9288,Lmp());
        WeatherSprite white=(g,p,at)=>{if(p.Index==0)g.FillRectangle(Brushes.White,10,70,1,1);};
        using(var scene=Solid(800,600,0)){
            r.PaintAmbienceScene(scene,new(0,0,800,600),dark,particles,sprite:white);
            int f=AmbienceCurve.Coefficient(0,dark.FrameLevel,0);
            t.Equal((int)AmbienceCurve.Channel8(255,f),(int)scene.GetPixel(10,70).R,"雨點在明暗之前畫，跟著場景變暗");
        }
        // 雪：S115 0x12 → 雪 2，96 顆，每格 y+10。
        var snowWorld=World();snowWorld.Ambience.TimeReceived(12*3600,0);
        snowWorld.Receive(P([115],[0x12]),"offline");
        var snow=r.PrepareAmbience(snowWorld,[],0);
        float y0=snowWorld.Ambience.Particles.Active(snow).First().Y;
        r.PrepareAmbience(snowWorld,[],16);
        float y1=snowWorld.Ambience.Particles.Active(snow).First().Y;
        t.Check(snow.Snow==2&&snowWorld.Ambience.Particles.Active(snow).Count()==96&&(y1-y0==10||y1==0),"雪 2：96 顆、每格 y+5n",new{y0,y1});
    }

    // 3. WeatherSpriteSet：雨圖 surf 對照、程序圖樣點數、透明色、讀不到客戶端時丟錯。
    static void Sprites(WorldModuleTestContext t){
        WeatherParticle Rain(int index,int stage=0)=>new(WeatherKind.Rain,index,WeatherParticles.RainLayer(index),0,0,stage,485);
        t.Check(WeatherSpriteSet.RainSurf(Rain(246))==924&&WeatherSpriteSet.RainSurf(Rain(247))==925&&WeatherSpriteSet.RainSurf(Rain(255))==925,"層 0：924／925 依序號奇偶");
        t.Check(WeatherSpriteSet.RainSurf(Rain(236))==926&&WeatherSpriteSet.RainSurf(Rain(237))==927&&WeatherSpriteSet.RainSurf(Rain(492))==926,"層 1：926／927 依序號奇偶（第二組 256 顆同樣）");
        t.Check(WeatherSpriteSet.RainSurf(Rain(0))==0&&WeatherSpriteSet.RainSurf(Rain(200))==0,"層 2／3 是程序圖樣，不用 surf");
        t.Check(WeatherSpriteSet.Pattern(103).Length==5&&WeatherSpriteSet.Pattern(102).Length==12&&WeatherSpriteSet.Pattern(203).Length==11&&WeatherSpriteSet.Pattern(202).Length==14,"程序圖樣點數：十字 5、4×4 共 12、雪 11／14");
        t.Check(WeatherSpriteSet.Pattern(102).Distinct().Count()==12&&WeatherSpriteSet.Pattern(202).Distinct().Count()==14,"圖樣沒有重複點");
        t.Check(WeatherSpriteSet.SplashWeightAt(0)==0&&WeatherSpriteSet.SplashWeightAt(1)==16&&WeatherSpriteSet.SplashWeightAt(2)==8&&WeatherSpriteSet.SplashWeightAt(5)==3,"水花權重 16/階段；階段 0 不畫");
        // 合成雨圖：2×1，左黑（透明色 0）右紅。
        byte[] img=[2,0,1,0,0,0,0,0,0,0,0x00,0x7C];
        var image=WeatherImage.Decode(img);
        t.Check(image.Width==2&&image.Height==1&&image.Pixels[1]==0x7C00&&image.Transparent(0),"合成 .img 解碼");
        t.Throws<InvalidDataException>(()=>WeatherImage.Decode(img.AsSpan(0,10)),"長度不符的 .img 被拒絕");
        var set=new WeatherSpriteSet([image,image,image,image]);
        using(var b=Solid(20,20,100)){
            using(var g=Graphics.FromImage(b)){set.Sprite(g,Rain(246),new(5,5));set.Sprite(g,Rain(0,1),new(15,15));}
            t.Check(b.GetPixel(6,5).R==255&&b.GetPixel(6,5).G==0&&b.GetPixel(5,5).R==100,"雨圖畫在粒子位置；顏色 0 透明",new{r=b.GetPixel(6,5),key=b.GetPixel(5,5)});
            // 層 3 水花，階段 1：權重 16、中心 +12 → 28/31。
            int centre=b.GetPixel(15,15).R,arm=b.GetPixel(16,15).R,beside=b.GetPixel(16,16).R;
            t.Check(centre>arm&&arm>100&&beside==100,"層 3 十字水花：中心較亮、對角不畫",new{centre,arm,beside});
        }
        var proceduralOnly=WeatherSpriteSet.ProceduralOnly;
        using(var b=Solid(20,20,100)){
            using(var g=Graphics.FromImage(b)){proceduralOnly.Sprite(g,Rain(246),new(5,5));proceduralOnly.Sprite(g,new(WeatherKind.Snow,0,3,10,10,0,400),new(10,10));}
            t.Check(Enumerable.Range(0,20).All(x=>b.GetPixel(x,5).R==100),"沒有雨圖時層 0 不畫替代圖");
            t.Check(b.GetPixel(11,11).R>100,"沒有雨圖時雪仍畫程序圖樣");
        }
        t.Throws<IOException>(()=>WeatherSpriteSet.Load(Path.Combine(AppContext.BaseDirectory,"no-such-client")),"缺客戶端時回報讀取錯誤");
    }

    // 4. 盲目光圈：Source.lmp k=5 蓋章取代圓形；盲目 2 用係數表第 1 級。
    static void Blind(WorldModuleTestContext t){
        var images=Lmp();
        // 8×4 格置於 (400,240)：左 ((400−8)>>1)&~3 = 196，上 (240−4)>>1 = 118；中心格 (200,120) 對到影像 (4,2)。
        t.Equal(255>>3,AfflictionColors.BlindLightStamp(images,0,0),"中心是 k=5 影像的亮處");
        t.Equal(64>>3,AfflictionColors.BlindLightStamp(images,-6,-3),"邊緣是影像的暗處");
        t.Equal(0,AfflictionColors.BlindLightStamp(images,40,0),"影像範圍外沒有光");
        t.Equal(0,AfflictionColors.BlindLightStamp(AmbienceLightImages.Parse(new byte[AmbienceLightImages.HeaderBytes]),0,0),"k=5 空槽不補光圈");
        t.Check(Enumerable.Range(0,32).All(i=>AfflictionColors.BlindCoefficient(2,i)==AmbienceCurve.Coefficient(0,1,(byte)(i<<3))),"盲目 2 的係數＝表 mode 0 第 1 級");
        t.Check(AfflictionColors.BlindCoefficient(1,17)==17&&AfflictionColors.BlindCoefficient(2,0)==4,"盲目 1＝光照值；盲目 2 未照亮 4/31");
        using var r=NewRenderer(syntheticLmp:false);
        t.Check(r.LoadedAmbienceImages==null,"沒畫過的 renderer 不會自己讀 Source.lmp（盲目退回舊圓形）");
        r.AmbienceImagesOverride=images;
        t.Check(r.LoadedAmbienceImages==images,"畫面讀到 Source.lmp 後，盲目改用 k=5 影像");
    }

    // 5. 酒醉：sub_81B0E0 為假時位移減半；地圖 67／420 型 0 留醉度 1 並凍結相位。
    static void Liquor(WorldModuleTestContext t){
        for(int phase=0;phase<200;phase+=7)for(int row=0;row<100;row+=3){
            int full=TimedBuffRules.ScanlineOffset(3,phase,row);
            if(TimedBuffRules.LiquorOffset(3,phase,row,true)!=full||TimedBuffRules.LiquorOffset(3,phase,row,false)!=full/2){t.Check(false,"位移減半",new{phase,row});return;}
        }
        t.Check(true,"sub_81B0E0 假：位移 /2（C 整數除法）；真：不變");
        t.Check(TimedBuffRules.LiquorFullWave()&&TimedBuffRules.LiquorOffset(2,10,20)==TimedBuffRules.ScanlineOffset(2,10,20),"產品預設全振幅（sub_81B0E0 意義未查）");
        t.Check(TimedBuffRules.MinimumLiquorMap(67)&&TimedBuffRules.MinimumLiquorMap(420)&&!TimedBuffRules.MinimumLiquorMap(4)&&!TimedBuffRules.MinimumLiquorMap(null),"最低醉度地圖 67／420");
        long now=0;var w=World();Set(w,"MagicStatusClock",(Func<long>)(()=>now));
        byte[] Drunk(byte type)=>P([103],Le32(Me),[type]);
        w.Receive(Drunk(2),"offline");now=1000;
        t.Check(w.LiquorLevel==2&&w.LiquorPhase==20,"一般地圖 S103 型 2",new{w.LiquorLevel,w.LiquorPhase});
        w.Receive(Drunk(0),"offline");
        t.Equal(0,w.LiquorLevel,"一般地圖型 0 清除");
        w.Receive(Drunk(2),"offline");now=1500;
        Set(w,"MapId",(ushort)67);
        w.Receive(Drunk(0),"offline");now=4000;
        t.Check(w.LiquorLevel==1&&w.LiquorPhase==10,"地圖 67 型 0：醉度 1，相位停在收到時",new{w.LiquorLevel,w.LiquorPhase});
        w.Receive(Drunk(3),"offline");now=4100;
        t.Check(w.LiquorLevel==3&&w.LiquorPhase==2,"再喝：計時器重新開始",new{w.LiquorLevel,w.LiquorPhase});
        var fresh=World();Set(fresh,"MagicStatusClock",(Func<long>)(()=>now));Set(fresh,"MapId",(ushort)420);
        fresh.Receive(Drunk(0),"offline");
        t.Check(fresh.LiquorLevel==1&&fresh.LiquorPhase==0,"地圖 420 沒醉過也是醉度 1、相位 0",new{fresh.LiquorLevel,fresh.LiquorPhase});
    }

    // 6. 隱身：只有隊伍成員可見（50% 混合），同血盟不是例外（FU4§4，T288／#689）；其他人仍不畫。
    static void Invisible(WorldModuleTestContext t){
        var w=World(ownClan:77);
        w.Receive(Appear(Peer,32601,2,0),"offline");w.Receive(Appear(Other,32602,2,0),"offline");w.Receive(Appear(900,32603,2,77),"offline");
        Draw D(uint id,string layer="actor")=>new(0,0,0,0,layer,0,-1,-1,new Tile(0,[new Pixel(0,0,31)]),id);
        t.Check(w.InvisibilityOpacity(D(Peer),null) is {Hidden:true}&&w.InvisibilityOpacity(D(Other),null) is {Hidden:true},"不同隊伍、不同血盟：不畫");
        t.Check(!w.InvisiblePeerVisible(900)&&w.InvisibilityOpacity(D(900),null) is {Hidden:true}&&w.InvisibilityOpacity(D(900,"shadow"),null) is {Hidden:true},"同血盟（id 77）但不同隊：不畫，影子也不畫");
        var members=(System.Collections.IDictionary)typeof(WorldSession).GetField("partyMembers",F)!.GetValue(w)!;
        members["target456"]=new PartyMember(Peer,"target456",100);
        t.Check(w.InvisiblePeerVisible(Peer)&&w.InvisibilityOpacity(D(Peer),null) is {Half:true,Hidden:false},"隊伍成員：50% 混合");
        t.Check(w.InvisibilityOpacity(D(Other),null) is {Hidden:true},"隊伍外的人仍不畫");
        t.Check(!w.InvisiblePeerVisible(0)&&!w.InvisiblePeerVisible(12345),"id 0 與不存在的物件不算");
        var noClan=World();noClan.Receive(Appear(Peer,32601,2,0),"offline");
        t.Check(noClan.InvisibilityOpacity(D(Peer),null) is {Hidden:true},"自己沒有血盟（id 0）時，血盟 id 0 的人不算同盟");
    }
}
