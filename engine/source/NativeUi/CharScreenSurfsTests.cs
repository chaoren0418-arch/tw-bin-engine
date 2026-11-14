namespace P06.NativeUi;

// #1242 (#1211 SM07/SM08) 離線測試：選角／建角畫面每個元素拿到預期的 surf，職業卡片（舊版 60×80 頭像）不會出現在背景或按鈕上。
// 合成資料照 PakViewer 查到的實況（docs/reports/issue-1242.md、scratch/i1242/surf-compare.tsv）：
// - 本機（catalog）：6400–6529 每個 id 都是 "<id>.img" 60×80 頭像，沒有 "<id>c.img"。
// - kor25（reference）：141 個 "<id>c.img" 是真正的介面圖；另 30 個 id 只有 "<id>.img" 60×80 頭像。
// - ly（art）：同樣 141 個 "<id>c.img"，加上那 30 個 id 的真正圖（6421 800×600 背景等）。
// 不讀真實客戶端、不啟動遊戲、不連網路；CI 由 --module-tests 自動發現執行（CharUiBuilder／SelectCharPanel.Load 是 internal）。
static class CharScreenSurfsTests {
    /// <summary>The 30 ids that kor25 has only as a 60×80 "&lt;id&gt;.img" portrait (ly has the real picture).</summary>
    static readonly int[] PlainOnly=[6400,6401,6402,6403,6411,6412,6413,6414,6421,6422,6423,6424,6425,6429,6487,6488,
        6495,6496,6497,6498,6499,6500,6501,6502,6503,6504,6505,6506,6507,6509];
    /// <summary>Real sizes in ly (PakViewer, surf-compare.tsv).</summary>
    static Size? ArtSize(int id)=>id switch{
        6421=>new(800,600),
        >=6404 and <=6406=>new(115,31),
        >=6407 and <=6409=>new(58,24),
        6410=>new(345,220),
        >=6411 and <=6414=>new(58,50),
        >=6415 and <=6417=>new(88,35),
        >=6418 and <=6420=>new(96,35),
        >=6422 and <=6485=>new(337,110),
        6486=>new(70,14),
        6487 or 6488=>new(459,295),
        >=6489 and <=6494=>new(150,34),
        >=6495 and <=6501=>new(68,68),
        6502 or 6503=>new(26,25),
        >=6504 and <=6507=>new(18,16),
        6509=>new(181,17),
        >=6510 and <=6518=>new(105,31),
        >=6519 and <=6521 or >=6527 and <=6529=>new(137,30),
        6522=>new(406,350),
        6524=>new(383,350),
        6526=>new(266,28),
        _=>null,
    };
    static readonly Size PortraitSize=new(60,80);
    /// <summary>An RGB555 colour per id, so a painted pixel tells which surf is drawn.</summary>
    static ushort ColorOf(int id)=>(ushort)(((id*2654435761u)>>9)&0x7FFF|0x0421);
    static byte[] Img(Size s,ushort color){
        var d=new byte[8+s.Width*s.Height*2];
        BitConverter.GetBytes((ushort)s.Width).CopyTo(d,0);BitConverter.GetBytes((ushort)s.Height).CopyTo(d,2);
        for(int i=8;i<d.Length;i+=2){d[i]=(byte)color;d[i+1]=(byte)(color>>8);}
        return d;
    }
    static string N(int id)=>id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    static CharArtPayload? Art(int id)=>ArtSize(id) is {} s?new CharArtPayload("Sprite00",PlainOnly.Contains(id)?N(id)+".img":N(id)+"c.img",Img(s,ColorOf(id))):null;
    static CharArtPayload? Kor25(int id){
        if(PlainOnly.Contains(id))return new CharArtPayload("Sprite00",N(id)+".img",Img(PortraitSize,0x7C00));
        return ArtSize(id) is {} s?new CharArtPayload("Sprite00",N(id)+"c.img",Img(s,ColorOf(id))):null;
    }
    static UiSurface? Local(int id)=>id is >=6400 and <=6529
        ?new UiSurface(id,N(id)+".img","Sprite00",UiImage.Decode(Img(PortraitSize,0x7C00)))
        :id==7156?new UiSurface(id,"7156.img","Sprite14",UiImage.Decode(Img(new(800,600),0x001F))):null;
    static CharScreenSurfs WithArt()=>new(Art,Local,Kor25,"synthetic-ly");
    static CharScreenSurfs NoArt()=>new(null,Local,Kor25);

