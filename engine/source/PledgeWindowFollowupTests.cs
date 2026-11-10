using P06.NativeUi;
namespace P06;

// R4-E（T296／#708）離線測試：合成封包、合成 XML／surf、記憶體送出閘門；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
// 涵蓋：S200（op 78）C(type) C(result) 解碼與 type 0 訊息 3349／3324、C254 0x2E 顯示盟徽、WorldPledgeSource 轉送 C78 15／C63／C254、
// 原生血盟窗的關注清單側欄、公告編輯與送出、改階級彈出窗、顯示盟徽勾選框、上傳盟徽入口、自己盟徽的繪製。
static class PledgeWindowFollowupTests {
    // 合成版面：節點名稱與巢狀照 PledgeUI.xml 的結構（WIN2§3），座標與 surf 編號是測試值。
    const string Xml="""
<?xml version="1.0"?>
<PledgeUI>
  <Window Name="ChangeRank_PopupWindow" X="0" Y="0" Width="100" Height="73">
    <Button Name="Rank0_Button" X="0" Y="0" BaseSurf="900"/>
    <Button Name="Rank1_Button" X="0" Y="20" BaseSurf="901"/>
    <Button Name="Rank2_Button" X="0" Y="40" BaseSurf="902"/>
    <Button Name="Rank3_Button" X="0" Y="60" BaseSurf="903"/>
  </Window>
  <Window Name="Noticeuserinfo_Window" X="0" Y="0" BaseSurf="910" Moveable="20">
    <Button Name="Closeui_Button" X="260" Y="13" BaseSurf="911"/>
    <Control Name="Pledgename_Control" X="19" Y="25" Width="189" Height="20">
      <Emblem Name="Pledge_Emblem" Y="2" Width="20"/>
      <Label Name="Pledge_Label" X="17" Y="-2" Width="169" Height="20"/>
    </Control>
    <Control Name="Noticebutton_Control" X="183" Y="24" Width="66" Height="21">
      <Button Name="Writenotice_Button" BaseSurf="912"/>
      <Button Name="Savenotice_Button" BaseSurf="913"/>
    </Control>
    <Button Name="Pledgenotice_Button" X="16" Y="41" BaseSurf="914">
      <Button Name="Noticetri_Button" X="200" Y="0" BaseSurf="915"/>
    </Button>
    <Control Name="Notice_Control" X="21" Y="63" Width="225" Height="112">
      <MultiLabel Name="Notice_Label" Width="220" Height="109"/>
    </Control>
    <List Name="Sort_List" X="16" Y="181"><Button Name="Namesort_Button" BaseSurf="916"/></List>
    <List Name="Userinfo_List" X="21" Y="202" Vertical="1">
      <List Name="Userinfo0_List" Width="204" Height="15" Gap="1">
        <Image Name="Class_Image" Width="19"/><Label Name="Name_Label" Width="74" Height="15"/><Label Name="Rank_Label" Width="35" Height="15"/><Label Name="Memo_Label" Width="97" Height="15"/>
      </List>
      <List Name="Userinfo1_List" Width="204" Height="15" Gap="1"/>
    </List>
    <ScrollBar Name="Userinfo_Scrollbar" X="255" Y="67" ScrollStart="14" ScrollHeight="262">
      <Image Name="Background_Image" Y="14" Surf="917"/><Button Name="Up_Button" BaseSurf="918"/><Button Name="Move_Button" Y="14" BaseSurf="919"/><Button Name="Down_Button" Y="277" BaseSurf="918"/>
    </ScrollBar>
  </Window>
  <Window Name="Nonoticeuserinfo_Window" X="0" Y="0" BaseSurf="910" Moveable="20">
    <Button Name="Closeui_Button" X="260" Y="13" BaseSurf="911"/>
    <Control Name="Pledgename_Control" X="19" Y="25" Width="189" Height="20">
      <Emblem Name="Pledge_Emblem" Y="2" Width="20"/>
      <Label Name="Pledge_Label" X="17" Y="-2" Width="169" Height="20"/>
    </Control>
    <Button Name="Pledgenotice_Button" X="16" Y="41" BaseSurf="914">
      <Button Name="Noticetri_Button" X="200" Y="0" BaseSurf="915"/>
    </Button>
    <List Name="Sort_List" X="16" Y="61"><Button Name="Namesort_Button" BaseSurf="916"/></List>
    <List Name="Userinfo_List" X="21" Y="82" Vertical="1">
      <List Name="Userinfo0_List" Width="204" Height="15" Gap="1">
        <Image Name="Class_Image" Width="19"/><Label Name="Name_Label" Width="74" Height="15"/><Label Name="Rank_Label" Width="35" Height="15"/><Label Name="Memo_Label" Width="97" Height="15"/>
      </List>
      <List Name="Userinfo1_List" Width="204" Height="15" Gap="1"/>
      <List Name="Userinfo2_List" Width="204" Height="15" Gap="1"/>
    </List>
    <ScrollBar Name="Userinfo_Scrollbar" X="255" Y="67" ScrollStart="14" ScrollHeight="262">
      <Image Name="Background_Image" Y="14" Surf="917"/><Button Name="Up_Button" BaseSurf="918"/><Button Name="Move_Button" Y="14" BaseSurf="919"/><Button Name="Down_Button" Y="277" BaseSurf="918"/>
    </ScrollBar>
  </Window>
</PledgeUI>
""";
    static readonly Dictionary<int,(int w,int h)> Sizes=new(){
        [900]=(48,20),[901]=(48,20),[902]=(48,20),[903]=(48,20),[910]=(277,423),[911]=(12,12),[912]=(33,17),[913]=(33,17),
        [914]=(200,16),[915]=(12,16),[916]=(76,21),[917]=(12,263),[918]=(12,14),[919]=(12,20),
    };
    // 階級鈕 900–903 用綠色，其他用 0x204060，才分得出彈出窗有沒有畫。
    static UiSurface? Get(int surf)=>Sizes.TryGetValue(surf,out var s)?new(surf,surf+".img","synthetic",new UiImage(s.w,s.h,0,0,Enumerable.Repeat(surf is >=900 and <=903?0xFF00FF00u:0xFF204060u,s.w*s.h).ToArray())):null;
    static UiMouseEvent Click(Point p)=>new(UiMouseKind.Down,p,MouseButtons.Left,1,0,Keys.None);
    static UiMouseEvent Wheel(Point p,int delta)=>new(UiMouseKind.Wheel,p,MouseButtons.None,0,delta,Keys.None);
    static UiKeyEvent Down(Keys k)=>new(k,true,false);
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static byte[] Attention(byte type,byte result,params string[] names){
        var b=new List<byte>{200,type,result};
        if(type==2){b.AddRange(BitConverter.GetBytes((uint)names.Length));foreach(var n in names){b.AddRange(LoginPackets.TextEncoding.GetBytes(n));b.Add(0);}}
        b.Add(0);b.Add(0);   // 3.81 S_ClanAttention 的 H(0) 結尾
        return LoginPackets.Pad(b.ToArray());
    }

