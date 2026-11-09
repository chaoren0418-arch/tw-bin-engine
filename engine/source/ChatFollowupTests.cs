using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R3-G（T282／#675）聊天收尾二的離線測試：合成封包、記憶體內版面與 surf，不開遊戲、網路、視窗，不讀客戶端素材。
// 由 --module-tests 執行（名稱 chat-followup）。
static class ChatFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Npc=0x2000_0001;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    /// <summary>op 61 S_SAY_CODE＝3.81 S161：C type、D id、S text。</summary>
    static byte[] Code(byte type,uint id,string text)=>P([161,type],Le32(id),Str(text));
    static PlayerChatLine Decode(byte[] packet){if(!PlayerChatProtocol.TryDecode(packet,out var line))throw new InvalidOperationException("decode");return line!;}

    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static LoginConnection Seed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");
        return c;
    }

    // CENTER_WINDOW 最小版面（座標同 ui-layout.md §3；第二組原點證明 SetHeights 依 XML 換階）。
    static readonly (string Path,int? X,int? Y,bool Abs,int Surf)[] Fixture=[
        ("CHAT_MAIN_WINDOW/CHAT_WINDOW",0,600,true,3279),("CHAT_MAIN_WINDOW/CHAT_BORN",0,600,true,3280),
        ("CHAT_MAIN_WINDOW/LCHAT_WINDOW",0,600,true,3288),("CHAT_MAIN_WINDOW/LCHAT_BORN",0,600,true,3289),
        ("CHAT_MAIN_WINDOW/RCHAT_WINDOW",260,600,true,3290),("CHAT_MAIN_WINDOW/RCHAT_BORN",260,600,true,3291),
        ("CHAT_MAIN_WINDOW/CHAT_BAR",10,114,false,2273),
        ("CHAT_STEP_CONTROL/CHAT_UP",345,115,false,2274),("CHAT_STEP_CONTROL/CHAT_DOWN",345,115,false,2277),
        ("CHAT_SCROLL_CONTROL/SCROLL_UP_OFF",346,30,false,2284),("CHAT_SCROLL_CONTROL/SCROLL_UP_ON",null,null,false,2285),
        ("CHAT_SCROLL_CONTROL/SCROLL_DOWN_OFF",346,107,false,2286),("CHAT_SCROLL_CONTROL/SCROLL_DOWN_ON",null,null,false,2287),
        ("CHAT_SCROLL_CONTROL/SCROLL_KNOB",null,null,false,2288),
        ("CHAT_ICON/CHAT_ICON_FRAME",164,4,false,3285),("CHAT_ICON/CHAT_ICON_ACTIVE_ON",299,5,false,3286),("CHAT_ICON/CHAT_ICON_ACTIVE",299,5,false,3287),
        ("CHAT_ICON/GENERAL_CHAT_ON",179,5,false,2290),("CHAT_ICON/GENERAL_CHAT",179,5,false,2291),
        ("CHAT_ICON/PARTY_CHAT_ON",198,5,false,2292),("CHAT_ICON/PARTY_CHAT",198,5,false,2293),
        ("CHAT_ICON/PLEDGE_CHAT_ON",221,5,false,2294),("CHAT_ICON/PLEDGE_CHAT",221,5,false,2295),
        ("CHAT_ICON/TRADE_CHAT_ON",240,5,false,2296),("CHAT_ICON/TRADE_CHAT",240,5,false,2297),
        ("CHAT_ICON/SYSTEM_CHAT_ON",258,5,false,2975),("CHAT_ICON/SYSTEM_CHAT",258,5,false,2976),
        ("CHANNEL_ICON/GENERAL_CHANNEL",8,115,false,2977),("CHANNEL_ICON/GENERAL_CHANNEL_ON",null,null,false,2978),
        ("CHANNEL_ICON/BLOOD_CHANNEL",8,100,false,2980),("CHANNEL_ICON/BLOOD_CHANNEL_ON",null,null,false,2981),
        ("CHANNEL_ICON/PARTY_CHANNEL",8,85,false,2983),("CHANNEL_ICON/PARTY_CHANNEL_ON",null,null,false,2984),
        ("CHANNEL_ICON/WHOLE_CHANNEL",8,70,false,2986),("CHANNEL_ICON/WHOLE_CHANNEL_ON",null,null,false,2987),
        ("CHANNEL_ICON/TRADE_CHANNEL",8,55,false,2989),("CHANNEL_ICON/TRADE_CHANNEL_ON",null,null,false,2990),
        ("CHANNEL_ICON/CHANNEL_CHANNEL",8,40,false,2992),("CHANNEL_ICON/CHANNEL_CHANNEL_ON",null,null,false,2993),
        ("CHANNEL_ICON/ALLIANCE_CHANNEL",8,25,false,2995),("CHANNEL_ICON/ALLIANCE_CHANNEL_ON",null,null,false,2996),
        ("CHANNEL_ICON/UPPER_CHANNEL",8,10,false,2998),("CHANNEL_ICON/UPPER_CHANNEL_ON",null,null,false,2999),
    ];
    static UiLayout Layout(int x,int y,int step){
        var sb=new System.Text.StringBuilder($"<MainStatusConfigulation><CENTER_WINDOW x=\"{x}\" y=\"{y}\" stepSize=\"{step}\">");
        foreach(var g in Fixture.GroupBy(f=>f.Path.Split('/')[0])){
            sb.Append($"<{g.Key}>");
            foreach(var f in g)sb.Append($"<{f.Path.Split('/')[1]} surfNumber=\"{f.Surf}\"{(f.X is int fx?$" xRPos=\"{fx}\"":"")}{(f.Y is int fy?(f.Abs?$" yPos=\"{fy}\"":$" yRPos=\"{fy}\""):"")}/>");
            sb.Append($"</{g.Key}>");
        }
        sb.Append("</CENTER_WINDOW></MainStatusConfigulation>");
        return UiLayout.Parse(sb.ToString(),"uistatus86-c.xml");
    }
    static readonly Dictionary<int,UiSurface> surfaces=[];
    static UiSurface? Surface(int id){
        if(surfaces.TryGetValue(id,out var s))return s;
        (int w,int h)=id switch{3279 or 3280=>(362,240),3288 or 3289=>(260,240),3290 or 3291=>(259,240),2273=>(346,16),_=>(14,12)};
        return surfaces[id]=new UiSurface(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,new uint[w*h]));
    }

    [WorldModuleTest("chat-followup")]
    static void Run(WorldModuleTestContext t){
        // 1. op 61 S_SAY_CODE：type 0／2／3 → 一般／喊話／全體 Printer；字串由伺服器組好。
        var say=Decode(Code(0,Npc,"潘朵拉: 歡迎光臨"));
        t.Equal(PlayerChatChannel.Normal,say.Channel,"S161/0 → normal printer");
        t.Equal(Npc,say.Speaker,"S161 speaker id kept");
        t.Check(say.Code,"S161 line marked as code chat");
        t.Equal("潘朵拉: 歡迎光臨",ChatFormat.DisplayText(say),"S161/0 shown as `name: msg`");
        t.Equal(ChatFormat.White,ChatFormat.TextColor(say),"S161/0 white 14");
        t.Equal(ChatFormat.White,ChatFormat.TextColor(Decode(Code(0,Npc,"*守衛: 停下"))),"S161 '*' not yellow (S_SAY rule only)");
        t.Equal(ChatFormat.Yellow,ChatFormat.TextColor(Decode(P([81,0],Le32(Me),Str("*甲: 嗨")))),"S81 '*' still yellow");

        var shout=Decode(Code(2,Npc,"<歐林> 小心！"));
        t.Equal(PlayerChatChannel.Shout,shout.Channel,"S161/2 → shout printer");
        t.Equal("<歐林> 小心！",shout.Text,"model keeps server `<name>`");
        t.Equal("[歐林] 小心！",ChatFormat.DisplayText(shout),"S161/2 shown as `[name] msg`");
        t.Equal(33,ChatFormat.TextColor(shout),"S161/2 shout colour 33");

        var world=Decode(Code(3,Npc,"[*公告*] 攻城開始"));
        t.Equal(PlayerChatChannel.Global,world.Channel,"S161/3 → world printer");
        t.Equal("[*公告*] 攻城開始",ChatFormat.DisplayText(world),"S161/3 shown as `[name] msg`");
        t.Equal(44,ChatFormat.TextColor(world),"S161/3 world 44, '*' red rule not applied");
        t.Equal(ChatFormat.Red,ChatFormat.TextColor(Decode(P([243,3],Str("[*公告*] 攻城開始")))),"S243/3 '*' still red");

        t.Check(!PlayerChatProtocol.TryDecode(Code(1,Npc,"x"),out _),"S161/1 not chat");
        t.Check(!PlayerChatProtocol.TryDecode(Code(4,Npc,"x"),out _),"S161/4 not chat");
        t.Check(!PlayerChatProtocol.TryDecode(Code(0,0,"系統訊息"),out _),"S161 id 0 (S_ServerMessage) not chat");
        t.Check(!PlayerChatProtocol.TryDecode(Code(2,0,"系統"),out _),"S161/2 id 0 (S_SystemMessage nameid) not chat");
        t.Equal(ChatFormat.White,ChatFormat.SecondColor(shout),"code chat second colour 14");

        // 2. 經過實際分派：NPC 代碼聊天進聊天；id 0 仍是伺服器訊息。
        using(var c=Seed()){
            c.World.Receive(Code(0,Npc,"潘朵拉: 歡迎光臨"),"offline");
            c.World.Receive(Code(2,Npc,"<歐林> 小心！"),"offline");
            var lines=c.World.PlayerChatLines;
            t.Equal(2,lines.Length,"two code chat lines stored");
            t.Check(lines.Length==2&&lines[0].Sequence==1&&lines[1].Sequence==2&&lines.All(l=>l.Code),"code chat sequenced");
            c.World.Receive(Code(0,0,"伺服器公告文字"),"offline");
            t.Equal(2,c.World.PlayerChatLines.Length,"S161 id 0 not added to chat");
            t.Equal("伺服器公告文字",c.World.LastServerMessage,"S161 id 0 still the server message");
        }

        // 3. op 104 S_TELL（3.81 S73）：收密語 `(name) msg` #FFCECE（32）；送密語回覆 S243/9 `-> (target) msg` #FFF7B5（24）。
        var tell=Decode(P([73],Str("甲"),Str("你好")));
        t.Equal(PlayerChatChannel.Whisper,tell.Channel,"S73 → whisper");
        t.Equal("(甲) 你好",ChatFormat.DisplayText(tell),"S73 shown as `(name) msg`");
        t.Equal(32,ChatFormat.TextColor(tell),"S73 colour 32");
        t.Equal(ChatFormat.White,ChatFormat.SecondColor(tell),"whisper second colour 14");
        t.Check(!tell.Code,"S73 is not code chat");
        var echo=Decode(P([243,9],Str("-> (乙) 收到")));
        t.Equal(PlayerChatChannel.WhisperTo,echo.Channel,"S243/9 echo → sent whisper");
        t.Equal("-> (乙) 收到",ChatFormat.DisplayText(echo),"sent whisper `-> (target) msg`");
        t.Equal(24,ChatFormat.TextColor(echo),"sent whisper colour 24");
        t.Check(!PlayerChatProtocol.TryDecode(P([243,9],Str("系統訊息")),out _),"S243/9 system text left to Inventory");
        t.Check(PlayerChatProtocol.Encode("\"甲 你好").SequenceEqual(P([184],Str("甲"),Str("你好"))),"whisper send C184 name + body");

        // 4. 第二色（Q3-9，只帶資料）：原生聊天窗的每一列帶 SecondColor，換行後每段相同。
        var view=new ChatViewState(new ChatHeights(130,240,36));
        var rows=view.Rows([new PlayerChatLine(1,0,"{甲} abcdef",PlayerChatChannel.Clan),new PlayerChatLine(2,0,"[乙] x",PlayerChatChannel.Global)],5,_=>1);
        t.Check(rows.Where(r=>r.Sequence==1).All(r=>r.SecondColor==ChatFormat.Grey)&&rows.Count(r=>r.Sequence==1)>1,"clan rows carry second colour 7 across wraps");
        t.Check(rows.Where(r=>r.Sequence==2).All(r=>r.SecondColor==ChatFormat.White),"world rows second colour 14");
        t.Equal(ChatFormat.White,new ChatRow(1,PlayerChatChannel.Clan,"x").SecondColor,"bare ChatRow default second colour 14");

        // 5. 原生聊天窗高度切換：Load 走 ChatViewState.SetHeights，重新載入保留分頁／分割，高度夾到新階。
        var panel=new ChatPanel();
        panel.Load(Layout(129,470,36),Surface);
        t.Check(panel.View.Heights.Steps.SequenceEqual([130,166,202,238,240]),"TW layout steps 130→240",panel.View.Heights.Steps.ToArray());
        t.Equal(130,panel.View.Height,"first load starts at min");
        t.Equal(202,panel.SetHeight(210),"SetHeight 210 → step 202");
        t.Equal(new Rectangle(129,600-202,362,202),panel.Bounds,"bounds follow the height, bottom at 600");
        t.Equal(240,panel.SetHeight(999),"SetHeight clamps to max 240");
        t.Equal(130,panel.SetHeight(0),"SetHeight below min → 130");
        panel.SetHeight(202);panel.View.Tab=ChatTab.Blood;panel.View.Split=true;
        panel.Load(Layout(30,400,20),Surface);
        t.Check(panel.View.Heights.Steps.SequenceEqual([200,220,240]),"reload: steps from new XML 200/220/240",panel.View.Heights.Steps.ToArray());
        t.Equal(200,panel.View.Height,"reload: 202 clamps to 200");
        t.Check(panel.View.Tab==ChatTab.Blood&&panel.View.Split,"reload keeps tab and split");
        t.Equal(0,panel.View.Scroll,"reload resets scroll");
        panel.SetHeight(240);
        panel.Load(Layout(129,470,36),Surface);
        t.Equal(240,panel.View.Height,"reload back: 240 still a step");
        panel.View.Cycle();t.Equal(130,panel.View.Height,"step button cycle from max → min");
    }
}
