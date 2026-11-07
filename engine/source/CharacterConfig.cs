using System.Buffers.Binary;
namespace P06;

// The server stores an opaque blob. TWCC is our versioned format, not the old BIN format.
public sealed class CharacterConfig {
    readonly byte[] data;
    readonly PlayerHudBinding?[]? slots;
    public byte[] Data=>data.ToArray();
    public PlayerHudBinding?[]? Slots=>slots?.ToArray();
    public bool Editable=>slots!=null;
    public CharacterConfig(ReadOnlySpan<byte> bytes){
        data=bytes.ToArray();
        if(data.Length==0){slots=new PlayerHudBinding?[4];return;}
        if(data.Length!=26||!data.AsSpan(0,4).SequenceEqual("TWCC"u8)||data[4]!=1||data[5]!=4)return;
        var parsed=new PlayerHudBinding?[4];
        for(int i=0;i<4;i++){
            int offset=6+i*5;byte kind=data[offset];uint id=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset+1));
            if(kind>2||(kind==0)!=(id==0)||(kind==2&&id>int.MaxValue))return;
            parsed[i]=kind==0?null:new(kind==1?PlayerHudKind.Item:PlayerHudKind.Magic,id);
        }
        slots=parsed;
    }
    public static CharacterConfig FromSlots(IReadOnlyList<PlayerHudBinding?> bindings){
        if(bindings.Count!=4)throw new ArgumentException("expected existing four HUD slots");
        byte[] bytes=new byte[26];"TWCC"u8.CopyTo(bytes);bytes[4]=1;bytes[5]=4;
        for(int i=0;i<4;i++)if(bindings[i] is {} binding){
            if(binding.Id==0||!Enum.IsDefined(binding.Kind)||(binding.Kind==PlayerHudKind.Magic&&binding.Id>int.MaxValue))throw new ArgumentException("invalid shortcut binding");
            bytes[6+i*5]=(byte)(binding.Kind==PlayerHudKind.Item?1:2);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(7+i*5),binding.Id);
        }
        return new(bytes);
    }
}

public static class CharacterConfigPackets {
    public const int ServerStorageLimit=65535;
    // C pad is stored too; S adds a six-byte header, a zero terminator and its own pad.
    // Largest round-trip blob that fits LoginFrames' 10238-byte body limit.
    public const int MaximumDataLength=10219;
    public static byte[] Save(CharacterConfig config){
        byte[] data=config.Data;
        if(data.Length>MaximumDataLength)throw new ArgumentException("character config exceeds transport limit");
        byte[] bytes=new byte[5+data.Length];bytes[0]=244;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1),(uint)data.Length+3);
        data.CopyTo(bytes,5);return LoginPackets.Pad(bytes);
    }
    public static CharacterConfig Read(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=250||r.C()!=41)throw new ProtocolException("expected S250/41 character config");
        uint size=r.D();
        if(size>ServerStorageLimit||plain.Length>10238||size>r.Remaining)throw new ProtocolException("invalid character config size");
        int length=(int)size;
        int direct=(6+length+1+7)&~7;
        int storedPad=(8-((5+length)&7))&7;
        int echoed=(6+length+storedPad+1+7)&~7;
        if(plain.Length!=direct&&plain.Length!=echoed)throw new ProtocolException("invalid character config tail length");
        byte[] data=new byte[length];for(int i=0;i<length;i++)data[i]=r.C();
        // Binary data may contain zeros. Only bytes after the declared data are padding.
        while(r.Remaining>0)if(r.C()!=0)throw new ProtocolException("nonzero character config tail");
        return new(data);
    }
}

public sealed partial class WorldSession {
    CharacterConfig? characterConfig;
    int characterConfigGeneration;
    uint characterConfigActor;
    public CharacterConfig? ServerCharacterConfig {
        get {
            if(!Entered||Status==null||characterConfigActor!=Status.ObjectId||characterConfigGeneration!=(PickupSessionGeneration?.Invoke()??0))characterConfig=null;
            return characterConfig;
        }
    }
    void ReceiveCharacterConfig(byte[] plain){
        var parsed=CharacterConfigPackets.Read(plain); // Validate completely before publication.
        characterConfig=parsed;characterConfigGeneration=PickupSessionGeneration?.Invoke()??0;
        characterConfigActor=Status?.ObjectId??0;
    }
}

public sealed partial class LoginConnection {
    bool characterConfigSending;
    public string CharacterConfigNotice {get;private set;}="";
    public async Task<bool> SaveCharacterConfig(PlayerHudStamp stamp,CharacterConfig config){
        if(characterConfigSending||!PlayerHudActions.Current(this,stamp)||sendMovementGate==null)return false;
        // Never silently replace settings whose client format we do not understand.
        var original=World.ServerCharacterConfig;
        if(!config.Editable||original is {Editable:false}){CharacterConfigNotice="舊版角色設定格式未支援，保留原設定。";return false;}
        byte[] bytes=CharacterConfigPackets.Save(config);var sender=sendMovementGate;
        characterConfigSending=true;
        try {
            bool sent=await sender(bytes,()=>PlayerHudActions.Current(this,stamp)&&ReferenceEquals(original,World.ServerCharacterConfig),()=>{});
            if(!PlayerHudActions.Current(this,stamp))return false;
            CharacterConfigNotice=sent?"快捷設定已送出；伺服器沒有儲存確認回包。":"快捷設定未送出。";
            return sent;
        }catch(Exception){if(PlayerHudActions.Current(this,stamp))CharacterConfigNotice="快捷設定傳送結果不明；請重新登入核對。";return false;}
        finally{characterConfigSending=false;}
    }
}

// Synchronizes the existing HUD model at its single mount point. Defaults are never auto-saved.
public sealed class CharacterConfigHud {
    int generation=-1;uint actor;CharacterConfig? applied;
    PlayerHudBinding?[] previous=[];bool saving;
    public void Update(LoginConnection connection,PlayerHudSnapshot state,PlayerHudSlots bindings,Action<string> notice){
        var stamp=state.Stamp;
        if(!PlayerHudActions.Current(connection,stamp))return;
        bool identity=generation!=stamp.Generation||actor!=stamp.Actor;
        var received=connection.World.ServerCharacterConfig;
        if(identity||!ReferenceEquals(applied,received)){
            generation=stamp.Generation;actor=stamp.Actor;applied=received;
            // Clear old local per-character cache on every login; server data is authoritative.
            var restored=received?.Slots??new PlayerHudBinding?[4];
            for(int i=0;i<4;i++)bindings.Assign(i,restored[i]);
            previous=bindings.Bindings.ToArray();
            if(received is {Editable:false})notice("舊版角色設定格式未支援；保留伺服器原設定，不會自動覆寫。");
            return;
        }
        var current=bindings.Bindings.ToArray();
        if(previous.SequenceEqual(current)||saving)return;
        previous=current;
        if(received is {Editable:false}){notice("舊版設定保留中；本次快捷變更僅在本機有效。");return;}
        _=Save();
        async Task Save(){
            saving=true;
            try {
                await connection.SaveCharacterConfig(stamp,CharacterConfig.FromSlots(current));
                if(PlayerHudActions.Current(connection,stamp))notice(connection.CharacterConfigNotice);
            }finally{saving=false;}
        }
    }
}

public sealed partial class ClientForm {
    readonly CharacterConfigHud characterConfigHud=new();
    internal void SyncCharacterConfig(PlayerHudSnapshot snapshot,PlayerHudSlots slots)=>
        characterConfigHud.Update(login,snapshot,slots,message=>{playerHudNotice=message;playerHudNoticeUntil=Environment.TickCount64+6000;});
}
