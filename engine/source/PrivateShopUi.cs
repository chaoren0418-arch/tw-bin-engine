using System.Text.Json;
using P06.NativeUi;
namespace P06;
public sealed record PrivateShopCommand(string Command,int Generation,int Epoch,int Revision,int TargetRevision,uint ActorId,uint OwnerId,int Row=0,uint Count=0,bool Sell=false){
    public static PrivateShopCommand Parse(JsonElement o){
        string cmd=o.GetProperty("cmd").GetString()??"";var keys=new List<string>{"v","id","cmd","generation","epoch","revision","targetRevision","actorId","ownerId"};
        if(cmd=="privateQuery")keys.Add("sell");else if(cmd=="privateChoose")keys.Add("row");else if(cmd=="privateSubmit")keys.AddRange(["row","count","sell"]);else if(cmd is not ("privateOwner" or "privateClose"))throw new FormatException("private command");
        if(!o.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(keys.Order())||o.GetProperty("v").GetInt32()!=1||o.GetProperty("id").GetInt32()<1)throw new FormatException("private fields");
        int g=o.GetProperty("generation").GetInt32(),e=o.GetProperty("epoch").GetInt32(),r=o.GetProperty("revision").GetInt32(),t=o.GetProperty("targetRevision").GetInt32();uint actor=o.GetProperty("actorId").GetUInt32(),owner=o.GetProperty("ownerId").GetUInt32();int row=cmd is "privateChoose" or "privateSubmit"?o.GetProperty("row").GetInt32():0;uint count=cmd=="privateSubmit"?o.GetProperty("count").GetUInt32():0;bool sell=cmd is "privateQuery" or "privateSubmit"&&o.GetProperty("sell").GetBoolean();
        if(g<0||e<0||r<0||t<0||actor==0||owner==0||row<0||row>=2048||cmd=="privateSubmit"&&(count==0||count>int.MaxValue||sell&&count>0xFFFFFF))throw new FormatException("private identity/quantity");return new(cmd,g,e,r,t,actor,owner,row,count,sell);
    }
}
public sealed partial class ClientForm {
    readonly Panel privateShopPanel=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(23,38,41)};
    readonly FlowLayoutPanel privateShopChoices=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
    readonly FlowLayoutPanel privateShopActions=new(){Dock=DockStyle.Top,Height=70,AutoSize=false};
    readonly Label privateShopNotice=new(){Dock=DockStyle.Bottom,Height=105,ForeColor=Color.White,Padding=new(5)};
    readonly Label privateShopTotal=new(){Dock=DockStyle.Bottom,Height=44,ForeColor=Color.White,Padding=new(5)};
    readonly NumericUpDown privateShopQuantity=new(){Dock=DockStyle.Bottom,Height=30,Minimum=0,Maximum=0,Value=0};
    readonly Button privateShopBuy=new(){Text="查詢可買入商品",Width=150,Height=30};
    readonly Button privateShopSell=new(){Text="向店主出售（查詢回購）",Width=190,Height=30};
    readonly Button privateShopClose=new(){Text="關閉舊清單",Width=130,Height=30};
    readonly Button privateShopSubmit=new(){Dock=DockStyle.Bottom,Height=34,Text="確認一次",Enabled=false};
    PrivateShopView? displayedPrivateShop;string privateUiSignature="",privateQuantityKey="";bool privateSyncing;
    static PrivateShopCommand PrivateCommand(PrivateShopView v,string cmd,int row=0,uint count=0,bool sell=false,uint? owner=null)=>new(cmd,v.Generation,v.Epoch,v.Revision,v.TargetRevision,v.ActorId,owner??v.OwnerId,row,count,sell);
    WorldObject[] VisiblePrivateOwners()=>login.World.Objects.Where(o=>o.ShopActive&&!o.Dead&&HasVisibleObjectPixel(o.ObjectId)).ToArray();
    // R6-C（#779）：原生 TRADE_WINDOW 接上時，清單與送出在原生窗（模式 13／14）；這裡只留查詢、選店主、關閉與提示。
    const string PrivateNativeNotice="商品清單已在交易視窗顯示：在視窗裡輸入各列數量，按 BUY／SELL 一次送出。";
    PrivateShopView PrivateDisplayView(){var v=login.PrivateShopSnapshot();bool visible=v.OwnerId!=0&&HasVisibleObjectPixel(v.OwnerId)&&!World.Walking;return v with{OwnerName=ResolveNpcText(v.OwnerName),Notice=ResolveNpcText(v.Notice)+(login.World.PrivateShopNativeWindow&&v.Phase=="list"?"\n"+PrivateNativeNotice:""),ServerText=ResolveNpcText(v.ServerText),Rows=v.Rows.Select(r=>r with{Name=ResolveNpcText(r.Name)}).ToArray(),CanQuery=v.CanQuery&&visible,CanChoose=v.CanChoose&&visible,CanSubmit=v.CanSubmit&&visible,CanQueryBuy=v.CanQueryBuy&&visible,CanQuerySell=v.CanQuerySell&&visible};}
    object PrivateShopWeb()=>new{view=PrivateDisplayView(),owners=VisiblePrivateOwners().Select(o=>new{id=o.ObjectId,name=ResolveNpcText(o.Name),x=o.X,y=o.Y})};
    // R9-I（#853）：點店主 → 原生 HyperText 開本機 usershop 頁（UserShopPagePanel）；沒有原生窗（--classic-ui）或沒有模板時才切到 WinForms 分頁（備援）。
    async Task<bool> OpenPrivateShopFromTalk(NpcWebCommand c){var w=login.World;if(c.Revision!=w.InteractionRevision||c.Epoch!=w.WorldEpoch||c.TargetRevision!=w.TargetRevision||w.TargetId is not uint owner)return false;bool accepted=await RunPrivateShop(PrivateCommand(login.PrivateShopSnapshot(),"privateOwner",owner:owner))==PrivateShopResult.Accepted;if(accepted&&!OpenUserShopPage(owner)&&privateShopPanel.Parent is TabPage page&&normalPanels!=null)normalPanels.SelectedTab=page;return accepted;}
    void InitPrivateShop(){privateShopActions.Controls.AddRange([privateShopBuy,privateShopSell,privateShopClose]);privateShopPanel.Controls.AddRange([privateShopChoices,privateShopNotice,privateShopSubmit,privateShopTotal,privateShopQuantity,privateShopActions,personalShopActions]);InitPersonalShop();privateShopPanel.Enter+=(_,_)=>LeaveGameFocus("private-shop");
        privateShopBuy.Click+=async(_,_)=>{if(displayedPrivateShop is {} v)await RunPrivateShop(PrivateCommand(v,"privateQuery"));};privateShopSell.Click+=async(_,_)=>{if(displayedPrivateShop is {} v)await RunPrivateShop(PrivateCommand(v,"privateQuery",sell:true));};privateShopClose.Click+=async(_,_)=>{if(displayedPrivateShop is {} v)await RunPrivateShop(PrivateCommand(v,"privateClose"));};
        privateShopSubmit.Click+=async(_,_)=>{if(displayedPrivateShop is {SelectedRow:not null} v)await RunPrivateShop(PrivateCommand(v,"privateSubmit",v.SelectedRow.Value,(uint)privateShopQuantity.Value,v.Sell));};privateShopQuantity.ValueChanged+=(_,_)=>{if(!privateSyncing)UpdatePrivateQuantity();};}
    Control[] PrivateLegacyControls()=>[privateShopChoices,privateShopNotice,privateShopSubmit,privateShopTotal,privateShopQuantity,privateShopActions];
    void UpdatePrivateQuantity(){var v=displayedPrivateShop;var row=v?.Rows.FirstOrDefault(r=>r.Id==v.SelectedRow);uint count=(uint)privateShopQuantity.Value;privateShopSubmit.Enabled=v?.CanSubmit==true&&row!=null&&count>0&&count<=row.Maximum;privateShopSubmit.Text=v?.Sell==true?"確認出售一次":"確認購買一次";privateShopTotal.Text=row==null?"選一筆商品，再輸入數量。":$"數量 {count:N0} · {(v!.Sell?"預估收入":"預估支出")} {(long)row.Price*count:N0}\n貨幣種類：原服未提供";}
    async Task<PrivateShopResult> RunPrivateShop(PrivateShopCommand c){
        ClearInput("private-shop");var v=login.PrivateShopSnapshot();bool valid=c.Generation==v.Generation&&c.Epoch==v.Epoch&&c.Revision==v.Revision&&c.TargetRevision==v.TargetRevision&&c.ActorId==v.ActorId&&login.State==LoginState.InWorld;PrivateShopResult result=PrivateShopResult.NotSent;
        if(valid&&!World.Walking){
            if(c.Command=="privateOwner"){if(HasVisibleObjectPixel(c.OwnerId)&&login.World.Objects.Any(o=>o.ObjectId==c.OwnerId&&o.ShopActive&&!o.Dead)){login.World.SelectTarget(c.OwnerId);login.World.ClosePrivateShop();result=PrivateShopResult.Accepted;}}
            else if(c.OwnerId==v.OwnerId){if(c.Command=="privateClose")result=login.ClosePrivateShop(v)?PrivateShopResult.Accepted:PrivateShopResult.NotSent;else if(HasVisibleObjectPixel(c.OwnerId)){if(c.Command=="privateChoose")result=login.ChoosePrivateShop(c.Row,0,v)?PrivateShopResult.Accepted:PrivateShopResult.NotSent;else result=await login.SendPrivateShop(c.Command=="privateQuery",c.Sell,c.Row,c.Count,v);}}
        }
        SyncPrivateShop();lastWebState="";PublishWebState();return result;
    }
    async Task DispatchPrivateShop(int id,PrivateShopCommand c){var result=await RunPrivateShop(c);Reply(id,PrivateShopFeedback.Text(result,login.World.PrivateShopNotice));}
    void SyncPrivateShop(){
        var v=PrivateDisplayView();displayedPrivateShop=v;privateShopPanel.Visible=login.State==LoginState.InWorld;
        // R7-G（#809）：原生 TRADE_WINDOW 模式 13／14 開著時，私商的 WinForms 控制項全部讓位（個人商店設定 personalShopActions 不受影響）；
        // --classic-ui／原生 UI 沒接上時 PrivateShopNativeWindow＝false，不讓位。
        bool yields=login.PrivateShopLegacyYields;foreach(Control c in PrivateLegacyControls())c.Visible=!yields;
        SyncUserShopPage();
        privateShopNotice.Text=v.Notice+(v.ServerText.Length>0?"\n原服訊息："+v.ServerText:"");privateShopNotice.Height=TextRenderer.MeasureText(privateShopNotice.Text,privateShopNotice.Font,new Size(Math.Max(100,privateShopPanel.ClientSize.Width-10),1000),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+10;privateShopBuy.Enabled=v.CanQueryBuy;privateShopSell.Enabled=v.CanQuerySell;privateShopClose.Enabled=v.ActorId!=0&&v.OwnerId!=0;
        var selected=v.Rows.FirstOrDefault(r=>r.Id==v.SelectedRow);string key=$"{v.Generation}/{v.Epoch}/{v.OwnerId}/{v.Sell}/{v.SelectedRow}";privateSyncing=true;if(key!=privateQuantityKey||selected==null){privateShopQuantity.Value=0;privateQuantityKey=key;}privateShopQuantity.Maximum=selected?.Maximum??0;privateShopQuantity.Enabled=v.CanSubmit;privateSyncing=false;UpdatePrivateQuantity();
        var owners=VisiblePrivateOwners();string signature=JsonSerializer.Serialize(new{v,owners=owners.Select(o=>new{o.ObjectId,o.Name,o.X,o.Y})});if(signature==privateUiSignature)return;privateUiSignature=signature;foreach(Control old in privateShopChoices.Controls.Cast<Control>().ToArray())old.Dispose();privateShopChoices.Controls.Clear();
        privateShopChoices.Controls.Add(new Label{Text="店主："+v.OwnerName,ForeColor=Color.White,AutoSize=true});foreach(var owner in owners){var command=PrivateCommand(v,"privateOwner",owner:owner.ObjectId);var button=new Button{Width=370,Height=30,Text=$"選取 {ResolveNpcText(owner.Name)} ({owner.X}, {owner.Y})"};button.Click+=async(_,_)=>await RunPrivateShop(command);privateShopChoices.Controls.Add(button);}
        foreach(var row in v.Rows){var command=PrivateCommand(v,"privateChoose",row.Id);var button=new Button{Width=370,Height=62,Text=$"{(v.SelectedRow==row.Id?"已選：":"選擇：")}{row.Name}\n單價 {row.Price:N0} · {(v.Sell?"原訂收購量":"清單數量")} {row.ListedCount:N0} · 本次最多 {row.Maximum:N0}",Enabled=v.CanChoose&&row.Maximum>0};button.Click+=async(_,_)=>await RunPrivateShop(command);privateShopChoices.Controls.Add(button);}
    }

    // ---- R9-I（T369／#853）：私商查詢頁（本機 HTML usershop → userShop0／1 → C47） ----
    UserShopPageModel? userShopPage;int userShopRevision;string userShopNotice="";UserShopPageHost? userShopHost;
    /// <summary>原生 UI 開著、私商查詢頁面板沒有故障時回傳面板；否則 null（走 WinForms 分頁備援）。</summary>
    internal UserShopPagePanel? NativeUserShopPage()=>NativeUiActive&&NativeUiRoot is {} root&&root.Find(UserShopPagePanel.PanelId) is UserShopPagePanel p&&!root.Faulted.Contains(p)?p:null;
    /// <summary>
    /// 點店主（右鍵或「與選取目標對話」）：開 usershop 頁，#0＝店主名。原版 sub_5001C0 LABEL_46 不送封包、交給 NPC 對話窗；
    /// 同一個窗（op 144）→ 開頁時關掉原生 HYPER_TEXT_WINDOW 上的 NPC／查詢頁。回 false＝沒有原生窗或沒有模板（呼叫端退回 WinForms 分頁）。
    /// </summary>
    bool OpenUserShopPage(uint owner){
        if(NativeUserShopPage() is not {} panel)return false;
        var w=login.World;var shop=w.Objects.FirstOrDefault(o=>o.ObjectId==owner);
        if(shop==null||w.TargetId!=owner)return false;
        var model=UserShopPageModel.Open(w.DialogTemplates,owner,w.WorldEpoch,w.TargetRevision,++userShopRevision,ResolveNpcText(shop.Name),ResolveNpcText(w.Actor?.Name??""));
        if(model==null){events.Add(new{kind="usershop-page",status="待驗證：dialogs/templates.json 沒有 usershop 模板，退回 WinForms 分頁"});return false;}
        if(panel.Page.Host==null)panel.Page.Attach(userShopHost??=new UserShopPageHost(this));
        if(NativeHyperText() is {Visible:true} pages)pages.Close();
        userShopPage=model;userShopNotice="";
        SyncUserShopPage();NativeUiRoot?.BringToFront(panel);Invalidate();
        return true;
    }
    /// <summary>
    /// 每格同步：店主不再是目前目標／世界或目標版本改變／店主不可見或收攤／原生 TRADE_WINDOW 開了私商清單（sub_A58980 關掉頁名含
    /// usershop 的頁）／同一窗出現新的 NPC 或查詢頁 → 關頁（本機，不送封包）。兩顆鈕可按＝原有的查詢條件（CanQueryBuy／CanQuerySell）。
    /// </summary>
    void SyncUserShopPage(){
        var panel=NativeUserShopPage();
        if(panel==null){userShopPage=null;return;}
        if(userShopPage is {} m&&!UserShopPageLive(m))userShopPage=null;
        if(userShopPage is not {} page){panel.Page.Show(null);return;}
        var v=PrivateDisplayView();
        panel.Page.Show(page.View(v.CanQueryBuy,v.CanQuerySell,userShopNotice));
    }
    bool UserShopPageLive(UserShopPageModel m){
        var w=login.World;
        return login.State==LoginState.InWorld&&m.Current(w.TargetId,w.WorldEpoch,w.TargetRevision)&&w.Objects.Any(o=>o.ObjectId==m.OwnerId&&o.ShopActive&&!o.Dead)
            &&HasVisibleObjectPixel(m.OwnerId)&&!login.PrivateShopLegacyYields&&NativeHyperText() is not {Visible:true};
    }
    /// <summary>userShop0／1 → 原有的私商查詢（C47 type＋店主 id，LoginConnection.SendPrivateShop）；過期頁、走路中、不可查詢都不送。</summary>
    async Task<PrivateShopResult> RunUserShopLink(HyperTextView shown,int option){
        if(userShopPage is not {} m||shown.Revision!=m.Revision||m.Sell(option) is not bool sell)return PrivateShopResult.NotSent;
        var v=login.PrivateShopSnapshot();if(v.OwnerId!=m.OwnerId)return PrivateShopResult.NotSent;
        var result=await RunPrivateShop(PrivateCommand(v,"privateQuery",sell:sell));
        userShopNotice=result==PrivateShopResult.Accepted?"":PrivateShopFeedback.Text(result,"");
        events.Add(new{kind="usershop-page",action=sell?"userShop1":"userShop0",owner=m.OwnerId,result=result.ToString()});
        SyncUserShopPage();Invalidate();return result;
    }
    void CloseUserShopPage(int revision){if(userShopPage?.Revision==revision){userShopPage=null;userShopNotice="";}SyncUserShopPage();ReturnGameFocus();}
    /// <summary>私商查詢頁的動作：只有連結與關窗（頁上沒有數字框／名稱框）。</summary>
    sealed class UserShopPageHost(ClientForm form):IHyperTextHost {
        public void Link(HyperTextView shown,int option)=>_=form.RunUserShopLink(shown,option);
        public void Submit(HyperTextView shown,long value){}
        public void CancelNumber(HyperTextView shown){}
        public void Close(HyperTextView shown){
            // 同 NpcUi：Close() 可能來自 UiRoot.Evict（畫面更新中途），狀態改動放到 UI 執行緒之後。
            if(form.IsHandleCreated&&!form.IsDisposed)form.BeginInvoke((Action)(()=>form.CloseUserShopPage(shown.Revision)));else form.CloseUserShopPage(shown.Revision);
        }
    }
}

