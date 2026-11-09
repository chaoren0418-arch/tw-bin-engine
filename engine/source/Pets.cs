namespace P06;

public sealed record PetCollar(uint ItemId, byte Count);
public sealed record PetItem(uint ItemId, byte Type, ushort Icon, byte Bless, uint Count, byte Identified, string Name) {
    public bool Equipped => (Identified & 2) != 0;
    public bool Usable => Type is 2 or 0x16;
}
public sealed record PetBag(uint PetId, sbyte Ac, PetItem[] Items);
public sealed record PetInfo(uint PetId, string Kind, string Mode, uint Hp, uint MaxHp, uint Mp, uint MaxMp, byte Level, string Name);
public sealed record PetStamp(int Generation, int Epoch, int Revision, int ObjectRevision, uint ActorId, uint PetId);
public sealed record PetView(PetStamp Stamp, string Name, int? HpPercent, PetInfo? Info, PetBag? Bag, bool CanSend, string Notice);

// M7-W（T223／#530）：召喚怪物選擇的請求。Source="server"＝原服 S39 summonlist（回 C125 D(objid) S(action)）；
// "local"＝本機入口（施放技能 51 時 C6 尾段 D 帶召喚編號，原服在裝備召喚控制戒指時採用）。
public sealed record SummonRequest(uint ObjectId,string Source,long Revision);
public sealed record SummonView(int Generation,int Epoch,long Revision,bool Open,string Source,uint ObjectId,int? Level,int? Charisma,bool CanSend,bool SkillLearned,string Notice);

public static class PetPackets {
    public static readonly string[] Commands = ["aggressive", "defensive", "stay", "extend", "alert", "dismiss"];
    /// <summary>原服 L1ActionPet：只對寵物（不含召喚物）回 S236 SelectTarget，之後客戶端送 C223（PetTarget.cs）。</summary>
    public const string TargetCommand = "attackchr";
    /// <summary>召喚選擇的 action 是 summonList.xml 的數字字串（例如 "7"、"263"），原服以數字比對。</summary>
    public static bool ValidSummonAction(string action) => action.Length is >=1 and <=6 && action.All(char.IsAsciiDigit) && uint.TryParse(action,out uint v) && v>0;
    public static byte[] SummonChoice(uint self, string action) {
        if(self==0||!ValidSummonAction(action))throw new ArgumentException("summon choice");
        return NpcPackets.Action(self,action);
    }
    public static byte[] Information(uint pet) { if(pet==0)throw new ArgumentException("pet identity");return NpcPackets.Talk(pet); }
    public static byte[] Menu(uint pet) { if(pet==0)throw new ArgumentException("pet identity");return LoginPackets.Pad([103,..BitConverter.GetBytes(pet)]); }
    public static byte[] UseItem(uint pet, byte data, int index) {
        if(pet==0 || data is not (2 or 0x16) || index is <0 or >255)throw new ArgumentException("pet item");
        return LoginPackets.Pad([104,data,..BitConverter.GetBytes(pet),(byte)index]);
    }
    public static byte[] Command(uint pet, string command) {
        if(pet==0 || !Commands.Contains(command,StringComparer.Ordinal)&&command!=TargetCommand)throw new ArgumentException("pet command");
        return NpcPackets.Action(pet,command);
    }
    public static (uint Price,PetCollar[] Rows) ReadList(LoginReader r) {
        uint price=r.D();int count=r.H();if(count>2048)throw new ProtocolException("pet list count");
        var rows=new PetCollar[count];var ids=new HashSet<uint>();
        for(int i=0;i<count;i++){uint id=r.D();byte amount=r.C();if(id==0||!ids.Add(id))throw new ProtocolException("pet collar identity");rows[i]=new(id,amount);}
        r.Finish();return (price,rows);
    }
    public static PetBag ReadBag(LoginReader r) {
        uint pet=r.D();int count=r.H();if(pet==0||count>2048||r.C()!=11)throw new ProtocolException("pet bag header");
        var rows=new PetItem[count];var ids=new HashSet<uint>();
        for(int i=0;i<count;i++){
            uint id=r.D();byte type=r.C();ushort icon=r.H();byte bless=r.C();uint amount=r.D();byte identified=r.C();string name=r.S();
            if(id==0||!ids.Add(id)||amount>2000000000||identified>3)throw new ProtocolException("pet bag row");
            rows[i]=new(id,type,icon,bless,amount,identified,name);
        }
        sbyte ac=unchecked((sbyte)r.C());r.Finish();return new(pet,ac,rows);
    }
    // S39 companion menus use C(flag), unlike the generic NPC H(flag) envelope.
    public static PetInfo ReadInfo(uint id,string html,LoginReader r) {
        int count=html=="anicom"?10:html=="moncom"?6:0;
        if(id==0||count==0||r.C()!=0||r.H()!=count)throw new ProtocolException("pet menu header");
        var args=new string[count];for(int i=0;i<count;i++)args[i]=r.S();r.Finish();
        uint Number(int index){if(!uint.TryParse(args[index],out uint n))throw new ProtocolException("pet menu number");return n;}
        uint hp=Number(1),maxHp=Number(2),mp=Number(3),maxMp=Number(4),level=Number(5);
        if(hp>maxHp||mp>maxMp||level>255)throw new ProtocolException("pet menu vitals");
        return new(id,html=="anicom"?"pet":"summon",args[0],hp,maxHp,mp,maxMp,(byte)level,count==10?args[6]:"");
    }
    public static string OwnerName(string text) => System.Text.RegularExpressions.Regex.Replace(text,@"\f.","");
}

