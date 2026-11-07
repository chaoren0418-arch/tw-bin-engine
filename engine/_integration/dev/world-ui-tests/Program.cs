using P06;
using System.Buffers.Binary;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

// Offline product DLL regression. No product entry point, ClientForm constructor,
// login/socket, server, DB, audio device, or visible window is started.
class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> passed=[];
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed.Add(name);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,F)!.GetValue(o)!;
    static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F)!.Invoke(o,args);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static byte[] Pack(uint id,ushort x=32600,ushort y=32940,byte action=0,ushort gfx=0)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write(gfx);b.Write(action);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);Text(b,id==123?"offline":"peer");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] Move(uint id,ushort x,ushort y,byte h)=>Packet(b=>{b.Write((byte)10);b.Write(id);b.Write(x);b.Write(y);b.Write(h);b.Write((byte)128);});
    static byte[] Remove(uint id)=>LoginPackets.Pad([120,..BitConverter.GetBytes(id)]);
    static byte[] Dialog(uint id)=>Packet(b=>{b.Write((byte)39);b.Write(id);Text(b,"offline-page");b.Write((ushort)0);b.Write((ushort)0);});
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static void SeedWorld(LoginConnection c){
        Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        Feed(c,Pack(123));Feed(c,Pack(456,32601));
    }
    static LoginConnection Seed(){var c=new LoginConnection();SeedWorld(c);return c;}
    static void Sender(LoginConnection c,Func<byte[],Func<bool>,Action,Task<bool>> send)=>Set(c,"sendMovementGate",send);
    static List<byte[]> Recording(LoginConnection c){var packets=new List<byte[]>();Sender(c,(p,valid,admit)=>{if(!valid())return Task.FromResult(false);packets.Add(p);admit();return Task.FromResult(true);});return packets;}

    static void Lifecycle(){
        using var c=Seed();var w=c.World;w.SelectTarget(456);int rev=w.TargetRevision;
        Feed(c,Pack(456,32602,action:8));Check(w.TargetId==null&&w.Objects.Single().Dead,"S87 death clears selection");
        Feed(c,Pack(456,32603));Check(!w.Objects.Single().Dead&&w.Objects.Single().Combat==null,"fresh S87 revives same id without sticky death");
        w.SelectTarget(456);Feed(c,Move(456,32603,32940,2));Check(w.Objects.Single().X==32604&&w.Objects.Single().MotionAt!=0,"peer move uses source plus heading");
        Feed(c,Remove(456));Check(w.TargetId==null&&w.Objects.Length==0&&w.TargetRevision>rev,"AOI remove clears object and target");
        Feed(c,Move(456,32604,32940,2));Check(w.Objects.Length==0&&w.UnknownObjectUpdates==1,"late delta cannot recreate removed object");
        Feed(c,Pack(456,32601));Check(w.Objects.Single().MotionAt==0&&!w.Objects.Single().Dead&&w.TargetId==null,"AOI reentry starts fresh and does not reselect");
        w.SelectTarget(456);Feed(c,LoginPackets.Pad([76,..BitConverter.GetBytes(456u),0,0,0,0]));
        Check(w.TargetId==null&&w.Objects.Single().AppearancePending,"pending appearance clears selection");w.SelectTarget(456);Check(w.TargetId==null,"pending appearance cannot be selected");
        Feed(c,Pack(456,32601));w.SelectTarget(456);Check(w.TargetId==456,"full pack restores selectable appearance");
        Feed(c,LoginPackets.Pad([206,0,0,0]));Check(w.Objects.Length==0&&w.TargetId==null&&!w.Renderable,"same-map reset clears AOI before own pack");
        Feed(c,Pack(123));Feed(c,Pack(789,32601));w.SelectTarget(789);int oldEpoch=w.WorldEpoch;
        c.Cancel();Check(w.Objects.Length==0&&w.TargetId==null&&w.Actor==null&&w.AuthoritativeActor==null&&w.WorldEpoch!=oldEpoch,"disconnect clears world and authority");
        SeedWorld(c);Check(w.Objects.Length==1&&w.Objects[0].ObjectId==456&&w.TargetId==null,"reconnect has no old objects or selection");
    }
    static void Position(){
        using var c=Seed();var w=c.World;long revision=w.AuthoritativePositionRevision;
        Call(w,"Predict",32603,32940,2,true);
        Check(w.Actor!.X==32603&&w.AuthoritativeActor!.X==32600&&w.AuthoritativePositionRevision==revision,"prediction does not invent own ACK");
        Check(w.PositionDisplayText.Contains("x 32603")&&w.PositionDisplayText.Contains("本地預測")&&w.PositionDisplayText.Contains("x 32600"),"HUD shows current prediction and last authority distinctly");
        for(byte h=0;h<8;h++){Feed(c,Move(123,32600,32940,h));Check(w.AuthoritativeActor!.X==32600+MovementRules.Dx[h]&&w.AuthoritativeActor.Y==32940+MovementRules.Dy[h]&&!w.PositionPredictedSinceAuthority,"own S10 heading "+h);}
        Feed(c,Pack(123,32605));Check(w.Actor!.X==32605&&w.AuthoritativeActor!.X==32605&&!w.PositionPredictedSinceAuthority,"S87 correction wins over prediction");
        Check(JsonSerializer.Serialize(w.AuthoritativePositionSnapshot()).Contains("displayText"),"Web position includes same display contract");
    }
    static (ushort X,ushort Y,int H) OpenEdge(){
        for(ushort y=32920;y<32960;y++)for(ushort x=32580;x<32620;x++)for(int h=0;h<8;h++)
            if(PlayerPointerPath.Step(new(x,y),h,MovementRules.Passable))return(x,y,h);
        throw new Exception("no local open edge");
    }
    static async Task Pointer(){
        using var c=Seed();var edge=OpenEdge();Feed(c,Remove(456));Feed(c,Pack(123,edge.X,edge.Y));
        bool focus=true;long now=10000;Set(c,"MovementClock",(Func<long>)(()=>now));var packets=Recording(c);
        var p=new PlayerPointerController(c,()=>focus,()=>false,_=>true,()=>now);
        var goal=new Point(edge.X+MovementRules.Dx[edge.H],edge.Y+MovementRules.Dy[edge.H]);
        Check(p.MoveTo(goal),"click ground plans open edge");await p.TickAsync();
        Check(packets.Count==1&&packets[0][0]==29&&packets[0][5]==edge.H&&c.World.Actor!.X==goal.X&&c.World.Actor.Y==goal.Y&&!p.Active,"click ground sends one exact C29 and reaches predicted destination");
        now+=10000;Feed(c,Pack(123,edge.X,edge.Y));Check(p.MoveTo(goal),"prepare cancel route");p.Cancel("Esc");await p.TickAsync();Check(packets.Count==1,"Esc sends no queued route");
        Check(p.MoveTo(goal),"prepare focus route");focus=false;await p.TickAsync();Check(packets.Count==1&&!p.Active,"focus loss stops route");focus=true;
        Check(p.MoveTo(goal),"prepare occupancy route");Feed(c,Pack(456,(ushort)goal.X,(ushort)goal.Y));await p.TickAsync();Check(packets.Count==1&&!p.Active,"new object blocks planned route before send");
        Feed(c,Remove(456));Check(p.MoveTo(goal),"prepare correction route");Feed(c,Pack(123,edge.X,edge.Y));await p.TickAsync();Check(packets.Count==1&&!p.Active,"own correction cancels queued route");
        Check(PlayerPointerPath.Find(new(0,0),new(65,0),(_,_,_)=>true)==null,"route is distance bounded");
        Check(!PlayerPointerPath.Step(new(0,0),1,(_,_,h)=>h==1),"diagonal cannot cut blocked corners");
    }
    static async Task Actions(){
        foreach(bool talk in new[]{false,true}){
            using var c=Seed();var packets=Recording(c);var p=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(p.Select(456)&&packets.Count==0,"single select does not act "+talk);
            Check(await p.ActAsync(456,talk)&&packets.Count==1&&packets[0][0]==(talk?34:229)&&BinaryPrimitives.ReadUInt32LittleEndian(packets[0].AsSpan(1))==456,"explicit pointer action sends one packet "+talk);
            if(talk){Feed(c,Dialog(456));Check(c.World.InteractionKind=="dialog","C34 then S39 opens server-driven dialog");Feed(c,Remove(456));Feed(c,Dialog(456));Check(c.World.InteractionKind=="closed","late S39 cannot reopen removed NPC");}
        }
        foreach(ushort distance in new ushort[]{4,5,6}){
            using var c=Seed();Feed(c,Pack(456,(ushort)(32600+distance)));var packets=Recording(c);var p=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            bool sent=await p.ActAsync(456,true);Check(sent==(distance<=5)&&packets.Count==(distance<=5?1:0),"NPC server distance boundary "+distance);
            if(sent){Feed(c,Dialog(456));Check(c.World.InteractionKind=="dialog","NPC reply accepted at distance "+distance);}
        }
        foreach(string change in new[]{"remove-reappear","full-pack","focus","disconnect"})foreach(bool talk in new[]{false,true}){
            using var c=Seed();bool focus=true;int sent=0;var release=new TaskCompletionSource<bool>();
            Sender(c,async(_,valid,admit)=>{await release.Task;if(!valid())return false;sent++;admit();return true;});
            var p=new PlayerPointerController(c,()=>focus,()=>false,_=>true);var pending=p.ActAsync(456,talk);
            Check(!pending.IsCompleted,"pending pointer action "+change+talk);
            if(change=="remove-reappear"){Feed(c,Remove(456));Feed(c,Pack(456,32601));c.World.SelectTarget(456);}
            if(change=="full-pack")Feed(c,Pack(456,32601));
            if(change=="focus")focus=false;
            if(change=="disconnect"){c.Cancel();SeedWorld(c);}
            release.SetResult(true);Check(!await pending&&sent==0,"stale pointer action never sends "+change+talk);
        }
    }
    static PlayerHudSnapshot HudState(PlayerHudStamp stamp,bool busy=false)=>new(stamp,"offline","position",55,100,20,40,"ready","",busy,"peer",false,
        [new(new(PlayerHudKind.Item,900),"item","×1",2,false,true)],[new(new(PlayerHudKind.Magic,4),"spell","MP 3",null,false,true)]);
    static void Hud(){
        using var c=Seed();var stamp=PlayerHudActions.Stamp(c);var state=HudState(stamp);var bindings=new PlayerHudSlots();bindings.Observe(stamp);bindings.PopulateDefaults(state);
        Check(bindings.Resolve(0,state)?.Binding.Id==900&&bindings.Resolve(PlayerHudKeys.ItemDefaultSlots,state)?.Binding.Id==4,"HUD defaults bind items and learned skills");
        Check(bindings.Resolve(0,state with{Items=[]})==null,"removed inventory item disables quickslot");
        bindings.Assign(0,null);bindings.PopulateDefaults(state);Check(bindings.Resolve(0,state)==null,"explicitly cleared quickslot stays clear");
        bindings.Assign(0,new(PlayerHudKind.Item,900));bindings.Observe(stamp with{Actor=789});Check(bindings.Bindings.All(x=>x==null),"quickslots isolated by actor");bindings.Observe(stamp);Check(bindings.Resolve(0,state)!=null,"same actor retains explicit binding");
        using var view=new PlayerHudView{Size=new(1000,434)};int actions=0;view.ActivateEntry+=(_,_)=>actions++;
        view.UpdateState(state,_=>null);view.ActivateSlot(0);Check(actions==1,"native HUD one slot activation");view.UpdateState(state with{Busy=true},_=>null);view.ActivateSlot(0);Check(actions==1,"native HUD busy prevents activation");
        view.UpdateState(state with{Items=[]},_=>null);view.ActivateSlot(0);Check(actions==1,"native HUD missing item prevents activation");
        var vitals=Get<PlayerHudVitals>(view,"vitals");Check(vitals.AccessibleName!.Contains("HP 55/100")&&vitals.AccessibleName.Contains("MP 20/40"),"HUD vitals display actual received values");
        foreach(int width in new[]{1000,1400})foreach(bool drawer in new[]{false,true}){
            var bounds=PlayerHudGeometry.Scene(new(width,800),320,true,drawer);
            Check(bounds.Right==width-320&&bounds.Bottom==800-198-(drawer?236:0),"scene excludes HUD and F9 pane "+width+drawer);
            Check(!ClientForm.PlayerPointerInScene(new(bounds.Right,20),bounds,false,true)&&!ClientForm.PlayerPointerInScene(new(10,10),bounds,true,true),"scene boundary/covered control rejects click "+width+drawer);
        }
        c.Cancel();Check(!PlayerHudActions.Current(c,stamp),"old HUD stamp invalid after disconnect");
    }
    static void Focus(Assets assets){
        using var c=Seed();var form=(ClientForm)RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));GC.SuppressFinalize(form);
        Set(form,"login",c);Set(form,"<Input>k__BackingField",new InputState());Set(form,"<World>k__BackingField",new World(assets));Set(form,"events",new List<object>());Set(form,"playerHudHeld",new HashSet<Keys>{Keys.F1});
        form.Input.Down(Keys.Up);Call(form,"ClearHeldInput","chat-focus");
        Check(form.Input.Held.Count==0&&Get<HashSet<Keys>>(form,"playerHudHeld").Count==0,"offline ClientForm focus clear releases movement and quickslot holds");
        var owns=typeof(ClientForm).GetMethod("OwnsGameKeyboardKey",BindingFlags.Static|BindingFlags.NonPublic,null,[typeof(Keys),typeof(bool),typeof(bool)],null)!;
        foreach(Keys key in new[]{Keys.Up,Keys.W,Keys.G,Keys.F1})Check(!(bool)owns.Invoke(null,[key,false,true])!&&!(bool)owns.Invoke(null,[key,true,false])!,"editor/web focus blocks game key "+key);
        var loginEnter=typeof(ClientForm).GetMethod("LoginEnterDisposition",BindingFlags.Static|BindingFlags.NonPublic)!;
        Check((int)loginEnter.Invoke(null,[Keys.Enter,true,true,true,true,false])! ==2,"login fresh Enter can activate once");
        Check((int)loginEnter.Invoke(null,[Keys.Enter,true,true,true,true,true])! ==1,"login held Enter is consumed without resubmission");
        Check((int)loginEnter.Invoke(null,[Keys.Enter,true,true,false,true,false])! ==1,"login unavailable Enter is consumed");
        Check((int)loginEnter.Invoke(null,[Keys.Enter,true,false,true,true,false])! ==0,"world Enter does not activate credential login");
        var camera=new Camera{View=new(680,366),ViewOffset=new(0,140)};camera.SetCenter(Coordinates.Screen(32600,32940));
        foreach(float factor in new[]{1f,1.15f,1/1.15f}){camera.Scale(factor,new(300,200));var scene=Coordinates.Screen(32601,32941);var picked=camera.ToScene(camera.ToView(scene));Check(Coordinates.Pick(picked.X,picked.Y)==new Point(32601,32941),"viewport/zoom pointer inverse "+factor);}
        var enter=new PlayerChatEnterPress();enter.Open(true);Check(!enter.Down(Keys.None),"opening Enter repeat cannot submit chat");enter.Release();Check(enter.Down(Keys.None)&&!enter.Down(Keys.None),"one later Enter submits once");
        using var chat=new PlayerHudChatPanel();chat.UpdateChat(new(1,1,123,0,true,"",[]));var input=Get<TextBox>(chat,"input");input.Text="draft";chat.UpdateChat(new(1,1,123,1,true,"",[new(1,123,"server echo")]));
        Check(input.Text=="draft"&&Get<RichTextBox>(chat,"history").Text=="server echo","chat keeps draft and displays only server echo");chat.UpdateChat(new(2,2,456,0,true,"",[]));Check(input.Text==""&&Get<RichTextBox>(chat,"history").Text=="","chat identity change clears draft/history");
    }
    static void Render(Assets assets){
        using var c=Seed();using var renderer=new Renderer(assets);var catalog=new ObjectAssets(assets.Root);var own=new World(assets);own.Teleport(32600,32940);
        var patch=renderer.ComposeObjects(own,c.World,catalog,out var visuals,out _);
        Check(visuals.Any(v=>v.ObjectId==456)&&patch.Commands.Any(d=>d.Layer=="shadow"),"offline renderer composes peer and original shadow");
        Feed(c,Remove(456));patch=renderer.ComposeObjects(own,c.World,catalog,out visuals,out _);Check(visuals.All(v=>v.ObjectId!=456)&&!patch.VisibleObjectIds.Contains(456),"removed object has no rendered pixels/selection owner");
        Feed(c,Pack(456,32601));patch=renderer.ComposeObjects(own,c.World,catalog,out visuals,out _);Check(visuals.Any(v=>v.ObjectId==456),"AOI reentry is drawn again");
        var shadow=new Draw(0,0,0,1,"shadow",0,-1,-1,new Tile(2,[new(0,0,0)]),456);var rect=new Rectangle(shadow.Px,shadow.Py,1,1);var colors=new ushort[]{32767};var mask=new bool[1];var ids=new uint?[]{789};
        Renderer.Blit(shadow,rect,colors,mask,null,ids);Check(colors[0]==Renderer.Blend(0,32767)&&mask[0]&&ids[0]==789,"shadow half-blends and preserves underlying pick owner");
        ids[0]=null;Renderer.Blit(shadow,rect,colors,mask,null,ids);Check(ids[0]==null,"shadow never becomes a click target");
        var shadows=new PlayerShadowCatalog(assets.Root);Check(shadows.Get(65535,65535,"missing")==null,"unknown appearance does not borrow shadow");
    }
    static void Icons(string root){
        var pins=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"engine/_integration/dev/local-resource-tests/icon-pins.json")))!;
        foreach(var (id,sha) in pins){var r=WebLocalResource.Read(Path.Combine(root,"engine"),$"https://p08.example/icons/{id}.png");Check(r.Status==200&&r.Headers.Contains("image/png")&&Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(r.Body!)).Equals(sha,StringComparison.OrdinalIgnoreCase),"F9 original icon bytes "+id);}
        Check(WebLocalResource.Read(Path.Combine(root,"engine"),"https://p08.example/icons/65536.png").Status==404,"F9 invalid icon returns 404");
    }
    [STAThread] static int Main(string[] args){try{
        string root=Path.GetFullPath(args[0]);AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(root,"engine/build/offline-checks")+Path.DirectorySeparatorChar);
        using var guard=new SocketGuard();var assets=new Assets(Path.Combine(root,"engine/assets"));
        Lifecycle();Position();Pointer().GetAwaiter().GetResult();Actions().GetAwaiter().GetResult();Hud();Focus(assets);Render(assets);Icons(root);
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{status="pass",checks=passed.Count,scope="產品 DLL、合成封包、記憶體 sender、離線 ClientForm handlers／HUD／renderer；非原生驗收",labels=passed},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {passed.Count} world/UI checks");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    sealed class SocketGuard:EventListener {protected override void OnEventSourceCreated(EventSource s){if(s.Name=="System.Net.Sockets")EnableEvents(s,EventLevel.LogAlways);}protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Socket activity forbidden in offline checks");}
}
