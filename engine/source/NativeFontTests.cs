using System.Security.Cryptography;
namespace P06;

// T321 (#759) native-font 離線測試：名稱／稱號字型（GDI MingLiU 12 px、FW_NORMAL、字元集 136、4 向 1 px 描邊）。
// 只用合成資料（記憶體遮罩、測試輸出目錄下的假 .fnt）；不讀真實客戶端、不啟動遊戲。CI 由 --module-tests 自動發現執行。
// GDI 光柵化依執行機器有沒有 MingLiU 分兩支檢查：有就核對字寬（半形 6、全形 12、高 12），沒有就核對「回 null＋原因」讓呼叫端走備援。
static class NativeFontTests {
    static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    // Only the files and folders this test created are removed (never a recursive delete).
    sealed class Scratch:IDisposable {
        public string Dir{get;}
        readonly List<string> files=[];readonly List<string> dirs=[];
        public Scratch(string name){
            string parent=Path.Combine(AppContext.BaseDirectory,"module-tests-tmp");
            Dir=Path.Combine(parent,name+"-"+Environment.ProcessId);
            if(!Directory.Exists(parent)){Directory.CreateDirectory(parent);dirs.Add(parent);}
            if(!Directory.Exists(Dir)){Directory.CreateDirectory(Dir);dirs.Insert(0,Dir);}
        }
        public string Sub(string name){string p=Path.Combine(Dir,name);if(!Directory.Exists(p)){Directory.CreateDirectory(p);dirs.Insert(0,p);}return p;}
        public string WriteIn(string sub,string name,byte[] data){string p=Path.Combine(Sub(sub),name);File.WriteAllBytes(p,data);files.Add(p);return p;}
        public void Dispose(){
            foreach(var f in files)try{File.Delete(f);}catch(IOException){}catch(UnauthorizedAccessException){}
            foreach(var d in dirs)try{if(Directory.Exists(d)&&!Directory.EnumerateFileSystemEntries(d).Any())Directory.Delete(d,false);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    [WorldModuleTest("native-font")]
    static void Run(WorldModuleTestContext t){
        Constants(t);
        Compose(t);
        FontFiles(t);
        Rasterize(t);
        Drawing(t);
    }

    static void Constants(WorldModuleTestContext t){
        // sub_8444A0: CreateFontA(12,0,0,0,400,0,0,0,136,0,0,0,0,"MingLiU")（UI§1 V4）。
        t.Equal("MingLiU",NativeNameFont.Face,"face MingLiU");
        t.Equal(12,NativeNameFont.Height,"height 12");
        t.Equal(400,NativeNameFont.Weight,"FW_NORMAL");
        t.Equal((byte)136,NativeNameFont.CharSet,"CHINESEBIG5 charset 136");
        t.Equal(0,NativeNameFont.Quality,"DEFAULT_QUALITY as in the original call");
        t.Equal(NameLayout.FontPixels,NativeNameFont.Height,"layout font pixels agree");
        t.Check(NativeNameFont.OutlineOffsets.Length==4&&NativeNameFont.OutlineOffsets.All(o=>Math.Abs(o.Dx)+Math.Abs(o.Dy)==1)&&NativeNameFont.OutlineOffsets.Distinct().Count()==4,"4-way 1 px outline");
        t.Check(NativeNameFont.IsMingLiU("MingLiU")&&NativeNameFont.IsMingLiU("mingliu")&&NativeNameFont.IsMingLiU("細明體"),"MingLiU face names accepted");
        t.Check(!NativeNameFont.IsMingLiU("PMingLiU")&&!NativeNameFont.IsMingLiU("Arial")&&!NativeNameFont.IsMingLiU(null)&&!NativeNameFont.IsMingLiU(""),"substituted faces rejected");
    }

    static void Compose(WorldModuleTestContext t){
        const int F=unchecked((int)0xFFFFE000),O=unchecked((int)0xFF000000);
        // 1 ink pixel → 3×3: plus-shaped outline around the fill, corners transparent.
        var one=NativeNameFont.Compose(new(1,1,[true]),F,O,out int w,out int h);
        t.Check(w==3&&h==3,"border of 1 px on every side",new{w,h});
        t.Check(one.SequenceEqual(new[]{0,O,0,O,F,O,0,O,0}),"plus outline, fill on top, corners clear",one);
        // Two adjacent ink pixels: the fill wins where an outline stamp overlaps ink.
        var two=NativeNameFont.Compose(new(2,1,[true,true]),F,O,out w,out h);
        t.Check(w==4&&h==3&&two.SequenceEqual(new[]{0,O,O,0,O,F,F,O,0,O,O,0}),"fill drawn after outline",two);
        // Empty text mask (width 0): still a transparent 2 px wide strip, nothing painted.
        var none=NativeNameFont.Compose(new(0,12,[]),F,O,out w,out h);
        t.Check(w==2&&h==14&&none.All(p=>p==0),"empty mask stays transparent");
        // No ink → nothing painted.
        var blank=NativeNameFont.Compose(new(2,2,new bool[4]),F,O,out _,out _);
        t.Check(blank.All(p=>p==0),"blank mask transparent");
    }

    static void FontFiles(WorldModuleTestContext t){
        using var s=new Scratch("native-font-files");
        t.Equal(0,NativeNameFont.FontFiles(Path.Combine(s.Dir,"no-client")).Length,"missing client → empty list");
        string root=s.Sub("client");
        t.Equal(0,NativeNameFont.FontFiles(root).Length,"no Font folder → empty list");
        // Synthetic stand-ins with the local file sizes (PAK§4.2): eng 1,140 B, kor 57,624 B, small 184 B. Content is generated.
        byte[] eng=new byte[1140],kor=new byte[57624],small=new byte[184],odd=new byte[25];
        for(int i=0;i<eng.Length;i++)eng[i]=(byte)i;for(int i=0;i<kor.Length;i++)kor[i]=(byte)(i*7);small[0]=1;
        s.WriteIn("client\\Font","eng.fnt",eng);s.WriteIn("client\\Font","kor.fnt",kor);s.WriteIn("client\\Font","small.fnt",small);
        s.WriteIn("client\\Font","Korgulim.fnt",odd);s.WriteIn("client\\Font","readme.txt",[1]);s.WriteIn("client\\Font\\sub","nested.fnt",eng);
        var list=NativeNameFont.FontFiles(root);
        t.Equal("eng.fnt,kor.fnt,Korgulim.fnt,small.fnt",string.Join(",",list.Select(f=>f.Name)),"only top-level *.fnt, ordered by name");
        t.Check(list[0] is {Size:1140,Glyphs:95}&&list[0].Sha256==Sha(eng),"eng.fnt: 95 glyphs of 6×12 (12 B), SHA-256 recorded",list[0]);
        t.Check(list[1] is {Size:57624,Glyphs:2401}&&list[1].Sha256==Sha(kor),"kor.fnt: 2401 glyphs of 12×12 (24 B), SHA-256 recorded",list[1]);
        t.Check(list[2] is {Size:25,Glyphs:null},"size not a multiple of the glyph size → no glyph count",list[2]);
        t.Check(list[3] is {Size:184,Glyphs:null}&&list[3].Sha256==Sha(small),"small.fnt layout unknown → no glyph count",list[3]);
        t.Check(File.ReadAllBytes(Path.Combine(root,"Font","eng.fnt")).AsSpan().SequenceEqual(eng),"font files untouched");
        t.Equal<int?>(12,NativeNameFont.GlyphBytes("ENG.FNT"),"glyph bytes case-insensitive");
        t.Equal<int?>(24,NativeNameFont.GlyphBytes("kor-batang.fnt"),"kor variants 24 B");
        t.Equal<int?>(null,NativeNameFont.GlyphBytes("small.fnt"),"small unknown");
    }

    static void Rasterize(WorldModuleTestContext t){
        var a=NativeNameFont.Rasterize("A");
        if(NativeNameFont.Unavailable is {} reason){
            t.Check(a==null&&NativeNameFont.Glyphs("A",Color.White,Color.Black)==null,"MingLiU unavailable → null (caller keeps the GDI+ fallback)",reason);
            t.Check(reason.Length>0,"unavailable reason recorded",reason);
            return;
        }
        t.Check(NativeNameFont.IsMingLiU(NativeNameFont.FaceUsed),"GDI selected MingLiU",NativeNameFont.FaceUsed);
        t.Check(a is {Width:6,Height:12},"half-width glyph 6×12 (NameLayout 6 px per Big5 byte)",a is null?null:new{a.Width,a.Height});
        t.Check(a!=null&&a.Ink.Any(i=>i)&&!a.Ink.All(i=>i),"glyph has ink and background");
        var han=NativeNameFont.Rasterize("中");
        t.Check(han is {Width:12,Height:12},"full-width glyph 12×12",han is null?null:new{han.Width,han.Height});
        string name="天堂Lin";var n=NativeNameFont.Rasterize(name);
        t.Equal(NameLayout.GlyphWidth*NameLayout.ByteLength(name),n?.Width??-1,"width = 6 × Big5 bytes");
        t.Check(NativeNameFont.Rasterize("") is {Width:0,Height:12},"empty string → empty mask");
        var b1=NativeNameFont.Glyphs("A",Color.White,Color.Black);var b2=NativeNameFont.Glyphs("A",Color.White,Color.Black);
        t.Check(b1!=null&&ReferenceEquals(b1,b2)&&b1.Width==8&&b1.Height==14,"composed bitmap cached, 1 px border",b1 is null?null:new{b1.Width,b1.Height});
        t.Check(!ReferenceEquals(b1,NativeNameFont.Glyphs("A",Color.Gold,Color.Black)),"colour is part of the cache key");
        if(b1!=null){
            bool clearCorner=b1.GetPixel(0,0).A==0;bool hasFill=false,hasOutline=false;
            for(int y=0;y<b1.Height;y++)for(int x=0;x<b1.Width;x++){var c=b1.GetPixel(x,y);if(c.ToArgb()==Color.White.ToArgb())hasFill=true;if(c.ToArgb()==Color.Black.ToArgb())hasOutline=true;}
            t.Check(clearCorner&&hasFill&&hasOutline,"bitmap: transparent background, fill and outline colours only");
        }
    }

    static void Drawing(WorldModuleTestContext t){
        using var bmp=new Bitmap(320,40,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bmp)){
            g.Clear(Color.FromArgb(14,23,27));
            using var font=new Font("Microsoft JhengHei UI",9);
            bool native=PlayerHudPainter.NameLine(g,new Rectangle(4,4,300,25),font,"測試","騎士");
            t.Equal(NativeNameFont.Unavailable==null,native,"HUD name line uses the native font exactly when MingLiU is available");
            bool tooNarrow=PlayerHudPainter.NameLine(g,new Rectangle(4,4,5,25),font,"很長的名稱","騎士");
            t.Check(!tooNarrow,"name wider than the box → TextRenderer fallback with ellipsis");
            t.Check(!PlayerHudPainter.NameLine(g,new Rectangle(4,4,300,25),font,"","騎士"),"empty name → fallback line");
            bool drawn=NativeNameFont.Draw(g,"A",Color.White,Color.Black,10,10,2f);
            t.Equal(NativeNameFont.Unavailable==null,drawn,"Draw reports whether the native path painted");
        }
    }
}