public sealed partial class WorldSession {
    int petEpoch=-1,petRevision;
    PetCollar[] petCollars=[];
    readonly Dictionary<uint,PetBag> petBags=new();
    readonly Dictionary<uint,PetInfo> petInfo=new();
    string petNotice="選擇自己的寵物或召喚物。";
    SummonRequest? summonRequest;long summonRevision;string summonNotice="";
    void CheckPets() {
        if(petEpoch!=WorldEpoch){petEpoch=WorldEpoch;petBags.Clear();petInfo.Clear();petCollars=[];petRevision++;petNotice="世界已更新，請重新選取與查詢。";
            if(summonRequest!=null){summonRequest=null;summonRevision++;summonNotice="世界已更新，召喚選擇已關閉。";}}
        foreach(uint id in petBags.Keys.Concat(petInfo.Keys).Distinct().ToArray())
            if(!objects.TryGetValue(id,out var o)||!OwnedCompanion(o)){petBags.Remove(id);petInfo.Remove(id);petRevision++;}
    }
    bool OwnedCompanion(WorldObject o)=>Actor is {} a&&o.ObjectId!=a.ObjectId&&!o.Dead&&!o.AppearancePending&&
        PetPackets.OwnerName(o.Owner)==a.Name&&o.Owner.Length>0;
    public WorldObject[] PetCompanions { get {CheckPets();return Objects.Where(o=>OwnedCompanion(o)&&(o.HpPercent!=null||petBags.ContainsKey(o.ObjectId)||petInfo.ContainsKey(o.ObjectId))).ToArray();} }
    public PetCollar[] PetCollars {get{CheckPets();return petCollars.ToArray();}}
    public int PetRevision {get{CheckPets();return petRevision;}}
    public string PetNotice {get{CheckPets();return petNotice;}}
    public PetBag? PetInventory(uint id){CheckPets();return petBags.TryGetValue(id,out var b)?b with{Items=b.Items.ToArray()}:null;}
    public PetInfo? PetInformation(uint id){CheckPets();return petInfo.GetValueOrDefault(id);}
    public bool PetNearby(uint id)=>PetCompanions.Any(o=>o.ObjectId==id&&Actor is {} a&&Math.Abs(a.X-o.X)<=5&&Math.Abs(a.Y-o.Y)<=5);
    void ReceivePetList(LoginReader r) {
        var list=PetPackets.ReadList(r);CheckPets();
        // The fixed server distinguishes pet collars (70) from repair weapons (200) by price.
        petCollars=list.Price==70?list.Rows:[];petRevision++;
        if(list.Price!=70){Unhandled(83);return;}
        petNotice=$"收到 {list.Rows.Length} 個可領取項圈（單價 70）；名稱依玩家背包顯示。";
    }
    void ReceivePetInventory(LoginReader r,byte[] plain) {
        if(plain.Length<8)throw new ProtocolException("retrieve header");
        if(plain[7]!=11){ReceiveInteraction(176,r);return;}
        var bag=PetPackets.ReadBag(r);CheckPets();
        if(!objects.TryGetValue(bag.PetId,out var o)||!OwnedCompanion(o))return;
        petBags[bag.PetId]=bag;petRevision++;petNotice="已收到原服寵物背包；點選裝備後只送出一次。";
    }
    bool ReceivePetDialog(byte[] plain) {
        var r=new LoginReader(plain);r.C();uint id=r.D();string html=r.S();
        if(html=="summonlist"){ReceiveSummonList(id,r);return true;}
        if(html is not ("anicom" or "moncom"))return false;
        var info=PetPackets.ReadInfo(id,html,r);CheckPets();
        if(objects.TryGetValue(id,out var o)&&OwnedCompanion(o)){petInfo[id]=info;petRevision++;petNotice="已收到原服寵物／召喚物資訊。";}
        return true;
    }
    internal void PetSubmitted(uint id,string command) {
        CheckPets();petRevision++;
        if(command=="item"||command=="refresh")petBags.Remove(id);
        if(command=="info"||command=="item")petInfo.Remove(id);
        petNotice=command switch {
            "info"=>"已查詢寵物／召喚物資訊，等待原服回覆；沒有回覆不代表數值為零。",
            "refresh"=>"已查詢寵物背包，等待原服回覆。",
            PetPackets.TargetCommand=>"已要求指定攻擊目標；原服回 S236 後請點選目標（只對寵物有效，召喚物原服不處理）。",
            _=>"已送出一次；請核對原服結果。裝備操作後請重新查詢背包。"
        };
    }

