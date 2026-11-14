namespace P06;

// #1163（#1150 G07）離線檢查：開發工具分頁對 UIA 的「分頁索引 ↔ 名稱 ↔ HWND 文字」對應。不建 handle、不開視窗。
// 真機的 UIA 樹（TabItem 名稱／矩形／Select）只能在實機驗收（f1150-retest）。
static class DevToolsTabUiaTests {
    /// <summary>
    /// 模擬 .NET Framework UIA 用戶端代理 WindowsTab.GetOverrideProviderForHwnd（UIAutomationClientsideProviders 4.8）：
    /// 分頁 HWND 的視窗文字是空字串就不接手；否則回傳第一個標籤文字相同的原生索引（接手＝把 Pane 掛到代理 TabItem 底下）。
    /// </summary>
    internal static int ProxyOverrideIndex(string pageWindowText,IReadOnlyList<string> tabTexts){
        if(string.IsNullOrEmpty(pageWindowText))return -1;
        for(int i=0;i<tabTexts.Count;i++)if(pageWindowText==tabTexts[i])return i;
        return -1;
    }
    /// <summary>其他執行緒讀分頁 HWND 文字時看到的字串（DevToolsPage.WndProc 的行為）。</summary>
    static string CrossThreadText(string title,uint inSendMessageEx)=>DevToolsPage.HidesWindowText(inSendMessageEx)?"":title;

    [WorldModuleTest("devtools-tab-uia")]
    static void Run(WorldModuleTestContext t){
        string[] titles=["網頁面板","服務","登入／選角","控制","狀態列","Button"];
        // 1. InSendMessageEx：0＝本執行緒（ISMEX_NOSEND），其餘（ISMEX_SEND=1、ISMEX_NOTIFY=2、ISMEX_CALLBACK=4、加 ISMEX_REPLIED=8）都是其他執行緒。
        t.Check(!DevToolsPage.HidesWindowText(0),"same-thread WM_GETTEXT keeps the page title (WinForms reads its own Text)");
        foreach(uint ismex in new uint[]{1,2,4,9})t.Check(DevToolsPage.HidesWindowText(ismex),"cross-thread WM_GETTEXT hides the page text "+ismex);
        // 2. 修正前：分頁 HWND 文字＝標籤文字，代理對每一頁都會接手（G07 的錯位來源）；修正後：跨執行緒看到空字串，沒有任何一頁被接手。
        for(int i=0;i<titles.Length;i++){
            t.Equal(i,ProxyOverrideIndex(titles[i],titles),"before #1163 the proxy claims page "+titles[i]);
            t.Equal(-1,ProxyOverrideIndex(CrossThreadText(titles[i],1),titles),"after #1163 the proxy leaves page "+titles[i]+" to the WinForms provider");
        }
        // 3. AddPage：每頁都是 DevToolsPage，標題、順序不變；Pane 的 AccessibleName＝分頁標題；內容在該頁裡。
        using var tools=new DevToolsForm(Path.Combine("scratch","devtools-tab-uia-missing","dev-tools-window.json"));
        var contents=titles.Select(_=>new Panel()).ToArray();
        for(int i=0;i<titles.Length;i++)tools.AddPage(titles[i],contents[i]);
        t.Check(tools.PageTitles.SequenceEqual(titles),"page order and titles unchanged",tools.PageTitles);
        for(int i=0;i<titles.Length;i++){
            var page=contents[i].Parent as TabPage;
            t.Check(page is DevToolsPage&&page.Text==titles[i]&&page.AccessibleName==titles[i]&&contents[i].Dock==DockStyle.Fill,"page "+i+" is a DevToolsPage named "+titles[i],page?.GetType().Name);
        }
        // 4. SelectPage 仍以內容控制項選頁（G04／ShowDevTools 依賴）。
        tools.SelectPage(contents[2]);
        t.Check((contents[2].Parent as TabPage)?.Parent is TabControl tc&&tc.SelectedTab==contents[2].Parent,"SelectPage(content) selects the page that holds it");
    }
}
