using P06;
using P06.NativeUi;
using System.Drawing.Imaging;
using System.Text.Json;

// ui-buff-icons (Issue #473). Offline: effectlist2.xml, every archive and every .tbt here is synthesized under scratch/issue-473/;
// numbers are the V3 tables (docs/reference/ui-layout.md §4). The real client is only read by NativeUiBuffIconsRealClientChecks
// when TW_CLIENT_ROOT is set (PNG and JSON go to scratch/ only).
partial class Checks {
    const ushort BiBuff=0x7C00,BiOff=0x03E0,BiDebuff=0x001F,BiOther=0x7FE0;
    static int BiArgb(ushort p)=>unchecked((int)(UiImage.ToArgb(p)|0xFF000000));

    /// <summary>Synthetic .tbt: header (x, y, w, h), one full segment per row (skip 0, w pixels).</summary>
    static byte[] Tbt(int w,int h,ushort px,byte ox=0,byte oy=0){
        var b=new List<byte>{ox,oy,(byte)w,(byte)h};
        for(int y=0;y<h;y++){b.Add(1);b.Add(0);b.Add((byte)w);for(int x=0;x<w;x++){b.Add((byte)px);b.Add((byte)(px>>8));}}
        return b.ToArray();
    }
    const string BiEffects="""
        <?xml version="1.0"?>
        <!-- synthetic -->
        <effectlist count="12">
        <effect id="0" icon="5397" offIcon="5398" duration="0" startMsgName="HASTE_S" kind="speed" priority="5"/>
        <effect id="1" icon="480" duration="0" startMsgName="SLOW_S" kind="slow"/>
        <effect id="2" icon="5395" offIcon="5395" duration="0" startMsgName="SPEED_DOUBLE_S" kind="cook" priority="6"/>
        <effect id="3" icon="484" duration="0" startMsgName="POTION_PRUDENCE_S" kind="cook"/>
        <effect id="4" icon="485" offIcon="5271" duration="0" startMsgName="SHIELD_S" kind="defense"/>
        <effect id="5" icon="1104" duration="960" startMsgName="OLD_SHADOW_ARMOR" kind="defense"/>
        <effect id="6" icon="0" duration="0" startMsgName="NO_ICON" kind="defense"/>
        <effect id="7" icon="9999" duration="0" startMsgName="MISSING_TBT" kind="defense"/>
        <effect id="8" icon="1620" duration="-1" startMsgName="HOLD_S" kind="paralyse"/>
        <effect id="9" icon="824" duration="0" startMsgName="SILENCE_S" kind="curse"/>
        <effect id="10" icon="5401" duration="-1" startMsgName="Invisiblity" kind="neutrality"/>
        <effect id="11" icon="552" duration="0" startMsgName="WATER_BREATH_S" kind="cook"/>
        </effectlist>
        """;

