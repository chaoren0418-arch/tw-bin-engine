using System.Reflection;
using System.Text.Json;
namespace P06;

// T283／#676（R3-H）：盲目 2 讓 list.spr 欄位 102 為 {5,6,10,12} 的物件發光（k=5），以及職業條件下的 6/31。
// 只用合成封包、合成 LMP 與合成 list.spr 文字；不開遊戲、網路、視窗，也不讀素材。
// Q3-3（酒醉減半）、Q3-4（光效 +5 觸發）、Q3-5（隱身例外）等 T287 查證，本組不測。
static class AfflictionFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Monster=456,Item=789,Hidden=900;
    const ushort OwnX=32600,OwnY=32940;
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
    // S87（欄位同 AmbienceWiringTests.Appear），可指定 gfx。
    static byte[] Appear(uint id,ushort x,ushort gfx)=>P([87],Le16(x),Le16(OwnY),Le32(id),Le16(gfx),[0,0,0,0],Le32(0),Le16(0),
        Str("o"+id),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static WorldSession World(){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)4);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        w.Receive(Appear(Me,OwnX,0),"offline");
        return w;
    }
    // 合成 LMP：槽 4／5 共用 8×4（中央 2×2 為 255，其餘 64），其他槽 0×0（同 AmbienceWiringTests）。
    static AmbienceLightImages Lmp(){
        var data=new byte[AmbienceLightImages.HeaderBytes+32];
        BitConverter.GetBytes(192u).CopyTo(data,16);BitConverter.GetBytes(192u).CopyTo(data,20);
        BitConverter.GetBytes(8u).CopyTo(data,64+8*4);BitConverter.GetBytes(4u).CopyTo(data,68+8*4);
        BitConverter.GetBytes(8u).CopyTo(data,64+8*5);BitConverter.GetBytes(4u).CopyTo(data,68+8*5);
        for(int y=0;y<4;y++)for(int x=0;x<8;x++)data[192+y*8+x]=(byte)(x is 3 or 4&&y is 1 or 2?255:64);
        return AmbienceLightImages.Parse(data);
    }
    static Draw D(uint id)=>new(0,0,0,0,"actor",0,-1,-1,new Tile(0,[new Pixel(0,0,31)]),id);

    [WorldModuleTest("affliction-followup")]
    static void Run(WorldModuleTestContext t){Rules(t);Types(t);Centers(t);Lighting(t);}

    // 1. 純規則：發光種類與係數表。
    static void Rules(WorldModuleTestContext t){
        var glowing=Enumerable.Range(0,256).Where(v=>AfflictionColors.BlindTwoGlows((byte)v)).ToArray();
        t.Check(glowing.SequenceEqual([5,6,10,12]),"盲目 2 發光的 list.spr 種類＝{5,6,10,12}（AM§3(c)）",glowing);
        t.Check(Enumerable.Range(0,32).All(i=>AfflictionColors.BlindCoefficient(2,i,true)==AmbienceCurve.Coefficient(2,3,(byte)(i<<3))),"職業條件：盲目 2 用表 mode 2 第 3 級");
        t.Equal(6,AfflictionColors.BlindCoefficient(2,0,true),"職業條件：盲目 2 未照亮 6/31");
        t.Equal(31,AfflictionColors.BlindCoefficient(2,31,true),"職業條件：光圈中心 31");
        t.Check(AfflictionColors.BlindCoefficient(2,0)==4&&AfflictionColors.BlindCoefficient(1,0,true)==0&&AfflictionColors.BlindCoefficient(1,17,true)==17,"沒有職業條件仍是 4/31；盲目 1 不受職業影響");
        t.Check(!World().BlindClassBonus,"產品不設職業條件（byte_1542AA1 的來源封包未查到）");
    }

    // 2. list.spr 欄位 102：解析、預設 1、alias 不繼承、讀不到客戶端時丟錯。
    static void Types(WorldModuleTestContext t){
        const string text="#0 208 prince\n\t0.walk(1 4,0.0:4 0.1:4)\n\t101.shadow(1)\n\t102.type(5)\n\t104.attr(8)\n"
            +"#45 8 monster\r\n\t0.walk(1 4,0.0:4)\r\n\t102.type(10)\r\n"
            +"#46 8=45 alias\n"
            +"#66 1 arrow\n\t0.fly(1 3,0.2:1 0.0:1 0.1:1) 102.type(9) 109.effect(3 -1)\n"
            +"#70 1 nofield\n\t3.animation(0 12,0.0:5)\n"
            +"#71 1 big\n\t102.type(300)\n";
        var table=SpriteTypeTable.Parse(text);
        t.Check(table[0]==5&&table[45]==10&&table[66]==9,"欄位 102 依 gfx 取值（含 CRLF、同一行多欄位）",new{g0=table[0],g45=table[45],g66=table[66]});
        t.Check(table[46]==SpriteTypeTable.Default&&table[70]==1&&table[12345]==1,"沒有欄位 102 的 gfx（含 alias）是預設 1");
        t.Equal(1,(int)table[71],"超過 byte 的值不採用");
        t.Equal(3,table.Count,"只記有效的欄位 102");
        t.Throws<InvalidDataException>(()=>SpriteTypeTable.Load(Path.Combine(AppContext.BaseDirectory,"affliction-followup-no-client")),"缺客戶端時回報讀取錯誤");
    }

    // 3. 光源中心：自己先放；只有盲目 2 且有種類表時，畫面上有畫的發光種類物件才加。
    static void Centers(WorldModuleTestContext t){
        var w=World();
        w.Receive(Appear(Monster,OwnX+4,45),"offline");w.Receive(Appear(Item,OwnX+2,66),"offline");w.Receive(Appear(Hidden,OwnX+6,46),"offline");
        var types=SpriteTypeTable.FromTypes(new Dictionary<int,byte>{[0]=5,[45]=10,[66]=9,[46]=12});
        var own=Coordinates.Screen(OwnX,OwnY);
        List<Draw> commands=[D(0),D(Monster),D(Item)];
        var two=Renderer.BlindLightCenters(w,2,commands,own,types);
        t.Check(two.Count==2&&two[0]==own&&two[1]==Coordinates.Screen(OwnX+4,OwnY),"盲目 2：自己＋種類 10 的怪；種類 9 不發光；沒畫出來的物件不發光",two);
        t.Check(Renderer.BlindLightCenters(w,1,commands,own,types).Count==1,"盲目 1：只有自己");
        t.Check(Renderer.BlindLightCenters(w,2,commands,own,null).Count==1,"讀不到種類表：只有自己");
        // 預算：自己 5 + 每個物件 5，總和到 700 就停（139 個物件）。
        var crowd=World();var many=new List<Draw>{D(0)};
        for(uint i=0;i<200;i++){crowd.Receive(Appear(1000+i,(ushort)(OwnX+1+i%20),45),"offline");many.Add(D(1000+i));}
        t.Equal(140,Renderer.BlindLightCenters(crowd,2,many,own,types).Count,"光源預算：k 總和 700 為止");
    }

    // 4. 畫面：Source.lmp k=5 蓋在怪物腳下，遠處維持未照亮；職業條件把未照亮提到 6/31。
    static void Lighting(WorldModuleTestContext t){
        var w=World();w.Receive(Appear(Monster,OwnX+4,45),"offline");
        var types=SpriteTypeTable.FromTypes(new Dictionary<int,byte>{[45]=10});
        var own=Coordinates.Screen(OwnX,OwnY);var monster=Coordinates.Screen(OwnX+4,OwnY);
        var area=new Rectangle((int)MathF.Floor(own.X)-200,(int)MathF.Floor(own.Y)-100,400,200);
        int At(PointF p)=>((int)MathF.Floor(p.X)-area.X)+((int)MathF.Floor(p.Y)-area.Y)*area.Width;
        int far=At(new(own.X+150,own.Y+80));
        ushort[] Paint(ushort blind,SpriteTypeTable? table,AmbienceLightImages? images){
            var colors=Enumerable.Repeat((ushort)0x7FFF,area.Width*area.Height).ToArray();
            Renderer.ApplyBlindLight(w,blind,area,colors,[D(0),D(Monster)],images,own,table);
            return colors;
        }
        static int Red(ushort c)=>(c>>10)&31;
        var lit=Paint(2,types,Lmp());
        t.Check(Red(lit[At(own)])==31&&Red(lit[At(monster)])==31&&Red(lit[far])==4,"盲目 2：自己與怪物腳下全亮，遠處 4/31",new{own=Red(lit[At(own)]),monster=Red(lit[At(monster)]),far=Red(lit[far])});
        var plain=Paint(2,null,Lmp());
        t.Check(Red(plain[At(own)])==31&&Red(plain[At(monster)])==4,"沒有種類表：怪物腳下不亮",new{monster=Red(plain[At(monster)])});
        var one=Paint(1,types,Lmp());
        t.Check(Red(one[At(own)])==31&&Red(one[At(monster)])==0&&Red(one[far])==0,"盲目 1：怪物不發光、未照亮全黑");
        var disc=Paint(2,types,null);
        t.Check(Red(disc[At(monster)])>Red(disc[far])&&Red(disc[far])==4,"讀不到 Source.lmp：怪物也有替代圓形光",new{monster=Red(disc[At(monster)]),far=Red(disc[far])});
        w.BlindClassBonus=true;
        var bonus=Paint(2,types,Lmp());
        t.Check(Red(bonus[far])==6&&Red(bonus[At(monster)])==31,"職業條件：未照亮 6/31，光圈內仍到 31",new{far=Red(bonus[far])});
        // 與舊行為相同：只有自己時，蓋章結果等於 BlindLightStamp 逐點換算。
        w.BlindClassBonus=false;var images=Lmp();var self=Paint(2,null,images);
        bool same=true;
        for(int i=0;i<self.Length&&same;i++){
            int dx=area.X+i%area.Width-(int)MathF.Floor(own.X),dy=area.Y+i/area.Width-(int)MathF.Floor(own.Y);
            same=self[i]==AfflictionColors.Scale(0x7FFF,AfflictionColors.BlindCoefficient(2,AfflictionColors.BlindLightStamp(images,dx,dy)));
        }
        t.Check(same,"只有自己時與 #612 的 k=5 蓋章逐像素相同");
    }
}
