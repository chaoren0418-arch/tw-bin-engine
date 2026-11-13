using P06;
using System.Security.Cryptography;
using System.Text.Json;

// T459（#1044）：混色查表與 motion 規則的離線測試。不讀客戶端、不需要遊戲、網路或 DB。
// 預期值是 T448（docs/reference/asset-recipes/motion-blend.md）記錄的邊界、SHA-256 與八方向向量；表本身全部由 BlendRules 算出，不寫成常數。
// 若本機 engine/assets 有基線檔（CI 沒有），另外做逐 byte 比對。
// 群組 motion-blend；同時掛在 CI 已在跑的 native-ui-resources 之下（與 asset-prep 相同做法，不改 build.yml）。
partial class Checks {
    // 基線 r29 assets 的 SHA-256（T448 已證明算式產生的內容與這三個檔逐 byte 相同；engine/_integration/baseline-pins.json 也記同一值）。
    static readonly (ChannelBlend Kind,string Sha256)[] MbBaseline=[
        (ChannelBlend.Screen,"9F394F6FBE392B7F25C76253EA29808E46B3B6653676AE1A28E242F78923AD3C"),
        (ChannelBlend.Multiply,"4E2A67F562DCE5734928F33C7CF67893289BE22CBBA9E2B146490133A4D9ECA8"),
        (ChannelBlend.Half,"52F925E71EDB30A18D4321EE28BABA7E537862278822D9BDE166DCDFEAC5E5CB")];

    [CheckGroup("motion-blend",Order=1),CheckGroup("native-ui-resources",Order=62),CheckGroup(CheckGroups.Default)]
    static void MotionBlendChecks(string repo){
        BlendFormulaChecks();
        BlendFileCompatChecks(repo);
        MotionRuleChecks(repo);
    }

