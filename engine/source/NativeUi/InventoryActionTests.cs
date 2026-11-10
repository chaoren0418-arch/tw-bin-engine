using System.Reflection;
using System.Text;
using System.Text.Json;
namespace P06.NativeUi;

// R9-L（T372／#856，FU9§13、FU8§8.1）背包操作的離線測試：合成 XML／surface、合成字串表與 alertitems 清單、記憶體內假傳送端，
// 不開遊戲、網路、視窗，不讀客戶端素材。由 --module-tests 執行（名稱 inventory-action）。
static class InventoryActionTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Stack=0x12345678,Single=0x22222222;
    const ushort Alerted=1234,Plain=4321;

    [WorldModuleTest("inventory-action")]
    static void Run(WorldModuleTestContext t){
        Texts(t);AlertList(t);DeleteFlow(t);PanelInput(t);
    }

    // ---------------------------------------------------------------- 668／3054 文字
    static ServerStringTable Table(params (int Id,string Text)[] rows){
        int n=rows.Max(r=>r.Id)+1;var lines=new string[n];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        return ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes(n+"\n"+string.Join("\n",lines)));
    }
    static void Texts(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){
            t.Equal("真的要把 \"劍\" 刪除嗎？(Y/N)",ItemDeleteText.AskFirst("劍"),"668 fallback = local string-c.tbl text without colour codes");
            t.Equal("要刪除幾個呢？(1-10, Enter=全部, Esc=取消)",ItemDeleteText.Amount("箭",10),"amount fallback = 3054 text, %u = held count");
        }
        using(ServerMessageText.UseTable(Table((668,"合成668 \\fQ\"%s\"\\f> (Y/N)"),(3054,"合成3054 1-%u")))){
            t.Equal("合成668 \"劍\" (Y/N)",ItemDeleteText.AskFirst("劍"),"668 read from the table, %s = name, colour codes dropped");
            t.Equal("合成3054 1-10",ItemDeleteText.Amount("箭",10),"4115 beyond the table → 3054 from the table, %u = count");
        }
        using(ServerMessageText.UseTable(Table((10,"x")))){
            t.Equal("真的要把 \"劍\" 刪除嗎？(Y/N)",ItemDeleteText.AskFirst("劍"),"668 beyond the table falls back");
        }
    }

    // ---------------------------------------------------------------- alertitems.tbl
    static (byte[] Index,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }
    static ItemDeleteAlertList List(params int[] ids)=>ItemDeleteAlertList.Parse(Encoding.ASCII.GetBytes(string.Join("\r\n",ids)+"\r\n"));
    static void AlertList(WorldModuleTestContext t){
        t.Equal("alertitems.tbl",ItemDeleteAlertList.FileName,"sub_996C40 reads alertitems.tbl");
        var l=ItemDeleteAlertList.Parse(Encoding.ASCII.GetBytes("25\r\n26\n\n  64 \n13954"));
        t.Check(l.Count==4&&l.Contains(25)&&l.Contains(64)&&l.Contains(13954)&&!l.Contains(27),"one integer per line; blank lines and spaces ignored; no count line");
        t.Throws<InvalidDataException>(()=>ItemDeleteAlertList.Parse(Encoding.ASCII.GetBytes("25\nabc\n")),"non-integer line → InvalidDataException");
        t.Throws<InvalidDataException>(()=>ItemDeleteAlertList.Parse(Encoding.ASCII.GetBytes("-1\n")),"negative → InvalidDataException");
        var (idx,pak)=Archive(("string-c.tbl",[0x31,0x0A]),("alertitems.tbl",Encoding.ASCII.GetBytes("25\n26\n")));
        t.Throws<InvalidDataException>(()=>ItemDeleteAlertList.Load(TextArchive.FromBytes(idx,pak)),"synthetic content ≠ local pin → InvalidDataException (caller treats as no list)");
        var (idx2,pak2)=Archive(("string-c.tbl",[0x31,0x0A]));
        t.Throws<InvalidDataException>(()=>ItemDeleteAlertList.Load(TextArchive.FromBytes(idx2,pak2)),"archive without alertitems.tbl → InvalidDataException");
        InventoryItem Item(ushort desc)=>new(1,desc,0,0,1,1,1,0,"x","","");
        using(ItemDeleteAlertList.Use(List(Alerted))){
            t.Check(ItemDeleteAlertList.NeedsConfirm(Item(Alerted))&&!ItemDeleteAlertList.NeedsConfirm(Item(Plain)),"listed DescriptionId → confirm; others → no confirm");
        }
        using(ItemDeleteAlertList.Use(null)){
            t.Check(ItemDeleteAlertList.NeedsConfirm(Item(Plain)),"no table → always confirm (never fewer questions than the original)");
        }
    }

    // ---------------------------------------------------------------- 刪除流程（LoginConnection，合成封包）
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    // S15（同 chat-command-followup）：D id、H desc、C use、C 0、H icon、C bless、D count、C 0、S name、C 0、11 byte。
    static byte[] Inventory(uint id,ushort desc,uint count,string name)=>P([15],Le32(id),Le16(desc),[0,0],Le16(12),[1],Le32(count),[0],Str(name),[0],new byte[11]);
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Say(LoginConnection c,string text){var s=c.PlayerChatSnapshot();return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor).GetAwaiter().GetResult();}
    static string Notice(LoginConnection c)=>c.PlayerChatSnapshot().Notice;
    static LoginConnection With(List<byte[]> sent,uint id,ushort desc,uint count,string name){
        var c=Seed(sent);c.World.Receive(Inventory(id,desc,count,name),"offline");c.World.SelectItem(id);return c;
    }
    static ItemDeletePrompt Begin(LoginConnection c,string name)=>c.BeginInventoryDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,name)!;

    static void DeleteFlow(WorldModuleTestContext t){
        using var table=ServerMessageText.UseTable(null);
        using var alert=ItemDeleteAlertList.Use(List(Alerted));
        var sent=new List<byte[]>();

        // 單件、不在 alertitems.tbl：668 → 是 → 直接送 C138。
        using(var c=With(sent,Single,Plain,1,"劍")){
            var p=Begin(c,"劍");
            t.Check(p.AskFirst&&!p.AskAmount&&p.AlertOnly&&Notice(c)==ItemDeleteText.AskFirst("劍"),"single item asks 668 first",Notice(c));
            t.Check(Say(c,"n")&&sent.Count==0&&c.ItemDeletePromptCurrent==null&&Notice(c).Contains("取消"),"668 N cancels without sending",Notice(c));
            t.Check(Say(c,"hello")&&sent.Count==1&&sent[0][0]==136,"after the prompt the line is chat again");
            sent.Clear();
            Begin(c,"劍");
            t.Check(Say(c,"y")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Single))&&c.ItemDeletePromptCurrent==null,"668 Y, not listed → one C138, no 3122",sent.Select(Convert.ToHexString).ToArray());
            t.Check(c.World.Inventory.Single().Count==1,"no local inventory prediction");
        }
        sent.Clear();

        // 單件、在 alertitems.tbl：668 → 是 → 3122 → 是 → 送。
        using(var c=With(sent,Single,Alerted,1,"劍")){
            Begin(c,"劍");
            t.Check(Say(c,"Y")&&sent.Count==0&&c.ItemDeletePromptCurrent is {AskFirst:false,AskAmount:false}&&Notice(c)==ItemDeleteText.Confirm("劍",1),"listed item: 668 Y → 3122",Notice(c));
            t.Check(Say(c,"n")&&sent.Count==0&&c.ItemDeletePromptCurrent==null,"3122 N cancels");
            Begin(c,"劍");Say(c,"y");
            t.Check(Say(c,"y")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Single)),"668 Y, 3122 Y → one C138");
        }
        sent.Clear();

        // 沒有清單（CI、pin 不符）：一律再問 3122。
        using(ItemDeleteAlertList.Use(null))
        using(var c=With(sent,Single,Plain,1,"劍")){
            Begin(c,"劍");
            t.Check(Say(c,"y")&&sent.Count==0&&Notice(c)==ItemDeleteText.Confirm("劍",1),"no alert list → 3122 still asked");
            t.Check(Say(c,"y")&&sent.Count==1,"then sent");
        }
        sent.Clear();

        // 堆疊、不在清單：數量（3054）→ 全部 → 直接送；部分數量不送。
        using(var c=With(sent,Stack,Plain,10,"箭")){
            var p=Begin(c,"箭");
            t.Check(p.AskAmount&&!p.AskFirst&&Notice(c)==ItemDeleteText.Amount("箭",10),"stack asks the amount first (no 668)",Notice(c));
            t.Check(Say(c,"3")&&sent.Count==0&&c.ItemDeletePromptCurrent==null&&Notice(c).Contains("整疊"),"partial amount refused (3.81 C138 deletes the stack)",Notice(c));
            Begin(c,"箭");
            t.Check(Say(c,"")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Stack))&&c.ItemDeletePromptCurrent==null,"Enter (empty) = all, not listed → sent without Y/N");
            sent.Clear();Set(c,"nextItemAt",0L);   // 送出後的本地點擊間隔（GroundClickGuardMs）
            Begin(c,"箭");
            t.Check(Say(c,"10")&&sent.Count==1,"full amount typed, not listed → sent");
        }
        sent.Clear();

        // 堆疊、在清單：數量 → 4114（本機表沒有，引擎文字）→ 是 → 送。
        using(var c=With(sent,Stack,Alerted,10,"箭")){
            Begin(c,"箭");
            t.Check(Say(c,"10")&&sent.Count==0&&c.ItemDeletePromptCurrent is {AskAmount:false,AskFirst:false}&&Notice(c)==ItemDeleteText.Confirm("箭",10),"listed stack: amount → 4114 Y/N",Notice(c));
            t.Check(Say(c,"y")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Stack)),"Y sends one C138");
        }
        sent.Clear();

        // R6-E（WinForms 垃圾桶）不變：單件直接 3122，即使不在清單。
        using(var c=With(sent,Single,Plain,1,"劍")){
            var p=c.BeginItemDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,"劍")!;
            t.Check(!p.AskFirst&&!p.AlertOnly&&Notice(c)==ItemDeleteText.Confirm("劍",1),"R6-E path still asks 3122 directly");
            t.Check(Say(c,"y")&&sent.Count==1,"R6-E Y sends");
        }
        sent.Clear();

        // 新的背包窗刪除取代舊提示；世界改變後提示失效。
        using(var c=With(sent,Single,Plain,1,"劍")){
            var first=c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;c.BeginInventoryDelete(first,"劍");
            var second=c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;c.BeginInventoryDelete(second,"劍");
            t.Check(Volatile.Read(ref first.Consumed)==1,"newer delete prompt retires the older request");
            Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);
            t.Check(c.ItemDeletePromptCurrent==null&&sent.Count==0,"world change expires the prompt");
        }
    }

    // ---------------------------------------------------------------- 背包窗輸入
    sealed class Host:IItemWindowHost {
        public readonly List<string> Calls=[];
        public bool Use(uint objectId){Calls.Add("use:"+objectId);return true;}
        public bool Delete(uint objectId){Calls.Add("delete:"+objectId);return true;}
    }
    static UiMouseEvent Down(Point at,int clicks=1,MouseButtons button=MouseButtons.Left)=>new(UiMouseKind.Down,at,button,clicks,0,Keys.None);
    static UiKeyEvent K(Keys k,bool down=true,bool repeat=false)=>new(k,down,repeat);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);

    static void PanelInput(WorldModuleTestContext t){
        var surf=LegacyWindowTestKit.Surfaces(new(){
            [2046]=(170,260),[2047]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10),
            [2048]=(130,20),[2049]=(130,20),[2050]=(130,20),[2051]=(130,20),[2052]=(22,22),[2054]=(22,22)});
        var layout=UiLayout.Parse(ItemWindowPanel.TestXml,"uimagicitem.xml");
        var host=new Host();var root=new UiRoot();var cmd=new UiNullCommands();
        var chat=new ChatPanel();root.Add(chat);
        var p=new ItemWindowPanel(host:()=>host,find:root.Find);p.Load(layout,surf.Get);root.Add(p);
        InventoryItem Item(uint id,string name)=>new(id,1,0,0,10,1,1,1,name,"","");
        var inv=new[]{Item(1,"劍"),Item(2,"藥水"),Item(3,"卷軸")};
        IUiGameState state=new UiChatGameState(new UiItemWindowGameState(new UiStaticGameState(InWorld:true),new UiItemWindowData(inv)),new(new PlayerChatView(1,1,Me,1,true,"",[]),false));
        p.Open();chat.Update(state);p.Update(state);
        t.Check(p.Visible&&p.PageItems.Count==3,"window open with three items");

        // 雙擊：同一格第二下才使用；第一下只選取。
        var cell0=Center(p.CellRect(0));var cell1=Center(p.CellRect(1));
        t.Check(p.Mouse(Down(cell0),cmd)&&p.SelectedId==1&&host.Calls.Count==0,"single click selects only");
        t.Check(p.Mouse(Down(cell0,2),cmd)&&host.Calls.SequenceEqual(["use:1"]),"double click on the selected cell → Use");
        host.Calls.Clear();
        t.Check(p.Mouse(Down(cell1,2),cmd)&&p.SelectedId==2&&host.Calls.Count==0,"a Clicks=2 press on another cell selects it, does not use");
        t.Check(p.Mouse(Down(cell1,1,MouseButtons.Right),cmd)&&host.Calls.Count==0,"right button does nothing");

        // 垃圾桶：有選取 → Delete；不是拖曳區。
        var trash=Center(p.TrashRect);
        t.Check(!p.IsDragHandle(trash),"trash is a control, not a drag handle");
        t.Check(p.Mouse(Down(trash),cmd)&&host.Calls.SequenceEqual(["delete:2"]),"trash with a selection → Delete(selected)");
        host.Calls.Clear();
        t.Check(p.Mouse(Down(Center(p.ChestRect)),cmd)&&host.Calls.Count==0,"chest click sends nothing (drawn only, FU9§13.3)");

        // Delete 鍵。
        t.Check(p.Key(K(Keys.Delete),cmd)&&host.Calls.SequenceEqual(["delete:2"]),"Delete key → Delete(selected)");
        host.Calls.Clear();
        t.Check(!p.Key(K(Keys.Delete,repeat:true),cmd)&&!p.Key(K(Keys.Delete,down:false),cmd)&&!p.Key(K(Keys.Delete|Keys.Shift),cmd)&&host.Calls.Count==0,"repeat, release and Shift+Delete not taken");
        t.Check(!p.Key(K(Keys.I),cmd)&&!p.Key(K(Keys.Tab),cmd),"other keys still not taken (hotkeys live in MenuButtonsPanel)");
        chat.Input.Begin(false);
        if(chat.Input.Open)t.Check(!p.Key(K(Keys.Delete),cmd)&&host.Calls.Count==0,"chat input open → Delete goes to the chat line");
        chat.Input.Close();
        t.Check(!chat.Input.Open,"chat input closed again");

        // 沒有選取／沒有宿主／窗關著 → 不刪。
        var q=new ItemWindowPanel(host:()=>host);q.Load(layout,surf.Get);q.Open();q.Update(state);
        t.Check(!q.Key(K(Keys.Delete),cmd)&&q.Mouse(Down(Center(q.TrashRect)),cmd)&&host.Calls.Count==0,"no selection → Delete key not taken, trash sends nothing");
        var none=new ItemWindowPanel(host:()=>null);none.Load(layout,surf.Get);none.Open();none.Update(state);
        none.Mouse(Down(Center(none.CellRect(0))),cmd);
        t.Check(none.SelectedId==1&&!none.Key(K(Keys.Delete),cmd)&&none.Mouse(Down(Center(none.CellRect(0)),2),cmd),"no host → no delete, double click only selects");
        p.Close();
        t.Check(!p.Key(K(Keys.Delete),cmd)&&host.Calls.Count==0,"closed window takes no Delete");

        // 宿主預設是靜態 ItemWindowPanel.Host（ClientForm.InitItemTransfer 設定）。
        var saved=ItemWindowPanel.Host;
        try{
            ItemWindowPanel.Host=host;
            var d=new ItemWindowPanel();d.Load(layout,surf.Get);d.Open();d.Update(state);
            d.Mouse(Down(Center(d.CellRect(2))),cmd);
            t.Check(d.DeleteSelected()&&host.Calls.SequenceEqual(["delete:3"]),"default host = ItemWindowPanel.Host");
        }finally{ItemWindowPanel.Host=saved;}
    }
}
