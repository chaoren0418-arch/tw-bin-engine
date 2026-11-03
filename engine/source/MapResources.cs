using System.Text.Json;
namespace P06;
public sealed record MapBounds(int MinX,int MinY,int MaxX,int MaxY){
    public int Width=>MaxX-MinX+1;public int Height=>MaxY-MinY+1;
    public bool Contains(int x,int y)=>x>=MinX&&x<=MaxX&&y>=MinY&&y<=MaxY;
}
public sealed partial class WorldSession {
    public string MapResourceState {get;private set;}="waiting";
    public string MapResourceNotice {get;private set;}="等待地圖與角色位置。";
    public MapBounds? LoadedMapBounds=>MapCache?.Bounds;
    public bool SceneContains(int x,int y)=>MapCache is {} cache?cache.Bounds.Contains(x,y)&&cache.Contains(x,y):MapId==0&&Coordinates.Inside(x,y);
    internal string MapAssetsRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"));
    void MapWaiting(){MapResourceState="waiting";MapResourceNotice="等待地圖與角色位置；可取消連線或回離線預覽。";}
    void MapFailed(Exception ex){Clear6016(SceneClock());sceneOpacity=null;MapCache=null;MapResourceState="unavailable";MapResourceNotice=$"地圖 {MapId} 資源無法載入：{ex.Message}。可取消連線或回離線預覽。";RefreshArrivalNotice();ObserveOpacity("resource-error");}
}
