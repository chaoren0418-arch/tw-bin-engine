using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// T319 (#757) ref-client-xml 離線測試：只用合成 Tile.idx／Tile.pak（記憶體或測試輸出目錄下的暫存檔），
// 不讀真實客戶端或參考客戶端、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class RefClientXmlSourceTests {
    static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static byte[] Bytes(string s)=>Encoding.UTF8.GetBytes(s);

    static (byte[] Idx,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var idx=new MemoryStream();using var pak=new MemoryStream();using var w=new BinaryWriter(idx);
        w.Write((uint)files.Length);
        foreach(var (name,data) in files){
            w.Write((uint)pak.Position);var raw=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(raw,0);w.Write(raw);w.Write((uint)data.Length);pak.Write(data);
        }
        w.Flush();return (idx.ToArray(),pak.ToArray());
    }

    // Inverse of UiXmlSource.Decrypt (key/IV of sub_A9FA30): "<?xm"+body → "X?xm"+CBC(body), tail XORed with the running IV.
    static byte[] Encrypt(byte[] plain){
        byte[] key=[0xDC,0x84,0x01,0x21,0x2A,0x40,0x20,0x0A,0xDD,0x25,0xB9,0xA7,0x0D,0xB9,0xC9,0x4E];
        byte[] iv=[0x3E,0x09,0x78,0xAA,0xC4,0xD5,0x30,0x63,0x30,0x0C,0x5F,0x9A,0x80,0x7F,0x22,0x46];
        using var aes=Aes.Create();aes.Key=key;
        var output=new byte[plain.Length];output[0]=(byte)'X';output[1]=plain[1];output[2]=plain[2];output[3]=plain[3];
        int bodyLength=plain.Length-4,full=bodyLength/16*16;var block=new byte[16];var cipher=new byte[16];
        for(int i=0;i<full;i+=16){
            for(int k=0;k<16;k++)block[k]=(byte)(plain[4+i+k]^iv[k]);
            aes.EncryptEcb(block,cipher,PaddingMode.None);
            cipher.CopyTo(output,4+i);cipher.CopyTo(iv,0);
        }
        for(int k=0;k<bodyLength-full;k++)output[4+full+k]=(byte)(plain[4+full+k]^iv[k]);
        return output;
    }

    // Only the files this test created are removed (never a recursive delete).
    sealed class Scratch:IDisposable {
        public string Dir{get;}
        readonly List<string> files=[];readonly List<string> dirs=[];
        public Scratch(string name){
            string parent=Path.Combine(AppContext.BaseDirectory,"module-tests-tmp");
            Dir=Path.Combine(parent,name+"-"+Environment.ProcessId);
            if(!Directory.Exists(parent)){Directory.CreateDirectory(parent);dirs.Add(parent);}
            if(!Directory.Exists(Dir)){Directory.CreateDirectory(Dir);dirs.Insert(0,Dir);}
        }
        public string Write(string name,byte[] data){string p=Path.Combine(Dir,name);File.WriteAllBytes(p,data);files.Add(p);return p;}
        public string Sub(string name){string p=Path.Combine(Dir,name);if(!Directory.Exists(p)){Directory.CreateDirectory(p);dirs.Insert(0,p);}return p;}
        public string WriteIn(string sub,string name,byte[] data){string p=Path.Combine(Sub(sub),name);File.WriteAllBytes(p,data);files.Add(p);return p;}
        public void Dispose(){
            foreach(var f in files)try{File.Delete(f);}catch(IOException){}catch(UnauthorizedAccessException){}
            foreach(var d in dirs)try{if(Directory.Exists(d)&&!Directory.EnumerateFileSystemEntries(d).Any())Directory.Delete(d,false);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    [WorldModuleTest("ref-client-xml")]
    static void Run(WorldModuleTestContext t){
        Resolve(t);
        Memory(t);
        Files(t);
        Hook(t);
    }

    static void Resolve(WorldModuleTestContext t){
        using var s=new Scratch("ref-client-xml-settings");
        t.Equal(("D:\\ref","env"),RefClientXmlSource.ResolveRoot(" D:\\ref ",null),"TW_REF_CLIENT wins and is trimmed");
        t.Equal((RefClientXmlSource.DefaultRoot,"default"),RefClientXmlSource.ResolveRoot(null,null),"nothing set → kor25 default (PAK§3)");
        t.Equal(@"D:\381懶人包kor25(原)",RefClientXmlSource.DefaultRoot,"default equals the TW_SPELLLIST_CLIENT default");
        t.Equal((RefClientXmlSource.DefaultRoot,"default"),RefClientXmlSource.ResolveRoot("  ",Path.Combine(s.Dir,"absent.json")),"blank env + missing settings file → default");
        string path=s.Write("native-ui.json",Bytes("{\"nativeUi\":true,\"refClient\":\"E:\\\\t89\"}"));
        t.Equal(("E:\\t89","settings"),RefClientXmlSource.ResolveRoot(null,path),"native-ui.json refClient string");
        t.Equal(("D:\\ref","env"),RefClientXmlSource.ResolveRoot("D:\\ref",path),"env beats settings");
        File.WriteAllBytes(path,Bytes("{\"refClient\":false}"));
        t.Equal(((string?)null,"settings"),RefClientXmlSource.ResolveRoot(null,path),"refClient false → off");
        File.WriteAllBytes(path,Bytes("{\"refClient\":\"\"}"));
        t.Equal(((string?)null,"settings"),RefClientXmlSource.ResolveRoot(null,path),"refClient \"\" → off");
        File.WriteAllBytes(path,Bytes("{\"refClient\":7}"));
        t.Equal((RefClientXmlSource.DefaultRoot,"default"),RefClientXmlSource.ResolveRoot(null,path),"refClient of another type ignored");
        File.WriteAllBytes(path,Bytes("{not json"));
        t.Equal((RefClientXmlSource.DefaultRoot,"default"),RefClientXmlSource.ResolveRoot(null,path),"malformed settings ignored");

        // Configured(): reads the real environment variable; set and restore it in-process only.
        string? saved=Environment.GetEnvironmentVariable(RefClientXmlSource.EnvironmentVariable);
        try{
            string main=s.Sub("main");
            Environment.SetEnvironmentVariable(RefClientXmlSource.EnvironmentVariable,main+Path.DirectorySeparatorChar);
            t.Check(RefClientXmlSource.Configured(main,path)==null,"same folder as the main client → no reference");
            Environment.SetEnvironmentVariable(RefClientXmlSource.EnvironmentVariable,"relative\\kor25");
            t.Check(RefClientXmlSource.Configured(main,path)==null,"relative path → no reference");
            string other=s.Sub("other");
            Environment.SetEnvironmentVariable(RefClientXmlSource.EnvironmentVariable,other);
            var c=RefClientXmlSource.Configured(main,path);
            t.Check(c is {Source:"env"}&&c.Root==Path.GetFullPath(other),"env path → configured source",c?.Root);
            Environment.SetEnvironmentVariable(RefClientXmlSource.EnvironmentVariable,null);
            File.WriteAllBytes(path,Bytes("{\"refClient\":false}"));
            t.Check(RefClientXmlSource.Configured(main,path)==null,"settings off → no reference");
        }finally{Environment.SetEnvironmentVariable(RefClientXmlSource.EnvironmentVariable,saved);}
    }

    static void Memory(WorldModuleTestContext t){
        byte[] loginC=Bytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Window name=\"LoginUI-c\"/>"),
            login=Bytes("<?xml version=\"1.0\"?><Window name=\"LoginUI\"/>"),
            select=Bytes("<?xml version=\"1.0\"?><Window name=\"SelectUI\" pad=\"0123456789abcdef0123456789\"/>"),
            plain=Bytes("\uFEFF  <Window name=\"Plain\"/>");
        var encLoginC=Encrypt(loginC);var encSelect=Encrypt(select);
        var (idx,pak)=Archive(("LoginUI.xml",Encrypt(login)),("LoginUI-c.xml",encLoginC),("SelectUI.xml",encSelect),("Plain.xml",plain),
            ("Junk.xml",[1,2,3,4]),("Dup.xml",Encrypt(login)),("Dup.xml",Encrypt(select)),("Empty.xml",[]));
        var src=RefClientXmlSource.FromImages("Z:\\synthetic-ref",idx,pak);

        var a=src.TryRead("LoginUI");
        t.Check(a is {ResolvedName:"LoginUI-c.xml",Encrypted:true},"\"-c\" first (FileManager order)",a?.ResolvedName);
        t.Check(a!=null&&a.Plain.AsSpan().SequenceEqual(loginC),"encrypted entry decrypted in memory");
        t.Equal(Sha(loginC),a?.PlainSha256,"plain SHA-256 recorded");
        t.Equal(Sha(encLoginC),a?.BlobSha256,"stored blob SHA-256 recorded");
        t.Equal(Sha(idx),a?.IndexSha256,"Tile.idx SHA-256 recorded");
        t.Equal(Path.GetFullPath("Z:\\synthetic-ref"),a?.Root,"root recorded");
        t.Check(ReferenceEquals(a,src.TryRead("LoginUI.xml")),"cached per name (with or without .xml)");
        t.Check(src.TryRead("loginui-C.XML") is {ResolvedName:"LoginUI-c.xml"},"names are case-insensitive");

        var b=src.TryRead("SelectUI.xml");
        t.Check(b is {ResolvedName:"SelectUI.xml"}&&b.Plain.AsSpan().SequenceEqual(select),"no -c → plain name; multi-block body round-trips");
        var p=src.TryRead("Plain");
        t.Check(p is {Encrypted:false}&&p.Plain.AsSpan().SequenceEqual(plain),"unencrypted XML (BOM + whitespace + '<') accepted as is");
        t.Check(src.TryRead("Junk")==null,"not X?xm and not '<' → null");
        t.Check(src.TryRead("Dup")==null,"stored twice → null (never pick one)");
        t.Check(src.TryRead("Empty")==null,"size 0 → null");
        t.Check(src.TryRead("AgitAuctionUI")==null,"absent → null (caller falls back)");
        t.Check(src.TryRead("..\\LoginUI")==null&&src.TryRead("a/b")==null&&src.TryRead(" ")==null,"path-like or blank names → null");
        t.Check(src.Unavailable==null,"usable archive: no Unavailable reason");
        t.Equal(3,src.Served.Count,"Served lists LoginUI, SelectUI, Plain");

        // Out-of-range entry: idx says 64 bytes past the end of the pak.
        var (idx2,pak2)=Archive(("Far.xml",Encrypt(login)));
        BitConverter.GetBytes((uint)1000).CopyTo(idx2,4);
        t.Check(RefClientXmlSource.FromImages("Z:\\r",idx2,pak2).TryRead("Far")==null,"offset+size past pak → null");

        var bad=RefClientXmlSource.FromImages("Z:\\r",[2,0,0,0,1],[]);
        t.Check(bad.TryRead("LoginUI")==null&&bad.Unavailable!=null,"malformed idx → null with a reason",bad.Unavailable);

        // A record whose 20-byte name has no NUL and odd bytes: skipped, the rest still readable.
        var (idx3,pak3)=Archive(("ABCDEFGHIJKLMNOPQRST",[0x3C]),("Ok.xml",Bytes("<a/>")));
        idx3[4+4]=0x07;
        var odd=RefClientXmlSource.FromImages("Z:\\r",idx3,pak3);
        t.Check(odd.TryRead("Ok") is {Encrypted:false},"unprintable name skipped, other entries usable");
    }

    static void Files(WorldModuleTestContext t){
        using var s=new Scratch("ref-client-xml-files");
        var none=new RefClientXmlSource(Path.Combine(s.Dir,"no-such-client"));
        t.Check(none.TryRead("LoginUI")==null&&none.Unavailable!=null,"missing reference client → null with a reason",none.Unavailable);
        t.Check(none.TryRead("SelectUI")==null,"stays unavailable without retrying");

        byte[] xml=Bytes("<?xml version=\"1.0\"?><Window name=\"FishingUI\"/>");
        var (idx,pak)=Archive(("FishingUI.xml",Encrypt(xml)));
        string root=s.Sub("ref");
        s.WriteIn("ref","Tile.idx",idx);
        var onlyIdx=new RefClientXmlSource(root);
        t.Check(onlyIdx.TryRead("FishingUI")==null&&onlyIdx.Unavailable!=null,"Tile.pak missing → null");
        s.WriteIn("ref","Tile.pak",pak);
        var src=new RefClientXmlSource(root);
        var r=src.TryRead("FishingUI");
        t.Check(r!=null&&r.Plain.AsSpan().SequenceEqual(xml)&&r.IndexSha256==Sha(idx),"reads Root\\Tile.idx/Tile.pak from disk",r?.Root);
        t.Check(File.ReadAllBytes(Path.Combine(root,"Tile.pak")).AsSpan().SequenceEqual(pak)&&File.ReadAllBytes(Path.Combine(root,"Tile.idx")).AsSpan().SequenceEqual(idx),"reference files untouched");
    }

    static void Hook(WorldModuleTestContext t){
        using var s=new Scratch("ref-client-xml-hook");
        byte[] mainPanel=Bytes("<?xml version=\"1.0\"?><Window name=\"main\"/>"),refPanel=Bytes("<?xml version=\"1.0\"?><Window name=\"ref\"/>"),
            refOnly=Bytes("<?xml version=\"1.0\"?><Window name=\"OptionUI\" note=\"中\"/>");
        var (mIdx,mPak)=Archive(("Panel-c.xml",Encrypt(mainPanel)));
        string main=s.Sub("main");s.WriteIn("main","Tile.idx",mIdx);s.WriteIn("main","Tile.pak",mPak);
        using var tile=new ClientArchive(main,new ClientArchivePin("Tile"));
        var (rIdx,rPak)=Archive(("Panel.xml",Encrypt(refPanel)),("OptionUI-c.xml",Encrypt(refOnly)));

        var xml=new UiXmlSource(tile);
        t.Check(xml.Reference==null,"no reference by default (synthetic UiResources keep their behaviour)");
        t.Check(xml.TryReadPlain("OptionUI")==null,"without reference: absent → null");
        t.Throws<UiLayoutException>(()=>xml.ReadText("OptionUI"),"without reference: ReadText still throws");

        xml.Reference=RefClientXmlSource.FromImages("Z:\\synthetic-ref",rIdx,rPak);
        var m=xml.TryReadPlain("Panel");
        t.Check(m is {ResolvedName:"Panel-c.xml"}&&m.Value.Plain.AsSpan().SequenceEqual(mainPanel),"main client wins over the reference",m?.ResolvedName);
        t.Check(xml.Reference.Served.Count==0,"reference not even asked when the main client has the XML");
        var o=xml.TryReadPlain("OptionUI");
        t.Check(o is {ResolvedName:"OptionUI-c.xml"}&&o.Value.Plain.AsSpan().SequenceEqual(refOnly),"main lacks it → reference served",o?.ResolvedName);
        var (text,resolved)=xml.ReadText("OptionUI.xml");
        t.Check(resolved=="OptionUI-c.xml"&&text.Contains("note=\"中\""),"ReadText decodes the reference XML (UTF-8)",resolved);
        t.Check(xml.TryReadPlain("GameRankingUI")==null,"neither has it → null");
        t.Throws<UiLayoutException>(()=>xml.ReadText("GameRankingUI"),"neither has it → ReadText throws (caller keeps WinForms fallback)");
    }
}
