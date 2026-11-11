using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R10-J（T394／#909）bookmark-followup 離線測試：書籤窗收尾（gap-matrix 15.12.6、15.12.5 的 4:BookmarkWindowUI）。
// ① 隨機傳送鈕（id 0）：AbilityStatus 接受 id 0 → C6 HD(0,0)；RingWindows 的宿主宣告 AcceptsRandomTeleport。
// ② /qbookmark：C165 放行後設旗標（this+486），下一筆新書籤（S92）加進快速移動清單。
// ③ 記憶清單拖到快速移動清單。
// 只用合成封包、記憶體內假傳送端與合成單色 surf（元素名稱與座標照 BookmarkUI.xml，視窗原點 (10,20)）；
// 不讀真實客戶端、不開遊戲、網路或視窗。字串表以 ServerMessageText.UseTable(null) 固定成「沒有表」。
static class BookmarkFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);
    static byte[] Appear(uint id,ushort x,string name)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static OwnStatus Status(int level)=>JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Me},\"Level\":{level},\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}}")!;

    static LoginConnection Seed(List<byte[]> log,params int[] skills){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",Status(44));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");
        var learned=(HashSet<int>)typeof(WorldSession).GetField("learnedMagic",F)!.GetValue(c.World)!;foreach(int s in skills)learned.Add(s);
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    // ---- 面板（同 BookmarkWindowOriginalTests 的合成版面） ----
    sealed class Host:IBookmarkWindowHost {
        public BookmarkView Bookmarks{get;set;}=View(1);
        public TeleportDestinationRequest? Destination{get;set;}
        public bool Sending{get;set;}
        public bool AcceptsRandomTeleport{get;set;}
        public readonly List<string> Calls=[];
        public void Teleport(TeleportDestinationRequest shown,uint bookmarkId)=>Calls.Add($"teleport:{shown.Revision}:{bookmarkId}");
        public void CancelTeleport(TeleportDestinationRequest shown)=>Calls.Add($"cancel:{shown.Revision}");
    }
    static readonly Bookmark[] Sample=[new(101,"Gludio",4,1,1),new(102,"Aden",0,1,1),new(103,"Kent",4,1,1),new(104,"Oren",57,1,1),new(105,"Bear",0,1,1),
        new(106,"Giran",4,1,1),new(107,"Heine",4,1,1)];
    const string ListNotice="已收到伺服器書籤清單。";
    static BookmarkView View(int revision,Bookmark[]? items=null,int session=1,uint actor=7,string notice=ListNotice)=>new(session,1,revision,actor,true,50,notice,items??Sample);
    static BookmarkView Single(int revision,Bookmark[] items,int session=1,uint actor=7)=>View(revision,items,session,actor,BookmarkWindowPanel.SingleBookmarkNotice);

    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [1]=Surface(1,263,420,0xFF808080),[2]=Surface(2,251,407,0xFF202020),[3]=Surface(3,12,12,0xFFFF0000),[6]=Surface(6,50,12,0xFF406080),[7]=Surface(7,50,12,0xFF202020),
        [8]=Surface(8,200,17,0xFF0000FF),[16]=Surface(16,60,18,0xFF00FF00),[19]=Surface(19,60,18,0xFF00FF00),[20]=Surface(20,60,18,0xFFFF0000),
        [45]=Surface(45,120,15,0xFF00FFFF),[50]=Surface(50,40,16,0xFFFFA0A0),
    };
    static string Layout(bool quick=true){
        var rows=string.Concat(Enumerable.Range(0,12).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" Caption=\"temp{i}\" PushSurf=\"8;4\" Activate=\"0\" Height=\"17\"/>"));
        var quickRows=string.Concat(Enumerable.Range(0,5).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" String=\"{i}\" PushSurf=\"45;4\" Activate=\"1\" Height=\"15\" MouseEvent=\"QuickList\"/>"));
        return "<?xml version=\"1.0\"?><BookmarkWindowUI Toggle=\"true\">"+
            "<Window Name=\"Bookmark_Window\" X=\"10\" Y=\"20\" FrameSurf=\"1;1\" Moveable=\"20\">"+
            "<Image Name=\"Base_Image\" Surf=\"2;0\" X=\"6\" Y=\"9\"/>"+
            "<Button Name=\"Exit_Button\" X=\"249\" Y=\"15\" BaseSurf=\"3\"/>"+
            "<Image Name=\"QuickMoveListTitle_Image\" Surf=\"6;1\" X=\"24\" Y=\"46\"/>"+
            "<Image Name=\"MemoryZoneListTitle_Image\" Surf=\"7;1\" X=\"24\" Y=\"145\"/>"+
            (quick?"<MovableList Name=\"QuickMove_List\" X=\"23\" Y=\"62\" Width=\"120\" Height=\"75\" Vertical=\"1\" MouseEvent=\"QuickMoveList\">"+quickRows+"</MovableList>":"")+
            "<Button Name=\"RandomTeleport_Button\" BaseSurf=\"50\" PushSurf=\"51;1\" HighlightSurf=\"52\" X=\"129\" Y=\"42\" MouseEvent=\"RandomTeleport\"/>"+
            "<MovableList Name=\"MemoryPlace_List\" X=\"22\" Y=\"162\" Width=\"200\" Height=\"204\" Vertical=\"1\">"+rows+"</MovableList>"+
            "<Button Name=\"Confirm_Button\" BaseSurf=\"16\" DisableSurf=\"19\" X=\"67\" Y=\"395\"/>"+
            "<Button Name=\"Cancel_Button\" BaseSurf=\"20\" X=\"134\" Y=\"395\"/>"+
            "<Label Name=\"BookmarkCount_Label\" X=\"200\" Y=\"397\" Width=\"20\" Height=\"15\"/>"+
            "</Window></BookmarkWindowUI>";
    }
    static BookmarkWindowPanel Panel(Host host,string? xml=null){
        var map=Surfaces();var p=new BookmarkWindowPanel();
        p.Load(UiLayout.Parse(xml??Layout(),"BookmarkUI.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);p.Update(InWorld);p.Toggle();p.Update(InWorld);
        return p;
    }
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true),OutOfWorld=new UiStaticGameState(InWorld:false);
    static UiMouseEvent M(UiMouseKind k,Point at,MouseButtons b=MouseButtons.Left,int clicks=1)=>new(k,at,b,clicks,0,Keys.None);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static void Drag(BookmarkWindowPanel p,Rectangle from,Point to,UiNullCommands c){
        p.Mouse(M(UiMouseKind.Down,Center(from)),c);p.Mouse(M(UiMouseKind.Move,to,MouseButtons.None),c);p.Mouse(M(UiMouseKind.Up,to),c);
    }
    static void Click(BookmarkWindowPanel p,Rectangle r,UiNullCommands c){var at=Center(r);p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}

    // ① 隨機傳送：AbilityStatus／RingWindows（3.81 C_UseSkill.java 5／69 讀 H（丟掉）＋D；TELEPORT.java 書籤查不到 → randomLocation(200)）。
    [WorldModuleTest("bookmark-followup-random")]
    static void Random(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log,5,69);var w=c.World;
        w.Receive(P([92],Str("家"),Le16(4),Le16(32600),Le16(32900),Le32(77)),"offline");
        w.SelectMagic(5);w.Receive(P([36,1,1]),"offline");
        t.Check(w.TryOpenRingWindow()&&w.TeleportDestination is {Skill:5},"ring: destination request opened");
        var q=w.TeleportDestination!;
        var target=w.TeleportDestinationTarget(q.Revision,BookmarkWindowPanel.RandomTeleportId);
        t.Check(target is {Kind:SkillTargetKind.Bookmark,MapId:0,BookmarkId:0},"id 0 → bookmark target map 0 + id 0 (fake bookmark: name 487, coords 0)",target);
        t.Equal("06000400000000000000000000000000",Hex(SkillCast.Request(5,target!)),"C6 skill 5 HD(map 0, bookmark 0)");
        t.Check(w.TeleportDestinationTarget(q.Revision+1,0)==null,"stale revision with id 0 refused");
        t.Check(w.TeleportDestinationTarget(q.Revision,77) is {MapId:4,BookmarkId:77},"real bookmark still uses its own map");
        t.Check(w.TeleportDestinationTarget(q.Revision,78)==null,"unknown non-zero bookmark still refused");
        w.SelectMagic(69);
        t.Check(w.TeleportDestinationTarget(q.Revision,0) is {BookmarkId:0}&&w.SelectedMagicId==5,"id 0 also restores the requested skill");
        t.Check(log.Count==0,"nothing sent by target building");
        c.CloseTeleportDestination(q.Revision);
        t.Check(w.TeleportDestinationTarget(q.Revision,0)==null,"no request → id 0 refused");

        // 產品宿主（ClientForm.BookmarkWindowHost）宣告接受 id 0 → 原生窗在選目的地模式畫隨機傳送鈕。
        var hostType=typeof(ClientForm).GetNestedType("BookmarkWindowHost",BindingFlags.NonPublic);
        t.Check(hostType!=null,"BookmarkWindowHost found");
        if(hostType!=null){
            var host=(IBookmarkWindowHost)Activator.CreateInstance(hostType,F,null,[null],null)!;
            t.Check(host.AcceptsRandomTeleport,"RingWindows host: AcceptsRandomTeleport = true");
        }
        using var noTable=ServerMessageText.UseTable(null);
        var cmd=new UiNullCommands();
        var h=new Host{AcceptsRandomTeleport=true};var p=Panel(h);h.Destination=new(21,5);p.Update(InWorld);
        t.Check(p.RandomVisible,"accepting host + destination mode → button shown");
        Click(p,p.RandomRect,cmd);
        t.Check(h.Calls.SequenceEqual(["teleport:21:0"]),"button → Teleport(request, 0)",h.Calls);
    }

    // ② /qbookmark：旗標 → 下一筆 S92 的新書籤加進快速移動清單。
    [WorldModuleTest("bookmark-followup-qbookmark")]
    static void QBookmark(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        // 聊天指令：照送 C165，提示改成會加進快速移動清單；離線沒有視窗，旗標轉交是 no-op（不丟例外）。
        var log=new List<byte[]>();using var c=Seed(log);
        var plan=c.ChatFollowupPlan(ChatFollowupCommands.Parse("/qbookmark 洞穴")!);
        t.Check(plan.Packets.Length==1&&plan.Packets[0].SequenceEqual(BookmarkPackets.Add("洞穴")),"qbookmark plan = one C165");
        t.Check(plan.Notice.Contains("快速移動清單")&&!plan.Notice.Contains("尚未"),"notice says it will join the quick list",plan.Notice);
        bool threw=false;try{plan.Admitted?.Invoke();}catch(Exception){threw=true;}
        t.Check(!threw,"admitted callback without a window does not throw");
        var bplan=c.ChatFollowupPlan(ChatFollowupCommands.Parse("/bookmark 洞穴2")!);
        t.Check(!bplan.Notice.Contains("快速移動清單"),"plain /bookmark does not mention the quick list",bplan.Notice);
        t.Check(ClientForm.OfferQuickBookmark(c.World,1,Me)==false,"no ClientForm offline → not handled");

        var h=new Host();var p=Panel(h);
        t.Check(p.RequestQuickOnNextBookmark(1,7)&&p.QuickPending,"flag set (this+486 = 1)");
        // 整份清單（S64）不算新書籤
        h.Bookmarks=View(2,[..Sample,new(108,"Silver",4,1,1)]);p.Update(InWorld);
        t.Check(p.QuickPending&&p.QuickIds.Count==0,"full list receive does not consume the flag");
        // 單筆 S92：新書籤加進去
        h.Bookmarks=Single(3,[..Sample,new(108,"Silver",4,1,1),new(109,"洞穴",4,1,1)]);p.Update(InWorld);
        t.Check(p.QuickIds.SequenceEqual([109u])&&!p.QuickPending,"S92 new bookmark → quick list, flag cleared",p.QuickIds);
        t.Equal("洞穴",string.Join(",",p.QuickRows.Select(r=>r.Name)),"quick row shows the new bookmark");
        // 旗標用掉之後，下一筆不再自動加
        h.Bookmarks=Single(4,[..Sample,new(108,"Silver",4,1,1),new(109,"洞穴",4,1,1),new(110,"X",4,1,1)]);p.Update(InWorld);
        t.Check(p.QuickIds.SequenceEqual([109u]),"next S92 without the flag is not added",p.QuickIds);
        // S92 只更新既有書籤（沒有新 id）→ 旗標保留
        p.RequestQuickOnNextBookmark(1,7);
        h.Bookmarks=Single(5,[..Sample,new(108,"Silver",4,1,1),new(109,"洞穴",4,1,1),new(110,"X2",4,1,1)]);p.Update(InWorld);
        t.Check(p.QuickPending&&p.QuickIds.Count==1,"S92 without a new id keeps the flag");
        h.Bookmarks=Single(6,[..Sample,new(108,"Silver",4,1,1),new(109,"洞穴",4,1,1),new(110,"X2",4,1,1),new(111,"Y",4,1,1)]);p.Update(InWorld);
        t.Check(p.QuickIds.SequenceEqual([109u,111u])&&!p.QuickPending,"then the next new bookmark joins",p.QuickIds);
        // 換角色 → 旗標丟掉
        p.RequestQuickOnNextBookmark(1,7);
        h.Bookmarks=View(1,actor:8);p.Update(InWorld);
        t.Check(!p.QuickPending&&p.QuickIds.Count==0,"other character → flag and quick list cleared");
        // 別的角色要的旗標不吃
        p.RequestQuickOnNextBookmark(1,7);
        h.Bookmarks=Single(2,[..Sample,new(120,"Z",4,1,1)],actor:8);p.Update(InWorld);
        t.Check(!p.QuickPending&&p.QuickIds.Count==0,"flag for another character is dropped, not applied");
        // 已滿 → 2828
        var fh=new Host();var f=Panel(fh);var cmd=new UiNullCommands();
        for(int r=0;r<5;r++){Click(f,f.RowRect(r),cmd);f.Key(new(Keys.Insert,true,false),cmd);}
        t.Check(f.QuickIds.Count==5,"5 entries");
        f.RequestQuickOnNextBookmark(1,7);
        fh.Bookmarks=Single(2,[..Sample,new(130,"Full",4,1,1)]);f.Update(InWorld);
        t.Check(f.QuickIds.Count==5&&!f.QuickIds.Contains(130u)&&f.Message=="快速移動目錄最多只能登錄5個為止。"&&!f.QuickPending,"full → 2828, flag consumed",f.Message);
        // 版面沒有快速移動清單、角色 0 → 不設
        var n=Panel(new Host(),Layout(quick:false));
        t.Check(!n.RequestQuickOnNextBookmark(1,7)&&!n.QuickPending,"no QuickMove_List → flag not set");
        t.Check(!p.RequestQuickOnNextBookmark(1,0),"actor 0 → flag not set");
        // 窗關著也能收（原版旗標在物件上，不看窗開不開）；S92 會開窗
        var ch=new Host();var cp=Panel(ch);cp.Toggle();
        t.Check(!cp.Visible&&cp.RequestQuickOnNextBookmark(1,7),"closed window still takes the flag");
        ch.Bookmarks=Single(2,[..Sample,new(140,"Closed",4,1,1)]);cp.Update(InWorld);
        t.Check(cp.Visible&&cp.QuickIds.SequenceEqual([140u]),"S92 opens the window and adds the bookmark",cp.QuickIds);
        // 離開世界再回同一角色：旗標保留
        cp.RequestQuickOnNextBookmark(1,7);cp.Update(OutOfWorld);ch.Bookmarks=View(3,[..Sample,new(140,"Closed",4,1,1)]);cp.Update(InWorld);
        t.Check(cp.QuickPending,"leaving and re-entering the world with the same character keeps the flag");
    }

    // ③ 從記憶清單拖到快速移動清單（FU9§1.1 第 5 項）。
    [WorldModuleTest("bookmark-followup-drag")]
    static void DragToQuick(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        var cmd=new UiNullCommands();var h=new Host();var p=Panel(h);
        t.Equal(new Rectangle(33,82,120,75),p.QuickListRect,"QuickMove_List (23,62) 120×75 + origin (10,20)");
        Drag(p,p.RowRect(2),Center(p.QuickListRect),cmd);
        t.Check(p.QuickIds.SequenceEqual([103u])&&p.DragRow==null,"drag row 2 → quick list",p.QuickIds);
        t.Check(h.Calls.Count==0&&p.ConfirmingDelete==null,"drag sends nothing");
        Drag(p,p.RowRect(2),new(40,150),cmd);
        t.Check(p.QuickIds.Count==1,"same bookmark not added twice (also when dropped on an empty quick row)");
        Drag(p,p.RowRect(0),new(p.QuickListRect.Right-2,p.QuickListRect.Bottom-2),cmd);
        t.Check(p.QuickIds.SequenceEqual([103u,101u]),"dropped anywhere in the list area → appended",p.QuickIds);
        // 放在清單外：不加，只是一般點選
        Drag(p,p.RowRect(3),Center(p.RowRect(5)),cmd);
        t.Check(p.QuickIds.Count==2&&p.Selected==104,"dropped outside the quick list → nothing added",p.QuickIds);
        Drag(p,p.RowRect(3),Center(p.ConfirmRect),cmd);
        t.Check(p.QuickIds.Count==2,"dropped on a button → nothing added");
        // 從快速移動清單按下（不是記憶清單）→ 不算拖曳
        p.Mouse(M(UiMouseKind.Down,Center(p.QuickRowRect(0))),cmd);
        t.Check(p.DragRow==null,"press on a quick row is not a drag source");
        p.Mouse(M(UiMouseKind.Up,Center(p.QuickRowRect(1))),cmd);
        t.Check(p.QuickIds.SequenceEqual([103u,101u]),"quick-to-quick release changes nothing",p.QuickIds);
        // 右鍵不拖
        p.Mouse(M(UiMouseKind.Down,Center(p.RowRect(4)),MouseButtons.Right),cmd);p.Mouse(M(UiMouseKind.Up,Center(p.QuickListRect),MouseButtons.Right),cmd);
        t.Check(p.QuickIds.Count==2,"right button does not drag");
        // 滿 5 筆 → 2828
        foreach(int r in new[]{3,4,5})Drag(p,p.RowRect(r),Center(p.QuickListRect),cmd);
        t.Check(p.QuickIds.Count==5&&p.Message==null,"5 entries by drag");
        Drag(p,p.RowRect(6),Center(p.QuickListRect),cmd);
        t.Check(p.QuickIds.Count==5&&p.Message=="快速移動目錄最多只能登錄5個為止。","6th drag → 2828",p.Message);
        // 已送刪除的書籤不加
        var dh=new Host{Bookmarks=View(1,[..Sample.Select(b=>b.Id==102?b with{DeletePending=true}:b)])};var d=Panel(dh);
        Drag(d,d.RowRect(1),Center(d.QuickListRect),cmd);
        t.Check(d.QuickIds.Count==0,"delete-pending bookmark is not added");
        // 選目的地模式也能拖（推測），之後雙擊快速移動清單＝傳送
        var th=new Host();var tp=Panel(th);th.Destination=new(30,5);tp.Update(InWorld);
        Drag(tp,tp.RowRect(4),Center(tp.QuickListRect),cmd);
        t.Check(tp.DestinationMode&&tp.QuickIds.SequenceEqual([105u])&&th.Calls.Count==0,"destination mode: drag adds, no teleport",tp.QuickIds);
        // 版面沒有快速移動清單：拖曳不動作、按鈕照常
        var n=Panel(new Host(),Layout(quick:false));
        Drag(n,n.RowRect(0),new(90,110),cmd);
        t.Check(n.QuickIds.Count==0&&n.QuickListRect.IsEmpty,"no QuickMove_List → drag does nothing");
        // 846 確認中不拖
        var eh=new Host();var e=Panel(eh);e.AttachEditor(new NullEditor());
        Click(e,e.RowRect(0),cmd);e.Key(new(Keys.Delete,true,false),cmd);
        t.Check(e.ConfirmingDelete==101,"846 pending");
        Drag(e,e.RowRect(1),Center(e.QuickListRect),cmd);
        t.Check(e.QuickIds.Count==0,"no drag while 846 is shown");
    }
    sealed class NullEditor:IBookmarkEditHost {
        public bool Sending=>false;
        public void Add(BookmarkView shown,string name){}
        public void Delete(BookmarkView shown,uint bookmarkId){}
    }
}
