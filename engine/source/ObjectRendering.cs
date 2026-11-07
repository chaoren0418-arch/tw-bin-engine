namespace P06;
public sealed record ObjectVisual(uint ObjectId,int Gfx,int Sprite,string Name,int X,int Y,string Action,string Frame,PointF Foot,Rectangle Bounds,bool Walking,int StepMs);
public sealed partial class Renderer {
    public ObjectVisual? OwnCombatVisual {get;private set;}
    public Patch ComposeObjects(World own,WorldSession session,ObjectAssets catalog,out ObjectVisual[] visuals,out WorldObject[] missing,long? testTime=null,bool historicalUntriggeredReplay=false){
        long now=testTime??Environment.TickCount64;session.AdvanceScene();BindCache(historicalUntriggeredReplay?null:session.MapCache);var opacity=historicalUntriggeredReplay?null:session.Opacity;SceneBackground(opacity);PrepareShipBackdrop(session,now);
        ObjectAppearance? ownAppearance=session.Actor is {} ownActor?catalog.Get(ownActor.Gfx,ownActor.Weapon)??throw new NotSupportedException("Own appearance profile unavailable"):null;
        own.SetOnlineAppearance(ownAppearance);var draws=Actor(own);OwnCombatVisual=session.Actor is {} shown&&ownAppearance!=null?new(shown.ObjectId,shown.Gfx,ownAppearance.Sprite,shown.Name,own.X,own.Y,own.Walking?"walk":"breath",own.Frame.Key,own.DisplayPosition,Rectangle.Empty,own.Walking,own.Walking?own.NetworkStepMs:session.MovementAnimationMs??ownAppearance.StepMs):null;
        ComposeMapEffects(session,draws,now);ComposeProjectile(session,draws,now);ComposeMagic(session,draws,now);
        BindPickupPresentation(own,session,catalog);
        if(session.Actor is {} actor&&ownAppearance is {} appearance){
            var self=ActiveOwnCombat(session,appearance,now);
            if(self!=null){
                string name=OwnCombatName(self.Action);double elapsed=now-self.At;
                var frame=self.Timing?.Frame(appearance,actor.Direction,elapsed)??appearance.At(name,actor.Direction,elapsed);draws.RemoveAll(d=>(d.Layer is "actor" or "shadow")&&d.ObjectId==0);draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(actor.X-Coordinates.MinX)+b.A,actor.Y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile)));draws.AddRange(ShadowDraws(appearance,frame,actor.X,actor.Y));
                OwnCombatVisual=new(actor.ObjectId,actor.Gfx,appearance.Sprite,actor.Name,actor.X,actor.Y,name,frame.Key,Coordinates.Screen(actor.X,actor.Y),Rectangle.Empty,false,self.Timing?.AnimationMs??(int)appearance.Duration(name));
            }
            if(session.PickupFrame(now) is {} pickupFrame){
                draws.RemoveAll(d=>(d.Layer is "actor" or "shadow")&&d.ObjectId==0);
                draws.AddRange(pickupFrame.Blocks.Select(b=>new Draw(2*(actor.X-Coordinates.MinX)+b.A,actor.Y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile)));
                draws.AddRange(ShadowDraws(appearance,pickupFrame,actor.X,actor.Y));
                OwnCombatVisual=new(actor.ObjectId,actor.Gfx,appearance.Sprite,actor.Name,actor.X,actor.Y,"pickup",pickupFrame.Key,Coordinates.Screen(actor.X,actor.Y),Rectangle.Empty,false,0);
            }
        }
        var vs=new List<ObjectVisual>();var unavailable=new List<WorldObject>();
        ComposeGround(session,catalog,draws,vs);
        foreach(var o in session.Objects){
            bool door=o.Gfx==2346,shop=!door&&o.ShopActive&&!o.Dead;var a=door?catalog.GetDoor(o.Gfx):shop?catalog.GetShop(o.Gfx):catalog.Get(o.Gfx,o.Action);
            if(a==null||o.AppearancePending||(!shop&&(!NormalSpeed.Supported(o.Speed,o.Brave)||o.MotionAt!=0&&!NormalSpeed.Supported(o.MotionSpeed,o.MotionBrave)))||(door?(o.DoorAction is not (28 or 29)||o.Dead||o.Combat!=null):o.ReceivedAction is not (null or 0 or 1 or 2 or 3 or 8 or 18 or 19 or 70))||!(session.MapCache?.Contains(o.X,o.Y)??Coordinates.Inside(o.X,o.Y))){unavailable.Add(o);continue;}
            int stepMs=shop?a.StepMs:NormalSpeed.AnimationMs(a.StepMs,o.MotionAt!=0?o.MotionSpeed:o.Speed,o.MotionAt!=0?o.MotionBrave:o.Brave);double elapsed=now-o.MotionAt;bool moving=!door&&!shop&&o.MotionAt!=0&&elapsed<stepMs;
            string action=door?(o.DoorAction==28?"open":"close"):shop?"shop":moving?"walk":"breath";double animationElapsed=door?0:shop?now-o.ShopAt:moving?elapsed*a.StepMs/stepMs:now-o.UpdatedAt;
            if(!door&&!shop&&WithProjectileHit(o.ObjectId,o.Combat,now,o.Dead) is {} combat){string requested=combat.Action switch{1=>"attack",2=>"damage",8=>"die",18=>"spellDir",19=>"spellNoDir",_=>"unsupported"};if(!a.Supports(requested,o.Direction)){unavailable.Add(o);continue;}double age=now-combat.At;if((combat.Timing==null||combat.Timing.Matches(o.Gfx,o.Action))&&(o.Dead||age<(combat.Timing?.AnimationMs??a.Duration(requested)))){moving=false;action=requested;animationElapsed=age;stepMs=combat.Timing?.AnimationMs??(int)a.Duration(requested);}}
            // Noncombat NPCs and scenery only have the actions present in their original metadata.
            if(!a.Supports(action,o.Direction)){unavailable.Add(o);continue;}
            var displayedCombat=WithProjectileHit(o.ObjectId,o.Combat,now,o.Dead);var frame=displayedCombat?.Timing is {} timing&&action==ActionCadence.Name(timing.Action)?timing.Frame(a,o.Direction,animationElapsed):a.At(action,o.Direction,animationElapsed);
            int x=moving?o.SourceX:o.X,y=moving?o.SourceY:o.Y;
            draws.AddRange(ShadowDraws(a,frame,x,y,o.ObjectId));
            // An authenticated original zero-tile frame is transparent, not a missing asset.
            if(frame.Blocks.Length==0){vs.Add(new(o.ObjectId,o.Gfx,a.Sprite,catalog.DisplayName(o),x,y,action,frame.Key,Coordinates.Screen(x,y),Rectangle.Empty,false,stepMs));continue;}
            var blocks=frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile,o.ObjectId)).ToList();
            draws.AddRange(blocks);int left=blocks.Min(b=>b.Px),top=blocks.Min(b=>b.Py);var bounds=Rectangle.FromLTRB(left,top,blocks.Max(b=>b.Px+24),blocks.Max(b=>b.Py+24));
            var foot=Coordinates.Screen(x+(moving?MovementRules.Dx[o.Direction]*elapsed/stepMs:0),y+(moving?MovementRules.Dy[o.Direction]*elapsed/stepMs:0));
            vs.Add(new(o.ObjectId,o.Gfx,a.Sprite,catalog.DisplayName(o),x,y,action,frame.Key,foot,bounds,moving,stepMs));
        }
        int l=draws.Min(d=>d.Px),t=draws.Min(d=>d.Py),r=draws.Max(d=>d.Px+24),bott=draws.Max(d=>d.Py+24);
        var area=Rectangle.Intersect(Rectangle.FromLTRB(l,t,r,bott),Bounds);
        // Stable ID tie-break makes exact overlaps deterministic, independent of arrival order.
        var commands=ActiveScene.Where(c=>c.Px<area.Right&&c.Px+24>area.Left&&c.Py<area.Bottom&&c.Py+24>area.Top).Concat(draws).OrderBy(c=>c,DrawComparer.Instance).ThenBy(c=>c.ObjectId).ToList();
        visuals=vs.ToArray();missing=unavailable.ToArray();return ComposeAfflictionPatch(session,area,commands,opacity);
    }
}
public sealed partial class ClientForm {
    ObjectAssets? objectAssets;Patch? objectPatch;ObjectVisual[] objectVisuals=[];WorldObject[] missingObjects=[];
    void RefreshObjects(){
        objectAssets??=new(World.Assets.Root);
        objectPatch=renderer.ComposeObjects(World,login.World,objectAssets,out objectVisuals,out missingObjects);
        Camera.SetBounds(renderer.Bounds);
        patch?.Dispose();patch=Renderer.BitmapOf(objectPatch.Bounds.Width,objectPatch.Bounds.Height,objectPatch.Colors,objectPatch.Mask);patchRect=objectPatch.Bounds;RecordOpacityFrame();
    }
    void PickObject(Point location){
        uint? id=null;var p=Camera.ToScene(location);
        if(login.World.Renderable&&objectPatch is {} op){int x=(int)Math.Floor(p.X)-op.Bounds.X,y=(int)Math.Floor(p.Y)-op.Bounds.Y;if((uint)x<(uint)op.Bounds.Width&&(uint)y<(uint)op.Bounds.Height)id=op.EntityOwners?[y*op.Bounds.Width+x];}
        if(id is uint gid&&login.World.Ground.Any(i=>i.ObjectId==gid)){login.World.SelectGround(gid);}else{login.World.SelectGround(null);login.World.SelectTarget(id);}lastWebState="";PublishWebState();Invalidate();
        events.Add(new{kind="object-pick",scene=p,targetId=id,strategy="topmost visible opaque pixel after original tile/actor depth composition"});
    }
    void PaintObjectSelection(Graphics g){
        if(login.World.TargetId is uint id&&objectVisuals.FirstOrDefault(v=>v.ObjectId==id) is {} v){var p=Camera.ToView(v.Foot);using var pen=new Pen(Color.Gold,2);g.DrawEllipse(pen,p.X-19*Camera.Zoom,p.Y-7*Camera.Zoom,38*Camera.Zoom,14*Camera.Zoom);g.DrawString(v.Name+" #"+id,Font,Brushes.Gold,p.X-40,p.Y+10);}
        foreach(var groundVisual in objectVisuals.Where(v=>v.Action=="ground")){var pt=Camera.ToView(groundVisual.Foot);bool selected=login.World.SelectedGroundId==groundVisual.ObjectId;g.DrawString(groundVisual.Name,Font,selected?Brushes.Gold:Brushes.White,pt.X-20,pt.Y+17);if(selected){using var pen=new Pen(Color.Gold,2);g.DrawEllipse(pen,pt.X-16,pt.Y+3,32,12);}}
        PaintObjectPresence(g,Camera.ToView,new RectangleF(Camera.ViewOffset,Camera.View));
        PaintDuelOpponent(g);
    }
    object ObjectUiSnapshot()=>new{ownCombatVisual=renderer.OwnCombatVisual,magic=renderer.MagicVisuals,projectile=renderer.CurrentProjectileVisual,projectiles=renderer.ProjectileVisuals,visuals=objectVisuals,presence=PresenceSnapshot(),missing=missingObjects.Select(o=>new{o.ObjectId,o.Gfx,o.Action,o.Speed,o.Brave,o.ReceivedAction,o.Name,o.X,o.Y,o.ShopActive}),target=login.World.Target,targetName=login.World.Target is {} t?objectAssets?.DisplayName(t):null,world=login.World.Snapshot()};
}
