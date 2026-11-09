namespace P06;
public sealed record ShopRow(uint Id,ushort Icon,uint Price,string Name,uint Available,string StatusHex);
public sealed record ShopChoice(uint Id,uint Count);
public static class NpcPackets {
    public static byte[] Talk(uint id)=>LoginPackets.Pad([34,..BitConverter.GetBytes(id)]);
    public static byte[] Action(uint id,string action){if(action.Length is <1 or >200||action.Any(char.IsControl))throw new ArgumentException("action");return LoginPackets.Pad([125,..BitConverter.GetBytes(id),..LoginPackets.TextEncoding.GetBytes(action),0]);}
    public static byte[] Shop(uint npc,bool sell,IReadOnlyList<ShopChoice> rows){if(rows.Count is <1 or >255||rows.Select(r=>r.Id).Distinct().Count()!=rows.Count||rows.Any(r=>r.Count is 0 or >int.MaxValue))throw new ArgumentException("shop rows");using var s=new MemoryStream();using var w=new BinaryWriter(s);w.Write((byte)161);w.Write(npc);w.Write((byte)(sell?1:0));w.Write((byte)rows.Count);w.Write((byte)0);foreach(var r in rows){w.Write(r.Id);w.Write(r.Count);}return LoginPackets.Pad(s.ToArray());}
}
public sealed partial class WorldSession {
    public NpcTemplates? DialogTemplates {get;set;}
    public int InteractionRevision {get;private set;}
    public uint? InteractionNpc {get;private set;}
    int interactionEpoch,interactionTargetRevision;
    public string InteractionKind {get;private set;}="closed";
    // Local recovery policy, not a server acknowledgement or the original BIN's timeout.
    public const int InteractionTimeoutMs=15000;
    internal Func<long> InteractionClock=()=>Environment.TickCount64;
    long? interactionWaitStarted;
    public bool CanStartInteraction=>InteractionKind is "closed" or "timedout";
    public bool InteractionHasResult {get;private set;}
    public string InteractionNotice {get;private set;}="選取附近目標後可開啟對話。";
    public NpcPage? InteractionPage {get;private set;}
    public ShopRow[] ShopRows {get;private set;}=[];
    string[] dialogArguments=[];string dialogNpcName="";ushort shopCurrency;
    // WorldInteractionGuard.npcTalk/npcAction: same visible world, Chebyshev distance <= 5.
    public bool NearbyTarget=>Ready&&!OwnDead&&Status?.Hp>0&&Actor is {} a&&Target is {Dead:false,AppearancePending:false} t&&Math.Abs(a.X-t.X)<=5&&Math.Abs(a.Y-t.Y)<=5;
    public bool InteractionValid=>InteractionNpc!=null&&interactionEpoch==WorldEpoch&&interactionTargetRevision==TargetRevision&&TargetId==InteractionNpc&&NearbyTarget;
    public void CloseInteraction(string notice="對話已關閉。"){ClearNpcService();interactionWaitStarted=null;InteractionHasResult=false;InteractionRevision++;InteractionNpc=null;InteractionKind="closed";InteractionPage=null;ShopRows=[];dialogArguments=[];InteractionNotice=notice;}
    public void CheckInteraction(){
        if(InteractionNpc!=null&&!InteractionValid){CloseInteraction("目標、位置或世界已改變，舊對話已失效。");return;}
        if(InteractionKind is "waiting" or "submitted"&&interactionWaitStarted is long started&&InteractionClock()-started>=InteractionTimeoutMs){
            CloseInteraction("等待伺服器回覆已逾時，這不表示操作成功或失敗。請核對背包、金額與所在位置，再重新對話；不會自動重送。");
            InteractionKind="timedout";
        }
    }
    internal void BeginTalk(){CloseInteraction();InteractionNpc=TargetId;interactionEpoch=WorldEpoch;interactionTargetRevision=TargetRevision;InteractionKind="waiting";interactionWaitStarted=InteractionClock();InteractionNotice="已要求對話，等待原服回包；可隨時關閉，不會自動重送。";}
    internal void AwaitInteraction(bool purchase=false){InteractionRevision++;InteractionKind=purchase?"submitted":"waiting";interactionWaitStarted=InteractionClock();InteractionPage=null;ShopRows=[];InteractionNotice=purchase?"交易請求已送出，尚未確認成交；等待背包與金額回包，不會自動重送。":"選項已送出，等待原服回包；可隨時關閉，不會自動重送。";}
    internal void InteractionInventoryUpdate(byte opcode){if(InteractionKind=="submitted"&&opcode is 5 or 15 or 24 or 242 or 57 or 100){if(pendingNpcReply=="skills"){InteractionSkillsUpdated();return;}InteractionNotice="送出後收到背包更新；請核對物品與金額，這不代表完整交易已確認。重新對話才能再次提交。";InteractionRevision++;}}
    internal void InteractionMessage(){
        CheckInteraction();if(InteractionNpc==null)return;
        string guidance=LastServerReply is {Recognized:true,Id:403}?"請核對背包與金額；再次操作請重新對話。":LastServerReply is {Recognized:true,Id:189}?"請備妥金幣後重新對話。":"舊操作已失效，請重新對話。";
        CloseInteraction((LastServerMessage??"收到伺服器訊息")+"\n"+guidance);InteractionHasResult=true;
    }
    public bool OpenDialogLink(int option,int revision){CheckInteraction();if(!InteractionValid||revision!=InteractionRevision||InteractionKind!="dialog")return false;var o=InteractionPage?.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind=="link");if(o==null)return false;ShowPage(o.Value);return true;}
    void ShowPage(string key,string[]? fallback=null){ClearNpcService();InteractionRevision++;InteractionKind="dialog";ShopRows=[];InteractionPage=RenderHyperText(key,dialogArguments,dialogNpcName,fallback);InteractionNotice=InteractionPage.Limitation;}
    // WIN§2.1: one HYPER_TEXT_WINDOW shows NPC pages and S39 query results. The native layout waits for T190;
    // until then known query pages without a template are shown as decoded plain text, never as markup.
    public const string HyperTextFallbackNotice="查詢結果以解碼後的文字顯示；原版 HYPER_TEXT_WINDOW 版面尚未實作。";
    NpcPage RenderHyperText(string key,string[] args,string npc,string[]? fallback){
        var page=DialogTemplates?.Render(key,args,npc,Actor?.Name??"");
        bool missing=page==null||page.Options.Length==0&&page.Text=="缺少此對話的原始繁中模板："+key;
        if(missing&&fallback!=null)return new(string.Join("\n",fallback),[],HyperTextFallbackNotice);
        return page??new("對話資源尚未載入。",[],"");
    }
    string[]? dialogFallback;
    // Consumed by the very next S39 dispatch (same packet); see ReceivePlayerQueryHtml.
    internal void OfferDialogFallback(string[] lines)=>dialogFallback=lines;
    // Self-addressed S_HYPERTEXT results (y_who, pc_show, /rank pages). Kept apart from the NPC context so
    // a pending dialog, shop or submitted order is never replaced; both share the NPC panel.
    public NpcPage? QueryPage {get;private set;}
    public string QueryHtml {get;private set;}="";
    public int QueryRevision {get;private set;}
    int queryEpoch;
    public bool QueryVisible=>QueryPage!=null&&Ready&&queryEpoch==WorldEpoch;
    internal void ShowQueryPage(uint self,string html,string[] args,string[] lines){
        var page=RenderHyperText(html,args,Actor?.Name??"",lines);
        // Player-query results (y_who, pc_show, /rank): links/actions stay visible but disabled.
        if(page.Options.Length>0)page=page with{Options=page.Options.Select(o=>o with{Enabled=false}).ToArray(),Limitation=string.Join("\n",new[]{page.Limitation,"查詢結果頁的選項尚未支援。"}.Where(s=>s.Length>0))};
        QueryPage=page;QueryHtml=html;queryEpoch=WorldEpoch;QueryActionable=false;queryArguments=[];QueryRevision++;
    }
    public bool CloseQueryPage(){if(QueryPage==null)return false;QueryPage=null;QueryHtml="";QueryActionable=false;queryArguments=[];QueryRevision++;return true;}
    // W2-A（T234／#556，windows-2.md §4）：伺服器主動送、物件 id 是自己的 S39（3.81 S_ShowPolyList 的 monlist、pledge 等）。
    // 原版一律交給同一個 NPC 對話窗（S_HYPERTEXT sub_940030 → sub_BBCB00）；id 剛好是 "monlist" 時走一般 HTML（sub_874A10）。
    // 這裡放在查詢頁（與 NPC 對話共用面板），不取代進行中的 NPC 對話／交易，也不受選取目標改變影響。
    // 選項可點：link 本地換頁（沿用同一組參數），action 回 C125，物件 id＝封包裡的自己（與 NPC 頁同一路徑）。
    public bool QueryActionable {get;private set;}
    uint queryObject;string[] queryArguments=[];
    void ReceiveServerPage(uint self,string html,string[] args,string[]? fallback){
        if(html.Length==0){if(QueryActionable)CloseQueryPage();return;}
        ShowServerPage(self,html,args,fallback);
    }
    void ShowServerPage(uint self,string html,string[] args,string[]? fallback){
        QueryPage=RenderHyperText(html,args,Actor?.Name??"",fallback);QueryHtml=html;queryEpoch=WorldEpoch;queryObject=self;queryArguments=args;QueryActionable=true;QueryRevision++;
    }
    NpcOption? FindQueryOption(int option,int revision,string kind)=>QueryVisible&&QueryActionable&&revision==QueryRevision?QueryPage!.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind==kind):null;
    public bool OpenQueryLink(int option,int revision){if(FindQueryOption(option,revision,"link") is not {} o)return false;ShowServerPage(queryObject,o.Value,queryArguments,null);return true;}
    // HyperAct "checkpk" is handled by the client itself (C51), same as on NPC pages.
    public byte[]? QueryActionPacket(int option,int revision){
        if(FindQueryOption(option,revision,"action") is not {} o||queryObject==0||Status?.ObjectId!=queryObject)return null;
        return string.Equals(o.Value,CheckPkAction,StringComparison.OrdinalIgnoreCase)?WeaponRepairPackets.CheckPk():NpcPackets.Action(queryObject,o.Value);
    }
    // 送出後關頁、不自動重送；伺服器若回新頁會再開（推測：原版點 action 後的關窗時機待驗證）。
    internal void QueryActionSent(int revision){if(revision==QueryRevision&&QueryActionable)CloseQueryPage();}
    // HyperAct name table (TW16 FO 0xDA78A4 "checkpk", next to "fix"/"getall"/"exitghost"): the client handles
    // this action itself and sends the field-less C_CHECK_PK instead of C_HACTION.
    public const string CheckPkAction="checkpk";
    /// <summary>R2-F：目前對話 NPC 的外型（物件 gfx）；對話無效時為 null。xchgspell／buyspell 依此分流（SS2§1.1）。</summary>
    public ushort? InteractionNpcGfx=>InteractionValid&&Target is {} t&&t.ObjectId==InteractionNpc?t.Gfx:null;
    /// <summary>伺服器主動頁上某個可用 action 選項的動作字串；不存在或版本不符為 null。</summary>
    public string? QueryActionValue(int option,int revision)=>FindQueryOption(option,revision,"action")?.Value;
    bool ReceiveInteraction(byte opcode,LoginReader r){
        string[]? fallback=dialogFallback;dialogFallback=null;
        if(ReceiveNpcService(opcode,r))return true;
        if(opcode is not (39 or 70 or 65))return false;
        uint npc=r.D();CheckInteraction();bool accept=InteractionValid&&npc==InteractionNpc;
        bool selfPage=opcode==39&&!accept&&npc!=0&&Ready&&Status?.ObjectId==npc;
        if(opcode==39){string html=r.S();
            // Fixed d287 S_Bonusstats writes only C39, self D, and S("RaiseAttr").
            if(html=="RaiseAttr"){
                r.Finish();if(npc==0||Status?.ObjectId!=npc)throw new ProtocolException("bonus stats self identity");
                ReceiveBonusOffer(npc);return true;
            }
            ushort flag=r.H(),count=r.H();if(flag>1||count>256||flag==0&&count!=0)throw new ProtocolException("dialog arguments");var args=new string[count];for(int i=0;i<count;i++)args[i]=r.S();r.Finish();if(selfPage){ReceiveServerPage(npc,html,args,fallback);return true;}if(!accept)return true;if(html.Length==0){CloseInteraction("伺服器已關閉對話。");return true;}dialogArguments=args;dialogNpcName=Target?.Name??"";ShowPage(html,fallback);return true;}
        int n=r.H();if(n>2048)throw new ProtocolException("shop list too large");var rows=new List<ShopRow>();var ids=new HashSet<uint>();
        for(int i=0;i<n;i++){uint id=r.D();if(!ids.Add(id))throw new ProtocolException("duplicate shop identity");ushort icon=opcode==70?r.H():(ushort)0;uint price=r.D();if(price>int.MaxValue)throw new ProtocolException("shop price");string name=opcode==70?r.S():inventory.GetValueOrDefault(id)?.Name??"未持有物件 "+id;string status=opcode==70?Bytes(r,r.C()):"";uint available=opcode==70?int.MaxValue:inventory.GetValueOrDefault(id)?.Count??0;rows.Add(new(id,icon,price,name,available,status));}
        // Lists have no request ID: never let a duplicate reply reopen a submitted order.
        ushort currency=opcode==70&&n>0?r.H():(ushort)7;r.Finish();if(!accept||InteractionKind!="waiting"||pendingNpcReply!="")return true;InteractionRevision++;InteractionKind=opcode==70?"buy":"sell";InteractionPage=null;ShopRows=rows.ToArray();shopCurrency=currency;InteractionNotice=n==0?(opcode==65?"原服未列出可出售給此商人的物品。":"原服目前沒有列出可購買的物品。"):currency==7?"請設定數量並明確提交一次；結果以原服背包與金額為準。":"尚未支援此商店的貨幣，提交已停用。";return true;
    }
    public bool CanSubmitShop=>InteractionValid&&InteractionKind is "buy" or "sell"&&shopCurrency==7&&ShopRows.Length>0;
    public bool ValidateShop(IReadOnlyList<ShopChoice> choices){if(!CanSubmitShop||choices.Count is <1 or >255||choices.Select(c=>c.Id).Distinct().Count()!=choices.Count)return false;long total=0;foreach(var c in choices){var row=ShopRows.FirstOrDefault(r=>r.Id==c.Id);if(row==null||c.Count is 0 or >int.MaxValue)return false;if(InteractionKind=="sell"&&(c.Count>row.Available||!inventory.TryGetValue(c.Id,out var item)||c.Count>item.Count))return false;total+=(long)c.Count*row.Price;if(total>int.MaxValue)return false;}return true;}
    public object InteractionSnapshot()=>new{revision=InteractionRevision,epoch=WorldEpoch,targetRevision=TargetRevision,npcId=InteractionNpc,targetId=TargetId,kind=InteractionKind,hasResult=InteractionHasResult,notice=InteractionNotice,text=InteractionPage?.Text,options=InteractionPage?.Options.Select(o=>new{id=o.Id,label=o.Label,enabled=o.Enabled,kind=o.Kind}),rows=ShopRows.Select(r=>new{id=r.Id,icon=r.Icon,price=r.Price,name=r.Name,available=r.Available}),currency=shopCurrency,title=InteractionTitle,priceLabel=InteractionPriceLabel,selection=IsInteractionSelection,feePerRow=InteractionKind=="withdraw",canSubmit=CanSubmitShop||CanSubmitService,query=QueryVisible?new{revision=QueryRevision,html=QueryHtml,text=QueryPage!.Text,notice=QueryPage.Limitation,actionable=QueryActionable,options=QueryPage.Options.Select(o=>new{id=o.Id,label=o.Label,enabled=o.Enabled&&QueryActionable,kind=o.Kind})}:null};
}
public sealed partial class LoginConnection {
    bool interactionSending;
    public bool CanTalk=>State==LoginState.InWorld&&!World.ActionLocked&&World.NearbyTarget&&!World.TradeBlocksActions&&!MovementBusy&&!interactionSending&&World.CanStartInteraction;
    // W2-A：伺服器主動頁的選項。link 本地換頁；action 送一次 C125（或 checkpk 的 C51），送出後關頁，不重送。
    public async Task<bool> QueryOption(int option,int revision,int epoch){
        if(State!=LoginState.InWorld||epoch!=World.WorldEpoch||revision!=World.QueryRevision||!World.QueryVisible||!World.QueryActionable)return false;
        if(World.OpenQueryLink(option,revision))return true;
        if(interactionSending||MovementBusy||sendMovementGate==null||World.ActionLocked||World.TradeBlocksActions)return false;
        // R2-F：exitghost 由客戶端自己處理（R2-D 的 TryHyperAction 送 C173），不送 C125；送出後照其他動作關頁。
        if(World.QueryActionValue(option,revision) is {} hyper&&TryHyperAction(hyper,out var ghost)){
            bool ok=await ghost;if(ok)World.QueryActionSent(revision);return ok;
        }
        if(World.QueryActionPacket(option,revision) is not {} packet)return false;
        int session=generation;var sender=sendMovementGate;interactionSending=true;
        bool Valid()=>session==generation&&State==LoginState.InWorld&&epoch==World.WorldEpoch&&revision==World.QueryRevision&&World.QueryVisible&&World.QueryActionable&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
        try{bool sent=await sender(packet,Valid,()=>World.QueryActionSent(revision));return sent&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"互動傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(session==generation)interactionSending=false;}
    }
    /// <summary>
    /// R2-F（#622）：NPC 頁上由客戶端自己處理的 HyperAct（伺服器不認這些字串，SS2§1.1、WO §4）。回 null＝一般動作，照舊送 C125。
    /// exitghost → R2-D 的 TryHyperAction（幽靈中才送 C173）；xchgspell → 外型 834 才送 C245；buyspell 在外型 834 時不送（只有 ≠ 834 才走原本的金幣學魔法）。
    /// </summary>
    Task<bool>? ClientHyperAction(int option){
        if(!World.InteractionValid||World.InteractionKind!="dialog")return null;
        var selected=World.InteractionPage?.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind=="action");if(selected==null)return null;
        string action=selected.Value.Trim();
        if(TryHyperAction(action,out var ghost))return ghost;
        ushort gfx=World.InteractionNpcGfx??0;
        if(!SpellExchangeProtocol.ActionAllowed(action,gfx))return Task.FromResult(false);
        if(action==SpellExchangeProtocol.ExchangeAction)return RequestSpellExchangeList(openWindow:false);
        return null;
    }
    public Task<bool> Interact(string command,int revision,int epoch,int targetRevision,int option=0,IReadOnlyList<ShopChoice>? choices=null)=>InteractCore(command,revision,epoch,targetRevision,option,choices,null);
    async Task<bool> InteractCore(string command,int revision,int epoch,int targetRevision,int option,IReadOnlyList<ShopChoice>? choices,Func<bool>? pointerCurrent){
        if(pointerCurrent?.Invoke()==false)return false;
        World.CheckInteraction();if(command!="close"&&World.TradeBlocksActions)return false;if(State!=LoginState.InWorld||revision!=World.InteractionRevision||epoch!=World.WorldEpoch||targetRevision!=World.TargetRevision)return false;
        if(command=="close"){World.CloseInteraction();Set(State,World.InteractionNotice);return true;}
        if(command=="talk"&&BoardPackets.IsBoard(World.Target))return await OpenBoardUi(pointerCurrent);
        if(command=="option"&&World.OpenDialogLink(option,revision)){Set(State,World.InteractionNotice);return true;}
        if(command=="option"&&World.OpenDeposit(option,revision)){Set(State,World.InteractionNotice);return true;}
        if(interactionSending||MovementBusy||sendMovementGate==null)return false;
        if(command=="option"&&ClientHyperAction(option) is {} local)return await local;
        byte[] packet;bool shop=command=="shop";string service="";
        if(command=="talk"){if(!CanTalk)return false;packet=NpcPackets.Talk(World.TargetId!.Value);}
        else if(command=="option"){var selected=World.InteractionPage?.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind=="action");if(!World.InteractionValid||selected==null||World.InteractionKind!="dialog")return false;service=selected.Value switch{"buyspell"=>"skill-list","retrieve"=>"withdraw-list",_=>""};packet=service=="skill-list"?ServicePackets.SkillList(World.InteractionNpc!.Value):string.Equals(selected.Value,WorldSession.CheckPkAction,StringComparison.OrdinalIgnoreCase)?WeaponRepairPackets.CheckPk():NpcPackets.Action(World.InteractionNpc!.Value,selected.Value);}
        else if(shop){if(choices==null)return false;service=World.IsServiceSelection?World.InteractionKind:"";if(service!=""?!World.ValidateService(choices):!World.ValidateShop(choices))return false;choices=choices.ToArray();packet=service=="skills"?ServicePackets.Learn(choices):service is "deposit" or "withdraw"?ServicePackets.Warehouse(World.InteractionNpc!.Value,service=="withdraw",choices):NpcPackets.Shop(World.InteractionNpc!.Value,World.InteractionKind=="sell",choices);}
        else return false;
        int session=generation;var sender=sendMovementGate;interactionSending=true;
        bool Valid()=>(pointerCurrent?.Invoke()??true)&&session==generation&&epoch==World.WorldEpoch&&targetRevision==World.TargetRevision&&revision==World.InteractionRevision&&State==LoginState.InWorld&&!World.ActionLocked&&World.NearbyTarget&&!World.TradeBlocksActions&&!MovementBusy&&(command=="talk"?World.CanStartInteraction:World.InteractionValid)&&(!shop||(service!=""?World.ValidateService(choices!):World.ValidateShop(choices!)));
        try{bool sent=await sender(packet,Valid,()=>{if(command=="talk")World.BeginTalk();else if(service!="")World.AwaitNpcService(service,choices);else World.AwaitInteraction(shop);Set(State,World.InteractionNotice);});return sent&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"互動傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(session==generation)interactionSending=false;}
    }
}
