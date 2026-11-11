using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
namespace P06.NativeUi;

// Q9-C（T438／#1009）離線測試：HTML 的 <date>／<time>／<var src="#998">／<var src="#999">（NativeUi/HyperTextLayout.cs 的 HyperTextClock、
// NpcTemplates.cs）。依據 docs/reference/followups-17.md §3.2、§3.4。只用自寫的合成 HTML、合成字串表與固定時間值；
// 不讀客戶端素材、不開遊戲／網路／視窗。由 --module-tests 執行（群組 hypertext-date）。
static class HyperTextDateTests {
    [WorldModuleTest("hypertext-date")]
    static void Run(WorldModuleTestContext t){
        Convert(t);Formats(t);Strings(t);Pages(t);Session(t);
    }

    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    // 1970-03-05 09:05:00、1970-11-23 18:30:00（UTC 秒數）。
    const uint March5=5475900,Nov23=28233000;

    static NpcTemplates Templates(params (string Key,string Html)[] pages){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (k,h) in pages)d[k]=h;
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,d);
        return t;
    }
    static ServerStringTable Table(string month,string day){
        var lines=Enumerable.Range(0,764).Select(i=>i==762?month:i==763?day:"").ToArray();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes("764\n"+string.Join("\n",lines)));
    }

    // 1. sub_A23760：< 0x80000000 直接當 time_t；否則減 2019686400、年份 +64；再 gmtime（UTC）。
    static void Convert(WorldModuleTestContext t){
        t.Equal(new HyperTextClock.GameTime(1970,1,1,0,0,0),HyperTextClock.Convert(0),"0 → 1970-01-01 00:00:00");
        t.Equal(new HyperTextClock.GameTime(1970,3,5,9,5,0),HyperTextClock.Convert(March5),"直接當 time_t");
        t.Equal(new HyperTextClock.GameTime(2038,1,19,3,14,7),HyperTextClock.Convert(0x7FFFFFFF),"0x7FFFFFFF 不換算");
        t.Equal(new HyperTextClock.GameTime(2038,1,19,3,14,8),HyperTextClock.Convert(0x80000000),"0x80000000：減 2019686400＝1974-01-19，年份 +64");
        // 23376 日不等於 2042→2106 的 64 年（2100 不是閏年），所以和無號 time_t 差一天：照原版規則，不是 2106-02-07。
        t.Equal(new HyperTextClock.GameTime(2106,2,6,6,28,15),HyperTextClock.Convert(0xFFFFFFFF),"0xFFFFFFFF：照原版規則（2042-02-06＋64 年）");
    }

    // 2. 格式：<date>「%2d%s %2d%s」、<time>「%02d:%02d」、#999「%2d:%2d」。
    static void Formats(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);   // 沒有字串表：用 762「月」／763「日」
        t.Equal(" 3月  5日",HyperTextClock.Date(March5),"單位數月日補空白");
        t.Equal("11月 23日",HyperTextClock.Date(Nov23),"雙位數月日");
        t.Equal("09:05",HyperTextClock.Time(March5),"<time> 補 0");
        t.Equal("18:30",HyperTextClock.Time(Nov23),"<time> 雙位數");
        t.Equal(" 9: 5",HyperTextClock.VarTime(March5),"#999 補空白");
        t.Equal("18:30",HyperTextClock.VarTime(Nov23),"#999 雙位數");
        t.Equal(" 1月 19日",HyperTextClock.Date(0x80000000),"高位值換算後的日期");
        t.Equal(" 1月  1日",HyperTextClock.Date(0),"0 → 1月 1日");
    }

    // 3. 字串 762／763 取自字串表；表比 763 短時是空字串（sub_4EAFF0 超出筆數回 ""）。
    static void Strings(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(Table("M","D")))t.Equal(" 3M  5D",HyperTextClock.Date(March5),"讀字串表 762／763");
        using(ServerMessageText.UseTable(ServerStringTable.Parse(Encoding.ASCII.GetBytes("700\n"))))t.Equal(" 3  5",HyperTextClock.Date(March5),"表沒有 762／763 → 空字串");
    }

    // 4. NpcTemplates 與 HyperTextMarkup 輸出相同；#998／#999 是客戶端變數，不讀參數、不算缺少參數；不再標「部分原始標記尚未支援」。
    static void Pages(WorldModuleTestContext t){
        using var table=ServerMessageText.UseTable(Table("月","日"));
        using var clock=HyperTextClock.Use(()=>Nov23);
        var templates=Templates(("today","<html>今天是<date>。<br>現在 <time>，<var src=\"#999\">。<br><var src=\"#998\">／<var src=\"#0\"></html>"),
            ("link","<html><a action=\"x\"><date></a></html>"));
        string[] args=["甲"];
        var page=templates.Render("today",args,"商人","勇者");
        t.Equal("今天是11月 23日。\n現在 18:30，18:30。\n勇者／甲",page.Text,"NpcTemplates 輸出");
        t.Equal("",page.Limitation,"沒有「尚未支援」或缺少參數的提示");
        var doc=HyperTextMarkup.Parse(NpcTemplateMarkup.Markup(templates,"today")??"",args,"商人","勇者");
        t.Equal(page.Text,doc.Plain,"HyperTextMarkup 純文字同 NpcTemplates");
        t.Check(doc.Nodes.Any(n=>n.Kind==HyperTextNodeKind.Text&&n.Text=="11月 23日"),"<date> 是文字節點");
        var link=templates.Render("link",[],"商人","勇者");
        t.Equal("11月 23日",link.Options.Single().Label,"<a> 裡的 <date> 成為選項文字");
        var linkDoc=HyperTextMarkup.Parse(NpcTemplateMarkup.Markup(templates,"link")??"",[],"商人","勇者");
        t.Equal(1,linkDoc.LinkCount,"選項數相同");
        t.Check(linkDoc.Nodes.Any(n=>n.Text=="11月 23日"&&n.Link==0),"<a> 裡的 <date> 可點");
        // 參數再多也不蓋過 #998／#999。
        var many=Enumerable.Range(0,1000).Select(i=>"參"+i).ToArray();
        t.Equal("勇者",Templates(("p","<var src=\"#998\">")).Render("p",many,"商人","勇者").Text,"#998 不讀參數");
        t.Equal("18:30",Templates(("p","<var src=\"#999\">")).Render("p",many,"商人","勇者").Text,"#999 不讀參數");
        // #997 仍是一般參數（缺少時照舊提示）。
        t.Check(Templates(("p","<var src=\"#997\">")).Render("p",args,"商人","勇者").Limitation.Contains("#997"),"#997 仍是參數");
    }

    // 5. 時間來源：S123／S8 觀察器自動登錄；取最後收到的那一種，沒有時用 0。
    static void Session(WorldModuleTestContext t){
        var e=WorldExtensionRegistry.Default.Entries;
        t.Check(e.Any(x=>x is {Kind:"observe",Opcode:123,Method:"WorldSession.ObserveHyperTextServerTime"}),"S123 觀察器自動登錄");
        t.Check(e.Any(x=>x is {Kind:"observe",Opcode:8,Method:"WorldSession.ObserveHyperTextStatusTime"}),"S8 觀察器自動登錄");
        t.Check(!e.Any(x=>x.Kind=="receive"&&x.Opcode is 123 or 8),"不註冊消化型（核心 S123／S8 照舊）");
        using(HyperTextClock.Use(()=>null)){
            t.Equal((uint?)null,HyperTextClock.Seconds,"沒有時間 → null");
            t.Equal(0u,HyperTextClock.Value,"沒有時間 → 0");
        }
        var s=new WorldSession();
        s.Receive([123,0x10,0x20,0x30,0x40,0,0,0],"");
        t.Equal((uint?)0x40302010,s.ServerTime,"核心 S123 照舊設 ServerTime");
        t.Equal((uint?)0x40302010,HyperTextClock.Seconds,"時鐘讀最後收到 S123 的 session");
        HyperTextClock.Observed(s,true);
        t.Equal((uint?)0x40302010,HyperTextClock.Seconds,"最後是 S8 但沒有 Status → 退回 ServerTime");
        using(ServerMessageText.UseTable(null))using(HyperTextClock.Use(()=>March5))
            t.Equal("3月  5日",Templates(("p","<date>")).Render("p",[],"","").Text,"測試來源可替換（頁面頭尾空白照舊去掉）");
    }
}
