using System.Drawing.Imaging;
using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// M7-V（T222／#529）好友／封鎖／信件封鎖三窗的離線測試。由 --module-tests 執行（名稱 friend-panel）。
// 只用程式合成的 XML（元素結構照 windows.md §1.2 的描述）與合成 .img，不讀原客戶端、不連網路、不開視窗。
// 另有 friend-panel-real：設 TW_NATIVE_UI_REAL=1 時才用真實 Tile.pak／Sprite*.pak 載入三窗並把畫布 PNG 寫到
// TW_NATIVE_UI_REAL_OUT（應指向 scratch/）；未設定時只記一筆「略過」，CI 不讀素材。
static class FriendPanelTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;

    // ---- synthetic XML (same element/attribute names and spacing style as the client files; coordinates from windows.md §1.2) ----
    internal static string Xml(FriendWindowSpec s,int rows=14,bool withList=true,string? root=null){
        var b=new StringBuilder();
        b.Append($"<?xml version=\"1.0\"?>\n<{root??s.RootName}  Toggle=\"true\">\n");
        b.Append($"<Window Name = \"{s.WindowName}\"  X = \"0\" Y = \"0\" BaseSurf = \"4990;1\" FrameSurf = \"4991;1\" Moveable = \"20\" KeyboardEvent = \"W\" MouseEvent = \"Dummy\" Activate = \"0\">\n");
        b.Append("<Button Name = \"Closewindow_Button\" X = \"222\" Y = \"13\" BaseSurf = \"4429\" PushSurf = \"4430\" MouseEvent = \"Close\"/>\n");
        b.Append($"<Image Name = \"{s.TitleImageName}\" X = \"20\" Y = \"28\" Surf = \"{4992+(int)s.Kind}\"/>\n");
        if(withList){
            b.Append("<List Name = \"Item_List\" X = \"25\" Y = \"50\" Width = \"182\" Height = \"226\" Vertical = \"1\">\n");
            for(int i=0;i<rows;i++)b.Append($"<RadioButton Name = \"Item_Button\" Width = \"182\" Height = \"16\" PushSurf = \"4995;4\" Caption = \"1\" MouseEvent = \"{s.NameEvent}\"><Label Name = \"Info_Label\" X = \"122\" Width = \"60\" Height = \"16\" Caption = \"[Info]\"/></RadioButton>\n");
            b.Append("</List>\n");
        }
        b.Append("<ScrollBar Name = \"Bestlist_Scrollbar\" X = \"215\" Y = \"56\" Width = \"12\" Height = \"205\" ScrollStart = \"14\" ScrollHeight = \"177\" Vertical = \"1\">\n");
        b.Append("<Image Name = \"Background_Image\" X = \"1\" Y = \"14\" Surf = \"4060;1\"/>\n<Button Name = \"Up_Button\" BaseSurf = \"4061;1\" PushSurf = \"4062;1\"/>\n");
        b.Append("<Button Name = \"Move_Button\" Y = \"14\" BaseSurf = \"4063;1\" PushSurf = \"4064;1\"/>\n<Button Name = \"Down_Button\" Y = \"191\" BaseSurf = \"4065;1\" PushSurf = \"4066;1\"/>\n");
        b.Append("</ScrollBar>\n<Label Name = \"Count_Label\" X = \"150\" Y = \"28\" Width = \"57\" Height = \"18\"/>\n</Window>\n");
        b.Append($"</{root??s.RootName}>\n");
        return b.ToString();
    }
    static UiSurface Solid(int id,int w,int h,ushort rgb555){
        var img=new UiImage(w,h,0,0,Enumerable.Repeat(UiImage.ToArgb(rgb555),w*h).ToArray());
        return new UiSurface(id,$"{id}.img","synthetic",img);
    }
    // Base 240×300 blue, close 12×12 red, row selection 182×16 green, thumb 12×10 white; title (4992–4994) and others missing.
    static readonly Dictionary<int,UiSurface> surfaces=new(){
        [4990]=Solid(4990,240,300,0x001F),[4429]=Solid(4429,12,12,0x7C00),[4430]=Solid(4430,12,12,0x7FE0),
        [4995]=Solid(4995,182,16,0x03E0),[4063]=Solid(4063,12,10,0x7FFF),
    };
    static UiSurface? Surf(int id)=>surfaces.GetValueOrDefault(id);

    sealed class FakeSource(int friends):IFriendListSource {
        public int Count=friends;
        public FriendListData Read(FriendListKind kind)=>new(Enumerable.Range(0,Count).Select(i=>new FriendListRow($"{kind}{i:00}",kind==FriendListKind.Friend?i%2==0:null)).ToArray());
    }
    static UiMouseEvent M(UiMouseKind k,int x,int y,int delta=0,MouseButtons b=MouseButtons.Left)=>new(k,new(x,y),b,1,delta,Keys.None);
    static void Click(FriendPanel p,int x,int y){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static readonly UiStaticGameState InWorld=new(InWorld:true);

    static FriendPanel Make(FriendWindowSpec s,IFriendListSource? src){
        var p=new FriendPanel(s,()=>src);p.Load(UiLayout.Parse(Xml(s),s.Layout+".xml"),Surf);return p;
    }

    [WorldModuleTest("friend-panel")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：三窗各一個 id，Layout 是對應 XML，共用同一個類別。
        foreach(var s in FriendWindowSpec.All){
            var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==s.Id);
            t.Check(reg!=null&&reg.Layout==s.Layout,"registered "+s.Id);
            t.Check(reg?.Create(new UiRoot()) is FriendPanel fp&&fp.Spec==s,"registration creates FriendPanel "+s.Id);
        }
        t.Equal(3,FriendWindowSpec.All.Select(s=>s.Id).Distinct().Count(),"three distinct window ids");
        t.Equal("Mailexclude_Window",FriendWindowSpec.MailExclude.Id,"mail exclude id is the XML window name");

        // 2. surf 屬性 "4990;1"／"4429"。
        t.Equal(new FriendSurf(4990,1),FriendSurf.Parse("4990;1"),"surf a;n");
        t.Equal(new FriendSurf(4429,1),FriendSurf.Parse(" 4429 "),"surf single");
        t.Equal(new FriendSurf(4995,4),FriendSurf.Parse("4995;4"),"surf count kept");
        t.Throws<FormatException>(()=>FriendSurf.Parse("x"),"surf rejects text");
        t.Throws<FormatException>(()=>FriendSurf.Parse("1;2;3"),"surf rejects three parts");

        // 3. 版面：座標全部來自 XML；三窗相同。
        var src=new FakeSource(20);
        foreach(var s in FriendWindowSpec.All){
            var p=Make(s,src);var l=p.Layout;
            t.Check(!p.Visible,"Activate=0 starts closed "+s.Id);
            t.Equal(new Rectangle(0,0,240,300),p.Bounds,"bounds = base surf size "+s.Id);
            t.Equal(14,l.RowCount,"14 rows "+s.Id);
            t.Equal(new Rectangle(25,50,182,16),p.RowRect(0),"row 0 "+s.Id);
            t.Equal(new Rectangle(25,50+13*16,182,16),p.RowRect(13),"row 13 "+s.Id);
            t.Equal(new Rectangle(147,50,60,16),p.InfoRect(0),"info label x=122 w=60 "+s.Id);
            t.Equal(new Rectangle(150,28,57,18),p.CountRect,"count label "+s.Id);
            t.Equal(new Rectangle(215,56,12,205),p.ScrollRect,"scroll bar "+s.Id);
            t.Equal(new Rectangle(222,13,12,12),p.CloseRect,"close button "+s.Id);
            t.Equal(new Rectangle(215,56,12,14),p.UpRect,"up button "+s.Id);
            t.Equal(new Rectangle(215,56+191,12,14),p.DownRect,"down button "+s.Id);
            t.Equal(new Rectangle(215,56+14,12,177),p.TrackRect,"track "+s.Id);
            t.Equal(4992+(int)s.Kind,l.Title.Id,"title surf "+s.Id);
        }
        // Base surf missing → hit rectangle from the XML extent (list/scroll/count), never an invented size.
        {var s=FriendWindowSpec.Friend;var p=new FriendPanel(s,()=>src);p.Load(UiLayout.Parse(Xml(s),"FriendUI.xml"),_=>null);
         t.Equal(new Rectangle(0,0,227,276),p.Bounds,"missing base surf → element extent");t.Equal(Size.Empty,p.CloseRect.Size,"missing close surf → not clickable");}
        t.Throws<UiLayoutException>(()=>Make(FriendWindowSpec.Friend,src).Load(UiLayout.Parse(Xml(FriendWindowSpec.Friend,withList:false),"FriendUI.xml"),Surf),"missing Item_List rejected");
        t.Throws<UiLayoutException>(()=>Make(FriendWindowSpec.Friend,src).Load(UiLayout.Parse(Xml(FriendWindowSpec.Friend,root:"ExcludeUI"),"FriendUI.xml"),Surf),"wrong root rejected");
        t.Throws<UiLayoutException>(()=>Make(FriendWindowSpec.Friend,src).Load(UiLayout.Parse(Xml(FriendWindowSpec.Exclude),"ExcludeUI.xml"),Surf),"wrong window name rejected");
        t.Throws<UiLayoutException>(()=>Make(FriendWindowSpec.Friend,src).Load(UiLayout.Parse(Xml(FriendWindowSpec.Friend,rows:15),"FriendUI.xml"),Surf),"rows taller than list rejected");

        // 4. 資料、人數、捲動。
        {
            var p=Make(FriendWindowSpec.Friend,src);var cmd=new UiNullCommands();
            p.Update(new UiStaticGameState());t.Equal(0,p.Rows.Count,"not in world → empty list");
            p.Update(InWorld);t.Equal(20,p.Rows.Count,"rows from source");t.Equal("20",p.CountText,"count label text");t.Equal(6,p.MaxTop,"max top = 20-14");
            t.Check(!p.Mouse(M(UiMouseKind.Down,30,60),cmd),"closed window ignores mouse");
            p.Toggle();t.Check(p.Visible,"toggle opens");
            t.Equal(14,p.ThumbY,"thumb at ScrollStart when top=0");
            Click(p,220,56+195);t.Equal(1,p.Top,"down button scrolls 1");
            Click(p,220,58);t.Equal(0,p.Top,"up button scrolls back");
            Click(p,220,58);t.Equal(0,p.Top,"up clamps at 0");
            t.Check(p.Mouse(M(UiMouseKind.Wheel,100,100,-240),cmd),"wheel handled");t.Equal(2,p.Top,"wheel down 2 notches");
            t.Check(!p.Mouse(M(UiMouseKind.Wheel,500,500,-120),cmd),"wheel outside not handled");
            p.Mouse(M(UiMouseKind.Down,220,56+14+176),cmd);t.Equal(6,p.Top,"track bottom = max top");
            t.Equal(14+177-10,p.ThumbY,"thumb at bottom when top=max");
            p.Mouse(M(UiMouseKind.Move,220,56+14),cmd);t.Equal(0,p.Top,"track drag to top");
            p.Mouse(M(UiMouseKind.Up,220,56+14),cmd);
            p.ScrollTo(6);
            Click(p,40,50+3*16+5);t.Equal("Friend09",p.SelectedName,"row 3 at top 6 selects index 9");
            src.Count=8;p.Update(InWorld);t.Equal(0,p.Top,"shorter list clamps top");t.Equal(null,p.SelectedName,"selection dropped when name disappears");
            Click(p,40,50+13*16+5);t.Equal(null,p.SelectedName,"empty row selects nothing");
            Click(p,40,50+5);t.Equal("Friend00",p.SelectedName,"row 0 selects");
            t.Check(p.IsDragHandle(new(100,5))&&!p.IsDragHandle(new(100,30))&&!p.IsDragHandle(new(225,15)),"drag strip = top Moveable px, excluding close");
            p.MoveTo(new(300,100));t.Equal(new Rectangle(325,150,182,16),p.RowRect(0),"rows follow window move");t.Equal(new Rectangle(522,113,12,12),p.CloseRect,"close follows move");
            t.Check(!p.Key(new UiKeyEvent(Keys.Down,true,false),cmd),"keys go to the game (待驗證)");
            Click(p,525,115);t.Check(!p.Visible,"close button hides");
            t.Equal(0,cmd.Calls.Count,"panel never calls outward commands");
            src.Count=20;
        }

        // 5. 繪製：底圖、選取列、捲軸頭；缺的 surf（標題、框）不畫任何替代色。
        {
            var p=Make(FriendWindowSpec.Exclude,src);p.Update(InWorld);p.Show();Click(p,40,50+16+5);
            using var canvas=new UiCanvas();var root=new UiRoot();root.Add(p);root.Compose(canvas,null);
            var bmp=canvas.Bitmap;
            t.Equal(Color.FromArgb(255,0,0,255).ToArgb(),bmp.GetPixel(21,29).ToArgb(),"missing title surf: base shows through");
            t.Equal(Color.FromArgb(255,0,255,0).ToArgb(),bmp.GetPixel(25+180,50+16+8).ToArgb(),"selected row draws 4995");
            t.Equal(Color.FromArgb(255,0,0,255).ToArgb(),bmp.GetPixel(25+180,50+8).ToArgb(),"unselected row no highlight");
            t.Equal(Color.FromArgb(255,255,0,0).ToArgb(),bmp.GetPixel(223,14).ToArgb(),"close button 4429");
            t.Equal(Color.White.ToArgb(),bmp.GetPixel(220,56+14+2).ToArgb(),"thumb at ScrollStart");
            t.Equal(Color.Black.ToArgb(),bmp.GetPixel(260,10).ToArgb(),"nothing outside bounds");
            t.Check(Enumerable.Range(27,100).Any(x=>Enumerable.Range(52,12).Any(y=>bmp.GetPixel(x,y).ToArgb()==Color.White.ToArgb())),"row text drawn");
        }

        // 6. 信件封鎖名單（本機）與轉接器。
        {
            var c=Seed();var v=c.BuddySnapshot();
            t.Equal(0,v.MailExcluded.Length,"mail exclude starts empty");
            t.Check(c.ToggleMailExclude("Alice",v)&&c.World.IsMailExcluded("alice"),"toggle adds (case-insensitive)");
            t.Check(!c.World.IsExcluded("Alice"),"mail exclude does not touch server exclude list");
            t.Check(!c.ToggleMailExclude("Bob",v),"stale view rejected");
            v=c.BuddySnapshot();t.Equal("Alice",string.Join(",",v.MailExcluded),"snapshot carries mail exclude");
            t.Check(v.Notice.Contains("本機"),"notice says local only");
            var adapter=new LoginFriendListSource(c);
            t.Equal("Alice",string.Join(",",adapter.Read(FriendListKind.MailExclude).Rows.Select(r=>r.Name)),"adapter mail exclude rows");
            t.Equal(0,adapter.Read(FriendListKind.Exclude).Rows.Length,"adapter exclude rows");
            t.Throws<ArgumentException>(()=>c.ToggleMailExclude("A B",c.BuddySnapshot()),"name with space rejected");
            t.Check(c.ToggleMailExclude("ALICE",c.BuddySnapshot())&&!c.World.IsMailExcluded("Alice"),"second toggle removes");
            c.ToggleMailExclude("Carol",c.BuddySnapshot());
            c.World.Reset();t.Equal(0,c.World.MailExcludedNames.Length,"world reset clears mail exclude");
            var off=new LoginConnection();t.Check(!off.ToggleMailExclude("Alice",off.BuddySnapshot()),"not in world → rejected");
        }
    }

    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87 own-character appear (same fields as AmountPromptTests / demo-tests Seed).
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static LoginConnection Seed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");
        return c;
    }

    [WorldModuleTest("friend-panel-real")]
    static void Real(WorldModuleTestContext t){
        if(Environment.GetEnvironmentVariable("TW_NATIVE_UI_REAL")!="1"){t.Check(true,"skipped (set TW_NATIVE_UI_REAL=1 to load the real client resources)");return;}
        string? output=Environment.GetEnvironmentVariable("TW_NATIVE_UI_REAL_OUT");
        var resources=UiResources.Shared;
        var src=new FakeSource(20);
        foreach(var s in FriendWindowSpec.All){
            var p=new FriendPanel(s,()=>src);
            try{p.Load(resources.Xml.Load(s.Layout),resources.Surfs);}catch(Exception ex){t.Check(false,"real load "+s.Id,ex.Message);continue;}
            var l=p.Layout;
            t.Equal(14,l.RowCount,"real 14 rows "+s.Id);t.Equal(new Rectangle(25,50,182,16),p.RowRect(0),"real row 0 "+s.Id);
            t.Equal(new Rectangle(150,28,57,18),p.CountRect,"real count "+s.Id);t.Equal(new Rectangle(215,56,12,205),p.ScrollRect,"real scroll "+s.Id);
            t.Equal(4992+(int)s.Kind,l.Title.Id,"real title surf "+s.Id);
            foreach(int id in new[]{l.Base.Id,l.Frame.Id,l.Title.Id,l.CloseBase.Id,l.RowPush.Id,l.ScrollBack.Id,l.UpBase.Id,l.ThumbBase.Id,l.DownBase.Id})
                t.Check(resources.Surfs.Get(id)!=null,$"real surf {id} {s.Id}");
            t.Equal(new Rectangle(0,0,resources.Surfs.Get(l.Base.Id)?.Width??-1,resources.Surfs.Get(l.Base.Id)?.Height??-1),p.Bounds,"real bounds "+s.Id);
            p.Update(InWorld);p.Show();Click(p,40,50+2*16+5);p.ScrollTo(1);
            if(output!=null){
                using var canvas=new UiCanvas();var root=new UiRoot();root.Add(p);root.Compose(canvas,null);
                Directory.CreateDirectory(output);canvas.Bitmap.Save(Path.Combine(output,s.Layout+".png"),ImageFormat.Png);
            }
        }
    }
}
