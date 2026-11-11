namespace P06;

/// <summary>R12-A（T428／#985，gap-matrix 15.14.5 第 5:`sub_87C890` 列①）：自己最後一次收到的 S158 動作。
/// At＝WorldSession.ActionClock()；X／Y＝收到當下自己的格座標（之後移動就不再算這個動作）。</summary>
public sealed record OwnActionView(uint Actor,int Epoch,int Action,long At,ushort X,ushort Y);

// 自己的 S158 在主分派的 UpdateObject 找不到物件（自己不在 Objects 內），只會累加 UnknownObjectUpdates（#922 報告第 87 行），
// 所以另用自動發現的觀察器記下來（不改 WorldSession.cs）。觀察器先於主分派執行，不吃掉封包。
// 依據：FU14§3.2（TW16 sub_A46620 放層：自己＋動作 71 會畫；3.81 sub_4E0200 沒有自己例外）、FU14§3.4（伺服器
// FishingPole.java:76 送 S_Fishing(objid, 71, x, y)；x／y 是釣點，不是角色位置）。
// 結束條件（引擎規則，推測；原版是 +action 被下一個動作或移動蓋掉）：
// - 自己再收到別的 S158 動作；
// - 自己的格座標改變（走路、瞬移）；
// - 收到比它新的自己的戰鬥動作（OwnCombat.At 晚於這個動作）；
// - 自己的 S119（伺服器收竿時送外觀更新，同 Fishing.cs 的結束條件）；
// - 世界重設、換角色（Actor 的 id 不同）、WorldEpoch 改變；
// - 死亡。
public sealed partial class WorldSession {
    OwnActionView? ownAction;
    /// <summary>自己最後一次 S158 動作（沒有或已經換世界／換角色時 null）。</summary>
    public OwnActionView? OwnAction=>ownAction is {} a&&Actor is {} own&&a.Actor==own.ObjectId&&a.Epoch==WorldEpoch?a:null;

    [WorldReceive(158,Observe=true)]
    void ObserveOwnAction(byte[] plain){
        uint id;byte action;
        try{var r=new LoginReader(plain);r.C();id=r.D();action=r.C();}
        catch(ProtocolException){return;}   // 格式錯誤留給主分派
        if(id==0||Actor is not {} own||own.ObjectId!=id)return;
        ownAction=new(id,WorldEpoch,action,ActionClock(),own.X,own.Y);
    }

    [WorldReceive(119,Observe=true)]
    void ObserveOwnActionAppearance(byte[] plain){
        uint id;
        try{var r=new LoginReader(plain);r.C();id=r.D();}
        catch(ProtocolException){return;}
        if(Actor?.ObjectId==id)ownAction=null;
    }

    [WorldReset]
    void ResetOwnAction()=>ownAction=null;

    /// <summary>自己目前在釣魚（S158 71）時回開始時間（ActionClock），否則 null。條件見上方註解。</summary>
    internal long? OwnFishingSince(){
        if(OwnAction is not {Action:ListSprLayers.FishingAction} a||Actor is not {} own||OwnDead)return null;
        if(own.X!=a.X||own.Y!=a.Y)return null;
        if(OwnCombat is {} c&&c.At>=a.At)return null;
        return a.At;
    }

    [WorldSnapshot("own-action")]
    object? OwnActionSnapshot()=>OwnAction is {} a?new{action=a.Action,at=a.At,x=a.X,y=a.Y,fishing=OwnFishingSince()!=null}:null;
}

public sealed partial class Renderer {
    /// <summary>自己的釣魚當格（list.spr 動作 71：本體 `&lt;別名&gt;-&lt;304＋朝向&gt;.spr`）。沒有在釣魚、走路中、
    /// 或動作／子檔讀不到時 null（呼叫端照舊畫姿勢，同他人的 AN§3.4 退回）。</summary>
    internal Frame? OwnFishingFrame(WorldSession session,bool walking,long now){
        if(walking||session.Actor is not {} actor||session.OwnFishingSince() is not long at)return null;
        return FishingFrame(actor.Gfx,actor.Direction,now-at);
    }
    /// <summary>自己的釣魚本體＋cloth 附加層（gfx 0 的 6262 釣魚線等；同一個 sid:frame，任何一層缺子檔整組不畫，FU14§3.1）。
    /// 照 3.81 不做職業外型例外（FU14§3.2）。ObjectId＝0（自己）。影子由呼叫端用自己的外型補。</summary>
    internal List<Draw> OwnFishingDraws(OwnActor actor,Frame fish){
        var list=fish.Blocks.Select(b=>new Draw(2*(actor.X-Coordinates.MinX)+b.A,actor.Y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile)).ToList();
        list.AddRange(ClothDraws(actor.Gfx,fish,actor.X,actor.Y));
        return list;
    }
}
