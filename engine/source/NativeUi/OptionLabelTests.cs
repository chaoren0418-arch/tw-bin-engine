using System.Text;
namespace P06.NativeUi;

// #1208（#1186 SM57）option-labels 離線測試：選項窗「常用選項」勾選列的名稱。
// 合成 OptionUI 版面（八個 CheckButton＋Label Desc，與 kor25 OptionUI.xml 同樣的結構，編號是測試用）＋合成 Text.idx／Text.pak
// （記憶體內，假字，不是遊戲字串）。斷言：每個勾選列都有名稱 Label、Desc 編號對到正確的列；參考表是 dummy 佔位字時從補字表取同編號，
// 參考表有字時參考表優先；補字表編號對不齊、參考表是空字串、沒有補字表時都不補。不讀真實客戶端、不開遊戲、不寫檔。
// 由 --module-tests 自動發現執行（CI）。
static class OptionLabelTests {
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    static byte[] Table(int count,IEnumerable<(int Id,string Text)> rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        return Cp950(count+"\r\n"+string.Join("\r\n",lines));
    }
    static TextArchive Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return TextArchive.FromBytes([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }
    static RefClientTextSource Source(string name,byte[] desc,byte[]? strings=null)=>RefClientTextSource.FromArchive(
        Path.Combine(AppContext.BaseDirectory,"option-labels-synthetic",name),
        strings==null?Archive((RefClientTextSource.DescFileName,desc)):Archive((RefClientTextSource.DescFileName,desc),(RefClientTextSource.StringFileName,strings)));

    // 八個勾選列：MouseEvent → 測試用 Desc 編號（kor25 OptionUI.xml 的 Home 頁是 18805、18806、18803、20826、18808、18807、18813、20829）。
    static readonly (string Event,int Desc,int X,int Y)[] Rows=[
        ("ShowDropItemNameBtn",300,48,72),("ShowNpcNameBtn",301,226,72),("MacroScrollBtn",302,48,94),("GetAllItemsBtn",303,226,94),
        ("AutoLootPartyBtn",304,48,116),("TransparentDeadMonsterBtn",305,226,116),("TopmostWindowBtn",306,48,138),("EnableElfTelControlBtn",307,226,138),
    ];
    const int Count=400,Shared=200,Empty=320;
    static string FakeName(int id)=>"假名"+id;
    static IEnumerable<(int,string)> Common(int shift=0)=>Enumerable.Range(0,Shared).Select(i=>(i,"共同"+(i+shift)));
    // 參考表：共同字＋八個列都是 dummy 佔位字（kor25 的寫法：dummy 後面接行號），301 例外有真字（參考表優先）；320 是空字串。
    static byte[] PrimaryDesc=>Table(Count,Common().Concat(Rows.Select(r=>(r.Desc,r.Desc==301?"參考字":"dummy"+(r.Desc+2)))));
    // 補字表：同樣的共同字（編號對齊）＋八個列的假名；320 也有字（不能被拿來補空字串）。
    static byte[] FillDesc(int shift=0)=>Table(Count,Common(shift).Concat(Rows.Select(r=>(r.Desc,FakeName(r.Desc)))).Append((Empty,"不該出現")));

    static string Xml(){
        var rows=string.Concat(Rows.Select(r=>$"""
                  <CheckButton Name = "{r.Event[..^3]}" X = "{r.X}" Y="{r.Y}" BasePng = "930" PushPng = "931" Width="120" Height = "12" MouseEvent = "{r.Event}">
                    <Label Name = "Text_Label" X = "18" Y="1" Desc="{r.Desc}" FontColor="171,163,131"/>
                  </CheckButton>

            """));
        return $"""
            <?xml version="1.0"?>
            <OptionUI>
              <Window Name="Option" X="100" Y="50" BasePng="901;1" Moveable="30">
                <Control Name="Home" X="0" Y="0" Activate="0" EnableEvent = "HomeEnable">
                  <Image X="41" Y="49" Png="913;1"/>
            {rows}
                </Control>
              </Window>
            </OptionUI>
            """;
    }
    const uint BaseC=0xFF5A7A5A,CheckC=0xFF101010;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static OptionPanel Panel(){
        var png=new Dictionary<int,UiSurface>{[901]=Surface(901,420,200,BaseC),[930]=Surface(930,12,12,CheckC)};
        var p=new OptionPanel();
        p.Load(UiLayout.Parse(Xml(),"OptionUI-c.xml"),_=>null,id=>png.GetValueOrDefault(id));
        return p;
    }

    [WorldModuleTest("option-labels")]
    static void Run(WorldModuleTestContext t){
        LabelsPerRow(t);Fill(t);NoFill(t);Roots(t);Paint(t);
    }

    // ---- 每個勾選列都有名稱 Label，Desc 編號對到那一列 ----
    static void LabelsPerRow(WorldModuleTestContext t){
        var p=Panel();
        t.Check(p.Available,"合成版面可用",p.Unavailable);
        var home=p.Pages.Single(x=>x.Name=="Home").Pieces;
        var checks=home.Where(x=>x.Element=="CheckButton").ToArray();
        t.Equal(8,checks.Length,"常用選項八個勾選列");
        foreach(var r in Rows){
            var button=checks.SingleOrDefault(x=>x.MouseEvent==r.Event);
            t.Check(button!=null,$"{r.Event} 有勾選框");
            if(button==null)continue;
            int at=Array.IndexOf(home.ToArray(),button);
            var label=at+1<home.Count?home[at+1]:null;
            t.Check(label is {Kind:OptionPieceKind.Label,TextKind:"Desc"}&&label.TextValue==r.Desc.ToString(),
                $"{r.Event} 緊接著有名稱 Label，Desc={r.Desc}",label==null?"沒有":$"{label.Kind} {label.TextKind}={label.TextValue}");
            t.Equal(new Point(r.X+18,r.Y+1),label?.Offset??Point.Empty,$"{r.Event} 名稱在勾選框右邊 (+18,+1)");
        }
        t.Check(!p.Skipped.Any(s=>s.Contains("CheckButton")),"勾選列沒有被跳過",string.Join(" | ",p.Skipped));
    }

    // ---- dummy 佔位字 → 補字表同編號；參考表有字時參考表優先 ----
    static void Fill(WorldModuleTestContext t){
        var primary=Source("primary",PrimaryDesc);var fill=new RefClientTextFill(primary,Source("fill",FillDesc()));
        foreach(var r in Rows){
            string want=r.Desc==301?"參考字":FakeName(r.Desc);
            t.Equal(want,fill.Resolve("Desc",r.Desc.ToString()),$"{r.Event} Desc {r.Desc} → {want}");
            t.Check(r.Desc==301?primary.Resolve("Desc",r.Desc.ToString())=="參考字":primary.Resolve("Desc",r.Desc.ToString())==null,$"參考表本身：{r.Desc} {(r.Desc==301?"有字":"是佔位字（不畫）")}");
        }
        t.Check(primary.IsPlaceholder("Desc","300")&&!primary.IsPlaceholder("Desc","301")&&!primary.IsPlaceholder("Desc",Empty.ToString())&&!primary.IsPlaceholder("Desc","9999"),"IsPlaceholder 只認 dummy 記錄");
        t.Check(fill.Aligned("Desc"),"補字表與參考表編號對齊（共同字全同）");
        var share=RefClientTextFill.Agreement(primary.Table("Desc")!.Table,fill.Fill!.Table("Desc")!.Table);
        t.Check(share==200.0/201,"對齊率＝相同 200 筆／共同 201 筆（301 兩邊不同）",share);
        t.Equal("共同5",fill.Resolve("Desc","5"),"一般編號照參考表");
        t.Check(fill.Resolve("Desc",Empty.ToString())==null,"參考表是空字串（不是佔位字）→ 不補");
        t.Check(fill.Resolve("Desc","300;1")==FakeName(300),"\"300;1\" 旗標忽略");
        t.Check(fill.Resolve("String","300")==null,"補字表沒有 string-c.tbl → String 不補");
        t.Check(fill.Resolve("Caption","300")==null,"其他 kind → null");
        Func<string,string,string?> resolver=fill.Resolve;
        var p=Panel();p.TextResolver=resolver;
        var names=p.Pages[0].Pieces.Where(x=>x.Kind==OptionPieceKind.Label).Select(x=>resolver(x.TextKind!,x.TextValue!)).ToArray();
        t.Check(names.Length==8&&names.All(n=>n is {Length:>0}),"選項窗每個勾選列都解出名稱",string.Join("、",names));
    }

    // ---- 不補的情況 ----
    static void NoFill(WorldModuleTestContext t){
        var primary=Source("primary",PrimaryDesc);
        var shifted=new RefClientTextFill(primary,Source("shifted",FillDesc(shift:1)));
        t.Check(!shifted.Aligned("Desc")&&shifted.Resolve("Desc","300")==null,"補字表編號錯開（共同字都不同）→ 不補");
        var tiny=new RefClientTextFill(primary,Source("tiny",Table(Count,Rows.Select(r=>(r.Desc,FakeName(r.Desc))))));
        t.Check(RefClientTextFill.Agreement(primary.Table("Desc")!.Table,tiny.Fill!.Table("Desc")!.Table)==null&&tiny.Resolve("Desc","300")==null,
            $"共同記錄少於 {RefClientTextFill.MinShared} 筆，無法判斷對齊 → 不補");
        var none=new RefClientTextFill(primary,null);
        t.Check(none.Resolve("Desc","300")==null&&none.Resolve("Desc","301")=="參考字","沒有補字表 → 與原本的參考表行為相同");
        t.Check(RefClientTextFill.Resolver(null)==null,"沒有 UiResources → TextResolver 維持 null");
    }

    // ---- 補字客戶端的設定 ----
    static void Roots(WorldModuleTestContext t){
        t.Equal<(string?,string)>((RefClientTextFill.DefaultRoot,"default"),RefClientTextFill.ResolveRoot(null),"沒設定 → 預設小草");
        t.Equal<(string?,string)>((RefClientTextFill.DefaultRoot,"default"),RefClientTextFill.ResolveRoot("  "),"空白 → 預設");
        t.Equal<(string?,string)>((@"D:\x","env"),RefClientTextFill.ResolveRoot(@" D:\x "),"TW_REF_TEXT_FILL 路徑（去前後空白）");
        foreach(var off in new[]{"off","OFF","0","false"})t.Equal<(string?,string)>((null,"env"),RefClientTextFill.ResolveRoot(off),$"\"{off}\" → 不補");
        var primary=Source("primary",PrimaryDesc);
        t.Check(RefClientTextFill.ConfiguredFill(primary,primary.Root)==null,"補字客戶端與參考客戶端同一資料夾 → 不補");
        t.Check(RefClientTextFill.ConfiguredFill(primary,"relative\\dir")==null,"相對路徑 → 不補");
        t.Check(RefClientTextFill.ConfiguredFill(primary,Path.Combine(AppContext.BaseDirectory,"option-labels-absent-"+Environment.ProcessId))==null,"沒有 Text.idx → 不補");
        t.Check(RefClientTextFill.ConfiguredFill(primary,"off")==null,"off → 不補");
    }

    // ---- 畫面：有名稱時勾選框右邊有字（原本只有勾選框） ----
    static void Paint(WorldModuleTestContext t){
        var p=Panel();p.Open();
        var fill=new RefClientTextFill(Source("primary",PrimaryDesc),Source("fill",FillDesc()));
        int Ink(OptionPanel panel){
            using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(panel.Bounds);panel.Paint(canvas);canvas.Release();
            int n=0;var label=panel.Pages[0].Pieces.First(x=>x.Kind==OptionPieceKind.Label);
            for(int y=0;y<12;y++)for(int x=0;x<90;x++)
                if(canvas.Bitmap.GetPixel(panel.Origin.X+label.Offset.X+x,panel.Origin.Y+label.Offset.Y+y).ToArgb()!=Color.FromArgb(unchecked((int)BaseC)).ToArgb())n++;
            return n;
        }
        p.TextResolver=new RefClientTextFill(Source("primary",PrimaryDesc),null).Resolve;
        t.Equal(0,Ink(p),"只有參考表（dummy）→ 名稱位置沒有字（#1186 SM57 的樣子）");
        p.TextResolver=fill.Resolve;
        int ink=Ink(p);
        t.Check(ink>0,"補字後名稱位置有字",ink.ToString());
    }
}
