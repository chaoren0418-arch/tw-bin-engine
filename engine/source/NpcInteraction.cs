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
    void ShowPage(string key){ClearNpcService();InteractionRevision++;InteractionKind="dialog";ShopRows=[];InteractionPage=DialogTemplates?.Render(key,dialogArguments,dialogNpcName,Actor?.Name??"")??new("對話資源尚未載入。",[],"");InteractionNotice=InteractionPage.Limitation;}
    bool ReceiveInteraction(byte opcode,LoginReader r){
        if(ReceiveNpcService(opcode,r))return true;
        if(opcode is not (39 or 70 or 65))return false;
        uint npc=r.D();CheckInteraction();bool accept=InteractionValid&&npc==InteractionNpc;
        if(opcode==39){string html=r.S();
            // Fixed d287 S_Bonusstats writes only C39, self D, and S("RaiseAttr").
            if(html=="RaiseAttr"){
                r.Finish();if(npc==0||Status?.ObjectId!=npc)throw new ProtocolException("bonus stats self identity");
                ReceiveBonusOffer(npc);return true;
            }
            ushort flag=r.H(),count=r.H();if(flag>1||count>256||flag==0&&count!=0)throw new ProtocolException("dialog arguments");var args=new string[count];for(int i=0;i<count;i++)args[i]=r.S();r.Finish();if(!accept)return true;if(html.Length==0){CloseInteraction("伺服器已關閉對話。");return true;}dialogArguments=args;dialogNpcName=Target?.Name??"";ShowPage(html);return true;}
        int n=r.H();if(n>2048)throw new ProtocolException("shop list too large");var rows=new List<ShopRow>();var ids=new HashSet<uint>();
        for(int i=0;i<n;i++){uint id=r.D();if(!ids.Add(id))throw new ProtocolException("duplicate shop identity");ushort icon=opcode==70?r.H():(ushort)0;uint price=r.D();if(price>int.MaxValue)throw new ProtocolException("shop price");string name=opcode==70?r.S():inventory.GetValueOrDefault(id)?.Name??"未持有物件 "+id;string status=opcode==70?Bytes(r,r.C()):"";uint available=opcode==70?int.MaxValue:inventory.GetValueOrDefault(id)?.Count??0;rows.Add(new(id,icon,price,name,available,status));}
        // Lists have no request ID: never let a duplicate reply reopen a submitted order.
        ushort currency=opcode==70&&n>0?r.H():(ushort)7;r.Finish();if(!accept||InteractionKind!="waiting"||pendingNpcReply!="")return true;InteractionRevision++;InteractionKind=opcode==70?"buy":"sell";InteractionPage=null;ShopRows=rows.ToArray();shopCurrency=currency;InteractionNotice=n==0?(opcode==65?"原服未列出可出售給此商人的物品。":"原服目前沒有列出可購買的物品。"):currency==7?"請設定數量並明確提交一次；結果以原服背包與金額為準。":"尚未支援此商店的貨幣，提交已停用。";return true;
    }
    public bool CanSubmitShop=>InteractionValid&&InteractionKind is "buy" or "sell"&&shopCurrency==7&&ShopRows.Length>0;
    public bool ValidateShop(IReadOnlyList<ShopChoice> choices){if(!CanSubmitShop||choices.Count is <1 or >255||choices.Select(c=>c.Id).Distinct().Count()!=choices.Count)return false;long total=0;foreach(var c in choices){var row=ShopRows.FirstOrDefault(r=>r.Id==c.Id);if(row==null||c.Count is 0 or >int.MaxValue)return false;if(InteractionKind=="sell"&&(c.Count>row.Available||!inventory.TryGetValue(c.Id,out var item)||c.Count>item.Count))return false;total+=(long)c.Count*row.Price;if(total>int.MaxValue)return false;}return true;}
    public object InteractionSnapshot()=>new{revision=InteractionRevision,epoch=WorldEpoch,targetRevision=TargetRevision,npcId=InteractionNpc,targetId=TargetId,kind=InteractionKind,hasResult=InteractionHasResult,notice=InteractionNotice,text=InteractionPage?.Text,options=InteractionPage?.Options.Select(o=>new{id=o.Id,label=o.Label,enabled=o.Enabled,kind=o.Kind}),rows=ShopRows.Select(r=>new{id=r.Id,icon=r.Icon,price=r.Price,name=r.Name,available=r.Available}),currency=shopCurrency,title=InteractionTitle,priceLabel=InteractionPriceLabel,selection=IsInteractionSelection,feePerRow=InteractionKind=="withdraw",canSubmit=CanSubmitShop||CanSubmitService};
}
public sealed partial class LoginConnection {
    bool interactionSending;
    public bool CanTalk=>State==LoginState.InWorld&&!World.ActionLocked&&World.NearbyTarget&&!World.TradeBlocksActions&&!MovementBusy&&!interactionSending&&World.CanStartInteraction;
    public Task<bool> Interact(string command,int revision,int epoch,int targetRevision,int option=0,IReadOnlyList<ShopChoice>? choices=null)=>InteractCore(command,revision,epoch,targetRevision,option,choices,null);
    async Task<bool> InteractCore(string command,int revision,int epoch,int targetRevision,int option,IReadOnlyList<ShopChoice>? choices,Func<bool>? pointerCurrent){
        if(pointerCurrent?.Invoke()==false)return false;
        World.CheckInteraction();if(command!="close"&&World.TradeBlocksActions)return false;if(State!=LoginState.InWorld||revision!=World.InteractionRevision||epoch!=World.WorldEpoch||targetRevision!=World.TargetRevision)return false;
        if(command=="close"){World.CloseInteraction();Set(State,World.InteractionNotice);return true;}
        if(command=="option"&&World.OpenDialogLink(option,revision)){Set(State,World.InteractionNotice);return true;}
        if(command=="option"&&World.OpenDeposit(option,revision)){Set(State,World.InteractionNotice);return true;}
        if(interactionSending||MovementBusy||sendMovementGate==null)return false;
        byte[] packet;bool shop=command=="shop";string service="";
        if(command=="talk"){if(!CanTalk)return false;packet=NpcPackets.Talk(World.TargetId!.Value);}
        else if(command=="option"){var selected=World.InteractionPage?.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind=="action");if(!World.InteractionValid||selected==null||World.InteractionKind!="dialog")return false;service=selected.Value switch{"buyspell"=>"skill-list","retrieve"=>"withdraw-list",_=>""};packet=service=="skill-list"?ServicePackets.SkillList(World.InteractionNpc!.Value):NpcPackets.Action(World.InteractionNpc!.Value,selected.Value);}
        else if(shop){if(choices==null)return false;service=World.IsServiceSelection?World.InteractionKind:"";if(service!=""?!World.ValidateService(choices):!World.ValidateShop(choices))return false;choices=choices.ToArray();packet=service=="skills"?ServicePackets.Learn(choices):service is "deposit" or "withdraw"?ServicePackets.Warehouse(World.InteractionNpc!.Value,service=="withdraw",choices):NpcPackets.Shop(World.InteractionNpc!.Value,World.InteractionKind=="sell",choices);}
        else return false;
        int session=generation;var sender=sendMovementGate;interactionSending=true;
        bool Valid()=>(pointerCurrent?.Invoke()??true)&&session==generation&&epoch==World.WorldEpoch&&targetRevision==World.TargetRevision&&revision==World.InteractionRevision&&State==LoginState.InWorld&&!World.ActionLocked&&World.NearbyTarget&&!World.TradeBlocksActions&&!MovementBusy&&(command=="talk"?World.CanStartInteraction:World.InteractionValid)&&(!shop||(service!=""?World.ValidateService(choices!):World.ValidateShop(choices!)));
        try{bool sent=await sender(packet,Valid,()=>{if(command=="talk")World.BeginTalk();else if(service!="")World.AwaitNpcService(service,choices);else World.AwaitInteraction(shop);Set(State,World.InteractionNotice);});return sent&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"互動傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(session==generation)interactionSending=false;}
    }
}
