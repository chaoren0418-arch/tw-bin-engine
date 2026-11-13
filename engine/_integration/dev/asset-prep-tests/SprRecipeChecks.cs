using P06;
using P06.AssetPrep;
using System.Text;
using System.Text.Json;

// T454（#1039）：SPR 共用層與 character、objects 兩個 recipe 的離線測試。list.spz、Text、17 個 Sprite 容器與每一個 .spr
// 都由本檔合成（像素是依編號算出的測試值，不是任何原客戶端的圖），寫在 scratch/issue-1039/run-<時間>/ 下；不讀客戶端、
// 不需要遊戲、網路或 DB。每次執行用新的 run 目錄，測試本身不做遞迴刪除。
// 群組 asset-prep-spr；同時掛在 CI 已在跑的 native-ui-resources 之下（與 asset-prep、asset-prep-text 相同做法，不改 build.yml）。
partial class Checks {
    sealed record SrFrame(int[] Bounds,(int A,int B,int Z,int Block)[] Tiles,bool Empty=false);
    sealed record SrSpr(SrFrame[] Frames,(int X,int Y,ushort C)[][] Blocks,bool Indexed=false);

    /// <summary>Test-side SPR encoder (sprites.md §3.4 layout). Block pixels are emitted row by row, left to right.</summary>
    static byte[] SrEncode(SrSpr s){
        var o=new List<byte>();
        void U16(int v){o.Add((byte)v);o.Add((byte)(v>>8));}
        void U32(uint v){o.AddRange(BitConverter.GetBytes(v));}
        ushort[] palette=s.Blocks.SelectMany(b=>b.Select(p=>p.C)).Distinct().Order().ToArray();
        if(s.Indexed){o.Add(255);o.Add((byte)(palette.Length==256?0:palette.Length));foreach(ushort c in palette)U16(c);}
        o.Add((byte)s.Frames.Length);
        foreach(var f in s.Frames){
            foreach(int v in f.Bounds)U16(v);
            U16(f.Empty?65535:0);U16(0);U16(f.Tiles.Length);
            foreach(var t in f.Tiles){o.Add((byte)(sbyte)t.A);o.Add((byte)(sbyte)t.B);o.Add((byte)t.Z);U16(t.Block);}
        }
        U32((uint)s.Blocks.Length);
        var data=new List<byte>();var offsets=new List<uint>();long expanded=0;
        foreach(var block in s.Blocks){
            offsets.Add((uint)data.Count);
            int sx=block.Min(p=>p.X),sy=block.Min(p=>p.Y),w=block.Max(p=>p.X)-sx+1,lines=block.Max(p=>p.Y)-sy+1;
            data.AddRange(new[]{(byte)sx,(byte)sy,(byte)w,(byte)lines});expanded+=4;
            for(int y=sy;y<sy+lines;y++){
                var row=block.Where(p=>p.Y==y).OrderBy(p=>p.X).ToArray();
                var runs=new List<List<(int X,int Y,ushort C)>>();
                foreach(var p in row){if(runs.Count>0&&runs[^1][^1].X==p.X-1)runs[^1].Add(p);else runs.Add([p]);}
                data.Add((byte)runs.Count);expanded++;int x=sx;
                foreach(var run in runs){
                    data.Add((byte)((run[0].X-x)*2));data.Add((byte)run.Count);expanded+=2;
                    foreach(var p in run){if(s.Indexed)data.Add((byte)Array.IndexOf(palette,p.C));else{data.Add((byte)p.C);data.Add((byte)(p.C>>8));}expanded+=2;}
                    x=run[^1].X+1;
                }
            }
        }
        foreach(uint v in offsets)U32(v);
        U32((uint)expanded);o.AddRange(data);
        return o.ToArray();
    }
    /// <summary>A synthetic SPR: <paramref name="frames"/> frames, two tiles each (one per block); frame <paramref name="emptyAt"/> is an empty frame with non-standard inverted bounds.</summary>
    static SrSpr SrMake(int sprite,int sid,int frames,int emptyAt=-1,bool indexed=false){
        var blocks=new List<(int,int,ushort)[]>();var list=new List<SrFrame>();
        int seed=sprite*131+sid*17;
        for(int f=0;f<frames;f++){
            if(f==emptyAt){list.Add(new([352,240,-288,-240],[],true));continue;}
            var tiles=new List<(int,int,int,int)>();
            for(int t=0;t<2;t++){
                int s=seed+f*7+t*3;
                (int,int,ushort)[] px=[(s%20,s%11,(ushort)(s*97&0x7FFF)),(s%20+1,s%11,0),(s%20+3,s%11,(ushort)(s*31&0x7FFF)),(s%7,s%11+2,(ushort)((s+5)*13&0x7FFF))];
                tiles.Add((t-1+f%3,f%2-t,s%40,blocks.Count));blocks.Add(px);
            }
            list.Add(new([-10-f,-20-sid%5,15+f,4+sid%3],tiles.ToArray()));
        }
        return new(list.ToArray(),blocks.ToArray(),indexed);
    }