    static void BlendFormulaChecks(){
        var kinds=new[]{ChannelBlend.Screen,ChannelBlend.Multiply,ChannelBlend.Half};
        const int N=BlendRules.ChannelValues,M=BlendRules.ChannelMax;
        // 對稱、值域、單調：三張表窮舉 1,024 格。
        foreach(var k in kinds){
            var t=BlendRules.Table(k);
            Check(t.Count==1024,$"MB {k} table has 1024 entries");
            bool sym=true,range=true,mono=true,match=true;
            for(int a=0;a<N;a++)for(int b=0;b<N;b++){
                int v=t[a*N+b];sym&=v==t[b*N+a];range&=v is >=0 and <=M;match&=v==BlendRules.Channel(k,a,b);
                if(b>0)mono&=v>=t[a*N+b-1];
            }
            Check(sym,$"MB {k} symmetric T[a][b]==T[b][a]");
            Check(range,$"MB {k} values stay in 0..31");
            Check(mono,$"MB {k} non-decreasing in each argument");
            Check(match,$"MB {k} table equals the channel formula");
        }
        // 邊界值。
        bool ok=true;
        for(int x=0;x<N;x++){
            ok&=BlendRules.Screen(0,x)==x&&BlendRules.Screen(x,0)==x&&BlendRules.Screen(M,x)==M&&BlendRules.Screen(x,M)==M;
            ok&=BlendRules.Multiply(0,x)==0&&BlendRules.Multiply(M,x)==x&&BlendRules.Multiply(x,M)==x;
            ok&=BlendRules.Half(x,x)==x;
        }
        Check(ok,"MB identities: screen(0,x)=x, screen(31,x)=31, multiply(0,x)=0, multiply(31,x)=x, half(x,x)=x");
        Check(BlendRules.Half(0,M)==15&&BlendRules.Half(1,0)==0&&BlendRules.Half(M,M-1)==30,"MB half rounds down: (0,31)=15, (1,0)=0, (31,30)=30");
        Check(BlendRules.Screen(1,1)==1&&BlendRules.Screen(16,16)==23&&BlendRules.Screen(30,30)==30,"MB screen corner values (1,1)=1 (16,16)=23 (30,30)=30");
        Check(BlendRules.Multiply(30,30)==29&&BlendRules.Multiply(1,30)==0&&BlendRules.Multiply(16,16)==8,"MB multiply corner values (30,30)=29 (1,30)=0 (16,16)=8");
        bool order=true;
        for(int a=0;a<N;a++)for(int b=0;b<N;b++){
            order&=BlendRules.Screen(a,b)>=Math.Max(a,b)&&BlendRules.Multiply(a,b)<=Math.Min(a,b);
            int h=BlendRules.Half(a,b);order&=h>=Math.Min(a,b)&&h<=Math.Max(a,b);
        }
        Check(order,"MB screen >= max, multiply <= min, half between");
        bool thrown=Throws(()=>BlendRules.Screen(32,0))&&Throws(()=>BlendRules.Multiply(-1,0))&&Throws(()=>BlendRules.Half(0,32));
        Check(thrown,"MB channel formulas reject values outside 0..31");
        // T448：整數式與原碼的 double 式（cvttsd2si 截斷）以及另一個等價式全同；31-floor(...) 不相符（900 格不同）。
        int doubleDiff=0,altDiff=0,wrongDiff=0;
        for(int a=0;a<N;a++)for(int b=0;b<N;b++){
            int hi=Math.Max(a,b),lo=Math.Min(a,b),v=BlendRules.Screen(a,b);
            if(v!=(int)Math.Truncate(((31.0-hi)/31.0)*lo+hi))doubleDiff++;
            if(v!=(31*(a+b)-a*b)/31)altDiff++;
            if(v!=31-(31-a)*(31-b)/31)wrongDiff++;
        }
        Check(doubleDiff==0,"MB screen integer form equals original double trunc form on all 1024 cells");
        Check(altDiff==0,"MB screen equals (31*(a+b)-a*b)/31 on all 1024 cells");
        Check(wrongDiff==900,"MB screen differs from 31-floor((31-a)(31-b)/31) on exactly 900 cells (T448)");
    }
    static bool Throws(Action a){try{a();return false;}catch(ArgumentOutOfRangeException){return true;}}

    static void BlendFileCompatChecks(string repo){
        foreach(var (k,sha) in MbBaseline){
            var bytes=BlendRules.ChannelFileBytes(k);
            Check(bytes.Length==2048,$"MB {k} file bytes are 2048");
            Check(Convert.ToHexString(SHA256.HashData(bytes))==sha,$"MB {k} computed file SHA-256 equals r29 baseline {sha[..8]}");
            var parsed=BlendRules.ParseChannelFile(bytes);
            Check(parsed.SequenceEqual(BlendRules.Table(k)),$"MB {k} file round-trip equals computed table");
            string local=Path.Combine(Path.GetFullPath(repo),"engine","assets","cache",BlendRules.FileName(k));
            if(File.Exists(local))Check(File.ReadAllBytes(local).AsSpan().SequenceEqual(bytes),$"MB {k} local baseline file byte-identical");
        }
        bool rejected=false;try{BlendRules.ParseChannelFile(new byte[2047]);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"MB file parser rejects a non-2048-byte table like the old renderer");
        rejected=false;try{BlendRules.Compose(new ushort[1023],0,0);}catch(ArgumentException){rejected=true;}
        Check(rejected,"MB compose rejects a table that is not 1024 entries");
        // 介面相容：Renderer 的三個 RGB555 混色函式，等於「用檔案版的表逐色版查」。
        var screenFile=BlendRules.ParseChannelFile(BlendRules.ChannelFileBytes(ChannelBlend.Screen));
        var multiplyFile=BlendRules.ParseChannelFile(BlendRules.ChannelFileBytes(ChannelBlend.Multiply));
        var halfFile=BlendRules.ParseChannelFile(BlendRules.ChannelFileBytes(ChannelBlend.Half));
        static ushort Rgb(int v,int g,int r)=>(ushort)(v|(g<<5)|(r<<10));
        bool same=true;int pairs=0;
        void Pair(ushort a,ushort b){pairs++;same&=Renderer.ScreenBlend(a,b)==BlendRules.Compose(screenFile,a,b)&&Renderer.MultiplyBlend(a,b)==BlendRules.Compose(multiplyFile,a,b)&&Renderer.Blend(a,b)==BlendRules.Compose(halfFile,a,b);}
        // 每個色版都窮舉 32×32（另兩色版放不同值，確認位移沒有串色）。
        for(int a=0;a<32;a++)for(int b=0;b<32;b++){
            Pair(Rgb(a,7,19),Rgb(b,25,3));Pair(Rgb(5,a,30),Rgb(12,b,0));Pair(Rgb(31,9,a),Rgb(0,17,b));
        }
        var rng=new Random(1044);for(int i=0;i<20000;i++)Pair((ushort)rng.Next(0,32768),(ushort)rng.Next(0,32768));
        Check(same&&pairs==3*1024+20000,"MB Renderer screen/multiply/half equal file-table lookup per RGB555 channel");
        Check(Renderer.ScreenBlend(0x7FFF,0x1234)==0x7FFF&&Renderer.ScreenBlend(0,0x1234)==0x1234&&Renderer.MultiplyBlend(0x7FFF,0x1234)==0x1234,"MB RGB555 white/black boundaries");
        Check(Renderer.ScreenBlend(0x8000|0x0421,0x0421)==Renderer.ScreenBlend(0x0421,0x0421),"MB bit 15 is ignored like the old lookup");
    }

