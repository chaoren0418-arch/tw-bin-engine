using P06;
using System.Buffers.Binary;
using System.Drawing;
using System.Text.Json;

// T212 / #518 (M7-L): pickup aligned to WO §1–2. Synthetic S87/S158 packets, memory sender,
// memory movement and synthetic all-passable terrain; no game, socket, map or asset files.
partial class Checks {
    static LoginConnection PickupSeed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Direction\":0,\"Name\":\"offline\"}"));
        return c;
    }
    static void PickupGroundAt(LoginConnection c,uint id,int dx,int dy,uint count){
        var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");ushort x=(ushort)(c.World.Actor!.X+dx),y=(ushort)(c.World.Actor.Y+dy);
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write(row.GroundGfx);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);Text(b,row.CountName(count));Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
    }
    static List<byte[]> PickupSender(LoginConnection c){var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});return sent;}
    // S158/15 itself needs player profiles (assets); only the pending association is released here.
    static void PickupConfirm(LoginConnection c)=>Call(c.World,"InvalidatePickupRequest");
    static void PickupRelease(LoginConnection c){PickupConfirm(c);Set(c,"nextGroundAt",0L);}
    static uint PickupCount(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(9));
    static uint PickupId(byte[] b)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5));
    // Memory movement: one step = local position update, as the real PointerMove predicts.
    static ApproachWalk MemoryApproach(LoginConnection c,List<int> steps,Func<int,int,int,bool>? terrain=null)=>new(c,wait:()=>Task.Delay(1),terrain:terrain??((_,_,_)=>true),available:()=>true,move:(h,valid)=>{
        if(!valid())return Task.FromResult(false);steps.Add(h);var a=c.World.Actor!;Set(c.World,"Actor",a with{X=(ushort)(a.X+MovementRules.Dx[h]),Y=(ushort)(a.Y+MovementRules.Dy[h]),Direction=(byte)h});return Task.FromResult(true);});

    [CheckGroup("pickup")]
    static async Task PickupAlignChecks(){
        // Rules (WO §1 distance, §2 steps 3–4).
        Check(ApproachRules.Within(0,0,1,1,1)&&ApproachRules.Within(0,0,-1,0,1)&&!ApproachRules.Within(0,0,2,0,1)&&!ApproachRules.Within(0,0,1,2,1),"WO§1 1 格含斜角");
        Check(GroundRules.PickupThrottle(false)==986&&GroundRules.PickupThrottle(true)==600&&LoginConnection.GroundClickGuardMs==986,"WO§2 節流 986／600 ms");
        Check(GroundRules.DirectPickupCount(1,false)==1&&GroundRules.DirectPickupCount(10,true)==10&&GroundRules.DirectPickupCount(10,false)==null&&GroundRules.DirectPickupCount(0,true)==null,"單個或全部拾取送整疊，否則提示");
        Check(GroundRules.TryPromptCount("",10,out uint blank)&&blank==10&&GroundRules.TryPromptCount("  ",10,out uint spaces)&&spaces==10,"提示空白＝整疊");
        Check(GroundRules.TryPromptCount("4",10,out uint four)&&four==4&&!GroundRules.TryPromptCount("0",10,out _)&&!GroundRules.TryPromptCount("11",10,out _)&&!GroundRules.TryPromptCount("-1",10,out _),"提示 0 或超過持有量不送");
        var open=new Func<int,int,int,bool>((_,_,_)=>true);
        Check(ApproachRules.Plan(new(0,0),new(1,1),1,open)!.Length==0,"已在 1 格內不走");
        var plan=ApproachRules.Plan(new(0,0),new(4,0),1,open)!;Check(plan.Length==3&&plan[^1]==new Point(3,0),"走到 1 格內即停");
        // Target tile unreachable: approach a reachable neighbour within range.
        var walled=new Func<int,int,int,bool>((x,y,h)=>!(x+MovementRules.Dx[h]==4&&y+MovementRules.Dy[h]==0));
        var around=ApproachRules.Plan(new(0,0),new(4,0),1,walled)!;Check(around.Length>0&&ApproachRules.Within(around[^1].X,around[^1].Y,4,0,1)&&around[^1]!=new Point(4,0),"目標格不可達時走到相鄰格");

        // In range, single item: whole stack, local turn before send, 986 ms throttle without queue.
        using(var c=PickupSeed()){
            var sent=PickupSender(c);PickupGroundAt(c,900301,1,0,1);
            Check(await c.PickupGround(900301)&&sent.Count==1&&sent[0][0]==112&&PickupId(sent[0])==900301&&PickupCount(sent[0])==1,"1 格內單個直接送整疊");
            Check(c.World.Actor!.Direction==2,"送出前本機轉向物品（東）");
            long left=(long)typeof(LoginConnection).GetField("nextGroundAt",F)!.GetValue(c)!-Environment.TickCount64;Check(left>900&&left<=986,"一般節流 986 ms");
            PickupConfirm(c);PickupGroundAt(c,900302,0,1,1);
            Check(!await c.PickupGround(900302)&&sent.Count==1,"節流未過不送也不排隊");
            await Task.Delay(1000);Check(sent.Count==1,"節流期間的點擊不會事後補送");
            Check(await c.PickupGround(900302)&&sent.Count==2&&c.World.Actor!.Direction==4,"節流過後可再送，轉向南");
        }
        using(var c=PickupSeed()){
            // T253 (#614): the flag now comes from S139 (dword_13CE514), not own haste.
            var sent=PickupSender(c);Set(c,"serverFeatures",new ServerFeatures(0,0,3,950,ServerFeatures.FastPickupFlag,0,0));PickupGroundAt(c,900303,0,0,1);
            Check(await c.PickupGround(900303)&&sent.Count==1,"S139 旗標下可送");
            long left=(long)typeof(LoginConnection).GetField("nextGroundAt",F)!.GetValue(c)!-Environment.TickCount64;Check(left>500&&left<=600,"S139 旗標 0x10000 節流 600 ms");
            Check(c.World.Actor!.Direction==0,"腳下物品不轉向");
        }

        // Stack >1 without loot-all: prompt; blank = whole, 0 / over = nothing.
        using(var c=PickupSeed()){
            var sent=PickupSender(c);PickupGroundAt(c,900304,1,1,10);int opened=0;c.PickupPromptOpened+=_=>opened++;
            Check(!await c.PickupGround(900304)&&sent.Count==0&&opened==1&&c.World.PickupPrompt?.Id==900304,"多個且未開全部拾取：跳數量提示");
            Check(!await c.AnswerPickupPrompt("0")&&sent.Count==0&&c.World.PickupPrompt==null,"提示輸入 0 不送");
            await c.PickupGround(900304);Check(!await c.AnswerPickupPrompt("11")&&sent.Count==0,"提示超過持有量不送");
            await c.PickupGround(900304);Check(await c.AnswerPickupPrompt("")&&sent.Count==1&&PickupCount(sent[0])==10,"提示空白送整疊");
            PickupRelease(c);await c.PickupGround(900304);Check(await c.AnswerPickupPrompt("3")&&sent.Count==2&&PickupCount(sent[1])==3,"提示輸入數量");
            PickupRelease(c);await c.PickupGround(900304);PickupGroundAt(c,900304,1,1,9);Check(!await c.AnswerPickupPrompt("")&&sent.Count==2,"提示期間數量變了不送舊快照");
            Check(!await c.AnswerPickupPrompt("1")&&c.World.GroundNotice.Contains("沒有等待中"),"無提示時回答不送");
            Check(c.World.TogglePickupAll()&&c.World.PickupAll,"切換全部拾取");
            Check(await c.PickupGround(900304)&&sent.Count==3&&PickupCount(sent[2])==9&&c.World.PickupPrompt==null,"全部拾取開：多個也直接送整疊");
        }

        // Ghost blocks every pickup path (WO §2 step 1, §4).
        using(var c=PickupSeed()){
            var sent=PickupSender(c);PickupGroundAt(c,900305,0,0,1);Set(c.World,"Actor",c.World.Actor! with{Flags=0x80});
            Check(c.World.IsGhost&&!c.PickupAvailable,"幽靈狀態判定");
            Check(!await c.PickupGround(900305)&&!await c.LootNearby()&&!await c.PickupSelected(1)&&sent.Count==0&&c.World.GroundNotice.Contains("幽靈"),"幽靈中擋拾取（點擊、鍵盤、明確數量）");
        }

        // Keyboard Action_Loot: first item within 1 tile, whole stack, never asks or walks.
        using(var c=PickupSeed()){
            var sent=PickupSender(c);PickupGroundAt(c,900306,2,0,5);
            Check(!await c.LootNearby()&&sent.Count==0&&c.World.GroundNotice.Contains("1 格內沒有"),"鍵盤拾取不走過去");
            PickupGroundAt(c,900307,-1,-1,7);
            Check(await c.LootNearby()&&sent.Count==1&&PickupId(sent[0])==900307&&PickupCount(sent[0])==7&&c.World.PickupPrompt==null,"鍵盤拾取 1 格內物品、整疊、不問數量");
            Check(c.World.Actor!.Direction==7,"鍵盤拾取同樣先轉向");
        }

        // Click far item: approach to 1 tile, then send once; local input cancel or target loss stops it.
        using(var c=PickupSeed()){
            var sent=PickupSender(c);var steps=new List<int>();Set(c,"Approach",MemoryApproach(c,steps));PickupGroundAt(c,900308,4,0,1);
            Check(await c.PickupGround(900308),"遠處物品走近後送出");
            Check(steps.SequenceEqual([2,2,2])&&c.World.Actor!.X==32603&&sent.Count==1&&PickupId(sent[0])==900308&&c.World.Actor.Direction==2,"走 3 步到 1 格內，轉向後送一次");
            Check(!c.Approach.Active,"走近結束");
        }
        using(var c=PickupSeed()){
            var sent=PickupSender(c);var steps=new List<int>();Set(c,"Approach",MemoryApproach(c,steps));PickupGroundAt(c,900309,-3,3,10);
            Check(!await c.PickupGround(900309)&&steps.Count==2&&sent.Count==0&&c.World.PickupPrompt?.Id==900309,"走近後多個仍跳數量提示");
            Check(await c.AnswerPickupPrompt("")&&sent.Count==1&&PickupCount(sent[0])==10&&c.World.Actor!.Direction==5,"走近後提示整疊並轉向西南");
        }
        foreach(string change in new[]{"input","removed","reselect","ghost"}){
            using var c=PickupSeed();var sent=PickupSender(c);var steps=new List<int>();
            Set(c,"Approach",new ApproachWalk(c,wait:()=>Task.Delay(1),terrain:(_,_,_)=>true,available:()=>true,move:(h,valid)=>{
                if(!valid())return Task.FromResult(false);steps.Add(h);var a=c.World.Actor!;Set(c.World,"Actor",a with{X=(ushort)(a.X+1)});
                if(steps.Count==1)switch(change){
                    case "input":c.CancelMovementInput();break;
                    case "removed":Feed(c,Packet(b=>{b.Write((byte)120);b.Write(900310u);}));break;
                    case "reselect":c.World.SelectGround(null);break;
                    case "ghost":Set(c.World,"Actor",c.World.Actor! with{Flags=0x80});break;
                }
                return Task.FromResult(true);}));
            PickupGroundAt(c,900310,4,0,1);
            Check(!await c.PickupGround(900310)&&steps.Count==1&&sent.Count==0&&!c.Approach.Active,"走近中途 "+change+" 即停止、不送");
        }
        using(var c=PickupSeed()){
            var sent=PickupSender(c);var steps=new List<int>();Set(c,"Approach",MemoryApproach(c,steps,(_,_,_)=>false));PickupGroundAt(c,900311,4,0,1);
            Check(!await c.PickupGround(900311)&&steps.Count==0&&sent.Count==0&&c.World.GroundNotice.Contains("路徑"),"無路徑時不送並提示");
        }
        // Low-level explicit-count path keeps every existing gate but now uses the 1-tile distance.
        using(var c=PickupSeed()){
            var sent=PickupSender(c);PickupGroundAt(c,900312,2,0,3);c.World.SelectGround(900312);
            Check(!await c.PickupSelected(1)&&sent.Count==0&&c.World.GroundNotice.Contains("1 格"),"明確數量路徑距離門檻改為 1 格");
        }
        labels.Add("T212 拾取對齊：WO§1 距離、自動走近、整疊／提示、轉向、986／600 ms、幽靈、鍵盤拾取");
    }
}