    /// <summary>Synthetic client: 17 Sprite containers (index 0 = Sprite) and Text with list.spz. Returns the anchor pins of that list.spz.</summary>
    static AssetPrepPins SrClient(string dir,string listText,IEnumerable<(int Archive,string Name,byte[] Data)> files,params (string Name,byte[] Data)[] text){
        var groups=files.GroupBy(f=>f.Archive).ToDictionary(g=>g.Key,g=>g.Select(f=>(f.Name,f.Data)).ToList());
        for(int i=0;i<17;i++){var items=groups.GetValueOrDefault(i)??[];items.Add(($"pad{i}.img",Img(1,1,0,0)));Archive(dir,SpriteName(i),items.ToArray());}
        byte[] plain=Encoding.ASCII.GetBytes(listText);
        TxArchive(dir,[("list.spr",Encoding.ASCII.GetBytes("#0\t1\r\n")),("list.spz",SpriteList.EncodeSpz(plain)),..text]);
        return new AssetPrepPins(Sha(plain));
    }

    [CheckGroup("asset-prep-spr",Order=1),CheckGroup("native-ui-resources",Order=62),CheckGroup(CheckGroups.Default)]
    static void SprRecipeChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1039","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        string Active(string engine,string id)=>Path.Combine(engine,"assets",id);
        string Staging(string engine,string id)=>Path.Combine(engine,"runtime","assets-staging",id);

        // --- 0. 自動發現。
        var product=AssetRecipeDiscovery.Product;
        Check(product.Errors.Count==0&&product.Recipes.Count(r=>r is CharacterRecipe{Id:"character",Coverage:AssetRecipeCoverage.Complete})==1&&product.Recipes.Count(r=>r is ObjectsRecipe{Id:"objects",Coverage:AssetRecipeCoverage.Partial})==1,"SR character and objects recipes discovered once");
        Check(AssetPrepRunner.Product().RecipeIds.Contains("character")&&AssetPrepRunner.Product().RecipeIds.Contains("objects"),"SR product runner knows both recipes");

        // --- 合成的動畫表。gfx 0 的 breath 有 "8.5:4"、怪物 50 的 breath 有 "8.0:6"：找動作 8 時不可以從這些 token 開始配（sprites.md §8.3）。
        const string list=
            "S1223\r\n"+
            "#0\t96=3225 prince 0.walk(1 4,0.0:4<479 0.1:4 0.2:4<478 0.3:4) 3.breath(1 2,8.5:4 8.0:4) 1.attack(1 2,16.0:3 16.1:3!) 2.damage(1 1,24.0:2)"+
            " 8.death(0 2,32.0:5 32.1:7) 18.spell dir(1 2,40.0:2 40.1:3!) 19.spell nodir(1 3,48.0:2 48.1:4! 48.2:1)"+
            " 20.walk bow(1 2,56.0:4 56.1:4) 21.attack bow(1 3,64.0:5 64.1:5! 64.2:5) 22.damage bow(1 1,72.0:3) 23.breath bow(1 2,80.0:4 80.1:4) 101.shadow(3226)\r\n"+
            "#50\t3=51 orc 0.walk(1 2,0.0:4 0.1:4) 3.breath(0 1,8.0:6) 8.fly(0 2,16.0:3 16.1:3)\r\n"+
            "#60\t1 conflict 3.breath(0 1,0.0:4)\r\n"+
            "#61\t1 nofile 3.breath(0 1,0.0:4)\r\n"+
            "#62\t1 corrupt 3.breath(0 1,0.0:4)\r\n"+
            "#63\t1 beyond 3.breath(0 1,0.9:4)\r\n"+
            "#66\t8 arrow 0.fly(1 3,0.2:1 0.0:1 0.1:1) 102.type(9) 109.effect(3 -1)\r\n"+
            "#70\t1 sign 70.act shop(1 1,0.0:4)\r\n"+
            "#167\t4 bolt 0.fly(1 2,0.0:3 0.1:3)\r\n"+
            "#189\t1 burst 0.effect(0 3,0.0:3 0.1:3[189 0.2:3!)\r\n"+
            "#234\t3=200 tree 0.walk(1 1,0.0:4) 3.breath(0 2,0.0:24 0.1:24) 101.shadow(225) 105.clothes(2 201 204)\r\n";
        var sprs=new Dictionary<string,SrSpr>();
        var files=new List<(int,string,byte[])>();
        void Put(int archive,int sprite,int sid,SrSpr spr){string name=$"{sprite}-{sid}.spr";sprs[name]=spr;files.Add((archive,name,SrEncode(spr)));}
        foreach(int sid in Enumerable.Range(0,88).Where(s=>s<33||s>=40)){
            if(sid==40)continue;                                           // 3225-40 only in an alternate (below)
            Put(0,3225,sid,SrMake(3225,sid,6));
        }
        Put(5,3225,40,SrMake(3225,40,6));
        files.Add((3,"3225-1.spr",SrEncode(SrMake(9999,1,6))));             // main has it: this different alternate is never looked at
        for(int sid=0;sid<8;sid++)Put(1+sid%2,51,sid,SrMake(51,sid,2));
        Put(2,51,8,SrMake(51,8,1));files.Add((7,"51-8.spr",SrEncode(sprs["51-8.spr"])));      // identical copies in two alternates: accepted
        Put(1,51,16,SrMake(51,16,3,emptyAt:2));                           // non-standard empty frame (sprites.md §8.2)
        files.Add((4,"60-0.spr",SrEncode(SrMake(60,0,1))));files.Add((9,"60-0.spr",SrEncode(SrMake(60,1,1))));   // two different alternates
        files.Add((0,"62-0.spr",[..SrEncode(SrMake(62,0,1)),0]));          // trailing byte
        Put(0,63,0,SrMake(63,0,2));
        for(int sid=0;sid<8;sid++)Put(0,66,sid,SrMake(66,sid,3));
        for(int sid=0;sid<4;sid++)Put(0,167,sid,SrMake(167,sid,2));
        Put(0,189,0,SrMake(189,0,3,indexed:true));
        foreach(int sprite in new[]{200,201,204})Put(sprite==201?6:0,sprite,0,SrMake(sprite,0,2));