    static void MotionRuleChecks(string repo){
        // T448 八方向表：world_delta 與 screen_delta（基線、BIN 立即值、規則三方相同）。
        (int Dx,int Dy,int Sx,int Sy)[] expected=[(0,-1,-24,-12),(1,-1,0,-24),(1,0,24,-12),(1,1,48,0),(0,1,24,12),(-1,1,0,24),(-1,0,-24,12),(-1,-1,-48,0)];
        var dirs=MotionRules.Directions();
        Check(dirs.Length==8&&dirs.Select((d,i)=>d.Id==i&&(d.Dx,d.Dy,d.Sx,d.Sy)==expected[i]).All(x=>x),"MO eight directions match T448 table");
        Check(dirs.All(d=>d.Dx==MovementRules.Dx[d.Id]&&d.Dy==MovementRules.Dy[d.Id]),"MO directions come from MovementRules.Dx/Dy");
        Check(dirs.All(d=>d.Sx%4==0&&d.Sy%4==0),"MO screen step splits into four quarter steps (BIN 0x01143F10)");
        Check(dirs.All(d=>{var o=dirs[(d.Id+4)%8];return o.Dx==-d.Dx&&o.Dy==-d.Dy&&o.Sx==-d.Sx&&o.Sy==-d.Sy;}),"MO opposite directions are negated");
        Check(dirs.All(d=>d.Dx==Math.Sign(Math.Round(Math.Sin(d.Id*Math.PI/4),9))&&d.Dy==-Math.Sign(Math.Round(Math.Cos(d.Id*Math.PI/4),9))),"MO closed form dx=sgn(sin), dy=-sgn(cos)");
        Check(!ReferenceEquals(MotionRules.Directions(),MotionRules.Directions())&&!ReferenceEquals(MotionRules.Spawn(),MotionRules.Spawn()),"MO accessors return fresh arrays");
        bool rejected=false;try{MotionRules.ForHeading(8);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"MO heading outside 0..7 rejected");
        Check(MotionRules.FrameMs==166&&MotionRules.FrameMs==4*1000/24,"MO frame_ms = floor(4 tick * 1000 / 24) = 166");
        Check(MotionRules.Spawn().SequenceEqual([32588,32928]),"MO offline spawn constant 32588,32928");

        // 介面相容：Assets 不需要 motion.json；夾具故意放一份錯誤的 motion.json，確認不被讀取。
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1044","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        var assets=new Assets(MbAssetsFixture(Path.Combine(root,"no-motion"),null));
        Check(assets.FrameMs==166&&assets.Spawn.SequenceEqual(MotionRules.Spawn())&&assets.Directions.SequenceEqual(MotionRules.Directions()),"MO Assets loads rules without motion.json");
        var stale=new Assets(MbAssetsFixture(Path.Combine(root,"stale-motion"),"{\"frame_ms\":999,\"spawn\":[1,1],\"directions\":[]}"));
        Check(stale.FrameMs==166&&stale.Spawn.SequenceEqual(MotionRules.Spawn())&&stale.Directions.Length==8,"MO Assets ignores a stale motion.json");
        var w=new World(assets);
        Check(w.X==MotionRules.SpawnX&&w.Y==MotionRules.SpawnY,"MO World starts at offline spawn");
        // 若本機有基線 motion.json（CI 沒有），讀它的產品欄位比對規則。
        string local=Path.Combine(Path.GetFullPath(repo),"engine","assets","motion.json");
        if(File.Exists(local)){
            var m=Json.Read(local);
            var fromFile=m.GetProperty("directions").EnumerateArray().Select(d=>new Direction(d.I("direction"),d.A("world_delta")[0],d.A("world_delta")[1],d.A("screen_delta")[0],d.A("screen_delta")[1])).ToArray();
            Check(m.I("frame_ms")==MotionRules.FrameMs&&m.A("spawn").SequenceEqual(MotionRules.Spawn())&&fromFile.SequenceEqual(MotionRules.Directions()),"MO local baseline motion.json equals rules");
            Check(m.GetProperty("directions").EnumerateArray().All(d=>d.A("internal_delta").SequenceEqual(new[]{2*d.A("world_delta")[0],d.A("world_delta")[1]})),"MO baseline internal_delta = (2*dx, dy)");
        }
    }

