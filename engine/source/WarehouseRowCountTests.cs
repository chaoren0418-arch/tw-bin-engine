using P06.NativeUi;
namespace P06;

// R10-P（T400／#915）離線測試：原生倉庫窗的列內數量「▼ 數量 ▲」（docs/reference/followups-12.md §1.5）。
// 預設 0、▲ 超過持有量歸 0、▼ 在 0 跳到持有量、點列選取並清緩衝、0–9 逐位輸入夾到持有量、Backspace、數字鍵盤 ±、
// 確定只送數量 > 0 的列、取出手續費＝種類數 × 30、▼▲ 圖（3005／3003）畫在列內。
// 只用合成 XML／surf，不讀客戶端、不連線、不開視窗。由 --module-tests 執行（名稱 native-warehouse-row-count）。
static class WarehouseRowCountTests {
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Press(IUiPanel p,Point at,IUiCommands c){p.Mouse(M(UiMouseKind.Down,at),c);}
    static void Drop(IUiPanel p,Point at,IUiCommands c){p.Mouse(M(UiMouseKind.Up,at),c);}
    static void Tap(IUiPanel p,Point at,IUiCommands c){Press(p,at,c);Drop(p,at,c);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static bool Key(IUiPanel p,Keys k,IUiCommands c)=>p.Key(new UiKeyEvent(k,true,false),c);

    sealed class Commands:IUiCommands,IUiWarehouseCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus(){}
        public void Logout(){}
        public void SubmitWarehouse(int revision,IReadOnlyList<ShopChoice> choices)=>Calls.Add($"submit:{revision}:"+string.Join(",",choices.Select(c=>$"{c.Id}x{c.Count}")));
        public void CancelWarehouse(int revision)=>Calls.Add($"cancel:{revision}");
    }

    /// <summary>Synthetic surfaces; arrows = the local client's 7 × 7 3003 (UP) / 3005 (DOWN) are present.</summary>
    static LegacyWindowTestKit.SurfaceSet Surfaces(bool arrows){
        var sizes=new Dictionary<int,(int W,int H)>{
            [3001]=(220,320),[3002]=(250,360),[3031]=(14,14),[3024]=(10,220),[3025]=(10,12),[3027]=(10,12),[3029]=(10,10),
            [3007]=(60,14),[3014]=(50,18),[3017]=(50,18),[3008]=(30,12),[3009]=(30,12),[3010]=(9,9),[3012]=(9,9),
            [3020]=(180,20),[3021]=(180,20),[3022]=(180,20),[3023]=(180,20)};
        if(arrows){sizes[3003]=(7,7);sizes[3005]=(7,7);}
        return LegacyWindowTestKit.Surfaces(sizes);
    }

