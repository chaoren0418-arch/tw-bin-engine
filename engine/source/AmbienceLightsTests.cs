namespace P06;

// T241／#566：光照圖與地圖特效光源表（FX1）的離線測試。只用合成資料；不開遊戲、網路，也不讀素材。
// 設 TW_MODULE_CLIENT_CHECKS=1 時另外唯讀核對本機原客戶端的 Source.lmp（CI 不設）。
static class AmbienceLightsTests {
    static MapEffectAppearance Dot(int id)=>new(System.Text.Encoding.UTF8.GetBytes(
        $$"""{"id":{{id}},"flags":0,"tickRate":24,"frames":[{"index":0,"bounds":[0,0,0,0],"blocks":[{"a":0,"b":0,"z":0,"order":0,"pixels":[[0,0,13707]]}]}],"sequence":[{"frame":0,"ticks":1,"events":""}]}"""));

    // 合成 LMP：槽 0／1 共用 2×1、槽 2／3 共用 4×2，其餘槽 0×0。
    static byte[] SyntheticLmp(){
        var data=new byte[AmbienceLightImages.HeaderBytes+2+8];
        void U32(int at,uint v)=>BitConverter.GetBytes(v).CopyTo(data,at);
        U32(0,192);U32(4,192);U32(8,194);U32(12,194);
        U32(64,2);U32(68,1);U32(72,2);U32(76,1);U32(80,4);U32(84,2);U32(88,4);U32(92,2);
        data[192]=100;data[193]=200;for(int i=0;i<8;i++)data[194+i]=(byte)(10*(i+1));
        return data;
    }

