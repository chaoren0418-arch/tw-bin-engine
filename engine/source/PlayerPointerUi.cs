namespace P06;
public sealed partial class ClientForm {
    PlayerPointerController? playerPointer;
    // Sent means admitted by the existing sender, never server-confirmed success. HUD owns response presentation.
    public event Action<string,uint>? PlayerPointerActionSubmitted;
    public string PlayerPointerNotice=>playerPointer?.Notice??PointerFlow.Help+"Esc／空白停止。";
    PlayerPointerController Pointer=>playerPointer??=new(login,()=>GameInputAllowed,()=>World.Walking,HasVisibleObjectPixel);
    public static bool PlayerPointerInScene(Point point,RectangleF viewport,bool covered,bool extraAllowed)=>!covered&&extraAllowed&&viewport.Contains(point);
    bool PlayerPointerSceneHit(Point point)=>PlayerPointerInScene(point,PlayerSceneBounds,GetChildAtPoint(point,GetChildAtPointSkip.Invisible)!=null,PlayerSceneAccepts(point));
    void StopPlayerPointer(string reason)=>playerPointer?.Cancel(reason);
    void PlayerPointerTick(){if(playerPointer!=null)_=PumpPlayerPointer();}
    async Task PumpPlayerPointer(){try{await Pointer.TickAsync();}catch(Exception ex){StopPlayerPointer("路徑無法繼續");events.Add(new{kind="pointer-error",error=ex.Message});}}
    uint? PointerPixel(Point point){var p=Camera.ToScene(point);if(!login.World.Renderable||objectPatch is not {} patch)return null;int x=(int)Math.Floor(p.X)-patch.Bounds.X,y=(int)Math.Floor(p.Y)-patch.Bounds.Y;return (uint)x<(uint)patch.Bounds.Width&&(uint)y<(uint)patch.Bounds.Height?patch.EntityOwners?[y*patch.Bounds.Width+x]:null;}
    async Task HandlePlayerPointer(MouseEventArgs e){
        if(!GameInputAllowed||!PlayerPointerSceneHit(e.Location)){StopPlayerPointer("場景外輸入");return;}
        try{
            var id=PointerPixel(e.Location);PickObject(e.Location);
            if(id is uint target){
                // R2-C: double-click on a ground item = object click (WO §1–2): approach, then whole stack or quantity prompt.
                if(login.World.Ground.Any(g=>g.ObjectId==target)){StopPlayerPointer("選取地上物品");if(e.Button==MouseButtons.Left&&e.Clicks>=2&&await Pointer.ClickGroundAsync(target,PickupGroundClicked))PlayerPointerActionSubmitted?.Invoke("pickup",target);SyncGround();lastWebState="";PublishWebState();Invalidate();return;}
                if(e.Button==MouseButtons.Right){
                    if(login.World.Target?.ShopActive==true){StopPlayerPointer("個人商店");if(Pointer.Refusal(target,true) is {} refused)ShowPlayerPointerRefusal(refused);else if(await RunInteraction(CurrentNpcCommand("talk")))PlayerPointerActionSubmitted?.Invoke("private-shop",target);}
                    else if(await Pointer.ActAsync(target,true))PlayerPointerActionSubmitted?.Invoke("talk",target);
                }else if(e.Clicks>=2||(ModifierKeys&Keys.Control)!=0||DirectAttackTarget(target)){if(await Pointer.ActAsync(target,false))PlayerPointerActionSubmitted?.Invoke("attack",target);}
                else Pointer.Select(target);
            }else if(e.Button==MouseButtons.Left){var scene=Camera.ToScene(e.Location);Pointer.MoveTo(Coordinates.Pick(scene.X,scene.Y));await Pointer.TickAsync();}
            else StopPlayerPointer("右鍵取消");
            lastWebState="";PublishWebState();Invalidate();
        }catch(Exception ex){StopPlayerPointer("輸入未完成");events.Add(new{kind="pointer-error",error=ex.Message});}
    }
}
