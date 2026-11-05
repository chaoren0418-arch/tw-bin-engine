using P06;
using System.Reflection;
using System.Text.Json;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Diagnostics.Tracing;

// Executes the built product assembly; never invokes its application entry point.
// Reflection only seeds offline world state and substitutes an in-memory sender.
class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int assertions;static readonly List<string> cases=[];static JsonElement fixtures;
    static void Check(bool ok,string label){assertions++;if(!ok)throw new Exception(label);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static void Call(object o,string name)=>o.GetType().GetMethod(name,F)!.Invoke(o,null);
    static byte[] Fixture(string name)=>LoginPackets.Pad(Convert.FromHexString(fixtures.GetProperty("packetFixtures").EnumerateArray().Single(x=>x.S("id")==name).S("payloadHex")));
    static void Feed(LoginConnection c,byte[] p)=>typeof(LoginConnection).GetMethod("ApplyWorldPacket",F)!.Invoke(c,[p,"offline",1L]);
    static byte[] Packet(Action<BinaryWriter> action){using var m=new MemoryStream();using var b=new BinaryWriter(m);action(b);return LoginPackets.Pad(m.ToArray());}
    static void Text(BinaryWriter b,string s){b.Write(LoginPackets.TextEncoding.GetBytes(s));b.Write((byte)0);}
    static void Ground(LoginConnection c,GroundDefinition row,uint count=250,uint id=900101,ushort? x=null){
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write(x??c.World.Actor!.X);b.Write(c.World.Actor!.Y);b.Write(id);b.Write(row.GroundGfx);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);Text(b,row.CountName(count));Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
    }
    static void Item(LoginConnection c,GroundDefinition row,uint count=250,uint id=900101){
        Feed(c,Packet(b=>{b.Write((byte)15);b.Write(id);b.Write(row.InventoryDesc);b.Write(row.WireUseType);b.Write((byte)0);b.Write(row.InventoryIcon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,row.CountName(count));b.Write((byte)0);b.Write(new byte[11]);}));
    }
    static LoginConnection Seed(GroundDefinition row){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Ground(c,row);Item(c,row);c.World.SelectGround(900101);c.World.SelectItem(900101);return c;
    }
    static void Sender(LoginConnection c,Func<byte[],Func<bool>,Action,Task<bool>> sender)=>Set(c,"sendMovementGate",sender);
    static Task<bool> Send(LoginConnection c,bool pickup,string count,GroundQuantitySelection? selection=null)=>c.SendGroundQuantity(pickup,count,selection??c.World.GroundQuantity(pickup));
    static async Task RejectedDrop(GroundDefinition gold){
        // #49's unchanged static S15, plus a source-derived S71/125 response.
        // Neither is a live capture, and the sender never executes Java/network code.
        foreach(bool beforeCompletion in new[]{false,true}){
            using var c=Seed(gold);Feed(c,Fixture("gold-s15"));
            uint id=BinaryPrimitives.ReadUInt32LittleEndian(Fixture("gold-s15").AsSpan(1));
            c.World.SelectItem(id);var selection=c.World.GroundQuantity(false)!;
            var item=c.World.SelectedItem;string ground=JsonSerializer.Serialize(c.World.Ground);int sends=0;
            var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();sends++;
                Check(b[0]==25&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5))==id,"drop uses owned ObjectId");
                Check(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(9))==(sends==1?1u:10u),"retry count is explicit");
                return beforeCompletion&&sends==1?completion.Task:Task.FromResult(true);});
            var first=Send(c,false,"1",selection);if(!beforeCompletion)Check(await first,"first drop completed");
            int revision=c.World.InventoryRevision;
            Feed(c,LoginPackets.Pad([71,125,0,0]));
            Check(c.World.LastServerReply?.Id==125,"server rejection decoded");
            Check(c.World.InventoryRevision>revision,"message still refreshes UI");
            Check(c.World.CurrentGroundQuantity(false,selection),"rejection preserves unchanged quantity snapshot");
            Check(c.World.LastServerReply is {Recognized:true,Text:"你不能夠放棄此樣物品。"},"source-proven 125 text");
            Check(c.World.SelectedItem==item&&JsonSerializer.Serialize(c.World.Ground)==ground,"rejection does not settle items/ground");
            Check(!c.World.AwaitingPickupAction,"drop has no pickup pending");
            if(beforeCompletion){completion.SetResult(true);Check(await first,"rejection before completion keeps owner");}
            Check(!await Send(c,false,"10",selection)&&sends==1,"rejection does not bypass 900ms guard");
            await Task.Delay(LoginConnection.GroundClickGuardMs+50);
            Check(!c.GroundBusy,"transport lease and click guard released");
            Check(await Send(c,false,"10",selection)&&sends==2,"explicit second drop after rejection");
            Check(c.World.SelectedItem==item,"second request has no predicted deduction");
            foreach(string invalid in new[]{"0","251"}){
                Check(!await Send(c,false,invalid,selection)&&sends==2,"invalid retry does not send");
                Check(c.World.GroundNotice.Contains("數量不合法")&&!c.World.GroundNotice.Contains("重新選取"),"quantity-only message");
            }
            Item(c,gold,249,id);Item(c,gold,250,id);Feed(c,LoginPackets.Pad([71,125,0,0]));
            Check(!await Send(c,false,"1",selection)&&sends==2,"real item ABA remains stale after rejection");
            Check(c.World.GroundNotice.Contains("選取已過期")&&!c.World.GroundNotice.Contains("數量不合法"),"selection-only message");
            Check(!await Send(c,false,"0",selection)&&c.World.GroundNotice.Contains("數量不合法")&&!c.World.GroundNotice.Contains("重新選取"),"invalid quantity takes precedence even when stale");
            c.World.SelectItem(id);var fresh=c.World.GroundQuantity(false)!;
            await Task.Delay(LoginConnection.GroundClickGuardMs+50);
            Check(await Send(c,false,"10",fresh)&&sends==3,"explicit reselect restores real stale item");
        }
        using(var c=Seed(gold)){
            var selection=c.World.GroundQuantity(false);int sends=0;
            Sender(c,(b,v,a)=>{Feed(c,LoginPackets.Pad([71,125,0,0]));if(!v())return Task.FromResult(false);a();sends++;return Task.FromResult(true);});
            Check(await Send(c,false,"1",selection)&&sends==1,"message before final admission does not expire item content");
        }
        Check(!ServerMessageText.Decode(125,["unexpected"]).Recognized,"unexpected 125 argument stays unknown");
        cases.Add("#77 S71/125 拒絕→恢復→再次手動 C25；送出前／完成前／完成後、900ms、數量訊息、ABA 保護");
    }
    static async Task<int> Main(string[] args){
        try{await Run(args);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static async Task Run(string[] args){
        // Existing PlayerProfiles resolves ../../assets from AppContext. Point only
        // this offline host at the original engine's read-only assets; no junction/copy.
        if(args.Length>3)AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(Path.GetFullPath(args[3]),"build","offline-checks")+Path.DirectorySeparatorChar);
        using var guard=new SocketGuard();string repo=Path.GetFullPath(args[0]);string output=Path.GetFullPath(args[1]);
        byte[] raw=File.ReadAllBytes(Path.Combine(repo,"work/issue-51/fixtures-49.json"));Check(Convert.ToHexString(SHA256.HashData(raw)).Equals("2fd34c51c43b11becb5738508f1722918f9254ccb15f568a348f6b362061d9a4",StringComparison.OrdinalIgnoreCase),"#49 fixture SHA");
        using var doc=JsonDocument.Parse(raw);fixtures=doc.RootElement;
        Check(GroundCatalog.Rows.Count==7&&GroundCatalog.Rows.Count(r=>r.Sprite!=null)==4,"catalog coverage");
        foreach(var row in GroundCatalog.Rows){
            Check((GroundCatalog.Find(row.GroundGfx)!=null)==(row.Sprite!=null),row.Id+" enabled only with pin");
            using var c=Seed(row);Check((c.World.Ground.Length==1)==(row.Sprite!=null),row.Id+" S87 classification");Check(GroundRules.Droppable(c.World.SelectedItem!)==(row.Sprite!=null),row.Id+" drop classification");
        }
        cases.Add("七筆模板交集：四筆啟用、三筆缺 pin 關閉");
        var gold=GroundCatalog.Rows.Single(r=>r.Id=="gold");
        await RejectedDrop(gold);
        foreach(string name in new[]{"gold","meat"}){
            using var c=new LoginConnection();Feed(c,Fixture(name+"-s87"));Feed(c,Fixture(name+"-s15"));var row=GroundCatalog.Rows.Single(r=>r.Id==name);uint n=name=="gold"?250u:3u;
            Check(c.World.Ground.Single().Count==n&&c.World.Ground.Single().Gfx==row.GroundGfx,"source S87 "+name);Check(GroundRules.Droppable(c.World.Inventory.Single()),"source S15 "+name);
            var o=c.World.Ground.Single().Envelope;
            foreach(var bad in new[]{o with{Gfx=3247},o with{Gfx=999},o with{Name="$4"},o with{Action=1},o with{Flags=4},o with{HpByte=100},o with{Title="npc"},o with{Owner="owner"},o with{ObjectId=0},o with{WireExp=2000000001}})Check(!GroundRules.IsGround(bad),"nonitem/mismatched envelope");
            var i=c.World.Inventory.Single();foreach(var bad in new[]{i with{UseType=1},i with{Bless=129},i with{Flags=32},i with{Flags=128},i with{Charges=1},i with{IconId=999},i with{DescriptionId=999},i with{Name="same name"},i with{Count=0}})Check(!GroundRules.Droppable(bad),"inventory identity/policy");
        }
        cases.Add("#49 金幣／肉 S87、S15 原 fixture 與同名／同 gfx 非物品欄位反例");
        foreach(var f in fixtures.GetProperty("packetFixtures").EnumerateArray().Where(f=>f.S("id").StartsWith("pickup-"))){
            using var c=Seed(gold);var wire=Convert.FromHexString(f.S("payloadHex"));uint id=BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(5)),count=BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(9));
            if(f.S("id")=="pickup-over-available"){Ground(c,gold,f.GetProperty("input").GetProperty("available").GetUInt32(),id);c.World.SelectGround(id);}else {c.World.SelectGround(null);Check(!c.World.SelectGround(id),"nonitem id cannot select");}
            int sends=0;Sender(c,(b,v,a)=>{sends++;return Task.FromResult(v());});Check(!await Send(c,true,count.ToString())&&sends==0,"#49 "+f.S("id"));
        }
        cases.Add("#49 非物品與超量拒絕 fixture");
        foreach(var row in GroundCatalog.Rows.Where(r=>r.Sprite!=null))foreach(bool pickup in new[]{false,true})foreach(uint count in new uint[]{1,250}){
            using var c=Seed(row);int sends=0;byte[]? actual=null;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);actual=b;a();sends++;return Task.FromResult(true);});
            Check(await Send(c,pickup,count.ToString()),row.Id+" 1/N admitted");Check(actual!.SequenceEqual(GroundRules.Request(pickup?(byte)112:(byte)25,32600,32940,900101,count)),"wire exact");
            Check(actual![0]==(pickup?112:25)&&BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(9))==count&&actual.Length==16&&actual.AsSpan(13).ToArray().All(b=>b==0),"wire fields/padding");
            Check(c.World.SelectedItem!.Count==250&&c.World.SelectedGround!.Count==250,"no predicted amount");Check(!await Send(c,pickup,"1")&&sends==1,"900ms rapid click");
            Feed(c,LoginPackets.Pad([158,..BitConverter.GetBytes(123u),15]));Check(c.World.SelectedItem!.Count==250&&c.World.SelectedGround!.Count==250,"S158 does not settle quantity");
            Ground(c,row,200);Item(c,row,300);Check(c.World.SelectedGround!.Count==200&&c.World.SelectedItem!.Count==300,"authoritative replacement");
        }
        cases.Add("金幣／肉／箭／治癒藥水 C25/C112：1/N、padding、不預扣、權威更新、900ms");
        foreach(bool pickup in new[]{false,true})foreach(string text in new[]{"0","-1","1.0","1.5","2147483648","4294967296","999999999999999999999999999","251",""," ","+1","1e2"," 1","１"}){
            using var c=Seed(gold);int sends=0;Sender(c,(b,v,a)=>{sends++;return Task.FromResult(v());});Check(!await Send(c,pickup,text)&&sends==0,"strict quantity "+text);
        }
        Check(GroundCatalog.TryCount("2147483647",uint.MaxValue,out uint max)&&max==int.MaxValue,"signed max");Check(!GroundCatalog.TryCount("2",1,out _),"available max");
        cases.Add("零、負、小數、溢位、超額、空白、指數、非 ASCII 數字拒絕");
        foreach(bool pickup in new[]{false,true})foreach(string change in new[]{"selection","amount","amount-aba","removed","epoch","death","trade","distance","lease","equipment"}){
            using var c=Seed(gold);var selection=c.World.GroundQuantity(pickup);int sends=0;var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Sender(c,async(b,v,a)=>{await gate.Task;if(!v())return false;a();sends++;return true;});var pending=Send(c,pickup,"1",selection);Check(!pending.IsCompleted,"queued quantity");
            switch(change){
                case "selection":if(pickup){c.World.SelectGround(null);c.World.SelectGround(900101);}else{c.World.SelectItem(null);c.World.SelectItem(900101);}break;
                case "amount":case "amount-aba":if(pickup)Ground(c,gold,249);else Item(c,gold,249);if(change=="amount-aba"){if(pickup)Ground(c,gold);else Item(c,gold);}break;
                case "removed":Feed(c,LoginPackets.Pad([pickup?(byte)120:(byte)57,..BitConverter.GetBytes(900101u)]));break;
                case "epoch":c.Cancel();break;
                case "death":Set(c.World,"OwnDead",true);break;
                case "trade":Set(c.World,"TradeState","open");break;
                case "distance":Set(c.World,"Actor",c.World.Actor! with{X=32610});break;
                case "lease":Call(c,"ResetGroundSend");break;
                case "equipment":if(!pickup)Feed(c,LoginPackets.Pad([64,66,..BitConverter.GetBytes(900101u),1,1]));else Set(c.World,"AppearanceRevision",c.World.AppearanceRevision+1);break;
            }
            gate.SetResult();Check(!await pending&&sends==0,"delayed stale "+pickup+change);
        }
        foreach(bool pickup in new[]{false,true}){
            using var c=Seed(gold);var selection=c.World.GroundQuantity(pickup);if(pickup)Ground(c,gold,249);else Item(c,gold,249);int sends=0;Sender(c,(b,v,a)=>{sends++;return Task.FromResult(v());});Check(!await Send(c,pickup,"1",selection)&&sends==0,"stale UI snapshot at dispatch");
        }
        cases.Add("UI 舊快照及送出前選取、數量 ABA、移除、世界、死亡、交易、位置、lease、裝備變更拒絕");
        using(var c=Seed(gold)){
            int sends=0;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();sends++;return Task.FromResult(true);});Check(await Send(c,true,"1"),"timeout initial");await Task.Delay(3050);Check(c.World.PickupOutcomeTimedOut&&!await Send(c,true,"1")&&sends==1,"3000ms pending retained");
        }
        cases.Add("3000ms 未確認鎖定不重送");
        foreach(var row in GroundCatalog.Rows.Where(r=>r.Sprite!=null)){
            var frame=GroundSpriteReader.Read(row,args.Length>2?args[2]:null);Check(frame.Bounds.SequenceEqual(row.Sprite!.Bounds)&&frame.Blocks.Length>0,"actual SPR bounds/hash/RGBA "+row.Id);
            using var image=GroundSpriteReader.Image(frame);Check(image.Width==frame.Bounds[2]-frame.Bounds[0]+1&&image.Height==frame.Bounds[3]-frame.Bounds[1]+1,"web image bounds");
            var rgba=new List<byte>();for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var pixel=image.GetPixel(x,y);rgba.AddRange([pixel.R,pixel.G,pixel.B,pixel.A]);}
            Check(Convert.ToHexString(SHA256.HashData(rgba.ToArray())).Equals(row.Sprite.RgbaSha256,StringComparison.OrdinalIgnoreCase),"Web RGBA equals original pin");
            var bounds=new System.Drawing.Rectangle(0,0,48,96);var colors=new ushort[bounds.Width*bounds.Height];var mask=new bool[colors.Length];var owners=new uint?[colors.Length];
            foreach(var block in frame.Blocks){var draw=new Draw(block.A,block.B,block.Z,block.Order,"actor",block.Order,-1,-1,block.Tile,900101);var area=bounds with{Y=716};Renderer.Blit(draw,area,colors,mask,null,owners);}
            Check(owners.Any(id=>id==900101)&&owners.Any(id=>id==null),"ground opaque pixels hit; transparent pixels do not hit");
            if(args.Length>3&&row.Id is "meat" or "arrow"){
                var old=Json.Read(Path.Combine(args[3],"assets/ground/"+row.GroundGfx+".json"));var oldBlocks=old.GetProperty("blocks").EnumerateArray().ToArray();
                Check(old.A("bounds").SequenceEqual(frame.Bounds)&&oldBlocks.Length==frame.Blocks.Length,"legacy frame bounds/block count");
                for(int i=0;i<oldBlocks.Length;i++){
                    var a=oldBlocks[i];var b=frame.Blocks[i];Check(a.I("a")==b.A&&a.I("b")==b.B&&a.I("z")==b.Z&&a.I("source_order")==b.Order,"legacy A/B/sort/order");
                    Check(a.GetProperty("pixels").EnumerateArray().Select(p=>p.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).Select(v=>new Pixel(v[0],v[1],(ushort)v[2])).SequenceEqual(b.Tile.Pixels),"legacy original pixels unchanged");
                }
            }
            foreach(var bad in new[]{row.Sprite with{PayloadSha256=new string('0',64)},row.Sprite with{RgbaSha256=new string('0',64)}}){
                bool refused=false;try{GroundSpriteReader.Read(row with{Sprite=bad},args.Length>2?args[2]:null);}catch(InvalidDataException){refused=true;}Check(refused,"wrong payload/RGBA pin fails closed");
            }
        }
        cases.Add("四筆原客戶端 IDX／payload／RGBA pin、bounds、Web 原圖尺寸");
        if(args.Length>3){
            var assets=new Assets(Path.Combine(args[3],"assets"));using var renderer=new Renderer(assets);var objects=new ObjectAssets(assets.Root);
            foreach(var row in GroundCatalog.Rows.Where(r=>r.Sprite!=null)){
                using var c=Seed(row);var draws=new List<Draw>();var visuals=new List<ObjectVisual>();
                typeof(Renderer).GetMethod("ComposeGround",F)!.Invoke(renderer,[c.World,objects,draws,visuals]);
                Check(visuals.Single().ObjectId==900101&&visuals.Single().Gfx==row.GroundGfx&&draws.All(d=>d.ObjectId==900101),"catalog reaches production rendering "+row.Id);
                var b=visuals.Single().Bounds;var colors=new ushort[b.Width*b.Height];var mask=new bool[colors.Length];var owners=new uint?[colors.Length];
                foreach(var draw in draws)Renderer.Blit(draw,b,colors,mask,null,owners);
                var patch=new Patch(b,colors,mask,new int[colors.Length],draws,owners);
                Check(patch.VisibleObjectIds.SetEquals(new uint[]{900101})&&owners.Any(id=>id==null),"production visible pixel ownership "+row.Id);
                Check(c.World.SelectGround(patch.VisibleObjectIds.Single())&&c.World.SelectedGround?.Gfx==row.GroundGfx,"pixel hit to catalog selection "+row.Id);
            }
            cases.Add("實際 ComposeGround→Blit→可見像素 owner→選取，四種 catalog 物品一致");
        }
        Check(!guard.Observed,"no sockets");Json.Save(output,new{status="pass",assertions,cases,fixtureSha256=Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(),productSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LoginConnection).Assembly.Location))).ToLowerInvariant(),scope="離線：來源 fixture、明示合成狀態、記憶體 sender、真實原圖；未執行遊戲／服務／DB"});Console.WriteLine($"PASS {assertions} assertions / {cases.Count} groups");
    }
    sealed class SocketGuard:EventListener {public bool Observed;protected override void OnEventSourceCreated(EventSource e){if(e.Name.StartsWith("System.Net.Sockets"))EnableEvents(e,EventLevel.LogAlways);}protected override void OnEventWritten(EventWrittenEventArgs e){Observed=true;}}
}
