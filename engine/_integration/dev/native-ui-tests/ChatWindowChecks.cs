using P06;
using P06.NativeUi;
using System.Drawing.Imaging;
using System.Text.Json;

// ui-chat-window (Issue #468). Offline: synthetic XML/.img under scratch/issue-468/synthetic (never the real client).
// The optional NativeUiChatWindowRealRender reads the real client only when TW_CLIENT_ROOT is set and writes PNGs to scratch/.
partial class Checks {
    // CENTER_WINDOW of the TW uistatus86-c.xml (docs/reference/ui-layout.md §3). Group, element, xRPos, yRPos (or yPos for
    // the bottom-anchored bases), surf. Only this fixture holds the coordinates; a second origin proves nothing is hard-coded.
    static readonly (string Group,string Name,int? X,int? Y,bool Abs,string Surf)[] ChatFixture=[
        ("CHAT_MAIN_WINDOW","CHAT_WINDOW",0,600,true,"3279"),("CHAT_MAIN_WINDOW","CHAT_BORN",0,600,true,"3280"),
        ("CHAT_MAIN_WINDOW","CHAT_BAR",10,114,false,"2273"),
        ("CHAT_MAIN_WINDOW","LCHAT_WINDOW",0,600,true,"3288"),("CHAT_MAIN_WINDOW","LCHAT_BORN",0,600,true,"3289"),
        ("CHAT_MAIN_WINDOW","RCHAT_WINDOW",260,600,true,"3290"),("CHAT_MAIN_WINDOW","RCHAT_BORN",260,600,true,"3291"),
        ("CHAT_STEP_CONTROL","CHAT_UP",345,115,false,"2274"),("CHAT_STEP_CONTROL","CHAT_UP_FOCUS_ON",null,null,false,"2275"),("CHAT_STEP_CONTROL","CHAT_UP_FOCUS",null,null,false,"2276"),
        ("CHAT_STEP_CONTROL","CHAT_DOWN",345,115,false,"2277"),("CHAT_STEP_CONTROL","CHAT_DOWN_FOCUS_ON",null,null,false,"2278"),("CHAT_STEP_CONTROL","CHAT_DOWN_FOCUS",null,null,false,"2279"),
        ("CHAT_SCROLL_CONTROL","SCROLL1",346,37,false,"3281"),("CHAT_SCROLL_CONTROL","SCROLL2",null,null,false,"3282"),
        ("CHAT_SCROLL_CONTROL","SCROLL_UP_OFF",346,30,false,"2284"),("CHAT_SCROLL_CONTROL","SCROLL_UP_ON",null,null,false,"2285"),
        ("CHAT_SCROLL_CONTROL","SCROLL_DOWN_OFF",346,107,false,"2286"),("CHAT_SCROLL_CONTROL","SCROLL_DOWN_ON",null,null,false,"2287"),
        ("CHAT_SCROLL_CONTROL","SCROLL_KNOB",null,null,false,"2288"),("CHAT_SCROLL_CONTROL","SCROLL_KNOB_ON",null,null,false,"2289"),
        ("CHAT_ICON","CHAT_ICON_FRAME",164,4,false,"3285"),("CHAT_ICON","CHAT_ICON_ACTIVE_ON",299,5,false,"3286"),("CHAT_ICON","CHAT_ICON_ACTIVE",299,5,false,"3287"),
        ("CHAT_ICON","GENERAL_CHAT_ON",179,5,false,"2290"),("CHAT_ICON","GENERAL_CHAT",179,5,false,"2291"),
        ("CHAT_ICON","PARTY_CHAT_ON",198,5,false,"2292"),("CHAT_ICON","PARTY_CHAT",198,5,false,"2293"),
        ("CHAT_ICON","PLEDGE_CHAT_ON",221,5,false,"2294"),("CHAT_ICON","PLEDGE_CHAT",221,5,false,"2295"),
        ("CHAT_ICON","TRADE_CHAT_ON",240,5,false,"2296"),("CHAT_ICON","TRADE_CHAT",240,5,false,"2297"),
        ("CHAT_ICON","SYSTEM_CHAT_ON",258,5,false,"2975"),("CHAT_ICON","SYSTEM_CHAT",258,5,false,"2976"),
        ("CHANNEL_ICON","GENERAL_CHANNEL",8,115,false,"2977"),("CHANNEL_ICON","GENERAL_CHANNEL_ON",null,null,false,"2978"),("CHANNEL_ICON","GENERAL_CHANNEL_FOCUS",null,null,false,"2979"),
        ("CHANNEL_ICON","BLOOD_CHANNEL",8,100,false,"2980"),("CHANNEL_ICON","BLOOD_CHANNEL_ON",null,null,false,"2981"),
        ("CHANNEL_ICON","PARTY_CHANNEL",8,85,false,"2983"),("CHANNEL_ICON","PARTY_CHANNEL_ON",null,null,false,"2984"),
        ("CHANNEL_ICON","WHOLE_CHANNEL",8,70,false,"2986"),("CHANNEL_ICON","WHOLE_CHANNEL_ON",null,null,false,"2987"),
        ("CHANNEL_ICON","TRADE_CHANNEL",8,55,false,"2989"),("CHANNEL_ICON","TRADE_CHANNEL_ON",null,null,false,"2990"),
        ("CHANNEL_ICON","CHANNEL_CHANNEL",8,40,false,"2992"),("CHANNEL_ICON","CHANNEL_CHANNEL_ON",null,null,false,"2993"),
        ("CHANNEL_ICON","ALLIANCE_CHANNEL",8,25,false,"2995"),("CHANNEL_ICON","ALLIANCE_CHANNEL_ON",null,null,false,"2996"),
        ("CHANNEL_ICON","UPPER_CHANNEL",8,10,false,"2998"),("CHANNEL_ICON","UPPER_CHANNEL_ON",null,null,false,"2999"),
    ];
    static string ChatXml(int x,int y,int step=36,string? omit=null){
        var sb=new System.Text.StringBuilder("<?xml version=\"1.0\"?>\n<MainStatusConfigulation>\n  <LEFT_WINDOW x=\"0\" y=\"475\"/>\n");
        sb.Append($"  <CENTER_WINDOW x = \"{x}\" y = \"{y}\" stepSize = \"{step}\">\n");
        foreach(var g in ChatFixture.Where(f=>f.Group+"/"+f.Name!=omit).GroupBy(f=>f.Group)){
            sb.Append($"    <{g.Key}>\n");
            foreach(var f in g)sb.Append($"      <{f.Name} surfNumber = \"{f.Surf}\"{(f.X is int fx?$" xRPos = \"{fx}\"":"")}{(f.Y is int fy?(f.Abs?$" yPos = \"{fy}\"":$" yRPos = \"{fy}\""):"")}/>\n");
            sb.Append($"    </{g.Key}>\n");
        }
        sb.Append("  </CENTER_WINDOW>\n</MainStatusConfigulation>\n");
        return sb.ToString();
    }
    // Synthetic surfaces. 3279/3288/3290 have two row colours: the top 10 rows ("title") vs the rest, to prove the image is drawn from the window top.
    const ushort CwLow=0x0010,CwHigh=0x0200,CwLeft=0x4000,CwRight=0x0011,CwBar=0x2108,CwStep=0x7C00,CwStepDown=0x03E0;
    static readonly Dictionary<int,(int W,int H,ushort C)> ChatSurfs=BuildChatSurfs();
    static Dictionary<int,(int W,int H,ushort C)> BuildChatSurfs(){
        var d=new Dictionary<int,(int,int,ushort)>{
            [3279]=(362,240,CwLow),[3288]=(260,240,CwLeft),[3290]=(259,240,CwRight),[3280]=(362,240,0x7FFF),[2273]=(346,16,CwBar),
            [2274]=(14,12,CwStep),[2277]=(14,12,CwStepDown),[2284]=(12,7,0x0101),[2285]=(12,7,0x0102),[2286]=(12,7,0x0103),[2287]=(12,7,0x0104),[2288]=(12,10,0x0105),
            [3285]=(118,15,0x0106),[3286]=(16,12,0x0107),[3287]=(16,12,0x0108),
        };
        ushort c=0x1000;
        foreach(int id in new[]{2290,2291,2292,2293,2294,2295,2296,2297,2975,2976})d[id]=(16,12,c++);
        foreach(int id in new[]{2977,2978,2980,2981,2983,2984,2986,2987,2989,2990,2992,2993,2995,2996,2998,2999})d[id]=(18,14,c++);
        return d;
    }
    static byte[] ChatImg(int id,int w,int h,ushort color){
        if(id==3280)return Img(w,h,1,0x7FFF,0x7FFF); // fully colour-keyed frame: drawn, but transparent
        if(id is not (3279 or 3288 or 3290))return Img(w,h,0,0,color);
        // The top 10 rows use CwHigh.
        var px=new ushort[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=y<10?CwHigh:color;
        return Img(w,h,0,0,px);
    }
    static UiResources ChatResources(string dir,string xml,int[]? noImage=null){
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        var rows=string.Concat(ChatSurfs.Keys.Select(id=>$"<surf id=\"{id}\" file=\"{id}\"/>"));
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml($"<?xml version=\"1.0\"?><SurfList>{rows}</SurfList>")),("uistatus86-c.xml",Xml(xml)));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,(w,h,c)) in ChatSurfs){if(noImage?.Contains(id)==true)continue;string n=$"{id}.img";items[1+SurfCatalog.NameHash(n)].Add((n,ChatImg(id,w,h,c)));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        return new UiResources(dir,tilePin,spritePins);
    }

    sealed class ChatCommands:IUiChatCommands {
        public List<string> Calls{get;}=[];
        public bool Allow=true,SendResult=true;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools");
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool BeginChatInput(){Calls.Add("begin");return Allow;}
        public void EndChatInput()=>Calls.Add("end");
        public Task<bool> SendChat(string text,PlayerChatView displayed){Calls.Add("send:"+text);return Task.FromResult(SendResult);}
    }
    static PlayerChatView ChatView(IEnumerable<PlayerChatLine> lines,bool canSend=true,int generation=1,uint actor=7)=>new(generation,1,actor,1,canSend,"",lines.ToArray());
    static PlayerChatLine[] ChatLines(int count,PlayerChatChannel channel=PlayerChatChannel.Normal,long first=1)=>
        Enumerable.Range(0,count).Select(i=>new PlayerChatLine(first+i,1,$"a: line {first+i}",channel)).ToArray();
    static IUiGameState ChatGame(PlayerChatView? view,bool gameFocus=false,long now=0)=>new UiChatGameState(new UiStaticGameState(InWorld:true,Now:now),new UiChatSnapshot(view,gameFocus));
    static UiMouseEvent ChatClick(int x,int y)=>new(UiMouseKind.Down,new(x,y),MouseButtons.Left,1,0,Keys.None);
    static UiMouseEvent ChatWheel(int delta)=>new(UiMouseKind.Wheel,new(300,560),MouseButtons.None,0,delta,Keys.None);

    static void NativeUiChatWindowChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-468","synthetic");Directory.CreateDirectory(root);
        int Px(UiCanvas c,int x,int y)=>c.Bitmap.GetPixel(x,y).ToArgb();
        int Argb(ushort p)=>unchecked((int)UiImage.ToArgb(p));
        var reg=UiPanelRegistry.Panels.Single(p=>p.Id=="CENTER_WINDOW");
        Check(reg.Layout=="uistatus86","registry: CENTER_WINDOW reads uistatus86 (-c first)");

        using(var res=ChatResources(Path.Combine(root,"tw"),ChatXml(129,470))){
            var built=UiRoot.Build(res,[reg]);
            var panel=(ChatPanel)built.Find("CENTER_WINDOW")!;panel.CharWidth=_=>6;
            var cmd=new ChatCommands();
            Check(panel.Layer==UiLayer.Bar&&panel.Origin==new Point(129,470)&&panel.Bottom==600&&panel.Width==362,"CENTER_WINDOW origin (129,470), bottom = CHAT_WINDOW yPos 600, width = 3279 width 362");
            Check(panel.View.Heights.Steps.SequenceEqual([130,166,202,238,240]),"height steps 130→240 by stepSize 36 (last step clamps to the 240 base height)");

            built.Update(new UiStaticGameState());
            using(var c=new UiCanvas()){built.Compose(c,null);Check(!panel.Visible&&Px(c,130,599)==Color.Black.ToArgb(),"not in world: chat window hidden");}

            // Every step: Bounds, bottom anchoring of 3279, fixed / top-anchored components, visible rows.
            var lines=ChatLines(30);
            built.Update(ChatGame(ChatView(lines)));
            int[] rowsAt=[7,10,13,16,16];
            for(int i=0;i<5;i++){
                int h=panel.View.Heights.Steps[i];int top=600-h;
                if(i>0)Check(panel.Mouse(ChatClick(129+345+2,470+115+2),cmd),$"step button click at {panel.View.Heights.Steps[i-1]} handled");
                Check(panel.View.Height==h&&panel.Bounds==new Rectangle(129,top,362,h),$"step {i}: height {h}, Bounds (129,{top},362,{h}) bottom-anchored at 600");
                using var c=new UiCanvas();built.Compose(c,null);
                var b=panel.Drawn.Single(d=>d.Element==ChatPanel.Base);
                Check(b.Rect==new Rectangle(129,top,362,h)&&Px(c,487,599)==Argb(CwLow)&&Px(c,487,top+10)==Argb(CwLow)&&Px(c,487,top)==Argb(CwHigh)&&Px(c,487,top+9)==Argb(CwHigh)&&(top==0||Px(c,487,top-1)==Color.Black.ToArgb())&&panel.Drawn.Single(d=>d.Element==ChatPanel.Frame).Rect==new Rectangle(129,top,362,h),
                    $"step {i}: 3279 + frame 3280 drawn from the window top {top} (top {h} rows), window bottom 600, nothing above");
                var comp=panel.Components;
                Check(comp[ChatPanel.Bar]==new Point(139,584)&&comp[ChatPanel.StepUp]==new Point(474,585)&&comp[ChatPanel.ScrollDown]==new Point(475,577)
                    &&comp[ChatPanel.TabPart(ChatTab.General,false)]==new Point(137,585)&&comp[ChatPanel.TabPart(ChatTab.Upper,false)]==new Point(137,480),
                    $"step {i}: input bar (10,114), step button (345,115), tabs x=8 y=10–115, scroll-down stay on the fixed base (470)");
                Check(comp[ChatPanel.FilterPart(ChatFilter.General,true)]==new Point(308,top+5)&&comp[ChatPanel.FilterPart(ChatFilter.System,true)]==new Point(387,top+5)
                    &&comp[ChatPanel.IconFrame]==new Point(293,top+4)&&comp[ChatPanel.ScrollUp]==new Point(475,top+30),$"step {i}: filter icons y=5, frame, scroll-up follow the top edge ({top})");
                Check(panel.VisibleRows==rowsAt[i]&&panel.Drawn.Count(d=>d.Element=="LINE")==rowsAt[i],$"step {i}: {rowsAt[i]} text rows visible");
                Check(panel.Drawn.Any(d=>d.Element==(i==4?ChatPanel.StepDown:ChatPanel.StepUp))&&!panel.Drawn.Any(d=>d.Element==(i==4?ChatPanel.StepUp:ChatPanel.StepDown)),$"step {i}: {(i==4?"CHAT_DOWN at max":"CHAT_UP below max")}");
            }
            panel.Mouse(ChatClick(129+345+2,470+115+2),cmd);
            Check(panel.View.Height==130&&panel.Bounds.Y==470,"step button at max returns to 130");
            panel.View.Grow();panel.View.Grow();Check(panel.View.Height==202,"Grow ×2 → 202");panel.View.Shrink();Check(panel.View.Height==166,"Shrink → 166");
            panel.View.SetHeight(500);Check(panel.View.Height==240,"SetHeight clamps to 240");panel.View.SetHeight(130);
            Check(built.HitTest(new(130,470))==panel&&built.HitTest(new(130,469))!=panel,"hit area = current bounds only");

            // Lines: newest at the bottom row, palette colour, scroll / wheel / buttons.
            built.Update(ChatGame(ChatView(lines)));
            using(var c=new UiCanvas()){
                built.Compose(c,null);
                var rows=panel.Drawn.Where(d=>d.Element=="LINE").ToList();
                Check(rows[0].Text=="a: line 30"&&rows[0].Rect.Location==new Point(159,570)&&rows[^1].Text=="a: line 24","newest line on the bottom row (y = 582 − 12), 7 rows: 24…30");
                Check(panel.TotalRows==30,"30 rows total");
            }
            string Bottom(){using var c=new UiCanvas();built.Compose(c,null);return panel.Drawn.First(d=>d.Element=="LINE").Text!;}
            panel.Mouse(ChatWheel(120),cmd);Check(panel.View.Scroll==3&&Bottom()=="a: line 27","wheel up scrolls 3 rows");
            panel.Mouse(ChatClick(476,470+30+2),cmd);Check(panel.View.Scroll==4&&Bottom()=="a: line 26","scroll-up button +1 row");
            panel.Mouse(ChatClick(476,470+107+2),cmd);Check(panel.View.Scroll==3,"scroll-down button −1 row");
            for(int i=0;i<20;i++)panel.Mouse(ChatWheel(120),cmd);
            Check(panel.View.Scroll==23&&Bottom()=="a: line 7","scroll stops at 30 − 7 = 23 (oldest line at the top row)");
            using(var c=new UiCanvas()){built.Compose(c,null);Check(panel.Drawn.Any(d=>d.Element==ChatPanel.ScrollDownOn)&&panel.Drawn.Any(d=>d.Element==ChatPanel.ScrollUp)&&panel.Drawn.Any(d=>d.Element=="CHAT_SCROLL_CONTROL/SCROLL_KNOB"),"at the top: scroll-down ON, scroll-up OFF, knob drawn");}
            // A new line while scrolled up keeps the view still.
            panel.Mouse(ChatWheel(-120),cmd);int before=panel.View.Scroll;string held=Bottom();
            built.Update(ChatGame(ChatView(lines.Concat(ChatLines(2,first:31)))));
            Check(Bottom()==held&&panel.View.Scroll==before+2,"new lines while scrolled up: view stays on the same rows");
            for(int i=0;i<20;i++)panel.Mouse(ChatWheel(-120),cmd);
            Check(panel.View.Scroll==0&&Bottom()=="a: line 32","wheel down back to the newest row");
            built.Update(ChatGame(ChatView(lines.Concat(ChatLines(3,first:31)))));
            Check(Bottom()=="a: line 33"&&panel.View.Scroll==0,"at the bottom the view follows new lines");

            // Line cap: 150 lines → only the last 100 are kept and scrollable.
            built.Update(ChatGame(ChatView(ChatLines(150),generation:2)));
            Check(panel.View.Scroll==0,"new session resets scroll");
            Bottom();Check(panel.TotalRows==ChatRules.MaxLines,"150 lines → 100 rows kept");
            for(int i=0;i<60;i++)panel.Mouse(ChatWheel(120),cmd);
            using(var c=new UiCanvas()){built.Compose(c,null);var r=panel.Drawn.Where(d=>d.Element=="LINE").ToList();Check(panel.View.Scroll==93&&r[^1].Text=="a: line 51"&&r[0].Text=="a: line 57","max scroll 100 − 7 = 93: top row is line 51 (lines 1–50 dropped)");}
            panel.View.ResetScroll();

            // Wrapping: 60 chars at 6 px in 314 px → 52 + 8.
            var longLine=new PlayerChatLine(1,1,new string('x',60));
            Check(ChatRules.Wrap(longLine.Text,314,_=>6).Select(s=>s.Length).SequenceEqual([52,8])&&ChatRules.Wrap("",10,_=>6).Count==1&&ChatRules.Wrap("ab",1,_=>6).Count==2,"wrap: greedy by width, ≥1 char per row");
            built.Update(ChatGame(ChatView([longLine],generation:3)));
            Bottom();Check(panel.TotalRows==2,"a long line takes two rows");

            // Tabs and filters: only the display changes (model untouched, no command sent).
            var mixed=new[]{new PlayerChatLine(1,1,"n: normal"),new PlayerChatLine(2,1,"{c} clan",PlayerChatChannel.Clan),new PlayerChatLine(3,1,"(p) party",PlayerChatChannel.Party),
                new PlayerChatLine(4,1,"[t] trade",PlayerChatChannel.Trade),new PlayerChatLine(5,1,"[g] global",PlayerChatChannel.Global),new PlayerChatLine(6,1,"(w) whisper",PlayerChatChannel.Whisper),
                new PlayerChatLine(7,1,"{{u}} prince",PlayerChatChannel.Prince),new PlayerChatLine(8,1,"[a] ally",PlayerChatChannel.Alliance)};
            var mixedView=ChatView(mixed,generation:4);
            built.Update(ChatGame(mixedView));cmd.Calls.Clear();panel.View.SetHeight(166);int ft=panel.Bounds.Y+5+2;
            string[] Shown(){using var c=new UiCanvas();built.Compose(c,null);return panel.Drawn.Where(d=>d.Element=="LINE").Select(d=>d.Text!).Reverse().ToArray();}
            Check(Shown().Length==8,"General tab shows every channel");
            foreach(var (tab,y,want) in new[]{(ChatTab.Blood,100,new[]{"{c} clan"}),(ChatTab.Party,85,["(p) party"]),(ChatTab.Whole,70,["[g] global"]),(ChatTab.Trade,55,["[t] trade"]),
                (ChatTab.Channel,40,Array.Empty<string>()),(ChatTab.Alliance,25,["[a] ally"]),(ChatTab.Upper,10,["{{u}} prince"]),(ChatTab.General,115,mixed.Select(l=>l.Text).ToArray())}){
                panel.Mouse(ChatClick(129+8+2,470+y+2),cmd);
                var shown=Shown();
                var tabs=panel.Drawn.Where(d=>d.Element.StartsWith("CHANNEL_ICON/",StringComparison.Ordinal)).ToList();
                Check(panel.View.Tab==tab&&shown.SequenceEqual(want)&&tabs.Count==8&&tabs.Count(d=>d.Element.EndsWith("_ON",StringComparison.Ordinal))==1&&tabs.Single(d=>d.Element.EndsWith("_ON",StringComparison.Ordinal)).Element==ChatPanel.TabPart(tab,true)
                    &&tabs.Single(d=>d.Element==ChatPanel.TabPart(tab,true)).Rect.Location==new Point(137,470+y),$"tab {tab} at (8,{y}): ON surf only on it, shows [{string.Join(", ",want)}]");
            }
            foreach(var (filter,x,hidden) in new[]{(ChatFilter.General,179,new[]{"n: normal","[g] global"}),(ChatFilter.Party,198,["(p) party"]),(ChatFilter.Pledge,221,["{c} clan","{{u}} prince","[a] ally"]),(ChatFilter.Trade,240,["[t] trade"]),(ChatFilter.System,258,Array.Empty<string>())}){
                panel.Mouse(ChatClick(129+x+2,ft),cmd);
                var shown=Shown();
                Check(!panel.View.Enabled(filter)&&shown.SequenceEqual(mixed.Select(l=>l.Text).Except(hidden))&&panel.Drawn.Any(d=>d.Element==ChatPanel.FilterPart(filter,false))&&!panel.Drawn.Any(d=>d.Element==ChatPanel.FilterPart(filter,true)),$"filter {filter} off hides [{string.Join(", ",hidden)}]");
                panel.Mouse(ChatClick(129+x+2,ft),cmd);
                Check(panel.View.Enabled(filter)&&Shown().Length==8,$"filter {filter} back on");
            }
            Check(cmd.Calls.Count==0&&mixedView.Lines.Length==8,"tab/filter clicks send nothing and do not touch the chat model");

            // Split: LCHAT 3288 + RCHAT 3290 at x=260; whispers in the right pane.
            panel.Mouse(ChatClick(129+299+2,ft),cmd);
            using(var c=new UiCanvas()){
                built.Compose(c,null);
                Check(panel.View.Split&&panel.Drawn.Single(d=>d.Element==ChatPanel.LeftBase).Rect==new Rectangle(129,434,260,166)&&panel.Drawn.Single(d=>d.Element==ChatPanel.RightBase).Rect==new Rectangle(389,434,259,166)
                    &&panel.Bounds.Width==519&&!panel.Drawn.Any(d=>d.Element==ChatPanel.Base)&&Px(c,130,599)==Argb(CwLeft)&&Px(c,490,599)==Argb(CwRight)&&panel.Drawn.Any(d=>d.Element==ChatPanel.SplitIconOn),"split: 3288 (260 wide) left, 3290 at x=260, window 519 wide, ACTIVE_ON icon");
                Check(panel.Drawn.Where(d=>d.Element=="RLINE").Select(d=>d.Text).SequenceEqual(["(w) whisper"])&&!panel.Drawn.Any(d=>d.Element=="LINE"&&d.Text=="(w) whisper")&&panel.Drawn.Single(d=>d.Element=="RLINE").Rect.X==129+264,"split: whisper only in the right pane");
            }
            panel.Mouse(ChatClick(129+299+2,ft),cmd);Check(!panel.View.Split,"split toggles back");

            // Input line: PlayerChatKeyboardFlow semantics.
            cmd.Calls.Clear();
            var enter=new UiKeyEvent(Keys.Enter,true,false);var enterUp=new UiKeyEvent(Keys.Enter,false,false);
            Check(!panel.Char('x'),"closed line ignores characters");
            built.Update(ChatGame(mixedView with{CanSend=false}));
            Check(panel.Key(enter,cmd)&&!panel.Input.Open&&cmd.Calls.Count==0,"CanSend=false: Enter consumed, line stays closed");
            built.Update(ChatGame(mixedView));
            cmd.Allow=false;Check(panel.Key(enter,cmd)&&!panel.Input.Open&&cmd.Calls.SequenceEqual(["begin"]),"bridge refuses (CanOpen) → stays closed");cmd.Allow=true;cmd.Calls.Clear();
            Check(panel.Key(new UiKeyEvent(Keys.Enter,true,true),cmd)&&!panel.Input.Open&&cmd.Calls.Count==0,"repeated Enter does not open but is consumed");
            Check(panel.Key(new UiKeyEvent(Keys.Enter|Keys.Shift,true,false),cmd)==false,"Shift+Enter is not the chat key");
            Check(panel.Key(enter,cmd)&&panel.Input.Open&&cmd.Calls.SequenceEqual(["begin"]),"Enter opens via BeginChatInput (LeaveGameFocus in the bridge)");
            Check(panel.Key(enter,cmd)&&cmd.Calls.Count==1,"opening Enter still held: no submit");
            panel.Key(enterUp,cmd);
            Check(panel.Key(enter,cmd)&&cmd.Calls.Count==1,"empty text: Enter does not send");
            panel.Key(enterUp,cmd);
            Check(!panel.Key(new UiKeyEvent(Keys.H,true,false),cmd),"printable key not consumed (WM_CHAR must follow)");
            foreach(char ch in "hi!")Check(panel.Char(ch),"char "+ch);
            Check(panel.Key(new UiKeyEvent(Keys.Back,true,false),cmd)&&panel.Input.Text=="hi","Backspace removes the last character");
            Check(!panel.Char('\b')&&!panel.Char('\r'),"control characters are not typed");
            using(var c=new UiCanvas()){built.Update(ChatGame(mixedView,now:0));built.Compose(c,null);
                Check(panel.Drawn.Single(d=>d.Element=="INPUT") is {Text:"hi"} inp&&inp.Rect.Location==new Point(143,586)&&panel.Drawn.Any(d=>d.Element=="CARET"),"typed text drawn inside CHAT_BAR (10,114)+(4,2), caret visible");}
            using(var c=new UiCanvas()){built.Update(ChatGame(mixedView,now:ChatRules.CaretBlinkMs));built.Compose(c,null);Check(!panel.Drawn.Any(d=>d.Element=="CARET"),"caret blinks");}
            Check(panel.Key(enter,cmd)&&cmd.Calls.SequenceEqual(["begin","send:hi"])&&panel.Input.Text==""&&panel.Input.Open,"Enter sends \"hi\" via SendChat; cleared after success, line stays open (legacy flow)");
            panel.Key(enterUp,cmd);
            cmd.SendResult=false;panel.Char('y');panel.Key(enter,cmd);panel.Key(enterUp,cmd);
            Check(panel.Input.Text=="y","failed send keeps the draft");cmd.SendResult=true;
            Check(panel.Key(new UiKeyEvent(Keys.Escape,true,false),cmd)&&!panel.Input.Open&&cmd.Calls[^1]=="end","Esc closes via EndChatInput (ReturnGameFocus)");
            Check(!panel.Key(new UiKeyEvent(Keys.Escape,true,false),cmd),"Esc with the line closed is left to the game");
            panel.Key(enter,cmd);panel.Key(enterUp,cmd);panel.Input.Clear();
            for(int i=0;i<200;i++)panel.Char('a');
            Check(panel.Input.Text.Length==PlayerChatProtocol.MaxInputCharacters,"input capped at 106 characters");
            built.Update(ChatGame(mixedView,gameFocus:true));
            Check(!panel.Input.Open,"game focus returned elsewhere (e.g. world click) closes the line");
            cmd.Calls.Clear();
            Check(panel.Mouse(ChatClick(129+100,470+114+4),cmd)&&panel.Input.Open&&cmd.Calls.SequenceEqual(["begin"]),"click on CHAT_BAR opens the line");
            built.Update(new UiStaticGameState());
            Check(!panel.Input.Open&&!panel.Key(enter,cmd),"leaving the world closes the line; keys ignored");
            built.Update(new UiStaticGameState(InWorld:true));
            Check(panel.Key(enter,cmd)&&!panel.Input.Open,"plain game state (no chat adapter): nothing to send, line stays closed");
            Check(panel.Mouse(ChatClick(129+150,470+60),new UiNullCommands()),"click inside the window is consumed (world does not get it)");
        }

        // A different origin: everything follows the XML.
        using(var res=ChatResources(Path.Combine(root,"moved"),ChatXml(30,400,step:20))){
            var panel=(ChatPanel)UiRoot.Build(res,[reg]).Find("CENTER_WINDOW")!;
            Check(panel.View.Heights.Steps.SequenceEqual([200,220,240])&&panel.Bounds==new Rectangle(30,400,362,200)&&panel.Components[ChatPanel.Bar]==new Point(40,514),"origin (30,400), stepSize 20 from XML: steps 200/220/240, bar at (40,514)");
        }
        // Null surfs: the missing element is not drawn and cannot be clicked; nothing replaces it.
        using(var res=ChatResources(Path.Combine(root,"null-surf"),ChatXml(129,470),noImage:[3279,2980,2273])){
            var built=UiRoot.Build(res,[reg]);var panel=(ChatPanel)built.Find("CENTER_WINDOW")!;panel.CharWidth=_=>6;
            Check(panel.Width==519&&panel.View.Heights.Steps.SequenceEqual([130]),"no 3279: width from LCHAT/RCHAT (260+259), no growth (max height unknown)");
            built.Update(ChatGame(ChatView(ChatLines(3))));
            using var c=new UiCanvas();built.Compose(c,null);
            Check(!panel.Drawn.Any(d=>d.Element is ChatPanel.Base or ChatPanel.Bar||d.Element==ChatPanel.TabPart(ChatTab.Blood,false))&&Px(c,300,470+122)==Color.Black.ToArgb(),"missing 3279 / 2980 / 2273 → not drawn, no placeholder");
            panel.Mouse(ChatClick(129+8+2,470+100+2),new UiNullCommands());
            Check(panel.View.Tab==ChatTab.General&&panel.Drawn.Count(d=>d.Element=="LINE")==3,"missing tab surf is not clickable; lines still drawn");
        }
        using(var res=ChatResources(Path.Combine(root,"no-node"),ChatXml(129,470,omit:"CHANNEL_ICON/TRADE_CHANNEL_ON"))){
            Throws<UiLayoutException>(()=>UiRoot.Build(res,[reg]),"missing TRADE_CHANNEL_ON node refuses the panel","TRADE_CHANNEL_ON");
        }
        using(var res=ChatResources(Path.Combine(root,"bad-bottom"),ChatXml(129,600))){
            Throws<UiLayoutException>(()=>UiRoot.Build(res,[reg]),"window y at/below CHAT_WINDOW yPos is refused","yPos");
        }
        // Pure rules.
        var hs=new ChatHeights(130,240,36);
        Check(hs.Cycle(130)==166&&hs.Cycle(238)==240&&hs.Cycle(240)==130&&hs.Clamp(200)==166&&hs.Grow(240)==240&&hs.Shrink(130)==130,"height rules: cycle / clamp / grow / shrink");
        Check(new ChatHeights(130,130,36).Steps.Count==1&&new ChatHeights(130,240,0).Steps.Count==1,"degenerate heights: one step");
        Check(ChatViewState.MaxScroll(5,7)==0&&ChatViewState.MaxScroll(30,7)==23,"max scroll");
    }

    /// <summary>Optional: real client (TW_CLIENT_ROOT). Writes PNGs + the component table to scratch/issue-468/real only.</summary>
    static void NativeUiChatWindowRealRender(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui chat-window real render (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-468","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var layout=res.Xml.Load("uistatus86");
        var panel=new ChatPanel();panel.Load(layout,res.Surfs);
        var root=new UiRoot(res);root.Add(panel);
        var sample=new[]{new PlayerChatLine(1,1,"測試甲: 你好"),new PlayerChatLine(2,1,"[測試乙] 喊話測試",PlayerChatChannel.Shout),new PlayerChatLine(3,1,"{測試丙} 血盟訊息",PlayerChatChannel.Clan),
            new PlayerChatLine(4,1,"(測試丁) 隊伍訊息",PlayerChatChannel.Party),new PlayerChatLine(5,1,"[測試戊] 全體訊息",PlayerChatChannel.Global),new PlayerChatLine(6,1,"(測試己) 密語",PlayerChatChannel.Whisper)}
            .Concat(ChatLines(20,first:7)).ToArray();
        var state=ChatGame(ChatView(sample));
        var surfaces=new List<object>();
        foreach(var id in new[]{3279,3280,3288,3290,2273,2274,2277,2284,2286,2288,3285,3286,3287,2290,2291,2977,2978})if(res.Surfs.Get(id) is {} s){surfaces.Add(new{id,s.FileName,s.Archive,s.Width,s.Height,s.Flags});s.Bitmap.Save(Path.Combine(outDir,$"surf-{id}.png"),ImageFormat.Png);}
        var steps=new Dictionary<string,object>();
        void Shot(string name){
            root.Update(state);
            using var c=new UiCanvas();root.Compose(c,null);
            c.Bitmap.Save(Path.Combine(outDir,$"canvas-{name}.png"),ImageFormat.Png);
            steps[name]=new{bounds=new[]{panel.Bounds.X,panel.Bounds.Y,panel.Bounds.Width,panel.Bounds.Height},visibleRows=panel.VisibleRows,
                components=panel.Components.ToDictionary(k=>k.Key,k=>new[]{k.Value.X,k.Value.Y}),
                drawn=panel.Drawn.Select(d=>new{d.Element,d.Surf,rect=new[]{d.Rect.X,d.Rect.Y,d.Rect.Width,d.Rect.Height},d.Text}).ToArray()};
        }
        foreach(var h in panel.View.Heights.Steps){panel.View.SetHeight(h);Shot($"h{h}");}
        panel.View.SetHeight(panel.View.Heights.Min);panel.View.Split=true;Shot("split-h130");panel.View.Split=false;
        panel.View.Tab=ChatTab.Blood;Shot("tab-blood");panel.View.Tab=ChatTab.General;
        panel.View.Toggle(ChatFilter.General);Shot("filter-general-off");panel.View.Toggle(ChatFilter.General);
        Check(panel.Origin==new Point(129,470)&&panel.Bottom==600,"real uistatus86-c CENTER_WINDOW at (129,470), bottom 600");
        File.WriteAllText(Path.Combine(outDir,"chat-window.json"),JsonSerializer.Serialize(new{client,xml=layout.FileName,origin=new[]{panel.Origin.X,panel.Origin.Y},panel.Bottom,panel.Width,
            heights=panel.View.Heights.Steps,surfaces,steps},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
    }
}
