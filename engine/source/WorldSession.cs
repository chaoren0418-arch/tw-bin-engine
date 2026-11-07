using System.Buffers.Binary;
namespace P06;
public sealed record OwnStatus(uint ObjectId,byte Level,uint Exp,byte Strength,byte Intelligence,byte Wisdom,byte Dexterity,byte Constitution,byte Charisma,uint Hp,uint MaxHp,uint Mp,uint MaxMp,int Ac,uint Time,byte Food,byte Weight,short Lawful,short Fire,short Water,short Wind,short Earth,uint Kills);
public sealed record OwnActor(ushort X,ushort Y,uint ObjectId,ushort Gfx,byte Weapon,byte Direction,byte Light,byte Speed,uint Exp,short Lawful,string Name,string Title,byte Flags,uint ClanId,string Clan,string Owner,byte Rank,byte HpPercent,byte Brave,byte Level,byte Unknown,byte Tail1,byte Tail2);
public sealed partial class WorldSession {
    public bool Entered {get;private set;}
    public ushort? MapId {get;private set;}
    public bool Underwater {get;private set;}
    public OwnStatus? Status {get;private set;}
    public OwnActor? Actor {get;private set;}
    public int KeepalivesSent {get;set;}
    public int TimeReplies {get;private set;}
    public uint? ServerTime {get;private set;}
    public bool ProbeSent {get;set;}
    public bool CoreComplete=>Entered&&MapId!=null&&Status!=null&&Actor!=null&&Status.ObjectId==Actor.ObjectId;
    public bool Ready=>CoreComplete&&TimeReplies>0;
    public bool SupportedMap=>Actor!=null&&(CacheEnabled?MapResourceState=="ready"&&MapCache is {} loaded&&loaded.MapId==MapId&&loaded.Bounds.Contains(Actor.X,Actor.Y)&&loaded.Contains(Actor.X,Actor.Y):MapId==0&&Coordinates.Inside(Actor.X,Actor.Y));
    public bool SelfAppearancePending {get;private set;}
    public int AppearanceRevision {get;private set;}
    // S87 serializes status8 instead of currentWeapon for a dead player.
    public PlayerProfile? AppearanceProfile=>Actor is {} actor?PlayerProfiles.Get(actor.Gfx,actor.Weapon==8?0:actor.Weapon):null;
    public bool SupportedAppearance=>AppearanceProfile!=null&&!SelfAppearancePending;
    public string AppearanceNotice=>Actor==null?"等待角色資料":SelfAppearancePending?"角色外觀正在更新；可取消連線":SupportedAppearance?"角色外觀已載入":$"尚未支援此外觀（角色 {Actor.Gfx}／武器 {Actor.Weapon}）；移動與戰鬥已停止，仍可從背包卸下裝備或取消連線。";
    public bool Renderable=>Ready&&!TeleportPending&&SupportedMap&&SupportedAppearance;
    readonly System.Collections.Concurrent.ConcurrentDictionary<byte,int> unhandled=new();
    public void Reset(){Clear6016(SceneClock());ResetPlayerChat();ResetBuddies();ResetPrivateShop();ResetShip();ResetSpeedStatus();ResetBonusStats();ResetHitReactionSuppress();AppearanceRevision++;ResetTeleport();Entered=false;MapId=null;Status=null;Actor=null;SelfAppearancePending=false;Underwater=false;KeepalivesSent=TimeReplies=0;ServerTime=null;ProbeSent=false;unhandled.Clear();ResetMovement();ResetCombat();ResetMagic();ResetInventory();ResetEquipment();ResetRewards();ClearObjects();}
    public object Snapshot()=>new{playerChat=PlayerChatLines,sustenance=SustenanceText,basicSupplyNotice=BasicSupplyNotice,privateShop=PrivateShopSnapshotModel(),announcement=LastAnnouncement,bonusStatsNotice=BonusStatsNotice,bonusStats=new{phase=BonusStatsPhase,revision=BonusStatsRevision,selected=SelectedBonusAttribute,remaining=(int?)null},ready=Ready,coreComplete=CoreComplete,entered=Entered,mapId=MapId,underwater=Underwater,status=Status,actor=Actor,supportedMap=SupportedMap,mapResourceState=MapResourceState,mapResourceNotice=MapResourceNotice,loadedRegion=MapCache is {} cache?new{minX=(cache.OriginX+32768)/2,minY=cache.OriginY,maxX=(cache.OriginX+32768)/2+127,maxY=cache.OriginY+127,mode="original moving 2x2 cache"}:new{minX=Coordinates.MinX,minY=Coordinates.MinY,maxX=Coordinates.MaxX,maxY=Coordinates.MaxY,mode="resident original dual blocks"},supportedAppearance=SupportedAppearance,appearanceNotice=AppearanceNotice,appearanceRevision=AppearanceRevision,appearanceProfile=AppearanceProfile,renderable=Renderable,keepalivesSent=KeepalivesSent,timeReplies=TimeReplies,serverTime=ServerTime,unhandled=unhandled.ToDictionary(x=>$"0x{x.Key:X2}",x=>x.Value),ship=ShipSnapshot(),teleport=TeleportSnapshot(),movement=MovementSnapshot(),combat=CombatSnapshot(),inventory=InventorySnapshot(),equipment=EquipmentSnapshot(),rewards=RewardsSnapshot(),ground=GroundSnapshot(),magic=MagicSnapshot(),objects=Objects,target=Target,targetId=TargetId,opacity=sceneOpacity?.Snapshot(),cache=MapCache?.Snapshot(),objectRevision=ObjectRevision,worldEpoch=WorldEpoch,unknownObjectUpdates=UnknownObjectUpdates};
    public event Action? CombatSoundStateChanged;
    public void Receive(byte[] plain,string selected){try{ReceiveCore(plain,selected);}finally{ValidatePickupPresentation(ActionClock());CombatSoundStateChanged?.Invoke();}}
    void ReceiveCore(byte[] plain,string selected){
        var r=new LoginReader(plain);byte opcode=r.C();
        if(ReceiveDuel(opcode,plain))return;
        if(opcode==39&&ReceiveBuddies(opcode,plain))return;
        switch(opcode){
        case 64 when plain.Length>1 && plain[1] is >=1 and <=4:ReceiveCharReset(plain);return;
        case 83 when !WeaponRepairPackets.IsRepairList(plain):ReceivePetList(r);return;
        case 176:ReceivePetInventory(r,plain);return;
        case 115:ReceiveWeather(r);return;
        case 40:ReceiveLight(r);return;
        case 171:ReceiveInvisibility(r);return;
        case 250 when plain.Length>1&&plain[1]==111:ReceiveLocationShare(plain);return;
        case 71 when LocationShareProtocol.IsReply(plain):ReceiveLocationShare(plain);return;
        case 250 when plain.Length>1&&plain[1]==41:ReceiveCharacterConfig(plain);return;
        case 250 when plain.Length>=2&&plain[1] is 17 or 18 or 19:ReceiveBuddies(opcode,plain);return;
        case 71 when plain.Length>=3&&BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(1)) is 109 or 472:
            ReceiveBuddies(opcode,plain);break; // Continue through the existing server-message dispatch.
        case 39:if(ReceivePetDialog(plain)||ReceivePlayerQueryHtml(plain))return;break;
        case 71:ObserveFishing(plain);if(ReceiveMarriageMessage(plain)||ReceivePlayerQueryMessage(plain))return;break;
        case 119:ObserveFishing(plain);break;
        case 158:ObserveFishing(plain);if(ReceiveDoorAction(plain))return;break;
        case 22:ReceiveSoundPacket(plain);return;
        case 55:ReceiveEffectPacket(plain);return;
        case 106:ReceiveEffectPacket(plain);return;
        }
        if(ReceiveRecovery(opcode,r)||ReceivePlayerChat(opcode,r,plain)||ReceiveSustenance(opcode,r,plain)||ReceiveBonusStats(opcode,r,plain)||ReceivePrivateShop(opcode,r,plain)||ReceiveTeleport(opcode,r)||ReceiveTrade(opcode,r)||ReceiveInteraction(opcode,r)||ReceiveMagic(opcode,r,plain)||ReceiveEquipment(opcode,r,plain)||ReceiveInventory(opcode,r,plain)||ReceiveExperience(opcode,r)||ReceiveCombat(opcode,r,plain)||ReceiveObjectDelta(opcode,r)){BasicSupplyReply(opcode,plain);return;}
        switch(opcode){
        case 186:ReceiveMail(plain);break;
        case 72:ReceiveClanUpdate(plain);break;
        case 118:ReceiveClanEmblem(plain);break;
        case 200:ReceiveClanAttention(plain);break;
        case 167:ReceiveClanAnnouncement(plain);break;
        case 170:ReceiveClanMembers(plain);break;
        case 250 when plain.Length>1&&plain[1]==171:ReceiveClanOnlineMembers(plain);break;
        case 83 when WeaponRepairPackets.IsRepairList(plain):ReceiveWeaponRepair(r);break;
        case 76:ReceivePolymorph(plain);break;
        case 68:ReceiveBoardList(r);break;
        case 148:ReceiveBoardRead(r);break;
        case 209:ReceiveDoorAttribute(r);break;
        case 223:
            if(!plain.AsSpan(1).SequenceEqual(Convert.FromHexString("03158B7B94F02F")))throw new ProtocolException("unexpected EnterGame payload");Entered=true;break;
        case 8:
            var v=new OwnStatus(r.D(),r.C(),r.D(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C(),r.D(),r.D(),r.D(),r.D(),unchecked((int)r.D()),r.D(),r.C(),r.C(),unchecked((short)r.H()),unchecked((short)r.H()),unchecked((short)r.H()),unchecked((short)r.H()),unchecked((short)r.H()),r.D());r.Finish();
            if(Status!=null&&Status.ObjectId!=v.ObjectId)throw new ProtocolException("self status identity changed");Status=v;PartialAcByte=null;ApplyExperience(v.Level,v.Exp,opcode);break;
        case 206:ReceiveMapId(r);break;
        case 87:
            ushort x=r.H(),y=r.H();uint id=r.D();
            if(id!=Status?.ObjectId&&ReceiveDoorPack(plain,r,x,y,id))break;
            if(Status==null||id!=Status.ObjectId){ReceiveObject(r,x,y,id);break;}
            var a=new OwnActor(x,y,id,r.H(),r.C(),r.C(),r.C(),r.C(),r.D(),unchecked((short)r.H()),r.S(),r.S(),r.C(),r.D(),r.S(),r.S(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C());r.Finish();
            if(a.Name!=selected||a.Direction>7)throw new ProtocolException("self actor name/heading mismatch");SelfAppearancePending=false;objects.TryRemove(id,out _);ResetSpeedStatus();ReceiveGhostState(a);ApplyAuthoritative(a);OwnArrived();break;
        case 255:case 67:
            uint speedId=r.D();byte mode=r.C();ushort duration=r.H();r.Finish();ReceiveSpeed(opcode,speedId,mode,duration);break;
        case 123:ServerTime=r.D();r.Finish();ShipTimeReceived(ServerTime.Value);if(CoreComplete&&ProbeSent){TimeReplies++;CompleteInitialInventory();}
            Ambience.TimeReceived(ServerTime.Value,SceneClock());break;
        case 225:case 33:
            uint current=r.D(),maximum=r.D();r.Finish();if(Status==null)throw new ProtocolException("vitals before own status");Status=opcode==225?Status with{Hp=current,MaxHp=maximum}:Status with{Mp=current,MaxMp=maximum};break;
        case 227:throw new ProtocolException("server disconnect packet");
        default:Unhandled(opcode);break;
        }
        BasicSupplyReply(opcode,plain);
    }
    void Unhandled(byte opcode){unhandled.TryGetValue(opcode,out int count);unhandled[opcode]=count+1;}
}
public sealed partial class LoginConnection {
    public WorldSession World {get;}=new();
    Func<byte[],Task>? sendWorld;string? worldSelection;
    public async Task SelectCharacter(string name){
        if(State!=LoginState.CharacterList||!Characters.Any(c=>c.Name==name))throw new InvalidOperationException("請選擇本次伺服器清單中的角色");
        await SelectForProbe(name);
    }
    // Internal negative-test seam: only the real-JAR probe can bypass the UI list guard.
    internal async Task SelectForProbe(string name){
        if(State!=LoginState.CharacterList||sendWorld==null)throw new InvalidOperationException("character list required");
        int epoch=generation;var sender=sendWorld;
        World.Reset();worldInitialized=false;entryTiming=new(WorldClock());worldSelection=name;Set(LoginState.EnteringWorld,"要求進入角色："+name);
        try{await sender(LoginPackets.Pad([137,..LoginPackets.TextEncoding.GetBytes(name),0]));}
        catch(Exception ex){if(epoch==generation){Cancel();Set(LoginState.Faulted,"選角傳送失敗："+ex.Message);}}
    }
    async Task WorldHeartbeat(int epoch,CancellationToken token){
        try{while(!token.IsCancellationRequested){await Task.Delay(250,token);await PumpWorldAsync(epoch,token);}}
        catch(OperationCanceledException) when(token.IsCancellationRequested){}
        catch(Exception ex){if(epoch==generation){LastWorldEntryFailure=new{phase="network",timing=EntrySnapshot(),error=ex.Message};Cancel();Set(LoginState.Faulted,ex.GetType().Name+": "+ex.Message);}}
    }
}

