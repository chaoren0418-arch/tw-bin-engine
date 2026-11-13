using P06;
using P06.AssetPrep;
using P06.NativeUi;
using System.Diagnostics;

// T469 (#1054): F9 面板的道具圖示在 assets/inventory 沒有檔案時，由本機即時產生。
// 全部用合成 .tbt 與測試根目錄底下的檔案；不讀客戶端、不連線、不需要 engine/assets。
// 案例：有檔（檔案優先）、無檔即時產生、不存在的 icon 回 404、路徑穿越拒絕、只有 assets 根可以是 junction。
static class GeneratedIconChecks {
    const string Icons="https://p08.example/icons/";

    /// <summary>合成 .tbt：標頭 x、y、w、h，每列一段（位移 bytes、像素數、RGB555 像素），含略過與 0 值的透明像素。</summary>
    static byte[] Tbt(int w,int h,int seed,byte ox,byte oy){
        var b=new List<byte>{ox,oy,(byte)w,(byte)h};
        for(int y=0;y<h;y++){
            int skip=y%3,count=w-skip-y%2;
            if(count<1){skip=0;count=w;}
            b.Add(1);b.Add((byte)(skip*2));b.Add((byte)count);
            for(int x=0;x<count;x++){
                ushort v=(ushort)((seed*131+y*37+x*7+1)&0x7FFF);if((x+y)%5==0)v=0;
                b.Add((byte)v);b.Add((byte)(v>>8));
            }
        }
        return b.ToArray();
    }

    static bool Served((int Status,byte[]? Body,string Headers) r,byte[] body)=>r.Status==200&&r.Body!=null&&r.Body.SequenceEqual(body)&&r.Headers.Contains("image/png");
    static bool Refused((int Status,byte[]? Body,string Headers) r,int status)=>r.Status==status&&r.Body==null;

