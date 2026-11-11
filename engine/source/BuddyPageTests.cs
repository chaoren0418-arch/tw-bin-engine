using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// R10-V（T409／#926，FU13 D4）離線測試（名稱 buddy-page，由 --module-tests 執行）：
//   收到 S39 buddy → 依 3.81 sub_4A5220 的實際變數陣列 [全部空格數, 全部, 在線空格數, 在線] 開 buddy 頁（伺服器查詢頁），
//   名單模型照舊更新、NPC 對話不受影響、頁上沒有可送的選項；未就緒不開頁；別人的 owner 照舊拒絕、不開頁。
// 只用依 3.81 欄位合成的封包與合成模板（版面同 buddy-c.html 的 #0／#1／#2，文字不是原檔）；不開遊戲、網路、DB、視窗，也不讀客戶端素材。
static class BuddyPageTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static void Set(object o,string name,object? value){
        for(var t=o.GetType();t!=null;t=t.BaseType){
            if(t.GetProperty(name,F|BindingFlags.DeclaredOnly) is {} p){p.SetValue(o,value);return;}
            if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f){f.SetValue(o,value);return;}
        }
        throw new MissingMemberException(o.GetType().Name,name);
    }
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    /// <summary>S39 "buddy"（3.81 S_Buddy）：D owner、S "buddy"、H 2、H 2、S 全部名單、S 在線名單（每個名字後面一個空格）。</summary>
    static byte[] BuddyList(uint owner,string all,string online)=>P([39],Le32(owner),Str("buddy"),Le16(2),Le16(2),Str(all),Str(online));
    static NpcTemplates Templates(Dictionary<string,string> rows){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,new Dictionary<string,string>(rows,StringComparer.OrdinalIgnoreCase));
        return t;
    }
    // 合成模板：只保留 buddy-c.html 的變數位置（#0、#1、#2），文字是測試自訂。
    const string Page="<body>A<var src=\"#0\">B<var src=\"#1\">C<br><p><var src=\"#2\"></body>";
    static LoginConnection Seed(bool templates=true,bool ready=true){
        var c=new LoginConnection();var w=c.World;
        Set(c,"State",LoginState.InWorld);Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",ready?1:0);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Me},\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}}"));
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>($"{{\"ObjectId\":{Me},\"Name\":\"測試者\",\"Gfx\":0,\"X\":32600,\"Y\":32940,\"HpPercent\":100}}"));
        if(templates)w.DialogTemplates=Templates(new(){["buddy"]=Page});
        return c;
    }

    [WorldModuleTest("buddy-page")]
    static void Run(WorldModuleTestContext t){
        // 1. 變數陣列＝3.81 的 [全部空格數, 全部, 在線空格數, 在線]（數的是空格字元，不是去重後的人數）。
        t.Equal("3|甲 乙 丙 |1|甲 ",string.Join("|",BuddyPage.Arguments("甲 乙 丙 ","甲 ")),"3.81 變數陣列（sub_4A5220 0x4A55D6–0x4A5631）");
        t.Equal("0||0|",string.Join("|",BuddyPage.Arguments("","")),"空名單：兩個 0");
        t.Equal(2,BuddyPage.SpaceCount("甲  乙"),"連續空格各算一個（照 3.81 逐字元數）");
        t.Check(BuddyPackets.TryBuddy(BuddyList(Me,"甲 乙 ","乙 "),out var packet)&&packet!.All=="甲 乙 "&&packet.Online=="乙 "&&packet.Entries.Length==2,"TryBuddy 保留兩個字串原文",packet);

        // 2. 收到 S39 buddy → 開 buddy 頁（伺服器查詢頁），模板照變數陣列填；名單照舊更新；NPC 對話不動。
        using(var c=Seed()){
            var w=c.World;t.Check(w.Ready,"seed session is ready");
            int interaction=w.InteractionRevision,query=w.QueryRevision;
            w.Receive(BuddyList(Me,"甲 乙 丙 ","甲 "),"");
            t.Check(w.BuddyLoaded&&w.Buddies.Select(b=>b.Name).SequenceEqual(["甲","乙","丙"])&&w.Buddies[0].Online,"名單模型照舊更新",w.Buddies);
            t.Check(w.QueryVisible&&w.QueryHtml=="buddy"&&w.QueryRevision==query+1,"開 buddy 頁",w.QueryHtml);
            t.Check(w.QueryPage!.Text.StartsWith("A3B甲 乙 丙",StringComparison.Ordinal)&&w.QueryPage.Text.TrimEnd().EndsWith('1'),"#0＝全部空格數、#1＝全部名單、#2＝在線空格數",w.QueryPage.Text);
            t.Check(!w.QueryActionable&&w.QueryPage.Options.Length==0,"頁上沒有可送的選項");
            t.Check(w.InteractionKind=="closed"&&w.InteractionRevision==interaction,"不開、不改 NPC 對話");
            // 再查一次：同一頁換成新內容。
            w.Receive(BuddyList(Me,"甲 ",""),"");
            t.Check(w.QueryHtml=="buddy"&&w.QueryRevision==query+2&&w.QueryPage!.Text.StartsWith("A1B甲",StringComparison.Ordinal),"再收到 S39 buddy 換新內容",w.QueryPage?.Text);
        }
        // 3. 沒有模板：以解碼後的參數顯示（HyperTextFallbackNotice），不冒充原版版面。
        using(var c=Seed(templates:false)){
            var w=c.World;w.Receive(BuddyList(Me,"甲 乙 ","乙 "),"");
            t.Check(w.QueryVisible&&w.QueryPage!.Text=="2\n甲 乙 \n1\n乙 "&&w.QueryPage.Limitation==WorldSession.HyperTextFallbackNotice,"沒有模板 → 參數文字＋限制說明",w.QueryPage?.Text);
        }
        // 4. 世界未就緒：名單更新，但不開頁。
        using(var c=Seed(ready:false)){
            var w=c.World;w.Receive(BuddyList(Me,"甲 ",""),"");
            t.Check(w.BuddyLoaded&&!w.QueryVisible&&w.QueryPage==null,"未就緒不開頁");
        }
        // 5. owner 不是自己：照舊拒絕，不開頁、不改名單。
        using(var c=Seed()){
            var w=c.World;int rev=w.BuddyRevision;
            t.Throws<ProtocolException>(()=>w.Receive(BuddyList(999,"甲 ",""),""),"別人的 owner 拒絕");
            t.Check(!w.QueryVisible&&w.BuddyRevision==rev&&!w.BuddyLoaded,"拒絕時不開頁、不改名單");
        }
    }
}
