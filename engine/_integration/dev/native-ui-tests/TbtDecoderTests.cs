using P06.NativeUi;

// T320（#758）P-C：.tbt 與 surf 的關係。TW16 靜態查證（docs/reports/issue-758.md）：
//   surf 群組 4 只組 "%d.img"（sub_E55DD0，VA 0x11DFA2C）與 "%s.img"（sub_E55C50，VA 0x11DFA6C）；
//   ".tbt" 只有群組 3（Icon）讀："%s.tbt"（sub_87A720，VA 0x117D800）、"%d.tbt"（sub_87B3A0，VA 0x117D924）。
// 所以 SurfCatalog 不把同號 .tbt 當 surf（那是圖示，畫出來就是替代圖）；TbtImage 只給 Icon 路徑用。
// 這裡的封存、SurfList.xml、.img、.tbt 全是本檔合成，寫在 scratch/issue-758/ 下；不讀任何客戶端。
partial class Checks {
    [CheckGroup("native-ui-tbt",Order=1),CheckGroup("native-ui-resources",Order=50),CheckGroup(CheckGroups.Default,Order=126)]
    static void NativeUiTbtDecoderChecks(string repo){
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-758","synthetic");
        Directory.CreateDirectory(dir);
        foreach(var f in Directory.GetFiles(dir))File.Delete(f);

        // --- 1. .tbt 解碼（TbtImage，Icon 路徑）：兩段、位移、0 值透明、x／y 位移保留。
        byte[] two=[3,1,5,2, 2,0,1,0x00,0x7C, 4,1,0x1F,0x00, 1,2,3,0xE0,0x03,0x00,0x00,0x1F,0x00];
        var t=TbtImage.Decode(two,"two.tbt");
        Check(t is {OffsetX:3,OffsetY:1,Width:5,Height:2},"TBT header x, y, w, h");
        uint red=UiImage.ToArgb(0x7C00)|0xFF000000,blue=UiImage.ToArgb(0x001F)|0xFF000000,green=UiImage.ToArgb(0x03E0)|0xFF000000;
        Check(t.Argb[0]==red&&t.Argb[1]==0&&t.Argb[2]==0&&t.Argb[3]==blue&&t.Argb[4]==0,"TBT row 0: pixel, skip 4 bytes = 2 px, pixel");
        Check(t.Argb[5]==0&&t.Argb[6]==green&&t.Argb[7]==0&&t.Argb[8]==blue&&t.Argb[9]==0,"TBT row 1: skip 1 px, value 0 stays transparent");
        using(var bmp=t.ToBitmap())Check(bmp.Width==5&&bmp.Height==2&&bmp.GetPixel(0,0).ToArgb()==unchecked((int)red)&&bmp.GetPixel(1,0).A==0,"TBT bitmap 32bpp, gaps alpha 0");
        Throws<UiResourceException>(()=>TbtImage.Decode([0,0,0,1,0]),"TBT width 0");
        Throws<UiResourceException>(()=>TbtImage.Decode([0,0,2,1]),"TBT missing row");
        Throws<UiResourceException>(()=>TbtImage.Decode([0,0,2,1,1,2,2,0,0,0,0]),"TBT segment past width");
        // 同一份 bytes 當 .img 解會失敗（兩種格式不互通，不能拿 .tbt 頂 surf）。
        Throws<UiResourceException>(()=>UiImage.Decode(Tbt(32,32,0x7C00)),"TBT bytes are not an .img");

        // --- 2. SurfCatalog 只找 .img。
        //   6104：只有 6104.tbt（模擬本機 FishingUI 的 6104／6105／6106／6108）→ null，日誌指出只有 Icon 檔。
        //   5902：有 5902c.img 與 5902.tbt（ReNStatusUIEx 的關閉鈕）→ 讀 c.img。
        //   6102：有 6102.img（Sprite.pak）與 6102.tbt → 讀 .img，尺寸是 img 的，不是 tbt 的。
        //   7000：什麼都沒有 → null，日誌沒有 Icon 提示。
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml("""
            <?xml version="1.0"?>
            <!-- synthetic -->
            <SurfList><surf id="6104" file="6104"/><surf id="5902" file="5902"/><surf id="6102" file="6102"/><surf id="7000" file="7000"/></SurfList>
            """.Trim())));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        items[Hn("6104.tbt")].Add(("6104.tbt",Tbt(23,32,0x7C00)));
        items[Hn("5902c.img")].Add(("5902c.img",Img(11,9,1,0x0000,0x03E0)));
        items[Hn("5902.tbt")].Add(("5902.tbt",Tbt(32,32,0x001F)));
        items[0].Add(("6102.img",Img(20,4,0,0,0x001F)));
        items[Hn("6102.tbt")].Add(("6102.tbt",Tbt(32,32,0x7C00)));
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        using var res=new UiResources(dir,tilePin,spritePins);
        var cat=res.Surfs;

        Check(SurfCatalog.SurfExtension==".img"&&SurfCatalog.IconExtension==".tbt","TBT/surf extensions: surf .img, icon .tbt");
        foreach(var f in new[]{"6104","5902","6102","7000"}){
            var names=cat.Candidates(f).Select(c=>c.Name).ToArray();
            Check(names.Length==4&&names.All(n=>n.EndsWith(".img",StringComparison.Ordinal))&&names[0]==f+"c.img"&&names[2]==f+".img","TBT surf candidates for "+f+" are .img only: "+string.Join(",",names));
        }
        static string[] Lines(string prefix)=>UiResourceLog.Recent().Where(l=>l.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
        int c6104=Lines("surf 6104:").Length;
        Check(cat.Resolve(6104)==null&&cat.Get(6104)==null,"TBT surf 6104 with only 6104.tbt → null (no substitute)");
        string where=SpriteName(Hn("6104.tbt"))+"/6104.tbt";
        Check(cat.IconOnlyFile(6104)==where,"TBT IconOnlyFile(6104) = "+where+" (got "+cat.IconOnlyFile(6104)+")");
        var log=Lines("surf 6104:");
        Check(log.Length==c6104+1&&log[^1].Contains(where)&&log[^1].Contains("不讀 .tbt"),"TBT missing-surf log names the icon file: "+string.Join(" | ",log));
        Check(cat.Get(6104)==null&&Lines("surf 6104:").Length==log.Length,"TBT second miss is not logged again");

        var s5902=cat.Get(5902);
        Check(s5902 is {Width:11,Height:9,FileName:"5902c.img"}&&s5902.Archive==SpriteName(Hn("5902c.img")),"TBT surf 5902 reads 5902c.img, not 5902.tbt");
        var s6102=cat.Get(6102);
        Check(s6102 is {Width:20,Height:4,FileName:"6102.img",Archive:"Sprite"},"TBT surf 6102 reads 6102.img (20×4), not the 32×32 6102.tbt");
        Check(cat.IconOnlyFile(5902)!=null&&cat.IconOnlyFile(6102)!=null,"TBT IconOnlyFile is diagnostic only (img still wins when present)");

        int c7000=Lines("surf 7000:").Length;
        Check(cat.Get(7000)==null&&cat.IconOnlyFile(7000)==null,"TBT surf 7000 with nothing → null, no icon hint");
        var log7000=Lines("surf 7000:");
        Check(log7000.Length==c7000+1&&log7000[^1]=="surf 7000: 圖檔不在 Sprite*.pak（file=7000）","TBT plain missing-surf log unchanged: "+string.Join(" | ",log7000));
        Check(cat.IconOnlyFile(12345)==null,"TBT IconOnlyFile for an id not in SurfList → null");

        // --- 3. Icon 路徑（BuffIconCatalog）仍讀得到同一個 .tbt：兩條路徑各讀各的群組。
        var icons=new BuffIconCatalog(res.Sprites);
        var ic=icons.Get(6104);
        Check(ic!=null&&ic.Value.Image.Width==23&&ic.Value.Image.Height==32&&ic.Value.Where==where,"TBT icon path still reads 6104.tbt (23×32)");
    }
}
