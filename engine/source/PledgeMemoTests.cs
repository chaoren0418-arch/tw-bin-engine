using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R7-K（T350／#813）離線測試：合成 XML／surf、合成封包、記憶體內假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：C78 16 的線上格式（ClanMatchingProtocol.Notes：C(78) C(16) S(備註)）；備註上限＝S250/169 的 62 byte；
// 原生血盟窗 ModifyMemo_Button（WIN2§3，MouseEvent="ModifyMemo"）開始編輯／Enter 或再按一次送出／Esc 取消；
// 送出後不先改本機備註（等 169）；產品端 WorldPledgeSource.SendMemo 經 LoginConnection.SendClanContent 送出、沒有血盟不送。
static class PledgeMemoTests {
    const string Xml="""
<?xml version="1.0"?>
<PledgeUI Toggle="true">
  <Window Name = "Noticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4512;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y="-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "ModifyMemo_Button" X = "217" Y = "24" BaseSurf = "4452" MouseEvent = "ModifyMemo"/>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4456"/>
    </Button>
    <Control Name = "Notice_Control" X = "21" Y = "63" Width = "225" Height = "112">
      <MultiLabel Name = "Notice_Label" Width = "220" Height = "109"/>
    </Control>
    <List Name = "Sort_List" X = "16" Y = "181" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "202" Width = "214" Height = "222" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
  <Window Name = "Nonoticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4450;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y = "-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "ModifyMemo_Button" X = "217" Y = "24" BaseSurf = "4452" MouseEvent = "ModifyMemo"/>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4494"/>
    </Button>
    <List Name = "Sort_List" X = "16" Y = "61" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "82" Width = "204" Height = "339" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
      <List Name = "Userinfo1_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
      <List Name = "Userinfo2_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
</PledgeUI>
""";

    static UiSurface Img(int surf,int w,int h)=>new(surf,surf+".img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF204060u,w*h).ToArray()));
    static readonly Dictionary<int,(int w,int h)> Sizes=new(){
        [4450]=(277,423),[4512]=(277,423),[4429]=(12,12),[4452]=(32,21),[4455]=(200,16),[4456]=(12,16),[4494]=(12,16),
        [4458]=(20,21),[4443]=(12,263),[4446]=(12,14),[4448]=(12,20),[4444]=(12,14),
    };
    static UiSurface? Get(int surf)=>Sizes.TryGetValue(surf,out var s)?Img(surf,s.w,s.h):null;
    static UiMouseEvent Click(Point p)=>new(UiMouseKind.Down,p,MouseButtons.Left,1,0,Keys.None);
    static UiKeyEvent Key(Keys k)=>new(k,true,false);
    static IUiGameState World(PledgeWindowState p,string? me)=>new UiPledgeGameState(new UiStaticGameState(InWorld:true,CharacterName:me),p);
    static PledgeWindowState Data(long open,params string[] online)=>new(open,open,"紅血盟",online,
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["Bob"]="補師"});

    /// <summary>只記錄 SendMemo 的假來源；Accept=false 模擬本機條件不合。</summary>
    sealed class FakeSource:IPledgeWindowSource {
        public readonly List<string> Memos=[];
        public bool Accept=true;
        public bool? ShowEmblem=>null;
        public ClanEmblem? Emblem=>null;
        public bool SendNotice(string text)=>false;
        public bool SetRank(string member,byte rank)=>false;
        public bool SetShowEmblem(bool show)=>false;
        public bool UploadEmblem()=>false;
        public bool SendMemo(string text){if(!Accept)return false;Memos.Add(text);return true;}
    }

    [WorldModuleTest("pledge-memo")]
    static void Run(WorldModuleTestContext t){Wire(t);Panel(t);Product(t);}

