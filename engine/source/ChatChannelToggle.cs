namespace P06;

// T231（#549）：聊天頻道開關。依據 docs/reference/protocol-gaps.md §6 C_ONOFF（184）列與 §8 任務 C。
// 原版（TW16）送 C_ONOFF `cc`（類型、值）；3.81 對應 C26 `C(type) C(button)`，伺服器只處理：
//   type 255：合併位元（button 90–95、122–127）一次設定全體與密語；
//   type 0 全體、2 密語、6 買賣：button 0＝關、1＝開；其他 type／button 伺服器忽略。
// 原版 sub_99B430 一次送 255＋合併位元，再送 6、9、10；3.81 不處理 9、10，所以本引擎只送 255 與 6。
// 推定（未實機對包）：合併位元 bit0＝全體、bit2＝密語，其餘位元固定為 0x5A（伺服器接受的 90–95 區段）；
// 0x20 位元（122–127 區段）的原版意義未查，不送。
// 聊天列 `/on 頻道`、`/off 頻道` 的確切語法原版未查（protocol-gaps 只記字串 401「開」／402「關」），本檔的指令格式是推定；
// R2-G（#616）：聊天列已接上——LoginConnection.SendPlayerChat 先用 ChatChannelCommands.IsCommand 判斷，指令改走 SendChatChannelCommand，不再當聊天送出。
public enum ChatChannelKind : byte { World=0, Whisper=2, Trade=6 }

public readonly record struct ChatChannelSettings(bool World,bool Whisper,bool Trade) {
    public static ChatChannelSettings Default=>new(true,true,true);
    /// <summary>TWCC v2 保存位元：bit0 全體、bit1 密語、bit2 買賣；其他位元必須為 0。</summary>
    public const byte StoredMask=0x07;
    public byte Flags=>(byte)((World?1:0)|(Whisper?2:0)|(Trade?4:0));
    public static ChatChannelSettings? FromFlags(byte flags)=>(flags&~StoredMask)!=0?null:new((flags&1)!=0,(flags&2)!=0,(flags&4)!=0);
    public bool Get(ChatChannelKind kind)=>kind switch{ChatChannelKind.World=>World,ChatChannelKind.Whisper=>Whisper,ChatChannelKind.Trade=>Trade,_=>throw new ArgumentOutOfRangeException(nameof(kind))};
    public ChatChannelSettings With(ChatChannelKind kind,bool on)=>kind switch{
        ChatChannelKind.World=>this with{World=on},ChatChannelKind.Whisper=>this with{Whisper=on},ChatChannelKind.Trade=>this with{Trade=on},
        _=>throw new ArgumentOutOfRangeException(nameof(kind))};
}

public static class ChatChannelPackets {
    public const byte Opcode=26,MergedType=255;
    // 推定：90＝0x5A 基底；+1 全體、+4 密語（伺服器接受 90／91／94／95）。
    public const byte MergedBase=0x5A,MergedWorldBit=0x01,MergedWhisperBit=0x04;
    public static byte MergedButton(ChatChannelSettings s)=>(byte)(MergedBase|(s.World?MergedWorldBit:0)|(s.Whisper?MergedWhisperBit:0));
    public static byte[] Toggle(ChatChannelKind kind,bool on){
        if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));
        return LoginPackets.Pad([Opcode,(byte)kind,(byte)(on?1:0)]);
    }
    public static byte[] Merged(ChatChannelSettings s)=>LoginPackets.Pad([Opcode,MergedType,MergedButton(s)]);
    /// <summary>登入後還原：先送 255＋合併位元（全體、密語），再送 6（買賣），順序同原版 sub_99B430。</summary>
    public static byte[][] Sync(ChatChannelSettings s)=>[Merged(s),Toggle(ChatChannelKind.Trade,s.Trade)];
    /// <summary>3.81 伺服器收到 C26 後的設定變化（文件 server-protocol.md／server-rules.md R06 所述語義），供離線測試核對封包。</summary>
    public static ChatChannelSettings Apply(ChatChannelSettings s,byte type,byte button){
        switch(type){
        case MergedType:
            if(button is (>=90 and <=95) or (>=122 and <=127)){
                int low=button&0x1F; // 90–95 與 122–127 只差 0x20
                if(low is 0x1A or 0x1B or 0x1E or 0x1F)return s with{World=(low&MergedWorldBit)!=0,Whisper=(low&MergedWhisperBit)!=0};
            }
            return s;
        case 0 or 2 or 6:
            if(button>1)return s;
            return s.With((ChatChannelKind)type,button==1);
        default:return s;
        }
    }
    public static bool TryDecode(byte[] plain,out byte type,out byte button){
        type=button=0;
        if(plain.Length<3||plain[0]!=Opcode)return false;
        type=plain[1];button=plain[2];return true;
    }
}