        string client=Case("client");var pins=SrClient(client,list,files);
        string clientBefore=ApTreeHash(client);
        var runner=AssetPrepRunner.Product();
        AssetPrepRequest Request(string recipe,string? c,string engine,params string[] items)=>new(c,engine,recipe,items){Pins=pins};

        // --- 1. 共用層：list.spz、entry、動作、欄位、SPR 解碼。
        {
            var table=SpriteList.Parse(SpriteList.DecodeSpz(SpriteList.EncodeSpz(Encoding.ASCII.GetBytes(list))));
            Check(table.Count==11&&table.Sha256==pins.ListSpzSha256,"SR list.spz: 11 entries, SHA-256 of the decoded table = anchor");
            var e0=table.Require(0);var e50=table.Require(50);var e234=table.Require(234);
            Check(e0.Sprite==3225&&e50.Sprite==51&&table.Require(66).Sprite==66&&e234.Sprite==200,"SR gfx → sprite: '=' number, otherwise the gfx itself");
            var die=e50.Action(8)!;
            Check(die is {Name:"fly",Directional:false,Count:2}&&die.Steps.Select(s=>(s.Base,s.Frame,s.Ticks)).SequenceEqual(new[]{(16,0,3),(16,1,3)}),"SR action 8 found by id, not from the '8.0:6' token of breath");
            var breath=e0.Action(3)!;
            Check(breath.Steps[0] is {Base:8,Frame:5,Ticks:4,Events:""}&&e0.Action(0)!.Steps[0].Events=="<479"&&table.Require(189).Action(0)!.Steps[1].Events=="[189"&&table.Require(189).Action(0)!.Steps[2].Events=="!","SR step tokens: base, frame, ticks and the events tail copied as is");
            Check(e0.Action(18)!.Name=="spell dir"&&e0.Action(4)==null&&e0.Field(101)!.SequenceEqual(new[]{3226})&&e234.Field(105)!.SequenceEqual(new[]{2,201,204})&&table.Find(9999)==null,"SR names with spaces, absent actions, numeric fields, absent gfx");
            Check(SprTiming.Times40(e0.Action(0)!)==640&&SprTiming.Floor(e0.Action(19)!)==83+166+41&&Math.Abs(SprTiming.Exact(e0.Action(2)!)-2000d/24)<1e-9&&SprTiming.Release(e0.Action(19)!)==83&&SprTiming.Release(e0.Action(0)!)==null,"SR timing: ×40, floor, exact and release point");
            Throws<AssetPrepException>(()=>SpriteList.Parse(Encoding.ASCII.GetBytes("S1223\n#5 1 a 0.x(0 1,0.0:1)\n#5 1 b 0.x(0 1,0.0:1)\n")),"SR one gfx with two entries refused","兩筆");
            Throws<AssetPrepException>(()=>SpriteList.DecodeSpz(Encoding.ASCII.GetBytes("XXXXX0123456789abcdef")),"SR list.spz with an unknown header refused","S1223");
            Throws<AssetPrepException>(()=>SpriteList.Parse(Encoding.ASCII.GetBytes("S1223\n#7 1 a 0.x(2 1,0.0:1)\n")).Require(7).Action(0),"SR malformed action body refused","格式不支援");

            foreach(var (name,expected) in sprs){
                var file=SprFile.Decode(SrEncode(expected),name);
                bool same=file.Frames.Count==expected.Frames.Length&&file.Blocks.Count==expected.Blocks.Length;
                for(int f=0;same&&f<expected.Frames.Length;f++){
                    var a=file.Frames[f];var b=expected.Frames[f];
                    same&=a.Empty==b.Empty&&a.Bounds.SequenceEqual(b.Bounds)&&a.Tiles.Select(t=>(t.A,t.B,t.Z,t.BlockIndex)).SequenceEqual(b.Tiles);
                }
                for(int k=0;same&&k<expected.Blocks.Length;k++)same&=file.Blocks[k].Select(p=>(p.X,p.Y,p.Color)).SequenceEqual(expected.Blocks[k].OrderBy(p=>p.Y).ThenBy(p=>p.X));
                Check(same,"SR decode "+name+(expected.Indexed?" (indexed palette)":"")+": frames, bounds, tiles and RGB555 pixels");
            }
            Check(SprFile.Decode(SrEncode(sprs["51-16.spr"]),"51-16.spr").Frames[2] is {Empty:true,Tiles.Count:0} empty&&empty.Bounds.SequenceEqual(new[]{352,240,-288,-240}),"SR empty frame with non-standard inverted bounds accepted, bounds kept");
            byte[] good=SrEncode(sprs["63-0.spr"]);
            Throws<AssetPrepException>(()=>SprFile.Decode([..good,0],"x"),"SR SPR with trailing bytes refused","多餘");
            Throws<AssetPrepException>(()=>SprFile.Decode(good[..^3],"x"),"SR truncated SPR refused","截斷");
            Check(SprFile.Origin(0,0)==(0,0)&&SprFile.Origin(1,0)==(24,0)&&SprFile.Origin(-1,0)==(0,12)&&SprFile.Origin(2,3)==(96,24),"SR tile origin x = 24·(b + a − ⌊a/2⌋), y = 12·(b − ⌊a/2⌋)");
        }

