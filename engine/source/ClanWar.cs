namespace P06;

public enum ClanWarCommand:byte {Declare=0,Surrender=2,Cease=3}
public sealed record ClanWarEvent(byte Type,string First,string Second);
public sealed record ClanWarPrompt(long Revision,ushort Message,string Clan,string Leader,int Epoch,int Interaction);
public sealed record ClanWarView(int Session,int Epoch,uint Actor,int Revision,bool CanRequest,bool CanAnswer,string Clan,string[] Enemies,string Notice,ClanWarPrompt? Prompt);

// Wire evidence: docs/protocol/clan-war.md. The zero D in S219/C121 is not a request ID.
public static class ClanWarProtocol {
    public static bool IsPrompt(ushort message)=>message is 217 or 221 or 222;
    static bool WireName(string name)=>!string.IsNullOrWhiteSpace(name)&&!name.Any(char.IsControl);
    public static bool Same(string left,string right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
    public static byte[] Encode(ClanWarCommand command,string name){
        if(command is not (ClanWarCommand.Declare or ClanWarCommand.Surrender or ClanWarCommand.Cease))throw new ArgumentException("未知血盟戰爭操作。");
        // A single space selects the server's castle path; this module only names a clan.
        if(string.IsNullOrWhiteSpace(name)||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))throw new ArgumentException("血盟名稱不可為空白或含控制字元。");
        byte[] text;
        try{text=LoginPackets.TextEncoding.GetBytes(name);}catch(System.Text.EncoderFallbackException){throw new ArgumentException("血盟名稱含遊戲字集無法表示的字元。");}
        if(text.Length>32)throw new ArgumentException("血盟名稱最多 32 個 Big5 位元組。");
        return LoginPackets.Pad([227,(byte)command,..text,0]);
    }
    public static byte[] Answer(ushort message,bool accept){
        if(!IsPrompt(message))throw new ArgumentException("未知血盟戰爭確認訊息。");
        return LoginPackets.Pad([121,0,0,0,0,0,0,(byte)message,(byte)(message>>8),(byte)(accept?1:0)]);
    }
    public static ClanWarEvent Decode(byte[] packet){
        var r=new LoginReader(packet);if(r.C()!=84)throw new ProtocolException("clan war opcode");
        var value=new ClanWarEvent(r.C(),r.S(),r.S());r.Finish();
        if(!WireName(value.First)||!WireName(value.Second))throw new ProtocolException("clan war names");
        return value;
    }
    internal static (string Clan,string Leader) DecodePrompt(ushort message,uint reserved,LoginReader r){
        string clan=r.S(),leader=message==217?r.S():"";r.Finish();
        if(reserved!=0||!WireName(clan)||message==217&&!WireName(leader))throw new ProtocolException("clan war confirmation");
        return (clan,leader);
    }
    public static string Describe(ClanWarEvent e,string own)=>e.Type switch{
        1=>$"{e.First} 血盟向 {e.Second} 血盟宣戰。",
        2=>$"{e.First} 血盟向 {e.Second} 血盟投降了。",
        3=>$"{e.First} 血盟與 {e.Second} 血盟之間的戰爭結束了。",
        4=>$"{e.First} 血盟贏了對 {e.Second} 血盟的戰爭。",
        6=>$"{e.First} 血盟與 {e.Second} 血盟成為同盟關係。",
        7=>$"毀掉 {e.First} 血盟與 {e.Second} 血盟之間的同盟。",
        8=>$"目前你的血盟與 {(Same(e.Second,own)?e.First:e.Second)} 血盟交戰當中。",
        _=>$"原服血盟戰爭訊息 {e.Type}：{e.First}、{e.Second}"};
    public static string Question(ClanWarPrompt prompt)=>prompt.Message switch{
        217=>$"{prompt.Clan} 血盟（{prompt.Leader}）向你的血盟宣戰。是否接受？",
        221=>$"{prompt.Clan} 血盟要向你投降。是否接受？",
        _=>$"{prompt.Clan} 血盟要結束戰爭。是否接受？"};
    // These replies stay in the normal S71 path; this only names the ones C_War and C_Attr can produce.
    public static string? ServerResult(ServerMessageReport? reply){
        if(reply==null)return null;string name=reply.Arguments.Length==1?reply.Arguments[0]:"";
        return (reply.Id,reply.Arguments.Length) switch{
            (218,1)=>name+" 血盟君主不在線上。",(232,0)=>"等級 25 以下的君主無法宣戰。",(234,0)=>"你的血盟已經在戰爭中。",
            (236,1)=>name+" 血盟拒絕你的宣戰。",(237,1)=>name+" 血盟拒絕你的提案。",(272,0)=>"若想要戰爭，首先必須創立血盟。",
            (474,0)=>"你已經擁有城堡，無法再擁有其他城堡。",(475,0)=>"若要攻城，君主的等級得 25 以上。",(476,0)=>"尚未到攻城時間。",
            (477,0)=>"包含你，所有的血盟成員到城外，才能宣佈攻城。",(478,0)=>"只有王子和公主才能宣戰。",(812,0)=>"想要參加攻城戰需有盟徽。",_=>null};
    }
}

