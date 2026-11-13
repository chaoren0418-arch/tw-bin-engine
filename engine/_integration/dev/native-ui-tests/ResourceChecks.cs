using P06.NativeUi;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// C1 ui-core-resources (Issue #461). Every archive, XML and .img here is synthesized by this file under scratch/;
// the real client is only read by the optional NativeUiRealClientChecks when TW_CLIENT_ROOT is set.
partial class Checks {
    // Test-side copy of the published key/IV (docs/reference/rea-scripts/ui_xml_decrypt.py) used to *encrypt* fixtures.
    static readonly byte[] UiKey=[0xDC,0x84,0x01,0x21,0x2A,0x40,0x20,0x0A,0xDD,0x25,0xB9,0xA7,0x0D,0xB9,0xC9,0x4E];
    static readonly byte[] UiIv=[0x3E,0x09,0x78,0xAA,0xC4,0xD5,0x30,0x63,0x30,0x0C,0x5F,0x9A,0x80,0x7F,0x22,0x46];

    static void Throws<T>(Action action,string label,string? contains=null) where T:Exception{
        try{action();}catch(T ex){Check(contains==null||ex.Message.Contains(contains),label+" message: "+ex.Message);return;}
        catch(Exception ex){throw new Exception(label+": expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message,ex);}
        throw new Exception(label+": no exception");
    }
    static byte[] UiEncrypt(byte[] plain){
        if(plain.Length<4||plain[0]!='<')throw new ArgumentException("fixture must start with <");
        using var aes=Aes.Create();aes.Key=UiKey;
        var body=plain.AsSpan(4);int full=body.Length/16*16;
        var blob=new byte[plain.Length];blob[0]=(byte)'X';plain.AsSpan(1,3).CopyTo(blob.AsSpan(1));
        var iv=(byte[])UiIv.Clone();var block=new byte[16];
        for(int i=0;i<full;i+=16){
            for(int k=0;k<16;k++)block[k]=(byte)(body[i+k]^iv[k]);
            aes.EncryptEcb(block,blob.AsSpan(4+i,16),PaddingMode.None);
            blob.AsSpan(4+i,16).CopyTo(iv);
        }
        for(int k=0;k<body.Length-full;k++)blob[4+full+k]=(byte)(body[full+k]^iv[k]);
        return blob;
    }
    static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static byte[] IdxRecord(uint offset,byte[] name20,uint size){var r=new byte[28];BitConverter.GetBytes(offset).CopyTo(r,0);name20.CopyTo(r,4);BitConverter.GetBytes(size).CopyTo(r,24);return r;}
    static byte[] Name20(string name){var b=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(b,0);return b;}
    static ClientArchivePin RawArchive(string dir,string name,byte[] idx,byte[] pak){
        Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+".idx"),idx);File.WriteAllBytes(Path.Combine(dir,name+".pak"),pak);
        return new(name);   // T479: a container is identified by name; anchors are added by the caller where needed
    }
    static ClientArchivePin Archive(string dir,string name,params (string Name,byte[] Data)[] items){
        using var pak=new MemoryStream();using var idx=new MemoryStream();idx.Write(BitConverter.GetBytes((uint)items.Length));
        foreach(var (n,d) in items){idx.Write(IdxRecord((uint)pak.Position,Name20(n),(uint)d.Length));pak.Write(d);}
        return RawArchive(dir,name,idx.ToArray(),pak.ToArray());
    }
    static byte[] Img(int w,int h,ushort flags,ushort key,params ushort[] pixels){
        var b=new byte[8+w*h*2];BitConverter.GetBytes((ushort)w).CopyTo(b,0);BitConverter.GetBytes((ushort)h).CopyTo(b,2);
        BitConverter.GetBytes(flags).CopyTo(b,4);BitConverter.GetBytes(key).CopyTo(b,6);
        for(int i=0;i<w*h;i++)BitConverter.GetBytes(pixels.Length==0?(ushort)0x1234:pixels[i%pixels.Length]).CopyTo(b,8+i*2);
        return b;
    }
    static byte[] Xml(string text,Encoding? encoding=null)=>UiEncrypt((encoding??new UTF8Encoding(false)).GetBytes(text));
    static int IndependentHash(string name)=>name.Sum(c=>(int)c)%16;
    static string SpriteName(int index)=>index==0?"Sprite":$"Sprite{index-1:00}";