    // S39 S_ShowSummonList：C(39) D(objid) S("summonlist")，objid 是玩家自己（原服 SUMMON_MONSTER 目前把這行註解掉，
    // 仍照協定接收）。之後原服在 isSummonMonster 為真時，把 C125 的純數字 action 當召喚編號（L1ActionPc.summonMonster）。
    void ReceiveSummonList(uint id,LoginReader r) {
        r.Finish();CheckPets();summonRevision++;
        if(id==0||Actor is not {} a||id!=a.ObjectId){summonNotice="收到非本角色的召喚清單要求，已忽略。";return;}
        summonRequest=new(id,"server",summonRevision);summonNotice="原服要求選擇召喚怪物；選定後送出一次。";
    }
    public SummonRequest? SummonRequestState {get{CheckPets();return summonRequest;}}
    public long SummonRevision {get{CheckPets();return summonRevision;}}
    public string SummonNotice {get{CheckPets();return summonNotice;}}
    /// <summary>本機開啟召喚選擇（過渡入口；原版開窗時機待查證）。選定後走技能 51 的 C6（PetUi.cs）。</summary>
    internal bool OpenLocalSummon() {
        CheckPets();if(Actor is not {} a||Status?.ObjectId!=a.ObjectId)return false;
        summonRevision++;summonRequest=new(a.ObjectId,"local",summonRevision);summonNotice="選擇召喚怪物後，以技能 51 施放並帶入召喚編號（原服需裝備召喚控制戒指才採用）。";return true;
    }
    internal bool SummonCurrent(long revision)=>SummonRequestState is {} q&&q.Revision==revision;
    public bool CloseSummon(long revision) {
        if(!SummonCurrent(revision))return false;
        summonRequest=null;summonRevision++;summonNotice="已關閉召喚選擇。";return true;
    }
    internal void SummonSubmitted(long revision,string action) {
        if(!SummonCurrent(revision))return;
        summonRequest=null;summonRevision++;summonNotice=$"已送出召喚選擇（編號 {action}）一次；召喚結果以原服為準，不會自動重送。";
    }
    [WorldReset] void ResetSummon(){summonRequest=null;summonRevision++;summonNotice="";}
    [WorldSnapshot("summon")] object? SummonSnapshotModel()=>new{revision=summonRevision,notice=summonNotice,request=summonRequest is {} q?new{objectId=q.ObjectId,source=q.Source,revision=q.Revision}:null};
}

