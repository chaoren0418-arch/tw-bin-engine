using P06;
using System.Buffers.Binary;
using System.Text.Json;
using System.Security.Cryptography;

partial class Checks {
    static void SlowSpeed(LoginConnection c,byte speed=2)=>Feed(c,Packet(b=>{
        b.Write((byte)255);b.Write(123u);b.Write(speed);b.Write((ushort)60);
    }));
    static void SlowItem(LoginConnection c,string kind="healing") {
        var (desc,use,icon,flags,name)=kind switch {
            "haste"=>(17,246,77,0,"$234"),
            "meat"=>(10,38,124,1,"$23"),
            "home"=>(1338,9,474,1,"$505"),
            "weapon"=>(1,1,1,0,"offline weapon"),
            "armor"=>(1,2,1,0,"offline armor"),
            _=>(1342,249,80,0,"$26")
        };
        Feed(c,Packet(b=>{
            b.Write((byte)15);b.Write(900101u);b.Write((ushort)desc);b.Write((byte)use);b.Write((byte)0);
            b.Write((ushort)icon);b.Write((byte)1);b.Write(10u);b.Write((byte)flags);Text(b,name+" (10)");
            b.Write((byte)0);b.Write(new byte[11]);
        }));
        Check(c.World.SelectItem(900101),"選取合成道具 "+kind);
    }
    static Task<bool> SlowRequest(LoginConnection c,string action) {
        var p=new PlayerPointerController(c,()=>true,()=>false,_=>true);
        return action switch {
            "item"=>c.UseSelectedItem(),
            "drop"=>c.DropSelected(1),
            "pickup"=>c.PickupSelected(1),
            "pointer-pickup"=>p.PickupAsync(900102),
            "pointer-talk"=>p.ActAsync(456,true),
            _=>c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision)
        };
    }
    static void SlowPrepare(LoginConnection c,string action) {
        if(action=="item")SlowItem(c);
        if(action=="drop")SlowItem(c,"meat");
        if(action.Contains("pickup")){GroundPacket(c);c.World.SelectGround(900102);}
    }
    static byte[] SlowDenial(byte opcode)=>Packet(b=>{
        b.Write(opcode);
        if(opcode==71){b.Write((ushort)125);b.Write((byte)0);}
        else {if(opcode==161){b.Write((byte)0);b.Write(0u);}else b.Write((byte)9);Text(b,"伺服器拒絕此操作");}
    });
    [CheckGroup("slow-actions")]
    static async Task SlowActions() {
        // Use the actual receive path and memory sender; no game, socket or resource fixture.
        SlowProfileFixture();
        foreach(byte speed in new byte[]{0,1,2})
        foreach(string kind in new[]{"healing","haste","meat","home","weapon","armor"}) {
            using var c=Seed();SlowSpeed(c,speed);SlowItem(c,kind);int sends=0;
            var before=c.World.SelectedItem;var status=c.World.Status;
            Sender(c,(bytes,valid,admit)=>{
                if(!valid())return Task.FromResult(false);
                Check(bytes.SequenceEqual(LoginPackets.Pad([164,..BitConverter.GetBytes(900101u)])),"C164 物件 ID 與 padding "+kind);
                sends++;admit();return Task.FromResult(true);
            });
            Check(c.ItemAvailable&&await c.UseSelectedItem()&&sends==1,$"速度 {speed} 可送出 {kind}");
            Check(c.World.SelectedItem==before&&c.World.Status==status,"送出不預扣數量／HP／屬性 "+kind);
            Check(!await c.UseSelectedItem()&&sends==1,"保留道具冷卻／等待回包 "+kind);
        }
        foreach(byte speed in new byte[]{0,1,2})
        foreach(string action in new[]{"drop","pickup","pointer-pickup","talk","pointer-talk"}) {
            using var c=Seed();SlowSpeed(c,speed);SlowPrepare(c,action);int sends=0;
            Sender(c,(bytes,valid,admit)=>{
                if(!valid())return Task.FromResult(false);
                byte[] expected=action.Contains("talk")?NpcPackets.Talk(456):
                    GroundRules.Request(action=="drop"?(byte)25:(byte)112,32600,32940,action=="drop"?900101u:900102u,1);
                Check(bytes.SequenceEqual(expected),"送出既有封包格式 "+action);
                sends++;admit();return Task.FromResult(true);
            });
            Check(await SlowRequest(c,action)&&sends==1,$"速度 {speed} 可送出 {action}");
            if(action.Contains("talk"))Check(!c.CanTalk&&c.World.InteractionKind=="waiting"&&sends==1,"交談送出後等待回包 "+action);
            else Check(!await SlowRequest(c,action)&&sends==1,"保留冷卻／待回包防重送 "+action);
        }
        foreach(string action in new[]{"item","drop","pickup","pointer-pickup","talk","pointer-talk"})
        foreach(byte opcode in new byte[]{71,161,243})
        foreach(bool early in new[]{false,true}) {
            using var c=Seed();SlowSpeed(c);SlowPrepare(c,action);int sends=0;
            var item=c.World.SelectedItem;string ground=JsonSerializer.Serialize(c.World.Ground);
            Sender(c,(bytes,valid,admit)=>{
                if(!valid())return Task.FromResult(false);
                sends++;admit();if(early)Feed(c,SlowDenial(opcode));return Task.FromResult(true);
            });
            Check(await SlowRequest(c,action),"拒絕不偽裝為未送出 "+action+opcode+early);
            if(!early)Feed(c,SlowDenial(opcode));
            string reason=opcode==71?"你不能夠放棄此樣物品。":"伺服器拒絕此操作";
            Check(c.World.LastServerMessage==reason,"保留伺服器理由 "+action+opcode+early);
            Check(JsonSerializer.SerializeToElement(c.World.InventorySnapshot()).GetProperty("serverMessage").GetString()==reason,"UI 訊息模型可見伺服器理由");
            if(action.Contains("talk")&&opcode!=161)Check(c.World.InteractionNotice.Contains(reason),"NPC 結果顯示原服理由");
            Check(c.World.SelectedItem==item&&JsonSerializer.Serialize(c.World.Ground)==ground&&sends==1,"拒絕不結算物品或自動重送");
        }
        foreach(string action in new[]{"item","drop","pickup","pointer-pickup","talk","pointer-talk"})
        foreach(string guard in new[]{"dead","paralysis","trade","movement","selection","world"}) {
            using var c=Seed();SlowSpeed(c);SlowPrepare(c,action);int sends=0;
            bool queued=guard is "selection" or "world";
            void Invalidate() {
                switch(guard) {
                    case "dead":Set(c.World,"OwnDead",true);break;
                    case "paralysis":Feed(c,LoginPackets.Pad([202,2]));break;
                    case "trade":Set(c.World,"TradeState","inviting");break;
                    case "movement":Set(c,"nextMovementAt",Environment.TickCount64+60000);break;
                    case "world":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                    case "selection":
                        if(action=="item"||action=="drop")c.World.SelectItem(null);
                        else if(action.Contains("pickup"))c.World.SelectGround(null);
                        else c.World.SelectTarget(null);
                        break;
                }
            }
            if(!queued)Invalidate();
            Sender(c,(bytes,valid,admit)=>{if(queued)Invalidate();if(!valid())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);});
            Check(!await SlowRequest(c,action)&&sends==0,"緩速仍受既有保護 "+action+"/"+guard);
        }
        foreach(string action in new[]{"item","drop","pickup","pointer-pickup","pointer-talk"}) {
            using var c=Seed();SlowSpeed(c);SlowPrepare(c,action);int sends=0;
            Sender(c,(bytes,valid,admit)=>{SlowSpeed(c,0);if(!valid())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);});
            Check(!await SlowRequest(c,action)&&sends==0,"送出前速度變更仍取消舊請求 "+action);
        }
        using(var c=Seed()) {
            int normal=c.World.MovementIntervalMs!.Value;SlowSpeed(c);
            Check(c.MovementAvailable&&c.World.MovementIntervalMs==normal*2,"緩速移動節奏仍為正常兩倍");
            SlowItem(c,"meat");GroundPacket(c);c.World.SelectGround(900102);
            Sender(c,(_,_,_)=>throw new Exception("無效數量不得進入 sender"));
            foreach(uint count in new uint[]{0,11,uint.MaxValue})
                Check(!await c.DropSelected(count)&&!await c.PickupSelected(count),"緩速仍拒絕無效數量 "+count);
            SlowItem(c);Set(c.World,"Actor",c.World.Actor! with{Speed=3});
            Check(!c.ItemAvailable,"未知速度支援範圍未擴張");
        }
    }
    static void SlowProfileFixture() {
        string root=Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../..")),"scratch","issue-191","synthetic-policy");
        string assets=Path.Combine(root,"assets");Directory.CreateDirectory(Path.Combine(assets,"players"));
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(root,"build","offline-checks")+Path.DirectorySeparatorChar);
        // Authored timing metadata for admission only; no images, map data or original assets.
        byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(new {
            schema="p24-player-profiles-v1",profiles=new[]{new {
                gfx=0,weapon=0,path="players/not-rendered.json",sha256=new string('0',64),stepMs=640,
                serverActionMs=new{walk=640,attack=640,spellDir=1320,spellNoDir=1480},
                castReleaseMs=new{spellDir=320}
            }}
        });
        File.WriteAllBytes(Path.Combine(assets,"players/manifest.json"),manifest);
        File.WriteAllBytes(Path.Combine(assets,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new {
            files=new[]{new{path="players/manifest.json",size=manifest.Length,sha256=Convert.ToHexString(SHA256.HashData(manifest))}}
        }));
    }
}