    [WorldModuleTest("native-warehouse-row-count")]
    static void Run(WorldModuleTestContext t){
        var p=new WarehouseWindowPanel();
        p.Load(UiLayout.Parse(WarehouseWindowPanel.TestXml,"uiextra86-c.xml"),Surfaces(true).Get);
        var rows=new ShopRow[]{new(11,5,30,"長劍",1,""),new(12,6,30,"箭",500,""),new(13,7,30,"藥水",20,"")};
        IUiGameState S(UiWarehouseView v)=>new UiWarehouseGameState(new UiStaticGameState(InWorld:true),v);
        p.Update(S(new UiWarehouseView("withdraw",1,rows,30)));
        var cmd=new Commands();

        // 1. 預設 0；▲／▼ 的位置。
        t.Check(p.Visible&&p.Selection.Count==0&&rows.All(r=>p.CountOf(r.Id)==0)&&p.FocusedRow==null,"new list: every count 0, nothing selected or focused");
        var r0=p.RowRect(0);
        t.Equal(new Rectangle(r0.X+180,r0.Bottom-14,12,12),p.CountUpRect(0),"▲ 12 × 12 at row x + 180 (sub_4D83B0)");
        t.Equal(new Rectangle(r0.X+120,r0.Bottom-14,12,12),p.CountDownRect(0),"▼ 12 × 12 at row x + 120 (推測), same line");
        t.Check(p.CountUpRect(5)==Rectangle.Empty&&p.CountDownRect(-1)==Rectangle.Empty,"no buttons for rows that are not shown");
        t.Check(!p.PaintedParts.Any(x=>x.Surf is 3003 or 3005),"3003 / 3005 are not window parts (drawn per row only)");

        // 2. ▲：+1，超過持有量歸 0。▼：−1，0 時跳到持有量。
        Tap(p,Mid(p.CountUpRect(0)),cmd);
        t.Check(p.CountOf(11)==1&&p.Selection.Count==1&&p.FocusedRow==11,"▲ on 長劍 (held 1) → 1; the row is focused");
        Tap(p,Mid(p.CountUpRect(0)),cmd);
        t.Check(p.CountOf(11)==0&&p.Selection.Count==0,"▲ above the held count → 0 (not selected)");
        Tap(p,Mid(p.CountDownRect(1)),cmd);
        t.Equal(500u,p.CountOf(12),"▼ at 0 → held count (500 arrows, all)");
        Tap(p,Mid(p.CountDownRect(1)),cmd);
        t.Equal(499u,p.CountOf(12),"▼ again → 499");
        for(int k=0;k<2;k++)Tap(p,Mid(p.CountUpRect(1)),cmd);
        t.Equal(0u,p.CountOf(12),"▲ 499 → 500 → 0");
        Tap(p,Mid(p.CountUpRect(1)),cmd);Tap(p,Mid(p.CountUpRect(1)),cmd);
        t.Equal(2u,p.CountOf(12),"▲ twice from 0 → 2");
        Press(p,Mid(p.CountUpRect(1)),cmd);Drop(p,Mid(p.CountDownRect(1)),cmd);
        t.Equal(2u,p.CountOf(12),"▲ pressed, released on ▼ → nothing");
        Press(p,Mid(p.CountDownRect(2)),cmd);Drop(p,new(400,450),cmd);
        t.Equal(0u,p.CountOf(13),"▼ pressed, released outside the window → nothing");
        Tap(p,Mid(p.RowRect(1)),cmd);
        t.Check(p.CountOf(12)==2&&p.FocusedRow==12,"plain row click keeps the count (no whole-stack toggle)");
        p.Mouse(new UiMouseEvent(UiMouseKind.Down,Mid(p.RowRect(1)),MouseButtons.Left,2,0,Keys.None),cmd);Drop(p,Mid(p.RowRect(1)),cmd);
        t.Equal(2u,p.CountOf(12),"double click: no special action (原版沒有)");

        // 3. 點列＝選取並清緩衝；0–9 逐位輸入、夾到持有量；Backspace 刪一位。
        Tap(p,Mid(p.RowRect(2)),cmd);
        t.Check(p.FocusedRow==13&&p.InputBuffer==0&&p.CountOf(13)==0,"click 藥水 → focused, buffer 0");
        t.Check(Key(p,Keys.D1,cmd)&&p.CountOf(13)==1&&p.InputBuffer==1,"'1' → 1");
        t.Check(Key(p,Keys.NumPad5,cmd)&&p.CountOf(13)==15,"numpad '5' → 15");
        Key(p,Keys.D7,cmd);
        t.Check(p.CountOf(13)==20&&p.InputBuffer==20,"'7' → 157 capped at the held 20");
        t.Check(Key(p,Keys.Back,cmd)&&p.InputBuffer==2&&p.CountOf(13)==2,"Backspace → 2 (buffer ÷ 10, written back)");
        Key(p,Keys.Back,cmd);
        t.Check(p.InputBuffer==0&&p.CountOf(13)==0&&!p.Selection.ContainsKey(13),"Backspace again → 0, not selected");
        Key(p,Keys.D3,cmd);Tap(p,Mid(p.RowRect(2)),cmd);
        t.Check(p.InputBuffer==0&&p.CountOf(13)==3,"clicking the row clears the buffer, not the count");
        Key(p,Keys.D8,cmd);
        t.Equal(8u,p.CountOf(13),"after the click, typing starts again from 0 → 8");
        Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);Key(p,Keys.D0,cmd);
        t.Equal(20u,p.CountOf(13),"many digits never overflow, stay at the held count");

        // 4. 數字鍵盤 ±＝▲▼；Enter 吃掉不送；有修飾鍵、沒有焦點列 → 不吃。
        Tap(p,Mid(p.RowRect(0)),cmd);
        t.Check(Key(p,Keys.Add,cmd)&&p.CountOf(11)==1,"numpad + → ▲ (0 → 1)");
        Key(p,Keys.Add,cmd);t.Equal(0u,p.CountOf(11),"numpad + above held → 0");
        t.Check(Key(p,Keys.Subtract,cmd)&&p.CountOf(11)==1,"numpad − at 0 → held (1)");
        Key(p,Keys.Subtract,cmd);t.Equal(0u,p.CountOf(11),"numpad − → 0");
        t.Check(Key(p,Keys.Enter,cmd)&&cmd.Calls.Count==0,"Enter is taken and sends nothing");
        t.Check(!Key(p,Keys.Shift|Keys.D1,cmd)&&!Key(p,Keys.F1,cmd)&&p.CountOf(11)==0,"Shift+1 / F1 are not taken");
        t.Check(!p.Key(new UiKeyEvent(Keys.D1,false,false),cmd),"key up is not taken");
        var fresh=new WarehouseWindowPanel();fresh.Load(UiLayout.Parse(WarehouseWindowPanel.TestXml,"uiextra86-c.xml"),Surfaces(true).Get);
        fresh.Update(S(new UiWarehouseView("withdraw",1,rows,30)));
        t.Check(!Key(fresh,Keys.D1,cmd)&&!Key(fresh,Keys.Enter,cmd)&&fresh.Selection.Count==0,"no focused row → digits / Enter pass through to the game");

