namespace P06.NativeUi;

public enum UiRoute {
    /// <summary>On the black border, or nothing to do: neither the UI nor the world receives it.</summary>
    Outside,
    /// <summary>A panel took it (hit, captured or dragged); the world must not see it.</summary>
    Panel,
    /// <summary>No panel hit: hand Logical to the existing world pointer flow (PlayerPointer).</summary>
    World,
}
public readonly record struct UiMouseResult(UiRoute Route,Point? Logical);

/// <summary>
/// Input mapping for the native UI (§3.3): client pixel → logical pixel → hit test from the float layer down → IUiPanel,
/// otherwise the world. Border input is dropped. A handled press captures the panel until release. Window drags move in
/// logical pixels and keep the whole window inside 800×600. Key focus = last panel pressed.
/// </summary>
public sealed class UiInput(UiRoot root,IUiCommands commands) {
    public UiViewport Viewport{get;set;}=UiPresenter.Compute(new(UiRoot.LogicalWidth,UiRoot.LogicalHeight));
    public IUiPanel? Captured{get;private set;}
    public IUiPanel? Focused{get;private set;}
    IUiMovablePanel? dragging;Point dragStart,dragOrigin;
    public bool Dragging=>dragging!=null;

    /// <summary>Focus loss / mode change: drop capture and any drag (the window stays where it is).</summary>
    public void Reset(){Captured=null;dragging=null;}

    public UiMouseResult Mouse(UiMouseKind kind,Point client,MouseButtons button=MouseButtons.None,int clicks=0,int delta=0,Keys modifiers=Keys.None){
        if(dragging!=null){
            if(!root.Usable(dragging)){dragging=null;return new(UiRoute.Outside,null);}
            var at=Viewport.ToLogicalClamped(client);
            if(kind is UiMouseKind.Move or UiMouseKind.Up){
                dragging.MoveTo(ClampInside(new(dragOrigin.X+at.X-dragStart.X,dragOrigin.Y+at.Y-dragStart.Y),dragging.Bounds.Size));
                if(kind==UiMouseKind.Up)dragging=null;
            }
            return new(UiRoute.Panel,at);
        }
        if(Captured!=null&&kind is UiMouseKind.Move or UiMouseKind.Up){
            var panel=Captured;var at=Viewport.ToLogicalClamped(client);
            if(kind==UiMouseKind.Up)Captured=null;
            if(root.Usable(panel))Deliver(panel,new(kind,at,button,clicks,delta,modifiers));
            return new(UiRoute.Panel,at);
        }
        if(Viewport.ToLogical(client) is not Point logical){
            if(kind==UiMouseKind.Down)Captured=null;
            return new(UiRoute.Outside,null);
        }
        var hit=root.HitTest(logical);
        if(hit==null){
            if(kind==UiMouseKind.Down){Focused=null;Captured=null;}
            return new(UiRoute.World,logical);
        }
        if(kind==UiMouseKind.Down){
            root.BringToFront(hit);Focused=hit;
            if(button==MouseButtons.Left&&hit is IUiMovablePanel movable&&movable.IsDragHandle(logical)){
                dragging=movable;dragStart=logical;dragOrigin=movable.Bounds.Location;return new(UiRoute.Panel,logical);
            }
            if(Deliver(hit,new(kind,logical,button,clicks,delta,modifiers)))Captured=hit;
            return new(UiRoute.Panel,logical);
        }
        Deliver(hit,new(kind,logical,button,clicks,delta,modifiers));
        return new(UiRoute.Panel,logical);
    }

    /// <summary>
    /// Focused panel first, then every visible panel from the top, then windows pushed out of the stack (R3-I): those are
    /// hidden but still own their toggle key (e.g. Ctrl+M), which is how they close and can open again on top.
    /// false = not for the UI (game keys continue).
    /// </summary>
    public bool Key(UiKeyEvent e){
        root.SyncWindows();
        if(Focused!=null&&!root.Usable(Focused))Focused=null;
        if(Focused!=null&&DeliverKey(Focused,e))return true;
        var order=root.HitOrder().ToArray();
        foreach(var p in order)if(p!=Focused&&root.Usable(p)&&DeliverKey(p,e))return true;
        foreach(var p in order)if(root.IsEvicted(p)&&p.Visible&&!root.Faulted.Contains(p)&&DeliverKey(p,e))return true;
        return false;
    }

    /// <summary>Top-left that keeps a window of this size entirely inside 800×600 (a window larger than the canvas pins to 0).</summary>
    public static Point ClampInside(Point location,Size size)=>new(
        Math.Clamp(location.X,0,Math.Max(0,UiRoot.LogicalWidth-size.Width)),
        Math.Clamp(location.Y,0,Math.Max(0,UiRoot.LogicalHeight-size.Height)));

    bool Deliver(IUiPanel p,UiMouseEvent e){try{return p.Mouse(e,commands);}catch(Exception ex){root.Fault(p,"Mouse",ex);if(Captured==p)Captured=null;return false;}}
    bool DeliverKey(IUiPanel p,UiKeyEvent e){try{return p.Key(e,commands);}catch(Exception ex){root.Fault(p,"Key",ex);return false;}}
}
