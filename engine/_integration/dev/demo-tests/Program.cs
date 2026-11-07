using P06;
using System.Reflection;
using System.Text.Json;
using System.Buffers.Binary;
using System.Diagnostics.Tracing;

// Synthetic packets and memory-only transport, executing the built product DLL.
// Never calls the game entry point, creates a connection or shows a window.
partial class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;static List<string> labels=[];
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;labels.Add(label);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F)!.Invoke(o,args);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] bytes)=>c.World.Receive(bytes,"offline");
    static byte[] Pack(uint id,ushort x=32600,ushort y=32940,byte action=0)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)(id==123?0:1110));b.Write(action);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);Text(b,id==123?"offline":"target");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] Move(uint id,ushort x,ushort y,byte h)=>Packet(b=>{b.Write((byte)10);b.Write(id);b.Write(x);b.Write(y);b.Write(h);b.Write((byte)128);});
    static LoginConnection Seed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        Feed(c,Pack(123));Feed(c,Pack(456,32601));c.World.SelectTarget(456);
        return c;
    }
    static void Sender(LoginConnection c,Func<byte[],Func<bool>,Action,Task<bool>> sender)=>Set(c,"sendMovementGate",sender);
    static async Task Combat(){
        foreach(bool pointer in new[]{false,true}){
            using var c=Seed();int sends=0;byte[]? sent=null;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;sent=b;a();return Task.FromResult(true);});
            var controller=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(controller.Select(456)&&controller.Notice.Contains("已選取"),"pointer selection feedback "+pointer);
            Check(pointer?await controller.ActAsync(456,false):await c.Attack(),"selected target attack admitted "+pointer);
            Check(sends==1&&sent![0]==229&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(1))==456,"one C229 target id "+pointer);
            Check(!await c.Attack()&&sends==1&&c.World.CombatNotice.Contains("冷卻"),"cooldown preserved "+pointer);
        }
        using(var c=Seed()){
            int sends=0;bool moved=false;
            Sender(c,(b,v,a)=>{if(!moved){moved=true;Feed(c,Move(456,32601,32940,2));}if(!v())return Task.FromResult(false);sends++;Check(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(5))==32602,"retry uses latest target x");a();return Task.FromResult(true);});
            Check(!await c.Attack()&&sends==0&&c.World.CombatNotice.Contains("目標已移動"),"moving target refusal is explicit");
            Check(await c.Attack()&&sends==1,"explicit manual retry succeeds without changing selection");
        }
        using(var c=Seed()){
            Sender(c,(_,_,_)=>throw new Exception("invalid target reached sender"));c.World.SelectTarget(null);
            Check(!await c.Attack()&&c.World.CombatNotice.Contains("未選取"),"no target message");
            Feed(c,Pack(456,32601,32940,8));c.World.SelectTarget(456);Check(!await c.Attack(),"dead target rejected");
        }
    }
    static async Task Magic(){
        using var c=Seed();var learned=new byte[32];learned[0]=164;learned[1]=32;learned[2]=8;Feed(c,learned);
        Check(c.World.SelectMagic(4),"select learned light arrow");c.World.SelectTarget(null);
        Check(!await c.CastSelectedMagic(),"light arrow requires selected enemy");
        Check(c.World.SelectMagicTarget(456)&&!c.World.EffectiveMagicTargetSelf,"explicit enemy selection");
        int sends=0;bool moved=false;
        Sender(c,(b,v,a)=>{if(!moved){moved=true;Feed(c,Move(456,32601,32940,2));}if(!v())return Task.FromResult(false);sends++;
            Check(b[0]==6&&b[1]==0&&b[2]==3,"C6 skill 4 encoding");Check(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(3))==456,"C6 enemy object id");
            Check(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(7))==32602,"C6 latest enemy x");a();return Task.FromResult(true);});
        Check(!await c.CastSelectedMagic()&&sends==0&&c.World.MagicNotice.Contains("目標已移動"),"spell moving target refusal");
        Check(await c.CastSelectedMagic()&&sends==1,"manual light arrow retry");
        Check(!await c.CastSelectedMagic()&&sends==1,"spell cooldown preserved");
        Check(c.World.Status!.Mp==100,"no predicted MP deduction");
        Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Check(!c.World.SelectMagicTarget(456),"removed enemy cannot be selected");
    }
    static async Task Quantity(){
        using var c=Seed();var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        Feed(c,Packet(b=>{b.Write((byte)15);b.Write(900101u);b.Write(row.InventoryDesc);b.Write(row.WireUseType);b.Write((byte)0);b.Write(row.InventoryIcon);b.Write((byte)1);b.Write(10u);b.Write((byte)0);Text(b,row.CountName(10));b.Write((byte)0);b.Write(new byte[11]);}));
        c.World.SelectItem(900101);var selection=c.World.GroundQuantity(false);int revision=c.World.InventoryContentRevision;
        Sender(c,(_,_,_)=>throw new Exception("invalid quantity reached sender"));
        foreach(var text in new[]{"0","11","-1","2147483648",""}){
            Feed(c,LoginPackets.Pad([71,125,0,0]));Check(c.World.LastServerMessage!=null,"seed old 125 "+text);
            Check(!await c.SendGroundQuantity(false,text,selection),"invalid count blocked "+text);
            Check(c.World.LastServerMessage==null&&c.World.LastServerReply==null,"old 125 cleared "+text);
            Check(c.World.ItemNotice.Contains("數量不合法")&&c.World.GroundNotice==c.World.ItemNotice,"native inventory receives quantity error "+text);
            Check(c.World.InventoryContentRevision==revision&&c.World.CurrentGroundQuantity(false,selection),"error preserves item snapshot "+text);
        }
    }
    static void GroundPacket(LoginConnection c,uint count=10)=>Feed(c,Packet(b=>{
        var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(900102u);b.Write(row.GroundGfx);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);
        Text(b,row.CountName(count));Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    }));
    static async Task PickupCorrection(){
        foreach(bool pickupError in new[]{true,false})foreach(string invalid in new[]{"0","11"}){
            using var c=Seed();GroundPacket(c);Check(c.World.SelectGround(900102),"select ground before correction "+pickupError+invalid);
            var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));GC.SuppressFinalize(form);Set(form,"login",c);
            Call(form,"SyncQuantitySelection",true);
            var snapshot=(GroundQuantitySelection)typeof(ClientForm).GetField("pickupSelection",F)!.GetValue(form)!;
            if(!pickupError){
                var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
                Feed(c,Packet(b=>{b.Write((byte)15);b.Write(900101u);b.Write(row.InventoryDesc);b.Write(row.WireUseType);b.Write((byte)0);b.Write(row.InventoryIcon);b.Write((byte)1);b.Write(10u);b.Write((byte)0);Text(b,row.CountName(10));b.Write((byte)0);b.Write(new byte[11]);}));
                c.World.SelectItem(900101);
            }
            var errorSelection=c.World.GroundQuantity(pickupError);
            int sends=0;byte[]? sent=null;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;sent=b;a();return Task.FromResult(true);});
            Feed(c,LoginPackets.Pad([71,125,0,0]));
            Check(!await c.SendGroundQuantity(pickupError,invalid,errorSelection)&&sends==0,"invalid quantity sends no packet "+pickupError+invalid);
            Check(c.World.GroundNotice.Contains("數量不合法")&&c.World.LastServerMessage==null&&c.World.LastServerReply==null,"quantity notice replaces old 125 "+pickupError+invalid);
            Call(form,"SyncQuantitySelection",true);
            var retained=(GroundQuantitySelection)typeof(ClientForm).GetField("pickupSelection",F)!.GetValue(form)!;
            Check(ReferenceEquals(snapshot,retained),"native UI retains displayed selection "+pickupError+invalid);
            Check(await c.SendGroundQuantity(true,"1",retained),"corrected pickup succeeds without reselection "+pickupError+invalid);
            Check(sends==1&&sent![0]==112&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(5))==900102&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(9))==1,"exactly one C112 with selected id and corrected count "+pickupError+invalid);
            Check(c.World.SelectedGround!.Count==10,"pickup waits for server count "+pickupError+invalid);
        }
        foreach(string change in new[]{"count","aba","remove","reselect","queued"}){
            using var c=Seed();GroundPacket(c);c.World.SelectGround(900102);var snapshot=c.World.GroundQuantity(true);int sends=0;
            Check(!await c.SendGroundQuantity(true,"0",snapshot),"seed quantity error before real change "+change);
            if(change=="count"||change=="aba")GroundPacket(c,9);
            if(change=="aba")GroundPacket(c,10);
            if(change=="remove")Feed(c,Packet(b=>{b.Write((byte)120);b.Write(900102u);}));
            if(change=="reselect")c.World.SelectGround(900102);
            Sender(c,(b,v,a)=>{if(change=="queued")GroundPacket(c,9);if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.SendGroundQuantity(true,"1",snapshot)&&sends==0,"real ground change still rejects stale pickup "+change);
        }
    }
    static void Position(){
        using var c=Seed();var w=c.World;long serial=w.AuthoritativePositionRevision;
        Check(w.AuthoritativeMapId==0&&w.AuthoritativeActor!.X==32600&&w.AuthoritativePositionOpcode==87,"S87 server position");
        Call(w,"Predict",32601,32940,2,true);
        Check(w.Actor!.X==32601&&w.AuthoritativeActor!.X==32600&&w.AuthoritativePositionRevision==serial&&w.PositionPredictedSinceAuthority,"local prediction cannot update authority");
        Check(w.AuthoritativePositionText.Contains("尚無位置回包"),"stale authority is explicit");
        Feed(c,Move(456,32601,32940,2));Check(w.AuthoritativePositionRevision==serial,"peer move cannot update own authority");
        Feed(c,Move(123,32600,32940,2));Check(w.AuthoritativeActor!.X==32601&&w.AuthoritativePositionOpcode==10&&w.AuthoritativePositionRevision==serial+1&&!w.PositionPredictedSinceAuthority,"own S10 updates authority");
        foreach(byte h in Enumerable.Range(0,8)){Feed(c,Move(123,32600,32940,h));Check(w.AuthoritativeActor!.X==32600+MovementRules.Dx[h]&&w.AuthoritativeActor.Y==32940+MovementRules.Dy[h],"server eight-direction coordinate "+h);}
        Feed(c,LoginPackets.Pad([206,4,0,0]));Check(w.AuthoritativeActor==null&&w.AuthoritativeMapId==null,"map transition clears old coordinates");
        Feed(c,Pack(123,32605,32942));Check(w.AuthoritativeMapId==4&&w.AuthoritativeActor!.X==32605,"map plus self pack binds new authority");
        w.Reset();Check(w.AuthoritativeActor==null&&w.AuthoritativePositionText.Contains("等待"),"logout clears authority");
    }
    static void NativeTargetUi(){
        using var c=Seed();
        // Only call the actual target-list sync method on a constructor-free host;
        // real ComboBox, synthetic pixel ownership; no Form/game construction or Show.
        var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
        using var list=new ComboBox();Set(form,"login",c);Set(form,"magicEnemyList",list);
        Set(form,"objectVisuals",Array.Empty<ObjectVisual>());Call(form,"SyncMagicEnemies");
        Check(list.Items.Count==1&&list.SelectedIndex==0,"native empty target list initializes safely");
        Set(form,"objectVisuals",new[]{new ObjectVisual(456,1110,1110,"target",32601,32940,"walk","0",new(),new(),true,640)});
        Set(form,"objectPatch",new Patch(new(0,0,1,1),new ushort[1],new[]{true},new[]{0},[],new uint?[]{456}));
        Call(form,"SyncMagicEnemies");Check(list.Items.Count==2&&list.SelectedIndex==1,"native list selects visible enemy object id");
        object option=list.Items[1]!;Call(form,"SyncMagicEnemies");Check(ReferenceEquals(option,list.Items[1]),"native unchanged list preserves options");
        Feed(c,Pack(456,32601,32940,8));Call(form,"SyncMagicEnemies");Check(list.Items.Count==1,"native dead target filtered");
        var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);Set(form,"playerPointer",pointer);
        pointer.Cancel("single-attack");Call(form,"SetSingleActionNotice","未送出：未選取有效目標");Call(form,"SyncPlayerPointerHud");
        Check((string)typeof(ClientForm).GetField("playerHudNotice",F)!.GetValue(form)! =="未送出：未選取有效目標","pointer cancellation cannot overwrite attack result");
        pointer.Cancel("single-magic");Call(form,"SetSingleActionNotice","單次施法已送出");Call(form,"SyncPlayerPointerHud");
        Check((string)typeof(ClientForm).GetField("playerHudNotice",F)!.GetValue(form)! =="單次施法已送出","pointer cancellation cannot overwrite cast result");
        GC.SuppressFinalize(form);
    }
    static async Task Movement123(){
        var keys=new[]{Keys.Home,Keys.Up,Keys.PageUp,Keys.Right,Keys.PageDown,Keys.Down,Keys.End,Keys.Left};
        var owns=typeof(ClientForm).GetMethod("OwnsGameKeyboardKey",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(Keys),typeof(bool),typeof(bool)},null)!;
        for(int h=0;h<8;h++){
            var input=new InputState();input.Down(keys[h]);Check(input.Direction==h,"eight-key heading "+keys[h]);
            Check((bool)owns.Invoke(null,new object[]{keys[h],true,true})!,"form owns "+keys[h]);
            Check(!(bool)owns.Invoke(null,new object[]{keys[h],true,false})! && !(bool)owns.Invoke(null,new object[]{keys[h],false,true})!,"focus protected "+keys[h]);
            using var c=Seed();int sends=0;byte[]? packet=null;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;packet=b;a();return Task.FromResult(true);});
            (int X,int Y)? open=null,blocked=null;
            for(int y=32920;y<32960 && (open==null||blocked==null);y++)for(int x=32565;x<32610;x++){
                if(!Coordinates.Inside(x,y))continue;
                if(MovementRules.Passable(x,y,h))open??=(x,y);else blocked??=(x,y);
            }
            Check(open!=null&&blocked!=null,"terrain fixture has open and blocked "+keys[h]);
            Feed(c,Pack(123,(ushort)blocked!.Value.X,(ushort)blocked.Value.Y));
            var denied=await c.RequestMovement(h,true,false);
            Check(!denied.Sent&&sends==0&&denied.Notice.Contains("地形"),"blocked terrain sends no C29 "+keys[h]+" / "+denied.Notice);
            Feed(c,Pack(123,(ushort)open!.Value.X,(ushort)open.Value.Y));
            var sent=await c.RequestMovement(input.Direction!.Value,true,false);
            Check(sent.Sent&&sends==1&&packet![0]==29&&packet[5]==h,"open terrain sends exact C29 heading "+keys[h]);
        }
    }
    // Constructor-free ClientForm fixture follows NativeTargetUi: no game entry,
    // Form constructor, window, device, or transport. Exercise product handlers directly.
    static ClientForm KeyboardForm(LoginConnection connection){
        var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
        GC.SuppressFinalize(form);
        Set(form,"login",connection);
        Set(form,"<Input>k__BackingField",new InputState());
        Set(form,"<World>k__BackingField",System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(World)));
        Set(form,"playerHudNotice","");
        Set(form,"lastPlayerPointerHudNotice","");
        return form;
    }
    static string HudNotice(ClientForm form)=>(string)typeof(ClientForm).GetField("playerHudNotice",F)!.GetValue(form)!;
    static void CheckKeyboardRefusal(ClientForm form,string reason,string label){
        Check(HudNotice(form).Contains(reason),label+" HUD reason "+reason);
        Check((long)typeof(ClientForm).GetField("playerHudNoticeUntil",F)!.GetValue(form)!>Environment.TickCount64,label+" HUD notice visible interval");
        Check(form.Input.Held.Count==0,label+" clears held input");
        Call(form,"SyncPlayerPointerHud");
        Check(HudNotice(form).Contains(reason),label+" pointer HUD preserves rejection");
    }
    static (ushort X,ushort Y,int Open) KeyboardTerrain(int blockedHeading){
        for(int y=32920;y<32960;y++)for(int x=Coordinates.MinX;x<32610;x++){
            if(MovementRules.Passable(x,y,blockedHeading))continue;
            for(int open=0;open<8;open++)if(MovementRules.Passable(x,y,open))return ((ushort)x,(ushort)y,open);
        }
        throw new Exception("No blocked/open terrain fixture for "+blockedHeading);
    }
    static readonly Keys[] DirectionKeys=[Keys.Home,Keys.Up,Keys.PageUp,Keys.Right,Keys.PageDown,Keys.Down,Keys.End,Keys.Left];
    static async Task KeyboardMovement123(){
        for(int heading=0;heading<8;heading++){
            using var connection=Seed();var form=KeyboardForm(connection);
            var fixture=KeyboardTerrain(heading);Feed(connection,Pack(123,fixture.X,fixture.Y));
            var packets=new List<byte[]>();long now=10000;
            Set(connection,"MovementClock",(Func<long>)(()=>now));
            Sender(connection,(bytes,current,admitted)=>{
                if(!current())return Task.FromResult(false);
                packets.Add(bytes);admitted();return Task.FromResult(true);
            });
            Call(form,"OnlineDown",DirectionKeys[heading]);
            Check(packets.Count==0,"OnlineDown blocked sends zero C29 "+heading);
            CheckKeyboardRefusal(form,"地形","OnlineDown "+heading);
            Call(form,"OnlineTick");
            Check(packets.Count==0&&form.Input.Held.Count==0,"rejected held does not retry on tick "+heading);
            form.Input.Down(DirectionKeys[heading]);Call(form,"OnlineTick");
            Check(packets.Count==0,"OnlineTick blocked sends zero C29 "+heading);
            CheckKeyboardRefusal(form,"地形","OnlineTick "+heading);
            form.Input.Down(DirectionKeys[heading]);
            await (Task)typeof(ClientForm).GetMethod("MoveFromKeyboard",F)!.Invoke(form,[heading])!;
            CheckKeyboardRefusal(form,"地形","MoveFromKeyboard "+heading);
            Call(form,"OnlineDown",DirectionKeys[fixture.Open]);
            Check(packets.Count==1&&packets[0][0]==29&&packets[0][5]==fixture.Open,"next open key sends exact C29 "+heading);
            Check(form.Input.Direction==fixture.Open,"new key is not combined with stale held "+heading);
            Call(form,"OnlineTick");Check(packets.Count==1,"movement cooldown waits without extra packet "+heading);
            Check(form.Input.Direction==fixture.Open,"movement cooldown retains held intent "+heading);
            now+=10000;Set(form.World,"Walking",true);Call(form,"OnlineTick");
            Check(packets.Count==1&&form.Input.Direction==fixture.Open,"walking animation retains held intent "+heading);
        }
    }
    static void KeyboardBusy123(){
        foreach(string entry in new[]{"OnlineDown","OnlineTick"})foreach(string field in new[]{"combatSending","magicSending","groundSending","itemSending"}){
            using var connection=Seed();var form=KeyboardForm(connection);int sends=0;
            Sender(connection,(_,_,_)=>{sends++;return Task.FromResult(false);});
            Set(connection,field,true);form.Input.Down(Keys.Left);
            if(entry=="OnlineDown")Call(form,entry,Keys.Up);else Call(form,entry);
            string reason=field switch{"combatSending"=>"攻擊","magicSending"=>"施法","groundSending"=>"拾取／丟棄",_=>"道具"};
            Check(sends==0,entry+" busy zero packets "+field);
            CheckKeyboardRefusal(form,reason,entry+" busy "+field);
        }
        foreach(string entry in new[]{"OnlineDown","OnlineTick"}){
            using var connection=Seed();var form=KeyboardForm(connection);int sends=0;
            Sender(connection,(_,_,_)=>{sends++;return Task.FromResult(false);});
            Set(connection.World,"OwnDead",true);form.Input.Down(Keys.Left);
            if(entry=="OnlineDown")Call(form,entry,Keys.Up);else Call(form,entry);
            Check(sends==0,entry+" unavailable zero packets");CheckKeyboardRefusal(form,"死亡",entry+" unavailable");
        }
    }
    static async Task KeyboardWorld123(){
        using var connection=Seed();var form=KeyboardForm(connection);var fixture=KeyboardTerrain(0);
        Feed(connection,Pack(123,fixture.X,fixture.Y));
        var release=new TaskCompletionSource<bool>();int sends=0;bool entered=false;
        Sender(connection,async(bytes,current,admitted)=>{
            entered=true;await release.Task;
            if(!current())return false;
            sends++;admitted();return true;
        });
        form.Input.Down(DirectionKeys[fixture.Open]);
        var pending=(Task)typeof(ClientForm).GetMethod("MoveFromKeyboard",F)!.Invoke(form,[fixture.Open])!;
        Check(entered&&!pending.IsCompleted,"keyboard move is waiting inside real sender gate");
        int oldEpoch=connection.World.WorldEpoch;
        Feed(connection,LoginPackets.Pad([206,4,0,0]));Feed(connection,Pack(123,fixture.X,fixture.Y));
        Check(connection.World.WorldEpoch!=oldEpoch,"synthetic map packet changes world epoch");
        form.Input.Clear();form.Input.Down(Keys.Down);
        Call(form,"SetSingleActionNotice","新世界目前的提示");
        string motion=connection.World.MotionNotice;
        release.SetResult(true);await pending;
        Check(sends==0,"old world queued movement sends zero C29");
        Check(HudNotice(form)=="新世界目前的提示","old keyboard result cannot overwrite new world HUD");
        Check(form.Input.Direction==5,"old keyboard result cannot clear new world held input");
        Check(connection.World.MotionNotice==motion,"old sender rejection cannot overwrite new world motion notice");
    }

    static async Task<int> Main(string[] args){try{
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(Path.GetFullPath(args[0]),"engine","build","offline-checks")+Path.DirectorySeparatorChar);
        using var guard=new SocketGuard();
        PolymorphChecks();
        if(args.Length>2){
            switch(args[2]){
                case "map-continuity":await MapContinuity();await BoardChecks();await PlayerQueryChecks();await CharacterConfigChecks();break;
                case "player-query":await PlayerQueryChecks();break;
                case "character-config":await CharacterConfigChecks();break;
                case "polymorph":break;
                case "polymorph-cadence":PolymorphCadenceChecks(args[0]);break;
                case "buddies":await BuddyChecks();break;
                case "buddies-codec":await BuddyChecks(true);break;
                case "board":await BoardChecks();break;
                case "item-transfer":await ItemTransferChecks();break;
                case "combat-loop":await CombatLoop();break;
                case "combat-admission":await CombatAdmission();AttackSkillChecks.Run(args[1],Check);HitReactionSuppression();break;
                case "attack-skills":AttackSkillChecks.Run(args[1],Check);break;
                case "attack-skill-resources":AttackSkillChecks.NativeResources(args[3],args[4],Check);break;
                case "hit-reaction-suppress":HitReactionSuppression();break;
                case "slow-actions":await SlowActions();break;
                case "keyboard":await KeyboardMovement123();break;
                case "busy":KeyboardBusy123();break;
                case "world":await KeyboardWorld123();break;
                default:throw new ArgumentException("Unknown regression group");
            }
        }else{
            await CharacterConfigChecks();NativeTargetUi();await Movement123();await KeyboardMovement123();KeyboardBusy123();await KeyboardWorld123();
            await Combat();await Magic();await Quantity();await PickupCorrection();Position();await CombatLoop();await BoardChecks();HitReactionSuppression();await PlayerQueryChecks();
        }
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{status="pass",checks,scope="本次產品 DLL；合成封包與記憶體 sender；非真機",labels},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks} DEMO checks");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    sealed class SocketGuard:EventListener {protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network not allowed in offline regression");}
}
