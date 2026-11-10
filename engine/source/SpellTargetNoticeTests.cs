namespace P06;

// T378（#862，FU9§8）選目標模式提示的離線測試：179 是字串（不是游標），進入模式時印到聊天欄（palette[24]）。
// 只用記憶體內的 WorldSession 與合成字串表（或明確設成「沒有表」）；不讀客戶端素材、不開遊戲、網路、視窗。
// 由 --module-tests 自動發現執行（名稱 spell-target-notice）。
static class SpellTargetNoticeTests {
    static ServerStringTable Table(int id,string text){
        var lines=new string[id+1];Array.Fill(lines,"");lines[id]=text;
        return ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes((id+1)+"\n"+string.Join("\n",lines)));
    }

    [WorldModuleTest("spell-target-notice")]
    static void Run(WorldModuleTestContext t){
        t.Check(SpellTargeting.Cursor==179&&SpellTargeting.Cursor==ChatFollowupCommands.SelectTarget,"sub_F691C0 第 3 參數 = 字串 179（與組隊 /invite 同一筆）");
        t.Equal(24,SpellTargeting.NoticeColorIndex,"sub_7DC800 色 palette[24]");
        t.Equal(15,SpellTargeting.Range,"範圍 15 格不變");
        using(ServerMessageText.UseTable(null))t.Equal("請選擇對象。",SpellTargeting.PickNotice,"沒有表：本機 string-c.tbl 第 179 行原文");
        using(ServerMessageText.UseTable(Table(179,@"\f1請點目標")))t.Equal("請點目標",SpellTargeting.PickNotice,"有表：照第 179 行並去色碼");

        using(ServerMessageText.UseTable(Table(179,@"\f1請選擇對象。"))){
            var w=new WorldSession();
            int lines=w.PlayerChatLines.Length,rev=w.PlayerChatRevision,magic=w.MagicRevision;
            w.BeginSpellTargetPick(4);
            t.Check(w.SpellTargetPick is {Skill:4,Range:15,Cursor:179},"進入選目標模式",w.SpellTargetPick);
            t.Equal(lines+1,w.PlayerChatLines.Length,"聊天欄多一行");
            var last=w.PlayerChatLines[^1];
            t.Check(last.Text=="請選擇對象。"&&last.ColorIndex==24&&last.Speaker==0,"字串 179、色 24、系統行（沒有說話者）",last);
            t.Check(w.PlayerChatRevision>rev&&w.MagicRevision>magic,"聊天與魔法版本都前進");
            t.Equal("請選擇對象。",w.MagicNotice,"MagicNotice 同一行字");
            w.CancelSpellTargetPick("測試");
            t.Equal(lines+1,w.PlayerChatLines.Length,"取消不再印 179");
            for(int i=0;i<120;i++)w.BeginSpellTargetPick(4);
            t.Equal(100,w.PlayerChatLines.Length,"聊天欄最多保留 100 行");
            t.Check(w.PlayerChatLines.All(l=>l.Text=="請選擇對象。"),"每次進入模式各印一行");
        }
    }
}
