using System.Text;
namespace P06;

public sealed record CharacterCreation(string Name,byte Class,byte Sex,byte Strength,byte Dexterity,byte Constitution,byte Wisdom,byte Charisma,byte Intelligence) {
    public byte[] Stats=>[Strength,Dexterity,Constitution,Wisdom,Charisma,Intelligence];
}

// Wire rules from the inspected server; deployment-specific class switches and bad names remain server-authoritative.
public static class CharacterManagementPackets {
    public static IReadOnlyList<string> Classes {get;}=Array.AsReadOnly(new[]{"王族","騎士","精靈","法師","黑暗精靈","龍騎士","幻術師"});
    static readonly byte[][] bases=[ [13,10,10,11,13,10], [16,12,14,9,12,8], [11,12,12,12,9,12], [8,7,12,12,8,12], [12,15,8,10,9,11], [13,11,14,12,8,11], [11,10,12,12,8,12] ];
    public static byte[] BaseStats(int type)=>type is >=0 and <7?(byte[])bases[type].Clone():throw new ArgumentOutOfRangeException(nameof(type));
    public static int AvailablePoints(int type)=>75-BaseStats(type).Sum(n=>(int)n);
    const string Banned="あアいイうウえエおオかカきキくクけケこコさサしシすスせセそソたタちチつツてテとトなナにニぬヌねネのノはハひヒふフへヘほホまマみミむムめメもモやヤゆユよヨらラりリるルれレろロわワをヲんン丶\uE6C2\uE6C1-/+*?!@#$%^&()[]<>{};:'\",.~`";
    public static string NormalizeName(string name)=>new(name.Where(c=>c is not (' ' or '\t' or '\n' or '\r' or '\f' or '\v' or '　')).ToArray());
    public static string? Validate(CharacterCreation request,int count,int slots){
        if(count>=slots)return "角色欄位已滿。";
        if(request.Class>=7||request.Sex>1)return "請選擇有效的職業與性別。";
        string name=NormalizeName(request.Name);
        // Backslash is refused locally: the server rewrites it through quoteReplacement.
        if(name.Length==0||name.Any(c=>char.IsControl(c)||c=='\\'||Banned.Contains(c)))return "名稱包含禁止字元或為空白。";
        try{int length=LoginPackets.TextEncoding.GetByteCount(name);if(length>12||length-name.Length>5)return "名稱最多 12 個 Big5 位元組，其中雙位元組字最多 5 字。";}
        catch(EncoderFallbackException){return "名稱含有 Big5 無法表示的字元。";}
        byte[] minimum=BaseStats(request.Class),stats=request.Stats;int points=AvailablePoints(request.Class);
        if(stats.Sum(v=>(int)v)!=75||stats.Where((v,i)=>v<minimum[i]||v>minimum[i]+points).Any())return "六項能力總和必須為 75，且不得低於職業基礎值或超過可分配點數。";
        return null;
    }
    public static byte[] Create(CharacterCreation request,int count,int slots){
        if(Validate(request,count,slots) is {} error)throw new ArgumentException(error);
        return LoginPackets.Pad([84,..LoginPackets.TextEncoding.GetBytes(NormalizeName(request.Name)),0,request.Class,request.Sex,..request.Stats]);
    }
    public static byte[] Delete(string name)=>LoginPackets.Pad([162,..LoginPackets.TextEncoding.GetBytes(name),0]);
    public static byte[] Switch()=>LoginPackets.Pad([7]);
    public static byte[] Refresh()=>LoginPackets.Pad([16]);
    public static bool PendingDeletion(LoginCharacter character)=>character.Class is >=32 and <=38;
    public static string Display(LoginCharacter character)=>character.Name+(PendingDeletion(character)?"（七日待刪除）":"");
}