    static void Expect(WorldModuleTestContext t,CharUiWindow w,string name,int surf,string label){
        var p=w.Pieces.FirstOrDefault(x=>x.Name==name);
        t.Check(p?.Surface is {} s&&s.Surf==surf&&ArtSize(surf) is {} size&&s.Width==size.Width&&s.Height==size.Height,$"{label}: {name} = surf {surf} ({ArtSize(surf)})",p?.Surface is {} got?$"{got.Surf} {got.Width}x{got.Height} {got.Archive}/{got.FileName}":"null");
    }

    [WorldModuleTest("char-screen-surfs")]
    static void Run(WorldModuleTestContext t){
        // ---- 1. resolver order ------------------------------------------------------------------------------------------
        t.Check(CharScreenSurfs.Legacy(6430,"6430.img")&&CharScreenSurfs.Legacy(6421,"6421.IMG"),"plain <id>.img in 6400–6529 = legacy portrait");
        t.Check(!CharScreenSurfs.Legacy(6430,"6430c.img")&&!CharScreenSurfs.Legacy(7156,"7156.img")&&!CharScreenSurfs.Legacy(6399,"6399.img"),"c files and ids outside 6400–6529 are not legacy");
        var art=WithArt();
        foreach(int id in Enumerable.Range(6400,130).Where(i=>ArtSize(i)!=null)){
            var s=art.Get(id);
            if(s==null||s.Width!=ArtSize(id)!.Value.Width||s.Height!=ArtSize(id)!.Value.Height||!s.Archive.StartsWith("art:",StringComparison.Ordinal)){t.Check(false,$"art client serves surf {id}",s==null?"null":$"{s.Width}x{s.Height} {s.Archive}");break;}
        }
        t.Equal(new Size(800,600),art.Get(6421)!.Bitmap.Size,"6421 (Character_Select_Window BaseSurf) = 800×600 background from the art client");
        t.Check(art.Origins[6421].StartsWith("art:",StringComparison.Ordinal),"origin recorded",art.Origins[6421]);
        t.Check(ReferenceEquals(art.Get(6430),art.Get(6430)),"decoded once, cached");
        t.Equal(new Size(800,600),art.Get(7156)?.Bitmap.Size??Size.Empty,"id outside the legacy range and not in the art client → SurfCatalog (local)");

        var none=NoArt();
        t.Equal(new Size(337,110),none.Get(6430)?.Bitmap.Size??Size.Empty,"no art client: 6430 → kor25 6430c.img, the local 60×80 portrait is skipped");
        t.Check(none.Get(6430)!.Archive.StartsWith("ref:",StringComparison.Ordinal),"no art client: served by the reference client",none.Get(6430)!.Archive);
        foreach(int id in PlainOnly)if(none.Get(id) is {} bad){t.Check(false,$"no art client: legacy-only surf {id} is not drawn",$"{bad.Width}x{bad.Height} {bad.Archive}/{bad.FileName}");break;}
        t.Check(none.Origins[6421].StartsWith("missing:",StringComparison.Ordinal)&&none.Origins[6421].Contains("60×80"),"no art client: 6421 missing with the reason",none.Origins[6421]);
        foreach(int id in Enumerable.Range(6400,130))if(none.Get(id) is {} s&&s.Bitmap.Size==PortraitSize||art.Get(id) is {} a&&a.Bitmap.Size==PortraitSize){t.Check(false,$"no 60×80 portrait is ever returned (surf {id})");break;}

        // ---- 2. SelectUI: every element gets its surf; class cards only on the slot buttons ------------------------------
        var p=new SelectCharPanel();
        p.Load(UiLayout.Parse(SelectScreenGeometryTests.Xml,"SelectUI.xml"),art.Get);
        t.Check(p.Available,"SelectUI with the art client is usable",p.Unavailable);
        t.Check(p.Main!.Base is {Surf:6421,Width:800,Height:600},"Character_Select_Window BaseSurf = 6421 800×600");
        t.Equal(new Rectangle(0,0,800,600),p.Main.Bounds,"window = full screen");
        var expect=new (string Name,int Surf)[]{("Slot_Back",6429),("Select_Button",6430),("WaitDelete_Button",6430),("WaitToDel_Image",6486),("Create_Button",6426),
            ("Slot_Lock_Back",6425),("Lock_Button",6422),("Up_Button",6411),("Down_Button",6413),("Enter_Button",6404),("Back_Button",6415),("Delete_Button",6407),("CharacterInfo_Back",6410)};
        foreach(var (name,surf) in expect)Expect(t,p.Main,name,surf,"select");
        t.Check(p.Message!.Base is {Surf:6488,Width:459,Height:295},"Message_Window BaseSurf = 6488 459×295");
        Expect(t,p.Message,"Ok_Button",6489,"message");Expect(t,p.Message,"Cancel_Button",6492,"message");Expect(t,p.Message,"Back_Image",6487,"message");
        foreach(var piece in p.Main.Pieces.Concat(p.Message.Pieces))
            if(piece.Surface is {} s&&s.Bitmap.Size==PortraitSize){t.Check(false,$"no element is drawn with a 60×80 portrait ({piece.Path})");break;}
        // Class pictures 6430 + 8×class + 4×sex (+0…+3) belong to Select_Button / WaitDelete_Button only (R9-O sub_EF13F0).
        var classIds=new HashSet<int>();
        for(int k0=0;k0<=6;k0++)for(int sex=0;sex<=1;sex++){int c=SelectCharPanel.ClassSurf(k0,sex,true);for(int k=0;k<4;k++)classIds.Add(c+k);}
        t.Check(classIds.All(id=>id is >=6430 and <=6485),"class surfs stay inside 6430–6485");
        var foreign=p.Main.Pieces.Concat(p.Message.Pieces).Where(x=>x.Name is not ("Select_Button" or "WaitDelete_Button")&&(x.Surface is {} s&&classIds.Contains(s.Surf)||x.Highlight is {} h&&classIds.Contains(h.Surf)||x.Push is {} q&&classIds.Contains(q.Surf))).Select(x=>x.Path).ToArray();
        t.Equal("",string.Join(",",foreign),"no class picture on any other element (background, page / enter / back / delete / create / lock buttons)");
        t.Check(classIds.All(id=>art.Get(id) is {Width:337,Height:110}),"every class picture resolves to the 337×110 slot card");
        foreach(int pg in new[]{0,1}){var (n,h,q)=SelectCharPanel.UpSurfs(pg);var (dn,dh,dq)=SelectCharPanel.DownSurfs(pg);
            t.Check(new[]{n,h,q,dn,dh,dq}.All(id=>art.Get(id) is {Width:58,Height:50}),$"page {pg}: Up/Down run-time surfs are the 58×50 page buttons");}
        t.Check(new[]{6426,6427,6428}.All(id=>art.Get(id) is {Width:337,Height:110})&&SelectCharPanel.CreateSurfs(6428,false)==(6426,6427,6428),"Create_Button run-time surfs = 6426–6428 slot cards");

        // Painted: background, Enter_Button and a slot card show their own pictures.
        var root=new UiRoot();var shown=new SelectCharPanel(null,root);shown.Load(UiLayout.Parse(SelectScreenGeometryTests.Xml,"SelectUI.xml"),art.Get);root.Add(shown);
        shown.Attach(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4,SelectCharPanelTests.Char("甲",0,52))));
        root.Update(new UiStaticGameState(false,null,null,null,null,null,null,0));
        using(var canvas=new UiCanvas()){
            root.Compose(canvas,(g,r)=>g.FillRectangle(Brushes.White,r));
            var b=canvas.Bitmap;
            bool Is(int x,int y,int id)=>SelectCharPanelTests.Near(b.GetPixel(x,y),UiImage.ToArgb(ColorOf(id)));
            t.Check(Is(790,10,6421),"background 6421 painted (top right, no piece there)",b.GetPixel(790,10));
            t.Check(Is(550+100,354+20,6404),"Enter_Button painted with 6404",b.GetPixel(650,374));
            int sel=SelectCharPanel.SlotSurfs(SelectCharPanel.ClassSurf(0,0,true),true).Normal;
            t.Check(Is(31+300,28+100,sel),$"row 0 (selected prince) painted with slot card {sel}",b.GetPixel(331,128));
            t.Check(Is(31+300,28+124*2+100,6426),"row 2 (free slot) painted with Create_Button 6426",b.GetPixel(331,28+248+100));
        }

        // Without the art client the 30 legacy-only surfs are not drawn: Message_Window has no size → screen unavailable
        // (the existing WinForms select path is used, as for any missing layout image).
        var bare=new SelectCharPanel();bare.Load(UiLayout.Parse(SelectScreenGeometryTests.Xml,"SelectUI.xml"),none.Get);
        t.Check(bare.Main!.Base==null&&bare.Main.Find("Enter_Button")?.Surface is {Width:115,Height:31},"no art client: background not drawn, Enter_Button from kor25 6404c");
        t.Check(!bare.Available&&bare.Unavailable!.Contains("Message_Window"),"no art client: Message_Window has no picture → unavailable (WinForms fallback)",bare.Unavailable);

        // ---- 3. CreateUI: buttons of both scenes --------------------------------------------------------------------------
        var create=UiLayout.Parse("""
<?xml version="1.0" encoding="UTF-8"?>
<CreateUI>
    <Window Name="Class_Select_Scene" X="0" Y="0" Width="800" Height="600" Activate="0">
        <Button Name="Male_Button" X="148" Y="326" BaseSurf="6495;1" HighlightSurf="6496;1" PushSurf="6497;1" MouseEvent="Male_ClassSelectScene"/>
        <Image Name="Male_Back" X="148" Y="326" Surf="6501;3"/>
        <Button Name="Female_Button" X="220" Y="326" BaseSurf="6498;1" HighlightSurf="6499;1" PushSurf="6500;1" MouseEvent="Female_ClassSelectScene"/>
        <Button Name="Prev_Button" X="14" Y="535" BaseSurf="6415;4" HighlightSurf="6416;4" PushSurf="6417;4" MouseEvent="Prev_ClassSelectScene"/>
        <Button Name="Next_Button" X="690" Y="535" BaseSurf="6418;4" HighlightSurf="6419;4" PushSurf="6420;4" MouseEvent="Next_ClassSelectScene"/>
    </Window>
    <Window Name="Stat_Setting_Scene" X="0" Y="0" Width="800" Height="600" Activate="0">
        <Button Name="Down_Button" X="520" Y="357" BaseSurf="6504" HighlightSurf="6504" PushSurf="6505" MouseEvent="Down_CustomizingWin"/>
        <Button Name="Up_Button" X="542" Y="357" BaseSurf="6506" HighlightSurf="6506" PushSurf="6507" MouseEvent="Up_CustomizingWin"/>
        <Button Name="Exit_Button" X="734" Y="112" BaseSurf="6502" HighlightSurf="6502" PushSurf="6503" MouseEvent="Exit_CustomizingWin"/>
        <Image Name="Gauge1" X="161" Y="204" Surf="6509"/>
        <Button Name="Attack_Button" X="31" Y="149" BaseSurf="6510" HighlightSurf="6511" PushSurf="6512" MouseEvent="Auto_StatSettingScene"/>
        <Image Name="Stat_Summary_Back" X="8" Y="90" Surf="6524;1"/>
        <Button Name="Customizing_Button" X="54" Y="443" BaseSurf="6519" HighlightSurf="6520" PushSurf="6521;1" MouseEvent="Customizing_StatSettingScene"/>
        <Button Name="OK_Button" X="514" Y="532" BaseSurf="6489" HighlightSurf="6490" PushSurf="6491" MouseEvent="OK_StatSettingScene"/>
        <Button Name="Prev_Button" X="14" Y="535" BaseSurf="6415;4" HighlightSurf="6416;4" PushSurf="6417;4" MouseEvent="Prev_StatSettingScene"/>
        <Image Name="Input_Back" X="193" Y="536" Surf="6526"/>
    </Window>
</CreateUI>
""","CreateUI.xml");
        var builder=new CharUiBuilder("CreateUI.xml",art.Get);
        var cls=builder.Window(create.Root.Children[0]);var stat=builder.Window(create.Root.Children[1]);
        foreach(var (name,surf) in new[]{("Male_Button",6495),("Male_Back",6501),("Female_Button",6498),("Prev_Button",6415),("Next_Button",6418)})Expect(t,cls,name,surf,"create class scene");
        foreach(var (name,surf) in new[]{("Down_Button",6504),("Up_Button",6506),("Exit_Button",6502),("Gauge1",6509),("Attack_Button",6510),("Stat_Summary_Back",6524),("Customizing_Button",6519),("OK_Button",6489),("Prev_Button",6415),("Input_Back",6526)})Expect(t,stat,name,surf,"create stat scene");
        t.Check(cls.Find("Next_Button")!.Push is {Surf:6420,Width:96,Height:35}&&stat.Find("Customizing_Button")!.Push is {Surf:6521,Width:137},"highlight / push states resolve the same way");
        t.Equal(0,builder.Missing.Count,"create scenes: nothing missing with the art client");
        foreach(var piece in cls.Pieces.Concat(stat.Pieces))
            if(new[]{piece.Surface,piece.Highlight,piece.Push}.Any(s=>s!=null&&s.Bitmap.Size==PortraitSize)){t.Check(false,$"create: no 60×80 portrait ({piece.Path})");break;}

        // ---- 4. configuration ---------------------------------------------------------------------------------------------
        t.Equal((CharScreenSurfs.DefaultRoot,"default"),CharScreenSurfs.ResolveRoot(null,null),"default art client = 雷雨天堂");
        t.Equal(((string?)@"E:\art","env"),CharScreenSurfs.ResolveRoot(@" E:\art ",null),"TW_CHAR_ART_CLIENT wins");
    }
}
