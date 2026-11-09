using P06;
using P06.NativeUi;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

// ui-red-notice (Issue #474). Offline: notices, string table and state are synthesized here; numbers are V2
// (docs/reference/ui-layout.md §2 "紅字與畫面中央系統訊息（V2）"). The real client string table is only read by
// NativeUiRedNoticeRealClientChecks when TW_CLIENT_ROOT is set (PNG and JSON go to scratch/ only).
partial class Checks {
    static ServerStringTable RnTable(IReadOnlyDictionary<int,string> lines,int count=2000){
        var all=new string[count];for(int i=0;i<count;i++)all[i]=lines.TryGetValue(i,out var s)?s:"";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\n"+string.Join("\n",all)));
    }
    static IUiGameState RnState(RedNotice? latest,int epoch,long now,ServerStringTable? table=null,Func<string,string>? resolve=null,bool inWorld=true)=>
        new UiRedNoticeGameState(new UiStaticGameState(InWorld:inWorld,Now:now),new RedNoticeState(latest,epoch,table,resolve));

    [CheckGroup("native-ui-red-notice",Order=1),CheckGroup(CheckGroups.Default,Order=126)]
    static void NativeUiRedNoticeChecks(){
        // --- V2 geometry: y = 144 + 12k; x = 400 − bytes·6/2 (CP950 bytes, escape codes included).
        Check(RedNoticePanel.Origin(4,"abcd")==new Point(388,192)&&RedNoticePanel.Origin(0,"")==new Point(400,144)&&RedNoticePanel.Origin(1,"x")==new Point(397,156)&&RedNoticePanel.Origin(2,"xy")==new Point(394,168),"RED origin: slot k at y 144+12k, x = 400 − bytes·3");
        Check(RedNoticePanel.ByteCount("你必須")==6&&RedNoticePanel.Origin(4,"你必須在此地停留 300 秒。")==new Point(400-(RedNoticePanel.ByteCount("你必須在此地停留 300 秒。")*6)/2,192)&&RedNoticePanel.ByteCount("你必須在此地停留 300 秒。")==25,"RED CP950: one Chinese character = 2 bytes = 12 px");
        Check(RedNoticePanel.Origin(4,"\\f3ab")==new Point(400-15,192),"RED width estimate includes the 3-byte \\f3 escape");
        Check(RedNoticePanel.SlotOf(1829)==0&&RedNoticePanel.SlotOf(554)==1&&RedNoticePanel.SlotOf(553)==2&&RedNoticePanel.SlotOf(551)==4&&RedNoticePanel.SlotOf(0)==4&&RedNoticePanel.SlotOf(1006)==4,"RED slots: 1829 → 0, 554 → 1, 553 → 2, others → 4");
        Check(RedNoticePanel.Palette.SequenceEqual(new ushort[]{0x0000,0x001F,0x03E0,0x7C00})&&NameColors.ToColor(RedNoticePanel.Palette[RedNoticePanel.TextIndex]).ToArgb()==Color.FromArgb(255,0,0).ToArgb()&&NameColors.ToColor(RedNoticePanel.Palette[RedNoticePanel.OutlineIndex]).ToArgb()==Color.Black.ToArgb(),"RED colours: text palette[3] #FF0000, outline palette[0] #000000");

        // --- colour runs (sub_841770).
        var runs=RedNoticePanel.Runs("a\\f2b\\<c\\f1d\\f9e",0x7C00);
        Check(runs.Select(r=>(r.Text,r.Color)).SequenceEqual([("a",(ushort)0x7C00),("b",(ushort)0x03E0),("c",(ushort)0x7C00),("d",(ushort)0x001F),("e",(ushort)0x001F)]),"RED runs: \\f2 green, \\< start colour, \\f1 blue, unread palette entry keeps the colour");
        Check(RedNoticePanel.Runs("x\\",0x7C00).Single().Text=="x\\"&&RedNoticePanel.Runs("",0x7C00).Count==0,"RED runs: trailing backslash literal, empty text no run");

        // --- text: string-c.tbl + sub_933850; 1006 → 1017; id ≥ count ignored; no table → DisplayNotices text.
        var table=RnTable(new Dictionary<int,string>{[551]="你已經殺了 %0 人。 如果你PK的次數達到 %1， 你將會被打入地獄。",[553]="\\f3NPC伺服器暫時關閉。",[1017]="環境設定錯誤",[1006]="買賣過濾",[637]="你必須在此地停留 %0 秒。"});
        var n551=new RedNotice(551,["5","100"],"你的PK次數已達5。達100次將下地獄。",true,1000);
        Check(RedNoticePanel.Raw(n551,table)=="你已經殺了 5\\< 人。 如果你PK的次數達到 100\\<， 你將會被打入地獄。","RED text from string-c.tbl with %n → argument + \\<");
        Check(RedNoticePanel.Raw(n551,null)==n551.Text,"RED text without the table = DisplayNotices text (engine fallback)");
        Check(RedNoticePanel.Raw(new RedNotice(1006,[],"",false,0),table)=="環境設定錯誤","RED 1006 → 1017");
        Check(RedNoticePanel.Raw(new RedNotice(2000,[],"x",false,0),table)==null,"RED id ≥ table count → ignored");

        // --- state transitions.
        {
            var p=new RedNoticePanel();
            Check(p.Layer==UiLayer.World&&!p.Visible&&p.Bounds.IsEmpty,"RED world layer (never hit-tested), empty → invisible");
            p.Update(RnState(null,1,0,table));Check(!p.Visible,"RED no notice → nothing");
            p.Update(RnState(n551,1,1000,table));
            var line=p.Lines().Single();
            Check(line.Slot==4&&line.Origin==RedNoticePanel.Origin(4,line.Raw)&&line.Origin.Y==192&&p.Visible&&p.Bounds==RedNoticePanel.Band,"RED normal notice in slot 4 at y=192");
            p.Update(RnState(n551,1,1000+RedNoticePanel.NormalMs-1,table));Check(p.Slots[4]!=null,"RED still shown at 14.999 s");
            p.Update(RnState(n551,1,1000+RedNoticePanel.NormalMs,table));Check(p.Slots[4]==null&&!p.Visible,"RED slot 4 cleared at 15 s");
            p.Update(RnState(n551,1,1000+RedNoticePanel.NormalMs+5,table));Check(p.Slots[4]==null,"RED the same notice is not shown again");
            var n637=new RedNotice(637,["300"],"你必須在此地停留 300 秒。",true,20000);var n553=new RedNotice(553,[],"",false,20100);var n1829=new RedNotice(1829,[],"AuthD",false,20200);
            p.Update(RnState(n637,1,20000,table));p.Update(RnState(n553,1,20100,table));p.Update(RnState(n1829,1,20200,null));
            Check(p.Slots[4]=="你必須在此地停留 300\\< 秒。"&&p.Slots[2]=="\\f3NPC伺服器暫時關閉。"&&p.Slots[0]=="AuthD"&&p.Slots[1]==null&&p.Slots[3]==null,"RED dedicated slots 0/2 plus slot 4 at the same time");
            Check(p.Lines().Select(l=>(l.Slot,l.Origin.Y)).SequenceEqual([(0,144),(2,168),(4,192)]),"RED lines y 144 / 168 / 192");
            p.Update(RnState(n1829,1,20000+RedNoticePanel.NormalMs,null));
            Check(p.Slots[4]==null&&p.Slots[0]=="AuthD"&&p.Slots[2]!=null,"RED slots 0–2 have no timer");
            var n552=new RedNotice(552,["1","2"],"B",true,40000);var n551b=n551 with{At=41000};
            p.Update(RnState(n552,1,40000,null));p.Update(RnState(n551b,1,41000,null));
            Check(p.Slots[4]==n551b.Text,"RED a new normal notice overwrites slot 4 (one line, no queue)");
            p.Update(RnState(n551b,1,41000+RedNoticePanel.NormalMs-1,null));Check(p.Slots[4]!=null,"RED 15 s counted from the newest notice");
            p.Update(RnState(n551b,2,41100,null));Check(!p.Visible,"RED world reset (epoch change) clears all five slots");
            p.Update(RnState(new RedNotice(700,[],"x",false,42000),2,42000,table));Check(p.Slots[4]=="",
                "RED table row empty → slot holds \"\" (drawn as nothing)");
            Check(!p.Visible,"RED empty text is not visible");
            var resolve=new RedNoticePanel();
            resolve.Update(RnState(new RedNotice(551,["$5","3"],"",true,0),1,0,table,s=>s.Replace("$5","某人")));
            Check(resolve.Slots[4]!.StartsWith("你已經殺了 某人\\<",StringComparison.Ordinal),"RED $nnn names resolved before measuring");
            var out_=new RedNoticePanel();out_.Update(RnState(n551,1,1000,table));out_.Update(RnState(n551,1,1001,table,inWorld:false));
            Check(!out_.Visible,"RED not in world → nothing drawn");
            Check(RedNoticeState.Of(new UiStaticGameState(InWorld:true))==null&&new RedNoticePanel() is var none&&Run(()=>none.Update(new UiStaticGameState(InWorld:true,Now:5))) &&!none.Visible,"RED without adapter → nothing (no guessed values)");
            var nested=new UiBuffGameState(new UiRedNoticeGameState(new UiStaticGameState(InWorld:true),new RedNoticeState(n551,3,null,null)),BuffState.Empty);
            Check(RedNoticeState.Of(nested)?.WorldEpoch==3,"RED adapter found under the buff wrapper");
            Check(new RedNoticePanel().Mouse(new UiMouseEvent(UiMouseKind.Down,new(400,195),MouseButtons.Left,1,0,Keys.None),new UiNullCommands())==false,"RED takes no mouse input");
        }

        // --- paint: red text, 1 px black outline, no background, inside the line box.
        {
            var p=new RedNoticePanel();p.Update(RnState(new RedNotice(9,[],"WARNING 12",false,0),1,0,null));
            using var canvas=new UiCanvas();var bg=Color.FromArgb(255,0,128,128);canvas.Clear(bg);
            canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            var o=p.Lines().Single().Origin;int red=0,black=0,other=0;var box=Rectangle.Empty;
            for(int y=0;y<UiRoot.LogicalHeight;y++)for(int x=0;x<UiRoot.LogicalWidth;x++){
                var c=canvas.Bitmap.GetPixel(x,y);if(c.ToArgb()==bg.ToArgb())continue;
                if(c.R==255&&c.G==0&&c.B==0)red++;else if(c.R==0&&c.G==0&&c.B==0)black++;else other++;
                var r=new Rectangle(x,y,1,1);box=box.IsEmpty?r:Rectangle.Union(box,r);
            }
            Check(red>20&&black>red/2&&other==0,$"RED paint: only #FF0000 text and #000000 outline (red {red}, black {black}, other {other})");
            Check(box.Left>=o.X-1&&box.Top>=o.Y-1&&box.Bottom<=o.Y+14&&box.Right<=o.X+10*6+12,$"RED paint stays in the line box from {o}: {box}");
            int boxPixels=box.Width*box.Height;Check(red+black<boxPixels*0.9,"RED no filled background (glyph pixels < 90 % of the box)");
        }
    }
    static bool Run(Action a){a();return true;}

