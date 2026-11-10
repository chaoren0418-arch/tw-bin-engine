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
public sealed class UiInput {
    readonly UiRoot root;readonly IUiCommands commands;
    public UiInput(UiRoot root,IUiCommands commands){
        this.root=root;this.commands=commands;
        // R7-H (#810): a window pushed out of (or refused by) the stack is hidden; it must not keep focus, capture or a drag.
        root.WindowEvicted+=Drop;root.WindowRefused+=Drop;
    }
    void Drop(IUiPanel p){
        if(Focused==p)Focused=null;
        if(Captured==p)Captured=null;
        if(ReferenceEquals(dragging,p))dragging=null;
    }
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
    /// R7-H (#810): when no panel takes ESC, the global ESC closes the stacked windows (<see cref="GlobalEscape"/>).
    /// false = not for the UI (game keys continue).
    /// </summary>
    public bool Key(UiKeyEvent e){
        root.SyncWindows();
        if(Focused!=null&&!root.Usable(Focused))Focused=null;
        if(Focused!=null&&DeliverKey(Focused,e))return true;
        var order=root.HitOrder().ToArray();
        foreach(var p in order)if(p!=Focused&&root.Usable(p)&&DeliverKey(p,e))return true;
        foreach(var p in order)if(root.IsEvicted(p)&&p.Visible&&!root.Faulted.Contains(p)&&DeliverKey(p,e))return true;
        return GlobalEscape(e);
    }

    /// <summary>
    /// R7-H (T347, #810), FU4§1.1 (已驗證): ESC (WM_CHAR 27) reaches the global dispatch sub_8C14F0 only after the window
    /// stack, the chat line and the other key owners of sub_8CA740 passed on it, and there it calls sub_A57C60(0): every
    /// window in the stack (at most 4) closes at once. Engine: a plain ESC press (no modifier, not a repeat) that no panel took
    /// closes the stack through <see cref="UiRoot.CloseAll"/>. true only when a window actually closed — with nothing open,
    /// ESC still goes to the game (YES/NO refusal, ClearInput …). The font window FU4 also lists has no engine counterpart.
    /// </summary>
    public bool GlobalEscape(UiKeyEvent e){
        if(e.KeyData!=Keys.Escape||!e.Down||e.Repeat)return false;
        var closed=root.CloseAll();
        if(closed.Count==0)return false;
        foreach(var p in closed)Drop(p);
        commands.ReturnGameFocus();
        return true;
    }

    /// <summary>Top-left that keeps a window of this size entirely inside 800×600 (a window larger than the canvas pins to 0).</summary>
    public static Point ClampInside(Point location,Size size)=>new(
        Math.Clamp(location.X,0,Math.Max(0,UiRoot.LogicalWidth-size.Width)),
        Math.Clamp(location.Y,0,Math.Max(0,UiRoot.LogicalHeight-size.Height)));

    bool Deliver(IUiPanel p,UiMouseEvent e){try{return p.Mouse(e,commands);}catch(Exception ex){root.Fault(p,"Mouse",ex);if(Captured==p)Captured=null;return false;}}
    bool DeliverKey(IUiPanel p,UiKeyEvent e){try{return p.Key(e,commands);}catch(Exception ex){root.Fault(p,"Key",ex);return false;}}
}
