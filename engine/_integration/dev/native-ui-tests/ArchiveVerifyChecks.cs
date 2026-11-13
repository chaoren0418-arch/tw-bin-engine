using P06.NativeUi;
using System.Text;

// T479 (#1083, docs/design/asset-recipe.md 1.1 point 3, 1.2, §8 D1): runtime idx/pak verification without whole-container pins.
// Every Sprite/Tile container here is synthesized under scratch/issue-1083 (hand-made .img/.spr bytes, nothing from a client).
//   (a) unrelated entries appended to the pak tail + matching idx rows → accepted, the entries read are unchanged;
//   (b) idx size ≠ count × 28 + 4, offset beyond the pak, duplicate name → refused, naming the container;
//   (c) an anchor entry with other bytes → refused, naming the container and the entry.
// Plus sprites.md §3.3 (main container first, alternates must agree) for the ground reader, and the shared rule in PlainArchive.
partial class Checks {
    [CheckGroup("native-ui-resources",Order=3),CheckGroup(CheckGroups.Default,Order=31)]
    static void RuntimeArchiveVerifyChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1083","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");

        // Container bytes: idx rows in the given order, payloads packed back to back.
        (byte[] Idx,byte[] Pak) Pack(params (string Name,byte[] Data)[] items){
            using var pak=new MemoryStream();using var idx=new MemoryStream();idx.Write(BitConverter.GetBytes((uint)items.Length));
            foreach(var (n,d) in items){idx.Write(IdxRecord((uint)pak.Position,Name20(n),(uint)d.Length));pak.Write(d);}
            return (idx.ToArray(),pak.ToArray());
        }
        // The 2026-11-09 pattern (sprites.md §8.1): extra payloads after the old pak tail, extra rows after the old idx rows.
        (byte[] Idx,byte[] Pak) Appended((byte[] Idx,byte[] Pak) old,params (string Name,byte[] Data)[] extra){
            using var pak=new MemoryStream();pak.Write(old.Pak);
            using var idx=new MemoryStream();idx.Write(BitConverter.GetBytes(BitConverter.ToUInt32(old.Idx,0)+(uint)extra.Length));idx.Write(old.Idx.AsSpan(4));
            foreach(var (n,d) in extra){idx.Write(IdxRecord((uint)pak.Position,Name20(n),(uint)d.Length));pak.Write(d);}
            return (idx.ToArray(),pak.ToArray());
        }
        void Write(string dir,string name,(byte[] Idx,byte[] Pak) c){RawArchive(dir,name,c.Idx,c.Pak);}
        void Refused<T>(Action action,string label,params string[] parts) where T:Exception{
            try{action();}
            catch(T ex){Check(parts.All(p=>ex.Message.Contains(p,StringComparison.Ordinal)),$"{label}: message names {string.Join(" / ",parts)} — {ex.Message}");return;}
            catch(Exception ex){throw new Exception($"{label}: expected {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}",ex);}
            throw new Exception(label+": not refused");
        }

        byte[] anchorBytes=Img(3,1,0,0,0x7C00,0x03E0,0x001F),art=Img(2,2,1,0x1234,0x1234,0x7C00,0x03E0,0x001F),spr=Encoding.ASCII.GetBytes("synthetic spr payload 900-0");
        var original=Pack(("anchor.img",anchorBytes),("900-0.spr",spr),("art.img",art));
        string anchorSha=Sha(anchorBytes);
        ClientArchivePin AnchoredPin(string name)=>new(name,[new("ANCHOR.IMG",anchorSha)]);

