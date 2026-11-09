using System.Text;
namespace P06;

// T229（#547）：S250 聊天訊息類子碼（protocol-gaps.md 第 2.2、2.3 節 B1 組、第 8 節）。
// 欄位一律依 3.81 伺服器寫法（docs/reference/server-protocol.md）；文字由 T228 的執行期字串表（ServerMessageText.Table，
// 原客戶端 string-c.tbl）查，不存進 repo。字串 id 取自 TW16 S_EVENT（op 108，sub_9357E0）各 case 直接取的 id。
//
// 子碼  3.81 類別／建構子                       3.81 欄位（不含 op、子碼）        處理
// 0     S_PacketBoxWar(MSG_WAR_BEGIN,城)          C(城堡) H(0)                       聊天欄：字串 639（%s＝城堡名，取字串 912＋城堡，推定）
// 1     S_PacketBoxWar(MSG_WAR_END,城)            C(城堡) H(0)                       聊天欄：字串 640
// 2     S_PacketBoxWar(MSG_WAR_GOING,城)          C(城堡) H(0)                       聊天欄：字串 641
// 3     S_PacketBoxWar(MSG_WAR_INITIATIVE)        無                                 聊天欄：字串 642
// 4     S_PacketBoxWar(MSG_WAR_OCCUPY)            無                                 聊天欄：字串 643
// 12    S_PacketBoxItemLv(min,max)                C(min) C(max)                      聊天欄：min>0→672「…%d以上…」、max>0→673「…%d以下…」
// 15    S_PacketBox(MSG_ELF,n)                    C(n)                               聊天欄：字串 679，%s＝字串 679＋n（3.81 送 1 火／2 水／3 風／4 地＝680–683）
// 23    S_PacketBox(MSG_TOWN_LEADER,name)         S(村長名)                          聊天欄：字串 764（%s＝名稱，推定）
// 27    S_PacketBox(MSG_RANK_CHANGED[,rank,name]) 無；或 C(階級) S(名稱)              只存資料（TW16 交給 sub_B32AB0，顯示方式待驗證）
// 30    S_PacketBoxWar(30,id,name,clan)           D(id) S(名稱) S(血盟)               只存資料（3.81 無呼叫者；TW16 讀 `dd`，寬度不同，待驗證）
// 31    S_PacketBoxHpMsg()                        無                                 只存資料（TW16 讀 `d`、無字串 id，待驗證）
// 79    S_PacketBoxWar() MSG_WAR_OVER             C(count) S×(count−1)（無主寫 " "）    聊天欄：1511；有主城時 1513 標題＋每城一行 1514（%s＝城堡名、血盟），無主城不列（推定）
//
// 字串表不在（CI、沒有原客戶端）時照 ServerMessageText 慣例顯示「原服訊息 id：參數」；id ≥ 表筆數時原版不顯示，本模組照做。

/// <summary>S250/27 最近一次階級變更：Rank／Name 為 null 表示 3.81 送的是不帶欄位的形式。</summary>
public sealed record PacketBoxRankChange(byte? Rank,string? Name);
/// <summary>S250/30 最近一次打敗反王：3.81 寫 D(id) S(名稱) S(血盟)；id 意義伺服器註解為「血盟 ID 或其他？」。</summary>
public sealed record PacketBoxLastavard(uint Id,string Name,string Clan);
/// <summary>本模組送進聊天欄的一行（給測試與快照）：子碼、字串 id、參數、顯示文字。</summary>
public sealed record PacketBoxMessageLine(byte Subtype,int StringId,string[] Arguments,string Text);

