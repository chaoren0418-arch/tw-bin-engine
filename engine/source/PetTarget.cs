namespace P06;

// M7-W（T223／#530）：寵物指定攻擊目標（TW16 op 228 S_COMMAND_TARGET；3.81 對應 S236 S_SelectTarget／C223 C_SelectTarget）。
// 協定依 docs/reference/server-protocol.md（#132）：
//   S236 S_SelectTarget   C(236) D(objectId) C(0) C(0) C(2)
//   C223 C_SelectTarget   C(223) D(petId) C(未用) D(targetId)
// 原服流程（L1ActionPet "attackchr"）：玩家在寵物選單送 C125 D(pet) S("attackchr") → 原服回 S236 D(pet) → 客戶端進入選目標模式，
// 玩家點選目標後送 C223 D(pet) C0 D(target)。原服只對 WorldPet（寵物，不含召喚物）處理，並驗證主人、地圖、可見性與安全區。
// S236 D(自己) 是 /check、掉落查詢的選取模式（PlayerQuery），不是寵物；本模組不消化，保留原本的未處理計數。
// 本機作法：選目標模式中，下一次「選取目標」（TargetRevision 改變）就送一次 C223。原版游標外觀與點擊時是否同時攻擊／移動，待查證。

public sealed record PetTargetRequest(uint PetId,int Epoch,int ArmedTargetRevision,long Revision);
public sealed record PetTargetView(int Generation,int Epoch,long Revision,uint? PetId,string PetName,uint? Candidate,bool CanSend,string Notice);

public static class PetTargetPackets {
    public const byte SSelect=236,CSelect=223;
    public static uint DecodeSelect(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SSelect)throw new ProtocolException("select target opcode");
        uint id=r.D();
        if(r.C()!=0||r.C()!=0||r.C()!=2)throw new ProtocolException("select target tail");
        r.Finish();
        if(id==0)throw new ProtocolException("select target identity");
        return id;
    }
    public static byte[] Select(uint pet,uint target){
        if(pet==0||target==0||pet==target)throw new ArgumentException("pet target");
        return LoginPackets.Pad([CSelect,..BitConverter.GetBytes(pet),0,..BitConverter.GetBytes(target)]);
    }
}

public sealed partial class WorldSession {
    PetTargetRequest? petTarget;long petTargetRevision;string petTargetNotice="";int petTargetIgnored;

    [WorldReceive(PetTargetPackets.SSelect)]
    bool ReceivePetSelectTarget(byte[] plain){
        uint id=PetTargetPackets.DecodeSelect(plain);
        if(Actor is {} self&&id==self.ObjectId)return false;   // 玩家查詢／掉落查詢的選取模式
        CheckPets();
        if(!objects.TryGetValue(id,out var o)||!OwnedCompanion(o)){petTargetIgnored++;return true;}
        petTarget=new(id,WorldEpoch,TargetRevision,++petTargetRevision);
        petTargetNotice="原服要求為寵物指定攻擊目標：請點選目標，選取後送出一次。";
        return true;
    }
    void CheckPetTarget(){
        if(petTarget is not {} p)return;
        if(p.Epoch!=WorldEpoch||!objects.TryGetValue(p.PetId,out var o)||!OwnedCompanion(o)){
            petTarget=null;petTargetRevision++;petTargetNotice="寵物已不在或世界已更新，指定目標已取消。";
        }
    }
    public PetTargetRequest? PetTargetState{get{CheckPetTarget();return petTarget;}}
    /// <summary>選目標模式中、S236 之後新選的目標（不能是寵物自己、自己的角色或已死亡物件）。</summary>
    internal uint? PetTargetCandidate(){
        if(PetTargetState is not {} p||TargetRevision==p.ArmedTargetRevision||TargetId is not uint t)return null;
        if(t==p.PetId||t==Actor?.ObjectId||!objects.TryGetValue(t,out var o)||o.Dead)return null;
        return t;
    }
    internal bool PetTargetCurrent(long revision,uint pet,uint target)=>PetTargetState is {} p&&p.Revision==revision&&p.PetId==pet&&PetTargetCandidate()==target;
    internal void PetTargetSubmitted(long revision,uint target){
        if(petTarget is not {} p||p.Revision!=revision)return;
        petTarget=null;petTargetRevision++;petTargetNotice=$"已送出寵物攻擊目標（物件 {target}）一次；寵物行動以原服為準。";
    }
    /// <summary>只清本機選目標模式；原服可能仍在等 C223（下一個 C223 才會被消耗）。</summary>
    internal bool CancelPetTarget(long revision){
        if(PetTargetState is not {} p||p.Revision!=revision)return false;
        petTarget=null;petTargetRevision++;petTargetNotice="已取消指定目標（只清除本機狀態）。";return true;
    }
    internal PetTargetView PetTargetSnapshot(int generation,bool access){
        var p=PetTargetState;uint? candidate=PetTargetCandidate();
        string name=p!=null&&objects.TryGetValue(p.PetId,out var o)?o.Name:"";
        return new(generation,WorldEpoch,petTargetRevision,p?.PetId,name,candidate,access&&candidate!=null,petTargetNotice);
    }
    [WorldReset] void ResetPetTarget(){petTarget=null;petTargetRevision++;petTargetNotice="";petTargetIgnored=0;}
    [WorldSnapshot("petTarget")] object? PetTargetSnapshotModel()=>new{revision=petTargetRevision,notice=petTargetNotice,ignored=petTargetIgnored,
        pending=petTarget is {} p?new{petId=p.PetId,epoch=p.Epoch,armedTargetRevision=p.ArmedTargetRevision,revision=p.Revision}:null};
}

public sealed partial class LoginConnection {
    int petTargetSendingGeneration=-1;
    bool PetTargetAccess=>PetAccess&&petTargetSendingGeneration!=SessionGeneration&&!World.IsGhost;
    public PetTargetView PetTargetSnapshot()=>CaptureWorld(()=>World.PetTargetSnapshot(SessionGeneration,PetTargetAccess));
    public bool CancelPetTarget(PetTargetView v)=>CaptureWorld(()=>v.Generation==SessionGeneration&&v.Epoch==World.WorldEpoch&&World.CancelPetTarget(v.Revision));
    /// <summary>為 v 的寵物與已選目標送一次 C223。v 必須是目前快照，且目標是 S236 之後新選的。</summary>
    public async Task<bool> SendPetTarget(PetTargetView v){
        if(v.PetId is not uint pet||v.Candidate is not uint target||petTargetSendingGeneration==SessionGeneration||sendMovementGate is not {} sender)return false;
        bool Valid()=>CaptureWorld(()=>v.Generation==SessionGeneration&&v.Epoch==World.WorldEpoch&&PetAccess&&!World.IsGhost&&World.PetTargetCurrent(v.Revision,pet,target));
        if(!Valid())return false;
        int session=SessionGeneration;petTargetSendingGeneration=session;
        try{return await sender(PetTargetPackets.Select(pet,target),Valid,()=>CaptureWorld(()=>{World.PetTargetSubmitted(v.Revision,target);return true;}))&&session==SessionGeneration;}
        catch(Exception){if(session==SessionGeneration){Cancel();Set(LoginState.Faulted,"寵物目標傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(petTargetSendingGeneration==session)petTargetSendingGeneration=-1;}
    }
}
