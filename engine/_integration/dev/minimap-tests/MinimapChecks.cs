using P06;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

partial class Checks {
    // Only synthetic, memory-owned cache data. No asset directory or store loader is used.
    static OriginalMapCache MinimapCache(int revision=1){
        var store=(OriginalMapStore)RuntimeHelpers.GetUninitializedObject(typeof(OriginalMapStore));
        typeof(OriginalMapStore).GetField("<Bounds>k__BackingField",F)!.SetValue(store,new MapBounds(32576,32896,32703,33023));
        var cache=(OriginalMapCache)RuntimeHelpers.GetUninitializedObject(typeof(OriginalMapCache));
        Set(cache,"store",store);SetCacheCollections(cache);Set(cache,"OriginX",32384);Set(cache,"OriginY",32896);Set(cache,"Revision",revision);
        var tile=new Tile(0,Enumerable.Range(0,576).Select(i=>new Pixel(i%24,i/24,0x3e0)).ToArray());
        Set(cache,"Scene",new Draw[]{new(48,44,0,1,"ground",0,0,0,tile),new(50,44,0,2,"ground",0,0,0,tile)});
        return cache;
    }
    // GetUninitializedObject skips initializers; OriginalMapCache.Snapshot() reads these on every ObserveOpacity.
    static void SetCacheCollections(OriginalMapCache cache){
        typeof(OriginalMapCache).GetField("<Slots>k__BackingField",F)!.SetValue(cache,new OriginalMapBlock?[4]);
        typeof(OriginalMapCache).GetField("<Events>k__BackingField",F)!.SetValue(cache,new List<object>());
    }
    static void BindMinimap(WorldSession world,OriginalMapCache cache){
        Set(world,"MapCache",cache);Set(world,"MapResourceState","ready");
        Set(world,"sceneOpacity",new SceneOpacity(new OpacityCatalog([],[],[])));
    }
    static byte[] MinimapPack(uint id,byte flags=0,ushort x=32601,ushort y=32940)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);
        Text(b,"NPC");Text(b,"");b.Write(flags);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    [CheckGroup("minimap"),CheckGroup("hotbar",Order=3),CheckGroup("map-continuity",Order=270),CheckGroup(CheckGroups.Default,Order=130)]
    static void MinimapChecks(){
        using(var connection=Seed())using(var model=new Minimap()){
            Sender(connection,(_,_,_)=>throw new Exception("minimap must not send C packets"));
            var world=connection.World;var cache=MinimapCache();BindMinimap(world,cache);
            void Update()=>model.Update(MinimapSnapshot.Capture(world));
            Update();Check(model.Image!=null&&model.Snapshot!.Markers.Count(m=>m.Self)==1,"minimap S87 own position + cached image");
            var original=model.Image;
            Feed(connection,MinimapPack(457,4));Feed(connection,MinimapPack(458,0,32000));GroundPacket(connection);
            Update();Check(ReferenceEquals(original,model.Image),"marker updates reuse thumbnail");
            Check(model.Snapshot!.Markers.Select(m=>m.Id).Order().SequenceEqual(new uint[]{123,456}),"NPC marker excludes PC flags, ground items and outside-cache coordinates");
            for(byte heading=0;heading<8;heading++){
                Feed(connection,Move(456,32601,32940,heading));Update();var marker=model.Snapshot!.Markers.Single(m=>m.Id==456);
                Check(marker.X==32601+MovementRules.Dx[heading]&&marker.Y==32940+MovementRules.Dy[heading],"S10 NPC source plus heading "+heading);
                Feed(connection,Move(123,32600,32940,heading));Update();marker=model.Snapshot!.Markers.Single(m=>m.Self);
                Check(marker.X==32600+MovementRules.Dx[heading]&&marker.Y==32940+MovementRules.Dy[heading],"S10 own source plus heading "+heading);
            }
            Feed(connection,Pack(123));Update();Check(model.Snapshot!.Markers.Single(m=>m.Self).X==32600,"S87 correction replaces predicted/self coordinates");
            var before=model.Snapshot.Markers.Single(m=>m.Id==456);
            Feed(connection,Packet(b=>{b.Write((byte)122);b.Write(456u);b.Write((byte)6);}));Update();
            Check(before==model.Snapshot!.Markers.Single(m=>m.Id==456),"S122 heading leaves minimap position unchanged");
            Feed(connection,Packet(b=>{b.Write((byte)158);b.Write(456u);b.Write((byte)8);}));Update();
            Check(model.Snapshot!.Markers.All(m=>m.Id!=456),"S158 death removes live NPC marker");
            Feed(connection,MinimapPack(456));Update();Check(model.Snapshot!.Markers.Any(m=>m.Id==456),"S87 respawn restores NPC marker");
            before=model.Snapshot!.Markers.Single(m=>m.Id==456);Feed(connection,Poly(456,99,0));Update();
            Check(before==model.Snapshot!.Markers.Single(m=>m.Id==456)&&world.Objects.Single(o=>o.ObjectId==456).Gfx==99,"S76 polymorph keeps the same NPC marker in place");
            Feed(connection,MinimapPack(456));Feed(connection,Packet(b=>{b.Write((byte)120);b.Write(456u);}));Update();
            Check(model.Snapshot!.Markers.All(m=>m.Id!=456),"S120 removal clears NPC marker");
            Set(cache,"Revision",2);Update();Check(!ReferenceEquals(original,model.Image),"cache shift revision rebuilds thumbnail");
            original=model.Image;var replacement=MinimapCache(2);BindMinimap(world,replacement);Update();
            Check(!ReferenceEquals(original,model.Image),"new cache with same map/revision replaces thumbnail");
            original=model.Image;Set(world,"WorldEpoch",world.WorldEpoch+1);Update();Check(!ReferenceEquals(original,model.Image),"same map epoch reset invalidates thumbnail");
            model.ZoomBy(1);model.ZoomBy(1);model.ZoomBy(1);Check(model.Zoom==4,"zoom capped at 4x");
            var source=model.Image!.Viewport(model.Zoom,model.Snapshot!.Markers.Single(m=>m.Self));
            Check(source.Width==model.Image.Bitmap.Width/4f&&source.X>=0&&source.Right<=model.Image.Bitmap.Width,"zoom viewport remains within cached bitmap");
            model.ZoomBy(-1);model.ZoomBy(-1);model.ZoomBy(-1);Check(model.Zoom==1,"zoom capped at 1x");
            model.Toggle();Update();Check(!model.IsOpen&&model.Image==null&&model.Snapshot==null,"closing frees bitmap and snapshot");
            model.Toggle();Update();Check(model.IsOpen&&model.Image!=null,"reopening regenerates current cache");
            Set(world,"MapResourceState","unavailable");Update();Check(model.Image==null&&model.Snapshot==null,"failed resource clears prior image and markers");
            BindMinimap(world,replacement);Set(world,"MapId",(ushort)4);Update();Check(model.Image==null,"mismatched map cannot display old cache");Set(world,"MapId",(ushort)0);
            Update();Feed(connection,Packet(b=>{b.Write((byte)206);b.Write((ushort)0);b.Write((byte)0);}));Update();
            Check(model.Image==null&&model.Snapshot==null,"S206 same-map reset clears bitmap before own actor arrives");
            Feed(connection,Pack(123));BindMinimap(world,MinimapCache());Update();Check(model.Image!=null,"S87 + ready cache restore after map reset");
            Feed(connection,Packet(b=>{b.Write((byte)149);b.Write((ushort)4);b.Write(17u);}));Update();
            Check(model.Image==null,"S149 teleport pending clears old world");
            world.Reset();Update();Check(model.Image==null&&model.Snapshot==null,"logout reset releases all minimap content");
        }
        MinimapParty();
        MinimapPixels();
        // T176: F5 is the first macro slot; the minimap opens only from its menu button.
        Check(typeof(ClientForm).GetMethod("MinimapKeyAllowed",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)==null&&PlayerHudKeys.Slot(Keys.F5)==0,"F5 is no longer a minimap key");
        using(var hud=new PlayerHudView()){
            var menu=hud.Controls.OfType<FlowLayoutPanel>().Single(c=>c.AccessibleName=="玩家功能");
            Check(menu.Controls.OfType<Button>().Count(b=>b.Text=="小地圖")==1&&menu.Controls.OfType<Button>().All(b=>!b.Text.Contains("F5")),"HUD mounts one minimap menu button without F5");
            menu.Controls.OfType<Button>().Single(b=>b.Text=="小地圖").PerformClick();
            Check(!hud.IsDisposed,"detached HUD toggle and disposal require no game/assets");
            using var view=(MinimapUi)Activator.CreateInstance(typeof(MinimapUi),F,null,[hud,menu],null)!;
            var viewModel=(Minimap)typeof(MinimapUi).GetField("model",F)!.GetValue(view)!;
            var cache=MinimapCache();
            viewModel.Update(new(cache,1,0,0,cache.Scene,[new(456,"隊員",32600,32939,false,true),new(123,"self",32600,32940,true)]));
            view.Size=new(272,218);
            using var painted=new Bitmap(view.Width,view.Height);view.DrawToBitmap(painted,view.ClientRectangle);
            bool terrain=false,self=false,mate=false;
            for(int y=34;y<view.Height-27;y++)for(int x=6;x<view.Width-6;x++){
                int color=painted.GetPixel(x,y).ToArgb();terrain|=color==Color.Lime.ToArgb();self|=color==Color.White.ToArgb();mate|=color==Color.Cyan.ToArgb();
            }
            Check(terrain&&self&&mate,"real minimap control paints cached terrain, own and party markers without showing a window");
            view.Dispose();Check(viewModel.Image==null&&viewModel.Snapshot==null,"HUD/control disposal releases rendered minimap");
        }
    }
    static byte[] MinimapPartyRow(byte sub,uint id,string name,uint map,ushort x,ushort y)=>Packet(b=>{b.Write((byte)250);b.Write(sub);if(sub==104)b.Write((byte)1);b.Write(id);Text(b,name);b.Write((byte)75);b.Write(map);b.Write(x);b.Write(y);});
    static byte[] MinimapPartyMove(uint id,uint map,ushort x,ushort y)=>Packet(b=>{b.Write((byte)250);b.Write((byte)110);b.Write((byte)1);b.Write(id);b.Write(map);b.Write(x);b.Write(y);});
    static byte[] MinimapPc(uint id,string name,ushort x,ushort y,byte flags=4)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);
        Text(b,name);Text(b,"");b.Write(flags);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    // Seed() places the player on map 0, so synthetic rows use map 0 for "same map".
    static void MinimapParty(){
        using var connection=Seed();
        Sender(connection,(_,_,_)=>throw new Exception("minimap must not send C packets"));
        var world=connection.World;BindMinimap(world,MinimapCache());Check(world.Objects.All(o=>o.ObjectId is not (900 or 901)),"party fixture IDs start out of view");
        MinimapMarker[] All()=>MinimapSnapshot.Capture(world)!.Markers;
        MinimapMarker[] Mates()=>All().Where(m=>m.Party).ToArray();
        Check(Mates().Length==0,"no party means no party markers");
        Feed(connection,MinimapPc(700,"路人",32602,32940));Check(All().All(m=>m.Id!=700),"PC outside the party is not guessed as a teammate");
        Feed(connection,MinimapPartyRow(105,900,"隊員",0,32605,32941));
        Check(Mates().Single() is {Id:900,Name:"隊員",X:32605,Y:32941,Self:false},"S250/105 row places a teammate out of view");
        Feed(connection,MinimapPartyRow(104,901,"新隊員",0,32606,32942));
        Check(Mates().Select(m=>m.Id).Order().SequenceEqual(new uint[]{900,901}),"S250/104 row adds a second teammate");
        Feed(connection,MinimapPartyMove(900,0,32607,32943));
        Check(Mates().Single(m=>m.Id==900) is {X:32607,Y:32943},"S250/110 moves an out-of-view teammate");
        Feed(connection,MinimapPartyMove(900,4,32607,32943));Check(Mates().All(m=>m.Id!=900),"S250/110 on another map hides the teammate");
        Feed(connection,MinimapPartyMove(900,0,32000,32943));Check(Mates().All(m=>m.Id!=900),"reported position outside the cache has no marker");
        Feed(connection,MinimapPartyMove(900,4,32607,32943));Feed(connection,MinimapPc(900,"隊員",32608,32944));
        Check(Mates().Single(m=>m.Id==900) is {X:32608,Y:32944},"visible teammate uses live object over a stale report from another map");
        Feed(connection,Move(900,32608,32944,2));
        Check(Mates().Single(m=>m.Id==900) is {X:var liveX}&&liveX==32608+MovementRules.Dx[2],"S10 moves a visible teammate immediately");
        Feed(connection,Packet(b=>{b.Write((byte)158);b.Write(900u);b.Write((byte)8);}));Check(Mates().Any(m=>m.Id==900),"dead teammate stays marked");
        Feed(connection,MinimapPartyMove(900,0,32609,32945));Feed(connection,Packet(b=>{b.Write((byte)120);b.Write(900u);}));
        Check(Mates().Single(m=>m.Id==900) is {X:32609,Y:32945},"S120 falls back to the last S250/110 report");
        Feed(connection,MinimapPack(900,0,32603));Check(All().Count(m=>m.Id==900)==1&&All().Single(m=>m.Id==900).Party,"non-PC-flag teammate object is one party marker, not also an NPC");
        Check(All()[^1].Self,"own marker stays last so it paints on top");
        Feed(connection,PartyListPacket("offline","offline 遠方 "));
        Check(Mates().Length==0,"S39 name-only member without object or location has no marker");
        Feed(connection,MinimapPc(800,"遠方",32604,32940));Check(Mates().Single() is {Id:800,X:32604},"S39 name-only member resolves to one visible PC");
        Feed(connection,MinimapPc(801,"遠方",32605,32940));Check(Mates().Length==0,"ambiguous PC name is not guessed as the teammate");
        Feed(connection,MinimapPartyRow(105,900,"隊員",0,32605,32941));Feed(connection,PartyHp(900,255));Check(Mates().All(m=>m.Id!=900),"S237 FF removal clears the marker");
        Feed(connection,MinimapPartyRow(105,900,"隊員",0,32605,32941));Feed(connection,PartyMessage(420,"隊員"));Check(Mates().All(m=>m.Id!=900),"S71/420 departure clears the marker");
        Feed(connection,MinimapPartyRow(105,900,"隊員",0,32605,32941));Feed(connection,PartyMessage(418));Check(Mates().Length==0,"S71/418 dissolve clears all markers");
    }
    static void MinimapPixels(){
        var pixels=Enumerable.Range(0,576).Select(i=>new Pixel(i%24,i/24,(ushort)((i%24)<<10|(i/24)<<5))).ToArray();
        var tile=new Tile(0,pixels);var ground=new Draw(-2,-3,0,1,"ground",0,0,0,tile);
        using(var image=MinimapImage.Create([ground])!){
            Check(image.Bitmap.Size==new Size(24,24),"small cache retains exact pixel dimensions");
            Check(pixels.All(p=>image.Bitmap.GetPixel(p.X,p.Y).ToArgb()==Renderer.Argb(p.Color)),"all RGB555 pixels incl black match cache, with negative scene coordinates");
        }
        var overlay=ground with{Layer="objects",Tile=new Tile(0,[new Pixel(12,12,0x7c00)])};
        using(var image=MinimapImage.Create([ground,overlay])!){
            Check(image.Bitmap.GetPixel(12,12).ToArgb()==Renderer.Argb(0x7c00)&&image.Bitmap.GetPixel(11,12).ToArgb()==Renderer.Argb(pixels[12*24+11].Color),"scene order and sparse transparent coverage preserved");
        }
        Check(MinimapImage.Create([])==null&&MinimapImage.Create([ground with{Tile=new Tile(8,pixels)}])==null,"empty cache and ship transparent ground have no invented pixels");
        var cacheScene=Enumerable.Range(0,32768).Select(i=>ground with{X=i%256,Y=i/256,Serial=i}).ToArray();
        using(var image=MinimapImage.Create(cacheScene)!){
            Check(image.Bitmap.Width<=512&&image.Bitmap.Height<=512,"full 2x2 cache raster allocation bounded to 512 squared");
            var worldPoint=Coordinates.Screen(32600,32940);var p=image.Project(32600,32940);
            Check(Math.Abs(p.X-(worldPoint.X-image.SourceBounds.X)*image.Bitmap.Width/image.SourceBounds.Width)<.001f&&
                Math.Abs(p.Y-(worldPoint.Y-image.SourceBounds.Y)*image.Bitmap.Height/image.SourceBounds.Height)<.001f,"markers share terrain isometric projection");
        }
        // Independent dense composition oracle for a small downsampled scene.
        var scene=new[]{ground,ground with{X=50,Y=20},overlay};
        using(var image=MinimapImage.Create(scene)!){
            var bounds=image.SourceBounds;var colors=new ushort[bounds.Width*bounds.Height];var mask=new bool[colors.Length];
            foreach(var draw in scene)Renderer.Blit(draw,bounds,colors,mask,null);
            bool equal=true;
            for(int y=0;y<image.Bitmap.Height;y++)for(int x=0;x<image.Bitmap.Width;x++){
                int sx=(int)Math.Floor((x+.5)*bounds.Width/image.Bitmap.Width),sy=(int)Math.Floor((y+.5)*bounds.Height/image.Bitmap.Height);
                int index=sy*bounds.Width+sx;
                equal&=image.Bitmap.GetPixel(x,y).ToArgb()==Renderer.MaskedArgb(colors[index],mask[index]);
            }
            Check(equal,"downsampled terrain matches independent dense renderer sample at every pixel");
        }
    }
}