public sealed partial class WorldSession {
    const string ClanWarIdle="輸入對方血盟名稱後可宣戰；資格與結果由伺服器判定。";
    readonly object clanWarGate=new();
    // Wars outlive map changes and S84/8 is only sent at login, so this is not keyed by WorldEpoch.
    readonly List<(string First,string Second)> clanWarPairs=[];
    ClanWarPrompt? clanWarPrompt;long clanWarPromptRevision;int clanWarRevision;
    string clanWarOwnClan="",clanWarNotice=ClanWarIdle;bool clanWarSubscribed;
    void ExpireClanWarPrompt(string notice){lock(clanWarGate){if(clanWarPrompt==null)return;clanWarPrompt=null;clanWarNotice=notice;clanWarRevision++;}}
    void CheckClanWar(){
        lock(clanWarGate){
            if(!clanWarSubscribed){clanWarSubscribed=true;CombatSoundStateChanged+=CheckClanWar;}
            // Reset nulls Status; S8 always precedes S84/8 in C_LoginToServer, so no login state is lost here.
            if(!Entered||Status==null){
                if(clanWarPairs.Count>0||clanWarPrompt!=null||clanWarOwnClan.Length>0){clanWarPairs.Clear();clanWarPrompt=null;clanWarOwnClan="";clanWarNotice=ClanWarIdle;clanWarRevision++;}
                return;
            }
            if(Actor is {} actor&&!ClanWarProtocol.Same(actor.Clan,clanWarOwnClan)){
                clanWarPairs.Clear();clanWarOwnClan=actor.Clan;clanWarRevision++;
                ExpireClanWarPrompt("血盟已變更，舊戰爭確認不可再回答。");
            }
            // Party and Duel consume their S219 before Trade; a live invitation here is newer than this prompt.
            if(clanWarPrompt is {} prompt&&(prompt.Epoch!=WorldEpoch||prompt.Interaction!=InteractionRevision||OwnDead||partyInvitation!=null||DuelSnapshot.Phase=="invited"))
                ExpireClanWarPrompt("戰爭確認已失效，請對方重新提出；沒有自動回覆。");
        }
    }
    internal ClanWarPrompt? PendingClanWarPrompt {get{CheckClanWar();lock(clanWarGate)return clanWarPrompt;}}
    internal string ClanWarNotice {get{CheckClanWar();lock(clanWarGate)return clanWarNotice;}}
    internal int ClanWarRevision {get{CheckClanWar();lock(clanWarGate)return clanWarRevision;}}
    internal void ClanWarNote(string notice){lock(clanWarGate){clanWarNotice=notice;clanWarRevision++;}}
    internal void ClanWarAnswered(string notice){lock(clanWarGate){clanWarPrompt=null;clanWarNotice=notice;clanWarRevision++;}}
    // Uses the retained clan: Actor is briefly null across a map change, while the war continues.
    string[] CurrentClanWarEnemies(){
        lock(clanWarGate)return clanWarPairs.Select(p=>ClanWarProtocol.Same(p.First,clanWarOwnClan)?p.Second:p.First).ToArray();
    }
    // Every S219 passes here first, so one shared TempID slot never keeps two answerable prompts.
    bool ReceiveClanWarYesNo(ushort message,uint reserved,LoginReader r){
        CheckClanWar();
        if(!ClanWarProtocol.IsPrompt(message)){ExpireClanWarPrompt("收到其他確認訊息，原戰爭確認已失效。");return false;}
        var (clan,leader)=ClanWarProtocol.DecodePrompt(message,reserved,r);
        // Returning true skips Trade's other routes, which would have expired these older prompts.
        ReceiveMarriageYesNo(message,reserved,r);
        if(TradeState=="invited"){TradeState="attention";TradeRevision++;TradeNotice="原服改送其他確認訊息；舊交易邀請不可再回答，可取消交易。";}
        if(clanInvitation!=null)ClearClanInvitation("收到血盟戰爭確認，原入盟申請已失效，請對方重新申請。");
        if(!Ready||OwnDead||Status?.Hp<=0||string.IsNullOrEmpty(Actor?.Clan)){ExpireClanWarPrompt("收到無法回答的戰爭確認。");return true;}
        lock(clanWarGate){
            clanWarPrompt=new(++clanWarPromptRevision,message,clan,leader,WorldEpoch,InteractionRevision);
            clanWarNotice=ClanWarProtocol.Question(clanWarPrompt);clanWarRevision++;
        }
        return true;
    }
    void ReceiveClanWar(byte[] plain){
        var value=ClanWarProtocol.Decode(plain);CheckClanWar();
        if(value.Type is not (1 or 2 or 3 or 4 or 6 or 7 or 8)){Unhandled(84);return;}
        if(Status==null)return;
        lock(clanWarGate){
            bool Pair((string First,string Second) p)=>ClanWarProtocol.Same(p.First,value.First)&&ClanWarProtocol.Same(p.Second,value.Second)||ClanWarProtocol.Same(p.First,value.Second)&&ClanWarProtocol.Same(p.Second,value.First);
            // Castle wars end on 2 or 4 without a following 3 in some paths; repeated ends are harmless.
            if(value.Type is 2 or 3 or 4)clanWarPairs.RemoveAll(Pair);
            // S84(1,clan,"NPC") opens an ownerless castle and has no matching end message.
            // Castle S84 is broadcast to everyone, so only this clan's wars are kept; S87 precedes S84/8.
            else if(value.Type is 1 or 8&&!ClanWarProtocol.Same(value.First,value.Second)&&value.First!="NPC"&&value.Second!="NPC"&&clanWarOwnClan.Length>0
                &&(ClanWarProtocol.Same(value.First,clanWarOwnClan)||ClanWarProtocol.Same(value.Second,clanWarOwnClan))&&!clanWarPairs.Any(Pair)&&clanWarPairs.Count<256)
                clanWarPairs.Add((value.First,value.Second));
            clanWarNotice=ClanWarProtocol.Describe(value,clanWarOwnClan);clanWarRevision++;
        }
    }
}

