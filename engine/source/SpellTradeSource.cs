using P06.NativeUi;
namespace P06;

// R3-F（T286／#679）：學魔法窗改用原生 TRADE_WINDOW 模式 7（gap-matrix 12.5；TWP§1.1、§1.6；SS2§1.2）。
// 已驗證（TWP／SS2，靜態分析）：
//   - op 44 S_BUYABLE_SPELL_LIST（3.81 S41 `D(基價) H(n) n×D(索引)`）與 op 175 S_EXCHANGEABLE_SPELL_LIST（3.81 S13 `H(n) n×D`）
//     都填同一個 TradeBox，以 sub_A59780(7) 開 TRADE_WINDOW 模式 7（左鈕 BUY 1992）。
//   - op 44：TradeBox +153＝1（顯示單價），單價＝基價×((索引>>3)+1)²；op 175：+153＝0（不顯示單價，價格 0）。
//   - 送出（sub_A2BD10）：+153＝1 送 op 123＝3.81 C39 C_SkillBuyOK；否則送 op 210＝3.81 C191 C_SkillBuyItemOK；格式都是 H(n)＋n×D(索引)。
//   - 空清單：op 175 開 NPC 對話頁 HorunEv3（SpellExchange.cs 已做）；op 44 依 NPC 外型 237→GerengEv3、2820→Sedia1、其他→Siriss1。
//   - op 175 收包時本機已學的技能不列入（sub_475C90）。
// 推測（標明）：
//   - TW16 的 op 44 空清單從封包尾端讀 NPC id；3.81 空清單只送 H(0)，TW16 會讀過界（SS2§1.2）。本引擎改用目前對話 NPC 的外型（Target.Gfx）。
//   - op 44 已學技能：原版是否濾掉未查；這裡列出但停用（數量上限 0）。
// R9-G（T367／#851，FU9§11）列圖示（已驗證，靜態分析）：S13（sub_93F200）／S41（sub_931FF0）都以
//   sub_A27B70(索引, word_136D548[索引], 0, 名稱) 建 TradeItem → Item+290 是**物品圖示編號**（不是 surf）；
//   列由 Item vtable +8＝sub_891DF0 → sub_88CE60 畫 32×32，與背包同一條路（引擎 ItemIconSource 的 "<編號>.tbt"）。
//   表見 SpellTradeProtocol.SpellIcons；0 或缺圖（本機缺 5731、5734、5737、6458、6501）就不畫。
// 收包：S41 照舊由 NpcServices.cs 解析成 InteractionKind="skills" 的列；本檔只以 M7-A 觀察器（[WorldReceive(41,Observe=true)]）
//   在同一包先記下基價，並在空清單時直接換頁（換頁後 NpcServices 的「waiting／skill-list」條件不成立，不會再蓋掉頁面）。
// 送出：op 44 走既有 LoginConnection.Interact("shop")（ValidateService → ServicePackets.Learn＝C39）；op 175 走 LearnSpellExchange（C191）。

public sealed partial class WorldSession {
    uint spellBuyBasis;int spellBuyRevision=-1;

    /// <summary>op 44 空清單時開的 NPC 頁（SS2§1.2）：外型 237→GerengEv3、2820→Sedia1、其他→Siriss1。</summary>
    public static string SpellBuyEmptyPage(ushort? npcGfx)=>npcGfx switch{237=>"GerengEv3",2820=>"Sedia1",_=>"Siriss1"};

    /// <summary>
    /// S41 觀察器：與 NpcServices.cs 的解析同樣嚴格；格式不對時什麼都不做（交給主分派丟 ProtocolException）。
    /// 只在 NpcServices 會接受這包的狀態（對話有效、等待 skill-list）才動作。
    /// </summary>
    [WorldReceive(41,Observe=true)]
    void ObserveSpellBuyList(byte[] plain){
        if(SpellTradeProtocol.TryDecodeBuyList(plain) is not {} decoded)return;
        var (basis,indices)=decoded;CheckInteraction();
        if(!InteractionValid||pendingNpcReply!="skill-list"||InteractionKind!="waiting")return;
        if(indices.Length==0){ShowPage(SpellBuyEmptyPage(InteractionNpcGfx));return;}
        // NpcServices 接受時 InteractionRevision 會 +1；只有那一版的「skills」清單是這包的。
        spellBuyBasis=basis;spellBuyRevision=InteractionRevision+1;
    }
    [WorldReset]
    void ResetSpellBuy(){spellBuyBasis=0;spellBuyRevision=-1;}