public sealed record ChatChannelCommand(ChatChannelKind Channel,bool On);

public static class ChatChannelCommands {
    // 字串 401「開」／402「關」（protocol-gaps §6）。原版在執行期從 Text.pak 讀；這裡只用兩個字作為回報文字。
    public const string OnText="開",OffText="關";
    public const string Help="頻道開關：/on 頻道 或 /off 頻道（頻道：全體、密語、買賣）";
    public static string ChannelName(ChatChannelKind kind)=>kind switch{ChatChannelKind.World=>"全體",ChatChannelKind.Whisper=>"密語",ChatChannelKind.Trade=>"買賣",_=>"?"};
    public static string Describe(ChatChannelKind kind,bool on)=>$"{ChannelName(kind)}聊天：{(on?OnText:OffText)}";
    static ChatChannelKind? Channel(string name)=>name switch{
        "全體" or "全體聊天" or "&" or "world"=>ChatChannelKind.World,
        "密語" or "密語頻道" or "\"" or "whisper"=>ChatChannelKind.Whisper,
        "買賣" or "交易" or "買賣頻道" or "交易頻道" or "trade"=>ChatChannelKind.Trade,
        _=>null};
    /// <summary>是否為頻道開關指令（不含頻道名稱檢查）；聊天列可先用這個判斷要不要交給 SendChatChannelCommand。</summary>
    public static bool IsCommand(string input)=>input is "/on" or "/off"||input.StartsWith("/on ",StringComparison.Ordinal)||input.StartsWith("/off ",StringComparison.Ordinal);
    /// <summary>解析 `/on 頻道`／`/off 頻道`；不是指令回 null；是指令但頻道不明丟 ArgumentException。</summary>
    public static ChatChannelCommand? Parse(string input){
        if(!IsCommand(input))return null;
        bool on=input.StartsWith("/on",StringComparison.Ordinal);
        string name=input[(on?3:4)..].Trim();
        if(Channel(name.ToLowerInvariant()) is not {} kind)throw new ArgumentException(Help);
        return new(kind,on);
    }
}

public sealed partial class WorldSession {
    // 本場已送出的頻道狀態與角色設定。伺服器沒有存檔回包，所以以本機送出成功的版本為準；身分（世代、角色）變了就失效。
    ChatChannelSettings? chatChannelsSession;CharacterConfig? sentCharacterConfig;
    int chatChannelGeneration,sentConfigGeneration;uint chatChannelActor,sentConfigActor;
    bool SameIdentity(int generation,uint actor)=>Entered&&Status!=null&&actor==Status.ObjectId&&generation==(PickupSessionGeneration?.Invoke()??0);
    internal void RememberCharacterConfig(CharacterConfig config){sentCharacterConfig=config;sentConfigGeneration=PickupSessionGeneration?.Invoke()??0;sentConfigActor=Status?.ObjectId??0;}
    internal void RememberChatChannels(ChatChannelSettings settings){chatChannelsSession=settings;chatChannelGeneration=PickupSessionGeneration?.Invoke()??0;chatChannelActor=Status?.ObjectId??0;}
    /// <summary>本角色最新的設定：本場送出成功的版本優先，否則用伺服器登入時給的版本。</summary>
    public CharacterConfig? LatestCharacterConfig=>sentCharacterConfig!=null&&SameIdentity(sentConfigGeneration,sentConfigActor)?sentCharacterConfig:ServerCharacterConfig;
    /// <summary>要寫進角色設定的頻道狀態；從未設定過回 null（保存時沿用 TWCC v1，不改既有資料）。</summary>
    public ChatChannelSettings? PersistedChatChannels=>chatChannelsSession is {} s&&SameIdentity(chatChannelGeneration,chatChannelActor)?s:LatestCharacterConfig?.Channels;
    public ChatChannelSettings ChatChannels=>PersistedChatChannels??ChatChannelSettings.Default;
    [WorldReset]
    void ResetChatChannels(){chatChannelsSession=null;sentCharacterConfig=null;chatChannelGeneration=sentConfigGeneration=0;chatChannelActor=sentConfigActor=0;}
    [WorldSnapshot("chatChannels")]
    object? ChatChannelSnapshot(){var s=ChatChannels;return new{world=s.World,whisper=s.Whisper,trade=s.Trade,stored=PersistedChatChannels!=null};}
}