public sealed partial class LoginConnection {
    enum CharacterOperation { None,Create,CreateDetails,Delete,Switch,RefreshCount,RefreshItems }
    CharacterOperation characterOperation;
    int characterLease;CancellationTokenSource? characterDeadline;
    internal int CharacterManagementTimeoutMs=15000;
    CharacterCreation? creatingCharacter;
    readonly List<LoginCharacter> refreshedCharacters=[];
    int refreshedCount,refreshedSlots;
    public string CharacterNotice {get;private set;}="";
    public bool CharacterManagementPending=>characterOperation!=CharacterOperation.None;
    public bool CanCreateCharacter=>State==LoginState.CharacterList&&sendMovementGate!=null&&characters.Count<Slots;
    public bool CanDeleteCharacter=>State==LoginState.CharacterList&&sendMovementGate!=null&&characters.Count>0;
    public bool CanSwitchCharacter=>State==LoginState.InWorld&&World.Actor!=null&&!worldProbeSending&&sendMovementGate!=null;
    bool CharacterManagementRead=>State is LoginState.ManagingCharacters or LoginState.SwitchingCharacter;
    void ResetCharacterManagement(){characterLease++;characterDeadline?.Cancel();characterDeadline?.Dispose();characterDeadline=null;characterOperation=CharacterOperation.None;creatingCharacter=null;refreshedCharacters.Clear();CharacterNotice="";}
    void CompleteCharacterManagement(string notice){ResetCharacterManagement();CharacterNotice=notice;Set(LoginState.CharacterList,notice);}
    bool CurrentCharacterOperation(int session,int lease)=>session==generation&&lease==characterLease&&CharacterManagementRead&&CharacterManagementPending;
    async Task CharacterManagementTimeout(int session,int lease,CancellationToken token){
        try{await Task.Delay(CharacterManagementTimeoutMs,token);if(CurrentCharacterOperation(session,lease)){Cancel();Set(LoginState.Faulted,"角色操作回覆逾時；連線已關閉，請重新登入確認結果，沒有自動重送。");}}
        catch(OperationCanceledException) when(token.IsCancellationRequested){}
    }
    public Task<LoginAction> CreateCharacter(CharacterCreation request,int session,int revision){
        if(session!=generation||revision!=Revision||!CanCreateCharacter)return Task.FromResult(new LoginAction(false,"角色清單已變更或沒有空位，請重新確認。"));
        if(CharacterManagementPackets.Validate(request,characters.Count,Slots) is {} error)return Task.FromResult(new LoginAction(false,error));
        request=request with{Name=CharacterManagementPackets.NormalizeName(request.Name)};
        return SendCharacterOperation(CharacterOperation.Create,CharacterManagementPackets.Create(request,characters.Count,Slots),request);
    }
    public Task<LoginAction> DeleteCharacter(string name,bool confirmed,int session,int revision){
        if(!confirmed)return Task.FromResult(new LoginAction(false,"已取消刪除。"));
        if(session!=generation||revision!=Revision||!CanDeleteCharacter||!characters.Any(c=>c.Name==name))return Task.FromResult(new LoginAction(false,"角色清單已變更，請重新選取並確認刪除。"));
        return SendCharacterOperation(CharacterOperation.Delete,CharacterManagementPackets.Delete(name));
    }
    public Task<LoginAction> SwitchCharacter(int session,int revision){
        if(session!=generation||revision!=Revision||!CanSwitchCharacter)return Task.FromResult(new LoginAction(false,"目前不能切換角色，請依最新狀態重試。"));
        return SendCharacterOperation(CharacterOperation.Switch,CharacterManagementPackets.Switch());
    }
    void RetireCharacterWorld(){
        restartSending=false;ResetPlayerChatSend();ResetPrivateShopSend();ResetBonusStatsSend();ResetWorldEntry();ResetTeleportSend();ResetShipSend();tradeSendLease++;tradeSending=false;interactionSending=false;
        ResetMovementSend();ResetCombatSend();ResetMagicSend();ResetItemSend();ResetGroundSend();World.Reset();worldSelection=null;
    }
    async Task<LoginAction> SendCharacterOperation(CharacterOperation operation,byte[] bytes,CharacterCreation? creation=null){
        int session=generation,lease=++characterLease;var sender=sendMovementGate!;
        characterOperation=operation;creatingCharacter=creation;
        CharacterNotice=operation switch{CharacterOperation.Create=>"正在建立角色，等待伺服器結果。",CharacterOperation.Delete=>"正在處理刪除，等待伺服器結果。",_=>"正在原連線切換角色，等待伺服器選角清單。"};
        if(operation==CharacterOperation.Switch)RetireCharacterWorld();
        Set(operation==CharacterOperation.Switch?LoginState.SwitchingCharacter:LoginState.ManagingCharacters,CharacterNotice);
        if(!CurrentCharacterOperation(session,lease))return new(false,"操作已取消，未送出。");
        characterDeadline=new();_ = CharacterManagementTimeout(session,lease,characterDeadline.Token);
        try{
            bool sent=await sender(bytes,()=>CurrentCharacterOperation(session,lease),()=>{});
            if(!sent&&CurrentCharacterOperation(session,lease)){Cancel();Set(LoginState.Faulted,"角色操作未送出；連線已關閉，請重新登入。");}
            return new(sent&&session==generation,sent?"請求已送出，等待伺服器確認。":"操作已取消，未送出。");
        }catch{if(CurrentCharacterOperation(session,lease)){Cancel();Set(LoginState.Faulted,"角色操作傳送失敗；請重新登入確認結果，沒有自動重送。");}return new(false,"角色操作未完成，請查看連線狀態。");}
    }
    // The sole receive hook runs before world dispatch. Old world packets during C7 retirement cannot repopulate a new world.
    async Task<bool> ReceiveCharacterManagement(byte[] plain,LoginProfile profile,long sequence){
        if(!CharacterManagementRead)return false;
        PacketTrace.In(plain,sequence);PacketReceived?.Invoke(new{kind="server-packet",sequence,plainHex=Convert.ToHexString(plain)});
        if(!CharacterManagementRead)return true;
        var r=new LoginReader(plain);byte opcode=r.C();
        if(opcode==227)throw new ProtocolException("server disconnect during character management");
        if(characterOperation==CharacterOperation.Switch){
            if(opcode==250&&r.C()==42){
                for(int i=0;i<6;i++)if(r.C()!=0)throw new ProtocolException("invalid character selection marker");r.Finish();
                characterOperation=CharacterOperation.RefreshCount;Set(LoginState.ManagingCharacters,CharacterNotice);
            }else if(opcode is 178 or 93)throw new ProtocolException("character list before switch acknowledgement");
            AppliedPacketSequence=sequence;return true;
        }
        if(characterOperation==CharacterOperation.Create&&opcode==98){
            byte reason=r.C();r.Finish();
            if(reason==2){characterOperation=CharacterOperation.CreateDetails;CharacterNotice="伺服器已接受建立，等待新角色資料。";Set(State,CharacterNotice);}
            else CompleteCharacterManagement(reason switch{6=>"角色名稱已有人使用。",9=>"伺服器拒絕角色名稱。",21=>"伺服器拒絕建立：請確認欄位數、能力值與職業開放設定。",_=>$"伺服器拒絕建立角色（代碼 {reason}）。"});
        }else if(characterOperation==CharacterOperation.CreateDetails&&opcode==127){
            var item=LoginPackets.Character(r,profile);var request=creatingCharacter!;
            if(item.Name!=request.Name||item.Class!=request.Class||item.Sex!=request.Sex||characters.Any(c=>c.Name==item.Name)||characters.Count>=Slots)throw new ProtocolException("unexpected created character identity");
            characters.Add(item);Expected=characters.Count;CompleteCharacterManagement("角色已建立，可從清單選擇進入世界。");
        }else if(characterOperation==CharacterOperation.Delete&&opcode==6){
            byte result=r.C();r.Finish();
            if(result is not (5 or 81)){CompleteCharacterManagement($"伺服器未確認刪除（代碼 {result}）；角色清單保留。");return true;}
            CharacterNotice=result==5?"角色已刪除；正在更新清單。":"七日刪除狀態已切換；正在更新清單。";
            characterOperation=CharacterOperation.RefreshCount;Set(State,CharacterNotice);
            int session=generation,lease=characterLease;
            if(sendMovementGate is not {} sender||!await sender(CharacterManagementPackets.Refresh(),()=>CurrentCharacterOperation(session,lease),()=>{}))return true;
        }else if(characterOperation==CharacterOperation.RefreshCount&&opcode==178){
            int count=r.C(),slots=r.C();r.Finish();if(count>slots)throw new ProtocolException("character count exceeds slots");
            refreshedCount=count;refreshedSlots=slots;refreshedCharacters.Clear();characterOperation=CharacterOperation.RefreshItems;
            if(count==0)CommitRefreshedCharacters();
        }else if(characterOperation==CharacterOperation.RefreshItems&&opcode==93){
            var item=LoginPackets.Character(r,profile);if(refreshedCharacters.Any(c=>c.Name==item.Name))throw new ProtocolException("duplicate character name");
            refreshedCharacters.Add(item);if(refreshedCharacters.Count==refreshedCount)CommitRefreshedCharacters();
        }else throw new ProtocolException($"unexpected character-management opcode {opcode}");
        AppliedPacketSequence=sequence;return true;
    }
    void CommitRefreshedCharacters(){
        characters.Clear();characters.AddRange(refreshedCharacters);Expected=refreshedCount;Slots=refreshedSlots;
        CompleteCharacterManagement("角色清單已更新；標示七日待刪除的角色可再次按刪除以取消排程。");
    }
}