public sealed partial class LoginConnection {
    int clanWarSendingGeneration=-1;
    public ClanWarView ClanWarSnapshot()=>CaptureWorld(()=>{
        var prompt=World.PendingClanWarPrompt;string clan=World.Actor?.Clan??"";bool access=ClanAccess&&clanWarSendingGeneration!=generation;
        return new ClanWarView(generation,World.WorldEpoch,World.Actor?.ObjectId??0,World.ClanWarRevision,access&&clan.Length>0&&prompt==null,access&&prompt!=null,clan,World.ClanWarEnemies,World.ClanWarNotice,prompt);
    });
    async Task<bool> SendClanWarPacket(byte[] packet,Func<bool> current,Action admit,string failure){
        int session=generation;var sender=sendMovementGate!;clanWarSendingGeneration=session;
        try{bool sent=await sender(packet,current,admit);if(session!=generation)return false;Changed?.Invoke();return sent;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,failure);}return false;}
        finally{if(clanWarSendingGeneration==session)clanWarSendingGeneration=-1;}
    }
    public async Task<bool> SendClanWar(ClanWarCommand command,ClanWarView view,string name){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&view.Clan==World.Actor?.Clan&&World.PendingClanWarPrompt==null;
        if(!Current()||clanWarSendingGeneration==generation||sendMovementGate==null)return false;
        byte[] packet;
        try{
            // The server drops both cases silently, so the player would otherwise get no result.
            if(view.Clan.Length==0)throw new ArgumentException("你沒有血盟，不能操作血盟戰爭。");
            if(ClanWarProtocol.Same(view.Clan,name))throw new ArgumentException("對象不可是自己的血盟。");
            packet=ClanWarProtocol.Encode(command,name);
        }catch(ArgumentException ex){World.ClanWarNote(ex.Message);return false;}
        string notice=(command switch{ClanWarCommand.Declare=>"宣戰",ClanWarCommand.Surrender=>"投降",_=>"停戰"})+"請求已送出；結果以伺服器回報為準。";
        return await SendClanWarPacket(packet,Current,()=>World.ClanWarNote(notice),"血盟戰爭請求傳送失敗，已斷線；沒有自動重送。");
    }
    public async Task<bool> AnswerClanWar(ClanWarView view,bool accept){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&view.Prompt!=null&&view.Prompt==World.PendingClanWarPrompt;
        if(!Current()||clanWarSendingGeneration==generation||sendMovementGate==null)return false;
        return await SendClanWarPacket(ClanWarProtocol.Answer(view.Prompt!.Message,accept),Current,
            ()=>World.ClanWarAnswered(accept?"已接受；等待伺服器回報戰爭狀態。":"已拒絕。"),"戰爭確認傳送失敗，已斷線；沒有自動重送。");
    }
}