        // --- (a) appended unrelated entries are accepted; what is read is byte-identical.
        {
            string before=Case("a-before"),after=Case("a-after");
            Write(before,"Sprite03",original);
            var grown=Appended(original,("22130.img",Img(4,4,0,0)),("22131.img",Img(1,2,0,0)));
            Write(after,"Sprite03",grown);
            Check(grown.Idx.Length==original.Idx.Length+2*28&&grown.Pak.AsSpan(0,original.Pak.Length).SequenceEqual(original.Pak),"(a) fixture: old idx rows and pak bytes kept, two rows and payloads appended");
            using var a=new ClientArchive(before,AnchoredPin("Sprite03"));using var b=new ClientArchive(after,AnchoredPin("Sprite03"));
            Check(a.Entries.Count==3&&b.Entries.Count==5&&b.Find("22131.IMG")!=null,"(a) appended rows are listed");
            foreach(string n in new[]{"anchor.img","900-0.spr","art.img"})
                Check(a.Read(a.Find(n)!).AsSpan().SequenceEqual(b.Read(b.Find(n)!))&&b.Find(n)!.Offset==a.Find(n)!.Offset,"(a) entry unchanged after append: "+n);
            Check(b.Read(b.Find("22130.img")!).Length==Img(4,4,0,0).Length,"(a) appended entry itself readable");

            // Same through the UI surf catalog and the ground reader (17 containers, appended one in the surf's hash slot).
            string ui=Case("a-ui");int h=1+SurfCatalog.NameHash("art.img");
            var tile=Pack(("SurfList.xml",Xml("<?xml version=\"1.0\"?><SurfList><surf id=\"7\" file=\"art\"/></SurfList>")),("Water.lac",anchorBytes));
            Write(ui,"Tile",Appended(tile,("ExtraUI-c.xml",Xml("<?xml version=\"1.0\"?><UI/>"))));
            for(int i=0;i<17;i++)Write(ui,SpriteName(i),i==h?Appended(original,("22130.img",Img(4,4,0,0))):i==0?Pack(("main.img",Img(1,1,0,0))):Pack(($"pad{i}.img",Img(1,1,0,0))));
            var spritePins=Enumerable.Range(0,17).Select(i=>i==h?AnchoredPin(SpriteName(i)):new ClientArchivePin(SpriteName(i))).ToArray();
            using(var res=new UiResources(ui,new ClientArchivePin("Tile",[new("water.LAC",anchorSha)]),spritePins)){
                UiRoot.VerifyResources(res);
                var s=res.Surfs.Get(7);var expected=UiImage.Decode(art);
                Check(s!=null&&s.Archive==SpriteName(h)&&s.Width==2&&s.Height==2&&s.FileName=="art.img","(a) surf served from the appended container");
                Check(UiImage.Decode(res.Sprites[h].Read(res.Sprites[h].Find("art.img")!)).Argb.AsSpan().SequenceEqual(expected.Argb),"(a) decoded surf pixels unchanged");
                Check(res.Xml.Load("ExtraUI").FileName=="ExtraUI-c.xml","(a) Tile with an appended entry and its anchor accepted");
            }
            using(var res=new UiResources(ui,new ClientArchivePin("Tile"),spritePins)){
                var sprites=res.Sprites;
                Check(P06.GroundSpriteReader.ReadPayload(sprites,"900-0.SPR",null) is {} p&&p.AsSpan().SequenceEqual(spr),"(a) ground reader reads the unchanged SPR from the appended container");
            }
        }

