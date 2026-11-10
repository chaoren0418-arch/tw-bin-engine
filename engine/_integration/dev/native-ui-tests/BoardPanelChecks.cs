using P06;
using P06.NativeUi;

// M7-U native board windows (Issue #528). Offline only: synthetic archives under scratch/issue-528/, no real client,
// no window, no connection. Surf sizes copy the real images' width/height only (no pixels).
partial class Checks {
    sealed class FakeBoardHost:IBoardPanelHost {
        public BoardView? Board{get;set;}
        public List<string> Pressed{get;}=[];
        public void Press(string command)=>Pressed.Add(command);
    }
    static BoardView FakeBoard(string kind,BoardArticle? article=null)=>new(1,1,1,1,1,456,kind,"",[new(20,"作者","2026/11/09","文章")],article,300,true,false,false);

    [CheckGroup("board",Order=2),CheckGroup("map-continuity",Order=60),CheckGroup(CheckGroups.Default,Order=90)]
    static void NativeUiBoardChecks(string repo){
        // --- WIN§2 T130 data, verbatim.
        Check(BoardWindowLayout.List.Select(b=>(b.Surf,b.X,b.Y)).SequenceEqual(new[]{(658,209,305),(660,34,332),(662,139,304),(664,171,304),(666,64,311),(668,99,311)}),"board list window: 6 buttons, surf and offsets = WIN§2 T130");
        Check(BoardWindowLayout.Read.Select(b=>(b.Surf,b.X,b.Y)).SequenceEqual(new[]{(672,56,312),(676,110,312),(678,213,297),(674,90,299)}),"board read window: 4 buttons, surf and offsets = WIN§2 T130");

        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-528","synthetic");
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        // Real widths/heights of the button images; 664 is left out on purpose (missing surf → not drawn, not hit).
        var sizes=new Dictionary<int,(int W,int H)>{[656]=(238,350),[658]=(17,17),[660]=(23,13),[662]=(31,26),[666]=(35,23),[668]=(35,22),[670]=(238,343),[672]=(34,18),[674]=(20,29),[676]=(33,17),[678]=(15,17)};
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(sizes.Keys.Append(664).Select(id=>$"<surf id=\"{id}\" file=\"{id}\"/>"))+"</SurfList>";
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,(w,h)) in sizes){string name=id+".img";items[1+SurfCatalog.NameHash(name)].Add((name,Img(w,h,0,0,0x7C00)));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        using var res=new UiResources(dir,tilePin,spritePins);

        var host=new FakeBoardHost();var set=new BoardPanelSet(host);set.LoadSurfaces(res.Surfs);
        var list=set.List;var read=set.Read;var o=BoardWindowLayout.DefaultOrigin;
        Check(list.Id=="BoardListWindow"&&read.Id=="BoardReadWindow"&&set.Write.Id=="BoardWriteWindow"&&set.Panels.All(p=>p.Layer==UiLayer.Window),"board panel ids are the original class names, window layer");
        Check(list.ButtonRect(0)==new Rectangle(o.X+209,o.Y+305,17,17)&&list.ButtonRect(1)==new Rectangle(o.X+34,o.Y+332,23,13),"board list buttons at origin + WIN§2 offset, sized by their surface");
        Check(list.ButtonRect(3).IsEmpty,"missing surf 664: no rectangle (not drawn, not hit)");
        Check(list.Bounds==Rectangle.FromLTRB(o.X+34,o.Y+304,o.X+226,o.Y+345),"board list bounds = union of loaded buttons only (no inferred background)");
        Check(read.Bounds==Rectangle.FromLTRB(o.X+56,o.Y+297,o.X+228,o.Y+330),"board read bounds = union of its 4 buttons");

        // --- which window is up.
        var state=new UiStaticGameState(true);
        void Sync(){foreach(var p in set.Panels)p.Update(state);}
        Sync();Check(set.Mode=="none"&&!list.Visible&&!read.Visible,"no board → no board window");
        host.Board=FakeBoard("list-wait");Sync();Check(list.Visible&&!read.Visible,"waiting for the list → list window");
        host.Board=FakeBoard("list");Sync();Check(list.Visible&&!read.Visible&&!set.Write.Visible,"list → list window only; write window never visible");
        var a20=new BoardArticle(20,"作者","2026/11/09","標題","內容");
        host.Board=FakeBoard("article",a20);Sync();Check(read.Visible&&!list.Visible,"article → read window");

        // --- painting: verified buttons only; inferred background and missing surf are not drawn.
        var ui=new UiRoot();foreach(var p in set.Panels)ui.Add(p);
        using(var canvas=new UiCanvas()){
            host.Board=FakeBoard("list");ui.Update(state);ui.Compose(canvas,null);
            int red=Color.FromArgb(255,255,0,0).ToArgb(),black=Color.Black.ToArgb();
            Check(canvas.Bitmap.GetPixel(o.X+209,o.Y+305).ToArgb()==red&&canvas.Bitmap.GetPixel(o.X+225,o.Y+321).ToArgb()==red,"button 658 drawn 1:1 at its offset");
            Check(canvas.Bitmap.GetPixel(o.X+40,o.Y+310).ToArgb()==black&&canvas.Bitmap.GetPixel(o.X,o.Y).ToArgb()==black&&canvas.Bitmap.GetPixel(o.X+120,o.Y+150).ToArgb()==black,"no background (656 is inferred) and nothing between buttons");
            Check(canvas.Bitmap.GetPixel(o.X+175,o.Y+310).ToArgb()==black,"missing surf 664 draws nothing (no placeholder)");
            Check(canvas.Bitmap.GetPixel(o.X+60,o.Y+315).ToArgb()==black,"read window hidden while the list is up");
        }

        // --- clicks → existing board actions.
        var cmds=new UiNullCommands();
        Point Mid(BoardButtonWindow w,int i){var r=w.ButtonRect(i);return new(r.X+r.Width/2,r.Y+r.Height/2);}
        bool Click(BoardButtonWindow w,Point down,Point? up=null,MouseButtons b=MouseButtons.Left){
            bool d=w.Mouse(new(UiMouseKind.Down,down,b,1,0,Keys.None),cmds);w.Mouse(new(UiMouseKind.Up,up??down,b,1,0,Keys.None),cmds);return d;
        }
        host.Board=FakeBoard("list");Sync();host.Pressed.Clear();
        foreach(int i in new[]{0,1,2,4,5})Click(list,Mid(list,i));
        Check(host.Pressed.SequenceEqual(new[]{"close","read","write","previous","next"}),"list buttons → close/read/write/previous/next");
        host.Pressed.Clear();Click(list,Mid(list,1),Mid(list,2));
        Check(host.Pressed.Count==0,"press on one button, release on another → nothing");
        Check(!Click(list,Mid(list,1),b:MouseButtons.Right)&&host.Pressed.Count==0,"right button is not handled");
        Check(Click(list,new(o.X+40,o.Y+310))&&host.Pressed.Count==0,"press inside bounds between buttons is swallowed, no action");

        // R9-N (#858, followups-9 §3.2): 672 previous / 676 next article. Rows are newest first; ids 31/30/29.
        BoardView Rows3(uint article,bool canNext=false)=>new(1,1,1,1,1,456,"article","",[new(31,"作者","2026/11/09","甲"),new(30,"作者","2026/11/09","乙"),new(29,"作者","2026/11/09","丙")],new BoardArticle(article,"作者","2026/11/09","標題","內容"),300,true,false,canNext);
        host.Board=Rows3(30);Sync();host.Pressed.Clear();
        Click(read,Mid(read,0));Click(read,Mid(read,1));
        Check(host.Pressed.SequenceEqual(new[]{"article-previous","article-next"})&&read.Visible,"read-window 672/676 on a middle row → article-previous / article-next (R9-N)");
        host.Board=Rows3(31);Sync();host.Pressed.Clear();Click(read,Mid(read,0));Sync();
        Check(host.Pressed.Count==0&&list.Visible&&!read.Visible,"672 on the first row closes the read window locally (R9-N), no packet");
        host.Board=Rows3(29,canNext:true);Sync();host.Pressed.Clear();Click(read,Mid(read,1));
        Check(host.Pressed.SequenceEqual(new[]{"article-next"})&&read.Visible,"676 on the last row with a next page → article-next (loads the next page, R9-N)");
        host.Pressed.Clear();host.Board=Rows3(29);Sync();Click(read,Mid(read,1));Sync();
        Check(host.Pressed.Count==0&&list.Visible&&!read.Visible,"676 on the last row, no next page → closes the read window locally (R9-N), no packet");
        host.Board=Rows3(30) with{CanAct=false};Sync();host.Pressed.Clear();Click(read,Mid(read,0));Click(read,Mid(read,1));
        Check(host.Pressed.Count==0&&read.Visible,"busy → 672/676 do nothing (R9-N)");

        host.Board=FakeBoard("article",a20);Sync();host.Pressed.Clear();
        Click(read,Mid(read,2));Sync();Check(host.Pressed.Count==0&&list.Visible&&!read.Visible,"read window X (678) closes only the read window (R7-F, sub_A59AB0), no packet");
        host.Pressed.Clear();Click(read,Mid(read,3));Sync();
        Check(host.Pressed.Count==0&&list.Visible&&!read.Visible,"read window back-to-list is a local switch, no packet");
        host.Board=FakeBoard("article",a20);Sync();Check(list.Visible,"same article stays behind the list");
        host.Board=FakeBoard("article",a20 with{Id=19});Sync();Check(read.Visible&&!list.Visible,"a newly read article brings the read window back");
        host.Board=FakeBoard("closed");Sync();Check(set.Mode=="none"&&!list.Visible&&!read.Visible,"closed board → no window");
        host.Board=FakeBoard("list");Sync();host.Pressed.Clear();
        Click(read,Mid(read,2));Check(host.Pressed.Count==0,"a hidden window does not act");

        // --- through UiInput (800×600, 1×): panel route, the world never sees the click.
        {
            var input=new UiInput(ui,cmds);host.Pressed.Clear();ui.Update(state);
            var p=Mid(list,0);
            var down=input.Mouse(UiMouseKind.Down,p,MouseButtons.Left,1);var up=input.Mouse(UiMouseKind.Up,p,MouseButtons.Left,1);
            Check(down.Route==UiRoute.Panel&&up.Route==UiRoute.Panel&&host.Pressed.SequenceEqual(new[]{"close"}),"UiInput: board button click goes to the panel, not the world");
            Check(input.Mouse(UiMouseKind.Down,new(o.X,o.Y),MouseButtons.Left,1).Route==UiRoute.World,"outside the buttons (inferred background area) → world");
            input.Mouse(UiMouseKind.Up,new(o.X,o.Y),MouseButtons.Left,1);
        }
    }
}