public sealed partial class ClientForm {
    bool clanWarUiHooked;
    // OnLoad, OnShown, OnVisibleChanged, OnCreateControl and OnHandleCreated belong to other modules.
    protected override void OnLayout(LayoutEventArgs e){
        base.OnLayout(e);
        if(!clanWarUiHooked){clanWarUiHooked=true;Shown+=(_,_)=>InitClanWarUi();}
    }
    void InitClanWarUi(){
        var button=new Button{Text="血盟戰爭",Dock=DockStyle.Top,Height=36,AccessibleName="開啟血盟戰爭"};
        if(normalPanels!=null){var page=new TabPage("血盟戰爭");page.Controls.Add(button);normalPanels.TabPages.Add(page);}
        else{button.Dock=DockStyle.Right;button.Width=88;bar.Controls.Add(button);button.BringToFront();}
        void Open(){
            if(login.State!=LoginState.InWorld)return;
            var existing=OwnedForms.OfType<ClanWarUi>().FirstOrDefault();if(existing!=null){existing.Activate();return;}
            ClearInput("clan-war");LeaveGameFocus("clan-war");new ClanWarUi(login).Show(this);
        }
        button.Click+=(_,_)=>Open();
        var refresh=new System.Windows.Forms.Timer{Interval=250};long shown=-1;
        refresh.Tick+=(_,_)=>{var view=login.ClanWarSnapshot();button.Text=view.Enemies.Length>0?$"血盟戰爭（交戰 {view.Enemies.Length}）":"血盟戰爭";if(view.Prompt is {} prompt&&prompt.Revision!=shown){shown=prompt.Revision;Open();}};
        FormClosed+=(_,_)=>refresh.Dispose();refresh.Start();
    }
    // Drawn over ClanUi's label at the same anchor; the backing box hides the neutral text underneath.
    void PaintClanWarEnemies(Graphics g){
        if(!login.World.Renderable)return;
        var viewport=new RectangleF(Camera.ViewOffset,Camera.View);
        using var backing=new SolidBrush(Color.FromArgb(235,20,8,8));
        foreach(var v in objectVisuals.Where(v=>objectPatch?.VisibleObjectIds.Contains(v.ObjectId)==true)){
            var obj=login.World.Objects.FirstOrDefault(o=>o.ObjectId==v.ObjectId);
            if(obj==null||obj.Name.Length==0||!login.World.IsClanWarEnemy(obj.Clan))continue;
            var p=Camera.ToView(v.Foot);if(!viewport.Contains(p))continue;
            string text=obj.Name+" ["+obj.Clan+"]";var size=g.MeasureString(text,Font);float x=p.X-size.Width/2,y=p.Y-48;
            g.FillRectangle(backing,x,y,size.Width+1,size.Height+1);g.DrawString(text,Font,Brushes.OrangeRed,x,y);
        }
    }
}