        // --- 2. 正常產生：兩個目錄逐 byte 相同；既有讀取程式（Assets.LoadCharacter、ObjectAppearance、ProjectileRendering 的讀法）載入得了。
        string[] objectItems=["manifest","world-234","projectile-66","magic-189","magic-167","50","0-weapon-20","0"];
        string pair=Case("two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        foreach(string engine in new[]{engineA,engineB}){
            Check(runner.Run(Request("character",client,engine,"character")).Outcome==AssetPrepOutcome.Published,"SR character published in "+Path.GetFileName(engine));
            var o=runner.Run(Request("objects",client,engine,engine==engineA?objectItems:objectItems.Reverse().ToArray()));
            Check(o.Outcome==AssetPrepOutcome.Published&&o.Produced.SequenceEqual(new[]{"0","0-weapon-20","50","projectile-66","magic-167","magic-189","world-234","manifest"}),"SR objects items in canonical order: "+string.Join(",",o.Produced));
        }
        foreach(string id in new[]{"character","objects"})
            Check(ApTreeHash(Active(engineA,id))==ApTreeHash(Active(engineB,id)),"SR "+id+": outputs and receipt byte-identical in two directories");
        string charRoot=Active(engineA,"character"),objRoot=Path.Combine(Active(engineA,"objects"),"objects");
        Check(ApNames(charRoot).SequenceEqual(new[]{"character",AssetReceipt.FileName})&&ApNames(Path.Combine(charRoot,"character")).SequenceEqual(new[]{"animation.json","blocks.json"}),"SR character writes exactly blocks.json and animation.json (no PNG, no preview)");
        Check(ApNames(objRoot).SequenceEqual(new[]{"0-weapon-20.json","0.json","50.json","magic-167.json","magic-189.json","manifest.json","projectile-66.json","world-234.json"}),"SR objects writes one file per item: "+string.Join(",",ApNames(objRoot)));
        foreach(string file in Directory.GetFiles(Active(engineA,"objects"),"*.json",SearchOption.AllDirectories).Concat(Directory.GetFiles(charRoot,"*.json",SearchOption.AllDirectories))){
            string text=File.ReadAllText(file);
            Check(!text.Contains(":\\",StringComparison.Ordinal)&&!text.Contains("scratch",StringComparison.OrdinalIgnoreCase)&&!text.Contains("source_offset",StringComparison.Ordinal)&&!text.Contains("block_index",StringComparison.Ordinal)&&!text.Contains("sourceOffset",StringComparison.Ordinal),"SR "+Path.GetFileName(file)+": no path, no provenance-only field");
        }