    /// <summary>op 44（金幣學魔法）的 TRADE_WINDOW 模式 7 內容；目前不是這份清單時為 null。</summary>
    public TradeListView? SpellBuyTradeWindow(){
        CheckInteraction();
        if(InteractionKind!="skills"||InteractionRevision!=spellBuyRevision||!InteractionValid||shopRows.Length==0)return null;
        var items=shopRows.Select(r=>{
            int index=(int)r.Id;bool learned=SpellLearned(index+1);
            // ShopRow.Available＝1 代表 NpcServices 判定可學（有價格依據、未學）；送出時仍由 ValidateService 再驗一次。
            uint max=r.Available>0&&!learned?1u:0u;
            return new TradeListItem(r.Id,SpellExchangeName(index+1),SpellTradeProtocol.SpellIcon(index),SpellTradeProtocol.BuyPrice(spellBuyBasis,index),max,max>0);
        }).ToArray();
        return new(TradeWindowPanel.ModeSpell,SpellTradeProtocol.BuyRevision(InteractionRevision),items,ShopTrade.Holding(this),true,
            ServiceSubmittable,InteractionNotice);
    }

    /// <summary>op 175（以物品學魔法）的 TRADE_WINDOW 模式 7 內容（不顯示單價、無 TOTAL）；沒有清單時為 null。</summary>
    public TradeListView? SpellExchangeTradeWindow(){
        if(spellExchangeOffer is not {} offer)return null;
        // 原版收包時跳過本機已學技能（SS2§1.2 步驟 2）。
        var items=offer.Indices.Where(i=>!SpellLearned(i+1)).Select(i=>new TradeListItem((uint)i,SpellExchangeName(i+1),SpellTradeProtocol.SpellIcon(i),0,1,true)).ToArray();
        return new(TradeWindowPanel.ModeSpell,SpellTradeProtocol.ExchangeRevision(offer.Revision),items,null,false,true,spellExchangeNotice);
    }
}

public static class SpellTradeProtocol {
    public const byte SBuyList=41;
    /// <summary>單價＝基價×((索引>>3)+1)²（TWP§1.1、SS2§1.2）；超過 uint 夾到上限。</summary>
    public static uint BuyPrice(uint basis,int index){long level=(index>>3)+1;long v=basis*level*level;return v>uint.MaxValue?uint.MaxValue:(uint)v;}
    // 兩份清單共用模式 7；TradeWindowPanel 以 (模式, 版本) 判斷新清單與已關閉的清單，所以版本以奇偶分開，避免互相誤判。
    public static int BuyRevision(int interactionRevision)=>(int)(((long)interactionRevision<<1)&int.MaxValue);
    public static int ExchangeRevision(long offerRevision)=>(int)(((offerRevision<<1)|1)&int.MaxValue);
    public static bool IsExchangeRevision(int revision)=>(revision&1)==1;

