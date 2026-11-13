using P06;
using System.Buffers.Binary;

partial class Checks {
    static byte[] Death(uint id)=>LoginPackets.Pad([158,..BitConverter.GetBytes(id),8]);
    static byte[] Hp(uint hp)=>Packet(b=>{b.Write((byte)225);b.Write(hp);b.Write(100u);});
    static byte[] Resurrection(uint id=123)=>Packet(b=>{b.Write((byte)85);b.Write(id);b.Write((byte)0);b.Write(789u);b.Write(0u);});
    static byte[] Weapon(byte weapon)=>LoginPackets.Pad([119,..BitConverter.GetBytes(123u),weapon]);
    static byte[] Item(uint id,uint count,bool arrow=false,bool list=false)=>Packet(b=>{
        var row=SyntheticGroundItem.Stack;
        b.Write((byte)(list?5:15));if(list)b.Write((byte)1);
        b.Write(id);b.Write(arrow?(ushort)0:row.InventoryDesc);b.Write(arrow?(byte)254:row.WireUseType);
        b.Write((byte)0);b.Write(arrow?(ushort)7:row.InventoryIcon);b.Write((byte)1);b.Write(count);b.Write((byte)1);
        Text(b,arrow?(count==1?"$61":$"$61 ({count})"):row.CountName(count));b.Write((byte)0);b.Write(new byte[11]);
    });
    static byte[] ItemCount(uint id,uint count)=>Packet(b=>{b.Write((byte)24);b.Write(id);Text(b,count==1?"$61":$"$61 ({count})");b.Write(count);b.Write((byte)0);});
    static byte[] AttackReply(bool arrow=false)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)1);b.Write(123u);b.Write(456u);b.Write((ushort)10);b.Write((byte)2);
        if(arrow){b.Write(338u);b.Write((ushort)66);b.Write((byte)127);b.Write((ushort)32600);b.Write((ushort)32940);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(0u);b.Write((byte)0);}
        else{b.Write((ushort)0);b.Write((ushort)0);b.Write((byte)0);}
    });
    static Task<bool> Service(LoginConnection c,string command,int option=0,IReadOnlyList<ShopChoice>? choices=null)=>c.Interact(command,c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,option,choices);
    [CheckGroup("combat-loop"),CheckGroup(CheckGroups.Default,Order=260)]
    static async Task CombatLoop(){
        await KillAndLoot();await LearnAndCast();await Ammunition();await DeathAndRecovery();await RecoveryRaces();
    }
    static async Task KillAndLoot(){
        using var c=Seed();long now=10000;Set(c,"MovementClock",(Func<long>)(()=>now));Set(c,"ActionClock",(Func<long>)(()=>now));
        var status=RewardTests.Status(1,0);BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(1),123);BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(16),100);Feed(c,status);
        Feed(c,LoginPackets.Pad([5,0]));var sent=new List<byte[]>();
        Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        for(int i=0;i<3;i++){Check(await c.Attack(),"manual consecutive melee "+i);Feed(c,AttackReply());now+=10000;}
        Check(sent.Count==3&&sent.All(p=>p[0]==229),"three explicit C229, no auto attack");
        Feed(c,Death(456));Check(c.World.TargetId==null&&!await c.Attack(),"kill clears target and rejects further attacks");
        Check(c.World.Experience!.Total==0&&c.World.Inventory.Length==0,"death cannot synthesize rewards");
        var exp=LoginPackets.Pad([113,1,10,0,0,0,1]);Feed(c,exp);Feed(c,exp);
        Check(c.World.Experience!.Total==10&&c.World.ExperienceChanges==1&&c.World.LastExperienceChange!.Delta==10,"S113 absolute EXP and duplicate idempotence");
        GroundPacket(c,1);Check(c.World.SelectGround(900102),"loot ground selectable");
        Check(await c.SendGroundQuantity(true,"1",c.World.GroundQuantity(true)),"loot pickup admitted");
        Check(sent[^1][0]==112&&BinaryPrimitives.ReadUInt32LittleEndian(sent[^1].AsSpan(5))==900102&&c.World.Inventory.Length==0,"C112 identity and no predicted item grant");
        Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(900102u)]));Feed(c,Item(900103,1));Feed(c,Item(900103,1));
        Check(c.World.SelectedGround==null&&c.World.Inventory.Single().Count==1&&c.World.InventoryChanges.Count(x=>x.Delta==1)==1,"ground removed, authoritative loot added once");
    }
    static async Task LearnAndCast(){
        using var c=Seed();var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../.."));
        var scratch=Path.Combine(root,"scratch","issue-129");Directory.CreateDirectory(scratch);
        // Test-only authored metadata, not copied NPC resources.
        var catalog=SpellTable("220\r\n3 測試技能 (3/0)\r\n");// T462：合成的 spelldesc-c.tbl（不是客戶端資料）
        var templates=Path.Combine(scratch,"npc.json");File.WriteAllText(templates,"{\"test\":\"<a action='buyspell'>學習</a>\"}");
        using var spells=ClientSpellTable.Use(catalog);c.World.SkillCatalog=new(catalog);c.World.DialogTemplates=new(templates);var sent=new List<byte[]>();
        Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        Check(await Service(c,"talk")&&sent[^1][0]==34,"NPC talk C34");
        Feed(c,Packet(b=>{b.Write((byte)39);b.Write(456u);Text(b,"test");b.Write((ushort)0);b.Write((ushort)0);}));
        Check(await Service(c,"option")&&sent[^1][0]==145&&BinaryPrimitives.ReadUInt32LittleEndian(sent[^1].AsSpan(1))==456,"NPC buyspell sends C145");
        Feed(c,Packet(b=>{b.Write((byte)41);b.Write(6000u);b.Write((ushort)1);b.Write(3u);}));
        Check(c.World.ShopRows.Single().Price==6000&&c.World.ValidateService([new(3,1)]),"S41 zero-based skill offer and price");
        Check(!await Service(c,"shop",choices:[new(4,1)]),"unoffered skill rejected");
        Check(await Service(c,"shop",choices:[new(3,1)]),"learn offered skill");
        Check(sent[^1][0]==39&&BinaryPrimitives.ReadUInt16LittleEndian(sent[^1].AsSpan(1))==1&&BinaryPrimitives.ReadUInt32LittleEndian(sent[^1].AsSpan(3))==3,"C39 H count D zero-based id");
        Check(!c.World.SelectMagic(4)&&!await c.CastSelectedMagic()&&!await Service(c,"shop",choices:[new(3,1)]),"no predicted skill or duplicate submission");
        var masks=new byte[32];masks[0]=164;masks[1]=32;masks[2]=8;Feed(c,masks);Feed(c,masks);
        Check(c.World.LearnedMagic.Length==1&&c.World.InteractionNotice.Contains("1/1"),"S164 acknowledges learned skill once");
        Check(c.World.SelectMagic(4)&&c.World.SelectMagicTarget(456)&&await c.CastSelectedMagic()&&sent[^1][0]==6&&sent[^1][2]==3,"learned skill can cast C6");
        masks[0]=160;Feed(c,masks);Check(!c.World.SelectMagic(4)&&!await c.CastSelectedMagic(),"removed skill cannot cast");
    }
    static async Task Ammunition(){
        using var c=Seed();long now=10000;Set(c,"MovementClock",(Func<long>)(()=>now));Set(c,"ActionClock",(Func<long>)(()=>now));Feed(c,Weapon(20));Feed(c,Item(900201,2,true,true));
        int sends=0;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b[0]==123,"bow C123");sends++;a();return Task.FromResult(true);});
        Check(await c.Attack()&&!await c.Attack()&&sends==1&&c.World.ArrowCount==2,"one shot and cooldown without predicted consumption");
        Feed(c,AttackReply(true));Check(c.World.LastProjectile is {Gfx:66,Target:456}&&c.World.ArrowCount==2,"S30 projectile does not consume ammunition");
        Feed(c,ItemCount(900201,1));Feed(c,ItemCount(900201,1));Check(c.World.ArrowCount==1,"absolute count decreases exactly one; duplicate not additive");
        now+=10000;Check(await c.Attack()&&sends==2,"last arrow can shoot");Feed(c,LoginPackets.Pad([57,..BitConverter.GetBytes(900201u)]));now+=10000;
        Check(!c.CombatAvailable&&!await c.Attack()&&sends==2&&c.World.CombatNotice.Contains("箭矢"),"empty ammunition rejects send");
        Feed(c,ItemCount(900201,99));Check(c.World.ArrowCount==0,"late count cannot recreate removed ammunition");
        Feed(c,Item(900201,1,true));var release=new TaskCompletionSource<bool>();Sender(c,async(b,v,a)=>{await release.Task;if(!v())return false;sends++;a();return true;});
        var pending=c.Attack();Feed(c,ItemCount(900201,0));release.SetResult(true);Check(!await pending&&sends==2,"queued shot rechecks depletion");
        c.World.Reset();Feed(c,Item(900201,1,true,true));Check(c.World.ArrowCount==1&&c.World.InventoryChanges.Length==0,"relogin uses server inventory baseline only; DB persistence not claimed");
    }
    static async Task DeathAndRecovery(){
        using var c=Seed();long now=10000;Set(c,"MovementClock",(Func<long>)(()=>now));Set(c,"ActionClock",(Func<long>)(()=>now));var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        Check(!await c.RestartAfterDeath(),"alive role cannot restart");Feed(c,Hp(0));Feed(c,Death(123));
        Check(c.World.OwnDead&&c.World.TargetId==null&&!c.MovementAvailable&&!await c.Move(2)&&!await c.Attack()&&!await c.CastSelectedMagic(),"death locks actions");
        Feed(c,Pack(123));Feed(c,Hp(50));Check(c.World.OwnDead&&!c.MovementAvailable,"late pack and HP alone cannot revive");
        Check(await c.RestartAfterDeath()&&sent.Count==1&&sent[0].SequenceEqual(LoginPackets.Pad([177]))&&c.World.OwnDead,"C177 once, no local resurrection");
        Check(!await c.RestartAfterDeath()&&sent.Count==1,"pending restart duplicate rejected");
        Feed(c,LoginPackets.Pad([206,0,0,0]));Check(!c.MovementAvailable,"map alone cannot restore movement");
        Feed(c,Pack(123));Feed(c,Weapon(0));Check(c.MovementAvailable&&!c.World.RestartRequested,"server map/self/HP restores controls");
        Check(await c.Turn(2)&&sent[^1][0]==225,"fresh movement request accepted after restart");
        Feed(c,Hp(0));Feed(c,Death(123));var resurrection=Resurrection();
        for(int n=1;n<resurrection.Length;n++){
            bool failed=false;try{Feed(c,resurrection[..n]);}catch(ProtocolException){failed=true;}
            Check(failed&&c.World.OwnDead,"truncated resurrection atomic "+n);
        }
        Feed(c,Resurrection(999));Check(c.World.OwnDead,"foreign resurrection cannot revive self");
        Feed(c,resurrection);Check(!c.World.OwnDead&&!c.MovementAvailable&&c.World.SelfAppearancePending,"S85 waits for HP and S119");
        Feed(c,Hp(50));Check(!c.MovementAvailable,"HP cannot replace missing weapon update");
        Feed(c,Weapon(0));Check(c.MovementAvailable,"S85 plus HP/S119 restores movement");
        int revision=c.World.AppearanceRevision;Feed(c,resurrection);Check(c.World.AppearanceRevision==revision&&!c.World.SelfAppearancePending,"duplicate resurrection cannot relock live actor");
        now+=10000;var actor=c.World.Actor!;int heading=Enumerable.Range(0,8).First(h=>MovementRules.Passable(actor.X,actor.Y,h));
        Check(await c.Move(heading)&&sent[^1][0]==29,"recovered role sends fresh C29 movement");
        c.World.SelectTarget(456);Feed(c,Pack(123,action:8));Check(c.World.OwnDead&&c.World.TargetId==null&&!c.MovementAvailable,"full corpse pack also locks actions and clears target");
    }
    static async Task RecoveryRaces(){
        foreach(bool restart in new[]{false,true}){
            using var c=Seed();var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,v,a)=>{await release.Task;if(!v())return false;sends++;a();return true;});
            if(restart)Feed(c,Death(123));var pending=restart?c.RestartAfterDeath():c.Attack();
            if(!restart)Feed(c,Death(123));Feed(c,Resurrection());Feed(c,Hp(50));Feed(c,Weapon(0));
            release.SetResult(true);Check(!await pending&&sends==0,"death/revive cancels old queued request "+restart);
        }
        using(var c=Seed()){
            var release=new TaskCompletionSource<bool>();int sends=0;Feed(c,Death(123));
            Sender(c,async(b,v,a)=>{await release.Task;if(!v())return false;sends++;a();return true;});
            var pending=c.RestartAfterDeath();c.Cancel();release.SetResult(true);
            Check(!await pending&&sends==0&&!c.World.RestartRequested,"disconnect cancels queued restart");
        }
    }
}