        // character：產品的載入程式（96 幀、16 筆序列）。
        var frames=new Dictionary<string,Frame>();var sequences=new Dictionary<(string,int),string[]>();
        Assets.LoadCharacter(charRoot,frames,sequences);
        Check(frames.Count==96&&sequences.Count==16&&sequences[("walk",3)].SequenceEqual(new[]{"3:0","3:1","3:2","3:3"})&&sequences[("breath",6)].SequenceEqual(new[]{"14:5","14:0"}),"SR character loads through Assets.LoadCharacter: 96 frames, 16 sequences, keys sid:frame");
        bool FrameMatches(Frame frame,SrSpr spr,int f){
            var e=spr.Frames[f];
            return frame.Bounds.SequenceEqual(e.Bounds)&&frame.Blocks.Length==e.Tiles.Length&&frame.Blocks.Select((b,i)=>b.A==e.Tiles[i].A&&b.B==e.Tiles[i].B&&b.Z==e.Tiles[i].Z&&b.Order==i
                &&b.Tile.Pixels.Select(p=>(p.X,p.Y,p.Color)).SequenceEqual(spr.Blocks[e.Tiles[i].Block].OrderBy(p=>p.Y).ThenBy(p=>p.X))).All(x=>x);
        }
        Check(frames.All(kv=>{var k=kv.Key.Split(':');return FrameMatches(kv.Value,sprs[$"3225-{k[0]}.spr"],int.Parse(k[1]));}),"SR character frames: bounds, a/b/z/source_order and every pixel equal to the synthetic SPR");
        using(var anim=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(charRoot,"character","animation.json")))){
            var walk0=anim.RootElement.GetProperty("mapping")[0];
            Check(walk0.GetProperty("sequence")[0].GetProperty("events").EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(new[]{479})&&walk0.GetProperty("sequence")[0].GetProperty("base_duration_ms").GetInt32()==166
                &&anim.RootElement.GetProperty("character").GetProperty("sprite_id").GetInt32()==3225&&anim.RootElement.GetProperty("metadata").GetProperty("spz_sha256").GetString()==pins.ListSpzSha256,"SR animation.json: sound events, base duration, sprite and the list.spz SHA-256");
        }
        var blocks=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(charRoot,"character","blocks.json"))).RootElement.GetProperty("frames")[0];
        Check(blocks.GetProperty("anchor").EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(new[]{10,20})&&blocks.GetProperty("source_sha256").GetString()==Sha(SrEncode(sprs["3225-0.spr"])),"SR blocks.json: anchor = [-left,-top], source_sha256 = the SPR file's SHA-256");

        // objects：ObjectAppearance（ObjectAssets、PlayerProfiles、MagicRendering、MapEffects 共用的讀法）。
        ObjectAppearance Load(string name)=>new(Path.Combine(objRoot,name+".json"));
        JsonElement Raw(string name)=>Json.Read(Path.Combine(objRoot,name+".json"));
        var p0=Load("0");var p20=Load("0-weapon-20");
        string[] seven=["walk","breath","attack","damage","die","spellDir","spellNoDir"];
        Check(seven.All(a=>Enumerable.Range(0,8).All(d=>p0.Supports(a,d)&&p20.Supports(a,d)))&&p0.Gfx==0&&p0.Sprite==3225&&p0.StepMs==640&&p20.StepMs==320,"SR player profiles: seven actions × 8 directions, stepMs = walk ×40");
        Check(p0.ActionMs["walk"]==640&&p0.ActionMs["attack"]==240&&Math.Abs(p0.ActionMs["damage"]-2000d/24)<1e-9&&Math.Abs(p0.ActionMs["breath"]-8000d/24)<1e-9&&p0.ActionMs["die"]==500&&p0.ActionMs["spellDir"]==83+125&&p0.ActionMs["spellNoDir"]==83+166+41,"SR objects/0.json actionMs: walk/attack ×40, damage/breath/die exact, spells floor");
        Check(p20.ActionMs["walk"]==320&&p20.ActionMs["attack"]==624&&p20.ActionMs["damage"]==125&&p20.ActionMs["breath"]==332&&p20.ActionMs["die"]==208+291,"SR bow stance (weapon 20): attack/damage/breath/die floor");
        Check(p0.Sequences[("spellDir",2)].All(s=>s.Milliseconds!=null)&&p0.Sequences[("walk",2)].All(s=>s.Milliseconds==null)&&p0.Sequences[("attack",0)].All(s=>s.Milliseconds==null)&&p20.Sequences[("attack",0)].All(s=>s.Milliseconds!=null),"SR step milliseconds exactly where the action length is the floor sum");
        Check(p0.Sequences[("die",5)].Select(s=>s.Key).SequenceEqual(new[]{"32:0","32:1"})&&p0.Sequences[("attack",5)].Select(s=>s.Key).SequenceEqual(new[]{"21:0","21:1"})&&p0.Sequences[("walk",0)][0].Events=="<479","SR non-directional die keeps its base file; directional actions add the direction");
        var raw0=Raw("0");var raw20=Raw("0-weapon-20");
        Check(raw0.GetProperty("castReleaseMs").GetProperty("spellDir").GetInt32()==83&&raw0.GetProperty("castReleaseMs").GetProperty("spellNoDir").GetInt32()==83&&!raw0.TryGetProperty("projectileReleaseMs",out _)
            &&raw20.I("serverAttackMs")==600&&raw20.I("projectileReleaseMs")==208&&raw0.S("entryHash")==raw20.S("entryHash")&&raw0.GetProperty("actionMs").GetProperty("attack").GetInt32()==240,"SR castReleaseMs, bow serverAttackMs / projectileReleaseMs, entryHash, integer attack length (Combat.cs reads it as int)");
        Check(p0.Frames.Count==49*6&&p0.Frames.All(kv=>{var k=kv.Key.Split(':');return FrameMatches(kv.Value,sprs[$"3225-{k[0]}.spr"],int.Parse(k[1]));}),"SR objects/0.json frames: every frame of every file used (incl. 3225-40 from the alternate), equal to the synthetic SPR");
        var m50=Load("50");
        Check(m50.Sprite==51&&m50.StepMs==320&&m50.Sequences.Keys.Select(k=>k.Item1).Distinct().Order().SequenceEqual(new[]{"breath","die","walk"})&&m50.ActionMs["walk"]==320&&m50.ActionMs["breath"]==250&&m50.ActionMs["die"]==250,"SR monster: only the creature actions the entry has; action 8 labelled die; walk ×40, breath/die exact");
        Check(m50.Frames.Count==8*2+1+3&&m50.Frames["16:2"].Blocks.Length==0&&m50.Frames["16:2"].Bounds.SequenceEqual(new[]{352,240,-288,-240})&&m50.Sequences[("breath",4)].Single().Key=="8:0","SR monster frames from the alternates, empty frame kept with its bounds");
        var bolt=Load("magic-167");var burst=Load("magic-189");
        Check(bolt.StepMs==1&&bolt.Duration("fly")==250&&bolt.Sequences[("fly",5)].Select(s=>s.Key).SequenceEqual(new[]{"1:0","1:1"})&&bolt.Sequences[("fly",3)][0].Key=="3:0"&&bolt.Frames.Count==8,"SR magic-167: label fly, floor length, direction d → file base + (d mod 4)");
        Check(burst.Duration("effect")==375&&burst.Sequences.Count==8&&burst.Sequences[("effect",7)].Select(s=>s.Events).SequenceEqual(new[]{"","[189","!"})&&burst.Sequences[("effect",0)].All(s=>s.Milliseconds==125)&&Raw("magic-189").I("effectTileType")==18&&!Raw("magic-167").TryGetProperty("effectTileType",out _),"SR magic-189: effect label, events, milliseconds, additive effectTileType 18 only for the listed gfx");
        Check(burst.Frames.Values.First().Blocks.All(b=>b.Tile.Type==18),"SR additive magic blocks load with tile type 18");
        // projectile：ProjectileRendering.cs:200 的讀法（不經 ObjectAppearance）。
        var projectile=Raw("projectile-66");
        var pf=projectile.GetProperty("frames").EnumerateArray().ToDictionary(f=>f.S("key"),f=>new Frame(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(2,b.GetProperty("pixels").EnumerateArray().Select(pix=>{var n=pix.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new Pixel(n[0],n[1],(ushort)n[2]);}).ToArray()))).ToArray()));
        var flySeq=projectile.GetProperty("sequences").EnumerateArray().ToArray();
        Check(pf.Count==24&&flySeq.Length==8&&flySeq[6].GetProperty("sequence").EnumerateArray().Select(s=>s.S("key")).SequenceEqual(new[]{"6:2","6:0","6:1"})&&flySeq.All(s=>s.GetProperty("sequence").EnumerateArray().All(t=>pf.ContainsKey(t.S("key"))))
            &&pf.All(kv=>{var k=kv.Key.Split(':');return FrameMatches(kv.Value,sprs[$"66-{k[0]}.spr"],int.Parse(k[1]));}),"SR projectile-66: 24 frames read the ProjectileRendering way, 8 fly sequences reference them");
        // world-234：body 加 105.clothes 疊成一幀。
        var tree=Load("world-234");
        var treeFrame=tree.Frames["0:1"];var layers=new[]{sprs["200-0.spr"],sprs["201-0.spr"],sprs["204-0.spr"]};
        var union=new[]{layers.Min(l=>l.Frames[1].Bounds[0]),layers.Min(l=>l.Frames[1].Bounds[1]),layers.Max(l=>l.Frames[1].Bounds[2]),layers.Max(l=>l.Frames[1].Bounds[3])};
        var expectedTiles=layers.SelectMany(l=>l.Frames[1].Tiles.Select(t=>(t.A,t.B,t.Z,Pixels:l.Blocks[t.Block].OrderBy(p=>p.Y).ThenBy(p=>p.X).ToArray()))).ToArray();
        Check(tree.Sprite==200&&tree.StepMs==160&&tree.Duration("breath")==2000&&tree.Frames.Count==2&&tree.Sequences.Keys.All(k=>k.Item1=="breath")&&treeFrame.Bounds.SequenceEqual(union)&&treeFrame.Blocks.Length==6
            &&treeFrame.Blocks.Select((b,i)=>b.Order==i&&b.A==expectedTiles[i].A&&b.B==expectedTiles[i].B&&b.Z==expectedTiles[i].Z&&b.Tile.Pixels.Select(p=>(p.X,p.Y,p.Color)).SequenceEqual(expectedTiles[i].Pixels)).All(x=>x),"SR world-234: body then clothes 201, 204 blocks, union bounds, source_order renumbered, breath only, stepMs from the entry's walk");
        Check(Raw("manifest").A("gfx").SequenceEqual(new[]{0,50}),"SR objects/manifest.json lists the plain gfx items of the request");

        // 收據：每個用到的 entry（list.spz 與每個 .spr）都有容器、名稱、大小、SHA-256。
        var receipt=AssetReceipt.TryLoad(Path.Combine(Active(engineA,"objects"),AssetReceipt.FileName))??throw new Exception("SR objects receipt does not load");
        var srcs50=receipt.Completed.Single(c=>c.Item=="50").Sources;
        Check(srcs50.Any(s=>s is {Container:"Text",Entry:"list.spz"}&&s.Sha256==pins.ListSpzSha256)&&srcs50.Where(s=>s.Container!="Text").Select(s=>s.Entry).Distinct().Count()==10,"SR receipt: list.spz with the decoded-table SHA-256, plus each SPR entry read");
        Check(srcs50.Count(s=>s.Entry=="51-8.spr")==2&&srcs50.Where(s=>s.Entry=="51-8.spr").Select(s=>s.Container).Order().SequenceEqual(new[]{"Sprite01","Sprite06"}),"SR receipt records both identical alternates of 51-8.spr");
        var srcs0=receipt.Completed.Single(c=>c.Item=="0").Sources;
        Check(srcs0.Single(s=>s.Entry=="3225-1.spr").Container=="Sprite"&&srcs0.Single(s=>s.Entry=="3225-40.spr").Container=="Sprite04","SR main container first: the different alternate of 3225-1.spr is not read; 3225-40.spr from its only alternate");

        // --- 3. 拒絕：替代包衝突、缺檔、壞檔、超出幀數、缺動作、錨點不符、list.spz 缺少；都不發布。
        {
            string engine=Case("reject-engine");
            AssetPrepException Rejected(string label,AssetPrepFailure failure,string? container,AssetPrepPins p,string? c,params string[] items){
                var ex=ApRefused(()=>runner.Run(new AssetPrepRequest(c??client,engine,"objects",items){Pins=p}),failure,label,container);
                Check(!Directory.Exists(Active(engine,"objects")),label+": nothing published");
                return ex;
            }
            var conflict=Rejected("SR alternates with different content",AssetPrepFailure.Unavailable,"Sprite08",pins,null,"60");
            Check(conflict.Message.Contains("60-0.spr",StringComparison.Ordinal)&&conflict.Message.Contains("Sprite03",StringComparison.Ordinal),"SR alternate conflict names the file and both containers: "+conflict.Message);
            Rejected("SR SPR in no container",AssetPrepFailure.MissingEntry,null,pins,null,"61");
            Rejected("SR SPR with trailing bytes",AssetPrepFailure.CorruptEntry,null,pins,null,"62");
            Rejected("SR step past the file's frames",AssetPrepFailure.CorruptEntry,null,pins,null,"63");
            Rejected("SR weapon stance without its actions",AssetPrepFailure.Unavailable,null,pins,null,"0-weapon-4");
            Rejected("SR gfx without creature actions",AssetPrepFailure.Unavailable,null,pins,null,"70");
            Rejected("SR gfx not in list.spz",AssetPrepFailure.Unavailable,null,pins,null,"9999");
            var anchor=Rejected("SR list.spz of another version (product anchor vs synthetic table)",AssetPrepFailure.ContainerMismatch,"Text",AssetPrepPins.Tw1608262200,null,"50");
            Check(anchor.Message.Contains("list.spz",StringComparison.Ordinal),"SR anchor refusal names list.spz: "+anchor.Message);
            ApRefused(()=>runner.Run(new AssetPrepRequest(client,engine,"character",["character"]){Pins=AssetPrepPins.Tw1608262200}),AssetPrepFailure.ContainerMismatch,"SR character with another list.spz version","Text");
            string noList=Case("no-list");SrClient(noList,list,files);TxArchive(noList,("list.spr",Encoding.ASCII.GetBytes("#0\t1\r\n")));
            Rejected("SR Text without list.spz (no fallback to list.spr)",AssetPrepFailure.MissingEntry,"Text",pins,noList,"50");
            foreach(string item in new[]{"","01","-1","65536","0-weapon-0","0-weapon-256","0-weapon-","magic-","world-01","shop-5","../0","0.json","Manifest"})
                Rejected("SR objects item \""+item+"\"",AssetPrepFailure.InvalidRequest,null,pins,null,item);
            ApRefused(()=>runner.Run(new AssetPrepRequest(client,engine,"character",["0"]){Pins=pins}),AssetPrepFailure.InvalidRequest,"SR character item other than \"character\"");
        }

        // --- 4. 續跑：list.spz 與 .spr 來源在續跑時驗得過（沿用），manifest 只在請求相同時沿用。
        {
            string engine=Case("resume-engine");int done=0;
            Throws<InvalidOperationException>(()=>runner.Run(Request("objects",client,engine,"50","magic-189","manifest") with{ItemCompleted=_=>{if(++done==3)throw new InvalidOperationException("synthetic interrupt");}}),"SR objects interrupted after the manifest","synthetic interrupt");
            Check(AssetReceipt.TryLoad(Path.Combine(Staging(engine,"objects"),AssetReceipt.FileName)) is {Status:AssetReceipt.InProgress,Completed.Count:3},"SR interrupted staging holds three finished items");
            var resumed=runner.Run(Request("objects",client,engine,"0","50","manifest"));
            Check(resumed.Outcome==AssetPrepOutcome.Published&&resumed.Reused.SequenceEqual(new[]{"50"})&&resumed.Produced.SequenceEqual(new[]{"0","manifest"}),"SR resume reuses 50 (list.spz and SPR sources verify); the manifest of another request is redone: reused "+string.Join(",",resumed.Reused)+"; produced "+string.Join(",",resumed.Produced));
            Check(Json.Read(Path.Combine(Active(engine,"objects"),"objects","manifest.json")).A("gfx").SequenceEqual(new[]{0,50})&&!File.Exists(Path.Combine(Active(engine,"objects"),"objects","magic-189.json")),"SR resumed manifest lists this request; items not requested are dropped");
            string fresh=Case("resume-fresh");runner.Run(Request("objects",client,fresh,"0","50","manifest"));
            Check(ApTreeHash(Active(engine,"objects"))==ApTreeHash(Active(fresh,"objects")),"SR resumed result is byte-identical to an uninterrupted run");
            Check(runner.Run(Request("objects",client,fresh,"manifest","50","0")).Outcome==AssetPrepOutcome.UpToDate,"SR same request again: up to date");
        }

        // --- 5. 整輪下來原客戶端沒有任何變動。
        Check(ApTreeHash(client)==clientBefore,"SR client directory is byte-identical after every run (read-only input)");
    }
}
