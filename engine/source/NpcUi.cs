using System.Text.Json;
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
    // S39 query results (WIN§2 T140) share the NPC panel; the native HYPER_TEXT_WINDOW layout waits for T190.
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
        var w=login.World;w.CheckInteraction();w.CheckNpcSale();npcTalk.Visible=login.State==LoginState.InWorld;npcTalk.Enabled=login.CanTalk&&!World.Walking&&w.TargetId is uint id&&HasVisibleObjectPixel(id);bool npcOpen=NpcContextOpen(w),query=w.QueryVisible;npcPanel.Visible=(npcOpen||query)&&login.State==LoginState.InWorld;npcClose.Text=npcOpen?"關閉對話／取消未送出選擇":w.QueryActionable?"關閉伺服器頁":"關閉查詢結果";npcNotice.Text=ResolveNpcText(npcOpen||!query?w.InteractionNotice:"");npcSubmit.Visible=w.IsInteractionSelection;npcSubmit.Text=w.InteractionTitle+"：提交一次";npcSubmit.Enabled=(w.IsServiceSelection?w.CanSubmitService:w.CanSubmitShop)&&!World.Walking;
        SyncCastlePrompt();SyncSpellExchangeWindow();
        int queryKey=query?w.QueryRevision:-1;if(npcUiRevision==w.InteractionRevision&&npcUiQueryRevision==queryKey)return;npcUiRevision=w.InteractionRevision;npcUiQueryRevision=queryKey;foreach(Control c in npcContent.Controls.Cast<Control>().ToArray())c.Dispose();npcContent.Controls.Clear();shopInputs.Clear();var stamp=CurrentNpcCommand("option");npcShopStamp=CurrentNpcCommand("shop");
        if(query)AddQueryResult(w,npcOpen);
        if(w.InteractionPage is {} page){npcContent.Controls.Add(new Label{Text=ResolveNpcText(page.Text),AutoSize=true,MaximumSize=new(400,0),ForeColor=Color.White,Margin=new(6)});foreach(var option in page.Options){var command=stamp with{Option=option.Id};var b=new Button{Text=ResolveNpcText(option.Label),Width=400,AutoSize=true,Enabled=option.Enabled};b.Click+=async(_,_)=>await RunInteraction(command);npcContent.Controls.Add(b);}}
        foreach(var row in w.ShopRows){var host=new FlowLayoutPanel{Width=410,Height=66};host.Controls.Add(new Label{Text=$"{ResolveNpcText(row.Name)}\n{w.InteractionPriceLabel} {row.Price:N0} 金幣"+(w.InteractionKind is "sell" or "deposit" or "withdraw"?$" · 最多 {row.Available}":""),ForeColor=Color.White,Width=280,Height=60});var quantity=new NumericUpDown{Width=110,Minimum=0,Maximum=Math.Min(row.Available,int.MaxValue),Value=0,Enabled=row.Available>0};host.Controls.Add(quantity);shopInputs.Add((row.Id,quantity));npcContent.Controls.Add(host);}
    }
    object InteractionWeb(){var w=login.World;w.CheckNpcSale();return new{targets=objectVisuals.Where(v=>w.Objects.Any(o=>o.ObjectId==v.ObjectId&&!o.Dead)&&HasVisibleObjectPixel(v.ObjectId)).Select(v=>new{id=v.ObjectId,name=v.Name}),model=w.InteractionSnapshot(),canTalk=login.CanTalk&&!World.Walking&&w.TargetId is uint id&&HasVisibleObjectPixel(id),text=ResolveNpcText(w.InteractionPage?.Text??""),notice=ResolveNpcText(w.InteractionNotice),options=w.InteractionPage?.Options.Select(o=>new{id=o.Id,label=ResolveNpcText(o.Label),enabled=o.Enabled}),rows=w.ShopRows.Select(r=>new{id=r.Id,name=ResolveNpcText(r.Name),price=r.Price,available=r.Available}),queryText=w.QueryVisible?ResolveNpcText(w.QueryPage!.Text):null,
        // R2-F：web 面板顯示伺服器頁／城堡頁與兩種數字框。
        query=w.QueryVisible?new{revision=w.QueryRevision,html=w.QueryHtml,actionable=w.QueryActionable,notice=ResolveNpcText(w.QueryPage!.Limitation),options=w.QueryPage.Options.Select(o=>new{id=o.Id,label=ResolveNpcText(o.Label),enabled=o.Enabled&&w.QueryActionable})}:null,
        amount=AmountPromptWeb(),castle=CastlePromptWeb()};}
}
