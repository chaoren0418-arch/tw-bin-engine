namespace P06.NativeUi;

// ui-bookmark（#472）：原生書籤窗讀的狀態。資料全部來自 T124（#258）的 Bookmarks／BookmarkView，不新增封包解析；
// 伺服器沒送的欄位（區域名稱、顏色、快速移動清單）一律不補。

/// <summary>One memory-place row (S64／S92 values verbatim). DeletePending = C3 sent, waiting for a fresh S64 list (T124).</summary>
public sealed record BookmarkRow(uint Id,string Name,ushort MapId,ushort X,ushort Y,bool DeletePending);

/// <summary>
/// Snapshot shown by the native bookmark window. View is the T124 snapshot itself, handed back unchanged to the host so that
/// delete goes through LoginConnection.SendBookmark's own staleness checks (session／epoch／revision／actor).
/// </summary>
public sealed record BookmarkUiState(P06.BookmarkView? View,bool CanEdit,int Capacity,string Notice,IReadOnlyList<BookmarkRow> Rows) {
    public static readonly BookmarkUiState Empty=new(null,false,0,"",[]);
    public static BookmarkUiState From(P06.BookmarkView view)=>new(view,view.CanEdit,view.Capacity,view.Notice,
        view.Items.Select(b=>new BookmarkRow(b.Id,b.Name,b.MapId,b.X,b.Y,b.DeletePending)).ToArray());
    /// <summary>BookmarkCount_Label text, format taken from its XML caption "30/60" (count/capacity).</summary>
    public string CountText=>View==null?"":$"{Rows.Count}/{Capacity}";
}

/// <summary>What the window needs from the client form (BookmarkUi.cs). UI modules never touch LoginConnection.</summary>
public interface IBookmarkUiHost {
    BookmarkUiState State{get;}
    /// <summary>Delete one bookmark through the existing T124 path; the host rejects stale or disallowed requests.</summary>
    void Delete(BookmarkUiState shown,uint id);
}

/// <summary>Zone filter and sort order of the memory-place list. Pure functions, local view only (no packet).</summary>
public static class BookmarkListView {
    /// <summary>Filter options: null = "ALL" (Item0 caption in BookmarkUI.xml), then every distinct map id in the list.</summary>
    public static IReadOnlyList<ushort?> FilterOptions(IReadOnlyList<BookmarkRow> rows)=>
        [null,..rows.Select(r=>r.MapId).Distinct().Order().Select(m=>(ushort?)m)];
    /// <summary>Zone names are not available (no verified map-name table) → the map id number is shown.</summary>
    public static string Caption(ushort? map)=>map is ushort m?m.ToString(System.Globalization.CultureInfo.InvariantCulture):"ALL";
    /// <summary>Server order by default; byName = ordinal name order (inferred meaning of Sorting_Button), id breaks ties.</summary>
    public static IReadOnlyList<BookmarkRow> Apply(IReadOnlyList<BookmarkRow> rows,ushort? map,bool byName){
        IEnumerable<BookmarkRow> q=map is ushort m?rows.Where(r=>r.MapId==m):rows;
        if(byName)q=q.OrderBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.Id);
        return q.ToArray();
    }
}