/// <summary>
/// R9-I（T369／#853）私商查詢頁的資料（依 docs/reference/followups-9.md 第 15 節 3:C_QUERY_PERSONAL_SHOP、第 16.2 節 R9-I）。
/// 已驗證（引用）：點店主由 sub_5001C0 LABEL_46 以 NPC 對話窗開本機 HTML「usershop」（字串 VA 0x114F31C），不送封包；
///   頁上兩顆圖片鈕 action＝userShop0／userShop1；HyperAct userShop（sub_BA7610 → sub_585270，FO 0x184670）取動作字串第 8 字元（0／1）
///   送查詢＋type＋店主 id；3.81 對應 C_ShopList（C47：C type, D 店主），回 S_PrivateShop（0＝店主賣出、1＝收購）。
/// 推測：type 只接受 '0'／'1'，動作名不分大小寫（模板表本來就不分）；其他字元不送。
/// </summary>
public sealed record UserShopPageModel(uint OwnerId,int Epoch,int TargetRevision,int Revision,string OwnerName,NpcPage Page,HyperTextDocument? Document){
    public const string Html="usershop";
    public const string ActionPrefix="userShop";
    /// <summary>userShop0 → false（向店主買：C47 type 0）、userShop1 → true（賣給店主：type 1）；其他 → null。</summary>
    public static bool? ActionSell(string? action){
        if(action is not {Length:9}||!action.StartsWith(ActionPrefix,StringComparison.OrdinalIgnoreCase))return null;
        return action[8] switch{'0'=>false,'1'=>true,_=>null};
    }
    /// <summary>用 dialogs/templates.json 的 usershop 模板開頁（#0＝店主名）；沒有模板回 null。HTML 文件對得上時行內排版（R9-F）。</summary>
    public static UserShopPageModel? Open(NpcTemplates? templates,uint owner,int epoch,int targetRevision,int revision,string ownerName,string player){
        if(templates==null||owner==0||templates.Markup(Html) is not {} html)return null;
        string[] args=[ownerName];
        var page=templates.Render(Html,args,ownerName,player);
        var doc=HyperTextMarkup.Parse(html,args,ownerName,player);
        return new(owner,epoch,targetRevision,revision,ownerName,page,doc.Plain==page.Text&&doc.LinkCount==page.Options.Length?doc:null);
    }
    public bool Current(uint? targetId,int epoch,int targetRevision)=>targetId==OwnerId&&epoch==Epoch&&targetRevision==TargetRevision;
    /// <summary>某個選項對應的查詢方向；不是可用的 userShop 動作回 null。</summary>
    public bool? Sell(int option)=>Page.Options.FirstOrDefault(o=>o.Id==option&&o.Enabled&&o.Kind=="action") is {} o?ActionSell(o.Value):null;
    /// <summary>要顯示的頁：userShop0 可按＝canBuy、userShop1 可按＝canSell；其他動作一律停用。</summary>
    public HyperTextView View(bool canBuy,bool canSell,string notice){
        var links=Page.Options.Select(o=>new HyperTextLink(o.Id,o.Label,Sell(o.Id) is bool sell&&(sell?canSell:canBuy))).ToArray();
        string text=string.Join("\n",new[]{Page.Limitation,notice}.Where(s=>s.Length>0));
        return new(HyperTextSource.Npc,Revision,Page.Text,links,text,null,null,Document);
    }
}