public static class PacketBoxMessageRules {
    public const byte Opcode=250;
    public const byte WarBegin=0,WarEnd=1,WarGoing=2,WarInitiative=3,WarOccupy=4,LevelOver=12,Elf=15,TownLeader=23,RankChanged=27,WinLastavard=30,FeelGood=31,WarOver=79;
    public static readonly byte[] Subtypes=[WarBegin,WarEnd,WarGoing,WarInitiative,WarOccupy,LevelOver,Elf,TownLeader,RankChanged,WinLastavard,FeelGood,WarOver];
    // 字串 id（protocol-gaps.md 第 2.2 節表格「字串」欄，TW16 case 內直接取的 id）。
    public const int WarBeginText=639,WarEndText=640,WarGoingText=641,WarInitiativeText=642,WarOccupyText=643;
    public const int LevelMinText=672,LevelMaxText=673,ElfText=679,TownLeaderText=764,WarOverText=1511,WarOverHeaderText=1513,WarOverClanText=1514;
    /// <summary>城堡名的字串 id 基底：本機 string-c.tbl 913–920 依序是 L1CastleLocation 1–8 的城名；TW16 case 0x1E 取 919（亞丁城）。各 case 實際如何取城名未展開（推定）。</summary>
    public const int CastleNameBase=912;
    public static int WarText(byte sub)=>sub switch{WarBegin=>WarBeginText,WarEnd=>WarEndText,WarGoing=>WarGoingText,WarInitiative=>WarInitiativeText,WarOccupy=>WarOccupyText,_=>throw new ArgumentOutOfRangeException(nameof(sub))};

    /// <summary>
    /// 把參數代入字串表的文字。TW16 這些 case 的字串（例 639「%s的攻城戰開始。」）是 C 格式：`%s`／`%d` 依序代入；
    /// `%數字` 依 ServerMessageText.Format（sub_933850）的位置參數規則；`%%`→`%`。參數不足時該處留空。
    /// 代入方式依格式字元推定，原版 case 內實際呼叫的格式化函式未展開（待驗證）。
    /// </summary>
    public static string Format(string template,IReadOnlyList<string> args){
        var sb=new StringBuilder(template.Length+16);int next=0;
        for(int i=0;i<template.Length;i++){
            char c=template[i];
            if(c!='%'){sb.Append(c);continue;}
            if(++i>=template.Length)break;
            char k=template[i];
            if(k is 's' or 'd'){if(next<args.Count)sb.Append(args[next]);next++;}
            else if(k is >= '0' and <= '9'){int n=k-'0';if(n<args.Count)sb.Append(args[n]);}
            else sb.Append(k);
        }
        return sb.ToString();
    }

    /// <summary>查字串表並格式化；回 null 表示原版不顯示（id ≥ 表筆數）。沒有字串表時回「原服訊息 id：參數」。</summary>
    public static string? Text(int id,IReadOnlyList<string> args){
        if(ServerMessageText.Table is {} t){
            if(id<0||id>=t.Count)return null;
            return ServerMessageText.DisplayText(Format(t[id],args));
        }
        return $"原服訊息 {id}"+(args.Count>0?"："+string.Join("、",args):"");
    }

    /// <summary>當作參數用的字串表文字（不格式化、去掉顏色控制）；沒有字串表時回 fallback，id 超出表時回空字串。</summary>
    public static string Argument(int id,string fallback)=>ServerMessageText.Table is {} t?(id>=0&&id<t.Count?ServerMessageText.DisplayText(t[id]):""):fallback;
    /// <summary>城堡名：1–8 取字串 912＋編號；沒有字串表時用 CastleProtocol.CastleName。</summary>
    public static string CastleName(byte castle)=>castle is >= 1 and <= 8?Argument(CastleNameBase+castle,CastleProtocol.CastleName(castle)):CastleProtocol.CastleName(castle);
    /// <summary>S250/15 的屬性名：字串 679＋n（3.81 只送 1–4）。</summary>
    public static string ElfAttribute(byte n)=>Argument(ElfText+n,$"字串 {ElfText+n}");
}

public sealed partial class WorldSession {
    readonly object packetBoxMessageGate=new();
    readonly SortedDictionary<int,int> packetBoxMessageCounts=[];
    readonly List<PacketBoxMessageLine> packetBoxMessageLines=[];
    PacketBoxRankChange? packetBoxRank;
    PacketBoxLastavard? packetBoxLastavard;
    string[]? packetBoxWarOverClans;
    int packetBoxFeelGood,packetBoxDroppedMessages;