    /// <summary>Assets 建構式需要的最小素材樹（空場景、96 格合成角色）；motionJson 為 null 時不寫 motion.json。</summary>
    static string MbAssetsFixture(string root,string? motionJson){
        void Save(string path,object data){string file=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllBytes(file,JsonSerializer.SerializeToUtf8Bytes(data));}
        object Pixel(string key)=>new{key,bounds=new[]{0,0,24,24},blocks=new[]{new{a=0,b=0,z=0,source_order=0,pixels=new[]{new[]{0,0,31}}}}};
        Save("scene/manifest.json",new{schema="p22-dual-map-v1",world_inclusive_bounds=new[]{Coordinates.MinX,Coordinates.MinY,Coordinates.MaxX,Coordinates.MaxY},pixel_origin=new[]{0,756},terrain_stride=Coordinates.Columns,terrain_rows=Coordinates.Rows,canvas_size=new[]{3200,2000},draw_count=0});
        Save("scene/tile-pixels.json",Array.Empty<object>());Save("scene/draw-commands.json",Array.Empty<object>());
        Save("character/blocks.json",new{schema="p06-spr-blocks-v1",frames=Enumerable.Range(0,96).Select(i=>Pixel("a"+i)).ToArray()});
        Save("character/animation.json",new{schema="p04-character-v1",mapping=Enumerable.Range(0,8).SelectMany(d=>new[]{("breath",8,d*12),("walk",4,d*12+8)}.Select(m=>new{action=m.Item1,direction=d,sequence=Enumerable.Range(m.Item3,m.Item2).Select(i=>new{key="a"+i}).ToArray()})).ToArray()});
        Save("integrity.json",new{schema="p06-assets-integrity-v1",files=Array.Empty<object>()});
        if(motionJson!=null)File.WriteAllText(Path.Combine(root,"motion.json"),motionJson);
        return root;
    }
}