    [CheckGroup("native-ui-resources",Order=1),CheckGroup(CheckGroups.Default,Order=30)]
    static void NativeUiResourceChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-461","synthetic");
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");

        // --- XML decryption: every tail length 0..15, plus whole-block bodies cross-checked against library CBC.
        for(int len=0;len<=64;len++){
            var plain=Encoding.ASCII.GetBytes("<?xm"+new string('a',len));
            for(int i=4;i<plain.Length;i++)plain[i]=(byte)(32+(i*7+len)%90);
            var blob=UiEncrypt(plain);
            Check(UiXmlSource.IsEncrypted(blob),"fixture encrypted "+len);
            Check(UiXmlSource.Decrypt(blob).AsSpan().SequenceEqual(plain),$"decrypt body {len} (tail {len%16})");
            if(len>0&&len%16==0){
                using var aes=Aes.Create();aes.Key=UiKey;
                Check(aes.DecryptCbc(blob.AsSpan(4),UiIv,PaddingMode.None).AsSpan().SequenceEqual(plain.AsSpan(4)),"whole blocks equal library CBC "+len);
            }
        }
        Throws<UiResourceException>(()=>UiXmlSource.Decrypt(Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?><a/>")),"plain XML is not accepted as encrypted","X?xm");
        Throws<UiResourceException>(()=>UiXmlSource.Decrypt([(byte)'X',(byte)'?']),"short blob rejected");

        // --- Text decoding: UTF-8 default (V4 (c)), declared encoding wins, invalid bytes rejected, BOM stripped.
        Check(UiXmlSource.DecodeText(new UTF8Encoding(false).GetBytes("<a t=\"血盟\"/>"),"u.xml")=="<a t=\"血盟\"/>","undeclared XML decodes as UTF-8");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);var big5=Encoding.GetEncoding(950);
        Check(UiXmlSource.DecodeText(big5.GetBytes("<?xml version=\"1.0\" encoding=\"big5\"?><a t=\"血盟\"/>"),"b.xml").Contains("血盟"),"declared big5 decodes as CP950");
        Check(UiXmlSource.DecodeText([0xEF,0xBB,0xBF,..Encoding.ASCII.GetBytes("<a/>")],"bom.xml")=="<a/>","UTF-8 BOM stripped");
        Throws<UiLayoutException>(()=>UiXmlSource.DecodeText(big5.GetBytes("<a t=\"血盟\"/>"),"cp950-undeclared.xml"),"undeclared CP950 bytes rejected (no silent replacement)","cp950-undeclared.xml");
        Throws<UiLayoutException>(()=>UiXmlSource.DecodeText(Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"no-such-enc\"?><a/>"),"e.xml"),"unknown declared encoding rejected");