public sealed partial class LoginConnection {
    bool chatChannelSending;
    public string ChatChannelNotice {get;private set;}="";
    async Task<bool> SendChatChannelPackets(PlayerHudStamp stamp,byte[][] packets){
        if(chatChannelSending||!PlayerHudActions.Current(this,stamp)||sendMovementGate is not {} sender)return false;
        chatChannelSending=true;
        try{
            foreach(var packet in packets)if(!await sender(packet,()=>PlayerHudActions.Current(this,stamp),()=>{}))return false;
            return true;
        }finally{chatChannelSending=false;}
    }
    /// <summary>登入後把已保存的頻道設定送給伺服器（3.81 角色每次登入都從全開開始）。沒有保存過就不送。</summary>
    public async Task<bool> SyncChatChannels(PlayerHudStamp stamp){
        if(!PlayerHudActions.Current(this,stamp)||World.PersistedChatChannels is not {} settings)return false;
        try{return await SendChatChannelPackets(stamp,ChatChannelPackets.Sync(settings));}
        catch(Exception){if(PlayerHudActions.Current(this,stamp))ChatChannelNotice="頻道設定還原結果不明；請重新登入核對。";return false;}
    }
    /// <summary>切換單一頻道：送 C26（type, 0/1），成功後把新狀態寫進角色設定（TWCC v2）。</summary>
    public async Task<bool> SetChatChannel(PlayerHudStamp stamp,ChatChannelKind kind,bool on){
        if(!Enum.IsDefined(kind)||!PlayerHudActions.Current(this,stamp))return false;
        var next=World.ChatChannels.With(kind,on);
        bool sent;
        try{sent=await SendChatChannelPackets(stamp,[ChatChannelPackets.Toggle(kind,on)]);}
        catch(Exception){if(PlayerHudActions.Current(this,stamp))ChatChannelNotice="頻道切換結果不明；請重新登入核對。";return false;}
        if(!PlayerHudActions.Current(this,stamp))return false;
        if(!sent){ChatChannelNotice="頻道切換未送出。";return false;}
        World.RememberChatChannels(next);
        string done=ChatChannelCommands.Describe(kind,on);
        var latest=World.LatestCharacterConfig;
        if(latest is {Editable:false}){ChatChannelNotice=done+"（舊版角色設定格式未支援，本次不保存）";return true;}
        var config=(latest??CharacterConfig.FromSlots(new PlayerHudBinding?[CharacterConfig.SlotCount])).WithChannels(next);
        bool saved=await SaveCharacterConfig(stamp,config);
        if(PlayerHudActions.Current(this,stamp))ChatChannelNotice=saved?done+"（已保存到角色設定）":done+"（角色設定未保存，下次登入可能還原）";
        return true;
    }
    /// <summary>聊天列指令入口：`/on 頻道`、`/off 頻道`。不是指令回 false 且不動作。</summary>
    public async Task<bool> SendChatChannelCommand(string text,PlayerHudStamp stamp){
        ChatChannelNotice=""; // 每次指令重新給回報，不沿用上一次的文字。
        ChatChannelCommand? command;
        try{command=ChatChannelCommands.Parse(text);}
        catch(ArgumentException ex){ChatChannelNotice=ex.Message;return false;}
        return command!=null&&await SetChatChannel(stamp,command.Channel,command.On);
    }
}
