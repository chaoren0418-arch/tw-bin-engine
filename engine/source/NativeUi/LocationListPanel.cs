namespace P06.NativeUi;

// R2-M（T258／#619）：發送座標的 10 筆清單畫面（docs/reference/world-objects.md §5、gap-matrix 11.5 R2-M）。
// 已驗證（WO §5）：sub_8D43B0 把收到的分享（地圖、x、y、寄件者）記到獨立清單，最多 10 筆，滿了先刪最舊的。
// 待驗證（WO §5「10 筆清單在哪個 UI 顯示、能否點擊」）：原版的清單視窗、底圖、位置與點擊行為都還沒查到，
// 所以本面板是引擎自己的過渡畫面，不冒充原版：
// - 不畫任何底圖或色塊（規則 5，native-ui.md §2.3），只畫文字列（白字＋黑色 1 px 陰影，UiText 的共用字型）；
// - 只在 Field_Map 視窗開著、且清單不是空的時候顯示，貼在 Field_Map 右側（位置是推測，查到原版 UI 後只改 Anchor）；
// - 每列顯示「寄件者：地區」（與聊天行相同的地區占位，字串 1784／mapDesc 等 Q-2）；點一列只在場景放 ⊕ 標記，
//   不移動角色、不送封包（與聊天連結相同，LocationShareUi 負責標記）。
// R5-H（T313／#740）：地圖窗的發送入口 TransferMenu（MapSendEntry，見下方）。本面板在執行期把入口裝到 Field_Map，
// 送出中（聊天輸入列是「/loc 」開頭）時在清單最上方多一列提示 1785（不可點）。

/// <summary>清單的一列：Sequence 是該分享在聊天中的序號（同一 session 內唯一）。</summary>
public sealed record LocationListRow(long Sequence,string Sender,string Region,ushort Map);
/// <summary>面板讀的狀態（由 LocationShare.cs 的轉接提供）。Revision 每次清單內容變更或重置都會增加。</summary>
public sealed record LocationListView(long Revision,IReadOnlyList<LocationListRow> Rows,long? Selected) {
    public static readonly LocationListView Empty=new(-1,[],null);
}
public interface ILocationListHost {
    LocationListView View{get;}
    /// <summary>選取 shown 中的一列（只放場景標記）；shown 已過期或該列不存在時由宿主拒絕。</summary>
    void Select(LocationListView shown,long sequence);
}

/// <summary>
/// R5-H（T313／#740）地圖窗的 TransferMenu 發送入口（WO §5）。
/// 已驗證（WO §5）：原版地圖視窗事件表 sub_8DE770 註冊 TransferMenu；點擊 → sub_8E0620 在聊天輸入列開提示 1785，
/// 輸入角色名、按 Enter 後由 sub_8D4A90 送 C_CHANNEL 55/11（＝3.81 C254/11 `S H H H D`）。
/// 引擎做法：打開原生聊天輸入列並填入「/loc 」，之後輸入名稱、Enter 走既有 ChatPanel → SendPlayerChat → SendLocation
/// （同一套 session／世界／角色戳記、冷卻與 sender gate，不另寫送包）；ESC 照聊天列規則取消。
/// 推測：原版提示是輸入列本身的提示狀態，引擎改用「/loc 」前綴＋清單面板上的 1785 提示列；原本的草稿會被取代（原版開提示時同樣換掉輸入列）。
/// 原生 UI 關閉（WinForms HUD）時的入口仍是聊天指令 /loc。
/// </summary>
public sealed class MapSendEntry {
    public const string Command="/loc ";
    /// <summary>可以發送的聊天狀態（UiChatSnapshot.View.CanSend），由面板每幀更新。</summary>
    internal bool CanSend{get;set;}
    internal ChatPanel? Chat{get;set;}
    /// <summary>TransferMenu：true＝聊天輸入列已打開並填入「/loc 」。不送任何封包。</summary>
    public bool Begin(IUiCommands commands){
        if(Chat is not {} chat||!CanSend||chat.Input.Pending||UiChatGameState.Commands(commands) is not {} c)return false;
        if(!chat.Input.Open){
            if(!c.BeginChatInput())return false;
            chat.Input.Begin(false);
        }
        chat.Input.Clear();
        foreach(char ch in Command)chat.Input.Type(ch);
        return chat.Input.Text==Command;
    }
    /// <summary>聊天輸入列打開且以「/loc 」開頭＝正在輸入收件角色名。</summary>
    public bool Prompting=>Chat is {Input.Open:true} chat&&chat.Input.Text.StartsWith(Command,StringComparison.Ordinal);
}