        // --- (b) structure faults are refused and name the container (and the entry where there is one).
        {
            void Fault(string tag,(byte[] Idx,byte[] Pak) c,string label,params string[] parts){
                string dir=Case(tag);Write(dir,"Sprite07",c);
                using(var arc=new ClientArchive(dir,new ClientArchivePin("Sprite07")))Refused<UiResourceException>(()=>arc.Find("art.img"),"(b) ClientArchive "+label,["Sprite07",..parts]);
                Refused<InvalidDataException>(()=>P06.PlainArchive.Open(dir,"Sprite07"),"(b) PlainArchive "+label,["Sprite07",..parts]);
                Refused<ClientArchiveFormatException>(()=>ClientArchiveIndex.Parse("Sprite07",c.Idx,c.Pak.Length),"(b) ClientArchiveIndex "+label,["Sprite07",..parts]);
            }
            Fault("idx-short",(original.Idx[..^1],original.Pak),"idx one byte short","筆數×28+4");
            Fault("idx-long",([..original.Idx,0,0,0,0],original.Pak),"idx with a stray tail","筆數×28+4");
            Fault("idx-count",([..BitConverter.GetBytes(4u),..original.Idx.AsSpan(4)],original.Pak),"idx count larger than its rows","筆數×28+4");
            Fault("oob",(original.Idx,original.Pak[..^1]),"last entry beyond the pak","art.img","越界");
            var far=Pack(("art.img",art));BitConverter.GetBytes(0xFFFFFF00u).CopyTo(far.Idx,4);
            Fault("oob-offset",far,"offset far beyond the pak","art.img","越界");
            Fault("dup",Pack(("art.img",art),("ART.IMG",art)),"duplicate name (case-insensitive)","ART.IMG","重名");
            Fault("dup-appended",Appended(original,("Anchor.img",anchorBytes)),"appended row repeating an existing name","Anchor.img","重名");
            var e=CaughtT479(()=>ClientArchiveIndex.Parse("Sprite07",original.Idx,original.Pak.Length-1));
            Check(e is ClientArchiveFormatException{Container:"Sprite07",Entry:"art.img"},"(b) exception carries container and entry");

            // Through the ground reader: InvalidDataException, container named.
            string gdir=Case("ground-oob");for(int i=0;i<17;i++)Write(gdir,SpriteName(i),i==0?(original.Idx,original.Pak[..^1]):Pack(($"pad{i}.img",Img(1,1,0,0))));
            var archives=Enumerable.Range(0,17).Select(i=>new ClientArchive(gdir,new ClientArchivePin(SpriteName(i)))).ToArray();
            try{Refused<InvalidDataException>(()=>P06.GroundSpriteReader.ReadPayload(archives,"900-0.spr",null),"(b) ground reader refuses a broken main container","Sprite:","越界");}
            finally{foreach(var a in archives)a.Dispose();}
        }

        // --- (c) anchor entry with other bytes → refused, naming container and entry; a missing anchor likewise.
        {
            string dir=Case("anchor-changed");
            byte[] changed=(byte[])anchorBytes.Clone();changed[^1]^=0x01;   // same size, one bit different
            Write(dir,"Sprite11",Pack(("anchor.img",changed),("900-0.spr",spr),("art.img",art)));
            using(var arc=new ClientArchive(dir,AnchoredPin("Sprite11"))){
                Check(arc.Find("art.img")!=null,"(c) lookup alone does not read the anchor");
                Refused<UiResourceException>(()=>arc.Read(arc.Find("art.img")!),"(c) changed anchor refuses every read of that container","Sprite11","anchor.img","與支援版本不符");
            }
            using(var arc=new ClientArchive(dir,new ClientArchivePin("Sprite11")))Check(arc.Read(arc.Find("art.img")!).AsSpan().SequenceEqual(art),"(c) the same container without an anchor reads");
            using(var arc=new ClientArchive(dir,new ClientArchivePin("Sprite11",[new("pier9.lac",anchorSha)])))
                Refused<UiResourceException>(()=>arc.Read(arc.Find("art.img")!),"(c) missing anchor entry refused","Sprite11","缺少錨點 entry pier9.lac");
            // UI resources: the anchored Tile refuses the whole native UI (UiRoot.Build → PinMismatchMessage in the bridge).
            string ui=Case("anchor-ui");
            Write(ui,"Tile",Pack(("SurfList.xml",Xml("<?xml version=\"1.0\"?><SurfList><surf id=\"7\" file=\"art\"/></SurfList>")),("Water.lac",changed)));
            for(int i=0;i<17;i++)Write(ui,SpriteName(i),Pack(($"pad{i}.img",Img(1,1,0,0))));
            using(var res=new UiResources(ui,new ClientArchivePin("Tile",[new("Water.lac",anchorSha)]),UiResourcePins.Sprites))
                Refused<UiResourceException>(()=>UiRoot.VerifyResources(res),"(c) Tile anchor mismatch refuses the native UI","Tile","Water.lac");
            // Ground reader: anchored container named in the InvalidDataException.
            string g=Case("anchor-ground");for(int i=0;i<17;i++)Write(g,SpriteName(i),i==0?Pack(("anchor.img",changed),("900-0.spr",spr)):Pack(($"pad{i}.img",Img(1,1,0,0))));
            var archives=Enumerable.Range(0,17).Select(i=>new ClientArchive(g,i==0?AnchoredPin("Sprite"):new ClientArchivePin(SpriteName(i)))).ToArray();
            try{Refused<InvalidDataException>(()=>P06.GroundSpriteReader.ReadPayload(archives,"900-0.spr",null),"(c) ground reader refuses a container whose anchor changed","Sprite","anchor.img");}
            finally{foreach(var a in archives)a.Dispose();}
        }