    static void Wire(WorldModuleTestContext t){
        var big5=LoginPackets.TextEncoding.GetBytes("守城");
        var p=ClanMatchingProtocol.Notes("守城");
        t.Equal("4E10",Convert.ToHexString(p,0,2),"C78 16 header");
        t.Equal(Convert.ToHexString(LoginPackets.Pad([78,16,..big5,0])),Convert.ToHexString(p),"C78 16 S(備註) NUL-terminated, padded");
        t.Equal(Convert.ToHexString(LoginPackets.Pad([78,16,0])),Convert.ToHexString(ClanMatchingProtocol.Notes("")),"empty memo allowed (clears)");
        t.Equal(62,PledgePanel.MemoByteLimit,"limit = S250/169 fixed 62 bytes");
        t.Equal((string?)null,PledgePanel.MemoError(new string('甲',31)),"31 CJK chars = 62 bytes ok");
        t.Check(PledgePanel.MemoError(new string('甲',32)) is not null,"32 CJK chars = 64 bytes rejected");
        t.Check(PledgePanel.MemoError(new string('a',63)) is not null,"63 ASCII rejected");
        t.Check(PledgePanel.MemoError("a\tb") is not null,"control char rejected");
        t.Equal((string?)null,PledgePanel.MemoError(""),"empty ok");
        // 62 byte 剛好能經 169 回送（PledgeProtocol.EncodeMemo 會擋 >62）。
        var full=new string('甲',31);
        t.Equal(full,PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeMemo("Bob",full)).Memo,"62-byte memo round-trips through 169");
    }

    static void Panel(WorldModuleTestContext t){
        var layout=UiLayout.Parse(Xml,"PledgeUI.xml");var cmd=new UiNullCommands();
        var p=new PledgePanel();p.Load(layout,Get);
        p.Update(World(Data(1,"Alice","Bob"),"bob"));
        t.Check(p.Visible,"171 opens");
        t.Equal(new Rectangle(217,24,32,21),p.ModifyMemoRect,"ModifyMemo_Button at XML (217,24)");
        // 沒有 Source：不進編輯。
        t.Check(!p.BeginMemoEdit()&&!p.EditingMemo,"no source → no edit");
        t.Check(p.Status.Contains("沒有接上"),"status explains",p.Status);
        var src=new FakeSource();p.Source=src;
        // 點鈕：以自己的備註當草稿。
        t.Check(p.Mouse(Click(new(220,28)),cmd)&&p.EditingMemo,"click ModifyMemo starts editing");
        t.Equal("補師",p.MemoDraft,"draft = own current memo (case-insensitive name)");
        t.Equal(1,p.OwnIndex,"own row index");
        t.Equal(p.Cell(1,"Memo_Label"),p.OwnMemoRect,"own memo cell visible");
        t.Check(p.Key(Key(Keys.Back),cmd)&&p.MemoDraft=="補","backspace");
        t.Check(p.Char('甲')&&p.MemoDraft=="補甲","char appended");
        t.Check(!p.Char('\r'),"control char not taken");
        // 草稿畫在自己那列的備註欄（編輯色）。
        using(var c=new UiCanvas()){
            c.Clear(Color.Black);c.Restrict(p.Bounds);p.Paint(c);c.Release();
            var r=p.OwnMemoRect;int n=0;
            for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++)if(c.Bitmap.GetPixel(x,y).ToArgb()==PledgePanel.EditColor.ToArgb())n++;
            t.Check(n>0,"draft painted in own memo cell with edit color",n);
        }
        // Enter 送出：只呼叫一次 SendMemo，本機備註不變（等 169）。
        t.Check(p.Key(Key(Keys.Enter),cmd)&&!p.EditingMemo,"enter sends and ends editing");
        t.Equal(1,src.Memos.Count,"one C78 16");
        t.Equal("補甲",src.Memos[0],"sent text");
        t.Check(p.Status.Contains("已送出成員備註"),"status sent",p.Status);
        t.Equal(0,cmd.Calls.Count,"no UI commands");
        // 再按一次鈕＝送出。
        p.Mouse(Click(new(220,28)),cmd);t.Check(p.EditingMemo,"second edit");
        p.Mouse(Click(new(220,28)),cmd);t.Check(!p.EditingMemo&&src.Memos.Count==2&&src.Memos[1]=="補師","clicking the button again sends",src.Memos);
        // 來源拒絕：保持編輯、不清草稿。
        src.Accept=false;p.BeginMemoEdit();p.Char('x');
        t.Check(!p.SaveMemo()&&p.EditingMemo&&p.MemoDraft=="補師x","source refuses → still editing",p.Status);
        src.Accept=true;
        // Esc 取消，不送。
        t.Check(p.Key(Key(Keys.Escape),cmd)&&!p.EditingMemo&&src.Memos.Count==2,"esc cancels without sending");
        t.Check(p.Visible,"esc while editing does not close the window");
        // 62 byte 上限：超過的字不收。
        p.BeginMemoEdit();while(p.Key(Key(Keys.Back),cmd)&&p.MemoDraft.Length>0){}
        for(int i=0;i<40;i++)p.Char('甲');
        t.Equal(31,p.MemoDraft.Length,"draft capped at 62 bytes (31 CJK)");
        p.Char('a');t.Equal(31,p.MemoDraft.Length,"ASCII 63rd byte refused");
        p.CancelMemoEdit();
        // 其他編輯互斥：開側欄輸入框會取消備註編輯，反之亦然。
        p.BeginMemoEdit();p.BeginAddAttention();
        t.Check(!p.EditingMemo&&p.SideInput==PledgePanel.SideInputKind.AddFocus,"side input cancels memo edit");
        p.BeginMemoEdit();t.Check(p.EditingMemo&&p.SideInput==PledgePanel.SideInputKind.None,"memo edit cancels side input");
        // 換窗、關窗都放棄編輯。
        p.Mouse(Click(new(20,45)),cmd);t.Check(p.ShowingNotice&&!p.EditingMemo,"switching windows drops the draft");
        t.Equal(new Rectangle(217,24,32,21),p.ModifyMemoRect,"notice window has the button too");
        p.BeginMemoEdit();p.Close();t.Check(!p.EditingMemo,"close drops the draft");
        t.Equal(2,src.Memos.Count,"nothing extra sent");

        // 自己那列不在畫面：開始編輯時捲過去（3 列，自己是第 5 個）。
        var q=new PledgePanel();q.Load(layout,Get);q.Source=src;
        q.Update(World(Data(1,"A","B","C","D","Me"),"Me"));
        t.Equal(0,q.Scroll,"starts at top");
        t.Check(q.BeginMemoEdit()&&q.Scroll==2&&!q.OwnMemoRect.IsEmpty,"scrolls own row into view",q.Scroll);
        t.Equal("",q.MemoDraft,"no memo received → empty draft");
        // 自己不在上線名單：仍可編輯，草稿改畫在側欄輸入框。
        var nr=new PledgePanel();nr.Load(layout,Get);nr.Source=src;
        nr.Update(World(Data(1,"Alice"),"Ghost"));
        t.Check(nr.BeginMemoEdit()&&nr.OwnIndex==-1&&nr.OwnMemoRect.IsEmpty,"not in list → still editable");
        // 不知道角色名：不進編輯。
        var u=new PledgePanel();u.Load(layout,Get);u.Source=src;
        u.Update(World(Data(1,"Alice"),null));
        t.Check(!u.BeginMemoEdit(),"no character name → no edit");
        // XML 沒有 ModifyMemo_Button：沒有入口。
        var nx=new PledgePanel();nx.Load(UiLayout.Parse(Xml.Replace("Name = \"ModifyMemo_Button\"","Name = \"Other_Button\""),"PledgeUI.xml"),Get);nx.Source=src;
        nx.Update(World(Data(1,"Bob"),"Bob"));
        t.Check(nx.ModifyMemoRect.IsEmpty&&!nx.BeginMemoEdit(),"no ModifyMemo_Button → no entry");
        // 伺服器回送 169 後，面板照資料顯示新備註。
        var sent=Data(1,"Alice","Bob") with{Revision=2,Memos=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["Bob"]="補甲"}};
        p.Update(World(Data(2,"Alice","Bob"),"Bob"));p.Update(World(sent with{OpenRevision=2},"Bob"));
        t.Check(p.BeginMemoEdit()&&p.MemoDraft=="補甲","after 169 the draft starts from the new memo",p.MemoDraft);
    }

    // ---- 產品端 WorldPledgeSource → LoginConnection.SendClanContent（記憶體內假傳送端，同 PartyDelegateTests）----
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static LoginConnection Seed(List<byte[]> sent,uint clanId){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)4);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\",\"ClanId\":"+clanId+",\"Clan\":\""+(clanId==0?"":"紅血盟")+"\"}"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    static void Product(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent,5);
        t.Check(c.World.Ready,"seed: world ready");
        IPledgeWindowSource s=new WorldPledgeSource(c,()=>false);
        t.Check(s.SendMemo("守城"),"in clan → sent");
        t.Equal(1,sent.Count,"one packet");
        t.Equal(Convert.ToHexString(ClanMatchingProtocol.Notes("守城")),Convert.ToHexString(sent[0]),"product sends C78 16 S(備註)");
        t.Check(!s.SendMemo(new string('甲',32))&&sent.Count==1,"over 62 bytes → nothing sent");
        t.Check(s.SendNotice("公告")&&sent.Count==2&&sent[1][1]==ClanMatchingProtocol.ContentAnnounce,"notice path unchanged (C78 15)");
        var sent2=new List<byte[]>();using var d=Seed(sent2,0);
        t.Check(!new WorldPledgeSource(d,()=>false).SendMemo("x")&&sent2.Count==0,"no clan → nothing sent");
        // 介面預設實作＝沒有接上。
        IPledgeWindowSource bare=new BareSource();
        t.Check(!bare.SendMemo("x"),"default SendMemo is not wired");
    }
    sealed class BareSource:IPledgeWindowSource {
        public bool? ShowEmblem=>null;public ClanEmblem? Emblem=>null;
        public bool SendNotice(string text)=>false;public bool SetRank(string member,byte rank)=>false;
        public bool SetShowEmblem(bool show)=>false;public bool UploadEmblem()=>false;
    }
}
