namespace P06;
public sealed record ObjectVisual(uint ObjectId,int Gfx,int Sprite,string Name,int X,int Y,string Action,string Frame,PointF Foot,Rectangle Bounds,bool Walking,int StepMs);
public sealed partial class Renderer {
    public ObjectVisual? OwnCombatVisual {get;private set;}
    public Patch ComposeObjects(World own,WorldSession session,ObjectAssets catalog,out ObjectVisual[] visuals,out WorldObject[] missing,long? testTime=null,bool historicalUntriggeredReplay=false){
        long now=testTime??Environment.TickCount64;session.AdvanceScene();BindCache(historicalUntriggeredReplay?null:session.MapCache);var opacity=historicalUntriggeredReplay?null:session.Opacity;SceneBackground(opacity);PrepareShipBackdrop(session,now);
        ObjectAppearance? ownAppearance=session.Actor is {} ownActor?catalog.Get(ownActor.Gfx,ownActor.Weapon)??throw new NotSupportedException("Own appearance profile unavailable"):null;
        own.SetOnlineAppearance(ownAppearance);var draws=Actor(own);OwnCombatVisual=session.Actor is {} shown&&ownAppearance!=null?new(shown.ObjectId,shown.Gfx,ownAppearance.Sprite,shown.Name,own.X,own.Y,own.Walking?"walk":"breath",own.Frame.Key,own.DisplayPosition,Rectangle.Empty,own.Walking,own.Walking?own.NetworkStepMs:session.MovementAnimationMs??ownAppearance.StepMs):null;
        ComposeMapEffects(session,draws,now);ComposeProjectile(session,draws,now);ComposeMagic(session,draws,now);ComposeSpecialShapeEffects(session,draws,now);
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
            bool door=o.Gfx==2346||session.IsDoor(o.ObjectId),shop=!door&&o.ShopActive&&!o.Dead;var a=door?catalog.GetDoor(o.Gfx):shop?catalog.GetShop(o.Gfx):catalog.Get(o.Gfx,o.Action);
            if(a==null||o.AppearancePending||(!shop&&(!NormalSpeed.Supported(o.Speed,o.Brave)||o.MotionAt!=0&&!NormalSpeed.Supported(o.MotionSpeed,o.MotionBrave)))||(door?(o.DoorAction is not (28 or 29)||o.Dead||o.Combat!=null):o.ReceivedAction is not (null or 0 or 1 or 2 or 3 or 8 or 18 or 19 or 70))||!(session.MapCache?.Contains(o.X,o.Y)??Coordinates.Inside(o.X,o.Y))){unavailable.Add(o);continue;}
            int stepMs=shop?a.StepMs:NormalSpeed.AnimationMs(a.StepMs,o.MotionAt!=0?o.MotionSpeed:o.Speed,o.MotionAt!=0?o.MotionBrave:o.Brave);double elapsed=now-o.MotionAt;bool moving=!door&&!shop&&o.MotionAt!=0&&elapsed<stepMs;
            string action=door?(o.DoorAction==28?"open":"close"):shop?"shop":moving?"walk":"breath";double animationElapsed=door?session.DoorPlaybackElapsed(o.ObjectId,now)??double.MaxValue:shop?now-o.ShopAt:moving?elapsed*a.StepMs/stepMs:now-o.UpdatedAt;
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
        var composed=ComposeTitlesAndEmotes(own,session,catalog,ComposeAfflictionPatch(session,area,commands,opacity),vs,now);visuals=vs.ToArray();missing=unavailable.ToArray();
        // Screen rectangle R (sub_A3ED50) per actor, from the final actor tiles; key 0 is the own character.
        ActorBounds=composed.Commands.Where(d=>d.Layer=="actor").GroupBy(d=>d.ObjectId).ToDictionary(g=>g.Key,g=>Rectangle.FromLTRB(g.Min(d=>d.Px),g.Min(d=>d.Py),g.Max(d=>d.Px+24),g.Max(d=>d.Py+24)));
        return composed;
    }
    public IReadOnlyDictionary<uint,Rectangle> ActorBounds {get;private set;}=new Dictionary<uint,Rectangle>();
}