    [WorldModuleTest("ambience-lights")]
    static void Run(WorldModuleTestContext t){
        // 1. .s32 地圖特效 id → k（EF2 §1.1(b)）。
        t.Equal((byte)5,MapEffectLightTable.Radius(3442),"3442 dot range five → 5");
        t.Equal((byte)7,MapEffectLightTable.Radius(3443),"3443 → 7");
        t.Equal((byte)9,MapEffectLightTable.Radius(3444),"3444 → 9");
        t.Equal((byte)11,MapEffectLightTable.Radius(3445),"3445 → 11");
        t.Equal((byte)13,MapEffectLightTable.Radius(3446),"3446 dot range thirteen → 13");
        t.Equal((byte)8,MapEffectLightTable.Radius(3737),"3737 → 8 (word_138BE2C)");
        t.Equal((byte)11,MapEffectLightTable.Radius(2088),"2088 → 11 (word_138BE44)");
        t.Equal((byte)5,MapEffectLightTable.Radius(1901),"1901 → 5");
        t.Equal((byte)5,MapEffectLightTable.Radius(1987),"1987 → 5");
        t.Equal((byte)5,MapEffectLightTable.Radius(2209),"2209 → 5");
        t.Equal((byte)7,MapEffectLightTable.Radius(1895),"1895 → 7");
        t.Equal((byte)9,MapEffectLightTable.Radius(2195),"2195 → 9");
        t.Equal((byte)9,MapEffectLightTable.Radius(2202),"2202 → 9");
        t.Equal((byte)13,MapEffectLightTable.Radius(149),"149 → 13");
        t.Equal((byte)14,MapEffectLightTable.Radius(1886),"1886 brazier → 14");
        t.Equal((byte)14,MapEffectLightTable.Radius(2196),"2196 → 14");
        foreach(int id in new[]{0,1,1883,1888,1900,1988,2203,3441,3447,6016,65535})t.Equal((byte)0,MapEffectLightTable.Radius(id),$"未列出的 id {id} → 0");

        // 2. 地圖特效載入時記下 k；本身仍是 1 像素特效，不另加光圈圖形。
        var dot=new MapLocalEffect(3446,32600,32800,0,Dot(3446));
        t.Equal((byte)13,dot.Light,"MapLocalEffect 記下 k");
        t.Equal(1,dot.Appearance.Frames.Single().Blocks.Single().Tile.Pixels.Length,"3446 外觀仍只有 1 像素");
        t.Equal((byte)0,new MapLocalEffect(6016,32600,32800,0,Dot(6016)).Light,"不發光特效 k=0");

        // 3. AmbienceLights 加入地圖特效：k=0 不加入、700 預算、距離濾除、近者優先。
        var s=new WorldSession();
        var at=Coordinates.Screen(32600,32800);
        Func<PointF,PointF> view=p=>new(p.X-at.X+400,p.Y-at.Y+240);
        var window=new RectangleF(0,0,800,480);
        var lights=s.AmbienceLights(view,null,[],[new(6016,32600,32800,0),new(3442,32600,32800,5)],window);
        t.Equal(1,lights.Length,"k=0 不成光源");
        t.Check(lights.Length==1&&lights[0].Center==new PointF(400,240)&&lights[0].Source==5&&lights[0].MapEffectId==3442&&lights[0].ObjectId==0,"地圖特效光源中心與 k",lights);
        var crowd=Enumerable.Range(0,100).Select(i=>new MapEffectLight(3446,32600+i%5,32800+i/5%5,13)).ToArray();
        var budgeted=s.AmbienceLights(view,null,[],crowd,window);
        t.Check(budgeted.Length==54&&budgeted.Sum(l=>l.Source)==54*13,"地圖特效套 700 預算（和 ≥ 700 即停）",new{budgeted.Length,sum=budgeted.Sum(l=>l.Source)});
        var far=new MapEffectLight(3446,32700,32800,13);var near=new MapEffectLight(3442,32601,32801,5);
        var filtered=s.AmbienceLights(view,null,[],[far,near],window);
        t.Check(filtered.Length==1&&filtered[0].MapEffectId==3442,"畫面外遠處的光源被距離濾除",filtered);
        t.Equal(2,s.AmbienceLights(view,null,[],[far,near]).Length,"不給 window 時不濾除（舊呼叫相容）");
        var order=s.AmbienceLights(view,null,[],[new(3446,32604,32804,13),new(3442,32600,32800,5)],window);
        t.Check(order.Length==2&&order[0].MapEffectId==3442,"預算順序：離畫面中心近者先",order);
        t.Check(AmbienceLightMap.Reaches(window,new(-311,240))&&!AmbienceLightMap.Reaches(window,new(-312,240))&&AmbienceLightMap.Reaches(window,new(400,480+155))&&!AmbienceLightMap.Reaches(window,new(400,480+156)),"距離濾除邊界＝最大光源圖 312×156");

        // 4. Source.lmp 格式：16 槽、共用偏移、空槽、越界拒絕。
        var images=AmbienceLightImages.Parse(SyntheticLmp());
        t.Check(images.Width(0)==2&&images.Height(0)==1&&images.Offset(0)==images.Offset(1)&&images.Width(2)==4&&images.Offset(2)==images.Offset(3),"槽寬高與共用偏移");
        t.Check(images.Width(15)==0&&images.Image(15).IsEmpty&&images.Image(16).IsEmpty,"空槽沒有像素");
        t.Check(images.Image(1).SequenceEqual(new byte[]{100,200}),"槽 1 像素");
        var broken=SyntheticLmp();BitConverter.GetBytes(4u).CopyTo(broken,92);BitConverter.GetBytes(3u).CopyTo(broken,88);
        t.Throws<InvalidDataException>(()=>AmbienceLightImages.Parse(broken),"越界的槽被拒絕");
        t.Throws<InvalidDataException>(()=>AmbienceLightImages.Parse(new byte[100]),"截斷的標頭被拒絕");
        t.Throws<IOException>(()=>AmbienceLightImages.Load(Path.Combine(AppContext.BaseDirectory,"no-such-client")),"缺客戶端時回報讀取錯誤，不退回替代圖");

        // 5. 光照圖：清空、依 k 蓋章、重疊取最大值、空槽不畫。
        var map=new AmbienceLightMap();
        map.Render([new(1,new(10,10),2),new(2,new(12,10),2),new(3,new(100,100),15)],images);
        // k=2：4×2，左 ((10−4)>>1)&~3 = 0、上 (10−2)>>1 = 4；第二個左 ((12−4)>>1)&~3 = 4。
        t.Check(map[0,4]==10&&map[3,5]==80&&map[4,4]==10&&map[7,5]==80,"蓋章位置",new{a=map[0,4],b=map[3,5],c=map[4,4],d=map[7,5]});
        // k=1：2×1 置於 (2,9) → 左 0、上 4，與上一張的左上角重疊。
        map.Render([new(1,new(10,10),2),new(2,new(2,9),1)],images);
        t.Check(map[0,4]==100&&map[1,4]==200&&map[2,4]==30&&map[0,5]==50,"重疊取最大值，不相加",new{a=map[0,4],b=map[1,4],c=map[2,4]});
        map.Render([new(3,new(100,100),15)],images);
        t.Check(Enumerable.Range(0,AmbienceLightMap.Width).All(x=>Enumerable.Range(0,AmbienceLightMap.Height).All(y=>map[x,y]==0)),"第 15 槽空，不補光圈；Render 先清空");

        // 6. 夜晚明暗：有光處比無光處亮（係數表 mode 0）。
        using(var bitmap=new Bitmap(8,4,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
            for(int x=0;x<8;x++)for(int y=0;y<4;y++)bitmap.SetPixel(x,y,Color.FromArgb(255,248,248,248));
            var lit=new AmbienceLightMap();lit.Stamp(2,2,Enumerable.Repeat((byte)255,4).ToArray(),2,2);
            var night=new AmbienceFrame(0,0,0){Level=1};
            AmbiencePainter.Shade(bitmap,new(0,0,8,4),new(0,0,8,4),night,lit);
            // 800→8 縮放後只有 x=0、y=0 對應到光照圖 (0,0)。
            t.Check(bitmap.GetPixel(0,0).R>bitmap.GetPixel(7,3).R,"光照處較亮",new{lit=bitmap.GetPixel(0,0).R,dark=bitmap.GetPixel(7,3).R});
        }

        // 7. （選用）本機原客戶端 Source.lmp：AM §3.1 的尺寸表。
        if(Environment.GetEnvironmentVariable("TW_MODULE_CLIENT_CHECKS")=="1"){
            var real=AmbienceLightImages.Load();
            int[] w=[16,16,32,32,72,72,144,144,200,200,240,240,288,288,312,0],h=[8,8,16,16,36,36,72,72,100,100,120,120,144,144,156,0];
            t.Check(Enumerable.Range(0,16).All(k=>real.Width(k)==w[k]&&real.Height(k)==h[k]),"原客戶端 Source.lmp 16 槽尺寸");
            t.Check(Enumerable.Range(0,7).All(i=>real.Offset(2*i)==real.Offset(2*i+1))&&real.Offset(14)==0x19680,"原客戶端配對槽共用偏移");
            t.Check(real.Image(0).ToArray().Any(v=>v>0),"第 0 槽不是全黑");
        }
    }
}
