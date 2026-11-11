using System.Reflection;
using System.Text.Json;

namespace P06.NativeUi;

// R11-B（T416／#958，gap-matrix 15.13.5–15.13.6，4:BehaviourUI）action-window-followup 離線測試：只用合成版面、合成動作清單、
// 合成 surf、合成字串表、合成 S87 與記憶體內假傳送端；不讀客戶端資源、不開遊戲、網路、視窗。由 --module-tests 自動發現執行。
// 涵蓋：① 地面名稱／透明屍體改 Toggle，經 OptionPanel 宿主切換；② 隊長點已在隊上的人＝踢人（C255），點別人＝邀請（C230 type 0）；
//   ③ 邀請聊天／加好友／封鎖選定對象後直接送（C230 type 2／C207＋C4／C171）；④ 常駐鈕提示 3118。
static class ActionWindowFollowupTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static string Ui(int tooltip)=>$"""
        <?xml version="1.0"?>
        <ActionUI Toggle="true">
          <Button Name="ActionUI_Button" X="774" Y="470" MostTop="1" BaseSurf="960;1" HighlightSurf="961;1" MouseEvent="OpenButtonPressed">
            <Tooltip Name="Desc_Tooltip" String="{tooltip}"/>
          </Button>
          <Window Name="Action_Window" BaseSurf="900;1" Moveable="10" KeyboardEvent="KeyboardEvent" MouseEvent="Dummy">
            <ActionIconGrid Name="Grid" X="5" Y="25" Row="4" Column="6" UnitGap="1" UnitWidth="10" UnitHeight="10" Drag="1" MouseEvent="GridEvent"/>
          </Window>
        </ActionUI>
        """;
    static string List()=>"<?xml version=\"1.0\"?>\n<ActionList>\n"+string.Concat(ActionRoutes.Known.Select((n,i)=>
        $"  <Action action=\"{n}\" category=\"action\" control=\"action\" icon=\"{1000+i}\"/>\n"))+"</ActionList>\n";
    const uint ButtonBase=0xFF336699,ButtonHover=0xFF99CCFF;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [900]=Surface(900,100,80,0xFF102030),[960]=Surface(960,20,24,ButtonBase),[961]=Surface(961,20,24,ButtonHover)};
    static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
    static ActionWindowPanel Loaded(UiRoot? root,int tooltip=ActionOpenButtonPanel.TooltipStringId){
        var p=new ActionWindowPanel(null,root);p.Load(UiLayout.Parse(Ui(tooltip),"ActionUI-c.xml"),UiLayout.Parse(List(),"Actions.xml"),Get);return p;
    }
    static ActionEntry A(ActionWindowPanel p,string name)=>p.Actions.Single(a=>a.Name==name);
    static UiMouseEvent M(UiMouseKind k,int x,int y,int clicks=1,MouseButtons b=MouseButtons.Left)=>new(k,new Point(x,y),b,clicks,0,Keys.None);

    /// <summary>選項窗宿主替身：只持有兩個顯示開關（同 ClientForm.OptionWindowHost 的鍵）。</summary>
    sealed class OptionHost:IOptionWindowHost {
        public readonly Dictionary<string,bool> Values=new(){[OptionPanel.ShowDropItemNameEvent]=true,[OptionPanel.TransparentDeadMonsterEvent]=false};
        public bool Refuse;
        public bool? Value(string e)=>Values.TryGetValue(e,out var v)?v:null;
        public bool Set(string e,bool v){if(Refuse||!Values.ContainsKey(e))return false;Values[e]=v;return true;}
    }

    [WorldModuleTest("action-window-followup")]
    static void Run(WorldModuleTestContext t){
        Routes(t);
        DisplayToggles(t);
        Targets(t);
        TargetSend(t);
        PartyKick(t);
        Tooltip(t);
        Product(t);
    }

    // 路線表：① 兩個 Toggle；③ 三個仍保留聊天列前綴（離線退路）；過時的「輸入框」「未做」說明已更正。
    static void Routes(WorldModuleTestContext t){
        t.Check(ActionRoutes.Of("Action_ViewDropItemName") is {Kind:ActionRouteKind.Toggle,Argument:OptionPanel.ShowDropItemNameEvent},"view drop item name → ShowDropItemNameBtn toggle (byte_138BE7E)");
        t.Check(ActionRoutes.Of("Action_CorpseTransparent") is {Kind:ActionRouteKind.Toggle,Argument:OptionPanel.TransparentDeadMonsterEvent},"corpse transparent → TransparentDeadMonsterBtn toggle (byte_138BFAA)");
        t.Check(ActionRoutes.DisplayToggles.SetEquals(OptionPanel.DisplayEvents),"display toggles = the two R10-I option display events");
        foreach(var name in new[]{"Action_InviteChatParty","Action_AddFriend","Action_Exclude"}){
            var r=ActionRoutes.Of(name);
            t.Check(r.Kind==ActionRouteKind.ChatLine&&r.Note is {} n&&!n.Contains("未做",StringComparison.Ordinal)&&!n.Contains("輸入框",StringComparison.Ordinal),name+": prefix fallback kept, stale note corrected",r.Note);
        }
        t.Check(ActionRoutes.Of("Action_InviteParty").Note is {} inv&&!inv.Contains("未做",StringComparison.Ordinal),"invite party: kick branch no longer marked as not done");
        t.Check(ActionRoutes.Of("Action_Report").Note is {} rep&&!rep.Contains("輸入框",StringComparison.Ordinal),"report: sub_9FBC00 is a send, not an input box");
        t.Check(ActionRoutes.Known.Where(n=>ActionRoutes.Of(n).Kind==ActionRouteKind.None).All(n=>ActionRoutes.Of(n).Note is {Length:>0}),"every unwired action says why");
    }

    // ① 面板退路：經 OptionPanel 的宿主切換（同一個值）；沒有宿主、宿主拒絕 → Unhandled。
    static void DisplayToggles(WorldModuleTestContext t){
        var root=new UiRoot();var option=new OptionPanel();root.Add(option);var p=Loaded(root);root.Add(p);var none=new UiNullCommands();
        t.Equal(ActionRunResult.Unhandled,p.Execute(A(p,"Action_ViewDropItemName"),none),"option window without host → unhandled");
        var host=new OptionHost();option.Attach(host);
        t.Equal(ActionRunResult.Toggled,p.Execute(A(p,"Action_ViewDropItemName"),none),"drop names toggled through the option host");
        t.Check(!host.Values[OptionPanel.ShowDropItemNameEvent]&&!host.Values[OptionPanel.TransparentDeadMonsterEvent],"ShowDropItemName on → off; the other untouched",host.Values);
        t.Equal(ActionRunResult.Toggled,p.Execute(A(p,"Action_ViewDropItemName"),none),"again");
        t.Check(host.Values[OptionPanel.ShowDropItemNameEvent],"back on");
        t.Equal(ActionRunResult.Toggled,p.Execute(A(p,"Action_CorpseTransparent"),none),"corpse transparent toggled");
        t.Check(host.Values[OptionPanel.TransparentDeadMonsterEvent]&&host.Values[OptionPanel.ShowDropItemNameEvent],"TransparentDeadMonster off → on",host.Values);
        host.Refuse=true;
        t.Equal(ActionRunResult.Unhandled,p.Execute(A(p,"Action_CorpseTransparent"),none),"host refuses → unhandled");
        t.Check(host.Values[OptionPanel.TransparentDeadMonsterEvent],"value kept");
        t.Check(p.Unhandled.Contains("Action_CorpseTransparent")&&p.Unhandled.Contains("Action_ViewDropItemName"),"refusals logged once");
        var bare=Loaded(new UiRoot());
        t.Equal(ActionRunResult.Unhandled,bare.Execute(A(bare,"Action_ViewDropItemName"),none),"no option window → unhandled");
    }

    static WorldObject Obj(uint id,string name,byte flags)=>new(32600,32940,id,100,0,0,0,0,0,0,name,"",flags,0,"","",0,0,0,10,0,0,0);

    // ③ 對象判斷：玩家、不是自己、外觀已到、名稱合法；送出文字解析成既有斜線指令。
    static void Targets(WorldModuleTestContext t){
        t.Equal("甲",ActionTargets.PlayerName(Obj(5,"甲",4),1),"player → name");
        t.Equal(null,ActionTargets.PlayerName(Obj(5,"怪",0),1),"not a player → null");
        t.Equal(null,ActionTargets.PlayerName(Obj(1,"我",4),1),"self → null");
        t.Equal(null,ActionTargets.PlayerName(Obj(5,"甲",4) with{AppearancePending=true},1),"appearance pending → null");
        t.Equal(null,ActionTargets.PlayerName(Obj(5,"甲 乙",4),1),"name with a space → null");
        t.Equal(null,ActionTargets.PlayerName(Obj(5,"",4),1),"empty name → null");
        t.Equal(null,ActionTargets.PlayerName(null,1),"nothing → null");
        t.Equal("甲",ActionTargets.PlayerName(Obj(5,"甲",4) with{Dead=true},1),"dead player still a target (domain global, 推測)");
        foreach(var (name,kind) in new[]{("Action_InviteChatParty",ChatFollowupKind.ChatInvite),("Action_AddFriend",ChatFollowupKind.AddBuddy),("Action_Exclude",ChatFollowupKind.Exclude)}){
            var text=ActionTargets.Command(ActionRoutes.Of(name).Argument!,"甲");
            t.Check(ChatFollowupCommands.Parse(text) is {} c&&c.Kind==kind&&c.Argument=="甲",name+" → "+text+" parses as "+kind,text);
            t.Check(ActionTargets.PickName(name).Length>0&&ActionTargets.PickName(name)!=name&&ActionTargets.PickNotice(name).Contains(ActionTargets.PickName(name),StringComparison.Ordinal),name+": pick name and notice");
        }
        t.Equal("/addbuddy 甲",ActionTargets.Command("/addbuddy","甲"),"prefix without the trailing space");
        t.Equal("組隊邀請",ActionTargets.PickName(ActionTargets.InvitePartyAction),"invite party pick name");
    }

    // ---- 假連線（同 ChatCommandFollowupTests.Seed）：自己 123「offline」；甲 456 玩家；怪 458；乙 457 玩家（隊員）。
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Peer=456,Member=457,Npc=458;
    const ushort X=32600,Y=32940;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Op(byte op,string s)=>LoginPackets.Pad([op,..LoginPackets.TextEncoding.GetBytes(s),0]);
    static byte[] Appear(uint id,ushort x,ushort y,string name,byte flags=0)=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[flags],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,X,Y,"offline"),"offline");
        c.World.Receive(Appear(Peer,(ushort)(X+5),Y,"甲",4),"offline");
        c.World.Receive(Appear(Member,(ushort)(X+3),Y,"乙",4),"offline");
        c.World.Receive(Appear(Npc,(ushort)(X+2),Y,"怪"),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static bool Send(LoginConnection c,string action,uint id){Set(c,"playerChatNextAt",0L);return Wait(ActionTargets.SendToTarget(c,ActionRoutes.Of(action).Argument!,id));}
    static string Hex(List<byte[]> sent)=>string.Join(" ",sent.Select(Convert.ToHexString));

    // ③ 選定後直接送：走 R11-A 的選對象入口（PlayerPointer 點中 → 回呼），送 3.81 的封包。
    static void TargetSend(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        using var noDesc=PlayerChatProtocol.UseDesc(null);
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(c.World.Ready,"seed: world ready");
        t.Check(Send(c,"Action_AddFriend",Peer)&&sent.Count==2&&sent[0].SequenceEqual(Op(207,"甲"))&&sent[1].SequenceEqual(LoginPackets.Pad([4])),"add friend → C207 甲 ＋ C4 (cs 138＋c 56, FU13 第 70 行)",Hex(sent));
        sent.Clear();
        t.Check(Send(c,"Action_Exclude",Peer)&&sent.Count==1&&sent[0].SequenceEqual(Op(171,"甲")),"exclude → C171 甲",Hex(sent));
        sent.Clear();
        t.Check(Send(c,"Action_InviteChatParty",Peer)&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.ChatInvite("甲")),"chat invite → C230 type 2 甲",Hex(sent));
        sent.Clear();
        t.Check(!Send(c,"Action_AddFriend",Npc)&&!Send(c,"Action_AddFriend",Me)&&!Send(c,"Action_AddFriend",999)&&sent.Count==0,"NPC, self, unknown → nothing sent",Hex(sent));

        // 經 PlayerPointer：選對象模式中點人 → 回呼送出；游標 8；點地面取消、不送。
        var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
        Set(c,"playerChatNextAt",0L);
        c.BeginObjectPick(ActionTargets.PickName("Action_AddFriend"),id=>ActionTargets.SendToTarget(c,ActionRoutes.Of("Action_AddFriend").Argument!,id));
        t.Check(c.TargetPicking&&c.ObjectPickName=="新增好友","object pick mode (R11-A)");
        t.Equal(GameCursors.Aim,TargetPickCursor.Index(c,Peer),"aim cursor 8 while picking");
        t.Check(!pointer.Select(Peer)&&Wait(pointer.LastObjectPick!)&&sent.Count==2&&sent[0].SequenceEqual(Op(207,"甲")),"click on the player sends at once",Hex(sent));
        t.Check(!c.TargetPicking&&c.World.TargetId==null,"pick ends; target selection unchanged");
        t.Equal(PlayerPointerController.ObjectPickSentNotice("新增好友"),pointer.Notice,"sent notice");
        sent.Clear();
        c.BeginObjectPick(ActionTargets.PickName("Action_Exclude"),id=>ActionTargets.SendToTarget(c,ActionRoutes.Of("Action_Exclude").Argument!,id));
        t.Check(!pointer.Select(Npc)&&!Wait(pointer.LastObjectPick!)&&sent.Count==0,"click on an NPC: refused, nothing sent",Hex(sent));
        t.Equal(PlayerPointerController.ObjectPickRefusedNotice("斷絕"),pointer.Notice,"refused notice");
        c.BeginObjectPick(ActionTargets.PickName("Action_Exclude"),id=>ActionTargets.SendToTarget(c,ActionRoutes.Of("Action_Exclude").Argument!,id));
        t.Check(!pointer.MoveTo(new Point(X+1,Y))&&!c.TargetPicking&&sent.Count==0,"ground click cancels, nothing sent");
    }

    // ② 隊長點已在隊上的人＝踢人（C255），點別人＝邀請（C230 type 0）；不是隊長時不走這條（CanKick false）。
    static void PartyKick(WorldModuleTestContext t){
        var self=new PartyMember(1,"我",100);var b=new PartyMember(2,"乙",100);var c0=new PartyMember(0,"丙",null);
        PartyView View(bool canKick)=>new(1,1,1,1,[self,b,c0],"我","",null,true,true,canKick);
        t.Equal("乙",ActionTargets.PartyKick(View(true),Obj(2,"乙",4)),"leader clicks a member (id) → kick 乙");
        t.Equal("丙",ActionTargets.PartyKick(View(true),Obj(9,"丙",4)),"name-only member row → matched by name");
        t.Equal(null,ActionTargets.PartyKick(View(true),Obj(4,"丁",4)),"not in the party → invite");
        t.Equal(null,ActionTargets.PartyKick(View(true),Obj(1,"我",4)),"self → never kicked");
        t.Equal(null,ActionTargets.PartyKick(View(false),Obj(2,"乙",4)),"not leader → invite path");
        t.Equal(null,ActionTargets.PartyKick(View(true),null),"nothing → null");

        using var noTable=ServerMessageText.UseTable(null);
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        _=c.PartySnapshot();   // CheckParty：記下 partyOwner＝自己
        var members=(Dictionary<string,PartyMember>)w.GetType().GetField("partyMembers",F)!.GetValue(w)!;
        members["offline"]=new(Me,"offline",100);members["乙"]=new(Member,"乙",100);Set(w,"partyLeader","offline");
        var shown=c.PartySnapshot();
        t.Check(shown.CanKick&&shown.Members.Length==2,"seed: leader with a party (CanKick)",shown);
        t.Check(Wait(ActionTargets.InviteOrKick(c,Member))&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Kick("乙")),"leader picks member 乙 → C255 乙",Hex(sent));
        sent.Clear();
        t.Check(Wait(ActionTargets.InviteOrKick(c,Peer))&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Invite(Peer,0)),"leader picks 甲 (not in party) → C230 type 0",Hex(sent));
        t.Check(!w.PartyInvitePicking,"invite pick mode closed after the send");
        sent.Clear();
        t.Check(!Wait(ActionTargets.InviteOrKick(c,Npc))&&sent.Count==0&&!w.PartyInvitePicking,"NPC → nothing sent",Hex(sent));
    }

    static ServerStringTable Table(int id,string text){
        var lines=new string[id+1];Array.Fill(lines,"");lines[id]=text;
        return ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes((id+1)+"\n"+string.Join("\n",lines)));
    }
    static uint Pixel(IUiPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.White);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();}

    // ④ 常駐鈕提示 3118：停留才畫；字串表優先，表讀不到時只有 3118 用查到的原文；提示框不收點擊。
    static void Tooltip(WorldModuleTestContext t){
        var root=new UiRoot();var w=Loaded(root);root.Add(w);var btn=new ActionOpenButtonPanel(root);root.Add(btn);var c=new UiNullCommands();
        btn.Update(new UiStaticGameState(InWorld:true));
        t.Equal((int?)ActionOpenButtonPanel.TooltipStringId,w.OpenButton?.TooltipString,"XML Tooltip String 3118 read");
        using(ServerMessageText.UseTable(Table(3118,"\\f3測試提示"))){
            t.Equal("測試提示",btn.TooltipText,"string table text (color code stripped)");
            t.Check(btn.Tooltip==null&&btn.Bounds==new Rectangle(774,470,20,24),"not hovered: no tooltip, bounds = button",btn.Bounds);
            btn.Mouse(M(UiMouseKind.Move,780,480,0,MouseButtons.None),c);
            var tip=btn.Tooltip;
            t.Check(tip is {Lines.Count:1}&&tip.Lines[0]=="測試提示","hover: one line",tip);
            var size=SummonPanel.TooltipSize(["測試提示"]);
            t.Check(tip!=null&&tip.Box.Size==size&&tip.Box.Y==494&&UiRoot.Canvas.Contains(tip.Box),"box size per sub_B0DAF0, just below the button, inside the canvas",tip?.Box);
            t.Check(tip!=null&&btn.Bounds.Contains(tip.Box)&&btn.Bounds.Contains(btn.ButtonRect),"bounds = button ∪ tooltip",btn.Bounds);
            t.Equal(0xFF000000u,Pixel(btn,tip!.Box.Right-2,tip.Box.Bottom-2),"tooltip box painted (black, 推測)");
            t.Equal(ButtonHover,Pixel(btn,780,480),"button still drawn hovered");
            t.Check(!btn.Mouse(M(UiMouseKind.Down,tip.Box.Right-2,tip.Box.Bottom-2),c)&&!w.Visible,"click on the tooltip is not taken, window not opened");
            btn.Mouse(M(UiMouseKind.Move,tip.Box.Right-2,tip.Box.Bottom-2,0,MouseButtons.None),c);
            t.Check(!btn.Hover&&btn.Tooltip==null&&btn.Bounds==btn.ButtonRect,"moving onto the tooltip leaves the button: tooltip hidden");
        }
        using(ServerMessageText.UseTable(null)){
            t.Equal(ActionOpenButtonPanel.TooltipFallback,btn.TooltipText,"no table → 3118 fallback text (local string-c.tbl, PakViewer)");
            var r2=new UiRoot();var w2=Loaded(r2,77);r2.Add(w2);var b2=new ActionOpenButtonPanel(r2);r2.Add(b2);b2.Update(new UiStaticGameState(InWorld:true));
            b2.Mouse(M(UiMouseKind.Move,780,480,0,MouseButtons.None),c);
            t.Check(b2.Hover&&b2.TooltipText==null&&b2.Tooltip==null,"other string id without a table → no tooltip (nothing invented)");
        }
        using(ServerMessageText.UseTable(Table(3118,"甲^乙"))){
            btn.Mouse(M(UiMouseKind.Move,780,480,0,MouseButtons.None),c);
            t.Check(btn.Tooltip is {Lines.Count:2},"'^' splits lines (same as SummonPanel)");
            btn.Update(new UiStaticGameState(InWorld:false));
            t.Check(btn.Tooltip==null&&!btn.Visible,"outside the world: no tooltip");
        }
    }

    static void Product(WorldModuleTestContext t){
        t.Check(typeof(IUiActionCommands).IsAssignableFrom(typeof(ClientForm.NativeUiCommands)),"NativeUiCommands still implements IUiActionCommands");
        t.Check(typeof(ClientForm.NativeUiCommands).GetMethod("BeginPick",F) is {} m&&m.ReturnType==typeof(bool),"product bridge has the object-pick entry");
    }
}
