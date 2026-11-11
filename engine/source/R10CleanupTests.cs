using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T403（#918，gap-matrix 15.12.6「小清理」）離線測試：合成封包、記憶體內假傳送端、合成的 idx／pak 與版面，
// 不開遊戲、網路、視窗，不讀真實客戶端或參考客戶端。由 --module-tests 自動發現執行（名稱 r10-cleanup）。
static class R10CleanupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Single=0x22222222,Stack=0x12345678;
    const ushort Desc=1234;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    // S15 新增背包物品（同 chat-command-followup）：D id、H desc、C use、C 0、H icon、C bless、D count、C 0、S name、C 0、11 byte。
    static byte[] Inventory(uint id,uint count,string name)=>P([15],Le32(id),Le16(Desc),[0,0],Le16(12),[1],Le32(count),[0],Str(name),[0],new byte[11]);
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
    static ItemTransferRequest Delete(LoginConnection c,uint id){c.World.SelectItem(id);return c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;}
    static ItemDeleteAlertList Alerts(params int[] ids)=>ItemDeleteAlertList.Parse(System.Text.Encoding.ASCII.GetBytes(string.Join("\n",ids)+"\n"));

    [WorldModuleTest("r10-cleanup")]
    static void Run(WorldModuleTestContext t){
        ClassicDelete(t);EscapeCancel(t);KickDuringCharacterManagement(t);InputHelp(t);MailThumbDrag(t);IconFallback(t);
    }

    // ① classic 路徑（WinForms 垃圾桶）的刪除確認只問 alertitems.tbl 內的物品；讀不到表＝一律問。
    static void ClassicDelete(WorldModuleTestContext t){
        using var tableless=ServerMessageText.UseTable(null);
        var sent=new List<byte[]>();
        using(ItemDeleteAlertList.Use(Alerts(Desc)))using(var c=Seed(sent)){
            c.World.Receive(Inventory(Single,1,"劍"),"offline");
            var p=c.BeginClassicItemDelete(Delete(c,Single),"劍").GetAwaiter().GetResult();
            t.Check(p is {AlertOnly:true,AskAmount:false,AskFirst:false}&&Notice(c)==ItemDeleteText.Confirm("劍",1)&&sent.Count==0,"單件、在清單內 → 直接問 3122（classic 不問 668）",Notice(c));
            t.Check(Say(c,"y")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Single)),"Y → 一個 C138",sent.Select(Convert.ToHexString).ToArray());
        }
        sent.Clear();
        using(ItemDeleteAlertList.Use(Alerts(9999)))using(var c=Seed(sent)){
            c.World.Receive(Inventory(Single,1,"劍"),"offline");
            var old=Delete(c,Single);c.BeginItemDelete(old,"劍");
            var p=c.BeginClassicItemDelete(Delete(c,Single),"劍").GetAwaiter().GetResult();
            t.Check(p==null&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Single)),"單件、不在清單內 → 不問，直接送 C138",sent.Select(Convert.ToHexString).ToArray());
            t.Check(c.ItemDeletePromptCurrent==null&&Volatile.Read(ref old.Consumed)==1,"舊的提示被取代、舊請求不送");
            t.Check(Notice(c).Contains("刪除請求已送出"),"結果寫在聊天列提示",Notice(c));
        }
        sent.Clear();
        using(ItemDeleteAlertList.Use(null))using(var c=Seed(sent)){
            c.World.Receive(Inventory(Single,1,"劍"),"offline");
            var p=c.BeginClassicItemDelete(Delete(c,Single),"劍").GetAwaiter().GetResult();
            t.Check(p!=null&&sent.Count==0&&Notice(c)==ItemDeleteText.Confirm("劍",1),"讀不到 alertitems.tbl → 一律問 3122");
            t.Check(Say(c,"n")&&sent.Count==0&&Notice(c).Contains("取消"),"N → 不送");
        }
        sent.Clear();
        using(ItemDeleteAlertList.Use(Alerts(9999)))using(var c=Seed(sent)){
            c.World.Receive(Inventory(Stack,10,"箭"),"offline");
            var p=c.BeginClassicItemDelete(Delete(c,Stack),"箭").GetAwaiter().GetResult();
            t.Check(p is {AskAmount:true,AlertOnly:true}&&Notice(c)==ItemDeleteText.Amount("箭",10),"堆疊 → 先問數量",Notice(c));
            t.Check(Say(c,"")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Stack)),"不在清單內：數量（空白＝全部）之後直接送，不問 4114",sent.Select(Convert.ToHexString).ToArray());
        }
        sent.Clear();
        using(ItemDeleteAlertList.Use(Alerts(Desc)))using(var c=Seed(sent)){
            c.World.Receive(Inventory(Stack,10,"箭"),"offline");
            c.BeginClassicItemDelete(Delete(c,Stack),"箭").GetAwaiter().GetResult();
            t.Check(Say(c,"10")&&sent.Count==0&&c.ItemDeletePromptCurrent is {AskAmount:false}&&Notice(c)==ItemDeleteText.Confirm("箭",10),"在清單內：數量之後問 4114",Notice(c));
            t.Check(Say(c,"Y")&&sent.Count==1,"Y → 送出");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            c.World.Receive(Inventory(Single,1,"劍"),"offline");
            var r=Delete(c,Single);Set(c,"generation",(int)GetField(c,"generation")+1);
            t.Check(c.BeginClassicItemDelete(r,"劍").GetAwaiter().GetResult()==null&&sent.Count==0,"過期的請求（換了連線世代）→ 不問也不送");
        }
    }
    static object GetField(object o,string name)=>o.GetType().GetField(name,F)!.GetValue(o)!;

    // ② Esc 取消刪除提示（WinForms 聊天列／背包；原生背包窗沒有吃 Esc 時）。
    static void EscapeCancel(WorldModuleTestContext t){
        t.Check(ClientForm.ItemDeleteEscapeCancels(Keys.Escape,true,true,true,false),"世界中、有刪除提示、焦點在本窗 → Esc 取消");
        t.Check(!ClientForm.ItemDeleteEscapeCancels(Keys.Escape,true,false,true,false),"沒有提示 → 照舊（關聊天列、清輸入）");
        t.Check(!ClientForm.ItemDeleteEscapeCancels(Keys.Escape,true,true,true,true),"按住不放的重複 Esc 不算");
        t.Check(!ClientForm.ItemDeleteEscapeCancels(Keys.Escape,true,true,false,false),"焦點在別的控制項 → 不取消");
        t.Check(!ClientForm.ItemDeleteEscapeCancels(Keys.Escape,false,true,true,false),"不在世界中 → 不取消");
        t.Check(!ClientForm.ItemDeleteEscapeCancels(Keys.Escape|Keys.Shift,true,true,true,false)&&!ClientForm.ItemDeleteEscapeCancels(Keys.Enter,true,true,true,false),"其他鍵／加修飾鍵 → 不取消");
        t.Check(typeof(ClientForm).GetMethod("ProcessItemDeleteEscape",F) is {ReturnType:var rt}&&rt==typeof(bool),"ClientForm.ProcessItemDeleteEscape 存在（KeyboardNavigation.cs）");
        // 原生聊天列（R10-F）的取消動作就是 CancelItemDelete。
        using var tableless=ServerMessageText.UseTable(null);
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        c.World.Receive(Inventory(Single,1,"劍"),"offline");
        var r=Delete(c,Single);c.BeginItemDelete(r,"劍");
        var line=ChatLinePrompts.Of(c);
        t.Check(line?.Cancel!=null,"刪除提示出現在聊天列提示");
        line!.Cancel!.Invoke();
        t.Check(c.ItemDeletePromptCurrent==null&&Volatile.Read(ref r.Consumed)==1&&Notice(c)=="已取消刪除。"&&sent.Count==0,"取消 → 提示清掉、請求作廢、不送",Notice(c));
        t.Check(Say(c,"y")&&sent.Count==1&&sent[0][0]==136,"取消後下一行是一般聊天");
    }

    // ③ 角色管理途中收到 S227：先記下踢除通知（開框的來源），再照舊結束連線。
    static void KickDuringCharacterManagement(WorldModuleTestContext t){
        using var tableless=ServerMessageText.UseTable(null);
        var receive=typeof(LoginConnection).GetMethod("ReceiveCharacterManagement",F);
        var operation=typeof(LoginConnection).GetField("characterOperation",F);
        t.Check(receive!=null&&operation!=null,"ReceiveCharacterManagement／characterOperation 存在");
        if(receive==null||operation==null)return;
        foreach(var (state,op) in new[]{(LoginState.ManagingCharacters,"Delete"),(LoginState.ManagingCharacters,"Create"),(LoginState.SwitchingCharacter,"Switch")}){
            using var c=new LoginConnection();
            Set(c,"State",state);operation.SetValue(c,Enum.Parse(operation.FieldType,op));
            t.Check(c.World.Kick==null,"開始時沒有踢除通知");
            var task=(Task<bool>)receive.Invoke(c,[LoginPackets.Pad([227,0xF4,0x01,0,0,0,0]),null,1L])!;
            Exception? error=null;try{task.GetAwaiter().GetResult();}catch(Exception ex){error=ex;}
            t.Check(error is ProtocolException,$"{state}/{op}：S227 仍結束連線（ProtocolException）",error?.GetType().Name);
            t.Check(c.World.Kick is {StringId:KickNoticeRules.StringId,Reason:0xF4}&&c.World.Kick.Text==KickNoticeRules.Fallback,$"{state}/{op}：踢除通知已記下（字串 64），畫面會開框",c.World.Kick);
        }
        using(var c=new LoginConnection()){
            Set(c,"State",LoginState.ManagingCharacters);operation.SetValue(c,Enum.Parse(operation.FieldType,"Delete"));
            var task=(Task<bool>)receive.Invoke(c,[LoginPackets.Pad([6,5]),null,1L])!;
            try{task.GetAwaiter().GetResult();}catch(Exception){}
            t.Check(c.World.Kick==null,"其他封包不產生踢除通知");
        }
    }

    // ④ InputHelp 列出 /duel（R8-C 的指令），既有指令還在。
    static void InputHelp(WorldModuleTestContext t){
        var help=PlayerChatProtocol.InputHelp;
        t.Check(help.Contains("/duel")&&help.Contains("/決鬥"),"說明列出 /duel（/決鬥）",help);
        t.Check(DuelChatCommand.Is("/duel")&&DuelChatCommand.Is("/決鬥"),"說明裡的兩個寫法都是決鬥指令");
        foreach(var cmd in new[]{"/invite","/bookmark","/trade","/shop","/who"})t.Check(help.Contains(cmd),"既有說明保留 "+cmd);
    }

    // ⑤ 信件窗捲軸滑塊可以拖曳（合成版面，元素種類仿 RenewalMailUI.xml）。
    sealed class MailHost(MailView view):IMailPanelHost {
        public MailView? View=>view;
        public Task<bool> Run(MailView shown,string command,byte box,uint id,string receiver="",string subject="",string body="")=>Task.FromResult(true);
    }
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF00FFFFu,w*h).ToArray()));
    static string Row(int i)=>$"""<CheckButton Name = "MailInfo0{i}" BasePng = "748" Width = "10" Height = "15" MouseEvent = "SelectMailItem"/>""";
    const string ScrollXml="""
            <ScrollBar Name = "S" X = "188" Y = "80" Width = "12" Height = "100" ScrollStart = "10" ScrollHeight = "80" Vertical = "1" MouseEvent = "Scroll">
              <Button Name = "Up_Button" BaseSurf = "446;1" MouseEvent = "ScrollButton"/>
              <Button Name = "Move_Button" Y = "10" BaseSurf = "448;1" MouseEvent = "ScrollButton"/>
              <Button Name = "Down_Button" Y = "90" BaseSurf = "444;1" MouseEvent = "ScrollButton"/>
            </ScrollBar>
        """;
    static readonly string MailXml=$"""
        <?xml version="1.0"?>
        <RenewalMailUI Toggle = "true">
          <Window Name = "MailList_Window" X = "10" Y = "20" Width = "200" Height = "220" BasePng = "782;1" Moveable = "30" KeyboardEvent = "CloseMailUI" MouseEvent = "Dummy" Activate = "0">
            <List Name = "MailList" X= "10" Y = "80" Width = "180" Height = "45" Vertical = "1">
              {Row(0)}
              {Row(1)}
              {Row(2)}
            </List>
        {ScrollXml}
          </Window>
          <Window Name = "ReadMail_Window" X = "0" Y = "0" Width = "200" Height = "220" BasePng = "782;1" Moveable = "30" KeyboardEvent = "CloseReadMailUI" MouseEvent = "Dummy" Activate = "0">
            <MultiEdit Name = "MailText_Label" X = "20" Y = "95" Width = "150" Height = "100" ReadOnly = "1" LimitText = "256"/>
        {ScrollXml}
          </Window>
          <Window Name = "WriteMail_Window" X = "0" Y = "0" Width = "200" Height = "220" BasePng = "782;1" Moveable = "30" KeyboardEvent = "CloseWriteMailUI" MouseEvent = "Dummy" Activate = "0">
          </Window>
        </RenewalMailUI>
        """;
    static UiMouseEvent M(UiMouseKind k,int x,int y)=>new(k,new(10+x,20+y),MouseButtons.Left,1,0,Keys.None);
    static void MailThumbDrag(WorldModuleTestContext t){
        var surfs=new Dictionary<int,UiSurface>{[446]=Surface(446,12,10),[448]=Surface(448,12,10),[444]=Surface(444,12,10)};
        var pngs=new Dictionary<int,UiSurface>{[782]=Surface(782,200,220),[748]=Surface(748,10,10)};
        var headers=Enumerable.Range(0,10).Select(i=>new MailHeader((uint)(100+i),0,26,11,(byte)(1+i),"寄件"+i,"主旨"+i)).ToArray();
        var p=new MailPanel();
        p.Load(UiLayout.Parse(MailXml,"RenewalMailUI-c.xml"),id=>surfs.GetValueOrDefault(id),id=>pngs.GetValueOrDefault(id));
        p.Host=new MailHost(new(new MailStamp(1,1,7),1,headers,null,"",""));
        var c=new UiNullCommands();
        t.Check(p.Available&&p.Open()&&p.Rows.Count==10,"合成版面可用，10 封信、3 列（捲動範圍 7）",p.Unavailable);
        // Thumb at (188, 90), 12×10; track 80 − 10 = 70 px → 10 px per row.
        t.Check(p.Mouse(M(UiMouseKind.Down,190,92),c)&&p.DraggingThumb,"按在滑塊上 → 開始拖曳");
        p.Mouse(M(UiMouseKind.Move,190,122),c);
        t.Equal(3,p.ListScroll,"往下 30 px → 第 3 列");
        p.Mouse(M(UiMouseKind.Move,190,500),c);
        t.Equal(7,p.ListScroll,"拉過頭 → 停在最後（7）");
        p.Mouse(M(UiMouseKind.Move,190,-200),c);
        t.Equal(0,p.ListScroll,"拉過頂 → 0");
        p.Mouse(M(UiMouseKind.Move,190,142),c);
        t.Check(p.Mouse(M(UiMouseKind.Up,190,142),c)&&!p.DraggingThumb&&p.ListScroll==5,"放開 → 停在 5、結束拖曳",p.ListScroll);
        // The thumb is now drawn (and hit) 50 px lower; the old place is just the track.
        t.Check(p.Mouse(M(UiMouseKind.Down,190,92),c)&&!p.DraggingThumb,"滑塊原位置已經不是滑塊");
        p.Mouse(M(UiMouseKind.Up,190,92),c);
        t.Check(p.Mouse(M(UiMouseKind.Down,190,143),c)&&p.DraggingThumb,"按在滑塊的新位置 → 拖曳");
        p.Mouse(M(UiMouseKind.Move,190,133),c);
        t.Equal(4,p.ListScroll,"抓的點跟著指標（往上 10 px → 4）");
        p.Mouse(M(UiMouseKind.Up,190,133),c);
        t.Check(p.Mouse(M(UiMouseKind.Down,190,172),c)&&!p.DraggingThumb,"Down 鈕不是滑塊");
        p.Mouse(M(UiMouseKind.Up,190,172),c);
        t.Equal(5,p.ListScroll,"Down 鈕照舊捲一列");
        p.Mouse(M(UiMouseKind.Down,190,145),c);   // position 5 → thumb at 90 + 50
        t.Check(p.DraggingThumb,"再按住滑塊");
        p.Close();
        t.Check(!p.DraggingThumb,"關窗 → 拖曳中止");
        var few=new MailPanel();
        few.Load(UiLayout.Parse(MailXml,"RenewalMailUI-c.xml"),id=>surfs.GetValueOrDefault(id),id=>pngs.GetValueOrDefault(id));
        few.Host=new MailHost(new(new MailStamp(1,1,7),1,headers[..2],null,"",""));few.Open();
        few.Mouse(M(UiMouseKind.Down,190,92),c);few.Mouse(M(UiMouseKind.Move,190,150),c);few.Mouse(M(UiMouseKind.Up,190,150),c);
        t.Equal(0,few.ListScroll,"沒有可捲的內容 → 滑塊不動");
        t.Check(c.Calls.Count==0,"不呼叫任何指令");
    }

    // ⑥ ItemIconSource 的參考客戶端備援：本機沒有的 <icon>.tbt 才讀參考客戶端（合成的 idx／pak）。
    static byte[] Tbt(ushort pixel)=>[0,0,2,1,1,0,2,(byte)pixel,(byte)(pixel>>8),(byte)pixel,(byte)(pixel>>8)];
    static (byte[] Idx,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        var pak=new List<byte>();var idx=new List<byte>();idx.AddRange(Le32((uint)files.Length));
        foreach(var (name,data) in files){
            idx.AddRange(Le32((uint)pak.Count));var n=new byte[20];System.Text.Encoding.ASCII.GetBytes(name).CopyTo(n,0);idx.AddRange(n);idx.AddRange(Le32((uint)data.Length));
            pak.AddRange(data);
        }
        return (idx.ToArray(),pak.ToArray());
    }
    static void IconFallback(WorldModuleTestContext t){
        t.Check(RefClientIconFallback.For(null)==null,"沒有參考客戶端 → 不建備援");
        t.Check(RefClientIconFallback.Kor25Sha256.Keys.OrderBy(k=>k).SequenceEqual([5731,5734,5737,6458,6501]),"記下 5 個學魔法缺圖的 kor25 SHA-256");
        var (a5731,n5731)=BuffIconCatalog.Candidates(5731).Skip(2).First();   // "<n>.tbt" in its hashed archive (Sprite08)
        t.Check(a5731==9&&n5731=="5731.tbt","5731.tbt 的雜湊檔是 Sprite08（與 kor25 實際位置相同）",$"{a5731} {n5731}");
        var (a6501,_)=BuffIconCatalog.Candidates(6501).Skip(2).First();
        var dupName="5734.tbt";var (aDup,_)=BuffIconCatalog.Candidates(5734).Skip(2).First();
        var images=new Dictionary<int,(byte[] Idx,byte[] Pak)>{
            [a5731]=Archive(("5731.tbt",Tbt(0x7C00))),
            [0]=Archive(("6501.tbt",Tbt(0x001F)),("bad.tbt",[1,2])),
        };
        if(aDup!=0)images[aDup]=Archive((dupName,Tbt(1)),(dupName,Tbt(2)));
        var reference=RefClientIconFallback.FromImages(@"Z:\ref",images);
        var local=TbtImage.Decode(Tbt(0x03E0),"local");
        var source=ItemIconSource.Combine(icon=>icon==100?(local,local.ToBitmap(),"Sprite/100.tbt"):null,reference);
        t.Check(source.Get(100) is {Where:"Sprite/100.tbt"},"本機有的圖用本機（主客戶端優先）");
        var hit=source.Get(5731);
        t.Check(hit is {Icon:5731,Offset.X:0}&&hit.Where==$"ref:{RefClientSurfFallback.ArchiveName(a5731)}/5731.tbt"&&hit.Bitmap.Width==2,"本機沒有的 5731 → 參考客戶端 Sprite08",hit?.Where);
        t.Check(hit!=null&&hit.Bitmap.GetPixel(0,0).R>200,"像素照 .tbt 解出");
        t.Check(source.Get(6501) is {Where:"ref:Sprite/6501.tbt"},"雜湊檔沒有時再找 Sprite（同 HashFileManager 順序）",source.Get(6501)?.Where);
        t.Check(a6501!=0,"6501 的雜湊檔不是 Sprite（測到第二順位）");
        t.Check(aDup==0||source.Get(5734)==null,"idx 重名 → 不用");
        t.Check(source.Get(5737)==null&&source.Get(0)==null,"兩邊都沒有 → null（不畫）");
        t.Check(reference.Served.Count==2&&reference.Served.All(s=>s.Sha256.Length==64),"讀到的圖都記下 SHA-256",reference.Served.Select(s=>s.Where).ToArray());
        t.Check(reference.Get(5731) is {} first&&reference.Get(5731) is {} again&&ReferenceEquals(first.Bitmap,again.Bitmap)&&reference.Served.Count==2,"同一張圖只解一次（快取）");
        var none=RefClientIconFallback.FromImages(@"Z:\none",new Dictionary<int,(byte[],byte[])>());
        t.Check(ItemIconSource.Combine(_=>null,none).Get(5731)==null&&ItemIconSource.Combine(_=>null,null).Get(5731)==null,"參考客戶端沒有檔或沒設定 → null");
    }
}