    static void Junction(string link,string target){
        link=Path.GetFullPath(link);target=Path.GetFullPath(target);   // mklink 需要反斜線路徑
        var start=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true};
        foreach(string a in new[]{"/c","mklink","/J",link,target})start.ArgumentList.Add(a);
        using var p=Process.Start(start)!;p.WaitForExit();
        if(p.ExitCode!=0||(File.GetAttributes(link)&FileAttributes.ReparsePoint)==0)throw new Exception("T469 無法建立測試用 junction "+link);
    }

    public static (Func<ushort,byte[]?> Icons,(int Width,int Height,byte[] Png,byte[] Rgba) Made) Run(string root,string stage,byte[] file42){
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("T469 "+name);checks++;}

        var t77=TbtImage.Decode(Tbt(12,9,7,3,2),"synthetic/77.tbt");
        var images=new Dictionary<int,TbtImage>{[77]=t77,[42]=TbtImage.Decode(Tbt(5,4,1,0,0),"synthetic/42.tbt")};
        var decoded=new Dictionary<int,ItemIcon>();var asked=new List<int>();
        var source=new ItemIconSource(i=>{
            asked.Add(i);
            if(i==500)throw new UiResourceException("synthetic/500.tbt: .tbt 第 0 列截斷");
            if(!images.TryGetValue(i,out var image))return null;
            if(!decoded.TryGetValue(i,out var icon))decoded[i]=icon=new ItemIcon(i,image.ToBitmap(),new(image.OffsetX,image.OffsetY),"synthetic");
            return icon;
        });
        Func<ushort,byte[]?> icons=id=>WebIconGenerator.Png(source,id);
        byte[] png77=PngWriter.Encode(t77.Width,t77.Height,t77.Argb);
        string inventory=Path.Combine(stage,"assets/inventory");

        // 有檔：檔案優先，不問產生器（合成來源也有 42，內容不同）。
        var file=WebLocalResource.Read(stage,Icons+"42.png",icons);
        Check(file.Status==200&&file.Body!.SequenceEqual(file42)&&file.Headers.Contains("image/png")&&asked.Count==0,"有檔：回檔案內容，不產生");

        // 無檔：即時產生，位元組與 inventory recipe 對同一張 .tbt 的輸出相同；不寫檔；第二次用記憶體快取。
        var made=WebLocalResource.Read(stage,Icons+"77.png",icons);
        Check(made.Status==200&&made.Headers.Contains("image/png")&&made.Body!.SequenceEqual(png77),"無檔：即時產生的 PNG 與 PngWriter(.tbt) 相同");
        using(var back=new Bitmap(new MemoryStream(made.Body!))){
            bool same=back.Width==t77.Width&&back.Height==t77.Height;
            for(int y=0;same&&y<back.Height;y++)for(int x=0;same&&x<back.Width;x++)same=(uint)back.GetPixel(x,y).ToArgb()==t77.Argb[y*t77.Width+x];
            Check(same,"無檔：PNG 解回來的像素等於 .tbt 解碼結果（含透明）");
        }
        Check(t77.Argb.Any(p=>p==0)&&t77.Argb.Any(p=>p>>24==0xFF),"合成 .tbt 同時有透明與不透明像素");
        var again=WebLocalResource.Read(stage,Icons+"77.png",icons);
        Check(again.Status==200&&ReferenceEquals(again.Body,made.Body),"無檔：第二次取用同一份記憶體快取");
        Check(Directory.GetFileSystemEntries(inventory).Select(Path.GetFileName).SequenceEqual(["42.png"])&&!Directory.Exists(Path.Combine(stage,"runtime")),"即時產生不寫任何檔案");
        Check(WebLocalResource.Read(stage,Icons+"77.png").Status==404&&WebIconResource.Read(Path.Combine(stage,"assets"),Icons+"77.png").Status==404,"沒有產生器時，無檔仍是 404");

        // 不存在的 icon：來源沒有、來源解碼失敗、id 0 → 404，不代換別的圖。
        asked.Clear();
        Check(Refused(WebLocalResource.Read(stage,Icons+"999.png",icons),404)&&asked.SequenceEqual([999]),"不存在的 icon 回 404");
        Check(Refused(WebLocalResource.Read(stage,Icons+"500.png",icons),404),"解不開的 .tbt 回 404");
        Check(Refused(WebLocalResource.Read(stage,Icons+"0.png",icons),404),"icon 0 回 404");
        Check(WebIconResource.Read(Path.Combine(stage,"assets"),Icons+"77.png",_=>[1,2,3]).Status==404,"產生器回的不是 PNG 時回 404");

        // 路徑穿越：不是「/icons/<0..65535>.png」原樣字串的一律拒絕，而且碰不到檔案系統與產生器。
        File.WriteAllBytes(Path.Combine(stage,"assets/secret.png"),file42);File.WriteAllBytes(Path.Combine(stage,"web/77.png"),file42);
        asked.Clear();
        foreach(string url in new[]{
            Icons+"../secret.png",Icons+"..%2Fsecret.png",Icons+"..\\secret.png",Icons+"%2e%2e/secret.png",Icons+"77.png/../../secret.png",
            Icons+"77/../77.png",Icons+"/77.png",Icons+"./77.png",Icons+"inventory/42.png",Icons+"../inventory/42.png",Icons+"..\\..\\web\\77.png",
            Icons+"77.png?x=1",Icons+"77.png#x",Icons+"77.png%00",Icons+"77.png/",Icons+"077.png",Icons+"+77.png",Icons+" 77.png",Icons+"%37%37.png",Icons+"77.PNG",Icons+"77",Icons+"65536.png",Icons+"C:/77.png",
            "https://p08.example/x/../icons/77.png","https://p08.example/web/../icons/77.png","https://p08.example/../assets/inventory/42.png",
            "https://p08.example/assets/inventory/42.png","https://p08.example/assets/secret.png","https://p08.example/web/77.png","https://p08.example/77.png",
            "https://p08.example//icons/77.png","https://p08.example/ICONS/77.png","https://p08.example:443/icons/77.png","https://user@p08.example/icons/77.png",
            "https://p08.example.evil/icons/77.png","https://evil.example/icons/77.png","http://p08.example/icons/77.png","file:///icons/77.png"}){
            var r=WebLocalResource.Read(stage,url,icons);
            Check(r.Body==null&&r.Status==(url.StartsWith(Icons,StringComparison.Ordinal)?404:403),"路徑穿越拒絕 "+url);
        }
        Check(asked.Count==0,"被拒絕的網址不會問產生器");

        // assets 根不存在：仍可即時產生（可攜版尚未準備 assets/inventory）；沒有產生器則 404。
        string bare=Path.Combine(root,"t469-no-assets");Directory.CreateDirectory(bare);
        Check(Served(WebLocalResource.Read(bare,Icons+"77.png",icons),png77)&&WebLocalResource.Read(bare,Icons+"77.png").Status==404,"assets 根不存在時即時產生");

        // 只有 assets 根可以是 junction：根是 junction → 檔案與即時產生都照常；inventory 是 junction → 403，也不以產生的圖代替。
        string rootLinkStage=Path.Combine(root,"t469-root-link"),innerLinkStage=Path.Combine(root,"t469-inner-link");
        string rootLink=Path.Combine(rootLinkStage,"assets"),innerLink=Path.Combine(innerLinkStage,"assets","inventory");
        Directory.CreateDirectory(rootLinkStage);Directory.CreateDirectory(Path.Combine(innerLinkStage,"assets"));
        foreach(string stale in new[]{rootLink,innerLink})if(Directory.Exists(stale))Directory.Delete(stale);   // 上次留下的 junction：非遞迴，只拆連結
        try{
            Junction(rootLink,Path.Combine(stage,"assets"));Junction(innerLink,inventory);
            Check(Served(WebLocalResource.Read(rootLinkStage,Icons+"42.png",icons),file42),"assets 根是 junction：有檔照常");
            Check(Served(WebLocalResource.Read(rootLinkStage,Icons+"77.png",icons),png77),"assets 根是 junction：無檔即時產生");
            asked.Clear();
            Check(Refused(WebLocalResource.Read(innerLinkStage,Icons+"42.png",icons),403),"inventory 是 junction：有檔也拒絕");
            Check(Refused(WebLocalResource.Read(innerLinkStage,Icons+"77.png",icons),403)&&asked.Count==0,"inventory 是 junction：不以即時產生的圖代替");
        }finally{
            foreach(string link in new[]{rootLink,innerLink})if(Directory.Exists(link)&&(File.GetAttributes(link)&FileAttributes.ReparsePoint)!=0)Directory.Delete(link);
        }
        Console.WriteLine($"PASS T469 generated icons: {checks} checks (file wins, generated on demand, 404, traversal, junction rule)");
        return (icons,(t77.Width,t77.Height,png77,PngWriter.Rgba(t77.Argb)));
    }
}