    /// <summary>B1 子碼套用後觸發（參數＝子碼）。</summary>
    public event Action<int>? PacketBoxMessageReceived;
    public PacketBoxRankChange? RankChange{get{lock(packetBoxMessageGate)return packetBoxRank;}}
    public PacketBoxLastavard? LastavardWin{get{lock(packetBoxMessageGate)return packetBoxLastavard;}}
    /// <summary>最近一次 S250/79 的各城血盟名（索引 0＝城堡 1；無主時為空字串）。</summary>
    public string[]? WarOverClans{get{lock(packetBoxMessageGate)return packetBoxWarOverClans?.ToArray();}}
    public int FeelGoodCount{get{lock(packetBoxMessageGate)return packetBoxFeelGood;}}
    /// <summary>最近 100 行本模組送進聊天欄的訊息（含字串 id 與參數）。</summary>
    public PacketBoxMessageLine[] PacketBoxMessageLines{get{lock(packetBoxMessageGate)return [..packetBoxMessageLines];}}

    [WorldReceive(250,Subtype=0)][WorldReceive(250,Subtype=1)][WorldReceive(250,Subtype=2)][WorldReceive(250,Subtype=3)]
    [WorldReceive(250,Subtype=4)][WorldReceive(250,Subtype=12)][WorldReceive(250,Subtype=15)][WorldReceive(250,Subtype=23)]
    [WorldReceive(250,Subtype=27)][WorldReceive(250,Subtype=30)][WorldReceive(250,Subtype=31)][WorldReceive(250,Subtype=79)]
    bool ReceivePacketBoxMessage(byte[] plain){
        // 先完整解析與驗證整包，再改狀態；格式錯誤丟 ProtocolException，不留半套狀態。
        var r=new LoginReader(plain);
        if(r.C()!=PacketBoxMessageRules.Opcode)throw new ProtocolException("not S250");
        byte sub=r.C();
        var lines=new List<(int id,string[] args)>();
        Action? apply=null;
        switch(sub){
        case PacketBoxMessageRules.WarBegin or PacketBoxMessageRules.WarEnd or PacketBoxMessageRules.WarGoing:{
            // 3.81 寫 C(城堡) H(0)；TW16 讀 `d`（little-endian 下低位 byte 即城堡編號）。
            byte castle=r.C();if(r.H()!=0)throw new ProtocolException("invalid war castle tail");r.Finish();
            lines.Add((PacketBoxMessageRules.WarText(sub),[PacketBoxMessageRules.CastleName(castle)]));
            break;}
        case PacketBoxMessageRules.WarInitiative or PacketBoxMessageRules.WarOccupy:
            r.Finish();lines.Add((PacketBoxMessageRules.WarText(sub),[]));
            break;
        case PacketBoxMessageRules.LevelOver:{
            // 3.81 只送 (0,max) 或 (min,0)；TW16 讀 `ccc`，第三個落在補零。
            byte min=r.C(),max=r.C();r.Finish();
            if(min==0&&max==0)throw new ProtocolException("level-over without limit");
            if(min>0)lines.Add((PacketBoxMessageRules.LevelMinText,[min.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
            if(max>0)lines.Add((PacketBoxMessageRules.LevelMaxText,[max.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
            break;}
        case PacketBoxMessageRules.Elf:{
            byte n=r.C();r.Finish();
            lines.Add((PacketBoxMessageRules.ElfText,[PacketBoxMessageRules.ElfAttribute(n)]));
            break;}
        case PacketBoxMessageRules.TownLeader:{
            string name=PlayerChatProtocol.Display(r.S());r.Finish();
            lines.Add((PacketBoxMessageRules.TownLeaderText,[name]));
            break;}
        case PacketBoxMessageRules.RankChanged:{
            // 兩種 3.81 寫法：S_PacketBox(27,rank)（兩參數建構子沒有 27 的 case，只寫子碼）；S_PacketBox(27,rank,name)＝C S。
            PacketBoxRankChange change;
            if(r.Remaining==0||plain.AsSpan(2).IndexOfAnyExcept((byte)0)<0){r.Finish();change=new(null,null);}
            else{byte rank=r.C();string name=PlayerChatProtocol.Display(r.S());r.Finish();change=new(rank,name);}
            apply=()=>{lock(packetBoxMessageGate)packetBoxRank=change;};
            break;}
        case PacketBoxMessageRules.WinLastavard:{
            uint id=r.D();string name=PlayerChatProtocol.Display(r.S()),clan=PlayerChatProtocol.Display(r.S());r.Finish();
            apply=()=>{lock(packetBoxMessageGate)packetBoxLastavard=new(id,name,clan);};
            break;}
        case PacketBoxMessageRules.FeelGood:
            r.Finish();apply=()=>{lock(packetBoxMessageGate)packetBoxFeelGood++;};
            break;
        case PacketBoxMessageRules.WarOver:{
            int count=r.C();
            if(count==0)throw new ProtocolException("war-over count 0");
            // 每筆至少 1 byte（空字串的結尾 0）；先擋不可能的筆數。
            if(count-1>r.Remaining)throw new ProtocolException("war-over count exceeds packet");
            var clans=new string[count-1];
            for(int i=0;i<clans.Length;i++)clans[i]=PlayerChatProtocol.Display(r.S()).Trim();
            r.Finish();
            lines.Add((PacketBoxMessageRules.WarOverText,[]));
            if(clans.Any(c=>c.Length>0))lines.Add((PacketBoxMessageRules.WarOverHeaderText,[]));
            for(int i=0;i<clans.Length;i++)
                if(clans[i].Length>0)lines.Add((PacketBoxMessageRules.WarOverClanText,[PacketBoxMessageRules.CastleName((byte)(i+1)),clans[i]]));
            apply=()=>{lock(packetBoxMessageGate)packetBoxWarOverClans=clans;};
            break;}
        default:return false;
        }
        // 文字在解析成功後才查（查表本身不改 session 狀態）。
        var shown=new List<PacketBoxMessageLine>();int dropped=0;
        foreach(var (id,args) in lines){
            if(PacketBoxMessageRules.Text(id,args) is {} text)shown.Add(new(sub,id,args,text));else dropped++;
        }
        apply?.Invoke();
        foreach(var line in shown){
            playerChatLines.Enqueue(new(++playerChatSequence,0,line.Text));
            while(playerChatLines.Count>100)playerChatLines.Dequeue();
        }
        if(shown.Count>0)playerChatRevision++;
        lock(packetBoxMessageGate){
            packetBoxMessageCounts[sub]=packetBoxMessageCounts.GetValueOrDefault(sub)+1;
            packetBoxDroppedMessages+=dropped;
            packetBoxMessageLines.AddRange(shown);
            if(packetBoxMessageLines.Count>100)packetBoxMessageLines.RemoveRange(0,packetBoxMessageLines.Count-100);
        }
        PacketBoxMessageReceived?.Invoke(sub);
        return true;
    }

    [WorldReset]
    void ResetPacketBoxMessages(){
        lock(packetBoxMessageGate){
            packetBoxMessageCounts.Clear();packetBoxMessageLines.Clear();
            packetBoxRank=null;packetBoxLastavard=null;packetBoxWarOverClans=null;packetBoxFeelGood=0;packetBoxDroppedMessages=0;
        }
    }

    [WorldSnapshot("packetBoxMessages")]
    object? PacketBoxMessagesSnapshot(){
        lock(packetBoxMessageGate)return new{
            packets=packetBoxMessageCounts.ToDictionary(x=>x.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),x=>x.Value),
            lines=packetBoxMessageLines.Select(l=>new{subtype=l.Subtype,stringId=l.StringId,args=l.Arguments,text=l.Text}).ToArray(),
            dropped=packetBoxDroppedMessages,
            rank=packetBoxRank is {} rk?new{rank=rk.Rank,name=rk.Name}:null,
            lastavard=packetBoxLastavard is {} lv?new{id=lv.Id,name=lv.Name,clan=lv.Clan}:null,
            warOverClans=packetBoxWarOverClans,
            feelGood=packetBoxFeelGood,
            table=ServerMessageText.Table is null?"fallback":"string-c.tbl",
        };
    }
}
