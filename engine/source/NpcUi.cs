using System.Text.Json;
using P06.NativeUi;
namespace P06;
// R2-F（#622）：npcAmount（S136 數字框 → C11）、npcCastle（城堡頁數字框 → C19／C56／C44）、npcCastleCancel（關城堡頁，不送封包）。
// 這三個指令的 revision＝畫面顯示時的頁版本（npcAmount：InteractionRevision；npcCastle*：QueryRevision），prompt＝詢問版本，value＝數字框的值。
public sealed record NpcWebCommand(string Command,int Revision,int Epoch,int TargetRevision,int Option,ShopChoice[] Choices,uint TargetId=0,long Prompt=0,long Value=0){
    public static NpcWebCommand Parse(JsonElement o){
        string cmd=o.GetProperty("cmd").GetString()??"";var keys=new List<string>{"v","id","cmd","revision","epoch","targetRevision"};if(cmd=="npcSelectTarget")keys.Add("targetId");else if(cmd is "npcOption" or "npcQueryOption")keys.Add("option");else if(cmd=="npcShop")keys.Add("rows");else if(cmd is "npcAmount" or "npcCastle")keys.AddRange(["prompt","value"]);else if(cmd=="npcCastleCancel")keys.Add("prompt");else if(cmd is not ("npcTalk" or "npcClose"))throw new FormatException("command");
        var actual=o.EnumerateObject().Select(p=>p.Name).ToArray();if(!actual.Order().SequenceEqual(keys.Order())||o.GetProperty("v").GetInt32()!=1||o.GetProperty("id").GetInt32()<1)throw new FormatException("fields");int revision=o.GetProperty("revision").GetInt32(),epoch=o.GetProperty("epoch").GetInt32(),target=o.GetProperty("targetRevision").GetInt32();if(revision<0||epoch<0||target<0)throw new FormatException("generation");
        int option=cmd is "npcOption" or "npcQueryOption"?o.GetProperty("option").GetInt32():0;if(option<0)throw new FormatException("option");var rows=new List<ShopChoice>();if(cmd=="npcShop"){var array=o.GetProperty("rows");if(array.ValueKind!=JsonValueKind.Array||array.GetArrayLength() is <1 or >255)throw new FormatException("rows");foreach(var row in array.EnumerateArray()){if(row.ValueKind!=JsonValueKind.Object||!row.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"count","id"}))throw new FormatException("row fields");uint id=row.GetProperty("id").GetUInt32(),count=row.GetProperty("count").GetUInt32();if(count is 0 or >int.MaxValue||rows.Any(r=>r.Id==id))throw new FormatException("row quantity");rows.Add(new(id,count));}}
        long prompt=0,value=0;
        if(cmd is "npcAmount" or "npcCastle" or "npcCastleCancel"){prompt=o.GetProperty("prompt").GetInt64();if(prompt<1)throw new FormatException("prompt");}
        if(cmd is "npcAmount" or "npcCastle"){value=o.GetProperty("value").GetInt64();if(value is <1 or >int.MaxValue)throw new FormatException("value");}
        return new(cmd switch{"npcTalk"=>"talk","npcClose"=>"close","npcOption"=>"option","npcQueryOption"=>"query-option","npcSelectTarget"=>"target","npcAmount"=>"amount","npcCastle"=>"castle","npcCastleCancel"=>"castle-cancel",_=>"shop"},revision,epoch,target,option,rows.ToArray(),cmd=="npcSelectTarget"?o.GetProperty("targetId").GetUInt32():0,prompt,value);
    }
}
/// <summary>T435（#993，classic-ui-audit G14）：WinForms NPC 面板這一輪是否整個讓位給原生窗（拆出來給離線測試）。</summary>
public static class NpcPanelYield {
    /// <summary>
    /// 原本（R4-I）：HYPER_TEXT_WINDOW 接手頁面，而且 NPC 這邊沒有東西、是原生倉庫、或只有頁面。
    /// G14：商店買賣清單在原生 TRADE_WINDOW 時也讓位（ShopTradeWindow 不是 null＝原生窗接上、沒故障、有列）。查詢頁要靠 WinForms 顯示時
    /// （原生 HYPER_TEXT_WINDOW 不可用）不讓位。空清單（原服沒列出物品）不讓位：提示文字照舊只在 NPC 面板。
    /// </summary>
    public static bool OwnsAll(bool nativePages,bool npcOpen,bool nativeWarehouse,bool hasPage,bool nativeShop,bool query)=>
        nativePages&&(!npcOpen||nativeWarehouse||hasPage)||nativeShop&&(nativePages||!query);
}
/// <summary>
/// T435（#993，classic-ui-audit G15）：哪一個 NPC 頁是 S39 agsel。同一個 S39 會同時變成 NPC 頁（NpcInteraction.cs）和
/// AuctionInfo_Window 的請求（AgitAuctionInfoWindow，觀察者）；看到新的請求時，若目前 NPC 對話就是這個看板、而且有頁，那一頁的
/// 版本就是 agsel 頁。之後頁面一換（版本變了）就不是。
/// </summary>
public sealed class AgselPageTracker {
    long seen=-1;int page=-1;
    /// <remarks>
    /// 觀察者比 NPC 對話的處理先跑（WorldExtensions：Observe 先收到），兩者都在 LoginConnection 的 worldCaptureGate 裡；
    /// 呼叫端要在 CaptureWorld 裡讀這幾個值，才不會讀到「有請求、頁還沒換」的中間狀態。
    /// </remarks>
    public void Observe(AgitAuctionInfoRequest? request,uint? interactionNpc,bool hasPage,int interactionRevision){
        if(request==null){seen=-1;page=-1;return;}
        if(request.Revision==seen)return;
        seen=request.Revision;
        page=interactionNpc==request.BoardId&&hasPage?interactionRevision:-1;
    }
    public bool IsAgselPage(int interactionRevision)=>page>=0&&page==interactionRevision;
    /// <summary>
    /// HYPER_TEXT_WINDOW 不放這一頁：是 agsel 頁，而且原生 AuctionInfo_Window 可用、Agitmap_Window 正顯示同一個看板（會由它顯示）。
    /// 推測（真機再確認）：原版把 agsel 交給 CAgitAuctionUI 的路徑沒有追到（AgitAuctionPanel.cs 檔頭）；比照 agapply（HyperTextOmitsNumber），
    /// 使用者關掉 AuctionInfo_Window 後也不改回 HyperText。
    /// </summary>
    public static bool HyperTextOmits(bool agselPage,bool nativeInfoShows)=>agselPage&&nativeInfoShows;
}
/// <summary>T434（#992，classic-ui-audit G4）：classic 以物品學魔法清單的判斷（拆出來給離線測試）。</summary>
public static class ClassicSpellExchange {
    /// <summary>要在 WinForms 顯示的 op 175 清單：有清單、有列、而且原生 TRADE_WINDOW 沒在畫它；否則 null。</summary>
    public static TradeListView? Shown(TradeListView? exchange,bool nativeOwns)=>
        exchange is {Items.Length:>0}&&SpellTradeProtocol.IsExchangeRevision(exchange.Revision)&&!nativeOwns?exchange:null;
    /// <summary>原生窗的學魔法來源目前給的是 op 175 清單（奇數版本、模式 7）。原生窗接著時一律由它負責（使用者關掉也不改回 WinForms，原生行為不變）。</summary>
    public static bool NativeOwns(TradeListView? shown)=>
        shown!=null&&SpellTradeProtocol.IsExchangeRevision(shown.Revision)&&shown.Mode==TradeWindowPanel.ModeSpell;
    /// <summary>勾選的 skillIndex（列 Id）；照清單順序、不重複。</summary>
    public static int[] Choices(IEnumerable<(uint Id,bool Checked)> rows)=>rows.Where(r=>r.Checked&&r.Id<int.MaxValue).Select(r=>(int)r.Id).Distinct().ToArray();
    /// <summary>畫面上的清單（TradeListView.Revision）仍是快照裡的那一份。</summary>
    public static bool Current(SpellExchangeView v,int shownRevision)=>v.OfferRevision!=0&&SpellTradeProtocol.ExchangeRevision(v.OfferRevision)==shownRevision;
}
public sealed partial class ClientForm {
    readonly Button npcTalk=new(){Text="與選取目標對話",Location=new(530,150),Size=new(180,34)};
    readonly Panel npcPanel=new(){Location=new(266,190),Size=new(450,380),BackColor=Color.FromArgb(23,38,41),Visible=false};
    readonly Label npcNotice=new(){Dock=DockStyle.Bottom,Height=72,ForeColor=Color.White,Padding=new(5)};
    readonly FlowLayoutPanel npcContent=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    readonly Button npcClose=new(){Dock=DockStyle.Top,Height=30,Text="關閉對話／取消未送出選擇"};
    readonly Button npcSubmit=new(){Dock=DockStyle.Bottom,Height=34,Text="提交一次（等待原服確認）"};
    readonly List<(uint Id,NumericUpDown Input)> shopInputs=[];int npcUiRevision=-1,npcUiQueryRevision=-1;NpcWebCommand? npcShopStamp;
    string ResolveNpcText(string text)=>(objectAssets??=new(World.Assets.Root)).Name(text);
    void InitInteraction(){
        InitWeaponRepairService();
        string path=Path.Combine(World.Assets.Root,"dialogs/templates.json");if(File.Exists(path))login.World.DialogTemplates=new(path);string skills=Path.Combine(World.Assets.Root,"dialogs/skills.json");if(File.Exists(skills))login.World.SkillCatalog=new(skills);
        npcPanel.Controls.AddRange([npcContent,npcNotice,npcSubmit,npcClose]);Controls.Add(npcTalk);Controls.Add(npcPanel);npcPanel.BringToFront();npcTalk.BringToFront();
        npcTalk.Click+=async(_,_)=>await RunInteraction(CurrentNpcCommand("talk"));npcClose.Click+=async(_,_)=>{if(NpcContextOpen(login.World))await RunInteraction(CurrentNpcCommand("close"));else CloseQueryResult();};
        npcSubmit.Click+=async(_,_)=>{if(npcShopStamp!=null)await RunInteraction(npcShopStamp with{Choices=shopInputs.Where(x=>x.Input.Value>0).Select(x=>new ShopChoice(x.Id,(uint)x.Input.Value)).ToArray()});};
        npcPanel.Enter+=(_,_)=>LeaveGameFocus("npc");npcTalk.Enter+=(_,_)=>LeaveGameFocus("npc-talk");InitCastlePromptUi();
    }
    NpcWebCommand CurrentNpcCommand(string cmd)=>new(cmd,login.World.InteractionRevision,login.World.WorldEpoch,login.World.TargetRevision,0,[]);
    // W2-A: options of a server-pushed self page; Revision carries QueryRevision, not InteractionRevision.
    async Task<bool> RunQueryOption(NpcWebCommand c){ClearInput("npc-command");if(World.Walking)return false;bool ok=await login.QueryOption(c.Option,c.Revision,c.Epoch);SyncInteraction();lastWebState="";PublishWebState();return ok;}
    // R2-F：web 面板的數字框（S136／城堡頁）。實際的版本檢查在 LoginConnection.SendAmountPromptReply／SendCastlePageReply／CancelCastlePage。
    async Task<bool> RunNpcPrompt(NpcWebCommand c){
        ClearInput("npc-prompt");if(c.Command!="castle-cancel"&&World.Walking)return false;
        bool ok=c.Command switch{
            "amount"=>await login.SendAmountPromptReply(c.Epoch,c.Revision,c.Prompt,c.Value),
            "castle"=>await login.SendCastlePageReply(c.Epoch,c.Revision,c.Prompt,c.Value),
            _=>login.CancelCastlePage(c.Epoch,c.Revision,c.Prompt)};
        SyncInteraction();lastWebState="";PublishWebState();return ok;
    }
    async Task<bool> RunInteraction(NpcWebCommand c){if(c.Command=="query-option")return await RunQueryOption(c);if(c.Command is "amount" or "castle" or "castle-cancel")return await RunNpcPrompt(c);if(c.Command=="talk"&&login.World.Target?.ShopActive==true)return await OpenPrivateShopFromTalk(c);ClearInput("npc-command");if(c.Command=="target"){var w=login.World;if(login.State!=LoginState.InWorld||c.Epoch!=w.WorldEpoch||c.TargetRevision!=w.TargetRevision||c.Revision!=w.InteractionRevision||!HasVisibleObjectPixel(c.TargetId))return false;w.SelectTarget(c.TargetId);SyncInteraction();lastWebState="";PublishWebState();return true;}if(c.Command!="close"&&World.Walking||c.Command=="talk"&&(!login.World.TargetId.HasValue||!HasVisibleObjectPixel(login.World.TargetId.Value)))return false;bool sent=await login.Interact(c.Command,c.Revision,c.Epoch,c.TargetRevision,c.Option,c.Choices);SyncInteraction();lastWebState="";PublishWebState();return sent;}
    async Task DispatchNpc(int id,NpcWebCommand c){bool accepted=await RunInteraction(c);Reply(id,accepted?(c.Command=="query-option"?"伺服器頁選項已處理；不會自動重送。":c.Command is "castle" or "castle-cancel"?login.World.CastleNotice:login.World.InteractionNotice):"操作已過期、數量無效或目標無法互動；未送出。");}
    static bool NpcContextOpen(WorldSession w)=>w.InteractionKind!="closed"||w.InteractionHasResult;
    void CloseQueryResult(){if(!login.World.CloseQueryPage())return;SyncInteraction();lastWebState="";PublishWebState();}
    // S39 query results (WIN§2 T140) share the NPC panel (native UI off). Native UI on → HYPER_TEXT_WINDOW (R4-I, SyncNativeHyperText).
    void AddQueryResult(WorldSession w,bool withNpc){
        var page=w.QueryPage!;
        // W2-A: server-pushed self pages (monlist, pledge, ...) have clickable options; player-query results stay read-only.
        bool actionable=w.QueryActionable;string title=actionable?"伺服器頁":"查詢結果";
        npcContent.Controls.Add(new Label{Text=title,AutoSize=true,ForeColor=Color.Khaki,Margin=new(6,6,6,0)});
        npcContent.Controls.Add(new Label{Text=ResolveNpcText(page.Text)+(page.Limitation.Length>0?"\n\n"+page.Limitation:""),AutoSize=true,MaximumSize=new(400,0),ForeColor=Color.White,Margin=new(6)});
        var stamp=new NpcWebCommand("query-option",w.QueryRevision,w.WorldEpoch,w.TargetRevision,0,[]);
        foreach(var option in page.Options){var b=new Button{Text=ResolveNpcText(option.Label),Width=400,AutoSize=true,Enabled=actionable&&option.Enabled};var command=stamp with{Option=option.Id};if(b.Enabled)b.Click+=async(_,_)=>await RunInteraction(command);npcContent.Controls.Add(b);}
        if(withNpc){var close=new Button{Text="關閉"+title,Width=400,Height=28};close.Click+=(_,_)=>CloseQueryResult();npcContent.Controls.Add(close);}
    }
    void SyncInteraction(){
        var w=login.World;w.CheckInteraction();w.CheckNpcSale();npcTalk.Visible=login.State==LoginState.InWorld;npcTalk.Enabled=login.CanTalk&&!World.Walking&&w.TargetId is uint id&&HasVisibleObjectPixel(id);
        // R3-A (#678): with the native UI the warehouse list (S176 / deposit) is WAREHOUSE_WINDOW; the WinForms list is the fallback.
        bool npcOpen=NpcContextOpen(w),query=w.QueryVisible,nativeWarehouse=NativeWarehouseActive(w);
        // R4-I (#712): with the native UI the pages (NPC dialog, query/server page, S136 and castle number boxes) go to HYPER_TEXT_WINDOW;
        // the WinForms panel keeps only what is not a page (shop/service lists, waiting notices). Native UI off → unchanged.
        bool nativePages=SyncNativeHyperText(w,npcOpen,query,nativeWarehouse);npcOpen=NpcContextOpen(w);query=w.QueryVisible;
        // T435（#993，classic-ui-audit G14）：商店買賣 0／1 的清單由原生 TRADE_WINDOW 畫著時，WinForms NPC 面板整個讓位（原本只剩空清單＋停用的提交鈕）。
        bool nativeShop=login.State==LoginState.InWorld&&w.ShopTradeWindow()!=null;
        bool nativeOwnsAll=NpcPanelYield.OwnsAll(nativePages,npcOpen,nativeWarehouse,w.InteractionPage!=null,nativeShop,query);
        npcPanel.Visible=(npcOpen&&!nativeWarehouse||query)&&login.State==LoginState.InWorld&&!nativeOwnsAll;npcClose.Text=npcOpen?"關閉對話／取消未送出選擇":w.QueryActionable?"關閉伺服器頁":"關閉查詢結果";npcNotice.Text=ResolveNpcText(npcOpen||!query?w.InteractionNotice:"");npcSubmit.Visible=w.IsInteractionSelection&&!nativeWarehouse;npcSubmit.Text=w.InteractionTitle+"：提交一次";npcSubmit.Enabled=(w.IsServiceSelection?w.CanSubmitService:w.CanSubmitShop)&&!World.Walking;
        SyncCastlePrompt();SyncSpellExchangeWindow();
        // T434（#992，classic-ui-audit G4）：op 175（以物品學魔法）沒有原生 TRADE_WINDOW 畫的時候，清單放進 WinForms NPC 面板。
        var exchange=login.State==LoginState.InWorld?ClassicSpellExchange.Shown(w.SpellExchangeTradeWindow(),SpellExchangeOwnedByNativeWindow()):null;
        if(exchange!=null)npcPanel.Visible=true;
        long exchangeKey=exchange==null?-1:(uint)HashCode.Combine(exchange.Revision,exchange.Items.Length);
        int queryKey=query?w.QueryRevision:-1;if(npcUiRevision==w.InteractionRevision&&npcUiQueryRevision==queryKey&&npcUiNativePages==nativePages&&npcUiExchangeKey==exchangeKey){SyncClassicSpellExchangeNotice(w);return;}npcUiRevision=w.InteractionRevision;npcUiQueryRevision=queryKey;npcUiNativePages=nativePages;npcUiExchangeKey=exchangeKey;foreach(Control c in npcContent.Controls.Cast<Control>().ToArray())c.Dispose();npcContent.Controls.Clear();shopInputs.Clear();exchangeBoxes.Clear();exchangeNotice=null;var stamp=CurrentNpcCommand("option");npcShopStamp=CurrentNpcCommand("shop");
        if(exchange!=null)AddClassicSpellExchange(w,exchange);
        if(query&&!nativePages)AddQueryResult(w,npcOpen);
        if(!nativePages&&w.InteractionPage is {} page){npcContent.Controls.Add(new Label{Text=ResolveNpcText(page.Text),AutoSize=true,MaximumSize=new(400,0),ForeColor=Color.White,Margin=new(6)});foreach(var option in page.Options){var command=stamp with{Option=option.Id};var b=new Button{Text=ResolveNpcText(option.Label),Width=400,AutoSize=true,Enabled=option.Enabled};b.Click+=async(_,_)=>await RunInteraction(command);npcContent.Controls.Add(b);}}
        if(!nativePages&&w.HtmlNameInput is {} namePrompt)AddHtmlNamePrompt(namePrompt);
        if(!nativeWarehouse)foreach(var row in w.ShopRows){var host=new FlowLayoutPanel{Width=410,Height=66};host.Controls.Add(new Label{Text=$"{ResolveNpcText(row.Name)}\n{w.InteractionPriceLabel} {row.Price:N0} 金幣"+(w.InteractionKind is "sell" or "deposit" or "withdraw"?$" · 最多 {row.Available}":""),ForeColor=Color.White,Width=280,Height=60});var quantity=new NumericUpDown{Width=110,Minimum=0,Maximum=Math.Min(row.Available,int.MaxValue),Value=0,Enabled=row.Available>0};host.Controls.Add(quantity);shopInputs.Add((row.Id,quantity));npcContent.Controls.Add(host);}
    }
    // ---- T434（#992，classic-ui-audit G4）：op 175 以物品學魔法的 WinForms 清單 ----
    // R3-F（#679）把暫行窗拿掉後，--classic-ui 只剩提示文字。這裡把 SpellExchangeTradeWindow 的列（已學的不列，同原版 sub_475C90）
    // 做成勾選清單；送出走既有 LearnSpellExchange（C191），關閉走 CloseSpellExchange（不送封包）。原生 TRADE_WINDOW 畫著這份清單時不出現。
    long npcUiExchangeKey=-1;bool exchangeSending;
    readonly List<(uint Id,CheckBox Box)> exchangeBoxes=[];Label? exchangeNotice;
    /// <summary>原生 TRADE_WINDOW 接上學魔法來源、沒有故障，而且正在畫 op 175 這份清單（沒被關掉）。</summary>
    bool SpellExchangeOwnedByNativeWindow(){
        var panel=NativeUiActive?nativeUiRoot?.Find(TradeWindowPanel.PanelId) as TradeWindowPanel:null;
        if(panel==null||nativeUiRoot==null||nativeUiRoot.Faulted.Contains(panel)||spellTradeSource==null)return false;
        if(panel.Source is not TradeWindowSources s||!s.Sources.Contains(spellTradeSource))return false;
        return ClassicSpellExchange.NativeOwns(spellTradeSource.Current);
    }
    void AddClassicSpellExchange(WorldSession w,TradeListView list){
        npcContent.Controls.Add(new Label{Text="以物品學魔法（勾選後送出一次）",AutoSize=true,ForeColor=Color.Khaki,Margin=new(6,6,6,0)});
        foreach(var item in list.Items){
            var box=new CheckBox{Text=ResolveNpcText(item.Name),AutoSize=true,ForeColor=Color.White,Enabled=item.Enabled,Margin=new(10,2,6,2)};
            box.CheckedChanged+=(_,_)=>SyncClassicSpellExchangeNotice(login.World);
            exchangeBoxes.Add((item.Id,box));npcContent.Controls.Add(box);
        }
        var learn=new Button{Text="以物品學習（送出一次）",Width=400,Height=30,Tag="exchange-learn"};
        var close=new Button{Text="關閉以物品學魔法",Width=400,Height=28};
        int revision=list.Revision;
        learn.Click+=async(_,_)=>{
            if(exchangeSending||World.Walking)return;
            var v=login.SpellExchangeSnapshot();
            var chosen=ClassicSpellExchange.Choices(exchangeBoxes.Select(x=>(x.Id,x.Box.Checked)));
            if(!ClassicSpellExchange.Current(v,revision)||chosen.Length==0)return;
            exchangeSending=true;ClearInput("spell-exchange");SyncInteraction();
            try{await login.LearnSpellExchange(v,chosen);}finally{exchangeSending=false;SyncInteraction();}
        };
        close.Click+=(_,_)=>{login.CloseSpellExchange(login.SpellExchangeSnapshot());SyncInteraction();};
        exchangeNotice=new Label{AutoSize=true,MaximumSize=new(400,0),ForeColor=Color.White,Margin=new(6)};
        npcContent.Controls.AddRange([learn,close,exchangeNotice]);
        SyncClassicSpellExchangeNotice(w);
    }
    void SyncClassicSpellExchangeNotice(WorldSession w){
        if(exchangeNotice==null)return;
        exchangeNotice.Text=ResolveNpcText(w.SpellExchangeNotice);
        bool any=exchangeBoxes.Any(x=>x.Box.Checked);
        foreach(var (_,box) in exchangeBoxes)box.Enabled=!exchangeSending;
        foreach(Control c in npcContent.Controls)if(c is Button {Tag:"exchange-learn"} b)b.Enabled=any&&!exchangeSending&&!World.Walking&&login.SpellExchangeSnapshot().CanSend;
    }
    // R7-D (#806): WinForms fallback (native UI off) for the HTML clan-name box; the page revision changes on open/close, so this rebuilds.
    void AddHtmlNamePrompt(HtmlNamePrompt prompt){
        var row=new FlowLayoutPanel{Width=410,Height=60};
        var box=new TextBox{Width=200,MaxLength=HtmlActions.NameBytes};
        var ok=new Button{Text="確定",AutoSize=true};var cancel=new Button{Text="取消",AutoSize=true};
        ok.Click+=async(_,_)=>{if(!login.HtmlNameCanSend||World.Walking)return;await login.SubmitHtmlName(prompt.Revision,box.Text);};
        cancel.Click+=(_,_)=>login.CancelHtmlName(prompt.Revision);
        row.Controls.Add(new Label{Text=prompt.Prompt,AutoSize=true,MaximumSize=new(400,0),ForeColor=Color.White});row.Controls.AddRange([box,ok,cancel]);
        npcContent.Controls.Add(row);
    }
    // ---- R4-I (#712): page routing to the native HYPER_TEXT_WINDOW (NativeUi/HyperTextPanel.cs) ----
    bool npcUiNativePages;
    readonly HyperTextRouter hyperTextRouter=new();
    NativeHyperTextHost? hyperTextHost;
    /// <summary>The native page window when the native UI is active and the panel is not faulted; otherwise null (WinForms path).</summary>
    internal HyperTextPanel? NativeHyperText()=>NativeUiActive&&NativeUiRoot is {} root&&root.Find(HyperTextPanel.PanelId) is HyperTextPanel p&&!root.Faulted.Contains(p)?p:null;
    /// <summary>
    /// Pushes the current page to HYPER_TEXT_WINDOW. One window (op 144): whichever of the NPC page and the query page changed last is
    /// shown; the other page is closed locally (no packet) because the original window replaced its content. Returns true when the
    /// native window owns the pages (the WinForms panel then skips them).
    /// </summary>
    bool SyncNativeHyperText(WorldSession w,bool npcOpen,bool query,bool nativeWarehouse){
        if(NativeHyperText() is not {} panel){hyperTextRouter.Reset();return false;}
        if(panel.Host==null)panel.Attach(hyperTextHost??=new NativeHyperTextHost(this));
        bool inWorld=login.State==LoginState.InWorld;
        // T435（#993，G15）：agsel 頁由原生 AuctionInfo_Window 顯示時不再同時進 HYPER_TEXT_WINDOW。這一頁在本機關掉（不送封包，
        // 同下面「另一頁被取代」的做法），對話不會卡在看不到的頁上；之後的 S136 agapply 照舊開 Bid_Window。
        var agsel=login.CaptureWorld(()=>{var r=AgitAuctionInfoWindow.Current;agselPages.Observe(r,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);return r;});
        if(inWorld&&w.InteractionPage!=null&&AgselPageTracker.HyperTextOmits(agselPages.IsAgselPage(w.InteractionRevision),NativeAgitInfoShows(agsel))){
            login.CaptureWorld(()=>{w.CloseInteraction("拍賣資訊顯示在盟屋拍賣資訊窗。");return 0;});npcOpen=NpcContextOpen(w);
        }
        bool npcPage=inWorld&&npcOpen&&!nativeWarehouse&&w.InteractionPage!=null,queryPage=inWorld&&query;
        var front=hyperTextRouter.Pick(npcPage,w.InteractionRevision,queryPage,w.QueryRevision);
        if(front==HyperTextSource.Query&&npcPage){w.CloseInteraction();npcPage=false;}
        else if(front==HyperTextSource.Npc&&queryPage){w.CloseQueryPage();queryPage=false;}
        if(!npcPage&&!queryPage)front=null;
        panel.Show(front switch{HyperTextSource.Npc=>NpcHyperTextView(w),HyperTextSource.Query=>QueryHyperTextView(w),_=>null});
        if(front!=null)NativeUiRoot?.BringToFront(panel);
        return true;
    }
    HyperTextView NpcHyperTextView(WorldSession w){
        var page=w.InteractionPage!;
        HyperTextNumberBox? number=null;
        var amount=login.AmountPromptSnapshot();
        if(amount.Prompt is {} o&&o.InteractionRevision==w.InteractionRevision&&!HyperTextOmitsNumber(o.Html,NativeAgitBidAvailable()))
            number=new(HyperTextNumberKind.Amount,o.Revision,o.Lower,o.Max,o.Suggested,o.Answerable,amount.CanSend&&!World.Walking,AmountPromptProtocol.Range(o));
        return new(HyperTextSource.Npc,w.InteractionRevision,ResolveNpcText(page.Text),page.Options.Select(x=>new HyperTextLink(x.Id,ResolveNpcText(x.Label),x.Enabled)).ToArray(),ResolveNpcText(w.InteractionNotice),number,HtmlNameBox(w,query:false));
    }
    /// <summary>
    /// R10-O (#914, WIN2§5.3): the original hands S136 `agapply` to CAgitAuctionUI (Bid_Window), never to the HyperText page. So while
    /// the native Bid_Window can show it, the page carries no number box for agapply — also after the user closed Bid_Window (the
    /// original has no second box either). Native UI off, or Bid_Window faulted / unusable (missing AgitAuctionUI.xml) → page box as before.
    /// </summary>
    internal static bool HyperTextOmitsNumber(string html,bool nativeBidAvailable)=>nativeBidAvailable&&html==AgitBidPanel.ApplyHtml;
    readonly AgselPageTracker agselPages=new();
    /// <summary>T435（G15）：原生 AuctionInfo_Window 會顯示這個 agsel：兩個窗都沒故障、AuctionInfo 可用、Agitmap_Window 開著同一個看板。</summary>
    bool NativeAgitInfoShows(AgitAuctionInfoRequest? request)=>request!=null&&NativeUiActive&&NativeUiRoot is {} root
        &&root.Find(AgitAuctionInfoPanel.PanelId) is AgitAuctionInfoPanel info&&!root.Faulted.Contains(info)&&info.Available
        &&root.Find(AgitAuctionPanel.PanelId) is AgitAuctionPanel auction&&!root.Faulted.Contains(auction)&&auction.Visible&&auction.Shown?.List.BoardId==request.BoardId;
    bool NativeAgitBidAvailable()=>NativeUiActive&&NativeUiRoot is {} root&&root.Find(AgitBidPanel.PanelId) is AgitBidPanel p&&!root.Faulted.Contains(p)&&p.Available;
    // R7-D (#806): the in-page clan-name box opened by the HTML actions surrender / cease / pledgefocusadd (NpcInteraction.cs).
    HyperTextNameBox? HtmlNameBox(WorldSession w,bool query)=>w.HtmlNameInput is {} p&&p.Query==query?new(p.Revision,p.Prompt,login.HtmlNameCanSend&&!World.Walking):null;
    HyperTextView QueryHyperTextView(WorldSession w){
        var page=w.QueryPage!;
        HyperTextNumberBox? number=null;
        if(w.CastlePageOffer is {} c)number=new(HyperTextNumberKind.Castle,c.Revision,c.Min,c.Max,CastleProtocol.Initial(c),true,login.CastleSnapshot().CanSend&&!World.Walking,CastleProtocol.Range(c));
        string notice=string.Join("\n",new[]{ResolveNpcText(page.Limitation),number!=null?w.CastleNotice:""}.Where(s=>s.Length>0));
        return new(HyperTextSource.Query,w.QueryRevision,ResolveNpcText(page.Text),page.Options.Select(x=>new HyperTextLink(x.Id,ResolveNpcText(x.Label),x.Enabled&&w.QueryActionable)).ToArray(),notice,number,HtmlNameBox(w,query:true));
    }
    /// <summary>The panel's actions go through the same paths as the WinForms/web panel (RunInteraction / RunNpcPrompt); stale pages do nothing.</summary>
    sealed class NativeHyperTextHost(ClientForm form):IHyperTextHost {
        WorldSession W=>form.login.World;
        bool Current(HyperTextView shown)=>shown.Source==HyperTextSource.Npc?shown.Revision==W.InteractionRevision:shown.Revision==W.QueryRevision;
        public void Link(HyperTextView shown,int option){
            if(!Current(shown))return;
            var w=W;string cmd=shown.Source==HyperTextSource.Npc?"option":"query-option";
            _=form.RunInteraction(new NpcWebCommand(cmd,shown.Revision,w.WorldEpoch,w.TargetRevision,option,[]));
        }
        public void Submit(HyperTextView shown,long value){
            if(!Current(shown)||shown.Number is not {} n)return;
            var w=W;string cmd=n.Kind==HyperTextNumberKind.Amount?"amount":"castle";
            _=form.RunInteraction(new NpcWebCommand(cmd,shown.Revision,w.WorldEpoch,w.TargetRevision,0,[],0,n.Revision,value));
        }
        public void CancelNumber(HyperTextView shown){
            if(!Current(shown)||shown.Number is not {Kind:HyperTextNumberKind.Castle} n)return;
            var w=W;_=form.RunInteraction(new NpcWebCommand("castle-cancel",shown.Revision,w.WorldEpoch,w.TargetRevision,0,[],0,n.Revision));
        }
        public void SubmitName(HyperTextView shown,string name){if(Current(shown)&&shown.Name is {} n)_=form.login.SubmitHtmlName(n.Revision,name);}
        public void CancelName(HyperTextView shown){if(Current(shown)&&shown.Name is {} n)form.login.CancelHtmlName(n.Revision);}
        public void Close(HyperTextView shown){
            // Close() may come from UiRoot.Evict in the middle of a frame: do the state change after it (UI thread).
            if(form.IsHandleCreated&&!form.IsDisposed)form.BeginInvoke((Action)ClosePages);else ClosePages();
        }
        void ClosePages(){
            // One window: closing it closes every page in it (an NPC page and a query page), never a shop/service list.
            var w=W;
            if(w.InteractionPage!=null&&NpcContextOpen(w))_=form.RunInteraction(form.CurrentNpcCommand("close"));
            if(w.QueryVisible)form.CloseQueryResult();
            form.ReturnGameFocus();
        }
    }
    object InteractionWeb(){var w=login.World;w.CheckNpcSale();return new{targets=objectVisuals.Where(v=>w.Objects.Any(o=>o.ObjectId==v.ObjectId&&!o.Dead)&&HasVisibleObjectPixel(v.ObjectId)).Select(v=>new{id=v.ObjectId,name=v.Name}),model=w.InteractionSnapshot(),canTalk=login.CanTalk&&!World.Walking&&w.TargetId is uint id&&HasVisibleObjectPixel(id),text=ResolveNpcText(w.InteractionPage?.Text??""),notice=ResolveNpcText(w.InteractionNotice),options=w.InteractionPage?.Options.Select(o=>new{id=o.Id,label=ResolveNpcText(o.Label),enabled=o.Enabled}),rows=w.ShopRows.Select(r=>new{id=r.Id,name=ResolveNpcText(r.Name),price=r.Price,available=r.Available}),queryText=w.QueryVisible?ResolveNpcText(w.QueryPage!.Text):null,
        // R2-F：web 面板顯示伺服器頁／城堡頁與兩種數字框。
        query=w.QueryVisible?new{revision=w.QueryRevision,html=w.QueryHtml,actionable=w.QueryActionable,notice=ResolveNpcText(w.QueryPage!.Limitation),options=w.QueryPage.Options.Select(o=>new{id=o.Id,label=ResolveNpcText(o.Label),enabled=o.Enabled&&w.QueryActionable})}:null,
        amount=AmountPromptWeb(),castle=CastlePromptWeb()};}
}
