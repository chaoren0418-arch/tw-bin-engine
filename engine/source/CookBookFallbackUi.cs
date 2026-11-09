using P06.NativeUi;
namespace P06;

// R4-D（T295／#707）：原生 UI 關閉時的料理窗暫行清單（WinForms）。gap-matrix 第 13.5 節：新面板在原生 UI 關閉時也要有可用的入口。
// 收到 S250 52（Cooking.cs）→ 列出這一級料理書的食譜（recipe.tbl，名稱經道具名稱表代換）與材料；選一道按「製作」
// → LoginConnection.CookRecipe（C_USE_ITEM D C(1) C(編號)）。關閉＝本機關窗（不送封包），與原生窗關閉 X 相同。
// 由 Cooking.cs 的 AttachNativeCookBook（共用 timer tick，UI 執行緒）每 tick 呼叫 SyncCookBookFallback。
// 清單內容與篩選規則與原生窗相同（Cooking.cs 檔頭的推測：等級 L 列編號 < 8×L）；本窗是引擎的暫行畫面，不是原版外觀。

/// <summary>暫行清單的顯示文字（離線測試可直接驗）。</summary>
public static class CookBookFallbackView {
    public const string Title="料理書";
    public static readonly string[] Columns=["編號","料理","材料"];
    public const string Footnote="暫行清單（原生介面關閉時使用）：選一道料理按「製作」，會對料理書送出製作請求；材料與成敗以伺服器回報為準。";
    public const string EmptyText="讀不到原客戶端的食譜表（recipe.tbl）；仍可從背包的料理書輸入食譜編號製作。";
    public static string Caption(CookBookState book,int count)=>$"{Title}（等級 {book.Level}，{count} 道）";
    public static string[] Cells(CookBookRecipe r)=>[r.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),r.Name,r.Materials];
    /// <summary>原生 UI 關閉、在遊戲中、料理窗開著、且使用者沒有關掉這一次的窗時才顯示。</summary>
    public static bool Wanted(bool nativeUi,bool inWorld,CookBookState book,long dismissedRevision)=>
        !nativeUi&&inWorld&&book.Open&&book.Revision!=dismissedRevision;
}

public sealed partial class ClientForm {
    CookBookFallbackWindow? cookBookFallback;

    void SyncCookBookFallback(){
        var book=login.World.CookBook;
        var window=cookBookFallback is {IsDisposed:false} w?w:null;
        if(!CookBookFallbackView.Wanted(NativeUiActive,login.State==LoginState.InWorld,book,window?.DismissedRevision??-1)){
            if(window is {Visible:true})window.Hide();
            return;
        }
        if(window==null){
            cookBookFallback=window=new CookBookFallbackWindow();
            window.Cook=async(revision,recipe)=>{
                await login.CookRecipe(revision,recipe);
                window.ShowNotice(login.World.ItemNotice);
            };
            window.Dismissed=revision=>login.World.CloseCookBook(revision);
            FormClosed+=(_,_)=>cookBookFallback?.Dispose();
        }
        if(window.Revision!=book.Revision)window.SetBook(book,CookRecipeTable.Rows(book.Level,ResolveNpcText),CookRecipeTable.Table!=null);
        if(!window.Visible)window.Show(this);
    }
}

/// <summary>料理書暫行清單窗：不搶遊戲焦點（ShowWithoutActivation）；按 X 只隱藏並關掉這一次的料理窗。</summary>
public sealed class CookBookFallbackWindow:Form {
    static readonly Color Back=Color.FromArgb(23,38,41);
    readonly ListView list=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,GridLines=true,HideSelection=false,
        HeaderStyle=ColumnHeaderStyle.Nonclickable,BackColor=Back,ForeColor=Color.White};
    readonly Label footnote=new(){Dock=DockStyle.Bottom,Height=48,Text=CookBookFallbackView.Footnote,ForeColor=Color.Silver,Padding=new(6,4,6,4)};
    readonly Label notice=new(){Dock=DockStyle.Bottom,Height=24,ForeColor=Color.Khaki,Padding=new(6,4,6,0)};
    readonly Button make=new(){Text="製作",AutoSize=true,Enabled=false};
    readonly Button close=new(){Text="關閉",AutoSize=true};

    /// <summary>目前顯示的料理窗 revision（-1＝還沒有）。</summary>
    public long Revision{get;private set;}=-1;
    /// <summary>使用者關掉的 revision；同一次的窗不再自動跳出。</summary>
    public long DismissedRevision{get;private set;}=-1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<long,int,Task>? Cook{get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<long>? Dismissed{get;set;}

    public CookBookFallbackWindow(){
        Text=CookBookFallbackView.Title;
        FormBorderStyle=FormBorderStyle.SizableToolWindow;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        ClientSize=new(460,340);BackColor=Back;ForeColor=Color.White;
        int[] widths=[50,150,240];
        for(int i=0;i<CookBookFallbackView.Columns.Length;i++)list.Columns.Add(CookBookFallbackView.Columns[i],widths[i]);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=36,FlowDirection=FlowDirection.RightToLeft,Padding=new(4)};
        buttons.Controls.AddRange([close,make]);
        Controls.Add(list);Controls.Add(notice);Controls.Add(buttons);Controls.Add(footnote);
        list.SelectedIndexChanged+=(_,_)=>make.Enabled=SelectedRecipe!=null;
        list.DoubleClick+=async(_,_)=>await MakeSelected();
        make.Click+=async(_,_)=>await MakeSelected();
        close.Click+=(_,_)=>Dismiss();
    }
    protected override bool ShowWithoutActivation=>true;

    /// <summary>標題列 X：不釋放視窗，只隱藏並關掉這一次的料理窗（主視窗關閉時才真的關）。</summary>
    protected override void OnFormClosing(FormClosingEventArgs e){
        if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Dismiss();return;}
        base.OnFormClosing(e);
    }

    void Dismiss(){
        if(Revision>=0){DismissedRevision=Revision;Dismissed?.Invoke(Revision);}
        Hide();
    }

    async Task MakeSelected(){
        if(SelectedRecipe is not {} r||Cook==null||Revision<0)return;
        make.Enabled=false;
        try{await Cook(Revision,r.Index);}
        finally{if(!IsDisposed)make.Enabled=SelectedRecipe!=null;}
    }

    // 直接看各列的 Selected（視窗 handle 還沒建立時也正確）。
    internal CookBookRecipe? SelectedRecipe=>list.Items.Cast<ListViewItem>().FirstOrDefault(i=>i.Selected)?.Tag as CookBookRecipe;

    /// <summary>以這一次開窗的內容取代清單（新的 S250 52 或換等級都整份重列）。</summary>
    public void SetBook(CookBookState book,IReadOnlyList<CookBookRecipe> rows,bool tableLoaded){
        Revision=book.Revision;
        list.BeginUpdate();
        try{
            list.Items.Clear();
            foreach(var r in rows)list.Items.Add(new ListViewItem(CookBookFallbackView.Cells(r)){Tag=r});
        }finally{list.EndUpdate();}
        Text=CookBookFallbackView.Caption(book,rows.Count);
        notice.Text=tableLoaded?"":CookBookFallbackView.EmptyText;
        make.Enabled=false;
    }
    public void ShowNotice(string text){if(!IsDisposed)notice.Text=text;}
    internal int RowCount=>list.Items.Count;
    internal string Cell(int row,int column)=>list.Items[row].SubItems[column].Text;
    internal void SelectRow(int row){list.Items[row].Selected=true;}
}