// Name, title and emblem placement: docs/reference/actions-and-names.md 2.2-2.4 (TW16 sub_EFF300, sub_F01380,
// sub_F02810). Coordinates are scene pixels; the painter scales them with the camera like the sprites.
public sealed record NameTarget(uint ObjectId,string Name,Rectangle Bounds,bool Self);
public sealed record NameText(uint ObjectId,bool Title,string Text,ushort Color,PointF Position);
public sealed record NameEmblem(uint ObjectId,uint EmblemId,PointF Position,bool OverHead);
public sealed record NamePlan(NameText[] Texts,NameEmblem[] Emblems);
public static class NameLayout {
    public const int GlyphWidth=6,FontPixels=12,EmblemWidth=16,EmblemHeight=12;
    public const int NameDy=-3,TitleLine=13,EmblemDx=-20,EmblemDy=-4,ClanNameShift=10,PartyDy=-9,AllNamesDy=-16,HeadEmblemDy=-23;
    public static int ByteLength(string text)=>LoginPackets.TextEncoding.GetByteCount(text);
    // X0(s) = L + (W - 6*len)/2: fixed 6 px per Big5 byte.
    public static int CenterX(Rectangle r,string text)=>r.Left+(r.Width-GlyphWidth*ByteLength(text))/2;
    public static (PointF? Title,PointF Name,PointF Emblem) Hover(Rectangle r,string title,string name,bool clan,bool partyOther){
        bool titled=title.Length>0;int dy=partyOther?PartyDy:0,x=CenterX(r,name);
        PointF? t=titled?new(CenterX(r,title),r.Top+NameDy):null;
        return(t,new(x+(clan?ClanNameShift:0),r.Top+NameDy+(titled?TitleLine:0)+dy),new(x+EmblemDx,r.Top+EmblemDy+(titled?TitleLine:0)+dy));
    }
    // Full-name mode puts the name at T-16 and never draws the title. Its emblem routine (sub_F02870) was not
    // decompiled, so the hover-mode offsets relative to the name are reused (推測).
    public static (PointF Name,PointF Emblem) AllNames(Rectangle r,string name,bool clan){
        int x=CenterX(r,name);return(new(x+(clan?ClanNameShift:0),r.Top+AllNamesDy),new(x+EmblemDx,r.Top+AllNamesDy-1));
    }
    public static PointF HeadEmblem(Rectangle r)=>new(r.Left+(r.Width-EmblemWidth)/2,r.Top+HeadEmblemDy);
    public static NamePlan Plan(WorldSession w,IReadOnlyList<NameTarget> targets,uint? hovered,bool showAll,long? now=null){
        var texts=new List<NameText>();var emblems=new List<NameEmblem>();
        // Blind mode 1 blacks out everyone else; their names must not reveal where they stand.
        bool blind=w.BlindMode==1;uint own=w.Actor?.ObjectId??0;string ownClan=w.Actor?.Clan??"";bool war=w.ClanWarMode;
        foreach(var t in targets){
            if(t.Name.Length==0||blind&&!t.Self)continue;
            var (clan,clanId)=w.ClanIdentity(t.ObjectId);bool hasClan=clanId!=0;ushort color=w.NameColor555(t.ObjectId,now);
            if(hovered==t.ObjectId){
                string title=w.DisplayTitle(t.ObjectId);var p=Hover(t.Bounds,title,t.Name,hasClan,!t.Self&&w.IsPartyMemberId(t.ObjectId));
                if(p.Title is {} tp)texts.Add(new(t.ObjectId,true,title,color,tp));
                texts.Add(new(t.ObjectId,false,t.Name,color,p.Name));if(hasClan)emblems.Add(new(t.ObjectId,clanId,p.Emblem,false));
            }else if(showAll&&w.ClanWarFlag(t.ObjectId)==0){
                var p=AllNames(t.Bounds,t.Name,hasClan);texts.Add(new(t.ObjectId,false,t.Name,color,p.Name));if(hasClan)emblems.Add(new(t.ObjectId,clanId,p.Emblem,false));
            }
            // sub_F09070: own clan members while we have a clan; sub_F08C30: +197!=0 in war mode.
            if(hasClan&&(ownClan.Length>0&&ClanWarProtocol.Same(clan,ownClan)||war&&w.ClanWarFlag(t.ObjectId)!=0))emblems.Add(new(t.ObjectId,clanId,HeadEmblem(t.Bounds),true));
        }
        return new(texts.ToArray(),emblems.ToArray());
    }
}
public sealed partial class WorldSession {
    internal (string Clan,uint ClanId) ClanIdentity(uint id)=>Actor?.ObjectId==id?(Actor.Clan,Actor.ClanId):objects.TryGetValue(id,out var o)?(o.Clan,o.ClanId):("",0u);
    internal bool IsPartyMemberId(uint id)=>id!=0&&partyMembers.Values.Any(m=>m.ObjectId==id);
}
// "Show all names" option (0x138BE7F). There is no prior client settings file, so it is kept beside the
// emblem cache in the engine user-data directory.
public static class NameDisplayOptions {
    public static string? FilePath {get;set;}
    public static string DefaultFilePath=>Path.Combine(Path.GetDirectoryName(ClanEmblemCache.DefaultDiskPath)!,"name-display.txt");
    public static bool LoadShowAll(){try{return FilePath is {} p&&File.Exists(p)&&File.ReadAllText(p).Trim()=="show-all-names=1";}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
    public static void SaveShowAll(bool value){
        if(FilePath is not {} p)return;
        try{Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,value?"show-all-names=1\n":"show-all-names=0\n");}catch(IOException){}catch(UnauthorizedAccessException){}
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
    static readonly bool nameDisplayConfigured=ConfigureNameDisplay();
    static bool ConfigureNameDisplay(){ClanEmblemCache.DiskPath??=ClanEmblemCache.DefaultDiskPath;NameDisplayOptions.FilePath??=NameDisplayOptions.DefaultFilePath;return true;}
    bool nameHoverHooked,showAllNames;Point? nameMouse;uint? paintedHover;
    void HookNameDisplay(){
        if(nameHoverHooked)return;nameHoverHooked=true;_=nameDisplayConfigured;showAllNames=NameDisplayOptions.LoadShowAll();
        MouseMove+=(_,e)=>{nameMouse=e.Location;var id=NameHoverTarget(e.Location);UpdateAttackCursor(id);if(id!=paintedHover)Invalidate();};
        MouseLeave+=(_,_)=>{nameMouse=null;UpdateAttackCursor(null);if(paintedHover!=null)Invalidate();};
        BeginInvoke(()=>{
            var box=new CheckBox{Text="全部顯示名稱",AutoSize=true,Checked=showAllNames,AccessibleName="全部顯示名稱"};
            box.CheckedChanged+=(_,_)=>{showAllNames=box.Checked;NameDisplayOptions.SaveShowAll(showAllNames);Invalidate();};
            if(normalPanels!=null){var page=new TabPage("名稱顯示");page.Controls.Add(box);normalPanels.TabPages.Add(page);}
            else{box.Dock=DockStyle.Right;bar.Controls.Add(box);box.BringToFront();}
        });
    }
    // Pointer pixel owner (own pixels carry no id, so the own rectangle stands in); ground items have their own label.
    uint? NameHoverTarget(Point point){
        if(!login.World.Renderable)return null;var id=PointerPixel(point);
        if(id is uint g)return login.World.Ground.Any(i=>i.ObjectId==g)?null:g;
        var p=Camera.ToScene(point);return login.World.Actor is {} a&&renderer.ActorBounds.TryGetValue(0,out var r)&&r.Contains((int)Math.Floor(p.X),(int)Math.Floor(p.Y))?a.ObjectId:null;
    }
    List<NameTarget> NameTargets(){
        var list=new List<NameTarget>();
        static Rectangle Fallback(PointF foot)=>new((int)foot.X-24,(int)foot.Y-64,48,64);
        if(login.World.Actor is {} a&&renderer.OwnCombatVisual is {} own)list.Add(new(a.ObjectId,TitleAndEmotePackets.DisplayText(a.Name),renderer.ActorBounds.TryGetValue(0,out var r)?r:Fallback(own.Foot),true));
        foreach(var v in objectVisuals){
            if(v.Action=="ground"||objectPatch?.VisibleObjectIds.Contains(v.ObjectId)!=true)continue;
            list.Add(new(v.ObjectId,TitleAndEmotePackets.DisplayText(v.Name),renderer.ActorBounds.TryGetValue(v.ObjectId,out var r)?r:v.Bounds.IsEmpty?Fallback(v.Foot):v.Bounds,false));
        }
        return list;
    }
    void PaintNames(Graphics g){
        HookNameDisplay();if(!login.World.Renderable){paintedHover=null;return;}
        var hovered=nameMouse is Point m?NameHoverTarget(m):null;paintedHover=hovered;
        var plan=NameLayout.Plan(login.World,NameTargets(),hovered,showAllNames);
        float zoom=Camera.Zoom;bool dim=login.World.BlindMode==2;
        var state=g.Save();
        try{
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            foreach(var e in plan.Emblems){
                if(login.ClanEmblemFor(e.ObjectId,e.EmblemId) is not {} emblem)continue;
                using var bitmap=new Bitmap(NameLayout.EmblemWidth,NameLayout.EmblemHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                for(int y=0;y<NameLayout.EmblemHeight;y++)for(int x=0;x<NameLayout.EmblemWidth;x++)bitmap.SetPixel(x,y,Color.FromArgb(emblem.Pixels[y*16+x]));
                var p=Camera.ToView(e.Position);g.DrawImage(bitmap,p.X,p.Y,NameLayout.EmblemWidth*zoom,NameLayout.EmblemHeight*zoom);
            }
            // MingLiU 12 px, weight 400; outline colour palette[0] at +-1 px, then the name colour (+204).
            using var font=new Font("MingLiU",NameLayout.FontPixels*zoom,FontStyle.Regular,GraphicsUnit.Pixel);
            using var outline=new SolidBrush(NameColors.ToColor(NameColors.Outline));var format=StringFormat.GenericTypographic;
            foreach(var t in plan.Texts){
                var p=Camera.ToView(t.Position);using var brush=new SolidBrush(NameColors.ToColor(dim?AfflictionColors.Darken(t.Color):t.Color));
                foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1)})g.DrawString(t.Text,font,outline,p.X+dx*zoom,p.Y+dy*zoom,format);
                g.DrawString(t.Text,font,brush,p.X,p.Y,format);
            }
        }finally{g.Restore(state);}
    }
    void PaintObjectSelection(Graphics g){
        if(login.World.TargetId is uint id&&objectVisuals.FirstOrDefault(v=>v.ObjectId==id) is {} v){var p=Camera.ToView(v.Foot);using var pen=new Pen(Color.Gold,2);g.DrawEllipse(pen,p.X-19*Camera.Zoom,p.Y-7*Camera.Zoom,38*Camera.Zoom,14*Camera.Zoom);}
        foreach(var groundVisual in objectVisuals.Where(v=>v.Action=="ground")){var pt=Camera.ToView(groundVisual.Foot);bool selected=login.World.SelectedGroundId==groundVisual.ObjectId;g.DrawString(groundVisual.Name,Font,selected?Brushes.Gold:Brushes.White,pt.X-20,pt.Y+17);if(selected){using var pen=new Pen(Color.Gold,2);g.DrawEllipse(pen,pt.X-16,pt.Y+3,32,12);}}
        PaintObjectPresence(g,Camera.ToView,new RectangleF(Camera.ViewOffset,Camera.View));
        PaintNames(g);
        PaintDisplayNotices(g);
    }
    object ObjectUiSnapshot()=>new{ownCombatVisual=renderer.OwnCombatVisual,magic=renderer.MagicVisuals,projectile=renderer.CurrentProjectileVisual,projectiles=renderer.ProjectileVisuals,visuals=objectVisuals,presence=PresenceSnapshot(),missing=missingObjects.Select(o=>new{o.ObjectId,o.Gfx,o.Action,o.Speed,o.Brave,o.ReceivedAction,o.Name,o.X,o.Y,o.ShopActive}),target=login.World.Target,targetName=login.World.Target is {} t?objectAssets?.DisplayName(t):null,world=login.World.Snapshot()};
}