public sealed partial class LoginConnection {
    int petSendingGeneration=-1;
    bool PetAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {Hp:>0}&&!World.OwnDead&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy&&!MagicBusy&&!ItemBusy&&!interactionSending;
    public PetView PetSnapshot(uint id) {
        var obj=World.PetCompanions.FirstOrDefault(o=>o.ObjectId==id);
        return new(new(SessionGeneration,World.WorldEpoch,World.PetRevision,World.ObjectRevision,World.Status?.ObjectId??0,id),obj?.Name??"",obj?.HpPercent,World.PetInformation(id),World.PetInventory(id),PetAccess&&World.PetNearby(id)&&petSendingGeneration!=SessionGeneration,World.PetNotice);
    }
    bool PetCurrent(PetStamp stamp)=>stamp.Generation==SessionGeneration&&stamp.Epoch==World.WorldEpoch&&stamp.Revision==World.PetRevision&&stamp.ObjectRevision==World.ObjectRevision&&stamp.ActorId!=0&&stamp.ActorId==World.Status?.ObjectId&&stamp.ActorId==World.Actor?.ObjectId&&PetAccess&&World.PetNearby(stamp.PetId);
    public async Task<bool> SendPet(PetStamp stamp,string command,int itemIndex=-1,uint itemId=0) {
        if(petSendingGeneration==SessionGeneration||!PetCurrent(stamp)||sendMovementGate==null)return false;
        byte[] bytes;
        if(command=="info")bytes=PetPackets.Information(stamp.PetId);
        else if(command=="refresh")bytes=PetPackets.Menu(stamp.PetId);
        else if(command=="item"){
            var bag=World.PetInventory(stamp.PetId);
            if(bag==null||itemIndex<0||itemIndex>255||itemIndex>=bag.Items.Length||bag.Items[itemIndex] is not {Usable:true} item||item.ItemId!=itemId)return false;
            bytes=PetPackets.UseItem(stamp.PetId,item.Type,itemIndex);
        }else if(PetPackets.Commands.Contains(command)||command==PetPackets.TargetCommand)bytes=PetPackets.Command(stamp.PetId,command);
        else return false;
        int session=SessionGeneration;var sender=sendMovementGate;petSendingGeneration=session;
        try{return await sender(bytes,()=>PetCurrent(stamp),()=>World.PetSubmitted(stamp.PetId,command))&&session==SessionGeneration;}
        catch(Exception){if(session==SessionGeneration){Cancel();Set(LoginState.Faulted,"寵物操作傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(petSendingGeneration==session)petSendingGeneration=-1;}
    }

    int summonSendingGeneration=-1;
    bool SummonAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {Hp:>0}&&!World.OwnDead&&!World.IsGhost&&!World.TeleportPending&&!World.ActionLocked&&!World.TradeBlocksActions&&!interactionSending;
    public SummonView SummonSnapshot()=>CaptureWorld(()=>{
        var q=World.SummonRequestState;var s=World.Status;bool learned=World.LearnedMagic.Any(m=>m.Id==51);
        bool can=q!=null&&SummonAccess&&summonSendingGeneration!=SessionGeneration&&(q.Source=="server"||learned&&!MagicBusy&&!MovementBusy);
        return new SummonView(SessionGeneration,World.WorldEpoch,World.SummonRevision,q!=null,q?.Source??"",q?.ObjectId??0,s?.Level,s?.Charisma,can,learned,World.SummonNotice);
    });
    public bool OpenLocalSummon()=>State==LoginState.InWorld&&CaptureWorld(()=>World.OpenLocalSummon());
    public bool CloseSummon(SummonView v)=>CaptureWorld(()=>v.Generation==SessionGeneration&&v.Epoch==World.WorldEpoch&&World.CloseSummon(v.Revision));
    /// <summary>
    /// 送出一次召喚選擇。server：C125 D(自己) S(action)。local：選技能 51 並以 C6 D(action) 施放（SkillCast 的 Summon 目標）。
    /// v 必須是目前的快照；action 必須是數字字串（summonList.xml）。
    /// </summary>
    public async Task<bool> SendSummonChoice(SummonView v,string action) {
        if(!v.Open||!PetPackets.ValidSummonAction(action)||summonSendingGeneration==SessionGeneration)return false;
        bool Valid()=>CaptureWorld(()=>v.Generation==SessionGeneration&&v.Epoch==World.WorldEpoch&&World.SummonCurrent(v.Revision)&&World.SummonRequestState?.ObjectId==World.Actor?.ObjectId&&SummonAccess);
        if(!Valid())return false;
        int session=SessionGeneration;summonSendingGeneration=session;
        try{
            if(v.Source=="server"){
                if(sendMovementGate is not {} sender)return false;
                byte[] bytes=PetPackets.SummonChoice(v.ObjectId,action);
                return await sender(bytes,Valid,()=>CaptureWorld(()=>{World.SummonSubmitted(v.Revision,action);return true;}))&&session==SessionGeneration;
            }
            if(v.Source!="local"||!CaptureWorld(()=>World.SelectMagic(51)))return false;
            bool cast=await CastSelectedMagic(new SkillCastTarget(SkillTargetKind.Summon,SummonId:uint.Parse(action)));
            if(cast&&session==SessionGeneration)CaptureWorld(()=>{World.SummonSubmitted(v.Revision,action);return true;});
            return cast&&session==SessionGeneration;
        }
        catch(Exception) when(v.Source=="server"){if(session==SessionGeneration){Cancel();Set(LoginState.Faulted,"召喚選擇傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(summonSendingGeneration==session)summonSendingGeneration=-1;}
    }
}