    static UiResources BuffSyntheticResources(string dir,IReadOnlyDictionary<string,(int Archive,byte[] Data)> tbts){
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml("<?xml version=\"1.0\"?><SurfList><surf id=\"1\" file=\"1\"/></SurfList>")),("effectlist2.xml",Xml(BiEffects)));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (name,(a,data)) in tbts)items[a].Add((name,data));
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        return new UiResources(dir,tilePin,spritePins);
    }
    static int Hn(string name)=>1+SurfCatalog.NameHash(name);

    [CheckGroup("native-ui-buff-icons",Order=1),CheckGroup(CheckGroups.Default,Order=125)]
    static void NativeUiBuffIconsChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-473","synthetic");Directory.CreateDirectory(root);

        // --- V3 layout: (798 − 35·(n mod 22 + 1), 5 + 35·⌊n/22⌋).
        Check(BuffIconsPanel.Cell(0)==new Point(763,5)&&BuffIconsPanel.Cell(1)==new Point(728,5)&&BuffIconsPanel.Cell(21)==new Point(28,5)&&BuffIconsPanel.Cell(22)==new Point(763,40)&&BuffIconsPanel.Cell(45)==new Point(728,75),"BUFF cells: first (763,5), 35 px pitch leftwards, 22 per row");
        Check(BuffIconsPanel.TooltipRect(new(763,5,32,32))==new Rectangle(635,37,160,BuffIconsPanel.TooltipHeight)&&BuffIconsPanel.TooltipRect(new(28,5,32,32)).Location==new Point(28,37),"BUFF tooltip 160 wide at (x, y+32), moved left when < 5 px from the right edge");

        // --- effectlist2 parsing and sort keys.
        var layout=UiLayout.Parse(BiEffects.Trim(),"effectlist2.xml");
        var table=BuffEffectTable.Parse(layout);
        Check(table.Count==12&&table[0] is {Icon:5397,OffIcon:5398,Kind:BuffKind.Speed,Priority:5}&&table[12]==null&&table[-1]==null&&table.OrderFaults.Count==0,"BUFF effectlist2: 12 records, id ≥ count → none");
        Check(table[0]!.SortKey==5&&table[1]!.SortKey==0&&table[9]!.SortKey==1&&table[8]!.SortKey==2&&table[4]!.SortKey==100&&table[1]!.Debuff&&table[9]!.Debuff&&table[8]!.Debuff&&!table[4]!.Debuff,"BUFF sort key: priority, else kind 0–2, else 100; slow/curse/paralyse are debuffs");
        Check(BuffEffectTable.ParseKind("magic doll")==BuffKind.MagicDoll&&BuffEffectTable.ParseKind("legacy")==BuffKind.Legacy&&BuffEffectTable.ParseKind(null)==null,"BUFF kind names (off_138D8FC)");
        Throws<UiLayoutException>(()=>BuffEffectTable.Parse(UiLayout.Parse("<effectlist><effect id=\"0\" kind=\"bogus\"/></effectlist>","e.xml")),"BUFF unknown kind rejected");
        Throws<UiLayoutException>(()=>BuffEffectTable.Parse(UiLayout.Parse("<effectlist><effect id=\"0\"/><effect id=\"0\"/></effectlist>","e.xml")),"BUFF duplicate id rejected");
        var faulty=BuffEffectTable.Parse(UiLayout.Parse("<effectlist><effect id=\"1\"/></effectlist>","e.xml"));
        Check(faulty.OrderFaults.Count==1&&faulty.Count==1&&faulty[1]==null,"BUFF id out of order → Order fault logged; id ≥ record count not shown");

        // --- order: insertion semantics of sub_8FDE00.
        {
            var o=new BuffIconOrder();
            o.Sync([table[4]!,table[5]!]);
            o.Sync([table[4]!,table[5]!,table[2]!]);          // priority 6 goes in front of two keyless icons
            o.Sync([table[4]!,table[5]!,table[2]!,table[0]!]); // priority 5 in front of priority 6
            o.Sync([table[4]!,table[5]!,table[2]!,table[0]!,table[3]!]); // keyless → last (arrival order)
            Check(o.Buffs.Select(i=>i.Effect.Id).SequenceEqual([0,2,4,5,3]),"BUFF order: priority 5, 6, then keyless by arrival");
            o.Sync([table[9]!,table[8]!,table[1]!]);
            Check(o.Buffs.Count==0&&o.Debuffs.Select(i=>i.Effect.Id).SequenceEqual([1,9,8]),"BUFF debuff order slow, curse, paralyse; removed buffs drop out");
            Check(o.Cells().Select(c=>c.Cell).SequenceEqual([0,1,2]),"BUFF debuffs start at row 0 when there is no buff");
            o.Sync([table[9]!,table[8]!,table[1]!,table[4]!,table[0]!]);
            Check(o.Cells().Select(c=>(c.Effect.Id,c.Cell)).SequenceEqual([(0,0),(4,1),(1,22),(9,23),(8,24)]),"BUFF debuffs start on the row after the buffs");
            o.Sync([table[4]!,table[5]!]);o.Sync([table[4]!,table[5]!,table[3]!]);o.Sync([table[5]!,table[3]!,table[4]!]);
            Check(o.Buffs.Select(i=>i.Effect.Id).SequenceEqual([4,5,3]),"BUFF kept icons never move (arrival order, not list order)");
            var many=new BuffIconOrder();many.Sync(Enumerable.Range(0,23).Select(i=>new BuffEffect(i,1,0,BuffKind.Cook,0,0,"")).Append(new BuffEffect(99,1,0,BuffKind.Slow,0,0,"")));
            Check(many.Cells().Last()==(many.Debuffs[0].Effect,44),"BUFF 23 buffs → debuffs from row 2 (⌈23/22⌉ = 2)");
        }

        // --- .tbt decoding.
        {
            var img=TbtImage.Decode([1,2,4,2, 2,0,1,0x00,0x7C, 2,1,0x1F,0x00, 1,6,1,0x00,0x00]);
            Check(img is {OffsetX:1,OffsetY:2,Width:4,Height:2}&&img.Argb[0]==(UiImage.ToArgb(0x7C00)|0xFF000000)&&img.Argb[1]==0&&img.Argb[2]==(UiImage.ToArgb(0x001F)|0xFF000000)&&img.Argb[7]==0&&img.Argb.Count(v=>v!=0)==2,"BUFF .tbt RLE: skip/2, RGB555, value 0 and gaps transparent");
            Throws<UiResourceException>(()=>TbtImage.Decode([0,0,2,1,1,1,1,0,0]),"BUFF .tbt odd skip");
            Throws<UiResourceException>(()=>TbtImage.Decode([0,0,2,1,1,0,3,0,0,0,0,0,0]),"BUFF .tbt row overflow");
            Throws<UiResourceException>(()=>TbtImage.Decode([0,0,1,2,1,0,1,0,0]),"BUFF .tbt truncated");
            Throws<UiResourceException>(()=>TbtImage.Decode([0,0,1,1,1,0,1,0,0,9]),"BUFF .tbt trailing bytes");
            var c=BuffIconCatalog.Candidates(552).ToArray();
            Check(c.Length==4&&c[0]==(Hn("552c.tbt"),"552c.tbt")&&c[1]==(0,"552c.tbt")&&c[2]==(Hn("552.tbt"),"552.tbt")&&c[3]==(0,"552.tbt"),"BUFF icon lookup order: <n>c.tbt in Sprite{h}, Sprite; <n>.tbt in Sprite{h}, Sprite");
        }

        // --- world adapter: V3 packet → effect rows, unmapped reasons.
        {
            Check(BuffEffectRules.Map("bless-of-eva",TimedBuffRules.WaterBreath,null).Effect==11,"BUFF S126 → 11");
            Check(BuffEffectRules.Map("shield",99,null).Effect==5&&BuffEffectRules.Map("shield",3,null).Effect==482&&BuffEffectRules.Map("shield",151,null).Effect==6&&BuffEffectRules.Map("shield",159,null).Effect==7&&BuffEffectRules.Map("shield",168,null).Effect==8&&BuffEffectRules.Map("shield",-1001,null).Effect==4,"BUFF S216 type 3/5/6/7/10/other → 5/482/6/7/8/4");
            Check(BuffEffectRules.Map("strup",TimedBuffRules.DressMighty,null).Effect==9&&BuffEffectRules.Map("strup",TimedBuffRules.PhysicalEnchantStr,null).Effect==null&&BuffEffectRules.Map("dexup",TimedBuffRules.DressDexterity,null).Effect==10&&BuffEffectRules.Map("dexup",TimedBuffRules.PhysicalEnchantDex,null).Effect==null,"BUFF S166/S188: type 2 → 9/10; type unknown (could be 3) → unmapped");
            Check(BuffEffectRules.Map("icons1",32,null).Effect==45&&BuffEffectRules.Map("icons1",167,null).Effect==52&&BuffEffectRules.Map("icons1",148,null).Effect==18&&BuffEffectRules.Map("icons1",166,null).Effect==23&&BuffEffectRules.Map("icons1",183,null).Effect==108,"BUFF 250/20 snapshot rows");
            Check(new[]{31,1017,14,111,176,181}.All(s=>BuffEffectRules.Map("icons1",s,null).Effect==null)&&BuffEffectRules.Map("icons1",171,null).Effect==null&&BuffEffectRules.Map("icons1",171,0).Effect==27&&BuffEffectRules.Map("icons1",171,2).Effect==58,"BUFF snapshot: unread / conditional bytes unmapped; byte 26 by class");
            Check(BuffEffectRules.Map("aura",114,null).Effect==25&&BuffEffectRules.Map("aura",-220000-119*256-3,null).Effect==28&&BuffEffectRules.Map("aura",-220000-221*256-2,null).Effect==77&&BuffEffectRules.Map("aura",-220000-221*256-9,null).Effect==null&&BuffEffectRules.Map("aura",-220000-90*256,null).Effect==null,"BUFF 250/22 aura rows; others unmapped");
            Check(BuffEffectRules.Map("proxy",148,null).Effect==518&&BuffEffectRules.Map("proxy",12,null).Effect==537&&BuffEffectRules.Map("proxy",-400600,null).Effect==null,"BUFF 250/154 proxy keys 518–537");
            Check(BuffEffectRules.Map("native-weapon",12,null).Effect==379&&BuffEffectRules.Map("packetbox36",-3036,null).Effect==63&&BuffEffectRules.Map("packetbox40",68,null).Effect==41&&BuffEffectRules.Map("wisdom",-3057,null).Effect==3&&BuffEffectRules.Map("wind-shackle",167,null).Effect==52,"BUFF 250/154 gfx, 36, 40, 57, 44");
            Check(new[]{"packetbox34","packetbox35","icon21","cooking","doll","cake","map-limit"}.All(s=>BuffEffectRules.Map(s,0,null) is {Effect:null,Reason:not null}),"BUFF sources without a V3 row are unmapped with a reason");
            Check(BuffEffectRules.Brave(1,2).Effect==19&&BuffEffectRules.Brave(1,1).Effect==2&&BuffEffectRules.Brave(1,null).Effect==null&&BuffEffectRules.Brave(4,3).Effect==42&&BuffEffectRules.Brave(4,6).Effect==153&&BuffEffectRules.Brave(4,0).Effect==37&&BuffEffectRules.Brave(6,null).Effect==110&&BuffEffectRules.Brave(8,null).Effect==19&&BuffEffectRules.Brave(5,null).Effect==2&&BuffEffectRules.Brave(7,null).Effect==null,"BUFF S_SkillBrave modes");

            // Through the real WorldSession model.
            var w=TimedWorld();long speedNow=1000;Set(w,"SpeedClock",(Func<long>)(()=>speedNow));
            PolyFeed(w,Haste(123,1,10));PolyFeed(w,Eva(123,60));
            PolyFeed(w,Packet(b=>{b.Write((byte)216);b.Write((ushort)30);b.Write((byte)3);}));
            PolyFeed(w,Strup(100,16,90,5));
            PolyFeed(w,Packet(b=>{b.Write((byte)67);b.Write(123u);b.Write((byte)6);b.Write((ushort)300);}));
            var s=BuffState.From(w,1,BuffTooltipText.None);
            Check(s.Entries.Select(e=>e.Effect).Order().SequenceEqual([0,5,11,110])&&s.Entries.Single(e=>e.Effect==0) is {Seconds:10,Expired:false}&&s.Entries.Single(e=>e.Effect==110).Seconds==null&&s.Entries.Single(e=>e.Effect==11).Seconds==60,"BUFF world: haste 0 (10 s), shield type 3 → 5, S126 → 11, brave 6 → 110 (no timer kept)");
            Check(s.Unmapped.Any(u=>u.Origin=="strup:42"),"BUFF world: S166 type 5 (engine key 42) unmapped and listed");
            speedNow+=10_500;
            Check(BuffState.From(w,1,BuffTooltipText.None).Entries.Single(e=>e.Effect==0) is {Seconds:0,Expired:true},"BUFF world: haste timer over while still hasted → Expired (offIcon)");
            PolyFeed(w,Haste(123,0,0));PolyFeed(w,Packet(b=>{b.Write((byte)171);b.Write(123u);b.Write((byte)1);}));
            var s2=BuffState.From(w,1,BuffTooltipText.None);
            Check(!s2.Entries.Any(e=>e.Effect is 0 or 1)&&s2.Entries.Single(e=>e.Effect==396).DefaultDuration,"BUFF world: haste off removes 0; S171 → 396 with the table duration");
            Check(BuffState.From(w,4,BuffTooltipText.None).Unmapped.Any(u=>u.Origin=="invisible")&&BuffState.From(w,null,BuffTooltipText.None).Unmapped.Any(u=>u.Origin=="invisible"),"BUFF world: dark elf / unknown class invisibility unmapped (377/396 flag)");
        }

        // --- state unwrapping through the bridge chain (HpMp → Macro → LeftStatus → Chat → Buff).
        {
            var bs=new BuffState([new(4,30,false,"t")],[],BuffTooltipText.None);
            var basic=new UiStaticGameState(true,"t",1,1,1,1,1,0);
            IUiGameState chain=new UiHpMpGameState(new UiMacroGameState(new UiLeftStatusGameState(new UiChatGameState(new UiBuffGameState(basic,bs),new UiChatSnapshot(null,false)),LeftStatusState.Empty),new MacroBook(),null,false),[]);
            Check(BuffState.Of(chain)==bs&&BuffState.Of(basic)==null&&LeftStatusState.From(chain)==LeftStatusState.Empty&&MacroBook.StateOf(chain)!=null&&!UiChatGameState.From(chain).GameFocus,"BUFF state found through HpMp → Macro → LeftStatus → Chat; other modules still find theirs");
        }

        // --- panel end to end on synthetic archives.
        {
            var tbts=new Dictionary<string,(int,byte[])>{
                ["5397.tbt"]=(Hn("5397.tbt"),Tbt(32,32,BiBuff)),["5398.tbt"]=(Hn("5398.tbt"),Tbt(32,32,BiOff)),
                ["485.tbt"]=(Hn("485.tbt"),Tbt(32,32,BiBuff)),
                ["480.tbt"]=(0,Tbt(32,32,BiOther)),["480c.tbt"]=(Hn("480c.tbt"),Tbt(32,32,BiDebuff)),   // V4 (e): <n>c.tbt in SpriteNN wins over Sprite.pak
                ["1620.tbt"]=(Hn("1620.tbt"),Tbt(32,32,BiDebuff)),
            };
            using var res=BuffSyntheticResources(Path.Combine(root,"archives"),tbts);
            var built=UiRoot.Build(res,UiPanelRegistry.Panels.Where(r=>r.Id==BuffIconsPanel.PanelId).ToArray());
            Check(built.Find(BuffIconsPanel.PanelId) is BuffIconsPanel{Table.Count:12},"BUFF registry line builds the panel from effectlist2 (one line in UiPanelRegistry)");
            var p=new BuffIconsPanel(res.Sprites);p.Load(res.Xml.Load(BuffEffectTable.FileName),res.Surfs);
            Check(p.Id=="STATUS_EFFECT_ICONS"&&p.Layer==UiLayer.Bar&&p.Table!.Count==12&&!p.Visible,"BUFF panel loads effectlist2 from Tile; hidden with no effect");
            var state=new BuffState([new(4,30,false,"shield"),new(0,0,true,"haste"),new(1,12,false,"slow"),new(7,5,false,"missing"),new(6,5,false,"noicon"),new(40,5,false,"beyond")],[new("strup:42","x")],new("剩餘時間","秒","時間已到"));
            var gs=new UiBuffGameState(new UiStaticGameState(true,"t",1,1,1,1,1,0),state);
            p.Update(gs);
            Check(p.Layout.Select(x=>(x.Effect.Id,x.Rect.Location)).SequenceEqual([(0,new Point(763,5)),(4,new Point(728,5)),(7,new Point(693,5)),(6,new Point(658,5)),(1,new Point(763,40))]),"BUFF panel: haste (priority 5) first, keyless by arrival, slow on the next row; effect 40 ≥ count not created");
            Check(p.Layout[0] is {Off:true,Icon:5398}&&p.Layout[1] is {Off:false,Icon:485},"BUFF panel: expired haste draws offIcon 5398");
            Check(p.Bounds==Rectangle.Union(new(658,5,137,32),new(763,40,32,32))&&p.Visible,"BUFF panel bounds = icon cells");
            Check(p.Unmapped.Count==1,"BUFF panel keeps the unmapped list");
            using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            Check(canvas.Bitmap.GetPixel(770,10).ToArgb()==BiArgb(BiOff)&&canvas.Bitmap.GetPixel(735,10).ToArgb()==BiArgb(BiBuff)&&canvas.Bitmap.GetPixel(770,45).ToArgb()==BiArgb(BiDebuff),"BUFF paint: offIcon, icon, and the SpriteNN c.tbt for slow");
            Check(canvas.Bitmap.GetPixel(700,20).ToArgb()==Color.Black.ToArgb(),"BUFF paint: missing .tbt draws nothing (no placeholder)");
            bool numberDrawn=false;for(int y=5;y<37;y++)for(int x=658;x<690;x++)if(canvas.Bitmap.GetPixel(x,y).ToArgb()!=Color.Black.ToArgb())numberDrawn=true;
            Check(numberDrawn,"BUFF paint: icon 0 → effect id text (sub_9001F0)");
            // hover and tooltip
            var cmd=new UiNullCommands();
            Check(p.Mouse(new(UiMouseKind.Move,new(740,20),MouseButtons.None,0,0,Keys.None),cmd)&&p.Hover==4,"BUFF hover over SHIELD");
            Check(p.TooltipText(p.Hovered!)=="剩餘時間: 30 秒"&&p.Bounds.Contains(BuffIconsPanel.TooltipRect(new(728,5,32,32))),"BUFF tooltip \"930: s 931\" and bounds grow to include it");
            p.Mouse(new(UiMouseKind.Move,new(770,20),MouseButtons.None,0,0,Keys.None),cmd);
            Check(p.TooltipText(p.Hovered!)=="時間已到","BUFF tooltip on offIcon = string 2854");
            p.Mouse(new(UiMouseKind.Move,new(700,60),MouseButtons.None,0,0,Keys.None),cmd);
            Check(p.Hover==null&&p.Bounds==Rectangle.Union(new(658,5,137,32),new(763,40,32,32)),"BUFF leaving the icons hides the tooltip");
            Check(p.Mouse(new(UiMouseKind.Down,new(740,20),MouseButtons.Left,1,0,Keys.None),cmd)&&cmd.Calls.Count==0,"BUFF press on an icon is taken, no command");
            var none=new BuffIconsPanel(res.Sprites);none.Load(res.Xml.Load(BuffEffectTable.FileName),res.Surfs);
            none.Update(new UiBuffGameState(new UiStaticGameState(true),state with{Text=BuffTooltipText.None}));
            Check(none.TooltipText(none.Layout[1])==null,"BUFF no string table → no tooltip text");
            var infinite=new BuffIconsPanel(res.Sprites);infinite.Load(res.Xml.Load(BuffEffectTable.FileName),res.Surfs);
            infinite.Update(new UiBuffGameState(new UiStaticGameState(true),new([new(10,null,false,"invisible",DefaultDuration:true),new(5,null,false,"brave")],[],new("L","U","O"))));
            Check(infinite.TooltipText(infinite.Layout.Single(x=>x.Effect.Id==10))=="L: -- U"&&infinite.TooltipText(infinite.Layout.Single(x=>x.Effect.Id==5))==null,"BUFF duration −1 → \"--\"; unknown timer → no tooltip");
            p.Update(new UiStaticGameState(false));
            Check(!p.Visible&&p.Bounds.IsEmpty,"BUFF out of world → nothing");
            p.Update(new UiStaticGameState(true));
            Check(!p.Visible,"BUFF no buff adapter → nothing");
        }
    }

    /// <summary>Optional: TW_CLIENT_ROOT set → check the V3 table against the real effectlist2.xml and .tbt files, render to scratch/.</summary>
    [CheckGroup("native-ui-buff-icons",Order=2)]
    static void NativeUiBuffIconsRealClientChecks(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui buff-icons real client checks (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-473","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var layout=res.Xml.Load(BuffEffectTable.FileName);var table=BuffEffectTable.Parse(layout);
        Check(table.Count==540&&table.OrderFaults.Count==0,$"BUFF real: {layout.FileName} has 540 records in id order (got {table.Count})");
        // V3 §4 effect → icon/offIcon rows (sample of every mapped id).
        var v3=new (int Id,int Icon,int Off)[]{(0,5397,5398),(1,480,0),(2,5395,5395),(3,484,0),(4,485,5271),(5,1104,5272),(6,763,5273),(7,725,5274),(8,783,5275),(9,482,5246),(10,483,5247),(11,552,0),(12,777,5248),(13,728,5267),(18,774,5249),(19,735,5250),(20,738,5251),(21,855,5252),(22,771,5253),(23,827,5254),(25,880,5280),(26,1567,5281),(27,1557,5282),(28,890,0),(33,1119,5284),(37,1110,5243),(40,1556,5320),(41,1562,5286),(43,1554,5287),(45,795,5289),(46,1609,5290),(48,754,0),(50,760,0),(51,852,0),(52,1623,0),(53,1740,0),(54,1733,5292),(55,1834,0),(58,1736,5260),(63,1476,0),(67,2357,0),(69,2348,0),(72,2354,5261),(77,2433,0),(78,2432,0),(108,3187,0),(110,3075,5245),(111,3190,0),(113,3089,5303),(114,3188,0),(120,3097,5305),(123,3127,5306),(128,3119,5264),(129,3125,0),(132,2566,0),(153,5395,0),(379,5265,5266),(392,1128,5268),(394,5316,5317),(396,5401,0),(482,485,5271),(518,774,5249),(537,5265,5266)};
        var mism=v3.Where(r=>table[r.Id] is not {} e||e.Icon!=r.Icon||e.OffIcon!=r.Off).Select(r=>r.Id).ToArray();
        Check(mism.Length==0,"BUFF real: effectlist2 icon/offIcon equal the V3 table for "+v3.Length+" effects"+(mism.Length>0?": "+string.Join(",",mism):""));
        var icons=new BuffIconCatalog(res.Sprites);var rows=new List<object>();var bad=new List<string>();
        foreach(var r in v3)foreach(int icon in new[]{r.Icon,r.Off}.Where(i=>i>0).Distinct()){
            var g=icons.Get(icon);rows.Add(new{effect=r.Id,icon,where=g?.Where,size=g==null?null:$"{g.Value.Image.Width}×{g.Value.Image.Height}",offset=g==null?null:$"{g.Value.Image.OffsetX},{g.Value.Image.OffsetY}"});
            if(g==null||g.Value.Image.Width!=32||g.Value.Image.Height!=32)bad.Add($"{r.Id}:{icon}");
        }
        Check(bad.Count==0,"BUFF real: every V3 icon/offIcon decodes to 32×32 ("+rows.Count+" icons)"+(bad.Count>0?": "+string.Join(",",bad):""));
        // Render: a sample set (synthetic timers, labelled) over black.
        var p=new BuffIconsPanel(res.Sprites);p.Load(layout,res.Surfs);
        var sample=new BuffState([new(0,120,false,"haste"),new(4,300,false,"shield"),new(11,1800,false,"eva"),new(45,64,false,"meditation"),new(110,null,false,"brave"),new(518,600,false,"proxy"),new(5,0,true,"expired shadow armor"),new(1,8,false,"slow"),new(52,16,false,"shackle"),new(55,30,false,"erase"),new(63,60,false,"no chat")],[],BuffTooltipText.None);
        p.Update(new UiBuffGameState(new UiStaticGameState(true),sample));
        using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        string file=Path.Combine(outDir,"buff-icons-sample.png");canvas.Bitmap.Save(file,ImageFormat.Png);
        File.WriteAllText(Path.Combine(outDir,"buff-icons-real.json"),JsonSerializer.Serialize(new{client,file=layout.FileName,count=table.Count,sampleTimers="synthetic, not server data",rows,placed=p.Layout.Select(x=>new{effect=x.Effect.Id,name=x.Effect.Name,x.Icon,x.Off,rect=new{x.Rect.X,x.Rect.Y,x.Rect.Width,x.Rect.Height}}),bounds=new{p.Bounds.X,p.Bounds.Y,p.Bounds.Width,p.Bounds.Height}},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        Check(File.Exists(file),"BUFF real: sample PNG written to scratch/issue-473/real");
        // The whole registry still builds against the pinned real client (all-or-nothing start, §2.1).
        var ui=UiRoot.Build(res,UiPanelRegistry.Panels);
        Check(ui.Find(BuffIconsPanel.PanelId) is BuffIconsPanel{Table.Count:540}&&ui.Faulted.Count==0,"BUFF real: full UiPanelRegistry builds with the buff panel ("+ui.Panels.Count+" panels)");
    }
}
