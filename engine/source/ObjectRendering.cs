using System.Runtime.InteropServices;
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
                ObserveOwnerFrames(session,"self",0,appearance,name,actor.Direction,self.Timing,$"{name}:{self.At}",elapsed,actor.X,actor.Y,false,now);
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
            bool door=o.Gfx==2346||session.IsDoor(o.ObjectId),shop=!door&&o.ShopActive&&!o.Dead;
            // R2-I（AN§3.4）：一般物件缺 (gfx, 姿勢) 時退回姿勢 0，整個 gfx 都沒有圖時改畫 gfx 1120（只對他人；自己不在 Objects 內）。
            // 門與個人商店維持原本的嚴格判斷：沒有查證過的圖就不畫。
            var resolved=door||shop?null:ObjectDrawFallback.Appearance(o.Gfx,o.Action,catalog.Get);
            var a=door?catalog.GetDoor(o.Gfx):shop?catalog.GetShop(o.Gfx):resolved?.Appearance;int drawGfx=resolved?.Gfx??o.Gfx;
            // 引擎沒有對應原圖的 S158 動作（例如 71 釣魚），原版在缺圖時改播姿勢動作（AN§3.4 第 1 點）；商店仍照舊不畫。
            bool otherAction=o.ReceivedAction is not (null or 0 or 1 or 2 or 3 or 8 or 18 or 19 or 70);
            if(a==null||o.AppearancePending||(!shop&&(!NormalSpeed.Supported(o.Speed,o.Brave)||o.MotionAt!=0&&!NormalSpeed.Supported(o.MotionSpeed,o.MotionBrave)))||(door?(o.DoorAction is not (28 or 29)||o.Dead||o.Combat!=null):shop&&otherAction)||!(session.MapCache?.Contains(o.X,o.Y)??Coordinates.Inside(o.X,o.Y))){unavailable.Add(o);continue;}
            int stepMs=shop?a.StepMs:NormalSpeed.AnimationMs(a.StepMs,o.MotionAt!=0?o.MotionSpeed:o.Speed,o.MotionAt!=0?o.MotionBrave:o.Brave);double elapsed=now-o.MotionAt;bool moving=!door&&!shop&&o.MotionAt!=0&&elapsed<stepMs;
            // 換成 1120 時原版播動作 0（走路，即姿勢動作）。
            string action=door?(o.DoorAction==28?"open":"close"):shop?"shop":moving||resolved?.Substituted==true||otherAction?"walk":"breath";double animationElapsed=door?session.DoorPlaybackElapsed(o.ObjectId,now)??double.MaxValue:shop?now-o.ShopAt:moving?elapsed*a.StepMs/stepMs:now-o.UpdatedAt;
            // 門的動作實例＝這次開／關播放的開始時間（每次 S_DOOR 開關重新觸發一次 `]`）。
            long stamp=door?DoorPlaybackStamp(animationElapsed,now):shop?o.ShopAt:moving?o.MotionAt:o.UpdatedAt;
            Func<ObjectAppearance?> bare=()=>catalog.Get(drawGfx,0);
            if(!door&&!shop&&WithProjectileHit(o.ObjectId,o.Combat,now,o.Dead) is {} combat){
                string requested=combat.Action switch{1=>"attack",2=>"damage",8=>"die",18=>"spellDir",19=>"spellNoDir",_=>"unsupported"};
                // 他人缺動作圖時逐動作退回：動作 → 同姿勢的姿勢動作 → 姿勢 0（AN§3.4 第 1 點），不再整個不畫。
                if(ObjectDrawFallback.Action(a,bare,requested,o.Direction,(ap,n,d)=>ap.Supports(n,d)) is not {} pick){unavailable.Add(o);continue;}
                bool exact=pick.Action==requested&&ReferenceEquals(pick.Appearance,a);var exactTiming=exact?combat.Timing:null;double age=now-combat.At;
                if((exactTiming==null||exactTiming.Matches(o.Gfx,o.Action))&&(o.Dead||age<(exactTiming?.AnimationMs??pick.Appearance.Duration(pick.Action)))){a=pick.Appearance;moving=false;action=pick.Action;animationElapsed=age;stepMs=exactTiming?.AnimationMs??(int)a.Duration(action);stamp=combat.At;}
            }
            // Noncombat NPCs and scenery only have the actions present in their original metadata（門與商店不退回）。
            if(door||shop){if(!a.Supports(action,o.Direction)){unavailable.Add(o);continue;}}
            else if(ObjectDrawFallback.Action(a,bare,action,o.Direction,(ap,n,d)=>ap.Supports(n,d)) is {} chosen){a=chosen.Appearance;action=chosen.Action;}
            else{unavailable.Add(o);continue;}
            var displayedCombat=WithProjectileHit(o.ObjectId,o.Combat,now,o.Dead);var cadence=displayedCombat?.Timing is {} timing&&action==ActionCadence.Name(timing.Action)?timing:null;var frame=cadence!=null?cadence.Frame(a,o.Direction,animationElapsed):a.At(action,o.Direction,animationElapsed);
            int x=moving?o.SourceX:o.X,y=moving?o.SourceY:o.Y;
            // `]`／`{`／`}` (effects-2.md §2): children take this frame's position and direction; an invisible
            // other object's state flag is copied, so its children are not drawn either.
            // T290（#698，gap-matrix 13.2 第 8 點、TWP§3.4）：門在原版也是 UserObject，開關動作的 `]` 同樣由 sub_A4B060 處理，
            // 所以門不再排除。門沒有播放中的動作時 DoorPlaybackElapsed 是 null（elapsed＝MaxValue），ObserveOwnerFrames 直接略過。
            ObserveOwnerFrames(session,$"o{o.ObjectId}",o.ObjectId,a,action,o.Direction,cadence,$"{action}:{stamp}",animationElapsed,x,y,o.ObjectId!=session.Actor?.ObjectId&&(o.Flags&2)!=0,now);
            draws.AddRange(ShadowDraws(a,frame,x,y,o.ObjectId));
            // An authenticated original zero-tile frame is transparent, not a missing asset.
            if(frame.Blocks.Length==0){vs.Add(new(o.ObjectId,o.Gfx,a.Sprite,catalog.DisplayName(o),x,y,action,frame.Key,Coordinates.Screen(x,y),Rectangle.Empty,false,stepMs));continue;}
            var blocks=frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile,o.ObjectId)).ToList();
            draws.AddRange(blocks);int left=blocks.Min(b=>b.Px),top=blocks.Min(b=>b.Py);var bounds=Rectangle.FromLTRB(left,top,blocks.Max(b=>b.Px+24),blocks.Max(b=>b.Py+24));
            var foot=Coordinates.Screen(x+(moving?MovementRules.Dx[o.Direction]*elapsed/stepMs:0),y+(moving?MovementRules.Dy[o.Direction]*elapsed/stepMs:0));
            vs.Add(new(o.ObjectId,o.Gfx,a.Sprite,catalog.DisplayName(o),x,y,action,frame.Key,foot,bounds,moving,stepMs));
        }
        ComposeSubEffects(session,draws,now);
        int l=draws.Min(d=>d.Px),t=draws.Min(d=>d.Py),r=draws.Max(d=>d.Px+24),bott=draws.Max(d=>d.Py+24);
        var area=Rectangle.Intersect(Rectangle.FromLTRB(l,t,r,bott),Bounds);
        // Stable ID tie-break makes exact overlaps deterministic, independent of arrival order.
        var commands=ActiveScene.Where(c=>c.Px<area.Right&&c.Px+24>area.Left&&c.Py<area.Bottom&&c.Py+24>area.Top).Concat(draws).OrderBy(c=>c,DrawComparer.Instance).ThenBy(c=>c.ObjectId).ToList();
        var composed=ComposeTitlesAndEmotes(own,session,catalog,ComposeFadedPatch(session,area,commands,opacity,now),vs,now);visuals=vs.ToArray();missing=unavailable.ToArray();
        // Screen rectangle R (sub_A3ED50) per actor, from the final actor tiles; key 0 is the own character.
        ActorBounds=composed.Commands.Where(d=>d.Layer=="actor").GroupBy(d=>d.ObjectId).ToDictionary(g=>g.Key,g=>Rectangle.FromLTRB(g.Min(d=>d.Px),g.Min(d=>d.Py),g.Max(d=>d.Px+24),g.Max(d=>d.Py+24)));
        return composed;
    }
    public IReadOnlyDictionary<uint,Rectangle> ActorBounds {get;private set;}=new Dictionary<uint,Rectangle>();
    /// <summary>T290：門播放實例的時間戳（播放開始＝now − elapsed）；沒有播放中的動作（elapsed＝MaxValue）時是 0。</summary>
    internal static long DoorPlaybackStamp(double elapsed,long now)=>elapsed<double.MaxValue/2?now-(long)elapsed:0;
}