/// <summary>
/// R9-I：私商查詢頁的原生窗。原版是同一個 HYPER_TEXT_WINDOW（uiextra86），這裡另包一個 HyperTextPanel（同版面、同排版器），
/// 因為 NpcUi.cs 的頁面分流只認 NPC 頁與查詢頁（本組不改該檔）；「同一個窗」由 ClientForm 在開頁與每格同步時互相關閉來模擬。
/// </summary>
public sealed class UserShopPagePanel:IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(174)] internal static UiPanelRegistration Registration=>new(PanelId,HyperTextPanel.LayoutName,_=>new UserShopPagePanel());   // R9-I (#853)
    public const string PanelId="USERSHOP_HYPER_TEXT";
    public UserShopPagePanel(Func<string,int>? measure=null){Page=new(measure);}
    /// <summary>實際畫頁的 HYPER_TEXT_WINDOW 面板。</summary>
    public HyperTextPanel Page{get;}
    public string Id=>PanelId;
    public UiLayer Layer=>Page.Layer;
    public Rectangle Bounds=>Page.Bounds;
    public bool Visible=>Page.Visible;
    public void Load(UiLayout layout,SurfCatalog surfs)=>Page.Load(layout,surfs);
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf)=>Page.Load(layout,surf);
    public void Update(IUiGameState state)=>Page.Update(state);
    public void Paint(UiCanvas canvas)=>Page.Paint(canvas);
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>Page.Mouse(e,commands);
    public bool Key(UiKeyEvent e,IUiCommands commands)=>Page.Key(e,commands);
    public bool Char(char c)=>Page.Char(c);
    public bool IsDragHandle(Point location)=>Page.IsDragHandle(location);
    public void MoveTo(Point location)=>Page.MoveTo(location);
    public void Close()=>Page.Close();
}
