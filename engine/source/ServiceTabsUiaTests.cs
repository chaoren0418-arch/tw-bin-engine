namespace P06;

// #1172（#1165 服務頁巢狀分頁）離線檢查：「開發工具服務分頁」normalPanels 的一般 TabPage 也對跨執行緒 WM_GETTEXT 回空字串。
// 不建 handle、不開視窗；真機 UIA 樹（TabItem 名稱／矩形／Select、「血盟」在樹中）只能在實機驗收（f1165-retest N01）。
static class ServiceTabsUiaTests {
    [WorldModuleTest("service-tabs-uia")]
    static void Run(WorldModuleTestContext t){
        // 1. 共用判定與 DevToolsPage 一致（外層行為不變）。
        foreach(uint ismex in new uint[]{0,1,2,4,9})
            t.Equal(DevToolsPage.HidesWindowText(ismex),CrossThreadTabText.HidesWindowText(ismex),"DevToolsPage and the shared helper agree on InSendMessageEx="+ismex);
        t.Check(!CrossThreadTabText.HidesWindowText(0),"same-thread WM_GETTEXT keeps the page title");
        // 2. 哪些控制項要掛子類別：一般 TabPage 要；DevToolsPage（自己處理）與非分頁控制項不要。
        t.Check(CrossThreadTabText.NeedsSubclass(new TabPage("血盟")),"plain TabPage needs the subclass");
        t.Check(!CrossThreadTabText.NeedsSubclass(new DevToolsPage("服務")),"DevToolsPage already hides its text");
        t.Check(!CrossThreadTabText.NeedsSubclass(new Panel())&&!CrossThreadTabText.NeedsSubclass(null),"non-page controls are left alone");

        // 3. Apply：Apply 之前已在的分頁、之後用 Add／AddRange 加入的分頁都掛上；標題、順序不變，不建 handle。
        string[] titles=["背包","地面","魔法","角色","能力點","個人商店","NPC","交易","記憶座標","好友／封鎖","血盟戰爭","血盟","聲音","決鬥","求婚","寵物","名稱顯示","稱號／表情"];
        using var tabs=new TabControl{Multiline=true,AccessibleName="開發工具服務分頁"};
        var early=new TabPage(titles[0]);tabs.TabPages.Add(early);
        CrossThreadTabText.Apply(tabs);
        var pages=new List<TabPage>{early};
        for(int i=1;i<titles.Length;i++){
            if(titles[i]=="決鬥"){var duel=new TabPage(titles[i]);var proposal=new TabPage(titles[++i]);tabs.TabPages.AddRange([duel,proposal]);pages.AddRange([duel,proposal]);continue;}
            var page=new TabPage(titles[i]);tabs.TabPages.Add(page);pages.Add(page);
        }
        string[] actual=tabs.TabPages.Cast<TabPage>().Select(p=>p.Text).ToArray();
        t.Check(actual.SequenceEqual(titles),"page order and titles unchanged",actual);
        foreach(var page in pages)t.Check(CrossThreadTabText.IsAttached(page),"page "+page.Text+" hides its cross-thread window text");
        t.Check(pages.All(p=>!p.IsHandleCreated)&&!tabs.IsHandleCreated,"no window handle created by the checks");
        t.Check(pages.All(p=>p.AccessibleName==null),"AccessibleName untouched (nested pages keep their default UIA names)");

        // 4. 動態移除／加回（RingWindows「盟屋拍賣」）：不重複掛，加回後仍掛著。
        var agit=new TabPage("盟屋拍賣");
        tabs.TabPages.Add(agit);
        t.Check(CrossThreadTabText.IsAttached(agit),"dynamic page attached on first add");
        tabs.TabPages.Remove(agit);
        t.Check(!CrossThreadTabText.Attach(agit),"removed page is not attached twice");
        tabs.TabPages.Add(agit);
        t.Check(CrossThreadTabText.IsAttached(agit)&&tabs.TabPages.Contains(agit),"re-added page still attached");

        // 5. DevToolsPage 加進同一個 TabControl 也不另外掛（避免雙重處理）。
        var dev=new DevToolsPage("外層");tabs.TabPages.Add(dev);
        t.Check(!CrossThreadTabText.IsAttached(dev),"DevToolsPage is not subclassed again");
        tabs.TabPages.Remove(dev);

        // 6. 對照 #1163 的代理模型：修正後每一頁跨執行緒看到空字串，代理不接手任何巢狀分頁。
        for(int i=0;i<titles.Length;i++){
            string seen=CrossThreadTabText.HidesWindowText(1)?"":titles[i];
            t.Equal(i,DevToolsTabUiaTests.ProxyOverrideIndex(titles[i],titles),"before #1172 the proxy claims nested page "+titles[i]);
            t.Equal(-1,DevToolsTabUiaTests.ProxyOverrideIndex(seen,titles),"after #1172 the proxy leaves nested page "+titles[i]+" to the WinForms provider");
        }
    }
}