public sealed class ClanWarUi:Form {
    readonly LoginConnection connection;
    readonly Label notice=new(){Dock=DockStyle.Fill,Padding=new(12)};
    readonly TextBox target=new(){Width=240,MaxLength=32};
    readonly Button declare=new(){Text="宣戰",AutoSize=true},surrender=new(){Text="投降",AutoSize=true},cease=new(){Text="停戰",AutoSize=true},yes=new(){Text="接受",AutoSize=true},no=new(){Text="拒絕",AutoSize=true};
    readonly System.Windows.Forms.Timer timer=new(){Interval=200};
    ClanWarView displayed;bool busy,prefilled;
    public ClanWarUi(LoginConnection connection){
        this.connection=connection;displayed=connection.ClanWarSnapshot();
        Text="血盟戰爭";Size=new(520,300);StartPosition=FormStartPosition.CenterParent;
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=84};
        actions.Controls.AddRange([new Label{Text="對方血盟",AutoSize=true},target,declare,surrender,cease,yes,no]);Controls.Add(notice);Controls.Add(actions);
        declare.Click+=async(_,_)=>await Request(ClanWarCommand.Declare,"宣戰");surrender.Click+=async(_,_)=>await Request(ClanWarCommand.Surrender,"投降");cease.Click+=async(_,_)=>await Request(ClanWarCommand.Cease,"提議停戰");
        yes.Click+=async(_,_)=>await Run(v=>connection.AnswerClanWar(v,true));no.Click+=async(_,_)=>await Run(v=>connection.AnswerClanWar(v,false));
        timer.Tick+=(_,_)=>Sync();FormClosed+=(_,_)=>timer.Dispose();Sync();timer.Start();
    }
    void Sync(){
        var v=connection.ClanWarSnapshot();if(v.Session!=displayed.Session||connection.State!=LoginState.InWorld){Close();return;}
        displayed=v;if(!prefilled&&v.Enemies.Length>0){prefilled=true;if(target.TextLength==0)target.Text=v.Enemies[0];}
        string result=ClanWarProtocol.ServerResult(connection.World.LastServerReply) is {} text?"\n伺服器："+text:"";
        notice.Text=(v.Clan.Length>0?"血盟："+v.Clan:"目前沒有血盟")+"\n交戰中："+(v.Enemies.Length>0?string.Join("、",v.Enemies):"（未收到戰爭狀態）")+"\n"+v.Notice+result;
        declare.Enabled=surrender.Enabled=cease.Enabled=!busy&&v.CanRequest;yes.Enabled=no.Enabled=!busy&&v.CanAnswer;
    }
    async Task Request(ClanWarCommand command,string label){
        string name=target.Text;
        if(MessageBox.Show(this,$"確定向「{name}」{label}？","血盟戰爭",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        await Run(v=>connection.SendClanWar(command,v,name));
    }
    async Task Run(Func<ClanWarView,Task<bool>> send){
        var stamp=displayed;if(busy)return;busy=true;Sync();await send(stamp);busy=false;if(!IsDisposed)Sync();
    }
}