// Name, title and emblem placement: docs/reference/actions-and-names.md 2.2-2.4 (TW16 sub_EFF300, sub_F01380,
// sub_F02810). Coordinates are scene pixels; the painter scales them with the camera like the sprites.
public sealed record NameTarget(uint ObjectId,string Name,Rectangle Bounds,bool Self);
// Outline：外框色（palette[0] 黑；怪物等級差選項開啟時改成等級差色，AN§2.4）。Line252：全名模式的 +252 淺藍行。
public sealed record NameText(uint ObjectId,bool Title,string Text,ushort Color,PointF Position){public ushort Outline {get;init;}=NameColors.Outline;public bool Line252 {get;init;}}
public sealed record NameEmblem(uint ObjectId,uint EmblemId,PointF Position,bool OverHead);
// 名稱旁的小圖示（AN§2.3、FU5§1）：Kind＝"mark181-1"／"mark181-2"／"party"／"master"；Surf＝要畫的 surf（&lt;id&gt;.img，0＝不畫）。
// party：隊長 surf 8264、隊員 surf 2826（sub_9551E0 的 map&lt;索引, surf&gt; 由誰填沒查到，一律 2826）；master：surf 773。
// mark181：icon 5199（.tbt），3.81 不送 S250/148，S87 尾端也不會是 1／2（FU5§1），所以實際不會出現，Surf＝0。
public sealed record NameIcon(uint ObjectId,string Kind,PointF Position){public int Surf {get;init;}}
public sealed record NamePlan(NameText[] Texts,NameEmblem[] Emblems){public NameIcon[] Icons {get;init;}=[];}
// 繪製名稱需要、但 3.81 封包不一定有的欄位。Kind＝原版物件種類（2／3／9／10）；Listed＝種類 10 的「名單內」；
// LevelDiff＝物件等級 − 自己等級（方向是推測）。null／預設值＝不知道，不套用對應規則。
public sealed record NameMarks(byte Mark181=0,bool Master=false,string Line252="",int? Kind=null,bool Listed=false,int? LevelDiff=null);
// R2-I（AN§3.4）：sub_A3EF40 的缺圖退回。泛型只為了讓離線測試不必讀素材。
public static class ObjectDrawFallback {
    public const int MissingSpriteGfx=PlayerProfiles.MissingSpriteGfx;
    public sealed record Resolved<T>(T Appearance,int Gfx,int Weapon,bool Substituted);
    // 1. (gfx, 姿勢) → 2. (gfx, 0)（原版把 +194 改成 0）→ 3. 整個 gfx 沒有 sprite：改成 gfx 1120、動作 0。
    public static Resolved<T>? Appearance<T>(int gfx,int weapon,Func<int,int,T?> get) where T:class{
        if(get(gfx,weapon) is {} exact)return new(exact,gfx,weapon,false);
        if(weapon!=0&&get(gfx,0) is {} bare)return new(bare,gfx,0,false);
        return gfx!=MissingSpriteGfx&&get(MissingSpriteGfx,0) is {} substitute?new(substitute,MissingSpriteGfx,0,true):null;
    }
    // 缺動作：改播同姿勢的姿勢動作（引擎名稱 "walk"＝動作編號＝姿勢），再沒有就姿勢 0 的姿勢動作。
    // 姿勢 0 的動作 0 本身缺圖時不再退（原版只對動作 > 0 退回）。
    public static (T Appearance,string Action)? Action<T>(T appearance,Func<T?> bare,string action,int direction,Func<T,string,int,bool> supports) where T:class{
        if(supports(appearance,action,direction))return(appearance,action);
        if(action!="walk"&&supports(appearance,"walk",direction))return(appearance,"walk");
        return bare() is {} b&&!ReferenceEquals(b,appearance)&&supports(b,"walk",direction)?(b,"walk"):null;
    }
}
// R2-I 城主王冠（sprites-and-strings.md §1，followups-3.md §2）：crown.raw＝Sprite.pak 內 12 格 16×12 RGB555，0 透明，沒有檔頭。
// 執行期從原客戶端唯讀讀取並核對 pin，不複製進專案。
public sealed class CastleCrownImages {
    public const string EntryName="crown.raw",PayloadSha256="0e6dc26b1caca1725535c76b44d215df3c850ca42fa5373c54bf7d741014a7db";
    public const int Width=16,Height=12,FramePixels=Width*Height,PayloadBytes=CastleProtocol.CrownFrames*FramePixels*2;
    readonly ushort[] pixels;
    CastleCrownImages(ushort[] pixels){this.pixels=pixels;}
    /// <summary>第 f 格（0–11）的 192 個像素（列優先）；格號越界時回空。</summary>
    public ReadOnlySpan<ushort> Frame(int f)=>(uint)f<CastleProtocol.CrownFrames?pixels.AsSpan(f*FramePixels,FramePixels):default;
    public static CastleCrownImages Parse(byte[] payload){
        if(payload.Length!=PayloadBytes)throw new InvalidDataException("crown.raw 長度不符");
        var p=new ushort[payload.Length/2];for(int i=0;i<p.Length;i++)p[i]=BitConverter.ToUInt16(payload,2*i);
        return new(p);
    }
    /// <summary>照 AmbienceLightImages.Load：核對 Sprite.idx pin、Sprite.pak 長度、唯一一筆（不分大小寫）與 payload SHA-256。</summary>
    public static CastleCrownImages Load(string? clientRoot=null){
        string root=Path.GetFullPath(clientRoot??GroundSpriteReader.ClientRoot);
        byte[] index=File.ReadAllBytes(Path.Combine(root,"Sprite.idx"));
        if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(index)).Equals(AmbienceLightImages.IndexSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Sprite.idx pin 不符");
        uint count=BitConverter.ToUInt32(index,0);
        if(index.Length!=4+28L*count)throw new InvalidDataException("Sprite.idx 長度不符");
        (long Offset,int Size)? found=null;
        for(int i=0;i<count;i++){
            int at=4+28*i,end=Array.IndexOf(index,(byte)0,at+4,20);if(end<0)end=at+24;
            if(!System.Text.Encoding.ASCII.GetString(index,at+4,end-at-4).Equals(EntryName,StringComparison.OrdinalIgnoreCase))continue;
            if(found!=null)throw new InvalidDataException("Sprite.idx 有重複的 crown.raw");
            found=(BitConverter.ToUInt32(index,at),checked((int)BitConverter.ToUInt32(index,at+24)));
        }
        if(found is not {} entry)throw new InvalidDataException("Sprite.idx 找不到 crown.raw");
        using var pak=new FileStream(Path.Combine(root,"Sprite.pak"),FileMode.Open,FileAccess.Read,FileShare.Read);
        if(pak.Length!=AmbienceLightImages.PakSize||entry.Size!=PayloadBytes||entry.Offset+entry.Size>pak.Length)throw new InvalidDataException("Sprite.pak 長度或 crown.raw 範圍不符");
        pak.Position=entry.Offset;var payload=new byte[entry.Size];pak.ReadExactly(payload);
        if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).Equals(PayloadSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("crown.raw payload pin 不符");
        return Parse(payload);
    }
}
/// <summary>一頂要畫的王冠：Position＝場景座標的左上角（16×12）。</summary>
public sealed record CrownDraw(uint ObjectId,byte Castle,int Frame,PointF Position,bool Self);
public sealed record CrownTarget(uint ObjectId,int Gfx,int X,int Y,bool Self);
public static class CrownLayout {
    /// <summary>sub_501260：gfx 6080／6094 用高度 50，其餘 40。</summary>
    public static bool Tall(int gfx)=>gfx is 6080 or 6094;
    // sub_F1A750 的人物畫面點＝精靈區塊偏移的原點＝Coordinates.Screen(x,y) −(24,12)（與 Draw.Px／Py 在 A=B=0 時相同）。
    // 王冠位置照引擎畫身體的同一個格位（ComposeObjects 的 x、y），不另套走路內插表（SS§1.3）。
    public static PointF Position(int x,int y,int gfx){
        var s=Coordinates.Screen(x,y);var (cx,cy)=CastleProtocol.CrownAnchor((int)s.X-24,(int)s.Y-12,Tall(gfx));return new(cx,cy);
    }
    /// <summary>sub_F08880 的條件：自己一律畫；他人沒有隱身旗標（Flags &amp; 2，不看同隊同盟可見，FU3§2）。
    /// 盲目 1 不畫他人（與名稱相同，推測）。y＞0 與整格在畫面內由 <see cref="Fits"/> 在畫面座標判斷。</summary>
    public static CrownDraw[] Plan(WorldSession w,IEnumerable<CrownTarget> targets){
        var list=new List<CrownDraw>();bool blind=w.BlindMode==1;
        foreach(var t in targets){
            if(!t.Self&&(blind||w.InvisibleFlagOf(t.ObjectId)))continue;
            foreach(var c in w.CastleCrownFrames(t.ObjectId))list.Add(new(t.ObjectId,c.Castle,c.Frame,Position(t.X,t.Y,t.Gfx),t.Self));
        }
        return list.ToArray();
    }
    /// <summary>sub_96E790 不裁切：整格要落在畫面內才畫；他人另外要 y＞0（sub_971E50）。</summary>
    public static bool Fits(PointF view,float zoom,SizeF client,bool self)=>
        (self||view.Y>0)&&view.X>=0&&view.Y>=0&&view.X+CastleCrownImages.Width*zoom<=client.Width&&view.Y+CastleCrownImages.Height*zoom<=client.Height;
}
public static class NameLayout {
    public const int GlyphWidth=6,FontPixels=12,EmblemWidth=16,EmblemHeight=12;
    public const int NameDy=-3,TitleLine=13,EmblemDx=-20,EmblemDy=-4,ClanNameShift=10,PartyDy=-9,AllNamesDy=-16,HeadEmblemDy=-23;
    public static int ByteLength(string text)=>LoginPackets.TextEncoding.GetByteCount(text);
    // X0(s) = L + (W - 6*len)/2: fixed 6 px per Big5 byte.
    public static int CenterX(Rectangle r,string text)=>r.Left+(r.Width-GlyphWidth*ByteLength(text))/2;
    // R6-B（T338／#784，AN§2.3、FU8§1.1）：sub_EFF300 的自己分支（0xEFF3AD–0xEFF3C8、0xEFF4E8–0xEFF503）
    // X0 = P.x − 3·len + dword_154524C − 56（800×600 下 dword_154524C＝0）。P＝sub_F1A750(obj+16)＝精靈區塊偏移原點
    // ＝Coordinates.Screen(腳下格) −(24,12)（與 CrownLayout 同一約定）。Y 仍照外框 T；只有 X 換基準。
    public const int SelfOriginDx=-24,SelfNameDx=-56;
    /// <summary>自己的 P.x（場景座標，取整同 <see cref="CrownLayout.Position"/>）。</summary>
    public static int SelfOriginX(int x,int y)=>(int)Coordinates.Screen(x,y).X+SelfOriginDx;
    /// <summary>自己的置中基準：P.x − 56 − 3·len。</summary>
    public static int SelfX(int originX,string text)=>originX+SelfNameDx-GlyphWidth/2*ByteLength(text);
    static int BaseX(Rectangle r,string text,int? selfOriginX)=>selfOriginX is int p?SelfX(p,text):CenterX(r,text);
    /// <summary>selfOriginX 有值＝自己：名稱、稱號、盟徽改以 P.x 為基準（盟徽仍是名稱 X0 − 20、血盟名稱 ＋10）。</summary>
    public static (PointF? Title,PointF Name,PointF Emblem) Hover(Rectangle r,string title,string name,bool clan,bool partyOther,int? selfOriginX=null){
        bool titled=title.Length>0;int dy=partyOther?PartyDy:0,x=BaseX(r,name,selfOriginX);
        PointF? t=titled?new(BaseX(r,title,selfOriginX),r.Top+NameDy):null;
        return(t,new(x+(clan?ClanNameShift:0),r.Top+NameDy+(titled?TitleLine:0)+dy),new(x+EmblemDx,r.Top+EmblemDy+(titled?TitleLine:0)+dy));
    }
    // Full-name mode puts the name at T-16 and never draws the title. Its emblem routine (sub_F02870) was not
    // decompiled, so the hover-mode offsets relative to the name are reused (推測).
    public static (PointF Name,PointF Emblem) AllNames(Rectangle r,string name,bool clan){
        int x=CenterX(r,name);return(new(x+(clan?ClanNameShift:0),r.Top+AllNamesDy),new(x+EmblemDx,r.Top+AllNamesDy-1));
    }
    public static PointF HeadEmblem(Rectangle r)=>new(r.Left+(r.Width-EmblemWidth)/2,r.Top+HeadEmblemDy);
    // AN§2.3 滑鼠指到時的圖示：+181（1 或 2）在盟徽 X −10（有血盟）或 +10（無血盟），Y 同盟徽；
    // 隊伍圖示在名稱 X＋名稱寬＋2，主人圖示在名稱 X＋名稱寬＋7，Y 同名稱（FU5§1 已驗證：sub_957A80、sub_EFF300 結尾）。
    public const int Mark181WithClanDx=-10,Mark181NoClanDx=10,PartyIconDx=2,MasterIconDx=7;
    /// <summary>FU5§1：隊長 surf 8264（0x2048）、隊員退回 surf 2826、主人 surf 773（`push 305h`）。</summary>
    public const int PartyLeaderSurf=8264,PartyMemberSurf=2826,MasterSurf=773;
    /// <summary>主人圖示的地圖例外（sub_EFF300：地圖 ≠ 509）。</summary>
    public const int MasterIconExcludedMap=509;
    public static NameIcon[] HoverIcons(uint id,PointF name,PointF emblem,string text,bool clan,bool partyOther,byte mark181,bool master,bool partyLeader=false){
        var icons=new List<NameIcon>();float right=name.X+GlyphWidth*ByteLength(text);
        if(mark181 is 1 or 2)icons.Add(new(id,"mark181-"+mark181,new(emblem.X+(clan?Mark181WithClanDx:Mark181NoClanDx),emblem.Y)));
        if(partyOther)icons.Add(new(id,"party",new(right+PartyIconDx,name.Y)){Surf=partyLeader?PartyLeaderSurf:PartyMemberSurf});
        if(master)icons.Add(new(id,"master",new(right+MasterIconDx,name.Y)){Surf=MasterSurf});
        return icons.ToArray();
    }
    // 全名模式（sub_F01380）：+252 行在 T−16（palette[54]），有這一行時名稱移到 T−29、X −3。
    public const int Line252Dy=-16,Line252NameDy=-29,Line252NameDx=-3;
    public const ushort Line252Color=0x4B7F;
    public static (PointF Line,PointF Name) AllNamesWithLine(Rectangle r,string line,string name,bool clan){
        var p=AllNames(r,name,clan);return(new(CenterX(r,line),r.Top+Line252Dy),new(p.Name.X+Line252NameDx,r.Top+Line252NameDy));
    }
    // 全名模式不畫：+196＝1、物件種類 2／3／9；種類 10 只畫名單內的。
    public static bool AllNamesSkips(int? kind,bool listed,bool war196)=>war196||kind is 2 or 3 or 9||kind==10&&!listed;
    // 「怪物等級差」外框（只對種類 10）：不是使用者選項，是 byte_13CE3FE 地區碼（sub_8BA680 依 GetACP() 設定），只有 1／5 才畫（FU5§1）。
    // 台版 950＝3，所以永遠不畫；不做選項 UI。屬性保留給離線測試切換規則。
    public const int TaiwanRegionCode=3;
    public static int MonsterLevelOption {get;set;}=TaiwanRegionCode;
    public static ushort? LevelDiffOutline(int option,int? kind,int? diff)=>option is 1 or 5&&kind==10&&diff is int d?d switch{>=10=>0x7C00,>=3=>0x4200,<=-2=>0x02E0,_=>0x001F}:null;
    public static NamePlan Plan(WorldSession w,IReadOnlyList<NameTarget> targets,uint? hovered,bool showAll,long? now=null){
        var texts=new List<NameText>();var emblems=new List<NameEmblem>();var icons=new List<NameIcon>();
        // Blind mode 1 blacks out everyone else; their names must not reveal where they stand.
        bool blind=w.BlindMode==1;uint own=w.Actor?.ObjectId??0;string ownClan=w.Actor?.Clan??"";bool war=w.ClanWarMode;
        foreach(var t in targets){
            if(t.Name.Length==0||blind&&!t.Self)continue;
            var (clan,clanId)=w.ClanIdentity(t.ObjectId);bool hasClan=clanId!=0;ushort color=w.NameColor555(t.ObjectId,now);
            var marks=w.NameMarksFor(t.ObjectId);ushort outline=LevelDiffOutline(MonsterLevelOption,marks.Kind,marks.LevelDiff)??NameColors.Outline;
            bool war196=w.ClanWarFlag(t.ObjectId)!=0;
            if(hovered==t.ObjectId){
                string title=w.DisplayTitle(t.ObjectId);bool partyOther=!t.Self&&w.IsPartyMemberId(t.ObjectId);
                // 自己的腳下格取 WorldSession.Actor（走路中不內插，見 issue-784 報告「待驗證」）；全名模式（sub_F01380）沒有查到自己分支，不改。
                int? selfOrigin=t.Self&&w.Actor is {} me?SelfOriginX(me.X,me.Y):null;
                var p=Hover(t.Bounds,title,t.Name,hasClan,partyOther,selfOrigin);
                if(p.Title is {} tp)texts.Add(new(t.ObjectId,true,title,color,tp){Outline=outline});
                texts.Add(new(t.ObjectId,false,t.Name,color,p.Name){Outline=outline});if(hasClan)emblems.Add(new(t.ObjectId,clanId,p.Emblem,false));
                // 主人圖示：地圖 ≠ 509、有稱號、非職業外型、+248 非空、非戰爭（+196＝0）（FU5§1）。
                bool master=marks.Master&&title.Length>0&&!war196&&w.MapId!=MasterIconExcludedMap&&!TitleAndEmotePackets.ClassForm(w.GfxOf(t.ObjectId));
                icons.AddRange(HoverIcons(t.ObjectId,p.Name,p.Emblem,t.Name,hasClan,partyOther,marks.Mark181,master,partyOther&&w.IsPartyLeaderId(t.ObjectId)));
            }else if(showAll&&!AllNamesSkips(marks.Kind,marks.Listed,war196)){
                string line=TitleAndEmotePackets.DisplayText(marks.Line252);
                if(line.Length>0){
                    var q=AllNamesWithLine(t.Bounds,line,t.Name,hasClan);texts.Add(new(t.ObjectId,false,line,Line252Color,q.Line){Outline=outline,Line252=true});
                    texts.Add(new(t.ObjectId,false,t.Name,color,q.Name){Outline=outline});
                    // 有 +252 行時的盟徽位置沒有反編譯（sub_F02870），沿用「名稱基準 −20、名稱上方 1 px」跟著名稱移動（推測）。
                    if(hasClan)emblems.Add(new(t.ObjectId,clanId,new(q.Name.X-ClanNameShift+EmblemDx,q.Name.Y-1),false));
                }else{
                    var p=AllNames(t.Bounds,t.Name,hasClan);texts.Add(new(t.ObjectId,false,t.Name,color,p.Name){Outline=outline});if(hasClan)emblems.Add(new(t.ObjectId,clanId,p.Emblem,false));
                }
            }
            // sub_F09070: own clan members while we have a clan; sub_F08C30: +197!=0 in war mode.
            if(hasClan&&(ownClan.Length>0&&ClanWarProtocol.Same(clan,ownClan)||war&&w.ClanWarFlag(t.ObjectId)!=0))emblems.Add(new(t.ObjectId,clanId,HeadEmblem(t.Bounds),true));
        }
        return new(texts.ToArray(),emblems.ToArray()){Icons=icons.ToArray()};
    }
}
// T321（#759）P-E：名稱與稱號照原版走 GDI（UI§1 V4、PAK§4.2 字型列）。sub_8444A0 在 CP950 建
// CreateFontA(12,0,0,0,400,0,0,0,136,0,0,0,0,"MingLiU")（高 12、FW_NORMAL、字元集 136＝CHINESEBIG5、品質 0），
// sub_83DC90／sub_83E100 用 GDI TextOut 畫；名稱先畫 4 向 ±1 px 描邊、最後畫本色（+204）。
// 引擎做法：同一組 CreateFont 參數在記憶體 DIB 上以 1× 光柵化成墨點遮罩，合成描邊＋本色後，依鏡頭倍率最近鄰放大
// （原版也是 1× 畫在 800×600 面上）。MingLiU 不在（GDI 字型對應換成別的字）或 GDI 失敗時回傳 null，
// 呼叫端退回既有 GDI+ DrawString（WinForms 備援），不用替代圖。
// Font\*.fnt 點陣字（kor／eng／small）在 CP950＋MingLiU 時原版不走（UI§1 sub_83DC90），這裡只從主客戶端讀檔並記錄 SHA-256。
public sealed record NameGlyphMask(int Width,int Height,bool[] Ink);
public sealed record NameFontFile(string Name,long Size,string Sha256,int? Glyphs);
public static class NativeNameFont {
    public const string Face="MingLiU",FaceLocal="細明體",FontDirectory="Font";
    public const int Height=12,Weight=400,Quality=0,Border=1,CacheLimit=512;public const byte CharSet=136;
    public static readonly (int Dx,int Dy)[] OutlineOffsets=[(-1,0),(1,0),(0,-1),(0,1)];
    public static bool IsMingLiU(string? face)=>face is not null&&(face.Equals(Face,StringComparison.OrdinalIgnoreCase)||face==FaceLocal);
    /// <summary>遮罩四周各加 1 px：先蓋 4 向描邊，再蓋本色；沒有墨的地方是 0（透明）。</summary>
    public static int[] Compose(NameGlyphMask m,int fillArgb,int outlineArgb,out int width,out int height){
        width=m.Width+2*Border;height=m.Height+2*Border;var px=new int[width*height];
        foreach(var (dx,dy) in OutlineOffsets)Stamp(m,px,width,Border+dx,Border+dy,outlineArgb);
        Stamp(m,px,width,Border,Border,fillArgb);return px;
    }
    static void Stamp(NameGlyphMask m,int[] px,int stride,int ox,int oy,int argb){for(int y=0;y<m.Height;y++)for(int x=0;x<m.Width;x++)if(m.Ink[y*m.Width+x])px[(y+oy)*stride+x+ox]=argb;}
    /// <summary>點陣字檔每字位元組數（推測：eng 6×12＝每列 1 byte、kor 12×12＝每列 2 byte；small 版面未查證）。</summary>
    public static int? GlyphBytes(string name)=>name.StartsWith("eng",StringComparison.OrdinalIgnoreCase)?12:name.StartsWith("kor",StringComparison.OrdinalIgnoreCase)?24:null;
    /// <summary>主客戶端 Font\*.fnt（只看該層，不遞迴、不寫入）：檔名、大小、SHA-256、整除時的字數。</summary>
    public static NameFontFile[] FontFiles(string clientRoot){
        string dir=Path.Combine(clientRoot,FontDirectory);
        try{
            if(!Directory.Exists(dir))return [];
            return Directory.EnumerateFiles(dir,"*.fnt",SearchOption.TopDirectoryOnly).Where(p=>Path.GetExtension(p).Equals(".fnt",StringComparison.OrdinalIgnoreCase))
                .OrderBy(p=>Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).Select(p=>{
                    var bytes=File.ReadAllBytes(p);string name=Path.GetFileName(p);int? per=GlyphBytes(name);
                    return new NameFontFile(name,bytes.Length,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),per is int n&&bytes.Length>0&&bytes.Length%n==0?bytes.Length/n:null);
                }).ToArray();
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException){return [];}
    }
    /// <summary>來源紀錄（給事件記錄用）：GDI 字型實際對應到的字面、系統字型檔 SHA-256、主客戶端 Font\*.fnt。</summary>
    public static object Describe(string clientRoot){
        Ensure();string ttc=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"mingliu.ttc");string? ttcSha=null;long? ttcSize=null;
        try{if(File.Exists(ttc)){using var s=File.OpenRead(ttc);ttcSize=s.Length;ttcSha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)).ToLowerInvariant();}}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
        return new{face=Face,height=Height,weight=Weight,charSet=CharSet,quality=Quality,faceUsed=FaceUsed,unavailable=Unavailable,systemFont=new{path=ttc,size=ttcSize,sha256=ttcSha},clientRoot,fnt=FontFiles(clientRoot)};
    }

    static readonly object gate=new();static bool tried;static IntPtr dc,hfont,oldFont;
    public static string? Unavailable{get;private set;}
    public static string? FaceUsed{get;private set;}
    static void Ensure(){
        lock(gate){
            if(tried)return;tried=true;
            try{
                dc=Gdi.CreateCompatibleDC(IntPtr.Zero);if(dc==IntPtr.Zero){Unavailable="CreateCompatibleDC failed";return;}
                hfont=Gdi.CreateFontW(Height,0,0,0,Weight,0,0,0,CharSet,0,0,Quality,0,Face);if(hfont==IntPtr.Zero){Unavailable="CreateFont MingLiU failed";return;}
                oldFont=Gdi.SelectObject(dc,hfont);var sb=new System.Text.StringBuilder(64);Gdi.GetTextFaceW(dc,sb.Capacity,sb);FaceUsed=sb.ToString();
                if(!IsMingLiU(FaceUsed))Unavailable="GDI mapped MingLiU to '"+FaceUsed+"'";
            }catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or ExternalException){Unavailable="GDI unavailable: "+e.Message;}
        }
    }
    /// <summary>以原版 CreateFont 參數 1× 光柵化；字型不是 MingLiU 或 GDI 失敗時為 null。</summary>
    public static NameGlyphMask? Rasterize(string text){
        Ensure();
        lock(gate){
            if(Unavailable!=null)return null;
            if(text.Length==0)return new(0,Height,[]);
            if(!Gdi.GetTextExtentPoint32W(dc,text,text.Length,out var size)||size.Cx<=0||size.Cy<=0||size.Cx>4096)return null;
            var bmi=new Gdi.BitmapInfoHeader{Size=40,Width=size.Cx,Height=-size.Cy,Planes=1,BitCount=32};
            var dib=Gdi.CreateDIBSection(dc,ref bmi,0,out var bits,IntPtr.Zero,0);if(dib==IntPtr.Zero||bits==IntPtr.Zero)return null;
            var old=Gdi.SelectObject(dc,dib);
            try{
                Gdi.SetBkMode(dc,2);Gdi.SetBkColor(dc,0);Gdi.SetTextColor(dc,0xFFFFFF);Gdi.SetTextAlign(dc,0);
                if(!Gdi.TextOutW(dc,0,0,text,text.Length))return null;Gdi.GdiFlush();
                var raw=new int[size.Cx*size.Cy];Marshal.Copy(bits,raw,0,raw.Length);var ink=new bool[raw.Length];
                for(int i=0;i<raw.Length;i++){int p=raw[i];ink[i]=Math.Max(p&255,Math.Max(p>>8&255,p>>16&255))>=128;}
                return new(size.Cx,size.Cy,ink);
            }finally{Gdi.SelectObject(dc,old);Gdi.DeleteObject(dib);}
        }
    }
    static readonly Dictionary<(string,int,int),Bitmap?> cache=new();
    /// <summary>合成好的名稱圖（1×，含 1 px 描邊邊框）；不可用時為 null。依（字串、本色、描邊色）快取。</summary>
    public static Bitmap? Glyphs(string text,Color fill,Color outline){
        var key=(text,fill.ToArgb(),outline.ToArgb());
        lock(gate){if(cache.TryGetValue(key,out var hit))return hit;}
        Bitmap? bmp=null;
        if(Rasterize(text) is {} mask){
            var px=Compose(mask,key.Item2,key.Item3,out int w,out int h);
            bmp=new Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data=bmp.LockBits(new Rectangle(0,0,w,h),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(px,y*w,data.Scan0+y*data.Stride,w);}finally{bmp.UnlockBits(data);}
        }
        lock(gate){
            if(cache.Count>=CacheLimit){foreach(var b in cache.Values)b?.Dispose();cache.Clear();}
            if(cache.TryGetValue(key,out var raced)){bmp?.Dispose();return raced;}
            cache[key]=bmp;return bmp;
        }
    }
    /// <summary>在 (x, y)（TextOut 原點）畫名稱：圖含 1 px 邊框，所以往左上退 1 px×倍率；最近鄰放大。畫不了回傳 false。</summary>
    public static bool Draw(Graphics g,string text,Color fill,Color outline,float x,float y,float zoom){
        if(Glyphs(text,fill,outline) is not {} bmp)return false;
        var state=g.Save();
        try{
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(bmp,x-Border*zoom,y-Border*zoom,bmp.Width*zoom,bmp.Height*zoom);
        }finally{g.Restore(state);}
        return true;
    }
    static class Gdi {
        [StructLayout(LayoutKind.Sequential)] public struct Size32{public int Cx,Cy;}
        [StructLayout(LayoutKind.Sequential)] public struct BitmapInfoHeader{public int Size,Width,Height;public short Planes,BitCount;public int Compression,SizeImage,XPelsPerMeter,YPelsPerMeter,ClrUsed,ClrImportant;}
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] public static extern IntPtr CreateFontW(int h,int w,int esc,int orient,int weight,uint italic,uint underline,uint strike,uint charSet,uint outPrecision,uint clipPrecision,uint quality,uint pitchAndFamily,string face);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] public static extern int GetTextFaceW(IntPtr hdc,int count,System.Text.StringBuilder face);
        [DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] public static extern bool GetTextExtentPoint32W(IntPtr hdc,string text,int count,out Size32 size);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc,ref BitmapInfoHeader bmi,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll")] public static extern int SetBkMode(IntPtr hdc,int mode);
        [DllImport("gdi32.dll")] public static extern uint SetBkColor(IntPtr hdc,uint color);
        [DllImport("gdi32.dll")] public static extern uint SetTextColor(IntPtr hdc,uint color);
        [DllImport("gdi32.dll")] public static extern uint SetTextAlign(IntPtr hdc,uint align);
        [DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] public static extern bool TextOutW(IntPtr hdc,int x,int y,string text,int count);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
    }
}
public sealed partial class WorldSession {
    internal (string Clan,uint ClanId) ClanIdentity(uint id)=>Actor?.ObjectId==id?(Actor.Clan,Actor.ClanId):objects.TryGetValue(id,out var o)?(o.Clan,o.ClanId):("",0u);
    internal bool IsPartyMemberId(uint id)=>id!=0&&partyMembers.Values.Any(m=>m.ObjectId==id);
    internal bool IsPartyLeaderId(uint id)=>id!=0&&partyLeader.Length>0&&partyMembers.TryGetValue(partyLeader,out var m)&&m.ObjectId==id;
    internal bool InvisibleFlagOf(uint id)=>objects.TryGetValue(id,out var o)&&(o.Flags&2)!=0;
    internal int GfxOf(uint id)=>Actor?.ObjectId==id?Actor.Gfx:objects.TryGetValue(id,out var o)?o.Gfx:-1;
    // 3.81 封包能提供的部分：+248 主人名＝S87 owner 欄；+252 行＝個人商店標題（AN§2.1 推測）；等級差＝S87 等級 − 自己等級。
    // +181 與物件種類在 3.81 S87 沒有對應欄位（需先查證），一律留空，對應規則不啟用。
    internal NameMarks NameMarksFor(uint id){
        if(Actor?.ObjectId==id||!objects.TryGetValue(id,out var o))return new();
        return new(Master:o.Owner.Length>0,Line252:o.ShopActive?o.ShopText:"",LevelDiff:Status is {} s?o.Level-s.Level:null);
    }
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
            // MingLiU 12 px, weight 400; outline colour palette[0]（或等級差色）at +-1 px, then the name colour (+204).
            // T290（FU5§1）：隊伍／主人圖示畫 surf（sub_E538D0 → sub_542370，畫在 (x, y)，不縮放以外的處理）。
            // 原版缺 surf 會跳「Surf[%d] Error.」並結束程式；引擎改成缺圖不畫（本機 8264.img 沒有，隊長圖示因此不會出現）。
            foreach(var icon in plan.Icons){
                if(icon.Surf<=0||NameIconSurface(icon.Surf) is not {} surface)continue;
                var p=Camera.ToView(icon.Position);g.DrawImage(surface.Bitmap,p.X,p.Y,surface.Width*zoom,surface.Height*zoom);
            }
            // T321：先走原版 GDI（NativeNameFont，1× 光柵化後最近鄰放大）；MingLiU 不可用時才退回 GDI+ DrawString 備援。
            ReportNameFont();Font? font=null;var format=StringFormat.GenericTypographic;
            try{
                foreach(var t in plan.Texts){
                    var p=Camera.ToView(t.Position);var fill=NameColors.ToColor(dim?AfflictionColors.Darken(t.Color):t.Color);var edge=NameColors.ToColor(t.Outline);
                    if(NativeNameFont.Draw(g,t.Text,fill,edge,p.X,p.Y,zoom))continue;
                    font??=new Font(NativeNameFont.Face,NameLayout.FontPixels*zoom,FontStyle.Regular,GraphicsUnit.Pixel,NativeNameFont.CharSet);
                    using var brush=new SolidBrush(fill);using var outline=new SolidBrush(edge);
                    foreach(var (dx,dy) in NativeNameFont.OutlineOffsets)g.DrawString(t.Text,font,outline,p.X+dx*zoom,p.Y+dy*zoom,format);
                    g.DrawString(t.Text,font,brush,p.X,p.Y,format);
                }
            }finally{font?.Dispose();}
        }finally{g.Restore(state);}
    }
    // 名稱字型來源只記一次（GDI 對應到的字面、mingliu.ttc 與 Font\*.fnt 的 SHA-256）；雜湊在背景做，不卡繪製。
    bool nameFontReported;
    void ReportNameFont(){
        if(nameFontReported)return;nameFontReported=true;string root=P06.NativeUi.UiResourcePins.ClientRoot;
        Task.Run(()=>NativeNameFont.Describe(root)).ContinueWith(task=>{
            if(task.IsCompletedSuccessfully&&IsHandleCreated&&!IsDisposed)try{BeginInvoke(()=>events.Add(new{kind="name-font",source=task.Result}));}catch(InvalidOperationException){}
        },TaskScheduler.Default);
    }
    // 名稱旁圖示的 surf：執行期從原客戶端 Sprite*.pak 讀（SurfList.xml 對照，與原生 UI 同一份目錄），讀不到就不畫。
    static bool nameIconResourcesTried;static P06.NativeUi.SurfCatalog? nameIconSurfs;
    static P06.NativeUi.UiSurface? NameIconSurface(int surf){
        if(!nameIconResourcesTried){
            nameIconResourcesTried=true;
            try{nameIconSurfs=P06.NativeUi.UiResources.Shared.Surfs;}
            catch(Exception e){nameIconSurfs=null;System.Diagnostics.Trace.WriteLine("[name-icons] client surf catalog unavailable: "+e.Message);}
        }
        try{return nameIconSurfs?.Get(surf);}
        catch(Exception e) when(e is IOException or InvalidDataException or P06.NativeUi.UiResourceException or P06.NativeUi.UiLayoutException){return null;}
    }
    // 城主王冠（sub_F08880）：圖源讀不到時不畫、不退回替代圖，只記一次事件。
    static CastleCrownImages? crownImages;static bool crownLoadTried;static Bitmap?[]? crownBitmaps;
    static Bitmap? CrownBitmap(int frame){
        if(!crownLoadTried){crownLoadTried=true;try{crownImages=CastleCrownImages.Load();}catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException){crownImages=null;CrownLoadError=e.Message;}}
        if(crownImages is not {} images||(uint)frame>=CastleProtocol.CrownFrames)return null;
        crownBitmaps??=new Bitmap?[CastleProtocol.CrownFrames];
        if(crownBitmaps[frame] is {} cached)return cached;
        var b=new Bitmap(CastleCrownImages.Width,CastleCrownImages.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var px=images.Frame(frame);
        for(int y=0;y<CastleCrownImages.Height;y++)for(int x=0;x<CastleCrownImages.Width;x++){ushort c=px[y*CastleCrownImages.Width+x];b.SetPixel(x,y,c==0?Color.Transparent:NameColors.ToColor(c));}
        return crownBitmaps[frame]=b;
    }
    static string? CrownLoadError;
    void PaintCrowns(Graphics g){
        if(!login.World.Renderable)return;
        var targets=new List<CrownTarget>();
        if(login.World.Actor is {} a&&renderer.OwnCombatVisual is {} own)targets.Add(new(a.ObjectId,a.Gfx,own.X,own.Y,true));
        foreach(var v in objectVisuals)if(v.Action!="ground"&&objectPatch?.VisibleObjectIds.Contains(v.ObjectId)==true)targets.Add(new(v.ObjectId,v.Gfx,v.X,v.Y,false));
        var plan=CrownLayout.Plan(login.World,targets);if(plan.Length==0)return;
        float zoom=Camera.Zoom;var state=g.Save();
        try{
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            foreach(var c in plan){
                var p=Camera.ToView(c.Position);if(!CrownLayout.Fits(p,zoom,ClientSize,c.Self)||CrownBitmap(c.Frame) is not {} bitmap)continue;
                g.DrawImage(bitmap,p.X,p.Y,CastleCrownImages.Width*zoom,CastleCrownImages.Height*zoom);
            }
        }finally{g.Restore(state);}
        if(CrownLoadError is {} error){CrownLoadError=null;events.Add(new{kind="crown-unavailable",error});}
    }
    void PaintObjectSelection(Graphics g){
        if(login.World.TargetId is uint id&&objectVisuals.FirstOrDefault(v=>v.ObjectId==id) is {} v){var p=Camera.ToView(v.Foot);using var pen=new Pen(Color.Gold,2);g.DrawEllipse(pen,p.X-19*Camera.Zoom,p.Y-7*Camera.Zoom,38*Camera.Zoom,14*Camera.Zoom);}
        foreach(var groundVisual in objectVisuals.Where(v=>v.Action=="ground")){var pt=Camera.ToView(groundVisual.Foot);bool selected=login.World.SelectedGroundId==groundVisual.ObjectId;g.DrawString(groundVisual.Name,Font,selected?Brushes.Gold:Brushes.White,pt.X-20,pt.Y+17);if(selected){using var pen=new Pen(Color.Gold,2);g.DrawEllipse(pen,pt.X-16,pt.Y+3,32,12);}}
        PaintObjectPresence(g,Camera.ToView,new RectangleF(Camera.ViewOffset,Camera.View));
        PaintCrowns(g);
        PaintNames(g);
        PaintDisplayNotices(g);
    }
    object ObjectUiSnapshot()=>new{ownCombatVisual=renderer.OwnCombatVisual,magic=renderer.MagicVisuals,subEffects=renderer.SubEffectVisuals,subEffectMissingGfx=renderer.SubEffects.Missing,subEffectDropped=renderer.SubEffects.Dropped,fades=renderer.FadeAlphas(Environment.TickCount64),projectile=renderer.CurrentProjectileVisual,projectiles=renderer.ProjectileVisuals,visuals=objectVisuals,presence=PresenceSnapshot(),missing=missingObjects.Select(o=>new{o.ObjectId,o.Gfx,o.Action,o.Speed,o.Brave,o.ReceivedAction,o.Name,o.X,o.Y,o.ShopActive}),target=login.World.Target,targetName=login.World.Target is {} t?objectAssets?.DisplayName(t):null,world=login.World.Snapshot()};
}