        // --- Layout parsing: spaced '=', verbatim names, paths, required attributes, surf ranges.
        const string status="<?xml version=\"1.0\"?>\n<UI>\n <LEFT_WINDOW x = \"0\" y = \"475\" surfNumber = \"3168\">\n  <FOOD_GUAGE_BAR xRPos = \"12\" yRPos=\"-3\" surfNumber=\"5726~5728\"/>\n  <BTN surfNumber=\"1\"/><BTN surfNumber=\"2\"/>\n </LEFT_WINDOW>\n <CHAT_WINDOW yPos=\"600\" w=\"abc\" bad=\"9~3\" huge=\"1~200\" neg=\"-1\" empty=\"\"/>\n</UI>";
        var layout=UiLayout.Parse(status,"uistatus86-c.xml");
        var left=layout.Child("LEFT_WINDOW");
        Check(left.Int("x")==0&&left.Int("y")==475&&left.Surfs().SequenceEqual(new[]{3168}),"spaced attributes parsed");
        var food=layout.Child("LEFT_WINDOW/FOOD_GUAGE_BAR");
        Check(food.Name=="FOOD_GUAGE_BAR"&&food.Int("xRPos")==12&&food.Int("yRPos")==-3,"misspelled element kept verbatim, relative coords");
        Check(food.Surfs().SequenceEqual(new[]{5726,5727,5728}),"surf range a~b expanded in order");
        Check(layout.Child("CHAT_WINDOW").Int("yPos")==600&&layout.Child("CHAT_WINDOW").TryInt("x")==null,"bottom anchor yPos and optional attribute");
        Check(left.Children.Count==3&&left.ChildrenNamed("BTN").Count()==2&&left.Children[2].Path=="UI/LEFT_WINDOW/BTN[1]","children enumerate with indexed paths");
        Throws<UiLayoutException>(()=>layout.Child("LEFT_WINDOW/NOPE"),"missing node throws with file and path","uistatus86-c.xml:UI/LEFT_WINDOW/NOPE");
        Throws<UiLayoutException>(()=>layout.Child("LEFT_WINDOW/BTN"),"ambiguous node throws","不唯一");
        Throws<UiLayoutException>(()=>left.Int("w"),"missing attribute throws (no default coordinate)","缺少屬性 w");
        Throws<UiLayoutException>(()=>layout.Child("CHAT_WINDOW").Int("w"),"non-integer attribute throws","不是整數");
        Check(layout.Child("CHAT_WINDOW").Surfs("empty").Length==0,"empty surfNumber (real MACRO_ICON) → no surf");
        Throws<UiLayoutException>(()=>layout.Child("CHAT_WINDOW").Surfs("bad"),"reversed surf range rejected");
        Throws<UiLayoutException>(()=>layout.Child("CHAT_WINDOW").Surfs("huge"),"oversized surf range rejected");
        Throws<UiLayoutException>(()=>layout.Child("CHAT_WINDOW").Surfs("neg"),"negative surf rejected");
        Throws<UiLayoutException>(()=>UiLayout.Parse("<UI><a></UI>","broken.xml"),"malformed XML throws with file name","broken.xml");

        // --- Tile archive: -c first, fallback to plain name, missing both rejected.
        {
            string dir=Case("tile-fallback");
            var pin=Archive(dir,"Tile",("uistatus86-c.xml",Xml("<?xml version=\"1.0\"?><UI which=\"c\"/>")),("uistatus86.xml",Xml("<?xml version=\"1.0\"?><UI which=\"plain\"/>")),
                ("BookmarkUI.xml",Xml("<?xml version=\"1.0\"?><UI which=\"bookmark\"/>")),("odd.xml",Xml("<?xml version=\"1.0\"?><UI which=\"tail\" pad=\"12345\"/>")));
            using var tile=new ClientArchive(dir,pin);var xml=new UiXmlSource(tile);
            var a=xml.Load("uistatus86");
            Check(a.FileName=="uistatus86-c.xml"&&a.Root.Attr("which")=="c","-c version preferred");
            Check(xml.Load("uistatus86.xml").Root.Attr("which")=="c"&&xml.Load("uistatus86-c.xml").Root.Attr("which")=="c","-c preferred for explicit names");
            int before=UiResourceLog.Recent().Length;
            var b=xml.Load("BookmarkUI");
            Check(b.FileName=="BookmarkUI.xml"&&b.Root.Attr("which")=="bookmark","fallback to name without -c");
            Check(UiResourceLog.Recent().Any(l=>l=="ui-xml-fallback BookmarkUI-c.xml -> BookmarkUI.xml"),"fallback logged as ui-xml-fallback");
            Check(xml.Load("odd").Root.Attr("pad")=="12345","encrypted XML with short tail round-trips through Tile.pak");
            Throws<UiLayoutException>(()=>xml.Load("MapSystem"),"missing XML (both names) rejected","MapSystem.xml");
            Throws<UiLayoutException>(()=>xml.Load("..\\x"),"path-like XML name rejected");
            Check(xml.XmlNames().Count()==4,"Tile XML names enumerated");
        }

