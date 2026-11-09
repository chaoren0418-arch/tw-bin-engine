namespace P06.NativeUi;

// R2-J（T263／#624）：伺服器詢問的 YES／NO 文字鈕改成原生面板（docs/design/native-ui.md §5.1）。
// 已驗證（WIN2 §5.3，靜態）：InputWindow 1 號（建構子 sub_A57590／FO 0x656990）在 800×600 畫面座標
//   (361, 567) 有文字鈕「YES」、(401, 567) 有「NO」，掛在聊天輸入列上；按下等同在輸入列打 Y／N 後按 Enter（C121）。
// 已驗證（WO §6）：Esc（27）走 sub_A58380 同一條路徑，字首不是 y 所以送拒絕。
// 推定（標明）：鈕的大小 32×14（YesNoButtons.Size，來源沒有讀到）；文字鈕不用 surf，畫法（暗底、淡框、置中字）是引擎自訂。
// InputWindow 是寫死在程式裡的舊式窗，沒有 XML，所以 Load 不讀版面（同 HpMpPanel 的作法）；面板 id 也是引擎自訂。
// 提示文字仍由聊天輸入列上方的提示列顯示（YesNoPrompt.cs）；原版把文字放進輸入列 (192, 581) 280×12，本面板不畫（待驗證兩者是否同一列）。

/// <summary>面板顯示的詢問：Revision 對應 YesNoView.Revision，回覆時用來確認還是同一個詢問。</summary>
public sealed record YesNoPanelView(int Revision,string Text);

/// <summary>由 ClientForm 接上共用詢問槽（YesNoPrompt.cs）；面板不碰 LoginConnection。</summary>
public interface IYesNoSource {
    /// <summary>目前的詢問；null＝不顯示。</summary>
    YesNoPanelView? Current {get;}
    /// <summary>回覆指定的詢問（既有 AnswerYesNo 會再驗證一次）。</summary>
    void Answer(int revision,bool accept);
}

public sealed class YesNoPanel:IUiPanel {
    [UiPanel(180)] internal static UiPanelRegistration Registration=>new(YesNoPanel.PanelId,YesNoPanel.LayoutName,_=>new YesNoPanel());   // R2-J (#624): InputWindow YES／NO (Float, no XML)
    public const string PanelId="YES_NO_PROMPT";
    public const string LayoutName="uistatus86";   // 不讀 XML；只是借既有的版面檔名讓註冊表能建立
    static readonly Color Back=Color.FromArgb(40,36,28),Border=Color.FromArgb(120,110,80),Ink=Color.FromArgb(255,230,160);

    public string Id=>PanelId;
    // Float：疊在 CENTER_WINDOW（聊天）之上，Esc 也先到這裡（UiInput 依 HitOrder 由上往下送）。
    public UiLayer Layer=>UiLayer.Float;
    public Rectangle Bounds{get;}=Rectangle.Union(YesNoButtons.Logical(true),YesNoButtons.Logical(false));
    public bool Visible=>view!=null;
    public IYesNoSource? Source{get;set;}
    YesNoPanelView? view;
    public YesNoPanelView? View=>view;

    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state)=>view=state.InWorld?Source?.Current:null;

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        var g=canvas.Graphics;
        foreach(bool accept in new[]{true,false}){
            var r=YesNoButtons.Logical(accept);
            using(var fill=new SolidBrush(Back))g.FillRectangle(fill,r);
            using(var pen=new Pen(Border))g.DrawRectangle(pen,r.X,r.Y,r.Width-1,r.Height-1);
            string label=YesNoButtons.Label(accept);var size=UiText.Measure(g,label);
            canvas.DrawText(label,r.X+(r.Width-size.Width)/2,r.Y+(r.Height-size.Height)/2,Ink,Color.Black);
        }
    }

    void Answer(bool accept){if(view is {} v){view=null;Source?.Answer(v.Revision,accept);}}

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null||!Bounds.Contains(e.Location))return false;
        if(e.Kind==UiMouseKind.Down&&e.Button==MouseButtons.Left&&YesNoButtons.Hit(e.Location) is bool accept)Answer(accept);
        return true;   // 兩鈕之間的空隙也不穿透到世界（點地移動）
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down||e.Repeat||e.KeyData!=Keys.Escape)return false;
        Answer(false);return true;
    }
}