    sealed class FakeSource:IPledgeWindowSource {
        public bool? ShowEmblem{get;set;}
        public ClanEmblem? Emblem{get;set;}
        public bool Accept=true;
        public readonly List<string> Notices=[];public readonly List<(string Member,byte Rank)> Ranks=[];public readonly List<bool> Shows=[];public int Uploads;
        public bool SendNotice(string text){if(Accept)Notices.Add(text);return Accept;}
        public bool SetRank(string member,byte rank){if(Accept)Ranks.Add((member,rank));return Accept;}
        public bool SetShowEmblem(bool show){if(Accept)Shows.Add(show);return Accept;}
        public bool UploadEmblem(){if(Accept)Uploads++;return Accept;}
    }

    [WorldModuleTest("pledge-window-followup")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){Attention(t);Sends(t);}
        Panel(t);
    }

    // ---- 1. S200（TW16 op 78 S_PLEDGE_WATCH，SS2§3）----
    static void Attention(WorldModuleTestContext t){
        var list=ClanProtocol.DecodeAttention(Attention(2,0,"盟一","盟二"));
        t.Check(list is {Type:2,Result:0}&&list.Names.SequenceEqual(["盟一","盟二"]),"3.81 H(2) D S S H(0) = C(2) C(0) list",list);
        t.Check(ClanProtocol.DecodeAttention(Attention(1,0)) is {Type:1,Names.Length:0},"type 1 refresh");
        var noTrailer=LoginPackets.Pad([200,0,131]);   // TW16 form without the 3.81 H(0)
        t.Check(ClanProtocol.DecodeAttention(noTrailer) is {Type:0,Result:131},"type 0 without trailer accepted");
        t.Equal((ushort?)3349,ClanProtocol.AttentionMessage(new(0,[],131)),"type 0 result 131 → 3349");
        t.Equal((ushort?)3349,ClanProtocol.AttentionMessage(new(0,[],129)),"type 0 result 129 → 3349");
        t.Equal((ushort?)3324,ClanProtocol.AttentionMessage(new(0,[],138)),"type 0 result 138 → 3324");
        t.Equal((ushort?)null,ClanProtocol.AttentionMessage(new(0,[],0)),"type 0 result 0 → no message");
        t.Equal((ushort?)null,ClanProtocol.AttentionMessage(new(2,[],131)),"type 2 never prints 3349");
        t.Throws<ProtocolException>(()=>ClanProtocol.DecodeAttention(LoginPackets.Pad([200,0,131,0,1])),"nonzero trailer rejected");
        t.Throws<ProtocolException>(()=>ClanProtocol.DecodeAttention([200,0,131,0,0,0,0,0,0,0,0,0,0,0,0,0]),"over-long zero tail rejected");

        var s=new WorldSession();
        s.Receive(Attention(2,0,"盟一","盟二"),"");
        t.Check(s.ClanAttentionNames.SequenceEqual(["盟一","盟二"]),"S200 type 2 result 0 stores the list");
        s.Receive(Attention(2,1,"別盟"),"");
        t.Check(s.ClanAttentionNames.SequenceEqual(["盟一","盟二"]),"type 2 with result ≠ 0 keeps the list (sub_C146E0 rebuilds only on 0)");
        int lines=s.PlayerChatLines.Length;
        s.Receive(noTrailer,"");
        t.Check(s.PlayerChatLines.Length==lines+1&&s.PlayerChatLines[^1].Text=="原服訊息 3349"&&s.ClanAttentionMessage==3349,"type 0 / 131 → chat message 3349 (no table → id text)",s.PlayerChatLines.LastOrDefault());
        s.Receive(Attention(0,138),"");
        t.Check(s.PlayerChatLines[^1].Text=="原服訊息 3324"&&s.ClanAttentionMessage==3324,"type 0 / 138 → chat message 3324");
        lines=s.PlayerChatLines.Length;
        s.Receive(Attention(0,5),"");s.Receive(Attention(1,0),"");
        t.Check(s.PlayerChatLines.Length==lines&&s.ClanAttentionNames.Length==2,"other results and type 1: no message, list unchanged");
    }

    // ---- 2. 送出：C254 0x2E、WorldPledgeSource → C78 15／C63／C254 ----
    static void Sends(WorldModuleTestContext t){
        t.Check(ClanEmblemStatusPackets.EncodeShow(true).SequenceEqual(LoginPackets.Pad([254,0x2e,1]))&&ClanEmblemStatusPackets.EncodeShow(false).SequenceEqual(LoginPackets.Pad([254,0x2e,0])),"C254 C(0x2E) C(0/1)");
        var sent=new List<byte[]>();var c=new LoginConnection();c.SeedClanFollowupTest("紅血盟",sent);
        t.Check(Wait(c.SetClanShowEmblem(c.ClanSnapshot(),true))&&sent.Count==1&&sent[0].SequenceEqual(ClanEmblemStatusPackets.EncodeShow(true)),"SetClanShowEmblem sends one C254");
        t.Check(c.ClanSnapshot().Notice.Contains("開啟顯示盟徽請求已送出"),"show-emblem notice",c.ClanSnapshot().Notice);
        var none=new List<byte[]>();var n=new LoginConnection();n.SeedClanFollowupTest("",none);
        t.Check(!Wait(n.SetClanShowEmblem(n.ClanSnapshot(),true))&&none.Count==0,"no clan: C254 not sent");

        int uploads=0;var src=new WorldPledgeSource(c,()=>{uploads++;return true;});sent.Clear();
        t.Check(src.SendNotice("今晚攻城")&&sent.Count==1&&sent[0].SequenceEqual(ClanMatchingProtocol.Announce("今晚攻城")),"source: notice → C78 C(15) S");
        t.Check(src.SetRank("成員",8)&&sent.Count==2&&sent[1].SequenceEqual(ClanProtocol.EncodeRank(8,"成員")),"source: rank → C63 C(1) C(8) S");
        t.Check(src.SetShowEmblem(false)&&sent.Count==3&&sent[2].SequenceEqual(ClanEmblemStatusPackets.EncodeShow(false)),"source: show emblem → C254 0x2E 0");
        t.Check(src.UploadEmblem()&&uploads==1&&sent.Count==3,"source: upload goes to the dialog callback, nothing sent directly");
        t.Equal((bool?)null,src.ShowEmblem,"show flag unknown before S250/173");
        c.World.Receive(LoginPackets.Pad([250,173,1,1,0,0,0,0]),"");
        t.Equal((bool?)true,src.ShowEmblem,"show flag follows S250/173");
        var pixels=Enumerable.Repeat(unchecked((int)0xFFFF0000),192).ToArray();
        c.World.Emblems.Store(new(9,pixels));
        t.Check(src.Emblem is {Id:9} e&&e.Pixels.SequenceEqual(pixels),"source: own emblem by clan id");
        var nsrc=new WorldPledgeSource(n,()=>true);
        t.Check(!nsrc.SendNotice("x")&&!nsrc.SetRank("成員",8)&&!nsrc.SetShowEmblem(true)&&none.Count==0&&nsrc.Emblem==null,"source without clan: nothing sent, no emblem");
    }

    // ---- 3. 原生血盟窗 ----
    static void Panel(WorldModuleTestContext t){
        t.Check(typeof(IUiTextInput).IsAssignableFrom(typeof(PledgePanel))&&typeof(IUiClosablePanel).IsAssignableFrom(typeof(PledgePanel)),"PledgePanel takes typed characters and is closable");
        var layout=UiLayout.Parse(Xml,"PledgeUI.xml");
        var p=new PledgePanel();p.Load(layout,Get);
        var attention=Enumerable.Range(1,10).Select(i=>"關注"+i).ToArray();
        var data=new PledgeWindowState(1,1,"紅血盟",["Alice","Bob"],new Dictionary<string,string>(),"舊公告");
        IUiGameState World(PledgeWindowState d)=>new UiPledgeGameState(new UiStaticGameState(true),d,attention);
        p.Update(World(data));
        var cmd=new UiNullCommands();
        t.Check(p.Visible&&!p.ShowingNotice,"171 opens the plain window");
        t.Equal(4,p.RankButtons.Count,"ChangeRank_PopupWindow: 4 rank buttons");
        t.Equal(new Rectangle(0,0,277,423),p.WindowBounds,"window bounds");
        t.Equal(new Rectangle(277,0,PledgePanel.SideWidth,PledgePanel.SideHeight),p.SideRect,"side box right of the window");
        t.Equal(new Rectangle(0,0,277+PledgePanel.SideWidth,423),p.Bounds,"bounds = window ∪ side box");
        t.Equal(new Rectangle(19,27,16,12),p.EmblemRect,"Pledge_Emblem = control (19,25) + (0,2), 16×12");
        t.Equal(Rectangle.Empty,p.NoticeEditRect,"plain window has no notice write button");

        // 關注清單：7 列，(10 − 7) / 2 = 1 的捲動上限；側欄的箭頭與滾輪捲關注清單，窗內滾輪捲成員。
        t.Equal(7,p.AttentionPage.Count,"7 attention rows");
        t.Check(p.Mouse(Click(Center(p.AttentionDownRect)),cmd)&&p.AttentionScroll==1&&p.AttentionPage[0]=="關注2","down arrow scrolls attention");
        p.Mouse(Click(Center(p.AttentionDownRect)),cmd);t.Equal(1,p.AttentionScroll,"attention scroll clamps at (n−7)/2");
        t.Check(p.Mouse(Wheel(Center(p.AttentionRow(3)),120),cmd)&&p.AttentionScroll==0,"wheel up over the side box scrolls attention");
        p.Mouse(Wheel(new(100,300),-120),cmd);
        t.Check(p.AttentionScroll==0&&p.Scroll==0,"wheel over the window scrolls members (2 members, 3 rows: nothing to scroll), not attention");
        t.Check(p.Mouse(Click(Center(p.AttentionUpRect)),cmd)&&p.AttentionScroll==0,"up arrow at top stays at 0");

        int Count(Rectangle area,Color color){
            using var c=new UiCanvas();c.Clear(Color.Black);c.Restrict(p.Bounds);p.Paint(c);c.Release();
            int k=0;for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(c.Bitmap.GetPixel(x,y).ToArgb()==color.ToArgb())k++;
            return k;
        }
        t.Check(Count(p.AttentionRow(0),Color.White)>0,"attention name painted in row 0");
        t.Check(Count(new(p.SideRect.Right-3,p.SideRect.Bottom-3,1,1),Color.Black)==0,"side box background painted");

        // 顯示盟徽與上傳：沒有 Source 不送，只寫提示。
        t.Check(p.Mouse(Click(Center(p.ShowEmblemRect)),cmd)&&p.Status.Contains("沒有接上"),"no source: check box only reports");
        var src=new FakeSource();p.Source=src;
        p.Mouse(Click(Center(p.ShowEmblemRect)),cmd);
        t.Check(src.Shows.SequenceEqual([true]),"unknown flag → click asks to show");
        src.ShowEmblem=true;p.Mouse(Click(Center(p.ShowEmblemRect)),cmd);
        t.Check(src.Shows.SequenceEqual([true,false])&&p.Status.Contains("關閉"),"shown → click asks to hide",p.Status);
        t.Check(p.Mouse(Click(Center(p.UploadRect)),cmd)&&src.Uploads==1,"upload button calls the source");
        int marked=Count(p.ShowEmblemRect,Color.White);src.ShowEmblem=false;int unmarked=Count(p.ShowEmblemRect,Color.White);
        t.Check(marked>unmarked,"check mark painted only when the flag is on",new{marked,unmarked});

        // 自己的盟徽畫在 Pledge_Emblem。
        src.Emblem=new(9,Enumerable.Repeat(unchecked((int)0xFFFF0000),192).ToArray());
        t.Equal(16*12,Count(p.EmblemRect,Color.Red),"own emblem drawn 16×12 at Pledge_Emblem");
        src.Emblem=null;t.Equal(0,Count(p.EmblemRect,Color.Red),"no emblem → nothing drawn");

        // 改階級：點成員列的階級欄開彈出窗（Rank0–3 → 8／7／9／3），點階級鈕送出；點別處或 Esc 只關彈出窗。
        var rank0=p.Cell(0,"Rank_Label");
        t.Equal(new Rectangle(21+20+75,82,35,15),rank0,"rank column of row 0");
        t.Check(p.Mouse(Click(Center(rank0)),cmd)&&p.RankPopupMember=="Alice","rank label opens the popup for that member");
        t.Equal(new Rectangle(rank0.X,rank0.Bottom,48,20),p.RankButtonRect(0),"popup under the rank cell");
        t.Equal(new Rectangle(rank0.X,rank0.Bottom+60,48,20),p.RankButtonRect(3),"Rank3 at popup (0,60)");
        t.Equal(48*20,Count(p.RankButtonRect(1),Color.Lime),"popup buttons painted while open");
        t.Check(p.Mouse(Click(Center(p.RankButtonRect(3))),cmd)&&src.Ranks.SequenceEqual([("Alice",(byte)3)])&&p.RankPopupMember==null,"Rank3 → C63 rank 3 for Alice, popup closes");
        p.Mouse(Click(Center(p.Cell(1,"Rank_Label"))),cmd);
        t.Equal("Bob",p.RankPopupMember,"row 1 → Bob");
        t.Check(p.Mouse(Click(new(30,300)),cmd)&&p.RankPopupMember==null&&src.Ranks.Count==1,"click elsewhere closes the popup without sending");
        p.Mouse(Click(Center(p.Cell(1,"Rank_Label"))),cmd);
        t.Check(p.Key(Down(Keys.Escape),cmd)&&p.RankPopupMember==null&&p.Visible,"Esc closes the popup first, window stays");
        p.Mouse(Click(Center(p.Cell(2,"Rank_Label"))),cmd);
        t.Equal((string?)null,p.RankPopupMember,"empty row opens nothing");
        p.Mouse(Click(Center(p.Cell(0,"Rank_Label"))),cmd);
        t.Equal(PledgePanel.PopupRanks[0],(byte)8,"Rank0 (修習) → 8 (推測)");
        p.Mouse(Click(Center(p.RankButtonRect(0))),cmd);
        t.Check(src.Ranks[^1]==("Alice",(byte)8),"Rank0 → rank 8");

        // 公告：換到公告窗，「公告」鈕開始編輯（草稿＝目前公告），打字、Backspace、Enter 送 C78 15。
        p.Mouse(Click(new(20,45)),cmd);
        t.Check(p.ShowingNotice,"switched to the notice window");
        t.Equal(new Rectangle(183,24,33,17),p.NoticeEditRect,"write button = Noticebutton_Control (183,24)");
        t.Check(!p.Char('a'),"typing ignored while not editing");
        t.Check(p.Mouse(Click(Center(p.NoticeEditRect)),cmd)&&p.EditingNotice&&p.NoticeDraft=="舊公告","write button starts editing with the current notice");
        t.Check(p.Key(Down(Keys.Back),cmd)&&p.NoticeDraft=="舊公","Backspace deletes");
        t.Check(p.Char('新')&&p.NoticeDraft=="舊公新","typed character appended");
        t.Check(!p.Char('\r')&&!p.Char('\n'),"control characters are not taken");
        t.Check(p.Char('\uD83D')&&p.NoticeDraft=="舊公新","surrogate consumed but not stored (3.81 rejects it)");
        t.Check(!p.Key(Down(Keys.A),cmd),"printable keys pass through to WM_CHAR");
        t.Check(Count(p.NoticeTextRect,PledgePanel.EditColor)>0,"draft painted in the edit colour");
        t.Check(p.Key(Down(Keys.Enter),cmd)&&!p.EditingNotice&&src.Notices.SequenceEqual(["舊公新"])&&p.Status.Contains("已送出血盟公告"),"Enter saves: C78 15 sent, editing ends",p.Status);
        t.Equal("舊公告",p.Notice,"local notice waits for the server's 168");

        p.Mouse(Click(Center(p.NoticeEditRect)),cmd);
        for(int i=0;i<200;i++)p.Char('字');
        t.Equal(PledgePanel.NoticeLimit,p.NoticeDraft.Length,"draft limited to 160 characters");
        src.Accept=false;
        t.Check(p.Mouse(Click(Center(p.NoticeEditRect)),cmd)&&p.EditingNotice&&src.Notices.Count==1&&p.Status.Contains("目前不能送出"),"save button: refused by the source → still editing");
        t.Check(p.Key(Down(Keys.Escape),cmd)&&!p.EditingNotice&&p.Visible,"Esc cancels editing, window stays");
        src.Accept=true;
        p.Mouse(Click(Center(p.NoticeEditRect)),cmd);
        p.Mouse(Click(new(20,45)),cmd);
        t.Check(!p.ShowingNotice&&!p.EditingNotice,"switching window drops the draft");
        p.Mouse(Click(new(20,45)),cmd);p.Mouse(Click(Center(p.NoticeEditRect)),cmd);
        p.Source=null;
        t.Check(!p.SaveNotice()&&p.Status.Contains("沒有接上")&&p.EditingNotice,"no source: not sent, still editing");
        p.Close();
        t.Check(!p.Visible&&!p.EditingNotice,"close ends editing");
        t.Check(!p.Char('x')&&!p.Key(Down(Keys.Back),cmd),"closed window takes no input");
        p.Update(World(data with{OpenRevision=2}));
        t.Check(p.Visible&&!p.EditingNotice&&p.Status.Length==0,"next 171 reopens clean");

        // 版面：Noticebutton_Control 只有一個鈕或沒有 ChangeRank 鈕時，沒有該入口（WinForms 血盟窗仍有）。
        var partial=new PledgePanel();
        partial.Load(UiLayout.Parse(Xml.Replace("<Button Name=\"Savenotice_Button\" BaseSurf=\"913\"/>","").Replace("Name=\"Rank0_Button\"","Name=\"RankX_Button\""),"PledgeUI.xml"),Get);
        partial.Update(World(data));partial.Mouse(Click(new(20,45)),cmd);
        t.Check(partial.ShowingNotice&&partial.NoticeEditRect.IsEmpty&&!partial.BeginNoticeEdit(),"write without save button: no editing");
        t.Equal(0,partial.RankButtons.Count,"missing Rank0_Button: no rank popup");
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse(Xml.Replace("<Button Name=\"Rank1_Button\"","<Button Name=\"Rank0_Button\""),"PledgeUI.xml"),Get),"duplicate rank button rejected");
    }
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
}