    /// <summary>
    /// R9-G：學魔法列的物品圖示編號表 word_136D548（TW1608262200.bin SHA-256 e12949cf…641a2，.data VA 0x136D548／FO 0xF6BD48），
    /// 索引＝技能編號 − 1。長度 236：VA 0x136D720（索引 236 起）被 FO 0x47DF53／0x47DF6E 的程式碼另外直接引用，
    /// 內容是 dword 成對的另一張表（46,4／47,5／48,6…），所以表到 0x136D71F 為止（FU9§11.1 的「240 個」多算了尾端 4 個 word）。
    /// 非 0 的 190 個。
    /// </summary>
    public static readonly ushort[] SpellIcons=[
        383,384,385,386,387,657,663,775,388,389,390,391,392,755,651,659,
        393,394,395,396,397,655,848,0,398,399,400,401,402,647,746,793,
        403,404,405,406,407,643,791,749,408,409,410,411,412,661,850,726,
        413,414,415,1578,417,622,1570,758,418,419,420,421,422,649,784,822,
        1579,424,425,426,427,653,752,1571,428,429,1577,1582,432,715,1610,5737,
        0,0,0,0,0,0,1624,1887,1881,2349,2343,0,0,0,0,0,
        1097,1100,1103,1106,1109,1112,1115,1118,1121,1124,1127,1130,1574,1575,1611,4471,
        894,878,875,883,885,1751,881,888,5734,0,0,0,0,0,0,0,
        811,731,846,1755,1744,1731,0,0,743,802,0,0,0,0,0,0,
        820,729,871,772,853,856,761,764,767,867,5731,769,891,799,723,2340,
        720,869,736,796,739,825,1621,781,1737,1756,1734,828,2346,2355,2352,2337,
        0,0,0,0,3079,3076,3083,3087,3181,3074,3081,3090,4559,3183,3088,3093,
        3085,4583,3077,0,0,0,0,0,3122,3098,3130,3116,3102,3096,3121,3095,
        3114,3105,3126,3128,3094,3112,3107,3118,3124,3120,3110,3099,0,0,0,0,
        6191,6180,6189,6188,6458,6501,0,0,0,0,0,0,
    ];
    /// <summary>索引 → 物品圖示編號；表外（原版會讀過界，這裡不畫）回 0。</summary>
    public static ushort SpellIcon(int index)=>(uint)index<(uint)SpellIcons.Length?SpellIcons[index]:(ushort)0;

    /// <summary>解 S41：C(41) D(基價) H(n) n×D(索引)；規則同 NpcServices.cs（n ≤ 256、基價 ≤ int.MaxValue、索引不重複且 &lt; int.MaxValue）。格式不對回 null。</summary>
    public static (uint Basis,int[] Indices)? TryDecodeBuyList(byte[] plain){
        try{
            var r=new LoginReader(plain);if(r.C()!=SBuyList)return null;
            uint basis=r.D();int n=r.H();if(n>256||basis>int.MaxValue)return null;
            var indices=new int[n];var seen=new HashSet<uint>();
            for(int i=0;i<n;i++){uint index=r.D();if(index>=int.MaxValue||!seen.Add(index))return null;indices[i]=(int)index;}
            r.Finish();return (basis,indices);
        }catch(ProtocolException){return null;}
    }
}

/// <summary>
/// 學魔法的 TRADE_WINDOW 資料來源（模式 7）：先看 op 175 的以物品學魔法清單，再看 op 44 的金幣學魔法清單。
/// 左鈕（BUY）依 TradeBox +153 分流：顯示單價（op 44）→ C39；不顯示（op 175）→ C191。每列數量上限 1。
/// </summary>
public sealed class SpellTradeSource(LoginConnection login,Func<bool> busy):ITradeWindowSource {
    public TradeListView? Current=>login.State!=LoginState.InWorld?null:login.World.SpellExchangeTradeWindow()??login.World.SpellBuyTradeWindow();
    public void Submit(IReadOnlyList<ShopChoice> choices,int revision){
        if(busy()||choices.Count==0||choices.Any(c=>c.Count!=1||c.Id>=int.MaxValue))return;
        var w=login.World;
        if(SpellTradeProtocol.IsExchangeRevision(revision)){
            var v=login.SpellExchangeSnapshot();
            if(v.OfferRevision==0||SpellTradeProtocol.ExchangeRevision(v.OfferRevision)!=revision)return;
            _=login.LearnSpellExchange(v,choices.Select(c=>(int)c.Id).ToArray());
        } else {
            if(SpellTradeProtocol.BuyRevision(w.InteractionRevision)!=revision)return;
            _=login.Interact("shop",w.InteractionRevision,w.WorldEpoch,w.TargetRevision,0,choices);
        }
    }
    public void Close(){
        var w=login.World;
        if(w.SpellExchangeOffer!=null){login.CloseSpellExchange(login.SpellExchangeSnapshot());return;}
        if(w.InteractionKind=="skills")_=login.Interact("close",w.InteractionRevision,w.WorldEpoch,w.TargetRevision);
    }
}