        // --- Archive boundaries, truncation and pins (each rejection is an independent fixture).
        {
            var ok=Archive(Case("ok"),"A",("x.img",Img(1,1,0,0)));
            using(var arc=new ClientArchive(Path.Combine(root,$"{caseNo:00}-ok"),ok))Check(arc.Find("X.IMG")!=null&&arc.Read(arc.Find("x.img")!).Length==10,"valid archive reads (case-insensitive name)");
            using(var arc=new ClientArchive(Case("missing"),ok))Check(arc.Find("x.img")==null&&arc.Entries.Count==0,"missing idx behaves as empty archive");

            ClientArchive Raw(string tag,byte[] idx,byte[] pak,Func<ClientArchivePin,ClientArchivePin>? edit=null){
                string dir=Case(tag);var pin=RawArchive(dir,"A",idx,pak);return new ClientArchive(dir,edit==null?pin:edit(pin));
            }
            byte[] One(uint off,string name,uint size)=>[..BitConverter.GetBytes(1u),..IdxRecord(off,Name20(name),size)];
            var pak10=new byte[10];
            using(var arc=Raw("count",[..BitConverter.GetBytes(2u),..IdxRecord(0,Name20("x.img"),10)],pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"idx count×28+4 ≠ size rejected","筆數");
            using(var arc=Raw("trunc-idx",One(0,"x.img",10)[..20],pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"truncated idx rejected");
            using(var arc=Raw("oob",One(4,"x.img",10),pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"offset+size beyond pak rejected","越界");
            using(var arc=Raw("ctrl",One(0,"x\u0001.img",10),pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"non-printable name rejected","不可列印");
            using(var arc=Raw("nonul",[..BitConverter.GetBytes(1u),..IdxRecord(0,Encoding.ASCII.GetBytes("ReNStatusWinUI-c.xml"),10)],pak10))Check(arc.Find("ReNStatusWinUI-c.xml")?.Size==10,"20-byte name without NUL accepted (real Tile.idx record 5420)");
            using(var arc=Raw("nonul-ctrl",[..BitConverter.GetBytes(1u),..IdxRecord(0,Encoding.ASCII.GetBytes("abcdefghijklmnopqrs\u0007"),10)],pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"20-byte name with control byte rejected","不可列印");
            using(var arc=Raw("empty-name",One(0,"",10),pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"empty name rejected");
            using(var arc=Raw("dup",[..BitConverter.GetBytes(2u),..IdxRecord(0,Name20("x.img"),5),..IdxRecord(5,Name20("X.img"),5)],pak10))Throws<UiResourceException>(()=>arc.Find("x.img"),"duplicate name in one idx rejected","重名");
            // T479: no whole-container pin. The pak only has to hold every listed entry (idx size and SHA are not compared).
            using(var arc=Raw("trunc-pak",One(0,"x.img",10),pak10[..9]))Throws<UiResourceException>(()=>arc.Find("x.img"),"pak shorter than the idx says rejected","越界");
            using(var arc=Raw("missing-pak",One(0,"x.img",10),pak10)){File.Delete(Path.Combine(root,$"{caseNo:00}-missing-pak","A.pak"));Throws<UiResourceException>(()=>arc.Find("x.img"),"idx without pak rejected","缺少 pak");}
            var big=9u*1024*1024;
            using(var arc=Raw("too-big",One(0,"x.img",big),new byte[big]))Throws<UiResourceException>(()=>arc.Read(arc.Find("x.img")!),"entry over 8 MiB rejected","8 MiB");
            using(var arc=Raw("zero",One(0,"x.img",0),pak10))Throws<UiResourceException>(()=>arc.Read(arc.Find("x.img")!),"zero-size entry rejected");
            using(var other=new ClientArchive(Case("foreign"),ok))Throws<UiResourceException>(()=>other.Read(new ClientArchiveEntry("B",0,"x.img",0,10)),"entry from another archive rejected");
        }

        // --- .img decoding.
        {
            Check(UiImage.ToArgb(0x7C00)==0xFFFF0000&&UiImage.ToArgb(0x03E0)==0xFF00FF00&&UiImage.ToArgb(0x001F)==0xFF0000FF,"RGB555 channel order (R high)");
            Check(UiImage.ToArgb(0x7FFF)==0xFFFFFFFF&&UiImage.ToArgb(0)==0xFF000000&&UiImage.ToArgb(0x0421)==0xFF080808,"5-bit → v*255/31");
            var keyed=UiImage.Decode(Img(2,2,1,0x324C,0x324C,0x7C00,0x001F,0x324C));
            Check(keyed.Argb[0]==0&&keyed.Argb[3]==0&&keyed.Argb[1]==0xFFFF0000&&keyed.Argb[2]==0xFF0000FF,"flags≠0: key pixels transparent");
            var opaque=UiImage.Decode(Img(2,1,0,0x324C,0x324C,0x7C00));
            Check(opaque.Argb[0]==UiImage.ToArgb(0x324C)&&opaque.Flags==0,"flags=0: key ignored, fully opaque (V4 (a))");
            using(var bmp=keyed.ToBitmap())Check(bmp.PixelFormat==PixelFormat.Format32bppPArgb&&bmp.Width==2&&bmp.Height==2&&bmp.GetPixel(0,0).A==0&&bmp.GetPixel(1,0).ToArgb()==unchecked((int)0xFFFF0000),"32bpp PArgb bitmap");
            Throws<UiResourceException>(()=>UiImage.Decode(Img(2,2,0,0)[..^1]),"truncated .img rejected","8+寬×高×2");
            Throws<UiResourceException>(()=>UiImage.Decode([..Img(1,1,0,0),0,0]),"oversized .img rejected");
            Throws<UiResourceException>(()=>UiImage.Decode(new byte[7]),"short header rejected","標頭");
            Throws<UiResourceException>(()=>UiImage.Decode(Img(0,0,0,0)),"zero dimension rejected");
        }

        // --- SurfCatalog over synthetic Tile + 17 Sprite archives.
        {
            string dir=Case("surf");
            string surfList="<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<SurfList>\n"+
                "<surf id=\"10\" file=\"10\"/><surf id=\"11\" file=\"500\"/><surf id=\"12\" file=\"12\"/><surf id=\"13\" file=\"13\"/>"+
                "<surf id=\"14\" file=\"14\"/><surf id=\"15\" file=\"15\"/><surf id=\"16\" file=\"16\"/><surf id=\"17\" file=\"17\"/><surf id=\"18\" file=\"18\"/><surf id=\"19\" filedesc=\"desc only\"/>\n</SurfList>";
            var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)));
            var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
            void Put(int index,string name,byte[] data)=>items[index].Add((name,data));
            int H(string n)=>1+IndependentHash(n);
            Check(SurfCatalog.NameHash("3168.img")==13&&SurfCatalog.NameHash("3176.img")==12&&SurfCatalog.NameHash("2273.img")==9&&SurfCatalog.NameHash("3204.img")==4
                &&SurfCatalog.NameHash("4313.img")==6&&SurfCatalog.NameHash("2818.img")==14,"name hash = byte sum % 16 matches the six T448 archive samples");
            Check(SurfCatalog.NameHash("3168c.img")==IndependentHash("3168c.img"),"name hash includes the c suffix");
            Put(H("10c.img"),"10c.img",Img(3,2,0,0,0x7C00));                    // c version in Sprite{h}
            Put(0,"10.img",Img(9,9,0,0));                                       // plain version must lose
            Put(H("500.img"),"500.img",Img(4,1,0,0,0x03E0));                    // id 11 → file 500 (id ≠ file)
            Put(H("11.img"),"11.img",Img(1,1,0,0));                             // decoy: id used as file name would pick this
            Put(0,"13c.img",Img(2,2,0,0));Put(H("13.img"),"13.img",Img(1,1,0,0)); // Sprite.pak c version beats plain in Sprite{h}
            Put(H("14.img"),"14.img",Img(5,1,0,0,0x001F));Put(0,"14.img",Img(6,1,0,0,0x7C00)); // duplicate, different content → Sprite{h} wins
            Put(0,"15.img",Img(1,3,0,0));                                       // only in Sprite.pak
            int wrong=H("16.img")==16?1:H("16.img")+1;Put(wrong,"16.img",Img(1,1,0,0)); // in a SpriteNN that is not h → unreachable
            Put(H("17.img"),"17.img",Img(2,2,0,0)[..^2]);                       // corrupt size
            Put(H("18.img"),"18.img",Img(1,1,0,0));
            var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
            long cost(int w,int h)=>(long)w*h*4;
            using(var res=new UiResources(dir,tilePin,spritePins,capacity:cost(3,2)+cost(4,1))){
                var s=res.Surfs;
                Check(s.Files.Count==9&&s.Files[11]=="500"&&s.IdsWithoutFile.SequenceEqual(new[]{19}),"SurfList parsed (id→file, row without file kept apart)");
                Check(s.Get(19)==null&&UiResourceLog.Recent().Any(l=>l=="surf 19: SurfList.xml 此列沒有 file 屬性"),"row without file → null, id never used as file name");
                var s10=s.Get(10)!;Check(s10.Width==3&&s10.Height==2&&s10.FileName=="10c.img"&&s10.Archive==SpriteName(H("10c.img")),"c version in Sprite{h} preferred");
                var s11=s.Get(11)!;Check(s11.Width==4&&s11.FileName=="500.img","id ≠ file honoured");
                Check(s.Get(12)==null,"missing file → null (no placeholder)");
                Check(UiResourceLog.Recent().Count(l=>l.StartsWith("surf 12:"))==1&&s.Get(12)==null&&UiResourceLog.Recent().Count(l=>l.StartsWith("surf 12:"))==1,"missing surf logged once");
                Check(s.Get(999)==null&&UiResourceLog.Recent().Any(l=>l.StartsWith("surf 999:")),"unknown id → null and logged");
                Check(s.Get(13)!.FileName=="13c.img"&&s.Get(13)!.Archive=="Sprite","Fc.img in Sprite beats F.img in Sprite{h}");
                var s14=s.Get(14)!;Check(s14.Width==5&&s14.Archive==SpriteName(H("14.img")),"duplicate name: Sprite{h} wins over Sprite.pak (V4 (e))");
                Check(s.Get(15)!.Archive=="Sprite","Sprite.pak fallback");
                Check(s.Get(16)==null,"entry in non-hash SpriteNN is unreachable (as in BIN)");
                Throws<UiResourceException>(()=>s.Get(17),"corrupt .img inside archive rejected");
                // LRU: capacity holds two of the decoded sizes above.
                var lru=new SurfCatalog(res.Xml,res.Sprites,cost(3,2)+cost(4,1));
                lru.Get(10);lru.Get(11);Check(lru.IsCached(10)&&lru.IsCached(11)&&lru.CachedBytes==cost(3,2)+cost(4,1),"LRU holds within capacity");
                lru.Get(10);lru.Get(18);Check(lru.IsCached(10)&&!lru.IsCached(11)&&lru.IsCached(18)&&lru.CachedBytes<=lru.Capacity,"LRU evicts least recently used");
                var again=lru.Get(10);Check(ReferenceEquals(again,lru.Get(10)),"cache hit returns same surface");
                Check(SurfCatalog.DefaultCapacity==32L*1024*1024,"default memory cache 32 MiB");
            }
            // T479 anchor: lookup still resolves, but reading pixels is refused when an anchor entry of that container differs.
            var badPins=spritePins.Select((p,i)=>i==H("18.img")?p with{Anchors=[new("18.img",new string('f',64))]}:p).ToArray();
            using(var res=new UiResources(dir,tilePin,badPins))Throws<UiResourceException>(()=>res.Surfs.Get(18),"Sprite anchor mismatch refused (names the entry)","18.img 與支援版本不符");
            using(var res=new UiResources(dir,tilePin with{Anchors=[new("NoSuch.lac",new string('e',64))]},spritePins))Throws<UiResourceException>(()=>res.Surfs.Get(10),"missing Tile anchor refused","缺少錨點 entry NoSuch.lac");
            Throws<ArgumentException>(()=>new UiResources(dir,tilePin,spritePins[..16]),"17 Sprite archives required");
        }
        {
            string dir=Case("surf-dup");
            var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml("<?xml version=\"1.0\"?><SurfList><surf id=\"1\" file=\"1\"/><surf id=\"1\" file=\"2\"/></SurfList>")));
            var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i))).ToArray();
            using var res=new UiResources(dir,tilePin,spritePins);Throws<UiLayoutException>(()=>res.Surfs.Get(1),"duplicate surf id rejected","重複");
        }
        {
            string dir=Case("surf-badfile");
            var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml("<?xml version=\"1.0\"?><SurfList><surf id=\"1\" file=\"..\\\\x\"/></SurfList>")));
            var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i))).ToArray();
            using var res=new UiResources(dir,tilePin,spritePins);Throws<UiLayoutException>(()=>res.Surfs.Get(1),"path-like file name rejected");
        }
        // T479: production containers carry names and the asset-recipe.md 1.2 anchors only (no whole-file SHA or size).
        Check(UiResourcePins.Sprites.Count==17&&Enumerable.Range(0,17).All(i=>UiResourcePins.Sprites[i].Name==SpriteName(i)&&UiResourcePins.Sprites[i].AnchorList.Count==0)
            &&UiResourcePins.Tile.Name=="Tile"&&UiResourcePins.Tile.AnchorList.Select(a=>a.Entry).SequenceEqual(new[]{"Water.lac","Pier1.lac","Pier5.lac"})
            &&UiResourcePins.Tile.AnchorList.All(a=>a.Sha256.Length==64&&a.Sha256.All(Uri.IsHexDigit)),"production containers: 17 Sprite names without whole pins, Tile anchors = asset-recipe.md 1.2");
    }

    // Optional (TW_CLIENT_ROOT set): read the real client read-only; outputs (JSON + PNG) go to scratch/issue-461/real only.
    [CheckGroup("native-ui-resources",Order=2)]
    static void NativeUiRealClientChecks(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui real client checks (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-461","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var xmls=new List<object>();int surfAttrs=0;var surfErrors=new List<string>();
        foreach(var name in res.Xml.XmlNames()){
            var blob=res.Tile.Read(res.Tile.Find(name)!);
            var layout=UiLayout.Parse(UiXmlSource.DecodeText(UiXmlSource.Decrypt(blob),name),name);
            int nodes=0;
            void Walk(UiNode n){nodes++;foreach(var a in new[]{"surfNumber"}){var v=n.Attr(a);if(v!=null){surfAttrs++;try{n.Surfs(a);}catch(UiLayoutException ex){surfErrors.Add(ex.Message);}}}foreach(var c in n.Children)Walk(c);}
            Walk(layout.Root);
            xmls.Add(new{name,root=layout.Root.Name,nodes,bytes=blob.Length,tail=(blob.Length-4)%16});
        }
        Check(xmls.Count==65,"real Tile.pak has 65 XML, all decrypted and parsed (got "+xmls.Count+")");
        var status=res.Xml.Load("uistatus86");Check(status.FileName=="uistatus86-c.xml","real uistatus86 resolves to -c");
        var bookmark=res.Xml.Load("BookmarkUI");Check(bookmark.FileName=="BookmarkUI.xml","real BookmarkUI falls back to plain name");
        var files=res.Surfs.Files;Check(files.Count+res.Surfs.IdsWithoutFile.Count==6653&&res.Surfs.IdsWithoutFile.Count==343,"real SurfList 6653 rows = 6310 with file + 343 without");int differ=files.Count(kv=>kv.Value!=kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var expected=new Dictionary<int,(int W,int H)>{[3168]=(135,125),[3176]=(157,124),[3279]=(362,240)};
        var surfs=new List<object>();
        foreach(var (id,size) in expected){
            var s=res.Surfs.Get(id);
            Check(s!=null,"real surf "+id+" present");
            // Also report the plain F.img (T161 measured that one) to show whether the c version differs in size.
            var plain=res.Surfs.Candidates(files[id]).Where(c=>!c.Name.Contains("c.img")).Select(c=>(c.Archive,Entry:c.Archive.Find(c.Name))).FirstOrDefault(c=>c.Entry!=null);
            UiImage? plainImg=plain.Entry==null?null:UiImage.Decode(plain.Archive.Read(plain.Entry));
            s!.Bitmap.Save(Path.Combine(outDir,$"surf-{id}-{s.FileName}.png"),ImageFormat.Png);
            surfs.Add(new{id,file=files[id],resolved=s.FileName,archive=s.Archive,s.Width,s.Height,flags=s.Flags,key=$"0x{s.TransparentColor:X4}",
                plainArchive=plain.Archive?.Pin.Name,plainWidth=plainImg?.Width,plainHeight=plainImg?.Height,expected=$"{size.W}×{size.H}"});
            Check(s.Width==size.W&&s.Height==size.H,$"real surf {id} size {s.Width}×{s.Height} = ui-layout.md {size.W}×{size.H}");
        }
        File.WriteAllText(Path.Combine(outDir,"real-client.json"),JsonSerializer.Serialize(new{client,xmlCount=xmls.Count,surfListRowsWithFile=files.Count,surfListRowsWithoutFile=res.Surfs.IdsWithoutFile.Count,withoutFileRange=new[]{res.Surfs.IdsWithoutFile.Min(),res.Surfs.IdsWithoutFile.Max()},idNotFile=differ,surfAttrs,surfErrors,surfs,cachedBytes=res.Surfs.CachedBytes,xmls},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        Check(surfErrors.Count==0,"every real surfNumber attribute parses ("+surfAttrs+")");
    }
}
