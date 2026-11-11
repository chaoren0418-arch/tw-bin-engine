using System.Collections.Concurrent;
using System.Reflection;
using P06.NativeUi;
namespace P06;

// T434（#992，CU-2）classic-ui-audit §3 G3、G4、G5、G9、G10、G11 的離線測試：只測拆出來的規則與控制器（假主機），
// 合成封包與記憶體內的 WorldSession；不開視窗、不開遊戲、不讀客戶端資源、不連網路。WinForms 控制項本身（分頁、清單、MessageBox）
// 在測試裡不建立，真機再確認（報告「待驗證」）。
static class ClassicGapTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static InventoryItem Item(uint id,uint count,ushort icon=7,string name="測試物品")=>new(id,1,0,0,icon,1,count,0,name,"","");
    static ConcurrentDictionary<uint,InventoryItem> Inventory(WorldSession w)=>(ConcurrentDictionary<uint,InventoryItem>)typeof(WorldSession).GetField("inventory",F)!.GetValue(w)!;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];

    // ---- G3：classic 開店設定 ----
    sealed class ShopHost(WorldSession world):IPersonalShopHost {
        public int Generation{get;set;}=1;
        public bool InWorld{get;set;}=true;
        public bool CanSend{get;set;}=true;
        public uint? SelectedItemId{get;set;}
        public bool Classic{get;set;}=true;
        public bool Active{get;set;}
        public int Shown;
        public List<byte[]> Sent{get;}=[];
        public PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell)=>world.PersonalShopCandidate(objectId,price,count,sell);
        public Task<bool> Send(byte[] packet){Sent.Add(packet);return Task.FromResult(true);}
        public Bitmap? Icon(ushort iconId)=>null;
        public uint? HeldCount(uint objectId)=>world.Inventory.FirstOrDefault(i=>i.ObjectId==objectId)?.Count;
        public bool ShopActive=>Active;
        public void ShowClassicSetup()=>Shown++;
        public bool ClassicSetup=>Classic;
    }

    [WorldModuleTest("classic-personal-shop")]
    static void PersonalShop(WorldModuleTestContext t){
        var world=new WorldSession();var inv=Inventory(world);
        foreach(var it in new[]{Item(500,10),Item(501,1,9,"樣本")})inv[it.ObjectId]=it;

        // 舊主機（沒有 ClassicSetup）：沒有面板照舊拒絕。
        var old=new ShopHost(world){Classic=false};var oldCtl=new PersonalShopController(old);
        t.Check(!oldCtl.Open()&&!oldCtl.ClassicOpen&&oldCtl.Notice.Contains("原生 UI")&&old.Shown==0,"host without ClassicSetup: Open refused as before");

        var host=new ShopHost(world);var ctl=new PersonalShopController(host);
        host.InWorld=false;t.Check(!ctl.Open()&&!ctl.ClassicOpen,"outside the world: refused");host.InWorld=true;
        t.Check(ctl.Open()&&ctl.ClassicOpen&&host.Shown==1,"no native panel → classic setup opens and the page is shown (/shop path)");
        t.Check(ctl.Open()&&!ctl.ClassicOpen,"Open again toggles it closed (sub_F480C0 toggle)");
        ctl.Open();
        t.Check(!ctl.SelectClassic(true,8)&&ctl.SelectClassic(true,0)&&ctl.Setup.Selected==(true,0),"select sell slot 1 (slot 9 out of range)");
        host.SelectedItemId=500;
        t.Check(ctl.Place(30,5)&&ctl.Setup.Sell[0] is {ObjectId:500,Price:30,Count:5},"place selected inventory item into the selected slot",ctl.Notice);
        t.Check(ctl.SelectClassic(false,2)&&ctl.Setup.Selected==(false,2),"select buy slot 3");
        host.SelectedItemId=501;t.Check(ctl.Place(12,3)&&ctl.Setup.Buy[2] is {ObjectId:501},"place buy sample");
        t.Check(ctl.SetMessages("賣東西","")&&ctl.Setup.Messages[0]=="賣東西","messages applied");
        t.Equal("出售 1：測試物品 × 5，單價 30",PersonalShopController.ClassicRow(ctl.Setup,0),"row 0 text");
        t.Equal("收購 3：樣本 × 3，單價 12",PersonalShopController.ClassicRow(ctl.Setup,10),"row 10 text (buy 3)");
        t.Equal("出售 2：（空）",PersonalShopController.ClassicRow(ctl.Setup,1),"empty row text");

        var expected=ctl.Setup.BuildOpen()!;
        t.Check(ctl.SubmitClassic().Result&&host.Sent.Count==1&&host.Sent[0].SequenceEqual(expected)&&!ctl.ClassicOpen&&ctl.OpenSent,"OK sends C38 type 0 (same bytes as Setup.BuildOpen) and closes the setup");
        t.Check(!ctl.SubmitClassic().Result&&host.Sent.Count==1,"OK while closed sends nothing");

        // 已開店（S119 動作 70）：OK 只關、Cancel 關店（R10-M 原生規則）。
        host.Active=true;ctl.Open();
        t.Check(!ctl.SubmitClassic().Result&&host.Sent.Count==1&&!ctl.ClassicOpen,"shop active: OK only closes, no resend");
        ctl.Open();
        t.Check(ctl.CancelClassic().Result&&host.Sent.Count==2&&host.Sent[1].SequenceEqual(PersonalShopPackets.Close())&&!ctl.ClassicOpen,"shop active: Cancel sends C38 type 1");
        host.Active=false;ctl.Open();
        t.Check(ctl.CancelClassic().Result&&host.Sent.Count==2&&!ctl.ClassicOpen,"not open: Cancel only closes");

        // 空清單：不送，提示 Setup.Notice。
        var empty=new PersonalShopController(new ShopHost(world));empty.Open();
        t.Check(!empty.SubmitClassic().Result&&empty.Notice.Contains("至少"),"empty setup refused",empty.Notice);

        // 換角／離開世界：設定關閉。
        ctl.Open();host.Generation=2;ctl.Update();
        t.Check(!ctl.ClassicOpen&&ctl.Setup.Sell.All(e=>e==null),"generation change closes and clears");
        ctl.Open();host.InWorld=false;ctl.Update();
        t.Check(!ctl.ClassicOpen,"leaving the world closes the classic setup");
    }

    // ---- G4：op 175 以物品學魔法 ----
    [WorldModuleTest("classic-spell-exchange")]
    static void SpellExchange(WorldModuleTestContext t){
        var s=new WorldSession();
        t.Check(ClassicSpellExchange.Shown(s.SpellExchangeTradeWindow(),false)==null,"no S13 → nothing to show");
        s.Receive(LoginPackets.Pad([SpellExchangeProtocol.SList,2,0,..Le32(0),..Le32(4)]),"");
        var list=s.SpellExchangeTradeWindow();
        t.Check(list is {Items.Length:2,Mode:TradeWindowPanel.ModeSpell}&&SpellTradeProtocol.IsExchangeRevision(list.Revision),"S13 → mode 7, odd revision, 2 rows",list);
        t.Check(ReferenceEquals(ClassicSpellExchange.Shown(list,false),list),"classic shows it when the native window does not");
        t.Check(ClassicSpellExchange.Shown(list,true)==null,"native window owns it → classic hides");
        t.Check(ClassicSpellExchange.NativeOwns(list)&&!ClassicSpellExchange.NativeOwns(null),"native owns the op 175 list");
        var buy=new TradeListView(TradeWindowPanel.ModeSpell,SpellTradeProtocol.BuyRevision(3),[new(1,"x",0,10,1,true)],0,true,true,"");
        t.Check(!ClassicSpellExchange.NativeOwns(buy)&&ClassicSpellExchange.Shown(buy,false)==null,"op 44 (even revision) is not the op 175 list");
        t.Check(ClassicSpellExchange.Shown(list! with{Items=[]},false)==null,"no rows → nothing to show");
        t.Equal("0,4",string.Join(",",ClassicSpellExchange.Choices([(0u,true),(4u,true),(9u,false),(0u,true)])),"choices: checked, in order, distinct");
        var v=s.SpellExchangeSnapshot(1,false,true);
        t.Check(ClassicSpellExchange.Current(v,list!.Revision)&&!ClassicSpellExchange.Current(v,list.Revision+2),"snapshot matches the shown list only");
        t.Check(v.CanSend&&s.SpellExchangeRequest(v.OfferRevision,[0,4]) is {} p&&p[0]==SpellExchangeProtocol.CLearn,"C191 built for the chosen rows");
        s.CloseSpellExchange(s.SpellExchangeRevision);
        t.Check(!ClassicSpellExchange.Current(s.SpellExchangeSnapshot(1,false,true),list.Revision)&&s.SpellExchangeTradeWindow()==null,"closed → stale, list gone");
    }

    // ---- G5：選項的 WinForms 入口 ----
    [WorldModuleTest("classic-options")]
    static void Options(WorldModuleTestContext t){
        var events=ClassicOptionEntries.Items.Select(i=>i.Event).ToArray();
        t.Check(events.SequenceEqual([OptionPanel.ShowDropItemNameEvent,OptionPanel.TransparentDeadMonsterEvent,OptionPanel.ElfTeleportEvent]),"three option check boxes: drop names, transparent dead, elf teleport",events);
        t.Check(ClassicOptionEntries.Items.All(i=>i.Text.Length>0),"labels present");
        var bars=ClassicVolumeEntries.Items.Select(i=>PlayerAudioSettings.BarCategory(i.Bar)).ToArray();
        t.Check(bars.SequenceEqual(new SoundCategory?[]{SoundCategory.Music,SoundCategory.Sound,SoundCategory.EnvSound}),"three volume bars → Music / Sound / EnvSound (same mapping as OptionUI)",bars);
        t.Check(!ClassicVolumeEntries.Items.Any(i=>i.Bar==OptionPanel.VoiceVolumeBar),"voice bar not offered");
        var vol=new SoundVolumes();
        t.Check(PlayerAudioSettings.SetBarLevel(vol,OptionPanel.MusicVolumeBar,0.37)&&ClassicVolumeEntries.Percent(PlayerAudioSettings.BarLevel(vol,OptionPanel.MusicVolumeBar))==37,"slider 37 → music 0.37 → reads back 37");
        t.Check(PlayerAudioSettings.BarLevel(vol,OptionPanel.EnvVolumeBar)!=0.37,"other categories untouched");
        t.Equal(56,ClassicVolumeEntries.Percent(0.555),"0.555 → 56 (round half away)");
        t.Equal(0,ClassicVolumeEntries.Percent(null),"null → 0");
        t.Equal(100,ClassicVolumeEntries.Percent(1.5),"clamped to 100");
        t.Equal(0,ClassicVolumeEntries.Percent(double.NaN),"NaN → 0");
    }

    // ---- G9：S250/84 在 classic 印進聊天欄 ----
    [WorldModuleTest("classic-center-message")]
    static void CenterMessage(WorldModuleTestContext t){
        static byte[] Green(string msg,byte type=2)=>LoginPackets.Pad([250,84,type,..Str(msg)]);
        var s=new WorldSession();
        s.Receive(Green("原生模式"),"");
        t.Check(s.CenterMessages.Length==1&&s.PlayerChatLines.Length==0,"flag off (native panel): queued, nothing in chat");
        s.CenterMessagesToChat=true;s.CenterMessageNames=x=>x.Replace("$7","肯特城");
        s.Receive(Green("$7 攻城開始"),"");
        var line=s.PlayerChatLines.LastOrDefault();
        t.Check(s.CenterMessages.Length==2&&line is {Text:"肯特城 攻城開始",ColorIndex:PacketBoxScreenRules.TextColorIndex},"flag on (classic): also printed into chat, names resolved, palette[2]",line);
        int n=s.PlayerChatLines.Length;s.Receive(Green("三號",3),"");
        t.Check(s.PlayerChatLines.Length==n,"type 3 (not shown by the original) not printed");
    }

    // ---- G10：盟屋拍賣／地圖 ----
    [WorldModuleTest("classic-agit")]
    static void Agit(WorldModuleTestContext t){
        var tr=new ClassicAgitTracker();
        t.Check(!tr.Observe(null,null),"nothing yet");
        t.Check(tr.Observe(1,null),"first S156 → show page");
        t.Check(!tr.Observe(1,null),"same request → not again");
        t.Check(tr.Observe(1,5),"S187 arrives → show page");
        t.Check(!tr.Observe(null,5)&&!tr.Observe(1,null),"request cleared or unchanged → not again");
        t.Check(tr.Observe(2,5),"new S156 → show again");
        var h=new AgitHouseEntry((8u<<16)|3,"$1234",78,11,20,1500000);
        t.Equal("古魯丁盟屋　面積 78　截止 11/20　1,500,000 金幣",ClassicAgitTracker.Row(h,x=>x=="$1234"?"古魯丁盟屋":x),"row: name (resolved), area, deadline, price");
        t.Check(ClassicAgitTracker.MapText(new AgitHouseMap(77,(4u<<16)|12)).Contains("區域 4、編號 12"),"map text: region and number");
        t.Check(ClassicAgitTracker.MapText(new AgitHouseMap(77,(9u<<16)|1)).Contains("不送"),"out of the original range → says it won't send");
        t.Equal("select",AgitSelectPackets.Action,"select goes through the existing C125 path (SendAgitAuctionSelect／SendAgitSelect)");
    }

    // ---- G11：1813／1516 確認 ----
    [WorldModuleTest("classic-confirm")]
    static void Confirm(WorldModuleTestContext t){
        t.Check(ClassicConfirm.Allows(null,"x"),"no confirm hook → allowed (old behaviour)");
        var asked=new List<string>();
        t.Check(!ClassicConfirm.Allows(q=>{asked.Add(q);return false;},FriendPanels.DeleteConfirmText)&&asked.SequenceEqual([FriendPanels.DeleteConfirmText]),"answer no → not allowed, asked with 1813 text");
        t.Check(ClassicConfirm.Allows(_=>true,"x"),"answer yes → allowed");
        var prior=ClassicConfirm.Prompt;
        try{
            string? got=null;ClassicConfirm.Prompt=q=>{got=q;return true;};
            t.Check(ClassicConfirm.Ask(null,FishingQuitText.Confirm)&&got==FishingQuitText.Confirm,"Ask uses the injected prompt with the 1516 text");
            ClassicConfirm.Prompt=_=>false;t.Check(!ClassicConfirm.Ask(null,"x"),"injected no");
        }finally{ClassicConfirm.Prompt=prior;}
        t.Check(FriendPanels.DeleteConfirmText.Length>0&&FishingQuitText.Confirm.Length>0,"1813 / 1516 texts available (table or Text.pak fallback)");

        var asked1=new FishingView(1,2,7,10,true,false,"");
        t.Check(FishingQuitRules.StillCurrent(asked1,asked1 with{Revision=11}),"still fishing (revision moved) → send");
        t.Check(!FishingQuitRules.StillCurrent(asked1,asked1 with{Active=false}),"fishing ended while asking → no send");
        t.Check(!FishingQuitRules.StillCurrent(asked1,asked1 with{CancelPending=true}),"already cancelling → no send");
        t.Check(!FishingQuitRules.StillCurrent(asked1,asked1 with{Generation=2})&&!FishingQuitRules.StillCurrent(asked1,asked1 with{Epoch=3})&&!FishingQuitRules.StillCurrent(asked1,asked1 with{Actor=8}),"other session / world / actor → no send");
    }
}
