using P06.NativeUi;
namespace P06.NativeUi {
    // ui-red-notice (Issue #474). Read-only adapter over the existing DisplayNotices queue (WorldSession.redNotice, one entry,
    // overwritten by each S105 / S_BlueMessage). No packet parsing; the 15 s / slot rules live in RedNoticePanel (V2).

    /// <summary>
    /// Latest: the last red notice of this session (null = none, or not entered). WorldEpoch: changes on world/map reset.
    /// Table: string-c.tbl (null = unavailable → the engine's DisplayNotices text). Resolve: "$nnn" name lookup (null = none).
    /// </summary>
    public sealed record RedNoticeState(RedNotice? Latest,int WorldEpoch,ServerStringTable? Table,Func<string,string>? Resolve) {
        public static readonly RedNoticeState Empty=new(null,0,null,null);

        /// <summary>Finds the adapter through the bridge wrappers (HpMp → Macro → LeftStatus → Chat → Party → Buff → RedNotice).</summary>
        public static RedNoticeState? Of(IUiGameState? state){
            for(int depth=0;state!=null&&depth<10;depth++){
                if(state is IUiRedNoticeState r)return r.RedNotice;
                state=state switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,UiLeftStatusGameState l=>l.Inner,UiChatGameState c=>c.Inner,UiPartyGameState q=>q.Inner,UiBuffGameState b=>b.Inner,UiRedNoticeGameState n=>n.Inner,_=>null};
            }
            return null;
        }
    }

    public interface IUiRedNoticeState { RedNoticeState RedNotice { get; } }

    /// <summary>Decorator: the shared IUiGameState plus the red-notice snapshot. Innermost wrapper of the bridge chain.</summary>
    public sealed class UiRedNoticeGameState(IUiGameState inner,RedNoticeState notice):IUiGameState,IUiRedNoticeState {
        public IUiGameState Inner=>inner;
        public RedNoticeState RedNotice=>notice;
        public bool InWorld=>inner.InWorld;
        public string? CharacterName=>inner.CharacterName;
        public int? Level=>inner.Level;
        public long? Hp=>inner.Hp;
        public long? MaxHp=>inner.MaxHp;
        public long? Mp=>inner.Mp;
        public long? MaxMp=>inner.MaxMp;
        public long Now=>inner.Now;
    }
}

namespace P06 {
    public sealed partial class WorldSession {
        /// <summary>The stored S105 notice without the legacy 5 s window (the native panel applies V2's 15 s); null before entering.</summary>
        internal RedNotice? LatestRedNotice=>Entered?redNotice:null;
    }
    public sealed partial class ClientForm {
        /// <summary>Adds the red-notice snapshot (ui-red-notice #474).</summary>
        IUiGameState WithRedNoticeState(IUiGameState state){
            if(!state.InWorld)return new UiRedNoticeGameState(state,RedNoticeState.Empty);
            var w=login.World;
            return new UiRedNoticeGameState(state,new RedNoticeState(w.LatestRedNotice,w.WorldEpoch,ServerMessageText.Table,ResolveNpcText));
        }
        /// <summary>true = the native red-notice panel draws S105, so the legacy black-box centre line must not be drawn too.</summary>
        bool NativeRedNoticeActive=>NativeUiActive&&nativeUiRoot?.Find(RedNoticePanel.PanelId) is {} p&&!nativeUiRoot.Faulted.Contains(p);
    }
}
