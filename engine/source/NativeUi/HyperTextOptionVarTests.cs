using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
namespace P06.NativeUi;

// Q9-D（T439／#1010）離線測試：HTML 的 <var src="#N">，N 在 1002–1029 裡引擎有設定的選項值（NativeUi/HyperTextLayout.cs 的
// HyperTextOptionVars、NpcTemplates.cs）。依據 docs/reference/followups-17.md §3.4。只用自寫的合成 HTML、合成字串表與記憶體內的
// WorldSession；不讀客戶端素材、不開遊戲／網路／視窗。由 --module-tests 執行（群組 hypertext-option-var）。
static class HyperTextOptionVarTests {
    [WorldModuleTest("hypertext-option-var")]
    static void Run(WorldModuleTestContext t){
        HeadBubbleOptions.SetCached(true);bool frames=HeadBubbleOptions.BubbleFrames;
        try{Defaults(t);Changes(t);HpMp(t);Missing(t);Strings(t);Pages(t);}
        finally{HeadBubbleOptions.SetCached(null);HeadBubbleOptions.BubbleFrames=frames;}
    }

    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static NpcTemplates Templates(params (string Key,string Html)[] pages){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (k,h) in pages)d[k]=h;
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,d);
        return t;
    }
    static string Var(int id)=>Templates(("p",$"<var src=\"#{id}\">")).Render("p",[],"商人","勇者").Text;
    static NpcPage Page(int id)=>Templates(("p",$"<var src=\"#{id}\">")).Render("p",[],"商人","勇者");
    /// <summary>進入世界的 session（頻道設定只在 Entered＋Status 時才算本場的值）。</summary>
    static WorldSession Entered(){
        var w=new WorldSession();Set(w,"Entered",true);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        return w;
    }

    // 1. 預設值：交易詢問 1（可使用）、密語／買賣頻道開、全部拾取關；泡泡話框關、個人商店名稱開。
    static void Defaults(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);
        HeadBubbleOptions.BubbleFrames=false;
        using var src=HyperTextOptionVars.Use(new WorldSession());
        t.Equal("可使用",Var(1019),"1019 accepttrade 預設 1 → 573");
        t.Equal("開",Var(1005),"1005 dowhisper 預設開 → 567");
        t.Equal("開",Var(1020),"1020 dotradechat 預設開 → 567");
        t.Equal("關",Var(1002),"1002 getall 預設關 → 568");
        t.Equal("關",Var(1003),"1003 bubbleon 預設關（bss 0）→ 568");
        t.Equal("開",Var(1027),"1027 showMargetMsg 預設開 → 567");
        t.Equal("",Page(1019).Limitation,"有值：不算缺少參數");
    }

    // 2. 值跟著引擎設定變。
    static void Changes(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);
        var w=Entered();
        using var src=HyperTextOptionVars.Use(w);
        w.RememberChatChannels(new(true,false,false,false));
        t.Check(!w.TradeAskWindow&&!w.ChatChannels.Whisper&&!w.ChatChannels.Trade,"前提：設定已寫入本場");
        t.Equal("無法使用",Var(1019),"1019 交易詢問 0 → 574");
        t.Equal("關",Var(1005),"1005 密語關 → 568");
        t.Equal("關",Var(1020),"1020 買賣關 → 568");
        w.TradeAskWindow=true;
        t.Equal("可使用",Var(1019),"1019 改回 1 → 573");
        w.TogglePickupAll();
        t.Equal("開",Var(1002),"1002 全部拾取開 → 567");
        HeadBubbleOptions.BubbleFrames=true;
        t.Equal("開",Var(1003),"1003 泡泡話框開 → 567");
        HeadBubbleOptions.SetCached(false);
        t.Equal("關",Var(1027),"1027 個人商店名稱關 → 568");
        HeadBubbleOptions.SetCached(true);
    }

    // 3. HP/MP：只有掛上 HpMpPanel 的設定才有值；沒掛照舊缺少參數。
    static void HpMp(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);
        var w=new WorldSession();
        using(HyperTextOptionVars.Use(w,()=>HpMpSettings.Default)){
            t.Equal("開",Var(1023),"1023 showhpmp 預設開 → 567");
            t.Equal("開",Var(1024),"1024 showfloathpmp 預設開 → 567");
        }
        using(HyperTextOptionVars.Use(w,()=>HpMpSettings.Default with{ShowCharacterHpMp=false,Show=false})){
            t.Equal("關",Var(1023),"1023 關 → 568");
            t.Equal("關",Var(1024),"1024 關 → 568");
        }
        using(HyperTextOptionVars.Use(w,()=>null))t.Check(Page(1023).Limitation.Contains("#1023"),"HP/MP 視窗不存在 → 缺少參數");
        using(HyperTextOptionVars.Use(w,null))t.Equal("〔缺少參數 #1024〕",Var(1024),"沒有 HP/MP 來源 → 缺少參數");
    }

    // 4. 沒有對應設定的號碼（以及沒有 session 時的世界值）維持「缺少參數」；1030／2001 以上不做。
    static void Missing(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);
        using(HyperTextOptionVars.Use(new WorldSession(),()=>HpMpSettings.Default)){
            foreach(int id in new[]{1000,1001,1004,1006,1007,1008,1009,1010,1014,1021,1022,1025,1026,1028,1029,1030,1032,2001,2027}){
                t.Equal($"〔缺少參數 #{id}〕",Var(id),id+" 沒有引擎設定 → 缺少參數");
                t.Equal((bool?)null,HyperTextOptionVars.Flag(id),id+" Flag null");
            }
            t.Check(Page(1025).Limitation.Contains("#1025"),"缺少參數提示");
        }
        using(HyperTextOptionVars.Use(null,null)){
            foreach(int id in new[]{1002,1005,1019,1020})t.Equal($"〔缺少參數 #{id}〕",Var(id),id+" 沒有 session → 缺少參數");
            HeadBubbleOptions.BubbleFrames=false;
            t.Equal("關",Var(1003),"1003 是全域選項，不需要 session");
        }
        t.Check(HyperTextOptionVars.Supported.SequenceEqual(HyperTextOptionVars.Supported.Order())&&HyperTextOptionVars.Supported.All(i=>i is >=1002 and <=1029),"支援表遞增且在 3.81 範圍");
    }

    // 5. 字串 567／568／573／574 取自字串表；表太短時是空字串（同 762／763）。
    static void Strings(WorldModuleTestContext t){
        var lines=Enumerable.Range(0,575).Select(i=>i switch{567=>"ON",568=>"OFF",573=>"OK",574=>"NG",_=>""}).ToArray();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var table=ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes("575\n"+string.Join("\n",lines)));
        var w=new WorldSession();
        using var src=HyperTextOptionVars.Use(w);
        using(ServerMessageText.UseTable(table)){
            t.Equal("ON",Var(1005),"讀字串表 567");
            t.Equal("OK",Var(1019),"讀字串表 573");
            w.TradeAskWindow=false;   // 沒進世界：寫入不算本場，仍是預設 1
            t.Equal("OK",Var(1019),"未進世界的寫入不改值");
            t.Equal("OFF",Var(1002),"讀字串表 568");
        }
        using(ServerMessageText.UseTable(ServerStringTable.Parse(Encoding.ASCII.GetBytes("500\n"))))
            t.Equal("",Var(1005),"表沒有 567 → 空字串（不是缺少參數）");
    }

    // 6. NpcTemplates 與 HyperTextMarkup 輸出相同（純文字、選項）；參數再多也不蓋過選項值。
    static void Pages(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);
        var w=Entered();w.RememberChatChannels(new(true,true,false,false));
        using var src=HyperTextOptionVars.Use(w,()=>HpMpSettings.Default);
        const string html="<html>交易：<a action=\"accepttrade\"><var src=\"#1019\"></a><br>密語 <var src=\"#1005\">、買賣 <var src=\"#1020\"><br>"+
            "HP/MP 條 <a action=\"showhpmp\"><var src=\"#1023\"></a>，透明度 <var src=\"#1025\"></html>";
        var templates=Templates(("opt",html));
        var page=templates.Render("opt",[],"","勇者");
        t.Equal("交易：無法使用\n密語 開、買賣 關\nHP/MP 條 開，透明度 〔缺少參數 #1025〕",page.Text,"NpcTemplates 輸出");
        t.Check(page.Options.Select(o=>o.Label).SequenceEqual(["無法使用","開"]),"<a> 裡的選項值成為選項文字",page.Options.Select(o=>o.Label).ToArray());
        t.Check(page.Limitation.Contains("#1025")&&!page.Limitation.Contains("#1019"),"只有 1025 是缺少參數");
        var doc=HyperTextMarkup.Parse(NpcTemplateMarkup.Markup(templates,"opt")??"",[],"","勇者");
        t.Equal(page.Text,doc.Plain,"HyperTextMarkup 純文字同 NpcTemplates");
        t.Equal(page.Options.Length,doc.LinkCount,"選項數相同");
        t.Check(doc.Nodes.Any(n=>n.Text=="無法使用"&&n.Link==0)&&doc.Nodes.Any(n=>n.Text=="開"&&n.Link==1),"<a> 裡的選項值可點");
        var many=Enumerable.Range(0,1100).Select(i=>"參"+i).ToArray();
        t.Equal("無法使用",Templates(("p","<var src=\"#1019\">")).Render("p",many,"","").Text,"#1019 不讀參數");
        t.Equal("參1025",Templates(("p","<var src=\"#1025\">")).Render("p",many,"","").Text,"沒有設定的號碼照舊讀參數（行為不變）");
    }
}
