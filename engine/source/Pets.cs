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

public static class PetPackets {
    public static readonly string[] Commands = ["aggressive", "defensive", "stay", "extend", "alert", "dismiss"];
    public static byte[] Menu(uint pet) { if(pet==0)throw new ArgumentException("pet identity");return LoginPackets.Pad([103,..BitConverter.GetBytes(pet)]); }
    public static byte[] UseItem(uint pet, byte data, int index) {
        if(pet==0 || data is not (2 or 0x16) || index is <0 or >255)throw new ArgumentException("pet item");
        return LoginPackets.Pad([104,data,..BitConverter.GetBytes(pet),(byte)index]);
    }
    public static byte[] Command(uint pet, string command) {
        if(pet==0 || !Commands.Contains(command,StringComparer.Ordinal))throw new ArgumentException("pet command");
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
    void CheckPets() {
        if(petEpoch!=WorldEpoch){petEpoch=WorldEpoch;petBags.Clear();petInfo.Clear();petCollars=[];petRevision++;petNotice="世界已更新，請重新選取與查詢。";}
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
        if(html is not ("anicom" or "moncom"))return false;
        var info=PetPackets.ReadInfo(id,html,r);CheckPets();
        if(objects.TryGetValue(id,out var o)&&OwnedCompanion(o)){petInfo[id]=info;petRevision++;petNotice="已收到原服寵物／召喚物資訊。";}
        return true;
    }
    internal void PetSubmitted(uint id,string command) {
        CheckPets();petRevision++;
        if(command=="item"||command=="refresh")petBags.Remove(id);
        petNotice=command=="refresh"?"已查詢寵物背包，等待原服回覆。":"已送出一次；請核對原服結果。裝備操作後請重新查詢背包。";
    }
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
        if(command=="refresh")bytes=PetPackets.Menu(stamp.PetId);
        else if(command=="item"){
            var bag=World.PetInventory(stamp.PetId);
            if(bag==null||itemIndex<0||itemIndex>255||itemIndex>=bag.Items.Length||bag.Items[itemIndex] is not {Usable:true} item||item.ItemId!=itemId)return false;
            bytes=PetPackets.UseItem(stamp.PetId,item.Type,itemIndex);
        }else if(PetPackets.Commands.Contains(command))bytes=PetPackets.Command(stamp.PetId,command);
        else return false;
        int session=SessionGeneration;var sender=sendMovementGate;petSendingGeneration=session;
        try{return await sender(bytes,()=>PetCurrent(stamp),()=>World.PetSubmitted(stamp.PetId,command))&&session==SessionGeneration;}
        catch(Exception){if(session==SessionGeneration){Cancel();Set(LoginState.Faulted,"寵物操作傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(petSendingGeneration==session)petSendingGeneration=-1;}
    }
}