        // 5. 確定只送數量 > 0 的列（伺服器順序）；取出手續費＝種類數 × 30。
        t.Check(p.Selection.Count==2&&p.CountOf(12)==2&&p.CountOf(13)==20,"selected = rows with a count (箭 2, 藥水 20)");
        t.Equal(60L,p.SelectedFee,"withdraw fee = 2 kinds × 30 (not × count)");
        Tap(p,Mid(p.OkRect),cmd);
        t.Check(cmd.Calls.SequenceEqual(new[]{"submit:1:12x2,13x20"}),"OK → only rows with a count, server order",cmd.Calls.ToArray());
        t.Check(p.Selection.Count==0&&p.FocusedRow==null&&p.InputBuffer==0,"after OK: counts back to 0, no focus");
        Tap(p,Mid(p.OkRect),cmd);t.Equal(1,cmd.Calls.Count,"OK with every count 0 sends nothing");
        p.Update(S(new UiWarehouseView("deposit",2,rows,0)));
        Tap(p,Mid(p.CountDownRect(1)),cmd);
        t.Check(p.CountOf(12)==500&&p.SelectedFee==0,"deposit: ▼ selects all, no fee");

        // 6. 同一份清單：數量超過新持有量 → 清掉；焦點列消失 → 不再吃鍵。新清單 → 全部歸 0。
        Tap(p,Mid(p.RowRect(2)),cmd);Key(p,Keys.D9,cmd);
        p.Update(S(new UiWarehouseView("deposit",2,[rows[0],rows[1] with{Available=100}],0)));
        t.Check(p.Selection.Count==0&&p.FocusedRow==null&&!Key(p,Keys.D1,cmd),"same revision: over-count dropped, vanished focus cleared");
        Tap(p,Mid(p.CountUpRect(0)),cmd);
        p.Update(S(new UiWarehouseView("withdraw",3,rows,30)));
        t.Check(p.Selection.Count==0&&p.FocusedRow==null,"new revision → every count 0");

        // 7. 聊天輸入開著時數字給聊天列。
        var root=new UiRoot();var chat=new ChatPanel();root.Add(chat);
        var withChat=new WarehouseWindowPanel(null,root.Find);withChat.Load(UiLayout.Parse(WarehouseWindowPanel.TestXml,"uiextra86-c.xml"),Surfaces(true).Get);
        withChat.Update(S(new UiWarehouseView("withdraw",1,rows,30)));
        withChat.Focus(13);
        t.Check(Key(withChat,Keys.D4,cmd)&&withChat.CountOf(13)==4,"chat closed → digit goes to the row");
        chat.Input.Begin(true);
        t.Check(!Key(withChat,Keys.D5,cmd)&&withChat.CountOf(13)==4,"chat input open → digit not taken");

        // 8. 像素：▼▲ 圖畫在按鈕框中央；缺圖就不畫（按鈕照樣能用）。
        using(var canvas=new UiCanvas()){
            var r=new UiRoot();r.Add(p);r.Update(S(new UiWarehouseView("withdraw",3,rows,30)));r.Compose(canvas,null);
            var up=p.CountUpRect(0);var down=p.CountDownRect(0);
            t.Equal(LegacyWindowTestKit.ColourOf(3003),canvas.Bitmap.GetPixel(up.X+6,up.Y+6).ToArgb(),"▲ 3003 drawn centred in its box");
            t.Equal(LegacyWindowTestKit.ColourOf(3005),canvas.Bitmap.GetPixel(down.X+6,down.Y+6).ToArgb(),"▼ 3005 drawn centred in its box");
        }
        var bare=new WarehouseWindowPanel();bare.Load(UiLayout.Parse(WarehouseWindowPanel.TestXml,"uiextra86-c.xml"),Surfaces(false).Get);
        bare.Update(S(new UiWarehouseView("withdraw",1,rows,30)));
        using(var canvas=new UiCanvas()){
            var r=new UiRoot();r.Add(bare);r.Update(S(new UiWarehouseView("withdraw",1,rows,30)));r.Compose(canvas,null);
            var up=bare.CountUpRect(0);
            t.Check(canvas.Bitmap.GetPixel(up.X+6,up.Y+6).ToArgb()!=LegacyWindowTestKit.ColourOf(3003),"no 3003 surf → no ▲ image");
        }
        Tap(bare,Mid(bare.CountUpRect(1)),cmd);
        t.Equal(1u,bare.CountOf(12),"no image → ▲ still works");
    }
}