public sealed class LocationListPanel(Func<Rectangle> anchor,Func<string,IUiPanel?>? find=null):IUiPanel {
    /// <summary>引擎自訂的面板 Id（原版視窗名稱待查證，不借用 MapLocationSaveWindow 等原版名稱）。</summary>
    public const string PanelId="R2M_LocationShareList";
    public const int RowHeight=16,Width=200,PromptWidth=420,Gap=2,Padding=2;
    // T270（#645）自動發現：註冊宣告在面板旁，不改 UiPanelRegistry.cs。
    [UiPanel(170)] internal static UiPanelRegistration Registration=>new(PanelId,"uistatus86",root=>new LocationListPanel(()=>root.Find(FieldMapPanel.PanelId)?.Bounds??Rectangle.Empty,root.Find));   // R2-M (#619): location-share list, text only beside Field_Map (original window unverified); R5-H (#740): TransferMenu entry
    public static readonly Color TextColor=Color.White,SelectedColor=Color.Cyan,ShadowColor=Color.Black,PromptColor=Color.Yellow;

    LocationListView shown=LocationListView.Empty;bool inWorld,prompting;long? pressed;
    readonly MapSendEntry? entry=find==null?null:new MapSendEntry();
    public ILocationListHost? Host{get;private set;}
    public void Attach(ILocationListHost host)=>Host=host;
    internal LocationListView Shown=>shown;
    /// <summary>TransferMenu 入口（沒有 find 的離線建構＝null，不裝入口）。</summary>
    public MapSendEntry? Entry=>entry;
    /// <summary>目前是否顯示 1785 提示列。</summary>
    public bool Prompting=>prompting;
    int Lead=>prompting?1:0;
    IReadOnlyList<LocationListRow> Rows=>Host!=null?shown.Rows:[];

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds{get{
        var a=anchor();int count=Rows.Count+Lead;
        if(a.IsEmpty||count==0)return Rectangle.Empty;
        int width=prompting?PromptWidth:Width;
        int x=Math.Max(0,Math.Min(a.Right+Gap,UiRoot.LogicalWidth-width)),height=count*RowHeight+Padding*2;
        int y=Math.Max(0,Math.Min(a.Top,UiRoot.LogicalHeight-height));
        return new(x,y,width,height);
    }}
    public bool Visible=>inWorld&&((Host!=null&&shown.Rows.Count>0)||prompting);

    /// <summary>不讀版面 XML、不用任何 surf（原版畫面待查證）。</summary>
    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        var now=inWorld?Host?.View??LocationListView.Empty:LocationListView.Empty;
        if(now.Revision!=shown.Revision)pressed=null;
        shown=now;
        if(entry!=null){
            // R5-H：入口裝到 Field_Map（同一個 UiRoot），聊天面板與可否發送每幀更新。
            if(find!(FieldMapPanel.PanelId) is FieldMapPanel field&&field.TransferHandler==null)field.TransferHandler=entry.Begin;
            entry.Chat=find!(ChatPanel.WindowName) as ChatPanel;
            entry.CanSend=inWorld&&UiChatGameState.From(state).View is {CanSend:true};
        }
        prompting=inWorld&&entry is {Prompting:true};
    }
    public static string RowText(LocationListRow row)=>row.Sender+"："+row.Region;
    /// <summary>1785 提示列的文字（字串表沒載入時是編號占位）。</summary>
    public static string PromptRowText=>LocationShareProtocol.PromptText;
    /// <summary>邏輯座標 → 列（null＝不在任何列上；提示列不是清單列）。</summary>
    internal LocationListRow? RowAt(Point logical){
        var b=Bounds;if(!b.Contains(logical))return null;
        int index=(logical.Y-b.Top-Padding)/RowHeight-Lead;
        return logical.Y-b.Top>=Padding&&index>=0&&index<Rows.Count?Rows[index]:null;
    }
    internal Rectangle RowRect(int index){var b=Bounds;return new(b.X,b.Y+Padding+(index+Lead)*RowHeight,b.Width,RowHeight);}

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        if(prompting){var b=Bounds;canvas.DrawText(PromptRowText,b.X+Padding,b.Y+Padding+1,PromptColor,ShadowColor);}
        var rows=Rows;
        for(int i=0;i<rows.Count;i++){
            var row=rows[i];var r=RowRect(i);
            canvas.DrawText(RowText(row),r.X+Padding,r.Y+1,row.Sequence==shown.Selected?SelectedColor:TextColor,ShadowColor);
        }
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Down){pressed=RowAt(e.Location)?.Sequence;return Bounds.Contains(e.Location);}
        if(e.Kind==UiMouseKind.Up){
            long? was=pressed;pressed=null;
            if(RowAt(e.Location) is {} row&&row.Sequence==was)Host?.Select(shown,row.Sequence);
            return true;
        }
        return Bounds.Contains(e.Location);
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