    /// <summary>Optional: TW_CLIENT_ROOT set → render real string-c.tbl texts of 551 / 552 / 637 / 553 / 1829 on the 800×600 canvas (scratch only).</summary>
    [CheckGroup("native-ui-red-notice",Order=2)]
    static void NativeUiRedNoticeRealClientChecks(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui red notice real client checks (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-474","real");Directory.CreateDirectory(outDir);
        var table=ServerStringTable.Load(TextArchive.Open(client));
        var p=new RedNoticePanel();long t=0;
        foreach(var n in new[]{new RedNotice(1829,[],"",false,t),new RedNotice(553,[],"",false,t+1),new RedNotice(551,["5","100"],"",true,t+2)}){p.Update(RnState(n,1,n.At,table));}
        var rows=p.Lines().Select(l=>new{slot=l.Slot,raw=l.Raw,bytes=RedNoticePanel.ByteCount(l.Raw),x=l.Origin.X,y=l.Origin.Y}).ToArray();
        using(var canvas=new UiCanvas()){canvas.Clear(Color.FromArgb(255,40,60,40));canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();canvas.Bitmap.Save(Path.Combine(outDir,"red-notice-800x600.png"),ImageFormat.Png);}
        File.WriteAllText(Path.Combine(outDir,"red-notice-lines.json"),JsonSerializer.Serialize(new{table=table.Sha256,count=table.Count,rows},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}),new UTF8Encoding(false));
        Check(rows.Length==3&&rows.Select(r=>r.y).SequenceEqual([144,168,192]),"RED real string-c.tbl: slots 0 / 2 / 4 rendered at y 144 / 168 / 192");
        Console.WriteLine("red-notice real render: "+outDir);
    }
}
