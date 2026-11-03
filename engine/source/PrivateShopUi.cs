using System.Text.Json;
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
    readonly Button privateShopSell=new(){Text="查詢可售出商品",Width=150,Height=30};
    readonly Button privateShopClose=new(){Text="關閉舊清單",Width=130,Height=30};
    readonly Button privateShopSubmit=new(){Dock=DockStyle.Bottom,Height=34,Text="確認一次",Enabled=false};
    PrivateShopView? displayedPrivateShop;string privateUiSignature="",privateQuantityKey="";bool privateSyncing;
    static PrivateShopCommand PrivateCommand(PrivateShopView v,string cmd,int row=0,uint count=0,bool sell=false,uint? owner=null)=>new(cmd,v.Generation,v.Epoch,v.Revision,v.TargetRevision,v.ActorId,owner??v.OwnerId,row,count,sell);
    WorldObject[] VisiblePrivateOwners()=>login.World.Objects.Where(o=>o.ShopActive&&!o.Dead&&HasVisibleObjectPixel(o.ObjectId)).ToArray();
    PrivateShopView PrivateDisplayView(){var v=login.PrivateShopSnapshot();bool visible=v.OwnerId!=0&&HasVisibleObjectPixel(v.OwnerId)&&!World.Walking;return v with{OwnerName=ResolveNpcText(v.OwnerName),Notice=ResolveNpcText(v.Notice),ServerText=ResolveNpcText(v.ServerText),Rows=v.Rows.Select(r=>r with{Name=ResolveNpcText(r.Name)}).ToArray(),CanQuery=v.CanQuery&&visible,CanChoose=v.CanChoose&&visible,CanSubmit=v.CanSubmit&&visible,CanQueryBuy=v.CanQueryBuy&&visible,CanQuerySell=v.CanQuerySell&&visible};}
    object PrivateShopWeb()=>new{view=PrivateDisplayView(),owners=VisiblePrivateOwners().Select(o=>new{id=o.ObjectId,name=ResolveNpcText(o.Name),x=o.X,y=o.Y})};
    async Task<bool> OpenPrivateShopFromTalk(NpcWebCommand c){var w=login.World;if(c.Revision!=w.InteractionRevision||c.Epoch!=w.WorldEpoch||c.TargetRevision!=w.TargetRevision||w.TargetId is not uint owner)return false;bool accepted=await RunPrivateShop(PrivateCommand(login.PrivateShopSnapshot(),"privateOwner",owner:owner))==PrivateShopResult.Accepted;if(accepted&&privateShopPanel.Parent is TabPage page&&normalPanels!=null)normalPanels.SelectedTab=page;return accepted;}
    void InitPrivateShop(){privateShopActions.Controls.AddRange([privateShopBuy,privateShopSell,privateShopClose]);privateShopPanel.Controls.AddRange([privateShopChoices,privateShopNotice,privateShopSubmit,privateShopTotal,privateShopQuantity,privateShopActions]);privateShopPanel.Enter+=(_,_)=>LeaveGameFocus("private-shop");
        privateShopBuy.Click+=async(_,_)=>{if(displayedPrivateShop is {} v)await RunPrivateShop(PrivateCommand(v,"privateQuery"));};privateShopSell.Click+=async(_,_)=>{if(displayedPrivateShop is {} v)await RunPrivateShop(PrivateCommand(v,"privateQuery",sell:true));};privateShopClose.Click+=async(_,_)=>{if(displayedPrivateShop is {} v)await RunPrivateShop(PrivateCommand(v,"privateClose"));};
        privateShopSubmit.Click+=async(_,_)=>{if(displayedPrivateShop is {SelectedRow:not null} v)await RunPrivateShop(PrivateCommand(v,"privateSubmit",v.SelectedRow.Value,(uint)privateShopQuantity.Value,v.Sell));};privateShopQuantity.ValueChanged+=(_,_)=>{if(!privateSyncing)UpdatePrivateQuantity();};}
    void UpdatePrivateQuantity(){var v=displayedPrivateShop;var row=v?.Rows.FirstOrDefault(r=>r.Id==v.SelectedRow);uint count=(uint)privateShopQuantity.Value;privateShopSubmit.Enabled=v?.CanSubmit==true&&row!=null&&count>0&&count<=row.Maximum;privateShopTotal.Text=row==null?"選一筆商品，再輸入數量。":$"數量 {count:N0} · 合計 {(long)row.Price*count:N0}\n貨幣種類：原服未提供";}
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
        var v=PrivateDisplayView();displayedPrivateShop=v;privateShopPanel.Visible=login.State==LoginState.InWorld;privateShopNotice.Text=v.Notice+(v.ServerText.Length>0?"\n原服訊息："+v.ServerText:"");privateShopNotice.Height=TextRenderer.MeasureText(privateShopNotice.Text,privateShopNotice.Font,new Size(Math.Max(100,privateShopPanel.ClientSize.Width-10),1000),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height+10;privateShopBuy.Enabled=v.CanQueryBuy;privateShopSell.Enabled=v.CanQuerySell;privateShopClose.Enabled=v.ActorId!=0&&v.OwnerId!=0;
        var selected=v.Rows.FirstOrDefault(r=>r.Id==v.SelectedRow);string key=$"{v.Generation}/{v.Epoch}/{v.OwnerId}/{v.Sell}/{v.SelectedRow}";privateSyncing=true;if(key!=privateQuantityKey||selected==null){privateShopQuantity.Value=0;privateQuantityKey=key;}privateShopQuantity.Maximum=selected?.Maximum??0;privateShopQuantity.Enabled=v.CanSubmit;privateSyncing=false;UpdatePrivateQuantity();
        var owners=VisiblePrivateOwners();string signature=JsonSerializer.Serialize(new{v,owners=owners.Select(o=>new{o.ObjectId,o.Name,o.X,o.Y})});if(signature==privateUiSignature)return;privateUiSignature=signature;foreach(Control old in privateShopChoices.Controls.Cast<Control>().ToArray())old.Dispose();privateShopChoices.Controls.Clear();
        privateShopChoices.Controls.Add(new Label{Text="店主："+v.OwnerName,ForeColor=Color.White,AutoSize=true});foreach(var owner in owners){var command=PrivateCommand(v,"privateOwner",owner:owner.ObjectId);var button=new Button{Width=370,Height=30,Text=$"選取 {ResolveNpcText(owner.Name)} ({owner.X}, {owner.Y})"};button.Click+=async(_,_)=>await RunPrivateShop(command);privateShopChoices.Controls.Add(button);}
        foreach(var row in v.Rows){var command=PrivateCommand(v,"privateChoose",row.Id);var button=new Button{Width=370,Height=62,Text=$"{(v.SelectedRow==row.Id?"已選：":"選擇：")}{row.Name}\n單價 {row.Price:N0} · 清單數量 {row.ListedCount:N0} · 本次最多 {row.Maximum:N0}",Enabled=v.CanChoose&&row.Maximum>0};button.Click+=async(_,_)=>await RunPrivateShop(command);privateShopChoices.Controls.Add(button);}
    }
}