        // --- sprites.md §3.3: main container first; alternates must be byte-identical; pinned rows only from the main container.
        {
            byte[] other=Encoding.ASCII.GetBytes("different synthetic spr");
            ClientArchive[] Open(string tag,Func<int,(string,byte[])[]> items){
                string dir=Case(tag);for(int i=0;i<17;i++)Write(dir,SpriteName(i),Pack([..items(i),($"pad{i}.img",Img(1,1,0,0))]));
                return Enumerable.Range(0,17).Select(i=>new ClientArchive(dir,new ClientArchivePin(SpriteName(i)))).ToArray();
            }
            void With(ClientArchive[] archives,Action<ClientArchive[]> body){try{body(archives);}finally{foreach(var a in archives)a.Dispose();}}
            With(Open("main-wins",i=>i==0?[("900-0.spr",spr)]:i is 3 or 9?[("900-0.spr",other)]:[]),a=>
                Check(P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",null) is {} p&&p.AsSpan().SequenceEqual(spr),"§3.3 main container wins; differing alternates are not looked at"));
            With(Open("alt-same",i=>i is 2 or 5 or 16?[("900-0.spr",spr)]:[]),a=>
                Check(P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",null) is {} p&&p.AsSpan().SequenceEqual(spr),"§3.3 identical copies in several alternates accepted"));
            With(Open("alt-differ",i=>i==2?[("900-0.spr",spr)]:i==5?[("900-0.spr",other)]:[]),a=>
                Refused<InvalidDataException>(()=>P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",null),"§3.3 different alternates refused (never one picked)","Sprite01","Sprite04","900-0.spr"));
            With(Open("none",_=>[]),a=>Check(P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",null)==null,"§3.3 no container has it → null (caller reports the gap)"));
            var pin=new P06.GroundSprite("900-0.spr",0,0,spr.Length,Sha(spr),0,0,[0,0,0,0],new string('0',64),"synthetic");
            With(Open("pinned-main",i=>i==0?[("900-0.spr",spr)]:[]),a=>
                Check(P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",pin) is {} p&&p.AsSpan().SequenceEqual(spr),"pinned row read from the main container"));
            With(Open("pinned-moved",i=>i==0?[("x.spr",other),("900-0.spr",spr)]:[]),a=>
                Refused<InvalidDataException>(()=>P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",pin),"pinned row with another record/offset refused","Sprite/900-0.spr","entry pin 不符"));
            With(Open("pinned-alt",i=>i==4?[("900-0.spr",spr)]:[]),a=>
                Refused<InvalidDataException>(()=>P06.GroundSpriteReader.ReadPayload(a,"900-0.spr",pin),"pinned row found only in an alternate refused","不在主包","Sprite03"));
            Check(SpriteContainerRule.Choose(0,_=>throw new Exception("no read"),(_,_)=>new Exception())==null,"rule with no container → null");
        }
    }

    static Exception? CaughtT479(Action action){try{action();return null;}catch(Exception ex){return ex;}}
}
