namespace P06.NativeUi;

// R2-M（T258／#619）：發送座標的 10 筆清單畫面（docs/reference/world-objects.md §5、gap-matrix 11.5 R2-M）。
// 已驗證（WO §5）：sub_8D43B0 把收到的分享（地圖、x、y、寄件者）記到獨立清單，最多 10 筆，滿了先刪最舊的。
// 待驗證（WO §5「10 筆清單在哪個 UI 顯示、能否點擊」）：原版的清單視窗、底圖、位置與點擊行為都還沒查到，
// 所以本面板是引擎自己的過渡畫面，不冒充原版：
// - 不畫任何底圖或色塊（規則 5，native-ui.md §2.3），只畫文字列（白字＋黑色 1 px 陰影，UiText 的共用字型）；
// - 只在 Field_Map 視窗開著、且清單不是空的時候顯示，貼在 Field_Map 右側（位置是推測，查到原版 UI 後只改 Anchor）；
// - 每列顯示「寄件者：地區」（與聊天行相同的地區占位，字串 1784／mapDesc 等 Q-2）；點一列只在場景放 ⊕ 標記，
//   不移動角色、不送封包（與聊天連結相同，LocationShareUi 負責標記）。

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

public sealed class LocationListPanel(Func<Rectangle> anchor):IUiPanel {
    /// <summary>引擎自訂的面板 Id（原版視窗名稱待查證，不借用 MapLocationSaveWindow 等原版名稱）。</summary>
    public const string PanelId="R2M_LocationShareList";
    public const int RowHeight=16,Width=200,Gap=2,Padding=2;
    // T270（#645）自動發現：註冊宣告在面板旁，不改 UiPanelRegistry.cs。
    [UiPanel(170)] internal static UiPanelRegistration Registration=>new(PanelId,"uistatus86",root=>new LocationListPanel(()=>root.Find(FieldMapPanel.PanelId)?.Bounds??Rectangle.Empty));   // R2-M (#619): location-share list, text only beside Field_Map (original window unverified)
    public static readonly Color TextColor=Color.White,SelectedColor=Color.Cyan,ShadowColor=Color.Black;

    LocationListView shown=LocationListView.Empty;bool inWorld;long? pressed;
    public ILocationListHost? Host{get;private set;}
    public void Attach(ILocationListHost host)=>Host=host;
    internal LocationListView Shown=>shown;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds{get{
        var a=anchor();
        if(a.IsEmpty||shown.Rows.Count==0)return Rectangle.Empty;
        int x=Math.Min(a.Right+Gap,UiRoot.LogicalWidth-Width),height=shown.Rows.Count*RowHeight+Padding*2;
        int y=Math.Max(0,Math.Min(a.Top,UiRoot.LogicalHeight-height));
        return new(x,y,Width,height);
    }}
    public bool Visible=>inWorld&&Host!=null&&shown.Rows.Count>0;

    /// <summary>不讀版面 XML、不用任何 surf（原版畫面待查證）。</summary>
    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        var now=inWorld?Host?.View??LocationListView.Empty:LocationListView.Empty;
        if(now.Revision!=shown.Revision)pressed=null;
        shown=now;
    }
    public static string RowText(LocationListRow row)=>row.Sender+"："+row.Region;
    /// <summary>邏輯座標 → 列（null＝不在任何列上）。</summary>
    internal LocationListRow? RowAt(Point logical){
        var b=Bounds;if(!b.Contains(logical))return null;
        int index=(logical.Y-b.Top-Padding)/RowHeight;
        return logical.Y-b.Top>=Padding&&index>=0&&index<shown.Rows.Count?shown.Rows[index]:null;
    }
    internal Rectangle RowRect(int index){var b=Bounds;return new(b.X,b.Y+Padding+index*RowHeight,b.Width,RowHeight);}

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        for(int i=0;i<shown.Rows.Count;i++){
            var row=shown.Rows[i];var r=RowRect(i);
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
