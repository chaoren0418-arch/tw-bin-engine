namespace P06;
public sealed record ExperienceState(byte Level,uint WireLow32,ulong? Total,ulong? LevelStart,ulong? NextLevel,decimal? Percent,string Source,string Limitation);
public sealed record ExperienceChange(long Number,byte Opcode,byte BeforeLevel,byte Level,ulong? Before,ulong? Total,long? Delta);
public sealed record InventoryChange(long Number,byte Opcode,uint ObjectId,ushort DescriptionId,ushort IconId,string Name,uint Before,uint Count,long Delta);
public sealed partial class WorldSession {
    public ExperienceState? Experience {get;private set;}
    public ExperienceChange? LastExperienceChange {get;private set;}
    public long ExperienceChanges {get;private set;}
    readonly List<InventoryChange> inventoryChanges=[];
    public InventoryChange[] InventoryChanges=>inventoryChanges.ToArray();
    long itemChangeNumber;
    public static ExperienceState DecodeExperience(byte level,uint low,string source){
        var t=OriginalExpTable.Thresholds;
        if(level==0||level>=t.Length)return new(level,low,null,null,null,null,source,"等級不在已核對原表；總 EXP 未知");
        ulong start=t[level-1],end=t[level];
        // Do not invent missing high bits even when modulo inference might be possible.
        if(end>uint.MaxValue||low<start||low>=end)return new(level,low,null,start,end,null,source,"原封包僅低32位或級距不符；總 EXP／進度未知");
        return new(level,low,low,start,end,100m*(low-start)/(end-start),source,"");
    }
    void ResetRewards(){Experience=null;LastExperienceChange=null;ExperienceChanges=0;inventoryChanges.Clear();itemChangeNumber=0;}
    void ApplyExperience(byte level,uint low,byte opcode){
        var before=Experience;var next=DecodeExperience(level,low,"S"+opcode);
        if(before!=null&&(before.Level!=level||before.WireLow32!=low))LastExperienceChange=new(++ExperienceChanges,opcode,before.Level,level,before.Total,next.Total,before.Total is ulong a&&next.Total is ulong b?checked((long)b-(long)a):null);
        Experience=next;
        if(Status!=null)Status=Status with{Level=level,Exp=low};
    }
    bool ReceiveExperience(byte opcode,LoginReader r){
        if(opcode!=113)return false;
        byte level=r.C();uint low=r.D();if(r.C()!=1)throw new ProtocolException("unexpected original EXP marker");r.Finish();
        if(Status==null)throw new ProtocolException("EXP update before own status");
        ApplyExperience(level,low,opcode);return true;
    }
    void RecordInventoryChanges(byte opcode,InventoryItem[] before){
        var old=before.ToDictionary(i=>i.ObjectId);var after=Inventory.ToDictionary(i=>i.ObjectId);
        foreach(uint id in old.Keys.Union(after.Keys)){
            old.TryGetValue(id,out var a);after.TryGetValue(id,out var b);uint from=a?.Count??0,to=b?.Count??0;if(from==to)continue;var item=b??a!;
            inventoryChanges.Add(new(++itemChangeNumber,opcode,id,item.DescriptionId,item.IconId,item.Name,from,to,(long)to-from));
        }
        if(inventoryChanges.Count>32)inventoryChanges.RemoveRange(0,inventoryChanges.Count-32);
    }
    public object RewardsSnapshot()=>new{experience=Experience,lastExperienceChange=LastExperienceChange,experienceChanges=ExperienceChanges,inventoryChanges=InventoryChanges,notice="原回包狀態差值；背包增加的來源需由實際戰鬥證據核對"};
    public string ExperienceText=>Experience is {} e?$"Lv.{e.Level}  EXP {(e.Total?.ToString()??"未知（低32位 "+e.WireLow32+"）")}\n"+(e.Percent is decimal p?$"本級 {p:F6}% · 下一級 {e.NextLevel}":e.Limitation)+(LastExperienceChange is {} d?$"\n原回包變化 {(d.Delta is long x?x.ToString("+0;-0;0"):"未知")}":""):"等待原服 EXP";
}
public sealed partial class ClientForm {
    readonly Label rewardsLabel=new(){Location=new(270,150),Size=new(440,154),ForeColor=Color.White,BackColor=Color.FromArgb(23,38,41),Padding=new(5),Visible=false};
    void InitRewards(){Controls.Add(rewardsLabel);rewardsLabel.BringToFront();}
    void SyncRewards(){rewardsLabel.Visible=login.State==LoginState.InWorld;rewardsLabel.Text=login.World.ExperienceText+(login.World.Status is {} st?$"\n{login.World.AcNotice} · HP {st.Hp}/{st.MaxHp} · MP {st.Mp}/{st.MaxMp}\n力 {st.Strength} 敏 {st.Dexterity} 體 {st.Constitution} 智 {st.Intelligence} 精 {st.Wisdom} 魅 {st.Charisma}":"")+(login.World.InventoryChanges.LastOrDefault() is {} d?$"\n原背包 {(objectAssets??=new(World.Assets.Root)).Name(d.Name)}：{d.Before} → {d.Count}":"");}
}
